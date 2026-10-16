using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    public static class SibucaoPreparationChecks
    {
        public static string Run()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<SibucaoSession>();var bridge=game.GetComponent<SibucaoPreparationBridge>();var s=bridge.preparation;
            if(!Application.isPlaying||!bridge.Active)throw new Exception("Start a preparation run before testing.");
            var baseline=s.Capture();var checks=new List<string>();
            void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);checks.Add(text);}
            PreparednessItem I(string id)=>s.items.First(i=>i.id==id);
            try
            {
                Check(game.gameObject.scene.path=="Assets/Scenes/SIBUCAO_RAIN.unity","Actual Sibucao scene is the entry point");
                Check(s.homeStart==game.homes[0].standingPoint,"Preparation references original first-home spawn transform");
                Check(game.Mode==SibucaoMode.Preparation&&!game.player.gameObject.activeSelf&&Camera.allCamerasCount==1,"Preparation owns input and exactly one camera");
                Check(!game.weather.enabled&&!RenderSettings.fog,"Storm weather is suspended for peaceful preparation");
                Check(s.items.Count(i=>i.kind==PrepKind.HouseholdTask)==22,"22 household tasks installed");
                Check(s.items.Count(i=>i.kind==PrepKind.Neighbor)>=6,"Original residents and neighborhood households are registered");
                Check(s.items.Select(i=>i.id).Distinct().Count()==s.items.Length,"Unique persistence IDs including external house doors");
                s.SetPhase(PrepPhase.Preparing);
                for(int n=0;n<4;n++)
                {
                    var door=bridge.houseDoors[n];door.closed=false;door.ApplyVisual();var h=game.homes[n].transform;
                    s.player.Warp(h.TransformPoint(new Vector3(2.5f,.12f,-2.7f)),h.rotation);Physics.SyncTransforms();
                    var motor=s.player.GetComponent<CharacterController>();for(int k=0;k<45;k++)motor.Move(h.TransformDirection(new Vector3(0,-.01f,-.05f)));
                    Check(h.InverseTransformPoint(s.player.transform.position).z<-4.5f,"Home "+(n+1)+" doorway is traversable");
                }
                var npc=I("neighbor-1-0");var neighbor=npc.GetComponent<PreparednessNeighbor>();npc.cells=0;npc.prepared=false;s.advisoryRead=false;
                neighbor.Talk(s);Check(npc.cells==0,"Neighbor warning requires checking the advisory first");
                s.advisoryRead=true;neighbor.Talk(s);Check(npc.prepared&&npc.cells==1,"Warning is confirmed immediately; follow-up remains available");
                neighbor.Talk(s);neighbor.Talk(s);Check(npc.prepared&&npc.cells==3,"Warning, needs and meeting plan acknowledged");
                var task=I("fastener-0").GetComponent<PreparednessHouseTask>();task.Begin(s,null);Check(!task.CompleteWork(),"Task rejects missing tool");
                s.player.Grab(I("screwdriver"));task.Begin(s,s.player.held);Check(!task.CompleteWork(),"Task requires elapsed work time");
                typeof(PreparednessHouseTask).GetField("progress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(task,task.workSeconds);
                Check(task.CompleteWork()&&I("fastener-0").prepared&&task.after.activeSelf&&!task.before.activeSelf,"Completed work changes visible fitting state");s.player.Release();
                s.evacuationOrdered=true;var outdoor=I("drain-clean-0").GetComponent<PreparednessHouseTask>();s.player.Grab(I("house-brush"));outdoor.Begin(s,s.player.held);Check(!outdoor.CompleteWork(),"Evacuation advisory blocks exterior cleanup");s.player.Release();s.evacuationOrdered=false;
                var data=s.Capture();I("fastener-0").prepared=false;npc.prepared=false;
                Check(s.Restore(JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(data)))&&I("fastener-0").prepared&&npc.prepared,"Household tasks and neighbor acknowledgement survive save restore");
                Check(bridge.houseDoors[0].transform.parent==game.homes[0].transform,"Restore preserves original house door hierarchy");
                var text="PASS: "+checks.Count+" integration checks\n"+string.Join("\n",checks);File.WriteAllText("Documentation/TORMSIM_SIBUCAO_CHECKS.txt",text);return text;
            }
            finally{s.Restore(baseline);}
        }
    }
}
