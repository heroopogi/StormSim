using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoBatch2Checks
    {
        [MenuItem("StormSim/Validation/Batch 2 recovery and saves")]
        public static void Menu() { Debug.Log(Run()); }
        public static string Run()
        {
            var s = SibucaoSession.Instance;
            if (!Application.isPlaying || !s) return "Enter Play Mode first.";
            var log = new List<string>(); var d = s.RouteChoices; var manager = s.physicalDecisions;
            var original = s.state; var originalHome = s.home; var originalPosition = s.player.transform.position;
            var originalForward = s.player.transform.forward;
            string directory = Path.GetFullPath("Captures/Batch2_20260929/save-fixture-" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(directory); s.UseTestSaveDirectory(directory);
            void Check(bool condition, string name) { log.Add((condition ? "PASS " : "FAIL ") + name); }
            void Stage(int index)
            {
                manager.Grabber.Release(false); s.home = null; s.state = new RunState(); manager.EnsureNodeCapacity();
                for (int i = 0; i < index; i++) manager.Data.results[i] = 0;
                manager.Data.causal = new CausalRun { path = SibucaoCausalModel.Path(manager.Data) };
                d.ApplyLayout(); s.SetMode(SibucaoMode.Playing); Physics.SyncTransforms();
            }
            try
            {
                SibucaoEditorInput.Active = true; Stage(0);
                var m = d.sections[0].left; m.closureDepth = 100;
                s.player.Teleport(d.Current.Approach,d.Current.station.transform.forward);
                Check(d.Previewing && manager.Data.history.Count == 0, "preview records no outcome");
                m.State.step = 2; m.Restore();
                var step = m.Step; var point = step.item.GetComponentInChildren<Collider>().bounds.center;
                s.player.Teleport(point - m.transform.forward * 1.2f, m.transform.forward); s.player.view.transform.LookAt(point);
                m.Interact(); SibucaoEditorInput.UseHeld = true; SibucaoEditorInput.ToolTurn = Vector2.zero; m.Tick(.2f);
                Check(m.State.amount == 0, "idle hold does not advance mechanism");
                SibucaoEditorInput.ToolTurn = new Vector2(0, -20); m.Tick(.2f);
                Check(m.State.amount == 0, "vertical drag does not contradict RIGHT hint");
                SibucaoEditorInput.ToolTurn = new Vector2(20, 0); m.Tick(.2f);
                Check(m.State.amount > 0 && m.Hint.Contains("RIGHT"), "right drag advances with explicit direction and percent");
                float partial = m.State.amount; m.Cancel();
                Check(m.State.amount == partial && m.State.step == 2 && !d.Working, "cancel retains partial work and completed steps");
                s.RecoverCurrentTask(); Check(m.State.amount == 0 && m.State.step == 2, "recover mechanism resets current action only");
                m.State.amount = 1; m.Interact(); SibucaoEditorInput.UseHeld = false; m.Tick(.02f);
                Check(m.State.step == 3 && manager.Data.history.Count == 0, "release commits one action, not a route outcome");
                m.State.step = m.steps.Length; d.Complete(m);
                Check(!m.State.complete && manager.Data.history.Count == 0, "unfinished merge cannot commit route");

                Stage(0); m = d.sections[0].left; m.closureDepth = 100; m.Interact();
                m.Step.item.position = m.Step.target.position + Vector3.right * .25f; m.State.carryDistance = 1;
                Check(m.PlacementReady, "placement readiness includes target radius and travel");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Batch 2 temporary placement obstruction"; wall.transform.position = m.Step.target.position + Vector3.right * .125f; wall.transform.localScale = new Vector3(.035f, 2, 2); Physics.SyncTransforms();
                Check(!m.PlacementReady, "foreign wall prevents placement through obstruction");
                m.ReleaseCarry(); Check(m.State.step == 0 && !d.HeldBy, "failed placement puts down without completing action");
                UnityEngine.Object.DestroyImmediate(wall); m.RecoverCurrentStep();
                Check(!m.State.hasCarriedPosition && Vector3.Distance(m.Step.item.localPosition, m.Step.from) < .001f, "dropped side prop returns to authored pickup");

                Stage(0); var escort = d.sections[0].right; escort.closureDepth = 100; escort.State.step = 3; escort.Restore(); d.Begin(escort);
                var route = escort.route; var basePoint = escort.companions[0].position; escort.route = new[] { basePoint + Vector3.forward * 3 };
                foreach (var actor in escort.companions) actor.position = basePoint;
                var cage = new GameObject("Batch 2 temporary escort obstructions");
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                { var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.transform.SetParent(cage.transform); block.transform.position = basePoint + side * .6f + Vector3.up; block.transform.localScale = Mathf.Abs(side.x) > 0 ? new Vector3(.12f, 2, 2) : new Vector3(2, 2, .12f); }
                Physics.SyncTransforms(); s.player.Teleport(escort.WorkTarget, Vector3.forward);
                for (int i = 0; i < 500; i++) { escort.Tick(.033f); Physics.SyncTransforms(); }
                Check(escort.State.step == 3 && escort.EscortStatus.Contains("blocked"), "blocked escort stops and explains recovery");
                UnityEngine.Object.DestroyImmediate(cage); escort.route = route; escort.RecoverCurrentStep();
                Check(escort.State.step == 3 && escort.State.escortPoint == 0 && !d.Working, "regroup restores last safe point without advance");

                Stage(0);var tool=UnityEngine.Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.station==d.Current.station&&x.kind==PhysicalItemKind.Stick);
                tool.delivered=tool.secured=false;manager.RecallCurrentTools();var originalTool=tool.transform.localPosition;tool.transform.localPosition+=Vector3.up*10;
                int recalled=manager.RecallCurrentTools();
                Check(recalled>0&&Vector3.Distance(tool.transform.localPosition,originalTool)<.01f&&manager.NextCoreStage==0,"loose junction tool recovery preserves ordered progression");
                tool.delivered=true;tool.transform.localPosition+=Vector3.up*10;var deliveredPosition=tool.transform.position;manager.RecallCurrentTools();
                Check(Vector3.Distance(tool.transform.position,deliveredPosition)<.001f,"tool recovery cannot reclaim a delivered tool");tool.delivered=false;

                Stage(0); m = d.sections[0].left; m.closureDepth = 100; s.state.homeSeconds = 10; s.SaveRun();
                string manual = File.ReadAllText(s.SlotPath(false));
                m.State.step = 2; m.State.amount = .42f; m.State.started = true; m.Restore(); d.Begin(m);
                d.sections[0].RestoreFloodLift(2); float lift = d.sections[0].FloodLift;
                s.player.Teleport(m.Step.item.position + m.transform.forward * 1.2f, -m.transform.forward);
                s.state.homeSeconds = 20; Check(s.SaveCheckpoint(), "checkpoint write succeeds");
                Check(File.ReadAllText(s.SlotPath(false)) == manual, "checkpoint never overwrites manual bytes");
                s.state.homeSeconds = 30; Check(s.SaveCheckpoint() && s.SlotExists(true, true), "checkpoint rotates previous valid backup");
                m.State.step = 0; m.State.amount = 0;
                Check(s.LoadSlot(true) && d.Working == m && m.State.step == 2 && Mathf.Abs(m.State.amount - .42f) < .001f, "mid-mechanism checkpoint restores partial work and active route");
                Check(Mathf.Abs(d.sections[0].FloodLift - lift) < .001f, "checkpoint immediately restores raised deck height");
                s.home = null; File.WriteAllText(s.SlotPath(true), "{truncated");
                Check(s.LoadSlot(true) && s.state.homeSeconds == 20 && s.toastTitle.Contains("backup"), "corrupt checkpoint recovers previous valid backup with notice");
                Check(File.ReadAllText(s.SlotPath(false)) == manual, "loading checkpoint leaves manual file unchanged");
                s.home = null; s.state.homeSeconds = 40; s.SaveRun();
                Check(File.ReadAllText(s.SlotPath(false, true)) == manual, "manual replacement retains previous manual backup");
                File.WriteAllText(s.SlotPath(false), "{}"); s.state.homeSeconds = 50; s.SaveRun();
                Check(File.ReadAllText(s.SlotPath(false, true)) == manual, "corrupt primary cannot overwrite last-good manual backup");
                var current = s.state; File.WriteAllText(s.SlotPath(true), "{}"); File.WriteAllText(s.SlotPath(true, true), "{}");
                Check(!s.LoadSlot(true) && ReferenceEquals(current, s.state), "invalid primary and backup fail without replacing current run");
                string denied=Path.Combine(directory,"not-a-directory");File.WriteAllText(denied,"fixture");bool rejected=false;
                string manualBeforeFailure=File.ReadAllText(s.SlotPath(false));
                try{SibucaoSaveStore.Write(Path.Combine(denied,"save.json"),s.state,null);}catch(IOException){rejected=true;}
                Check(rejected&&File.ReadAllText(s.SlotPath(false))==manualBeforeFailure,"failed write preserves existing save bytes");

                Stage(0); m = d.sections[0].left; m.closureDepth = 100; m.Interact();
                m.Step.item.position += m.transform.right * .4f; m.Tick(.03f); var carried = m.Step.item.position; s.state.homeSeconds = 60;
                s.SaveCheckpoint(); m.Step.item.position += Vector3.up * 4;
                Check(s.LoadSlot(true) && d.Working == m && !d.HeldBy && Vector3.Distance(m.Step.item.position, carried) < .02f, "mid-carry reload preserves object position and deliberately releases grip");
                s.home = null; m.Cancel(); s.SaveCheckpoint(); s.LoadSlot(true);
                Check(!d.Working && d.RecoverableMission == m, "cancelled mission remains paused after load and stays recoverable");

                Stage(0); escort = d.sections[0].right; escort.State.started = true; escort.State.step = 3; escort.Restore(); d.Begin(escort);
                escort.companions[0].position += Vector3.right * .37f; var companionPosition = escort.companions[0].position;
                s.SaveCheckpoint(); escort.companions[0].position += Vector3.forward * 2; s.LoadSlot(true);
                Check(d.Working == escort && Vector3.Distance(escort.companions[0].position, companionPosition) < .02f, "mid-escort reload restores exact companion position");

                Stage(0); var bag = s.inventory.Find("prep:bag");
                var supplies = UnityEngine.Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => SibucaoInventory.Storable(x) && x.persistentId.StartsWith("prep:")).ToArray();
                if (!bag || supplies.Length < 3) throw new Exception("Preparation inventory fixture is missing.");
                bag.delivered = bag.secured = false; s.inventory.Collect(bag);
                var item = supplies.First(x => x.inventoryTag != "food"); item.delivered = false; s.inventory.Collect(item);
                Check(s.inventory.ItemStatus(item).StartsWith("Stored"), "stored status reflects slot ownership");
                s.inventory.Selected = Array.IndexOf(manager.Data.inventorySlots, item.persistentId); s.inventory.EquipSelected();
                Check(item.IsHeld && s.inventory.ItemStatus(item).StartsWith("Equipped") && manager.Grabber.ToggleHeld, "take out equips with persistent F carry grip");
                manager.Grabber.Release(false); Check(s.inventory.ItemStatus(item).Contains("ground") || s.inventory.ItemStatus(item).Contains("Ready"), "released supply remains recoverable");
                s.inventory.Collect(item); s.inventory.RemoveTag(item.inventoryTag, true);
                Check(s.inventory.ItemStatus(item).StartsWith("Lost") && !s.inventory.Collect(item), "lost supply remains named and cannot be recollected");
                var second = supplies.First(x => x != item); second.delivered = false; s.inventory.Collect(second); s.inventory.RemoveTag(second.inventoryTag, false);
                Check(s.inventory.ItemStatus(second).StartsWith("Consumed") && !s.inventory.Collect(second), "consumed supply remains named and unavailable");
                manager.SetFlag("broken_bag:" + bag.persistentId);
                Check(s.inventory.BagStatus.StartsWith("Damaged"), "damaged equipped bag is identified");
                manager.Data.backpackId = null; s.inventory.Open();
                Check(s.inventory.IsOpen && s.inventory.ShowStatus, "item status remains accessible after losing equipped bag"); s.inventory.Close();

                Stage(0); s.player.Teleport(manager.evacuationCenter.arrivalPoint.position, Vector3.forward); manager.CheckEndDestination();
                Check(s.Mode == SibucaoMode.Recovery && s.RecoveryAtSchool && !manager.Data.boundaryReached, "school arrival explains earliest unfinished junction without false completion");
                int score = s.state.scores.Sum(); bool returned = s.ReturnToUnfinishedJunction();
                Check(returned && SibucaoRouteChoices.Flat(s.player.transform.position, d.Current.Approach) < 2 && manager.NextCoreStage == 0 && s.state.scores.Sum() == score, "school recovery returns to supported junction without awarding progress");
            }
            catch (Exception ex) { log.Add("FAIL fixture exception: " + ex); }
            finally
            {
                SibucaoEditorInput.Reset(); manager.Grabber.Release(false); s.state = original; s.home = originalHome;
                manager.RestoreRun(); s.player.Teleport(originalPosition, originalForward); s.SetMode(SibucaoMode.Paused);
            }
            File.WriteAllLines("Captures/Batch2_20260929/recovery-save-checks.txt", log);
            return string.Join("\n", log);
        }
    }
}
