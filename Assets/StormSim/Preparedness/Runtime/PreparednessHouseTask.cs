using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace StormSim.Preparedness
{
    public sealed class PreparednessHouseTask : MonoBehaviour
    {
        public string requiredItem, instruction, result;
        public float workSeconds=3;
        public bool exterior,consumeMaterial;
        public GameObject before,after;
        public PreparednessItem prerequisite;
        public bool requiresLadderHeight;
        PreparednessSession session;PreparednessItem tool,item;float progress;bool working;int nextProgressAnnouncement=25;
        public float ProgressSeconds=>progress;
        public int ProgressPercent=>item&&item.prepared?100:Mathf.FloorToInt(Mathf.Clamp01(progress/Mathf.Max(.1f,workSeconds))*100);
        public bool Working=>working;
        public void RestoreProgress(float value){working=false;progress=item&&item.prepared?workSeconds:Mathf.Clamp(value,0,workSeconds);}
        void Awake(){item=GetComponent<PreparednessItem>();ApplyState();}
        public void Begin(PreparednessSession s,PreparednessItem held)
        {
            item=GetComponent<PreparednessItem>();
            if(working&&s.toggleWork){Cancel();s.Say("Work paused. Use again to resume.");return;}
            if(item.prepared){s.Say(result);return;}
            if(prerequisite&&!prerequisite.prepared){s.Say("First: "+prerequisite.displayName);return;}
            if(requiresLadderHeight&&s.player.transform.position.y<transform.position.y-1.9f){s.Say("Use the secured ladder to reach the working platform first.");return;}
            if(exterior&&!s.OutdoorSafe){s.Say("Outdoor work is closed. "+s.WeatherText);return;}
            if(requiresLadderHeight&&!s.ElevatedWorkSafe){s.Say("Wind is rising. Stop elevated work and descend using the ladder.");return;}
            if(!string.IsNullOrEmpty(requiredItem)&&(!held||held.id!=requiredItem)){s.Say(instruction);return;}
            session=s;tool=held;working=true;nextProgressAnnouncement=Mathf.Max(25,(Mathf.FloorToInt(ProgressPercent/25f)+1)*25);s.Say(instruction+(s.toggleWork?" Working; Q stops. Stay facing the task.":" Hold E / right trigger while facing the task."));
        }
        void Update()
        {
            if(!working)return;
            bool pressed=Keyboard.current!=null&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).isPressed;
            if(XRSettings.isDeviceActive)InputDevices.GetDeviceAtXRNode(session.player.carriedBag?XRNode.LeftHand:XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out pressed);
            if(!pressed&&!session.toggleWork||!session.CanPrepare||session.player.ReadingPanel||StormSim.Sibucao.SibucaoSession.Instance&&StormSim.Sibucao.SibucaoSession.Instance.Mode==StormSim.Sibucao.SibucaoMode.Settings||session.player.focused!=item||Vector3.Distance(session.player.view.transform.position,transform.position)>3||tool&&session.player.held!=tool||exterior&&!session.OutdoorSafe||requiresLadderHeight&&!session.ElevatedWorkSafe){working=false;return;}
            progress+=Time.deltaTime;
            int percent=Mathf.FloorToInt(Mathf.Clamp01(progress/workSeconds)*100);
            if(percent>=nextProgressAnnouncement&&percent<100){session.Say(instruction+" "+nextProgressAnnouncement+"%");nextProgressAnnouncement+=25;}
            if(progress>=workSeconds)CompleteWork();
        }
        public bool CompleteWork()
        {
            if(!working||progress<workSeconds||!session||!session.CanPrepare||exterior&&!session.OutdoorSafe||requiresLadderHeight&&!session.ElevatedWorkSafe||!string.IsNullOrEmpty(requiredItem)&&(!tool||session.player.held!=tool||tool.id!=requiredItem))return false;
            item.prepared=true;working=false;
            if(consumeMaterial&&tool){session.player.Release();tool.storedIn="installed";tool.SetPhysics(false);tool.transform.position=transform.position;tool.gameObject.SetActive(false);}
            ApplyState();session.SpeakCue("task.completed."+item.id,"Household guide",result);PreparationFeedback.Confirm();return true;
        }
        public void Cancel(){working=false;}
        public void ApplyState()
        {
            working=false;progress=0;
            if(!item)item=GetComponent<PreparednessItem>();if(!item)return;
            if(before)before.SetActive(!item.prepared);if(after)after.SetActive(item.prepared);
        }
    }
}
