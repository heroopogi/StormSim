using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace StormSim.Sibucao.Editor
{
    public static class SibucaoRemainingChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        public static string Run()
        {
            Check(Application.isPlaying,"Run in Play mode");var s=SibucaoSession.Instance;s.StartRun();s.state.homeSeconds=90;s.SetMode(SibucaoMode.Playing);
            Check(SibucaoActor.StateForContext(false,false,false,false,WaterTier.Tier1)==ResidentState.Calm,"Calm state failed");
            Check(SibucaoActor.StateForContext(false,false,false,true,WaterTier.Tier1)==ResidentState.Alert,"Storm cue alert failed");
            Check(SibucaoActor.StateForContext(false,true,false,false,WaterTier.Tier1)==ResidentState.Panic,"Nearby water panic failed");
            Check(SibucaoActor.StateForContext(false,false,true,false,WaterTier.Tier1)==ResidentState.Panic,"Final stretch panic failed");
            Check(SibucaoActor.StateForContext(true,true,true,true,WaterTier.Tier5)==ResidentState.ReactionToPlayer,"Player reaction priority failed");
            var actors=Object.FindObjectsByType<SibucaoActor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Check(actors.All(a=>a.animator.avatar.isValid&&a.animator.avatar.isHuman),"Invalid live avatar");
            Check(actors.All(a=>!a.alert_PLACEHOLDER&&!a.reaction_PLACEHOLDER),"Missing gesture clips were incorrectly substituted");
            Check(actors.Count(a=>a.panicIsLocomotion&&a.panic_PLACEHOLDER&&a.panic_PLACEHOLDER.isHumanMotion)==32,"Owned Run not assigned to 32 background actors");
            var actor=actors.First(a=>a.gameObject.activeInHierarchy);actor.React();actor.SendMessage("Update");Check(actor.state==ResidentState.ReactionToPlayer&&actor.MissingCurrentAnimation,"Assist reaction did not expose missing clip honestly");
            var audio=s.GetComponent<SibucaoRouteAudio>();var old=audio.SFX_Scream_PLACEHOLDER;var clip=AudioClip.Create("TEST_ONLY_silent_trigger",44100,1,44100,false);
            try
            {
                audio.SFX_Scream_PLACEHOLDER=clip;s.player.Teleport(audio.wheelchair.transform.position+Vector3.forward*2,Vector3.forward);audio.SendMessage("Update");
                Check(audio.wheelchair.isPlaying,"Environmental scream trigger failed");audio.wheelchair.Stop();audio.SendMessage("Update");Check(!audio.wheelchair.isPlaying,"One-shot repeats every frame");
                s.SetMode(SibucaoMode.Menu);audio.SendMessage("Update");Check(!audio.wheelchair.isPlaying&&!audio.checkpoint.isPlaying,"Route audio continues in menu");
            }
            finally{audio.SFX_Scream_PLACEHOLDER=old;audio.wheelchair.clip=null;Object.Destroy(clip);}
            var props=Object.FindObjectsByType<SibucaoPropSource>(FindObjectsSortMode.None);Check(props.Length==127,"Unexpected prop placement count");
            Check(props.All(p=>p.triangles>0&&p.triangles<=25000&&p.textureResolution==1024),"Imported prop metadata invalid");
            string report="PASS\nCalm / storm Alert / nearby-water Panic / final-stretch Panic / player Reaction transitions\n"+actors.Length+" valid live Humanoid actors; 32 owned Run clips; missing gesture slots remain empty\nEnvironmental one-shot triggers once and stops in menu\n127 validated prop instances, 1024px textures, within mesh budgets\n";
            System.IO.File.WriteAllText("Documentation/SIBUCAO_REMAINING_CHECKS.txt",report);s.StartRun();return report;
        }
    }
}
