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

public static class BanagoVegetationTestBuilder
{
    const string RootName = "Vegetation Test 100m";
    const string DataPath = "RoadRepairBackup/banago-vegetation-test-data.json";
    public static string Status = "Not built";
    static bool running;

    [MenuItem("Tools/StormSim/Build Satellite Vegetation Test (100m Only)")]
    public static void Build()
    {
        if (running || EditorApplication.isPlaying) throw new InvalidOperationException("Wait for sampling and exit Play mode first.");
        if (GameObject.Find(RootName)) throw new InvalidOperationException("The vegetation review batch already exists. Review or remove that group before rebuilding.");
        var data = JObject.Parse(File.ReadAllText(DataPath));
        var summary = data["summary"];
        if ((int)summary["treeOverlapViolations"] != 0 || (int)summary["grassOverlapViolations"] != 0)
            throw new InvalidOperationException("Source clearance validation failed.");
        var trees = ((JArray)data["trees"]).ToArray();
        var grass = ((JArray)data["grass"]).ToArray();
        var all = trees.Concat(grass).ToArray();
        var geo = GameObject.Find("CesiumGeoreference").GetComponent<CesiumGeoreference>();
        var terrain = GameObject.Find("Cesium World Terrain").GetComponent<Cesium3DTileset>();
        var coordinates = all.Select(t => CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(
            geo.TransformUnityPositionToEarthCenteredEarthFixed(new double3((double)t["x"], 0, (double)t["z"])))).ToArray();
        var task = terrain.SampleHeightMostDetailed(coordinates);
        running = true;
        Status = "Sampling terrain for " + trees.Length + " trees and " + grass.Length + " grass clumps";
        double started = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction poll = null;
        poll = () =>
        {
            if (!task.IsCompleted && EditorApplication.timeSinceStartup - started < 150) return;
            EditorApplication.update -= poll;
            running = false;
            try
            {
                if (!task.IsCompleted) throw new TimeoutException("Terrain sampling timed out; no vegetation placed.");
                if (task.IsFaulted) throw task.Exception;
                var result = task.Result;
                if (result.sampleSuccess.Any(ok => !ok)) throw new InvalidOperationException("A ground sample failed; no vegetation placed.");
                var ground = result.longitudeLatitudeHeightPositions.Select(llh =>
                    (float)geo.TransformEarthCenteredEarthFixedPositionToUnity(CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(llh)).y).ToArray();
                Create(geo, data, trees, grass, ground);
            }
            catch (Exception ex) { Status = "Failed: " + ex.Message; Debug.LogError("BANAGO_VEGETATION: " + Status); }
        };
        EditorApplication.update += poll;
    }

    static Color SourceColor(JToken token)
    {
        return new Color((float)token[0], (float)token[1], (float)token[2], 1);
    }

