using UnityEngine;
using UnityEngine.InputSystem;
namespace StormSim.Sibucao
{
    [DefaultExecutionOrder(-110)]
    public sealed class SibucaoMissionGuide : MonoBehaviour
    {
        public bool IsOpen {get;private set;}
        public bool BlocksInput=>IsOpen||consumedFrame==Time.frameCount;
        public SibucaoInstructionPaper FocusedPaper {get;private set;}
        int consumedFrame=-1;
        RunState guidedRun;
        bool wasInsideHome,thresholdAnnounced;
        Vector3 trackedTarget;
        float bestTargetDistance=float.PositiveInfinity,stalledSeconds;
        int recoveryLevel;
        int trackedCoreStage=-999;
        SibucaoSession S=>SibucaoSession.Instance;
        public SibucaoOptionalSideMission OptionalMission=>S?S.GetComponent<SibucaoOptionalSideMission>():null;
        public SibucaoFloodedStreetIncident FloodedStreet=>S?S.GetComponent<SibucaoFloodedStreetIncident>():null;
        public SibucaoRouteIncidentSequence RouteIncidents=>S?S.GetComponent<SibucaoRouteIncidentSequence>():null;
        public SibucaoContinuousChapterOne ContinuousChapter=>S?S.GetComponent<SibucaoContinuousChapterOne>():null;
        public SibucaoRouteChoices RouteChoices=>S&&S.physicalDecisions?S.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;
        public static readonly string[] Objectives={"Assess the flooded street.","Find a way past the stalled cars.","Find a route through the flooded junction.","Find a way past the fallen cable.","Deal with your damaged bag.","A resident needs help at the curb.","Reach the rescue checkpoint.","An evacuee has collapsed nearby.","Continue toward the next evacuation area."};
        public static readonly string[] Hints={"","","","","","","","",""};
        public bool InsideHome
        {
            get {if(!S||!S.home)return false;var p=S.home.transform.InverseTransformPoint(S.player.transform.position);return Mathf.Abs(p.x)<4.1f&&Mathf.Abs(p.z)<3.8f;}
        }
        public PhysicalDecisionStation CurrentStation
        {
            get {if(!S||!S.physicalDecisions)return null;PhysicalDecisionStation best=null;float distance=30;float progress=S.route.Progress(S.player.transform.position);
                foreach(var station in S.physicalDecisions.stations)if(station.EngagedBranch)return station;
                foreach(var station in S.physicalDecisions.stations)if(station.Active&&(!station.supplementary||station.routeDistance>=progress-4)){float d=Vector3.Distance(S.player.transform.position,station.transform.position);if(d<distance){distance=d;best=station;}}return best;}
        }
        public PhysicalDecisionStation NextStation
        {
            get {if(!S||!S.physicalDecisions)return null;PhysicalDecisionStation first=null;
                float progress=S.route.Progress(S.player.transform.position);
                foreach(var st in S.physicalDecisions.stations)
                    if(st.gameObject.activeInHierarchy&&S.physicalDecisions.CanEnter(st)&&!st.Resolved&&(!st.supplementary||st.routeDistance>=progress-4)&&(!first||st.routeDistance<first.routeDistance))first=st;
                return first;}
        }
        public bool WaitingForStreetFlood=>false; // The street crossing is actionable before deep water arrives.
        string B1Objective(PhysicalDecisionStation station)
        {
            var branch=station?station.EngagedBranch:null;
            if(!branch)return station&&station.HasVisibleWater?"Floodwater blocks the route. Observe it, then choose a way across.":"Continue along the rain-soaked street.";
            if(branch.choiceIndex==0)
            {
                int samples=station.Attempt.probeSamples;
                if(samples==0)return "Use the bamboo stick to check the water beside the raised boards.";
                if(samples==1)return "Compare the depth at a second wet spot.";
                return "Depth checked. Follow the raised boards to dry ground.";
            }
            if(branch.choiceIndex==1)return "Cross the marked lane at a steady pace.";
            return "Move through the damaged railing gap and reach dry ground.";
        }
        string B1Hint(PhysicalDecisionStation station)
        {
            var branch=station?station.EngagedBranch:null;
            if(!branch)return "Look at the advisory point, bamboo depth stick, or parent. A contextual action appears when the crosshair is on the situation.";
            if(branch.choiceIndex==0)
            {
                int samples=station.Attempt.probeSamples;
                if(!branch.tool||!branch.tool.IsHeld)return "Hold left mouse on the bamboo stick to grip it. Q cancels.";
                if(samples<2)return "Aim the stick tip into visible water and hold it still. Test two spots at least one step apart.";
                return "Keep the stick or put it down, then walk along the raised-board route.";
            }
            if(branch.choiceIndex==1)return "Use WASD and remain in the marked lane. Q cancels before completion.";
            return "Hold Shift + W and stay inside the damaged-gap lane. Q cancels.";
        }
        public string Objective
        {
            get
            {
                var experience=S?S.GetComponent<SibucaoEvacuationExperience>():null;
                if(experience&&(!S.home||!S.home.MovementLocked))return experience.CurrentObjective;
                if(RouteIncidents&&RouteIncidents.ProgressVisible&&!InsideHome&&(!S.home||!S.home.MovementLocked))return RouteIncidents.Objective;
                if(FloodedStreet&&FloodedStreet.Active&&!InsideHome&&(!S.home||!S.home.MovementLocked))return "Inspect the junction from dry footing; do not enter floodwater.";
                if(OptionalMission&&!InsideHome&&(!S.home||!S.home.MovementLocked))return OptionalMission.Objective;
                if(ContinuousChapter&&!InsideHome&&(!S.home||!S.home.MovementLocked))return ContinuousChapter.Objective;
                if(RouteChoices&&!string.IsNullOrEmpty(RouteChoices.Objective)&&!InsideHome&&(!S.home||!S.home.MovementLocked))return RouteChoices.Objective;
                if(S.home&&S.home.Standing)return "Stand up and get your bearings.";
                if(S.home&&S.home.MovementLocked)return "Watch the weather bulletin.";
                if(InsideHome)return S.state.homeDoorOpen?"Step outside with your go-bag.":"Open the front door when you are ready.";
                var current=CurrentStation;
                if(current)return current.index==0?B1Objective(current):current.supplementary?"Get past "+LowerTitle(current.title)+".":current.node?current.FloodObjective:Objectives[current.index];
                if(WaitingForStreetFlood)return "The street is still clear. Watch the changing weather.";
                var next=NextStation;
                if(next)return next.index==0?B1Objective(next):next.supplementary?"Reach "+LowerTitle(next.title)+".":next.node?next.FloodObjective:next.title;
                return "Enter the school evacuation center.";
            }
        }
        public string Hint
        {
            get
            {
                if(RouteIncidents&&RouteIncidents.ProgressVisible&&!InsideHome&&(!S.home||!S.home.MovementLocked))return RouteIncidents.Hint;
                if(FloodedStreet&&FloodedStreet.Active&&!InsideHome&&(!S.home||!S.home.MovementLocked))return FloodedStreet.Hint;
                if(OptionalMission&&!InsideHome&&(!S.home||!S.home.MovementLocked))return OptionalMission.Hint;
                if(ContinuousChapter&&!InsideHome&&(!S.home||!S.home.MovementLocked))return ContinuousChapter.Hint;
                if(RouteChoices&&!string.IsNullOrEmpty(RouteChoices.Hint)&&!InsideHome&&(!S.home||!S.home.MovementLocked))return RouteChoices.Hint;
                if(FocusedPaper)return "E  Read full scenario guide";
                if(S.home&&S.home.Standing)return "You can move when you finish standing up.";
                if(S.home&&S.home.MovementLocked)return "Listen to the news; movement resumes after standing up.";
                if(InsideHome)return S.state.homeDoorOpen?"WASD: step through the doorway.":"Look at the front door and press E.";
                if(S.player.swimming&&S.player.swimming.IsSwimming)return "WASD swim / Mouse look / Space rise / Ctrl or C dive";
                if(CurrentStation)return CurrentStation.index==0?B1Hint(CurrentStation):CurrentStation.ActionHint;
                if(RouteChoices&&RouteChoices.TryDepartureTarget(out _))return "WASD: follow the yellow evacuation route.";
                return "WASD move / Mouse look / E interact / J scenario guide";
            }
        }
        public Vector3 NavigationTarget
        {
            get {if(InsideHome)return S.home.doorPanel.position;if(RouteIncidents&&RouteIncidents.WantsLocalGuidance)return RouteIncidents.NavigationTarget;if(FloodedStreet&&FloodedStreet.WantsLocalGuidance)return FloodedStreet.NavigationTarget;if(OptionalMission&&OptionalMission.SideMissionActive)return OptionalMission.NavigationTarget;if(S.physicalDecisions&&S.physicalDecisions.stations.Length==0&&S.physicalDecisions.evacuationCenter&&S.physicalDecisions.evacuationCenter.arrivalPoint)return S.physicalDecisions.evacuationCenter.arrivalPoint.position;if(ContinuousChapter)return ContinuousChapter.NavigationTarget;if(RouteChoices&&RouteChoices.TryDepartureTarget(out var exit))return exit;if(RouteChoices&&(RouteChoices.Working||RouteChoices.Previewing))return RouteChoices.NavigationTarget;
                var target=CurrentStation?CurrentStation:NextStation;
                if(target&&!target.supplementary&&!target.EngagedBranch&&RouteChoices&&RouteChoices.Current&&RouteChoices.Current.station==target)return RouteChoices.NavigationTarget;
                if(CurrentStation)return CurrentStation.NavigationTarget;
                if(NextStation)return NextStation.NavigationTarget;
                if(S.physicalDecisions&&S.physicalDecisions.evacuationCenter&&S.physicalDecisions.Data.results[8]>=0)return S.physicalDecisions.evacuationCenter.arrivalPoint.position;
                float progress=S.route.Progress(S.player.transform.position);return S.route.Sample(Mathf.Min(S.route.length,progress+12));}
        }
        public string ClearInstructions
        {
            get
            {
                if(S.home&&S.home.MovementLocked)return "Listen to the bulletin. You can move after standing up.";
                if(InsideHome)return S.state.homeDoorOpen?"Walk through the open doorway. Your first outdoor objective will appear when you cross the threshold.":"Face the front door and press E when you are ready to evacuate.";
                if(RouteIncidents&&RouteIncidents.ProgressVisible)return RouteIncidents.ClearInstructions;
                if(FloodedStreet&&FloodedStreet.Active)return FloodedStreet.ClearInstructions;
                if(OptionalMission)return OptionalMission.ClearInstructions;
                if(ContinuousChapter)return ContinuousChapter.ClearInstructions;
                if(S.player.swimming&&S.player.swimming.IsSwimming)return "WASD: swim / Space: rise / Ctrl: dive. Follow the route toward a dry landing.";
                if(RouteChoices&&RouteChoices.TryDepartureTarget(out _))return "Follow the yellow evacuation markers toward the highlighted situation. J opens the guide if you need it.";
                var side=RouteChoices?RouteChoices.Working:null;
                if(side)return side.Step!=null?"Step "+(side.State.step+1)+"/"+side.steps.Length+" - "+side.Hint:"Work finished. Follow the map route to the meeting point.";
                var st=CurrentStation?CurrentStation:NextStation;
                var branch=st?st.EngagedBranch:null;
                if(st&&st.supplementary&&!branch&&st.branches.Length>0)branch=st.branches[0];
                if(branch&&branch.requiredPassage&&!branch.requiredPassage.State.secured)
                {
                    var passage=branch.requiredPassage;int undone=2-((passage.State.fasteners&1)!=0?1:0)-((passage.State.fasteners&2)!=0?1:0);
                    if(undone>0)return "Release the awning ropes: aim at each knot and press E. Knots remaining: "+undone+".";
                    return passage.State.opening<.98f?"E: take the lifting grip. Hold E and walk backward to lift the awning. Q: let go.":"Aim at the grip and press E to secure the awning. Then walk through the opening.";
                }
                if(branch&&branch.HasEncounter&&branch.Engaged&&!branch.EncounterComplete)return "Next action: "+branch.Prompt+" / Q: cancel";
                if(branch)switch(branch.mechanism)
                {
                    case PhysicalMechanism.Mantle:return "Face the trunk. Hold Space + W to climb; release and press Space again to shift footing. Q: cancel.";
                    case PhysicalMechanism.GatherItems:return "Aim at each sealed supply and press E to store it in your go-bag, then follow the route past the cart.";
                    case PhysicalMechanism.Squeeze:return "Hold E + W to squeeze through the gap. Turn toward the opening. Q: cancel.";
                    case PhysicalMechanism.Walk:return "Follow the bright map line and arrow through the passage to the next marker.";
                    case PhysicalMechanism.ProbeThenWalk:return B1Hint(st);
                    case PhysicalMechanism.RepairStrap:return "F: grip the connector. R + mouse: align it with the buckle. F / Q: release to place.";
                    case PhysicalMechanism.CarryResident:return "F: take the assistance grip. Follow the marked route; F / Q: release at the landing.";
                    case PhysicalMechanism.GuideResident:return "E: take chair handles. Hold E while moving. Space: lift over the curb. Follow the map route.";
                    case PhysicalMechanism.SignalHelp:return "E: call the neighbour. Hold E while moving to lead them to the resident, then to the landing.";
                    case PhysicalMechanism.TalkThenWalk:return "Face the resident and press E to speak. Then follow the marked lane.";
                    case PhysicalMechanism.BoardBoat:return "Walk along the gangway. On the deck, hold E to release the mooring.";
                    case PhysicalMechanism.FlashlightPassage:return "Equip a flashlight from your bag. Press E at the passage, then follow the yellow route.";
                    case PhysicalMechanism.TreatThenCarry:return "Use the kit from your bag. Grip, position and release it; then take the assistance grip and follow the route.";
                    case PhysicalMechanism.SwimPassage:return "WASD: swim toward the marked landing / Space: rise / Ctrl: dive.";
                    case PhysicalMechanism.SprintPassage:return "Hold Shift + W to cross. If tired, rest on dry footing first.";
                }
                if(RouteChoices&&RouteChoices.Previewing)return "Inspect LEFT / AHEAD / RIGHT. Approach your choice, aim at its person or item, then follow the action prompt.";
                return "WASD: walk / Shift: run / E: interact / J: guide / N: repeat narration. Follow the bright map line.";
            }
        }
        public void Open()
        {
            if(!S||S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked)return;
            IsOpen=true;consumedFrame=Time.frameCount;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void Close(){IsOpen=false;consumedFrame=Time.frameCount;if(S&&S.Mode==SibucaoMode.Playing)S.LockCursor();}
        static string LowerTitle(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "the next situation";
            return char.ToLowerInvariant(value[0])+value.Substring(1);
        }
        void ResetDepartureGuidance()
        {
            guidedRun=S.state;wasInsideHome=InsideHome;thresholdAnnounced=false;trackedTarget=Vector3.zero;
            bestTargetDistance=float.PositiveInfinity;stalledSeconds=0;recoveryLevel=0;
            trackedCoreStage=S.physicalDecisions?S.physicalDecisions.NextCoreStage:-999;
        }
        void UpdateDepartureGuidance()
        {
            if(guidedRun!=S.state){ResetDepartureGuidance();return;}
            if(OptionalMission)return;
            if(ContinuousChapter)return;
            if(Time.unscaledTime<S.toastUntil&&S.toastTitle=="Route updated")
            {stalledSeconds=0;recoveryLevel=0;bestTargetDistance=float.PositiveInfinity;return;}
            var history=S.physicalDecisions?S.physicalDecisions.Data.history:null;
            if(history!=null&&history.Count>0&&S.state.homeSeconds-history[history.Count-1].finishedAt<8)
            {stalledSeconds=0;recoveryLevel=0;bestTargetDistance=float.PositiveInfinity;return;}
            int coreStage=S.physicalDecisions?S.physicalDecisions.NextCoreStage:-999;
            if(coreStage!=trackedCoreStage)
            {
                trackedCoreStage=coreStage;trackedTarget=Vector3.zero;
                bestTargetDistance=float.PositiveInfinity;stalledSeconds=0;recoveryLevel=0;return;
            }
            bool inside=InsideHome;
            if(!thresholdAnnounced&&wasInsideHome&&!inside&&S.home&&S.home.Active)
            {
                thresholdAnnounced=true;
                var first=NextStation;
                string situation=first&&first.supplementary?LowerTitle(first.title):"the first street obstacle";
                S.Notify("Evacuate to the school","First: get past "+situation+". Follow the yellow evacuation markers.");
                S.Say("The school is this way. Follow the evacuation markers and get past "+situation+".",7);
            }
            wasInsideHome=inside;
            if(inside||S.home&&S.home.MovementLocked||IsOpen||S.inventory&&S.inventory.BlocksInput||RouteChoices&&RouteChoices.Working||RouteChoices&&RouteChoices.Previewing)
            {stalledSeconds=0;recoveryLevel=0;bestTargetDistance=float.PositiveInfinity;return;}
            var station=CurrentStation;
            if(station&&(station.EngagedBranch||Vector3.Distance(S.player.transform.position,station.InteractionPosition)<8))
            {stalledSeconds=0;recoveryLevel=0;bestTargetDistance=float.PositiveInfinity;return;}
            Vector3 target=NavigationTarget;
            if((target-trackedTarget).sqrMagnitude>9)
            {trackedTarget=target;bestTargetDistance=float.PositiveInfinity;stalledSeconds=0;recoveryLevel=0;}
            float distance=SibucaoRouteChoices.Flat(S.player.transform.position,target);
            if(distance<bestTargetDistance-1)
            {bestTargetDistance=distance;stalledSeconds=0;recoveryLevel=0;return;}
            stalledSeconds+=Time.deltaTime;
            if(recoveryLevel==0&&stalledSeconds>=15)
            {
                recoveryLevel=1;
                Vector3 local=S.player.transform.InverseTransformPoint(target);
                string direction=Mathf.Abs(local.x)<2?"ahead":local.x>0?"to your right":"to your left";
                S.Notify("Need a direction?","The next evacuation marker is "+direction+", about "+Mathf.CeilToInt(distance)+" metres away.");
            }
            else if(recoveryLevel==1&&stalledSeconds>=32)
            {
                recoveryLevel=2;
                S.Say("Keep moving toward the yellow evacuation markers. The school route continues there.",6);
            }
        }
        void Update()
        {
            if(!S||S.Mode!=SibucaoMode.Playing){IsOpen=false;FocusedPaper=null;return;}
            if(S.inventory&&S.inventory.BlocksInput){FocusedPaper=null;return;}
            var experience=S.GetComponent<SibucaoEvacuationExperience>();
            if(!experience||!experience.AtRestPoint)UpdateDepartureGuidance();
            var kb=Keyboard.current;
            if(IsOpen){if(kb!=null&&(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame||kb.escapeKey.wasPressedThisFrame||StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.J).wasPressedThisFrame))Close();return;}
            FocusedPaper=null;
            var camera=S.player.view.transform;
            var hits=Physics.RaycastAll(camera.position,camera.forward,2.8f,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.transform.IsChildOf(S.player.transform))continue;FocusedPaper=hit.collider.GetComponentInParent<SibucaoInstructionPaper>();break;}
            if(kb!=null&&(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.J).wasPressedThisFrame||(FocusedPaper&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame)))Open();
        }
    }
}
