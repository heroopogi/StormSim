using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using StormSim.UI;
using Keyboard=UnityEngine.InputSystem.Keyboard;
using Gamepad=UnityEngine.InputSystem.Gamepad;

namespace StormSim.Sibucao
{
    public enum StormSimLaunchMode { None, NewSimulation, Continue }

    public static class StormSimLaunchRequest
    {
        static string scene;
        static StormSimLaunchMode mode;
        public static void Set(string target,StormSimLaunchMode launchMode){scene=target;mode=launchMode;}
        public static StormSimLaunchMode Consume(string activeScene)
        {
            if(string.IsNullOrEmpty(scene)||scene!=activeScene)return StormSimLaunchMode.None;
            var result=mode;scene=null;mode=StormSimLaunchMode.None;return result;
        }
    }

    [DefaultExecutionOrder(-900)]
    public sealed class StormSimMainMenuScene : MonoBehaviour
    {
        public const string SceneName="STORMSIM_MainMenu";
        sealed class Area
        {
            public string name,scene,savePrefix,description,preview;
            public Area(string n,string s,string p,string d,string image){name=n;scene=s;savePrefix=p;description=d;preview=image;}
        }
        sealed class SaveSummary
        {
            public int areaIndex;public string path,slot;public DateTime changed;public RunState state;
        }
        enum Page { Home,Locations,Settings,Accessibility,Audio,Desktop,VR,VRComfort,VRControls,Onboarding,HowTo,About,ConfirmExit }
        static readonly Area[] Areas={
            new Area("Sibucao","SIBUCAO_RAIN","sibucao-rain","Coastal streets, households, and the school evacuation route.","Stormglass/Locations/Sibucao"),
            new Area("Riverside","RIVERSIDE_RAIN","riverside-rain","A low-lying riverside community with rising-water hazards.","Stormglass/Locations/Riverside"),
            new Area("Baybayon","BAYBAYON_RAIN","baybayon-rain","A shoreline community exposed to wind, rain, and coastal flooding.","Stormglass/Locations/Baybayon")};

        StormglassTheme theme;
        Canvas canvas;
        StormSimMainMenu authoredMenu;
        [SerializeField,Tooltip("Use the compact Signal Station prefab instead of the full-screen menu.")]
        bool useCompactHome;
        bool AuthoredHome=>useCompactHome&&authoredMenu&&page==Page.Home;
        RectTransform content;
        RectTransform readingViewport;
        ScrollRect readingScroll;
        GameObject lastReadingSelection;
        int actionFocusIndex=-1;
        Page actionFocusPage;
        Camera eye;
        Transform trackingOrigin;
        RectTransform animatedBackdrop;
        readonly List<RectTransform> rainBands=new List<RectTransform>();
        readonly List<Texture2D> generatedTextures=new List<Texture2D>();
        readonly Sprite[] locationPreviews=new Sprite[3];
        readonly List<Button> buttons=new List<Button>();
        StormglassPointer[] pointers;
        LineRenderer[] rays;
        GameObject[] cursors;
        Material rayMaterial;
        AudioSource uiSound;
        AudioClip uiHover,uiActivate;
        Page page;
        int selectedArea,onboardingStep;
        bool anchored,xrWasActive,startAfterLocationChoice,onboardingLaunchAfter;
        readonly bool[] triggerHeld=new bool[2];
        int activePointer=-1,lastClickFrame=-1;
        SibucaoSettings settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(SceneManager.GetActiveScene().name==SceneName&&!FindAnyObjectByType<StormSimMainMenuScene>())
                new GameObject("StormSim Main Menu").AddComponent<StormSimMainMenuScene>();
        }
        public static bool Available=>Application.CanStreamedLevelBeLoaded(SceneName);
        public IReadOnlyList<Button> Buttons=>buttons;
        public string PageKey=>page.ToString();
        public static void Load()
        {
            if(Available)StormSim.UI.StormglassSceneTransition.Load(SceneName);
            else if(SibucaoSession.Instance)SibucaoSession.Instance.SetMode(SibucaoMode.Menu);
        }

