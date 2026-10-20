using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using StormSim.Sibucao;
using StormSim.Preparedness;
using Keyboard = UnityEngine.InputSystem.Keyboard;
using Gamepad = UnityEngine.InputSystem.Gamepad;

namespace StormSim.UI
{
    [DefaultExecutionOrder(800)]
    public sealed partial class StormglassUI : MonoBehaviour
    {
        public static StormglassUI Active { get; private set; }
        public static bool InUse => Active && Active.isActiveAndEnabled && Active.ready;
        public static bool BlocksWorldInput => StormglassSceneTransition.IsLoading || InUse && Active.modal.gameObject.activeSelf;
        public static bool PointerConsumesWorldInput => InUse && Active.routePointerHovered;
        public StormglassTheme theme;
        public float viewingDistance = 1.8f;
        public Canvas Canvas { get; private set; }
        public IReadOnlyList<Button> Buttons => buttons;
        public string ScreenKey => screenKey;
        public Camera Eye => Prep ? Prep.player.view : S.player.view;
        SibucaoSession S;
        SibucaoPreparationBridge bridge;
        PreparednessSession Prep => bridge && bridge.Active ? bridge.preparation : null;
        RectTransform modal, content, viewport, footer, hud, objective, prompt, itemInfo, itemInfoToggle, caption, notice, vitals;
        TMP_Text objectiveText, promptText, itemInfoText, itemInfoToggleText, captionText, noticeText, vitalsText;
        ScrollRect scroll;
        readonly List<Button> buttons = new List<Button>();
        StormglassPointer[] pointers;
        LineRenderer[] rays;
        GameObject[] cursors;
        Material rayMaterial;
        AudioSource uiSound;
        AudioClip uiHover, uiActivate;
        RectTransform healthFill;
        string screenKey = "";
        bool ready, rebuild, menuHeld, objectiveHeld, xrWasActive, locationsOpen, guideHeld, bagHeld;
        bool menuAnchorValid, xrMaterialsConfigured;
        readonly bool[] pointerTriggerHeld = new bool[2];
        int activePointer = -1;
        float nextStickScroll;
        int lastActivationFrame = -1;
        int settingsTab, reviewTab, inventoryPage, noticePriority;
        float nextHud;
        CanvasScaler screenScaler;
        Button scrollUpButton,scrollDownButton;
        TMP_Text scrollPositionText;
        GameObject lastVisibleSelection;
        int actionFocusIndex=-1;
        string actionFocusScreen;

