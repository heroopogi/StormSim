using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using StormSim.Sibucao;
using StormSim.UI;

namespace StormSim.Editor
{
    // Uses production sizing and fonts in a small preview scene; not an in-game acceptance run.
    public static class StormglassItemLayoutChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("StormSim/Stormglass/Check item information layout")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new List<string> { "Isolated production UI layout check; no environment or headset acceptance implied." };
            string folder = "Captures/UICompletion/ItemLayoutRecheck";
            Directory.CreateDirectory(folder);
            RenderTexture target = null;
            Texture2D pixels = null;
            var previousTarget = RenderTexture.active;
            try
            {
                var owner = new GameObject("Inactive layout dependencies");
                owner.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
                var session = owner.AddComponent<SibucaoSession>();
                session.settings = new SibucaoSettings();
                var ui = owner.AddComponent<StormglassUI>();
                typeof(StormglassUI).GetField("S", Private).SetValue(ui, session);
                var theme = Resources.Load<StormglassTheme>("Stormglass/Theme");
                var cameraObject = new GameObject("Layout camera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = StormglassTheme.Background;
                camera.scene = scene;
                var root = new GameObject("Layout canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 720f / 1000f;
                var card = new GameObject("Item information", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                card.SetParent(root.transform, false);
                card.GetComponent<Image>().sprite = theme.PanelSprite; card.GetComponent<Image>().type = Image.Type.Sliced;
                card.GetComponent<Image>().color = StormglassTheme.Surface;
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(card, false); label.font = theme.bodyFont; label.color = StormglassTheme.Ink;
                label.alignment = TextAlignmentOptions.TopLeft; label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Truncate;
                label.text = "NAME\nPortable radio — fit batteries and test\n\nPURPOSE\nReceives Monitoring, Alert, and Evacuate updates when television or mobile service is unavailable. Fit batteries and test it.";
                typeof(StormglassUI).GetField("itemInfo", Private).SetValue(ui, card);
                typeof(StormglassUI).GetField("itemInfoText", Private).SetValue(ui, label);
                target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
                foreach (float scale in new[] { 1f, 1.3f })
                {
                    session.settings.textScale = scale;
                    typeof(StormglassUI).GetMethod("FitItemInformation", Private).Invoke(ui, null);
                    Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
                    bool pass = !label.isTextOverflowing && card.rect.height <= 600;
                    report.Add((pass ? "PASS" : "FAIL") + ": longest observed item, text scale=" + scale + ", height=" + card.rect.height + ", overflow=" + label.isTextOverflowing);
                    camera.Render(); RenderTexture.active = target;
                    pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                    File.WriteAllBytes(folder + "/item-" + scale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png", pixels.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pixels); pixels = null;
                }
                File.WriteAllLines(folder + "/inspection.txt", report);
                if (report.Exists(line => line.StartsWith("FAIL"))) throw new Exception(string.Join("\n", report));
                Debug.Log(string.Join("\n", report));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
