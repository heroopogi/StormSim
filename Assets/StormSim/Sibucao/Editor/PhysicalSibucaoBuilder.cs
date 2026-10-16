using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
    public static class PhysicalSibucaoBuilder
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Physical/";
        static Material wood,metal,cloth,red,water;
        static Material Mat(string name,Color color)
        {
            var path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Shape(string name,Transform parent,Vector3 position,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Mark(GameObject go,int index,string purpose){var marker=go.GetComponent<DecisionTriggerMarker>();if(!marker)marker=go.AddComponent<DecisionTriggerMarker>();marker.decision=index+1;marker.purpose=purpose;}
        static PhysicalDecisionSensor Zone(PhysicalDecisionStation s,string name,Vector3 p,Vector3 size,PhysicalSensorKind kind,int outcome=0,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent?parent:s.transform,false);go.transform.localPosition=p;
            var col=go.AddComponent<BoxCollider>();col.isTrigger=true;col.size=size;
            var sensor=go.AddComponent<PhysicalDecisionSensor>();sensor.station=s;sensor.kind=kind;sensor.outcome=outcome;Mark(go,s.index,kind.ToString());return sensor;
        }
        static PhysicalGrabbable Grab(GameObject go,PhysicalDecisionStation s,PhysicalItemKind kind,string id,float mass=1)
        {
            var rb=go.GetComponent<Rigidbody>();if(!rb)rb=go.AddComponent<Rigidbody>();rb.mass=mass;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var g=go.AddComponent<PhysicalGrabbable>();g.kind=kind;g.station=s;g.persistentId=id;Mark(go,s?s.index:0,"Physical "+kind+" spawn");return g;
        }
        static void Stick(PhysicalDecisionStation s,Vector3 p)
        {
            var go=new GameObject("Depth probe stick - physically dip tip");go.transform.SetParent(s.transform,false);go.transform.localPosition=p;
            Shape("Bamboo shaft",go.transform,Vector3.zero,new Vector3(.035f,.8f,.035f),wood,PrimitiveType.Cylinder);
            var g=Grab(go,s,PhysicalItemKind.Stick,"B"+s.index+"_stick",.7f);
            var tip=new GameObject("Stick tip");tip.transform.SetParent(go.transform,false);tip.transform.localPosition=Vector3.down*.8f;g.tip=tip.transform;
            var tipCollider=tip.AddComponent<SphereCollider>();tipCollider.radius=.04f;
        }
        static GameObject Resident(Transform parent,string name,Vector3 p)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StormSim/Sibucao/Art/Expansion/PLACEHOLDER_Resident_Walking.prefab");
            var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab);actor.name="PLACEHOLDER_"+name;actor.transform.SetParent(parent,false);actor.transform.localPosition=p;actor.transform.localRotation=Quaternion.identity;
            var npc=actor.GetComponent<SibucaoResident>();npc.enabled=false;
            var capsule=actor.AddComponent<CapsuleCollider>();capsule.center=Vector3.up*.85f;capsule.height=1.7f;capsule.radius=.27f;
            if(!actor.GetComponent<PlaceholderCharacter>())actor.AddComponent<PlaceholderCharacter>();return actor;
        }
        static void MarkRoute(PhysicalDecisionStation s,Vector3 p)
        {
            var go=new GameObject("Physical route ribbon");go.transform.SetParent(s.transform,false);go.transform.localPosition=p;
            for(int i=0;i<4;i++){Shape("Ribbon stake",go.transform,new Vector3(0,.5f,i),new Vector3(.04f,1,.04f),wood);Shape("Cloth route ribbon",go.transform,new Vector3(0,.9f,i),new Vector3(.35f,.08f,.02f),cloth,PrimitiveType.Cube,false);}
            s.routeMark=go;
        }
        static void Water(PhysicalDecisionStation s,Vector3 p)
        {
            Shape("Shallow moving canal overflow",s.transform,p,new Vector3(2.7f,.04f,4),water,PrimitiveType.Cube,false);
            Zone(s,"Probe water volume",p-Vector3.up*.08f,new Vector3(2.7f,.25f,4),PhysicalSensorKind.Water);
        }
        public static string Install()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode.");
            var session=Object.FindFirstObjectByType<SibucaoSession>();if(session.physicalDecisions)return "Physical decisions already installed.";
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(string behavior in new[]{"Walking","Running","Panicking","Evacuating"})
            {
                string old="Assets/StormSim/Sibucao/Art/Expansion/Resident "+behavior+".prefab";
                string path="Assets/StormSim/Sibucao/Art/Expansion/PLACEHOLDER_Resident_"+behavior+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(old)){string error=AssetDatabase.MoveAsset(old,path);if(!string.IsNullOrEmpty(error))throw new System.Exception(error);}
                var prefab=PrefabUtility.LoadPrefabContents(path);prefab.name="PLACEHOLDER_Resident_"+behavior;if(!prefab.GetComponent<PlaceholderCharacter>())prefab.AddComponent<PlaceholderCharacter>();PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
            }
            foreach(var resident in Object.FindObjectsByType<SibucaoResident>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(!resident.name.StartsWith("PLACEHOLDER_"))resident.name="PLACEHOLDER_"+resident.name;if(!resident.GetComponent<PlaceholderCharacter>())resident.gameObject.AddComponent<PlaceholderCharacter>();}
            wood=Mat("Weathered bamboo",new Color(.32f,.24f,.14f));metal=Mat("Dull galvanized metal",new Color(.34f,.38f,.4f));cloth=Mat("Route cloth",new Color(.49f,.76f,.86f));red=Mat("Worn equipment canvas",new Color(.4f,.12f,.08f));
            water=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Sibucao/Art/Home/Floodwater.mat");if(!water)water=session.home.floodSurface.GetComponent<Renderer>().sharedMaterial;
            var root=new GameObject("SIBUCAO - physical decision systems");var manager=root.AddComponent<DecisionTreeManager>();session.physicalDecisions=manager;
            var grabber=session.player.gameObject.AddComponent<PhysicalGrabber>();grabber.player=session.player;session.player.grabber=grabber;manager.Grabber=grabber;manager.stations=new PhysicalDecisionStation[9];
            for(int i=0;i<9;i++)
            {
                var go=new GameObject("B-"+(i+1)+" - "+DecisionTreeManager.Titles[i]);go.transform.SetParent(root.transform,false);
                float distance=35+i*73;go.transform.position=session.route.Sample(distance);var f=session.route.Sample(distance+2)-session.route.Sample(distance);f.y=0;go.transform.rotation=Quaternion.LookRotation(f);
                var s=go.AddComponent<PhysicalDecisionStation>();s.index=i;s.title=DecisionTreeManager.Titles[i];s.manager=manager;s.routeDistance=distance;s.timeout=i==0||i==8?15:60;manager.stations[i]=s;
                Zone(s,"Encounter approach",new Vector3(0,1,-5),new Vector3(8,3,8),PhysicalSensorKind.Enter);
                if(i==0){Stick(s,new Vector3(1.4f,1,-4));Water(s,new Vector3(0,.1f,1));}
                if(i==1)
                {
                    var source=GameObject.Find("car 185 - ");
                    for(int c=0;c<2;c++)
                    {
                        var car=Object.Instantiate(source,s.transform);car.name="Stalled car "+(c+1);car.transform.localPosition=new Vector3(c==0?-1.7f:1.7f,.05f,c==0?0:3);car.transform.localRotation=Quaternion.Euler(0,c==0?70:-65,0);car.transform.localScale=source.transform.localScale*.65f;
                        foreach(var component in car.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(component);
                    }
                    Zone(s,"Tight physical gap",new Vector3(0,1,1.5f),new Vector3(.65f,1.8f,.6f),PhysicalSensorKind.Path,1);
                    Zone(s,"Hood contact",new Vector3(-1.7f,1.2f,0),new Vector3(1.8f,.25f,1.2f),PhysicalSensorKind.Hazard,2);
                    Zone(s,"Alternate street",new Vector3(-4,1,1),new Vector3(1.3f,2.5f,5),PhysicalSensorKind.Path,0);
                    MarkRoute(s,new Vector3(-3.7f,0,-3));
                }
                if(i==2)
                {
                    Water(s,new Vector3(0,.15f,1));Stick(s,new Vector3(-1.4f,1,-4));
                    Zone(s,"Main road",new Vector3(0,1,3),new Vector3(2.8f,2,1),PhysicalSensorKind.Path,1);
                    Zone(s,"Alley entrance",new Vector3(3.5f,1,0),new Vector3(1.3f,2,5),PhysicalSensorKind.Path,2);
                    s.conversationActor=Resident(s.transform,"DoorwayResident",new Vector3(-2.2f,0,-2)).transform;s.conversationLine="The canal has overflowed. There are passages on both sides of this junction.";MarkRoute(s,new Vector3(3.1f,0,-2));
                }
                if(i==3)
                {
                    for(int c=0;c<8;c++)
                    {
                        var wire=Shape("Downed wire "+c,s.transform,new Vector3(-1.6f+c*.45f,.12f,Mathf.Sin(c)*.2f),new Vector3(.48f,.04f,.04f),metal);
                        wire.transform.localRotation=Quaternion.Euler(0,Mathf.Cos(c)*15,0);wire.GetComponent<BoxCollider>().isTrigger=true;wire.GetComponent<BoxCollider>().size=new Vector3(1,4,4);
                        var sensor=wire.AddComponent<PhysicalDecisionSensor>();sensor.station=s;sensor.kind=PhysicalSensorKind.Hazard;Mark(wire,i,"Wire contact only");
                    }
                    Zone(s,"Close pass",new Vector3(2.1f,1,0),new Vector3(.55f,2,1),PhysicalSensorKind.Path,1);
                    Zone(s,"Wide detour",new Vector3(-3.4f,1,0),new Vector3(1.5f,2,3),PhysicalSensorKind.Path,0);
                }
                if(i==4)
                {
                    var spill=new GameObject("Visible broken strap and spilled items");spill.transform.SetParent(s.transform,false);s.spill=spill;
                    var bag=Shape("Go-bag with broken strap",spill.transform,new Vector3(0,.25f,0),new Vector3(.45f,.4f,.23f),red);
                    for(int seam=0;seam<3;seam++)Shape("Bag seam",bag.transform,new Vector3((seam-1)*.25f,0,-.51f),new Vector3(.025f,.85f,.04f),metal,PrimitiveType.Cube,false);
                    var socket=Zone(s,"Buckle socket",new Vector3(.22f,.4f,0),Vector3.one*.4f,PhysicalSensorKind.StrapSocket,0,spill.transform);
                    Shape("Metal buckle",socket.transform,Vector3.zero,new Vector3(.12f,.06f,.04f),metal,PrimitiveType.Cube,false);
                    var strap=Shape("Loose strap - place into buckle",spill.transform,new Vector3(.65f,.2f,-.4f),new Vector3(.08f,.3f,.035f),red);Grab(strap,s,PhysicalItemKind.Strap,"B4_strap",.15f);
                    for(int n=0;n<3;n++){var item=Shape("Spilled item "+n,spill.transform,new Vector3(-.5f+n*.45f,.2f,-.6f),new Vector3(.15f,.22f,.12f),n==0?cloth:wood);Grab(item,s,PhysicalItemKind.Supply,"B4_supply_"+n,.4f);}
                }
                if(i==5||i==7)
                {
                    var carrier=new GameObject(i==5?"Wheelchair - physical handle":"Evacuee - physical assistance point");carrier.transform.SetParent(s.transform,false);carrier.transform.localPosition=new Vector3(1,0,0);
                    var actor=Resident(carrier.transform,i==5?"WheelchairResident":"CollapsedEvacuee",i==5?new Vector3(0,-.35f,0):Vector3.zero);
                    if(i==5)
                    {
                        actor.transform.localScale=Vector3.one;actor.transform.localPosition=Vector3.zero;var occupant=actor.GetComponent<CapsuleCollider>();occupant.center=new Vector3(0,1.05f,0);occupant.height=.95f;
                        for(int side=-1;side<=1;side+=2){var wheel=Shape("Wheelchair wheel",carrier.transform,new Vector3(side*.38f,.38f,0),new Vector3(.72f,.06f,.72f),metal,PrimitiveType.Cylinder);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
                        Shape("Chair seat",carrier.transform,new Vector3(0,.65f,0),new Vector3(.65f,.12f,.7f),cloth);
                        Shape("Back support",carrier.transform,new Vector3(0,.95f,-.3f),new Vector3(.65f,.65f,.1f),metal);
                        Shape("Grab handle",carrier.transform,new Vector3(0,1.2f,-.48f),new Vector3(.65f,.05f,.08f),metal);
                    }
                    var assist=Grab(carrier,s,PhysicalItemKind.Assist,"B"+i+"_assist",i==5?30:24);assist.Body.constraints=i==5?RigidbodyConstraints.FreezeRotation:RigidbodyConstraints.None;s.assist=assist;
                    if(i==5){var pose=carrier.AddComponent<PhysicalAssistPose>();pose.actor=actor.GetComponent<SibucaoResident>();pose.body=assist;pose.wheelchair=true;}
                    if(i==7)Zone(s,"First-aid physical contact",new Vector3(0,.85f,0),new Vector3(.75f,.7f,.75f),PhysicalSensorKind.MedicalContact,0,carrier.transform);
                    Zone(s,"Clear ground - entire assisted body",new Vector3(-2.2f,1,4.5f),new Vector3(2,3,2),PhysicalSensorKind.AssistDestination);
                }
                if(i==6)
                {
                    // The moored workboat is too large for a street checkpoint; use an explicitly named functional rescue-boat placeholder.
                    var boat=new GameObject("PLACEHOLDER_RescueBoat - boarding collision prototype");boat.transform.SetParent(s.transform,false);boat.transform.localPosition=new Vector3(2.8f,0,1);
                    Shape("Boat deck",boat.transform,new Vector3(0,.25f,0),new Vector3(1.4f,.2f,3.4f),metal);
                    for(int side=-1;side<=1;side+=2){var tube=Shape("Inflatable tube",boat.transform,new Vector3(side*.72f,.5f,0),new Vector3(.45f,1.8f,.45f),red,PrimitiveType.Capsule);tube.transform.localRotation=Quaternion.Euler(90,0,0);}
                    for(int seat=0;seat<2;seat++)Shape("Bench",boat.transform,new Vector3(0,.5f,seat*1.3f-.7f),new Vector3(1.3f,.12f,.4f),wood);
                    var gangway=Shape("Boat gangway",s.transform,new Vector3(2.8f,.15f,-2.7f),new Vector3(1.1f,.12f,2.5f),wood);gangway.transform.localRotation=Quaternion.Euler(-8,0,0);
                    Zone(s,"Boarding deck",new Vector3(2.8f,1,1),new Vector3(1.2f,1.6f,2.5f),PhysicalSensorKind.Path,1);
                    Zone(s,"Continue original road",new Vector3(-.8f,1,4),new Vector3(2,2,1),PhysicalSensorKind.Path,2);
                    s.conversationActor=Resident(s.transform,"Responder",new Vector3(-2,0,-1)).transform;s.conversationLine="This boat is going to another landing. The road and the boat leave from this checkpoint.";MarkRoute(s,new Vector3(-2,0,2));
                }
                if(i==8)
                {
                    Zone(s,"Path away from creek",new Vector3(3,1,3),new Vector3(2,2,3),PhysicalSensorKind.Boundary,0);
                    Zone(s,"Creek-side path",new Vector3(-1,1,5),new Vector3(2,2,1),PhysicalSensorKind.Boundary,1);
                    MarkRoute(s,new Vector3(2.6f,0,-2));
                }
                if(i==0||i==8)
                {
                    s.ambient=go.AddComponent<AudioSource>();s.ambient.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"Creek_Roar_Procedural_Placeholder.wav");s.ambient.loop=true;s.ambient.playOnAwake=false;s.ambient.spatialBlend=1;s.ambient.minDistance=4;s.ambient.maxDistance=22;s.ambient.volume=.65f;
                }
            }
            foreach(var e in session.encounters)e.gameObject.SetActive(false);
            foreach(var h in session.homes)
            {
                var kit=Shape("First-aid kit - physical single-use item",h.transform,new Vector3(-1.7f,.8f,.3f),new Vector3(.3f,.17f,.22f),red);Grab(kit,null,PhysicalItemKind.FirstAid,h.name+"_firstaid",.6f);
                foreach(var t in h.supplies)if(!t.GetComponent<PhysicalGrabbable>()){var item=Grab(t.gameObject,null,PhysicalItemKind.Supply,h.name+"_"+t.name,.5f);item.supplyBit=System.Array.IndexOf(h.supplies,t);}
                var container=Shape("Carry basket - place supplies inside",h.transform,new Vector3(-1.8f,.4f,.3f),new Vector3(.65f,.12f,.55f),wood);
                Grab(container,null,PhysicalItemKind.Bag,h.name+"_carrybag",1.4f);
                Zone(manager.stations[0],"Basket contents volume",new Vector3(0,2.5f,0),new Vector3(.9f,4,.9f),PhysicalSensorKind.SupplyContainer,0,container.transform);
                for(int side=-1;side<=1;side+=2){Shape("Basket woven side",container.transform,new Vector3(side*.46f,1.4f,0),new Vector3(.08f,2.6f,.94f),wood);Shape("Basket woven end",container.transform,new Vector3(0,1.4f,side*.46f),new Vector3(.84f,2.6f,.08f),wood);}
            }
            EditorUtility.SetDirty(session);EditorUtility.SetDirty(session.player);var scene=SceneManager();EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Nine physical stations installed; legacy encounters disabled; four resident prefabs renamed and marked.";
        }
        static UnityEngine.SceneManagement.Scene SceneManager()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }
}

