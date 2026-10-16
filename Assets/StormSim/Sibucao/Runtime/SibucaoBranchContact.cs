using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoBranchContact:MonoBehaviour
    {
        public SibucaoPhysicalOption option;
        void OnTriggerEnter(Collider other){Contact(other);}
        void OnTriggerStay(Collider other){Contact(other);}
        void Contact(Collider other){if(option&&option.Engaged&&other.GetComponentInParent<SibucaoPlayer>())option.Attempt.contactConsequence=true;}
    }
}
