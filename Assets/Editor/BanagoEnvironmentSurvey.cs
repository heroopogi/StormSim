using System;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

public static class BanagoEnvironmentSurvey
{
    public static string Status="Not started";
    static Camera camera;
    static float previousError;
    public static void Begin()
    {
        if(camera)throw new InvalidOperationException("Survey is already running.");
        var geo=GameObject.Find("CesiumGeoreference").GetComponent<CesiumGeoreference>();
        var terrain=GameObject.Find("Cesium World Terrain").GetComponent<Cesium3DTileset>();
        previousError=terrain.maximumScreenSpaceError;
        terrain.maximumScreenSpaceError=4;
        var go=new GameObject("Environment satellite survey (temporary)"){hideFlags=HideFlags.HideAndDontSave};
        camera=go.AddComponent<Camera>();camera.transform.position=geo.transform.TransformPoint(new Vector3(324.51624f,2000,769.08044f));
        camera.transform.rotation=Quaternion.Euler(90,0,0);camera.orthographic=true;camera.orthographicSize=710;camera.aspect=1;
        camera.nearClipPlane=.3f;camera.farClipPlane=4000;camera.targetTexture=new RenderTexture(4096,4096,24);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
        geo.GetComponent<CesiumCameraManager>().additionalCameras.Add(camera);
        const int nx=242,nz=274;
        var coordinates=new double3[nx*nz];
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
        {
            double px=-277+x*1202.0/(nx-1),pz=89+z*1361.0/(nz-1);
            coordinates[z*nx+x]=CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(geo.TransformUnityPositionToEarthCenteredEarthFixed(new double3(px,0,pz)));
        }
        var task=terrain.SampleHeightMostDetailed(coordinates);
        Status="Loading high-resolution imagery and sampling "+coordinates.Length+" measured terrain heights";
        double started=EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction poll=null;
        poll=()=>
        {
            if(!task.IsCompleted && EditorApplication.timeSinceStartup-started<240)return;
            EditorApplication.update-=poll;
            try
            {
                if(!task.IsCompleted)throw new TimeoutException("Elevation sampling timed out.");
                if(task.IsFaulted)throw task.Exception;
                var result=task.Result;
                if(result.sampleSuccess.Any(ok=>!ok))throw new InvalidOperationException("Elevation samples failed.");
                var heights=result.longitudeLatitudeHeightPositions.Select(llh=>(float)geo.TransformEarthCenteredEarthFixedPositionToUnity(CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(llh)).y).ToArray();
                File.WriteAllText("RoadRepairBackup/environment-elevation.json",JsonConvert.SerializeObject(new{nx,nz,x0=-277,z0=89,width=1202,depth=1361,heights}));
                Status="Elevation ready: "+heights.Length+" successful samples, local height "+heights.Min().ToString("F2")+" to "+heights.Max().ToString("F2")+" m. Call Capture to save imagery.";
            }
            catch(Exception ex){Status="Failed: "+ex.Message;Debug.LogError(Status);Cleanup();}
        };
        EditorApplication.update+=poll;
    }
    public static void Capture()
    {
        if(!camera)throw new InvalidOperationException("Start survey first.");
        var terrain=GameObject.Find("Cesium World Terrain");
        var renderers=terrain.GetComponentsInChildren<Renderer>();
        var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
        var old=RenderTexture.active;Texture2D texture=null;
        try
        {
            for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=30;
            camera.Render();RenderTexture.active=camera.targetTexture;
            texture=new Texture2D(4096,4096,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,4096,4096),0,0);texture.Apply();
            File.WriteAllBytes("RoadRepairBackup/environment-satellite.png",texture.EncodeToPNG());
            Status="Saved ground-only 4096px satellite imagery and measured elevation grid.";
        }
        finally
        {
            RenderTexture.active=old;
            for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].gameObject.layer=layers[i];
            if(texture)UnityEngine.Object.DestroyImmediate(texture);Cleanup();
        }
    }
    static void Cleanup()
    {
        var terrain=GameObject.Find("Cesium World Terrain").GetComponent<Cesium3DTileset>();terrain.maximumScreenSpaceError=previousError;
        if(!camera)return;
        GameObject.Find("CesiumGeoreference").GetComponent<CesiumCameraManager>().additionalCameras.Remove(camera);
        var rt=camera.targetTexture;camera.targetTexture=null;if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        UnityEngine.Object.DestroyImmediate(camera.gameObject);camera=null;
    }
}
