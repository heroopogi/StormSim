using System;

using System.Collections.Generic;

using UnityEngine;



namespace StormSim.Sibucao

{

    [Serializable] public sealed class CausalRule

    {

        public int source, choice;

        [TextArea] public string nextSituation;

        [Range(-.2f,.2f)] public float nextChance;

        [Range(0,8)] public float transitionEffort;

    }

    [Serializable] public sealed class CausalEntry

    {

        public int stage;

        public string parentPath, cause;

        public float chanceModifier, effort, staminaAfter;

    }

    [Serializable] public sealed class CausalRun

    {

        public int version=1;

        public string path="";

        public List<CausalEntry> entries=new List<CausalEntry>();

    }



    // Authored scenario rules, not a trained classifier or calibrated injury probabilities.

    [CreateAssetMenu(menuName="STORMSIM/During causal branching model")]

    public sealed class SibucaoCausalModel : ScriptableObject

    {

        public CausalRule[] rules=Defaults();

        public static readonly int[] ChoiceCounts={3,4,4,3,3,3,3,4,3};

        public CausalRule Find(int stage,int choice)=>Array.Find(rules,r=>r.source==stage&&r.choice==choice);

        public static bool IsSideResult(int result)=>result==100||result==101;
        public static char PathCode(int result)=>result==100?'L':result==101?'R':(char)('A'+result);
        public static int Next(PhysicalRunData run)

        {

            for(int i=0;i<9;i++)if(run.results==null||i>=run.results.Length||run.results[i]<0||run.results[i]>=ChoiceCounts[i]&&!IsSideResult(run.results[i]))return i;

            return 9;

        }

        public static string Path(PhysicalRunData run)

        {

            string path="";int n=Next(run);

            for(int i=0;i<n;i++)path+=PathCode(run.results[i]);

            return path;

        }

        public void ValidateRun(PhysicalRunData run)

        {

            if(rules==null||rules.Length!=30)throw new InvalidOperationException("The DURING model needs all 30 authored choice rules.");

            for(int s=0;s<9;s++)for(int c=0;c<ChoiceCounts[s];c++)

            {

                int count=0;foreach(var r in rules)if(r!=null&&r.source==s&&r.choice==c){count++;if(string.IsNullOrWhiteSpace(r.nextSituation))throw new InvalidOperationException("Missing downstream cause.");}

                if(count!=1)throw new InvalidOperationException("Missing or duplicate B"+(s+1)+" choice "+c+".");

            }

            if(run==null)return;

            bool gap=false;string expected="";

            for(int i=0;i<9;i++)

            {

                int c=run.results!=null&&i<run.results.Length?run.results[i]:-1;

                if(c<0){gap=true;continue;}

                if(gap||c>=ChoiceCounts[i]&&!IsSideResult(c))throw new InvalidOperationException("This older save bypassed a core encounter. Start a new run for ordered B1–B9 branching; the old save has been kept.");

                expected+=PathCode(c);

            }

            if(run.causal!=null&&(run.causal.version!=1||run.causal.path!=expected))throw new InvalidOperationException("Saved branch history does not match completed encounters.");

        }

        public CausalEntry Enter(PhysicalRunData run,int stage)

        {

            if(stage<0||stage>9||Next(run)!=stage)throw new InvalidOperationException("A core encounter cannot be entered out of order.");

            if(run.causal==null)run.causal=new CausalRun{path=Path(run)};

            var existing=run.causal.entries.Find(e=>e.stage==stage);

            if(existing!=null)return existing;

            var entry=new CausalEntry{stage=stage,parentPath=run.causal.path};

            if(stage>0)

            {

                var previous=run.results[stage-1];
                var rule=IsSideResult(previous)?new CausalRule{source=stage-1,choice=previous,nextSituation="You completed the "+(previous==100?"left":"right")+" passage mission. Your supplies and condition carry into the next section.",transitionEffort=1}:Find(stage-1,previous);
                if(rule==null)throw new InvalidOperationException("No downstream rule for the previous choice.");

                entry.cause=rule.nextSituation;entry.chanceModifier=rule.nextChance;entry.effort=rule.transitionEffort;

                var variant=run.floodVariants!=null&&stage-1<run.floodVariants.Length?run.floodVariants[stage-1]:null;

                if(variant!=null&&variant.band>0&&!IsSideResult(previous))

                    entry.cause=FloodVariantCatalog.Action(stage-1,run.results[stage-1])+". "+(entry.chanceModifier>0?"That approach helps with the next situation.":entry.chanceModifier<0?"That approach adds strain to the next situation.":"Your condition carries into arrival.")+" Earlier injuries, assistance and supply losses still count.";

                run.stamina=Mathf.Max(0,run.stamina-entry.effort);

            }

            entry.staminaAfter=run.stamina;run.causal.entries.Add(entry);return entry;

        }

        static CausalRule R(int s,int c,float chance,float effort,string message)=>new CausalRule{source=s-1,choice=c,nextChance=chance,transitionEffort=effort,nextSituation=message};

