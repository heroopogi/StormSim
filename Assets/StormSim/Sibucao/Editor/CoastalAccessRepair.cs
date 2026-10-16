using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using StormSim.Preparedness;

public static class CoastalAccessRepair
{
    [Serializable] public class Connection { public int home,difficulty,after; public Vector3[] points; }
    [Serializable] public class Archive { public int schema=1;public Connection[] connections; }
    static float Ground(Vector3 p)=>CoastalWalkthroughRoutes.Surface(p);
    public static string RemoveObsoleteFootings()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        int count=0;
        foreach(var st in s.physicalDecisions.stations.Where(x=>x&&x.index<9))foreach(var b in st.branches)
        {
            if(b.mechanism==PhysicalMechanism.Mantle||b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.BoardBoat||b.mechanism==PhysicalMechanism.RepairStrap)continue;
            foreach(var t in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Layout ")&&t.name.EndsWith(" - physical footing")).ToArray())
            {UnityEngine.Object.DestroyImmediate(t.gameObject);count++;}
            b.layoutPresentation=Array.Empty<GameObject>();
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        return "Removed "+count+" obsolete generated ground-path presentations; mapped terrain retained.";
    }
    public static string RefitSideConnections()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var reports=new List<string>();
        foreach(var sec in s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections)
        {
            var original=sec.layouts.First(l=>l.home==0);
            for(int side=0;side<2;side++)
            {
                var target=side==0?original.left:original.right;
                var other=side==0?original.right:original.left;
                Vector3[] approach=null;
                var candidates=new List<Vector3>{target};
                for(int radius=2;radius<=50;radius+=2)for(int angle=0;angle<360;angle+=30)candidates.Add(target+Quaternion.Euler(0,angle,0)*Vector3.forward*radius);
                foreach(var candidate in candidates)
                {
                    var at=candidate;try{at.y=Ground(at);}catch{continue;}
                    if(SibucaoRouteChoices.Flat(at,other)<7)continue;
                    if(SibucaoRouteChoices.Flat(at,sec.Approach)<4.25f||SibucaoRouteChoices.Flat(at,sec.Merge)<4.25f||s.route.points.Any(p=>SibucaoRouteChoices.Flat(at,p)<4.25f))continue;
                    if(sec.station.branches.SelectMany(b=>b.path).Any(t=>SibucaoRouteChoices.Flat(at,t.position)<4.25f))continue;
                    bool clear=true;
                    for(float x=-2.5f;x<=2.5f&&clear;x+=1.25f)for(float z=-2.5f;z<=3&&clear;z+=1.25f)
                    {
                        var q=at+sec.station.transform.rotation*new Vector3(x,0,z);
                        try{if(Mathf.Abs(Ground(q)-at.y)>.5f){clear=false;break;}}catch{clear=false;break;}
                        if(Physics.RaycastAll(new Vector3(q.x,70,q.z),Vector3.down,140,~0,QueryTriggerInteraction.Ignore).Any(h=>h.point.y>q.y+1.9f&&(h.collider.name.StartsWith("Building ")||h.collider.name=="metal"||h.collider.name=="plaster"))){clear=false;break;}
                        clear=!Physics.OverlapCapsule(q+Vector3.up*.35f,q+Vector3.up*2.2f,.35f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.name.StartsWith("Building ")||c.name=="metal"||c.name=="plaster"||c.name.Contains("raised timber floors"));
                    }
                    if(!clear)continue;
                    try{var path=CoastalWalkthroughRoutes.Plan(sec.Approach,at,100000,true);if(SibucaoRouteChoices.Flat(path.Last(),at)>.4f)continue;approach=path;break;}catch{}
                }
                if(approach==null)throw new Exception("No clear side workspace at B"+(sec.station.index+1)+" side "+side);
                var fitted=approach.Last();
                var departure=CoastalWalkthroughRoutes.Plan(fitted,sec.Merge,100000,true);
                foreach(var layout in sec.layouts.Concat(sec.floodLayouts??Array.Empty<RouteSectionLayout>()))
                {
                    if(side==0){layout.left=fitted;layout.leftApproach=(Vector3[])approach.Clone();layout.leftPath=(Vector3[])departure.Clone();}
                    else{layout.right=fitted;layout.rightApproach=(Vector3[])approach.Clone();layout.rightPath=(Vector3[])departure.Clone();}
                }
                var mission=side==0?sec.left:sec.right;mission.transform.position=fitted;mission.route=departure;
                reports.Add("B"+(sec.station.index+1)+" side "+side+": "+approach.Length+" approach / "+departure.Length+" departure points; moved "+Vector3.Distance(target,fitted).ToString("F2")+" m");
            }
        }
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        File.WriteAllLines("Reports/CoastalAreas/Walkthrough/"+s.gameObject.scene.name+"/side-refit.txt",reports);
        return string.Join("\n",reports);
    }

