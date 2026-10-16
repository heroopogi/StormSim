using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao.Editor
{
    /// <summary>Editor-only surface/capsule search. Never opens walls or teleports a player.</summary>
    public sealed class SibucaoBranchPathBake
    {
        readonly PhysicalDecisionStation station;readonly SibucaoFloodTerrain terrain;
        readonly Dictionary<Vector2Int,Vector3> positions=new Dictionary<Vector2Int,Vector3>();
        readonly HashSet<Vector2Int> blocked=new HashSet<Vector2Int>();
        HashSet<Vector2Int> connected;
        const float Cell=.85f;const int Limit=52;
        readonly bool floodAware;
        public SibucaoBranchPathBake(PhysicalDecisionStation s,SibucaoFloodTerrain t,bool floodAware=false){station=s;terrain=t;this.floodAware=floodAware;}
        float Lift(Vector3 world)
        {
            if(!floodAware||!terrain||terrain.initialDepth==null)return 0;
            int x=Mathf.RoundToInt((world.x-terrain.origin.x)/terrain.cellSize),z=Mathf.RoundToInt((world.z-terrain.origin.y)/terrain.cellSize);
            if(x<0||z<0||x>=terrain.width||z>=terrain.height)return 0;
            int i=z*terrain.width+x;
            return Mathf.Max(0,terrain.ground[i]+terrain.initialDepth[i]-world.y-1.45f);
        }
        bool Ignore(Collider c)=>c.isTrigger||c.GetComponentInParent<PhysicalDecisionStation>()||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>()||c.attachedRigidbody;
        bool Position(Vector2Int cell,out Vector3 local)
        {
            if(positions.TryGetValue(cell,out local))return true;if(blocked.Contains(cell)||Mathf.Abs(cell.x)>Limit||Mathf.Abs(cell.y)>Limit)return false;
            var p=station.transform.TransformPoint(cell.x*Cell,0,cell.y*Cell);float expected=p.y;
            if(terrain){int x=Mathf.RoundToInt((p.x-terrain.origin.x)/terrain.cellSize),z=Mathf.RoundToInt((p.z-terrain.origin.y)/terrain.cellSize);if(x>=0&&z>=0&&x<terrain.width&&z<terrain.height)expected=terrain.ground[z*terrain.width+x];}
            var hits=Physics.RaycastAll(new Vector3(p.x,expected+2.2f,p.z),Vector3.down,4.5f,~0,QueryTriggerInteraction.Ignore);
            bool ground=false;float height=float.NegativeInfinity;
            foreach(var hit in hits)if(!Ignore(hit.collider)&&hit.normal.y>.75f&&hit.point.y<expected+1.1f&&hit.point.y>height){height=hit.point.y;ground=true;}
            if(!ground){blocked.Add(cell);return false;}p.y=height+.03f;
            float lift=Lift(p);
            if(Physics.OverlapCapsule(p+Vector3.up*(.4f+lift),p+Vector3.up*(1.55f+lift),.31f,~0,QueryTriggerInteraction.Ignore).Any(c=>!Ignore(c))){blocked.Add(cell);return false;}
            local=station.transform.InverseTransformPoint(p);positions[cell]=local;return true;
        }
        Vector2Int Nearest(Vector3 p)
        {
            var desired=new Vector2Int(Mathf.RoundToInt(p.x/Cell),Mathf.RoundToInt(p.z/Cell));
            if(connected!=null)
            {
                var candidate=connected.OrderBy(c=>(c-desired).sqrMagnitude).First();
                if(Vector2Int.Distance(candidate,desired)>13)throw new Exception(station.name+" no connected alley within 11 m of "+p);
                return candidate;
            }
            for(int r=0;r<=9;r++)for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)
            {if(Mathf.Abs(x)!=r&&Mathf.Abs(z)!=r)continue;var c=desired+new Vector2Int(x,z);if(Position(c,out _))return c;}
            throw new Exception(station.name+" has no reachable ground near "+p);
        }
        bool Edge(Vector3 a,Vector3 b)
        {
            if(Mathf.Abs(a.y-b.y)>.37f)return false;
            var from=station.transform.TransformPoint(a);var to=station.transform.TransformPoint(b);float lift=Mathf.Max(Lift(from),Lift(to));
            return !Physics.CapsuleCastAll(from+Vector3.up*(.4f+lift),from+Vector3.up*(1.55f+lift),.31f,(to-from).normalized,(to-from).magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!Ignore(h.collider));
        }
        List<Vector3> Leg(Vector2Int from,Vector2Int to)
        {
            var comparer=Comparer<(float cost,int serial,Vector2Int cell)>.Create((a,b)=>a.cost!=b.cost?a.cost.CompareTo(b.cost):a.serial.CompareTo(b.serial));
            var frontier=new SortedSet<(float cost,int serial,Vector2Int cell)>(comparer);int serial=0;
            var costs=new Dictionary<Vector2Int,float>{{from,0}};var parent=new Dictionary<Vector2Int,Vector2Int>();frontier.Add((0,serial++,from));
            var closed=new HashSet<Vector2Int>();var dirs=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(frontier.Count>0)
            {
                var first=frontier.Min;frontier.Remove(first);var cur=first.cell;if(!closed.Add(cur))continue;
                if(cur==to){var list=new List<Vector3>();while(cur!=from){Position(cur,out var p);list.Add(p);cur=parent[cur];}Position(from,out var start);list.Add(start);list.Reverse();return list;}
                Position(cur,out var a);
                foreach(var dir in dirs)
                {
                    var next=cur+dir;if(closed.Contains(next)||!Position(next,out var b)||!Edge(a,b))continue;
                    float cost=costs[cur]+Vector3.Distance(a,b);if(costs.TryGetValue(next,out float old)&&old<=cost)continue;
                    costs[next]=cost;parent[next]=cur;frontier.Add((cost+Vector2Int.Distance(next,to)*Cell,serial++,next));
                }
            }
            throw new Exception(station.name+" blocked physical branch segment "+from+" -> "+to+". No wall collisions were disabled.");
        }
        public Vector3[] Route(Vector3[] anchors)
        {
            if(connected==null)
            {
                var origin=Nearest(new Vector3(0,0,-5));connected=new HashSet<Vector2Int>{origin};var queue=new Queue<Vector2Int>();queue.Enqueue(origin);
                var dirs=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
                while(queue.Count>0){var cell=queue.Dequeue();Position(cell,out var a);foreach(var dir in dirs){var next=cell+dir;if(!connected.Contains(next)&&Position(next,out var b)&&Edge(a,b)){connected.Add(next);queue.Enqueue(next);}}}
            }
            var all=new List<Vector3>();var cells=anchors.Select(Nearest).ToArray();
            for(int i=1;i<cells.Length;i++){var leg=Leg(cells[i-1],cells[i]);if(all.Count>0)leg.RemoveAt(0);all.AddRange(leg);}
            if(all.Count<2)throw new Exception("Degenerate branch "+station.name);
            var simple=new List<Vector3>{all[0]};for(int i=1;i<all.Count-1;i++)
            {var a=(all[i]-all[i-1]).normalized;var b=(all[i+1]-all[i]).normalized;if(Vector3.Dot(a,b)<.998f||Vector3.Distance(simple.Last(),all[i])>3.3f)simple.Add(all[i]);}
            simple.Add(all.Last());return simple.ToArray();
        }
    }
}
