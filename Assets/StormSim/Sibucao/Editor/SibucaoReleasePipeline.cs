using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    // Runs each fixture on a separate editor update so input cooldowns can expire.
    public static class SibucaoReleasePipeline
    {
        static int step;static double next;static bool oldDev;
        [MenuItem("StormSim/Validation/Run desktop regression pipeline")]
        public static void Start()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            Application.runInBackground=true;EditorApplication.isPaused=false;
            Directory.CreateDirectory(SibucaoReleaseChecks.Folder);step=0;next=0;
            string archive=Path.Combine(SibucaoReleaseChecks.Folder,"previous-reports-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            foreach(string directory in new[]{"Captures/Batch2_20260929","Captures/Batch3_20260930"})
                if(Directory.Exists(directory))foreach(string report in Directory.GetFiles(directory,"*.txt"))
                {string destination=Path.Combine(archive,Path.GetFileName(directory));Directory.CreateDirectory(destination);File.Copy(report,Path.Combine(destination,Path.GetFileName(report)),true);}
            const string routes="Documentation/SIBUCAO_THREE_WAY_INTERACTION_CHECKS.txt";
            if(File.Exists(routes)){Directory.CreateDirectory(archive);File.Copy(routes,Path.Combine(archive,Path.GetFileName(routes)),true);}
            oldDev=EditorPrefs.GetBool(SibucaoDevelopmentControls.EditorPreference,false);EditorPrefs.SetBool(SibucaoDevelopmentControls.EditorPreference,true);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            var s=SibucaoSession.Instance;if(!s)return;
            string name="pipeline-"+step;
            try
            {
                if(step==0)
                {
                    s.UseTestSaveDirectory(Path.GetFullPath(SibucaoReleaseChecks.Folder+"isolated-saves"));
                    s.StartWithRole("Student");s.StartWithDifficulty(2);
                    var bridge=s.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();if(bridge&&bridge.Active)bridge.SkipToDuring();
                    if(s.home)s.home.SkipTV();SibucaoEditorInput.Active=true;step++;next=EditorApplication.timeSinceStartup+3;return;
                }
                string result;
                switch(step)
                {
                    case 1:name="release-checks";result=SibucaoReleaseChecks.Run();break;
                    case 2:name="batch2-regression";result=SibucaoBatch2Checks.Run();break;
                    case 3:name="batch3-regression";result=SibucaoBatch3Checks.Run();break;
                    case 4:name="compatibility";result=SibucaoBatch3Checks.Compatibility();break;
                    case 5:name="physical-access";result=SibucaoBatch3Checks.Access();break;
                    case 6:name="side-interactions";result=SibucaoRouteChoiceChecks.RunInteractions();break;
                    case 7:name="route-structure";result=SibucaoRouteChoiceChecks.Validate();break;
                    default:File.WriteAllText(SibucaoReleaseChecks.Folder+"pipeline-finished.txt","Finished. Inspect every report for FAIL before accepting tasks.");Finish();return;
                }
                File.WriteAllText(SibucaoReleaseChecks.Folder+name+".txt",result);step++;
            }
            catch(Exception ex){File.WriteAllText(SibucaoReleaseChecks.Folder+name+"-exception.txt",ex.ToString());step++;}
        }
        static void Finish(){EditorApplication.update-=Tick;EditorPrefs.SetBool(SibucaoDevelopmentControls.EditorPreference,oldDev);SibucaoEditorInput.Reset();}
    }
}
