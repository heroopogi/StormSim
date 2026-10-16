using System;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoSurroundingsChecks
    {
        [MenuItem("StormSim/Validation/Roadside surroundings B1-B5")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            int stations = 0, pockets = 0;
            foreach (var dressing in UnityEngine.Object.FindObjectsByType<SibucaoObstacleSurroundings>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var station = dressing.GetComponent<PhysicalDecisionStation>();
                if (station.supplementary || station.index < 0 || station.index > 4) throw new Exception("Surroundings installed outside B1-B5");
                var root = dressing.transform.Find("Storm debris - roadside pockets");
                if (!root || dressing.PocketCount == 0) throw new Exception("No roadside debris at " + station.name);
                foreach (Transform pocket in root)
                {
                    float clearance = pocket.localScale.x < .6f ? 3.8f : 4.5f;
                    if (!dressing.ClearOfRoutes(pocket.position, clearance)) throw new Exception("Debris obstructs authored route: " + station.name);
                    foreach (var collider in pocket.GetComponentsInChildren<Collider>())
                        if (!collider.enabled || collider.isTrigger) throw new Exception("Debris has no solid collision");
                    foreach (var renderer in pocket.GetComponentsInChildren<Renderer>())
                        if (!renderer.sharedMaterial) throw new Exception("Debris material missing");
                    pockets++;
                }
                stations++;
            }
            if (stations != 5) throw new Exception("Expected surroundings on five main stations");
            return "PASS: " + stations + " stations, " + pockets + " solid roadside pockets; authored-route clearance and materials.";
        }
    }
}
