using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using StormSim.UI;

namespace StormSim.Sibucao
{
    // Explicit unattended startup check. No gameplay controls or save mutations are enabled.
    public sealed class SibucaoDesktopSmokeChecks : MonoBehaviour
    {
        [Serializable] class DeveloperSettings { public bool enabled; public string serverAddress,accessToken,witClientAccessToken; }
        readonly List<string> checks=new List<string>();
        int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stormsim-startup-check")<0)return;
            var go=new GameObject("Desktop startup verification");DontDestroyOnLoad(go);go.AddComponent<SibucaoDesktopSmokeChecks>();
        }
        void Awake(){Application.logMessageReceived+=Log;}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);}
        bool MenuAction(string prefix)
        {
            IReadOnlyList<UnityEngine.UI.Button> menuButtons=null;
            var dedicated=FindAnyObjectByType<StormSimMainMenuScene>();
            if(dedicated)menuButtons=dedicated.Buttons;
            else if(StormglassUI.InUse)menuButtons=StormglassUI.Active.Buttons;
            if(menuButtons==null)return false;
            foreach(var button in menuButtons)
            {
                var text=button?button.GetComponentInChildren<TMPro.TMP_Text>():null;
                if(text&&text.text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return true;
            }
            return false;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-stormsim-report");
            string folder=i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Application.persistentDataPath,"startup-validation");Directory.CreateDirectory(folder);
            float until=Time.realtimeSinceStartup+180;
            while(!SibucaoSession.Instance&&!FindAnyObjectByType<StormSimMainMenuScene>()&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(10);
            var s=SibucaoSession.Instance;
            var dedicated=FindAnyObjectByType<StormSimMainMenuScene>();
            Check(dedicated||(s&&s.Mode==SibucaoMode.Menu),"Desktop opens at the main menu");
            Check(dedicated&&dedicated.PageKey=="Home"||StormglassUI.InUse&&StormglassUI.Active.ScreenKey=="Menu","Stormglass is the active shipped main menu");
            Check(MenuAction("Start")&&MenuAction("Continue")&&MenuAction("Choose training location")&&MenuAction("Comfort & settings")&&MenuAction("About & credits")&&MenuAction("Exit game"),"Main menu exposes every desktop action");
            bool menuLayout=true;IReadOnlyList<UnityEngine.UI.Button> buttons=dedicated?dedicated.Buttons:StormglassUI.InUse?StormglassUI.Active.Buttons:null;if(buttons!=null)foreach(var button in buttons){var text=button?button.GetComponentInChildren<TMPro.TMP_Text>():null;if(text&&text.isTextOverflowing)menuLayout=false;}
            Check(menuLayout,"Main menu button text does not overflow");
            Check(!Debug.isDebugBuild,"Candidate is a non-development player");
            Check(!SibucaoDevelopmentControls.Enabled,"Development controls remain unavailable");
            Check(!UnityEngine.XR.XRSettings.isDeviceActive,"Desktop starts without an active XR device");
            var asset=Resources.Load<ScriptableObject>("DevAgentSettings");
            var settings=asset?JsonUtility.FromJson<DeveloperSettings>(JsonUtility.ToJson(asset)):new DeveloperSettings();
            Check(!settings.enabled&&string.IsNullOrEmpty(settings.serverAddress)&&string.IsNullOrEmpty(settings.accessToken)&&string.IsNullOrEmpty(settings.witClientAccessToken),"No developer assistant connection settings in the player");
            Check(errors==0,"Startup logged no errors, exceptions or assertions");
            Check(Screen.width==1920&&Screen.height==1080,"Requested 1920 x 1080 desktop viewport");
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"release-menu.png"));yield return null;yield return null;
            File.WriteAllLines(Path.Combine(folder,"startup-checks.txt"),checks);
            File.WriteAllText(Path.Combine(folder,"finished.txt"),"Finished; inspect assertions, screenshot and Player.log.");
            Application.Quit();
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
