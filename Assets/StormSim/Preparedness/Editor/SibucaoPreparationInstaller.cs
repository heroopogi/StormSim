using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class SibucaoPreparationInstaller
    {
        static Transform root;static Material wood,metal,cloth;static PreparednessSession prep;
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static Bounds BoundsOf(GameObject g){var b=new Bounds(g.transform.position,Vector3.zero);foreach(var r in g.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);return b;}
        static PreparednessItem Bind(GameObject go,string id,string label,PrepKind kind,bool portable)
        {
            var i=go.AddComponent<PreparednessItem>();i.id=id;i.displayName=label;i.kind=kind;i.portable=portable;
            var b=BoundsOf(go);var c=go.AddComponent<BoxCollider>();c.center=go.transform.InverseTransformPoint(b.center);c.size=Vector3.Max(b.size,Vector3.one*.09f);
            if(portable){var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=.5f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;}
            return i;
        }
        static PreparednessItem Prop(string asset,string id,string label,Vector3 p)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(PreparednessBuilder.Folder+"/Art/Prefabs/PREP_"+asset+".prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(source);g.transform.SetParent(root,false);g.transform.localPosition=p;g.name=label;return Bind(g,id,label,PrepKind.Tool,true);
        }
        static PreparednessHouseTask Task(string id,string label,Vector3 p,string tool,string action,string outcome,bool outside=false,bool consume=false)
        {
            var go=new GameObject(label);go.transform.SetParent(root,false);go.transform.localPosition=p;
            var i=go.AddComponent<PreparednessItem>();i.id=id;i.displayName=label;i.kind=PrepKind.HouseholdTask;i.portable=false;
            var c=go.AddComponent<BoxCollider>();c.size=new Vector3(.45f,.45f,.45f);c.isTrigger=true;
            var t=go.AddComponent<PreparednessHouseTask>();t.requiredItem=tool;t.instruction=action;t.result=outcome;t.exterior=outside;t.consumeMaterial=consume;t.workSeconds=outside?4:3;
            return t;
        }
        static void Pile(PreparednessHouseTask t,int seed)
        {
            var go=new GameObject("Uncleared debris");go.transform.SetParent(t.transform,false);var rng=new System.Random(seed);
            for(int n=0;n<7;n++){var p=Box("Loose dry debris",go.transform,new Vector3((float)rng.NextDouble()*.45f-.22f,.01f,(float)rng.NextDouble()*.4f-.2f),new Vector3(.13f,.015f,.07f),wood,false);p.transform.localRotation=Quaternion.Euler(0,rng.Next(180),0);}
            t.before=go;
        }
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Stop play mode first.");
            var game=Object.FindFirstObjectByType<SibucaoSession>();if(!game||game.gameObject.scene.path!="Assets/Scenes/SIBUCAO_RAIN.unity")throw new Exception("Open actual SIBUCAO_RAIN scene.");
            if(game.GetComponent<SibucaoPreparationBridge>())throw new Exception("Already installed. Do not overwrite integration edits.");
            var content=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PreparednessBuilder.Folder+"/Art/SibucaoPreparationContent.prefab"));
            PrefabUtility.UnpackPrefabInstance(content,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var home=game.homes[0];content.transform.SetPositionAndRotation(home.transform.position,home.transform.rotation);
            prep=content.GetComponentInChildren<PreparednessSession>();root=prep.transform;prep.homeStart=home.standingPoint;prep.sun=game.weather.sun;
            var bridge=game.gameObject.AddComponent<SibucaoPreparationBridge>();bridge.content=content;bridge.preparation=prep;bridge.firstHome=home;
            wood=AssetDatabase.LoadAssetAtPath<Material>(PreparednessBuilder.Folder+"/Art/Preparation timber.mat");
            metal=AssetDatabase.LoadAssetAtPath<Material>(PreparednessBuilder.Folder+"/Art/Preparation teal.mat");cloth=AssetDatabase.LoadAssetAtPath<Material>(PreparednessBuilder.Folder+"/Art/Preparation path.mat");
            var hidden=new List<GameObject>();
            foreach(var h in game.homes)
            {
                foreach(var t in h.GetComponentsInChildren<Transform>(true))
                    if(t.name.Contains("pack with")||t.name.Contains("equip inventory")||t.name.Contains("physical single-use")||t.name.Contains("instruction paper")||t.name.Contains("Neighbour -")||t.name.Contains("Fallback local news"))hidden.Add(t.gameObject);
                foreach(var supply in h.supplies)if(supply)hidden.Add(supply.gameObject);
                if(h.fallbackNews)hidden.Add(h.fallbackNews);if(h.floodSurface)hidden.Add(h.floodSurface.gameObject);
            }
            foreach(var passage in Object.FindObjectsByType<SibucaoStormPassage>(FindObjectsSortMode.None))hidden.Add(passage.gameObject);
            bridge.hiddenDuringPreparation=hidden.Distinct().ToArray();
            var doors=new List<PreparednessItem>();
            for(int n=0;n<game.homes.Length;n++)
            {
                var hinge=game.homes[n].doorHinge;var i=hinge.GetComponent<PreparednessItem>();if(!i)i=hinge.gameObject.AddComponent<PreparednessItem>();
                i.id=n==0?"door":"neighbor-door-"+n;i.displayName=n==0?"Home front door":"Neighbor's front door — open / close";i.portable=false;i.kind=PrepKind.Door;i.closed=true;i.mechanism=hinge;i.openEuler=new Vector3(0,100,0);doors.Add(i);
            }
            bridge.houseDoors=doors.ToArray();
            // Reuse the existing household broom and Higgsfield cleanup tools.
            var sourceBroom=home.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Walis tambo beside front door");
            var broom=Object.Instantiate(sourceBroom.gameObject,root);broom.name="Household broom";broom.transform.localPosition=new Vector3(.85f,.08f,-3.1f);broom.transform.localRotation=Quaternion.identity;
            Bind(broom,"house-broom","Broom — sweep dry floor debris",PrepKind.Tool,true);
            var screwdriver=new GameObject("Hand screwdriver");screwdriver.transform.SetParent(root,false);screwdriver.transform.localPosition=new Vector3(-1.4f,.94f,2.8f);
            Box("Handle",screwdriver.transform,Vector3.zero,new Vector3(.045f,.12f,.045f),metal,false);Box("Shaft",screwdriver.transform,new Vector3(0,.13f,0),new Vector3(.009f,.16f,.009f),cloth,false);
            Bind(screwdriver,"screwdriver","Screwdriver — tighten accessible fittings",PrepKind.Tool,true);
            Prop("WorkGloves","house-gloves","Work gloves — handle dry debris",new Vector3(-3.42f,.93f,.97f));
            Prop("WasteBags","house-wastebags","Waste bags — bag household refuse",new Vector3(-2.6f,.93f,.75f));
            Prop("ScrubBrush","house-brush","Stiff brush — clear dry drain grate",new Vector3(.6f,.18f,-2.9f));
            for(int n=0;n<4;n++)
            {
                var material=new GameObject("Stored shutter panel "+(n+1));material.transform.SetParent(root,false);material.transform.localPosition=new Vector3(-3.65f+n*.13f,.12f,-3.08f);
                Box("Timber panel",material.transform,new Vector3(0,.42f,0),new Vector3(.055f,.84f,.52f),wood,false);
                Bind(material,"panel-"+n,"Pre-cut window panel "+(n+1),PrepKind.Tool,true).mass=2;
                int side=n<2?-1:1;float z=n%2==0?-1.1f:1.1f;
                var task=Task("secure-window-"+n,"Secure window panel "+(n+1),new Vector3(side*3.77f,1.55f,z),"panel-"+n,"Carry the matching pre-cut panel to its existing mounts.","Panel secured to the existing mounts. This protects this opening; it does not make the house flood-proof.",false,true);
                var panel=Box("Mounted window protection",task.transform,new Vector3(side*.05f,0,0),new Vector3(.08f,1.1f,1.75f),wood);task.after=panel;panel.SetActive(false);
            }
            string[] fasten={"Tighten door hinge screws","Secure accessible awning fastener","Tighten window catch","Secure cabinet restraint"};
            Vector3[] fastenAt={new Vector3(1.8f,1.2f,-3.35f),new Vector3(3.55f,1.6f,-3.6f),new Vector3(-3.8f,1.4f,.05f),new Vector3(1.75f,1.7f,2.68f)};
            for(int n=0;n<4;n++)
            {
                var task=Task("fastener-"+n,fasten[n],fastenAt[n],"screwdriver","Hold the screwdriver against the accessible fitting.","Fitting secured. Major roof and structural repairs still require a qualified worker.",n==1);
                var loose=Box("Loose fitting",task.transform,Vector3.zero,new Vector3(.15f,.08f,.035f),metal,false);loose.transform.localRotation=Quaternion.Euler(0,0,25);task.before=loose;
                task.after=Box("Secured fitting",task.transform,Vector3.zero,new Vector3(.15f,.08f,.035f),metal,false);task.after.SetActive(false);
            }
            Vector3[] sweepAt={new Vector3(.8f,.07f,-1.2f),new Vector3(-.4f,.07f,.8f),new Vector3(2.4f,.07f,-2.35f),new Vector3(1.5f,.07f,1.9f)};
            for(int n=0;n<sweepAt.Length;n++){var t=Task("sweep-"+n,"Sweep dry floor area "+(n+1),sweepAt[n],"house-broom","Use the broom on the loose dry debris.","Loose debris collected. This floor section is clear.");Pile(t,n+10);}
            for(int n=0;n<2;n++){var t=Task("drain-clean-"+n,"Clear accessible drain grate "+(n+1),new Vector3(n==0?-3.8f:3.7f,-.06f,-4.8f),"house-brush","Use the brush to remove dry leaves from the surface grate.","Surface grate cleared. Underground drainage capacity is unchanged.",true);Pile(t,n+20);Box("Grate",t.transform,new Vector3(0,-.02f,0),new Vector3(.5f,.025f,.4f),metal,false);}
            var waste=Task("bag-refuse","Bag loose household refuse",new Vector3(-2.8f,.08f,-2.9f),"house-wastebags","Hold the waste bags and collect loose dry refuse.","Dry refuse contained away from drains.");Pile(waste,30);waste.after=Box("Contained refuse",waste.transform,new Vector3(0,.14f,0),new Vector3(.3f,.28f,.3f),metal);waste.after.SetActive(false);
            var path=Task("clear-exit","Clear loose objects from the exit",new Vector3(2.45f,.07f,-2.6f),"house-gloves","Use gloves to shift the dry loose clutter away from the exit.","The exit is clear and can be used from inside.");Pile(path,31);
            string[] objects={"Move the loose porch bucket under cover","Bring the spare footwear indoors","Store the cleanup box above the floor"};
            string[] objectIds={"outdoor-0","outdoor-1","drybox"};
            for(int n=0;n<3;n++)
            {
                var t=Task("shelter-object-"+n,objects[n],new Vector3(-2.5f+n*.42f,.8f,2.55f),objectIds[n],"Carry this object to the sheltered storage spot.","Loose item secured in sheltered storage.",false,true);
                var source=prep.items.First(i=>i.id==objectIds[n]);var visible=Object.Instantiate(source.gameObject,t.transform);foreach(var m in visible.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(m);foreach(var b in visible.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(b);visible.transform.localPosition=Vector3.zero;visible.transform.localRotation=Quaternion.identity;t.after=visible;visible.SetActive(false);
            }
            var inspect=Task("report-roof","Report visible roof damage from ground level",new Vector3(-2.6f,1.3f,-4),"phone","Use the phone from ground level to report the visible roof defect.","Roof defect reported for qualified repair. No roof strength increase is credited for reporting alone.",true);
            Box("Visible inspection target",inspect.transform,new Vector3(0,.8f,0),new Vector3(.3f,.08f,.12f),wood,false);
            Task("emergency-contact","Confirm household emergency contact",new Vector3(1.15f,1.3f,2.5f),"phone","Use the phone to confirm the household contact and meeting plan.","Household contact and meeting plan recorded. Follow actual local authority instructions.");
            Task("document-high","Move sealed documents to temporary high storage",new Vector3(-3.1f,1.45f,2.6f),"documents","Hold the document pouch at the high storage position.","High storage location checked. Remember to collect the pouch for the bag.");
            // Six residents inside the three existing furnished houses; no replacement neighborhood.
            var actor=Object.FindObjectsByType<SibucaoActor>(FindObjectsSortMode.None).First(a=>a.animator&&a.animator.isHuman&&a.idle);
            for(int h=1;h<game.homes.Length;h++)for(int n=0;n<2;n++)
            {
                var house=game.homes[h];var npc=Object.Instantiate(actor.gameObject,content.transform);npc.name="House "+(h+1)+" resident "+(n+1);
                var casting=npc.GetComponent<SibucaoActor>();var animator=casting.animator;var idle=casting.idle;
                foreach(var m in npc.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(m);
                foreach(var c in npc.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                foreach(var rb in npc.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(rb);
                npc.transform.SetPositionAndRotation(house.transform.TransformPoint(n==0?new Vector3(2.9f,.07f,-.28f):new Vector3(.1f,.07f,.6f)),house.transform.rotation);
                var info=npc.AddComponent<PreparednessItem>();info.id="neighbor-"+h+"-"+n;info.kind=PrepKind.Neighbor;info.portable=false;info.displayName="Warn household "+(h+1)+", resident "+(n+1);
                var collider=npc.AddComponent<CapsuleCollider>();collider.height=n==0?1.2f:1.7f;collider.radius=.25f;collider.center=Vector3.up*(collider.height*.5f);
                var neighbor=npc.AddComponent<PreparednessNeighbor>();neighbor.animator=animator;neighbor.idle=idle;neighbor.seated=n==0;neighbor.sweeping=n==1;neighbor.residentName="Neighbor in "+house.locationName;neighbor.activity=n==0?"Sitting at the dining table":"Sweeping the room";
                neighbor.need=n==0?"Please let us know when the local evacuation instruction changes.":"I will finish clearing the exit and remind the others to gather their supplies.";
                if(n==1){var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var b=Object.Instantiate(sourceBroom.gameObject,hand);b.transform.localPosition=new Vector3(0,-.75f,0);b.transform.localRotation=Quaternion.identity;foreach(var c in b.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);neighbor.broom=b.transform;}
            }
            prep.items=content.GetComponentsInChildren<PreparednessItem>(true).Concat(doors).Distinct().ToArray();
            foreach(var i in prep.items)i.ApplyVisual();
            prep.player.transform.SetPositionAndRotation(home.standingPoint.position,home.standingPoint.rotation);
            content.SetActive(false);EditorUtility.SetDirty(game);EditorUtility.SetDirty(bridge);EditorUtility.SetDirty(prep);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            return "Integrated actual first home at "+home.transform.position+"; "+prep.items.Count(i=>i.kind==PrepKind.HouseholdTask)+" new household tasks; six residents in three existing homes; "+doors.Count+" operable home doors.";
        }
    }
}

