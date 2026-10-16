using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StormSim.Preparedness;

namespace StormSim.UI
{
    public sealed partial class StormglassUI
    {
        TMP_Text PhoneLabel(string name,string value,Vector2 position,Vector2 size,float fontSize)
        {
            var r=Rect(name,phoneFrame,size);r.anchoredPosition=position;
            var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=theme.bodyFont;
            label.fontSize=fontSize;label.color=Color.white;label.text=value;
            label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
        }
        void BuildPhone()
        {
            phoneFrame=Rect("iPhone style video phone",modal,new Vector2(410,840));
            var rim=Rect("Titanium rim",phoneFrame,new Vector2(410,840));Panel(rim,new Color(.38f,.39f,.42f));
            var body=Rect("Black glass",phoneFrame,new Vector2(398,828));Panel(body,new Color(.025f,.025f,.03f));
            foreach(float y in new[]{265f,185f})
            {var side=Rect("Volume button",phoneFrame,new Vector2(5,54));side.anchoredPosition=new Vector2(-207,y);Panel(side,new Color(.4f,.41f,.44f));}
            var power=Rect("Side button",phoneFrame,new Vector2(5,85));power.anchoredPosition=new Vector2(207,225);Panel(power,new Color(.4f,.41f,.44f));
            PhoneLabel("Time","9:41",new Vector2(-138,379),new Vector2(70,35),20);
            var item=Prep.items.FirstOrDefault(i=>i.kind==PrepKind.Phone);
            PhoneLabel("Battery",(item?item.charge.ToString("0"):"0")+"%",new Vector2(140,379),new Vector2(70,35),18);
            var island=Rect("Camera island",phoneFrame,new Vector2(115,32));island.anchoredPosition=new Vector2(0,379);Panel(island,Color.black);
            var lens=Rect("Front camera",island,new Vector2(10,10));lens.anchoredPosition=new Vector2(40,0);Panel(lens,new Color(.08f,.12f,.18f));
            var media=Prep.GetComponent<PreparationPhoneMedia>();
            var screen=Rect("Odette video screen",phoneFrame,new Vector2(368,650));screen.anchoredPosition=new Vector2(0,22);
            var raw=screen.gameObject.AddComponent<RawImage>();raw.color=Color.white;raw.texture=media?media.Texture:null;raw.raycastTarget=false;
            phoneStatus=PhoneLabel("Video status",media?media.Status:"Video unavailable",new Vector2(0,22),new Vector2(340,80),22);
            PhoneLabel("Video title","Typhoon Odette in Southern Cebu",new Vector2(0,-320),new Vector2(365,32),18);
            var pause=Action("Pause",()=>{if(media)media.TogglePlayback();rebuild=false;},media,parent:phoneFrame,height:50);
            var pauseRect=(RectTransform)pause.transform;pauseRect.sizeDelta=new Vector2(164,50);pauseRect.anchoredPosition=new Vector2(-89,-366);
            phonePlaybackLabel=pause.GetComponentInChildren<TMP_Text>();
            var close=Action("Close",()=>Prep.player.CloseDesktopPanels(),parent:phoneFrame,height:50);
            var closeRect=(RectTransform)close.transform;closeRect.sizeDelta=new Vector2(164,50);closeRect.anchoredPosition=new Vector2(89,-366);
            var home=Rect("Home indicator",phoneFrame,new Vector2(130,5));home.anchoredPosition=new Vector2(0,-405);Panel(home,Color.white);
        }
    }
}
