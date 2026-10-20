using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace StormSim.UI
{
    /// <summary>Persistent loading feedback shared by location, restart and menu transitions.</summary>
    public sealed class StormglassSceneTransition : MonoBehaviour
    {
        static StormglassSceneTransition active;
        readonly List<BaseInputModule> suspendedInput = new List<BaseInputModule>();
        float previousTimeScale;
        bool loaded;
        public static bool IsLoading => active;
        public static void Load(string scene)
        {
            if (active) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogError("Cannot load unavailable scene: " + scene); return; }
            var root = new GameObject("Stormglass loading", typeof(RectTransform));
            DontDestroyOnLoad(root);
            active = root.AddComponent<StormglassSceneTransition>();
            active.StartCoroutine(active.ShowAndLoad(scene));
        }
        IEnumerator ShowAndLoad(string scene)
        {
            var theme = Resources.Load<StormglassTheme>("Stormglass/Theme");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.sortingOrder = 32000;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (XRSettings.isDeviceActive && Camera.main)
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
                ((RectTransform)transform).sizeDelta = new Vector2(1280, 720);
                transform.SetPositionAndRotation(Camera.main.transform.position + Camera.main.transform.forward * 1.8f, Camera.main.transform.rotation);
                transform.localScale = Vector3.one * .00135f;
                scaler.enabled = false;
            }
            var blocker = new GameObject("Loading background", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            blocker.SetParent(transform, false); blocker.anchorMin = Vector2.zero; blocker.anchorMax = Vector2.one; blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = StormglassTheme.Background;
            var panel = new GameObject("Loading panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(transform, false); panel.sizeDelta = new Vector2(620, 240);
            panel.GetComponent<Image>().color = StormglassTheme.Surface;
            if (theme) { panel.GetComponent<Image>().sprite = theme.PanelSprite; panel.GetComponent<Image>().type = Image.Type.Sliced; }
            var text = new GameObject("Loading status", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(panel, false); text.rectTransform.sizeDelta = new Vector2(560, 190);
            if (theme) text.font = theme.bodyFont;
            text.fontSize = 32 * (StormSim.Sibucao.SibucaoSession.Instance ? StormSim.Sibucao.SibucaoSession.Instance.settings.textScale : 1.3f);
            text.color = StormglassTheme.Ink; text.alignment = TextAlignmentOptions.Center;
            text.text = "LOADING\nPreparing your next screen…";
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                foreach (var input in EventSystem.current.GetComponents<BaseInputModule>())
                    if (input.enabled) { suspendedInput.Add(input); input.enabled = false; }
            }
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            // Give the renderer a frame to present feedback before scene work begins.
            yield return null;
            AsyncOperation operation;
            try { operation = SceneManager.LoadSceneAsync(scene); }
            catch (System.Exception exception) { Debug.LogException(exception); Destroy(gameObject); yield break; }
            if (operation == null) { Destroy(gameObject); yield break; }
            while (!operation.isDone)
            {
                text.text = operation.progress < .9f ? "LOADING\nPreparing your next screen…" : "LOADING\nFinishing setup…";
                yield return null;
            }
            loaded = true; Time.timeScale = 1;
            Destroy(gameObject);
        }
        void OnDestroy()
        {
            foreach (var input in suspendedInput) if (input) input.enabled = true;
            if (!loaded) Time.timeScale = previousTimeScale;
            if (active == this) active = null;
        }
    }
}
