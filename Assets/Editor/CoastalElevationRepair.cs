using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Survey and apply explicit coastal repair plans without overwriting source meshes.</summary>
public static class CoastalElevationRepair
{
    public const string Data = "RoadRepairBackup/CoastalElevationRepair";
    [Serializable] public class Surface { public string name, path; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    [Serializable] public class SurveyData { public Surface[] surfaces; }
    public static void Survey()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/MAIN_GAME.unity")
            throw new InvalidOperationException("Open MAIN_GAME in Edit mode before surveying.");
        Directory.CreateDirectory(Data);
        var fs = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Where(f => f.sharedMesh && f.sharedMesh.isReadable && f.GetComponent<Renderer>() &&
            (f.name == "Calm water - traced sea boundary" || f.name == "Baybayin sea and river-mouth water" ||
            (f.transform.parent && f.transform.parent.name == "Walkable coastal terrain") || f.name.Contains("foreshore") ||
            f.name.Contains("sandy") || f.name.Contains("shoreline") || f.name.Contains("Seawall"))).ToArray();
        File.WriteAllText(Data + "/survey.json", JsonUtility.ToJson(new SurveyData { surfaces = fs.Select(f => new Surface {
            name=f.name, path=AssetDatabase.GetAssetPath(f.sharedMesh), vertices=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray(),
            uv=f.sharedMesh.uv, triangles=f.sharedMesh.triangles }).ToArray() }));
        Capture("before-seam", new Vector3(-205,-7,89),55,42);
        Capture("before-coast", new Vector3(130,-5,-900),75,48);
        Debug.Log("Coastal survey: " + fs.Length + " surfaces. " + Data);
    }
    public static void Capture(string name, Vector3 target, float size, float pitch)
    {
        var method = typeof(MapExtensionConnectionRepair).GetMethod("Capture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(null, new object[] { Data+"/"+name+".png", target, size, pitch });
    }

    [MenuItem("Tools/StormSim/Survey coastal elevation joins")]
    public static void SurveyFromMenu() => Survey();
}
