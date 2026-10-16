using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Photo-guided area styles within retained mapped envelopes; never a new map or global rebuild.</summary>
public static class BanagoReferenceAreasBuilder
{
    public const string RootName="Banago reference areas - ponds shoreline and Felix Amante";
    public static string Folder,Backup,Status="Not started";
    static GameObject root;
    static BanagoReferenceAreasReview review;
    static readonly List<BanagoReferenceAreasReview.Swap> swaps=new List<BanagoReferenceAreasReview.Swap>();
    static readonly List<BanagoReferenceAreasReview.Prop> propSwaps=new List<BanagoReferenceAreasReview.Prop>();
    sealed class House
    {
        public string id,tile,name;public Vector2[] poly;public Vector2 center,roadPoint;public uint seed;
        public bool coast,highway;public float height,rise,floor,newHeight,newRise,lift;public int vertices;
        public float Y(float y){if(y<=floor)return y; if(y<=floor+height)return floor+lift+(y-floor)*newHeight/height;return floor+lift+newHeight+(y-floor-height)*newRise/Mathf.Max(.1f,rise);}
    }
    struct Tri {public Vector2 a,b,c;public Rect bounds;}
    sealed class Original {public Transform t;public Vector3 p,s;public Quaternion q;public bool active;public MeshFilter f;public Mesh mesh;public Renderer r;public Material[] materials;}
    static List<House> houses;static List<Tri> roads,highway;static List<Original> originals;
    static Dictionary<string,House[]> tiles;
    static Transform[] batches;static int next,assetIndex,pondCount,bankClumps,dryCount,fronts,stiltHomes;
    static Material bank,details,roofCoast,roofStreet,wallCoast,wallStreet,roadMat;
    static int nx,nz;static float[] heights;
    static bool baybayin;
    static uint Hash(string s){unchecked{uint x=2166136261;foreach(char c in s)x=(x^c)*16777619;return x;}}
    static Vector2 XZ(Vector3 v)=>new Vector2(v.x,v.z);
    static Vector3 V(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
    static float Edge(Vector2 p,Vector2[] poly)
    {float d=float.MaxValue;for(int i=0;i<poly.Length;i++){var a=poly[i];var ab=poly[(i+1)%poly.Length]-a;var q=a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.00001f,ab.sqrMagnitude));d=Mathf.Min(d,Vector2.Distance(p,q));}return d;}
    static bool Inside(Vector2 p,Vector2[] poly)
    {bool v=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)v=!v;return v;}
    static float Ground(Vector2 p)
    {float gx=Mathf.Clamp((p.x+277)/1202*(nx-1),0,nx-1.001f),gz=Mathf.Clamp((p.y-89)/1361*(nz-1),0,nz-1.001f);int x=(int)gx,z=(int)gz;float u=gx-x,v=gz-z;float a=heights[z*nx+x],b=heights[z*nx+x+1],c=heights[(z+1)*nx+x],d=heights[(z+1)*nx+x+1];return u+v<=1?a+u*(b-a)+v*(c-a):d+(1-u)*(c-d)+(1-v)*(b-d);}
    static List<Tri> RoadTris(IEnumerable<MeshFilter> filters)
    {var list=new List<Tri>();foreach(var f in filters){var m=f.sharedMesh;var v=m.vertices;var ix=m.triangles;for(int i=0;i<ix.Length;i+=3){var a=XZ(f.transform.TransformPoint(v[ix[i]]));var b=XZ(f.transform.TransformPoint(v[ix[i+1]]));var c=XZ(f.transform.TransformPoint(v[ix[i+2]]));if(Mathf.Abs((b.x-a.x)*(c.y-a.y)-(c.x-a.x)*(b.y-a.y))<.0001f)continue;list.Add(new Tri{a=a,b=b,c=c,bounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x),Mathf.Min(a.y,b.y,c.y),Mathf.Max(a.x,b.x,c.x),Mathf.Max(a.y,b.y,c.y))});}}return list;}
    static float RoadDistance(Vector2 p,List<Tri> ts,out Vector2 nearest)
    {float best=99999;nearest=p;foreach(var t in ts){if(p.x<t.bounds.xMin-best||p.x>t.bounds.xMax+best||p.y<t.bounds.yMin-best||p.y>t.bounds.yMax+best)continue;var a=new[]{t.a,t.b,t.c};if(Inside(p,a)){nearest=p;return 0;}for(int j=0;j<3;j++){var ab=a[(j+1)%3]-a[j];var q=a[j]+ab*Mathf.Clamp01(Vector2.Dot(p-a[j],ab)/Mathf.Max(.00001f,ab.sqrMagnitude));float d=Vector2.Distance(p,q);if(d<best){best=d;nearest=q;}}}return best;}
    static bool Clear(Vector2 p,House own,float margin=.2f)
    {if(RoadDistance(p,roads,out _) < margin)return false;foreach(var h in houses){if(h==own||Vector2.Distance(p,h.center)>90)continue;if(Inside(p,h.poly)||Edge(p,h.poly)<margin)return false;}return true;}
    static Material Mat(string name,int mode,Color color,float coastal=0)
    {var m=new Material(Shader.Find("StormSim/Banago Reference Areas")){name=name,enableInstancing=true};m.SetFloat("_Mode",mode);m.SetColor("_BaseColor",color);m.SetFloat("_Coastal",coastal);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static void Swap(MeshFilter f,Mesh m,Material[] mats)
    {var c=f.GetComponent<MeshCollider>();swaps.Add(new BanagoReferenceAreasReview.Swap{target=f,before=f.sharedMesh,after=m,materialsBefore=f.GetComponent<Renderer>().sharedMaterials,materialsAfter=mats,colliderBefore=c?c.sharedMesh:null});review.swaps=swaps.ToArray();EditorUtility.SetDirty(review);review.Apply();}
    sealed class Shape
    {
        public List<Vector3> vs=new List<Vector3>(),ns=new List<Vector3>();public List<Color> cs=new List<Color>();public List<Vector2> uv=new List<Vector2>();
        public void T(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 normal)
        {if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var q=b;b=c;c=q;}foreach(var p in new[]{a,b,c}){vs.Add(p);ns.Add(normal);cs.Add(color);uv.Add(new Vector2(p.x,p.y));}}
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col,Vector3 n){T(a,b,c,col,n);T(a,c,d,col,n);}
        public void Box(Vector3 c,Vector3 size,Color color,Quaternion q)
        {var v=new List<Vector3>();foreach(var s in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)})v.Add(c+q*Vector3.Scale(s,size*.5f));foreach(var face in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}}){var n=Vector3.Cross(v[face[1]]-v[face[0]],v[face[2]]-v[face[0]]).normalized;Q(v[face[0]],v[face[1]],v[face[2]],v[face[3]],color,n);}}
        public void Post(Vector3 a,Vector3 b,float width,Color col){Box((a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),col,Quaternion.FromToRotation(Vector3.up,(b-a).normalized));}
    }
    static void SaveShape(string name,Shape s,Material mat,bool collider=false)
    {if(s.vs.Count==0)return;if(s.vs.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception("Invalid new geometry "+name);var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(s.vs);m.SetNormals(s.ns);m.SetColors(s.cs);m.SetUVs(0,s.uv);m.SetTriangles(Enumerable.Range(0,s.vs.Count).ToArray(),0);m.RecalculateBounds();AssetDatabase.CreateAsset(m,Folder+"/Detail "+(++assetIndex)+".asset");var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)go.AddComponent<MeshCollider>().sharedMesh=m;}
    public static object Start(bool baybayinOnly=false)
    {
        baybayin=baybayinOnly;string activeName=baybayin?"Banago Baybayin - school-side waterfront reference":RootName;
        if(EditorApplication.isPlaying||GameObject.Find(activeName))throw new Exception("Exit Play mode / reference pass already exists.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Wrong scene.");
        var shader=Shader.Find("StormSim/Banago Reference Areas");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Reference shader not ready.");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");Backup="RoadRepairBackup/ReferenceAreas_"+stamp;Directory.CreateDirectory(Backup);
        if(!EditorSceneManager.SaveScene(scene,Backup+"/MAIN_GAME.before-reference-areas.unity",true))throw new Exception("Live scene backup failed.");
        originals=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>{var f=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();return new Original{t=t,p=t.position,s=t.localScale,q=t.localRotation,active=t.gameObject.activeSelf,f=f,mesh=f?f.sharedMesh:null,r=r,materials=r?r.sharedMaterials:null};}).ToList();
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];heights=e["heights"].Values<float>().ToArray();
        var roadFilters=originals.Where(o=>o.f&&o.f.sharedMesh&&o.active&&o.t.gameObject.activeInHierarchy&&AssetDatabase.GetAssetPath(o.f.sharedMesh).Contains("/StudyRoads_")&&!o.t.name.Contains("markings")).Select(o=>o.f).ToArray();roads=RoadTris(roadFilters);highway=RoadTris(roadFilters.Where(f=>f.name=="Felix Amante Drive"));if(highway.Count==0)throw new Exception("Named highway missing.");
        houses=JArray.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-records.json")).Select(d=>{var p=d["footprint"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();if(p[0]==p[p.Length-1])p=p.Take(p.Length-1).ToArray();return new House{id=(string)d["id"],name=(string)d["name"],tile=(string)d["tile"],poly=p,center=new Vector2(p.Average(v=>v.x),p.Average(v=>v.y)),height=(float)d["height"],rise=(float)d["roofRise"],coast=(bool)d["coastal"],seed=Hash((string)d["id"])};}).ToList();
        foreach(var h in houses){h.coast&=baybayin?(h.center.y>650&&h.center.y<835):(h.center.y>=90&&h.center.y<=650);float distance=RoadDistance(h.center,highway,out var nearest);h.roadPoint=nearest;h.highway=!baybayin&&distance<24&&h.center.y>=90&&h.center.y<=650;h.newHeight=h.coast?Mathf.Min(h.height,2.75f+(h.seed%5)*.1f):h.height;h.newRise=h.coast?Mathf.Min(h.rise,.55f+(h.seed%7)*.09f):h.highway?Mathf.Min(h.rise,.85f+(h.seed%9)*.12f):h.rise;h.lift=h.coast?.42f:0;}
        tiles=houses.GroupBy(h=>h.tile).ToDictionary(g=>g.Key,g=>g.ToArray());var hr=GameObject.Find("Banago Houses - full detail redesign");batches=hr.transform.Cast<Transform>().Where(t=>tiles[t.name.Replace("Neighbourhood ","")].Any(h=>h.coast||h.highway)).ToArray();
        Folder="Assets/StormSim/Generated/BanagoReferenceAreas_"+stamp;Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        root=new GameObject(activeName);Undo.RegisterCreatedObjectUndo(root,"Reference area redesign");review=root.AddComponent<BanagoReferenceAreasReview>();review.backupPath=Backup;review.notes="Photo-guided representative reconstruction, not a measured building-by-building replica. Disable this root to restore pre-pass meshes/materials and rooftop prop positions. Existing original objects, terrain, roads and water boundaries retained. "+(baybayin?"Baybayin school-side coastal houses: local z 650–835.":"Coastal and highway house pass limited to local z 90–650.")+" No tree instances added.";
        swaps.Clear();propSwaps.Clear();next=assetIndex=pondCount=bankClumps=dryCount=fronts=stiltHomes=0;
        bank=Mat("Vegetated earthen pond banks",1,Color.white);details=Mat("Weathered details",5,Color.white);roofCoast=Mat("Coastal patched GI and timber",2,Color.white,1);roofStreet=Mat("Highway patched blue silver and rust GI",2,Color.white);wallCoast=Mat("Coastal weathered timber",3,Color.white,1);wallStreet=Mat("Roadside unfinished CHB",3,Color.white);roadMat=Mat("Felix Amante pale worn pavement",4,Color.white);
        if(!baybayin){BuildPonds();BuildDryBasins();RoadSurfaces();}AssetDatabase.SaveAssets();Status="Preparing "+batches.Length+" house batches";
        return new{backup=Backup,folder=Folder,ponds=pondCount,bankClumps,dryBasins=dryCount,coastalHomes=houses.Count(h=>h.coast),roadsideHomes=houses.Count(h=>h.highway),batches=batches.Length};
    }
    static void BuildPonds()
    {
        var inland=GameObject.Find("Banago Inland - ponds farms and vacant land");var pd=JObject.Parse(File.ReadAllText("RoadRepairBackup/banago-inland-data.json"));var grass=new Shape();
        foreach(var p in pd["ponds"])
        {
            int id=(int)p["index"];string name="Pond "+id.ToString("00");var f=inland.GetComponentsInChildren<MeshFilter>().First(x=>x.name==name+" - still water");float level=(float)p["level"];
            var palette=new[]{new Color(.17f,.255f,.235f),new Color(.21f,.285f,.25f),new Color(.235f,.28f,.245f),new Color(.15f,.225f,.215f),new Color(.29f,.32f,.275f)};
            var mat=Mat(name+" muted pond water",0,palette[(id*7)%palette.Length]);mat.SetFloat("_Seed",id*3.7f);mat.SetFloat("_Dryness",id%7==3?.52f:id%5==0?.26f:.045f);Swap(f,null,new[]{mat});pondCount++;
            var bf=inland.GetComponentsInChildren<MeshFilter>().First(x=>x.name==name+" - fitted muddy banks");Swap(bf,null,new[]{bank});var vs=bf.sharedMesh.vertices;var ids=bf.sharedMesh.triangles;var rng=new System.Random(id*337);
            for(int i=0;i<ids.Length;i+=3)
            {var a=bf.transform.TransformPoint(vs[ids[i]]);var b=bf.transform.TransformPoint(vs[ids[i+1]]);var c=bf.transform.TransformPoint(vs[ids[i+2]]);var q=a*.62f+b*.19f+c*.19f;if(q.y<level+.045f||rng.NextDouble()<.22)continue;float hh=.30f+(float)rng.NextDouble()*.37f;var col=Color.Lerp(new Color(.20f,.32f,.12f),new Color(.32f,.41f,.16f),(float)rng.NextDouble());for(int k=0;k<4;k++){float angle=k*Mathf.PI/4;var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.31f;grass.Q(q-side,q+side,q+side*.42f+Vector3.up*hh,q-side*.42f+Vector3.up*hh,col,Vector3.up);}bankClumps++;}
        }
        SaveShape("Low reeds and grasses on existing pond banks",grass,details);
    }
    static void BuildDryBasins()
    {
        string script=File.ReadAllText("RoadRepairBackup/prepare_banago_inland.py");var match=System.Text.RegularExpressions.Regex.Match(script,@"dry=\[([\s\S]*?)\n\]");if(!match.Success)throw new Exception("Existing basin trace missing");
        string json="["+match.Groups[1].Value.Replace('(','[').Replace(')',']').Trim().TrimEnd(',')+"]";var polys=JArray.Parse(json);int id=0;
        foreach(var token in polys)
        {
            var poly=token.Select(p=>new Vector2(-385.48376f+(float)p[0]/1500*1420,1479.08044f-(float)p[1]/1500*1420)).ToArray();var sh=new Shape();id++;
            for(float x=poly.Min(p=>p.x);x<poly.Max(p=>p.x);x+=2.5f)for(float z=poly.Min(p=>p.y);z<poly.Max(p=>p.y);z+=2.5f)
            {var q=new[]{new Vector2(x,z),new Vector2(x+2.5f,z),new Vector2(x+2.5f,z+2.5f),new Vector2(x,z+2.5f)};if(q.Any(p=>!Inside(p,poly)||Edge(p,poly)<1.25f||!Clear(p,null,.8f)))continue;float n=Mathf.PerlinNoise(x*.15f+id*2,z*.15f);float edge=Edge(new Vector2(x+1.25f,z+1.25f),poly);float veg=Mathf.Clamp01((6-edge)/6)*.65f+(id%4==0?.40f:.10f)*Mathf.SmoothStep(0,1,n);var col=Color.Lerp(new Color(.43f,.435f,.375f),new Color(.24f,.34f,.155f),veg);col*=.88f+.19f*n;sh.Q(V(q[0],Ground(q[0])+.022f),V(q[1],Ground(q[1])+.022f),V(q[2],Ground(q[2])+.022f),V(q[3],Ground(q[3])+.022f),col,Vector3.up);}
            SaveShape("Drained basin "+id+" - damp soil and low greenery",sh,details);dryCount++;
        }
    }
    static void RoadSurfaces()
    {
        foreach(var o in originals.Where(o=>o.f&&o.r&&o.t.gameObject.activeInHierarchy))
        {
            bool main=o.t.name=="Felix Amante Drive";bool marking=o.t.parent&&o.t.parent.name=="Felix Amante Drive"&&o.t.name.Contains("markings");
            bool junction=o.t.name.StartsWith("Junction")&&AssetDatabase.GetAssetPath(o.mesh).Contains("/StudyRoads_")&&o.r.bounds.center.z<650&&o.r.bounds.center.z>90&&RoadDistance(XZ(o.r.bounds.center),highway,out _) < 5;
            if((main||marking)&&o.r.bounds.center.z>650)continue;if(main||marking||junction)Swap(o.f,null,new[]{roadMat});
        }
    }
    static House Owner(Vector2 p,House[] hh,float tolerance=.055f)
    {var inside=hh.FirstOrDefault(h=>Inside(p,h.poly));if(inside!=null)return inside;House result=null;float best=tolerance;foreach(var h in hh){float d=Edge(p,h.poly);if(d<best){best=d;result=h;}}return result;}
    public static string Step(int count=3)
    {
        if(!root||houses==null)throw new Exception("Start pass first / domain reloaded.");
        for(int pass=0;pass<count&&next<batches.Length;pass++,next++)ProcessBatch(batches[next]);
        Status="Prepared "+next+" / "+batches.Length+" house batches";return Status;
    }
    static void ProcessBatch(Transform batch)
    {
        var hh=tiles[batch.name.Replace("Neighbourhood ","")];var plaster=batch.Find("plaster").GetComponent<MeshFilter>();var vv=plaster.sharedMesh.vertices;var ix=plaster.sharedMesh.triangles;
        foreach(var h in hh){h.floor=Ground(h.center)+.06f;if(h.coast){float top=-999;for(int i=0;i<ix.Length;i+=3){var p=(vv[ix[i]]+vv[ix[i+1]]+vv[ix[i+2]])/3;if(Owner(XZ(p),hh)==h)for(int j=0;j<3;j++)top=Mathf.Max(top,vv[ix[i+j]].y);}if(top>-998)h.floor=top-.20f;}else{var m=batch.Find("metal").GetComponent<MeshFilter>().sharedMesh;float low=999;var v=m.vertices;var ids=m.triangles;for(int i=0;i<ids.Length;i+=3){if(Owner(XZ((v[ids[i]]+v[ids[i+1]]+v[ids[i+2]])/3),hh)==h)for(int j=0;j<3;j++)low=Mathf.Min(low,v[ids[i+j]].y);}if(low<998)h.floor=low-h.height;}}
        var decks=new Shape();var supports=new Shape();
        foreach(var f in batch.GetComponentsInChildren<MeshFilter>())
        {
            var before=f.sharedMesh;if(before.vertexCount!=before.triangles.Length)throw new Exception("Unexpected shared mesh topology "+batch.name);
            int oldCount=before.subMeshCount;var oldGroup=new int[before.vertexCount];for(int s=0;s<oldCount;s++)foreach(int k in before.GetTriangles(s))oldGroup[k]=s;
            var v=before.vertices;var n=before.normals;var ids=before.triangles;var groups=Enumerable.Range(0,oldCount+2).Select(_=>new List<int>()).ToArray();int changed=0;
            for(int i=0;i<ids.Length;i+=3)
            {
                var mid=(v[ids[i]]+v[ids[i+1]]+v[ids[i+2]])/3;var h=Owner(XZ(mid),hh);int group=h!=null&&(h.coast||h.highway)?oldCount+(h.coast?0:1):oldGroup[ids[i]];
                for(int j=0;j<3;j++)groups[group].Add(ids[i+j]);if(group<oldCount)continue;changed++;
                if(h.coast&&f.name=="metal"&&n[ids[i]].y>.75f){decks.T(V(XZ(v[ids[i]]),h.floor+h.lift+.01f),V(XZ(v[ids[i+1]]),h.floor+h.lift+.01f),V(XZ(v[ids[i+2]]),h.floor+h.lift+.01f),new Color(.34f,.29f,.22f),Vector3.up);}
                for(int j=0;j<3;j++){int k=ids[i+j];if(h.coast&&f.name=="plaster")v[k].y=Mathf.Min(v[k].y,h.floor+.11f);else v[k].y=h.Y(v[k].y);h.vertices++;}
            }
            if(changed==0)continue;var after=UnityEngine.Object.Instantiate(before);after.name=before.name+" reference area";after.vertices=v;after.subMeshCount=groups.Length;for(int g=0;g<groups.Length;g++)after.SetTriangles(groups[g],g);after.RecalculateNormals();after.RecalculateBounds();AssetDatabase.CreateAsset(after,Folder+"/House mesh "+(++assetIndex)+".asset");
            var a=f.name=="metal"?roofCoast:f.name=="plaster"?wallCoast:details;var b=f.name=="metal"?roofStreet:f.name=="plaster"?wallStreet:details;Swap(f,after,f.GetComponent<Renderer>().sharedMaterials.Concat(new[]{a,b}).ToArray());
        }
        foreach(var h in hh.Where(h=>h.coast))
        {
            for(int i=0;i<h.poly.Length;i++){var a=h.poly[i];var b=h.poly[(i+1)%h.poly.Length];float l=Vector2.Distance(a,b);int count=Mathf.Max(1,Mathf.CeilToInt(l/1.8f));for(int k=0;k<count;k++){var p=Vector2.Lerp(a,b,(k+.25f)/count);var inward=(h.center-p).normalized;var q=p+inward*.14f;supports.Post(V(q,Ground(q)-.02f),V(q,h.floor+h.lift+.09f),.10f,new Color(.28f,.245f,.18f));}}
            stiltHomes++;
            if(baybayin)BuildWaterfrontPorch(h);
        }
        SaveShape(batch.name+" raised timber floors",decks,details,true);SaveShape(batch.name+" short timber shoreline supports",supports,details);
        foreach(var h in hh.Where(h=>h.highway&&!h.coast))BuildFront(h);
        foreach(var h in hh.Where(h=>h.coast||h.highway))
        {var groups=GameObject.Find(BanagoLivedInBuilder.RootName).transform;foreach(string name in new[]{"Ribbed PVC rooftop tanks","Rooftop satellite dishes"}){var g=groups.Find(name);if(!g)continue;foreach(Transform t in g){if(!Inside(XZ(t.position),h.poly))continue;var old=t.position;var pos=old;pos.y=h.Y(pos.y);propSwaps.Add(new BanagoReferenceAreasReview.Prop{target=t,before=old,after=pos});}}}
        review.props=propSwaps.ToArray();review.Apply();EditorUtility.SetDirty(review);
    }
    static void BuildFront(House h)
    {
        if(fronts>=64||h.height<2.6f)return;Vector2 a=default,b=default,outward=default;float best=-1;
        for(int i=0;i<h.poly.Length;i++){var u=h.poly[i];var v=h.poly[(i+1)%h.poly.Length];var d=(v-u).normalized;var normal=new Vector2(d.y,-d.x);if(Inside((u+v)*.5f+normal*.15f,h.poly))normal=-normal;float score=Vector2.Dot(normal,(h.roadPoint-h.center).normalized)*Mathf.Min(8,Vector2.Distance(u,v));if(score>best){best=score;a=u;b=v;outward=normal;}}
        float len=Vector2.Distance(a,b);if(best<1.2f||len<3)return;var along=(b-a).normalized;float width=Mathf.Min(5.8f,len-.65f);var center=(a+b)*.5f;float depth=1.2f;
        var corners=new[]{center-along*width*.5f-outward*.12f,center+along*width*.5f-outward*.12f,center+along*width*.5f+outward*depth,center-along*width*.5f+outward*depth};
        if(corners.Any(p=>!Clear(p,h,.45f)))return;
        // Sample the full rectangle, including narrow side lanes, before adding any frontage.
        for(float u=-width*.5f;u<=width*.5f;u+=.18f)for(float d=0;d<=depth;d+=.18f)if(!Clear(center+along*u+outward*d,h,.35f))return;
        float floor=h.floor;var top=new Shape();float back=floor+2.6f,front=floor+2.37f;var c=new Color(.25f,.42f,.53f);
        top.Q(V(corners[0],back),V(corners[1],back),V(corners[2],front),V(corners[3],front),c,Vector3.up);SaveShape("Roadside tin awning - "+h.name,top,roofStreet);
        var detail=new Shape();var faceNormal=new Vector3(outward.x,0,outward.y);var wood=new Color(.32f,.28f,.22f);
        detail.Post(V(corners[2],Ground(corners[2])),V(corners[2],front),.065f,wood);detail.Post(V(corners[3],Ground(corners[3])),V(corners[3],front),.065f,wood);
        var left=center-along*width*.48f-outward*.08f;var right=center+along*width*.48f-outward*.08f;var color=h.seed%3==0?new Color(.44f,.20f,.16f):h.seed%3==1?new Color(.24f,.35f,.30f):new Color(.41f,.46f,.44f);
        // A half-width shutter leaves the existing entry usable; no full-width wall covers the doorway.
        var edge=Vector2.Lerp(left,right,.37f);detail.Q(V(left,floor+.13f),V(edge,floor+.13f),V(edge,floor+2.12f),V(left,floor+2.12f),color,faceNormal);
        for(float y=.2f;y<2.1f;y+=.11f)detail.Post(V(left+outward*.014f,floor+y),V(edge+outward*.014f,floor+y),.015f,color*.66f);
        detail.Q(V(left+outward*.03f,floor+2.12f),V(right+outward*.03f,floor+2.12f),V(right+outward*.03f,floor+2.46f),V(left+outward*.03f,floor+2.46f),h.seed%2==0?new Color(.67f,.58f,.32f):new Color(.54f,.26f,.22f),faceNormal);
        SaveShape("Worn shutter and sign band - "+h.name,detail,details);
        var textGo=new GameObject("Sari-sari sign - "+h.name);textGo.transform.SetParent(root.transform,false);textGo.transform.position=V(center+outward*.055f,floor+2.28f);textGo.transform.rotation=Quaternion.LookRotation(faceNormal,Vector3.up);var tm=textGo.AddComponent<TextMesh>();tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");tm.text=h.seed%3==0?"SARI-SARI STORE":h.seed%3==1?"MINI GROCERY":"MERYENDA";tm.fontSize=48;tm.characterSize=.13f;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.82f,.80f,.67f);textGo.GetComponent<Renderer>().sharedMaterial=tm.font.material;fronts++;
    }
    static void BuildWaterfrontPorch(House h)
    {
        Vector2 a=default,b=default,outward=default;float best=0;
        for(int i=0;i<h.poly.Length;i++){var u=h.poly[i];var v=h.poly[(i+1)%h.poly.Length];var along=(v-u).normalized;var normal=new Vector2(along.y,-along.x);if(Inside((u+v)*.5f+normal*.1f,h.poly))normal=-normal;float score=Mathf.Max(0,-normal.x)*Mathf.Min(5,Vector2.Distance(u,v));if(score>best){best=score;a=u;b=v;outward=normal;}}
        if(best<1.8f)return;float width=Mathf.Min(4.5f,Vector2.Distance(a,b)-.55f),depth=1.45f;var d=(b-a).normalized;var center=(a+b)*.5f;
        for(float x=-width*.5f;x<=width*.5f;x+=.16f)for(float z=.08f;z<=depth;z+=.16f){var p=center+d*x+outward*z;if(!Clear(p,h,.28f)||Ground(p)>h.floor+h.lift-.1f)return;}
        var s=new Shape();var roof=new Shape();float floor=h.floor+h.lift;var left=center-d*width*.5f;var right=center+d*width*.5f;var lf=left+outward*depth;var rf=right+outward*depth;var wood=new Color(.39f,.335f,.25f);
        for(float x=-width*.5f;x<width*.5f;x+=.18f){float w=Mathf.Min(.166f,width*.5f-x);var p=center+d*(x+w*.5f)+outward*depth*.5f;s.Box(V(p,floor-.035f),new Vector3(w,.07f,depth),wood*(.85f+.2f*Mathf.PerlinNoise(x,h.seed%97)),Quaternion.LookRotation(V(outward,0),Vector3.up));}
        foreach(var p in new[]{left,right,lf,rf})s.Post(V(p,Ground(p)-.03f),V(p,floor+2.15f),.065f,wood*.76f);
        s.Post(V(lf,floor+.85f),V(lf+d*width*.3f,floor+.85f),.045f,wood);s.Post(V(rf-d*width*.3f,floor+.85f),V(rf,floor+.85f),.045f,wood);
        roof.Q(V(left,floor+2.33f),V(right,floor+2.33f),V(rf,floor+2.12f),V(lf,floor+2.12f),new Color(.48f,.41f,.30f),Vector3.up);
        SaveShape("Baybayin raised timber porch - "+h.name,s,details);SaveShape("Baybayin lean-to GI roof - "+h.name,roof,roofCoast);
    }
    public static object Finish()
    {
        if(next<batches.Length)throw new Exception("House batches remain.");
        var errors=new List<string>();var allowed=new HashSet<MeshFilter>(swaps.Select(s=>s.target));var moved=new HashSet<Transform>(propSwaps.Select(p=>p.target));
        foreach(var o in originals){if(!o.t){errors.Add("Missing original");continue;}bool movedFamily=moved.Any(t=>o.t==t||o.t.IsChildOf(t));if(o.t.gameObject.activeSelf!=o.active||o.t.localScale!=o.s||o.t.localRotation!=o.q||(!movedFamily&&o.t.position!=o.p))errors.Add("Transform/active changed: "+o.t.name);if(o.f&&!allowed.Contains(o.f)&&o.f.sharedMesh!=o.mesh)errors.Add("Unrelated mesh: "+o.t.name);if(o.r&&!allowed.Contains(o.f)&&!o.r.sharedMaterials.SequenceEqual(o.materials))errors.Add("Unrelated material: "+o.t.name);}
        int bad=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertices.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));
        foreach(var s in swaps.Where(s=>s.after)){if(s.before.vertexCount!=s.after.vertexCount)errors.Add("Topology changed "+s.target.name);var a=s.before.vertices;var b=s.after.vertices;for(int i=0;i<a.Length;i++)if(a[i].x!=b[i].x||a[i].z!=b[i].z){errors.Add("House footprint moved "+s.target.name);break;}bad+=b.Count(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z));}
        var report=new{root=root.name,backup=Backup,folder=Folder,ponds=pondCount,lowBankGrassClumps=bankClumps,dryBasins=dryCount,coastalHomes=houses.Count(h=>h.coast&&h.vertices>0),highwayHomes=houses.Count(h=>h.highway&&h.vertices>0),shopFronts=fronts,stiltHomes,rooftopPropsAdjusted=propSwaps.Count,existingObjectsChecked=originals.Count,newTrees=0,badVertices=bad,preservationErrors=errors};
        File.WriteAllText(Backup+"/validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));File.WriteAllText(Backup+"/house-adjustments.json",JsonConvert.SerializeObject(houses.Where(h=>h.coast||h.highway).Select(h=>new{h.id,h.name,h.tile,h.floor,h.height,h.newHeight,h.rise,h.newRise,h.lift,h.coast,h.highway,h.vertices}),Formatting.Indented));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);SceneView.RepaintAll();Status=errors.Count==0&&bad==0?"Ready for visual review":"Validation needs attention";if(errors.Count>0||bad>0)throw new Exception(JsonConvert.SerializeObject(report));return report;
    }
}
