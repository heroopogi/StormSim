using UnityEngine;

/// <summary>Switches between retained original mesh assets and the reviewed architecture.</summary>
[ExecuteAlways]
public sealed class BanagoArchitectureReview : MonoBehaviour
{
    public MeshFilter[] targets = new MeshFilter[0];
    public Mesh[] originals = new Mesh[0];
    public Mesh[] replacements = new Mesh[0];
    public Renderer[] oldSigns = new Renderer[0];
    public bool[] signEnabled = new bool[0];
    [TextArea(3,8)] public string notes;

    public void Apply()
    {
        for(int i=0;i<targets.Length&&i<originals.Length&&i<replacements.Length;i++)
        {
            var f=targets[i];if(!f||!replacements[i])continue;
            if(f.sharedMesh!=originals[i]&&f.sharedMesh!=replacements[i])continue;
            var collider=f.GetComponent<MeshCollider>();
            if(collider&&(collider.sharedMesh==originals[i]||collider.sharedMesh==replacements[i]))collider.sharedMesh=replacements[i];
            f.sharedMesh=replacements[i];
        }
        foreach(var r in oldSigns)if(r)r.enabled=false;
    }
    void OnEnable(){Apply();}
    void OnDisable()
    {
        for(int i=0;i<targets.Length&&i<originals.Length&&i<replacements.Length;i++)
        {
            var f=targets[i];if(!f)continue;
            if(f.sharedMesh==replacements[i])f.sharedMesh=originals[i];
            var collider=f.GetComponent<MeshCollider>();if(collider&&collider.sharedMesh==replacements[i])collider.sharedMesh=originals[i];
        }
        for(int i=0;i<oldSigns.Length;i++)if(oldSigns[i])oldSigns[i].enabled=i<signEnabled.Length&&signEnabled[i];
    }
}
