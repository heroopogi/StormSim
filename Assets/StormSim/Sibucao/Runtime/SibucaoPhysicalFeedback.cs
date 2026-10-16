using UnityEngine;
namespace StormSim.Sibucao
{
    [DefaultExecutionOrder(500)]
    public sealed class SibucaoPhysicalFeedback:MonoBehaviour
    {
        public float Squeeze {get;set;}
        public bool Impaired=>remaining>1.45f;
        public float MovementFactor=>remaining>0?.42f:1;
        public float remaining;
        SibucaoPlayer player;Texture2D edge;Vector3 applied,lastPosition;
        void Awake()
        {
            player=GetComponent<SibucaoPlayer>();edge=new Texture2D(128,128,TextureFormat.RGBA32,false);edge.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;pixels[y*128+x]=new Color(.035f,.045f,.05f,Mathf.SmoothStep(0,1,(d-.43f)/.7f));}edge.SetPixels(pixels);edge.Apply();
        }
        public void Clear(){remaining=Squeeze=0;if(player&&player.view&&(player.view.transform.localPosition-lastPosition).sqrMagnitude<.00001f)player.view.transform.localPosition-=applied;applied=Vector3.zero;}
        public void Stumble(float duration=2){remaining=Mathf.Max(remaining,duration);player.Gesture(2);}
        void LateUpdate()
        {
            if(!player||!player.view)return;var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing)return;
            var cam=player.view.transform;
            if((cam.localPosition-lastPosition).sqrMagnitude<.00001f)cam.localPosition-=applied;
            remaining=Mathf.Max(0,remaining-Time.deltaTime);
            float k=Mathf.Clamp01(remaining);applied=new Vector3(Mathf.Sin(Time.time*29)*.028f,-Mathf.Sin(k*Mathf.PI)*.1f,0)*k;
            if(s.settings.reducedMotion||UnityEngine.XR.XRSettings.isDeviceActive)applied=Vector3.zero;
            cam.localPosition+=applied;lastPosition=cam.localPosition;
            if(!s.settings.reducedMotion&&!UnityEngine.XR.XRSettings.isDeviceActive)cam.localRotation*=Quaternion.Euler(k*Mathf.Sin(Time.time*17)*2,0,k*Mathf.Sin(Time.time*11)*3);
        }
        void OnGUI()
        {
            var s=SibucaoSession.Instance;if(!s||s.Mode!=SibucaoMode.Playing)return;
            float intensity=Mathf.Max(Squeeze*.82f,Mathf.Clamp01(remaining)*.7f);if(intensity<=.001f)return;
            var old=GUI.color;GUI.color=new Color(1,1,1,intensity);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),edge);GUI.color=old;
        }
        void OnDestroy(){if(edge)Destroy(edge);}
    }
}
