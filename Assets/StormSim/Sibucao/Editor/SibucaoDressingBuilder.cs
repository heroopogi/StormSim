using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace StormSim.Sibucao.Editor
{
    [CustomEditor(typeof(SibucaoDressing))]
    public sealed class SibucaoDressingInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();if(GUILayout.Button("Rebake this seed"))SibucaoDressingBuilder.Bake((SibucaoDressing)target);}
    }
    public static class SibucaoDressingBuilder
    {
        const string Folder="Assets/StormSim/Sibucao/Art/Dressing";
        static readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
        static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        public static string Install()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
            if(!s||s.gameObject.scene.name!="SIBUCAO_RAIN"||Application.isPlaying)throw new Exception("Open the Sibucao scene in Edit mode.");
            var cfg=UnityEngine.Object.FindFirstObjectByType<SibucaoDressing>();
            if(!cfg)cfg=new GameObject("Sibucao yard dressing - imported assets").AddComponent<SibucaoDressing>();
            cfg.route=s.route;
            cfg.householdPrefabs=new[]{
                "Nature/Potted Plants/Prefabs/pot plant.prefab",
                "Containers/Crates and Boxes/Prefabs/Crate_2.prefab",
                "Containers/Buckets/Prefabs/Fish_Market_Bucket_With_Fish.prefab",
                "Nature/Firewood/Prefabs/SM_FIREWOOD_LAYING.prefab",
                "Vehicles/Bicycles/Prefabs/cycle.prefab",
                "Furniture/Chairs/Prefabs/chair (school).prefab"
            }.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Organized Assets/"+p)).ToArray();
            cfg.grassPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Organized Assets/Nature/Grass/Prefabs/LPW_Grass_A1_40cm_01.prefab");
            return Bake(cfg);
        }
        static Material Compatible(Material source)
        {
            if(!source)return source;
            if(source.shader.name.StartsWith("Universal Render Pipeline/")||source.shader.name.StartsWith("Shader Graphs/"))return source;
            if(materials.TryGetValue(source,out var cached))return cached;
            string path=Folder+"/"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".mat";
            var target=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!target)
            {
                target=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                target.name=source.name+" - Sibucao URP";
                if(source.HasProperty("_MainTex")){target.SetTexture("_BaseMap",source.GetTexture("_MainTex"));target.SetTextureScale("_BaseMap",source.GetTextureScale("_MainTex"));target.SetTextureOffset("_BaseMap",source.GetTextureOffset("_MainTex"));}
                if(source.HasProperty("_Color"))target.SetColor("_BaseColor",source.GetColor("_Color"));
                if(source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap")){target.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));target.EnableKeyword("_NORMALMAP");}
                target.SetFloat("_Smoothness",source.HasProperty("_Glossiness")?source.GetFloat("_Glossiness"):.15f);
                target.SetFloat("_Cull",0);target.enableInstancing=true;
                AssetDatabase.CreateAsset(target,path);
            }
            materials[source]=target;return target;
        }
        static bool Ground(Vector3 p,out Vector3 ground)
        {
            ground=default;
            var hits=Physics.RaycastAll(p+Vector3.up*15,Vector3.down,30,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var h in hits)
            {
                if(h.collider.name.IndexOf("terrain",StringComparison.OrdinalIgnoreCase)<0)continue;
                if(h.normal.y<.92f)return false;
                ground=h.point;return true;
            }
            return false;
        }
        static bool Place(GameObject prefab,Vector3 p,float yaw,float scale,Transform parent,bool solid)
        {
            if(!prefab||!Ground(p,out p))return false;
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(parent,false);
            g.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));g.transform.localScale*=scale;
            foreach(var c in g.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            foreach(var rb in g.GetComponentsInChildren<Rigidbody>())UnityEngine.Object.DestroyImmediate(rb);
            var b=BoundsOf(g);g.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y+.025f,p.z-b.center.z);b=BoundsOf(g);
            // Reject roofs, walls, foliage trunks, vehicles and previous placements. Leave roads/door access clear.
            var box=Physics.OverlapBox(b.center+Vector3.up*.06f,new Vector3(b.extents.x+.12f,Mathf.Max(.06f,b.extents.y-.06f),b.extents.z+.12f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(box.Any(c=>!c.transform.IsChildOf(g.transform)&&c.name.IndexOf("terrain",StringComparison.OrdinalIgnoreCase)<0)) {UnityEngine.Object.DestroyImmediate(g);return false;}
            foreach(var r in g.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(Compatible).ToArray();if(!solid)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            if(solid){var c=g.AddComponent<BoxCollider>();c.center=g.transform.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/g.transform.lossyScale.x,b.size.y/g.transform.lossyScale.y,b.size.z/g.transform.lossyScale.z);}
            if(!g.GetComponent<LODGroup>()){var lod=g.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(solid?.008f:.025f,g.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();}
            Physics.SyncTransforms();return true;
        }
        public static string Bake(SibucaoDressing cfg)
        {
            if(Application.isPlaying)throw new Exception("Bake in Edit mode.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/StormSim/Sibucao/Art","Dressing");materials.Clear();
            var old=cfg.transform.Find("Baked yards");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var root=new GameObject("Baked yards").transform;root.SetParent(cfg.transform,false);Undo.RegisterCreatedObjectUndo(root.gameObject,"Bake Sibucao yards");
            Physics.SyncTransforms();var rng=new System.Random(cfg.seed);int count=0,yards=0;
            var vehicles=UnityEngine.Object.FindObjectsByType<SibucaoVehicle>(FindObjectsSortMode.None);
            var encounters=UnityEngine.Object.FindObjectsByType<SibucaoEncounter>(FindObjectsSortMode.None);
            var houses=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,"^House [0-9]+$")).OrderBy(t=>t.name).OrderBy(t=>rng.Next()).ToArray();
            foreach(var house in houses)
            {
                var progress=cfg.route.Progress(house.position);var nearest=cfg.route.Sample(progress);
                var delta=house.position-nearest;delta.y=0;
                if(progress>cfg.routeEnd||delta.magnitude>cfg.routeBand||house.position.z<cfg.minimumWorldZ)continue;
                var rs=house.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)continue;
                var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                yards++;
                for(int i=0;i<cfg.propsPerYard+cfg.grassPerYard && count<cfg.maximumObjects;i++)
                {
                    bool grass=i>=cfg.propsPerYard;
                    float side=(float)rng.NextDouble()*2-1;
                    bool xSide=rng.Next(2)==0;
                    var offset=xSide?new Vector3((rng.Next(2)==0?-1:1)*(b.extents.x+.8f+(float)rng.NextDouble()),0,side*b.extents.z):new Vector3(side*b.extents.x,0,(rng.Next(2)==0?-1:1)*(b.extents.z+.8f+(float)rng.NextDouble()));
                    Vector3 p=b.center+offset;p.y=house.position.y;
                    var path=cfg.route.Sample(cfg.route.Progress(p));var d=p-path;d.y=0;
                    if(d.magnitude<7||vehicles.Any(v=>Vector3.Distance(v.transform.position,p)<6)||encounters.Any(e=>Vector3.Distance(e.transform.position,p)<13))continue;
                    // Preserve the middle of each facade as an access lane.
                    if(Mathf.Abs(side)<.35f)continue;
                    var prefab=grass?cfg.grassPrefab:cfg.householdPrefabs[rng.Next(cfg.householdPrefabs.Length)];
                    if(Place(prefab,p,(float)rng.NextDouble()*360,grass?.7f+(float)rng.NextDouble()*.7f:.9f+(float)rng.NextDouble()*.2f,root,!grass))count++;
                }
            }
            cfg.lastBake="Seed "+cfg.seed+": "+count+" props across "+yards+" candidate yards; road, vehicle and encounter clearances reserved.";
            EditorUtility.SetDirty(cfg);EditorSceneManager.MarkSceneDirty(cfg.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(cfg.gameObject.scene);return cfg.lastBake;
        }
    }
}

