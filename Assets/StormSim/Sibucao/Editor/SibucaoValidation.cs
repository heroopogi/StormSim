using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoValidation
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception("Sibucao validation: "+message);}
        public static string SceneAudit()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();Require(s,"session missing");
            Require(s.route.spawnPoints.Length==5,"five spawn candidates required");Require(s.encounters.Length==8,"seven decisions plus arrival required");
            Require(s.route.length>650,"route too short");Require(s.route.spawnDistances.Max()<s.encounters[0].routeDistance-25,"spawn bypasses opening encounters");
            Require(s.encounters.SelectMany(e=>e.node.choices).Count()==21,"21 choices required");
            foreach(var n in s.encounters.Select(e=>e.node)){Require(n.tier==WaterTier.Tier1,"inactive tier must be Tier1");Require(n.choices.All(c=>c && c.baseSuccessChance>=0&&c.baseSuccessChance<=1),"invalid choice");}
            var flood=s.GetComponent<FloodSimulationManager>();Require(flood.GetWaterDepth()==0&&flood.GetWaterTier()==WaterTier.Tier1,"flood stub contract");
            Require(s.player.fingerBones.Length==10 && s.player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==2,"rigged hands missing");
            Require(s.weather.rainAudio.clip&&s.weather.windAudio.clip&&s.weather.thunder.Length>0,"storm audio missing");
            Require(s.weather.stormSky.shader.name=="StormSim/Sibucao Overcast Sky","storm shader missing");
            var ui=s.GetComponent<SibucaoUI>();Require(ui.roboto&&ui.robotoBold,"Roboto missing");
            Physics.SyncTransforms();int samples=0;
            for(float d=1;d<s.route.length;d+=.5f)
            {
                var p=s.route.Sample(d);Require(Physics.Raycast(p+Vector3.up*1.5f,Vector3.down,out var hit,4,1,QueryTriggerInteraction.Ignore),"no ground at route metre "+d);
                var walls=Physics.OverlapCapsule(hit.point+Vector3.up*.4f,hit.point+Vector3.up*1.5f,.25f,1,QueryTriggerInteraction.Ignore).Where(c=>!c.GetComponent<SibucaoPlayer>()).ToArray();
                Require(walls.Length==0,"route blocked at "+d+"m: "+string.Join(",",walls.Select(c=>c.name)));samples++;
            }
            return "PASS: "+samples+" grounded clearance samples; five distant spawns; 21 choices; Tier1/zero depth; rigged hands; fonts; storm audio.";
        }
        public static string GameplayAudit()
        {
            Require(EditorApplication.isPlaying,"run gameplay audit in Play mode");var s=SibucaoSession.Instance;
            var rng=UnityEngine.Random.state;int oldSpawn=PlayerPrefs.GetInt("SibucaoLastSpawn",-1);
            try
            {
                int previous=-1;var seen=new System.Collections.Generic.HashSet<int>();
                for(int i=0;i<30;i++){s.StartRun();Require(s.state.spawnIndex!=previous,"consecutive spawn repeated");previous=s.state.spawnIndex;seen.Add(previous);}
                Require(seen.Count>=3,"spawn variation failed");
                for(int i=0;i<7;i++)
                {
                    s.OpenDecision(s.encounters[i]);Require(s.Mode==SibucaoMode.Decision,"decision did not open");s.Choose(i==3?1:0);Require(s.state.resolved[i+1],"decision did not resolve");
                    int total=s.state.scores.Sum();s.Choose(0);Require(s.state.scores.Sum()==total,"duplicate choice scored");
                }
                Require(s.state.firstAid==0,"first aid not consumed");Require(s.state.companion,"companion not recruited");Require(s.state.speedMultiplier<1,"carrying/assistance has no movement effect");
                var roundtrip=JsonUtility.FromJson<RunState>(JsonUtility.ToJson(s.state));Require(roundtrip.chosen.SequenceEqual(s.state.chosen)&&roundtrip.scores.SequenceEqual(s.state.scores),"save data roundtrip failed");
                s.Complete();Require(s.Mode==SibucaoMode.Complete,"arrival did not complete");Require(s.state.scores[0]==0,"Preparedness scored");int score=s.state.scores.Sum();s.Complete();Require(score==s.state.scores.Sum(),"arrival scored twice");
                s.StartRun();s.OpenDecision(s.encounters[6]);s.state.firstAid=0;s.Choose(0);Require(!s.state.resolved[7],"unavailable kit choice accepted");s.SetMode(SibucaoMode.Playing);
                s.StartRun();s.state.pendingPenalty=8;s.state.pendingAt=10;s.Complete();Require(s.state.pendingPenalty==0&&s.state.scores[1]==12,"pending consequences lost on arrival");
                s.StartRun();s.Pause();Require(Time.timeScale==0,"pause failed");s.SetMode(SibucaoMode.Menu);s.weather.SetStorm(false);
                return "PASS: 30 randomized starts; seven decisions; duplicate-score guards; first-aid gating; companion effect; save roundtrip; completion; pending consequences; pause.";
            }
            finally{UnityEngine.Random.state=rng;PlayerPrefs.SetInt("SibucaoLastSpawn",oldSpawn);PlayerPrefs.Save();}
        }
    }
}
