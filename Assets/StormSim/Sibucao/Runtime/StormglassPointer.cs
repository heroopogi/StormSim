using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace StormSim.UI
{
    // A controller ray for this UI only. It does not synthesize mouse input or
    // submit through a second UI module, so one release produces one click.
    public sealed class StormglassPointer
    {
        public Button Hovered { get; private set; }
        public bool ClickedThisFrame { get; private set; }
        Button pressed;
        bool wasHeld, armed;
        readonly PointerEventData data;
        public StormglassPointer(EventSystem events, int id) { data = new PointerEventData(events) { pointerId = id }; }
        public void Reset()
        {
            if (Hovered) ExecuteEvents.Execute(Hovered.gameObject, data, ExecuteEvents.pointerExitHandler);
            if (pressed) ExecuteEvents.Execute(pressed.gameObject, data, ExecuteEvents.pointerUpHandler);
            Hovered = pressed = null; wasHeld = false; armed = false;
        }
        public bool Process(Ray ray, bool held, System.Collections.Generic.IReadOnlyList<Button> buttons, RectTransform viewport, out Vector3 hitPoint)
        {
            ClickedThisFrame = false;
            hitPoint = ray.GetPoint(2); Button hit = null; float nearest = 10;
            foreach (var b in buttons)
            {
                if (!b || !b.isActiveAndEnabled || !b.IsInteractable()) continue;
                var rect = (RectTransform)b.transform;
                if(Vector3.Dot(ray.direction,rect.forward)<=0)continue;
                if (!new Plane(rect.forward, rect.position).Raycast(ray, out float d) || d < 0 || d >= nearest) continue;
                var point = ray.GetPoint(d);
                if (!rect.rect.Contains(rect.InverseTransformPoint(point))) continue;
                if (viewport && rect.IsChildOf(viewport) && !viewport.rect.Contains(viewport.InverseTransformPoint(point))) continue;
                nearest = d; hit = b; hitPoint = point;
            }
            if (hit != Hovered)
            {
                if (Hovered) ExecuteEvents.Execute(Hovered.gameObject, data, ExecuteEvents.pointerExitHandler);
                Hovered = hit;
                if (Hovered) ExecuteEvents.Execute(Hovered.gameObject, data, ExecuteEvents.pointerEnterHandler);
            }
            // Opening a panel while holding a trigger must not activate it.
            if (!held && !wasHeld) armed = true;
            if (held && !wasHeld && armed)
            {
                pressed = hit;
                if (pressed) ExecuteEvents.Execute(pressed.gameObject, data, ExecuteEvents.pointerDownHandler);
            }
            if (!held && wasHeld)
            {
                var released = pressed; pressed = null;
                if (released) ExecuteEvents.Execute(released.gameObject, data, ExecuteEvents.pointerUpHandler);
                if (released && released == hit && released.IsInteractable())
                {
                    ExecuteEvents.Execute(released.gameObject, data, ExecuteEvents.pointerClickHandler);
                    ClickedThisFrame = true;
                }
                armed = true;
            }
            wasHeld = held;
            return hit;
        }
        public static bool TrackedRay(XRNode node, Transform origin, out Ray ray, out bool trigger)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            trigger = false; ray = default;
            if (!d.isValid || !d.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) ||
                !d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return false;
            d.TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
            // OpenXR distinguishes the pointing pose from the hand's grip pose.
            var controller = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRController>(node == XRNode.LeftHand ? "LeftHand" : "RightHand");
            if (controller != null)
            {
                var aimPosition = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("pointerPosition");
                var aimRotation = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointerRotation");
                if (aimPosition != null && aimRotation != null) { position = aimPosition.ReadValue(); rotation = aimRotation.ReadValue(); }
            }
            ray = new Ray(origin.TransformPoint(position), origin.rotation * rotation * Vector3.forward); return true;
        }
    }

    public static class StormglassInteractionRay
    {
        public static XRNode PreferredHand
        {
            get
            {
                var session=StormSim.Sibucao.SibucaoSession.Instance;
                return session&&session.settings.leftHanded?XRNode.LeftHand:XRNode.RightHand;
            }
        }

        // Desktop deliberately remains camera/mouse driven. In XR the visible
        // dominant controller ray becomes the single source of world targeting.
        public static Ray From(Camera view)
        {
            if(XRSettings.isDeviceActive&&view&&StormglassPointer.TrackedRay(PreferredHand,view.transform.parent,out Ray ray,out _))return ray;
            return new Ray(view.transform.position,view.transform.forward);
        }
    }
}
