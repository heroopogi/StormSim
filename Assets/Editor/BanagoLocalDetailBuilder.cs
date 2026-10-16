using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using CesiumForUnity;

public static class BanagoLocalDetailBuilder
{
    public static string Status="Not built";
    public static string OutputFolder;
    static string meshPath; static bool firstMesh;
    static Color ColorOf(JToken t) => new Color((float)t[0],(float)t[1],(float)t[2],t.Count()>3?(float)t[3]:1);
    static Mesh ReadMesh(JToken d)
    {
        var m=new Mesh{name=(string)d["name"],indexFormat=IndexFormat.UInt32};
        m.vertices=d["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();
        m.normals=d["normals"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();
        m.uv=d["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();
        m.colors=d["colors"].Select(ColorOf).ToArray();m.triangles=d["triangles"].Values<int>().ToArray();m.RecalculateBounds();m.RecalculateTangents();
        if(m.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new InvalidDataException(m.name+" has invalid coordinates");
        if(firstMesh){AssetDatabase.CreateAsset(m,meshPath);firstMesh=false;}else AssetDatabase.AddObjectToAsset(m,meshPath);
        return m;
    }
    static Texture2D Grain(string name,bool rust)
    {
        int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,true){name=name,wrapMode=TextureWrapMode.Repeat,anisoLevel=4};var c=new Color[n*n];var rng=new System.Random(rust?770:990);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=y/(float)n,noise=Mathf.PerlinNoise(u*12+31,v*12+12),fine=(float)rng.NextDouble()-.5f;
            float gray=.78f+noise*.24f+fine*.08f;
            if(rust){float stain=Mathf.SmoothStep(.27f,.73f,Mathf.PerlinNoise(u*7+66,v*6+44));c[y*n+x]=Color.Lerp(new Color(.95f,.96f,.91f),new Color(.49f,.29f,.18f),stain*.55f)*(gray+.055f*Mathf.Sin(u*24*Mathf.PI));}
            else c[y*n+x]=new Color(gray,gray,gray,1);
        }
        t.SetPixels(c);t.Apply();AssetDatabase.CreateAsset(t,OutputFolder+"/"+name+".asset");return t;
    }
    static Material VertexMat(string name,Texture2D texture,float smooth=.1f)
    {
        var m=new Material(Shader.Find("StormSim/Environment Lit")){name=name,enableInstancing=true};m.SetFloat("_VertexColor",1);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Cull",0);m.SetFloat("_Smoothness",smooth);if(texture)m.SetTexture("_BaseMap",texture);AssetDatabase.CreateAsset(m,OutputFolder+"/"+name+".mat");return m;
    }
    public static void BuildInland() { Build("RoadRepairBackup/banago-inland-data.json","Banago Inland - ponds farms and vacant land"); }
    public static void BuildCommunity() { Build("RoadRepairBackup/banago-community-data.json","Banago Community - streets civic buildings and pier"); }
    public static void RefineCommunity()
    {
        var root=GameObject.Find("Banago Community - streets civic buildings and pier");
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/banago-community-data.json"));
        OutputFolder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(root.GetComponentsInChildren<MeshFilter>()[0].sharedMesh)).Replace('\\','/');
        meshPath=OutputFolder+"/RefinedCommunityMeshes.asset";firstMesh=true;
        foreach(var d in data["meshes"])
        {
            var f=root.GetComponentsInChildren<MeshFilter>().First(g=>g.name==(string)d["name"]);f.sharedMesh=ReadMesh(d);
            var c=f.GetComponent<MeshCollider>();if(c)c.sharedMesh=f.sharedMesh;
        }
        var texts=root.GetComponentsInChildren<TextMesh>();int index=0;
        foreach(var d in data["labels"])
        {
            var txt=texts[index++];txt.transform.localPosition=new Vector3((float)d["position"][0],(float)d["position"][1],(float)d["position"][2]);
            txt.transform.localRotation=Quaternion.Euler(0,(float)d["yaw"],0);txt.characterSize=(float)d["size"]*.57f;
        }
        var control=root.GetComponent<BanagoLocalDetailReview>();var originals=control.originals.ToList();var active=control.originalActive.ToList();
        var houses=GameObject.Find("Banago Houses").GetComponentsInChildren<BanagoHousePreview>(true);
        foreach(var id in JArray.Parse(File.ReadAllText("RoadRepairBackup/community-extra-shell-ids.json")).Values<string>())
        {
            var go=houses.First(h=>h.footprintId==id).gameObject;if(!originals.Contains(go)){originals.Add(go);active.Add(go.activeSelf);}
        }
        control.originals=originals.ToArray();control.originalActive=active.ToArray();control.Apply();EditorUtility.SetDirty(control);
        var lettering=AssetDatabase.LoadAssetAtPath<Material>(OutputFolder+"/Sign lettering.mat");
        foreach(var name in new[]{root.name,"Banago Market and Pier - reference details"})
        {
            var group=GameObject.Find(name);var signs=group.GetComponent<BanagoWorldSigns>();if(!signs)signs=group.AddComponent<BanagoWorldSigns>();signs.lettering=lettering;
            foreach(var txt in group.GetComponentsInChildren<TextMesh>())txt.GetComponent<MeshRenderer>().sharedMaterial=lettering;EditorUtility.SetDirty(signs);
        }
        var sky=new Material(Shader.Find("StormSim/Hazy Coastal Sky")){name="Gray coastal cloud cover"};AssetDatabase.CreateAsset(sky,OutputFolder+"/Gray coastal clouds.mat");
        var atmosphere=root.GetComponent<BanagoReviewAtmosphere>();atmosphere.hazySky=sky;atmosphere.Apply();EditorUtility.SetDirty(atmosphere);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Status="Refined campus clearance, signs and coastal sky";
    }
    public static void RefineInland()
    {
        var root=GameObject.Find("Banago Inland - ponds farms and vacant land");var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/banago-inland-data.json"));
        OutputFolder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(root.GetComponentsInChildren<MeshFilter>()[0].sharedMesh)).Replace('\\','/');
        meshPath=OutputFolder+"/RefinedMeshes.asset";firstMesh=true;
        var control=root.GetComponent<BanagoLocalDetailReview>();var originalList=control.originals.ToList();var activeList=control.originalActive.ToList();
        var ground=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/BanagoEnvironment_20260904_181528/Satellite ground - shadow receiving.mat");
        var mud=AssetDatabase.LoadAssetAtPath<Material>(OutputFolder+"/Weathered concrete.mat");
        foreach(var d in data["meshes"].Concat(data["replacements"].Select(t=>t["mesh"])))
        {
            string name=(string)d["name"];var f=root.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f=>f.name==name);
            if(!f)
            {
                var parent=root.transform.Find((string)d["group"]);var child=new GameObject(name);child.transform.SetParent(parent,false);f=child.AddComponent<MeshFilter>();child.AddComponent<MeshRenderer>().sharedMaterial=ground;
                var record=data["replacements"].First(t=>(string)t["mesh"]["name"]==name);string oldName=(string)record["name"];
                var orig=GameObject.Find("Banago Environment").GetComponentsInChildren<MeshFilter>(true).First(g=>g.name==oldName).gameObject;
                if(!orig.activeInHierarchy)throw new InvalidOperationException("Refinement overlaps another ground replacement");originalList.Add(orig);activeList.Add(true);
            }
            var mesh=ReadMesh(d);Undo.RecordObject(f,"Refine pond geometry");f.sharedMesh=mesh;
            var collider=f.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=mesh;else if((bool?)d["collider"]==true)f.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            var renderer=f.GetComponent<MeshRenderer>();if((string)d["material"]=="concrete")renderer.sharedMaterial=mud;
            if((string)d["material"]=="pond"){renderer.sharedMaterial.SetColor("_BaseColor",ColorOf(d["tint"]));EditorUtility.SetDirty(renderer.sharedMaterial);}
        }
        control.originals=originalList.ToArray();control.originalActive=activeList.ToArray();control.Apply();EditorUtility.SetDirty(control);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Status="Refined pond banks and full water interiors";
    }
    public static void PolishCommunity()
    {
        var root=GameObject.Find("Banago Community - streets civic buildings and pier");var control=root.GetComponent<BanagoLocalDetailReview>();
        OutputFolder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(root.GetComponentsInChildren<MeshFilter>()[0].sharedMesh)).Replace('\\','/');
        var clip=root.GetComponent<BanagoCampusReplacement>();if(!clip)clip=root.AddComponent<BanagoCampusReplacement>();
        clip.streamedBuildings=GameObject.Find("Cesium OSM Buildings").transform;clip.georeference=GameObject.Find("CesiumGeoreference").transform;
        clip.campus=new[]{new Vector2(-34,714),new Vector2(109,714),new Vector2(109,587),new Vector2(-18,587),new Vector2(-38,650)};clip.Apply();EditorUtility.SetDirty(clip);
        var originalList=control.originals.ToList();var states=control.originalActive.ToList();
        foreach(string name in JArray.Parse(File.ReadAllText("RoadRepairBackup/community-tree-clearance.json")).Values<string>())
        {
            var go=GameObject.Find(name);if(go&&!originalList.Contains(go)){originalList.Add(go);states.Add(go.activeSelf);}
        }
        var parent=new GameObject("Coconut palms at existing canopy positions");parent.transform.SetParent(root.transform,false);
        string[] palmNames={"Tree 0036 - oval crown","Tree 0040 - oval crown","Tree 0047 - spreading crown","Tree 0057 - spreading crown"};int count=0;
        var mat=root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.sharedMaterial).First(m=>m&&m.name=="Mixed local details");
        foreach(string name in palmNames)
        {
            var source=GameObject.Find(name);if(!source||originalList.Contains(source))continue;
            var go=new GameObject("Coconut palm "+(++count));go.transform.SetParent(parent.transform,false);go.transform.position=source.transform.position;go.transform.rotation=source.transform.rotation;go.transform.localScale=new Vector3(source.transform.localScale.x,10.5f+count*.6f,source.transform.localScale.z);
            var mesh=BanagoEnvironmentModels.Tree(2,count,true,new Color(.29f,.40f,.18f));AssetDatabase.CreateAsset(mesh,OutputFolder+"/Coconut palm "+count+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;originalList.Add(source);states.Add(source.activeSelf);
        }
        control.originals=originalList.ToArray();control.originalActive=states.ToArray();control.Apply();EditorUtility.SetDirty(control);
        var texts=root.GetComponentsInChildren<TextMesh>();var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture(string.Concat(texts.Select(t=>t.text)),64);
        var lettering=new Material(Shader.Find("StormSim/World Sign Text")){name="Depth-tested painted sign lettering"};lettering.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(lettering,OutputFolder+"/Sign lettering.mat");
        foreach(var text in texts){text.characterSize*=.57f;text.GetComponent<MeshRenderer>().sharedMaterial=lettering;EditorUtility.SetDirty(text);}
        var sky=new Material(Shader.Find("Skybox/Procedural")){name="Muted humid daylight"};sky.SetColor("_SkyTint",new Color(.60f,.64f,.69f));sky.SetColor("_GroundColor",new Color(.47f,.49f,.51f));sky.SetFloat("_AtmosphereThickness",1.1f);sky.SetFloat("_Exposure",1.0f);sky.SetFloat("_SunSize",.005f);AssetDatabase.CreateAsset(sky,OutputFolder+"/Muted humid sky.mat");
        var atmosphere=root.GetComponent<BanagoReviewAtmosphere>();if(!atmosphere)atmosphere=root.AddComponent<BanagoReviewAtmosphere>();atmosphere.sun=GameObject.Find("Directional Light").GetComponent<Light>();atmosphere.hazySky=sky;atmosphere.CaptureAndApply();EditorUtility.SetDirty(atmosphere);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Status="Removed old campus shells, cleared intersecting canopies, added four coconut palms, fixed sign depth and softened lighting";
    }
    static void Build(string file,string rootName)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
        if(GameObject.Find(rootName))throw new InvalidOperationException("Review group already exists");
        var data=JObject.Parse(File.ReadAllText(file));var geo=GameObject.Find("CesiumGeoreference");var originals=new List<GameObject>();
        var terrain=GameObject.Find("Banago Environment").GetComponentsInChildren<MeshFilter>(true);
        foreach(var t in data["replacements"]??new JArray())
        {
            string name=(string)t["name"];var g=terrain.First(f=>f.name==name || (name=="Measured terrain 0_0" && f.name=="EnvironmentMeshes")).gameObject;
            if(!g.activeInHierarchy)throw new InvalidOperationException("Terrain already replaced by another review: "+name);originals.Add(g);
        }
        var houses=GameObject.Find("Banago Houses").GetComponentsInChildren<BanagoHousePreview>(true);
        foreach(var id in data["replaceHouseIds"]??new JArray()) {var h=houses.First(h=>h.footprintId==(string)id);if(!h.gameObject.activeInHierarchy)throw new InvalidOperationException("House already replaced: "+h.name);originals.Add(h.gameObject);}
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");EditorSceneManager.SaveScene(geo.scene);File.Copy(geo.scene.path,"RoadRepairBackup/MAIN_GAME_before_local_detail_"+stamp+".unity");
        OutputFolder="Assets/StormSim/Generated/BanagoLocalDetail_"+stamp;Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add Banago local environment review");
        var root=new GameObject(rootName);root.transform.SetParent(geo.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add Banago detail review");
        var control=root.AddComponent<BanagoLocalDetailReview>();control.originals=originals.ToArray();control.originalActive=originals.Select(g=>g.activeSelf).ToArray();
        control.notes=data["summary"].ToString()+"\nHide this root to restore the original objects. All geometry remains inside the existing study boundary. Architectural details are reference-based approximations.";
        meshPath=OutputFolder+"/DetailMeshes.asset";firstMesh=true;
        try
        {
            var grain=Grain("Concrete and soil grain",false);var rust=Grain("Weathered sheet metal",true);
            var mats=new Dictionary<string,Material>{{"detail",VertexMat("Mixed local details",null)},{"concrete",VertexMat("Weathered concrete",grain,.045f)},{"rust",VertexMat("Rust and corrugated metal",rust,.22f)}};
            mats["ground"]=AssetDatabase.LoadAssetAtPath<Material>("Assets/StormSim/Generated/BanagoEnvironment_20260904_181528/Satellite ground - shadow receiving.mat");
            var groups=new Dictionary<string,Transform>();
            var entries=(data["meshes"]??new JArray()).Concat((data["replacements"]??new JArray()).Select(t=>t["mesh"]));
            foreach(var d in entries)
            {
                if(d["vertices"].Count()==0)continue;string group=(string)d["group"],kind=(string)d["material"];
                if(!groups.TryGetValue(group,out var parent)){var go=new GameObject(group);go.transform.SetParent(root.transform,false);parent=go.transform;groups[group]=parent;}
                var child=new GameObject((string)d["name"]);child.transform.SetParent(parent,false);var mesh=ReadMesh(d);child.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=child.AddComponent<MeshRenderer>();
                if(kind=="pond")
                {
                    var mat=new Material(Shader.Find("StormSim/Inland Still Water")){name=child.name+" material"};mat.SetColor("_BaseColor",ColorOf(d["tint"]));AssetDatabase.CreateAsset(mat,OutputFolder+"/"+child.name+".mat");renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                else {renderer.sharedMaterial=mats[kind];renderer.shadowCastingMode=ShadowCastingMode.On;}
                renderer.receiveShadows=true;if((bool?)d["collider"]==true)child.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            foreach(var t in data["labels"]??new JArray())
            {
                var g=new GameObject((string)t["text"]);g.transform.SetParent(root.transform,false);g.transform.localPosition=new Vector3((float)t["position"][0],(float)t["position"][1],(float)t["position"][2]);g.transform.localRotation=Quaternion.Euler(0,(float)t["yaw"],0);
                var text=g.AddComponent<TextMesh>();text.text=(string)t["text"];text.characterSize=(float)t["size"];text.fontSize=64;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=ColorOf(t["color"]);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                g.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(var g in originals)Undo.RecordObject(g,"Retain original object for review");control.Apply();root.AddComponent<CesiumGlobeAnchor>().Sync();EditorUtility.SetDirty(control);
            File.WriteAllText(OutputFolder+"/PlacementNotes.json",data["summary"].ToString());AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;Status=rootName+": "+data["summary"].ToString();
        }
        catch { Undo.RevertAllDownToGroup(undo);throw; }
    }
    public static void Capture(string path,Vector3 target,Vector3 offset)
    {
        var go=new GameObject("Local detail review camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.transform.position=target+offset;cam.transform.LookAt(target);cam.fieldOfView=52;cam.nearClipPlane=.08f;cam.farClipPlane=4000;
        cam.clearFlags=CameraClearFlags.Skybox;var extra=cam.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.requiresDepthTexture=true;extra.requiresColorTexture=true;
        var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;Texture2D image=null;
        try {cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally {RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/StormSim/Review Ponds and Farmland")]
    public static void FramePonds() {var v=SceneView.lastActiveSceneView;if(v){v.orthographic=false;v.LookAtDirect(new Vector3(320,-2,875),Quaternion.Euler(45,25,0),340);v.showGrid=false;}}
}
