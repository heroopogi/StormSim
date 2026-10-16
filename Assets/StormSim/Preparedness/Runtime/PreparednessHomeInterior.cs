using UnityEngine;
namespace StormSim.Preparedness
{
    // Authoring metadata for representative interiors, not surveyed household demographics.
    public sealed class PreparednessHomeInterior : MonoBehaviour
    {
        public string footprintId;
        public float width,depth;
        public Transform entrance;
        public GameObject family;
        public int residents=4;
    }
}
