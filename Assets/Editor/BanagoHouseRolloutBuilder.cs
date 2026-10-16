using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Imports spatially batched house meshes without blocking the Editor.</summary>
public static class BanagoHouseRolloutBuilder
{
    public static string Status="Not started";
    static JArray batches;static int next;static GameObject root;static string folder;
    static Dictionary<string,Material> materials;
    public static void RefineGeometry()
    {
        root=GameObject.Find("Banago Houses - full detail redesign");
        batches=(JArray)JObject.Parse(File.ReadAllText("RoadRepairBackup/full-house-manifest.json"))["batches"];next=0;
        EditorApplication.update-=RefineTick;EditorApplication.update+=RefineTick;Status="Refining shared footprint edges";
    }
    static void RefineTick()
    {
        try
        {
            if(next>=batches.Count)
            {
                EditorApplication.update-=RefineTick;AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Status="Complete: all 2257 houses refined with separated roof boundaries";return;
            }
            var b=batches[next];var parent=root.transform.Find((string)b["name"]);
            if(!parent)throw new InvalidDataException("A house batch moved outside its original neighbourhood");
            foreach(var d in b["meshes"])
            {
                var f=parent.Find((string)d["name"]).GetComponent<MeshFilter>();var source=ReadBinary((string)d["file"],f.sharedMesh.name);var m=f.sharedMesh;
                var collider=f.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=null;
                m.Clear();m.indexFormat=IndexFormat.UInt32;m.vertices=source.vertices;m.normals=source.normals;m.uv=source.uv;m.colors=source.colors;m.triangles=source.triangles;m.RecalculateBounds();m.UploadMeshData(false);
                if(collider)collider.sharedMesh=m;UnityEngine.Object.DestroyImmediate(source);EditorUtility.SetDirty(m);
            }
            next++;Status="Refined neighbourhood "+next+" / "+batches.Count;
        }
        catch(Exception e){EditorApplication.update-=RefineTick;Status="Refinement stopped: "+e.Message;Debug.LogException(e);}
    }
    public static void Resume()
    {
        root=GameObject.Find("Banago Houses - full detail redesign");
        if(!root)throw new InvalidOperationException("No partial house rollout exists");
        batches=(JArray)JObject.Parse(File.ReadAllText("RoadRepairBackup/full-house-manifest.json"))["batches"];
        next=root.transform.childCount;
        folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(root.GetComponentInChildren<MeshFilter>().sharedMesh)).Replace('\\','/');
        materials=new Dictionary<string,Material>();foreach(var kind in new[]{"plaster","metal","details"})materials[kind]=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+kind+".mat");
        var completed=new HashSet<string>(batches.Take(next).SelectMany(b=>b["ids"].Values<string>()));
        foreach(var old in root.GetComponent<BanagoLocalDetailReview>().originals)old.SetActive(!completed.Contains(old.GetComponent<BanagoHousePreview>().footprintId));
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;Status="Resuming neighbourhood "+next+" / "+batches.Count;
    }
    public static void Start()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
        if(GameObject.Find("Banago Houses - full detail redesign"))throw new InvalidOperationException("House rollout already exists");
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/full-house-manifest.json"));batches=(JArray)data["batches"];next=0;
        var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene);
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");File.Copy(scene.path,"RoadRepairBackup/MAIN_GAME_before_full_house_redesign_"+stamp+".unity");
        folder="Assets/StormSim/Generated/HouseRedesign_"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        root=new GameObject("Banago Houses - full detail redesign");root.transform.SetParent(GameObject.Find("CesiumGeoreference").transform,false);
        Undo.RegisterCreatedObjectUndo(root,"Redesign all remaining houses");
        var ids=new HashSet<string>(batches.SelectMany(b=>b["ids"].Values<string>()));
        var originals=GameObject.Find("Banago Houses").GetComponentsInChildren<BanagoHousePreview>(true).Where(h=>ids.Contains(h.footprintId)).Select(h=>h.gameObject).ToArray();
        if(originals.Any(g=>!g.activeInHierarchy))throw new InvalidOperationException("An original house has already been replaced");
        var review=root.AddComponent<BanagoLocalDetailReview>();review.originals=originals;review.originalActive=originals.Select(g=>g.activeSelf).ToArray();
        review.notes=data["summary"].ToString()+"\nDisable this root to restore the original houses. Spatial batches share materials to reduce rendering cost. Public/community buildings retain their separate review groups.";
        materials=new Dictionary<string,Material>();
        foreach(var name in new[]{"plaster","metal","details"})
        {
            var m=new Material(Shader.Find("StormSim/Weathered Building Surface")){name=name=="plaster"?"Varied rough plaster and concrete":name=="metal"?"Weathered corrugated roofing and sheets":"Window frames doors fascia and foundations",enableInstancing=true};
            m.SetFloat("_Corrugation",name=="metal"?1:0);m.SetFloat("_Weathering",name=="details"?.15f:name=="metal"?.48f:.25f);
            m.SetFloat("_Smoothness",name=="metal"?.25f:.10f);m.SetFloat("_Metallic",name=="metal"?.18f:0);m.SetFloat("_Cull",0);
            AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");materials[name]=m;
        }
        File.WriteAllText(folder+"/Placement and reversibility.json",data["summary"].ToString());
        Status="Starting "+originals.Length+" house upgrades";EditorApplication.update+=Tick;
    }
    static Mesh ReadBinary(string file,string name)
    {
        using(var reader=new BinaryReader(File.OpenRead(file)))
        {
            int count=reader.ReadInt32();var vs=new Vector3[count];var ns=new Vector3[count];var uv=new Vector2[count];var cs=new Color[count];var triangles=new int[count];
            for(int i=0;i<count;i++)
            {
                vs[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());ns[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());cs[i]=new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),1);triangles[i]=i;
                if(!float.IsFinite(vs[i].x)||!float.IsFinite(vs[i].y)||!float.IsFinite(vs[i].z))throw new InvalidDataException(file);
            }
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.vertices=vs;mesh.normals=ns;mesh.uv=uv;mesh.colors=cs;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
    }
    static void Tick()
    {
        try
        {
            if(!root)throw new InvalidOperationException("Redesign root was removed");
            if(next>=batches.Count)
            {
                EditorApplication.update-=Tick;root.GetComponent<BanagoLocalDetailReview>().Apply();
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
                Selection.activeGameObject=root;Status="Complete: "+root.GetComponent<BanagoLocalDetailReview>().originals.Length+" houses in "+next+" neighbourhood batches";return;
            }
            var batch=batches[next];var parent=new GameObject((string)batch["name"]);parent.transform.SetParent(root.transform,false);string path=folder+"/Batch_"+next+".asset";bool first=true;
            foreach(var d in batch["meshes"])
            {
                string kind=(string)d["kind"];var mesh=ReadBinary((string)d["file"],parent.name+" "+kind);
                if(first){AssetDatabase.CreateAsset(mesh,path);first=false;}else AssetDatabase.AddObjectToAsset(mesh,path);
                var go=new GameObject((string)d["name"]);go.transform.SetParent(parent.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[kind];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                if(kind=="plaster"||kind=="metal")go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            var ids=new HashSet<string>(batch["ids"].Values<string>());
            foreach(var old in root.GetComponent<BanagoLocalDetailReview>().originals)
                if(ids.Contains(old.GetComponent<BanagoHousePreview>().footprintId))old.SetActive(false);
            next++;Status="Imported neighbourhood "+next+" / "+batches.Count;
        }
        catch(Exception e){EditorApplication.update-=Tick;Status="House import stopped: "+e.Message;Debug.LogException(e);}
    }
}
