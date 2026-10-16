using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoRouteAudio:MonoBehaviour
    {
        public AudioClip SFX_Scream_PLACEHOLDER,SFX_CrowdUrgency_PLACEHOLDER,SFX_TierChange_PLACEHOLDER;
        public AudioSource wheelchair,collapsed,checkpoint,boat,tierChange;
        public int WheelchairTriggers{get;private set;}
        public int CollapsedTriggers{get;private set;}
        public int CheckpointTriggers{get;private set;}
        public int BoatTriggers{get;private set;}
        public int TierTriggers{get;private set;}
        public WaterTier currentTier;
        float swell,lastClock;
        bool suspended;
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s)return;
            if(s.Mode!=SibucaoMode.Playing)
            {
                foreach(var source in Sources())if(source){if(s.Mode==SibucaoMode.Paused)source.Pause();else source.Stop();}
                suspended=true;return;
            }
            if(suspended){foreach(var source in Sources())if(source)source.UnPause();suspended=false;}
            var f=s.GetComponent<FloodSimulationManager>();var tier=f?f.GetWaterTier():WaterTier.Tier1;
            if(s.state.elapsed<lastClock){currentTier=WaterTier.Tier1;swell=0;WheelchairTriggers=CollapsedTriggers=CheckpointTriggers=BoatTriggers=TierTriggers=0;foreach(var source in Sources())if(source)source.Stop();}lastClock=s.state.elapsed;
            if(tier!=currentTier){if(tier>currentTier)s.physicalDecisions.EnvironmentBeat();currentTier=tier;swell=1;TierTriggers++;if(tierChange){tierChange.transform.position=s.player.transform.position;if(SFX_TierChange_PLACEHOLDER){tierChange.clip=SFX_TierChange_PLACEHOLDER;tierChange.volume=SibucaoAudioMix.Effects;tierChange.Play();}}}
            swell=Mathf.MoveTowards(swell,0,Time.deltaTime*.12f);if(s.weather)s.weather.tierSwell=swell*.18f+(int)tier*.045f;
            if(Cue(wheelchair,SFX_Scream_PLACEHOLDER,false))WheelchairTriggers++;
            if(Cue(collapsed,SFX_Scream_PLACEHOLDER,false))CollapsedTriggers++;
            if(Cue(checkpoint,SFX_CrowdUrgency_PLACEHOLDER,true))CheckpointTriggers++;
            if(Cue(boat,SFX_CrowdUrgency_PLACEHOLDER,true))BoatTriggers++;
        }
        System.Collections.Generic.IEnumerable<AudioSource> Sources(){yield return wheelchair;yield return collapsed;yield return checkpoint;yield return boat;yield return tierChange;}
        bool Cue(AudioSource source,AudioClip clip,bool loop)
        {
            if(!source)return false;source.volume=SibucaoAudioMix.Speech;var s=SibucaoSession.Instance;
            bool near=(s.player.transform.position-source.transform.position).sqrMagnitude<225;
            string flag="audio_near_"+source.name;
            bool entered=near&&!s.state.physical.flags.Contains(flag);
            // A missing recording must not disconnect the event itself.
            if(entered)s.physicalDecisions.SetFlag(flag);
            if(near&&clip&&(loop||entered)&&!source.isPlaying){source.clip=clip;source.loop=loop;source.Play();}
            if(loop&&!near){if(source.isPlaying)source.Stop();s.state.physical.flags.Remove(flag);}
            return entered;
        }
    }
}
