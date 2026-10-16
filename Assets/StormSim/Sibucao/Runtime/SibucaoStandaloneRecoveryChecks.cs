#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StormSim.Sibucao
{
    // Explicit development-player fixture. It never reads or replaces a player's saves.
    public sealed class SibucaoStandaloneRecoveryChecks : MonoBehaviour
    {
        [Serializable] class DeveloperSettings { public bool enabled; public string serverAddress, accessToken, witClientAccessToken; }
        string folder;
        readonly List<string> lines = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-stormsim-recovery-checks") >= 0)
                new GameObject("Development recovery validation").AddComponent<SibucaoStandaloneRecoveryChecks>();
        }
        void Check(bool value, string label)
        {
            lines.Add((value ? "PASS " : "FAIL ") + label);
            File.WriteAllLines(Path.Combine(folder, "standalone-recovery.txt"), lines);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-stormsim-report");
            folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "recovery-validation");
            Directory.CreateDirectory(folder);
            while (!SibucaoSession.Instance) yield return null;
            var s = SibucaoSession.Instance;
            int oldHome = PlayerPrefs.GetInt("SibucaoLastHome", -1), oldSpawn = PlayerPrefs.GetInt("SibucaoLastSpawn", -1);
            s.UseTestSaveDirectory(Path.Combine(folder, "isolated-saves"));
            bool completed = false;
            try
            {
                var developerAsset = Resources.Load<ScriptableObject>("DevAgentSettings");
                var developerSettings = developerAsset ? JsonUtility.FromJson<DeveloperSettings>(JsonUtility.ToJson(developerAsset)) : new DeveloperSettings();
                Check(!developerSettings.enabled && string.IsNullOrEmpty(developerSettings.serverAddress) && string.IsNullOrEmpty(developerSettings.accessToken) && string.IsNullOrEmpty(developerSettings.witClientAccessToken), "Built player contains no developer assistant connection settings");
                s.StartWithRole("Student"); s.StartWithDifficulty(2);
                yield return null;
                var bridge = s.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();
                if (bridge && bridge.Active) bridge.SkipToDuring();
                if (s.home) s.home.SkipTV();
                float baseClock=s.home?s.home.wakeDuration+s.home.newsDuration+s.home.standDuration+10:90;
                s.state.homeSeconds=baseClock;if(s.home)s.home.Restore();
                yield return null;yield return null;
                var routes = s.RouteChoices; var mission = routes.sections[0].left;
                s.player.Teleport(routes.Current.Approach + Vector3.up * .08f, routes.Current.station.transform.forward);
                Check(s.physicalDecisions.NextCoreStage == 0 && s.state.physical.history.Count == 0, "Preview does not record an outcome");
                mission.closureDepth = 100;
                mission.State.started = true; mission.State.step = 0; mission.Restore();
                mission.Step.item.position += Vector3.right * 2;
                mission.State.hasCarriedPosition = true;
                mission.RecoverCurrentStep();
                Check(!mission.State.hasCarriedPosition && Vector3.Distance(mission.Step.item.localPosition, mission.Step.from) < .001f && mission.State.step == 0, "Dropped side prop returns to its authored pickup without advancing work");
                var tool = UnityEngine.Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.station == routes.Current.station && x.kind == PhysicalItemKind.Stick);
                tool.delivered = tool.secured = false; s.physicalDecisions.RecallCurrentTools();
                var toolOrigin = tool.transform.localPosition; tool.transform.localPosition += Vector3.up * 10;
                Check(s.physicalDecisions.RecallCurrentTools() > 0 && Vector3.Distance(tool.transform.localPosition, toolOrigin) < .01f && s.physicalDecisions.NextCoreStage == 0, "Dropped junction tool recovery preserves ordered progression");
                tool.delivered = true; tool.transform.localPosition += Vector3.up * 10; var deliveredPosition = tool.transform.position; s.physicalDecisions.RecallCurrentTools();
                Check(Vector3.Distance(tool.transform.position, deliveredPosition) < .001f, "Recovery cannot reclaim an already delivered tool");
                tool.delivered = false; s.physicalDecisions.RecallCurrentTools();
                var escort = routes.sections[0].right; escort.closureDepth = 100; escort.State.started = true; escort.State.step = 3; escort.Restore(); routes.Begin(escort);
                var savedRoute = escort.route; var basePoint = escort.companions[0].position; escort.route = new[] { basePoint + Vector3.forward * 3 };
                foreach (var actor in escort.companions) actor.position = basePoint;
                var cage = new GameObject("Development recovery obstruction");
                try
                {
                    foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    {
                        var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.transform.SetParent(cage.transform); block.transform.position = basePoint + side * .6f + Vector3.up;
                        block.transform.localScale = Mathf.Abs(side.x) > 0 ? new Vector3(.12f, 2, 2) : new Vector3(2, 2, .12f);
                    }
                    Physics.SyncTransforms(); s.player.Teleport(escort.WorkTarget, Vector3.forward);
                    for (int i = 0; i < 500; i++) { escort.Tick(.033f); Physics.SyncTransforms(); }
                    Check(escort.State.step == 3 && escort.EscortStatus.Contains("blocked"), "Blocked escort stops and explains recovery in the standalone player");
                }
                finally { cage.SetActive(false); Destroy(cage); escort.route = savedRoute; }
                escort.RecoverCurrentStep();
                Check(escort.State.step == 3 && escort.State.escortPoint == 0 && !routes.Working, "Escort regroup restores its last safe point without awarding progress");
                s.player.Teleport(routes.Current.Approach + Vector3.up * .08f, routes.Current.station.transform.forward);
                mission.State.step = 2; mission.State.amount = .42f; mission.State.started = true;
                mission.Restore(); routes.Begin(mission); mission.Cancel();
                Check(!routes.Working && mission.State.step == 2 && Mathf.Abs(mission.State.amount - .42f) < .001f, "Cancellation preserves partial work");
                s.RecoverCurrentTask();
                Check(mission.State.step == 2 && mission.State.amount == 0, "Recovery resets only the current mechanism action");
                s.state.homeSeconds = baseClock+10; s.SaveRun();
                Check(s.SlotExists(false) && string.IsNullOrEmpty(s.error), "Manual save writes in the standalone player");
                string manual = File.ReadAllText(s.SlotPath(false));
                mission.State.amount = .42f; routes.Begin(mission); s.state.homeSeconds = baseClock+20;
                Check(s.SaveCheckpoint(), "Mid-task checkpoint writes");
                Check(File.ReadAllText(s.SlotPath(false)) == manual, "Checkpoint preserves manual-save bytes");
                s.state.homeSeconds = baseClock+30;
                Check(s.SaveCheckpoint() && s.SlotExists(true, true), "Checkpoint keeps a previous valid backup");
                mission.State.step = 0; mission.State.amount = 0;
                Check(s.LoadSlot(true) && routes.Working == mission && mission.State.step == 2 && Mathf.Abs(mission.State.amount - .42f) < .001f, "Standalone loading restores active partial work");
                File.WriteAllText(s.SlotPath(true), "{truncated");
                Check(s.LoadSlot(true) && Mathf.Abs(s.state.homeSeconds - (baseClock+20)) < .01f && s.toastTitle.Contains("backup"), "Corrupt checkpoint recovers its valid backup with a notice");
                Check(File.ReadAllText(s.SlotPath(false)) == manual, "Recovery preserves the separate manual slot");
                mission.Cancel(); s.SaveCheckpoint();
                Check(s.LoadSlot(true) && !routes.Working && routes.RecoverableMission == mission, "Cancelled work stays recoverable after loading");
                var current = s.state;
                File.WriteAllText(s.SlotPath(true), "{}"); File.WriteAllText(s.SlotPath(true, true), "{}");
                Check(!s.LoadSlot(true) && ReferenceEquals(current, s.state), "Invalid saves cannot replace the current run");
                s.SetMode(SibucaoMode.Playing);
                s.player.Teleport(s.physicalDecisions.evacuationCenter.arrivalPoint.position, Vector3.forward);
                s.physicalDecisions.CheckEndDestination();
                Check(s.Mode == SibucaoMode.Recovery && !s.state.physical.boundaryReached, "Unfinished school arrival explains recovery instead of completing the run");
                Check(s.ReturnToUnfinishedJunction() && s.physicalDecisions.NextCoreStage == 0, "School recovery returns to the unfinished junction without awarding progress");
                s.state.homeSeconds = 1800; s.state.elapsed = 1800;
                var flood = s.GetComponent<FloodSimulationManager>(); flood.RestoreClock();
                yield return null;
                Check(!flood.IsCatchingUp && s.state.homeSeconds >= 1800, "Late clock restore evaluates the flood without historical replay");
                for (int i = 0; i < 9; i++) s.state.physical.results[i] = 0;
                s.SetMode(SibucaoMode.ArrivalReview); s.physicalDecisions.ConfirmArrivalReport();
                Check(s.Mode == SibucaoMode.ArrivalReview && s.state.physical.arrivalReceived && !s.state.physical.arrivalReported, "Reception precedes handover in the standalone player");
                s.physicalDecisions.ConfirmArrivalReport();
                Check(s.Mode == SibucaoMode.Complete && s.state.physical.arrivalReported, "Handover opens the debrief");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "standalone-handover.png"));
                yield return null;
                completed = true;
            }
            finally
            {
                if (!completed) Check(false, "Validation interrupted; inspect Player.log for the exception");
                if (oldHome < 0) PlayerPrefs.DeleteKey("SibucaoLastHome"); else PlayerPrefs.SetInt("SibucaoLastHome", oldHome);
                if (oldSpawn < 0) PlayerPrefs.DeleteKey("SibucaoLastSpawn"); else PlayerPrefs.SetInt("SibucaoLastSpawn", oldSpawn);
                PlayerPrefs.Save();
                lines.Add("Staged development-player API checks; not continuous traversal or participant acceptance.");
                File.WriteAllLines(Path.Combine(folder, "standalone-recovery.txt"), lines);
                File.WriteAllText(Path.Combine(folder, "finished.txt"), "Finished; inspect every assertion and Player.log.");
                Application.Quit();
            }
        }
    }
}
#endif
