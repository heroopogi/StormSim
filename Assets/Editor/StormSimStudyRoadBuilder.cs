using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds persistent, geographically anchored road meshes from the archived OSM extract.
// Widths without OSM width/lane tags are visual estimates; coordinates are source data.
public static class StormSimStudyRoadBuilder
{
    const string RootName = "Study Area Roads";
    const string Source = "RoadRepairBackup/study-roads-source.json";
    const double South = 10.7045, North = 10.7168, West = 122.9475, East = 122.9585;
    static CesiumGeoreference geo;
    static readonly List<Section> sections = new List<Section>();
    static readonly Dictionary<long, Junction> junctions = new Dictionary<long, Junction>();
    static readonly List<Surface> surfaces = new List<Surface>();
    static string output;
    static bool running;
    public static string Status = "Not started";

    class Section
    {
        public string name, kind;
        public long way, start, end;
        public float width;
        public int lanes;
        public List<Vector3> points = new List<Vector3>();
    }
    class Junction
    {
        public Vector3 center;
        public List<Section> roads = new List<Section>();
        public List<Vector3> rim = new List<Vector3>();
        public float radius;
    }
    class Surface
    {
        public string name, kind;
        public Section road;
        public List<Vector3> vertices = new List<Vector3>();
        public List<int> triangles = new List<int>();
        public List<Vector2> uv = new List<Vector2>();
    }

