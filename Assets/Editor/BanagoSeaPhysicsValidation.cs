using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>Runs the installed forces in a disposable physics scene; never simulates the user's edit scene.</summary>
public static class BanagoSeaPhysicsValidation
{
    public static string LastReport = "Not run";

    [MenuItem("Tools/StormSim/Sea Physics/Validate Waves and Buoyancy")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run validation outside Play mode.");
        var source = UnityEngine.Object.FindAnyObjectByType<BanagoSeaWaves>();
        if (!source) throw new InvalidOperationException("Install sea physics first.");
        var originals = UnityEngine.Object.FindObjectsByType<BanagoBuoyantBody>().Where(b => b.sea == source).ToArray();
        var report = new StringBuilder();
        report.AppendLine("Banago sea physics validation " + DateTime.Now.ToString("s"));
        Require(originals.Length >= 16, "Expected the bangka and floating timber to be installed.");
        var surfaceMaterial = source.GetComponent<Renderer>().sharedMaterial;
        Require(surfaceMaterial && surfaceMaterial.HasProperty("_SeaWave0") && surfaceMaterial.HasProperty("_SeaTime")
            && surfaceMaterial.HasProperty("_SeaFrequencies"), "The sea renderer is using a material without moving-wave support.");
        Require(!ShaderUtil.ShaderHasError(surfaceMaterial.shader), "Sea shader has errors.");
        source.UpdateMaterial(source.WaveTime);
        var properties = new MaterialPropertyBlock();
        source.GetComponent<Renderer>().GetPropertyBlock(properties);
        Require(properties.GetVector("_SeaWave0").w > 0, "Wave cache was not initialized after the editor reload.");
        source.TrySample(new Vector3(-174,0,1375), 2, out var originalSurface);
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var waterObject = UnityEngine.Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(waterObject, scene);
            var sea = waterObject.GetComponent<BanagoSeaWaves>();
            sea.animateInEditor = false;
            sea.GetComponent<Renderer>().enabled = false;
            sea.RebuildSurfaceIndex();
            ValidateMaterialReview(sea, scene, report);
            var bodies = new List<BanagoBuoyantBody>();
            foreach (var original in originals)
            {
                var go = UnityEngine.Object.Instantiate(original.gameObject);
                go.name = original.name;
                SceneManager.MoveGameObjectToScene(go, scene);
                foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                var buoyancy = go.GetComponent<BanagoBuoyantBody>();
                buoyancy.sea = sea;
                buoyancy.InitializeBody();
                buoyancy.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
                bodies.Add(buoyancy);
            }

            Require(!sea.TrySample(new Vector3(400,0,1300), 0, out _), "Land outside the traced sea returned water.");
            Require(!sea.TrySample(new Vector3(-180,0,2000), 0, out _), "Point beyond the sea returned water.");
            foreach (var body in bodies)
                foreach (var probe in body.buoyancyPoints)
                    Require(sea.TrySample(body.transform.TransformPoint(probe), 0, out _), body.name + " has a dry probe at its starting position.");
            report.AppendLine("PASS: water mask rejects land and points beyond the sea; all starting probes are in water.");

            var mesh = sea.GetComponent<MeshFilter>().sharedMesh;
            float originalHeight = sea.waveHeight;
            sea.waveHeight = 0;
            foreach (var body in bodies)
            {
                Require(sea.TrySample(body.transform.position, 10, out var sample), "Missing calm water sample.");
                Require(Mathf.Abs(sample.height - sample.baseHeight) < .001f, "Zero-height waves changed the sea level.");
            }
            sea.waveHeight = originalHeight;
            var point = new Vector3(-174,0,1375);
            Require(sea.TrySample(point, 0, out var first), "Missing wave test point.");
            float min = first.height, max = first.height;
            for (int i = 1; i <= 100; i++)
            {
                Require(sea.TrySample(point, i*.2f, out var sample), "Wave sampling failed.");
                min = Mathf.Min(min,sample.height); max = Mathf.Max(max,sample.height);
                Require(Mathf.Abs(sample.height-sample.baseHeight) <= sea.waveHeight*.5f+.002f, "Wave exceeded its amplitude bound.");
            }
            Require(max-min > .15f, "Moving waves do not visibly change surface height.");
            sea.waveDirection += 90;
            sea.TrySample(point, 0, out var turned);
            Require(Mathf.Abs(turned.height-first.height) > .0001f, "Changing wave direction did not change the surface.");
            sea.waveDirection = source.waveDirection;
            sea.waveSpeed = 0; sea.currentSpeed = 0;
            sea.TrySample(point, 12, out var frozen);
            Require(frozen.velocity.sqrMagnitude < 1e-8f, "Zero wave/current speed produced water velocity.");
            sea.waveSpeed = source.waveSpeed; sea.currentSpeed = source.currentSpeed;
            report.AppendLine("PASS: height, direction and speed controls; wave range at test point " + (max-min).ToString("F3") + "m.");

            // Origin shifts must move the sampling frame with the sea, as Cesium does.
            Vector3 offset = new Vector3(72,3,-45);
            sea.TrySample(point, 2, out var unshifted);
            sea.transform.position += offset;
            Require(sea.TrySample(point+offset, 2, out var shifted), "Origin shift lost the water surface.");
            Require(Mathf.Abs(shifted.height-unshifted.height-offset.y) < .001f, "Origin shift changed relative sea height.");
            sea.transform.position -= offset;
            report.AppendLine("PASS: sea sampling follows a translated world origin.");

            var physics = scene.GetPhysicsScene();
            SimulateScenario("Calm water", sea, bodies, originals, physics, 0, 0, 30, report);
            SimulateScenario("Default waves and current", sea, bodies, originals, physics, source.waveHeight, source.currentSpeed, 90, report);
            SimulateScenario("Larger waves", sea, bodies, originals, physics, 1, .15f, 45, report);
            report.AppendLine("PASS: shader compiles; refined sea has " + mesh.vertexCount + " vertices and " + mesh.triangles.Length/3 + " triangles.");
            LastReport = report.ToString();
            File.WriteAllText(BanagoSeaPhysicsBuilder.Folder + "/Validation.txt", LastReport);
            Debug.Log(LastReport);
        }
        finally
        {
            File.WriteAllText(BanagoSeaPhysicsBuilder.Folder + "/Validation-progress.txt", report.ToString());
            EditorSceneManager.ClosePreviewScene(scene);
            SceneManager.SetActiveScene(active);
            source.TrySample(new Vector3(-174,0,1375), 2, out var preservedSurface);
            Require(Mathf.Abs(preservedSurface.height-originalSurface.height) < .0001f, "Preview validation altered the source water.");
        }
    }

    static void ValidateMaterialReview(BanagoSeaWaves sea, Scene scene, StringBuilder report)
    {
        var water = sea.GetComponent<Renderer>();
        var waveMaterial = water.sharedMaterial;
        var oldWater = AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/BanagoLocalDetail_20260904_213203/Tidal harbor water.mat");
        Require(oldWater, "The legacy water material regression fixture is missing.");
        var reviewObject = new GameObject("Legacy material lifecycle regression");
        SceneManager.MoveGameObjectToScene(reviewObject, scene);
        var ordinaryObject = new GameObject("Ordinary reviewed renderer");
        SceneManager.MoveGameObjectToScene(ordinaryObject, scene);
        var ordinary = ordinaryObject.AddComponent<MeshRenderer>();
        ordinary.sharedMaterial = waveMaterial;
        var review = reviewObject.AddComponent<BanagoMaterialReview>();
        review.targets = new Renderer[] { water, ordinary };
        review.originals = new[] { oldWater, waveMaterial };
        review.replacements = new[] { oldWater, oldWater };
        review.Apply();
        Require(water.sharedMaterial == waveMaterial, "Applying a legacy review replaced the wave shader.");
        Require(ordinary.sharedMaterial == oldWater, "Ordinary material reviews stopped applying.");
        review.enabled = false;
        Require(water.sharedMaterial == waveMaterial, "Disabling a legacy review replaced the wave shader.");
        Require(ordinary.sharedMaterial == waveMaterial, "Ordinary material reviews stopped restoring.");
        review.enabled = true;
        Require(water.sharedMaterial == waveMaterial, "Re-enabling a legacy review replaced the wave shader.");
        sea.enabled = false;
        review.Apply();
        Require(water.sharedMaterial == waveMaterial, "Temporarily disabling waves let the review replace their shader.");
        sea.enabled = true;
        report.AppendLine("PASS: legacy material apply/disable/enable preserves the wave shader; ordinary reviews still apply and restore.");
    }

    [MenuItem("Tools/StormSim/Sea Physics/Verify Rendered Wave Motion")]
    public static void VerifyRenderedMotion()
    {
        var sea = UnityEngine.Object.FindAnyObjectByType<BanagoSeaWaves>();
        if (!sea) throw new InvalidOperationException("No sea surface found.");
        var water = sea.GetComponent<MeshRenderer>();
        Require(water.sharedMaterial && water.sharedMaterial.HasProperty("_SeaTime"), "The actual sea renderer has no animated wave material.");
        // Render only the actual sea, using its assigned mesh, shader and property block.
        // A changing CPU height sample alone cannot catch a renderer using the old flat-water shader.
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        int layer = -1;
        for (int i = 31; i >= 8; i--)
            if (!renderers.Any(r => r != water && r.gameObject.layer == i)) { layer = i; break; }
        Require(layer >= 0, "No unused layer available for the temporary render check.");
        int originalLayer = water.gameObject.layer;
        var savedProperties = new MaterialPropertyBlock();
        water.GetPropertyBlock(savedProperties);
        var cameraObject = new GameObject("Temporary sea motion verification camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(512,288,24);
        var first = new Texture2D(512,288,TextureFormat.RGB24,false);
        var second = new Texture2D(512,288,TextureFormat.RGB24,false);
        var previousTarget = RenderTexture.active;
        try
        {
            camera.enabled = false;
            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f,.10f,.12f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 150;
            camera.fieldOfView = 50;
            Vector3 focus = sea.transform.TransformPoint(new Vector3(-190,-7.5f,1375));
            camera.transform.position = focus + new Vector3(-9,5,-11);
            camera.transform.LookAt(focus);
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresColorTexture = true; data.requiresDepthTexture = true;
            data.renderPostProcessing = false;
            camera.targetTexture = target;
            water.gameObject.layer = layer;
            sea.UpdateMaterial(0);
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            first.ReadPixels(new Rect(0,0,512,288),0,0); first.Apply();
            sea.UpdateMaterial(2);
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            second.ReadPixels(new Rect(0,0,512,288),0,0); second.Apply();
            var a = first.GetPixels32(); var b = second.GetPixels32();
            long difference = 0; int changed = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
                difference += delta;
                if (delta > 3) changed++;
            }
            double mean = difference/(a.Length*3.0);
            double fraction = changed/(double)a.Length;
            Directory.CreateDirectory("RoadRepairBackup/SeaMaterialFix_20260908");
            File.WriteAllBytes("RoadRepairBackup/SeaMaterialFix_20260908/waves-t0.png",first.EncodeToPNG());
            File.WriteAllBytes("RoadRepairBackup/SeaMaterialFix_20260908/waves-t2.png",second.EncodeToPNG());
            Require(mean > .15 && fraction > .01, "The rendered sea did not visibly change between wave times 0s and 2s.");
            string message = "PASS: actual sea shader="+water.sharedMaterial.shader.name+"; rendered changed pixels="
                +(fraction*100).ToString("F2")+"%; mean RGB change="+mean.ToString("F3")+"/255 between 0s and 2s.";
            File.WriteAllText("RoadRepairBackup/SeaMaterialFix_20260908/rendered-motion.txt",message);
            Debug.Log(message);
        }
        finally
        {
            water.gameObject.layer = originalLayer;
            water.SetPropertyBlock(savedProperties);
            camera.targetTexture = null;
            RenderTexture.active = previousTarget;
            target.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    static void SimulateScenario(string name, BanagoSeaWaves sea, List<BanagoBuoyantBody> bodies,
        BanagoBuoyantBody[] originals, PhysicsScene physics, float height, float current, int seconds, StringBuilder report)
    {
        sea.waveHeight = height;
        sea.currentSpeed = current;
        float[] minY = new float[bodies.Count], maxY = new float[bodies.Count], maxTilt = new float[bodies.Count];
        for (int i = 0; i < bodies.Count; i++)
        {
            var rb = bodies[i].GetComponent<Rigidbody>();
            rb.position = originals[i].transform.position;
            rb.rotation = originals[i].transform.rotation;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            minY[i] = float.PositiveInfinity; maxY[i] = float.NegativeInfinity;
        }
        Physics.SyncTransforms();
        for (int frame = 0; frame < seconds*50; frame++)
        {
            float time = frame * .02f;
            foreach (var body in bodies) body.ApplyWaterForces(time);
            physics.Simulate(.02f);
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                Vector3 p = body.GetComponent<Rigidbody>().position;
                Require(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z), name + ": non-finite body position.");
                Require(sea.TrySample(p, time, out var sample), name + ": " + body.name + " left the sea.");
                Require(Mathf.Abs(p.y-sample.height) < 1.2f, name + ": " + body.name + " sank or launched out of the water.");
                if (frame < 250) continue;
                minY[i] = Mathf.Min(minY[i],p.y); maxY[i] = Mathf.Max(maxY[i],p.y);
                maxTilt[i] = Mathf.Max(maxTilt[i],Vector3.Angle(body.transform.up,Vector3.up));
                Require(!body.moored || maxTilt[i] < 65, name + ": " + body.name + " capsized.");
            }
        }
        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            Vector3 delta = body.transform.position-originals[i].transform.position;
            float travel = new Vector2(delta.x,delta.z).magnitude;
            Require(!body.moored || travel < body.mooringRadius+1, name + ": mooring failed for " + body.name);
            if (height == 0) Require(maxY[i]-minY[i] < .06f, "Calm water did not settle: " + body.name);
            if (height > 0 && !body.name.Contains("observed")) Require(maxY[i]-minY[i] > .04f, "Body did not bob: " + body.name);
            if (current > 0 && !body.moored) Require(travel > .5f, "Free debris did not drift: " + body.name);
            report.AppendLine(name + " | " + body.name + " | bob=" + (maxY[i]-minY[i]).ToString("F3")
                + "m tilt=" + maxTilt[i].ToString("F2") + "deg drift=" + travel.ToString("F2") + "m");
        }
        report.AppendLine("PASS: " + name + " (" + seconds + " simulated seconds, " + bodies.Count + " bodies).");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
