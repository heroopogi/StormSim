using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoPropSource:MonoBehaviour
    {
        public string sourceProject,sourceAsset;
        public int triangles,textureResolution;
        [TextArea] public string validation;
    }
}
