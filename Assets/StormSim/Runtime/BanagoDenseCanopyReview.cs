using System;
using UnityEngine;

/// <summary>Placement provenance for an additive, whole-root-toggle vegetation layer.</summary>
public sealed class BanagoDenseCanopyReview : MonoBehaviour
{
    [Serializable] public sealed class Plant
    {
        public Transform target;
        public string species, nearHouse;
        public float baseClearance, crownRadius, groundY, crownFloor;
    }
    [TextArea(4,12)] public string notes;
    public string sceneBackup, assetFolder;
    public Plant[] plants = Array.Empty<Plant>();
}
