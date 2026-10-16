using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CesiumForUnity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BanagoCoastalTestBuilder
{
    public static string Status="Not built";
    public static string OutputFolder;
    static string meshPath;
    static bool firstMesh;
    const string RootName="Banago Coast";
    static GameObject FindRoot()=>GameObject.Find(RootName)??GameObject.Find("Coastal Water Test");
    static Color ColorOf(JToken c)=>new Color((float)c[0],(float)c[1],(float)c[2],c.Count()>3?(float)c[3]:1);
    static GameObject Child(string name,Transform parent)
    { var g=new GameObject(name);g.transform.SetParent(parent,false);return g; }
    static void SaveMesh(Mesh mesh)
    {
        if(firstMesh){AssetDatabase.CreateAsset(mesh,meshPath);firstMesh=false;}
        else AssetDatabase.AddObjectToAsset(mesh,meshPath);
    }
    static Mesh MeshOf(JToken d)
    {
        var m=new Mesh{name=(string)d["name"],indexFormat=IndexFormat.UInt32};
        m.vertices=d["vertices"].Select(t=>new Vector3((float)t[0],(float)t[1],(float)t[2])).ToArray();
        m.uv=d["uv"].Select(t=>new Vector2((float)t[0],(float)t[1])).ToArray();
        m.colors=d["colors"].Select(ColorOf).ToArray();m.triangles=d["triangles"].Values<int>().ToArray();
        SmoothNormals(m);m.RecalculateBounds();SaveMesh(m);return m;
    }
    static void SmoothNormals(Mesh mesh)
    {
        if(mesh.vertexCount==0)return;
        mesh.RecalculateNormals();var vertices=mesh.vertices;var normals=mesh.normals;
        var sums=new Dictionary<Vector3Int,Vector3>();
        Func<Vector3,Vector3Int> key=v=>new Vector3Int(Mathf.RoundToInt(v.x*1000),Mathf.RoundToInt(v.y*1000),Mathf.RoundToInt(v.z*1000));
        for(int i=0;i<vertices.Length;i++){var k=key(vertices[i]);sums.TryGetValue(k,out var n);sums[k]=n+normals[i];}
        for(int i=0;i<vertices.Length;i++)normals[i]=sums[key(vertices[i])].normalized;
        mesh.normals=normals;
    }
    public static void RefreshPreparedMeshes()
    {
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/coastal-test-data.json"));var root=FindRoot();
        var all=data["replacements"].Concat(new[]{data["water"],data["seabed"],data["sand"]});
        foreach(var d in all)
        {
            var filter=root.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name==(string)d["name"] || f.name==(string)d["name"]+" - coastal cut");
            var m=filter.sharedMesh;m.Clear();
            m.vertices=d["vertices"].Select(t=>new Vector3((float)t[0],(float)t[1],(float)t[2])).ToArray();
            m.uv=d["uv"].Select(t=>new Vector2((float)t[0],(float)t[1])).ToArray();
            m.colors=d["colors"].Select(ColorOf).ToArray();m.triangles=d["triangles"].Values<int>().ToArray();
            SmoothNormals(m);m.RecalculateBounds();m.UploadMeshData(false);EditorUtility.SetDirty(m);
            var collider=filter.GetComponent<MeshCollider>();if(collider){collider.sharedMesh=null;collider.sharedMesh=m;}
        }
        var wet=root.transform.Find("Wet sand - narrow 1.8m waterline").GetComponent<MeshRenderer>();
        if(wet.sharedMaterial.shader.name!="StormSim/Coastal Sand Blend")
        {
            var material=new Material(wet.sharedMaterial){name="Wet sand into original beach",shader=Shader.Find("StormSim/Coastal Sand Blend")};
            var original=root.GetComponent<BanagoCoastalTest>().originalGround[0].GetComponent<MeshRenderer>().sharedMaterial;
            material.SetTexture("_SatelliteMap",original.GetTexture("_BaseMap"));
            var folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(wet.sharedMaterial)).Replace('\\','/');
            AssetDatabase.CreateAsset(material,folder+"/Wet sand into original beach.mat");wet.sharedMaterial=material;
        }
        AssetDatabase.SaveAssets();Status="Coastal mesh transition refreshed";
    }
    static MeshRenderer Render(GameObject go,Mesh mesh,Material mat,bool collide,bool shadow=true)
    {
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
        r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;
        if(collide && mesh.vertexCount>0)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return r;
    }
    static Material Lit(string name,float smoothness=0)
    {
        var m=new Material(Shader.Find("StormSim/Environment Lit")){name=name,enableInstancing=true};
        m.SetFloat("_VertexColor",1);m.SetFloat("_Cull",0);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",smoothness);
        AssetDatabase.CreateAsset(m,OutputFolder+"/"+name+".mat");return m;
    }
    static Texture2D Ripples()
    {
        const int size=256;var t=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Small calm ripple normals",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x*2*Mathf.PI/size,v=y*2*Mathf.PI/size;
            float a=Mathf.Cos(u*4+v*2),b=Mathf.Cos(u*9-v*5+.7f),c=Mathf.Cos(u*15+v*11+1.6f);
            float sx=.43f*a+.23f*b+.11f*c,sz=.22f*a-.19f*b+.10f*c;
            pixels[y*size+x]=new Color(.5f+sx*.5f,.5f+sz*.5f,Mathf.Sqrt(Mathf.Max(0,1-sx*sx-sz*sz)),1);
        }
        t.SetPixels(pixels);t.Apply(true,false);AssetDatabase.CreateAsset(t,OutputFolder+"/CalmRippleNormals.asset");return t;
    }
    static Texture2D SandGrain()
    {
        const int size=128;var t=new Texture2D(size,size,TextureFormat.RGBA32,true,false){name="Fine sand grain detail",wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
        var rng=new System.Random(704);var p=new Color[size*size];
        for(int i=0;i<p.Length;i++){float v=.91f+.09f*(float)rng.NextDouble();p[i]=new Color(v,v,v,1);}
        t.SetPixels(p);t.Apply(true,false);AssetDatabase.CreateAsset(t,OutputFolder+"/SandGrain.asset");return t;
    }
    [MenuItem("Tools/StormSim/Build Coastal Water and Bangka Test")]
    public static void Build()
    {
        if(EditorApplication.isPlaying || UnityEngine.Object.FindObjectsByType<BanagoCoastalTest>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)
            throw new InvalidOperationException("Exit Play mode and review the existing coastal test first.");
        Status="Building coastal test";
        EditorApplication.delayCall+=()=>{try{Create();}catch(Exception ex){Status="Failed: "+ex;Debug.LogError(Status);}};
    }
    static void Create()
    {
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/coastal-test-data.json"));
        if((int)data["summary"]["violations"]!=0)throw new InvalidOperationException("Coastal placement validation failed.");
        var waterShader=Shader.Find("StormSim/Calm Coastal Water");
        if(!waterShader || ShaderUtil.ShaderHasError(waterShader))throw new InvalidOperationException("Water shader has errors.");
        var geo=GameObject.Find("CesiumGeoreference");var env=GameObject.Find("Banago Environment");
        var originals=data["replacements"].Select(t=>env.GetComponentsInChildren<MeshFilter>(true).First(m=>m.name==(string)t["name"]).gameObject).ToArray();
        var mat=originals[0].GetComponent<MeshRenderer>().sharedMaterial;
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EditorSceneManager.SaveScene(geo.scene);
        File.Copy(geo.scene.path,"RoadRepairBackup/MAIN_GAME_before_coastal_test_"+stamp+".unity");
        OutputFolder="Assets/StormSim/Generated/BanagoCoastalTest_"+stamp;Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("100m coastal water and bangka test");
        var root=Child(RootName,geo.transform);Undo.RegisterCreatedObjectUndo(root,"100m coastal water and bangka test");
        meshPath=OutputFolder+"/CoastalMeshes.asset";firstMesh=true;
        AssetDatabase.StartAssetEditing();
        try
        {
            var terrain=Child("Land preserved outside traced waterline",root.transform);
            foreach(var d in data["replacements"])Render(Child((string)d["name"]+" - coastal cut",terrain.transform),MeshOf(d),mat,true);
            var sandMat=Lit("Satellite sampled wet sand");sandMat.SetTexture("_BaseMap",SandGrain());sandMat.SetTextureScale("_BaseMap",Vector2.one*2840);
            Render(Child("Shallow sand seabed",root.transform),MeshOf(data["seabed"]),sandMat,true);
            var blendMat=new Material(sandMat){name="Wet sand into original beach",shader=Shader.Find("StormSim/Coastal Sand Blend")};
            blendMat.SetTexture("_SatelliteMap",mat.GetTexture("_BaseMap"));AssetDatabase.CreateAsset(blendMat,OutputFolder+"/Wet sand into original beach.mat");
            Render(Child("Wet sand - narrow 1.8m waterline",root.transform),MeshOf(data["sand"]),blendMat,true);
            var waterMat=new Material(waterShader){name="Calm coastal blue teal water"};
            waterMat.SetTexture("_RippleNormal",Ripples());waterMat.SetColor("_DeepColor",new Color(.115f,.34f,.39f));
            waterMat.SetColor("_ShallowColor",new Color(.24f,.48f,.43f));
            waterMat.SetFloat("_NormalStrength",.16f);waterMat.SetFloat("_RippleSpeed",.006f);waterMat.SetFloat("_FoamStrength",.18f);
            AssetDatabase.CreateAsset(waterMat,OutputFolder+"/Calm coastal water.mat");
            Render(Child("Calm water - traced sea boundary",root.transform),MeshOf(data["water"]),waterMat,false,false);
            var control=root.AddComponent<BanagoCoastalTest>();control.waterMaterial=waterMat;
            control.originalGround=originals;control.originallyActive=originals.Select(g=>g.activeSelf).ToArray();
            control.placementNotes="100m test. Shoreline traced from original Banago satellite capture. Sand strip 1.8m. One boat-like shoreline candidate at (-132.35,1375.25); identity and bow direction uncertain. Boat appearance follows user's white/yellow bangka photo. Full boat outline >8m from houses. Disable this group to restore original ground.";
            foreach(var g in originals)Undo.RecordObject(g,"Retain original coastal ground for restoration");
            control.ApplyVisibility();Physics.SyncTransforms();
            var model=BanagoBangkaModel.Create();SaveMesh(model);var boatMat=Lit("Bangka white yellow blue painted wood",.23f);
            var boat=Child("Bangka - observed shoreline candidate",root.transform);
            float x=(float)data["boat"]["x"],z=(float)data["boat"]["z"],yaw=(float)data["boat"]["yaw"];
            boat.transform.localPosition=new Vector3(x,0,z);boat.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // Seat the partly beached hull and outrigger floats above the reconstructed ground.
            float baseY=(float)data["seaSamples"].OrderBy(t=>Mathf.Abs((float)t[0]-z)).First()[1]-.065f;
            var supports=new List<Vector3>();
            foreach(float zz in new[]{-2.65f,-1.3f,0,1.3f,2.65f})
            {
                float keel=-.19f+.20f*Mathf.Pow(Mathf.Abs(zz/3.3f),3);
                supports.Add(new Vector3(0,keel,zz));
                foreach(int side in new[]{-1,1})supports.Add(new Vector3(side*2,.055f+.27f*Mathf.Pow(Mathf.Abs(zz/2.65f),5)-.065f,zz));
            }
            var groundColliders=env.GetComponentsInChildren<MeshCollider>().Concat(root.GetComponentsInChildren<MeshCollider>()).Where(c=>c.gameObject.activeInHierarchy).ToArray();
            foreach(var support in supports)
            {
                var v=boat.transform.TransformPoint(support);float best=float.NegativeInfinity;
                var ray=new Ray(new Vector3(v.x,50,v.z),Vector3.down);
                foreach(var collider in groundColliders)if(collider.Raycast(ray,out var hit,100))best=Mathf.Max(best,hit.point.y);
                if(!float.IsNegativeInfinity(best))baseY=Mathf.Max(baseY,best-support.y+.014f);
            }
            boat.transform.localPosition=new Vector3(x,baseY,z);Render(boat,model,boatMat,true);
            File.WriteAllText(OutputFolder+"/PlacementValidation.json",data["summary"].ToString());
            File.WriteAllText(OutputFolder+"/README.txt",control.placementNotes+"\n\nWater and wet-sand geometry excludes roofs with 3m margin, roads with 1.8m margin. Original terrain chunks retained; boat clearance uses the full 4.2 x 6.6m oriented envelope. Shallow seabed is visual reconstruction, not surveyed bathymetry. Sand colors sampled from satellite, fine grain generated. Calm normal-map ripples; shoreline-only foam; no physical waves. No additional boats inferred.\n");
            root.AddComponent<CesiumGlobeAnchor>();EditorUtility.SetDirty(control);
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=root;Frame();
        Status="Complete: 100m coast, 1 bangka, 1.8m wet sand. Houses >8m from water and boat. "+OutputFolder;
    }
    public static void Frame()
    {
        var view=SceneView.lastActiveSceneView;if(!view)return;
        var target=new Vector3(-138,-7.4f,1378);
        view.orthographic=false;view.LookAtDirect(target,Quaternion.LookRotation(new Vector3(1,-.52f,.40f)),40);view.showGrid=false;view.Repaint();
    }
    [MenuItem("Tools/StormSim/Extend Water Across Full Banago Sea")]
    public static void ExpandFullCoast()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        var root=FindRoot();if(!root)throw new InvalidOperationException("Build the coastal test first.");
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/coastal-full-data.json"));
        if((int)data["summary"]["violations"]!=0)throw new InvalidOperationException("Full coast mask failed validation.");
        var control=root.GetComponent<BanagoCoastalTest>();
        var prior=new Dictionary<GameObject,bool>();for(int i=0;i<control.originalGround.Length;i++)prior[control.originalGround[i]]=control.originallyActive[i];
        var env=GameObject.Find("Banago Environment");var filters=env.GetComponentsInChildren<MeshFilter>(true);
        var originals=data["replacements"].Select(t=>filters.First(f=>f.name==(string)t["name"] || ((string)t["name"]=="Measured terrain 0_0" && f.name=="EnvironmentMeshes")).gameObject).ToArray();
        var states=originals.Select(g=>prior.TryGetValue(g,out var active)?active:g.activeSelf).ToArray();
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");EditorSceneManager.SaveScene(root.scene);
        File.Copy(root.scene.path,"RoadRepairBackup/MAIN_GAME_before_full_sea_"+stamp+".unity");
        OutputFolder="Assets/StormSim/Generated/BanagoCoast_"+stamp;Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Extend water across full Banago sea");
        Undo.RecordObject(root,"Expand coastal water");Undo.RecordObject(control,"Store original land for restoration");
        meshPath=OutputFolder+"/CoastMeshes.asset";firstMesh=true;
        AssetDatabase.StartAssetEditing();
        try
        {
            var parent=root.transform.Find("Land preserved outside traced waterline");
            var groundMat=originals[0].GetComponent<MeshRenderer>().sharedMaterial;
            foreach(var d in data["replacements"])
            {
                string name=(string)d["name"]+" - coastal cut";var old=parent.Find(name);var mesh=MeshOf(d);
                if(old)AssignMesh(old.gameObject,mesh);
                else{var child=Child(name,parent);Render(child,mesh,groundMat,true);Undo.RegisterCreatedObjectUndo(child,"Extend coastal terrain cut");}
            }
            AssignMesh(root.transform.Find("Calm water - traced sea boundary").gameObject,MeshOf(data["water"]));
            AssignMesh(root.transform.Find("Shallow sand seabed").gameObject,MeshOf(data["seabed"]));
            AssignMesh(root.transform.Find("Wet sand - narrow 1.8m waterline").gameObject,MeshOf(data["sand"]));
            foreach(var g in originals)Undo.RecordObject(g,"Retain original sea ground");
            control.originalGround=originals;control.originallyActive=states;control.ApplyVisibility();
            root.name=RootName;
            control.placementNotes="Full sea coverage along the 1,361m study-area coast. Boundary follows satellite coastline and harbor; houses have 3m water buffer, roads 1.8m. Visible breakwaters and jetty remain clear. Narrow 1.8m wet-sand treatment retained on the reviewed beach section. Existing boat instances and positions preserved. Disable Banago Coast to restore the original ground. Boat identity/orientation was approximate in glare-obscured imagery.";
            File.WriteAllText(OutputFolder+"/PlacementValidation.json",data["summary"].ToString());
            File.WriteAllText(OutputFolder+"/README.txt",control.placementNotes+"\n\nCalm ripple shader and boat assets reused from the approved test. Bathymetry is a visual reconstruction. Harbor edges retain the measured original terrain grade. No additional boats placed during expansion.\n");
            File.Copy("RoadRepairBackup/coastal-full-reference-marked.png",OutputFolder+"/CoastlineReference.png");
            EditorUtility.SetDirty(control);
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=root;Status="Full Banago sea covered. "+originals.Length+" original ground chunks retained for restoration. "+OutputFolder;
    }
    static void AssignMesh(GameObject go,Mesh mesh)
    {
        var filter=go.GetComponent<MeshFilter>();Undo.RecordObject(filter,"Update coastal mesh");filter.sharedMesh=mesh;
        var collider=go.GetComponent<MeshCollider>();if(collider){Undo.RecordObject(collider,"Update coastal ground collision");collider.sharedMesh=null;if(mesh.vertexCount>0)collider.sharedMesh=mesh;}
    }
    public static void CaptureFullCoast()
    {
        Capture("RoadRepairBackup/banago-full-sea.png",new Vector3(-95,-5,770),new Vector3(-1150,910,-175));
        Capture("RoadRepairBackup/banago-harbor-water.png",new Vector3(-145,-8,550),new Vector3(-150,105,-70));
        var view=SceneView.lastActiveSceneView;
        if(view){view.orthographic=false;view.LookAtDirect(new Vector3(-100,-6,770),Quaternion.LookRotation(new Vector3(1,-.68f,.05f)),760);view.showGrid=false;view.Repaint();}
        Status="Full sea and harbor review captured";
    }
    public static void CaptureReview()
    {
        Capture("RoadRepairBackup/coastal-test-overview.png",new Vector3(-138,-7.3f,1375),new Vector3(-58,44,-37));
        Capture("RoadRepairBackup/coastal-test-bangka.png",new Vector3(-132.35f,-6.9f,1375.25f),new Vector3(-10,5.7f,-7.5f));
        Status="Coastal overview and bangka detail captured.";
    }
    static void Capture(string path,Vector3 target,Vector3 offset)
    {
        var go=new GameObject("Coastal review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();
        camera.transform.position=target+offset;camera.transform.LookAt(target);camera.fieldOfView=48;camera.nearClipPlane=.12f;camera.farClipPlane=4000;
        camera.clearFlags=CameraClearFlags.Skybox;var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.requiresColorTexture=true;extra.requiresDepthTexture=true;
        var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
