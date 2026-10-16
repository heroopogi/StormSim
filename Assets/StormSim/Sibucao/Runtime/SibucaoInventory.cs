using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StormSim.Sibucao
{
    [DefaultExecutionOrder(-115)]
    public sealed partial class SibucaoInventory : MonoBehaviour
    {
        public const int Capacity=27;
        public bool IsOpen {get;private set;}
        int consumedFrame=-1;
        public bool BlocksInput=>IsOpen||consumedFrame==Time.frameCount;
        PhysicalGrabbable[] items;
        SibucaoSession S=>SibucaoSession.Instance;
        PhysicalRunData Data=>S.state.physical;
        public bool HasBag=>S&&!string.IsNullOrEmpty(Data.backpackId)&&Find(Data.backpackId)&&!Data.flags.Contains("lost:"+Data.backpackId);
        public int Selected {get=>Mathf.Clamp(Data.inventorySelection,0,Capacity-1);set=>Data.inventorySelection=Mathf.Clamp(value,0,Capacity-1);}
        public int Count {get {EnsureSlots();int n=0;foreach(var id in Data.inventorySlots)if(!string.IsNullOrEmpty(id))n++;return n;}}
        void Awake(){items=FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None);}
        public void RefreshPreparationItems(){items=FindObjectsByType<PhysicalGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None);}
        void EnsureSlots(){if(Data.inventorySlots==null||Data.inventorySlots.Length!=Capacity)Array.Resize(ref Data.inventorySlots,Capacity);}
        public PhysicalGrabbable Find(string id){if(string.IsNullOrEmpty(id)||items==null)return null;foreach(var item in items)if(item&&item.persistentId==id)return item;return null;}
        public PhysicalGrabbable Slot(int index){EnsureSlots();return index>=0&&index<Capacity?Find(Data.inventorySlots[index]):null;}
        public bool HasTag(string tag)
        {
            if(string.IsNullOrEmpty(tag))return true;EnsureSlots();
            foreach(var id in Data.inventorySlots){var item=Find(id);if(item&&item.inventoryTag==tag&&!item.delivered&&!Data.flags.Contains("lost:"+id)&&!Data.flags.Contains("consumed:"+id))return true;}
            if(items!=null)foreach(var item in items)if(item&&item.inventoryTag==tag&&item.preparedForUse&&item.gameObject.activeInHierarchy&&!item.delivered&&!Data.flags.Contains("consumed:"+item.persistentId)&&!Data.flags.Contains("lost:"+item.persistentId))return true;
            return false;
        }
        public string RemoveTag(string tag,bool lost)
        {
            EnsureSlots();int slot=-1;
            for(int i=0;i<Capacity;i++){var item=Slot(i);if(item&&(string.IsNullOrEmpty(tag)||item.inventoryTag==tag)){slot=i;break;}}
            if(slot<0&&lost)for(int i=0;i<Capacity;i++)if(Slot(i)){slot=i;break;}
            if(slot<0)return null;
            var removed=Slot(slot);Data.inventorySlots[slot]=null;
            if(removed.holder)removed.holder.Release();removed.delivered=true;
            if(lost)
            {
                removed.transform.SetParent(null,true);removed.transform.position=S.player.transform.position+S.player.transform.forward*.7f+Vector3.up;
                removed.gameObject.SetActive(true);removed.secured=false;removed.Body.isKinematic=false;removed.Body.linearVelocity=S.player.transform.forward*1.6f+S.player.transform.right*.6f;
                var loss=removed.GetComponent<SibucaoLostSupply>();if(!loss)loss=removed.gameObject.AddComponent<SibucaoLostSupply>();loss.Begin();
            }
            else removed.gameObject.SetActive(false);
            S.physicalDecisions.SetFlag((lost?"lost:":"consumed:")+removed.persistentId);
            if(removed.supplyBit>=0)S.state.homeSupplies&=~(1<<removed.supplyBit);
            S.Notify(ItemName(removed)+(lost?" lost":" consumed"),lost?"This item is no longer available. I / Tab > Item status shows your remaining supplies.":"This item has been used up. I / Tab > Item status keeps the record.");
            S.RequestCheckpoint();return removed.persistentId;
        }
        public static bool Storable(PhysicalGrabbable item)=>item&&(item.kind==PhysicalItemKind.Supply||item.kind==PhysicalItemKind.FirstAid||item.routinePickup&&item.kind==PhysicalItemKind.Object);
        public static string ItemName(PhysicalGrabbable item)
        {
            if(!item)return "Empty slot";
            if(item.kind==PhysicalItemKind.Bag)return "Go-bag";
            var preparation=item.GetComponent<StormSim.Preparedness.PreparednessItem>();
            return preparation&&!string.IsNullOrWhiteSpace(preparation.displayName)?preparation.displayName:item.name.Split(new[]{" - "},StringSplitOptions.None)[0];
        }
        public string PickupPrompt(PhysicalGrabbable item)
        {
            if(!item)return null;if(item.kind==PhysicalItemKind.Bag)return HasBag?"Go-bag already equipped":"E  Equip go-bag";
            if(item.kind==PhysicalItemKind.FirstAid&&item.preparedForUse)return "F  Carry the kit · E  Pack it away";
            return Storable(item)?HasBag?"F  Carry "+ItemName(item)+" / E  Pack into bag":"F  Carry "+ItemName(item)+" / collect a bag to pack":null;
        }
        public bool Collect(PhysicalGrabbable item)
        {
            if(!item||item.delivered||Data.flags.Contains("consumed:"+item.persistentId)||Data.flags.Contains("lost:"+item.persistentId))return false;
            if(item.kind==PhysicalItemKind.Bag)
            {
                if(HasBag){S.Notify("Bag equipped","You already have a go-bag.");return true;}
                if(Data.flags.Contains("broken_bag:"+item.persistentId)&&Data.results[4]<0)S.physicalDecisions.SetFlag("bag_carried_by_handle");
                Data.backpackId=item.persistentId;item.everHeld=true;item.secured=true;item.Body.isKinematic=true;
                if(item.holder)item.holder.Release();item.gameObject.SetActive(false);
                foreach(var cargo in item.GetComponentsInChildren<PhysicalGrabbable>(true))if(cargo!=item&&Storable(cargo)&&cargo.secured)Collect(cargo);
                S.Notify("Go-bag equipped","F carries supplies; P packs them. I / Tab opens your bag.");return true;
            }
            if(!Storable(item))return false;
            if(!HasBag){S.Notify("A bag is needed","Pick up your go-bag before collecting items.");return true;}
            EnsureSlots();int index=Array.IndexOf(Data.inventorySlots,item.persistentId);bool newlyCollected=index<0;
            if(newlyCollected&&Data.backpackId=="prep:bag")
            {
                float Weight(PhysicalGrabbable value){var prepItem=value.GetComponent<StormSim.Preparedness.PreparednessItem>();return prepItem?prepItem.mass:value.Body.mass;}
                float weight=Weight(item);foreach(var id in Data.inventorySlots){var existing=Find(id);if(existing)weight+=Weight(existing);}
                if(weight>8){S.Notify("Bag carrying limit","The go-bag holds 8 kg. Put down something before adding this item.");return true;}
            }
            if(index<0){for(int i=0;i<Capacity;i++)if(string.IsNullOrEmpty(Data.inventorySlots[i])){index=i;break;}}
            if(index<0){S.Notify("Bag is full","Open your inventory and drop an item to make room.");return true;}
            if(item.holder)item.holder.Release();Data.equippedInventoryId=null;
            if(item.station&&item.station.index==4)S.physicalDecisions.SetFlag("spill_recovered");
            if(item.station&&item.station.supplementary)
            {
                S.physicalDecisions.SetFlag("collected:"+item.persistentId);int found=0;
                foreach(var supply in items)if(supply&&supply.station==item.station&&Data.flags.Contains("collected:"+supply.persistentId))found++;
                if(found>=2)S.physicalDecisions.SetFlag("gathered_"+item.station.index);
            }
            Data.inventorySlots[index]=item.persistentId;item.preparedForUse=false;item.everHeld=true;item.secured=true;item.Body.isKinematic=true;item.gameObject.SetActive(false);
            var prep=item.GetComponent<StormSim.Preparedness.PreparednessItem>();if(prep)prep.storedIn="bag";
            if(newlyCollected)S.GetComponent<StormSim.Preparedness.PreparationContinuity>()?.RecordStormPickup(item);
            if(item.supplyBit>=0)
            {
                S.state.homeSupplies|=1<<item.supplyBit;string flag="packed_"+item.supplyBit;
                if(!Data.flags.Contains(flag)){S.state.scores[0]=Mathf.Min(1500,S.state.scores[0]+500);S.state.scores[2]=Mathf.Min(1000,S.state.scores[2]+150);S.physicalDecisions.SetFlag(flag);}
            }
            S.Notify(ItemName(item)+" stored","Packed in your go-bag. I / Tab shows your supplies.");S.RequestCheckpoint();
            return true;
        }
        public void Open()
        {
            if(!S||S.Mode!=SibucaoMode.Playing||S.home&&S.home.MovementLocked||S.missionGuide&&S.missionGuide.IsOpen)return;
            if(!HasBag)ShowStatus=true;
            IsOpen=true;consumedFrame=Time.frameCount;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void Close(){IsOpen=false;consumedFrame=Time.frameCount;if(S&&S.Mode==SibucaoMode.Playing)S.LockCursor();}
        public void EquipSelected()
        {
            var item=Slot(Selected);if(!item)return;
            var poster=item.GetComponent<StormSim.Preparedness.PosterSupply>();
            if(poster&&poster.supply==StormSim.Preparedness.PosterSupplyType.Whistle){poster.Signal();S.Notify("Whistle signal","Three short sounds. Keep listening for a response; signaling does not guarantee rescue.");return;}
            if(item.inventoryTag=="food")
            {
                if(Data.hunger<1){S.Notify("Not hungry","Keep this food for later.");return;}
                Data.inventorySlots[Selected]=null;item.delivered=true;item.gameObject.SetActive(false);
                S.physicalDecisions.SetFlag("consumed:"+item.persistentId);Data.hunger=Mathf.Max(0,Data.hunger-35);
                S.Notify(ItemName(item)+" consumed","Hunger reduced. This food pack has been used.");S.RequestCheckpoint();return;
            }
            Close();S.RouteChoices?.HeldBy?.Cancel();var grab=S.physicalDecisions.Grabber;
            if(grab.held&&Storable(grab.held))Collect(grab.held);
            grab.Release();
            item.transform.SetParent(null,true);item.transform.position=S.player.view.transform.TransformPoint(new Vector3(.18f,-.18f,.8f));
            item.preparedForUse=item.kind==PhysicalItemKind.FirstAid;
            item.gameObject.SetActive(true);item.secured=false;item.Body.isKinematic=false;Physics.SyncTransforms();
            Data.inventorySlots[Selected]=null;Data.equippedInventoryId=null;
            if(grab.HoldItem(item,true))Data.equippedInventoryId=item.persistentId;
            S.Notify(ItemName(item)+" taken out",item.IsHeld?"F places / releases; Q puts down; P packs it again.":"The item is within reach. F carries it; P packs it again.");
            S.RequestCheckpoint();
            if(item.supplyBit>=0)S.state.homeSupplies&=~(1<<item.supplyBit);
        }
        public void DropSelected()
        {
            var item=Slot(Selected);if(!item)return;
            Data.inventorySlots[Selected]=null;if(Data.equippedInventoryId==item.persistentId)Data.equippedInventoryId=null;
            if(item.holder)item.holder.Release();item.preparedForUse=false;item.transform.SetParent(null,true);
            // Drop within the same unobstructed reach used for handling objects.
            Vector3 origin=S.player.view.transform.position;Vector3 direction=S.player.view.transform.forward;
            float distance=Physics.Raycast(origin,direction,out var hit,1.1f,~0,QueryTriggerInteraction.Ignore)?Mathf.Max(.25f,hit.distance-.25f):1.1f;
            item.transform.position=origin+direction*distance;item.secured=false;item.gameObject.SetActive(true);item.Body.isKinematic=false;item.Body.linearVelocity=Vector3.zero;
            if(item.supplyBit>=0)S.state.homeSupplies&=~(1<<item.supplyBit);
            S.Notify(ItemName(item)+" put down","On the ground nearby. F carries it; E packs it again.");S.RequestCheckpoint();
        }
        public void RestoreInventory()
        {
            IsOpen=false;EnsureSlots();Data.equippedInventoryId=null;
            var bag=Find(Data.backpackId);if(bag){bag.secured=true;bag.Body.isKinematic=true;bag.gameObject.SetActive(false);}
            for(int i=0;i<Capacity;i++){var item=Slot(i);if(!item||item.delivered||Data.flags.Contains("lost:"+item.persistentId)||Data.flags.Contains("consumed:"+item.persistentId)){Data.inventorySlots[i]=null;continue;}item.secured=true;item.Body.isKinematic=true;item.gameObject.SetActive(false);}
        }
        Vector3 ClearSpillPosition()
        {
            var player=S.player.transform;var origin=player.position+Vector3.up*.7f;
            // Keep the bag out of both the player's capsule and the obstruction
            // they approached. A drop inside a fence can eject a physics body.
            foreach(var direction in new[]{player.forward,player.right,-player.right,-player.forward})
            {
                bool blocked=false;
                foreach(var hit in Physics.SphereCastAll(origin,.36f,direction,1.05f,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(player)&&hit.normal.y<.65f){blocked=true;break;}
                var candidate=origin+direction*1.05f;
                if(!blocked)foreach(var c in Physics.OverlapSphere(candidate,.34f,~0,QueryTriggerInteraction.Ignore))
                    if(!c.transform.IsChildOf(player)){blocked=true;break;}
                if(!blocked)return candidate;
            }
            return origin-player.forward*.65f;
        }
        public bool Spill(PhysicalDecisionStation station)
        {
            if(!HasBag)return false;var bag=Find(Data.backpackId);Data.backpackId=null;
            bag.transform.SetParent(null,true);bag.transform.position=ClearSpillPosition();
            // This pickup is a stable work surface for the separate strap tool.
            bag.transform.rotation=Quaternion.LookRotation(S.player.transform.forward,Vector3.up);
            bag.Body.constraints=RigidbodyConstraints.FreezeRotation;
            bag.gameObject.SetActive(true);bag.secured=false;bag.Body.isKinematic=false;station.brokenBag=bag;station.spill.transform.position=bag.transform.position;
            S.physicalDecisions.SetFlag("broken_bag:"+bag.persistentId);S.physicalDecisions.SetFlag("strap_broken");
            for(int i=0;i<Capacity;i++)
            {
                var item=Slot(i);if(!item)continue;if(item.holder)item.holder.Release();
                item.transform.SetParent(station.spill.transform,true);item.transform.position=bag.transform.position+new Vector3((i%3-1)*.3f,.25f,(i/3)*.12f);
                item.station=station;item.preparedForUse=false;item.secured=false;item.gameObject.SetActive(true);item.Body.isKinematic=false;item.Body.linearVelocity=Vector3.up*.5f;
                S.physicalDecisions.SetFlag("spilled:"+item.persistentId);Data.inventorySlots[i]=null;
            }
            Data.equippedInventoryId=null;S.state.homeSupplies=0;IsOpen=false;S.LockCursor();
            S.Notify("Your bag strap broke","Bag damaged. Your belongings are on the ground; I / Tab still shows their status.");S.RequestCheckpoint();return true;
        }
        void Update()
        {
            if(!S)return;if(S.Mode!=SibucaoMode.Playing){IsOpen=false;return;}
            EnsureSlots();
            for(int i=0;i<Capacity;i++){var item=Slot(i);if(item&&(item.delivered||Data.flags.Contains("lost:"+item.persistentId)))Data.inventorySlots[i]=null;}
            if(!string.IsNullOrEmpty(Data.equippedInventoryId))
            {
                var item=Find(Data.equippedInventoryId);
                if(!item||!item.IsHeld){if(item&&item.gameObject.activeSelf){int index=Array.IndexOf(Data.inventorySlots,item.persistentId);if(index>=0)Data.inventorySlots[index]=null;}Data.equippedInventoryId=null;}
            }
            var kb=Keyboard.current;if(kb==null)return;
            if(IsOpen){if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.I).wasPressedThisFrame||kb.tabKey.wasPressedThisFrame||kb.escapeKey.wasPressedThisFrame)Close();return;}
            if(StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.I).wasPressedThisFrame||kb.tabKey.wasPressedThisFrame)Open();
            if(HasBag&&(!S.home||!S.home.MovementLocked)&&(!S.missionGuide||!S.missionGuide.BlocksInput))
                for(int i=0;i<9;i++)if(kb[(Key)((int)Key.Digit1+i)].wasPressedThisFrame){Selected=i;EquipSelected();break;}
        }
    }
}
