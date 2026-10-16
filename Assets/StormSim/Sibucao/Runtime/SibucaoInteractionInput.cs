using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
namespace StormSim.Sibucao
{
    /// <summary>One press edge per frame, shared by gaze/menu/door interactions.</summary>
    public static class SibucaoInteractionInput
    {
        static int frame=-1;static bool previous,pressed;
        public static bool Pressed
        {
            get
            {
                if(frame==Time.frameCount)return pressed;frame=Time.frameCount;
                bool current=false;
                foreach(var node in new[]{XRNode.RightHand,XRNode.LeftHand})
                {var d=InputDevices.GetDeviceAtXRNode(node);if(d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out var trigger))current|=trigger;if(!StormSim.UI.StormglassUI.InUse&&d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out var primary))current|=primary;}
                pressed=(current&&!previous)||(Keyboard.current!=null&&StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.E).wasPressedThisFrame);previous=current;return pressed;
            }
        }
    }
}
