using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Explicitly abstracted training transport, not rescue-vehicle physics or a real rescue guarantee.</summary>
    public sealed class SibucaoEvacuationAssistance : MonoBehaviour
    {
        SibucaoSession S=>GetComponent<SibucaoSession>();
        EvacuationExperienceState State=>S.GetComponent<SibucaoEvacuationExperience>().State;
        public bool Pending=>State.assistanceRequested;
        public bool Ready=>Pending&&State.assistanceWait>=12;
        public string Status=>!Pending?"Flooded access? Request assistance from supported dry footing. Transport is abstracted in this training scenario.":Ready?"Scenario transport is ready. Confirm boarding; nearby consenting companions may travel with you.":"Request acknowledged. Stay at this dry meeting point. Scenario transport is being arranged ("+Mathf.CeilToInt(12-State.assistanceWait)+"s).";
        public bool CanRequest=>S.Mode==SibucaoMode.Playing&&!Pending&&!S.state.physical.boundaryReached&&(!S.home||!S.home.MovementLocked)&&DryFooting(S.player.transform.position)&&WaterNearby();
        bool DryFooting(Vector3 p)
        {
            var flood=S.GetComponent<FloodSimulationManager>();
            if(flood&&flood.IsCatchingUp)return false;
            if(!Physics.Raycast(p+Vector3.up*.25f,Vector3.down,out var ground,.7f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<.75f)return false;
            return !flood||flood.GetWaterDepth(ground.point)<=.02f;
        }
        bool WaterNearby()
        {
            var flood=S.GetComponent<FloodSimulationManager>();if(!flood)return false;
            float progress=S.route.Progress(S.player.transform.position);
            // Nearby companions can be cut off behind the player on rising water.
            // Eligibility must not inspect only the still-dry road ahead.
            for(int i=-6;i<=12;i++)if(flood.GetWaterDepth(S.route.Sample(Mathf.Clamp(progress+i*2,0,S.route.length)))>.02f)return true;
            return false;
        }
        public bool Request()
        {
            if(!CanRequest){S.Notify("Assistance request unavailable","Wait for storm loading, return to supported dry footing near flooded access, then open the guide again. Do not enter floodwater to trigger assistance.");return false;}
            State.assistanceRequested=true;State.assistanceWait=0;State.assistanceOrigin=S.player.transform.position;
            S.GetComponent<SibucaoEvacuationExperience>().Record("assistance-request","Requested evacuation assistance","Reported flooded access and a dry meeting point.","Seeking help is a valid decision when access is cut off.");S.RequestCheckpoint();return true;
        }
        public void Cancel(){State.assistanceRequested=false;State.assistanceWait=0;S.RequestCheckpoint();}
        float nextHint;
        void Update()
        {
            if(!S||S.state==null||S.Mode!=SibucaoMode.Playing)return;
            if(!Pending)
            {
                if(Time.time>=nextHint){nextHint=Time.time+10;if(CanRequest&&!S.state.physical.flags.Contains("assistance_available_notice")){S.physicalDecisions.SetFlag("assistance_available_notice");S.Notify("Reassess flooded access","Do not enter floodwater. J opens the guide: request assisted evacuation from supported dry footing if you cannot continue.");}}
                return;
            }
            if(Vector3.Distance(S.player.transform.position,State.assistanceOrigin)>2||!DryFooting(S.player.transform.position))
            {
                if(S.GetComponent<FloodSimulationManager>()?.IsCatchingUp==true)return;
                Cancel();S.Notify("Meeting point changed","The request was interrupted. Reassess dry footing and request assistance again.");return;
            }
            State.assistanceWait=Mathf.Min(12,State.assistanceWait+Time.deltaTime);
        }
        public bool Board()
        {
            if(!Ready||S.Mode!=SibucaoMode.Playing||!DryFooting(S.player.transform.position)||Vector3.Distance(S.player.transform.position,State.assistanceOrigin)>2)return false;
            var refuge=S.physicalDecisions.evacuationCenter;Vector3 destination=refuge.arrivalPoint.position;
            if(!Physics.Raycast(destination+Vector3.up*2,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore))return false;
            destination=hit.point+Vector3.up*.08f;
            var origin=S.player.transform.position;
            S.GetComponent<SibucaoOptionalSideMission>()?.ArriveByAssistance(origin,destination);
            S.GetComponent<SibucaoRouteIncidentSequence>()?.ArriveByAssistance(origin,destination);
            S.physicalDecisions.Grabber.Release(false);if(S.player.vehicle)S.player.vehicle.Release();
            S.missionGuide.Close();S.inventory.Close();S.player.Teleport(destination,refuge.transform.forward);
            State.assistanceRequested=false;S.physicalDecisions.SetFlag("assisted_evacuation");
            S.GetComponent<SibucaoEvacuationExperience>().Record("assisted-arrival","Arrived by assisted transport","The scenario skips the transport journey to school reception. This was not an on-foot crossing or a simulated rescue-vehicle route.","Report remaining people and hazards; assistance does not complete skipped tasks.");S.RequestCheckpoint();
            // Enter reception in this same call, before route triggers can infer a
            // walking journey through places skipped by the abstracted transfer.
            S.physicalDecisions.CheckEndDestination();
            S.Notify("Assisted transport — time and travel abstracted","You are at school reception. Check in and report anyone who did not travel with you.");return true;
        }
    }
}
