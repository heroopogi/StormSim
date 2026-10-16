using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    [Serializable] public sealed class PreparationActionRecord
    {
        public string id,title,phase,evidence,aftermathKey;
        public bool completed;
        public int points;
    }

    // Uses the physical inventory's existing identifiers, save data and loss rules.
    public sealed class PreparationContinuity : MonoBehaviour
    {
        public GameObject[] legacyBags;
        SibucaoSession Game=>GetComponent<SibucaoSession>();
        PreparednessSession Prep=>GetComponent<SibucaoPreparationBridge>().preparation;
        public void HideLegacyBag(){if(legacyBags!=null)foreach(var bag in legacyBags)if(bag)bag.SetActive(false);}
        public void Transfer(PrepSave snapshot)
        {
            HideLegacyBag();var data=Game.state.physical;int slot=0;
            Array.Resize(ref data.inventorySlots,SibucaoInventory.Capacity);Array.Clear(data.inventorySlots,0,data.inventorySlots.Length);
            foreach(var item in Prep.items)
            {
                var physical=item.GetComponent<PhysicalGrabbable>();if(!physical)continue;
                physical.delivered=false;physical.everHeld=item.gathered;physical.secured=item.IsPacked;
                item.SetPhysics(false);item.SynchronizePose();
                if(item.IsPacked&&slot<data.inventorySlots.Length){data.inventorySlots[slot++]=physical.persistentId;item.gameObject.SetActive(false);}
            }
            Prep.Bag.gameObject.SetActive(true);Prep.Bag.closed=true;
            // A bag already in the player's hand must not require a second pickup
            // after switching controllers at the exit.
            var bag=Prep.Bag.GetComponent<PhysicalGrabbable>();
            if(snapshot.bagInRightHand&&bag)
            {
                data.backpackId=bag.persistentId;bag.secured=true;bag.everHeld=true;
                Prep.Bag.gameObject.SetActive(false);
            }
            if(snapshot.guidanceUsed)data.flags.Add("preparation_guidance_used");
            CommitPreparation();
            if(Prep.chapter)Prep.chapter.HideUnpurchased();
        }
        public void CommitPreparation()
        {
            var state=Game.state;
            if(state.physical.flags.Contains("preparation_scored"))return;
            Prep.RefreshEvidence();var records=new List<PreparationActionRecord>();
            void Add(string id,string title,bool done,int points,string evidence,string key)
            {records.Add(new PreparationActionRecord{id=id,title=title,phase="preparedness",completed=done,points=done?points:0,evidence=evidence,aftermathKey=key});}
            for(int i=0;i<20;i++)Add("readiness-"+i,PreparednessSession.Titles[i],Prep.decisions[i],ReadinessPoints(i),Prep.evidence[i],"readiness_"+i);
            Add("travel-water","Pack portable drinking water",Prep.TravelWaterPacked,40,"One 1 L bottle demonstrates the player's share; household needs are explained in the plan.","travel_water");
            foreach(var item in Prep.items.Where(i=>i.kind==PrepKind.HouseholdTask||i.kind==PrepKind.Neighbor))
                Add(item.id,item.displayName,item.prepared,0,item.kind==PrepKind.Neighbor?(item.cells>=3?"Warning, need and meeting plan discussed; arrival unconfirmed":item.cells>=2?"Warning acknowledged and need heard; meeting plan not discussed":item.prepared?"Warning acknowledged; needs not yet discussed":"No conversation recorded"):item.prepared?"Action completed":"Optional work not completed",item.id);
            foreach(var activity in Prep.GetComponentsInChildren<PreparednessActivity>(true))
                Add(activity.id,activity.title,activity.Complete(Prep),2,activity.Evidence(Prep),activity.aftermathKey);
            if(Prep.chapter){var c=Prep.chapter.state;
                Add("family-needs","Discuss household needs",c.needsDiscussed,0,"Household shopping needs discussed","family_needs");
                Add("family-meeting","Agree on meeting points",c.meetingAgreed,0,"School reception; backup at school entrance staff desk","family_meeting");
                Add("family-assistance","Agree on assistance",c.assistanceAgreed,0,"Family member accompanies Lola; player carries the go-bag","family_assistance");
                Add("family-contact","Agree on emergency contact",c.contactAgreed,0,"Fictional household contact card discussed","family_contact");
                foreach(var product in Prep.chapter.products)Add("purchase-"+product.item.id,"Buy "+product.item.displayName,Prep.chapter.Owns(product.item),0,Prep.chapter.Owns(product.item)?"Paid; packing and carrying tracked separately":"Not purchased","purchase_"+product.item.id);
            }
            foreach(var item in Prep.items.Where(i=>i.GetComponent<PosterSupply>())){var supply=item.GetComponent<PosterSupply>();Add("poster-"+item.id,item.displayName,supply.Complete,0,supply.Complete?"Checked and packed or worn": "Not ready to carry", "poster_"+item.id);}
            state.preparationActions=records;
            state.scores[0]=Mathf.Min(1500,state.scores[0]+records.Sum(r=>r.points));
            state.physical.flags.Add("preparation_scored");
            foreach(var record in records.Where(r=>r.completed))state.physical.flags.Add("aftermath:"+record.aftermathKey);
        }
        public static int ReadinessPoints(int index)=>index==19?200:index==18?80:index>=12?0:index<2?25:40;
        public void RecordStormPickup(PhysicalGrabbable item)
        {
            var records=Game.state.preparationActions;
            if(records==null)Game.state.preparationActions=records=new List<PreparationActionRecord>();
            string id="storm-collected:"+item.persistentId;
            if(records.Any(r=>r.id==id))return;
            int before=Game.state.scores[3];Game.state.scores[3]=Mathf.Min(1000,before+5);
            records.Add(new PreparationActionRecord{id=id,title=SibucaoInventory.ItemName(item),phase="during",completed=true,points=Game.state.scores[3]-before,evidence="Collected during the typhoon",aftermathKey="supply_recovered:"+item.persistentId});
        }
        public bool Interact()
        {
            if(!Physics.Raycast(Game.player.view.transform.position,Game.player.view.transform.forward,out var hit,2.7f,~0,QueryTriggerInteraction.Ignore))return false;
            var item=hit.collider.GetComponentInParent<PreparednessItem>();if(!item)return false;
            if(item.kind==PrepKind.StorageDoor)
            {item.closed=!item.closed;item.AnimateVisual();Game.state.preparationSnapshot=JsonUtility.ToJson(Prep.Capture());Game.Notify("Storage",item.closed?"Closed":"Open. E collects supplies into your bag.");return true;}
            var bin=item.GetComponent<PreparednessWasteBin>();if(bin){bin.Toggle();Game.state.preparationSnapshot=JsonUtility.ToJson(Prep.Capture());return true;}
            return false;
        }
        public static bool StageBag(PreparednessSession prep)
        {
            var bag=prep.Bag;var table=prep.items.FirstOrDefault(i=>i.kind==PrepKind.TableZone);if(!bag||!table)return false;
            if(bag.storedIn=="table"){bag.closed=true;return true;}
            var size=bag.WorldBounds().size;var zone=table.GetComponent<Collider>().bounds;
            foreach(float x in new[]{0f,-.35f,.35f})foreach(float z in new[]{0f,-.22f,.22f})
            {
                Vector3 center=table.transform.position+new Vector3(x,size.y*.5f+.015f,z);
                Bounds candidate=new Bounds(center,size);
                if(candidate.min.x<zone.min.x||candidate.max.x>zone.max.x||candidate.min.z<zone.min.z||candidate.max.z>zone.max.z)continue;
                if(prep.items.Any(i=>i!=bag&&i.portable&&i.gameObject.activeInHierarchy&&i.storedIn=="table"&&candidate.Intersects(i.WorldBounds())))continue;
                prep.player.ReleaseBag();bag.transform.SetParent(prep.transform,true);bag.SetPhysics(false);
                bag.transform.position+=center-bag.WorldBounds().center;bag.closed=true;bag.storedIn="table";bag.storageDoor=null;bag.SynchronizePose();return true;
            }
            prep.Say("Clear a space on the dining table for your bag before sleeping.");return false;
        }
    }
}
