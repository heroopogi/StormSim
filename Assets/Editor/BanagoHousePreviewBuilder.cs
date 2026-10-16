using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BanagoHousePreviewBuilder
{
    const string Source = "RoadRepairBackup/banago-house-preview-data.json";
    const string RootName = "Banago Houses";
    public static string Status = "Not started";
    static bool running;

    [MenuItem("Tools/StormSim/Build 1180 House Review Batch")]
    public static void Build()
    {
        Begin(Source, false);
    }

    [MenuItem("Tools/StormSim/Add Remaining Banago Houses")]
    public static void BuildRemaining()
    {
        Begin("RoadRepairBackup/banago-house-remaining-data.json", true);
    }

    static void Begin(string sourcePath, bool remaining)
    {
        if (running) throw new InvalidOperationException("House sampling is already running.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var existingRoot = GameObject.Find(RootName);
        if (!remaining && existingRoot) throw new InvalidOperationException("Banago Houses already exists; use Add Remaining Banago Houses to extend it.");
        if (remaining && !existingRoot) throw new InvalidOperationException("The first house batch must exist before extending it.");
        var geo = GameObject.Find("CesiumGeoreference").GetComponent<CesiumGeoreference>();
        var terrain = GameObject.Find("Cesium World Terrain").GetComponent<Cesium3DTileset>();
        var existingIds = existingRoot ? new HashSet<string>(existingRoot.GetComponentsInChildren<BanagoHousePreview>(true).Select(h => h.footprintId)) : new HashSet<string>();
        var candidates = ((JArray)JObject.Parse(File.ReadAllText(sourcePath))["houses"]).Where(h => !existingIds.Contains((string)h["id"]));
        var houses = (remaining ? candidates : candidates.Take(1180)).ToArray();
        if (houses.Length == 0) { Status = "All eligible house footprints are already present."; return; }
        var coordinates = new List<double3>();
        var ranges = new List<int>();
        foreach (var house in houses)
        {
            ranges.Add(coordinates.Count);
            coordinates.Add(new double3((double)house["lon"], (double)house["lat"], 0));
            foreach (var point in (JArray)house["footprint"])
            {
                var local = new double3((double)house["x"] + (double)point[0], 0, (double)house["z"] + (double)point[1]);
                coordinates.Add(CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(geo.TransformUnityPositionToEarthCenteredEarthFixed(local)));
            }
        }
        ranges.Add(coordinates.Count);
        var task = terrain.SampleHeightMostDetailed(coordinates.ToArray());
        running = true;
        Status = "Sampling ground for " + houses.Length + " houses";
        double started = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction poll = null;
        poll = () =>
        {
            if (!task.IsCompleted && EditorApplication.timeSinceStartup - started < 150) return;
            EditorApplication.update -= poll; running = false;
            try
            {
                if (!task.IsCompleted) throw new TimeoutException("Terrain sampling timed out.");
                if (task.IsFaulted) throw task.Exception;
                var result = task.Result;
                if (result.sampleSuccess.Any(ok => !ok)) throw new InvalidOperationException("Some ground samples failed; no houses placed.");
                var baseY = new float[houses.Length];
                for (int i = 0; i < houses.Length; i++)
                {
                    baseY[i] = float.MinValue;
                    for (int k = ranges[i]; k < ranges[i + 1]; k++)
                    {
                        var llh = result.longitudeLatitudeHeightPositions[k];
                        var p = geo.TransformEarthCenteredEarthFixedPositionToUnity(CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(llh));
                        baseY[i] = Mathf.Max(baseY[i], (float)p.y);
                    }
                }
                Create(geo, houses, baseY, sourcePath, remaining);
            }
            catch (Exception ex) { Status = "Failed: " + ex.Message; Debug.LogError("BANAGO_HOUSES: " + Status); }
        };
        EditorApplication.update += poll;
    }

    static void Create(CesiumGeoreference geo, JToken[] houses, float[] baseY, string sourcePath, bool remaining)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/StormSim/Generated/BanagoHouseVertexColor.shader");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("House shader is not ready.");
        string folder = "Assets/StormSim/Generated/BanagoHousePreview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string meshAsset = folder + "/HouseMeshes.asset";
        var existingRoot = remaining ? GameObject.Find(RootName) : null;
        int existingCount = existingRoot ? existingRoot.GetComponentsInChildren<BanagoHousePreview>(true).Length : 0;
        var material = existingRoot ? existingRoot.GetComponentInChildren<MeshRenderer>(true).sharedMaterial : new Material(shader) { name = "Banago sampled roof and wall colors", enableInstancing = true };
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build Banago house review batch");
        var root = new GameObject(remaining ? "Remaining Houses" : RootName);
        root.transform.SetParent(geo.transform, false);
        if (existingRoot) root.transform.SetParent(existingRoot.transform, true);
        Undo.RegisterCreatedObjectUndo(root, "Build Banago house review batch");
        var blocks = new Dictionary<string, Transform>();
        AssetDatabase.StartAssetEditing();
        try
        {
            if (!existingRoot) AssetDatabase.CreateAsset(material, folder + "/HouseColors.mat");
            for (int i = 0; i < houses.Length; i++)
            {
                var house = houses[i];
                float x = (float)house["x"], z = (float)house["z"];
                string block = "Block " + Mathf.FloorToInt(x / 100) + "_" + Mathf.FloorToInt(z / 100);
                if (!blocks.ContainsKey(block))
                {
                    var blockObject = new GameObject(block); blockObject.transform.SetParent(root.transform, false); blocks[block] = blockObject.transform;
                }
                var go = new GameObject("House " + (existingCount + i + 1).ToString("D4"));
                go.transform.SetParent(blocks[block], false);
                go.transform.localPosition = new Vector3(x, baseY[i] + .04f, z);
                var points = (JArray)house["vertices"];
                var vertexColors = (JArray)house["colors"];
                var vertices = new Vector3[points.Count]; var colors = new Color32[points.Count];
                for (int j = 0; j < points.Count; j++)
                {
                    vertices[j] = new Vector3((float)points[j][0], (float)points[j][1], (float)points[j][2]);
                    colors[j] = new Color((float)vertexColors[j][0], (float)vertexColors[j][1], (float)vertexColors[j][2], 1);
                }
                var mesh = new Mesh { name = go.name + " footprint roof", indexFormat = IndexFormat.UInt32 };
                mesh.vertices = vertices; mesh.colors32 = colors; mesh.triangles = house["triangles"].Values<int>().ToArray();
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if (i == 0) AssetDatabase.CreateAsset(mesh, meshAsset); else AssetDatabase.AddObjectToAsset(mesh, meshAsset);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                var info = go.AddComponent<BanagoHousePreview>();
                info.footprintId = (string)house["id"]; info.longitude = (double)house["lon"]; info.latitude = (double)house["lat"];
                info.wallHeightMeters = (float)house["height"]; info.roofRiseMeters = (float)house["roofRise"]; info.footprintAreaSquareMeters = (float)house["area"];
                info.sampledRoofColor = new Color((float)house["roofColor"][0], (float)house["roofColor"][1], (float)house["roofColor"][2]);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        if (!existingRoot) root.AddComponent<CesiumGlobeAnchor>();
        File.Copy(sourcePath, folder + "/HousePreviewSource.json");
        File.WriteAllText(folder + "/README.txt", (remaining ? "Remaining-house batch: " : "Review batch: ") + houses.Length + " houses. Total generated houses: " + (existingCount + houses.Length) + ".\nFootprints: Overture Maps (Google Open Buildings and Microsoft ML Buildings); existing Cesium OSM buildings preserved.\nhttps://docs.overturemaps.org/guides/buildings/\nColors sampled from the Unity satellite map. Heights, pitched roof geometry, doors and windows are estimates, not surveyed facade details.\nHide or remove CesiumGeoreference/Banago Houses" + (remaining ? "/Remaining Houses" : "") + " to remove this batch. Scene backups are in RoadRepairBackup.\n");
        AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(meshAsset);
        var manager = geo.GetComponent<CesiumCameraManager>();
        var survey = manager.additionalCameras.FirstOrDefault(c => c && c.name == "Banago Roof Survey (temporary)");
        if (survey)
        {
            manager.additionalCameras.Remove(survey); var rt = survey.targetTexture; survey.targetTexture = null;
            if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            UnityEngine.Object.DestroyImmediate(survey.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
        Status = "Built and saved " + houses.Length + " houses in " + blocks.Count + " blocks; all ground samples successful.";
        Debug.Log("BANAGO_HOUSES: " + Status);
    }
}
