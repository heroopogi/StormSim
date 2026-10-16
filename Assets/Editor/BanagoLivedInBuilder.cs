using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Adds a reversible barangay surface-and-prop pass without moving existing geometry.</summary>
public static class BanagoLivedInBuilder
{
    public const string RootName="Banago Lived-in Coastal Barangay - September redesign";
    public static string Status="Not started";
    public static string Folder;
    static GameObject root;
    static BanagoLivedInReview review;
    static int next;
    sealed class House { public string id,tile; public Vector2[] polygon; public Vector2 center; public bool coastal; public uint seed; }
    struct Tri { public Vector3 a,b,c; public float minX,maxX,minZ,maxZ; }
    sealed class Original { public Transform t; public Vector3 p,s; public Quaternion q; public bool active; public Mesh mesh; }
    static List<House> houses;
    static List<Original> original;
    static Dictionary<string,List<Tri>> roofs;
    static Dictionary<Vector2Int,List<House>> grid;
    static MeshCollider[] ground,roads;
    static List<Tri> roadTriangles;
    static Bounds[] ponds;
    static List<Vector3> reservations;
    static Dictionary<string,Mesh[]> models;
    static Dictionary<string,Transform> groups;
    static Dictionary<string,int> counts;
    static Material props,nipa,foliage;
    static string snapshotFolder="RoadRepairBackup/BarangayRedesign_20260912";

