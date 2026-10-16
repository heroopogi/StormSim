using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Matches every Baybayin mapped house to the approved Banago house finish.</summary>
public static class BaybayinHouseStyleBuilder
{
    const string RootName = "Purok Baybayin - satellite matched expansion";
    const string MaterialFolder = "Assets/StormSim/Generated/BanagoLivedIn_20260912_030153";

    [MenuItem("Tools/StormSim/Match All Baybayin Houses To First House Style")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MAIN_GAME.unity")
            throw new InvalidOperationException("Open MAIN_GAME in Edit mode.");

        var root = GameObject.Find(RootName);
        if (!root) throw new InvalidOperationException("The Baybayin expansion is missing.");
        if (root.GetComponent<BaybayinHouseStyleReview>())
            throw new InvalidOperationException("All Baybayin houses already use the first house style.");

        var walls = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Chalk paint exposed CHB and damp streaks.mat");
        var roofs = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Patchwork galvanized iron rust and occasional tile.mat");
        var details = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Weathered door frames and house trims.mat");
        if (!walls || !roofs || !details) throw new InvalidOperationException("Approved Banago house materials are missing.");

        var houses = root.GetComponentsInChildren<BaybayinMappedBuilding>(true);
        if (houses.Length == 0) throw new InvalidOperationException("No mapped Baybayin houses were found.");

        string backup = "RoadRepairBackup/BaybayinExpansion/MAIN_GAME.before-house-style-match.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("Could not create the scene backup.");

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Match all Baybayin houses to first house style");

        var review = Undo.AddComponent<BaybayinHouseStyleReview>(root);
        review.swaps = houses.Select(h => h.GetComponent<Renderer>()).Where(r => r).Select(r =>
        {
            Undo.RecordObject(r, "Restyle Baybayin house");
            int slots = Math.Max(3, r.sharedMaterials.Length);
            var replacement = Enumerable.Range(0, slots).Select(i => i == 0 ? walls : i == 1 ? roofs : details).ToArray();
            return new BaybayinHouseStyleReview.MaterialSwap { target = r, original = r.sharedMaterials, replacement = replacement };
        }).ToArray();
        review.notes = "All mapped Baybayin houses now share the approved first-neighborhood finish: weathered CHB/plaster walls, patchwork corrugated metal roofs, and dark door/window trim. Footprints, heights, roads, terrain, trees, civic buildings, and coastline are unchanged. Disable this component to restore the previous pale materials. Backup: " + backup;
        review.Apply();
        EditorUtility.SetDirty(review);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Undo.CollapseUndoOperations(undo);
        Debug.Log("Matched " + review.swaps.Length + " Baybayin houses to the first house style.", root);
    }
}
