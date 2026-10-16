using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace StormSim.Sibucao
{
    public sealed class StormSimMenuOrb : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Text tooltip;
        public Image disc;
        public Sprite normal,hover;
        public bool primary;
        public bool signalStation;
        bool pointer,gaze,selected;
        Vector2 rest;
        void Awake(){rest=((RectTransform)transform).anchoredPosition;}
        public void OnPointerEnter(PointerEventData e){pointer=true;}
        public void OnPointerExit(PointerEventData e){pointer=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;}
        public void Gaze(bool value){if(signalStation&&gaze!=value)disc.overrideSprite=value?hover:null;gaze=value;}
        void OnDisable(){pointer=gaze=selected=false;if(tooltip)tooltip.gameObject.SetActive(false);}
        void Update()
        {
            if(signalStation){tooltip.gameObject.SetActive(true);return;}
            bool on=pointer||gaze||selected;
            tooltip.gameObject.SetActive(on);
            if(!primary)disc.sprite=on?hover:normal;
            var r=(RectTransform)transform;r.anchoredPosition=Vector2.Lerp(r.anchoredPosition,rest+Vector2.up*(on?3:0),1-Mathf.Exp(-Time.unscaledDeltaTime*16));
        }
    }
}
