using UnityEngine;
using UnityEngine.Rendering;

namespace StormSim.Sibucao
{
    public sealed class SibucaoWeather : MonoBehaviour
    {
        public Transform player;
        public ParticleSystem rain;
        public AudioSource rainAudio,windAudio,thunderAudio;
        public AudioClip[] thunder;
        public Light sun;
        public Material stormSky;
        public float tierSwell;
        public bool aftermath;
        public float Intensity=>strength;
        float strength=.3f,nextThunder,flash,thunderDelay=-1;
        bool storm;
        int quality=1;
        void Start(){
            if(rain){var collision=rain.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.quality=ParticleSystemCollisionQuality.High;collision.enableDynamicColliders=true;collision.lifetimeLoss=1;collision.radiusScale=.05f;collision.collidesWith=~0;collision.maxCollisionShapes=1024;}
            nextThunder=Time.time+Random.Range(18,30);RenderSettings.skybox=stormSky;ApplyLighting();}
        public void SetStorm(bool value){storm=value;}
        public void ApplyQuality(int value){quality=value; if(rain){var main=rain.main;main.maxParticles=value==0?2500:value==1?5000:8000;}}
        void ApplyLighting()
        {
            float brightness=SibucaoSession.Instance?SibucaoSession.Instance.settings.brightness:.7f;
            if(aftermath)
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.57f,.62f,.66f)*(.6f+brightness);
                RenderSettings.ambientEquatorColor=new Color(.43f,.46f,.46f)*(.6f+brightness);
                RenderSettings.ambientGroundColor=new Color(.24f,.23f,.21f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
                RenderSettings.fogColor=new Color(.65f,.69f,.71f);RenderSettings.fogDensity=.0018f;
                if(sun){sun.intensity=.65f;sun.color=new Color(1f,.95f,.85f);sun.useColorTemperature=false;}
                return;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            float darkness=Mathf.Lerp(1,.62f,Mathf.InverseLerp(.6f,1.35f,strength));
            RenderSettings.ambientSkyColor=new Color(.28f,.32f,.37f)*(.6f+brightness)*darkness;
            RenderSettings.ambientEquatorColor=new Color(.23f,.26f,.28f)*(.6f+brightness)*darkness;
            RenderSettings.ambientGroundColor=new Color(.13f,.14f,.15f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(.24f,.28f,.32f);RenderSettings.fogDensity=Mathf.Lerp(.006f,.011f,Mathf.InverseLerp(.6f,1.35f,strength));
            if(sun){sun.intensity=(.15f+brightness*.22f)*darkness+(SibucaoSession.Instance&&SibucaoSession.Instance.settings.reducedFlashes?0:flash*2.5f);sun.color=new Color(.7f,.78f,.9f);sun.useColorTemperature=false;}
        }
        void Update()
        {
            if(aftermath)
            {
                strength=0;flash=0;thunderDelay=-1;
                if(rain&&rain.isPlaying)rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(rainAudio)rainAudio.Stop();if(thunderAudio)thunderAudio.Stop();
                if(windAudio)windAudio.volume=.045f*SibucaoAudioMix.Environment;
                ApplyLighting();return;
            }
            var session=SibucaoSession.Instance;
            float target=storm?SibucaoStormTimeline.Intensity(session?session.home:null,session?session.state.homeSeconds:0):.3f;
            if(storm&&session&&session.physicalDecisions)
            {
                float progress=Mathf.InverseLerp(session.state.physical.routeStart,session.route.length,session.route.Progress(player.position));
                target=Mathf.Max(target,Mathf.Lerp(.6f,1.35f,progress)); // Geography/time only; never score, branch or roll.
            }
            strength=Mathf.MoveTowards(strength,target,Time.deltaTime*.3f);
            float severity=Mathf.InverseLerp(.6f,1.35f,strength);
            var exposure=session?session.GetComponent<SibucaoTyphoonExposure>():null;
            float gust=exposure?exposure.Gust01:SibucaoTyphoonExposure.GustEnvelope(session?session.state.homeSeconds:0);
            Vector3 wind=exposure?exposure.WindDirection:new Vector3(.82f,0,.57f).normalized;
            if(rain){rain.transform.position=player.position+Vector3.up*10;var emission=rain.emission;emission.rateOverTime=(quality==0?1200:quality==1?2600:4000)*strength;var v=rain.velocityOverLifetime;v.space=ParticleSystemSimulationSpace.World;v.x=wind.x*Mathf.Lerp(2,11,gust);v.z=wind.z*Mathf.Lerp(2,11,gust);}
            var experience=session?session.GetComponent<SibucaoEvacuationExperience>():null;
            float cover=exposure&&exposure.UnderCover?.32f:1;
            float windCover=exposure&&exposure.WindSheltered?.4f:1;
            if(rainAudio)rainAudio.volume=Mathf.Clamp01(.6f*strength+tierSwell)*SibucaoAudioMix.Environment*cover;
            if(windAudio)windAudio.volume=Mathf.Clamp01((.2f+.55f*gust)*strength+tierSwell)*SibucaoAudioMix.Environment*windCover;
            if(thunderAudio)thunderAudio.volume=SibucaoAudioMix.Environment;
            if(storm && Time.time>=nextThunder){flash=1;thunderDelay=Time.time+Random.Range(.6f,1.8f);nextThunder=Time.time+Mathf.Lerp(48,20,severity)+Random.Range(0,12);}
            if(thunderDelay>=0 && Time.time>=thunderDelay){if(thunder.Length>0)thunderAudio.PlayOneShot(thunder[Random.Range(0,thunder.Length)],.65f);thunderDelay=-1;}
            flash=Mathf.MoveTowards(flash,0,Time.deltaTime*5);ApplyLighting();
        }
    }
}
