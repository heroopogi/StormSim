using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Procedural upper-body reactions layered after an NPC's idle/walk clip.</summary>
    [DefaultExecutionOrder(320)]
    public sealed class SibucaoIncidentParticipantAnimator : MonoBehaviour
    {
        public enum Pose { None, Point, Call, Push, Brace, Receive, Injured, Assist }

        public Animator animator;
        Pose requested;
        Transform target;
        float progress;
        int requestedFrame=-10;

        public Pose CurrentPose=>Time.frameCount-requestedFrame<=2?requested:Pose.None;

        public void Drive(Pose pose,Transform toward,float normalizedProgress)
        {
            requested=pose;target=toward;progress=Mathf.Clamp01(normalizedProgress);requestedFrame=Time.frameCount;
        }

        void LateUpdate()
        {
            if(!animator||!animator.isActiveAndEnabled||!animator.isHuman||Time.frameCount-requestedFrame>2||requested==Pose.None)return;
            float pulse=.5f+.5f*Mathf.Sin(Time.time*(requested==Pose.Push||requested==Pose.Brace?9:5));
            Vector3 toTarget=target?target.position-transform.position:transform.forward;
            toTarget.y=Mathf.Clamp(toTarget.y,-.35f,.8f);
            if(toTarget.sqrMagnitude<.001f)toTarget=transform.forward;
            Vector3 forward=toTarget.normalized;

            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            if(head&&target)
            {
                Vector3 look=target.position-head.position;
                if(look.sqrMagnitude>.001f)head.rotation=Quaternion.Slerp(head.rotation,Quaternion.LookRotation(look,transform.up),.42f);
            }

            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            if(spine&&(requested==Pose.Push||requested==Pose.Brace||requested==Pose.Assist))
                spine.localRotation*=Quaternion.Euler(Mathf.Lerp(2,13,pulse),0,0);

            switch(requested)
            {
                case Pose.Point:
                    AimArm(true,forward+transform.up*.12f,.78f);
                    break;
                case Pose.Call:
                    HandToHead(true,new Vector3(.12f,.02f,.12f));
                    AimArm(false,forward+transform.up*.15f,.55f);
                    break;
                case Pose.Push:
                    AimArm(true,forward-transform.right*.12f-transform.up*.08f,.82f);
                    AimArm(false,forward+transform.right*.12f-transform.up*.08f,.82f);
                    break;
                case Pose.Brace:
                    AimArm(true,forward-transform.right*.18f-transform.up*.2f,.86f);
                    AimArm(false,forward+transform.right*.18f-transform.up*.2f,.86f);
                    break;
                case Pose.Receive:
                    AimArm(true,forward+transform.up*(.04f+pulse*.08f),.72f);
                    AimArm(false,forward+transform.up*(.04f+pulse*.08f),.68f);
                    break;
                case Pose.Injured:
                    HandToLeg(false,HumanBodyBones.LeftLowerLeg);
                    AimArm(true,forward+transform.up*.22f,.48f);
                    break;
                case Pose.Assist:
                    AimArm(true,forward-transform.up*.12f,.84f);
                    AimArm(false,forward+transform.right*.15f-transform.up*.25f,.72f);
                    break;
            }
        }

        void AimArm(bool right,Vector3 direction,float weight)
        {
            var upper=animator.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
            var lower=animator.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
            var hand=animator.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
            if(!upper||!lower||!hand)return;
            direction.Normalize();
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation,weight);
            Vector3 lowerDirection=(direction+Vector3.down*.08f).normalized;
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,lowerDirection)*lower.rotation,weight*.86f);
        }

        void HandToHead(bool right,Vector3 offset)
        {
            var head=animator.GetBoneTransform(HumanBodyBones.Head);if(!head)return;
            Vector3 handTarget=head.TransformPoint(offset);
            Vector3 direction=handTarget-(animator.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm)?.position??transform.position);
            AimArm(right,direction,.86f);
        }

        void HandToLeg(bool right,HumanBodyBones legBone)
        {
            var leg=animator.GetBoneTransform(legBone);var upper=animator.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
            if(!leg||!upper)return;AimArm(right,leg.position-upper.position,.88f);
        }
    }
}
