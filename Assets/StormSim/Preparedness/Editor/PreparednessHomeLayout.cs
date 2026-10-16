using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    // Upgrade only the isolated preparedness scene. Original home geometry stays in place.
    public static class PreparednessHomeLayout
    {
        static PreparednessSession s;
        static Material wood;
        static GameObject Panel(string name,Transform parent,Vector3 position,Vector3 size)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=position;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=wood;return g;
        }
        static PreparednessItem Door(string id,Transform parent,Vector3 hinge,Vector3 panel,Vector3 size,Vector3 open)
        {
            var g=new GameObject(id);g.transform.SetParent(parent,false);g.transform.localPosition=hinge;
            var i=g.AddComponent<PreparednessItem>();i.id=id;i.kind=PrepKind.StorageDoor;i.portable=false;i.closed=true;i.mechanism=g.transform;i.openEuler=open;i.displayName=id=="closet-door"?"Household closet — open/close":"Storage chest — lift/close lid";
            Panel("Wooden door or lid",g.transform,panel,size);
            Panel("Handle",g.transform,panel+new Vector3(0,.03f,-.04f),new Vector3(.16f,.045f,.06f));return i;
        }
        public static string Apply()
        {
            if(Application.isPlaying)throw new Exception("Stop play first.");
            s=Object.FindFirstObjectByType<PreparednessSession>();
            if(!s||s.gameObject.scene.path!=PreparednessBuilder.ScenePath)throw new Exception("Open the isolated preparation scene.");
            if(s.items.Any(i=>i.id=="table-zone"))throw new Exception("Home layout already applied; preserve subsequent edits.");
            wood=AssetDatabase.LoadAssetAtPath<Material>(PreparednessBuilder.Folder+"/Art/Preparation timber.mat");
            var home=s.transform.Find("PreparednessHomeShell");
            s.homeStart=home.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Stand up beside daybed");
            s.player.transform.SetPositionAndRotation(s.homeStart.position,s.homeStart.rotation);
            foreach(Transform t in s.transform.Cast<Transform>().ToArray())
                if(t.name.StartsWith("Preparation sideboard")||t.name.StartsWith("Raised household supply shelf leg")||t.name=="Raised household supply shelf")Object.DestroyImmediate(t.gameObject);
            var closet=new GameObject("Household closet").transform;closet.SetParent(s.transform,false);closet.localPosition=new Vector3(1.05f,0,3.05f);
            Panel("Closet back",closet,new Vector3(0,1.1f,.34f),new Vector3(1.7f,2.2f,.06f));
            foreach(int side in new[]{-1,1})Panel("Closet side",closet,new Vector3(side*.85f,1.1f,0),new Vector3(.06f,2.2f,.7f));
            foreach(float y in new[]{.12f,.82f,1.52f,2.2f})Panel("Closet shelf",closet,new Vector3(0,y,0),new Vector3(1.7f,.05f,.7f));
            var door=Door("closet-door",closet,new Vector3(-.85f,0,-.38f),new Vector3(.85f,1.1f,0),new Vector3(1.68f,2.2f,.05f),new Vector3(0,105,0));
            var chest=new GameObject("Mixed household storage chest").transform;chest.SetParent(s.transform,false);chest.localPosition=new Vector3(.15f,0,-2.9f);
            Panel("Chest bottom",chest,new Vector3(0,.13f,0),new Vector3(1.35f,.07f,.7f));
            foreach(int sign in new[]{-1,1}){Panel("Chest end",chest,new Vector3(sign*.68f,.38f,0),new Vector3(.05f,.52f,.7f));Panel("Chest side",chest,new Vector3(0,.38f,sign*.36f),new Vector3(1.4f,.52f,.05f));}
            var lid=Door("chest-lid",chest,new Vector3(0,.65f,.36f),new Vector3(0,0,-.36f),new Vector3(1.43f,.05f,.76f),new Vector3(105,0,0));
            var rng=new System.Random(92726);
            PreparednessItem Item(string id)=>s.items.First(i=>i.id==id);
            void Place(string id,Vector3 position,PreparednessItem access=null,float yaw=0)
            {
                var i=Item(id);i.transform.rotation=Quaternion.Euler(0,yaw+(float)(rng.NextDouble()*24-12),0);
                var b=i.WorldBounds();i.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);
                i.storageDoor=access;i.storedIn="";i.gathered=false;i.SetPhysics(false);EditorUtility.SetDirty(i);
            }
            Place("bag",new Vector3(.59f,.16f,3.03f),door,18);
            Place("hygiene",new Vector3(1.5f,.16f,3.06f),door,-10);
            Place("raincoat",new Vector3(.56f,.86f,3.04f),door,8);
            Place("first-aid",new Vector3(1.42f,.86f,3.05f),door,-8);
            Place("documents",new Vector3(.61f,1.56f,3.07f),door,12);
            Place("medicine",new Vector3(1.38f,1.56f,3.04f),door,-15);
            Place("radio",new Vector3(-.22f,.18f,-2.9f),lid,15);
            Place("flashlight",new Vector3(.17f,.18f,-2.78f),lid,-8);
            Place("powerbank",new Vector3(.5f,.18f,-2.99f),lid,28);
            Place("cells-0",new Vector3(.25f,.18f,-3.1f),lid,33);
            Place("cells-1",new Vector3(.49f,.18f,-2.72f),lid,-25);
            Place("cable",new Vector3(-.26f,.18f,-3.12f),lid,-18);
            // Ordinary household surfaces, not rows of supplies on the packing table.
            Place("phone",new Vector3(-.45f,.94f,2.52f),null,57);
            Place("cells-2",new Vector3(-1.57f,.94f,2.53f),null,-28);
            Place("whistle",new Vector3(-.98f,.94f,2.53f),null,35);
            Place("food-0",new Vector3(-3.4f,.91f,.7f),null,18);
            Place("food-1",new Vector3(-2.57f,.91f,.5f),null,-22);
            Place("food-2",new Vector3(-3.52f,.91f,1.27f),null,6);
            for(int n=0;n<10;n++)Place("water-"+n,new Vector3(-3.55f+(n%3)*.42f,.065f,.63f+(n/3)*.38f),null,(float)rng.NextDouble()*60-30);
            // Keep the existing dining table in its original location and initially clear.
            var table=home.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Dining table top");var top=table.GetComponent<Renderer>().bounds;
            var zone=new GameObject("Dining table — gather supplies here");zone.transform.SetParent(s.transform,false);zone.transform.position=new Vector3(top.center.x,top.max.y,top.center.z);
            var ti=zone.AddComponent<PreparednessItem>();ti.id="table-zone";ti.kind=PrepKind.TableZone;ti.portable=false;ti.displayName="Dining table — place held supply in a clear spot";
            var c=zone.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(top.size.x,.25f,top.size.z);c.center=Vector3.up*.125f;
            Item("charger").transform.position=new Vector3(-1.55f,.94f,3.04f);
            var pad=s.transform.Find("Charging pad symbol backing");if(pad)pad.position=new Vector3(-1.55f,.931f,3.04f);
            Item("supply-zone").transform.position=new Vector3(-3.05f,.93f,.65f);
            Item("supply-zone").displayName="Kitchen worktop — household water and food";
            Item("plan-primary").transform.position=new Vector3(2.05f,1.1f,3.13f);
            Item("plan-backup").transform.position=new Vector3(2.48f,1.1f,3.13f);
            s.statusBoard.transform.position=new Vector3(2.43f,2.12f,3.3f);s.statusBoard.characterSize=.011f;
            s.items=s.GetComponentsInChildren<PreparednessItem>(true);foreach(var i in s.items)i.ApplyVisual();
            EditorUtility.SetDirty(s);EditorSceneManager.SaveScene(s.gameObject.scene);
            return "Original home start and dining table retained; closed closet and chest added; supplies scattered; table initially empty.";
        }
    }
}

