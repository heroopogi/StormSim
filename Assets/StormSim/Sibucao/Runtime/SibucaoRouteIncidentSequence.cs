using System;
using System.Collections.Generic;
using StormSim.Preparedness;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    /// <summary>The remaining continuous evacuation chapters. Choices are made through movement and world interactions.</summary>
    [DefaultExecutionOrder(-84)]
    public sealed class SibucaoRouteIncidentSequence : MonoBehaviour
    {
        const float Reach=4.8f;
        enum TargetKind { DrainVolunteer, DrainDebris, VaultGrip, Casualty, Treatment, Signal }
        sealed class Target { public TargetKind kind; public int index; public Transform visual; }

        readonly Dictionary<Collider,Target> targets=new Dictionary<Collider,Target>();
        readonly Transform[] debris=new Transform[3];
        Transform runtimeRoot,drainRoot,rescueRoot,volunteer,casualty,helper,sack,trunk,treatmentKit,signalPost,safeLanding;
        Animator volunteerAnimator,casualtyAnimator,helperAnimator;
        Target working;
        float drainDistance,rescueDistance,workProgress,escortProgress,helperProgress;
        bool ready;

        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S&&S.state!=null?S.state.physical:null;
        public bool DrainDiscovered=>Has("route_drain_discovered");
        public bool DrainComplete=>Has("route_drain_complete");
        public bool RescueDiscovered=>Has("route_rescue_discovered");
        public bool RescueComplete=>Has("route_rescue_complete");
        public bool DrainActive=>DrainDiscovered&&!DrainComplete;
        public bool RescueActive=>RescueDiscovered&&!RescueComplete;
        public int ActiveChapter=>RescueActive?3:DrainActive?2:0;
        public bool ProgressVisible=>ActiveChapter>0;
        public bool IsWorking=>working!=null;
        public bool WantsLocalGuidance=>DrainActive&&Has("route_drain_branch_clear")||RescueActive&&Has("route_rescue_branch_treat");
        public string MissionTitle=>ActiveChapter==2?"Open a passage past storm debris":"Respond to an injured evacuee";
        public string Objective=>ActiveChapter==2?"Reach school reception. Assistance here is optional.":"Reach school reception. Request trained help if possible.";
        public string EquipmentNote
        {
            get
            {
                if(ActiveChapter==2)return HasPrepared("gloves")||HasPrepared("tongs")?"Protective handling gear packed — debris work is safer":"No handling gear — report the blockage and continue";
                return HasPrepared("first-aid")?"Prepared kit available — offer supplies, not a diagnosis":HasPrepared("radio")||HasPrepared("whistle")?"Signal equipment packed — help arrives faster":"Your voice can request a nearby responder";
            }
        }
        public int TaskCount=>3;
        public string TaskLabel(int index)
        {
            if(ActiveChapter==2)
            {
                if(index==0)return Has("route_drain_spoken")?"Volunteer briefing complete":"Speak with the barangay volunteer";
                if(index==1)return DrainChoiceMade?DrainChoiceLabel+" response selected":"Choose how to pass the blockage";
                return Has("route_drain_branch_clear")?"Remove drain debris  "+DrainDebrisCount+" / 3":"Report the blockage or inspect the open side";
            }
            if(index==0)return Has("route_rescue_assessed")?"Condition assessed":"Ask what help is needed";
            if(index==1)return RescueChoiceMade?RescueChoiceLabel+" response selected":"Offer supplies or request trained help";
            return Has("route_rescue_branch_treat")?(Has("route_rescue_stabilized")?Has("route_rescue_walk_consent")?"Stay close on clear, dry access":"Ask permission before accompanying":"Offer the prepared first-aid kit"):(Has("route_rescue_signaled")?"Wait for the responder":"Signal a nearby responder");
        }
        public float TaskProgress(int index)
        {
            if(ActiveChapter==2)
            {
                if(index==0)return Has("route_drain_spoken")?1:0;
                if(index==1)return DrainChoiceMade?1:0;
                if(DrainComplete)return 1;
                if(Has("route_drain_branch_clear"))return Mathf.Clamp01((DrainDebrisCount+(working!=null&&working.kind==TargetKind.DrainDebris?workProgress:0))/3f);
                return Has("route_drain_vault_ready")?Mathf.Clamp01((S.route.Progress(S.player.transform.position)-(drainDistance+8))/14f):working!=null&&working.kind==TargetKind.VaultGrip?workProgress:0;
            }
            if(index==0)return Has("route_rescue_assessed")?1:0;
            if(index==1)return RescueChoiceMade?1:0;
            if(RescueComplete)return 1;
            if(Has("route_rescue_branch_treat"))return Has("route_rescue_stabilized")?Mathf.Clamp01(escortProgress/Mathf.Max(1,S.route.length-rescueDistance)):working!=null&&working.kind==TargetKind.Treatment?workProgress:0;
            return Has("route_rescue_signaled")?Mathf.Clamp01(helperProgress/9f):working!=null&&working.kind==TargetKind.Signal?workProgress:0;
        }
        public string TaskValue(int index,float progress)
        {
            if(progress>=.999f)return "DONE";
            if(index==2&&ActiveChapter==2&&Has("route_drain_branch_clear"))return DrainDebrisCount+" / 3";
            if(index==2&&ActiveChapter==3&&Has("route_rescue_branch_treat")&&Has("route_rescue_stabilized"))return Mathf.RoundToInt(Mathf.Clamp01(escortProgress/Mathf.Max(1,S.route.length-rescueDistance))*100)+"%";
            return index==2&&working!=null?Mathf.RoundToInt(workProgress*100)+"%":"NOW";
        }
        bool DrainChoiceMade=>Has("route_drain_branch_clear")||Has("route_drain_branch_vault");
        bool RescueChoiceMade=>Has("route_rescue_branch_treat")||Has("route_rescue_branch_signal");
        string DrainChoiceLabel=>Has("route_drain_branch_clear")?"Clear-drain":"Step-over";
        string RescueChoiceLabel=>Has("route_rescue_branch_treat")?"Supplies-and-company":"Signal-help";
        int DrainDebrisCount=>(Has("route_drain_piece_0")?1:0)+(Has("route_drain_piece_1")?1:0)+(Has("route_drain_piece_2")?1:0);

        public Vector3 NavigationTarget
        {
            get
            {
                if(ActiveChapter==2)
                {
                    if(!Has("route_drain_spoken"))return volunteer?volunteer.position:RoutePoint(drainDistance,2);
                    if(!DrainChoiceMade)return RoutePoint(drainDistance+7,-1.3f);
                    if(Has("route_drain_branch_clear")&&DrainDebrisCount<3)for(int i=0;i<3;i++)if(!Has("route_drain_piece_"+i)&&debris[i])return debris[i].position;
                    if(Has("route_drain_branch_vault")&&!Has("route_drain_vault_ready"))return trunk?trunk.position:RoutePoint(drainDistance+10,0);
                    return RoutePoint(drainDistance+24,0);
                }
                if(ActiveChapter==3)
                {
                    if(!Has("route_rescue_assessed"))return casualty?casualty.position:RoutePoint(rescueDistance,1.4f);
                    if(!RescueChoiceMade)return treatmentKit?treatmentKit.position:RoutePoint(rescueDistance+2,-1.8f);
                    if(Has("route_rescue_branch_treat")&&!Has("route_rescue_stabilized"))return treatmentKit.position;
                    if(Has("route_rescue_branch_treat")&&Has("route_rescue_stabilized"))return Has("route_rescue_walk_consent")?S.physicalDecisions.evacuationCenter.arrivalPoint.position:casualty.position;
                    if(Has("route_rescue_branch_signal")&&!Has("route_rescue_signaled"))return signalPost.position;
                    return safeLanding?safeLanding.position:RoutePoint(rescueDistance+24,0);
                }
                return S&&S.physicalDecisions&&S.physicalDecisions.evacuationCenter&&S.physicalDecisions.evacuationCenter.arrivalPoint?S.physicalDecisions.evacuationCenter.arrivalPoint.position:S.route.Sample(S.route.length);
            }
        }
        public Transform ContextTarget { get { var focused=Focused();return focused?.visual; } }
        public string Prompt
        {
            get
            {
                var target=working??Focused();if(target==null)return null;
                if(target.kind==TargetKind.DrainVolunteer)return Has("route_drain_spoken")?"E  Report this blockage and continue":"E  Ask the barangay volunteer about the blockage";
                if(target.kind==TargetKind.Casualty)return Has("route_rescue_stabilized")?"E  Ask whether they can walk and want company":"E  Ask what help is needed";
                if(target.kind==TargetKind.DrainDebris)return workProgress>=.35f?"Keep E held · Carry this piece to the sack":"Hold E to grip the loose piece, then carry it to the sack";
                if(target.kind==TargetKind.VaultGrip)return "E  Inspect obstruction · do not climb";
                if(target.kind==TargetKind.Treatment)return  "Hold E  Offer your prepared first-aid kit  ·  "+SecondsLeft(TreatmentDuration)+"s";
                return "Hold E  Signal the nearby responder  ·  "+SecondsLeft(SignalDuration)+"s";
            }
        }
        int SecondsLeft(float duration)=>Mathf.Max(1,Mathf.CeilToInt((1-workProgress)*duration));
        float DrainWorkDuration=>HasPrepared("gloves")||HasPrepared("tongs")?1.15f:2.1f;
        float TreatmentDuration=>HasPrepared("first-aid")?2.2f:3.8f;
        float SignalDuration=>HasPrepared("radio")||HasPrepared("whistle")?1.3f:3.2f;
        public string Hint
        {
            get
            {
                if(ActiveChapter==2)
                {
                    if(!Has("route_drain_spoken"))return "Speak with the volunteer beside the blocked drain.";
                    if(!DrainChoiceMade)return "Interact with loose debris to offer help, or speak to the volunteer again to report and continue.";
                    if(Has("route_drain_branch_clear")&&DrainDebrisCount<3)return "Aim at each marked debris piece and hold E until it is inside the sack.";
                    if(Has("route_drain_branch_vault")&&!Has("route_drain_vault_ready"))return "Inspect open access around the obstruction. Do not climb the timber.";
                    return "Continue beyond the blockage to complete the passage.";
                }
                if(ActiveChapter==3)
                {
                    if(!Has("route_rescue_assessed"))return "Approach the injured evacuee and press E to assess them.";
                    if(!RescueChoiceMade)return "Use the LEFT treatment pouch for hands-on aid, or the RIGHT signal post to call a responder.";
                    if(Has("route_rescue_branch_treat")&&!Has("route_rescue_stabilized"))return "Offer your prepared supplies with permission, or call trained help.";
                    if(Has("route_rescue_branch_treat"))return Has("route_rescue_walk_consent")?"Accompany the evacuee to school reception. Stay close; stop at water or obstructions. J opens the assistance request.":"Ask whether the evacuee can walk and wants company. Stay close on dry, clear ground.";
                    if(!Has("route_rescue_signaled"))return "Hold E at the orange signal post until the responder acknowledges you.";
                    return "The responder is approaching. Keep the route clear and continue when they reach the evacuee.";
                }
                return "Continue toward Banago Elementary School I.";
            }
        }
        public string ClearInstructions=>Hint+"\nWASD move · Mouse look · E interact/hold · Shift run";

        void Start(){BuildAndLayout();RestorePresentation();}
        public void ResetRun(){BuildAndLayout();RestorePresentation();}
        public void RestoreRun(){BuildAndLayout();RestorePresentation();}
        public void Layout(){if(Application.isPlaying){BuildAndLayout();RestorePresentation();}}

        void BuildAndLayout()
        {
            if(!S||!S.route||Data==null)return;
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);targets.Clear();working=null;workProgress=escortProgress=helperProgress=0;
            runtimeRoot=new GameObject("CONTINUOUS ROUTE - Incidents 2 and 3").transform;runtimeRoot.SetParent(transform,false);
            drainDistance=Mathf.Min(S.route.length-360,Mathf.Max(185,Data.routeStart+225));
            rescueDistance=Mathf.Min(S.route.length-70,Mathf.Max(drainDistance+230,Data.routeStart+585));
            BuildDrainIncident();BuildRescueIncident();
            var saved=S.GetComponent<SibucaoEvacuationExperience>()?.State;
            if(saved!=null&&saved.escortPositionSaved&&Vector3.Distance(saved.escortPosition,S.route.Sample(S.route.Progress(saved.escortPosition)))<20)casualty.position=saved.escortPosition;
            if(saved!=null&&saved.responderPositionSaved&&Vector3.Distance(saved.responderPosition,RoutePoint(rescueDistance,0))<50)helper.position=saved.responderPosition;
            ready=true;Physics.SyncTransforms();
        }

        void BuildDrainIncident()
        {
            drainRoot=WorldGroup("Incident 2 - Blocked drainage passage",RoutePoint(drainDistance,0),RouteRotation(drainDistance));
            volunteer=CreateNpc("Barangay volunteer",drainRoot,new Vector3(2.15f,0,-1.5f),1f);volunteerAnimator=volunteer?volunteer.GetComponentInChildren<Animator>():null;
            Register(drainRoot,TargetKind.DrainVolunteer,0,volunteer,new Vector3(1.25f,2.15f,1.25f),new Vector3(2.15f,1.05f,-1.5f));
            WorldText(drainRoot,"VOLUNTEER",new Vector3(2.15f,2.25f,-1.5f),Quaternion.identity,.028f,new Color(.5f,1,.9f));
            sack=LocalGroup("Community debris sack",drainRoot,new Vector3(-2.15f,.1f,5.3f),Quaternion.identity);
            Part(sack,"Open waste sack",PrimitiveType.Cylinder,new Vector3(0,.35f,0),new Vector3(.48f,.36f,.48f),new Color(.08f,.16f,.12f));
            Part(drainRoot,"Blocked concrete drain",PrimitiveType.Cube,new Vector3(-2.15f,.05f,7.1f),new Vector3(1.2f,.12f,1.05f),new Color(.22f,.25f,.25f));
            for(int i=0;i<3;i++)
            {
                debris[i]=LocalGroup("Drain debris "+(i+1),drainRoot,new Vector3(-2.55f+i*.45f,.24f,6.55f+(i%2)*.55f),Quaternion.Euler(i*8,i*37,0));
                var piece=Part(debris[i],i==0?"Wet plastic sheet":i==1?"Branch bundle":"Loose plastic container",i==1?PrimitiveType.Cylinder:PrimitiveType.Cube,Vector3.zero,i==1?new Vector3(.12f,.62f,.12f):new Vector3(.44f,.13f,.34f),i==0?new Color(.12f,.32f,.55f):i==1?new Color(.32f,.22f,.13f):new Color(.43f,.46f,.45f),i==1?Quaternion.Euler(90,0,0):(Quaternion?)null);
                Register(drainRoot,TargetKind.DrainDebris,i,piece,new Vector3(.95f,.85f,.95f),debris[i].localPosition+Vector3.up*.25f);
            }
            trunk=LocalGroup("Fallen timber step-over",drainRoot,new Vector3(-2.2f,.3f,9.2f),Quaternion.Euler(0,0,90));
            Part(trunk,"Storm-fallen trunk",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.22f,.85f,.22f),new Color(.28f,.18f,.09f));
            var grip=Part(trunk,"Cyan tested handhold",PrimitiveType.Sphere,new Vector3(0,.72f,0),new Vector3(.18f,.18f,.18f),new Color(.2f,.95f,.82f));
            Register(trunk,TargetKind.VaultGrip,0,grip,new Vector3(1.2f,1.2f,1.2f),new Vector3(0,.72f,0));
            BuildChoiceSign(drainRoot,"OPTIONAL\nASK VOLUNTEER",new Vector3(-2.15f,0,4.5f),new Color(.2f,.88f,.7f));
            BuildChoiceSign(drainRoot,"KEEP CLEAR\nINSPECT SIDE ROUTE",new Vector3(2.5f,0,4.5f),new Color(1,.72f,.3f));
        }

        void BuildRescueIncident()
        {
            rescueRoot=WorldGroup("Incident 3 - Injured evacuee response",RoutePoint(rescueDistance,0),RouteRotation(rescueDistance));
            casualty=CreateNpc("Injured evacuee",rescueRoot,new Vector3(1.25f,0,0),.92f);casualtyAnimator=casualty?casualty.GetComponentInChildren<Animator>():null;
            if(casualty)casualty.localRotation=Quaternion.Euler(0,-80,0);
            Register(rescueRoot,TargetKind.Casualty,0,casualty,new Vector3(1.65f,1.45f,1.65f),new Vector3(1.25f,.7f,0));
            treatmentKit=LocalGroup("LEFT treatment pouch",rescueRoot,new Vector3(-1.65f,.18f,2.8f),Quaternion.identity);
            var kit=Part(treatmentKit,"Community treatment pouch",PrimitiveType.Cube,Vector3.zero,new Vector3(.42f,.2f,.32f),new Color(.78f,.12f,.1f));
            Part(treatmentKit,"White aid mark",PrimitiveType.Cube,new Vector3(0,.22f,-.34f),new Vector3(.18f,.045f,.05f),Color.white);
            Register(rescueRoot,TargetKind.Treatment,0,kit,new Vector3(1.15f,.9f,1.15f),new Vector3(-1.65f,.42f,2.8f));
            signalPost=LocalGroup("RIGHT responder signal",rescueRoot,new Vector3(2.15f,0,2.8f),Quaternion.identity);
            Part(signalPost,"Orange signal post",PrimitiveType.Cylinder,new Vector3(0,1,0),new Vector3(.055f,1,.055f),new Color(.95f,.4f,.08f));
            var signal=Part(signalPost,"Signal plate",PrimitiveType.Cube,new Vector3(0,1.72f,0),new Vector3(.55f,.34f,.06f),new Color(.75f,.2f,.04f));
            WorldText(signalPost,"CALL\nHELP",new Vector3(0,1.72f,-.07f),Quaternion.identity,.032f,Color.white);
            Register(signalPost,TargetKind.Signal,0,signal,new Vector3(1.2f,1.25f,1.2f),new Vector3(0,1.7f,0));
            BuildChoiceSign(rescueRoot,"OFFER\nSUPPLIES",new Vector3(-1.65f,0,4.2f),new Color(.35f,1,.82f));
            BuildChoiceSign(rescueRoot,"REQUEST\nRESPONDER",new Vector3(2.15f,0,4.2f),new Color(1,.72f,.3f));
            helper=CreateNpc("Community responder",rescueRoot,new Vector3(1.25f,0,6f),1f);helperAnimator=helper?helper.GetComponentInChildren<Animator>():null;if(helper)helper.gameObject.SetActive(false);
            safeLanding=WorldGroup("Green safe landing",RoutePoint(rescueDistance+24,-1.2f),RouteRotation(rescueDistance+24));
            Part(safeLanding,"Safe landing marker",PrimitiveType.Cylinder,new Vector3(0,.04f,0),new Vector3(1.15f,.035f,1.15f),new Color(.12f,.72f,.48f));
            WorldText(safeLanding,"RENDEZVOUS\nCHECK FOOTING",new Vector3(0,.75f,-.03f),Quaternion.identity,.035f,new Color(.45f,1,.75f));
        }

        public bool Interact()
        {
            var target=Focused();if(target==null)return false;
            if(target.kind==TargetKind.DrainVolunteer&&Has("route_drain_spoken"))
            {CancelWork();Flag("route_drain_reported");Flag("route_drain_complete");S.Say("Volunteer: Blockage reported. Continue to school reception; do not enter the drain.",6);S.RequestCheckpoint();return true;}
            if(target.kind==TargetKind.DrainDebris)
            {
                if(!Has("route_drain_spoken")||(!HasPrepared("gloves")&&!HasPrepared("tongs"))||S.player.WaterDepthAtFeet>.02f)
                {S.Notify("Do not handle the debris","Handling requires supervision, protective equipment and dry footing. Report it to the volunteer instead.");return true;}
                Flag("route_drain_branch_clear");
            }
            if(target.kind==TargetKind.VaultGrip)
            {S.Notify("Use a clear passage","Do not climb unstable timber. Inspect the open side of the road, or report the blockage to the volunteer.");return true;}
            if(target.kind==TargetKind.Casualty&&Has("route_rescue_stabilized"))
            {Flag("route_rescue_walk_consent");S.Say("Evacuee: I can walk. Please stay close, and stop if the path is obstructed or wet.",6);S.RequestCheckpoint();return true;}
            if(target.kind==TargetKind.Treatment&&(!HasPrepared("first-aid")||S.player.WaterDepthAtFeet>.02f))
            {S.Notify("Request trained assistance","Offering supplies requires a prepared kit and dry footing. Your voice remains available to request trained help.");return true;}
            if(target.kind==TargetKind.DrainVolunteer&&!Has("route_drain_spoken"))
            {
                Flag("route_drain_spoken");S.state.scores[2]+=8;S.Say("Volunteer: Only collect these loose pieces from dry ground with protective gear. Leave the drain itself alone. Speak to me again to report it and continue.",9);S.Notify("Optional assistance","You can continue to school without clearing this blockage.");S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Casualty&&!Has("route_rescue_assessed"))
            {
                Flag("route_rescue_assessed");S.state.scores[0]+=10;S.Say("Evacuee: I have a small scrape and feel shaken. Please call a responder. If you have a kit, may I use the supplies?",8);S.Notify("Assessment complete","Offer supplies or call the responder. Do not move someone who cannot walk safely.");S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Treatment&&Has("route_rescue_assessed")&&!RescueChoiceMade){Flag("route_rescue_branch_treat");S.physicalDecisions?.RecordRouteDecision("injured-evacuee","supplies-and-company","selected",HasPrepared("first-aid"));working=target;workProgress=0;S.Notify("Hands-on response selected","Offer supplies with permission; this does not replace trained assessment.");return true;}
            if(target.kind==TargetKind.Signal&&Has("route_rescue_assessed")&&!Has("route_rescue_signaled")){Data.flags.Remove("route_rescue_branch_treat");Flag("route_rescue_branch_signal");S.physicalDecisions?.RecordRouteDecision("injured-evacuee","signal-help","selected",HasPrepared("radio")||HasPrepared("whistle"));working=target;workProgress=0;S.Notify("Responder signal selected","Keep signaling until the responder acknowledges you.");return true;}
            if(target.kind==TargetKind.DrainDebris&&Has("route_drain_branch_clear")&&!Has("route_drain_piece_"+target.index)){CancelWork();working=target;SetCarryCollision(target.index,true);return true;}
            if(target.kind==TargetKind.VaultGrip&&Has("route_drain_branch_vault")&&!Has("route_drain_vault_ready")){working=target;return true;}
            if(target.kind==TargetKind.Treatment&&Has("route_rescue_branch_treat")&&!Has("route_rescue_stabilized")){working=target;return true;}
            if(target.kind==TargetKind.Signal&&Has("route_rescue_branch_signal")&&!Has("route_rescue_signaled")){working=target;return true;}
            return false;
        }

        void Update()
        {
            if(!ready||!S)return;
            if(S.GetComponent<FloodSimulationManager>()?.IsCatchingUp==true){CancelWork();return;}
            if(S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete){UpdateRescue(S.route.length);RestorePresentation();return;}
            if(S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked||S.inventory&&S.inventory.BlocksInput||S.missionGuide&&S.missionGuide.BlocksInput){CancelWork();return;}
            float progress=S.route.Progress(S.player.transform.position);
            if(!DrainDiscovered&&!DrainComplete&&progress>drainDistance-22&&progress<drainDistance+32){Flag("route_drain_discovered");S.Notify("Storm debris blocking the route","A barangay volunteer is waiting beside the blocked drain.");}
            UpdateWork();UpdateDrain(progress);
            if(!RescueDiscovered&&!RescueComplete&&progress>rescueDistance-24&&progress<rescueDistance+36){Flag("route_rescue_discovered");S.Notify("Injured evacuee ahead","Assess the person beside the route before deciding how to respond.");}
            UpdateRescue(progress);RestorePresentation();
            var saved=S.GetComponent<SibucaoEvacuationExperience>()?.State;
            if(saved!=null&&Has("route_rescue_stabilized")&&casualty){saved.escortPosition=casualty.position;saved.escortPositionSaved=true;}
            if(saved!=null&&Has("route_rescue_signaled")&&helper){saved.responderPosition=helper.position;saved.responderPositionSaved=true;}
        }

        static bool CancelledForTesting()
        {
#if UNITY_EDITOR
            return SibucaoEditorInput.Active&&SibucaoEditorInput.Cancel;
#else
            return false;
#endif
        }
        void SetCarryCollision(int index,bool ignore)
        {
            if(index<0||index>=debris.Length||!debris[index]||!S||!S.player)return;
            var controller=S.player.GetComponent<CharacterController>();
            if(controller)foreach(var collider in debris[index].GetComponentsInChildren<Collider>())Physics.IgnoreCollision(collider,controller,ignore);
        }
        void CancelWork()
        {
            if(working!=null&&working.kind==TargetKind.DrainDebris&&!Has("route_drain_piece_"+working.index))
            {
                int i=working.index;
                SetCarryCollision(i,false);
                debris[i].SetParent(drainRoot,false);
                debris[i].localPosition=new Vector3(-2.55f+i*.45f,.24f,6.55f+(i%2)*.55f);
            }
            working=null;workProgress=0;
        }
        void UpdateWork()
        {
            if(working==null)return;
            if(SibucaoControls.Button(Key.Q).wasPressedThisFrame||CancelledForTesting())
            {
                CancelWork();return;
            }
            bool held=Keyboard.current!=null&&SibucaoControls.Button(Key.E).isPressed||SibucaoControls.WorkHeld;
#if UNITY_EDITOR
            if(SibucaoEditorInput.Active)held=SibucaoEditorInput.UseHeld;
#endif
            if(Flat(S.player.view.transform.position,working.visual.position)>Reach||working.kind!=TargetKind.DrainDebris&&Focused()!=working){CancelWork();return;}
            if(!held){CancelWork();return;}
            var hands=S.player.hands?S.player.hands.GetComponent<SibucaoHandPresentation>():null;
            hands?.DriveIncident(working.visual,working.kind==TargetKind.Treatment?4:working.kind==TargetKind.Signal?1:working.kind==TargetKind.DrainDebris?6:2,workProgress);
            if(working.kind==TargetKind.DrainDebris)
            {
                // Grip, then carry toward the sack; work depends on moving the load.
                var item=debris[working.index];item.position=Vector3.Lerp(item.position,S.player.view.transform.TransformPoint(new Vector3(.25f,-.65f,1)),Time.deltaTime*5);
                if(S.player.WaterDepthAtFeet>.02f){CancelWork();S.Notify("Handling stopped","Return to dry footing. Report the remaining debris instead.");return;}
                if(workProgress>=.35f&&Flat(S.player.transform.position,sack.position)>2.2f)return;
            }
            float duration=working.kind==TargetKind.DrainDebris?DrainWorkDuration:working.kind==TargetKind.VaultGrip?2.1f:working.kind==TargetKind.Treatment?TreatmentDuration:SignalDuration;
            workProgress=Mathf.Clamp01(workProgress+Time.deltaTime/duration);if(workProgress<1)return;
            FinishWork(working);working=null;workProgress=0;
        }

        void FinishWork(Target target)
        {
            if(target.kind==TargetKind.DrainDebris)
            {
                SetCarryCollision(target.index,false);
                Flag("route_drain_piece_"+target.index);debris[target.index].SetParent(sack,true);debris[target.index].localPosition=new Vector3((target.index-1)*.2f,.65f,target.index*.08f);debris[target.index].localRotation=Quaternion.identity;
                if(!HasPrepared("gloves")&&!HasPrepared("tongs")){Data.stamina=Mathf.Max(0,Data.stamina-(Has("side_family_complete")?1:3));Flag("route_drain_bare_hand");}
                S.state.scores[1]+=6;SibucaoSoundFeedback.Play(SibucaoSoundKind.Placement);S.Notify("Debris bagged",DrainDebrisCount>=3?"The drain is opening. Watch the water recede as you continue.":"Loose pieces remaining: "+(3-DrainDebrisCount));
            }
            else if(target.kind==TargetKind.VaultGrip){Flag("route_drain_vault_ready");Data.stamina=Mathf.Max(0,Data.stamina-4);S.Notify("Handhold tested","Step past the timber and continue along the marked road line.");}
            else if(target.kind==TargetKind.Treatment)
            {
                Flag("route_rescue_stabilized");Flag("route_rescue_used_prepared_first_aid");S.state.scores[0]+=18;S.state.scores[2]+=12;
                if(casualty)casualty.localRotation=Quaternion.identity;S.Say("Evacuee: Thank you for the supplies. Please check with me before we walk.",7);S.Notify("Supplies offered","Speak to the evacuee before accompanying them, or request the responder.");
            }
            else
            {
                Flag("route_rescue_signaled");if(HasPrepared("radio"))Flag("route_rescue_used_radio");if(HasPrepared("whistle"))Flag("route_rescue_used_whistle");if(helper)helper.gameObject.SetActive(true);S.state.scores[2]+=10;S.Notify("Responder acknowledged","You may continue to reception. Arrival at the evacuee is tracked separately.");
            }
            S.RequestCheckpoint();
        }

        void UpdateDrain(float progress)
        {
            if(!DrainActive)return;
            bool workDone=Has("route_drain_branch_clear")?DrainDebrisCount>=3:Has("route_drain_vault_ready");
            if(workDone)
            {
                Flag("route_drain_complete");S.state.scores[3]+=Has("route_drain_branch_clear")?18:8;if(Has("route_drain_branch_clear"))Flag("route_drain_community_benefit");S.Notify("Loose material removed","Continue toward the school. Only local runoff is affected; reassess any water ahead.");S.RequestCheckpoint();
                S.physicalDecisions?.RecordRouteDecision("blocked-drainage",DrainChoiceLabel.ToLowerInvariant(),"resolved",HasPrepared("gloves")||HasPrepared("tongs"),true,false);
            }
            else if(progress>drainDistance+32&&!DrainComplete)
            {
                Flag("route_drain_bypassed");Flag("route_drain_complete");S.physicalDecisions?.RecordRouteDecision("blocked-drainage","continue-evacuation","resolved",false,true,false);S.Notify("Continuing evacuation","The blockage remains unresolved. You can report it at reception.");S.RequestCheckpoint();
            }
        }

        void UpdateRescue(float progress)
        {
            if(!RescueActive)return;
            bool nearby=casualty&&Flat(S.player.transform.position,casualty.position)<=8;
            if(Has("route_rescue_branch_treat")&&Has("route_rescue_stabilized")&&Has("route_rescue_walk_consent"))
            {
                float targetProgress=Mathf.Clamp(Mathf.Max(rescueDistance+4,progress-1.2f),0,S.route.length);
                bool reception=S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete;
                var saved=S.GetComponent<SibucaoEvacuationExperience>().State;if(saved.escortTrail==null)saved.escortTrail=new List<Vector3>();
                var leader=reception?S.physicalDecisions.evacuationCenter.arrivalPoint.position:S.player.transform.position;
                var target=SibucaoSafeNpcMotion.TrailTarget(saved.escortTrail,leader,casualty.position,reception);
                if(casualty){bool moved=nearby&&SibucaoSafeNpcMotion.Step(casualty,target,1.35f,S);casualty.rotation=Quaternion.Slerp(casualty.rotation,Quaternion.LookRotation(RouteForward(targetProgress)),Time.deltaTime*5);PlayAnimation(casualtyAnimator,moved);escortProgress=Mathf.Max(escortProgress,S.route.Progress(casualty.position)-rescueDistance);}
                if(casualty){saved.escortPosition=casualty.position;saved.escortPositionSaved=true;if(S.physicalDecisions.evacuationCenter.HasArrived(casualty.position))CompleteRescue("route_rescue_escorted",20,"Evacuee checked in at school reception");}
            }
            else if(Has("route_rescue_branch_signal")&&Has("route_rescue_signaled"))
            {
                helperProgress=Mathf.Min(9,helperProgress+Time.deltaTime*(HasPrepared("radio")||HasPrepared("whistle")?1.65f:1f));
                if(helper&&casualty){var target=casualty.position-RouteForward(rescueDistance)*.8f;bool moved=SibucaoSafeNpcMotion.Step(helper,target,1.8f,S);helper.rotation=Quaternion.Slerp(helper.rotation,Quaternion.LookRotation((target-helper.position).sqrMagnitude>.01f?(target-helper.position).normalized:RouteForward(rescueDistance)),Time.deltaTime*5);PlayAnimation(helperAnimator,moved);}
                if(helperProgress>=9&&helper&&casualty&&Flat(helper.position,casualty.position)<1.6f)CompleteRescue("route_rescue_responder_arrived",20,"Responder reached the injured evacuee");
            }
            else if(progress>rescueDistance+36&&!RescueComplete)
            {
                Flag("route_rescue_bypassed");CompleteRescue("route_rescue_left_behind",0,"Continuing to reception · report the evacuee's location");
            }
        }

        void CompleteRescue(string flag,int teamwork,string title)
        {if(RescueComplete)return;Flag(flag);Flag("route_rescue_complete");S.state.scores[2]+=teamwork;S.state.scores[3]+=Has("route_rescue_bypassed")?0:10;string branch=Has("route_rescue_bypassed")?"bypass":Has("route_rescue_branch_treat")?"supplies-and-company":"signal-help";S.physicalDecisions?.RecordRouteDecision("injured-evacuee",branch,"resolved",HasPrepared("first-aid")||HasPrepared("radio")||HasPrepared("whistle"),true,false);S.Notify(title,"Continue to Banago Elementary School I. Your response was recorded.");S.RequestCheckpoint();}

        public bool ArriveByAssistance(Vector3 origin,Vector3 destination)
        {
            if(!casualty||!Has("route_rescue_walk_consent")||RescueComplete||Flat(casualty.position,origin)>8)return false;
            casualty.position=destination+Vector3.forward;
            var saved=S.GetComponent<SibucaoEvacuationExperience>().State;saved.escortPosition=casualty.position;saved.escortPositionSaved=true;
            Flag("evacuee_assisted_transport");CompleteRescue("route_rescue_escorted",20,"Evacuee checked in after assisted transport");return true;
        }

        void RestorePresentation()
        {
            if(!ready)return;
            for(int i=0;i<3;i++)if(debris[i]&&Has("route_drain_piece_"+i)&&debris[i].parent!=sack){debris[i].SetParent(sack,true);debris[i].localPosition=new Vector3((i-1)*.2f,.65f,i*.08f);debris[i].localRotation=Quaternion.identity;}
            if(helper)helper.gameObject.SetActive(Has("route_rescue_signaled")&&!RescueComplete||Has("route_rescue_responder_arrived"));
            if(casualty&&!Has("route_rescue_stabilized"))PlayAnimation(casualtyAnimator,false);
            PlayAnimation(volunteerAnimator,false);
        }

        Target Focused()
        {
            if(!ready||!S||!S.player||!S.player.view)return null;
            var view=S.player.view.transform;var hits=Physics.RaycastAll(view.position,view.forward,Reach,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.transform.IsChildOf(S.player.transform))continue;if(targets.TryGetValue(hit.collider,out var target)&&Available(target))return target;if(!hit.collider.isTrigger)break;}return null;
        }
        bool Available(Target target)
        {
            if(target.kind==TargetKind.DrainVolunteer)return DrainActive;
            if(target.kind==TargetKind.DrainDebris)return DrainActive&&Has("route_drain_spoken")&&!Has("route_drain_piece_"+target.index);
            if(target.kind==TargetKind.VaultGrip)return DrainActive;
            if(target.kind==TargetKind.Casualty)return RescueActive&&(!Has("route_rescue_assessed")||Has("route_rescue_stabilized")&&!Has("route_rescue_walk_consent"));
            if(target.kind==TargetKind.Treatment)return RescueActive&&Has("route_rescue_assessed")&&(!RescueChoiceMade||Has("route_rescue_branch_treat")&&!Has("route_rescue_stabilized"));
            return RescueActive&&Has("route_rescue_assessed")&&!Has("route_rescue_signaled");
        }
        void Register(Transform root,TargetKind kind,int index,Transform visual,Vector3 size,Vector3 localCenter)
        {if(!visual)return;var zone=new GameObject("Interaction - "+kind+" "+index);zone.transform.SetParent(root,false);zone.transform.localPosition=localCenter;var collider=zone.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=size;var target=new Target{kind=kind,index=index,visual=visual};targets[collider]=target;foreach(var visible in visual.GetComponentsInChildren<Collider>(true))targets[visible]=target;}
        Transform CreateNpc(string displayName,Transform parent,Vector3 localPosition,float scale)
        {
            var optional=S.GetComponent<SibucaoOptionalSideMission>();GameObject prefab=optional?optional.parentPrefab:null;
            if(prefab)
            {
                var instance=Instantiate(prefab,parent);instance.name=displayName+" - animated resident";instance.transform.localPosition=localPosition;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one*scale;
                foreach(var resident in instance.GetComponentsInChildren<SibucaoResident>(true))resident.enabled=false;foreach(var actor in instance.GetComponentsInChildren<SibucaoActor>(true))actor.enabled=false;foreach(var body in instance.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
                var lit=Shader.Find("Universal Render Pipeline/Lit");
                if(lit)foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var converted=new Material[renderer.sharedMaterials.Length];
                    for(int i=0;i<converted.Length;i++)
                    {
                        var source=renderer.sharedMaterials[i];var material=new Material(lit){name=(source?source.name:displayName)+" - runtime URP"};
                        if(source)
                        {
                            if(source.HasProperty("_MainTex"))material.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
                            if(source.HasProperty("_Color"))material.SetColor("_BaseColor",source.GetColor("_Color"));
                            material.mainTextureScale=source.mainTextureScale;material.mainTextureOffset=source.mainTextureOffset;
                        }
                        converted[i]=material;
                    }
                    renderer.sharedMaterials=converted;
                }
                return instance.transform;
            }
            var fallback=LocalGroup(displayName+" - fallback",parent,localPosition,Quaternion.identity);Part(fallback,"Raincoat body",PrimitiveType.Capsule,new Vector3(0,.9f,0),new Vector3(.32f,.55f,.27f),new Color(.12f,.38f,.5f));Part(fallback,"Head",PrimitiveType.Sphere,new Vector3(0,1.68f,0),new Vector3(.23f,.23f,.23f),new Color(.48f,.31f,.23f));fallback.localScale=Vector3.one*scale;return fallback;
        }
        void PlayAnimation(Animator animator,bool walking){if(!animator||!animator.isActiveAndEnabled)return;animator.Play(walking?"metarig|Walk":"metarig|Idle",0,Mathf.Repeat(Time.time*(walking?1.1f:.45f),1));animator.applyRootMotion=false;}
        void BuildChoiceSign(Transform parent,string text,Vector3 localPosition,Color color)
        {var sign=LocalGroup(text.Replace("\n"," ")+" sign",parent,localPosition,Quaternion.identity);Part(sign,"Post",PrimitiveType.Cylinder,new Vector3(0,.78f,0),new Vector3(.045f,.78f,.045f),new Color(.3f,.22f,.14f));Part(sign,"Board",PrimitiveType.Cube,new Vector3(0,1.46f,0),new Vector3(.72f,.36f,.055f),new Color(color.r*.28f,color.g*.28f,color.b*.28f));WorldText(sign,text,new Vector3(0,1.46f,-.065f),Quaternion.identity,.033f,color);}
        Transform WorldGroup(string name,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(runtimeRoot,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;}
        static Transform LocalGroup(string name,Transform parent,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go.transform;}
        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion? rotation=null)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=scale;var renderer=go.GetComponent<Renderer>();if(renderer){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}return go.transform;}
        void WorldText(Transform parent,string text,Vector3 position,Quaternion rotation,float size,Color color)
        {var go=new GameObject("Route label - "+text.Replace("\n"," "));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.fontSize=72;mesh.characterSize=size*.22f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;}
        Vector3 RoutePoint(float distance,float lane){var forward=RouteForward(distance);return S.route.Sample(Mathf.Clamp(distance,0,S.route.length))+Vector3.Cross(Vector3.up,forward)*lane;}
        Vector3 RouteForward(float distance){var forward=S.route.Sample(Mathf.Min(S.route.length,distance+2))-S.route.Sample(Mathf.Max(0,distance-2));forward.y=0;return forward.sqrMagnitude>.001f?forward.normalized:Vector3.forward;}
        Quaternion RouteRotation(float distance)=>Quaternion.LookRotation(RouteForward(distance));
        bool Has(string flag)=>Data!=null&&Data.flags!=null&&Data.flags.Contains(flag);
        void Flag(string flag){if(S.physicalDecisions)S.physicalDecisions.SetFlag(flag);else if(Data!=null&&!Data.flags.Contains(flag))Data.flags.Add(flag);}
        PhysicalGrabbable PackedPrepared(string id){if(!S.inventory)return null;for(int i=0;i<SibucaoInventory.Capacity;i++){var item=S.inventory.Slot(i);var prep=item?item.GetComponent<PreparednessItem>():null;if(prep&&prep.id==id)return item;}return null;}
        bool HasPrepared(string id){var experience=S.GetComponent<SibucaoEvacuationExperience>();if(experience)return experience.HasGear(id);var item=PackedPrepared(id);var prep=item?item.GetComponent<PreparednessItem>():null;return prep&&prep.prepared;}
        static float Flat(Vector3 a,Vector3 b){var d=a-b;return new Vector2(d.x,d.z).magnitude;}
#if UNITY_EDITOR
        public void EditorPrepareDrain(bool clear){Flag("route_drain_discovered");Flag("route_drain_spoken");Flag(clear?"route_drain_branch_clear":"route_drain_branch_vault");RestorePresentation();}
        public void EditorPrepareRescue(bool treat){Flag("route_drain_complete");Flag("route_rescue_discovered");Flag("route_rescue_assessed");Flag(treat?"route_rescue_branch_treat":"route_rescue_branch_signal");RestorePresentation();}
#endif
    }
}
