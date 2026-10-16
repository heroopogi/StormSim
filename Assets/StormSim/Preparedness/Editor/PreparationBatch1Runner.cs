using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    [InitializeOnLoad]
    public static class PreparationBatch1Runner
    {
        const string Folder="Reports/PreparationBatch1_20261013/";
        const string Request=Folder+"request.txt";
        static PreparationBatch1Runner(){EditorApplication.update+=Tick;}
        [MenuItem("StormSim/Validation/All Preparation Batches")]
        public static void RequestAllRun(){Directory.CreateDirectory(Folder);File.WriteAllText(Request,"remaining-start");}

        [MenuItem("StormSim/Validation/Preparation Coaching")]
        public static void RequestCoachingRun()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Request,"coaching-start");
        }

        [MenuItem("StormSim/Validation/Preparation Batch 1")]
        public static void RequestRun()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Request,"start");
        }

        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            try
            {
                string phase=File.ReadAllText(Request).Trim();
                if(phase=="cancel-check"&&!EditorApplication.isPlaying)
                {
                    VerifyCancel();File.Delete(Request);return;
                }
                bool remaining=phase.StartsWith("remaining-");
                bool coaching=remaining||phase.StartsWith("coaching-");
                string prefix=remaining?"remaining-":coaching?"coaching-":"";
                string reportFolder=remaining?"Reports/PreparationRemaining_20261014/":coaching?"Reports/PreparationCoaching_20261014/":Folder;
                if(phase==prefix+"start")
                {
                    if(SceneManager.GetActiveScene().path!="Assets/Scenes/SIBUCAO_RAIN.unity")throw new Exception("Open SIBUCAO_RAIN before running this fixture.");
                    if(EditorApplication.isPlaying)throw new Exception("Stop the existing play session before starting this fixture.");
                    Directory.CreateDirectory(reportFolder);File.WriteAllText(reportFolder+"status.txt","ENTERING PLAY MODE");
                    File.WriteAllText(Request,prefix+"waiting");EditorApplication.EnterPlaymode();return;
                }
                if(phase==prefix+"waiting"&&EditorApplication.isPlaying&&SibucaoSession.Instance)
                {
                    File.WriteAllText(Request,prefix+"running");VerifyCancel();PreparationBatch1Live.Run(coaching,remaining);return;
                }
                if(phase==prefix+"running")
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    string status=File.Exists(reportFolder+"status.txt")?File.ReadAllText(reportFolder+"status.txt"):"RUNNING";
                    if(status.StartsWith("PASS:")||status.StartsWith("FAIL:"))
                    {File.Delete(Request);EditorApplication.ExitPlaymode();}
                }
            }
            catch(Exception error)
            {
                string request=File.Exists(Request)?File.ReadAllText(Request):"";
                string reportFolder=request.StartsWith("remaining-")?"Reports/PreparationRemaining_20261014/":request.StartsWith("coaching-")?"Reports/PreparationCoaching_20261014/":Folder;
                Directory.CreateDirectory(reportFolder);File.WriteAllText(reportFolder+"status.txt","FAIL: "+error);
                File.Delete(Request);Debug.LogException(error);
            }
        }

        static void VerifyCancel()
        {
            var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Preparation cancel regression");SceneManager.MoveGameObjectToScene(root,preview);
                var session=root.AddComponent<PreparednessSession>();session.items=new PreparednessItem[0];session.phase=PrepPhase.Preparing;
                var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform);
                var player=avatar.AddComponent<PreparednessPlayer>();player.session=session;session.player=player;
                var fixture=new GameObject("Optional work");fixture.transform.SetParent(root.transform);
                var item=fixture.AddComponent<PreparednessItem>();item.kind=PrepKind.HouseholdTask;item.portable=false;
                var work=fixture.AddComponent<PreparednessHouseTask>();work.instruction="Check fitting";work.result="Checked";
                var progress=typeof(PreparednessHouseTask).GetField("progress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                work.Begin(session,null);progress.SetValue(work,work.workSeconds);
                player.PutDown();
                if(work.CompleteWork()||item.prepared)throw new Exception("Cancelled household work still completed.");
                work.Begin(session,null);progress.SetValue(work,work.workSeconds);
                if(!work.CompleteWork()||!item.prepared)throw new Exception("Cancelled work could not be restarted.");
                File.WriteAllText(Folder+"cancel-checks.txt","PASS: Q action cancels household work even without a held tool.\nPASS: Cancelled work cannot commit completion.\nPASS: Cancelled work can be restarted and completed.\nPreview-scene component checks; not a physical keyboard playtest.");
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
