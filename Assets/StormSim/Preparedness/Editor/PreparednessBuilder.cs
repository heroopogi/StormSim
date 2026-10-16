using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessBuilder
    {
        public const string Folder="Assets/StormSim/Preparedness";
        public const string ScenePath=Folder+"/TORMSIM_Preparedness.unity";
        static Transform root;static PreparednessSession session;static Material wood,teal,concrete,grass,dark;
        static Font font;static Material textMaterial;static int meshIndex;
        static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=Folder+"/Art/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;return m;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(true);var b=rs.Length>0?rs[0].bounds:new Bounds(go.transform.position,Vector3.one*.1f);
            foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }
        public static string ImportProps()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode before importing.");
            Directory.CreateDirectory(Folder+"/Art/Prefabs");AssetDatabase.Refresh();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Art/TORMSIM_Preparedness_Kit.glb");if(!asset)throw new Exception("GLB is not imported yet.");
            var all=Object.Instantiate(asset);var names=new List<string>();
            try
            {
                foreach(var src in all.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("PREP_")).ToArray())
                {
                    var wrapper=new GameObject(src.name);var visual=Object.Instantiate(src.gameObject,wrapper.transform);
                    visual.name="Model";visual.transform.localPosition=Vector3.zero;
                    // Preserve GLB axis conversion and intentional authored scale.
                    visual.transform.localRotation=Quaternion.Euler(0,180,0)*src.rotation;visual.transform.localScale=src.lossyScale;
                    foreach(var a in wrapper.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
                    foreach(var a in wrapper.GetComponentsInChildren<Animation>())Object.DestroyImmediate(a);
                    var b=BoundsOf(wrapper);visual.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                    foreach(var f in wrapper.GetComponentsInChildren<MeshFilter>())if(!f.sharedMesh||f.sharedMesh.vertexCount==0)throw new Exception("Empty mesh in "+src.name);
                    foreach(var r in wrapper.GetComponentsInChildren<Renderer>())if(r.sharedMaterials.Any(m=>!m||m.shader.name.Contains("Error")))throw new Exception("Invalid material in "+src.name);
                    PrefabUtility.SaveAsPrefabAsset(wrapper,Folder+"/Art/Prefabs/"+src.name+".prefab");Object.DestroyImmediate(wrapper);names.Add(src.name);
                }
            }
            finally{Object.DestroyImmediate(all);}
            if(names.Count!=23)throw new Exception("Expected 23 source prop roots, got "+names.Count);
            AssetDatabase.SaveAssets();return "Imported "+names.Count+" Higgsfield prefabs.";
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material mat,bool solid=true,Transform parent=null)
        {
            // ProBuilder geometry remains editable; persist its mesh for scene reload.
            var pb=ShapeGenerator.GenerateCube(PivotLocation.Center,size);var go=pb.gameObject;go.name=name;go.transform.SetParent(parent?parent:root,false);go.transform.localPosition=position;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;AssetDatabase.CreateAsset(Object.Instantiate(mesh),Folder+"/Art/Geometry/"+(meshIndex++)+".asset");
            go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Geometry/"+(meshIndex-1)+".asset");
            Object.DestroyImmediate(pb);foreach(var c in go.GetComponents<Collider>())Object.DestroyImmediate(c);
            if(solid)go.AddComponent<BoxCollider>().size=size;return go;
        }
        static TextMesh Text(string name,string value,Vector3 position,float size,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent?parent:root,false);go.transform.localPosition=position;
            var t=go.AddComponent<TextMesh>();t.font=font;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=value;t.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=textMaterial;return t;
        }
        static PreparednessItem Bind(GameObject go,string id,PrepKind kind,string title,bool portable)
        {
            var item=go.AddComponent<PreparednessItem>();item.id=id;item.kind=kind;item.displayName=title;item.portable=portable;
            var b=BoundsOf(go);var c=go.AddComponent<BoxCollider>();c.center=go.transform.InverseTransformPoint(b.center);c.size=new Vector3(Mathf.Max(.035f,b.size.x),Mathf.Max(.035f,b.size.y),Mathf.Max(.035f,b.size.z));
            if(portable){var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=item.mass;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.interpolation=RigidbodyInterpolation.Interpolate;}
            return item;
        }
        static PreparednessItem Prop(string model,string id,PrepKind kind,Vector3 position,string title,bool portable=true)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Art/Prefabs/PREP_"+model+".prefab");if(!prefab)throw new Exception("Missing model "+model);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(root,false);go.transform.localPosition=position;go.name=title;
            return Bind(go,id,kind,title,portable);
        }
        static PreparednessItem Zone(string id,PrepKind kind,Vector3 position,Vector3 size,string title)
        {
            var go=new GameObject(title);go.transform.SetParent(root,false);go.transform.localPosition=position;
            var i=go.AddComponent<PreparednessItem>();i.id=id;i.kind=kind;i.displayName=title;i.portable=false;
            var c=go.AddComponent<BoxCollider>();c.size=size;c.center=Vector3.up*.1f;c.isTrigger=true;return i;
        }
        static void Table(string name,Vector3 p,Vector2 size,float height)
        {
            Box(name,p+Vector3.up*(height-.035f),new Vector3(size.x,.07f,size.y),wood);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box(name+" leg",p+new Vector3(x*(size.x/2-.06f),height/2,z*(size.y/2-.06f)),new Vector3(.075f,height,.075f),wood);
        }
        public static string Build()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode before building.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("The active scene has unsaved changes. It was left untouched.");
            if(File.Exists(ScenePath))throw new Exception("Preparedness scene already exists. Open it rather than rebuilding over edits.");
            Directory.CreateDirectory(Folder+"/Art/Geometry");AssetDatabase.Refresh();meshIndex=0;
            var baseHome=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Art/PreparednessHomeShell.prefab");
            if(!baseHome)
            {
                var source=Object.FindFirstObjectByType<StormSim.Sibucao.SibucaoHomeOpening>();if(!source)throw new Exception("Open the existing home scene once to create the independent shell copy.");
                var sourceBag=source.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="SchoolBackpack_Visual");
                if(sourceBag){var bagCopy=Object.Instantiate(sourceBag.gameObject);bagCopy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(bagCopy,Folder+"/Art/HouseholdBackpack.prefab");Object.DestroyImmediate(bagCopy);}
                var clone=Object.Instantiate(source.gameObject);clone.name="Preparedness home shell";clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);clone.transform.localScale=Vector3.one;
                foreach(var t in clone.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if(!t||t==clone.transform)continue;
                    if(t.name.Contains("pack with")||t.name.Contains("equip inventory")||t.name.Contains("physical single-use")||t.name.Contains("instruction paper")||t.name.Contains("Printed wall calendar")||t.name.Contains("Neighbour -")||t.name.Contains("Cinematic eyelids")||t.name.Contains("Delayed neighborhood")||t.name.Contains("Typhoon wind")||t.name.Contains("Fallback local news"))Object.DestroyImmediate(t.gameObject);
                }
                foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true))Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<TextMesh>(true))Object.DestroyImmediate(c.gameObject);
                baseHome=PrefabUtility.SaveAsPrefabAsset(clone,Folder+"/Art/PreparednessHomeShell.prefab");Object.DestroyImmediate(clone);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            root=new GameObject("TORMSIM preparation session").transform;session=root.gameObject.AddComponent<PreparednessSession>();
            wood=Mat("Preparation timber",new Color(.32f,.20f,.11f));teal=Mat("Preparation teal",new Color(.035f,.25f,.26f));concrete=Mat("Preparation path",new Color(.53f,.5f,.43f));grass=Mat("Preparation garden",new Color(.24f,.36f,.12f));dark=Mat("Preparation screen",new Color(.02f,.07f,.12f),true);
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StormSim/Sibucao/Art/Roboto-Regular.ttf");if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMaterial=new Material(Shader.Find("StormSim/World Sign Text"));textMaterial.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(textMaterial,Folder+"/Art/Preparation text.mat");
            var house=(GameObject)PrefabUtility.InstantiatePrefab(baseHome);house.transform.SetParent(root,false);house.SetActive(true);
            var door=house.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Front door hinge");door.localRotation=Quaternion.identity;
            var doorItem=door.gameObject.AddComponent<PreparednessItem>();doorItem.id="door";doorItem.kind=PrepKind.Door;doorItem.displayName="Front door — close/check or open";doorItem.portable=false;doorItem.mechanism=door;doorItem.openEuler=new Vector3(0,-100,0);doorItem.closed=false;doorItem.ApplyVisual();
            var tv=house.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("TV screen"));tv.GetComponent<Renderer>().sharedMaterial=dark;
            session.television=Text("Scenario television bulletin","TORMSIM\nA quiet afternoon",new Vector3(-.85f,1.71f,2.77f),.017f);
            Zone("advisory",PrepKind.Advisory,new Vector3(-.85f,1.6f,2.70f),new Vector3(2.1f,1.1f,.05f),"Television — recheck advisory");
            Box("Quiet green yard",new Vector3(0,-.62f,0),new Vector3(34,.3f,34),grass);
            Box("Dry courtyard",new Vector3(2,-.20f,-6),new Vector3(10,.22f,5),concrete);
            Box("Inland footpath",new Vector3(2.5f,-.20f,-11),new Vector3(2,.22f,7),concrete);
            // Boundary houses and gardens establish a peaceful neighborhood without importing storm logic.
            for(int n=0;n<4;n++)
            {
                var h=Box("Neighbour home "+n,new Vector3(n<2?-11:11,1.15f,(n%2)*10-3),new Vector3(5,3.4f,5),n%2==0?teal:concrete);
                Box("Neighbour roof "+n,h.transform.localPosition+Vector3.up*1.85f,new Vector3(5.5f,.22f,5.5f),wood);
            }
            var sunGo=new GameObject("Clear afternoon sunlight");session.sun=sunGo.AddComponent<Light>();session.sun.type=LightType.Directional;session.sun.intensity=2.2f;sunGo.transform.rotation=Quaternion.Euler(48,-30,0);session.sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.72f,.80f);RenderSettings.fog=false;
            var sky=Mat("Clear afternoon sky",new Color(.48f,.72f,.93f),true);RenderSettings.skybox=null;
            Table("Preparation sideboard",new Vector3(0,0,1.9f),new Vector2(2.0f,.65f),.84f);
            Table("Raised household supply shelf",new Vector3(-3.3f,0,.7f),new Vector2(1.05f,2.7f),1.1f);
            Table("High valuables shelf",new Vector3(-3.1f,0,2.75f),new Vector2(1.45f,.5f),1.35f);
            var backpackAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Art/HouseholdBackpack.prefab");
            var bag=backpackAsset?(GameObject)PrefabUtility.InstantiatePrefab(backpackAsset):Box("Household go-bag",Vector3.zero,new Vector3(.36f,.45f,.20f),teal,false);
            bag.transform.SetParent(root,false);bag.transform.position=new Vector3(2.1f,.35f,-2.7f);
            var bi=Bind(bag,"bag",PrepKind.Bag,"Go-bag — open/close, pack held item",true);bi.mass=1;
            var bagZone=Zone("bag-zone",PrepKind.BagZone,bag.transform.position,new Vector3(.45f,.5f,.35f),"Open go-bag storage");bagZone.transform.SetParent(bag.transform,true);
            Zone("supply-zone",PrepKind.SupplyZone,new Vector3(-3.3f,1.14f,-.45f),new Vector3(1.2f,.7f,2.5f),"Raised household supply shelf — place water and food");
            Zone("high-zone",PrepKind.HighShelf,new Vector3(-3.1f,1.4f,2.75f),new Vector3(1.5f,.7f,.55f),"High shelf — place disconnected electronics and valuables");
            Zone("inside-zone",PrepKind.IndoorZone,new Vector3(2.65f,.10f,-1.65f),new Vector3(1.7f,.7f,.65f),"Clear indoor storage near the exit");
            Zone("charger",PrepKind.ChargeZone,new Vector3(3.0f,.94f,1.45f),new Vector3(.65f,.40f,.5f),"Charging pad — place phone or power bank");
            Box("Charging pad symbol backing",new Vector3(3,.93f,1.5f),new Vector3(.65f,.014f,.40f),teal,false);
            for(int n=0;n<10;n++){var w=Prop("WaterContainer_5L","water-"+n,PrepKind.Water,new Vector3(-3.5f+(n%2)*.38f,.075f,-1.0f-(n/2)*.35f),"Sealed drinking-water container (5 L)");w.litres=5;w.mass=5.2f;w.Body.mass=w.mass;}
            for(int n=0;n<3;n++)Prop("FoodAndMedicine","food-"+n,PrepKind.Food,new Vector3(2.55f+n*.32f,.93f,.62f),"Ready-to-eat food pack");
            var flashlight=Prop("Flashlight","flashlight",PrepKind.Flashlight,new Vector3(-.65f,.89f,1.88f),"Flashlight — fit batteries and test");
            var lamp=new GameObject("Flashlight beam");lamp.transform.SetParent(flashlight.transform,false);lamp.transform.localPosition=Vector3.up*.24f;lamp.transform.localRotation=Quaternion.Euler(-90,0,0);flashlight.deviceLight=lamp.AddComponent<Light>();flashlight.deviceLight.type=LightType.Spot;flashlight.deviceLight.range=9;flashlight.deviceLight.intensity=3;flashlight.deviceLight.spotAngle=45;flashlight.deviceLight.enabled=false;
            Prop("PortableRadio","radio",PrepKind.Radio,new Vector3(-.2f,.89f,1.88f),"Portable radio — fit batteries and test");
            for(int n=0;n<3;n++){var b=Prop("BatteryOrganizer","cells-"+n,PrepKind.Batteries,new Vector3(.28f+n*.20f,.89f,1.88f),"Matching spare battery organizer");b.cells=2;}
            var firstAid=Prop("FirstAidCase","first-aid",PrepKind.FirstAid,new Vector3(2.6f,.93f,1.04f),"First-aid case — inspect and close");firstAid.closed=true;
            firstAid.mechanism=firstAid.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Lid_Hinge");firstAid.openEuler=new Vector3(-100,0,0);
            Prop("FoodAndMedicine","medicine",PrepKind.Medicine,new Vector3(3.3f,.93f,1.02f),"Household medicine pack — check care card");
            Prop("DocumentPouch","documents",PrepKind.Documents,new Vector3(2.5f,.93f,1.58f),"Document pouch — seal before packing");
            Prop("Phone","phone",PrepKind.Phone,new Vector3(.5f,.89f,2.08f),"Phone").charge=20;
            Prop("PowerBankAndCable","powerbank",PrepKind.PowerBank,new Vector3(.76f,.89f,2.08f),"Power bank").charge=10;
            Prop("PowerBankAndCable","cable",PrepKind.Cable,new Vector3(.25f,.89f,2.08f),"Matching charging cable set");
            Prop("HygieneDryBag","hygiene",PrepKind.Hygiene,new Vector3(3.22f,.93f,1.82f),"Sealed hygiene dry bag");
            Prop("FoldedRaincoat","raincoat",PrepKind.Decoration,new Vector3(2.62f,.93f,1.88f),"Folded dry raincoat");
            Prop("BatteryLantern","electronics",PrepKind.Electronics,new Vector3(-2.75f,.075f,2.35f),"Portable household lamp — disconnect, then elevate");
            Prop("HouseholdPlanBoard","valuables",PrepKind.Valuables,new Vector3(-2.45f,.075f,2.3f),"Family keepsake — raise above exposure");
            var window=Prop("WindowLatchDemonstrator","window",PrepKind.Window,new Vector3(3.75f,1.2f,.10f),"Accessible window — close and check latch",false);
            window.mechanism=window.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Window_Hinge");window.openEuler=new Vector3(0,65,0);window.ApplyVisual();
            for(int n=0;n<2;n++)Prop(n==0?"CleanupBucket":"RubberBoots","outdoor-"+n,PrepKind.Outdoor,new Vector3(4.4f+n*.8f,-.08f,-5.5f),"Loose outdoor belongings — bring indoors");
            var drain=Prop("SurfaceDrain","drain",PrepKind.Decoration,new Vector3(.1f,-.08f,-5.8f),"Accessible surface drain",false);
            PrefabUtility.UnpackPrefabInstance(drain.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var t in drain.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Removable dry litter")).ToArray())
            {
                t.SetParent(root,true);Bind(t.gameObject,"litter-"+t.GetSiblingIndex(),PrepKind.Litter,"Dry surface litter — lift with tongs and put in bin",true);
            }
            Prop("LitterTongs","tongs",PrepKind.Tool,new Vector3(.55f,-.08f,-5.8f),"Litter tongs");
            Table("Porch equipment bench",new Vector3(5.5f,-.08f,-4.3f),new Vector2(2,.7f),.8f);
            Prop("WorkGloves","gloves",PrepKind.Decoration,new Vector3(4.85f,.74f,-4.3f),"Work gloves");
            Prop("ScrubBrush","brush",PrepKind.Decoration,new Vector3(5.2f,.74f,-4.3f),"Cleanup brush");
            Prop("WasteBags","waste-bags",PrepKind.Decoration,new Vector3(5.6f,.74f,-4.3f),"Waste bags");
            Prop("EmergencyWhistle","whistle",PrepKind.Decoration,new Vector3(.05f,.89f,2.08f),"Emergency whistle");
            Prop("DrySupplyBox","drybox",PrepKind.Decoration,new Vector3(5.95f,.74f,-4.3f),"Dry storage box");
            Prop("CleanupBucket","bin",PrepKind.Decoration,new Vector3(-.7f,-.08f,-5.7f),"Cleanup bin",false);
            Zone("bin-zone",PrepKind.BinZone,new Vector3(-.7f,.15f,-5.7f),new Vector3(.4f,.4f,.4f),"Cleanup bin — collect dry litter");
            Prop("HouseholdPlanBoard","plan-primary",PrepKind.Plan,new Vector3(1.4f,1.06f,2.8f),"Primary route — inland community hall",false);
            Prop("HouseholdPlanBoard","plan-backup",PrepKind.Plan,new Vector3(1.85f,1.06f,2.8f),"Backup route — elevated school",false);
            session.statusBoard=Text("Household scenario information","",new Vector3(1.75f,2.15f,3.30f),.014f);
            Zone("bed",PrepKind.Bed,new Vector3(-1.4f,.72f,-1.3f),new Vector3(1.5f,.5f,2.4f),"Rest until tomorrow — recheck advisory first");
            Zone("exit",PrepKind.Exit,new Vector3(2.5f,.9f,-8.5f),new Vector3(1.8f,1.8f,.4f),"Leave early by the inland route");
            Text("Route exit sign","INLAND ROUTE\nLeave early",new Vector3(2.5f,1.7f,-8.7f),.035f).transform.rotation=Quaternion.Euler(0,180,0);
            var avatar=new GameObject("Preparation player");avatar.transform.position=new Vector3(.25f,.10f,-1.6f);var cc=avatar.AddComponent<CharacterController>();cc.height=1.72f;cc.radius=.24f;cc.center=Vector3.up*.86f;cc.stepOffset=.25f;
            var player=avatar.AddComponent<PreparednessPlayer>();session.player=player;player.session=session;
            var eye=new GameObject("Preparation camera");eye.transform.SetParent(avatar.transform,false);eye.transform.localPosition=Vector3.up*1.63f;player.view=eye.AddComponent<Camera>();player.view.tag="MainCamera";player.view.nearClipPlane=.06f;player.view.farClipPlane=90;player.view.backgroundColor=new Color(.48f,.72f,.93f);player.view.clearFlags=CameraClearFlags.SolidColor;eye.AddComponent<AudioListener>();eye.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            foreach(bool right in new[]{true,false}){var hand=new GameObject(right?"Tracked right controller":"Tracked left controller");hand.transform.SetParent(avatar.transform,false);if(right)player.rightHand=hand.transform;else player.leftHand=hand.transform;}
            player.worldPrompt=Text("VR contextual prompt","",new Vector3(0,-.25f,1.25f),.008f,eye.transform);player.worldPrompt.gameObject.SetActive(false);
            session.items=root.GetComponentsInChildren<PreparednessItem>(true);
            foreach(var item in session.items)item.ApplyVisual();
            if(session.items.Select(i=>i.id).Distinct().Count()!=session.items.Length)throw new Exception("Duplicate persistence IDs.");
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            PreparednessHomeLayout.Apply();
            Selection.activeGameObject=avatar;return "Created independent playable preparation scene with "+session.items.Length+" interactables. Existing storm and build settings unchanged.";
        }
    }
}


