using CesiumForUnity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

public sealed class StormSimOsmBuildingSpawner : MonoBehaviour
{
    private const string DefaultOverpassUrl = "https://overpass-api.de/api/interpreter";
    private const double DefaultSouthLatitude = 10.6950d;
    private const double DefaultWestLongitude = 122.9400d;
    private const double DefaultNorthLatitude = 10.7120d;
    private const double DefaultEastLongitude = 122.9600d;

    [Header("Cesium")]
    [SerializeField] private CesiumGeoreference georeference;
    [SerializeField] private Cesium3DTileset terrainTileset;

    [Header("Overpass")]
    [SerializeField] private string overpassUrl = DefaultOverpassUrl;
    [SerializeField] private double southLatitude = DefaultSouthLatitude;
    [SerializeField] private double westLongitude = DefaultWestLongitude;
    [SerializeField] private double northLatitude = DefaultNorthLatitude;
    [SerializeField] private double eastLongitude = DefaultEastLongitude;
    [SerializeField] private int requestTimeoutSeconds = 30;
    [SerializeField] private int fewResultWarningThreshold = 10;

    [Header("Generation")]
    [SerializeField] private bool loadOnStart;
    [SerializeField] private bool clearExistingBeforeSpawn = true;
    [SerializeField] private Transform buildingParent;
    [SerializeField] private string generatedRootName = "StormSim OSM Buildings";
    [SerializeField] private float minimumFootprintDimensionMeters = 2f;
    [SerializeField] private float maximumFootprintDimensionMeters = 160f;
    [SerializeField] private float defaultStoryHeightMeters = 3f;
    [SerializeField] private int defaultMinimumStories = 1;
    [SerializeField] private int defaultMaximumStories = 3;
    [SerializeField] private double fallbackBaseElevationMeters = 0d;
    [SerializeField] private bool addBoxColliders = true;

    [Header("Materials")]
    [SerializeField] private Material residentialMaterial;
    [SerializeField] private Material civicMaterial;
    [SerializeField] private Material governmentMaterial;

    [Header("Runtime Lookup")]
    [SerializeField] private List<StormSimBuildingMetadata> spawnedBuildings = new List<StormSimBuildingMetadata>();

    private readonly Dictionary<long, StormSimBuildingMetadata> buildingsByOsmId =
        new Dictionary<long, StormSimBuildingMetadata>();

    private Coroutine activeGeneration;
    private Material generatedResidentialMaterial;
    private Material generatedCivicMaterial;
    private Material generatedGovernmentMaterial;

    public IReadOnlyDictionary<long, StormSimBuildingMetadata> BuildingsByOsmId
    {
        get { return buildingsByOsmId; }
    }

    public IReadOnlyList<StormSimBuildingMetadata> SpawnedBuildings
    {
        get { return spawnedBuildings; }
    }

    private void Start()
    {
        if (loadOnStart)
        {
            DownloadAndSpawnBuildings();
        }
    }

    [ContextMenu("Download And Spawn Buildings")]
    public void DownloadAndSpawnBuildings()
    {
        if (activeGeneration != null)
        {
            StopCoroutine(activeGeneration);
        }

        activeGeneration = StartCoroutine(DownloadAndSpawnBuildingsRoutine());
    }

