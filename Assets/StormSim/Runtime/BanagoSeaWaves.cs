using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Coastline-preserving waves. Physics samples the same displaced triangles as the GPU.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BanagoSeaWaves : MonoBehaviour
{
    [Header("Sea waves")]
    [Tooltip("Maximum combined crest-to-trough height in metres, away from shore.")]
    [Range(0, 2)] public float waveHeight = .5f;
    [Tooltip("Length of the main swell in metres. Two smaller swells add variation.")]
    [Range(8, 60)] public float wavelength = 24;
    [Tooltip("Wave travel speed multiplier. Zero freezes the waves.")]
    [Range(0, 3)] public float waveSpeed = 1;
    [Tooltip("Travel direction in degrees in sea-local space: 0 = +X, 90 = +Z.")]
    [Range(0, 360)] public float waveDirection = 25;
    [Tooltip("Distance from the mapped shoreline over which waves fade in, in metres.")]
    [Min(.5f)] public float shorelineFade = 10;
    public bool animateInEditor = true;

    [Header("Water current")]
    [Tooltip("Steady current speed in metres per second. Drives floating objects through water drag.")]
    [Range(0, 1)] public float currentSpeed = .08f;
    [Range(0, 360)] public float currentDirection = 110;
    [Tooltip("Use baked river tangents (mesh UV3) for current and UV2 for downstream ripple motion.")]
    public bool followRiverFlow;

    public struct SurfaceSample
    {
        public float height, baseHeight, shoreDistance;
        public Vector3 normal, velocity;
    }

    const float CellSize = 12;
    readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
    readonly Vector4[] waves = new Vector4[3];
    readonly float[] frequencies = new float[3];
    static readonly float[] Phases = { 0, 1.7f, 4.1f };
    Vector3[] vertices;
    Vector2[] shore;
    Vector2[] flowDirections;
    int[] triangles;
    Mesh cachedMesh;
    MeshRenderer waterRenderer;
    MaterialPropertyBlock properties;
    [System.NonSerialized] Vector4 cachedSettings = new Vector4(float.NaN, 0, 0, 0);
    double waveClock, lastClock;
    double currentClock;
    bool clockStarted;

    public float WaveTime => (float)waveClock;
    public int IndexedTriangleCount => triangles == null ? 0 : triangles.Length / 3;

    void OnEnable()
    {
        waterRenderer = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        // Unity can restore private value fields during a domain reload, but not readonly arrays.
        // Always repopulate the wave arrays when this instance is enabled or cloned.
        cachedSettings = new Vector4(float.NaN, 0, 0, 0);
        waveClock = 0;
        currentClock = 0;
        clockStarted = false;
        RebuildSurfaceIndex();
        UpdateMaterial(0);
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        if (waterRenderer)
        {
            waterRenderer.GetPropertyBlock(properties);
            properties.SetVector("_SeaWave0", Vector4.zero);
            properties.SetVector("_SeaWave1", Vector4.zero);
            properties.SetVector("_SeaWave2", Vector4.zero);
            waterRenderer.SetPropertyBlock(properties);
            waterRenderer.ResetLocalBounds();
        }
    }

    void OnValidate()
    {
        waveHeight = Mathf.Clamp(waveHeight, 0, 2);
        wavelength = Mathf.Clamp(wavelength, 8, 60);
        waveSpeed = Mathf.Clamp(waveSpeed, 0, 3);
        shorelineFade = Mathf.Max(.5f, shorelineFade);
        currentSpeed = Mathf.Clamp(currentSpeed, 0, 1);
    }

    void LateUpdate()
    {
        if (Application.isPlaying) AdvanceClock(Time.timeAsDouble);
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (!this || !isActiveAndEnabled || Application.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (clockStarted && now - lastClock < 1.0 / 30) return;
        AdvanceClock(now);
        if (animateInEditor) SceneView.RepaintAll();
    }
#endif

    void AdvanceClock(double now)
    {
        if (clockStarted && (Application.isPlaying || animateInEditor))
        {
            double dt = System.Math.Max(0, now - lastClock);
            waveClock += dt * waveSpeed;
            currentClock += dt;
        }
        lastClock = now;
        clockStarted = true;
        UpdateMaterial(WaveTime);
    }

    void PrepareWaves()
    {
        var settings = new Vector4(waveHeight, wavelength, waveDirection, shorelineFade);
        if (cachedSettings == settings) return;
        cachedSettings = settings;
        SetWave(0, waveDirection, wavelength, waveHeight * .5f * .62f);
        SetWave(1, waveDirection + 34, wavelength * .52f, waveHeight * .5f * .26f);
        SetWave(2, waveDirection - 47, wavelength * .29f, waveHeight * .5f * .12f);
    }

    void SetWave(int i, float direction, float length, float amplitude)
    {
        float angle = direction * Mathf.Deg2Rad;
        float k = 2 * Mathf.PI / Mathf.Max(1, length);
        waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), k, amplitude);
        frequencies[i] = Mathf.Sqrt(9.81f * k);
    }

    public void UpdateMaterial(float time)
    {
        if (!waterRenderer) waterRenderer = GetComponent<MeshRenderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
        PrepareWaves();
        waterRenderer.GetPropertyBlock(properties);
        for (int i = 0; i < 3; i++) properties.SetVector("_SeaWave" + i, waves[i]);
        properties.SetVector("_SeaFrequencies", new Vector4(frequencies[0], frequencies[1], frequencies[2], 0));
        properties.SetFloat("_SeaTime", time);
        properties.SetFloat("_RiverTime", (float)currentClock);
        properties.SetFloat("_RiverSpeed", currentSpeed);
        properties.SetFloat("_RiverFlow", followRiverFlow ? 1 : 0);
        properties.SetFloat("_ShorelineFade", Mathf.Max(.5f, shorelineFade));
        waterRenderer.SetPropertyBlock(properties);
        if (cachedMesh)
        {
            var bounds = cachedMesh.bounds;
            bounds.Expand(new Vector3(0, waveHeight + .5f, 0));
            waterRenderer.localBounds = bounds;
        }
    }

    // Called once at load (and explicitly after a mesh replacement), never per physics probe.
    public void RebuildSurfaceIndex()
    {
        cells.Clear();
        cachedMesh = GetComponent<MeshFilter>().sharedMesh;
        vertices = null; triangles = null; shore = null;
        if (!cachedMesh || !cachedMesh.isReadable) return;
        vertices = cachedMesh.vertices;
        triangles = cachedMesh.triangles;
        shore = cachedMesh.uv;
        flowDirections = cachedMesh.uv3;
        if (shore.Length != vertices.Length) { triangles = null; return; }
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
            Vector2Int min = Cell(Vector3.Min(a, Vector3.Min(b, c)));
            Vector2Int max = Cell(Vector3.Max(a, Vector3.Max(b, c)));
            for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
            {
                var key = new Vector2Int(x, z);
                if (!cells.TryGetValue(key, out var list)) cells.Add(key, list = new List<int>());
                list.Add(t);
            }
        }
    }

    static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));

    public bool TrySample(Vector3 worldPoint, out SurfaceSample sample) => TrySample(worldPoint, WaveTime, out sample);

    public bool TrySample(Vector3 worldPoint, float time, out SurfaceSample sample)
    {
        sample = default;
        if (!isActiveAndEnabled || triangles == null) return false;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        if (!cells.TryGetValue(Cell(p), out var candidates)) return false;
        PrepareWaves();
        foreach (int t in candidates)
        {
            int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
            Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
            float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(det) < 1e-9f) continue;
            float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / det;
            float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / det;
            float w = 1 - u - v;
            if (u < -.00001f || v < -.00001f || w < -.00001f) continue;
            if (shore[ia].y * u + shore[ib].y * v + shore[ic].y * w < .5f) return false;
            sample.baseHeight = transform.TransformPoint(new Vector3(p.x, a.y * u + b.y * v + c.y * w, p.z)).y;
            DisplaceVertex(ia, time, out Vector3 da, out Vector3 va);
            DisplaceVertex(ib, time, out Vector3 db, out Vector3 vb);
            DisplaceVertex(ic, time, out Vector3 dc, out Vector3 vc);
            sample.height = transform.TransformPoint(da * u + db * v + dc * w).y;
            var normal = Vector3.Cross(db - da, dc - da).normalized;
            sample.normal = transform.TransformDirection(normal.y < 0 ? -normal : normal);
            sample.shoreDistance = shore[ia].x * u + shore[ib].x * v + shore[ic].x * w;
            float angle = currentDirection * Mathf.Deg2Rad;
            var current = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentSpeed;
            if (followRiverFlow && flowDirections != null && flowDirections.Length == vertices.Length)
            {
                Vector2 direction = (flowDirections[ia] * u + flowDirections[ib] * v + flowDirections[ic] * w).normalized;
                current = new Vector3(direction.x, 0, direction.y) * currentSpeed;
            }
            sample.velocity = transform.TransformVector((va * u + vb * v + vc * w) * waveSpeed + current);
            return true;
        }
        return false;
    }

    public void DisplaceVertex(int index, float time, out Vector3 position, out Vector3 velocity)
    {
        PrepareWaves();
        position = vertices[index];
        velocity = Vector3.zero;
        float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(shore[index].x / Mathf.Max(.5f, shorelineFade)));
        for (int i = 0; i < 3; i++)
        {
            Vector4 wave = waves[i];
            float phase = wave.z * (wave.x * position.x + wave.y * position.z) - frequencies[i] * time + Phases[i];
            float amplitude = wave.w * fade;
            position.y += amplitude * Mathf.Sin(phase);
            velocity.y -= amplitude * frequencies[i] * Mathf.Cos(phase);
            float drift = -.2f * amplitude * frequencies[i] * Mathf.Sin(phase);
            velocity.x += wave.x * drift;
            velocity.z += wave.y * drift;
        }
    }
}
