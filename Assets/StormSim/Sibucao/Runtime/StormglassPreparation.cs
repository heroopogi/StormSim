using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using StormSim.Preparedness;

namespace StormSim.UI
{
    public sealed partial class StormglassUI
    {
        RectTransform aimCircle, preparationProgress;
        TMP_Text preparationProgressText;
        Texture2D aimTexture;
        Sprite aimSprite;
        readonly bool[] previousTasks = new bool[20];
        string progressRun;
        string completionText;
        float completionUntil;
        RectTransform phoneFrame;
        TMP_Text phoneStatus, phonePlaybackLabel;
        AudioSource taskSound;
        AudioClip taskChime;

        void CreatePreparationHud()
        {
            taskSound=gameObject.AddComponent<AudioSource>();taskSound.playOnAwake=false;taskSound.spatialBlend=0;
            const int rate=22050;var samples=new float[6615];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float frequency=t<.12f?660:880;samples[i]=Mathf.Sin(t*frequency*Mathf.PI*2)*.12f*Mathf.Min(1,t*70)*Mathf.Clamp01((.3f-t)*15);}
            taskChime=AudioClip.Create("Preparation complete chime",samples.Length,1,rate,false);taskChime.SetData(samples,0);
            aimCircle=Rect("Aim circle",hud,new Vector2(12,12));
            aimTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16));
                aimTexture.SetPixel(x,y,d>10 && d<14?Color.white:Color.clear);
            }
            aimTexture.Apply();
            aimSprite=Sprite.Create(aimTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f));
            var image=aimCircle.gameObject.AddComponent<Image>();image.sprite=aimSprite;image.raycastTarget=false;
            var outline=aimCircle.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=Vector2.one;
            preparationProgress=Card(hud,"Current narrated instruction",Vector2.zero,new Vector2(380,126),out preparationProgressText);
            preparationProgress.anchorMin=preparationProgress.anchorMax=new Vector2(0,1);
            preparationProgress.pivot=new Vector2(0,1);preparationProgress.anchoredPosition=new Vector2(24,-24);
            preparationProgressText.fontSize=17;preparationProgressText.enableAutoSizing=true;preparationProgressText.fontSizeMin=13;preparationProgressText.fontSizeMax=17;
        }
        void UpdatePreparationHud()
        {
            bool visible=screenKey=="HUD";
            aimCircle.gameObject.SetActive(visible&&!XRSettings.isDeviceActive);
            preparationProgress.gameObject.SetActive(visible && Prep);
            if(phoneStatus && Prep){var media=Prep.GetComponent<PreparationPhoneMedia>();if(media){phoneStatus.text=media.Status;if(phonePlaybackLabel)phonePlaybackLabel.text=media.Paused?"Play":"Pause";}}
            if(!Prep)return;
            if(progressRun!=Prep.runId)
            {
                progressRun=Prep.runId;System.Array.Copy(Prep.decisions,previousTasks,20);completionUntil=0;
            }
            for(int i=0;i<20;i++)
            {
                if(Prep.decisions[i]&&!previousTasks[i])
                {
                    completionText="DONE: "+PreparednessSession.Titles[i];completionUntil=Time.unscaledTime+4;
                    if(taskSound&&!taskSound.isPlaying)taskSound.PlayOneShot(taskChime,S.settings.effectsVolume);
                }
                previousTasks[i]=Prep.decisions[i];
            }
            preparationProgressText.text="<color=#80E2E1>CURRENT INSTRUCTION</color>\n"+
                VRText(Prep.CurrentInstructionText)+"\n<size=14>J / X: open field guide</size>";
            if(visible && Time.unscaledTime<completionUntil)
            {notice.gameObject.SetActive(true);noticeText.text=completionText+"\nPreparation updated.";}
        }
        void BuildPreparationChecklist()
        {
            MeterCard("PREPARATION",Prep.decisions.Count(v=>v)+" / 20 checked",Prep.decisions.Count(v=>v)/20f);
            MessageCard("NEXT BEST ACTION",Prep.NextPreparationStep,StormglassTheme.Accent);
            Action("Hear the next checklist step",()=>Prep.NarrateChecklist(),height:72);
            Text("Checks update only from physical supplies and completed actions. Optional chores can stay unfinished. Leave when advised.",20);
            for(int g=0;g<5;g++)
            {
                Text(Prep.GroupHeading(g),24,StormglassTheme.Accent);
                foreach(int i in PreparednessSession.GroupChecks[g])
                    Text((Prep.decisions[i]?"[DONE] ":"[TO DO] ")+PreparednessSession.Titles[i]+(Prep.decisions[i]?"":"\n"+Prep.evidence[i]),20);
            }
        }
    }
}
