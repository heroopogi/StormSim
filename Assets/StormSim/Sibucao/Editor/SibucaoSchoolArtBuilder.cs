using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoSchoolArtBuilder
    {
        const string Folder = "Assets/StormSim/Sibucao/Art/HiggsfieldSchool";
        public static string Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (GameObject.Find("Higgsfield Philippine school classrooms")) return "Already installed; existing placement preserved.";
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PhilippineSchool.glb");
            if (!imported) throw new Exception("Higgsfield GLB missing");
            var source = UnityEngine.Object.Instantiate(imported);
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var prefabRoot = new GameObject("Philippine public school classroom");
            foreach (var group in source.GetComponentsInChildren<MeshRenderer>().GroupBy(r => r.sharedMaterial))
            {
                var mesh = new Mesh { name = group.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh,
                    transform = source.transform.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray());
                AssetDatabase.CreateAsset(mesh, Folder + "/School mesh " + group.Key.name.Replace('/', '-') + ".asset");
                var part = new GameObject(group.Key.name);
                part.transform.SetParent(prefabRoot.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                string n = filter.name;
                bool solid = new[] { "Raised school slab", "Rear classroom wall", "Masonry end", "Front dado", "Window lintel",
                    "Window pier", "Recessed window backing", "Classroom door", "Veranda concrete column", "Low veranda parapet",
                    "Central entrance step", "Veranda bench seat" }.Any(prefix => n.StartsWith(prefix, StringComparison.Ordinal));
                if (!solid) continue;
                var part = new GameObject(n + " collision");
                part.transform.SetParent(prefabRoot.transform, false);
                part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                part.transform.localScale = filter.transform.lossyScale;
                var collider = part.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, Folder + "/PhilippineSchool.prefab");
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            UnityEngine.Object.DestroyImmediate(source);
            var root = new GameObject("Higgsfield Philippine school classrooms");
            Undo.RegisterCreatedObjectUndo(root, "Philippine school classroom upgrade");
            var center = new Vector3(1.76f, -.43f, 677.83f);
            var along = new Vector3(.914f, 0, .406f).normalized;
            foreach (int side in new[] { -1, 1 })
            {
                var block = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                block.name = side < 0 ? "Philippine classroom A" : "Philippine classroom B";
                block.transform.SetParent(root.transform, false);
                block.transform.SetPositionAndRotation(center + along * (side * 18.3f), Quaternion.LookRotation(-side * along));
            }
            foreach (string name in new[] { "Classroom block A - left of gym", "Classroom block B - right of gym", "Classroom A pitched roof", "Classroom B pitched roof" })
            {
                var old = GameObject.Find(name);
                if (!old) throw new Exception("Original classroom missing: " + name);
                Undo.RecordObject(old, "Preserve original school model");
                old.SetActive(false);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            return "Two Higgsfield classroom instances installed; original four meshes preserved inactive; 10 material batches per building.";
        }
    }
}
