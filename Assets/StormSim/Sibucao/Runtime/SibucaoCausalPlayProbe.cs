#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    // Production movement-input negative regression; teleports are explicit setup/bypass fixtures.
    public sealed class SibucaoCausalPlayProbe:MonoBehaviour
    {
        public string status="Idle";
        public void Run(){StartCoroutine(Guard(CheckCrossing()));}
        void Require(bool value,string message){if(!value)throw new Exception(message);}
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object next=null;bool more=false;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){status="FAIL: "+error.Message;break;}
                if(!more){stack.Pop();continue;}if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
            SibucaoEditorInput.Reset();Directory.CreateDirectory("Reports");File.WriteAllText("Reports/DuringPhysicalCrossing.txt",status);Debug.Log(status);
        }
        IEnumerator CheckCrossing()
        {
            status="Running physical unfinished-work guard";Application.runInBackground=true;
            var s=SibucaoSession.Instance;var m=s.physicalDecisions;var st=m.stations[0];
            s.story.ResetPlayback();s.home.Begin();s.state.homeSeconds=90;s.home.Restore();m.ResetRun();s.SetMode(SibucaoMode.Playing);
            SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;
            var branch=st.branches[1];
            s.player.Teleport(branch.path[0].position-st.transform.forward*3+Vector3.up*.12f,st.transform.forward);
            yield return new WaitForSeconds(.5f);
            Require(!m.FinishPhysicalChoice(st,1),"Unfinished crossing resolved");
            var start=s.player.transform.position;
            SibucaoEditorInput.Move=Vector2.up;
            yield return new WaitForSeconds(5);
            SibucaoEditorInput.Move=Vector2.zero;
            Require(Vector3.Distance(start,s.player.transform.position)>.1f,"Production input did not move the player");
            Require(branch.HasEncounter&&!branch.EncounterComplete,"Fixture does not contain unfinished physical work");
            Require(m.Data.results[0]==-1&&m.Data.causal.path==""&&m.Data.history.Count==0,"Walking past unfinished work committed a decision");
            // An endpoint teleport is deliberately a negative fixture, never a completed action.
            s.player.Teleport(branch.path[branch.path.Length-1].position+st.transform.forward,st.transform.forward);
            yield return new WaitForSeconds(.5f);
            Require(!st.Resolved&&!m.FinishPhysicalChoice(st,1),"Endpoint bypass earned a choice");
            m.stations[1].Activate();Require(!m.stations[1].Active,"B2 opened before B1 work was complete");
            Require(s.GetComponent<SibucaoCrestBridge>().IsRendering,"Crest bridge inactive");
            status="PASS: production movement input cannot finish B1 with uncleared timber; endpoint bypass records no choice/history; B2 stays locked; Crest remains active. This checks unfinished-work guards, not a completed physical crossing or full B1-B9 playthrough.";
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
    }
}
#endif
