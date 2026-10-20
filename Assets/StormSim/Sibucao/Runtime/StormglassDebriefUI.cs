using UnityEngine;
using StormSim.Sibucao;

namespace StormSim.UI
{
    public sealed partial class StormglassUI
    {
        const int DebriefPages=5;

        void BuildCompletionDebrief()
        {
            reviewTab=Mathf.Clamp(reviewTab,0,DebriefPages-1);
            string[] names={"Outcome","Timeline","Decisions","People & supplies","Reflection"};
            Heading("SCENARIO COMPLETE  •  "+(reviewTab+1)+" / "+DebriefPages,names[reviewTab],"See what happened, then choose what to improve next time.");
            if(reviewTab==0)BuildOutcomePage();
            else if(reviewTab==1)BuildTimelinePage();
            else if(reviewTab==2)BuildDecisionPage();
            else if(reviewTab==3)BuildPeoplePage();
            else BuildReflectionPage();
            string previous=reviewTab==0?"Return to menu":"← "+names[reviewTab-1];
            string next=reviewTab==DebriefPages-1?"Finish review":"Next: "+names[reviewTab+1]+" →";
            ActionPair(previous,reviewTab==0?(System.Action)S.ReturnToMainMenu:()=>reviewTab--,next,reviewTab==DebriefPages-1?(System.Action)S.ReturnToMainMenu:()=>reviewTab++);
            if(reviewTab==DebriefPages-1)
            {
                Action("Replay this location",()=>S.Confirm("restart"),primary:false);
                Action("Choose another location",S.ReturnToMainMenu,primary:false);
            }
        }

        void BuildOutcomePage()
        {
            MessageCard("YOU REACHED SHELTER","Your arrival at school reception is recorded. Review your supplies, route decisions, and help given along the way.",StormglassTheme.Accent);
            for(int i=0;i<SibucaoDebrief.Categories.Length;i++)
            {
                int maximum=Mathf.Max(1,SibucaoDecisionEvaluation.Maximum((ScoreCategory)i));
                MeterCard(SibucaoDebrief.Categories[i],S.state.scores[i]+" / "+maximum,Mathf.Clamp01(S.state.scores[i]/(float)maximum),S.state.scores[i]<0);
            }
        }

        void BuildTimelinePage()
        {
            var experience=S.GetComponent<SibucaoEvacuationExperience>();var moments=experience?experience.State.moments:null;
            if(moments==null||moments.Count==0)MessageCard("NO TIMELINE EVENTS","The run contains no additional recorded learning moments.",StormglassTheme.Muted);
            else foreach(var moment in moments)MessageCard(moment.action,moment.result,StormglassTheme.Accent);
        }

        void BuildDecisionPage()
        {
            var history=S.state.physical.history;
            if(history==null||history.Count==0)MessageCard("NO RECORDED DECISIONS","No completed physical decision was recorded.",StormglassTheme.Warning);
            else foreach(var record in history)MessageCard(SibucaoDebrief.DecisionTitle(S,record),SibucaoDebrief.DecisionOutcome(record),record.success?StormglassTheme.Accent:StormglassTheme.Warning);
        }

        void BuildPeoplePage()
        {
            MessageCard("PEOPLE & FOLLOW-UP",SibucaoDebrief.Section(S,"RESIDENTS & FOLLOW-UP","UNRESOLVED SITUATIONS"),StormglassTheme.Accent);
            MessageCard("UNRESOLVED SITUATIONS",SibucaoDebrief.Section(S,"UNRESOLVED SITUATIONS","SUPPLIES AT HANDOVER"),StormglassTheme.Warning);
            MessageCard("SUPPLIES AT HANDOVER",SibucaoDebrief.Section(S,"SUPPLIES AT HANDOVER","LATER CONSEQUENCES"),StormglassTheme.Accent);
        }

        void BuildReflectionPage()
        {
            MessageCard("LATER CONSEQUENCES",SibucaoDebrief.Section(S,"LATER CONSEQUENCES","REFLECTION"),StormglassTheme.Warning);
            MessageCard("REFLECT","Which earlier action changed a later situation?\nWhat would you pack, report, or do differently on another run?",StormglassTheme.Accent);
            Text("This simulation is a learning exercise, not a live warning service. Follow official advisories during a real emergency.",18,StormglassTheme.Muted);
        }
    }
}
