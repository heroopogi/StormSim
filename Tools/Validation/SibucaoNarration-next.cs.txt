using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    [Serializable] public sealed class SibucaoNarrationCue
    {
        public string key,text;
        public AudioClip clip;
    }
    [DisallowMultipleComponent]
    public sealed class SibucaoNarration : MonoBehaviour
    {
        public SibucaoNarrationCue[] cues=Array.Empty<SibucaoNarrationCue>();
        AudioSource voice;PhysicalRunData run;SibucaoNarrationCue last;
        bool paused;float nextSpeech;int observedResults;
        SibucaoSession S=>GetComponent<SibucaoSession>();
        [Serializable] sealed class CaptionManifest { public SibucaoNarrationCue[] cues; }
        public bool Speaking=>voice&&(voice.isPlaying||paused);
        public string Caption=>Speaking&&last!=null?"Narrator: "+last.text:null;
        void Awake()
        {
            var captions=Resources.Load<TextAsset>("SibucaoNarrationText");
            if(captions){var manifest=JsonUtility.FromJson<CaptionManifest>(captions.text);foreach(var cue in cues){var updated=Array.Find(manifest.cues,c=>c.key==cue.key);if(updated!=null)cue.text=updated.text;}}
            var source=new GameObject("Route narration voice");source.transform.SetParent(transform,false);voice=source.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.volume=.85f;
        }
        public void InterruptForDialogue(float seconds)
        {if(voice)voice.Stop();paused=false;nextSpeech=Time.unscaledTime+seconds+1;}
        public bool PlayCue(string key,bool repeat=false,string progressKey=null)
        {
            var cue=Array.Find(cues,c=>c.key==key);
            if(cue==null||!cue.clip||!S)return false;
            string flag="narration:"+(progressKey??key);
            if(!repeat&&S.state.physical.flags.Contains(flag))return false;
            S.RouteChoices?.StopVoices();voice.Stop();voice.volume=.85f*SibucaoAudioMix.Speech;voice.clip=cue.clip;voice.Play();paused=false;last=cue;
            S.physicalDecisions.SetFlag(flag);S.SayNarration("Narrator: "+cue.text,cue.clip.length+.5f);
            nextSpeech=Time.unscaledTime+cue.clip.length+2;return true;
        }
        void Update()
        {
            var s=S;if(!s||!s.physicalDecisions)return;voice.volume=.85f*SibucaoAudioMix.Speech;
            if(run!=s.state.physical){run=s.state.physical;voice.Stop();paused=false;last=null;nextSpeech=0;observedResults=run.history.Count;}
            bool allowed=s.Mode==SibucaoMode.Playing&&(!s.home||!s.home.MovementLocked)&&(!s.missionGuide||!s.missionGuide.BlocksInput)&&(!s.inventory||!s.inventory.BlocksInput);
            if(!allowed){if(voice.isPlaying){voice.Pause();paused=true;}return;}
            if(paused){voice.UnPause();paused=false;float remaining=Mathf.Max(0,voice.clip.length-voice.time);nextSpeech=Time.unscaledTime+remaining+2;S.SayNarration("Narrator: "+last.text,remaining+.5f);}
            if(Keyboard.current!=null&&Keyboard.current.nKey.wasPressedThisFrame&&last!=null){PlayCue(last.key,true);return;}
            if(voice.isPlaying||Time.unscaledTime<nextSpeech||Time.unscaledTime<s.subtitleUntil)return;
            var guide=s.missionGuide;var routes=guide?guide.RouteChoices:null;
            float depth=s.player.WaterDepthAtFeet;
            if(depth>1.25f&&PlayCue("swim"))return;
            if(depth>=.35f&&depth<=1.25f&&PlayCue("wade"))return;
            if(run.stamina<20&&PlayCue("tired"))return;
            if(guide&&guide.InsideHome){PlayCue("home");return;}
            if(PlayCue("depart"))return;
            if(run.history.Count>observedResults){observedResults=run.history.Count;if(PlayCue("complete",false,"complete_"+observedResults))return;}
            if(routes&&routes.Working){PlayCue("side_"+routes.Working.id);return;}
            var station=guide?(guide.CurrentStation?guide.CurrentStation:guide.NextStation):null;
            if(station&&station.node&&Vector3.Distance(s.player.transform.position,station.InteractionPosition)<28)
                PlayCue("station_"+station.node.id);
        }
        void OnDisable(){if(voice)voice.Stop();paused=false;}
    }
}
