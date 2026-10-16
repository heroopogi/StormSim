using System;
using System.IO;
using System.Linq;
using CesiumForUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StormSimCameraSetup
{
    public static string LastReport = "Not installed";

    [MenuItem("Tools/StormSim/Camera/Install Custom Camera")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before installing the camera.");
        var camera = Camera.main;
        if (!camera) throw new InvalidOperationException("The scene needs an active MainCamera.");
        if (camera.GetComponent<StormSimCameraController>())
        {
            Selection.activeGameObject = camera.gameObject;
            LastReport = "Custom camera is already installed.";
            return;
        }
        var scene = camera.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save the scene before installing the camera.");
        string backup = "RoadRepairBackup/CustomCamera_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/MAIN_GAME.disk-before.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/MAIN_GAME.live-before.unity", true))
            throw new IOException("Could not back up the current scene.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace Cesium camera controls with StormSim camera");
        try
        {
            // Keep the Camera, Transform and AudioListener identities: weather/UI references survive.
            Undo.RecordObject(camera.gameObject, "Name the custom camera");
            camera.gameObject.name = "StormSim Camera";
            Undo.RecordObject(camera, "Set local survey camera clipping");
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20000f;
            var controller = Undo.AddComponent<StormSimCameraController>(camera.gameObject);
            // Flush property callbacks while the Cesium inspector still has a valid anchor.
            // Remove that anchor last, after all additions and recorded property changes.
            Undo.FlushUndoRecordObjects();
            foreach (var component in camera.GetComponents<MonoBehaviour>()
                .Where(c => c is CesiumFlyToController || c is CesiumCameraController || c is CesiumOriginShift).ToArray())
                Undo.DestroyObjectImmediate(component);
            var anchor = camera.GetComponent<CesiumGlobeAnchor>();
            if (anchor) Undo.DestroyObjectImmediate(anchor);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the camera setup.");
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = camera.gameObject;
            LastReport = "Installed StormSim Camera; retained camera, transform, audio and render settings. Backup: " + backup;
            File.WriteAllText(backup + "/README.txt", LastReport + "\nThe live-before copy includes pre-existing unsaved scene changes.\n");
            Debug.Log(LastReport);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
}
