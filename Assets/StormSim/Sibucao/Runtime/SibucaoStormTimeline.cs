using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>Scenario time compression, not measured rainfall or an event forecast.</summary>
    public static class SibucaoStormTimeline
    {
        public static float FloodStart(SibucaoHomeOpening home)=>home.wakeDuration+home.newsDuration+home.standDuration+home.preparationDuration;
        public static float Progress(float seconds,float riseDuration)=>Mathf.Clamp01(seconds/Mathf.Max(1,riseDuration));
        // Integral from 0..1 is one: preserves the authored rain budget while accelerating its delivery.
        public static float RainMultiplier(float seconds,float riseDuration)
        {
            float t=Progress(seconds,riseDuration);return .2f+2.4f*t*t;
        }
        public static float Intensity(SibucaoHomeOpening home,float clock)
        {
            if(!home)return .6f;
            float t=FloodDifficultySettings.Current.Fraction(clock-FloodStart(home));
            return Mathf.Lerp(.6f,1.35f,t*t);
        }
    }
}
