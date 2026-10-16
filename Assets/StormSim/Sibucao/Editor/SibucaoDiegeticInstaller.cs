using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

[Serializable] public sealed class SibucaoHazardSurvey
{
    public List<SibucaoHazardRecord> sites=new List<SibucaoHazardRecord>();
    public List<string> exclusions=new List<string>();
}
[Serializable] public sealed class SibucaoHazardRecord
{
    public string id,location,reason;
    public Vector3 position,forward;
    public SibucaoHazardKind kind;
    public bool junction;
    public int homeMask=15;
}
public static partial class SibucaoDiegeticInstaller
{
    const string Folder="Assets/StormSim/Sibucao/DiegeticHazards";
    static SibucaoSession s;
    static SibucaoFloodTerrain terrain;
    static SibucaoHazardSurvey survey;
    static readonly List<string> report=new List<string>();
    static void Context(){s=Object.FindFirstObjectByType<SibucaoSession>();terrain=s.GetComponent<FloodSimulationManager>().terrain;Directory.CreateDirectory("tmp/diegetic");}
    static bool Ignore(Collider c)=>c.isTrigger||c.attachedRigidbody||c.GetComponentInParent<PhysicalDecisionStation>()||c.GetComponentInParent<SibucaoPlayer>()||c.GetComponentInParent<SibucaoResident>();
    static bool Ground(Vector3 proposed,out Vector3 p)
    {
        p=proposed;int x=Mathf.RoundToInt((p.x-terrain.origin.x)/terrain.cellSize),z=Mathf.RoundToInt((p.z-terrain.origin.y)/terrain.cellSize);
        if(x<1||z<1||x>=terrain.width-1||z>=terrain.height-1)return false;
        int index=z*terrain.width+x;if(terrain.kind[index]!=1||terrain.covered[index])return false;
        var refuge=s.GetComponent<FloodSimulationManager>().dryRefuge;if(refuge&&refuge.Contains(p,5))return false;
        foreach(var h in s.homes)if(Flat(h.transform.position,p)<8)return false;
        float expected=terrain.ground[index],height=float.NegativeInfinity;
        foreach(var h in Physics.RaycastAll(new Vector3(p.x,expected+1.4f,p.z),Vector3.down,3.2f,~0,QueryTriggerInteraction.Ignore))
            if(!Ignore(h.collider)&&h.normal.y>.72f&&h.point.y<expected+.8f)height=Mathf.Max(height,h.point.y);
        if(!float.IsFinite(height))return false;p.y=height+.04f;
        return !Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore).Any(c=>!Ignore(c));
    }
    static float Flat(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
    static float RouteLateral(Vector3 p)=>Flat(p,s.route.Sample(s.route.Progress(p)));
    static Vector3 Forward(float d){var f=s.route.Sample(d+2)-s.route.Sample(d-2);f.y=0;return f.normalized;}
    static Vector3 StationPoint(int home,int node){float start=s.route.Progress(s.homes[home].standingPoint.position);return s.route.Sample(start+(s.route.length-start-12)*(node+1)/10);}
    static void Add(Vector3 position,Vector3 forward,string label,bool junction,int homeMask=15)
    {
        var previous=survey.sites.FirstOrDefault(x=>Flat(x.position,position)<17);
        if(previous!=null){if(!previous.location.Contains(label))previous.location+="; "+label;previous.junction|=junction;previous.homeMask|=homeMask;return;}
        int n=survey.sites.Count;
        var kind=junction?(n%5==0?SibucaoHazardKind.Supplies:n%5==1?SibucaoHazardKind.Neighbour:SibucaoHazardKind.CollapsedRoof):n%10==3?SibucaoHazardKind.FallenTree:n%10==7?SibucaoHazardKind.LiveWire:SibucaoHazardKind.LowStreet;
        survey.sites.Add(new SibucaoHazardRecord{id="D"+(n+1).ToString("00"),location=label,position=position,forward=forward,kind=kind,junction=junction,homeMask=homeMask,
            reason=kind==SibucaoHazardKind.LowStreet?"Depressed side street receives rainfall and runoff before the raised route.":kind==SibucaoHazardKind.FallenTree?"Uprooted roadside tree leaves a trunk crossing and a narrow end passage.":kind==SibucaoHazardKind.LiveWire?"A storm-damaged service drop lies across the shortcut; contact uses the electrical consequence model.":kind==SibucaoHazardKind.Supplies?"Storm-scattered sealed supplies at an open junction.":kind==SibucaoHazardKind.Neighbour?"A resident at an exposed junction can be consulted before a physical route choice.":"A collapsed roadside lean-to spills roofing into an open passage."});
    }
    public static string Survey()
    {
        if(EditorApplication.isPlaying)throw new Exception("Survey requires Edit Mode");Context();survey=new SibucaoHazardSurvey();
        for(int home=0;home<4;home++)for(int node=0;node<9;node++)
        {
            var center=StationPoint(home,node);float d=s.route.Progress(center);var f=Forward(d);var right=Vector3.Cross(Vector3.up,f);
            for(int side=-1;side<=1;side+=2)
            {
                bool found=false;Vector3 best=default;float cost=float.MaxValue;
                for(float lateral=10;lateral<=26;lateral+=2)for(float along=-10;along<=10;along+=2)
                {
                    var candidate=center+right*lateral*side+f*along;if(!Ground(candidate,out var point)||RouteLateral(point)<7)continue;
                    // An open approach must connect toward the road, not sit inside a roofed building.
                    var delta=center-point;delta.y=0;
                    if(Physics.SphereCastAll(point+Vector3.up*.85f,.29f,delta.normalized,Mathf.Min(5,delta.magnitude),~0,QueryTriggerInteraction.Ignore).Any(h=>!Ignore(h.collider)))continue;
                    float score=Mathf.Abs(lateral-15)+Mathf.Abs(along)*.65f;
                    if(score<cost){cost=score;best=point;found=true;}
                }
                string label="H"+home+" B"+(node+1)+(side<0?" left":" right");
                if(found)Add(best,f,label,false);else survey.exclusions.Add(label+": no connected uncovered side path in the 10–26 m survey; buildings, coast and dry refuge retained.");
            }
        }
        // Sample all walkable open junctions on the route, not just authored nodes.
        float first=s.homes.Min(h=>s.route.Progress(h.standingPoint.position));
        for(float d=first+20;d<s.route.length-24;d+=6)
        {
            if(!Ground(s.route.Sample(d),out var p))continue;
            int mask=0;for(int h=0;h<4;h++)if(d>s.route.Progress(s.homes[h].standingPoint.position)+12&&Enumerable.Range(0,9).Min(n=>Flat(p,StationPoint(h,n)))>19)mask|=1<<h;
            if(mask==0||survey.sites.Any(x=>x.junction&&Flat(x.position,p)<28))continue;
            int open=0;for(int k=0;k<8;k++)
            {var dir=Quaternion.Euler(0,k*45,0)*Vector3.forward;if(Ground(p+dir*6,out _)&&!Physics.SphereCastAll(p+Vector3.up*.85f,.3f,dir,6,~0,QueryTriggerInteraction.Ignore).Any(x=>!Ignore(x.collider)))open++;}
            if(open>=5)Add(p,Forward(d),"Open junction at route "+d.ToString("F0")+"m",true,mask);
        }
        File.WriteAllText("tmp/diegetic/survey.json",JsonUtility.ToJson(survey,true));
        File.WriteAllLines("tmp/diegetic/survey.txt",survey.sites.Select(x=>x.id+" | "+x.kind+" | "+x.position+" | "+x.location).Concat(survey.exclusions));
        return survey.sites.Count+" surveyed locations, "+survey.sites.Count(x=>x.junction)+" open-area situations; "+survey.exclusions.Count+" excluded sides. See tmp/diegetic/survey.txt.";
    }
    static Transform Node(Transform parent,string name,Vector3 local){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
    static T Asset<T>(string name) where T:ScriptableObject
    {string path=Folder+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(!a){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
    static Material Mat(string name,Color color)
    {string path=Folder+"/"+name+".mat";var a=AssetDatabase.LoadAssetAtPath<Material>(path);if(!a){a=new Material(Shader.Find("Universal Render Pipeline/Lit"));a.color=color;a.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(a,path);}return a;}
    static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 size,Material material,bool collision=true)
    {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static string[][] Reactions={
        new[]{"The stick found the bottom. I'm across.","I'm across the street. Water is coming off the roofs.","I'm past the edge. The wind is picking up."},
        new[]{"That alley brought me around the cars.","I'm through the gap between the cars.","My feet are back on the road.","I'm out of the water beside the cars."},
        new[]{"That was the resident's lane. I'm through.","I'm through the junction. My clothes are soaked.","That side alley took me around the corner.","I'm out of the covered passage."},
        new[]{"I've come around the outside of the cable.","I'm through the space beside the cable.","I've crossed the line. Keep moving."},
        new[]{"The buckle is fastened. I can carry the bag.","The loose supplies are in the bag.","I left the bag where it fell."},
        new[]{"The chair is on the landing now.","We've reached the clear transfer point.","I'm continuing past the curb."},
        new[]{"I've reached the landing the responder described.","The boat is moving toward the other landing.","I'm continuing along the original street."},
        new[]{"I've put the evacuee down at the landing.","We've brought the evacuee to the landing.","I'm continuing along the street.","The kit is used. The evacuee is at the landing."},
        new[]{"The inland lane has brought me past the creek.","I'm past the creek edge.","I'm across the short bank."}
    };
    static void Beat(PhysicalDecisionStation st,string[] lines)
    {
        var beat=st.GetComponent<SibucaoResolutionBeat>();if(!beat)beat=st.gameObject.AddComponent<SibucaoResolutionBeat>();
        beat.station=st;beat.lines=lines;beat.weatherCue=s.weather.windAudio.clip;
        if(!beat.source){beat.source=Node(st.transform,"Crossing reaction - neutral weather cue",Vector3.up).gameObject.AddComponent<AudioSource>();beat.source.playOnAwake=false;beat.source.spatialBlend=0;beat.source.volume=.16f;beat.source.loop=false;}
        st.resolutionBeat=beat;EditorUtility.SetDirty(st);EditorUtility.SetDirty(beat);
    }
    public static string InstallBeats()
    {Context();for(int i=0;i<9;i++)Beat(s.physicalDecisions.stations[i],Reactions[i]);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "All 9 authored stations have choice-specific neutral reaction beats after recorded resolution.";}
}
