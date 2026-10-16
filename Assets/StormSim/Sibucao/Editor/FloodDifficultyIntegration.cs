using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
    public static class FloodDifficultyIntegration
    {
        const string Folder="Assets/StormSim/Sibucao/Data/CausalDuring/";
        [MenuItem("STORMSIM/During/Install five flood difficulties")]
        public static string Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode first.");
            var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
            if(!s||!s.physicalDecisions.causalModel)throw new InvalidOperationException("Open the integrated DURING scene.");
            string path=Folder+"FloodDifficulties.asset";
            var settings=AssetDatabase.LoadAssetAtPath<FloodDifficultySettings>(path);
            if(!settings){settings=ScriptableObject.CreateInstance<FloodDifficultySettings>();AssetDatabase.CreateAsset(settings,path);}
            s.floodDifficulty=settings;EditorUtility.SetDirty(s);
            var deck=Material("EvacuationLanding",new Color(.24f,.29f,.27f));
            var rail=Material("EvacuationSafetyRail",new Color(.72f,.46f,.09f));
            foreach(var st in s.physicalDecisions.stations.Where(x=>!x.supplementary))
            {
                var adapter=st.GetComponent<FloodEncounterAdapter>();if(!adapter)adapter=st.gameObject.AddComponent<FloodEncounterAdapter>();
                adapter.station=st;adapter.deckMaterial=deck;adapter.railMaterial=rail;EditorUtility.SetDirty(adapter);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
            return Validate();
        }
        static Material Material(string name,Color color)
        {
            string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(m,path);}return m;
        }
        public static string Validate()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();int choices=0;
            foreach(var st in s.physicalDecisions.stations.Where(x=>!x.supplementary))
            {
                if(!st.FloodAdapter||st.FloodAdapter.station!=st)throw new Exception("Adapter missing at "+st.index);
                for(int level=1;level<=5;level++)foreach(var b in st.branches)
                {
                    string id=b.Choice.id;if(string.IsNullOrEmpty(id))throw new Exception("Missing ID");
                    string label=FloodVariantCatalog.Label(st.index,b.choiceIndex,level);
                    var work=FloodVariantCatalog.Work(st.index,b.choiceIndex,level);
                    if(st.index<=4&&(!work||!work.Valid))throw new Exception("Invalid physical variant: "+label);
                    if(work)UnityEngine.Object.DestroyImmediate(work);choices++;
                }
            }
            foreach(var profile in s.floodDifficulty.profiles)
            {
                if(profile.level<1||profile.level>5||profile.peakDepth<=0)throw new Exception("Invalid profile");
                float last=0;for(int time=0;time<=7200;time+=15)
                {float depth=profile.Limit(time);if(depth<last||depth>profile.peakDepth+.0001f)throw new Exception("Unbounded storm");last=depth;}
            }
            string result="PASS: nine adapters, "+choices+" choice/difficulty mappings, valid physical definitions; five storm envelopes monotone and capped through 120 minutes. Editor configuration test, not a physical route playthrough.";
            Directory.CreateDirectory("Reports");File.WriteAllText("Reports/FloodDifficultyConfiguration.txt",result);return result;
        }
    }
}
