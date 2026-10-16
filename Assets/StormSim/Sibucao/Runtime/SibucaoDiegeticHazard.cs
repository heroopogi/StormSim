using UnityEngine;
namespace StormSim.Sibucao
{
    public enum SibucaoHazardKind { LowStreet, FallenTree, CollapsedRoof, LiveWire, LGUCordon, Supplies, Neighbour }
    public sealed class SibucaoDiegeticHazard:MonoBehaviour
    {
        public string siteId,location,reason;
        public SibucaoHazardKind kind;
        public Vector2 basinRadius=new Vector2(13,10);
        public float lowering=1.65f;
        public int nearbyStation=-1;
    }
}
