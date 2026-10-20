using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Conservative local stepping: stop rather than tunnel or invent a safe route.</summary>
    public static class SibucaoSafeNpcMotion
    {
        static readonly RaycastHit[] obstructionHits=new RaycastHit[32];
        static readonly float[] angles={0,35,-35,70,-70,90,-90};
        public static Vector3 TrailTarget(List<Vector3> trail,Vector3 leader,Vector3 follower,bool finish=false)
        {
            if(trail.Count==0||Flat(trail[trail.Count-1],leader)>.45f)
                if(trail.Count<128)trail.Add(leader);
            while(trail.Count>1&&Flat(follower,trail[0])<.3f)trail.RemoveAt(0);
            if(!finish&&Flat(follower,leader)<1.6f)return follower;
            return trail.Count>0?trail[0]:leader;
        }
        static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        public static bool Step(Transform actor,Vector3 target,float speed,SibucaoSession session,Transform group=null,float radius=.24f)
        {
            if(!actor||!session)return false;
            var flood=session.GetComponent<FloodSimulationManager>();
            if(flood&&flood.IsCatchingUp)return false;
            Vector3 delta=target-actor.position;delta.y=0;
            float distance=Mathf.Min(delta.magnitude,Mathf.Min(Time.deltaTime,.1f)*speed);
            if(distance<.001f)return true; // Already at the target is not an obstruction.
            foreach(float angle in angles)
            {
                Vector3 direction=Quaternion.Euler(0,angle,0)*delta.normalized,next=actor.position+direction*distance;
                bool blocked=false;
                for(int bodyPart=0;bodyPart<2;bodyPart++)
                {
                    // The lower sweep starts above the allowed 0.30 m ground step.
                    // Ground sampling still rejects higher ledges and unsupported landings.
                    float height=bodyPart==0?radius+.31f:1.5f;
                    int count=Physics.SphereCastNonAlloc(actor.position+Vector3.up*height,radius,direction,obstructionHits,distance+.12f,~0,QueryTriggerInteraction.Ignore);
                    if(count==obstructionHits.Length){blocked=true;break;}
                    for(int i=0;i<count;i++)
                    {
                        var hit=obstructionHits[i];
                        if(hit.transform.IsChildOf(actor)||actor.IsChildOf(hit.transform)||group&&hit.transform.IsChildOf(group)||hit.normal.y>.65f)continue;
                        blocked=true;break;
                    }
                    if(blocked)break;
                }
                if(blocked)continue;
                if(!Physics.Raycast(next+Vector3.up*.45f,Vector3.down,out var ground,.9f,~0,QueryTriggerInteraction.Ignore))continue;
                if(ground.normal.y<.75f||ground.transform.IsChildOf(actor)||Mathf.Abs(ground.point.y-actor.position.y)>.3f)continue;
                next.y=ground.point.y;if(flood&&flood.GetWaterDepth(next)>.02f)continue;
                actor.position=next;return true;
            }
            return false;
        }
    }
}
