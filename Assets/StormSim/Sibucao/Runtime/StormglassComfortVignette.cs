using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace StormSim.UI
{
    /// <summary>A soft peripheral mask used only during artificial VR movement.</summary>
    public sealed class StormglassComfortVignette : MonoBehaviour
    {
        Camera eye;
        MeshRenderer display;
        Material material;
        Texture2D texture;
        float target,visible;

        void Start()
        {
            var session=StormSim.Sibucao.SibucaoSession.Instance;
            eye=session&&session.player?session.player.view:null;
            if(!eye)return;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Stormglass comfort vignette";
            quad.transform.SetParent(eye.transform,false);Destroy(quad.GetComponent<Collider>());
            display=quad.GetComponent<MeshRenderer>();display.shadowCastingMode=ShadowCastingMode.Off;display.receiveShadows=false;
            var shader=Shader.Find("Unlit/Transparent");if(!shader){enabled=false;Destroy(quad);return;}
            material=new Material(shader){name="Stormglass comfort vignette material",renderQueue=5000};
            texture=BuildTexture();material.mainTexture=texture;material.color=new Color(StormglassTheme.Background.r,StormglassTheme.Background.g,StormglassTheme.Background.b,0);
            display.sharedMaterial=material;display.sortingOrder=32767;display.enabled=false;
        }

        Texture2D BuildTexture()
        {
            const int size=256;var result=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Stormglass comfort vignette",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2-1,ny=(y+.5f)/size*2-1;
                float radius=Mathf.Sqrt(nx*nx+ny*ny);float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.54f,1.02f,radius));
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            result.SetPixels(pixels);result.Apply(false,true);return result;
        }

        public void SetMotion(float amount){target=Mathf.Clamp01(amount);}

        void LateUpdate()
        {
            if(!display||!eye)return;
            var session=StormSim.Sibucao.SibucaoSession.Instance;float strength=session?session.settings.vignetteStrength:0;
            visible=Mathf.MoveTowards(visible,target,Time.unscaledDeltaTime*(target>visible?4:2.5f));
            float alpha=XRSettings.isDeviceActive?visible*strength:0;display.enabled=alpha>.01f;
            if(!display.enabled)return;
            float distance=Mathf.Max(eye.nearClipPlane+.035f,.12f);float height=2*Mathf.Tan(eye.fieldOfView*Mathf.Deg2Rad*.5f)*distance;
            display.transform.localPosition=new Vector3(0,0,distance);display.transform.localRotation=Quaternion.identity;display.transform.localScale=new Vector3(height*eye.aspect*1.06f,height*1.06f,1);
            material.color=new Color(StormglassTheme.Background.r,StormglassTheme.Background.g,StormglassTheme.Background.b,alpha);
        }

        void OnDestroy(){if(material)Destroy(material);if(texture)Destroy(texture);}
    }
}