        public static CausalRule[] Defaults()=>new[] {

            R(1,0,.08f,0,"You checked the water before crossing. That careful footing helps at the stalled cars."),

            R(1,1,.01f,1,"The road crossing leaves a little more exertion to carry into the car passage."),

            R(1,2,-.08f,3,"The damaged railing crossing leaves you less steady at the stalled cars."),

            R(2,0,.06f,1,"Clearing the car passage gives you a steadier approach to the canal."),

            R(2,1,-.03f,2,"Squeezing between the cars leaves you less steady approaching the canal."),

            R(2,2,-.06f,3,"Climbing the hood adds exertion before the canal crossing."),

            R(2,3,-.1f,4,"Swimming past the cars leaves extra fatigue at the canal."),

            R(3,0,.08f,0,"The resident's directions help you approach the next obstruction carefully."),

            R(3,1,-.05f,2,"Wading the main road adds exertion before the fallen cable."),

            R(3,2,.03f,1,"The side alley gives you a steadier approach to the fallen cable."),

            R(3,3,.06f,.5f,"Using the covered passage leaves less exposure before the fallen cable."),

            R(4,0,.06f,0,"The outside lane leaves you steadier for handling the damaged bag."),

            R(4,1,-.04f,2,"Passing beside the cable leaves you less steady for handling supplies."),

            R(4,2,-.1f,3,"Crossing the wire leaves extra strain at the damaged bag. Any injury remains with you."),

            R(5,0,.05f,1,"The strap repair attempt costs some effort before helping at the curb. Only supplies actually retained remain usable."),

            R(5,1,-.02f,2,"Recovering scattered contents adds effort before helping at the curb. Check what you actually recovered."),

            R(5,2,-.06f,0,"You left the bag behind. Its lost supplies stay unavailable during later assistance."),

            R(6,0,-.04f,3,"Lifting the chair adds fatigue on the approach to the rescue checkpoint."),

            R(6,1,.04f,1,"The clear approach conserves effort before the rescue checkpoint."),

            R(6,2,-.02f,0,"You continued without assisting the wheelchair resident. Their location still needs to be reported."),

            R(7,0,.08f,0,"The responder's directions help you approach the collapsed evacuee."),

            R(7,1,.03f,1,"You reached the next area by boat. Its landing and passenger details remain part of your arrival report."),

            R(7,2,-.06f,3,"Staying on the road adds fatigue before reaching the collapsed evacuee."),

            R(8,0,-.07f,3,"Carrying the evacuee adds exertion before the creek boundary."),

            R(8,1,.05f,1,"Fetching a neighbour shares the assistance work before the creek boundary."),

            R(8,2,-.02f,0,"You left the collapsed evacuee behind. Report their location at the school."),

            R(8,3,-.03f,2,"Treatment and carrying add exertion before the creek. The used kit remains consumed."),

            R(9,0,0,1,"You arrived by the inland route. The arrival record retains your remaining health, energy and supplies."),

            R(9,1,0,2,"You arrived along the creek edge. Any injuries and missing supplies carry into the arrival record."),

            R(9,2,0,4,"You arrived through the damaged bank gap with added exertion. Earlier assistance and losses remain on record.")

        };

    }



    public sealed partial class DecisionTreeManager

    {

        public SibucaoCausalModel causalModel;

        public int NextCoreStage=>SibucaoCausalModel.Next(Data);

        public bool CanEnter(PhysicalDecisionStation station)=>station.supplementary||!causalModel||station.index==NextCoreStage;

        public void EnterCausalStage(int stage)

        {

            if(!causalModel)return;

            var entry=causalModel.Enter(Data,stage);

            if(stage>0)
            {
                SetFlag("visible_consequence:"+stage);
                AddConsequenceNotice("branch:"+entry.parentPath,stage,entry.cause);
            }

        }

        public float CausalChance(int stage)

        {

            if(!causalModel||stage<0||stage>=9)return 0;

            var entry=Data.causal?.entries.Find(e=>e.stage==stage);

            // Fatigue/injury persist across every later stage; authored context applies to the next stage.

            return (entry?.chanceModifier??0)-Mathf.Clamp01((40-Data.stamina)/40)*.12f-Mathf.Clamp01((70-Data.health)/70)*.12f;

        }

        public void RecordCausalChoice(PhysicalChoiceResult result)

        {

            if(!causalModel||result.node>=9)return;

            if(Data.causal==null)Data.causal=new CausalRun();

            result.parentBranch=Data.causal.path;

            Data.causal.path=SibucaoCausalModel.Path(Data);

            result.branch=Data.causal.path;

            string fact=BranchFact(result.node,result.choice);
            if(!string.IsNullOrEmpty(fact))SetFlag(fact);

        }

        static string BranchFact(int stage,int choice)
        {
            if(choice==100)return "left_passage_assisted:"+stage;
            if(choice==101)return "right_passage_assisted:"+stage;
            string[][] facts={
                new[]{"route_verified","water_exposure","railing_risk"},
                new[]{"car_passage_cleared","tight_squeeze","hood_climb","swim_exposure"},
                new[]{"resident_directions","main_road_exposure","alley_route","covered_route"},
                new[]{"cable_diversion","cable_close_pass","cable_crossed"},
                new[]{"bag_repaired","supplies_recovered","bag_abandoned"},
                new[]{"resident_carried","resident_guided","resident_unassisted"},
                new[]{"responder_route","boat_transfer","road_fatigue"},
                new[]{"evacuee_carried","neighbour_help","evacuee_unassisted","first_aid_used"},
                new[]{"inland_arrival","creek_arrival","bank_arrival"}
            };
            return stage>=0&&stage<facts.Length&&choice>=0&&choice<facts[stage].Length?facts[stage][choice]:null;
        }

    }

}

