using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>Only route position and storm time control anticipatory pressure. Never scoring or random outcome rolls.</summary>
    public sealed class SibucaoRoutePressure:MonoBehaviour
    {
        public AudioSource distantCrowd,risingWater;
        public AudioClip SFX_DistantCrowd_PLACEHOLDER;
        public float Pressure {get;private set;}
        public int CrowdCueCount {get;private set;}
        float nextCrowd,lastClock;
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s)return;
            if(s.Mode!=SibucaoMode.Playing){if(risingWater)risingWater.Pause();if(distantCrowd)distantCrowd.Pause();return;}
            if(s.state.homeSeconds<lastClock){nextCrowd=0;CrowdCueCount=0;}lastClock=s.state.homeSeconds;
            Pressure=Mathf.Max(s.home?s.home.Severity:0,Mathf.InverseLerp(s.state.physical.routeStart,s.route.length,s.route.Progress(s.player.transform.position)));
            if(risingWater){risingWater.volume=Mathf.Lerp(.015f,.24f,Pressure)*SibucaoAudioMix.Environment;if(!risingWater.isPlaying&&risingWater.clip)risingWater.Play();risingWater.transform.position=s.player.transform.position;}
            if(s.state.homeSeconds>=nextCrowd)
            {
                nextCrowd=s.state.homeSeconds+Mathf.Lerp(34,9,Pressure);CrowdCueCount++;
                if(distantCrowd&&SFX_DistantCrowd_PLACEHOLDER){distantCrowd.clip=SFX_DistantCrowd_PLACEHOLDER;distantCrowd.transform.position=s.player.transform.position+s.player.transform.forward*16;distantCrowd.volume=Mathf.Lerp(.12f,.4f,Pressure)*SibucaoAudioMix.Speech;distantCrowd.Play();}
            }
        }
    }
}
