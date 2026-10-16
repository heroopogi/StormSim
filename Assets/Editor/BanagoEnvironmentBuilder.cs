using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Parts=BanagoEnvironmentModels.Parts;

public static class BanagoEnvironmentBuilder
{
    public static string Status="Not built";
    public static string OutputFolder;
    static int nx,nz;
    static float[] heights;
    static float dx,dz;
    static string meshPath;
    static bool firstMesh;
    static int vertices;
    const string RootName="Banago Environment";

    [MenuItem("Tools/StormSim/Build Final Satellite Environment")]
    public static void Build()
    {
        if(EditorApplication.isPlaying || GameObject.Find(RootName))throw new InvalidOperationException("Exit Play mode and review the existing environment before rebuilding.");
        Status="Building final environment from verified imagery masks";
        EditorApplication.delayCall+=()=>{try{Create();}catch(Exception ex){Status="Failed: "+ex;Debug.LogError(Status);}};
    }
    static Color ColorOf(JToken t)=>new Color((float)t[0],(float)t[1],(float)t[2],1);
    static void LoadElevation()
    {
        var e=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-elevation.json"));nx=(int)e["nx"];nz=(int)e["nz"];
        dx=1202f/(nx-1);dz=1361f/(nz-1);heights=e["heights"].Values<float>().ToArray();
    }
    public static float Ground(float x,float z)
    {
        float gx=Mathf.Clamp((x+277)/dx,0,nx-1.001f),gz=Mathf.Clamp((z-89)/dz,0,nz-1.001f);
        int ix=Mathf.FloorToInt(gx),iz=Mathf.FloorToInt(gz);float u=gx-ix,v=gz-iz;
        float a=heights[iz*nx+ix],b=heights[iz*nx+ix+1],c=heights[(iz+1)*nx+ix],d=heights[(iz+1)*nx+ix+1];
        return u+v<=1 ? a+u*(b-a)+v*(c-a) : d+(1-u)*(c-d)+(1-v)*(b-d);
    }
    static void SaveMesh(Mesh m)
    {
        if(firstMesh){AssetDatabase.CreateAsset(m,meshPath);firstMesh=false;}else AssetDatabase.AddObjectToAsset(m,meshPath);
        vertices+=m.vertexCount;
    }
    static MeshRenderer Renderer(GameObject go,Mesh mesh,Material mat,bool shadow=true)
    {
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
        r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;return r;
    }
    static GameObject Child(string name,Transform parent)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);return g;
    }
    static Material Material(string name,bool vertex,Color color,float cull=2)
    {
        var m=new Material(Shader.Find("StormSim/Environment Lit")){name=name,enableInstancing=true};
        m.SetFloat("_VertexColor",vertex?1:0);m.SetFloat("_Cull",cull);m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.06f);
        AssetDatabase.CreateAsset(m,OutputFolder+"/"+name+".mat");return m;
    }
    static void Create()
    {
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/environment-placement.json"));
        if((int)data["summary"]["overlapFailures"]!=0)throw new InvalidOperationException("Placement masks did not pass clearance checks.");
        LoadElevation();var geo=GameObject.Find("CesiumGeoreference");var terrain=GameObject.Find("Cesium World Terrain");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");OutputFolder="Assets/StormSim/Generated/BanagoEnvironment_"+stamp;
        Directory.CreateDirectory(OutputFolder);
        File.Copy("RoadRepairBackup/environment-satellite.png",OutputFolder+"/SatelliteGround.png");
        AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(OutputFolder+"/SatelliteGround.png");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.anisoLevel=8;
        importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Finalize Banago environment");
        var root=Child(RootName,geo.transform);Undo.RegisterCreatedObjectUndo(root,"Finalize Banago environment");
        meshPath=OutputFolder+"/EnvironmentMeshes.asset";firstMesh=true;vertices=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            var groundMat=Material("Satellite ground - shadow receiving",false,Color.white);
            groundMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(OutputFolder+"/SatelliteGround.png"));
            CreateGround(Child("Terrain - measured elevation and satellite textures",root.transform).transform,groundMat);
            var foliage=Material("Leaf and bark colors",true,Color.white,0);
            var plants=Material("Grass flowers and litter",true,Color.white,0);
            CreateTrees(data,Child("Trees - satellite canopy locations",root.transform).transform,foliage);
            CreateShrubs(data,Child("Shrubs - canopy edges",root.transform).transform,foliage);
            CreateGrass(data,Child("Grass - green and dry vegetation masks",root.transform).transform,plants);
            CreateDetails(data,"flowers",Child("Sparse garden flowers",root.transform).transform,plants);
            CreateDetails(data,"clutter",Child("Leaves and small stones",root.transform).transform,plants);
            UpgradeBuildingsAndRoads();
            ConfigureDaylight(root.transform);
            Undo.RecordObject(terrain,"Replace streamed ground with measured textured ground");terrain.SetActive(false);
            var oldTest=GameObject.Find("Vegetation Test 100m");if(oldTest){Undo.RecordObject(oldTest,"Replace test vegetation");oldTest.SetActive(false);}
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
        finally{AssetDatabase.StopAssetEditing();}
        root.AddComponent<CesiumGlobeAnchor>();
        File.Copy("RoadRepairBackup/environment-placement.json",OutputFolder+"/PlacementSource.json");
        File.Copy("RoadRepairBackup/environment-mask-summary.json",OutputFolder+"/PlacementValidation.json");
        File.Copy("RoadRepairBackup/environment-elevation.json",OutputFolder+"/MeasuredElevation.json");
        File.Copy("RoadRepairBackup/environment-placement-mask.png",OutputFolder+"/PlacementMask.png");
        File.WriteAllText(OutputFolder+"/README.txt",data["summary"].ToString()+"\n\nGround texture: unaltered RGB satellite image captured from the scene's Cesium raster overlay, mapped with its original 1420m orthographic survey coordinates. All terrain vertices come from 66308 successful elevation samples. Dirt, paving, grass and worn transitions are preserved directly from imagery; no artificial hills or color-block terrain repainting.\n"+
            "Vegetation: independent whole-crown/clump clearance checks, 1.75m building and 1.5m street margin, with pond/water exclusions. Broadleaf, oval and palm-like forms are approximations. Flower/litter accents are deliberately sparse authored details within vegetated yards/canopy.\n"+
            "Tree/shrub meshes are shared and use near/far LODs. Grass is combined in spatial batches with a sparse distant LOD. Lighting uses a warm directional sun, ambient sky fill, soft cascaded shadows, SSAO and neutral tonemapping.\n"+
            "Original scene and pipeline settings: "+File.ReadAllText("RoadRepairBackup/environment-backup-path.txt")+". Previous test vegetation and streamed terrain are retained inactive for rollback. Unity Undo restores scene changes. The ground imagery retains the original provider content; source Cesium imagery attribution remains applicable.\n");
        AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(meshPath);
        EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject=root;Frame();
        Status="Saved final environment: "+data["summary"].ToString(Newtonsoft.Json.Formatting.None)+"; shared mesh vertices="+vertices+"; folder="+OutputFolder;
        Debug.Log("BANAGO_ENVIRONMENT: "+Status);
    }
    static void CreateGround(Transform parent,Material mat)
    {
        for(int z0=0;z0<nz-1;z0+=20)for(int x0=0;x0<nx-1;x0+=20)
        {
            int xs=Mathf.Min(20,nx-1-x0),zs=Mathf.Min(20,nz-1-z0),stride=xs+1;
            var v=new Vector3[(xs+1)*(zs+1)];var uv=new Vector2[v.Length];var tris=new List<int>();
            for(int z=0;z<=zs;z++)for(int x=0;x<=xs;x++)
            {
                int i=z*stride+x;float px=-277+(x+x0)*dx,pz=89+(z+z0)*dz;
                v[i]=new Vector3(px,heights[(z+z0)*nx+x+x0],pz);
                uv[i]=new Vector2((px+385.48376f)/1420,(pz-59.08044f)/1420);
                if(x<xs&&z<zs){tris.AddRange(new[]{i,i+stride,i+1,i+1,i+stride,i+stride+1});}
            }
            var m=new Mesh{name="Measured terrain "+x0+"_"+z0};m.vertices=v;m.uv=uv;m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();SaveMesh(m);
            var go=Child(m.name,parent);Renderer(go,m,mat);go.AddComponent<MeshCollider>().sharedMesh=m;
        }
    }
    static void CreateTrees(JObject data,Transform parent,Material material)
    {
        var items=((JArray)data["trees"]).ToArray();var models=new Dictionary<string,Mesh[]>();
        foreach(var g in items.GroupBy(t=>(int)t["kind"]+"_"+(int)t["variant"]))
        {
            var t=g.First();var colors=g.Select(t=>ColorOf(t["color"])).ToArray();var color=new Color(colors.Average(c=>c.r),colors.Average(c=>c.g),colors.Average(c=>c.b));
            var near=BanagoEnvironmentModels.Tree((int)t["kind"],(int)t["variant"],true,color);
            var far=BanagoEnvironmentModels.Tree((int)t["kind"],(int)t["variant"],false,color);
            SaveMesh(near);SaveMesh(far);models[g.Key]=new[]{near,far};
        }
        for(int i=0;i<items.Length;i++)
        {
            var t=items[i];string key=(int)t["kind"]+"_"+(int)t["variant"];
            var go=Plant("Tree "+(i+1).ToString("D4")+" - "+((int)t["kind"]==2?"palm-like":(int)t["kind"]==0?"spreading crown":"oval crown"),parent,t);
            AddLods(go,models[key],material,.11f,.0015f,true);
            var collider=go.AddComponent<CapsuleCollider>();collider.radius=.045f;collider.height=.56f;collider.center=Vector3.up*.28f;
        }
    }
    static GameObject Plant(string name,Transform parent,JToken t)
    {
        float x=(float)t["x"],z=(float)t["z"],radius=(float)t["radius"],height=(float)t["height"];
        if((int)t["kind"]==6&&(int)t["variant"]!=0)height=.012f;
        var go=Child(name,parent);go.transform.localPosition=new Vector3(x,Ground(x,z)+.004f,z);
        go.transform.localRotation=Quaternion.Euler(0,(float)t["yaw"],0);go.transform.localScale=new Vector3(radius,height,radius);return go;
    }
    static void AddLods(GameObject go,Mesh[] models,Material material,float near,float far,bool shadows)
    {
        var a=Renderer(Child("Near detail",go.transform),models[0],material,shadows);
        var b=Renderer(Child("Distant silhouette",go.transform),models[1],material,shadows);
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(near,new Renderer[]{a}),new LOD(far,new Renderer[]{b})});lod.RecalculateBounds();
    }
    static void CreateShrubs(JObject data,Transform parent,Material material)
    {
        var models=new Dictionary<int,Mesh[]>();int i=0;
        foreach(var t in (JArray)data["shrubs"])
        {
            int variant=(int)t["variant"];
            if(!models.ContainsKey(variant))
            {
                models[variant]=new[]{BanagoEnvironmentModels.Shrub(variant,true,ColorOf(t["color"])),BanagoEnvironmentModels.Shrub(variant,false,ColorOf(t["color"]))};
                foreach(var mesh in models[variant])SaveMesh(mesh);
            }
            var go=Plant("Shrub "+(++i).ToString("D3"),parent,t);AddLods(go,models[variant],material,.026f,.004f,false);
        }
    }
    static void CreateGrass(JObject data,Transform parent,Material material)
    {
        foreach(var batch in ((JArray)data["grass"]).GroupBy(t=>Mathf.FloorToInt((float)t["x"]/40)+"_"+Mathf.FloorToInt((float)t["z"]/40)))
        {
            var near=new Parts();var far=new Parts();int i=0;
            foreach(var t in batch)
            {
                float x=(float)t["x"],z=(float)t["z"];var center=new Vector3(x,Ground(x,z)+.004f,z);
                BanagoEnvironmentModels.Grass(near,center,(float)t["height"],(float)t["yaw"],ColorOf(t["color"]),i*7919+27,false);
                if(i%6==0)BanagoEnvironmentModels.Grass(far,center,(float)t["height"],(float)t["yaw"],ColorOf(t["color"]),i*7919+27,true);i++;
            }
            var a=near.Mesh("Grass near "+batch.Key);var b=far.Mesh("Grass distant "+batch.Key);SaveMesh(a);SaveMesh(b);
            var go=Child("Grass patch "+batch.Key+" ("+i+" clumps)",parent);AddLods(go,new[]{a,b},material,.045f,.014f,false);
            var lod=go.GetComponent<LODGroup>();lod.size=14; // approx. 150m full detail, 500m sparse silhouette
        }
    }
    static void CreateDetails(JObject data,string key,Transform parent,Material material)
    {
        var models=new Dictionary<int,Mesh>();int i=0;
        foreach(var t in (JArray)data[key])
        {
            int variant=(int)t["variant"];
            if(!models.ContainsKey(variant)){models[variant]=key=="flowers"?BanagoEnvironmentModels.Flowers(variant):BanagoEnvironmentModels.Clutter(variant);SaveMesh(models[variant]);}
            var go=Plant(key+" "+(++i).ToString("D3"),parent,t);var renderer=Renderer(go,models[variant],material,false);
            var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.007f,new Renderer[]{renderer})});lod.RecalculateBounds();
        }
    }
    static void UpgradeBuildingsAndRoads()
    {
        var houses=GameObject.Find("Banago Houses").GetComponentsInChildren<MeshRenderer>();
        Undo.RecordObjects(houses,"Enable house lighting and shadows");var houseMat=Material("Houses - daylight and shadows",true,Color.white);
        foreach(var r in houses){r.sharedMaterial=houseMat;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
        var roads=GameObject.Find("Study Area Roads").GetComponentsInChildren<MeshRenderer>();
        Undo.RecordObjects(roads,"Enable road shadow receiving");var roadMaterials=new Dictionary<Material,Material>();
        foreach(var r in roads)
        {
            var old=r.sharedMaterial;if(!old)continue;
            if(!roadMaterials.ContainsKey(old))
            {
                Color color=old.HasProperty("_BaseColor")?old.GetColor("_BaseColor"):old.HasProperty("_Color")?old.GetColor("_Color"):Color.gray;
                roadMaterials[old]=Material("Road surface "+roadMaterials.Count,false,color);
            }
            r.sharedMaterial=roadMaterials[old];r.receiveShadows=true;
        }
        var osm=GameObject.Find("Cesium OSM Buildings");
        if(osm)foreach(var r in osm.GetComponentsInChildren<Renderer>()){Undo.RecordObject(r,"Enable existing building shadows");r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
    }
    static void ConfigureDaylight(Transform parent)
    {
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();Undo.RecordObject(sun,"Natural daylight");Undo.RecordObject(sun.transform,"Natural sun angle");
        sun.transform.rotation=Quaternion.Euler(49,145,0);sun.color=Color.white;sun.useColorTemperature=true;sun.colorTemperature=5700;
        sun.intensity=1.45f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.86f;sun.shadowBias=.035f;sun.shadowNormalBias=.18f;RenderSettings.sun=sun;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.53f,.63f,.74f);
        RenderSettings.ambientEquatorColor=new Color(.38f,.43f,.40f);RenderSettings.ambientGroundColor=new Color(.22f,.20f,.16f);RenderSettings.ambientIntensity=1;
        var sky=new Material(Shader.Find("Skybox/Procedural")){name="Banago daylight sky"};sky.SetFloat("_SunSize",.025f);
        sky.SetFloat("_AtmosphereThickness",1.05f);sky.SetFloat("_Exposure",1.05f);sky.SetColor("_SkyTint",new Color(.52f,.59f,.66f));
        AssetDatabase.CreateAsset(sky,OutputFolder+"/DaylightSky.mat");RenderSettings.skybox=sky;
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");Undo.RecordObject(pipeline,"Soft cascaded shadows");
        var settings=new SerializedObject(pipeline);
        settings.FindProperty("m_ShadowDistance").floatValue=1300;
        settings.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
        settings.FindProperty("m_ShadowCascadeCount").intValue=4;
        settings.FindProperty("m_Cascade4Split").vector3Value=new Vector3(.06f,.20f,.48f);
        settings.FindProperty("m_SoftShadowsSupported").boolValue=true;settings.FindProperty("m_SoftShadowQuality").intValue=2;
        settings.FindProperty("m_MSAA").intValue=2;settings.ApplyModifiedProperties();EditorUtility.SetDirty(pipeline);
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach(var feature in renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>())
        {
            Undo.RecordObject(feature,"Environment contact shading");var so=new SerializedObject(feature);var s=so.FindProperty("m_Settings");
            s.FindPropertyRelative("Intensity").floatValue=.8f;s.FindPropertyRelative("Radius").floatValue=.65f;
            s.FindPropertyRelative("Falloff").floatValue=350;s.FindPropertyRelative("Downsample").boolValue=true;
            s.FindPropertyRelative("DirectLightingStrength").floatValue=.23f;so.ApplyModifiedProperties();feature.SetActive(true);EditorUtility.SetDirty(feature);
        }
        renderer.SetDirty();EditorUtility.SetDirty(renderer);
        var volume=Child("Natural daylight grade",parent).AddComponent<Volume>();volume.isGlobal=true;volume.priority=5;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,OutputFolder+"/DaylightVolume.asset");volume.sharedProfile=profile;
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);AssetDatabase.AddObjectToAsset(tone,profile);
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.1f);color.contrast.Override(4);color.saturation.Override(-3);AssetDatabase.AddObjectToAsset(color,profile);
        foreach(var camera in GameObject.Find("CesiumGeoreference").GetComponentsInChildren<Camera>())
        {
            if(camera.hideFlags!=HideFlags.None)continue;Undo.RecordObject(camera,"Daylight camera");camera.clearFlags=CameraClearFlags.Skybox;
            var extra=camera.GetUniversalAdditionalCameraData();Undo.RecordObject(extra,"Daylight grading");extra.renderPostProcessing=true;
        }
        foreach(SceneView view in SceneView.sceneViews){view.showGrid=false;view.sceneLighting=true;view.sceneViewState.showImageEffects=true;}
        DynamicGI.UpdateEnvironment();
    }
    public static void Frame()
    {
        var geo=GameObject.Find("CesiumGeoreference").transform;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(geo.TransformPoint(new Vector3(240,0,500)),Quaternion.Euler(54,315,0),100);
        SceneView.RepaintAll();
    }
    public static void CaptureReview()
    {
        Capture("RoadRepairBackup/environment-review.png",new Vector3(240,0,500),new Vector3(86,102,-90),false);
        Capture("RoadRepairBackup/environment-detail.png",new Vector3(229,0,527),new Vector3(17,10,-19),false);
        Capture("RoadRepairBackup/environment-overview.png",new Vector3(324,-3,769),new Vector3(680,1250,-920),true);
    }
    static void Capture(string path,Vector3 target,Vector3 offset,bool wide)
    {
        var geo=GameObject.Find("CesiumGeoreference").transform;
        var go=new GameObject("Environment review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();
        camera.transform.position=geo.TransformPoint(target+offset);camera.transform.LookAt(geo.TransformPoint(target));
        camera.fieldOfView=wide?52:48;camera.nearClipPlane=.2f;camera.farClipPlane=5000;camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
