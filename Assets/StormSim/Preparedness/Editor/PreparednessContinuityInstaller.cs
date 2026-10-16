using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessContinuityInstaller
    {
        static PreparednessSession s;
        static Material metal,wood,dark,cream;
        static T Component<T>(GameObject g)where T:Component=>g.GetComponent<T>()??g.AddComponent<T>();
        static Material Material(string name,Color color)
        {
            string path="Assets/StormSim/Preparedness/Art/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,path);}return m;
        }
        static GameObject Shape(string name,Transform parent,Vector3 local,Vector3 scale,Material m,PrimitiveType type=PrimitiveType.Cube)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=local;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;return g;
        }
        static PreparednessItem Item(string id,string title,PrepKind kind,Vector3 world,bool portable=false)
        {
            var found=s.GetComponentsInChildren<PreparednessItem>(true).FirstOrDefault(i=>i.id==id);if(found)return found;
            var g=new GameObject(title);g.transform.SetParent(s.transform,true);g.transform.position=world;
            var i=g.AddComponent<PreparednessItem>();i.id=id;i.displayName=title;i.kind=kind;i.portable=portable;
            if(portable){var rb=g.AddComponent<Rigidbody>();rb.isKinematic=true;}return i;
        }
        static PreparednessItem Task(string id,string title,Vector3 p,string tool,bool outside,Vector3 scale)
        {
            var i=Item(id,title,PrepKind.HouseholdTask,p);var t=Component<PreparednessHouseTask>(i.gameObject);
            t.requiredItem=tool;t.instruction=title+" with "+tool;t.result=title+" completed.";t.workSeconds=4;t.exterior=outside;
            if(!t.before){t.before=Shape("Before work",i.transform,Vector3.zero,scale,dark);t.after=Shape("Finished work",i.transform,Vector3.zero,scale,cream);}
            t.ApplyState();return i;
        }
        static void Activity(string id,string title,string effect,PreparednessActivity.Check check,params string[] requirements)
        {
            var a=s.GetComponentsInChildren<PreparednessActivity>(true).FirstOrDefault(x=>x.id==id);
            if(!a){var g=new GameObject("Optional activity - "+title);g.transform.SetParent(s.transform,false);a=g.AddComponent<PreparednessActivity>();}
            a.id=id;a.title=title;a.aftermathKey=effect;a.check=check;a.requiredIds=requirements;
        }
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode before installing.");
            var bridge=Object.FindFirstObjectByType<SibucaoPreparationBridge>(FindObjectsInactive.Include);s=bridge.preparation;
            metal=Material("Continuity galvanized metal",new Color(.42f,.49f,.5f));wood=Material("Continuity tool handle",new Color(.35f,.19f,.09f));dark=Material("Continuity dark rubber",new Color(.055f,.10f,.11f));cream=Material("Continuity plaster and bin",new Color(.7f,.7f,.56f));
            var continuity=Component<PreparationContinuity>(bridge.gameObject);Component<StormSurvivalVitals>(bridge.gameObject);
            continuity.legacyBags=bridge.firstHome.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Emergency bag"||t.GetComponent<PhysicalGrabbable>()&&t.GetComponent<PhysicalGrabbable>().kind==PhysicalItemKind.Bag).Select(t=>t.gameObject).ToArray();
            // The pictured decorative rice pail was jammed into the closet. Replace its
            // two meshes with a working bin at a clear position on the same floor.
            foreach(var t in bridge.firstHome.GetComponentsInChildren<Transform>(true))if(t.name=="Rice storage pail"||t.name=="Rice pail lid")t.gameObject.SetActive(false);
            var bin=Item("indoor-waste-bin","Lidded waste bin - open, dispose, close",PrepKind.BinZone,new Vector3(-93.55f,-1.20f,1261.95f));
            if(!bin.mechanism)
            {
                Shape("Bin base",bin.transform,new Vector3(0,.025f,0),new Vector3(.4f,.025f,.4f),cream,PrimitiveType.Cylinder);
                for(int n=0;n<20;n++){float a=n*Mathf.PI/10;var wall=Shape("Bin wall",bin.transform,new Vector3(Mathf.Sin(a)*.2f,.28f,Mathf.Cos(a)*.2f),new Vector3(.066f,.52f,.028f),cream);wall.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);}
                var hinge=new GameObject("Hinged waste lid").transform;hinge.SetParent(bin.transform,false);hinge.localPosition=new Vector3(0,.55f,.21f);
                Shape("Round lid",hinge,new Vector3(0,0,-.21f),new Vector3(.44f,.02f,.44f),dark,PrimitiveType.Cylinder);
                Shape("Lid handle",hinge,new Vector3(0,.045f,-.25f),new Vector3(.12f,.04f,.04f),metal);
                bin.mechanism=hinge;bin.openEuler=new Vector3(-105,0,0);bin.closed=true;
            }
            var wasteBin=Component<PreparednessWasteBin>(bin.gameObject);
            if(!wasteBin.contents){wasteBin.contents=Shape("Contained refuse",bin.transform,new Vector3(0,.26f,0),new Vector3(.3f,.37f,.3f),dark,PrimitiveType.Sphere);wasteBin.contents.SetActive(false);}
            bin.ApplyVisual();
            var putty=Item("wall-putty","Wall patch compound and scraper",PrepKind.Tool,new Vector3(-98.55f,-.25f,1262.65f),true);
            if(putty.transform.childCount==0){Shape("Patch compound tub",putty.transform,Vector3.zero,new Vector3(.13f,.075f,.13f),cream,PrimitiveType.Cylinder);Shape("Scraper",putty.transform,new Vector3(.1f,.02f,0),new Vector3(.08f,.02f,.16f),metal);}
            var crack=Task("patch-wall","Seal the small plaster crack",new Vector3(-100.75f,-.05f,1262.65f),"wall-putty",false,new Vector3(.02f,.42f,.045f));
            crack.GetComponent<PreparednessHouseTask>().instruction="Fill the small surface crack using patch compound. Hold E";
            var ladder=Item("porch-ladder","Stored folding ladder beside the house",PrepKind.Tool,new Vector3(-101.8f,-2.02f,1256.7f),true);ladder.mass=2;
            if(ladder.transform.childCount==0)
            {
                foreach(int sign in new[]{-1,1})Shape("Ladder rail",ladder.transform,new Vector3(sign*.22f,.8f,0),new Vector3(.045f,1.6f,.06f),metal);
                for(int n=0;n<5;n++)Shape("Ladder rung",ladder.transform,new Vector3(0,.16f+n*.3f,0),new Vector3(.46f,.05f,.07f),metal);
            }
            var setup=Task("place-ladder","Place and secure the folding ladder",new Vector3(-101.9f,-1.98f,1258.2f),"porch-ladder",true,new Vector3(.65f,.04f,.65f));
            setup.GetComponent<PreparednessHouseTask>().consumeMaterial=true;
            var access=Item("ladder-access","Secured ladder - climb or descend",PrepKind.Decoration,new Vector3(-101.9f,-2.01f,1258.2f));
            if(access.transform.childCount==0)
            {
                foreach(int sign in new[]{-1,1})Shape("Secured ladder rail",access.transform,new Vector3(sign*.27f,.72f,0),new Vector3(.06f,1.44f,.06f),metal);
                for(int n=0;n<5;n++)Shape("Secured ladder tread",access.transform,new Vector3(0,.1f+n*.28f,0),new Vector3(.56f,.055f,.13f),metal);
                Shape("Working platform",access.transform,new Vector3(0,1.46f,.28f),new Vector3(.85f,.06f,.8f),metal);
            }
            var climb=Component<PreparednessLadder>(access.gameObject);climb.setup=setup;climb.basePosition=new Vector3(-102.5f,-1.94f,1258.2f);climb.workingPosition=new Vector3(-101.9f,.2f,1258.2f);
            setup.GetComponent<PreparednessHouseTask>().after=access.gameObject;setup.ApplyVisual();
            var roof=Task("repair-roof-panel","Secure the loose roof edge panel",new Vector3(-101.26f,1.9f,1258.2f),"screwdriver",true,new Vector3(.42f,.04f,.6f));
            var roofTask=roof.GetComponent<PreparednessHouseTask>();roofTask.prerequisite=setup;roofTask.requiresLadderHeight=true;roofTask.instruction="Tighten the panel fasteners from the supported ladder platform";
            ladder.transform.position=new Vector3(-101.8f,-2.02f,1256.7f);setup.transform.position=new Vector3(-101.9f,-1.98f,1258.2f);access.transform.position=new Vector3(-101.9f,-2.01f,1258.2f);roof.transform.position=new Vector3(-101.26f,1.9f,1258.2f);roof.transform.rotation=Quaternion.Euler(0,0,10);
            foreach(var t in access.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Secured ladder rail"){var p=t.localPosition;p.y=1.05f;t.localPosition=p;t.localScale=new Vector3(.06f,2.1f,.06f);}
                if(t.name=="Working platform"){t.localPosition=new Vector3(0,2.12f,0);}
            }
            if(!access.transform.Find("Upper ladder tread")){Shape("Upper ladder tread",access.transform,new Vector3(0,1.65f,0),new Vector3(.56f,.055f,.13f),metal);Shape("Upper ladder tread 2",access.transform,new Vector3(0,1.94f,0),new Vector3(.56f,.055f,.13f),metal);}
            // Two real jalousie banks, each with its own operable handle.
            foreach(int side in new[]{-1,1})
            {
                var window=side==1?s.items.First(i=>i.id=="window"):Item("window-west","West window - close and latch",PrepKind.Window,new Vector3(-100.76f,-.05f,1260));
                if(side==-1)Shape("Window latch",window.transform,Vector3.zero,new Vector3(.06f,.16f,.09f),metal);
                var slats=Component<PreparednessWindowSlats>(window.gameObject);slats.slats=bridge.firstHome.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Window jalousie slat"&&(side<0?t.position.x<-97:t.position.x>-97)).ToArray();
            }
            Activity("optional-drains","Clear both house drain grates","drains_clear",PreparednessActivity.Check.Tasks,"drain-clean-0","drain-clean-1");
            Activity("optional-clean","Sweep and tidy all four floor areas","floors_clean",PreparednessActivity.Check.Tasks,"sweep-0","sweep-1","sweep-2","sweep-3");
            Activity("optional-documents","Seal documents in the waterproof pouch and pack it","documents_protected",PreparednessActivity.Check.Documents);
            Activity("optional-crack","Patch the small wall crack","surface_crack_patched",PreparednessActivity.Check.Tasks,"patch-wall");
            Activity("optional-windows","Close and latch both window banks","windows_latched",PreparednessActivity.Check.Windows);
            Activity("optional-roof","Fetch and secure the ladder, then repair the porch panel","porch_panel_secured",PreparednessActivity.Check.Tasks,"place-ladder","repair-roof-panel");
            Activity("optional-raise","Disconnect and raise electronics; elevate valuables","belongings_elevated",PreparednessActivity.Check.RaisedItems);
            Activity("optional-loose","Bring loose outdoor belongings inside","loose_objects_sheltered",PreparednessActivity.Check.IndoorObjects);
            Activity("optional-restraint","Tighten the cabinet restraint","cabinet_restrained",PreparednessActivity.Check.Tasks,"fastener-3");
            Activity("optional-waste","Dispose of household refuse and close the waste bin","refuse_contained",PreparednessActivity.Check.Waste);
            // Neighborhood residents and original house doors live outside the session
            // transform. Retain them when registering newly authored household props.
            s.items=s.items.Concat(bridge.content.GetComponentsInChildren<PreparednessItem>(true)).Concat(bridge.houseDoors).Where(i=>i).Distinct().ToArray();
            foreach(var item in s.items.Where(i=>i.portable&&i.Body&&i.kind!=PrepKind.Litter))
            {
                // Bulky furniture/containers remain world objects; packable supplies use
                // the inventory, while the same bag retains its own physical identity.
                if(item.kind==PrepKind.Tool||item.kind==PrepKind.Outdoor||item.kind==PrepKind.Electronics||item.kind==PrepKind.Valuables)continue;
                var p=Component<PhysicalGrabbable>(item.gameObject);p.persistentId="prep:"+item.id;p.kind=item.kind==PrepKind.Bag?PhysicalItemKind.Bag:item.kind==PrepKind.FirstAid?PhysicalItemKind.FirstAid:PhysicalItemKind.Supply;p.supplyBit=-1;
                p.inventoryTag=item.kind==PrepKind.FirstAid?"first_aid":item.kind.ToString().ToLowerInvariant();item.SetPhysics(false);
                EditorUtility.SetDirty(p);
            }
            EditorUtility.SetDirty(s);EditorUtility.SetDirty(continuity);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(s.gameObject.scene);
            return "Installed shared bag inventory, 10 optional activity records, working bin, ladder/roof/wall/window interactions and survival bars.";
        }
    }
}
