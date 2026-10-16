using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness
{
    public sealed class PreparednessWasteBin : MonoBehaviour
    {
        public GameObject contents;
        void LateUpdate(){if(contents)contents.SetActive(Item.cells>0);}
        PreparednessItem Item=>GetComponent<PreparednessItem>();
        public void Toggle(){Item.closed=!Item.closed;Item.AnimateVisual();}
        public void Use(PreparednessSession s,PreparednessItem held)
        {
            if(!held){Toggle();s.Say(Item.closed?"Waste bin closed.":"Waste bin open. Bring collected litter or a filled refuse bag here.");return;}
            if(Item.closed){s.Say("Open the bin with an empty hand before disposing of waste.");return;}
            if(Vector3.Distance(held.WorldBounds().center,transform.position)>.95f){s.Say("Bring the waste closer to the open bin.");return;}
            var litter=s.items.Where(i=>i.storedIn=="tool"&&i.transform.IsChildOf(held.transform)).ToArray();
            if(held.kind==PrepKind.Litter)litter=new[]{held};
            bool refuse=held.id=="house-wastebags"&&s.items.Any(i=>i.id=="bag-refuse"&&i.prepared)&&!held.prepared;
            if(litter.Length==0&&!refuse){s.Say("Only collected litter or a filled household refuse bag belongs in this bin.");return;}
            foreach(var waste in litter){if(waste.held)s.player.Release();waste.storedIn="bin";waste.SetPhysics(false);waste.transform.SetParent(s.transform,true);waste.gameObject.SetActive(false);Item.cells++;}
            if(refuse){held.prepared=true;Item.cells++;}
            s.Say("Waste disposed of. Close the lid to finish.");s.RefreshEvidence();
        }
    }
}
