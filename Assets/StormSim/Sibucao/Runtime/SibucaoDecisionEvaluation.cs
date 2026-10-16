using System;
using UnityEngine;
namespace StormSim.Sibucao
{
    [Serializable] public sealed class DecisionEvaluation
    {
        public string role,category,consequence;public bool success;
        public int adjustedBasePoints,rawPoints;public float roll,chance;
    }
    // Pure branching kernel. Environmental values are inputs; this never changes flood state.
    public static class SibucaoDecisionEvaluation
    {
        public static int Maximum(ScoreCategory category)=>category==ScoreCategory.Preparedness?1500:category==ScoreCategory.Evacuation?1200:category==ScoreCategory.Teamwork?300:1000;
        public static float Fraction(ScoreCategory category,int score)=>Mathf.Clamp01(score/(float)Maximum(category));
        public static int SecondaryPoints(ScoreCategory category,int rawPoints)=>category==ScoreCategory.DecisionMaking?0:Mathf.RoundToInt(rawPoints*.3f);
        public static int ApplyPoints(int[] scores,ScoreCategory category,int rawPoints)
        {
            int i=(int)category,before=scores[i];
            scores[i]=Mathf.Clamp(before+rawPoints,0,Maximum(category));
            scores[(int)ScoreCategory.DecisionMaking]=Mathf.Clamp(scores[(int)ScoreCategory.DecisionMaking]+SecondaryPoints(category,rawPoints),0,1000);
            return scores[i]-before;
        }
        public static DecisionEvaluation Evaluate(DecisionChoice c,string role,int seed,int node,int choice,WaterTier tier,float rain,float causalModifier=0)
        {
            int points=c.basePoints;float chance=c.baseSuccessChance;
            role="Resident"; // Retain the serialized field for old saves, without role modifiers.
            if(!c.deterministicOutcome)chance=Mathf.Clamp(chance+causalModifier-c.tierRisk*(int)tier-c.rainfallRisk*Mathf.Clamp01(rain)+(DecisionTreeManager.SeedValue(seed,node,choice,17)*2-1)*c.seededChanceVariance,.05f,.98f);
            float roll=DecisionTreeManager.SeedValue(seed,node,choice,31);bool success=roll<chance;
            float[] weights={.6f,.8f,1,1.4f,1.8f};
            return new DecisionEvaluation{role=role,category=c.scoreCategory.ToString(),adjustedBasePoints=points,rawPoints=Mathf.RoundToInt((points-(success?0:c.failurePenalty))*weights[(int)tier]),chance=chance,roll=roll,success=success,consequence=(success?c.onSuccess:c.onFailure).ToString()};
        }
    }
    [Serializable] public sealed class ConsequenceNotice
    {
        public string id,message;public int destination;public bool shown;
    }
}