    static uint Seed(string s){unchecked{uint v=2166136261;foreach(char c in s)v=(v^c)*16777619;return v;}}
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool result=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)result=!result;return result;}
    static float EdgeDistance(Vector2 p,Vector2[] poly)
    {float d=float.MaxValue;for(int i=0;i<poly.Length;i++){var a=poly[i];var ab=poly[(i+1)%poly.Length]-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.000001f,ab.sqrMagnitude));d=Mathf.Min(d,Vector2.Distance(p,a+ab*t));}return d;}
    static List<Tri> Triangles(MeshFilter f)
    {
        var m=f.sharedMesh;var vs=m.vertices;var ix=m.triangles;var result=new List<Tri>();
        for(int i=0;i<ix.Length;i+=3){var a=f.transform.TransformPoint(vs[ix[i]]);var b=f.transform.TransformPoint(vs[ix[i+1]]);var c=f.transform.TransformPoint(vs[ix[i+2]]);if(Vector3.Cross(b-a,c-a).normalized.y<.25f)continue;result.Add(new Tri{a=a,b=b,c=c,minX=Mathf.Min(a.x,b.x,c.x),maxX=Mathf.Max(a.x,b.x,c.x),minZ=Mathf.Min(a.z,b.z,c.z),maxZ=Mathf.Max(a.z,b.z,c.z)});}return result;
    }
    static bool RoofAt(House h,Vector2 p,out float y)
    {
        y=-999;foreach(var t in roofs[h.tile]){if(p.x<t.minX||p.x>t.maxX||p.y<t.minZ||p.y>t.maxZ)continue;float den=(t.b.z-t.c.z)*(t.a.x-t.c.x)+(t.c.x-t.b.x)*(t.a.z-t.c.z);if(Mathf.Abs(den)<.000001f)continue;float a=((t.b.z-t.c.z)*(p.x-t.c.x)+(t.c.x-t.b.x)*(p.y-t.c.z))/den;float b=((t.c.z-t.a.z)*(p.x-t.c.x)+(t.a.x-t.c.x)*(p.y-t.c.z))/den;float c=1-a-b;if(a>=-.00001f&&b>=-.00001f&&c>=-.00001f)y=Mathf.Max(y,a*t.a.y+b*t.b.y+c*t.c.y);}return y>-998;
    }
    static bool RoofSpot(House h,float radius,out Vector3 result)
    {
        result=default;var p=h.center;float best=Inside(p,h.polygon)?EdgeDistance(p,h.polygon):0;
        float minX=h.polygon.Min(v=>v.x),maxX=h.polygon.Max(v=>v.x),minZ=h.polygon.Min(v=>v.y),maxZ=h.polygon.Max(v=>v.y);
        for(int i=1;i<6;i++)for(int j=1;j<6;j++){var q=new Vector2(Mathf.Lerp(minX,maxX,i/6f),Mathf.Lerp(minZ,maxZ,j/6f));if(!Inside(q,h.polygon))continue;float d=EdgeDistance(q,h.polygon);if(d>best){best=d;p=q;}}
        if(best<radius||!RoofAt(h,p,out var y))return false;
        result=new Vector3(p.x,y+.16f,p.y);return true;
    }
    static bool GroundHeight(Vector2 p,out float y)
    {
        y=-999;var ray=new Ray(new Vector3(p.x,60,p.y),Vector3.down);
        foreach(var c in ground){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)continue;if(c.Raycast(ray,out var hit,140)&&hit.normal.y>.75f)y=Mathf.Max(y,hit.point.y);}return y>-998;
    }
    static bool IsRoadCollider(MeshCollider c)
    {
        if(!c.enabled||!c.sharedMesh||c.name.StartsWith("Measured terrain",StringComparison.OrdinalIgnoreCase))return false;
        // Street objects use their actual names (e.g. Felix Amante Drive), not necessarily "road".
        return AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/StudyRoads_")||
            c.name.IndexOf("road",StringComparison.OrdinalIgnoreCase)>=0||
            c.name.IndexOf("street",StringComparison.OrdinalIgnoreCase)>=0||
            c.name.IndexOf("asphalt",StringComparison.OrdinalIgnoreCase)>=0;
    }
    static List<Tri> RoadTriangles(IEnumerable<MeshCollider> surfaces)
    {
        var result=new List<Tri>();
        foreach(var c in surfaces)
        {
            var vs=c.sharedMesh.vertices;var ix=c.sharedMesh.triangles;
            for(int i=0;i<ix.Length;i+=3)
            {
                var a=c.transform.TransformPoint(vs[ix[i]]);var b=c.transform.TransformPoint(vs[ix[i+1]]);var d=c.transform.TransformPoint(vs[ix[i+2]]);
                if(Mathf.Abs(Vector3.Cross(b-a,d-a).y)<.00001f)continue;
                result.Add(new Tri{a=a,b=b,c=d,minX=Mathf.Min(a.x,b.x,d.x),maxX=Mathf.Max(a.x,b.x,d.x),minZ=Mathf.Min(a.z,b.z,d.z),maxZ=Mathf.Max(a.z,b.z,d.z)});
            }
        }
        return result;
    }
    static bool ClearRoadSpace(Vector2 p,float radius,List<Tri> triangles)
    {
        // Exact projected circle/triangle clearance, including narrow lane edges between ray samples.
        foreach(var t in triangles)
        {
            if(p.x<t.minX-radius||p.x>t.maxX+radius||p.y<t.minZ-radius||p.y>t.maxZ+radius)continue;
            var poly=new[]{new Vector2(t.a.x,t.a.z),new Vector2(t.b.x,t.b.z),new Vector2(t.c.x,t.c.z)};
            if(Inside(p,poly)||EdgeDistance(p,poly)<radius)return false;
        }
        return true;
    }
    static float GroundPropRadius(string name)
    {return name=="Courtyard laundry lines"?1.25f:name=="Banana courtyard clusters"?1.95f:name=="Bamboo nipa shelters"?1.98f:name=="Coconut palms"?2.25f:0;}
    public static object ValidateExistingRoadClearance()
    {
        var existing=GameObject.Find(RootName);if(!existing)throw new Exception("Redesign root not found");
        var surfaces=existing.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(IsRoadCollider).ToArray();
        var triangles=RoadTriangles(surfaces);var issues=new List<string>();int checkedProps=0;
        foreach(Transform group in existing.transform)
        {
            float radius=GroundPropRadius(group.name);if(radius==0)continue;
            foreach(Transform t in group){if(!t.gameObject.activeInHierarchy)continue;checkedProps++;if(!ClearRoadSpace(new Vector2(t.position.x,t.position.z),radius,triangles))issues.Add(group.name+"/"+t.name);}
        }
        if(surfaces.Length==0||triangles.Count==0)throw new Exception("No road surfaces available for validation");
        return new{roadColliders=surfaces.Length,roadTriangles=triangles.Count,checkedProps,overlaps=issues.ToArray()};
    }
    static bool ClearLot(Vector2 p,float radius)
    {
        int gx=Mathf.FloorToInt(p.x/25),gz=Mathf.FloorToInt(p.y/25);
        for(int x=gx-1;x<=gx+1;x++)for(int z=gz-1;z<=gz+1;z++)if(grid.TryGetValue(new Vector2Int(x,z),out var near))foreach(var h in near)if(Inside(p,h.polygon)||EdgeDistance(p,h.polygon)<radius+.12f)return false;
        foreach(var r in reservations)if(Vector2.Distance(p,new Vector2(r.x,r.z))<radius+r.y+.25f)return false;
        foreach(var b in ponds)if(p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.y>b.min.z-radius&&p.y<b.max.z+radius)return false;
        if(!ClearRoadSpace(p,radius,roadTriangles))return false;
        return true;
    }
    static bool LotSpot(House h,float radius,out Vector3 result,out float yaw)
    {
        result=default;yaw=0;if(h.coastal)return false;
        for(int j=0;j<h.polygon.Length;j++)
        {
            int i=(j+(int)(h.seed%(uint)h.polygon.Length))%h.polygon.Length;var a=h.polygon[i];var b=h.polygon[(i+1)%h.polygon.Length];var edge=b-a;if(edge.magnitude<radius*1.5f)continue;var dir=edge.normalized;var outward=new Vector2(dir.y,-dir.x);var p=(a+b)*.5f+outward*(radius+.45f);
            if(!ClearLot(p,radius)||!GroundHeight(p,out var y))continue;float low=y,high=y;bool valid=true;
            for(int k=0;k<4;k++){var q=p+new Vector2(k%2==0?-radius:radius,k<2?-radius:radius)*.70f;if(!GroundHeight(q,out var yy)){valid=false;break;}low=Mathf.Min(low,yy);high=Mathf.Max(high,yy);}
            if(!valid||high-low>.35f)continue;result=new Vector3(p.x,high+.025f,p.y);yaw=Mathf.Atan2(outward.x,outward.y)*Mathf.Rad2Deg;reservations.Add(new Vector3(p.x,radius,p.y));return true;
        }return false;
    }
    static Material MaterialAsset(string name,float kind,float rough,float metal)
    {var m=new Material(Shader.Find("StormSim/Banago Lived In PBR")){name=name,enableInstancing=true};m.SetFloat("_Kind",kind);m.SetFloat("_Weathering",.72f);m.SetFloat("_Smoothness",rough);m.SetFloat("_Metallic",metal);m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static void Add(string kind,Vector3 p,float yaw,int variant,float scale=1)
    {
        if(!groups.TryGetValue(kind,out var group)){var g=new GameObject(kind);g.transform.SetParent(root.transform,false);groups[kind]=group=g.transform;}
        int index=counts.ContainsKey(kind)?counts[kind]+1:1;counts[kind]=index;
        var go=new GameObject(kind+" "+index.ToString("000"));go.transform.SetParent(group,false);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        go.AddComponent<MeshFilter>().sharedMesh=models[kind][variant%models[kind].Length];var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=kind=="Bamboo nipa shelters"?nipa:kind=="Banana courtyard clusters"||kind=="Coconut palms"?foliage:props;
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(kind=="Rooftop satellite dishes"?.012f:.006f,new Renderer[]{r})});lod.RecalculateBounds();
        if(kind=="Bamboo nipa shelters"){var c=go.AddComponent<BoxCollider>();c.center=new Vector3(0,.25f,0);c.size=new Vector3(2,.15f,1.9f);}
    }
    public static string Start()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Open MAIN_GAME outside Play mode");
        if(GameObject.Find(RootName))throw new Exception("Redesign already exists; refusing duplicate generation");
        if(!File.Exists(snapshotFolder+"/MAIN_GAME.before-redesign.unity"))throw new Exception("Verified scene backup required");
        var shader=Shader.Find("StormSim/Banago Lived In PBR");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Redesign shader not compiled successfully");
        var houseRoot=GameObject.Find("Banago Houses - full detail redesign");if(!houseRoot||houseRoot.transform.childCount!=103)throw new Exception("Existing house layout differs");
        original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>new Original{t=t,p=t.localPosition,q=t.localRotation,s=t.localScale,active=t.gameObject.activeSelf,mesh=t.GetComponent<MeshFilter>()?t.GetComponent<MeshFilter>().sharedMesh:null}).ToList();
        Folder="Assets/StormSim/Generated/BanagoLivedIn_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        root=new GameObject(RootName);root.transform.SetParent(houseRoot.transform.parent,false);Undo.RegisterCreatedObjectUndo(root,"Add reversible coastal barangay redesign");
        review=root.AddComponent<BanagoLivedInReview>();review.previousFog=RenderSettings.fog;review.previousFogMode=RenderSettings.fogMode;review.previousFogColor=RenderSettings.fogColor;review.previousFogDensity=RenderSettings.fogDensity;
        var walls=MaterialAsset("Chalk paint exposed CHB and damp streaks",0,.12f,0);var roofsMat=MaterialAsset("Patchwork galvanized iron rust and occasional tile",1,.28f,.6f);var detailMat=MaterialAsset("Weathered door frames and house trims",2,.12f,.03f);
        props=MaterialAsset("PVC cloth wood and household details",2,.22f,.08f);nipa=MaterialAsset("Dry nipa and split bamboo fibers",3,.04f,0);foliage=MaterialAsset("Tropical banana and coconut foliage",2,.12f,0);
        var swaps=new List<BanagoLivedInReview.MaterialSwap>();foreach(var r in houseRoot.GetComponentsInChildren<MeshRenderer>()){var old=r.sharedMaterials;var filter=r.GetComponent<MeshFilter>();int slots=Mathf.Max(old.Length,filter&&filter.sharedMesh?filter.sharedMesh.subMeshCount:1);var replacement=Enumerable.Range(0,slots).Select(_=>r.name=="plaster"?walls:r.name=="metal"?roofsMat:detailMat).ToArray();swaps.Add(new BanagoLivedInReview.MaterialSwap{target=r,original=old,replacement=replacement});Undo.RecordObject(r,"Apply reversible barangay surfaces");}review.swaps=swaps.ToArray();review.useHaze=true;review.Apply();
        var records=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json"));houses=new List<House>();grid=new Dictionary<Vector2Int,List<House>>();
        foreach(var d in records){var poly=d["footprint"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();if(poly[0]==poly[poly.Length-1])poly=poly.Take(poly.Length-1).ToArray();var h=new House{id=(string)d["id"],tile=(string)d["tile"],polygon=poly,coastal=(bool)d["coastal"],seed=Seed((string)d["id"]),center=new Vector2(poly.Average(v=>v.x),poly.Average(v=>v.y))};houses.Add(h);int x0=Mathf.FloorToInt(poly.Min(v=>v.x)/25),x1=Mathf.FloorToInt(poly.Max(v=>v.x)/25),z0=Mathf.FloorToInt(poly.Min(v=>v.y)/25),z1=Mathf.FloorToInt(poly.Max(v=>v.y)/25);for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++){var k=new Vector2Int(x,z);if(!grid.TryGetValue(k,out var list))grid[k]=list=new List<House>();list.Add(h);}}
        roofs=new Dictionary<string,List<Tri>>();foreach(Transform t in houseRoot.transform)roofs[t.name.Replace("Neighbourhood ","")]=Triangles(t.Find("metal").GetComponent<MeshFilter>());
        var colliders=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh).ToArray();ground=colliders.Where(c=>c.name.StartsWith("Measured terrain",StringComparison.OrdinalIgnoreCase)).ToArray();roads=colliders.Where(IsRoadCollider).ToArray();roadTriangles=RoadTriangles(roads);
        ponds=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name.IndexOf("pond",StringComparison.OrdinalIgnoreCase)>=0).Select(r=>r.bounds).ToArray();if(ground.Length==0||roads.Length==0)throw new Exception("Ground and road safety checks unavailable");
        models=new Dictionary<string,Mesh[]>{{"Ribbed PVC rooftop tanks",Enumerable.Range(0,2).Select(BanagoLivedInModels.Tank).ToArray()},{"Rooftop satellite dishes",Enumerable.Range(0,2).Select(BanagoLivedInModels.Dish).ToArray()},{"Courtyard laundry lines",Enumerable.Range(0,3).Select(BanagoLivedInModels.Laundry).ToArray()},{"Banana courtyard clusters",Enumerable.Range(0,3).Select(BanagoLivedInModels.Banana).ToArray()},{"Bamboo nipa shelters",Enumerable.Range(0,2).Select(BanagoLivedInModels.Kubo).ToArray()},{"Coconut palms",Enumerable.Range(0,2).Select(i=>BanagoEnvironmentModels.Tree(2,i,true,new Color(.30f,.40f,.15f))).ToArray()}};
        foreach(var pair in models)for(int i=0;i<pair.Value.Length;i++)AssetDatabase.CreateAsset(pair.Value[i],Folder+"/"+pair.Key+" "+i+".asset");
        groups=new Dictionary<string,Transform>();counts=new Dictionary<string,int>();reservations=new List<Vector3>();next=0;Status="Applying details to 2257 houses";EditorApplication.update+=Tick;return Status;
    }
    static void Tick()
    {
        try
        {
            int end=Mathf.Min(next+35,houses.Count);for(;next<end;next++)
            {
                var h=houses[next];int variant=(int)(h.seed%3);float yaw=h.seed%360;
                if(h.seed%11==0&&RoofSpot(h,.95f,out var tank))Add("Ribbed PVC rooftop tanks",tank,yaw,variant);
                else if(h.seed%7==2&&RoofSpot(h,.48f,out var dish))Add("Rooftop satellite dishes",dish,yaw,variant);
                if(h.seed%13==4&&LotSpot(h,1.25f,out var line,out var y1))Add("Courtyard laundry lines",line,y1,variant);
                else if(h.seed%17==6&&LotSpot(h,GroundPropRadius("Banana courtyard clusters"),out var banana,out var y2))Add("Banana courtyard clusters",banana,y2,variant);
                else if(h.seed%53==7&&LotSpot(h,GroundPropRadius("Bamboo nipa shelters"),out var hut,out var y3))Add("Bamboo nipa shelters",hut,y3,variant);
                else if(h.seed%41==9&&LotSpot(h,GroundPropRadius("Coconut palms"),out var palm,out var y4))
                {
                    Add("Coconut palms",palm,y4,variant);var t=groups["Coconut palms"].GetChild(groups["Coconut palms"].childCount-1);t.localScale=new Vector3(2.5f,8.2f+(h.seed%4)*.5f,2.5f);
                }
            }
            Status="Dressed houses "+next+" / "+houses.Count;if(next<houses.Count)return;
            EditorApplication.update-=Tick;
            var failures=new List<string>();foreach(var o in original)if(!o.t||o.t.localPosition!=o.p||o.t.localRotation!=o.q||o.t.localScale!=o.s||o.t.gameObject.activeSelf!=o.active||(o.t.GetComponent<MeshFilter>()&&o.t.GetComponent<MeshFilter>().sharedMesh!=o.mesh))failures.Add(o.t?o.t.name:"missing original");
            if(failures.Count>0)throw new Exception("Original object preservation failed: "+string.Join(", ",failures.Take(8)));
            review.notes="Reversible PBR weathering for 2257 existing houses. Original meshes, footprints, ground, roads, water and buildings retained. Shared instanced props are representative, not surveyed locations. Disable this entire root to restore previous materials and fog. "+string.Join("; ",counts.Select(p=>p.Key+": "+p.Value));
            var report=new{existingObjectsVerified=original.Count,oldTransformsOrMeshesChanged=failures.Count,houseMaterialSwaps=review.swaps.Length,footprintsUnchanged=houses.Count,counts=counts,groundColliders=ground.Length,roadColliders=roads.Length,generatedFolder=Folder};
            File.WriteAllText(Folder+"/Redesign report.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);Selection.activeGameObject=root;
            Status="Ready for visual review. "+string.Join("; ",counts.Select(p=>p.Key+" "+p.Value));
        }
        catch(Exception e){EditorApplication.update-=Tick;Status="Stopped: "+e.Message;if(review)root.SetActive(false);Debug.LogException(e);}
    }
    public static string Capture(string name,Vector3 eye,Vector3 target)
    {
        // Own temporary capture objects only; no existing game camera is moved or modified.
        var go=new GameObject("Barangay review capture - temporary"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
        var rt=new RenderTexture(1400,900,24,RenderTextureFormat.ARGB32);Texture2D image=null;var previous=RenderTexture.active;
        try{cam.enabled=false;cam.transform.position=eye;cam.transform.LookAt(target);cam.fieldOfView=49;cam.aspect=1400f/900;cam.nearClipPlane=.1f;cam.farClipPlane=2500;cam.clearFlags=CameraClearFlags.Skybox;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image=new Texture2D(1400,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();var path=snapshotFolder+"/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());return path;}
        finally{RenderTexture.active=previous;cam.targetTexture=null;if(image)UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
