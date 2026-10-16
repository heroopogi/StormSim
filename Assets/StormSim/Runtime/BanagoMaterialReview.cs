using UnityEngine;
[ExecuteAlways]
public sealed class BanagoMaterialReview : MonoBehaviour
{
    public Renderer[] targets=new Renderer[0];
    public Material[] originals=new Material[0];
    public Material[] replacements=new Material[0];
    public void Apply(){ApplyMaterials(replacements);}
    void OnEnable(){Apply();}
    void OnDisable(){ApplyMaterials(originals);}

    void ApplyMaterials(Material[] materials)
    {
        for(int i=0;i<targets.Length && i<materials.Length;i++)
        {
            var target=targets[i];
            if(!target)continue;
            // Animated water owns its shader. Restoring this review's old water snapshots
            // during Play-mode/domain transitions would silently remove visible waves.
            if(target.TryGetComponent<BanagoSeaWaves>(out _))continue;
            target.sharedMaterial=materials[i];
        }
    }
}
