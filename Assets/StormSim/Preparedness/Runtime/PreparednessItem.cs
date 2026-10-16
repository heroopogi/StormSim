using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    public enum PrepKind { Decoration, Water, Food, Flashlight, Radio, Batteries, FirstAid, Medicine, Documents, Phone, PowerBank, Hygiene, Bag, Electronics, Valuables, Outdoor, Litter, Tool, Cable, Window, Door, Plan, Advisory, Bed, Exit, BagZone, SupplyZone, HighShelf, IndoorZone, BinZone, ChargeZone, TableZone, StorageDoor, HouseholdTask, Neighbor }

    [DisallowMultipleComponent]
    public sealed class PreparednessItem : MonoBehaviour
    {
        public string id, displayName;
        [TextArea] public string purpose;
        public PrepKind kind;
        public bool portable=true, prepared, closed, plugged=true;
        public bool gathered;
        public PreparednessItem storageDoor;
        public int cells;
        public float charge, litres, mass=.3f;
        public Transform mechanism;
        public Vector3 openEuler;
        public Light deviceLight;
        [HideInInspector] public string storedIn="";
        [HideInInspector] public bool held;
        bool movingMechanism;
        Collider[] carryColliders;bool[] originalTriggers;Vector3 lastSafePosition;Quaternion lastSafeRotation;bool safePoseReady;
        float settlingSeconds;Vector3 lastPhysicsPosition;
        void OnEnable(){RememberSafePose();}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        void RememberSafePose(){if(Finite(transform.position)){lastSafePosition=transform.position;lastSafeRotation=transform.rotation;safePoseReady=true;}}
        public void SetCarried(bool value)
        {
            if(value)
            {
                carryColliders=GetComponentsInChildren<Collider>();originalTriggers=new bool[carryColliders.Length];
                for(int n=0;n<carryColliders.Length;n++){originalTriggers[n]=carryColliders[n].isTrigger;carryColliders[n].isTrigger=true;}
            }
            else if(carryColliders!=null)
            {for(int n=0;n<carryColliders.Length;n++)if(carryColliders[n])carryColliders[n].isTrigger=originalTriggers[n];carryColliders=null;}
        }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public bool IsZone => kind>=PrepKind.BagZone&&kind<=PrepKind.TableZone;
        public bool IsPacked => storedIn=="bag";
        public void FitColliderToVisual()
        {
            var collider=GetComponent<BoxCollider>();
            if(!portable||!collider||collider.isTrigger)return;
            var renderers=GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!(r is ParticleSystemRenderer)&&!r.GetComponent<TextMesh>()).ToArray();
            if(renderers.Length==0)return;
            var visual=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))visual.Encapsulate(renderer.bounds);
            var scale=transform.lossyScale;
            scale=new Vector3(Mathf.Max(.0001f,Mathf.Abs(scale.x)),Mathf.Max(.0001f,Mathf.Abs(scale.y)),Mathf.Max(.0001f,Mathf.Abs(scale.z)));
            collider.center=transform.InverseTransformPoint(visual.center);
            collider.size=new Vector3(visual.size.x/scale.x,visual.size.y/scale.y,visual.size.z/scale.z);
        }
        public void SynchronizePose()
        {
            var b=Body;if(!b)return;
            var interpolation=b.interpolation;b.interpolation=RigidbodyInterpolation.None;
            b.position=transform.position;b.rotation=transform.rotation;b.interpolation=interpolation;
        }
        public void SetPhysics(bool dynamic)
        {
            var b=Body;if(!b)return;
            settlingSeconds=0;
            if(dynamic)
            {
                RememberSafePose();SynchronizePose();
                b.mass=Mathf.Max(.05f,mass);b.linearDamping=3;b.angularDamping=7;
                // Speculative contacts and a low depenetration ceiling stop small props
                // from being catapulted when released close to furniture or the player.
                b.maxDepenetrationVelocity=.35f;b.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                b.isKinematic=false;b.useGravity=true;b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;lastPhysicsPosition=b.position;b.WakeUp();
            }
            else
            {
                if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}
                b.isKinematic=true;b.useGravity=false;b.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;b.Sleep();
                RememberSafePose();
            }
        }
        public void ApplyVisual()
        {
            var task=GetComponent<PreparednessHouseTask>();if(task)task.ApplyState();
            if(mechanism)mechanism.localRotation=Quaternion.Euler(closed?Vector3.zero:openEuler);
            if(deviceLight)deviceLight.enabled=prepared&&cells>=2;
        }
        public void AnimateVisual(){movingMechanism=mechanism; if(deviceLight)deviceLight.enabled=prepared&&cells>=2;}
        void Update()
        {
            if(!movingMechanism||!mechanism)return;
            var target=Quaternion.Euler(closed?Vector3.zero:openEuler);
            mechanism.localRotation=Quaternion.RotateTowards(mechanism.localRotation,target,120*Time.deltaTime);
            if(Quaternion.Angle(mechanism.localRotation,target)<.1f)movingMechanism=false;
        }
        void FixedUpdate()
        {
            var body=Body;
            if(!body)return;
            if(held)return;
            if(body.isKinematic){RememberSafePose();return;}
            if(!Finite(body.position)||!Finite(body.linearVelocity)||!Finite(body.angularVelocity))
            {
                if(!safePoseReady)lastSafePosition=Vector3.zero;
                body.position=lastSafePosition;body.rotation=safePoseReady?lastSafeRotation:Quaternion.identity;
                SetPhysics(false);return;
            }
            body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,3.5f);
            body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,7);
            // A released object should never travel far enough to appear airborne
            // outside the house. Restore the exact release pose if physics explodes.
            if(safePoseReady&&(Vector3.Distance(body.position,lastSafePosition)>8||body.position.y>lastSafePosition.y+3))
            {body.position=lastSafePosition;body.rotation=lastSafeRotation;SetPhysics(false);return;}
            float movement=(body.position-lastPhysicsPosition).sqrMagnitude;lastPhysicsPosition=body.position;
            bool settled=body.IsSleeping()||(body.linearVelocity.sqrMagnitude<.0025f&&body.angularVelocity.sqrMagnitude<.01f)||
                (movement<.000001f&&body.linearVelocity.y<=0);
            settlingSeconds=settled?settlingSeconds+Time.fixedDeltaTime:0;
            // Once a prop has naturally landed, lock it in that pose. It remains
            // fully pickable, but cannot accumulate jitter or launch later.
            if(settlingSeconds>=.45f)SetPhysics(false);
        }
        public void ResolveReleaseOverlap(Transform ignoredRoot)
        {
            var own=GetComponentsInChildren<Collider>();Vector3 correction=Vector3.zero;
            for(int pass=0;pass<3;pass++)
            {
                bool moved=false;
                foreach(var collider in own)
                {
                    if(!collider||collider.isTrigger||!collider.enabled)continue;
                    foreach(var other in Physics.OverlapBox(collider.bounds.center,collider.bounds.extents*.98f,collider.transform.rotation,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(!other||other.transform.IsChildOf(transform)||ignoredRoot&&other.transform.IsChildOf(ignoredRoot))continue;
                        if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out var distance)&&distance>0)
                        {var step=direction*Mathf.Min(distance+.008f,.18f);transform.position+=step;correction+=step;moved=true;}
                    }
                }
                if(!moved||correction.magnitude>.5f)break;
            }
            SynchronizePose();RememberSafePose();
        }
        public Bounds WorldBounds()
        {
            var renderers=GetComponentsInChildren<Renderer>();
            var bounds=new Bounds(transform.position,Vector3.zero);
            if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);}
            return bounds;
        }
    }
}
