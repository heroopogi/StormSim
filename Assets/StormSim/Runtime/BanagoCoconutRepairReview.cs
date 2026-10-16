using System;
using UnityEngine;

/// <summary>Original tree references retained for a reversible, same-count appearance repair.</summary>
public sealed class BanagoCoconutRepairReview : MonoBehaviour
{
    [Serializable] public sealed class MeshSwap
    {public MeshFilter target;public Mesh previous,replacement;public Renderer renderer;public Material[] previousMaterials;public Material material;}
    [Serializable] public sealed class TreeSwap
    {public Transform target;public string previousName;public Vector3 previousScale;public Quaternion previousRotation;public CapsuleCollider collider;public float radius,height;public Vector3 center;public LODGroup lod;public Vector3 lodCenter;public float lodSize;}
    public string backupScene,assetFolder;
    [TextArea(3,12)]public string notes;
    public MeshSwap[] meshes=Array.Empty<MeshSwap>();
    public TreeSwap[] trees=Array.Empty<TreeSwap>();
    // No automatic OnEnable hook: reopening the project must not overwrite later manual edits.
}
