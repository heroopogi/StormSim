using UnityEngine;
namespace StormSim.Sibucao
{
    [DisallowMultipleComponent]
    public sealed class DecisionTriggerMarker : MonoBehaviour
    {
        [Range(1,9)] public int decision=1;
        public string purpose;
    }
}
