#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    // Editor-only visual documentation capture. It never ships in a player build
    // and does not alter the route, flood, preparation inventory or save data.
    public sealed class SibucaoB1VideoCapture : MonoBehaviour
    {
        public string status="Idle";
        public int frameRate=12,width=1280,height=720;
        SibucaoSession S=>SibucaoSession.Instance;
        string folder;
        public static SibucaoB1VideoCapture Get()
        {var capture=FindAnyObjectByType<SibucaoB1VideoCapture>();return capture?capture:new GameObject("EDITOR ONLY B1 video capture").AddComponent<SibucaoB1VideoCapture>();}
        public void Run()
        {
            StopAllCoroutines();folder=Path.GetFullPath("Documentation/B1RecordingFrames");
            Directory.CreateDirectory(folder);foreach(var file in Directory.GetFiles(folder,"frame_*.jpg"))File.Delete(file);
            StartCoroutine(Capture());
        }
        void OnDestroy(){SibucaoEditorInput.Reset();}
        IEnumerator Capture()
        {
            status="Capturing";Application.runInBackground=true;SibucaoEditorInput.Reset();SibucaoEditorInput.Active=true;
            var section=S.RouteChoices.Current;
            if(!section||section.station.index!=0){status="FAILED: B1 is not current";yield break;}
            var camera=S.player.view;
            var wide=section.Approach-section.station.transform.forward*5+Vector3.up*.12f;
            var left=section.left.steps[0].item.position+Vector3.up*.8f;
            var center=section.station.transform.position+Vector3.up*.65f;
            var right=section.right.caller.position+Vector3.up*1.2f;
            var segments=new[]{
                new Shot(wide,center,wide,center,3),
                new Shot(wide,center,wide,left,3),
                new Shot(wide,left,wide,center,3),
                new Shot(wide,center,wide,right,3),
                new Shot(wide,right,Vector3.Lerp(wide,section.Approach,.55f),right,3),
                new Shot(Vector3.Lerp(wide,section.Approach,.55f),right,wide,center,3)};
            int frame=0;
            foreach(var shot in segments)
            {
                int count=Mathf.RoundToInt(shot.seconds*frameRate);
                for(int i=0;i<count;i++)
                {
                    float t=count<=1?1:i/(float)(count-1);t=t*t*(3-2*t);
                    var position=Vector3.Lerp(shot.fromPosition,shot.toPosition,t);
                    var target=Vector3.Lerp(shot.fromTarget,shot.toTarget,t);
                    var facing=target-(position+Vector3.up*1.55f);facing.y=0;
                    S.player.Teleport(position,facing.sqrMagnitude>.001f?facing.normalized:section.station.transform.forward);
                    yield return null;
                    camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position,Vector3.up);
                    SaveFrame(camera,Path.Combine(folder,$"frame_{frame++:0000}.jpg"));
                }
            }
            SibucaoEditorInput.Reset();status=$"PASS: {frame} frames at {frameRate} fps";
            File.WriteAllText("Documentation/B1_RECORDING_STATUS.txt",status+Environment.NewLine+DateTime.Now.ToString("O"));
        }
        void SaveFrame(Camera camera,string path)
        {
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,width,height),0,0,false);image.Apply(false,false);
                File.WriteAllBytes(path,image.EncodeToJPG(88));
            }
            finally
            {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        readonly struct Shot
        {
            public readonly Vector3 fromPosition,fromTarget,toPosition,toTarget;public readonly float seconds;
            public Shot(Vector3 fp,Vector3 ft,Vector3 tp,Vector3 tt,float duration)
            {fromPosition=fp;fromTarget=ft;toPosition=tp;toTarget=tt;seconds=duration;}
        }
    }
}
#endif
