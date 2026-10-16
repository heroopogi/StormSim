using UnityEngine;
namespace StormSim.Sibucao
{
    public enum PhysicalMechanism { Walk, ProbeThenWalk, Squeeze, Mantle, RepairStrap, GatherItems, CarryResident, GuideResident, SignalHelp, TalkThenWalk, BoardBoat, FlashlightPassage, TreatThenCarry, SwimPassage, SprintPassage }
    /// <summary>A world binding for one data choice. Never selects an outcome from a menu.</summary>
    public sealed partial class SibucaoPhysicalOption:MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public int choiceIndex;
        public PhysicalMechanism mechanism;
        public Transform[] path;
        [Tooltip("Optional local debris work positions, independent of the walking path.")]
        public Vector3[] coastalWorkAnchors;
        public PhysicalPathVariant[] layouts;
        public Light flashlight;
        public Transform actionPoint;
        public PhysicalGrabbable tool,subject;
        public Transform[] workSockets;
        public BoxCollider destination;
        public Transform helper,boat;
        public GameObject presentation;
        public GameObject[] layoutPresentation;
        public float arrivalRadius=.48f;
        public string verb="Interact";
        public bool previewAvailable;
        public bool requireContact;
        [Tooltip("Optional existing debris-clearing work which must be performed before this crossing.")]
        public SibucaoStormPassage requiredPassage;
        Vector3 boatStart,helperStart;Quaternion helperRotation;bool placementCached;
        void Awake(){CachePlacement();}
        void CachePlacement(){if(placementCached)return;if(boat)boatStart=boat.localPosition;if(helper){helperStart=helper.localPosition;helperRotation=helper.localRotation;}placementCached=true;}
        SibucaoSession S=>SibucaoSession.Instance;
        public DecisionChoice Choice=>floodChoice?floodChoice:station.node&&choiceIndex<station.node.choices.Length?station.node.choices[choiceIndex]:null;
        public PhysicalBranchAttempt Attempt=>station.manager.Branch(station.index);
        public bool Engaged=>station.Active&&Attempt.selected==choiceIndex&&Attempt.engaged;
        public bool Available=>Choice&&(Engaged||station.manager.ChoiceAvailable(station,choiceIndex));
        public float PathLength
        {
            get {float length=0;if(path!=null)for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1].position,path[i].position);return length;}
        }
        public float SpeedLimit
        {
            get {if(RequiresPassageWork)return float.PositiveInfinity;
                if(PacedTraversal||mechanism==PhysicalMechanism.Walk||mechanism==PhysicalMechanism.TalkThenWalk||mechanism==PhysicalMechanism.FlashlightPassage)return float.PositiveInfinity;
                if(IsFirstCrossing)return float.PositiveInfinity;
                float limit=Choice&&Choice.timeCost>0?Mathf.Clamp(PathLength/(Choice.timeCost*1.5f),.12f,4.1f):float.PositiveInfinity;
                if(Engaged&&subject&&(mechanism==PhysicalMechanism.GuideResident||mechanism==PhysicalMechanism.SignalHelp&&Attempt.helperArrived)&&FlatDistance(S.player.transform.position,subject.transform.position)>2.2f)limit=Mathf.Min(limit,.12f);
                return limit;}
        }
        public Transform NextPoint=>path!=null&&path.Length>0?path[Mathf.Clamp(Attempt.step,0,path.Length-1)]:actionPoint;
        public Vector3 WorkTarget
        {
            get {if(workSockets==null||workSockets.Length==0)return actionPoint?actionPoint.position:transform.position;
                var p=workSockets[Mathf.Clamp(Attempt.toolStep,0,workSockets.Length-1)];
                return p.position+p.right*(Mathf.Sin(Time.time*1.7f)*.035f);}
        }
        public bool NearAction
        {
            get {if(!actionPoint||!S)return false;var d=actionPoint.position-S.player.view.transform.position;return d.magnitude<2.8f&&Vector3.Dot(d.normalized,S.player.view.transform.forward)>.58f;}
        }
        public string Prompt
        {
            get
            {
                if(!station.Active||!Available)return null;
                if(requiredPassage&&!requiredPassage.State.secured)return null; // Its physical handles supply the work prompt.
                if(!Engaged&&!NearAction)return null;
                if(HasEncounter&&Engaged&&!EncounterComplete)return Encounter.Prompt;
                switch(mechanism)
                {
                    case PhysicalMechanism.Squeeze:return "Hold E + move: squeeze through";
                    case PhysicalMechanism.Mantle:return Engaged?Attempt.step>=3&&!Attempt.secondInput?"Release and press Space: shift footing / W: move":"Hold Space + W: climb":"Space: foothold";
                    case PhysicalMechanism.ProbeThenWalk:return station.Attempt.probeSamples<2?"Hold left mouse + aim down: probe two wet spots ("+station.Attempt.probeSamples+"/2) / Wheel: reach":"Walk beside the boards to the far side";
                    case PhysicalMechanism.RepairStrap:return Engaged?"Hold connector at buckle / R + mouse: turn / Release: place":"Hold left mouse: grip connector";
                    case PhysicalMechanism.CarryResident:return "Hold left mouse: grip / Move / Release at landing";
                    case PhysicalMechanism.TreatThenCarry:return Engaged?"Hold left mouse: grip / Move / Release at landing":"Use kit from bag / Grip, place and release";
                    case PhysicalMechanism.GuideResident:return Engaged?"Hold E + move: guide chair / Space: lift over a curb":"E: Take chair handles";
                    case PhysicalMechanism.SignalHelp:return Engaged?Attempt.helperArrived?"Hold E + move: lead the transfer":"Hold E + move: lead neighbour to resident":"E: Call neighbour";
                    case PhysicalMechanism.BoardBoat:return Engaged?"Move along gangway / Hold E on deck: release mooring":"Move onto the boat";
                    case PhysicalMechanism.FlashlightPassage:return Engaged?"Move through the covered passage":"E: Use flashlight";
                    case PhysicalMechanism.SprintPassage:return "Shift + move: cross the passage";
                    case PhysicalMechanism.GatherItems:return null; // The focused item supplies its own E pickup prompt.
                    case PhysicalMechanism.Walk:return FloodAdapted?Choice.label:"Move through the passage";
                    default:return Engaged?"Move along the lane":"E: "+(FloodAdapted?Choice.label:verb);
                }
            }
        }
        public void ResetBinding()
        {
            if(encounterRunner)encounterRunner.Rebuild();
            cancelledUntilMoved=false;crossingOutsideSince=-1;
            var probe=tool?tool.GetComponent<SibucaoDepthProbe>():null;if(probe)probe.ClearReading();
            if(IsFirstCrossing&&choiceIndex==0&&tool){if(!probe)probe=tool.gameObject.AddComponent<SibucaoDepthProbe>();probe.PrepareForRun();}
            CachePlacement();if(boat)boat.localPosition=boatStart;if(helper){helper.localPosition=helperStart;helper.localRotation=helperRotation;}
            if(presentation)presentation.SetActive(true);
        }
        public void SaveBinding()
        {
            if(Attempt.selected!=choiceIndex)return;Attempt.hasPlacement=true;
            if(boat)Attempt.boatLocalPosition=boat.localPosition;
            if(helper){Attempt.helperLocalPosition=helper.localPosition;Attempt.helperRotation=helper.localRotation;}
        }
        public void RestoreBinding()
        {
            if(IsFirstCrossing&&choiceIndex==0&&tool&&!tool.everHeld){var probe=tool.GetComponent<SibucaoDepthProbe>();if(!probe)probe=tool.gameObject.AddComponent<SibucaoDepthProbe>();probe.PrepareForRun();}
            if(Attempt.selected!=choiceIndex||!Attempt.hasPlacement)return;
            if(boat)boat.localPosition=Attempt.boatLocalPosition;
            if(helper){helper.localPosition=Attempt.helperLocalPosition;helper.localRotation=Attempt.helperRotation;}
        }
        public void ApplyLayout(int home)
        {
            if(layoutPresentation!=null)for(int i=0;i<layoutPresentation.Length;i++)if(layoutPresentation[i])layoutPresentation[i].SetActive(i==home);
            if(layouts==null)return;foreach(var layout in layouts)if(layout.home==home&&layout.points!=null)
            {
                if(path!=null)foreach(var point in path)if(point)Destroy(point.gameObject);
                path=new Transform[layout.points.Length];
                for(int i=0;i<path.Length;i++){var go=new GameObject("Physical step "+i);go.transform.SetParent(transform,false);go.transform.localPosition=layout.points[i];path[i]=go.transform;}
                if(destination&&path.Length>0)destination.transform.position=path[path.Length-1].position+Vector3.up*1.5f;
                break;
            }
        }
        public bool Begin(bool deliberate=false)
        {
            if(requiredPassage&&!requiredPassage.State.secured)return false;
            if(deliberate)cancelledUntilMoved=false;
            if(!MayBegin||!deliberate&&!station.AutomaticEntryAllowed)return false;
            var routes=station.manager.GetComponent<SibucaoRouteChoices>();if(routes&&!routes.AllowsCenter(station,deliberate))return false;
            // Coastal streets can place a reachable workpiece before the center-entry corridor.
            // A deliberate grab still passes Activate's stage, tier and mode checks.
            if(deliberate&&!station.Active&&S.GetComponent<CoastalAreaProfile>())station.Activate(true);
            if(!station.Active||!Available)return false;
            var a=Attempt;if(a.selected==choiceIndex&&a.engaged)return true;
            // Holding E on a workpiece near another route must not replace the active choice.
            if(HasEncounter&&station.EngagedBranch&&station.EngagedBranch!=this)return false;
            // Dense junctions can overlap. One physical action owns the controls;
            // Q releases it before another encounter can begin.
            foreach(var other in station.manager.stations)
            {
                if(other==station||!other.EngagedBranch)continue;
                var active=other.EngagedBranch;
                // Passing an incidental walking sensor must not reserve the hands
                // when the player deliberately starts work at a coastal junction.
                if(deliberate&&S.GetComponent<CoastalAreaProfile>()&&other.supplementary&&
                    active.mechanism==PhysicalMechanism.Walk&&!active.HasEncounter&&!active.Attempt.actionStarted)
                {active.CancelInteraction();continue;}
                return false;
            }
            a.selected=choiceIndex;a.engaged=true;a.finished=false;a.step=0;a.toolStep=0;a.toolSeconds=0;a.activeSeconds=0;a.distance=0;a.enteredAt=S.state.homeSeconds;
            if(HasEncounter){a.encounter=new PhysicalEncounterProgress();Encounter.ResetPresentation();}
            if(path!=null&&path.Length>1&&FlatDistance(S.player.transform.position,path[0].position)<arrivalRadius)a.step=1;
            a.actionStarted=a.secondInput=a.helperCalled=a.helperArrived=a.contactConsequence=false;a.previousPosition=S.player.transform.position;
            if(tool)a.previousToolPosition=tool.transform.position;
            CachePlacement();
            if(S.story)S.story.BeginAction(this);
            return true;
        }
        public bool Interact()
        {
            if(TryEncounterInput())return true;
            if(!NearAction||!Available||!station.Active)return false;
            if(mechanism==PhysicalMechanism.TalkThenWalk||mechanism==PhysicalMechanism.GuideResident||mechanism==PhysicalMechanism.SignalHelp||mechanism==PhysicalMechanism.FlashlightPassage)
            {
                if(!Begin(true))return false;Attempt.actionStarted=true;
                if(mechanism==PhysicalMechanism.TalkThenWalk)S.Say(station.conversationLine,6);
                if(mechanism==PhysicalMechanism.SignalHelp)Attempt.helperCalled=true;
                return true;
            }
            return false;
        }
        public void Grip(PhysicalGrabbable item)
        {
            var current=station.EngagedBranch;if(current&&current!=this&&current.mechanism==PhysicalMechanism.TreatThenCarry)return;
            if(mechanism==PhysicalMechanism.TreatThenCarry&&item==subject&&Engaged)return;
            if((mechanism==PhysicalMechanism.RepairStrap||mechanism==PhysicalMechanism.ProbeThenWalk)&&item==tool||mechanism==PhysicalMechanism.CarryResident&&item==subject)Begin(true);
        }
        public bool Release(PhysicalGrabbable item)
        {
            if(mechanism==PhysicalMechanism.TreatThenCarry&&item.kind==PhysicalItemKind.FirstAid&&item.tip==null&&Available&&subject)
            {
                if(Vector3.Distance(item.transform.position,subject.transform.position+Vector3.up*.7f)>1.0f||item.gripTravel<.2f)return false;
                if(!Begin(true))return false;Attempt.actionStarted=true;item.delivered=true;item.preparedForUse=false;item.gameObject.SetActive(false);station.manager.SetFlag("consumed:"+item.persistentId);station.manager.SetFlag("evacuee_treated");return true;
            }
            if(!Engaged)return false;
            if(mechanism==PhysicalMechanism.RepairStrap&&item==tool)
            {
                if(!EncounterComplete||Attempt.toolStep<workSockets.Length||Vector3.Distance(item.tip.position,WorkTarget)>(HasEncounter?.3f:.12f)||Vector3.Dot(item.transform.up,workSockets[workSockets.Length-1].up)<(HasEncounter?.65f:.85f))return false;
                item.secured=true;item.Body.isKinematic=true;item.transform.SetParent(station.brokenBag?station.brokenBag.transform:transform,true);
                station.manager.SetFlag("strap_repaired");return FinishEncounterChoice();
            }
            if((mechanism==PhysicalMechanism.CarryResident||mechanism==PhysicalMechanism.TreatThenCarry)&&item==subject)
            {
                if(Attempt.step<path.Length||Attempt.activeSeconds+.15f<Choice.timeCost||!destination||!ContainsBody(item))return false;
                if(!EncounterComplete)return false;
                item.delivered=true;item.Body.isKinematic=true;return FinishEncounterChoice();
            }
            return false;
        }
        bool ContainsBody(PhysicalGrabbable body)
        {
            foreach(var c in body.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)
            {
                Bounds bounds;bool local=true;
                if(c is BoxCollider box)bounds=new Bounds(box.center,box.size);
                else if(c is CapsuleCollider capsule){var size=Vector3.one*capsule.radius*2;size[capsule.direction]=Mathf.Max(capsule.height,capsule.radius*2);bounds=new Bounds(capsule.center,size);}
                else if(c is SphereCollider sphere)bounds=new Bounds(sphere.center,Vector3.one*sphere.radius*2);
                else if(c is MeshCollider mesh&&mesh.sharedMesh)bounds=mesh.sharedMesh.bounds;
                else{bounds=c.bounds;local=false;}
                for(int n=0;n<8;n++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));if(local)p=c.transform.TransformPoint(p);if(Vector3.Distance(destination.ClosestPoint(p),p)>.08f)return false;}
            }
            return true;
        }
        public bool Drive(CharacterController controller,Vector2 input,bool useHeld,bool jump,bool jumpHeld,float dt)
        {
            if(!station.Active||!Available)return false;
            if(!Engaged)
            {
                if(mechanism==PhysicalMechanism.Squeeze&&NearAction&&useHeld){if(!Begin())return false;SibucaoControls.BeginGrip();}
                else if(mechanism==PhysicalMechanism.Mantle&&NearAction&&jump){if(!Begin(true))return false;Attempt.actionStarted=true;}
                else return false;
            }
            var a=Attempt;
            if(mechanism==PhysicalMechanism.GuideResident&&subject&&useHeld&&jump&&FlatDistance(S.player.transform.position,subject.transform.position)<3)
            {
                // A curb requires another deliberate input. Keep the real body
                // and its collisions; never slide the chair through the lip.
                subject.Body.AddForce(Vector3.up*2.7f,ForceMode.VelocityChange);
                a.secondInput=true;S.player.Gesture(2);
            }
            var flood=S.GetComponent<FloodSimulationManager>();
            if((mechanism==PhysicalMechanism.Squeeze||mechanism==PhysicalMechanism.Mantle)&&flood&&flood.GetWaterDepth(S.player.transform.position)>1.3f){a.engaged=false;return false;}
            if(mechanism==PhysicalMechanism.Squeeze)
            {
                var feedback=S.player.GetComponent<SibucaoPhysicalFeedback>();if(feedback)feedback.Squeeze=1;
                controller.radius=.17f;
                if(input.y<-.1f){controller.Move((-S.player.transform.forward*.6f+Vector3.down*2)*dt);if(!HasEncounter&&FlatDistance(S.player.transform.position,path[0].position)<.6f)a.engaged=false;return true;}
                if(useHeld&&input.sqrMagnitude>.01f){var delta=NextPoint.position-S.player.transform.position;delta.y=0;if(Vector3.Dot(delta.normalized,S.player.view.transform.forward)>.45f)controller.Move((delta.normalized*Mathf.Min(.7f,SpeedLimit)+Vector3.down*2)*dt);}
                else controller.Move(Vector3.down*2*dt);
                return true;
            }
            if(mechanism==PhysicalMechanism.Mantle)
            {
                if(input.y<-.1f){if(!HasEncounter)a.engaged=false;return false;}
                if(a.step>=3&&jump)a.secondInput=true;
                if(jumpHeld&&input.y>.1f&&(a.step<3||a.secondInput))
                {
                    var delta=NextPoint.position-S.player.transform.position;
                    if(delta.sqrMagnitude>.001f)controller.Move(Vector3.ClampMagnitude(delta,dt*.85f));
                }
                else if(!jumpHeld)controller.Move(Vector3.down*3*dt);
                return true;
            }
            return false;
        }
        public void Tick(float dt)
        {
            if(!station.node||!Choice||!S)return;
            if(HasEncounter)Encounter.Tick();
            previewAvailable=Available;if(presentation&&presentation.activeSelf!=previewAvailable)presentation.SetActive(previewAvailable);
            if(flashlight)flashlight.enabled=Engaged&&mechanism==PhysicalMechanism.FlashlightPassage&&S.inventory.HasTag("flashlight");
            if(!station.Active)return;
            // Route selection comes from physical entry and continued movement.
            if(IsFirstCrossing){TickFirstCrossing(dt);return;}
            if(Engaged&&mechanism==PhysicalMechanism.FlashlightPassage&&!S.inventory.HasTag("flashlight")){Attempt.engaged=false;return;}
            var a=Attempt;var player=S.player;
            bool useHeld=player.UseHeld;
            if(!Engaged)
            {
                if(station.EngagedBranch)return;
                if(path==null||path.Length==0)return;
                bool automatic=mechanism==PhysicalMechanism.Walk||mechanism==PhysicalMechanism.GatherItems||mechanism==PhysicalMechanism.BoardBoat||mechanism==PhysicalMechanism.SwimPassage||mechanism==PhysicalMechanism.SprintPassage;
                if(automatic&&Available&&(mechanism!=PhysicalMechanism.SprintPassage||player.Sprinting)&&FlatDistance(player.transform.position,path[0].position)<(FloodAdapted?.52f:.85f)&&player.MoveInput.sqrMagnitude>.01f){if(!Begin())return;}
                else return;
            }
            float moved=Vector3.Distance(player.transform.position,a.previousPosition);a.previousPosition=player.transform.position;
            bool walking=moved<1.2f&&moved>dt*.025f&&player.MoveInput.sqrMagnitude>.01f;
            if(walking&&(!PacedTraversal||mechanism!=PhysicalMechanism.SprintPassage||player.Sprinting)){a.distance+=moved;a.activeSeconds+=dt;}
            if(mechanism==PhysicalMechanism.RepairStrap){Repair(dt);return;}
            if(mechanism==PhysicalMechanism.ProbeThenWalk&&station.Attempt.probeSamples<2)
            {
                if(tool&&tool.IsHeld&&tool.tip)
                {
                    bool contact=false;var flood=S.GetComponent<FloodSimulationManager>();
                    if(flood&&flood.TrySurface(tool.tip.position,out float surface,out float bed))contact=tool.tip.position.y<=surface&&tool.tip.position.y>=bed-.12f;
                    if(!contact)foreach(var hit in Physics.RaycastAll(tool.tip.position+Vector3.up*.12f,Vector3.down,.3f,~0,QueryTriggerInteraction.Ignore))if(hit.normal.y>.6f&&!hit.collider.GetComponentInParent<PhysicalGrabbable>()&&!hit.collider.GetComponentInParent<SibucaoPlayer>()){contact=true;break;}
                    if(contact)station.Probe(tool);
                }
                return;
            }
            if(mechanism==PhysicalMechanism.SwimPassage&&(!player.swimming||!player.swimming.IsSwimming))return;
            if(mechanism==PhysicalMechanism.SprintPassage&&!player.Sprinting)return;
            if((mechanism==PhysicalMechanism.GuideResident||mechanism==PhysicalMechanism.SignalHelp)&&a.actionStarted)
            {
                if(!useHeld)return;
                if(mechanism==PhysicalMechanism.SignalHelp&&!a.helperArrived)
                {
                    if(helper&&subject)
                    {
                        MoveHelper(player.transform.position-player.transform.forward*.65f,dt);
                        a.helperArrived=FlatDistance(helper.position,subject.transform.position)<1.8f&&FlatDistance(player.transform.position,subject.transform.position)<2.3f;
                    }
                    if(!a.helperArrived&&a.step>1)return;
                }
                else if(walking||mechanism==PhysicalMechanism.SignalHelp&&a.helperArrived)FollowSubject(dt);
            }
            if((mechanism==PhysicalMechanism.CarryResident||mechanism==PhysicalMechanism.TreatThenCarry)&&(!subject||!subject.IsHeld))return;
            if(mechanism==PhysicalMechanism.GatherItems&&(!S.inventory.HasBag||!station.manager.Data.flags.Contains(station.supplementary?"gathered_"+station.index:"spill_recovered")))return;
            if(mechanism==PhysicalMechanism.TalkThenWalk&&!a.actionStarted)return;
            if(a.step<path.Length&&walking)
            {
                var point=path[a.step].position;float distance=mechanism==PhysicalMechanism.Mantle?Vector3.Distance(player.transform.position,point):FlatDistance(player.transform.position,point);
                float tolerance=mechanism==PhysicalMechanism.Mantle?(a.step==0||a.step>=path.Length-2?.24f:.12f):mechanism==PhysicalMechanism.Walk||mechanism==PhysicalMechanism.TalkThenWalk||mechanism==PhysicalMechanism.FlashlightPassage?.8f:arrivalRadius;
                float heightTolerance=player.swimming&&player.swimming.IsSwimming?Mathf.Max(2.5f,S.GetComponent<FloodSimulationManager>().GetWaterDepth(point)+.5f):2.5f;
                if(distance<tolerance&&Mathf.Abs(player.transform.position.y-point.y)<heightTolerance)a.step++;
            }
            if(a.step<path.Length)return;
            if(requireContact&&!a.contactConsequence)return;
            if(mechanism==PhysicalMechanism.CarryResident||mechanism==PhysicalMechanism.TreatThenCarry)return; // Requires deliberate release.
            if(mechanism==PhysicalMechanism.BoardBoat)
            {
                if(!useHeld)return;a.toolSeconds+=dt;
                if(boat)
                {
                    var delta=station.transform.forward*dt*.65f;boat.position+=delta;
                    player.GetComponent<CharacterController>().Move(delta);
                }
                if(a.toolSeconds<5)return;
            }
            if(mechanism==PhysicalMechanism.GuideResident||mechanism==PhysicalMechanism.SignalHelp)
            {
                FollowSubject(dt);
                if(subject&&destination&&!ContainsBody(subject))return;
                if(subject){subject.delivered=true;subject.Body.isKinematic=true;}
            }
            bool routeTravel=mechanism==PhysicalMechanism.Walk||mechanism==PhysicalMechanism.TalkThenWalk||mechanism==PhysicalMechanism.FlashlightPassage||station.supplementary;
            if(requiredPassage&&!requiredPassage.State.crossed)return;
            if(!MinimumMovementDone||(!routeTravel&&!PacedTraversal&&a.activeSeconds+.15f<Choice.timeCost))return;
            FinishEncounterChoice();
        }
        void Repair(float dt)
        {
            if(HasEncounter){if(EncounterComplete)Attempt.toolStep=workSockets.Length;return;}
            var a=Attempt;if(!tool||!tool.IsHeld||!tool.tip||workSockets==null||workSockets.Length==0)return;
            if(a.toolStep>=workSockets.Length)return;
            var socket=workSockets[a.toolStep];float distance=Vector3.Distance(tool.tip.position,WorkTarget);
            if(distance<.095f&&Vector3.Dot(tool.transform.up,socket.up)>.85f)
            {
                a.toolSeconds+=dt;a.activeSeconds+=dt;
                if(a.toolSeconds>=Choice.timeCost/workSockets.Length){a.toolSeconds=0;a.toolStep++;}
            }
        }
        void FollowSubject(float dt)
        {
            if(!subject)return;
            Vector3 target=S.player.transform.position-S.player.transform.forward*.9f;
            var delta=target-subject.transform.position;delta.y=0;
            if(delta.magnitude>5)return;
            if(subject.Body.isKinematic)subject.Body.isKinematic=false;
            var velocity=Vector3.ClampMagnitude(delta*1.5f,mechanism==PhysicalMechanism.SignalHelp?1.5f:1.15f);
            var change=velocity-subject.Body.linearVelocity;change.y=0;subject.Body.AddForce(change*dt*(mechanism==PhysicalMechanism.SignalHelp?25:6),ForceMode.VelocityChange);
            if(helper)MoveHelper(subject.transform.position+station.transform.right*.8f,dt);
        }
        void MoveHelper(Vector3 target,float dt)
        {
            if(!helper)return;var delta=target-helper.position;delta.y=0;if(delta.magnitude<.2f)return;
            float distance=Mathf.Min(delta.magnitude,dt*1.15f);bool blocked=false;
            foreach(var hit in Physics.SphereCastAll(helper.position+Vector3.up*.85f,.24f,delta.normalized,distance,~0,QueryTriggerInteraction.Ignore))
                if(!hit.collider.GetComponentInParent<PhysicalGrabbable>()&&!hit.collider.GetComponentInParent<SibucaoPlayer>()&&!hit.collider.transform.IsChildOf(helper)&&!hit.collider.GetComponentInParent<SibucaoResident>()){blocked=true;break;}
            if(!blocked)helper.position+=delta.normalized*distance;
            helper.rotation=Quaternion.RotateTowards(helper.rotation,Quaternion.LookRotation(delta),dt*150);
        }
        public static float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
    }
}
