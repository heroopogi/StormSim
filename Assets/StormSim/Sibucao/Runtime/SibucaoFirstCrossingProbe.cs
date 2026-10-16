#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Editor-only regression. Fixtures teleport to approaches, then normal
    // production movement/grip input and all scene colliders perform the work.
    public sealed class SibucaoFirstCrossingProbe:MonoBehaviour
    {
        public string status="Idle";
        public List<string> results=new List<string>();
        SibucaoSession S=>SibucaoSession.Instance;
        SibucaoPlaythroughProbe input;
        PhysicalDecisionStation Station=>S.physicalDecisions.stations[0];
        public static SibucaoFirstCrossingProbe Get()
        {var p=FindFirstObjectByType<SibucaoFirstCrossingProbe>();return p?p:new GameObject("EDITOR ONLY first crossing regression").AddComponent<SibucaoFirstCrossingProbe>();}
        public void Run(string filter="",bool retainPrevious=false){StopAllCoroutines();StartCoroutine(Suite(filter,retainPrevious));}
        void OnDestroy(){SibucaoEditorInput.Reset();}
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        void Save(){System.IO.File.WriteAllLines("Documentation/SIBUCAO_FIRST_CROSSING_CHECKS.txt",new[]{"Scripted production-input Play Mode regression, not native manual input. "+DateTime.Now,status}.Concat(results));}
        IEnumerator Suite(string filter,bool retainPrevious)
        {
            results.Clear();
            if(retainPrevious&&System.IO.File.Exists("Documentation/SIBUCAO_FIRST_CROSSING_CHECKS.txt"))
                results.AddRange(System.IO.File.ReadAllLines("Documentation/SIBUCAO_FIRST_CROSSING_CHECKS.txt").Skip(2));
            input=SibucaoPlaythroughProbe.Get();Application.runInBackground=true;
            var cases=new List<(string,IEnumerator)>();
            for(int home=0;home<S.homes.Length;home++)
            {
                cases.Add(("Home "+home+" road shoulder +1.2m",Cross(home,1,1.2f)));
                cases.Add(("Home "+home+" road shoulder -1.3m",Cross(home,1,-1.3f)));
                cases.Add(("Home "+home+" fence sprint",Cross(home,2,0)));
                cases.Add(("Home "+home+" stick contacts and crossing",ProbeCross(home)));
                cases.Add(("Home "+home+" door to first crossing",FromHome(home)));
            }
            cases.Add(("No press-and-wait or endpoint completion",NoShortcut()));
            cases.Add(("Leaving a partial crossing releases engagement",LeavePassage()));
            cases.Add(("Release unfinished stick, then cross road",DropStick()));
            cases.Add(("Flooded crossing with live water and movement",FloodedCross()));
            foreach(var test in cases)
            {
                if(!string.IsNullOrEmpty(filter)&&!test.Item1.Contains(filter))continue;
                status=test.Item1;Exception failure=null;var stack=new Stack<IEnumerator>();stack.Push(test.Item2);
                while(stack.Count>0)
                {
                    object value=null;bool next=false;
                    try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){failure=e;break;}
                    if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
                }
                SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.GripHeld=SibucaoEditorInput.UseHeld=SibucaoEditorInput.Sprint=false;
                results.RemoveAll(x=>x.Substring(5).Split(new[]{" -- "},StringSplitOptions.None)[0]==test.Item1);
                results.Add((failure==null?"PASS ":"FAIL ")+test.Item1+(failure==null?"":" -- "+failure.GetBaseException().Message));Save();
            }
            status="Finished: "+results.Count(x=>x.StartsWith("PASS"))+" passed, "+results.Count(x=>x.StartsWith("FAIL"))+" failed";Save();
        }
        IEnumerator Setup(int home)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartRun();
            S.home.Suspend();S.home=S.homes[home];S.state.homeIndex=home;S.home.Begin();S.physicalDecisions.ResetRun();
            S.state.homeSeconds=72;S.home.Restore();S.state.physical.seed=23421;
            yield return null;yield return new WaitForFixedUpdate();yield return new WaitForSeconds(1);
        }
        IEnumerator Cross(int home,int choice,float shoulder)
        {
            yield return Setup(home);var b=Station.branches[choice];
            var offset=Station.transform.right*shoulder;
            var start=Station.branches[1].path[0].position-Station.transform.forward*7;
            S.player.Teleport(start+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.3f);
            Check(!Station.Resolved,"Resolved before approach");
            // Approach on the actual road; the fence lane is only open at its entrance.
            yield return input.MoveTo(Station.branches[1].path[0].position-Station.transform.forward*2,10);
            yield return input.MoveTo(b.path[0].position+offset-Station.transform.forward*2,10);
            SibucaoEditorInput.Sprint=choice==2;
            float before=S.state.homeSeconds;
            for(int i=0;i<b.path.Length;i++)yield return input.MoveTo(b.path[i].position+offset,10);
            yield return input.MoveTo(b.path[b.path.Length-1].position+offset+Station.transform.forward*2,10);
            Check(Station.Resolved&&S.state.physical.results[0]==choice,"Crossing did not record choice "+choice+"; step="+b.Attempt.step+" distance="+b.Attempt.distance);
            Check(Station.EngagedBranch==null&&float.IsPositiveInfinity(b.SpeedLimit),"Crossing left a speed limit/engagement");
            Check(S.state.homeSeconds>before,"Shared clock did not advance");
            Check(S.toastTitle=="Route updated","Missing neutral completion feedback");
        }
        IEnumerator NoShortcut()
        {
            yield return Setup(0);var b=Station.branches[1];
            S.player.Teleport(b.path[0].position+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.3f);
            SibucaoEditorInput.Interact=true;yield return new WaitForSeconds(2);
            Check(!Station.Resolved&&!b.Engaged,"Press/wait completed or engaged a crossing");
            S.player.Teleport(b.path.Last().position+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.2f);
            yield return input.MoveTo(b.path.Last().position+Station.transform.forward,5);
            Check(!Station.Resolved,"Starting at the endpoint completed a crossing");
        }
        IEnumerator LeavePassage()
        {
            yield return Setup(0);var b=Station.branches[1];
            S.player.Teleport(b.path[0].position-Station.transform.forward*2+Vector3.up*.12f,Station.transform.forward);
            yield return input.MoveTo(b.path[0].position+Station.transform.forward,8);Check(b.Engaged,"Road never engaged");
            yield return input.MoveTo(b.path[0].position-Station.transform.forward*2,8);yield return new WaitForSeconds(.6f);
            Check(Station.EngagedBranch==null&&!Station.Resolved,"Off-passage attempt stayed latched or completed");
            Check(float.IsPositiveInfinity(b.SpeedLimit),"Off-passage movement capped");
        }
        IEnumerator DropStick()
        {
            yield return Setup(0);var b=Station.branches[0];var tool=b.tool;
            S.player.Teleport(tool.transform.position-Station.transform.forward*1.1f+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.3f);
            var collider=tool.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger&&c.enabled);
            yield return input.Aim(collider.bounds.center);SibucaoEditorInput.GripDown=SibucaoEditorInput.GripHeld=true;
            yield return null;yield return new WaitForFixedUpdate();
            Check(S.player.grabber.held==tool&&b.Engaged,"Stick grip did not engage");
            Check(float.IsPositiveInfinity(b.SpeedLimit),"Stick engaged forced crawl");
            SibucaoEditorInput.GripHeld=false;yield return new WaitForSeconds(.5f);
            Check(Station.EngagedBranch==null&&!Station.Resolved,"Released incomplete stick stayed latched");
            var road=Station.branches[1];yield return input.MoveTo(road.path[0].position,8);
            yield return input.MoveTo(road.path.Last().position+Station.transform.forward,12);
            Check(Station.Resolved&&S.state.physical.results[0]==1,"Road unavailable after putting down stick");
        }
        IEnumerator ProbeCross(int home)
        {
            yield return Setup(home);var b=Station.branches[0];var tool=b.tool;
            S.player.Teleport(tool.transform.position-Station.transform.forward*1.2f+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.35f);
            yield return GripVisibleTool(tool);
            Check(b.Engaged,"Held stick did not engage its branch");
            Check(float.IsPositiveInfinity(b.SpeedLimit),"Forced stick speed limit");
            for(int sample=0;sample<2;sample++)
            {
                yield return input.Aim(Station.transform.TransformPoint(-2.5f,-.1f,sample*.85f));
                yield return input.MoveTo(Station.transform.TransformPoint(-2.5f,0,sample*.85f-1),15);yield return new WaitForSeconds(.8f);
            }
            Check(Station.Attempt.probeSamples==2&&!Station.Resolved,"Two distinct contacts required before traversal; samples="+Station.Attempt.probeSamples+" engaged="+b.Engaged+" held="+tool.IsHeld+" tip="+Station.transform.InverseTransformPoint(tool.tip.position));
            Check(b.Prompt=="Walk beside the boards to the far side","Missing next action after contacts");
            for(int i=1;i<b.path.Length;i++)yield return input.MoveTo(b.path[i].position,15);
            yield return input.MoveTo(b.path.Last().position+Station.transform.forward,10);
            Check(Station.Resolved&&S.state.physical.results[0]==0&&Station.EngagedBranch==null,"Probe route failed to complete");
            SibucaoEditorInput.GripHeld=false;
        }
        IEnumerator FromHome(int home)
        {
            yield return Setup(home);var h=S.home;
            Check(Vector3.Distance(S.player.transform.position,h.standingPoint.position)<1,"Not starting at normal standing point");
            var door=h.doorPanel.position;door.y=S.player.transform.position.y;
            var inner=door+h.transform.forward*1.3f;
            yield return input.MoveTo(inner,15);yield return input.Aim(h.doorPanel.position);
            SibucaoEditorInput.Interact=true;yield return null;yield return new WaitForSeconds(1);
            Check(S.state.homeDoorOpen,"E did not open front door");
            yield return input.MoveTo(door-h.transform.forward*1.25f,15);
            yield return WalkToRoad();
            float start=S.route.Progress(S.player.transform.position),end=Station.routeDistance-6;
            for(float d=start;d<end;d+=3)yield return input.MoveTo(S.route.Sample(d),12);
            var b=Station.branches[1];yield return input.MoveTo(b.path[0].position-Station.transform.forward*2,12);
            foreach(var point in b.path)yield return input.MoveTo(point.position,12);
            yield return input.MoveTo(b.path.Last().position+Station.transform.forward*2,12);
            Check(Station.Resolved&&S.state.physical.results[0]==1,"Home-to-crossing traversal failed");
            Check(S.missionGuide.CurrentStation!=Station,"Objective remained stuck at the crossing");
            Check(S.state.physical.history.Count(x=>x.node==0)==1,"Crossing recorded more than once");
        }
        IEnumerator WalkToRoad()
        {
            // Plan the test walk around existing houses/parked cars. This creates
            // no navigation aid in the game and never disables their colliders.
            var playerPosition=S.player.transform.position;var road=S.route.Sample(S.route.Progress(playerPosition));
            // The existing branch planner seeds connectivity at local (0,0,-5).
            // Place that seed at the actual doorway exit, not behind a nearby wall.
            var origin=playerPosition+Vector3.forward*5;
            var go=new GameObject("EDITOR ONLY home approach planning");go.transform.position=origin;
            var fixture=go.AddComponent<PhysicalDecisionStation>();fixture.enabled=false;
            Vector3[] points;
            try
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StormSim.Sibucao.Editor.SibucaoBranchPathBake")).First(t=>t!=null);
                var planner=Activator.CreateInstance(type,fixture,S.GetComponent<FloodSimulationManager>().terrain);
                points=(Vector3[])type.GetMethod("Route").Invoke(planner,new object[]{new[]{playerPosition-origin,road-origin}});
            }
            finally{DestroyImmediate(go);}
            foreach(var p in points)yield return input.MoveTo(origin+p,12);
        }
        IEnumerator GripVisibleTool(PhysicalGrabbable tool)
        {
            float until=Time.realtimeSinceStartup+3;
            while(!S.player.grabber.held&&Time.realtimeSinceStartup<until)
            {
                yield return input.Aim(tool.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger&&c.enabled).bounds.center);
                S.player.grabber.RefreshFocus(out _);
                if(S.player.grabber.CanReach&&S.player.grabber.FocusedItem==tool)
                {
                    SibucaoEditorInput.GripDown=SibucaoEditorInput.GripHeld=true;
                    yield return null;yield return new WaitForFixedUpdate();
                }
                if(!S.player.grabber.held){SibucaoEditorInput.GripHeld=false;yield return new WaitForSeconds(.1f);}
            }
            Check(S.player.grabber.held==tool,"Stick grip failed; focus="+S.player.grabber.FocusedItem+" reachable="+S.player.grabber.CanReach);
        }
        IEnumerator FloodedCross()
        {
            yield return Setup(0);var flood=S.GetComponent<FloodSimulationManager>();
            S.state.homeSeconds=SibucaoStormTimeline.FloodStart(S.home)+S.home.floodRiseDuration;
            yield return null;while(flood.IsCatchingUp)yield return null;
            var b=Station.branches[1];float depth=flood.GetWaterDepth(Station.transform.position);
            Check(depth>.1f,"Flood fixture is not wet: "+depth);
            var start=b.path[0].position-Station.transform.forward*2;
            if(flood.TrySurface(start,out float surface,out _))start.y=Mathf.Max(start.y,surface-1.45f);
            S.player.Teleport(start+Vector3.up*.12f,Station.transform.forward);yield return new WaitForSeconds(.35f);
            foreach(var point in b.path)yield return input.MoveTo(point.position,20);
            yield return input.MoveTo(b.path.Last().position+Station.transform.forward,12);
            Check(Station.Resolved&&S.state.physical.results[0]==1&&Station.EngagedBranch==null,"Flooded crossing failed to complete");
            Check(float.IsPositiveInfinity(b.SpeedLimit),"Flooded crossing retained a task speed cap");
        }
    }
}
#endif
