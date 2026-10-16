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

public static class CoastalAreaSceneChecks
{
    class Pose{public Vector3 position;public Quaternion rotation;public int home;public bool departure;}
    static Quaternion Heading(SibucaoRoute route,float d){var f=route.Sample(Mathf.Min(route.length,d+2))-route.Sample(Mathf.Max(0,d-2));f.y=0;return Quaternion.LookRotation(f.sqrMagnitude>.001f?f:Vector3.forward);}
    public static string RepairBindings()
    {
        var target=SceneManager.GetActiveScene();var s=target.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SibucaoSession>(true)).Single();
        var source=EditorSceneManager.OpenScene("Assets/Scenes/SIBUCAO_RAIN.unity",OpenSceneMode.Additive);
        var original=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SibucaoSession>(true)).Single();
        var oldRoute=original.route;var homePos=original.homes.Select(h=>h.transform.position).ToArray();var homeRot=original.homes.Select(h=>h.transform.rotation).ToArray();var starts=original.homes.Select(h=>oldRoute.Progress(h.standingPoint.position)).ToArray();
        var stations=original.physicalDecisions.stations.Where(st=>st.supplementary).ToDictionary(st=>st.index,st=>new Pose{position=st.transform.position,rotation=st.transform.rotation,home=Enumerable.Range(0,4).FirstOrDefault(i=>(st.homeMask&(1<<i))!=0),departure=st.departureEncounter});
        var passages=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SibucaoStormPassage>(true)).ToDictionary(p=>p.persistentId,p=>new Pose{position=p.transform.position,rotation=p.transform.rotation,home=p.home});
        EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(target);
        void Place(Transform t,Pose pose)
        {
            int home=Mathf.Clamp(pose.home,0,3);float d=oldRoute.Progress(pose.position);float nd=Mathf.Clamp01((d-starts[home])/Mathf.Max(1,oldRoute.length-starts[home]))*s.route.length;
            var rot=Heading(s.route,nd)*Quaternion.Inverse(Heading(oldRoute,d));var pos=s.route.Sample(nd)+rot*(pose.position-oldRoute.Sample(d));
            if(pose.departure){rot=s.homes[home].transform.rotation*Quaternion.Inverse(homeRot[home]);pos=s.homes[home].transform.position+rot*(pose.position-homePos[home]);}
            t.SetPositionAndRotation(pos,rot*pose.rotation);
        }
        foreach(var st in s.physicalDecisions.stations.Where(st=>st.supplementary)){Place(st.transform,stations[st.index]);st.routeDistance=s.route.Progress(st.transform.position);}
        foreach(var p in target.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SibucaoStormPassage>(true)))if(passages.TryGetValue(p.persistentId,out var pose))Place(p.transform,pose);
        EditorSceneManager.MarkSceneDirty(target);EditorSceneManager.SaveScene(target);return "Rebound "+stations.Count+" supplemental encounters and "+passages.Count+" physical passages to their local starting homes.";
    }
    public static string Inspect()
    {
        var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();var s=roots.SelectMany(g=>g.GetComponentsInChildren<SibucaoSession>(true)).Single();var profile=s.GetComponent<CoastalAreaProfile>();var issues=new List<string>();
        int missing=roots.Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));if(missing>0)issues.Add(missing+" missing scripts");
        var sections=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections;
        foreach(var section in sections){if(section.layouts.Length!=4)issues.Add("Missing home layouts "+section.name);if(section.leftSign&&section.leftSign.gameObject.activeSelf||section.rightSign&&section.rightSign.gameObject.activeSelf)issues.Add("Visible indicator");foreach(var layout in section.layouts)if(layout.leftPath.Length<2||layout.rightPath.Length<2)issues.Add("Incomplete branch path");}
        var prep=s.GetComponent<SibucaoPreparationBridge>();var homeRows=new List<object>();Physics.SyncTransforms();
        foreach(var h in s.homes)
        {
            var p=h.standingPoint.position;var hits=Physics.OverlapCapsule(p+Vector3.up*.5f,p+Vector3.up*1.5f,.25f,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(h.transform)&&!c.transform.IsChildOf(prep.content.transform)&&!c.GetComponentInParent<SibucaoPlayer>()).Select(c=>c.name).Distinct().ToArray();
            if(hits.Length>0)issues.Add(h.name+": starting position overlaps "+string.Join(", ",hits));
            homeRows.Add(new{name=h.name,position=new[]{p.x,p.y,p.z},overlaps=hits});
        }
        var report=new{scene=scene.path,profile=profile.areaName,length=s.route.length,homes=homeRows,coreSections=sections.Length,sideMissions=sections.Length*2,totalStations=s.physicalDecisions.stations.Length,passages=s.passages.passages.Length,missingScripts=missing,issues,savePrefix=s.SavePrefix,weatherSky=AssetDatabase.GetAssetPath(s.weather.stormSky),floodTerrain=AssetDatabase.GetAssetPath(s.GetComponent<FloodSimulationManager>().terrain),prepDoors=prep.houseDoors.Select(d=>d?AnimationUtility.CalculateTransformPath(d.transform,null):"NULL").ToArray()};
        string text=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);Directory.CreateDirectory("Reports/CoastalAreas");File.WriteAllText("Reports/CoastalAreas/"+profile.areaName+"-inspection.json",text);return text;
    }
}
