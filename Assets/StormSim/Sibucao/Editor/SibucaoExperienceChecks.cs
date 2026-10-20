using System;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoExperienceChecks
    {
        [MenuItem("StormSim/Validation/Check evacuation experience")]
        public static void Run()
        {
            var state=new RunState();
            state.experience.departedAt=70;state.experience.drainOpenedAt=100;
            state.experience.familyDistance=240;state.experience.measuredDepths[1]=.42f;
            state.experience.moments.Add(new EvacuationMoment{id="drain-open",action="Opened drain",result="Water recedes",lesson="Earlier work changes later conditions",time=100});
            var copy=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));
            Require(copy.experience.moments.Count==1&&copy.experience.moments[0].id=="drain-open","Causal timeline survives saving");
            Require(copy.experience.drainOpenedAt==100&&copy.experience.familyDistance==240&&Mathf.Approximately(copy.experience.measuredDepths[1],.42f),"Drain, family and measurements survive saving");
            state.experience.assessmentSnapshot=JsonUtility.ToJson(state);
            var checkpoint=JsonUtility.FromJson<RunState>(state.experience.assessmentSnapshot);
            Require(string.IsNullOrEmpty(checkpoint.experience.assessmentSnapshot),"Replay snapshot contains no recursive snapshot");
            Require(SibucaoEvacuationExperience.SpecialistLimit==3&&SibucaoEvacuationExperience.IsSpecialist("first-aid")&&!SibucaoEvacuationExperience.IsSpecialist("travel-water")&&!SibucaoEvacuationExperience.IsSpecialist("medicine"),"Specialist tradeoff leaves essentials available");
            Require(Mathf.Approximately(SibucaoEvacuationExperience.ReliefAt(Vector3.zero,Vector3.zero,.32f),.32f),"Drain relief is strongest at the blockage");
            Require(Mathf.Approximately(SibucaoEvacuationExperience.ReliefAt(new Vector3(12,0,0),Vector3.zero,.32f),.16f),"Drain relief falls off locally");
            Require(SibucaoEvacuationExperience.ReliefAt(new Vector3(25,0,0),Vector3.zero,.32f)==0&&SibucaoEvacuationExperience.ReliefAt(Vector3.zero,Vector3.zero,0)==0,"Unrelated areas and unopened drains remain unaffected");
            var legacy=JsonUtility.FromJson<RunState>("{\"version\":1,\"physical\":{\"health\":75}}");
            Require(legacy!=null&&legacy.physical.health==75,"Older run data remains readable");
            Require(SibucaoTyphoonExposure.GustEnvelope(5)<SibucaoTyphoonExposure.GustEnvelope(18),"Gust buildup follows a lull");
            Require(SibucaoTyphoonExposure.GustEnvelope(22)>SibucaoTyphoonExposure.GustEnvelope(29),"Gust eases after its peak");
            Require(Mathf.Approximately(SibucaoTyphoonExposure.GustEnvelope(10),SibucaoTyphoonExposure.GustEnvelope(42)),"Saved-clock cycle is deterministic");
            Require(SibucaoTyphoonExposure.HeadingResistance(Vector3.left,Vector3.right)==1,"Headwind opposes movement");
            Require(SibucaoTyphoonExposure.HeadingResistance(Vector3.right,Vector3.right)==0,"Tailwind is not a headwind");
            Require(SibucaoTyphoonExposure.HeadingResistance(Vector3.zero,Vector3.right)==0,"Standing still has no heading resistance");
            state.experience.escortPositionSaved=true;state.experience.escortPosition=new Vector3(1,2,3);
            state.experience.dynamicAnchorSaved=true;state.experience.dynamicAnchor=new Vector3(4,5,6);
            copy=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));
            Require(copy.experience.escortPositionSaved&&copy.experience.escortPosition==state.experience.escortPosition,"Escort location survives serialization");
            Require(copy.experience.dynamicAnchorSaved&&copy.experience.dynamicAnchor==state.experience.dynamicAnchor,"Optional encounter anchor survives serialization");
            state.experience.familyPositionSaved=true;state.experience.familyCartPosition=new Vector3(9,2,7);
            state.experience.assistanceRequested=true;state.experience.assistanceWait=7;state.experience.assistanceOrigin=new Vector3(3,2,1);
            copy=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));
            Require(copy.experience.familyPositionSaved&&copy.experience.familyCartPosition==state.experience.familyCartPosition,"Family world position survives serialization");
            Require(copy.experience.assistanceRequested&&copy.experience.assistanceWait==7&&copy.experience.assistanceOrigin==state.experience.assistanceOrigin,"Pending assistance survives serialization");
            state.floodClockOffset=72;state.experience.familyTrail.Add(new Vector3(1,0,4));state.experience.escortTrail.Add(new Vector3(2,0,5));
            copy=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));
            Require(copy.floodClockOffset==72&&legacy.floodClockOffset==0,"Early-departure offset persists without changing legacy storm age");
            Require(copy.experience.familyTrail.Count==1&&copy.experience.escortTrail[0]==state.experience.escortTrail[0],"Companion trails survive serialization");
            Debug.Log("Evacuation experience: 20 persistence, storm-direction, replay, equipment and local-drainage checks passed.");
        }
        static void Require(bool passed,string message){if(!passed)throw new InvalidOperationException("Evacuation experience check failed: "+message);}
    }
}
