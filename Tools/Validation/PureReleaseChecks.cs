using System;
using StormSim.Sibucao;
using UnityEngine.InputSystem;

class PureReleaseChecks
{
    static int passed,failed;
    static void Check(bool value,string name){Console.WriteLine((value?"PASS ":"FAIL ")+name);if(value)passed++;else failed++;}
    static int Main()
    {
        int[] caps={1500,1200,1000,1000,300};
        for(int i=0;i<5;i++)
        {
            var scores=new int[5];var category=(ScoreCategory)i;
            Check(SibucaoDecisionEvaluation.Maximum(category)==caps[i],"Expected score denominator "+category);
            SibucaoDecisionEvaluation.ApplyPoints(scores,category,10000);
            Check(scores[i]==caps[i]&&SibucaoDecisionEvaluation.Fraction(category,scores[i])==1,"Saturation fills "+category);
            SibucaoDecisionEvaluation.ApplyPoints(scores,category,-10000);Check(scores[i]==0,"Penalty floor "+category);
        }
        var score=new int[5];SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.DecisionMaking,100);
        Check(score[2]==100,"Primary Decision-Making receives no duplicate credit");
        score=new int[5];SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.Teamwork,30);Check(score[4]==30&&score[2]==9,"Side mission credit 30 plus 9 secondary");
        score[2]=997;SibucaoDecisionEvaluation.ApplyPoints(score,ScoreCategory.Evacuation,30);Check(score[2]==1000,"Secondary saturation");
        var settings=new SibucaoSettings();SibucaoControls.Ensure(settings);Check(settings.keyBindings.Length==14,"Fourteen initialized bindings");
        Check(SibucaoControls.Rebind(settings,4,Key.F)&&settings.keyBindings[4]=="F"&&settings.keyBindings[5]=="E","Duplicate bindings swap");
        Check(!SibucaoControls.Rebind(settings,4,Key.Escape),"Pause key protected");
        Check(!SibucaoControls.Rebind(settings,4,Key.Tab),"Bag alias protected");
        Check(!SibucaoControls.Rebind(settings,4,Key.RightShift),"Run alias cannot also trigger a remapped action");
        Check(!SibucaoControls.Rebind(settings,99,Key.Z),"Invalid binding rejected");
        Check(!SibucaoControls.Rebind(settings,4,(Key)9999),"Unknown input key rejected");
        settings.keyBindings[4]="9999";SibucaoControls.Ensure(settings);Check(settings.keyBindings[4]=="E","Corrupt persisted bindings recover to defaults");
        var run=new PhysicalRunData();Check(SibucaoDebrief.ResidentStatus(run,5,"Resident").Contains("unconfirmed"),"Missing rescue remains unconfirmed");
        run.history.Add(new PhysicalChoiceResult{node=5,choice=100,success=true});Check(SibucaoDebrief.ResidentStatus(run,5,"Resident").Contains("unconfirmed"),"Side completion does not rescue center resident");
        run.history[0].choice=0;Check(SibucaoDebrief.ResidentStatus(run,5,"Resident").Contains("requires confirmation"),"Assistance is distinct from arrival");
        run.flags.Add("resident_left_5");Check(SibucaoDebrief.ResidentStatus(run,5,"Resident").Contains("unconfirmed"),"Abandoned resident remains unresolved");
        Check(SibucaoDebrief.ConsequenceText("Injury, DelayedInjury").Contains("immediate injury")&&SibucaoDebrief.ConsequenceText("Injury, DelayedInjury").Contains("later effect"),"Debrief distinguishes immediate and delayed effects");
        Check(SibucaoDebrief.ConsequenceText("No action recorded") == "No action recorded","Legacy consequence text preserved");
        Console.WriteLine($"{passed} passed, {failed} failed. Production pure functions only; Unity integration not exercised.");
        return failed==0?0:1;
    }
}
