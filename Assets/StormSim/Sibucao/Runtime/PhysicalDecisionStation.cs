using System;
using UnityEngine;
namespace StormSim.Sibucao
{
    [Serializable] public sealed class PhysicalAttempt
    {
        public int[] routeSteps=new int[3];
        public Vector3[] routeLast=new Vector3[3];
        public int probeSamples,drops;
        public Vector3 firstProbe,secondProbe,assistStart;
        public float firstProbeDepth,secondProbeDepth;
        public bool talked,assistGripped,rampPassed,treated,enteredWater;
        public float walked,slowDistance,sprintDistance,assistDistance;
    }
    public sealed partial class PhysicalDecisionStation : MonoBehaviour
    {
        public DecisionTreeManager manager;
        [Range(0,127)] public int index;
        public bool supplementary;
        [Tooltip("Storm damage at a starting home's exit; include before the route's first waypoint.")]
        public bool departureEncounter;
        public int homeMask=15;
        public SibucaoResolutionBeat resolutionBeat;
        public string title;
        public float routeDistance,timeout=120;
        public WaterTier minimumTier=WaterTier.Tier1,maximumTier=WaterTier.Tier5;
        public Transform conversationActor;
        public string conversationLine;
        public AudioClip conversationVoice;
        public AudioSource ambient;
        public GameObject routeMark,spill;
        public PhysicalGrabbable assist;
        [NonSerialized] public PhysicalGrabbable brokenBag;
        public bool probed,bodyEnteredWater;
        Vector3 previousPosition;
        float gazeSeconds;
        public PhysicalAttempt Attempt
        {
            get {if(manager.Data.attempts==null||manager.Data.attempts.Length<=index)manager.EnsureNodeCapacity();return manager.Data.attempts[index]??(manager.Data.attempts[index]=new PhysicalAttempt());}
        }
        public bool Arrived=>manager.Data.flags.Contains("arrived_"+index);
        public bool Observation=>index==0||index==8;
        public float ObservationProgress=>0; // Retained only for old serialized/editor callers; never a timed objective.
        public FloodEncounterAdapter FloodAdapter=>GetComponent<FloodEncounterAdapter>();
        public Vector3 InteractionPosition=>FloodAdapter&&FloodAdapter.Adapted?FloodAdapter.DeckCenter:transform.position;
        public string FloodObjective=>FloodAdapter?FloodAdapter.Objective:node?node.prompt:title;
        public Vector3 NavigationTarget
        {
            get {var branch=EngagedBranch;var s=SibucaoSession.Instance;
                if(branch)
                {
                    if(branch.HasEncounter&&!branch.EncounterComplete)return branch.Encounter.TargetPosition;
                    if(branch.mechanism==PhysicalMechanism.RepairStrap)return branch.WorkTarget;
                    if(branch.NextPoint)return branch.NextPoint.position;
                }
                return FloodAdapter&&FloodAdapter.Adapted?FloodAdapter.Entry:transform.position;}
        }
        public string ActionHint=>"WASD move / Hold left mouse: grip / R + mouse: turn tool / Q: cancel action";
        public string InteractionPrompt=>node?BranchPrompt:Active&&!Attempt.talked&&LookingAtResident()?"E  Talk":null;
        // Physics triggers can arrive while the menu still has its legacy nine
        // default slots. Uninitialized supplementary encounters are inactive.
        public bool Resolved=>manager&&SibucaoSession.Instance&&index>=0&&index<manager.Data.results.Length&&manager.Data.results[index]>=0;
        public bool Active=>manager&&SibucaoSession.Instance&&index>=0&&index<manager.Data.started.Length&&manager.Data.started[index]>=0&&!Resolved;
        public bool TierEligible
        {
            get {if(FloodAdapter&&FloodAdapter.Adapted)return true;var f=SibucaoSession.Instance.GetComponent<FloodSimulationManager>();var t=f?f.GetWaterTier(transform.position):WaterTier.Tier1;return t>=minimumTier&&t<=maximumTier;}
        }
        public void CheckArrival()
        {
            var s=SibucaoSession.Instance;if(!Active||Arrived)return;var d=s.player.transform.position-InteractionPosition;
            if(new Vector2(d.x,d.z).magnitude>6||Mathf.Abs(d.y)>4)return;
            manager.SetFlag("arrived_"+index);previousPosition=s.player.transform.position;
            if(!node)s.Notify("Nearby situation",title);
        }
        bool LookingAtResident()
        {
            var s=SibucaoSession.Instance;if(!conversationActor)return false;
            var point=conversationActor.position+Vector3.up*1.3f;var delta=point-s.player.view.transform.position;
            if(delta.magnitude>3.5f||Vector3.Dot(s.player.view.transform.forward,delta.normalized)<.92f)return false;
            var hits=Physics.RaycastAll(s.player.view.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.transform.IsChildOf(s.player.transform))continue;return hit.transform.IsChildOf(conversationActor)||conversationActor.IsChildOf(hit.transform);}
            return true;
        }
        void Talk()
        {
            if(Attempt.talked)return;Attempt.talked=true;
            var s=SibucaoSession.Instance;s.Say(conversationLine,9);
            if(conversationVoice)AudioSource.PlayClipAtPoint(conversationVoice,conversationActor.position+Vector3.up*1.4f);
            var actor=conversationActor.GetComponent<SibucaoActor>();if(actor)actor.React();
            manager.SetFlag("conversation_"+index);
            // Information is only a sub-step. Nothing resolves until the player traverses a route.
        }
        public bool TryInteract(){if(node){if(branches!=null)foreach(var b in branches)if(b&&b.Interact())return true;return false;}if(!Active||Attempt.talked||!LookingAtResident())return false;Talk();return true;}
        public void ResetStation()
        {
            ResetAutomaticEntry();
            probed=bodyEnteredWater=false;gazeSeconds=0;previousPosition=SibucaoSession.Instance.player.transform.position;
            if(routeMark)routeMark.SetActive(false);if(spill)spill.SetActive(false);if(ambient)ambient.Stop();
        }
        public void Activate(bool deliberate=false)
        {
            var s=SibucaoSession.Instance;
            if(Resolved||Active||!manager.CanEnter(this)||!TierEligible||s.Mode!=SibucaoMode.Playing||s.home&&s.home.MovementLocked)return;
            var routes=manager.GetComponent<SibucaoRouteChoices>();
            if(routes&&!routes.AllowsCenter(this,deliberate))return;
            // Rain and the visible street are already an encounter while dry;
            // the depth-probe choice separately requires actual water.
            if(!supplementary){manager.EnterCausalStage(index);manager.ApplyPending(index);}manager.Data.started[index]=s.state.elapsed;previousPosition=s.player.transform.position;
            if(spill)spill.SetActive(true);
            if(index==4&&!manager.SpillBag(this)){if(fallbackBag){fallbackBag.gameObject.SetActive(true);brokenBag=fallbackBag;manager.SetFlag("found_damaged_bag");}else if(!node){if(spill)spill.SetActive(false);manager.SetFlag("no_bag_to_break");}}
            if(FloodAdapter){if(FloodAdapter.State!=null)FloodAdapter.State.parentPath=manager.Data.causal?.path??"";FloodAdapter.AfterActivation();}
            if(ambient&&!ambient.isPlaying)ambient.Play();
        }
        public void Resolve(int outcome){if(!Resolved)manager.Resolve(this,outcome);}
        public void EnterRoute(int route,int step,Vector3 position)
        {
            if(!Active||route<0||route>2||step<0||step>2)return;
            var a=Attempt;if(a.routeSteps[route]!=step)return;
            if(step>0&&Vector3.Distance(position,a.routeLast[route])<.65f)return;
            a.routeLast[route]=position;a.routeSteps[route]++;
            if(step!=2)return;
            int result=route;
            if(Observation)result=a.probeSamples>=2||a.slowDistance>=2?0:a.sprintDistance>a.walked*.5f?2:1;
            if((index==2||index==6)&&a.talked)result=0;
            Resolve(result);
        }
        public void Probe(PhysicalGrabbable item)
        {
            if(!Active||(!node&&(bodyEnteredWater||Attempt.enteredWater))||!item.IsHeld||item.kind!=PhysicalItemKind.Stick||!item.tip)return;
            if(index==0&&node)return; // B1 records sampled water depths through its physical probe.
            var a=Attempt;var point=item.tip.position;
            if(a.probeSamples==0){a.firstProbe=point;a.probeSamples=1;}
            else if(a.probeSamples==1&&Vector2.Distance(new Vector2(point.x,point.z),new Vector2(a.firstProbe.x,a.firstProbe.z))>=.65f)
            {a.probeSamples=2;probed=true;manager.SetFlag("depth_probed_"+index);}
        }
        public void BeginGrip(PhysicalGrabbable item)
        {
            if(!Active)return;
            if(node&&branches!=null)foreach(var b in branches)if(b)b.Grip(item);
            if(item==assist){Attempt.assistGripped=true;Attempt.assistStart=item.transform.position;}
        }
        public bool ReleaseAtTarget(PhysicalGrabbable item)
        {
            if(!Active)return false;
            if(node&&branches!=null){foreach(var b in branches)if(b&&b.Release(item))return true;return false;}
            var a=Attempt;
            foreach(var sensor in GetComponentsInChildren<PhysicalDecisionSensor>())
            {
                if(sensor.kind==PhysicalSensorKind.StrapSocket&&item.kind==PhysicalItemKind.Strap&&item.station==this)
                {
                    Vector3 point=item.tip?item.tip.position:item.transform.position;
                    if(item.gripTravel<.18f||!sensor.ContainsPoint(point)||Vector3.Dot(item.transform.up,sensor.transform.up)<.96f)continue;
                    item.secured=true;item.Body.isKinematic=true;
                    var offset=point-item.transform.position;item.transform.position=sensor.transform.position-offset;
                    item.transform.SetParent(brokenBag?brokenBag.transform:sensor.transform,true);manager.SetFlag("strap_repaired");Resolve(0);return true;
                }
                if(sensor.kind==PhysicalSensorKind.AssistDestination&&item==assist&&a.assistDistance>.8f&&sensor.ContainsBody(item))
                {item.delivered=true;item.Body.isKinematic=true;Resolve(0);return true;}
                if(sensor.kind==PhysicalSensorKind.MedicalContact&&index==7&&item.kind==PhysicalItemKind.FirstAid&&item.gripTravel>.2f&&sensor.ContainsPoint(item.transform.position))
                {item.delivered=true;item.Body.isKinematic=true;item.gameObject.SetActive(false);a.treated=true;manager.SetFlag("firstaid_used");manager.SetFlag("evacuee_treated");return true;}
            }
            a.drops++;manager.SetFlag("partial_attempt_"+index);return false;
        }
        public int AbandonedOutcome()=>assist&&Attempt.assistGripped&&Attempt.assistDistance>.5f?1:index==4&&manager.Data.flags.Contains("spill_recovered")?1:3;
        void Update()
        {
            var s=SibucaoSession.Instance;if(!s||!manager||s.Mode!=SibucaoMode.Playing)return;
            if(node){TickBranches(Time.deltaTime);if(Active)CheckArrival();return;}
            if(!Active)return;
            CheckArrival();var delta=s.player.transform.position-previousPosition;delta.y=0;previousPosition=s.player.transform.position;
            float d=delta.magnitude,dt=Time.deltaTime;
            if(d<1&&d>.0001f){Attempt.walked+=d;float speed=d/Mathf.Max(.001f,dt);if(speed<1.6f)Attempt.slowDistance+=d;if(speed>s.player.walkSpeed*1.12f)Attempt.sprintDistance+=d;}
            if(assist&&assist.IsHeld)Attempt.assistDistance=Mathf.Max(Attempt.assistDistance,Vector3.Distance(assist.transform.position,Attempt.assistStart));
            if(brokenBag&&spill)foreach(var sensor in spill.GetComponentsInChildren<PhysicalDecisionSensor>())if(sensor.kind==PhysicalSensorKind.StrapSocket)sensor.transform.SetPositionAndRotation(brokenBag.transform.position+brokenBag.transform.up*.3f,brokenBag.transform.rotation);
            bool reading=s.inventory&&s.inventory.IsOpen||s.missionGuide&&s.missionGuide.IsOpen;
            gazeSeconds=!reading&&!Attempt.talked&&LookingAtResident()?gazeSeconds+dt:0;if(gazeSeconds>1.5f)Talk();
            // No animation or observation timeout awards completion. Leaving records physical partial/abandonment.
        }
    }
}
