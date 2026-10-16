using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using StormSim.Preparedness;

public static class CoastalAreaSceneryCleanup
{
    // These interiors use Sibucao footprints, not the MAIN_GAME buildings.
    // Their translated floors and furniture otherwise appear offshore.
    public static int RemoveCopiedInteriors(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var interiors = roots.FirstOrDefault(r => r.name == "Spawn neighborhood - accessible family homes");
        int count = interiors ? interiors.transform.childCount : 0;
        var families = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "Spawn neighborhood families - fictional placeholders");
        if (families)
        {
            foreach (var session in roots.SelectMany(r => r.GetComponentsInChildren<PreparednessSession>(true)))
                session.items = session.items.Where(item => item && !item.transform.IsChildOf(families)).ToArray();
            Object.DestroyImmediate(families.gameObject);
        }
        if (interiors) Object.DestroyImmediate(interiors);
        return count;
    }
}
