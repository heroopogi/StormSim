using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoSession
    {
        public bool RecoveryAtSchool { get; private set; }
        public SibucaoRouteChoices RouteChoices => physicalDecisions ? physicalDecisions.GetComponent<SibucaoRouteChoices>() : null;
        public void OpenRecovery(bool atSchool = false)
        {
            RecoveryAtSchool = atSchool;
            if (inventory) inventory.Close(); if (missionGuide) missionGuide.Close();
            SetMode(SibucaoMode.Recovery);
        }
        public bool ReturnToUnfinishedJunction()
        {
            var section = RouteChoices ? RouteChoices.Current : null;
            bool rescue=state.physical.needsRescue;
            if (!section&&!rescue) { Notify("No unfinished junction", "Continue toward the school."); return false; }
            // Choose supported, clear footing at the authored approach, never past an unfinished stage.
            var controller = player.GetComponent<CharacterController>();
            Vector3? landing = null;
            var basis=section?section.station.transform:physicalDecisions.evacuationCenter.transform;
            var target=section?section.Approach:physicalDecisions.evacuationCenter.arrivalPoint.position;
            foreach (var offset in new[] { Vector3.zero, basis.right, -basis.right, -basis.forward * 1.5f })
            {
                var candidate = target + offset;
                if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out var ground, 3, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f) continue;
                candidate = ground.point + Vector3.up * .08f;
                bool blocked = false;
                float radius = controller.radius * .9f;
                foreach (var c in Physics.OverlapCapsule(candidate + Vector3.up * (radius + .1f), candidate + Vector3.up * (controller.height - radius), radius, ~0, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(player.transform)) { blocked = true; break; }
                if (!blocked) { landing = candidate; break; }
            }
            if (!landing.HasValue) { Notify("Junction footing is obstructed", "Resume and follow the guide, or load a checkpoint from Pause."); return false; }
            if(RouteChoices)RouteChoices.Working?.Cancel();
            foreach (var station in physicalDecisions.stations) if (station.EngagedBranch) station.EngagedBranch.CancelInteraction();
            physicalDecisions.Grabber.Release(false);
            if(player.vehicle)player.vehicle.Release();
            player.Teleport(landing.Value, basis.forward);
            if(rescue){state.physical.health=50;state.physical.stamina=60;state.physical.submergedSeconds=0;state.physical.needsRescue=false;state.physical.assistedRecoveries++;physicalDecisions.SetFlag("assisted_recovery");}
            physicalDecisions.SetFlag("recovery_used"); SetMode(SibucaoMode.Playing); RequestCheckpoint();
            Notify(rescue?"Assisted recovery recorded":"Returned to unfinished junction", rescue?"Recovered on supported footing with 50 health and 60 stamina. Completed work and remaining supplies are retained.":"Completed work is retained. Inspect a passage or resume your task."); return true;
        }
        public bool RecoverCurrentTask()
        {
            var mission = RouteChoices ? RouteChoices.RecoverableMission : null;
            if (mission && mission.Step != null)
            {
                mission.RecoverCurrentStep(); physicalDecisions.SetFlag("recovery_used");
                SetMode(SibucaoMode.Playing); RequestCheckpoint(); return true;
            }
            int count = physicalDecisions ? physicalDecisions.RecallCurrentTools() : 0;
            Notify(count > 0 ? "Tools returned" : "No loose task tools to recover",
                count > 0 ? "Find them at this junction's original tool positions. Completed actions are retained." : "Use Return to unfinished junction or load a checkpoint.");
            if (count > 0) { SetMode(SibucaoMode.Playing); RequestCheckpoint(); }
            return count > 0;
        }
    }
    public sealed partial class DecisionTreeManager
    {
        public int RecallCurrentTools()
        {
            var s = SibucaoSession.Instance; var section = s.RouteChoices ? s.RouteChoices.Current : null;
            if (!section) return 0;
            int count = 0;
            foreach (var pair in initial)
            {
                var item = pair.Key;
                if (!item || initialStation[item] != section.station || item.secured || item.delivered ||
                    item.kind != PhysicalItemKind.Stick && item.kind != PhysicalItemKind.Strap ||
                    Data.flags.Contains("lost:" + item.persistentId) || Data.flags.Contains("consumed:" + item.persistentId)) continue;
                if (item.holder) item.holder.Release(false);
                item.transform.SetParent(initialParent[item], false);
                item.transform.localPosition = initialLocal[item]; item.transform.localRotation = initialRotation[item];
                item.gameObject.SetActive(true);
                if (!item.Body.isKinematic) { item.Body.linearVelocity = Vector3.zero; item.Body.angularVelocity = Vector3.zero; }
                count++;
            }
            if (count > 0) SetFlag("recovery_used"); Physics.SyncTransforms(); return count;
        }
    }
}
