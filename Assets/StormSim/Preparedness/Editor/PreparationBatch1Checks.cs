using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    // Exercise production actions in the actual scene. This is a regression
    // fixture, not a continuous or first-time-player playtest.
    public static class PreparationBatch1Checks
    {
        public static string Run()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();
            var bridge=game.GetComponent<SibucaoPreparationBridge>();
            if(!Application.isPlaying||!bridge.Active)throw new Exception("Start SIBUCAO_RAIN preparation first.");
            var s=bridge.preparation;var p=s.player;var baseline=s.Capture();var results=new List<string>();
            bool hints=game.settings.hints;float advisoryDelay=s.evacuationAfterSeconds;
            bool hadSave=File.Exists(s.SavePath);byte[] saveBytes=hadSave?File.ReadAllBytes(s.SavePath):null;
            void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);results.Add("PASS: "+name);}
            PreparednessItem Item(PrepKind kind)=>s.items.First(i=>i.kind==kind);
            void Reset(){p.CloseDesktopPanels();s.Restore(baseline);s.SetPhase(PrepPhase.Preparing);s.evacuationOrdered=false;s.advisoryRead=false;s.preparationSeconds=0;s.departure="";p.ReleaseAll();p.focused=null;}
            void Update()=>typeof(PreparednessSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
            try
            {
                Reset();s.Finish(true);Check(s.CanPrepare&&s.message.Contains("television"),"Early departure requests the current advisory");
                s.Use(Item(PrepKind.Advisory));s.Finish(true);
                Check(s.phase==PrepPhase.Fade&&s.departure=="early-evacuation"&&s.decisions[19],"Advertised early departure works without an evacuation order or completed chores");
                var departureSave=JsonUtility.FromJson<PrepSave>(File.ReadAllText(s.SavePath));
                Check(departureSave.phase==PrepPhase.Fade&&departureSave.departure=="early-evacuation","Departure save resumes the transition rather than returning to preparation");
                s.CompleteTransition();Check(s.phase==PrepPhase.ReadyForStorm,"Early exit reaches the storm handoff state");
                float early=SibucaoPreparationBridge.DepartureClock(bridge.firstHome,"early-evacuation");
                float ordered=SibucaoPreparationBridge.DepartureClock(bridge.firstHome,"ordered-evacuation");
                Check(early<ordered&&early>=bridge.firstHome.wakeDuration+bridge.firstHome.newsDuration+bridge.firstHome.standDuration,"Early departure has flood head-start without replaying wake-up");

                Reset();s.evacuationAfterSeconds=1;s.preparationSeconds=1;Update();
                Check(s.evacuationOrdered&&!s.advisoryRead,"Normal session Update issues the timed advisory");
                Check(p.CurrentObjective.Contains("exit")&&!s.OutdoorSafe,"Advisory prioritizes departure and blocks outdoor preparation");
                s.Finish(false);Check(s.CanPrepare,"Rest is unavailable during an evacuation advisory");
                s.Finish(true);Check(s.phase==PrepPhase.Fade&&s.departure=="ordered-evacuation"&&s.decisions[19],"Ordered departure does not force a return to the TV");

                Reset();s.preparationSeconds=1;p.ToggleGuide();Update();
                Check(!s.evacuationOrdered&&s.preparationSeconds==1,"Reading the guide pauses the preparation clock");
                p.CloseDesktopPanels();game.SetMode(SibucaoMode.Settings);Update();
                Check(!s.evacuationOrdered,"Settings pauses the preparation clock");game.SetMode(SibucaoMode.Preparation);
                s.evacuationAfterSeconds=advisoryDelay;

                Reset();s.advisoryRead=true;
                // Clear the authored table only for the rest-placement fixture.
                foreach(var item in s.items.Where(i=>i.portable&&i!=s.Bag&&i.storedIn=="table"))item.storedIn="";
                s.Bag.storedIn="table";s.Finish(false);Check(s.CanPrepare&&s.message.Contains("tomorrow"),"First bed interaction explains the rest choice");
                s.Finish(false);Check(s.phase==PrepPhase.Fade&&s.departure=="overnight","Second bed interaction confirms rest");
                s.CompleteTransition();Check(s.phase==PrepPhase.ReadyForStorm&&!s.evacuationOrdered,"Rest hands directly to the mid-typhoon wake sequence");

                Reset();Check(PreparednessSession.GroupChecks.SelectMany(x=>x).OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,20)),"Five guide groups cover each readiness check exactly once");
                Check(s.GroupDetails(2).Contains("Optional")&&s.GroupDetails(3).Contains("not required")&&s.GroupDetails(4).Contains("depart early"),"Guide labels priorities and optional work and explains departure choices");
                game.settings.hints=false;Check(!p.CurrentObjective.Contains("document")&&!s.Guided,"Hints-off mode avoids step-by-step supply recommendations");game.settings.hints=true;

                s.tutorialProgress=0;var documents=Item(PrepKind.Documents);var door=documents.storageDoor;
                if(door){door.closed=true;p.focused=door;p.UseFocused();Check(!door.closed&&(s.tutorialProgress&1)!=0,"E opens storage and records the first tutorial action");}
                documents.closed=false;documents.prepared=false;documents.storedIn="";documents.gameObject.SetActive(true);
                p.focused=documents;p.UseFocused();Check(p.held==documents&&(s.tutorialProgress&2)!=0,"E picks up a supply");
                p.focused=null;p.UseFocused();Check(documents.closed&&documents.prepared&&(s.tutorialProgress&4)!=0,"E checks and seals a held document pouch");
                p.PutDown();Check(!p.held,"Q action releases the held supply");
                s.Bag.storageDoor=null;s.Bag.closed=true;p.focused=s.Bag;p.UseFocused();Check(p.carriedBag==s.Bag,"E collects the go-bag");
                p.focused=documents;p.UseFocused();p.focused=null;p.UseFocused();Check(!s.Bag.closed,"E opens a carried bag before packing");
                // Put the bag in reach in the clear, using production packing collision checks.
                p.Warp(s.homeStart.position+Vector3.up*6,s.homeStart.rotation);
                s.Bag.transform.position=p.view.transform.position+p.view.transform.forward*.75f;
                s.Bag.SynchronizePose();Physics.SyncTransforms();
                p.UseFocused();Check(p.Packing,"E starts the normal packing animation");
                Check(s.PackHeld()&&documents.IsPacked&&(s.tutorialProgress&8)!=0,"Checked supply enters the bag and advances tutorial state");
                Check(s.decisions[7],"Packed sealed documents satisfy readiness");

                s.preparationSeconds=123;s.guidanceUsed=true;var snapshot=s.Capture();
                s.preparationSeconds=0;s.tutorialProgress=0;s.guidanceUsed=false;
                Check(s.Restore(JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(snapshot)))&&s.preparationSeconds==123&&s.guidanceUsed&&s.tutorialProgress==snapshot.tutorialProgress,"Clock, guidance use and tutorial progress survive JSON restore");
                Check(p.carriedBag==s.Bag&&documents.IsPacked,"Carried bag and packed contents survive restore");
                var legacy=JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(snapshot));legacy.preparationSeconds=0;legacy.tutorialProgress=0;legacy.guidanceUsed=false;
                Check(s.Restore(legacy),"Version-2 saves without new progress fields remain compatible");
                s.Restart();Check(s.preparationSeconds==0&&s.tutorialProgress==0&&!s.evacuationOrdered,"Restart clears advisory and tutorial progress");
                var text=string.Join("\n",results)+"\nTOTAL: "+results.Count+" checks passed.\nContinuous and human playtests remain separate.";
                File.WriteAllText("Documentation/SIBUCAO_PREPARATION_BATCH1_CHECKS.txt",text);return text;
            }
            finally
            {
                game.settings.hints=hints;s.evacuationAfterSeconds=advisoryDelay;
                game.SetMode(SibucaoMode.Preparation);p.CloseDesktopPanels();s.Restore(baseline);
                if(hadSave)File.WriteAllBytes(s.SavePath,saveBytes);else if(File.Exists(s.SavePath))File.Delete(s.SavePath);
            }
        }
    }
}
