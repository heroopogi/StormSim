using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;
using Object=UnityEngine.Object;

namespace StormSim.Preparedness.Editor
{
    public static class PosterCompletionInstaller
    {
        public static string Install()
        {
            if(Application.isPlaying)throw new Exception("Exit play mode before installing.");
            var s=Object.FindFirstObjectByType<PreparednessSession>(FindObjectsInactive.Include);
            if(!s||s.gameObject.scene.name!="SIBUCAO_RAIN")throw new Exception("Open SIBUCAO_RAIN.");
            Directory.CreateDirectory("output/poster-completion");if(!File.Exists("output/poster-completion/before.unity"))EditorSceneManager.SaveScene(s.gameObject.scene,"output/poster-completion/before.unity",true);
            if(!s.GetComponent<PreparationIntro>())s.gameObject.AddComponent<PreparationIntro>();
            const string folder="Assets/StormSim/Preparedness/Art/Poster";Directory.CreateDirectory(folder);
            Material Mat(string name,Color color){string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};AssetDatabase.CreateAsset(m,path);}return m;}
            var cloth=Mat("Dry blue clothing",new Color(.2f,.4f,.65f));var tan=Mat("Blanket fabric",new Color(.65f,.48f,.3f));var green=Mat("Rubber boots",new Color(.1f,.24f,.17f));var cream=Mat("Candles",new Color(.9f,.85f,.64f));var red=Mat("Waterproof match case",new Color(.6f,.15f,.12f));
            void Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material mat){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;}
            PreparednessItem Add(string id,string title,PosterSupplyType type,Vector3 pos,Vector3 size,float mass,Material mat,string instruction)
            {
                var item=s.items.FirstOrDefault(i=>i.id==id);
                if(!item){var go=new GameObject(title);go.transform.SetParent(s.transform,true);go.transform.position=pos;item=go.AddComponent<PreparednessItem>();item.id=id;item.displayName=title;item.kind=PrepKind.Decoration;item.mass=mass;item.portable=true;go.AddComponent<Rigidbody>().isKinematic=true;
                    Shape(go.transform,title,PrimitiveType.Cube,Vector3.zero,size,mat);
                    var physical=go.AddComponent<PhysicalGrabbable>();physical.persistentId="prep-"+id;physical.kind=PhysicalItemKind.Supply;physical.inventoryTag=id;physical.routinePickup=true;
                    s.items=s.items.Concat(new[]{item}).ToArray();}
                var supply=item.GetComponent<PosterSupply>();if(!supply)supply=item.gameObject.AddComponent<PosterSupply>();supply.supply=type;supply.instruction=instruction;return item;
            }
            var clothes=Add("poster-clothes","Dry clothes in waterproof pouch",PosterSupplyType.DryClothes,new Vector3(-99.7f,.57f,1262.95f),new Vector3(.36f,.12f,.22f),.6f,cloth,"Inspect the dry change of clothes and seal its waterproof pouch.");
            Add("poster-blanket","Rolled emergency blanket",PosterSupplyType.Blanket,new Vector3(-99.15f,.57f,1262.95f),new Vector3(.4f,.14f,.23f),.5f,tan,"Check that the blanket is dry and fasten its protective wrap.");
            Add("poster-cash","Emergency cash pouch",PosterSupplyType.EmergencyCash,new Vector3(-98.65f,.55f,1262.95f),new Vector3(.22f,.08f,.16f),.05f,green,"Reserve P100 from the household budget and seal the emergency cash pouch.");
            var candles=Add("poster-candles","Candles in protective case",PosterSupplyType.Candles,new Vector3(-98.2f,.59f,1262.95f),new Vector3(.24f,.1f,.16f),.2f,cream,"Inspect and protect the candle case. Use the flashlight first; keep flames away from flammable materials and never unattended.");
            Add("poster-matches","Matches in waterproof case",PosterSupplyType.Matches,new Vector3(-97.75f,.55f,1262.95f),new Vector3(.18f,.06f,.12f),.05f,red,"Check and seal the matches. Keep dry and away from children. No flame is needed for this preparation task.");
            var boots=Add("poster-boots","Pair of rubber boots",PosterSupplyType.Boots,new Vector3(-93.8f,-.99f,1258.1f),new Vector3(.12f,.38f,.16f),.9f,green,"Check the boots, then put them on. Boots do not make floodwater safe.");
            if(boots.transform.childCount==1){Shape(boots.transform,"Second boot",PrimitiveType.Cube,new Vector3(.19f,0,0),new Vector3(.12f,.38f,.16f),green);foreach(float x in new[]{0f,.19f})Shape(boots.transform,"Boot toe",PrimitiveType.Cube,new Vector3(x,-.14f,-.07f),new Vector3(.13f,.1f,.28f),green);}
            foreach(var id in new[]{"raincoat","whistle"}){var i=s.items.First(x=>x.id==id);Add(id,i.displayName,id=="whistle"?PosterSupplyType.Whistle:PosterSupplyType.Raincoat,i.transform.position,Vector3.one*.1f,i.mass,cloth,id=="whistle"?"Test the whistle with three short sounds; keep it accessible for signaling.":"Inspect the raincoat and fold it into its protective pouch.");}
            if(!s.transform.Find("Poster essentials shelf")){var shelf=new GameObject("Poster essentials shelf");shelf.transform.SetParent(s.transform,true);shelf.transform.position=new Vector3(-98.7f,.46f,1262.95f);Shape(shelf.transform,"Shelf",PrimitiveType.Cube,Vector3.zero,new Vector3(2.7f,.08f,.38f),tan);}
            // Keep the preparation supplies clear of the television and its advisory text.
            var rack=s.transform.Find("Poster essentials shelf");rack.position=new Vector3(-100.48f,.3f,1257.7f);rack.rotation=Quaternion.Euler(0,90,0);rack.GetChild(0).localScale=new Vector3(1.5f,.08f,.4f);
            if(!rack.Find("Lower shelf"))Shape(rack,"Lower shelf",PrimitiveType.Cube,new Vector3(.25f,-.65f,0),new Vector3(1.05f,.08f,.4f),tan);
            var shelfIds=new[]{"poster-clothes","poster-blanket","poster-cash","poster-candles","poster-matches"};
            for(int n=0;n<shelfIds.Length;n++){var item=s.items.First(i=>i.id==shelfIds[n]);item.transform.position=new Vector3(-100.4f,n<2?-.22f:.43f,n<2?1257.2f+n*.5f:n==2?1258.2f:1257.2f+(n-3)*.5f);item.transform.rotation=Quaternion.Euler(0,90,0);item.SynchronizePose();}
            foreach(var food in s.items.Where(i=>i.kind==PrepKind.Food))food.displayName="Ready-to-eat household-day food set (4 people)";
            EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(s.gameObject.scene);
            return "Opening title installed; eight poster supplies now interactive. Existing quantities: "+s.items.Where(i=>i.kind==PrepKind.Water&&i.id!="travel-water").Sum(i=>i.litres)+" L, "+s.items.Count(i=>i.kind==PrepKind.Food)+" household-day food sets.";
        }
    }
}
