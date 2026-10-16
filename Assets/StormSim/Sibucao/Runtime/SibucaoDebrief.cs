using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StormSim.Sibucao
{
    public static class SibucaoDebrief
    {
        public static readonly string[] Categories={"Preparedness","Evacuation","Decision-Making","Resource Management","Teamwork"};
        public static string Decision(SibucaoSession s,PhysicalChoiceResult r)
        {
            string title=r.node>=0&&r.node<9?DecisionTreeManager.Titles[r.node]:r.nodeId;
            string choice=r.choiceId;
            if(r.choice<100&&s.physicalDecisions.stations!=null)
                foreach(var station in s.physicalDecisions.stations)
                    if(station&&station.index==r.node&&station.node&&r.choice>=0&&r.choice<station.node.choices.Length)choice=station.node.choices[r.choice].label;
            var routes=s.physicalDecisions?s.physicalDecisions.GetComponent<SibucaoRouteChoices>():null;
            if(r.choice>=100&&routes)foreach(var mission in routes.GetComponentsInChildren<SibucaoSideMission>(true))if(mission.id==r.choiceId)choice=mission.title;
            var text=new StringBuilder(title+"\n"+choice+"\n");
            text.Append(r.choice>=100?"Side passage completed; the center situation was not attempted.":r.success?"Physical action completed. Recorded outcome: success.":"Physical action completed. Recorded outcome: setback.");
            if(r.chance>0&&r.chance<1)text.Append($" Seeded outcome: {r.chance:P0} chance; recorded draw {r.roll:P0}. Role, local water, weather and earlier events affected this result.");
            if(!string.IsNullOrEmpty(r.consequence))text.Append("\nConsequence: "+ConsequenceText(r.consequence));
            text.Append($"\n{r.category}: {r.appliedPoints:+0;-0;0}");
            text.Append(r.scoringRecorded?$"; secondary Decision-Making: {r.secondaryPoints:+0;-0;0}.":"; older save: secondary credit was not separately recorded.");
            if(r.lostItems!=null&&r.lostItems.Length>0)text.Append("\nLost supplies: "+string.Join(", ",r.lostItems)+".");
            return text.ToString();
        }
        public static string ConsequenceText(string value)
        {
            if(!Enum.TryParse(value,out PhysicalConsequence flags))return value;
            if(flags==PhysicalConsequence.None)return "No additional consequence was recorded.";
            var parts=new List<string>();
            if((flags&PhysicalConsequence.Stumble)!=0)parts.Add("a stumble");
            if((flags&PhysicalConsequence.Injury)!=0)parts.Add("an immediate injury");
            if((flags&PhysicalConsequence.LoseItem)!=0)parts.Add("lost supplies");
            if((flags&PhysicalConsequence.Exhaustion)!=0)parts.Add("exhaustion");
            if((flags&PhysicalConsequence.LeaveResident)!=0)parts.Add("the resident was left awaiting assistance");
            if((flags&PhysicalConsequence.Diversion)!=0)parts.Add("a route diversion");
            if((flags&PhysicalConsequence.DelayedInjury)!=0)parts.Add("an injury with a later effect");
            return parts.Count>0?string.Join("; ",parts)+".":value;
        }
        public static string ResidentStatus(PhysicalRunData data,int stage,string name)
        {
            var record=data.history?.FindLast(r=>r.node==stage);
            if(record==null||record.choice>=100||record.choice==2||data.flags.Contains("resident_left_"+stage))return name+": arrival unconfirmed; location requires follow-up.";
            // Assistance is not proof of arrival. Delivered is tracked separately by the physical body.
            return name+": assisted on the route; shelter arrival requires confirmation.";
        }
        public static string Summary(SibucaoSession s)
        {
            var d=s.state.physical;var b=new StringBuilder();
            b.AppendLine(StormSim.Preparedness.PreparationExposure.Summary(s));b.AppendLine();
            b.AppendLine("RESIDENTS & FOLLOW-UP");
            foreach(int stage in new[]{5,7})
            {
                string name=stage==5?"Wheelchair resident":"Collapsed evacuee";
                var record=d.history.FindLast(r=>r.node==stage);
                var station=Array.Find(s.physicalDecisions.stations,x=>x&&x.index==stage);
                bool delivered=record!=null&&record.choice<100&&record.choice!=2&&!d.flags.Contains("resident_left_"+stage)&&station&&station.assist&&station.assist.delivered;
                b.AppendLine(delivered?name+": delivered to the route's designated safe point.":ResidentStatus(d,stage,name));
            }
            var routes=s.physicalDecisions.GetComponent<SibucaoRouteChoices>();
            if(routes&&routes.sections!=null)foreach(var section in routes.sections)foreach(var mission in new[]{section.left,section.right})
                if(d.sideMissions.Exists(x=>x.id==mission.id&&x.complete)&&mission.companions!=null&&mission.companions.Length>0)
                    b.AppendLine(mission.title+": "+mission.companions.Length+" resident(s) escorted through this passage.");
            b.AppendLine("\nUNRESOLVED SITUATIONS");bool unresolved=false;
            for(int i=0;i<Mathf.Min(9,d.results.Length);i++)if(d.results[i]<0||d.results[i]>=99)
            {unresolved=true;b.AppendLine(DecisionTreeManager.Titles[i]+(d.results[i]>=100?": center outcome unconfirmed; another passage was completed.":": no completed outcome recorded."));}
            foreach(var mission in d.sideMissions)if(mission.started&&!mission.complete){unresolved=true;b.AppendLine(mission.id+": side work was started but not completed.");}
            if(!unresolved)b.AppendLine("Every core situation has a recorded outcome. See resident follow-up above.");
            b.AppendLine("\nSUPPLIES AT HANDOVER");var ids=new HashSet<string>();
            if(s.inventory)for(int i=0;i<SibucaoInventory.Capacity;i++){var item=s.inventory.Slot(i);if(item&&ids.Add(item.persistentId))b.AppendLine(SibucaoInventory.ItemName(item)+" — "+s.inventory.ItemStatus(item));}
            if(s.inventory){var item=s.inventory.Find(d.equippedInventoryId);if(item&&ids.Add(item.persistentId))b.AppendLine(SibucaoInventory.ItemName(item)+" — equipped");}
            if(s.player&&s.player.grabber){var item=s.player.grabber.held;if(SibucaoInventory.Storable(item)&&ids.Add(item.persistentId))b.AppendLine(SibucaoInventory.ItemName(item)+" — carried");}
            if(ids.Count==0)b.AppendLine("No supplies recorded in your bag or equipped slot.");
            b.AppendLine("\nLATER CONSEQUENCES");
            if(d.consequenceNotices.Count==0)b.AppendLine("No delayed notices were recorded.");
            foreach(var notice in d.consequenceNotices)b.AppendLine((notice.shown?"Reported: ":"For school follow-up: ")+notice.message);
            b.AppendLine("Assisted recoveries: "+d.assistedRecoveries);
            b.AppendLine("\nREFLECTION\nWhich earlier action changed a later situation? What would you pack or report differently on another run?");
            return b.ToString();
        }
    }
}
