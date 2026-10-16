using UnityEngine;
namespace StormSim.Sibucao
{
    // A retained wet mark on the measuring stick, driven by the sampled water field.
    // No score color, numeric HUD readout, or recommended-choice classification.
    [DisallowMultipleComponent]
    public sealed class SibucaoDepthProbe:MonoBehaviour
    {
        public float LastDepth {get;private set;}
        public Vector3 LastLocation {get;private set;}
        public string LastReaction {get;private set;}
        PhysicalGrabbable item;Transform mark;Material material;
        Vector3 stablePoint;float contactSeconds;bool sampledHere;
        void Awake()
        {
            item=GetComponent<PhysicalGrabbable>();
            var source=GetComponentInChildren<Renderer>();
            var ring=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ring.name="Measured water mark";
            var collider=ring.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            mark=ring.transform;mark.SetParent(transform,false);mark.localScale=new Vector3(.075f,.014f,.075f);
            material=new Material(source.sharedMaterial);
            material.color=new Color(.5f,.76f,.86f);ring.GetComponent<Renderer>().sharedMaterial=material;ring.SetActive(false);
        }
        public void ClearReading(){LastDepth=0;LastReaction=null;contactSeconds=0;sampledHere=false;if(mark)mark.gameObject.SetActive(false);}
        public void PrepareForRun()
        {
            ClearReading();if(!item||item.everHeld||item.IsHeld)return;
            Physics.SyncTransforms();
            // Layouts follow a sloped street. The authored local height can put the
            // thin stick below a different home's ground before the player arrives.
            float floor=float.NegativeInfinity;var p=transform.position;
            foreach(var hit in Physics.RaycastAll(p+Vector3.up*4,Vector3.down,10,~0,QueryTriggerInteraction.Ignore))
                if(hit.normal.y>.6f&&!hit.collider.GetComponentInParent<PhysicalGrabbable>()&&
                    !hit.collider.GetComponentInParent<SibucaoPlayer>()&&!hit.collider.GetComponentInParent<SibucaoResident>()&&
                    hit.point.y<p.y+2&&hit.point.y>floor)floor=hit.point.y;
            float bottom=float.PositiveInfinity;
            foreach(var collider in item.GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger)bottom=Mathf.Min(bottom,collider.bounds.min.y);
            if(float.IsFinite(floor)&&float.IsFinite(bottom))transform.position+=Vector3.up*(floor+.04f-bottom);
            if(!item.Body.isKinematic){item.Body.linearVelocity=Vector3.zero;item.Body.angularVelocity=Vector3.zero;}
            item.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            item.Body.isKinematic=true; // The real grip releases it to physical movement.
        }
        public bool TryRead(out Vector3 point,out float depth)
        {
            point=default;depth=0;
            if(!item||!item.IsHeld||!item.tip)return false;
            var flood=SibucaoSession.Instance.GetComponent<FloodSimulationManager>();point=item.tip.position;
            // Use the same surface and bed that generate the visible flood mesh.
            if(!flood||!flood.TrySurface(point,out float surface,out float ground)||surface-ground<=.05f||
                point.y>surface+.04f||point.y<ground-.2f)
            {contactSeconds=0;sampledHere=false;return false;}
            if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(stablePoint.x,stablePoint.z))>.22f)
            {stablePoint=point;contactSeconds=0;sampledHere=false;}
            contactSeconds+=Time.deltaTime;
            if(sampledHere||contactSeconds<.35f)return false;
            depth=surface-ground;sampledHere=true;LastDepth=depth;LastLocation=new Vector3(point.x,ground,point.z);
            return true;
        }
        public void ShowReading(float depth,float previousDepth,bool second)
        {
            LastDepth=depth;
            mark.localPosition=item.tip.localPosition+Vector3.up*Mathf.Clamp(depth,.035f,1.55f);
            mark.gameObject.SetActive(true);
            string height=depth<.15f?"Water covers the tip here.":depth<.45f?"Water reaches the lower marks.":
                depth<.85f?"Water reaches the middle marks.":depth<1.3f?"Water reaches the upper marks.":"Water is beyond the upper marks here.";
            LastReaction=second?(depth>previousDepth+.08f?"This spot is deeper than the first.":depth<previousDepth-.08f?
                "This spot is shallower than the first.":"The water is about the same depth here."):height;
            SibucaoSession.Instance.Say(LastReaction,5);
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
