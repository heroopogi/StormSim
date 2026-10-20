using System.Linq;
using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.Preparedness
{
    public sealed partial class PreparednessSession
    {
        public static readonly string[] GroupNames={"Supplies","Go-bag","Home safety","Household plan","Departure"};
        public static readonly int[][] GroupChecks={new[]{0,1},new[]{2,3,4,5,6,7,8,9,10,11},new[]{12,13,14,15,16,17},new[]{18},new[]{19}};
        bool coachingEnabled=true;
        float checklistNarratedAt=-100;
        public bool CoachingChoicePending {get;private set;}
        public bool Guided=>SibucaoSession.Instance?SibucaoSession.Instance.settings.hints:coachingEnabled;
        public void NarrateChecklist()
        {
            guidanceUsed=true;int complete=decisions.Count(value=>value);int index=NextChecklistIndex();
            if(Time.unscaledTime-checklistNarratedAt<2)return;checklistNarratedAt=Time.unscaledTime;
            string text=complete>=decisions.Length?PreparationNarration.ChecklistComplete:"Next checklist step: "+PreparationNarration.ChecklistStep(index);
            SpeakCue(complete>=decisions.Length?"checklist.complete":"checklist.next."+index,"Household guide",text);
        }
        int NextChecklistIndex()
        {
            int[] order={2,3,4,5,6,7,8,9,10,0,1,11,18,12,13,14,15,16,17,19};
            foreach(int index in order)if(!decisions[index])return index;return 19;
        }
        public void BeginCoachingChoice()
        {
            if(gameObject.scene.name!="SIBUCAO_RAIN")return;
            // Keep the choice pending behind the opening title. PreparationIntro blocks
            // input until its fade completes, after which this panel becomes interactive.
            CoachingChoicePending=true;
            GetComponent<PreparationIntro>()?.Begin();
            if(player)player.CloseDesktopPanels();
        }
        public void SetCoaching(bool enabled)
        {
            GetComponent<PreparationIntro>()?.End();
            RestoreCoaching(enabled?1:2);
            CoachingChoicePending=false;
            if(player)player.RefreshPanelCursor();
            if(televisionMediaFinished&&(phase==PrepPhase.Peaceful||phase==PrepPhase.Bulletin))CompleteTelevisionBulletin();
            else Say(enabled?"Coaching on. Move with WASD and look with the mouse. J opens your tutorial and guide.":"Coaching off. Move with WASD and look with the mouse. J opens your guide.");
        }
        void RestoreCoaching(int mode)
        {
            CoachingChoicePending=false;
            if(mode==0)return; // Older saves retain the player's existing preference.
            coachingEnabled=mode==1;
            if(SibucaoSession.Instance)SibucaoSession.Instance.settings.hints=coachingEnabled;
        }
        public string TutorialSummary=>TutorialComplete
            ? "Tutorial complete — you opened storage, picked up, checked, and packed a supply. Try these actions with another essential."
            : "Learn by doing — progress stays when coaching is off.\n"+
                ((tutorialProgress&1)!=0?"[done] ":"[  ] ")+"Open storage to find supplies.\n"+
                ((tutorialProgress&2)!=0?"[done] ":"[  ] ")+"Pick up a supply.\n"+
                ((tutorialProgress&4)!=0?"[done] ":"[  ] ")+"Check or seal it so it is ready to use.\n"+
                ((tutorialProgress&8)!=0?"[done] ":"[  ] ")+"Pack it in the open go-bag to carry it safely.";
        public bool TutorialComplete=>(tutorialProgress&15)==15;
        public string PlanSummary=>WarningStageText+"\n\n"+(evacuationOrdered
            ? "Take your bag to the exit. Unfinished chores and neighbor visits must not delay departure."
            : "Prepare the supplies you judge useful, keep the bag within its carrying limit, and review the household route. Property work and neighbor visits are optional. Reading this guide pauses preparation time.");
        public string GroupHeading(int group)=>GroupNames[group]+"  "+GroupChecks[group].Count(i=>decisions[i])+" / "+GroupChecks[group].Length+
            (group==2?" · optional while safe":group==4?" · choose when to leave":" · priority");
        public string GroupDetails(int group)
        {
            var lines=string.Join("\n",GroupChecks[group].Select(i=>(decisions[i]?"[done] ":"[  ] ")+Titles[i]+(!decisions[i]?"\n    "+evidence[i]:"")));
            if(group==0)lines+="\n"+QuantityGuide;
            if(group==1)lines+="\nCheck equipment before packing. You can leave without completing every check.";
            if(group==2)
            {
                lines+="\n\nOptional home activities — stop when evacuation is advised:\n"+
                    string.Join("\n",GetComponentsInChildren<PreparednessActivity>(true)
                        .Where(a=>a.check==PreparednessActivity.Check.Tasks||a.check==PreparednessActivity.Check.Waste)
                        .Select(a=>(a.Complete(this)?"[done] ":"[  ] ")+a.title));
            }
            if(group==3)lines+="\n\n"+HouseholdCard+"\n\n"+RouteDescription(true)+"\n\n"+RouteDescription(false)+"\n\n"+WarningProgress+". Visiting every household is not required. Neighbor warnings are optional; acknowledgement is not proof of evacuation.";
            if(group==4)lines+=evacuationOrdered
                ? "\nAn evacuation advisory is active. Use the exit now; you do not need to return to the TV."
                : "\nEXIT: after checking the TV, depart early with what you prepared.\nBED: confirm rest to advance to the morning advisory. Supplies stay where you left them.\nKEEP PREPARING: an advisory will arrive later. Do not wait for every box to be checked.";
            return lines;
        }
        public string NextPreparationStep
        {
            get
            {
                if(phase==PrepPhase.Peaceful||phase==PrepPhase.Bulletin)return "Listen to the television weather update.";
                if(phase==PrepPhase.Fade)return departure=="overnight"?"Resting until the morning advisory…":"Leaving home with your preparations…";
                if(phase==PrepPhase.ReadyForStorm)return "Your preparations carry forward.";
                if(evacuationOrdered)return player.carriedBag?"Leave through the exit. Chores can wait.":"Collect your bag, then leave through the exit.";
                if(Weather==PreparationWeather.UnsafeOutside)return "Outdoor work stopped. Prepare inside, or recheck the TV and leave early.";
                if(!Guided)return "Prepare essentials. Recheck the TV before leaving or resting.";
                if(chapter&&!chapter.state.needsDiscussed)return "Day before: speak with your family beside the dining table, then check supplies and visit the nearby store.";
                if(!bagRetrieved)return Bag&&Bag.storageDoor&&Bag.storageDoor.closed?"Open the bag's storage with E.":"Look at your go-bag and press E to collect it.";
                var held=player.held;
                if(held)
                {
                    if(held.kind==PrepKind.Batteries&&items.Any(i=>(i.kind==PrepKind.Flashlight||i.kind==PrepKind.Radio)&&i.cells<2))return "Look at a flashlight or radio; E fits the batteries.";
                    if(NeedsCheck(held))return CheckInstruction(held);
                    if(CanPackSupply(held))return Bag.closed
                        ? player.carriedBag?"Press E to open the bag for your supply.":"Look at your bag and press E to open it."
                        : "Press E to pack your checked supply.";
                    return "Place the item in its storage area with E; Q puts it down.";
                }
                if(!TutorialComplete)
                {
                    var example=items.FirstOrDefault(i=>i.kind==PrepKind.Documents&&!i.IsPacked);
                    if(example)return example.storageDoor&&example.storageDoor.closed?"Open the document pouch's storage with E.":"Pick up the document pouch with E, then E seals it.";
                }
                if(!decisions[2])return DeviceStep(PrepKind.Flashlight,"flashlight");
                if(!decisions[3])return DeviceStep(PrepKind.Radio,"radio");
                if(!TravelWaterPacked)return "Pack the small travel-water bottle. Keep the large containers at home.";
                if(!decisions[4])return "Pack a battery organizer with a spare pair remaining.";
                if(!decisions[5])return "Check, close, and pack your first-aid case.";
                if(!decisions[6])return "Check the labelled medicine and pack it.";
                if(!decisions[7])return "Seal your document pouch and pack it.";
                if(!decisions[8])return "Charge your phone at the charging area, then pack it.";
                if(!decisions[9])return "Charge and pack the power bank, and pack the matching cable.";
                if(!decisions[10])return "Pack the hygiene bag and folded raincoat.";
                if(!decisions[0])return "At the household supply shelf, E asks for help storing water containers.";
                if(!decisions[1])return "Store or pack the three household-day food sets.";
                if(!primaryRoute||!backupRoute)return "Review the primary and backup route cards.";
                if(!advisoryRead)return "Recheck the TV. You can then leave early.";
                return "Leave early at the exit, or keep preparing. J shows your plan.";
            }
        }
        string DeviceStep(PrepKind kind,string name)
        {
            var item=items.FirstOrDefault(i=>i.kind==kind);
            if(item&&item.IsPacked)return "Your "+name+" needs checking. Retrieve it from I / Tab bag.";
            if(item&&item.cells<2)return "Pick up batteries; use E on the "+name+" to fit them.";
            return "Pick up, test, and pack your "+name+" with E.";
        }
        public static bool CanPackSupply(PreparednessItem item)=>item&&(item.GetComponent<PosterSupply>()&&item.GetComponent<PosterSupply>().supply!=PosterSupplyType.Boots||item.id=="travel-water"||item.kind==PrepKind.Decoration&&(item.id=="raincoat"||item.id=="whistle")||new[]{PrepKind.Food,PrepKind.Flashlight,PrepKind.Radio,PrepKind.Batteries,PrepKind.FirstAid,PrepKind.Medicine,PrepKind.Documents,PrepKind.Phone,PrepKind.PowerBank,PrepKind.Hygiene,PrepKind.Cable}.Contains(item.kind));
        public static bool NeedsCheck(PreparednessItem item)
        {
            if(!item)return false;
            if(item.GetComponent<PosterSupply>())return !item.prepared;
            switch(item.kind)
            {
                case PrepKind.Flashlight:case PrepKind.Radio:return !item.prepared||item.cells<2;
                case PrepKind.Documents:return !item.closed;
                case PrepKind.FirstAid:return !item.prepared||!item.closed;
                case PrepKind.Medicine:return !item.prepared;
                default:return false;
            }
        }
        public static string CheckInstruction(PreparednessItem item)
        {
            if((item.kind==PrepKind.Flashlight||item.kind==PrepKind.Radio)&&item.cells<2)return "Q puts the device down. Pick up batteries and use E on it.";
            if(item.kind==PrepKind.Documents)return "Press E to seal the document pouch.";
            if(item.kind==PrepKind.FirstAid)return item.closed?"Press E to inspect the first-aid case.":"Press E to close the checked first-aid case.";
            return "Press E to check "+item.displayName+".";
        }
    }
}
