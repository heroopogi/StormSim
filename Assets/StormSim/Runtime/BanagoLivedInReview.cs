using System;
using UnityEngine;

/// <summary>Reversible, additive appearance pass. Disable the whole root to revert.</summary>
[ExecuteAlways]
public sealed class BanagoLivedInReview : MonoBehaviour
{
    [Serializable] public sealed class MaterialSwap
    {
        public Renderer target;
        public Material[] original;
        public Material[] replacement;
    }
    public MaterialSwap[] swaps = Array.Empty<MaterialSwap>();
    [TextArea(3, 8)] public string notes;
    public bool useHaze;
    public bool previousFog;
    public FogMode previousFogMode;
    public Color previousFogColor;
    public float previousFogDensity;
    public Color hazeColor = new Color(.67f, .73f, .73f);
    public float hazeDensity = .00032f;

    static bool Same(Material[] a, Material[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; ++i) if (a[i] != b[i]) return false;
        return true;
    }
    public void Apply()
    {
        foreach (var s in swaps)
            if (s.target && (Same(s.target.sharedMaterials, s.original) || Same(s.target.sharedMaterials, s.replacement)))
                s.target.sharedMaterials = s.replacement;
        if (!useHaze) return;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = hazeColor;
        RenderSettings.fogDensity = hazeDensity;
    }
    void OnEnable() { Apply(); }
    void OnDisable()
    {
        foreach (var s in swaps)
            if (s.target && Same(s.target.sharedMaterials, s.replacement)) s.target.sharedMaterials = s.original;
        if (useHaze && RenderSettings.fogMode == FogMode.Exponential &&
            Mathf.Approximately(RenderSettings.fogDensity, hazeDensity) && RenderSettings.fogColor == hazeColor)
        {
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogDensity = previousFogDensity;
        }
    }
}
