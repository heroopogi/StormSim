using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    // Reachability after removing interaction areas covers all paths in the baked graph,
    // including cyclic detours. It does not enumerate an arbitrary subset of waypoint routes.
    public static class SibucaoRouteCoverageAudit
    {
        [Serializable] public sealed class HomeResult
        {
            public string home,status;public Vector3 spawn,snappedSpawn,arrival;public float snapDistance;
            public bool fullRoute,zeroEncounterRoute;public Vector3[] fullPath,bypassPath;
            public int reachableWithoutEncounter;public int[] fullTrianglePath,bypassTrianglePath;
        }
        [Serializable] public sealed class Report
        {
            public string generatedUtc,method;public float voxel;public Vector3 boundsCenter,boundsSize;
            public int sourceCount,triangles;public List<HomeResult> homes=new List<HomeResult>();
        }
        [Serializable] public sealed class Graph{public Vector3[] vertices;public int[] triangles,areas;}
        public static string Status{get;private set;}="Idle";
        public static NavMeshData Data{get;private set;}
        static NavMeshDataInstance instance;static AsyncOperation build;
        static SibucaoSession session;static Report report;static string output;
        static int currentHome;static bool includeDeparture;static List<NavMeshBuildSource> baseSources;static Bounds buildBounds;static NavMeshBuildSettings buildSettings;
        const string Folder="Documentation/RouteCoverage";
        [MenuItem("STORMSIM/Verification/Full route coverage (Sibucao)")]
        public static void RunCurrentScene(){Start(.12f,"current");}
        public static string Start(float voxel=.12f,string label="current",bool includeDepartureCuts=true)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode for the geometry audit.");
            if(build!=null&&!build.isDone)throw new InvalidOperationException("An audit is already building.");
            Cleanup();session=Object.FindFirstObjectByType<SibucaoSession>();Directory.CreateDirectory(Folder);output=label;includeDeparture=includeDepartureCuts;
            if(includeDeparture)foreach(var gate in session.physicalDecisions.stations.Where(s=>s.departureEncounter).Select(s=>s.GetComponent<SibucaoCoverageGate>()))ValidateGate(gate);
            Physics.SyncTransforms();var colliders=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&!c.isTrigger).ToArray();
            var bounds=colliders[0].bounds;foreach(var c in colliders)bounds.Encapsulate(c.bounds);bounds.Expand(4);
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            // Movable characters/supplies are not reliable permanent barriers. Decision geometry
            // is omitted to conservatively permit every physical branch. Home doors are open.
            sources.RemoveAll(src=>src.component is Collider c&&(c.isTrigger||c.attachedRigidbody||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>()||c.GetComponentInParent<PhysicalDecisionStation>()||session.homes.Any(h=>h.doorPanel&&c.transform.IsChildOf(h.doorPanel.transform))));
            var settings=NavMesh.GetSettingsByIndex(0);var player=session.player.GetComponent<CharacterController>();
            // A conservative movement envelope includes the existing narrow squeeze and
            // the player's 5.5 m/s jump (20 m/s2 gravity). This over-approximates walking;
            // a passing coverage cut is stronger, while reachability needs a Play Mode witness.
            settings.agentRadius=.10f;settings.agentHeight=Mathf.Min(player.height,1.25f);settings.agentClimb=.75f;settings.agentSlope=player.slopeLimit;
            settings.overrideVoxelSize=true;settings.voxelSize=voxel;settings.overrideTileSize=true;settings.tileSize=128;settings.minRegionArea=0;
            // A CharacterController can drop off a ledge. Include generated drop links;
            // otherwise the audit can mistake a one-way descent for a closed road.
            settings.ledgeDropHeight=2;settings.maxJumpAcrossDistance=0;
            for(int i=0;i<sources.Count;i++){var src=sources[i];src.generateLinks=true;sources[i]=src;}
            report=new Report{generatedUtc=DateTime.UtcNow.ToString("O"),method="Full-scene collider graph, separately annotated for each home. Conservative radius 0.10m, jump step 0.75m, 1.25m swim height, actual slope limit. Movable actors/items, open home doors and decision geometry are omitted from obstruction bake. Departure cut volumes enabled="+includeDeparture+". Existing solid hazard footprints also mark area 2. Dry streets and optional conversations are not gates. Unbounded graph BFS joins shared vertices conservatively and targets a superset of the whole arrival volume. All path combinations are covered by reachability after removing area 2; full-route physical traversability is a separate Play Mode test.",voxel=voxel,boundsCenter=bounds.center,boundsSize=bounds.size,sourceCount=sources.Count};
            baseSources=sources;buildBounds=bounds;buildSettings=settings;currentHome=0;Data=new NavMeshData(settings.agentTypeID);BeginHome();EditorApplication.update+=Poll;return Status;
        }
        static void BeginHome()
        {
            var sources=new List<NavMeshBuildSource>(baseSources);float start=session.route.Progress(session.homes[currentHome].standingPoint.position);
            foreach(var st in session.physicalDecisions.stations)
            {
                var meta=st.GetComponent<SibucaoDiegeticHazard>();
                if(st.supplementary&&((st.homeMask&(1<<currentHome))==0||!st.departureEncounter&&session.route.Progress(st.transform.position)<=start+8))continue;
                var gate=st.GetComponent<SibucaoCoverageGate>();
                if(gate){if(includeDeparture)Box(sources,gate.transform.TransformPoint(gate.center),gate.size,gate.transform.rotation);continue;}
                bool physical=st.supplementary?meta&&(meta.kind==SibucaoHazardKind.FallenTree||meta.kind==SibucaoHazardKind.CollapsedRoof||meta.kind==SibucaoHazardKind.LiveWire||meta.kind==SibucaoHazardKind.LGUCordon):st.index==1||st.index==3;
                if(!physical)continue;
                Vector3 position=st.transform.position;Quaternion rotation=st.transform.rotation;
                if(!st.supplementary){float d=start+(session.route.length-start-12)*(st.index+1)/10f;position=session.route.Sample(d);var forward=session.route.Sample(d+2)-session.route.Sample(d-2);forward.y=0;rotation=Quaternion.LookRotation(forward);}
                foreach(var c in st.GetComponentsInChildren<Collider>(true))
                {
                    if(c.attachedRigidbody||c.GetComponentInParent<PhysicalGrabbable>()||c.isTrigger&&!c.GetComponent<SibucaoElectricalContact>())continue;
                    if(c.bounds.size.sqrMagnitude<.001f)continue;var local=st.transform.InverseTransformPoint(c.bounds.center);
                    // Generous footprint: counting this as covered favors the old build.
                    Box(sources,position+rotation*local,new Vector3(c.bounds.size.x+.1f,6,c.bounds.size.z+.1f),Quaternion.identity);
                }
            }
            build=NavMeshBuilder.UpdateNavMeshDataAsync(Data,buildSettings,sources,buildBounds);Status="Building home "+(currentHome+1)+" / 4 full-scene graph";
        }
        public static void Box(List<NavMeshBuildSource> sources,Vector3 center,Vector3 size,Quaternion rotation)
            =>sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.ModifierBox,transform=Matrix4x4.TRS(center,rotation,Vector3.one),size=size,area=2});
        static void ValidateGate(SibucaoCoverageGate gate)
        {
            if(!gate||!gate.station||!gate.debris||!gate.debris.moving||gate.station.branches.Length!=2||!gate.station.branches.Any(b=>b&&b.requiredPassage==gate.debris)||!gate.station.branches.Any(b=>b&&b.mechanism==PhysicalMechanism.Squeeze))throw new InvalidOperationException("Departure gate has missing physical bindings.");
            if(gate.size.x>1.5f||gate.size.z>.8f)throw new InvalidOperationException(gate.name+": audit volume exceeds the authored doorway.");
            Physics.SyncTransforms();
            // Never count a marker whose visible obstruction was deleted/disabled.
            if(!gate.debris.moving.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy)||!gate.debris.moving.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger))throw new InvalidOperationException(gate.name+": no active visible collision geometry.");
            foreach(float height in new[]{1.8f,1.25f})foreach(float lift in new[]{0,.4f,.75f})for(float x=-.55f;x<.56f;x+=.11f)
            {
                var p=gate.transform.TransformPoint(x,lift,-.9f);
                bool blocked=Physics.CapsuleCastAll(p+Vector3.up*.27f,p+Vector3.up*(height-.27f),.27f,gate.transform.forward,1.8f,~0,QueryTriggerInteraction.Ignore).Any(hit=>
                {
                    var c=hit.collider;if(hit.normal.y>.6f||c.attachedRigidbody||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>())return false;
                    return !session.homes.Any(h=>h.doorPanel&&c.transform.IsChildOf(h.doorPanel.transform));
                });
                if(!blocked)throw new InvalidOperationException(gate.name+": unengaged capsule can cross at x="+x+", height="+height+", lift="+lift);
            }
        }
        static void Poll()
        {
            if(build==null||!build.isDone)return;
            try
            {
                instance=NavMesh.AddNavMeshData(Data);var graph=NavMesh.CalculateTriangulation();report.triangles=graph.indices.Length/3;
                File.WriteAllText(Folder+"/"+output+"-graph-h"+(currentHome+1)+".json",JsonUtility.ToJson(new Graph{vertices=graph.vertices,triangles=graph.indices,areas=graph.areas}));
                foreach(var home in new[]{session.homes[currentHome]})
                {
                    var r=new HomeResult{home=home.name,spawn=home.standingPoint.position,arrival=session.physicalDecisions.evacuationCenter.arrivalPoint.position};
                    if(!NavMesh.SamplePosition(r.spawn,out var from,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(r.arrival,out var to,3,NavMesh.AllAreas)){r.status="FAIL: spawn/arrival outside graph";report.homes.Add(r);continue;}
                    r.snappedSpawn=from.position;r.snapDistance=Vector3.Distance(r.spawn,from.position);var path=new NavMeshPath();
                    r.fullRoute=NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;r.fullPath=path.corners;
                    var coverage=new SibucaoCoverageGraph(graph);
                    var fullWitness=coverage.Search(r.spawn,session.physicalDecisions.evacuationCenter,false,out _,out r.fullTrianglePath);
                    var bypass=coverage.Search(r.spawn,session.physicalDecisions.evacuationCenter,true,out r.reachableWithoutEncounter,out r.bypassTrianglePath);
                    r.fullRoute=fullWitness.Length>0;r.zeroEncounterRoute=bypass.Length>0;r.bypassPath=bypass;
                    if(r.fullPath==null||r.fullPath.Length==0||path.status!=NavMeshPathStatus.PathComplete)r.fullPath=fullWitness;
                    r.status=!r.fullRoute?"FAIL: no full route":r.zeroEncounterRoute?"FAIL: zero-encounter route exists":"PASS: no zero-encounter route in graph";report.homes.Add(r);
                }
                File.WriteAllText(Folder+"/"+output+".json",JsonUtility.ToJson(report,true));Status=string.Join(" | ",report.homes.Select(r=>r.home+": "+r.status));
            }
            catch(Exception e){Status="FAIL "+e;File.WriteAllText(Folder+"/"+output+"-error.txt",Status);}
            finally{if(instance.valid)instance.Remove();build=null;}
            if(++currentHome<4)BeginHome();else{EditorApplication.update-=Poll;Debug.Log("STORMSIM ROUTE COVERAGE: "+Status);}
        }
        public static void Cleanup(){if(instance.valid)instance.Remove();if(Data)Object.DestroyImmediate(Data);Data=null;EditorApplication.update-=Poll;}
    }
}
