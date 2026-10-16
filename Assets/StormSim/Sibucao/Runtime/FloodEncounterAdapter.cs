using UnityEngine;
namespace StormSim.Sibucao
{
    // Compatibility component for existing scenes and saves. Flooding affects the
    // real terrain; it must never manufacture elevated task lanes or access ramps.
    public sealed class FloodEncounterAdapter : MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public Material deckMaterial, railMaterial;
        public FloodVariantState State => station.manager.Data.floodVariants[station.index];
        public float Lift => 0;
        public bool Adapted => false;
        public string Objective => station.node.prompt;
        public Vector3 Entry => station.transform.position;
        public Vector3 DeckCenter => station.transform.position;
        public void ResetPresentation()
        {
            if(station.branches!=null)
                foreach(var branch in station.branches)if(branch)branch.ClearFloodChoice();
        }
        public void PrepareStaticAccess() { }
        public void RestorePresentation()
        {
            ResetPresentation();
            // Old platform height is consumed by the save migration before this reset.
            if(State!=null){State.band=0;State.lift=0;}
        }
        public void AfterActivation() { }
    }
    public sealed partial class SibucaoPhysicalOption
    {
        DecisionChoice floodChoice;
        public bool FloodAdapted=>floodChoice;
        public void SetFloodChoice(string label,PhysicalEncounterDefinition work,bool preserve)
        {
            if(!floodChoice){floodChoice=Instantiate(station.node.choices[choiceIndex]);floodChoice.hideFlags=HideFlags.DontSave;}
            floodChoice.label=label;floodChoice.outcome=label+" completed. Your supplies, assistance and consequences carry forward.";floodChoice.failureOutcome=label+" completed with a setback. Check your condition and remaining supplies.";floodChoice.minimumTier=WaterTier.Tier1;floodChoice.maximumTier=WaterTier.Tier5;
            if(work)Encounter.SetFloodWork(work,preserve);
        }
        public void ClearFloodChoice()
        {
            if(floodChoice)Destroy(floodChoice);floodChoice=null;
            if(encounterRunner)encounterRunner.RestoreGroundWork();

        }
    }
}

