using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SibucaoPlayer : MonoBehaviour
    {
        public Camera view;
        public Transform[] fingerBones;
        public Transform hands;
        public float walkSpeed=4.1f, sensitivity=.11f;
        CharacterController controller;
        float pitch, vertical, gesture;
        int gestureKind;
        bool exhausted;
        public float WaterDepthAtFeet {get;private set;}
        public static float WaterSpeedFactor(float depth) => Mathf.Lerp(1f,.45f,Mathf.InverseLerp(.15f,1.25f,depth));
        public bool SprintAllowed(float depth,float stamina,bool companion)
        {
            if(stamina<=.01f)exhausted=true;
            if(stamina>=15)exhausted=false;
            return !exhausted&&!companion&&(depth<.35f||depth>1.25f);
        }
        public Vector3 lastGrounded;
        public SibucaoVehicle vehicle;
        public SibucaoVehicle nearbyVehicle;
        public PhysicalGrabber grabber;
        public SibucaoSwimming swimming;
        public Vector2 MoveInput {get;private set;}
        public bool UseHeld {get;private set;}
        public void SetXRMovement(Vector2 movement,bool sprint,bool use,float depth) { MoveInput=movement;Sprinting=sprint&&movement.sqrMagnitude>.01f;UseHeld=use;WaterDepthAtFeet=depth; }
        public bool Sprinting {get;private set;}
        public SibucaoPhysicalOption ActivePhysicalOption {get;private set;}
        void Awake(){controller=GetComponent<CharacterController>();lastGrounded=transform.position;swimming=GetComponent<SibucaoSwimming>();if(hands&&!hands.GetComponent<SibucaoHandPresentation>())hands.gameObject.AddComponent<SibucaoHandPresentation>().player=this;}
        public void Teleport(Vector3 p,Vector3 forward)
        {
            if(!controller)controller=GetComponent<CharacterController>();
            controller.enabled=false;transform.position=p;forward.y=0;
            if(forward.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(forward);
            controller.enabled=true;vertical=0;pitch=0;view.transform.localRotation=Quaternion.identity;lastGrounded=p;if(swimming)swimming.ResetMotion();
        }
        public void Gesture(int kind=0){gesture=1;gestureKind=kind;}
        void Update()
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive && TryGetComponent<StormSim.UI.StormglassXR>(out var xr)) { xr.Step(); return; }
            var s=SibucaoSession.Instance;
            if(s && s.Mode==SibucaoMode.Cinematic)return;
            gesture=Mathf.MoveTowards(gesture,0,Time.unscaledDeltaTime*.8f);
            if(fingerBones!=null)for(int i=0;i<fingerBones.Length;i++)if(fingerBones[i])fingerBones[i].localRotation=Quaternion.Euler((i%5==0?12:vehicle?48:12)+gesture*(gestureKind==1?(i%5==0?20:52):gestureKind==2?(i%5==1?-8:32):-8),i%5==0?(i<5?45:-45):0,0);
            bool driven=false;
#if UNITY_EDITOR
            driven=SibucaoEditorInput.Active;
#endif
            if(!s || s.Mode!=SibucaoMode.Playing || !Application.isFocused&&!driven)return;
            var loadingFlood=s.GetComponent<FloodSimulationManager>();
            if(loadingFlood&&loadingFlood.IsCatchingUp||s.state.physical.needsRescue){MoveInput=Vector2.zero;Sprinting=UseHeld=false;return;}
            if(s.home && s.home.MovementLocked)return;view.fieldOfView=s.settings.fieldOfView;
            if(s.missionGuide && s.missionGuide.BlocksInput || s.inventory && s.inventory.BlocksInput)return;
            var kb=Keyboard.current;var mouse=Mouse.current;if(kb==null||mouse==null)return;
            if(!driven&&Cursor.lockState!=CursorLockMode.Locked){if(mouse.leftButton.wasPressedThisFrame)s.LockCursor();return;}
            bool interact=SibucaoInteractionInput.Pressed;
            Vector2 look=mouse.delta.ReadValue()*s.settings.sensitivity;if(s.settings.invertLook)look.y=-look.y;
            if(grabber&&grabber.held&&grabber.held.kind!=PhysicalItemKind.Assist&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.R).isPressed)look=Vector2.zero;
#if UNITY_EDITOR
            if(driven){interact=SibucaoEditorInput.Take(ref SibucaoEditorInput.Interact);look=SibucaoEditorInput.Look;SibucaoEditorInput.Look=Vector2.zero;}
#endif
            bool workGrip=SibucaoControls.WorkHeld;
            var sideRoutes=s.physicalDecisions?s.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;
            if(sideRoutes&&sideRoutes.Working&&sideRoutes.Working.Manipulating)look=Vector2.zero;
#if UNITY_EDITOR
            if(driven)workGrip=SibucaoEditorInput.UseHeld;
