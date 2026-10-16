using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed class StormSurvivalVitals : MonoBehaviour
    {
        SibucaoSession S=>GetComponent<SibucaoSession>();
        SibucaoPreparationBridge Bridge=>GetComponent<SibucaoPreparationBridge>();
        Vector3 previous;bool tracking;
        public static float StaminaRate(bool moving,bool running,bool swimming=false,float depth=0,bool carrying=false,float hunger=0)
        {
            float rate=swimming?(moving?(running?-4:-2):-.25f):running&&moving?-4:moving&&depth>=.35f?-.8f:moving?1.5f:6;
            if(moving&&carrying)rate-=1.5f;
            return rate>0?rate*Mathf.Lerp(1,.5f,Mathf.InverseLerp(60,100,hunger)):rate;
        }
        public const float BreathSeconds=40;
        public static void StepVitals(PhysicalRunData data,float dt,bool moving,bool running,bool swimming,bool underwater,float depth,bool carrying)
        {
            data.stamina=Mathf.Clamp(data.stamina+dt*StaminaRate(moving,running,swimming,depth,carrying,data.hunger),0,100);
            data.hunger=Mathf.Clamp(data.hunger+dt*(moving&&running?.07f:.05f),0,100);
            data.submergedSeconds=underwater?data.submergedSeconds+dt:Mathf.Max(0,data.submergedSeconds-dt*4);
            if(underwater&&data.submergedSeconds>BreathSeconds)data.health=Mathf.Max(0,data.health-dt*10);
            if(data.health<=0)data.needsRescue=true;
        }
        UnityEngine.UI.Text vrText;UnityEngine.UI.Image[] vrBars;
        void LateUpdate()
        {
            if(StormSim.UI.StormglassUI.InUse){if(vrText)vrText.transform.parent.gameObject.SetActive(false);return;}
            if(!UnityEngine.XR.XRSettings.isDeviceActive){if(vrText)vrText.transform.parent.gameObject.SetActive(false);return;}
            bool prep=S.Mode==SibucaoMode.Preparation;
            if(!prep&&S.Mode!=SibucaoMode.Playing){if(vrText)vrText.transform.parent.gameObject.SetActive(false);return;}
            var view=prep?Bridge.preparation.player.view.transform:S.player.view.transform;
            if(!vrText)
            {
                var root=new GameObject("Temporary VR survival display",typeof(RectTransform),typeof(Canvas));
                root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(280,140);
                var text=new GameObject("Health stamina hunger labels",typeof(RectTransform),typeof(UnityEngine.UI.Text));text.transform.SetParent(root.transform,false);
                vrText=text.GetComponent<UnityEngine.UI.Text>();vrText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");vrText.fontSize=18;vrText.color=Color.white;
                vrText.rectTransform.sizeDelta=new Vector2(280,140);vrBars=new UnityEngine.UI.Image[3];
                for(int n=0;n<3;n++){var bar=new GameObject("Vital bar "+n,typeof(RectTransform),typeof(UnityEngine.UI.Image));bar.transform.SetParent(root.transform,false);vrBars[n]=bar.GetComponent<UnityEngine.UI.Image>();vrBars[n].color=n==0?Color.red:n==1?Color.green:Color.yellow;vrBars[n].rectTransform.pivot=new Vector2(0,.5f);vrBars[n].rectTransform.anchoredPosition=new Vector2(-140,43-n*42);}
            }
            var panel=vrText.transform.parent;panel.SetParent(view,false);panel.localPosition=new Vector3(.24f,-.31f,1);panel.localRotation=Quaternion.identity;panel.localScale=Vector3.one*.0015f;panel.gameObject.SetActive(true);
            var d=S.state.physical;vrText.text="Health  "+d.health.ToString("0")+" / 100\n\nStamina  "+d.stamina.ToString("0")+" / 100\n\nHunger  "+d.hunger.ToString("0")+" / 100";
            float[] values={d.health,d.stamina,d.hunger};for(int i=0;i<3;i++)vrBars[i].rectTransform.sizeDelta=new Vector2(270*Mathf.Clamp01(values[i]/100),5);
        }
        void Update()
        {
            bool prep=S.Mode==SibucaoMode.Preparation&&Bridge&&Bridge.preparation&&Bridge.preparation.CanPrepare&&!Bridge.preparation.player.ReadingPanel;
            var flood=S.GetComponent<FloodSimulationManager>();
            bool storm=S.Mode==SibucaoMode.Playing&&(!S.home||!S.home.MovementLocked)&&(!flood||!flood.IsCatchingUp);
            if(!prep&&!storm){tracking=false;return;}
            var player=prep?Bridge.preparation.player.transform:S.player.transform;
            var movement=player.position-previous;movement.y=0;
            bool moving=tracking&&movement.magnitude>Time.deltaTime*.04f&&movement.sqrMagnitude<4;
            if(storm)moving&=!S.player.vehicle&&S.player.MoveInput.sqrMagnitude>.01f&&(!S.inventory||!S.inventory.IsOpen)&&(!S.missionGuide||!S.missionGuide.BlocksInput);
            bool running=prep?Bridge.preparation.player.Running:S.player.Sprinting;
            previous=player.position;tracking=true;
            var d=S.state.physical;
            bool swimming=storm&&S.player.swimming&&S.player.swimming.IsSwimming;
            bool underwater=storm&&flood&&flood.TrySurface(S.player.transform.position,out var height,out _)&&height>S.player.view.transform.position.y+.04f;
            if(underwater&&(S.inventory&&S.inventory.IsOpen||S.missionGuide&&S.missionGuide.IsOpen))
            {
                if(S.inventory&&S.inventory.IsOpen)S.inventory.Close();
                if(S.missionGuide&&S.missionGuide.IsOpen)S.missionGuide.Close();
                S.Notify("Surface first","Bag and guide close underwater. Press Space to rise.");
            }
            bool carrying=storm&&S.player.grabber&&S.player.grabber.held&&S.player.grabber.MovementScale<.9f;
            StepVitals(d,Time.deltaTime,moving,running,swimming,underwater,storm?S.player.WaterDepthAtFeet:0,carrying);
            if(storm&&d.needsRescue)S.OpenRecovery();
        }
        void OnGUI()
        {
            if(StormSim.UI.StormglassUI.InUse)return;
            if(!S||UnityEngine.XR.XRSettings.isDeviceActive||(S.Mode!=SibucaoMode.Preparation&&S.Mode!=SibucaoMode.Playing))return;
            if(S.Mode==SibucaoMode.Playing&&((S.inventory&&S.inventory.IsOpen)||(S.missionGuide&&S.missionGuide.IsOpen)))return;
            if(S.Mode==SibucaoMode.Preparation&&Bridge&&Bridge.preparation&&Bridge.preparation.player.DesktopPanelOpen)return;
            var layout=SibucaoHudLayout.Current;var previous=SibucaoDesktopHud.Begin(layout);var d=S.state.physical;
            SibucaoDesktopHud.Vitals(layout,d.health,d.stamina,d.hunger);GUI.matrix=previous;
        }

        static void Bar(float x,float y,string label,float value,Color color)
        {
            var old=GUI.color;GUI.color=Color.white;GUI.Label(new Rect(x,y,215,22),label+"  "+Mathf.RoundToInt(value)+" / 100");
            GUI.color=new Color(.1f,.15f,.17f);GUI.DrawTexture(new Rect(x,y+24,214,7),Texture2D.whiteTexture);
            GUI.color=color;GUI.DrawTexture(new Rect(x,y+24,214*Mathf.Clamp01(value/100),7),Texture2D.whiteTexture);GUI.color=old;
        }
    }
}
