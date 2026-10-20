using UnityEngine;

namespace StormSim.Sibucao
{
    public enum AftermathDamageLevel { Minor, Moderate, Unsafe }

    // Small, reusable metadata component for desktop inspection and future VR interaction.
    public sealed class SibucaoAftermathBuildingState : MonoBehaviour
    {
        public AftermathDamageLevel damageLevel;
        public string visibleCondition;
        public float routeClearance;
        public bool simplifiedCollision = true;
        public bool usesLod = true;
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class SibucaoAftermathSpatialAudio : MonoBehaviour
    {
        public Vector2 interval = new Vector2(8, 16);
        AudioSource source;
        float next;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2;
            source.maxDistance = 18;
            source.dopplerLevel = 0;
            Schedule();
        }

        void OnEnable() { Schedule(); }
        void Schedule() { next = Time.time + Random.Range(interval.x, interval.y); }
        void Update()
        {
            if (!source || !source.clip || Time.time < next) return;
            source.pitch = Random.Range(.94f, 1.06f);
            source.Play();
            Schedule();
        }
    }
}
