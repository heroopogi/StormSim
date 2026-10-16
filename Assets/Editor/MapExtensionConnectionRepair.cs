using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Fits the surveyed northern extension edge to the original map, retaining source assets.</summary>
public static class MapExtensionConnectionRepair
{
    const string Backup = "RoadRepairBackup/MapConnection_20260921";
    const string Folder = "Assets/StormSim/Generated/MapConnection_20260921";
    const string Root = "Original map - extension connection repair";
    [Serializable] public class Vertex { public Vector3 position, weights; public int[] source; }
    [Serializable] public class Patch { public string path, asset; public int sourceVertexCount; public Vector3 sourceFirst; public Vertex[] vertices; }
    [Serializable] public class Plan { public Patch[] meshes; }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    // Explicit one-shot request survives an editor-tool disconnect during domain reload.
    [InitializeOnLoadMethod]
    static void RegisterRequest() { EditorApplication.update -= RunRequested; EditorApplication.update += RunRequested; }
    static void RunRequested()
    {
        const string request = "Temp/map-connection-repair.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || !File.Exists(request)) return;
        File.Move(request, request + "." + DateTime.UtcNow.Ticks + ".started");
        try
        {
            if (!GameObject.Find(Root)) Apply();
            var review = GameObject.Find(Root).GetComponent<BanagoReferenceAreasReview>();
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Backup + "/plan.json"));
            if (review.swaps.Length != plan.meshes.Length) throw new InvalidOperationException("Incomplete repair.");
            int colliders = 0; float error = 0;
            for (int n = 0; n < review.swaps.Length; n++)
            {
                var s = review.swaps[n];
                if (s.target.sharedMesh != s.after) throw new InvalidOperationException("Mesh swap was not retained.");
                var vs = s.after.vertices;
                if (vs.Length != plan.meshes[n].vertices.Length) throw new InvalidOperationException("Vertex count mismatch.");
                for (int i = 0; i < vs.Length; i++) error = Mathf.Max(error, Vector3.Distance(s.target.transform.TransformPoint(vs[i]), plan.meshes[n].vertices[i].position));
                var c = s.target.GetComponent<MeshCollider>();
                if (c) { if (c.sharedMesh != s.after) throw new InvalidOperationException("Collider mismatch."); colliders++; }
            }
            if (error > .001f) throw new InvalidOperationException("Live geometry differs from the validated plan.");
            Physics.SyncTransforms();
            Capture(Backup + "/after.png", new Vector3(130,-2,87), 210, 65);
            Capture(Backup + "/road-join-after.png", new Vector3(1,-2.3f,89), 18, 48);
            var scene = EditorSceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MAIN_GAME.");
            File.WriteAllText(Backup + "/applied-validation.txt", "PASS\nSaved: " + scene.path + "\nMeshes: " + review.swaps.Length + "\nMatching collision meshes: " + colliders + "\nMaximum live vertex error: " + error + "m\nSource meshes and materials retained.\n");
            var sv = SceneView.lastActiveSceneView;
            if (sv) sv.LookAt(new Vector3(1,-2,89), Quaternion.Euler(48,0,0), 35, true, true);
        }
        catch (Exception ex) { File.WriteAllText(Backup + "/apply-error.txt", ex.ToString()); Debug.LogException(ex); }
    }

    static void Capture(string path, Vector3 target, float size, float pitch)
    {
        var go = new GameObject("Temporary map connection review camera");
        var rt = RenderTexture.GetTemporary(1500,900,24); var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            var camera = go.AddComponent<Camera>(); camera.transform.rotation = Quaternion.Euler(pitch,0,0);
            camera.transform.position = target-camera.transform.forward*Mathf.Max(80,size*2.05f);
            camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .3f; camera.farClipPlane = 2000;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image = new Texture2D(1500,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1500,900),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
            camera.targetTexture = null;
        }
        finally { RenderTexture.active = previous; if(image) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go); RenderTexture.ReleaseTemporary(rt); }
    }

    [MenuItem("Tools/StormSim/Apply surveyed map connection repair")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MAIN_GAME.unity")
            throw new InvalidOperationException("MAIN_GAME must be open in Edit mode.");
        if (GameObject.Find(Root)) throw new InvalidOperationException("Repair is already applied.");
        if (!File.Exists(Backup + "/MAIN_GAME.before.unity")) throw new IOException("Live-scene backup is missing.");
        var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Backup + "/plan.json"));
        if (plan?.meshes == null || plan.meshes.Length == 0) throw new InvalidDataException("Empty repair plan.");
        var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        var targets = new List<MeshFilter>();
        foreach (var p in plan.meshes)
        {
            var matches = filters.Where(f => PathOf(f.transform) == p.path && AssetDatabase.GetAssetPath(f.sharedMesh) == p.asset &&
                f.sharedMesh.vertexCount == p.sourceVertexCount &&
                Vector3.Distance(f.transform.TransformPoint(f.sharedMesh.vertices[0]),p.sourceFirst) < .001f).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Ambiguous or changed survey target: " + p.path);
            var f = matches[0];
            if (!f.sharedMesh.isReadable || f.sharedMesh.subMeshCount != 1) throw new InvalidOperationException("Unsupported mesh: " + p.path);
            if (p.vertices.Length % 3 != 0) throw new InvalidDataException("Invalid triangle list.");
            foreach (var v in p.vertices)
                if (v.source.Length != 3 || v.source.Any(i => i < 0 || i >= f.sharedMesh.vertexCount) ||
                    !float.IsFinite(v.position.x) || !float.IsFinite(v.position.y) || !float.IsFinite(v.position.z))
                    throw new InvalidDataException("Invalid vertex in " + p.path);
            targets.Add(f);
        }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var swaps = new List<BanagoReferenceAreasReview.Swap>();
        for (int n = 0; n < plan.meshes.Length; n++)
        {
            var p = plan.meshes[n]; var f = targets[n]; var old = f.sharedMesh;
            var colors = old.colors; var normals = old.normals; var uv = old.uv; var uv2 = old.uv2;
            var vertices = new Vector3[p.vertices.Length]; var nc = new Color[vertices.Length];
            var nn = new Vector3[vertices.Length]; var nu = new Vector2[vertices.Length]; var nu2 = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = p.vertices[i]; var ids = v.source; var w = v.weights;
                vertices[i] = f.transform.InverseTransformPoint(v.position);
                if (colors.Length == old.vertexCount) nc[i] = colors[ids[0]] * w.x + colors[ids[1]] * w.y + colors[ids[2]] * w.z;
                if (normals.Length == old.vertexCount) nn[i] = (normals[ids[0]] * w.x + normals[ids[1]] * w.y + normals[ids[2]] * w.z).normalized;
                if (uv.Length == old.vertexCount) nu[i] = uv[ids[0]] * w.x + uv[ids[1]] * w.y + uv[ids[2]] * w.z;
                if (uv2.Length == old.vertexCount) nu2[i] = uv2[ids[0]] * w.x + uv2[ids[1]] * w.y + uv2[ids[2]] * w.z;
            }
            var mesh = new Mesh { name = old.name + " - connected", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.triangles = Enumerable.Range(0, vertices.Length).ToArray();
            if (colors.Length == old.vertexCount) mesh.colors = nc;
            if (uv.Length == old.vertexCount) mesh.uv = nu;
            if (uv2.Length == old.vertexCount) mesh.uv2 = nu2;
            if (normals.Length == old.vertexCount) mesh.normals = nn; else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/Connection " + n.ToString("000") + ".asset");
            var r = f.GetComponent<Renderer>(); var c = f.GetComponent<MeshCollider>();
            swaps.Add(new BanagoReferenceAreasReview.Swap { target = f, before = old, after = mesh,
                materialsBefore = r.sharedMaterials, materialsAfter = r.sharedMaterials, colliderBefore = c ? c.sharedMesh : null });
        }
        var root = new GameObject(Root);
        Undo.RegisterCreatedObjectUndo(root, "Repair original map extension connection");
        var review = root.AddComponent<BanagoReferenceAreasReview>();
        review.backupPath = Backup + "/MAIN_GAME.before.unity";
        review.notes = "Closed the 0.3333m terrain gap at z=89, matched northern terrain edge heights, tapered nine road connections, and trimmed overlapping original road ends. Changes stay within the final extension tiles (z>=80) and original road tips. Materials and source mesh assets are retained. Disable this object to restore the previous meshes and colliders. Coastal outline beyond the shared land edge is preserved to keep the creek mouth open.";
        review.swaps = swaps.ToArray();
        foreach (var swap in swaps)
        {
            Undo.RecordObject(swap.target, "Connect mesh");
            var collider = swap.target.GetComponent<MeshCollider>();
            if (collider) Undo.RecordObject(collider, "Connect collision mesh");
        }
        review.Apply();
        EditorUtility.SetDirty(review);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        // Leave the main scene unsaved until numerical and visual validation pass.
        File.WriteAllText(Folder + "/README.txt", review.notes + "\nBackup: " + review.backupPath);
        Selection.activeGameObject = root;
        SceneView.RepaintAll();
        Debug.Log("Applied reversible map connection repair to " + swaps.Count + " meshes.", root);
    }
}
