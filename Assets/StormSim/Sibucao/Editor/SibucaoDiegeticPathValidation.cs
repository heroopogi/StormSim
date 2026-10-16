using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using StormSim.Sibucao.Editor;
using Object=UnityEngine.Object;
public static partial class SibucaoDiegeticInstaller
{
    static float SwimLift(Vector3 p)
    {
        int x=Mathf.RoundToInt((p.x-terrain.origin.x)/terrain.cellSize),z=Mathf.RoundToInt((p.z-terrain.origin.y)/terrain.cellSize);
        if(x<0||z<0||x>=terrain.width||z>=terrain.height)return 0;
        int i=z*terrain.width+x;return Mathf.Max(0,terrain.ground[i]+terrain.initialDepth[i]-p.y-1.45f);
    }
    static bool PathClear(Vector3[] points)
    {
        for(int i=1;i<points.Length;i++)
        {
            Vector3 a=points[i-1],b=points[i],delta=b-a;float lift=Mathf.Max(SwimLift(a),SwimLift(b));
            if(Physics.CapsuleCastAll(a+Vector3.up*(.4f+lift),a+Vector3.up*(1.55f+lift),.3f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!Ignore(h.collider)))return false;
        }
        return true;
    }
    static SibucaoPhysicalOption[] repairQueue;static int repairIndex;
    public static string PathStatus="Idle";
    public static string RepairHazardPaths()
    {
        Context();repairQueue=s.physicalDecisions.stations.Where(st=>st.supplementary).SelectMany(st=>st.branches).Where(b=>b.mechanism!=PhysicalMechanism.Mantle).ToArray();repairIndex=0;
        File.WriteAllText("tmp/diegetic/path-validation.txt","");EditorApplication.update-=RepairTick;EditorApplication.update+=RepairTick;return "Validating "+repairQueue.Length+" ground/swim paths";
    }
    static void RepairTick()
    {
        if(repairIndex>=repairQueue.Length)
        {
            EditorApplication.update-=RepairTick;AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);PathStatus="COMPLETE";return;
        }
        var b=repairQueue[repairIndex++];string label=b.station.GetComponent<SibucaoDiegeticHazard>().siteId+" C"+b.choiceIndex;
        try
        {
            if(PathClear(b.path.Select(t=>t.position).ToArray())){File.AppendAllText("tmp/diegetic/path-validation.txt","CLEAR "+label+"\n");return;}
            var points=new SibucaoBranchPathBake(b.station,terrain,true).Route(b.path.Select(t=>b.station.transform.InverseTransformPoint(t.position)).ToArray());
            if(!PathClear(points.Select(p=>b.station.transform.TransformPoint(p)).ToArray()))throw new Exception("Rebuilt path still fails capsule sweep");
            foreach(var point in b.path)Object.DestroyImmediate(point.gameObject);b.path=points.Select((p,i)=>Node(b.transform,"Physical step "+i,p)).ToArray();
            if(b.mechanism!=PhysicalMechanism.TalkThenWalk)b.actionPoint.position=b.path[0].position+Vector3.up;
            b.destination.transform.position=b.path.Last().position+Vector3.up*1.5f;
            File.AppendAllText("tmp/diegetic/path-validation.txt","REPAIRED "+label+" "+b.PathLength.ToString("F1")+"m\n");
        }
        catch(Exception e){File.AppendAllText("tmp/diegetic/path-validation.txt","FAIL "+label+" "+e.Message+"\n");}
        finally{PathStatus="Checked "+repairIndex+"/"+repairQueue.Length;}
    }
    public static string RemoveRouteBacktracking()
    {
        Context();var lines=new System.Collections.Generic.List<string>();
        foreach(var st in s.physicalDecisions.stations.Where(st=>st.supplementary))foreach(var b in st.branches.Where(b=>b.mechanism!=PhysicalMechanism.Mantle))
        {
            var retained=new System.Collections.Generic.List<Transform>();
            foreach(var point in b.path)
            {
                int duplicate=retained.FindIndex(p=>Vector3.Distance(p.position,point.position)<.08f);
                if(duplicate>=0)retained.RemoveRange(duplicate,retained.Count-duplicate);
                retained.Add(point);
            }
            if(retained.Count<2||retained.Count==b.path.Length)continue;
            float before=b.PathLength;foreach(var p in b.path)if(!retained.Contains(p))Object.DestroyImmediate(p.gameObject);b.path=retained.ToArray();
            lines.Add(st.GetComponent<SibucaoDiegeticHazard>().siteId+" C"+b.choiceIndex+" "+before.ToString("F1")+" -> "+b.PathLength.ToString("F1")+"m; repeated intersection loops removed");
        }
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);File.WriteAllLines("tmp/diegetic/path-backtracking.txt",lines);return string.Join("\n",lines);
    }
}
