using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoBranchInstaller
    {
        const string Folder="Assets/StormSim/Sibucao/Data/PhysicalBranches";
        static readonly int[,] Points={{65,25,-60},{60,25,-50},{70,25,60},{100,20,-120},{55,30,-40},{50,20,-40},{65,50,35},{50,20,-40},{65,25,-60}};
        static readonly float[,] Chances={{.9f,.7f,.5f},{.85f,.65f,.62f},{.9f,.65f,.8f},{.95f,.6f,.25f},{.85f,.6f,1},{.85f,.75f,1},{.9f,.9f,.65f},{.85f,.8f,1},{.9f,.7f,.45f}};
        static readonly float[,] Seconds={{0,12,12},{40,15,0},{40,8,30},{45,15,5},{50,0,10},{45,10,6},{30,8,60},{60,15,0},{30,8,6}};
        static readonly string[][] Labels={new[]{"Probe beside the boards","Cross the road","Cross by the fence"},new[]{"Backtrack through the alley","Squeeze between the cars","Climb across the hood"},new[]{"Speak to the resident and take the lane","Wade along the main road","Follow the side alley","Use the covered passage"},new[]{"Follow the outside lane","Pass beside the cable","Step across the wire"},new[]{"Thread, tighten and fasten the strap","Gather the scattered contents","Leave the bag and continue"},new[]{"Lift the chair over the curb","Guide the chair along the clear route","Continue past the resident"},new[]{"Speak to the responder and follow the landing","Board the boat","Stay on the original road"},new[]{"Carry the evacuee to the landing","Fetch and lead a neighbour","Continue along the street","Apply a kit and carry the evacuee"},new[]{"Turn inland","Follow the creek edge","Cross the short bank"}};
        static readonly PhysicalMechanism[][] Mechanisms={new[]{PhysicalMechanism.ProbeThenWalk,PhysicalMechanism.Walk,PhysicalMechanism.Walk},new[]{PhysicalMechanism.Walk,PhysicalMechanism.Squeeze,PhysicalMechanism.Mantle},new[]{PhysicalMechanism.TalkThenWalk,PhysicalMechanism.Walk,PhysicalMechanism.Walk,PhysicalMechanism.FlashlightPassage},new[]{PhysicalMechanism.Walk,PhysicalMechanism.Squeeze,PhysicalMechanism.Walk},new[]{PhysicalMechanism.RepairStrap,PhysicalMechanism.GatherItems,PhysicalMechanism.Walk},new[]{PhysicalMechanism.CarryResident,PhysicalMechanism.GuideResident,PhysicalMechanism.Walk},new[]{PhysicalMechanism.TalkThenWalk,PhysicalMechanism.BoardBoat,PhysicalMechanism.Walk},new[]{PhysicalMechanism.CarryResident,PhysicalMechanism.SignalHelp,PhysicalMechanism.Walk,PhysicalMechanism.TreatThenCarry},new[]{PhysicalMechanism.Walk,PhysicalMechanism.Walk,PhysicalMechanism.Walk}};
        static T Asset<T>(string name) where T:ScriptableObject {string p=Folder+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(p);if(!a){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,p);}return a;}
        static Transform Point(Transform parent,string name,Vector3 local){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
        static Material Material(string name,Color color){string p=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.24f);AssetDatabase.CreateAsset(m,p);}return m;}
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool collider=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        public static string InstallData()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before installation.");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var s=Object.FindFirstObjectByType<SibucaoSession>();var m=s.physicalDecisions;
            if(!s.player.GetComponent<SibucaoPhysicalFeedback>())s.player.gameObject.AddComponent<SibucaoPhysicalFeedback>();
            foreach(var item in Object.FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {if(item.kind==PhysicalItemKind.FirstAid)item.inventoryTag="first_aid";else if(item.kind==PhysicalItemKind.Supply)item.inventoryTag=item.supplyBit==1?"flashlight":item.supplyBit==0?"water":"documents";EditorUtility.SetDirty(item);}
            for(int n=0;n<9;n++)
            {
                var station=m.stations[n];var node=Asset<DecisionNode>("B"+(n+1)+"_"+DecisionTreeManager.Titles[n].Replace(" ","_"));station.node=node;node.id="sibucao_b"+(n+1);node.title=station.title;node.prompt=station.title;node.approachDistance=24;
                node.choices=new DecisionChoice[Labels[n].Length];
                foreach(var sensor in station.GetComponentsInChildren<PhysicalDecisionSensor>(true))if(sensor.kind==PhysicalSensorKind.Path||sensor.kind==PhysicalSensorKind.Boundary)sensor.gameObject.SetActive(false);else if(sensor.kind==PhysicalSensorKind.Hazard)sensor.enabled=false;
                if(station.routeMark)station.routeMark.SetActive(false);
                var old=station.transform.Find("Physical branching");if(old)Object.DestroyImmediate(old.gameObject);
                var root=Point(station.transform,"Physical branching",Vector3.zero);station.branches=new SibucaoPhysicalOption[Labels[n].Length];
                for(int c=0;c<Labels[n].Length;c++)
                {
                    var choice=Asset<DecisionChoice>("B"+(n+1)+"_C"+c);node.choices[c]=choice;choice.id=node.id+"_c"+c;choice.label=Labels[n][c];choice.basePoints=c<3?Points[n,c]:n==2?35:45;choice.baseSuccessChance=c<3?Chances[n,c]:.88f;choice.timeCost=c<3?Seconds[n,c]:n==2?20:65;
                    choice.maximumTier=WaterTier.Tier5;choice.minimumTier=WaterTier.Tier1;choice.scoreCategory=n==4?ScoreCategory.ResourceManagement:n==5||n==7?ScoreCategory.Teamwork:c==2?ScoreCategory.ResourceManagement:ScoreCategory.Evacuation;
                    if(c==2&&n==0)choice.label="Cross through the damaged railing gap";
                    if(c==2&&n==8)choice.label="Cross the storm-damaged bank gap";
                    choice.category=n==4?DecisionCategory.ResourceEquipment:n==5||n==7?DecisionCategory.PeopleSocial:n==3?DecisionCategory.ElectricalFire:n==6?DecisionCategory.RescueAuthority:DecisionCategory.RouteNavigation;
                    choice.roleModifiers=new[]{new RoleModifier{role="Student",points=n==2&&c==0?-5:0},new RoleModifier{role="Parent",successChance=n==5||n==7?.04f:0},new RoleModifier{role="Volunteer",points=n==6&&c==0?5:0,successChance=n==7?.04f:0}};
                    choice.requiredItemTag=c==3?(n==2?"flashlight":"first_aid"):null;choice.onSuccess=PhysicalConsequence.None;choice.onFailure=PhysicalConsequence.Stumble;choice.healthCost=0;choice.staminaCost=c==0?3:c==1?2:6;choice.failureHealthCost=8;choice.failureStaminaCost=12;choice.lossItemTag="flashlight";choice.rainfallRisk=.08f;choice.tierRisk=.025f;choice.seededChanceVariance=.05f;choice.failurePenalty=8;choice.deterministicOutcome=false;choice.delayedConsequenceFlag=false;choice.successFlag=null;
                    if(c==1){choice.onFailure|=PhysicalConsequence.LoseItem;choice.scoreCategory=ScoreCategory.ResourceManagement;}
                    if(c==2){choice.onFailure|=PhysicalConsequence.Injury|PhysicalConsequence.DelayedInjury;choice.rainfallRisk=.2f;choice.tierRisk=.045f;}
                    if((n==5||n==7)&&c==2){choice.deterministicOutcome=true;choice.onSuccess=choice.onFailure=PhysicalConsequence.LeaveResident;choice.delayedConsequenceFlag=true;choice.delayedFlagId="resident_left_"+n;choice.delayedAtDecision=8;}
                    if(n==4&&c==2){choice.deterministicOutcome=true;choice.onSuccess=choice.onFailure=PhysicalConsequence.LoseItem;}
                    if(n==6&&c==1){choice.onSuccess=PhysicalConsequence.Diversion;choice.delayedConsequenceFlag=true;choice.delayedFlagId="boat_manifest";choice.delayedAtDecision=9;}
                    if(n==3&&c==2){choice.onSuccess=choice.onFailure=PhysicalConsequence.Stumble|PhysicalConsequence.Injury|PhysicalConsequence.DelayedInjury;choice.failureHealthCost=18;}
                    if((n==1&&c>0)||(n==3&&c==1))choice.maximumTier=WaterTier.Tier4;
                    var branch=Point(root,"C"+c+" - "+choice.label,Vector3.zero).gameObject.AddComponent<SibucaoPhysicalOption>();branch.station=station;branch.choiceIndex=c;branch.mechanism=Mechanisms[n][c];branch.verb=n==5?"Take chair handles":n==7?"Call neighbour":"Speak";branch.subject=station.assist;
                    branch.actionPoint=Point(branch.transform,"Interaction point",new Vector3(c==0?-2:c==1?0:2,1,-3));
                    branch.tool=station.GetComponentsInChildren<PhysicalGrabbable>(true).FirstOrDefault(x=>x.kind==(n==4?PhysicalItemKind.Strap:PhysicalItemKind.Stick));
                    if(station.conversationActor&&branch.mechanism==PhysicalMechanism.TalkThenWalk)branch.actionPoint.position=station.conversationActor.position+Vector3.up*1.3f;
                    var goal=Point(branch.transform,"Delivery volume",new Vector3(c==0?-2:2,1.5f,7)).gameObject.AddComponent<BoxCollider>();goal.isTrigger=true;goal.size=new Vector3(4,4,4);branch.destination=goal;station.branches[c]=branch;EditorUtility.SetDirty(choice);
                }
                var source=Point(root,"Ambient approach - independent of selected outcome",new Vector3(0,1,0)).gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.playOnAwake=false;source.minDistance=3;source.maxDistance=30;source.volume=.28f;station.approachSource=source;node.approachAudio=n==8&&station.ambient?station.ambient.clip:s.weather.windAudio.clip;
                var cloth=Material("Weathered canvas",new Color(.31f,.32f,.29f));var pole=Box(root,"Wind-torn awning",new Vector3(-4,2.8f,-4),new Vector3(1.7f,.025f,.8f),cloth,false);station.preCueProp=pole.transform;
                EditorUtility.SetDirty(node);EditorUtility.SetDirty(station);
            }
            ConfigureObjects(s);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);return "Bound 9 nodes and 29 distinct physical choice bindings. Paths still require BakeStation for all nine.";
        }
        static void ConfigureObjects(SibucaoSession s)
        {
            var m=s.physicalDecisions;var metal=Material("Wet galvanized metal",new Color(.32f,.34f,.33f));var timber=Material("Wet timber",new Color(.24f,.19f,.14f));
            var cars=m.stations[1];
            for(int k=0;k<2;k++)
            {
                var car=cars.transform.Find("Stalled car "+(k+1));car.localRotation=Quaternion.identity;car.localScale=Vector3.one*.92f;car.localPosition=new Vector3(k==0?-1.19f:1.19f,.04f,1.4f);
                foreach(var col in car.GetComponentsInChildren<Collider>())Object.DestroyImmediate(col);
                var bounds=new Bounds();bool first=true;foreach(var r in car.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                // Cars are longitudinal. Preserve their actual mesh; collision separates hood from cabin.
                var visual=car.GetComponentInChildren<MeshFilter>();
                var hull=Box(cars.transform,"Car "+k+" chassis collision",new Vector3(k==0?-1.19f:1.19f,.43f,1.4f),new Vector3(1.94f,.78f,3.9f),metal);hull.GetComponent<Renderer>().enabled=false;
                var cabin=Box(cars.transform,"Car "+k+" cabin collision",new Vector3(k==0?-1.19f:1.19f,1.0f,1.95f),new Vector3(1.8f,.5f,1.5f),metal);cabin.GetComponent<Renderer>().enabled=false;
            }
            cars.branches[1].actionPoint.localPosition=new Vector3(0,1,-1.3f);cars.branches[2].actionPoint.localPosition=new Vector3(1.19f,.8f,-.8f);
            var strap=m.stations[4];var repair=strap.branches[0];repair.workSockets=new Transform[3];
            for(int i=0;i<3;i++){repair.workSockets[i]=Point(repair.transform,new[]{"Thread loop","Tension keeper","Fasten buckle"}[i],new Vector3(-1.6f+i*.34f,.75f,-.2f));Box(repair.workSockets[i],"Buckle",Vector3.zero,new Vector3(.13f,.06f,.12f),metal,false);}
            if(!strap.fallbackBag)
            {
                var template=s.homes[0].GetComponentsInChildren<PhysicalGrabbable>(true).First(x=>x.kind==PhysicalItemKind.Bag);var bag=Object.Instantiate(template,strap.spill.transform);bag.name="Abandoned damaged school bag";bag.persistentId="branch_fallback_bag";bag.station=strap;bag.transform.localPosition=new Vector3(-1,.35f,0);strap.fallbackBag=bag;
            }
            var b=m.stations[6];b.branches[1].boat=b.transform.Find("PLACEHOLDER_RescueBoat - boarding collision prototype");
            foreach(int n in new[]{5,7})
            {
                var st=m.stations[n];st.assist.Body.constraints=RigidbodyConstraints.FreezeRotation;st.branches[0].actionPoint.position=st.assist.transform.position+Vector3.up;
                st.branches[1].actionPoint.position=st.assist.transform.position+Vector3.up;
            }
            var neighbour=Object.Instantiate(m.stations[2].conversationActor,m.stations[7].branches[1].transform);neighbour.name="Neighbour - walk and idle only";neighbour.localPosition=new Vector3(4,0,-3);m.stations[7].branches[1].helper=neighbour;m.stations[7].branches[1].actionPoint.position=neighbour.position+Vector3.up*1.3f;
            var passage=m.stations[2].branches[3];var lamp=Point(s.player.view.transform,"Inventory flashlight beam",Vector3.zero).gameObject.AddComponent<Light>();lamp.type=LightType.Spot;lamp.range=12;lamp.spotAngle=48;lamp.intensity=1.8f;lamp.enabled=false;passage.flashlight=lamp;
            for(int z=-2;z<=4;z+=3){Box(passage.transform,"Passage post",new Vector3(5,1,z),new Vector3(.12f,2,.12f),timber);Box(passage.transform,"Passage post",new Vector3(7,1,z),new Vector3(.12f,2,.12f),timber);}Box(passage.transform,"Covered passage roof",new Vector3(6,2.1f,1),new Vector3(2.4f,.1f,8),metal);
        }
        public static Vector3[] Anchors(int n,int c)
        {
            if(n==3&&c==1)return new[]{new Vector3(2.1f,0,-3),new Vector3(2.1f,0,0),new Vector3(2.1f,0,3),new Vector3(2.1f,0,7)};
            if(n==3&&c==2)return new[]{new Vector3(0,0,-3),new Vector3(0,0,0),new Vector3(0,0,3),new Vector3(0,0,7)};
            if(n==1&&c==1)return new[]{new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(0,0,3.8f),new Vector3(0,0,5)};
            if(n==1&&c==2)return new[]{new Vector3(1.19f,0,-1.4f),new Vector3(1.19f,1.02f,-1),new Vector3(1.19f,1.02f,.45f),new Vector3(1.19f,1.58f,.85f),new Vector3(1.19f,1.58f,2.9f),new Vector3(1.19f,1.58f,3.9f),new Vector3(1.19f,.05f,4.2f),new Vector3(0,0,5.5f)};
            if(n==4&&c==0)return new[]{new Vector3(-2,0,-2),new Vector3(-1,0,0)};
            if(n==4&&c==1)return new[]{new Vector3(0,0,-2),new Vector3(-1,0,1),new Vector3(1,0,2),new Vector3(0,0,7)};
            if(n==5||n==7)
            {
                if(c==0||c==3)return new[]{new Vector3(-1,0,-1),new Vector3(-2,0,2),new Vector3(-4,0,4),new Vector3(-3,0,8)};
                if(n==7&&c==1)return new[]{new Vector3(4,0,-3),new Vector3(0,0,0),new Vector3(3,0,1),new Vector3(3,0,5),new Vector3(2,0,8)};
                if(c==1)return new[]{new Vector3(3,0,-3),new Vector3(3,0,1),new Vector3(3,0,5),new Vector3(2,0,8)};
            }
            if(n==6&&c==1)return new[]{new Vector3(2.5f,0,-3),new Vector3(2.5f,.22f,-1),new Vector3(2.5f,.4f,1),new Vector3(2.5f,.4f,2.1f)};
            if(c==3)return new[]{new Vector3(6,0,-4),new Vector3(6,0,0),new Vector3(6,0,5),new Vector3(2,0,9)};
            bool longRoute=n==1&&c==0||n==2&&(c==0||c==2)||n==3&&c==0||n==6&&(c==0||c==2)||n==8&&c==0;
            if(longRoute)
            {
                float side=c==2?1:-1,back=n==1?26:16;
                return new[]{new Vector3(side*3,0,-5),new Vector3(side*5,0,-back),new Vector3(side*16,0,-back-3),new Vector3(side*18,0,12),new Vector3(side*6,0,15),new Vector3(0,0,11)};
            }
            float x=c==0?-2.5f:c==1?0:2.8f;
            return new[]{new Vector3(x,0,-3),new Vector3(x,0,0),new Vector3(x+(c==2?1:0),0,3),new Vector3(x,0,7)};
        }
        public static string BakeStation(int n)
        {
            var s=Object.FindFirstObjectByType<SibucaoSession>();var station=s.physicalDecisions.stations[n];var originalPosition=station.transform.position;var originalRotation=station.transform.rotation;
            var mat=Material("Unmarked damp concrete",new Color(.3f,.3f,.28f));var wood=Material("Wet timber",new Color(.24f,.19f,.14f));var lines=new List<string>();
            foreach(var b in station.branches){b.layouts=new PhysicalPathVariant[s.homes.Length];b.layoutPresentation=new GameObject[s.homes.Length];foreach(Transform t in b.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Layout ")).ToArray())Object.DestroyImmediate(t.gameObject);}
            for(int h=0;h<s.homes.Length;h++)
            {
                float start=s.route.Progress(s.homes[h].standingPoint.position),d=start+(s.route.length-start-12)*(n+1)/10;
                var forward=s.route.Sample(d+2)-s.route.Sample(d-2);forward.y=0;station.transform.SetPositionAndRotation(s.route.Sample(d),Quaternion.LookRotation(forward));Physics.SyncTransforms();
                var grid=new SibucaoBranchPathBake(station,s.GetComponent<FloodSimulationManager>().terrain);
                foreach(var b in station.branches)
                {
                    var anchors=Anchors(n,b.choiceIndex);bool exact=b.mechanism==PhysicalMechanism.Mantle||b.mechanism==PhysicalMechanism.Squeeze||b.mechanism==PhysicalMechanism.BoardBoat||b.mechanism==PhysicalMechanism.RepairStrap;
                    Vector3[] points;
                    if(exact)points=anchors;
                    else {try{points=grid.Route(anchors);}catch(Exception){points=grid.Route(anchors.Select(p=>new Vector3(-p.x,p.y,p.z)).ToArray());}}
                    b.layouts[h]=new PhysicalPathVariant{home=h,points=points};
                    var visual=Point(b.transform,"Layout "+h+" - physical footing",Vector3.zero);b.layoutPresentation[h]=visual.gameObject;
                    for(int j=1;j<points.Length;j++)
                    {
                        var delta=points[j]-points[j-1];if(delta.magnitude<.2f)continue;
                        if(exact&&b.mechanism!=PhysicalMechanism.BoardBoat)continue;
                        var strip=Box(visual,"Worn footing",(points[j]+points[j-1])*.5f-Vector3.up*.06f,new Vector3(n==8&&b.choiceIndex==1?.78f:1.35f,.09f,delta.magnitude+.04f),n==8||b.mechanism==PhysicalMechanism.BoardBoat?wood:mat);
                        strip.transform.localRotation=Quaternion.LookRotation(delta,Vector3.up);
                    }
                    visual.gameObject.SetActive(h==0);
                    if(h==0)
                    {
                        if(b.path!=null)foreach(var p in b.path)if(p)Object.DestroyImmediate(p.gameObject);
                        b.path=points.Select((p,i)=>Point(b.transform,"Physical step "+i,p)).ToArray();
                        if(b.destination)b.destination.transform.localPosition=points.Last()+Vector3.up*1.5f;
                        if(b.mechanism==PhysicalMechanism.FlashlightPassage)b.actionPoint.localPosition=points[0]+Vector3.up;
                    }
                    float length=0;for(int j=1;j<points.Length;j++)length+=Vector3.Distance(points[j-1],points[j]);lines.Add("H"+h+" B"+(n+1)+" C"+b.choiceIndex+" "+points.Length+" steps "+length.ToString("F1")+"m");
                }
            }
            float resetStart=s.route.Progress(s.homes[0].standingPoint.position),resetDistance=resetStart+(s.route.length-resetStart-12)*(n+1)/10;
            var resetForward=s.route.Sample(resetDistance+2)-s.route.Sample(resetDistance-2);resetForward.y=0;station.transform.SetPositionAndRotation(s.route.Sample(resetDistance),Quaternion.LookRotation(resetForward));Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(station.gameObject.scene);AssetDatabase.SaveAssets();return string.Join("\n",lines);
        }
        public static string AddDeepWaterBranch()
        {
            var s=Object.FindFirstObjectByType<SibucaoSession>();var st=s.physicalDecisions.stations[1];if(st.branches.Length==4)return "Deep-water branch already bound.";
            var choice=Asset<DecisionChoice>("B2_C3");choice.id="sibucao_b2_c3";choice.label="Swim beside the submerged cars";choice.basePoints=5;choice.baseSuccessChance=.55f;choice.timeCost=15;choice.minimumTier=WaterTier.Tier5;choice.maximumTier=WaterTier.Tier5;choice.scoreCategory=ScoreCategory.ResourceManagement;choice.onFailure=PhysicalConsequence.Stumble|PhysicalConsequence.LoseItem|PhysicalConsequence.Exhaustion;choice.staminaCost=14;choice.requiredItemTag=null;
            Array.Resize(ref st.node.choices,4);st.node.choices[3]=choice;Array.Resize(ref st.branches,4);
            var b=Point(st.branches[0].transform.parent,"C3 - submerged car passage",Vector3.zero).gameObject.AddComponent<SibucaoPhysicalOption>();b.station=st;b.choiceIndex=3;b.mechanism=PhysicalMechanism.SwimPassage;b.actionPoint=Point(b.transform,"Submerged approach",new Vector3(-2.5f,1,-3));st.branches[3]=b;EditorUtility.SetDirty(choice);EditorUtility.SetDirty(st.node);EditorUtility.SetDirty(st);AssetDatabase.SaveAssets();return "Added swimming alternative for Tier 5; no low-water swim prompt.";
        }
        public static string RepairSceneBindings()
        {
            var s=Object.FindFirstObjectByType<SibucaoSession>();var stations=s.physicalDecisions.stations;
            foreach(var st in stations){st.node.prompt=SibucaoMissionGuide.Objectives[st.index];EditorUtility.SetDirty(st.node);}
            var wire=stations[3];foreach(var sensor in wire.GetComponentsInChildren<PhysicalDecisionSensor>(true))if(sensor.kind==PhysicalSensorKind.Hazard){sensor.gameObject.SetActive(true);sensor.enabled=false;}
            wire.branches[1].actionPoint.localPosition=new Vector3(2.1f,1,-3);
            wire.branches[2].requireContact=true;wire.node.choices[2].onSuccess=PhysicalConsequence.Stumble|PhysicalConsequence.Exhaustion;EditorUtility.SetDirty(wire.node.choices[2]);
            if(!wire.transform.Find("Cable physical contact")){var contact=Point(wire.transform,"Cable physical contact",new Vector3(0,.75f,0));var box=contact.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(3.7f,1.6f,.85f);contact.gameObject.AddComponent<SibucaoBranchContact>().option=wire.branches[2];}
            foreach(int n in new[]{0,8})stations[n].branches[2].mechanism=PhysicalMechanism.SprintPassage;
            var wood=Material("Wet timber",new Color(.24f,.19f,.14f));var metal=Material("Wet galvanized metal",new Color(.32f,.34f,.33f));
            var passage=stations[2].branches[3];foreach(Transform t in passage.transform.Cast<Transform>().Where(x=>x.name=="Passage post"||x.name=="Covered passage roof").ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(var tuple in new[]{(stations[0].branches[2],false),(stations[8].branches[2],false),(passage,true)})
            {
                var branch=tuple.Item1;for(int h=0;h<branch.layouts.Length;h++)
                {
                    var parent=branch.layoutPresentation[h].transform;var old=parent.Find("Passage structure");if(old)Object.DestroyImmediate(old.gameObject);
                    var frame=Point(parent,"Passage structure",Vector3.zero);var points=branch.layouts[h].points;
                    for(int i=1;i<Mathf.Min(points.Length,tuple.Item2?4:3);i++)
                    {
                        var delta=points[i]-points[i-1];delta.y=0;if(delta.magnitude<.3f)continue;
                        // A rail offset from a short side-step cuts across the incoming lane.
                        // Leave these B1 corners open so the visible fence follows the passage.
                        if(!tuple.Item2&&branch.station.index==0&&delta.magnitude<1.5f)continue;
                        var center=(points[i]+points[i-1])*.5f;var right=Vector3.Cross(Vector3.up,delta.normalized);
                        if(tuple.Item2)
                        {
                            var roof=Box(frame,"Corrugated passage roof",center+Vector3.up*2.3f,new Vector3(1.8f,.08f,delta.magnitude+.1f),metal);roof.transform.localRotation=Quaternion.LookRotation(delta);
                            for(int side=-1;side<=1;side+=2)Box(frame,"Timber roof post",points[i]+right*(side*.87f)+Vector3.up*1.1f,new Vector3(.09f,2.2f,.09f),wood);
                        }
                        else for(int side=-1;side<=1;side+=2)
                        {
                            var rail=Box(frame,"Weathered fence rail",center+right*(side*1.05f)+Vector3.up*.75f,new Vector3(.08f,.09f,delta.magnitude),wood);rail.transform.localRotation=Quaternion.LookRotation(delta);
                            Box(frame,"Fence post",points[i]+right*(side*1.05f)+Vector3.up*.48f,new Vector3(.09f,.96f,.09f),wood);
                        }
                    }
                }
            }
            foreach(var st in stations)EditorUtility.SetDirty(st);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();return "Restored visible cable, bound actual contact, and aligned fence/covered passages to each baked layout.";
        }
    }
}
