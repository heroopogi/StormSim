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
        readonly List<Transform> presentedHands = new List<Transform>();
        readonly List<Vector3> baseHandPositions = new List<Vector3>();
        readonly List<Quaternion> baseHandRotations = new List<Quaternion>();
        Vector3 shownPosition;
        bool reaching, releasing;
        Transform incidentTarget;
        int incidentAction=-1,incidentFrame=-10;
        float incidentProgress;

        public bool IncidentAnimationActive=>incidentTarget&&Time.frameCount-incidentFrame<=2;
        public void DriveIncident(Transform target,int action,float progress)
        {
            incidentTarget=target;incidentAction=action;incidentProgress=Mathf.Clamp01(progress);incidentFrame=Time.frameCount;
        }

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
                presentedHands.Add(renderer.transform);
                baseHandPositions.Add(renderer.transform.localPosition);
                baseHandRotations.Add(renderer.transform.localRotation);
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
            bool incidentActive=IncidentAnimationActive;
            bool active = incidentActive||work && work.Step != null && (work.Manipulating || routes.HeldBy == work);
            var wanted = transform.localPosition;
            Quaternion wantedRotation=Quaternion.identity;
            if(incidentActive)
            {
                var shape=incidentTarget.GetComponentInChildren<Collider>();
                var target=shape?shape.bounds.center:incidentTarget.position;
                wanted=transform.parent.InverseTransformPoint(target);
                wanted=Vector3.ClampMagnitude(wanted,1.08f);wanted.z=Mathf.Max(.28f,wanted.z);
                float pulse=Mathf.Sin(Time.time*(incidentAction==2||incidentAction==4?11:6));
                if(incidentAction==1){wanted.y+=.18f;wanted.x+=pulse*.035f;wantedRotation=Quaternion.Euler(-18,0,pulse*9);}
                else if(incidentAction==2){wanted.z+=.08f;wanted.y-=.04f;wantedRotation=Quaternion.Euler(-13,0,pulse*2);}
                else if(incidentAction==3){wanted.x+=pulse*.09f;wantedRotation=Quaternion.Euler(7,pulse*8,0);}
                else if(incidentAction==4){wanted.y-=.15f;wanted.z+=.04f;wantedRotation=Quaternion.Euler(19,0,pulse*2);}
                else if(incidentAction==5){wanted.y+=.02f;wanted.z+=.07f;wantedRotation=Quaternion.Euler(-6,0,0);}
                else if(incidentAction==6){wanted.y-=.07f;wantedRotation=Quaternion.Euler(12,0,pulse*3);}
                else wantedRotation=Quaternion.Euler(4,pulse*3,0);
                // Keep desktop hands readable in-frame even when the world target is at ground level.
                wanted.x=Mathf.Clamp(wanted.x,-.32f,.32f);wanted.y=Mathf.Clamp(wanted.y,-.41f,.22f);wanted.z=Mathf.Clamp(wanted.z,.3f,.76f);
                player.Gesture(incidentAction==1||incidentAction==5?1:2);
                if(!reaching)shownPosition=transform.localPosition;
                reaching=true;releasing=true;
            }
            else if (active)
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
                transform.localRotation=Quaternion.Slerp(transform.localRotation,wantedRotation,1-Mathf.Exp(-Time.deltaTime*14));
                if (!active && (shownPosition - wanted).sqrMagnitude < .00001f) releasing = false;
            }
            AnimatePresentedHands(incidentActive?incidentAction:-1,incidentProgress);
        }

        void AnimatePresentedHands(int action,float progress)
        {
            float blend=1-Mathf.Exp(-Time.deltaTime*14),pulse=Mathf.Sin(Time.time*(action==2||action==4?11:6));
            for(int i=0;i<presentedHands.Count;i++)
            {
                var hand=presentedHands[i];if(!hand)continue;
                float side=Mathf.Sign(baseHandPositions[i].x);if(Mathf.Approximately(side,0))side=i%2==0?-1:1;
                Vector3 offset=Vector3.zero;Quaternion rotation=baseHandRotations[i];
                if(action==0){offset=new Vector3(side*.015f,side<0?.015f:-.02f,side>0?.08f:.015f);rotation*=Quaternion.Euler(-8,side*8,side*5);}
                else if(action==1){offset=new Vector3(side*.035f,(side>0?.08f:.01f)+pulse*.012f,side>0?.06f:0);rotation*=Quaternion.Euler(-18,side*18,side*pulse*10);}
                else if(action==2){offset=new Vector3(side*.025f,pulse*.006f,.105f+pulse*.012f);rotation*=Quaternion.Euler(-22,side*4,side*7);}
                else if(action==3){offset=new Vector3(side*(.025f+pulse*.035f),-.025f,.045f);rotation*=Quaternion.Euler(12,side*(12+pulse*10),0);}
                else if(action==4){offset=new Vector3(side*.025f,-.06f,.06f-pulse*.018f);rotation*=Quaternion.Euler(28,side*4,side*3);}
                else if(action==5){offset=new Vector3(side*.055f,.015f,.085f);rotation*=Quaternion.Euler(-8,side*22,side*14);}
                else if(action==6){offset=new Vector3(side*.018f,-.025f,.07f+Mathf.Sin(progress*Mathf.PI)*.035f);rotation*=Quaternion.Euler(18,side*5,side*5);}
                hand.localPosition=Vector3.Lerp(hand.localPosition,baseHandPositions[i]+offset,blend);
                hand.localRotation=Quaternion.Slerp(hand.localRotation,rotation,blend);
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
