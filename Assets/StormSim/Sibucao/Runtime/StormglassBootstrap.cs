using UnityEngine;
using UnityEngine.SceneManagement;
using StormSim.Sibucao;

namespace StormSim.UI
{
    public static class StormglassBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded -= Install; SceneManager.sceneLoaded += Install; }
        static void Install(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects()) foreach (var session in root.GetComponentsInChildren<SibucaoSession>(true))
            {
                if (!session.GetComponent<StormglassUI>()) session.gameObject.AddComponent<StormglassUI>();
                if (session.player && !session.player.GetComponent<StormglassXR>()) session.player.gameObject.AddComponent<StormglassXR>();
            }
        }
    }
}