    public static string FreezeMapReviews()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var states=s.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        int count=0;foreach(var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(b&&(b is BanagoLocalDetailReview||b is BanagoHouseDetailTest)&&b.enabled){b.enabled=false;count++;}
        foreach(var state in states)if(state.Key)state.Key.SetActive(state.Value);
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Preserved authored map visibility; disabled "+count+" reversible review toggles.";
    }
    public static string FitJunctionMarkers()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var sections=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.OrderBy(x=>x.station.index).ToArray();
        Vector3 At(Vector3[] path,float distance){for(int i=1;i<path.Length;i++){float d=Vector3.Distance(path[i-1],path[i]);if(distance<=d)return Vector3.Lerp(path[i-1],path[i],distance/Mathf.Max(.001f,d));distance-=d;}return path.Last();}
        var paths=new List<Vector3[]>();
        paths.Add(CoastalWalkthroughRoutes.Plan(s.homes[0].transform.TransformPoint(new Vector3(2.5f,0,-4.05f)),sections[0].station.transform.position));
        for(int i=0;i<sections.Length;i++)paths.Add(CoastalWalkthroughRoutes.Plan(sections[i].station.transform.position,i+1<sections.Length?sections[i+1].station.transform.position:s.physicalDecisions.evacuationCenter.arrivalPoint.position));
        for(int i=0;i<sections.Length;i++)
        {
            var sec=sections[i];var approach=At(paths[i].Reverse().ToArray(),8);var merge=At(paths[i+1],10);
            sec.approachMarker.position=approach;sec.mergeMarker.position=merge;
            foreach(var l in sec.layouts.Concat(sec.floodLayouts??Array.Empty<RouteSectionLayout>())){l.approach=l.entryGround=approach;l.merge=l.exitGround=merge;if(l.leftApproach?.Length>0)l.leftApproach[0]=approach;if(l.rightApproach?.Length>0)l.rightApproach[0]=approach;}
        }
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Junction approaches and exits follow actual street bends.";
    }
    public static string FitEncounterSites()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var sections=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.OrderBy(x=>x.station.index).ToArray();var messages=new List<string>();
        var anchor=s.homes[0].transform.TransformPoint(new Vector3(2.5f,0,-4.05f));
        foreach(var section in sections)
        {
            var st=section.station;var origin=st.transform.position;Vector3? chosen=null;
            var candidates=new List<Vector3>{origin};for(int radius=2;radius<=26;radius+=2)for(int angle=0;angle<360;angle+=30)candidates.Add(origin+Quaternion.Euler(0,angle,0)*Vector3.forward*radius);
            foreach(var candidate in candidates)
            {
                bool clear=true;
                for(float x=-3.5f;x<=3.5f&&clear;x+=1.75f)for(float z=-5;z<=7&&clear;z+=2)
                {
                    var p=candidate+st.transform.rotation*new Vector3(x,0,z);p.y=Ground(p);
                    var roof=Physics.RaycastAll(new Vector3(p.x,70,p.z),Vector3.down,140,~0,QueryTriggerInteraction.Ignore).Any(h=>h.point.y>p.y+1.9f&&(h.collider.name.StartsWith("Building ")||h.collider.name=="metal"||h.collider.name=="plaster"));
                    if(roof){clear=false;break;}
                    foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.32f,~0,QueryTriggerInteraction.Ignore))
                        if(c.name.StartsWith("Building ")||c.name=="plaster"||c.name=="metal"||c.name.Contains("raised timber floors")){clear=false;break;}
                }
                if(!clear)continue;
                try{var path=CoastalWalkthroughRoutes.Plan(anchor,candidate,100000,true);if(SibucaoRouteChoices.Flat(path.Last(),candidate)>.5f)continue;chosen=path.Last();break;}catch{}
            }
            if(!chosen.HasValue)throw new Exception("No open working area near B"+(st.index+1));
            if(SibucaoRouteChoices.Flat(origin,chosen.Value)>.2f||Mathf.Abs(origin.y-chosen.Value.y)>.1f)messages.Add(RelocateSection(st.index,chosen.Value,st.transform.rotation));
            anchor=chosen.Value;
        }
        return string.Join("\n",messages);
    }
    public static string BakeRoutes()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();var profile=s.GetComponent<CoastalAreaProfile>();
        if(EditorApplication.isPlaying||!profile)throw new Exception("Coastal edit mode required");
        var sections=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.OrderBy(x=>x.station.index).ToArray();
        var homes=new List<Vector3[]>();var streets=new List<Vector3[]>();var crossings=new List<Vector3[]>();
        foreach(var h in s.homes)homes.Add(CoastalWalkthroughRoutes.Plan(h.transform.TransformPoint(new Vector3(2.5f,0,-4.05f)),sections[0].Approach));
        for(int i=0;i<sections.Length;i++)
        {
            crossings.Add(CoastalWalkthroughRoutes.Plan(sections[i].Approach,sections[i].Merge));
            streets.Add(CoastalWalkthroughRoutes.Plan(sections[i].Merge,i+1<sections.Length?sections[i+1].Approach:s.physicalDecisions.evacuationCenter.arrivalPoint.position,100000,true,true));
        }
        // Prepare every path before touching serialized scene or route data.
        var route=new List<Vector3>(homes[0]);
        for(int i=0;i<sections.Length;i++){route.AddRange(crossings[i].Skip(1));route.AddRange(streets[i].Skip(1));}
        var folder="Assets/StormSim/Areas/"+profile.areaName;var backup="Reports/CoastalAreas/Backups/access-repair/"+profile.areaName+"Route.json";
        if(!File.Exists(backup))File.WriteAllText(backup,JsonUtility.ToJson(s.route,true));
        var oldLength=s.route.length;s.route.points=route.ToArray();s.route.length=0;
        for(int i=1;i<route.Count;i++)s.route.length+=Vector3.Distance(route[i-1],route[i]);
        s.route.spawnPoints=s.route.spawnDistances.Select(d=>s.route.Sample(d)+Vector3.up*.1f).ToArray();
        foreach(var st in s.physicalDecisions.stations.Where(x=>x))st.routeDistance=s.route.Progress(st.transform.position);
        foreach(var e in s.encounters)if(e)e.routeDistance=e.arrival?s.route.length:s.route.Progress(e.transform.position);
        foreach(var r in UnityEngine.Object.FindObjectsByType<SibucaoResident>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {r.initialDistance=r.initialDistance/oldLength*s.route.length;r.routeDistance=r.routeDistance/oldLength*s.route.length;}
        var list=new List<Connection>();
        for(int h=0;h<4;h++)for(int d=1;d<=5;d++)
        {list.Add(new Connection{home=h,difficulty=d,after=-1,points=homes[h]});for(int i=0;i<streets.Count;i++)list.Add(new Connection{home=h,difficulty=d,after=i,points=streets[i]});}
        var file=folder+"/StreetConnections.json";var oldConnections="Reports/CoastalAreas/Backups/access-repair/"+profile.areaName+"StreetConnections.json";
        if(!File.Exists(oldConnections))File.Copy(file,oldConnections);
        File.WriteAllText(file,JsonUtility.ToJson(new Archive{connections=list.ToArray()}));AssetDatabase.ImportAsset(file);profile.streetConnections=AssetDatabase.LoadAssetAtPath<TextAsset>(file);
        var flood=s.GetComponent<FloodSimulationManager>();if(flood)flood.groundSamples=s.route.points;
        EditorUtility.SetDirty(s.route);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        return profile.areaName+": four home connections, nine street connections, nine crossings; route "+s.route.length.ToString("F1")+" m";
    }
    public static string RefitBranchPaths(int minimumIndex=0,int maximumIndex=8,int choice=-1)
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var reports=new List<string>();
        foreach(var st in s.physicalDecisions.stations.Where(x=>x&&x.index>=minimumIndex&&x.index<=maximumIndex))foreach(var b in st.branches.Where(b=>choice<0||b.choiceIndex==choice))
        {
            // These mechanisms require the authored heights of their vehicle or obstacle.
            if(b.mechanism==PhysicalMechanism.Mantle||b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.BoardBoat||b.mechanism==PhysicalMechanism.RepairStrap)continue;
            try
            {
                var original=b.layouts.First(x=>x.home==0).points.Select(p=>b.transform.TransformPoint(p)).ToArray();
                var result=new List<Vector3>();var current=original[0];
                for(int i=1;i<original.Length;i++)
                {
                    // Keep the ordered detour landmarks, routing around intervening mapped walls.
                    try{var part=CoastalWalkthroughRoutes.Plan(current,original[i],6000,true);result.AddRange(part.Skip(result.Count==0?0:1));current=part.Last();}
                    catch{if(i==original.Length-1)throw;}
                }
                if(result.Count<2)throw new Exception("No traversable path");
                var simplified=new List<Vector3>{result[0]};
                for(int i=1;i<result.Count-1;i++)
                {var a=result[i]-simplified.Last();var c=result[i+1]-result[i];if(a.magnitude>=1.5f||Vector3.Angle(a,c)>2)simplified.Add(result[i]);}
                simplified.Add(result.Last());
                var local=simplified.Select(p=>b.transform.InverseTransformPoint(p)).ToArray();
                b.layouts=Enumerable.Range(0,4).Select(h=>new PhysicalPathVariant{home=h,points=(Vector3[])local.Clone()}).ToArray();
                foreach(var t in b.path.Where(t=>t).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                b.path=local.Select((p,i)=>{var t=new GameObject("Physical step "+i).transform;t.SetParent(b.transform,false);t.localPosition=p;return t;}).ToArray();
                if(b.destination)b.destination.transform.position=simplified.Last()+Vector3.up*1.5f;
                reports.Add("B"+(st.index+1)+" C"+b.choiceIndex+": fitted "+local.Length+" points");
                File.WriteAllLines("Reports/CoastalAreas/Walkthrough/"+s.gameObject.scene.name+"/branch-refit-progress.txt",reports);
            }
            catch(Exception e){reports.Add("B"+(st.index+1)+" C"+b.choiceIndex+": FAILED "+e.Message);}
        }
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        var text=string.Join("\n",reports);File.WriteAllText("Reports/CoastalAreas/Walkthrough/"+s.gameObject.scene.name+"/branch-refit.txt",text);return text;
    }
    public static string MoveSection(int index,Vector3 delta)
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var sec=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.First(x=>x.station.index==index);
        sec.station.transform.position+=delta;
        foreach(var l in sec.layouts.Concat(sec.floodLayouts??Array.Empty<RouteSectionLayout>()))
        {l.approach+=delta;l.merge+=delta;l.left+=delta;l.right+=delta;l.entryGround+=delta;l.exitGround+=delta;
         if(l.leftPath!=null)l.leftPath=l.leftPath.Select(p=>p+delta).ToArray();if(l.rightPath!=null)l.rightPath=l.rightPath.Select(p=>p+delta).ToArray();
         if(l.leftApproach!=null)l.leftApproach=l.leftApproach.Select(p=>p+delta).ToArray();if(l.rightApproach!=null)l.rightApproach=l.rightApproach.Select(p=>p+delta).ToArray();}
        sec.left.route=sec.layouts[0].leftPath;sec.right.route=sec.layouts[0].rightPath;
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Moved section "+index;
    }
    public static string RelocateSection(int index,Vector3 position,Quaternion rotation)
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(EditorApplication.isPlaying||!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal edit mode required");
        var sec=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.First(x=>x.station.index==index);
        position.y=Ground(position);var map=Matrix4x4.TRS(position,rotation,Vector3.one)*sec.station.transform.worldToLocalMatrix;
        Vector3 P(Vector3 p)=>map.MultiplyPoint3x4(p);
        sec.station.transform.SetPositionAndRotation(position,rotation);
        foreach(var l in sec.layouts.Concat(sec.floodLayouts??Array.Empty<RouteSectionLayout>()))
        {l.approach=P(l.approach);l.merge=P(l.merge);l.left=P(l.left);l.right=P(l.right);l.entryGround=P(l.entryGround);l.exitGround=P(l.exitGround);
         if(l.leftPath!=null)l.leftPath=l.leftPath.Select(P).ToArray();if(l.rightPath!=null)l.rightPath=l.rightPath.Select(P).ToArray();
         if(l.leftApproach!=null)l.leftApproach=l.leftApproach.Select(P).ToArray();if(l.rightApproach!=null)l.rightApproach=l.rightApproach.Select(P).ToArray();}
        sec.left.route=sec.layouts[0].leftPath;sec.right.route=sec.layouts[0].rightPath;
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Relocated section "+index;
    }

    public static string BuildHomeAccess(int index)
    {
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();var profile=s.GetComponent<CoastalAreaProfile>();
        if(EditorApplication.isPlaying||!profile)throw new Exception("Coastal edit mode required");
        var h=s.homes[index];var goal=s.physicalDecisions.GetComponent<SibucaoRouteChoices>().sections.First(x=>x.station.index==0).Approach;
        var requested=h.transform.TransformPoint(new Vector3(2.5f,0,-4.05f));
        var path=CoastalWalkthroughRoutes.Plan(requested,goal);
        if(SibucaoRouteChoices.Flat(path[0],requested)>.65f)throw new Exception("Door exit is blocked: "+h.name);
        var door=h.transform.TransformPoint(new Vector3(2.5f,.035f,-3.35f));
        var strip=new List<Vector3>{door};var distance=0f;var first=path[0];first.y=door.y;strip.Add(first);
        for(int i=1;i<path.Length;i++)
        {distance+=Vector3.Distance(path[i-1],path[i]);var p=path[i];p.y=Mathf.Lerp(door.y,p.y,Mathf.Clamp01(distance/4f));strip.Add(p);if(distance>=4)break;}
        var name="Ground coastal home access "+index;var old=GameObject.Find(name);if(old)UnityEngine.Object.DestroyImmediate(old);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<strip.Count;i++)
        {
            var tangent=strip[Mathf.Min(i+1,strip.Count-1)]-strip[Mathf.Max(i-1,0)];tangent.y=0;
            var right=Vector3.Cross(Vector3.up,tangent.normalized)*.44f;
            var a=strip[i]-right;var b=strip[i]+right;
            vertices.Add(a);vertices.Add(b);a.y=Mathf.Min(a.y-.1f,Ground(a)-.04f);b.y=Mathf.Min(b.y-.1f,Ground(b)-.04f);vertices.Add(a);vertices.Add(b);
            if(i==0)continue;int k=(i-1)*4,n=i*4;
            triangles.AddRange(new[]{k,n,k+1,k+1,n,n+1,k+2,k,n+2,k,n,n+2,k+1,k+3,n+1,k+3,n+3,n+1});
        }
        triangles.AddRange(new[]{0,2,1,1,2,3});int end=vertices.Count-4;triangles.AddRange(new[]{end,end+1,end+2,end+1,end+3,end+2});
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var folder="Assets/StormSim/Areas/"+profile.areaName+"/ExistingHomes/";var asset=folder+"Home access "+index+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(asset);if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,asset);
        var go=new GameObject(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/Data/PhysicalBranches/Unmarked damp concrete.mat");go.AddComponent<MeshCollider>().sharedMesh=mesh;
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        return h.name+": grounded entrance ramp with "+strip.Count+" sections";
    }
    public static string ReorientHome(int index, int quarterTurns)
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal scene required");
        var h=s.homes[index];var t=h.transform;
        if(t.Find("Street-facing entrance fitted"))throw new Exception("Entrance already refitted");
        // Seal the former opening in the scene copy of the existing building.
        var patch=GameObject.CreatePrimitive(PrimitiveType.Cube);patch.name=h.name+" former doorway wall";
        patch.transform.SetPositionAndRotation(t.TransformPoint(new Vector3(2.5f,1.35f,-3.55f)),t.rotation);
        patch.transform.localScale=Vector3.Scale(t.lossyScale,new Vector3(2.2f,2.9f,.22f));
        patch.GetComponent<Renderer>().sharedMaterial=h.doorPanel.GetComponent<Renderer>().sharedMaterial;
        var old=t.worldToLocalMatrix;var oldRotation=t.rotation;
        t.rotation*=Quaternion.Euler(0,quarterTurns*90,0);
        if(quarterTurns%2!=0){var a=t.localScale;t.localScale=new Vector3(a.z*7/8,a.y,a.x*8/7);}
        foreach(var st in s.physicalDecisions.stations.Where(x=>x&&x.departureEncounter&&(x.homeMask&(1<<index))!=0))
            st.transform.SetPositionAndRotation(t.TransformPoint(old.MultiplyPoint3x4(st.transform.position)),t.rotation*Quaternion.Inverse(oldRotation)*st.transform.rotation);
        foreach(var p in UnityEngine.Object.FindObjectsByType<SibucaoStormPassage>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.home==index))
            p.transform.SetPositionAndRotation(t.TransformPoint(old.MultiplyPoint3x4(p.transform.position)),t.rotation*Quaternion.Inverse(oldRotation)*p.transform.rotation);
        new GameObject("Street-facing entrance fitted").transform.SetParent(t,false);
        return CoastalExistingHomeBuilder.RepairOpenings();
    }

    public static string SetInwardDoors()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        if(!s.GetComponent<CoastalAreaProfile>())throw new Exception("Coastal scene required");
        foreach(var h in s.homes){h.doorOpenAngle=-100;var item=h.doorHinge.GetComponent<PreparednessItem>();if(item)item.openEuler=new Vector3(0,-100,0);}
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        return "Four doors open inward during preparation and storm play.";
    }
}

