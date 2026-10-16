using System.Collections.Generic;
using UnityEngine;

namespace StormSim.Sibucao
{
    public sealed partial class SibucaoInventory
    {
        public bool ShowStatus { get; set; }
        public string BagStatus => !HasBag ? "No bag equipped" : ItemStatus(Find(Data.backpackId));
        public string ItemStatus(PhysicalGrabbable item)
        {
            string status=BaseItemStatus(item);var prep=item?item.GetComponent<StormSim.Preparedness.PreparednessItem>():null;
            var bridge=S.GetComponent<StormSim.Preparedness.SibucaoPreparationBridge>();
            if(prep&&bridge)status+=" · "+bridge.preparation.ItemReadiness(prep);
            if(prep&&Data.flags.Contains("prep_wet:"+prep.id))status+=" · Water exposure recorded";
            return status;
        }
        string BaseItemStatus(PhysicalGrabbable item)
        {
            if (!item) return "Empty";
            string id = item.persistentId;
            if (Data.flags.Contains("lost:" + id)) return "Lost in water · unavailable";
            if (Data.flags.Contains("consumed:" + id)) return "Consumed · used up";
            if (item.delivered && item.kind == PhysicalItemKind.FirstAid) return "Consumed · used up";
            if (item.delivered) return "Delivered / used";
            if (Data.flags.Contains("broken_bag:" + id) && !Data.flags.Contains("strap_repaired"))
                return Data.backpackId == id ? "Damaged bag · carrying by handle" : "Damaged bag · on ground";
            if (Data.backpackId == id) return "Equipped";
            if (item.IsHeld) return "Equipped · in hand";
            EnsureSlots();
            if (System.Array.IndexOf(Data.inventorySlots, id) >= 0) return "Stored in go-bag";
            if (item.preparedForUse) return "Ready for use · beside you";
            return "On ground · recoverable";
        }
        public List<PhysicalGrabbable> KnownItems()
        {
            var known = new List<PhysicalGrabbable>();
            if (items != null) foreach (var item in items)
                if (item && (Storable(item) || item.kind == PhysicalItemKind.Bag) &&
                    (item.everHeld || item.persistentId == Data.backpackId || Data.flags.Contains("lost:" + item.persistentId) || Data.flags.Contains("consumed:" + item.persistentId))) known.Add(item);
            known.Sort((a, b) => string.Compare(ItemName(a), ItemName(b), System.StringComparison.Ordinal));
            return known;
        }
    }
}