#endif
            if(s.physicalDecisions)foreach(var station in s.physicalDecisions.stations)
            {
                var active=station.EngagedBranch;
                if(active&&active.HasEncounter&&active.Encounter.Manipulate(look,workGrip,Mathf.Min(Time.deltaTime,Time.unscaledDeltaTime)))
                {look=Vector2.zero;break;}
            }
            if(!driven&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.F).wasPressedThisFrame&&TryCarry())return;
            if(!driven&&!s.DebugTimeInputThisFrame&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.P).wasPressedThisFrame&&grabber&&grabber.PackCarried())return;
            if(interact&&(!SibucaoControls.GripReleasedThisFrame||s.passages&&s.passages.Held&&s.passages.Held.State.opening>=.98f)&&TryInteract())return;
            bool cancel=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Q).wasPressedThisFrame;
#if UNITY_EDITOR
            if(driven)cancel=SibucaoEditorInput.Take(ref SibucaoEditorInput.Cancel);
#endif
            if(cancel)SibucaoControls.ResetGrip();
            if(cancel&&s.physicalDecisions)
            {
                if(grabber&&grabber.held)grabber.Release();
                if(sideRoutes&&sideRoutes.Working)sideRoutes.Working.Cancel();
                if(s.passages)s.passages.Held=null;
                foreach(var station in s.physicalDecisions.stations)if(station.EngagedBranch)station.EngagedBranch.CancelInteraction();
                ActivePhysicalOption=null;MoveInput=Vector2.zero;return;
            }
            if(vehicle && vehicle.Cockpit)vehicle.Cockpit.Look(look);
            else{transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-80,80);view.transform.localRotation=Quaternion.Euler(pitch,0,0);}
            nearbyVehicle=vehicle?null:SibucaoVehicle.LookAt(this);
            if(vehicle)
            {
                vehicle.Drive((StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.W).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.S).isPressed?1:0),(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.D).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.A).isPressed?1:0),StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).isPressed,Time.deltaTime);
                if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame)vehicle.TryExit();
                return;
            }
            if(nearbyVehicle && StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame){nearbyVehicle.TryEnter(this);return;}
            Vector2 move=new Vector2((StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.D).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.A).isPressed?1:0),(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.W).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.S).isPressed?1:0));
            bool sprint=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.LeftShift).isPressed,jump=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).wasPressedThisFrame,ascend=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).isPressed,dive=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.LeftCtrl).isPressed||kb.cKey.isPressed;
#if UNITY_EDITOR
            if(driven){move=SibucaoEditorInput.Move;sprint=SibucaoEditorInput.Sprint;jump=SibucaoEditorInput.Take(ref SibucaoEditorInput.Jump);ascend=SibucaoEditorInput.Ascend;dive=SibucaoEditorInput.Dive;}
#endif
            var flood=s.GetComponent<FloodSimulationManager>();
            WaterDepthAtFeet=flood?flood.GetWaterDepth(transform.position):0;
            sprint&=SprintAllowed(WaterDepthAtFeet,s.state.physical.stamina,s.state.companion);
            move=Vector2.ClampMagnitude(move,1);MoveInput=move;Sprinting=sprint&&move.sqrMagnitude>.01f;UseHeld=SibucaoControls.WorkHeld;
#if UNITY_EDITOR
            if(driven)UseHeld=SibucaoEditorInput.UseHeld;
