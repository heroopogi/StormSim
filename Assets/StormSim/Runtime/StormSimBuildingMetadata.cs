using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct StormSimGeoPoint
{
    [SerializeField] private double latitude;
    [SerializeField] private double longitude;
    [SerializeField] private double heightMeters;

    public StormSimGeoPoint(double latitude, double longitude, double heightMeters)
    {
        this.latitude = latitude;
        this.longitude = longitude;
        this.heightMeters = heightMeters;
    }

    public double Latitude
    {
        get { return latitude; }
    }

    public double Longitude
    {
        get { return longitude; }
    }

    public double HeightMeters
    {
        get { return heightMeters; }
    }
}

[Serializable]
public struct StormSimOsmTag
{
    [SerializeField] private string key;
    [SerializeField] private string value;

    public StormSimOsmTag(string key, string value)
    {
        this.key = key;
        this.value = value;
    }

    public string Key
    {
        get { return key; }
    }

    public string Value
    {
        get { return value; }
    }
}

public sealed class StormSimBuildingMetadata : MonoBehaviour
{
    private static readonly Dictionary<long, StormSimBuildingMetadata> activeBuildingsByOsmId =
        new Dictionary<long, StormSimBuildingMetadata>();

    [SerializeField] private long osmId;
    [SerializeField] private string buildingTag;
    [SerializeField] private double centroidLatitude;
    [SerializeField] private double centroidLongitude;
    [SerializeField] private double baseElevationMeters;
    [SerializeField] private float heightMeters;
    [SerializeField] private Vector2 footprintSizeMeters;
    [SerializeField] private List<StormSimGeoPoint> footprint = new List<StormSimGeoPoint>();
    [SerializeField] private List<StormSimOsmTag> osmTags = new List<StormSimOsmTag>();
    private long registeredOsmId;
    private bool isRegistered;

    public static IReadOnlyDictionary<long, StormSimBuildingMetadata> ActiveBuildingsByOsmId
    {
        get { return activeBuildingsByOsmId; }
    }

    public static bool TryGetActiveBuilding(long osmId, out StormSimBuildingMetadata metadata)
    {
        return activeBuildingsByOsmId.TryGetValue(osmId, out metadata);
    }

    public long OsmId
    {
        get { return osmId; }
    }

    public string BuildingTag
    {
        get { return buildingTag; }
    }

    public double CentroidLatitude
    {
        get { return centroidLatitude; }
    }

    public double CentroidLongitude
    {
        get { return centroidLongitude; }
    }

    public double BaseElevationMeters
    {
        get { return baseElevationMeters; }
    }

    public float HeightMeters
    {
        get { return heightMeters; }
    }

    public double RoofElevationMeters
    {
        get { return baseElevationMeters + heightMeters; }
    }

    public double FloodBlockingElevationMeters
    {
        get { return RoofElevationMeters; }
    }

    public Vector2 FootprintSizeMeters
    {
        get { return footprintSizeMeters; }
    }

    public IReadOnlyList<StormSimGeoPoint> Footprint
    {
        get { return footprint; }
    }

    public IReadOnlyList<StormSimOsmTag> OsmTags
    {
        get { return osmTags; }
    }

    public void Initialize(
        long osmId,
        string buildingTag,
        double centroidLatitude,
        double centroidLongitude,
        double baseElevationMeters,
        float heightMeters,
        Vector2 footprintSizeMeters,
        IList<StormSimGeoPoint> footprint,
        IDictionary<string, string> tags)
    {
        UnregisterActiveBuilding();

        this.osmId = osmId;
        this.buildingTag = buildingTag;
        this.centroidLatitude = centroidLatitude;
        this.centroidLongitude = centroidLongitude;
        this.baseElevationMeters = baseElevationMeters;
        this.heightMeters = heightMeters;
        this.footprintSizeMeters = footprintSizeMeters;

        this.footprint.Clear();
        if (footprint != null)
        {
            this.footprint.AddRange(footprint);
        }

        osmTags.Clear();
        if (tags != null)
        {
            foreach (KeyValuePair<string, string> pair in tags)
            {
                osmTags.Add(new StormSimOsmTag(pair.Key, pair.Value));
            }
        }

        RegisterActiveBuilding();
    }

    public bool TryGetTag(string key, out string value)
    {
        for (int i = 0; i < osmTags.Count; i++)
        {
            if (string.Equals(osmTags[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = osmTags[i].Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public bool ContainsLongitudeLatitude(double longitude, double latitude)
    {
        if (footprint.Count < 3)
        {
            return false;
        }

        bool inside = false;
        int previous = footprint.Count - 1;
        for (int current = 0; current < footprint.Count; current++)
        {
            double currentLon = footprint[current].Longitude;
            double currentLat = footprint[current].Latitude;
            double previousLon = footprint[previous].Longitude;
            double previousLat = footprint[previous].Latitude;

            bool crossesLatitude = (currentLat > latitude) != (previousLat > latitude);
            if (crossesLatitude)
            {
                double edgeLongitude =
                    (previousLon - currentLon) * (latitude - currentLat) / (previousLat - currentLat) + currentLon;
                if (longitude < edgeLongitude)
                {
                    inside = !inside;
                }
            }

            previous = current;
        }

        return inside;
    }

    private void OnEnable()
    {
        RegisterActiveBuilding();
    }

    private void OnDisable()
    {
        UnregisterActiveBuilding();
    }

    private void RegisterActiveBuilding()
    {
        if (isRegistered || osmId == 0)
        {
            return;
        }

        activeBuildingsByOsmId[osmId] = this;
        registeredOsmId = osmId;
        isRegistered = true;
    }

    private void UnregisterActiveBuilding()
    {
        if (!isRegistered)
        {
            return;
        }

        if (activeBuildingsByOsmId.TryGetValue(registeredOsmId, out StormSimBuildingMetadata current) && current == this)
        {
            activeBuildingsByOsmId.Remove(registeredOsmId);
        }

        registeredOsmId = 0;
        isRegistered = false;
    }
}
