using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Additive yard planting with exact road/footprint checks and shared LOD meshes.</summary>
public static class BanagoDenseCanopyBuilder
{
    public const string RootName="Banago Dense Tropical Canopy - September redesign";
    const string Backup="RoadRepairBackup/DenseCanopy_20260912/MAIN_GAME.before-dense-canopy.unity";
    public static string Status="Not started",Folder;
    sealed class House { public string id,tile;public uint seed;public Vector2[] poly;public Vector2 center;public float top;public bool coastal; }
    sealed class Triangle { public Vector3 a,b,c;public Vector2[] poly;public Bounds bounds; }
    sealed class Original {public Transform t;public Vector3 p,s;public Quaternion r;public bool active;public Mesh mesh;public Material[] mats;}
    sealed class Circle {public Vector2 p;public float radius;public Transform owner;}
    static List<House> houses;
    static Dictionary<Vector2Int,List<House>> houseGrid;
    static Dictionary<Vector2Int,List<Triangle>> roadGrid,roofGrid;
    static MeshCollider[] ground,roads;
    static List<Bounds> protectedBounds;
    static List<Circle> occupied;
    static List<Original> originals;
    static GameObject root;
    static BanagoDenseCanopyReview review;
    static List<BanagoDenseCanopyReview.Plant> plants;
    static Dictionary<string,Mesh[][]> meshes;
    static Dictionary<string,Transform> groups;
    static Material material;
    static int next,phase;
    static uint Seed(string s){unchecked{uint h=2166136261;foreach(char c in s)h=(h^c)*16777619;return h;}}
    static float R(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool result=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)result=!result;return result;}
    static float EdgeDistance(Vector2 p,Vector2[] poly)
    {float d=float.MaxValue;for(int i=0;i<poly.Length;i++){var a=poly[i];var ab=poly[(i+1)%poly.Length]-a;d=Mathf.Min(d,Vector2.Distance(p,a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.00001f,ab.sqrMagnitude))));}return d;}
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static bool Streamed(Transform t){for(var p=t;p;p=p.parent)if(p.name.StartsWith("https://assets.ion.cesium"))return true;return false;}
    static void GridAdd<T>(Dictionary<Vector2Int,List<T>> grid,Vector2 min,Vector2 max,T item)
    {for(int x=Mathf.FloorToInt(min.x/25);x<=Mathf.FloorToInt(max.x/25);x++)for(int z=Mathf.FloorToInt(min.y/25);z<=Mathf.FloorToInt(max.y/25);z++){var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<T>();list.Add(item);}}
    static IEnumerable<T> Nearby<T>(Dictionary<Vector2Int,List<T>> grid,Vector2 p,float radius)
    {var found=new HashSet<T>();for(int x=Mathf.FloorToInt((p.x-radius)/25);x<=Mathf.FloorToInt((p.x+radius)/25);x++)for(int z=Mathf.FloorToInt((p.y-radius)/25);z<=Mathf.FloorToInt((p.y+radius)/25);z++)if(grid.TryGetValue(new Vector2Int(x,z),out var list))foreach(var item in list)if(found.Add(item))yield return item;}
    static void AddTriangles(Dictionary<Vector2Int,List<Triangle>> grid,Mesh mesh,Transform transform,bool roofs)
    {
        var vs=mesh.vertices;var ix=mesh.triangles;
        for(int i=0;i<ix.Length;i+=3){var a=transform.TransformPoint(vs[ix[i]]);var b=transform.TransformPoint(vs[ix[i+1]]);var c=transform.TransformPoint(vs[ix[i+2]]);var normal=Vector3.Cross(b-a,c-a);if(Mathf.Abs(normal.y)<.00001f||(roofs&&normal.normalized.y<.25f))continue;var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);var t=new Triangle{a=a,b=b,c=c,poly=new[]{XZ(a),XZ(b),XZ(c)},bounds=bounds};GridAdd(grid,XZ(bounds.min),XZ(bounds.max),t);}
    }
    static bool Road(MeshCollider c)
    {return c.enabled&&c.sharedMesh&&!c.name.StartsWith("Measured terrain")&&(AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/StudyRoads_")||c.name.IndexOf("road",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("street",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("asphalt",StringComparison.OrdinalIgnoreCase)>=0);}
    static float RoadDistance(Vector2 p,float maxDistance=30)
    {float d=maxDistance;foreach(var t in Nearby(roadGrid,p,maxDistance)){if(Inside(p,t.poly))return 0;d=Mathf.Min(d,EdgeDistance(p,t.poly));}return d;}
    static bool Ground(Vector2 p,out float y)
    {y=-999;foreach(var c in ground){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,60,p.y),Vector3.down),out var hit,140)&&hit.normal.y>.82f)y=Mathf.Max(y,hit.point.y);}return y>-998;}
    static float BaseClearance(string kind)=>kind=="Coconut palms"?1.6f:kind=="Mango shade trees"?1.55f:kind=="Banana yard clusters"?2.0f:1.55f;
    static float RoadClearance(string kind)=>kind=="Coconut palms"?2.35f:kind=="Mango shade trees"?2.7f:kind=="Banana yard clusters"?2.05f:1.8f;
    static float Spacing(string kind)=>kind=="Coconut palms"?1.8f:kind=="Mango shade trees"?2.7f:kind=="Banana yard clusters"?1.8f:1.35f;
    static bool CoreClear(Vector2 p,string kind,Transform self=null)
    {
        float radius=BaseClearance(kind);
        foreach(var h in Nearby(houseGrid,p,radius+1))if(Inside(p,h.poly)||EdgeDistance(p,h.poly)<radius+.25f)return false;
        if(RoadDistance(p,RoadClearance(kind)+.1f)<RoadClearance(kind))return false;
        foreach(var b in protectedBounds)if(p.x>b.min.x-radius&&p.x<b.max.x+radius&&p.y>b.min.z-radius&&p.y<b.max.z+radius)return false;
        foreach(var c in occupied)if(c.owner!=self&&Vector2.Distance(p,c.p)<Spacing(kind)+c.radius)return false;
        return true;
    }
    static float NearbyRoof(Vector2 p,float radius,float groundY)
    {float top=groundY+2;foreach(var h in Nearby(houseGrid,p,radius))if(Inside(p,h.poly)||EdgeDistance(p,h.poly)<radius)top=Mathf.Max(top,h.top);return top;}
    static Bounds? ObjectBounds(Transform t)
    {var rs=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(rs.Length==0)return null;var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static void PrepareContext()
    {
        var scene=EditorSceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();var cs=roots.SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.enabled&&c.sharedMesh).ToArray();ground=cs.Where(c=>c.name.StartsWith("Measured terrain")).ToArray();roads=cs.Where(Road).ToArray();if(ground.Length==0||roads.Length<300)throw new Exception("Full measured ground/road coverage is required");
        roadGrid=new Dictionary<Vector2Int,List<Triangle>>();foreach(var c in roads)AddTriangles(roadGrid,c.sharedMesh,c.transform,false);
        roofGrid=new Dictionary<Vector2Int,List<Triangle>>();var homeRoot=GameObject.Find("Banago Houses - full detail redesign");if(!homeRoot||homeRoot.transform.childCount!=103)throw new Exception("House layout changed; inspect before planting");foreach(Transform tile in homeRoot.transform){var f=tile.Find("metal").GetComponent<MeshFilter>();AddTriangles(roofGrid,f.sharedMesh,f.transform,true);}
        houses=new List<House>();houseGrid=new Dictionary<Vector2Int,List<House>>();
        foreach(var d in JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")))
        {
            var poly=d["footprint"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();if(poly[0]==poly[poly.Length-1])poly=poly.Take(poly.Length-1).ToArray();var min=new Vector2(poly.Min(p=>p.x),poly.Min(p=>p.y));var max=new Vector2(poly.Max(p=>p.x),poly.Max(p=>p.y));var h=new House{id=(string)d["id"],tile=(string)d["tile"],coastal=(bool)d["coastal"],poly=poly,center=(min+max)*.5f,seed=Seed((string)d["id"]),top=-999};
            foreach(var t in Nearby(roofGrid,h.center,Vector2.Distance(min,max)*.5f+.5f))if(t.poly.Any(p=>Inside(p,poly)||EdgeDistance(p,poly)<.15f)||Inside(XZ((t.a+t.b+t.c)/3),poly))h.top=Mathf.Max(h.top,t.a.y,t.b.y,t.c.y);houses.Add(h);GridAdd(houseGrid,min,max,h);
        }
        protectedBounds=roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name.IndexOf("pond",StringComparison.OrdinalIgnoreCase)>=0).Select(r=>r.bounds).ToList();occupied=new List<Circle>();
        var oldTrees=GameObject.Find("Trees - satellite canopy locations");if(oldTrees)foreach(Transform t in oldTrees.transform)occupied.Add(new Circle{p=XZ(t.position),radius=1.15f,owner=t});
        var life=GameObject.Find("Banago Neighborhood Life - reference additions");
        if(life)foreach(Transform group in life.transform)
        {
            if(new[]{"store","frontage","jeepney","tricycle","car","streetlight","drainage","flowers"}.Contains(group.name))foreach(Transform t in group){var b=ObjectBounds(t);if(b.HasValue)protectedBounds.Add(b.Value);}
            else if(group.name=="mango")foreach(Transform t in group)occupied.Add(new Circle{p=XZ(t.position),radius=1.7f,owner=t});
        }
        var oldDetails=GameObject.Find(BanagoLivedInBuilder.RootName);if(oldDetails)foreach(Transform group in oldDetails.transform)
        {if(group.name.Contains("rooftop")||group.name.Contains("satellite"))continue;foreach(Transform t in group){if(group.name=="Coconut palms")occupied.Add(new Circle{p=XZ(t.position),radius=1.25f,owner=t});else{var b=ObjectBounds(t);if(b.HasValue)protectedBounds.Add(b.Value);}}}
        var civic=GameObject.Find("Banago Architecture - roadside homes and public buildings");if(civic)foreach(Transform t in civic.transform)if(t.name!="House upper-floor slabs"){var b=ObjectBounds(t);if(b.HasValue)protectedBounds.Add(b.Value);}
    }
    static bool FindSpot(House h,string kind,int pass,out Vector3 result,out float yaw,out float height,out float crownRadius,out float crownFloor)
    {
        result=default;yaw=height=crownRadius=crownFloor=0;if(h.top<-998)return false;
        var rng=new System.Random(unchecked((int)(h.seed+Seed(kind)+(uint)pass*7919)));float radius=BaseClearance(kind);
        // Prefer rear/side edges, away from the road-facing entrance, with irregular edge positions.
        var edges=Enumerable.Range(0,h.poly.Length).Select(i=>new{i,d=RoadDistance((h.poly[i]+h.poly[(i+1)%h.poly.Length])*.5f)+R(rng,0,4)}).OrderByDescending(e=>e.d).ToArray();
        for(int attempt=0;attempt<32;attempt++)
        {
            int i=edges[attempt%edges.Length].i;var a=h.poly[i];var b=h.poly[(i+1)%h.poly.Length];var edge=b-a;if(edge.magnitude<1.5f)continue;var normal=new Vector2(edge.y,-edge.x).normalized;var mid=Vector2.Lerp(a,b,R(rng,.13f,.87f));if(Inside(mid+normal*.1f,h.poly))normal=-normal;var p=mid+normal*(radius+R(rng,.7f,attempt<16?4.2f:7f));
            if(!CoreClear(p,kind)||!Ground(p,out float y))continue;bool stable=true;float max=y,min=y;for(int k=0;k<4;k++){float angle=k*Mathf.PI*.5f;var q=p+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.55f;if(!Ground(q,out float yy)){stable=false;break;}max=Mathf.Max(max,yy);min=Mathf.Min(min,yy);}if(!stable||max-min>.4f)continue;
            if(kind=="Coconut palms")
            {crownRadius=R(rng,4.1f,5.5f);height=Mathf.Max(R(rng,11.5f,17.5f),(NearbyRoof(p,crownRadius+1.6f,max)-max+1.15f)/.70f);if(height>20)continue;crownFloor=max+height*.70f;}
            else if(kind=="Mango shade trees")
            {crownRadius=R(rng,4.5f,6.5f);height=Mathf.Max(R(rng,10,15),(NearbyRoof(p,crownRadius,max)-max+1.0f)/.42f);if(height>20)continue;crownFloor=max+height*.42f;}
            else{height=kind=="Banana yard clusters"?R(rng,2.8f,3.8f):R(rng,3.3f,4.9f);crownRadius=kind=="Banana yard clusters"?1.8f:1.45f;crownFloor=max;}
            result=new Vector3(p.x,max-.025f,p.y);yaw=Mathf.Atan2(normal.x,normal.y)*Mathf.Rad2Deg+R(rng,-40,40);return true;
        }
        return false;
    }
    static void Plant(House h,string kind,int pass)
    {
        if(!FindSpot(h,kind,pass,out var pos,out float yaw,out float height,out float crownRadius,out float crownFloor))return;
        if(!groups.TryGetValue(kind,out var group)){var g=new GameObject(kind);g.transform.SetParent(root.transform,false);groups[kind]=group=g.transform;}
        var go=new GameObject(kind+" "+(group.childCount+1).ToString("D4"));go.transform.SetParent(group,false);go.transform.position=pos;go.transform.rotation=Quaternion.Euler(0,yaw,0);int variant=(int)((h.seed+(uint)pass)%4);var pair=meshes[kind][variant];
        go.transform.localScale=kind=="Coconut palms"?new Vector3(crownRadius/5.6f,height/12f,crownRadius/5.6f):kind=="Mango shade trees"?new Vector3(crownRadius,height,crownRadius):kind=="Banana yard clusters"?new Vector3(.96f,height/3.78f,.96f):new Vector3(.8f,height/4.7f,.8f);
        var renderers=new List<Renderer>();for(int lod=0;lod<pair.Length;lod++){var child=new GameObject(lod==0?"Near leaves and bark":"Distant canopy silhouette");child.transform.SetParent(go.transform,false);child.AddComponent<MeshFilter>().sharedMesh=pair[lod];var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=material;renderers.Add(r);}
        var lods=go.AddComponent<LODGroup>();lods.SetLODs(renderers.Count==2?new[]{new LOD(.12f,new[]{renderers[0]}),new LOD(.002f,new[]{renderers[1]})}:new[]{new LOD(.009f,new[]{renderers[0]})});lods.RecalculateBounds();
        if(kind=="Coconut palms"||kind=="Mango shade trees"){var c=go.AddComponent<CapsuleCollider>();c.radius=kind=="Coconut palms"?.2f:.08f;c.height=kind=="Coconut palms"?6f:.4f;c.center=Vector3.up*c.height*.5f;}
        var plant=new BanagoDenseCanopyReview.Plant{target=go.transform,species=kind,nearHouse=h.id,baseClearance=BaseClearance(kind),crownRadius=crownRadius,groundY=pos.y+.025f,crownFloor=crownFloor};plants.Add(plant);occupied.Add(new Circle{p=XZ(pos),radius=Spacing(kind),owner=go.transform});
    }
    public static string Start()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("MAIN_GAME must be open outside Play mode");if(GameObject.Find(RootName))throw new Exception("Dense canopy already exists; refusing duplicates");if(!File.Exists(Backup))throw new Exception("A pre-canopy scene backup is required");
        var shader=Shader.Find("StormSim/Banago Lived In PBR");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Canopy surface shader unavailable");
        originals=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>!Streamed(t)).Select(t=>new Original{t=t,p=t.localPosition,s=t.localScale,r=t.localRotation,active=t.gameObject.activeSelf,mesh=t.GetComponent<MeshFilter>()?t.GetComponent<MeshFilter>().sharedMesh:null,mats=t.GetComponent<Renderer>()?t.GetComponent<Renderer>().sharedMaterials:null}).ToList();PrepareContext();
        Folder="Assets/StormSim/Generated/BanagoDenseCanopy_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();root=new GameObject(RootName);root.transform.SetParent(GameObject.Find("CesiumGeoreference").transform,false);Undo.RegisterCreatedObjectUndo(root,"Add dense tropical canopy");review=root.AddComponent<BanagoDenseCanopyReview>();review.assetFolder=Folder;review.sceneBackup=Backup;
        material=new Material(shader){name="Layered tropical greens and aged bark",enableInstancing=true};material.SetFloat("_Kind",2);material.SetFloat("_Smoothness",.15f);material.SetFloat("_Metallic",0);material.SetFloat("_Cull",0);material.SetFloat("_Weathering",.6f);
        meshes=new Dictionary<string,Mesh[][]>();AssetDatabase.StartAssetEditing();
        try
        {
            AssetDatabase.CreateAsset(material,Folder+"/Canopy surface.mat");
            foreach(string kind in new[]{"Coconut palms","Mango shade trees","Banana yard clusters","Bamboo fence clumps"})
            {
                meshes[kind]=new Mesh[4][];for(int v=0;v<4;v++)
                {
                    meshes[kind][v]=kind=="Coconut palms"?new[]{BanagoDenseCanopyModels.Coconut(v,true),BanagoDenseCanopyModels.Coconut(v,false)}:kind=="Mango shade trees"?new[]{BanagoDenseCanopyModels.Mango(v,true),BanagoDenseCanopyModels.Mango(v,false)}:kind=="Bamboo fence clumps"?new[]{BanagoDenseCanopyModels.Bamboo(v,true),BanagoDenseCanopyModels.Bamboo(v,false)}:new[]{BanagoLivedInModels.Banana(v)};
                    for(int k=0;k<meshes[kind][v].Length;k++)AssetDatabase.CreateAsset(meshes[kind][v][k],Folder+"/"+kind+" "+v+" LOD"+k+".asset");
                }
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        groups=new Dictionary<string,Transform>();plants=new List<BanagoDenseCanopyReview.Plant>();next=phase=0;EditorApplication.update+=Tick;Status="Planting irregular coconut canopy";return Status;
    }
    static void Tick()
    {
        try
        {
            int end=Mathf.Min(next+18,houses.Count);for(;next<end;next++){var h=houses[next];if(phase==0)Plant(h,"Coconut palms",0);else{if(h.seed%3==0)Plant(h,"Coconut palms",1);if(h.seed%5==1)Plant(h,"Mango shade trees",2);if(h.seed%3==2)Plant(h,"Banana yard clusters",3);if(h.seed%6==3)Plant(h,"Bamboo fence clumps",4);}}
            Status="Canopy pass "+(phase+1)+"/2, houses "+next+"/"+houses.Count+", plants "+plants.Count;if(next<houses.Count)return;if(phase==0){phase=1;next=0;return;}EditorApplication.update-=Tick;review.plants=plants.ToArray();
            var changes=originals.Where(o=>!o.t||o.t.localPosition!=o.p||o.t.localScale!=o.s||o.t.localRotation!=o.r||o.t.gameObject.activeSelf!=o.active||(o.t.GetComponent<MeshFilter>()&&o.t.GetComponent<MeshFilter>().sharedMesh!=o.mesh)||(o.mats!=null&&!o.t.GetComponent<Renderer>().sharedMaterials.SequenceEqual(o.mats))).Select(o=>o.t?o.t.name:"missing").ToArray();if(changes.Length>0)throw new Exception("Original object preservation failed: "+string.Join(", ",changes.Take(8)));
            review.notes="Additive dense tropical canopy: irregular curved coconut palms, darker rounded mango crowns, banana yards and bamboo edges. Crown colors/heights vary. Tree bases avoid house footprints, 309 road surfaces, gates, public buildings and prior props; tall crowns may overhang roofs above checked clearance. Species and placements are illustrative, not surveyed. Disable this entire root to hide only the added vegetation. "+string.Join("; ",plants.GroupBy(p=>p.species).Select(g=>g.Key+": "+g.Count()));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene:root.scene);Selection.activeGameObject=root;Status="Ready for review: "+plants.Count+" new plants; "+originals.Count+" original objects preserved";
            File.WriteAllText(Folder+"/Canopy report.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{originalObjectsVerified=originals.Count,originalChanges=changes.Length,roadSurfaces=roads.Length,counts=plants.GroupBy(p=>p.species).ToDictionary(g=>g.Key,g=>g.Count()),assetFolder=Folder,sceneBackup=Backup,placementType="Illustrative, ground-sampled, road and building constrained; not surveyed",validation=Validate()},Newtonsoft.Json.Formatting.Indented));
        }
        catch(Exception ex){EditorApplication.update-=Tick;Status="Stopped: "+ex.Message;if(root)root.SetActive(false);Debug.LogException(ex);}
    }
    public static object Validate()
    {
        var existing=GameObject.Find(RootName);if(!existing)throw new Exception("Dense canopy root not found");var data=existing.GetComponent<BanagoDenseCanopyReview>();PrepareContext();foreach(var p in data.plants)if(p.target&&p.target.gameObject.activeInHierarchy)occupied.Add(new Circle{p=XZ(p.target.position),radius=Spacing(p.species),owner=p.target});
        var issues=new List<string>();int roofOverhangs=0;foreach(var plant in data.plants)
        {
            var t=plant.target;if(!t){issues.Add("Missing plant");continue;}if(!t.gameObject.activeInHierarchy)continue;var p=XZ(t.position);
            if(!CoreClear(p,plant.species,t))issues.Add(t.name+" base clearance");if(!Ground(p,out float y)||Mathf.Abs(y-plant.groundY)>.45f)issues.Add(t.name+" ground");
            if(plant.species=="Coconut palms"||plant.species=="Mango shade trees"){float top=NearbyRoof(p,plant.crownRadius,plant.groundY);if(top>plant.groundY+2)roofOverhangs++;if(top>plant.crownFloor-.45f)issues.Add(t.name+" roof clearance");}
        }
        return new{plants=data.plants.Length,roadSurfaces=roads.Length,issues=issues.ToArray(),tallTreesNearRoofs=roofOverhangs,counts=data.plants.GroupBy(p=>p.species).ToDictionary(g=>g.Key,g=>g.Count())};
    }
}
