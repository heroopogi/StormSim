using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    public static class PreparednessContinuityChecks
    {
        public static string Run()
        {
            if(!Application.isPlaying)throw new Exception("Run in Play mode.");
            var game=SibucaoSession.Instance;var bridge=game.GetComponent<SibucaoPreparationBridge>();
            if(!bridge.Active)game.StartRun();
            var s=bridge.preparation;var p=s.player;var c=game.GetComponent<PreparationContinuity>();
            bool existed=File.Exists(s.SavePath);string saved=existed?File.ReadAllText(s.SavePath):null;
            var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label+" | "+s.message);checks.Add("PASS "+label);}
            PreparednessItem I(string id)=>s.items.First(i=>i.id==id);
            try
            {
                s.Restart();s.SetPhase(PrepPhase.Preparing);
                Check(s.GetComponentsInChildren<PreparednessActivity>(true).Length==10,"Exactly ten optional activities available");
                Check(s.items.Count(i=>i.kind==PrepKind.Neighbor)==100&&s.items.Any(i=>i.id=="door"),"All neighborhood contacts and original house door remain registered");
                Check(s.GetComponentsInChildren<PreparednessActivity>(true).All(a=>!a.Complete(s)),"Optional activities start incomplete");
                var roof=I("repair-roof-panel").GetComponent<PreparednessHouseTask>();roof.Begin(s,null);
                Check(!I("repair-roof-panel").prepared&&s.message.Contains("First:"),"Roof task requires secured ladder");
                var bin=I("indoor-waste-bin").GetComponent<PreparednessWasteBin>();bin.Use(s,null);
                Check(!I("indoor-waste-bin").closed,"Waste lid opens");
                bin.Use(s,I("food-0"));Check(I("indoor-waste-bin").cells==0,"Waste bin rejects usable supplies");
                var waste=I("house-wastebags");I("bag-refuse").prepared=true;
                waste.transform.position=bin.transform.position+Vector3.up*.3f;bin.Use(s,waste);
                Check(I("indoor-waste-bin").cells==1,"Filled household refuse is deposited");
                bin.Use(s,waste);Check(I("indoor-waste-bin").cells==1,"Same refuse cannot be deposited twice");bin.Use(s,null);
                Check(s.GetComponentsInChildren<PreparednessActivity>(true).First(a=>a.id=="optional-waste").Complete(s),"Closing bin verifies optional waste activity");
                var binState=s.Capture();s.Restore(JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(binState)));
                Check(I("indoor-waste-bin").cells==1&&I("indoor-waste-bin").closed,"Bin state survives preparation save/load");
                s.Use(I("closet-door"));p.GrabBag(s.Bag,true);s.Bag.closed=false;
                s.Bag.transform.position=p.view.transform.position+Vector3.forward*.5f;s.Bag.SynchronizePose();
                var food=I("food-0");p.Grab(food,true);
                Check(s.Store(food,I("bag-zone")),"Food physically packs into preparation bag");
                p.Grab(I("documents"),true);I("documents").closed=true;I("documents").prepared=true;
                Check(s.Store(I("documents"),I("bag-zone")),"Sealed document pouch packs into the same bag");
                s.advisoryRead=true;s.Finish(false);s.Finish(false);
                Check(s.phase==PrepPhase.Fade&&s.Bag.storedIn=="table","Incomplete optional tasks do not prevent sleep; bag stays on table");
                var bag=s.Bag;Vector3 bagPosition=bag.transform.position;s.CompleteTransition();bridge.BeginStorm();
                Check(game.Mode==SibucaoMode.Playing&&game.state.homeSeconds==0&&game.state.elapsed==0,"Wake-up begins with reset storm clocks");
                Check(s.Bag==bag&&Vector3.Distance(bag.transform.position,bagPosition)<.01f&&bag.gameObject.activeInHierarchy,"Exact same bag remains at its prepared table position");
                Check(c.legacyBags.All(b=>!b.activeSelf),"Replacement bags stay hidden");
                Check(game.inventory.Count==2,"Both prepared contents are present in inventory data");
                game.inventory.Collect(bag.GetComponent<PhysicalGrabbable>());
                Check(game.inventory.HasBag&&game.state.physical.backpackId=="prep:bag"&&game.inventory.Count==2,"Collecting the same bag preserves its contents without duplicates");
                int points=game.state.scores.Sum();c.CommitPreparation();Check(game.state.scores.Sum()==points,"Preparation scoring is idempotent");
                Check(game.state.preparationActions.Any(a=>a.id=="optional-waste"&&a.completed&&a.points==2)&&game.state.preparationActions.Any(a=>a.id=="optional-roof"&&!a.completed),"Completed and skipped optional activities are recorded for aftermath");
                game.inventory.Collect(I("food-1").GetComponent<PhysicalGrabbable>());Check(game.inventory.Count==3,"Additional supplies can be collected during the typhoon");
                points=game.state.scores.Sum();game.inventory.Collect(I("food-1").GetComponent<PhysicalGrabbable>());Check(points==game.state.scores.Sum()&&game.inventory.Count==3,"Repeated pickup gives neither duplicates nor extra points");
                game.state.physical.hunger=60;game.inventory.Selected=Array.IndexOf(game.state.physical.inventorySlots,"prep:food-0");game.inventory.EquipSelected();
                Check(game.state.physical.hunger==25&&game.inventory.Count==2&&game.state.physical.flags.Contains("consumed:prep:food-0"),"Eating consumes one food pack and reduces hunger");
                game.physicalDecisions.SavePhysical();var json=JsonUtility.ToJson(game.state);game.state=JsonUtility.FromJson<RunState>(json);
                bridge.RestoreStormSnapshot(game.state.preparationSnapshot);game.physicalDecisions.RestoreRun();
                Check(game.inventory.HasBag&&game.inventory.Count==2&&!I("food-0").gameObject.activeInHierarchy&&I("food-0").GetComponent<PhysicalGrabbable>().delivered,"Storm save/load preserves inventory and does not resurrect consumed food");
                Check(game.state.physical.hunger==25&&game.state.preparationActions.Any(a=>a.id=="storm-collected:prep:food-1"),"Vitals and during-phase action record survive save/load");
                string report=string.Join("\n",checks);File.WriteAllText("output/preparedness/continuity-checks.txt",report);return report;
            }
            finally
            {
                if(existed)File.WriteAllText(s.SavePath,saved);else if(File.Exists(s.SavePath))File.Delete(s.SavePath);
            }
        }
    }
}
