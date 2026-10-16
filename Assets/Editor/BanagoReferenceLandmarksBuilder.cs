using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class BanagoReferenceLandmarksBuilder
{
    public const string RootName="Banago school frontage and coastal barrier - photo reference";
    static string folder,backup;static GameObject root;static Material solid,greenRoof,letters;
    static BanagoReferenceAreasReview surfaces;static List<BanagoReferenceAreasReview.Swap> swaps=new List<BanagoReferenceAreasReview.Swap>();
    static MeshCollider[] roads;static int n;static float[] grid;static int nx,nz;
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static Vector3 At(Vector2 p,float y)=>V(p.x,y,p.y);
    static float Ground(Vector2 p){float gx=Mathf.Clamp((p.x+277)/1202*(nx-1),0,nx-1.001f),gz=Mathf.Clamp((p.y-89)/1361*(nz-1),0,nz-1.001f);int x=(int)gx,z=(int)gz;float u=gx-x,v=gz-z,a=grid[z*nx+x],b=grid[z*nx+x+1],c=grid[(z+1)*nx+x],d=grid[(z+1)*nx+x+1];return u+v<=1?a+u*(b-a)+v*(c-a):d+(1-u)*(c-d)+(1-v)*(b-d);}
    static bool In(Vector2 p,Vector2[] poly){bool b=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)b=!b;return b;}
    static bool Road(Vector2 p,float margin=.35f){foreach(var c in roads){var b=c.bounds;if(p.x<b.min.x-margin||p.x>b.max.x+margin||p.y<b.min.z-margin||p.y>b.max.z+margin)continue;foreach(var q in new[]{p,p+Vector2.right*margin,p-Vector2.right*margin,p+Vector2.up*margin,p-Vector2.up*margin})if(c.Raycast(new Ray(At(q,70),Vector3.down),out _,150))return true;}return false;}
    sealed class S
    {
        public BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col){var normal=Vector3.Cross(b-a,c-a).normalized;p.Face(a,b,c,col,normal);p.Face(a,c,d,col,normal);}
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col,Vector3 normal){p.Face(a,b,c,col,normal);p.Face(a,c,d,col,normal);}
        public void Box(Vector3 c,Vector3 size,Color col,Quaternion q){var v=new[]{V(-1,-1,-1),V(1,-1,-1),V(1,1,-1),V(-1,1,-1),V(-1,-1,1),V(1,-1,1),V(1,1,1),V(-1,1,1)}.Select(s=>c+q*Vector3.Scale(s,size*.5f)).ToArray();foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Q(v[f[0]],v[f[1]],v[f[2]],v[f[3]],col);}
        public void Beam(Vector3 a,Vector3 b,float width,Color col){Box((a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),col,Quaternion.FromToRotation(Vector3.up,(b-a).normalized));}
    }
    static GameObject Save(string name,S s,Material mat,bool collider=false)
    {var m=s.p.Mesh(name);m.uv=m.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();if(m.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception("Invalid geometry");AssetDatabase.CreateAsset(m,folder+"/Mesh "+(++n)+".asset");var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;}
    static void Sign(string title,Vector3 point,Vector3 outward,float size)
    {var go=new GameObject(title);go.transform.SetParent(root.transform,false);go.transform.position=point;go.transform.rotation=Quaternion.LookRotation(-outward,Vector3.up);var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=title;t.characterSize=size;t.fontSize=64;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.19f,.32f,.38f);go.GetComponent<Renderer>().sharedMaterial=letters;}
    static void Swap(MeshFilter f,Mesh after,Material mat)
    {var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=after,materialsBefore=f.GetComponent<Renderer>().sharedMaterials,materialsAfter=new[]{mat},colliderBefore=c?c.sharedMesh:null});}
    public static object BuildSchoolAndHighway()=>Build(false,true);
    public static object FixSchoolPhotoGate()
    {
        const string title="School gate - green panels pink posts photo match";
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||GameObject.Find(title))throw new Exception("Exit Play mode or gate already exists");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/SchoolGate_"+stamp;Directory.CreateDirectory(backup);EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-gate.unity",true);
        folder="Assets/StormSim/Generated/SchoolGate_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);n=0;
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        var along=new Vector2(.54f,-.84f).normalized;var outward=new Vector2(-along.y,along.x)*-1;var center=new Vector2(-22.4f,654.4f)+along;float floor=Ground(center);var rotation=Quaternion.LookRotation(At(-outward,0));
        Func<float,float,float,Vector3>P=(x,y,z)=>At(center+along*x+outward*z,floor+y);
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface"));solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.18f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Painted green steel and faded pink masonry.mat");
        var pink=new Color(.64f,.44f,.37f);var green=new Color(.09f,.25f,.16f);var edge=new Color(.16f,.33f,.22f);var s=new S();
        foreach(float x in new[]{-4.3f,4.3f}){s.Box(P(x,1.38f,0),V(.38f,2.9f,.42f),pink,rotation);s.Box(P(x,2.88f,0),V(.43f,.10f,.46f),pink,rotation);}
        // Closed double-leaf gateway, with corrugated lower panels and tall open bars.
        foreach(int side in new[]{-1,1})
        {
            float lo=side<0?-4.10f:.045f,hi=side<0?-.045f:4.10f;
            s.Box(P((lo+hi)*.5f,1.02f,0),V(hi-lo,1.66f,.055f),green,rotation);
            for(float x=lo+.06f;x<hi;x+=.115f)s.Box(P(x,1.02f,.039f),V(.025f,1.64f,.035f),edge,rotation);
            foreach(float x in new[]{lo,hi})s.Beam(P(x,.17f,.03f),P(x,3.27f,.03f),.055f,edge);
            foreach(float y in new[]{.18f,1.86f,2.38f,3.27f})s.Beam(P(lo,y,.03f),P(hi,y,.03f),.052f,edge);
            for(float x=lo+.13f;x<hi;x+=.16f)s.Beam(P(x,1.85f,.03f),P(x,3.25f,.03f),.025f,green);
            s.Beam(P(side*.16f,1.10f,.09f),P(side*.16f,1.44f,.09f),.034f,edge);
        }
        var fresh=Save("Wide closed green double gate with pink posts",s,solid,true);
        var old=GameObject.Find("School side entrances - orange frontage and green gates").transform.Find("Open green school side gates").gameObject;
        var vis=root.AddComponent<BanagoLocalDetailReview>();vis.originals=new[]{old};vis.originalActive=new[]{old.activeSelf};vis.notes="Replaces folded-back gate with photographed green sheet-metal lower panels, tall barred upper panels and pink concrete posts. Original gate recoverable.";vis.Apply();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=fresh;return new{width=8.2f,height=3.27f,backup};
    }
    public static object ReviseSchoolGymCampus()
    {
        const string title="School campus - smaller gym two classrooms and mixed trees";var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or campus revision already exists");
        var center=new Vector2(1.76f,677.83f);var along=new Vector2(.914f,.406f).normalized;var across=new Vector2(-along.y,along.x);var rotation=Quaternion.LookRotation(At(across,0));Func<Vector2,Vector2>local=p=>new Vector2(Vector2.Dot(p-center,along),Vector2.Dot(p-center,across));Func<float,float,Vector2>point=(x,z)=>center+along*x+across*z;
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var court=GameObject.Find("School campus and covered court");if(!court)throw new Exception("School gym not found");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/SchoolCampusRevision_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-campus.unity",true))throw new Exception("Backup failed");folder="Assets/StormSim/Generated/SchoolCampusRevision_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"School campus reference revision");n=0;swaps.Clear();var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Local school-only revision: 58% gym footprint, two flanking classrooms, grade blending, wider side gate and three retained coconut palms. Original meshes and trees remain recoverable.";
        Action<MeshFilter,Func<Vector3,Vector3>,string>remap=(f,map,label)=>{var old=f.sharedMesh;if(!old)return;var m=UnityEngine.Object.Instantiate(old);var v=m.vertices;bool changed=false;for(int i=0;i<v.Length;i++){var p=f.transform.TransformPoint(v[i]);var p2=map(p);if((p-p2).sqrMagnitude>.0000001f){v[i]=f.transform.InverseTransformPoint(p2);changed=true;}}if(!changed){UnityEngine.Object.DestroyImmediate(m);return;}m.vertices=v;m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,folder+"/"+label+" "+(++n)+".asset");var renderer=f.GetComponent<Renderer>();var col=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=old,after=m,materialsBefore=renderer.sharedMaterials,materialsAfter=renderer.sharedMaterials,colliderBefore=col?col.sharedMesh:null});};
        foreach(var f in court.GetComponentsInChildren<MeshFilter>())remap(f,p=>{var d=XZ(p)-center;return At(center+d*.58f,-.4f+(p.y+.4f)*.78f);},"Smaller gym");
        // Blend only the school yard into a level pad; shared terrain outside is untouched.
        Func<Vector3,Vector3>grade=p=>{var v=local(XZ(p));float w=Mathf.Clamp01(Mathf.Min((26-Mathf.Abs(v.x))/3,(17-Mathf.Abs(v.y))/3));w=w*w*(3-2*w);p.y=Mathf.Lerp(p.y,-.48f,w);return p;};int terrain=0;
        foreach(var f in all.Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh&&f.gameObject.activeInHierarchy&&f.name.StartsWith("Measured terrain"))){int before=swaps.Count;remap(f,grade,"Level campus ground");if(swaps.Count>before)terrain++;}
        foreach(var f in GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshFilter>())if(f.sharedMesh&&f.name!="Felix Amante Drive"&&f.transform.parent&&f.transform.parent.name!="Felix Amante Drive")remap(f,grade,"Campus approach grading");
        var gate=GameObject.Find("School side entrances - orange frontage and green gates").transform.Find("Open green school side gates").GetComponent<MeshFilter>();var gc=new Vector2(-22.4f,654.4f);var ga=new Vector2(.54f,-.84f).normalized;remap(gate,p=>{float x=Vector2.Dot(XZ(p)-gc,ga);return p+At(ga*x*.5f,0);},"Wider school gate");
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Classroom pale cream green trim"};solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.12f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Classroom walls.mat");greenRoof=new Material(solid){name="Classroom green and rust metal roofing"};greenRoof.SetFloat("_Corrugation",1);AssetDatabase.CreateAsset(greenRoof,folder+"/Classroom roofs.mat");
        var cream=new Color(.79f,.78f,.67f);var green=new Color(.28f,.42f,.30f);var dark=new Color(.12f,.19f,.16f);
        foreach(int side in new[]{-1,1})
        {
            var origin=point(side*18.3f,0);Func<float,float,float,Vector3>P=(x,y,z)=>At(origin+along*x+across*z,-.43f+y);var walls=new S();walls.Box(P(0,1.52f,0),V(6.2f,3.04f,15),cream,rotation);walls.Box(P(0,-.025f,0),V(6.5f,.10f,15.3f),new Color(.51f,.51f,.45f),rotation);
            // Long windowed facade faces the gym; two doorways indicate classroom rooms.
            float front=-side*3.14f;for(float z=-5.8f;z<6;z+=2.35f){walls.Box(P(front,1.76f,z),V(.07f,1.18f,1.50f),green,rotation);walls.Box(P(front-side*.045f,1.76f,z),V(.035f,1.02f,1.32f),dark,rotation);for(float dz=-.5f;dz<=.5f;dz+=.25f)walls.Beam(P(front-side*.07f,1.26f,z+dz),P(front-side*.07f,2.27f,z+dz),.026f,cream);}foreach(float z in new[]{-3.4f,3.6f})walls.Box(P(front-side*.04f,1.06f,z),V(.06f,2.12f,.94f),green,rotation);
            foreach(float z in new[]{-7.5f,7.5f})walls.p.Face(P(-3.1f,3.04f,z),P(3.1f,3.04f,z),P(0,3.95f,z),cream,At(across*Mathf.Sign(z),0));Save(side<0?"Classroom block A - left of gym":"Classroom block B - right of gym",walls,solid,true);
            var roof=new S();var col=side<0?new Color(.47f,.62f,.47f):new Color(.51f,.29f,.23f);roof.Q(P(-3.4f,3.05f,-7.8f),P(0,3.98f,-7.8f),P(0,3.98f,7.8f),P(-3.4f,3.05f,7.8f),col,Vector3.up);roof.Q(P(0,3.98f,-7.8f),P(3.4f,3.05f,-7.8f),P(3.4f,3.05f,7.8f),P(0,3.98f,7.8f),col,Vector3.up);Save("Classroom "+(side<0?"A":"B")+" pitched roof",roof,greenRoof);
        }
        // Keep three palms around this gym precinct, not across the entire barangay.
        var palms=all.Where(t=>t.gameObject.activeInHierarchy&&(t.name.ToLower().Contains("coconut")||t.name.StartsWith("Coconut palms "))&&t.position.x>-32&&t.position.x<40&&t.position.z>640&&t.position.z<715&&t.childCount>0).Where(t=>!t.name.ToLower().Contains("repair")&&!t.name.Contains("positions")).OrderByDescending(t=>(XZ(t.position)-center).sqrMagnitude).ToArray();
        var keep=palms.Where(t=>{var p=local(XZ(t.position));return !(Mathf.Abs(Mathf.Abs(p.x)-18.3f)<4&&Mathf.Abs(p.y)<9)&&!(Mathf.Abs(p.x)<14&&Mathf.Abs(p.y)<8);}).Take(3).ToArray();var hide=palms.Except(keep).Select(t=>t.gameObject).ToArray();var vis=root.AddComponent<BanagoLocalDetailReview>();vis.originals=hide;vis.originalActive=hide.Select(g=>g.activeSelf).ToArray();vis.notes="Excess coconut palms in the gym precinct hidden reversibly; three retained. Trees outside this local school area untouched.";vis.Apply();
        var treePositions=new[]{new Vector2(-23,-10),new Vector2(23,-10),new Vector2(-23,10),new Vector2(23,10),new Vector2(-7,12),new Vector2(8,12)};int added=0;
        foreach(var v in treePositions){var p=point(v.x,v.y);var m=BanagoEnvironmentModels.Tree(added%2,added,true,added%2==0?new Color(.29f,.43f,.18f):new Color(.22f,.37f,.22f));AssetDatabase.CreateAsset(m,folder+"/Mixed leafy tree "+added+".asset");var g=new GameObject((added%2==0?"Spreading shade tree ":"Upright leafy tree ")+(added+1));g.transform.SetParent(root.transform,false);g.transform.position=grade(At(p,Ground(p)));g.transform.localScale=V(2.8f+(added%2)*.5f,6.8f+(added%3),2.8f+(added%2)*.5f);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=solid;added++;}
        review.swaps=swaps.ToArray();review.Apply();EditorUtility.SetDirty(review);EditorUtility.SetDirty(vis);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=root;var report=new{folder,backup,newClassroomBlocks=2,gymLinearScale=.58f,gateOpeningMetres=8.2f,terrainMeshesGraded=terrain,coconutPalmsRetained=keep.Select(t=>t.name).ToArray(),coconutPalmsHidden=hide.Length,mixedLeafyTreesAdded=added,originalObjectsDeleted=0};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
    public static object AddChurchNeighbourhoodHouses()
    {
        const string title="Church-side infill houses - individually adjustable";var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or house group already exists");
        var homes=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(h=>h["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray()).ToList();var ponds=JObject.Parse(File.ReadAllText("RoadRepairBackup/banago-inland-data.json"))["ponds"].Select(h=>h["polygon"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray()).ToArray();
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        var occupied=homes.Select(h=>new{poly=h,bounds=new Rect(h.Min(p=>p.x)-1,h.Min(p=>p.y)-1,h.Max(p=>p.x)-h.Min(p=>p.x)+2,h.Max(p=>p.y)-h.Min(p=>p.y)+2)}).ToList();
        Func<Vector2,Vector2[],float> distance=(p,h)=>{float result=float.MaxValue;for(int i=0;i<h.Length;i++){var a=h[i];var d=h[(i+1)%h.Length]-a;var near=a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude));result=Mathf.Min(result,Vector2.Distance(p,near));}return result;};
        var candidates=new List<Vector2>();for(float z=746;z<846;z+=4.4f)for(float x=-122;x<18;x+=4.4f){if(x< -78&&z>813)continue;var p=new Vector2(x,z);float near=homes.Min(h=>distance(p,h));if(near<13&&near>2)candidates.Add(p);}
        candidates=candidates.OrderBy(p=>Mathf.PerlinNoise(p.x*.17f,p.y*.17f)).ToList();var accepted=new List<(Vector2 center,Quaternion rotation,float width,float depth,float floor,float baseDepth)>();int trial=0;
        foreach(var p in candidates)
        {
            if(accepted.Count>=32)break;trial++;float w=3.6f+1.7f*Mathf.PerlinNoise(p.x*.37f,3),d=4.3f+2.1f*Mathf.PerlinNoise(p.y*.29f,6);var nearest=homes.OrderBy(h=>distance(p,h)).First();var axis=(nearest[1]-nearest[0]).normalized;float angle=Mathf.Atan2(-axis.y,axis.x)*Mathf.Rad2Deg;var rot=Quaternion.Euler(0,angle,0);Func<float,float,Vector2>point=(x,z)=>p+XZ(rot*V(x,0,z));var footprint=new[]{point(-w*.5f,-d*.5f),point(w*.5f,-d*.5f),point(w*.5f,d*.5f),point(-w*.5f,d*.5f)};
            bool clear=true;float low=100,high=-100;for(float x=-w*.5f-.5f;x<=w*.5f+.51f&&clear;x+=.45f)for(float z=-d*.5f-.5f;z<=d*.5f+.51f&&clear;z+=.45f){var v=point(x,z);if(Road(v,1.15f)||occupied.Any(h=>h.bounds.Contains(v)&&(In(v,h.poly)||distance(v,h.poly)<.95f))||ponds.Any(h=>In(v,h)||distance(v,h)<2.1f)){clear=false;break;}float y=Ground(v);low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
            if(!clear||high-low>.62f)continue;accepted.Add((p,rot,w,d,high+.04f,high-low+.14f));occupied.Add(new{poly=footprint,bounds=new Rect(footprint.Min(v=>v.x)-1,footprint.Min(v=>v.y)-1,footprint.Max(v=>v.x)-footprint.Min(v=>v.x)+2,footprint.Max(v=>v.y)-footprint.Min(v=>v.y)+2)});
        }
        if(accepted.Count==0)throw new Exception("No clear infill plots found");string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/ChurchInfill_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-infill.unity",true))throw new Exception("Backup failed");folder="Assets/StormSim/Generated/ChurchInfill_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);var group=root;Undo.RegisterCreatedObjectUndo(group,"Add adjustable church-side homes");n=0;
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Infill houses faded plaster timber and trim"};solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.31f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/House walls.mat");greenRoof=new Material(solid){name="Mixed reclaimed GI roof sheets"};greenRoof.SetFloat("_Corrugation",1);greenRoof.SetFloat("_Weathering",.55f);AssetDatabase.CreateAsset(greenRoof,folder+"/House roof.mat");var wallColors=new[]{new Color(.63f,.60f,.51f),new Color(.62f,.50f,.34f),new Color(.49f,.58f,.55f),new Color(.68f,.66f,.58f),new Color(.53f,.46f,.36f)};var roofColors=new[]{new Color(.63f,.65f,.62f),new Color(.49f,.26f,.18f),new Color(.36f,.48f,.51f),new Color(.36f,.45f,.32f),new Color(.65f,.65f,.59f)};int count=0;
        foreach(var h in accepted)
        {
            count++;root=new GameObject("Infill house "+count.ToString("00")+" - move or rotate");root.transform.SetParent(group.transform,false);float w=h.width,d=h.depth,height=2.5f+(count%4)*.17f,rise=.70f+(count%3)*.20f;var body=new S();var col=wallColors[count%wallColors.Length];body.Box(V(0,-h.baseDepth*.5f,0),V(w,h.baseDepth,d),new Color(.43f,.43f,.38f),Quaternion.identity);body.Box(V(0,height*.5f,0),V(w,height,d),col,Quaternion.identity);
            body.p.Face(V(-w*.5f,height,-d*.5f),V(w*.5f,height,-d*.5f),V(0,height+rise,-d*.5f),col,Vector3.back);body.p.Face(V(w*.5f,height,d*.5f),V(-w*.5f,height,d*.5f),V(0,height+rise,d*.5f),col,Vector3.forward);
            var wood=new Color(.32f,.28f,.21f);var window=new Color(.13f,.19f,.18f);body.Box(V(-w*.24f,1.0f,-d*.5f-.025f),V(.82f,2.0f,.055f),wood,Quaternion.identity);body.Box(V(w*.22f,1.6f,-d*.5f-.032f),V(1.08f,.86f,.06f),window,Quaternion.identity);foreach(float x in new[]{w*.22f-.46f,w*.22f+.46f})body.Box(V(x,1.6f,-d*.5f-.07f),V(.045f,.91f,.04f),col*.78f,Quaternion.identity);body.Box(V(w*.5f+.025f,1.6f,d*.18f),V(.06f,.80f,1.1f),window,Quaternion.identity);if(count%2==0)for(float x=-w*.48f;x<w*.48f;x+=.20f)body.Box(V(x,height*.50f,d*.5f+.015f),V(.023f,height-.1f,.022f),col*.78f,Quaternion.identity);Save("Walls doors windows and grounded foundation",body,solid,true);
            var roof=new S();for(float z=-d*.5f-.20f;z<d*.5f+.2f;z+=.85f){float end=Mathf.Min(z+.87f,d*.5f+.2f);var color=roofColors[(count+(int)((z+d*.5f+.2f)/.85f)/3)%roofColors.Length];roof.Q(V(-w*.5f-.22f,height,z),V(0,height+rise,z),V(0,height+rise,end),V(-w*.5f-.22f,height,end),color,Vector3.up);roof.Q(V(0,height+rise,z),V(w*.5f+.22f,height,z),V(w*.5f+.22f,height,end),V(0,height+rise,end),color*.96f,Vector3.up);}Save("Patched pitched metal roof",roof,greenRoof);root.transform.position=At(h.center,h.floor);root.transform.rotation=h.rotation;
        }
        root=group;Selection.activeGameObject=group;int bad=group.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertices.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));var report=new{folder,backup,addedHouses=count,individuallyMovable=true,newTrees=0,originalObjectsChanged=0,roadAndPondClearanceChecked=true,centralOpenLotRetained=true,badVertices=bad};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);return report;
    }
    public static object BuildOpenCoastalHall()
    {
        const string title="Baybayon open coastal hall - two angle reference";var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or hall already exists");
        var record=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).First(h=>(string)h["name"]=="House 1846");var poly=record["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();var a=poly[1];var b=poly[2];var along=(b-a).normalized;var inward=new Vector2(-along.y,along.x);var outward=-inward;var origin=(a+b)*.5f;float half=Vector2.Distance(a,b)*.5f,depth=poly.Max(p=>Vector2.Dot(p-origin,inward));var q=Quaternion.LookRotation(At(outward,0));
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();float floor=poly.Max(p=>Ground(p))+.16f;Func<float,float,float,Vector3>P=(x,y,z)=>At(origin+along*x+inward*z,floor+y);
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/OpenCoastalHall_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-hall.unity",true))throw new Exception("Backup failed");folder="Assets/StormSim/Generated/OpenCoastalHall_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Open coastal hall photo reference");n=0;swaps.Clear();var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Photo-guided open-sided shelter and yellow side room, replacing House 1846 inside its footprint. Original assets retained. No port, shoreline expansion or new trees.";
        var batch=GameObject.Find("Banago Houses - full detail redesign").transform.Find("Neighbourhood "+(string)record["tile"]);int removed=0;
        foreach(var f in batch.GetComponentsInChildren<MeshFilter>()){var old=f.sharedMesh;if(!old)continue;var v=old.vertices;var mesh=UnityEngine.Object.Instantiate(old);int count=0;for(int s=0;s<old.subMeshCount;s++){var ix=old.GetTriangles(s);var keep=new List<int>();for(int i=0;i<ix.Length;i+=3){var mid=f.transform.TransformPoint((v[ix[i]]+v[ix[i+1]]+v[ix[i+2]])/3);var d=XZ(mid)-origin;float x=Vector2.Dot(d,along),z=Vector2.Dot(d,inward);if(Mathf.Abs(x)<half+.65f&&z>-.65f&&z<depth+.65f){count++;continue;}keep.Add(ix[i]);keep.Add(ix[i+1]);keep.Add(ix[i+2]);}mesh.SetTriangles(keep,s);}if(count==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}removed+=count;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Retained neighbours "+(++n)+".asset");var r=f.GetComponent<Renderer>();var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=old,after=mesh,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=c?c.sharedMesh:null});}
        var hidden=GameObject.Find(BanagoLivedInBuilder.RootName).GetComponentsInChildren<Transform>().Where(t=>t.parent&&(t.parent.name=="Rooftop satellite dishes"||t.parent.name=="Ribbed PVC rooftop tanks")&&In(XZ(t.position),poly)).Select(t=>t.gameObject).ToArray();var visibility=root.AddComponent<BanagoLocalDetailReview>();visibility.originals=hidden;visibility.originalActive=hidden.Select(g=>g.activeSelf).ToArray();visibility.Apply();
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Coastal hall aged concrete timber and yellow paint"};solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.36f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Hall surfaces.mat");greenRoof=new Material(solid){name="Patched silver and rust roof"};greenRoof.SetFloat("_Corrugation",1);greenRoof.SetFloat("_Weathering",.55f);AssetDatabase.CreateAsset(greenRoof,folder+"/Roof sheets.mat");var concrete=new Color(.62f,.59f,.48f);var timber=new Color(.25f,.23f,.17f);var yellow=new Color(.67f,.58f,.28f);var dark=new Color(.12f,.14f,.12f);
        var structure=new S();structure.Box(P(0,-.13f,depth*.5f),V(half*2-.24f,.26f,depth-.24f),new Color(.44f,.43f,.37f),q);
        foreach(float x in new[]{-half+.35f,-2.15f,1.65f,half-.35f})foreach(float z in new[]{.38f,depth*.52f,depth-.38f}){var pt=P(x,0,z);float bottom=Ground(XZ(pt))-.10f;structure.Box(new Vector3(pt.x,(bottom+floor+3.0f)*.5f,pt.z),V(.30f,floor+3-bottom,.30f),concrete,q);structure.Box(P(x,.18f,z),V(.39f,.36f,.39f),new Color(.35f,.39f,.27f),q);}
        foreach(float z in new[]{.38f,depth*.52f,depth-.38f}){structure.Beam(P(-half+.2f,3.04f,z),P(half-.2f,3.04f,z),.16f,timber);structure.Beam(P(-half+.2f,3.04f,z),P(0,4.60f,z),.11f,timber);structure.Beam(P(0,4.60f,z),P(half-.2f,3.04f,z),.11f,timber);}
        foreach(float x in new[]{-half+.3f,-3f,0,3f,half-.3f})structure.Beam(P(x,3.10f,.20f),P(x,3.10f,depth-.20f),.12f,timber);
        // Side stall retains an open serving window and door, while the central hall is open to the sea.
        structure.Box(P(half-1.42f,1.38f,depth*.62f),V(2.4f,2.76f,.16f),yellow,q);structure.Box(P(half-.18f,1.38f,depth*.33f),V(.16f,2.76f,depth*.60f),yellow,q);
        structure.Box(P(half-1.5f,.51f,.45f),V(2.3f,1.02f,.18f),yellow,q);structure.Box(P(half-1.5f,2.46f,.45f),V(2.3f,.55f,.18f),yellow,q);structure.Box(P(half-1.5f,1.02f,.29f),V(2.45f,.12f,.40f),timber,q);for(float x=half-2.5f;x<half-.5f;x+=.20f)structure.Beam(P(x,1.1f,.45f),P(x,2.16f,.45f),.025f,timber);
        for(float y=.42f;y<1.05f;y+=.23f)structure.Beam(P(-half+.3f,y,depth-.34f),P(1.6f,y,depth-.34f),.075f,timber);
        foreach(float x in new[]{-3.8f,-.6f}){structure.Box(P(x,.48f,depth-2f),V(2.2f,.10f,.45f),timber,q);foreach(float dx in new[]{-.8f,.8f})structure.Box(P(x+dx,.22f,depth-2f),V(.12f,.44f,.35f),timber,q);}
        Save("Open concrete columns timber trusses slab and yellow side stall",structure,solid,true);
        var roof=new S();for(float z=-.1f;z<depth+.12f;z+=.8f){float end=Mathf.Min(z+.82f,depth+.12f);int k=(int)((z+.1f)/.8f);var col=k%6==2||k%7==4?new Color(.48f,.26f,.16f):new Color(.76f,.77f,.71f);roof.Q(P(-half-.10f,3.12f,z),P(0,4.62f,z),P(0,4.62f,end),P(-half-.1f,3.12f,end),col,Vector3.up);roof.Q(P(0,4.62f,z),P(half+.1f,3.12f,z),P(half+.1f,3.12f,end),P(0,4.62f,end),col*.96f,Vector3.up);}Save("Patchwork silver GI roof with rust sheets",roof,greenRoof);
        var trim=new S();for(float x=-half+.2f;x<half-.2f;x+=1.35f){float end=Mathf.Min(x+1.27f,half-.2f);trim.Q(P(x,2.92f,.10f),P(end,2.92f,.10f),P(end,2.68f,.10f),P(x,2.73f,.10f),new Color(.70f,.63f,.48f),At(outward,0));trim.Box(P((x+end)*.5f,2.80f,.085f),V(.55f,.06f,.015f),new Color(.58f,.25f,.20f),q);}Save("Faded pennant strips along hall eaves",trim,solid);
        // Local wet gravel patches are visual-only and do not change road collision.
        var mud=new S();var puddles=new S();var rubble=new S();var rng=new System.Random(1846);
        Func<Vector2,float>surface=p=>{float y=Ground(p);foreach(var c in roads)if(c.Raycast(new Ray(At(p,70),Vector3.down),out var hit,150))y=Mathf.Max(y,hit.point.y);return y;};
        for(int j=0;j<33;j++){float x=-half+(float)rng.NextDouble()*widthForHall(half),z=-.6f-(float)rng.NextDouble()*4.6f;var center=XZ(P(x,0,z));float rx=.22f+(float)rng.NextDouble()*.9f,rz=.15f+(float)rng.NextDouble()*.40f;var dst=j%3==0?puddles:mud;var color=j%3==0?new Color(.29f,.34f,.34f):new Color(.31f,.29f,.235f);for(int k=0;k<10;k++){float t=k*Mathf.PI*.2f,t2=(k+1)*Mathf.PI*.2f;var p1=center+along*(Mathf.Cos(t)*rx)+inward*(Mathf.Sin(t)*rz);var p2=center+along*(Mathf.Cos(t2)*rx)+inward*(Mathf.Sin(t2)*rz);dst.p.Face(At(center,surface(center)+.022f),At(p1,surface(p1)+.022f),At(p2,surface(p2)+.022f),color,Vector3.up);}if(!Road(center,.3f))rubble.Box(At(center,surface(center)+.035f),V(.16f,.07f,.11f),new Color(.48f,.46f,.40f),q);}
        Save("Irregular damp gravel frontage",mud,solid);var wet=new Material(solid){name="Shallow puddle sheen"};wet.SetFloat("_Weathering",0);if(wet.HasProperty("_Smoothness"))wet.SetFloat("_Smoothness",.75f);AssetDatabase.CreateAsset(wet,folder+"/Puddles.mat");Save("Small shallow frontage puddles",puddles,wet);Save("Scattered small frontage stones",rubble,solid);
        review.swaps=swaps.ToArray();review.Apply();EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);var report=new{folder,backup,house="House 1846",removedHouseTriangles=removed,openHall=true,yellowSideStall=true,newTrees=0,roadGeometryChanged=false,shorelineExpanded=false};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
    static float widthForHall(float half)=>half*2;
    public static object BuildCoastalHouseStore()
    {
        const string title="Baybayon roadside house with sari-sari store";
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or store already exists");
        var records=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json"));var record=records.First(h=>(string)h["name"]=="House 1766");
        var poly=record["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();
        // Western edge faces Felix Amante; retain this house's original footprint.
        var a=poly[2];var b=poly[3];var along=(b-a).normalized;var inward=new Vector2(-along.y,along.x);var outward=-inward;var origin=(a+b)*.5f;
        float width=Vector2.Distance(a,b),depth=poly.Max(p=>Vector2.Dot(p-origin,inward)),half=width*.5f;
        var q=Quaternion.LookRotation(At(outward,0));var normal=At(outward,0);
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        float floor=poly.Max(p=>Ground(p))+.045f;Func<float,float,float,Vector3>P=(x,y,z)=>At(origin+along*x+inward*z,floor+y);
        for(float x=-half-.12f;x<=half+.12f;x+=.2f)for(float z=-1.65f;z<0;z+=.2f)if(Road(XZ(P(x,0,z)),.25f))throw new Exception("Store awning would overlap road");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/CoastalStore_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-store.unity",true))throw new Exception("Backup failed");
        folder="Assets/StormSim/Generated/CoastalStore_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Coastal house and sari-sari store");n=0;swaps.Clear();
        var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Replaces only House 1766 geometry with a low concrete/timber house and working shop opening. Original neighbourhood assets and wall sections retained. No new trees or port.";
        var batch=GameObject.Find("Banago Houses - full detail redesign").transform.Find("Neighbourhood "+(string)record["tile"]);int removed=0;
        foreach(var f in batch.GetComponentsInChildren<MeshFilter>())
        {
            var old=f.sharedMesh;if(!old)continue;var v=old.vertices;var m=UnityEngine.Object.Instantiate(old);int local=0;
            for(int s=0;s<old.subMeshCount;s++){var ix=old.GetTriangles(s);var keep=new List<int>();for(int i=0;i<ix.Length;i+=3){var mid=f.transform.TransformPoint((v[ix[i]]+v[ix[i+1]]+v[ix[i+2]])/3);if(In(XZ(mid),poly)){local++;continue;}keep.Add(ix[i]);keep.Add(ix[i+1]);keep.Add(ix[i+2]);}m.SetTriangles(keep,s);}
            if(local==0){UnityEngine.Object.DestroyImmediate(m);continue;}removed+=local;m.RecalculateBounds();AssetDatabase.CreateAsset(m,folder+"/Neighbourhood retained "+(++n)+".asset");var r=f.GetComponent<Renderer>();var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=old,after=m,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=c?c.sharedMesh:null});
        }
        var hidden=new List<GameObject>();var wall=GameObject.Find("Baybayon corner-to-corner concrete wall - adjustable sections");
        foreach(Transform t in wall.transform)if(t.name.StartsWith("Wall section")){var r=t.GetComponent<Renderer>();if(r&&r.bounds.max.z>978.2f&&r.bounds.min.z<987.0f)hidden.Add(t.gameObject);}
        hidden.AddRange(GameObject.Find(BanagoLivedInBuilder.RootName).GetComponentsInChildren<Transform>().Where(t=>t.parent&&(t.parent.name=="Rooftop satellite dishes"||t.parent.name=="Ribbed PVC rooftop tanks")&&In(XZ(t.position),poly)).Select(t=>t.gameObject));
        var visibility=root.AddComponent<BanagoLocalDetailReview>();visibility.originals=hidden.ToArray();visibility.originalActive=hidden.Select(g=>g.activeSelf).ToArray();visibility.notes="Open the roadside wall only at the shop approach. Old wall panels and roof props remain available for restoration.";visibility.Apply();
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Store faded paint timber and stock"};solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.21f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Store details.mat");
        var concrete=new Material(Shader.Find("StormSim/Banago Reference Areas")){name="Rough unfinished shop CHB"};concrete.SetFloat("_Mode",7);AssetDatabase.CreateAsset(concrete,folder+"/Shop concrete.mat");
        greenRoof=new Material(solid){name="Salvaged rusty zinc store roof"};greenRoof.SetFloat("_Corrugation",1);greenRoof.SetFloat("_Weathering",.60f);AssetDatabase.CreateAsset(greenRoof,folder+"/Store roofing.mat");
        var gray=new Color(.60f,.59f,.53f);var wood=new Color(.48f,.37f,.23f);var dark=new Color(.11f,.13f,.12f);var walls=new S();
        // Real opening between side piers, not a painted window on a solid wall.
        walls.Box(P(0,1.42f,depth-.13f),V(width-.12f,2.84f,.18f),gray,q);
        foreach(float x in new[]{-half+.10f,half-.10f})walls.Box(P(x,1.42f,depth*.5f),V(.18f,2.84f,depth-.12f),gray,q);
        walls.Box(P(-half+.9f,1.42f,.12f),V(1.62f,2.84f,.20f),gray,q);
        walls.Box(P(half-.18f,1.42f,.12f),V(.27f,2.84f,.20f),gray,q);
        walls.Box(P(.76f,.48f,.12f),V(width-1.92f,.96f,.20f),gray,q);
        walls.Box(P(0,2.7f,.12f),V(width,.27f,.20f),gray,q);
        Save("Rough blockwork house with open service counter",walls,concrete,true);
        var details=new S();details.Box(P(0,.01f,depth*.5f),V(width-.15f,.10f,depth-.15f),new Color(.43f,.41f,.35f),q);
        details.Box(P(.67f,1.0f,-.10f),V(width-1.8f,.13f,.58f),new Color(.66f,.56f,.28f),q);
        details.Box(P(.65f,1.65f,1.05f),V(width-2.02f,1.22f,.13f),dark,q);
        foreach(float y in new[]{1.05f,1.42f,1.83f,2.20f})details.Box(P(.65f,y,.83f),V(width-2.08f,.055f,.55f),wood,q);
        for(int shelf=0;shelf<3;shelf++)for(int k=0;k<9;k++){float x=-.68f+k*.31f,y=1.12f+shelf*.39f;var col=new[]{new Color(.21f,.40f,.25f),new Color(.64f,.24f,.13f),new Color(.29f,.47f,.59f),new Color(.77f,.65f,.29f)}[(k+shelf)%4];details.Box(P(x,y+.12f,.60f),V(.13f,.24f,.13f),col,q);details.Box(P(x,y+.26f,.60f),V(.06f,.055f,.06f),new Color(.82f,.79f,.66f),q);}
        foreach(float x in new[]{-half+.12f,half-.12f})details.Beam(P(x,-.06f,-1.50f),P(x,2.28f,-1.50f),.065f,wood);
        details.Beam(P(-half,2.28f,-1.50f),P(half,2.28f,-1.50f),.07f,wood);
        // Timber door to the dwelling at the side; the shop remains street-facing.
        details.Box(P(half+.008f,1.0f,depth-1.0f),V(.045f,2,1.0f),wood,q);
        Save("Store shelves bottles counter and timber awning posts",details,solid);
        var roof=new S();for(float x=-half-.15f;x<half+.15f;x+=.65f){float end=Mathf.Min(x+.67f,half+.15f);var col=((int)((x+half+.15f)/.65f))%4==0?new Color(.65f,.65f,.59f):new Color(.47f,.25f,.16f);roof.Q(P(x,2.91f,-.16f),P(end,2.91f,-.16f),P(end,3.72f,depth*.5f),P(x,3.72f,depth*.5f),col,Vector3.up);roof.Q(P(x,3.72f,depth*.5f),P(end,3.72f,depth*.5f),P(end,2.91f,depth+.15f),P(x,2.91f,depth+.15f),col*.94f,Vector3.up);var zinc=((int)((x+half+.15f)/.65f))%3==0?new Color(.49f,.30f,.21f):new Color(.62f,.64f,.60f);roof.Q(P(x,2.73f,.10f),P(end,2.73f,.10f),P(end,2.30f,-1.62f),P(x,2.30f,-1.62f),zinc,Vector3.up);}Save("Low rust roof and overlapping corrugated shop awning",roof,greenRoof);
        var chairs=new S();foreach(float cx in new[]{-1.65f,-.95f,1.9f}){float z=-.88f;var plastic=new Color(.31f,.61f,.46f);chairs.Box(P(cx,.46f,z),V(.44f,.07f,.43f),plastic,q);foreach(float dx in new[]{-.18f,.18f})foreach(float dz in new[]{-.16f,.16f})chairs.Beam(P(cx+dx,.02f,z+dz),P(cx+dx,.44f,z+dz),.045f,plastic);foreach(float dx in new[]{-.18f,.18f})chairs.Beam(P(cx+dx,.47f,z+.17f),P(cx+dx,.94f,z+.17f),.052f,plastic);chairs.Box(P(cx,.91f,z+.17f),V(.42f,.13f,.065f),plastic,q);chairs.Box(P(cx,.69f,z+.17f),V(.40f,.075f,.055f),plastic,q);}chairs.Box(P(-.23f,.72f,-1.03f),V(.60f,.055f,.47f),new Color(.65f,.61f,.43f),q);foreach(float dx in new[]{-.24f,.24f})foreach(float dz in new[]{-.18f,.18f})chairs.Beam(P(-.23f+dx,.02f,-1.03f+dz),P(-.23f+dx,.70f,-1.03f+dz),.045f,wood);Save("Three green plastic chairs and small roadside table",chairs,solid);
        var banners=new S();var cols=new[]{new Color(.20f,.44f,.62f),new Color(.77f,.45f,.18f),new Color(.24f,.48f,.33f)};for(int i=0;i<3;i++)banners.Box(P(-1.73f+i*1.72f,2.12f,-1.65f),V(1.67f,.39f,.022f),cols[i],q);banners.Box(P(0,3.10f,-.38f),V(2.55f,.70f,.035f),new Color(.16f,.39f,.25f),q);Save("Faded advertising banners and green store board",banners,solid);
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");letters=new Material(Shader.Find("StormSim/World Sign Text")){name="Store sign lettering"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/Store lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        Action<string,Vector3,float,float>label=(text,pos,w,h)=>{Sign(text,pos,normal,.05f);var t=root.transform.GetChild(root.transform.childCount-1).GetComponent<TextMesh>();t.color=new Color(.9f,.88f,.72f);Fit(t,w,h);};label("SARI-SARI STORE",P(0,3.11f,-.415f),2.35f,.35f);for(int i=0;i<3;i++)label(new[]{"COLD DRINKS","SNACKS","LOAD HERE"}[i],P(-1.73f+i*1.72f,2.12f,-1.68f),1.48f,.22f);
        var yard=new S();for(float x=-half-.1f;x<half+.1f;x+=.4f)for(float z=-2.2f;z<.1f;z+=.4f){var ps=new[]{XZ(P(x,0,z)),XZ(P(x+.41f,0,z)),XZ(P(x+.41f,0,z+.41f)),XZ(P(x,0,z+.41f))};if(ps.Any(p=>Road(p,.12f)))continue;var vs=ps.Select(p=>At(p,Ground(p)+.024f)).ToArray();yard.Q(vs[0],vs[3],vs[2],vs[1],new Color(.48f,.445f,.36f),Vector3.up);}Save("Unpaved shop approach - road kept clear",yard,solid);
        review.swaps=swaps.ToArray();review.Apply();EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        var report=new{folder,backup,house="House 1766",removedHouseTriangles=removed,openedWallSections=hidden.Count(g=>g.name.StartsWith("Wall section")),chairs=3,newTrees=0,originalObjectsDeleted=0,roadGeometryChanged=false};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
    public static object BuildAdventistAndLightCoastalHomes()
    {
        const string title="Banago Adventist church and light bamboo coastal homes";
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or pass already exists");
        var shader=Shader.Find("StormSim/Banago Reference Areas");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Reference shader must compile first");
        var data=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json"));
        // Reuse the long roadside footprint at the western approach, rather than
        // covering a road or inserting another building between occupied plots.
        var record=data.First(h=>(string)h["name"]=="House 1732");
        var poly=record["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();
        var a=poly[0];var along=(poly[1]-a).normalized;var inward=new Vector2(-along.y,along.x);var outward=-inward;
        float width=Vector2.Distance(a,poly[1]),depth=poly.Max(p=>Vector2.Dot(p-a,inward));
        var origin=a+along*(width*.5f);var q=Quaternion.LookRotation(At(outward,0));
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        for(float x=-width*.5f+.3f;x<width*.5f-.3f;x+=.5f)for(float z=.3f;z<depth-.3f;z+=.5f)if(Road(origin+along*x+inward*z,.15f))throw new Exception("Church footprint intersects road");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/AdventistReference_"+stamp;Directory.CreateDirectory(backup);
        if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-adventist.unity",true))throw new Exception("Backup failed");
        folder="Assets/StormSim/Generated/AdventistReference_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Adventist church and coastal palette");n=0;swaps.Clear();
        var review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Photo-guided church using existing House 1732 footprint; approximate landmark placement, not surveyed. Local coastal palette only. Original meshes and materials retained.";
        var batch=GameObject.Find("Banago Houses - full detail redesign").transform.Find("Neighbourhood "+(string)record["tile"]);
        int removed=0;
        foreach(var f in batch.GetComponentsInChildren<MeshFilter>())
        {
            var old=f.sharedMesh;if(!old)continue;var v=old.vertices;var m=UnityEngine.Object.Instantiate(old);int local=0;
            for(int s=0;s<old.subMeshCount;s++){var ix=old.GetTriangles(s);var keep=new List<int>();for(int i=0;i<ix.Length;i+=3){var mid=f.transform.TransformPoint((v[ix[i]]+v[ix[i+1]]+v[ix[i+2]])/3);if(In(XZ(mid),poly)){local++;continue;}keep.Add(ix[i]);keep.Add(ix[i+1]);keep.Add(ix[i+2]);}m.SetTriangles(keep,s);}
            if(local==0){UnityEngine.Object.DestroyImmediate(m);continue;}removed+=local;m.RecalculateBounds();AssetDatabase.CreateAsset(m,folder+"/Retained neighbourhood "+(++n)+".asset");var r=f.GetComponent<Renderer>();var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=old,after=m,materialsBefore=r.sharedMaterials,materialsAfter=r.sharedMaterials,colliderBefore=c?c.sharedMesh:null});
        }
        var coastal=GameObject.Find("Banago Baybayin - school-side waterfront reference").GetComponent<BanagoReferenceAreasReview>();
        var replacements=new Dictionary<Material,Material>();
        foreach(var s in coastal.swaps)
        {
            if(!s.target)continue;var r=s.target.GetComponent<Renderer>();var before=r.sharedMaterials;var after=before.ToArray();bool changed=false;
            for(int i=0;i<after.Length;i++){var mat=after[i];if(!mat||!AssetDatabase.GetAssetPath(mat).Contains("BanagoReferenceAreas_20260912_060319")||!mat.HasProperty("_Coastal")||mat.GetFloat("_Coastal")<.5f)continue;if(!replacements.TryGetValue(mat,out var copy)){copy=new Material(mat){name=mat.name+" light photo palette"};copy.SetFloat("_PhotoCoast",1);AssetDatabase.CreateAsset(copy,folder+"/Coastal palette "+replacements.Count+".mat");replacements.Add(mat,copy);}after[i]=copy;changed=true;}
            if(changed){var existing=swaps.FirstOrDefault(x=>x.target==s.target);if(existing!=null)existing.materialsAfter=after;else swaps.Add(new BanagoReferenceAreasReview.Swap{target=s.target,before=s.target.sharedMesh,materialsBefore=before,materialsAfter=after});}
        }
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Church pale plaster green trim"};solid.SetFloat("_VertexColor",1);solid.SetFloat("_Weathering",.17f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Church plaster.mat");
        greenRoof=new Material(solid){name="Church green corrugated roof"};greenRoof.SetFloat("_Corrugation",1);AssetDatabase.CreateAsset(greenRoof,folder+"/Church green roof.mat");
        float floor=poly.Max(p=>Ground(p))+.05f;float half=width*.5f-.35f,back=depth-.35f;
        Func<float,float,float,Vector3>P=(x,y,z)=>At(origin+along*x+inward*z,floor+y);
        var cream=new Color(.83f,.82f,.71f);var green=new Color(.24f,.44f,.30f);var dark=new Color(.12f,.19f,.15f);var walls=new S();
        walls.Box(P(0,1.6f,back*.5f),V(half*2,3.2f,back-.35f),cream,q);
        walls.Box(P(0,3.05f,.30f),V(half*2,.22f,.20f),green,q);
        // Roof ridge runs parallel to the narrow street frontage.
        var roof=new S();roof.Q(P(-half-.15f,3.18f,.16f),P(half+.15f,3.18f,.16f),P(half+.15f,4.50f,back*.5f),P(-half-.15f,4.50f,back*.5f),green,Vector3.up);
        roof.Q(P(-half-.15f,4.50f,back*.5f),P(half+.15f,4.50f,back*.5f),P(half+.15f,3.18f,back+.15f),P(-half-.15f,3.18f,back+.15f),green,Vector3.up);
        for(float x=-half+1;x<half;x+=2.1f){walls.Box(P(x,1.9f,.31f),V(1.15f,1.3f,.08f),green,q);walls.Box(P(x,1.9f,.25f),V(.97f,1.1f,.05f),dark,q);for(float dx=-.4f;dx<.5f;dx+=.2f)walls.Beam(P(x+dx,1.37f,.21f),P(x+dx,2.43f,.21f),.024f,cream);}
        walls.Box(P(0,1.2f,.18f),V(1.8f,2.4f,.13f),green,q);
        walls.Box(P(0,2.95f,.14f),V(8.5f,.48f,.10f),cream,q);
        foreach(float x in new[]{-half,half})walls.Box(P(x,1.2f,.30f),V(.20f,2.4f,.25f),cream,q);
        Save("Adventist church cream walls and green window grilles",walls,solid,true);Save("Adventist church low green pitched roof",roof,greenRoof);
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");letters=new Material(Shader.Find("StormSim/World Sign Text")){name="Church sign text"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/Church lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        Sign("BANAGO SEVENTH-DAY ADVENTIST CHURCH",P(0,2.96f,.075f),At(outward,0),.04f);var text=root.GetComponentInChildren<TextMesh>();text.color=new Color(.02f,.09f,.04f);Fit(text,8.15f,.32f);
        var hide=GameObject.Find(BanagoLivedInBuilder.RootName).GetComponentsInChildren<Transform>().Where(t=>t.parent&&(t.parent.name=="Ribbed PVC rooftop tanks"||t.parent.name=="Rooftop satellite dishes")&&In(XZ(t.position),poly)).Select(t=>t.gameObject).ToArray();var visibility=root.AddComponent<BanagoLocalDetailReview>();visibility.originals=hide;visibility.originalActive=hide.Select(g=>g.activeSelf).ToArray();visibility.Apply();
        review.swaps=swaps.ToArray();review.Apply();EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        var report=new{folder,backup,churchFootprint="House 1732",approximatePlacement=true,removedHouseTriangles=removed,lightCoastalMaterials=replacements.Count,housePositionsChanged=0,newTrees=0,portAdded=false};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
    public static object Build(bool barrierOnly=false,bool schoolOnly=false)
    {
        string activeName=schoolOnly?"Banago Elementary I and Felix Amante - photo reference":barrierOnly?"Baybayin tidal shoreline and barrier - no port":RootName;
        if(GameObject.Find(activeName))throw new Exception("Landmark pass already exists");var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MAIN_GAME.unity"||EditorApplication.isPlaying)throw new Exception("Wrong scene or play mode");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/ReferenceLandmarks_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-landmarks.unity",true))throw new Exception("Backup failed");
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>new{t.position,t.localRotation,t.localScale,active=t.gameObject.activeSelf});
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        roads=before.Keys.Select(t=>t.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.sharedMesh&&AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/StudyRoads_")).ToArray();
        folder="Assets/StormSim/Generated/BanagoReferenceLandmarks_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(activeName);Undo.RegisterCreatedObjectUndo(root,"School and coastal barrier review");surfaces=root.AddComponent<BanagoReferenceAreasReview>();surfaces.backupPath=backup;surfaces.notes=(barrierOnly?"Baybayin first: no school changes in this pass. ":"White school frontage and green roof/fence details from supplied photographs. Existing campus footprints retained. ")+"Former small-pier decoration is hidden reversibly; replaced by a narrow low coastal barrier, not a port.";
        solid=new Material(Shader.Find("StormSim/Banago Reference Areas")){name="School and barrier surfaces"};solid.SetFloat("_Mode",5);AssetDatabase.CreateAsset(solid,folder+"/School and barrier.mat");
        greenRoof=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="School pale silver corrugated roof"};greenRoof.SetFloat("_VertexColor",1);greenRoof.SetColor("_BaseColor",Color.white);greenRoof.SetFloat("_Corrugation",1);greenRoof.SetFloat("_Weathering",.10f);greenRoof.SetFloat("_Cull",0);AssetDatabase.CreateAsset(greenRoof,folder+"/School silver roof.mat");
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");letters=new Material(Shader.Find("StormSim/World Sign Text")){name="School sign lettering"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/School lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        swaps.Clear();n=0;var hidden=new List<GameObject>();
        int blocks=barrierOnly?0:School(hidden);int barrier=schoolOnly?0:Barrier(hidden);int bankPanels=schoolOnly?0:barrierOnly?TidalGround():GreenBanks();int highwaySurfaces=schoolOnly?SchoolHighway(hidden):0;
        if(schoolOnly)surfaces.notes="School photo reference applied to roadside classroom block 4: white pilasters and pediment, open upper corridor with olive-green balustrade, silver GI roof, red-and-white boundary and school sign. Existing campus footprints and highway route retained. No port or vegetation changes. Disable this root to restore original school meshes and road materials.";
        surfaces.swaps=swaps.ToArray();surfaces.Apply();EditorUtility.SetDirty(surfaces);
        var visibility=root.AddComponent<BanagoLocalDetailReview>();visibility.originals=hidden.ToArray();visibility.originalActive=hidden.Select(g=>g.activeSelf).ToArray();visibility.notes="Retained original small-pier deck/decoration, shelter and superseded classroom sign. Disable this group to restore. No objects deleted.";visibility.Apply();EditorUtility.SetDirty(visibility);
        var errors=new List<string>();foreach(var kv in before){var t=kv.Key;var a=kv.Value;if(!t){errors.Add("Missing original");continue;}if(t.position!=a.position||t.localRotation!=a.localRotation||t.localScale!=a.localScale)errors.Add("Moved original "+t.name);if(!hidden.Contains(t.gameObject)&&t.gameObject.activeSelf!=a.active)errors.Add("Changed unrelated active state "+t.name);}
        var report=new{folder,backup,schoolBlocksRestyled=blocks,roadsideFrontageBlock=schoolOnly?4:0,highwaySurfaces,coastalBarrier=barrier,greenBankPanels=bankPanels,retainedHiddenObjects=hidden.Select(g=>g.name).ToArray(),noNewPort=true,noOriginalObjectsDeleted=true,newTrees=0,existingObjectsChecked=before.Count,preservationErrors=errors};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();if(errors.Count>0)throw new Exception(JsonConvert.SerializeObject(report));return report;
    }
    static int School(List<GameObject> hide)
    {
        var civic=GameObject.Find(BanagoArchitectureBuilder.RootName);var records=JObject.Parse(File.ReadAllText("RoadRepairBackup/public-school-hall-data.json"))["records"].Where(d=>(bool)d["school"]).ToArray();
        var d=records.First(r=>(string)r["group"]=="Public school classroom block 4");float floor=(float)d["center"][1];var a=new Vector2(-13.243941f,643.10335f);var b=new Vector2(3.642485f,616.83557f);var along=(b-a).normalized;var outward=new Vector2(along.y,-along.x);var normal=At(outward,0);float width=Vector2.Distance(a,b);var origin=(a+b)*.5f;var q=Quaternion.LookRotation(normal,Vector3.up);var white=new Color(.88f,.875f,.835f);var green=new Color(.38f,.43f,.22f);var red=new Color(.50f,.20f,.17f);
        foreach(var rec in records)
        {
            string name=(string)rec["group"];var group=civic.transform.Find(name);foreach(var f in group.GetComponentsInChildren<MeshFilter>())
            {bool roof=f.name.Contains("pitched metal roof"),walls=f.name.Contains("concrete"),street=name=="Public school classroom block 4";if(!walls&&!roof&&!street)continue;var m=UnityEngine.Object.Instantiate(f.sharedMesh);m.name=f.sharedMesh.name+" school reference";var cs=m.colors;for(int i=0;i<cs.Length;i++)if(roof)cs[i]=new Color(.74f,.765f,.745f);else if(cs[i].r>.70f)cs[i]=Color.Lerp(cs[i],white,.90f);m.colors=cs;
             // Inset the roadside wall/windows to expose a genuine covered corridor.
             // Slab edges, footprint and roof stay at their existing coordinates.
             if(street&&!roof){var vs=m.vertices;var ns=m.normals;var ix=m.triangles;var moved=new HashSet<int>();for(int i=0;i<ix.Length;i+=3){var mid=(vs[ix[i]]+vs[ix[i+1]]+vs[ix[i+2]])/3;float depth=Vector2.Dot(XZ(mid)-a,outward);if(depth<-.24f||depth>.24f||Mathf.Abs(ns[ix[i]].y)>.35f)continue;foreach(int k in new[]{ix[i],ix[i+1],ix[i+2]})if(moved.Add(k)&&vs[k].y>floor+.17f&&vs[k].y<floor+6.29f){float x=Vector2.Dot(XZ(vs[k])-a,along);float taper=Mathf.Clamp01(Mathf.Min(x,width-x)/.5f);vs[k]-=normal*(1.12f*taper);}}m.vertices=vs;m.RecalculateNormals();m.RecalculateBounds();}
             AssetDatabase.CreateAsset(m,folder+"/School block "+(++n)+".asset");Swap(f,m,roof?greenRoof:solid);}
        }
        Func<float,float,float,Vector3> P=(x,y,z)=>At(origin+along*x+outward*z,floor+y);
        var facade=new S();
        // Full-height pilasters and open upper-floor balustrade; keep all original openings.
        for(float x=-width*.5f+.4f;x<width*.5f-.2f;x+=5.75f)
        {facade.Box(P(x,3.22f,.16f),V(.44f,6.44f,.38f),white,q);facade.Box(P(x,6.35f,.16f),V(.69f,.28f,.59f),white*.97f,q);}
        facade.Box(P(0,6.54f,.16f),V(width,.46f,.54f),white,q);facade.Box(P(0,3.14f,.19f),V(width,.20f,.43f),white*.94f,q);
        for(float x=-width*.5f+.5f;x<width*.5f-.2f;x+=.27f)facade.Beam(P(x,3.30f,.4f),P(x,4.17f,.4f),.035f,green);
        facade.Beam(P(-width*.5f+.3f,4.18f,.40f),P(width*.5f-.3f,4.18f,.40f),.075f,white);
        // Shallow central pediment matches the white institutional street-facing silhouette.
        float pediment=Mathf.Min(7.2f,width*.23f);facade.p.Face(P(-pediment,6.72f,.25f),P(pediment,6.72f,.25f),P(0,8.45f,.25f),white,normal);
        facade.Beam(P(-pediment-.1f,6.75f,.29f),P(0,8.49f,.29f),.18f,white);facade.Beam(P(0,8.49f,.29f),P(pediment+.1f,6.75f,.29f),.18f,white);
        Save("School I white columns green balustrade and pediment",facade,solid);
        Sign("BANAGO ELEMENTARY SCHOOL I",P(0,7.03f,.40f),normal,.23f);
        var fence=new S();float gateX=-width*.27f;int safe=0;
        for(float x=-width*.5f+.3f;x<width*.5f-.6f;x+=.65f)
        {if(Mathf.Abs(x-gateX)<2.3f)continue;var c=XZ(P(x,0,1.30f));if(Road(c,.55f)||Road(c+along*.34f,.55f)||Road(c-along*.34f,.55f))continue;float y=Ground(c);fence.Box(At(c,y+.35f),V(.66f,.70f,.18f),red,q);for(int k=0;k<3;k++){var cc=c+along*((k-1)*.21f);fence.Beam(At(cc,y+.72f),At(cc,y+1.64f),.033f,white);}fence.Beam(At(c-along*.34f,y+1.68f),At(c+along*.34f,y+1.68f),.064f,white);safe++;}
        Save("School red base and white grille fence - road kept clear",fence,solid);
        var entrance=XZ(P(gateX,0,1.30f));if(!Road(entrance,.7f))
        {var gate=new S();float y=Ground(entrance);foreach(float x in new[]{-2.30f,2.30f}){var p=entrance+along*x;if(!Road(p,.5f))gate.Box(At(p,y+1.15f),V(.30f,2.3f,.30f),white,q);}var c0=entrance-along*2.3f;var c1=entrance+along*2.3f;gate.Beam(At(c0,y+2.45f),At((c0+c1)*.5f,y+2.95f),.10f,green);gate.Beam(At((c0+c1)*.5f,y+2.95f),At(c1,y+2.45f),.10f,green);Save("Open school entrance arch",gate,solid);Sign("WELCOME",At(entrance,y+2.52f)+normal*.02f,normal,.15f);}
        var signPoint=XZ(P(width*.5f-1.8f,0,1.3f));if(!Road(signPoint,.6f)){float y=Ground(signPoint);var sign=new S();sign.Beam(At(signPoint,y),At(signPoint,y+2.15f),.045f,new Color(.43f,.44f,.42f));sign.Box(At(signPoint,y+1.77f),V(.72f,.86f,.035f),new Color(.75f,.66f,.32f),q);Save("Yellow school-zone roadside sign",sign,solid);Sign("SLOW\nDOWN\nSCHOOL\nZONE",At(signPoint,y+1.77f)+normal*.025f,normal,.12f);}
        var old=civic.GetComponentsInChildren<TextMesh>().FirstOrDefault(t=>t.text=="CLASSROOM BUILDING 4");if(old)hide.Add(old.gameObject);return records.Length;
    }
    static int SchoolHighway(List<GameObject> hide)
    {
        var mat=new Material(Shader.Find("StormSim/Banago Reference Areas")){name="Felix Amante worn asphalt without highway paint"};mat.SetFloat("_Mode",6);AssetDatabase.CreateAsset(mat,folder+"/Felix Amante worn asphalt.mat");int count=0;
        foreach(var f in GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshFilter>(true))
        {
            if(!f.sharedMesh)continue;bool main=f.name=="Felix Amante Drive";
            bool junction=f.name.StartsWith("Junction")&&f.GetComponent<Renderer>().bounds.center.x<20&&f.GetComponent<Renderer>().bounds.center.x> -175;
            if(main||junction){var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=null,materialsBefore=f.GetComponent<Renderer>().sharedMaterials,materialsAfter=Enumerable.Repeat(mat,f.sharedMesh.subMeshCount).ToArray(),colliderBefore=c?c.sharedMesh:null});count++;}
            if(f.name.Contains("markings")&&f.transform.parent.name=="Felix Amante Drive"&&f.gameObject.activeSelf)hide.Add(f.gameObject);
        }
        return count;
    }
    public static object BuildSchoolSideEntrances()
    {
        const string name="School side entrances - orange frontage and green gates";
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(name))throw new Exception("Wrong scene or side entrance already exists");
        var main=GameObject.Find("Banago Elementary I and Felix Amante - photo reference");if(!main)throw new Exception("Main school review missing");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/SchoolEntrances_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-entrances.unity",true))throw new Exception("Backup failed");
        var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();
        roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        folder="Assets/StormSim/Generated/SchoolEntrances_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(name);Undo.RegisterCreatedObjectUndo(root,"School side entrance reference");n=0;swaps.Clear();surfaces=root.AddComponent<BanagoReferenceAreasReview>();surfaces.backupPath=backup;surfaces.notes="Photo-guided orange end bay, open green gate, green-roofed older entry and arched name board. Original school meshes retained. No tree instances added. Access lane left open.";
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Weathered school painted plaster"};solid.SetFloat("_Corrugation",0);solid.SetFloat("_Weathering",.13f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Painted plaster.mat");
        greenRoof=new Material(solid){name="Weathered corrugated entrance roofs"};greenRoof.SetFloat("_Corrugation",1);greenRoof.SetFloat("_Weathering",.22f);AssetDatabase.CreateAsset(greenRoof,folder+"/Entrance roofs.mat");
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");letters=new Material(Shader.Find("StormSim/World Sign Text")){name="School entrance dark green lettering"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/Entrance lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        var a=new Vector2(-13.243941f,643.10335f);var b=new Vector2(3.642485f,616.83557f);var along=(b-a).normalized;var outw=new Vector2(along.y,-along.x);var normal=At(outw,0);var rotation=Quaternion.LookRotation(normal,Vector3.up);float floor=-1.1584284f;
        Func<float,float,float,Vector3> P=(x,y,z)=>At(a+along*(x*.9f)+outw*z,floor+y);
        var orange=new Color(.76f,.345f,.17f);var gold=new Color(.64f,.58f,.27f);var dark=new Color(.15f,.19f,.18f);
        // User explicitly placed the orange annex BESIDE the white building.
        // The verified six-metre strip is outside its footprint and access road.
        for(float x=-6.35f;x<-.35f;x+=.25f)for(float z=0;z>=-6;z-=.25f)if(Road(XZ(P(x,0,z)),.12f))throw new Exception("Annex would obstruct the access road");
        var annex=new S();var details=new S();float left=-6.35f,right=-.35f;
        var openings=new[]{new[]{-5.85f,-4.65f,1.0f,2.08f},new[]{-3.90f,-2.85f,.10f,2.15f},new[]{-2.1f,-.85f,1.0f,2.08f},new[]{-5.90f,-3.65f,4.02f,5.36f},new[]{-3.08f,-.83f,4.02f,5.36f}};
        var stops=new[]{left,right}.Concat(openings.SelectMany(o=>o.Take(2))).Distinct().OrderBy(v=>v).ToArray();
        for(int k=0;k<stops.Length-1;k++){float x0=stops[k],x1=stops[k+1],cursor=.1f;foreach(var o in openings.Where(o=>(x0+x1)*.5f>o[0]&&(x0+x1)*.5f<o[1]).OrderBy(o=>o[2])){if(o[2]>cursor)annex.Q(P(x0,cursor,.1f),P(x1,cursor,.1f),P(x1,o[2],.1f),P(x0,o[2],.1f),orange,normal);cursor=o[3];}annex.Q(P(x0,cursor,.1f),P(x1,cursor,.1f),P(x1,6.35f,.1f),P(x0,6.35f,.1f),orange,normal);}
        foreach(var o in openings){details.Q(P(o[0],o[2],.06f),P(o[1],o[2],.06f),P(o[1],o[3],.06f),P(o[0],o[3],.06f),dark,normal);foreach(float x in new[]{o[0],o[1]})details.Beam(P(x,o[2]-.05f,.14f),P(x,o[3]+.05f,.14f),.075f,gold);foreach(float h in new[]{o[2],o[3]})details.Beam(P(o[0]-.05f,h,.14f),P(o[1]+.05f,h,.14f),.075f,gold);for(float h=o[2]+.18f;h<o[3];h+=.22f)details.Beam(P(o[0],h,.16f),P(o[1],h,.16f),.022f,dark*.7f);}
        foreach(float x in new[]{left,right})annex.Q(P(x,.1f,.1f),P(x,.1f,-6),P(x,6.35f,-6),P(x,6.35f,.1f),orange,x==left?-At(along,0):At(along,0));
        annex.Q(P(left,.1f,-6),P(right,.1f,-6),P(right,6.35f,-6),P(left,6.35f,-6),orange,-normal);
        foreach(float h in new[]{.03f,3.13f,6.30f})annex.Box(P(-3.35f,h,-2.95f),V(5.4f,.14f,6.1f),new Color(.56f,.54f,.48f),rotation);
        annex.Box(P(-3.35f,7.3f,.20f),V(5.4f,1.9f,.18f),orange,rotation);foreach(float h in new[]{6.34f,8.25f})details.Beam(P(left,h,.32f),P(right,h,.32f),.12f,gold);
        for(float x=left+.35f;x<right-.2f;x+=.43f)details.Box(P(x,7.30f,.34f),V(.075f,1.80f,.08f),gold,rotation);
        Save("Orange annex beside white school - walls and parapet",annex,solid,true);Save("Yellow trim and barred annex windows",details,solid);
        var awning=new S();awning.Q(P(left-.12f,2.62f,.2f),P(right+.1f,2.62f,.2f),P(right+.1f,2.30f,1.12f),P(left-.12f,2.30f,1.12f),new Color(.43f,.235f,.20f),Vector3.up);awning.Q(P(left,6.40f,.03f),P(right,6.40f,.03f),P(right,6.60f,-6),P(left,6.60f,-6),new Color(.48f,.43f,.38f),Vector3.up);Save("Low red awning and annex GI roof",awning,greenRoof);
        // Green gateway is beside the school end, outside the existing vehicle lane.
        var center=new Vector2(-22.4f,654.4f);float y=Ground(center);Func<float,float,float,Vector3> G=(x,h,z)=>At(center+along*x+outw*z,y+h);
        var gate=new S();var green=new Color(.17f,.32f,.23f);int checkedPosts=0;
        foreach(float x in new[]{-2.8f,2.8f}){var p=XZ(G(x,0,0));if(Road(p,.25f))throw new Exception("Gate post would obstruct existing access lane");gate.Box(G(x,1.15f,0),V(.27f,2.3f,.30f),new Color(.61f,.53f,.44f),rotation);checkedPosts++;}
        // Leaves folded back into the site, leaving the central opening unobstructed.
        foreach(float x in new[]{-2.72f,2.72f}){gate.Box(G(x,.40f,-1.05f),V(.07f,.78f,2.0f),green,rotation);for(float z=-2;z<0;z+=.14f)gate.Beam(G(x,.80f,z),G(x,1.85f,z),.022f,green);gate.Beam(G(x,1.87f,-2),G(x,1.87f,0),.05f,green);}
        Save("Open green school side gates",gate,solid,true);
        // The older covered pedestrian entry sits on the north side of the gate.
        var entryCenter=center-along*5.4f;float entryY=Ground(entryCenter);Func<float,float,float,Vector3> E=(x,h,z)=>At(entryCenter+along*x+outw*z,entryY+h);
        var older=new S();for(float x=-2.4f;x<=2.4f;x+=1.2f){var p=XZ(E(x,0,-.8f));if(Road(p,.2f))continue;older.Box(E(x,1.08f,-.8f),V(.16f,2.16f,.16f),new Color(.72f,.71f,.64f),rotation);}
        older.Box(E(0,.40f,-.8f),V(4.8f,.8f,.13f),new Color(.44f,.18f,.17f),rotation);for(float x=-2.35f;x<2.4f;x+=.14f)older.Beam(E(x,.82f,-.8f),E(x,2.10f,-.8f),.026f,new Color(.60f,.63f,.56f));
        Save("Older entrance red base and aged metal grilles",older,solid,true);
        var roof=new S();roof.Q(E(-2.65f,2.22f,.15f),E(2.65f,2.22f,.15f),E(2.65f,2.55f,-1.6f),E(-2.65f,2.55f,-1.6f),new Color(.35f,.44f,.28f),Vector3.up);Save("Faded green covered entry roof",roof,greenRoof);
        // Elliptical sign band and individually rotated lettering follow the photographed arch.
        var arch=new S();for(int k=0;k<40;k++){float t=k*Mathf.PI/40,u=(k+1)*Mathf.PI/40;arch.Q(E(Mathf.Cos(t)*2.5f,2.66f+Mathf.Sin(t)*1.65f,-.7f),E(Mathf.Cos(u)*2.5f,2.66f+Mathf.Sin(u)*1.65f,-.7f),E(Mathf.Cos(u)*2.5f,3.05f+Mathf.Sin(u)*1.65f,-.7f),E(Mathf.Cos(t)*2.5f,3.05f+Mathf.Sin(t)*1.65f,-.7f),new Color(.73f,.73f,.63f),normal);}
        foreach(float x in new[]{-2.5f,2.5f})arch.Beam(E(x,2.1f,-.7f),E(x,3.08f,-.7f),.07f,green);arch.Box(E(0,3.01f,-.69f),V(4.6f,.28f,.045f),new Color(.72f,.73f,.64f),rotation);Save("Banago Elementary curved entrance sign",arch,solid);
        string title="BANAGO ELEMENTARY SCHOOL I";for(int k=0;k<title.Length;k++){if(title[k]==' ')continue;float t=Mathf.Lerp(Mathf.PI-.12f,.12f,(k+.5f)/title.Length);Sign(title[k].ToString(),E(Mathf.Cos(t)*2.5f,2.85f+Mathf.Sin(t)*1.65f,-.655f),normal,.06f);var text=root.transform.GetChild(root.transform.childCount-1).GetComponent<TextMesh>();text.color=new Color(.055f,.16f,.08f);text.transform.Rotate(0,0,(t-Mathf.PI*.5f)*Mathf.Rad2Deg);Fit(text,.22f,.29f);}
        Sign("DISTRICT I",E(0,3.01f,-.65f),normal,.05f);Fit(root.transform.GetChild(root.transform.childCount-1).GetComponent<TextMesh>(),2.3f,.23f);
        surfaces.swaps=swaps.ToArray();surfaces.Apply();EditorUtility.SetDirty(surfaces);
        // Correct the earlier labels to physical signboard dimensions, not billboard scale.
        foreach(var t in main.GetComponentsInChildren<TextMesh>()){if(t.text=="BANAGO ELEMENTARY SCHOOL I")Fit(t,10.5f,.42f);else if(t.text=="WELCOME")Fit(t,2.2f,.27f);else if(t.text.Contains("SCHOOL\nZONE"))Fit(t,.58f,.68f);EditorUtility.SetDirty(t);}
        var bad=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertices.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));
        var report=new{folder,backup,orangeAnnexWidth=5.4f,annexBesideWhiteBuilding=true,greenGateClearOpening=5.44f,gatePostsClearOfRoad=checkedPosts==2,archedSchoolSign=true,newTrees=0,originalObjectsDeleted=original.Count(t=>!t),badVertices=bad};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return report;
    }
    static void Fit(TextMesh text,float width,float height)
    {
        // Font metrics are available immediately, unlike a newly created renderer's bounds.
        text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
        var lines=text.text.Split('\n');float advance=1;
        foreach(var line in lines){float total=0;foreach(char ch in line){if(text.font.GetCharacterInfo(ch,out var info,text.fontSize,text.fontStyle))total+=info.advance;else total+=text.fontSize*.5f;}advance=Mathf.Max(advance,total);}
        text.characterSize=Mathf.Min(width*10/advance,height*10/Mathf.Max(1,text.fontSize*lines.Length));
    }
    public static object BuildOppositeSchoolShops()
    {
        const string title="Opposite school - shopfronts balconies and laundry";var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or shops already exist");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/SchoolOpposite_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-shops.unity",true))throw new Exception("Backup failed");
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>new{t.position,t.localRotation,t.localScale,active=t.gameObject.activeSelf});
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];grid=e["heights"].Values<float>().ToArray();roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        var allHomes=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(h=>new{poly=h["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray(),height=(float)h["height"],name=(string)h["name"]}).ToArray();
        var edges=new List<(Vector2 a,Vector2 b)>();foreach(var c in roads.Where(c=>c.name=="Felix Amante Drive")){var vs=c.sharedMesh.vertices;var ix=c.sharedMesh.triangles;for(int i=0;i<ix.Length;i+=3)for(int k=0;k<3;k++)edges.Add((XZ(c.transform.TransformPoint(vs[ix[i+k]])),XZ(c.transform.TransformPoint(vs[ix[i+(k+1)%3]]))));}
        Func<Vector2,Vector2> nearest=p=>{float best=float.MaxValue;var result=p;foreach(var edge in edges){var d=edge.b-edge.a;var q=edge.a+d*Mathf.Clamp01(Vector2.Dot(p-edge.a,d)/Mathf.Max(.0001f,d.sqrMagnitude));if((p-q).sqrMagnitude<best){best=(p-q).sqrMagnitude;result=q;}}return result;};
        folder="Assets/StormSim/Generated/SchoolOpposite_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Opposite-school shopfront reference");n=0;
        solid=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Faded shop paint and timber"};solid.SetFloat("_Weathering",.22f);solid.SetFloat("_Cull",0);AssetDatabase.CreateAsset(solid,folder+"/Shop surfaces.mat");greenRoof=new Material(solid){name="Weathered shop awnings"};greenRoof.SetFloat("_Corrugation",1);AssetDatabase.CreateAsset(greenRoof,folder+"/Shop awnings.mat");
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");letters=new Material(Shader.Find("StormSim/World Sign Text")){name="Shop lettering"};letters.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(letters,folder+"/Shop lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=letters;
        int shops=0,balconies=0,cars=0;var names=new[]{"DADAI STORE","SNACKS & DRINKS","SARI-SARI STORE","SCHOOL SUPPLIES"};var usedCars=new List<Bounds>();var source=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName).transform.Find("car");
        foreach(var h in allHomes)
        {
            var center=new Vector2(h.poly.Average(p=>p.x),h.poly.Average(p=>p.y));if(center.y<590||center.y>675||center.x>0||center.x< -65)continue;var roadPoint=nearest(center);if(center.x>roadPoint.x-.5f||Vector2.Distance(center,roadPoint)>14)continue;
            float best=-999;Vector2 start=default,end=default,outward=default;for(int k=0;k<h.poly.Length-1;k++){var a=h.poly[k];var b=h.poly[k+1];var d=b-a;float length=d.magnitude;if(length<2)continue;var outw=new Vector2(d.y,-d.x).normalized;var mid=(a+b)*.5f;if(Vector2.Dot(outw,mid-center)<0)outw=-outw;float facing=Vector2.Dot(outw,(roadPoint-center).normalized);if(facing<.60f)continue;float score=length*facing;if(score>best){best=score;start=a;end=b;outward=outw;}}
            if(best<0)continue;var origin=(start+end)*.5f;var along=(end-start).normalized;float width=Mathf.Min(5.5f,Vector2.Distance(start,end)-.35f);float gap=Vector2.Distance(origin,nearest(origin));float depth=Mathf.Min(1.10f,gap-.75f);if(depth<.42f)continue;
            bool safe=true;for(float x=-width*.5f;x<=width*.5f;x+=.25f)for(float d=.12f;d<=depth;d+=.2f){var p=origin+along*x+outward*d;if(Road(p,.40f)||allHomes.Any(o=>o!=h&&In(p,o.poly)))safe=false;}if(!safe)continue;
            float floor=Ground(origin)+.09f;var normal=At(outward,0);var rotation=Quaternion.LookRotation(normal,Vector3.up);Func<float,float,float,Vector3> P=(x,y,z)=>At(origin+along*x+outward*z,floor+y);
            var roof=new S();var colors=new[]{new Color(.47f,.47f,.42f),new Color(.23f,.36f,.44f),new Color(.46f,.26f,.21f),new Color(.34f,.41f,.29f)};roof.Q(P(-width*.5f,2.55f,.05f),P(width*.5f,2.55f,.05f),P(width*.5f,2.32f,depth),P(-width*.5f,2.32f,depth),colors[shops%4],Vector3.up);Save(h.name+" corrugated storefront awning",roof,greenRoof);
            var front=new S();var paint=shops%2==0?new Color(.66f,.52f,.22f):new Color(.49f,.27f,.22f);front.Box(P(0,2.85f,.15f),V(width,.53f,.09f),paint,rotation);front.Box(P(0,.80f,.23f),V(Mathf.Min(2.4f,width-.3f),.11f,.38f),new Color(.46f,.37f,.27f),rotation);for(float x=-width*.40f;x<width*.40f;x+=.27f)front.Beam(P(x,.93f,.08f),P(x,2.16f,.08f),.026f,paint);foreach(float x in new[]{-width*.48f,width*.48f})front.Beam(P(x,0,depth-.06f),P(x,2.33f,depth-.06f),.055f,new Color(.29f,.27f,.22f));Save(h.name+" grille counter and signboard",front,solid);
            Sign(names[shops%names.Length],P(0,2.85f,.21f),normal,.06f);var text=root.transform.GetChild(root.transform.childCount-1).GetComponent<TextMesh>();text.color=new Color(.12f,.12f,.09f);Fit(text,width*.85f,.32f);
            if(h.height>5.2f&&width>3.4f){var balcony=new S();float deck=3.16f;balcony.Box(P(0,deck,depth*.5f),V(width,.13f,depth),new Color(.52f,.43f,.32f),rotation);for(float x=-width*.48f;x<width*.49f;x+=.2f)balcony.Beam(P(x,deck+.12f,depth),P(x,deck+.93f,depth),.027f,new Color(.35f,.31f,.24f));balcony.Beam(P(-width*.5f,deck+.96f,depth),P(width*.5f,deck+.96f,depth),.045f,new Color(.38f,.32f,.23f));Save(h.name+" narrow upper balcony",balcony,solid);var laundry=BanagoLivedInModels.Laundry(balconies%3);AssetDatabase.CreateAsset(laundry,folder+"/Laundry "+balconies+".asset");var l=new GameObject(h.name+" balcony laundry");l.transform.SetParent(root.transform,false);l.transform.position=P(0,deck+.04f,depth*.7f);l.transform.rotation=rotation;l.transform.localScale=Vector3.one*.70f;l.AddComponent<MeshFilter>().sharedMesh=laundry;l.AddComponent<MeshRenderer>().sharedMaterial=solid;balconies++;}
            if(source&&cars<3&&gap>2.5f){var edge=nearest(origin);var p=edge+(origin-edge).normalized*1.35f;var car=UnityEngine.Object.Instantiate(source.GetChild(cars%source.childCount).gameObject,root.transform);car.name="School opposite-side parked car "+(cars+1);car.transform.localScale=Vector3.one;car.transform.position=At(p,Ground(p)+.05f);car.transform.rotation=Quaternion.LookRotation(At(along,0),Vector3.up);var renderers=car.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);bool clear=true;for(float x=bounds.min.x;x<=bounds.max.x;x+=.25f)for(float z=bounds.min.z;z<=bounds.max.z;z+=.25f){var point=new Vector2(x,z);if(Road(point,.15f)||allHomes.Any(o=>In(point,o.poly)))clear=false;}if(usedCars.Any(b=>b.Intersects(bounds)))clear=false;if(clear){usedCars.Add(bounds);cars++;}else UnityEngine.Object.DestroyImmediate(car);}
            shops++;
        }
        var errors=before.Where(kv=>!kv.Key||kv.Key.position!=kv.Value.position||kv.Key.localRotation!=kv.Value.localRotation||kv.Key.localScale!=kv.Value.localScale||kv.Key.gameObject.activeSelf!=kv.Value.active).Select(kv=>kv.Key?kv.Key.name:"missing").ToArray();var report=new{folder,backup,shopfronts=shops,balconiesWithLaundry=balconies,parkedCars=cars,newTrees=0,preservationErrors=errors};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return report;
    }
    static int Barrier(List<GameObject> hide)
    {
        var groups=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var pier=groups.First(t=>t.name=="Small pier - port reference details");var shelter=groups.First(t=>t.name=="Pier shelter and moored workboat");
        // Keep nearby standalone fishing boats; retire only the authored port-styled deck and structures.
        hide.Add(pier.gameObject);foreach(Transform t in shelter)if(t.name.StartsWith("Small pier shelter")||t.name.StartsWith("Pier shelter windows"))hide.Add(t.gameObject);
        var oldSign=groups.FirstOrDefault(t=>t.name=="PIER SHELTER");if(oldSign)hide.Add(oldSign.gameObject);
        var s=new S();var a=new Vector2(-385.48376f+182f/1500*1420,1479.08044f-700f/1500*1420);var b=new Vector2(-385.48376f+230f/1500*1420,1479.08044f-704f/1500*1420);var d=(b-a).normalized;var q=Quaternion.LookRotation(At(new Vector2(d.y,-d.x),0),Vector3.up);float length=Vector2.Distance(a,b),top=-5.48f;
        for(float x=0;x<length;x+=2){float size=Mathf.Min(2,length-x);var p=a+d*(x+size*.5f);var col=new Color(.45f,.44f,.37f)*(.91f+.1f*Mathf.PerlinNoise(x*.8f,7));s.Box(At(p,top-.85f),V(size+.01f,1.70f,1.35f),col,q);s.Box(At(p,top+.045f),V(size+.01f,.09f,1.42f),col*1.12f,q);}
        Save("Narrow concrete coastal barrier - no port",s,solid,true);return 1;
    }
    public static object FinishNoPortCleanup()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MAIN_GAME.unity"||EditorApplication.isPlaying)throw new Exception("Wrong scene or play mode");
        var reviewRoot=GameObject.Find("Baybayin tidal shoreline and barrier - no port");
        if(!reviewRoot)throw new Exception("Build the Baybayin barrier review first");
        var review=reviewRoot.GetComponent<BanagoReferenceAreasReview>();
        var visibility=reviewRoot.GetComponent<BanagoLocalDetailReview>();
        var sand=GameObject.Find("Wider rough sand - existing water retained").GetComponent<MeshFilter>();
        if(review.swaps.Any(s=>s.target==sand))throw new Exception("Port cleanup already applied");
        // Remove only the later decorative widening around the former dock, not the
        // original narrow traced barrier, coastline, houses, sea mesh or road geometry.
        var original=sand.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);
        var vertices=original.vertices;var indices=original.triangles;var kept=new List<int>();int removed=0;
        var a=new Vector2(-385.48376f+182f/1500*1420,1479.08044f-700f/1500*1420);
        var b=new Vector2(-385.48376f+230f/1500*1420,1479.08044f-704f/1500*1420);
        var direction=(b-a).normalized;float length=Vector2.Distance(a,b);
        for(int i=0;i<indices.Length;i+=3)
        {
            var mid=(sand.transform.TransformPoint(vertices[indices[i]])+sand.transform.TransformPoint(vertices[indices[i+1]])+sand.transform.TransformPoint(vertices[indices[i+2]]))/3;
            var delta=XZ(mid)-a;float along=Vector2.Dot(delta,direction),across=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
            if(along> -4&&along<length-4.5f&&across<7){removed++;continue;}
            kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);
        }
        if(removed==0)throw new Exception("No decorative dock widening found");
        mesh.name="Dirty shoreline - obsolete dock widening removed";mesh.triangles=kept.ToArray();mesh.RecalculateBounds();
        string assetFolder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(reviewRoot.transform.Find("Narrow concrete coastal barrier - no port").GetComponent<MeshFilter>().sharedMesh)).Replace('\\','/');
        AssetDatabase.CreateAsset(mesh,assetFolder+"/Shoreline without port widening.asset");
        var renderer=sand.GetComponent<Renderer>();var collider=sand.GetComponent<MeshCollider>();
        review.swaps=review.swaps.Concat(new[]{new BanagoReferenceAreasReview.Swap{target=sand,before=original,after=mesh,materialsBefore=renderer.sharedMaterials,materialsAfter=renderer.sharedMaterials,colliderBefore=collider?collider.sharedMesh:null}}).ToArray();
        var sign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="PIER SHELTER");
        if(sign&&!visibility.originals.Contains(sign.gameObject)){visibility.originals=visibility.originals.Concat(new[]{sign.gameObject}).ToArray();visibility.originalActive=visibility.originalActive.Concat(new[]{sign.gameObject.activeSelf}).ToArray();}
        review.Apply();visibility.Apply();EditorUtility.SetDirty(review);EditorUtility.SetDirty(visibility);
        var report=new{removedDecorativeDockTriangles=removed,retainedOriginalMesh=AssetDatabase.GetAssetPath(original),newMesh=AssetDatabase.GetAssetPath(mesh),hiddenPortItems=visibility.originals.Select(g=>g.name).ToArray(),originalVerticesUnchanged=mesh.vertices.SequenceEqual(original.vertices),originalObjectsDeleted=0,roadGeometryChanged=false,seaGeometryChanged=false};
        File.WriteAllText(review.backupPath+"/no-port-cleanup.json",JsonConvert.SerializeObject(report,Formatting.Indented));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return report;
    }
    static int GreenBanks()
    {
        var ponds=JObject.Parse(File.ReadAllText("RoadRepairBackup/banago-inland-data.json"))["ponds"].Select(d=>d["polygon"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray()).ToArray();var houses=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(d=>d["footprint"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray()).ToArray();var sh=new S();int count=0;
        foreach(var poly in ponds)for(int i=0;i<poly.Length-1;i++)
        {var a=poly[i];var b=poly[i+1];var d=(b-a).normalized;var outw=new Vector2(d.y,-d.x);if(In((a+b)*.5f+outw*.2f,poly))outw=-outw;float len=Vector2.Distance(a,b);
         for(float x=.5f;x<len-.5f;x+=1.25f)foreach(float offset in new[]{.6f,1.55f,2.5f})
         {var p=a+d*x+outw*offset;var corners=new[]{p-d*.60f-outw*.43f,p+d*.60f-outw*.43f,p+d*.60f+outw*.43f,p-d*.60f+outw*.43f};if(corners.Any(c=>Road(c,.55f)||ponds.Any(pp=>In(c,pp))||houses.Any(h=>c.x>=h.Min(v=>v.x)-.2f&&c.x<=h.Max(v=>v.x)+.2f&&c.y>=h.Min(v=>v.y)-.2f&&c.y<=h.Max(v=>v.y)+.2f&&In(c,h))))continue;float noise=Mathf.PerlinNoise(p.x*.65f,p.y*.65f);if(noise<.26f)continue;var col=Color.Lerp(new Color(.19f,.29f,.10f),new Color(.31f,.40f,.18f),noise);sh.Q(At(corners[0],Ground(corners[0])+.038f),At(corners[3],Ground(corners[3])+.038f),At(corners[2],Ground(corners[2])+.038f),At(corners[1],Ground(corners[1])+.038f),col);count++;}
        }
        Save("Low green margins outside existing pond banks",sh,solid);return count;
    }
    static int TidalGround()
    {
        var sh=new S();int triangles=0;var filters=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh&&f.name.StartsWith("Measured terrain")).ToArray();
        foreach(var f in filters){var m=f.sharedMesh;var vertices=m.vertices;var ix=m.triangles;for(int i=0;i<ix.Length;i+=3){var a=f.transform.TransformPoint(vertices[ix[i]]);var b=f.transform.TransformPoint(vertices[ix[i+1]]);var c=f.transform.TransformPoint(vertices[ix[i+2]]);var mid=(a+b+c)/3;if(mid.z<610||mid.z>835||mid.x< -225||mid.x> -90)continue;var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<.25f)continue;float noise=Mathf.PerlinNoise(mid.x*.31f,mid.z*.31f);var color=Color.Lerp(new Color(.32f,.315f,.27f),new Color(.43f,.42f,.355f),noise);color=Color.Lerp(color,new Color(.255f,.30f,.20f),Mathf.Clamp01((noise-.64f)*1.4f));sh.p.Face(a+Vector3.up*.018f,b+Vector3.up*.018f,c+Vector3.up*.018f,color,normal);triangles++;}}
        Save("Baybayin gray-brown tidal ground - original height retained",sh,solid);return triangles;
    }
}
