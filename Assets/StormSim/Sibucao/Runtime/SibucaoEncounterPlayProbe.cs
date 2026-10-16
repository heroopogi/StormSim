#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Work-area positioning is a fixture. Grip, drag and release use production input.
    public sealed class SibucaoEncounterPlayProbe:MonoBehaviour
    {
        public string status="Idle";
        public float activeSeconds,wallSeconds;
        public int stationIndex=0,choiceIndex=1;
        SibucaoSession S=>SibucaoSession.Instance;
        public void Run(){StartCoroutine(Guard(Check()));}
        IEnumerator Guard(IEnumerator work)
        {
            while(true)
            {
                object value=null;bool more;
                try{more=work.MoveNext();if(more)value=work.Current;}
                catch(Exception e){status="FAIL: "+e.Message;Debug.LogError(status);SibucaoEditorInput.Reset();yield break;}
                if(!more)break;yield return value;
            }
        }
        void Require(bool value,string message){if(!value)throw new Exception(message);}
        void Aim(Vector3 at)
        {
            var q=Quaternion.LookRotation(at-S.player.view.transform.position);
            SibucaoEditorInput.Look=new Vector2(Mathf.DeltaAngle(S.player.transform.eulerAngles.y,q.eulerAngles.y),
                Mathf.DeltaAngle(0,S.player.view.transform.localEulerAngles.x)-Mathf.DeltaAngle(0,q.eulerAngles.x));
        }
        IEnumerator Check()
        {
            Application.runInBackground=true;SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;S.StartRun();
            var bridge=S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();if(bridge&&bridge.Active){bridge.preparation.departure="temporary-skip";bridge.preparation.SetPhase(StormSim.Preparedness.PrepPhase.ReadyForStorm);bridge.BeginStorm();}if(S.home)S.home.SkipTV();
            if(S.home)S.home.Suspend();S.home=null;S.state.homeSequence=false;
            foreach(var st in S.physicalDecisions.stations)if(st.index!=stationIndex)S.state.physical.results[st.index]=99;
            var b=S.physicalDecisions.stations[stationIndex].branches[choiceIndex];
            var enclosure=b.station.GetComponent<SibucaoCarEnclosure>();
            var entryPosition=enclosure?enclosure.WorkPoint(0):b.path[0].position;
            S.player.Teleport(entryPosition-b.station.transform.forward*1.5f+Vector3.up*.1f,b.station.transform.forward);
            b.station.Activate();Require(b.Begin(true),"Begin failed");var r=b.Encounter;yield return null;
            Require(r.Parts.Count==r.Definition.OperationCount&&r.Parts.Count==3,"Expected three meaningful physical actions");
            Require(r.GetComponentsInChildren<TextMesh>().Length==0&&r.GetComponentsInChildren<LineRenderer>().Length==0,"External puzzle UI remains");
            float started=Time.realtimeSinceStartup;
            for(int index=0;index<r.Definition.OperationCount;index++)
            {
                var part=r.Parts[index];status="Physical part "+index;
                if(index==r.Definition.PhaseOffset(r.Progress.phase))
                {
                    var direction=b.path.Length>1?(b.path[1].position-b.path[0].position).normalized:b.station.transform.forward;
                    var pos=part.transform.parent.position-direction*1.5f+Vector3.up*.1f;
                    S.player.Teleport(pos,direction);yield return null;
                }
                for(int i=0;i<10;i++){Aim(part.GetComponent<Collider>().bounds.center+Vector3.up*.055f);yield return null;}
                Require(PhysicalEncounterRunner.FocusedPart()==part,"Ray cannot reach part "+index);
                SibucaoEditorInput.Interact=SibucaoEditorInput.UseHeld=true;yield return null;yield return null;
                Require(r.Holding,"Grip failed "+index);
                if(index==0)
                {
                    float before=r.Progress.activeSeconds;float end=Time.realtimeSinceStartup+1;
                    while(Time.realtimeSinceStartup<end)yield return null;
                    Require(r.Progress.activeSeconds==before,"Static grip credited work");
                }
                float deadline=Time.realtimeSinceStartup+30;
                while(r.Progress.displacement[index]<1&&Time.realtimeSinceStartup<deadline)
                {
                    var direction=part.PullDirection;var view=S.player.view.transform;
                    var drag=new Vector2(Vector3.Dot(direction,view.right),Vector3.Dot(direction,view.up));
                    SibucaoEditorInput.Look=(drag.sqrMagnitude<.08f?Vector2.up:drag.normalized)*3;
                    var approach=part.transform.position-S.player.transform.position;approach.y=0;
                    SibucaoEditorInput.Move=Vector3.Distance(view.position,part.transform.position)>2.5f?
                        new Vector2(Vector3.Dot(approach.normalized,S.player.transform.right),Vector3.Dot(approach.normalized,S.player.transform.forward))*.4f:Vector2.zero;
                    activeSeconds=r.Progress.activeSeconds;wallSeconds=Time.realtimeSinceStartup-started;
                    Require(!b.station.Resolved,"Premature resolution");yield return null;
                }
                Require(r.Progress.displacement[index]>=1,"Object blocked "+index);
                SibucaoEditorInput.Move=Vector2.zero;SibucaoEditorInput.UseHeld=false;yield return null;yield return null;
                Require(!r.Holding,"Grip did not release");
            }
            Require(r.Complete&&wallSeconds>=18&&r.Progress.activeSeconds>=r.Definition.MinimumSeconds,"Floor or physical work failed");
            status="PASS: three distinct real colliders moved, released and retained; "+r.Progress.activeSeconds+" seconds physical work; no puzzle UI or idle credit.";
            Debug.Log(status);SibucaoEditorInput.Reset();
        }
    }
}
#endif