    public static void Build()
    {
        if (running) throw new InvalidOperationException("Road sampling is already running.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        geo = UnityEngine.Object.FindFirstObjectByType<CesiumGeoreference>();
        var terrain = GameObject.Find("Cesium World Terrain").GetComponent<Cesium3DTileset>();
        if (!geo || !terrain) throw new InvalidOperationException("Cesium terrain is required.");
        sections.Clear(); junctions.Clear(); surfaces.Clear();
        var elements = (JArray)JObject.Parse(File.ReadAllText(Source))["elements"];
        var nodes = elements.Where(e => (string)e["type"] == "node").ToDictionary(e => (long)e["id"], e => Geo((double)e["lon"], (double)e["lat"], 0));
        var ways = elements.Where(e => (string)e["type"] == "way" && (string)e["tags"]?["highway"] != "construction" && (string)e["tags"]?["highway"] != "proposed").ToList();
        var degrees = new Dictionary<long, HashSet<long>>();
        foreach (var way in ways)
        {
            var ids = way["nodes"].Values<long>().ToArray();
            for (int i = 0; i < ids.Length - 1; i++)
            {
                if (!degrees.ContainsKey(ids[i])) degrees[ids[i]] = new HashSet<long>();
                if (!degrees.ContainsKey(ids[i + 1])) degrees[ids[i + 1]] = new HashSet<long>();
                degrees[ids[i]].Add(ids[i + 1]); degrees[ids[i + 1]].Add(ids[i]);
            }
        }
        Vector3 sw = Geo(West, South, 0), ne = Geo(East, North, 0);
        long boundaryId = -1;
        foreach (var way in ways)
        {
            var tags = way["tags"];
            string kind = (string)tags["highway"], name = (string)tags["name"];
            int lanes;
            if (!int.TryParse((string)tags["lanes"], out lanes)) lanes = kind == "secondary" || kind == "tertiary" || kind == "primary" ? 2 : 1;
            float width;
            if (!float.TryParse((string)tags["width"], NumberStyles.Float, CultureInfo.InvariantCulture, out width))
                width = lanes > 1 ? lanes * 3.25f : kind == "residential" ? 4.5f : kind == "service" ? 3.2f : kind == "track" ? 2.8f : 2f;
            width = Mathf.Clamp(width, 1.2f, 18f);
            var ids = way["nodes"].Values<long>().ToArray();
            Section current = null;
            for (int i = 0; i < ids.Length - 1; i++)
            {
                if (!nodes.ContainsKey(ids[i]) || !nodes.ContainsKey(ids[i + 1])) { current = null; continue; }
                Vector3 a = nodes[ids[i]], b = nodes[ids[i + 1]];
                float t0 = 0, t1 = 1;
                if (!Clip(a.x, b.x - a.x, sw.x, ne.x, ref t0, ref t1) || !Clip(a.z, b.z - a.z, sw.z, ne.z, ref t0, ref t1)) { current = null; continue; }
                Vector3 p = Vector3.Lerp(a, b, t0), q = Vector3.Lerp(a, b, t1);
                if (FlatDistance(p, q) < .1f) continue;
                long start = t0 > .00001f ? boundaryId-- : ids[i], end = t1 < .99999f ? boundaryId-- : ids[i + 1];
                if (current == null || current.end != start)
                {
                    current = new Section { way = (long)way["id"], name = string.IsNullOrEmpty(name) ? kind + " " + way["id"] : name, kind = kind, width = width, lanes = lanes, start = start };
                    current.points.Add(p); sections.Add(current);
                }
                current.points.Add(q); current.end = end;
                if (end < 0 || degrees[end].Count != 2) current = null;
            }
        }
        sections.RemoveAll(s => s.points.Count < 2 || Length(s.points) < 1f);
        foreach (var s in sections) { Register(s.start, s.points[0], s); Register(s.end, s.points[s.points.Count - 1], s); }
        foreach (var j in junctions.Values)
            j.radius = j.roads.Count > 2 ? j.roads.Max(s => s.width) * .65f : j.roads.Count == 2 ? j.roads.Max(s => s.width) * .2f : 0;
        foreach (var s in sections)
        {
            var p = new List<Vector3>(s.points);
            float length = Length(p);
            Trim(p, Mathf.Min(junctions[s.start].radius, length * .3f));
            p.Reverse(); Trim(p, Mathf.Min(junctions[s.end].radius, length * .3f)); p.Reverse();
            var dense = new List<Vector3> { p[0] };
            for (int i = 1; i < p.Count; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(FlatDistance(p[i - 1], p[i]) / 4f));
                for (int k = 1; k <= steps; k++) dense.Add(Vector3.Lerp(p[i - 1], p[i], k / (float)steps));
            }
            var surface = new Surface { name = s.name, kind = s.kind, road = s };
            float distance = 0;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 prev = Direction(dense[Mathf.Max(0, i - 1)], dense[i]), next = Direction(dense[i], dense[Mathf.Min(i + 1, dense.Count - 1)]);
                if (prev == Vector3.zero) prev = next; if (next == Vector3.zero) next = prev;
                Vector3 right = Vector3.Cross(Vector3.up, next), miter = (right + Vector3.Cross(Vector3.up, prev)).normalized;
                Vector3 offset = miter * Mathf.Min(s.width, s.width * .5f / Mathf.Max(.5f, Vector3.Dot(miter, right)));
                surface.vertices.Add(dense[i] - offset); surface.vertices.Add(dense[i] + offset);
                surface.uv.Add(new Vector2(0, distance)); surface.uv.Add(new Vector2(s.width, distance));
                if (i > 0) Quad(surface, i * 2 - 2, i * 2 - 1, i * 2, i * 2 + 1);
                if (i < dense.Count - 1) distance += FlatDistance(dense[i], dense[i + 1]);
            }
            junctions[s.start].rim.AddRange(surface.vertices.Take(2));
            junctions[s.end].rim.AddRange(surface.vertices.Skip(surface.vertices.Count - 2));
            surfaces.Add(surface);
        }
        foreach (var pair in junctions)
        {
            var j = pair.Value; if (j.roads.Count < 2) continue;
            var hull = Hull(j.rim); if (hull.Count < 3) continue;
            var surface = new Surface { name = "Junction " + pair.Key, kind = "junction" };
            surface.vertices.Add(j.center); surface.vertices.AddRange(hull);
            foreach (var v in surface.vertices) surface.uv.Add(new Vector2(v.x, v.z));
            for (int i = 0; i < hull.Count; i++) Triangle(surface, 0, i + 1, (i + 1) % hull.Count + 1);
            surfaces.Add(surface);
        }
        var coordinates = surfaces.SelectMany(s => s.vertices).Select(p => CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(geo.TransformUnityPositionToEarthCenteredEarthFixed(new double3(p.x, p.y, p.z)))).ToArray();
        running = true; Status = "Sampling " + coordinates.Length + " road vertices";
        var task = terrain.SampleHeightMostDetailed(coordinates);
        double started = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction poll = null;
        poll = () =>
        {
            if (!task.IsCompleted && EditorApplication.timeSinceStartup - started < 180) return;
            EditorApplication.update -= poll; running = false;
            try
            {
                if (!task.IsCompleted) throw new TimeoutException("Terrain sampling timed out.");
                if (task.IsFaulted) throw task.Exception;
                var result = task.Result;
                int failed = result.sampleSuccess.Count(ok => !ok);
                if (failed > 0) throw new InvalidOperationException(failed + " terrain samples failed; no scene changes made.");
                int index = 0;
                foreach (var s in surfaces) for (int i = 0; i < s.vertices.Count; i++)
                {
                    var llh = result.longitudeLatitudeHeightPositions[index++];
                    s.vertices[i] = Geo(llh.x, llh.y, llh.z + .16);
                }
                SaveNetwork();
            }
            catch (Exception ex) { Status = "Failed: " + ex.Message; Debug.LogError(Status); }
        };
        EditorApplication.update += poll;
    }

    static void SaveNetwork()
    {
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build study area roads");
        output = "Assets/StormSim/Generated/StudyRoads_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(output); AssetDatabase.Refresh();
        var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Build study area roads");
        root.transform.SetParent(geo.transform, false);
        var asphalt = MaterialAsset("Asphalt", new Color(.27f, .275f, .265f));
        var concrete = MaterialAsset("Access lanes", new Color(.27f, .275f, .265f));
        var dirt = MaterialAsset("Tracks", new Color(.16f, .12f, .075f));
        var white = MaterialAsset("White edge paint", new Color(.84f, .84f, .82f));
        var centerPaint = MaterialAsset("White center paint", new Color(.84f, .84f, .82f));
        int n = 0, colliders = 0;
        foreach (var s in surfaces)
        {
            var material = s.kind == "track" ? dirt : s.kind == "footway" || s.kind == "path" || s.kind == "pedestrian" || s.kind == "service" ? concrete : asphalt;
            var go = SaveSurface(s, root.transform, material, "Road_" + n++, true); colliders++;
            if (s.road == null) continue;
            var edge = SolidEdges(s.vertices, s.triangles, s.road.width);
            SaveSurface(edge, go.transform, white, "Edge_" + n, false);
            if (s.road.lanes < 2) continue;
            var center = new Surface { name = "Lane markings" };
            for (int lane = 1; lane < s.road.lanes; lane++) AddLine(center, s, s.road.width * lane / s.road.lanes, .14f, true);
            SaveSurface(center, go.transform, centerPaint, "Center_" + n, false);
        }
        root.AddComponent<CesiumGlobeAnchor>();
        var old = geo.GetComponentsInChildren<Transform>(true).Where(t => t.name == RootName && t.gameObject != root).ToArray();
        foreach (var t in old) { Undo.RecordObject(t.gameObject, "Replace previous study roads"); t.gameObject.SetActive(false); t.name = RootName + " (previous)"; }
        File.Copy(Source, output + "/OpenStreetMap-source.json");
        Status = "Built " + sections.Count + " road sections from " + sections.Select(s => s.way).Distinct().Count() + " OSM ways, " + surfaces.Count(s => s.road == null) + " junctions, " + colliders + " colliders. Length " + (sections.Sum(s => Length(s.points)) / 1000f).ToString("F2") + " km.";
        File.WriteAllText(output + "/README.txt", Status + "\nSource: OpenStreetMap contributors, ODbL. Downloaded " + DateTime.Now.ToString("yyyy-MM-dd") + ".\nBounds: " + South + "," + West + " to " + North + "," + East + ".\nRoad widths are estimated when not supplied by OSM. Height sampling succeeded for every vertex.\nRebuild: Tools/StormSim/Build Study Area Roads\n");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
        Undo.CollapseUndoOperations(group); Debug.Log("STUDY_ROADS: " + Status);
    }

    [MenuItem("Tools/StormSim/Build Study Area Roads")]
    static void BuildMenu() { Build(); }

    // Repair the existing meshes in place; rebuilding terrain/roads is unnecessary.
    [MenuItem("Tools/StormSim/Repair All Road Edge Lines")]
    public static void RepairEdgeMarkings()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var root = GameObject.Find(RootName);
        if (!root) throw new InvalidOperationException("Study Area Roads was not found.");
        var roads = root.transform.Cast<Transform>().Where(t => !t.name.StartsWith("Junction ") && t.GetComponent<MeshFilter>()).ToArray();
        var material = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).First(m => AssetDatabase.GetAssetPath(m).EndsWith("White edge paint.mat"));
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(roads[0].GetComponent<MeshFilter>().sharedMesh)).Replace('\\', '/');
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/SolidEdgeMarkings.asset");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Repair all road edge lines");
        int added = 0, updated = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < roads.Length; i++)
            {
                var road = roads[i]; var surface = road.GetComponent<MeshFilter>().sharedMesh;
                var vertices = surface.vertices;
                if (vertices.Length < 4 || vertices.Length % 2 != 0) throw new InvalidOperationException("Unexpected road mesh: " + road.name);
                // The endpoint cross section has no corner miter, so it gives the road width.
                float width = FlatDistance(vertices[0], vertices[1]);
                var edges = SolidEdges(vertices.ToList(), surface.triangles.ToList(), width);
                var mesh = new Mesh { name = surface.name + " solid white edges", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(edges.vertices); mesh.SetTriangles(edges.triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if (i == 0) AssetDatabase.CreateAsset(mesh, assetPath); else AssetDatabase.AddObjectToAsset(mesh, assetPath);
                var child = road.Find("Edge markings");
                if (!child)
                {
                    var go = new GameObject("Edge markings"); go.transform.SetParent(road, false);
                    go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>();
                    Undo.RegisterCreatedObjectUndo(go, "Add missing edge lines"); child = go.transform; added++;
                }
                else updated++;
                var filter = child.GetComponent<MeshFilter>(); var renderer = child.GetComponent<MeshRenderer>();
                Undo.RecordObject(filter, "Fit edge lines to road triangles"); Undo.RecordObject(renderer, "Set white edge paint");
                filter.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(assetPath);
        EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
        Undo.CollapseUndoOperations(group);
        Status = "Solid white edge lines on " + roads.Length + " road sections: " + added + " added, " + updated + " repaired.";
        Debug.Log("ROAD_EDGE_REPAIR: " + Status);
    }

    struct PaintVertex
    {
        public Vector3 point;
        public float lateral;
        public PaintVertex(Vector3 point, float lateral) { this.point = point; this.lateral = lateral; }
    }

    // Clip stripes directly against each source triangle. Bilinear height interpolation
    // can sink paint below a non-planar road quad even when its corners are above it.
    static Surface SolidEdges(List<Vector3> vertices, List<int> triangles, float roadWidth)
    {
        var edge = new Surface { name = "Edge markings" };
        const float inset = .28f, lineWidth = .17f;
        float lo = (inset - lineWidth * .5f) / roadWidth;
        float hi = (inset + lineWidth * .5f) / roadWidth;
        for (int i = 0; i < triangles.Count; i += 3)
        {
            for (int side = 0; side < 2; side++)
            {
                var polygon = new List<PaintVertex>();
                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[i + k]; polygon.Add(new PaintVertex(vertices[index], index % 2));
                }
                polygon = ClipPaint(polygon, side == 0 ? lo : 1 - hi, true);
                polygon = ClipPaint(polygon, side == 0 ? hi : 1 - lo, false);
                int start = edge.vertices.Count;
                foreach (var v in polygon) edge.vertices.Add(v.point + Vector3.up * .045f);
                for (int k = 1; k < polygon.Count - 1; k++) Triangle(edge, start, start + k, start + k + 1);
            }
        }
        return edge;
    }

    static List<PaintVertex> ClipPaint(List<PaintVertex> polygon, float limit, bool minimum)
    {
        var result = new List<PaintVertex>();
        if (polygon.Count == 0) return result;
        var previous = polygon[polygon.Count - 1];
        bool previousInside = minimum ? previous.lateral >= limit : previous.lateral <= limit;
        foreach (var current in polygon)
        {
            bool inside = minimum ? current.lateral >= limit : current.lateral <= limit;
            if (inside != previousInside)
            {
                float t = (limit - previous.lateral) / (current.lateral - previous.lateral);
                result.Add(new PaintVertex(Vector3.Lerp(previous.point, current.point, t), limit));
            }
            if (inside) result.Add(current);
            previous = current; previousInside = inside;
        }
        return result;
    }

    static Material MaterialAsset(string name, Color color)
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/Materials/Generated_OSM_Road_Surface.mat");
        var material = new Material(template) { name = name };
        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", null);
        AssetDatabase.CreateAsset(material, output + "/" + name + ".mat"); return material;
    }
    static GameObject SaveSurface(Surface s, Transform parent, Material material, string file, bool collider)
    {
        var mesh = new Mesh { name = s.name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(s.vertices); mesh.SetTriangles(s.triangles, 0);
        if (s.uv.Count == s.vertices.Count) mesh.SetUVs(0, s.uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, output + "/" + file + ".asset");
        var go = new GameObject(s.name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }
    static void AddLine(Surface line, Surface road, float offset, float width, bool dashed)
    {
        float cursor = 0, total = Length(road.vertices.Where((v, i) => i % 2 == 0).ToList());
        for (int i = 0; i < road.vertices.Count - 2; i += 2)
        {
            Vector3 a = (road.vertices[i] + road.vertices[i + 1]) * .5f, b = (road.vertices[i + 2] + road.vertices[i + 3]) * .5f;
            float length = FlatDistance(a, b); if (length < .01f) continue;
            float t = 0;
            while (t < length - .001f)
            {
                float phase = (cursor + t) % 8f;
                float step = Mathf.Min(length - t, dashed ? (phase < 3f ? 3f - phase : 8f - phase) : length);
                if (step < .001f) step = .001f;
                if (!dashed || (phase < 3f && cursor + t > 2 && cursor + t < total - 2))
                {
                    float f0 = (offset - width * .5f) / road.road.width, f1 = (offset + width * .5f) / road.road.width;
                    Vector3 l0 = Vector3.Lerp(road.vertices[i], road.vertices[i + 1], f0), r0 = Vector3.Lerp(road.vertices[i], road.vertices[i + 1], f1);
                    Vector3 l1 = Vector3.Lerp(road.vertices[i + 2], road.vertices[i + 3], f0), r1 = Vector3.Lerp(road.vertices[i + 2], road.vertices[i + 3], f1);
                    int v = line.vertices.Count;
                    line.vertices.Add(Vector3.Lerp(l0, l1, t / length) + Vector3.up * .028f); line.vertices.Add(Vector3.Lerp(r0, r1, t / length) + Vector3.up * .028f);
                    line.vertices.Add(Vector3.Lerp(l0, l1, (t + step) / length) + Vector3.up * .028f); line.vertices.Add(Vector3.Lerp(r0, r1, (t + step) / length) + Vector3.up * .028f);
                    Quad(line, v, v + 1, v + 2, v + 3);
                }
                t += step;
            }
            cursor += length;
        }
    }
    static Vector3 Geo(double lon, double lat, double height)
    {
        double3 p = geo.TransformEarthCenteredEarthFixedPositionToUnity(CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lon, lat, height)));
        return new Vector3((float)p.x, (float)p.y, (float)p.z);
    }
    static bool Clip(float a, float delta, float min, float max, ref float t0, ref float t1)
    {
        if (Mathf.Abs(delta) < .00001f) return a >= min && a <= max;
        float lo = (min - a) / delta, hi = (max - a) / delta;
        if (lo > hi) { float temp = lo; lo = hi; hi = temp; }
        t0 = Mathf.Max(t0, lo); t1 = Mathf.Min(t1, hi); return t0 <= t1;
    }
    static void Register(long id, Vector3 p, Section s)
    {
        if (!junctions.ContainsKey(id)) junctions[id] = new Junction { center = p };
        junctions[id].roads.Add(s);
    }
    static void Trim(List<Vector3> p, float amount)
    {
        while (amount > .001f && p.Count > 1)
        {
            float d = FlatDistance(p[0], p[1]);
            if (d <= amount && p.Count > 2) { p.RemoveAt(0); amount -= d; }
            else { p[0] = Vector3.Lerp(p[0], p[1], Mathf.Min(.99f, amount / Mathf.Max(.001f, d))); break; }
        }
    }
    static float Length(List<Vector3> p) { float d = 0; for (int i = 1; i < p.Count; i++) d += FlatDistance(p[i - 1], p[i]); return d; }
    static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    static Vector3 Direction(Vector3 a, Vector3 b) { b -= a; b.y = 0; return b.normalized; }
    static void Quad(Surface s, int a, int b, int c, int d) { Triangle(s, a, c, b); Triangle(s, b, c, d); }
    static void Triangle(Surface s, int a, int b, int c)
    {
        if (Vector3.Cross(s.vertices[b] - s.vertices[a], s.vertices[c] - s.vertices[a]).y < 0) { int t = b; b = c; c = t; }
        s.triangles.Add(a); s.triangles.Add(b); s.triangles.Add(c);
    }
    static float Cross(Vector3 a, Vector3 b, Vector3 c) { return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x); }
    static List<Vector3> Hull(List<Vector3> points)
    {
        var p = points.OrderBy(v => v.x).ThenBy(v => v.z).ToList();
        var hull = new List<Vector3>();
        foreach (var v in p) { while (hull.Count > 1 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], v) <= .001f) hull.RemoveAt(hull.Count - 1); hull.Add(v); }
        int lower = hull.Count;
        for (int i = p.Count - 2; i >= 0; i--) { while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p[i]) <= .001f) hull.RemoveAt(hull.Count - 1); hull.Add(p[i]); }
        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1); return hull;
    }
}
