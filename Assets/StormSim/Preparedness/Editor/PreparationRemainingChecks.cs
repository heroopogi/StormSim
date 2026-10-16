using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness.Editor
{
    public static class PreparationRemainingChecks
    {
        public static string Run(PreparednessSession s,string folder)
        {
            var game=SibucaoSession.Instance;var p=s.player;var baseline=s.Capture();var oldState=game.state;
            var lines=new List<string>();
            void Check(bool value,string label){if(!value)throw new Exception(label);lines.Add("PASS: "+label);File.WriteAllLines(folder+"remaining-checks.txt",lines);}
            PreparednessItem Item(PrepKind kind)=>s.items.First(i=>i.kind==kind);
            void Reset(){game.state=oldState;s.Restore(baseline);s.SetCoaching(true);s.SetPhase(PrepPhase.Preparing);s.preparationSeconds=0;s.evacuationOrdered=false;p.CloseDesktopPanels();}
            try
            {
                Reset();
                var focusItem=Item(PrepKind.Phone);focusItem.storageDoor=null;
                p.Warp(s.homeStart.position+Vector3.up*8,s.homeStart.rotation);focusItem.transform.position=p.view.transform.position+p.view.transform.forward*1.2f;focusItem.SynchronizePose();Physics.SyncTransforms();p.RefreshFocus();
                Check(p.focused==focusItem,"Portable supplies receive focus feedback in clear reach");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try{blocker.transform.position=p.view.transform.position+p.view.transform.forward*.6f;blocker.transform.localScale=Vector3.one*.4f;Physics.SyncTransforms();p.RefreshFocus();Check(p.focused!=focusItem,"Forgiving focus does not select a supply through a solid obstruction");}
                finally{UnityEngine.Object.DestroyImmediate(blocker);}
                Reset();
                Check(s.peacefulSeconds<=5&&s.bulletinSeconds<=12,"Opening waits shortened to at most five and twelve seconds");
                Check(s.items.Select(i=>i.id).Distinct().Count()==s.items.Length,"All preparation persistence IDs are unique");
                Check(s.Capture().layoutId==s.gameObject.scene.name&&s.Capture().homeIndex>=0,"Checkpoint records scene and household layout identity");
                var water=s.items.Single(i=>i.id=="travel-water");
                Check(PreparednessSession.CanPackSupply(water)&&!PreparednessSession.CanPackSupply(s.items.First(i=>i.kind==PrepKind.Water&&i.id!="travel-water")),"Portable water is distinct from bulky home reserves");
                Check(game.inventory.Find("prep:travel-water")==water.GetComponent<PhysicalGrabbable>(),"Portable water is registered in the evacuation inventory");
                Check(!PreparednessSession.CanPackSupply(s.items.First(i=>i.id=="gloves")),"Unrelated household props cannot fill the go-bag");
                Check(PreparednessSession.QuantityGuide.Contains("4 people")&&PreparednessSession.QuantityGuide.Contains("not the household's full requirement"),"Quantities explain household size and the travel bottle's limited share");
                Check(s.RouteDescription(true).Contains("Banago Elementary School I")&&s.RouteDescription(false).Contains("same school route"),"Primary and backup guidance agree with the playable school route");
                Check(PreparednessSession.HouseholdCard.Contains("Older adult")&&PreparednessSession.HouseholdCard.Contains("school reception"),"Household card includes assistance and a meeting/contact plan");
                s.SetPhase(PrepPhase.Bulletin);s.Use(Item(PrepKind.Advisory));Check(s.CanPrepare&&s.advisoryRead,"Reading the bulletin can start preparation without waiting out narration");
                var snapshot=s.Capture();snapshot.items.Single(i=>i.id=="documents").storedIn="bag";snapshot.items.Single(i=>i.id=="radio").storedIn="bag";snapshot.bagInRightHand=true;
                snapshot.items.Single(i=>i.id==s.Bag.id).closed=false;s.Restore(snapshot);s.Bag.transform.position=p.view.transform.position+p.view.transform.forward*.7f;Physics.SyncTransforms();
                float before=s.BagMass;var docs=Item(PrepKind.Documents);var radio=Item(PrepKind.Radio);
                Check(s.Unpack(docs)&&p.held==docs&&!docs.IsPacked&&radio.IsPacked,"Selecting a packed item retrieves it while preserving another item");
                Check(Mathf.Abs(s.BagMass-(before-docs.mass))<.001f,"Displayed bag weight decreases by the retrieved item's weight");
                Check(!s.Unpack(radio)&&radio.IsPacked,"Retrieval with an occupied hand leaves bag contents intact");
                p.Release();Check(!s.Unpack(docs),"Repeated retrieval cannot duplicate an item");
                s.Bag.closed=true;Check(!s.Unpack(radio)&&radio.IsPacked,"Closed bag rejects retrieval without losing contents");
                Reset();docs=Item(PrepKind.Documents);docs.storageDoor=null;p.Grab(docs);s.Bag.storageDoor=null;s.Bag.closed=false;s.Bag.transform.position=p.view.transform.position+p.view.transform.forward*.7f;Physics.SyncTransforms();
                float normalMass=docs.mass;
                try{docs.mass=9;float packedBefore=s.BagMass;Check(!s.Store(docs,Item(PrepKind.BagZone))&&p.held==docs&&s.BagMass==packedBefore,"Overweight packing rejects before releasing the held item");}
                finally{docs.mass=normalMass;}
                Check(s.PlacementHint(docs,Item(PrepKind.ChargeZone)).Contains("Not suitable"),"Placement hint explains a mismatched destination");
                Reset();var phone=Item(PrepKind.Phone);phone.storedIn="bag";phone.charge=79;s.RefreshEvidence();
                Check(!s.decisions[8]&&s.ItemReadiness(phone).Contains("partial"),"Partial charge is described rather than treated as empty");
                phone.charge=80;s.RefreshEvidence();Check(s.decisions[8]&&s.ItemReadiness(phone).Contains("useful"),"Eighty percent charge satisfies the disclosed scenario target");
                var bank=Item(PrepKind.PowerBank);bank.storedIn="bag";bank.charge=80;s.RefreshEvidence();Check(!s.decisions[9]&&s.evidence[9].Contains("cable",StringComparison.OrdinalIgnoreCase),"Power bank readiness also requires the cable");
                Item(PrepKind.Cable).storedIn="bag";s.RefreshEvidence();Check(s.decisions[9],"Useful power-bank charge plus packed cable satisfies readiness");
                var battery=Item(PrepKind.Batteries);battery.cells=0;Check(s.ItemReadiness(battery).Contains("Empty"),"Empty battery organizers remain distinguishable in the bag");
                docs.closed=false;Check(s.ItemReadiness(docs).Contains("Unsealed"),"Unsealed documents show their missing preparation");
                Reset();docs=Item(PrepKind.Documents);docs.closed=true;docs.prepared=true;s.Use(docs);Check(docs.closed,"First reopening interaction warns without undoing protection");
                s.CancelInteraction();s.Use(docs);Check(docs.closed,"Cancel clears a pending reversal confirmation");s.Use(docs);Check(!docs.closed,"Second explicit interaction confirms reopening");
                Reset();s.Use(s.items.First(i=>i.id=="plan-primary"));p.CloseDesktopPanels();s.Use(s.items.First(i=>i.id=="plan-primary"));p.CloseDesktopPanels();Check(s.primaryRoute,"Rereading a route card does not erase the review");
                Reset();s.preparationSeconds=s.evacuationAfterSeconds*.55f;Check(s.OutdoorSafe&&!s.ElevatedWorkSafe,"Rising wind closes elevated work before general outdoor work");
                s.preparationSeconds=s.evacuationAfterSeconds*.8f;Check(!s.OutdoorSafe&&!s.evacuationOrdered,"Unsafe weather closes outdoor work before the evacuation order");
                s.IssueEvacuation();Check(s.NextPreparationStep.Contains("exit"),"Evacuation overrides unfinished optional work");
                Reset();var zone=Item(PrepKind.SupplyZone);p.Warp(zone.transform.position+Vector3.back,Quaternion.identity);
                int moved=s.StoreHouseholdWater(zone);Check(moved>0&&moved<=3,"Household help stores a bounded group of nearby water containers");
                Check(s.HomeWater>=moved*5&&s.items.Count(i=>i.id=="travel-water")==1,"Grouped water handling preserves quantities and the separate travel bottle");
                var groupSave=s.Capture();float litres=s.HomeWater;s.Restore(groupSave);Check(s.HomeWater==litres,"Grouped water quantity survives save/load");
                Reset();var work=s.items.Select(i=>i.GetComponent<PreparednessHouseTask>()).First(t=>t);
                work.RestoreProgress(work.workSeconds*.4f);s.toggleWork=true;var workSave=s.Capture();work.RestoreProgress(0);s.toggleWork=false;s.Restore(workSave);
                Check(Mathf.Abs(work.ProgressSeconds-work.workSeconds*.4f)<.01f&&s.toggleWork&&!work.Working,"Interrupted work and press-once preference restore without unattended work");
                work.Cancel();Check(work.ProgressSeconds>0&&!work.CompleteWork(),"Cancelling preserves partial work but prevents completion");
                Reset();var tool=Item(PrepKind.Tool);tool.storageDoor=null;p.Grab(tool);phone=Item(PrepKind.Phone);phone.charge=53;
                var heldSave=s.Capture();p.Release();phone.charge=0;s.Restore(heldSave);Check(p.held==tool&&Mathf.Abs(phone.charge-53)<.01f,"Held tool and partial device charge survive restore");
                var oldSave=JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(heldSave));oldSave.items=oldSave.items.Where(i=>i.id!="travel-water").ToArray();oldSave.work=null;
                Check(s.Restore(oldSave)&&s.items.Count(i=>i.id=="travel-water")==1,"Older saves migrate the added travel bottle without duplication");
                var malformed=JsonUtility.FromJson<PrepSave>(JsonUtility.ToJson(heldSave));malformed.items[0].charge=float.NaN;string held=p.held.id;
                Check(!s.Restore(malformed)&&p.held.id==held,"Invalid numeric save data is rejected before changing the current run");
                s.Save();s.Save();File.WriteAllText(s.SavePath,"not valid json");s.Load();Check(s.message.Contains("backup")&&p.held==tool,"Corrupt primary checkpoint recovers from its complete backup");
                Reset();s.advisoryRead=true;p.OpenDepartureReview(true);Check(p.ReadingPanel&&s.CanPrepare,"Departure review pauses reading without requiring a completed checklist");p.CloseDesktopPanels();s.Finish(true);Check(s.phase==PrepPhase.Fade,"Empty/incomplete preparations do not block an informed early departure");
                Reset();game.state=new RunState();s.advisoryRead=true;s.departure="early-evacuation";var continuity=game.GetComponent<PreparationContinuity>();continuity.CommitPreparation();
                int score=game.state.scores[0];continuity.CommitPreparation();Check(game.state.scores[0]==score,"Preparation scoring is idempotent");
                Check(game.state.preparationActions.First(r=>r.id=="readiness-19").points==200,"Safe departure receives substantial preparedness credit");
                Check(game.state.preparationActions.Where(r=>r.id.StartsWith("optional-")).Sum(r=>r.points)<PreparationContinuity.ReadinessPoints(19),"Optional activities cannot outweigh safe departure");
                Check(Enumerable.Range(0,12).Sum(PreparationContinuity.ReadinessPoints)>s.GetComponentsInChildren<PreparednessActivity>(true).Length*2,"Essential supply checks outweigh all optional household activities");
                docs.closed=false;PreparationExposure.Observe(game,docs);Check(game.state.physical.flags.Contains("prep_wet:"+docs.id),"Unsealed document contact records water exposure");
                game.state.physical.flags.Clear();docs.closed=true;PreparationExposure.Observe(game,docs);Check(game.state.physical.flags.Contains("prep_seal_protected:"+docs.id)&&!game.state.physical.flags.Contains("prep_wet:"+docs.id),"Sealed document contact records the simulated protection");
                Check(PreparationExposure.Summary(game).Contains("PREPARATION FOLLOW-UP"),"Shelter review includes preparation outcomes rather than only flags");
            }
            finally{game.state=oldState;s.Restore(baseline);p.CloseDesktopPanels();}
            return string.Join("\n",lines);
        }
    }
}
