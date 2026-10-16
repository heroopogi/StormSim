using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    // Local development queue. Files are outside Assets and never included in players.
    [InitializeOnLoad]
    public static class SibucaoValidationQueue
    {
        const string Request="Temp/stormsim-validation-request.json";
        const string Response="Temp/stormsim-validation-response.json";
        [Serializable] class Job { public string action,code,role="Student";public int difficulty=1,side;public bool resume; }
        static double next;
        static SibucaoValidationQueue(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+.5;
            if(!File.Exists(Request))return;
            try
            {
                var job=JsonUtility.FromJson<Job>(File.ReadAllText(Request));File.Delete(Request);
                if(job.action=="refresh"){File.WriteAllText(Response,"Refresh requested");AssetDatabase.Refresh();return;}
                if(job.action=="play"){File.WriteAllText(Response,"Play requested");EditorApplication.isPlaying=true;return;}
                if(job.action=="stop"){File.WriteAllText(Response,"Stop requested");EditorApplication.isPlaying=false;return;}
                if(job.action=="unload"){UnloadScene();File.WriteAllText(Response,"Clean saved scene unloaded");return;}
                if(job.action=="restoreScene"){RestoreScene();File.WriteAllText(Response,"Saved validation scene restored");return;}
                if(job.action=="playFresh"){PlayFresh();File.WriteAllText(Response,"Play requested using freshly compiled domain");return;}
                if(job.action=="assistance"){File.WriteAllText(Response,SibucaoDesktopReviewSuite.RunAssistance().ToString());return;}
                if(job.action=="regressions"){SibucaoReleasePipeline.Start();File.WriteAllText(Response,"Regression pipeline started");return;}
                if(job.action=="build"){SibucaoReleaseChecks.BuildDesktop();File.WriteAllText(Response,"Development build completed");return;}
                if(job.action=="review"){SibucaoDesktopReviewSuite.Start();File.WriteAllText(Response,"Compiled desktop review suite started");return;}
                if(job.action=="journey"){var result=SibucaoDesktopReviewSuite.StartJourney(job.difficulty,job.role,job.side,job.resume);File.WriteAllText(Response,result.ToString());return;}
                if(job.action=="buildRelease"){SibucaoReleaseChecks.BuildRelease();File.WriteAllText(Response,"Non-development release build completed");return;}
                if(job.action=="execute")
                {
                    var result=MCPForUnity.Editor.Tools.ExecuteCode.HandleCommand(new Newtonsoft.Json.Linq.JObject{{"action","execute"},{"code",job.code}});
                    File.WriteAllText(Response,Newtonsoft.Json.JsonConvert.SerializeObject(result));return;
                }
                File.WriteAllText(Response,JsonUtility.ToJson(new Status()));
            }
            catch(Exception ex){File.WriteAllText(Response,ex.ToString());}
        }
        static void UnloadScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before releasing scene memory");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(UnityEngine.SceneManagement.SceneManager.sceneCount!=1||scene.isDirty||string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Expected one clean saved scene; preserving workspace");
            File.WriteAllText("Temp/standalone-editor-scene.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {scene=scene.path,releasedUtc=DateTime.UtcNow.ToString("O")}));
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        static void RestoreScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before restoring the validation scene");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving unsaved scene changes");
            var record=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Temp/standalone-editor-scene.json"));
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene((string)record["scene"],UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        static void PlayFresh()
        {
            if(EditorApplication.isPlaying)return;
            bool enabled=EditorSettings.enterPlayModeOptionsEnabled;var options=EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            Action<PlayModeStateChange> restore=null;restore=state=>{if(state==PlayModeStateChange.EnteredPlayMode||state==PlayModeStateChange.EnteredEditMode){EditorSettings.enterPlayModeOptions=options;EditorSettings.enterPlayModeOptionsEnabled=enabled;EditorApplication.playModeStateChanged-=restore;}};
            EditorApplication.playModeStateChanged+=restore;Application.runInBackground=true;EditorApplication.isPlaying=true;
        }
        [Serializable] class Status { public bool playing=EditorApplication.isPlaying; public string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path; }
    }
}
