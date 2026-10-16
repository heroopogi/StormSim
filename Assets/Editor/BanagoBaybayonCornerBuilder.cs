using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Local, reversible street-reference treatment; no global map regeneration.</summary>
public static class BanagoBaybayonCornerBuilder
{
    public const string RootName="Baybayon corner - worn road blue fence and bamboo frontage";
    static GameObject root; static string folder,backup; static int asset;
    static Material rough,pavement,concrete,blueRoof;
    static MeshCollider[] roads,ground; static List<Vector2[]> homes;
    static BanagoReferenceAreasReview review;
    static readonly List<BanagoReferenceAreasReview.Swap> swaps=new List<BanagoReferenceAreasReview.Swap>();
    static readonly List<Bounds> occupied=new List<Bounds>();
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static bool Area(Vector3 p)=>p.x> -190&&p.x< -90&&p.z>780&&p.z<905;
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool b=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)b=!b;return b;}
    static bool Hit(MeshCollider c,Vector2 p,out float y)
    {y=0;var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.z||p.y>b.max.z)return false;if(c.Raycast(new Ray(V(p.x,50,p.y),Vector3.down),out var hit,100)){y=hit.point.y;return true;}return false;}
    static bool Ground(Vector2 p,out float y)
    {foreach(var c in ground)if(Hit(c,p,out y))return true;y=0;return false;}
    static bool Road(Vector2 p,float margin=0)
    {foreach(var q in new[]{p,p+Vector2.right*margin,p-Vector2.right*margin,p+Vector2.up*margin,p-Vector2.up*margin})foreach(var c in roads)if(Hit(c,q,out _))return true;return false;}
    static bool Clear(Vector2 p,float margin=.25f)
    {if(!Ground(p,out _)||Road(p,margin))return false;foreach(var q in new[]{p,p+Vector2.right*margin,p-Vector2.right*margin,p+Vector2.up*margin,p-Vector2.up*margin})if(homes.Any(h=>Inside(q,h)))return false;return true;}
    sealed class Shape
    {
        public BanagoEnvironmentModels.Parts p=new BanagoEnvironmentModels.Parts();
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col,Vector3 normal)
        {p.Face(a,b,c,col,normal);p.Face(a,c,d,col,normal);}
        public void Box(Vector3 center,Vector3 size,Color col,Quaternion rot=default)
        {if(rot==default)rot=Quaternion.identity;var v=new[]{V(-1,-1,-1),V(1,-1,-1),V(1,1,-1),V(-1,1,-1),V(-1,-1,1),V(1,-1,1),V(1,1,1),V(-1,1,1)}.Select(s=>center+rot*Vector3.Scale(s,size*.5f)).ToArray();foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}})Q(v[f[0]],v[f[1]],v[f[2]],v[f[3]],col,Vector3.Cross(v[f[1]]-v[f[0]],v[f[2]]-v[f[0]]));}
        public void Beam(Vector3 a,Vector3 b,float w,Color col)=>Box((a+b)*.5f,V(w,Vector3.Distance(a,b),w),col,Quaternion.FromToRotation(Vector3.up,(b-a).normalized));
    }
    static GameObject Save(string name,Shape s,Material mat,bool collider=false)
    {if(s.p.vertices.Count==0)return null;var m=s.p.Mesh(name);m.uv=m.vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToArray();AssetDatabase.CreateAsset(m,folder+"/Corner mesh "+(++asset)+".asset");var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;return g;}
    static Material Mat(string name,int mode)
    {var m=new Material(Shader.Find("StormSim/Banago Reference Areas")){name=name};m.SetFloat("_Mode",mode);AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
    static void Swap(MeshFilter f,Mesh after,Material[] mats)
    {var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=after,materialsBefore=f.GetComponent<Renderer>().sharedMaterials,materialsAfter=mats,colliderBefore=c?c.sharedMesh:null});}
    static int Roads(Transform[] before,out int markings)
    {
        int surfaces=0;markings=0;
        foreach(var t in before)
        {
            var f=t.GetComponent<MeshFilter>();if(!f||!f.sharedMesh||!t.gameObject.activeInHierarchy||!AssetDatabase.GetAssetPath(f.sharedMesh).Contains("/StudyRoads_"))continue;
            bool line=t.name.Contains("markings");var m=f.sharedMesh;var v=m.vertices;var keep=new List<int>();var local=new List<int>();var ix=m.triangles;
            for(int i=0;i<ix.Length;i+=3){var p=f.transform.TransformPoint((v[ix[i]]+v[ix[i+1]]+v[ix[i+2]])/3);var dst=Area(p)?local:keep;dst.Add(ix[i]);dst.Add(ix[i+1]);dst.Add(ix[i+2]);}
            if(local.Count==0)continue;if(m.subMeshCount!=1)throw new Exception("Unexpected road topology "+t.name);
            var replacement=UnityEngine.Object.Instantiate(m);replacement.name=m.name+" Baybayon corner";
            Material[] mats;if(line){replacement.triangles=keep.ToArray();mats=f.GetComponent<Renderer>().sharedMaterials;markings+=local.Count/3;}
            else{replacement.subMeshCount=2;replacement.SetTriangles(keep,0);replacement.SetTriangles(local,1);mats=new[]{f.GetComponent<Renderer>().sharedMaterial,pavement};surfaces++;}
            AssetDatabase.CreateAsset(replacement,folder+"/Road surface "+(++asset)+".asset");Swap(f,replacement,mats);
        }
        return surfaces;
    }
    static int Fences()
    {
        var wall=new Shape();var gate=new Shape();int sections=0;
        // Inland boundary beside the existing long roadside building. Short panels
        // step with the measured ground and leave a wide blue-gate entrance.
        for(float z=835;z<868;z+=.4f)
        {
            float x=-151.6f+(z-835)*.049f;var p=new Vector2(x,z);if(!Clear(p,.18f)||!Ground(p,out var y))continue;
            bool blue=z>=852.6f&&z<=859.8f;float h=blue?2.0f:1.8f;var col=blue?new Color(.16f,.31f,.44f):new Color(.55f,.54f,.485f);
            (blue?gate:wall).Box(V(x,y+h*.5f,z),V(.14f,h,.405f),col*(.91f+.08f*Mathf.PerlinNoise(z*2,7)));
            if(blue)for(int k=0;k<4;k++)gate.Box(V(x-.082f,y+1,z-.15f+k*.1f),V(.025f,1.98f,.020f),col*.80f);
            if(((int)((z-835)*10))%24<4)wall.Box(V(x,y+.99f,z),V(.25f,1.98f,.24f),new Color(.58f,.57f,.52f));
            occupied.Add(new Bounds(V(x,y+1,z),V(.45f,2.2f,.6f)));sections++;
        }
        Save("Weather-stained CHB boundary wall",wall,concrete,true);Save("Faded blue corrugated sheet gate and fence",gate,rough,true);
        var bamboo=new Shape();var a=new Vector2(-138.55f,809.5f);var b=new Vector2(-125.8f,795.9f);var d=(b-a).normalized;var outw=new Vector2(d.y,-d.x);float len=Vector2.Distance(a,b);int reeds=0;
        for(float t=.4f;t<len-.4f;t+=.085f)
        {if(t>6.2f&&t<7.7f)continue;var p=a+d*t+outw*.22f;if(!Clear(p,.045f)||!Ground(p,out var y))continue;float h=1.62f+.10f*Mathf.PerlinNoise(t*3,2);bamboo.Beam(V(p.x,y+.06f,p.y),V(p.x+.01f,y+h,p.y),.057f,new Color(.54f,.46f,.31f)*(.87f+.18f*Mathf.PerlinNoise(t*9,4)));reeds++;}
        Save("Uneven bamboo frontage beside corner home",bamboo,rough,true);return sections+reeds;
    }
    static int ShouldersAndPosts()
    {
        var dirt=new Shape();var rubble=new Shape();var weeds=new Shape();var posts=new Shape();int count=0;
        var rng=new System.Random(5314);
        for(float z=828;z<902;z+=1.2f)
        {
            // Follow the named coastal road, not the inland side-road crossing.
            var main=roads.Where(c=>c.name=="Felix Amante Drive").ToArray();var xs=new List<float>();for(float x=-169;x<-140;x+=.25f)if(main.Any(c=>Hit(c,new Vector2(x,z),out _)))xs.Add(x);if(xs.Count==0)continue;
            foreach(int side in new[]{-1,1})
            {
                float edge=side<0?xs.Min():xs.Max();
                for(float offset=.3f;offset<2.6f;offset+=.5f)
                {var p=new Vector2(edge+side*offset,z);var cs=new[]{p+new Vector2(-.23f,-.55f),p+new Vector2(.23f,-.55f),p+new Vector2(.23f,.55f),p+new Vector2(-.23f,.55f)};if(cs.Any(c=>!Clear(c,.06f)))continue;var vs=cs.Select(c=>{Ground(c,out var h);return V(c.x,h+.042f,c.y);}).ToArray();dirt.Q(vs[0],vs[3],vs[2],vs[1],new Color(.45f,.42f,.345f)*(.91f+.09f*Mathf.PerlinNoise(p.x,p.y)),Vector3.up);
                    if(offset>1.5f&&rng.NextDouble()<.18){Ground(p,out var y);rubble.Box(V(p.x,y+.07f,p.y),V(.19f,.13f,.12f),new Color(.48f,.46f,.40f),Quaternion.Euler(8,(float)rng.NextDouble()*180,13));}
                    if(side>0&&offset>1.6f&&rng.NextDouble()<.12){Ground(p,out var y);for(int k=0;k<5;k++){float angle=k*1.4f;var w=V(Mathf.Cos(angle)*.12f,0,Mathf.Sin(angle)*.12f);var c=V(p.x,y+.04f,p.y);weeds.Q(c-w,c+w,c+w*.2f+Vector3.up*.27f,c-w*.2f+Vector3.up*.27f,new Color(.25f,.34f,.16f),Vector3.up);}}
                }
            }
            if(((int)((z-828)/1.2f))%5==0){var p=new Vector2(xs.Min()-1.3f,z);if(Clear(p,.2f)&&Ground(p,out var y)){posts.Box(V(p.x,y+.59f,p.y),V(.16f,1.18f,.17f),new Color(.64f,.62f,.54f),Quaternion.Euler(0,0,1.5f));count++;}}
        }
        Save("Rough packed-earth shoulders following existing ground",dirt,rough);Save("Scattered shoulder rubble",rubble,rough);Save("Sparse low roadside weeds - no trees added",weeds,rough);Save("Spaced concrete posts on sea side",posts,concrete,true);return count;
    }
    static int Vehicles()
    {
        var source=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName).transform.Find("tricycle");if(!source)return 0;int added=0;
        foreach(var p in new[]{new Vector2(-149.4f,862.4f),new Vector2(-149.2f,867.3f)})
        {
            var go=UnityEngine.Object.Instantiate(source.GetChild(added%source.childCount).gameObject,root.transform);go.name="Parked corner tricycle "+(added+1);go.transform.localScale=Vector3.one;go.transform.rotation=Quaternion.Euler(0,added==0?5:-4,0);if(!Ground(p,out var y)){UnityEngine.Object.DestroyImmediate(go);continue;}go.transform.position=V(p.x,y+.045f,p.y);
            var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);bool safe=true;
            for(float x=b.min.x;x<=b.max.x;x+=.25f)for(float z=b.min.z;z<=b.max.z;z+=.25f)if(!Clear(new Vector2(x,z),.12f)||occupied.Any(o=>x>=o.min.x&&x<=o.max.x&&z>=o.min.z&&z<=o.max.z))safe=false;
            if(!safe){UnityEngine.Object.DestroyImmediate(go);continue;}occupied.Add(b);added++;
        }
        return added;
    }
    public static object ExtendAdjustableBoundary()
    {
        const string title="Baybayon corner-to-corner concrete wall - adjustable sections";
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(title))throw new Exception("Wrong scene or wall already exists");
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        ground=before.Select(t=>t.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.name.StartsWith("Measured terrain")).ToArray();
        roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh&&!c.name.Contains("markings")).ToArray();
        homes=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(h=>h["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray()).Where(p=>p.Max(v=>v.y)>825&&p.Min(v=>v.y)<1010).ToList();
        var main=roads.Where(c=>c.name=="Felix Amante Drive").ToArray();
        Func<float,int,float> edge=(z,side)=>{var xs=new List<float>();for(float x=-180;x<-125;x+=.20f)if(main.Any(c=>Hit(c,new Vector2(x,z),out _)))xs.Add(x);return xs.Count==0?float.NaN:side>0?xs.Max():xs.Min();};
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/BaybayonBoundary_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-boundary.unity",true))throw new Exception("Backup failed");
        folder="Assets/StormSim/Generated/BaybayonBoundary_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(title);Undo.RegisterCreatedObjectUndo(root,"Adjustable coastal roadside wall");asset=0;
        concrete=Mat("Aged block wall with damp staining",7);rough=Mat("Continuous rough dirt shoulder",5);
        var hidden=new List<GameObject>();var old=GameObject.Find(RootName);foreach(string name in new[]{"Weather-stained CHB boundary wall","Rough packed-earth shoulders following existing ground"}){var child=old.transform.Find(name);if(child)hidden.Add(child.gameObject);}
        var visibility=root.AddComponent<BanagoLocalDetailReview>();visibility.originals=hidden.ToArray();visibility.originalActive=hidden.Select(g=>g.activeSelf).ToArray();visibility.notes="Earlier short wall and tiled shoulders retained for undo. Blue gate and original sea-side posts stay visible.";visibility.Apply();
        int panels=0,skipped=0;float total=0;var posts=new Shape();var dirt=new Shape();var rubble=new Shape();
        for(float z=835;z<1000;z+=2.5f)
        {
            // Keep the photographed blue gate as the main opening.
            if(z<860&&z+2.5f>852.5f){skipped++;continue;}
            float e0=edge(z,1),e1=edge(z+2.5f,1);if(float.IsNaN(e0+e1)){skipped++;continue;}
            var a=new Vector2(e0+1.4f,z);var b=new Vector2(e1+1.4f,z+2.5f);bool clear=true;float low=100,high=-100;
            for(float t=0;t<=1.001f;t+=.10f){var p=Vector2.Lerp(a,b,t);if(!Clear(p,.22f)||!Ground(p,out var y)){clear=false;break;}low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
            if(!clear||high-low>.7f){skipped++;continue;}
            float length=Vector2.Distance(a,b),height=1.85f+high-low;var center=(a+b)*.5f;var rotation=Quaternion.LookRotation(V(-(b-a).y,0,(b-a).x));
            var panel=new Shape();panel.Box(V(0,height*.5f,0),V(length+.018f,height,.18f),new Color(.60f,.59f,.53f));panel.Box(V(-length*.5f,height*.5f+.05f,0),V(.26f,height+.1f,.27f),new Color(.63f,.62f,.56f));
            var g=Save("Wall section "+(++panels).ToString("000")+" - move or rotate individually",panel,concrete,true);g.transform.position=V(center.x,low-.07f,center.y);g.transform.rotation=rotation;total+=length;
        }
        // Sea side remains open; these quads follow existing measured land only.
        for(float z=828;z<1000;z+=1f)
        {
            float e0=edge(z,-1),e1=edge(z+1,-1);if(float.IsNaN(e0+e1))continue;
            for(float offset=.15f;offset<3.0f;offset+=.5f){var cs=new[]{new Vector2(e0-offset,z),new Vector2(e1-offset,z+1),new Vector2(e1-offset-.52f,z+1),new Vector2(e0-offset-.52f,z)};if(cs.Any(p=>!Clear(p,.015f)))continue;var vs=cs.Select(p=>{Ground(p,out var y);return V(p.x,y+.027f,p.y);}).ToArray();dirt.Q(vs[0],vs[1],vs[2],vs[3],Color.Lerp(new Color(.45f,.425f,.35f),new Color(.53f,.49f,.39f),Mathf.PerlinNoise(z*.45f,offset)),Vector3.up);}
            if(z>903&&((int)z)%6==0){var p=new Vector2(e0-1.35f,z);if(Clear(p,.2f)&&Ground(p,out var y))posts.Box(V(p.x,y+.65f,p.y),V(.17f,1.3f,.17f),new Color(.67f,.65f,.56f));}
            if(((int)z)%3==0){var p=new Vector2(e0-2.6f,z);if(Clear(p,.2f)&&Ground(p,out var y))rubble.Box(V(p.x,y+.09f,p.y),V(.34f,.18f,.26f),new Color(.50f,.48f,.41f),Quaternion.Euler(5,z*17,9));}
        }
        Save("Open sea-side concrete posts extension",posts,concrete,true);Save("Continuous rough shoreline shoulder - no tiles",dirt,rough);Save("Small rubble beside sea-side posts",rubble,rough);
        var report=new{folder,backup,adjustableWallSections=panels,wallLengthMetres=total,openingsAndObstructionsSkipped=skipped,wallHeightMetres=1.85f,inlandSideOnly=true,seaSideOpen=true,newTrees=0,originalObjectsDeleted=0};File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));EditorUtility.SetDirty(visibility);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return report;
    }
    public static object Build()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity"||GameObject.Find(RootName))throw new Exception("Wrong scene, play mode, or corner already applied");
        var before=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>new{t.position,t.localRotation,t.localScale,active=t.gameObject.activeSelf});
        ground=before.Keys.Select(t=>t.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.name.StartsWith("Measured terrain")).ToArray();
        roads=before.Keys.Select(t=>t.GetComponent<MeshCollider>()).Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.sharedMesh&&AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/StudyRoads_")&&!c.name.Contains("markings")).ToArray();
        homes=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(h=>h["footprint"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray()).Where(p=>p.Max(v=>v.x)> -200&&p.Min(v=>v.x)< -70&&p.Max(v=>v.y)>760&&p.Min(v=>v.y)<925).ToList();
        if(ground.Length==0||roads.Length==0)throw new Exception("Missing measured ground or road colliders");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");backup="RoadRepairBackup/BaybayonCorner_"+stamp;Directory.CreateDirectory(backup);if(!EditorSceneManager.SaveScene(scene,backup+"/MAIN_GAME.before-corner.unity",true))throw new Exception("Backup failed");
        folder="Assets/StormSim/Generated/BaybayonCorner_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Baybayon street reference");review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=backup;review.notes="Local street reference: unmarked worn road, blue corrugated boundary, CHB and bamboo frontage. No port, no trees, no original objects deleted. Disable this root to restore road appearance.";
        rough=Mat("Weathered corner details",5);pavement=Mat("Pale worn unmarked corner road",4);concrete=Mat("Unfinished CHB and stained concrete",3);asset=0;swaps.Clear();occupied.Clear();
        int surfaces=Roads(before.Keys.ToArray(),out var markings);int fenceParts=Fences();int posts=ShouldersAndPosts();int vehicles=Vehicles();review.swaps=swaps.ToArray();review.Apply();EditorUtility.SetDirty(review);
        var errors=new List<string>();foreach(var kv in before){var t=kv.Key;var old=kv.Value;if(!t){errors.Add("Missing original");continue;}if(t.position!=old.position||t.localRotation!=old.localRotation||t.localScale!=old.localScale||t.gameObject.activeSelf!=old.active)errors.Add("Changed original transform/visibility "+t.name);}
        int badVertices=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertices.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));
        var report=new{root=RootName,folder,backup,roadSurfaces=surfaces,localMarkingTrianglesHidden=markings,fenceParts,seaSidePosts=posts,parkedTricycles=vehicles,newTrees=0,originalObjectsDeleted=0,originalRoadVerticesUnchanged=swaps.All(s=>s.before.vertices.SequenceEqual(s.after.vertices)),existingObjectsChecked=before.Count,badVertices,preservationErrors=errors};
        File.WriteAllText(backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();if(errors.Count>0||badVertices>0)throw new Exception(JsonConvert.SerializeObject(report));return report;
    }
}
