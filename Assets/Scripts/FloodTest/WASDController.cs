using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StormSim.FloodTest
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WASDController : MonoBehaviour
    {
        [Min(0f)] public float speed = 5f;
        public float gravity = -20f;
        public Transform movementReference;
        public bool MovementFrozen { get; set; }
        private CharacterController controller;
        private float verticalSpeed;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            Vector2 input = Vector2.zero;
            if (!MovementFrozen)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
#elif ENABLE_INPUT_SYSTEM
                // The main project uses the new Input System; avoid changing its settings.
                var keyboard = Keyboard.current;
                if (keyboard != null)
                    input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
#endif
            }
            input = Vector2.ClampMagnitude(input, 1f);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            // Keep gravity active after flooding so the capsule stays grounded.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (movementReference != null)
            {
                forward = Vector3.ProjectOnPlane(movementReference.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, forward);
            }
            controller.Move(((right * input.x + forward * input.y) * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }
    }
}
