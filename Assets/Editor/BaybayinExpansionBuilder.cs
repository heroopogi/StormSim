using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Builds only the supplied Baybayin expansion; existing map objects are retained.</summary>
public static class BaybayinExpansionBuilder
{
    public const string RootName="Purok Baybayin - satellite matched expansion";
    public const string Folder="Assets/StormSim/Generated/BaybayinExpansion_20260915";
    public const string Source="RoadRepairBackup/BaybayinExpansion";
    public static string Status="Not started";
    static GameObject root;
    static JArray objects,trees;
    static int next,treeNext;
    static Dictionary<string,Transform> groups;
    static Dictionary<string,Material> materials;
    static Mesh[] treeMeshes;
    static Material treeMaterial;
    static Color C(JToken a)=>new Color((float)a[0],(float)a[1],(float)a[2],a.Count()>3?(float)a[3]:1);
    static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
    static Transform Group(string name)
    {
        if(groups.TryGetValue(name,out var t))return t;
        var g=new GameObject(name);g.transform.SetParent(root.transform,false);groups[name]=g.transform;return g.transform;
    }
    static Material Material(string name,float corrugation=0,float metallic=0)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");if(existing)return existing;
        var s=Shader.Find("StormSim/Weathered Building Surface");if(!s||ShaderUtil.ShaderHasError(s))throw new Exception("Existing building surface shader unavailable.");
        var m=new Material(s){name=name,enableInstancing=true};m.SetFloat("_VertexColor",1);m.SetFloat("_Cull",0);m.SetFloat("_Corrugation",corrugation);m.SetFloat("_Weathering",.3f);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.1f);
        AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    public static object Begin()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Open MAIN_GAME in Edit mode.");
        var existingRoot=GameObject.Find(RootName);
        if(existingRoot&&existingRoot.GetComponentsInChildren<MeshFilter>(true).Length>0)throw new Exception("Expansion already contains geometry. Inspect it before rebuilding.");
        var data=JObject.Parse(File.ReadAllText(Source+"/scene-plan.json"));objects=(JArray)data["objects"];trees=(JArray)data["trees"];
        Directory.CreateDirectory(Source);if(!File.Exists(Source+"/MAIN_GAME.before-expansion.unity")&&!EditorSceneManager.SaveScene(scene,Source+"/MAIN_GAME.before-expansion.unity",true))throw new Exception("Scene backup failed.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        materials=new Dictionary<string,Material>();materials["walls"]=Material("Weathered plaster and timber");materials["roofs"]=Material("Satellite sampled corrugated metal",.9f,.17f);materials["detail"]=Material("Doors windows roads and shoreline");
        var seaSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/BanagoSeaPhysics_20260907/DynamicSea.mat");
        materials["sea"]=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Coastal water.mat");if(!materials["sea"]){materials["sea"]=new Material(seaSource){name="Baybayin coastal water"};AssetDatabase.CreateAsset(materials["sea"],Folder+"/Coastal water.mat");}
        root=existingRoot?existingRoot:new GameObject(RootName);if(!existingRoot)Undo.RegisterCreatedObjectUndo(root,"Build Baybayin map extension");groups=new Dictionary<string,Transform>();next=0;treeNext=0;
        var marker=root.GetComponent<BaybayinExpansionInfo>();if(!marker)marker=root.AddComponent<BaybayinExpansionInfo>();marker.referenceNotes=(string)data["notes"];marker.sourceFolder=Source;marker.buildingCount=(int)data["buildingCount"];marker.coastalBuildingCount=(int)data["coastalCount"];
        // Existing vegetation design is reused at actual green-cover locations.
        treeMeshes=new Mesh[4];for(int i=0;i<4;i++){treeMeshes[i]=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Tree "+i+".asset");if(!treeMeshes[i]){treeMeshes[i]=BanagoEnvironmentModels.Tree(i==3?2:0,i,false,new Color(.15f,.29f,.09f));AssetDatabase.CreateAsset(treeMeshes[i],Folder+"/Tree "+i+".asset");}}
        treeMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/BanagoDenseCanopy_20260912_033402/Canopy surface.mat");
        Status="Building "+objects.Count+" mapped objects";return new{objects=objects.Count,trees=trees.Count,backup=Source+"/MAIN_GAME.before-expansion.unity"};
    }
    public static object Step(int count=100)
    {
        if(!root)throw new Exception("Begin the expansion first.");
        int end=Math.Min(next+count,objects.Count);
        for(;next<end;next++)
        {
            var d=objects[next];var g=new GameObject((string)d["name"]);g.transform.SetParent(Group((string)d["group"]),false);
            if(d["position"]!=null)g.transform.localPosition=V(d["position"]);
            var mesh=new Mesh{name=g.name,indexFormat=IndexFormat.UInt32};
            mesh.vertices=d["vertices"].Select(V).ToArray();mesh.colors=d["colors"].Select(C).ToArray();
            if(d["uv"]!=null)mesh.uv=d["uv"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();
            var subs=(JArray)d["triangles"];mesh.subMeshCount=subs.Count;for(int s=0;s<subs.Count;s++)mesh.SetTriangles(subs[s].Values<int>().ToArray(),s);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Folder+"/Mesh "+next.ToString("00000")+".asset");
            g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=d["materials"].Values<string>().Select(k=>materials[k]).ToArray();r.shadowCastingMode=(string)d["group"]=="Coastal water"?ShadowCastingMode.Off:ShadowCastingMode.On;
            if((bool?)d["collider"]==true)g.AddComponent<MeshCollider>().sharedMesh=mesh;
            if(d["sourceId"]!=null){var info=g.AddComponent<BaybayinMappedBuilding>();info.sourceId=(string)d["sourceId"];info.coastal=(bool?)d["coastal"]==true;info.heightEstimated=true;}
        }
        Status="Objects "+next+"/"+objects.Count;return new{done=next==objects.Count,next,total=objects.Count};
    }
    public static object Plant(int count=150)
    {
        int end=Math.Min(treeNext+count,trees.Count);
        for(;treeNext<end;treeNext++)
        {
            var d=trees[treeNext];int variant=(int)d["variant"];
            var g=new GameObject("Mapped tree "+treeNext.ToString("000"));g.transform.SetParent(Group("Satellite green belts and yards"),false);g.transform.position=V(d["position"]);g.transform.rotation=Quaternion.Euler(0,(float)d["yaw"],0);g.transform.localScale=new Vector3((float)d["radius"],(float)d["height"],(float)d["radius"]);
            g.AddComponent<MeshFilter>().sharedMesh=treeMeshes[variant];g.AddComponent<MeshRenderer>().sharedMaterial=treeMaterial;
        }
        return new{done=treeNext==trees.Count,treeNext,total=trees.Count};
    }
    public static object Finish()
    {
        if(next!=objects.Count||treeNext!=trees.Count)throw new Exception("Build is not complete.");
        var filters=root.GetComponentsInChildren<MeshFilter>();var problems=new List<string>();long triangles=0;
        foreach(var f in filters)
        {
            var m=f.sharedMesh;if(!m){problems.Add(f.name+": no mesh");continue;}
            if(m.vertices.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)))problems.Add(f.name+": invalid vertices");
            if(f.GetComponent<Renderer>().sharedMaterials.Any(m=>!m||!m.shader))problems.Add(f.name+": missing material");
            triangles+=m.triangles.Length/3;
        }
        if(problems.Count>0)throw new Exception(string.Join(";",problems.Take(10)));
        AssetDatabase.SaveAssets();var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);bool saved=EditorSceneManager.SaveScene(scene);
        var report=new{saved,root=RootName,objects=next,trees=treeNext,meshCount=filters.Length,triangles,problems};File.WriteAllText(Source+"/unity-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));Status="Complete";return report;
    }
}
