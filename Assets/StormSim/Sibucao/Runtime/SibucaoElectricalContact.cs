using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoElectricalContact:MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public int choice;
        void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<SibucaoPlayer>();var s=SibucaoSession.Instance;
            if(player&&s&&s.Mode==SibucaoMode.Playing&&station)
            {station.manager.ApplyWorldContact(station,choice);station.manager.Branch(station.index).contactConsequence=true;}
        }
    }
}
