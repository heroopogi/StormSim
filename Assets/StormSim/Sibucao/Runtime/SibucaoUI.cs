using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Desktop reference UI, scaled from a 1440 x 900 design canvas.</summary>
    public sealed partial class SibucaoUI : MonoBehaviour
    {
        public Font roboto,robotoBold;
        public StormSimMainMenu worldMenu;
        [Tooltip("Temporary testing clock. Disable for participant builds.")]
        public bool showTemporaryClock=true;
        static readonly Color Primary=Hex("E6784A"),Dark=Hex("12181A"),PanelColor=Hex("2F3A3A"),Blue=Hex("5F9BBF"),Success=Hex("5FBF9F"),Warning=Hex("C9A13B"),Danger=Hex("D9534F");
        GUIStyle body,small,h1,h2,h3,button;
        Texture2D normalTexture,hoverTexture,pressedTexture,disabledTexture;
        Vector2 scroll;
        int tab;
        bool initialized;
        SibucaoSession S=>SibucaoSession.Instance;
        bool CanAdvanceClock=>SibucaoDevelopmentControls.Enabled&&showTemporaryClock&&S&&(S.Mode==SibucaoMode.Playing||S.Mode==SibucaoMode.Paused)&&(!S.home||!S.home.MovementLocked);
        void Update()
        {
            if(CanAdvanceClock&&UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame)AdvanceTemporaryClock();
        }
        public bool AdvanceTemporaryClock()
        {
            if(!CanAdvanceClock)return false;
            S.state.homeSeconds+=60;S.state.elapsed=S.state.homeSeconds;
            if(S.physicalDecisions)S.physicalDecisions.SetFlag("temporary_clock_advanced");
            return true;
        }
        void TemporaryClock()
        {
            if(!SibucaoDevelopmentControls.Enabled||!showTemporaryClock||(S.Mode!=SibucaoMode.Playing&&S.Mode!=SibucaoMode.Paused))return;
            if((S.inventory&&S.inventory.IsOpen)||(S.missionGuide&&S.missionGuide.IsOpen))return;
            int seconds=Mathf.FloorToInt(S.state.homeSeconds);
            Panel(new Rect(24,180,300,145));
            Label(new Rect(40,192,268,22),"TEMPORARY TEST CLOCK",small);
            Label(new Rect(40,217,268,32),$"{seconds/60:00}:{seconds%60:00}",h3);
            if(Button(new Rect(40,255,268,36),"+1 minute  /  F8",false,null,CanAdvanceClock))AdvanceTemporaryClock();
            var flood=S.GetComponent<FloodSimulationManager>();
            string hint=S.home&&S.home.MovementLocked?"Available after standing up":flood&&flood.IsCatchingUp?(S.Mode==SibucaoMode.Paused?"Resume to update floodwater":"Floodwater catching up..."):"F8 in game / Esc to click";
            Label(new Rect(40,297,268,22),hint,small);
        }
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
        GUIStyle TextStyle(int size,bool bold=false)
        {
            return new GUIStyle{font=SignalStationUI.Font(bold),fontSize=size,fontStyle=FontStyle.Normal,wordWrap=true,normal={textColor=Color.white},alignment=TextAnchor.UpperLeft};
        }
        void Initialize()
        {
            if(initialized)return;initialized=true;
            body=TextStyle(16);small=TextStyle(13);h1=TextStyle(44,true);h2=TextStyle(30,true);h3=TextStyle(22,true);
            normalTexture=SignalStationUI.Texture("btn_normal");hoverTexture=SignalStationUI.Texture("btn_hover");pressedTexture=SignalStationUI.Texture("btn_pressed");disabledTexture=SignalStationUI.Texture("btn_disabled");
            button=new GUIStyle(body){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(20,18,5,5),border=new RectOffset(8,8,8,8),normal={background=normalTexture,textColor=Hex("EEF4F2")},hover={background=hoverTexture,textColor=Hex("EEF4F2")},active={background=pressedTexture,textColor=Hex("EEF4F2")},focused={background=SignalStationUI.Texture("btn_selected"),textColor=Hex("EEF4F2")},onNormal={background=disabledTexture,textColor=Hex("7F8F8B")}};
        }
        void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Line(Vector2 a,Vector2 b,Color color,float width=2)
        {
            Matrix4x4 old=GUI.matrix;Vector2 d=b-a;GUI.matrix=old*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);Fill(new Rect(0,0,width+d.magnitude,width),color);GUI.matrix=old;
        }
        void Border(Rect r,Color color){Fill(new Rect(r.x,r.y,r.width,1),color);Fill(new Rect(r.x,r.yMax-1,r.width,1),color);Fill(new Rect(r.x,r.y,1,r.height),color);Fill(new Rect(r.xMax-1,r.y,1,r.height),color);}
        void Label(Rect r,string text,GUIStyle style,Color? color=null){var old=style.normal.textColor;style.normal.textColor=color??Color.white;GUI.Label(r,SibucaoControls.Prompt(text),style);style.normal.textColor=old;}
        void Panel(Rect r,string title=null)
        {
            SignalStationUI.Draw(r,title=="Confirm"?"panel_bg_modal":"panel_bg",16);
            if(title!=null){Label(new Rect(r.x+24,r.y+20,r.width-48,46),title,h2);Fill(new Rect(r.x+24,r.y+73,r.width-48,2),new Color(.4f,.4f,.4f,.3f));}
        }
        bool Button(Rect r,string label,bool primary=false,string icon=null,bool enabled=true,bool mapControls=true)
        {
            bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&enabled;
            var style=enabled?button:DisabledStyle();var normal=style.normal.background;
            if(primary&&enabled)style.normal.background=SignalStationUI.Texture("btn_selected");
            bool clicked=GUI.Button(r,(icon!=null?"      ":"")+(mapControls?SibucaoControls.Prompt(label):label),style);
            style.normal.background=normal;GUI.enabled=previousEnabled;
            if(icon!=null)Icon(icon,new Rect(r.x+16,r.y+13,23,23),enabled?Hex("EEF4F2"):Hex("7F8F8B"));
            return clicked;
        }
        GUIStyle disabledButton;
        GUIStyle DisabledStyle(){if(disabledButton==null){disabledButton=new GUIStyle(button);disabledButton.normal=button.onNormal;}return disabledButton;}
        void Icon(string kind,Rect r,Color c)
        {
            float x=r.x,y=r.y,w=r.width,h=r.height;
            if(kind=="play"){Line(new Vector2(x+3,y),new Vector2(x+w,y+h/2),c);Line(new Vector2(x+w,y+h/2),new Vector2(x+3,y+h),c);Line(new Vector2(x+3,y+h),new Vector2(x+3,y),c);}
            else if(kind=="home"){Line(new Vector2(x,y+h*.45f),new Vector2(x+w*.5f,y),c);Line(new Vector2(x+w*.5f,y),new Vector2(x+w,y+h*.45f),c);Border(new Rect(x+4,y+h*.45f,w-8,h*.55f),c);}
            else if(kind=="close"){Line(new Vector2(x,y),new Vector2(x+w,y+h),c,3);Line(new Vector2(x+w,y),new Vector2(x,y+h),c,3);}
            else if(kind=="alert"){Line(new Vector2(x+w*.5f,y),new Vector2(x,y+h),c,3);Line(new Vector2(x,y+h),new Vector2(x+w,y+h),c,3);Line(new Vector2(x+w,y+h),new Vector2(x+w*.5f,y),c,3);Label(new Rect(x+w*.4f,y+4,w,h),"!",small,c);}
            else if(kind=="save"){Border(r,c);Border(new Rect(x+5,y+1,w-10,h*.35f),c);Border(new Rect(x+5,y+h*.58f,w-10,h*.42f),c);}
            else if(kind=="settings"){for(int i=0;i<3;i++){float yy=y+4+i*8;Line(new Vector2(x,yy),new Vector2(x+w,yy),c);Fill(new Rect(x+4+(i%2)*10,yy-3,5,7),c);}}
            else if(kind=="people"){Fill(new Rect(x+8,y,8,8),c);Border(new Rect(x+3,y+11,w-6,h-11),c);}
            else if(kind=="objective"){Border(r,c);for(int i=0;i<3;i++)Line(new Vector2(x+5,y+5+i*6),new Vector2(x+w-4,y+5+i*6),c);}
            else if(kind=="tools"){Line(new Vector2(x+2,y+h-2),new Vector2(x+w-3,y+3),c,4);Line(new Vector2(x+w-9,y+1),new Vector2(x+w,y+10),c,3);}
            else if(kind=="map"){Border(r,c);Line(new Vector2(x+w*.33f,y),new Vector2(x+w*.33f,y+h),c);Line(new Vector2(x+w*.66f,y),new Vector2(x+w*.66f,y+h),c);}
            else {Border(r,c);Label(new Rect(x+8,y-2,w,h+3),kind=="info"?"i":"?",body,c);}
        }
        void Key(Rect r,string key){SignalStationUI.Draw(r,"btn_selected",8);Label(new Rect(r.x+9,r.y+4,r.width,r.height),key,h3,Hex("EEF4F2"));}
        void Check(float x,float y,string text,bool done,float width=310)
        {
            SignalStationUI.Draw(new Rect(x,y,24,24),done?"checkbox_on":"checkbox_off");
            Label(new Rect(x+29,y,width-29,42),text,small);
        }
        void Portrait(Rect r)
        {
            Fill(r,Hex("30373B"));Fill(new Rect(r.x+12,r.y+92,r.width-24,r.height-92),Hex("697378"));
            Fill(new Rect(r.x+27,r.y+88,13,r.height-88),Primary);Fill(new Rect(r.xMax-40,r.y+88,13,r.height-88),Primary);
            Fill(new Rect(r.x+r.width*.4f,r.y+67,r.width*.2f,32),Hex("AB896A"));
            Fill(new Rect(r.x+29,r.y+23,r.width-58,60),Hex("BD9B77"));
            Fill(new Rect(r.x+23,r.y+14,r.width-40,20),Hex("465563"));Fill(new Rect(r.x+23,r.y+31,r.width-28,7),Hex("33414B"));
            Fill(new Rect(r.x+39,r.y+47,5,6),Dark);Fill(new Rect(r.xMax-44,r.y+47,5,6),Dark);Line(new Vector2(r.x+42,r.y+71),new Vector2(r.xMax-40,r.y+71),Hex("6A5141"));SignalStationUI.Draw(r,"portrait_frame",2);
        }
        void Logo(float x,float y)
        {
            Line(new Vector2(x+90,y+50),new Vector2(x+140,y),Color.white,5);Line(new Vector2(x+140,y),new Vector2(x+190,y+50),Color.white,5);
            Line(new Vector2(x+175,y+10),new Vector2(x+215,y+50),Color.white,4);
            Label(new Rect(x,y+64,360,64),"TYPHOON",h1);Label(new Rect(x,y+121,380,46),"PREPAREDNESS",h2,Primary);
            Label(new Rect(x+4,y+169,340,30),"S I M U L A T I O N   G A M E",small);Label(new Rect(x+4,y+205,340,32),"STORMSIM  /  PUROK "+(S.GetComponent<CoastalAreaProfile>()?S.GetComponent<CoastalAreaProfile>().areaName.ToUpperInvariant():"SIBUCAO"),small,Primary);
        }
        void OnGUI()
        {
            if (StormSim.UI.StormglassUI.InUse) return;
            if(!S||S.Mode==SibucaoMode.Cinematic||S.Mode==SibucaoMode.Preparation)return;Initialize();
            body.fontSize=Mathf.RoundToInt(16*S.settings.textScale);small.fontSize=Mathf.RoundToInt(13*S.settings.textScale);
            button.fontSize=body.fontSize;if(disabledButton!=null)disabledButton.fontSize=body.fontSize;
            bool physical=S.physicalDecisions;
            bool guideOpen=S.missionGuide&&S.missionGuide.IsOpen;
            bool bagOpen=S.inventory&&S.inventory.IsOpen;
            if(physical&&S.Mode==SibucaoMode.Playing&&!guideOpen&&!bagOpen){PhysicalHud();DrawPlacementTarget();}
            float safeHeight=Mathf.Max(1,Screen.height-48);
            float scale=Mathf.Min(Screen.width/1440f,safeHeight/900f);var previous=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(safeHeight-900*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            switch(S.Mode)
            {
                case SibucaoMode.Menu:if(!worldMenu)MainMenu();break;
                case SibucaoMode.Playing:if(!physical)Hud();else if(guideOpen)DrawScenarioGuide();break;
                case SibucaoMode.Decision:Hud();Dialogue();break;
                case SibucaoMode.Paused:Pause();break;
                case SibucaoMode.Settings:Settings();break;
                case SibucaoMode.Complete:Complete();break;
                case SibucaoMode.Confirm:Confirmation();break;
                case SibucaoMode.About:About();break;
                case SibucaoMode.RoleSelection:S.SelectRole();break;
                case SibucaoMode.DifficultySelection:DifficultySelection();break;
                case SibucaoMode.ArrivalReview:ArrivalReview();break;
                case SibucaoMode.Recovery:RecoveryPanel();break;
                case SibucaoMode.ResumeSelection:ResumePanel();break;
            }
            if(S.Mode==SibucaoMode.Playing&&bagOpen&&!guideOpen)DrawInventory();
            if(S.home&&S.home.CanSkipTV)
                if(Button(new Rect(1090,720,320,48),"Development: skip TV / Space"))S.home.SkipTV();
            if(Time.unscaledTime<S.toastUntil&&S.Mode!=SibucaoMode.Playing&&S.Mode!=SibucaoMode.Decision&&S.Mode!=SibucaoMode.Complete&&!(worldMenu&&S.Mode==SibucaoMode.Menu))
            {
                Panel(new Rect(994,30,414,105));
                Label(new Rect(1010,46,380,30),S.toastTitle,body);
                Label(new Rect(1010,77,380,48),S.toastText,small);
            }
            TemporaryClock();GUI.matrix=previous;
        }

        void MainMenu()
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.24f));Panel(new Rect(56,52,444,794));Logo(101,98);
            Label(new Rect(97,371,350,38),"Find your way to shelter.",h3);Label(new Rect(97,415,345,60),"Rain lashes the coastal streets. Reach the school and respond to the people you meet.",body);
            if(Button(new Rect(98,516,356,51),"Play",true,"play"))S.SelectRole();
            if(Button(new Rect(98,578,356,49),"Continue",false,"save",S.HasSave))S.LoadRun();
            if(Button(new Rect(98,638,356,49),"Settings",false,"settings"))S.OpenSettings();
            if(Button(new Rect(98,698,171,49),"About",false,"info"))S.SetMode(SibucaoMode.About);
            if(Button(new Rect(283,698,171,49),"Exit",false,"close"))S.Confirm("exit");
            Label(new Rect(98,783,360,22),"WASD move   •   Mouse look   •   E interact",small);
            Label(new Rect(870,733,500,60),"A safer community starts with you.",h2);
        }
        void Hud()
        {
            if(S.physicalDecisions){PhysicalHud();return;}
            if(S.home && S.home.Active && (S.home.MovementLocked || S.home.Preparing)){HomeHud();return;}
            if(S.home && !string.IsNullOrEmpty(S.home.Prompt)) {Panel(new Rect(500,607,460,63));Label(new Rect(525,622,420,40),S.home.Prompt,body);}
            Panel(new Rect(28,28,370,260));Icon("objective",new Rect(50,50,24,24),Primary);Label(new Rect(87,46,288,35),"Main Objective",h3,Primary);
            Label(new Rect(50,91,320,53),"Reach Banago Elementary School I",body);
            Check(50,151,"Leave the northern neighborhood",S.route.Progress(S.player.transform.position)>200);
            Check(50,187,"Respond to encounters along the route",CountResolved()==7);
            Check(50,223,"Enter the school campus",S.state.resolved[8]);
            Panel(new Rect(510,28,420,76));Icon("map",new Rect(532,49,25,25),Color.white);
            Vector3 delta=S.NavigationTarget()-S.player.transform.position;
            float angle=Vector3.SignedAngle(S.player.transform.forward,new Vector3(delta.x,0,delta.z),Vector3.up);
            string heading=Mathf.Abs(angle)<25?"Continue ahead":angle>0?"Bear right":"Bear left";
            Label(new Rect(577,43,332,25),heading+"  •  School route",body);Label(new Rect(577,72,325,22),"Felix Amante Drive",small);
            SignalStationUI.Draw(new Rect(704,434,32,32),"crosshair");
            if(S.state.elapsed<12 && S.Mode==SibucaoMode.Playing)
            {
                Rect alert=new Rect(1032,173,376,130);Panel(alert);Border(alert,Danger);Icon("alert",new Rect(1050,195,28,28),Danger);
                Label(new Rect(1094,190,289,32),"Storm intensifying",body,Danger);Label(new Rect(1094,226,289,69),"Strong gusts and flying debris. Move toward shelter.",body);
            }
            if(S.settings.hints && S.state.elapsed<35)
            {
                Panel(new Rect(28,673,414,114));Border(new Rect(28,673,414,114),Blue);Icon("info",new Rect(47,699,27,27),Blue);
                Label(new Rect(91,690,330,82),"WASD to walk. Move the mouse to look. Face an encounter and press E to interact.",body);
            }
            if((S.player.vehicle || S.player.nearbyVehicle) && S.settings.interactPrompts && S.Mode==SibucaoMode.Playing)
            {
                float x=S.player.vehicle?1015:500;float y=S.player.vehicle?713:607;
                Rect r=new Rect(x,y,S.player.vehicle?395:470,110);Panel(r);Key(new Rect(x+13,y+11,40,40),"E");
                Label(new Rect(x+70,y+16,300,32),S.player.vehicle?"Exit "+S.player.vehicle.displayName:"Drive "+S.player.nearbyVehicle.displayName,h3);
                if(S.player.vehicle){Label(new Rect(x+15,y+58,375,24),"W/S Drive · A/D Steer · Space Brake",small);Label(new Rect(x+15,y+82,375,24),(S.player.vehicle.displayName=="Tricycle"?"C Center view   ":"C Center view · V Wipers   ")+Mathf.Abs(S.player.vehicle.Speed*3.6f).ToString("0")+" km/h",small);}
            }
            else if(S.nearby && S.settings.interactPrompts && S.Mode==SibucaoMode.Playing)
            {
                Rect r=new Rect(550,607,340,63);Panel(r);Key(new Rect(563,618,40,40),"E");Label(new Rect(620,625,250,32),S.nearby.index==3?"Pick Up":S.nearby.index<3?"Inspect":"Talk",h3);
            }
            if(S.settings.subtitles && Time.unscaledTime<S.subtitleUntil)
            {
                Panel(new Rect(452,752,720,114));Label(new Rect(475,766,650,28),"Resident",body,Primary);Label(new Rect(475,798,670,62),S.subtitle,body);
            }
            Label(new Rect(30,859,560,26),S.player.vehicle?"ESC Pause     Space Brake     E Exit":"ESC Pause     Shift Walk faster     E Enter vehicle",small);
        }
        void PhysicalHud()
        {
            var layout=SibucaoHudLayout.Current;var previous=SibucaoDesktopHud.Begin(layout);
            var guide=S.missionGuide;var manager=S.physicalDecisions;
            bool locked=S.home&&S.home.MovementLocked;
            var choices=guide?guide.RouteChoices:null;
            string title=choices&&choices.Working?"SIDE MISSION":choices&&choices.Previewing?"CHOOSE A PASSAGE":"CURRENT OBJECTIVE";
            SibucaoDesktopHud.Objective(layout,title,guide?guide.Objective:"Reach the school evacuation center.");
            if(!locked)
            {
                SignalStationUI.Draw(new Rect(layout.Width/2-10,layout.Height/2-10,20,20),"crosshair");
                var flood=S.GetComponent<FloodSimulationManager>();
                if(S.settings.interactPrompts||flood&&flood.IsCatchingUp||S.player.swimming&&S.player.swimming.IsUnderwater)SibucaoDesktopHud.Action(layout,ContextAction());
            }
            if(guide&&!guide.InsideHome&&!locked)DrawMinimap(layout);
            if(choices&&choices.Previewing&&!guide.InsideHome&&!locked)
            {
                var r=layout.Routes;var section=choices.Current;SibucaoDesktopHud.Panel(r);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+9,r.width-24,19),"Inspect a passage before starting work",SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+34,r.width-24,32),"LEFT   "+section.left.title,SibucaoDesktopHud.Small);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+68,r.width-24,32),"AHEAD  "+section.centerLine,SibucaoDesktopHud.Small);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+102,r.width-24,32),"RIGHT  "+section.right.title,SibucaoDesktopHud.Small);
            }
            string line=S.home&&S.home.neighborWarning?S.home.neighborWarning.Subtitle:null;
            var narration=S.GetComponent<SibucaoNarration>();if(narration&&narration.Speaking)line=narration.Caption;
            if(string.IsNullOrEmpty(line)&&Time.unscaledTime<S.subtitleUntil)line=S.subtitle;
            if(S.settings.subtitles)SibucaoDesktopHud.Subtitle(layout,line);
            if(Time.unscaledTime<S.toastUntil)
            {
                var r=layout.Toast;SibucaoDesktopHud.Panel(r);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+10,r.width-24,24),S.toastTitle,SibucaoDesktopHud.Text,SibucaoDesktopHud.Accent);
                SibucaoDesktopHud.Label(new Rect(r.x+12,r.y+39,r.width-24,60),S.toastText,SibucaoDesktopHud.Small);
            }
            SibucaoDesktopHud.Label(layout.Help,(S.inventory&&S.inventory.HasBag?"I / Tab  Bag ("+S.inventory.Count+"/27)":"I / Tab  Bag")+"\nJ  Guide  ·  Esc  Pause",SibucaoDesktopHud.Small,SibucaoDesktopHud.Muted);
            if(manager.ShockFlash>0&&!S.settings.reducedFlashes)Fill(new Rect(0,0,layout.Width,layout.Height),new Color(.9f,.95f,1,manager.ShockFlash*.3f));
            GUI.matrix=previous;
        }
        public string ContextAction()
        {
            var guide=S.missionGuide;var grab=S.player.grabber;
            var flood=S.GetComponent<FloodSimulationManager>();
            if(flood&&flood.IsCatchingUp)return "Restoring floodwater · "+Mathf.RoundToInt(flood.CatchUpProgress*100)+"%\nMovement resumes when ready. Esc pauses.";
            if(S.player.swimming&&S.player.swimming.IsUnderwater)return "Breath "+Mathf.CeilToInt(Mathf.Max(0,StormSim.Preparedness.StormSurvivalVitals.BreathSeconds-S.state.physical.submergedSeconds))+" s · Space to surface\n"+(S.state.physical.submergedSeconds>30?"Low breath · rising toward the surface":"Ctrl / C dives · release to float");
            if(S.player.vehicle)return S.player.vehicle.TravelRestriction(S.player.vehicle.transform.position,null)??"W/S drive · A/D steer · Space brake\nE exit when stopped · C center view";
            if(guide&&guide.FocusedPaper)return "E  Read the household guide";
            if(S.home&&!string.IsNullOrEmpty(S.home.Prompt))return S.home.Prompt;
            var routes=guide?guide.RouteChoices:null;
            if(routes&&!string.IsNullOrEmpty(routes.Prompt))return routes.Prompt;
            if(S.passages&&!string.IsNullOrEmpty(S.passages.Prompt))return S.passages.Prompt+(S.passages.Held?" / Q: put down":"");
            if(grab&&grab.held)
            {
                if(S.inventory&&S.inventory.HasBag&&SibucaoInventory.Storable(grab.held))return "P / E  Pack "+SibucaoInventory.ItemName(grab.held)+"\nF / Q  Put down";
                return grab.held.kind==PhysicalItemKind.Assist?"Move with the resident · F / Q release":"R + mouse  Turn · Wheel  Reach\nF release / place · Q put down / cancel";
            }
            if(grab&&grab.CanReach&&grab.FocusedItem)
            {
                var prompt=S.inventory?S.inventory.PickupPrompt(grab.FocusedItem):null;
                return !string.IsNullOrEmpty(prompt)?prompt:"F  Carry / grip "+SibucaoInventory.ItemName(grab.FocusedItem);
            }
            if(guide&&guide.CurrentStation)
            {
                var station=guide.CurrentStation;
                if(station.EngagedBranch&&!string.IsNullOrEmpty(station.BranchPrompt))return station.BranchPrompt+" / Q: cancel";
                if(!string.IsNullOrEmpty(station.InteractionPrompt))return station.InteractionPrompt;
            }
            if(S.player.nearbyVehicle)return S.player.nearbyVehicle.TravelRestriction(S.player.nearbyVehicle.transform.position,S.player)??"E  Enter "+S.player.nearbyVehicle.displayName;
            if(S.player.swimming&&S.player.swimming.IsSwimming)return "Swimming · Space to rise · release to float\nUse a low, clear shore to leave the water.";
            var access=S.RouteChoices?S.RouteChoices.Current:null;
            if(access&&!S.RouteChoices.Working&&access.FloodLift>0&&S.player.transform.position.y<access.Approach.y-.45f&&SibucaoRouteChoices.Flat(S.player.transform.position,access.Approach)<30)
                return "Follow the map to the junction.\nInspect the three passages.";
            if(S.state.physical.hunger>=60)return "Hunger slows stamina recovery. Eat stored food from I / Tab.\nDry walking and resting restore stamina.";
            if(!S.settings.hints)return null;
            return guide?guide.ClearInstructions:"WASD move · E interact · J guide";
        }

        int guidePage;
        Vector2 guideScroll;
        void DrawScenarioGuide()
        {
            var layout=SibucaoHudLayout.Current;var previous=SibucaoDesktopHud.Begin(layout);var r=layout.Modal;
            Fill(new Rect(0,0,layout.Width,layout.Height),new Color(.02f,.05f,.06f,.92f));SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+18,r.width-44,40),"Your scenario guide",SibucaoDesktopHud.Heading);
            string[] tabs={"Your mission","Along the route","Controls"};float tabWidth=(r.width-56)/3;
            for(int i=0;i<3;i++)if(GUI.Button(new Rect(r.x+22+i*(tabWidth+6),r.y+60,tabWidth,36),(guidePage==i?"• ":"")+tabs[i],SibucaoDesktopHud.ButtonStyle)){guidePage=i;guideScroll=Vector2.zero;}
            string text;
            if(guidePage==0)text="1  Listen and prepare\nListen to the weather bulletin. Open storage with E, collect your bag with F, and choose your supplies. I / Tab opens the bag; F carries a supply and P packs it. Household warnings are optional.\n\n2  Leave home\nFace the door and press E to open it. Recheck the advisory, listen to the neighbor, and take your packed bag when leaving.\n\n3  Follow the route\nThe map points to your current situation. At a junction, look left, ahead and right. Inspect a passage before starting work; side streets have their own tasks.\n\n4  Reach the school\nAfter the creek encounter, follow the route to the school entrance and report to the arrival desk.";
            else if(guidePage==1)text="1  Wind-damaged street — inspect the obstruction and passages. Stick readings need actual floodwater.\n\n2  Stalled cars — find a way through the blocked street.\n\n3  Flooded junction — inspect the passages and the resident's situation.\n\n4  Fallen cable — notice the cable and choose a way past.\n\n5  Damaged bag — recover belongings if they spill.\n\n6  Wheelchair at the curb — decide how to respond to the resident.\n\n7  Rescue checkpoint — approach the responder and boat.\n\n8  Collapsed evacuee — assess the person on the road.\n\n9  Creek checkpoint — observe the water and follow your chosen route.";
            else text=SibucaoDesktopHud.Controls+"\n\n1–9 takes out a quick-slot item. Food is eaten when selected. Decision tools remain in the world. Use the current action prompt for gates, tools and assistance.";
            if(guidePage==0)text+="\n\nTasks use the existing streets and terrain at every difficulty. Floodwater can submerge low routes.\n\nFlood depths follow the chosen storm scenario and local terrain. This is a training scenario, not a flood forecast.";
            if(guidePage==2)text+="\n\nDry walking restores stamina; running, heavy carrying, wading and swimming use it. Hunger above 60 slows recovery; stored food reduces hunger.\n\nUnderwater breath lasts 40 seconds. A low-breath warning helps you surface; staying submerged after it runs out reduces health. At zero health, request assisted recovery or load a save. Assistance retains progress and is recorded.\n\nVehicles stop before flooded roads and pedestrian task areas. Stay on foot with escorted residents. Pause > Recover task can return you to supported footing if an exit is blocked.";
            var viewport=new Rect(r.x+22,r.y+110,r.width-44,r.height-188);float height=SibucaoDesktopHud.Text.CalcHeight(new GUIContent(SibucaoControls.Prompt(text)),viewport.width-24)+12;
            guideScroll=GUI.BeginScrollView(viewport,guideScroll,new Rect(0,0,viewport.width-24,Mathf.Max(viewport.height,height)));
            SibucaoDesktopHud.Label(new Rect(0,0,viewport.width-24,height),text);GUI.EndScrollView();
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.yMax-60,r.width-280,44),"The storm continues while you read.",SibucaoDesktopHud.Small,SibucaoDesktopHud.Muted);
            if(GUI.Button(new Rect(r.xMax-252,r.yMax-60,230,38),"Close / J / Esc",SibucaoDesktopHud.ButtonStyle))S.missionGuide.Close();
            GUI.matrix=previous;
        }
        SibucaoMinimap minimap;
        void DrawMinimap(SibucaoHudLayout layout)
        {
            if(!minimap)minimap=S.GetComponent<SibucaoMinimap>();if(!minimap||!minimap.streetMap)return;
            float textGrowth=Mathf.InverseLerp(1,1.3f,S.settings.textScale);
            float mapHeight=Mathf.Lerp(104,94,textGrowth),lineHeight=Mathf.Lerp(18,26,textGrowth);
            Rect outer=layout.Map,area=new Rect(outer.x+7,outer.y+7,outer.width-14,mapHeight);
            float textTop=area.yMax+5;
            Panel(outer);
            GUI.DrawTextureWithTexCoords(area,minimap.streetMap,minimap.MapUV(area));
            var path=minimap.GuidancePath;var routeColor=Hex("FFD05B");
            for(int i=1;i<path.Count;i++)
            {
                var a=minimap.Project(path[i-1],area);var b=minimap.Project(path[i],area);
                MapLine(area,a,b,routeColor,2);
                if(i%4==0&&area.Contains(b)&&(b-a).sqrMagnitude>1)
                {
                    var heading=(b-a).normalized;var wing=new Vector2(-heading.y,heading.x);
                    MapLine(area,b,b-heading*8+wing*4,routeColor,3);
                    MapLine(area,b,b-heading*8-wing*4,routeColor,3);
                }
            }
            Vector2 target=minimap.Project(minimap.Target,area);target=new Vector2(Mathf.Clamp(target.x,area.xMin+10,area.xMax-10),Mathf.Clamp(target.y,area.yMin+10,area.yMax-10));
            var amber=Hex("E6784A");Line(target+Vector2.up*7,target+Vector2.right*7,amber,3);Line(target+Vector2.right*7,target+Vector2.down*7,amber,3);Line(target+Vector2.down*7,target+Vector2.left*7,amber,3);Line(target+Vector2.left*7,target+Vector2.up*7,amber,3);
            float angle=S.player.view.transform.eulerAngles.y*Mathf.Deg2Rad;Vector2 dir=new Vector2(Mathf.Sin(angle),-Mathf.Cos(angle)),side=new Vector2(dir.y,-dir.x);
            Vector2 center=area.center;Line(center+dir*10,center-dir*6+side*6,Hex("EEF4F2"),3);Line(center+dir*10,center-dir*6-side*6,Hex("EEF4F2"),3);Line(center-dir*6-side*6,center-dir*6+side*6,Hex("EEF4F2"),3);
            Label(new Rect(area.x+8,area.y+5,24,24),"N",small,Hex("EEF4F2"));
            string direction=minimap.Distance<3?"AT THE TASK":minimap.HomeTarget?"FRONT DOOR":minimap.DirectionText;
            SibucaoDesktopHud.Label(new Rect(outer.x+9,textTop,outer.width-18,lineHeight),direction,SibucaoDesktopHud.Small,Hex("FFD05B"));
            SibucaoDesktopHud.Label(new Rect(outer.x+9,textTop+lineHeight,outer.width-18,lineHeight),minimap.Distance.ToString("0")+" m to target",SibucaoDesktopHud.Small);
            var flood=S.GetComponent<FloodSimulationManager>();
            string footing=S.player.swimming&&S.player.swimming.IsSwimming?"Swimming":S.player.WaterDepthAtFeet>=.35f?"Wading · no run":S.player.WaterDepthAtFeet>.05f?"Shallow water":"Dry footing";
            SibucaoDesktopHud.Label(new Rect(outer.x+9,textTop+lineHeight*2,outer.width-18,lineHeight),footing,SibucaoDesktopHud.Small);
            var flow=flood?flood.GetFlowVelocity(S.player.transform.position):Vector3.zero;
            if(flow.magnitude>.12f&&S.player.WaterDepthAtFeet>.05f)
            {
                var local=S.player.transform.InverseTransformDirection(flow);
                string toward=Mathf.Abs(local.x)>Mathf.Abs(local.z)?local.x>0?"right":"left":local.z>0?"ahead":"back";
                SibucaoDesktopHud.Label(new Rect(outer.x+9,textTop+lineHeight*3,outer.width-18,lineHeight),"Flow "+toward+" "+flow.magnitude.ToString("0.0")+"m/s",SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);
            }
            
        }
        void MapLine(Rect bounds,Vector2 a,Vector2 b,Color color,float width)
        {
            Vector2 d=b-a;float begin=0,end=1;
            if(!ClipMap(-d.x,a.x-bounds.xMin,ref begin,ref end)||!ClipMap(d.x,bounds.xMax-a.x,ref begin,ref end)||!ClipMap(-d.y,a.y-bounds.yMin,ref begin,ref end)||!ClipMap(d.y,bounds.yMax-a.y,ref begin,ref end))return;
            Line(a+d*begin,a+d*end,color,width);
        }
        static bool ClipMap(float p,float q,ref float begin,ref float end)
        {
            if(Mathf.Abs(p)<.00001f)return q>=0;
            float r=q/p;if(p<0){if(r>end)return false;begin=Mathf.Max(begin,r);}else{if(r<begin)return false;end=Mathf.Min(end,r);}return true;
        }
        void InventoryIcon(PhysicalGrabbable item,Rect r,Color color)
        {
            if(!item)return;float x=r.x,y=r.y,w=r.width,h=r.height;
            if(item.kind==PhysicalItemKind.FirstAid){Border(new Rect(x,y+h*.2f,w,h*.65f),color);Border(new Rect(x+w*.3f,y,w*.4f,h*.2f),color);Line(new Vector2(x+w*.5f,y+h*.35f),new Vector2(x+w*.5f,y+h*.7f),color,3);Line(new Vector2(x+w*.32f,y+h*.52f),new Vector2(x+w*.68f,y+h*.52f),color,3);}
            else if(item.kind==PhysicalItemKind.Stick)Line(new Vector2(x+3,y+h-2),new Vector2(x+w-3,y+2),color,4);
            else if(item.kind==PhysicalItemKind.Strap){Border(new Rect(x+w*.2f,y,w*.6f,h),color);Border(new Rect(x+w*.28f,y+h*.25f,w*.44f,h*.18f),color);}
            else if(item.supplyBit==0){Border(new Rect(x+w*.2f,y+h*.2f,w*.6f,h*.8f),color);Border(new Rect(x+w*.35f,y,w*.3f,h*.2f),color);Line(new Vector2(x+w*.2f,y+h*.6f),new Vector2(x+w*.8f,y+h*.6f),color);}
            else if(item.supplyBit==2){Border(r,color);for(int i=0;i<3;i++)Line(new Vector2(x+5,y+8+i*7),new Vector2(x+w-5,y+8+i*7),color);}
            else{Border(new Rect(x,y+4,w*.55f,h-4),color);Border(new Rect(x+w*.68f,y+h*.25f,w*.25f,h*.75f),color);Line(new Vector2(x,y+h*.3f),new Vector2(x+w*.55f,y+h*.3f),color);}
        }
        void InventorySlot(SibucaoInventory bag,int index,Rect rect,bool clickable)
        {
            bool selected=bag.Selected==index;var item=bag.Slot(index);
            SignalStationUI.Draw(rect,selected?"inv_slot_selected":item?"inv_slot_filled":"inv_slot",1);
            InventoryIcon(item,new Rect(rect.x+rect.width*.23f,rect.y+rect.height*.18f,rect.width*.55f,rect.height*.6f),Hex("5FBF9F"));
            if(index<9)Label(new Rect(rect.x+4,rect.y+rect.height-19,22,20),(index+1).ToString(),small,Hex("7F8F8B"));
            if(item)Label(new Rect(rect.xMax-18,rect.y+4,18,22),"1",small,Hex("EEF4F2"));
            if(clickable&&GUI.Button(rect,GUIContent.none,GUIStyle.none))bag.Selected=index;
        }
        Vector2 inventoryScroll;
        void DrawInventory()
        {
            var bag=S.inventory;if(!bag||!bag.IsOpen)return;
            var layout=SibucaoHudLayout.Current;var previous=SibucaoDesktopHud.Begin(layout);var r=layout.Modal;
            Fill(new Rect(0,0,layout.Width,layout.Height),new Color(.02f,.05f,.06f,.92f));SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+18,r.width-44,30),"Go-bag  ·  "+bag.Count+" / 27",SibucaoDesktopHud.Heading);
            if(GUI.Button(new Rect(r.xMax-228,r.y+15,206,38),bag.ShowStatus?"Stored items":"Item status",SibucaoDesktopHud.ButtonStyle))bag.ShowStatus=!bag.ShowStatus;
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+56,r.width-44,22),bag.BagStatus+" · F carries; P packs.",SibucaoDesktopHud.Small,SibucaoDesktopHud.Muted);
            if(bag.ShowStatus)
            {
                var known=bag.KnownItems();float height=r.height-166,total=0;
                var names=new float[known.Count];var states=new float[known.Count];
                for(int i=0;i<known.Count;i++)
                {
                    names[i]=Mathf.Max(25,SibucaoDesktopHud.Text.CalcHeight(new GUIContent(SibucaoControls.Prompt(SibucaoInventory.ItemName(known[i]))),r.width-75)+3);
                    states[i]=Mathf.Max(22,SibucaoDesktopHud.Small.CalcHeight(new GUIContent(SibucaoControls.Prompt(bag.ItemStatus(known[i]))),r.width-75)+3);
                    total+=names[i]+states[i]+8;
                }
                inventoryScroll=GUI.BeginScrollView(new Rect(r.x+22,r.y+88,r.width-44,height),inventoryScroll,new Rect(0,0,r.width-66,Mathf.Max(height,total)));
                if(known.Count==0)SibucaoDesktopHud.Label(new Rect(0,0,r.width-70,60),"No items handled yet. Collect your bag and supplies to begin.");
                float rowY=0;
                for(int i=0;i<known.Count;i++)
                {SibucaoDesktopHud.Label(new Rect(0,rowY,r.width-75,names[i]),SibucaoInventory.ItemName(known[i]));SibucaoDesktopHud.Label(new Rect(0,rowY+names[i],r.width-75,states[i]),bag.ItemStatus(known[i]),SibucaoDesktopHud.Small,SibucaoDesktopHud.Accent);rowY+=names[i]+states[i]+8;}
                GUI.EndScrollView();
                if(GUI.Button(new Rect(r.x+22,r.yMax-60,r.width-44,38),SibucaoControls.Prompt("Close / I / Tab"),SibucaoDesktopHud.ButtonStyle))bag.Close();
                GUI.matrix=previous;return;
            }
            float cell=(r.width-44)/9,cellHeight=Mathf.Min(Mathf.Lerp(70,64,Mathf.InverseLerp(1,1.3f,S.settings.textScale)),(r.height-242)/3);
            for(int i=0;i<27;i++)InventorySlot(bag,i,new Rect(r.x+22+(i%9)*cell,r.y+88+(i/9)*cellHeight,cell-5,cellHeight-5),true);
            float detailsY=r.y+92+cellHeight*3;
            SibucaoDesktopHud.Label(new Rect(r.x+22,detailsY,r.width-44,r.yMax-132-detailsY),SibucaoInventory.ItemName(bag.Slot(bag.Selected))+" · "+bag.ItemStatus(bag.Slot(bag.Selected)),SibucaoDesktopHud.Text);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.yMax-124,r.width-44,52),"Take out equips an item in your hand. Item status includes dropped, lost and used supplies. The storm continues here.",SibucaoDesktopHud.Small,SibucaoDesktopHud.Muted);
            bool occupied=bag.Slot(bag.Selected),old=GUI.enabled;float w=(r.width-56)/3;GUI.enabled=old&&occupied;
            if(GUI.Button(new Rect(r.x+22,r.yMax-60,w,38),occupied&&bag.Slot(bag.Selected).inventoryTag=="food"?"Eat selected":"Take out",SibucaoDesktopHud.ButtonStyle))bag.EquipSelected();
            if(GUI.Button(new Rect(r.x+28+w,r.yMax-60,w,38),"Drop selected",SibucaoDesktopHud.ButtonStyle))bag.DropSelected();
            GUI.enabled=old;
            if(GUI.Button(new Rect(r.x+34+w*2,r.yMax-60,w,38),"Close / I / Tab",SibucaoDesktopHud.ButtonStyle))bag.Close();
            GUI.matrix=previous;
        }
        void PhysicalComplete()
        {
            ReleaseResults();
        }
        void DifficultySelection(){S.SelectRole();}
        void RoleSelection()
        {
            ReleaseRoles();
        }
        void ArrivalReview()
        {
            ReleaseArrival();
        }
        void HomeHud()
        {
            var home=S.home;
            if(!home.MovementLocked)
            {
                SignalStationUI.Draw(new Rect(704,434,32,32),"crosshair");
                if(S.settings.interactPrompts&&!string.IsNullOrEmpty(home.Prompt))
                {Panel(new Rect(490,610,480,60));Label(new Rect(513,627,440,40),home.Prompt,body);}
            }
            string warning=home.neighborWarning?home.neighborWarning.Subtitle:null;
            if(S.settings.subtitles&&!string.IsNullOrEmpty(warning))
            {Panel(new Rect(250,785,940,65));Label(new Rect(275,803,890,36),warning,body);}
        }
        int CountResolved(){int n=0;for(int i=1;i<=7;i++)if(S.state.resolved[i])n++;return n;}
        void Dialogue()
        {
            var e=S.activeEncounter;if(!e)return;var n=e.node;
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.5f));Panel(new Rect(259,201,922,518));
            Portrait(new Rect(282,225,134,165));Label(new Rect(444,225,671,36),n.speaker,h3,Primary);
            Label(new Rect(444,269,671,47),n.title,h2);Label(new Rect(444,326,687,99),n.message,body);
            for(int i=0;i<n.choices.Length;i++)
            {
                var c=n.choices[i];bool enabled=S.IsAvailable(c);
                if(Button(new Rect(284,446+i*68,872,57),c.label+(enabled?"":"  (kit already used)"),false,null,enabled))S.Choose(i);
            }
            Label(new Rect(286,683,790,24),"Choose your response.   Esc: return to the scene",small);
            if(Button(new Rect(1117,220,42,38),"×")){S.activeEncounter=null;S.SetMode(SibucaoMode.Playing);}
        }
        void Pause()
        {
            var l=SibucaoHudLayout.Current;var old=SibucaoDesktopHud.Begin(l);var r=l.Modal;SibucaoDesktopHud.Panel(r);
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.y+18,r.width-44,30),"Paused",SibucaoDesktopHud.Heading);
            float y=r.y+62;float h=Mathf.Min(46,(r.height-135)/6);
            if(PanelButton(r,y,h,"Resume"))S.SetMode(SibucaoMode.Playing);y+=h+6;
            if(PanelButton(r,y,h,"Save game · manual slot"))S.SaveRun();y+=h+6;
            if(PanelButton(r,y,h,"Load manual save / checkpoint"))S.LoadRun();y+=h+6;
            if(PanelButton(r,y,h,"Recover task / return to unfinished junction"))S.OpenRecovery();y+=h+6;
            if(PanelButton(r,y,h,"Settings"))S.OpenSettings();y+=h+6;
            if(PanelButton(r,y,h,"Main menu"))S.Confirm("menu");
            SibucaoDesktopHud.Label(new Rect(r.x+22,r.yMax-42,r.width-44,28),S.CheckpointStatus,SibucaoDesktopHud.Small);
            GUI.matrix=old;
        }
        bool Toggle(float y,string label,bool value)
        {
            Label(new Rect(361,y+8,400,36),label,body);
            if(Button(new Rect(829,y,246,45),value?"On":"Off"))value=!value;
            SignalStationUI.Draw(new Rect(995,y+10,64,26),value?"toggle_on":"toggle_off");
            return value;
        }
        float Slider(float y,string label,float value,float min,float max,string readout)
        {
            Label(new Rect(361,y,350,33),label,body);
            Rect rail=new Rect(759,y+13,251,7);SignalStationUI.Draw(rail,"slider_track");float t=Mathf.InverseLerp(min,max,value);SignalStationUI.Draw(new Rect(rail.x,rail.y,rail.width*t,7),"slider_fill");
            Rect hit=new Rect(rail.x-10,y,rail.width+20,35);int id=GUIUtility.GetControlID(FocusType.Passive,hit);
            if(Event.current.type==EventType.MouseDown && hit.Contains(Event.current.mousePosition)){GUIUtility.hotControl=id;value=Mathf.Lerp(min,max,Mathf.Clamp01((Event.current.mousePosition.x-rail.x)/rail.width));Event.current.Use();}
            if(GUIUtility.hotControl==id && (Event.current.type==EventType.MouseDrag||Event.current.type==EventType.MouseDown)){value=Mathf.Lerp(min,max,Mathf.Clamp01((Event.current.mousePosition.x-rail.x)/rail.width));Event.current.Use();}
            if(Event.current.type==EventType.MouseUp&&GUIUtility.hotControl==id){GUIUtility.hotControl=0;Event.current.Use();}
            Fill(new Rect(rail.x+rail.width*t-6,y+8,13,18),Primary);Label(new Rect(1030,y,85,30),readout,small);return value;
        }
        void Settings()
        {
            ReleaseSettings();
        }
        void Confirmation()
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.65f));Panel(new Rect(452,301,536,276),"Confirm");Icon("question",new Rect(481,410,36,36),Color.white);
            Label(new Rect(538,400,414,66),S.confirmation=="exit"?"Are you sure you want to exit the game?":"Return to the main menu? Unsaved progress will be lost.",body);
            if(Button(new Rect(540,490,175,50),"Yes",true))S.ConfirmYes();if(Button(new Rect(735,490,175,50),"No"))S.SetMode(S.confirmReturn);
        }
        void Complete()
        {
            if(S.physicalDecisions){PhysicalComplete();return;}
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.68f));Panel(new Rect(104,57,1232,785));
            Label(new Rect(144,86,950,65),"Mission Complete",h1,Primary);Label(new Rect(145,160,1100,45),"You reached Banago Elementary School I.",h3);
            int seconds=Mathf.RoundToInt(S.state.elapsed);Label(new Rect(145,212,1110,30),$"Evacuation time  {seconds/60:00}:{seconds%60:00}     •     Encounters  {CountResolved()}/7     •     {(S.state.companion?"Arrived with a resident":"Arrived at shelter")}",body);
            Fill(new Rect(145,263,1150,1),Hex("525252"));Label(new Rect(146,287,475,48),"Your preparedness review",h2);
            string[] names={"Preparedness","Evacuation","Decision-Making","Resource Management","Teamwork"};
            for(int i=0;i<5;i++)
            {
                float y=356+i*63;Label(new Rect(146,y,329,31),names[i],body);Label(new Rect(503,y,105,31),S.state.scores[i].ToString(),body,Color.white);
                SignalStationUI.Bar(new Rect(147,y+34,439,6),S.state.scores[i]/140f);
            }
            Label(new Rect(660,287,620,48),"Decisions along the way",h2);
            scroll=GUI.BeginScrollView(new Rect(658,347,638,350),scroll,new Rect(0,0,600,7*116));
            for(int i=1;i<=7;i++)
            {
                var n=S.encounters[i-1].node;float y=(i-1)*116;Label(new Rect(6,y,575,30),n.title,body,Primary);
                string label=S.state.chosen[i]>=0?n.choices[S.state.chosen[i]].label:"Not encountered";Label(new Rect(6,y+32,575,39),label,small);Label(new Rect(6,y+72,575,41),S.state.outcomes[i]??"",small,Hex("CBCBCB"));
            }
            GUI.EndScrollView();Label(new Rect(146,705,1100,30),"Preparedness reflects the supplies packed at home. Scores are revealed only here.",small);
            if(Button(new Rect(146,766,262,50),"Play Again",true,"play"))S.SelectRole();if(Button(new Rect(431,766,262,50),"Main Menu",false,"home")){S.SetMode(SibucaoMode.Menu);S.weather.SetStorm(false);}
        }
        void About()
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.6f));Panel(new Rect(340,218,760,424),"STORMSIM");
            Label(new Rect(375,321,690,209),"Purok Sibucao\nTyphoon preparedness simulation\n\nFollow the coastal streets to the school. Observe your surroundings, talk to residents, and decide how to respond.\n\nRoboto © Google, Apache License 2.0.",body);
            if(Button(new Rect(872,562,190,51),"Back",true))S.SetMode(SibucaoMode.Menu);
        }
        // Resource textures belong to Unity and are shared across scene reloads.
    }
}
