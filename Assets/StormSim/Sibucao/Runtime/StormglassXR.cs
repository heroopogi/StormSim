using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using StormSim.Sibucao;

namespace StormSim.UI
{
    [DefaultExecutionOrder(700)]
    public sealed class StormglassXR : MonoBehaviour
    {
        SibucaoPlayer player;
        CharacterController motor;
        StormglassComfortVignette comfortVignette;
        float gravity;
        bool active, turnHeld, gripHeld, packHeld, jumpHeld;
        public static bool WorkHeld => XRSettings.isDeviceActive && !StormglassUI.BlocksWorldInput && TriggerHeld();
        static bool TriggerHeld() { var d = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); return d.TryGetFeatureValue(CommonUsages.triggerButton, out bool held) && held; }
        void Awake() { player = GetComponent<SibucaoPlayer>(); motor = GetComponent<CharacterController>(); comfortVignette=gameObject.AddComponent<StormglassComfortVignette>(); }
        void OnEnable() { Application.onBeforeRender += Track; }
        void OnDisable() { Application.onBeforeRender -= Track; }
        void LateUpdate() { Track(); }
        void Track()
        {
            if (!XRSettings.isDeviceActive || !player || !player.view || !player.gameObject.activeInHierarchy) return;
            if (!active)
            {
                var systems = new List<XRInputSubsystem>(); SubsystemManager.GetSubsystems(systems);
                foreach (var system in systems) system.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                active = true;
            }
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) player.view.transform.localPosition = p;
            if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q)) player.view.transform.localRotation = q;
            var settings=SibucaoSession.Instance?SibucaoSession.Instance.settings:null;
            motor.height = Mathf.Clamp(player.view.transform.localPosition.y, (settings!=null && settings.seatedMode) ? .72f : 1.05f, 2.15f);
            motor.center = new Vector3(player.view.transform.localPosition.x, motor.height * .5f, player.view.transform.localPosition.z);
        }
        public void Step()
        {
            Track(); var s = SibucaoSession.Instance; if (!s) return;
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right.TryGetFeatureValue(CommonUsages.gripButton, out bool grip); left.TryGetFeatureValue(CommonUsages.gripButton, out bool pack);
            right.TryGetFeatureValue(CommonUsages.primaryButton, out bool jump); bool jumpDown = jump && !jumpHeld;
            bool carryDown = grip && !gripHeld, packDown = pack && !packHeld; gripHeld = grip; packHeld = pack; jumpHeld = jump;
            bool interact=SibucaoInteractionInput.Pressed;
            player.SetXRMovement(Vector2.zero,false,false,player.WaterDepthAtFeet);
            if (StormglassUI.BlocksWorldInput || s.Mode != SibucaoMode.Playing || s.home && s.home.MovementLocked || s.missionGuide && s.missionGuide.BlocksInput || s.inventory && s.inventory.BlocksInput) {comfortVignette.SetMotion(0);return;}
            if (carryDown) player.TryCarry(); if (packDown && player.grabber) player.grabber.PackCarried();
            if (interact) { if (s.missionGuide && s.missionGuide.FocusedPaper) s.missionGuide.Open(); else player.TryInteract(); }
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 move); right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turn);
            if(s.settings.smoothTurn)
            {
                if(Mathf.Abs(turn.x)>.12f)transform.RotateAround(player.view.transform.position,Vector3.up,turn.x*s.settings.smoothTurnSpeed*Time.deltaTime);
                turnHeld=false;
            }
            else
            {
                if (Mathf.Abs(turn.x) > .75f && !turnHeld) { transform.RotateAround(player.view.transform.position, Vector3.up, Mathf.Sign(turn.x) * s.settings.snapTurnAngle); turnHeld = true; }
                if (Mathf.Abs(turn.x) < .3f) turnHeld = false;
            }
            left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool sprint); right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool dive);
            var flood = s.GetComponent<FloodSimulationManager>(); float depth = flood ? flood.GetWaterDepth(transform.position) : 0;
            if (flood && flood.IsCatchingUp || s.state.physical.needsRescue) {comfortVignette.SetMotion(0);return;}
            sprint &= player.SprintAllowed(depth, s.state.physical.stamina, s.state.companion);
            move = Vector2.ClampMagnitude(move, 1);comfortVignette.SetMotion(Mathf.Max(move.magnitude,Mathf.Abs(turn.x)));player.SetXRMovement(move, sprint, WorkHeld, depth);
            if (player.swimming && player.swimming.Move(move, sprint, jump, dive, Time.deltaTime)) { gravity = 0; return; }
            if (s.physicalDecisions && (!s.passages || !s.passages.Held)) foreach (var station in s.physicalDecisions.stations)
                if (station.branches != null) foreach (var branch in station.branches)
                    if (branch && branch.Drive(motor, move, WorkHeld, jumpDown, jump, Time.deltaTime * SibucaoPlayer.WaterSpeedFactor(depth))) return;
            float speed = 2.2f * s.state.speedMultiplier * (sprint ? 1.6f : 1) * SibucaoPlayer.WaterSpeedFactor(depth);
            if (player.grabber) speed *= player.grabber.MovementScale;
            if (s.state.companion) speed = Mathf.Min(speed, 2.6f);
            if (s.passages && s.passages.Held) speed = Mathf.Min(speed, .6f);
            if (motor.isGrounded) { gravity = -2; player.lastGrounded = transform.position; } else gravity = Mathf.Max(-25, gravity - 20 * Time.deltaTime);
            var forward = Vector3.ProjectOnPlane(player.view.transform.forward, Vector3.up).normalized;
            if(s.settings.handRelativeMovement&&left.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion handRotation))
            {
                var handForward=Vector3.ProjectOnPlane(player.view.transform.parent?player.view.transform.parent.TransformDirection(handRotation*Vector3.forward):handRotation*Vector3.forward,Vector3.up).normalized;
                if(handForward.sqrMagnitude>.01f)forward=handForward;
            }
            var flow = flood ? Vector3.ClampMagnitude(flood.GetFlowVelocity(transform.position), .7f) * Mathf.Clamp01(depth) : Vector3.zero; flow.y = 0;
            motor.Move((forward * move.y * speed + Vector3.Cross(Vector3.up, forward) * move.x * speed + flow + Vector3.up * gravity) * Time.deltaTime);
        }
    }
}
