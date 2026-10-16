using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Reversible, geographically registered aerial landscape completion.</summary>
public static class BanagoAerialCompletionBuilder
{
    public const string RootName = "Banago aerial reference - green landscape and pond neighborhoods";
    public static string Status = "Not started", Folder;
    static GameObject root;
    static BanagoReferenceAreasReview review;
    static JArray candidates, ponds;
    static MeshCollider[] ground, roads;
    static List<Bounds> protectedBounds;
    static Dictionary<Vector2Int,List<Bounds>> protectedGrid;
    static Dictionary<string,Mesh[]> nearFar;
    static Material leaf, detail, pathMaterial;
    static List<object> records;
    static int next, planted, pathSections, reeds, sluices;
    static Transform trees, banks;
    static System.Random rng;
    static List<BanagoReferenceAreasReview.Swap> swaps;
    static float R(float a,float b)=>a+(b-a)*(float)rng.NextDouble();
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static bool Contains(Bounds b,Vector2 p,float pad=0)=>p.x>=b.min.x-pad&&p.x<=b.max.x+pad&&p.y>=b.min.z-pad&&p.y<=b.max.z+pad;
    static string PathOf(Transform t){string p=t.name;while(t.parent){t=t.parent;p=t.name+"/"+p;}return p;}
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool result=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)result=!result;return result;}
    static bool Ground(Vector2 p,out float y)
    {y=-999;foreach(var c in ground)if(Contains(c.bounds,p)&&c.Raycast(new Ray(new Vector3(p.x,80,p.y),Vector3.down),out var h,160)&&h.normal.y>.78f)y=Mathf.Max(y,h.point.y);return y>-998;}
    static bool RoadClear(Vector2 p,float radius)
    {
        foreach(var c in roads)if(Contains(c.bounds,p,radius))
            for(int i=0;i<9;i++){var v=i==0?p:p+new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4))*radius;if(c.Raycast(new Ray(new Vector3(v.x,70,v.y),Vector3.down),out var h,140))return false;}
        return true;
    }
    static bool BuildingsClear(Vector2 p,float radius)
    {var key=new Vector2Int(Mathf.FloorToInt(p.x/25),Mathf.FloorToInt(p.y/25));return !protectedGrid.TryGetValue(key,out var bs)||!bs.Any(b=>Contains(b,p,radius));}
    static void Swap(MeshFilter f,Material m)
    {var r=f.GetComponent<Renderer>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,materialsBefore=r.sharedMaterials,materialsAfter=Enumerable.Repeat(m,Mathf.Max(1,f.sharedMesh.subMeshCount)).ToArray()});}
    static Transform Group(string name,Transform parent)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static GameObject MeshObject(string name,Mesh m,Material mat,Transform parent,bool collider=false)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;}
    static void SaveMesh(Mesh m,string name){m.name=name;AssetDatabase.CreateAsset(m,Folder+"/"+name+".asset");}
    static void Box(BanagoEnvironmentModels.Parts p,Vector3 c,Vector3 s,Color color,Quaternion rot)
    {
        Vector3[] v={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
        for(int i=0;i<8;i++)v[i]=c+rot*Vector3.Scale(v[i],s*.5f);
        int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};
        foreach(var f in faces){var n=((v[f[0]]+v[f[1]]+v[f[2]]+v[f[3]])*.25f-c).normalized;p.Face(v[f[0]],v[f[1]],v[f[2]],color,n);p.Face(v[f[0]],v[f[2]],v[f[3]],color,n);}
    }
    static Mesh Finish(BanagoEnvironmentModels.Parts p,string name)
    {var m=p.Mesh(name);var v=m.vertices;m.uv=v.Select(a=>new Vector2(a.x+a.z,a.y)).ToArray();m.RecalculateTangents();SaveMesh(m,name);return m;}
    public static string Start()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Open MAIN_GAME outside Play mode.");
        if(GameObject.Find(RootName))throw new Exception("Aerial completion already exists; refusing a duplicate.");
        var greenShader=Shader.Find("StormSim/Banago Aerial Green Ground");var waterShader=Shader.Find("StormSim/Banago Aerial Pond Water");
        if(!greenShader||!waterShader||ShaderUtil.ShaderHasError(greenShader)||ShaderUtil.ShaderHasError(waterShader))throw new Exception("New landscape shaders must compile first.");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");Folder="Assets/StormSim/Generated/AerialCompletion_"+stamp;string backup="RoadRepairBackup/AerialCompletion_"+stamp+"/MAIN_GAME.before-aerial-completion.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Scene backup failed.");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var filters=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh&&f.gameObject.activeInHierarchy&&f.GetComponent<Renderer>()).ToArray();
        ground=filters.Where(f=>f.name.StartsWith("Measured terrain")).Select(f=>f.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled).ToArray();
        roads=filters.Where(f=>f.transform.parent&&f.transform.parent.name=="Study Area Roads"||new[]{"Two traffic lanes - asphalt carriageway","Highway continuation past school to north bend"}.Contains(f.name)||PathOf(f.transform).Contains("Small road on house side of wall")).Select(f=>f.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled).ToArray();
        if(ground.Length<100||roads.Length<200)throw new Exception("Expected measured ground and complete road network.");
        protectedBounds=filters.Where(f=>!f.name.StartsWith("Measured terrain")&&!(f.transform.parent&&f.transform.parent.name=="Study Area Roads")&&!f.name.Contains("bank")&&!f.name.Contains("berm")&&!f.name.Contains("water")&&!f.name.Contains("Basin")&&!f.name.Contains("basin")&&!f.name.Contains("carriageway")&&!f.name.Contains("continuation")&&!f.name.Contains("open lot")&&!f.name.Contains("ground")).Select(f=>f.GetComponent<Renderer>().bounds).Where(b=>b.size.y>1.1f&&b.size.x>1.2f&&b.size.z>1.2f&&b.size.x<115&&b.size.z<115&&b.min.y<8).ToList();
        protectedGrid=new Dictionary<Vector2Int,List<Bounds>>();foreach(var b in protectedBounds)for(int x=Mathf.FloorToInt((b.min.x-4)/25);x<=Mathf.FloorToInt((b.max.x+4)/25);x++)for(int z=Mathf.FloorToInt((b.min.z-4)/25);z<=Mathf.FloorToInt((b.max.z+4)/25);z++){var key=new Vector2Int(x,z);if(!protectedGrid.TryGetValue(key,out var list))protectedGrid[key]=list=new List<Bounds>();list.Add(b);}
        root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Complete Banago aerial landscape");review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;root.AddComponent<BanagoAerialMaterialRestore>().review=review;
        swaps=new List<BanagoReferenceAreasReview.Swap>();rng=new System.Random(9132026);records=new List<object>();next=planted=pathSections=reeds=sluices=0;
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/aerial-completion-plan.json"));candidates=(JArray)data["plants"];ponds=(JArray)data["ponds"];
        var terrain=filters.First(f=>f.name.StartsWith("Measured terrain"));var groundMat=new Material(terrain.GetComponent<Renderer>().sharedMaterial){shader=greenShader,name="Tropical green land cover with irregular earth paths"};AssetDatabase.CreateAsset(groundMat,Folder+"/Green landscape.mat");
        foreach(var f in filters.Where(f=>f.name.StartsWith("Measured terrain")))Swap(f,groundMat);
        foreach(var f in filters.Where(f=>f.name.Contains(" - still water")))
        {
            int id=int.Parse(f.name.Substring(5,2));var m=new Material(f.GetComponent<Renderer>().sharedMaterial){shader=waterShader,name="Pond "+id.ToString("00")+" - green water and soft sky sheen"};
            m.SetColor("_BaseColor",Color.Lerp(new Color(.28f,.40f,.345f),new Color(.40f,.49f,.39f),(id%7)/6f));m.SetFloat("_Seed",id*1.71f);m.SetFloat("_Dryness",id%6==0?.10f:0);m.SetFloat("_Mode",0);AssetDatabase.CreateAsset(m,Folder+"/Pond "+id.ToString("00")+".mat");Swap(f,m);
        }
        review.swaps=swaps.ToArray();review.Apply();
        leaf=new Material(Shader.Find("StormSim/Banago Lived In PBR")){name="Deep tropical leaves and weathered bark",enableInstancing=true};leaf.SetFloat("_Kind",2);leaf.SetFloat("_Weathering",.15f);leaf.SetFloat("_Smoothness",.12f);leaf.SetFloat("_Cull",0);AssetDatabase.CreateAsset(leaf,Folder+"/Tropical foliage.mat");
        detail=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Pond wood concrete and farm details",enableInstancing=true};detail.SetFloat("_Weathering",.35f);detail.SetFloat("_Cull",0);AssetDatabase.CreateAsset(detail,Folder+"/Working pond details.mat");
        pathMaterial=new Material(Shader.Find("StormSim/Banago Reference Areas")){name="Narrow walked earth on pond berms"};pathMaterial.SetFloat("_Mode",5);pathMaterial.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(pathMaterial,Folder+"/Pond footpath.mat");
        nearFar=new Dictionary<string,Mesh[]>();foreach(string kind in new[]{"mango","coconut","bamboo"})for(int v=0;v<4;v++)
        {var pair=new Mesh[2];for(int lod=0;lod<2;lod++){var m=kind=="mango"?BanagoDenseCanopyModels.Mango(v,lod==0):kind=="coconut"?BanagoDenseCanopyModels.Coconut(v,lod==0):BanagoDenseCanopyModels.Bamboo(v,lod==0);SaveMesh(m,kind+" "+v+" LOD"+lod);pair[lod]=m;}nearFar[kind+v]=pair;}
        trees=Group("Dense tree belts and neighborhood yards",root.transform);banks=Group("Pond berm footpaths reeds and working sluices",root.transform);
        EditorApplication.update+=Tick;Status="Placing trees against the live terrain and road network";WriteProgress();return Status;
    }
    static void Tick()
    {
        try
        {
            int end=Mathf.Min(next+55,candidates.Count);
            for(;next<end;next++)
            {
                var d=candidates[next];var p=new Vector2((float)d["x"],(float)d["z"]);
                if(!Ground(p,out float y)||!RoadClear(p,3.6f)||!BuildingsClear(p,2.1f))continue;
                var kind=(string)d["kind"];float h=(float)d["height"],radius=(float)d["radius"];int v=(int)d["variant"];
                var g=Group(kind+" "+(++planted).ToString("0000"),trees);g.position=new Vector3(p.x,y-.02f,p.y);g.rotation=Quaternion.Euler(0,(float)d["yaw"],0);
                g.localScale=kind=="mango"?new Vector3(radius,h,radius):kind=="coconut"?new Vector3(radius/4.6f,h/12,radius/4.6f):new Vector3(1.25f,1.5f,1.25f);
                var pair=nearFar[kind+v];var renderers=new Renderer[2];for(int lod=0;lod<2;lod++)renderers[lod]=MeshObject(lod==0?"Leaves and bark":"Distant crown",pair[lod],leaf,g).GetComponent<Renderer>();
                var levels=g.gameObject.AddComponent<LODGroup>();levels.SetLODs(new[]{new LOD(.08f,new[]{renderers[0]}),new LOD(.0014f,new[]{renderers[1]})});levels.RecalculateBounds();
                if(kind!="bamboo"){var c=g.gameObject.AddComponent<CapsuleCollider>();c.radius=kind=="mango"?.055f:.2f;c.height=kind=="mango"?.42f:6f;c.center=Vector3.up*c.height*.5f;}
                records.Add(new{kind,x=p.x,y,z=p.y,height=h,radius});
            }
            Status="Reference vegetation: "+planted+" placed; checked "+next+" / "+candidates.Count;WriteProgress();
            if(next<candidates.Count)return;EditorApplication.update-=Tick;BuildPondEdges();FinishScene();
        }
        catch(Exception e){EditorApplication.update-=Tick;Status="Stopped: "+e.Message;WriteProgress();Debug.LogException(e);}
    }
    static void WriteProgress()=>File.WriteAllText("RoadRepairBackup/aerial-completion-progress.json",JObject.FromObject(new{Status,Folder,planted,next,pathSections,reeds,sluices}).ToString());
    static void BuildPondEdges()
    {
        foreach(var pd in ponds)
        {
            int id=(int)pd["index"];var poly=pd["polygon"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();var parts=new BanagoEnvironmentModels.Parts();var work=new BanagoEnvironmentModels.Parts();var reedCombine=new List<CombineInstance>();var parent=Group("Pond "+id.ToString("00")+" - bank access and vegetation",banks);bool sluice=false;
            for(int edge=0;edge<poly.Length;edge++)
            {
                var a=poly[edge];var b=poly[(edge+1)%poly.Length];var dir=(b-a).normalized;var outside=new Vector2(-dir.y,dir.x);if(Inside((a+b)*.5f+outside*.1f,poly))outside=-outside;
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/2));
                for(int j=0;j<count;j++)
                {
                    var p=Vector2.Lerp(a,b,j/(float)count)+outside*2.25f;var q=Vector2.Lerp(a,b,(j+1f)/count)+outside*2.25f;var mid=(p+q)*.5f;
                    if(Ground(p,out var y)&&Ground(q,out var y2)&&Mathf.Abs(y-y2)<.65f&&BuildingsClear(mid,.65f)&&RoadClear(mid,1.2f))
                    {
                        float w=.38f+R(-.05f,.08f);var l=p-outside*w;var r=p+outside*w;var l2=q-outside*w;var r2=q+outside*w;var color=new Color(.49f,.46f,.35f)*R(.93f,1.05f);
                        Vector3 A=new Vector3(l.x,y+.03f,l.y),B=new Vector3(r.x,y+.03f,r.y),C=new Vector3(r2.x,y2+.03f,r2.y),D=new Vector3(l2.x,y2+.03f,l2.y);parts.Face(A,B,C,color,Vector3.up);parts.Face(A,C,D,color,Vector3.up);pathSections++;
                        if(!sluice&&j>1&&edge%2==0)
                        {
                            var c=new Vector3(mid.x,(y+y2)*.5f,mid.y);var rot=Quaternion.LookRotation(new Vector3(outside.x,0,outside.y));
                            foreach(int sign in new[]{-1,1})Box(work,c+rot*new Vector3(sign*.66f,.25f,0),new Vector3(.25f,.6f,1.5f),new Color(.48f,.47f,.42f),rot);
                            for(int board=0;board<5;board++)Box(work,c+rot*new Vector3(0,.12f+board*.105f,0),new Vector3(1.12f,.09f,.12f),new Color(.36f,.29f,.19f),rot);
                            Box(work,c+Vector3.up*.91f,new Vector3(.055f,.65f,.055f),new Color(.27f,.27f,.25f),rot);sluice=true;sluices++;
                        }
                    }
                    if(j%4==0)
                    {
                        var rp=Vector2.Lerp(a,b,(j+.5f)/count)+outside*R(.15f,1.1f);if(Ground(rp,out float ry)&&BuildingsClear(rp,.3f)&&RoadClear(rp,1))
                        {reedCombine.Add(new CombineInstance{mesh=nearFar["bamboo"+(j%4)][1],transform=Matrix4x4.TRS(new Vector3(rp.x,ry-.025f,rp.y),Quaternion.Euler(0,R(0,360),0),new Vector3(R(.6f,1.1f),R(.12f,.26f),R(.6f,1.1f)))});reeds++;}
                    }
                }
            }
            if(parts.vertices.Count>0)MeshObject("Narrow walked berm paths",Finish(parts,"Pond "+id+" paths"),pathMaterial,parent);
            if(work.vertices.Count>0)MeshObject("Weathered timber sluice and concrete cheeks",Finish(work,"Pond "+id+" sluice"),detail,parent,true);
            if(reedCombine.Count>0){var m=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.CombineMeshes(reedCombine.ToArray(),true,true);SaveMesh(m,"Pond "+id+" bank reeds");MeshObject("Irregular rushes and low bank growth",m,leaf,parent);}
        }
    }
    static void FinishScene()
    {
        review.notes="Aerial reference completion. Original measured elevations, road network, mapped houses, school, expanded gate, church wall, access lane and coast retained. New ground and 25 pond materials are reversible swaps; disabling this root restores them. Dense mixed tropical vegetation follows source green cover and avoids live roads/buildings. Bank paths, reeds and working sluices are approximate small details, not surveyed objects.";
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
        Status="Landscape complete";WriteProgress();
        File.WriteAllText("RoadRepairBackup/aerial-completion-latest.json",JObject.FromObject(new{root=RootName,Folder,backup=review.backupPath,planted,pathSections,reeds,sluices,materialSwaps=swaps.Count,roadsChecked=roads.Length,plants=records}).ToString());
    }
}
