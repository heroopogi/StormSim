using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    // Meta's preprocess hook injects Editor connection credentials into this Resources asset.
    // The desktop game does not use that developer assistant. Scrub the build copy after
    // the package hook, then let BuildDesktop's finally restore the original Editor asset.
    public sealed class SibucaoDesktopBuildSanitizer : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        const string SettingsPath = "Assets/Resources/DevAgentSettings.asset";
        static bool active;
        static UnityEngine.Object original;
        static string originalJson;
        public int callbackOrder => int.MaxValue;

        public static void Begin()
        {
            if (active) throw new InvalidOperationException("Desktop build sanitation is already active.");
            original = AssetDatabase.LoadMainAssetAtPath(SettingsPath);
            originalJson = original ? EditorJsonUtility.ToJson(original) : null;
            active = true;
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!active || report.summary.platform != BuildTarget.StandaloneWindows64) return;
            var settings = AssetDatabase.LoadMainAssetAtPath(SettingsPath);
            if (!settings) return;
            var data = new SerializedObject(settings);
            var enabled = data.FindProperty("enabled");
            if (enabled == null) throw new BuildFailedException("Developer settings schema changed; inspect desktop sanitation before shipping.");
            enabled.boolValue = false;
            foreach (var name in new[] { "serverAddress", "accessToken", "witClientAccessToken" })
            {
                var value = data.FindProperty(name);
                if (value == null) throw new BuildFailedException("Developer settings schema changed: " + name);
                value.stringValue = "";
            }
            var voice = data.FindProperty("witConfiguration");
            if (voice != null) voice.objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("StormSim desktop build: developer assistant disabled and connection settings removed. Values are never logged.");
        }
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (!active || report == null || report.summary.platform != BuildTarget.StandaloneWindows64) return;
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var objects = root.GetComponentsInChildren<Transform>(true);
                for (int i = objects.Length - 1; i >= 0; i--)
                    if (objects[i] && objects[i].name.StartsWith("EDITOR ONLY", StringComparison.Ordinal))
                    { UnityEngine.Object.DestroyImmediate(objects[i].gameObject); removed++; }
            }
            Debug.Log("StormSim desktop build: removed " + removed + " dedicated Editor validation objects from the build scene.");
        }
        public static void Restore()
        {
            try
            {
                if (original && originalJson != null)
                {
                    EditorJsonUtility.FromJsonOverwrite(originalJson, original);
                    EditorUtility.SetDirty(original);
                    AssetDatabase.SaveAssetIfDirty(original);
                }
            }
            finally { active = false; original = null; originalJson = null; }
        }
    }
}
