using UnityEngine;

namespace StormSim.FloodTest
{
    // Directional surge prototype, not a hydrodynamic solver. Positive Z points inland.
    public sealed class TsunamiScenario : MonoBehaviour
    {
        public Renderer waterRenderer;
        public Transform player;
        public float seaLevel = -0.7f;
        [Min(0f)] public float warningSeconds = 6f;
        public float startFrontZ = -65f;
        [Min(0.1f)] public float surgeSpeed = 4f;
        [Min(0f)] public float surgeHeight = 3.4f;
        [Min(1f)] public float frontWidth = 12f;
        public float recessionStart = 48f;
        [Min(1f)] public float recessionDuration = 25f;
        [HideInInspector] public float elapsed;
        public bool HasEvacuated { get; private set; }
        private MaterialPropertyBlock properties;

        public float FrontZ => startFrontZ + Mathf.Max(0f, elapsed - warningSeconds) * surgeSpeed;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            ApplySurface();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplySurface();
            var detector = player != null ? player.GetComponent<FloodDetector>() : null;
            if (player != null && player.position.z > 39f && player.position.y > 5.5f &&
                (detector == null || !detector.IsFlooded)) HasEvacuated = true;
        }

        public float SampleHeight(Vector3 position)
        {
            if (Mathf.Abs(position.x) > 45f || position.z < -90f || position.z > 65f)
                return float.NegativeInfinity;
            // Replace this travelling profile with local CA cell height/velocity data.
            // Keep CPU detection and shader displacement on the same simulation clock.
            float front = FrontZ + Mathf.Sin(position.x * 0.18f) * 1.4f;
            float distance = front - position.z;
            float fill = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / frontWidth));
            float crest = Mathf.Exp(-Mathf.Pow((distance - frontWidth * 0.65f) / (frontWidth * 0.32f), 2f));
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - warningSeconds) / 3f));
            strength *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - recessionStart) / recessionDuration));
            return seaLevel + strength * surgeHeight * (fill + 0.24f * crest);
        }

        private void ApplySurface()
        {
            if (waterRenderer == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat("_SurgeFront", FrontZ);
            properties.SetFloat("_SurgeWidth", frontWidth);
            properties.SetFloat("_SurgeHeight", surgeHeight);
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - warningSeconds) / 3f));
            strength *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - recessionStart) / recessionDuration));
            properties.SetFloat("_SurgeStrength", strength);
            properties.SetFloat("_FlowTime", elapsed);
            waterRenderer.SetPropertyBlock(properties);
        }

        private void OnGUI()
        {
            var detector = player != null ? player.GetComponent<FloodDetector>() : null;
            string state = elapsed < warningSeconds ? "WARNING — incoming tsunami" :
                elapsed > recessionStart ? "SURGE RECEDING — stay on high ground" : "SURGE MOVING INLAND";
            if (HasEvacuated) state = "SAFE — high ground reached";
            else if (detector != null && detector.IsFlooded) state = "CAUGHT BY THE SURGE — evacuation failed";
            GUI.Box(new Rect(16, 16, 480, 106), "STORMSIM | Beach tsunami sandbox");
            GUI.Label(new Rect(30, 43, 452, 24), state);
            GUI.Label(new Rect(30, 69, 452, 24), "WASD: move • follow the green ramp to high ground");
            GUI.Label(new Rect(30, 93, 452, 24), "Stop / Play to restart • illustrative surge, not hazard prediction");
        }
    }
}
