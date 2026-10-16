using UnityEngine;
namespace StormSim.Preparedness
{
    public sealed class PreparednessWindowSlats : MonoBehaviour
    {
        public Transform[] slats;
        void LateUpdate(){var item=GetComponent<PreparednessItem>();if(slats!=null)foreach(var slat in slats)if(slat)slat.localRotation=Quaternion.Euler(0,0,item.closed?0:20);}
    }
}
