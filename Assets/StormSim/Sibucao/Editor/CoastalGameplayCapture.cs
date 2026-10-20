using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using StormSim.Sibucao;
using StormSim.UI;

namespace StormSim.Editor
{
    [InitializeOnLoad]
    public static class CoastalGameplayCapture
    {
        const string Key="CoastalGameplayCapture.Scene";
        static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly List<string> Report=new List<string>();
        static string folder;
        static CoastalGameplayCapture(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Baybayon(){Begin("BAYBAYON_RAIN");}
        public static void Riverside(){Begin("RIVERSIDE_RAIN");}
        public static void FullBaybayon(){SessionState.SetBool(Key+".Full",true);Begin("BAYBAYON_RAIN");}
        public static void FullRiverside(){SessionState.SetBool(Key+".Full",true);Begin("RIVERSIDE_RAIN");}
        public static void LightRiverside(){SessionState.SetBool(Key+".Light",true);FullRiverside();}
        public static void RiversidePreparationRecheck(){SessionState.SetBool(Key+".PreparationOnly",true);LightRiverside();}
        public static void FullMenu(){SessionState.SetBool(Key+".Full",true);Begin("STORMSIM_MainMenu");}
        static void Begin(string scene)
        {
            SessionState.SetString(Key,scene);
            EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
            EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||string.IsNullOrEmpty(SessionState.GetString(Key,"")))return;
            EditorApplication.delayCall+=()=>{
                if(SessionState.GetString(Key,"")=="STORMSIM_MainMenu")
                {var menu=UnityEngine.Object.FindAnyObjectByType<StormSimMainMenuScene>();menu.StartCoroutine(RunMenu(menu));return;}
                var s=SibucaoSession.Instance;
                if(!s){Debug.LogError("Capture session missing");EditorApplication.Exit(1);return;}
                s.StartCoroutine(Run(s));
            };
        }
        static IEnumerator Run(SibucaoSession s)
        {
            bool full=SessionState.GetBool(Key+".Full",false);
            folder=(full?"Captures/UICompletion/":"Captures/CoastalSignal/Updated/")+SessionState.GetString(Key,"")+"/";
            if(SessionState.GetBool(Key+".PreparationOnly",false))folder+="PreparationRecheck/";
            Directory.CreateDirectory(folder);
            Report.Clear();Report.Add("Play Mode capture, initialized run; controlled warning/subtitle/vitals state. Not a completed participant run.");
            Application.runInBackground=true;
            if(SessionState.GetBool(Key+".Light",false))
            {
                Report.Add("Reduced-detail fixture: runtime texture mip limit 2, shadows disabled, MSAA disabled. Production assets/settings are not saved.");
                SessionState.SetInt(Key+".Mips",QualitySettings.globalTextureMipmapLimit);SessionState.SetInt(Key+".Shadows",(int)QualitySettings.shadows);SessionState.SetInt(Key+".MSAA",QualitySettings.antiAliasing);
                QualitySettings.globalTextureMipmapLimit=2;QualitySettings.shadows=ShadowQuality.Disable;QualitySettings.antiAliasing=0;
                yield return Resources.UnloadUnusedAssets();GC.Collect();
            }
            typeof(SibucaoSession).GetField("savePath",Flags).SetValue(s,Path.GetFullPath(folder+"test-save.json"));
            s.UseTestSaveDirectory(Path.GetFullPath(folder));
            s.settings.textScale=1.3f;s.settings.hints=true;s.settings.subtitles=true;
            int home=PlayerPrefs.GetInt("SibucaoLastHome",-1),spawn=PlayerPrefs.GetInt("SibucaoLastSpawn",-1);
            s.StartRun();
            yield return new WaitForSecondsRealtime(2);
            var bridge=s.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();
            if(full&&bridge&&bridge.Active)
            {
                bridge.preparation.GetComponent<StormSim.Preparedness.PreparationIntro>()?.End();
                foreach(float scale in new[]{1f,1.3f})
                {
                    s.settings.textScale=scale;
                    foreach(string key in new[]{"Preparation bag","Preparation guide","Departure","Phone"})
                    {BuildScreen(key);Capture("prep-"+key.Replace(" ","-")+"-"+scale.ToString("0.0"),s);yield return null;}
                    var player=bridge.preparation.player;
                    StormSim.Preparedness.PreparednessItem longest=null;
                    foreach(var item in bridge.preparation.items)
                        if(item&&!item.IsZone&&(!longest||player.ItemInformation(item).Length>player.ItemInformation(longest).Length))longest=item;
                    player.focused=longest;
                    typeof(StormSim.Preparedness.PreparednessPlayer).GetField("itemInfoTarget",Flags).SetValue(player,longest);
                    typeof(StormSim.Preparedness.PreparednessPlayer).GetField("itemInfoOpen",Flags).SetValue(player,true);
                    BuildScreen("HUD");
                    typeof(StormglassUI).GetMethod("UpdateHud",Flags).Invoke(StormglassUI.Active,null);
                    typeof(StormglassUI).GetMethod("UpdatePreparationHud",Flags).Invoke(StormglassUI.Active,null);
                    typeof(StormglassUI).GetMethod("OfferNotice",Flags).Invoke(StormglassUI.Active,new object[]{"Preparation updated","Your household preparation has been recorded.",20});
                    Capture("prep-details-notice-"+scale.ToString("0.0"),s);
                    yield return null;
                }
            }
            if(SessionState.GetBool(Key+".PreparationOnly",false))
            {
                File.WriteAllLines(folder+"inspection.txt",Report);
                File.WriteAllText(folder+"finished.txt","Preparation capture completed");
                QualitySettings.globalTextureMipmapLimit=SessionState.GetInt(Key+".Mips",0);
                QualitySettings.shadows=(ShadowQuality)SessionState.GetInt(Key+".Shadows",2);
                QualitySettings.antiAliasing=SessionState.GetInt(Key+".MSAA",0);
                SessionState.EraseBool(Key+".PreparationOnly");SessionState.EraseBool(Key+".Light");SessionState.EraseBool(Key+".Full");SessionState.EraseString(Key);
                EditorApplication.Exit(0);yield break;
            }
            if(bridge&&bridge.Active)bridge.SkipToDuringForEditorTesting();
            if(s.home)s.home.Suspend();
            if(s.physicalDecisions)s.physicalDecisions.EnsureNodeCapacity();
            PlayerPrefs.SetInt("SibucaoLastHome",home);PlayerPrefs.SetInt("SibucaoLastSpawn",spawn);PlayerPrefs.Save();
            s.player.Teleport(s.route.spawnPoints[0],s.route.Sample(s.route.spawnDistances[0]+8)-s.route.Sample(s.route.spawnDistances[0]));
            s.SetMode(SibucaoMode.Playing);
            yield return new WaitForSecondsRealtime(4);
            var flood=s.GetComponent<FloodSimulationManager>();
            float deadline=Time.realtimeSinceStartup+120;
            while(flood&&flood.IsCatchingUp&&Time.realtimeSinceStartup<deadline)yield return null;
            Report.Add("Flood catch-up at capture: "+(flood&&flood.IsCatchingUp));
            s.state.physical.health=72;s.state.physical.stamina=40;
            s.Say("Stay together. Bring your emergency bag and follow the marked route to the school.",40);
            s.Notify("WATER RISING","Keep to higher ground and follow the marked evacuation route.");
            yield return null;
            yield return null;
            Capture("gameplay-large-text",s);
            s.SetMode(SibucaoMode.Paused);
            yield return null;yield return null;
            Capture("pause-large-text",s);
            if(full)yield return Matrix(s);
            File.WriteAllLines(folder+"inspection.txt",Report);
            File.WriteAllText(folder+"finished.txt","Capture completed");
            if(full)
            {
                EditorApplication.delayCall+=()=>{
                    UnityEngine.SceneManagement.SceneManager.LoadScene("STORMSIM_MainMenu");
                    EditorApplication.delayCall+=()=>{
                        var menu=UnityEngine.Object.FindAnyObjectByType<StormSimMainMenuScene>();
                        menu.StartCoroutine(RunMenu(menu));
                    };
                };
                yield break;
            }
            SessionState.EraseString(Key);
            SessionState.EraseBool(Key+".Full");
            SessionState.EraseBool(Key+".Light");
            EditorApplication.Exit(0);
        }
        static void BuildScreen(string key)
        {
            var ui=StormglassUI.Active;
            typeof(StormglassUI).GetField("screenKey",Flags).SetValue(ui,key);
            typeof(StormglassUI).GetMethod("Build",Flags).Invoke(ui,null);
        }
        static IEnumerator Matrix(SibucaoSession s)
        {
            Report.Add(StormglassChecks.CheckPointer());
            StormSim.Sibucao.Editor.SibucaoExperienceChecks.Run();
            Report.Add("PASS: evacuation experience persistence checks");
            s.SetMode(SibucaoMode.Paused);yield return null;
            BuildScreen("Paused");
            foreach(var button in StormglassUI.Active.Buttons)
                if(button.name=="Action • Resume"){button.onClick.Invoke();break;}
            Report.Add((s.Mode==SibucaoMode.Playing&&Time.timeScale==1?"PASS":"FAIL")+": Resume callback restores gameplay and time");
            yield return null;s.OpenSettings();yield return null;
            Report.Add((s.Mode==SibucaoMode.Settings&&Time.timeScale==0?"PASS":"FAIL")+": settings pauses simulation");
            bool subtitles=s.settings.subtitles;
            BuildScreen("Settings");
            foreach(var button in StormglassUI.Active.Buttons)
                if(button.name.StartsWith("Action • Subtitles:")){button.onClick.Invoke();break;}
            yield return null;
            var selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Report.Add((selected&&selected.name.StartsWith("Action • Subtitles:")?"PASS":"FAIL")+": changing a toggle retains navigation focus");
            s.settings.subtitles=subtitles;
            s.Confirm("menu");yield return null;BuildScreen("Confirm");
            foreach(var button in StormglassUI.Active.Buttons)
                if(button.name=="Action • Cancel"){button.onClick.Invoke();break;}
            Report.Add((s.Mode==SibucaoMode.Settings?"PASS":"FAIL")+": confirmation cancellation restores previous screen");
            s.SetMode(SibucaoMode.Paused);yield return null;
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080)})
            {
                Screen.SetResolution(resolution.x,resolution.y,false);yield return null;yield return null;
                foreach(float scale in new[]{1f,1.3f})
                {
                    s.settings.textScale=scale;
                    string suffix="-"+resolution.x+"x"+resolution.y+"-"+scale.ToString("0.0");
                    foreach(string key in new[]{"Paused","Settings","Bag","Guide","Recovery","ResumeSelection","Confirm","ArrivalReview","Complete"})
                    {
                        int pages=key=="Complete"?5:key=="Settings"?3:1;
                        for(int page=0;page<pages;page++)
                        {
                            typeof(StormglassUI).GetField(key=="Settings"?"settingsTab":"reviewTab",Flags).SetValue(StormglassUI.Active,page);
                            BuildScreen(key);Capture(key+"-"+page+suffix,s,resolution.x,resolution.y);
                            var scroll=typeof(StormglassUI).GetField("scroll",Flags).GetValue(StormglassUI.Active) as ScrollRect;
                            if(scroll.content.rect.height>scroll.viewport.rect.height+4)
                            {
                                UnityEngine.UI.Button last=null;
                                foreach(var button in StormglassUI.Active.Buttons)if(button.transform.IsChildOf(scroll.content)&&button.IsInteractable())last=button;
                                if(last)
                                {
                                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(last.gameObject);
                                    typeof(StormglassUI).GetMethod("UpdateScrollControls",Flags).Invoke(StormglassUI.Active,null);
                                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,last.transform);
                                    Report.Add((bounds.min.y>=scroll.viewport.rect.yMin-2&&bounds.max.y<=scroll.viewport.rect.yMax+2?"PASS":"FAIL")+": focused final action visible "+key+"-"+page+suffix);
                                }
                                scroll.verticalNormalizedPosition=0;Capture(key+"-"+page+suffix+"-bottom",s,resolution.x,resolution.y);
                            }
                            yield return null;
                        }
                    }
                    s.SetMode(SibucaoMode.Playing);yield return null;yield return null;
                    Capture("HUD"+suffix,s,resolution.x,resolution.y);
                    var choices=s.missionGuide?s.missionGuide.RouteChoices:null;
                    if(choices&&choices.Current)
                    {
                        s.player.Teleport(choices.Current.Approach,choices.Current.station.transform.forward);
                        yield return null;yield return null;
                        if(choices.Previewing)Capture("route-choice"+suffix,s,resolution.x,resolution.y);
                        else Report.Add("UNTESTED route preview could not be activated"+suffix);
                    }
                    s.SetMode(SibucaoMode.Paused);
                }
                File.WriteAllLines(folder+"inspection.txt",Report);
            }
            // Exercise the real save/load path using only this fixture's isolated directory.
            float savedHealth=s.state.physical.health;
            s.SaveRun();
            bool exists=s.SlotExists(false,false);
            s.state.physical.health=Mathf.Max(1,savedHealth-17);
            bool restored=exists&&s.LoadSlot(false,false);
            Report.Add((restored&&Mathf.Approximately(s.state.physical.health,savedHealth)?"PASS":"FAIL")+": isolated manual save restores recorded health and run state");
            File.WriteAllLines(folder+"inspection.txt",Report);
        }
        static IEnumerator RunMenu(StormSimMainMenuScene menu)
        {
            Screen.SetResolution(1280,720,false);yield return null;yield return null;
            folder="Captures/UICompletion/MainMenu/";Directory.CreateDirectory(folder);Report.Clear();
            Report.Add("Actual main-menu scene; pages opened by fixture, not a full user journey.");
            var type=typeof(StormSimMainMenuScene);var pageField=type.GetField("page",Flags);
            var settings=(SibucaoSettings)type.GetField("settings",Flags).GetValue(menu);
            var canvas=(Canvas)type.GetField("canvas",Flags).GetValue(menu);
            var camera=(Camera)type.GetField("eye",Flags).GetValue(menu);
            foreach(float scale in new[]{1f,1.3f})
            {
                settings.textScale=scale;
                foreach(string page in Enum.GetNames(pageField.FieldType))
                {
                    for(int step=0;step<(page=="Onboarding"?3:1);step++)
                    {
                        pageField.SetValue(menu,Enum.Parse(pageField.FieldType,page));type.GetField("onboardingStep",Flags).SetValue(menu,step);
                        type.GetMethod("Build",Flags).Invoke(menu,null);yield return null;
                        var authored=UnityEngine.Object.FindAnyObjectByType<StormSimMainMenu>();
                        Render("menu-"+page+"-"+step+"-"+scale.ToString("0.0"),page=="Home"&&authored?authored.canvas:canvas,camera,1280,720);
                        if(page!="Home")
                        {
                            var scroll=(ScrollRect)type.GetField("readingScroll",Flags).GetValue(menu);
                            if(scroll.content.rect.height>scroll.viewport.rect.height+4)
                            {scroll.verticalNormalizedPosition=0;Render("menu-"+page+"-"+step+"-"+scale.ToString("0.0")+"-bottom",canvas,camera,1280,720);}
                        }
                    }
                }
            }
            Report.Add(StormglassChecks.CheckPointer());File.WriteAllLines(folder+"inspection.txt",Report);File.WriteAllText(folder+"finished.txt","Capture completed");
            if(SessionState.GetBool(Key+".Light",false))
            {QualitySettings.globalTextureMipmapLimit=SessionState.GetInt(Key+".Mips",0);QualitySettings.shadows=(ShadowQuality)SessionState.GetInt(Key+".Shadows",2);QualitySettings.antiAliasing=SessionState.GetInt(Key+".MSAA",0);}
            SessionState.EraseString(Key);SessionState.EraseBool(Key+".Full");SessionState.EraseBool(Key+".Light");EditorApplication.Exit(0);
        }
        static void Capture(string name,SibucaoSession s,int width=1280,int height=720)
        {
            var ui=StormglassUI.Active;var canvas=ui.Canvas;var camera=ui.Eye;
            Render(name,canvas,camera,width,height);
        }
        static void Render(string name,Canvas canvas,Camera camera,int width,int height)
        {
            var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;
            var previousTarget=camera.targetTexture;
            var target=new RenderTexture(width,height,24);
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(folder+name+".png",image.EncodeToPNG());
            int count=0;
            foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())if(text.isTextOverflowing){Report.Add(name+" OVERFLOW: "+text.text);count++;}
            Report.Add(name+": text overflow="+count);
            canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;camera.targetTexture=previousTarget;RenderTexture.active=null;
            target.Release();
            UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
        }
    }
}
