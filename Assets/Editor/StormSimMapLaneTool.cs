using CesiumForUnity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

public sealed class StormSimMapLaneTool : EditorWindow
{
    private const string GeneratedRootName = "Generated Lanes";
    private const string GeneratedPlaneRootName = "Generated Banago Elevation Plane";
    private const string GeneratedBuildingRootName = "Generated OSM Buildings";
    private const string GeneratedStudyPurokVisibleBoxName = "Generated Study Puroks Visible Box";
    private const string MaterialFolder = "Assets/StormSim/Generated/Materials";
    private const string OverpassInterpreterUrl = "https://overpass-api.de/api/interpreter";
    private const double BanagoCenterLatitude = 10.703697095987568d;
    private const double BanagoCenterLongitude = 122.95003347604923d;
    private const double BanagoBuildingSouthLatitude = 10.6950d;
    private const double BanagoBuildingWestLongitude = 122.9400d;
    private const double BanagoBuildingNorthLatitude = 10.7180d;
    private const double BanagoBuildingEastLongitude = 122.9600d;
    private const double StudyPurokVisibleBoxSouthLatitude = 10.7045d;
    private const double StudyPurokVisibleBoxWestLongitude = 122.9475d;
    private const double StudyPurokVisibleBoxNorthLatitude = 10.7168d;
    private const double StudyPurokVisibleBoxEastLongitude = 122.9585d;
    private const double StudyPurokVisibleBoxHeightMeters = 0d;

    private int _sampleResolution = 12;
    private float _raycastPadding = 100f;
    private float _heightOffset = 0.06f;
    private int _laneCount = 2;
    private float _laneWidth = 3.5f;
    private float _lineWidth = 0.16f;
    private float _roadLength = 80f;
    private float _orientationDegrees;
    private bool _createRoadSurface = true;
    private bool _dashedInnerLines = true;
    private float _dashLength = 3f;
    private float _dashGap = 4f;
    private int _surfaceLengthSegments = 40;
    private GameObject _targetObject;
    private Transform _laneAnchor;
    private bool _useAnchorRotation = true;
    private double _osmCenterLatitude = BanagoCenterLatitude;
    private double _osmCenterLongitude = BanagoCenterLongitude;
    private float _osmRadiusMeters = 250f;
    private int _osmMaxRoads = 120;
    private int _osmDefaultLaneCount = 2;
    private bool _osmAutoDetectLaneCount = true;
    private OsmRoadFilter _osmRoadFilter = OsmRoadFilter.MainRoadsOnly;
    private bool _smoothRoadJoins = true;
    private bool _mergeIntersections = true;
    private float _intersectionPadding = 1.2f;
    private double _planeCenterLatitude = BanagoCenterLatitude;
    private double _planeCenterLongitude = BanagoCenterLongitude;
    private float _planeSizeMeters = 3200f;
    private int _planeResolution = 64;
    private float _planeHeightOffset = 0.02f;
    private bool _planeUseMeshVertexFallback = true;
    private bool _planeCreateCollider;
    private CesiumGeoreference _buildingGeoreference;
    private Cesium3DTileset _buildingTerrainTileset;
    private double _buildingSouthLatitude = BanagoBuildingSouthLatitude;
    private double _buildingWestLongitude = BanagoBuildingWestLongitude;
    private double _buildingNorthLatitude = BanagoBuildingNorthLatitude;
    private double _buildingEastLongitude = BanagoBuildingEastLongitude;
    private int _buildingRequestTimeoutSeconds = 25;
    private int _buildingFewResultWarningThreshold = 10;
    private float _buildingMinimumFootprintDimensionMeters = 2f;
    private float _buildingMaximumFootprintDimensionMeters = 160f;
    private float _buildingDefaultStoryHeightMeters = 3f;
    private int _buildingDefaultMinimumStories = 1;
    private int _buildingDefaultMaximumStories = 3;
    private double _buildingFallbackBaseElevationMeters;
    private bool _buildingClearExistingBeforeGenerate = true;
    private bool _buildingAddBoxColliders = true;
    private bool _buildingGenerationInProgress;
    private double _buildingSamplingStartedAt;
    private CesiumGeoreference _pendingBuildingGeoreference;
    private Task<CesiumSampleHeightResult> _pendingBuildingHeightTask;
    private List<OsmBuildingFootprint> _pendingBuildingFootprints;
    private int _pendingBuildingSkippedIncomplete;
    private double _studyPurokSouthLatitude = StudyPurokVisibleBoxSouthLatitude;
    private double _studyPurokWestLongitude = StudyPurokVisibleBoxWestLongitude;
    private double _studyPurokNorthLatitude = StudyPurokVisibleBoxNorthLatitude;
    private double _studyPurokEastLongitude = StudyPurokVisibleBoxEastLongitude;
    private double _studyPurokHeightMeters = StudyPurokVisibleBoxHeightMeters;
    private readonly List<StormSimBuildingMetadata> _generatedBuildingMetadata = new List<StormSimBuildingMetadata>();
    private readonly Dictionary<long, StormSimBuildingMetadata> _generatedBuildingsByOsmId = new Dictionary<long, StormSimBuildingMetadata>();
    private Vector2 _scrollPosition;
    private string _report = "Select the map or terrain object, then click Analyze Selection Elevation.";

    [MenuItem("Tools/StormSim/Map Elevation and Lanes")]
    public static void Open()
    {
        GetWindow<StormSimMapLaneTool>("Environment");
    }

    [MenuItem("Tools/StormSim/Apply Study Purok Visible Box")]
    public static void ApplyStudyPurokVisibleBoxFromMenu()
    {
        GetWindow<StormSimMapLaneTool>("Environment").ApplyStudyPurokVisibleBox();
    }

    [MenuItem("Tools/StormSim/Clear Study Purok Visible Box")]
    public static void ClearStudyPurokVisibleBoxFromMenu()
    {
        GetWindow<StormSimMapLaneTool>("Environment").ClearStudyPurokVisibleBox();
    }

