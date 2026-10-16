using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed partial class PhysicalDecisionStation
    {
        public DecisionNode node;
        public SibucaoPhysicalOption[] branches;
        public AudioSource approachSource;
        public Transform preCueProp;
        public PhysicalGrabbable fallbackBag;
        float lastApproachTime;Quaternion cueRest;bool cueCached;
        public SibucaoPhysicalOption EngagedBranch
        {
            get {if(branches==null)return null;foreach(var b in branches)if(b&&b.Engaged)return b;return null;}
        }
        public string BranchPrompt
        {
            get {if(branches==null)return null;var current=EngagedBranch;if(current&&!string.IsNullOrEmpty(current.Prompt))return current.Prompt;foreach(var b in branches)if(b&&!string.IsNullOrEmpty(b.Prompt))return b.Prompt;return null;}
        }
        public void ResetBranches(){if(branches!=null)foreach(var b in branches)if(b)b.ResetBinding();}
        public void TickBranches(float dt)
        {
            if(!node)return;var s=SibucaoSession.Instance;if(approachSource)approachSource.volume=SibucaoAudioMix.Speech;
            // A prior branch leaves a real route marker in the next encounter.  It is
            // restored from run flags as well as shown during a continuous playthrough.
            if(routeMark&&manager&&manager.Data.flags.Contains("visible_consequence:"+index))routeMark.SetActive(true);
            float distance=SibucaoPhysicalOption.FlatDistance(s.player.transform.position,transform.position);
            string flag="buildup_"+index;
            if(!Resolved&&(index!=0||HasVisibleWater)&&distance<node.approachDistance&&!manager.Data.flags.Contains(flag))
            {
                manager.SetFlag(flag);lastApproachTime=Time.time;
                if(approachSource&&node.approachAudio&&Time.unscaledTime>=s.subtitleUntil){approachSource.clip=node.approachAudio;approachSource.Play();if(!string.IsNullOrEmpty(node.approachLine))s.Say("Nearby resident: "+node.approachLine,node.approachAudio.length+.5f);}
            }
            if(preCueProp)
            {
                if(!cueCached){cueRest=preCueProp.localRotation;cueCached=true;}
                preCueProp.localRotation=cueRest*Quaternion.Euler(0,0,Mathf.Sin(Time.time*3.1f+index)*Mathf.Lerp(2,9,s.home?s.home.Severity:0));
            }
            if(branches!=null)foreach(var branch in branches)if(branch)branch.Tick(dt);
            if(index==4&&Active&&brokenBag&&branches!=null&&branches.Length>0)
            {
                var work=branches[0].workSockets;if(work!=null)for(int i=0;i<work.Length;i++)if(work[i])work[i].SetPositionAndRotation(brokenBag.transform.TransformPoint(new Vector3((i-1)*.25f,.32f,0)),brokenBag.transform.rotation);
            }
        }
        public void ShowPhysicalResolution(bool success,PhysicalConsequence effect)
        {
            var s=SibucaoSession.Instance;
            if(index==4&&manager.Branch(index).selected==2)
            {
                foreach(var item in spill.GetComponentsInChildren<PhysicalGrabbable>(true))if(SibucaoInventory.Storable(item))
                {manager.SetFlag("lost:"+item.persistentId);item.gameObject.SetActive(false);}
                if(brokenBag){manager.SetFlag("lost:"+brokenBag.persistentId);brokenBag.gameObject.SetActive(false);if(manager.Data.backpackId==brokenBag.persistentId)manager.Data.backpackId=null;}
            }
            // Factual world reactions follow the event. No score text, green light, success chime or best-route cue.
            if(assist&&!resolutionBeat)
            {
                if((effect&PhysicalConsequence.LeaveResident)!=0)s.Say("I'm still here!",5);
                else if(assist.delivered)s.Say(success?"I can stay here for now.":"I need a moment. My leg hurts.",5);
            }
            if(index==6&&!resolutionBeat&&manager.Branch(index).selected==1)s.Say("This boat is going to the other landing.",6);
        }
    }
}
