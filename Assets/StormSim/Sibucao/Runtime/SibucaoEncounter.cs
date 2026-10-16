using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed class SibucaoEncounter : MonoBehaviour
    {
        public DecisionNode node;
        public int index;
        public float routeDistance;
        public bool arrival;
        public Transform actor;
        public bool following;
        public bool Resolved => SibucaoSession.Instance && SibucaoSession.Instance.state.resolved[index];
        void OnTriggerEnter(Collider other)
        {
            var session=SibucaoSession.Instance;
            if(!session || !other.GetComponentInParent<SibucaoPlayer>())return;
            if(arrival)session.Complete();
            else if(session.Mode==SibucaoMode.Playing && !Resolved)session.Notify(node.title,"Look toward the encounter and press E.");
        }
        void Update()
        {
            var s=SibucaoSession.Instance;
            if(!following || !actor || !s || s.Mode!=SibucaoMode.Playing)return;
            Vector3 goal=s.player.transform.position-s.player.transform.forward*2;
            if(Vector3.Distance(actor.position,goal)>2.5f)
            {
                Vector3 delta=goal-actor.position;delta.y=0;
                actor.position+=delta.normalized*Mathf.Min(delta.magnitude,2.7f*Time.deltaTime);
                if(Physics.Raycast(actor.position+Vector3.up*5,Vector3.down,out var hit,12,s.groundMask,QueryTriggerInteraction.Ignore))actor.position=new Vector3(actor.position.x,hit.point.y,actor.position.z);
                if(delta.sqrMagnitude>.1f)actor.rotation=Quaternion.LookRotation(delta);
            }
        }
    }
}
