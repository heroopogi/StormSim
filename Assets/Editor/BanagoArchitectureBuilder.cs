using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Imports new architecture as reversible mesh substitutions, never overwriting originals.</summary>
public static class BanagoArchitectureBuilder
{
    public const string RootName="Banago Architecture - roadside homes and public buildings";
    public static string Status="Not started";
    public static string Folder;
    static JArray batches;
    static JObject civic;
    static GameObject root;
    static int next;
    static BanagoArchitectureReview review;
    static List<MeshFilter> targets;
    static List<Mesh> originals,replacements;
    static Dictionary<string,Material> materials;

    public static void Start()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");
        if(GameObject.Find(RootName))throw new Exception("Architecture group already exists; refusing duplicate import");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MAIN_GAME.unity")throw new Exception("Unexpected scene");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if(!EditorSceneManager.SaveScene(scene,"RoadRepairBackup/MAIN_GAME_before_architecture_import_"+stamp+".unity",true))throw new Exception("Backup could not be saved");
        var manifest=JObject.Parse(File.ReadAllText("RoadRepairBackup/roadside-house-manifest.json"));
        batches=(JArray)manifest["batches"];civic=JObject.Parse(File.ReadAllText("RoadRepairBackup/public-school-hall-data.json"));
        var houses=GameObject.Find("Banago Houses - full detail redesign");
        if(!houses||houses.transform.childCount!=batches.Count)throw new Exception("Original house batch layout does not match");
        foreach(var b in batches)if(!houses.transform.Find((string)b["name"]))throw new Exception("Missing house neighbourhood "+b["name"]);
        Folder="Assets/StormSim/Generated/BanagoArchitecture_"+stamp;Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        root=new GameObject(RootName);root.transform.SetParent(GameObject.Find("CesiumGeoreference").transform,false);Undo.RegisterCreatedObjectUndo(root,"Build Banago architecture review");
        review=root.AddComponent<BanagoArchitectureReview>();review.notes="Disable this root to restore retained house/civic mesh assets and old civic signs. No existing object or original mesh asset was deleted. Public-school style is representative, not an exact surveyed replica. "+manifest["summary"];
        targets=new List<MeshFilter>();originals=new List<Mesh>();replacements=new List<Mesh>();next=0;
        materials=new Dictionary<string,Material>();
        foreach(string kind in new[]{"plaster","metal","details"})
        {
            var mat=new Material(Shader.Find("StormSim/Weathered Building Surface")){name="Public buildings "+kind,enableInstancing=true};
            mat.SetFloat("_Corrugation",kind=="metal"?1:0);mat.SetFloat("_Weathering",kind=="metal"?.25f:.12f);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",kind=="metal"?.15f:0);mat.SetFloat("_Cull",0);AssetDatabase.CreateAsset(mat,Folder+"/Civic "+kind+".mat");materials[kind]=mat;
        }
        File.WriteAllText(Folder+"/House summary.json",manifest["summary"].ToString());File.WriteAllText(Folder+"/Civic summary.json",civic["summary"].ToString());
        Status="Importing 103 house neighbourhoods into new assets";EditorApplication.update+=Tick;
    }

    static Mesh ReadHouseMesh(string path,string name)
    {
        using(var r=new BinaryReader(File.OpenRead(path)))
        {
            int n=r.ReadInt32();var v=new Vector3[n];var ns=new Vector3[n];var uv=new Vector2[n];var cs=new Color[n];var ts=new int[n];
            for(int i=0;i<n;i++){v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());ns[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());cs[i]=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());ts[i]=i;if(!float.IsFinite(v[i].x)||!float.IsFinite(v[i].y)||!float.IsFinite(v[i].z))throw new Exception("Invalid house vertex");}
            var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.vertices=v;m.normals=ns;m.uv=uv;m.colors=cs;m.triangles=ts;m.RecalculateBounds();return m;
        }
    }
    static Mesh ReadCivicMesh(JToken d)
    {
        var m=new Mesh{name=(string)d["name"],indexFormat=IndexFormat.UInt32};
        m.vertices=d["vertices"].Select(t=>new Vector3((float)t[0],(float)t[1],(float)t[2])).ToArray();m.normals=d["normals"].Select(t=>new Vector3((float)t[0],(float)t[1],(float)t[2])).ToArray();m.uv=d["uv"].Select(t=>new Vector2((float)t[0],(float)t[1])).ToArray();m.colors=d["colors"].Select(t=>new Color((float)t[0],(float)t[1],(float)t[2])).ToArray();m.triangles=d["triangles"].Values<int>().ToArray();m.RecalculateBounds();return m;
    }
    static void AddSwap(MeshFilter target,Mesh replacement)
    {
        if(targets.Contains(target))throw new Exception("Duplicate mesh substitution");
        targets.Add(target);originals.Add(target.sharedMesh);replacements.Add(replacement);
        // Persist the mapping after each batch; no mutation to the old mesh assets.
        review.targets=targets.ToArray();review.originals=originals.ToArray();review.replacements=replacements.ToArray();EditorUtility.SetDirty(review);
    }
    static void Tick()
    {
        try
        {
            if(!root)throw new Exception("New architecture root is unavailable");
            if(next<batches.Count)
            {
                var b=batches[next];var parent=GameObject.Find("Banago Houses - full detail redesign").transform.Find((string)b["name"]);bool first=true;string path=Folder+"/Neighbourhood "+next+".asset";
                foreach(var d in b["meshes"])
                {
                    var target=parent.Find((string)d["name"]).GetComponent<MeshFilter>();
                    var m=ReadHouseMesh((string)d["file"],parent.name+" roadside "+(string)d["kind"]);
                    if(first){AssetDatabase.CreateAsset(m,path);first=false;}else AssetDatabase.AddObjectToAsset(m,path);
                    AddSwap(target,m);
                }
                next++;Status="Prepared neighbourhood "+next+" / "+batches.Count;return;
            }
            EditorApplication.update-=Tick;BuildCivic();AddUpperFloorSlabs();
            foreach(var f in targets){Undo.RecordObject(f,"Apply reviewed building mesh");var c=f.GetComponent<MeshCollider>();if(c)Undo.RecordObject(c,"Apply reviewed building collider");}
            review.Apply();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);Selection.activeGameObject=root;
            Status="Built: 2257 roadside houses, 325 new second floors, five public-school blocks and two hall buildings; awaiting validation";
        }
        catch(Exception e){EditorApplication.update-=Tick;Status="Stopped: "+e.Message;Debug.LogException(e);}
    }
    static bool Inside(Vector3 p,JToken polygon)
    {
        bool inside=false;int n=polygon.Count();
        for(int i=0,j=n-1;i<n;j=i++)
        {
            float ax=(float)polygon[i][0],az=(float)polygon[i][1],bx=(float)polygon[j][0],bz=(float)polygon[j][1];
            var ab=new Vector2(bx-ax,bz-az);var ap=new Vector2(p.x-ax,p.z-az);float t=Mathf.Clamp01(Vector2.Dot(ap,ab)/Mathf.Max(.00001f,ab.sqrMagnitude));
            if((ap-ab*t).sqrMagnitude<.01f)return true;
            if((az>p.z)!=(bz>p.z)&&p.x<(bx-ax)*(p.z-az)/(bz-az)+ax)inside=!inside;
        }
        return inside;
    }
    static void BuildCivic()
    {
        var community=GameObject.Find("Banago Community - streets civic buildings and pier");
        var filters=community.transform.Find("Street reference test - patchwork homes").GetComponentsInChildren<MeshFilter>().ToList();
        filters.Add(community.transform.Find("Shops posters and parked motorcycles/Shop signs and notice boards").GetComponent<MeshFilter>());
        int index=0;
        foreach(var f in filters)
        {
            var original=f.sharedMesh;var m=UnityEngine.Object.Instantiate(original);m.name=original.name+" - retained outside civic upgrades";var vs=original.vertices;int removed=0;
            for(int s=0;s<original.subMeshCount;s++)
            {
                var ts=original.GetTriangles(s);var keep=new List<int>(ts.Length);
                for(int i=0;i<ts.Length;i+=3)
                {
                    var center=f.transform.TransformPoint((vs[ts[i]]+vs[ts[i+1]]+vs[ts[i+2]])/3);
                    if(civic["retireMasks"].Any(poly=>Inside(center,poly))){removed++;continue;}
                    keep.Add(ts[i]);keep.Add(ts[i+1]);keep.Add(ts[i+2]);
                }
                m.SetTriangles(keep,s);
            }
            AssetDatabase.CreateAsset(m,Folder+"/Retained community "+index+++".asset");AddSwap(f,m);
            if(removed==0)throw new Exception("Civic target did not intersect the verified building masks: "+f.name);
        }
        var signs=community.GetComponentsInChildren<TextMesh>().Where(t=>t.text=="BANAGO PEOPLE'S HALL"||t.text=="BANAGO ELEMENTARY"||t.text=="CLASSROOMS").Select(t=>t.GetComponent<Renderer>()).ToArray();
        review.oldSigns=signs;review.signEnabled=signs.Select(r=>r.enabled).ToArray();
        var groups=new Dictionary<string,Transform>();index=0;
        foreach(var d in civic["meshes"])
        {
            string name=(string)d["group"];if(!groups.TryGetValue(name,out var group)){var g=new GameObject(name);g.transform.SetParent(root.transform,false);group=g.transform;groups[name]=group;}
            var go=new GameObject((string)d["name"]);go.transform.SetParent(group,false);var m=ReadCivicMesh(d);AssetDatabase.CreateAsset(m,Folder+"/Civic mesh "+index+++".asset");go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=materials[(string)d["material"]];if((bool)d["collider"])go.AddComponent<MeshCollider>().sharedMesh=m;
        }
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture(string.Join("",civic["labels"].Select(t=>(string)t["text"])),64);
        var lettering=new Material(Shader.Find("StormSim/World Sign Text")){name="Public building lettering"};lettering.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(lettering,Folder+"/Lettering.mat");root.AddComponent<BanagoWorldSigns>().lettering=lettering;
        foreach(var d in civic["labels"])
        {
            var go=new GameObject((string)d["text"]);go.transform.SetParent(groups[(string)d["group"]],false);var p=d["position"];go.transform.localPosition=new Vector3((float)p[0],(float)p[1],(float)p[2]);go.transform.localRotation=Quaternion.Euler(0,(float)d["yaw"],0);
            var t=go.AddComponent<TextMesh>();t.text=(string)d["text"];t.font=font;t.fontSize=64;t.characterSize=(float)d["size"];t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.07f,.23f,.32f);go.GetComponent<MeshRenderer>().sharedMaterial=lettering;go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
    }

    public static string AddUpperFloorSlabs()
    {
        var current=GameObject.Find(RootName);if(!current)throw new Exception("No architecture group");
        if(current.transform.Find("House upper-floor slabs"))return "Upper-floor slabs already present";
        var control=current.GetComponent<BanagoArchitectureReview>();
        string folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(control.replacements[0])).Replace('\\','/');
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/architecture-floor-slabs.json"));var m=ReadCivicMesh(data["mesh"]);
        AssetDatabase.CreateAsset(m,folder+"/House upper-floor slabs.asset");var go=new GameObject("House upper-floor slabs");go.transform.SetParent(current.transform,false);Undo.RegisterCreatedObjectUndo(go,"Add solid upper-floor slabs");go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Civic plaster.mat");go.AddComponent<MeshCollider>().sharedMesh=m;AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(current.scene);
        return "Solid floor slabs installed for "+(int)data["count"]+" two-storey houses";
    }
}
