using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crest;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoCausalIntegration
    {
        const string Folder="Assets/StormSim/Sibucao/Data/CausalDuring";
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static T Asset<T>(string name,Func<T> factory) where T:UnityEngine.Object
        {
            string path=Folder+"/"+name;var result=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!result){result=factory();AssetDatabase.CreateAsset(result,path);}return result;
        }
        static Material Input(string file,string shader)
        {
            var found=Shader.Find(shader);Require(found,"Shader missing: "+shader);
            return Asset(file+".mat",()=>new Material(found));
        }
        static MeshFilter InputObject<T>(string name,Transform parent,Material material,Mesh mesh=null) where T:Component
        {
            var child=parent.Find(name);var go=child?child.gameObject:new GameObject(name);
            go.SetActive(false);go.transform.SetParent(parent,false);
            var filter=go.GetComponent<MeshFilter>();if(!filter)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();if(!renderer)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            var input=go.GetComponent<T>();if(!input)input=go.AddComponent<T>();
            if(input is RegisterClipSurfaceInput){var so=new SerializedObject(input);so.FindProperty("_mode").enumValueIndex=0;so.ApplyModifiedPropertiesWithoutUndo();}
            if(input is RegisterFlowInput){var so=new SerializedObject(input);so.FindProperty("_followHorizontalMotion").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();}
            go.SetActive(true);return filter;
        }
        [MenuItem("STORMSIM/During/Install B1-B9 model and Crest")]
        public static string Install()
        {
            Require(!EditorApplication.isPlaying,"Stop play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Require(scene.path=="Assets/Scenes/SIBUCAO_RAIN.unity","Open the latest SIBUCAO_RAIN scene.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var session=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();Require(session,"Session missing.");
            var manager=session.physicalDecisions;Require(manager,"Physical decision manager missing.");
            manager.causalModel=Asset("DuringBranchModel.asset",()=>ScriptableObject.CreateInstance<SibucaoCausalModel>());
            manager.causalModel.ValidateRun(null);
            foreach(var st in manager.stations.Where(s=>!s.supplementary))Require(st.node&&st.node.choices.Length==SibucaoCausalModel.ChoiceCounts[st.index],"Unexpected choice mapping: "+st.name);
            EditorUtility.SetDirty(manager);
            var flood=session.GetComponent<FloodSimulationManager>();Require(flood&&flood.terrain&&flood.sea,"Flood grid or coast missing.");
            var existing=UnityEngine.Object.FindObjectsByType<OceanRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Require(existing.Length==0||existing.Length==1&&existing[0].name=="STORMSIM Crest Water","Another Crest ocean exists; keep its settings and resolve the duplicate first.");
            var go=existing.Length>0?existing[0].gameObject:new GameObject("STORMSIM Crest Water");go.SetActive(false);
            go.transform.position=new Vector3(0,flood.sea.transform.position.y,0);
            var ocean=go.GetComponent<OceanRenderer>();if(!ocean)ocean=go.AddComponent<OceanRenderer>();
            var water=Asset("DuringCrestWater.mat",()=>new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat")));
            water.SetColor("_Diffuse",new Color(.12f,.18f,.16f));water.SetColor("_DiffuseGrazing",new Color(.19f,.25f,.22f));
            water.SetFloat("_ClipSurface",1);water.EnableKeyword("_CLIPSURFACE_ON");water.SetFloat("_Flow",1);water.EnableKeyword("_FLOW_ON");
            water.SetFloat("_Foam",0);water.DisableKeyword("_FOAM_ON");water.SetFloat("_NormalsScale",12);water.SetFloat("_NormalsStrength",.5f);
            water.SetFloat("_ApplyNormalMapping",1);water.EnableKeyword("_APPLYNORMALMAPPING_ON");
            water.SetFloat("_Caustics",0);water.DisableKeyword("_CAUSTICS_ON");water.SetFloat("_Smoothness",.78f);water.SetFloat("_ReflectionBlur",2);water.SetVector("_DepthFogDensity",new Vector4(1.4f,1.1f,.9f,1));
            ocean.OceanMaterial=water;ocean.Viewpoint=session.player.view.transform;
            ocean._defaultClippingState=OceanRenderer.DefaultClippingState.EverythingClipped;
            ocean._simSettingsSeaFloorDepth=Asset("WaterHeightSettings.asset",()=>ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>());
            ocean._simSettingsAnimatedWaves=Asset("WaveQuerySettings.asset",()=>ScriptableObject.CreateInstance<SimSettingsAnimatedWaves>());
            ocean._simSettingsAnimatedWaves.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;
            var settings=new SerializedObject(ocean);
            settings.FindProperty("_createClipSurfaceData").boolValue=true;settings.FindProperty("_createSeaFloorDepthData").boolValue=true;
            settings.FindProperty("_createFlowSim").boolValue=true;settings.FindProperty("_createFoamSim").boolValue=false;
            settings.FindProperty("_lodDataResolution").intValue=256;settings.FindProperty("_lodCount").intValue=7;
            settings.FindProperty("_minScale").floatValue=8;settings.FindProperty("_maxScale").floatValue=64;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var height=Input("RunoffHeight","Crest/Inputs/Sea Floor Depth/STORMSIM simulation height");
            height.SetFloat("_FloodWaveHeight",flood.temporaryWaterMaterial.GetFloat("_WaveHeight"));height.SetFloat("_FloodWaveSpeed",flood.temporaryWaterMaterial.GetFloat("_WaveSpeed"));
            var coastal=Input("CoastalHeight","Crest/Inputs/Sea Floor Depth/STORMSIM simulation height");coastal.SetFloat("_SeaMode",1);
            // Coast first; terrain-connected runoff takes precedence at shared shoreline triangles.
            coastal.renderQueue=1980;height.renderQueue=2000;
            var bridge=session.GetComponent<SibucaoCrestBridge>();if(!bridge)bridge=session.gameObject.AddComponent<SibucaoCrestBridge>();bridge.flood=flood;bridge.ocean=ocean;
            bridge.floodHeight=InputObject<RegisterHeightInput>("Crest runoff heights",flood.transform,height);
            bridge.floodClip=InputObject<RegisterClipSurfaceInput>("Crest wet-cell mask",flood.transform,Input("WetCellMask","Crest/Inputs/Clip Surface/STORMSIM wet cells"));
            bridge.floodFlow=InputObject<RegisterFlowInput>("Crest runoff currents",flood.transform,Input("RunoffCurrent","Crest/Inputs/Flow/STORMSIM runoff current"));
            var seaMesh=flood.sea.GetComponent<MeshFilter>().sharedMesh;
            bridge.seaHeight=InputObject<RegisterHeightInput>("Crest coastal heights",flood.sea.transform,coastal,seaMesh);
            bridge.seaHeightRenderer=bridge.seaHeight.GetComponent<Renderer>();
            bridge.seaClip=InputObject<RegisterClipSurfaceInput>("Crest coastal boundary",flood.sea.transform,Input("CoastalMask","Crest/Inputs/Clip Surface/Include Area"),seaMesh);
            ConfigureCoastalTint(bridge);
            EditorUtility.SetDirty(bridge);EditorUtility.SetDirty(water);EditorUtility.SetDirty(height);EditorUtility.SetDirty(coastal);
            EditorUtility.SetDirty(ocean._simSettingsAnimatedWaves);EditorUtility.SetDirty(ocean);
            // Runtime activation gives the simulation time to supply its generated mesh.
            go.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return ValidateModel()+" Scene saved with Crest bridge and all core nodes connected.";
        }
        public static string ApplyCoastalTint()
        {
            Require(!EditorApplication.isPlaying,"Stop play mode before saving the sea appearance.");
            var bridge=UnityEngine.Object.FindFirstObjectByType<SibucaoCrestBridge>();Require(bridge&&bridge.ocean&&bridge.flood.sea,"Crest sea bridge missing.");
            ConfigureCoastalTint(bridge);
            AssetDatabase.SaveAssets();
            var scene=bridge.gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Saved dark bluish Crest tint on the traced sea mesh; inland water colour and flood simulation unchanged.";
        }
        static void ConfigureCoastalTint(SibucaoCrestBridge bridge)
        {
            var tint=Input("CoastalDarkBlue","Crest/Inputs/Albedo/STORMSIM coastal tint");
            // Partial tint retains Crest's moving normals/reflections; exact coastline mesh limits its extent.
            tint.SetColor("_Color",new Color(.018f,.065f,.12f,.72f));
            InputObject<RegisterAlbedoInput>("Crest dark blue sea",bridge.flood.sea.transform,tint,bridge.flood.sea.GetComponent<MeshFilter>().sharedMesh);
            var water=bridge.ocean.OceanMaterial;water.SetFloat("_Albedo",1);water.EnableKeyword("_ALBEDO_ON");
            var settings=new SerializedObject(bridge.ocean);settings.FindProperty("_createAlbedoData").boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tint);EditorUtility.SetDirty(water);EditorUtility.SetDirty(bridge.ocean);
        }
        [MenuItem("STORMSIM/During/Validate causal model")]
        public static string ValidateModel()
        {
            var model=AssetDatabase.LoadAssetAtPath<SibucaoCausalModel>(Folder+"/DuringBranchModel.asset");Require(model,"Model asset missing.");model.ValidateRun(null);
            int checks=0;
            for(int stage=0;stage<9;stage++)for(int choice=0;choice<SibucaoCausalModel.ChoiceCounts[stage];choice++)
            {
                var run=new PhysicalRunData{causal=new CausalRun()};
                for(int p=0;p<stage;p++)run.results[p]=0;
                run.results[stage]=choice;run.causal.path=SibucaoCausalModel.Path(run);
                var e=model.Enter(run,stage+1);float stamina=run.stamina;
                Require(e.parentPath.Length==stage+1&&e.cause==model.Find(stage,choice).nextSituation,"Missing downstream consumer.");
                Require(object.ReferenceEquals(e,model.Enter(run,stage+1))&&run.stamina==stamina,"Transition applied twice.");
                var restored=JsonUtility.FromJson<PhysicalRunData>(JsonUtility.ToJson(run));model.ValidateRun(restored);
                Require(restored.causal.path==run.causal.path&&model.Enter(restored,stage+1).cause==e.cause&&restored.stamina==stamina,"Save/resume changed branch state.");
                checks++;
            }
            var invalid=new PhysicalRunData();invalid.results[2]=0;bool rejected=false;
            try{model.ValidateRun(invalid);}catch(InvalidOperationException){rejected=true;}Require(rejected,"Out-of-order old save accepted.");
            var skip=new PhysicalRunData();bool blocked=false;try{model.Enter(skip,3);}catch(InvalidOperationException){blocked=true;}Require(blocked,"Out-of-order entry accepted.");
            var sample=new PhysicalRunData{causal=new CausalRun()};string target="AACAABADA";
            for(int i=0;i<9;i++){model.Enter(sample,i);sample.results[i]=target[i]-'A';sample.causal.path=SibucaoCausalModel.Path(sample);}
            model.Enter(sample,9);Require(sample.causal.path==target&&sample.causal.entries.Count==10,"Example path lost ancestry.");
            var c=AssetDatabase.LoadAssetAtPath<DecisionChoice>("Assets/StormSim/Sibucao/Data/PhysicalBranches/B2_C1.asset");
            var before=SibucaoDecisionEvaluation.Evaluate(c,"Student",42,1,1,WaterTier.Tier1,0,.08f);
            var after=SibucaoDecisionEvaluation.Evaluate(c,"Student",42,1,1,WaterTier.Tier1,0,-.08f);
            Require(before.chance>after.chance&&before.roll==after.roll,"Earlier choice did not affect downstream evaluation.");
            string report="PASS: "+checks+" choice-to-next-stage rules; idempotent transitions; save/resume; ordered entry; rejected skipped saves; AACAABADA ancestry; downstream risk influence.";
            Directory.CreateDirectory("Reports");File.WriteAllText("Reports/DuringCausalValidation.txt",report);Debug.Log(report);return report;
        }
    }
}
