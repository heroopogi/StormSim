using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Hide this group to restore the four original satellite-ground chunks.</summary>
[ExecuteAlways]
public sealed class BanagoCoastalTest : MonoBehaviour
{
    public GameObject[] originalGround = new GameObject[0];
    public bool[] originallyActive = new bool[0];
    public Material waterMaterial;
    [TextArea(3, 8)] public string placementNotes;
    public static float reviewTime = -1;
#if UNITY_EDITOR
    double lastRepaint;
#endif
    void OnEnable()
    {
        ApplyVisibility();
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
#endif
    }
    public void ApplyVisibility()
    {
        for (int i = 0; i < originalGround.Length; i++)
            if (originalGround[i]) originalGround[i].SetActive(false);
    }
    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        for (int i = 0; i < originalGround.Length; i++)
            if (originalGround[i]) originalGround[i].SetActive(i < originallyActive.Length && originallyActive[i]);
    }
    void Update()
    {
        if (Application.isPlaying) Shader.SetGlobalFloat("_BanagoCoastalTime", Time.time);
    }
#if UNITY_EDITOR
    void EditorTick()
    {
        if (!this || !isActiveAndEnabled || Application.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        Shader.SetGlobalFloat("_BanagoCoastalTime", reviewTime >= 0 ? reviewTime : (float)(now % 100000));
        if (now - lastRepaint > .066)
        {
            lastRepaint = now;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Repaint();
        }
    }
#endif
}
