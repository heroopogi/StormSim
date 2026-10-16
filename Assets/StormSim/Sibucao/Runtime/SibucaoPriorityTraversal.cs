using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed partial class SibucaoPhysicalOption
    {
        bool cancelledUntilMoved;
        Vector3 cancelledAt;
        public bool PacedTraversal=>station&&(station.index==0&&choiceIndex>0||
            station.index==2&&choiceIndex==1||station.index==3&&choiceIndex==2||
            station.index==5&&choiceIndex==2||station.index==6&&choiceIndex==1||
            station.index==8&&choiceIndex>0);
        public bool RequiresPassageWork=>false; // Superseded: hazards, never a fence-clearing prerequisite.
        public bool PassageWorkDone=>RequiresPassageWork&&S.passages.WorkDone(station.index);
        public bool MinimumMovementDone=>!PacedTraversal||Attempt.distance>=Mathf.Min(PathLength*.65f,8)&&Attempt.activeSeconds>1;
        bool MayBegin
        {
            get
            {
                if(cancelledUntilMoved&&FlatDistance(S.player.transform.position,cancelledAt)>2)cancelledUntilMoved=false;
                return !cancelledUntilMoved;
            }
        }
        public bool CancelInteraction()
        {
            if(!Engaged)return false;
            if(encounterRunner)encounterRunner.ResetPresentation();
            if(S.player.grabber&&S.player.grabber.held)S.player.grabber.Release();
            cancelledAt=S.player.transform.position;cancelledUntilMoved=true;
            station.CancelAutomaticEntry(cancelledAt);
            station.manager.Data.branches[station.index]=new PhysicalBranchAttempt();
            if(IsFirstCrossing)
            {
                station.manager.Data.attempts[0]=new PhysicalAttempt();
                station.manager.Data.flags.Remove("depth_probed_0");station.probed=false;
                var probe=tool?tool.GetComponent<SibucaoDepthProbe>():null;if(probe)probe.ClearReading();
                crossingOutsideSince=-1;
            }
            if(flashlight)flashlight.enabled=false;
            S.Notify("Action cancelled","Move away or begin another interaction.");
            return true;
        }
    }
    public sealed partial class PhysicalDecisionStation
    {
        bool automaticEntryCancelled;Vector3 automaticCancelPosition;
        public void CancelAutomaticEntry(Vector3 position){automaticEntryCancelled=true;automaticCancelPosition=position;}
        public void ResetAutomaticEntry(){automaticEntryCancelled=false;}
        public bool AutomaticEntryAllowed
        {
            get
            {
                var p=SibucaoSession.Instance.player.transform.position-automaticCancelPosition;p.y=0;
                if(p.magnitude>2)automaticEntryCancelled=false;
                return !automaticEntryCancelled;
            }
        }
        public bool HasVisibleWater
        {
            get
            {
                var s=SibucaoSession.Instance;var flood=s?s.GetComponent<FloodSimulationManager>():null;
                if(!flood||flood.IsCatchingUp)return false;
                foreach(var offset in new[]{Vector3.zero,Vector3.right*2,Vector3.left*2,Vector3.forward*2,Vector3.back*2})
                {
                    var p=transform.TransformPoint(offset);
                    if(flood.TrySurface(p,out float surface,out float ground)&&surface-Mathf.Max(ground,p.y)>.05f)return true;
                }
                return false;
            }
        }
    }
}
