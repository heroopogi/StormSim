using UnityEngine;
namespace StormSim.Sibucao
{
    public enum PhysicalItemKind { Object, Stick, Strap, Bag, Supply, FirstAid, Assist }
    [RequireComponent(typeof(Rigidbody)),DisallowMultipleComponent]
    public sealed class PhysicalGrabbable : MonoBehaviour
    {
        public string persistentId;
        public PhysicalItemKind kind;
        public int supplyBit=-1;
        public Transform grip,tip;
        public PhysicalDecisionStation station;
        public bool everHeld,secured,delivered;
        public bool routinePickup;
        public string inventoryTag;
        [System.NonSerialized] public bool preparedForUse;
        [System.NonSerialized] public float gripTravel;
        public bool PhysicalTool=>kind==PhysicalItemKind.Stick||kind==PhysicalItemKind.Strap||kind==PhysicalItemKind.Assist||kind==PhysicalItemKind.FirstAid&&preparedForUse;
        public Rigidbody Body=>GetComponent<Rigidbody>();
        public bool IsHeld=>holder;
        [System.NonSerialized] public PhysicalGrabber holder;
        public bool CanGrab=>(!secured||kind==PhysicalItemKind.Supply||kind==PhysicalItemKind.FirstAid)&&!delivered&&gameObject.activeInHierarchy;
        public void EnsureAssistanceGrip()
        {
            if(kind!=PhysicalItemKind.Assist||grip)return;
            var actor=GetComponentInChildren<SibucaoActor>(true);
            if(!actor||!actor.injuredPose||!actor.animator||!actor.animator.isHuman)return;
            var support=actor.animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if(!support)support=actor.animator.GetBoneTransform(HumanBodyBones.Chest);
            if(!support)support=actor.animator.GetBoneTransform(HumanBodyBones.Hips);
            if(!support)return;
            // Fix support to the body at its current upper-torso pose, not the arbitrary ray hit.
            grip=new GameObject("Resident torso support grip").transform;
            grip.SetParent(transform,false);grip.position=support.position;
        }
        public void SetDelivered(){delivered=true;if(holder)holder.Release();Body.isKinematic=true;}
    }
}
