using UnityEngine;
using System.Collections;
namespace StormSim.Preparedness
{
    public sealed class PreparednessLadder : MonoBehaviour
    {
        public PreparednessItem setup;
        public Vector3 basePosition,workingPosition;
        public void Use(PreparednessSession s)
        {
            if(s.player.LadderClimbing)return;
            bool upstairs=s.player.transform.position.y>basePosition.y+.8f;
            if(!upstairs&&(!s.ElevatedWorkSafe||!setup.prepared)){s.Say("Elevated property work is optional and only available in calm conditions. Stay on the ground as wind rises.");return;}
            if(!upstairs&&(s.player.carriedBag||s.player.held)){s.Say("Put carried items down before using the ladder.");return;}
            StartCoroutine(Climb(s,upstairs?basePosition:workingPosition,upstairs));
        }
        IEnumerator Climb(PreparednessSession s,Vector3 destination,bool descending)
        {
            var player=s.player;player.LadderClimbing=true;var start=player.transform.position;
            float time=0;while(time<1.5f&&s.CanPrepare){if(!descending&&!s.ElevatedWorkSafe){destination=basePosition;descending=true;start=player.transform.position;time=0;}time+=Time.deltaTime;player.Warp(Vector3.Lerp(start,destination,Mathf.SmoothStep(0,1,time/1.5f)),player.transform.rotation);yield return null;}
            player.LadderClimbing=false;
            s.Say(descending?"Returned to the ground.":"At the supported working platform. Use the screwdriver on the loose panel; use the ladder to descend.");
        }
    }
}
