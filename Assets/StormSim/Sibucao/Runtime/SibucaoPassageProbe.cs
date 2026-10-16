#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Fixture placement is explicit. All gate work uses the same E/movement input,
    // raycast, collision and saved-state code as the desktop player.
    public sealed class SibucaoPassageProbe:MonoBehaviour
    {
        public string status="Idle";
        public List<string> results=new List<string>();
        SibucaoSession S=>SibucaoSession.Instance;
        SibucaoPlaythroughProbe input;
        public static SibucaoPassageProbe Get(){var p=FindFirstObjectByType<SibucaoPassageProbe>();return p?p:new GameObject("EDITOR ONLY storm passage checks").AddComponent<SibucaoPassageProbe>();}
        void Awake(){if(System.IO.File.Exists("tmp/passages/checks.txt"))results.AddRange(System.IO.File.ReadAllLines("tmp/passages/checks.txt").Skip(2));}
        void Save(){System.IO.Directory.CreateDirectory("tmp/passages");System.IO.File.WriteAllLines("tmp/passages/checks.txt",new[]{"Automated production-input Play Mode checks; fixture teleports are labelled. "+DateTime.Now,status}.Concat(results));}
        void Check(bool condition,string text){if(!condition)throw new Exception(text);}
        void Record(string text){results.Add(text);Save();}
        public void Run(string mode="gate",int home=0,int zone=0,int choice=1,int kind=-1)
        {StopAllCoroutines();input=SibucaoPlaythroughProbe.Get();Application.runInBackground=true;StartCoroutine(Guard(mode,Case(mode,home,zone,choice,kind)));}
        IEnumerator Guard(string label,IEnumerator work)
        {
            status=label;Save();var stack=new Stack<IEnumerator>();stack.Push(work);Exception error=null;
            while(stack.Count>0)
            {
                bool next=false;object current=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
            SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.UseHeld=SibucaoEditorInput.GripHeld=SibucaoEditorInput.Sprint=false;
            status=(error==null?"PASS ":"FAIL ")+label+(error==null?"":": "+error.GetBaseException().Message);Record(status);
        }
        IEnumerator Fixture(int home,int zone)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartRun();
            S.home.Suspend();S.home=S.homes[home];S.state.homeIndex=home;S.home.Begin();S.physicalDecisions.ResetRun();
            S.state.homeSeconds=90;S.home.Restore();S.state.physical.seed=23421;
            foreach(var st in S.physicalDecisions.stations)if(st.index!=zone)S.state.physical.results[st.index]=99;
            foreach(var item in S.home.GetComponentsInChildren<PhysicalGrabbable>(true).OrderBy(i=>i.kind==PhysicalItemKind.Bag?0:1))if(item.kind==PhysicalItemKind.Bag||SibucaoInventory.Storable(item))S.inventory.Collect(item);
            yield return null;while(S.GetComponent<FloodSimulationManager>().IsCatchingUp)yield return null;yield return new WaitForSeconds(.4f);
        }
        IEnumerator Tap(SibucaoPassageHandle handle)
        {
            yield return input.Aim(handle.GetComponent<Collider>().bounds.center);yield return null;
            Check(S.passages.Focused==handle,"Handle not reachable by gaze: "+handle.name+" at "+handle.transform.position+" focus="+S.passages.Focused);
            SibucaoEditorInput.Interact=true;yield return null;yield return null;
        }
        IEnumerator Move(Vector3 target,float timeout=18)
        {
            var delta=target-S.player.transform.position;delta.y=0;
            if(delta.sqrMagnitude>.02f)yield return input.Aim(target+Vector3.up*1.2f);
            yield return input.MoveTo(target,timeout);
        }
        IEnumerator Gate(SibucaoStormPassage p)
        {
            status="Testing "+p.persistentId+" "+p.kind;Save();
            var start=p.transform.TransformPoint(p.approachLocal);
            S.player.Teleport(start,p.transform.forward);yield return new WaitForSeconds(.4f);
            yield return input.Aim(p.transform.position+Vector3.up*1.1f);
            ScreenCapture.CaptureScreenshot("tmp/passages/"+p.persistentId+"_closed.png");yield return new WaitForEndOfFrame();
            // Collision proof: a player cannot simply walk through the closed opening.
            SibucaoEditorInput.Move=Vector2.up;yield return new WaitForSeconds(.5f);SibucaoEditorInput.Move=Vector2.zero;
            Check(p.transform.InverseTransformPoint(S.player.transform.position).z<-.2f,"Closed gate did not block player");
            yield return Move(start);
            yield return Tap(p.grip);yield return new WaitForSeconds(.4f);
            Check(!S.passages.Held&&p.State.opening==0&&!p.State.secured,"One E bypassed tied fasteners");
            for(int i=0;i<2;i++)
            {
                yield return Tap(p.fasteners[i].GetComponent<SibucaoPassageHandle>());
                Check((p.State.fasteners&(1<<i))!=0,"Fastener input was not recorded");
            }
            yield return Tap(p.grip);Check(S.passages.Held==p,"Handle did not engage");
            SibucaoEditorInput.UseHeld=true;yield return new WaitForSeconds(1);
            Check(p.State.opening<.01f&&!p.State.secured&&!p.State.crossed,"Holding E without movement advanced obstacle");
            var workStart=S.player.transform.position;
            if(p.kind==StormPassageKind.SwingFence)
            {
                yield return Move(workStart-p.transform.forward*2.8f,18);
            }
            else if(p.kind==StormPassageKind.PushCart)
                yield return Move(workStart+p.transform.right*(p.width+1.4f),18);
            else yield return Move(workStart-p.transform.forward*2.8f,18);
            SibucaoEditorInput.UseHeld=false;
            Check(p.State.opening>=.98f,"Movement did not fully open obstacle: "+p.State.opening.ToString("F3"));
            Check(!p.State.secured&&!p.State.crossed,"Opening without securing/crossing awarded passage");
            SibucaoEditorInput.Interact=true;yield return null;yield return null;
            Check(p.State.secured&&!S.passages.Held,"Final E did not secure/release obstacle");
            yield return Move(p.transform.TransformPoint(p.kind==StormPassageKind.SwingFence?p.width*.3f:0,.1f,-.65f));
            yield return Move(p.transform.TransformPoint(0,.1f,-.5f));
            yield return Move(p.transform.TransformPoint(0,.1f,1.35f));yield return null;
            Check(p.State.crossed&&S.passages.WorkDone(p.zone),"Physically crossing cleared gate did not record work");
            Check(p.State.distance>1.5f&&p.State.workSeconds>1,"Insufficient actual movement recorded");
            Record("PASS "+p.persistentId+" "+p.kind+": closed collision; two separate E fasteners; E+idle no progress; hold E + "+p.State.distance.ToString("F2")+"m movement; separate secure; physical crossing. Work="+p.State.workSeconds.ToString("F2")+"s.");
            yield return input.Aim(p.transform.position+Vector3.up*1.1f);
            ScreenCapture.CaptureScreenshot("tmp/passages/"+p.persistentId+"_opened.png");yield return new WaitForEndOfFrame();
        }
        IEnumerator Case(string mode,int home,int zone,int choice,int kind)
        {
            yield return Fixture(home,zone);Check(S.passages&&S.passages.Installed,"No installed passage layer");
            var st=S.physicalDecisions.stations[zone];var branch=st.branches[choice];
            if(mode!="existing")
            {
                var candidates=S.passages.passages.Where(p=>p.home==home&&p.zone==zone&&p.canOperate&&(kind<0?p.core:(int)p.kind==kind));
                var gate=candidates.OrderBy(p=>(p.transform.position-branch.path[0].position).sqrMagnitude).FirstOrDefault();
                Check(gate,"No requested gate H"+home+" B"+(zone+1)+" kind="+kind);
                yield return Gate(gate);
                if(mode=="npc")
                {
                    string opened=JsonUtility.ToJson(S.state.physical);
                    // Closed/open fixtures isolate the resident response after the gate
                    // itself has already passed the production-input sequence above.
                    gate.State.secured=gate.State.crossed=false;gate.State.opening=0;gate.State.fasteners=0;gate.Apply();
                    var resident=FindObjectsByType<SibucaoResident>().First(r=>r.enabled&&r.casting);
                    resident.distributeFromCurrentHome=false;resident.initialDistance=S.route.Progress(gate.transform.position)-2;
                    resident.ResetResident();resident.transform.position=gate.transform.TransformPoint(0,.03f,-2.5f);resident.routeDistance=S.route.Progress(resident.transform.position);resident.speed=0;
                    S.player.Teleport(gate.transform.TransformPoint(0,.1f,-5),gate.transform.forward);
                    yield return input.Aim(resident.transform.position+Vector3.up*1.2f);yield return new WaitForSeconds(3);
                    Check(gate.transform.InverseTransformPoint(resident.transform.position).z<-.5f,"Resident walked through closed obstruction");
                    var waiting=resident.transform.position;ScreenCapture.CaptureScreenshot("tmp/passages/resident_waiting.png");yield return new WaitForEndOfFrame();
                    S.state.physical=JsonUtility.FromJson<PhysicalRunData>(opened);S.passages.Restore();
                    yield return new WaitForSeconds(7);
                    Check(gate.transform.InverseTransformPoint(resident.transform.position).z>.9f&&Vector3.Distance(waiting,resident.transform.position)>2,"Resident did not use cleared opening: "+resident.transform.position);
                    ScreenCapture.CaptureScreenshot("tmp/passages/resident_crossing.png");yield return new WaitForEndOfFrame();
                    Record("PASS resident waited behind a closed passage and walked through the same opening after restoration of the input-cleared state; real imported actor "+resident.name+".");yield break;
                }
                if(mode=="persistence")
                {
                    string before=JsonUtility.ToJson(S.state.physical);float amount=gate.State.opening;
                    S.state.physical=JsonUtility.FromJson<PhysicalRunData>(before);S.physicalDecisions.RestoreRun();yield return null;
                    Check(gate.State.secured&&gate.State.crossed&&gate.State.opening==amount&&S.passages.WorkDone(zone),"Saved passage did not restore");
                    Check(!S.passages.Held,"Save restoration retained a stale held handle");
                    S.physicalDecisions.ResetRun();yield return null;
                    Check(!gate.State.secured&&!gate.State.crossed&&gate.State.fasteners==0&&gate.State.opening==0&&!S.passages.WorkDone(zone),"New run did not reset obstruction");
                    Record("PASS passage save-data round trip restored geometry and work; new run reset fasteners, motion and completion. No user save file overwritten.");yield break;
                }
                if(mode=="gate")yield break;
            }
            Check(!st.Resolved,"Obstacle work alone resolved whole station");
            if(zone>0&&st.EngagedBranch&&st.EngagedBranch!=branch)
            {
                // The gate and the authored branch are separate fixtures here.
                // Cancel a different route selected by the gate's exit position.
                SibucaoEditorInput.Cancel=true;yield return null;yield return null;
                S.player.Teleport(branch.path[0].position-st.transform.forward*3+Vector3.up*.1f,st.transform.forward);yield return null;
            }
            if(zone==0)Check(st.Active&&!st.HasVisibleWater&&!st.branches[0].Available,"Dry B1 activation/probe gating failed");
            if(zone==0)
            {
                // Continue on foot from the cleared opening for the user's failing task.
                SibucaoEditorInput.Sprint=choice==2;
                yield return Move(branch.path[0].position);
                foreach(var point in branch.path)yield return Move(point.position,25);
                yield return Move(branch.path.Last().position+st.transform.forward,25);
            }
            else
            {
                var old=SibucaoBranchPlayProbe.Get();typeof(SibucaoBranchPlayProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(old,input);
                yield return (IEnumerator)typeof(SibucaoBranchPlayProbe).GetMethod("Exercise",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(old,new object[]{branch});
            }
            Check(st.Resolved&&S.state.physical.history.Count(r=>r.node==zone)==1,"Full action did not resolve exactly once");
            if(zone<8){S.state.physical.results[zone+1]=-1;Check(S.missionGuide.NextStation.index==zone+1,"Objective did not advance to the next unresolved situation");}
            Record("PASS H"+home+" B"+(zone+1)+" C"+choice+": passage work plus authored action resolved once; "+S.passages.LastCompletion);
            float deadline=Time.realtimeSinceStartup+8;while(S.story.IsPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(S.Mode==SibucaoMode.Playing,"Gameplay did not resume");
            Check(!st.EngagedBranch,"Completed action still owns movement");
            yield return null;Check(!S.player.ActivePhysicalOption,"Completed action retains movement control");
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
