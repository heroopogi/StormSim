using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    public static class PreparationCoachingChecks
    {
        public static string Run(PreparednessSession s)
        {
            var game=SibucaoSession.Instance;var p=s.player;
            var baseline=s.Capture();var lines=new List<string>();
            bool hints=game.settings.hints;float delay=s.evacuationAfterSeconds;
            void Check(bool pass,string label){if(!pass)throw new Exception(label);lines.Add("PASS: "+label);}
            void Update()=>typeof(PreparednessSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
            try
            {
                p.CloseDesktopPanels();s.SetPhase(PrepPhase.Preparing);s.BeginCoachingChoice();
                s.preparationSeconds=5;s.phaseTime=8;s.elapsed=10;s.evacuationAfterSeconds=5;
                Update();
                Check(s.CoachingChoicePending&&p.ReadingPanel&&!s.CanPrepare,"Opening choice pauses preparation actions");
                Check(s.preparationSeconds==5&&s.phaseTime==8&&s.elapsed==10&&!s.evacuationOrdered,"Opening choice pauses all preparation clocks and advisory");
                s.SetCoaching(false);s.evacuationAfterSeconds=delay;
                Check(!s.CoachingChoicePending&&!s.Guided&&s.CanPrepare,"Explore selection closes choice and permits preparation");
                Check(s.NextPreparationStep=="Prepare essentials. Recheck the TV before leaving or resting.","Explore shows a brief objective instead of step-by-step coaching");
                var item=s.items.First(i=>i.kind==PrepKind.Documents);p.focused=item;
                Check(p.ContextAction().StartsWith("E  Pick up")&&!p.ContextAction().Contains("\n"),"Explore retains a small contextual pickup hint");
                p.focused=null;Check(p.ContextAction()==null,"Explore does not repeat tutorial hints when nothing is targeted");
                s.tutorialProgress=3;var before=s.Capture();s.SetCoaching(true);
                Check(s.Guided&&s.tutorialProgress==3,"Enabling coaching resumes existing tutorial progress");
                Check(s.TutorialSummary.Contains("[done] Open storage")&&s.TutorialSummary.Contains("[  ] Check"),"Tutorial shows completed and remaining hands-on steps");
                s.SetCoaching(false);var after=s.Capture();
                Check(after.tutorialProgress==before.tutorialProgress&&after.preparationSeconds==before.preparationSeconds&&after.decisions.SequenceEqual(before.decisions)&&after.items.Select(i=>i.storedIn).SequenceEqual(before.items.Select(i=>i.storedIn)),"Switching coaching preserves timer, readiness, inventory, and tutorial progress");
                p.ToggleGuide();Check(p.GuideOpen&&!s.Guided,"Explore players can open their guide without enabling coaching");p.CloseDesktopPanels();
                var exploreSave=JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(s.Capture()));s.SetCoaching(true);s.Restore(exploreSave);
                Check(!s.Guided&&!s.CoachingChoicePending,"Explore preference survives save serialization and restore");
                s.SetCoaching(true);var guidedSave=JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(s.Capture()));s.SetCoaching(false);s.Restore(guidedSave);
                Check(s.Guided,"Guided preference survives save serialization and restore");
                guidedSave.coachingMode=0;game.settings.hints=false;s.Restore(guidedSave);
                Check(!s.Guided,"Legacy saves retain the existing hint preference");
                game.settings.hints=true;Check(s.Guided,"Settings toggle enables preparation coaching immediately");
                game.settings.hints=false;Check(!s.Guided,"Settings toggle disables preparation coaching immediately");
                s.tutorialProgress=15;s.SetCoaching(true);Check(s.TutorialComplete&&s.TutorialSummary.StartsWith("Tutorial complete"),"Completed tutorial has a clear completion state");
                s.SetCoaching(false);s.IssueEvacuation();
                Check(s.NextPreparationStep.Contains("exit")&&!s.OutdoorSafe,"Explore mode preserves urgent evacuation guidance and outdoor restrictions");
            }
            finally{s.Restore(baseline);s.evacuationAfterSeconds=delay;game.settings.hints=hints;p.CloseDesktopPanels();}
            return string.Join("\n",lines);
        }
    }
}
