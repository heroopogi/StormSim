using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using StormSim.Sibucao;
using StormSim.Preparedness;
using Object=UnityEngine.Object;

public static class CoastalAreaSceneBuilder
{
    const string Source="Assets/Scenes/SIBUCAO_RAIN.unity";
    static Scene scene;
    static SibucaoSession s;
    static SibucaoRoute oldRoute,route;
    static Collider[] floors;
    static string folder;
    static Vector3 V(float x,float z)=>new Vector3(x,0,z);
    static bool FloorName(string n){n=n.ToLowerInvariant();return n.Contains("felix amante")||n.Contains("carlos hilado")||n.StartsWith("ground ")||n.Contains("measured terrain")||n.StartsWith("residential ")||n.StartsWith("junction ")||n.Contains("road surface")||n.Contains("asphalt")||n.Contains("sidewalk")||n.Contains("paved")||n.Contains("court painted")||n.Contains("highway");}
    static float Ground(Vector3 p)
    {
        float y=float.NegativeInfinity;
        foreach(var c in floors){var b=c.bounds;if(p.x<b.min.x||p.x>b.max.x||p.z<b.min.z||p.z>b.max.z)continue;if(c.Raycast(new Ray(new Vector3(p.x,70,p.z),Vector3.down),out var hit,140))y=Mathf.Max(y,hit.point.y);}
        if(!float.IsFinite(y))throw new Exception("No map ground at "+p);
        return y+.035f;
    }
    static Vector3 OnGround(Vector3 p){p.y=Ground(p);return p;}
    static Quaternion Heading(SibucaoRoute r,float d){var f=r.Sample(Mathf.Min(r.length,d+2))-r.Sample(Mathf.Max(0,d-2));f.y=0;return Quaternion.LookRotation(f.sqrMagnitude>.001f?f:Vector3.forward);}
    static Vector3 Map(Vector3 p){float d=oldRoute.Progress(p),nd=d/oldRoute.length*route.length;return route.Sample(nd)+Heading(route,nd)*Quaternion.Inverse(Heading(oldRoute,d))*(p-oldRoute.Sample(d));}
    static void Move(Transform t,Vector3 target,Quaternion rotation){t.SetPositionAndRotation(target,rotation);}
    static void MapRoot(Transform t){float d=oldRoute.Progress(t.position);Move(t,Map(t.position),Heading(route,d/oldRoute.length*route.length)*Quaternion.Inverse(Heading(oldRoute,d))*t.rotation);}
    static IEnumerable<T> All<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    static string Key(Transform t){var names=new List<string>();while(t){names.Add(t.name);t=t.parent;}names.Reverse();return string.Join("/",names);}
    static T Asset<T>(T a,string name) where T:Object{var path=folder+"/"+name;var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(a,old);Object.DestroyImmediate(a);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(a,path);return a;}
    public static string Build(string area)
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        string path="Assets/Scenes/"+area.ToUpperInvariant()+"_RAIN.unity";
        if(File.Exists(path))throw new Exception("Scene already exists; refusing to overwrite: "+path);
        folder="Assets/StormSim/Areas/"+area;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var main=SceneManager.GetSceneByPath("Assets/Scenes/MAIN_GAME.unity");
        if(!main.IsValid())main=EditorSceneManager.OpenScene("Assets/Scenes/MAIN_GAME.unity",OpenSceneMode.Additive);
        if(!AssetDatabase.CopyAsset(Source,path))throw new Exception("Copy failed");
        scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var sourceRoots=scene.GetRootGameObjects();
        var mapNames=new HashSet<string>(main.GetRootGameObjects().Select(g=>g.name));
        var oldMap=sourceRoots.Where(g=>mapNames.Contains(g.name)&&g.name!="Global Volume"&&!g.name.StartsWith("Directional Light")).ToArray();
        var newComponents=new Dictionary<string,Component>();
        foreach(var root in main.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))if(c)newComponents[Key(c.transform)+"|"+c.GetType().FullName]=c;
        var oldMapSet=new HashSet<GameObject>(oldMap);
        foreach(var c in sourceRoots.Where(g=>!oldMapSet.Contains(g)).SelectMany(g=>g.GetComponentsInChildren<Component>(true)))
        {
            if(!c||c is Transform)continue;var so=new SerializedObject(c);var p=so.GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue)
            {
                var target=p.objectReferenceValue as Component;var go=p.objectReferenceValue as GameObject;var t=target?target.transform:go?go.transform:null;
                if(!t||!oldMapSet.Contains(t.root.gameObject))continue;
                if(go){if(newComponents.TryGetValue(Key(t)+"|"+typeof(Transform).FullName,out var replacement))p.objectReferenceValue=replacement.gameObject;}
                else if(newComponents.TryGetValue(Key(t)+"|"+target.GetType().FullName,out var replacement))p.objectReferenceValue=replacement;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var g in oldMap)Object.DestroyImmediate(g);
        foreach(var g in main.GetRootGameObjects())if(g.name!="Global Volume"&&!g.name.StartsWith("Directional Light"))SceneManager.MoveGameObjectToScene(g,scene);
        EditorSceneManager.CloseScene(main,true);
        s=All<SibucaoSession>().Single();oldRoute=s.route;
        var gameplay=sourceRoots.Where(g=>g&&g.name!="Global Volume"&&!g.name.StartsWith("Directional Light")).ToArray();
        var active=gameplay.Select(g=>g.activeSelf).ToArray();foreach(var g in gameplay)g.SetActive(false);
        // The actual MAIN_GAME meshes remain at their original geographic coordinates.
        foreach(var f in All<MeshFilter>().Where(f=>f.sharedMesh&&FloorName(f.name)&&!f.GetComponent<Collider>()))f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
        walkable.Clear();blocked.Clear();
        floors=All<Collider>().Where(c=>c.enabled&&FloorName(c.name)).ToArray();Physics.SyncTransforms();
        var anchors=area=="Riverside"?new[]{V(-174,130),V(2,150),V(2,180),V(5,310),V(8,435),V(10,541),V(0,586),V(-16,619),V(19,643),V(34,652)}:
            new[]{V(155,-930),V(242,-848),V(214,-759),V(190,-691),V(165,-615),V(143,-549),V(150,-518),V(233,-493),V(293,-474),V(359,-451),V(389,-446),V(400,-450)};
        route=ScriptableObject.CreateInstance<SibucaoRoute>();var pts=new List<Vector3>();
        for(int i=0;i<anchors.Length-1;i++){var segment=WalkPath(anchors[i],anchors[i+1]);pts.AddRange(i==0?segment:segment.Skip(1));}route.points=pts.ToArray();route.length=0;for(int i=1;i<pts.Count;i++)route.length+=Vector3.Distance(pts[i-1],pts[i]);route.spawnDistances=new[]{0f,4f,8f,12f};route.spawnPoints=route.spawnDistances.Select(d=>route.Sample(d)+Vector3.up*.1f).ToArray();route=Asset(route,area+"Route.asset");
        for(int i=0;i<gameplay.Length;i++)gameplay[i].SetActive(active[i]);
        var oldHomeStarts=s.homes.Select(h=>oldRoute.Progress(h.standingPoint.position)).ToArray();
        var oldHomePos=s.homes.Select(h=>h.transform.position).ToArray();var oldHomeRot=s.homes.Select(h=>h.transform.rotation).ToArray();
        var homePositions=area=="Riverside"?new[]{V(-184,137),V(-178,155),V(-196,117),V(-177,99)}:new[]{V(149,-931),V(157,-911),V(140,-951),V(155,-968)};
        for(int i=0;i<s.homes.Length;i++){Move(s.homes[i].transform,OnGround(homePositions[i]),Quaternion.Euler(0,area=="Riverside"?90:70,0));s.homes[i].locationName=area+" coastal household "+(i+1);s.homes[i].name=area+" Home "+(i+1);float dy=s.homes[i].transform.position.y-oldHomePos[i].y;s.homes[i].initialWaterHeight+=dy;s.homes[i].finalWaterHeight+=dy;}
        var prep=s.GetComponent<SibucaoPreparationBridge>();var prepDelta=s.homes[0].transform.rotation*Quaternion.Inverse(oldHomeRot[0]);
        Move(prep.content.transform,s.homes[0].transform.position+prepDelta*(prep.content.transform.position-oldHomePos[0]),prepDelta*prep.content.transform.rotation);
        foreach(var root in gameplay)
        {
            if(!root||root==s.gameObject||root==prep.content||s.homes.Any(h=>h.gameObject==root))continue;
            if(root.GetComponentsInChildren<SibucaoStormPassage>(true).Length>0){foreach(var passage in root.GetComponentsInChildren<SibucaoStormPassage>(true))MapRoot(passage.transform);continue;}
            if(root.GetComponent<DecisionTreeManager>()||root.GetComponent<SibucaoDryRefuge>()||root.name=="STORMSIM Crest Water"||root.name.Contains("EventSystem"))continue;
            if(root.name.Contains("School")||root.name.Contains("school classrooms")){root.SetActive(false);continue;}
            if(root.name.Contains("Spawn neighborhood")){Move(root.transform,s.homes[0].transform.position+prepDelta*(root.transform.position-oldHomePos[0]),prepDelta*root.transform.rotation);continue;}
            if(root.name.Contains("MainMenu")){Move(root.transform,s.homes[0].standingPoint.position, s.homes[0].standingPoint.rotation);continue;}
            if(root.transform.childCount>0&&root.transform.position==Vector3.zero){foreach(Transform child in root.transform)MapRoot(child);}else MapRoot(root.transform);
        }
        foreach(var st in s.physicalDecisions.stations)
        {
            if(!st)continue;
            if(st.supplementary){MapRoot(st.transform);st.routeDistance=route.Progress(st.transform.position);continue;}
            float d=Mathf.Max(5,(route.length-12)*(st.index+1)/10f);var oldPos=st.transform.position;var oldRot=st.transform.rotation;
            Move(st.transform,route.Sample(d),Heading(route,d));st.routeDistance=d;
            var section=st.GetComponentInChildren<SibucaoRouteSection>(true);
            if(section)
            {
                foreach(var layout in section.layouts.Concat(section.floodLayouts??Array.Empty<RouteSectionLayout>()))
                {
                    float sd=oldHomeStarts[layout.home]+Mathf.Max(5,(oldRoute.length-oldHomeStarts[layout.home]-12)*(st.index+1)/10f);
                    var pivot=oldRoute.Sample(sd);var rot=Heading(route,d)*Quaternion.Inverse(Heading(oldRoute,sd));
                    Vector3 MapPoint(Vector3 p){var v=route.Sample(d)+rot*(p-pivot);try{v.y=Ground(v);}catch{v=walkable[Nearest(v)];}return v;}
                    layout.approach=MapPoint(layout.approach);layout.left=MapPoint(layout.left);layout.right=MapPoint(layout.right);layout.merge=MapPoint(layout.merge);
                    layout.entryGround=layout.approach;layout.exitGround=layout.merge;layout.leftDeck=layout.rightDeck=layout.fittedRamps=false;
                    if(layout.leftPath!=null)layout.leftPath=layout.leftPath.Select(MapPoint).ToArray();if(layout.rightPath!=null)layout.rightPath=layout.rightPath.Select(MapPoint).ToArray();
                    if(layout.leftApproach!=null)layout.leftApproach=layout.leftApproach.Select(MapPoint).ToArray();if(layout.rightApproach!=null)layout.rightApproach=layout.rightApproach.Select(MapPoint).ToArray();
                }
                var l=section.layouts[0];section.approachMarker.position=l.approach;section.mergeMarker.position=l.merge;section.left.transform.position=l.left;section.right.transform.position=l.right;section.left.route=l.leftPath;section.right.route=l.rightPath;
            }
        }
        foreach(var res in All<SibucaoResident>()){res.route=route;res.initialDistance=res.initialDistance/oldRoute.length*route.length;res.routeDistance=res.routeDistance/oldRoute.length*route.length;}
        foreach(var e in s.encounters){e.routeDistance=e.arrival?route.length:e.routeDistance/oldRoute.length*route.length;Move(e.transform,route.Sample(e.routeDistance),Heading(route,e.routeDistance));}
        s.route=route;s.name=area.ToUpperInvariant()+" RAIN - standalone gameplay";s.physicalDecisions.name=area+" - physical decision systems";s.physicalDecisions.riversideScene="";
        var refuge=s.physicalDecisions.evacuationCenter;Move(refuge.transform,route.points.Last(),Quaternion.identity);refuge.name=area+" evacuation destination";refuge.size=area=="Riverside"?new Vector2(65,55):new Vector2(40,40);refuge.arrivalPoint.position=route.points.Last();
        var flood=s.GetComponent<FloodSimulationManager>();flood.groundSamples=route.points;flood.dryRefuge=refuge;
        BakeFlood(flood);
        if(s.initialPlayerSpawn)s.initialPlayerSpawn.SetPositionAndRotation(s.homes[0].standingPoint.position,s.homes[0].standingPoint.rotation);s.player.transform.SetPositionAndRotation(s.homes[0].standingPoint.position,s.homes[0].standingPoint.rotation);
        var profile=s.gameObject.AddComponent<CoastalAreaProfile>();profile.areaName=area;profile.saveId=area.ToLowerInvariant()+"-rain";
        foreach(var c in All<Camera>())if(c!=s.player.view&&!c.transform.IsChildOf(prep.content.transform))c.enabled=false;
        foreach(var a in All<AudioListener>())if(!a.transform.IsChildOf(s.player.transform)&&!a.transform.IsChildOf(prep.content.transform))a.enabled=false;
        foreach(var b in All<MonoBehaviour>())if(b&&(b.GetType().Name=="BanagoReviewAtmosphere"||b.GetType().Name=="RisingWater"||b.GetType().Name=="TsunamiScenario"||b.GetType().Name=="FloodDetector"))b.enabled=false;
        foreach(var l in All<Light>())if(l.type==LightType.Directional&&l!=s.weather.sun)l.enabled=false;
        var scenes=EditorBuildSettings.scenes.ToList();scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
        CoastalAreaSceneryCleanup.RemoveCopiedInteriors(scene);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/"+area+"-build.json",JsonUtility.ToJson(new BuildReport{area=area,routeLength=route.length,points=route.points.Length,homes=s.homes.Length,stations=s.physicalDecisions.stations.Length},true));
        return path;
    }
    [Serializable]class BuildReport{public string area;public float routeLength;public int points,homes,stations;}
    public static string Finish()
    {
        scene=SceneManager.GetActiveScene();s=All<SibucaoSession>().Single();route=s.route;
        var profile=s.GetComponent<CoastalAreaProfile>();folder="Assets/StormSim/Areas/"+profile.areaName;
        floors=All<Collider>().Where(c=>c.enabled&&FloorName(c.name)).ToArray();Physics.SyncTransforms();walkable.Clear();blocked.Clear();
        var reports=new List<string>();var choices=s.physicalDecisions.GetComponent<SibucaoRouteChoices>();
        foreach(var section in choices.sections)
        {
            var st=section.station;bool active=st.gameObject.activeSelf;st.gameObject.SetActive(false);Physics.SyncTransforms();
            try
            {
                var fitted=StormSim.Sibucao.Editor.SibucaoRouteChoiceBuilder.FitAreaLayout(st.transform.position,st.transform.rotation,st.transform,0);
                if(fitted.leftDeck||fitted.rightDeck)throw new Exception("Unacceptable raised route deck");
                section.layouts=Enumerable.Range(0,4).Select(i=>{var l=JsonUtility.FromJson<RouteSectionLayout>(JsonUtility.ToJson(fitted));l.home=i;return l;}).ToArray();section.floodLayouts=Array.Empty<RouteSectionLayout>();
                section.approachMarker.position=fitted.approach;section.mergeMarker.position=fitted.merge;section.left.transform.position=fitted.left;section.right.transform.position=fitted.right;section.left.route=fitted.leftPath;section.right.route=fitted.rightPath;
                reports.Add("Section "+st.index+": ground-fitted side routes");
            }
            catch(Exception e){reports.Add("Section "+st.index+": NEEDS REVIEW: "+e.Message);}
            finally{st.gameObject.SetActive(active);}
        }
        var archive=new ConnectionArchive{schema=1};var connections=new List<Connection>();
        foreach(var section in choices.sections)
        {
            int next=section.station.index+1;if(next>=choices.sections.Length)continue;
            var from=section.mergeMarker.position;var target=choices.sections.First(c=>c.station.index==next).approachMarker.position;
            var points=new List<Vector3>{from};float start=route.Progress(from),end=route.Progress(target);for(float d=start;d<end;d+=2)points.Add(route.Sample(d));points.Add(target);
            for(int home=0;home<4;home++)for(int difficulty=1;difficulty<=5;difficulty++)connections.Add(new Connection{home=home,difficulty=difficulty,after=section.station.index,points=points.ToArray()});
        }
        archive.connections=connections.ToArray();string file=folder+"/StreetConnections.json";File.WriteAllText(file,JsonUtility.ToJson(archive));AssetDatabase.ImportAsset(file);profile.streetConnections=AssetDatabase.LoadAssetAtPath<TextAsset>(file);
        reports.Add(SibucaoNavigationBuilder.BakeMap(folder));
        File.WriteAllLines("Temp/"+profile.areaName+"-placement-review.txt",reports);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);return string.Join("\n",reports);
    }
    [Serializable]class ConnectionArchive{public int schema;public Connection[] connections;}
    [Serializable]class Connection{public int home,difficulty,after;public Vector3[] points;}
    static readonly Dictionary<Vector2Int,Vector3> walkable=new Dictionary<Vector2Int,Vector3>();
    static readonly HashSet<Vector2Int> blocked=new HashSet<Vector2Int>();
    static bool Walkable(Vector2Int key,out Vector3 p)
    {
        if(walkable.TryGetValue(key,out p))return true;if(blocked.Contains(key))return false;
        try{p=OnGround(V(key.x,key.y));}catch{blocked.Add(key);return false;}
        foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.45f,p+Vector3.up*1.5f,.3f,~0,QueryTriggerInteraction.Ignore))
            if(!FloorName(c.name)){blocked.Add(key);return false;}
        walkable[key]=p;return true;
    }
    static Vector2Int Nearest(Vector3 p)
    {
        var k=new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z));
        for(int r=0;r<18;r++)for(int x=-r;x<=r;x++)for(int z=-r;z<=r;z++)if(Mathf.Abs(x)==r||Mathf.Abs(z)==r){var q=k+new Vector2Int(x,z);if(Walkable(q,out _))return q;}
        throw new Exception("No walkable ground near "+p);
    }
    static List<Vector3> WalkPath(Vector3 from,Vector3 to)
    {
        var a=Nearest(from);var b=Nearest(to);var open=new SortedSet<(float,int,int)>();var costs=new Dictionary<Vector2Int,float>{{a,0}};var parent=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();open.Add((Vector2Int.Distance(a,b),a.x,a.y));
        int minX=Mathf.Min(a.x,b.x)-85,maxX=Mathf.Max(a.x,b.x)+85,minZ=Mathf.Min(a.y,b.y)-85,maxZ=Mathf.Max(a.y,b.y)+85;
        while(open.Count>0&&closed.Count<45000)
        {
            var entry=open.Min;open.Remove(entry);var k=new Vector2Int(entry.Item2,entry.Item3);if(!closed.Add(k))continue;
            if(k==b){var result=new List<Vector3>{walkable[k]};while(parent.TryGetValue(k,out var prev)){k=prev;result.Add(walkable[k]);}result.Reverse();return result;}
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if(x==0&&z==0)continue;var q=k+new Vector2Int(x,z);if(q.x<minX||q.x>maxX||q.y<minZ||q.y>maxZ||closed.Contains(q)||!Walkable(q,out var p))continue;
                if(x!=0&&z!=0&&(!Walkable(k+new Vector2Int(x,0),out _)||!Walkable(k+new Vector2Int(0,z),out _)))continue;
                if(Mathf.Abs(p.y-walkable[k].y)>.45f)continue;float cost=costs[k]+Vector3.Distance(p,walkable[k]);if(costs.TryGetValue(q,out float old)&&old<=cost)continue;costs[q]=cost;parent[q]=k;open.Add((cost+Vector2Int.Distance(q,b),q.x,q.y));
            }
        }
        throw new Exception("No walkable connection from "+from+" to "+to);
    }
    static void BakeFlood(FloodSimulationManager flood)
    {
        var data=ScriptableObject.CreateInstance<SibucaoFloodTerrain>();data.cellSize=3;data.origin=new Vector2(route.points.Min(p=>p.x)-75,route.points.Min(p=>p.z)-75);data.width=Mathf.CeilToInt((route.points.Max(p=>p.x)+75-data.origin.x)/3)+1;data.height=Mathf.CeilToInt((route.points.Max(p=>p.z)+75-data.origin.y)/3)+1;int n=data.width*data.height;data.ground=new float[n];data.crestX=new float[n];data.crestZ=new float[n];data.kind=new byte[n];data.covered=new bool[n];
        for(int i=0;i<n;i++){var p=V(data.origin.x+i%data.width*3,data.origin.y+i/data.width*3);try{data.ground[i]=Ground(p);data.kind[i]=1;}catch{data.ground[i]=-8;data.kind[i]=2;}}
        for(int i=0;i<n;i++){data.crestX[i]=i%data.width<data.width-1?Mathf.Max(data.ground[i],data.ground[i+1]):50;data.crestZ[i]=i/data.width<data.height-1?Mathf.Max(data.ground[i],data.ground[i+data.width]):50;}
        flood.terrain=Asset(data,"FloodTerrain.asset");
    }
}
