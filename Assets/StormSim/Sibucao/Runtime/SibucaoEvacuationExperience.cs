using System;
using System.Collections.Generic;
using System.Text;
using StormSim.Preparedness;
using UnityEngine;

namespace StormSim.Sibucao
{
    [Serializable] public sealed class EvacuationMoment
    {
        public string id,action,result,lesson;
        public float time;
    }

    [Serializable] public sealed class EvacuationExperienceState
    {
        public List<EvacuationMoment> moments=new List<EvacuationMoment>();
        public float departedAt=-1,drainOpenedAt=-1,restSeconds;
        public int weatherStage=-1;
        public float familyDistance=-1;
        public Vector3 familyCartPosition,familyParentPosition,familyChildPosition;
        public bool familyPositionSaved;
        public List<Vector3> familyTrail=new List<Vector3>(),escortTrail=new List<Vector3>();
        public bool assistanceRequested;
        public float assistanceWait;
        public Vector3 assistanceOrigin;
        public Vector3 escortPosition,responderPosition,dynamicAnchor,dynamicForward;
        public bool escortPositionSaved,responderPositionSaved,dynamicAnchorSaved;
        public string assessmentSnapshot;
        public float[] measuredDepths={-1,-1};
        public float[] measuredFlows={-1,-1};
    }

