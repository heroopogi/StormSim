using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    // Observations use simulated water contact, not a promise that storage prevents real damage.
    public sealed class PreparationExposure : MonoBehaviour
    {
        float next;
        static readonly Dictionary<Renderer,MaterialPropertyBlock> originalBlocks=new Dictionary<Renderer,MaterialPropertyBlock>();
        public static void RestorePresentation(){foreach(var pair in originalBlocks)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);originalBlocks.Clear();}
        void Update()
        {
            var game=GetComponent<SibucaoSession>();var bridge=GetComponent<SibucaoPreparationBridge>();
            if(!game||!bridge)return;
            if(bridge.Active){RestorePresentation();return;}
            if(game.Mode!=SibucaoMode.Playing||Time.time<next)return;
            next=Time.time+1;
            var flood=GetComponent<FloodSimulationManager>();if(!flood)return;
            foreach(var item in bridge.preparation.items)
            {
                if(item.kind!=PrepKind.Documents&&item.kind!=PrepKind.Electronics&&item.kind!=PrepKind.Valuables)continue;
                var physical=item.GetComponent<PhysicalGrabbable>();
                bool packed=physical&&game.state.physical.inventorySlots.Contains(physical.persistentId);
                var point=packed?game.player.transform.position:item.WorldBounds().min;
                if(flood.GetWaterDepth(point)>.05f)Observe(game,item);
                if(game.state.physical.flags.Contains("prep_wet:"+item.id))Tint(item);
            }
            if(game.state.homeSeconds>=SibucaoStormTimeline.FloodStart(game.home))
            {
                bool closed=bridge.preparation.items.Where(i=>i.kind==PrepKind.Window).All(i=>i.closed&&i.prepared);
                string key=closed?"prep_windows_latched":"prep_window_rain";
                if(!game.state.physical.flags.Contains(key)){game.state.physical.flags.Add(key);game.Notify("Home preparation",closed?"Latched windows reduce wind-driven rain in this scenario; they do not stop floodwater.":"An open or unchecked window lets wind-driven rain enter in this scenario.");}
            }
        }
        public static void Observe(SibucaoSession game,PreparednessItem item)
        {
            string key=(item.kind==PrepKind.Documents&&item.closed?"prep_seal_protected:":"prep_wet:")+item.id;
            if(game.state.physical.flags.Contains(key))return;
            game.state.physical.flags.Add(key);
            game.Notify("Preparation consequence",item.kind==PrepKind.Documents&&item.closed?"The sealed pouch protected the documents during this simulated water contact.":item.displayName+" was exposed to water. Its position and protection matter.");
            game.RequestCheckpoint();
        }
        static void Tint(PreparednessItem item)
        {
            var block=new MaterialPropertyBlock();
            foreach(var r in item.GetComponentsInChildren<Renderer>()){if(!originalBlocks.ContainsKey(r)){var original=new MaterialPropertyBlock();r.GetPropertyBlock(original);originalBlocks[r]=original;}r.GetPropertyBlock(block);block.SetColor("_BaseColor",new Color(.35f,.43f,.48f));r.SetPropertyBlock(block);}
        }
        public static string Summary(SibucaoSession game)
        {
            if(game.state.preparationActions==null||game.state.preparationActions.Count==0)return "No preparation record was carried into this run.";
            var b=new StringBuilder("PREPARATION FOLLOW-UP\n");
            foreach(var record in game.state.preparationActions.Where(r=>r.phase=="preparedness"&&(r.id=="readiness-7"||r.id=="readiness-12"||r.id=="readiness-13"||r.id=="readiness-14"||r.id=="readiness-19"||r.id=="travel-water")))
                b.AppendLine(record.title+": "+(record.completed?"completed":"not completed")+". "+record.evidence);
            var bridge=game.GetComponent<SibucaoPreparationBridge>();
            if(bridge)foreach(var item in bridge.preparation.items.Where(i=>i.kind==PrepKind.Documents||i.kind==PrepKind.Electronics||i.kind==PrepKind.Valuables))
                b.AppendLine(item.displayName+": "+(game.state.physical.flags.Contains("prep_wet:"+item.id)?"simulated water exposure recorded":game.state.physical.flags.Contains("prep_seal_protected:"+item.id)?"sealed pouch protected contents during simulated contact":"no water contact observed; preparation alone is not proof of protection"));
            b.AppendLine(game.state.physical.flags.Contains("prep_window_rain")?"Unchecked window: wind-driven rain entry was reported.":game.state.physical.flags.Contains("prep_windows_latched")?"Latched windows: reduced rain entry; flood protection is not implied.":"No window exposure observation recorded.");
            foreach(var record in game.state.preparationActions.Where(r=>r.phase=="preparedness"&&(r.id.StartsWith("family-")||r.id.StartsWith("neighbor-"))&&r.completed))b.AppendLine(record.title+": "+record.evidence);
            return b.ToString();
        }
    }
}
