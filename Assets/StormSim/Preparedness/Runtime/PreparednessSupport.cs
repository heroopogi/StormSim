using System;
using System.IO;
using System.Linq;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public enum PreparationWeather { Calm, WindRising, UnsafeOutside, Evacuate }
    [Serializable] public sealed class PrepWorkSave { public string id; public float seconds; }

    public sealed partial class PreparednessSession
    {
        public const float BagLimit=8, UsefulCharge=80;
        public const string HouseholdCard="HOUSEHOLD OF FOUR\nYou: carry the go-bag and keep the household together.\nAdult companion: carry household travel water and help the older adult.\nOlder adult: needs walking support and their labelled medicine.\nChild: stays with an adult; never returns alone for belongings.\nContact: use the phone to report the group's location to the school reception team. If separated, tell a route volunteer; reunite at school reception. This is a fictional training household, not a real contact list.";
        public const string QuantityGuide="Home reserve practice: 4 people × 3 days × about 3.8 L = 45.4 L; ten 5 L containers provide 50 L. This is a short training baseline, not a complete home emergency supply.\nThree food packs represent three household-day sets in this scenario; actual nutrition and portions are not simulated.\nThe small 1 L travel bottle belongs in your bag. It is only a demonstration share; the other adult carries additional travel supplies. One bottle is not the household's full requirement.\nBag limit: 8 kg in this scenario. Real carrying needs and abilities vary.";
        public const string BulletinText="A strong typhoon may bring heavy rain, wind and flooding. Prepare essential supplies, plan how the household will leave, and follow evacuation advisories. Property tasks are optional. You can leave early after checking this update.";
        public PreparationWeather Weather=>evacuationOrdered?PreparationWeather.Evacuate:preparationSeconds>=evacuationAfterSeconds*.75f?PreparationWeather.UnsafeOutside:preparationSeconds>=evacuationAfterSeconds*.5f?PreparationWeather.WindRising:PreparationWeather.Calm;
        public bool ElevatedWorkSafe=>CanPrepare&&Weather==PreparationWeather.Calm;
        public string WeatherText=>Weather==PreparationWeather.Calm?"Calm and dry — optional ground-level work only while safe.":Weather==PreparationWeather.WindRising?"Wind rising — stop elevated work; prepare to leave.":Weather==PreparationWeather.UnsafeOutside?"Wind and rain increasing — stop outdoor work and return inside.":"Evacuation advised — leave now. Do not finish chores.";
        public float BagMass=>items.Where(i=>i.IsPacked).Sum(i=>i.mass);
        public float HomeWater=>items.Where(i=>i.kind==PrepKind.Water&&i.id!="travel-water"&&i.storedIn=="supplies").Sum(i=>i.litres);
        public bool TravelWaterPacked=>items.Any(i=>i.id=="travel-water"&&i.IsPacked);
        public string CapacityText=>$"Go-bag: {BagMass:0.0} / {BagLimit:0.0} kg (scenario limit)";
        public string CheckpointText {get;private set;}="No preparation checkpoint yet";
        public bool HasPreparationSave=>File.Exists(SavePath)||File.Exists(SavePath+".bak");
        float nextPreparationCheckpoint=45;
        PreparationWeather lastWeather;
        bool improvementsReady;
        string undoId="";float undoUntil;
        public string RouteDescription(bool primary)
        {
            return primary?"PRIMARY: Follow the marked inland evacuation route to Banago Elementary School I. At each junction, follow the route signs and rejoin the marked route after a passage. Meet your household at school reception."
                : "BACKUP: If a center passage is unsafe, use a marked side passage at that junction and rejoin the same school route. Do not enter floodwater to find an unmarked shortcut. If neither passage is safe, stay at a safe point and seek help. Both options lead to school reception; this game does not model a second shelter.";
        }
        public void EnsureImprovements()
        {
            if(improvementsReady||items==null)return;improvementsReady=true;
            peacefulSeconds=Mathf.Min(peacefulSeconds,5);bulletinSeconds=Mathf.Min(bulletinSeconds,12);
            foreach(var item in items)
            {
                if(item.kind==PrepKind.Water)item.displayName="Household water reserve (5 L)";
                if(item.kind==PrepKind.Food)item.displayName="Household-day food set (scenario portion)";
                if(item.id=="plan-primary")item.displayName="Primary plan — school route";
                if(item.id=="plan-backup")item.displayName="Backup plan — marked side passages";
            }
            if(!items.Any(i=>i.id=="travel-water"))
            {
                var source=items.FirstOrDefault(i=>i.kind==PrepKind.Water);
                if(source)
                {
                    var bottle=Instantiate(source,transform);bottle.name="Travel drinking water — 1 L";bottle.id="travel-water";bottle.displayName="Travel drinking water (1 L)";
                    bottle.litres=1;bottle.mass=1.1f;bottle.storedIn="";bottle.held=false;bottle.storageDoor=null;bottle.transform.localScale*=.55f;
                    bottle.transform.position=source.transform.position+transform.right*.55f;
                    if(bottle.Body)bottle.Body.mass=bottle.mass;
                    var physical=bottle.GetComponent<PhysicalGrabbable>();if(physical){physical.persistentId="prep:travel-water";physical.kind=PhysicalItemKind.Supply;physical.inventoryTag="water";physical.secured=false;physical.delivered=false;}
                    items=items.Concat(new[]{bottle}).ToArray();
                    var shelf=items.FirstOrDefault(i=>i.kind==PrepKind.TableZone);
                    if(shelf&&PreparednessSurfacePlacement.TryPlace(this,bottle,shelf,true,out var p,out var r)){bottle.transform.SetPositionAndRotation(p,r);bottle.storedIn="table";}
                    bottle.SetPhysics(false);bottle.SynchronizePose();
                }
            }
            if(player&&!player.GetComponent<PreparednessFocusFeedback>())player.gameObject.AddComponent<PreparednessFocusFeedback>().player=player;
            if(SibucaoSession.Instance&&SibucaoSession.Instance.inventory)SibucaoSession.Instance.inventory.RefreshPreparationItems();
            if(SibucaoSession.Instance&&SibucaoSession.Instance.physicalDecisions)
                foreach(var item in items)SibucaoSession.Instance.physicalDecisions.RegisterPreparationItem(item.GetComponent<PhysicalGrabbable>());
            if(SibucaoSession.Instance&&!SibucaoSession.Instance.GetComponent<PreparationExposure>())SibucaoSession.Instance.gameObject.AddComponent<PreparationExposure>();
        }
        public string ItemReadiness(PreparednessItem item)
        {
            if(!item)return "Missing item";
            switch(item.kind)
            {
                case PrepKind.Flashlight:case PrepKind.Radio:return item.cells<2?"Needs a pair of batteries":item.prepared?"Tested; ready":"Batteries fitted; test before packing";
                case PrepKind.FirstAid:return !item.prepared?"Inspect contents":!item.closed?"Checked; close the case":"Checked and closed";
                case PrepKind.Documents:return item.closed?"Sealed against rain":"Unsealed — seal before packing";
                case PrepKind.Medicine:return item.prepared?"Checked against household care card":"Check household care card";
                case PrepKind.Batteries:return item.cells>=2?item.cells+" spare cells":"Empty organizer";
                case PrepKind.Phone:case PrepKind.PowerBank:return $"{item.charge:0}% — "+(item.charge>=99.9f?"fully charged":item.charge>=UsefulCharge?"useful charge; top up if safe":"partial charge; top up if time allows")+(item.storedIn=="charger"?" · charging (time compressed)":"");
                case PrepKind.Water:return item.id=="travel-water"?"Sealed travel bottle; 1 L demonstration share":"Home reserve; not for this go-bag";
                default:return CanPackSupply(item)?"Ready to pack":"Household item — use its matching storage";
            }
        }
        public string LocationOf(PreparednessItem item)
        {
            if(!item)return "not found";
            if(item.IsPacked)return "in your go-bag";
            if(item.held)return "in your hand";
            if(item.storageDoor)return "in "+item.storageDoor.displayName;
            switch(item.storedIn){case "charger":return "on the charging pad";case "table":return "on the packing table";case "supplies":return "on the household supply shelf";case "high-shelf":return "on the high shelf";case "indoors":return "in indoor storage";default:return "in the home or nearby storage";}
        }
        string SupplyEvidence(PrepKind kind)
        {
            var item=items.FirstOrDefault(i=>i.kind==kind);return item?item.displayName+": "+ItemReadiness(item)+"; "+LocationOf(item)+".":"This supply is missing from the household.";
        }
        public string PlacementHint(PreparednessItem item,PreparednessItem zone)
        {
            if(!zone)return "Look at a storage surface.";
            if(!item)return zone.kind==PrepKind.SupplyZone?"E  Household helps store up to 3 nearby water containers":"Pick up an item to place here.";
            if(zone.kind==PrepKind.BagZone||zone==Bag)
            {
                if(!CanPackSupply(item))return "Home reserve / household object — choose its storage shelf.";
                if(Bag.closed)return "Open the go-bag first.";
                if(BagMass+item.mass>BagLimit)return $"No room by weight: {BagMass:0.0} + {item.mass:0.0} kg exceeds {BagLimit:0} kg.";
                return $"E  Pack {item.displayName} ({item.mass:0.0} kg)";
            }
            bool accepts=zone.kind==PrepKind.TableZone&&item.portable&&item.kind!=PrepKind.Litter&&(item.kind!=PrepKind.Water||item.id=="travel-water")
                ||zone.kind==PrepKind.SupplyZone&&(item.kind==PrepKind.Food||item.kind==PrepKind.Water)
                ||zone.kind==PrepKind.HighShelf&&(item.kind==PrepKind.Valuables||item.kind==PrepKind.Electronics&&!item.plugged)
                ||zone.kind==PrepKind.IndoorZone&&(item.kind==PrepKind.Outdoor||item.kind==PrepKind.Bag)
                ||zone.kind==PrepKind.ChargeZone&&(item.kind==PrepKind.Phone||item.kind==PrepKind.PowerBank)
                ||zone.kind==PrepKind.BinZone&&(item.kind==PrepKind.Litter||item.kind==PrepKind.Tool);
            if(!accepts)return "Not suitable here — "+(item.kind==PrepKind.Electronics&&item.plugged?"disconnect it first.":"choose the matching shelf or packing table.");
            if(zone.kind!=PrepKind.BinZone&&!PreparednessSurfacePlacement.TryPlace(this,item,zone,true,out _,out _))return "No clear space — move another item first.";
            return "E  Place "+item.displayName+" here";
        }
        public int StoreHouseholdWater(PreparednessItem zone)
        {
            if(!CanPrepare||evacuationOrdered||player.held||!zone||zone.kind!=PrepKind.SupplyZone||Vector3.Distance(player.view.transform.position,zone.WorldBounds().ClosestPoint(player.view.transform.position))>player.reach)return 0;
            int count=0;
            foreach(var item in items.Where(i=>i.kind==PrepKind.Water&&i.id!="travel-water"&&!i.held&&i.storedIn!="supplies"&&(!i.storageDoor||!i.storageDoor.closed)&&Vector3.Distance(i.transform.position,zone.transform.position)<6).ToArray())
            {
                if(count==3)break;
                if(!PreparednessSurfacePlacement.TryPlace(this,item,zone,true,out var position,out var rotation))continue;
                item.transform.SetPositionAndRotation(position,rotation);item.storedIn="supplies";item.storageDoor=null;item.SetPhysics(false);item.SynchronizePose();count++;
            }
            RefreshEvidence();Say(count>0?$"Household helped store {count} containers. {HomeWater:0.#} L at home. Carrying time is compressed; keep travel water separate.":"No nearby containers fit. Clear shelf space or move a container closer.");return count;
        }
        public bool ConfirmUndo(PreparednessItem item,string warning)
        {
            if(undoId==item.id&&Time.unscaledTime<undoUntil){undoId="";return true;}
            undoId=item.id;undoUntil=Time.unscaledTime+8;Say(warning+" Use it again within 8 seconds to confirm; Q cancels.");return false;
        }
        public string DepartureReview=>CapacityText+"\n\nPACKED\n"+string.Join("\n",items.Where(i=>i.IsPacked).Select(i=>i.displayName+" — "+ItemReadiness(i)))+
            "\n\nLEFT AT HOME\n"+string.Join("\n",items.Where(i=>CanPackSupply(i)&&!i.IsPacked).Select(i=>i.displayName+" — "+LocationOf(i)))+
            $"\nHome water: {HomeWater:0.#} L. Travel bottle packed: {(TravelWaterPacked?"yes":"no")}.\n\n"+HouseholdCard+"\n\n"+RouteDescription(true)+"\n"+RouteDescription(false)+"\n\n"+WeatherText+"\nUnfinished checks never prevent leaving.";
        void TickPreparationSupport()
        {
            if(Weather!=lastWeather)
            {
                lastWeather=Weather;Say(WeatherText);
                foreach(var task in GetComponentsInChildren<PreparednessHouseTask>(true))if(task.exterior&&!OutdoorSafe||task.requiresLadderHeight&&!ElevatedWorkSafe)task.Cancel();
            }
            Shader.SetGlobalFloat("_SibucaoHomeGust",Weather==PreparationWeather.Calm?0:Weather==PreparationWeather.WindRising?.25f:.6f);
            if(CanPrepare&&preparationSeconds>=nextPreparationCheckpoint){WritePreparationSave(false);nextPreparationCheckpoint=preparationSeconds+45;}
        }
        public void WritePreparationSave(bool announce)
        {
            try
            {
                var path=SavePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonUtility.ToJson(Capture(),true));
                if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
                CheckpointText="Preparation checkpoint saved";if(announce)Say("Preparation saved.");
            }
            catch(Exception ex){CheckpointText="Checkpoint failed; use Save in the guide to retry";if(announce)Say("Save failed: "+ex.Message);Debug.LogWarning("Preparation save failed: "+ex.Message);}
        }
        bool ValidPreparationSave(PrepSave save)
        {
            if(save==null||save.version!=2||save.items==null||!Enum.IsDefined(typeof(PrepPhase),save.phase)||!float.IsFinite(save.preparationSeconds)||save.preparationSeconds<0||!float.IsFinite(save.phaseTime)||!float.IsFinite(save.elapsed)||!Finite(save.playerPosition)||!Finite(save.playerRotation)||!float.IsFinite(save.health)||!float.IsFinite(save.stamina)||!float.IsFinite(save.hunger))return false;
            var game=SibucaoSession.Instance;
            if(!string.IsNullOrEmpty(save.layoutId)&&(save.layoutId!=gameObject.scene.name||game&&game.homes!=null&&(save.homeIndex<0||save.homeIndex>=game.homes.Length)))return false;
            if(save.items.Any(s=>s==null||!items.Any(i=>i.id==s.id)||!Finite(s.position)||!Finite(s.rotation)||!float.IsFinite(s.charge)||s.charge<0||s.charge>100||s.cells<0)||save.items.Select(i=>i.id).Distinct().Count()!=save.items.Length)return false;
            if(items.Any(i=>!save.items.Any(s=>s.id==i.id)&&(i.id!="travel-water"||initial?.items.FirstOrDefault(s=>s.id==i.id)==null)))return false;
            if(!string.IsNullOrEmpty(save.heldId)&&!save.items.Any(i=>i.id==save.heldId&&i.storedIn!="bag"))return false;
            return save.work==null||save.work.All(w=>w!=null&&float.IsFinite(w.seconds)&&w.seconds>=0&&items.Any(i=>i.id==w.id&&i.GetComponent<PreparednessHouseTask>()));
        }
        bool RestoreCheckpoint(PrepSave save)
        {
            if(!ValidPreparationSave(save))return false;
            var game=SibucaoSession.Instance;var profile=game?game.GetComponent<CoastalAreaProfile>():null;var bridge=game?game.GetComponent<SibucaoPreparationBridge>():null;
            if(profile&&bridge&&!string.IsNullOrEmpty(save.layoutId))
            {profile.SelectHome(bridge,save.homeIndex);game.home=bridge.firstHome;game.state.homeIndex=save.homeIndex;}
            return Restore(save);
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        static bool Finite(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w)&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.1f;
        PrepItemSave[] MigratedItems(PrepSave save)=>save.items.Concat(items.Where(i=>!save.items.Any(s=>s.id==i.id)).Select(i=>initial.items.First(s=>s.id==i.id))).ToArray();
        void UpdateDetailedEvidence()
        {
            evidence[0]=$"{HomeWater:0.#} / 45.4 L stored at home. Travel bottle: "+(TravelWaterPacked?"packed":"not packed");
            evidence[1]="Store or pack the three household-day food sets; these represent scenario portions, not measured meals.";
            var kinds=new[]{PrepKind.Flashlight,PrepKind.Radio,PrepKind.Batteries,PrepKind.FirstAid,PrepKind.Medicine,PrepKind.Documents,PrepKind.Phone};
            for(int n=0;n<kinds.Length;n++)evidence[n+2]=SupplyEvidence(kinds[n]);
            evidence[9]=SupplyEvidence(PrepKind.PowerBank)+" "+SupplyEvidence(PrepKind.Cable);
            evidence[10]=SupplyEvidence(PrepKind.Hygiene)+" Pack the folded raincoat too.";
            evidence[12]="Disconnect the household lamp while dry, then place it on the high shelf.";
            evidence[13]="Place the family keepsake on the high shelf.";
            evidence[14]="Close the window and check its latch; reopening removes this check.";
            evidence[15]="Check the front door; keep it usable from inside.";
            evidence[16]="Move loose outdoor belongings to the indoor storage area while safe.";
            evidence[17]="Use tongs to move dry surface litter into the cleanup bin, only while safe.";
            evidence[18]="Primary route: "+(primaryRoute?"reviewed":"read the primary card")+"; backup: "+(backupRoute?"reviewed":"read the backup card");
        }
    }
}
