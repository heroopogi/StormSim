using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoReleaseChecks
    {
        public const string Folder="Captures/Batches4-7_20260930/";
        [MenuItem("StormSim/Validation/Batches 4-7 release checks")]
        public static void Menu()=>Debug.Log(Run());
        public static string Run()
        {
            Directory.CreateDirectory(Folder);var lines=new List<string>();
            void Check(bool ok,string label)=>lines.Add((ok?"PASS ":"FAIL ")+label);
            for(int i=0;i<5;i++)
            {
                var category=(ScoreCategory)i;int cap=SibucaoDecisionEvaluation.Maximum(category);var scores=new int[5];
                SibucaoDecisionEvaluation.ApplyPoints(scores,category,10000);
                Check(scores[i]==cap&&SibucaoDecisionEvaluation.Fraction(category,scores[i])==1,"Full bar matches cap for "+category);
                SibucaoDecisionEvaluation.ApplyPoints(scores,category,-10000);Check(scores[i]==0,"Negative scores stop at zero for "+category);
            }
            var score=new int[5];SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.DecisionMaking,100);Check(score[2]==100,"Decision-Making does not credit itself twice");
            score=new int[5];SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.Teamwork,30);Check(score[4]==30&&score[2]==9,"Side and center scoring share secondary credit");
            score[2]=997;SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.Evacuation,100);Check(score[2]==1000,"Secondary credit stops at cap");
            var settings=new SibucaoSettings();SibucaoControls.Ensure(settings);Check(settings.keyBindings.Length==14,"All shared movement/action bindings initialized");
            Check(SibucaoControls.Rebind(settings,4,Key.F)&&settings.keyBindings[4]=="F"&&settings.keyBindings[5]=="E","Conflicting binding swaps without losing either action");
            Check(!SibucaoControls.Rebind(settings,4,Key.Escape)&&!SibucaoControls.Rebind(settings,4,Key.Tab),"Back and bag alias remain reserved");
            settings.textScale=1.3f;settings.reducedFlashes=true;settings.toggleGrip=true;settings.speechVolume=.3f;
            var copy=JsonUtility.FromJson<SibucaoSettings>(JsonUtility.ToJson(settings));
            Check(copy.keyBindings[4]=="F"&&copy.textScale==1.3f&&copy.reducedFlashes&&copy.toggleGrip&&copy.speechVolume==.3f,"Accessibility settings survive save/reload");
            var legacy=JsonUtility.FromJson<SibucaoSettings>("{\"volume\":0.6,\"sensitivity\":0.11}");
            Check(legacy.textScale==1&&legacy.fieldOfView==70&&legacy.speechVolume==1,"Older settings get usable new defaults");
            var data=new PhysicalRunData();Check(SibucaoDebrief.ResidentStatus(data,5,"Resident").Contains("unconfirmed"),"Absent rescue record never counts as rescued");
            data.history.Add(new PhysicalChoiceResult{node=5,choice=100,success=true});Check(SibucaoDebrief.ResidentStatus(data,5,"Resident").Contains("unconfirmed"),"Side completion never implies center rescue");
            data.history[0].choice=0;Check(SibucaoDebrief.ResidentStatus(data,5,"Resident").Contains("requires confirmation"),"Assistance is distinguished from confirmed arrival");
            data.flags.Add("resident_left_5");Check(SibucaoDebrief.ResidentStatus(data,5,"Resident").Contains("unconfirmed"),"Leaving a resident is represented accurately");
            var joinRoute=ScriptableObject.CreateInstance<SibucaoRoute>();joinRoute.points=new[]{Vector3.zero,new Vector3(0,0,100)};joinRoute.length=100;
            Check(Mathf.Abs(joinRoute.TravelJoinProgress(new Vector3(12,0,10),90)-22)<.001f,"Street rejoin moves ahead of a lateral side-passage exit");
            Check(joinRoute.TravelJoinProgress(new Vector3(30,0,10),20)==20,"Street rejoin never overshoots its destination");
            Check(joinRoute.TravelJoinProgress(new Vector3(12,0,10),0)==0,"Backward recovery rejoin stays within its destination");
            Check(Mathf.Abs(joinRoute.TravelJoinProgress(new Vector3(0,0,31),90)-31)<.001f,"On-street guidance preserves route progress");
            UnityEngine.Object.DestroyImmediate(joinRoute);
            var s=SibucaoSession.Instance;
            if(Application.isPlaying&&s)
            {
                var saved=s.state;var mode=s.Mode;
                try
                {
                    s.state=new RunState();s.physicalDecisions.EnsureNodeCapacity();for(int i=0;i<9;i++)s.state.physical.results[i]=0;
                    s.SetMode(SibucaoMode.ArrivalReview);s.physicalDecisions.ConfirmArrivalReport();
                    Check(s.Mode==SibucaoMode.ArrivalReview&&s.state.physical.arrivalReceived&&!s.state.physical.boundaryReached,"Reception occurs before final handover");
                    s.physicalDecisions.ConfirmArrivalReport();Check(s.Mode==SibucaoMode.Complete&&s.state.physical.arrivalReported,"Handover opens results after required stages");
                    Check(SibucaoDebrief.Summary(s).Contains("No supplies"),"Empty inventory described without fabricated supplies");
                    var bridge=s.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();Check(bridge&&bridge.firstHome==s.homes[0],"Player preparation starts at the documented home");
                }
                finally{s.state=saved;s.SetMode(mode);}
            }
            else lines.Add("NOT RUN reception and scene checks: enter Play Mode");
            int passes=lines.FindAll(x=>x.StartsWith("PASS")).Count,failures=lines.FindAll(x=>x.StartsWith("FAIL")).Count;
            lines.Add($"Result: {passes} passed, {failures} failed. Scripted checks; human acceptance is separate.");
            var text=string.Join("\n",lines);File.WriteAllText(Folder+"release-checks.txt",text);return text;
        }
        [MenuItem("StormSim/Build/Desktop validation player")]
        public static void BuildDesktop()=>BuildDesktop(true);
        [MenuItem("StormSim/Build/Desktop release candidate")]
        public static void BuildRelease()=>BuildDesktop(false);
        static void BuildDesktop(bool development)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before building.");
            string destination=development?"output/DesktopCandidate":"output/DesktopRelease";
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(destination);
            string oldVersion=PlayerSettings.bundleVersion;bool oldTiming=PlayerSettings.enableFrameTimingStats;
            var oldScenes=EditorBuildSettings.scenes;
            var xrGeneral=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var xr=xrGeneral?xrGeneral.Manager:null;
            var oldLoaders=xr?new List<UnityEngine.XR.Management.XRLoader>(xr.activeLoaders):null;
            try
            {
                SibucaoDesktopBuildSanitizer.Begin();
                // Assigned loaders still run native pre-initialization even with auto-start disabled.
                if(xr){if(!xr.TrySetLoaders(new List<UnityEngine.XR.Management.XRLoader>()))throw new InvalidOperationException("Could not isolate desktop XR loaders.");EditorUtility.SetDirty(xr);AssetDatabase.SaveAssetIfDirty(xr);}
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/SIBUCAO_RAIN.unity",true)};
                PlayerSettings.bundleVersion="2026.09.30-b4-7-rc2";PlayerSettings.enableFrameTimingStats=true;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SIBUCAO_RAIN.unity"},locationPathName=destination+"/StormSim.exe",target=BuildTarget.StandaloneWindows64,options=development?BuildOptions.Development:BuildOptions.None});
                var detail=new System.Text.StringBuilder($"Build: 2026.09.30-b4-7-rc2\nDevelopment: {development}\nResult: {report.summary.result}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\n");
                foreach(var step in report.steps)foreach(var message in step.messages)if(message.type==LogType.Error||message.type==LogType.Warning)detail.AppendLine(message.type+": "+message.content);
                File.WriteAllText(Folder+(development?"build-report.txt":"release-build-report.txt"),detail.ToString());
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Desktop build failed; inspect build report and Editor.log.");
                File.WriteAllText(destination+"/BUILD-ID.txt","2026.09.30-b4-7-rc2\nDevelopment: "+development+"\nBuilt UTC: "+DateTime.UtcNow.ToString("O")+"\nBuild success does not establish gameplay or participant acceptance.");
            }
            finally{try{SibucaoDesktopBuildSanitizer.Restore();}finally{try{if(xr){if(!xr.TrySetLoaders(oldLoaders))throw new InvalidOperationException("Could not restore XR loaders after desktop build.");EditorUtility.SetDirty(xr);AssetDatabase.SaveAssetIfDirty(xr);}}finally{EditorBuildSettings.scenes=oldScenes;PlayerSettings.bundleVersion=oldVersion;PlayerSettings.enableFrameTimingStats=oldTiming;}}}
        }
    }
}
