using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StormSim.Preparedness
{
    public sealed class PreparednessNeighbor : MonoBehaviour
    {
        public string residentName,activity,need;
        public Animator animator;public AnimationClip idle;
        public bool seated,sweeping;
        public Transform broom;
        PlayableGraph graph;Vector3 modelRest;float attentiveUntil;
        void Start()
        {
            if(!animator)return;modelRest=animator.transform.localPosition;animator.applyRootMotion=false;
            if(idle){animator.runtimeAnimatorController=null;graph=PlayableGraph.Create("Preparation household idle");var clip=AnimationClipPlayable.Create(graph,idle);clip.SetTime(Random.value*idle.length);AnimationPlayableOutput.Create(graph,"Resident",animator).SetSourcePlayable(clip);graph.Play();}
        }
        public void Talk(PreparednessSession s)
        {
            var item=GetComponent<PreparednessItem>();
            if(!s.OutdoorSafe){s.Say("Do not delay departure for a visit. Report unconfirmed households to a route volunteer or school reception.");return;}
            if(!s.advisoryRead){s.Say("Check the TV advisory before sharing the warning with your neighbor.");return;}
            attentiveUntil=Time.time+8;
            if(item.cells==0){item.cells=1;item.prepared=true;s.Say("You: A strong typhoon is coming. Please prepare and monitor the official advisories.\n"+residentName+": Understood. I will tell my household. WARNING CONFIRMED. Speak again to ask about our needs.");}
            else if(item.cells==1){item.cells=2;s.Say(residentName+": "+need+" Speak again to confirm our evacuation meeting plan.");}
            else {item.cells=3;item.prepared=true;s.Say(residentName+": We will follow local evacuation instructions and use the marked school route. Report our needs to school reception if we are separated. Our arrival is not yet confirmed.");}
        }
        void LateUpdate()
        {
            if(!animator||!animator.isHuman)return;
            if(seated)
            {
                animator.transform.localPosition=modelRest;
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);if(hips)animator.transform.position+=Vector3.up*(transform.position.y+.49f-hips.position.y);
                Leg(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot);
                Leg(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot);
            }
            if(sweeping&&Time.time>attentiveUntil)
            {
                var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                if(upper&&lower&&hand){var grip=transform.position+transform.forward*.6f+Vector3.up*.9f+transform.right*(.12f+Mathf.Sin(Time.time*1.6f)*.18f);upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,grip-upper.position)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,grip-lower.position)*lower.rotation;}
                if(broom){broom.position=transform.position+transform.forward*.6f+transform.right*(.12f+Mathf.Sin(Time.time*1.6f)*.18f)-Vector3.up*.18f;broom.rotation=transform.rotation*Quaternion.Euler(0,0,Mathf.Sin(Time.time*1.6f)*8);}
            }
        }
        void Leg(HumanBodyBones a,HumanBodyBones b,HumanBodyBones c)
        {var upper=animator.GetBoneTransform(a);var lower=animator.GetBoneTransform(b);var foot=animator.GetBoneTransform(c);if(!upper||!lower||!foot)return;var r=foot.rotation;upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,transform.forward)*upper.rotation;lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,-transform.up)*lower.rotation;foot.rotation=r;}
        void OnEnable(){if(graph.IsValid())graph.Play();}
        void OnDisable(){if(graph.IsValid())graph.Stop();}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
