using UnityEngine;

/// <summary>Reapply the latest guarded swaps after older review components finish reloading.</summary>
[ExecuteAlways, DefaultExecutionOrder(20000)]
public sealed class BanagoAerialMaterialRestore : MonoBehaviour
{
    public BanagoReferenceAreasReview review;
    bool pending;
    void OnEnable() { pending = true; }
    void OnDisable() { pending = false; }
    void Update()
    {
        if (!pending || !review || !review.isActiveAndEnabled) return;
        pending = false;
        // Apply only recognizes this review's before/after materials, preserving later edits.
        review.Apply();
    }
}