#endif
            var feedback=GetComponent<SibucaoPhysicalFeedback>();
            ActivePhysicalOption=null;
            if(feedback){feedback.Squeeze=0;if(feedback.Impaired){controller.Move(Vector3.down*2*Time.deltaTime);return;}}
            controller.radius=.27f;ActivePhysicalOption=null;
            if(s.physicalDecisions)foreach(var station in s.physicalDecisions.stations)
                if(station.EngagedBranch){ActivePhysicalOption=station.EngagedBranch;break;}
            // Swimming must take control before a dry-land climb/squeeze can move the body.
            if(swimming&&swimming.Move(move,sprint,ascend,dive,Time.deltaTime)){vertical=0;return;}
            if(s.physicalDecisions&&(!s.passages||!s.passages.Held))foreach(var station in s.physicalDecisions.stations)
            {
                var branch=station.EngagedBranch;if(branch)ActivePhysicalOption=branch;
                if(station.branches!=null)foreach(var option in station.branches)
                    if(option&&option.Drive(controller,move,UseHeld,jump,ascend,Time.deltaTime*WaterSpeedFactor(WaterDepthAtFeet))){ActivePhysicalOption=option;return;}
            }
            float speed=walkSpeed*s.state.speedMultiplier*(sprint&&!s.state.companion?1.6f:1);
            var incidentDirector=s.GetComponent<SibucaoIncidentDirector>();
            if(incidentDirector)speed*=incidentDirector.MovementFactor;
            if(grabber)speed*=grabber.MovementScale;
            speed*=WaterSpeedFactor(WaterDepthAtFeet);
            var typhoon=s.GetComponent<SibucaoTyphoonExposure>();
            if(typhoon)speed*=typhoon.MovementFactor;
            if(s.state.companion)speed=Mathf.Min(speed,2.6f);
            if(ActivePhysicalOption)speed=Mathf.Min(speed,ActivePhysicalOption.SpeedLimit);
            if(s.passages&&s.passages.Held)speed=Mathf.Min(speed,.6f);
            if(feedback)speed*=feedback.MovementFactor;
            // Exhaustion limits exertion, without slowing ordinary dry walking into a spiral.
            if(WaterDepthAtFeet>=.35f||grabber&&grabber.held)speed*=Mathf.Lerp(.8f,1,Mathf.Clamp01(s.state.physical.stamina/30));
            if(controller.isGrounded){vertical=-2;lastGrounded=transform.position;}else vertical=Mathf.Max(-35,vertical-20*Time.deltaTime);
            if(grabber&&controller.isGrounded&&jump&&WaterDepthAtFeet<.35f&&(!grabber.held||grabber.held.kind!=PhysicalItemKind.Assist))vertical=5.5f;
            var current=flood?Vector3.ClampMagnitude(flood.GetFlowVelocity(transform.position),.7f)*Mathf.Clamp01(WaterDepthAtFeet):Vector3.zero;current.y=0;
            var windDrift=typhoon?typhoon.DriftVelocity:Vector3.zero;
            controller.Move((transform.right*move.x*speed+transform.forward*move.y*speed+current+windDrift+Vector3.up*vertical)*Time.deltaTime);
            if(transform.position.y < -35 || (transform.position-lastGrounded).sqrMagnitude>1600 && !controller.isGrounded){Teleport(lastGrounded+Vector3.up*.2f,transform.forward);s.Notify("Footing recovered","You return to the last stable footing.");}
            float bob=s.settings.reducedMotion?0:move.magnitude*Mathf.Sin(Time.time*8)*.008f;
            if(hands){hands.localPosition=new Vector3(0,-.25f+bob,.44f-gesture*.03f);hands.localRotation=Quaternion.identity;}
            if(!s.physicalDecisions&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame && s.nearby!=null){Gesture(s.nearby.index==3?1:s.nearby.index>3?2:0);s.OpenDecision(s.nearby);}
        }
        public bool TryCarry()
        {
            var s=SibucaoSession.Instance;if(!s||!s.CanMove||vehicle||s.home&&s.home.MovementLocked||s.inventory&&s.inventory.BlocksInput||s.missionGuide&&s.missionGuide.BlocksInput)return false;
            var choices=s.physicalDecisions?s.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;
            if(choices&&choices.HeldBy){choices.HeldBy.ReleaseCarry();return true;}
            if(grabber&&grabber.held)return grabber.CarryOrRelease();
            var mission=choices?choices.Focused():null;if(mission&&mission.Step!=null&&mission.Step.work==RouteWork.Carry)return mission.Interact();
            return grabber&&grabber.CarryOrRelease();
        }
        public bool TryInteract()
        {
            var s=SibucaoSession.Instance;
            if(!s||s.Mode!=SibucaoMode.Playing||s.home&&s.home.MovementLocked||s.missionGuide&&s.missionGuide.BlocksInput||s.inventory&&s.inventory.BlocksInput)return false;
            var incidentDirector=s.GetComponent<SibucaoIncidentDirector>();
            if(incidentDirector&&incidentDirector.Interact())return true;
            var experience=s.GetComponent<SibucaoEvacuationExperience>();
            if(experience&&experience.Interact())return true;
            if(s.home&&s.home.TryInteract())return true;
            var optional=s.GetComponent<SibucaoOptionalSideMission>();
            if(optional&&optional.Interact())return true;
            var floodedStreet=s.GetComponent<SibucaoFloodedStreetIncident>();
            if(floodedStreet&&floodedStreet.Interact())return true;
            var routeIncidents=s.GetComponent<SibucaoRouteIncidentSequence>();
            if(routeIncidents&&routeIncidents.Interact())return true;
            var chapter=s.GetComponent<SibucaoContinuousChapterOne>();
            if(chapter&&chapter.Interact())return true;
            var choices=s.physicalDecisions?s.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;
            if(choices&&choices.Interact())return true;
            if(s.GetComponent<StormSim.Preparedness.PreparationContinuity>()?.Interact()==true)return true;
            var workPart=PhysicalEncounterRunner.FocusedPart();
            if(workPart&&workPart.owner.Interact())return true;
            if(s.physicalDecisions)foreach(var station in s.physicalDecisions.stations)
            {var current=station.EngagedBranch;if(current&&current.TryEncounterInput())return true;}
            if(s.passages&&s.passages.Interact())return true;
            if(grabber&&grabber.ToggleGrab())return true;
            var typhoon=s.GetComponent<SibucaoTyphoonExposure>();
            if(typhoon&&typhoon.Interact())return true;
            return !vehicle&&s.physicalDecisions&&s.physicalDecisions.TryInteract();
        }
        void OnApplicationFocus(bool focused){
#if UNITY_EDITOR
            if(SibucaoEditorInput.Active)return;
#endif
            if(!focused && SibucaoSession.Instance && SibucaoSession.Instance.Mode==SibucaoMode.Playing)SibucaoSession.Instance.Pause();}
    }
}
