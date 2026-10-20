using UnityEngine;
namespace StormSim.Sibucao
{
    public sealed partial class SibucaoPhysicalOption
    {
        // The introductory crossing is a continuous passage, not a sequence of
        // tiny foot targets. Other encounters retain their authored mechanisms.
        public bool IsFirstCrossing=>station&&station.index==0;
        float crossingOutsideSince=-1;

        public bool CrossingPosition(Vector3 position,out float progress,out float length,out float distance)
        {
            progress=length=0;distance=float.PositiveInfinity;
            if(path==null||path.Length<2)return false;
            float bestHeight=0;
            for(int i=1;i<path.Length;i++)
            {
                Vector3 start=path[i-1].position,end=path[i].position,delta=end-start;
                delta.y=0;float segment=delta.magnitude;
                if(segment<.001f)continue;
                Vector3 offset=position-start;offset.y=0;
                float rawT=Vector3.Dot(offset,delta)/(segment*segment);
                float t=Mathf.Clamp01(rawT);
                float lateral=(offset-delta*t).magnitude;
                // Keep the far-side exit as a corridor extension. Without this,
                // a player who walks naturally beyond the final marker becomes
                // "outside" the polyline before the completion frame can record.
                if(i==path.Length-1&&rawT>1)
                {
                    t=1;
                    lateral=(offset-delta*rawT).magnitude;
                }
                if(lateral<distance)
                {
                    distance=lateral;progress=length+segment*t;
                    bestHeight=rawT>1&&i==path.Length-1?end.y:Mathf.Lerp(start.y,end.y,t);
                }
                length+=segment;
            }
            return length>1&&Mathf.Abs(position.y-bestHeight)<2.5f;
        }

        void CancelFirstCrossing()
        {
            // A dropped probe or local lane correction is recoverable. Q still cancels.
            if(HasEncounter)return;
            var a=Attempt;a.engaged=false;a.selected=-1;a.step=0;
            crossingOutsideSince=-1;
        }

        void SampleFirstCrossing()
        {
            if(!tool||!tool.IsHeld||!tool.tip||station.Attempt.probeSamples>=2)return;
            var probe=tool.GetComponent<SibucaoDepthProbe>();if(!probe)probe=tool.gameObject.AddComponent<SibucaoDepthProbe>();
            if(!probe.TryRead(out Vector3 point,out float depth))return;
            var a=station.Attempt;
            if(a.probeSamples==0){a.firstProbe=point;a.firstProbeDepth=depth;a.probeSamples=1;probe.ShowReading(depth,0,false);if(S.story)S.story.MarkActionBeat(this);}
            else if(FlatDistance(point,a.firstProbe)>=.65f)
            {a.secondProbe=point;a.secondProbeDepth=depth;a.probeSamples=2;station.probed=true;station.manager.SetFlag("depth_probed_0");probe.ShowReading(depth,a.firstProbeDepth,true);if(S.story)S.story.MarkActionBeat(this);}
        }

        void TickFirstCrossing(float dt)
        {
            var player=S.player;
            bool onLevel=CrossingPosition(player.transform.position,out float progress,out float length,out float lateral);
            bool moving=player.MoveInput.sqrMagnitude>.01f;
            if(FloodAdapted&&mechanism==PhysicalMechanism.TalkThenWalk&&(!Engaged||!Attempt.actionStarted))return;
            if(!Engaged)
            {
                crossingOutsideSince=-1;
                if(mechanism==PhysicalMechanism.ProbeThenWalk)
                {
                    // A player may pick up the stick while this street is still dry.
                    // Start its branch when water arrives without requiring a re-grip.
                    if(station.EngagedBranch||!Available||!tool||!tool.IsHeld||!Begin(true))return;
                }
                else
                {
                float entryWidth=FloodAdapted?.52f:mechanism==PhysicalMechanism.Walk?1.45f:1.1f;
                if(station.EngagedBranch||!Available||!onLevel||lateral>entryWidth||progress>length*.5f||!moving)return;
                if(mechanism==PhysicalMechanism.SprintPassage&&!player.Sprinting)return;
                if(!Begin())return;
                }
            }
            var a=Attempt;
            float moved=Vector3.Distance(player.transform.position,a.previousPosition);
            a.previousPosition=player.transform.position;
            bool walking=moving&&moved>dt*.025f&&moved<1.2f;

            if(mechanism==PhysicalMechanism.ProbeThenWalk)
            {
                SampleFirstCrossing();
                // Putting down an unfinished tool must not latch a movement task.
                if(station.Attempt.probeSamples<2&&(!tool||!tool.IsHeld)){CancelFirstCrossing();return;}
            }
            if(!onLevel||lateral>1.5f)
            {
                // The stick starts beside the road, away from the boards. Allow
                // the player to carry it to the entry before judging lane departure.
                if(mechanism==PhysicalMechanism.ProbeThenWalk&&a.step==0&&
                    FlatDistance(player.transform.position,station.transform.position)<12)
                {crossingOutsideSince=-1;return;}
                if(crossingOutsideSince<0)crossingOutsideSince=Time.time;
                if(Time.time-crossingOutsideSince>.35f)CancelFirstCrossing();
                return;
            }
            crossingOutsideSince=-1;
            if(!walking)return;
            if(mechanism==PhysicalMechanism.SprintPassage&&!player.Sprinting)return;
            a.distance+=moved;a.activeSeconds+=dt;

            // The first half is an entry region, so a shoulder approach or a
            // probe made beside the boards does not require walking backwards.
            if(a.step==0&&progress<=length*.55f)a.step=1;
            if(a.step==0)return;
            if(mechanism==PhysicalMechanism.ProbeThenWalk&&station.Attempt.probeSamples<2)return;
            float along=0;
            for(int i=1;i<path.Length;i++)
            {
                along+=FlatDistance(path[i-1].position,path[i].position);
                if(i==a.step&&progress>=along-.65f)a.step++;
            }
            // Entry, real travel and reaching the far side are all necessary.
            // Waiting, an endpoint teleport or bypassing the passage earns nothing.
            if(a.step<path.Length||progress<length-.65f||a.distance<length*.4f||!MinimumMovementDone)return;
            if(FinishEncounterChoice())
            {
                a.engaged=false;
            }
        }
    }
}
