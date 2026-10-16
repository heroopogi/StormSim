using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoCoverageInstaller
    {
        const string Folder="Assets/StormSim/Sibucao/RouteCoverage";
        static Material timber,metal;
        static Transform Node(Transform parent,string name,Vector3 local){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 scale,Material material,bool collision=true)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static Material Mat(string query,Color fallback)
        {var id=AssetDatabase.FindAssets(query+" t:Material").FirstOrDefault();if(id!=null)return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));throw new Exception("Existing storm material not found: "+query);}
        static T Asset<T>(string name) where T:ScriptableObject
        {var path=Folder+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(!a){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
        static void Branch(PhysicalDecisionStation st,int choice,PhysicalMechanism mechanism,Vector3[] points,SibucaoStormPassage passage)
        {
            var c=Asset<DecisionChoice>(st.node.id+"_c"+choice);c.id=st.node.id+"_c"+choice;c.label=choice==0?"Release and lift the fallen awning":"Squeeze beside the fallen awning";
            c.category=DecisionCategory.ResourceEquipment;c.scoreCategory=ScoreCategory.Evacuation;c.basePoints=choice==0?14:10;c.baseSuccessChance=choice==0?.87f:.78f;c.timeCost=choice==0?2:3;c.staminaCost=choice==0?3:2;c.failurePenalty=3;c.onFailure=PhysicalConsequence.Stumble;c.minimumTier=WaterTier.Tier1;c.maximumTier=WaterTier.Tier5;
            c.roleModifiers=new[]{new RoleModifier{role="Student"},new RoleModifier{role="Parent"},new RoleModifier{role="Volunteer"}};
            var b=Node(st.transform,"C"+choice+" - "+c.label,Vector3.zero).gameObject.AddComponent<SibucaoPhysicalOption>();b.station=st;b.choiceIndex=choice;b.mechanism=mechanism;b.requiredPassage=passage;b.arrivalRadius=.16f;
            b.path=points.Select((p,i)=>Node(b.transform,"Physical step "+i,p)).ToArray();b.layouts=Array.Empty<PhysicalPathVariant>();b.actionPoint=Node(b.transform,"Action focus",points[0]+Vector3.up);
            b.destination=Node(b.transform,"Destination",points.Last()+Vector3.up).gameObject.AddComponent<BoxCollider>();b.destination.isTrigger=true;b.destination.size=new Vector3(1,3,1);st.node.choices[choice]=c;st.branches[choice]=b;EditorUtility.SetDirty(c);
        }
        public static string Install()
        {
            if(EditorApplication.isPlaying)throw new Exception("Install in Edit Mode.");
            if(GameObject.Find("SIBUCAO - departure storm damage"))throw new Exception("Departure encounters already exist; inspect before changing them.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/StormSim/Sibucao","RouteCoverage");
            timber=Mat("Storm-soaked coconut timber",Color.gray);metal=Mat("Oxidised galvanized roofing",Color.gray);
            var s=Object.FindFirstObjectByType<SibucaoSession>();var all=s.physicalDecisions.stations.ToList();var root=new GameObject("SIBUCAO - departure storm damage").transform;
            var layouts=new GameObject[4];var passages=new SibucaoStormPassage[4];
            for(int h=0;h<4;h++)
            {
                var home=s.homes[h];var layout=Node(root,"Home "+(h+1)+" - displaced porch awning",Vector3.zero);layouts[h]=layout.gameObject;
                var t=Node(layout,"D"+(44+h)+" - storm-damaged home exit",Vector3.zero);t.position=home.transform.TransformPoint(new Vector3(2.5f,.07f,-3.62f));t.rotation=home.transform.rotation*Quaternion.Euler(0,180,0);
                var st=t.gameObject.AddComponent<PhysicalDecisionStation>();st.manager=s.physicalDecisions;st.index=all.Count;st.supplementary=st.departureEncounter=true;st.homeMask=1<<h;st.title="Collapsed porch awning";
                st.node=Asset<DecisionNode>("sibucao_d"+(44+h));st.node.id="sibucao_d"+(44+h);st.node.title=st.title;st.node.prompt="The porch awning has fallen across the doorway.";st.node.category=DecisionCategory.ResourceEquipment;st.node.choices=new DecisionChoice[2];st.branches=new SibucaoPhysicalOption[2];
                var meta=t.gameObject.AddComponent<SibucaoDiegeticHazard>();meta.siteId="D"+(44+h);meta.kind=SibucaoHazardKind.CollapsedRoof;meta.location=home.name+" doorway";meta.reason="Storm-damaged awning caught on its lashings: lift and secure it or use the narrow gap.";
                var p=t.gameObject.AddComponent<SibucaoStormPassage>();passages[h]=p;p.home=h;p.zone=st.index;p.kind=StormPassageKind.TiltRack;p.width=1.4f;p.persistentId="departure_awning_"+h;p.description=meta.reason;p.core=false;p.approachLocal=new Vector3(0,0,-1.1f);
                p.moving=Node(t,"Awning caught on upper lashings",new Vector3(0,2.25f,0));
                // Real sheet collision leaves a 44 cm gap against the original door jamb.
                Box(p.moving,"Displaced GI awning sheet",new Vector3(-.22f,-1.12f,0),new Vector3(.96f,2.24f,.13f),metal);
                for(int rib=0;rib<9;rib++)Box(p.moving,"Corrugation",new Vector3(-.66f+rib*.11f,-1.12f,-.078f),new Vector3(.035f,2.24f,.035f),metal,false);
                for(int side=-1;side<=1;side+=2)Box(p.moving,"Split coconut awning batten",new Vector3(-.22f+side*.39f,-1.12f,.10f),new Vector3(.07f,2.24f,.08f),timber);
                p.fasteners=new Transform[2];
                for(int i=0;i<3;i++)
                {
                    var hand=Box(t,i<2?"Storm-tensioned lashing "+(i+1):"Awning lifting grip",new Vector3(i<2?-.56f+i*.55f:-.2f,i<2?1.12f:1.45f,-.07f),new Vector3(.14f,.14f,.10f),timber);var handle=hand.AddComponent<SibucaoPassageHandle>();handle.passage=p;handle.part=i;
                    hand.GetComponent<Collider>().isTrigger=true;
                    if(i<2)p.fasteners[i]=hand.transform;else p.grip=handle;
                }
                Branch(st,0,PhysicalMechanism.Walk,new[]{new Vector3(-.1f,0,-1),new Vector3(-.1f,0,-.4f),new Vector3(-.1f,-.1f,.4f),new Vector3(-.1f,-.3f,1.1f),new Vector3(-.1f,-.45f,1.7f)},p);
                Branch(st,1,PhysicalMechanism.Squeeze,new[]{new Vector3(.48f,0,-.9f),new Vector3(.48f,0,-.4f),new Vector3(.48f,-.08f,.35f),new Vector3(.48f,-.22f,.85f),new Vector3(.48f,-.45f,1.65f)},null);
                var gate=t.gameObject.AddComponent<SibucaoCoverageGate>();gate.station=st;gate.debris=p;
                var beat=t.gameObject.AddComponent<SibucaoResolutionBeat>();beat.station=st;beat.lines=new[]{"The awning is secured. I'm outside.","I'm through the gap beside the awning."};st.resolutionBeat=beat;
                all.Add(st);EditorUtility.SetDirty(st.node);
            }
            // Old fence layouts stay unused. Only the four visible porch-damage assemblies run.
            if(!s.passages)s.passages=s.gameObject.AddComponent<SibucaoPassageDirector>();s.passages.layouts=layouts;s.passages.passages=passages;s.physicalDecisions.stations=all.ToArray();
            AddStormDetails();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);return "Installed four departure encounters in the scene; not yet saved pending checks.";
        }
        public static void AddStormDetails()
        {
            timber=Mat("Storm-soaked coconut timber",Color.gray);
            foreach(var gate in Object.FindObjectsByType<SibucaoCoverageGate>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(gate.transform.Find("Torn awning supports"))continue;
                var root=Node(gate.transform,"Torn awning supports",Vector3.zero);
                for(int i=0;i<2;i++)
                {
                    var batten=Box(root,"Splintered cantilever batten",new Vector3(-.57f+i*.67f,2.27f,.66f),new Vector3(.065f,.065f,1.4f),timber,false);batten.transform.localRotation=Quaternion.Euler(i==0?12:-9,0,i==0?-8:5);
                    var hand=gate.debris.fasteners[i];var cord=Node(hand,"Coir lashing knot",Vector3.zero).gameObject.AddComponent<LineRenderer>();cord.useWorldSpace=false;cord.sharedMaterial=timber;cord.widthMultiplier=.025f;cord.positionCount=37;
                    for(int k=0;k<37;k++){float angle=k*Mathf.PI/6;cord.SetPosition(k,new Vector3(Mathf.Cos(angle)*.085f,Mathf.Sin(angle)*.065f,-.057f+(k/12)*.012f));}
                    var loose=Node(root,"Frayed loose rope end "+i,Vector3.zero).gameObject.AddComponent<LineRenderer>();loose.useWorldSpace=false;loose.sharedMaterial=timber;loose.widthMultiplier=.014f;loose.positionCount=5;
                    for(int k=0;k<5;k++)loose.SetPosition(k,hand.localPosition+new Vector3(Mathf.Sin(k*1.4f+i)*.06f,-k*.085f,-.075f));
                }
                EditorSceneManager.MarkSceneDirty(gate.gameObject.scene);
            }
        }
    }
}
