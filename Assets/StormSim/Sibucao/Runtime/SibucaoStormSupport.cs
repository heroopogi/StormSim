using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Explicitly reviewed fixed architecture, never inferred from an arbitrary collider.</summary>
    [DisallowMultipleComponent]
    public sealed class SibucaoStormSupport : MonoBehaviour
    {
        public bool canBrace = true;
        public bool blocksWind = true;
        public bool sheltersRain;
    }
}
