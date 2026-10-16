using UnityEngine;

namespace StormSim.Sibucao
{
    [CreateAssetMenu(menuName="StormSim/Sibucao/Route")]
    public sealed class SibucaoRoute : ScriptableObject
    {
        public Vector3[] points;
        public Vector3[] spawnPoints;
        public float[] spawnDistances;
        public float length;
        public Vector3 Sample(float distance)
        {
            for(int i=1;i<points.Length;i++)
            {
                float d=Vector3.Distance(points[i-1],points[i]);
                if(distance<=d)return Vector3.Lerp(points[i-1],points[i],d<.001f?0:distance/d);
                distance-=d;
            }
            return points[points.Length-1];
        }
        // Join ahead after a lateral detour; returning perpendicularly can cross its supports.
        public float TravelJoinProgress(Vector3 position,float targetProgress)
        {
            float start=Progress(position),direction=Mathf.Sign(targetProgress-start);
            var offset=position-Sample(start);offset.y=0;
            float joined=start+direction*Mathf.Min(24,offset.magnitude);
            return direction>=0?Mathf.Min(joined,targetProgress):Mathf.Max(joined,targetProgress);
        }
        public float Progress(Vector3 position)
        {
            float best=float.MaxValue, result=0, accumulated=0;
            for(int i=1;i<points.Length;i++)
            {
                Vector3 a=points[i-1], delta=points[i]-a;
                float t=Mathf.Clamp01(Vector3.Dot(position-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                float sq=(position-(a+delta*t)).sqrMagnitude;
                if(sq<best){best=sq;result=accumulated+delta.magnitude*t;}
                accumulated+=delta.magnitude;
            }
            return result;
        }
    }
}
