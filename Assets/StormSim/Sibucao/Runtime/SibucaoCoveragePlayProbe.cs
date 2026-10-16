#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    public sealed class SibucaoCoveragePlayProbe:MonoBehaviour
    {
        public string status="Idle";public List<string> evidence=new List<string>();
        SibucaoSession S=>SibucaoSession.Instance;
        SibucaoPlaythroughProbe input;
        public static SibucaoCoveragePlayProbe Get(){var p=FindFirstObjectByType<SibucaoCoveragePlayProbe>();return p?p:new GameObject("EDITOR ONLY route-coverage acceptance").AddComponent<SibucaoCoveragePlayProbe>();}
        string testLog="physical-gate-tests.txt";
        public void Run(int home=-1,int choice=-1){StopAllCoroutines();testLog=home<0?"physical-gate-tests.txt":"physical-gate-smoke.txt";StartCoroutine(Suite(home,choice));}
        public void RunJourney(int home=3){StopAllCoroutines();input=SibucaoPlaythroughProbe.Get();StartCoroutine(JourneyGuard(home));}
        [Serializable] sealed class Paths{public HomePath[] homes;}
        [Serializable] sealed class HomePath{public Vector3[] bypassPath;}
        IEnumerator JourneyGuard(int h)
        {
            var homes=S.homes;var stack=new Stack<IEnumerator>();stack.Push(Journey(h,homes));Exception error=null;
            try{while(stack.Count>0){object value=null;bool next=false;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;}}
            finally{S.homes=homes;SibucaoEditorInput.Reset();status=error==null?"PASS continuous home "+(h+1)+" to school":"FAIL continuous home "+(h+1)+": "+error.Message;File.AppendAllText("Documentation/RouteCoverage/continuous-runs.txt",status+"\n"+JsonUtility.ToJson(S.state.physical,true)+"\n");}
        }
        IEnumerator Journey(int h,SibucaoHomeOpening[] homes)
        {
            Application.runInBackground=true;SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.homes=new[]{homes[h]};S.StartWithRole("Student");S.state.homeIndex=h;S.physicalDecisions.ResetRun();S.state.physical.seed=23421;
            S.home.SkipTV();while(S.home.MovementLocked)yield return null;
            var st=S.physicalDecisions.stations.Single(x=>x.departureEncounter&&x.homeMask==(1<<h));
            status="Continuous home "+(h+1)+": opening door";
            yield return input.MoveTo(st.transform.TransformPoint(0,0,-1.1f),20);yield return input.Aim(S.home.doorPanel.GetComponent<Collider>().bounds.center);yield return E();yield return new WaitForSeconds(1);
            Check(S.state.homeDoorOpen,"Door did not open");yield return SibucaoBranchPlayProbe.Get().ExerciseExisting(st.branches[1],false);while(S.Mode==SibucaoMode.Cinematic)yield return null;
            var points=JsonUtility.FromJson<Paths>(File.ReadAllText("Documentation/RouteCoverage/envelope-baseline.json")).homes[h].bypassPath;
            // Skip the old witness's interior corners after leaving the new exit encounter.
            int nearest=Enumerable.Range(0,points.Length).Where(i=>{var p=S.home.transform.InverseTransformPoint(points[i]);return Mathf.Abs(p.x)>4.35f||p.z< -3.85f||p.z>3.85f;}).OrderBy(i=>Vector3.Distance(points[i],S.player.transform.position)).First();
            for(int i=Mathf.Max(1,nearest);i<points.Length&&S.Mode==SibucaoMode.Playing;i++)
            {
                status="Continuous home "+(h+1)+": graph waypoint "+i+" / "+points.Length;
                SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.UseHeld=SibucaoEditorInput.Ascend=false;SibucaoEditorInput.Sprint=true;
                yield return input.Aim(points[i]+Vector3.up*1.5f);
                // NavMesh corners can be hundreds of metres apart on open ground.
                // A fixed 20 second timeout falsely reported ordinary travel as a collision.
                float budget=Mathf.Max(20,Vector3.Distance(points[i],S.player.transform.position)/Mathf.Max(.5f,S.player.walkSpeed*.25f)+10);
                yield return input.MoveTo(points[i],budget);
            }
            if(S.Mode==SibucaoMode.Playing)yield return input.MoveTo(S.physicalDecisions.evacuationCenter.arrivalPoint.position,25);
            Check(S.Mode==SibucaoMode.ArrivalReview,"Did not enter the dry school arrival trigger");
            Check(S.state.physical.history.Any(x=>x.node==st.index&&x.choice==1&&x.distance>1),"No physical departure action in history");
            ScreenCapture.CaptureScreenshot("Documentation/RouteCoverage/continuous-home"+(h+1)+"-arrival.png");yield return new WaitForEndOfFrame();
        }
        void Save(){Directory.CreateDirectory("Documentation/RouteCoverage");File.WriteAllLines("Documentation/RouteCoverage/"+testLog,evidence);}
        IEnumerator Suite(int selectedHome,int selectedChoice)
        {
            var homes=S.homes;input=SibucaoPlaythroughProbe.Get();evidence.Clear();Application.runInBackground=true;
            try{for(int h=0;h<4;h++)for(int c=0;c<2;c++)
            {
                if(selectedHome>=0&&h!=selectedHome||selectedChoice>=0&&c!=selectedChoice)continue;
                status="Home "+(h+1)+" choice "+c;var stack=new Stack<IEnumerator>();stack.Push(Case(h,c,homes));Exception error=null;
                while(stack.Count>0){object value=null;bool next=false;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;}
                evidence.Add((error==null?"PASS":"FAIL")+" Home "+(h+1)+" choice "+c+(error==null?" physical work + crossing completed":" — "+error.Message));Save();SibucaoEditorInput.Reset();
            }}finally{S.homes=homes;SibucaoEditorInput.Reset();int expected=(selectedHome<0?4:1)*(selectedChoice<0?2:1);status="Finished "+evidence.Count(x=>x.StartsWith("PASS Home")&&x.EndsWith("physical work + crossing completed"))+" / "+expected+" passed";Save();}
        }
        static void Check(bool okay,string message){if(!okay)throw new Exception(message);}
        IEnumerator E(){SibucaoEditorInput.Interact=true;yield return null;yield return null;yield return new WaitForFixedUpdate();}
        IEnumerator Case(int h,int c,SibucaoHomeOpening[] homes)
        {
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.homes=new[]{homes[h]};S.StartWithRole("Student");S.state.homeIndex=h;S.physicalDecisions.ResetRun();S.state.physical.seed=23421;S.home.SkipTV();while(S.home.MovementLocked)yield return null;
            var st=S.physicalDecisions.stations.Single(x=>x.departureEncounter&&x.homeMask==(1<<h));
            // Isolated geometry fixtures: other decisions cannot take the controls. The
            // tested gate is never resolved directly; all work uses production input.
            foreach(var other in S.physicalDecisions.stations)if(other!=st)S.state.physical.results[other.index]=99;
            var gate=st.GetComponent<SibucaoCoverageGate>();var p=gate.debris;
            S.player.Teleport(st.transform.TransformPoint(0,0,-1.1f),st.transform.forward);yield return null;
            yield return input.Aim(S.home.doorPanel.GetComponent<Collider>().bounds.center);yield return E();yield return new WaitForSeconds(1);
            Check(S.state.homeDoorOpen,"E could not open home door");Check(st.Active,"Departure encounter inactive");
            foreach(float x in new[]{-.4f,0,.48f})
            {
                S.player.Teleport(st.transform.TransformPoint(x,0,-.65f),st.transform.forward);yield return null;
                SibucaoEditorInput.Move=Vector2.up;SibucaoEditorInput.Sprint=true;SibucaoEditorInput.Jump=true;
                yield return new WaitForSeconds(1.1f);SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.Sprint=false;
                Check(st.transform.InverseTransformPoint(S.player.transform.position).z<.40f,"Walk/sprint/jump bypass at x="+x);
            }
            evidence.Add("PASS Home "+(h+1)+" choice "+c+" unaided movement blocked at left, centre and gap");
            S.player.Teleport(st.transform.TransformPoint(c==0?-.2f:.48f,0,-.9f),st.transform.forward);yield return null;
            if(c==0)
            {
                for(int part=0;part<2;part++){yield return input.Aim(p.fasteners[part].position);yield return E();}
                Check(p.State.fasteners==3,"Both lashings were not released");yield return input.Aim(p.grip.transform.position);yield return E();Check(S.passages.Held==p,"Lifting grip not acquired");
                SibucaoEditorInput.UseHeld=true;yield return input.MoveTo(st.transform.TransformPoint(1.0f,0,-3.7f),18);SibucaoEditorInput.UseHeld=false;
                Check(p.State.opening>=.98f,"Awning did not move with player: "+p.State.opening);yield return E();Check(p.State.secured,"Awning not secured");
                yield return input.MoveTo(st.branches[0].path[0].position,18);
            }
            yield return SibucaoBranchPlayProbe.Get().ExerciseExisting(st.branches[c],false);
            Check(st.Resolved&&S.state.physical.results[st.index]==c,"Departure choice failed to record");
            evidence.Add("OBSERVED Home "+(h+1)+" choice "+c+" history "+JsonUtility.ToJson(S.state.physical.history.Last()));
            while(S.Mode==SibucaoMode.Cinematic)yield return null;
            ScreenCapture.CaptureScreenshot("Documentation/RouteCoverage/home"+(h+1)+"-choice"+c+".png");yield return new WaitForEndOfFrame();
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
