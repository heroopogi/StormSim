#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Editor-only regression driver. Fresh runs use StartRun and input from the home,
    // with no position/clock override; other cases are explicitly labelled fixtures.
    public sealed class SibucaoPriorityOneProbe:MonoBehaviour
    {
        public string status="Idle";public int freshSeed=-1;
        public List<string> results=new List<string>();
        SibucaoSession S=>SibucaoSession.Instance;
        SibucaoPlaythroughProbe input;
        PhysicalDecisionStation B1=>S.physicalDecisions.stations[0];
        public static SibucaoPriorityOneProbe Get(){var p=FindFirstObjectByType<SibucaoPriorityOneProbe>();return p?p:new GameObject("EDITOR ONLY priority one regression").AddComponent<SibucaoPriorityOneProbe>();}
        void Awake(){if(System.IO.File.Exists("Documentation/SIBUCAO_PRIORITY1_CHECKS.txt"))results.AddRange(System.IO.File.ReadAllLines("Documentation/SIBUCAO_PRIORITY1_CHECKS.txt").Skip(2));}
        void OnDestroy(){SibucaoEditorInput.Reset();}
        public void Run(string mode,int choice=1)
        {
            StopAllCoroutines();input=SibucaoPlaythroughProbe.Get();Application.runInBackground=true;
            StartCoroutine(Guard(mode,mode=="durations"?Durations():mode=="cancel"?CancelAll():mode=="probe"?ProbeFixture():mode=="complete_probe"?CompleteProbeFixture():mode=="edges"?Edges():mode=="contrast"?Contrast():Fresh(choice)));
        }
        void Save(){System.IO.File.WriteAllLines("Documentation/SIBUCAO_PRIORITY1_CHECKS.txt",new[]{"AUTOMATED production-input Play Mode checks, not native/manual or visibility-only play. "+DateTime.Now,status}.Concat(results));}
        void Record(string text){results.Add(text);Save();}
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        void StartStormRun()
        {
            var preparation=S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();
            if(!preparation){S.StartRun();return;}
            if(preparation.Active)
            {
                if(preparation.CanSkipToDuring){preparation.SkipToDuring();return;}
                throw new InvalidOperationException("Enable temporary development controls so the B1 regression can leave PREPARATION without changing task results.");
            }
            var handingOff=typeof(StormSim.Preparedness.SibucaoPreparationBridge).GetField("<HandingOff>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance);
            handingOff.SetValue(preparation,true);
            try{S.StartRun();}finally{handingOff.SetValue(preparation,false);}
        }
        IEnumerator Guard(string name,IEnumerator work)
        {
            status=name;var stack=new Stack<IEnumerator>();stack.Push(work);Exception error=null;
            while(stack.Count>0)
            {
                object value=null;bool next=false;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;break;}
                if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
            SibucaoEditorInput.Reset();
            status=error==null?"PASS "+name:"FAIL "+name+": "+error.GetBaseException().Message;Record(status);
        }
        IEnumerator Invoke(MonoBehaviour host,string method,params object[] args)=>(IEnumerator)host.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(host,args);
        IEnumerator Fixture(int node,bool wet=false,int home=0)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;StartStormRun();
            S.home.Suspend();S.home=S.homes[home];S.state.homeIndex=home;S.home.Begin();S.physicalDecisions.ResetRun();
            if(wet)S.state.difficultyLevel=5; // Exercise the real wet interaction independently of the menu default.
            S.state.homeSeconds=wet?SibucaoStormTimeline.FloodStart(S.home)+Mathf.Max(70,S.home.floodRiseDuration):90;S.home.Restore();S.state.physical.seed=23421;
            foreach(var st in S.physicalDecisions.stations)if(st.index!=node)S.state.physical.results[st.index]=99;
            foreach(var item in S.home.GetComponentsInChildren<PhysicalGrabbable>(true).OrderBy(i=>i.kind==PhysicalItemKind.Bag?0:1))if(item.kind==PhysicalItemKind.Bag||SibucaoInventory.Storable(item))S.inventory.Collect(item);
            yield return null;var flood=S.GetComponent<FloodSimulationManager>();while(flood.IsCatchingUp)yield return null;
            yield return new WaitForSeconds(.5f);
            if(wet)Check(B1.HasVisibleWater,"Wet B1 fixture finished without visible water.");
        }
        IEnumerator Durations()
        {
            int[] nodes={0,0,2,3,5,6,8,8},choices={1,2,1,2,2,1,1,2};
            var old=SibucaoBranchPlayProbe.Get();typeof(SibucaoBranchPlayProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(old,input);
            for(int i=0;i<nodes.Length;i++)
            {
                yield return Fixture(nodes[i],nodes[i]==0);var b=S.physicalDecisions.stations[nodes[i]].branches[choices[i]];
                status="Fixture B"+(nodes[i]+1)+" C"+choices[i];Save();
                yield return Invoke(old,"Exercise",b);
                Check(b.Attempt.activeSeconds>=b.Choice.timeCost,"Resolved before active minimum");
                Record("PASS B"+(nodes[i]+1)+" C"+choices[i]+" active="+b.Attempt.activeSeconds.ToString("F2")+"s minimum="+b.Choice.timeCost+"s, E + idle did not resolve, actual traversal recorded once.");
            }
        }
        IEnumerator CancelAll()
        {
            for(int n=0;n<9;n++)
            {
                yield return Fixture(n,n==0);var st=S.physicalDecisions.stations[n];var b=n==0?st.branches[1]:st.branches.First(x=>x.Available);
                S.player.Teleport(b.path[0].position+Vector3.up*.1f,st.transform.forward);yield return new WaitForSeconds(.4f);
                Check(st.Active&&b.Begin(true),"Cancellation fixture could not engage B"+(n+1));
                float idleStart=b.Attempt.activeSeconds;yield return new WaitForSeconds(1);
                Check(b.Engaged&&!st.Resolved&&b.Attempt.activeSeconds==idleStart,"Idle input counted as active work");
                SibucaoEditorInput.Cancel=true;yield return null;yield return new WaitForSeconds(.35f);
                Check(!st.EngagedBranch&&!st.Resolved&&S.player.ActivePhysicalOption==null,"Q did not release B"+(n+1));
                float t=b.Attempt.activeSeconds;SibucaoEditorInput.Move=Vector2.up;yield return new WaitForSeconds(.12f);SibucaoEditorInput.Move=Vector2.zero;
                Check(!st.EngagedBranch&&b.Attempt.activeSeconds==t,"Automatic re-engagement immediately after Q");
                Check(st.NavigationTarget==st.transform.position,"Objective still follows cancelled branch");
                Check(S.state.physical.history.Count==0,"Cancellation scored a choice");
                Record("PASS B"+(n+1)+" Q cancels, no immediate relatch, no result, objective returns to situation (engagement fixture).");
            }
        }
        IEnumerator ProbeFixture()
        {
            yield return Fixture(0,true);var b=B1.branches[0];
            S.player.Teleport(b.tool.transform.position-B1.transform.forward*1.2f+Vector3.up*.12f,B1.transform.forward);yield return new WaitForSeconds(.35f);
            yield return ProbeAndCancel();
        }
        IEnumerator ProbeAndCancel()
        {
            var b=B1.branches[0];var legacy=SibucaoFirstCrossingProbe.Get();
            typeof(SibucaoFirstCrossingProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(legacy,input);
            yield return Invoke(legacy,"GripVisibleTool",b.tool);
            float engageUntil=Time.realtimeSinceStartup+1.5f;
            while(!b.Engaged&&Time.realtimeSinceStartup<engageUntil)yield return null;
            Check(b.Engaged&&b.Prompt!=null&&b.Prompt.Contains("two wet spots"),"Held probe prompt missing; engaged="+b.Engaged+" prompt="+b.Prompt+" active="+B1.Active);
            Record("PASS held stick retains two-location guidance; B1 has visible water.");
            for(int sample=0;sample<2;sample++)
            {
                status="Probing real wet location "+(sample+1);Save();
                yield return input.Aim(B1.transform.TransformPoint(-2.5f,-.1f,sample*1.4f));
                yield return input.MoveTo(B1.transform.TransformPoint(-2.5f,0,sample*1.4f-1),20);
                yield return input.Aim(B1.transform.TransformPoint(-2.5f,-.1f,sample*1.4f));
                float settle=Time.time+5;while(B1.Attempt.probeSamples<sample+1&&Time.time<settle)yield return null;
            }
            var a=B1.Attempt;
            Check(a.probeSamples==2,"Need two real readings; got "+a.probeSamples+" tip="+b.tool.tip.position);
            var flood=S.GetComponent<FloodSimulationManager>();
            Check(a.firstProbeDepth>.05f&&a.secondProbeDepth>.05f&&SibucaoPhysicalOption.FlatDistance(a.firstProbe,a.secondProbe)>=.65f,"Invalid depth samples");
            Record("PASS two real depths "+a.firstProbeDepth.ToString("F3")+"m at "+a.firstProbe+" and "+a.secondProbeDepth.ToString("F3")+"m at "+a.secondProbe+". Reaction: "+b.tool.GetComponent<SibucaoDepthProbe>().LastReaction);
            SibucaoEditorInput.Cancel=true;yield return null;yield return new WaitForSeconds(.3f);
            Check(!S.player.grabber.held&&!B1.EngagedBranch&&!B1.Resolved&&B1.Attempt.probeSamples==0,"Q did not release/reset probe");
            SibucaoEditorInput.GripHeld=false;Record("PASS Q released physical stick, cleared readings and branch lock without recording a choice.");
        }
        IEnumerator CompleteProbeFixture()
        {
            yield return Fixture(0,true);var b=B1.branches[0];var legacy=SibucaoFirstCrossingProbe.Get();
            typeof(SibucaoFirstCrossingProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(legacy,input);
            S.player.Teleport(b.tool.transform.position-B1.transform.forward*1.2f+Vector3.up*.12f,B1.transform.forward);yield return new WaitForSeconds(.35f);
            yield return Invoke(legacy,"GripVisibleTool",b.tool);
            float engageUntil=Time.realtimeSinceStartup+1.5f;while(!b.Engaged&&Time.realtimeSinceStartup<engageUntil)yield return null;
            Check(b.Engaged,"Probe route did not engage after gripping the stick");
            for(int sample=0;sample<2;sample++)
            {
                var wet=B1.transform.TransformPoint(-2.5f,-.1f,sample*1.4f);
                yield return input.Aim(wet);yield return input.MoveTo(B1.transform.TransformPoint(-2.5f,0,sample*1.4f-1),20);yield return input.Aim(wet);
                float settle=Time.time+5;while(B1.Attempt.probeSamples<sample+1&&Time.time<settle)yield return null;
            }
            Check(B1.Attempt.probeSamples==2,"Probe route needs two real wet readings");
            yield return input.MoveTo(b.path[0].position,25);
            foreach(var point in b.path.Skip(1))yield return input.MoveTo(point.position,25);
            yield return input.MoveTo(b.path.Last().position+B1.transform.forward*1.5f,20);
            Check(B1.Resolved&&S.state.physical.results[0]==0&&S.state.physical.history.Count(x=>x.node==0)==1,"Probe route did not complete exactly once");
            SibucaoEditorInput.GripHeld=false;Record("PASS B1 depth route sampled two real wet spots, crossed the raised boards, resolved once, and released the route state.");
        }
        IEnumerator Fresh(int choice)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;if(freshSeed>=0)UnityEngine.Random.InitState(freshSeed);StartStormRun();
            int home=S.state.homeIndex;float started=Time.realtimeSinceStartup;
            status="Fresh home "+home+": wake / TV skip / stand";Save();
            yield return new WaitForSeconds(7);S.home.SkipTV();
            while(S.home.MovementLocked)yield return null;
            var h=S.home;var door=h.doorPanel.position;door.y=S.player.transform.position.y;
            yield return input.MoveTo(door+h.transform.forward*1.3f,20);yield return input.Aim(h.doorPanel.position);
            SibucaoEditorInput.Interact=true;yield return null;yield return new WaitForSeconds(1);
            Check(S.state.homeDoorOpen,"E failed to open home door");
            yield return input.MoveTo(door-h.transform.forward*1.25f,20);
            var legacy=SibucaoFirstCrossingProbe.Get();typeof(SibucaoFirstCrossingProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(legacy,input);
            yield return Invoke(legacy,"WalkToRoad");
            float start=S.route.Progress(S.player.transform.position),end=B1.routeDistance-6;
            for(float d=start;d<end;d+=3)yield return input.MoveTo(S.route.Sample(d),15);
            var road=B1.branches[1];yield return input.MoveTo(road.path[0].position-B1.transform.forward*2,15);
            Check(!B1.Active&&!B1.HasVisibleWater&&!B1.Resolved,"Dry first arrival activated station");
            Check(!S.missionGuide.Objective.Contains("flooded"),"Dry ground described as flooded");
            Record("PASS fresh home "+home+": woke, stood, E opened door, walked to B1 without teleport/time override; dry arrival inactive. Objective: "+S.missionGuide.Objective);
            status="Fresh home "+home+": waiting for natural flood arrival";Save();
            while(!B1.Active&&Time.realtimeSinceStartup-started<480){Check(!B1.Resolved,"Waiting resolved B1");yield return null;}
            Check(B1.Active&&B1.HasVisibleWater,"Flood did not visibly arrive/activate within 8 min");
            Record("PASS fresh home "+home+": B1 activated only with visible water at storm clock "+S.state.homeSeconds.ToString("F1")+"s.");
            if(choice==2)
            {
                yield return input.MoveTo(B1.branches[0].tool.transform.position-B1.transform.forward*1.2f,20);
                yield return ProbeAndCancel();
            }
            var b=B1.branches[choice];
            yield return input.MoveTo(road.path[0].position-B1.transform.forward*2,20);
            yield return input.MoveTo(b.path[0].position-B1.transform.forward*2,20);
            SibucaoEditorInput.Sprint=choice==2;
            foreach(var point in b.path)yield return input.MoveTo(point.position,25);
            yield return input.MoveTo(b.path.Last().position+B1.transform.forward*1.5f,20);
            Check(B1.Resolved&&S.state.physical.results[0]==choice,"Fresh crossing did not resolve intended branch");
            Check(b.Attempt.activeSeconds>=b.Choice.timeCost&&!B1.EngagedBranch&&float.IsPositiveInfinity(b.SpeedLimit),"Minimum/exit movement failed");
            Check(S.state.physical.history.Count(x=>x.node==0)==1,"Duplicate result");
            Record("PASS fresh home "+home+" C"+choice+" completed after "+b.Attempt.activeSeconds.ToString("F2")+"s active movement; task and movement cap released; wall duration "+(Time.realtimeSinceStartup-started).ToString("F1")+"s.");
        }
        IEnumerator Edges()
        {
            var legacy=SibucaoFirstCrossingProbe.Get();typeof(SibucaoFirstCrossingProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(legacy,input);
            for(int home=0;home<S.homes.Length;home++)
            {
                yield return Fixture(0,false,home);var b=B1.branches[0];var tool=b.tool;var rest=tool.transform.position;
                yield return new WaitForSeconds(2);
                Check(tool.Body.isKinematic&&Vector3.Distance(rest,tool.transform.position)<.001f,"Unheld stick drifted/fell at home "+home);
                S.player.Teleport(tool.transform.position-B1.transform.forward*1.2f+Vector3.up*.1f,B1.transform.forward);yield return new WaitForSeconds(.35f);
                yield return Invoke(legacy,"GripVisibleTool",tool);
                Check(tool.IsHeld&&!tool.Body.isKinematic&&!B1.Active,"Dry stick grip/activation incorrect");
                if(home==2)
                {
                    S.state.homeSeconds=SibucaoStormTimeline.FloodStart(S.home)+70;
                    yield return null;while(S.GetComponent<FloodSimulationManager>().IsCatchingUp)yield return null;
                    yield return new WaitForSeconds(.5f);
                    Check(B1.Active&&b.Engaged&&b.Prompt.Contains("two wet spots"),"Stick held before water did not engage when water arrived");
                    Record("PASS stick held on dry ground begins correct branch when water arrives, without re-gripping (clock fixture).");
                    SibucaoEditorInput.Cancel=true;yield return null;yield return null;
                    Check(!tool.IsHeld&&!B1.EngagedBranch,"Q did not release pre-held stick");
                }
                SibucaoEditorInput.GripHeld=false;yield return new WaitForSeconds(.3f);
                Record("PASS home "+home+" initial stick stays supported, is reachable, and becomes physical only on actual grip.");
            }
            yield return Fixture(8);var sprint=S.physicalDecisions.stations[8].branches[2];
            S.player.Teleport(sprint.path[0].position+Vector3.up*.1f,sprint.station.transform.forward);yield return new WaitForSeconds(.3f);
            SibucaoEditorInput.Move=Vector2.up;yield return new WaitForSeconds(.2f);SibucaoEditorInput.Move=Vector2.zero;
            Check(!sprint.Engaged,"Sprint lane engaged without Shift");Record("PASS B9 C2 does not engage on ordinary walking input.");
        }
        IEnumerator Contrast()
        {
            yield return Fixture(0,true);S.state.homeSeconds=SibucaoStormTimeline.FloodStart(S.home)+110;
            yield return null;while(S.GetComponent<FloodSimulationManager>().IsCatchingUp)yield return null;
            var b=B1.branches[0];var legacy=SibucaoFirstCrossingProbe.Get();typeof(SibucaoFirstCrossingProbe).GetField("input",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(legacy,input);
            S.player.Teleport(b.tool.transform.position-B1.transform.forward*1.2f+Vector3.up*.1f,B1.transform.forward);yield return new WaitForSeconds(.35f);
            yield return Invoke(legacy,"GripVisibleTool",b.tool);
            yield return input.MoveTo(B1.transform.TransformPoint(0,0,-4),20);
            yield return input.Aim(B1.transform.TransformPoint(0,-.1f,-3));yield return new WaitForSeconds(1.2f);
            yield return input.MoveTo(B1.transform.TransformPoint(-3.3f,0,2),20);
            yield return input.Aim(B1.transform.TransformPoint(-3.3f,-.1f,3));yield return new WaitForSeconds(1.2f);
            var a=B1.Attempt;var probe=b.tool.GetComponent<SibucaoDepthProbe>();
            Check(a.probeSamples==2,"Contrasting locations did not sample twice");
            Record("DEPTH comparison: "+a.firstProbeDepth.ToString("F3")+"m at "+a.firstProbe+" -> "+a.secondProbeDepth.ToString("F3")+"m at "+a.secondProbe+"; "+probe.LastReaction);
            Check(Mathf.Abs(a.firstProbeDepth-a.secondProbeDepth)>.08f&&probe.LastReaction!="The water is about the same depth here.","Chosen locations too similar to verify contrasting feedback");
            Record("PASS contrasting real locations changed the diegetic depth reaction and stick mark.");
            yield return input.MoveTo(b.path[0].position,20);
            foreach(var point in b.path)yield return input.MoveTo(point.position,20);
            yield return input.MoveTo(b.path.Last().position+B1.transform.forward,15);
            Check(B1.Resolved&&S.state.physical.results[0]==0,"Probe plus physical traversal did not complete C0");Record("PASS actual C0 grip, depth samples and traversal completed exactly one choice.");
        }
    }
}
#endif
