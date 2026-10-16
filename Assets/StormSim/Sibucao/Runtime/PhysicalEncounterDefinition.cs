using System;
using UnityEngine;

namespace StormSim.Sibucao
{
    public enum EncounterWork { Inspect, Align, Clearance, Gather }

    [Serializable]
    public sealed class PhysicalEncounterPhase
    {
        public string title;
        public EncounterWork work;
        public float activeSeconds = 20;
        [Range(0, 1)] public float routeFraction;
        public string[] sequence;
    }

    [CreateAssetMenu(menuName = "StormSim/Physical encounter")]
    public sealed class PhysicalEncounterDefinition : ScriptableObject
    {
        public PhysicalEncounterPhase[] phases;
        public int OperationCount
        {
            get { int count=0;if(phases!=null)foreach(var p in phases)if(p?.sequence!=null)count+=p.sequence.Length;return count; }
        }
        public float MinimumSeconds
        {
            get { float sum = 0; if (phases != null) foreach (var p in phases) sum += Mathf.Max(0, p.activeSeconds); return Mathf.Max(18, sum); }
        }
        public bool Valid => phases != null && phases.Length >= 1 && phases.Length <= 3 && OperationCount >= 2 && OperationCount <= 3 && Array.TrueForAll(phases,
            p => p != null && p.activeSeconds > 0 && p.sequence != null && p.sequence.Length >= 1 && p.sequence.Length <= 3 && Array.TrueForAll(p.sequence, s => !string.IsNullOrEmpty(s)));
        public int PhaseOffset(int phase)
        {
            int offset=0;for(int i=0;i<phase&&i<phases.Length;i++)offset+=phases[i].sequence.Length;return offset;
        }
        public float WorkSeconds(int phase)
        {
            float sum = 0; foreach (var p in phases) sum += p.activeSeconds;
            return phases[phase].activeSeconds * MinimumSeconds / sum / phases[phase].sequence.Length;
        }
    }

    // Serializable physical progress only. No score, chance, inventory or branch flags.
    [Serializable]
    public sealed class PhysicalEncounterProgress
    {
        public int physicalVersion;
        public float[] displacement;
        public int phase, operation;
        public float activeSeconds;
        public string[] gatherItems;

        public bool Complete(PhysicalEncounterDefinition definition) => physicalVersion==3&&displacement!=null&&definition&&displacement.Length==definition.OperationCount&&
            definition && definition.Valid && phase >= definition.phases.Length && activeSeconds >= definition.MinimumSeconds&&
            Array.TrueForAll(displacement,x=>x>=1);

        public float ProposedPosition(PhysicalEncounterDefinition definition,float requested,float realDelta)
        {
            if(phase>=definition.phases.Length)return 1;
            float speed=1/definition.WorkSeconds(phase);
            return Mathf.Clamp01(displacement[definition.PhaseOffset(phase)+operation]+Mathf.Clamp(requested,-speed*Mathf.Clamp(realDelta,0,.1f),speed*Mathf.Clamp(realDelta,0,.1f)));
        }
        public void MovePhysical(PhysicalEncounterDefinition definition,float target,float realDelta)
        {
            if(phase>=definition.phases.Length)return;
            int index=definition.PhaseOffset(phase)+operation;float before=displacement[index];
            float next=ProposedPosition(definition,target-before,realDelta);
            displacement[index]=next;
            if(next>before)activeSeconds+=(next-before)*definition.WorkSeconds(phase);
        }
        public bool ReleasePhysical(PhysicalEncounterDefinition definition)
        {
            if(phase>=definition.phases.Length||displacement[definition.PhaseOffset(phase)+operation]<1)return false;
            operation++;if(operation==definition.phases[phase].sequence.Length){operation=0;phase++;}
            if(phase>=definition.phases.Length)activeSeconds=Mathf.Max(activeSeconds,definition.MinimumSeconds);
            return true;
        }

    }
}
