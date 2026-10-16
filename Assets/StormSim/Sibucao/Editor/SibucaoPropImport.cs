using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoPropImport
    {
        public const string Folder="Assets/StormSim/Sibucao/Art/Higgsfield";
        public const string Source=Folder+"/SibucaoBarangayKit.glb";
        const string Project="https://higgsfield.ai/3d-jutsu/e2997397-33d6-4267-bfec-dd5758e7eed4";
        [Serializable] public class Entry{public string name,prefab;public int triangles,meshes,materials;public Vector3 metres;public string[] textures;public string validation;}
        [Serializable] public class Audit{public string source;public Entry[] props;}
        static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("No renderable geometry: "+go.name);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
        public static string BuildPrefabs()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode first.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);if(!source)throw new Exception("Validated GLB is not imported yet: "+Source);
            Directory.CreateDirectory(Folder+"/Prefabs");AssetDatabase.Refresh();
            var staged=Object.Instantiate(source);staged.hideFlags=HideFlags.HideAndDontSave;
            var entries=new List<Entry>();
            try
            {
                var roots=staged.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("PROP_")&&!t.name.EndsWith("_mesh")&&(!t.parent||!t.parent.name.StartsWith("PROP_"))).ToArray();
                if(roots.Length==0)throw new Exception("No named prop roots found in GLB.");
                foreach(var src in roots)
                {
                    var go=Object.Instantiate(src.gameObject);go.name=src.name;go.transform.SetParent(null);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
                    try
                    {
                        var filters=go.GetComponentsInChildren<MeshFilter>();int triangles=0;
                        foreach(var filter in filters)
                        {
                            var mesh=filter.sharedMesh;if(!mesh||mesh.vertexCount==0)throw new Exception(go.name+": empty mesh");
                            triangles+=mesh.triangles.Length/3;
                            if(mesh.uv.Length!=mesh.vertexCount||mesh.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)))throw new Exception(go.name+": missing/invalid UVs");
                            if(mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception(go.name+": non-finite vertices");
                        }
                        int limit=go.name.Contains("Tricycle")?25000:go.name.Contains("SariSari")?15000:go.name.Contains("Basketball")?6500:8000;
                        if(triangles>limit)throw new Exception(go.name+": triangle budget exceeded "+triangles+" / "+limit);
                        var b=BoundsOf(go);if(b.size.magnitude<.05f||b.size.magnitude>7)throw new Exception(go.name+": implausible metre scale "+b.size);
                        var offset=new Vector3(-b.center.x,-b.min.y,-b.center.z);foreach(Transform child in go.transform)child.position+=offset;
                        b=BoundsOf(go);
                        var mats=go.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
                        var tex=mats.SelectMany(m=>m.GetTexturePropertyNames().Select(p=>m.GetTexture(p))).Where(t=>t).Distinct().ToArray();
                        if(tex.Length==0)throw new Exception(go.name+": expected PBR atlas is missing");
                        foreach(var r in go.GetComponentsInChildren<Renderer>())if(!r.sharedMaterial||!r.sharedMaterial.shader||r.sharedMaterial.shader.name.Contains("Error"))throw new Exception(go.name+": broken material");
                        foreach(var filter in filters){var c=filter.gameObject.AddComponent<MeshCollider>();c.sharedMesh=filter.sharedMesh;}
                        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(go.name.Contains("SariSari")?.003f:.012f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                        var provenance=go.AddComponent<SibucaoPropSource>();provenance.sourceProject=Project;provenance.sourceAsset=src.name;provenance.triangles=triangles;provenance.textureResolution=tex.Max(t=>t.width);
                        provenance.validation="Metre scale; ground-centre pivot; finite geometry and UVs; atlas UV overlap is intentional. Visual review required alongside this numeric audit.";
                        string path=Folder+"/Prefabs/"+go.name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);
                        entries.Add(new Entry{name=go.name,prefab=path,triangles=triangles,meshes=filters.Length,materials=mats.Length,metres=b.size,textures=tex.Select(t=>t.name+" "+t.width+"x"+t.height).ToArray(),validation=provenance.validation});
                    }
                    finally{Object.DestroyImmediate(go);}
                }
            }
            finally{Object.DestroyImmediate(staged);}
            File.WriteAllText("Documentation/SIBUCAO_PROP_IMPORT_AUDIT.json",JsonUtility.ToJson(new Audit{source=Project,props=entries.ToArray()},true));AssetDatabase.SaveAssets();
            return "Validated and created "+entries.Count+" reusable prefabs; total "+entries.Sum(e=>e.triangles)+" triangles.";
        }
        static GameObject Prefab(string suffix)=>AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/PROP_"+suffix+".prefab");
        public static string InstallSmallProps()
        {
            var s=Object.FindAnyObjectByType<SibucaoSession>();if(Application.isPlaying||!s||s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("Open SIBUCAO_RAIN in Edit mode.");
            if(GameObject.Find("Sibucao Higgsfield validated yard props"))return "Prop scatter already installed; use its inspector to rebake intentionally.";
            var cfg=new GameObject("Sibucao Higgsfield validated yard props").AddComponent<SibucaoDressing>();cfg.route=s.route;cfg.seed=260926;cfg.propsPerYard=2;cfg.grassPerYard=0;cfg.maximumObjects=120;cfg.routeEnd=s.route.length;cfg.routeBand=35;cfg.minimumWorldZ=-100000;
            cfg.householdPrefabs=new[]{"WaterDrum_Blue","PlasticChair","PlasticBasins","FirewoodStack","Planter_Pail","Planter_TinCan","Planter_CutJug","LaundryBundle"}.Select(Prefab).ToArray();
            if(cfg.householdPrefabs.Any(p=>!p))throw new Exception("Validated prop prefabs missing.");
            return SibucaoDressingBuilder.Bake(cfg);
        }
        public static string PlaceSetPiece(string suffix,Vector3 groundPoint,float yaw)
        {
            var s=Object.FindAnyObjectByType<SibucaoSession>();if(Application.isPlaying||!s||s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("Use SIBUCAO_RAIN in Edit mode.");
            var prefab=Prefab(suffix);if(!prefab)throw new Exception("Missing validated prop "+suffix);
            string name="Sibucao set piece - "+suffix;if(GameObject.Find(name))return "Already placed: "+name;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(go,"Place Sibucao set piece");go.name=name;go.transform.SetPositionAndRotation(groundPoint,Quaternion.Euler(0,yaw,0));Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return name;
        }
    }
}
