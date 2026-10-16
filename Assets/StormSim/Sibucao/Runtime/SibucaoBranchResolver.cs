using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed partial class DecisionTreeManager
    {
        public PhysicalBranchAttempt Branch(int index)
        {
            if(Data.branches==null||Data.branches.Length<=index)EnsureNodeCapacity();
            return Data.branches[index]??(Data.branches[index]=new PhysicalBranchAttempt());
        }
        public static float SeedValue(int seed,int node,int choice,int salt)
        {
            unchecked {uint v=(uint)seed^(uint)(node*73856093)^(uint)(choice*19349663)^(uint)(salt*83492791);v^=v>>16;v*=0x7feb352d;v^=v>>15;v*=0x846ca68b;v^=v>>16;return (v&0x00ffffff)/16777216f;}
        }
        public bool ChoiceAvailable(PhysicalDecisionStation station,int index)
        {
            if(!CanEnter(station)||!station.node||index<0||index>=station.node.choices.Length)return false;
            var c=station.branches[index].Choice;if(!c)return false;var s=SibucaoSession.Instance;
            if(station.index==0&&index==0&&!station.branches[index].FloodAdapted&&!station.HasVisibleWater)return false;
            if(!string.IsNullOrEmpty(c.forbiddenFlag)&&Data.flags.Contains(c.forbiddenFlag)||!string.IsNullOrEmpty(c.requiredFlag)&&!Data.flags.Contains(c.requiredFlag))return false;
            if(!string.IsNullOrEmpty(c.requiredItemTag)&&(!s.inventory||!s.inventory.HasTag(c.requiredItemTag)))return false;
            var flood=s.GetComponent<FloodSimulationManager>();var tier=flood?flood.GetWaterTier(station.transform.position):WaterTier.Tier1;
            return tier>=c.minimumTier&&tier<=c.maximumTier;
        }
        public float Chance(PhysicalDecisionStation station,int index,WaterTier tier)
        {
            var s=SibucaoSession.Instance;
            float rain=s.weather?Mathf.InverseLerp(.6f,1.35f,s.weather.Intensity):0;
            return SibucaoDecisionEvaluation.Evaluate(station.branches[index].Choice,Data.role,Data.seed,station.index,index,tier,rain,CausalChance(station.index)).chance;
        }
        public bool FinishPhysicalChoice(PhysicalDecisionStation station,int choice)
        {
            if(!CanEnter(station)||station.Resolved||!station.node||choice<0||choice>=station.node.choices.Length)return false;
            var a=Branch(station.index);var c=station.branches[choice].Choice;
            if(!a.finished||a.selected!=choice)return false;
            var binding=station.branches[choice];if(binding&&binding.PacedTraversal&&!binding.MinimumMovementDone)return false;
            if(binding&&binding.RequiresPassageWork&&!binding.PassageWorkDone)return false;
            if(binding&&binding.requiredPassage&&(!binding.requiredPassage.State.secured||!binding.requiredPassage.State.crossed))return false;
            var s=SibucaoSession.Instance;var f=s.GetComponent<FloodSimulationManager>();var tier=f?f.GetWaterTier(station.transform.position):WaterTier.Tier1;
            var lossesBefore=new HashSet<string>(Data.flags.Where(x=>x.StartsWith("lost:")));
            float rain=s.weather?Mathf.InverseLerp(.6f,1.35f,s.weather.Intensity):0;
            float causalModifier=CausalChance(station.index);
            var evaluated=SibucaoDecisionEvaluation.Evaluate(c,Data.role,Data.seed,station.index,choice,tier,rain,causalModifier);
            float roll=evaluated.roll,chance=evaluated.chance;bool success=evaluated.success;int score=evaluated.rawPoints;
            int category=(int)c.scoreCategory,before=s.state.scores[category],decisionBefore=s.state.scores[(int)ScoreCategory.DecisionMaking];
            SibucaoDecisionEvaluation.ApplyPoints(s.state.scores,c.scoreCategory,score);
            var variant=station.GetComponent<FloodEncounterAdapter>();
            var record=new PhysicalChoiceResult{difficulty=s.state.difficultyLevel,localFloodDepth=f?f.GetWaterDepth(station.transform.position):0,floodVariant=variant&&variant.State!=null?variant.State.variantId:"street",node=station.index,choice=choice,nodeId=station.node.id,choiceId=c.id,category=c.scoreCategory.ToString(),rawPoints=score,appliedPoints=s.state.scores[category]-before,success=success,roll=roll,chance=chance,tier=tier,startedAt=a.enteredAt,finishedAt=s.state.homeSeconds,activeSeconds=a.activeSeconds,distance=a.distance,healthBefore=Data.health,staminaBefore=Data.stamina};
            record.scoringRecorded=true;record.secondaryPoints=c.scoreCategory==ScoreCategory.DecisionMaking?0:s.state.scores[(int)ScoreCategory.DecisionMaking]-decisionBefore;
            if(binding&&binding.requiredPassage&&s.passages)s.passages.RecordWork(record);
            // Obstruction work and travel consume actual time while the shared clock runs.
            // Never add an invisible block of time, reset a local storm, or change weather on success.
            Data.health=Mathf.Max(0,Data.health-c.healthCost);Data.stamina=Mathf.Max(0,Data.stamina-c.staminaCost);
            var consequence=success?c.onSuccess:c.onFailure;
            if(!binding||!binding.requireContact||!Data.flags.Contains("contact_handled_"+station.index))ApplyConsequence(station.index,consequence,c,record);
            // Treatment already consumes its placed physical kit. Do not consume a second kit on completion.
            if(!string.IsNullOrEmpty(c.consumedItemTag)&&(!binding||binding.mechanism!=PhysicalMechanism.TreatThenCarry||!a.actionStarted))s.inventory.RemoveTag(c.consumedItemTag,false);
            if(success&&!string.IsNullOrEmpty(c.successFlag))SetFlag(c.successFlag);
            if(c.delayedConsequenceFlag&&!string.IsNullOrEmpty(c.delayedFlagId))
            {
                if(Data.pending==null)Data.pending=new List<PendingPhysicalConsequence>();
                QueueConsequence(c.delayedFlagId,station.index,c.delayedAtDecision);SetFlag(c.delayedFlagId);
            }
            record.healthAfter=Data.health;record.staminaAfter=Data.stamina;record.consequence=consequence.ToString();
            if(Data.history==null)Data.history=new List<PhysicalChoiceResult>();Data.history.Add(record);Data.results[station.index]=choice;
            SibucaoControls.ResetGrip();station.ShowPhysicalResolution(success,consequence);
            s.Notify("Route updated",station.index==0?"Crossing complete. Continue along the street.":"Situation recorded. Continue along the route.");
            record.lostItems=Data.flags.Where(x=>x.StartsWith("lost:")&&!lossesBefore.Contains(x)).Select(x=>x.Substring(5)).ToArray();
            record.causalChanceModifier=causalModifier;RecordCausalChoice(record);
            if(station.resolutionBeat)station.resolutionBeat.Play(choice);
            if(s.story)s.story.QueueCompletedAction(binding);
            s.RequestCheckpoint();
#if UNITY_EDITOR
            Debug.Log("STORMSIM_DECISION "+JsonUtility.ToJson(record));
#endif
            return true;
        }
        void ApplyConsequence(int index,PhysicalConsequence effect,DecisionChoice c,PhysicalChoiceResult record)
        {
            var s=SibucaoSession.Instance;
            if((effect&PhysicalConsequence.LoseItem)!=0)record.lostItem=s.inventory.RemoveTag(c.lossItemTag,true);
            if((effect&PhysicalConsequence.Injury)!=0){Data.health=Mathf.Max(0,Data.health-c.failureHealthCost);s.state.speedMultiplier=Mathf.Min(s.state.speedMultiplier,.82f);SetFlag("injury_from_"+index);}
            if((effect&(PhysicalConsequence.Exhaustion|PhysicalConsequence.Stumble))!=0)Data.stamina=Mathf.Max(0,Data.stamina-c.failureStaminaCost);
            if((effect&PhysicalConsequence.LeaveResident)!=0){SetFlag("resident_left_"+index);QueueConsequence("resident_left_"+index,index,8);}
            if((effect&PhysicalConsequence.Diversion)!=0)SetFlag("boat_diverted");
            if((effect&PhysicalConsequence.DelayedInjury)!=0)
            {
                if(Data.pending==null)Data.pending=new List<PendingPhysicalConsequence>();
                QueueConsequence("swelling_"+index,index,Mathf.Min(8,index+1));
            }
            if((effect&(PhysicalConsequence.Stumble|PhysicalConsequence.Injury))!=0)
            {
                var response=s.player.GetComponent<SibucaoPhysicalFeedback>();if(response)response.Stumble();
            }
        }
        public void ApplyWorldContact(PhysicalDecisionStation station,int choice)
        {
            string flag="contact_handled_"+station.index;if(Data.flags.Contains(flag))return;
            var s=SibucaoSession.Instance;var tier=s.GetComponent<FloodSimulationManager>().GetWaterTier(s.player.transform.position);
            var c=station.branches[choice].Choice;bool success=SeedValue(Data.seed,station.index,choice,31)<Chance(station,choice,tier);
            ApplyConsequence(station.index,success?c.onSuccess:c.onFailure,c,new PhysicalChoiceResult());SetFlag(flag);
        }
        public void ApplyPending(int destination)
        {
            if(Data.pending==null)return;var s=SibucaoSession.Instance;
            foreach(var p in Data.pending)
            {
                if(p.applied||p.dueAt>destination||p.dueAt<0)continue;p.applied=true;SetFlag("resurfaced:"+p.id);
                if(p.id.StartsWith("swelling_")){Data.health=Mathf.Max(0,Data.health-5);s.state.speedMultiplier=Mathf.Min(s.state.speedMultiplier,.74f);var view=s.player.GetComponent<SibucaoPhysicalFeedback>();if(view)view.Stumble(.8f);AddConsequenceNotice(p.id,destination,"My leg is swelling from the earlier crossing. I cannot keep the same pace.");}
                else if(p.id.Contains("resident_left")){SetFlag("resident_unaccounted_at_"+destination);AddConsequenceNotice(p.id,destination,p.source==5?"The resident in the wheelchair has not reached this group. Give their location at the school desk.":"The collapsed evacuee has not reached this group. Give their location at the school desk.");}
                else if(p.id.Contains("manifest")||p.id.Contains("boat")){SetFlag("arrival_manifest_mismatch");AddConsequenceNotice(p.id,destination,"Your boat stopped at the other landing. The school desk needs your arrival details for the passenger list.");}
            }
        }
        public void QueueConsequence(string id,int source,int destination)
        {
            if(Data.pending==null)Data.pending=new List<PendingPhysicalConsequence>();
            if(!Data.pending.Exists(p=>p.id==id))Data.pending.Add(new PendingPhysicalConsequence{id=id,source=source,dueAt=destination});
        }
        public void Missed(PhysicalDecisionStation station)
        {
            if(causalModel&&!station.supplementary)return;
            if(station.supplementary&&(!station.Active||!station.Arrived&&Branch(station.index).selected<0)){Data.results[station.index]=98;return;}
            if(station.Resolved)return;Data.results[station.index]=99;SetFlag("missed_"+station.index);
            var s=SibucaoSession.Instance;int category=station.index==4?3:station.index==5||station.index==7?4:1;
            s.state.scores[category]=Mathf.Max(0,s.state.scores[category]-(station.supplementary?4:40));
            if(station.assist){SetFlag("resident_left_"+station.index);QueueConsequence("resident_left_"+station.index,station.index,8);}
        }
    }
}
