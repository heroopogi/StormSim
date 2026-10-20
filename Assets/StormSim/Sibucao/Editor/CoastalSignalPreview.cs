using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using StormSim.Sibucao;

namespace StormSim.Editor
{
    // Run in a separate batch editor. Captures the real runtime menu without
    // saving a scene or changing the player's stored settings.
    public static class CoastalSignalPreview
    {
        public static void Capture()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Coastal preview");
                var menu = root.AddComponent<StormSimMainMenuScene>();
                typeof(StormSimMainMenuScene).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                var camera = Camera.main;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var target = new RenderTexture(1920,1080,24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
                Directory.CreateDirectory("Captures/CoastalSignal");
                File.WriteAllBytes("Captures/CoastalSignal/main-menu.png",image.EncodeToPNG());
                int overflow=0;
                foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())
                    if(text.isTextOverflowing){Debug.LogWarning("Coastal preview text overflow: "+text.text);overflow++;}
                File.WriteAllText("Captures/CoastalSignal/validation.txt", "Main menu rendered at 1920 x 1080. Text overflow count: "+overflow+"\n"+StormglassChecks.CheckPointer());
                camera.targetTexture=null;RenderTexture.active=null;
                UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
                EditorApplication.Exit(overflow==0?0:2);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
