#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Continuous-input acceptance attempts. No fixture teleports or direct decision completion.
    public sealed class SibucaoBranchingRouteProbe:MonoBehaviour
    {
        public string status="Idle";public List<string> evidence=new List<string>();
        SibucaoSession S=>SibucaoSession.Instance;
        void Awake(){if(File.Exists("Documentation/BranchingEvaluation/continuous-route-attempts.txt"))evidence.AddRange(File.ReadAllLines("Documentation/BranchingEvaluation/continuous-route-attempts.txt"));}
        public static SibucaoBranchingRouteProbe Get(){var p=FindFirstObjectByType<SibucaoBranchingRouteProbe>();return p?p:new GameObject("EDITOR ONLY continuous branching run").AddComponent<SibucaoBranchingRouteProbe>();}
        public void Run(){StartCoroutine(Suite());}
        void Save(){Directory.CreateDirectory("Documentation/BranchingEvaluation");File.WriteAllLines("Documentation/BranchingEvaluation/continuous-route-attempts.txt",evidence);}
        IEnumerator Suite()
        {
            var originalHomes=S.homes;int evidenceStart=evidence.Count;
            try
            {
                for(int scenario=0;scenario<3;scenario++)
                {
                    var stack=new Stack<IEnumerator>();stack.Push(Journey(scenario,originalHomes));Exception failure=null;
                    while(stack.Count>0)
                    {
                        object value=null;bool next=false;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){failure=e;break;}
                        if(!next){stack.Pop();continue;}if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
                    }
                    evidence.Add((failure==null?"PASS":"FAIL")+" continuous "+new[]{"delayed injury","resident-left","boat manifest"}[scenario]+(failure==null?" reached school and reported consequences":" — "+failure.Message));
                    File.WriteAllText("Documentation/BranchingEvaluation/continuous-state-"+scenario+".json",JsonUtility.ToJson(S.state.physical,true));
                    SibucaoEditorInput.Reset();Save();
                }
            }
            finally{S.homes=originalHomes;SibucaoEditorInput.Reset();status="Finished: "+evidence.Skip(evidenceStart).Count(x=>x.StartsWith("PASS"))+" passed / "+evidence.Skip(evidenceStart).Count(x=>x.StartsWith("FAIL"))+" failed this suite; earlier attempts retained";Save();}
        }
        IEnumerator Journey(int scenario,SibucaoHomeOpening[] homes)
        {
            var input=SibucaoPlaythroughProbe.Get();var branches=SibucaoBranchPlayProbe.Get();
            S.homes=new[]{homes[3]};SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;Application.runInBackground=true;
            S.StartWithRole(new[]{"Student","Parent","Volunteer"}[scenario]);S.state.homeIndex=3;S.physicalDecisions.ResetRun();S.state.physical.seed=scenario==0?10:23421;
            evidence.Add("BEGIN "+scenario+": home4, role="+S.state.physical.role+", seed="+S.state.physical.seed+". Production new-run/TV skip; continuous movement; no clock writes.");
            S.home.SkipTV();while(S.home.MovementLocked)yield return null;
            var door=S.home.doorPanel.GetComponent<Collider>().bounds.center;var outward=door-S.player.transform.position;outward.y=0;outward.Normalize();
            status="Scenario "+scenario+": leaving home";
            yield return input.MoveTo(door-outward*1.25f,20);yield return input.Aim(door);SibucaoEditorInput.Interact=true;yield return null;yield return new WaitForSeconds(1.2f);
            if(!S.state.homeDoorOpen)throw new Exception("Door did not open from E input");
            yield return input.MoveTo(door+outward*3,20);
            int[] choices={1,1,1,scenario==0?2:0,2,2,scenario==2?1:0,2,0};
            for(int n=0;n<9;n++)
            {
                var st=S.physicalDecisions.stations[n];status="Scenario "+scenario+": approaching B"+(n+1);
                float progress=S.route.Progress(S.player.transform.position);
                for(float d=progress+2;d<st.routeDistance-5;d+=2)
                {
                    if(S.player.ActivePhysicalOption&&S.player.ActivePhysicalOption.station!=st){SibucaoEditorInput.Cancel=true;yield return null;}
                    yield return WalkConnection(st,S.route.Sample(d),input);
                }
                yield return branches.ExerciseExisting(st.branches[choices[n]],false);
                evidence.Add("Scenario "+scenario+" B"+(n+1)+" actual choice="+S.state.physical.results[n]+" at "+S.state.homeSeconds.ToString("F1")+"s");Save();
                SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.UseHeld=SibucaoEditorInput.GripHeld=SibucaoEditorInput.Sprint=SibucaoEditorInput.Ascend=false;
                while(S.Mode==SibucaoMode.Cinematic)yield return null;
            }
            for(float d=S.route.Progress(S.player.transform.position)+2;d<S.route.length&&S.Mode==SibucaoMode.Playing;d+=2)yield return input.MoveTo(S.route.Sample(d),14);
            if(S.Mode==SibucaoMode.Playing)yield return input.MoveTo(S.physicalDecisions.evacuationCenter.arrivalPoint.position,20);
            if(S.Mode!=SibucaoMode.ArrivalReview)throw new Exception("Arrival review did not appear");
            var notices=S.state.physical.consequenceNotices;
            if(scenario==0&&!notices.Exists(n=>n.id.StartsWith("swelling_")))throw new Exception("Injury scenario did not produce delayed swelling");
            if(scenario==1&&(!notices.Exists(n=>n.id=="resident_left_5")||!notices.Exists(n=>n.id=="resident_left_7")))throw new Exception("Both resident notices not present");
            if(scenario==2&&!notices.Exists(n=>n.id=="boat_manifest"))throw new Exception("Boat manifest notice not present");
            evidence.Add("OBSERVED "+scenario+": "+string.Join(" | ",notices.Select(n=>n.id+": "+n.message)));
            ScreenCapture.CaptureScreenshot("Documentation/BranchingEvaluation/continuous-arrival-"+scenario+".png");yield return new WaitForEndOfFrame();
            S.physicalDecisions.ConfirmArrivalReport();if(!S.state.physical.boundaryReached)throw new Exception("Arrival report failed to complete");
        }
        IEnumerator WalkConnection(PhysicalDecisionStation station,Vector3 destination,SibucaoPlaythroughProbe input)
        {
            // Read-only existing ground/capsule planner. Navigate around actual buildings.
            // Reflection avoids a runtime-assembly reference to the Editor assembly.
            var plannerType=Type.GetType("StormSim.Sibucao.Editor.SibucaoBranchPathBake, Assembly-CSharp-Editor");
            var planner=Activator.CreateInstance(plannerType,station,S.GetComponent<FloodSimulationManager>().terrain,false);
            var anchors=new[]{station.transform.InverseTransformPoint(S.player.transform.position),station.transform.InverseTransformPoint(destination)};
            var points=(Vector3[])plannerType.GetMethod("Route").Invoke(planner,new object[]{anchors});
            foreach(var point in points)yield return input.MoveTo(station.transform.TransformPoint(point),14);
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
