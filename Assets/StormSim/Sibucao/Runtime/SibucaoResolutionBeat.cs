using System.Collections;
using UnityEngine;
namespace StormSim.Sibucao
{
    // All choices get the same duration, sound level and presentation. Only the
    // factual action line changes; score, roll and success are never inputs.
    public sealed class SibucaoResolutionBeat:MonoBehaviour
    {
        public PhysicalDecisionStation station;
        public string[] lines;
        public AudioSource source;
        public AudioClip weatherCue;
        public int PlayCount {get;private set;}
        public string LastLine {get;private set;}
        public int LastChoice {get;private set;}=-1;
        public int PresentedCount {get;private set;}
        public void Play(int choice)
        {
            var s=SibucaoSession.Instance;if(!s||!station||!station.Resolved||s.state.physical.results[station.index]!=choice)return;
            string flag="resolution_beat_"+station.index;if(s.state.physical.flags.Contains(flag))return;
            s.physicalDecisions.SetFlag(flag);LastChoice=choice;PlayCount++;
            LastLine=lines!=null&&choice<lines.Length?lines[choice]:"I'm through this section. Keep moving.";
            StopAllCoroutines();StartCoroutine(Reaction());
        }
        IEnumerator Reaction()
        {
            yield return new WaitForEndOfFrame();var s=SibucaoSession.Instance;
            while(s&&s.Mode==SibucaoMode.Cinematic)yield return null;
            if(!s||s.Mode!=SibucaoMode.Playing)yield break;
            PresentedCount++;s.Say(LastLine,5);s.Notify("Situation completed",LastLine);s.player.Gesture();
            if(source&&weatherCue){source.volume=SibucaoAudioMix.Environment;source.clip=weatherCue;source.Play();yield return new WaitForSeconds(2.2f);source.Stop();}
        }
        public void ResetBeat(){StopAllCoroutines();if(source)source.Stop();LastChoice=-1;LastLine=null;PlayCount=PresentedCount=0;}
    }
}
