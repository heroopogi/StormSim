using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Preparedness
{
    public enum PrepPhase { Peaceful, Bulletin, Preparing, Fade, ReadyForStorm, MorningPacking }
    [Serializable] public sealed class PrepItemSave
    {
        public string id, storedIn;public Vector3 position;public Quaternion rotation;
        public bool prepared,closed,plugged,gathered;public string storageDoor;public int cells;public float charge;
    }
    [Serializable] public sealed class PrepWorkSave
    {
        public string id;
        public float seconds;
    }
    [Serializable] public sealed class PrepSave
    {
        public PreparationChapterSave chapter; public int version=2; public string runId,departure,heldId;public PrepPhase phase;
        public bool bagRetrieved,bagInRightHand;
        public float health=100,stamina=100,hunger;
        public float elapsed,phaseTime;public bool evacuationOrdered,advisoryRead,primaryRoute,backupRoute;
        public float preparationSeconds;public int tutorialProgress;public bool guidanceUsed;
        public string layoutId;public int homeIndex;
        public int coachingMode; // 0 = legacy preference, 1 = guided, 2 = explore.
        public bool toggleWork;
        public PrepWorkSave[] work;
        public Vector3 playerPosition;public Quaternion playerRotation;public PrepItemSave[] items;
        public bool[] decisions;public string[] evidence;
    }

    public sealed partial class PreparednessSession : MonoBehaviour
    {
        public PreparationChapter chapter;
        public PreparednessItem[] items;
        public PreparednessPlayer player;
        public TextMesh television, statusBoard;
        public Light sun;
        public Transform homeStart;
        public bool bagRetrieved;
        public PrepPhase phase;
        public float peacefulSeconds=18,bulletinSeconds=24,elapsed,phaseTime;
        [Tooltip("Active preparation seconds before the training evacuation advisory. Reading panels and settings pause this clock.")]
        public float evacuationAfterSeconds=720;
        public float preparationSeconds;
        public int tutorialProgress;
        public bool guidanceUsed;
        public bool evacuationOrdered,advisoryRead,primaryRoute,backupRoute;
        public string departure="",runId="",message="Explore the home. Listen for the television bulletin.";
        public readonly bool[] decisions=new bool[20];
        public readonly string[] evidence=new string[20];
        float messageTime;PrepSave initial;float refresh;bool televisionMediaFinished;
        public bool RecentMessage=>Time.unscaledTime<messageTime+6&&!string.IsNullOrEmpty(message)&&message!="New preparation run.";
        public string SavePath=>Path.Combine(StormSim.Sibucao.SibucaoSession.Instance?Path.GetDirectoryName(StormSim.Sibucao.SibucaoSession.Instance.SlotPath(false)):Application.persistentDataPath, StormSim.Sibucao.SibucaoSession.Instance && StormSim.Sibucao.SibucaoSession.Instance.GetComponent<StormSim.Sibucao.CoastalAreaProfile>() ? StormSim.Sibucao.SibucaoSession.Instance.SavePrefix+"-preparation.json" : "TORMSIM_Preparedness_v2.json");
        public bool CanPrepare=>!PreparationIntro.Blocking&&!CoachingChoicePending&&(phase==PrepPhase.Preparing||phase==PrepPhase.MorningPacking);
        public bool OutdoorSafe=>CanPrepare&&Weather<PreparationWeather.UnsafeOutside;
        public bool toggleWork;
        public PreparednessItem Bag=>items.FirstOrDefault(i=>i.kind==PrepKind.Bag);
        public int WarnedHouseholds=>items.Count(i=>i.kind==PrepKind.Neighbor&&i.prepared);
        public int PeopleReached=>items.Where(i=>i.kind==PrepKind.Neighbor&&i.prepared).Sum(i=>i.id.StartsWith("family-")?4:1);
        public string WarningProgress=>"Optional warnings: "+WarnedHouseholds+" households contacted | "+PeopleReached+" people reached";
        bool sleepConfirmationPending;
        public static readonly string[] Titles={"Store household water","Store ready-to-eat food","Test and pack flashlight","Test and pack radio","Pack spare batteries","Check and pack first aid","Check and pack medicines","Seal and pack documents","Charge and pack phone","Charge power bank and pack cable","Pack hygiene and dry clothing","Close and stage the go-bag","Raise disconnected electronics","Raise valuables","Close and latch window","Check door and exit access","Bring loose objects indoors","Clear dry surface litter","Choose primary and backup routes","Check advisory and depart safely"};

        void Start()
        {
            EnsureInitial();ApplyPhase();
        }
        public void EnsureInitial()
        {
            EnsureImprovements();EnsureExperienceSupport();if(initial!=null)return;
            // Authored preparation props begin at rest. This also neutralizes any
            // velocity left by an interrupted play session before the baseline is saved.
            foreach(var item in items.Where(i=>i&&i.portable&&!i.held)){item.FitColliderToVisual();item.SetPhysics(false);item.SynchronizePose();}
            runId=Guid.NewGuid().ToString("N");RefreshEvidence();initial=Capture();
        }
        public void ResetForHomeMove(){if(initial!=null)Restart();}
        public void RefreshInitialPlacement()
        {
            if(initial==null)return;
            foreach(var saved in initial.items)
            {
                var item=items.FirstOrDefault(i=>i.id==saved.id);
                if(!item||!item.transform.IsChildOf(transform))continue;
                saved.position=item.transform.position;saved.rotation=item.transform.rotation;
            }
        }
        public void RebaseHome(Vector3 offset)
        {
            if(initial==null)return;
            initial.playerPosition+=offset;
            foreach(var item in initial.items)
            {var live=items.FirstOrDefault(i=>i.id==item.id);if(live&&live.transform.IsChildOf(transform))item.position+=offset;}
        }
        public void RebaseHome(Matrix4x4 delta, Quaternion rotation)
        {
            if(initial==null)return;
            initial.playerPosition=delta.MultiplyPoint3x4(initial.playerPosition);
            initial.playerRotation=rotation*initial.playerRotation;
            foreach(var item in initial.items)
            {
                var live=items.FirstOrDefault(i=>i.id==item.id);
                if(!live||!live.transform.IsChildOf(transform))continue;
                item.position=delta.MultiplyPoint3x4(item.position);
                item.rotation=rotation*item.rotation;
            }
        }
        public void RecoverDroppedItem(PreparednessItem item)
        {
            var original=initial?.items.FirstOrDefault(i=>i.id==item.id);if(original==null)return;
            item.SetPhysics(false);item.transform.SetParent(transform,true);item.transform.SetPositionAndRotation(original.position,original.rotation);
            item.storedIn=original.storedIn;item.gathered=false;item.storageDoor=items.FirstOrDefault(i=>i.id==original.storageDoor);
            item.SynchronizePose();
            Say(item.displayName+" returned to its original storage spot after falling outside the playable area.");
        }
        public bool NeedsDroppedItemRecovery(PreparednessItem item)
        {
            if(!item||!item.portable||item.held||!item.gameObject.activeSelf)return false;
            var original=initial?.items.FirstOrDefault(i=>i.id==item.id);
            if(original==null)return homeStart&&(item.transform.position.y<homeStart.position.y-2||Vector3.Distance(item.transform.position,homeStart.position)>18);
            var delta=item.transform.position-original.position;
            // Twelve metres still permits every intended indoor/outdoor preparation
            // placement while catching props that escape through walls or the terrain.
            return new Vector2(delta.x,delta.z).magnitude>12||delta.y>5||delta.y<-2;
        }
        void Update()
        {
            if(CoachingChoicePending||PreparationIntro.Blocking)return;
            if(StormSim.Sibucao.SibucaoSession.Instance&&StormSim.Sibucao.SibucaoSession.Instance.Mode==StormSim.Sibucao.SibucaoMode.Settings)return;
            if(player&&player.ReadingPanel)return;
            if(phase!=PrepPhase.ReadyForStorm){elapsed+=Time.deltaTime;phaseTime+=Time.deltaTime;}
            var preparationTelevision=GetComponent<PreparationTelevision>();
            bool mediaControlsOpening=preparationTelevision&&preparationTelevision.UsesMediaTiming;
            if(!mediaControlsOpening&&phase==PrepPhase.Peaceful&&phaseTime>=peacefulSeconds)SetPhase(PrepPhase.Bulletin);
            if(!mediaControlsOpening&&phase==PrepPhase.Bulletin&&phaseTime>=bulletinSeconds)CompleteTelevisionBulletin();
            if(CanPrepare)
            {
                AdvancePreparation(Time.deltaTime);
                TickPreparationSupport();
                foreach(var i in items.Where(i=>i.storedIn=="charger"))i.charge=Mathf.Min(100,i.charge+Time.deltaTime*3);
            }
            if(phase==PrepPhase.Fade&&phaseTime>=2.5f){CompleteTransition();Save();}
            if(Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.2f;DetectTableDrops();RefreshEvidence();ApplyPhase();}
        }
        public void Say(string text){message=text;messageTime=Time.unscaledTime;}
        public void CancelInteraction()
        {
            sleepConfirmationPending=false;
            undoId="";
            foreach(var task in GetComponentsInChildren<PreparednessHouseTask>(true))task.Cancel();
        }
        public void SetPhase(PrepPhase value)
        {
            phase=value;phaseTime=0;ApplyPhase();
            if(value==PrepPhase.Preparing)NarrateAfterTelevision();
        }
        public void NotifyTelevisionMediaFinished()
        {
            televisionMediaFinished=true;advisoryRead=true;
            if(!PreparationIntro.Blocking&&!CoachingChoicePending)CompleteTelevisionBulletin();
        }
        public void CompleteTelevisionBulletin()
        {
            if(phase!=PrepPhase.Peaceful&&phase!=PrepPhase.Bulletin)return;
            televisionMediaFinished=true;advisoryRead=true;SetPhase(PrepPhase.Preparing);
        }
        void NarrateAfterTelevision()
        {
            string cue=Guided?"prep.after-tv.guided":"prep.after-tv.explore";
            SpeakCue(cue,"Household guide",Guided?PreparationNarration.AfterTelevisionGuided:PreparationNarration.AfterTelevisionExplore);
        }
        public void CompleteTransition()
        {
            if(phase!=PrepPhase.Fade)return;
            if(departure=="overnight")
            {
                // Sleeping ends Preparation.  The preparation bridge now hands the same
                // session and UI to SibucaoHomeOpening, which owns the mid-typhoon wake,
                // television interference, blackout and stand-up sequence.
                player.ReleaseAll();
                SetPhase(PrepPhase.ReadyForStorm);
            }
            else SetPhase(PrepPhase.ReadyForStorm);
        }
        void ApplyPhase()
        {
            if(!television)return;
            television.text="";
            var televisionRenderer=television.GetComponent<Renderer>();if(televisionRenderer)televisionRenderer.enabled=false;
            if(statusBoard)statusBoard.text="HOUSEHOLD OF FOUR\nMeet at school reception\nPrimary: marked inland school route\nBackup: marked side passages\nJ: care card, quantities and route plan\nFictional training household";
        }
        public void Use(PreparednessItem target,PreparednessItem held=null)
        {
            if(CoachingChoicePending||PreparationIntro.Blocking||!target)return;
            if(target.kind!=PrepKind.Bed)sleepConfirmationPending=false;
            if(target.kind==PrepKind.Advisory)
            {
                var activeTelevision=GetComponent<PreparationTelevision>();
                if((phase==PrepPhase.Peaceful||phase==PrepPhase.Bulletin)&&activeTelevision&&activeTelevision.UsesMediaTiming)
                {Say("The weather bulletin is still playing. Listen for the preparation instructions that follow.");return;}
                if(phase==PrepPhase.Peaceful){SetPhase(PrepPhase.Bulletin);SpeakCue("bulletin.open","Television","Listen to the training weather bulletin.");return;}
                if(phase==PrepPhase.Bulletin){CompleteTelevisionBulletin();return;}
                if(CanPrepare){advisoryRead=true;Say("Television advisory: "+WarningStageText);}return;
            }
            if(!CanPrepare){Say(phase==PrepPhase.ReadyForStorm?"Preparation saved. The typhoon is arriving.":"Wait for the weather bulletin before starting preparation.");return;}
            if(chapter && (target==chapter.family||target==chapter.shopkeeper||target==chapter.barangay||!chapter.Owns(target)) && chapter.TryOpen(target))return;
            var poster=target.GetComponent<PosterSupply>();if(poster){poster.Use(this);return;}
            var bin=target.GetComponent<PreparednessWasteBin>();if(bin){bin.Use(this,held);return;}
            var ladder=target.GetComponent<PreparednessLadder>();if(ladder){ladder.Use(this);return;}
            if(target.kind==PrepKind.HouseholdTask){target.GetComponent<PreparednessHouseTask>().Begin(this,held);return;}
            if(target.kind==PrepKind.Neighbor){target.GetComponent<PreparednessNeighbor>().Talk(this);return;}
            if(target.kind==PrepKind.SupplyZone&&!held){StoreHouseholdWater(target);return;}
            if(target.IsZone&&target.kind!=PrepKind.Bed&&target.kind!=PrepKind.Exit){Store(held,target);return;}
            if(target.kind==PrepKind.Bag&&held&&held!=target){Store(held,items.First(i=>i.kind==PrepKind.BagZone));return;}
            if(target.kind==PrepKind.Litter&&held&&held.kind==PrepKind.Tool)
            {
                if(!OutdoorSafe){Say("Outdoor work is closed. "+WeatherText);return;}
                if(items.Any(i=>i.storedIn=="tool")){Say("Empty the tongs into the cleanup bin first.");return;}
                if(Vector3.Distance(held.WorldBounds().center,target.WorldBounds().center)>.55f){Say("Bring the tongs into contact with the litter.");return;}
                target.storedIn="tool";target.SetPhysics(false);target.transform.SetParent(held.transform,true);target.transform.localPosition=Vector3.zero;
                Say("Litter collected with the tongs. Use the cleanup bin to release it.");return;
            }
            if((target.kind==PrepKind.Radio||target.kind==PrepKind.Flashlight)&&held&&held.kind==PrepKind.Batteries)
            {
                if(target.cells>=2){Say("This device already has batteries.");return;}
                if(held.cells<2){Say("This organizer has no spare pair remaining.");return;}
                held.cells-=2;target.cells=2;Say("Matching batteries fitted. Use the device to test it before packing.");return;
            }
            switch(target.kind)
            {
                case PrepKind.Flashlight:case PrepKind.Radio:
                    if(target.cells<2){Say("Hold a battery organizer and use this device to fit a pair first.");return;}
                    target.prepared=true;Say(target.kind==PrepKind.Radio?"Radio test received. "+WarningStageText:"Flashlight tested. Pack it in the open go-bag.");break;
                case PrepKind.Documents:if(target.closed&&!ConfirmUndo(target,"Reopening removes the pouch's rain protection."))return;target.closed=!target.closed;target.prepared=target.closed;Say(target.closed?"Document pouch sealed. Pack it in the go-bag.":"Pouch reopened; seal it again before packing.");break;
                case PrepKind.FirstAid:target.prepared=true;target.closed=!target.closed;Say(target.closed?"First-aid contents checked and case closed.":"Case open: dressings and supplies inspected. Close before packing.");break;
                case PrepKind.Medicine:target.prepared=true;Say("Matched to the fictional household care card. Pack the labelled medicine supply.");break;
                case PrepKind.Electronics:if(!OutdoorSafe){Say("Conditions are worsening. Leave electrical equipment and prepare to depart.");return;}if(!target.plugged&&!ConfirmUndo(target,"Reconnecting undoes safe disconnection."))return;target.plugged=!target.plugged;Say(target.plugged?"Device reconnected.":"Device safely disconnected in dry conditions. Move it to the high shelf.");break;
                case PrepKind.Bag:target.closed=!target.closed;Say(target.closed?"Go-bag zipped closed. Keep it ready to carry to the exit.":"Bag open. E checks and packs a held supply. In VR, release it into the opening.");break;
                case PrepKind.StorageDoor:target.closed=!target.closed;if(!target.closed)tutorialProgress|=1;Say(target.closed?"Storage closed.":"Storage open. Look at your bag or a supply and press E to pick it up.");break;
                case PrepKind.Window:case PrepKind.Door:
                    if(target.closed){if(target.kind==PrepKind.Window&&target.prepared&&!ConfirmUndo(target,"Opening this window removes its rain protection."))return;target.closed=false;target.prepared=false;Say("Opened. Close again to check the latch.");}
                    else if(!target.prepared){target.closed=true;target.prepared=true;Say("Closed and latch checked. Exit remains operable from inside.");}break;
                case PrepKind.Plan:
                    if(target.id=="plan-primary")primaryRoute=true;else backupRoute=true;
                    Say(RouteDescription(target.id=="plan-primary"));player.ToggleGuide();break;
                case PrepKind.Bed:if(evacuationOrdered){Finish(false);return;}player.OpenDepartureReview(false);return;
                case PrepKind.Exit:if(evacuationOrdered){Finish(true);return;}player.OpenDepartureReview(true);return;
                default:Say(Describe(target));break;
            }
            if(target.prepared&&(target.kind==PrepKind.Flashlight||target.kind==PrepKind.Radio||target.kind==PrepKind.Documents||target.kind==PrepKind.FirstAid||target.kind==PrepKind.Medicine))tutorialProgress|=4;
            target.AnimateVisual();RefreshEvidence();
        }
        public bool Store(PreparednessItem item,PreparednessItem zone)
        {
            if(!CanPrepare||!item||!zone||!item.held){Say("Pick up an appropriate item first.");return false;}
            if(zone.kind==PrepKind.BagZone)
            {if(Vector3.Distance(player.view.transform.position,Bag.WorldBounds().ClosestPoint(player.view.transform.position))>player.reach){Say("Move within arm's reach of the bag, then press E or P to pack.");return false;}}
            else if(Vector3.Distance(player.view.transform.position,zone.GetComponent<Collider>()?zone.GetComponent<Collider>().bounds.ClosestPoint(player.view.transform.position):zone.transform.position)>player.reach){Say("Move closer to the storage surface, then place the item with E.");return false;}
            if(zone.kind==PrepKind.BinZone&&item.kind==PrepKind.Tool)
            {
                if(!OutdoorSafe)return false;
                foreach(var litter in items.Where(i=>i.storedIn=="tool").ToArray())
                {litter.transform.SetParent(transform,true);litter.transform.position=zone.transform.position;litter.storedIn="bin";litter.SetPhysics(false);litter.gameObject.SetActive(false);}
                Say("Litter released into the cleanup bin.");RefreshEvidence();return true;
            }
            string destination="";bool accepted=false;
            switch(zone.kind)
            {
                case PrepKind.BagZone:
                    if(Bag.closed){Say("Open the go-bag before packing.");return false;}
                    if(Bag.storageDoor&&Bag.storageDoor.closed){Say("Open the closet and retrieve the bag first.");return false;}
                    accepted=CanPackSupply(item);
                    if(StormSim.Sibucao.SibucaoEvacuationExperience.IsSpecialist(item.id)&&items.Count(i=>i.IsPacked&&StormSim.Sibucao.SibucaoEvacuationExperience.IsSpecialist(i.id))>=StormSim.Sibucao.SibucaoEvacuationExperience.SpecialistLimit)
                    {Say("Three specialist tools fit in the quick-access kit. Unpack one to swap. Water, food, medicine and documents use the regular bag allowance.");return false;}
                    destination="bag";
                    if(BagMass+item.mass>BagLimit){Say(PlacementHint(item,zone));return false;}break;
                case PrepKind.SupplyZone:accepted=item.kind==PrepKind.Water||item.kind==PrepKind.Food;destination="supplies";break;
                case PrepKind.HighShelf:accepted=item.kind==PrepKind.Electronics||item.kind==PrepKind.Valuables;destination="high-shelf";
                    if(item.kind==PrepKind.Electronics&&item.plugged){Say("Disconnect the appliance while conditions are dry before moving it.");return false;}break;
                case PrepKind.IndoorZone:accepted=item.kind==PrepKind.Outdoor||item.kind==PrepKind.Bag;destination="indoors";break;
                case PrepKind.BinZone:accepted=item.kind==PrepKind.Litter&&OutdoorSafe;destination="bin";break;
                case PrepKind.ChargeZone:accepted=item.kind==PrepKind.Phone||item.kind==PrepKind.PowerBank;destination="charger";break;
                case PrepKind.TableZone:return PlaceOnTable(item,zone);
            }
            if(!accepted){Say(PlacementHint(item,zone));return false;}
            Vector3 storagePosition=zone.transform.position;Quaternion storageRotation=zone.transform.rotation;
            if(destination!="bag"&&destination!="bin"&&!PreparednessSurfacePlacement.TryPlace(this,item,zone,true,out storagePosition,out storageRotation))
            {Say("There is no clear space here. Move an item before adding another.");return false;}
            if(item==player.carriedBag)player.ReleaseBag();else player.Release();item.storedIn=destination;item.SetPhysics(false);
            if(destination=="bag")
            {
                item.transform.SetParent(Bag.transform,true);item.transform.localPosition=Vector3.zero;
                item.gameObject.SetActive(false);
            }
            else
            {
                item.transform.SetParent(transform,true);
                item.transform.SetPositionAndRotation(storagePosition,storageRotation);
            }
            item.SynchronizePose();
            if(destination=="bag"){tutorialProgress|=8;var visual=Bag.GetComponent<PreparednessBagVisual>();if(visual)visual.ConfirmPacked();}
            string feedback=destination=="bag"?PreparationNarration.PackedItem(item):destination=="charger"?"Charging started (time compressed). 80% is the scenario readiness target; a partial charge is still useful. Never delay evacuation for charging.":item.displayName+" placed safely.";
            if(destination=="bag")SpeakCue("item.packed."+item.id,"Household guide",feedback);else Say(feedback);
            PreparationFeedback.Confirm(destination=="bag");RefreshEvidence();return true;
        }
        public bool PackHeld()
        {
            if(!player.held||player.held==Bag){Say("Pick up a supply with E, then check and pack it with E. P is the optional packing shortcut.");return false;}
            // The shortcut still respects solid walls and closed furniture.
            var delta=Bag.WorldBounds().center-player.view.transform.position;
            foreach(var hit in Physics.RaycastAll(player.view.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                var item=hit.collider.GetComponentInParent<PreparednessItem>();
                if(hit.transform.IsChildOf(player.transform)||item==Bag||item==player.held)continue;
                if(!item||item.kind==PrepKind.StorageDoor){Say("Move where you can reach the bag without a wall or closed door between you.");return false;}
            }
            return Store(player.held,items.First(i=>i.kind==PrepKind.BagZone));
        }
        public void Unpack(){Unpack(items.LastOrDefault(i=>i.IsPacked));}
        public bool Unpack(PreparednessItem item)
        {
            if(!CanPrepare||!Bag||Bag.closed||player.held){Say("Open the bag and free your hand to retrieve an item.");return false;}
            if(player.carriedBag!=Bag&&Vector3.Distance(player.view.transform.position,Bag.WorldBounds().ClosestPoint(player.view.transform.position))>player.reach){Say("Move closer to the bag.");return false;}
            if(!item||!items.Contains(item)||!item.IsPacked){Say("That item is no longer in the bag.");return false;}
            item.gameObject.SetActive(true);item.transform.SetParent(transform,true);item.transform.position=Bag.transform.position+Vector3.up*.6f;
            if(!player.Grab(item)){item.transform.SetParent(Bag.transform,true);item.gameObject.SetActive(false);return false;}
            RefreshEvidence();Say(item.displayName+" retrieved. "+ItemReadiness(item));return true;
        }
        public bool DropFromBag(PreparednessItem item)
        {
            if(!CanPrepare||!Bag||Bag.closed){Say("Open the bag before removing an item.");return false;}
            if(player.held){Say("Put down the item in your hand first.");return false;}
            if(player.carriedBag!=Bag&&Vector3.Distance(player.view.transform.position,Bag.WorldBounds().ClosestPoint(player.view.transform.position))>player.reach){Say("Move closer to the bag.");return false;}
            if(!item||!items.Contains(item)||!item.IsPacked){Say("That item is no longer in the bag.");return false;}

            var bounds=item.WorldBounds();
            var localCenter=item.transform.InverseTransformPoint(bounds.center);
            var extents=Vector3.Max(bounds.extents,new Vector3(.08f,.05f,.08f));
            var origin=player.transform.position+Vector3.up*Mathf.Max(.35f,extents.y+.08f);
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=player.transform.forward;
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            Vector3 chosen=origin-forward*.9f;bool found=false;
            foreach(var direction in new[]{forward,right,-right,-forward})
            {
                var candidate=origin+direction*Mathf.Max(1.0f,extents.magnitude+.55f);
                if(Physics.Raycast(candidate+Vector3.up*1.2f,Vector3.down,out var ground,2.5f,~0,QueryTriggerInteraction.Ignore))
                    candidate.y=ground.point.y+extents.y+.025f;
                var center=candidate+item.transform.rotation*localCenter;
                bool blocked=Physics.OverlapBox(center,extents*.9f,item.transform.rotation,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>c&&!c.transform.IsChildOf(player.transform)&&!c.transform.IsChildOf(item.transform));
                if(!blocked){chosen=candidate;found=true;break;}
            }
            if(!found){Say("There is no clear space beside you. Move away from furniture and try again.");return false;}

            // Change the single authoritative item's state before enabling it. This
            // prevents a second click or stale UI event from creating duplicates.
            item.storedIn="";item.transform.SetParent(transform,true);item.transform.position=chosen;
            item.gameObject.SetActive(true);item.SynchronizePose();item.ResolveReleaseOverlap(player.transform);item.SetPhysics(true);
            RefreshEvidence();Say(item.displayName+" placed safely nearby. Pick it up to pack it again.");return true;
        }
        public void Finish(bool evacuate)
        {TryFinish(evacuate);}
        public bool TryFinish(bool evacuate,bool confirmed=false)
        {
            if(!CanPrepare){Say("Finish the weather bulletin before confirming departure.");return false;}
            // The evacuation instruction itself is sufficient: never make someone return
            // to the television while an order is active.
            if(!advisoryRead&&!evacuationOrdered){Say("Review and acknowledge the weather advisory below before departing.");return false;}
            if(!evacuate&&evacuationOrdered){Say("An evacuation instruction is active. Leave now; resting is unavailable.");return false;}
            if(!evacuate&&player.held){Say("Put down the item you are holding before resting.");return false;}
            if(!evacuate&&!sleepConfirmationPending&&!confirmed){sleepConfirmationPending=true;Say(items.Count(i=>i.IsPacked)+" items packed. Rest skips to tomorrow's evacuation advisory. Use the bed again to rest, or use the exit to leave early.");return false;}
            sleepConfirmationPending=false;
            if(!evacuate&&!PreparationContinuity.StageBag(this))return false;
            departure=evacuate?(phase==PrepPhase.MorningPacking?"morning-departure":evacuationOrdered?"ordered-evacuation":"early-evacuation"):"overnight";
            if(evacuate&&evacuationOrdered)advisoryRead=true;
            RefreshEvidence();SetPhase(PrepPhase.Fade);Save();
            return true;
        }
        public void AdvancePreparation(float seconds)
        {
            if(!CanPrepare||evacuationOrdered||seconds<=0)return;
            preparationSeconds+=seconds;
            if(preparationSeconds>=Mathf.Max(1,evacuationAfterSeconds))IssueEvacuation();
        }
        public void IssueEvacuation(){if(evacuationOrdered)return;evacuationOrdered=true;advisoryRead=false;sleepConfirmationPending=false;SpeakCue("advisory.evacuate","Television advisory",PreparationNarration.EvacuationAdvisory,PreparationVoicePriority.Emergency);PreparationFeedback.Warning();ApplyPhase();}
        bool PlaceOnTable(PreparednessItem item,PreparednessItem zone)
        {
            if(!item.portable||item.kind==PrepKind.Litter||item.kind==PrepKind.Water&&item.id!="travel-water"){Say("Keep bulky household water and litter away from the packing table.");return false;}
            if(!PreparednessSurfacePlacement.TryPlace(this,item,zone,true,out var position,out var rotation))
            {Say("There is something in that spot. Choose a clear space on the table.");return false;}
            if(item==player.carriedBag)player.ReleaseBag();else player.Release();item.transform.SetPositionAndRotation(position,rotation);item.storedIn="table";item.gathered=item.kind!=PrepKind.Bag;item.SetPhysics(false);item.SynchronizePose();
            Say("Placed on the dining table. It will stay here overnight until you pick it up.");RefreshEvidence();return true;
        }
        void DetectTableDrops()
        {
            if(!CanPrepare)return;
            var table=items.FirstOrDefault(i=>i.kind==PrepKind.TableZone);if(!table)return;
            var shape=table.GetComponent<BoxCollider>();var area=new Bounds(shape.center,shape.size);
            foreach(var item in items.Where(i=>i.portable&&!i.held&&!i.IsPacked&&i.Body&&!i.Body.isKinematic))
            {
                var b=PreparednessSurfacePlacement.BoundsIn(item,table.transform);
                if(b.min.x>=area.min.x&&b.max.x<=area.max.x&&b.min.z>=area.min.z&&b.max.z<=area.max.z&&Mathf.Abs(b.min.y)<.08f&&item.Body.linearVelocity.sqrMagnitude<.01f)
                {item.gathered=item.kind!=PrepKind.Bag;item.storedIn="table";item.SetPhysics(false);item.SynchronizePose();}
            }
        }
        bool Packed(PrepKind kind,Func<PreparednessItem,bool> extra=null)=>items.Any(i=>i.kind==kind&&i.IsPacked&&(extra==null||extra(i)));
        public void RefreshEvidence()
        {
            if(items==null)return;
            float water=HomeWater;
            int food=items.Count(i=>i.kind==PrepKind.Food&&(i.storedIn=="supplies"||i.IsPacked));
            decisions[0]=water>=45.4f;evidence[0]=$"{water:0.#} / 45.4 litres stored for four people.";
            decisions[1]=food>=3;evidence[1]=$"{food} / 3 household-day sets stored: each set represents ready-to-eat food for four people for one day.";
            decisions[2]=Packed(PrepKind.Flashlight,i=>i.prepared&&i.cells>=2);
            decisions[3]=Packed(PrepKind.Radio,i=>i.prepared&&i.cells>=2);
            decisions[4]=Packed(PrepKind.Batteries,i=>i.cells>=2);
            decisions[5]=Packed(PrepKind.FirstAid,i=>i.prepared&&i.closed);
            decisions[6]=Packed(PrepKind.Medicine,i=>i.prepared);
            decisions[7]=Packed(PrepKind.Documents,i=>i.closed);
            decisions[8]=Packed(PrepKind.Phone,i=>i.charge>=UsefulCharge);
            decisions[9]=Packed(PrepKind.PowerBank,i=>i.charge>=UsefulCharge)&&Packed(PrepKind.Cable);
            decisions[10]=Packed(PrepKind.Hygiene)&&items.Any(i=>i.id=="raincoat"&&i.IsPacked);
            decisions[11]=Bag&&Bag.closed&&(Bag.storedIn=="indoors"||Bag.storedIn=="table"||Bag.held)&&items.Count(i=>i.IsPacked)>=1;
            decisions[12]=items.Any(i=>i.kind==PrepKind.Electronics&&!i.plugged&&i.storedIn=="high-shelf");
            decisions[13]=items.Any(i=>i.kind==PrepKind.Valuables&&i.storedIn=="high-shelf");
            decisions[14]=items.Any(i=>i.kind==PrepKind.Window&&i.closed&&i.prepared);
            var game=StormSim.Sibucao.SibucaoSession.Instance;
            var bridge=game?game.GetComponent<SibucaoPreparationBridge>():null;
            decisions[15]=game&&game.GetComponent<StormSim.Sibucao.CoastalAreaProfile>()&&bridge ? bridge.houseDoors[System.Array.IndexOf(game.homes,bridge.firstHome)].prepared : items.Any(i=>i.id=="door"&&i.prepared);
            decisions[16]=items.Any(i=>i.kind==PrepKind.Outdoor)&&items.Where(i=>i.kind==PrepKind.Outdoor).All(i=>i.storedIn=="indoors");
            decisions[17]=items.Any(i=>i.kind==PrepKind.Litter)&&items.Where(i=>i.kind==PrepKind.Litter).All(i=>i.storedIn=="bin");
            decisions[18]=primaryRoute&&backupRoute;
            decisions[19]=advisoryRead&&(departure=="early-evacuation"||departure=="ordered-evacuation"||departure=="morning-departure");
            UpdateDetailedEvidence();
            evidence[11]=Bag?(Bag.held?"Bag carried. ":"Bag: "+LocationOf(Bag)+". ")+(Bag.closed?"Closed.":"Open — close it before leaving."):"Find your go-bag in the household closet.";
            evidence[19]="Departure branch: "+departure+". Advisory checked: "+advisoryRead;
        }
        public string Describe(PreparednessItem i)
        {
            if(!i)return "";
            if(i.kind==PrepKind.Phone||i.kind==PrepKind.PowerBank)return i.displayName+" — "+i.charge.ToString("0")+"% charged";
            if(i.kind==PrepKind.Batteries)return i.displayName+" — "+i.cells+" cells remaining";
            if(i.kind==PrepKind.Bag)return "Go-bag: "+(i.closed?"CLOSED — E to open":"OPEN — hold a supply, E or P to pack")+" | "+items.Count(x=>x.IsPacked)+" items";
            return i.displayName;
        }
        public string Journal()=>PosterSupply.Summary(this)+"\n\n"+PlanSummary+"\n\n"+string.Join("\n\n",Enumerable.Range(0,GroupNames.Length).Select(g=>GroupHeading(g)+"\n"+GroupDetails(g)));
        public PrepSave Capture()
        {
            var game=StormSim.Sibucao.SibucaoSession.Instance;
            RefreshEvidence();return new PrepSave{chapter=chapter?chapter.Capture():null,health=game?game.state.physical.health:100,stamina=game?game.state.physical.stamina:100,hunger=game?game.state.physical.hunger:0,runId=runId,departure=departure,bagRetrieved=bagRetrieved,bagInRightHand=player&&player.carriedBag,heldId=player&&player.held?player.held.id:"",phase=phase,elapsed=elapsed,phaseTime=phaseTime,preparationSeconds=preparationSeconds,tutorialProgress=tutorialProgress,guidanceUsed=guidanceUsed,coachingMode=Guided?1:2,layoutId=gameObject.scene.name,homeIndex=game&&game.homes!=null?Array.IndexOf(game.homes,game.home):-1,toggleWork=toggleWork,work=items.Where(i=>i.GetComponent<PreparednessHouseTask>()).Select(i=>new PrepWorkSave{id=i.id,seconds=i.GetComponent<PreparednessHouseTask>().ProgressSeconds}).ToArray(),evacuationOrdered=evacuationOrdered,advisoryRead=advisoryRead,primaryRoute=primaryRoute,backupRoute=backupRoute,playerPosition=player?player.transform.position:Vector3.zero,playerRotation=player?player.transform.rotation:Quaternion.identity,decisions=(bool[])decisions.Clone(),evidence=(string[])evidence.Clone(),items=items.Select(i=>new PrepItemSave{id=i.id,storedIn=i.storedIn,position=i.transform.position,rotation=i.transform.rotation,gathered=i.gathered,storageDoor=i.storageDoor?i.storageDoor.id:"",prepared=i.prepared,closed=i.closed,plugged=i.plugged,cells=i.cells,charge=i.charge}).ToArray()};
        }
        public void Save()
        {
            WritePreparationSave(true);
        }
        public bool Restore(PrepSave save)
        {
            if(!ValidPreparationSave(save))return false;
            player.ReleaseAll();sleepConfirmationPending=false;undoId="";RestoreCoaching(save.coachingMode);toggleWork=save.toggleWork;
            var game=StormSim.Sibucao.SibucaoSession.Instance;
            if(game&&game.Mode==StormSim.Sibucao.SibucaoMode.Preparation){game.state.physical.health=save.health;game.state.physical.stamina=save.stamina;game.state.physical.hunger=save.hunger;}
            foreach(var i in items){i.gameObject.SetActive(true);if(i.portable)i.transform.SetParent(transform,true);}
            foreach(var data in MigratedItems(save))
            {
                var i=items.First(x=>x.id==data.id);i.transform.SetPositionAndRotation(data.position,data.rotation);i.storedIn=data.storedIn;i.gathered=data.gathered;i.storageDoor=items.FirstOrDefault(x=>x.id==data.storageDoor);i.prepared=data.prepared;i.closed=data.closed;i.plugged=data.plugged;i.cells=data.cells;i.charge=data.charge;i.held=false;i.SetPhysics(false);i.ApplyVisual();
            }
            foreach(var i in items.Where(i=>i.IsPacked)){i.transform.SetParent(Bag.transform,true);i.gameObject.SetActive(false);}
            foreach(var i in items.Where(i=>i.storedIn=="installed"||i.storedIn=="bin"||i.storedIn=="worn"))i.gameObject.SetActive(false);
            var bagZone=items.FirstOrDefault(i=>i.kind==PrepKind.BagZone);if(bagZone){bagZone.transform.SetParent(Bag.transform,true);bagZone.transform.localPosition=Vector3.zero;}
            preparationSeconds=save.preparationSeconds;tutorialProgress=save.tutorialProgress;guidanceUsed=save.guidanceUsed;runId=save.runId;bagRetrieved=save.bagRetrieved;departure=save.departure;phase=save.phase;elapsed=save.elapsed;phaseTime=save.phaseTime;evacuationOrdered=save.evacuationOrdered;advisoryRead=save.advisoryRead;primaryRoute=save.primaryRoute;backupRoute=save.backupRoute;
            player.Warp(save.playerPosition,save.playerRotation);
            if(save.bagInRightHand)player.GrabBag(Bag,true);
            if(!string.IsNullOrEmpty(save.heldId))player.Grab(items.FirstOrDefault(i=>i.id==save.heldId),true);
            var tool=items.FirstOrDefault(i=>i.kind==PrepKind.Tool);if(tool)foreach(var i in items.Where(i=>i.storedIn=="tool"))i.transform.SetParent(tool.transform,true);
            foreach(var item in items)if(item.portable)item.SynchronizePose();
            if(game&&game.Mode==StormSim.Sibucao.SibucaoMode.Preparation)PreparationExposure.RestorePresentation();
            foreach(var item in items){var task=item.GetComponent<PreparednessHouseTask>();if(task)task.RestoreProgress(save.work?.FirstOrDefault(w=>w.id==item.id)?.seconds??0);}
            if(chapter)chapter.Restore(save.chapter);
            nextPreparationCheckpoint=preparationSeconds+45;lastWeather=Weather;player.CloseDesktopPanels();RefreshEvidence();ApplyPhase();return true;
        }
        public void Load()
        {
            foreach(var path in new[]{SavePath,SavePath+".bak"})
            {
                try{if(File.Exists(path)&&RestoreCheckpoint(JsonUtility.FromJson<PrepSave>(File.ReadAllText(path)))){Say(path.EndsWith(".bak")?"Preparation restored from backup.":"Preparation restored.");return;}}
                catch(Exception){ /* Try the previous complete checkpoint before reporting failure. */ }
            }
            Say("No compatible preparation save or backup found. Your current run was not changed.");
        }
        public void Restart(){if(initial!=null){Restore(initial);runId=Guid.NewGuid().ToString("N");Say("New preparation run.");}}
    }
}
