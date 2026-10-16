using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace StormSim.Sibucao
{
    [DefaultExecutionOrder(500)]
    public sealed class SibucaoUnderwater:MonoBehaviour
    {
        public Material fogMaterial;
        public float Blend {get;private set;}
        public int FilteredSources=>filters.Count;
        Camera cameraView;Material material;GameObject veil;FloodSimulationManager flood;float nextAudioScan;
        ParticleSystemRenderer rainRenderer;bool rainWasVisible;
        struct FilterState {public AudioLowPassFilter filter;public float cutoff;public bool enabled,owned;}
        readonly List<FilterState> filters=new List<FilterState>();
        void Start()
        {
            cameraView=GetComponent<Camera>();if(!cameraView||!fogMaterial)return;
            var data=cameraView.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
            material=new Material(fogMaterial);veil=GameObject.CreatePrimitive(PrimitiveType.Quad);veil.name="Underwater sediment volume";Destroy(veil.GetComponent<Collider>());
            veil.transform.SetParent(transform,false);veil.layer=2;veil.GetComponent<Renderer>().sharedMaterial=material;veil.SetActive(false);
        }
        void LateUpdate()
        {
            if(!veil)return;var s=SibucaoSession.Instance;if(!flood&&s)flood=s.GetComponent<FloodSimulationManager>();
            bool visible=s&&s.Mode!=SibucaoMode.Menu&&s.Mode!=SibucaoMode.Complete&&(!s.home||!s.home.MovementLocked);
            float immersion=visible&&flood&&flood.TrySurface(transform.position,out float h,out _)?h-transform.position.y:-1;
            float target=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.015f,.10f,immersion));
            Blend=Mathf.MoveTowards(Blend,target,Time.unscaledDeltaTime*8);veil.SetActive(Blend>.001f);
            float near=cameraView.nearClipPlane+.025f,height=2*near*Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad);
            veil.transform.localPosition=new Vector3(0,0,near);veil.transform.localRotation=Quaternion.identity;veil.transform.localScale=new Vector3(height*cameraView.aspect*1.05f,height*1.05f,1);
            material.SetFloat("_Immersion",Blend);material.SetFloat("_HeadDepth",Mathf.Max(0,immersion));
            if(!rainRenderer&&s&&s.weather&&s.weather.rain){rainRenderer=s.weather.rain.GetComponent<ParticleSystemRenderer>();rainWasVisible=rainRenderer&&rainRenderer.enabled;}
            if(rainRenderer)rainRenderer.enabled=rainWasVisible&&Blend<.7f;
            if(Time.unscaledTime>=nextAudioScan){ScanAudio(s);nextAudioScan=Time.unscaledTime+2;}
            foreach(var state in filters)if(state.filter){state.filter.enabled=Blend>.001f||state.enabled;state.filter.cutoffFrequency=Mathf.Lerp(state.cutoff,650,Blend);}
        }
        void ScanAudio(SibucaoSession s)
        {
            foreach(var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(source.spatialBlend<.1f&&(!s||!s.weather||(source!=s.weather.rainAudio&&source!=s.weather.windAudio&&source!=s.weather.thunderAudio)))continue;
                bool known=false;foreach(var state in filters)if(state.filter&&state.filter.gameObject==source.gameObject){known=true;break;}if(known)continue;
                var filter=source.GetComponent<AudioLowPassFilter>();bool owned=!filter;if(!filter)filter=source.gameObject.AddComponent<AudioLowPassFilter>();
                filters.Add(new FilterState{filter=filter,cutoff=owned?22000:filter.cutoffFrequency,enabled=!owned&&filter.enabled,owned=owned});
            }
        }
        void OnDisable(){Blend=0;if(veil)veil.SetActive(false);if(rainRenderer)rainRenderer.enabled=rainWasVisible;foreach(var state in filters)if(state.filter){state.filter.cutoffFrequency=state.cutoff;state.filter.enabled=state.enabled;}}
        void OnDestroy(){foreach(var state in filters)if(state.owned&&state.filter)Destroy(state.filter);if(material)Destroy(material);if(veil)Destroy(veil);}
    }
}
