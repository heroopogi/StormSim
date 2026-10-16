using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PreparationChapterInstaller
    {
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Exit play mode first.");
            var s=Object.FindFirstObjectByType<PreparednessSession>(FindObjectsInactive.Include);
            if(!s||s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("Open SIBUCAO_RAIN.");
            if(s.chapter)return "Chapter already installed.";
            Directory.CreateDirectory("output/preparation-expansion");
            EditorSceneManager.SaveScene(s.gameObject.scene,"output/preparation-expansion/before-chapter.unity",true);
            var chapter=s.gameObject.AddComponent<PreparationChapter>();s.chapter=chapter;chapter.session=s;
            s.evacuationAfterSeconds=1800;
            var root=new GameObject("Day before - sari-sari store");root.transform.SetParent(s.transform.parent,false);
            root.transform.position=new Vector3(-92,-1.65f,1255.2f);
            var source=s.items.First(i=>i.kind==PrepKind.Neighbor&&i.GetComponent<PreparednessNeighbor>().animator);
            PreparednessItem Person(string id,string name,Vector3 position,Transform parent)
            {
                var go=Object.Instantiate(source.gameObject,parent);go.name=name;go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,180,0);
                var item=go.GetComponent<PreparednessItem>();item.id=id;item.displayName=name;item.prepared=false;item.cells=0;
                var npc=go.GetComponent<PreparednessNeighbor>();npc.residentName=name;npc.seated=false;npc.sweeping=false;if(npc.broom)npc.broom.gameObject.SetActive(false);
                s.items=s.items.Concat(new[]{item}).ToArray();return item;
            }
            chapter.family=Person("chapter-family","Family member",new Vector3(-94.5f,-1.18f,1261.2f),s.transform);
            chapter.shopkeeper=Person("chapter-shopkeeper","Aling Rosa - shopkeeper",new Vector3(-92,-1.65f,1255.65f),root.transform);
            chapter.barangay=Person("chapter-barangay","Barangay preparation worker",new Vector3(-96,-1.7f,1252.6f),root.transform);
            const string folder="Assets/StormSim/Preparedness/Art/Chapter";Directory.CreateDirectory(folder);
            Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
            var timber=Mat("Store timber",new Color(.31f,.19f,.10f));var teal=Mat("Store teal",new Color(.12f,.42f,.39f));var cream=Mat("Store cream",new Color(.95f,.86f,.62f));
            GameObject Box(string name,Vector3 at,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root.transform,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
            Box("Store counter",new Vector3(0,.75f,-.15f),new Vector3(1.6f,.12f,.55f),timber);
            Box("Counter front",new Vector3(0,.4f,-.4f),new Vector3(1.6f,.7f,.08f),teal);
            foreach(float x in new[]{-.72f,.72f})Box("Awning post",new Vector3(x,1.15f,.45f),new Vector3(.08f,2.3f,.08f),timber);
            Box("Teal awning",new Vector3(0,2.3f,0),new Vector3(1.85f,.08f,1.5f),teal);
            Box("Store signboard",new Vector3(0,1.98f,-.63f),new Vector3(1.65f,.42f,.06f),cream);
            var label=new GameObject("Sari-sari sign").AddComponent<TextMesh>();label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,1.98f,-.67f);label.transform.localRotation=Quaternion.identity;label.text="ROSA'S SARI-SARI\nFOOD  WATER  BATTERIES";label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=.022f;label.fontSize=48;label.color=new Color(.07f,.2f,.17f);
            string[] ids={"food-2","water-9","cells-2"};int[] prices={120,40,60};string[] descriptions={"Ready-to-eat household food pack with easy-open cans. No cooking needed.","Sealed drinking water. Store household water and pack portable water separately.","Spare batteries matching your household flashlight and radio. Fit and test equipment at home."};
            chapter.products=ids.Select((id,i)=>{var item=s.items.First(x=>x.id==id);item.storageDoor=null;item.storedIn="store";item.transform.position=root.transform.position+new Vector3(-.5f+i*.5f,.95f,-.15f);item.SetPhysics(false);item.SynchronizePose();return new PreparationProduct{item=item,price=prices[i],description=descriptions[i],shelfPosition=item.transform.position};}).ToArray();
            // A single shared resident ruleset; retain legacy fields only for save compatibility.
            var manager=Object.FindFirstObjectByType<DecisionTreeManager>(FindObjectsInactive.Include);if(manager)manager.playerRole="Resident";
            EditorUtility.SetDirty(s);EditorUtility.SetDirty(chapter);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);AssetDatabase.SaveAssets();
            return "Installed day-before store, three conversation partners and three purchasable supplies.";
        }
    }
}
