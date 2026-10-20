using System;
using UnityEngine;
namespace StormSim.Sibucao
{
    [Serializable] public sealed class FloodDifficultyProfile
    {
        public int level;
        public string title;
        public float peakDepth,peakSeconds=960,expectedRouteSeconds=1200;
        // Monotone, bounded storm envelope. Route position never pumps water into the grid.
        public float Fraction(float seconds)
        {
            float t=Mathf.Clamp01(seconds/Mathf.Max(1,peakSeconds));
            return Mathf.SmoothStep(0,1,t);
        }
        public float Limit(float seconds)=>peakDepth*Fraction(seconds);
        public float RequiredStageLimit(int completedRequiredStages)
        {
            if(completedRequiredStages<0)return 0;
            // Ten authored depths: departure plus each of the nine ordered core
            // missions. Side missions never change completedRequiredStages.
            float[][] depths={
                new[]{.05f,.09f,.14f,.20f,.27f,.34f,.42f,.50f,.56f,.60f},
                new[]{.06f,.12f,.19f,.27f,.36f,.46f,.57f,.67f,.75f,.80f},
                new[]{.08f,.17f,.28f,.40f,.54f,.70f,.87f,1.02f,1.13f,1.20f},
                new[]{.10f,.22f,.36f,.52f,.70f,.92f,1.15f,1.36f,1.50f,1.60f},
                new[]{.15f,.35f,.65f,1.00f,1.40f,1.85f,2.30f,2.75f,3.15f,3.50f}
            };
            return depths[Mathf.Clamp(level,1,5)-1][Mathf.Clamp(completedRequiredStages,0,9)];
        }
        public float EffectiveLimit(float seconds,int completedRequiredStages)=>Mathf.Max(Limit(seconds),RequiredStageLimit(completedRequiredStages));
        public float RainRate(float seconds)
        {
            float t=Mathf.Clamp01(seconds/Mathf.Max(1,peakSeconds));
            // Catchments feed runoff while the storm rises, then a small maintained rainfall.
            return peakDepth*(1.8f*6*t*(1-t)/Mathf.Max(1,peakSeconds)+.00004f);
        }
    }
    [CreateAssetMenu(menuName="STORMSIM/Five flood difficulties")]
    public sealed class FloodDifficultySettings:ScriptableObject
    {
        public FloodDifficultyProfile[] profiles=Defaults();
        public FloodDifficultyProfile Get(int level)=>profiles[Mathf.Clamp(level,1,5)-1];
        static readonly FloodDifficultyProfile[] fallback=Defaults();
        public static FloodDifficultyProfile Current
        {
            get {var s=SibucaoSession.Instance;int level=s?s.state.difficultyLevel:2;return s&&s.floodDifficulty?s.floodDifficulty.Get(level):fallback[Mathf.Clamp(level,1,5)-1];}
        }
        public static FloodDifficultyProfile[] Defaults()=>new[] {
            new FloodDifficultyProfile{level=1,title="Easy",peakDepth=.6f},
            new FloodDifficultyProfile{level=2,title="Normal",peakDepth=.8f},
            new FloodDifficultyProfile{level=3,title="Hard",peakDepth=1.2f},
            new FloodDifficultyProfile{level=4,title="Severe",peakDepth=1.6f},
            new FloodDifficultyProfile{level=5,title="Extreme",peakDepth=3.5f}
        };
        // Six water bands cover the five difficulty peaks and the initially dry route.
        public static int Band(float depth)=>depth<=.15f?0:depth<=.6f?1:depth<=.8f?2:depth<=1.2f?3:depth<=1.6f?4:5;
        public static readonly string[] BandNames={"Street","Raised work area","Evacuation landing","Rescue platform","High-water transfer","Upper evacuation deck"};
    }
    [Serializable] public sealed class FloodVariantState
    {
        public int band;
        public float localDepth,enteredAt,lift;
        public string variantId,parentPath;
    }
}
