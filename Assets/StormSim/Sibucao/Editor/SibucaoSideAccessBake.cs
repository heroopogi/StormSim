using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    // Bake against the placed scene, including mission props, signs and the original encounters.
    public static class SibucaoSideAccessBake
    {
        [Serializable] public class Entry { public RouteSectionLayout[] layouts = new RouteSectionLayout[4]; }
        [Serializable] public class Archive { public Entry[] sections = new Entry[9]; }
        public const string FileName = "Temp/three-way-access.json";
        public const string FloodFileName = "Temp/three-way-flood-access.json";
        public static string BakeFlood()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
            var s=SibucaoSession.Instance;var manager=s.physicalDecisions;var director=manager.GetComponent<SibucaoRouteChoices>();
            var saved=s.state;var home=s.home;var mode=s.Mode;var archive=new Archive();
            for(int i=0;i<9;i++)archive.sections[i]=new Entry{layouts=new RouteSectionLayout[20]};
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var flood=s.GetComponent<FloodSimulationManager>();var ft=typeof(FloodSimulationManager);
            try
            {
                s.home=null;s.SetMode(SibucaoMode.Playing);
                for(int level=1;level<=5;level++)for(int h=0;h<4;h++)
                {
                    foreach(var section in director.sections)section.station.FloodAdapter.ResetPresentation();
                    s.state=new RunState{homeIndex=h,difficultyLevel=level,homeSeconds=960};manager.EnsureNodeCapacity();
                    manager.Data.routeStart=s.route.Progress(s.homes[h].standingPoint.position);manager.Layout();
                    ft.GetProperty("SimulatedSeconds").GetSetMethod(true).Invoke(flood,new object[]{960f});
                    ft.GetMethod("EnforceDepthLimit",flags).Invoke(flood,new object[]{960f});
                    ft.GetMethod("RebuildSurface",flags).Invoke(flood,null);
                    for(int tick=0;tick<650;tick++)foreach(var section in director.sections)
                        typeof(SibucaoRouteSection).GetMethod("Update",flags).Invoke(section,null);
                    foreach(var section in director.sections)typeof(FloodEncounterAdapter).GetMethod("Update",flags).Invoke(section.station.FloodAdapter,null);
                    Physics.SyncTransforms();
                    foreach(var section in director.sections)
                    {
                        // Extra corner clearance for the plaster-house turn and the radio-table exit.
                        bool wideCorner=section.station.index==1&&h==0||section.station.index==3&&h==3;
                        float height=section.left.transform.position.y;var grid=new Grid(section.station.transform,height,wideCorner?.42f:.29f);
                        var start=grid.Near(section.Approach);var merge=grid.Near(section.Merge);
                        var onward=grid.Find(start,merge,c=>c.y>=16 && Vector2Int.Distance(c,merge)<24);
                        if(onward==null)foreach(float side in new[]{-4f,4f,-8f,8f,-12f,12f})
                        {
                            start=grid.Near(section.Approach+section.station.transform.right*side);
                            onward=grid.Find(start,merge,c=>c.y>=16 && Vector2Int.Distance(c,merge)<24);
                            if(onward!=null)break;
                        }
                        if(onward==null)throw new InvalidOperationException("No shared high-water meeting point D"+level+" H"+h+" B"+(section.station.index+1));
                        merge=grid.Cell(onward[onward.Length-1]);
                        var layout=new RouteSectionLayout{home=h,difficulty=level,approach=grid.Point(start),merge=grid.Point(merge),left=section.left.transform.position,right=section.right.transform.position};
                        foreach(var mission in new[]{section.left,section.right})
                        {
                            var incoming=FirstAction(grid,start,mission);
                            if(incoming==null)throw new InvalidOperationException("No high-water first action D"+level+" H"+h+" "+mission.id);
                            var outgoing=grid.Find(grid.Cell(incoming[incoming.Length-1]),merge,c=>c==merge);
                            if(outgoing==null)
                            {
                                var fallback=new List<Vector3>(incoming);fallback.Reverse();
                                fallback.AddRange(onward);outgoing=fallback.ToArray();
                            }
                            if(mission.side==0){layout.leftApproach=incoming;layout.leftPath=outgoing;}
                            else{layout.rightApproach=incoming;layout.rightPath=outgoing;}
                        }
                        archive.sections[section.station.index].layouts[(level-1)*4+h]=layout;
                    }
                }
                File.WriteAllText(FloodFileName,JsonUtility.ToJson(archive));return "Baked 360 raised approaches and 360 raised exits across five difficulties.";
            }
            finally{foreach(var section in director.sections)section.station.FloodAdapter.ResetPresentation();s.state=saved;s.home=home;manager.EnsureNodeCapacity();manager.Layout();s.SetMode(mode);}
        }
        static Vector3[] FirstAction(Grid grid,Vector2Int start,SibucaoSideMission mission)
        {
            var item=mission.steps[0].item;var col=item.GetComponentInChildren<Collider>();var aim=col?col.bounds.center:item.position+Vector3.up;
            return grid.Find(start,grid.Cell(aim),c=>{
                var eye=grid.Point(c)+Vector3.up*1.6f;if(Vector3.Distance(eye,aim)>2.7f)return false;
                var delta=aim-eye;var hits=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.02f,~0,QueryTriggerInteraction.Ignore);
                Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits){if(Grid.Ignore(hit.collider))continue;return hit.transform==item||hit.transform.IsChildOf(item);}return true;
            });
        }
        public static string Bake()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var s = SibucaoSession.Instance; var manager = s.physicalDecisions;
            var director = manager.GetComponent<SibucaoRouteChoices>();
            var saved = s.state; var home = s.home; var mode = s.Mode;
            var archive = new Archive(); for (int i=0;i<9;i++) archive.sections[i]=new Entry();
            try
            {
                s.home=null; s.SetMode(SibucaoMode.Paused);
                for(int h=0;h<4;h++)
                {
                    s.state=new RunState { homeIndex=h }; manager.EnsureNodeCapacity();
                    manager.Data.routeStart=s.route.Progress(s.homes[h].standingPoint.position); manager.Layout();
                    Physics.SyncTransforms();
                    foreach(var section in director.sections)
                    {
                        var layout=JsonUtility.FromJson<RouteSectionLayout>(JsonUtility.ToJson(section.layouts[h]));
                        var grid=new Grid(section.station.transform);
                        var start=grid.Near(layout.approach); var merge=grid.Near(layout.merge);
                        layout.approach=grid.Point(start); layout.merge=grid.Point(merge);
                        foreach(var mission in new[]{section.left,section.right})
                        {
                            var item=mission.steps[0].item; var collider=item.GetComponentInChildren<Collider>();
                            var aim=collider?collider.bounds.center:item.position+Vector3.up;
                            var incoming=grid.Find(start,grid.Cell(aim),c=>
                            {
                                var eye=grid.Point(c)+Vector3.up*1.6f;
                                if(Vector3.Distance(eye,aim)>2.7f)return false;
                                var delta=aim-eye;
                                var hits=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.02f,~0,QueryTriggerInteraction.Ignore);
                                Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                                foreach(var hit in hits) { if(Grid.Ignore(hit.collider))continue;return hit.transform==item||hit.transform.IsChildOf(item); }
                                return true;
                            });
                            if(incoming==null)throw new InvalidOperationException("No reachable first action H"+h+" "+mission.id);
                            var outgoing=grid.Find(grid.Cell(incoming[incoming.Length-1]),merge,c=>c==merge);
                            if(outgoing==null)throw new InvalidOperationException("No onward passage H"+h+" "+mission.id);
                            if(mission.side==0){layout.leftApproach=incoming;layout.leftPath=outgoing;}
                            else {layout.rightApproach=incoming;layout.rightPath=outgoing;}
                        }
                        archive.sections[section.station.index].layouts[h]=layout;
                    }
                }
                File.WriteAllText(FileName,JsonUtility.ToJson(archive));
                return "Baked 72 first-action access paths and 72 onward paths against placed geometry.";
            }
            finally {s.state=saved;s.home=home;manager.EnsureNodeCapacity();manager.Layout();s.SetMode(mode);}
        }
        sealed class Grid
        {
            readonly Transform station;
            readonly float? height;
            readonly float radius;
            readonly Dictionary<Vector2Int,Vector3> points=new Dictionary<Vector2Int,Vector3>();
            readonly HashSet<Vector2Int> blocked=new HashSet<Vector2Int>();
            public Grid(Transform s,float? waterHeight=null,float clearance=.29f){station=s;height=waterHeight;radius=clearance;}
            public static bool Ignore(Collider c)=>c.GetComponentInParent<SibucaoPlayer>();
            bool RaisedFloor(Collider c)=>c.GetComponentInParent<PhysicalDecisionStation>() && (c.name.ToLowerInvariant().Contains("deck")||c.name.ToLowerInvariant().Contains("walkway")||c.name.ToLowerInvariant().Contains("ramp")||c.name.ToLowerInvariant().Contains("landing"));
            bool Floor(Collider c)=>c.name.StartsWith("Measured terrain")||c.name=="Felix Amante Drive"||c.name=="Carlos Hilado Highway"||c.name.StartsWith("residential ")||c is TerrainCollider||c.name=="Supported work deck"||c.name=="Raised pedestrian walkway";
            public Vector2Int Cell(Vector3 p){p=station.InverseTransformPoint(p);return new Vector2Int(Mathf.RoundToInt(p.x*2),Mathf.RoundToInt(p.z*2));}
            public Vector3 Point(Vector2Int c){Ground(c,out var p);return p;}
            bool Ground(Vector2Int c,out Vector3 p)
            {
                if(points.TryGetValue(c,out p))return true;if(blocked.Contains(c))return false;
                p=station.TransformPoint(new Vector3(c.x*.5f,0,c.y*.5f));
                if(height.HasValue)
                {
                    p.y=height.Value;
                    foreach(var hit in Physics.RaycastAll(p+Vector3.up*2.1f,Vector3.down,2.15f,~0,QueryTriggerInteraction.Ignore))
                        if((hit.collider.name.Contains("deck")||hit.collider.name.Contains("walkway")||hit.collider.name.Contains("ramp")||hit.collider.name.Contains("landing"))&&
                            hit.collider.GetComponentInParent<PhysicalDecisionStation>()&&hit.normal.y>.8f)
                            p.y=Mathf.Max(p.y,hit.point.y);
                    foreach(var col in Physics.OverlapCapsule(p+Vector3.up*(radius+.04f),p+Vector3.up*(1.82f-radius),radius,~0,QueryTriggerInteraction.Ignore))
                        if(!Ignore(col)&&col.bounds.max.y>p.y+.06f && !(RaisedFloor(col)&&col.ClosestPoint(p+Vector3.up*.34f).y<=p.y+.3f)){blocked.Add(c);return false;}
                    points[c]=p;return true;
                }
                var hits=Physics.RaycastAll(p+Vector3.up*8,Vector3.down,20,~0,QueryTriggerInteraction.Ignore);
                Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));bool found=false;
                foreach(var hit in hits)if(Floor(hit.collider)&&hit.normal.y>.75f){p=hit.point+Vector3.up*.04f;found=true;break;}
                if(found)foreach(var col in Physics.OverlapCapsule(p+Vector3.up*.34f,p+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore))
                    if(!Ignore(col)&&!Floor(col)){found=false;break;}
                if(!found){blocked.Add(c);return false;}points[c]=p;return true;
            }
            public Vector2Int Near(Vector3 p)
            {
                var c=Cell(p);
                if(height.HasValue)for(int r=0;r<16;r++)for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)
                {
                    var q=c+new Vector2Int(x,z);
                    if(!Ground(q,out var at)||at.y>height.Value+.1f)continue;
                    bool open=true;
                    foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                        if(!Ground(q+direction*2,out var next)||Mathf.Abs(next.y-at.y)>.3f){open=false;break;}
                    if(open)return q;
                }
                for(int r=0;r<12;r++)for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)
                {var q=c+new Vector2Int(x,z);if(Ground(q,out _))return q;}
                throw new InvalidOperationException("No walkable point near "+p);
            }
            public Vector3[] Find(Vector2Int start,Vector2Int goal,Func<Vector2Int,bool> reached)
            {
                var open=new List<Vector2Int>{start};var costs=new Dictionary<Vector2Int,float>{{start,0}};
                var parent=new Dictionary<Vector2Int,Vector2Int>();var visited=new HashSet<Vector2Int>();
                while(open.Count>0&&visited.Count<16000)
                {
                    int best=0;float score=float.MaxValue;
                    for(int i=0;i<open.Count;i++){float f=costs[open[i]]+Vector2Int.Distance(open[i],goal);if(f<score){score=f;best=i;}}
                    var c=open[best];open.RemoveAt(best);if(!visited.Add(c))continue;
                    if(reached(c)){var path=new List<Vector3>();for(var p=c;;p=parent[p]){path.Add(Point(p));if(p==start)break;}path.Reverse();return path.ToArray();}
                    for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                    {
                        if(x==0&&z==0)continue;var n=c+new Vector2Int(x,z);
                        if(Mathf.Abs(n.x)>90||Mathf.Abs(n.y)>90||visited.Contains(n))continue;
                        if(n!=goal && n.y>-10&&n.y<16&&Mathf.Abs(n.x)<6)continue;
                        if(!Ground(n,out var p)||Mathf.Abs(p.y-Point(c).y)>.3f)continue;
                        if(x!=0&&z!=0&&(!Ground(c+new Vector2Int(x,0),out _)||!Ground(c+new Vector2Int(0,z),out _)))continue;
                        var move=p-Point(c);bool blockedEdge=false;
                        foreach(var hit in Physics.CapsuleCastAll(Point(c)+Vector3.up*(height.HasValue?radius+.04f:.34f),Point(c)+Vector3.up*(height.HasValue?1.82f-radius:1.5f),radius,move.normalized,move.magnitude,~0,QueryTriggerInteraction.Ignore))
                            if(!Ignore(hit.collider)&&(height.HasValue?hit.collider.bounds.max.y>p.y+.06f && !(RaisedFloor(hit.collider)&&hit.normal.y>.7f):!Floor(hit.collider))){blockedEdge=true;break;}
                        if(blockedEdge)continue;float value=costs[c]+Vector2Int.Distance(c,n);
                        if(costs.TryGetValue(n,out var old)&&old<=value)continue;costs[n]=value;parent[n]=c;open.Add(n);
                    }
                }
                return null;
            }
        }
    }
}


