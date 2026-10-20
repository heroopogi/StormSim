using System;
using UnityEngine;
using UnityEngine.InputSystem;
using StormSim.Preparedness;

namespace StormSim.Sibucao
{
    public sealed class SibucaoSideMission : MonoBehaviour
    {
        public string id, title, callLine, completionLine;
        public int side;
        public bool teamwork;
        public SibucaoRouteSection section;
        public AudioSource voice;
        public Transform caller;
        public Transform[] companions;
        public Vector3[] companionStarts;
        Quaternion[] companionStartRotations;
        void Awake()
        {
            // Delivered blankets must travel with the family, including after save/restore.
            if(id=="B8_right"&&steps!=null&&companions!=null)
            {
                int recipient=0;
                foreach(var step in steps)
                    if(step.work==RouteWork.Carry&&step.target&&recipient<companions.Length)
                    {
                        var family=companions[recipient++];
                        if(family&&!step.target.IsChildOf(family))step.target.SetParent(family,true);
                    }
            }
            // Migrate the original B6 staging: its waiting child overlapped the wheelchair transfer lane.
            // Keep the same supervised side-mission activity, with its initial pose inside the side deck.
            if(id=="B6_left"&&steps!=null&&companions!=null)
                for(int i=0;i<companions.Length;i++)if(companions[i]&&companions[i].name=="Child waiting with volunteer")
                {
                    var pose=companionStarts[i];pose.x=-.35f;companionStarts[i]=pose;companions[i].localPosition=pose;
                    foreach(var step in steps)if(step.item==companions[i]){step.from=pose;step.to=pose;}
                }
            companionStartRotations=new Quaternion[companions==null?0:companions.Length];
            for(int i=0;i<companionStartRotations.Length;i++)if(companions[i])companionStartRotations[i]=Quaternion.Inverse(transform.rotation)*companions[i].rotation;
        }
        public RouteMissionStep[] steps;
        public Vector3[] route;
        public GameObject floodClosure;
        public float closureDepth = .18f;
        bool manipulating, holding;
        float blockedSeconds;
        float bestEscortDistance = float.PositiveInfinity;
        int measuredEscortPoint = -1;
        static readonly float[] AvoidanceAngles={0,35,-35,65,-65};
        readonly RaycastHit[] escortHits=new RaycastHit[32];
        float escortSpeed,foleyAt,observationSeconds;
        public string EscortStatus {get;private set;}
        public bool PlacementReady => holding && Step!=null && State.carryDistance>.08f && Vector3.Distance(Step.item.position,Step.target.position)<.8f && PlacementClear();
        public bool Manipulating => manipulating;
        Vector3 carryOrigin;
        float carryRadius = .12f;
        public RouteMissionProgress State => section.director.Progress(id);
        public RouteMissionStep Step => State.step < steps.Length ? steps[State.step] : null;
        public Vector3 WorkTarget => Step != null && Step.work == RouteWork.Escort && route != null && route.Length > 0 ?
            route[Mathf.Min(State.escortPoint, route.Length - 1)] : holding && Step != null ? Step.target.position :
            Step != null && Step.item ? Step.item.position : section.Merge;
        public bool Available => section.director.Current == section && !State.complete;
        public bool WithinWorkArea(Vector3 position)
        {
            if(SibucaoRouteChoices.Flat(position,transform.position)<=30)return true;
            if(route==null)return false;
            for(int i=1;i<route.Length;i++)
            {
                var line=route[i]-route[i-1];line.y=0;
                var offset=position-route[i-1];offset.y=0;
                float t=line.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(offset,line)/line.sqrMagnitude):0;
                if((offset-line*t).sqrMagnitude<=4.5f*4.5f)return true;
            }
            return false;
        }
        public bool Closed
        {
            get
            {
                var s = SibucaoSession.Instance; var f = s ? s.GetComponent<FloodSimulationManager>() : null;
                if (!f || f.IsCatchingUp || route == null) return false;
                if (f.GetWaterDepth(transform.position) > closureDepth) return true;
                foreach (var p in route) if (f.GetWaterDepth(p) > closureDepth) return true;
                return false;
            }
        }
        public string Hint
        {
            get
            {
                if (Closed) return "This passage is flooded. Return to the junction.";
                var step = Step;
                if (step == null) return "Continue through the passage to the next section.";
                if(step.work==RouteWork.Check)return "Observe: "+step.instruction+" · keep looking briefly, or E to inspect.";
                if(step.work==RouteWork.Preparation)
                {
                    if(step.preparationUse==RoutePreparationUse.Advisory)
                        return HasReadyRadio()?"E: listen to your tested radio at the advisory post.":HasReadyPhone()?"E: check the charged phone alert at the advisory post.":"E: inspect the posted barangay route marker.";
                    if(step.preparationUse==RoutePreparationUse.ParentAssist)
                        return HasPreparedItem("gloves")?"E: put on your packed work gloves and offer help.":HasPreparedItem("whistle")?"E: use your packed whistle to call a nearby adult helper.":"E: agree on a careful two-person carry with the parent.";
                }
                if (holding) return (PlacementReady?"Ready to place · E / F":"Carry to "+step.target.name+" · "+Vector3.Distance(step.item.position,step.target.position).ToString("0.0")+" m")+" / Q: put down";
                if (step.work == RouteWork.Turn || step.work == RouteWork.Slide) return State.amount>=.999f?"At end stop · release E to finish / Q: pause":"Hold E + drag RIGHT: "+step.instruction+" · "+Mathf.RoundToInt(State.amount*100)+"% / left reverses / release pauses";
                if (step.work == RouteWork.Escort && section.director.Working == this) return (string.IsNullOrEmpty(EscortStatus)?"Walk with the group. Stay close at each turn.":EscortStatus)+" / Q: pause / Esc: recovery";
                return (step.work == RouteWork.Carry ? "F / E: " : "E: ") + step.instruction;
            }
        }
        public bool CanUse(Transform hit)
        {
            if (!Available) return false;
            if (Step == null) return hit == caller || hit.IsChildOf(caller);
            return hit == Step.item || hit.IsChildOf(Step.item);
        }
        public bool VisibleFrom(Vector3 eye)
        {
            var point = caller.position + Vector3.up * 1.3f; var d = point - eye;
            var hits = Physics.RaycastAll(eye, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<SibucaoPlayer>()) continue;
                return h.transform.IsChildOf(transform);
            }
            return true;
        }
        public void Call()
        {
            section.director.StopVoices();
            SibucaoSession.Instance.Say((side == 0 ? "From the left: " : "From the right: ") +
                (Closed ? "This passage is flooded. Use another way!" : callLine), !Closed&&voice&&voice.clip?Mathf.Max(6,voice.clip.length+.5f):6);
            if (!Closed && voice && voice.clip){voice.volume=SibucaoAudioMix.Speech;voice.Play();}
            var actor = caller.GetComponent<SibucaoActor>(); if (actor) actor.React();
        }
        public bool Interact()
        {
            if (!Available) return false;
            if (Closed) { SibucaoSession.Instance.Say("Water has reached this passage. Return to the junction.", 4); return true; }
            section.director.Begin(this);
            var step = Step; if (step == null) return true;
            if (step.work == RouteWork.Talk || step.work == RouteWork.Check) { Advance(); return true; }
            if(step.work==RouteWork.Preparation){Advance(ResolvePreparation(step));return true;}
            if (step.work == RouteWork.Carry)
            {
                step.item.SetParent(transform,true);
                holding = true; carryOrigin = step.item.position;
                var shapes = step.item.GetComponentsInChildren<Collider>();
                var bounds = new Bounds(step.item.position, Vector3.zero);
                foreach(var shape in shapes) bounds.Encapsulate(shape.bounds);
                carryRadius = shapes.Length > 0 ? Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z), .02f, .12f) : .12f;
                section.director.SetHeld(this); return true;
            }
            if (step.work == RouteWork.Escort) return true;
            manipulating = true; SibucaoControls.BeginGrip();return true;
        }
        public void ReleaseCarry()
        {
            if (!holding || Step == null) return;
            var step = Step; holding = false; section.director.SetHeld(null);
            bool moved = State.carryDistance > .08f;
            if (moved && Vector3.Distance(step.item.position, step.target.position) < .8f && PlacementClear())
            {
                step.item.SetPositionAndRotation(step.target.position, step.target.rotation);
                step.item.SetParent(step.target,true);
                State.hasCarriedPosition = false; Advance();
            }
            else
            {
                PutDown(step.item);
                State.carriedPosition = transform.InverseTransformPoint(step.item.position); State.hasCarriedPosition = true;
                SibucaoSession.Instance.Say("Place it beside " + step.target.name + ".", 3);
            }
        }
        public void Cancel()
        {
            bool wasWorking=section.director.Working==this;
            if(holding&&Step!=null)PutDown(Step.item);
            holding = manipulating = false;SibucaoControls.ResetGrip();escortSpeed=0;
            if (Step != null && Step.work == RouteWork.Carry)
            {
                State.carriedPosition = transform.InverseTransformPoint(Step.item.position); State.hasCarriedPosition = true;
            }
            section.director.Release(this);
            if(wasWorking){SibucaoSession.Instance.Notify("Work paused", "Completed steps kept. Aim at the current item to resume, or use Esc > Recover task.");SibucaoSession.Instance.RequestCheckpoint();}
        }
        void PutDown(Transform item)
        {
            var hits=Physics.RaycastAll(item.position+Vector3.up*.2f,Vector3.down,4,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            var renderers=item.GetComponentsInChildren<Renderer>();var bounds=new Bounds(item.position,Vector3.zero);
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float offset=item.position.y-bounds.min.y;
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(item)||hit.collider.GetComponentInParent<SibucaoPlayer>()||hit.normal.y<.7f)continue;
                item.position=new Vector3(item.position.x,hit.point.y+offset+.015f,item.position.z);break;
            }
        }
        public void Tick(float dt)
        {
            if (!Available) { Cancel(); return; }
            if (Closed) { Cancel(); SibucaoSession.Instance.Say("This passage has flooded. Head back to the junction.", 4); return; }
            var s = SibucaoSession.Instance; var step = Step;
            if(step!=null&&step.work==RouteWork.Check)
            {
                observationSeconds=section.director.Focused()==this?observationSeconds+dt:0;
                if(observationSeconds>=1.2f){Advance();return;}
            }
            else observationSeconds=0;
            if (step == null)
            {
                if (SibucaoRouteChoices.Flat(s.player.transform.position, section.Merge) < 3)
                    section.director.Complete(this);
                return;
            }
            if (holding)
            {
                var cam = s.player.view.transform; var desired = cam.position + cam.forward * 1.35f - Vector3.up * .3f;
                var movement = desired - step.item.position;
                // Stop carried objects at walls instead of allowing delivery through a building.
                var hits = Physics.SphereCastAll(step.item.position, carryRadius, movement.normalized, movement.magnitude, ~0, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                    if (!hit.transform.IsChildOf(step.item) && !hit.transform.IsChildOf(s.player.transform) && hit.distance > .02f)
                        movement = movement.normalized * Mathf.Min(movement.magnitude, Mathf.Max(0, hit.distance - .03f));
                var displacement=Vector3.ClampMagnitude(movement, dt * 3);
                step.item.position += displacement;State.carryDistance+=displacement.magnitude;
                State.carriedPosition = transform.InverseTransformPoint(step.item.position); State.hasCarriedPosition = true;
                if (movement.sqrMagnitude > .0001f) State.activeSeconds += dt;
            }
            else if (manipulating)
            {
                bool pressed = SibucaoControls.WorkHeld;
                Vector2 drag = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#if UNITY_EDITOR
                if (SibucaoEditorInput.Active) { pressed = SibucaoEditorInput.UseHeld; drag = SibucaoEditorInput.ToolTurn; }
#endif
                if (!pressed)
                {
                    manipulating = false;
                    if (State.amount >= .999f) Advance();
                    return;
                }
                if (!WithinReach(s.player.view.transform.position, step.item, 3.4f)) { manipulating = false; s.Say("Move closer to continue. Your progress is kept.",3); return; }
                // Direction matters. Reversing drag undoes motion; idle holding earns nothing.
                float input = drag.x;
                float delta = Mathf.Clamp(input * .006f, -dt / step.seconds, dt / step.seconds);
                State.amount = Mathf.Clamp01(State.amount + delta);
                if (Mathf.Abs(delta) > .00001f) State.activeSeconds += dt;
                ApplyStep(step, State.amount);
                if(Mathf.Abs(delta)>.0001f&&Time.time>foleyAt){foleyAt=Time.time+.2f;SibucaoSoundFeedback.Play(SibucaoSoundKind.Mechanism);}
            }
            else if (step.work == RouteWork.Escort) TickEscort(dt);
        }
        public static bool WithinReach(Vector3 eye, Transform item, float reach)
        {
            foreach(var shape in item.GetComponentsInChildren<Collider>())
                if(shape.enabled && !shape.isTrigger && Vector3.Distance(eye,shape.ClosestPoint(eye))<=reach)return true;
            return false;
        }
        bool PlacementClear()
        {
            if(Step==null||!Step.target)return false;
            var delta=Step.target.position-Step.item.position;
            foreach(var hit in Physics.RaycastAll(Step.item.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                // A base-aligned placement target may end exactly on its supporting deck.
                // Only allow the upward-facing endpoint; walls and intervening floors still block placement.
                if(hit.normal.y>.65f&&hit.distance>=delta.magnitude-.025f)continue;
                if(!hit.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<SibucaoPlayer>())return false;
            }
            return true;
        }
        bool ClearEscortStep(Transform actor,Vector3 candidate)
        {
            var delta=candidate-actor.position;
            int count=Physics.SphereCastNonAlloc(actor.position+Vector3.up,.18f,delta.normalized,escortHits,delta.magnitude+.04f,~0,QueryTriggerInteraction.Ignore);
            if(count==escortHits.Length)return false;
            for(int i=0;i<count;i++)if(!escortHits[i].transform.IsChildOf(transform)&&!escortHits[i].collider.GetComponentInParent<SibucaoPlayer>()&&!escortHits[i].collider.GetComponentInParent<SibucaoActor>())return false;
            return true;
        }
        Vector3 EscortDestination(int point,int index)
        {
            point=Mathf.Clamp(point,0,route.Length-1);
            var session=SibucaoSession.Instance;
            if(session&&session.GetComponent<CoastalAreaProfile>())
            {
                // Narrow mapped streets need a single-file group. Follow the actual
                // bends instead of placing a resident beside the path inside a wall.
                float remaining=index*.65f;var destination=route[point];
                for(int i=point;i>0&&remaining>0;i--)
                {
                    var previous=route[i-1];float length=Vector3.Distance(destination,previous);
                    if(length>=remaining){destination=Vector3.MoveTowards(destination,previous,remaining);remaining=0;}
                    else{remaining-=length;destination=previous;}
                }
                if(remaining>0&&route.Length>1){var backwards=route[0]-route[1];backwards.y=0;destination+=backwards.normalized*remaining;}
                return destination+Vector3.up*companionStarts[index].y;
            }
            var direction=point+1<route.Length?route[point+1]-route[point]:point>0?route[point]-route[point-1]:transform.forward;
            direction.y=0;if(direction.sqrMagnitude<.001f)direction=transform.forward;direction.Normalize();
            var right=Vector3.Cross(Vector3.up,direction);
            // Each resident has a distinct slot; the third person must not seek the first person's occupied spot.
            return route[point]+right*((index%2)*.55f)-direction*((index/2)*.75f)+Vector3.up*companionStarts[index].y;
        }
        void TickEscort(float dt)
        {
            if(route==null||route.Length==0)return;
            var s=SibucaoSession.Instance;int point=Mathf.Min(State.escortPoint,route.Length-1);var goal=route[point];
            if(SibucaoRouteChoices.Flat(s.player.transform.position,goal)>4.5f)
            {escortSpeed=0;EscortStatus="Group waiting · follow the map to the next turn.";return;}
            escortSpeed=Mathf.MoveTowards(escortSpeed,s.player.Sprinting?1.8f:1.45f,dt*1.8f);
            bool arrived=true,moved=false;
            for(int i=0;i<companions.Length;i++)
            {
                var actor=companions[i];if(!actor)continue;
                var destination=EscortDestination(point,i);
                if(Vector3.Distance(actor.position,destination)<=.06f)continue;
                arrived=false;
                var direct=Vector3.MoveTowards(actor.position,destination,dt*escortSpeed)-actor.position;
                foreach(float angle in AvoidanceAngles)
                {
                    var motion=Quaternion.Euler(0,angle,0)*direct;var candidate=actor.position+motion;
                    if(!ClearEscortStep(actor,candidate))continue;
                    bool crowded=false;for(int other=0;other<companions.Length;other++)if(other!=i&&companions[other]&&Vector3.Distance(candidate,companions[other].position)<.42f&&Vector3.Distance(actor.position,companions[other].position)>.42f){crowded=true;break;}
                    if(crowded)continue;
                    actor.position=candidate;motion.y=0;if(motion.sqrMagnitude>.00001f)actor.rotation=Quaternion.RotateTowards(actor.rotation,Quaternion.LookRotation(motion),dt*140);
                    moved=true;break;
                }
            }
            float remaining=0;
            for(int i=0;i<companions.Length;i++)if(companions[i])
                remaining+=Vector3.Distance(companions[i].position,EscortDestination(point,i));
            // Shuffling against a wall is movement, but it is not progress toward the waypoint.
            if(measuredEscortPoint!=point||arrived||remaining<bestEscortDistance-.025f)
            {measuredEscortPoint=point;bestEscortDistance=remaining;blockedSeconds=0;}
            else blockedSeconds+=dt;
            EscortStatus=blockedSeconds>2?"Group blocked · clear the passage or Esc > Recover task > Regroup.":"Group following · wait together at the next turn.";
            if(moved)State.activeSeconds+=dt;
            if(arrived){if(point==route.Length-1){Advance();return;}State.escortPoint++;}
        }
        public void RecoverCurrentStep()
        {
            var step=Step;if(step==null)return;
            Cancel();
            if(step.work==RouteWork.Escort)
            {
                for(int i=0;i<companions.Length;i++)if(companions[i])
                    companions[i].position=State.escortPoint>0?EscortDestination(State.escortPoint-1,i):transform.TransformPoint(companionStarts[i]);
                State.companionPositions=null;State.companionRotations=null;blockedSeconds=0;measuredEscortPoint=-1;EscortStatus="Group regrouped · press E at the resident to resume.";
            }
            else if(step.work==RouteWork.Carry)
            {
                step.item.SetParent(step.originalParent?step.originalParent:transform,false);
                step.item.localPosition=step.from;step.item.localRotation=Quaternion.Euler(step.fromAngle);
                State.hasCarriedPosition=false;State.carryDistance=0;
            }
            else if(step.work==RouteWork.Slide||step.work==RouteWork.Turn){State.amount=0;ApplyStep(step,0);}
            Physics.SyncTransforms();
            SibucaoSession.Instance.Notify("Current action recovered","Completed actions are kept. Use the highlighted item or resident to resume.");
        }
        PhysicalGrabbable Packed(string tag)
        {
            var inventory=SibucaoSession.Instance.inventory;
            if(!inventory)return null;
            for(int i=0;i<SibucaoInventory.Capacity;i++)
            {var item=inventory.Slot(i);if(item&&item.inventoryTag==tag)return item;}
            return null;
        }
        PhysicalGrabbable PackedPreparationItem(string id)
        {
            var inventory=SibucaoSession.Instance.inventory;if(!inventory)return null;
            for(int i=0;i<SibucaoInventory.Capacity;i++)
            {var item=inventory.Slot(i);var prepared=item?item.GetComponent<PreparednessItem>():null;if(prepared&&prepared.id==id)return item;}
            return null;
        }
        bool HasPreparedItem(string id)
        {var item=PackedPreparationItem(id);return item&&item.GetComponent<PreparednessItem>().prepared;}
        bool HasReadyRadio()
        {
            var item=Packed("radio");if(!item)return false;
            var prepared=item.GetComponent<PreparednessItem>();return prepared&&prepared.prepared&&prepared.cells>=2;
        }
        bool HasReadyPhone()
        {
            var item=Packed("phone");if(!item)return false;
            var phone=item.GetComponent<PreparednessItem>();
            if(phone&&phone.charge>=PreparednessSession.UsefulCharge)return true;
            var power=Packed("powerbank")?.GetComponent<PreparednessItem>();
            return phone&&phone.charge>0&&power&&power.charge>=PreparednessSession.UsefulCharge&&Packed("cable");
        }
        int ResolvePreparation(RouteMissionStep step)
        {
            var s=SibucaoSession.Instance;
            if(step.preparationUse==RoutePreparationUse.Advisory)
            {
                if(HasReadyRadio())
                {State.preparationChoice="radio";s.physicalDecisions.SetFlag("b1_advisory_radio");s.Say("Your tested radio repeats the barangay instruction: use the raised route and keep away from the deeper road centre.",7);}
                else if(HasReadyPhone())
                {State.preparationChoice="phone";s.physicalDecisions.SetFlag("b1_advisory_phone");s.Say("The charged phone shows the latest barangay alert: the raised route remains open. Do not enter the deeper road centre.",7);}
                else
                {State.preparationChoice="posted-marker";s.physicalDecisions.SetFlag("b1_advisory_marker");s.Say("The physical barangay marker points to the raised route. Without a working device, use the posted local instruction.",7);}
                return 0;
            }
            if(step.preparationUse==RoutePreparationUse.ParentAssist)
            {
                if(HasPreparedItem("gloves"))
                {State.preparationChoice="gloves";s.physicalDecisions.SetFlag("b1_parent_gloves");s.Say("You put on the packed work gloves. Carry the supply bag while the parent keeps both hands on the stroller.",7);return 0;}
                if(HasPreparedItem("whistle"))
                {
                    State.preparationChoice="whistle";s.physicalDecisions.SetFlag("b1_parent_whistle");
                    var whistle=PackedPreparationItem("whistle").GetComponent<PosterSupply>();if(whistle)whistle.Signal();
                    int next=State.step+1;
                    if(next<steps.Length&&steps[next].work==RouteWork.Carry)
                    {var carry=steps[next];carry.item.SetPositionAndRotation(carry.target.position,carry.target.rotation);carry.item.SetParent(carry.target,true);}
                    s.Say("Three short whistle signals bring a nearby adult. They take the supply bag while you guide the parent and stroller.",7);return 1;
                }
                State.preparationChoice="shared-carry";s.physicalDecisions.SetFlag("b1_parent_shared_carry");
                s.Say("You have no suitable gloves or whistle. The parent steadies the stroller while you carry only the separate supply bag.",7);
            }
            return 0;
        }
        void Advance(int additionalSteps=0)
        {
            var step = Step;
            SibucaoControls.ResetGrip();SibucaoSoundFeedback.Play(step.work==RouteWork.Carry?SibucaoSoundKind.Placement:SibucaoSoundKind.Mechanism);
            if(caller){var actor=caller.GetComponent<SibucaoActor>();if(actor)actor.React();}
            if(step.attachAfter)step.item.SetParent(step.attachAfter,true);
            if (step.reveal) step.reveal.SetActive(true);
            if (step.lamp) step.lamp.enabled = true;
            if (!string.IsNullOrEmpty(step.response)) SibucaoSession.Instance.Say(step.response, 5);
            State.step=Mathf.Min(steps.Length,State.step+1+additionalSteps); State.amount = 0; State.carryDistance=0; State.hasCarriedPosition = false; manipulating = false;observationSeconds=0;
            SibucaoSession.Instance.RequestCheckpoint();
            if (State.step == steps.Length) SibucaoSession.Instance.Notify("Work finished · outcome pending", "Reach the merge marker to record this passage.");
        }
        void ApplyStep(RouteMissionStep step, float amount)
        {
            if (!step.item) return;
            if (step.work == RouteWork.Slide) step.item.localPosition = Vector3.Lerp(step.from, step.to, amount);
            if (step.work == RouteWork.Turn) step.item.localRotation = Quaternion.Slerp(Quaternion.Euler(step.fromAngle), Quaternion.Euler(step.toAngle), amount);
        }
        public void Restore()
        {
            manipulating = holding = false; blockedSeconds=0;measuredEscortPoint=-1;escortSpeed=0;observationSeconds=0;EscortStatus=null;
            var state = State;
            // Reset once per item, then replay completed operations in order (open then close works).
            var restored = new System.Collections.Generic.HashSet<Transform>();
            foreach (var step in steps)
            {
                if (step.reveal) step.reveal.SetActive(false);
                if (step.lamp) step.lamp.enabled = false;
                if (step.item && restored.Add(step.item))
                { step.item.SetParent(step.originalParent?step.originalParent:transform,false);step.item.localPosition = step.from; step.item.localRotation = Quaternion.Euler(step.fromAngle); }
            }
            for (int i = 0; i < Mathf.Min(state.step, steps.Length); i++)
            {
                var step = steps[i]; ApplyStep(step, 1);
                if (step.work == RouteWork.Carry) {step.item.SetPositionAndRotation(step.target.position, step.target.rotation);step.item.SetParent(step.target,true);}
                if(step.attachAfter)step.item.SetParent(step.attachAfter,true);
                if (step.reveal) step.reveal.SetActive(true);
                if (step.lamp) step.lamp.enabled = true;
            }
            if (Step != null)
            {
                ApplyStep(Step, state.amount);
                if (Step.work == RouteWork.Carry && state.hasCarriedPosition) Step.item.position = transform.TransformPoint(state.carriedPosition);
            }
            for (int i = 0; i < companions.Length; i++) if (companions[i])
            {
                companions[i].position = (state.started||state.complete)&&state.companionPositions!=null&&i<state.companionPositions.Length?transform.TransformPoint(state.companionPositions[i]):state.escortPoint > 0 && route != null && route.Length > 0 ? EscortDestination(state.escortPoint-1,i) : transform.TransformPoint(companionStarts[i]);
                if(state.companionRotations!=null&&i<state.companionRotations.Length)companions[i].rotation=transform.rotation*state.companionRotations[i];
                else if(companionStartRotations!=null&&i<companionStartRotations.Length)companions[i].rotation=transform.rotation*companionStartRotations[i];
            }
        }
        void Update()
        {
            if(voice)voice.volume=SibucaoAudioMix.Speech;
            if (!SibucaoSession.Instance || SibucaoSession.Instance.Mode != SibucaoMode.Playing) return;
            if (floodClosure) floodClosure.SetActive(Closed);
        }
    }
}
