using UnityEngine;
namespace StormSim.Sibucao
{
    // An audit annotation, not a collision barrier. The awning and existing home
    // walls provide the actual geometry; the two options provide physical actions.
    public sealed class SibucaoCoverageGate:MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public SibucaoStormPassage debris;
        public Vector3 center=new Vector3(0,1,0),size=new Vector3(1.4f,4,.65f);
        void OnDrawGizmosSelected(){Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.cyan;Gizmos.DrawWireCube(center,size);}
    }
}
