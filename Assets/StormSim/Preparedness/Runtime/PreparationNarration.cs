namespace StormSim.Preparedness
{
    /// <summary>
    /// Recording script for every fixed preparation narration cue. Keep these lines
    /// synchronized with Documentation/PREPARATION_NARRATION_SCRIPT.md.
    /// </summary>
    public static class PreparationNarration
    {
        public const string AfterTelevisionGuided="The weather bulletin has ended. Start inside the house: open the household storage and collect your go-bag. Then press J, or the left controller's primary button, to raise your checklist and hear the next step.";
        public const string AfterTelevisionExplore="The weather bulletin has ended. Prepare essential supplies and review both evacuation routes. Press J, or the left controller's primary button, whenever you want the checklist and spoken guidance.";
        public const string MorningAdvisory="An evacuation advisory is active. Collect your bag and leave through the exit. Do not finish chores.";
        public const string EvacuationAdvisory="Leave now through the exit. Take your bag and leave optional chores unfinished.";
        public const string BagCollected="Go-bag collected. Pick up a supply, check it, then place it into the open bag.";
        public const string ChecklistComplete="The preparation checklist is complete. Recheck the advisory, take your go-bag, and depart safely.";

        public static readonly string[] ChecklistSteps=
        {
            "Store the household drinking-water containers on the kitchen supply shelf.",
            "Store or pack the three ready-to-eat household food sets.",
            "Fit batteries into the flashlight, test it, and pack it in the open go-bag.",
            "Fit batteries into the portable radio, test the reception, and pack it.",
            "Pack a battery organizer with at least one spare pair remaining.",
            "Inspect the first-aid case, close it, and pack it.",
            "Check the labelled household medicine against the care card, then pack it.",
            "Seal the document pouch against water and pack it.",
            "Charge the phone at the charging area, then pack it when ready.",
            "Charge and pack the power bank, and pack its matching cable.",
            "Pack the sealed hygiene bag and the folded dry raincoat.",
            "Close the go-bag and keep it ready to carry to the exit.",
            "Disconnect portable electronics while the area is dry, then place them on the high shelf.",
            "Move valuables to the high shelf above likely water exposure.",
            "Close the accessible windows and check that each latch is secure.",
            "Check that the front door opens and that the exit path is clear.",
            "Bring loose outdoor objects indoors while conditions are still safe.",
            "Use the tongs to collect dry surface litter and place it in the cleanup bin.",
            "Review both the primary inland route and the backup school route with your household.",
            "Recheck the television advisory. Then take your go-bag and leave early, or follow any evacuation order immediately."
        };

        public static string ChecklistStep(int index)
        {
            if(index<0||index>=ChecklistSteps.Length)return "Open the checklist and review the next safe preparation step.";
            return ChecklistSteps[index];
        }

        public static string PackedItem(PreparednessItem item)
        {
            string name=item?item.displayName:"Supply";
            if(item)switch(item.id)
            {
                case "documents":name="Document pouch, sealed before packing";break;
                case "first-aid":name="First-aid case, inspected and closed";break;
                case "flashlight":name="Flashlight, fitted with batteries and tested";break;
                case "food-0":case "food-1":case "food-2":name="Ready-to-eat household food set";break;
                case "medicine":name="Household medicine pack, checked against the care card";break;
                case "poster-candles":name="Candles in their protective case";break;
                case "poster-clothes":name="Dry clothes in their waterproof pouch";break;
                case "poster-matches":name="Matches in their waterproof case";break;
                case "radio":name="Portable radio, fitted with batteries and tested";break;
            }
            return name+" packed and ready. Check the bag display for remaining capacity.";
        }
    }
}
