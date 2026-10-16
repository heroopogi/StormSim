using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoCarEnclosureBuilder
    {
        public static string Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring.");
            var station = UnityEngine.Object.FindObjectsByType<PhysicalDecisionStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(s => !s.supplementary && s.index == 1);
            var enclosure = station.GetComponent<SibucaoCarEnclosure>();
            if (!enclosure) enclosure = Undo.AddComponent<SibucaoCarEnclosure>(station.gameObject);
            enclosure.metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/DiegeticHazards/Oxidised galvanized roofing.mat");
            enclosure.timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/DiegeticHazards/Storm-soaked coconut timber.mat");
            for (int car = 0; car < 2; car++)
            {
                float x = (car == 0 ? -1 : 1) * SibucaoCarEnclosure.CarOffset;
                foreach (string name in new[] { "Stalled car " + (car + 1), "Car " + car + " chassis collision", "Car " + car + " cabin collision" })
                {
                    var part = station.transform.Find(name);
                    if (!part) throw new InvalidOperationException("Missing " + name);
                    var at = part.localPosition; at.x = x; part.localPosition = at;
                }
            }
            int homes = UnityEngine.Object.FindFirstObjectByType<SibucaoSession>().homes.Length;
            foreach (var branch in station.branches)
            {
                Vector3[] points;
                if (branch.choiceIndex == 2)
                    points = new[] { new Vector3(1.34f,0,-1.4f),new Vector3(1.34f,1.02f,-1),new Vector3(1.34f,1.02f,.45f),
                        new Vector3(1.34f,1.58f,.85f),new Vector3(1.34f,1.58f,2.9f),new Vector3(1.34f,1.58f,3.9f),
                        new Vector3(1.34f,.05f,4.2f),new Vector3(0,0,5.5f) };
                else if (branch.choiceIndex == 0)
                    points = new[] { new Vector3(-.8f,0,-1.4f),new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(0,0,3.8f),new Vector3(0,0,6) };
                else
                    points = new[] { new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(0,0,3.8f),new Vector3(0,0,5) };
                var local = points.Select(p => branch.transform.InverseTransformPoint(station.transform.TransformPoint(p))).ToArray();
                branch.layouts = Enumerable.Range(0, homes).Select(h => new PhysicalPathVariant { home = h, points = (Vector3[])local.Clone() }).ToArray();
                if (branch.path != null) foreach (var point in branch.path) if (point) UnityEngine.Object.DestroyImmediate(point.gameObject);
                branch.path = local.Select((p, i) => { var t = new GameObject("Physical step " + i).transform; t.SetParent(branch.transform, false); t.localPosition = p; return t; }).ToArray();
                if (branch.destination) branch.destination.transform.position = branch.path.Last().position + Vector3.up * 1.5f;
                if (branch.actionPoint) branch.actionPoint.position = station.transform.TransformPoint(points[0] + Vector3.up * .8f);
                if (branch.choiceIndex == 0)
                {
                    branch.name = "C0 - Clear the blocked car passage";
                    branch.verb = "Clear the blocked car passage";
                    // Presentation text only; all authored numerical values and consequences remain intact.
                    branch.Choice.label = "Clear the blocked car passage";
                    branch.Choice.outcome = "You cleared the wedged debris and passed between the stalled cars.";
                    EditorUtility.SetDirty(branch.Choice);
                }
                EditorUtility.SetDirty(branch);
            }
            enclosure.Rebuild();
            var surroundings = station.GetComponent<SibucaoObstacleSurroundings>();
            if (surroundings) surroundings.Rebuild();
            EditorUtility.SetDirty(enclosure);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            return "Enclosed car passage installed for " + homes + " home layouts and four physical choices.";
        }

        public static string Validate()
        {
            var enclosure = UnityEngine.Object.FindFirstObjectByType<SibucaoCarEnclosure>();
            if (!enclosure) throw new Exception("Car enclosure missing");
            var station = enclosure.GetComponent<PhysicalDecisionStation>();
            Physics.SyncTransforms();
            foreach (var branch in station.branches)
                foreach (var layout in branch.layouts)
                {
                    if (layout.points.Length < 4) throw new Exception("Car route missing");
                    foreach (var p in layout.points)
                    {
                        var at = station.transform.InverseTransformPoint(branch.transform.TransformPoint(p));
                        if (Mathf.Abs(at.x) > 1.5f || at.z < -1.5f || at.z > 6.1f) throw new Exception("Route leaves car enclosure");
                    }
                }
            var root = station.transform.Find("Enclosed stalled-car passage");
            var barriers = root.GetComponentsInChildren<BoxCollider>();
            for (int side = -1; side <= 1; side += 2)
                for (float z = -10.9f; z <= 8.9f; z += .2f)
                {
                    var ray = new Ray(station.transform.TransformPoint(new Vector3(0, 2.5f, z)), station.transform.right * side);
                    if (!barriers.Any(c => c.Raycast(ray, out _, 2.7f))) throw new Exception("Gap in car-side enclosure");
                }
            // The ordinary player fits through the central gap without passing through either chassis.
            for (float z = -.6f; z <= 3.5f; z += .2f)
            {
                var bottom = station.transform.TransformPoint(new Vector3(0, .4f, z));
                var top = bottom + Vector3.up;
                foreach (var hit in Physics.OverlapCapsule(bottom, top, .28f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.name.Contains("chassis collision") || hit.transform.IsChildOf(root)) throw new Exception("Central passage blocked");
            }
            return "PASS: all home paths inside enclosure; continuous side collision; central capsule clearance.";
        }
    }
}
