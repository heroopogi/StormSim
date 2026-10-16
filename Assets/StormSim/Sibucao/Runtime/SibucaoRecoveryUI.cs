using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoUI
    {
        bool PanelButton(Rect r, float y, float h, string text) => GUI.Button(new Rect(r.x + 22, y, r.width - 44, h), text, SibucaoDesktopHud.ButtonStyle);
        void RecoveryPanel()
        {
            var l = SibucaoHudLayout.Current; var old = SibucaoDesktopHud.Begin(l); var r = l.Modal;
            Fill(new Rect(0, 0, l.Width, l.Height), new Color(.02f, .05f, .06f, .94f)); SibucaoDesktopHud.Panel(r);
            bool rescue=S.state.physical.needsRescue;
            SibucaoDesktopHud.Label(new Rect(r.x + 22, r.y + 18, r.width - 44, 30), rescue?"You need assistance":S.RecoveryAtSchool ? "School arrival · unfinished route" : "Recover your task", SibucaoDesktopHud.Heading);
            var c = S.RouteChoices ? S.RouteChoices.Current : null; var mission = S.RouteChoices ? S.RouteChoices.RecoverableMission : null;
            string detail = c ? "Junction " + (c.station.index + 1) + " / 9: " + DecisionTreeManager.Titles[c.station.index] : "All nine junctions are recorded. Continue to the school.";
            if (c) detail += "\n" + (mission ? mission.title + " · " + mission.State.step + " / " + mission.steps.Length + " actions done." : "No passage has been completed here.");
            if(rescue)detail+="\nYour health reached zero. Request assisted recovery on supported footing, or load a saved run. Recovery is recorded and preserves completed work.";
            else if (S.RecoveryAtSchool) detail += "\nSchool completion requires a recorded passage at every junction. Return to this earliest unfinished junction to continue.";
            else detail += "\nQ pauses side work and keeps completed steps. Recovery resets only the current prop or regroups the escort at its last waypoint.";
            SibucaoDesktopHud.Label(new Rect(r.x + 22, r.y + 62, r.width - 44, 120), detail);
            SibucaoDesktopHud.Label(new Rect(r.x + 22, r.y + 190, r.width - 44, 55), "Recovery gives no completion credit or supplies. Lost and consumed items stay recorded. Manual saves and checkpoints are available from Pause.", SibucaoDesktopHud.Small, SibucaoDesktopHud.Muted);
            float h = Mathf.Min(46, (r.height - 295) / 3); float y = r.yMax - (h + 8) * 3 - 26;
            bool enabled = GUI.enabled; GUI.enabled = enabled && (c||rescue);
            if (PanelButton(r, y, h, rescue?"Request assistance · keep completed work":"Return to unfinished junction · keep progress")) S.ReturnToUnfinishedJunction(); y += h + 8;
            GUI.enabled=enabled&&c&&!rescue;
            if (PanelButton(r, y, h, mission && mission.Step != null ? "Reset current prop / regroup escort" : "Return loose junction tools")) S.RecoverCurrentTask(); y += h + 8;
            GUI.enabled = enabled;
            if (PanelButton(r, y, h, rescue?"Choose a saved run":"Resume here / Esc")){if(rescue)S.LoadRun();else S.SetMode(SibucaoMode.Playing);}
            GUI.matrix = old;
        }
        void ResumePanel()
        {
            var l = SibucaoHudLayout.Current; var old = SibucaoDesktopHud.Begin(l); var r = l.Modal;
            Fill(new Rect(0, 0, l.Width, l.Height), new Color(.02f, .05f, .06f, .94f)); SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x + 22, r.y + 18, r.width - 44, 30), "Choose a saved run", SibucaoDesktopHud.Heading);
            SibucaoDesktopHud.Label(new Rect(r.x + 22, r.y + 60, r.width - 44, 52), "Loading replaces your current unsaved progress. Checkpoints never overwrite the manual slot. Each backup is the previous valid version.", SibucaoDesktopHud.Small, SibucaoDesktopHud.Muted);
            float h = Mathf.Min(64, (r.height - 194) / 4), y = r.y + 120;
            for (int i = 0; i < 4; i++)
            {
                bool checkpoint = i < 2, backup = i % 2 == 1;
                string label = (checkpoint ? "Checkpoint" : "Manual save") + (backup ? " · backup" : "") + "\n" + S.SlotSummary(checkpoint, backup);
                bool enabled = GUI.enabled; GUI.enabled = enabled && S.SlotExists(checkpoint, backup);
                if (PanelButton(r, y, h, label)) S.LoadSlot(checkpoint, backup);
                GUI.enabled = enabled; y += h + 6;
            }
            if (PanelButton(r, r.yMax - 58, 36, "Back / Esc")) S.SetMode(S.resumeReturn);
            GUI.matrix = old;
        }
        void DrawPlacementTarget()
        {
            var mission = S.RouteChoices ? S.RouteChoices.HeldBy : null;
            if (!mission || mission.Step == null || !mission.Step.target) return;
            var cam = S.player.view; var p = cam.WorldToScreenPoint(mission.Step.target.position);
            if (p.z <= 0) return;
            var l = SibucaoHudLayout.Current; var old = SibucaoDesktopHud.Begin(l);
            var mark = new Rect(p.x / l.Scale - 12, (Screen.height - p.y) / l.Scale - 12, 24, 24);
            if (mark.xMin > l.Objective.xMax + 12 && mark.xMax < l.Vitals.xMin - 12 && mark.yMin > 25 && mark.yMax < l.Subtitle.yMin - 8)
                Border(mark, mission.PlacementReady ? SibucaoDesktopHud.Accent : Warning);
            GUI.matrix = old;
        }
    }
}
