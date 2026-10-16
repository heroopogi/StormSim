using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StormSim.Sibucao;

public static class SibucaoPlaytestFeedbackInstaller
{
    [Serializable] sealed class Manifest { public SibucaoNarrationCue[] cues; }
    [MenuItem("StormSim/Route choices/Install playtest narration")]
    public static string Install()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before installing narration.");
        const string folder="Assets/StormSim/Sibucao/Art/Narration/";
        var session=UnityEngine.Object.FindAnyObjectByType<SibucaoSession>();
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"manifest.json"));
        foreach(var cue in manifest.cues)
        {
            cue.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(folder+cue.key+".wav");
            if(!cue.clip)throw new InvalidOperationException("Missing narration recording: "+cue.key);
        }
        var narrator=session.GetComponent<SibucaoNarration>();
        if(!narrator)narrator=Undo.AddComponent<SibucaoNarration>(session.gameObject);
        Undo.RecordObject(narrator,"Install narrated guidance");narrator.cues=manifest.cues;
        EditorUtility.SetDirty(narrator);EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        return "Installed "+narrator.cues.Length+" recorded narration cues with captions and N replay.";
    }
}
