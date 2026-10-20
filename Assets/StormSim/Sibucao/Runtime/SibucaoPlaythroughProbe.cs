#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Drives the production Update/input, CharacterController, raycasts, joints and triggers.
    // Teleports only set up individual test cases. Never calls Resolve or EnterRoute.
    public sealed class SibucaoPlaythroughProbe:MonoBehaviour
    {
        public string status="Idle";
        public List<string> log=new List<string>();
        bool holdPreparation;
        void LateUpdate(){if(holdPreparation&&S)S.state.homeSeconds=90;}
        SibucaoSession S=>SibucaoSession.Instance;
        public static SibucaoPlaythroughProbe Instance;
        public static SibucaoPlaythroughProbe Get(){if(!Instance)Instance=new GameObject("EDITOR ONLY playthrough probe").AddComponent<SibucaoPlaythroughProbe>();return Instance;}
        void Awake(){Instance=this;SibucaoEditorInput.Reset();if(System.IO.File.Exists("Documentation/SIBUCAO_GAMEPLAY_PROBE.txt"))log.AddRange(System.IO.File.ReadAllLines("Documentation/SIBUCAO_GAMEPLAY_PROBE.txt").Skip(1));}
        void OnDestroy(){SibucaoEditorInput.Reset();}
        public void Run(string task,int index=0,int route=0)
        {
            StopAllCoroutines();SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.SetMode(SibucaoMode.Playing);status="Running "+task;
            holdPreparation=task=="suite";
            StartCoroutine(Guard(task=="suite"?Suite():task=="pickup"?Pickups():task=="route"?Route(index,route):task=="assist"?Assist(index):task=="strap"?Strap():task=="environment"?EnvironmentChecks():task=="walk"?WalkRoute():Idle()));
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object value=null;bool next=false;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception e){status="FAIL: "+e.Message;log.Add(status);Save();SibucaoEditorInput.Reset();yield break;}
                if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
            status="PASS";Save();SibucaoEditorInput.Reset();
        }
        void Save(){System.IO.File.WriteAllLines("Documentation/SIBUCAO_GAMEPLAY_PROBE.txt",new[]{"SCRIPTED Play-mode input; no native/manual keyboard walkthrough."}.Concat(log));}
        void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        IEnumerator Idle(){yield return null;}
        IEnumerator Suite()
        {
            log.Clear();log.Add("26 September 2026: current live scene regression, scripted production input.");
            S.homes=new[]{S.homes[0]};S.StartRun();S.state.homeSeconds=90;
            yield return new WaitForSeconds(1);yield return Pickups();
            for(int i=0;i<9;i++)
            {
                status="Testing B"+(i+1);
                if(i==4)yield return Strap();
                else if(i==5||i==7)yield return Assist(i);
                else yield return Route(i,i==2||i==6?2:0);
                Save();
            }
            Assert(S.Mode==SibucaoMode.Playing&&!S.state.physical.boundaryReached,"Creek ended session before school arrival");
            log.Add("PASS all nine decisions resolved without ending the session before the school entrance.");
            float start=S.route.Progress(S.player.transform.position);
            for(float d=start+2;d<S.route.length;d+=2){yield return Aim(S.route.Sample(d)+Vector3.up*1.6f);yield return MoveTo(S.route.Sample(d),12);}
            yield return MoveTo(S.physicalDecisions.evacuationCenter.arrivalPoint.position,12);
            yield return new WaitForSeconds(.2f);
            Assert(S.Mode==SibucaoMode.Complete&&S.state.physical.boundaryReached,"School entrance did not finish the scenario");
            log.Add("PASS walked from creek to dry school entrance; actual arrival completed scenario.");
        }
        public IEnumerator Aim(Vector3 target)
        {
            for(int frame=0;frame<6;frame++)
            {
                Vector3 dir=target-S.player.view.transform.position;var rotation=Quaternion.LookRotation(dir);
                float pitch=Mathf.DeltaAngle(0,rotation.eulerAngles.x),currentPitch=Mathf.DeltaAngle(0,S.player.view.transform.localEulerAngles.x);
                SibucaoEditorInput.Look=new Vector2(Mathf.DeltaAngle(S.player.transform.eulerAngles.y,rotation.eulerAngles.y),currentPitch-Mathf.Clamp(pitch,-80,80));yield return null;
            }
        }
        public IEnumerator MoveTo(Vector3 target,float seconds=15)
        {
            float until=Time.realtimeSinceStartup+seconds;Vector3 start=S.player.transform.position;
            while(Time.realtimeSinceStartup<until)
            {
                if(S.Mode==SibucaoMode.ArrivalReview||S.Mode==SibucaoMode.Complete&&S.state.physical.boundaryReached){SibucaoEditorInput.Move=Vector2.zero;yield break;}
                var delta=target-S.player.transform.position;delta.y=0;if(delta.magnitude<.2f)break;
                float scale=Mathf.Min(1,delta.magnitude/.45f);var v=delta.normalized*scale;
                bool pedestrian=Physics.SphereCastAll(S.player.transform.position+Vector3.up*.8f,.35f,delta.normalized,1.2f,~0,QueryTriggerInteraction.Ignore)
                    .Any(h=>h.collider.GetComponentInParent<SibucaoResident>() is SibucaoResident r&&r.enabled);
                if(pedestrian){var side=Vector3.Cross(Vector3.up,delta.normalized);v=(delta.normalized+side*1.5f).normalized*scale;}
                SibucaoEditorInput.Move=new Vector2(Vector3.Dot(v,S.player.transform.right),Vector3.Dot(v,S.player.transform.forward));yield return null;
            }
            SibucaoEditorInput.Move=Vector2.zero;yield return new WaitForFixedUpdate();
            var remaining=target-S.player.transform.position;remaining.y=0;
            Assert(remaining.magnitude<.35f,"Movement blocked "+start+" -> "+target+" stopped "+S.player.transform.position);
        }
        IEnumerator TapE(){SibucaoEditorInput.Interact=true;yield return null;yield return null;yield return new WaitForFixedUpdate();}
        IEnumerator LookAtItem(PhysicalGrabbable item)
        {
            var colliders=item.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger&&c.enabled).ToArray();Assert(colliders.Length>0,"No item collider "+item.name);
            var center=colliders.OrderByDescending(c=>c.bounds.size.sqrMagnitude).First().bounds.center;
            var flat=center-S.home.transform.forward*1.05f;flat.y=S.home.standingPoint.position.y+.1f;
            S.player.Teleport(flat,S.home.transform.forward);yield return new WaitForSeconds(.3f);yield return Aim(center);
            S.player.grabber.RefreshFocus(out _);Assert(S.player.grabber.FocusedItem==item,"Ray focus obstruction for "+item.name+" got "+S.player.grabber.FocusedItem);
        }
        IEnumerator Pickups()
        {
            yield return new WaitForSeconds(.5f);
            var items=S.home.GetComponentsInChildren<PhysicalGrabbable>(true);var bag=items.First(i=>i.kind==PhysicalItemKind.Bag);var water=items.First(i=>i.kind==PhysicalItemKind.Supply&&i.supplyBit==0);var food=items.First(i=>i.kind==PhysicalItemKind.Supply&&i.supplyBit==1);
            yield return LookAtItem(water);Assert(S.inventory.PickupPrompt(water)=="E: Pick Up (need bag)","Missing no-bag prompt");yield return TapE();Assert(water.gameObject.activeInHierarchy&&S.inventory.Count==0&&!S.player.grabber.held,"No-bag pickup changed item");log.Add("PASS need-bag prompt and E leaves water in world");
            yield return LookAtItem(bag);yield return TapE();Assert(S.inventory.HasBag&&!bag.gameObject.activeSelf,"E did not equip bag");
            foreach(var item in new[]{water,food})
            {
                yield return LookAtItem(item);Assert(S.inventory.PickupPrompt(item)=="E: Pick Up","Pickup prompt mismatch");SibucaoEditorInput.GripDown=SibucaoEditorInput.GripHeld=true;yield return null;yield return null;Assert(!S.player.grabber.held&&item.GetComponents<Joint>().Length==0,"Routine item acquired physics grip");SibucaoEditorInput.GripHeld=false;
                int count=S.inventory.Count;yield return TapE();Assert(!item.gameObject.activeSelf&&S.inventory.Count==count+1&&!S.player.grabber.held&&item.GetComponents<Joint>().Length==0,"E inventory pickup failed "+item.name);log.Add("PASS E pickup: "+item.name+"; world removed, inventory +1, no joint");
            }
        }
        IEnumerator Route(int index,int route)
        {
            var station=S.physicalDecisions.stations[index];var gates=station.GetComponentsInChildren<PhysicalDecisionSensor>().Where(x=>x.kind==PhysicalSensorKind.Path&&x.outcome==route).OrderBy(x=>x.sequenceStep).ToArray();Assert(gates.Length==3,"Expected three gates");
            var start=gates[0].transform.position-station.transform.forward*2;start.y=station.transform.position.y+.15f;S.player.Teleport(start,station.transform.forward);yield return new WaitForSeconds(.3f);
            yield return TapE();yield return new WaitForSeconds(2);Assert(!station.Resolved,"Press/wait resolved B"+(index+1));
            for(int i=0;i<3;i++)
            {
                yield return Aim(gates[i].transform.position+station.transform.forward*3);yield return MoveTo(gates[i].transform.position);yield return new WaitForSeconds(.15f);
                Assert(station.Attempt.routeSteps[route]>=i+1,"B"+(index+1)+" gate "+i+" did not trigger; steps "+station.Attempt.routeSteps[route]);if(i<2)Assert(!station.Resolved,"Route completed before exit");
            }
            Assert(station.Resolved,"Route exit did not resolve");log.Add("PASS B"+(index+1)+" route "+route+": E+wait no resolution, three gates crossed with CharacterController movement; result "+S.state.physical.results[index]);
        }
        IEnumerator WalkRoute()
        {
            float start=S.route.Progress(S.home.standingPoint.position)+8;S.player.Teleport(S.route.Sample(start)+Vector3.up*.15f,S.route.Sample(start+2)-S.route.Sample(start));
            float previous=start;
            foreach(var station in S.physicalDecisions.stations.OrderBy(x=>x.routeDistance))
            {
                if(station.routeDistance<start)continue;
                for(float d=previous+3;d<station.routeDistance-6;d+=3)
                {status="Walking route "+d.ToString("F0")+" / "+S.route.length.ToString("F0");yield return Aim(S.route.Sample(d)+Vector3.up*1.6f);yield return MoveTo(S.route.Sample(d),10);}
                int route=station.index==2||station.index==6?2:0;
                var gates=station.GetComponentsInChildren<PhysicalDecisionSensor>().Where(x=>x.kind==PhysicalSensorKind.Path&&x.outcome==route).OrderBy(x=>x.sequenceStep).ToArray();
                if(gates.Length>0)foreach(var gate in gates){yield return Aim(gate.transform.position+station.transform.forward*3);yield return MoveTo(gate.transform.position,18);}
                else foreach(float z in new[]{-4f,1f,6f}){var point=station.transform.TransformPoint(-.65f,0,z);yield return Aim(point+Vector3.up*1.6f);yield return MoveTo(point,18);}
                previous=station.routeDistance+6;
                if(S.Mode==SibucaoMode.Complete)break;
            }
            log.Add("PASS continuous CharacterController route walk");
        }
        IEnumerator EnvironmentChecks()
        {
            var f=S.GetComponent<FloodSimulationManager>();var audio=S.GetComponent<SibucaoRouteAudio>();var home=S.home;
            var station=S.physicalDecisions.stations[7];float clock=home.wakeDuration+home.newsDuration+home.standDuration+home.preparationDuration;
            var min=station.minimumTier;var max=station.maximumTier;int savedResult=S.state.physical.results[7];float savedStart=S.state.physical.started[7];
            for(int tier=0;tier<5;tier++)
            {
                float depth=new[]{.08f,.3f,.75f,1.25f,1.7f}[tier];S.state.homeSeconds=clock+depth/f.maximumDepth*home.floodRiseDuration;yield return null;yield return null;
                var actual=f.GetWaterTier(station.transform.position);Assert((int)actual==tier,"Depth tier mismatch "+actual+" wanted "+tier);
                S.state.physical.results[7]=-1;S.state.physical.started[7]=-1;station.minimumTier=station.maximumTier=(WaterTier)((tier+1)%5);station.Activate();Assert(!station.Active,"Tier exclusion failed");
                station.minimumTier=station.maximumTier=(WaterTier)tier;station.Activate();Assert(station.Active,"Tier inclusion failed");
                log.Add("PASS depth "+f.GetWaterDepth(station.transform.position).ToString("F2")+" m -> "+actual+"; station gated against live flood provider");
            }
            station.minimumTier=min;station.maximumTier=max;S.state.physical.results[7]=savedResult;S.state.physical.started[7]=savedStart;
            var renderer=f.transform.Find("TEMPORARY flood - placeholder ground ribbon").GetComponent<Renderer>();Assert(renderer.gameObject.activeInHierarchy&&renderer.enabled&&renderer.sharedMaterial==f.temporaryWaterMaterial&&renderer.sharedMaterial.shader.name=="StormSim/FloodTest/Depth Water","Route water renderer/material inactive");
            log.Add("PASS live route renderer uses Floodwater.mat / StormSim/FloodTest/Depth Water");
            var probe=AudioClip.Create("TEST ONLY silent trigger verification",441000,1,44100,false);
            var scream=audio.SFX_Scream_PLACEHOLDER;var crowd=audio.SFX_CrowdUrgency_PLACEHOLDER;var tierClip=audio.SFX_TierChange_PLACEHOLDER;
            audio.SFX_Scream_PLACEHOLDER=audio.SFX_CrowdUrgency_PLACEHOLDER=audio.SFX_TierChange_PLACEHOLDER=probe;
            foreach(var source in new[]{audio.wheelchair,audio.collapsed,audio.checkpoint,audio.boat})
            {
                source.Stop();S.state.physical.flags.Remove("audio_near_"+source.name);S.player.Teleport(source.transform.position+Vector3.up*.2f,Vector3.forward);yield return new WaitForSeconds(.2f);
                Assert(source.isPlaying,"Audio source did not fire "+source.name);log.Add("PASS "+source.name+" dispatched and AudioSource played test-only silent clip");
            }
            S.player.Teleport(station.transform.position+Vector3.up*.1f,station.transform.forward);S.state.homeSeconds=clock+25;yield return new WaitForSeconds(.2f);S.state.homeSeconds=clock+165;yield return new WaitForSeconds(.2f);
            Assert(audio.tierChange.isPlaying&&audio.TierTriggers>0,"Tier-change slot did not fire");log.Add("PASS tier-change AudioSource and rain/wind swell; event count "+audio.TierTriggers);
            audio.SFX_Scream_PLACEHOLDER=scream;audio.SFX_CrowdUrgency_PLACEHOLDER=crowd;audio.SFX_TierChange_PLACEHOLDER=tierClip;
            foreach(var source in new[]{audio.wheelchair,audio.collapsed,audio.checkpoint,audio.boat,audio.tierChange}){source.Stop();source.clip=null;}Destroy(probe);
            Assert(S.weather.rainAudio.isPlaying&&S.weather.windAudio.isPlaying,"Existing rain/wind not running");
            yield return new WaitForSeconds(2);
            var debris=FindObjectsByType<SibucaoFloatingDebris>(FindObjectsSortMode.None).Where(d=>f.TrySurface(d.transform.position,out _,out _)).ToArray();var before=debris.Select(d=>d.transform.position).ToArray();yield return new WaitForSeconds(3);
            foreach(string kind in new[]{"Branch","GI sheet","container","footwear"})
            {
                float drift=0;for(int i=0;i<debris.Length;i++)if(debris[i].name.Contains(kind)){var delta=debris[i].transform.position-before[i];delta.y=0;drift=Mathf.Max(drift,delta.magnitude);}
                Assert(drift>.05f,"No drifting "+kind);log.Add("PASS floating "+kind+" drift "+drift.ToString("F2")+" m / 3 s (live Rigidbody simulation)");
            }
            log.Add("Sound recordings remain unassigned; silent diagnostic clips removed. Rain/wind use existing assets.");
        }
        IEnumerator Grip(PhysicalGrabbable item,Vector3 target)
        {
            yield return Aim(target);S.player.grabber.RefreshFocus(out _);Assert(S.player.grabber.FocusedItem==item,"Grip ray blocked: "+item.name+" / "+S.player.grabber.FocusedItem);
            SibucaoEditorInput.GripDown=SibucaoEditorInput.GripHeld=true;yield return new WaitForSeconds(.3f);Assert(S.player.grabber.held==item,"Grip did not acquire "+item.name);
        }
        IEnumerator Assist(int index)
        {
            var station=S.physicalDecisions.stations[index];var item=station.assist;
            S.player.Teleport(station.transform.TransformPoint(0,.15f,-5),station.transform.forward);yield return new WaitForSeconds(.5f);yield return TapE();yield return new WaitForSeconds(2);Assert(!station.Resolved,"Assist press/wait auto-resolved");
            var handle=item.GetComponentsInChildren<Collider>().FirstOrDefault(c=>c.name=="Grab handle")??item.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger);
            Vector3 approach=item.transform.position-station.transform.forward*1.3f;approach.y=station.transform.position.y+.15f;
            S.player.Teleport(approach,station.transform.forward);yield return new WaitForSeconds(.3f);yield return Grip(item,handle.bounds.center);
            yield return new WaitForSeconds(1);Assert(!station.Resolved,"Grip/wait auto-resolved");
            yield return Aim(S.player.view.transform.position+station.transform.forward*10);
            yield return MoveTo(station.transform.TransformPoint(-2.2f,0,1.05f),20);yield return new WaitForSeconds(1.5f);
            Assert(station.Attempt.rampPassed,"Held body did not pass ramp; body local "+station.transform.InverseTransformPoint(item.transform.position));
            Assert(!station.Resolved,"Ramp alone auto-resolved");
            yield return MoveTo(station.transform.TransformPoint(-2.2f,0,3.25f),20);yield return new WaitForSeconds(1.5f);
            Assert(S.player.grabber.held==item&&!station.Resolved,"Assist grip lost or early resolution");
            var destination=station.GetComponentsInChildren<PhysicalDecisionSensor>().First(x=>x.kind==PhysicalSensorKind.AssistDestination);
            Assert(destination.ContainsBody(item),"Assisted body outside destination: "+station.transform.InverseTransformPoint(item.transform.position));
            SibucaoEditorInput.GripHeld=false;yield return new WaitForSeconds(.3f);Assert(station.Resolved&&item.delivered,"Deliberate assist release failed");
            log.Add("PASS B"+(index+1)+": E/wait no completion, mouse grip, move body through ramp, position whole body, deliberate release; moved "+station.Attempt.assistDistance.ToString("F2")+" m");
        }
        IEnumerator Strap()
        {
            var station=S.physicalDecisions.stations[4];Assert(S.inventory.HasBag,"Strap test needs equipped bag");
            S.player.Teleport(station.transform.TransformPoint(0,.15f,-5),station.transform.forward);yield return new WaitForSeconds(1.5f);yield return TapE();yield return new WaitForSeconds(1);Assert(!station.Resolved,"Strap press/wait auto-resolved");
            var item=station.spill.GetComponentsInChildren<PhysicalGrabbable>().First(x=>x.kind==PhysicalItemKind.Strap);
            var socket=station.spill.GetComponentsInChildren<PhysicalDecisionSensor>().First(x=>x.kind==PhysicalSensorKind.StrapSocket);
            Vector3 approach=item.transform.position-station.transform.forward*.4f;approach.y=station.transform.position.y+.15f;S.player.Teleport(approach,station.transform.forward);yield return new WaitForSeconds(.4f);
            yield return Grip(item,item.GetComponent<Collider>().bounds.center);
            SibucaoEditorInput.Scroll=-1000;yield return new WaitForSeconds(1.5f);
            yield return new WaitForSeconds(.5f);Assert(!station.Resolved,"Strap grip auto-resolved");
            float alignEnd=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<alignEnd)
            {
                Vector3 from=S.player.grabber.TargetToolRotation*Vector3.up,to=socket.transform.up;
                Vector3 axis=Vector3.Cross(from,to);float angle=Vector3.Angle(from,to);
                if(axis.sqrMagnitude>.0001f){var rotation=axis.normalized*Mathf.Min(2,angle*.15f);SibucaoEditorInput.ToolTurn=new Vector2(Vector3.Dot(rotation,S.player.view.transform.up),Vector3.Dot(rotation,S.player.view.transform.right));}
                if(Vector3.Dot(item.transform.up,to)>.985f)break;yield return null;
            }
            // Closed-loop hand positioning via movement and mouse-wheel, with the live joint left intact.
            float end=Time.realtimeSinceStartup+25;
            while(Time.realtimeSinceStartup<end&&S.player.grabber.held==item)
            {
                Vector3 error=socket.transform.position-item.tip.position;
                if(error.magnitude<.025f&&Vector3.Dot(item.transform.up,socket.transform.up)>.96f)break;
                SibucaoEditorInput.ToolTurn=Vector2.zero;
                var local=S.player.view.transform.InverseTransformDirection(error);
                SibucaoEditorInput.Look=new Vector2(Mathf.Clamp(local.x*2,-.5f,.5f),Mathf.Clamp(local.y*2,-.5f,.5f));
                SibucaoEditorInput.Scroll=Mathf.Clamp(local.z*40,-3,3);
                SibucaoEditorInput.Move=Vector2.zero;
                yield return null;
            }
            SibucaoEditorInput.Move=Vector2.zero;
            Assert(S.player.grabber.held==item,"Strap grip lost during positioning");
            Assert(socket.ContainsPoint(item.tip.position),"Strap tip misses socket by "+Vector3.Distance(item.tip.position,socket.transform.position).ToString("F3")+" m; upDot "+Vector3.Dot(item.transform.up,socket.transform.up));
            Assert(!station.Resolved,"Socket contact auto-resolved before release");SibucaoEditorInput.GripHeld=false;yield return new WaitForSeconds(.3f);Assert(station.Resolved&&item.secured,"Strap release did not repair");log.Add("PASS B5: bag spill, grip strap, move and align connector, release in socket");
        }
    }
}

#endif
