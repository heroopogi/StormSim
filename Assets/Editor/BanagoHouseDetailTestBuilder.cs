using System;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BanagoHouseDetailTestBuilder
{
    const string RootName="House Detail Test - 15 buildings";
    public static string Status="Not built";
    public static string OutputFolder;
    static Color C(JToken t)=>new Color((float)t[0],(float)t[1],(float)t[2],1);
    static Texture2D Texture(string name,bool normal,bool roof)
    {
        const int size=256;var t=new Texture2D(size,size,TextureFormat.RGBA32,true,normal){name=name,wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
        var p=new Color[size*size];var rng=new System.Random(roof?443:717);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size;
            float fine=(float)rng.NextDouble()-.5f,noise=Mathf.PerlinNoise(u*18+31,v*18+45)-.5f;
            if(normal)
            {
                float sx=roof?Mathf.Sin(u*Mathf.PI*8)*.24f:noise*.17f+fine*.06f;
                float sy=roof?noise*.018f:(Mathf.PerlinNoise(u*22+63,v*22+18)-.5f)*.17f;
                p[y*size+x]=new Color(.5f+sx*.5f,.5f+sy*.5f,Mathf.Sqrt(Mathf.Max(0,1-sx*sx-sy*sy)),1);
            }
            else
            {
                float grain=roof?.946f+noise*.038f+fine*.022f+.012f*Mathf.Sin(u*Mathf.PI*8):.953f+noise*.050f+fine*.040f;
                p[y*size+x]=new Color(grain,grain,grain,1);
            }
        }
        t.SetPixels(p);t.Apply(true,false);AssetDatabase.CreateAsset(t,OutputFolder+"/"+name+".asset");return t;
    }
    static Material Surface(string name,Color color,Texture2D texture,Texture2D normal,bool metal)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
        mat.SetColor("_BaseColor",color);mat.SetTexture("_BaseMap",texture);mat.SetTexture("_BumpMap",normal);
        mat.SetFloat("_BumpScale",metal?.7f:.5f);mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_Smoothness",metal?.30f:.055f);mat.SetFloat("_Metallic",metal?.12f:0);
        mat.SetFloat("_Cull",0);AssetDatabase.CreateAsset(mat,OutputFolder+"/"+name+".mat");return mat;
    }
    static Material DetailMaterial(string name,float smoothness,Texture2D texture=null)
    {
        var mat=new Material(Shader.Find("StormSim/Environment Lit")){name=name,enableInstancing=true};
        mat.SetFloat("_VertexColor",1);mat.SetFloat("_Cull",0);mat.SetFloat("_Smoothness",smoothness);mat.SetColor("_BaseColor",Color.white);
        if(texture)mat.SetTexture("_BaseMap",texture);AssetDatabase.CreateAsset(mat,OutputFolder+"/"+name+".mat");return mat;
    }
    static Mesh MeshOf(JToken d,string name)
    {
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
        mesh.vertices=d["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();
        mesh.normals=d["normals"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();
        mesh.uv=d["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();mesh.colors=d["colors"].Select(C).ToArray();
        mesh.triangles=d["triangles"].Values<int>().ToArray();mesh.RecalculateTangents();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,OutputFolder+"/"+name+".asset");return mesh;
    }
    [MenuItem("Tools/StormSim/Build 15 House Detail Test")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        if(UnityEngine.Object.FindObjectsByType<BanagoHouseDetailTest>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)throw new InvalidOperationException("The house detail test already exists.");
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/house-detail-test-data.json"));
        if((int)data["summary"]["violations"]!=0)throw new InvalidOperationException("House detail geometry failed validation.");
        var all=GameObject.Find("Banago Houses").GetComponentsInChildren<BanagoHousePreview>();
        var originals=data["houses"].Select(h=>all.First(a=>a.footprintId==(string)h["id"]).gameObject).ToArray();
        var geo=GameObject.Find("CesiumGeoreference");string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");EditorSceneManager.SaveScene(geo.scene);
        File.Copy(geo.scene.path,"RoadRepairBackup/MAIN_GAME_before_house_detail_test_"+stamp+".unity");
        OutputFolder="Assets/StormSim/Generated/HouseDetailTest_"+stamp;Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Upgrade 15 house models for review");
        var root=new GameObject(RootName);root.transform.SetParent(geo.transform,false);Undo.RegisterCreatedObjectUndo(root,"Upgrade 15 house models for review");
        AssetDatabase.StartAssetEditing();
        try
        {
            var plaster=Texture("Fine plaster grain",false,false);var plasterNormal=Texture("Fine plaster normal",true,false);
            var metal=Texture("Weathered sheet metal",false,true);var metalNormal=Texture("Corrugated roof normal",true,true);
            var detail=DetailMaterial("Frames doors glass and fascia",.23f);var foundation=DetailMaterial("Concrete foundation",.035f,plaster);
            foreach(var h in data["houses"])
            {
                string name=(string)h["name"];var go=new GameObject(name+" - detailed");go.transform.SetParent(root.transform,false);
                var wallMat=Surface(name+" plaster",C(h["wallColor"]),plaster,plasterNormal,false);
                var roofMat=Surface(name+" sampled metal roof",C(h["roofColor"]),metal,metalNormal,true);
                var mats=new[]{wallMat,roofMat,detail,foundation};int index=0;
                foreach(var d in h["meshes"])
                {
                    var part=new GameObject((string)d["name"]);part.transform.SetParent(go.transform,false);var mesh=MeshOf(d,name+" "+(string)d["name"]);
                    part.AddComponent<MeshFilter>().sharedMesh=mesh;var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=mats[index++];r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    part.AddComponent<MeshCollider>().sharedMesh=mesh;
                }
            }
            var control=root.AddComponent<BanagoHouseDetailTest>();control.originalHouses=originals;control.originalActive=originals.Select(g=>g.activeSelf).ToArray();
            control.notes="15-building review batch. Pitched roofs retain satellite roof colors; new corrugated normal maps, ridge caps, fascia and overhangs. Plaster grain, recessed framed windows/doors, small estimated height changes, ground-fitted concrete foundations. Existing footprints preserved. Hide this group to restore the original houses. Facades and inferred heights are visual approximations, not surveyed details.";
            foreach(var house in originals)Undo.RecordObject(house,"Retain original house for test rollback");control.Apply();
            root.AddComponent<CesiumGlobeAnchor>();EditorUtility.SetDirty(control);
            File.Copy("RoadRepairBackup/house-detail-test-validation.json",OutputFolder+"/GeometryValidation.json");File.WriteAllText(OutputFolder+"/README.txt",control.notes+"\n");
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=root;Frame();Status="15 detailed houses added and saved. "+OutputFolder;
    }
    public static void Frame()
    {
        var view=SceneView.lastActiveSceneView;if(!view)return;
        view.orthographic=false;view.LookAtDirect(new Vector3(-73,-.5f,1358),Quaternion.LookRotation(new Vector3(28,-22,30)),38);view.showGrid=false;view.Repaint();
    }
    public static void CaptureReview()
    {
        Capture("RoadRepairBackup/house-detail-after.png",new Vector3(-76,-1,1370),new Vector3(-42,35,-42));
        Capture("RoadRepairBackup/house-detail-closeup.png",new Vector3(-67.25f,-.1f,1355.46f),new Vector3(-11,6,-12));
        Status="15-house overview and detailed facade review captured";
    }
    static void Capture(string path,Vector3 target,Vector3 offset)
    {
        var go=new GameObject("House detail review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();
        camera.transform.position=target+offset;camera.transform.LookAt(target);camera.fieldOfView=48;camera.nearClipPlane=.1f;camera.farClipPlane=3000;
        camera.clearFlags=CameraClearFlags.Skybox;var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.requiresDepthTexture=true;extra.requiresColorTexture=true;
        var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
