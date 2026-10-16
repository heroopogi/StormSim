using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Retains the original scene objects so the entire review can be reverted by hiding its root.</summary>
[ExecuteAlways]
public sealed class BanagoLocalDetailReview : MonoBehaviour
{
    public GameObject[] originals = new GameObject[0];
    public bool[] originalActive = new bool[0];
    [TextArea(4,12)] public string notes;
    public void Apply() { foreach(var go in originals) if(go) go.SetActive(false); }
    void OnEnable() { Apply(); }
    void OnDisable() { for(int i=0;i<originals.Length;i++) if(originals[i]) originals[i].SetActive(i<originalActive.Length && originalActive[i]); }
}
