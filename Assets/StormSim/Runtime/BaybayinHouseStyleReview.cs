using UnityEngine;

/// <summary>Keeps the Baybayin house restyle reversible from the Inspector.</summary>
public sealed class BaybayinHouseStyleReview : MonoBehaviour
{
    [System.Serializable]
    public struct MaterialSwap
    {
        public Renderer target;
        public Material[] original;
        public Material[] replacement;
    }

    public MaterialSwap[] swaps = new MaterialSwap[0];
    [TextArea] public string notes;

    public void Apply()
    {
        foreach (var swap in swaps)
            if (swap.target) swap.target.sharedMaterials = swap.replacement;
    }

    void OnEnable() => Apply();

    void OnDisable()
    {
        foreach (var swap in swaps)
            if (swap.target) swap.target.sharedMaterials = swap.original;
    }
}
