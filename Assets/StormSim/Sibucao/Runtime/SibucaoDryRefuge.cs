using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>Authored non-inundation boundary for the scenario's designated evacuation center.
    /// This constraint is not a claim that the real school is safe at a measured flood level.</summary>
    [DisallowMultipleComponent]
    public sealed class SibucaoDryRefuge : MonoBehaviour
    {
        [Tooltip("School grounds in local X/Z; includes classrooms, court and entrance.")]
        public Vector2 size=new Vector2(122,110);
        public Transform arrivalPoint;
        public float arrivalRadius=3.5f;
        [Tooltip("Authored perimeter drainage outlet band, in metres. Not a calibrated real-world drain capacity.")]
        public float drainageWidth=4;
        public bool Contains(Vector3 world,float margin=0)
        {
            var p=transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x)<=size.x*.5f+margin&&Mathf.Abs(p.z)<=size.y*.5f+margin;
        }
        public bool HasArrived(Vector3 world)
        {
            if(!arrivalPoint||!Contains(world))return false;
            var d=world-arrivalPoint.position;
            return new Vector2(d.x,d.z).magnitude<=arrivalRadius&&Mathf.Abs(d.y)<3;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(.5f,.8f,.9f,.55f);Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero,new Vector3(size.x,.1f,size.y));
            if(arrivalPoint){Gizmos.matrix=Matrix4x4.identity;Gizmos.DrawWireSphere(arrivalPoint.position,arrivalRadius);}
        }
    }
}
