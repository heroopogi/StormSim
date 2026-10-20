using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoUI
    {
        Vector2 reviewScroll,settingsScroll;
        int reviewTab;static int rebind=-1;
        public static bool BindingCapture=>rebind>=0;
        public static void CancelBinding(){rebind=-1;}
        void ReleaseResults()
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.72f));Panel(new Rect(48,30,1344,838));
            Label(new Rect(80,55,1250,58),"Your evacuation review",h1);
            int seconds=Mathf.RoundToInt(S.state.homeSeconds),total=0;foreach(int value in S.state.scores)total+=value;
            Label(new Rect(80,119,1260,44),$"Difficulty {S.state.difficultyLevel} · {seconds/60:00}:{seconds%60:00} · {total:N0} / 5,000",h3);
            for(int i=0;i<5;i++)
            {
                float y=211+i*89;var category=(ScoreCategory)i;
                Label(new Rect(80,y,362,34),SibucaoDebrief.Categories[i],body);
                Label(new Rect(80,y+32,360,28),$"{S.state.scores[i]} / {SibucaoDecisionEvaluation.Maximum(category)}",small);
                SignalStationUI.Bar(new Rect(80,y+67,342,8),SibucaoDecisionEvaluation.Fraction(category,S.state.scores[i]));
            }
            Label(new Rect(80,680,345,95),"Category caps are not guaranteed points. Your recorded totals include secondary credit.",small);
            if(Button(new Rect(464,184,420,49),"Actions & outcomes",reviewTab==0)){reviewTab=0;reviewScroll=Vector2.zero;}
            if(Button(new Rect(898,184,420,49),"Residents, supplies & learning",reviewTab==1)){reviewTab=1;reviewScroll=Vector2.zero;}
            string content=reviewTab==0?DecisionReview():SibucaoDebrief.Summary(S);
            float height=body.CalcHeight(new GUIContent(content),816)+32;
            reviewScroll=GUI.BeginScrollView(new Rect(464,252,868,505),reviewScroll,new Rect(0,0,835,Mathf.Max(505,height)));
            Label(new Rect(8,8,816,height),content,body);GUI.EndScrollView();
            if(Button(new Rect(80,790,265,48),"Play again",true))S.SelectRole();
            if(Button(new Rect(362,790,265,48),"Main menu"))S.ReturnToMainMenu();
            Label(new Rect(685,798,650,34),"STORMSIM · desktop candidate 2026.09.30-b4-7",small);
        }
        string DecisionReview()
        {
            var experience=S.GetComponent<SibucaoEvacuationExperience>();if(experience)return experience.Review();
            var records=S.state.physical.history;
            var b=new System.Text.StringBuilder("The physical action and its eventual outcome are recorded separately. A setback can follow a completed action.\n\n");
            if(records.Count==0)b.AppendLine("No route decisions were recorded.");
            foreach(var record in records)b.AppendLine(SibucaoDebrief.Decision(S,record)+"\n");
            return b.ToString();
        }
        void ReleaseArrival()
        {
            Panel(new Rect(205,92,1030,716));var data=S.state.physical;
            Label(new Rect(245,125,930,52),data.arrivalReceived?"2 / 2 · Handover to the school team":"1 / 2 · Welcome to the school",h2);
            string text=data.arrivalReceived?"School volunteer: We have recorded your arrival. Please report the people who still need follow-up and the supplies available to you.\n\n"+SibucaoDebrief.Summary(S):
                "School volunteer: You have reached shelter. Rest here while we receive your group.\n\nYour completed assistance and unresolved situations will be included in the handover. Reporting an unconfirmed arrival does not mark that resident as rescued.\n\nTake a moment to review what happened before opening your assessment.";
            float height=body.CalcHeight(new GUIContent(text),908)+24;
            reviewScroll=GUI.BeginScrollView(new Rect(245,205,940,465),reviewScroll,new Rect(0,0,916,Mathf.Max(465,height)));
            Label(new Rect(0,0,908,height),text,body);GUI.EndScrollView();
            if(Button(new Rect(245,716,890,53),data.arrivalReceived?"Confirm handover and view my review":"Check in with the reception team",true)){S.physicalDecisions.ConfirmArrivalReport();reviewScroll=Vector2.zero;}
        }
        void ReleaseRoles(){S.SelectRole();}
        void ReleaseSettings()
        {
            Fill(new Rect(0,0,1440,900),new Color(0,0,0,.72f));Panel(new Rect(174,38,1092,820));
            Label(new Rect(212,64,990,54),"Settings & controls",h2);
            string[] tabs={"Comfort","Graphics","Audio","Controls"};
            for(int i=0;i<4;i++)if(Button(new Rect(212+i*258,135,245,45),tabs[i],tab==i)){tab=i;settingsScroll=Vector2.zero;rebind=-1;}
            var p=S.settings;SibucaoControls.Ensure(p);
            string controlsText=SibucaoDesktopHud.Controls+"\nEscape: pause/back · N: repeat narration · C: dive/center vehicle view · V: wipers";
            float controlsHeight=small.CalcHeight(new GUIContent(SibucaoControls.Prompt(controlsText)),940)+12;
            float controlsFooter=990+controlsHeight+12;
            settingsScroll=GUI.BeginScrollView(new Rect(204,201,1030,556),settingsScroll,new Rect(0,0,995,tab==3?controlsFooter+50:585));
            if(tab==0)
            {
                p.subtitles=ReleaseToggle(0,"Speaker subtitles",p.subtitles);p.hints=ReleaseToggle(62,S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>() is var prepBridge && prepBridge && prepBridge.Active ? "Preparation coaching" : "Contextual hints",p.hints);p.interactPrompts=ReleaseToggle(124,"Interaction prompts",p.interactPrompts);
                p.textScale=ReleaseSlider(198,"Text scale",p.textScale,1,1.3f,"0.00");p.fieldOfView=ReleaseSlider(272,"Field of view",p.fieldOfView,60,100,"0");
                p.reducedFlashes=ReleaseToggle(346,"Reduce lightning and shock flashes",p.reducedFlashes);p.reducedMotion=ReleaseToggle(408,"Reduce camera motion",p.reducedMotion);p.invertLook=ReleaseToggle(470,"Invert vertical look",p.invertLook);
            }
            if(tab==1)
            {
                p.fullscreen=ReleaseToggle(0,"Fullscreen",p.fullscreen);
                Label(new Rect(15,83,500,40),"Rain detail",body);if(Button(new Rect(615,75,340,45),new[]{"Low","Medium","High"}[Mathf.Clamp(p.graphics,0,2)]))p.graphics=(p.graphics+1)%3;
                p.brightness=ReleaseSlider(154,"Brightness",p.brightness,.25f,1,"0.00");
                Label(new Rect(15,255,940,120),"Desktop reference: 1920 × 1080 (16:9). Rain detail changes particle density. Keep your display at a 16:9 resolution for the intended layout.",body);
            }
            if(tab==2)
            {
                p.volume=ReleaseSlider(0,"Master",p.volume,0,1,"0.00");p.speechVolume=ReleaseSlider(86,"Speech",p.speechVolume,0,1,"0.00");p.environmentVolume=ReleaseSlider(172,"Rain / wind / ambience",p.environmentVolume,0,1,"0.00");p.effectsVolume=ReleaseSlider(258,"Interactions / footsteps",p.effectsVolume,0,1,"0.00");
                Label(new Rect(15,376,940,100),"Nearby speech lowers storm ambience while it plays. Subtitles remain available when speech is muted.",body);
            }
            if(tab==3)
            {
                p.sensitivity=ReleaseSlider(0,"Mouse sensitivity",p.sensitivity,.03f,.3f,"0.00");p.toggleGrip=ReleaseToggle(70,"Toggle grip / assistance (press again to release)",p.toggleGrip);
                Label(new Rect(15,139,940,92),"Click a binding, then press a key. Duplicate bindings swap. Esc cancels; Tab, B, G, C, V, N, Right Shift and function keys stay reserved for aliases and special actions. F carry already toggles.",small);
                for(int i=0;i<SibucaoControls.Defaults.Length;i++)
                {float y=246+i*51;Label(new Rect(15,y+8,570,40),SibucaoControls.Names[i],body);if(Button(new Rect(615,y,340,43),rebind==i?"Press a key…":p.keyBindings[i],mapControls:false))rebind=i;}
                if(rebind>=0&&Event.current.type==EventType.KeyDown&&Keyboard.current!=null)
                {if(Keyboard.current.escapeKey.wasPressedThisFrame)rebind=-1;else foreach(var key in Keyboard.current.allKeys)if(key.wasPressedThisFrame&&SibucaoControls.Rebind(p,rebind,key.keyCode)){rebind=-1;Event.current.Use();break;}}
                Label(new Rect(15,990,940,controlsHeight),controlsText,small);
                if(Button(new Rect(15,controlsFooter,380,40),"Restore default bindings")){p.keyBindings=null;SibucaoControls.Ensure(p);}
            }
            GUI.EndScrollView();
            if(Button(new Rect(740,785,215,45),"Back")){rebind=-1;S.ApplySettings();S.SetMode(S.settingsReturn);}
            if(Button(new Rect(974,785,230,45),"Apply",true)){S.ApplySettings();S.Notify("Settings saved","Your preferences apply to both gameplay phases.");}
        }
        bool ReleaseToggle(float y,string label,bool value){Label(new Rect(15,y+8,575,56),label,body);if(Button(new Rect(615,y,340,45),value?"On":"Off"))value=!value;return value;}
        float ReleaseSlider(float y,string label,float value,float min,float max,string format)
        {Label(new Rect(15,y,555,45),label,body);value=GUI.HorizontalSlider(new Rect(615,y+16,245,22),value,min,max);Label(new Rect(877,y,100,40),value.ToString(format),body);return value;}
    }
}
