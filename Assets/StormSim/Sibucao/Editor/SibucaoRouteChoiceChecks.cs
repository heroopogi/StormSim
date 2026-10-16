using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoRouteChoiceChecks
    {
        [MenuItem("StormSim/Route choices/Run Play Mode interaction fixture")]
        public static void InteractionMenu() { Debug.Log(RunInteractions()); }
        public static string RunInteractions()
        {
var s=StormSim.Sibucao.SibucaoSession.Instance;
if(!Application.isPlaying||!s)return "Enter Play Mode before running the fixture.";
var director=s.physicalDecisions.GetComponent<StormSim.Sibucao.SibucaoRouteChoices>();
var saved=s.state;var savedHome=s.home;var savedMode=s.Mode;var savedPos=s.player.transform.position;var savedForward=s.player.transform.forward;
var logs=new System.Collections.Generic.List<string>();
System.Func<StormSim.Sibucao.SibucaoSideMission,Transform,bool> focus=(mission,item)=>{
 Physics.SyncTransforms(); var collider=item.GetComponentInChildren<Collider>();var point=collider?collider.bounds.center:item.position;
 foreach(float radius in new[]{1.2f,1.8f,2.3f})for(int angle=0;angle<360;angle+=45){var offset=Quaternion.Euler(0,angle,0)*Vector3.forward*radius;var at=point+offset;at.y=mission.transform.position.y+.08f;s.player.Teleport(at,point-at);s.player.view.transform.LookAt(point);Physics.SyncTransforms();if(director.Focused()==mission)return true;}return false;
};
try{
 s.home=null;s.SetMode(StormSim.Sibucao.SibucaoMode.Playing);StormSim.Sibucao.SibucaoEditorInput.Active=true;
 foreach(var section in director.sections)foreach(var mission in new[]{section.left,section.right}){
  s.state=new StormSim.Sibucao.RunState();s.physicalDecisions.EnsureNodeCapacity();for(int j=0;j<section.station.index;j++)s.state.physical.results[j]=0;
  s.state.physical.causal=new StormSim.Sibucao.CausalRun{path=StormSim.Sibucao.SibucaoCausalModel.Path(s.state.physical)};
  director.ApplyLayout();float originalClosure=mission.closureDepth;mission.closureDepth=100;
  try{
   s.player.Teleport(section.Approach,section.station.transform.forward);if(s.physicalDecisions.Data.history.Count!=0)throw new Exception("Preview scored a choice");
   director.Complete(mission);if(s.physicalDecisions.NextCoreStage!=section.station.index)throw new Exception("Incomplete mission accepted");
   int guard=0;
   while(mission.Step!=null&&guard++<50){var step=mission.Step;int before=mission.State.step;
    if(!focus(mission,step.item))throw new Exception("Cannot focus step "+before+": "+step.instruction);
    if(!s.player.TryInteract())throw new Exception("Interaction rejected at step "+before);
    if(step.work==StormSim.Sibucao.RouteWork.Slide||step.work==StormSim.Sibucao.RouteWork.Turn){
      StormSim.Sibucao.SibucaoEditorInput.UseHeld=true;StormSim.Sibucao.SibucaoEditorInput.ToolTurn=Vector2.zero;mission.Tick(.2f);if(mission.State.amount!=0)throw new Exception("Idle hold advanced work");
      StormSim.Sibucao.SibucaoEditorInput.ToolTurn=new Vector2(-20,0);mission.Tick(.2f);if(mission.State.amount!=0)throw new Exception("Wrong direction advanced work");
      StormSim.Sibucao.SibucaoEditorInput.ToolTurn=new Vector2(20,0);
      for(int frame=0;frame<600&&mission.State.amount<.999f;frame++){if(frame%30==0){focus(mission,step.item);s.player.TryInteract();}s.player.view.transform.LookAt(step.item.position);mission.Tick(1f/30);s.state.homeSeconds+=1f/30;}
      if(mission.State.step!=before)throw new Exception("Work finished before deliberate release");
      StormSim.Sibucao.SibucaoEditorInput.UseHeld=false;mission.Tick(1f/30);StormSim.Sibucao.SibucaoEditorInput.ToolTurn=Vector2.zero;
    }
    else if(step.work==StormSim.Sibucao.RouteWork.Carry){
      for(int angle=0;angle<360&&mission.State.step==before;angle+=45){
        if(!director.HeldBy){if(!focus(mission,step.item))continue;if(!s.player.TryInteract())continue;}
        var point=step.target.position;var at=point+Quaternion.Euler(0,angle,0)*Vector3.back*1.15f;at.y=mission.transform.position.y+.08f;s.player.Teleport(at,point-at);s.player.view.transform.LookAt(point+Vector3.up*.2f);
        for(int frame=0;frame<180;frame++){mission.Tick(1f/30);s.state.homeSeconds+=1f/30;Physics.SyncTransforms();}
        s.player.TryInteract();
      }
    }
    else if(step.work==StormSim.Sibucao.RouteWork.Escort){
      for(int frame=0;frame<12000&&mission.State.step==before;frame++){var goal=mission.WorkTarget;s.player.Teleport(goal+section.station.transform.right*.3f,section.station.transform.forward);mission.Tick(1f/30);s.state.homeSeconds+=1f/30;Physics.SyncTransforms();}
    }
    if(mission.State.step==before)throw new Exception("Step stalled: "+before+" "+step.instruction+" amount="+mission.State.amount+" distance="+(step.target?Vector3.Distance(step.item.position,step.target.position):0));
   }
   if(mission.Step!=null)throw new Exception("Work did not finish");
   s.player.Teleport(section.Merge,section.station.transform.forward);mission.Tick(1f/30);
   if(!mission.State.complete||s.physicalDecisions.NextCoreStage!=section.station.index+1||s.state.physical.history.Count!=1)throw new Exception("Section handoff failed");
   var serialized=JsonUtility.ToJson(s.state.physical);var clone=JsonUtility.FromJson<StormSim.Sibucao.PhysicalRunData>(serialized);s.physicalDecisions.causalModel.ValidateRun(clone);
   director.Complete(mission);if(s.state.physical.history.Count!=1)throw new Exception("Duplicate completion");
   logs.Add("PASS "+mission.id+": "+mission.steps.Length+" focused physical steps, release/idle checks, separate completion and save round trip.");
  }catch(Exception ex){logs.Add("FAIL "+mission.id+": "+ex.Message);}finally{mission.closureDepth=originalClosure;mission.Cancel();}
 }
}finally{StormSim.Sibucao.SibucaoEditorInput.Reset();s.state=saved;s.home=savedHome;director.ApplyLayout();s.player.Teleport(savedPos,savedForward);s.SetMode(savedMode);}
System.IO.File.WriteAllLines("Documentation/SIBUCAO_THREE_WAY_INTERACTION_CHECKS.txt",logs);
return string.Join("\n",logs);



        }
        static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        [MenuItem("StormSim/Route choices/Validate sections and save compatibility")]
        public static void Menu() { Debug.Log(Validate()); }
        public static string Validate()
        {
            var director=UnityEngine.Object.FindFirstObjectByType<SibucaoRouteChoices>();
            Require(director&&director.sections.Length==9,"Nine sections must be installed.");
            var ids=new HashSet<string>();int steps=0,paths=0;
            foreach(var section in director.sections)
            {
                Require(section&&section.station&&section.station.index>=0&&section.station.index<9,"Missing center station.");
                Require(section.left&&section.right&&section.left!=section.right,"Missing side mission.");
                Require(section.layouts!=null&&section.layouts.Length==4,"Four home layouts required.");
                foreach(var layout in section.layouts)
                {
                    Require(layout!=null,"Unfitted section "+section.station.index);
                    Require(layout.leftPath!=null&&layout.leftPath.Length>1&&layout.rightPath!=null&&layout.rightPath.Length>1,"Missing passage path.");
                    Require(SibucaoRouteChoices.Flat(layout.left,layout.right)>=3.8f,"Side missions overlap.");
                    Require(SibucaoRouteChoices.Flat(layout.leftPath.Last(),layout.merge)<.1f&&SibucaoRouteChoices.Flat(layout.rightPath.Last(),layout.merge)<.1f,"Passages do not rejoin.");paths+=2;
                }
                foreach(var mission in new[]{section.left,section.right})
                {
                    Require(ids.Add(mission.id),"Duplicate mission ID.");Require(mission.voice&&mission.voice.clip,"Missing spoken approach call: "+mission.id);
                    Require(mission.steps!=null&&mission.steps.Length>=3,"Mission lacks actionable steps: "+mission.id);
                    Require(mission.steps.Any(s=>s.work!=RouteWork.Talk&&s.work!=RouteWork.Check),"Dialogue-only mission: "+mission.id);
                    foreach(var step in mission.steps)
                    {
                        Require(step.item&&!string.IsNullOrEmpty(step.instruction),"Missing interaction target: "+mission.id);
                        if(step.work==RouteWork.Carry)Require(step.target,"Missing delivery target: "+mission.id);
                        if(step.work==RouteWork.Escort)Require(mission.companions.Length>0,"No escort actors: "+mission.id);
                        if(step.work==RouteWork.Slide)Require(Vector3.Distance(step.from,step.to)>.02f,"Stationary slide: "+mission.id);
                        if(step.work==RouteWork.Turn)Require(Vector3.Distance(step.fromAngle,step.toAngle)>1,"Stationary turn: "+mission.id);
                        steps++;
                    }
                }
            }
            var model=ScriptableObject.CreateInstance<SibucaoCausalModel>();
            try
            {
                foreach(int choice in new[]{0,100,101})
                {
                    var run=new PhysicalRunData {causal=new CausalRun()};
                    for(int i=0;i<9;i++)
                    {
                        Require(SibucaoCausalModel.Next(run)==i,"Incorrect ordered stage.");model.Enter(run,i);
                        run.results[i]=choice;run.causal.path=SibucaoCausalModel.Path(run);model.ValidateRun(run);
                        var clone=JsonUtility.FromJson<PhysicalRunData>(JsonUtility.ToJson(run));model.ValidateRun(clone);
                        Require(SibucaoCausalModel.Next(clone)==i+1,"Save loses side progression.");
                    }
                    model.Enter(run,9);Require(run.causal.path.Length==9,"Incorrect arrival history.");
                }
                var partial=new PhysicalRunData();partial.sideMissions.Add(new RouteMissionProgress{id="B1_left",step=2,amount=.42f,hasCarriedPosition=true,carriedPosition=new Vector3(1,2,3)});
                var restored=JsonUtility.FromJson<PhysicalRunData>(JsonUtility.ToJson(partial));
                Require(restored.sideMissions[0].step==2&&Mathf.Abs(restored.sideMissions[0].amount-.42f)<.001f&&restored.results[0]==-1,"Partial work accidentally completes a section.");
            }
            finally {UnityEngine.Object.DestroyImmediate(model);}
            return "PASS: 9 centers, 18 unique physical side missions, "+steps+" action steps, "+paths+" home-specific paths; ordered center/left/right progression, arrival and partial-save round trips.";
        }
    }
}


