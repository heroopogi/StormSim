using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    /// <summary>Saved-clock gust cues and bounded movement. Not a calibrated wind forecast.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed class SibucaoTyphoonExposure : MonoBehaviour
    {
        public Vector3 prevailingDirection=new Vector3(.82f,0,.57f);
        [Range(0,.25f)] public float maximumDrift=.12f;
        [Range(.8f,2f)] public float braceReach=1.4f;
        public float Gust01 {get;private set;}
        public Vector3 WindDirection {get;private set;}=Vector3.right;
        public float WindSpeedMetresPerSecond=>Mathf.Lerp(4,24,Gust01);
        public bool UnderCover {get;private set;}
        public bool WindSheltered {get;private set;}
        public bool IsBracing=>braceCollider&&braceCollider.enabled&&braceCollider.gameObject.activeInHierarchy;
        public float MovementFactor {get;private set;}=1;
        public Vector3 DriftVelocity {get;private set;}
        public string PhaseLabel {get;private set;}="Lull";
        public Transform ContextTarget=>CanBrace?candidate.transform:null;
        public string Prompt=>IsBracing?"Braced · interact again to release; movement also releases contact":CanBrace?"Interact · steady yourself against this fixed support":Data!=null&&Data.stormBalance>.6f?"Footing strained · slow down and seek protected footing":null;
        SibucaoSession session;
        FloodSimulationManager flood;
        CharacterController motor;
        PhysicalRunData tracked;
        Collider candidate,braceCollider;
        Vector3 bracePosition;
        float braceStarted,nextSample,nextWarning,nextFall;
        readonly RaycastHit[] hits=new RaycastHit[32];
        PhysicalRunData Data=>session&&session.state!=null?session.state.physical:null;
        bool CanBrace=>candidate&&Gust01>.45f&&!IsBracing&&session.player.WaterDepthAtFeet<.15f;

        public static float GustEnvelope(float seconds)
        {
            float phase=Mathf.Repeat(seconds,32);
            if(phase<12)return .12f;
            if(phase<20)return Mathf.SmoothStep(.12f,1,(phase-12)/8);
            if(phase<24)return 1;
            return Mathf.SmoothStep(1,.12f,(phase-24)/8);
        }
        public static float HeadingResistance(Vector3 movement,Vector3 wind)
            =>movement.sqrMagnitude<.001f?0:Mathf.Max(0,-Vector3.Dot(movement.normalized,wind.normalized));
        void Awake(){session=GetComponent<SibucaoSession>();flood=GetComponent<FloodSimulationManager>();}
        public void CancelBrace(){braceCollider=null;MovementFactor=1;DriftVelocity=Vector3.zero;}
        void OnDisable(){CancelBrace();candidate=null;}
        void Update()
        {
            if(!session)session=SibucaoSession.Instance;
            if(!session||!session.player||!session.player.view||Data==null)return;
            if(tracked!=Data){tracked=Data;CancelBrace();candidate=null;nextSample=nextWarning=nextFall=0;}
            if(!motor)motor=session.player.GetComponent<CharacterController>();
            bool blocked=session.Mode!=SibucaoMode.Playing||session.home&&session.home.MovementLocked||
                session.player.vehicle||session.player.swimming&&session.player.swimming.IsSwimming||
                session.inventory&&session.inventory.BlocksInput||session.missionGuide&&session.missionGuide.BlocksInput||
                flood&&flood.IsCatchingUp||Data.needsRescue;
            if(blocked){CancelBrace();candidate=null;return;}
            float clock=session.state.homeSeconds;
            float severity=session.weather?Mathf.InverseLerp(.3f,1.35f,session.weather.Intensity):.5f;
            Gust01=Mathf.Lerp(.15f,.35f,severity)+GustEnvelope(clock)*Mathf.Lerp(.25f,.65f,severity);
            float phase=Mathf.Repeat(clock,32);
            PhaseLabel=phase<12?"Lull":phase<20?"Wind building":phase<24?"Strong gust":"Wind easing";
            Vector3 direction=Vector3.ProjectOnPlane(prevailingDirection,Vector3.up);
            WindDirection=direction.sqrMagnitude>.01f?direction.normalized:Vector3.right;
            if(Time.unscaledTime>=nextSample){nextSample=Time.unscaledTime+.12f;SampleEnvironment();}
            bool cancel=Keyboard.current!=null&&SibucaoControls.Button(Key.Q).wasPressedThisFrame;
#if UNITY_EDITOR
            cancel|=SibucaoEditorInput.Active&&SibucaoEditorInput.Cancel;
#endif
            if(IsBracing&&(!Eligible(braceCollider)||cancel||session.player.MoveInput.sqrMagnitude>.04f||
                Vector3.Distance(session.player.transform.position,bracePosition)>.35f||
                Vector3.Distance(session.player.view.transform.position,braceCollider.ClosestPoint(session.player.view.transform.position))>braceReach+.1f||
                clock<braceStarted||clock-braceStarted>5||Gust01<.4f))CancelBrace();
            float dt=Mathf.Min(Time.deltaTime,.1f);
            if(UnderCover)Data.stormShelterSeconds+=dt;else Data.stormExposureSeconds+=dt;
            float depth=session.player.WaterDepthAtFeet;
            Data.stormWetness=Mathf.Clamp01(Data.stormWetness+dt*(depth>.08f?.035f:UnderCover?-.008f:.012f));
            Vector3 forward=Vector3.ProjectOnPlane(session.player.view.transform.forward,Vector3.up).normalized;
            Vector3 movement=forward*session.player.MoveInput.y+Vector3.Cross(Vector3.up,forward)*session.player.MoveInput.x;
            bool moving=movement.sqrMagnitude>.04f;
            float exposure=WindSheltered?.25f:1;
            float risk=Gust01*exposure*(session.player.Sprinting?1.25f:moving?.7f:.2f)*(depth>.15f?1.3f:1);
            Data.stormBalance=Mathf.Clamp01(Data.stormBalance+dt*(IsBracing?-.6f:risk>.65f?(risk-.65f)*.35f:-.2f));
            MovementFactor=IsBracing?.15f:1-exposure*Gust01*(.04f+HeadingResistance(movement,WindDirection)*.16f);
            bool precise=session.player.ActivePhysicalOption||session.passages&&session.passages.Held||session.player.grabber&&session.player.grabber.held;
            precise|=session.GetComponent<SibucaoRouteIncidentSequence>()?.IsWorking==true;
            bool comfort=session.settings.reducedMotion||UnityEngine.XR.XRSettings.isDeviceActive;
            DriftVelocity=moving&&!IsBracing&&!precise&&!comfort&&motor&&motor.isGrounded?
                WindDirection*Mathf.Min(maximumDrift,.12f)*Mathf.InverseLerp(.75f,1,Gust01)*exposure:Vector3.zero;
            if(risk>.85f)Data.stamina=Mathf.Max(0,Data.stamina-dt*.2f);
            if(Data.stormBalance>.6f&&Time.unscaledTime>=nextWarning)
            {nextWarning=Time.unscaledTime+10;session.Notify("Footing strained","Slow down. A fixed support can steady you while you choose protected access.");}
            if(Data.stormBalance>=.98f&&Time.unscaledTime>=nextFall)
            {
                nextFall=Time.unscaledTime+8;Data.stormBalance=.35f;Data.stormStumbles++;
                Data.stamina=Mathf.Max(0,Data.stamina-3);
                var feedback=session.player.GetComponent<SibucaoPhysicalFeedback>();if(feedback)feedback.Stumble(1);
                session.GetComponent<SibucaoEvacuationExperience>()?.Record("storm-footing","Lost footing during exposed movement","Your movement was briefly interrupted.","Read increasing wind cues and reduce exertion before footing deteriorates.",false);
            }
        }
        public bool Interact()
        {
            if(IsBracing){CancelBrace();return true;}
            SampleEnvironment();if(!CanBrace)return false;
            braceCollider=candidate;bracePosition=session.player.transform.position;braceStarted=session.state.homeSeconds;
            Data.stormBraceCount++;session.player.Gesture(1);
            session.GetComponent<SibucaoEvacuationExperience>()?.Record("storm-brace","Steadied against a fixed support","You paused exposed movement. The storm continued.","A temporary grip is not a safe shelter or proof that the route is safe.",false);
            return true;
        }
        bool Eligible(Collider c)
        {
            if(!c||c.isTrigger||c.attachedRigidbody||c is TerrainCollider||c.transform.IsChildOf(session.player.transform))return false;
            if(c.GetComponentInParent<PhysicalGrabbable>()||c.GetComponentInParent<SibucaoResident>()||c.GetComponentInParent<Animator>())return false;
            var marker=c.GetComponentInParent<SibucaoStormSupport>();
            return marker&&marker.enabled;
        }
        bool Cast(Vector3 origin,Vector3 direction,float distance,out RaycastHit nearest)
        {
            nearest=default;float best=float.PositiveInfinity;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,distance,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {var h=hits[i];if(h.transform.IsChildOf(session.player.transform))continue;if(h.distance<best){best=h.distance;nearest=h;}}
            return best<float.PositiveInfinity;
        }
        void SampleEnvironment()
        {
            candidate=null;UnderCover=false;WindSheltered=false;
            var eye=session.player.view.transform;
            if(Cast(eye.position,Vector3.up,4,out var roof)&&Eligible(roof.collider))
                UnderCover=roof.collider.GetComponentInParent<SibucaoStormSupport>().sheltersRain;
            if(Cast(session.player.transform.position+Vector3.up,-WindDirection,2.5f,out var wall)&&Eligible(wall.collider))
                WindSheltered=wall.collider.GetComponentInParent<SibucaoStormSupport>().blocksWind;
            if(Cast(eye.position,eye.forward,braceReach,out var hit)&&Eligible(hit.collider)&&Mathf.Abs(hit.normal.y)<.55f&&
                hit.collider.GetComponentInParent<SibucaoStormSupport>().canBrace)candidate=hit.collider;
        }
    }
}
