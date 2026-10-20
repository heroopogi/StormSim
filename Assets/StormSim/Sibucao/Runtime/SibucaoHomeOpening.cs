using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace StormSim.Sibucao
{
    /// <summary>Scene-owned home opening. The run state owns all progress for pause/save/restart.</summary>
    public sealed class SibucaoHomeOpening : MonoBehaviour
    {
        [Header("Temporary desktop testing")]
        public bool allowTemporaryTvSkip = true;
        [Header("Timing in seconds")]
        [Min(1)] public float newsDuration = 60;
        [Min(1)] public float wakeDuration=6, standDuration=6;
        public string locationName="Northern courtyard";
        public Renderer eyelids;
        public SibucaoNeighborWarning neighborWarning;
        [Min(1)] public float preparationDuration = 180;
        [Min(1)] public float floodRiseDuration = 180;
        [Header("Drop your news video here")]
        public VideoClip newsVideo;
        public VideoPlayer television;
        public Renderer screen;
        public GameObject fallbackNews;
        public TextMesh newsText;
        public Light televisionGlow;
        [Header("House")]
        public Transform lyingView, standingPoint, doorHinge, doorPanel;
        public float doorOpenAngle = 100f;
        public Transform[] supplies;
        public string[] supplyNames = { "Pack drinking water", "Pack food and flashlight", "Protect important documents" };
        public Transform curtain;
        [Header("Neighborhood flood basin")]
        public Transform floodSurface;
        public Vector2 floodSize = new Vector2(110, 130);
        public float initialWaterHeight = -2.2f, finalWaterHeight = -.25f;
        public WindZone wind;
        Material tvMaterial;
        Material eyeMaterial;
        RenderTexture videoTexture;
        bool videoFailed;
        Vector3 cameraRest;
        float fovRest;
        bool wasLying;
        string areaName="Sibucao";
        public string Prompt { get; private set; }
        SibucaoSession S => SibucaoSession.Instance;
        float NewsEnd=>wakeDuration+newsDuration;
        float ControlAt=>NewsEnd+standDuration;
        public bool Active => S && S.home==this && S.state.homeSequence;
        public bool Watching => Active && S.state.homeSeconds < NewsEnd;
        public bool CanSkipTV => SibucaoDevelopmentControls.Enabled && allowTemporaryTvSkip && Watching && S.Mode==SibucaoMode.Playing;
        public void SkipTV()
        {
            if(!CanSkipTV)return;
            S.state.homeSeconds=NewsEnd;
            television.Stop();
            ApplyLyingPose();
            ApplyWorld();
            S.LockCursor();
        }
        public bool Standing => Active && !Watching && S.state.homeSeconds<ControlAt;
        public bool MovementLocked=>Watching||Standing;
        public bool Preparing => Active && !MovementLocked && S.state.homeSeconds < ControlAt + preparationDuration;
        public float Remaining => Mathf.Max(0, (Watching ? NewsEnd : Standing?ControlAt:ControlAt + preparationDuration) - S.state.homeSeconds);
        public float Severity => Active ? FloodDifficultySettings.Current.Fraction(S.state.homeSeconds-ControlAt-preparationDuration) : 0;
        public float WaterHeight => Mathf.Lerp(initialWaterHeight, finalWaterHeight, Severity);
        public int PackedCount { get { int n=0; if(S) for(int i=0;i<3;i++) if((S.state.homeSupplies & (1<<i))!=0)n++; return n; } }
        void Awake()
        {
            var area=Object.FindFirstObjectByType<CoastalAreaProfile>();
            if(area)
            {
                areaName=area.areaName;
                if(fallbackNews)foreach(var text in fallbackNews.GetComponentsInChildren<TextMesh>(true))
                    text.text=text.text.Replace("SIBUCAO",areaName.ToUpperInvariant()).Replace("Sibucao",areaName);
            }
            cameraRest = new Vector3(0,1.63f,0); fovRest = 72;
            tvMaterial = new Material(screen.sharedMaterial); screen.sharedMaterial = tvMaterial;
            videoTexture = new RenderTexture(1280,720,0){name="Sibucao TV video (runtime)"};
            television.playOnAwake=false; television.isLooping=true;
            var newsAudio=television.GetTargetAudioSource(0);if(newsAudio){newsAudio.volume=1;newsAudio.spatialBlend=.25f;newsAudio.minDistance=8;newsAudio.maxDistance=30;}
            television.renderMode=VideoRenderMode.RenderTexture; television.targetTexture=videoTexture;
            tvMaterial.SetTexture("_VideoTex",videoTexture);
            television.errorReceived += VideoError;
            if(eyelids){eyeMaterial=new Material(eyelids.sharedMaterial);eyelids.sharedMaterial=eyeMaterial;eyelids.enabled=false;}
        }
        void VideoError(VideoPlayer source,string message){videoFailed=true;Debug.LogWarning("News video unavailable; using the built-in bulletin. "+message,this);}
        public void Begin()
        {
            S.state.homeSequence=true; S.state.homeSeconds=0; S.state.homeSupplies=0; S.state.homeDoorOpen=false;
            Restore(); S.Notify("At home in "+areaName,"Watch the weather bulletin for one minute.");
        }
        public void Restore()
        {
            videoFailed=false; television.Stop(); television.clip=newsVideo;
            if(newsVideo && Watching){television.time=S.state.homeSeconds % Mathf.Max(1,(float)newsVideo.length); television.Play();}
            if(MovementLocked)ApplyLyingPose(); else ReleasePose(false);
            ApplyWorld();
        }
        void ApplyLyingPose()
        {
            if(!wasLying)S.player.Teleport(standingPoint.position,standingPoint.forward);
            float t=S.state.homeSeconds;
            Vector3 position=lyingView.position;Quaternion rotation=lyingView.rotation;
            if(Standing)
            {
                float u=Mathf.Clamp01((t-NewsEnd)/standDuration);
                Vector3 sitting=Vector3.Lerp(lyingView.position,standingPoint.position+standingPoint.up*1.2f,.8f);
                position=u<.55f?Vector3.Lerp(lyingView.position,sitting,Mathf.SmoothStep(0,1,u/.55f)):Vector3.Lerp(sitting,standingPoint.position+standingPoint.up*1.63f,Mathf.SmoothStep(0,1,(u-.55f)/.45f));
                rotation=Quaternion.Slerp(lyingView.rotation,standingPoint.rotation,Mathf.SmoothStep(0,1,u));
            }
            else position+=lyingView.up*(Mathf.Sin(t*1.7f)*.003f);
            if(UnityEngine.XR.XRSettings.isDeviceActive) { /* Headset pose stays under the player's control throughout the wake-up. */ }
            else {S.player.view.transform.SetPositionAndRotation(position,rotation);S.player.view.fieldOfView=62;}
            if(S.player.hands)S.player.hands.gameObject.SetActive(false);wasLying=true;
            if(eyelids)
            {
                eyelids.transform.SetParent(S.player.view.transform,false);eyelids.transform.localPosition=new Vector3(0,0,.055f);
                float open=t<1?0:t<2.8f?Mathf.SmoothStep(0,.65f,(t-1)/1.8f):t<3.25f?Mathf.Lerp(.65f,0,(t-2.8f)/.45f):t<5.3f?Mathf.SmoothStep(0,1,(t-3.25f)/2.05f):1;
                float blink=t-NewsEnd;if(blink>=0 && blink<.5f)open=1-Mathf.Sin(blink/.5f*Mathf.PI)*.92f;
                eyeMaterial.SetFloat("_Opening",open);eyelids.enabled=open<.999f;
            }
        }
        void ReleasePose(bool stand)
        {
            if(!wasLying)return;
            if(eyelids)eyelids.enabled=false;
            if(stand)S.player.Teleport(standingPoint.position,standingPoint.forward);
            S.player.view.transform.localPosition=cameraRest;S.player.view.transform.localRotation=Quaternion.identity;S.player.view.fieldOfView=fovRest;
            if(S.player.hands)S.player.hands.gameObject.SetActive(S.Mode==SibucaoMode.Playing);wasLying=false;
        }
        void Update()
        {
            Prompt=null;if(!S)return;
            var newsAudio=television?television.GetTargetAudioSource(0):null;if(newsAudio)newsAudio.volume=S.settings.volume*S.settings.speechVolume;
            if(S.home!=this)return;
            bool playing=S.Mode==SibucaoMode.Playing;
            if(Active && playing)
            {
                bool wasWatching=Watching;
                if(CanSkipTV)
                {
                    Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                    if(Keyboard.current!=null && StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).wasPressedThisFrame)SkipTV();
                }
                bool prepBefore=Preparing;
                var flood=S.GetComponent<FloodSimulationManager>();
                if(!flood||!flood.IsCatchingUp)S.state.homeSeconds+=Time.deltaTime;
                if(wasWatching&&!Watching)S.LockCursor();
                if(!MovementLocked&&wasLying)ReleasePose(true);
                if(prepBefore&&!Preparing)S.Notify("Floodwater is rising","Take your go-bag, open the front door, and evacuate to the school.");
            }
            if(MovementLocked && playing)ApplyLyingPose();
            if(!Active || S.Mode==SibucaoMode.Menu || S.Mode==SibucaoMode.Complete)ReleasePose(false);
            if(television.clip && !videoFailed)
            {
                if(playing&&Watching){if(!television.isPlaying)television.Play();}
                else if(television.isPlaying)television.Pause();
            }
            ApplyWorld();
            if(playing&&Active&&!MovementLocked&&!S.player.vehicle) {FindInteraction(false);if(UnityEngine.XR.XRSettings.isDeviceActive&&SibucaoInteractionInput.Pressed)TryInteract();}
        }
        public void Suspend(){ReleasePose(false);if(television)television.Stop();if(floodSurface)floodSurface.gameObject.SetActive(false);}
        public bool TryInteract(){return Active&&!MovementLocked&&FindInteraction(true);}
        bool FindInteraction(bool use)
        {
            var ray=StormSim.UI.StormglassInteractionRay.From(S.player.view);
            RaycastHit hit=default;float closest=float.PositiveInfinity;
            foreach(var candidate in Physics.RaycastAll(ray,2.8f,~0,QueryTriggerInteraction.Ignore))
            {
                if(candidate.collider.GetComponentInParent<SibucaoPlayer>()==S.player || candidate.distance>=closest)continue;
                closest=candidate.distance;hit=candidate;
            }
            if(float.IsPositiveInfinity(closest))return false;
            if(hit.transform==doorPanel || hit.transform.IsChildOf(doorPanel))
            {
                Prompt=S.state.homeDoorOpen?"E  Close front door":"E  Open front door";
                if(use)S.state.homeDoorOpen=!S.state.homeDoorOpen;return true;
            }
            for(int i=0;i<supplies.Length;i++)if(hit.transform==supplies[i]||hit.transform.IsChildOf(supplies[i]))
            {
                if(S.physicalDecisions)return false;
                if((S.state.homeSupplies&(1<<i))!=0)return false;
                Prompt="E: Take";
                if(use){S.state.homeSupplies|=1<<i;S.state.scores[0]+=10;}return true;
            }
            return false;
        }
        void ApplyWorld()
        {
            if(!S)return;
            doorHinge.localRotation=Quaternion.RotateTowards(doorHinge.localRotation,Quaternion.Euler(0,Active&&S.state.homeDoorOpen?doorOpenAngle:0,0),Time.deltaTime*120);
            if(!S.physicalDecisions)for(int i=0;i<supplies.Length;i++)supplies[i].gameObject.SetActive(!Active||(S.state.homeSupplies&(1<<i))==0);
            floodSurface.gameObject.SetActive(Active && Severity>0 && !S.GetComponent<FloodSimulationManager>());
            var p=floodSurface.position;p.y=WaterHeight;floodSurface.position=p;
            float t=Active?Mathf.Max(0,S.state.homeSeconds-wakeDuration):0;
            float glitch=Active && ((t>newsDuration-9 && t<newsDuration) || (t>12 && Mathf.Repeat(t,13)<.65f)) ? .85f : .035f;
            tvMaterial.SetFloat("_Glitch",glitch);tvMaterial.SetFloat("_Clock",t);
            bool hasVideo=Watching&&television.isPrepared&&!videoFailed&&newsVideo;
            tvMaterial.SetFloat("_Power",Watching?1:0);
            tvMaterial.SetFloat("_HasVideo",hasVideo?1:0);fallbackNews.SetActive(Watching&&!hasVideo);
            if(newsText)newsText.text=t<18?"TYPHOON APPROACHING\nHeavy rain and strong winds expected":t<36?"PUROK "+areaName.ToUpperInvariant()+"\nRainfall continues to intensify":t<newsDuration-6?"LOW-LYING AREAS MAY FLOOD\nWater levels are expected to rise":"SIGNAL INTERRUPTED\nWeather bulletin connection lost";
            if(televisionGlow)televisionGlow.intensity=Watching?.7f-glitch*.45f:0;
            if(wind)wind.windMain=1.5f+Severity*2+Mathf.Sin(t*.7f)*.35f;
            Shader.SetGlobalFloat("_SibucaoHomeGust",Active?1.2f+Severity*1.7f:0);
            if(curtain)curtain.localRotation=Quaternion.Euler(Mathf.Sin(t*2.3f)*5,0,Mathf.Sin(t*1.7f)*3);
        }
        public float Depth(Vector3 p)
        {
            if(S&&S.GetComponent<FloodSimulationManager>() is FloodSimulationManager flood)return flood.GetWaterDepth(p);
            if(!Active||Severity<=0||Mathf.Abs(p.x-floodSurface.position.x)>floodSize.x/2||Mathf.Abs(p.z-floodSurface.position.z)>floodSize.y/2)return 0;
            return Mathf.Max(0,WaterHeight-p.y);
        }
        void OnDestroy()
        {
            if(television)television.errorReceived-=VideoError;
            Shader.SetGlobalFloat("_SibucaoHomeGust",0);
            if(tvMaterial)Destroy(tvMaterial);if(videoTexture){videoTexture.Release();Destroy(videoTexture);}
            if(eyeMaterial)Destroy(eyeMaterial);
        }
    }
}
