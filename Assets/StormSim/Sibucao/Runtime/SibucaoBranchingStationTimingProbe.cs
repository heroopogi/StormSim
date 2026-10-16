#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Paired physical traversal fixtures. Earlier injury is resolved through production logic;
    // only setup teleports. The later B6-C2 decision must complete from movement input.
    public sealed class SibucaoBranchingStationTimingProbe:MonoBehaviour
    {
        [Serializable] public sealed class Trial
        {
            public int repetition,condition,seed;public string role="Student",choice="B6-C2";
            public float seconds,activeSeconds,distance,speedMultiplier,stamina,waterDepth;
            public bool resolved;
        }
        [Serializable] public sealed class Report
        {
            public string scope="Five paired B6-C2 physical completion tests, Home4. Baseline versus B4-C2 failure + delayed swelling. Earlier decision is a completed-attempt fixture; later traversal uses production movement. No flood/terrain edits.";
            public string status;public List<Trial> trials=new List<Trial>();
        }
        public string status="Idle";Report report=new Report();SibucaoSession S=>SibucaoSession.Instance;
        public static SibucaoBranchingStationTimingProbe Get(){var p=FindFirstObjectByType<SibucaoBranchingStationTimingProbe>();return p?p:new GameObject("EDITOR ONLY paired station timing").AddComponent<SibucaoBranchingStationTimingProbe>();}
        public void Run(){StartCoroutine(Guard());}
        void Save(){report.status=status;File.WriteAllText("Documentation/BranchingEvaluation/later-station-timing.json",JsonUtility.ToJson(report,true));}
        IEnumerator Guard()
        {
            var homes=S.homes;var stack=new Stack<IEnumerator>();stack.Push(Suite());Exception failure=null;
            while(stack.Count>0)
            {
                object value=null;bool more=false;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){failure=e;break;}
                if(!more){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
            S.homes=homes;SibucaoEditorInput.Reset();status=failure==null?"Finished: "+report.trials.Count+" station traversals":"FAIL: "+failure.Message;Save();
        }
        IEnumerator Suite()
        {
            var homes=S.homes;var input=SibucaoBranchPlayProbe.Get();S.homes=new[]{homes[3]};Application.runInBackground=true;
            for(int repetition=1;repetition<=5;repetition++)for(int condition=0;condition<2;condition++)
            {
                status="Pair "+repetition+", "+(condition==0?"baseline":"earlier injury");
                SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartWithRole("Student");S.state.homeIndex=3;S.physicalDecisions.ResetRun();
                S.state.homeSeconds=90;var m=S.physicalDecisions;m.Data.seed=10;
                foreach(var st in m.stations)m.Data.results[st.index]=98;
                yield return null;
                if(condition==1)
                {
                    var st=m.stations[3];m.Data.results[3]=-1;var a=m.Branch(3);
                    a.selected=2;a.engaged=a.finished=a.actionStarted=true;a.activeSeconds=200;a.distance=200;a.step=st.branches[2].path.Length;
                    if(!m.FinishPhysicalChoice(st,2))throw new Exception("Earlier injury fixture did not resolve");
                    m.ApplyPending(4);S.player.GetComponent<SibucaoPhysicalFeedback>().Clear();
                    if(!m.Data.consequenceNotices.Exists(n=>n.id=="swelling_3"))throw new Exception("Expected swelling absent");
                }
                m.Data.results[5]=-1;var branch=m.stations[5].branches[2];
                S.player.Teleport(branch.path[0].position+Vector3.up*.1f,m.stations[5].transform.forward);
                yield return new WaitForSeconds(.4f);
                var row=new Trial{repetition=repetition,condition=condition,seed=m.Data.seed,speedMultiplier=S.state.speedMultiplier,stamina=m.Data.stamina,waterDepth=S.GetComponent<FloodSimulationManager>().GetWaterDepth(S.player.transform.position)};
                float start=Time.time;yield return input.ExerciseExisting(branch,false);
                row.seconds=Time.time-start;row.activeSeconds=branch.Attempt.activeSeconds;row.distance=branch.Attempt.distance;row.resolved=m.Data.results[5]==2;
                report.trials.Add(row);Save();
                while(S.Mode==SibucaoMode.Cinematic)yield return null;
            }
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
