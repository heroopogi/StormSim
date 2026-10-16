using UnityEngine;
using UnityEngine.InputSystem;
namespace StormSim.Sibucao
{
    public sealed class SibucaoCockpit : MonoBehaviour
    {
        public Transform steering, speedNeedle, rpmNeedle, leftGrip, rightGrip;
        public Transform[] wipers;
        public Renderer mirrorSurface;
        public bool tricycle;
        public Material rainGlass;
        SibucaoVehicle vehicle;
        Transform[] hands;
        Transform[] parents;
        Vector3[] positions;
        Quaternion[] rotations;
        AudioSource engine;
        AudioClip tone;
        Camera mirror;
        RenderTexture mirrorTexture;
        Vector3 eye;
        float lookYaw,lookPitch=5,mirrorTime;
        public bool wipersOn=true;
        public void Bind(SibucaoVehicle v)
        {
            vehicle=v;eye=v.Driver.view.transform.localPosition;
            var p=v.Driver;hands=new[]{p.hands.Find("Left hand"),p.hands.Find("Right hand")};
            parents=new Transform[2];positions=new Vector3[2];rotations=new Quaternion[2];
            for(int i=0;i<2;i++)if(hands[i]){parents[i]=hands[i].parent;positions[i]=hands[i].localPosition;rotations[i]=hands[i].localRotation;hands[i].SetParent(i==0?leftGrip:rightGrip,false);hands[i].localPosition=Vector3.zero;hands[i].localRotation=Quaternion.Euler(-50,0,i==0?-12:12);}
            engine=gameObject.AddComponent<AudioSource>();engine.loop=true;engine.spatialBlend=0;engine.volume=.15f;
            int rate=22050;var samples=new float[rate];var random=new System.Random(47);
            for(int i=0;i<rate;i++){float t=(float)i/rate;float pulse=Mathf.Sin(2*Mathf.PI*(tricycle?38:48)*t);samples[i]=pulse*.35f+Mathf.Sin(2*Mathf.PI*96*t)*.12f+((float)random.NextDouble()*2-1)*.04f;}
            tone=AudioClip.Create("Original synthesized engine idle",rate,1,rate,false);tone.SetData(samples,0);engine.clip=tone;engine.Play();
            if(mirrorSurface)
            {
                mirrorTexture=new RenderTexture(256,128,16);mirrorTexture.Create();
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetTexture("_BaseMap",mirrorTexture);material.SetTextureScale("_BaseMap",new Vector2(-1,-1));material.SetTextureOffset("_BaseMap",Vector2.one);mirrorSurface.material=material;
                mirror=new GameObject("Rear view camera").AddComponent<Camera>();mirror.transform.SetParent(v.transform,false);mirror.transform.localPosition=new Vector3(0,1.8f,-2.4f);mirror.transform.localRotation=Quaternion.Euler(4,180,0);mirror.fieldOfView=65;mirror.nearClipPlane=.1f;mirror.farClipPlane=120;mirror.targetTexture=mirrorTexture;mirror.enabled=false;
            }
        }
        public void Look(Vector2 delta)
        {
            lookYaw=Mathf.Clamp(lookYaw+delta.x,-110,110);lookPitch=Mathf.Clamp(lookPitch-delta.y,-60,65);
            if(Keyboard.current!=null && Keyboard.current.cKey.wasPressedThisFrame){lookYaw=0;lookPitch=0;}
            if(Keyboard.current!=null && Keyboard.current.vKey.wasPressedThisFrame)wipersOn=!wipersOn;
        }
        void LateUpdate()
        {
            if(!vehicle||!vehicle.Driver)return;
            var p=vehicle.Driver;
            steering.localRotation=Quaternion.Euler(tricycle?0:12,tricycle?vehicle.SteerAngle:0,tricycle?0:-vehicle.SteerAngle*4);
            if(speedNeedle)speedNeedle.localRotation=Quaternion.Euler(0,0,125-Mathf.Clamp01(Mathf.Abs(vehicle.Speed)*3.6f/80)*250);
            if(rpmNeedle)rpmNeedle.localRotation=Quaternion.Euler(0,0,120-(vehicle.EngineRpm/6000)*240);
            float sweep=wipersOn?Mathf.Sin(Time.time*3.8f)*48-12:-60;
            if(wipers!=null)foreach(var w in wipers)if(w)w.localRotation=Quaternion.Euler(0,0,sweep);
            if(rainGlass)rainGlass.SetFloat("_Wipers",wipersOn?1:0);
            float speed=Mathf.Abs(vehicle.Speed);float shake=Mathf.Sin(Time.time*25)*Mathf.Min(.006f,speed*.0007f);
            p.transform.rotation=Quaternion.Euler(0,vehicle.transform.eulerAngles.y,0);
            p.view.transform.localPosition=eye+new Vector3(0,shake,0);
            p.view.transform.localRotation=Quaternion.Euler(lookPitch+Mathf.Clamp(-vehicle.LongitudinalAcceleration*.25f,-2,2),lookYaw,Mathf.Clamp(-vehicle.LateralAcceleration*.3f,-2.5f,2.5f));
            if(engine){engine.pitch=Mathf.Lerp(.75f,2.2f,vehicle.EngineRpm/6000);engine.volume=vehicle.Driver&&SibucaoSession.Instance.Mode==SibucaoMode.Playing?.14f*SibucaoAudioMix.Effects:0;}
            if(mirror && Time.unscaledTime>mirrorTime){mirrorTime=Time.unscaledTime+.1f;mirror.Render();}
        }
        public void Unbind()
        {
            if(hands!=null)for(int i=0;i<2;i++)if(hands[i]){hands[i].SetParent(parents[i],false);hands[i].localPosition=positions[i];hands[i].localRotation=rotations[i];}
            if(vehicle && vehicle.Driver){vehicle.Driver.view.transform.localPosition=eye;vehicle.Driver.view.transform.localRotation=Quaternion.identity;}
            if(mirror){Destroy(mirror.gameObject);Destroy(mirrorSurface.material);}
            if(mirrorTexture){mirrorTexture.Release();Destroy(mirrorTexture);}if(tone)Destroy(tone);
        }
    }
}


