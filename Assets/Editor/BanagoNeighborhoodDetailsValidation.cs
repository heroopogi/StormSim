using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>Checks and finishing adjustments confined to the new detail group.</summary>
public static class BanagoNeighborhoodDetailsValidation
{
    public static string FinishNewDetails()
    {
        var root=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName);
        if(!root)throw new Exception("No neighborhood additions to finish");
        foreach(var t in root.GetComponentsInChildren<TextMesh>())
        {
            Undo.RecordObject(t,"Fit new neighborhood sign");
            if(t.text=="SARI-SARI")t.characterSize=.045f;
            else if(t.text.StartsWith("LOAD"))t.characterSize=.021f;
            else if(t.text=="BANAGO - BACOLOD")t.characterSize=.022f;
            else if(t.transform.parent.name.StartsWith("tricycle"))t.characterSize=.040f;
            EditorUtility.SetDirty(t);
        }
        foreach(var lod in root.GetComponentsInChildren<LODGroup>())
        {
            Undo.RecordObject(lod,"Include new signs in distance culling");
            lod.SetLODs(new[]{new LOD(lod.name.StartsWith("flowers")?.012f:.0025f,lod.GetComponentsInChildren<Renderer>())});
            lod.RecalculateBounds();EditorUtility.SetDirty(lod);
        }
        // Early build's outer post caps exceeded the reservation by 55 mm.
        // Only our new shared frontage mesh assets can be adjusted here.
        int tightened=0;
        foreach(var mesh in root.transform.Find("frontage").GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Distinct())
        {
            string path=AssetDatabase.GetAssetPath(mesh);
            if(!path.StartsWith("Assets/StormSim/Generated/NeighborhoodLife_"))throw new Exception("Foreign mesh refused");
            if(mesh.bounds.size.x<=4.70f)continue;
            var vertices=mesh.vertices;float factor=4.69f/mesh.bounds.size.x;
            for(int i=0;i<vertices.Length;i++)vertices[i].x*=factor;
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);tightened++;
        }
        foreach(var c in root.transform.Find("frontage").GetComponentsInChildren<MeshCollider>())
        {var m=c.sharedMesh;c.sharedMesh=null;c.sharedMesh=m;}
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return "Fitted labels, included signs in LOD culling, tightened "+tightened+" new mesh variants";
    }

    public static string CheckGroundContact()
    {
        var root=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName);
        var plan=JObject.Parse(File.ReadAllText("RoadRepairBackup/neighborhood-detail-plan.json"));
        var terrain=root.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.name.StartsWith("Measured terrain")).ToArray();
        var rows=new List<object>();int i=0;
        foreach(var item in plan["items"])
        {
            i++;string kind=(string)item["kind"];if(kind=="shoreDebris")continue;
            var t=root.transform.Find(kind).Cast<Transform>().First(g=>g.name.StartsWith(kind+" "+i.ToString("000")));
            float width=(float)item["width"],depth=(float)item["depth"];var gaps=new List<float>();
            foreach(float x in new[]{-width*.40f,width*.40f})foreach(float z in new[]{-depth*.40f,depth*.40f})
            {
                var p=t.TransformPoint(new Vector3(x,0,z));
                foreach(var c in terrain)if(c.Raycast(new Ray(p+Vector3.up*50,Vector3.down),out var hit,100)){gaps.Add(p.y-hit.point.y);break;}
            }
            rows.Add(new{name=t.name,kind=kind,samples=gaps.Count,min=gaps.Count>0?gaps.Min():999,max=gaps.Count>0?gaps.Max():999});
        }
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(rows);
        File.WriteAllText("RoadRepairBackup/neighborhood-ground-contact.json",json);
        return "Measured contact at "+rows.Count+" new placements";
    }

    public static string FitToSlopesAndShore()
    {
        var root=GameObject.Find(BanagoNeighborhoodDetailsBuilder.RootName);
        if(!root)throw new Exception("No new detail group");
        var terrain=root.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>()).Where(c=>c.name.StartsWith("Measured terrain")||c.name=="Shallow sand seabed").ToArray();
        Func<Vector3,float?> height=p=>
        {
            float? y=null;foreach(var c in terrain)
                if(c.bounds.Contains(new Vector3(p.x,c.bounds.center.y,p.z))&&c.Raycast(new Ray(p+Vector3.up*50,Vector3.down),out var hit,100))
                    y=y.HasValue?Mathf.Max(y.Value,hit.point.y):hit.point.y;
            return y;
        };
        var plan=JObject.Parse(File.ReadAllText("RoadRepairBackup/neighborhood-detail-plan.json"));int i=0,fitted=0,footings=0;
        string folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(root.transform.Find("Wider rough sand - existing water retained").GetComponent<MeshFilter>().sharedMesh)).Replace('\\','/');
        foreach(var item in plan["items"])
        {
            i++;string kind=(string)item["kind"];if(kind=="shoreDebris")continue;
            var t=root.transform.Find(kind).Cast<Transform>().First(g=>g.name.StartsWith(kind+" "+i.ToString("000")));
            float width=(float)item["width"],depth=(float)item["depth"];
            if(kind=="car"||kind=="tricycle"||kind=="jeepney"||kind=="flowers"||kind=="drainage")
            {
                var yaw=Quaternion.Euler(0,(float)item["yaw"],0);var points=new List<Vector3>();
                foreach(float x in new[]{-width*.4f,width*.4f})foreach(float z in new[]{-depth*.4f,depth*.4f})
                {var p=t.position+yaw*new Vector3(x,0,z);var h=height(p);if(h.HasValue)points.Add(new Vector3(p.x,h.Value,p.z));}
                if(points.Count==4)
                {
                    var right=(points[2]+points[3]-points[0]-points[1])*.5f;
                    var forward=(points[1]+points[3]-points[0]-points[2])*.5f;
                    var normal=Vector3.Cross(forward,right).normalized;
                    Undo.RecordObject(t,"Fit new prop to ground slope");
                    t.rotation=Quaternion.LookRotation(Vector3.Cross(right,normal),normal);
                    t.position=new Vector3(t.position.x,points.Average(p=>p.y)+.007f,t.position.z);fitted++;
                }
            }
            if((kind=="frontage"||kind=="store")&&!t.Find("Ground-following foundation"))
            {
                var parts=new BanagoEnvironmentModels.Parts();
                Action<float,float,float,float> slab=(x0,x1,z0,z1)=>
                {
                    var top=new[]{new Vector3(x0,.15f,z0),new Vector3(x1,.15f,z0),new Vector3(x1,.15f,z1),new Vector3(x0,.15f,z1)};
                    var bottom=top.Select(p=>{var w=t.TransformPoint(p);float y=height(w)??(t.position.y-.25f);return t.InverseTransformPoint(new Vector3(w.x,Mathf.Min(y-.035f,t.position.y-.025f),w.z));}).ToArray();
                    var color=new Color(.43f,.42f,.36f);
                    for(int k=0;k<4;k++){int n=(k+1)%4;var normal=Vector3.Cross(top[n]-top[k],bottom[n]-top[k]).normalized;parts.Face(top[k],top[n],bottom[n],color,normal);parts.Face(top[k],bottom[n],bottom[k],color,normal);}
                    parts.Face(top[0],top[2],top[1],color,Vector3.up);parts.Face(top[0],top[3],top[2],color,Vector3.up);
                };
                if(kind=="store")slab(-1.25f,1.25f,-.825f,.825f);
                else {slab(-2.345f,-.685f,-.43f,-.015f);slab(.685f,2.345f,-.43f,-.015f);}
                var mesh=parts.Mesh("Fitted new foundation "+i);AssetDatabase.CreateAsset(mesh,folder+"/Foundation "+i+".asset");
                var g=new GameObject("Ground-following foundation");g.transform.SetParent(t,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=t.Find("masonry").GetComponent<MeshRenderer>().sharedMaterial;g.AddComponent<MeshCollider>().sharedMesh=mesh;Undo.RegisterCreatedObjectUndo(g,"Add new prop footing");footings++;
            }
        }
        // Match the retained seabed/ground so the added sand cannot float or disappear below it.
        var shore=root.transform.Find("Wider rough sand - existing water retained");var sm=shore.GetComponent<MeshFilter>().sharedMesh;
        if(!AssetDatabase.GetAssetPath(sm).StartsWith("Assets/StormSim/Generated/NeighborhoodLife_"))throw new Exception("Foreign shoreline mesh refused");
        var vertices=sm.vertices;var colors=new Color[vertices.Length];
        for(int k=0;k<vertices.Length;k++)
        {
            var v=vertices[k];var ground=height(shore.TransformPoint(v));
            if(ground.HasValue)v.y=ground.Value+.06f+.10f*Mathf.PerlinNoise(v.x*.21f,v.z*.21f);
            vertices[k]=v;float noise=Mathf.PerlinNoise(v.x*.47f+20,v.z*.47f)*.17f-.085f;
            colors[k]=new Color(.51f+noise,.46f+noise,.36f+noise);
        }
        sm.vertices=vertices;sm.colors=colors;sm.RecalculateNormals();sm.RecalculateBounds();sm.RecalculateTangents();EditorUtility.SetDirty(sm);
        var sc=shore.GetComponent<MeshCollider>();sc.sharedMesh=null;sc.sharedMesh=sm;
        foreach(Transform d in root.transform.Find("shoreDebris"))
            if(sc.Raycast(new Ray(d.position+Vector3.up*50,Vector3.down),out var hit,100))d.position=new Vector3(d.position.x,hit.point.y+.025f,d.position.z);
        foreach(var lod in root.GetComponentsInChildren<LODGroup>())
        {lod.SetLODs(new[]{new LOD(lod.name.StartsWith("flowers")?.012f:.0025f,lod.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();}
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return "Slope-fitted "+fitted+" new props; added "+footings+" fitted foundations; joined new sand to retained seabed";
    }
}
