using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StormSim.Sibucao;

public static class SibucaoNavigationBuilder
{
    const string Folder="Assets/StormSim/Sibucao/Navigation";
    static bool Building(MeshFilter f)
    {
        if(f.GetComponentInParent<SibucaoHomeOpening>()||f.GetComponentInParent<PhysicalGrabbable>())return false;
        string path=AnimationUtility.CalculateTransformPath(f.transform,null).ToLowerInvariant();
        return path.Contains("banago houses - full detail redesign") || path.Contains("street reference test - patchwork homes") ||
            path.Contains("church-side infill houses") || path.Contains("opposite school - shopfronts") ||
            (path.Contains("banago baybayin -") && (f.name.Contains("House")||f.name.Contains("house"))) ||
            (path.Contains("banago reference areas -") && f.name.Contains("House"));
    }
    public static string RepairBuildings()
    {
        if(EditorApplication.isPlaying||SceneManagerPath()!="Assets/Scenes/SIBUCAO_RAIN.unity")throw new Exception("Open SIBUCAO_RAIN outside Play mode.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        int front=0,back=0,triangles=0;
        foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.sharedMesh&&Building(f)))
        {
            var col=f.GetComponent<MeshCollider>();
            if(!f.GetComponent<Collider>()){col=f.gameObject.AddComponent<MeshCollider>();col.sharedMesh=f.sharedMesh;front++;}
            if(f.transform.Find("Solid wall reverse faces"))continue;
            var vertices=f.sharedMesh.vertices;var indices=f.sharedMesh.triangles;var reverse=new List<int>();
            for(int i=0;i<indices.Length;i+=3)
            {
                var n=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]);
                if(n.sqrMagnitude<.000001f||Mathf.Abs(n.normalized.y)>.8f)continue;
                reverse.Add(indices[i+2]);reverse.Add(indices[i+1]);reverse.Add(indices[i]);
            }
            if(reverse.Count==0)continue;
            // Separate reverse-face collider preserves windows/doorways and blocks either approach.
            // Original render and collider meshes remain untouched for architecture review swaps.
            var mesh=new Mesh{name=f.transform.parent.name+" solid wall backs",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=vertices;mesh.triangles=reverse.ToArray();mesh.RecalculateBounds();
            string path=AssetDatabase.GenerateUniqueAssetPath(Folder+"/WallBacks.asset");AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject("Solid wall reverse faces");go.layer=f.gameObject.layer;go.transform.SetParent(f.transform,false);go.AddComponent<MeshCollider>().sharedMesh=mesh;
            triangles+=reverse.Count/3;back++;
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return $"Added {front} missing building colliders and {back} reverse-face colliders ({triangles} triangles).";
    }
    static string SceneManagerPath()=>EditorSceneManager.GetActiveScene().path;
    static Color32[] pixels;static int width,height;static Rect bounds;
    static Vector2 Pixel(Vector3 v)=>new Vector2((v.x-bounds.x)/bounds.width*(width-1),(v.z-bounds.y)/bounds.height*(height-1));
    static float Edge(Vector2 a,Vector2 b,Vector2 p)=>(p.x-a.x)*(b.y-a.y)-(p.y-a.y)*(b.x-a.x);
    static void Triangle(Vector3 va,Vector3 vb,Vector3 vc,Color32 color)
    {
        var a=Pixel(va);var b=Pixel(vb);var c=Pixel(vc);float area=Edge(a,b,c);if(Mathf.Abs(area)<.03f)return;
        int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)))),x1=Mathf.Min(width-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
        int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)))),y1=Mathf.Min(height-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var p=new Vector2(x+.5f,y+.5f);if(Edge(a,b,p)*area>=0&&Edge(b,c,p)*area>=0&&Edge(c,a,p)*area>=0)pixels[y*width+x]=color;}
    }
    public static string BakeMap(string destinationFolder=null)
    {
        string outputFolder=destinationFolder??Folder;
        var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
        Directory.CreateDirectory(outputFolder);AssetDatabase.Refresh();
        float minX=s.route.points.Min(p=>p.x)-180,maxX=s.route.points.Max(p=>p.x)+180,minZ=s.route.points.Min(p=>p.z)-180,maxZ=s.route.points.Max(p=>p.z)+180;
        bounds=Rect.MinMaxRect(minX,minZ,maxX,maxZ);width=1024;height=Mathf.RoundToInt(width*bounds.height/bounds.width);height=Mathf.Min(height,2048);
        pixels=Enumerable.Repeat(new Color32(15,35,43,255),width*height).ToArray();int meshes=0;
        var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        for(int pass=0;pass<2;pass++)foreach(var f in filters)
        {
            if(!f.sharedMesh||!f.GetComponent<Renderer>())continue;
            bool building=Building(f)||f.GetComponentInParent<SibucaoHomeOpening>();
            string path=AnimationUtility.CalculateTransformPath(f.transform,null).ToLowerInvariant();
            bool road=path.Contains("residential ")||path.Contains("road surface")||path.Contains("asphalt")||path.Contains("felix amante drive");
            if(pass==0?!road:!building)continue;
            var bb=f.GetComponent<Renderer>().bounds;if(bb.max.x<bounds.xMin||bb.min.x>bounds.xMax||bb.max.z<bounds.yMin||bb.min.z>bounds.yMax)continue;
            var vs=f.sharedMesh.vertices;var ts=f.sharedMesh.triangles;var matrix=f.transform.localToWorldMatrix;
            var color=pass==0?new Color32(80,110,119,255):new Color32(38,65,74,255);
            for(int i=0;i<ts.Length;i+=3)Triangle(matrix.MultiplyPoint3x4(vs[ts[i]]),matrix.MultiplyPoint3x4(vs[ts[i+1]]),matrix.MultiplyPoint3x4(vs[ts[i+2]]),color);
            meshes++;
        }
        var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);tex.SetPixels32(pixels);tex.Apply();string file=outputFolder+"/SibucaoStreetMap.png";File.WriteAllBytes(file,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(file);
        var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var map=s.GetComponent<SibucaoMinimap>();if(!map)map=s.gameObject.AddComponent<SibucaoMinimap>();map.streetMap=AssetDatabase.LoadAssetAtPath<Texture2D>(file);map.worldBounds=bounds;
        EditorUtility.SetDirty(map);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);AssetDatabase.SaveAssets();
        return $"Baked {width} x {height} map from {meshes} street and building meshes.";
    }
}
