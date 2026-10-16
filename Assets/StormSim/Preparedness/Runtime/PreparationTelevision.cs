using UnityEngine;
using UnityEngine.Video;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    // Uses the selected home's physical screen while its storm controller is suspended.
    public sealed class PreparationTelevision : MonoBehaviour
    {
        public VideoClip dayBeforeNews, nextDayNews;
        [Min(.1f)] public float replayFadeSeconds=3;
        public bool PeacefulMusicReady {get;private set;}
        public bool PlaybackRequested {get;private set;}
        public float BroadcastLevel {get;private set;}=1;
        bool fadingReplay;
        PreparednessSession session;
        VideoPlayer video;
        AudioSource audioSource;
        RenderTexture texture;
        Renderer screen;
        Material original, material;
        bool failed;
        bool completionSent;
        string runId;
        void Awake()
        {
            session=GetComponent<PreparednessSession>();
            var host=new GameObject("Preparation TV playback");
            host.transform.SetParent(transform,false);
            audioSource=host.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;audioSource.spatialBlend=.25f;
            audioSource.minDistance=8;audioSource.maxDistance=30;
            video=host.AddComponent<VideoPlayer>();
            video.playOnAwake=false;video.isLooping=true;
            video.renderMode=VideoRenderMode.RenderTexture;
            texture=new RenderTexture(1280,720,0);
            video.targetTexture=texture;
            video.audioOutputMode=VideoAudioOutputMode.AudioSource;
            video.controlledAudioTrackCount=1;
            video.EnableAudioTrack(0,true);video.SetTargetAudioSource(0,audioSource);
            video.errorReceived+=OnError;
            video.loopPointReached+=OnReplay;
        }
        void OnReplay(VideoPlayer source)
        {
            if(source.clip==dayBeforeNews && session.phase!=PrepPhase.MorningPacking)fadingReplay=true;
        }
        void OnError(VideoPlayer source,string message)
        {
            failed=true;
            Debug.LogWarning("Preparation TV unavailable; showing the training bulletin. "+message,this);
        }
        void LateUpdate()
        {
            var game=SibucaoSession.Instance;
            var bridge=game?game.GetComponent<SibucaoPreparationBridge>():null;
            bool active=session.enabled && bridge && bridge.Active;
            if(!active){ReleaseScreen();return;}
            var target=bridge.firstHome?bridge.firstHome.screen:null;
            if(target!=screen)
            {
                ReleaseScreen();screen=target;
                if(screen){original=screen.sharedMaterial;material=new Material(original);screen.sharedMaterial=material;}
            }
            if(!screen)return;
            video.transform.position=screen.transform.position;
            var clip=session.phase==PrepPhase.MorningPacking?nextDayNews:dayBeforeNews;
            if(video.clip!=clip || runId!=session.runId)
            {
                video.Stop();video.clip=clip;failed=false;runId=session.runId;
                fadingReplay=false;PeacefulMusicReady=false;PlaybackRequested=false;BroadcastLevel=1;completionSent=false;
            }
            // The television is already broadcasting when Preparation opens. It is
            // diegetic scene media, so opening a guide must not restart or pause it.
            bool show=session.phase!=PrepPhase.Fade && session.phase!=PrepPhase.ReadyForStorm;
            bool play=show && game.Mode==SibucaoMode.Preparation;
            if(play && fadingReplay && !PeacefulMusicReady)
            {
                BroadcastLevel=Mathf.MoveTowards(BroadcastLevel,0,Time.unscaledDeltaTime/Mathf.Max(.1f,replayFadeSeconds));
                if(BroadcastLevel<=0)
                {
                    PeacefulMusicReady=true;video.Stop();
                    if(!completionSent){completionSent=true;session.NotifyTelevisionMediaFinished();}
                }
            }
            audioSource.volume=game.settings.volume*game.settings.speechVolume*BroadcastLevel;
            if(play && clip && !failed && !PeacefulMusicReady)
            {
                if(!video.isPlaying){video.Play();PlaybackRequested=true;}
            }
            else if(video.isPlaying)video.Pause();
            bool hasVideo=show && clip && video.isPrepared && !failed;
            material.SetTexture("_VideoTex",texture);
            material.SetFloat("_Power",show?BroadcastLevel:0);
            material.SetFloat("_HasVideo",hasVideo?1:0);
            material.SetFloat("_Glitch",0);
            // Never place instructional text over the physical television. If media
            // fails, the screen remains a screen; accessible guidance stays in the HUD.
            if(session.television)session.television.GetComponent<Renderer>().enabled=false;
        }
        public bool UsesMediaTiming=>dayBeforeNews&&PlaybackRequested&&!failed&&!PeacefulMusicReady;
        void ReleaseScreen()
        {
            if(video)video.Stop();
            if(screen && original)screen.sharedMaterial=original;
            if(material)Destroy(material);
            screen=null;material=null;original=null;
            if(session && session.television){session.television.text="";session.television.GetComponent<Renderer>().enabled=false;}
        }
        void OnDisable(){ReleaseScreen();}
        void OnDestroy()
        {
            ReleaseScreen();
            if(video)video.errorReceived-=OnError;
            if(video)video.loopPointReached-=OnReplay;
            if(texture){texture.Release();Destroy(texture);}
        }
    }
}
