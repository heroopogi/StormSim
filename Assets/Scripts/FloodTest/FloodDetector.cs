using UnityEngine;

namespace StormSim.FloodTest
{
    [RequireComponent(typeof(WASDController))]
    public sealed class FloodDetector : MonoBehaviour
    {
        public RisingWater water;
        // Optional scenario component: the tsunami prototype may not be installed.
        public MonoBehaviour tsunami;
        public bool freezeMovement = true;
        public bool IsFlooded { get; private set; }
        private WASDController movement;
        private System.Func<Vector3, float> sampleScenarioHeight;
        private System.Func<bool> scenarioHasEvacuated;

        private void Awake()
        {
            movement = GetComponent<WASDController>();
            if (tsunami == null) return;
            var type = tsunami.GetType();
            var sample = type.GetMethod("SampleHeight", new[] { typeof(Vector3) });
            var evacuated = type.GetProperty("HasEvacuated")?.GetGetMethod();
            if (sample?.ReturnType != typeof(float) || evacuated?.ReturnType != typeof(bool))
            {
                Debug.LogWarning("The optional flood scenario must expose SampleHeight(Vector3) and HasEvacuated. Using RisingWater instead.", this);
                return;
            }
            sampleScenarioHeight = (System.Func<Vector3, float>)System.Delegate.CreateDelegate(typeof(System.Func<Vector3, float>), tsunami, sample);
            scenarioHasEvacuated = (System.Func<bool>)System.Delegate.CreateDelegate(typeof(System.Func<bool>), tsunami, evacuated);
        }

        private void Update()
        {
            // The player origin is at its feet. For CA integration, sample the local
            // cell water height at this player's X/Z instead of one global plane.
            bool scenarioReady = tsunami != null && sampleScenarioHeight != null;
            float height = scenarioReady ? sampleScenarioHeight(transform.position) :
                water != null ? water.CurrentHeight : float.NegativeInfinity;
            if (IsFlooded || height <= transform.position.y || (scenarioReady && scenarioHasEvacuated())) return;
            IsFlooded = true;
            Debug.Log("Player has been flooded!", this);
            if (freezeMovement) movement.MovementFrozen = true;
        }

        private void OnGUI()
        {
            if (tsunami != null && sampleScenarioHeight != null) return;
            GUI.Box(new Rect(16, 16, 370, 100), "STORMSIM — Flood mechanics sandbox");
            GUI.Label(new Rect(28, 42, 345, 24), "WASD: move | Stop and Play to restart");
            GUI.Label(new Rect(28, 66, 345, 24), water == null ? "Assign water in Inspector" :
                $"Water: {water.CurrentHeight:F2} m / {water.maxHeight:F2} m");
            if (IsFlooded) GUI.Box(new Rect(16, 126, 370, 36), "Player has been flooded! Evacuation failed.");
        }
    }
}
