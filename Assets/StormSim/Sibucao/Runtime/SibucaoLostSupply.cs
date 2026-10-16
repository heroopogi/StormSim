using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>A removed inventory item visibly falls away, then becomes unavailable for this run.</summary>
    public sealed class SibucaoLostSupply:MonoBehaviour
    {
        float remaining;
        public void Begin(){remaining=5;}
        void Update()
        {
            if(remaining<=0)return;var item=GetComponent<PhysicalGrabbable>();
            if(!item.delivered){remaining=0;return;} // New run restored the original item.
            if(SibucaoSession.Instance.Mode!=SibucaoMode.Playing)return;
            remaining-=Time.deltaTime;if(remaining<=0)gameObject.SetActive(false);
        }
    }
}
