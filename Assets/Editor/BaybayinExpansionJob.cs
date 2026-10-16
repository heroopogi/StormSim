using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using CesiumForUnity;
using UnityEngine.Splines;
using Unity.Mathematics;
using Newtonsoft.Json.Linq;

/// <summary>One-shot, explicitly requested editor build, processed in small batches.</summary>
[InitializeOnLoad]
public static class BaybayinExpansionJob
{
    const string Request="Temp/baybayin-build.request";
    const string Report="RoadRepairBackup/BaybayinExpansion/job-status.txt";
    static int phase;
    static double nextTick;
    static BaybayinExpansionJob(){EditorApplication.update+=Tick;}
    [MenuItem("Tools/StormSim/Review Baybayin coast")]
    public static void ReviewCoast(){Review(new Vector3(190,-2,-880),210);}
    [MenuItem("Tools/StormSim/Review port and river")]
    public static void ReviewPort(){Review(new Vector3(-110,-2,-45),210);}
    static void Review(Vector3 center,float size)
    {
        var sv=SceneView.lastActiveSceneView;if(!sv)return;
        var property=typeof(SceneView).GetProperty("overrideSceneCullingMask",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if(property!=null)property.SetValue(sv,(ulong)0);
        sv.sceneViewState.showFog=false;sv.LookAt(center,Quaternion.Euler(65,85,0),size,false);sv.Focus();sv.Repaint();
    }
    static void Write(string s){File.WriteAllText(Report,DateTime.Now.ToString("s")+" "+s);}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying||EditorApplication.timeSinceStartup<nextTick)return;
        nextTick=EditorApplication.timeSinceStartup+.15;
        try
        {
            if(phase==0)
            {
                if(!File.Exists(Request))return;
                File.Move(Request,Request+".started");
                Write("Starting");BaybayinExpansionBuilder.Begin();phase=1;
            }
            else if(phase==1)
            {
                var r=JObject.FromObject(BaybayinExpansionBuilder.Step(60));
                Write(BaybayinExpansionBuilder.Status);if((bool)r["done"])phase=2;
            }
            else if(phase==2)
            {
                BaybayinExpansionBuilder.Plant(500);ConfigureTerrain();
                Write("Saving and validating");var r=BaybayinExpansionBuilder.Finish();
                Write(Newtonsoft.Json.JsonConvert.SerializeObject(r));phase=3;
                var sv=SceneView.lastActiveSceneView;
                if(sv){sv.LookAt(new Vector3(195,0,-893),Quaternion.Euler(68,80,0),260,false);sv.Repaint();}
                Debug.Log("Baybayin expansion completed and saved.");
            }
        }
        catch(Exception e){Write("ERROR "+e);phase=-1;Debug.LogException(e);}
    }
    public static void ConfigureTerrain()
    {
        var root=GameObject.Find(BaybayinExpansionBuilder.RootName);
        var geo=UnityEngine.Object.FindFirstObjectByType<CesiumGeoreference>();
        root.transform.SetParent(geo.transform,true);
        var overlay=geo.GetComponentsInChildren<CesiumPolygonRasterOverlay>().First(o=>o.name=="Cesium World Terrain");
        var info=root.GetComponent<BaybayinExpansionInfo>();
        info.terrainOverlay=overlay;info.originalTerrainAreas=overlay.polygons.ToArray();
        var result=overlay.polygons.Where(p=>p.name!="South extension").ToList();
        var group=new GameObject("Terrain boundary");group.transform.SetParent(root.transform,false);
        var areas=JArray.Parse(File.ReadAllText(BaybayinExpansionBuilder.Source+"/remaining-terrain.json"));
        for(int i=0;i<areas.Count;i++)
        {
            var g=new GameObject("Surrounding terrain "+i);g.transform.SetParent(group.transform,false);
            var polygon=g.AddComponent<CesiumCartographicPolygon>();var container=g.GetComponent<SplineContainer>();
            foreach(var s in container.Splines.ToArray())container.RemoveSpline(s);
            var spline=new Spline();spline.Knots=areas[i].Select(p=>new BezierKnot(new float3((float)p[0],-68.2f,(float)p[1]))).ToArray();
            spline.Closed=true;spline.SetTangentMode(TangentMode.Linear);container.AddSpline(spline);result.Add(polygon);
        }
        info.expandedTerrainAreas=result.ToArray();overlay.polygons=result;EditorUtility.SetDirty(info);EditorUtility.SetDirty(overlay);
    }
}
