using UnityEngine;

namespace StormSim.Sibucao
{
    [CreateAssetMenu(menuName="StormSim/Sibucao/Decision Node")]
    public sealed class DecisionNode : ScriptableObject
    {
        public string id, title, speaker;
        [TextArea(3,8)] public string message;
        public DecisionCategory category;
        public bool automatic, major;
        public WaterTier tier = WaterTier.Tier1;
        public DecisionChoice[] choices;
        [TextArea] public string prompt;
        public string approachLine;
        public AudioClip approachAudio;
        public float approachDistance=23;
    }
}
