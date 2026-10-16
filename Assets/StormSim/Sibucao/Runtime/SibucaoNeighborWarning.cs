using UnityEngine;

namespace StormSim.Sibucao
{
    // Uses the saved home clock so pause, loading and a new run preserve the sequence.
    public sealed class SibucaoNeighborWarning : MonoBehaviour
    {
        public SibucaoHomeOpening home;
        public SibucaoResident actor;
        [Header("Add the neighbour's recorded warning here later")]
        public AudioClip warningVoice;
        public AudioSource voice;
        public string warningLine="The rain is getting worse! Floodwater is coming!";
        public Vector3 approachStart=new Vector3(-4,0,-4.8f);
        public Vector3 doorstep=new Vector3(2.5f,0,-4.8f);
        public float runSpeed=2.8f,minimumWarningSeconds=8;
        bool voiceStarted,paused;
        float previousClock=-1;
        public bool Speaking { get; private set; }
        public string Subtitle => Speaking && home.Active &&
            Vector3.Distance(actor.transform.position,SibucaoSession.Instance.player.transform.position)<18 ? warningLine:null;

        void Update()
        {
            var s=SibucaoSession.Instance;if(!s||!home||!actor)return;if(voice)voice.volume=SibucaoAudioMix.Speech;
            float clock=s.state.homeSeconds;
            if(!home.Active||clock<previousClock){voiceStarted=false;voice.Stop();}
            previousClock=clock;
            float t=clock-home.wakeDuration-home.newsDuration;
            float speed=actor.casting?actor.casting.strideSpeed:runSpeed;
            float travel=Vector3.Distance(approachStart,doorstep)/Mathf.Max(.1f,speed);
            float duration=Mathf.Max(minimumWarningSeconds,warningVoice?warningVoice.length:0);
            float end=travel+duration;
            bool active=home.Active&&s.Mode!=SibucaoMode.Menu&&s.Mode!=SibucaoMode.Complete&&t>=0&&t<end+travel;
            actor.gameObject.SetActive(active);Speaking=active&&t>=travel&&t<end;
            if(!active){voice.Stop();return;}
            bool running=t<travel||t>=end;
            float u=t<travel?Mathf.Clamp01(t/travel):t<end?1:1-Mathf.Clamp01((t-end)/travel);
            Vector3 position=home.transform.TransformPoint(Vector3.Lerp(approachStart,doorstep,u));
            if(Physics.Raycast(position+Vector3.up*3,Vector3.down,out var ground,10,1,QueryTriggerInteraction.Ignore))position.y=ground.point.y+.025f;
            actor.transform.position=position;
            Vector3 forward=home.transform.TransformDirection(doorstep-approachStart)*(t<end?1:-1);
            if(Speaking){forward=s.player.transform.position-position;forward.y=0;}
            if(forward.sqrMagnitude>.001f)actor.transform.rotation=Quaternion.LookRotation(forward);
            actor.AnimateDirected(t,running);
            voice.transform.position=position+Vector3.up*1.5f;
            if(Speaking&&!voiceStarted&&s.Mode==SibucaoMode.Playing)
            {
                voiceStarted=true;
                if(warningVoice){voice.clip=warningVoice;voice.time=Mathf.Min(t-travel,Mathf.Max(0,warningVoice.length-.01f));voice.Play();}
            }
            bool shouldPause=s.Mode!=SibucaoMode.Playing;
            if(shouldPause&&!paused)voice.Pause();else if(!shouldPause&&paused)voice.UnPause();
            paused=shouldPause;
            if(t>=end)voice.Stop();
        }
        void OnDisable(){Speaking=false;if(voice)voice.Stop();if(actor)actor.gameObject.SetActive(false);}
    }
}
