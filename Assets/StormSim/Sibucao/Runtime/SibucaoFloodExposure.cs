using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoFloodExposure:MonoBehaviour
    {
        Vector3 previous;bool ready;
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s)return;var p=s.player.transform.position;
            if(!ready){previous=p;ready=true;return;}float distance=Vector3.Distance(p,previous);previous=p;
            if(s.Mode!=SibucaoMode.Playing||s.home&&s.home.MovementLocked||distance>1.2f||distance<.001f||s.player.MoveInput.sqrMagnitude<.01f)return;
            var f=s.GetComponent<FloodSimulationManager>();float depth=f?f.GetWaterDepth(p):0;if(depth<=.5f)return;
            var data=s.state.physical;data.waterExposureMetres+=distance;
            if(data.waterExposureMetres<(data.waterLossChecks+1)*8)return;
            int sample=data.waterLossChecks++;
            float chance=Mathf.Lerp(.08f,.24f,Mathf.InverseLerp(.5f,1.8f,depth));
            if(s.inventory.HasBag&&DecisionTreeManager.SeedValue(data.seed,127,sample,83)<chance)
            {var lost=s.inventory.RemoveTag(null,true);if(!string.IsNullOrEmpty(lost))s.Say(SibucaoInventory.ItemName(s.inventory.Find(lost))+" slipped out of the bag in the water.",4);}
        }
    }
}

