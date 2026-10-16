#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Logical integration fixtures, explicitly separate from complete-route locomotion evidence.
    public sealed class SibucaoBranchingStateProbe:MonoBehaviour
    {
        [Serializable] public sealed class Row{public string test,detail;public int repeat;public bool pass;public float beforeSeconds,afterSeconds;}
        [Serializable] public sealed class Report{public string scope="Play Mode state integration. Fixtures set completed physical-attempt inputs and teleport between tests; not full route playthroughs. Timing uses actual CharacterController input on a fixed dry school path.";public List<Row> trials=new List<Row>();}
        public string status="Idle";public Report report=new Report();SibucaoSession S=>SibucaoSession.Instance;
        public static SibucaoBranchingStateProbe Get(){var p=FindFirstObjectByType<SibucaoBranchingStateProbe>();return p?p:new GameObject("EDITOR ONLY branching state evaluation").AddComponent<SibucaoBranchingStateProbe>();}
        public void Run(){StartCoroutine(Guard(Suite()));}
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0){object value=null;bool more=false;Exception failure=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){status="FAILED: "+failure.Message;report.trials.Add(new Row{test=status,pass=false});break;}if(!more){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;}
            SibucaoEditorInput.Reset();Save();if(!status.StartsWith("FAILED"))status="Finished: "+report.trials.Count+" checks; "+report.trials.Count(x=>!x.pass)+" failures";
        }
        void Save(){Directory.CreateDirectory("Documentation/BranchingEvaluation");File.WriteAllText("Documentation/BranchingEvaluation/play-state-integration.json",JsonUtility.ToJson(report,true));}
        void Check(string name,int repeat,bool pass,string detail){report.trials.Add(new Row{test=name,repeat=repeat,pass=pass,detail=detail});Save();}
        void Fixture(string role,int seed=23421)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartWithRole(role);var bridge=S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();if(bridge&&bridge.Active){bridge.preparation.departure="temporary-skip";bridge.preparation.SetPhase(StormSim.Preparedness.PrepPhase.ReadyForStorm);bridge.BeginStorm();}if(S.home)S.home.SkipTV();S.state.homeSeconds=90;S.state.physical.seed=seed;
            foreach(var st in S.physicalDecisions.stations)S.state.physical.results[st.index]=98;
        }
        bool Finish(int n,int c)
        {
            var m=S.physicalDecisions;var st=m.stations[n];m.Data.results[n]=-1;m.Data.started[n]=0;
            var a=m.Branch(n);a.selected=c;a.finished=a.engaged=a.actionStarted=true;a.activeSeconds=200;a.distance=200;a.step=st.branches[c].path.Length;
            return m.FinishPhysicalChoice(st,c);
        }
        IEnumerator Suite()
        {
            Application.runInBackground=true;
            for(int rep=1;rep<=5;rep++)
            {
                status="State integration repetition "+rep;
                foreach(var role in new[]{"Student","Parent","Volunteer"})
                {
                    Fixture(role);yield return null;
                    Check("role-selection-to-run",rep,S.state.physical.role==role&&S.physicalDecisions.playerRole==role,"Selected "+role+" -> active "+S.state.physical.role);
                    Finish(6,0);var r=S.state.physical.history.Last();Check("B7-role-resolution-"+role,rep,r.rawPoints==(role=="Volunteer"?42:39),JsonUtility.ToJson(r));
                }
                Fixture("Parent");yield return null;
                Finish(5,2);Finish(7,2);S.physicalDecisions.ApplyPending(8);
                int count=S.state.physical.consequenceNotices.Count(n=>n.id.StartsWith("resident_left"));
                S.physicalDecisions.ApplyPending(8);
                Check("both-residents-at-B9-once",rep,count==2&&S.state.physical.consequenceNotices.Count(n=>n.id.StartsWith("resident_left"))==2,JsonUtility.ToJson(S.state.physical));
                var roundtrip=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(S.state));S.state=roundtrip;S.physicalDecisions.ApplyPending(9);
                Check("delayed-notices-save-restore",rep,S.state.physical.consequenceNotices.Count==2,"Two resident notices remain, no duplicate application");
                S.player.Teleport(S.physicalDecisions.evacuationCenter.arrivalPoint.position,Vector3.forward);S.physicalDecisions.CheckEndDestination();
                Check("residents-arrival-report-required",rep,S.Mode==SibucaoMode.ArrivalReview&&!S.state.physical.boundaryReached,S.Mode.ToString());
                S.physicalDecisions.ConfirmArrivalReport();Check("residents-report-completes",rep,S.Mode==SibucaoMode.Complete&&S.state.physical.arrivalReported,S.Mode.ToString());
                Fixture("Volunteer");yield return null;Finish(6,1);S.physicalDecisions.ApplyPending(8);
                Check("boat-not-early",rep,!S.state.physical.flags.Contains("arrival_manifest_mismatch"),"B9 does not settle an arrival-only manifest");
                S.player.Teleport(S.physicalDecisions.evacuationCenter.arrivalPoint.position,Vector3.forward);S.physicalDecisions.CheckEndDestination();
                Check("boat-arrival-observable",rep,S.Mode==SibucaoMode.ArrivalReview&&S.state.physical.consequenceNotices.Any(n=>n.id=="boat_manifest"),JsonUtility.ToJson(S.state.physical.consequenceNotices.FirstOrDefault()));
                S.physicalDecisions.ConfirmArrivalReport();Check("boat-report-completes",rep,S.state.physical.boundaryReached&&S.state.physical.arrivalReported,S.Mode.ToString());
                Fixture("Student");yield return null;
                foreach(var item in S.home.GetComponentsInChildren<PhysicalGrabbable>(true).OrderBy(i=>i.kind==PhysicalItemKind.Bag?0:1))if(item.kind==PhysicalItemKind.Bag||SibucaoInventory.Storable(item))S.inventory.Collect(item);
                var kit=Enumerable.Range(0,SibucaoInventory.Capacity).Select(S.inventory.Slot).FirstOrDefault(i=>i&&i.inventoryTag=="first_aid");
                if(kit){S.inventory.RemoveTag("first_aid",false);kit.delivered=false;bool collected=S.inventory.Collect(kit);Check("consumed-kit-cannot-return",rep,!collected&&!S.inventory.HasTag("first_aid")&&!S.physicalDecisions.ChoiceAvailable(S.physicalDecisions.stations[7],3),"Consumed persistent item rejected by collect, tag query and B8 availability");S.inventory.RestoreInventory();Check("consumed-kit-after-restore",rep,!S.inventory.HasTag("first_aid"),"Restore does not revive consumed supply");}else Check("kit-fixture",rep,false,"Selected home has no first-aid kit");
            }
            yield return Timing();
        }
        IEnumerator Timing()
        {
            var input=SibucaoPlaythroughProbe.Get();var m=S.physicalDecisions;
            var start=m.evacuationCenter.arrivalPoint.position+Vector3.right*12+Vector3.forward*12;
            var end=start+Vector3.forward*8;
            float baseline=0;
            for(int condition=0;condition<2;condition++)
            {
                Fixture("Student");yield return null;
                if(condition==1)
                {
                    var st=m.stations[3];int seed=1;while(SibucaoDecisionEvaluation.Evaluate(st.node.choices[2],"Student",seed,3,2,WaterTier.Tier1,0).success)seed++;
                    m.Data.seed=seed;Finish(3,2);m.ApplyPending(4);S.player.GetComponent<SibucaoPhysicalFeedback>().Clear();
                }
                S.player.Teleport(start,Vector3.forward);yield return new WaitForSeconds(.15f);float began=Time.time;
                yield return input.MoveTo(end,30);float elapsed=Time.time-began;
                if(condition==0)baseline=elapsed;
                else{report.trials.Add(new Row{test="B4-injury-later-traversal-time",repeat=1,pass=elapsed>baseline*1.15f,beforeSeconds=baseline,afterSeconds=elapsed,detail="Same 8m dry school corridor, normal WASD CharacterController; B4 failure + delayed swelling applied before second traversal. This is a traversal fixture, not a full route."});Save();}
            }
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
