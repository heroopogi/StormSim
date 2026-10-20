using System;
using System.Collections.Generic;
using StormSim.Preparedness;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    /// <summary>A continuous-route flood assessment. Observations never certify a safe crossing.</summary>
    [DefaultExecutionOrder(-85)]
    public sealed class SibucaoFloodedStreetIncident : MonoBehaviour
    {
        const string RootName="ROUTE INCIDENT - Flooded residential street";
        const float Reach=4.8f;
        enum TargetKind { Advisory, Pole, Probe }
        sealed class Target { public TargetKind kind; public int index; public Transform visual; }

        readonly Dictionary<Collider,Target> targets=new Dictionary<Collider,Target>();
        readonly Transform[] probes=new Transform[2];
        Transform runtimeRoot,advisory,pole,raisedEntry,directEntry;
        float routeDistance,holdProgress;
        int workingProbe=-1;
        bool ready,choiceRecorded;

        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S&&S.state!=null?S.state.physical:null;
        FloodSimulationManager Flood=>S?S.GetComponent<FloodSimulationManager>():null;
        public bool Discovered=>Has("flood_street_discovered");
        public bool AdvisoryRead=>Has("flood_street_advisory_read");
        public bool PoleTaken=>Has("flood_street_pole_taken");
        public bool Complete=>Has("flood_street_complete");
        public bool Active=>Discovered&&!Complete;
        public bool ProgressVisible=>Active;
        public bool WantsLocalGuidance=>Active&&PoleTaken&&SampleCount<2&&!Has("flood_street_assistance");
        public string MissionTitle=>"Assess the flooded residential street";
        public string EquipmentNote=>HasPrepared("radio")?"Radio packed — barangay advisory available":"Community bamboo pole available — no equipment required";
        public int TaskCount=>3;
        public int SampleCount=>(Sampled(0)?1:0)+(Sampled(1)?1:0);
        public string TaskLabel(int index)
        {
            if(index==0)return PoleTaken?"Advisory checked · pole ready":"Read warning (optional)";
            if(index==1)return "Observe safely  "+SampleCount+" / 2";
            return Complete?"Junction passed":"Inspect access / report";
        }
        public float TaskProgress(int index)
        {
            if(index==0)return (AdvisoryRead?0.5f:0)+(PoleTaken?0.5f:0);
            if(index==1)return (Sampled(0)?0.5f:0)+(Sampled(1)?0.5f:0)+(workingProbe>=0?holdProgress*.5f:0);
            if(Complete)return 1;
            if(SampleCount<2||!choiceRecorded)return 0;
            float p=S.route.Progress(S.player.transform.position);
            return Mathf.Clamp01((p-(routeDistance+7))/18f);
        }
        public string TaskValue(int index,float progress)
        {
            if(progress>=.999f)return "DONE";
            if(index==1)return SampleCount+" / 2";
            return index==2&&choiceRecorded?"CROSS":index==0&&AdvisoryRead?"1 / 2":"NOW";
        }
        string ChoiceLabel=>Has("flood_street_route_raised")?"Raised-edge":Has("flood_street_route_direct")?"Road":"Selected";
        public Vector3 IncidentMarker=>advisory?advisory.position:RoutePoint(routeDistance,2.7f);
        public Vector3 NavigationTarget
        {
            get
            {
                if(!AdvisoryRead)return advisory?advisory.position:IncidentMarker;
                if(Has("flood_street_assistance"))return S.route.Sample(S.route.length);
                if(!PoleTaken)return pole?pole.position:IncidentMarker;
                if(SampleCount<2)for(int i=0;i<2;i++)if(!Sampled(i)&&probes[i])return probes[i].position;
                if(!choiceRecorded)return raisedEntry?raisedEntry.position:RoutePoint(routeDistance+8,-2.4f);
                return Has("flood_street_route_raised")?(raisedEntry?raisedEntry.position:RoutePoint(routeDistance+18,-2.4f)):RoutePoint(routeDistance+24,0);
            }
        }
        public Transform ContextTarget { get { var focused=Focused(); return focused?.visual; } }
        public string Prompt
        {
            get
            {
                var target=Focused();if(target==null)return null;
                if(target.kind==TargetKind.Advisory)return AdvisoryRead?"E  Mark the junction for reception reporting":"E  Read the barangay flood advisory";
                if(target.kind==TargetKind.Pole)return AdvisoryRead?"E  Take the community bamboo measuring pole":"Read the advisory first";
                int seconds=Mathf.CeilToInt((1-holdProgress)*ProbeDuration);
                return "Hold E  Measure water point "+(target.index+1)+" of 2  ·  "+seconds+"s";
            }
        }
        public string Hint
        {
            get
            {
                if(!Discovered)return "Continue toward Banago Elementary School I.";
                if(!AdvisoryRead)return "Inspect current conditions and the warning beside the road.";
                if(!PoleTaken)return "Take the bamboo pole leaning beside the advisory. It is available even without prepared gear.";
                if(SampleCount<2)return "Inspect both points from dry footing, or return to the advisory and request route assistance.";
                if(!choiceRecorded)return "Compare your readings and the visible current. Inspect the raised line or ask the volunteer before proceeding.";
                if(!Complete)return "Avoid floodwater. Continue only along dry, unobstructed access or seek assistance.";
                return "Flooded street cleared. Continue toward the school.";
            }
        }
        public string ClearInstructions=>Hint+"\nWASD move · Mouse look · E interact/hold · Shift run";
        float ProbeDuration=>1.75f;

        void Start(){BuildAndLayout();RestorePresentation();}
        public void ResetRun(){BuildAndLayout();RestorePresentation();}
        public void RestoreRun(){BuildAndLayout();RestorePresentation();}
        public void Layout(){if(Application.isPlaying){BuildAndLayout();RestorePresentation();}}

        void BuildAndLayout()
        {
            if(!S||!S.route||Data==null)return;
            if(pole&&pole.parent==S.player.view.transform)Destroy(pole.gameObject);
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);
            targets.Clear();workingProbe=-1;holdProgress=0;choiceRecorded=Has("flood_street_route_raised")||Has("flood_street_route_direct");
            runtimeRoot=new GameObject(RootName).transform;runtimeRoot.SetParent(transform,false);
            routeDistance=Mathf.Min(S.route.length-55,Mathf.Max(55,Data.routeStart+60));

            advisory=WorldGroup("Barangay flood advisory",RoutePoint(routeDistance,2.75f),RouteRotation(routeDistance));
            Part(advisory,"Post",PrimitiveType.Cylinder,new Vector3(0,1,0),new Vector3(.07f,1,.07f),new Color(.24f,.18f,.12f));
            var board=Part(advisory,"Orange advisory board",PrimitiveType.Cube,new Vector3(0,1.9f,0),new Vector3(.85f,.54f,.08f),new Color(.94f,.36f,.08f));
            WorldText(advisory,"FLOOD RISK\nAVOID WATER",new Vector3(0,1.9f,-.09f),Quaternion.identity,.038f,Color.white);
            Register(advisory,TargetKind.Advisory,0,board,new Vector3(1.5f,1.35f,1.2f),new Vector3(0,1.9f,0));

            pole=WorldGroup("Community bamboo measuring pole",RoutePoint(routeDistance+.7f,2.25f),RouteRotation(routeDistance));
            var shaft=Part(pole,"Marked bamboo pole",PrimitiveType.Cylinder,new Vector3(0,.9f,0),new Vector3(.045f,.9f,.045f),new Color(.63f,.5f,.2f),Quaternion.Euler(0,0,8));
            for(int i=1;i<6;i++)Part(pole,"Depth mark "+i,PrimitiveType.Cylinder,new Vector3(0,.3f*i,0),new Vector3(.051f,.012f,.051f),new Color(.05f,.13f,.13f),Quaternion.Euler(0,0,8));
            Register(pole,TargetKind.Pole,0,shaft,new Vector3(1.1f,2.2f,1.1f),new Vector3(0,1,0));

            for(int i=0;i<2;i++)
            {
                float d=routeDistance+4.8f+i*4.1f,lane=i==0?-.7f:.85f;
                probes[i]=WorldGroup("Water measuring point "+(i+1),RoutePoint(d,lane),RouteRotation(d));
                var marker=Part(probes[i],"Striped depth marker",PrimitiveType.Cylinder,new Vector3(0,.06f,0),new Vector3(.42f,.035f,.42f),i==0?new Color(.1f,.75f,.78f):new Color(.95f,.68f,.14f));
                Part(probes[i],"Small marker post",PrimitiveType.Cylinder,new Vector3(0,.34f,0),new Vector3(.045f,.34f,.045f),new Color(.9f,.9f,.82f));
                WorldText(probes[i],"CHECK "+(i+1),new Vector3(0,.78f,-.04f),Quaternion.identity,.032f,Color.white);
                Register(probes[i],TargetKind.Probe,i,marker,new Vector3(1.35f,1.4f,1.35f),new Vector3(0,.45f,0));
            }

            raisedEntry=WorldGroup("LEFT - raised roadside entry",RoutePoint(routeDistance+10,-2.45f),RouteRotation(routeDistance+10));
            Part(raisedEntry,"Raised-edge sign post",PrimitiveType.Cylinder,new Vector3(0,.78f,0),new Vector3(.045f,.78f,.045f),new Color(.3f,.24f,.16f));
            Part(raisedEntry,"Raised-edge sign board",PrimitiveType.Cube,new Vector3(0,1.48f,0),new Vector3(.68f,.34f,.055f),new Color(.06f,.26f,.25f));
            WorldText(raisedEntry,"SIDE ACCESS\nINSPECT FIRST",new Vector3(0,1.48f,-.065f),Quaternion.identity,.034f,new Color(.45f,1,.85f));
            directEntry=WorldGroup("AHEAD - road crossing entry",RoutePoint(routeDistance+10,2.2f),RouteRotation(routeDistance+10));
            Part(directEntry,"Road-water sign post",PrimitiveType.Cylinder,new Vector3(0,.76f,0),new Vector3(.045f,.76f,.045f),new Color(.3f,.24f,.16f));
            Part(directEntry,"Road-water sign board",PrimitiveType.Cube,new Vector3(0,1.43f,0),new Vector3(.68f,.34f,.055f),new Color(.3f,.18f,.05f));
            WorldText(directEntry,"LOW ROAD\nFLOOD RISK",new Vector3(0,1.43f,-.065f),Quaternion.identity,.034f,new Color(1,.82f,.42f));
            // No artificial stepping stones or markers encouraging a floodwater crossing.
            ready=true;Physics.SyncTransforms();
        }

        public bool Interact()
        {
            var target=Focused();if(target==null)return false;
            if(target.kind==TargetKind.Advisory&&AdvisoryRead)
            {
                Flag("flood_street_assistance");choiceRecorded=true;
                S.GetComponent<SibucaoEvacuationExperience>()?.Record("crossing-help","Recorded an uncertain junction","The location is marked for reception. No passage has been certified safe.","Avoid floodwater; seek verified access or assistance rather than treating a measurement as clearance.");
                S.physicalDecisions?.RecordRouteDecision("flooded-street","request-assistance","selected",HasPrepared("radio"));S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Advisory&&!AdvisoryRead)
            {
                Flag("flood_street_advisory_read");if(HasPrepared("radio")){Flag("flood_street_radio_advisory");S.state.scores[0]+=5;}
                S.Say(HasPrepared("radio")?"Radio: The low road is under review. Compare the observations from dry footing or ask the volunteer to confirm raised access.":"Barangay advisory: Observe from dry ground. A pole reading cannot prove moving water is safe. Ask for route assistance if uncertain.",8);
                S.Notify("Flood advisory checked","Measurements are optional. Inspect dry access or mark this location for reception.");S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Pole&&AdvisoryRead&&!PoleTaken)
            {
                Flag("flood_street_pole_taken");S.state.scores[1]+=5;pole.SetParent(S.player.view.transform,true);pole.localPosition=new Vector3(.34f,-.58f,.72f);pole.localRotation=Quaternion.Euler(8,0,-18);pole.localScale=Vector3.one*.55f;
                foreach(var c in pole.GetComponentsInChildren<Collider>())c.enabled=false;
                S.Notify("Measuring pole ready","Hold E at both marked water points.");S.RequestCheckpoint();return true;
            }
            if(target.kind==TargetKind.Probe&&PoleTaken&&!Sampled(target.index)){workingProbe=target.index;return true;}
            return false;
        }

        void Update()
        {
            if(!ready||!S)return;
            if(S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked||S.inventory&&S.inventory.BlocksInput||S.missionGuide&&S.missionGuide.BlocksInput){workingProbe=-1;holdProgress=0;return;}
            float progress=S.route.Progress(S.player.transform.position),distance=Flat(S.player.transform.position,RoutePoint(routeDistance,0));
            if(!Discovered&&(distance<24||progress>routeDistance-18&&progress<routeDistance+29)){Flag("flood_street_discovered");S.Notify("Flooded street ahead","Inspect conditions from dry footing. Measurements do not certify a crossing.");}
            UpdateProbe();
            // Record actual water contact, not inferred intent from a route projection.
            if(!Complete&&distance<38&&S.player.WaterDepthAtFeet>.08f&&!Has("flood_street_direct_exposure"))
            {Flag("flood_street_direct_exposure");S.Notify("Water contact detected","Return to dry footing. Measurements cannot establish safe passage.");S.RequestCheckpoint();}
            if(Discovered&&!Complete&&progress>routeDistance+29)CompleteCrossing(progress);
            RestorePresentation();
        }

        void UpdateProbe()
        {
            if(Keyboard.current!=null&&SibucaoControls.Button(Key.Q).wasPressedThisFrame){workingProbe=-1;holdProgress=0;return;}
            if(workingProbe<0||workingProbe>=2||Sampled(workingProbe)){workingProbe=-1;holdProgress=0;return;}
            var focused=Focused();bool aimed=focused!=null&&focused.kind==TargetKind.Probe&&focused.index==workingProbe;
            bool held=Keyboard.current!=null&&SibucaoControls.Button(Key.E).isPressed||SibucaoControls.WorkHeld;
#if UNITY_EDITOR
            if(SibucaoEditorInput.Active)held=SibucaoEditorInput.UseHeld;
#endif
            if(!aimed||Flat(S.player.view.transform.position,probes[workingProbe].position)>Reach){workingProbe=-1;holdProgress=0;return;}
            if(!held){workingProbe=-1;holdProgress=0;return;}
            if(S.player.WaterDepthAtFeet>.08f){S.Notify("Return to dry footing","Observe from the edge. Do not enter the water to take a reading.");workingProbe=-1;holdProgress=0;return;}
            holdProgress=Mathf.Clamp01(holdProgress+Time.deltaTime/ProbeDuration);
            if(pole)pole.localRotation=Quaternion.Slerp(pole.localRotation,Quaternion.Euler(35+holdProgress*12,0,-18),Time.deltaTime*5);
            var hands=S.player.hands?S.player.hands.GetComponent<SibucaoHandPresentation>():null;
            hands?.DriveIncident(probes[workingProbe],0,holdProgress);
            if(holdProgress>=1)FinishProbe(workingProbe);
        }

        void FinishProbe(int index)
        {
            float depth=WaterDepth(probes[index].position),flow=Flood?Flood.GetFlowVelocity(probes[index].position).magnitude:0;
            S.GetComponent<SibucaoEvacuationExperience>()?.Measure(index,depth,flow);
            Flag("flood_street_probe_"+index);Flag(depth<.25f?"flood_street_sample_shallow":"flood_street_sample_deep");
            if(flow>.45f)Flag("flood_street_sample_fast_flow");
            S.state.scores[0]+=8;S.state.scores[3]+=5;workingProbe=-1;holdProgress=0;
            string reading=depth<.22f?"The pole shows ankle-deep water.":depth<.45f?"The pole shows lower-calf water.":"The pole shows water above the lower marks.";
            S.Say(reading+" A depth reading alone does not establish safe passage.",6);S.Notify("Water point "+(index+1),$"Depth {depth:0.00} m · Current {flow:0.00} m/s. "+(SampleCount>=2?"Compare the routes or ask for assistance.":"Compare with the second point from dry footing."));SibucaoSoundFeedback.Play(SibucaoSoundKind.Mechanism);S.RequestCheckpoint();
        }

        void CompleteCrossing(float progress)
        {
            Flag("flood_street_complete");
            string outcome=Has("flood_street_direct_exposure")?"water-contact-observed":"continued-past-junction";
            S.physicalDecisions?.RecordRouteDecision("flooded-street",outcome,"resolved",HasPrepared("radio"),true,false);
            S.Notify("Junction behind you","Continue to school reception. Report any uncertain access.");
            S.RequestCheckpoint();
        }

        void RestorePresentation()
        {
            if(!ready)return;
            if(pole)
            {
                pole.gameObject.SetActive(!Complete);
                if(PoleTaken&&pole.parent!=S.player.view.transform){pole.SetParent(S.player.view.transform,true);pole.localPosition=new Vector3(.34f,-.58f,.72f);pole.localRotation=Quaternion.Euler(8,0,-18);pole.localScale=Vector3.one*.55f;}
                foreach(var c in pole.GetComponentsInChildren<Collider>())c.enabled=!PoleTaken;
            }
            for(int i=0;i<2;i++)if(probes[i])
            {
                var renderer=probes[i].GetComponentInChildren<Renderer>();if(renderer){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);if(Sampled(i)){block.SetColor("_BaseColor",new Color(.22f,.9f,.66f));block.SetColor("_Color",new Color(.22f,.9f,.66f));renderer.SetPropertyBlock(block);}}
            }
        }

        Target Focused()
        {
            if(!ready||!S||!S.player||!S.player.view||Complete)return null;
            var view=S.player.view.transform;var hits=Physics.RaycastAll(view.position,view.forward,Reach,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.transform.IsChildOf(S.player.transform))continue;if(targets.TryGetValue(hit.collider,out var target)&&Available(target))return target;if(!hit.collider.isTrigger)break;}return null;
        }
        bool Available(Target target)
        {
            if(target.kind==TargetKind.Advisory)return !AdvisoryRead||!choiceRecorded;
            if(target.kind==TargetKind.Pole)return AdvisoryRead&&!PoleTaken;
            return PoleTaken&&!Sampled(target.index);
        }
        void Register(Transform root,TargetKind kind,int index,Transform visual,Vector3 size,Vector3 localCenter)
        {var zone=new GameObject("Interaction - "+kind+" "+index);zone.transform.SetParent(root,false);zone.transform.localPosition=localCenter;var collider=zone.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=size;var target=new Target{kind=kind,index=index,visual=visual};targets[collider]=target;foreach(var visible in visual.GetComponentsInChildren<Collider>(true))targets[visible]=target;}
        Transform WorldGroup(string name,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(runtimeRoot,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;}
        Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion? rotation=null)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=scale;var renderer=go.GetComponent<Renderer>();if(renderer){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}return go.transform;}
        void WorldText(Transform parent,string text,Vector3 position,Quaternion rotation,float size,Color color)
        {var go=new GameObject("Route label - "+text.Replace("\n"," "));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.fontSize=72;mesh.characterSize=size*.22f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;}
        Vector3 RoutePoint(float distance,float lane){var forward=RouteForward(distance);return S.route.Sample(Mathf.Clamp(distance,0,S.route.length))+Vector3.Cross(Vector3.up,forward)*lane;}
        Vector3 RouteForward(float distance){var forward=S.route.Sample(Mathf.Min(S.route.length,distance+2))-S.route.Sample(Mathf.Max(0,distance-2));forward.y=0;return forward.sqrMagnitude>.001f?forward.normalized:Vector3.forward;}
        Quaternion RouteRotation(float distance)=>Quaternion.LookRotation(RouteForward(distance));
        float WaterDepth(Vector3 point){if(!Flood)return 0;if(Flood.TrySurface(point,out float surface,out float ground))return Mathf.Max(0,surface-ground);return 0;}
        bool Sampled(int index)=>Has("flood_street_probe_"+index);
        bool Has(string flag)=>Data!=null&&Data.flags!=null&&Data.flags.Contains(flag);
        void Flag(string flag){if(S.physicalDecisions)S.physicalDecisions.SetFlag(flag);else if(Data!=null&&!Data.flags.Contains(flag))Data.flags.Add(flag);}
        PhysicalGrabbable PackedPrepared(string id){if(!S.inventory)return null;for(int i=0;i<SibucaoInventory.Capacity;i++){var item=S.inventory.Slot(i);var prep=item?item.GetComponent<PreparednessItem>():null;if(prep&&prep.id==id)return item;}return null;}
        bool HasPrepared(string id){var experience=S.GetComponent<SibucaoEvacuationExperience>();if(experience)return experience.HasGear(id);var item=PackedPrepared(id);var prep=item?item.GetComponent<PreparednessItem>():null;return prep&&prep.prepared;}
        static float Flat(Vector3 a,Vector3 b){var d=a-b;return new Vector2(d.x,d.z).magnitude;}
#if UNITY_EDITOR
        public void EditorCompleteAssessment(){Flag("flood_street_discovered");Flag("flood_street_advisory_read");Flag("flood_street_pole_taken");Flag("flood_street_probe_0");Flag("flood_street_probe_1");RestorePresentation();}
#endif
    }
}
