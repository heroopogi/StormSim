using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using StormSim.Sibucao;
using StormSim.Preparedness;

public static class PreparationBatch1Live
{
    public static void Run(bool coaching=false,bool remaining=false)
    {
        if(!Application.isPlaying)throw new Exception("Play Mode is required.");
        var game=SibucaoSession.Instance;
        var bridge=game.GetComponent<SibucaoPreparationBridge>();
        if(!bridge.Active)game.StartRun();
        string folder=remaining?"Reports/PreparationRemaining_20261014/":coaching?"Reports/PreparationCoaching_20261014/":"Reports/PreparationBatch1_20261013/";
        Directory.CreateDirectory(folder);
        game.UseTestSaveDirectory(Path.GetFullPath(folder+"saves"));
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var assembly=typeof(EditorWindow).Assembly;
        var viewType=assembly.GetType("UnityEditor.GameView");
        var view=EditorWindow.GetWindow(viewType);view.Show();view.Focus();
        var sizeIndex=viewType.GetProperty("selectedSizeIndex",flags);
        int oldSize=(int)sizeIndex.GetValue(view);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",flags).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var resolutionType=assembly.GetType("UnityEditor.GameViewSizeType");
        void Size(int width,int height)
        {
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null),index=-1;
            for(int i=0;i<count;i++)
            {
                var size=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                if((int)sizeType.GetProperty("width",flags).GetValue(size)==width&&(int)sizeType.GetProperty("height",flags).GetValue(size)==height){index=i;break;}
            }
            if(index<0)
            {
                index=count;var constructor=sizeType.GetConstructor(flags,null,new[]{resolutionType,typeof(int),typeof(int),typeof(string)},null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{constructor.Invoke(new[]{Enum.Parse(resolutionType,"FixedResolution"),(object)width,height,"StormSim "+width+"x"+height})});
            }
            sizeIndex.SetValue(view,index);view.Repaint();
        }
        var prep=bridge.preparation;var player=prep.player;
        bool oldHints=game.settings.hints,oldBackground=Application.runInBackground;
        float oldText=game.settings.textScale;
        bool hadSave=File.Exists(prep.SavePath);var save=hadSave?File.ReadAllBytes(prep.SavePath):null;
        var lines=new List<string>();
        void Check(bool passed,string text){if(!passed)throw new Exception(text);lines.Add("PASS: "+text);File.WriteAllLines(folder+"live-checks.txt",lines);}
        IEnumerator Cases()
        {
            Application.runInBackground=true;game.settings.hints=true;
            yield return null;yield return null;
            if(coaching)
            {
                Size(1280,720);game.settings.textScale=1.3f;prep.BeginCoachingChoice();
                yield return null;yield return null;yield return new WaitForSecondsRealtime(.5f);
                ScreenCapture.CaptureScreenshot(folder+"choice-720.png");yield return new WaitForSecondsRealtime(.5f);
                File.WriteAllText(folder+"coaching-checks.txt",StormSim.Preparedness.Editor.PreparationCoachingChecks.Run(prep));
                prep.SetPhase(PrepPhase.Preparing);prep.SetCoaching(false);
                yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(folder+"explore-720.png");yield return new WaitForSecondsRealtime(.5f);
                prep.SetCoaching(true);player.ToggleGuide();
                yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(folder+"coaching-guide-720.png");yield return new WaitForSecondsRealtime(.5f);
                player.CloseDesktopPanels();
            }
            if(remaining)
            {
                File.WriteAllText(folder+"remaining-checks.txt",StormSim.Preparedness.Editor.PreparationRemainingChecks.Run(prep,folder));
                File.WriteAllText(folder+"experience-checks.txt",StormSim.Preparedness.Editor.PreparationExperienceChecks.Run(prep));
                // Exercise the compact Figma-style item card at the most constrained
                // supported desktop layout and enlarged accessibility text setting.
                Size(1280,720);game.settings.textScale=1.3f;
                var inspectionTarget=prep.items.First(i=>i&&i.kind==PrepKind.Neighbor&&i.gameObject.activeInHierarchy);
                var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
                var oldViewRotation=player.view.transform.localRotation;
                float oldPitch=(float)typeof(PreparednessPlayer).GetField("pitch",flags).GetValue(player);
                var targetCenter=inspectionTarget.WorldBounds().center;
                var approach=Vector3.ProjectOnPlane(inspectionTarget.transform.forward,Vector3.up).normalized;
                if(approach.sqrMagnitude<.1f)approach=Vector3.forward;
                var testPosition=targetCenter+approach*1.45f;testPosition.y=oldPosition.y;
                var look=Vector3.ProjectOnPlane(targetCenter-testPosition,Vector3.up).normalized;
                player.Warp(testPosition,Quaternion.LookRotation(look,Vector3.up));
                var localDirection=player.transform.InverseTransformDirection((targetCenter-player.view.transform.position).normalized);
                float testPitch=Quaternion.LookRotation(localDirection,Vector3.up).eulerAngles.x;if(testPitch>180)testPitch-=360;
                typeof(PreparednessPlayer).GetField("pitch",flags).SetValue(player,testPitch);
                player.view.transform.localRotation=Quaternion.Euler(testPitch,0,0);Physics.SyncTransforms();player.RefreshFocus();
                Check(player.focused==inspectionTarget,"Crosshair resolves the individual item instead of an overlapping zone");
                player.ToggleItemInfo();yield return null;yield return null;
                var ui=StormSim.UI.StormglassUI.Active;var uiType=typeof(StormSim.UI.StormglassUI);
                var infoPanel=(RectTransform)uiType.GetField("itemInfo",flags).GetValue(ui);
                var infoText=(TMPro.TMP_Text)uiType.GetField("itemInfoText",flags).GetValue(ui);
                var infoToggleText=(TMPro.TMP_Text)uiType.GetField("itemInfoToggleText",flags).GetValue(ui);
                var promptPanel=(RectTransform)uiType.GetField("prompt",flags).GetValue(ui);
                var hudRoot=(RectTransform)uiType.GetField("hud",flags).GetValue(ui);
                float preferredInfo=infoText.GetPreferredValues(infoText.text,infoText.rectTransform.rect.width,1000).y;
                Check(infoPanel.gameObject.activeSelf&&preferredInfo<=infoText.rectTransform.rect.height+1,"Expanded item information remains inside its panel at 1280x720 and 130 percent text (preferred "+preferredInfo.ToString("0.0")+", available "+infoText.rectTransform.rect.height.ToString("0.0")+")");
                Check(infoToggleText.GetPreferredValues(infoToggleText.text,infoToggleText.rectTransform.rect.width,1000).y<=infoToggleText.rectTransform.rect.height+1,"The R item-info control remains readable inside its pill");
                var infoBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hudRoot,infoPanel);
                var promptBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hudRoot,promptPanel);
                Check(!infoBounds.Intersects(promptBounds),"Item information and action prompt use separate non-overlapping panels");
                ScreenCapture.CaptureScreenshot(folder+"item-info-720.png");yield return new WaitForSecondsRealtime(.5f);
                player.ToggleItemInfo();player.Warp(oldPosition,oldRotation);player.view.transform.localRotation=oldViewRotation;
                typeof(PreparednessPlayer).GetField("pitch",flags).SetValue(player,oldPitch);Physics.SyncTransforms();
                var beforePanels=prep.Capture();prep.SetPhase(PrepPhase.Preparing);prep.SetCoaching(true);
                prep.Bag.storageDoor=null;player.GrabBag(prep.Bag);prep.Bag.closed=false;
                foreach(var kind in new[]{PrepKind.Documents,PrepKind.Phone,PrepKind.Radio}){var item=prep.items.First(i=>i.kind==kind);item.storedIn="bag";item.gameObject.SetActive(false);}
                player.ToggleBagPanel();yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(folder+"selected-bag-720.png");yield return new WaitForSecondsRealtime(.5f);
                player.CloseDesktopPanels();player.OpenDepartureReview(true);yield return null;yield return null;
                ScreenCapture.CaptureScreenshot(folder+"departure-review-720.png");yield return new WaitForSecondsRealtime(.5f);
                player.CloseDesktopPanels();prep.Restore(beforePanels);
            }
            File.WriteAllText(folder+"regression-checks.txt",StormSim.Preparedness.Editor.PreparationBatch1Checks.Run());
            prep.SetPhase(PrepPhase.Preparing);player.CloseDesktopPanels();
            Size(1920,1080);game.settings.textScale=1;player.ToggleGuide();
            yield return null;yield return null;yield return new WaitForSecondsRealtime(.5f);
            Check(Screen.width==1920&&Screen.height==1080,"Guide runs at 1920x1080");
            ScreenCapture.CaptureScreenshot(folder+"guide-1080.png");yield return new WaitForSecondsRealtime(.5f);
            Size(1280,720);game.settings.textScale=1.3f;
            ((bool[])typeof(PreparednessPlayer).GetField("guideGroups",flags).GetValue(player))[1]=true;
            yield return null;yield return null;yield return new WaitForSecondsRealtime(.5f);
            Check(Screen.width==1280&&Screen.height==720,"Expanded guide runs at 1280x720 with 130 percent text");
            ScreenCapture.CaptureScreenshot(folder+"guide-720-expanded.png");yield return new WaitForSecondsRealtime(.5f);
            typeof(PreparednessPlayer).GetField("journalScroll",flags).SetValue(player,new Vector2(0,10000));
            yield return null;yield return null;
            ScreenCapture.CaptureScreenshot(folder+"guide-720-bottom.png");yield return new WaitForSecondsRealtime(.5f);
            player.CloseDesktopPanels();game.settings.textScale=1;Size(1920,1080);
            prep.Bag.storageDoor=null;player.GrabBag(prep.Bag);
            var documents=prep.items.First(i=>i.kind==PrepKind.Documents);documents.storageDoor=null;
            player.Grab(documents);player.focused=null;
            if(!documents.closed)player.UseFocused();
            if(prep.Bag.closed)player.UseFocused();
            yield return new WaitForSecondsRealtime(.5f);
            player.focused=null;player.UseFocused();yield return new WaitForSecondsRealtime(.7f);
            Check(documents.IsPacked,"Packing animation stores the checked pouch across live frames");
            PreparednessItem travelWater=null;
            if(remaining)
            {
                travelWater=prep.items.Single(i=>i.id=="travel-water");travelWater.storageDoor=null;player.Grab(travelWater);player.focused=null;player.UseFocused();
                yield return new WaitForSecondsRealtime(.7f);
                Check(travelWater.IsPacked,"Portable drinking water packs across live frames");
            }
            ScreenCapture.CaptureScreenshot(folder+"preparation-hud-1080.png");yield return new WaitForSecondsRealtime(.5f);
            prep.Use(prep.items.First(i=>i.kind==PrepKind.Advisory));prep.Finish(true);
            yield return new WaitForSecondsRealtime(3.5f);
            Check(!bridge.Active&&game.Mode==SibucaoMode.Playing,"Natural fade hands early departure to evacuation");
            Check(game.inventory.HasBag,"The carried go-bag stays equipped through handoff");
            var physical=documents.GetComponent<PhysicalGrabbable>();
            Check(game.state.physical.inventorySlots.Contains(physical.persistentId),"Packed document pouch remains in storm inventory");
            if(remaining)Check(game.state.physical.inventorySlots.Contains(travelWater.GetComponent<PhysicalGrabbable>().persistentId),"Portable drinking water stays in the bag through evacuation handoff");
            Check(game.state.homeSeconds<SibucaoStormTimeline.FloodStart(game.home),"Early departure retains time before flood rise");
            Check(game.state.physical.flags.Contains("preparation_guidance_used"),"Handoff records that guidance was shown");
            Check(!game.home.MovementLocked,"Departure does not restart the wake-up sequence");
            ScreenCapture.CaptureScreenshot(folder+"early-departure-1080.png");yield return new WaitForSecondsRealtime(.5f);
        }
        IEnumerator Guard()
        {
            var routine=Cases();Exception error=null;
            try
            {
                while(true)
                {
                    bool more=false;object current=null;
                    try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception ex){error=ex;break;}
                    if(!more)break;yield return current;
                }
            }
            finally
            {
                sizeIndex.SetValue(view,oldSize);view.Repaint();game.settings.hints=oldHints;game.settings.textScale=oldText;Application.runInBackground=oldBackground;
                if(hadSave)File.WriteAllBytes(prep.SavePath,save);else if(File.Exists(prep.SavePath))File.Delete(prep.SavePath);
                File.WriteAllText(folder+"status.txt",error==null?"PASS: inspect captured images. Human acceptance remains pending.":"FAIL: "+error);
                if(error!=null)Debug.LogException(error);
            }
        }
        File.WriteAllText(folder+"status.txt","RUNNING");game.StartCoroutine(Guard());
        Debug.Log("Batch 1 live regression and captures started.");
    }
}
