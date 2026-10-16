using UnityEngine;
namespace StormSim.Sibucao
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SibucaoFloatingDebris:MonoBehaviour
    {
        public FloodSimulationManager flood;
        public float floatOffset=.04f;
        Rigidbody body;Vector3 start;Quaternion rotation;float lastClock;
        void Awake(){body=GetComponent<Rigidbody>();start=transform.position;rotation=transform.rotation;}
        void FixedUpdate()
        {
            var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing||!flood)return;
            if(s.state.elapsed<lastClock){body.position=start;body.rotation=rotation;if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}}lastClock=s.state.elapsed;
            if(body.isKinematic)return;
            if(!flood.TrySurface(body.position,out float height,out float ground)||height-ground<.05f)return;
            float error=height+floatOffset-body.position.y;
            if(error<-.2f)return;
            body.AddForce(Vector3.up*Mathf.Clamp(9.81f+error*30-body.linearVelocity.y*6,0,30),ForceMode.Acceleration);
            var flow=flood.GetFlowVelocity(body.position);
            var planar=body.linearVelocity;planar.y=0;body.AddForce((flow-planar)*1.6f,ForceMode.Acceleration);
            body.AddTorque(Vector3.up*Mathf.Sin(Time.time+start.x)*.1f,ForceMode.Acceleration);
        }
    }
}