    static void Create(CesiumGeoreference geo, JObject data, JToken[] trees, JToken[] grass, float[] ground)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/StormSim/Generated/BanagoVegetation.shader");
        if (!shader || !shader.isSupported) throw new InvalidOperationException("Vegetation shader is unavailable.");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backup = "RoadRepairBackup/MAIN_GAME_before_vegetation_" + stamp + ".unity";
        var scene = geo.gameObject.scene;
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
        Capture("RoadRepairBackup/vegetation-before.png", false);
        string folder = "Assets/StormSim/Generated/BanagoVegetationTest_" + stamp;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add satellite vegetation test (100m)");
        var root = new GameObject(RootName);
        root.transform.SetParent(geo.transform, false);
        Undo.RegisterCreatedObjectUndo(root, "Add satellite vegetation test (100m)");
        var info = root.AddComponent<BanagoVegetationTest>();
        info.trees = trees.Length; info.grassClumps = grass.Length; info.sceneBackup = backup;
        info.sourceData = folder + "/PlacementSource.json";
        var treeRoot = new GameObject("Trees - satellite canopy mask"); treeRoot.transform.SetParent(root.transform, false);
        var grassRoot = new GameObject("Grass - satellite ground mask"); grassRoot.transform.SetParent(root.transform, false);
        var material = new Material(shader) { name = "Satellite vegetation colors", enableInstancing = true };
        string meshPath = folder + "/VegetationMeshes.asset";
        bool firstMesh = true;
        Action<Mesh> saveMesh = mesh => { if (firstMesh) { AssetDatabase.CreateAsset(mesh, meshPath); firstMesh = false; } else AssetDatabase.AddObjectToAsset(mesh, meshPath); };
        int totalVertices = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            AssetDatabase.CreateAsset(material, folder + "/VegetationColors.mat");
            for (int i = 0; i < trees.Length; i++)
            {
                var t = trees[i];
                var mesh = Tree(t, i);
                saveMesh(mesh); totalVertices += mesh.vertexCount;
                var go = new GameObject("Canopy Tree " + (i + 1).ToString("D2"));
                go.transform.SetParent(treeRoot.transform, false);
                go.transform.localPosition = new Vector3((float)t["x"], ground[i] - .04f, (float)t["z"]);
                AddMesh(go, mesh, material);
                var collider = go.AddComponent<CapsuleCollider>();
                collider.radius = .18f; collider.height = (float)t["height"] * .52f;
                collider.center = Vector3.up * collider.height * .5f;
            }
            var batches = new Dictionary<string, MeshParts>();
            for (int i = 0; i < grass.Length; i++)
            {
                var g = grass[i];
                float x = (float)g["x"], z = (float)g["z"];
                string key = Mathf.FloorToInt((x-190)/20) + "_" + Mathf.FloorToInt((z-450)/20);
                if (!batches.ContainsKey(key)) batches[key] = new MeshParts();
                Grass(batches[key], g, ground[trees.Length + i], i);
            }
            foreach (var batch in batches)
            {
                var mesh = batch.Value.ToMesh("Grass patch " + batch.Key);
                saveMesh(mesh); totalVertices += mesh.vertexCount;
                var go = new GameObject(mesh.name); go.transform.SetParent(grassRoot.transform, false);
                AddMesh(go, mesh, material);
            }
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally { AssetDatabase.StopAssetEditing(); }
        root.AddComponent<CesiumGlobeAnchor>();
        File.Copy(DataPath, info.sourceData);
        File.Copy("RoadRepairBackup/vegetation-placement-mask.png", folder + "/PlacementMask.png");
        File.WriteAllText(folder + "/README.txt", "100 x 100 m vegetation review only. Cesium local x=190..290, z=450..550.\n" +
            trees.Length + " broadleaf tree models; " + grass.Length + " grass clumps, combined in small mesh batches.\n" +
            "Tree positions: manually traced satellite crown envelopes, image green/brightness/texture confidence maxima. Grass density and color follow visible open vegetation. No vegetation on bare surfaces.\n" +
            "Whole model footprints clear live house roofs by at least 1.5m and road surfaces by 1m. Includes moved generated houses and existing OSM building geometry. Crown radius checks precede creation.\n" +
            "Every tree and grass clump received a successful Cesium detailed ground-height sample.\n" +
            "Tree species, trunk locations and heights are visual approximations; canopy clusters are inferred from imagery. No wind displacement outside clearance bounds.\n" +
            "Hide/remove CesiumGeoreference/Vegetation Test 100m or use Unity Undo. Scene backup: " + backup + "\n");
        AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(meshPath);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root;
        Frame();
        Status = "Saved 100m test: " + trees.Length + " trees, " + grass.Length + " grass clumps; " + totalVertices + " vertices. All terrain samples succeeded. Backup: " + backup;
        Debug.Log("BANAGO_VEGETATION: " + Status);
    }

    static void AddMesh(GameObject go, Mesh mesh, Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Mesh Tree(JToken t, int index)
    {
        var rng = new System.Random(unchecked((int)(long)t["seed"]));
        float radius = (float)t["radius"], height = (float)t["height"];
        Color leaf = SourceColor(t["color"]), bark = new Color(.28f, .23f, .17f);
        var parts = new MeshParts();
        Cylinder(parts, new Vector3(0,-.16f,0), new Vector3(.09f,height*.62f,-.04f), .17f, .075f, bark);
        // Overlapping, irregular broadleaf lobes create a rounded crown, not a conifer.
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2 / 7 + (float)rng.NextDouble()*.3f;
            float orbit = i >= 7 ? .16f : .49f;
            var c = new Vector3(Mathf.Cos(a)*radius*orbit, height*(i>=7 ? .81f : .60f+.13f*(float)rng.NextDouble()), Mathf.Sin(a)*radius*orbit);
            var scale = new Vector3(radius*(.50f+.07f*(float)rng.NextDouble()), height*(i>=7 ? .20f : .18f), radius*(.46f+.10f*(float)rng.NextDouble()));
            Cylinder(parts, new Vector3(0,height*.39f,0), c, .065f, .025f, bark*.9f);
            Crown(parts, c, scale, leaf*(.90f+.18f*(float)rng.NextDouble()), rng);
        }
        // Normalize the ENTIRE visible mesh inside the validated crown envelope.
        float maxRadius = parts.vertices.Max(v => new Vector2(v.x,v.z).magnitude);
        float maxY = parts.vertices.Max(v => v.y);
        for (int i = 0; i < parts.vertices.Count; i++)
        {
            var v = parts.vertices[i]; v.x *= radius*.975f/maxRadius; v.z *= radius*.975f/maxRadius;
            if (v.y>0) v.y *= height/maxY;
            parts.vertices[i]=v;
        }
        return parts.ToMesh("Broadleaf canopy " + (index+1).ToString("D2"));
    }

    static void Crown(MeshParts parts, Vector3 center, Vector3 scale, Color color, System.Random rng)
    {
        const int rings=8, segments=12;
        var grid=new Vector3[rings+1,segments];
        for(int y=0;y<=rings;y++) for(int x=0;x<segments;x++)
        {
            float theta=Mathf.PI*y/rings, phi=Mathf.PI*2*x/segments;
            float noise=.90f+.20f*(float)rng.NextDouble();
            grid[y,x]=center+Vector3.Scale(new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi))*noise,scale);
        }
        for(int y=0;y<rings;y++) for(int x=0;x<segments;x++)
        {
            int next=(x+1)%segments;
            var a=grid[y,x];var b=grid[y+1,x];var c=grid[y+1,next];var d=grid[y,next];
            var tint=color*(.94f+.12f*(float)rng.NextDouble());
            parts.Face(a,b,c,tint,(a+b+c)/3-center);
            parts.Face(a,c,d,tint,(a+c+d)/3-center);
        }
    }

    static void Cylinder(MeshParts parts, Vector3 start, Vector3 end, float lower, float upper, Color color)
    {
        Vector3 axis=(end-start).normalized;
        Vector3 right=Vector3.Cross(axis, Mathf.Abs(axis.y)>.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 forward=Vector3.Cross(axis,right).normalized;
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
            Vector3 da=right*Mathf.Cos(a)+forward*Mathf.Sin(a),db=right*Mathf.Cos(b)+forward*Mathf.Sin(b);
            Vector3 p=start+da*lower,q=start+db*lower,r=end+db*upper,s=end+da*upper;
            parts.Face(p,q,r,color,da+db);parts.Face(p,r,s,color,da+db);
        }
    }

    static void Grass(MeshParts parts, JToken g, float y, int index)
    {
        var rng=new System.Random(index*3719+41);
        Vector3 center=new Vector3((float)g["x"], y-.012f, (float)g["z"]);
        float h=(float)g["height"],rotation=(float)g["yaw"]*Mathf.Deg2Rad;
        Color baseColor=SourceColor(g["color"]);
        for(int i=0;i<7;i++)
        {
            float a=rotation+i*2.399963f;
            var outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            var side=new Vector3(-outward.z,0,outward.x);
            float spread=.08f+.12f*(float)rng.NextDouble(), width=.022f+.019f*(float)rng.NextDouble();
            Vector3 bottom=center+outward*spread;
            float bladeHeight=h*(.65f+.35f*(float)rng.NextDouble());
            Vector3 mid=bottom+Vector3.up*bladeHeight*.60f+outward*.05f;
            Vector3 tip=bottom+Vector3.up*bladeHeight+outward*.12f;
            Color tint=baseColor*(.85f+.28f*(float)rng.NextDouble());
            parts.Face(bottom-side*width,bottom+side*width,mid+side*width*.55f,tint,Vector3.up);
            parts.Face(bottom-side*width,mid+side*width*.55f,mid-side*width*.55f,tint,Vector3.up);
            parts.Face(mid-side*width*.55f,mid+side*width*.55f,tip,tint*1.07f,Vector3.up);
        }
    }

    sealed class MeshParts
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color32> colors=new List<Color32>();
        readonly List<int> indices=new List<int>();
        public void Face(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 expected)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),expected)<0) {var tmp=b;b=c;c=tmp;}
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);
        }
        public Mesh ToMesh(string name)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }

    public static void Frame()
    {
        var geo=GameObject.Find("CesiumGeoreference").transform;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAtDirect(geo.TransformPoint(new Vector3(240,0,500)),Quaternion.Euler(58,320,0),90);
        SceneView.RepaintAll();
    }

    public static void CaptureReview() { Capture("RoadRepairBackup/vegetation-review.png",false); Capture("RoadRepairBackup/vegetation-top.png",true); }

    static void Capture(string path,bool top)
    {
        var geo=GameObject.Find("CesiumGeoreference").transform;
        var target=geo.TransformPoint(new Vector3(240,0,500));
        var cameraObject=new GameObject("Vegetation review camera"){hideFlags=HideFlags.HideAndDontSave};
        var cam=cameraObject.AddComponent<Camera>();
        var rt=new RenderTexture(1500,1100,24);var old=RenderTexture.active;
        Texture2D image=null;
        try
        {
            if(top) {cam.transform.position=target+Vector3.up*220;cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographic=true;cam.orthographicSize=65;}
            else {cam.transform.position=target+new Vector3(70,110,-92);cam.transform.LookAt(target);cam.fieldOfView=48;}
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.38f,.41f,.44f);
            cam.nearClipPlane=.3f;cam.farClipPlane=3000;cam.targetTexture=rt;cam.Render();
            RenderTexture.active=rt;image=new Texture2D(1500,1100,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1500,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
