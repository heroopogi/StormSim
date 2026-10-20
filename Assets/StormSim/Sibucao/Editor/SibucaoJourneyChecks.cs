using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using StormSim.Preparedness;

namespace StormSim.Sibucao.Editor
{
    /// <summary>Play-mode integration fixture: public preparation actions and real controller travel.
    /// Video completion is injected; this is not a mouse/UI usability test.</summary>
    public static class SibucaoJourneyChecks
    {
        static SibucaoSession s;
        static string kind,stage,result;
        static Vector3 door,approach,outside,lastPosition;
        static float stuck,lastTime,lateClock;
        static int waypoint;
        static double began;
        public static string Status=>result;
        public static void Begin(string scenario="solo",float stormClock=0)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            Stop();s=SibucaoSession.Instance;kind=scenario;lateClock=stormClock;
            s.UseTestSaveDirectory("A:/Unity/StormSim/tmp/journey-validation/"+scenario);
            SibucaoEditorInput.Active=true;s.StartWithDifficulty(2);
            stage="preparation";waypoint=0;stuck=0;began=EditorApplication.timeSinceStartup;lastTime=Time.time;
            EditorApplication.update+=Tick;result="RUNNING preparation "+kind;
        }
        public static void Stop(){EditorApplication.update-=Tick;SibucaoEditorInput.Reset();}
        public static void BeginCompanion(string scenario)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            Stop();s=SibucaoSession.Instance;kind=scenario;lateClock=0;
            s.UseTestSaveDirectory("A:/Unity/StormSim/tmp/journey-validation/"+scenario);
            SibucaoEditorInput.Active=true;if(s.home)s.home.Suspend();s.home=null;
            s.state=new RunState();s.player.Teleport(s.route.Sample(0),s.route.Sample(2)-s.route.Sample(0));s.physicalDecisions.ResetRun();s.SetMode(SibucaoMode.Playing);s.weather.SetStorm(true);
            var family=s.GetComponent<SibucaoOptionalSideMission>();var incidents=s.GetComponent<SibucaoRouteIncidentSequence>();
            float start=scenario=="family"?33:590;
            s.player.Teleport(s.route.Sample(start)+Vector3.up*.1f,s.route.Sample(start+2)-s.route.Sample(start));
            if(scenario=="family"){s.physicalDecisions.SetFlag("side_family_discovered");s.physicalDecisions.SetFlag("side_family_accepted");family.EditorAdvanceSupply(4);family.EditorAdvanceSupply(4);}
            else{incidents.EditorPrepareRescue(true);s.physicalDecisions.SetFlag("route_rescue_stabilized");s.physicalDecisions.SetFlag("route_rescue_walk_consent");}
            stage="companion";waypoint=3;stuck=0;began=EditorApplication.timeSinceStartup;lastTime=Time.time;lastPosition=s.player.transform.position;
            EditorApplication.update+=Tick;result="RUNNING companion fixture "+kind;
        }
        static void Finish(string message){result=message;SessionState.SetString("StormSim.JourneyResult",message);Stop();}
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||!s){Finish("STOPPED");return;}
                SibucaoEditorInput.Active=true;
                float dt=Mathf.Clamp(Time.time-lastTime,0,.1f);lastTime=Time.time;
                if(EditorApplication.timeSinceStartup-began>1500){Finish("TIMEOUT "+result);return;}
                var bridge=s.GetComponent<SibucaoPreparationBridge>();var flood=s.GetComponent<FloodSimulationManager>();
                if(stage=="preparation")
                {
                    var p=bridge.preparation;
                    if(p.CoachingChoicePending)p.SetCoaching(false);
                    p.GetComponent<PreparationIntro>()?.End();
                    p.CompleteTelevisionBulletin();
                    if(!p.CanPrepare)return;
                    if(p.Bag.storageDoor&&p.Bag.storageDoor.closed)p.Use(p.Bag.storageDoor);
                    if(!p.player.carriedBag&&!p.player.GrabBag(p.Bag)){Finish("FAIL bag pickup: "+p.message);return;}
                    if(lateClock>0)p.IssueEvacuation();
                    if(!p.TryFinish(true)){Finish("FAIL departure: "+p.message);return;}
                    stage="handoff";return;
                }
                if(stage=="handoff")
                {
                    if(bridge.Active||s.Mode!=SibucaoMode.Playing)return;
                    if(!s.inventory.HasBag||string.IsNullOrEmpty(s.state.preparationSnapshot)){Finish("FAIL preparation continuity");return;}
                    var bounds=s.home.doorPanel.GetComponent<Collider>().bounds;
                    door=bounds.center;approach=door+s.home.transform.forward*1.25f;outside=door-s.home.transform.forward*2;
                    if(lateClock>0){s.state.homeSeconds=lateClock;s.state.floodClockOffset=0;flood.RestoreClock();}
                    stage="walking";lastPosition=s.player.transform.position;
                }
                if(flood.IsCatchingUp){SibucaoEditorInput.Move=Vector2.zero;result="RUNNING flood catchup "+flood.CatchUpProgress.ToString("P0");return;}
                if(s.Mode==SibucaoMode.ArrivalReview||s.Mode==SibucaoMode.Complete)
                {
                    if(stage=="companion"&&!s.state.physical.flags.Contains(kind=="family"?"experience_family_arrived":"route_rescue_escorted")){SibucaoEditorInput.Move=Vector2.zero;result="RUNNING waiting for companion reception";return;}
                    if(s.Mode==SibucaoMode.ArrivalReview){s.physicalDecisions.ConfirmArrivalReport();s.physicalDecisions.ConfirmArrivalReport();}
                    Finish("PASS "+kind+" "+(stage=="companion"?"companion fixture":"preparation")+" -> "+s.Mode+" clock="+s.state.homeSeconds.ToString("F1")+" assisted="+s.state.physical.flags.Contains("assisted_evacuation")+" bag="+s.inventory.HasBag+" health="+s.state.physical.health);return;
                }
                if(s.Mode!=SibucaoMode.Playing){Finish("FAIL unexpected mode "+s.Mode);return;}
                var help=s.GetComponent<SibucaoEvacuationAssistance>();
                if(help.Pending){SibucaoEditorInput.Move=Vector2.zero;if(help.Ready)help.Board();result="RUNNING assistance "+help.Status;return;}
                float progress=s.route.Progress(s.player.transform.position);
                if(stage=="companion")
                {
                    var family=s.GetComponent<SibucaoOptionalSideMission>();var incidents=s.GetComponent<SibucaoRouteIncidentSequence>();
                    var companion=kind=="family"?family.FamilyPosition:Field<Transform>(incidents,"casualty").position;
                    float gap=progress-s.route.Progress(companion);
                    if(gap>5)
                    {
                        SibucaoEditorInput.Move=Vector2.zero;stuck+=dt;
                        result="RUNNING "+kind+" waiting progress="+progress.ToString("F1")+" companion="+s.route.Progress(companion).ToString("F1")+" gap="+gap.ToString("F1")+" wait="+stuck.ToString("F1");
                        if(stuck>4&&help.CanRequest)help.Request();
                        else if(stuck>20)Finish("BLOCKED "+result);
                        return;
                    }
                }
                Vector3 target;
                if(waypoint<3)
                {
                    target=waypoint==0?approach:waypoint==1?door:outside;
                    if(!s.state.homeDoorOpen&&Vector3.Distance(s.player.view.transform.position,door)<2.8f){s.player.view.transform.LookAt(door);s.home.TryInteract();}
                    if(Flat(s.player.transform.position,target)<.5f){waypoint++;stuck=0;}
                }
                else target=s.route.Sample(Mathf.Min(s.route.length,progress+2));
                bool moved=Steer(target);
                if(Flat(lastPosition,s.player.transform.position)<.005f)stuck+=dt;else stuck=0;
                lastPosition=s.player.transform.position;
                result="RUNNING "+kind+" waypoint="+waypoint+" progress="+progress.ToString("F1")+" clock="+s.state.homeSeconds.ToString("F1")+" depth="+s.player.WaterDepthAtFeet.ToString("F3")+" stuck="+stuck.ToString("F1");
                if((!moved||stuck>3)&&help.CanRequest){SibucaoEditorInput.Move=Vector2.zero;help.Request();}
                else if(stuck>12)Finish("BLOCKED "+result+" position="+s.player.transform.position+" door="+s.state.homeDoorOpen);
            }
            catch(Exception ex){Finish("ERROR "+ex);}
        }
        static bool Steer(Vector3 target)
        {
            var delta=target-s.player.transform.position;delta.y=0;
            if(delta.magnitude<.15f){SibucaoEditorInput.Move=Vector2.zero;return true;}
            var flood=s.GetComponent<FloodSimulationManager>();
            foreach(float angle in new[]{0f,25,-25,50,-50,80,-80})
            {
                var direction=Quaternion.Euler(0,angle,0)*delta.normalized;
                bool blocked=false;
                foreach(float height in new[]{.45f,1.5f})
                foreach(var hit in Physics.SphereCastAll(s.player.transform.position+Vector3.up*height,.28f,direction,.65f,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(s.player.transform)&&hit.normal.y<.65f)blocked=true;
                var next=s.player.transform.position+direction*.8f;
                if(!Physics.Raycast(next+Vector3.up*.6f,Vector3.down,out var ground,1.4f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<.7f)continue;
                if(flood.GetWaterDepth(ground.point)>.02f)continue;
                if(blocked)continue;
                s.player.transform.rotation=Quaternion.LookRotation(direction);SibucaoEditorInput.Move=Vector2.up;return true;
            }
            SibucaoEditorInput.Move=Vector2.zero;return false;
        }
        static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
