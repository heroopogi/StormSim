using UnityEngine;
using UnityEngine.Video;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed class PreparationPhoneMedia : MonoBehaviour
    {
        public VideoClip clip;
        public RenderTexture Texture {get;private set;}
        public bool Paused {get;private set;}
        public string Status {get;private set;}="Loading video…";
        VideoPlayer video;
        AudioSource audioSource;
        bool opened,failed;
        void Awake()
        {
            var host=new GameObject("Phone video playback");host.transform.SetParent(transform,false);
            audioSource=host.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
            video=host.AddComponent<VideoPlayer>();video.playOnAwake=false;video.isLooping=true;
            Texture=new RenderTexture(540,954,0){name="Phone portrait video"};
            video.renderMode=VideoRenderMode.RenderTexture;video.targetTexture=Texture;
            video.aspectRatio=VideoAspectRatio.FitInside;
            video.audioOutputMode=VideoAudioOutputMode.AudioSource;video.controlledAudioTrackCount=1;
            video.EnableAudioTrack(0,true);video.SetTargetAudioSource(0,audioSource);
            video.errorReceived+=OnError;
        }
        void OnError(VideoPlayer source,string message){failed=true;Status="Video unavailable";Debug.LogWarning("Phone video: "+message,this);}
        public void TogglePlayback(){Paused=!Paused;}
        void Update()
        {
            var prep=GetComponent<PreparednessSession>();var game=SibucaoSession.Instance;
            bool show=prep.enabled && prep.player.PhoneOpen && game && game.Mode==SibucaoMode.Preparation;
            if(!show){if(opened){video.Stop();opened=false;}return;}
            if(!opened){opened=true;Paused=false;failed=false;video.clip=clip;Status="Loading video…";}
            audioSource.volume=game.settings.speechVolume;
            if(!clip){Status="Video not assigned";return;}
            if(Paused){if(video.isPlaying)video.Pause();}
            else if(!video.isPlaying&&!failed)video.Play();
            if(!failed)Status=video.isPrepared?"":"Loading video…";
        }
        void OnDisable(){if(video)video.Stop();opened=false;}
        void OnDestroy(){if(video)video.errorReceived-=OnError;if(Texture){Texture.Release();Destroy(Texture);}}
    }
}
