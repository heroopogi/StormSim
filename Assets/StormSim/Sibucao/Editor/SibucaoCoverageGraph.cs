using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace StormSim.Sibucao.Editor
{
    // Reachability, not a sample of hand-selected paths. Joining point contacts is
    // conservative: it adds possible bypasses instead of hiding narrow ones.
    public sealed class SibucaoCoverageGraph
    {
        readonly Vector3[] vertices;readonly int[] triangles,areas;
        readonly Dictionary<Vector3Int,List<int>> polygons=new Dictionary<Vector3Int,List<int>>();
        readonly Vector3Int[][] keys;readonly Vector3[] centers;
        public int Count=>areas.Length;
        public SibucaoCoverageGraph(NavMeshTriangulation graph)
        {
            vertices=graph.vertices;triangles=graph.indices;areas=graph.areas;keys=new Vector3Int[Count][];centers=new Vector3[Count];
            for(int i=0;i<Count;i++)
            {
                keys[i]=new Vector3Int[3];
                for(int j=0;j<3;j++)
                {
                    var p=vertices[triangles[i*3+j]];centers[i]+=p/3;
                    var key=new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));keys[i][j]=key;
                    if(!polygons.TryGetValue(key,out var list)){list=new List<int>();polygons[key]=list;}list.Add(i);
                }
            }
        }
        List<int> Sources(Vector3 point)
        {
            var found=new List<int>();float best=3;
            for(int i=0;i<Count;i++)
            {
                var a=vertices[triangles[i*3]];var b=vertices[triangles[i*3+1]];var c=vertices[triangles[i*3+2]];
                float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.00001f)continue;
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/den;
                float w=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/den;
                if(Mathf.Min(u,w,1-u-w)<-.001f)continue;
                float distance=Mathf.Abs(point.y-(u*a.y+w*b.y+(1-u-w)*c.y));
                if(distance>best+.02f)continue;if(distance<best-.02f){found.Clear();best=distance;}found.Add(i);
            }
            return found;
        }
        HashSet<int> Goals(SibucaoDryRefuge refuge)
        {
            // The arrival is a volume, not one sampled point. A surrounding box is
            // a conservative superset of its cylinder; no reachable edge is omitted.
            var box=new Bounds(refuge.arrivalPoint.position,new Vector3(refuge.arrivalRadius*2,6,refuge.arrivalRadius*2));var result=new HashSet<int>();
            for(int i=0;i<Count;i++)
            {
                var b=new Bounds(vertices[triangles[i*3]],Vector3.zero);b.Encapsulate(vertices[triangles[i*3+1]]);b.Encapsulate(vertices[triangles[i*3+2]]);
                if(b.Intersects(box))result.Add(i);
            }
            return result;
        }
        public Vector3[] Search(Vector3 start,SibucaoDryRefuge refuge,bool excludeEncounters,out int reachable,out int[] witness)
        {
            var goals=Goals(refuge);var parents=new Dictionary<int,int>();var queue=new Queue<int>();var visitedVertices=new HashSet<Vector3Int>();
            foreach(var i in Sources(start))if(!excludeEncounters||areas[i]!=2){parents[i]=-1;queue.Enqueue(i);}
            int end=-1;
            while(queue.Count>0)
            {
                int i=queue.Dequeue();if(goals.Contains(i)){end=i;break;}
                foreach(var key in keys[i])if(visitedVertices.Add(key))foreach(var next in polygons[key])
                    if(!parents.ContainsKey(next)&&(!excludeEncounters||areas[next]!=2)){parents[next]=i;queue.Enqueue(next);}
            }
            reachable=parents.Count;var route=new List<int>();while(end>=0){route.Add(end);end=parents[end];}route.Reverse();witness=route.ToArray();return route.ConvertAll(i=>centers[i]).ToArray();
        }
    }
}
