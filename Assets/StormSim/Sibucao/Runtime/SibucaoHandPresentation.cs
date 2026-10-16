using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    // Bevelled, tapered silhouettes retain the small, rigid-bone hand rig.
    [DefaultExecutionOrder(250)]
    public sealed class SibucaoHandPresentation : MonoBehaviour
    {
        public SibucaoPlayer player;
        readonly List<Mesh> meshes = new List<Mesh>();
        Vector3 shownPosition;
        bool reaching, releasing;

        void Start()
        {
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.bones.Length != 6 || !renderer.sharedMesh) continue;
                float side = renderer.transform.localPosition.x < 0 ? -1 : 1;
                // Mirror the left rig: each thumb and index finger face the space between the hands.
                var thumb = renderer.bones[1];
                thumb.localPosition = new Vector3(-side * .045f, 0, -.015f);
                thumb.localRotation = Quaternion.Euler(12, -side * 45, 0);
                for (int finger = 1; finger < 5; finger++)
                    renderer.bones[finger + 1].localPosition = new Vector3(side * (finger - 2.5f) * .023f, 0, .055f);
                var mesh = CreateMesh(renderer);
                renderer.sharedMesh = mesh;
                renderer.localBounds = new Bounds(new Vector3(0, 0, -.02f), new Vector3(.32f, .35f, .5f));
                meshes.Add(mesh);
            }
            shownPosition = transform.localPosition;
        }

        void LateUpdate()
        {
            if (!player) return;
            var s = SibucaoSession.Instance;
            if (!s || s.Mode != SibucaoMode.Playing) return;
            if (player.grabber && player.grabber.held)
            {
                shownPosition = transform.localPosition;
                reaching = false; releasing = true;
                return;
            }
            var routes = s.physicalDecisions ? s.physicalDecisions.GetComponent<SibucaoRouteChoices>() : null;
            var work = routes ? routes.Working : null;
            bool active = work && work.Step != null && (work.Manipulating || routes.HeldBy == work);
            var wanted = transform.localPosition;
            if (active)
            {
                var item = work.Step.item;
                var shape = item.GetComponentInChildren<Collider>();
                var target = shape ? shape.bounds.center : item.position;
                wanted = transform.parent.InverseTransformPoint(target);
                wanted = Vector3.ClampMagnitude(wanted, 1.05f);
                wanted.z = Mathf.Max(.24f, wanted.z);
                player.Gesture(work.Manipulating ? 2 : 1);
                if (!reaching && !releasing) shownPosition = transform.localPosition;
                reaching = true; releasing = true;
            }
            else reaching = false;
            if (releasing)
            {
                shownPosition = Vector3.Lerp(shownPosition, wanted, 1 - Mathf.Exp(-Time.deltaTime * 12));
                transform.localPosition = shownPosition;
                if (!active && (shownPosition - wanted).sqrMagnitude < .00001f) releasing = false;
            }
        }

        static Mesh CreateMesh(SkinnedMeshRenderer renderer)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var indices = new List<int>(); var weights = new List<BoneWeight>();
            var bindposes = new Matrix4x4[6];
            for (int i = 0; i < 6; i++) bindposes[i] = renderer.bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            void Shape(Vector3 center, Vector3 size, int bone, float taper)
            {
                var matrix = renderer.transform.worldToLocalMatrix * renderer.bones[bone].localToWorldMatrix;
                var half = size * .5f; float bevel = Mathf.Min(size.x, size.y, size.z) * .2f;
                Vector3 Point(Vector3 unit)
                {
                    var p = Vector3.Scale(unit, half);
                    p.x *= Mathf.Lerp(taper, 1, Mathf.InverseLerp(-half.z, half.z, p.z));
                    return matrix.MultiplyPoint3x4(center + p);
                }
                void Face(Vector3 normal, params Vector3[] points)
                {
                    int start = vertices.Count;
                    var transformed = new Vector3[points.Length];
                    for (int i = 0; i < points.Length; i++) transformed[i] = Point(points[i]);
                    var n = Vector3.Cross(transformed[1] - transformed[0], transformed[2] - transformed[0]).normalized;
                    bool reverse = Vector3.Dot(n, matrix.MultiplyVector(normal)) < 0;
                    if (reverse) n = -n;
                    foreach (var p in transformed) { vertices.Add(p); normals.Add(n); weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 }); }
                    for (int i = 1; i < points.Length - 1; i++)
                    { indices.Add(start); indices.Add(start + (reverse ? i + 1 : i)); indices.Add(start + (reverse ? i : i + 1)); }
                }
                var inset = new Vector3(1 - bevel / half.x, 1 - bevel / half.y, 1 - bevel / half.z);
                var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
                for (int axis = 0; axis < 3; axis++)
                {
                    int a = (axis + 1) % 3, b = (axis + 2) % 3;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var n = axes[axis] * sign; var u = axes[a] * inset[a]; var v = axes[b] * inset[b];
                        Face(n, n - u - v, n + u - v, n + u + v, n - u + v);
                    }
                    foreach (int sa in new[] { -1, 1 }) foreach (int sb in new[] { -1, 1 })
                    {
                        var aSide = axes[a] * sa; var bSide = axes[b] * sb;
                        var u = axes[axis] * inset[axis];
                        var p = aSide + bSide * inset[b]; var q = aSide * inset[a] + bSide;
                        Face(aSide + bSide, p - u, p + u, q + u, q - u);
                    }
                }
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    Face(new Vector3(x, y, z), new Vector3(x, y * inset.y, z * inset.z), new Vector3(x * inset.x, y, z * inset.z), new Vector3(x * inset.x, y * inset.y, z));
            }
            Shape(Vector3.zero, new Vector3(.081f, .037f, .112f), 0, .78f);
            Shape(new Vector3(0, 0, -.108f), new Vector3(.055f, .041f, .12f), 0, .86f);
            for (int finger = 0; finger < 5; finger++)
            {
                float length = finger == 0 ? .051f : .071f - Mathf.Abs(finger - 2) * .008f;
                Shape(new Vector3(0, 0, length * .5f), new Vector3(finger == 0 ? .019f : .017f, .024f, length), finger + 1, 1.08f);
            }
            var mesh = new Mesh { name = "Sibucao bevelled " + renderer.name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0);
            mesh.boneWeights = weights.ToArray(); mesh.bindposes = bindposes; mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy() { foreach (var mesh in meshes) if (mesh) Destroy(mesh); }
    }
}
