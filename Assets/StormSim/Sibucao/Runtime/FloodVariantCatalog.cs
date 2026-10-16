using UnityEngine;
namespace StormSim.Sibucao
{
    public static class FloodVariantCatalog
    {
        static readonly string[][] actions={
            new[]{"Read the depth staff and secure the crossing","Clear the raised crossing","Use the exposed side passage"},
            new[]{"Clear driftwood from the bypass","Clear the narrow landing passage","Secure the raised crossing panels","Secure the float-side crossing"},
            new[]{"Ask the resident about the raised lane","Clear the main landing route","Take the side landing","Illuminate the covered landing"},
            new[]{"Clear the outer exclusion route","Take the narrow marked detour","Clear the exposed detour edge"},
            new[]{"Repair the strap on the work deck","Recover the supplies on the deck","Leave the damaged bag behind"},
            new[]{"Lift the chair to the transfer point","Guide the chair along the clear route","Continue without assisting"},
            new[]{"Follow the responder through the landing","Board and release the rescue boat","Take the raised road-side route"},
            new[]{"Carry the evacuee to the transfer point","Fetch a neighbour for the transfer","Continue without assisting","Use a kit, then carry the evacuee"},
            new[]{"Take the inland elevated exit","Follow the creek-side landing","Use the exposed bank-side exit"}
        };
        public static string Action(int stage,int choice)=>actions[stage][choice];
        public static string Label(int stage,int choice,int band)=>actions[stage][choice]+" — "+FloodDifficultySettings.BandNames[band];
        public static PhysicalMechanism Mechanism(int stage,int choice,PhysicalMechanism original)
        {
            if(stage==0)return choice==0?PhysicalMechanism.TalkThenWalk:PhysicalMechanism.Walk;
            if(stage==1||stage==3||stage==8)return PhysicalMechanism.Walk;
            return original;
        }
        public static PhysicalEncounterDefinition Work(int stage,int choice,int band)
        {
            if(stage>4)return null;
            var d=PhysicalEncounterCatalog.Create(stage,choice);
            if(stage==4&&choice==0)return d; // Retain the real strap/buckle work.
            for(int p=0;p<d.phases.Length;p++)
            {
                d.phases[p].title=Label(stage,choice,band);
                d.phases[p].sequence=new[]{p==0?"Check the raised flood marker":p==1?"Pull the wedged driftwood clear":"Secure the raised exit footing"};
            }
            return d;
        }
    }
}
