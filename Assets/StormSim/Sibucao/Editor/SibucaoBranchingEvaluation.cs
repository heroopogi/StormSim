using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoBranchingEvaluation
    {
        const string Folder="Documentation/BranchingEvaluation";
        [Serializable] public sealed class Trial
        {
            public string test,role,category,consequence,expectedConsequence;
            public int repetition,seed,node,choice,adjustedBasePoints,rawPoints,expectedPoints,awardedPoints,expectedAwardedPoints;
            public float rain,chance,expectedChance,roll;public int tier;
            public bool success,expectedSuccess,accuracy,consistent;
        }
        [Serializable] public sealed class Report
        {
            public string generatedUtc,scope="Decision kernel; frozen Tier3/rain=.5 inputs; no flood simulation changes; five independent evaluations per case. Does not certify physical traversal.";
            public int cases,totalTrials,correctTrials,consistentRepeatedTests;public float accuracyPercent,reliabilityPercent;
            public List<Trial> trials=new List<Trial>();
        }
        // Independent specification oracle: independent hash, midpoint rounding and fixed role table.
        // The expected role bonuses do not come from the tested asset's modifiers.
        static float OracleDraw(int seed,int node,int choice,int salt)
        {
            unchecked{uint x=(uint)seed^((uint)node*73856093u)^((uint)choice*19349663u)^((uint)salt*83492791u);x=(x^(x>>16))*2146121005u;x=(x^(x>>15))*2221713035u;x^=x>>16;return (x&16777215u)/16777216f;}
        }
        static int RolePoints(int n,int c,string role)=>n==2&&c==0&&role=="Student"?-5:n==6&&c==0&&role=="Volunteer"?5:0;
        static float RoleChance(int n,string role)=>((n==5||n==7)&&role=="Parent"||n==7&&role=="Volunteer")?.04f:0;
        [MenuItem("Tools/StormSim/Branching/Run accuracy and reliability evaluation")]
        public static void MenuRun(){Debug.Log(Run());}
        public static string Run()
        {
            Directory.CreateDirectory(Folder);var report=new Report{generatedUtc=DateTime.UtcNow.ToString("O")};
            for(int n=0;n<9;n++)for(int c=0;c<(n==1||n==2||n==7?4:3);c++)
            {
                var asset=AssetDatabase.LoadAssetAtPath<DecisionChoice>($"Assets/StormSim/Sibucao/Data/PhysicalBranches/B{n+1}_C{c}.asset");
                if(!asset)throw new InvalidOperationException("Missing choice "+n+"/"+c);
                foreach(var role in new[]{"Student","Parent","Volunteer"})foreach(int seed in new[]{23421,10})
                {
                    string baseline=null;report.cases++;
                    for(int repeat=1;repeat<=5;repeat++)
                    {
                        // Instantiate fresh immutable input for each repetition; no retained trial state.
                        var input=UnityEngine.Object.Instantiate(asset);
                        var actual=SibucaoDecisionEvaluation.Evaluate(input,role,seed,n,c,WaterTier.Tier3,.5f);
                        float expectedChance=asset.deterministicOutcome?asset.baseSuccessChance:Math.Min(.98f,Math.Max(.05f,asset.baseSuccessChance+RoleChance(n,role)-asset.tierRisk*2-asset.rainfallRisk*.5f+(OracleDraw(seed,n,c,17)*2-1)*asset.seededChanceVariance));
                        bool expectedSuccess=OracleDraw(seed,n,c,31)<expectedChance;int expectedBase=asset.basePoints+RolePoints(n,c,role);
                        int expectedPoints=expectedBase-(expectedSuccess?0:asset.failurePenalty);
                        string expectedConsequence=(expectedSuccess?asset.onSuccess:asset.onFailure).ToString();
                        string fingerprint=JsonUtility.ToJson(actual);if(baseline==null)baseline=fingerprint;
                        var row=new Trial{test=$"B{n+1}-C{c}/{role}/seed{seed}",role=role,repetition=repeat,seed=seed,node=n,choice=c,tier=2,rain=.5f,category=actual.category,consequence=actual.consequence,expectedConsequence=expectedConsequence,adjustedBasePoints=actual.adjustedBasePoints,rawPoints=actual.rawPoints,expectedPoints=expectedPoints,chance=actual.chance,expectedChance=expectedChance,roll=actual.roll,success=actual.success,expectedSuccess=expectedSuccess,consistent=fingerprint==baseline};
                        row.accuracy=actual.adjustedBasePoints==expectedBase&&actual.rawPoints==expectedPoints&&Math.Abs(actual.chance-expectedChance)<.000001f&&actual.success==expectedSuccess&&actual.consequence==expectedConsequence&&actual.category==asset.scoreCategory.ToString();
                        int[] initialScores={100,100,100,100,100},limits={1500,1200,1000,1000,300};
                        row.awardedPoints=SibucaoDecisionEvaluation.ApplyPoints(initialScores,input.scoreCategory,actual.rawPoints);
                        int expectedTotal=Math.Min(limits[(int)asset.scoreCategory],Math.Max(0,100+expectedPoints));
                        // Primary Decision-Making awards no additional 30% bonus.
                        row.expectedAwardedPoints=expectedTotal-100;row.accuracy&=row.awardedPoints==row.expectedAwardedPoints;
                        report.trials.Add(row);if(row.accuracy)report.correctTrials++;if(row.consistent)report.consistentRepeatedTests++;UnityEngine.Object.DestroyImmediate(input);
                    }
                }
            }
            report.totalTrials=report.trials.Count;report.accuracyPercent=100f*report.correctTrials/report.totalTrials;report.reliabilityPercent=100f*report.consistentRepeatedTests/report.totalTrials;
            File.WriteAllText(Folder+"/accuracy-reliability.json",JsonUtility.ToJson(report,true));
            var csv=new List<string>{"test,repetition,role,seed,tier,rain,category,success,expected_success,adjusted_base_points,raw_points,expected_points,chance,expected_chance,consequence,expected_consequence,accuracy,consistent,awarded_points,expected_awarded_points"};
            foreach(var r in report.trials)csv.Add(string.Join(",",new[]{r.test,r.repetition.ToString(),r.role,r.seed.ToString(),r.tier.ToString(),r.rain.ToString(CultureInfo.InvariantCulture),r.category,r.success.ToString(),r.expectedSuccess.ToString(),r.adjustedBasePoints.ToString(),r.rawPoints.ToString(),r.expectedPoints.ToString(),r.chance.ToString("R",CultureInfo.InvariantCulture),r.expectedChance.ToString("R",CultureInfo.InvariantCulture),"\""+r.consequence+"\"","\""+r.expectedConsequence+"\"",r.accuracy.ToString(),r.consistent.ToString()}));
            for(int i=0;i<report.trials.Count;i++)csv[i+1]+=","+report.trials[i].awardedPoints+","+report.trials[i].expectedAwardedPoints;
            File.WriteAllLines(Folder+"/accuracy-reliability.csv",csv);
            return $"{report.cases} cases / {report.totalTrials} trials; accuracy {report.correctTrials}/{report.totalTrials} ({report.accuracyPercent:F2}%); consistency {report.consistentRepeatedTests}/{report.totalTrials} ({report.reliabilityPercent:F2}%).";
        }
    }
}
