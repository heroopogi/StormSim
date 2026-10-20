using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoPhysicalOption
    {
        [Tooltip("Optional physical-only override. B2-B5 use the encounter catalog by default; B1 has its own observe/probe/cross loop.")]
        public PhysicalEncounterDefinition encounterDefinition;
        PhysicalEncounterRunner encounterRunner;
        public bool HasEncounter => encounterDefinition || PhysicalEncounterCatalog.Supports(this);
        public PhysicalEncounterRunner Encounter
        {
            get
            {
                if (!HasEncounter) return null;
                if (!encounterRunner)
                {
                    encounterRunner = gameObject.AddComponent<PhysicalEncounterRunner>();
                    encounterRunner.Initialize(this, encounterDefinition);
                }
                return encounterRunner;
            }
        }
        public bool EncounterComplete => !HasEncounter || Encounter.Complete;
        public bool TryEncounterInput() => Engaged && HasEncounter && Encounter.Interact();
        bool FinishEncounterChoice()
        {
            if (!EncounterComplete) return false;
            if (HasEncounter) Attempt.activeSeconds = Mathf.Max(Attempt.activeSeconds, Encounter.Progress.activeSeconds);
            Attempt.finished = true;
            return station.manager.FinishPhysicalChoice(station, choiceIndex);
        }
    }
}
