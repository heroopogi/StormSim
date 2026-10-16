using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class PhysicalAssistPose : MonoBehaviour
    {
        public SibucaoResident actor;
        public PhysicalGrabbable body;
        public bool wheelchair;
        Transform[] wheels;Vector3 previous;
        void Start()
        {
            previous=transform.position;
            var found=new System.Collections.Generic.List<Transform>();
            if(wheelchair)foreach(var child in GetComponentsInChildren<Transform>())if(child.name=="Wheelchair wheel")found.Add(child);
            wheels=found.ToArray();
            if(body&&!body.grip)body.grip=transform.Find("Grab handle");
        }
        void LateUpdate()
        {
            float travel=Vector3.Dot(transform.position-previous,transform.forward);previous=transform.position;
            if(wheelchair&&wheels!=null&&Mathf.Abs(travel)<1)foreach(var wheel in wheels)if(wheel)wheel.Rotate(Vector3.up,travel/.36f*Mathf.Rad2Deg,Space.Self);
            if(!wheelchair||!actor||actor.casting)return;
            float effort=body.delivered?0:Mathf.Sin(Time.time*3)*.07f;
            actor.pelvis.localPosition=new Vector3(0,.72f,0);
            actor.chest.localRotation=Quaternion.Euler(8+effort*40,0,0);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                actor.thighs[i].localPosition=new Vector3(side*.1f,.72f,0);actor.thighs[i].localRotation=Quaternion.Euler(-90,0,0);
                actor.shins[i].localPosition=new Vector3(side*.1f,.72f,.38f);actor.shins[i].localRotation=Quaternion.identity;
                actor.feet[i].localPosition=new Vector3(side*.1f,.34f,.38f);
                Vector3 shoulder=new Vector3(side*.2f,1.18f,0),elbow=new Vector3(side*.3f,.95f,.12f+effort),hand=new Vector3(side*.36f,.7f,effort);
                actor.upperArms[i].localPosition=shoulder;actor.upperArms[i].localRotation=Quaternion.FromToRotation(Vector3.down,elbow-shoulder);
                actor.forearms[i].localPosition=elbow;actor.forearms[i].localRotation=Quaternion.FromToRotation(Vector3.down,hand-elbow);actor.hands[i].localPosition=hand;
            }
        }
    }
}
