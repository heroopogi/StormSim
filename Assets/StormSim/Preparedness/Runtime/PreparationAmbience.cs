using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed class PreparationAmbience : MonoBehaviour
    {
        [Range(0,1)] public float natureVolume=.5f, musicVolume=.22f;
        public AudioClip natureLoop, peacefulMusicLoop;
        AudioSource nature,music;
        void Awake()
        {
            nature=Create("Nature ambience","Nature_Loop");
            music=Create("Peaceful preparation music","Peaceful_Music_Loop");
            if(natureLoop)nature.clip=natureLoop;
            if(peacefulMusicLoop)music.clip=peacefulMusicLoop;
        }
        AudioSource Create(string label,string resource)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;
            source.spatialBlend=0;source.volume=0;
            source.clip=Resources.Load<AudioClip>("PreparationAmbience/"+resource);
            return source;
        }
        void Update()
        {
            var game=SibucaoSession.Instance;
            var prep=GetComponent<PreparednessSession>();
            bool active=game && game.Mode==SibucaoMode.Preparation && prep.enabled &&
                !PreparationIntro.Blocking && !prep.evacuationOrdered && prep.phase!=PrepPhase.Fade;
            float level=active?game.settings.volume*game.settings.environmentVolume:0;
            // Leave room for the news and dialogue.
            if(active && (prep.phase==PrepPhase.Bulletin || prep.player.ReadingPanel))level*=.3f;
            var tv=GetComponent<PreparationTelevision>();
            Fade(nature,level*natureVolume);Fade(music,tv&&tv.PeacefulMusicReady?level*musicVolume:0);
        }
        static void Fade(AudioSource source,float target)
        {
            if(!source||!source.clip)return;
            source.volume=Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime*.3f);
            if(source.volume>0&&!source.isPlaying)source.Play();
            if(source.volume<=0&&source.isPlaying)source.Stop();
        }
        void OnDisable(){if(nature)nature.Stop();if(music)music.Stop();}
    }
}
