using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BanagoBangkaVariationsBuilder
{
    const string GroupName="Additional Bangka - colors and sizes";
    public static string Status="Not built";
    public static string OutputFolder;
    static Color ColorOf(JToken c)=>new Color((float)c[0],(float)c[1],(float)c[2],1);

    static float SurfaceHeight(Mesh mesh,Vector2 point)
    {
        var v=mesh.vertices;var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            Vector3 a=v[triangles[i]],b=v[triangles[i+1]],c=v[triangles[i+2]];
            if(point.x<Mathf.Min(a.x,Mathf.Min(b.x,c.x)) || point.x>Mathf.Max(a.x,Mathf.Max(b.x,c.x)) ||
               point.y<Mathf.Min(a.z,Mathf.Min(b.z,c.z)) || point.y>Mathf.Max(a.z,Mathf.Max(b.z,c.z)))continue;
            float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Mathf.Abs(det)<1e-8f)continue;
            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.y-c.z))/det;
            float w=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.y-c.z))/det;
            if(u>=-.0001f && w>=-.0001f && u+w<=1.0001f)return u*a.y+w*b.y+(1-u-w)*c.y;
        }
        throw new InvalidOperationException("The boat position is outside the water surface: "+point);
    }

    [MenuItem("Tools/StormSim/Add Color and Size Bangka Variations")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before adding boats.");
        var coast=GameObject.Find("Banago Coast");if(!coast)throw new InvalidOperationException("Banago Coast is missing.");
        if(coast.transform.Find(GroupName))throw new InvalidOperationException("The additional bangka group already exists.");
        var data=JObject.Parse(File.ReadAllText("RoadRepairBackup/bangka-variations-plan.json"));
        if((int)data["summary"]["violations"]!=0)throw new InvalidOperationException("Boat placement did not pass clearance checks.");
        var water=coast.transform.Find("Calm water - traced sea boundary").GetComponent<MeshFilter>().sharedMesh;
        var material=coast.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Bangka")).sharedMaterial;
        // Resolve every water elevation before creating any scene objects.
        var boats=data["boats"].ToArray();var levels=boats.Select(b=>SurfaceHeight(water,new Vector2((float)b["x"],(float)b["z"]))).ToArray();
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");EditorSceneManager.SaveScene(coast.scene);
        File.Copy(coast.scene.path,"RoadRepairBackup/MAIN_GAME_before_extra_bangka_"+stamp+".unity");
        OutputFolder="Assets/StormSim/Generated/BangkaVariations_"+stamp;Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add six bangka in different colors and sizes");
        var group=new GameObject(GroupName);group.transform.SetParent(coast.transform,false);
        Undo.RegisterCreatedObjectUndo(group,"Add six bangka in different colors and sizes");
        AssetDatabase.StartAssetEditing();
        try
        {
            for(int i=0;i<boats.Length;i++)
            {
                var b=boats[i];string name=(string)b["name"];float length=(float)b["length"];
                var mesh=BanagoBangkaModel.CreateVariant(name+" bangka",ColorOf(b["hull"]),ColorOf(b["accent"]),ColorOf(b["trim"]),ColorOf(b["canopy"]),ColorOf(b["timber"]));
                if(mesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))throw new InvalidOperationException("Boat mesh contains invalid vertices.");
                AssetDatabase.CreateAsset(mesh,OutputFolder+"/"+name+".asset");
                var boat=new GameObject("Bangka - "+name+" - "+length.ToString("F1")+"m");boat.transform.SetParent(group.transform,false);
                boat.transform.localPosition=new Vector3((float)b["x"],levels[i]-.005f,(float)b["z"]);
                boat.transform.localRotation=Quaternion.Euler(0,(float)b["yaw"],0);boat.transform.localScale=Vector3.one*(length/6.6f);
                boat.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=boat.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                boat.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            File.Copy("RoadRepairBackup/bangka-variations-plan.json",OutputFolder+"/PlacementValidation.json");
            File.WriteAllText(OutputFolder+"/README.txt","Six user-requested additional bangka, 5.2 to 8.8 metres long, in cobalt blue, sea green, faded red, turquoise, wood/blue and cream/orange finishes. Full hull and outrigger envelopes checked against the sea mask, houses, roads and existing boats. Positions are authored additions near the existing northern anchorage, not new satellite detections. Existing boats and their shared meshes/materials are unchanged. Hide Additional Bangka - colors and sizes to remove this batch.\n");
        }
        catch{Undo.RevertAllDownToGroup(undo);throw;}
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(coast.scene);EditorSceneManager.SaveScene(coast.scene);
        Selection.activeGameObject=group;
        var view=SceneView.lastActiveSceneView;if(view){view.orthographic=false;view.LookAtDirect(new Vector3(-155,-7.5f,1358),Quaternion.LookRotation(new Vector3(45,-38,60)),53);view.showGrid=false;view.Repaint();}
        Status="Added 6 bangka, 5.2m to 8.8m, with six color schemes. "+OutputFolder;
    }

    public static void CaptureReview()
    {
        Capture("RoadRepairBackup/bangka-color-size-review.png",new Vector3(-153,-7.4f,1359),new Vector3(-43,44,-60),1600,1000);
        Capture("RoadRepairBackup/bangka-color-size-detail.png",new Vector3(-158,-7.2f,1324),new Vector3(-11,6.2f,-10),1400,900);
        Status="Bangka overview and color detail captured";
    }
    static void Capture(string path,Vector3 target,Vector3 offset,int width,int height)
    {
        var go=new GameObject("Bangka review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();
        camera.transform.position=target+offset;camera.transform.LookAt(target);camera.fieldOfView=50;camera.nearClipPlane=.1f;camera.farClipPlane=3000;
        camera.clearFlags=CameraClearFlags.Skybox;var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.requiresColorTexture=true;extra.requiresDepthTexture=true;
        var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
