using CesiumForUnity;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormSim.Sibucao
{
    /// <summary>Keeps map attribution on About & credits, outside the gameplay HUD.</summary>
    public sealed class StormSimMapCredits : MonoBehaviour
    {
        UIDocument creditDocument;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (FindAnyObjectByType<StormSimMapCredits>()) return;
            var host = new GameObject("StormSim map credits visibility");
            DontDestroyOnLoad(host);
            host.AddComponent<StormSimMapCredits>();
        }

        void LateUpdate()
        {
            // Cesium creates its credit system lazily and replaces it across scenes.
            // Keep its document enabled so live credits, images and links still update.
            if (!creditDocument)
            {
                var credits = FindAnyObjectByType<CesiumCreditSystem>();
                if (!credits) return;
                creditDocument = credits.GetComponent<UIDocument>();
            }

            if (!creditDocument || creditDocument.rootVisualElement == null) return;
            var session = SibucaoSession.Instance;
            bool show = session && session.Mode == SibucaoMode.About;
            creditDocument.rootVisualElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            // Close an expanded attribution popup before returning to the simulation.
            if (!show)
            {
                var popup = creditDocument.rootVisualElement.Q("PopupCredits");
                if (popup != null) popup.style.display = DisplayStyle.None;
            }
        }

        void OnDisable()
        {
            if (creditDocument && creditDocument.rootVisualElement != null)
                creditDocument.rootVisualElement.style.display = StyleKeyword.Null;
        }
    }
}
