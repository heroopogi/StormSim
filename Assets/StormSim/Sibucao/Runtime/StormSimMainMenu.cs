using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR;

namespace StormSim.Sibucao
{
    public sealed class StormSimMainMenu : MonoBehaviour
    {
        public Canvas canvas;
        public CanvasGroup group;
        public Button play,locations,settings;
        public StormSimMenuOrb[] orbs;
        [Range(1.5f,2)] public float viewingDistance=1.8f;
        public bool previewWithoutSession;
        bool visible;
        int locationPreview;
        StormSimMainMenuScene standalone;
        public Button ContinueButton { get; private set; }
        readonly Dictionary<Text,int> homeFontSizes=new Dictionary<Text,int>();
        readonly Dictionary<Text,float> homeTextHeights=new Dictionary<Text,float>();
        Vector2 authoredSize;
        Vector3 authoredScale;
        bool displayInitialized;
        public void BindStandalone(StormSimMainMenuScene owner){standalone=owner;}
        public void ConfigureStandaloneDisplay(Camera eye,bool xr)
        {
            var root=(RectTransform)canvas.transform;
            if(!displayInitialized){authoredSize=root.sizeDelta;authoredScale=root.localScale;displayInitialized=true;}
            var scaler=canvas.GetComponent<CanvasScaler>();
            if(!scaler)scaler=canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.renderMode=xr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=xr?eye:null;
            scaler.enabled=!xr;
            if(xr){root.sizeDelta=authoredSize;root.localScale=authoredScale;}
            else
            {
                scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1280,720);
                scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
            }
        }
        public void RefreshStandaloneHome(string location,float textScale,bool hasSave)
        {
            if(!ContinueButton)
            {
                ContinueButton=Instantiate(play,play.transform.parent,false);
                ContinueButton.name="Continue saved run";
                ContinueButton.onClick.RemoveAllListeners();
                ContinueButton.onClick.AddListener(()=>standalone.ContinueFromAuthoredMenu());
                var rect=(RectTransform)ContinueButton.transform;
                rect.anchoredPosition=new Vector2(0,-131);rect.sizeDelta=new Vector2(176,56);
                ContinueButton.GetComponent<StormSimMenuOrb>().tooltip.text="Continue";
            }
            ContinueButton.interactable=hasSave;
            foreach(var text in canvas.GetComponentsInChildren<Text>(true))
            {
                if(!homeFontSizes.TryGetValue(text,out int size))
                {
                    size=text.fontSize;homeFontSizes.Add(text,size);
                    homeTextHeights.Add(text,text.rectTransform.sizeDelta.y);
                }
                text.resizeTextForBestFit=false;
                float scale=Mathf.Clamp(textScale,1,1.3f);
                text.fontSize=Mathf.RoundToInt(size*scale);
                var box=text.rectTransform.sizeDelta;box.y=homeTextHeights[text]*scale;text.rectTransform.sizeDelta=box;
            }
            var subtitle=transform.Find("Signal Station subtitle");
            if(subtitle)subtitle.GetComponent<Text>().text="TYPHOON PREPAREDNESS / "+location.ToUpperInvariant();
        }
        public void SetStandaloneVisible(bool show)
        {
            visible=show;group.alpha=show?1:0;group.interactable=show;group.blocksRaycasts=show;
            canvas.gameObject.SetActive(show);
        }
        void Awake(){play.onClick.AddListener(Play);locations.onClick.AddListener(Locations);settings.onClick.AddListener(Settings);}
        public void Play(){if(standalone){standalone.StartFromAuthoredMenu();return;}if(SibucaoSession.Instance)SibucaoSession.Instance.SelectRole();}
        void Locations()
        {
            if(standalone){standalone.OpenAuthoredLocations();return;}
            var s=SibucaoSession.Instance;
            if(s && s.homes!=null&&s.homes.Length>0){locationPreview=(locationPreview+1)%s.homes.Length;orbs[0].tooltip.text=s.homes[locationPreview].locationName;}
        }
        void Settings(){if(standalone){standalone.OpenAuthoredSettings();return;}if(SibucaoSession.Instance)SibucaoSession.Instance.OpenSettings();}
        void Update()
        {
            if(standalone)return;
            var s=SibucaoSession.Instance;
            bool show=s?s.Mode==SibucaoMode.Menu:previewWithoutSession;
            var eye=s?s.player.view:Camera.main;
            if(show&&!visible&&eye)
            {
                canvas.worldCamera=eye;
                var f=eye.transform.forward;
                transform.SetPositionAndRotation(eye.transform.position+f*viewingDistance,Quaternion.LookRotation(f,eye.transform.up));
            }
            visible=show;group.alpha=show?1:0;group.interactable=show;group.blocksRaycasts=show;
            if(!show||!eye)return;
            bool xr=XRSettings.isDeviceActive;
            foreach(var orb in orbs)
            {
                bool gaze=xr&&RectTransformUtility.RectangleContainsScreenPoint((RectTransform)orb.transform,new Vector2(eye.pixelWidth*.5f,eye.pixelHeight*.5f),eye);
                orb.Gaze(gaze);
                if(gaze&&SibucaoInteractionInput.Pressed)orb.GetComponent<Button>().onClick.Invoke();
            }
        }
    }
}
