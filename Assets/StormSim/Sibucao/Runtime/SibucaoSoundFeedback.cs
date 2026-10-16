using UnityEngine;
namespace StormSim.Sibucao
{
    public enum SibucaoSoundKind { DryStep,WetStep,Mechanism,Placement }
    // Small procedural foley clips: no external sample ownership or network dependency.
    public sealed class SibucaoSoundFeedback : MonoBehaviour
    {
        AudioSource source;AudioClip[] clips;float nextStep;Vector3 previous;Transform tracked;
        StormSim.Preparedness.SibucaoPreparationBridge preparation;
        public static SibucaoSoundFeedback Instance {get;private set;}
        void Awake()
        {
            Instance=this;preparation=GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
            clips=new AudioClip[4];for(int i=0;i<4;i++)clips[i]=Make((SibucaoSoundKind)i);
        }
        static AudioClip Make(SibucaoSoundKind kind)
        {
            const int rate=22050;float duration=kind==SibucaoSoundKind.WetStep?.24f:.13f;var data=new float[(int)(rate*duration)];
            var random=new System.Random(379+(int)kind);float low=0;
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.24f);float env=Mathf.Sin(Mathf.PI*i/data.Length)*Mathf.Exp(-t*22);data[i]=(low*.65f+Mathf.Sin(t*(kind==SibucaoSoundKind.Placement?1050:kind==SibucaoSoundKind.Mechanism?620:240))*.35f)*env*.28f;}
            var clip=AudioClip.Create("Sibucao "+kind,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public static void Play(SibucaoSoundKind kind)
        {if(Instance&&Instance.source){var p=SibucaoControls.Settings;Instance.source.volume=p?.effectsVolume??1;Instance.source.PlayOneShot(Instance.clips[(int)kind]);}}
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s||!s.player)return;
            source.volume=SibucaoAudioMix.Effects;
            bool preparing=preparation&&preparation.Active;
            var actor=preparing?preparation.preparation.player.transform:s.player.transform;
            var position=actor.position;
            if(tracked!=actor){tracked=actor;previous=position;return;}
            Vector3 delta=position-previous;previous=position;delta.y=0;float travelled=delta.magnitude;
            bool allowed=preparing?s.Mode==SibucaoMode.Preparation:s.Mode==SibucaoMode.Playing&&!s.player.vehicle;
            var motor=actor.GetComponent<CharacterController>();
            if(!allowed||!motor||!motor.isGrounded||travelled<.006f||travelled>1||Time.time<nextStep)return;
            bool running=preparing?preparation.preparation.player.Running:s.player.Sprinting;
            nextStep=Time.time+(running?.3f:.48f);
            Play(!preparing&&s.player.WaterDepthAtFeet>.08f?SibucaoSoundKind.WetStep:SibucaoSoundKind.DryStep);
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(clips!=null)foreach(var clip in clips)if(clip)Destroy(clip);}
    }
    public static class SibucaoAudioMix
    {
        public static float Environment
        {
            get{var s=SibucaoSession.Instance;if(!s)return 1;return s.settings.environmentVolume*(Time.unscaledTime<s.subtitleUntil?.5f:1);}
        }
        public static float Speech=>SibucaoControls.Settings?.speechVolume??1;
        public static float Effects=>SibucaoControls.Settings?.effectsVolume??1;
    }
}
