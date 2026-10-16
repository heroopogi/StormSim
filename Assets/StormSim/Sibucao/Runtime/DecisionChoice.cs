using System;
using UnityEngine;

namespace StormSim.Sibucao
{
    public enum WaterTier { Tier1, Tier2, Tier3, Tier4, Tier5 }
    public enum DecisionCategory { Environmental, RouteNavigation, ElectricalFire, ResourceEquipment, PeopleSocial, RescueAuthority }
    public enum ScoreCategory { Preparedness, Evacuation, DecisionMaking, ResourceManagement, Teamwork }
    [Serializable] public struct RoleModifier { public string role; public int points; public float successChance; }
    [Flags] public enum PhysicalConsequence { None=0, Stumble=1, Injury=2, LoseItem=4, Exhaustion=8, LeaveResident=16, Diversion=32, DelayedInjury=64 }
    [CreateAssetMenu(menuName="StormSim/Sibucao/Decision Choice")]
    public sealed class DecisionChoice : ScriptableObject
    {
        public string label;
        public string id;
        [TextArea] public string outcome;
        [TextArea] public string failureOutcome;
        public DecisionCategory category;
        public int basePoints;
        [Range(0,1)] public float baseSuccessChance = 1;
        [Min(0)] public float timeCost;
        public RoleModifier[] roleModifiers = Array.Empty<RoleModifier>();
        public bool delayedConsequenceFlag;
        public WaterTier minimumTier = WaterTier.Tier1, maximumTier = WaterTier.Tier3;
        public int resourcePoints, teamworkPoints, evacuationPoints;
        public bool consumesFirstAid, recruitCompanion;
        [Range(.5f,1)] public float movementMultiplier = 1;
        public int failurePenalty = 8;
        public ScoreCategory scoreCategory=ScoreCategory.Evacuation;
        [Tooltip("Minimum real physical work/travel seconds on the shared storm clock. Never a hidden time jump.")]
        public bool physicalTimeCost=true;
        public string requiredItemTag,consumedItemTag,forbiddenFlag,requiredFlag;
        public string successFlag,delayedFlagId;
        public int delayedAtDecision=-1;
        public PhysicalConsequence onSuccess,onFailure;
        public float healthCost,staminaCost,failureHealthCost=8,failureStaminaCost=12;
        public string lossItemTag="flashlight";
        [Range(0,1)] public float rainfallRisk=.08f,tierRisk=.025f,seededChanceVariance=.05f;
        public bool deterministicOutcome;
    }
}
