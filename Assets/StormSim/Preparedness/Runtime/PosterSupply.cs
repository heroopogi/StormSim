using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    public enum PosterSupplyType { DryClothes, Raincoat, Blanket, Boots, EmergencyCash, Candles, Matches, Whistle }
    [RequireComponent(typeof(PreparednessItem))]
    public sealed class PosterSupply : MonoBehaviour
    {
        public PosterSupplyType supply;
        public string instruction;
        public bool Complete {get {var i=GetComponent<PreparednessItem>();return i.prepared&&(i.IsPacked||supply==PosterSupplyType.Boots&&i.storedIn=="worn")&&(supply!=PosterSupplyType.EmergencyCash||i.cells>0);}}
        public void Use(PreparednessSession s)
        {
            var i=GetComponent<PreparednessItem>();
            if(supply==PosterSupplyType.EmergencyCash&&!i.prepared)
            {
                if(!s.chapter||s.chapter.state.money<100){s.Say("Keep P100 from your household budget for the emergency pouch. It is not required to evacuate.");return;}
                s.chapter.state.money-=100;i.cells=100;
            }
            if(supply==PosterSupplyType.Whistle)Signal();
            i.prepared=true;i.closed=true;
            if(supply==PosterSupplyType.Boots){if(i.held)s.player.Release();i.storedIn="worn";i.SetPhysics(false);i.gameObject.SetActive(false);s.Say("Boots fitted and worn. They do not make floodwater safe to cross.");}
            else s.Say(instruction+" Checked and protected. Pack it in the open emergency bag.");
            s.RefreshEvidence();
        }
        static AudioClip whistle;
        public void Signal()
        {
            if(!whistle){const int rate=22050;var samples=new float[rate];for(int n=0;n<samples.Length;n++){float t=n/(float)rate;float pulse=t%.32f;samples[n]=pulse<.2f?Mathf.Sin(t*2*Mathf.PI*2400)*.12f*Mathf.Min(1,pulse*60)*Mathf.Min(1,(.2f-pulse)*60):0;}whistle=AudioClip.Create("Three short whistle sounds",samples.Length,1,rate,false);whistle.SetData(samples,0);}
            var listener=Object.FindFirstObjectByType<AudioListener>();AudioSource.PlayClipAtPoint(whistle,listener?listener.transform.position:transform.position,.6f);
        }
        public static string Summary(PreparednessSession s)=>"POSTER ESSENTIALS\n"+string.Join("\n",s.items.Where(i=>i.GetComponent<PosterSupply>()).Select(i=>(i.GetComponent<PosterSupply>().Complete?"[done] ":"[ ] ")+i.displayName));
    }
}
