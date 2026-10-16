using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Applies the reviewed street-reference plan in small editor batches. Never runs automatically.</summary>
public static class BaybayinStreetDetailBuilder
{
    const string Folder="Assets/StormSim/Generated/BaybayinStreetDetail_20260915";
    const string Source="RoadRepairBackup/BaybayinStreetDetail";
    static JObject plan;
    static List<JToken> pending;
    static Dictionary<string,GameObject> existing;
    static Dictionary<string,Material> materials;
    static GameObject root;
    static int next;
    static string prefix="Detail";
    static Vector3 V(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
    static Color C(JToken p)=>new Color((float)p[0],(float)p[1],(float)p[2],1);
    public static object Begin(bool extras=false)
    {
        if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Open MAIN_GAME in edit mode.");
        root=GameObject.Find(BaybayinExpansionBuilder.RootName);
        if(!root)throw new Exception("The existing expansion is required.");
        prefix=extras?"Extra":"Detail";
        plan=JObject.Parse(File.ReadAllText(Source+(extras?"/extra-plan.json":"/detail-plan.json")));
        pending=plan["objects"].Concat(plan["replacements"]).ToList();next=0;
        existing=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First().gameObject);
        Directory.CreateDirectory(Folder);
        materials=new Dictionary<string,Material>();
        foreach(var pair in new[]{new{key="walls",file="Weathered plaster and timber",tint=.80f},new{key="roofdetail",file="Satellite sampled corrugated metal",tint=.86f},new{key="detail",file="Doors windows roads and shoreline",tint=.88f}})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+pair.key+".mat");
            if(!mat){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(BaybayinExpansionBuilder.Folder+"/"+pair.file+".mat"));mat.name=pair.key;AssetDatabase.CreateAsset(mat,Folder+"/"+pair.key+".mat");}
            mat.SetColor("_BaseColor",new Color(pair.tint,pair.tint,pair.tint,1));materials[pair.key]=mat;EditorUtility.SetDirty(mat);
        }
        return new{objects=pending.Count,summary=plan["summary"]};
    }
    public static object Step(int count=12)
    {
        if(pending==null)throw new Exception("Begin first.");
        int end=Math.Min(next+count,pending.Count);
        AssetDatabase.StartAssetEditing();
        try
        {
            for(;next<end;next++)
            {
                var d=pending[next];string name=(string)d["name"],group=(string)d["group"];
                if(!existing.TryGetValue(group,out var parent)){parent=new GameObject(group);parent.transform.SetParent(root.transform,false);existing[group]=parent;}
                if(!existing.TryGetValue(name,out var go)){go=new GameObject(name);go.transform.SetParent(parent.transform,false);existing[name]=go;}
                if(d["position"]!=null)go.transform.localPosition=V(d["position"]);
                string path=Folder+"/"+prefix+" "+next.ToString("0000")+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh){mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;
                mesh.vertices=d["vertices"].Select(V).ToArray();mesh.colors=d["colors"].Select(C).ToArray();mesh.uv=d["uv"].Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray();
                mesh.subMeshCount=d["triangles"].Count();for(int i=0;i<mesh.subMeshCount;i++)mesh.SetTriangles(d["triangles"][i].Values<int>().ToArray(),i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var filter=go.GetComponent<MeshFilter>();if(!filter)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();if(!renderer)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=d["materials"].Select(k=>materials[(string)k]).ToArray();
                var collider=go.GetComponent<MeshCollider>();
                if((bool?)d["collider"]==true){if(!collider)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;}
                else if(collider)collider.enabled=false;
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        return new{next,total=pending.Count,done=next==pending.Count};
    }
    public static object Finish()
    {
        if(next!=pending.Count)throw new Exception("Finish all detail batches first.");
        foreach(var name in plan["disable"])if(existing.TryGetValue((string)name,out var g))g.SetActive(false);
        var parent=existing["Port of Banago terminal"].transform;
        var sign=parent.Find("PORT OF BANAGO sign");
        if(!sign){var go=new GameObject("PORT OF BANAGO sign");sign=go.transform;sign.SetParent(parent,false);}
        sign.position=new Vector3(-167,8.35f,-57.43f);sign.rotation=Quaternion.Euler(0,180,0);
        var text=sign.GetComponent<TextMesh>();if(!text)text=sign.gameObject.AddComponent<TextMesh>();
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.font=font;text.text="PORT OF BANAGO";text.fontSize=64;text.fontStyle=FontStyle.Bold;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.28f;text.color=new Color(.04f,.17f,.33f);
        var lettering=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Port lettering.mat");if(!lettering){lettering=new Material(Shader.Find("StormSim/World Sign Text"));AssetDatabase.CreateAsset(lettering,Folder+"/Port lettering.mat");}
        font.RequestCharactersInTexture(text.text,64,FontStyle.Bold);lettering.mainTexture=font.material.mainTexture;text.GetComponent<Renderer>().sharedMaterial=lettering;
        var signs=parent.GetComponent<BanagoWorldSigns>();if(!signs)signs=parent.gameObject.AddComponent<BanagoWorldSigns>();signs.lettering=lettering;
        var problems=new List<string>();long tris=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>())
        {
            if(!f.sharedMesh){problems.Add(f.name+": missing mesh");continue;}
            var m=f.sharedMesh;if(m.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))problems.Add(f.name+": invalid vertex");
            if(f.GetComponent<Renderer>().sharedMaterials.Any(mat=>!mat||!mat.shader||ShaderUtil.ShaderHasError(mat.shader)))problems.Add(f.name+": material/shader error");tris+=m.triangles.Length/3;
        }
        if(problems.Count>0)throw new Exception(string.Join("; ",problems.Take(10)));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);bool saved=EditorSceneManager.SaveScene(root.scene);
        var result=new{saved,triangles=tris,summary=plan["summary"],errors=problems};File.WriteAllText(Source+"/"+prefix.ToLowerInvariant()+"-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        pending=null;plan=null;return result;
    }
    [MenuItem("Tools/StormSim/Review four lane highway")]
    public static void ReviewHighway(){Frame(new Vector3(135,1,-450),Quaternion.Euler(15,162,0),40);}
    static void Frame(Vector3 p,Quaternion rot,float size)
    {
        var sv=SceneView.lastActiveSceneView;if(!sv)sv=EditorWindow.GetWindow<SceneView>();
        typeof(SceneView).GetProperty("overrideSceneCullingMask",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.SetValue(sv,(ulong)0);
        sv.sceneViewState.showFog=false;sv.LookAt(p,rot,size,false);sv.Focus();sv.Repaint();
    }
}
