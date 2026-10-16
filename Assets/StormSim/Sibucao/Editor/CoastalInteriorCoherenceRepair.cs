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
public static class CoastalInteriorCoherenceRepair
{
    struct Pose {public Vector3 p,s;public Quaternion q;}
    static Dictionary<string,Transform> Tree(Transform root)
    {var map=new Dictionary<string,Transform>();void Add(Transform parent,string path){var counts=new Dictionary<string,int>();foreach(Transform t in parent){counts.TryGetValue(t.name,out int n);counts[t.name]=n+1;var key=path+"/"+t.name+"#"+n;map[key]=t;Add(t,key);}}Add(root,"");return map;}
    static IEnumerable<T> All<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
    static void Box(Transform root,string name,Vector3 p,Vector3 size,Material mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;}
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var scene=SceneManager.GetActiveScene();var s=All<SibucaoSession>(scene).Single();var b=s.GetComponent<SibucaoPreparationBridge>();
        Directory.CreateDirectory("Reports/CoastalAreas/Backups");EditorSceneManager.SaveScene(scene,"Reports/CoastalAreas/Backups/"+s.GetComponent<CoastalAreaProfile>().areaName+"-before-coherence.unity",true);
        var source=EditorSceneManager.OpenScene("Assets/Scenes/SIBUCAO_RAIN.unity",OpenSceneMode.Additive);var original=All<SibucaoSession>(source).Single();
        var poses=Tree(original.homes[0].transform).ToDictionary(k=>k.Key,k=>new Pose{p=k.Value.localPosition,q=k.Value.localRotation,s=k.Value.localScale});
        var prepPoses=Tree(original.GetComponent<SibucaoPreparationBridge>().preparation.transform).ToDictionary(k=>k.Key,k=>new Pose{p=k.Value.localPosition,q=k.Value.localRotation,s=k.Value.localScale});
        EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(scene);
        int changed=0;
        foreach(var h in s.homes)
        {
            foreach(var name in new[]{"Sibucao TV assembly","Sibucao bed assembly","Sibucao dining assembly"})
            {var group=h.transform.Find(name);if(!group)continue;foreach(var t in group.Cast<Transform>().ToArray())t.SetParent(h.transform,true);Object.DestroyImmediate(group.gameObject);}
            foreach(var pair in Tree(h.transform))if(poses.TryGetValue(pair.Key,out var pose))
            {pair.Value.localPosition=pose.p;pair.Value.localRotation=pose.q;pair.Value.localScale=pose.s;changed++;}
            foreach(Transform t in h.transform)if(t.name.StartsWith("Monobloc")||t.name=="Chair leg")t.localPosition+=Vector3.forward*.8f;
            foreach(var decorativeBroom in h.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Walis tambo beside front door"))decorativeBroom.gameObject.SetActive(false);
            // The exterior remains the mapped building. Floor and preparation share its local frame.
            var frame=h.transform.Find("Fitted existing-house door frame");if(frame)Object.DestroyImmediate(frame.gameObject);
            var root=new GameObject("Fitted existing-house door frame").transform;root.SetParent(h.transform,false);
            var panel=h.doorPanel.GetComponent<Renderer>();var mat=panel?panel.sharedMaterial:h.transform.Find("Tiled floor").GetComponent<Renderer>().sharedMaterial;
            // Close the previous oversized cut without obstructing the actual 1.4 m door leaf.
            Box(root,"Left timber jamb",new Vector3(1.59f,1.15f,-3.53f),new Vector3(.42f,2.3f,.42f),mat);
            Box(root,"Right timber jamb",new Vector3(3.41f,1.15f,-3.53f),new Vector3(.42f,2.3f,.42f),mat);
            Box(root,"Solid upper door header",new Vector3(2.5f,2.67f,-3.53f),new Vector3(2.24f,.74f,.42f),mat);
            Box(root,"Level door threshold",new Vector3(2.5f,.018f,-3.53f),new Vector3(1.4f,.025f,.5f),mat);
            var item=h.doorHinge.GetComponent<PreparednessItem>();item.closed=true;item.ApplyVisual();
        }
        foreach(var pair in Tree(b.preparation.transform))if(prepPoses.TryGetValue(pair.Key,out var pose)){pair.Value.localPosition=pose.p;pair.Value.localRotation=pose.q;pair.Value.localScale=pose.s;}
        b.firstHome=s.homes[0];b.preparation.homeStart=s.homes[0].standingPoint;b.preparation.transform.SetPositionAndRotation(s.homes[0].transform.position,s.homes[0].transform.rotation);b.preparation.transform.localScale=s.homes[0].transform.lossyScale;
        s.GetComponent<CoastalAreaProfile>().preserveFurnishingSize=false;
        // Keep the chest and bed separated in the same coordinate system.
        var chest=b.preparation.transform.Find("Mixed household storage chest");if(chest)chest.localPosition=new Vector3(.25f,0,-2.9f);
        // Stored contents move with their actual storage furniture rather than remaining at stale offsets.
        var lid=b.preparation.items.FirstOrDefault(i=>i.id=="chest-lid");if(lid&&chest)foreach(var item in b.preparation.items.Where(i=>i.storageDoor==lid))item.transform.localPosition+=Vector3.right*.1f;
        // Put the small TV supplies on the cabinet top, clear of the screen and front edge.
        foreach(var id in new[]{"phone","cells-2","whistle"})
        {var item=b.preparation.items.First(i=>i.id==id);item.transform.localPosition+=Vector3.forward*.34f;}
        var screwdriver=b.preparation.items.First(i=>i.id=="screwdriver");screwdriver.transform.localPosition+=Vector3.up*.035f;
        var broom=b.preparation.items.First(i=>i.id=="house-broom");broom.transform.localPosition=new Vector3(1.2f,.04f,-3.05f);
        bool active=b.content.activeSelf;b.content.SetActive(true);CoastalHomePlacement.Fit(b.preparation);b.content.SetActive(active);
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Restored "+changed+" matching home transforms and shared preparation layout; fitted four door frames.";
    }
}
