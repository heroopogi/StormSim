using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BanagoSeaPhysicsBuilder
{
    public const string Folder = "Assets/StormSim/Generated/BanagoSeaPhysics_20260907";
    public static string Status = "Not installed";

    [MenuItem("Tools/StormSim/Sea Physics/Install Waves and Buoyancy")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before installing sea physics.");
        var coast = GameObject.Find("Banago Coast");
        if (!coast) throw new InvalidOperationException("Banago Coast was not found.");
        var water = coast.transform.Find("Calm water - traced sea boundary");
        if (!water) throw new InvalidOperationException("The traced sea surface was not found.");
        if (water.GetComponent<BanagoSeaWaves>()) throw new InvalidOperationException("Sea physics is already installed. Adjust the Sea Waves component instead.");
        var shader = Shader.Find("StormSim/Dynamic Coastal Sea");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The dynamic sea shader has compilation errors.");
        var filter = water.GetComponent<MeshFilter>();
        var renderer = water.GetComponent<MeshRenderer>();
        var boats = coast.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Bangka -")).ToArray();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backup = "RoadRepairBackup/SeaPhysics_" + stamp;
        Directory.CreateDirectory(backup);
        EditorSceneManager.SaveScene(coast.scene, backup + "/MAIN_GAME.before.unity", true);
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add sea waves and floating boats");
        try
        {
            // Uniform subdivision keeps shared edges identical, including the shoreline cutouts.
            var mesh = RefineWater(filter.sharedMesh);
            AssetDatabase.CreateAsset(mesh, Folder + "/DynamicSeaMesh.asset");
            var material = new Material(renderer.sharedMaterial) { name = "Dynamic coastal sea", shader = shader };
            material.SetFloat("_NormalStrength", .14f);
            material.SetFloat("_FoamStrength", .10f);
            AssetDatabase.CreateAsset(material, Folder + "/DynamicSea.mat");
            Undo.RecordObject(filter, "Subdivide sea surface");
            Undo.RecordObject(renderer, "Apply dynamic sea material");
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            Undo.RecordObject(water.gameObject, "Enable dynamic sea rendering");
            GameObjectUtility.SetStaticEditorFlags(water.gameObject, 0);
            var sea = Undo.AddComponent<BanagoSeaWaves>(water.gameObject);
            sea.RebuildSurfaceIndex();
            sea.UpdateMaterial(0);
            var coastalControl = coast.GetComponent<BanagoCoastalTest>();
            if (coastalControl)
            {
                Undo.RecordObject(coastalControl, "Use dynamic sea material");
                coastalControl.waterMaterial = material;
            }
            // This study scene mixes globe-anchored groups with local terrain/buildings.
            // Keep its kilometre-scale physics in one fixed frame instead of shifting only some groups.
            var camera = GameObject.Find("DynamicCamera");
            var originShift = camera ? camera.GetComponents<MonoBehaviour>().FirstOrDefault(b => b && b.GetType().Name == "CesiumOriginShift") : null;
            if (originShift)
            {
                Undo.RecordObject(originShift, "Keep the sea and ground in the same local physics frame");
                originShift.enabled = false;
            }
            int shifted = 0;
            foreach (var boat in boats)
            {
                Vector3 position = boat.transform.position;
                Vector3[] probes = BoatProbes();
                bool allWet = false;
                for (int step = 0; step <= 12; step++)
                {
                    Vector3 offset = sea.transform.TransformVector(Vector3.left * (step * .5f));
                    allWet = probes.All(p => sea.TrySample(boat.transform.TransformPoint(p) + offset, 0, out _));
                    if (!allWet) continue;
                    position += offset;
                    if (step > 0) shifted++;
                    break;
                }
                if (!allWet || !sea.TrySample(position, 0, out var sample))
                    throw new InvalidOperationException("Cannot place all buoyancy probes in the sea for " + boat.name);
                Undo.RecordObject(boat.transform, "Seat floating hull at the waterline");
                position.y = sample.baseHeight - .005f;
                boat.transform.position = position;
                ConfigureBoat(boat.gameObject, sea, probes);
            }
            AddFloatingTimber(coast.transform, sea);
            EditorUtility.SetDirty(sea);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(coast.scene);
            EditorSceneManager.SaveScene(coast.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = water.gameObject;
            FrameBoats();
            Status = "Installed waves on " + mesh.vertexCount + " vertices / " + mesh.triangles.Length / 3
                + " triangles; " + boats.Length + " buoyant bangka; 4 floating timber pieces. Boats shifted offshore: " + shifted
                + ". Backup: " + backup;
            File.WriteAllText(Folder + "/README.md", "# Banago sea waves and buoyancy\n\n"
                + "Select Banago Coast > Calm water - traced sea boundary. Banago Sea Waves exposes wave height (crest to trough), wavelength, speed, direction, shoreline fade, current speed and direction. Waves animate in Scene view; Rigidbody buoyancy runs in Play mode.\n\n"
                + "12 bangka use six buoyancy probes, hull/outrigger colliders, water drag and a 1.5m slack mooring. Four timber pieces under Floating sea debris drift freely. Select a body to adjust resistance, equilibrium depth or mooring; cyan gizmos show its probes.\n\n"
                + "The GPU and CPU use the same three directional swells. Physics interpolates the displaced mesh triangles, respects shoreline holes, and retains the original sea elevation. Water has no solid surface collider. This is interactive game water, with no inland flooding or fluid flow solver.\n\n"
                + "The original sea mesh/material assets and disabled static boat colliders are retained. The prior scene is backed up at " + backup + "/MAIN_GAME.before.unity.\n");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    public static Vector3[] BoatProbes()
    {
        var points = new List<Vector3>();
        foreach (float x in new[] { -1.65f, 1.65f })
            foreach (float z in new[] { -1.9f, 0, 1.9f }) points.Add(new Vector3(x, -.22f, z));
        return points.ToArray();
    }

    static void ConfigureBoat(GameObject boat, BanagoSeaWaves sea, Vector3[] probes)
    {
        Undo.RecordObject(boat, "Make boat dynamic");
        GameObjectUtility.SetStaticEditorFlags(boat, 0);
        foreach (var collider in boat.GetComponents<Collider>())
        {
            Undo.RecordObject(collider, "Preserve original static collider");
            collider.enabled = false;
        }
        Box(boat, new Vector3(0,.10f,0), new Vector3(.86f,.50f,4.3f));
        foreach (float end in new[] { -1f, 1f }) Box(boat, new Vector3(0,.15f,end*2.6f), new Vector3(.46f,.36f,1));
        foreach (float side in new[] { -1f, 1f }) Box(boat, new Vector3(side*2,.08f,0), new Vector3(.13f,.16f,4.6f));
        var body = Undo.AddComponent<Rigidbody>(boat);
        float scale = Mathf.Abs(boat.transform.lossyScale.y);
        ConfigureRigidbody(body, 160 * scale * scale * scale);
        var buoyancy = Undo.AddComponent<BanagoBuoyantBody>(boat);
        buoyancy.sea = sea;
        buoyancy.buoyancyPoints = probes;
        buoyancy.equilibriumDepth = .22f * scale;
        buoyancy.mooringAnchor = sea.transform.InverseTransformPoint(boat.transform.position);
        buoyancy.InitializeBody();
    }

    static BoxCollider Box(GameObject go, Vector3 center, Vector3 size)
    {
        var box = Undo.AddComponent<BoxCollider>(go);
        box.center = center;
        box.size = size;
        return box;
    }

    static void ConfigureRigidbody(Rigidbody body, float mass)
    {
        body.mass = mass;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.linearDamping = .02f;
        body.angularDamping = .9f;
        body.solverIterations = 12;
        body.solverVelocityIterations = 6;
    }

    static void AddFloatingTimber(Transform coast, BanagoSeaWaves sea)
    {
        var root = new GameObject("Floating sea debris");
        Undo.RegisterCreatedObjectUndo(root, "Add floating timber");
        root.transform.SetParent(coast, false);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Weathered floating timber" };
        material.SetColor("_BaseColor", new Color(.25f,.18f,.105f));
        material.SetFloat("_Smoothness", .22f);
        AssetDatabase.CreateAsset(material, Folder + "/FloatingTimber.mat");
        var positions = new[] { new Vector3(-160,0,1378), new Vector3(-164,0,1361), new Vector3(-150,0,1334), new Vector3(-182,0,1367) };
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 p = coast.TransformPoint(positions[i]);
            if (!sea.TrySample(p, 0, out var water)) throw new InvalidOperationException("Floating timber location is outside the sea.");
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Add floating timber");
            go.name = "Floating timber " + (i + 1);
            go.transform.SetParent(root.transform, false);
            p.y = water.baseHeight + .025f;
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0, 23 + i * 39, 0);
            float length = 1.4f + i * .25f;
            // Bake the dimensions into the mesh so buoyancy uses metre-based local coordinates.
            var mesh = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "Floating timber " + (i + 1);
            mesh.vertices = mesh.vertices.Select(v => Vector3.Scale(v, new Vector3(.22f,.16f,length))).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/FloatingTimber" + (i + 1) + ".asset");
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<BoxCollider>().size = new Vector3(.22f,.16f,length);
            var body = Undo.AddComponent<Rigidbody>(go);
            ConfigureRigidbody(body, 12 + i * 3);
            var buoyancy = Undo.AddComponent<BanagoBuoyantBody>(go);
            buoyancy.sea = sea;
            buoyancy.moored = false;
            // Half the probes are wet at rest: twice the per-probe lift supports the same draft.
            buoyancy.equilibriumDepth = .0275f;
            buoyancy.waterResistance = 2.2f;
            buoyancy.verticalDamping = 14;
            buoyancy.localCenterOfMass = new Vector3(0,-.035f,0);
            buoyancy.buoyancyPoints = TimberProbes(length);
            buoyancy.InitializeBody();
        }
    }

    public static Vector3[] TimberProbes(float length)
    {
        var points = new List<Vector3>();
        foreach (float x in new[] { -.08f, .08f })
            foreach (float y in new[] { -.08f, .08f })
                foreach (float z in new[] { -length*.35f, length*.35f }) points.Add(new Vector3(x,y,z));
        return points.ToArray();
    }

    public static Mesh RefineWater(Mesh source)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        var lookup = new Dictionary<(Vector3, Vector2), int>();
        var sourceVertices = source.vertices;
        var sourceUV = source.uv;
        foreach (int old in source.triangles)
        {
            var key = (sourceVertices[old], sourceUV[old]);
            if (!lookup.TryGetValue(key, out int index))
            {
                index = vertices.Count;
                lookup.Add(key, index);
                vertices.Add(key.Item1); uv.Add(key.Item2);
            }
            triangles.Add(index);
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var edges = new Dictionary<ulong, int>();
            var refined = new List<int>(triangles.Count * 4);
            int Mid(int a, int b)
            {
                ulong key = ((ulong)(uint)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
                if (edges.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                edges.Add(key,index);
                vertices.Add((vertices[a]+vertices[b])*.5f);
                uv.Add((uv[a]+uv[b])*.5f);
                return index;
            }
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a=triangles[t], b=triangles[t+1], c=triangles[t+2];
                int ab=Mid(a,b), bc=Mid(b,c), ca=Mid(c,a);
                refined.AddRange(new[] { a,ab,ca, ab,b,bc, ca,bc,c, ab,bc,ca });
            }
            triangles = refined;
        }
        var mesh = new Mesh { name = "Traced sea - subdivided wave surface", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    [MenuItem("Tools/StormSim/Sea Physics/Frame Floating Boats")]
    public static void FrameBoats()
    {
        var view = SceneView.lastActiveSceneView;
        if (!view) return;
        view.orthographic = false;
        view.LookAtDirect(new Vector3(-160,-7.3f,1365), Quaternion.LookRotation(new Vector3(15,-10,20)), 27);
        view.showGrid = false;
        view.Repaint();
    }
}
