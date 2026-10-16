using UnityEngine;

namespace StormSim.FloodTest
{
    public sealed class RisingWater : MonoBehaviour
    {
        [Min(0f)] public float riseSpeed = 0.12f;
        public float maxHeight = 3.5f;
        public float CurrentHeight => transform.position.y;

        private void Update()
        {
            Vector3 position = transform.position;
            // Replace this uniform riseSpeed integration with real Cellular Automata
            // flood step heights; interpolate between steps for smooth rendering.
            if (position.y < maxHeight)
                position.y = Mathf.Min(maxHeight, position.y + Mathf.Max(0f, riseSpeed) * Time.deltaTime);
            transform.position = position;
        }
    }
}