    [ContextMenu("Clear Spawned Buildings")]
    public void ClearSpawnedBuildings()
    {
        ResolveSceneReferences();

        Transform root = GetOrCreateBuildingParent(false);
        if (root != null)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyUnityObject(root.GetChild(i).gameObject);
            }
        }

        spawnedBuildings.Clear();
        buildingsByOsmId.Clear();
    }

    public bool TryGetBuilding(long osmId, out StormSimBuildingMetadata metadata)
    {
        return buildingsByOsmId.TryGetValue(osmId, out metadata);
    }

    private IEnumerator DownloadAndSpawnBuildingsRoutine()
    {
        ResolveSceneReferences();

        if (georeference == null)
        {
            Debug.LogError("StormSim OSM building generation failed: no CesiumGeoreference was assigned or found.");
            activeGeneration = null;
            yield break;
        }

        if (terrainTileset == null)
        {
            Debug.LogError("StormSim OSM building generation failed: no Cesium3DTileset terrain source was assigned or found.");
            activeGeneration = null;
            yield break;
        }

        if (clearExistingBeforeSpawn)
        {
            ClearSpawnedBuildings();
        }

        string query = BuildOverpassQuery();
        using (UnityWebRequest request = CreateOverpassRequest(query))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("StormSim OSM building generation failed: Overpass request error. " + request.error);
                activeGeneration = null;
                yield break;
            }

            List<OsmBuildingFootprint> buildings = ParseOverpassBuildings(request.downloadHandler.text);
            WarnIfCoverageLooksSparse(buildings.Count);

            if (buildings.Count == 0)
            {
                activeGeneration = null;
                yield break;
            }

            yield return SampleElevationsAndSpawn(buildings);
        }

        Debug.Log("StormSim OSM building generation complete. Spawned " + spawnedBuildings.Count + " placeholder buildings.");
        activeGeneration = null;
    }

    private IEnumerator SampleElevationsAndSpawn(List<OsmBuildingFootprint> buildings)
    {
        double3[] samplePositions = new double3[buildings.Count];
        for (int i = 0; i < buildings.Count; i++)
        {
            samplePositions[i] = new double3(
                buildings[i].CentroidLongitude,
                buildings[i].CentroidLatitude,
                fallbackBaseElevationMeters);
        }

        Task<CesiumSampleHeightResult> heightTask;
        try
        {
            heightTask = terrainTileset.SampleHeightMostDetailed(samplePositions);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("StormSim could not start Cesium terrain height sampling. Using fallback elevation. " + exception.Message);
            SpawnBuildings(buildings, null);
            yield break;
        }

        yield return new WaitForTask(heightTask);

        if (heightTask.IsFaulted || heightTask.IsCanceled)
        {
            string message = heightTask.Exception != null ? heightTask.Exception.GetBaseException().Message : "Task was canceled.";
            Debug.LogWarning("StormSim Cesium terrain height sampling did not complete cleanly. Using fallback elevation. " + message);
            SpawnBuildings(buildings, null);
            yield break;
        }

        CesiumSampleHeightResult result = heightTask.Result;
        if (result != null && result.warnings != null)
        {
            for (int i = 0; i < result.warnings.Length; i++)
            {
                if (!string.IsNullOrEmpty(result.warnings[i]))
                {
                    Debug.LogWarning("StormSim Cesium height warning: " + result.warnings[i]);
                }
            }
        }

        SpawnBuildings(buildings, result);
    }

    private void SpawnBuildings(List<OsmBuildingFootprint> buildings, CesiumSampleHeightResult heightResult)
    {
        Transform root = GetOrCreateBuildingParent(true);
        int failedHeightSamples = 0;

        for (int i = 0; i < buildings.Count; i++)
        {
            OsmBuildingFootprint building = buildings[i];
            double baseElevation = fallbackBaseElevationMeters;

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
                    failedHeightSamples++;
                }
            }

            float height = ResolveBuildingHeight(building);
            Vector2 footprintSize = CalculateFootprintSizeMeters(building);
            List<StormSimGeoPoint> elevatedFootprint = CreateElevatedFootprint(building, baseElevation);
            GameObject buildingObject = CreateBuildingObject(root, building, baseElevation, height, footprintSize);

            StormSimBuildingMetadata metadata = buildingObject.AddComponent<StormSimBuildingMetadata>();
            metadata.Initialize(
                building.OsmId,
                building.BuildingTag,
                building.CentroidLatitude,
                building.CentroidLongitude,
                baseElevation,
                height,
                footprintSize,
                elevatedFootprint,
                building.Tags);

            buildingsByOsmId[building.OsmId] = metadata;
            spawnedBuildings.Add(metadata);
        }

        if (failedHeightSamples > 0)
        {
            Debug.LogWarning("StormSim used fallback elevation for " + failedHeightSamples + " building centroid(s).");
        }
    }

    private GameObject CreateBuildingObject(
        Transform root,
        OsmBuildingFootprint building,
        double baseElevation,
        float height,
        Vector2 footprintSize)
    {
        GameObject anchorObject = new GameObject("OSM Building " + building.OsmId + " " + SanitizeName(building.BuildingTag));
        anchorObject.transform.SetParent(root, false);

        CesiumGlobeAnchor anchor = anchorObject.AddComponent<CesiumGlobeAnchor>();
        anchor.adjustOrientationForGlobeWhenMoving = true;
        anchor.detectTransformChanges = false;
        anchor.longitudeLatitudeHeight = new double3(
            building.CentroidLongitude,
            building.CentroidLatitude,
            baseElevation);

        GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volume.name = "Placeholder Volume";
        volume.transform.SetParent(anchorObject.transform, false);
        volume.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        volume.transform.localRotation = Quaternion.identity;
        volume.transform.localScale = new Vector3(footprintSize.x, height, footprintSize.y);

        Renderer renderer = volume.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = ResolveMaterial(building);
        }

        if (!addBoxColliders)
        {
            Collider collider = volume.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyUnityObject(collider);
            }
        }

        return anchorObject;
    }

    private void ResolveSceneReferences()
    {
        if (georeference == null)
        {
            georeference = FindAnyObjectByType<CesiumGeoreference>();
        }

        if (terrainTileset != null)
        {
            return;
        }

        Cesium3DTileset[] candidates = georeference != null
            ? georeference.GetComponentsInChildren<Cesium3DTileset>(true)
            : FindObjectsByType<Cesium3DTileset>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && ContainsIgnoreCase(candidates[i].name, "terrain"))
            {
                terrainTileset = candidates[i];
                return;
            }
        }

        if (candidates.Length > 0)
        {
            terrainTileset = candidates[0];
        }
    }

    private Transform GetOrCreateBuildingParent(bool createIfMissing)
    {
        if (buildingParent != null)
        {
            return buildingParent;
        }

        if (georeference == null)
        {
            return null;
        }

        Transform existing = georeference.transform.Find(generatedRootName);
        if (existing != null || !createIfMissing)
        {
            buildingParent = existing;
            return buildingParent;
        }

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(georeference.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        buildingParent = root.transform;
        return buildingParent;
    }

    private string BuildOverpassQuery()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[out:json][timeout:{0}];(way[\"building\"]({1},{2},{3},{4}););out tags geom;",
            requestTimeoutSeconds,
            southLatitude,
            westLongitude,
            northLatitude,
            eastLongitude);
    }

    private UnityWebRequest CreateOverpassRequest(string query)
    {
        string body = "data=" + UnityWebRequest.EscapeURL(query);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        UnityWebRequest request = new UnityWebRequest(overpassUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = requestTimeoutSeconds + 10;
        request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        request.SetRequestHeader("Accept", "application/json");
        return request;
    }

    private List<OsmBuildingFootprint> ParseOverpassBuildings(string json)
    {
        List<OsmBuildingFootprint> buildings = new List<OsmBuildingFootprint>();
        int skippedIncomplete = 0;

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (Exception exception)
        {
            Debug.LogError("StormSim could not parse Overpass JSON. " + exception.Message);
            return buildings;
        }

        JArray elements = root["elements"] as JArray;
        if (elements == null)
        {
            Debug.LogWarning("StormSim Overpass response had no elements array.");
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

            string type = ReadString(element, "type");
            if (!string.Equals(type, "way", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            long osmId = ReadLong(element, "id");
            Dictionary<string, string> tags = ReadTags(element["tags"] as JObject);
            if (!tags.ContainsKey("building"))
            {
                continue;
            }

            List<StormSimGeoPoint> footprint = ReadGeometry(element["geometry"] as JArray);
            if (footprint.Count < 3)
            {
                skippedIncomplete++;
                continue;
            }

            RemoveDuplicateClosingPoint(footprint);
            if (footprint.Count < 3)
            {
                skippedIncomplete++;
                continue;
            }

            double centroidLatitude;
            double centroidLongitude;
            CalculateCentroid(footprint, out centroidLatitude, out centroidLongitude);

            buildings.Add(new OsmBuildingFootprint(
                osmId,
                tags["building"],
                tags,
                footprint,
                centroidLatitude,
                centroidLongitude));
        }

        if (skippedIncomplete > 0)
        {
            Debug.LogWarning("StormSim skipped " + skippedIncomplete + " incomplete OSM building footprint(s).");
        }

        return buildings;
    }

    private static Dictionary<string, string> ReadTags(JObject tagsObject)
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

    private static List<StormSimGeoPoint> ReadGeometry(JArray geometryArray)
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

            double latitude = ReadDouble(point, "lat");
            double longitude = ReadDouble(point, "lon");
            if (double.IsNaN(latitude) || double.IsNaN(longitude))
            {
                continue;
            }

            points.Add(new StormSimGeoPoint(latitude, longitude, 0d));
        }

        return points;
    }

    private List<StormSimGeoPoint> CreateElevatedFootprint(OsmBuildingFootprint building, double baseElevation)
    {
        List<StormSimGeoPoint> elevatedFootprint = new List<StormSimGeoPoint>(building.Footprint.Count);
        for (int i = 0; i < building.Footprint.Count; i++)
        {
            elevatedFootprint.Add(new StormSimGeoPoint(
                building.Footprint[i].Latitude,
                building.Footprint[i].Longitude,
                baseElevation));
        }

        return elevatedFootprint;
    }

    private Vector2 CalculateFootprintSizeMeters(OsmBuildingFootprint building)
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

        width = Mathf.Clamp(width, minimumFootprintDimensionMeters, maximumFootprintDimensionMeters);
        depth = Mathf.Clamp(depth, minimumFootprintDimensionMeters, maximumFootprintDimensionMeters);
        return new Vector2(width, depth);
    }

    private float ResolveBuildingHeight(OsmBuildingFootprint building)
    {
        float height;
        if (TryReadHeightMeters(building.Tags, out height))
        {
            return Mathf.Max(defaultStoryHeightMeters, height);
        }

        float levels;
        if (TryReadBuildingLevels(building.Tags, out levels))
        {
            return Mathf.Max(defaultStoryHeightMeters, levels * defaultStoryHeightMeters);
        }

        return ResolveDeterministicDefaultHeight(building.OsmId);
    }

    private float ResolveDeterministicDefaultHeight(long osmId)
    {
        int minStories = Mathf.Max(1, defaultMinimumStories);
        int maxStories = Mathf.Max(minStories, defaultMaximumStories);

        unchecked
        {
            int seed = (int)(osmId ^ (osmId >> 32));
            System.Random random = new System.Random(seed);
            int stories = random.Next(minStories, maxStories + 1);
            return stories * defaultStoryHeightMeters;
        }
    }

    private static bool TryReadHeightMeters(IDictionary<string, string> tags, out float heightMeters)
    {
        string value;
        if (!tags.TryGetValue("height", out value) || string.IsNullOrWhiteSpace(value))
        {
            heightMeters = 0f;
            return false;
        }

        bool isFeet = ContainsIgnoreCase(value, "ft") ||
            ContainsIgnoreCase(value, "feet") ||
            value.IndexOf("'", StringComparison.Ordinal) >= 0;

        float parsedValue;
        if (!TryReadFirstNumber(value, out parsedValue) || parsedValue <= 0f)
        {
            heightMeters = 0f;
            return false;
        }

        heightMeters = isFeet ? parsedValue * 0.3048f : parsedValue;
        return true;
    }

    private static bool TryReadBuildingLevels(IDictionary<string, string> tags, out float levels)
    {
        string value;
        if ((!tags.TryGetValue("building:levels", out value) && !tags.TryGetValue("levels", out value)) ||
            string.IsNullOrWhiteSpace(value))
        {
            levels = 0f;
            return false;
        }

        if (!TryReadFirstNumber(value, out levels))
        {
            levels = 0f;
            return false;
        }

        levels = Mathf.Clamp(levels, 1f, 80f);
        return true;
    }

    private static bool TryReadFirstNumber(string text, out float value)
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

    private Material ResolveMaterial(OsmBuildingFootprint building)
    {
        if (IsCivicBuilding(building))
        {
            return civicMaterial != null ? civicMaterial : GetGeneratedCivicMaterial();
        }

        if (IsGovernmentBuilding(building))
        {
            return governmentMaterial != null ? governmentMaterial : GetGeneratedGovernmentMaterial();
        }

        return residentialMaterial != null ? residentialMaterial : GetGeneratedResidentialMaterial();
    }

    private static bool IsCivicBuilding(OsmBuildingFootprint building)
    {
        string buildingTag = building.BuildingTag;
        string amenity = GetTagOrEmpty(building.Tags, "amenity");
        string emergency = GetTagOrEmpty(building.Tags, "emergency");

        return ContainsIgnoreCase(buildingTag, "school") ||
            ContainsIgnoreCase(buildingTag, "evacuation") ||
            ContainsIgnoreCase(amenity, "school") ||
            ContainsIgnoreCase(amenity, "college") ||
            ContainsIgnoreCase(amenity, "kindergarten") ||
            ContainsIgnoreCase(amenity, "shelter") ||
            ContainsIgnoreCase(emergency, "assembly_point");
    }

    private static bool IsGovernmentBuilding(OsmBuildingFootprint building)
    {
        string buildingTag = building.BuildingTag;
        string amenity = GetTagOrEmpty(building.Tags, "amenity");
        string office = GetTagOrEmpty(building.Tags, "office");
        string operatorType = GetTagOrEmpty(building.Tags, "operator:type");

        return ContainsIgnoreCase(buildingTag, "government") ||
            ContainsIgnoreCase(buildingTag, "public") ||
            ContainsIgnoreCase(buildingTag, "civic") ||
            ContainsIgnoreCase(buildingTag, "municipal") ||
            ContainsIgnoreCase(amenity, "townhall") ||
            ContainsIgnoreCase(amenity, "police") ||
            ContainsIgnoreCase(amenity, "fire_station") ||
            ContainsIgnoreCase(office, "government") ||
            ContainsIgnoreCase(operatorType, "government");
    }

    private Material GetGeneratedResidentialMaterial()
    {
        if (generatedResidentialMaterial == null)
        {
            generatedResidentialMaterial = CreateRuntimeMaterial("StormSim Generated Residential", new Color(0.55f, 0.57f, 0.58f, 1f));
        }

        return generatedResidentialMaterial;
    }

    private Material GetGeneratedCivicMaterial()
    {
        if (generatedCivicMaterial == null)
        {
            generatedCivicMaterial = CreateRuntimeMaterial("StormSim Generated Civic", new Color(0.16f, 0.38f, 0.76f, 1f));
        }

        return generatedCivicMaterial;
    }

    private Material GetGeneratedGovernmentMaterial()
    {
        if (generatedGovernmentMaterial == null)
        {
            generatedGovernmentMaterial = CreateRuntimeMaterial("StormSim Generated Government", new Color(0.92f, 0.46f, 0.16f, 1f));
        }

        return generatedGovernmentMaterial;
    }

    private static Material CreateRuntimeMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = name;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void WarnIfCoverageLooksSparse(int buildingCount)
    {
        if (buildingCount == 0)
        {
            Debug.LogWarning(
                "StormSim Overpass query returned zero building footprints for Banago. " +
                "You will likely need manually placed supplemental buildings, especially around Purok Baybayon, Purok Sibucao, and Purok Riverside.");
            return;
        }

        if (buildingCount <= fewResultWarningThreshold)
        {
            Debug.LogWarning(
                "StormSim Overpass query returned only " + buildingCount + " building footprint(s) for Banago. " +
                "Check coverage and consider manual supplemental buildings around Purok Baybayon, Purok Sibucao, and Purok Riverside.");
        }
    }

    private static void CalculateCentroid(
        IList<StormSimGeoPoint> points,
        out double centroidLatitude,
        out double centroidLongitude)
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
            AveragePoints(points, out centroidLatitude, out centroidLongitude);
            return;
        }

        centroidLongitude = centroidX / (6d * signedArea);
        centroidLatitude = centroidY / (6d * signedArea);
    }

    private static void AveragePoints(
        IList<StormSimGeoPoint> points,
        out double latitude,
        out double longitude)
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

    private static string GetTagOrEmpty(IDictionary<string, string> tags, string key)
    {
        string value;
        return tags.TryGetValue(key, out value) ? value : string.Empty;
    }

    private static string ReadString(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        return value != null ? value.ToString() : string.Empty;
    }

    private static long ReadLong(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        long parsed;
        return value != null && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
            ? parsed
            : 0L;
    }

    private static double ReadDouble(JObject obj, string propertyName)
    {
        JToken value = obj[propertyName];
        double parsed;
        return value != null && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
            ? parsed
            : double.NaN;
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text) &&
            !string.IsNullOrEmpty(value) &&
            text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
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

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private sealed class OsmBuildingFootprint
    {
        public readonly long OsmId;
        public readonly string BuildingTag;
        public readonly Dictionary<string, string> Tags;
        public readonly List<StormSimGeoPoint> Footprint;
        public readonly double CentroidLatitude;
        public readonly double CentroidLongitude;

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
    }
}
