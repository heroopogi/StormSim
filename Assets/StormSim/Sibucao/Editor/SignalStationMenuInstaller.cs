using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StormSim.Sibucao.Editor
{
    public static class SignalStationMenuInstaller
    {
        const string Pack = "Assets/StormSim/Sibucao/Resources/SignalStation/";
        const string Prefab = "Assets/StormSim/Sibucao/UI/MainMenu/STORMSIM_MainMenu.prefab";
        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Pack + name + ".png");
        public static string Install()
        {
            foreach (string file in System.IO.Directory.GetFiles(Pack, "*.png"))
                AssetDatabase.ImportAsset(file.Replace('\\','/'), ImportAssetOptions.ForceUpdate);
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Apply(root.GetComponent<StormSimMainMenu>());
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return "Signal Station imported; main menu prefab reskinned with existing actions preserved.";
        }
        static void Apply(StormSimMainMenu menu)
        {
            var root = (RectTransform)menu.transform;
            root.sizeDelta = new Vector2(680, 470);
            foreach(var text in menu.GetComponentsInChildren<Text>(true))
            {
                text.font = AssetDatabase.LoadAssetAtPath<Font>(Pack + "RobotoMono-Regular.ttf");
                text.color = new Color32(238,244,242,255);
            }
            var background = root.Find("Void gradient - reference lens").GetComponent<Image>();
            background.sprite = Sprite("panel_bg_clipped");
            background.type = Image.Type.Sliced;
            background.rectTransform.sizeDelta = root.sizeDelta;
            var logo = root.Find("Droplet - exact reference SVG").GetComponent<Image>();
            logo.rectTransform.anchoredPosition = new Vector2(0,153);
            logo.color = new Color32(95,191,159,255);
            var title = root.Find("Sora wordmark").GetComponent<Text>();
            title.rectTransform.anchoredPosition = new Vector2(0,103);
            title.font = AssetDatabase.LoadAssetAtPath<Font>(Pack + "RobotoMono-Bold.ttf");
            title.fontSize = 34;
            Label(root,"Signal Station subtitle",new Vector2(0,52),new Vector2(600,28),"TYPHOON PREPAREDNESS / PUROK SIBUCAO",15);
            Label(root,"Signal Station instruction",new Vector2(0,-176),new Vector2(600,28),"PREPARE  /  RESPOND  /  REACH SHELTER",13);
            for(int i=0;i<menu.orbs.Length;i++)
            {
                var orb=menu.orbs[i];var rect=(RectTransform)orb.transform;
                rect.sizeDelta=new Vector2(176,72);rect.anchoredPosition=new Vector2((i-1)*192,-58);
                orb.signalStation=true;
                orb.normal=Sprite(i==1?"btn_selected":"btn_normal");orb.hover=Sprite("btn_hover");
                orb.disc.sprite=orb.normal;orb.disc.color=Color.white;orb.disc.type=Image.Type.Sliced;
                var button=orb.GetComponent<Button>();button.transition=Selectable.Transition.SpriteSwap;
                button.spriteState=new SpriteState { highlightedSprite=Sprite("btn_hover"),pressedSprite=Sprite("btn_pressed"),selectedSprite=Sprite("btn_selected"),disabledSprite=Sprite("btn_disabled") };
                var icon=rect.Find("Exact SVG icon").GetComponent<Image>();
                icon.rectTransform.anchoredPosition=new Vector2(-59,0);icon.rectTransform.sizeDelta=new Vector2(22,22);icon.color=new Color32(95,191,159,255);
                orb.tooltip.rectTransform.anchoredPosition=new Vector2(15,0);orb.tooltip.rectTransform.sizeDelta=new Vector2(120,50);
                orb.tooltip.fontSize=16;orb.tooltip.resizeTextForBestFit=true;orb.tooltip.resizeTextMinSize=11;orb.tooltip.resizeTextMaxSize=16;
                orb.tooltip.gameObject.SetActive(true);
            }
        }
        static void Label(RectTransform root,string name,Vector2 position,Vector2 size,string value,int fontSize)
        {
            var existing=root.Find(name);
            var rect=existing?(RectTransform)existing:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=rect.GetComponent<Text>();if(!text)text=rect.gameObject.AddComponent<Text>();
            text.text=value;text.font=AssetDatabase.LoadAssetAtPath<Font>(Pack+"RobotoMono-Regular.ttf");text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;
            text.color=new Color32(127,143,139,255);text.raycastTarget=false;
        }
    }
}
