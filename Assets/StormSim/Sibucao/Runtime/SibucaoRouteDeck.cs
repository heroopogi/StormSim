using UnityEngine;

namespace StormSim.Sibucao
{
    // Compatibility component: old scenes may contain generated access, but the
    // simulator now uses only the existing map terrain at every difficulty.
    public sealed class SibucaoRouteDeck : MonoBehaviour
    {
        public Material timber, metal;
        [SerializeField] Transform generated;
        public void Rebuild(RouteSectionLayout layout)
        {
            if(!generated)return;
            generated.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);
            generated=null;
        }
        public void SetFloodLift(RouteSectionLayout layout, float lift, RouteSectionLayout groundLayout)
        {
            Rebuild(groundLayout);
        }
    }
}
