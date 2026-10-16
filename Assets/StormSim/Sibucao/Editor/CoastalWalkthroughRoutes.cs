using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using StormSim.Sibucao;

public static class CoastalWalkthroughRoutes
{
    static HashSet<Collider> ground;
    static Dictionary<Collider,bool> obstacles;
    static Dictionary<Vector2Int,Vector3> positions;
    static HashSet<Vector2Int> blocked;
    static bool rejectRoofs,respectTaskProps,stepClearance;
    static float BodyRadius=>stepClearance?.34f:.29f;
    static float BodyBase=>stepClearance?.34f:.4f;
    static float BodyTop=>stepClearance?1.85f:1.5f;
    const float Cell=.25f;
    static bool Floor(string name){var n=name.ToLowerInvariant();return n.Contains("felix amante")||n.Contains("carlos hilado")||n.StartsWith("ground ")||n.Contains("measured terrain")||n.StartsWith("residential ")||n.StartsWith("junction ")||n.Contains("road surface")||n.Contains("asphalt")||n.Contains("sidewalk")||n.Contains("paved")||n.Contains("court painted")||n.Contains("highway")||n.StartsWith("service road ")||n.StartsWith("tertiary road ")||n.StartsWith("footway road ")||n.StartsWith("secondary_link road ")||n=="circumferential road"||n=="port approach road and causeway"||n.Contains("worn access road")||n=="open muddy coastal lot";}
    public static float Surface(Vector3 p){var hits=Physics.RaycastAll(new Vector3(p.x,70,p.z),Vector3.down,140,~0,QueryTriggerInteraction.Ignore).Where(h=>Floor(h.collider.name)).ToArray();if(hits.Length==0)throw new Exception("No ground at "+p);return hits.Max(h=>h.point.y)+.035f;}
    static bool Obstacle(Collider c){if(obstacles.TryGetValue(c,out var value))return value;return obstacles[c]=!Floor(c.name)&&!c.GetComponentInParent<SibucaoPlayer>()&&!c.GetComponentInParent<SibucaoResident>()&&!c.GetComponentInParent<SibucaoActor>()&&(respectTaskProps||!c.GetComponentInParent<PhysicalDecisionStation>())&&!c.GetComponentInParent<StormSim.Preparedness.PreparednessPlayer>();}
    static void Init(){Physics.SyncTransforms();ground=new HashSet<Collider>(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&Floor(c.name)));obstacles=new Dictionary<Collider,bool>();positions=new Dictionary<Vector2Int,Vector3>();blocked=new HashSet<Vector2Int>();}
    static bool Node(Vector2Int k,out Vector3 p)
    {
        if(positions.TryGetValue(k,out p))return true;if(blocked.Contains(k))return false;
        p=new Vector3(k.x*Cell,0,k.y*Cell);float y=float.NegativeInfinity;
        var verticalHits=Physics.RaycastAll(p+Vector3.up*70,Vector3.down,140,~0,QueryTriggerInteraction.Ignore);
        foreach(var hit in verticalHits)if(ground.Contains(hit.collider))y=Mathf.Max(y,hit.point.y);
        if(rejectRoofs&&verticalHits.Any(h=>h.point.y>y+1.9f&&h.collider.transform.root.name=="CesiumGeoreference"&&(h.collider.name.StartsWith("Building ")||h.collider.name=="metal"||h.collider.name=="plaster"||h.collider.name.ToLowerInvariant().Contains("roof")))){blocked.Add(k);return false;}
        if(!float.IsFinite(y)){blocked.Add(k);return false;}p.y=y+.035f;
        // Include controller skin and the extra height while stepping over a curb.
        if(Physics.OverlapCapsule(p+Vector3.up*BodyBase,p+Vector3.up*BodyTop,BodyRadius,~0,QueryTriggerInteraction.Ignore).Any(Obstacle)){blocked.Add(k);return false;}
        positions[k]=p;return true;
    }
    static bool Edge(Vector3 a,Vector3 b)
    {var delta=b-a;return !Physics.CapsuleCastAll(a+Vector3.up*BodyBase,a+Vector3.up*BodyTop,BodyRadius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>Obstacle(h.collider));}
    static Vector2Int Nearest(Vector3 p)
    {var k=new Vector2Int(Mathf.RoundToInt(p.x/Cell),Mathf.RoundToInt(p.z/Cell));for(int r=0;r<24;r++)for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)if(Mathf.Abs(x)==r||Mathf.Abs(z)==r){var n=k+new Vector2Int(x,z);if(Node(n,out _))return n;}throw new Exception("No accessible ground near "+p);}
    public static Vector3[] Plan(Vector3 from,Vector3 to,int maxNodes=100000,bool outdoors=false,bool taskProps=false,bool includeStepClearance=false)
    {
        rejectRoofs=outdoors;respectTaskProps=taskProps;stepClearance=includeStepClearance;Init();var a=Nearest(from);var b=Nearest(to);var open=new SortedSet<(float,int,int)>();var costs=new Dictionary<Vector2Int,float>{{a,0}};var parent=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();open.Add((Vector2Int.Distance(a,b)*Cell,a.x,a.y));
        while(open.Count>0&&closed.Count<maxNodes){var e=open.Min;open.Remove(e);var k=new Vector2Int(e.Item2,e.Item3);if(!closed.Add(k))continue;
            if(k==b){var result=new List<Vector3>{positions[k]};while(parent.TryGetValue(k,out var prev)){k=prev;result.Add(positions[k]);}result.Reverse();return result.ToArray();}
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){if(x==0&&z==0)continue;var q=k+new Vector2Int(x,z);if(closed.Contains(q)||!Node(q,out var p)||Mathf.Abs(p.y-positions[k].y)>.35f||!Edge(positions[k],p))continue;float cost=costs[k]+Vector3.Distance(p,positions[k]);if(costs.TryGetValue(q,out var old)&&old<=cost)continue;costs[q]=cost;parent[q]=k;open.Add((cost+Vector2Int.Distance(q,b)*Cell,q.x,q.y));}
        }throw new Exception("No continuous collision-clear path from "+from+" to "+to);
    }
}