        void Awake()
        {
            theme=Resources.Load<StormglassTheme>("Stormglass/Theme");
            if(!theme||!theme.headingFont||!theme.bodyFont||!theme.PanelSprite){Debug.LogError("Main Menu: Stormglass theme is missing.");enabled=false;return;}
            try{settings=PlayerPrefs.HasKey("SibucaoSettings")?JsonUtility.FromJson<SibucaoSettings>(PlayerPrefs.GetString("SibucaoSettings")):new SibucaoSettings();}catch{settings=new SibucaoSettings();}
            if(settings==null)settings=new SibucaoSettings();selectedArea=Mathf.Clamp(PlayerPrefs.GetInt("StormSimMenuArea",0),0,Areas.Length-1);
            // Keep the compact prefab available as an optional home-screen layout.
            authoredMenu=FindAnyObjectByType<StormSimMainMenu>();
            if(authoredMenu)authoredMenu.BindStandalone(this);
            uiSound=gameObject.AddComponent<AudioSource>();uiSound.playOnAwake=false;uiSound.spatialBlend=0;
            uiHover=MakeUITone("Main menu focus",520,760,.045f,.055f);uiActivate=MakeUITone("Main menu activate",420,920,.075f,.11f);
            CreateCamera();CreateCanvas();CreatePointers();Build();
            if(authoredMenu){authoredMenu.canvas.worldCamera=eye;eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=StormglassTheme.Background;}
        }
        public void StartFromAuthoredMenu(){startAfterLocationChoice=true;Show(Page.Locations);}
        public void OpenAuthoredLocations(){startAfterLocationChoice=false;Show(Page.Locations);}
        public void OpenAuthoredSettings(){Show(Page.Settings);}
        public void ContinueFromAuthoredMenu()
        {
            var recent=LatestSave();
            if(recent==null){Build();return;}
            selectedArea=recent.areaIndex;PlayerPrefs.SetInt("StormSimMenuArea",selectedArea);PlayerPrefs.Save();
            Launch(StormSimLaunchMode.Continue);
        }
        void CreateCamera()
        {
            var existingOrigin=FindAnyObjectByType<XROrigin>();
            var existingCamera=Camera.main;
            if(existingOrigin&&existingCamera)
            {
                trackingOrigin=existingOrigin.transform;eye=existingCamera;existingOrigin.Camera=eye;return;
            }
            var rig=new GameObject("XR Origin - Main Menu");trackingOrigin=rig.transform;var origin=rig.AddComponent<XROrigin>();
            var offset=new GameObject("Camera Floor Offset");offset.transform.SetParent(rig.transform,false);offset.transform.localPosition=Vector3.up*1.6f;origin.CameraFloorOffsetObject=offset;
            var go=new GameObject("Main Menu Camera");go.tag="MainCamera";go.transform.SetParent(offset.transform,false);eye=go.AddComponent<Camera>();eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=StormglassTheme.Background;eye.nearClipPlane=.03f;eye.farClipPlane=30;go.AddComponent<AudioListener>();origin.Camera=eye;
            var driver=go.AddComponent<TrackedPoseDriver>();driver.positionInput=new InputActionProperty(new InputAction("Head position",InputActionType.Value,"<XRHMD>/centerEyePosition"));driver.rotationInput=new InputActionProperty(new InputAction("Head rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation"));driver.trackingStateInput=new InputActionProperty(new InputAction("Tracking state",InputActionType.Value,"<XRHMD>/trackingState"));
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;return r;}
        void Panel(RectTransform r,Color color,bool raycast=false)
        {var image=r.gameObject.AddComponent<Image>();image.sprite=theme.PanelSprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=raycast;}
        void CreateCanvas()
        {
            if(!EventSystem.current){var events=new GameObject("Main Menu EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            var root=new GameObject("StormSim Main Menu Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=root.GetComponent<Canvas>();canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var background=Rect("Storm background",root.transform,new Vector2(1920,1080));background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.offsetMin=background.offsetMax=Vector2.zero;Panel(background,StormglassTheme.Background);
            animatedBackdrop=Rect("Animated coastal storm",background,new Vector2(2040,1160));animatedBackdrop.anchorMin=animatedBackdrop.anchorMax=new Vector2(.5f,.5f);var backdrop=animatedBackdrop.gameObject.AddComponent<Image>();backdrop.sprite=CoastalPreview(-1,1024,576);backdrop.preserveAspect=false;backdrop.color=new Color(.48f,.62f,.66f,.42f);backdrop.raycastTarget=false;
            var horizon=Rect("Rain horizon",background,new Vector2(1920,330));horizon.anchorMin=new Vector2(0,0);horizon.anchorMax=new Vector2(1,0);horizon.pivot=new Vector2(.5f,0);horizon.offsetMin=Vector2.zero;horizon.offsetMax=new Vector2(0,330);Panel(horizon,new Color(.07f,.20f,.27f,.88f));
            for(int i=0;i<7;i++){var band=Rect("Rain band "+(i+1),background,new Vector2(3,1160));band.anchorMin=band.anchorMax=new Vector2(0,.5f);band.anchoredPosition=new Vector2(170+i*270,0);var rain=band.gameObject.AddComponent<Image>();rain.color=new Color(.55f,.83f,.87f,.12f);rain.raycastTarget=false;band.localRotation=Quaternion.Euler(0,0,-10);rainBands.Add(band);}
            var brand=Rect("Brand",background,new Vector2(620,760));brand.anchorMin=brand.anchorMax=new Vector2(0,.5f);brand.pivot=new Vector2(0,.5f);brand.anchoredPosition=new Vector2(110,0);
            Label(brand,"STORMSIM",76,true,StormglassTheme.Ink,new Vector2(600,100),new Vector2(0,210));
            Label(brand,"WEATHER THE STORM.",30,true,StormglassTheme.Accent,new Vector2(600,50),new Vector2(0,145));
            Label(brand,"Prepare  /  Evacuate  /  Recover",25,false,StormglassTheme.Muted,new Vector2(600,45),new Vector2(0,92));
            Label(brand,"A typhoon-preparedness simulation about households, routes, and community decisions.",25,false,StormglassTheme.Ink,new Vector2(520,150),new Vector2(0,-25),TextAlignmentOptions.TopLeft);
            JourneyStep(brand, "01", "PREPARE", "Pack supplies and ready your household.", -120);
            JourneyStep(brand, "02", "EVACUATE", "Read the route. Reach the school.", -200);
            JourneyStep(brand, "03", "RECOVER", "Review your decisions and their outcomes.", -280);
            Label(brand,"FICTIONAL TRAINING SCENARIO  •  NOT A LIVE WARNING SERVICE",17,false,StormglassTheme.Warning,new Vector2(570,55),new Vector2(0,-370),TextAlignmentOptions.BottomLeft);
            var panel=Rect("Menu panel",background,new Vector2(760,900));panel.anchorMin=panel.anchorMax=new Vector2(1,.5f);panel.pivot=new Vector2(1,.5f);panel.anchoredPosition=new Vector2(-100,0);Panel(panel,new Color(StormglassTheme.Surface.r,StormglassTheme.Surface.g,StormglassTheme.Surface.b,.97f));
            readingViewport=Rect("Reading viewport",panel,new Vector2(680,790));readingViewport.anchoredPosition=new Vector2(0,20);readingViewport.gameObject.AddComponent<RectMask2D>();
            content=Rect("Menu content",readingViewport,new Vector2(660,0));content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            readingScroll=panel.gameObject.AddComponent<ScrollRect>();readingScroll.viewport=readingViewport;readingScroll.content=content;readingScroll.horizontal=false;readingScroll.movementType=ScrollRect.MovementType.Clamped;readingScroll.scrollSensitivity=45;
            Label(panel,"Scroll to read • Mouse wheel / controller stick",16,false,StormglassTheme.Muted,new Vector2(660,30),new Vector2(0,-415),TextAlignmentOptions.Center);
        }
        void JourneyStep(Transform parent,string number,string title,string detail,float y)
        {
            var step=Rect("Journey " + number,parent,new Vector2(560,70));step.anchoredPosition=new Vector2(0,y);
            var badge=Rect("Step number",step,new Vector2(52,52));badge.anchoredPosition=new Vector2(-250,0);Panel(badge,StormglassTheme.Subtle);
            Label(badge,number,22,true,StormglassTheme.Accent,new Vector2(48,48),alignment:TextAlignmentOptions.Center);
            Label(step,title,19,true,StormglassTheme.Ink,new Vector2(450,28),new Vector2(20,16));
            Label(step,detail,17,false,StormglassTheme.Muted,new Vector2(450,35),new Vector2(20,-16));
        }
        TMP_Text Label(Transform parent,string value,float size,bool heading,Color color,Vector2 box,Vector2 position=default,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
        {
            var r=Rect("Text • "+value.Split('\n')[0],parent,box);r.anchoredPosition=position;var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=heading?theme.headingFont:theme.bodyFont;t.fontSize=size*settings.textScale;t.color=color;t.text=value;t.alignment=alignment;t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;return t;
        }
        void FlowText(string value,float size=22,bool heading=false,Color? color=null,float height=48)
        {var t=Label(content,value,size,heading,color??StormglassTheme.Ink,new Vector2(660,height));var le=t.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=Mathf.Max(height,t.GetPreferredValues(value,660,Mathf.Infinity).y+8);}
        Button ActionButton(string label,Action action,bool enabled=true,bool primary=true,float height=68)
        {
            var r=Rect("Action • "+label.Split('\n')[0],content,new Vector2(660,height));Panel(r,primary?StormglassTheme.Accent:StormglassTheme.Subtle,true);var le=r.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=height*settings.textScale;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.interactable=enabled;b.navigation=new Navigation{mode=Navigation.Mode.Automatic};var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white;colors.selectedColor=new Color(.9f,.96f,.96f,1);colors.pressedColor=new Color(.72f,.82f,.82f,1);colors.disabledColor=new Color(.45f,.5f,.52f,1);b.colors=colors;
            var text=Label(r,label,23,false,enabled?(primary?StormglassTheme.OnAccent:StormglassTheme.Ink):StormglassTheme.Muted,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(18,6);text.rectTransform.offsetMax=new Vector2(-18,-6);text.enableAutoSizing=true;text.fontSizeMin=17*settings.textScale;text.fontSizeMax=23*settings.textScale;
            r.gameObject.AddComponent<StormglassFocus>().sprite=theme.PanelSprite;var sound=r.gameObject.AddComponent<StormSimMenuAudioFocus>();sound.owner=this;b.onClick.AddListener(()=>{if(lastClickFrame==Time.frameCount)return;lastClickFrame=Time.frameCount;actionFocusIndex=buttons.IndexOf(b);actionFocusPage=page;PlayMenuActivate();action();});buttons.Add(b);return b;
        }
        Button LocationButton(Area area,int index)
        {
            bool available=Application.CanStreamedLevelBeLoaded(area.scene);var r=Rect("Location • "+area.name,content,new Vector2(660,142));Panel(r,index==selectedArea?StormglassTheme.Accent:StormglassTheme.Subtle,true);var le=r.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=142;
            var preview=Rect("Preview",r,new Vector2(190,118));preview.anchorMin=preview.anchorMax=new Vector2(0,.5f);preview.pivot=new Vector2(0,.5f);preview.anchoredPosition=new Vector2(12,0);var image=preview.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>(area.preview)??(locationPreviews[index]?locationPreviews[index]:locationPreviews[index]=CoastalPreview(index,256,128));image.preserveAspect=false;image.color=available?Color.white:new Color(.35f,.38f,.4f,1);image.raycastTarget=false;
            string heading=area.name+(index==selectedArea?"  •  Selected":"");var title=Label(r,heading,23,true,index==selectedArea?StormglassTheme.OnAccent:StormglassTheme.Ink,new Vector2(420,46),new Vector2(105,39));title.alignment=TextAlignmentOptions.Left;
            var description=Label(r,area.description,18,false,index==selectedArea?StormglassTheme.OnAccent:StormglassTheme.Muted,new Vector2(420,65),new Vector2(105,-18),TextAlignmentOptions.TopLeft);description.enableAutoSizing=true;description.fontSizeMin=14*settings.textScale;description.fontSizeMax=18*settings.textScale;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.interactable=available;b.navigation=new Navigation{mode=Navigation.Mode.Automatic};r.gameObject.AddComponent<StormglassFocus>().sprite=theme.PanelSprite;var sound=r.gameObject.AddComponent<StormSimMenuAudioFocus>();sound.owner=this;b.onClick.AddListener(()=>{if(lastClickFrame==Time.frameCount)return;lastClickFrame=Time.frameCount;PlayMenuActivate();ChooseArea(index);});buttons.Add(b);return b;
        }
        Sprite CoastalPreview(int kind,int width,int height)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=kind<0?"StormSim animated coast":"StormSim "+Areas[kind].name+" preview",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var pixels=new Color[width*height];
            var skyTop=new Color(.035f,.09f,.13f,1);var skyLow=new Color(.16f,.32f,.36f,1);var land=new Color(.16f,.25f,.22f,1);var water=new Color(.055f,.27f,.36f,1);var road=new Color(.26f,.29f,.27f,1);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float nx=x/(float)(width-1),ny=y/(float)(height-1);Color color=Color.Lerp(skyLow,skyTop,Mathf.Floor(Mathf.InverseLerp(.36f,1,ny)*5)/5f);
                if(kind==1){float river=.16f+.05f*Mathf.Sin(ny*8);color=Mathf.Abs(nx-.52f)<river&&ny<.64f?water:ny<.38f?land:color;}
                else if(kind==2)color=ny<.42f+nx*.08f?water:color;
                else {if(ny<.28f)color=water;else if(ny<.37f)color=land;if(kind==0&&ny<.45f&&Mathf.Abs(nx-.52f)<.105f+ny*.08f)color=road;}
                float rain=Mathf.Repeat(x*13+y*5,83);if(rain<1.2f&&ny>.22f)color=Color.Lerp(color,new Color(.55f,.8f,.84f,1),.18f);pixels[y*width+x]=color;
            }
            void Block(float px,float py,float pw,float ph,Color color){int x0=Mathf.RoundToInt(px*width),x1=Mathf.RoundToInt((px+pw)*width),y0=Mathf.RoundToInt(py*height),y1=Mathf.RoundToInt((py+ph)*height);for(int y=Mathf.Max(0,y0);y<Mathf.Min(height,y1);y++)for(int x=Mathf.Max(0,x0);x<Mathf.Min(width,x1);x++)pixels[y*width+x]=color;}
            var wall=new Color(.62f,.57f,.43f,1);var roof=new Color(.25f,.14f,.12f,1);int count=kind<0?12:6;for(int i=0;i<count;i++){float x=.04f+i*(.91f/count),y=.32f+(i%3)*.025f;Block(x,y,.052f,.105f,wall);Block(x-.008f,y+.102f,.068f,.025f,roof);Block(x+.018f,y+.02f,.016f,.035f,new Color(.07f,.13f,.14f,1));}
            if(kind==2){Block(0,.25f,1,.025f,new Color(.76f,.69f,.48f,1));}else if(kind==1){Block(.30f,.29f,.44f,.025f,new Color(.43f,.36f,.27f,1));}
            texture.SetPixels(pixels);texture.Apply(false,true);generatedTextures.Add(texture);return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100);
        }
        void Clear(){for(int i=content.childCount-1;i>=0;i--){content.GetChild(i).gameObject.SetActive(false);Destroy(content.GetChild(i).gameObject);}buttons.Clear();}
        void Build()
        {
            Clear();var area=Areas[selectedArea];
            if(authoredMenu)
            {
                authoredMenu.SetStandaloneVisible(AuthoredHome);
                canvas.gameObject.SetActive(!AuthoredHome);
                if(AuthoredHome)
                {
                    authoredMenu.RefreshStandaloneHome(area.name,settings.textScale,LatestSave()!=null);
                    buttons.Add(authoredMenu.locations);buttons.Add(authoredMenu.play);buttons.Add(authoredMenu.settings);
                    buttons.Add(authoredMenu.ContinueButton);
                    if(EventSystem.current)EventSystem.current.SetSelectedGameObject(authoredMenu.play.gameObject);
                    Canvas.ForceUpdateCanvases();return;
                }
            }
            if(page==Page.Home)
            {
                FlowText("MAIN MENU",17,false,StormglassTheme.Accent,30);FlowText("Your next journey",38,true,height:55);FlowText("Training location  /  "+area.name,21,false,StormglassTheme.Muted,36);
                var recent=LatestSave();ActionButton(recent!=null?"Continue saved run\n"+SaveLabel(recent):"Continue saved run\nNo checkpoint or manual save exists",()=>{selectedArea=recent.areaIndex;PlayerPrefs.SetInt("StormSimMenuArea",selectedArea);PlayerPrefs.Save();Launch(StormSimLaunchMode.Continue);},recent!=null,recent!=null,88);
                ActionButton("Start preparation",()=>{startAfterLocationChoice=true;Show(Page.Locations);},true,true,68);ActionButton("Choose training location",()=>{startAfterLocationChoice=false;Show(Page.Locations);},true,false,62);ActionButton("Comfort & settings",()=>Show(Page.Settings),true,false,62);ActionButton("Before you begin / Controls",()=>Show(Page.HowTo),true,false,62);ActionButton("About & credits",()=>Show(Page.About),true,false,62);
                ActionButton(XRSettings.isDeviceActive?"Recenter UI":"Exit game",XRSettings.isDeviceActive?(Action)Recenter:()=>Show(Page.ConfirmExit),true,false,58);
            }
            else if(page==Page.Locations)
            {
                FlowText("TRAINING LOCATIONS",17,false,StormglassTheme.Accent,30);FlowText(startAfterLocationChoice?"Where will you train?":"Choose your community",36,true,height:52);FlowText(startAfterLocationChoice?"Select a location to begin with household preparation.":"Each community keeps separate manual saves and checkpoints.",19,false,StormglassTheme.Muted,42);
                for(int i=0;i<Areas.Length;i++)LocationButton(Areas[i],i);
                ActionButton("Back",Back,true,false,58);
            }
            else if(page==Page.Settings)BuildSettings();
            else if(page==Page.Accessibility)BuildAccessibility();
            else if(page==Page.Audio)BuildAudio();
            else if(page==Page.Desktop)BuildDesktop();
            else if(page==Page.VR)BuildVR();
            else if(page==Page.VRComfort)BuildVRComfort();
            else if(page==Page.VRControls)BuildVRControls();
            else if(page==Page.Onboarding)BuildOnboarding();
            else if(page==Page.HowTo)
            {
                FlowText("HOW TO PLAY",17,false,StormglassTheme.Accent,30);FlowText("Prepare. Evacuate. Recover.",36,true,height:55);FlowText("1  Prepare your household and go-bag before conditions worsen.\n\n2  Travel toward the school, read the environment, and help residents without creating new risk.\n\n3  Review how supplies, routes, and decisions affected the outcome.",22,false,StormglassTheme.Ink,285);FlowText(XRSettings.isDeviceActive?"VR: Point with the active controller and release Trigger to select. Use the Menu button to pause or go back.":"Desktop: WASD to move, mouse to look, E to use, F to carry, and Escape to pause or go back.",19,false,StormglassTheme.Muted,90);ActionButton("Back",()=>Show(Page.Home),true,false);
            }
            else if(page==Page.About)
            {
                FlowText("ABOUT & CREDITS",17,false,StormglassTheme.Accent,30);FlowText("StormSim",40,true,height:54);FlowText("Version "+Application.version+"  •  Build "+Application.buildGUID,17,false,StormglassTheme.Muted,32);
                FlowText("A University of St. La Salle thesis simulation for disaster-preparedness learning. Developed by the StormSim thesis team with guidance from academic advisers and preparedness reviewers.",20,false,StormglassTheme.Ink,112);
                FlowText("EDUCATIONAL USE",16,true,StormglassTheme.Warning,26);FlowText("The communities and events are fictional training scenarios. StormSim is not a live warning service and does not replace official advisories, evacuation orders, or professional emergency guidance.",18,false,StormglassTheme.Ink,100);
                FlowText("ACKNOWLEDGEMENTS",16,true,StormglassTheme.Accent,26);FlowText("Built with Unity and TextMesh Pro. Mapping technology and data include Cesium and OpenStreetMap. Interface typography uses Inter and Sora. Font licenses and third-party notices are included with the project distribution.",17,false,StormglassTheme.Muted,105);ActionButton("Back",Back,true,false,58);
            }
            else
            {
                FlowText("EXIT STORMSIM?",17,false,StormglassTheme.Warning,30);FlowText("Leave the simulation",38,true,height:55);FlowText("Any progress since the last checkpoint or manual save will be lost.",22,false,StormglassTheme.Ink,100);ActionButton("Exit StormSim",Application.Quit,true,true,72);ActionButton("Cancel",()=>Show(Page.Home),true,false,68);
            }
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);readingScroll.verticalNormalizedPosition=1;lastReadingSelection=null;
            if(!XRSettings.isDeviceActive&&EventSystem.current){var first=actionFocusPage==page&&actionFocusIndex>=0&&actionFocusIndex<buttons.Count&&buttons[actionFocusIndex].IsInteractable()?buttons[actionFocusIndex]:buttons.FirstOrDefault(b=>b&&b.IsInteractable());if(first)EventSystem.current.SetSelectedGameObject(first.gameObject);}actionFocusIndex=-1;
        }
        void BuildSettings()
        {
            FlowText("COMFORT & SETTINGS",17,false,StormglassTheme.Accent,30);FlowText("Make StormSim work for you",34,true,height:55);FlowText("Settings are shared by the menu, preparation, evacuation, and reflection phases.",19,false,StormglassTheme.Muted,52);
            ActionButton("Accessibility & text\nSubtitles, prompts, motion, flashes, and text size",()=>Show(Page.Accessibility),true,false,86);
            ActionButton("Audio levels\nMaster, speech, environment, and effects",()=>Show(Page.Audio),true,false,86);
            ActionButton(XRSettings.isDeviceActive?"VR comfort & controls\nTurning, dominant hand, grip, and recentering":"Desktop graphics & controls\nDisplay, quality, view, mouse, and key reference",()=>Show(XRSettings.isDeviceActive?Page.VR:Page.Desktop),true,false,86);
            ActionButton("Before you begin / Controls",()=>Show(Page.HowTo),true,false,62);
            ActionButton("About & credits",()=>Show(Page.About),true,false,62);
            ActionButton("Exit game",()=>Show(Page.ConfirmExit),true,false,62);
            ActionButton("Save & back",()=>Show(Page.Home),true,true,66);
        }
        void BuildAccessibility()
        {
            SettingsHeading("ACCESSIBILITY & TEXT","Read and respond comfortably");
            ToggleButton("Subtitles",()=>settings.subtitles,v=>settings.subtitles=v);ToggleButton("Interaction prompts",()=>settings.interactPrompts,v=>settings.interactPrompts=v);ToggleButton("Guidance hints",()=>settings.hints,v=>settings.hints=v);ToggleButton("Reduced motion",()=>settings.reducedMotion,v=>settings.reducedMotion=v);ToggleButton("Reduced flashes",()=>settings.reducedFlashes,v=>settings.reducedFlashes=v);
            ActionButton("Text size: "+settings.textScale.ToString("0.0")+"×",()=>{settings.textScale=settings.textScale>=1.29f?1:settings.textScale+.1f;SaveSettings();Build();},true,false,58);
            ActionButton("Subtitle background: "+Mathf.RoundToInt(settings.subtitleBackground*100)+"%",()=>{settings.subtitleBackground=settings.subtitleBackground>=.99f?.35f:Mathf.Clamp01(settings.subtitleBackground+.15f);SaveSettings();Build();},true,false,58);
            ActionButton("Back to settings",()=>Show(Page.Settings),true,true,62);
        }
        void BuildAudio()
        {
            SettingsHeading("AUDIO LEVELS","Select a control to increase it by 10%");
            VolumeButton("Master",()=>settings.volume,v=>settings.volume=v);VolumeButton("Speech",()=>settings.speechVolume,v=>settings.speechVolume=v);VolumeButton("Rain, wind & ambience",()=>settings.environmentVolume,v=>settings.environmentVolume=v);VolumeButton("Interactions & footsteps",()=>settings.effectsVolume,v=>settings.effectsVolume=v);ActionButton("Back to settings",()=>Show(Page.Settings),true,true,62);
        }
        void BuildDesktop()
        {
            SettingsHeading("DESKTOP GRAPHICS & CONTROLS","Display and mouse comfort");
            ToggleButton("Fullscreen",()=>settings.fullscreen,v=>settings.fullscreen=v);
            ActionButton("Graphics quality: "+new[]{"Low","Medium","High"}[Mathf.Clamp(settings.graphics,0,2)],()=>{settings.graphics=(settings.graphics+1)%3;SaveSettings();Build();},true,false,58);
            ActionButton("Field of view: "+Mathf.RoundToInt(settings.fieldOfView)+"°",()=>{settings.fieldOfView=settings.fieldOfView>=100?60:settings.fieldOfView+10;SaveSettings();Build();},true,false,58);
            ActionButton("Look sensitivity: "+settings.sensitivity.ToString("0.00"),()=>{settings.sensitivity=settings.sensitivity>=.29f?.05f:settings.sensitivity+.04f;SaveSettings();Build();},true,false,58);ToggleButton("Invert vertical look",()=>settings.invertLook,v=>settings.invertLook=v);
            FlowText("WASD move • Mouse look • E use • F carry • P pack • I bag • J checklist • N phone • Esc pause/back",17,false,StormglassTheme.Muted,72);ActionButton("Back to settings",()=>Show(Page.Settings),true,true,62);
        }
        void BuildVR()
        {
            SettingsHeading("VR COMFORT & CONTROLS","Stable menus and deliberate interactions");
            ActionButton("Movement & comfort\nTurning, direction, position, and vignette",()=>Show(Page.VRComfort),true,false,86);
            ActionButton("Hands & interactions\nDominant hand, grip behavior, and controls",()=>Show(Page.VRControls),true,false,86);
            ActionButton("Run VR setup again",()=>{onboardingStep=0;onboardingLaunchAfter=false;Show(Page.Onboarding);},true,false,62);
            ActionButton("Recenter menu now",Recenter,true,false,58);
            ActionButton("Back to settings",()=>Show(Page.Settings),true,true,62);
        }
        void BuildVRComfort()
        {
            SettingsHeading("VR MOVEMENT & COMFORT","Choose the least tiring movement style");
            ToggleButton("Smooth turning",()=>settings.smoothTurn,v=>settings.smoothTurn=v);
            if(settings.smoothTurn)ActionButton("Turn speed: "+Mathf.RoundToInt(settings.smoothTurnSpeed)+"°/s",()=>{settings.smoothTurnSpeed=settings.smoothTurnSpeed>=119?30:settings.smoothTurnSpeed+15;SaveSettings();Build();},true,false,58);
            else ActionButton("Snap turn: "+settings.snapTurnAngle+"°",()=>{settings.snapTurnAngle=settings.snapTurnAngle==15?30:settings.snapTurnAngle==30?45:15;SaveSettings();Build();},true,false,58);
            ActionButton("Movement direction: "+(settings.handRelativeMovement?"Controller":"Head"),()=>{settings.handRelativeMovement=!settings.handRelativeMovement;SaveSettings();Build();},true,false,58);
            ActionButton("Play position: "+(settings.seatedMode?"Seated":"Standing"),()=>{settings.seatedMode=!settings.seatedMode;SaveSettings();Build();},true,false,58);
            ActionButton("Comfort vignette: "+Mathf.RoundToInt(settings.vignetteStrength*100)+"%",()=>{settings.vignetteStrength=settings.vignetteStrength>=.99f?0:Mathf.Clamp01(settings.vignetteStrength+.25f);SaveSettings();Build();},true,false,58);
            ToggleButton("Reduced camera motion",()=>settings.reducedMotion,v=>settings.reducedMotion=v);ToggleButton("Reduced flashes",()=>settings.reducedFlashes,v=>settings.reducedFlashes=v);
            ActionButton("Back to VR settings",()=>Show(Page.VR),true,true,62);
        }
        void BuildVRControls()
        {
            SettingsHeading("VR HANDS & INTERACTIONS","The highlighted target is operated by the active hand");
            ActionButton("Dominant hand: "+(settings.leftHanded?"Left":"Right"),()=>{settings.leftHanded=!settings.leftHanded;activePointer=-1;SaveSettings();Build();},true,false,58);
            ToggleButton("Toggle grip",()=>settings.toggleGrip,v=>settings.toggleGrip=v);
            FlowText("TRIGGER  Use or select\nGRIP  Carry or release\nA  Rise while swimming\nX  Guide\nY  Objective\nB  Bag\nMENU  Pause or go back",19,false,StormglassTheme.Ink,230);
            ActionButton("Back to VR settings",()=>Show(Page.VR),true,true,62);
        }
        void BuildOnboarding()
        {
            if(onboardingStep==0)
            {
                FlowText("VR SETUP  •  1 / 3",17,false,StormglassTheme.Accent,30);FlowText("Choose your play position",34,true,height:52);
                FlowText("Set the option that matches how you will complete the simulation. You can change it later while paused.",20,false,StormglassTheme.Ink,90);
                ActionButton("Play position: "+(settings.seatedMode?"Seated":"Standing"),()=>{settings.seatedMode=!settings.seatedMode;SaveSettings();Build();},true,false,64);
                ActionButton("Dominant hand: "+(settings.leftHanded?"Left":"Right"),()=>{settings.leftHanded=!settings.leftHanded;activePointer=-1;SaveSettings();Build();},true,false,64);
                ActionButton("Recenter menu at my current position",Recenter,true,false,64);ActionButton("Next: movement comfort",()=>{onboardingStep=1;Build();},true,true,68);
            }
            else if(onboardingStep==1)
            {
                FlowText("VR SETUP  •  2 / 3",17,false,StormglassTheme.Accent,30);FlowText("Choose comfortable movement",34,true,height:52);
                ToggleButton("Smooth turning",()=>settings.smoothTurn,v=>settings.smoothTurn=v);
                if(!settings.smoothTurn)ActionButton("Snap angle: "+settings.snapTurnAngle+"°",()=>{settings.snapTurnAngle=settings.snapTurnAngle==15?30:settings.snapTurnAngle==30?45:15;SaveSettings();Build();},true,false,60);
                ActionButton("Movement direction: "+(settings.handRelativeMovement?"Controller":"Head"),()=>{settings.handRelativeMovement=!settings.handRelativeMovement;SaveSettings();Build();},true,false,60);
                ActionButton("Comfort vignette: "+Mathf.RoundToInt(settings.vignetteStrength*100)+"%",()=>{settings.vignetteStrength=settings.vignetteStrength>=.99f?0:Mathf.Clamp01(settings.vignetteStrength+.25f);SaveSettings();Build();},true,false,60);
                ActionButton("Next: interaction check",()=>{onboardingStep=2;Build();},true,true,68);
            }
            else
            {
                FlowText("VR SETUP  •  3 / 3",17,false,StormglassTheme.Accent,30);FlowText("Point and confirm",34,true,height:52);
                FlowText("Point with your active controller. The focused button becomes brighter and slightly larger. Release Trigger to select it. A short pulse confirms selection.",20,false,StormglassTheme.Ink,125);
                FlowText("Left stick move  •  Right stick turn\nTrigger use  •  Grip carry  •  Menu pause/back",18,false,StormglassTheme.Muted,76);
                ActionButton(onboardingLaunchAfter?"Start simulation":"Finish VR setup",()=>{PlayerPrefs.SetInt("StormSimVROnboarded",1);PlayerPrefs.Save();SaveSettings();if(onboardingLaunchAfter)Launch(StormSimLaunchMode.NewSimulation);else Show(Page.VR);},true,true,78);
                ActionButton("Back to movement settings",()=>{onboardingStep=1;Build();},true,false,60);
            }
        }
        void SettingsHeading(string eyebrow,string title){FlowText(eyebrow,17,false,StormglassTheme.Accent,30);FlowText(title,32,true,height:52);}
        void ToggleButton(string label,Func<bool> read,Action<bool> write){ActionButton(label+": "+OnOff(read()),()=>{write(!read());SaveSettings();Build();},true,false,58);}
        void VolumeButton(string label,Func<float> read,Action<float> write){ActionButton(label+": "+Mathf.RoundToInt(read()*100)+"%",()=>{write(Step(read()));SaveSettings();Build();},true,false,62);}
        static float Step(float value)=>value>=.99f?0:Mathf.Round((value+.1f)*10)/10;
        static string OnOff(bool value)=>value?"On":"Off";
        void SaveSettings(){settings.textScale=Mathf.Clamp(settings.textScale,1,1.3f);settings.volume=Mathf.Clamp01(settings.volume);settings.speechVolume=Mathf.Clamp01(settings.speechVolume);settings.environmentVolume=Mathf.Clamp01(settings.environmentVolume);settings.effectsVolume=Mathf.Clamp01(settings.effectsVolume);settings.smoothTurnSpeed=Mathf.Clamp(settings.smoothTurnSpeed,30,120);settings.vignetteStrength=Mathf.Clamp01(settings.vignetteStrength);settings.subtitleBackground=Mathf.Clamp(settings.subtitleBackground,.35f,1);PlayerPrefs.SetString("SibucaoSettings",JsonUtility.ToJson(settings));PlayerPrefs.Save();AudioListener.volume=settings.volume;if(eye)eye.fieldOfView=settings.fieldOfView;if(!XRSettings.isDeviceActive){if(QualitySettings.names.Length>0)QualitySettings.SetQualityLevel(Mathf.Clamp(settings.graphics,0,QualitySettings.names.Length-1),true);if(!Application.isEditor)Screen.fullScreen=settings.fullscreen;}}
        void Show(Page target){page=target;Build();}
        void Back(){if(page==Page.VRComfort||page==Page.VRControls)Show(Page.VR);else if(page==Page.Accessibility||page==Page.Audio||page==Page.Desktop||page==Page.VR)Show(Page.Settings);else if(page==Page.Onboarding){if(onboardingLaunchAfter){startAfterLocationChoice=true;Show(Page.Locations);}else Show(Page.VR);}else{startAfterLocationChoice=false;Show(Page.Home);}}
        void ChooseArea(int index)
        {
            selectedArea=index;PlayerPrefs.SetInt("StormSimMenuArea",index);PlayerPrefs.Save();if(startAfterLocationChoice){startAfterLocationChoice=false;if(XRSettings.isDeviceActive&&PlayerPrefs.GetInt("StormSimVROnboarded",0)==0){onboardingStep=0;onboardingLaunchAfter=true;Show(Page.Onboarding);}else Launch(StormSimLaunchMode.NewSimulation);}else Show(Page.Home);
        }
        bool HasSave(Area area)
        {
            string root=Application.persistentDataPath;foreach(var suffix in new[]{"-save.json","-checkpoint.json","-save.json.bak","-checkpoint.json.bak"})if(File.Exists(Path.Combine(root,area.savePrefix+suffix)))return true;return false;
        }
        SaveSummary LatestSave()
        {
            SaveSummary latest=null;string root=Application.persistentDataPath;for(int areaIndex=0;areaIndex<Areas.Length;areaIndex++)foreach(var slot in new[]{new[]{"-checkpoint.json","Checkpoint"},new[]{"-save.json","Manual save"},new[]{"-checkpoint.json.bak","Checkpoint backup"},new[]{"-save.json.bak","Manual backup"}})
            {
                string path=Path.Combine(root,Areas[areaIndex].savePrefix+slot[0]);if(!File.Exists(path))continue;try{var candidate=new SaveSummary{areaIndex=areaIndex,path=path,slot=slot[1],changed=File.GetLastWriteTime(path),state=SibucaoSaveStore.Read(path,null)};if(latest==null||candidate.changed>latest.changed)latest=candidate;}catch(Exception){ }
            }
            return latest;
        }
        string SaveLabel(SaveSummary save)
        {
            int seconds=Mathf.Max(0,Mathf.FloorToInt(save.state.homeSeconds));int stage=SibucaoCausalModel.Next(save.state.physical);string progress=stage<9?"Junction "+(stage+1)+"/9":"School arrival";return Areas[save.areaIndex].name+" • "+save.slot+" • "+save.changed.ToString("dd MMM yyyy HH:mm")+" • "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00")+" • "+progress;
        }
        void Launch(StormSimLaunchMode mode){var area=Areas[selectedArea];StormSimLaunchRequest.Set(area.scene,mode);StormSim.UI.StormglassSceneTransition.Load(area.scene);}
        void Recenter(){anchored=false;Anchor();}
        void ConfigureCanvas(bool xr)
        {
            if(authoredMenu)authoredMenu.ConfigureStandaloneDisplay(eye,xr);
            canvas.renderMode=xr?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;canvas.worldCamera=xr?eye:null;canvas.GetComponent<CanvasScaler>().enabled=!xr;
            if(xr){((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one*.0011f;}
            else canvas.transform.localScale=Vector3.one;anchored=false;
        }
        void Anchor()
        {
            if(!XRSettings.isDeviceActive||anchored)return;var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;canvas.transform.SetPositionAndRotation(eye.transform.position+forward*1.8f,Quaternion.LookRotation(forward,Vector3.up));
            if(authoredMenu)authoredMenu.transform.SetPositionAndRotation(eye.transform.position+forward*authoredMenu.viewingDistance,Quaternion.LookRotation(forward,Vector3.up));
            anchored=true;
        }
        void Update()
        {
            if (StormSim.UI.StormglassSceneTransition.IsLoading) return;
            bool xr=XRSettings.isDeviceActive;if(xr!=xrWasActive){xrWasActive=xr;ConfigureCanvas(xr);Build();}Anchor();
            AnimateStorm();bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame||Gamepad.current!=null&&Gamepad.current.buttonEast.wasPressedThisFrame;if(back&&page!=Page.Home)Back();
            UpdatePointers(xr);
            if(!AuthoredHome)
            {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                if(selected!=lastReadingSelection)
                {
                    lastReadingSelection=selected;
                    if(selected&&selected.transform.IsChildOf(content))
                    {
                        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(readingViewport,selected.transform);
                        float shift=bounds.max.y>readingViewport.rect.yMax?readingViewport.rect.yMax-bounds.max.y:bounds.min.y<readingViewport.rect.yMin?readingViewport.rect.yMin-bounds.min.y:0;
                        var pos=content.anchoredPosition;pos.y=Mathf.Clamp(pos.y+shift,0,Mathf.Max(0,content.rect.height-readingViewport.rect.height));content.anchoredPosition=pos;
                    }
                }
                float axis=Gamepad.current!=null?Gamepad.current.rightStick.ReadValue().y:0;
                if(xr&&InputDevices.GetDeviceAtXRNode(activePointer==1?XRNode.LeftHand:XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis,out Vector2 scrollStick))axis=scrollStick.y;
                if(Mathf.Abs(axis)>.2f)readingScroll.verticalNormalizedPosition=Mathf.Clamp01(readingScroll.verticalNormalizedPosition+axis*Time.unscaledDeltaTime);
            }
        }
        void AnimateStorm()
        {
            if(!animatedBackdrop)return;float t=settings.reducedMotion?0:Time.unscaledTime;animatedBackdrop.anchoredPosition=new Vector2(Mathf.Sin(t*.08f)*34,Mathf.Sin(t*.11f)*8);animatedBackdrop.localScale=Vector3.one*(1.035f+(settings.reducedMotion?0:Mathf.Sin(t*.13f)*.008f));for(int i=0;i<rainBands.Count;i++){var band=rainBands[i];float x=170+i*270+(settings.reducedMotion?0:Mathf.Repeat(t*(62+i*4)+i*113,2050)-1025);band.anchoredPosition=new Vector2(x,settings.reducedMotion?0:Mathf.Repeat(t*(210+i*7)+i*97,1240)-620);}
        }
        void CreatePointers()
        {
            pointers=new[]{new StormglassPointer(EventSystem.current,-201),new StormglassPointer(EventSystem.current,-202)};rays=new LineRenderer[2];cursors=new GameObject[2];rayMaterial=new Material(Shader.Find("Sprites/Default"));
            for(int i=0;i<2;i++){var go=new GameObject("Main menu controller ray "+i);rays[i]=go.AddComponent<LineRenderer>();rays[i].sharedMaterial=rayMaterial;rays[i].positionCount=2;rays[i].startWidth=.002f;rays[i].endWidth=.001f;rays[i].startColor=rays[i].endColor=StormglassTheme.Accent;rays[i].enabled=false;cursors[i]=GameObject.CreatePrimitive(PrimitiveType.Sphere);cursors[i].name="Main menu cursor";Destroy(cursors[i].GetComponent<Collider>());cursors[i].GetComponent<Renderer>().sharedMaterial=rayMaterial;cursors[i].transform.localScale=Vector3.one*.012f;cursors[i].SetActive(false);}ConfigureCanvas(XRSettings.isDeviceActive);xrWasActive=XRSettings.isDeviceActive;
        }
        void UpdatePointers(bool xr)
        {
            for(int i=0;i<2;i++)
            {
                bool valid=xr;Ray ray=default;bool trigger=false;valid&=StormglassPointer.TrackedRay(i==0?XRNode.RightHand:XRNode.LeftHand,trackingOrigin,out ray,out trigger);bool down=trigger&&!triggerHeld[i];triggerHeld[i]=trigger;if(down&&activePointer!=i){if(activePointer>=0)pointers[activePointer].Reset();activePointer=i;}if(activePointer<0&&valid)activePointer=settings.leftHanded?1:0;valid&=activePointer==i;
                if(!valid){pointers[i].Reset();rays[i].enabled=false;cursors[i].SetActive(false);continue;}var previous=pointers[i].Hovered;bool hit=pointers[i].Process(ray,trigger,buttons,AuthoredHome?(RectTransform)authoredMenu.canvas.transform:readingViewport,out Vector3 point);rays[i].enabled=true;rays[i].SetPosition(0,ray.origin);rays[i].SetPosition(1,point);cursors[i].SetActive(hit);if(hit)cursors[i].transform.position=point;var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.RightHand:XRNode.LeftHand);if(previous!=pointers[i].Hovered&&pointers[i].Hovered)device.SendHapticImpulse(0,.08f,.025f);if(pointers[i].ClickedThisFrame)device.SendHapticImpulse(0,.22f,.045f);
            }
        }
        static AudioClip MakeUITone(string name,float startHz,float endHz,float duration,float gain)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(rate*duration)];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate,u=i/(float)Mathf.Max(1,samples.Length-1);float frequency=Mathf.Lerp(startHz,endHz,u);samples[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*Mathf.Sin(Mathf.PI*u)*gain;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void PlayMenuHover(){if(uiSound&&uiHover&&!uiSound.isPlaying){uiSound.volume=settings.effectsVolume;uiSound.PlayOneShot(uiHover);}}
        void PlayMenuActivate(){if(uiSound&&uiActivate){uiSound.volume=settings.effectsVolume;uiSound.PlayOneShot(uiActivate);}}
        void OnDestroy(){if(rayMaterial)Destroy(rayMaterial);if(uiHover)Destroy(uiHover);if(uiActivate)Destroy(uiActivate);foreach(var texture in generatedTextures)if(texture)Destroy(texture);}
    }

    public sealed class StormSimMenuAudioFocus : MonoBehaviour,IPointerEnterHandler,ISelectHandler
    {
        public StormSimMainMenuScene owner;
        public void OnPointerEnter(PointerEventData eventData){if(owner)owner.PlayMenuHover();}
        public void OnSelect(BaseEventData eventData){if(owner)owner.PlayMenuHover();}
    }
}
