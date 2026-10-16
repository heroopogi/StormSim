using UnityEngine;
namespace StormSim.Sibucao
{
    [CreateAssetMenu(menuName="StormSim/Sibucao flood terrain")]
    public sealed class SibucaoFloodTerrain:ScriptableObject
    {
        public Vector2 origin;
        public int width,height;
        public float cellSize=2;
        public float[] ground,crestX,crestZ;
        public float[] initialDepth;
        public float antecedentRainMetres;
        // 0 outside scene geometry, 1 ground, 2 open sea outlet.
        public byte[] kind;
        public bool[] covered;
        public Vector3 Position(int index)=>new Vector3(origin.x+index%width*cellSize,ground[index],origin.y+index/width*cellSize);
    }
}
