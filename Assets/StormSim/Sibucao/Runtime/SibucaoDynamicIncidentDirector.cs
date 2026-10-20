using System;
using System.Collections.Generic;
using StormSim.Preparedness;
using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>
    /// Chooses three authored, location-aware emergency situations for each evacuation.
    /// Incidents are discovered in the world, not presented as numbered stations.
    /// </summary>
    [DefaultExecutionOrder(-69)]
    public sealed class SibucaoDynamicIncidentDirector : MonoBehaviour
    {
        public enum IncidentKind { BlackoutHousehold, SeparatedChild, StalledTricycle, FloodRouteChange, InjuredEvacuee }
        enum TargetRole { Caller, OptionA, OptionB, Final }

        sealed class Target
        {
            public Incident incident;
            public TargetRole role;
            public Transform visual;
            public string prompt;
        }

        sealed class Incident
        {
            public IncidentKind kind;
            public string id,title;
            public float distance;
            public int difficulty;
            public Transform root,caller,secondary,finalVisual,movingProp;
            public Animator callerAnimator,secondaryAnimator;
            public readonly List<Target> targets=new List<Target>();
            public bool active,engaged,resolved,bypassed,prepared;
            public int step;
            public string branch;
            public Target working;
            public float workProgress,activeTime,settle;
            public Light inspectionLight;
        }

        const float NoticeRange=27f;
        const float Reach=5.25f;
        readonly List<Incident> incidents=new List<Incident>();
        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S&&S.state!=null?S.state.physical:null;
        PhysicalRunData scheduledData;
        Incident active;
        Transform runtimeRoot;
        AudioClip attentionClip;
        GUIStyle cardTitle,cardText,promptStyle,smallStyle;
        float nextIncidentTime;
#if UNITY_EDITOR
        Vector3 debugLastActivationAnchor;
#endif

        public bool HasActiveIncident=>active!=null&&!active.resolved;
        public string ActiveIncidentId=>active!=null?active.id:string.Empty;
        public float ActiveProgress=>active!=null?active.workProgress:0;
        public string ContextPrompt {get{var target=Focused();return target==null?null:target.prompt+(active.working==target?" · HOLD":"");}}
        public string CurrentObjective=>active==null?null:active.step==0?"Investigate the nearby call for help.":active.step==1?Objective(active):FinalInstruction(active);

        // Experimental templates are retained for authoring, but are no longer auto-added.
        // Live QA found a generated facade blocking the road and incompatible NPC materials.
        // Re-enable only in a deliberately authored and validated scene.

        void Awake()
        {
            // Retire the fixed station prototypes. The optional opening family mission remains available.
            var chapter=GetComponent<SibucaoContinuousChapterOne>();if(chapter)chapter.enabled=false;
            var flood=GetComponent<SibucaoFloodedStreetIncident>();if(flood)flood.enabled=true;
            var route=GetComponent<SibucaoRouteIncidentSequence>();if(route)route.enabled=true;
            BuildAttentionAudio();
        }

        void Start(){Schedule();}

        void OnDestroy()
        {
            if(attentionClip)Destroy(attentionClip);
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);
        }

        void Update()
        {
            if(!S||Data==null||!S.route||!S.player)return;
            if(scheduledData!=Data){ClearRuntime();Schedule();}
            if(S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked||S.inventory&&S.inventory.BlocksInput||S.missionGuide&&S.missionGuide.BlocksInput){if(active!=null){active.working=null;active.workProgress=0;}return;}

            float progress=S.route.Progress(S.player.transform.position);
            if(active==null&&Time.unscaledTime>=nextIncidentTime&&!OtherMissionBusy())
            {
                foreach(var incident in incidents)
                {
                    if(incident.resolved||incident.active||Has("director_large_"+incident.id+"_resolved")||Has("director_large_"+incident.id+"_bypassed"))continue;
                    if(progress>incident.distance+29){incident.resolved=true;continue;}
                    if(progress>=incident.distance-NoticeRange){Activate(incident);break;}
                }
            }

            if(active!=null)
            {
                var current=active;
                current.activeTime+=Time.deltaTime;
                FaceCaller(current);
                UpdateWorking(current);
                DriveInteractionAnimations(current);
                if(!current.resolved&&progress>current.distance+(current.engaged?43f:29f))Bypass(current);
            }

            foreach(var incident in incidents)UpdateAftermath(incident);
        }

        void Schedule()
        {
            if(!S||Data==null||!S.route||!S.player)return;
            scheduledData=Data;
            runtimeRoot=new GameObject("Evacuation Incident Director - runtime encounters").transform;

            // One optional surprise between the three main situations preserves breathing room.
            var pool=new List<IncidentKind>{IncidentKind.BlackoutHousehold,IncidentKind.SeparatedChild};
            var random=new System.Random(Data.seed^0x41D1C7);
            for(int i=pool.Count-1;i>0;i--){int j=random.Next(i+1);var swap=pool[i];pool[i]=pool[j];pool[j]=swap;}
            float start=Mathf.Clamp(Data.routeStart+32,28,Mathf.Max(28,S.route.length-120));
            float available=Mathf.Max(105,S.route.length-start-42);
            float[] zones={.55f};
            for(int i=0;i<1;i++)
            {
                float jitter=((float)random.NextDouble()-.5f)*Mathf.Min(24,available*.05f);
                float distance=Mathf.Clamp(start+available*zones[i]+jitter,start+18,S.route.length-28);
                BuildIncident(pool[i],distance,i+1,random.Next(0,2)==0?-1:1);
            }
            Physics.SyncTransforms();
        }

        void ClearRuntime()
        {
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);
            runtimeRoot=null;incidents.Clear();active=null;scheduledData=null;nextIncidentTime=0;
        }

        void BuildIncident(IncidentKind kind,float distance,int difficulty,int side)
        {
            var incident=new Incident{kind=kind,distance=distance,difficulty=difficulty};
            Describe(incident);
            Vector3 position=Grounded(RoutePoint(distance,side*1.2f));
            incident.root=WorldGroup(incident.title+" - "+incident.id,position,RouteRotation(distance));
            incidents.Add(incident);
            switch(kind)
            {
                case IncidentKind.BlackoutHousehold: BuildBlackout(incident);break;
                case IncidentKind.SeparatedChild: BuildChild(incident);break;
                case IncidentKind.StalledTricycle: BuildTricycle(incident);break;
                case IncidentKind.FloodRouteChange: BuildFloodChange(incident);break;
                default: BuildInjured(incident);break;
            }
            Restore(incident);
            incident.root.gameObject.SetActive(false);
        }

        static void Describe(Incident incident)
        {
            switch(incident.kind)
            {
                case IncidentKind.BlackoutHousehold:incident.id="blackout-household";incident.title="Trapped household";break;
                case IncidentKind.SeparatedChild:incident.id="separated-child";incident.title="Separated child";break;
                case IncidentKind.StalledTricycle:incident.id="stalled-tricycle";incident.title="Stalled evacuation tricycle";break;
                case IncidentKind.FloodRouteChange:incident.id="flood-route-change";incident.title="Sudden route flooding";break;
                default:incident.id="injured-evacuee";incident.title="Injured evacuee";break;
            }
        }

        void BuildBlackout(Incident e)
        {
            var house=LocalGroup("Dark roadside household",e.root,new Vector3(0,0,0),Quaternion.identity);
            Part(house,"Concrete frontage",PrimitiveType.Cube,new Vector3(0,1.35f,0),new Vector3(2.2f,1.35f,.18f),new Color(.17f,.19f,.19f));
            Part(house,"Unlit doorway",PrimitiveType.Cube,new Vector3(0,1.05f,-.2f),new Vector3(.62f,1.05f,.08f),new Color(.018f,.025f,.026f));
            for(int i=0;i<3;i++)Part(house,"Wind-thrown roofing "+i,PrimitiveType.Cube,new Vector3(-.55f+i*.55f,.28f,-1.05f+i*.12f),new Vector3(.52f,.045f,.52f),new Color(.27f,.31f,.31f),Quaternion.Euler(7,i*19,5));
            e.caller=CreateNpc("Resident at dark house",e.root,new Vector3(1.25f,0,-.75f),1f);e.callerAnimator=AnimatorOf(e.caller);
            AddTarget(e,TargetRole.Caller,e.caller,"E  Ask what happened");
            var lamp=Part(e.root,"Dark doorway inspection point",PrimitiveType.Sphere,new Vector3(-.55f,1.05f,-.55f),Vector3.one*.16f,new Color(.12f,.75f,.78f));
            AddTarget(e,TargetRole.OptionA,lamp,"E  Use light and inspect the doorway");
            var call=Part(e.root,"Household signal point",PrimitiveType.Cylinder,new Vector3(1.35f,.55f,-1.25f),new Vector3(.18f,.55f,.18f),new Color(.95f,.55f,.12f));
            AddTarget(e,TargetRole.OptionB,call,"E  Contact or call nearby responders");
            var debris=Part(e.root,"Loose doorway debris",PrimitiveType.Cube,new Vector3(0,.42f,-1.05f),new Vector3(.9f,.08f,.42f),new Color(.48f,.49f,.45f),Quaternion.Euler(0,15,8));
            e.finalVisual=debris;// Roofing is a non-interactive hazard, never a repair task.
        }

        void BuildChild(Incident e)
        {
            e.caller=CreateNpc("Parent searching in the rain",e.root,new Vector3(.8f,0,-.6f),1f);e.callerAnimator=AnimatorOf(e.caller);
            AddTarget(e,TargetRole.Caller,e.caller,"E  Speak with the parent");
            var signal=Part(e.root,"Audible search point",PrimitiveType.Cylinder,new Vector3(-1.05f,.4f,.25f),new Vector3(.15f,.4f,.15f),new Color(.95f,.58f,.12f));
            AddTarget(e,TargetRole.OptionA,signal,"E  Signal with a whistle or your voice");
            var search=Part(e.root,"Visible search clue",PrimitiveType.Cube,new Vector3(1.1f,.08f,1.2f),new Vector3(.32f,.07f,.32f),new Color(.14f,.76f,.8f),Quaternion.Euler(0,22,0));
            AddTarget(e,TargetRole.OptionB,search,"E  Search the sheltered roadside");
            e.secondary=CreateNpc("Separated child",e.root,new Vector3(-1.75f,0,4.4f),.82f);e.secondaryAnimator=AnimatorOf(e.secondary);e.secondary.gameObject.SetActive(false);
            AddTarget(e,TargetRole.Final,e.secondary,"E  Reassure the child and reunite them");e.finalVisual=e.secondary;
        }

        void BuildTricycle(Incident e)
        {
            var vehicle=LocalGroup("Stalled evacuation tricycle",e.root,new Vector3(0,.22f,1.15f),Quaternion.Euler(0,4,0));e.movingProp=vehicle;
            Part(vehicle,"Motorcycle body",PrimitiveType.Cube,new Vector3(0,.42f,0),new Vector3(.4f,.24f,1.05f),new Color(.12f,.29f,.44f));
            Part(vehicle,"Sidecar",PrimitiveType.Cube,new Vector3(.82f,.43f,.05f),new Vector3(.6f,.4f,.82f),new Color(.15f,.38f,.52f));
            for(int i=0;i<3;i++)Part(vehicle,"Wheel "+i,PrimitiveType.Cylinder,new Vector3(i==2?.82f:0,.22f,i==0?-.7f:.62f),new Vector3(.26f,.09f,.26f),new Color(.025f,.025f,.025f),Quaternion.Euler(0,0,90));
            e.caller=CreateNpc("Tricycle driver",e.root,new Vector3(-1.05f,0,-.4f),1f);e.callerAnimator=AnimatorOf(e.caller);AddTarget(e,TargetRole.Caller,e.caller,"E  Check on the driver");
            var push=Part(e.root,"Rear push grip",PrimitiveType.Cube,new Vector3(0,.72f,2.15f),new Vector3(.55f,.08f,.13f),new Color(.12f,.8f,.72f));
            AddTarget(e,TargetRole.OptionA,push,"E  Push the tricycle toward the roadside");
            var signal=Part(e.root,"Assistance signal point",PrimitiveType.Cylinder,new Vector3(-1.45f,.46f,1.5f),new Vector3(.16f,.46f,.16f),new Color(.95f,.57f,.12f));
            AddTarget(e,TargetRole.OptionB,signal,"E  Call the barangay transport team");
            var crate=Part(e.root,"Loose family supply crate",PrimitiveType.Cube,new Vector3(.82f,.92f,.08f),new Vector3(.42f,.28f,.42f),new Color(.43f,.28f,.13f));
            e.finalVisual=crate;AddTarget(e,TargetRole.Final,crate,"E  Secure the loose family supplies");
        }

        void BuildFloodChange(Incident e)
        {
            e.caller=CreateNpc("Route volunteer",e.root,new Vector3(1.25f,0,-.65f),1f);e.callerAnimator=AnimatorOf(e.caller);AddTarget(e,TargetRole.Caller,e.caller,"E  Ask about the sudden flooding");
            for(int i=0;i<5;i++)Part(e.root,"Fast runoff "+i,PrimitiveType.Cube,new Vector3(-1.4f+i*.7f,.035f,2.7f),new Vector3(.38f,.025f,1.3f),new Color(.09f,.27f,.34f));
            var info=Part(e.root,"Weather information point",PrimitiveType.Cylinder,new Vector3(-1.15f,.48f,.1f),new Vector3(.16f,.48f,.16f),new Color(.95f,.57f,.12f));
            AddTarget(e,TargetRole.OptionA,info,"E  Verify the detour using radio or phone");
            var depth=Part(e.root,"Runoff depth edge",PrimitiveType.Cylinder,new Vector3(.15f,.09f,1.5f),new Vector3(.34f,.045f,.34f),new Color(.12f,.8f,.76f));
            AddTarget(e,TargetRole.OptionB,depth,"E  Inspect depth and current from dry ground");
            var safe=Part(e.root,"Safe roadside handrail",PrimitiveType.Cube,new Vector3(-2.15f,.82f,4.2f),new Vector3(.08f,.82f,1.25f),new Color(.2f,.72f,.48f));
            e.finalVisual=safe;AddTarget(e,TargetRole.Final,safe,"E  Confirm the raised detour is stable");
        }

        void BuildInjured(Incident e)
        {
            e.caller=CreateNpc("Injured evacuee",e.root,new Vector3(.3f,0,.2f),1f);e.caller.localRotation=Quaternion.Euler(0,0,68);e.callerAnimator=AnimatorOf(e.caller);AddTarget(e,TargetRole.Caller,e.caller,"E  Check breathing and bleeding");
            var treatment=Part(e.root,"Treatment position",PrimitiveType.Sphere,new Vector3(-.25f,.28f,-.25f),Vector3.one*.18f,new Color(.12f,.78f,.72f));
            AddTarget(e,TargetRole.OptionA,treatment,"E  Give first aid and stabilize the leg");
            var signal=Part(e.root,"Responder signal position",PrimitiveType.Cylinder,new Vector3(1.3f,.5f,-.25f),new Vector3(.16f,.5f,.16f),new Color(.95f,.56f,.12f));
            AddTarget(e,TargetRole.OptionB,signal,"E  Signal a trained responder");
            e.secondary=CreateNpc("Community responder",e.root,new Vector3(-2.4f,0,4.6f),1f);e.secondaryAnimator=AnimatorOf(e.secondary);e.secondary.gameObject.SetActive(false);
            var clear=Part(e.root,"Safe escort footing",PrimitiveType.Cube,new Vector3(-1.2f,.05f,2.5f),new Vector3(.55f,.05f,.55f),new Color(.2f,.66f,.45f));
            e.finalVisual=clear;AddTarget(e,TargetRole.Final,clear,"E  Help move the evacuee to safe footing");
        }

        void Activate(Incident incident)
        {
            float progress=S.route.Progress(S.player.transform.position);
            var saved=S.GetComponent<SibucaoEvacuationExperience>()?.State;
            Vector3 forward;Vector3 anchor;
            if(saved!=null&&saved.dynamicAnchorSaved){anchor=saved.dynamicAnchor;forward=saved.dynamicForward;}
            else
            {
                anchor=FindOpenPlacement(RouteForward(progress),out forward);
                if(saved!=null){saved.dynamicAnchor=anchor;saved.dynamicForward=forward;saved.dynamicAnchorSaved=true;}
            }
            if(forward.sqrMagnitude<.01f)forward=RouteForward(progress);
#if UNITY_EDITOR
            debugLastActivationAnchor=anchor;
#endif
            incident.root.SetPositionAndRotation(anchor,Quaternion.LookRotation(forward));
            incident.root.gameObject.SetActive(true);
            active=incident;incident.active=true;incident.activeTime=0;
            PlayAttention(incident.root.position);
            string cue=incident.kind==IncidentKind.BlackoutHousehold?"Metal crashes against a dark doorway nearby.":incident.kind==IncidentKind.SeparatedChild?"A parent is calling a child's name through the wind.":incident.kind==IncidentKind.StalledTricycle?"A loaded tricycle sputters and blocks part of the road.":incident.kind==IncidentKind.FloodRouteChange?"Fresh runoff suddenly cuts across the street ahead.":"Someone calls for help from the roadside.";
            S.Say(cue,6);S.Notify("Something is happening nearby","Look toward the sound and decide whether to help or keep evacuating.");
            Flag("director_large_"+incident.id+"_noticed");S.RequestCheckpoint();
        }

        public bool Interact()
        {
            if(active==null||active.resolved)return false;
            var target=Focused();if(target==null)return false;
            if(target.role==TargetRole.Caller&&active.step==0){Engage(active);return true;}
            if(active.step==1&&(target.role==TargetRole.OptionA||target.role==TargetRole.OptionB)){Choose(active,target);return true;}
            if(active.step==2&&target.role==TargetRole.Final){active.working=target;active.workProgress=0;SibucaoControls.BeginGrip();return true;}
            return false;
        }

        void Engage(Incident incident)
        {
            incident.engaged=true;incident.step=1;SetTargetVisibility(incident);
            string line=incident.kind==IncidentKind.BlackoutHousehold?"Resident: The power failed and roofing fell across our doorway. Two people are still inside.":incident.kind==IncidentKind.SeparatedChild?"Parent: We were separated when the water rose. Please help me call or search the nearby shelter.":incident.kind==IncidentKind.StalledTricycle?"Driver: The engine died and these supplies are sliding loose. We need the road clear.":incident.kind==IncidentKind.FloodRouteChange?"Volunteer: The main line flooded moments ago. Verify the raised edge before anyone follows it.":"Evacuee: My leg is bleeding. I cannot stand safely without help.";
            S.Say(line,8);S.Notify(incident.title,Objective(incident));
            Flag("director_large_"+incident.id+"_engaged");S.RequestCheckpoint();
        }

        void Choose(Incident incident,Target target)
        {
            if(incident.kind==IncidentKind.BlackoutHousehold&&target.role==TargetRole.OptionA&&!HasPrepared("flashlight"))
            {S.Notify("A light is needed","Use the responder contact point. The household can still receive help without a flashlight.");return;}
            incident.branch=target.role==TargetRole.OptionA?"A":"B";
            Flag("director_large_"+incident.id+"_branch_"+incident.branch);
            incident.prepared=PreparedFor(incident,target.role);
            incident.working=target;incident.workProgress=0;
            if(incident.kind==IncidentKind.BlackoutHousehold&&incident.prepared&&incident.branch=="A"&&!incident.inspectionLight)
            {
                var lamp=new GameObject("Prepared flashlight inspection");lamp.transform.SetParent(incident.root,true);
                incident.inspectionLight=lamp.AddComponent<Light>();incident.inspectionLight.type=LightType.Spot;incident.inspectionLight.range=9;incident.inspectionLight.spotAngle=52;incident.inspectionLight.intensity=4;
            }
            if(incident.kind==IncidentKind.SeparatedChild&&incident.branch=="A"&&HasPrepared("whistle"))PlayAttention(S.player.transform.position);
            S.physicalDecisions?.RecordRouteDecision(incident.id,BranchName(incident),"selected",incident.prepared);
            S.Notify("Response selected",WorkInstruction(incident));
            SibucaoControls.BeginGrip();
        }

        void UpdateWorking(Incident incident)
        {
            if(incident.working==null)return;
            if(!incident.working.visual||Flat(S.player.view.transform.position,incident.working.visual.position)>Reach+1.7f){incident.working=null;incident.workProgress=0;return;}
            bool held=SibucaoControls.WorkHeld;
#if UNITY_EDITOR
            if(SibucaoEditorInput.Active)held=SibucaoEditorInput.UseHeld;
#endif
            if(!held||Focused()!=incident.working){incident.working=null;incident.workProgress=0;return;}
            float duration=WorkDuration(incident);
            incident.workProgress=Mathf.Clamp01(incident.workProgress+Time.deltaTime/duration);
            if(incident.workProgress<1)return;
            var completed=incident.working;incident.working=null;incident.workProgress=0;
            if(incident.step==1)FinishFirstAction(incident,completed);else Resolve(incident);
        }

        void FinishFirstAction(Incident incident,Target target)
        {
            if(incident.kind==IncidentKind.BlackoutHousehold)
            {
                Flag("director_large_"+incident.id+"_reported");
                S.Say("Resident: We have heard your call. Keep away from the roofing and damaged electrical equipment. Tell school reception where we are.",7);
                Resolve(incident);return;
            }
            incident.step=2;Flag("director_large_"+incident.id+"_step2");SetTargetVisibility(incident);
            if(incident.kind==IncidentKind.SeparatedChild&&incident.secondary)incident.secondary.gameObject.SetActive(true);
            if(incident.kind==IncidentKind.InjuredEvacuee&&incident.branch=="B"&&incident.secondary)incident.secondary.gameObject.SetActive(true);
            if(incident.kind==IncidentKind.BlackoutHousehold)S.Say("Keep clear of the damaged frontage.",6);
            else if(incident.kind==IncidentKind.SeparatedChild)S.Say("Child: I'm here! I could not see the road through the rain.",6);
            else if(incident.kind==IncidentKind.StalledTricycle)S.Say("Driver: The lane is almost clear. Secure the box before it falls into the water.",6);
            else if(incident.kind==IncidentKind.FloodRouteChange)S.Say("Volunteer: The raised roadside edge is the safer line. Test the handrail before we redirect people.",7);
            else S.Say(incident.branch=="A"?"The bleeding is controlled. Move the evacuee onto stable footing.":"Responder: I have them. Help us clear one stable step.",7);
            S.Notify(incident.title,FinalInstruction(incident));S.RequestCheckpoint();
        }

        void Resolve(Incident incident)
        {
            if(incident.inspectionLight)incident.inspectionLight.enabled=false;
            incident.resolved=true;incident.active=false;incident.settle=0;
            Flag("director_large_"+incident.id+"_resolved");
            int staminaCost=StaminaCost(incident);Data.stamina=Mathf.Max(0,Data.stamina-staminaCost);
            S.state.scores[2]+=incident.prepared?16:10;S.state.scores[3]+=incident.difficulty*4;
            S.physicalDecisions?.RecordRouteDecision(incident.id,BranchName(incident),"resolved",incident.prepared,true,false);
            S.GetComponent<SibucaoEvacuationExperience>()?.Record("optional-"+incident.id,ResolutionTitle(incident),incident.kind==IncidentKind.BlackoutHousehold?"Contact established; household evacuation remains unconfirmed.": "Contact with the child was established. Physical reunion is confirmed only when the child reaches the parent.","Missing one tool changes the response; asking for help remains a useful option.",false);
            S.Notify(ResolutionTitle(incident),incident.prepared?"Your prepared equipment made the response faster and safer. Continue toward the school.":"You completed the response using the safe fallback. Continue toward the school.");
            S.RequestCheckpoint();nextIncidentTime=Time.unscaledTime+12;active=null;SetTargetVisibility(incident);
        }

        void Bypass(Incident incident)
        {
            incident.resolved=true;incident.bypassed=true;incident.active=false;
            Flag("director_large_"+incident.id+"_bypassed");
            S.physicalDecisions?.RecordRouteDecision(incident.id,"continued-evacuation","resolved",false,true,false);
            S.Notify("You continued evacuating","The outcome remains unconfirmed. You can report the location at school reception.");
            if(incident.root)incident.root.gameObject.SetActive(false);
            S.RequestCheckpoint();nextIncidentTime=Time.unscaledTime+10;active=null;SetTargetVisibility(incident);
        }

        void UpdateAftermath(Incident incident)
        {
            if(!incident.resolved||incident.bypassed)return;
            incident.settle+=Time.deltaTime;
            if(incident.kind==IncidentKind.SeparatedChild&&incident.secondary&&incident.caller)
            {
                MoveNpc(incident.secondary,incident.secondaryAnimator,incident.caller.position-incident.root.forward*.75f,1.45f);
            }
            else if(incident.kind==IncidentKind.StalledTricycle&&incident.movingProp)
            {
                incident.movingProp.localPosition=Vector3.MoveTowards(incident.movingProp.localPosition,new Vector3(1.9f,.22f,2.4f),Time.deltaTime*.65f);
            }
            else if(incident.kind==IncidentKind.InjuredEvacuee&&incident.caller)
            {
                Vector3 destination=incident.finalVisual?incident.finalVisual.position:incident.caller.position;
                MoveNpc(incident.caller,incident.callerAnimator,destination,incident.branch=="A"?.7f:.45f);
                if(incident.secondary&&incident.secondary.gameObject.activeSelf)MoveNpc(incident.secondary,incident.secondaryAnimator,incident.caller.position-incident.root.forward*.65f,1.1f);
            }
        }

        void DriveInteractionAnimations(Incident incident)
        {
            if(incident==null||incident.resolved)return;
            if(incident.inspectionLight)incident.inspectionLight.transform.SetPositionAndRotation(S.player.view.transform.position,S.player.view.transform.rotation);
            var hands=S.player&&S.player.hands?S.player.hands.GetComponent<SibucaoHandPresentation>():null;
            if(incident.working!=null)
            {
                int action=HandAction(incident);hands?.DriveIncident(incident.working.visual,action,incident.workProgress);
                var primary=Participant(incident.caller);
                var secondary=Participant(incident.secondary);
                var pose=CounterpartPose(incident);
                Transform toward=incident.working.visual;
                if(pose==SibucaoIncidentParticipantAnimator.Pose.Receive||pose==SibucaoIncidentParticipantAnimator.Pose.Assist)toward=S.player.hands?S.player.hands:incident.working.visual;
                primary?.Drive(pose,toward,incident.workProgress);
                if(incident.kind==IncidentKind.SeparatedChild&&incident.step==2)
                    secondary?.Drive(SibucaoIncidentParticipantAnimator.Pose.Receive,incident.caller,incident.workProgress);
                else if(incident.kind==IncidentKind.InjuredEvacuee&&incident.secondary&&incident.secondary.gameObject.activeSelf)
                    secondary?.Drive(SibucaoIncidentParticipantAnimator.Pose.Assist,incident.caller,incident.workProgress);
                return;
            }

            if(!incident.engaged)return;
            Transform objective=null;
            foreach(var target in incident.targets)if(Available(target)){objective=target.visual;break;}
            var idlePose=incident.kind==IncidentKind.SeparatedChild?SibucaoIncidentParticipantAnimator.Pose.Call:
                incident.kind==IncidentKind.InjuredEvacuee?SibucaoIncidentParticipantAnimator.Pose.Injured:SibucaoIncidentParticipantAnimator.Pose.Point;
            Participant(incident.caller)?.Drive(idlePose,objective?objective:S.player.hands,0);
        }

        int HandAction(Incident incident)
        {
            if(incident.step==2)
            {
                if(incident.kind==IncidentKind.BlackoutHousehold||incident.kind==IncidentKind.StalledTricycle)return 6;
                if(incident.kind==IncidentKind.SeparatedChild||incident.kind==IncidentKind.InjuredEvacuee)return 5;
                return 2;
            }
            if(incident.kind==IncidentKind.SeparatedChild)return incident.branch=="A"?1:3;
            if(incident.kind==IncidentKind.StalledTricycle)return incident.branch=="A"?2:1;
            if(incident.kind==IncidentKind.InjuredEvacuee)return incident.branch=="A"?4:1;
            if(incident.kind==IncidentKind.BlackoutHousehold)return incident.branch=="A"?0:1;
            return 0;
        }

        SibucaoIncidentParticipantAnimator.Pose CounterpartPose(Incident incident)
        {
            if(incident.kind==IncidentKind.BlackoutHousehold)return incident.step==2?SibucaoIncidentParticipantAnimator.Pose.Brace:incident.branch=="B"?SibucaoIncidentParticipantAnimator.Pose.Call:SibucaoIncidentParticipantAnimator.Pose.Point;
            if(incident.kind==IncidentKind.SeparatedChild)return incident.step==2?SibucaoIncidentParticipantAnimator.Pose.Receive:incident.branch=="A"?SibucaoIncidentParticipantAnimator.Pose.Call:SibucaoIncidentParticipantAnimator.Pose.Point;
            if(incident.kind==IncidentKind.StalledTricycle)return incident.step==2?SibucaoIncidentParticipantAnimator.Pose.Brace:incident.branch=="A"?SibucaoIncidentParticipantAnimator.Pose.Push:SibucaoIncidentParticipantAnimator.Pose.Call;
            if(incident.kind==IncidentKind.FloodRouteChange)return incident.step==2?SibucaoIncidentParticipantAnimator.Pose.Brace:SibucaoIncidentParticipantAnimator.Pose.Point;
            return incident.step==2?SibucaoIncidentParticipantAnimator.Pose.Assist:SibucaoIncidentParticipantAnimator.Pose.Injured;
        }

        static SibucaoIncidentParticipantAnimator Participant(Transform npc)=>npc?npc.GetComponent<SibucaoIncidentParticipantAnimator>():null;

        void Restore(Incident incident)
        {
            incident.resolved=Has("director_large_"+incident.id+"_resolved")||Has("director_large_"+incident.id+"_bypassed");
            incident.bypassed=Has("director_large_"+incident.id+"_bypassed");
            incident.engaged=Has("director_large_"+incident.id+"_engaged");
            incident.step=Has("director_large_"+incident.id+"_step2")?2:incident.engaged?1:0;
            incident.branch=Has("director_large_"+incident.id+"_branch_B")?"B":"A";
            SetTargetVisibility(incident);
        }

        void SetTargetVisibility(Incident incident)
        {
            foreach(var target in incident.targets)
            {
                if(!target.visual)continue;
                bool visible=target.role==TargetRole.Caller||target.role==TargetRole.OptionA||target.role==TargetRole.OptionB;
                if(target.role==TargetRole.OptionA||target.role==TargetRole.OptionB)visible=incident.engaged&&incident.step==1&&!incident.resolved;
                if(target.role==TargetRole.Final)visible=incident.engaged&&incident.step==2&&!incident.resolved;
                if(target.role==TargetRole.Caller&&target.visual==incident.caller)visible=true;
                if(incident.kind==IncidentKind.SeparatedChild&&target.role==TargetRole.Final)visible=incident.step>=2&&!incident.bypassed;
                if(target.visual.gameObject!=incident.caller?.gameObject&&target.visual.gameObject!=incident.secondary?.gameObject)target.visual.gameObject.SetActive(visible);
            }
        }

        Target Focused()
        {
            if(active==null||!S.player||!S.player.view)return null;
            Transform view=S.player.view.transform;Target best=null;float bestScore=.7f;
            foreach(var target in active.targets)
            {
                if(!Available(target)||!target.visual||!target.visual.gameObject.activeInHierarchy)continue;
                Vector3 delta=target.visual.position+Vector3.up*.7f-view.position;float distance=delta.magnitude;if(distance>Reach||distance<.01f)continue;
                if(Physics.Raycast(view.position,delta.normalized,out var obstruction,distance-.05f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!obstruction.transform.IsChildOf(target.visual)&&!obstruction.transform.IsChildOf(S.player.transform))continue;
                float score=Vector3.Dot(view.forward,delta/distance)-distance*.012f;if(score>bestScore){bestScore=score;best=target;}
            }
            return best;
        }

        bool Available(Target target)
        {
            if(target.incident!=active||active.resolved)return false;
            if(active.step==0)return target.role==TargetRole.Caller;
            if(active.step==1)return target.role==TargetRole.OptionA||target.role==TargetRole.OptionB;
            return target.role==TargetRole.Final;
        }

        void FaceCaller(Incident incident)
        {
            if(!incident.caller||incident.resolved)return;
            Vector3 direction=S.player.transform.position-incident.caller.position;direction.y=0;
            if(direction.sqrMagnitude>.2f)incident.caller.rotation=Quaternion.Slerp(incident.caller.rotation,Quaternion.LookRotation(direction),Time.deltaTime*2.2f);
            PlayAnimation(incident.callerAnimator,false);
        }

        void MoveNpc(Transform npc,Animator animator,Vector3 destination,float speed)
        {
            Vector3 delta=destination-npc.position;delta.y=0;
            bool moving=delta.sqrMagnitude>.12f;
            if(moving){moving=SibucaoSafeNpcMotion.Step(npc,destination,speed,S);npc.rotation=Quaternion.Slerp(npc.rotation,Quaternion.LookRotation(delta),Time.deltaTime*4);}
            PlayAnimation(animator,moving);
        }

        bool OtherMissionBusy()
        {
            var experience=GetComponent<SibucaoEvacuationExperience>();if(experience&&experience.AtRestPoint)return true;
            var flood=GetComponent<SibucaoFloodedStreetIncident>();if(flood&&flood.Active)return true;
            var sequence=GetComponent<SibucaoRouteIncidentSequence>();if(sequence&&sequence.ProgressVisible)return true;
            var micro=GetComponent<SibucaoIncidentDirector>();if(micro&&micro.VisionObscured)return true;
            var optional=GetComponent<SibucaoOptionalSideMission>();if(optional&&optional.SideMissionActive)return true;
            return S.activeEncounter||S.player.vehicle||S.player.ActivePhysicalOption||S.inventory&&S.inventory.BlocksInput||S.missionGuide&&S.missionGuide.BlocksInput;
        }

        string Objective(Incident e)
        {
            switch(e.kind)
            {
                case IncidentKind.BlackoutHousehold:return "Choose how to locate the obstruction without entering the dark house.";
                case IncidentKind.SeparatedChild:return "Choose a signal or a careful roadside search.";
                case IncidentKind.StalledTricycle:return "Clear the vehicle safely without scattering its supplies.";
                case IncidentKind.FloodRouteChange:return "Verify a dry detour before directing anyone onto it.";
                default:return "Choose first aid or signal trained help, then secure safe footing.";
            }
        }

        string WorkInstruction(Incident e)
        {
            if(e.kind==IncidentKind.BlackoutHousehold)return e.branch=="A"?"Hold E to inspect the door from outside.":"Hold E to establish contact and account for the household.";
            if(e.kind==IncidentKind.SeparatedChild)return e.branch=="A"?"Hold E to give a clear repeated signal.":"Hold E to check the sheltered roadside methodically.";
            if(e.kind==IncidentKind.StalledTricycle)return e.branch=="A"?"Hold E to push from the rear grip.":"Hold E until transport assistance acknowledges.";
            if(e.kind==IncidentKind.FloodRouteChange)return e.branch=="A"?"Hold E to verify the latest route advisory.":"Hold E to judge depth and current from dry ground.";
            return e.branch=="A"?"Hold E to apply pressure and secure the dressing.":"Hold E until a trained responder acknowledges.";
        }

        string FinalInstruction(Incident e)
        {
            if(e.kind==IncidentKind.BlackoutHousehold)return "Keep clear and report the damaged doorway.";
            if(e.kind==IncidentKind.SeparatedChild)return "Approach the child and hold E to reassure and reunite them.";
            if(e.kind==IncidentKind.StalledTricycle)return "Hold E at the sidecar crate to secure the supplies.";
            if(e.kind==IncidentKind.FloodRouteChange)return "Hold E at the raised handrail to confirm the detour.";
            return "Hold E at the stable footing to complete the assisted move.";
        }

        string BranchName(Incident e)
        {
            if(e.kind==IncidentKind.BlackoutHousehold)return e.branch=="A"?"light-inspection":"contact-responders";
            if(e.kind==IncidentKind.SeparatedChild)return e.branch=="A"?"audible-signal":"sheltered-search";
            if(e.kind==IncidentKind.StalledTricycle)return e.branch=="A"?"push-roadside":"call-transport-team";
            if(e.kind==IncidentKind.FloodRouteChange)return e.branch=="A"?"advisory-detour":"dry-ground-depth-check";
            return e.branch=="A"?"first-aid-assist":"signal-trained-help";
        }

        string ResolutionTitle(Incident e)
        {
            switch(e.kind)
            {
                case IncidentKind.BlackoutHousehold:return "Household location reported";
                case IncidentKind.SeparatedChild:return "Family reunited";
                case IncidentKind.StalledTricycle:return "Evacuation lane reopened";
                case IncidentKind.FloodRouteChange:return "Safe detour verified";
                default:return "Evacuee stabilized";
            }
        }

        bool PreparedFor(Incident e,TargetRole role)
        {
            if(e.kind==IncidentKind.BlackoutHousehold)return role==TargetRole.OptionA?HasPrepared("flashlight"):HasPrepared("phone")||HasPrepared("radio");
            if(e.kind==IncidentKind.SeparatedChild)return role==TargetRole.OptionA?HasPrepared("whistle")||HasPrepared("radio"):HasPrepared("flashlight");
            if(e.kind==IncidentKind.StalledTricycle)return role==TargetRole.OptionA?HasPrepared("gloves"):HasPrepared("phone")||HasPrepared("radio");
            if(e.kind==IncidentKind.FloodRouteChange)return role==TargetRole.OptionA?HasPrepared("radio")||HasPrepared("phone"):HasPrepared("flashlight")||HasPrepared("tongs");
            return role==TargetRole.OptionA?HasPrepared("first-aid"):HasPrepared("radio")||HasPrepared("whistle");
        }

        float WorkDuration(Incident e)
        {
            float baseDuration=e.step==2?2.1f:2.3f+e.difficulty*.22f;
            if(e.prepared)baseDuration*=.58f;
            if(e.kind==IncidentKind.StalledTricycle&&e.branch=="A")baseDuration+=.65f;
            return baseDuration;
        }

        int StaminaCost(Incident e)
        {
            int cost=e.prepared?1:3+e.difficulty;
            if(e.kind==IncidentKind.StalledTricycle&&e.branch=="A")cost+=e.prepared?2:5;
            if(e.kind==IncidentKind.InjuredEvacuee&&e.branch=="A")cost+=2;
            return cost;
        }

        void AddTarget(Incident incident,TargetRole role,Transform visual,string prompt)
        {
            incident.targets.Add(new Target{incident=incident,role=role,visual=visual,prompt=prompt});
        }

        void PlayAttention(Vector3 position)
        {
            if(!attentionClip)return;
            var cue=new GameObject("Spatial emergency cue");cue.transform.position=position;
            var source=cue.AddComponent<AudioSource>();source.clip=attentionClip;source.spatialBlend=1;source.minDistance=2;source.maxDistance=38;source.Play();Destroy(cue,attentionClip.length+.2f);
        }

        void BuildAttentionAudio()
        {
            const int rate=22050;int count=(int)(rate*.72f);var samples=new float[count];
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;float knock=Mathf.Exp(-22*Mathf.Repeat(t,.24f))*Mathf.Sin(t*470*Mathf.PI*2);float wind=Mathf.Sin(t*83)*.08f;
                samples[i]=(knock*.32f+wind)*Mathf.Sin(Mathf.PI*i/(count-1f));
            }
            attentionClip=AudioClip.Create("Distant emergency cue",count,1,rate,false);attentionClip.SetData(samples,0);
        }

        Transform CreateNpc(string displayName,Transform parent,Vector3 localPosition,float scale)
        {
            var optional=S.GetComponent<SibucaoOptionalSideMission>();
            GameObject prefab=displayName=="Community responder"&&optional&&optional.childPrefab?optional.childPrefab:optional?optional.parentPrefab:null;
            if(prefab)
            {
                var instance=Instantiate(prefab,parent);instance.name=displayName+" - animated resident";instance.transform.localPosition=localPosition;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one*scale;
                foreach(var resident in instance.GetComponentsInChildren<SibucaoResident>(true))resident.enabled=false;
                foreach(var actor in instance.GetComponentsInChildren<SibucaoActor>(true))actor.enabled=false;
                foreach(var body in instance.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
                var interaction=instance.GetComponent<SibucaoIncidentParticipantAnimator>();if(!interaction)interaction=instance.AddComponent<SibucaoIncidentParticipantAnimator>();interaction.animator=instance.GetComponentInChildren<Animator>();
                return instance.transform;
            }
            var fallback=LocalGroup(displayName+" - fallback",parent,localPosition,Quaternion.identity);
            Part(fallback,"Raincoat body",PrimitiveType.Capsule,new Vector3(0,.9f,0),new Vector3(.32f,.55f,.27f),new Color(.12f,.38f,.5f));
            Part(fallback,"Head",PrimitiveType.Sphere,new Vector3(0,1.68f,0),Vector3.one*.23f,new Color(.48f,.31f,.23f));fallback.localScale=Vector3.one*scale;return fallback;
        }

        static Animator AnimatorOf(Transform root)=>root?root.GetComponentInChildren<Animator>():null;
        static void PlayAnimation(Animator animator,bool walking){if(!animator||!animator.isActiveAndEnabled)return;animator.Play(walking?"metarig|Walk":"metarig|Idle",0,Mathf.Repeat(Time.time*(walking?1.1f:.45f),1));animator.applyRootMotion=false;}
        Transform WorldGroup(string name,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(runtimeRoot,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;}
        static Transform LocalGroup(string name,Transform parent,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go.transform;}
        static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion? rotation=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=scale;
            var renderer=go.GetComponent<Renderer>();if(renderer){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}return go.transform;
        }
        Vector3 RoutePoint(float distance,float lane){var forward=RouteForward(distance);return S.route.Sample(Mathf.Clamp(distance,0,S.route.length))+Vector3.Cross(Vector3.up,forward)*lane;}
        Vector3 Grounded(Vector3 point)
        {
            var hits=Physics.RaycastAll(point+Vector3.up*12,Vector3.down,24,S.groundMask,QueryTriggerInteraction.Ignore);
            float bestRise=float.MinValue,bestY=point.y;
            foreach(var hit in hits)
            {
                float rise=hit.point.y-point.y;
                if(hit.normal.y>.45f&&rise>=-.35f&&rise<3.5f&&rise>bestRise){bestRise=rise;bestY=hit.point.y;}
            }
            point.y=bestY;return point;
        }
        Vector3 FindOpenPlacement(Vector3 preferred,out Vector3 chosenForward)
        {
            preferred.y=0;if(preferred.sqrMagnitude<.01f)preferred=S.player.transform.forward;preferred.Normalize();
            Vector3 playerForward=S.player.transform.forward;playerForward.y=0;if(playerForward.sqrMagnitude<.01f)playerForward=preferred;playerForward.Normalize();
            Vector3[] choices={preferred,Quaternion.AngleAxis(42,Vector3.up)*preferred,Quaternion.AngleAxis(-42,Vector3.up)*preferred,playerForward,Quaternion.AngleAxis(82,Vector3.up)*playerForward,Quaternion.AngleAxis(-82,Vector3.up)*playerForward,-playerForward};
            Vector3 eye=S.player.view?S.player.view.transform.position:S.player.transform.position+Vector3.up*1.5f;
            foreach(var direction in choices)
            {
                Vector3 flat=direction;flat.y=0;flat.Normalize();
                if(Physics.Raycast(eye,flat,out var hit,5.25f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(S.player.transform))continue;
                chosenForward=flat;Vector3 candidate=S.player.transform.position+flat*5.2f;candidate.y=S.player.transform.position.y;return candidate;
            }
            chosenForward=playerForward;Vector3 fallback=S.player.transform.position+playerForward*3.2f;fallback.y=S.player.transform.position.y;return fallback;
        }
        Vector3 RouteForward(float distance){var forward=S.route.Sample(Mathf.Min(S.route.length,distance+2))-S.route.Sample(Mathf.Max(0,distance-2));forward.y=0;return forward.sqrMagnitude>.001f?forward.normalized:Vector3.forward;}
        Quaternion RouteRotation(float distance)=>Quaternion.LookRotation(RouteForward(distance));
        static float Flat(Vector3 a,Vector3 b){var d=a-b;return new Vector2(d.x,d.z).magnitude;}
        bool Has(string flag)=>Data!=null&&Data.flags!=null&&Data.flags.Contains(flag);
        void Flag(string flag){if(S.physicalDecisions)S.physicalDecisions.SetFlag(flag);else if(Data!=null&&!Data.flags.Contains(flag))Data.flags.Add(flag);}
        bool HasPrepared(string id)
        {
            if(!S.inventory)return false;
            for(int i=0;i<SibucaoInventory.Capacity;i++){var item=S.inventory.Slot(i);var prep=item?item.GetComponent<PreparednessItem>():null;if(prep&&prep.id==id&&prep.prepared)return true;}
            return false;
        }

        void EnsureStyles()
        {
            if(cardTitle!=null)return;
            cardTitle=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,wordWrap=true,normal={textColor=new Color(.43f,.96f,.94f)}};
            cardText=new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true,normal={textColor=Color.white}};
            promptStyle=new GUIStyle(GUI.skin.label){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=Color.white}};
            smallStyle=new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true,normal={textColor=new Color(.78f,.9f,.91f)}};
        }

        void OnGUI()
        {
            if(StormSim.UI.StormglassUI.InUse)return;
            if(!S||S.Mode!=SibucaoMode.Playing||active==null||active.resolved)return;
            var micro=GetComponent<SibucaoIncidentDirector>();if(micro&&micro.VisionObscured)return;
            EnsureStyles();float scale=Mathf.Clamp(Screen.height/760f,.78f,1.25f);
            if(active.engaged)
            {
                Rect card=new Rect(34,132,330*scale,(active.working!=null?151:130)*scale);
                GUI.color=new Color(.025f,.11f,.15f,.93f);GUI.DrawTexture(card,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(new Rect(card.x+14,card.y+10,card.width-28,25*scale),active.title.ToUpperInvariant(),cardTitle);
                string current=active.step==1?Objective(active):FinalInstruction(active);
                GUI.Label(new Rect(card.x+14,card.y+38,card.width-28,57*scale),current,cardText);
                string readiness=active.step==1?"Two valid responses · equipment changes speed and cost":active.prepared?"Prepared method in use":"Safe fallback in use";
                GUI.Label(new Rect(card.x+14,card.y+94,card.width-28,22*scale),readiness,smallStyle);
                if(active.working!=null)
                {
                    Rect track=new Rect(card.x+14,card.y+124*scale,card.width-28,10*scale);
                    GUI.color=new Color(.08f,.22f,.24f,1);GUI.DrawTexture(track,Texture2D.whiteTexture);
                    GUI.color=new Color(.28f,.94f,.86f,1);GUI.DrawTexture(new Rect(track.x,track.y,track.width*active.workProgress,track.height),Texture2D.whiteTexture);GUI.color=Color.white;
                }
            }
            var focused=Focused();
            if(focused!=null)
            {
                Rect prompt=new Rect(Screen.width*.5f-260*scale,Screen.height-126*scale,520*scale,62*scale);
                GUI.color=new Color(.025f,.11f,.15f,.94f);GUI.DrawTexture(prompt,Texture2D.whiteTexture);GUI.color=Color.white;
                string text=focused.prompt+(active.working==focused?" · HOLD":"");GUI.Label(new Rect(prompt.x+15,prompt.y+8,prompt.width-30,prompt.height-16),SibucaoControls.Prompt(text),promptStyle);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Start blackout household")]
        public void DebugStartBlackout(){DebugStartTemplate(0);}
        public void DebugStartTemplate(int templateIndex)
        {
            if(!S||Data==null||!S.route||!S.player)return;
            ClearRuntime();scheduledData=Data;runtimeRoot=new GameObject("Evacuation Incident Director - debug").transform;
            float distance=Mathf.Clamp(S.route.Progress(S.player.transform.position)+8,8,S.route.length-8);
            BuildIncident((IncidentKind)Mathf.Clamp(templateIndex,0,4),distance,2,1);
            var e=incidents[0];Activate(e);Physics.SyncTransforms();
        }
        public void DebugCompleteActive()
        {
            if(active==null)return;
            if(active.step==0)Engage(active);
            active.branch="A";active.prepared=PreparedFor(active,TargetRole.OptionA);active.step=2;SetTargetVisibility(active);Resolve(active);
        }
        public string DebugSnapshot()=>active==null?"active=none scheduled="+incidents.Count:"active="+active.id+" step="+active.step+" branch="+active.branch+" progress="+active.workProgress.ToString("0.00")+" anchor="+debugLastActivationAnchor;
#endif
    }
}
