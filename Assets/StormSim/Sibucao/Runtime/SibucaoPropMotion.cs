using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed class SibucaoPropMotion:MonoBehaviour
    {
        public Vector3 rotationPerSecond;
        void Update(){if(SibucaoSession.Instance&&SibucaoSession.Instance.Mode==SibucaoMode.Playing)transform.Rotate(rotationPerSecond*Time.deltaTime,Space.Self);}
    }
}
