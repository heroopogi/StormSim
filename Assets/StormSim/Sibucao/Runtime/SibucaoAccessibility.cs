using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace StormSim.Sibucao
{
    public static class SibucaoControls
    {
        public static readonly Key[] Defaults={Key.W,Key.S,Key.A,Key.D,Key.E,Key.F,Key.Q,Key.P,Key.I,Key.J,Key.LeftShift,Key.Space,Key.LeftCtrl,Key.R};
        public static readonly string[] Names={"Forward","Backward","Left","Right","Use / mechanism","Carry / release","Put down / cancel","Pack","Bag","Guide","Run","Jump / rise / brake","Dive","Rotate tool"};
        public static SibucaoSettings Settings=>SibucaoSession.Instance?SibucaoSession.Instance.settings:null;
        public static Key Bound(Key original)
        {
            var settings=Settings;int i=Array.IndexOf(Defaults,original);
            if(i<0||settings==null||settings.keyBindings==null||settings.keyBindings.Length!=Defaults.Length||!Enum.TryParse(settings.keyBindings[i],out Key key)||!Allowed(key))return original;
            return key;
        }
        public static KeyControl Button(Key original)=>Keyboard.current[Bound(original)];
        public static void Ensure(SibucaoSettings settings)
        {
            bool valid=settings.keyBindings!=null&&settings.keyBindings.Length==Defaults.Length;
            var used=new System.Collections.Generic.HashSet<Key>();
            if(valid)foreach(var value in settings.keyBindings)if(!Enum.TryParse(value,out Key key)||!Allowed(key)||!used.Add(key)){valid=false;break;}
            if(!valid)settings.keyBindings=Array.ConvertAll(Defaults,x=>x.ToString());
        }
        static bool Allowed(Key key)=>Enum.IsDefined(typeof(Key),key)&&key!=Key.None&&key!=Key.Escape&&key!=Key.Tab&&key!=Key.RightShift&&key!=Key.B&&key!=Key.G&&key!=Key.C&&key!=Key.V&&key!=Key.N&&!(key>=Key.F1&&key<=Key.F12);
        public static bool Rebind(SibucaoSettings settings,int index,Key key)
        {
            Ensure(settings);
            if(index<0||index>=Defaults.Length||!Allowed(key))return false;
            int other=Array.IndexOf(settings.keyBindings,key.ToString());
            if(other>=0&&other!=index)settings.keyBindings[other]=settings.keyBindings[index];
            settings.keyBindings[index]=key.ToString();ResetGrip();return true;
        }
        public static string Display(Key original)=>Bound(original).ToString().Replace("LeftShift","Shift").Replace("LeftCtrl","Ctrl");
        public static string Prompt(string text)
        {
            if(string.IsNullOrEmpty(text)||Settings==null)return text;
            // Only replace explicit control tokens, leaving mission IDs and ordinary words intact.
            if(Settings.toggleGrip)text=System.Text.RegularExpressions.Regex.Replace(text,@"\b[Hh]old E\b","Toggle E grip");
            return System.Text.RegularExpressions.Regex.Replace(text,@"\b(WASD|Shift|Space|Ctrl|E|F|Q|P|I(?=\s*(?:/|bag))|J|R)\b",m=>
            {
                if(m.Value=="WASD")return Bound(Key.W)==Key.W&&Bound(Key.A)==Key.A&&Bound(Key.S)==Key.S&&Bound(Key.D)==Key.D?"WASD":Display(Key.W)+" / "+Display(Key.A)+" / "+Display(Key.S)+" / "+Display(Key.D);
                Key key=m.Value=="Shift"?Key.LeftShift:m.Value=="Ctrl"?Key.LeftCtrl:Enum.Parse<Key>(m.Value);
                return Display(key);
            });
        }
        static int gripFrame=-1,releasedFrame=-1;static bool grip;
        public static bool GripReleasedThisFrame=>releasedFrame==Time.frameCount;
        public static void BeginGrip(){if(Settings!=null&&Settings.toggleGrip){grip=true;gripFrame=Time.frameCount;}}
        public static void ResetGrip(){grip=false;gripFrame=-1;}
        public static bool WorkHeld
        {
            get
            {
                if(UnityEngine.XR.XRSettings.isDeviceActive)return StormSim.UI.StormglassXR.WorkHeld;
                if(Keyboard.current==null)return false;
                if(Settings==null||!Settings.toggleGrip)return Button(Key.E).isPressed;
                if(releasedFrame==Time.frameCount)return false;
                if(!grip)return Button(Key.E).isPressed;
                if(gripFrame!=Time.frameCount){gripFrame=Time.frameCount;if(Button(Key.E).wasPressedThisFrame){grip=false;releasedFrame=Time.frameCount;}}
                return grip;
            }
        }
    }
}
