using UnityEditor;
using UnityEngine;

/// <summary>Paint the road's red vertex channel without modifying its surveyed geometry.</summary>
public sealed class BanagoStreetWetMudPainter : EditorWindow
{
    bool painting;
    float radius = 2.5f, amount = .85f, opacity = .30f;

    [MenuItem("Tools/StormSim/Paint Road Wet Mud")]
    static void Open() => GetWindow<BanagoStreetWetMudPainter>("Road wet mud");

    void OnEnable() => SceneView.duringSceneGui += Paint;
    void OnDisable() { SceneView.duringSceneGui -= Paint; AssetDatabase.SaveAssets(); }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Select a refined road mesh", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click or drag on the selected road in Scene view to paint wet mud. Hold Shift to dry it. Geometry and the paved-school mask stay unchanged.", MessageType.Info);
        painting = EditorGUILayout.Toggle("Enable brush", painting);
        radius = EditorGUILayout.Slider("Radius (metres)", radius, .3f, 8f);
        amount = EditorGUILayout.Slider("Wetness", amount, 0, 1);
        opacity = EditorGUILayout.Slider("Strength", opacity, .05f, 1);
        if (GUILayout.Button("Save painted road")) AssetDatabase.SaveAssets();
    }

    void Paint(SceneView view)
    {
        if (!painting || Application.isPlaying || !Selection.activeGameObject) return;
        var filter = Selection.activeGameObject.GetComponent<MeshFilter>();
        var collider = Selection.activeGameObject.GetComponent<MeshCollider>();
        if (!filter || !collider || !filter.sharedMesh) return;
        var mesh = filter.sharedMesh;
        if (!AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/StormSim/StreetReference/")) return;
        var e = Event.current;
        if (e.alt) return;
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!collider.Raycast(ray, out var hit, 1500)) return;
        Handles.color = e.shift ? new Color(.8f, .65f, .3f, .8f) : new Color(.2f, .5f, .8f, .8f);
        Handles.DrawWireDisc(hit.point + hit.normal * .025f, hit.normal, radius);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            Undo.RecordObject(mesh, "Paint road wet mud");
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            if (colors.Length != vertices.Length) return;
            for (int i = 0; i < vertices.Length; i++)
            {
                float distance = Vector3.Distance(filter.transform.TransformPoint(vertices[i]), hit.point);
                if (distance > radius) continue;
                float falloff = Mathf.SmoothStep(1, 0, distance / radius);
                colors[i].r = Mathf.Lerp(colors[i].r, e.shift ? 0 : amount, opacity * falloff);
            }
            mesh.colors = colors;
            EditorUtility.SetDirty(mesh);
            e.Use();
        }
        view.Repaint();
    }
}
