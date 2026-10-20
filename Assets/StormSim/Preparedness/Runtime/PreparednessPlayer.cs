using UnityEngine;
using StormSim.Sibucao;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Linq;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace StormSim.Preparedness
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PreparednessPlayer : MonoBehaviour
    {
        public PreparednessSession session;
        public Camera view;
        public Transform rightHand,leftHand;
        public TextMesh worldPrompt;
        public Renderer vrFade;
        public PreparednessItem held,focused,carriedBag;
        public float reach=2.3f,speed=2.2f;
        public float runSpeed=6.5f;
        public float jumpHeight=1.0f;
        bool jumpRequested,lastJump;
        public bool Running {get;private set;}
        bool exhausted;
        public bool LadderClimbing {get;set;}
        CharacterController motor;float pitch,fall;bool journal,lastGrip,lastTrigger,lastSecondary,turnLatch,lastLeftGrip,lastLeftTrigger,lastLeftSecondary;
        bool packing;float packingTime;
        bool bagPanel;
        bool phoneOpen;
        bool itemInfoOpen;
        PreparednessItem itemInfoTarget;
        public bool PhoneOpen=>phoneOpen;
        public void TogglePhone(){bool open=!phoneOpen;CloseDesktopPanels();phoneOpen=open;SetPanelCursor();}
        readonly bool[] guideGroups=new bool[5];
        bool controlsExpanded;
        public bool DesktopPanelOpen=>!XRActive&&(session.CoachingChoicePending||phoneOpen||bagPanel||departureReview||(session.chapter&&session.chapter.IsOpen));
        public bool ReadingPanel=>session.CoachingChoicePending||phoneOpen||journal||bagPanel||departureReview||(session.chapter&&session.chapter.IsOpen);
        public bool BagPanelOpen=>bagPanel;
        public bool GuideOpen=>journal;
        public bool ItemInfoOpen=>itemInfoOpen&&itemInfoTarget==focused&&FocusedItemCanShowInfo;
        public bool FocusedItemCanShowInfo=>focused&&!focused.IsZone;
        public bool Packing=>packing;
        Vector3 heldOffset;Quaternion heldRotation;
        Vector2 journalScroll;
        public bool XRActive=>XRSettings.isDeviceActive;
        SibucaoPreparationBridge preparationBridge;
        void Awake(){motor=GetComponent<CharacterController>();preparationBridge=FindFirstObjectByType<SibucaoPreparationBridge>();}
        void Start()
        {
            var systems=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(systems);
            foreach(var system in systems)system.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            if(!GetComponent<PreparationChecklistProp>())gameObject.AddComponent<PreparationChecklistProp>();
            if(!XRActive)SetPanelCursor();
        }
        public void Warp(Vector3 position,Quaternion rotation){motor.enabled=false;transform.SetPositionAndRotation(position,rotation);motor.enabled=true;fall=0;}
        public bool Grab(PreparednessItem item,bool restoring=false)
        {
            if(item&&item.kind==PrepKind.Bag)return GrabBag(item,restoring);
            if((!session.CanPrepare&&!restoring)||!item||!item.portable||held)return false;
            if(!restoring&&session.chapter&&!session.chapter.Owns(item)){session.chapter.TryOpen(item);return false;}
            if(item.storedIn=="installed"){session.Say("This material is secured in place.");return false;}
            if(!restoring&&item.storageDoor&&item.storageDoor.closed&&string.IsNullOrEmpty(item.storedIn)){session.Say("Open the storage door or lid first.");return false;}
            if(item.kind==PrepKind.Litter){session.Say("Use the litter tongs instead of picking it up by hand.");return false;}
            if(item.kind==PrepKind.Electronics&&item.plugged){session.Say("Use the device to disconnect it first.");return false;}
            if((item.kind==PrepKind.Litter||item.kind==PrepKind.Outdoor)&&!session.OutdoorSafe){session.Say("Outdoor preparation is closed after the evacuation advisory.");return false;}
            held=item;item.held=true;item.storedIn="";item.transform.SetParent(session.transform,true);item.SetPhysics(false);
            item.SetCarried(true);
            foreach(var collider in item.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(collider,motor,true);
            item.storageDoor=null;
            if(!restoring&&PreparednessSession.CanPackSupply(item))session.tutorialProgress|=2;
            if(!restoring&&item.kind==PrepKind.Bag&&session.phase==PrepPhase.MorningPacking)session.bagRetrieved=true;
            // Preserve authored object orientation; wrist follows tracked rotation while holding.
            var anchor=XRActive?(carriedBag?leftHand:rightHand):view.transform;
            heldRotation=Quaternion.Inverse(anchor.rotation)*item.transform.rotation;
            var b=item.WorldBounds();heldOffset=item.transform.position-b.center;
            if(!restoring)PreparationFeedback.Pickup();
            return true;
        }
        public bool GrabBag(PreparednessItem item,bool restoring=false)
        {
            if(!item||carriedBag||(!session.CanPrepare&&!restoring))return false;
            if(!restoring&&item.storageDoor&&item.storageDoor.closed){session.Say("Open the closet first.");return false;}
            carriedBag=item;item.held=true;item.storedIn="";item.storageDoor=null;item.SetPhysics(false);item.SetCarried(true);
            item.transform.SetParent(session.transform,true);foreach(var c in item.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(c,motor,true);
            session.bagRetrieved=true;if(!restoring){session.SpeakCue("bag.collected","Household guide",PreparationNarration.BagCollected);PreparationFeedback.Pickup(true);}return true;
        }
        public void ToggleBag(){if(session.CanPrepare&&carriedBag)session.Use(carriedBag);else if(session.CanPrepare)session.Say("Pick up the bag first, or look at it and press E.");}
        public void ReleaseBag()
        {
            if(!carriedBag)return;packing=false;var bag=carriedBag;carriedBag=null;bag.held=false;bag.SetCarried(false);bag.ResolveReleaseOverlap(transform);bag.SetPhysics(true);
            foreach(var collider in bag.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(collider,motor,false);
        }
        public void ReleaseAll(){Release();ReleaseBag();}
        public bool BeginPacking()
        {
            if(!held||!session.CanPrepare)return false;
            if(!carriedBag)return session.PackHeld();
            if(carriedBag.closed){session.Say("Open the bag first: I / Tab, then Open bag. In VR, use the right trigger.");return false;}
            if(!packing){packing=true;packingTime=0;}return true;
        }
        public bool TryInsertAtOpening()
        {
            if(!held||!carriedBag||carriedBag.closed)return false;
            var visual=carriedBag.GetComponent<PreparednessBagVisual>();if(!visual||!visual.Contains(held.WorldBounds().center))return false;
            return session.PackHeld();
        }
        public void Release()
        {
            packing=false;if(!held)return;var item=held;held=null;item.held=false;item.SetCarried(false);item.ResolveReleaseOverlap(transform);item.SetPhysics(true);
            foreach(var collider in item.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(collider,motor,false);
        }
        void OnDisable(){phoneOpen=bagPanel=journal=itemInfoOpen=false;itemInfoTarget=null;ReleaseAll();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void Update()
        {
            if(PreparationIntro.Blocking)
            {
                Running=false;
                // Desktop playtests must never feel frozen behind the title card.
                // 2 deliberately selects Explore; any normal start/movement input
                // skips the title and begins with Guided coaching.
                if(!XRActive)TryStartDesktopPreparation(true);
                return;
            }
            if(SibucaoSession.Instance&&(SibucaoSession.Instance.Mode==SibucaoMode.Settings||SibucaoSession.Instance.SettingsTransitionThisFrame)){Running=false;return;}
            if(XRActive)UpdateXR();else UpdateDesktop();
            RefreshFocus();
            if(!FocusedItemCanShowInfo||itemInfoTarget!=focused){itemInfoOpen=false;itemInfoTarget=null;}
            if(worldPrompt){worldPrompt.gameObject.SetActive(XRActive&&!journal&&!StormSim.UI.StormglassUI.InUse);worldPrompt.text=session.chapter&&session.chapter.IsOpen?session.chapter.XRText:session.CoachingChoicePending
                ? "CHOOSE YOUR PREPARATION\nLeft primary: Guided preparation — learn step by step.\nRight primary: Explore on your own — small interaction hints.\nLater: hold left primary for your guide; press right secondary to switch coaching."
                : journal?(session.Guided?"COACHING ON":"COACHING OFF")+" — Right secondary switches coaching\n"+(session.Guided?session.TutorialSummary+"\n":"")+session.Journal():Prompt();}
            foreach(var item in session.items)if(session.NeedsDroppedItemRecovery(item))
            {session.RecoverDroppedItem(item);}
        }
        void UpdateDesktop()
        {
            if(Keyboard.current==null||Mouse.current==null)return;
            var k=Keyboard.current;var m=Mouse.current;
            if(k.escapeKey.wasPressedThisFrame)
            {
                if(DesktopPanelOpen&&!session.CoachingChoicePending){CloseDesktopPanels();return;}
                if(SibucaoSession.Instance)SibucaoSession.Instance.OpenSettings();
                else {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}return;
            }
            if(session.CoachingChoicePending)
            {
                Running=false;SetPanelCursor();
                if(k.digit2Key.wasPressedThisFrame)session.SetCoaching(false);
                else if(k.digit1Key.wasPressedThisFrame||k.enterKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame||
                    k.wKey.wasPressedThisFrame||k.aKey.wasPressedThisFrame||k.sKey.wasPressedThisFrame||k.dKey.wasPressedThisFrame||
                    StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame)session.SetCoaching(true);
                return;
            }
            if(session.chapter&&session.chapter.IsOpen){Running=false;return;}
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.I).wasPressedThisFrame||k.tabKey.wasPressedThisFrame){ToggleBagPanel();return;}
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.J).wasPressedThisFrame){ToggleGuide();return;}
            if(k.nKey.wasPressedThisFrame){TogglePhone();return;}
            if(DesktopPanelOpen){Running=false;return;}
            if(journal){Running=false;return;}
            if(Cursor.lockState!=CursorLockMode.Locked&&preparationBridge&&preparationBridge.PointerOverSkip(m.position.ReadValue()))return;
            if(m.leftButton.wasPressedThisFrame){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(k.f5Key.wasPressedThisFrame)session.Save();if(k.f9Key.wasPressedThisFrame)session.Load();
            bool rPressed=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.R).wasPressedThisFrame;
            if(rPressed&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.LeftCtrl).isPressed)session.Restart();
            else if(rPressed)ToggleItemInfo();
            if(Cursor.lockState!=CursorLockMode.Locked&&DesktopGameplayKeyPressed(k))ClaimDesktopControl();
            if(Cursor.lockState!=CursorLockMode.Locked)return;
            if(session.phase==PrepPhase.Fade||session.phase==PrepPhase.ReadyForStorm)return;
            var game=SibucaoSession.Instance;if(game)view.fieldOfView=game.settings.fieldOfView;
            var delta=m.delta.ReadValue()*(game?game.settings.sensitivity:.085f);if(game&&game.settings.invertLook)delta.y=-delta.y;transform.Rotate(0,delta.x,0);pitch=Mathf.Clamp(pitch-delta.y,-75,75);view.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Running=StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.LeftShift).isPressed||k.rightShiftKey.isPressed;
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Space).wasPressedThisFrame)jumpRequested=true;
            Move(new Vector2((StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.D).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.A).isPressed?1:0),(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.W).isPressed?1:0)-(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.S).isPressed?1:0)));
            RefreshFocus();
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.F).wasPressedThisFrame)CarryOrRelease();
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Q).wasPressedThisFrame)PutDown();
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame)UseFocused();
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.P).wasPressedThisFrame)BeginPacking();
            // Bag opening/retrieval lives in the shared I / Tab panel. F and P remain
            // the same optional carry/pack shortcuts used during evacuation.
        }
        bool DesktopGameplayKeyPressed(Keyboard k)=>k.wKey.wasPressedThisFrame||k.aKey.wasPressedThisFrame||k.sKey.wasPressedThisFrame||k.dKey.wasPressedThisFrame||
            k.spaceKey.wasPressedThisFrame||k.leftShiftKey.wasPressedThisFrame||k.rightShiftKey.wasPressedThisFrame||
            StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame||
            StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Q).wasPressedThisFrame||
            StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.F).wasPressedThisFrame||
            StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.P).wasPressedThisFrame||
            StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.R).wasPressedThisFrame;
        public void ClaimDesktopControl(){if(XRActive||DesktopPanelOpen)return;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public bool ToggleItemInfo()
        {
            if(!FocusedItemCanShowInfo)return false;
            if(ItemInfoOpen){itemInfoOpen=false;itemInfoTarget=null;}
            else{itemInfoOpen=true;itemInfoTarget=focused;}
            return true;
        }
        bool TryStartDesktopPreparation(bool allowMouse)
        {
            var k=Keyboard.current;if(k==null)return false;
            if(k.digit2Key.wasPressedThisFrame){session.SetCoaching(false);return true;}
            bool start=k.digit1Key.wasPressedThisFrame||k.enterKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame||
                k.wKey.wasPressedThisFrame||k.aKey.wasPressedThisFrame||k.sKey.wasPressedThisFrame||k.dKey.wasPressedThisFrame||
                StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame||
                allowMouse&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame;
            if(start){session.SetCoaching(true);return true;}return false;
        }
        public void CarryOrRelease()
        {
            if(held){if(!TryInsertAtOpening())Release();}
            else if(focused&&focused.portable)Grab(focused);
            else if(carriedBag)ReleaseBag();
        }
        public void PutDown(){session.CancelInteraction();if(held)Release();else ReleaseBag();}
        public void ToggleBagPanel()
        {
            if(bagPanel){CloseDesktopPanels();return;}
            if(!session.bagRetrieved){session.Say("Collect your go-bag first. E opens storage and picks up the bag.");return;}
            phoneOpen=false;bagPanel=true;journal=false;SetPanelCursor();
        }
        public void CloseDesktopPanels(){phoneOpen=bagPanel=journal=departureReview=false;if(session.chapter)session.chapter.Close();SetPanelCursor();}
        public void ToggleGuide(){phoneOpen=false;journal=!journal;bagPanel=false;if(journal&&session.Guided)session.guidanceUsed=true;SetPanelCursor();}
        public void RefreshPanelCursor(){if(!XRActive)SetPanelCursor();}
        void SetPanelCursor(){Cursor.lockState=DesktopPanelOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=DesktopPanelOpen;}

        void UpdateXR()
        {
            if(session.chapter&&session.chapter.IsOpen&&!StormSim.UI.StormglassUI.InUse){Running=false;session.chapter.UpdateXR();return;}
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            Track(head,view.transform);Track(right,rightHand);Track(left,leftHand);
            if (StormSim.UI.StormglassUI.BlocksWorldInput || ReadingPanel && StormSim.UI.StormglassUI.InUse) {
                Running=false;
                right.TryGetFeatureValue(CommonUsages.gripButton,out lastGrip);right.TryGetFeatureValue(CommonUsages.triggerButton,out lastTrigger);
                left.TryGetFeatureValue(CommonUsages.gripButton,out lastLeftGrip);left.TryGetFeatureValue(CommonUsages.triggerButton,out lastLeftTrigger);
                right.TryGetFeatureValue(CommonUsages.secondaryButton,out lastSecondary);right.TryGetFeatureValue(CommonUsages.primaryButton,out lastJump);
                return;
            }
            motor.height=Mathf.Clamp(view.transform.localPosition.y,1.05f,2.15f);
            motor.center=new Vector3(view.transform.localPosition.x,motor.height*.5f,view.transform.localPosition.z);
            bool grip=false,trigger=false,secondary=false;right.TryGetFeatureValue(CommonUsages.gripButton,out grip);right.TryGetFeatureValue(CommonUsages.triggerButton,out trigger);right.TryGetFeatureValue(CommonUsages.secondaryButton,out secondary);
            left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool infoButton);
            left.TryGetFeatureValue(CommonUsages.primaryButton,out bool guideButton);
            right.TryGetFeatureValue(CommonUsages.primaryButton,out bool chooseExplore);
            if(session.CoachingChoicePending)
            {
                Running=false;
                if(guideButton)session.SetCoaching(true);
                else if(chooseExplore)session.SetCoaching(false);
                lastJump=chooseExplore;lastSecondary=secondary;lastGrip=grip;lastTrigger=trigger;
                return;
            }
            if(!StormSim.UI.StormglassUI.InUse)journal=guideButton;
            if(infoButton&&!lastLeftSecondary)ToggleItemInfo();
            lastLeftSecondary=infoButton;
            if(journal)
            {
                Running=false;
                if(secondary&&!lastSecondary)session.SetCoaching(!session.Guided);
                lastSecondary=secondary;lastGrip=grip;lastTrigger=trigger;
                if(session.Guided)session.guidanceUsed=true;
                return;
            }
            if(session.phase!=PrepPhase.Fade&&session.phase!=PrepPhase.ReadyForStorm)
            {
                left.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool running);Running=running;
                right.TryGetFeatureValue(CommonUsages.primaryButton,out bool jump);if(jump&&!lastJump)jumpRequested=true;lastJump=jump;
                Vector2 axis;left.TryGetFeatureValue(CommonUsages.primary2DAxis,out axis);Move(axis);
                right.TryGetFeatureValue(CommonUsages.primary2DAxis,out axis);
                if(Mathf.Abs(axis.x)>.75f&&!turnLatch){transform.RotateAround(view.transform.position,Vector3.up,Mathf.Sign(axis.x)*30);turnLatch=true;}if(Mathf.Abs(axis.x)<.3f)turnLatch=false;
                bool lg=false,lt=false;left.TryGetFeatureValue(CommonUsages.gripButton,out lg);left.TryGetFeatureValue(CommonUsages.triggerButton,out lt);
                if(carriedBag)
                {
                    RefreshFocus();if(lg&&!lastLeftGrip&&!held)Grab(focused);
                    if(!lg&&lastLeftGrip&&held){if(!TryInsertAtOpening())Release();}
                    if(trigger&&!lastTrigger)ToggleBag();if(lt&&!lastLeftTrigger)UseFocused();
                    if(!grip&&lastGrip)ReleaseBag();
                }
                else{RefreshFocus();if(grip&&!lastGrip&&!held)Grab(focused);if(!grip&&lastGrip)Release();if(trigger&&!lastTrigger)UseFocused();}
                lastLeftGrip=lg;lastLeftTrigger=lt;if(secondary&&!lastSecondary&&!StormSim.UI.StormglassUI.InUse)session.Unpack();
            }
            lastGrip=grip;lastTrigger=trigger;lastSecondary=secondary;
            bool primary;left.TryGetFeatureValue(CommonUsages.primaryButton,out primary);if(!StormSim.UI.StormglassUI.InUse)journal=primary;
            if(vrFade){vrFade.enabled=session.phase==PrepPhase.Fade;var color=Color.black;color.a=Mathf.Clamp01(session.phaseTime);vrFade.material.color=color;}
        }
        void Track(UnityEngine.XR.InputDevice device,Transform target)
        {
            if(!target)return;Vector3 p;Quaternion q;
            if(device.TryGetFeatureValue(CommonUsages.devicePosition,out p))target.localPosition=p;
            if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out q))target.localRotation=q;
        }
        void Move(Vector2 input)
        {
            if(LadderClimbing)return;
            var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            float load=(held?held.mass:0)+(carriedBag?carriedBag.mass:0);float factor=Mathf.Clamp(1-load*.04f,.5f,1);
            if(motor.isGrounded&&fall<=0)fall=-2;
            if(jumpRequested&&motor.isGrounded)fall=Mathf.Sqrt(2*Mathf.Abs(Physics.gravity.y)*jumpHeight);
            jumpRequested=false;fall+=Physics.gravity.y*Time.deltaTime;
            var direction=Vector3.ClampMagnitude(forward*input.y+right*input.x,1);
            var game=StormSim.Sibucao.SibucaoSession.Instance;
            if(game){if(game.state.physical.stamina<=.01f)exhausted=true;if(game.state.physical.stamina>=15)exhausted=false;if(exhausted)Running=false;}
            if(game&&Running)factor*=Mathf.Lerp(.85f,1,Mathf.Clamp01(game.state.physical.stamina/30));
            motor.Move((direction*(Running?runSpeed:speed)*factor+Vector3.up*fall)*Time.deltaTime);
        }
        public void RefreshFocus()
        {
            focused=null;var source=XRActive?(carriedBag?leftHand:rightHand):view.transform;if(!source)return;
            var hits=Physics.RaycastAll(source.position,source.forward,reach,~0,QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            PreparednessItem zoneCandidate=null;
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(transform))continue;
                var item=hit.collider.GetComponentInParent<PreparednessItem>();if(item&&(item==held||item==carriedBag))continue;
                if(item&&item.kind==PrepKind.BagZone&&carriedBag)continue;
                if(item&&item.kind==PrepKind.BagZone){zoneCandidate=session.Bag;continue;}
                if(item&&item.kind==PrepKind.TableZone&&held)
                {
                    var bagHit=System.Array.Find(hits,h=>h.collider.GetComponentInParent<PreparednessItem>()==session.Bag);
                    if(bagHit.collider&&bagHit.distance-hit.distance<.8f)continue;
                }
                if(item&&item.IsZone&&!held&&item.kind!=PrepKind.SupplyZone&&!(carriedBag&&item.kind==PrepKind.TableZone))continue;
                // Broad storage/placement zones may overlap the small prop that the
                // reticle actually touches. Remember the zone, but always prefer a
                // concrete preparation item hit by the same aim ray.
                if(item&&item.IsZone){if(IsInteractable(item)&&!zoneCandidate)zoneCandidate=item;continue;}
                if(item&&IsInteractable(item)){focused=item;return;}
                if(!hit.collider.isTrigger){ForgivingFocus(source,hit.distance);if(!focused)focused=zoneCandidate;return;}
            }
            ForgivingFocus(source,reach);
            if(!focused)focused=zoneCandidate;
        }
        public void UseFocused()
        {
            if(!session.CanPrepare){if(focused)session.Use(focused);return;}
            // Keep tracked-hand trigger/grip behavior intact. Desktop uses E for
            // pickup -> check -> pack; world fixtures take priority over packing.
            if(!XRActive)
            {
                if(held)
                {
                    if(focused&&focused!=held&&focused!=carriedBag&&(!focused.portable||held.kind==PrepKind.Batteries&&(focused.kind==PrepKind.Flashlight||focused.kind==PrepKind.Radio)))
                    {session.Use(focused,held);return;}
                    if(PreparednessSession.NeedsCheck(held)){session.Use(held);return;}
                    if(PreparednessSession.CanPackSupply(held))
                    {
                        if(session.Bag.closed&&(carriedBag||focused==session.Bag)){session.Use(session.Bag);return;}
                        BeginPacking();return;
                    }
                    session.Say("Look at a suitable storage area and press E to place this item. Q puts it down.");return;
                }
                if(focused&&focused.portable&&focused.kind!=PrepKind.Bag)
                {
                    if(focused.kind==PrepKind.Electronics&&focused.plugged)session.Use(focused);
                    else Grab(focused);
                    return;
                }
            }
            if(focused==session.Bag&&!held&&!carriedBag){GrabBag(focused);return;}
            if(held&&focused==session.Bag&&carriedBag){BeginPacking();return;}
            if(focused&&focused.IsZone&&!held&&carriedBag&&focused.kind!=PrepKind.SupplyZone){session.Use(focused,carriedBag);return;}
            if(focused)session.Use(focused,held);else if(held)session.Use(held);
        }
        void FixedUpdate()
        {
            if(ReadingPanel)return;
            if(carriedBag)
            {
                var bagAnchor=XRActive?rightHand:view.transform;
                var rotation=XRActive?bagAnchor.rotation:Quaternion.LookRotation(Vector3.ProjectOnPlane(view.transform.forward,Vector3.up));
                var grip=XRActive?bagAnchor.position:bagAnchor.TransformPoint(new Vector3(.4f,-.14f,.78f));
                var root=grip-rotation*new Vector3(0,.29f,0);
                carriedBag.Body.MovePosition(root);carriedBag.Body.MoveRotation(rotation);
            }
            if(!held)return;
            if(packing&&carriedBag)
            {
                packingTime+=Time.fixedDeltaTime;var visual=carriedBag.GetComponent<PreparednessBagVisual>();
                var mouth=visual?visual.Opening:carriedBag.transform.TransformPoint(new Vector3(0,.48f,0));
                held.Body.MovePosition(Vector3.Lerp(held.Body.position,mouth+heldOffset,Mathf.Clamp01(packingTime/.28f)));
                if(packingTime>=.28f){packing=false;session.PackHeld();}return;
            }
            var anchor=XRActive?(carriedBag?leftHand:rightHand):view.transform;
            var target=XRActive?anchor.position+anchor.forward*.12f:anchor.TransformPoint(new Vector3(carriedBag?-.28f:.22f,-.22f,.95f));
            target+=heldOffset;
            var delta=target-held.transform.position;
            // Stop held objects at obstructions instead of teleporting through walls.
            if(delta.sqrMagnitude>.0001f&&held.Body)
            {
                var hits=held.Body.SweepTestAll(delta.normalized,delta.magnitude,QueryTriggerInteraction.Ignore);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits)if(!hit.transform.IsChildOf(transform)&&!hit.transform.IsChildOf(held.transform)&&(!carriedBag||!hit.transform.IsChildOf(carriedBag.transform))){target=held.transform.position+delta.normalized*Mathf.Max(0,hit.distance-.025f);break;}
            }
            held.Body.MovePosition(Vector3.Lerp(held.transform.position,target,.4f));held.Body.MoveRotation(Quaternion.Slerp(held.transform.rotation,anchor.rotation*heldRotation,.35f));
        }
        string Prompt()
        {
            string action=focused?(ItemInfoOpen?FocusedItemInformation+"\n\n":"")+DetailedContextAction():held?session.Describe(held):"";
            if(session.phase==PrepPhase.ReadyForStorm)return "PREPARATION SAVED\nThe typhoon is arriving…\n"+session.WarningProgress;
            return action+"\n"+session.message+"\n"+session.WarningProgress+(carriedBag?" | Right-hand bag: "+(carriedBag.closed?"closed":"open"):"");
        }
        public string CurrentObjective=>session.NextPreparationStep;
        public string ItemInformation(PreparednessItem item)=>item?"NAME\n"+item.displayName+"\n\nPURPOSE\n"+session.ItemPurpose(item):null;
        public string FocusedItemInformation=>FocusedItemCanShowInfo?ItemInformation(focused):null;
        public string ContextAction()
        {
            if(!focused)return null;
            var action=DetailedContextAction();
            if(session.Guided||string.IsNullOrEmpty(action))return action;
            if(session.CanPrepare&&!focused&&!held&&!packing)return null;
            return action.Split('\n')[0];
        }
        string DetailedContextAction()
        {
            if(session.phase==PrepPhase.Fade||session.phase==PrepPhase.ReadyForStorm)return null;
            if(!session.CanPrepare)return focused&&focused.kind==PrepKind.Advisory?"E  Listen to the television":"WASD move · Mouse look\nYou can explore while listening to the TV.";
            if(packing)return "Placing the supply in your bag…";
            if(held)
            {
                if(focused&&focused.IsZone)return session.PlacementHint(held,focused)+"\nQ  Put down";
                if(focused&&focused!=held&&focused!=carriedBag&&!focused.portable)return "E  Use "+focused.displayName+"\nP pack supply · F / Q put down";
                if(held.kind==PrepKind.Batteries&&focused&&(focused.kind==PrepKind.Flashlight||focused.kind==PrepKind.Radio))return "E  Fit batteries into "+focused.displayName+"\nQ  Put down";
                if(PreparednessSession.NeedsCheck(held))return PreparednessSession.CheckInstruction(held)+"\nQ  Put down";
                if(PreparednessSession.CanPackSupply(held))return session.Bag.closed
                    ? carriedBag||focused==session.Bag?"E  Open your bag\nQ  Put down supply":"Look at your bag; E opens it\nQ  Put down supply"
                    : session.PlacementHint(held,session.Bag)+"\n"+session.CapacityText;
                return held.displayName+"\nE at storage to place · Q put down";
            }
            if(focused)
            {
                if(focused.kind==PrepKind.SupplyZone)return session.PlacementHint(null,focused);
                var work=focused.GetComponent<PreparednessHouseTask>();if(work){if(focused.prepared)return "Completed — "+work.result;if(work.exterior&&!session.OutdoorSafe||work.requiresLadderHeight&&!session.ElevatedWorkSafe)return "Work closed — "+session.WeatherText;return "E  "+(work.Working&&session.toggleWork?"Pause ":"Work on ")+focused.displayName+" · "+work.ProgressPercent+"%\n"+(session.toggleWork?"Press once to work; Q cancels":"Hold to work; J offers press-once controls");}
                if(focused.kind==PrepKind.Bag)return carriedBag?"E  "+(carriedBag.closed?"Open":"Close")+" bag · I / Tab inspect":"E  Collect your go-bag";
                if(focused.kind==PrepKind.StorageDoor||focused.kind==PrepKind.Door||focused.kind==PrepKind.Window)return "E  "+(focused.closed?"Open ":"Close ")+focused.displayName;
                if(focused.kind==PrepKind.Neighbor)return "E  Speak to "+focused.displayName+"\nOptional household warning";
                if(focused.kind==PrepKind.Exit)return session.evacuationOrdered?"E  Leave now with your preparations":session.advisoryRead?"E  Leave early with your preparations":"Recheck the TV before leaving early";
                if(focused.kind==PrepKind.Bed)return session.evacuationOrdered?"Rest unavailable — evacuate now":"E  Review rest until tomorrow\nThe exit lets you leave early instead";
                if(focused.portable)return focused.kind==PrepKind.Electronics&&focused.plugged?"E  Disconnect "+focused.displayName:"E  Pick up "+focused.displayName+"\nQ  Put down after pickup";
                return "E  Use "+focused.displayName;
            }
            if(carriedBag)return "E picks up, checks and packs supplies\nI / Tab bag · Q put down bag";
            return "E opens storage and picks up supplies\nJ opens your preparation plan";
        }
        void OnGUI()
        {
            if(PreparationIntro.Blocking)return;
            if (StormSim.UI.StormglassUI.InUse) return;
            if(XRActive||(SibucaoSession.Instance&&SibucaoSession.Instance.Mode==SibucaoMode.Settings))return;
            var layout=SibucaoHudLayout.Current;var previous=SibucaoDesktopHud.Begin(layout);
            if(session.CoachingChoicePending)DrawCoachingChoice(layout);
            else if(session.chapter&&session.chapter.IsOpen)session.chapter.Draw(layout);
            else if(departureReview)DrawDepartureReview(layout);
            else if(DesktopPanelOpen)DrawDesktopPanel(layout);
            else
            {
                SibucaoDesktopHud.Objective(layout,session.Guided?(session.TutorialComplete?"GUIDED PREPARATION":"GUIDED PREPARATION · TUTORIAL"):"EXPLORE ON YOUR OWN",CurrentObjective);
                if(session.Guided)session.guidanceUsed=true;
                SibucaoDesktopHud.Instruction(layout,session.CurrentInstructionText);
                if(!journal&&(!SibucaoSession.Instance||SibucaoSession.Instance.settings.interactPrompts))SibucaoDesktopHud.Action(layout,ContextAction());
                if(!journal&&FocusedItemCanShowInfo)DrawItemInfoControl(layout);
                if(!journal&&ItemInfoOpen)DrawItemInformation(layout,focused);
                if(session.voiceDirector&&session.voiceDirector.SubtitleVisible)SibucaoDesktopHud.NarrationSubtitle(layout,session.voiceDirector.SubtitleText);
                if(session.RecentMessage)SibucaoDesktopHud.Subtitle(layout,session.message);
                SibucaoDesktopHud.Label(layout.Help,journal?"J closes field guide":"I/Tab Bag\nJ Field guide · Esc",SibucaoDesktopHud.Small,SibucaoDesktopHud.Muted);
                if(!journal)SignalStationUI.Draw(new Rect(layout.Width/2-10,layout.Height/2-10,20,20),"crosshair");
            }
            if(session.phase==PrepPhase.Fade){var old=GUI.color;GUI.color=new Color(0,0,0,Mathf.Clamp01(session.phaseTime));GUI.DrawTexture(new Rect(0,0,layout.Width,layout.Height),Texture2D.whiteTexture);GUI.color=old;}
            GUI.matrix=previous;
        }
        void DrawItemInformation(SibucaoHudLayout layout,PreparednessItem item)
        {
            float width=322,contentWidth=width-28;
            var titleStyle=SibucaoDesktopHud.Text;
            float titleHeight=Mathf.Clamp(titleStyle.CalcHeight(new GUIContent(item.displayName),contentWidth),22,44);
            var purpose=session.ItemPurpose(item);float purposeHeight=Mathf.Clamp(SibucaoDesktopHud.Small.CalcHeight(new GUIContent(purpose),contentWidth),34,60);
            float height=titleHeight+purposeHeight+58;
            var r=new Rect(layout.Width-338,174,width,height);SibucaoDesktopHud.Panel(r);
            float y=r.y+10;
            SibucaoDesktopHud.Label(new Rect(r.x+14,y,contentWidth,18),"NAME",SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);y+=18;
            SibucaoDesktopHud.Label(new Rect(r.x+14,y,contentWidth,titleHeight),item.displayName,titleStyle);y+=titleHeight+5;
            SibucaoDesktopHud.Label(new Rect(r.x+14,y,contentWidth,18),"PURPOSE",SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);y+=18;
            SibucaoDesktopHud.Label(new Rect(r.x+14,y,contentWidth,purposeHeight),purpose,SibucaoDesktopHud.Small);
        }
        void DrawItemInfoControl(SibucaoHudLayout layout)
        {
            var r=new Rect(layout.Width-210,132,194,34);SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+10,r.y+7,r.width-20,22),ItemInfoOpen?"R  CLOSE INFO":"R  ITEM INFO",SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);
        }
        Vector2 coachingScroll;
        void DrawCoachingChoice(SibucaoHudLayout layout)
        {
            var tint=GUI.color;GUI.color=new Color(.02f,.04f,.05f,.96f);
            GUI.DrawTexture(new Rect(0,0,layout.Width,layout.Height),Texture2D.whiteTexture);GUI.color=tint;
            var r=layout.Modal;SibucaoDesktopHud.Panel(r);
            GUILayout.BeginArea(new Rect(r.x+22,r.y+18,r.width-44,r.height-36));
            coachingScroll=GUILayout.BeginScrollView(coachingScroll);
            GUILayout.Label("How would you like to prepare?",SibucaoDesktopHud.Heading);
            GUILayout.Space(14);
            GUILayout.Label("Guided preparation",SibucaoDesktopHud.Text);
            GUILayout.Label("Learn by doing: open storage, pick up, check, and pack supplies. Follow one suggested step at a time.",SibucaoDesktopHud.Small);
            if(GUILayout.Button("1 — Start guided preparation",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(48)))session.SetCoaching(true);
            GUILayout.Space(18);
            GUILayout.Label("Explore on your own",SibucaoDesktopHud.Text);
            GUILayout.Label("Choose your own actions with a brief objective and small interaction hints. Your guide is always available.",SibucaoDesktopHud.Small);
            if(GUILayout.Button("2 — Start exploring",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(48)))session.SetCoaching(false);
            if(session.HasPreparationSave&&GUILayout.Button("Resume preparation checkpoint",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(42)))session.Load();
            GUILayout.Space(18);
            GUILayout.Label(SibucaoControls.Prompt("Switch coaching anytime in J — Guide, or Esc — Settings. Both choices use the same tasks and consequences. Evacuation advisories remain visible.\nPreparation time is paused while you choose."),SibucaoDesktopHud.Small);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void DrawDesktopPanel(SibucaoHudLayout layout)
        {
            var tint=GUI.color;GUI.color=new Color(.02f,.04f,.05f,.92f);GUI.DrawTexture(new Rect(0,0,layout.Width,layout.Height),Texture2D.whiteTexture);GUI.color=tint;
            var r=layout.Modal;SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+18,r.width-44,40),bagPanel?"Go-bag":"Preparation guide",SibucaoDesktopHud.Heading);
            if(bagPanel)
            {
                DrawSelectedBag(new Rect(r.x+22,r.y+62,r.width-44,r.height-140));
            }
            else
            {
                DrawGuide(new Rect(r.x+22,r.y+62,r.width-44,r.height-140));
            }
            if(GUI.Button(new Rect(r.xMax-212,r.yMax-60,190,38),"Close / Esc",SibucaoDesktopHud.ButtonStyle))CloseDesktopPanels();
        }

        void DrawGuide(Rect viewport)
        {
            GUILayout.BeginArea(viewport);
            journalScroll=GUILayout.BeginScrollView(journalScroll);
            if(GUILayout.Button(session.Guided?"Coaching: ON — switch to Explore":"Coaching: OFF — switch to Guided",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(44)))session.SetCoaching(!session.Guided);
            GUILayout.Label("Switch anytime. Your supplies and tutorial progress stay with you.",SibucaoDesktopHud.Small);
            GUILayout.Space(10);
            GUILayout.Label("CURRENT OBJECTIVE",SibucaoDesktopHud.Small);
            GUILayout.Label(SibucaoControls.Prompt(CurrentObjective),SibucaoDesktopHud.Text);
            GUILayout.Space(10);
            GUILayout.Label("WARNING LEVEL",SibucaoDesktopHud.Small);
            GUILayout.Label(session.WarningStageText,SibucaoDesktopHud.Text);
            GUILayout.Space(10);
            GUILayout.Label("BAG STATUS",SibucaoDesktopHud.Small);
            GUILayout.Label(session.CapacityText+" · "+session.items.Count(i=>i.IsPacked)+" items packed",SibucaoDesktopHud.Text);
            GUILayout.Label("Look at an item and press R (left secondary in VR) for its purpose. Optional work never blocks departure.",SibucaoDesktopHud.Small);
            DrawPreparationHelp();
            if(GUILayout.Button((controlsExpanded?"− ":"+ ")+"Controls and help",SibucaoDesktopHud.ButtonStyle,GUILayout.MinHeight(38)))controlsExpanded=!controlsExpanded;
            if(controlsExpanded)GUILayout.Label(SibucaoControls.Prompt("E  Interact, pick up, check or pack\nQ  Put down / cancel\nI / Tab  Bag    J  Plan    Esc  Settings\nF carry and P pack are optional shortcuts in both phases.\nIn preparation, check a held supply before packing it. In evacuation, E collects ready supplies into your bag.\nSwitch coaching above or in Settings. Interaction prompts have their own setting; essential warnings stay visible."),SibucaoDesktopHud.Text);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }

        
    }
}
