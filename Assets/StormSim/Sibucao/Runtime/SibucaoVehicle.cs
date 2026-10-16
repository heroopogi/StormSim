using System;
using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    [Serializable] public sealed class VehicleState
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
    }

    // Low-speed, collision-tested arcade vehicle. Existing artwork remains unchanged.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SibucaoVehicle : MonoBehaviour
    {
        public static readonly List<SibucaoVehicle> All = new List<SibucaoVehicle>();
        public string vehicleId, displayName;
        public float maxSpeed=9, acceleration=3, turnRate=45;
        public Vector3 seat=new Vector3(-.3f,.05f,.4f);
        public float Speed {get;private set;}
        public float SteerAngle {get;private set;}
        public float EngineRpm {get;private set;}=850;
        public float LongitudinalAcceleration {get;private set;}
        public float LateralAcceleration {get;private set;}
        public GameObject cockpitPrefab;
        public SibucaoCockpit Cockpit {get;private set;}
        public SibucaoPlayer Driver {get;private set;}
        BoxCollider hull;
        Renderer[] art;
        Vector3 initialPosition;
        Quaternion initialRotation;
        void Awake(){hull=GetComponent<BoxCollider>();art=GetComponentsInChildren<Renderer>();initialPosition=transform.position;initialRotation=transform.rotation;}
        void OnEnable(){All.Add(this);}
        void OnDisable(){All.Remove(this);}
        bool Obstacle(Collider c)=>c && !c.isTrigger && !c.transform.IsChildOf(transform) && !c.GetComponentInParent<SibucaoPlayer>();
        bool Ground(Vector3 p,out RaycastHit ground)
        {
            ground=default;float best=float.MaxValue;
            foreach(var h in Physics.RaycastAll(p+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
                if(Obstacle(h.collider)&&h.normal.y>.75f&&h.distance<best){ground=h;best=h.distance;}
            return best<float.MaxValue;
        }
        public string TravelRestriction(Vector3 at,SibucaoPlayer player)
        {
            var s=SibucaoSession.Instance;if(!s)return null;
            var f=s.GetComponent<FloodSimulationManager>();
            if(f&&f.IsCatchingUp)return "Wait for the floodwater to finish loading.";
            if(f&&f.GetWaterDepth(at)>=.15f)return "Vehicle route closed by floodwater. Continue on foot using the raised access.";
            if(s.state.companion||s.RouteChoices&&s.RouteChoices.Working&&s.RouteChoices.Working.companions.Length>0)
                return "Stay on foot with the residents you are accompanying.";
            if(player&&player.grabber&&player.grabber.held)return "Put down or pack the carried item before entering.";
            if(s.RouteChoices)foreach(var section in s.RouteChoices.sections)
                if(SibucaoRouteChoices.Flat(at,section.station.transform.position)<24)return "Pedestrian task area. Park here and follow the street route on foot.";
            return null;
        }
        public bool TryEnter(SibucaoPlayer p,bool restoring=false)
        {
            if(Driver || p.vehicle || Vector3.Distance(hull.ClosestPoint(p.transform.position+Vector3.up),p.transform.position+Vector3.up)>3.5f)return false;
            var restriction=restoring?null:TravelRestriction(transform.position,p);if(restriction!=null){SibucaoSession.Instance.Notify("Vehicle unavailable",restriction);return false;}
            p.Teleport(transform.TransformPoint(seat),transform.forward);
            Driver=p;p.vehicle=this;p.GetComponent<CharacterController>().enabled=false;Speed=0;
            // Exterior meshes have opaque windows and no interior. Hide only for their driver.
            foreach(var r in art)r.forceRenderingOff=true;
            if(cockpitPrefab){var cabin=Instantiate(cockpitPrefab,transform);cabin.transform.localPosition=seat+Vector3.up*p.view.transform.localPosition.y;Cockpit=cabin.GetComponent<SibucaoCockpit>();Cockpit.Bind(this);}
            SyncDriver();return true;
        }
        public void SyncDriver()
        {
            if(!Driver)return;
            Driver.transform.position=transform.TransformPoint(seat);
        }
        public bool FindExit(out Vector3 position)
        {
            var size=Vector3.Scale(hull.size,transform.lossyScale);
            foreach(var local in new[]{new Vector3(-size.x*.5f-1,0,0),new Vector3(size.x*.5f+1,0,0),new Vector3(0,0,-size.z*.5f-1.2f),new Vector3(0,0,size.z*.5f+1.2f)})
            {
                var p=transform.position+transform.rotation*local;
                if(!Ground(p,out var hit))continue;
                p=hit.point+Vector3.up*.12f;
                var f=SibucaoSession.Instance.GetComponent<FloodSimulationManager>();if(f&&f.GetWaterDepth(p)>.85f)continue;
                bool blocked=false;
                foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.45f,.32f,~0,QueryTriggerInteraction.Ignore))
                    if(!c.GetComponentInParent<SibucaoPlayer>()){blocked=true;break;}
                if(!blocked){position=p;return true;}
            }
            position=default;return false;
        }
        public bool TryExit()
        {
            if(!Driver)return true;
            if(Mathf.Abs(Speed)>.6f){SibucaoSession.Instance.ShowError("Stop the vehicle before getting out.");return false;}
            if(!FindExit(out var p)){SibucaoSession.Instance.ShowError("There is no clear space to get out. Reposition the vehicle.");return false;}
            var player=Driver;Release();player.Teleport(p,transform.forward);return true;
        }
        public void Release()
        {
            if(Cockpit){Cockpit.Unbind();Destroy(Cockpit.gameObject);Cockpit=null;}
            SteerAngle=0;EngineRpm=850;LongitudinalAcceleration=0;LateralAcceleration=0;
            if(Driver){Driver.vehicle=null;Driver.GetComponent<CharacterController>().enabled=true;}
            Driver=null;Speed=0;
            foreach(var r in art)if(r)r.forceRenderingOff=false;
        }
        public void ResetVehicle(){Release();transform.SetPositionAndRotation(initialPosition,initialRotation);}
        public VehicleState Capture()=>new VehicleState{id=vehicleId,position=transform.position,rotation=transform.rotation};
        public void Restore(VehicleState state){Release();transform.SetPositionAndRotation(state.position,state.rotation);}
        public void Drive(float throttle,float steer,bool brake,float dt)
        {
            if(!Driver || dt<=0)return;
            if(TravelRestriction(transform.position,null)!=null){Speed=0;return;}
            float cap=SibucaoSession.Instance.state.companion?2.5f:maxSpeed;
            float oldSpeed=Speed;
            bool opposing=throttle!=0 && Speed*throttle<-.05f;
            float target=brake||opposing?0:Mathf.Clamp(throttle,-1,1)*(throttle<0?2.5f:cap);
            float response=brake?6:opposing?4.5f:throttle==0?.25f+.012f*Speed*Speed:displayName=="Tricycle"?1.6f:2.3f;
            Speed=Mathf.MoveTowards(Speed,target,dt*response);
            LongitudinalAcceleration=(Speed-oldSpeed)/dt;
            float maxAngle=Mathf.Lerp(32,13,Mathf.Clamp01(Mathf.Abs(Speed)/9));
            SteerAngle=Mathf.MoveTowards(SteerAngle,Mathf.Clamp(steer,-1,1)*maxAngle,dt*(steer==0?65:48));
            float wheelbase=displayName=="Tricycle"?1.65f:2.65f;
            float yawRate=Speed/wheelbase*Mathf.Tan(SteerAngle*Mathf.Deg2Rad);
            float gripLimit=(displayName=="Tricycle"?2.8f:4.2f)/Mathf.Max(.4f,Mathf.Abs(Speed));
            yawRate=Mathf.Clamp(yawRate,-gripLimit,gripLimit);
            LateralAcceleration=yawRate*Speed;
            float gearBand=Mathf.Repeat(Mathf.Abs(Speed),3.5f)/3.5f;
            EngineRpm=Mathf.Lerp(EngineRpm,850+gearBand*2900+Mathf.Abs(throttle)*650,Mathf.Clamp01(dt*5));
            var oldRotation=transform.rotation;
            var nextRotation=Quaternion.Euler(0,oldRotation.eulerAngles.y+yawRate*Mathf.Rad2Deg*dt,0);
            var delta=nextRotation*Vector3.forward*Speed*dt;
            var next=transform.position+delta;
            // Check the vehicle's leading edge before it crosses into a flooded/foot-only area.
            var leading=next+nextRotation*Vector3.forward*Mathf.Sign(Speed)*(hull.size.z*transform.lossyScale.z*.5f+.25f);
            if(TravelRestriction(next,null)!=null||TravelRestriction(leading,null)!=null){Speed=0;return;}
            if(!Ground(next,out var ground)||Mathf.Abs(ground.point.y-transform.position.y)>.7f){Speed=0;return;}
            next.y=ground.point.y;
            var half=Vector3.Scale(hull.size,transform.lossyScale)*.5f;
            // Lift the query bottom above the road surface to avoid treating the road as an obstacle.
            half.y=Mathf.Max(.2f,half.y-.15f);
            var center=next+nextRotation*Vector3.Scale(hull.center+Vector3.up*.17f,transform.lossyScale);
            foreach(var c in Physics.OverlapBox(center,half,nextRotation,~0,QueryTriggerInteraction.Ignore))
                if(Obstacle(c)){Speed=0;return;}
            var from=transform.TransformPoint(hull.center+Vector3.up*.17f);
            if(delta.sqrMagnitude>.000001f)
                foreach(var hit in Physics.BoxCastAll(from,half,delta.normalized,oldRotation,delta.magnitude+.02f,~0,QueryTriggerInteraction.Ignore))
                    if(Obstacle(hit.collider)){Speed=0;return;}
            transform.SetPositionAndRotation(next,nextRotation);
            Driver.transform.Rotate(0,Mathf.DeltaAngle(oldRotation.eulerAngles.y,nextRotation.eulerAngles.y),0);
            SyncDriver();Physics.SyncTransforms();
            var s=SibucaoSession.Instance;
            foreach(var e in s.encounters)if(e.arrival && e.GetComponent<Collider>().bounds.Contains(transform.position+Vector3.up))s.Complete();
        }
        public static SibucaoVehicle LookAt(SibucaoPlayer p)
        {
            if(Physics.Raycast(p.view.transform.position,p.view.transform.forward,out var hit,4.5f,~0,QueryTriggerInteraction.Ignore))return hit.collider.GetComponentInParent<SibucaoVehicle>();
            return null;
        }
    }
}

