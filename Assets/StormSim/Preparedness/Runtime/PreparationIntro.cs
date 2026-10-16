using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed class PreparationIntro : MonoBehaviour
    {
        [Tooltip("Drag your opening music here. Leave empty for a silent opening.")]
        public AudioClip openingMusic;
        public float titleSeconds=4,fadeSeconds=2;
        [Range(0,1)] public float musicVolume=.65f;
        public static bool Blocking {get;private set;}
        Canvas canvas;CanvasGroup group;AudioSource music;float elapsed;
        public void Begin()
        {
            End();Blocking=true;elapsed=0;
            var p=GetComponent<PreparednessSession>();
            var go=new GameObject("Opening title",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();group=go.GetComponent<CanvasGroup>();canvas.sortingOrder=32000;
            canvas.renderMode=XRSettings.isDeviceActive?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            if(XRSettings.isDeviceActive){go.transform.SetParent(p.player.view.transform,false);go.transform.localPosition=new Vector3(0,0,.5f);go.transform.localScale=Vector3.one*.001f;((RectTransform)go.transform).sizeDelta=new Vector2(3000,3000);canvas.worldCamera=p.player.view;}
            var bg=new GameObject("Black",typeof(RectTransform),typeof(Image));bg.transform.SetParent(go.transform,false);Stretch((RectTransform)bg.transform);bg.GetComponent<Image>().color=Color.black;
            var label=new GameObject("1 day before",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
            var rect=(RectTransform)label.transform;Stretch(rect);if(XRSettings.isDeviceActive){rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(850,180);}
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=XRSettings.isDeviceActive?38:44;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.text="1 day before";text.raycastTarget=false;
            var hint=new GameObject("Continue hint",typeof(RectTransform),typeof(Text));hint.transform.SetParent(go.transform,false);
            var hintRect=(RectTransform)hint.transform;hintRect.anchorMin=hintRect.anchorMax=new Vector2(.5f,.5f);hintRect.anchoredPosition=new Vector2(0,-105);hintRect.sizeDelta=new Vector2(920,80);
            var hintText=hint.GetComponent<Text>();hintText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");hintText.fontSize=XRSettings.isDeviceActive?22:20;hintText.alignment=TextAnchor.MiddleCenter;
            hintText.color=new Color(.76f,.86f,.82f);hintText.text=XRSettings.isDeviceActive?"Use either controller button to continue":"Press WASD, E, Space, or click to continue";hintText.raycastTarget=false;
            music=GetComponent<AudioSource>();if(!music)music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=false;music.spatialBlend=0;music.clip=openingMusic;music.volume=musicVolume*(SibucaoSession.Instance?SibucaoSession.Instance.settings.volume:1);if(openingMusic)music.Play();
        }
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        void Update()
        {
            if(!Blocking||!canvas)return;
            var game=SibucaoSession.Instance;if(game&&game.Mode!=SibucaoMode.Preparation){End();return;}
            if(XRSettings.isDeviceActive){var view=GetComponent<PreparednessSession>().player.view.transform;var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);if(head.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 pos))view.localPosition=pos;if(head.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion rot))view.localRotation=rot;}
            elapsed+=Time.unscaledDeltaTime;float alpha=1-Mathf.Clamp01((elapsed-Mathf.Max(0,titleSeconds))/Mathf.Max(.1f,fadeSeconds));group.alpha=alpha;
            if(music)music.volume=musicVolume*alpha*(game?game.settings.volume:1);
            if(alpha<=0)End();
        }
        public void End(){Blocking=false;if(canvas){canvas.gameObject.SetActive(false);Destroy(canvas.gameObject);}canvas=null;if(music)music.Stop();}
        void OnDisable(){End();}
    }
}
