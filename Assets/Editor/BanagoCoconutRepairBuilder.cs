using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

public static class BanagoCoconutRepairBuilder
{
    public const string RootName="Banago coconut repair - same tree count";
    static Bounds ActualBounds(Transform t)
    {
        bool first=true;var b=new Bounds();
        foreach(var m in t.GetComponentsInChildren<MeshFilter>())if(m.sharedMesh)foreach(var v in m.sharedMesh.vertices)
        {var p=m.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
        return b;
    }
    static float Rand(System.Random r,float a,float b)=>a+(b-a)*(float)r.NextDouble();
    static void SetLodBounds(Transform t,LODGroup lod)
    {
        if(!lod)return;bool first=true;var bounds=new Bounds();
        // Read mesh geometry directly: renderer bounds can lag one frame after an asset refresh.
        foreach(var mf in t.GetComponentsInChildren<MeshFilter>())foreach(var v in mf.sharedMesh.vertices)
        {var p=t.InverseTransformPoint(mf.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
        lod.localReferencePoint=bounds.center;lod.size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));EditorUtility.SetDirty(lod);
    }
    public static object Build()
    {
        if(GameObject.Find(RootName))throw new Exception("Repair already exists; refuse duplicate application.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Wrong scene.");
        var older=GameObject.Find("Trees - satellite canopy locations");var dense=GameObject.Find(BanagoDenseCanopyBuilder.RootName)?.GetComponent<BanagoDenseCanopyReview>();
        if(!older||!dense)throw new Exception("Expected tree groups missing.");
        var targets=older.transform.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy).ToList();int oldCount=targets.Count;
        targets.AddRange(dense.plants.Where(p=>p.target&&p.target.gameObject.activeInHierarchy&&(p.species.Contains("Coconut")||p.species.Contains("Mango"))).Select(p=>p.target));
        if(targets.Any(t=>t.GetComponentsInChildren<MeshFilter>().Length!=2))throw new Exception("Unexpected tree structure; nothing altered.");
        // Capture the live scene, including any edits the user made after the last save.
        string backupDir="RoadRepairBackup/CoconutRepair_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backupDir);
        string backup=backupDir+"/MAIN_GAME.before-coconut-repair.unity";
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not save recovery copy.");
        var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var originalStates=existing.ToDictionary(t=>t,t=>new{t.name,t.position,t.localRotation,t.localScale,active=t.gameObject.activeSelf});
        var originalMeshes=existing.Select(t=>t.GetComponent<MeshFilter>()).Where(m=>m).ToDictionary(m=>m,m=>m.sharedMesh);
        var originalMaterials=existing.Select(t=>t.GetComponent<Renderer>()).Where(r=>r).ToDictionary(r=>r,r=>r.sharedMaterials);
        var shader=Shader.Find("StormSim/Coconut Ringed Bark and Fronds");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Palm shader has errors.");
        string folder="Assets/StormSim/Generated/CoconutRepair_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var material=new Material(shader){name="Ringed gray-brown bark and varied coconut fronds",enableInstancing=true};AssetDatabase.CreateAsset(material,folder+"/Coconut surface.mat");
        var models=new Mesh[6,2];for(int v=0;v<6;v++)for(int d=0;d<2;d++){models[v,d]=BanagoCoconutRepairModels.Build(v,d==0);AssetDatabase.CreateAsset(models[v,d],folder+"/Coconut "+v+" LOD"+d+".asset");}
        var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Repair coconut palms");var review=root.AddComponent<BanagoCoconutRepairReview>();review.backupScene=backup;review.assetFolder=folder;
        var meshSwaps=new List<BanagoCoconutRepairReview.MeshSwap>();var treeSwaps=new List<BanagoCoconutRepairReview.TreeSwap>();var rng=new System.Random(99268);var report=new List<object>();
        foreach(var t in targets)
        {
            var filters=t.GetComponentsInChildren<MeshFilter>();var cap=t.GetComponent<CapsuleCollider>();var lod=t.GetComponent<LODGroup>();var previous=ActualBounds(t);
            treeSwaps.Add(new BanagoCoconutRepairReview.TreeSwap{target=t,previousName=t.name,previousScale=t.localScale,previousRotation=t.localRotation,collider=cap,radius=cap?cap.radius:0,height=cap?cap.height:0,center=cap?cap.center:Vector3.zero,lod=lod,lodCenter=lod?lod.localReferencePoint:Vector3.zero,lodSize=lod?lod.size:0});
            Undo.RecordObject(t,"House-scale coconut appearance");int variant=rng.Next(6);
            for(int d=0;d<filters.Length;d++)
            {
                var mf=filters[d];var rr=mf.GetComponent<Renderer>();var newMesh=models[variant,d];
                meshSwaps.Add(new BanagoCoconutRepairReview.MeshSwap{target=mf,previous=mf.sharedMesh,replacement=newMesh,renderer=rr,previousMaterials=rr.sharedMaterials,material=material});
                Undo.RecordObject(mf,"Coconut mesh");Undo.RecordObject(rr,"Coconut material");mf.sharedMesh=newMesh;rr.sharedMaterial=material;
            }
            var parentScale=t.parent?t.parent.lossyScale:Vector3.one;float radiusScale=Rand(rng,.80f,.99f),heightScale=Rand(rng,.93f,1.15f);
            t.localScale=new Vector3(radiusScale/parentScale.x,heightScale/parentScale.y,radiusScale/parentScale.z);
            t.rotation=Quaternion.Euler(0,t.rotation.eulerAngles.y,0);
            t.name=t.name.Replace("spreading crown","coconut palm").Replace("oval crown","coconut palm").Replace("palm-like","coconut palm").Replace("Mango shade trees","Coconut replacement");
            if(cap){Undo.RecordObject(cap,"Slender palm trunk collider");cap.radius=.22f;cap.height=8.2f;cap.center=new Vector3(0,4.1f,.15f);}
            if(lod){Undo.RecordObject(lod,"Coconut LOD bounds");SetLodBounds(t,lod);}
            var now=ActualBounds(t);report.Add(new{name=t.name,previousWidth=Mathf.Max(previous.size.x,previous.size.z),width=Mathf.Max(now.size.x,now.size.z),height=now.size.y,trunkHeight=8.2f*heightScale,fronds=21+variant%3});
        }
        review.trees=treeSwaps.ToArray();review.meshes=meshSwaps.ToArray();
        review.notes=$"Appearance repair only: {oldCount} currently visible satellite trees and {targets.Count-oldCount} visible added palms/placeholder mango crowns. No added trees; all prior disabled trees remain disabled. Curved ringed trunks, 21-23 separated arching fronds, two shared LODs. Original meshes/scales/materials retained here and in the backup. Fruit-bearing yard mango trees and banana/bamboo plants are unchanged.";
        // Validate that every non-target object's authored state, mesh and material stayed intact.
        var targetSet=new HashSet<Transform>(targets);var targetMeshes=new HashSet<MeshFilter>(meshSwaps.Select(s=>s.target));var targetRenderers=new HashSet<Renderer>(meshSwaps.Select(s=>s.renderer));var errors=new List<string>();
        foreach(var kv in originalStates){var t=kv.Key;var a=kv.Value;if(!t){errors.Add("Missing original object");continue;}if(t.position!=a.position||t.gameObject.activeSelf!=a.active)errors.Add("Position/visibility changed: "+t.name);if(!targetSet.Contains(t)&&(t.name!=a.name||t.localRotation!=a.localRotation||t.localScale!=a.localScale))errors.Add("Non-tree changed: "+t.name);}
        foreach(var kv in originalMeshes)if(kv.Key&&!targetMeshes.Contains(kv.Key)&&kv.Key.sharedMesh!=kv.Value)errors.Add("Non-tree mesh changed: "+kv.Key.name);
        foreach(var kv in originalMaterials)if(kv.Key&&!targetRenderers.Contains(kv.Key)&&!kv.Key.sharedMaterials.SequenceEqual(kv.Value))errors.Add("Non-tree material changed: "+kv.Key.name);
        var payload=new{changedTrees=targets.Count,oldCount,addedTrees=0,preservedObjects=existing.Length,preservationErrors=errors,backup,folder,trees=report};
        File.WriteAllText(backupDir+"/repair-report.json",JsonConvert.SerializeObject(payload,Formatting.Indented));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();return payload;
    }
    public static object Validate()
    {
        var review=GameObject.Find(RootName)?.GetComponent<BanagoCoconutRepairReview>();if(!review)throw new Exception("Repair not built.");
        var meshes=review.meshes.Select(s=>s.replacement).Distinct().ToArray();
        return new{count=review.trees.Length,active=review.trees.Count(s=>s.target&&s.target.gameObject.activeInHierarchy),maxWidth=review.trees.Max(s=>{var b=ActualBounds(s.target);return Mathf.Max(b.size.x,b.size.z);}),maxHeight=review.trees.Max(s=>ActualBounds(s.target).size.y),minimumLodSize=review.trees.Min(s=>s.lod.size),missingMeshes=review.meshes.Count(s=>!s.target||s.target.sharedMesh!=s.replacement),invalidVertices=meshes.Sum(m=>m.vertices.Count(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))),review.backupScene};
    }
    public static object RegenerateOwnedModels()
    {
        var review=GameObject.Find(RootName)?.GetComponent<BanagoCoconutRepairReview>();if(!review)throw new Exception("Repair not built.");
        if(!review.assetFolder.StartsWith("Assets/StormSim/Generated/CoconutRepair_"))throw new Exception("Unexpected asset directory.");
        for(int v=0;v<6;v++)for(int d=0;d<2;d++)
        {
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(review.assetFolder+"/Coconut "+v+" LOD"+d+".asset");if(!target)throw new Exception("Missing owned mesh.");
            var source=BanagoCoconutRepairModels.Build(v,d==0);
            EditorUtility.CopySerialized(source,target);EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(source);
        }
        foreach(var t in review.trees)SetLodBounds(t.target,t.lod);
        AssetDatabase.SaveAssets();SceneView.RepaintAll();return Validate();
    }
    public static object FinalizeReview()
    {
        var review=GameObject.Find(RootName).GetComponent<BanagoCoconutRepairReview>();
        foreach(var t in review.trees)SetLodBounds(t.target,t.lod);
        var dense=GameObject.Find(BanagoDenseCanopyBuilder.RootName).GetComponent<BanagoDenseCanopyReview>();var changed=new HashSet<Transform>(review.trees.Select(t=>t.target));
        foreach(var p in dense.plants)if(p.target&&changed.Contains(p.target))
        {
            if(p.species.Contains("Mango"))p.species="Coconut replacement for placeholder mango";
            var b=ActualBounds(p.target);p.crownRadius=Mathf.Max(b.size.x,b.size.z)/2;p.groundY=p.target.position.y;p.crownFloor=p.groundY+6.7f*p.target.lossyScale.y;
        }
        EditorUtility.SetDirty(dense);var result=Validate();
        File.WriteAllText(Path.GetDirectoryName(review.backupScene)+"/final-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());return result;
    }
}
