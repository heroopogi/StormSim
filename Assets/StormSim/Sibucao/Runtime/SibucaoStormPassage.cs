using System;
using UnityEngine;
namespace StormSim.Sibucao
{
    public enum StormPassageKind { SwingFence, PushCart, TiltRack }
    [Serializable] public sealed class StormPassageState
    {
        public string id; public int fasteners; public float opening,workSeconds,distance,startedAt=-1;
        public bool secured,crossed; public float approachSide;
    }
    public sealed class SibucaoStormPassage:MonoBehaviour
    {
        public string persistentId,description;
        public int home,zone;
        public StormPassageKind kind;
        public float width=2.4f;
        public Transform moving;
        public Transform[] fasteners;
        public SibucaoPassageHandle grip;
        public bool core;
        public bool canOperate=true;
        public Vector3 approachLocal=new Vector3(0,.1f,-.75f);
        Vector3 previousPlayer,gripStart;
        float openingAtGrip;
        StormPassageState cachedState;
        PhysicalRunData cachedRun;
        SibucaoSession S=>SibucaoSession.Instance;
        public StormPassageState State
        {
            get
            {
                var data=S.state.physical;
                if(ReferenceEquals(data,cachedRun)&&cachedState!=null)return cachedState;
                if(data.passages==null)data.passages=new System.Collections.Generic.List<StormPassageState>();
                var state=data.passages.Find(x=>x.id==persistentId);
                if(state==null){state=new StormPassageState{id=persistentId};data.passages.Add(state);}cachedRun=data;cachedState=state;return state;
            }
        }
        public string Prompt(int part)
        {
            if(!canOperate)return "Debris is wedged against the building";
            if(State.secured)return "Passage open — move through";
            if(part<2)return (State.fasteners&(1<<part))!=0?"Fastener released":"E: Release "+(kind==StormPassageKind.PushCart?"wheel brake":"tied fastener");
            if(State.fasteners!=3)return "Release the fasteners on both sides";
            if(S.passages.Held==this)return State.opening>=.98f?"E: Secure in place":"Hold E + move: "+(kind==StormPassageKind.SwingFence?"back up to swing the fence":kind==StormPassageKind.PushCart?"push the cart sideways":"back up to lift the debris");
            return State.opening>=.98f?"E: Secure in place":"E: Take the handle";
        }
        public bool Interact(int part)
        {
            var state=State;if(state.secured||!canOperate)return false;
            if(part<2){if(state.startedAt<0)state.startedAt=S.state.homeSeconds;state.fasteners|=1<<part;Apply();S.player.Gesture(1);return true;}
            if(state.fasteners!=3)return true;
            if(S.player.grabber&&S.player.grabber.held){S.Say("I need both hands free to move this.",3);return true;}
            if(state.opening>=.98f)
            {
                state.secured=true;state.opening=1;S.passages.Held=null;SibucaoControls.ResetGrip();Apply();
                S.physicalDecisions.SetFlag("passage_open:"+persistentId);
                S.Say(kind==StormPassageKind.PushCart?"The cart is out of the passage.":"It's secured out of the way.",3);return true;
            }
            S.passages.Held=this;SibucaoControls.BeginGrip();previousPlayer=gripStart=S.player.transform.position;openingAtGrip=state.opening;
            // Record the working side now, not a side visited before interaction.
            state.approachSide=Mathf.Sign(transform.InverseTransformPoint(gripStart).z);return true;
        }
        public void Restore(){previousPlayer=S.player.transform.position;Apply();}
        public void Apply()
        {
            if(!S)return;var state=State;
            for(int i=0;i<fasteners.Length;i++)if(fasteners[i])fasteners[i].localRotation=Quaternion.Euler((state.fasteners&(1<<i))!=0?85:0,0,0);
            if(!moving)return;
            if(kind==StormPassageKind.SwingFence){moving.localPosition=new Vector3(-width*.5f,0,0);moving.localRotation=Quaternion.Euler(0,state.opening*100,0);}
            else if(kind==StormPassageKind.PushCart){moving.localPosition=new Vector3(width+1.1f,0,2.2f)*state.opening;moving.localRotation=Quaternion.identity;}
            else {moving.localPosition=new Vector3(0,2.25f,0);moving.localRotation=Quaternion.Euler(-state.opening*100,0,0);}
        }
        public void Tick(float dt)
        {
            if(!canOperate)return;
            var state=State;var local=transform.InverseTransformPoint(S.player.transform.position);
            if(!state.crossed&&Mathf.Abs(local.x)<width*.5f+.1f)
            {
                if(state.approachSide==0&&Mathf.Abs(local.z)>1&&Mathf.Abs(local.z)<5)state.approachSide=Mathf.Sign(local.z);
                if(state.secured&&state.approachSide!=0&&local.z*state.approachSide<-.85f){state.crossed=true;S.passages.Crossed(this);}
            }
            if(S.passages.Held!=this)return;
            var delta=S.player.transform.position-previousPlayer;previousPlayer=S.player.transform.position;
            if(delta.magnitude>1||delta.sqrMagnitude<.000001f||!S.player.UseHeld||S.player.MoveInput.sqrMagnitude<.01f)return;
            state.workSeconds+=dt;state.distance+=delta.magnitude;
            float target;
            if(kind==StormPassageKind.SwingFence)
            {
                var fromHinge=local-new Vector3(-width*.5f,0,0);fromHinge.y=0;
                float pull=-transform.InverseTransformVector(S.player.transform.position-gripStart).z/2.5f;
                target=Mathf.Max(Mathf.Clamp01(Vector3.SignedAngle(Vector3.right,fromHinge,Vector3.up)/80),openingAtGrip+pull);
            }
            else {var travelled=transform.InverseTransformVector(S.player.transform.position-gripStart);target=openingAtGrip+(kind==StormPassageKind.PushCart?travelled.x/(width+1.1f):-travelled.z/2.5f);}
            // Physical displacement, not an elapsed timer, drives the obstruction.
            state.opening=Mathf.Clamp01(Mathf.MoveTowards(state.opening,target,delta.magnitude/Mathf.Max(1,width)));Apply();
        }
    }
}
