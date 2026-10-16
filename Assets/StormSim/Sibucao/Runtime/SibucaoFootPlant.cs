using UnityEngine;
namespace StormSim.Sibucao
{
    /// <summary>Post-retarget stance-foot correction; the supplied Walk clip still drives the pose.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class SibucaoFootPlant : MonoBehaviour
    {
        public SibucaoActor actor;
        public float soleHeight=.08f;
        Animator animator;
        public int lockedSamples;
        public float maxPlantError;
        readonly Vector3[] plants=new Vector3[2];
        readonly Vector3[] previousLocal=new Vector3[2];
        readonly bool[] locked=new bool[2];
        void Awake(){animator=GetComponent<Animator>();}
        void OnAnimatorIK(int layer)
        {
            if(!actor||!animator||!animator.isHuman||actor.Swimming)return;
            for(int i=0;i<2;i++)
            {
                var bone=animator.GetBoneTransform(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var local=actor.transform.InverseTransformPoint(bone.position);
                bool stance=!actor.seatedPose&&!actor.injuredPose&&actor.MoveSpeed>.05f&&local.y<soleHeight+.025f&&local.z<=previousLocal[i].z+.001f;
                previousLocal[i]=local;
                if(!stance){locked[i]=false;continue;}
                if(!locked[i]){plants[i]=bone.position;locked[i]=true;}
                if(Vector3.Distance(plants[i],bone.position)>.22f)locked[i]=false;
            }
        }
        void LateUpdate()
        {
            if(!animator||!actor||!animator.isHuman)return;
            if(actor.Swimming||actor.seatedPose||actor.injuredPose||actor.MoveSpeed<=.05f){locked[0]=locked[1]=false;return;}
            for(int i=0;i<2;i++)if(locked[i])
            {
                var hip=animator.GetBoneTransform(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var knee=animator.GetBoneTransform(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=animator.GetBoneTransform(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                Vector3 delta=plants[i]-hip.position;
                float upper=Vector3.Distance(hip.position,knee.position),lower=Vector3.Distance(knee.position,foot.position);
                // Release rather than overstretch the leg when terrain/turning makes the target unreachable.
                if(delta.magnitude>=upper+lower-.002f){locked[i]=false;continue;}
                Vector3 axis=delta.normalized;
                Vector3 bend=Vector3.ProjectOnPlane(knee.position-hip.position,axis).normalized;
                if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(actor.transform.forward,axis).normalized;
                float d=Mathf.Max(.001f,delta.magnitude);
                float x=(upper*upper-lower*lower+d*d)/(2*d);
                Vector3 targetKnee=hip.position+axis*x+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-x*x));
                var footRotation=foot.rotation;
                hip.rotation=Quaternion.FromToRotation(knee.position-hip.position,targetKnee-hip.position)*hip.rotation;
                knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,plants[i]-knee.position)*knee.rotation;
                foot.rotation=footRotation;
                maxPlantError=Mathf.Max(maxPlantError,Vector3.Distance(foot.position,plants[i]));lockedSamples++;
            }
        }
    }
}
