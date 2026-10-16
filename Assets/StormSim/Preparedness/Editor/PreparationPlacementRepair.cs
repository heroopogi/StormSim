using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace StormSim.Preparedness.Editor
{
 public static class PreparationPlacementRepair
 {
  static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
  public static string RepairCourtyardFloor()
  {
   if(Application.isPlaying)throw new Exception("Edit mode required");
   var room=Object.FindObjectsByType<PreparednessHomeInterior>(FindObjectsSortMode.None).First(h=>h.name.StartsWith("House 2192 "));
   if(room.transform.Find("Footprint fitted floor"))return "Floor already repaired";
   var source=Object.FindObjectsByType<BanagoHousePreview>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(h=>h.footprintId==room.footprintId);
   var sourceVertices=source.GetComponent<MeshFilter>().sharedMesh.vertices;float low=sourceVertices.Min(v=>v.y);
   var polygon=new List<Vector2>();
   foreach(var v in sourceVertices.Where(v=>Mathf.Abs(v.y-low)<.001f))
   {var world=source.transform.TransformPoint(v);var local=room.transform.InverseTransformPoint(world);var p=new Vector2(local.x,local.z);if(!polygon.Any(q=>(q-p).sqrMagnitude<.000001f))polygon.Add(p);}
   float area=0;for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=a.x*b.y-b.x*a.y;}if(area<0)polygon.Reverse();
   var remaining=Enumerable.Range(0,polygon.Count).ToList();var faces=new List<int>();int guard=0;
   while(remaining.Count>3&&guard++<1000)
   {
    bool clipped=false;
    for(int k=0;k<remaining.Count;k++)
    {
     int a=remaining[(k+remaining.Count-1)%remaining.Count],b=remaining[k],c=remaining[(k+1)%remaining.Count];
     if(Cross(polygon[a],polygon[b],polygon[c])<=.00001f)continue;
     bool occupied=remaining.Any(t=>t!=a&&t!=b&&t!=c&&Cross(polygon[a],polygon[b],polygon[t])>=-.00001f&&Cross(polygon[b],polygon[c],polygon[t])>=-.00001f&&Cross(polygon[c],polygon[a],polygon[t])>=-.00001f);
     if(occupied)continue;faces.AddRange(new[]{a,c,b});remaining.RemoveAt(k);clipped=true;break;
    }
    if(!clipped)throw new Exception("Footprint triangulation failed; no scene changes applied");
   }
   faces.AddRange(new[]{remaining[0],remaining[2],remaining[1]});
   var f=room.GetComponent<MeshFilter>();var original=f.sharedMesh;var vertices=original.vertices;
   if(!vertices.Take(24).All(v=>v.y>=-.201f&&v.y<=.001f))throw new Exception("Unexpected combined floor vertex order");
   var replacement=Object.Instantiate(original);replacement.name="House 2192 furnishings without oversized slab";
   for(int sub=0;sub<replacement.subMeshCount;sub++){var ts=replacement.GetTriangles(sub);var keep=new List<int>();for(int t=0;t<ts.Length;t+=3)if(!(ts[t]<24&&ts[t+1]<24&&ts[t+2]<24))keep.AddRange(new[]{ts[t],ts[t+1],ts[t+2]});replacement.SetTriangles(keep,sub);}
   string dir="Assets/StormSim/Preparedness/Art/Neighborhood/";
   AssetDatabase.CreateAsset(replacement,AssetDatabase.GenerateUniqueAssetPath(dir+"House 2192 corrected furnishings.asset"));f.sharedMesh=replacement;
   var floor=room.transform.Find("Solid household floor");floor.gameObject.SetActive(false);
   int count=polygon.Count;var vs=polygon.Select(p=>new Vector3(p.x,0,p.y)).Concat(polygon.Select(p=>new Vector3(p.x,-.22f,p.y))).ToArray();var tris=new List<int>(faces);
   for(int i=0;i<faces.Count;i+=3)tris.AddRange(new[]{faces[i]+count,faces[i+2]+count,faces[i+1]+count});
   for(int i=0;i<count;i++){int n=(i+1)%count;tris.AddRange(new[]{i,n,i+count,n,n+count,i+count});}
   var mesh=new Mesh{name="Concave footprint floor"};mesh.vertices=vs;mesh.triangles=tris.ToArray();mesh.uv=vs.Select(v=>new Vector2(v.x,v.z)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(dir+"House 2192 footprint floor.asset"));
   var go=new GameObject("Footprint fitted floor");go.transform.SetParent(room.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=room.GetComponent<Renderer>().sharedMaterials[0];go.AddComponent<MeshCollider>().sharedMesh=mesh;
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(room.gameObject.scene);return "Replaced oversized rectangle with "+count+"-vertex footprint and matching solid collider";
  }
 }
}