    private void OnDisable()
    {
        EditorApplication.update -= PollBuildingHeightSampling;
        EditorUtility.ClearProgressBar();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
        _targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", GetTargetObject(), typeof(GameObject), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Current Selection"))
            {
                _targetObject = Selection.activeGameObject;
                Repaint();
            }

            if (GUILayout.Button("Find Cesium World Terrain"))
            {
                _targetObject = GameObject.Find("Cesium World Terrain");
                _report = _targetObject == null
                    ? "Could not find an object named Cesium World Terrain."
                    : "Target set to Cesium World Terrain.";
                Repaint();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Elevation Check", EditorStyles.boldLabel);
        _sampleResolution = EditorGUILayout.IntSlider("Sample Resolution", _sampleResolution, 3, 40);
        _raycastPadding = EditorGUILayout.FloatField("Raycast Padding", _raycastPadding);

        if (GUILayout.Button("Analyze Target Elevation"))
        {
            AnalyzeSelection();
        }

        EditorGUILayout.HelpBox(_report, MessageType.Info);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Lane Preview", EditorStyles.boldLabel);
        _laneAnchor = (Transform)EditorGUILayout.ObjectField("Lane Anchor", _laneAnchor, typeof(Transform), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Lane Anchor"))
            {
                CreateLaneAnchor();
            }

            if (GUILayout.Button("Use Selected As Anchor"))
            {
                _laneAnchor = Selection.activeTransform;
                Repaint();
            }
        }

        _useAnchorRotation = EditorGUILayout.Toggle("Use Anchor Rotation", _useAnchorRotation);
        _laneCount = EditorGUILayout.IntSlider("Lane Count", _laneCount, 1, 8);
        _laneWidth = EditorGUILayout.FloatField("Lane Width", _laneWidth);
        _lineWidth = EditorGUILayout.FloatField("Line Width", _lineWidth);
        _roadLength = EditorGUILayout.FloatField("Road Length", _roadLength);

        using (new EditorGUI.DisabledScope(_useAnchorRotation && _laneAnchor != null))
        {
            _orientationDegrees = EditorGUILayout.Slider("Orientation Degrees", _orientationDegrees, -180f, 180f);
        }

        _heightOffset = EditorGUILayout.FloatField("Height Offset", _heightOffset);
        _createRoadSurface = EditorGUILayout.Toggle("Create Road Surface", _createRoadSurface);
        _dashedInnerLines = EditorGUILayout.Toggle("Dashed Inner Lines", _dashedInnerLines);

        using (new EditorGUI.DisabledScope(!_dashedInnerLines))
        {
            _dashLength = EditorGUILayout.FloatField("Dash Length", _dashLength);
            _dashGap = EditorGUILayout.FloatField("Dash Gap", _dashGap);
        }

        _surfaceLengthSegments = EditorGUILayout.IntSlider("Surface Segments", _surfaceLengthSegments, 2, 120);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Lane Preview"))
            {
                CreateLanePreview();
            }

            if (GUILayout.Button("Clear Generated Lanes"))
            {
                ClearGeneratedLanes();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Automatic Roads From OSM", EditorStyles.boldLabel);
        _osmCenterLatitude = EditorGUILayout.DoubleField("Center Latitude", _osmCenterLatitude);
        _osmCenterLongitude = EditorGUILayout.DoubleField("Center Longitude", _osmCenterLongitude);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Cesium Origin"))
            {
                UseCesiumOriginForOsmCenter();
            }

            if (GUILayout.Button("Use Anchor Position Offset"))
            {
                UseAnchorOffsetForOsmCenter();
            }
        }

        if (GUILayout.Button("Use Banago Main Roads Preset"))
        {
            UseBanagoMainRoadsPreset();
        }

        _osmRadiusMeters = EditorGUILayout.Slider("Radius Meters", _osmRadiusMeters, 25f, 3500f);
        _osmMaxRoads = EditorGUILayout.IntSlider("Max Roads", _osmMaxRoads, 1, 1000);
        _osmAutoDetectLaneCount = EditorGUILayout.Toggle("Auto Detect Lane Count", _osmAutoDetectLaneCount);
        _osmDefaultLaneCount = EditorGUILayout.IntSlider("Default Lane Count", _osmDefaultLaneCount, 1, 6);
        _osmRoadFilter = (OsmRoadFilter)EditorGUILayout.EnumPopup("Road Filter", _osmRoadFilter);
        _smoothRoadJoins = EditorGUILayout.Toggle("Smooth Road Joins", _smoothRoadJoins);
        _mergeIntersections = EditorGUILayout.Toggle("Merge Intersections", _mergeIntersections);

        using (new EditorGUI.DisabledScope(!_mergeIntersections))
        {
            _intersectionPadding = EditorGUILayout.Slider("Intersection Padding", _intersectionPadding, 0f, 6f);
        }

        if (GUILayout.Button("Download and Generate Roads"))
        {
            DownloadAndGenerateOsmRoads();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Study Purok Visible Box", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates or updates one CesiumCartographicPolygon box for Baybayon, Sibucao, and Riverside, then configures Cesium polygon overlays to hide everything outside it.", MessageType.Info);
        _studyPurokSouthLatitude = EditorGUILayout.DoubleField("South Latitude", _studyPurokSouthLatitude);
        _studyPurokWestLongitude = EditorGUILayout.DoubleField("West Longitude", _studyPurokWestLongitude);
        _studyPurokNorthLatitude = EditorGUILayout.DoubleField("North Latitude", _studyPurokNorthLatitude);
        _studyPurokEastLongitude = EditorGUILayout.DoubleField("East Longitude", _studyPurokEastLongitude);
        _studyPurokHeightMeters = EditorGUILayout.DoubleField("Anchor Height Meters", _studyPurokHeightMeters);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Study Box Preset"))
            {
                UseStudyPurokVisibleBoxPreset();
            }

            if (GUILayout.Button("Apply Visible Box"))
            {
                ApplyStudyPurokVisibleBox();
            }
        }

        if (GUILayout.Button("Clear Visible Box"))
        {
            ClearStudyPurokVisibleBox();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Automatic Buildings From OSM", EditorStyles.boldLabel);
        _buildingGeoreference = (CesiumGeoreference)EditorGUILayout.ObjectField("Cesium Georeference", _buildingGeoreference, typeof(CesiumGeoreference), true);
        _buildingTerrainTileset = (Cesium3DTileset)EditorGUILayout.ObjectField("Terrain Tileset", _buildingTerrainTileset, typeof(Cesium3DTileset), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Cesium References"))
            {
                ResolveBuildingCesiumReferences(true);
            }

            if (GUILayout.Button("Use Banago Building BBox"))
            {
                UseBanagoBuildingPreset();
            }
        }

        _buildingSouthLatitude = EditorGUILayout.DoubleField("South Latitude", _buildingSouthLatitude);
        _buildingWestLongitude = EditorGUILayout.DoubleField("West Longitude", _buildingWestLongitude);
        _buildingNorthLatitude = EditorGUILayout.DoubleField("North Latitude", _buildingNorthLatitude);
        _buildingEastLongitude = EditorGUILayout.DoubleField("East Longitude", _buildingEastLongitude);
        _buildingRequestTimeoutSeconds = EditorGUILayout.IntSlider("Request Timeout", _buildingRequestTimeoutSeconds, 10, 90);
        _buildingFewResultWarningThreshold = EditorGUILayout.IntSlider("Sparse Warning Count", _buildingFewResultWarningThreshold, 1, 50);
        _buildingDefaultStoryHeightMeters = EditorGUILayout.FloatField("Story Height Meters", _buildingDefaultStoryHeightMeters);
        _buildingDefaultMinimumStories = EditorGUILayout.IntSlider("Default Min Stories", _buildingDefaultMinimumStories, 1, 5);
        _buildingDefaultMaximumStories = EditorGUILayout.IntSlider("Default Max Stories", _buildingDefaultMaximumStories, _buildingDefaultMinimumStories, 8);
        _buildingMinimumFootprintDimensionMeters = EditorGUILayout.FloatField("Min Footprint Meters", _buildingMinimumFootprintDimensionMeters);
        _buildingMaximumFootprintDimensionMeters = EditorGUILayout.FloatField("Max Footprint Meters", _buildingMaximumFootprintDimensionMeters);
        _buildingFallbackBaseElevationMeters = EditorGUILayout.DoubleField("Fallback Base Elevation", _buildingFallbackBaseElevationMeters);
        _buildingClearExistingBeforeGenerate = EditorGUILayout.Toggle("Clear Existing First", _buildingClearExistingBeforeGenerate);
        _buildingAddBoxColliders = EditorGUILayout.Toggle("Add Box Colliders", _buildingAddBoxColliders);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_buildingGenerationInProgress))
            {
                if (GUILayout.Button("Download and Generate OSM Buildings"))
                {
                    DownloadAndGenerateOsmBuildings();
                }
            }

            if (GUILayout.Button("Clear Generated Buildings"))
            {
                ClearGeneratedBuildings();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Banago Elevation Plane", EditorStyles.boldLabel);
        _planeCenterLatitude = EditorGUILayout.DoubleField("Center Latitude", _planeCenterLatitude);
        _planeCenterLongitude = EditorGUILayout.DoubleField("Center Longitude", _planeCenterLongitude);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use OSM Center"))
            {
                _planeCenterLatitude = _osmCenterLatitude;
                _planeCenterLongitude = _osmCenterLongitude;
                _report = "Elevation plane center set from the current OSM center.";
            }

            if (GUILayout.Button("Use Banago Plane Preset"))
            {
                UseBanagoElevationPlanePreset();
            }
        }

        _planeSizeMeters = EditorGUILayout.Slider("Plane Size Meters", _planeSizeMeters, 100f, 5000f);
        _planeResolution = EditorGUILayout.IntSlider("Plane Resolution", _planeResolution, 4, 160);
        _planeHeightOffset = EditorGUILayout.FloatField("Plane Height Offset", _planeHeightOffset);
        _planeUseMeshVertexFallback = EditorGUILayout.Toggle("Use Mesh Vertex Fallback", _planeUseMeshVertexFallback);
        _planeCreateCollider = EditorGUILayout.Toggle("Create Mesh Collider", _planeCreateCollider);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Elevation Plane"))
            {
                GenerateElevationPlane();
            }

            if (GUILayout.Button("Clear Elevation Plane"))
            {
                ClearElevationPlane();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void AnalyzeSelection()
    {
        GameObject selected = GetTargetObject();
        if (selected == null)
        {
            _report = "No object selected.";
            return;
        }

        if (!TryGetBounds(selected, out Bounds bounds))
        {
            _report = "The selected object has no renderer or collider bounds to sample.";
            return;
        }

        int hits = 0;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        double total = 0d;
        int steps = Mathf.Max(2, _sampleResolution);

        for (int x = 0; x < steps; x++)
        {
            float tx = x / (float)(steps - 1);
            for (int z = 0; z < steps; z++)
            {
                float tz = z / (float)(steps - 1);
                Vector3 probe = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, tx),
                    bounds.max.y + Mathf.Max(1f, _raycastPadding),
                    Mathf.Lerp(bounds.min.z, bounds.max.z, tz));

                if (!TryRaycastSurface(probe, out Vector3 hitPoint, selected))
                {
                    continue;
                }

                hits++;
                min = Mathf.Min(min, hitPoint.y);
                max = Mathf.Max(max, hitPoint.y);
                total += hitPoint.y;
            }
        }

        if (hits == 0)
        {
            if (TryAnalyzeMeshVertices(selected, out string meshReport))
            {
                _report = meshReport + "\nRaycast hits: 0. Lane projection will be flat unless the selected map has colliders or Cesium physics meshes loaded.";
                return;
            }

            _report = "No surface hits found. Make sure the selected map has colliders or Cesium physics meshes are enabled and loaded.";
            return;
        }

        float range = max - min;
        float average = (float)(total / hits);
        _report =
            $"Samples hit: {hits}/{steps * steps}\n" +
            $"Min height: {min:0.###}\n" +
            $"Max height: {max:0.###}\n" +
            $"Average height: {average:0.###}\n" +
            $"Height range: {range:0.###}\n" +
            GetFlatnessText(range);
    }

    private static bool TryAnalyzeMeshVertices(GameObject selected, out string report)
    {
        MeshFilter[] meshFilters = selected.GetComponentsInChildren<MeshFilter>();
        int vertexCount = 0;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        double total = 0d;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = localToWorld.MultiplyPoint3x4(vertices[i]).y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
                total += y;
                vertexCount++;
            }
        }

        if (vertexCount == 0)
        {
            report = string.Empty;
            return false;
        }

        float range = max - min;
        float average = (float)(total / vertexCount);
        report =
            $"Mesh vertices checked: {vertexCount}\n" +
            $"Min vertex height: {min:0.###}\n" +
            $"Max vertex height: {max:0.###}\n" +
            $"Average vertex height: {average:0.###}\n" +
            $"Vertex height range: {range:0.###}\n" +
            GetFlatnessText(range);
        return true;
    }

    private static string GetFlatnessText(float range)
    {
        if (range <= 0.25f)
        {
            return "Very flat: a simple plane/strip overlay should work.";
        }

        if (range <= 1.5f)
        {
            return "Mild elevation change: terrain-following lanes are recommended.";
        }

        return "Noticeable elevation change: use terrain-following meshes, not a flat plane.";
    }

    private void CreateLanePreview()
    {
        GameObject selected = GetTargetObject();
        if (selected == null)
        {
            _report = "Select a map object before creating lanes.";
            return;
        }

        Vector3 center = GetLaneCenter(selected);
        Transform root = GetOrCreateGeneratedRoot().transform;
        Material roadMaterial = GetOrCreateMaterial("Generated_Road_Surface.mat", new Color(0.035f, 0.038f, 0.04f, 1f));
        Material lineMaterial = GetOrCreateMaterial("Generated_Lane_Markings.mat", new Color(1f, 0.92f, 0.55f, 1f));

        if (_createRoadSurface)
        {
            CreateMeshObject("Road Surface Preview", BuildRoadSurfaceMesh(center, selected), roadMaterial, root);
        }

        CreateMeshObject("Lane Markings Preview", BuildLaneMarkingMesh(center, selected), lineMaterial, root);
        _report = "Lane preview generated. If lines float or clip, adjust Height Offset and regenerate.";
    }

    private Mesh BuildRoadSurfaceMesh(Vector3 center, GameObject selected)
    {
        float roadWidth = Mathf.Max(0.5f, _laneCount * _laneWidth);
        int lengthSegments = Mathf.Max(2, _surfaceLengthSegments);
        int widthSegments = Mathf.Max(1, _laneCount);
        Quaternion rotation = GetLaneRotation();
        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int z = 0; z <= lengthSegments; z++)
        {
            float tz = z / (float)lengthSegments;
            float along = Mathf.Lerp(-_roadLength * 0.5f, _roadLength * 0.5f, tz);
            for (int x = 0; x <= widthSegments; x++)
            {
                float tx = x / (float)widthSegments;
                float across = Mathf.Lerp(-roadWidth * 0.5f, roadWidth * 0.5f, tx);
                Vector3 point = center + forward * along + right * across;
                vertices.Add(ProjectToSurface(point, selected, center.y, Mathf.Max(0f, _heightOffset * 0.5f)));
                uvs.Add(new Vector2(tx, tz));
            }
        }

        int row = widthSegments + 1;
        for (int z = 0; z < lengthSegments; z++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int a = z * row + x;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        return CreateMesh("Generated Road Surface", vertices, triangles, uvs);
    }

    private Mesh BuildLaneMarkingMesh(Vector3 center, GameObject selected)
    {
        float roadWidth = Mathf.Max(0.5f, _laneCount * _laneWidth);
        Quaternion rotation = GetLaneRotation();
        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int lineIndex = 0; lineIndex <= _laneCount; lineIndex++)
        {
            float offset = -roadWidth * 0.5f + lineIndex * _laneWidth;
            bool isOuterLine = lineIndex == 0 || lineIndex == _laneCount;
            bool dashed = _dashedInnerLines && !isOuterLine;

            if (dashed)
            {
                float cursor = -_roadLength * 0.5f;
                while (cursor < _roadLength * 0.5f)
                {
                    float start = cursor;
                    float end = Mathf.Min(cursor + Mathf.Max(0.1f, _dashLength), _roadLength * 0.5f);
                    AddLineSegment(vertices, triangles, center, selected, forward, right, offset, start, end);
                    cursor += Mathf.Max(0.1f, _dashLength) + Mathf.Max(0.1f, _dashGap);
                }
            }
            else
            {
                AddLineSegment(vertices, triangles, center, selected, forward, right, offset, -_roadLength * 0.5f, _roadLength * 0.5f);
            }
        }

        return CreateMesh("Generated Lane Markings", vertices, triangles);
    }

    private void AddLineSegment(List<Vector3> vertices, List<int> triangles, Vector3 center, GameObject selected, Vector3 forward, Vector3 right, float laneOffset, float start, float end)
    {
        float halfWidth = Mathf.Max(0.01f, _lineWidth * 0.5f);
        int baseIndex = vertices.Count;
        Vector3 p0 = center + forward * start + right * (laneOffset - halfWidth);
        Vector3 p1 = center + forward * start + right * (laneOffset + halfWidth);
        Vector3 p2 = center + forward * end + right * (laneOffset - halfWidth);
        Vector3 p3 = center + forward * end + right * (laneOffset + halfWidth);

        vertices.Add(ProjectToSurface(p0, selected, center.y, Mathf.Max(0f, _heightOffset)));
        vertices.Add(ProjectToSurface(p1, selected, center.y, Mathf.Max(0f, _heightOffset)));
        vertices.Add(ProjectToSurface(p2, selected, center.y, Mathf.Max(0f, _heightOffset)));
        vertices.Add(ProjectToSurface(p3, selected, center.y, Mathf.Max(0f, _heightOffset)));

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    private Vector3 ProjectToSurface(Vector3 point, GameObject selected, float fallbackY, float offset)
    {
        Vector3 probe = point + Vector3.up * Mathf.Max(1f, _raycastPadding);
        if (TryRaycastSurface(probe, out Vector3 hitPoint, selected))
        {
            hitPoint.y += offset;
            return hitPoint;
        }

        point.y = fallbackY + offset;
        return point;
    }

    private bool TryRaycastSurface(Vector3 origin, out Vector3 hitPoint, GameObject selected)
    {
        float distance = Mathf.Max(10f, _raycastPadding * 4f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsGeneratedLaneObject(hit.collider.gameObject))
            {
                continue;
            }

            if (selected == null || hit.collider.transform.IsChildOf(selected.transform) || selected.transform.IsChildOf(hit.collider.transform) || hits.Length == 1)
            {
                hitPoint = hit.point;
                return true;
            }
        }

        hitPoint = default;
        return false;
    }

    private static bool TryGetBounds(GameObject gameObject, out Bounds bounds)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
        bool initialized = false;
        bounds = new Bounds(gameObject.transform.position, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        foreach (Collider collider in colliders)
        {
            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return initialized;
    }

    private static bool IsGeneratedLaneObject(GameObject gameObject)
    {
        Transform current = gameObject.transform;
        while (current != null)
        {
            if (current.name == GeneratedRootName ||
                current.name == GeneratedPlaneRootName ||
                current.name == GeneratedBuildingRootName ||
                current.name == GeneratedStudyPurokVisibleBoxName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static GameObject GetOrCreateGeneratedRoot()
    {
        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Generated Lanes Root");
        return root;
    }

    private static GameObject CreateMeshObject(string name, Mesh mesh, Material material, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Lane Preview");
        gameObject.transform.SetParent(parent, false);

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        EditorUtility.SetDirty(gameObject);
        return gameObject;
    }

    private static Material GetOrCreateMaterial(string fileName, Color color)
    {
        if (!Directory.Exists(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }

        string path = $"{MaterialFolder}/{fileName}";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }

    private void ClearGeneratedLanes()
    {
        GameObject root = GameObject.Find(GeneratedRootName);
        if (root == null)
        {
            _report = "No generated lane preview found.";
            return;
        }

        Undo.DestroyObjectImmediate(root);
        _report = "Generated lane preview cleared.";
    }

    private void UseStudyPurokVisibleBoxPreset()
    {
        _studyPurokSouthLatitude = StudyPurokVisibleBoxSouthLatitude;
        _studyPurokWestLongitude = StudyPurokVisibleBoxWestLongitude;
        _studyPurokNorthLatitude = StudyPurokVisibleBoxNorthLatitude;
        _studyPurokEastLongitude = StudyPurokVisibleBoxEastLongitude;
        _studyPurokHeightMeters = StudyPurokVisibleBoxHeightMeters;
        _report = "Study purok visible box preset applied. Click Apply Visible Box to update the Cesium polygon mask.";
    }

    private void ApplyStudyPurokVisibleBox()
    {
        if (!ValidateStudyPurokVisibleBoxBounds())
        {
            return;
        }

        ResolveBuildingCesiumReferences(false);
        if (_buildingGeoreference == null)
        {
            _report = "Could not find a CesiumGeoreference for the study purok visible box.";
            return;
        }

        CesiumCartographicPolygon polygon = GetOrCreateStudyPurokVisiblePolygon(_buildingGeoreference);
        ConfigureStudyPurokVisiblePolygon(polygon, _buildingGeoreference);
        int overlayCount = ConfigureStudyPurokVisibleBoxOverlays(polygon, _buildingGeoreference);

        Selection.activeGameObject = polygon.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }

        _report = overlayCount > 0
            ? "Applied study purok visible box. Cesium tilesets now hide everything outside Baybayon, Sibucao, and Riverside's approximate box."
            : "Created the study purok visible box, but no Cesium3DTileset objects were found to receive the clipping overlay.";
    }

    private void ClearStudyPurokVisibleBox()
    {
        GameObject box = GameObject.Find(GeneratedStudyPurokVisibleBoxName);
        if (box == null)
        {
            _report = "No generated study purok visible box found.";
            return;
        }

        CesiumCartographicPolygon polygon = box.GetComponent<CesiumCartographicPolygon>();
        int overlayCount = RemoveStudyPurokVisibleBoxFromOverlays(polygon);
        Undo.DestroyObjectImmediate(box);
        _report = "Cleared the study purok visible box and removed it from " +
            overlayCount.ToString(CultureInfo.InvariantCulture) +
            " Cesium polygon overlay(s).";
    }

    private bool ValidateStudyPurokVisibleBoxBounds()
    {
        if (_studyPurokSouthLatitude >= _studyPurokNorthLatitude)
        {
            _report = "Study purok box is invalid: South Latitude must be less than North Latitude.";
            return false;
        }

        if (_studyPurokWestLongitude >= _studyPurokEastLongitude)
        {
            _report = "Study purok box is invalid: West Longitude must be less than East Longitude.";
            return false;
        }

        if (_studyPurokSouthLatitude < -90d || _studyPurokNorthLatitude > 90d)
        {
            _report = "Study purok box is invalid: latitudes must stay between -90 and 90 degrees.";
            return false;
        }

        if (_studyPurokWestLongitude < -180d || _studyPurokEastLongitude > 180d)
        {
            _report = "Study purok box is invalid: longitudes must stay between -180 and 180 degrees.";
            return false;
        }

        return true;
    }

    private CesiumCartographicPolygon GetOrCreateStudyPurokVisiblePolygon(CesiumGeoreference georeference)
    {
        GameObject existing = GameObject.Find(GeneratedStudyPurokVisibleBoxName);
        if (existing != null)
        {
            return EnsureStudyPurokVisiblePolygonComponents(existing);
        }

        CesiumCartographicPolygon polygon = FindReusableStudyPurokVisiblePolygon(georeference);
        if (polygon != null)
        {
            Undo.RecordObject(polygon.gameObject, "Reuse Study Purok Visible Box");
            polygon.gameObject.name = GeneratedStudyPurokVisibleBoxName;
            return EnsureStudyPurokVisiblePolygonComponents(polygon.gameObject);
        }

        GameObject box = new GameObject(GeneratedStudyPurokVisibleBoxName);
        Undo.RegisterCreatedObjectUndo(box, "Create Study Purok Visible Box");
        box.transform.SetParent(georeference.transform, false);
        box.transform.localPosition = Vector3.zero;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = Vector3.one;

        GetOrAddComponent<SplineContainer>(box);
        GetOrAddComponent<CesiumGlobeAnchor>(box);
        return GetOrAddComponent<CesiumCartographicPolygon>(box);
    }

    private static CesiumCartographicPolygon FindReusableStudyPurokVisiblePolygon(CesiumGeoreference georeference)
    {
        CesiumPolygonRasterOverlay[] overlays = georeference != null
            ? georeference.GetComponentsInChildren<CesiumPolygonRasterOverlay>(true)
            : FindObjectsByType<CesiumPolygonRasterOverlay>(FindObjectsInactive.Include);

        for (int i = 0; i < overlays.Length; i++)
        {
            List<CesiumCartographicPolygon> polygons = overlays[i].polygons;
            if (polygons == null)
            {
                continue;
            }

            for (int j = 0; j < polygons.Count; j++)
            {
                if (polygons[j] != null)
                {
                    return polygons[j];
                }
            }
        }

        CesiumCartographicPolygon[] candidates = georeference != null
            ? georeference.GetComponentsInChildren<CesiumCartographicPolygon>(true)
            : FindObjectsByType<CesiumCartographicPolygon>(FindObjectsInactive.Include);

        return candidates.Length > 0 ? candidates[0] : null;
    }

    private static CesiumCartographicPolygon EnsureStudyPurokVisiblePolygonComponents(GameObject box)
    {
        GetOrAddComponent<SplineContainer>(box);
        GetOrAddComponent<CesiumGlobeAnchor>(box);
        return GetOrAddComponent<CesiumCartographicPolygon>(box);
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private void ConfigureStudyPurokVisiblePolygon(CesiumCartographicPolygon polygon, CesiumGeoreference georeference)
    {
        GameObject box = polygon.gameObject;
        if (box.transform.parent != georeference.transform)
        {
            Undo.SetTransformParent(box.transform, georeference.transform, "Parent Study Purok Visible Box");
        }

        Undo.RecordObject(box, "Configure Study Purok Visible Box");
        box.name = GeneratedStudyPurokVisibleBoxName;

        CesiumGlobeAnchor anchor = GetOrAddComponent<CesiumGlobeAnchor>(box);
        double centerLatitude = (_studyPurokSouthLatitude + _studyPurokNorthLatitude) * 0.5d;
        double centerLongitude = (_studyPurokWestLongitude + _studyPurokEastLongitude) * 0.5d;

        Undo.RecordObject(anchor, "Anchor Study Purok Visible Box");
        anchor.adjustOrientationForGlobeWhenMoving = true;
        anchor.detectTransformChanges = true;
        anchor.longitudeLatitudeHeight = new double3(centerLongitude, centerLatitude, _studyPurokHeightMeters);

        Undo.RecordObject(box.transform, "Scale Study Purok Visible Box");
        box.transform.localScale = Vector3.one;

        ConfigureStudyPurokVisibleSpline(
            GetOrAddComponent<SplineContainer>(box),
            centerLatitude,
            centerLongitude);

        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(anchor);
        EditorUtility.SetDirty(polygon);
    }

    private void ConfigureStudyPurokVisibleSpline(
        SplineContainer container,
        double centerLatitude,
        double centerLongitude)
    {
        Undo.RecordObject(container, "Shape Study Purok Visible Box");

        IReadOnlyList<Spline> splines = container.Splines;
        for (int i = splines.Count - 1; i >= 0; i--)
        {
            container.RemoveSpline(splines[i]);
        }

        Vector3 southwest = LatLonToUnity(_studyPurokSouthLatitude, _studyPurokWestLongitude, centerLatitude, centerLongitude);
        Vector3 southeast = LatLonToUnity(_studyPurokSouthLatitude, _studyPurokEastLongitude, centerLatitude, centerLongitude);
        Vector3 northeast = LatLonToUnity(_studyPurokNorthLatitude, _studyPurokEastLongitude, centerLatitude, centerLongitude);
        Vector3 northwest = LatLonToUnity(_studyPurokNorthLatitude, _studyPurokWestLongitude, centerLatitude, centerLongitude);

        Spline boxSpline = new Spline();
        BezierKnot[] knots =
        {
            CreateBoxKnot(southwest),
            CreateBoxKnot(southeast),
            CreateBoxKnot(northeast),
            CreateBoxKnot(northwest)
        };

        boxSpline.Knots = knots;
        boxSpline.Closed = true;
        boxSpline.SetTangentMode(TangentMode.Linear);

        container.AddSpline(boxSpline);
        EditorUtility.SetDirty(container);
    }

    private static BezierKnot CreateBoxKnot(Vector3 position)
    {
        return new BezierKnot(new float3(position.x, 0f, position.z));
    }

    private static int ConfigureStudyPurokVisibleBoxOverlays(
        CesiumCartographicPolygon polygon,
        CesiumGeoreference georeference)
    {
        Cesium3DTileset[] tilesets = georeference.GetComponentsInChildren<Cesium3DTileset>(true);
        if (tilesets.Length == 0)
        {
            tilesets = FindObjectsByType<Cesium3DTileset>(FindObjectsInactive.Include);
        }

        int overlayCount = 0;
        for (int i = 0; i < tilesets.Length; i++)
        {
            if (tilesets[i] == null)
            {
                continue;
            }

            CesiumPolygonRasterOverlay overlay = tilesets[i].GetComponent<CesiumPolygonRasterOverlay>();
            if (overlay == null)
            {
                overlay = Undo.AddComponent<CesiumPolygonRasterOverlay>(tilesets[i].gameObject);
            }

            Undo.RecordObject(overlay, "Apply Study Purok Visible Box Overlay");
            overlay.materialKey = "Clipping";
            overlay.polygons = new List<CesiumCartographicPolygon> { polygon };
            overlay.invertSelection = true;
            overlay.excludeSelectedTiles = true;
            overlay.enabled = true;
            EditorUtility.SetDirty(overlay);
            overlayCount++;
        }

        return overlayCount;
    }

    private static int RemoveStudyPurokVisibleBoxFromOverlays(CesiumCartographicPolygon polygon)
    {
        if (polygon == null)
        {
            return 0;
        }

        CesiumPolygonRasterOverlay[] overlays = FindObjectsByType<CesiumPolygonRasterOverlay>(FindObjectsInactive.Include);
        int overlayCount = 0;
        for (int i = 0; i < overlays.Length; i++)
        {
            List<CesiumCartographicPolygon> polygons = overlays[i].polygons;
            if (polygons == null || !polygons.Contains(polygon))
            {
                continue;
            }

            List<CesiumCartographicPolygon> updatedPolygons = new List<CesiumCartographicPolygon>(polygons);
            updatedPolygons.RemoveAll(candidate => candidate == polygon);

            Undo.RecordObject(overlays[i], "Clear Study Purok Visible Box Overlay");
            overlays[i].polygons = updatedPolygons;
            EditorUtility.SetDirty(overlays[i]);
            overlayCount++;
        }

        return overlayCount;
    }

    private void GenerateElevationPlane()
    {
        GameObject selected = GetTargetObject();
        if (selected == null)
        {
            _report = "Select Cesium World Terrain or another map surface before generating the elevation plane.";
            return;
        }

        ClearElevationPlane();

        try
        {
            EditorUtility.DisplayProgressBar("StormSim Elevation Plane", "Sampling map elevation...", 0.25f);
            Mesh planeMesh = BuildElevationPlaneMesh(selected, out ElevationPlaneStats stats);

            EditorUtility.DisplayProgressBar("StormSim Elevation Plane", "Creating plane object...", 0.85f);
            GameObject root = new GameObject(GeneratedPlaneRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Banago Elevation Plane");

            Material material = GetOrCreateMaterial("Generated_Banago_Elevation_Plane.mat", new Color(0.11f, 0.2f, 0.14f, 1f));
            GameObject plane = CreateMeshObject("Banago Elevation Mesh", planeMesh, material, root.transform);
            if (_planeCreateCollider)
            {
                MeshCollider collider = plane.AddComponent<MeshCollider>();
                collider.sharedMesh = planeMesh;
            }

            _report =
                $"Banago elevation plane generated.\n" +
                $"Vertices: {stats.TotalVertices}\n" +
                $"Raycast samples: {stats.RaycastHits}\n" +
                $"Mesh fallback samples: {stats.MeshFallbackHits}\n" +
                $"Flat fallback samples: {stats.FlatFallbacks}\n" +
                $"Height range: {stats.MinHeight:0.###} to {stats.MaxHeight:0.###}";
        }
        catch (Exception exception)
        {
            _report = "Could not generate Banago elevation plane: " + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private Mesh BuildElevationPlaneMesh(GameObject selected, out ElevationPlaneStats stats)
    {
        int segments = Mathf.Max(1, _planeResolution);
        int vertexCountPerSide = segments + 1;
        float size = Mathf.Max(1f, _planeSizeMeters);
        float halfSize = size * 0.5f;
        Vector3 center = LatLonToUnity(_planeCenterLatitude, _planeCenterLongitude, _osmCenterLatitude, _osmCenterLongitude);
        float fallbackY = TryGetBounds(selected, out Bounds bounds) ? bounds.center.y : selected.transform.position.y;
        List<Vector3> meshSamples = _planeUseMeshVertexFallback ? CollectMeshVertexSamples(selected) : new List<Vector3>();
        List<Vector3> vertices = new List<Vector3>(vertexCountPerSide * vertexCountPerSide);
        List<Vector2> uvs = new List<Vector2>(vertexCountPerSide * vertexCountPerSide);
        List<int> triangles = new List<int>(segments * segments * 6);
        stats = new ElevationPlaneStats();

        for (int z = 0; z <= segments; z++)
        {
            float tz = z / (float)segments;
            for (int x = 0; x <= segments; x++)
            {
                float tx = x / (float)segments;
                Vector3 point = new Vector3(
                    center.x + Mathf.Lerp(-halfSize, halfSize, tx),
                    fallbackY,
                    center.z + Mathf.Lerp(-halfSize, halfSize, tz));

                Vector3 vertex = SampleElevationPoint(point, selected, meshSamples, fallbackY, ref stats);
                vertices.Add(vertex);
                uvs.Add(new Vector2(tx, tz));
            }
        }

        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int a = z * vertexCountPerSide + x;
                int b = a + 1;
                int c = a + vertexCountPerSide;
                int d = c + 1;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        return CreateMesh("Generated Banago Elevation Plane", vertices, triangles, uvs);
    }

    private Vector3 SampleElevationPoint(Vector3 point, GameObject selected, List<Vector3> meshSamples, float fallbackY, ref ElevationPlaneStats stats)
    {
        Vector3 probe = point + Vector3.up * Mathf.Max(1f, _raycastPadding);
        float y;

        if (TryRaycastSurface(probe, out Vector3 hitPoint, selected))
        {
            y = hitPoint.y + _planeHeightOffset;
            stats.AddRaycastHeight(y);
            point.y = y;
            return point;
        }

        if (TryGetNearestMeshSampleHeight(point, meshSamples, out y))
        {
            y += _planeHeightOffset;
            stats.AddMeshFallbackHeight(y);
            point.y = y;
            return point;
        }

        y = fallbackY + _planeHeightOffset;
        stats.AddFlatFallbackHeight(y);
        point.y = y;
        return point;
    }

    private static List<Vector3> CollectMeshVertexSamples(GameObject selected)
    {
        const int maxSamples = 120000;
        List<Vector3> samples = new List<Vector3>();
        MeshFilter[] meshFilters = selected.GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            int stride = Mathf.Max(1, vertices.Length / 20000);
            Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i += stride)
            {
                samples.Add(localToWorld.MultiplyPoint3x4(vertices[i]));
                if (samples.Count >= maxSamples)
                {
                    return samples;
                }
            }
        }

        return samples;
    }

    private static bool TryGetNearestMeshSampleHeight(Vector3 point, List<Vector3> samples, out float height)
    {
        height = 0f;
        if (samples.Count == 0)
        {
            return false;
        }

        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 sample = samples[i];
            float dx = point.x - sample.x;
            float dz = point.z - sample.z;
            float sqrDistance = dx * dx + dz * dz;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                height = sample.y;
            }
        }

        return bestSqrDistance < float.PositiveInfinity;
    }

    private void ClearElevationPlane()
    {
        GameObject root = GameObject.Find(GeneratedPlaneRootName);
        if (root == null)
        {
            _report = "No generated Banago elevation plane found.";
            return;
        }

        Undo.DestroyObjectImmediate(root);
        _report = "Generated Banago elevation plane cleared.";
    }

    private void DownloadAndGenerateOsmRoads()
    {
        GameObject selected = GetTargetObject();
        if (selected == null)
        {
            _report = "Select Cesium World Terrain or another map surface before generating OSM roads.";
            return;
        }

        string url = "https://overpass-api.de/api/interpreter?data=" + Uri.EscapeDataString(BuildOverpassQuery());

        try
        {
            EditorUtility.DisplayProgressBar("StormSim Roads", "Downloading OpenStreetMap road data...", 0.2f);
            string xml;
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "StormSim Unity Editor Road Tool");
                xml = client.DownloadString(url);
            }

            EditorUtility.DisplayProgressBar("StormSim Roads", "Building road meshes...", 0.65f);
            OsmGenerationStats stats = GenerateRoadsFromOsmXml(xml, selected);
            _report =
                $"Generated {stats.RoadCount} OSM roads.\n" +
                $"2-lane roads: {stats.TwoLaneRoads}\n" +
                $"4-lane roads: {stats.FourLaneRoads}\n" +
                $"Other lane counts: {stats.OtherLaneRoads}\n" +
                "Use Clear Generated Lanes to remove them and regenerate with different settings.";
        }
        catch (Exception exception)
        {
            _report = "Could not generate OSM roads: " + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void DownloadAndGenerateOsmBuildings()
    {
        if (_buildingGenerationInProgress)
        {
            _report = "Building generation is already waiting for Cesium terrain heights.";
            return;
        }

        if (!ValidateBuildingBounds())
        {
            return;
        }

        ResolveBuildingCesiumReferences(false);
        if (_buildingGeoreference == null)
        {
            _report = "Could not find a CesiumGeoreference for OSM building generation.";
            return;
        }

        if (_buildingTerrainTileset == null)
        {
            _report = "Could not find a Cesium3DTileset terrain source for OSM building height sampling.";
            return;
        }

        try
        {
            if (_buildingClearExistingBeforeGenerate)
            {
                ClearGeneratedBuildings(false);
            }

            EditorUtility.DisplayProgressBar("StormSim Buildings", "Downloading OpenStreetMap building data...", 0.2f);
            string json;
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "StormSim Unity Editor Building Tool");
                client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                client.Headers[HttpRequestHeader.Accept] = "application/json";
                json = client.UploadString(
                    OverpassInterpreterUrl,
                    "POST",
                    "data=" + Uri.EscapeDataString(BuildBuildingOverpassQuery()));
            }

            EditorUtility.DisplayProgressBar("StormSim Buildings", "Parsing OSM building footprints...", 0.45f);
            List<OsmBuildingFootprint> buildings = ParseOverpassBuildingJson(json, out int skippedIncomplete);
            WarnIfBuildingCoverageLooksSparse(buildings.Count);

            if (buildings.Count == 0)
            {
                _report =
                    "No OSM building footprints were generated for Banago.\n" +
                    "Skipped incomplete footprints: " + skippedIncomplete.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "Add manual supplemental buildings around Purok Baybayon, Purok Sibucao, and Purok Riverside.";
                return;
            }

            StartBuildingHeightSampling(buildings, skippedIncomplete);
        }
        catch (Exception exception)
        {
            _buildingGenerationInProgress = false;
            _pendingBuildingHeightTask = null;
            _pendingBuildingFootprints = null;
            _report = "Could not generate OSM buildings: " + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            if (!_buildingGenerationInProgress)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    private void StartBuildingHeightSampling(List<OsmBuildingFootprint> buildings, int skippedIncomplete)
    {
        double3[] samplePositions = new double3[buildings.Count];
        for (int i = 0; i < buildings.Count; i++)
        {
            samplePositions[i] = new double3(
                buildings[i].CentroidLongitude,
                buildings[i].CentroidLatitude,
                _buildingFallbackBaseElevationMeters);
        }

        try
        {
            _pendingBuildingHeightTask = _buildingTerrainTileset.SampleHeightMostDetailed(samplePositions);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("StormSim could not start Cesium terrain height sampling. Using fallback elevation. " + exception.Message);
            BuildingGenerationStats stats = GenerateBuildingObjects(buildings, null, _buildingGeoreference);
            _report = FormatBuildingGenerationReport(stats, skippedIncomplete, true);
            return;
        }

        _pendingBuildingFootprints = buildings;
        _pendingBuildingSkippedIncomplete = skippedIncomplete;
        _pendingBuildingGeoreference = _buildingGeoreference;
        _buildingGenerationInProgress = true;
        _buildingSamplingStartedAt = EditorApplication.timeSinceStartup;
        _report =
            "Downloaded " + buildings.Count.ToString(CultureInfo.InvariantCulture) +
            " OSM building footprint(s). Waiting for Cesium terrain height samples...";

        EditorApplication.update -= PollBuildingHeightSampling;
        EditorApplication.update += PollBuildingHeightSampling;
        Repaint();
    }

    private void PollBuildingHeightSampling()
    {
        if (!_buildingGenerationInProgress || _pendingBuildingHeightTask == null || _pendingBuildingFootprints == null)
        {
            EditorApplication.update -= PollBuildingHeightSampling;
            EditorUtility.ClearProgressBar();
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - _buildingSamplingStartedAt;
        double timeoutSeconds = Math.Max(30d, _buildingRequestTimeoutSeconds + 90d);
        if (!_pendingBuildingHeightTask.IsCompleted && elapsed < timeoutSeconds)
        {
            float pulse = Mathf.PingPong((float)elapsed * 0.12f, 0.18f);
            EditorUtility.DisplayProgressBar("StormSim Buildings", "Sampling Cesium terrain heights...", 0.65f + pulse);
            return;
        }

        EditorApplication.update -= PollBuildingHeightSampling;
        EditorUtility.ClearProgressBar();

        List<OsmBuildingFootprint> buildings = _pendingBuildingFootprints;
        int skippedIncomplete = _pendingBuildingSkippedIncomplete;
        CesiumGeoreference georeference = _pendingBuildingGeoreference;
        Task<CesiumSampleHeightResult> heightTask = _pendingBuildingHeightTask;
        _pendingBuildingFootprints = null;
        _pendingBuildingSkippedIncomplete = 0;
        _pendingBuildingGeoreference = null;
        _pendingBuildingHeightTask = null;
        _buildingGenerationInProgress = false;

        try
        {
            CesiumSampleHeightResult heightResult = null;
            bool usedFallbackElevation = false;

            if (!heightTask.IsCompleted)
            {
                usedFallbackElevation = true;
                Debug.LogWarning("StormSim Cesium terrain height sampling timed out. Using fallback elevation.");
            }
            else if (heightTask.IsFaulted || heightTask.IsCanceled)
            {
                usedFallbackElevation = true;
                string message = heightTask.Exception != null
                    ? heightTask.Exception.GetBaseException().Message
                    : "Task was canceled.";
                Debug.LogWarning("StormSim Cesium terrain height sampling failed. Using fallback elevation. " + message);
            }
            else
            {
                heightResult = heightTask.Result;
                LogHeightSamplingWarnings(heightResult);
            }

            BuildingGenerationStats stats = GenerateBuildingObjects(buildings, heightResult, georeference);
            _report = FormatBuildingGenerationReport(stats, skippedIncomplete, usedFallbackElevation);
        }
        catch (Exception exception)
        {
            _report = "Could not finish OSM building generation: " + exception.Message;
            Debug.LogException(exception);
        }

        Repaint();
    }

    private BuildingGenerationStats GenerateBuildingObjects(
        List<OsmBuildingFootprint> buildings,
        CesiumSampleHeightResult heightResult,
        CesiumGeoreference georeference)
    {
        Transform root = GetOrCreateGeneratedBuildingRoot(georeference).transform;
        BuildingGenerationStats stats = new BuildingGenerationStats();

        for (int i = 0; i < buildings.Count; i++)
        {
            OsmBuildingFootprint building = buildings[i];
            double baseElevation = _buildingFallbackBaseElevationMeters;

            if (heightResult != null &&
                heightResult.longitudeLatitudeHeightPositions != null &&
                i < heightResult.longitudeLatitudeHeightPositions.Length)
            {
                bool success = heightResult.sampleSuccess != null &&
                    i < heightResult.sampleSuccess.Length &&
                    heightResult.sampleSuccess[i];

                if (success)
                {
                    baseElevation = heightResult.longitudeLatitudeHeightPositions[i].z;
                }
                else
                {
                    stats.FallbackElevationCount++;
                }
            }
            else if (heightResult == null)
            {
                stats.FallbackElevationCount++;
            }

            float height = ResolveOsmBuildingHeight(building);
            Vector2 footprintSize = CalculateBuildingFootprintSizeMeters(building);
            GameObject buildingObject = CreateOsmBuildingObject(root, building, baseElevation, height, footprintSize);
            StormSimBuildingMetadata metadata = buildingObject.AddComponent<StormSimBuildingMetadata>();

            metadata.Initialize(
                building.OsmId,
                building.BuildingTag,
                building.CentroidLatitude,
                building.CentroidLongitude,
                baseElevation,
                height,
                footprintSize,
                CreateElevatedFootprint(building, baseElevation),
                building.Tags);

            _generatedBuildingMetadata.Add(metadata);
            _generatedBuildingsByOsmId[building.OsmId] = metadata;
            stats.AddBuilding(GetBuildingMaterialCategory(building));
            EditorUtility.SetDirty(buildingObject);
            EditorUtility.SetDirty(metadata);
        }

        Selection.activeGameObject = root.gameObject;
        EditorUtility.SetDirty(root.gameObject);
        return stats;
    }

    private GameObject CreateOsmBuildingObject(
        Transform root,
        OsmBuildingFootprint building,
        double baseElevation,
        float height,
        Vector2 footprintSize)
    {
        GameObject anchorObject = new GameObject("OSM Building " + building.OsmId.ToString(CultureInfo.InvariantCulture) + " " + SanitizeObjectName(building.BuildingTag));
        Undo.RegisterCreatedObjectUndo(anchorObject, "Create OSM Building");
        anchorObject.transform.SetParent(root, false);
        anchorObject.transform.localPosition = Vector3.zero;
        anchorObject.transform.localRotation = Quaternion.identity;
        anchorObject.transform.localScale = Vector3.one;

        CesiumGlobeAnchor anchor = anchorObject.AddComponent<CesiumGlobeAnchor>();
        anchor.adjustOrientationForGlobeWhenMoving = true;
        anchor.detectTransformChanges = false;
        anchor.longitudeLatitudeHeight = new double3(building.CentroidLongitude, building.CentroidLatitude, baseElevation);

        GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volume.name = "Placeholder Volume";
        volume.transform.SetParent(anchorObject.transform, false);
        volume.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        volume.transform.localRotation = Quaternion.identity;
        volume.transform.localScale = new Vector3(footprintSize.x, height, footprintSize.y);

        Renderer renderer = volume.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = ResolveOsmBuildingMaterial(building);
        }

        if (!_buildingAddBoxColliders)
        {
            Collider collider = volume.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }
        }

        return anchorObject;
    }

    private Transform GetOrCreateGeneratedBuildingRoot(CesiumGeoreference georeference)
    {
        GameObject existing = GameObject.Find(GeneratedBuildingRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject root = new GameObject(GeneratedBuildingRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Generated OSM Buildings Root");
        if (georeference != null)
        {
            root.transform.SetParent(georeference.transform, false);
        }

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root.transform;
    }

    private void ClearGeneratedBuildings()
    {
        ClearGeneratedBuildings(true);
    }

    private void ClearGeneratedBuildings(bool updateReport)
    {
        GameObject root = GameObject.Find(GeneratedBuildingRootName);
        if (root == null)
        {
            _generatedBuildingMetadata.Clear();
            _generatedBuildingsByOsmId.Clear();
            if (updateReport)
            {
                _report = "No generated OSM buildings found.";
            }

            return;
        }

        Undo.DestroyObjectImmediate(root);
        _generatedBuildingMetadata.Clear();
        _generatedBuildingsByOsmId.Clear();
        if (updateReport)
        {
            _report = "Generated OSM buildings cleared.";
        }
    }

    private void ResolveBuildingCesiumReferences(bool updateReport)
    {
        GameObject selected = Selection.activeGameObject;

        if (_buildingGeoreference == null && selected != null)
        {
            _buildingGeoreference = selected.GetComponentInParent<CesiumGeoreference>();
            if (_buildingGeoreference == null)
            {
                _buildingGeoreference = selected.GetComponentInChildren<CesiumGeoreference>(true);
            }
        }

        if (_buildingGeoreference == null)
        {
            _buildingGeoreference = FindAnyObjectByType<CesiumGeoreference>();
        }

        if (_buildingTerrainTileset == null && selected != null)
        {
            _buildingTerrainTileset = selected.GetComponentInParent<Cesium3DTileset>();
            if (_buildingTerrainTileset == null)
            {
                _buildingTerrainTileset = selected.GetComponentInChildren<Cesium3DTileset>(true);
            }
        }

        if (_buildingTerrainTileset == null)
        {
            Cesium3DTileset[] candidates = _buildingGeoreference != null
                ? _buildingGeoreference.GetComponentsInChildren<Cesium3DTileset>(true)
                : FindObjectsByType<Cesium3DTileset>(FindObjectsInactive.Include);

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && ContainsIgnoreCase(candidates[i].name, "terrain"))
                {
                    _buildingTerrainTileset = candidates[i];
                    break;
                }
            }

            if (_buildingTerrainTileset == null && candidates.Length > 0)
            {
                _buildingTerrainTileset = candidates[0];
            }
        }

        if (updateReport)
        {
            _report = _buildingGeoreference != null && _buildingTerrainTileset != null
                ? "Cesium building references found."
                : "Could not find both CesiumGeoreference and terrain Cesium3DTileset.";
            Repaint();
        }
    }

    private void UseBanagoBuildingPreset()
    {
        _buildingSouthLatitude = BanagoBuildingSouthLatitude;
        _buildingWestLongitude = BanagoBuildingWestLongitude;
        _buildingNorthLatitude = BanagoBuildingNorthLatitude;
        _buildingEastLongitude = BanagoBuildingEastLongitude;
        _buildingRequestTimeoutSeconds = 25;
        _buildingFewResultWarningThreshold = 10;
        _buildingDefaultStoryHeightMeters = 3f;
        _buildingDefaultMinimumStories = 1;
        _buildingDefaultMaximumStories = 3;
        _buildingMinimumFootprintDimensionMeters = 2f;
        _buildingMaximumFootprintDimensionMeters = 160f;
        _report = "Banago building bbox preset applied. Click Download and Generate OSM Buildings after Cesium terrain is loaded.";
    }

    private bool ValidateBuildingBounds()
    {
        if (_buildingSouthLatitude >= _buildingNorthLatitude)
        {
            _report = "Building bbox is invalid: South Latitude must be less than North Latitude.";
            return false;
        }

        if (_buildingWestLongitude >= _buildingEastLongitude)
        {
            _report = "Building bbox is invalid: West Longitude must be less than East Longitude.";
            return false;
        }

        _buildingRequestTimeoutSeconds = Mathf.Clamp(_buildingRequestTimeoutSeconds, 10, 90);
        _buildingDefaultStoryHeightMeters = Mathf.Max(1f, _buildingDefaultStoryHeightMeters);
        _buildingDefaultMinimumStories = Mathf.Max(1, _buildingDefaultMinimumStories);
        _buildingDefaultMaximumStories = Mathf.Max(_buildingDefaultMinimumStories, _buildingDefaultMaximumStories);
        _buildingMinimumFootprintDimensionMeters = Mathf.Max(0.5f, _buildingMinimumFootprintDimensionMeters);
        _buildingMaximumFootprintDimensionMeters = Mathf.Max(_buildingMinimumFootprintDimensionMeters, _buildingMaximumFootprintDimensionMeters);
        return true;
    }

    private string BuildBuildingOverpassQuery()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[out:json][timeout:{0}];(way[\"building\"]({1},{2},{3},{4}););out tags geom;",
            _buildingRequestTimeoutSeconds,
            _buildingSouthLatitude,
            _buildingWestLongitude,
            _buildingNorthLatitude,
            _buildingEastLongitude);
    }

    private List<OsmBuildingFootprint> ParseOverpassBuildingJson(string json, out int skippedIncomplete)
    {
        List<OsmBuildingFootprint> buildings = new List<OsmBuildingFootprint>();
        skippedIncomplete = 0;

        JObject root = JObject.Parse(json);
        JArray elements = root["elements"] as JArray;
        if (elements == null)
        {
            Debug.LogWarning("StormSim Overpass response had no elements array for buildings.");
            return buildings;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            JObject element = elements[i] as JObject;
            if (element == null)
            {
                skippedIncomplete++;
                continue;
            }

            if (!string.Equals(ReadJsonString(element, "type"), "way", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Dictionary<string, string> tags = ReadJsonTags(element["tags"] as JObject);
            if (!tags.TryGetValue("building", out string buildingTag) || string.IsNullOrWhiteSpace(buildingTag))
            {
                continue;
            }

            List<StormSimGeoPoint> footprint = ReadBuildingGeometry(element["geometry"] as JArray);
            RemoveDuplicateClosingPoint(footprint);
            if (footprint.Count < 3)
            {
                skippedIncomplete++;
                continue;
            }

            CalculateBuildingCentroid(footprint, out double centroidLatitude, out double centroidLongitude);
            buildings.Add(new OsmBuildingFootprint(
                ReadJsonLong(element, "id"),
                buildingTag,
                tags,
                footprint,
                centroidLatitude,
                centroidLongitude));
        }

        if (skippedIncomplete > 0)
        {
            Debug.LogWarning("StormSim skipped " + skippedIncomplete.ToString(CultureInfo.InvariantCulture) + " incomplete OSM building footprint(s).");
        }

        return buildings;
    }

    private static Dictionary<string, string> ReadJsonTags(JObject tagsObject)
    {
        Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (tagsObject == null)
        {
            return tags;
        }

        foreach (JProperty property in tagsObject.Properties())
        {
            tags[property.Name] = property.Value != null ? property.Value.ToString() : string.Empty;
        }

        return tags;
    }

    private static List<StormSimGeoPoint> ReadBuildingGeometry(JArray geometryArray)
    {
        List<StormSimGeoPoint> points = new List<StormSimGeoPoint>();
        if (geometryArray == null)
        {
            return points;
        }

        for (int i = 0; i < geometryArray.Count; i++)
        {
            JObject point = geometryArray[i] as JObject;
            if (point == null)
            {
                continue;
            }

            double latitude = ReadJsonDouble(point, "lat");
            double longitude = ReadJsonDouble(point, "lon");
            if (!double.IsNaN(latitude) && !double.IsNaN(longitude))
            {
                points.Add(new StormSimGeoPoint(latitude, longitude, 0d));
            }
        }

        return points;
    }

    private List<StormSimGeoPoint> CreateElevatedFootprint(OsmBuildingFootprint building, double baseElevation)
    {
        List<StormSimGeoPoint> footprint = new List<StormSimGeoPoint>(building.Footprint.Count);
        for (int i = 0; i < building.Footprint.Count; i++)
        {
            footprint.Add(new StormSimGeoPoint(
                building.Footprint[i].Latitude,
                building.Footprint[i].Longitude,
                baseElevation));
        }

        return footprint;
    }

    private Vector2 CalculateBuildingFootprintSizeMeters(OsmBuildingFootprint building)
    {
        double minLatitude = double.MaxValue;
        double maxLatitude = double.MinValue;
        double minLongitude = double.MaxValue;
        double maxLongitude = double.MinValue;

        for (int i = 0; i < building.Footprint.Count; i++)
        {
            StormSimGeoPoint point = building.Footprint[i];
            minLatitude = Math.Min(minLatitude, point.Latitude);
            maxLatitude = Math.Max(maxLatitude, point.Latitude);
            minLongitude = Math.Min(minLongitude, point.Longitude);
            maxLongitude = Math.Max(maxLongitude, point.Longitude);
        }

        double metersPerDegreeLatitude = 111320d;
        double metersPerDegreeLongitude = Math.Cos(building.CentroidLatitude * Math.PI / 180d) * metersPerDegreeLatitude;
        float width = (float)((maxLongitude - minLongitude) * metersPerDegreeLongitude);
        float depth = (float)((maxLatitude - minLatitude) * metersPerDegreeLatitude);

        width = Mathf.Clamp(width, _buildingMinimumFootprintDimensionMeters, _buildingMaximumFootprintDimensionMeters);
        depth = Mathf.Clamp(depth, _buildingMinimumFootprintDimensionMeters, _buildingMaximumFootprintDimensionMeters);
        return new Vector2(width, depth);
    }

    private float ResolveOsmBuildingHeight(OsmBuildingFootprint building)
    {
        if (TryReadOsmHeightMeters(building.Tags, out float heightMeters))
        {
            return Mathf.Max(_buildingDefaultStoryHeightMeters, heightMeters);
        }

        if (TryReadOsmBuildingLevels(building.Tags, out float levels))
        {
            return Mathf.Max(_buildingDefaultStoryHeightMeters, levels * _buildingDefaultStoryHeightMeters);
        }

        return ResolveDeterministicDefaultBuildingHeight(building.OsmId);
    }

    private float ResolveDeterministicDefaultBuildingHeight(long osmId)
    {
        int minStories = Mathf.Max(1, _buildingDefaultMinimumStories);
        int maxStories = Mathf.Max(minStories, _buildingDefaultMaximumStories);

        unchecked
        {
            int seed = (int)(osmId ^ (osmId >> 32));
            System.Random random = new System.Random(seed);
            int stories = random.Next(minStories, maxStories + 1);
            return stories * Mathf.Max(1f, _buildingDefaultStoryHeightMeters);
        }
    }

    private static bool TryReadOsmHeightMeters(IDictionary<string, string> tags, out float heightMeters)
    {
        if (!tags.TryGetValue("height", out string value) || string.IsNullOrWhiteSpace(value))
        {
            heightMeters = 0f;
            return false;
        }

        bool isFeet = ContainsIgnoreCase(value, "ft") ||
            ContainsIgnoreCase(value, "feet") ||
            value.IndexOf("'", StringComparison.Ordinal) >= 0;

        if (!TryReadFirstFloat(value, out float parsedValue) || parsedValue <= 0f)
        {
            heightMeters = 0f;
            return false;
        }

        heightMeters = isFeet ? parsedValue * 0.3048f : parsedValue;
        return true;
    }

    private static bool TryReadOsmBuildingLevels(IDictionary<string, string> tags, out float levels)
    {
        if ((!tags.TryGetValue("building:levels", out string value) && !tags.TryGetValue("levels", out value)) ||
            string.IsNullOrWhiteSpace(value))
        {
            levels = 0f;
            return false;
        }

        if (!TryReadFirstFloat(value, out levels))
        {
            levels = 0f;
            return false;
        }

        levels = Mathf.Clamp(levels, 1f, 80f);
        return true;
    }

    private static bool TryReadFirstFloat(string text, out float value)
    {
        Match match = Regex.Match(text, @"[-+]?\d+([\.,]\d+)?");
        if (!match.Success)
        {
            value = 0f;
            return false;
        }

        string normalized = match.Value.Replace(',', '.');
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private Material ResolveOsmBuildingMaterial(OsmBuildingFootprint building)
    {
        BuildingMaterialCategory category = GetBuildingMaterialCategory(building);
        if (category == BuildingMaterialCategory.Civic)
        {
            return GetOrCreateMaterial("Generated_OSM_Building_Civic.mat", new Color(0.16f, 0.38f, 0.76f, 1f));
        }

        if (category == BuildingMaterialCategory.Government)
        {
            return GetOrCreateMaterial("Generated_OSM_Building_Government.mat", new Color(0.92f, 0.46f, 0.16f, 1f));
        }

        return GetOrCreateMaterial("Generated_OSM_Building_Residential.mat", new Color(0.55f, 0.57f, 0.58f, 1f));
    }

    private static BuildingMaterialCategory GetBuildingMaterialCategory(OsmBuildingFootprint building)
    {
        string buildingTag = building.BuildingTag;
        string amenity = GetOsmTagValue(building.Tags, "amenity");
        string emergency = GetOsmTagValue(building.Tags, "emergency");
        string office = GetOsmTagValue(building.Tags, "office");
        string operatorType = GetOsmTagValue(building.Tags, "operator:type");

        if (ContainsIgnoreCase(buildingTag, "school") ||
            ContainsIgnoreCase(buildingTag, "evacuation") ||
            ContainsIgnoreCase(amenity, "school") ||
            ContainsIgnoreCase(amenity, "college") ||
            ContainsIgnoreCase(amenity, "kindergarten") ||
            ContainsIgnoreCase(amenity, "shelter") ||
            ContainsIgnoreCase(emergency, "assembly_point"))
        {
            return BuildingMaterialCategory.Civic;
        }

        if (ContainsIgnoreCase(buildingTag, "government") ||
            ContainsIgnoreCase(buildingTag, "public") ||
            ContainsIgnoreCase(buildingTag, "civic") ||
            ContainsIgnoreCase(buildingTag, "municipal") ||
            ContainsIgnoreCase(amenity, "townhall") ||
            ContainsIgnoreCase(amenity, "police") ||
            ContainsIgnoreCase(amenity, "fire_station") ||
            ContainsIgnoreCase(office, "government") ||
            ContainsIgnoreCase(operatorType, "government"))
        {
            return BuildingMaterialCategory.Government;
        }

        return BuildingMaterialCategory.Residential;
    }

    private void WarnIfBuildingCoverageLooksSparse(int buildingCount)
    {
        if (buildingCount == 0)
        {
            Debug.LogWarning(
                "StormSim Overpass query returned zero building footprints for Banago. " +
                "You will likely need manually placed supplemental buildings, especially around Purok Baybayon, Purok Sibucao, and Purok Riverside.");
            return;
        }

        if (buildingCount <= _buildingFewResultWarningThreshold)
        {
            Debug.LogWarning(
                "StormSim Overpass query returned only " + buildingCount.ToString(CultureInfo.InvariantCulture) + " building footprint(s) for Banago. " +
                "Check coverage and consider manual supplemental buildings around Purok Baybayon, Purok Sibucao, and Purok Riverside.");
        }
    }

    private static void LogHeightSamplingWarnings(CesiumSampleHeightResult heightResult)
    {
        if (heightResult == null || heightResult.warnings == null)
        {
            return;
        }

        for (int i = 0; i < heightResult.warnings.Length; i++)
        {
            if (!string.IsNullOrEmpty(heightResult.warnings[i]))
            {
                Debug.LogWarning("StormSim Cesium height warning: " + heightResult.warnings[i]);
            }
        }
    }

    private static string FormatBuildingGenerationReport(
        BuildingGenerationStats stats,
        int skippedIncomplete,
        bool usedFallbackElevation)
    {
        string report =
            "Generated " + stats.BuildingCount.ToString(CultureInfo.InvariantCulture) + " OSM building placeholder(s).\n" +
            "Residential/default: " + stats.ResidentialCount.ToString(CultureInfo.InvariantCulture) + "\n" +
            "School/evacuation/civic: " + stats.CivicCount.ToString(CultureInfo.InvariantCulture) + "\n" +
            "Government/public: " + stats.GovernmentCount.ToString(CultureInfo.InvariantCulture) + "\n" +
            "Fallback elevation samples: " + stats.FallbackElevationCount.ToString(CultureInfo.InvariantCulture);

        if (skippedIncomplete > 0)
        {
            report += "\nSkipped incomplete footprints: " + skippedIncomplete.ToString(CultureInfo.InvariantCulture);
        }

        if (usedFallbackElevation)
        {
            report += "\nSome or all buildings used fallback base elevation because Cesium sampling did not finish.";
        }

        return report;
    }

    private string BuildOverpassQuery()
    {
        double metersPerDegreeLatitude = 111320d;
        double metersPerDegreeLongitude = Math.Cos(_osmCenterLatitude * Math.PI / 180d) * 111320d;
        double latDelta = _osmRadiusMeters / metersPerDegreeLatitude;
        double lonDelta = _osmRadiusMeters / Math.Max(1d, metersPerDegreeLongitude);
        double south = _osmCenterLatitude - latDelta;
        double north = _osmCenterLatitude + latDelta;
        double west = _osmCenterLongitude - lonDelta;
        double east = _osmCenterLongitude + lonDelta;
        string highwayTypes = _osmRoadFilter == OsmRoadFilter.MainRoadsOnly
            ? "motorway|trunk|primary|secondary|tertiary"
            : "motorway|trunk|primary|secondary|tertiary|unclassified|residential|service";

        return string.Format(
            CultureInfo.InvariantCulture,
            "[out:xml][timeout:25];(way[\"highway\"~\"{0}\"]({1},{2},{3},{4}););out body;>;out skel qt;",
            highwayTypes,
            south,
            west,
            north,
            east);
    }

    private OsmGenerationStats GenerateRoadsFromOsmXml(string xml, GameObject selected)
    {
        XmlDocument document = new XmlDocument();
        document.LoadXml(xml);

        Dictionary<long, OsmNode> nodes = new Dictionary<long, OsmNode>();
        foreach (XmlNode node in document.SelectNodes("//node"))
        {
            if (TryGetLongAttribute(node, "id", out long id) &&
                TryGetDoubleAttribute(node, "lat", out double latitude) &&
                TryGetDoubleAttribute(node, "lon", out double longitude))
            {
                nodes[id] = new OsmNode(latitude, longitude);
            }
        }

        Transform root = GetOrCreateGeneratedRoot().transform;
        GameObject osmRoot = new GameObject("Automatic OSM Roads");
        Undo.RegisterCreatedObjectUndo(osmRoot, "Create Automatic OSM Roads");
        osmRoot.transform.SetParent(root, false);

        Material roadMaterial = GetOrCreateMaterial("Generated_OSM_Road_Surface.mat", new Color(0.035f, 0.038f, 0.04f, 1f));
        Material lineMaterial = GetOrCreateMaterial("Generated_OSM_Lane_Markings.mat", new Color(1f, 0.92f, 0.55f, 1f));
        OsmGenerationStats stats = new OsmGenerationStats();
        List<OsmRoad> roads = new List<OsmRoad>();
        Dictionary<long, OsmIntersectionAccumulator> intersections = new Dictionary<long, OsmIntersectionAccumulator>();

        foreach (XmlNode way in document.SelectNodes("//way"))
        {
            if (roads.Count >= _osmMaxRoads)
            {
                break;
            }

            List<Vector3> points = new List<Vector3>();
            List<long> nodeReferences = new List<long>();
            foreach (XmlNode nd in way.SelectNodes("nd"))
            {
                if (TryGetLongAttribute(nd, "ref", out long reference) && nodes.TryGetValue(reference, out OsmNode osmNode))
                {
                    nodeReferences.Add(reference);
                    points.Add(ProjectToSurface(OsmToUnity(osmNode.Latitude, osmNode.Longitude), selected, 0f, Mathf.Max(0f, _heightOffset * 0.5f)));
                }
            }

            if (points.Count < 2)
            {
                continue;
            }

            int laneCount = Mathf.Max(1, GetWayLaneCount(way, _osmDefaultLaneCount, _osmAutoDetectLaneCount));
            float width = Mathf.Max(1f, laneCount * _laneWidth);
            string roadName = GetWayName(way, roads.Count) + " (" + laneCount.ToString(CultureInfo.InvariantCulture) + " lanes)";
            roads.Add(new OsmRoad(roadName, points, nodeReferences, laneCount, width));
            stats.AddRoad(laneCount);

            if (_mergeIntersections)
            {
                for (int i = 0; i < nodeReferences.Count; i++)
                {
                    long reference = nodeReferences[i];
                    if (!intersections.TryGetValue(reference, out OsmIntersectionAccumulator accumulator))
                    {
                        accumulator = new OsmIntersectionAccumulator(points[i]);
                    }

                    accumulator.AddRoad(width);
                    intersections[reference] = accumulator;
                }
            }
        }

        for (int i = 0; i < roads.Count; i++)
        {
            OsmRoad road = roads[i];
            CreateMeshObject(road.Name + " Surface", BuildPolylineSurface(road.Points, road.Width), roadMaterial, osmRoot.transform);
            CreateMeshObject(road.Name + " Markings", BuildPolylineLaneMarkings(road.Points, road.LaneCount, road.Width), lineMaterial, osmRoot.transform);
        }

        if (_mergeIntersections)
        {
            Mesh intersectionMesh = BuildIntersectionPatchMesh(intersections, selected);
            if (intersectionMesh.vertexCount > 0)
            {
                CreateMeshObject("Merged Intersection Centers", intersectionMesh, roadMaterial, osmRoot.transform);
            }
        }

        if (stats.RoadCount == 0)
        {
            Undo.DestroyObjectImmediate(osmRoot);
        }

        return stats;
    }

    private Mesh BuildPolylineSurface(List<Vector3> points, float width)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        if (_smoothRoadJoins && points.Count > 1)
        {
            float halfWidth = width * 0.5f;
            float distance = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                GetJoinOffset(points, i, halfWidth, out Vector3 rightOffset);
                vertices.Add(points[i] - rightOffset);
                vertices.Add(points[i] + rightOffset);
                uvs.Add(new Vector2(0f, distance * 0.08f));
                uvs.Add(new Vector2(1f, distance * 0.08f));

                if (i < points.Count - 1)
                {
                    distance += HorizontalDistance(points[i], points[i + 1]);
                }
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }

            return CreateMesh("Generated OSM Road Surface", vertices, triangles, uvs);
        }

        float segmentDistance = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            Vector3 direction = end - start;
            direction.y = 0f;
            float segmentLength = direction.magnitude;
            if (segmentLength < 0.001f)
            {
                continue;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
            AddQuad(vertices, uvs, triangles, start - right * width * 0.5f, start + right * width * 0.5f, end - right * width * 0.5f, end + right * width * 0.5f, segmentDistance, segmentDistance + segmentLength);
            segmentDistance += segmentLength;
        }

        return CreateMesh("Generated OSM Road Surface", vertices, triangles, uvs);
    }

    private Mesh BuildPolylineLaneMarkings(List<Vector3> points, int laneCount, float roadWidth)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int lineIndex = 0; lineIndex <= laneCount; lineIndex++)
        {
            float offset = -roadWidth * 0.5f + lineIndex * _laneWidth;
            bool isOuterLine = lineIndex == 0 || lineIndex == laneCount;
            float dashCursor = 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = points[i];
                Vector3 end = points[i + 1];
                Vector3 direction = end - start;
                direction.y = 0f;
                float segmentLength = direction.magnitude;
                if (segmentLength < 0.001f)
                {
                    continue;
                }

                Vector3 forward = direction.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                bool dashed = _dashedInnerLines && !isOuterLine;

                if (!dashed)
                {
                    AddFlatLineQuad(vertices, triangles, start + right * offset + Vector3.up * Mathf.Max(0f, _heightOffset * 0.5f), end + right * offset + Vector3.up * Mathf.Max(0f, _heightOffset * 0.5f), right);
                    continue;
                }

                float local = 0f;
                while (local < segmentLength)
                {
                    float patternLength = Mathf.Max(0.1f, _dashLength) + Mathf.Max(0.1f, _dashGap);
                    float patternPosition = dashCursor % patternLength;
                    float step = Mathf.Min(patternLength - patternPosition, segmentLength - local);

                    if (patternPosition < _dashLength)
                    {
                        float dashEnd = Mathf.Min(step, _dashLength - patternPosition);
                        AddFlatLineQuad(vertices, triangles, start + forward * local + right * offset + Vector3.up * Mathf.Max(0f, _heightOffset * 0.5f), start + forward * (local + dashEnd) + right * offset + Vector3.up * Mathf.Max(0f, _heightOffset * 0.5f), right);
                    }

                    local += step;
                    dashCursor += step;
                }
            }
        }

        return CreateMesh("Generated OSM Lane Markings", vertices, triangles);
    }

    private Mesh BuildIntersectionPatchMesh(Dictionary<long, OsmIntersectionAccumulator> intersections, GameObject selected)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        const int sides = 18;

        foreach (KeyValuePair<long, OsmIntersectionAccumulator> pair in intersections)
        {
            OsmIntersectionAccumulator intersection = pair.Value;
            if (intersection.RoadCount < 2)
            {
                continue;
            }

            float radius = Mathf.Max(_laneWidth, intersection.MaxRoadWidth * 0.55f + _intersectionPadding);
            int centerIndex = vertices.Count;
            vertices.Add(ProjectToSurface(intersection.Position, selected, intersection.Position.y, Mathf.Max(0.01f, _heightOffset * 0.25f)));

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 point = intersection.Position + offset;
                vertices.Add(ProjectToSurface(point, selected, intersection.Position.y, Mathf.Max(0.01f, _heightOffset * 0.25f)));
            }

            for (int i = 0; i < sides; i++)
            {
                triangles.Add(centerIndex);
                triangles.Add(centerIndex + 1 + i);
                triangles.Add(centerIndex + 1 + ((i + 1) % sides));
            }
        }

        return CreateMesh("Generated OSM Intersection Patches", vertices, triangles);
    }

    private static void GetJoinOffset(List<Vector3> points, int index, float halfWidth, out Vector3 rightOffset)
    {
        Vector3 previousDirection = index > 0
            ? GetHorizontalDirection(points[index - 1], points[index])
            : GetHorizontalDirection(points[index], points[Mathf.Min(index + 1, points.Count - 1)]);
        Vector3 nextDirection = index < points.Count - 1
            ? GetHorizontalDirection(points[index], points[index + 1])
            : previousDirection;

        if (previousDirection.sqrMagnitude < 0.001f)
        {
            previousDirection = nextDirection;
        }

        if (nextDirection.sqrMagnitude < 0.001f)
        {
            nextDirection = previousDirection;
        }

        Vector3 previousRight = Vector3.Cross(Vector3.up, previousDirection.normalized);
        Vector3 nextRight = Vector3.Cross(Vector3.up, nextDirection.normalized);
        Vector3 miter = previousRight + nextRight;

        if (miter.sqrMagnitude < 0.001f)
        {
            rightOffset = nextRight * halfWidth;
            return;
        }

        miter.Normalize();
        float denominator = Mathf.Abs(Vector3.Dot(miter, nextRight));
        float miterLength = denominator > 0.2f ? halfWidth / denominator : halfWidth;
        miterLength = Mathf.Min(miterLength, halfWidth * 2.5f);
        rightOffset = miter * miterLength;
    }

    private static Vector3 GetHorizontalDirection(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        return direction;
    }

    private static float HorizontalDistance(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        return direction.magnitude;
    }

    private void AddFlatLineQuad(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, Vector3 right)
    {
        float halfWidth = Mathf.Max(0.01f, _lineWidth * 0.5f);
        AddQuad(vertices, triangles, start - right * halfWidth, start + right * halfWidth, end - right * halfWidth, end + right * halfWidth);
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int baseIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float startDistance, float endDistance)
    {
        int baseIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uvs.Add(new Vector2(0f, startDistance * 0.08f));
        uvs.Add(new Vector2(1f, startDistance * 0.08f));
        uvs.Add(new Vector2(0f, endDistance * 0.08f));
        uvs.Add(new Vector2(1f, endDistance * 0.08f));
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    private static Mesh CreateMesh(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs = null)
    {
        Mesh mesh = new Mesh { name = name };
        if (vertices.Count > 65000)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        if (uvs != null && uvs.Count == vertices.Count)
        {
            mesh.SetUVs(0, uvs);
        }

        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 OsmToUnity(double latitude, double longitude)
    {
        return LatLonToUnity(latitude, longitude, _osmCenterLatitude, _osmCenterLongitude);
    }

    private static Vector3 LatLonToUnity(double latitude, double longitude, double referenceLatitude, double referenceLongitude)
    {
        double metersPerDegreeLatitude = 111320d;
        double metersPerDegreeLongitude = Math.Cos(referenceLatitude * Math.PI / 180d) * 111320d;
        float x = (float)((longitude - referenceLongitude) * metersPerDegreeLongitude);
        float z = (float)((latitude - referenceLatitude) * metersPerDegreeLatitude);
        return new Vector3(x, 0f, z);
    }

    private void UseCesiumOriginForOsmCenter()
    {
        GameObject georeference = GameObject.Find("CesiumGeoreference");
        if (georeference == null)
        {
            _report = "Could not find CesiumGeoreference.";
            return;
        }

        Component cesiumGeoreference = GetComponentByTypeName(georeference, "CesiumGeoreference");
        if (cesiumGeoreference == null)
        {
            _report = "Found CesiumGeoreference object, but could not read the CesiumGeoreference component.";
            return;
        }

        SerializedObject serializedObject = new SerializedObject(cesiumGeoreference);
        SerializedProperty latitude = serializedObject.FindProperty("_latitude");
        SerializedProperty longitude = serializedObject.FindProperty("_longitude");
        if (latitude == null || longitude == null)
        {
            _report = "Could not read latitude/longitude from CesiumGeoreference.";
            return;
        }

        _osmCenterLatitude = latitude.doubleValue;
        _osmCenterLongitude = longitude.doubleValue;
        _report = "OSM center set from CesiumGeoreference.";
    }

    private void UseAnchorOffsetForOsmCenter()
    {
        if (_laneAnchor == null)
        {
            _report = "Create or assign a Lane Anchor first.";
            return;
        }

        UseCesiumOriginForOsmCenter();
        double metersPerDegreeLatitude = 111320d;
        double metersPerDegreeLongitude = Math.Cos(_osmCenterLatitude * Math.PI / 180d) * 111320d;
        _osmCenterLatitude += _laneAnchor.position.z / metersPerDegreeLatitude;
        _osmCenterLongitude += _laneAnchor.position.x / Math.Max(1d, metersPerDegreeLongitude);
        _report = "OSM center adjusted using the Lane Anchor position. This assumes the Cesium object is aligned north/up in Unity.";
    }

    private void UseBanagoMainRoadsPreset()
    {
        _osmCenterLatitude = BanagoCenterLatitude;
        _osmCenterLongitude = BanagoCenterLongitude;
        _osmRadiusMeters = 1600f;
        _osmMaxRoads = 250;
        _osmDefaultLaneCount = 2;
        _osmAutoDetectLaneCount = true;
        _osmRoadFilter = OsmRoadFilter.MainRoadsOnly;
        _report = "Banago main roads preset applied. Click Download and Generate Roads to cover the larger Banago area.";
    }

    private void UseBanagoElevationPlanePreset()
    {
        _osmCenterLatitude = BanagoCenterLatitude;
        _osmCenterLongitude = BanagoCenterLongitude;
        _planeCenterLatitude = BanagoCenterLatitude;
        _planeCenterLongitude = BanagoCenterLongitude;
        _planeSizeMeters = 3200f;
        _planeResolution = 64;
        _planeHeightOffset = 0.02f;
        _planeUseMeshVertexFallback = true;
        _planeCreateCollider = false;
        _report = "Banago elevation plane preset applied. Click Generate Elevation Plane after Cesium terrain is loaded.";
    }

    private static Component GetComponentByTypeName(GameObject gameObject, string typeName)
    {
        Component[] components = gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }

    private static int GetWayLaneCount(XmlNode way, int fallback, bool autoDetect)
    {
        if (!autoDetect)
        {
            return fallback;
        }

        if (TryGetTagInt(way, "lanes", out int lanes))
        {
            return lanes;
        }

        bool hasForward = TryGetTagInt(way, "lanes:forward", out int forward);
        bool hasBackward = TryGetTagInt(way, "lanes:backward", out int backward);
        if (hasForward || hasBackward)
        {
            return Mathf.Max(1, forward + backward);
        }

        string highway = GetTagValue(way, "highway");
        if (highway == "motorway" || highway == "trunk" || highway == "primary")
        {
            return 4;
        }

        if (highway == "secondary" || highway == "tertiary")
        {
            return 2;
        }

        return fallback;
    }

    private static bool TryGetTagInt(XmlNode way, string key, out int value)
    {
        string text = GetTagValue(way, key);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        int separatorIndex = text.IndexOfAny(new[] { ';', '|', ',' });
        if (separatorIndex > 0 && int.TryParse(text.Substring(0, separatorIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static string GetTagValue(XmlNode way, string key)
    {
        foreach (XmlNode tag in way.SelectNodes("tag"))
        {
            if (GetStringAttribute(tag, "k") == key)
            {
                return GetStringAttribute(tag, "v");
            }
        }

        return string.Empty;
    }

    private static string GetWayName(XmlNode way, int index)
    {
        foreach (XmlNode tag in way.SelectNodes("tag"))
        {
            if (GetStringAttribute(tag, "k") == "name")
            {
                string value = GetStringAttribute(tag, "v");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return "OSM Road " + value;
                }
            }
        }

        return "OSM Road " + (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private void CreateLaneAnchor()
    {
        Vector3 position = Vector3.zero;
        GameObject selected = GetTargetObject();

        if (SceneView.lastActiveSceneView != null)
        {
            position = SceneView.lastActiveSceneView.pivot;
        }
        else if (selected != null && TryGetBounds(selected, out Bounds bounds))
        {
            position = bounds.center;
        }
        else if (selected != null)
        {
            position = selected.transform.position;
        }

        GameObject anchor = new GameObject("Lane Anchor");
        Undo.RegisterCreatedObjectUndo(anchor, "Create Lane Anchor");
        anchor.transform.position = position;
        anchor.transform.rotation = Quaternion.Euler(0f, _orientationDegrees, 0f);
        _laneAnchor = anchor.transform;
        Selection.activeGameObject = anchor;
        _report = "Lane Anchor created. Move it to the road center, rotate it to match the road, then create the lane preview.";
    }

    private Vector3 GetLaneCenter(GameObject selected)
    {
        if (_laneAnchor != null)
        {
            return _laneAnchor.position;
        }

        return TryGetBounds(selected, out Bounds bounds) ? bounds.center : selected.transform.position;
    }

    private Quaternion GetLaneRotation()
    {
        if (_useAnchorRotation && _laneAnchor != null)
        {
            return Quaternion.Euler(0f, _laneAnchor.eulerAngles.y, 0f);
        }

        return Quaternion.Euler(0f, _orientationDegrees, 0f);
    }

    private GameObject GetTargetObject()
    {
        if (_targetObject != null)
        {
            return _targetObject;
        }

        return Selection.activeGameObject;
    }

    private static bool TryGetLongAttribute(XmlNode node, string name, out long value)
    {
        return long.TryParse(GetStringAttribute(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDoubleAttribute(XmlNode node, string name, out double value)
    {
        return double.TryParse(GetStringAttribute(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string GetStringAttribute(XmlNode node, string name)
    {
        return node?.Attributes?[name]?.Value ?? string.Empty;
    }

    private static void CalculateBuildingCentroid(IList<StormSimGeoPoint> points, out double centroidLatitude, out double centroidLongitude)
    {
        double signedArea = 0d;
        double centroidX = 0d;
        double centroidY = 0d;

        for (int i = 0; i < points.Count; i++)
        {
            StormSimGeoPoint current = points[i];
            StormSimGeoPoint next = points[(i + 1) % points.Count];
            double cross = current.Longitude * next.Latitude - next.Longitude * current.Latitude;
            signedArea += cross;
            centroidX += (current.Longitude + next.Longitude) * cross;
            centroidY += (current.Latitude + next.Latitude) * cross;
        }

        signedArea *= 0.5d;
        if (Math.Abs(signedArea) < 1e-12d)
        {
            AverageBuildingPoints(points, out centroidLatitude, out centroidLongitude);
            return;
        }

        centroidLongitude = centroidX / (6d * signedArea);
        centroidLatitude = centroidY / (6d * signedArea);
    }

    private static void AverageBuildingPoints(IList<StormSimGeoPoint> points, out double latitude, out double longitude)
    {
        latitude = 0d;
        longitude = 0d;
        if (points.Count == 0)
        {
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            latitude += points[i].Latitude;
            longitude += points[i].Longitude;
        }

        latitude /= points.Count;
        longitude /= points.Count;
    }

    private static void RemoveDuplicateClosingPoint(List<StormSimGeoPoint> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        StormSimGeoPoint first = points[0];
        StormSimGeoPoint last = points[points.Count - 1];
        bool sameLatitude = Math.Abs(first.Latitude - last.Latitude) < 1e-10d;
        bool sameLongitude = Math.Abs(first.Longitude - last.Longitude) < 1e-10d;
        if (sameLatitude && sameLongitude)
        {
            points.RemoveAt(points.Count - 1);
        }
    }

    private static string GetOsmTagValue(IDictionary<string, string> tags, string key)
    {
        return tags.TryGetValue(key, out string value) ? value : string.Empty;
    }

    private static string ReadJsonString(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        return value != null ? value.ToString() : string.Empty;
    }

    private static long ReadJsonLong(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        return value != null && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : 0L;
    }

    private static double ReadJsonDouble(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        return value != null && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : double.NaN;
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text) &&
            !string.IsNullOrEmpty(value) &&
            text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "building";
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString();
    }

    private readonly struct OsmBuildingFootprint
    {
        public OsmBuildingFootprint(
            long osmId,
            string buildingTag,
            Dictionary<string, string> tags,
            List<StormSimGeoPoint> footprint,
            double centroidLatitude,
            double centroidLongitude)
        {
            OsmId = osmId;
            BuildingTag = buildingTag;
            Tags = tags;
            Footprint = footprint;
            CentroidLatitude = centroidLatitude;
            CentroidLongitude = centroidLongitude;
        }

        public long OsmId { get; }
        public string BuildingTag { get; }
        public Dictionary<string, string> Tags { get; }
        public List<StormSimGeoPoint> Footprint { get; }
        public double CentroidLatitude { get; }
        public double CentroidLongitude { get; }
    }

    private struct BuildingGenerationStats
    {
        public int BuildingCount;
        public int ResidentialCount;
        public int CivicCount;
        public int GovernmentCount;
        public int FallbackElevationCount;

        public void AddBuilding(BuildingMaterialCategory category)
        {
            BuildingCount++;
            if (category == BuildingMaterialCategory.Civic)
            {
                CivicCount++;
            }
            else if (category == BuildingMaterialCategory.Government)
            {
                GovernmentCount++;
            }
            else
            {
                ResidentialCount++;
            }
        }
    }

    private enum BuildingMaterialCategory
    {
        Residential,
        Civic,
        Government
    }

    private struct ElevationPlaneStats
    {
        public int TotalVertices;
        public int RaycastHits;
        public int MeshFallbackHits;
        public int FlatFallbacks;
        public float MinHeight;
        public float MaxHeight;

        public void AddRaycastHeight(float height)
        {
            RaycastHits++;
            AddHeight(height);
        }

        public void AddMeshFallbackHeight(float height)
        {
            MeshFallbackHits++;
            AddHeight(height);
        }

        public void AddFlatFallbackHeight(float height)
        {
            FlatFallbacks++;
            AddHeight(height);
        }

        private void AddHeight(float height)
        {
            if (TotalVertices == 0)
            {
                MinHeight = height;
                MaxHeight = height;
            }
            else
            {
                MinHeight = Mathf.Min(MinHeight, height);
                MaxHeight = Mathf.Max(MaxHeight, height);
            }

            TotalVertices++;
        }
    }

    private readonly struct OsmRoad
    {
        public OsmRoad(string name, List<Vector3> points, List<long> nodeReferences, int laneCount, float width)
        {
            Name = name;
            Points = points;
            NodeReferences = nodeReferences;
            LaneCount = laneCount;
            Width = width;
        }

        public string Name { get; }
        public List<Vector3> Points { get; }
        public List<long> NodeReferences { get; }
        public int LaneCount { get; }
        public float Width { get; }
    }

    private readonly struct OsmNode
    {
        public OsmNode(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }
        public double Longitude { get; }
    }

    private struct OsmIntersectionAccumulator
    {
        public OsmIntersectionAccumulator(Vector3 position)
        {
            Position = position;
            RoadCount = 0;
            MaxRoadWidth = 0f;
        }

        public Vector3 Position { get; }
        public int RoadCount { get; private set; }
        public float MaxRoadWidth { get; private set; }

        public void AddRoad(float width)
        {
            RoadCount++;
            MaxRoadWidth = Mathf.Max(MaxRoadWidth, width);
        }
    }

    private struct OsmGenerationStats
    {
        public int RoadCount;
        public int TwoLaneRoads;
        public int FourLaneRoads;
        public int OtherLaneRoads;

        public void AddRoad(int laneCount)
        {
            RoadCount++;
            if (laneCount == 2)
            {
                TwoLaneRoads++;
            }
            else if (laneCount == 4)
            {
                FourLaneRoads++;
            }
            else
            {
                OtherLaneRoads++;
            }
        }
    }

    private enum OsmRoadFilter
    {
        MainRoadsOnly,
        AllDrivableRoads
    }
}
