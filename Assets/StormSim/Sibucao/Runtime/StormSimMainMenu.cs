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
        void Awake(){play.onClick.AddListener(Play);locations.onClick.AddListener(Locations);settings.onClick.AddListener(Settings);}
        public void Play(){if(SibucaoSession.Instance)SibucaoSession.Instance.SelectRole();}
        void Locations()
        {
            var s=SibucaoSession.Instance;
            if(s && s.homes!=null&&s.homes.Length>0){locationPreview=(locationPreview+1)%s.homes.Length;orbs[0].tooltip.text=s.homes[locationPreview].locationName;}
        }
        void Settings(){if(SibucaoSession.Instance)SibucaoSession.Instance.OpenSettings();}
        void Update()
        {
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
