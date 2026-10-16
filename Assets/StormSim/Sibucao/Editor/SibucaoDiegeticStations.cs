using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using StormSim.Sibucao.Editor;
using Object=UnityEngine.Object;

public static partial class SibucaoDiegeticInstaller
{
    static Transform hazardRoot;
    static int installIndex;
    static List<PhysicalDecisionStation> installed;
    static Material timber,metal,leaf,blue,tape;
    public static string InstallationStatus="Idle";
    static Vector3[] GroundPath(PhysicalDecisionStation st,Vector3[] anchors)
    {
        var result=new List<Vector3>();bool clear=true;
        foreach(var a in anchors){if(!Ground(st.transform.TransformPoint(a),out var p)){clear=false;break;}result.Add(st.transform.InverseTransformPoint(p));}
        if(clear)for(int i=1;i<result.Count;i++)
        {var a=st.transform.TransformPoint(result[i-1])+Vector3.up*.9f;var b=st.transform.TransformPoint(result[i])+Vector3.up*.9f;if(Physics.SphereCastAll(a,.28f,(b-a).normalized,Vector3.Distance(a,b),~0,QueryTriggerInteraction.Ignore).Any(h=>!Ignore(h.collider))){clear=false;break;}}
        if(clear)return result.ToArray();
        return new SibucaoBranchPathBake(st,terrain).Route(anchors);
    }
    static SibucaoPhysicalOption Branch(PhysicalDecisionStation st,int index,PhysicalMechanism mechanism,Vector3[] points,string label,DecisionCategory category,int basePoints=12)
    {
        var c=Asset<DecisionChoice>(st.node.id+"_choice_"+index);c.id=st.node.id+"_c"+index;c.label=label;c.category=category;
        c.basePoints=basePoints;c.baseSuccessChance=index==0?.78f:.87f;c.timeCost=0;c.maximumTier=WaterTier.Tier5;c.minimumTier=WaterTier.Tier1;
        c.scoreCategory=category==DecisionCategory.ResourceEquipment?ScoreCategory.ResourceManagement:category==DecisionCategory.PeopleSocial?ScoreCategory.Teamwork:ScoreCategory.Evacuation;
        c.failurePenalty=4;c.staminaCost=index==0?2:3;c.onFailure=PhysicalConsequence.Stumble;c.onSuccess=PhysicalConsequence.None;
        c.roleModifiers=new[]{new RoleModifier{role="Student",points=0},new RoleModifier{role="Parent",successChance=category==DecisionCategory.PeopleSocial?.03f:0},new RoleModifier{role="Volunteer",successChance=category==DecisionCategory.PeopleSocial?.04f:0}};
        var b=Node(st.transform,"C"+index+" - "+label,Vector3.zero).gameObject.AddComponent<SibucaoPhysicalOption>();b.station=st;b.choiceIndex=index;b.mechanism=mechanism;b.path=new Transform[points.Length];b.layouts=Array.Empty<PhysicalPathVariant>();
        for(int i=0;i<points.Length;i++)b.path[i]=Node(b.transform,"Physical step "+i,points[i]);
        b.actionPoint=Node(b.transform,"Action focus",points[0]+Vector3.up);b.verb="Speak to the resident";
        var volume=Node(b.transform,"Physical destination",points.Last()+Vector3.up*1.5f).gameObject.AddComponent<BoxCollider>();volume.isTrigger=true;volume.size=new Vector3(3.5f,4,3.5f);b.destination=volume;
        float length=0;for(int i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);c.timeCost=Mathf.Max(3,length/3);st.node.choices[index]=c;st.branches[index]=b;EditorUtility.SetDirty(c);return b;
    }
    static void WoodBeam(Transform parent,string name,Vector3 from,Vector3 to,float thickness)
    {var go=Shape(parent,name,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(thickness,Vector3.Distance(from,to)*.5f,thickness),timber);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);}
    static void Tree(Transform root)
    {
        WoodBeam(root,"Uprooted coconut trunk",new Vector3(-2,.55f,0),new Vector3(1.9f,.55f,.1f),.72f);
        Shape(root,"Exposed soil root ball",PrimitiveType.Sphere,new Vector3(-2,.4f,0),new Vector3(1.35f,.95f,1.25f),timber);
        for(int i=0;i<7;i++){float angle=i*Mathf.PI*2/7;WoodBeam(root,"Torn roots",new Vector3(-2,.5f,0),new Vector3(-2+Mathf.Cos(angle)*1.15f,.15f+Mathf.Abs(Mathf.Sin(angle))*.55f,Mathf.Sin(angle)*1.0f),.08f);}
        for(int i=0;i<4;i++){float z=(i-1.5f)*.65f;WoodBeam(root,"Broken crown branch",new Vector3(1.7f,.6f,0),new Vector3(2.8f,.18f,z),.12f);Shape(root,"Storm-thrown fronds",PrimitiveType.Sphere,new Vector3(2.65f,.2f,z),new Vector3(1.9f,.08f,.38f),leaf,false);}
    }
    static void Sheet(Transform parent,Vector3 position,Vector3 rotation,string id)
    {
        var node=Node(parent,"Bent corrugated GI sheet",position);node.localRotation=Quaternion.Euler(rotation);
        const int nx=32,nz=4;var v=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[v.Length];var indices=new List<int>();
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){int i=z*(nx+1)+x;float xx=(float)x/nx;v[i]=new Vector3((xx-.5f)*2.8f,Mathf.Sin(xx*Mathf.PI*24)*.028f+Mathf.Pow(Mathf.Abs(xx-.5f)*2,4)*.12f,((float)z/nz-.5f)*2.4f);uv[i]=new Vector2(xx,(float)z/nz);if(x<nx&&z<nz){int a=i,b=i+1,c=i+nx+1,d=c+1;indices.AddRange(new[]{a,c,b,b,c,d});}}
        var mesh=new Mesh{name=id+" storm-bent GI"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=indices.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/"+id+"_GI.asset");node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=metal;node.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static void Roof(Transform root,string id)
    {
        WoodBeam(root,"Snapped lean-to rafter",new Vector3(-1.4f,.15f,-1.1f),new Vector3(1.35f,.72f,1),.18f);
        WoodBeam(root,"Collapsed nipa post",new Vector3(-1.3f,.25f,1.2f),new Vector3(1.5f,.5f,-1.2f),.16f);
        Sheet(root,new Vector3(-.1f,.82f,0),new Vector3(9,3,8),id);
        var barrel=Shape(root,"Rain barrel knocked beside roofing",PrimitiveType.Cylinder,new Vector3(2.3f,.4f,0),new Vector3(.72f,.4f,.72f),blue);barrel.transform.localRotation=Quaternion.Euler(0,0,12);
    }
    static void Wire(PhysicalDecisionStation st,bool cordon)
    {
        var pole=Shape(st.transform,"Storm-damaged service pole",PrimitiveType.Cylinder,new Vector3(-2.6f,1.55f,1.8f),new Vector3(.16f,1.55f,.16f),timber);pole.transform.localRotation=Quaternion.Euler(12,0,-18);
        var line=Node(st.transform,"Fallen service cable",Vector3.zero).gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=20;line.widthMultiplier=.035f;line.sharedMaterial=metal;
        for(int i=0;i<20;i++){float t=i/19f;line.SetPosition(i,new Vector3(Mathf.Lerp(-2.1f,2,t),.07f+Mathf.Pow(t-.5f,2)*.4f,Mathf.Sin(t*Mathf.PI*2)*.4f));}
        var contact=Node(st.transform,"Electrical contact at visible cable",new Vector3(0,.18f,0)).gameObject.AddComponent<BoxCollider>();contact.isTrigger=true;contact.size=new Vector3(4.4f,.45f,1.0f);var hazard=contact.gameObject.AddComponent<SibucaoElectricalContact>();hazard.station=st;hazard.choice=0;
        var branchContact=contact.gameObject.AddComponent<SibucaoBranchContact>();branchContact.option=st.branches[0];st.branches[0].requireContact=true;
        var original=s.physicalDecisions.stations[3].node.choices[2];var choice=st.node.choices[0];choice.onSuccess=original.onSuccess;choice.onFailure=original.onFailure;choice.failureHealthCost=original.failureHealthCost;choice.baseSuccessChance=original.baseSuccessChance;choice.delayedConsequenceFlag=false;EditorUtility.SetDirty(choice);
        if(!cordon)return;
        for(int side=-1;side<=1;side+=2)
        {
            var p=new Vector3(side*2.3f,0,-1.5f);WoodBeam(st.transform,"Tanod sawhorse leg",p+new Vector3(-.3f,0,-.25f),p+Vector3.up*.85f,.065f);WoodBeam(st.transform,"Tanod sawhorse leg",p+new Vector3(.3f,0,.25f),p+Vector3.up*.85f,.065f);
            Shape(st.transform,"Sawhorse crossbar",PrimitiveType.Cube,p+Vector3.up*.78f,new Vector3(.8f,.09f,.1f),timber);
        }
        // Tape communicates the cordon. It has no invisible collision wall.
        for(int i=0;i<16;i++)Shape(st.transform,"Barangay caution tape",PrimitiveType.Cube,new Vector3(Mathf.Lerp(-2.3f,2.3f,(i+.5f)/16),.8f,-1.5f),new Vector3(.29f,.075f,.008f),i%2==0?tape:metal,false);
    }
    static void InstallOne(SibucaoHazardRecord site)
    {
        if(!Ground(site.position,out var floor))throw new Exception(site.id+" has no walkable floor after terrain tuning");
        var go=Node(hazardRoot,site.id+" - "+site.kind+" - "+site.location,floor).gameObject;go.transform.rotation=Quaternion.LookRotation(site.forward);
        var meta=go.AddComponent<SibucaoDiegeticHazard>();meta.siteId=site.id;meta.location=site.location;meta.reason=site.reason;meta.kind=site.kind;
        var st=go.AddComponent<PhysicalDecisionStation>();st.index=installed.Count;st.manager=s.physicalDecisions;st.supplementary=true;st.homeMask=site.homeMask;st.maximumTier=WaterTier.Tier5;st.title=site.kind==SibucaoHazardKind.LowStreet?"Low side street":site.kind==SibucaoHazardKind.FallenTree?"Uprooted roadside tree":site.kind==SibucaoHazardKind.CollapsedRoof?"Storm-damaged lean-to":site.kind==SibucaoHazardKind.Supplies?"Scattered supplies":site.kind==SibucaoHazardKind.Neighbour?"Resident at the junction":site.kind==SibucaoHazardKind.LGUCordon?"Barangay cordon":"Fallen service cable";
        var node=Asset<DecisionNode>(site.id);st.node=node;node.id="sibucao_"+site.id.ToLower();node.title=st.title;node.prompt=st.title+" ahead.";node.choices=new DecisionChoice[2];st.branches=new SibucaoPhysicalOption[2];node.approachDistance=14;
        var category=site.kind==SibucaoHazardKind.LiveWire||site.kind==SibucaoHazardKind.LGUCordon?DecisionCategory.ElectricalFire:site.kind==SibucaoHazardKind.Supplies?DecisionCategory.ResourceEquipment:site.kind==SibucaoHazardKind.Neighbour?DecisionCategory.PeopleSocial:site.kind==SibucaoHazardKind.LowStreet?DecisionCategory.Environmental:DecisionCategory.RouteNavigation;node.category=category;
        Vector3[] direct=null;var facing=st.transform.rotation;
        foreach(float angle in new[]{0,45f,-45f,90f,-90f,135f,-135f})
        {
            st.transform.rotation=facing*Quaternion.Euler(0,angle,0);
            try{direct=GroundPath(st,new[]{new Vector3(-.65f,0,-5),new Vector3(-.65f,0,-2),new Vector3(-.65f,0,2),new Vector3(-.65f,0,6)});break;}catch{}
        }
        if(direct==null)throw new Exception("Surveyed opening has no connected through-path in seven orientations");
        Vector3[] detour;
        try{detour=GroundPath(st,new[]{new Vector3(2,0,-5),new Vector3(9,0,-9),new Vector3(15,0,-6),new Vector3(15,0,12),new Vector3(2,0,9)});}
        catch
        {
            try{detour=GroundPath(st,new[]{new Vector3(2,0,-5),new Vector3(-9,0,-9),new Vector3(-15,0,-6),new Vector3(-15,0,12),new Vector3(2,0,9)});}
            catch
            {
                // Follow surveyed ground back to the main street when a backyard
                // is enclosed. Never carve a detour through somebody's house.
                float distance=s.route.Progress(st.transform.position);
                detour=GroundPath(st,new[]{direct[0],st.transform.InverseTransformPoint(s.route.Sample(distance-10)),st.transform.InverseTransformPoint(s.route.Sample(distance+12)),direct.Last()});
            }
        }
        string[] lines={"I've come through this section.","That lane brought me around the obstruction."};
        if(site.kind==SibucaoHazardKind.FallenTree||site.kind==SibucaoHazardKind.CollapsedRoof)
        {
            var climb=new[]{new Vector3(-.35f,0,-2),new Vector3(-.35f,1.22f,-1.25f),new Vector3(-.35f,1.22f,-.25f),new Vector3(-.35f,1.22f,.45f),new Vector3(-.35f,1.22f,1.4f),new Vector3(-.35f,0,2.2f),new Vector3(-.35f,0,4)};
            var b=Branch(st,0,PhysicalMechanism.Mantle,climb,"Climb over the storm debris",category);b.actionPoint.localPosition=new Vector3(-.35f,.8f,-2);
            Branch(st,1,PhysicalMechanism.Walk,detour,"Follow the surrounding lane",category);st.node.choices[0].maximumTier=WaterTier.Tier4;
            if(site.kind==SibucaoHazardKind.FallenTree)Tree(st.transform);else Roof(st.transform,site.id);lines[0]="My feet are back on the ground beyond the debris.";
        }
        else if(site.kind==SibucaoHazardKind.Supplies)
        {
            var gather=Branch(st,0,PhysicalMechanism.GatherItems,direct,"Collect loose supplies and continue",category);Branch(st,1,PhysicalMechanism.Walk,detour,"Continue through the side lane",category);
            var template=s.homes[0].GetComponentsInChildren<PhysicalGrabbable>(true).First(x=>x.kind==PhysicalItemKind.Supply);
            for(int i=0;i<2;i++){var item=Object.Instantiate(template,st.transform);item.name="Sealed supplies - E pickup "+(i+1);item.persistentId=site.id+"_supply_"+i;item.station=st;item.supplyBit=-1;item.inventoryTag=i==0?"water":"documents";item.transform.localPosition=new Vector3(-.65f+(i==0?-.65f:.65f),.45f,-1+i*2);item.secured=item.delivered=false;item.Body.isKinematic=false;item.gameObject.SetActive(true);}
            lines=new[]{"The loose supplies are in the bag. I'm moving on.","I'm continuing past the scattered supplies."};
        }
        else if(site.kind==SibucaoHazardKind.Neighbour)
        {
            var b=Branch(st,0,PhysicalMechanism.TalkThenWalk,direct,"Speak, then follow the lane",category);Branch(st,1,PhysicalMechanism.Walk,detour,"Continue around the block",category);
            var actor=Object.Instantiate(s.physicalDecisions.stations[2].conversationActor,st.transform);actor.name="Resident waiting by the junction";actor.localPosition=new Vector3(-2,0,-3);actor.localRotation=Quaternion.Euler(0,30,0);st.conversationActor=actor;b.actionPoint.position=actor.position+Vector3.up*1.3f;st.conversationLine="People passed this corner toward the school. The streets are changing quickly.";
            lines=new[]{"I've followed the lane from the junction.","I'm continuing around the block from the resident."};
        }
        else
        {
            Branch(st,0,PhysicalMechanism.Walk,direct,site.kind==SibucaoHazardKind.LowStreet?"Traverse the low street":"Cross the cable's line",category);Branch(st,1,PhysicalMechanism.Walk,detour,"Follow the surrounding lane",category);
            if(site.kind==SibucaoHazardKind.LowStreet){st.node.choices[0].onFailure=PhysicalConsequence.LoseItem|PhysicalConsequence.Exhaustion;lines[0]="I'm out of that low section. My clothes are soaked.";}
            else{Wire(st,site.kind==SibucaoHazardKind.LGUCordon);lines[0]="I'm past the fallen service line.";}
        }
        Beat(st,lines);EditorUtility.SetDirty(node);installed.Add(st);
        report.Add(site.id+" | "+st.index+" | "+site.kind+" | "+floor+" | "+site.location+" | "+site.reason+" | paths "+st.branches[0].PathLength.ToString("F1")+"m / "+st.branches[1].PathLength.ToString("F1")+"m");
    }
    public static string StartStations()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before installing stations");Context();if(GameObject.Find("SIBUCAO - diegetic storm situations"))throw new Exception("Hazard root already exists; inspect progress before restarting.");
        survey=JsonUtility.FromJson<SibucaoHazardSurvey>(File.ReadAllText("tmp/diegetic/survey.json"));
        timber=Mat("Storm-soaked coconut timber",new Color(.22f,.15f,.095f));metal=Mat("Oxidised galvanized roofing",new Color(.28f,.30f,.29f));leaf=Mat("Rain-darkened fallen fronds",new Color(.16f,.23f,.085f));blue=Mat("Faded blue HDPE",new Color(.07f,.2f,.35f));tape=Mat("Barangay caution yellow",new Color(.79f,.61f,.12f));
        hazardRoot=new GameObject("SIBUCAO - diegetic storm situations").transform;installed=s.physicalDecisions.stations.Take(9).ToList();installIndex=0;report.Clear();
        EditorApplication.update-=InstallTick;EditorApplication.update+=InstallTick;InstallationStatus="Installing 0/"+survey.sites.Count;return InstallationStatus;
    }
    static void InstallTick()
    {
        if(EditorApplication.isCompiling)return;
        try
        {
            if(installIndex<survey.sites.Count)
            {
                AssetDatabase.StartAssetEditing();
                try{InstallOne(survey.sites[installIndex]);}
                catch(Exception e)
                {
                    var site=survey.sites[installIndex];var partial=hazardRoot.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith(site.id+" - "));if(partial)Object.DestroyImmediate(partial.gameObject);
                    report.Add("EXCLUDED "+site.id+" | "+site.location+" | "+e.GetBaseException().Message+". No invisible collision or replacement wall installed.");
                }
                finally{AssetDatabase.StopAssetEditing();}
                installIndex++;File.WriteAllText("tmp/diegetic/installation-index.txt",installIndex.ToString());InstallationStatus="Processed "+installIndex+"/"+survey.sites.Count;File.WriteAllLines("tmp/diegetic/installed-sites.txt",report);return;
            }
            EditorApplication.update-=InstallTick;s.physicalDecisions.stations=installed.ToArray();if(!s.GetComponent<SibucaoFloodExposure>())s.gameObject.AddComponent<SibucaoFloodExposure>();
            foreach(var contact in s.physicalDecisions.stations[3].GetComponentsInChildren<SibucaoBranchContact>(true))if(contact.option&&contact.option.choiceIndex==2){var c=contact.GetComponent<SibucaoElectricalContact>();if(!c)c=contact.gameObject.AddComponent<SibucaoElectricalContact>();c.station=s.physicalDecisions.stations[3];c.choice=2;}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);InstallationStatus="COMPLETE: "+installed.Count+" DecisionNode stations saved (9 original + "+(installed.Count-9)+" additional).";File.AppendAllText("tmp/diegetic/installed-sites.txt","\n"+InstallationStatus);
        }
        catch(Exception e){EditorApplication.update-=InstallTick;InstallationStatus="FAILED at "+survey.sites[installIndex].id+": "+e.GetBaseException().Message;File.AppendAllText("tmp/diegetic/installed-sites.txt","\n"+InstallationStatus);Debug.LogError(InstallationStatus);}
    }
    public static string ResumeStations()
    {
        Context();hazardRoot=GameObject.Find("SIBUCAO - diegetic storm situations").transform;survey=JsonUtility.FromJson<SibucaoHazardSurvey>(File.ReadAllText("tmp/diegetic/survey.json"));
        foreach(var st in hazardRoot.GetComponentsInChildren<PhysicalDecisionStation>(true))if(st.branches==null||st.branches.Any(b=>!b))Object.DestroyImmediate(st.gameObject);
        installed=s.physicalDecisions.stations.Take(9).Concat(hazardRoot.GetComponentsInChildren<PhysicalDecisionStation>(true).OrderBy(st=>st.index)).ToList();installIndex=File.Exists("tmp/diegetic/installation-index.txt")?int.Parse(File.ReadAllText("tmp/diegetic/installation-index.txt")):installed.Count-9;
        report.Clear();if(File.Exists("tmp/diegetic/installed-sites.txt"))report.AddRange(File.ReadAllLines("tmp/diegetic/installed-sites.txt").Where(l=>!l.StartsWith("FAILED")));
        timber=Mat("Storm-soaked coconut timber",new Color(.22f,.15f,.095f));metal=Mat("Oxidised galvanized roofing",new Color(.28f,.30f,.29f));leaf=Mat("Rain-darkened fallen fronds",new Color(.16f,.23f,.085f));blue=Mat("Faded blue HDPE",new Color(.07f,.2f,.35f));tape=Mat("Barangay caution yellow",new Color(.79f,.61f,.12f));
        EditorApplication.update-=InstallTick;EditorApplication.update+=InstallTick;return "Resuming surveyed site "+installIndex;
    }
    public static string RetryMissingSite(string id)
    {
        Context();hazardRoot=GameObject.Find("SIBUCAO - diegetic storm situations").transform;
        if(hazardRoot.GetComponentsInChildren<SibucaoDiegeticHazard>(true).Any(h=>h.siteId==id))return id+" already installed";
        survey=JsonUtility.FromJson<SibucaoHazardSurvey>(File.ReadAllText("tmp/diegetic/survey.json"));installed=s.physicalDecisions.stations.ToList();
        timber=Mat("Storm-soaked coconut timber",new Color(.22f,.15f,.095f));metal=Mat("Oxidised galvanized roofing",new Color(.28f,.30f,.29f));leaf=Mat("Rain-darkened fallen fronds",new Color(.16f,.23f,.085f));blue=Mat("Faded blue HDPE",new Color(.07f,.2f,.35f));tape=Mat("Barangay caution yellow",new Color(.79f,.61f,.12f));
        report.Clear();InstallOne(survey.sites.First(x=>x.id==id));s.physicalDecisions.stations=installed.ToArray();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        File.AppendAllLines("tmp/diegetic/installed-sites.txt",report);return string.Join("\n",report);
    }
}
