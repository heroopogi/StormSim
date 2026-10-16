using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using StormSim.Sibucao;
using StormSim.Preparedness;
using Object=UnityEngine.Object;

public static class CoastalExistingHomeBuilder
{
    class Fit { public string name; public Vector3 center; public Quaternion rotation; public float width,depth; }
    struct Vertex { public Vector3 p,n; public Vector2 uv; public Color c; public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)}; }
    class Cut { public Matrix4x4 inverse; public Vector3 half; public Bounds bounds; }
    static IEnumerable<T> All<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    static Fit Measure(MeshFilter source,bool preview)
    {
        var vs=source.sharedMesh.vertices;
        var points=preview?vs.Where(v=>Mathf.Abs(v.y-vs.Min(q=>q.y))<.01f).Select(v=>source.transform.TransformPoint(v)).ToArray():new[]{vs[0],vs[6],vs[12],vs[18]}.Select(v=>source.transform.TransformPoint(v)).ToArray();
        var ps=points.Select(v=>new Vector2(v.x,v.z)).Distinct().ToArray();
        float best=float.MaxValue;var fit=new Fit{name=source.name};
        foreach(var a in ps)foreach(var b in ps)
        {
            if(a==b)continue;var x=(b-a).normalized;var z=new Vector2(-x.y,x.x);
            float xmin=ps.Min(p=>Vector2.Dot(p,x)),xmax=ps.Max(p=>Vector2.Dot(p,x)),zmin=ps.Min(p=>Vector2.Dot(p,z)),zmax=ps.Max(p=>Vector2.Dot(p,z));
            float area=(xmax-xmin)*(zmax-zmin);if(area>=best-.001f)continue;best=area;
            var center=x*((xmin+xmax)*.5f)+z*((zmin+zmax)*.5f);
            fit.center=new Vector3(center.x,source.transform.position.y+.08f,center.y);fit.rotation=Quaternion.LookRotation(new Vector3(z.x,0,z.y));fit.width=xmax-xmin;fit.depth=zmax-zmin;
        }
        // Put the longer edge across the room, retaining the source footprint.
        if(fit.width<fit.depth){float w=fit.width;fit.width=fit.depth;fit.depth=w;fit.rotation*=Quaternion.Euler(0,90,0);}
        return fit;
    }
    static bool Shell(string n)=>n=="Raised concrete foundation"||n.Contains("wall")&& !n.Contains("calendar")&&!n.Contains("cross") || n.StartsWith("Window ")&&n!="Window curtain pivot"||n=="Ceiling"||n.Contains("ceiling joist")||n.Contains("corrugated metal roof")||n=="Roof corrugation"||n.StartsWith("Front step")||n=="Walkable yard approach"||n=="House number"||n=="Door lintel"||n=="Exposed hollow-block course"||n=="Timber corner post"||n=="Rain gutter"||n=="Downpipe"||n=="Pipe wall clamp";
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit play mode first");
        var scene=SceneManager.GetActiveScene();var game=All<SibucaoSession>(scene).Single();var profile=game.GetComponent<CoastalAreaProfile>();if(!profile)throw new Exception("Coastal scene required");
        var folder="Assets/StormSim/Areas/"+profile.areaName+"/ExistingHomes";
        if(game.homes[0].transform.Find("Existing footprint binding"))throw new Exception("Already fitted");
        Directory.CreateDirectory("Reports/CoastalAreas/Backups");EditorSceneManager.SaveScene(scene,"Reports/CoastalAreas/Backups/"+profile.areaName+"-before-existing-homes.unity",true);
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var names=profile.areaName=="Riverside"?new[]{"House 0160","House 0134","House 0074","House 0196"}:new[]{"Building 1802","Building 1804","Building 1818","Building 1782"};
        var filters=All<MeshFilter>(scene).ToArray();var fits=names.Select(n=>Measure(filters.Single(f=>f.name==n),profile.areaName=="Riverside")).ToArray();
        var bridge=game.GetComponent<SibucaoPreparationBridge>();var cuts=new List<Cut>();var rows=new List<object>();
        var mapRoot=GameObject.Find("CesiumGeoreference").transform;
        var approachBounds=filters.Where(f=>f.gameObject.activeInHierarchy&&f.transform.IsChildOf(mapRoot)).Select(f=>f.GetComponent<Renderer>()).Where(r=>r&&r.bounds.size.x<40&&r.bounds.size.z<40).Select(r=>r.bounds).ToArray();
        var oldMatrices=game.homes.Select(h=>h.transform.worldToLocalMatrix).ToArray();var oldRotations=game.homes.Select(h=>h.transform.rotation).ToArray();var oldHeights=game.homes.Select(h=>h.transform.position.y).ToArray();
        for(int i=0;i<4;i++)
        {
            var home=game.homes[i];var fit=fits[i];
            // Choose the face with the clearest original approach, independently of map orientation.
            float best=float.MinValue;Quaternion rotation=fit.rotation;
            foreach(int side in new[]{0,2})
            {
                var q=fit.rotation*Quaternion.Euler(0,side*90,0);var door=fit.center+q*new Vector3(fit.width*.3125f,1,-fit.depth*.5f-.9f);
                float score=0;foreach(var b in approachBounds){if(b.Contains(door))score-=100;else score+=Mathf.Min(2,Vector3.Distance(b.ClosestPoint(door),door))*.001f;}
                if(score>best){best=score;rotation=q;}
            }
            home.transform.SetPositionAndRotation(fit.center,rotation);home.transform.localScale=new Vector3((fit.width-.16f)/8f, .92f,(fit.depth-.16f)/7f);
            foreach(var t in home.GetComponentsInChildren<Transform>(true))if(t!=home.transform&&Shell(t.name))t.gameObject.SetActive(false);
            // The operational door stays at the original building's exterior face.
            var hinge=home.doorHinge;var hp=hinge.localPosition;hp.z=-fit.depth*.5f/home.transform.localScale.z;hinge.localPosition=hp;
            var marker=new GameObject("Existing footprint binding");marker.transform.SetParent(home.transform,false);
            home.locationName=profile.areaName+" — "+fit.name;
            home.initialWaterHeight+=fit.center.y-oldHeights[i];home.finalWaterHeight+=fit.center.y-oldHeights[i];
            var center=home.transform.TransformPoint(new Vector3(2.5f,1.25f,hp.z));var half=new Vector3(home.transform.localScale.x*.74f,1.27f,.8f);
            cuts.Add(new Cut{inverse=Matrix4x4.TRS(center,rotation,Vector3.one).inverse,half=half,bounds=new Bounds(center,new Vector3(half.x*2+1.6f,half.y*2,half.x*2+1.6f))});
            rows.Add(new{home=home.name,existingBuilding=fit.name,width=fit.width,depth=fit.depth,center=fit.center.ToString()});
        }
        // Keep all interaction props in the selected home's local frame, including rotated homes.
        var prep=bridge.preparation.transform;var first=game.homes[0].transform;
        var playerLocal=oldMatrices[0].MultiplyPoint3x4(bridge.preparation.player.transform.position);
        prep.SetPositionAndRotation(first.position,first.rotation);prep.localScale=first.lossyScale;
        bridge.preparation.player.transform.SetPositionAndRotation(first.TransformPoint(playerLocal),first.rotation);
        bridge.firstHome=game.homes[0];bridge.preparation.homeStart=game.homes[0].standingPoint;
        // These residents belong at the other actual homes, not at translated Sibucao coordinates.
        foreach(Transform t in bridge.content.transform)
            for(int i=1;i<4;i++)if(t.name.StartsWith("House "+(i+1)+" resident"))t.SetPositionAndRotation(game.homes[i].transform.TransformPoint(new Vector3(t.name.EndsWith("1")?1:2.7f,0,-4.7f)),game.homes[i].transform.rotation);
        foreach(var st in game.physicalDecisions.stations.Where(st=>st&&st.departureEncounter))
        {
            int i=Enumerable.Range(0,4).FirstOrDefault(n=>(st.homeMask&(1<<n))!=0);st.transform.SetPositionAndRotation(game.homes[i].transform.TransformPoint(oldMatrices[i].MultiplyPoint3x4(st.transform.position)),game.homes[i].transform.rotation*Quaternion.Inverse(oldRotations[i])*st.transform.rotation);
        }
        foreach(var passage in All<SibucaoStormPassage>(scene))
        {int i=Mathf.Clamp(passage.home,0,3);passage.transform.SetPositionAndRotation(game.homes[i].transform.TransformPoint(oldMatrices[i].MultiplyPoint3x4(passage.transform.position)),game.homes[i].transform.rotation*Quaternion.Inverse(oldRotations[i])*passage.transform.rotation);}
        var map=GameObject.Find("CesiumGeoreference");int count=0;
        foreach(var f in filters.Where(f=>f&&f.gameObject.activeInHierarchy&&f.transform.IsChildOf(map.transform)&&MapHouse(f.transform)))
        {var r=f.GetComponent<Renderer>();if(!r||!cuts.Any(c=>c.bounds.Intersects(r.bounds))||!f.sharedMesh||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(f.sharedMesh)))continue;
         var relevant=cuts.Where(c=>c.bounds.Intersects(r.bounds)).ToArray();if(CutMesh(f,relevant,folder+"/Opened exterior "+count+".asset"))count++;}
        RemoveOldFloors(scene,folder);
        bridge.hiddenDuringPreparation=bridge.hiddenDuringPreparation.Where(g=>g).ToArray();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{area=profile.areaName,homes=rows,openedMeshes=count},Newtonsoft.Json.Formatting.Indented);File.WriteAllText("Reports/CoastalAreas/"+profile.areaName+"-existing-homes.json",report);return report;
    }
    public static string RepairFloors()
    {
        var scene=SceneManager.GetActiveScene();var profile=All<CoastalAreaProfile>(scene).Single();
        int changed=RemoveOldFloors(scene,"Assets/StormSim/Areas/"+profile.areaName+"/ExistingHomes");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Replaced overlapping map floor portions: "+changed;
    }
    static int RemoveOldFloors(Scene scene,string folder)
    {
        var game=All<SibucaoSession>(scene).Single();var cuts=game.homes.Select(h=>{var half=new Vector3(h.transform.lossyScale.x*4+.15f,2,h.transform.lossyScale.z*3.5f+.15f);var center=h.transform.position;return new Cut{half=half,inverse=Matrix4x4.TRS(center,h.transform.rotation,Vector3.one).inverse,bounds=new Bounds(center,new Vector3(half.magnitude*2,4,half.magnitude*2))};}).ToArray();
        int count=0;foreach(var f in All<MeshFilter>(scene).Where(f=>f.gameObject.activeInHierarchy&&f.name.Contains("raised timber floors")))
        {var r=f.GetComponent<Renderer>();if(!r||!cuts.Any(c=>c.bounds.Intersects(r.bounds)))continue;if(CutMesh(f,cuts,AssetDatabase.GenerateUniqueAssetPath(folder+"/Existing floor portions.asset")))count++;}return count;
    }
    public static string RepairOpenings()
    {
        var scene=SceneManager.GetActiveScene();var game=All<SibucaoSession>(scene).Single();var folder="Assets/StormSim/Areas/"+game.GetComponent<CoastalAreaProfile>().areaName+"/ExistingHomes";
        var cuts=game.homes.Select(h=>{var center=h.transform.TransformPoint(new Vector3(2.5f,1.25f,h.doorHinge.localPosition.z));var half=new Vector3(h.transform.lossyScale.x*.76f,1.3f,1f);return new Cut{inverse=Matrix4x4.TRS(center,h.transform.rotation,Vector3.one).inverse,half=half,bounds=new Bounds(center,new Vector3(4,3,4))};}).ToArray();
        int count=0;foreach(var f in All<MeshFilter>(scene).Where(f=>f.gameObject.activeInHierarchy&&MapHouse(f.transform)))
        {var r=f.GetComponent<Renderer>();if(!r||!cuts.Any(c=>c.bounds.Intersects(r.bounds)))continue;if(CutMesh(f,cuts,AssetDatabase.GenerateUniqueAssetPath(folder+"/Clear doorway.asset")))count++;}
        foreach(var h in game.homes)foreach(Transform t in h.transform)if(t.name=="Ceiling"||t.name=="Exposed ceiling joist")t.gameObject.SetActive(true);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Repaired doorways and interior ceilings: "+count;
    }
    static bool MapHouse(Transform t){while(t){if(t.name.StartsWith("Building ")||t.name.Contains("Houses")||t.name.Contains("Mapped")||t.name.Contains("roof clusters")||t.name.EndsWith(" - detailed"))return true;t=t.parent;}return false;}
    static float Distance(Vertex v,Cut c,int plane){var p=c.inverse.MultiplyPoint3x4(v.p);switch(plane){case 0:return p.x+c.half.x;case 1:return c.half.x-p.x;case 2:return p.y+c.half.y;case 3:return c.half.y-p.y;case 4:return p.z+c.half.z;default:return c.half.z-p.z;}}
    static List<Vertex> Clip(List<Vertex> poly,Cut c,int plane,bool inside)
    {var output=new List<Vertex>();if(poly.Count==0)return output;var a=poly[poly.Count-1];float da=Distance(a,c,plane)*(inside?1:-1);foreach(var b in poly){float db=Distance(b,c,plane)*(inside?1:-1);if((da>=0)!=(db>=0))output.Add(Vertex.Lerp(a,b,da/(da-db)));if(db>=0)output.Add(b);a=b;da=db;}return output;}
    static bool CutMesh(MeshFilter f,Cut[] cuts,string path)
    {
        var m=f.sharedMesh;var vs=m.vertices;var ns=m.normals;var uv=m.uv;var cs=m.colors;var output=new List<Vertex>();var subs=new List<int[]>();int touched=0;
        Vertex V(int i)=>new Vertex{p=f.transform.TransformPoint(vs[i]),n=ns.Length>i?f.transform.TransformDirection(ns[i]):Vector3.up,uv=uv.Length>i?uv[i]:Vector2.zero,c=cs.Length>i?cs[i]:Color.white};
        for(int sub=0;sub<m.subMeshCount;sub++)
        {var ids=new List<int>();var ts=m.GetTriangles(sub);for(int t=0;t<ts.Length;t+=3)
         {var poly=new List<Vertex>{V(ts[t]),V(ts[t+1]),V(ts[t+2])};var b=new Bounds(poly[0].p,Vector3.zero);b.Encapsulate(poly[1].p);b.Encapsulate(poly[2].p);var pieces=new List<List<Vertex>>{poly};
          foreach(var c in cuts.Where(c=>c.bounds.Intersects(b)))
          {var next=new List<List<Vertex>>();foreach(var piece in pieces){var intersection=piece;for(int p=0;p<6;p++)intersection=Clip(intersection,c,p,true);if(intersection.Count<3){next.Add(piece);continue;}touched++;var remaining=piece;for(int p=0;p<6;p++){var outside=Clip(remaining,c,p,false);if(outside.Count>=3)next.Add(outside);remaining=Clip(remaining,c,p,true);if(remaining.Count==0)break;}}pieces=next;}
          foreach(var piece in pieces)for(int k=1;k<piece.Count-1;k++)foreach(var v in new[]{piece[0],piece[k],piece[k+1]}){ids.Add(output.Count);output.Add(v);}
         }subs.Add(ids.ToArray());}
        if(touched==0)return false;
        var mesh=new Mesh{name=m.name+" - existing home doorway",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=output.Select(v=>f.transform.InverseTransformPoint(v.p)).ToArray();mesh.normals=output.Select(v=>f.transform.InverseTransformDirection(v.n)).ToArray();mesh.uv=output.Select(v=>v.uv).ToArray();mesh.colors=output.Select(v=>v.c).ToArray();mesh.subMeshCount=subs.Count;for(int i=0;i<subs.Count;i++)mesh.SetTriangles(subs[i],i);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;var collider=f.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=mesh;return true;
    }
}
