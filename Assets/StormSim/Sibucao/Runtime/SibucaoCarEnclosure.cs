using UnityEngine;

namespace StormSim.Sibucao
{
    // Station-local geometry follows every home layout, including restored runs.
    [DisallowMultipleComponent]
    public sealed class SibucaoCarEnclosure : MonoBehaviour
    {
        public Material metal, timber;
        [SerializeField] Transform enclosure;
        public const float CarOffset = 1.34f;
        public const float WallX = 2.5f;
        public Vector3 WorkPoint(int phase)
        {
            var at = transform.TransformPoint(new Vector3(0, 0, -8.4f + phase * 2.7f));
            var hits = Physics.RaycastAll(at + Vector3.up * 3, Vector3.down, 7, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.normal.y < .7f ||
                    hit.collider.GetComponentInParent<SibucaoPlayer>() || hit.collider.GetComponentInParent<PhysicalGrabbable>()) continue;
                return hit.point + Vector3.up * .03f;
            }
            return at;
        }

        public void Rebuild()
        {
            if (enclosure)
            {
                enclosure.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(enclosure.gameObject); else DestroyImmediate(enclosure.gameObject);
            }
            enclosure = new GameObject("Enclosed stalled-car passage").transform;
            enclosure.SetParent(transform, false);
            bool threeWays = GetComponentInChildren<SibucaoRouteSection>(true);
            for (int side = -1; side <= 1; side += 2)
            {
                // Overlapping panels and buried bases prevent shoulder/under-wall shortcuts.
                for (int panel = 0; panel < 10; panel++)
                {
                    float z = -10 + panel * 2;
                    if (threeWays && (z < -5 || z > 5)) continue;
                    Box("Continuous galvanized roadside barrier", new Vector3(side * WallX, threeWays ? .45f : 1.45f, z),
                        new Vector3(.24f, threeWays ? .75f : 5.3f, 2.05f), metal);
                    Box("Fence support", new Vector3(side * (WallX + .17f), threeWays ? .45f : 1.35f, z - .9f),
                        new Vector3(.18f, threeWays ? .75f : 5.1f, .18f), timber);
                    for (int rib = 0; rib < 8; rib++)
                        Box("Galvanized rib", new Vector3(side * (WallX - .13f), threeWays ? .45f : 1.45f, z - .875f + rib * .25f),
                            new Vector3(.035f, threeWays ? .75f : 5.3f, .045f), metal, false);
                }
                // Narrow returns at the cars close the residual space beside the chassis.
                Box("Car-side blockage", new Vector3(side * 2.39f, threeWays ? .45f : 1.45f, 1.4f),
                    new Vector3(.25f, threeWays ? .75f : 5.3f, 4.1f), metal);
            }
        }

        void Box(string label, Vector3 at, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(enclosure, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
        }
    }
}
