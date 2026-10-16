using UnityEngine;

namespace StormSim.Sibucao
{
    /// <summary>Shared desktop safe areas. All gameplay overlays use the same scale.</summary>
    public readonly struct SibucaoHudLayout
    {
        public readonly float Scale, Width, Height, Bottom;
        public SibucaoHudLayout(float width, float height)
        {
            Scale = Mathf.Max(.1f, Mathf.Min(1.25f, width / 960f, height / 540f));
            Width = width / Scale; Height = height / Scale;
            // Keep the real bottom 44 pixels available for source attribution.
            Bottom = Height - Mathf.Max(48, 44 / Scale);
        }
        public Rect Objective => new Rect(16, 16, Mathf.Min(440, Width * .49f), 94);
        public Rect Routes => new Rect(16, 120, Objective.width, 138);
        public Rect Map { get { float t=Mathf.InverseLerp(1,1.3f,SibucaoControls.Settings?.textScale??1); float height=Mathf.Lerp(204,224,t); return new Rect(16,Bottom-height,Mathf.Lerp(174,200,t),height); } }
        public Rect Vitals => new Rect(Width - 210, Bottom - 108, 194, 108);
        public Rect Action => new Rect((Width - ActionWidth) * .5f, Bottom - 88, ActionWidth, 88);
        float ActionWidth => Mathf.Min(520, Width - 452);
        public Rect Subtitle => new Rect(Action.x, Bottom - 166, Action.width, 68);
        public Rect Instruction => new Rect(16, 116, Objective.width, 88);
        public Rect Toast => new Rect(Width - 338, 16, 322, 106);
        public Rect Help => new Rect(Width - 210, Bottom - 158, 194, 44);
        public Rect Modal => new Rect((Width - Mathf.Min(720, Width - 64)) * .5f, 28, Mathf.Min(720, Width - 64), Bottom - 44);
        public static SibucaoHudLayout Current => new SibucaoHudLayout(Screen.width, Screen.height);
    }

