using System;
using System.IO;
using System.Linq;
using CesiumForUnity;
using UnityEditor;
using UnityEngine;

public static class BanagoCoastSurvey
{
    static Camera camera;
    static bool originalActive;
    static float originalError;
    public static string Status="Not started";
    public static void Begin()
    {
        var geo=GameObject.Find("CesiumGeoreference");var terrain=geo.transform.Find("Cesium World Terrain").gameObject;
        originalActive=terrain.activeSelf;terrain.SetActive(true);var tiles=terrain.GetComponent<Cesium3DTileset>();
        originalError=tiles.maximumScreenSpaceError;tiles.maximumScreenSpaceError=2;
        var go=new GameObject("Coastal image inspection"){hideFlags=HideFlags.HideAndDontSave};camera=go.AddComponent<Camera>();
        camera.transform.position=geo.transform.TransformPoint(new Vector3(-160,400,1280));camera.transform.rotation=Quaternion.Euler(90,0,0);
        camera.orthographic=true;camera.orthographicSize=135;camera.aspect=1;camera.nearClipPlane=.3f;camera.farClipPlane=1500;
        camera.targetTexture=new RenderTexture(4096,4096,24);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=~0;
        geo.GetComponent<CesiumCameraManager>().additionalCameras.Add(camera);Status="Loading close coastal imagery";
    }
    public static void Capture()
    {
        if(!camera)throw new InvalidOperationException("Start coastal survey first.");
        var terrain=GameObject.Find("CesiumGeoreference").transform.Find("Cesium World Terrain").gameObject;
        var rs=terrain.GetComponentsInChildren<Renderer>();var layers=rs.Select(r=>r.gameObject.layer).ToArray();
        Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            for(int i=0;i<rs.Length;i++)rs[i].gameObject.layer=30;
            camera.cullingMask=1<<30;
            camera.Render();RenderTexture.active=camera.targetTexture;image=new Texture2D(4096,4096,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,4096,4096),0,0);image.Apply();File.WriteAllBytes("RoadRepairBackup/coast-north-detail-survey.png",image.EncodeToPNG());
            Status="Captured coastal imagery: centre (-160,1280), width 270m, north up.";
        }
        finally
        {
            RenderTexture.active=previous;for(int i=0;i<rs.Length;i++)if(rs[i])rs[i].gameObject.layer=layers[i];
            if(image)UnityEngine.Object.DestroyImmediate(image);GameObject.Find("CesiumGeoreference").GetComponent<CesiumCameraManager>().additionalCameras.Remove(camera);
            var rt=camera.targetTexture;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);camera=null;
            terrain.GetComponent<Cesium3DTileset>().maximumScreenSpaceError=originalError;terrain.SetActive(originalActive);
        }
    }
}
