using UnityEngine;

namespace StormSim.Sibucao
{
    public static class PhysicalEncounterCatalog
    {
        // B1 already has a complete embodied loop: observe, optionally compare
        // two real depths, then traverse a chosen lane. Layering the generic
        // three-piece debris routine over it made the first lesson feel arbitrary.
        public static bool Supports(SibucaoPhysicalOption option) => option && option.station && !option.station.supplementary &&
            option.station.index > 0 && option.station.index < 5 && option.station.node &&
            option.station.node.id == "sibucao_b" + (option.station.index + 1) && option.Choice;

        static PhysicalEncounterPhase Phase(string title,EncounterWork work,float seconds,float route,string action) =>
            new PhysicalEncounterPhase {title=title,work=work,activeSeconds=seconds,routeFraction=route,sequence=new[]{action}};

        // Inspect, commit, then secure a visible result. Real collider work remains,
        // but repeated filler pieces no longer hide the decision from the player.
        static PhysicalEncounterPhase[] Steps(EncounterWork first,string inspect,string commit,string finish) => new[]
        {
            Phase("NOTICE — inspect the hazard",first,6,0,inspect),
            Phase("COMMIT — work through the route",EncounterWork.Clearance,7,.45f,commit),
            Phase("CONSEQUENCE — secure the exit",EncounterWork.Clearance,5,.85f,finish)
        };

        public static PhysicalEncounterDefinition Create(int station,int choice)
        {
            var d=ScriptableObject.CreateInstance<PhysicalEncounterDefinition>();
            d.name="B"+(station+1)+" C"+choice+" three-action encounter";d.hideFlags=HideFlags.DontSave;
            switch(station*10+choice)
            {
                case 0:d.phases=Steps(EncounterWork.Inspect,"Compare the near and far water marks","Move the depth marker to the safer line","Set the exit-footing marker");break;
                case 1:d.phases=Steps(EncounterWork.Inspect,"Test the road edge with the marker","Pull floating debris out of the crossing","Place the marker on firm exit footing");break;
                case 2:d.phases=Steps(EncounterWork.Inspect,"Check the bent railing for sharp edges","Pull the loose rail away from the gap","Clear the final snag from the exit");break;
                case 10:d.phases=Steps(EncounterWork.Inspect,"Check what is wedged between the cars","Pull the wedged timber clear","Stack the timber away from the passage");break;
                case 11:d.phases=Steps(EncounterWork.Inspect,"Check shoulder and bag clearance","Move the loose mirror edge aside","Clear the final clothing snag");break;
                case 12:d.phases=Steps(EncounterWork.Inspect,"Test the first hand and foothold","Shift the loose sheet off the hood","Secure the descent-side footing");break;
                case 13:d.phases=Steps(EncounterWork.Inspect,"Locate the visible exit between the cars","Push driftwood out of the swim line","Secure the climb-out edge");break;
                case 20:d.phases=Steps(EncounterWork.Inspect,"Match the resident's first landmark","Move the lane obstruction aside","Place the marker at the described exit");break;
                case 21:d.phases=Steps(EncounterWork.Inspect,"Read the main-road water line","Move debris away from the wading line","Secure the raised exit edge");break;
                case 22:d.phases=Steps(EncounterWork.Inspect,"Find the side-alley landmark","Clear the narrow alley opening","Verify the alley rejoins the route");break;
                case 23:d.phases=Steps(EncounterWork.Inspect,"Aim the light at the overhead edge","Move the hidden obstruction aside","Mark the illuminated exit");break;
                case 30:d.phases=Steps(EncounterWork.Inspect,"Locate the outside diversion marker","Pull debris off the diversion lane","Set the marker where the lane rejoins");break;
                case 31:d.phases=Steps(EncounterWork.Inspect,"Check body and bag clearance","Move the constrained-lane obstruction","Clear the final strap snag");break;
                case 32:d.phases=Steps(EncounterWork.Inspect,"Check the exposed cable and debris","Move the loose approach obstruction","Set a warning marker before crossing");break;
                case 40:d.phases=new[]
                {
                    Phase("NOTICE — inspect the damaged buckle",EncounterWork.Inspect,5,0,"Lift the buckle tongue"),
                    Phase("COMMIT — repair the strap",EncounterWork.Align,8,0,"Feed the strap through the buckle"),
                    Phase("CONSEQUENCE — test the repair",EncounterWork.Align,5,0,"Pull the repaired strap tight")
                };break;
                case 41:d.phases=Steps(EncounterWork.Gather,"Open a safe space beside the spill","Move the loose load back toward the bag","Secure the recovered supplies");break;
                case 42:d.phases=Steps(EncounterWork.Inspect,"Release the damaged bag strap","Move the bag clear of the escape gap","Mark the supplies left behind");break;
            }
            if(d.Valid)return d;Object.DestroyImmediate(d);return null;
        }
    }
}