    public static class SibucaoDesktopHud
    {
        public static readonly Color Ink = new Color32(238,244,242,255);
        public static readonly Color Muted = new Color32(176,197,193,255);
        public static readonly Color Accent = new Color32(116,212,183,255);
        public const string Controls = "WASD / Mouse   Move / look\nE   Interact, pick up, check or pack\nQ   Put down / cancel\nI / Tab   Bag\nJ   Guide\nF   Optional carry / release shortcut\nP   Optional pack shortcut\nShift   Run    Space   Jump\nTools: R + mouse turns; wheel adjusts reach\nMechanisms: hold E and follow the action prompt\nIn water: Space rises; Ctrl or C dives";
        static GUIStyle text, small, heading, button,narrationSubtitle;
        static Font arial;
        static GUIStyle Sized(ref GUIStyle style,int size,bool bold=false){if(style==null)style=Style(size,bold);style.fontSize=Mathf.RoundToInt(size*(SibucaoControls.Settings?.textScale??1));return style;}
        public static GUIStyle Text => Sized(ref text,16);
        public static GUIStyle Small => Sized(ref small,13);
        public static GUIStyle Heading => Sized(ref heading,20,true);
        public static GUIStyle NarrationSubtitleStyle
        {
            get
            {
                if(!arial)arial=Font.CreateDynamicFontFromOSFont(new[]{"Arial","Arial Unicode MS","Liberation Sans"},18);
                if(narrationSubtitle==null)narrationSubtitle=new GUIStyle
                {
                    font=arial?arial:SignalStationUI.Font(),fontSize=14,fontStyle=FontStyle.Italic,
                    alignment=TextAnchor.MiddleCenter,wordWrap=false,
                    normal={textColor=new Color32(255,221,87,255)}
                };
                narrationSubtitle.fontSize=Mathf.RoundToInt(14*(SibucaoControls.Settings?.textScale??1));
                return narrationSubtitle;
            }
        }
        public static GUIStyle ButtonStyle {get{if(button==null)button=new GUIStyle(GUI.skin.button){font=SignalStationUI.Font(),wordWrap=true};button.fontSize=Mathf.RoundToInt(15*(SibucaoControls.Settings?.textScale??1));return button;}}
        static GUIStyle Style(int size, bool bold = false) => new GUIStyle { font = SignalStationUI.Font(bold), fontSize = size, wordWrap = true, normal = { textColor = Ink } };
        public static Matrix4x4 Begin(SibucaoHudLayout layout)
        {
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1));
            return previous;
        }
        public static void Panel(Rect rect) => SignalStationUI.Draw(rect, "panel_bg_clipped", 12);
        public static void Label(Rect rect, string value, GUIStyle style = null, Color? color = null)
        {
            var chosen = style ?? Text; var previous = chosen.normal.textColor;
            chosen.normal.textColor = color ?? Ink; GUI.Label(rect, SibucaoControls.Prompt(value), chosen); chosen.normal.textColor = previous;
        }
        public static void Objective(SibucaoHudLayout layout, string title, string objective)
        {
            var r = layout.Objective; Panel(r);
            Label(new Rect(r.x+14,r.y+10,r.width-28,20),title,Small,Accent);
            var content=new Rect(r.x+14,r.y+34,r.width-28,54);Label(content,objective,Heading.CalcHeight(new GUIContent(objective),content.width)>content.height?Text:Heading);
        }
        public static void Action(SibucaoHudLayout layout, string text)
        {
            if(string.IsNullOrWhiteSpace(text))return;
            var r=layout.Action; Panel(r);
            var content=new Rect(r.x+14,r.y+12,r.width-28,r.height-20);
            Label(content,text,Text.CalcHeight(new GUIContent(text),content.width)>content.height?Small:Text);
        }
        public static void Instruction(SibucaoHudLayout layout,string text)
        {
            if(string.IsNullOrWhiteSpace(text))return;
            var r=layout.Instruction;Panel(r);
            Label(new Rect(r.x+12,r.y+7,r.width-24,16),"CURRENT INSTRUCTION",Small,Accent);
            var content=new Rect(r.x+12,r.y+25,r.width-24,r.height-29);
            Label(content,text,Small);
        }
        public static void Subtitle(SibucaoHudLayout layout, string text)
        {
            if(string.IsNullOrWhiteSpace(text))return;
            var r=layout.Subtitle;
            var style=Text.CalcHeight(new GUIContent(text),r.width-28)>r.height-18?Small:Text;
            r.height=Mathf.Max(r.height,style.CalcHeight(new GUIContent(text),r.width-28)+20);
            r.y=layout.Action.y-10-r.height;
            Panel(r);
            var content=new Rect(r.x+14,r.y+10,r.width-28,r.height-18);
            Label(content,text,style);
        }
        public static void NarrationSubtitle(SibucaoHudLayout layout,string text)
        {
            if(string.IsNullOrWhiteSpace(text))return;
            // Narration is intentionally one short phrase at a time. The voice
            // director guarantees no more than six words in this line.
            float width=Mathf.Min(640,layout.Width-80),height=28;
            var r=new Rect((layout.Width-width)*.5f,layout.Bottom-126,width,height);
            var style=NarrationSubtitleStyle;
            var shadow=style.normal.textColor;style.normal.textColor=new Color(0,0,0,.9f);
            GUI.Label(new Rect(r.x+1,r.y+2,r.width,r.height),text,style);
            style.normal.textColor=new Color32(255,221,87,255);GUI.Label(r,text,style);
            style.normal.textColor=shadow;
        }
        public static void Vitals(SibucaoHudLayout layout, float health, float stamina, float hunger)
        {
            var r=layout.Vitals; Panel(r);
            Vital(r,0,"Health",health,new Color(.85f,.36f,.34f));
            Vital(r,1,"Stamina",stamina,Accent);
            Vital(r,2,"Hunger",hunger,new Color(.95f,.68f,.25f));
        }
        static void Vital(Rect r,int row,string name,float value,Color color)
        {
            float y=r.y+8+row*32;
            Label(new Rect(r.x+12,y,r.width-24,24),name+"  "+Mathf.RoundToInt(value),Small);
            var previous=GUI.color;
            GUI.color=new Color(.12f,.19f,.20f);GUI.DrawTexture(new Rect(r.x+12,y+26,r.width-24,4),Texture2D.whiteTexture);
            GUI.color=color;GUI.DrawTexture(new Rect(r.x+12,y+26,(r.width-24)*Mathf.Clamp01(value/100),4),Texture2D.whiteTexture);GUI.color=previous;
        }
    }

    public static class SibucaoDevelopmentControls
    {
        public const string EditorPreference = "StormSim.Sibucao.ShowDevelopmentControls";
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(EditorPreference, false);
#elif DEVELOPMENT_BUILD
                return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stormsim-dev-controls") >= 0;
#else
                return false;
#endif
            }
        }
    }
}
