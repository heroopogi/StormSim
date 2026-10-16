using System;
using UnityEngine;
namespace StormSim.Sibucao
{
    [Serializable] public sealed class PhysicalPathVariant {public int home;public Vector3[] points;}
    [Serializable] public sealed class PhysicalBranchAttempt
    {
        public PhysicalEncounterProgress encounter;
        public int selected=-1,step,toolStep;
        public float enteredAt,activeSeconds,distance,toolSeconds;
        public bool engaged,finished,actionStarted,secondInput,helperCalled,helperArrived,contactConsequence;
        public Vector3 previousPosition,previousToolPosition,boatLocalPosition,helperLocalPosition;
        public Quaternion helperRotation;public bool hasPlacement;
    }
    [Serializable] public sealed class PhysicalChoiceResult
    {
        public string nodeId,choiceId,category,consequence,lostItem;
        public string parentBranch,branch;
        public string floodVariant;public int difficulty;public float localFloodDepth;
        public float causalChanceModifier;
        public string[] lostItems;
        public string[] clearedPassages;
        public float passageWorkSeconds,passageWorkDistance;
        public int node,choice,rawPoints,appliedPoints;
        public int secondaryPoints;
        public bool scoringRecorded;
        public bool success;
        public float roll,chance,startedAt,finishedAt,activeSeconds,distance,healthBefore,healthAfter,staminaBefore,staminaAfter;
        public WaterTier tier;
    }
    [Serializable] public sealed class PendingPhysicalConsequence
    {
        public string id;public int source,dueAt;public bool applied;
    }
}
