using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StormSim.Sibucao
{
    public enum ResidentState { Calm, Alert, Panic, ReactionToPlayer }
    /// <summary>Only assigned, appropriate clips are played. Missing reactions remain explicit.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class SibucaoActor : MonoBehaviour
    {
        public string role;
        public Animator animator;
        public AnimationClip idle,walk,alert_PLACEHOLDER,panic_PLACEHOLDER,reaction_PLACEHOLDER;
        [Tooltip("Enable only when the assigned Panic clip is actual locomotion, with its measured speed below.")]
        public bool panicIsLocomotion;
        public float panicStrideSpeed;
        [Tooltip("Measured from the retargeted stance foot, at animation speed 1.")]
        public float strideSpeed=1;
        public ResidentState state;
        public bool seatedPose,injuredPose;
        public string missingAnimations="Alert, Panic/run, Reaction, seated/assisted and injured clips are not supplied.";
        public float MoveSpeed {get;private set;}
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idlePlayer,walkPlayer;
        readonly AnimationClipPlayable[] reactions=new AnimationClipPlayable[3];
        readonly float[] blendWeights=new float[5];
        int previousState=-1;
        Vector3 previous;
        float reactionUntil;
        SibucaoResident resident;
        public bool Swimming=>resident&&resident.Swimming;
        public bool MissingCurrentAnimation=>state!=ResidentState.Calm&&!ClipForState(state)&&(!animator||!animator.isHuman);
        public bool UsingPanicLocomotion=>state==ResidentState.Panic&&panicIsLocomotion&&panic_PLACEHOLDER&&panicStrideSpeed>.05f;
        void OnEnable(){previous=transform.position;if(graph.IsValid())graph.Play();}
        void OnDisable(){if(graph.IsValid())graph.Stop();}
        void Start()
        {
            resident=GetComponent<SibucaoResident>();
            if(!animator||!animator.avatar||!animator.avatar.isValid||!idle||!walk)return;
            animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Sibucao real Idle / Walk");
            mixer=AnimationMixerPlayable.Create(graph,5);
            idlePlayer=AnimationClipPlayable.Create(graph,idle);walkPlayer=AnimationClipPlayable.Create(graph,walk);
            idlePlayer.SetApplyFootIK(true);walkPlayer.SetApplyFootIK(true);walkPlayer.SetApplyPlayableIK(true);
            graph.Connect(idlePlayer,0,mixer,0);graph.Connect(walkPlayer,0,mixer,1);
            for(int i=0;i<3;i++)
            {
                var clip=ClipForState((ResidentState)(i+1));if(!clip)continue;
                reactions[i]=AnimationClipPlayable.Create(graph,clip);reactions[i].SetApplyFootIK(true);reactions[i].SetApplyPlayableIK(true);
                graph.Connect(reactions[i],0,mixer,i+2);
            }
            // Different residents must not march with identical animation phase.
            float offset=resident?Mathf.Repeat(resident.variation*.371f,1):.17f;
            idlePlayer.SetTime(idle.length*offset);walkPlayer.SetTime(walk.length*offset);
            for(int i=0;i<3;i++)if(reactions[i].IsValid())reactions[i].SetTime(ClipForState((ResidentState)(i+1)).length*offset);
            blendWeights[0]=1;
            AnimationPlayableOutput.Create(graph,"Humanoid",animator).SetSourcePlayable(mixer);graph.Play();
        }
        public void React(){reactionUntil=Time.time+3;}
        public AnimationClip ClipForState(ResidentState value)=>value==ResidentState.Alert?alert_PLACEHOLDER:value==ResidentState.Panic?panic_PLACEHOLDER:value==ResidentState.ReactionToPlayer?reaction_PLACEHOLDER:idle;
        public static ResidentState StateForContext(bool react,bool nearFlood,bool finalStretch,bool stormCue,WaterTier tier)
            =>react?ResidentState.ReactionToPlayer:nearFlood||finalStretch||tier>=WaterTier.Tier4?ResidentState.Panic:stormCue||tier>=WaterTier.Tier2?ResidentState.Alert:ResidentState.Calm;
        public void SetMove(float speed){MoveSpeed=Mathf.Max(0,speed);}
        void Update()
        {
            var s=SibucaoSession.Instance;
            bool playing=s&&s.Mode==SibucaoMode.Playing;
            float actual=Time.deltaTime>0?Vector3.Distance(transform.position,previous)/Time.deltaTime:0;previous=transform.position;
            MoveSpeed=playing&&!seatedPose&&!injuredPose&&actual<8?actual:0;
            var flood=s?s.GetComponent<FloodSimulationManager>():null;
            var tier=flood?flood.GetWaterTier(transform.position):WaterTier.Tier1;
            bool near=playing&&(s.player.transform.position-transform.position).sqrMagnitude<6.25f;
            bool nearFlood=playing&&flood&&flood.GetWaterDepth(transform.position+transform.forward*1.2f)>.5f;
            bool finalStretch=playing&&s.home&&s.home.Severity>.4f&&s.route.Progress(transform.position)>s.route.length*.88f;
            bool stormCue=playing&&(s.home&&s.home.Severity>0||s.physicalDecisions&&s.physicalDecisions.Beat>0);
            bool urgency=resident&&!resident.Sheltered&&(resident.behavior==ResidentBehavior.Running||resident.behavior==ResidentBehavior.Panicking);
            // A passing player must not make a fleeing resident drop into an unavailable gesture.
            state=StateForContext(MoveSpeed<.06f&&(near||Time.time<reactionUntil),nearFlood||urgency,finalStretch,stormCue,tier);
            if(!graph.IsValid())return;
            int reactionIndex=(int)state-1;
            bool assigned=reactionIndex>=0&&reactions[reactionIndex].IsValid();
            // Stationary reactions never replace a moving walk with a sliding stationary pose.
            bool useReaction=assigned&&!seatedPose&&!injuredPose&&!Swimming&&(MoveSpeed<=.06f||UsingPanicLocomotion);
            int desiredInput=useReaction?reactionIndex+2:MoveSpeed>.06f&&!Swimming?1:0;
            if(useReaction)
            {
                if(previousState!=(int)state)reactions[reactionIndex].SetTime(0);
                reactions[reactionIndex].SetSpeed(!playing?0:UsingPanicLocomotion?MoveSpeed/Mathf.Max(.1f,panicStrideSpeed):1);
            }
            float blend=1-Mathf.Exp(-Time.deltaTime/ .16f);
            for(int i=0;i<5;i++)
            {
                blendWeights[i]=Mathf.Lerp(blendWeights[i],i==desiredInput?1:0,blend);
                mixer.SetInputWeight(i,blendWeights[i]);
            }
            previousState=(int)state;
            idlePlayer.SetSpeed(playing?1:0);walkPlayer.SetSpeed(playing?MoveSpeed/Mathf.Max(.1f,strideSpeed):0);
        }
        void LateUpdate()
        {
            if(!animator||!animator.isHuman)return;
            if(Swimming){SwimPose();return;}
            if(injuredPose){AimArm(false,transform.forward*.28f-transform.up*.65f+transform.right*.3f);return;}
            if(MoveSpeed<.06f&&!ClipForState(state)&&state!=ResidentState.Calm)ProceduralReaction();
            // Static staging poses, explicitly not substitutes for missing assist/reaction animations.
            if(seatedPose)
            {
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);if(hips)animator.transform.localPosition+=Vector3.up*(.74f-transform.InverseTransformPoint(hips.position).y);
                SitLeg(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot);
                SitLeg(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot);
                // Rest the hands beside the seat instead of leaving the standing idle swing.
                AimArm(true,transform.forward*.2f-transform.up*.7f+transform.right*.16f);
                AimArm(false,transform.forward*.2f-transform.up*.7f-transform.right*.16f);
            }
        }
        void SitLeg(HumanBodyBones upperBone,HumanBodyBones lowerBone,HumanBodyBones footBone)
        {
            var upper=animator.GetBoneTransform(upperBone);var lower=animator.GetBoneTransform(lowerBone);var foot=animator.GetBoneTransform(footBone);
            if(!upper||!lower||!foot)return;
            var footRotation=foot.rotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,transform.forward)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,-transform.up)*lower.rotation;
            foot.rotation=footRotation;
        }
        void ProceduralReaction()
        {
            var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing)return;
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            if(head)
            {
                Vector3 toward=s.player.view.transform.position-head.position;
                if(toward.sqrMagnitude>0.001f)
                {
                    var wanted=Quaternion.LookRotation(toward,transform.up);
                    head.rotation=Quaternion.RotateTowards(head.rotation,wanted,32);
                    if(state==ResidentState.ReactionToPlayer)head.localRotation*=Quaternion.Euler(Mathf.Sin(Time.time*4)*5,0,0);
                }
            }
            if(state==ResidentState.ReactionToPlayer)
            {
                var target=s.route.Sample(Mathf.Min(s.route.length,s.route.Progress(transform.position)+8))-transform.position;
                target.y=.25f;AimArm(true,target.normalized);
            }
            else AimArm(true,transform.forward*.4f+transform.up*(state==ResidentState.Panic?.8f:.25f));
        }
        void AimArm(bool right,Vector3 direction)
        {
            var upper=animator.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
            var lower=animator.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
            var hand=animator.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
            if(!upper||!lower||!hand)return;
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation,.65f);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation,.6f);
        }
        void SwimPose()
        {
            // Explicitly procedural treading/breaststroke, not a sourced mocap clip.
            float clock=Time.time*5;
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1,sweep=(Mathf.Sin(clock)+1)*.5f;
                var upper=animator.GetBoneTransform(i==0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                var lower=animator.GetBoneTransform(i==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var hand=animator.GetBoneTransform(i==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                if(upper&&lower&&hand){var direction=transform.forward*(1-sweep)+transform.right*side*(.2f+sweep)-transform.up*.15f;upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,transform.forward-transform.up*.35f)*lower.rotation;}
                var hip=animator.GetBoneTransform(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);if(hip)hip.localRotation*=Quaternion.Euler(15+Mathf.Sin(clock+(i==0?0:Mathf.PI))*20,0,side*8);
            }
        }
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
