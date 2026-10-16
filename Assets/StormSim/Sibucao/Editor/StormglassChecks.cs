using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StormSim.UI;

namespace StormSim.Editor
{
    public static class StormglassChecks
    {
        // Hardware-independent tests of the same ray/release path used by Quest.
        public static string CheckPointer()
        {
            var root = new GameObject("Stormglass pointer test", typeof(RectTransform));
            GameObject events = null;
            var eventSystem = EventSystem.current;
            if (!eventSystem) { events = new GameObject("Stormglass test events", typeof(EventSystem)); eventSystem = events.GetComponent<EventSystem>(); }
            var results = new List<string>();
            try
            {
                var view = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>(); view.SetParent(root.transform); view.sizeDelta = new Vector2(2,2);
                var go = new GameObject("Test action", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(view); go.transform.localPosition=Vector3.zero;
                var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1,1);var button=go.GetComponent<Button>();
                var buttons=new List<Button>{button};int clicks=0;button.onClick.AddListener(()=>clicks++);
                var pointer=new StormglassPointer(eventSystem,-101);
                var hit=new Ray(new Vector3(0,0,-2),Vector3.forward);var miss=new Ray(new Vector3(3,0,-2),Vector3.forward);
                void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);results.Add("PASS: "+label);}
                void Feed(Ray ray,bool held){pointer.Process(ray,held,buttons,view,out _);}
                Feed(hit,false);Feed(hit,true);Feed(hit,true);Check(clicks==0,"holding never auto-clicks");Feed(hit,false);Check(clicks==1,"release over same target clicks once");Feed(hit,false);Check(clicks==1,"idle does not repeat");
                Feed(hit,true);Feed(miss,false);Check(clicks==1,"release outside cancels");
                pointer.Reset();Feed(hit,true);Feed(hit,false);Check(clicks==1,"held trigger on panel-open cannot click");
                button.interactable=false;Feed(hit,false);Feed(hit,true);Feed(hit,false);Check(clicks==1,"disabled action cannot click");button.interactable=true;
                go.transform.localPosition=new Vector3(0,2,0);var clipped=new Ray(new Vector3(0,2,-2),Vector3.forward);Feed(clipped,false);Feed(clipped,true);Feed(clipped,false);Check(clicks==1,"clipped scroll content cannot click");
                go.transform.localPosition=Vector3.zero;Feed(hit,false);Feed(hit,true);button.gameObject.SetActive(false);Feed(hit,false);Check(clicks==1,"hidden action cannot click");
                return string.Join("\n",results);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);if(events)UnityEngine.Object.DestroyImmediate(events);}
        }
        public static string InspectLive()
        {
            var ui=StormglassUI.Active;if(!ui)throw new Exception("Stormglass UI missing");
            UnityEngine.Canvas.ForceUpdateCanvases();var problems=new List<string>();
            if(ui.Canvas.renderMode!=RenderMode.WorldSpace)problems.Add("Canvas not world-space");
            if(!ui.Canvas.worldCamera)problems.Add("Canvas camera missing");
            foreach(var b in ui.Buttons)
            {
                var r=(RectTransform)b.transform;
                if(r.rect.height<60)problems.Add(b.name+" short hit area");
                var text=b.GetComponentInChildren<TMPro.TMP_Text>();
                if(!text||!text.font)problems.Add(b.name+" missing font");
                else if(text.isTextOverflowing)problems.Add(b.name+" text overflow");
            }
            return "Screen="+ui.ScreenKey+"; Buttons="+ui.Buttons.Count+"; "+(problems.Count==0?"PASS":string.Join("; ",problems));
        }
    }
}
