#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Fixture setup teleports between cases. Traversal uses production keyboard/mouse input,
    // CharacterController collisions and the physical hand joint. Never calls a branch resolver.
    public sealed class SibucaoBranchPlayProbe:MonoBehaviour
    {
        public string status="Idle";public List<string> results=new List<string>();
        public int seed=23421;
        SibucaoSession S=>SibucaoSession.Instance;SibucaoPlaythroughProbe input;
        public static SibucaoBranchPlayProbe Get(){var p=FindFirstObjectByType<SibucaoBranchPlayProbe>();return p?p:new GameObject("EDITOR ONLY every branch verification").AddComponent<SibucaoBranchPlayProbe>();}
        void Awake(){if(System.IO.File.Exists("Documentation/SIBUCAO_BRANCH_PLAY_PROBE.txt"))results.AddRange(System.IO.File.ReadAllLines("Documentation/SIBUCAO_BRANCH_PLAY_PROBE.txt").Skip(1));}
        void OnDestroy(){SibucaoEditorInput.Reset();}
        public void Run(int node=-1,int choice=-1){StopAllCoroutines();StartCoroutine(Suite(node,choice));}
        void Save(){System.IO.File.WriteAllLines("Documentation/SIBUCAO_BRANCH_PLAY_PROBE.txt",new[]{"Production input automation in Play Mode; not native/manual input. "+DateTime.Now}.Concat(results));}
        IEnumerator Suite(int node,int choice)
        {
            int runStart=results.Count;input=SibucaoPlaythroughProbe.Get();
            var homes=FindObjectsByType<SibucaoHomeOpening>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(h=>h.name).ToArray();
            int homeIndex=node>=9?Enumerable.Range(0,homes.Length).First(h=>(S.physicalDecisions.stations[node].homeMask&(1<<h))!=0):0;
            S.homes=new[]{homes[homeIndex]};Application.runInBackground=true;
            foreach(var n in node>=0?new[]{node}:new[]{1,0,2,3,4,5,6,7,8})
            foreach(var c in choice>=0?new[]{choice}:Enumerable.Range(0,S.physicalDecisions.stations[n].branches.Length))
            {
                status="B"+(n+1)+" C"+c+" starting";SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartRun();var bridge=S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();if(bridge&&bridge.Active){bridge.preparation.departure="temporary-skip";bridge.preparation.SetPhase(StormSim.Preparedness.PrepPhase.ReadyForStorm);bridge.BeginStorm();}if(S.home)S.home.SkipTV();S.state.homeIndex=homeIndex;S.physicalDecisions.ResetRun();S.state.homeSeconds=90;S.state.physical.seed=seed;
                foreach(var st in S.physicalDecisions.stations)if(st.index!=n)S.state.physical.results[st.index]=99;
                // Inventory is a case fixture. Routine E pickup has its own independent regression.
                foreach(var item in S.home.GetComponentsInChildren<PhysicalGrabbable>(true).OrderBy(i=>i.kind==PhysicalItemKind.Bag?0:1))if(item.kind==PhysicalItemKind.Bag||SibucaoInventory.Storable(item))S.inventory.Collect(item);
                yield return null;yield return new WaitForFixedUpdate();
                var branch=S.physicalDecisions.stations[n].branches[c];
                if(branch.mechanism==PhysicalMechanism.SwimPassage)
                {
                    S.state.homeSeconds=SibucaoStormTimeline.FloodStart(S.home)+200;
                    yield return null;var flood=S.GetComponent<FloodSimulationManager>();while(flood.IsCatchingUp)yield return null;
                }
                Exception error=null;var work=Exercise(branch);var stack=new Stack<IEnumerator>();stack.Push(work);
                while(stack.Count>0)
                {
                    object value=null;bool next=false;
                    try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;break;}
                    if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
                }
                SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.GripHeld=SibucaoEditorInput.UseHeld=SibucaoEditorInput.Ascend=false;
                if(error!=null)results.Add("FAIL B"+(n+1)+" C"+c+": "+error.Message);
                else
                {
                    var r=S.state.physical.history.Last();results.Add("PASS B"+(n+1)+" C"+c+": "+JsonUtility.ToJson(r));
                    if(n<8)
                    {
                        var nextStation=S.physicalDecisions.stations[n+1];S.state.physical.results[n+1]=-1;float health=S.state.physical.health,stamina=S.state.physical.stamina,clock=S.state.homeSeconds;string lost=r.lostItem;
                        S.player.Teleport(nextStation.transform.position-nextStation.transform.forward*5+Vector3.up*.1f,nextStation.transform.forward);yield return new WaitForSeconds(.5f);
                        bool retained=S.state.homeSeconds>clock&&S.state.physical.health<=health&&S.state.physical.stamina<=stamina&&(string.IsNullOrEmpty(lost)||S.state.physical.flags.Contains("lost:"+lost)&&!S.state.physical.inventorySlots.Contains(lost));
                        if(r.lostItems!=null)retained&=r.lostItems.All(id=>S.state.physical.flags.Contains("lost:"+id)&&!S.state.physical.inventorySlots.Contains(id));
                        results.Add((retained?"PASS":"FAIL")+" B"+(n+1)+" persistence at B"+(n+2)+" clock="+S.state.homeSeconds.ToString("F1")+" health="+S.state.physical.health+" stamina="+S.state.physical.stamina);
                    }
                }
                Save();
            }
            status="Finished: "+results.Skip(runStart).Count(x=>x.StartsWith("FAIL"))+" failures";Save();
        }
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        IEnumerator TapE(){SibucaoEditorInput.Interact=true;yield return null;yield return null;}
        IEnumerator Grip(PhysicalGrabbable item)
        {
            var collider=item.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger&&c.enabled);
            yield return input.Aim(collider.bounds.center);S.player.grabber.RefreshFocus(out _);
            Check(S.player.grabber.FocusedItem==item,"Grip ray blocked: "+item.name+" by "+S.player.grabber.FocusedItem);
            SibucaoEditorInput.GripDown=SibucaoEditorInput.GripHeld=true;yield return null;yield return new WaitForFixedUpdate();Check(S.player.grabber.held==item,"Physical grip did not engage "+item.name);
        }
        public IEnumerator ExerciseExisting(SibucaoPhysicalOption b,bool teleportToEntry=true)
        {
            input=SibucaoPlaythroughProbe.Get();return Exercise(b,teleportToEntry);
        }
        IEnumerator Exercise(SibucaoPhysicalOption b,bool teleportToEntry=true)
        {
            Check(b.path!=null&&b.path.Length>1,"No physical path");var st=b.station;
            Vector3 entry=b.path[0].position;
            if(b.mechanism==PhysicalMechanism.RepairStrap||b.mechanism==PhysicalMechanism.ProbeThenWalk)entry=b.tool.transform.position-st.transform.forward*1.2f;
            else if(b.mechanism==PhysicalMechanism.CarryResident||b.mechanism==PhysicalMechanism.TreatThenCarry)entry=b.subject.transform.position-st.transform.forward*1.25f;
            else if(b.mechanism==PhysicalMechanism.TalkThenWalk||b.mechanism==PhysicalMechanism.SignalHelp||b.mechanism==PhysicalMechanism.GuideResident||b.mechanism==PhysicalMechanism.FlashlightPassage)entry=b.actionPoint.position-st.transform.forward*1.4f-Vector3.up;
            entry.y=st.InteractionPosition.y+.12f;
            if(b.mechanism==PhysicalMechanism.FlashlightPassage)entry=b.path[0].position+Vector3.up*.1f;
            if(b.mechanism==PhysicalMechanism.SwimPassage&&S.GetComponent<FloodSimulationManager>().TrySurface(entry,out float surface,out _))entry.y=surface-1.5f;
            if(!teleportToEntry&&S.player.ActivePhysicalOption&&S.player.ActivePhysicalOption!=b)
            {
                // A continuous approach may cross another branch entry. Q releases that action.
                SibucaoEditorInput.Cancel=true;yield return null;yield return null;
            }
            if(teleportToEntry)S.player.Teleport(entry,st.transform.forward);
            else if(!b.HasEncounter)yield return input.MoveTo(entry,25);
            yield return new WaitForSeconds(.35f);
            // The continuous review starts at the authored notice point, before the
            // old path-entry trigger. Physical work must be available there so the
            // player can clear the obstruction instead of walking through it.
            if(b.HasEncounter&&!st.Active)st.Activate(true);
            Check(st.Active,"Station never activated");Check(b.Available,"Choice not available to fixture");
            if(teleportToEntry){yield return TapE();yield return new WaitForSeconds(2);Check(!st.Resolved,"E then wait completed decision");}
            if(b.HasEncounter&&!b.EncounterComplete)
            {
                yield return WorkEncounter(b,teleportToEntry);
                if(!teleportToEntry)yield return input.MoveTo(entry,25);
            }
            status="B"+(st.index+1)+" C"+b.choiceIndex+" "+b.mechanism;
            if(b.mechanism==PhysicalMechanism.RepairStrap){yield return Repair(b);yield break;}
            if(b.mechanism==PhysicalMechanism.ProbeThenWalk)
            {
                yield return Grip(b.tool);
                for(int sample=0;sample<2;sample++)
                {
                    yield return input.Aim(st.transform.TransformPoint(-2.5f,-.1f,sample*.85f));
                    yield return input.MoveTo(st.transform.TransformPoint(-2.5f,0,sample*.85f-1),20);yield return new WaitForSeconds(.8f);
                }
                Check(st.Attempt.probeSamples>=2,"Stick did not make two separated surface contacts");
            }
            if(b.mechanism==PhysicalMechanism.CarryResident)yield return Grip(b.subject);
            if(b.mechanism==PhysicalMechanism.TreatThenCarry)
            {
                int slot=Enumerable.Range(0,SibucaoInventory.Capacity).First(i=>S.inventory.Slot(i)&&S.inventory.Slot(i).kind==PhysicalItemKind.FirstAid);
                var kit=S.inventory.Slot(slot);S.inventory.Selected=slot;S.inventory.EquipSelected();yield return new WaitForSeconds(.8f);Check(S.player.grabber.held==kit,"Taking out the kit did not engage its inventory grip");
                yield return input.Aim(b.subject.transform.position+Vector3.up*.75f);yield return new WaitForSeconds(.8f);
                S.player.grabber.CarryOrRelease();yield return null;yield return new WaitForSeconds(.25f);Check(kit.delivered&&b.Engaged,"Kit was not physically placed on evacuee");yield return Grip(b.subject);
            }
            if(b.mechanism==PhysicalMechanism.TalkThenWalk||b.mechanism==PhysicalMechanism.SignalHelp||b.mechanism==PhysicalMechanism.GuideResident||b.mechanism==PhysicalMechanism.FlashlightPassage)
            {yield return input.Aim(b.actionPoint.position);yield return TapE();Check(b.Engaged,"E did not start physical subtask");}
            if(b.mechanism==PhysicalMechanism.GatherItems)
            {
                var bag=st.brokenBag;if(bag&&bag.gameObject.activeSelf){yield return input.MoveTo(bag.transform.position-st.transform.forward*.7f,20);yield return input.Aim(bag.transform.position);yield return TapE();}
                foreach(var item in (st.supplementary?st.transform:st.spill.transform).GetComponentsInChildren<PhysicalGrabbable>().Where(SibucaoInventory.Storable).Take(2))
                {yield return input.MoveTo(item.transform.position-st.transform.forward*.7f,20);yield return input.Aim(item.transform.position);yield return TapE();}
                Check(S.inventory.HasBag&&S.state.physical.flags.Contains(st.supplementary?"gathered_"+st.index:"spill_recovered"),"Bag contents were not collected with E");
            }
            bool avoidSpill=st.index==4&&b.choiceIndex==2;
            if(avoidSpill)
            {
                // Walk around the actual dropped bag; keep every Rigidbody/collider enabled.
                yield return input.MoveTo(entry-st.transform.right*1.2f,15);
                yield return input.MoveTo(entry-st.transform.right*1.2f+st.transform.forward*2,15);
            }
            float deadline=Time.time+210;
            float lastMotion=Time.time;Vector3 lastPosition=S.player.transform.position;
            int previous=-1,walkGoal=avoidSpill?1:0;float nextChairLift=0;
            if(b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.Mantle){yield return input.Aim(b.actionPoint.position);SibucaoEditorInput.UseHeld=true;SibucaoEditorInput.Jump=SibucaoEditorInput.Ascend=true;yield return null;yield return null;}
            while(!st.Resolved&&Time.time<deadline)
            {
                if(Vector3.Distance(lastPosition,S.player.transform.position)>.04f){lastPosition=S.player.transform.position;lastMotion=Time.time;}
                Check(Time.time-lastMotion<14,"Traversal stalled at "+S.player.transform.position+" toward "+b.NextPoint.position+" subject="+(b.subject?b.subject.transform.position.ToString():"none")+" helper="+b.Attempt.helperArrived+" step="+b.Attempt.step);
                int step=b.Engaged?b.Attempt.step:0;
                if(b.mechanism==PhysicalMechanism.GuideResident&&b.Engaged&&b.subject&&Time.time>nextChairLift&&SibucaoPhysicalOption.FlatDistance(S.player.transform.position,b.subject.transform.position)>1.7f&&b.subject.Body.linearVelocity.magnitude<.15f)
                {SibucaoEditorInput.Jump=true;nextChairLift=Time.time+1.5f;}
                if(b.mechanism==PhysicalMechanism.SignalHelp&&b.Engaged&&!b.Attempt.helperArrived&&step>1)
                {
                    SibucaoEditorInput.UseHeld=true;var patient=b.subject.transform.position;patient.y=S.player.transform.position.y;
                    if(SibucaoPhysicalOption.FlatDistance(S.player.transform.position,patient)>1.4f)yield return input.MoveTo(patient-st.transform.forward*.8f,12);
                    SibucaoEditorInput.Move=Vector2.zero;lastMotion=Time.time;yield return null;continue;
                }
                bool directed=b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.Mantle;
                if(!directed&&walkGoal<b.path.Length&&SibucaoPhysicalOption.FlatDistance(S.player.transform.position,b.path[walkGoal].position)<.16f)walkGoal++;
                var target=b.path[Mathf.Min(directed?step:walkGoal,b.path.Length-1)].position;
                if(step!=previous){status="B"+(st.index+1)+" C"+b.choiceIndex+" step "+step+"/"+b.path.Length;previous=step;}
                if(b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.GuideResident||b.mechanism==PhysicalMechanism.SignalHelp||b.mechanism==PhysicalMechanism.BoardBoat)SibucaoEditorInput.UseHeld=true;
                if(b.mechanism==PhysicalMechanism.Mantle){SibucaoEditorInput.Ascend=true;if(!b.Engaged||step==3&&!b.Attempt.secondInput)SibucaoEditorInput.Jump=true;}
                if(b.mechanism==PhysicalMechanism.SprintPassage)SibucaoEditorInput.Sprint=true;
                var flat=target-S.player.transform.position;flat.y=0;
                var direction=flat.sqrMagnitude>.002f?flat.normalized:st.transform.forward;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;SibucaoEditorInput.Look=new Vector2(Mathf.DeltaAngle(S.player.transform.eulerAngles.y,yaw),b.Engaged?Mathf.DeltaAngle(0,S.player.view.transform.localEulerAngles.x):0);
                SibucaoEditorInput.Move=new Vector2(0,directed?1:Mathf.Clamp(flat.magnitude/.28f,.08f,1));
                if(step>=b.path.Length)
                {
                    if(b.mechanism==PhysicalMechanism.CarryResident||b.mechanism==PhysicalMechanism.TreatThenCarry)
                    {
                        var goal=b.destination.bounds.center-st.transform.forward*.9f;goal.y=S.player.transform.position.y;
                        if(Vector3.Distance(S.player.transform.position,goal)>.3f){yield return input.MoveTo(goal,25);}
                        SibucaoEditorInput.Move=Vector2.zero;yield return new WaitForSeconds(.7f);SibucaoEditorInput.GripHeld=false;yield return null;yield return new WaitForSeconds(.3f);break;
                    }
                    if(b.mechanism==PhysicalMechanism.BoardBoat)SibucaoEditorInput.Move=Vector2.zero;
                }
                yield return null;
            }
            SibucaoEditorInput.Move=Vector2.zero;Check(st.Resolved,"No resolution; step="+b.Attempt.step+"/"+b.path.Length+" active="+b.Attempt.activeSeconds.ToString("F1")+" position="+S.player.transform.position+" target="+b.NextPoint.position);
            Check(S.state.physical.results[st.index]==b.choiceIndex,"Wrong branch resolved "+S.state.physical.results[st.index]);
        }
        IEnumerator WorkEncounter(SibucaoPhysicalOption b,bool fixturePosition)
        {
            var r=b.Encounter;float buildUntil=Time.time+3;
            while(r.Parts.Count==0&&Time.time<buildUntil)yield return null;
            Check(r.Parts.Count==3,"Encounter must expose three meaningful actions");
            for(int index=0;index<r.Parts.Count;index++)
            {
                var part=r.Parts[index];var direction=b.path.Length>1?(b.path[1].position-b.path[0].position).normalized:b.station.transform.forward;
                var position=part.transform.position-direction*1.45f;
                position.y=b.station.InteractionPosition.y+.12f;
                if(fixturePosition)S.player.Teleport(position,direction);else yield return input.MoveTo(position,20);
                yield return input.Aim(part.GetComponent<Collider>().bounds.center+Vector3.up*.04f);
                SibucaoEditorInput.Interact=SibucaoEditorInput.UseHeld=true;yield return null;yield return null;
                Check(r.Holding,"Could not grip encounter action "+(index+1));
                float deadline=Time.time+25;
                while(r.Progress.displacement[index]<1&&Time.time<deadline)
                {
                    var axis=part.PullDirection;var view=S.player.view.transform;
                    var drag=new Vector2(Vector3.Dot(axis,view.right),Vector3.Dot(axis,view.up));
                    SibucaoEditorInput.Look=(drag.sqrMagnitude<.08f?Vector2.up:drag.normalized)*3;
                    SibucaoEditorInput.UseHeld=true;yield return null;
                }
                Check(r.Progress.displacement[index]>=1,"Encounter action "+(index+1)+" did not finish");
                SibucaoEditorInput.UseHeld=false;yield return null;yield return null;
            }
            Check(r.Complete,"Three encounter actions did not unlock the route");
        }
        IEnumerator Repair(SibucaoPhysicalOption b)
        {
            yield return Grip(b.tool);float deadline=Time.time+160;
            while(Time.time<deadline&&b.Attempt.toolStep<b.workSockets.Length)
            {
                var error=b.WorkTarget-b.tool.tip.position;var local=S.player.view.transform.InverseTransformDirection(error);
                SibucaoEditorInput.Look=new Vector2(Mathf.Clamp(local.x*2,-.5f,.5f),Mathf.Clamp(local.y*2,-.5f,.5f));SibucaoEditorInput.Scroll=Mathf.Clamp(local.z*40,-3,3);
                var rotation=Quaternion.FromToRotation(b.tool.transform.up,b.workSockets[b.Attempt.toolStep].up);rotation.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
                SibucaoEditorInput.ToolTurn=new Vector2(Vector3.Dot(axis,S.player.view.transform.up),Vector3.Dot(axis,S.player.view.transform.right))*Mathf.Clamp(angle*.12f,-2,2);
                status="Strap stage "+b.Attempt.toolStep+" distance "+error.magnitude.ToString("F2")+" work "+b.Attempt.toolSeconds.ToString("F1");yield return null;
            }
            SibucaoEditorInput.GripHeld=false;yield return null;yield return new WaitForSeconds(.3f);Check(b.station.Resolved,"Three-stage strap placement incomplete");
        }
    }
}
#endif
