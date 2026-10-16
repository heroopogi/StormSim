using UnityEngine;
namespace StormSim.Sibucao
{
    public enum PhysicalSensorKind { Enter, Path, Hazard, Water, StrapSocket, AssistDestination, SupplyContainer, Boundary, MedicalContact, AssistRamp }
    [RequireComponent(typeof(Collider))]
    public sealed class PhysicalDecisionSensor:MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public PhysicalSensorKind kind;
        public int outcome;
        [Range(0,2)] public int sequenceStep;
        void OnTriggerEnter(Collider other){Contact(other);}
        void OnTriggerStay(Collider other){if(kind==PhysicalSensorKind.Water||kind==PhysicalSensorKind.AssistRamp)Contact(other);}
        public bool ContainsPoint(Vector3 p)=>Vector3.Distance(GetComponent<Collider>().ClosestPoint(p),p)<.002f;
        public bool ContainsBody(PhysicalGrabbable item)
        {
            var volume=GetComponent<Collider>();
            foreach(var c in item.GetComponentsInChildren<Collider>())if(!c.isTrigger&&c.enabled)
            {var b=c.bounds;for(int n=0;n<8;n++){var p=b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));if(Vector3.Distance(volume.ClosestPoint(p),p)>.04f)return false;}}
            return ContainsPoint(item.transform.position);
        }
        public void Contact(Collider other)
        {
            var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing||!station)return;
            bool player=other.GetComponentInParent<SibucaoPlayer>()==s.player;
            var item=other.GetComponentInParent<PhysicalGrabbable>();
            if(player)
            {
                if(kind==PhysicalSensorKind.Enter){station.Activate();return;}
                if(!station.Active)return;
                if(kind==PhysicalSensorKind.Hazard){if(!station.node)station.Resolve(2);return;}
                if(kind==PhysicalSensorKind.Water){var f=s.GetComponent<FloodSimulationManager>();if(f&&f.GetWaterDepth(s.player.transform.position)>.02f){station.bodyEnteredWater=true;station.Attempt.enteredWater=true;}return;}
                if(kind==PhysicalSensorKind.Path||kind==PhysicalSensorKind.Boundary){if(!station.node)station.EnterRoute(outcome,sequenceStep,s.player.transform.position);return;}
            }
            if(!item||!station.Active)return;
            if(kind==PhysicalSensorKind.Water&&item.kind==PhysicalItemKind.Stick&&item.IsHeld&&item.tip&&ContainsPoint(item.tip.position))
            {var f=s.GetComponent<FloodSimulationManager>();if(f&&f.TrySurface(item.tip.position,out float height,out _)&&item.tip.position.y<=height)station.Probe(item);}
            if(kind==PhysicalSensorKind.AssistRamp&&item==station.assist&&item.IsHeld&&ContainsPoint(item.transform.position))station.Attempt.rampPassed=true;
            // Socket, medical and assistance destinations are checked freshly at deliberate mouse release.
        }
    }
}
