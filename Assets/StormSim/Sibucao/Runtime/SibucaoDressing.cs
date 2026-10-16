using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoDressing : MonoBehaviour
    {
        public int seed=130926;
        [Range(0,12)] public int propsPerYard=5;
        [Range(0,12)] public int grassPerYard=6;
        [Range(10,80)] public float routeBand=40;
        public float minimumWorldZ=975;
        [Range(0,732)] public float routeEnd=325;
        [Range(100,1500)] public int maximumObjects=550;
        public SibucaoRoute route;
        public GameObject[] householdPrefabs;
        public GameObject grassPrefab;
        [TextArea] public string lastBake;
    }
}
