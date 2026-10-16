using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Removes the oversized port-causeway overlay from the west side of the
/// Carlos Hilado junction. The mapped service-road mesh underneath is the
/// correct single-lane approach and remains untouched.
/// </summary>
public static class BaybayinOneLaneApproachBuilder
{
    const string ScenePath = "Assets/Scenes/MAIN_GAME.unity";
    const string ExpansionName = "Purok Baybayin - satellite matched expansion";
    const string RootName = "Baybayin left approach - one lane correction";
    const string CausewayPath = "Port and river/Port approach road and causeway";
    const string Folder = "Assets/StormSim/Generated/BaybayinOneLaneApproach_20260915";
    const string Backup = "RoadRepairBackup/BaybayinExpansion/MAIN_GAME.before-one-lane-left-approach.unity";

    [MenuItem("Tools/StormSim/Fix Baybayin Left Approach To One Lane")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Open MAIN_GAME in Edit mode.");

        var expansion = GameObject.Find(ExpansionName);
        if (!expansion) throw new InvalidOperationException("Baybayin expansion is missing.");
        if (GameObject.Find(RootName))
            throw new InvalidOperationException("The one-lane approach correction already exists.");

        var causeway = expansion.transform.Find(CausewayPath);
        if (!causeway) throw new InvalidOperationException("The oversized port approach overlay is missing.");
        var filter = causeway.GetComponent<MeshFilter>();
        var renderer = causeway.GetComponent<MeshRenderer>();
        if (!filter || !renderer || !filter.sharedMesh)
            throw new InvalidOperationException("The oversized port approach overlay has no valid mesh.");

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        if (!File.Exists(Backup) && !EditorSceneManager.SaveScene(scene, Backup, true))
            throw new IOException("Could not create the scene backup.");

        var empty = new Mesh { name = "Removed oversized two-lane port approach overlay" };
        AssetDatabase.CreateAsset(empty, Folder + "/Removed oversized two-lane port approach overlay.asset");

        var root = new GameObject(RootName);
        root.transform.SetParent(expansion.transform, false);
        Undo.RegisterCreatedObjectUndo(root, "Fix Baybayin left approach to one lane");

        var collider = causeway.GetComponent<MeshCollider>();
        var review = root.AddComponent<BanagoReferenceAreasReview>();
        review.backupPath = Backup;
        review.notes =
            "The oversized 11.4 m black port-causeway carriageway and its center dashes were removed from the west side of the Carlos Hilado junction. " +
            "This exposes the existing mapped service road underneath, which is approximately 3.5 m wide and reads as one lane. " +
            "The Carlos Hilado bridge, divided highway, nearby houses, terrain, and waterways are unchanged. Disable this root to restore the oversized overlay.";
        review.swaps = new[]
        {
            new BanagoReferenceAreasReview.Swap
            {
                target = filter,
                before = filter.sharedMesh,
                after = empty,
                materialsBefore = renderer.sharedMaterials,
                materialsAfter = renderer.sharedMaterials,
                colliderBefore = collider ? collider.sharedMesh : null
            }
        };
        review.Apply();

        EditorUtility.SetDirty(review);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        SceneView.RepaintAll();

        File.WriteAllText(Folder + "/README.txt", review.notes + "\nScene backup: " + Backup + ".\n");
        Debug.Log("Corrected the Baybayin west approach to the mapped single-lane service road.", root);
    }
}
