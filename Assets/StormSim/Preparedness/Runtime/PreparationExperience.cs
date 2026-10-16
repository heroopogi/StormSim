using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace StormSim.Preparedness
{
    public enum PreparationVoicePriority { Hint=0, Guidance=1, Warning=2, Emergency=3 }

    [Serializable]
    public sealed class PreparationVoiceCue
    {
        [Tooltip("Stable cue ID used by the game and as the optional Resources/PreparationVoice audio filename.")]
        public string id;
        [Tooltip("Optional imported narration. If empty, the subtitle still appears and Resources/PreparationVoice/<id> is checked.")]
        public AudioClip clip;
    }

    [DisallowMultipleComponent]
    public sealed class PreparationVoiceDirector : MonoBehaviour
    {
        public PreparationVoiceCue[] cues=new PreparationVoiceCue[0];
        [Range(0,1)] public float voiceVolume=.9f;
        public AudioSource output;

        readonly Dictionary<string,AudioClip> cache=new Dictionary<string,AudioClip>(StringComparer.OrdinalIgnoreCase);
        PreparationVoicePriority currentPriority;
        string lastCueId,lastSubtitle;
        PreparationVoicePriority lastPriority;
        string activeSubtitle="";
        float subtitleStartedAt,subtitleDuration;

        public bool IsSpeaking=>output&&output.isPlaying;
        public string LastCueId=>lastCueId;
        public bool SubtitleVisible=>!string.IsNullOrWhiteSpace(activeSubtitle)&&Time.unscaledTime<subtitleStartedAt+subtitleDuration;
        public string SubtitleText=>SubtitleVisible?SubtitleChunk(activeSubtitle,SubtitleProgress):"";
        float SubtitleProgress=>subtitleDuration<=0?1:Mathf.Clamp01((Time.unscaledTime-subtitleStartedAt)/subtitleDuration);

        void Awake(){BuildCache();EnsureOutput();}

        public void BuildCache()
        {
            cache.Clear();
            if(cues==null)return;
            foreach(var cue in cues)
                if(cue!=null&&!string.IsNullOrWhiteSpace(cue.id)&&cue.clip&&!cache.ContainsKey(cue.id))cache.Add(cue.id,cue.clip);
        }

        void EnsureOutput()
        {
            if(!output)output=GetComponent<AudioSource>();
            if(!output)output=gameObject.AddComponent<AudioSource>();
            output.playOnAwake=false;output.loop=false;output.spatialBlend=0;output.volume=voiceVolume;
        }

        public bool Speak(string cueId,string subtitle,PreparationVoicePriority priority=PreparationVoicePriority.Guidance,bool replayable=true)
        {
            if(string.IsNullOrWhiteSpace(cueId))return false;
            if(replayable){lastCueId=cueId;lastSubtitle=subtitle;lastPriority=priority;}
            EnsureOutput();
            if(output.isPlaying&&priority<currentPriority)return false;
            if(output.isPlaying)output.Stop();
            if(!cache.TryGetValue(cueId,out var clip))
            {
                clip=Resources.Load<AudioClip>("PreparationVoice/"+cueId);
                if(clip)cache[cueId]=clip;
            }
            activeSubtitle=subtitle??"";subtitleStartedAt=Time.unscaledTime;
            subtitleDuration=clip?Mathf.Max(1.5f,clip.length):Mathf.Clamp(WordCount(activeSubtitle)*.38f,2.5f,8f);
            currentPriority=priority;
            if(!clip)return false;
            output.clip=clip;output.volume=voiceVolume;output.Play();return true;
        }

        public static string SubtitleChunk(string text,float progress)
        {
            if(string.IsNullOrWhiteSpace(text))return "";
            var words=text.Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            int chunks=Mathf.Max(1,Mathf.CeilToInt(words.Length/6f));
            int chunk=Mathf.Min(chunks-1,Mathf.FloorToInt(Mathf.Clamp01(progress)*chunks));
            int first=chunk*6,count=Mathf.Min(6,words.Length-first);
            return string.Join(" ",words,first,count);
        }

        static int WordCount(string text)=>string.IsNullOrWhiteSpace(text)?0:text.Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Length;

        public bool ReplayLast(PreparednessSession session)
        {
            if(string.IsNullOrEmpty(lastCueId)||!session)return false;
            Speak(lastCueId,lastSubtitle,lastPriority,false);return true;
        }

        public void StopForEmergency()
        {
            if(output&&output.isPlaying&&currentPriority<PreparationVoicePriority.Emergency)output.Stop();
        }
    }

    public static class PreparationFeedback
    {
        public static bool Pulse(XRNode hand,float amplitude,float duration)
        {
            if(!XRSettings.isDeviceActive)return false;
            var device=InputDevices.GetDeviceAtXRNode(hand);
            return device.isValid&&device.SendHapticImpulse(0,Mathf.Clamp01(amplitude),Mathf.Clamp(duration,.015f,.35f));
        }
        public static void Pickup(bool leftHand=false)=>Pulse(leftHand?XRNode.LeftHand:XRNode.RightHand,.18f,.035f);
        public static void Confirm(bool both=false)
        {
            Pulse(XRNode.RightHand,.34f,.075f);
            if(both)Pulse(XRNode.LeftHand,.28f,.075f);
        }
        public static void Warning(){Pulse(XRNode.RightHand,.52f,.12f);}
    }

    public sealed partial class PreparednessSession
    {
        [Tooltip("Optional voice clips. Subtitles remain available when no clips are assigned.")]
        public PreparationVoiceDirector voiceDirector;
        public bool VoicePlaying=>voiceDirector&&voiceDirector.IsSpeaking;
        public string CurrentInstruction {get;private set;}="";
        public string CurrentInstructionText=>string.IsNullOrWhiteSpace(CurrentInstruction)?NextPreparationStep:CurrentInstruction;

        void EnsureExperienceSupport()
        {
            if(!voiceDirector)voiceDirector=GetComponent<PreparationVoiceDirector>();
            if(!voiceDirector)voiceDirector=gameObject.AddComponent<PreparationVoiceDirector>();
        }

        public void SpeakCue(string cueId,string speaker,string subtitle,PreparationVoicePriority priority=PreparationVoicePriority.Guidance,bool replayable=true)
        {
            EnsureExperienceSupport();
            if(cueId.StartsWith("prep.",StringComparison.OrdinalIgnoreCase)||
                cueId.StartsWith("checklist.",StringComparison.OrdinalIgnoreCase)||
                cueId.StartsWith("advisory.",StringComparison.OrdinalIgnoreCase)||
                cueId.Equals("bag.collected",StringComparison.OrdinalIgnoreCase))CurrentInstruction=subtitle;
            // Keep the narration available to saves/tests without also drawing it as a
            // generic notification. PreparationVoiceDirector owns the timed subtitle.
            message=string.IsNullOrWhiteSpace(speaker)?subtitle:speaker+": "+subtitle;
            messageTime=-100;
            voiceDirector.Speak(cueId,subtitle,priority,replayable);
        }

        public bool ReplayLastVoice()
        {
            EnsureExperienceSupport();
            if(voiceDirector.ReplayLast(this))return true;
            Say("No spoken guidance is available to replay yet.");return false;
        }
    }
}