        void Awake()
        {
            S = GetComponent<SibucaoSession>(); bridge = GetComponent<SibucaoPreparationBridge>();
            if (!theme) theme = Resources.Load<StormglassTheme>("Stormglass/Theme");
            if (!S || !theme || !theme.bodyFont || !theme.headingFont || !theme.PanelSprite)
            { Debug.LogError("Stormglass: theme assets missing. Run StormSim/Stormglass/Install theme."); enabled = false; return; }
            Active = this;
            var events = EventSystem.current;
            if (!events) { events = new GameObject("Stormglass EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>(); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            pointers = new[] { new StormglassPointer(events, -101), new StormglassPointer(events, -102) };
            var go = new GameObject("Stormglass • World UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); Canvas = go.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.WorldSpace;
            screenScaler=go.AddComponent<CanvasScaler>();
            screenScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            screenScaler.referenceResolution=new Vector2(1280,1000);
            screenScaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            Canvas.sortingOrder = 100; Canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            ((RectTransform)go.transform).sizeDelta = new Vector2(688, 860); go.transform.localScale = Vector3.one * .00135f;
            modal = Rect("Menu", go.transform, new Vector2(688, 860)); Panel(modal, StormglassTheme.Surface);
            viewport = Rect("Reading area", modal, new Vector2(624, 724)); viewport.anchoredPosition = new Vector2(0, 36); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport, new Vector2(624, 0)); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 20; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = modal.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            footer = Rect("Reading controls", modal, new Vector2(624, 64)); footer.anchoredPosition = new Vector2(0, -366);
            var row = footer.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 16; row.childControlWidth = true; row.childForceExpandWidth = true;
            hud = Rect("Context", go.transform, new Vector2(688, 860));
            objective = Card(hud, "Objective", Vector2.zero, new Vector2(420, 160), out objectiveText);
            objective.anchorMin=objective.anchorMax=new Vector2(0,1);objective.pivot=new Vector2(0,1);objective.anchoredPosition=new Vector2(24,-24);
            prompt = Card(hud, "Object prompt", new Vector2(0, -225), new Vector2(520, 150), out promptText);
            itemInfoToggle = Card(hud, "Item info control", Vector2.zero, new Vector2(190, 54), out itemInfoToggleText);
            itemInfo = Card(hud, "Item information", Vector2.zero, new Vector2(330, 170), out itemInfoText);
            caption = Card(hud, "Subtitles", new Vector2(0, -370), new Vector2(680, 118), out captionText);
            notice = Card(hud, "Alert", new Vector2(270, 260), new Vector2(420, 180), out noticeText);
            vitals = Card(hud, "Survival", new Vector2(-310, 40), new Vector2(300, 172), out vitalsText);
            // Gameplay HUD uses the viewport edges, independently of menu sizing.
            hud.anchorMin=Vector2.zero;hud.anchorMax=Vector2.one;hud.offsetMin=hud.offsetMax=Vector2.zero;
            vitals.anchorMin=vitals.anchorMax=new Vector2(1,0);vitals.pivot=new Vector2(1,0);
            vitals.anchoredPosition=new Vector2(-28,28);vitals.sizeDelta=new Vector2(240,112);
            vitalsText.enableAutoSizing=false;vitalsText.fontSize=18;
            vitalsText.rectTransform.sizeDelta=new Vector2(212,88);
            var healthTrack=Rect("Health bar",vitals,new Vector2(212,7));
            healthTrack.anchoredPosition=new Vector2(0,8);Panel(healthTrack,new Color(.1f,.2f,.23f,1));
            healthFill=Rect("Health remaining",healthTrack,new Vector2(212,7));
            healthFill.anchorMin=healthFill.anchorMax=new Vector2(0,.5f);healthFill.pivot=new Vector2(0,.5f);
            Panel(healthFill,StormglassTheme.Accent);
            prompt.localScale=itemInfo.localScale=itemInfoToggle.localScale=caption.localScale=notice.localScale=objective.localScale=Vector3.one;
            prompt.anchorMin=prompt.anchorMax=caption.anchorMin=caption.anchorMax=new Vector2(.5f,0);
            prompt.pivot=caption.pivot=new Vector2(.5f,0);
            itemInfoToggle.anchorMin=itemInfoToggle.anchorMax=itemInfo.anchorMin=itemInfo.anchorMax=new Vector2(1,1);
            itemInfoToggle.pivot=itemInfo.pivot=new Vector2(1,1);
            itemInfoToggle.anchoredPosition=new Vector2(-28,-210);
            itemInfo.anchoredPosition=new Vector2(-28,-262);
            itemInfoText.alignment=TextAlignmentOptions.TopLeft;
            itemInfoToggleText.alignment=TextAlignmentOptions.Center;
            itemInfoToggleText.enableAutoSizing=false;itemInfoToggleText.fontSize=18;
            itemInfoToggleText.rectTransform.sizeDelta=new Vector2(160,30);
            notice.anchorMin=notice.anchorMax=new Vector2(1,1);notice.pivot=new Vector2(1,1);notice.anchoredPosition=new Vector2(-28,-28);
            noticeText.color = StormglassTheme.Warning;
            uiSound=gameObject.AddComponent<AudioSource>();uiSound.playOnAwake=false;uiSound.spatialBlend=0;
            uiHover=MakeUITone("Stormglass focus",520,760,.045f,.055f);
            uiActivate=MakeUITone("Stormglass activate",420,920,.075f,.11f);
            foreach (var old in FindObjectsByType<StormSimMainMenu>(FindObjectsInactive.Include)) old.gameObject.SetActive(false);
            CreatePreparationHud();
            CreateRouteCards();
            CreateRays(); ready = true; rebuild = true;
        }
        void OnDestroy()
        {
            if(taskSound)Destroy(taskSound);if(taskChime)Destroy(taskChime);if(aimSprite)Destroy(aimSprite);if(aimTexture)Destroy(aimTexture);
            if(uiHover)Destroy(uiHover);if(uiActivate)Destroy(uiActivate);
            if (Active == this) Active = null;
            if (Canvas) Destroy(Canvas.gameObject);
            if (rays != null) foreach (var r in rays) if (r) Destroy(r.gameObject);
            if (cursors != null) foreach (var c in cursors) if (c) Destroy(c);
            if (rayMaterial) Destroy(rayMaterial);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.sizeDelta = size; return r; }
        void Panel(RectTransform rect, Color color)
        { var image = rect.gameObject.AddComponent<Image>(); image.sprite = theme.PanelSprite; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false; }
        void SignalEdge(RectTransform parent, Color color)
        {
            var edge=Rect("Signal edge",parent,Vector2.zero);
            edge.anchorMin=new Vector2(0,0);edge.anchorMax=new Vector2(0,1);
            edge.pivot=new Vector2(0,.5f);edge.offsetMin=new Vector2(0,14);edge.offsetMax=new Vector2(4,-14);
            var image=edge.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;
        }
        RectTransform Card(Transform parent, string name, Vector2 position, Vector2 size, out TMP_Text text)
        {
            var r = Rect(name, parent, size); r.anchoredPosition = position; Panel(r, StormglassTheme.Surface); SignalEdge(r, name=="Alert"?StormglassTheme.Warning:StormglassTheme.Accent);
            var tr = Rect("Text", r, size - new Vector2(40, 28)); text = tr.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.bodyFont; text.fontSize = 24; text.color = StormglassTheme.Ink; text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Truncate;
            text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 24; r.gameObject.SetActive(false); return r;
        }
        TMP_Text Text(string value, float size = 24, Color? color = null, bool heading = false)
        {
            var r = Rect("Text • " + value.Split('\n')[0], content, new Vector2(624, 40)); var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = heading ? theme.headingFont : theme.bodyFont; text.fontSize = size * S.settings.textScale;
            text.color = color ?? StormglassTheme.Ink; text.text = value; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.margin = new Vector4(2, 0, 2, 0);
            return text;
        }
        static AudioClip MakeUITone(string name,float startHz,float endHz,float duration,float gain)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(rate*duration)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=i/(float)Mathf.Max(1,samples.Length-1);
                float frequency=Mathf.Lerp(startHz,endHz,u);float envelope=Mathf.Sin(Mathf.PI*u);
                samples[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*gain;
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void PlayUIHover(){if(uiSound&&uiHover&&!uiSound.isPlaying){uiSound.volume=S?S.settings.effectsVolume:1;uiSound.PlayOneShot(uiHover);}}
        void PlayUIActivate(){if(uiSound&&uiActivate){uiSound.volume=S?S.settings.effectsVolume:1;uiSound.PlayOneShot(uiActivate);}}
        Button Action(string label, Action action, bool enabled = true, Transform parent = null, float height = 80, bool primary = true)
        {
            var r = Rect("Action • " + label.Split('\n')[0], parent ? parent : content, new Vector2(624, height)); Panel(r, primary?StormglassTheme.Accent:StormglassTheme.Subtle);
            var image = r.GetComponent<Image>(); image.raycastTarget = true;
            var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height * S.settings.textScale; le.flexibleWidth = 1;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.interactable = enabled;
            b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(.9f,.96f,.96f,1);colors.pressedColor = new Color(.72f, .82f, .82f, 1); colors.disabledColor = new Color(.45f,.5f,.52f,1); colors.fadeDuration = .08f; b.colors = colors;
            var tr = Rect("Label", r, Vector2.zero); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(20, 8); tr.offsetMax = new Vector2(-20, -8);
            var t = tr.gameObject.AddComponent<TextMeshProUGUI>(); t.font = theme.bodyFont; t.fontSize = 24 * S.settings.textScale;
            t.enableAutoSizing=true;t.fontSizeMin=18*S.settings.textScale;t.fontSizeMax=24*S.settings.textScale;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Truncate;
            t.color = enabled ? (primary?StormglassTheme.OnAccent:StormglassTheme.Ink) : StormglassTheme.Muted; t.alignment = TextAlignmentOptions.Center; t.text = label; t.raycastTarget = false;
            var focus = r.gameObject.AddComponent<StormglassFocus>(); focus.sprite = theme.PanelSprite;
            b.onClick.AddListener(() => { if(lastActivationFrame == Time.frameCount) return; lastActivationFrame = Time.frameCount;actionFocusIndex=buttons.IndexOf(b);actionFocusScreen=screenKey;PlayUIActivate(); rebuild = true; action(); }); buttons.Add(b); return b;
        }
        void ActionPair(string left,Action leftAction,string right,Action rightAction,bool rightEnabled=true)
        {
            var root=Rect("Secondary actions",content,new Vector2(624,72));
            var size=root.gameObject.AddComponent<LayoutElement>();size.minHeight=size.preferredHeight=72*S.settings.textScale;size.flexibleWidth=1;
            var row=root.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=16;row.childControlWidth=true;row.childForceExpandWidth=true;
            Action(left,leftAction,true,root,72,false);Action(right,rightAction,rightEnabled,root,72,false);
        }
        void Heading(string eyebrow, string title, string description = null)
        { Text(eyebrow, 18, StormglassTheme.Accent); Text(title, 42, null, true); if (!string.IsNullOrEmpty(description)) Text(description, 22, StormglassTheme.Muted); }
        void MessageCard(string title, string body, Color accent)
        {
            var root = Rect("Message • " + title, content, new Vector2(624, 104)); Panel(root, StormglassTheme.Subtle); SignalEdge(root,accent);
            var le = root.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 104; le.flexibleWidth = 1;
            var label = Rect("Message text", root, Vector2.zero); label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(22, 12); label.offsetMax = new Vector2(-22, -12);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>(); text.font = theme.bodyFont; text.fontSize = 19 * S.settings.textScale;
            text.color = StormglassTheme.Ink; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + title.ToUpperInvariant() + "</color>\n" + body;
            // The reading area scrolls; never cut recorded outcomes to a fixed card height.
            float preferred=Mathf.Max(text.GetPreferredValues(text.text,580,Mathf.Infinity).y+28,104);
            root.sizeDelta=new Vector2(624,preferred);le.minHeight=le.preferredHeight=preferred;
        }
        void MeterCard(string title, string value, float ratio, bool warning = false)
        {
            var root = Rect("Meter • " + title, content, new Vector2(624, 92)); Panel(root, StormglassTheme.Surface);
            var le = root.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 92; le.flexibleWidth = 1;
            var label = Rect("Meter label", root, new Vector2(580, 40)); label.anchoredPosition = new Vector2(0, 20);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>(); text.font = theme.bodyFont; text.fontSize = 20 * S.settings.textScale;
            text.color = StormglassTheme.Ink; text.text = title + "  " + value; text.raycastTarget = false;
            var track = Rect("Meter track", root, new Vector2(580, 10)); track.anchoredPosition = new Vector2(0, -25); Panel(track, StormglassTheme.Subtle);
            var fill = Rect("Meter fill", track, new Vector2(580 * Mathf.Clamp01(ratio), 10)); fill.anchorMin = fill.anchorMax = new Vector2(0, .5f); fill.pivot = new Vector2(0, .5f); Panel(fill, warning ? StormglassTheme.Warning : StormglassTheme.Accent);
        }
        void Clear(Transform root) { for (int i = root.childCount - 1; i >= 0; i--) { root.GetChild(i).gameObject.SetActive(false); Destroy(root.GetChild(i).gameObject); } }
        string Key()
        {
            if(PreparationIntro.Blocking)return "Hidden";
            if (S.Mode == SibucaoMode.Menu && locationsOpen) return "Locations";
            if (S.Mode == SibucaoMode.Settings) return "Settings";
            if (Prep && S.Mode == SibucaoMode.Preparation)
            {
                var p = Prep.player;
                if (p.PhoneOpen) return "Phone";

                if (Prep.chapter && Prep.chapter.IsOpen) return "Conversation";
                if (p.StormglassDepartureOpen) return "Departure";
                // The preparation checklist is a physical animated field guide.
                // Keep the surrounding interface in its small HUD state while it is open.
                if (p.GuideOpen) return "HUD";
                if (p.BagPanelOpen) return "Preparation bag";
                return "HUD";
            }
            if (S.Mode == SibucaoMode.Playing)
            { if (S.inventory && S.inventory.IsOpen) return "Bag"; if (S.missionGuide && S.missionGuide.IsOpen) return "Guide"; return "HUD"; }
            if (S.Mode == SibucaoMode.Cinematic) return "Hidden";
            return S.Mode.ToString();
        }
        void LateUpdate()
        {
            if (StormglassSceneTransition.IsLoading) return;
            if (!ready || !S || !Eye) return;
            bool xr = XRSettings.isDeviceActive;
            if(xr && Prep)
            {
                var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(head.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p))Eye.transform.localPosition=p;
                if(head.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))Eye.transform.localRotation=q;
            }
            if (xr != xrWasActive) { xrWasActive = xr; rebuild = true; }
            ReadShortcuts(); var key = Key();
            if (key != screenKey || rebuild) { bool changed = key != screenKey; screenKey = key; rebuild = false; Build(); if (changed) { menuAnchorValid = false; Anchor(); } }
            Anchor();
            UpdateRouteCards();
            if(screenKey=="HUD") UpdateHud();
            UpdatePreparationHud();
            UpdatePointers(xr);
            UpdateScrollControls();
        }
        public void Anchor()
        {
            if(!Eye)return;
            bool xr=XRSettings.isDeviceActive;
            Canvas.renderMode=xr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            Canvas.worldCamera=xr?Eye:null;
            screenScaler.enabled=!xr;
            if(xr)
            {
                bool stableMenu=modal.gameObject.activeSelf;
                if(!stableMenu)
                {
                    // The compact HUD follows gently and only after the player looks away.
                    Vector3 target=Eye.transform.position+Eye.transform.forward*viewingDistance;
                    float angle=Quaternion.Angle(Canvas.transform.rotation,Eye.transform.rotation);
                    if(!menuAnchorValid||angle>12f||Vector3.Distance(Canvas.transform.position,target)>.28f)
                    {
                        float blend=menuAnchorValid?1-Mathf.Exp(-4f*Time.unscaledDeltaTime):1;
                        Canvas.transform.position=Vector3.Lerp(Canvas.transform.position,target,blend);
                        Canvas.transform.rotation=Quaternion.Slerp(Canvas.transform.rotation,Eye.transform.rotation,blend);
                    }
                    menuAnchorValid=true;
                }
                else if(!menuAnchorValid)
                {
                    // Full menus are placed once and remain world-stable until Recenter.
                    var forward=Vector3.ProjectOnPlane(Eye.transform.forward,Vector3.up).normalized;
                    if(forward.sqrMagnitude<.01f)forward=Eye.transform.forward;
                    Canvas.transform.SetPositionAndRotation(Eye.transform.position+forward*viewingDistance,Quaternion.LookRotation(forward,Vector3.up));
                    menuAnchorValid=true;
                }
                Canvas.transform.localScale=Vector3.one*.00135f;
                ((RectTransform)Canvas.transform).sizeDelta=new Vector2(1280,1000);
                if(!xrMaterialsConfigured)
                {
                    foreach(var graphic in Canvas.GetComponentsInChildren<Graphic>())
                        graphic.materialForRendering.SetInt("unity_GUIZTestMode",(int)UnityEngine.Rendering.CompareFunction.Always);
                    xrMaterialsConfigured=true;
                }
            }
            else { Canvas.transform.localScale=Vector3.one;menuAnchorValid=false;xrMaterialsConfigured=false; }
        }

        public void RecenterUI(){menuAnchorValid=false;Anchor();}
        void Build()
        {
            xrMaterialsConfigured=false;
            bool phoneScreen=screenKey=="Phone";
            if(phoneFrame){phoneFrame.gameObject.SetActive(false);Destroy(phoneFrame.gameObject);phoneFrame=null;}
            viewport.gameObject.SetActive(!phoneScreen);footer.gameObject.SetActive(!phoneScreen);
            modal.GetComponent<Image>().color=phoneScreen?Color.clear:StormglassTheme.Surface;
            modal.sizeDelta=new Vector2(phoneScreen?410:688,860);
            viewport.sizeDelta=new Vector2(phoneScreen?416:624,724);
            footer.sizeDelta=new Vector2(phoneScreen?416:624,64);
            foreach (var p in pointers) p.Reset(); buttons.Clear(); Clear(content); Clear(footer);
            bool show = screenKey != "HUD" && screenKey != "Hidden";
            modal.gameObject.SetActive(show); hud.gameObject.SetActive(screenKey == "HUD"); if (!show) return;
            switch (screenKey)
            {
                case "Menu":
                    Heading("STORMSIM / " + AreaName().ToUpperInvariant(), "Weather the storm.", "Prepare • Evacuate • Reflect");
                    MessageCard("TRAINING LOCATION",AreaName()+" • A fictional typhoon-preparedness scenario",StormglassTheme.Accent);
                    if(S.HasSave)
                    {
                        Action("Continue saved run\nChoose a checkpoint or manual save",S.LoadRun,height:78);
                        Action("Start a new simulation",S.SelectRole,height:72);
                    }
                    else
                    {
                        Action("Start simulation",S.SelectRole,height:78);
                        Action("Continue saved run\nNo saved run is available",S.LoadRun,false,height:72);
                    }
                    Action("Choose training location",()=>locationsOpen=true,height:72,primary:false);
                    Action("Comfort & settings",S.OpenSettings,height:72,primary:false);
                    ActionPair("About & credits",()=>S.SetMode(SibucaoMode.About),XRSettings.isDeviceActive?"Recenter UI":"Exit game",XRSettings.isDeviceActive?(Action)RecenterUI:()=>S.Confirm("exit"));
                    Text(XRSettings.isDeviceActive ? "Point with your active controller • Release Trigger to select" : "Mouse, arrow keys, or gamepad • Enter / A to select", 18, StormglassTheme.Muted); break;
                case "Locations":
                    Heading("TRAINING LOCATIONS", "Choose your community.","Changing location reloads the scenario. Saves remain separate for each community.");
                    foreach (var entry in new[] { new[] { "Sibucao", "SIBUCAO_RAIN" }, new[] { "Riverside", "RIVERSIDE_RAIN" }, new[] { "Baybayon", "BAYBAYON_RAIN" } })
                    { string scene = entry[1];bool current=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==scene;bool available=Application.CanStreamedLevelBeLoaded(scene);Action(entry[0]+(current?" • Current location":available?"":" • Not installed"), () => { locationsOpen = false; if (!current) StormSim.UI.StormglassSceneTransition.Load(scene); }, available,height:82,primary:current); }
                    Action("Back", () => locationsOpen = false); break;
                case "About":
                    Heading("ABOUT STORMSIM","Preparedness is practiced before the storm.","Version "+Application.version);
                    MessageCard("TRAINING PURPOSE","Explore preparation, evacuation, and community decisions in a fictional coastal typhoon scenario.",StormglassTheme.Accent);
                    Text("StormSim is a thesis simulation developed for disaster-preparedness learning. It is not a live warning service. Follow official local advisories during a real emergency.",22);
                    Text("Interface typography: Inter and Sora.\nBuilt with Unity. Required third-party attribution remains available with the project distribution.",18,StormglassTheme.Muted);
                    Action("Back to main menu",()=>S.SetMode(SibucaoMode.Menu),height:72);break;
                case "Paused":
                    Heading("SIMULATION PAUSED", "Take a moment.");
                    Action("Resume", () => S.SetMode(SibucaoMode.Playing)); Action("Save game", S.SaveRun);
                    Action("Load saved run", S.LoadRun, S.HasSave); Action("Recover current task", () => S.OpenRecovery());
                    Action("Comfort & settings", S.OpenSettings); Action("Restart this location", () => S.Confirm("restart"),primary:false); Action("Return to menu", () => S.Confirm("menu")); break;
                case "Settings": BuildSettings(); break;
                case "Confirm":
                    Heading("CONFIRM", S.confirmation == "restart" ? "Restart this location?" : S.confirmation == "exit" ? "Exit StormSim?" : "Return to the menu?", S.confirmation=="restart"?"Begin again with household preparation. The current run will end.":"Your current checkpoint is kept. Unsaved actions may be lost.");
                    MessageCard(S.confirmation=="restart"?"NEW RUN":"CHECKPOINT PRESERVED", S.confirmation=="restart"?"Manual saves remain available. New checkpoints will replace the previous run's checkpoints.":"You can choose an available saved checkpoint when you return.", StormglassTheme.Warning);
                    Action("Confirm", S.ConfirmYes); Action("Cancel", () => S.SetMode(S.confirmReturn)); break;
                case "Complete": BuildCompletionDebrief(); break;
                case "ArrivalReview":
                    Heading("SCHOOL RECEPTION", S.state.physical.arrivalReceived ? "Complete your handover." : "Welcome to refuge.");
                    MessageCard("HANDOVER",SibucaoDebrief.ArrivalSummary(S),StormglassTheme.Accent);
                    Action(S.state.physical.arrivalReceived ? "Confirm handover & review" : "Check in", () => S.physicalDecisions.ConfirmArrivalReport()); break;
                case "Decision":
                    if (S.activeEncounter) { var n = S.activeEncounter.node; Heading(n.speaker, n.title, n.message); for (int i = 0; i < n.choices.Length; i++) { int index = i;bool available=S.IsAvailable(n.choices[i]);string label=n.choices[i].label+(available?"":"\nUnavailable • first aid already used");Action(label, () => S.Choose(index), available, height:104); } }
                    Action("Back to scene", () => { S.activeEncounter = null; S.SetMode(SibucaoMode.Playing); }); break;
                case "ResumeSelection":
                    Heading("SAVED RUNS", "Continue your journey.", "Loading replaces unsaved progress.");
                    for (int i = 0; i < 4; i++) { bool checkpoint = i < 2, backup = i % 2 == 1; Action((checkpoint ? "Checkpoint" : "Manual save") + (backup ? " / backup" : "") + "\n" + S.SlotSummary(checkpoint, backup), () => S.LoadSlot(checkpoint, backup), S.SlotExists(checkpoint, backup), height:112); }
                    Action("Back", S.CancelResumeSelection); break;
                case "Recovery":
                    Heading("ASSISTANCE REQUIRED", S.state.physical.needsRescue?"Your condition requires support.":"Recover your current task.", "Completed work is preserved. Recovery does not award completion credit or replace lost supplies.");
                    MessageCard("PROGRESS IS PRESERVED", "Choose the least disruptive recovery option, then continue when you are ready.", StormglassTheme.Accent);
                    if(S.state.physical.needsRescue)MessageCard("ASSISTED RECOVERY","Your health reached an unsafe level. Returning to supported footing is recorded in the debrief.",StormglassTheme.Danger);
                    Action(S.state.physical.needsRescue?"Request assisted recovery":"Return to supported footing", () => S.ReturnToUnfinishedJunction()); Action("Reset current prop / escort", () => S.RecoverCurrentTask(), !S.state.physical.needsRescue);
                    Action("Choose a saved run", S.LoadRun, S.HasSave); Action("Resume here", () => S.SetMode(SibucaoMode.Playing), !S.state.physical.needsRescue); break;
                case "Guide":
                    Heading("CURRENT OBJECTIVE", "Your scenario guide.", S.missionGuide.Objective); Text(VRText(S.missionGuide.ClearInstructions), 24); Controls(); Action("Close guide", S.missionGuide.Close); break;
                case "Bag": BuildBag(); break;
                case "Preparation guide":
                    Heading("PREPARATION GUIDE", "Prepare your household.", Prep.player.CurrentObjective);
                    BuildPreparationOverview();
                    Action(Prep.Guided ? "Coaching: On" : "Coaching: Off", () => Prep.SetCoaching(!Prep.Guided));
                    Action("Save preparation", () => Prep.Save()); Action("Close guide", () => Prep.player.CloseDesktopPanels());
                    Action("Open phone / advisories", () => Prep.player.TogglePhone());
                    Controls(); Text(Prep.Journal(), 22); if(Prep.chapter)Text(Prep.chapter.Summary,22); break;
                case "Phone": BuildPhone(); break;
                case "Preparation bag":
                    Heading("GO-BAG", "Your carried supplies.", Prep.CapacityText);
                    MeterCard("BAG LOAD", Prep.BagMass.ToString("0.#") + " / " + PreparednessSession.BagLimit.ToString("0.#") + " kg", Prep.BagMass / PreparednessSession.BagLimit, Prep.BagMass > PreparednessSession.BagLimit * .85f);
                    var packed = Prep.items.Where(i => i.IsPacked).OrderBy(i=>Prep.ItemCategory(i)).ThenBy(i=>i.displayName).ToList();
                    if (packed.Count == 0) MessageCard("BAG IS EMPTY", "Pack the supplies that support your route. Keep room for essentials.", StormglassTheme.Warning);
                    string prepCategory=null;
                    foreach (var item in packed)
                    {
                        var saved = item;
                        var nextCategory=Prep.ItemCategory(item);
                        if(nextCategory!=prepCategory){prepCategory=nextCategory;Text(prepCategory.ToUpperInvariant(),20,StormglassTheme.Accent);}
                        string state = PreparednessSession.NeedsCheck(item) ? " • NEEDS CHECK" : " • READY";
                        Action(item.displayName + state + "\nRemove and place nearby", () => Prep.DropFromBag(saved), height: 92);
                    }
                    Action("Close bag", () => Prep.player.CloseDesktopPanels()); break;
                case "Departure":
                    Heading("DEPARTURE", "Review your next step.", "Optional preparations may remain unfinished. Follow the latest training advisory.");
                    Action("Confirm departure / rest", () => Prep.player.StormglassConfirmDeparture()); Action("Keep preparing", () => Prep.player.CloseDesktopPanels()); break;
                case "Conversation": BuildConversation(); break;
                default: Heading("STORMSIM", "Typhoon preparedness", "A fictional training scenario."); Action("Back to menu", () => S.SetMode(SibucaoMode.Menu)); break;
            }
            if(phoneScreen)return;
            // Every modal can grow at large text sizes and must expose scrolling.
            bool compact=false;
            footer.gameObject.SetActive(!compact);viewport.sizeDelta=new Vector2(624,compact?800:724);viewport.anchoredPosition=compact?Vector2.zero:new Vector2(0,36);
            scrollUpButton=scrollDownButton=null;scrollPositionText=null;
            if(!compact)
            {
                scrollUpButton=Action("↑ Up", () => { scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + .35f); rebuild = false; }, parent:footer, height:64,primary:false);
                var statusRoot=Rect("Scroll position",footer,new Vector2(150,64));var statusSize=statusRoot.gameObject.AddComponent<LayoutElement>();statusSize.minWidth=120;statusSize.preferredWidth=140;
                scrollPositionText=statusRoot.gameObject.AddComponent<TextMeshProUGUI>();scrollPositionText.font=theme.bodyFont;scrollPositionText.fontSize=16;scrollPositionText.color=StormglassTheme.Muted;scrollPositionText.alignment=TextAlignmentOptions.Center;scrollPositionText.raycastTarget=false;
                scrollDownButton=Action("↓ Down", () => { scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - .35f); rebuild = false; }, parent:footer, height:64,primary:false);
            }
            UnityEngine.Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); scroll.verticalNormalizedPosition = 1;
            if(!XRSettings.isDeviceActive&&EventSystem.current&&buttons.Count>0)
            {
                var first=actionFocusScreen==screenKey&&actionFocusIndex>=0&&actionFocusIndex<buttons.Count&&buttons[actionFocusIndex].IsInteractable()?buttons[actionFocusIndex]:buttons.FirstOrDefault(b=>b&&b.IsInteractable());if(first)EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
            actionFocusIndex=-1;actionFocusScreen=null;
            lastVisibleSelection=null;
        }
        string AreaName() { var area = S.GetComponent<CoastalAreaProfile>(); return area ? area.areaName : "Sibucao"; }
        void Controls()
        { Text(XRSettings.isDeviceActive ? "Point with your active controller and release Trigger to select.\nMenu: pause or back • Y: objective • X: guide • B: bag\nLeft stick: move • Right stick: "+(S.settings.smoothTurn?"smooth turn":S.settings.snapTurnAngle+"° snap turn")+"\nAim at an object: Trigger to use • Grip to carry • A to rise" : "WASD: move • Mouse: look • E: use\nF: carry • P: pack • I: bag • J: checklist • N: phone\nEscape: pause or back", 22, StormglassTheme.Muted); }
        void BuildSettings()
        {
            Heading("PREFERENCES", "Comfort & settings");
            Action(new[] { "Comfort", "Audio", "Controls" }[settingsTab] + " • Next category", () => settingsTab = (settingsTab + 1) % 3);
            var p = S.settings;
            if (settingsTab == 0)
            {
                Toggle("Subtitles", p.subtitles, () => p.subtitles = !p.subtitles); Toggle("Interaction prompts", p.interactPrompts, () => p.interactPrompts = !p.interactPrompts);
                Toggle("Reduced flashes", p.reducedFlashes, () => p.reducedFlashes = !p.reducedFlashes); Toggle("Reduced motion", p.reducedMotion, () => p.reducedMotion = !p.reducedMotion);
                Action("Text size: " + p.textScale.ToString("0.0") + "×", () => p.textScale = p.textScale >= 1.29f ? 1 : p.textScale + .1f);
                Action("Subtitle background: "+Mathf.RoundToInt(p.subtitleBackground*100)+"%",()=>p.subtitleBackground=p.subtitleBackground>=.99f?.35f:Mathf.Clamp01(p.subtitleBackground+.15f));
                if(XRSettings.isDeviceActive)
                {
                    Action("Recenter UI", RecenterUI);
                    Toggle("Smooth turning",p.smoothTurn,()=>p.smoothTurn=!p.smoothTurn);
                    if(p.smoothTurn)Action("Turn speed: "+Mathf.RoundToInt(p.smoothTurnSpeed)+"°/s",()=>p.smoothTurnSpeed=p.smoothTurnSpeed>=119?30:p.smoothTurnSpeed+15);
                    else Action("Snap turn: " + p.snapTurnAngle + "°", () => p.snapTurnAngle=p.snapTurnAngle==15?30:p.snapTurnAngle==30?45:15);
                    Action("Dominant hand: "+(p.leftHanded?"Left":"Right"),()=>{p.leftHanded=!p.leftHanded;activePointer=-1;});
                    Action("Movement direction: "+(p.handRelativeMovement?"Controller":"Head"),()=>p.handRelativeMovement=!p.handRelativeMovement);
                    Action("Play position: "+(p.seatedMode?"Seated":"Standing"),()=>p.seatedMode=!p.seatedMode);
                    Action("Comfort vignette: "+Mathf.RoundToInt(p.vignetteStrength*100)+"%",()=>p.vignetteStrength=p.vignetteStrength>=.99f?0:Mathf.Clamp01(p.vignetteStrength+.25f));
                    Toggle("Toggle grip",p.toggleGrip,()=>p.toggleGrip=!p.toggleGrip);
                }
                Action("Rain detail: " + new[] { "Low", "Medium", "High" }[Mathf.Clamp(p.graphics, 0, 2)], () => p.graphics = (p.graphics + 1) % 3);
            }
            else if (settingsTab == 1)
            { Volume("Master", p.volume, v => p.volume = v); Volume("Speech", p.speechVolume, v => p.speechVolume = v); Volume("Environment", p.environmentVolume, v => p.environmentVolume = v); Volume("Effects", p.effectsVolume, v => p.effectsVolume = v); }
            else Controls();
            Action("Save & back", () => { S.ApplySettings(); S.SetMode(S.settingsReturn); });
        }
        void Toggle(string label, bool value, Action change) { Action(label + ": " + (value ? "On" : "Off"), change); }
        void Volume(string label, float value, Action<float> set)
        { Text(label + "  " + Mathf.RoundToInt(value * 100) + "%", 24); Action("− " + label, () => set(Mathf.Clamp01(value - .1f))); Action("+ " + label, () => set(Mathf.Clamp01(value + .1f))); }
        void BuildBag()
        {
            var bag = S.inventory; Heading("GO-BAG", "Your carried supplies.", bag.BagStatus);
            int occupied = Enumerable.Range(0, SibucaoInventory.Capacity).Count(i => bag.Slot(i));
            MeterCard("BAG SLOTS", occupied + " / " + SibucaoInventory.Capacity + " used", (float)occupied / SibucaoInventory.Capacity, occupied >= SibucaoInventory.Capacity);
            if (!bag.HasBag) MessageCard("NO BAG EQUIPPED", "Find your go-bag before packing supplies for the route.", StormglassTheme.Warning);
            else if (occupied == 0) MessageCard("BAG IS EMPTY", "Pack a useful first aid, water, and communication item when available.", StormglassTheme.Warning);
            for (int i = inventoryPage * 6; i < Mathf.Min(SibucaoInventory.Capacity, inventoryPage * 6 + 6); i++)
            { int index = i; var item = bag.Slot(i); if (item) Action(SibucaoInventory.ItemName(item) + "\n" + bag.ItemStatus(item), () => { bag.Selected = index; }, height: 96); }
            var selected = bag.Slot(bag.Selected);
            MessageCard("SELECTED ITEM", SibucaoInventory.ItemName(selected) + "\n" + bag.ItemStatus(selected), selected ? StormglassTheme.Accent : StormglassTheme.Muted);
            Action("Take out / eat selected", bag.EquipSelected, bag.Slot(bag.Selected)); Action("Drop selected", bag.DropSelected, bag.Slot(bag.Selected));
            Action("Next items (" + (inventoryPage + 1) + "/5)", () => inventoryPage = (inventoryPage + 1) % 5); Action("Close bag", bag.Close);
        }
        void BuildConversation()
        {
            var chapter = Prep.chapter; Heading("HOUSEHOLD CONVERSATION", chapter.StormglassSpeaker, chapter.StormglassReply);
            foreach (var topic in chapter.StormglassTopics) { string t = topic; Action(t, () => chapter.SubmitTranscript(t), height:104); }
            if (chapter.StormglassIsShop) for (int i = 0; i < chapter.products.Length; i++)
            { int index = i; var product = chapter.products[i]; Text(product.description, 22); Action("Buy " + product.item.displayName + " · P" + product.price, () => chapter.Purchase(index), !chapter.Owns(product.item)); }
            if (!XRSettings.isDeviceActive)
            {
                var inputRoot=Rect("Conversation text input",content,new Vector2(624,64));Panel(inputRoot,StormglassTheme.Surface);
                var size=inputRoot.gameObject.AddComponent<LayoutElement>();size.minHeight=size.preferredHeight=64;size.flexibleWidth=1;
                var input=inputRoot.gameObject.AddComponent<TMP_InputField>();input.characterLimit=500;
                var textRoot=Rect("Typed words",inputRoot,Vector2.zero);textRoot.anchorMin=Vector2.zero;textRoot.anchorMax=Vector2.one;textRoot.offsetMin=new Vector2(16,8);textRoot.offsetMax=new Vector2(-16,-8);
                var typed=textRoot.gameObject.AddComponent<TextMeshProUGUI>();typed.font=theme.bodyFont;typed.fontSize=24*S.settings.textScale;typed.color=StormglassTheme.Ink;
                input.textViewport=inputRoot;input.textComponent=typed;input.lineType=TMP_InputField.LineType.SingleLine;
                Text("Type your question above, then send.",18);
                Action("Send question",()=>chapter.SubmitTranscript(input.text));
            }
            Action("End conversation", () => { chapter.Close(); Prep.player.RefreshPanelCursor(); });
        }
        public static string VRText(string text)
        {
            if (!XRSettings.isDeviceActive || string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"\b(WASD|Mouse look|Shift|Space|Ctrl|Esc|E|F|Q|P|I|J|R)\b", m =>
                m.Value == "WASD" ? "Left stick" : m.Value == "Mouse look" ? "Look around" : m.Value == "E" ? "Trigger" : m.Value == "F" ? "Right grip" : m.Value == "P" ? "Left grip" : m.Value == "I" ? "B" : m.Value == "J" ? "X" : m.Value == "Esc" ? "Menu" : m.Value == "Space" ? "A" : m.Value == "Shift" ? "Left stick press" : m.Value == "Ctrl" ? "Right stick press" : m.Value == "Q" ? "Release grip" : "Rotate object");
        }
        void UpdateHud()
        {
            noticePriority=0;notice.gameObject.SetActive(false);
            string obj = Prep ? Prep.player.CurrentObjective : S.missionGuide ? S.missionGuide.Objective : S.NextObjective();
            objective.gameObject.SetActive(objectiveHeld || (!Prep && S.settings.hints && !RouteCardsVisible));
            var floodWarning=!Prep?S.GetComponent<FloodSimulationManager>():null;
            objectiveText.text = "<color=#65DAC2>NEXT STEP</color>\n" + VRText(obj)+
                (objectiveHeld && floodWarning?"\n<size=85%><color=#EAB45D>"+floodWarning.WarningStage+"</color> · "+VRText(floodWarning.RouteCondition)+"</size>":"");
            FitHud(objective,objectiveText,420,100,220,22);
            string action = Prep ? Prep.player.ContextAction() : S.GetComponent<SibucaoUI>()?.ContextAction();
            prompt.gameObject.SetActive(!RouteCardsVisible && S.settings.interactPrompts && !string.IsNullOrEmpty(action)); promptText.text = VRText(action);
            bool hasItemInfo=Prep&&Prep.player.FocusedItemCanShowInfo;
            itemInfoToggle.gameObject.SetActive(S.settings.interactPrompts&&hasItemInfo);
            itemInfoToggleText.text=XRSettings.isDeviceActive?(Prep&&Prep.player.ItemInfoOpen?"LEFT SECONDARY  CLOSE":"LEFT SECONDARY  INFO"):(Prep&&Prep.player.ItemInfoOpen?"R  CLOSE INFO":"R  ITEM INFO");
            FitHud(itemInfoToggle,itemInfoToggleText,330,54,110,18);
            bool showItemInfo=hasItemInfo&&Prep.player.ItemInfoOpen&&!string.IsNullOrEmpty(Prep.player.FocusedItemInformation);
            itemInfo.gameObject.SetActive(showItemInfo);
            itemInfoText.text=showItemInfo?Prep.player.FocusedItemInformation:"";
            string sub = Prep && Prep.RecentMessage ? Prep.message : Time.unscaledTime < S.subtitleUntil ? S.subtitle : "";
            var narration = S.GetComponent<SibucaoNarration>(); if (!Prep && narration && narration.Speaking) sub = narration.Caption;
            if (!Prep && S.home && S.home.neighborWarning && !string.IsNullOrEmpty(S.home.neighborWarning.Subtitle)) sub = S.home.neighborWarning.Subtitle;
            caption.gameObject.SetActive(S.settings.subtitles && !string.IsNullOrEmpty(sub)); captionText.text = VRText(sub);
            var captionImage=caption.GetComponent<Image>();if(captionImage){var c=StormglassTheme.Surface;c.a=S.settings.subtitleBackground;captionImage.color=c;}
            if(!Prep && Time.unscaledTime < S.toastUntil)OfferNotice(S.toastTitle,S.toastText,10);
            var data=S.state.physical;
            bool vitalState=data.health<99||data.stamina<55||data.hunger>55||data.submergedSeconds>0||data.needsRescue;
            vitals.gameObject.SetActive(vitalState);
            vitals.anchoredPosition=RouteCardsVisible?new Vector2(-28,routeCards.anchoredPosition.y+routeCards.sizeDelta.y+24):new Vector2(-28,28);
            vitalsText.fontSize=18*S.settings.textScale;
            vitals.sizeDelta=new Vector2(280,136*S.settings.textScale);
            vitalsText.rectTransform.sizeDelta=vitals.sizeDelta-new Vector2(32,28);
            vitalsText.text="Health  "+data.health.ToString("0")+" / 100\n\n<size=90%>Stamina  "+data.stamina.ToString("0")+"   Hunger  "+data.hunger.ToString("0")+"</size>";
            healthFill.sizeDelta=new Vector2(212*Mathf.Clamp01(data.health/100f),7);
            healthFill.GetComponent<Image>().color=data.health<30?StormglassTheme.Warning:StormglassTheme.Accent;
            if(data.submergedSeconds>30)OfferNotice("LOW BREATH",XRSettings.isDeviceActive?"Hold A to rise.":"Hold Space to rise.",100);
            FitHud(caption,captionText,580,64,260);
            FitHud(prompt,promptText,460,60,260);
            if(showItemInfo)FitItemInformation();
            // Stack contextual details below the actual alert height, including large text.
            PositionItemDetails();
            prompt.localRotation=Quaternion.identity;
            caption.anchoredPosition=RouteCardsVisible?new Vector2(0,routeCards.anchoredPosition.y+routeCards.sizeDelta.y+24):new Vector2(0,28);
            prompt.anchoredPosition=new Vector2(0,caption.gameObject.activeSelf?caption.anchoredPosition.y+caption.sizeDelta.y+18:28);
        }
        void FitItemInformation() => FitHud(itemInfo,itemInfoText,420,110,600,20);
        void FitHud(RectTransform panel,TMP_Text text,float width,float minimum,float maximum,float baseFontSize=24)
        {
            text.enableAutoSizing=false;text.fontSize=baseFontSize*S.settings.textScale;
            float height=Mathf.Clamp(text.GetPreferredValues(text.text,width-40,1000).y+32,minimum,maximum);
            panel.sizeDelta=new Vector2(width,height);text.rectTransform.sizeDelta=new Vector2(width-40,height-28);
        }
        void PositionItemDetails()
        {
            float top=notice.gameObject.activeSelf?28+notice.sizeDelta.y+18:28;
            itemInfoToggle.anchoredPosition=new Vector2(-28,-top);
            itemInfo.anchoredPosition=new Vector2(-28,-top-itemInfoToggle.sizeDelta.y-12);
        }
        void OfferNotice(string title,string body,int priority)
        {
            if(priority<noticePriority||string.IsNullOrWhiteSpace(title+body))return;
            noticePriority=priority;notice.gameObject.SetActive(true);
            var signal=notice.Find("Signal edge");if(signal)signal.GetComponent<Image>().color=priority>=100?StormglassTheme.Danger:priority>=40?StormglassTheme.Warning:StormglassTheme.Accent;noticeText.color=priority>=100?StormglassTheme.Danger:priority>=40?StormglassTheme.Warning:StormglassTheme.Ink;noticeText.text=VRText(title.ToUpperInvariant()+"\n"+body);
            FitHud(notice,noticeText,360,92,220,20);
            // Preparation completion notices arrive after UpdateHud; restack in the same frame.
            PositionItemDetails();
        }
        void UpdateScrollControls()
        {
            if(!scrollUpButton||!scrollDownButton||!scrollPositionText||!modal.gameObject.activeSelf)return;
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=lastVisibleSelection)
            {
                lastVisibleSelection=selected;
                if(selected&&selected.transform.IsChildOf(content))
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,selected.transform);
                    float shift=bounds.max.y>viewport.rect.yMax?viewport.rect.yMax-bounds.max.y:
                        bounds.min.y<viewport.rect.yMin?viewport.rect.yMin-bounds.min.y:0;
                    var contentPosition=content.anchoredPosition;contentPosition.y=Mathf.Clamp(contentPosition.y+shift,0,Mathf.Max(0,content.rect.height-viewport.rect.height));content.anchoredPosition=contentPosition;
                }
            }
            bool canScroll=content.rect.height>viewport.rect.height+4;float position=Mathf.Clamp01(scroll.verticalNormalizedPosition);
            scrollUpButton.interactable=canScroll&&position<.995f;scrollDownButton.interactable=canScroll&&position>.005f;
            scrollPositionText.text=!canScroll?"ALL VISIBLE":position>.92f?"TOP":position<.08f?"END":Mathf.RoundToInt((1-position)*100)+"%";
        }
        void ReadShortcuts()
        {
            if (!XRSettings.isDeviceActive)
            {
                bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame||Gamepad.current!=null&&Gamepad.current.buttonEast.wasPressedThisFrame;
                if(back&&locationsOpen){locationsOpen=false;rebuild=true;}
                else if(back&&S.Mode==SibucaoMode.About)S.SetMode(SibucaoMode.Menu);
                // Keyboard Escape is handled by the session; mirror it for gamepad B.
                else if(Gamepad.current!=null&&Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    if(S.inventory&&S.inventory.IsOpen)S.inventory.Close();
                    else if(S.missionGuide&&S.missionGuide.IsOpen)S.missionGuide.Close();
                    else if(S.Mode==SibucaoMode.Settings){S.ApplySettings();S.SetMode(S.settingsReturn);}
                    else if(S.Mode==SibucaoMode.Confirm)S.SetMode(S.confirmReturn);
                    else if(S.Mode==SibucaoMode.ResumeSelection)S.CancelResumeSelection();
                    else if(S.Mode==SibucaoMode.Playing)S.Pause();
                    else if(S.Mode==SibucaoMode.Paused||S.Mode==SibucaoMode.Recovery)S.SetMode(SibucaoMode.Playing);
                }
                if(Gamepad.current!=null&&Gamepad.current.startButton.wasPressedThisFrame)
                {if(S.Mode==SibucaoMode.Playing)S.Pause();else if(S.Mode==SibucaoMode.Paused)S.SetMode(SibucaoMode.Playing);}
                return;
            }
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            left.TryGetFeatureValue(CommonUsages.menuButton, out bool menu); left.TryGetFeatureValue(CommonUsages.secondaryButton, out objectiveHeld);
            left.TryGetFeatureValue(CommonUsages.primaryButton, out bool guide);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.secondaryButton, out bool bag);
            bool prepShortcuts = Prep && S.Mode == SibucaoMode.Preparation && !Prep.CoachingChoicePending && screenKey != "Conversation" && screenKey != "Departure";
            if(guide&&!guideHeld)
            {
                if(prepShortcuts) { if(Prep.player.GuideOpen)Prep.player.CloseDesktopPanels();else Prep.player.ToggleGuide(); }
                else if(S.Mode==SibucaoMode.Playing&&S.missionGuide) { if(S.missionGuide.IsOpen)S.missionGuide.Close();else S.missionGuide.Open(); }
            }
            if(bag&&!bagHeld)
            {
                if(prepShortcuts)Prep.player.ToggleBagPanel();
                else if(S.Mode==SibucaoMode.Playing&&S.inventory) { if(S.inventory.IsOpen)S.inventory.Close();else S.inventory.Open(); }
            }
            guideHeld=guide;bagHeld=bag;
            if (menu && !menuHeld)
            {
                if (S.Mode == SibucaoMode.Settings) { S.ApplySettings(); S.SetMode(S.settingsReturn); }
                else if (Prep) { if (Prep.player.ReadingPanel) Prep.player.CloseDesktopPanels(); else S.OpenSettings(); }
                else if (S.inventory && S.inventory.IsOpen) S.inventory.Close();
                else if (S.missionGuide && S.missionGuide.IsOpen) S.missionGuide.Close();
                else if (S.Mode == SibucaoMode.Playing) S.Pause();
                else if (S.Mode == SibucaoMode.Paused) S.SetMode(SibucaoMode.Playing);
            }
            menuHeld = menu;
        }
        void CreateRays()
        {
            rays = new LineRenderer[2]; cursors = new GameObject[2]; rayMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Stormglass controller ray " + i); rays[i] = go.AddComponent<LineRenderer>(); rays[i].sharedMaterial = rayMaterial;
                rays[i].positionCount = 2; rays[i].startWidth = .002f; rays[i].endWidth = .001f; rays[i].startColor = rays[i].endColor = StormglassTheme.Accent; rays[i].enabled = false;
                cursors[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere); cursors[i].name = "Stormglass selection cursor"; Destroy(cursors[i].GetComponent<Collider>());
                cursors[i].GetComponent<Renderer>().sharedMaterial = rayMaterial; cursors[i].transform.localScale = Vector3.one * .012f; cursors[i].SetActive(false);
            }
        }
        void UpdatePointers(bool xr)
        {
            routePointerHovered = false;
            if(!xr||(!modal.gameObject.activeSelf&&!RouteCardsVisible))activePointer=-1;
            for (int i = 0; i < 2; i++)
            {
                bool valid = xr && (modal.gameObject.activeSelf || RouteCardsVisible);
                Ray ray = default; bool trigger = false;
                valid &= StormglassPointer.TrackedRay(i == 0 ? XRNode.RightHand : XRNode.LeftHand, Eye.transform.parent, out ray, out trigger);
                bool triggerDown=trigger&&!pointerTriggerHeld[i];pointerTriggerHeld[i]=trigger;
                if(triggerDown&&activePointer!=i){if(activePointer>=0)pointers[activePointer].Reset();activePointer=i;}
                if(activePointer<0&&valid)activePointer=S.settings.leftHanded?1:0;
                valid&=activePointer==i;
                if (!valid) { pointers[i].Reset(); rays[i].enabled = false; cursors[i].SetActive(false); continue; }
                var previous=pointers[i].Hovered;
                bool hit = pointers[i].Process(ray, trigger, PointerButtons, PointerViewport, out Vector3 point);
                if (RouteCardsVisible && hit) routePointerHovered = true;
                rays[i].enabled = true; rays[i].SetPosition(0, ray.origin); rays[i].SetPosition(1, point);
                cursors[i].SetActive(hit); if (hit) cursors[i].transform.position = point;
                var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.RightHand:XRNode.LeftHand);
                if(previous!=pointers[i].Hovered&&pointers[i].Hovered)device.SendHapticImpulse(0,.08f,.025f);
                if(pointers[i].ClickedThisFrame)device.SendHapticImpulse(0,.22f,.045f);
                if(modal.gameObject.activeSelf&&device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick)&&Mathf.Abs(stick.y)>.55f&&Time.unscaledTime>=nextStickScroll)
                {scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+Mathf.Sign(stick.y)*.16f);nextStickScroll=Time.unscaledTime+.12f;}
            }
        }
    }
    public sealed class StormglassFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Sprite sprite;
        Outline outline;
        Vector3 baseScale=Vector3.one;
        void Awake(){baseScale=transform.localScale;}
        void Focus(bool value,bool sound=false)
        {
            if(value&&!outline){outline=gameObject.AddComponent<Outline>();outline.effectColor=StormglassTheme.Ink;outline.effectDistance=new Vector2(5,-5);}
            if(outline)outline.enabled=value;transform.localScale=value?baseScale*1.025f:baseScale;
            if(value&&sound&&StormglassUI.Active)StormglassUI.Active.PlayUIHover();
        }
        public void OnPointerEnter(PointerEventData e){Focus(true,true);}
        public void OnPointerExit(PointerEventData e){if(EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject)return;Focus(false);}
        public void OnSelect(BaseEventData e){Focus(true,true);}
        public void OnDeselect(BaseEventData e){Focus(false);}
        void OnDisable(){Focus(false);}
    }
}