    /// <summary>Connects preparation, authored incidents and the final review using saved evidence.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class SibucaoEvacuationExperience : MonoBehaviour
    {
        public const int SpecialistLimit=3;
        public static bool IsSpecialist(string id)=>id=="radio"||id=="flashlight"||id=="gloves"||id=="first-aid"||id=="whistle";
        SibucaoSession S=>GetComponent<SibucaoSession>();
        EvacuationExperienceState tracked;
        Transform runtimeRoot;
        readonly List<Transform> shelters=new List<Transform>();
        readonly List<Material> materials=new List<Material>();
        Vector3 lastPosition;
        float stillSeconds,nextNotice;
        int focusedRest=-1;
        public bool AtRestPoint {get;private set;}
        public float ShelterFactor=>AtRestPoint?.32f:1f;
        public EvacuationExperienceState State
        {
            get
            {
                if(S.state.experience==null)S.state.experience=new EvacuationExperienceState();
                if(S.state.experience.moments==null)S.state.experience.moments=new List<EvacuationMoment>();
                return S.state.experience;
            }
        }
        public bool Has(string id)=>S.state.physical.flags.Contains(id);
        public bool HasGear(string id)
        {
            if(!S.inventory)return false;
            var equipped=S.inventory.Find(S.state.physical.equippedInventoryId);var equippedPrep=equipped?equipped.GetComponent<PreparednessItem>():null;
            if(equippedPrep&&equippedPrep.id==id&&equippedPrep.prepared&&!equipped.delivered&&!Has("lost:"+equipped.persistentId)&&!Has("consumed:"+equipped.persistentId))return true;
            for(int i=0;i<SibucaoInventory.Capacity;i++)
            {
                var item=S.inventory.Slot(i);var prep=item?item.GetComponent<PreparednessItem>():null;
                if(prep&&prep.id==id&&prep.prepared&&!item.delivered&&!Has("lost:"+item.persistentId)&&!Has("consumed:"+item.persistentId))return true;
            }
            return false;
        }
        public string Prompt=>focusedRest<0?null:Has("experience_rest_"+focusedRest)?"Sheltered rest point · Check your bag or plan your next move":"E  Check the route board and rest under cover";
        public string CurrentObjective=>"Reach Banago Elementary School I reception."+ (LocalObjective=="Reach school reception."?"":" "+LocalObjective);
        string LocalObjective
        {
            get
            {
                var f=GetComponent<SibucaoFloodedStreetIncident>();var r=GetComponent<SibucaoRouteIncidentSequence>();
                var surprise=GetComponent<SibucaoDynamicIncidentDirector>();if(surprise&&surprise.HasActiveIncident)return surprise.CurrentObjective;
                if(S.missionGuide&&S.missionGuide.InsideHome)return "Take your go-bag and leave by the front door.";
                if(f&&!f.Complete)return f.Active?"Inspect the flooded junction.":"Follow the street toward the junction.";
                if(r&&!r.DrainComplete)return r.DrainActive?"Find a way past the blocked drain.":"Continue toward the drainage passage.";
                if(r&&!r.RescueComplete)return r.RescueActive?"Assess the injured evacuee.":"Continue toward the school approach.";
                return "Reach school reception.";
            }
        }
        public void Record(string id,string action,string result,string lesson,bool announce=true)
        {
            if(State.moments.Exists(m=>m.id==id))return;
            State.moments.Add(new EvacuationMoment{id=id,action=action,result=result,lesson=lesson,time=S.state.homeSeconds});
            if(announce){S.Notify(action,result);SibucaoSoundFeedback.Play(SibucaoSoundKind.Placement);}
            S.RequestCheckpoint();
        }
        public void Measure(int index,float depth,float flow)
        {
            if(State.measuredDepths==null||State.measuredDepths.Length!=2)State.measuredDepths=new[]{-1f,-1f};
            if(State.measuredFlows==null||State.measuredFlows.Length!=2)State.measuredFlows=new[]{-1f,-1f};
            State.measuredDepths[index]=depth;State.measuredFlows[index]=flow;
            Record("measurement-"+index,"Observed water point "+(index+1),$"Depth {depth:0.00} m; current {flow:0.00} m/s in this scenario.","A depth reading alone does not establish that floodwater is safe.",false);
        }
        public bool Interact()
        {
            if(focusedRest<0||Has("experience_rest_"+focusedRest))return false;
            S.physicalDecisions.SetFlag("experience_rest_"+focusedRest);
            Record("rest-"+focusedRest,"Checked the sheltered route board",HasGear("radio")?"Your radio confirms the school route. Catch your breath here; the storm continues outside.":"The board points toward school reception. Catch your breath and check your supplies.","A short pause on dry, sheltered footing helps you plan without abandoning evacuation.");
            return true;
        }
        void Update()
        {
            if(!S||!S.route||!S.player||S.state==null)return;
            if(tracked!=State){tracked=State;BuildShelters();AtRestPoint=false;focusedRest=-1;stillSeconds=0;lastPosition=S.player.transform.position;}
            if(S.Mode!=SibucaoMode.Playing){AtRestPoint=false;focusedRest=-1;if(S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete)ObserveConsequences();return;}
            if(S.home&&(S.home.MovementLocked||S.missionGuide&&S.missionGuide.InsideHome))return;
            if(State.departedAt<0)
            {
                State.departedAt=S.state.homeSeconds;
                var gear=new List<string>();foreach(var id in new[]{"radio","flashlight","gloves","first-aid","whistle"})if(HasGear(id))gear.Add(id);
                Record("departure","Left home",gear.Count>0?"Prepared equipment: "+string.Join(", ",gear)+".":"No specialist equipment packed. Community assistance remains available.","Your equipment changes the approaches available on the route.",false);
            }
            UpdateShelter();ObserveConsequences();UpdateStorm();
            var assessment=GetComponent<SibucaoFloodedStreetIncident>();
            if(assessment&&assessment.Discovered&&!assessment.Complete&&string.IsNullOrEmpty(State.assessmentSnapshot))S.CaptureAssessmentReplay();
        }
        void UpdateStorm()
        {
            float elapsed=S.state.homeSeconds-State.departedAt;
            float progress=Mathf.InverseLerp(S.state.physical.routeStart,S.route.length,S.route.Progress(S.player.transform.position));
            int stage=Mathf.Max(elapsed>600?3:elapsed>360?2:elapsed>150?1:0,progress>.78f?3:progress>.5f?2:progress>.2f?1:0);
            if(stage<=State.weatherStage||Time.unscaledTime<nextNotice)return;
            State.weatherStage=stage;nextNotice=Time.unscaledTime+12;
            string[] reports={"Rain is strengthening. Look for local warnings before each passage.","Wind is rising. Watch for loose sheets and branches.","Runoff is building along low roads. Recheck the route before continuing.","Visibility is worsening near the school approach. Stay together and follow reception signs."};
            Record("weather-"+stage,HasGear("radio")?"Radio route update":"Storm conditions changing",reports[stage],"Conditions change during evacuation; reassess earlier plans.",stage>0);
            if(stage>0&&HasGear("radio"))S.Say("Radio: "+reports[stage],7);
        }
        void ObserveConsequences()
        {
            if(Has("flood_street_complete"))Record("crossing","Passed the flooded junction",Has("flood_street_direct_exposure")?"Water contact was recorded near the junction.":Has("flood_street_assistance")?"You marked the junction for reception reporting.":"You continued beyond the junction. No water contact was recorded by this encounter.","Measurements do not establish safety; retreat or request assistance if access is uncertain.");
            if(Has("route_drain_community_benefit"))
            {
                if(State.drainOpenedAt<0)State.drainOpenedAt=S.state.homeSeconds;
                Record("drain-open","Removed supervised loose material","A modest local runoff reduction develops over time; surrounding flooding is unaffected.","A local blockage can affect nearby households; removing loose debris with assistance changes conditions.");
                if(S.state.homeSeconds-State.drainOpenedAt>30&&Vector3.Distance(S.player.transform.position,DrainCenter)<10)
                    Record("drain-later","Returned near the cleared drain","The model applies a modest local runoff reduction over time.","Clearing loose material is not a remedy for widespread flooding.",false);
            }
            else if(Has("route_drain_complete"))Record("drain-pass","Continued past the blockage",Has("route_drain_reported")?"The volunteer acknowledged your report. Clearing remains unconfirmed.":"The blockage remains unresolved; report it at reception.","Continuing to shelter and requesting trained assistance are valid decisions.");
            if(Has("side_family_complete"))Record("family-help","Helped the roadside family","The supplies task was completed. Family arrival is tracked separately.","Cooperation can provide continuing support during evacuation.");
            if(Has("route_rescue_complete"))Record("rescue","Responded to the injured evacuee",Has("route_rescue_responder_arrived")?"A trained responder reached the evacuee. Calling for help was a successful response.":Has("route_rescue_escorted")?Has("evacuee_assisted_transport")?"The evacuee checked in after abstracted assisted transport.":"The evacuee walked with you to school reception.":"The evacuee's safe arrival is unconfirmed; report their location at reception.","Choose assistance that matches your equipment, condition and available support.");
        }
        public float DrainDistance=>Mathf.Min(S.route.length-360,Mathf.Max(185,S.state.physical.routeStart+225));
        public Vector3 DrainCenter=>S.route.Sample(Mathf.Clamp(DrainDistance+9,0,S.route.length));
        public float DrainStrength=>State.drainOpenedAt<0||!Has("route_drain_community_benefit")?0:.08f*Mathf.Clamp01((S.state.homeSeconds-State.drainOpenedAt)/45);
        public static float ReliefAt(Vector3 world,Vector3 center,float strength)
        {
            float distance=new Vector2(world.x-center.x,world.z-center.z).magnitude;
            return Mathf.Max(0,strength)*Mathf.Clamp01(1-distance/24);
        }
        public float DrainRelief(Vector3 world)
        {
            return ReliefAt(world,DrainCenter,DrainStrength);
        }
        void UpdateShelter()
        {
            var p=S.player.transform.position;focusedRest=-1;AtRestPoint=false;
            foreach(var shelter in shelters)
            {
                var local=shelter.InverseTransformPoint(p);
                if(Mathf.Abs(local.x)<1.45f&&Mathf.Abs(local.z)<1.3f&&Mathf.Abs(local.y)<1.4f&&S.player.WaterDepthAtFeet<.06f)
                {
                    AtRestPoint=true;
                    if(Vector3.Dot(S.player.view.transform.forward,(shelter.position+Vector3.up*1.2f-S.player.view.transform.position).normalized)>.25f)focusedRest=shelters.IndexOf(shelter);
                    break;
                }
            }
            float delta=(p-lastPosition).sqrMagnitude;lastPosition=p;
            stillSeconds=AtRestPoint&&delta<.0004f?stillSeconds+Time.deltaTime:0;
            if(stillSeconds>2)
            {
                S.state.physical.stamina=Mathf.Min(100,S.state.physical.stamina+Time.deltaTime*2);
                State.restSeconds+=Time.deltaTime;
            }
        }
        void BuildShelters()
        {
            if(runtimeRoot)Destroy(runtimeRoot.gameObject);
            foreach(var material in materials)if(material)Destroy(material);materials.Clear();shelters.Clear();
            runtimeRoot=new GameObject("Evacuation sheltered planning points").transform;runtimeRoot.SetParent(transform,false);
            float rescue=Mathf.Min(S.route.length-70,Mathf.Max(DrainDistance+230,S.state.physical.routeStart+585));
            foreach(float d in new[]{S.state.physical.routeStart+115,rescue-65})
            {
                float at=Mathf.Clamp(d,0,S.route.length);var forward=S.route.Sample(Mathf.Min(S.route.length,at+2))-S.route.Sample(Mathf.Max(0,at-2));forward.y=0;
                if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
                var pos=S.route.Sample(at)+Vector3.Cross(Vector3.up,forward)*3.1f;
                bool found=false;
                foreach(float lane in new[]{3.1f,-3.1f,4.8f,-4.8f,6.5f,-6.5f})
                {
                    var candidate=S.route.Sample(at)+Vector3.Cross(Vector3.up,forward)*lane;
                    if(!Physics.Raycast(candidate+Vector3.up*1.2f,Vector3.down,out var ground,2.4f,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<.85f)continue;
                    candidate.y=ground.point.y;
                    if(!ClearPlacement(candidate))continue;
                    pos=candidate;found=true;break;
                }
                if(!found)continue;
                var root=new GameObject("Sheltered route board "+shelters.Count).transform;root.SetParent(runtimeRoot,false);root.SetPositionAndRotation(pos,Quaternion.LookRotation(forward));shelters.Add(root);
                var roof=Part(root,"Concrete planning pavilion roof",new Vector3(0,2.65f,0),new Vector3(3.3f,.22f,3.1f),new Color(.38f,.4f,.39f));
                var roofSupport=roof.AddComponent<SibucaoStormSupport>();roofSupport.canBrace=false;roofSupport.blocksWind=false;roofSupport.sheltersRain=true;
                foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-1.3f,1.3f})
                {
                    var column=Part(root,"Fixed concrete column",new Vector3(x,1.3f,z),new Vector3(.28f,2.6f,.28f),new Color(.43f,.45f,.43f));
                    var support=column.AddComponent<SibucaoStormSupport>();support.blocksWind=false;
                }
                Part(root,"Route board",new Vector3(0,1.3f,1.25f),new Vector3(1.75f,.9f,.08f),new Color(.08f,.25f,.24f));
                var text=new GameObject("School direction").AddComponent<TextMesh>();text.transform.SetParent(root,false);text.transform.localPosition=new Vector3(0,1.35f,1.19f);text.text="SCHOOL RECEPTION\nCHECK ROUTE · NO ALL-CLEAR";text.fontSize=50;text.characterSize=.009f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            }
        }
        bool ClearPlacement(Vector3 p)
        {
            // Avoid placing a shelter inside an existing building or obstructing the street.
            foreach(var c in Physics.OverlapBox(p+Vector3.up*1.4f,new Vector3(1.6f,.8f,1.5f),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(c&&!c.transform.IsChildOf(S.player.transform)&&c.bounds.size.y>1.6f)return false;
            return true;
        }
        GameObject Part(Transform root,string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader){var m=new Material(shader);m.color=color;go.GetComponent<Renderer>().sharedMaterial=m;materials.Add(m);}
            return go;
        }
        public void RecordReceptionReports()
        {
            var reports=new List<string>();
            if(Has("flood_street_discovered"))reports.Add("junction access requires reassessment");
            if(Has("route_drain_discovered")&&!Has("route_drain_community_benefit"))reports.Add("drain blockage remains unresolved");
            if(Has("route_rescue_discovered")&&!Has("route_rescue_responder_arrived")&&!Has("route_rescue_escorted"))reports.Add("injured evacuee location; handoff unconfirmed");
            if(Has("side_family_accepted")&&!Has("experience_family_arrived"))reports.Add("family location; arrival unconfirmed");
            foreach(var flag in S.state.physical.flags)
                if(flag.StartsWith("director_large_")&&flag.EndsWith("_bypassed"))reports.Add("unresolved roadside incident");
            Record("reception-report","Checked in with school reception",reports.Count>0?"Reception recorded: "+string.Join("; ",reports)+".":"Arrival recorded; no unresolved encountered incidents in this report.","A recorded report is not confirmation of a rescue outcome.",false);
            S.physicalDecisions.SetFlag("experience_hazards_reported");
        }
        public string Review()
        {
            var b=new StringBuilder("YOUR EVACUATION\n\n");
            foreach(var m in State.moments)
            {
                int seconds=Mathf.Max(0,Mathf.RoundToInt(m.time-Mathf.Max(0,State.departedAt)));
                b.AppendLine($"{seconds/60:00}:{seconds%60:00}  {m.action}\n{m.result}\nWhat this shows: {m.lesson}\n");
            }
            b.AppendLine(Has("experience_family_arrived")?"The roadside family checked in at the school with you.":Has("side_family_complete")?"The family was assisted on the route; school arrival remains unconfirmed.":"Report any residents whose arrival you could not confirm.");
            b.AppendLine("\nTRY ANOTHER APPROACH\nChoose a different set of specialist equipment, leave earlier, or compare asking for assistance with doing the work yourself.");
            return b.ToString();
        }
        void OnDestroy(){if(runtimeRoot)Destroy(runtimeRoot.gameObject);foreach(var material in materials)if(material)Destroy(material);}
    }
}
