using System;
using System.IO;
using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoSession
    {
        float nextCheckpoint;
        bool checkpointRequested, runStarted;
        public SibucaoMode resumeReturn = SibucaoMode.Menu;
        public string CheckpointStatus { get; private set; } = "No checkpoint this session";
        public string SlotPath(bool checkpoint, bool backup = false) =>
            (checkpoint ? Path.Combine(Path.GetDirectoryName(savePath), SavePrefix+"-checkpoint.json") : savePath) + (backup ? ".bak" : "");
        public bool SlotExists(bool checkpoint, bool backup = false) => File.Exists(SlotPath(checkpoint, backup));
        readonly string[] slotSummaries=new string[4];
        public string SlotSummary(bool checkpoint, bool backup) => slotSummaries[(checkpoint?0:2)+(backup?1:0)];
        string ReadSlotSummary(bool checkpoint, bool backup)
        {
            string path = SlotPath(checkpoint, backup);
            if (!File.Exists(path)) return "Empty";
            try
            {
                var saved = SibucaoSaveStore.Read(path, ValidateSave);
                int seconds=Mathf.FloorToInt(saved.homeSeconds),stage=SibucaoCausalModel.Next(saved.physical);
                return File.GetLastWriteTime(path).ToString("dd MMM HH:mm:ss") + " · " +
                    (seconds/60).ToString("00")+":"+(seconds%60).ToString("00")+" elapsed · "+
                    (stage<9?"junction "+(stage+1)+"/9":"school arrival");
            }
            catch (Exception) { return "Unreadable · try its backup"; }
        }
        void ValidateSave(RunState loaded)
        {
            if (loaded.spawnIndex < 0 || loaded.spawnIndex >= route.spawnPoints.Length ||
                loaded.homeIndex < 0 || homes != null && homes.Length > 0 && loaded.homeIndex >= homes.Length)
                throw new InvalidDataException("Invalid saved starting location.");
            if (loaded.difficultyLevel == 0) loaded.difficultyLevel = 2;
            if (loaded.difficultyLevel < 1 || loaded.difficultyLevel > 5) throw new InvalidDataException("Invalid saved difficulty.");
            if (RouteChoices && loaded.physical.sideMissions != null)
                foreach (var progress in loaded.physical.sideMissions)
                {
                    SibucaoSideMission mission = null;
                    foreach (var section in RouteChoices.sections)
                        foreach (var candidate in new[] { section.left, section.right })
                            if (progress != null && candidate.id == progress.id) mission = candidate;
                    if (!mission || progress.step < 0 || progress.step > mission.steps.Length ||
                        !float.IsFinite(progress.amount) || progress.amount < 0 || progress.amount > 1 || progress.escortPoint < 0 ||
                        mission.route != null && progress.escortPoint >= mission.route.Length)
                        throw new InvalidDataException("Invalid saved task progress.");
                }
            if (physicalDecisions && physicalDecisions.causalModel) physicalDecisions.causalModel.ValidateRun(loaded.physical);
        }
        void CaptureRun()
        {
            if (physicalDecisions) physicalDecisions.SavePhysical();
            state.position = player.transform.position; state.forward = player.transform.forward;
            state.vehicles = SibucaoVehicle.All.ConvertAll(v => v.Capture()).ToArray();
            state.drivenVehicle = player.vehicle ? player.vehicle.vehicleId : null;
        }
        public void RequestCheckpoint() { checkpointRequested = true; }
        void TickCheckpoint()
        {
            if (!runStarted || Time.unscaledTime < nextCheckpoint) return;
#if UNITY_EDITOR
            if (SibucaoEditorInput.Active) return; // Synthetic route fixtures never write player saves.
#endif
            if (checkpointRequested || Time.unscaledTime >= nextCheckpoint + 55) SaveCheckpoint();
        }
        public bool SaveCheckpoint()
        {
            try
            {
                CaptureRun(); SibucaoSaveStore.Write(SlotPath(true), state, ValidateSave);
                CheckpointStatus = "Checkpoint saved " + DateTime.Now.ToString("HH:mm:ss");
                checkpointRequested = false; nextCheckpoint = Time.unscaledTime + 5; return true;
            }
            catch (Exception ex)
            {
                CheckpointStatus = "Checkpoint failed. Use Save game to retry.";
                Notify("Checkpoint could not be saved", ex.Message); nextCheckpoint = Time.unscaledTime + 30; return false;
            }
        }
        public void SaveRun()
        {
            try
            {
                CaptureRun(); SibucaoSaveStore.Write(SlotPath(false), state, ValidateSave);
                Notify("Manual save updated", "The previous manual save is kept as a backup. Checkpoints use a separate slot."); error = "";
            }
            catch (Exception ex) { ShowError("Could not save: " + ex.Message); }
        }
        public void LoadRun() { for(int i=0;i<4;i++)slotSummaries[i]=ReadSlotSummary(i<2,i%2==1);resumeReturn = Mode; SetMode(SibucaoMode.ResumeSelection); }
        public bool LoadSlot(bool checkpoint, bool backup = false)
        {
            try
            {
                string path = SlotPath(checkpoint, backup); bool recovered = backup;
                RunState loaded;
                try { loaded = SibucaoSaveStore.Read(path, ValidateSave); }
                catch (Exception) when (!backup && File.Exists(path + ".bak"))
                { loaded = SibucaoSaveStore.Read(path + ".bak", ValidateSave); recovered = true; }
                // Validate completely before touching the current scene/run.
                ApplyLoadedRun(loaded);
                runStarted = true; checkpointRequested = false; nextCheckpoint = Time.unscaledTime + 5;
                bool updatedAccess=physicalDecisions&&physicalDecisions.RestoredLegacyAccess;
                Notify(recovered ? "Recovered from backup" : updatedAccess ? "Raised access updated" : "Evacuation resumed",
                    (checkpoint ? "Checkpoint" : "Manual save") + (recovered ? " backup loaded. " : " loaded. ") +
                    (updatedAccess ? "Older save: fixed evacuation walkways. Work and supplies kept." : "Work progress is retained; press E or F to re-grip objects."));
                error = ""; return true;
            }
            catch (Exception ex) { ShowError("Could not load: " + ex.Message); return false; }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void UseTestSaveDirectory(string directory)
        { savePath = Path.Combine(directory, "sibucao-rain-save.json"); nextCheckpoint = float.PositiveInfinity; }
#endif
    }
}
