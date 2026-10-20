using UnityEngine;

namespace StormSim.Sibucao
{
    // Physical scenery only: never selects a choice, applies damage, or awards progress.
    [DisallowMultipleComponent]
    public sealed class SibucaoObstacleSurroundings : MonoBehaviour
    {
        public Material timber, roofing, barrel;
        [SerializeField] Transform generated;
        [SerializeField] int layoutSignature;
        public int PocketCount => generated ? generated.childCount : 0;
        PhysicalDecisionStation Station => GetComponent<PhysicalDecisionStation>();

        void LateUpdate()
        {
            var s = SibucaoSession.Instance;
            if (!s || s.Mode != SibucaoMode.Playing) return;
            if (Signature() != layoutSignature || !generated) Rebuild();
        }

        int Signature()
        {
            unchecked
            {
                int hash = transform.position.GetHashCode();
                var station = Station;
                if (station && station.branches != null)
                    foreach (var branch in station.branches)
                        if (branch && branch.path != null)
                            foreach (var point in branch.path)
                                if (point) hash = hash * 31 + point.position.GetHashCode();
                return hash;
            }
        }

        public void Rebuild()
        {
            var station = Station;
            if (!station || station.supplementary || station.index > 4) return;
            if (generated)
            {
                generated.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generated.gameObject);
                else DestroyImmediate(generated.gameObject);
            }
            generated = new GameObject("Storm debris - roadside pockets").transform;
            generated.SetParent(transform, false);
            Physics.SyncTransforms();
            // Separated islands leave escape gaps, not an invisible boundary around the task.
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 5; row++)
                {
                    var local = new Vector3(side * (row % 2 == 0 ? 7.5f : 11), 0, (row - 2) * 5.5f);
                    var at = transform.TransformPoint(local);
                    if (!FindGround(ref at) || !ClearOfRoutes(at, 4.5f)) continue;
                    // Do not bury props in houses, existing obstacles, or the player's body.
                    if (Physics.CheckBox(at + Vector3.up * .9f, new Vector3(2, .75f, 1.6f),
                        transform.rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var pocket = new GameObject("Roadside debris " + side + " " + row).transform;
                    pocket.SetParent(generated, false);
                    pocket.SetPositionAndRotation(at, transform.rotation * Quaternion.Euler(0, side * 12, 0));
                    if ((row + station.index) % 3 == 0) FallenFence(pocket);
                    else if ((row + station.index) % 3 == 1) RoofingPile(pocket);
                    else FallenTimber(pocket);
                }
            // Dense streets need smaller fragments fitted between existing buildings.
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 9 && PocketCount < 8; row++)
                    for (int offset = 0; offset < 4 && PocketCount < 8; offset++)
                    {
                        var at = transform.TransformPoint(new Vector3(side * (5 + offset * 2.5f), 0, (row - 4) * 3));
                        if (!FindGround(ref at) || !ClearOfRoutes(at, 3.8f)) continue;
                        bool separated = true;
                        foreach (Transform other in generated)
                            if (FlatDistance(at, other.position) < 4) { separated = false; break; }
                        if (!separated || Physics.CheckBox(at + Vector3.up * .55f, new Vector3(1.15f, .4f, .9f),
                            transform.rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var pocket = new GameObject("Scattered roadside wreckage " + side + " " + row).transform;
                        pocket.SetParent(generated, false);
                        pocket.SetPositionAndRotation(at, transform.rotation);
                        pocket.localScale = Vector3.one * .55f;
                        if ((row + offset) % 2 == 0) FallenFence(pocket); else RoofingPile(pocket);
                        Physics.SyncTransforms();
                    }
            // Very dense junctions can consume every close roadside pocket after
            // route fitting. Keep a few small, solid storm remnants farther out so
            // the setting does not become bare, while preserving every playable lane.
            for (int side = -1; side <= 1 && PocketCount < 4; side += 2)
                for (int lane = 0; lane < 6 && PocketCount < 4; lane++)
                    for (int row = -5; row <= 5 && PocketCount < 4; row++)
                    {
                        var at = transform.TransformPoint(new Vector3(side * (13 + lane * 2.5f), 0, row * 3.5f));
                        if (!FindGround(ref at) || !ClearOfRoutes(at, 3.8f)) continue;
                        bool separated = true;
                        foreach (Transform other in generated)
                            if (FlatDistance(at, other.position) < 4) { separated = false; break; }
                        if (!separated || Physics.CheckBox(at + Vector3.up * .5f, new Vector3(1.05f, .35f, .8f),
                            transform.rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var pocket = new GameObject("Outer roadside wreckage " + side + " " + row).transform;
                        pocket.SetParent(generated, false);
                        pocket.SetPositionAndRotation(at, transform.rotation * Quaternion.Euler(0, side * 8, 0));
                        pocket.localScale = Vector3.one * .5f;
                        if ((lane + row) % 2 == 0) FallenTimber(pocket); else RoofingPile(pocket);
                        Physics.SyncTransforms();
                    }
            layoutSignature = Signature();
        }

        bool FindGround(ref Vector3 at)
        {
            var hits = Physics.RaycastAll(at + Vector3.up * 2, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.normal.y < .85f) continue;
                if (hit.collider.GetComponentInParent<PhysicalGrabbable>() || hit.collider.GetComponentInParent<SibucaoPlayer>()) continue;
                at = hit.point + Vector3.up * .03f;
                return true;
            }
            return false;
        }

        public bool ClearOfRoutes(Vector3 at, float clearance)
        {
            var station = Station;
            var choices = station.GetComponentInChildren<SibucaoRouteSection>();
            if (choices)
            {
                foreach (var mission in new[] { choices.left, choices.right })
                {
                    if (!mission) continue;
                    if (FlatDistance(at, mission.transform.position) < clearance + 2.5f) return false;
                    if (mission.route != null) foreach (var point in mission.route)
                        if (FlatDistance(at, point) < clearance + 1) return false;
                }
                if (FlatDistance(at, choices.Approach) < clearance + 3) return false;
            }
            if (station.branches == null) return false;
            foreach (var branch in station.branches)
            {
                if (!branch || branch.path == null) continue;
                if (branch.actionPoint && FlatDistance(at, branch.actionPoint.position) < clearance) return false;
                for (int i = 0; i < branch.path.Length; i++)
                {
                    if (!branch.path[i]) continue;
                    var start = branch.path[i].position;
                    var end = i + 1 < branch.path.Length && branch.path[i + 1] ? branch.path[i + 1].position : start;
                    start.y = end.y = at.y;
                    var segment = end - start;
                    float t = segment.sqrMagnitude > .001f ? Mathf.Clamp01(Vector3.Dot(at - start, segment) / segment.sqrMagnitude) : 0;
                    if (Vector3.Distance(at, start + segment * t) < clearance) return false;
                }
            }
            return true;
        }

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }

        Transform Piece(Transform parent, string label, PrimitiveType shape, Vector3 at, Vector3 size, Vector3 angle, Material surface)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(angle);
            go.transform.localScale = size;
            if (surface) go.GetComponent<Renderer>().sharedMaterial = surface;
            return go.transform;
        }

        void FallenFence(Transform root)
        {
            for (int i = 0; i < 6; i++)
                Piece(root, "Broken fence paling", PrimitiveType.Cube, new Vector3((i - 2.5f) * .52f, .65f, 0),
                    new Vector3(.18f, 1.35f, .12f), new Vector3(24, 0, (i % 2 == 0 ? 1 : -1) * 9), timber);
            for (int i = 0; i < 2; i++)
                Piece(root, "Split fence rail", PrimitiveType.Cube, new Vector3(0, .35f + i * .5f, .12f),
                    new Vector3(3.1f, .12f, .16f), new Vector3(0, 0, -5), timber);
            Piece(root, "Fallen post", PrimitiveType.Cylinder, new Vector3(.2f, .19f, .8f),
                new Vector3(.3f, 1.3f, .3f), new Vector3(0, 12, 90), timber);
        }

        void RoofingPile(Transform root)
        {
            Piece(root, "Collapsed roof rafter", PrimitiveType.Cube, new Vector3(0, .23f, 0),
                new Vector3(3.1f, .24f, .23f), new Vector3(0, 22, 8), timber);
            for (int panel = 0; panel < 2; panel++)
            {
                var sheet = new GameObject("Folded corrugated roofing").transform;
                sheet.SetParent(root, false);
                sheet.localPosition = new Vector3(panel * .6f - .3f, .48f, panel * .5f - .25f);
                sheet.localRotation = Quaternion.Euler(12, panel * 35 - 20, panel == 0 ? 18 : -16);
                for (int fold = 0; fold < 8; fold++)
                    Piece(sheet, "Galvanized fold", PrimitiveType.Cube, new Vector3(0, fold % 2 * .025f, (fold - 3.5f) * .12f),
                        new Vector3(2.4f, .045f, .13f), new Vector3(fold % 2 == 0 ? 12 : -12, 0, 0), roofing);
            }
        }

        void FallenTimber(Transform root)
        {
            Piece(root, "Fallen trunk", PrimitiveType.Cylinder, new Vector3(0, .38f, 0),
                new Vector3(.65f, 1.65f, .65f), new Vector3(0, -16, 90), timber);
            Piece(root, "Snapped branch", PrimitiveType.Cylinder, new Vector3(.5f, .5f, .65f),
                new Vector3(.22f, .9f, .22f), new Vector3(65, 30, 40), timber);
            Piece(root, "Washed-up drum", PrimitiveType.Cylinder, new Vector3(-.7f, .43f, .85f),
                new Vector3(.68f, .46f, .68f), new Vector3(0, 35, 90), barrel);
        }
    }
}
