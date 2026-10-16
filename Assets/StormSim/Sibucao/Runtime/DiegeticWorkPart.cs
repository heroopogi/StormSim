using UnityEngine;
namespace StormSim.Sibucao
{
    // The visible collider is the handle of a constrained physical joint.
    public sealed class DiegeticWorkPart : MonoBehaviour
    {
        public PhysicalEncounterRunner owner;
        public int index;
        public string action;
        public Vector3 closed,open;
        public Quaternion closedRotation,openRotation;
        public Vector3 PullDirection=>transform.parent.TransformDirection(open-closed).normalized;
        Vector3 Position(float t)
        {
            var p=Vector3.Lerp(closed,open,t);
            // Extract a supported board before lowering it onto the roadside.
            if(open.y<closed.y-.1f)p.y=Mathf.Lerp(closed.y,open.y,Mathf.InverseLerp(.75f,1,t));
            return p;
        }
        public void Apply(float position)
        {
            transform.localPosition=Position(position);
            transform.localRotation=Quaternion.Slerp(closedRotation,openRotation,position);
        }
        public float AllowedDisplacement(float from,float to)
        {
            if(to<=from)return to;
            Vector3 delta=transform.parent.TransformVector(Position(to)-Position(from));
            if(delta.sqrMagnitude<.0000001f)return to;
            var box=GetComponent<BoxCollider>();if(!box)return to;
            Vector3 half=Vector3.Scale(box.size*.45f,transform.lossyScale);
            foreach(var hit in Physics.BoxCastAll(transform.TransformPoint(box.center),half,delta.normalized,transform.rotation,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.GetComponentInParent<DiegeticWorkPart>()||hit.collider.transform.IsChildOf(owner.transform)||
                    hit.collider.GetComponentInParent<SibucaoPlayer>()||hit.collider.GetComponentInParent<PhysicalGrabbable>()||hit.normal.y>.6f)continue;
                if(hit.distance>.002f)to=Mathf.Min(to,from+(to-from)*Mathf.Clamp01((hit.distance-.002f)/delta.magnitude));
            }
            return to;
        }
    }
}
