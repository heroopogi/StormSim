using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormSim.Sibucao
{
    [Serializable] public sealed class RouteMissionProgress
    {
        public string id;
        public int step;
        public float amount, startedAt, activeSeconds;
        public bool complete, started;
        public Vector3 carriedPosition;
        public bool hasCarriedPosition;
        public int escortPoint;
        public float carryDistance;
        public Vector3[] companionPositions;
        public Quaternion[] companionRotations;
        public string preparationChoice;
    }

    public enum RouteWork { Talk, Slide, Turn, Carry, Escort, Check, Preparation }
    public enum RoutePreparationUse { None, Advisory, ParentAssist }

    [Serializable] public sealed class RouteMissionStep
    {
        public string instruction, response;
        public RouteWork work;
        public RoutePreparationUse preparationUse;
        public Transform item, target;
        public Transform originalParent;
        public Transform attachAfter;
        public Vector3 from, to, fromAngle, toAngle;
        public float seconds = 3;
        public GameObject reveal;
        public Light lamp;
    }

    [Serializable] public sealed class RouteSectionLayout
    {
        public int home;
        public int difficulty;
        public Vector3 approach, left, right, merge;
        public Vector3[] leftPath, rightPath;
        public Vector3[] leftApproach, rightApproach;
        public bool leftDeck, rightDeck;
        public bool fittedRamps;
        public Vector3 entryGround, exitGround;
    }

    // A side mission owns its work, while its parent section owns ordered route progression.
    public sealed class SibucaoRouteChoices : MonoBehaviour
    {
        public SibucaoRouteSection[] sections;
        public SibucaoSideMission Working { get; private set; }
        public SibucaoSideMission HeldBy { get; private set; }
        // Informational preview only. The player still commits by physically entering a passage.
        public int PreviewSelection { get; private set; } = 1;
        float nextCall;
        int callCursor;
        public SibucaoRouteSection Current
        {
            get
            {
                var s = SibucaoSession.Instance;
                if (!s || !s.physicalDecisions || sections == null) return null;
                int stage = s.physicalDecisions.NextCoreStage;
                foreach (var section in sections) if (section && section.station.index == stage) return section;
                return null;
            }
        }
        public void StopVoices()
        {foreach(var section in sections){if(section.centerVoice)section.centerVoice.Stop();if(section.left&&section.left.voice)section.left.voice.Stop();if(section.right&&section.right.voice)section.right.voice.Stop();}}
        public bool TryDepartureTarget(out Vector3 target)
        {
            target=Vector3.zero;var s=SibucaoSession.Instance;if(!s||Working||sections==null)return false;
            int previous=s.physicalDecisions.NextCoreStage-1;
            bool coastal=s.GetComponent<CoastalAreaProfile>();
            if(previous<0){if(coastal&&SibucaoStreetConnections.TryRemaining(s,-1,out var departure,out int first)){target=departure[first];return true;}return false;}
            if(previous>=s.state.physical.results.Length||s.state.physical.results[previous]<(coastal?0:100))return false;
            foreach(var section in sections)if(section&&section.station.index==previous&&section.TryExitTarget(s.player.transform.position,out target))return true;
            if(TryStreetConnection(out var path,out int start)){target=path[start];return true;}
            return false;
        }
        public bool TryStreetConnection(out Vector3[] path,out int start)
        {
            path=null;start=0;var s=SibucaoSession.Instance;if(!s||Working)return false;
            int previous=s.physicalDecisions.NextCoreStage-1;
            bool coastal=s.GetComponent<CoastalAreaProfile>();
            if(previous<0)return coastal&&SibucaoStreetConnections.TryRemaining(s,-1,out path,out start);
            if(previous>=s.state.physical.results.Length||s.state.physical.results[previous]<(coastal?0:100))return false;
            foreach(var section in sections)if(section&&section.station.index==previous&&section.TryExitTarget(s.player.transform.position,out _))return false;
            return SibucaoStreetConnections.TryRemaining(s,previous,out path,out start);
        }
        public bool Previewing
        {
            get
            {
                var c = Current; var s = SibucaoSession.Instance;
                if(s&&s.missionGuide&&s.missionGuide.CurrentStation&&s.missionGuide.CurrentStation.supplementary)return false;
                return c && !Working && !c.station.EngagedBranch &&
                    Flat(s.player.transform.position, c.Approach) < 26 &&
                    Vector3.Dot(s.player.transform.position - c.Approach, c.station.transform.forward) < 14;
            }
        }
        public string Objective => Working ? "WORK — "+Working.title : Previewing ? "NOTICE — inspect LEFT, AHEAD and RIGHT." : null;
        public string Hint => Working ? Working.Hint : Previewing ? "INSPECT: looking is safe and records nothing. COMMIT: approach one passage and use its person or object. WORK: finish its 2–3 actions. CONSEQUENCE: reach the exit and see what changed." : null;
        public Vector3 NavigationTarget => Working ? Working.WorkTarget : Previewing ? PreviewTarget(PreviewSelection) : Current ? Current.AccessTarget(SibucaoSession.Instance.player.transform.position) : transform.position;
        public void SelectPreview(int index) { PreviewSelection = Mathf.Clamp(index, 0, 2); }
        public Vector3 PreviewTarget(int index)
        {
            var section = Current;
            if (!section) return transform.position;
            if (index <= 0 && section.left) return section.left.transform.position;
            if (index >= 2 && section.right) return section.right.transform.position;
            return section.station ? section.station.NavigationTarget : section.transform.position;
        }
        public string Prompt
        {
            get
            {
                if (HeldBy) return HeldBy.Hint;
                var m = Focused(); return m ? m.Hint : null;
            }
        }
        public Transform ContextTarget
        {
            get
            {
                var mission=HeldBy?HeldBy:Focused();
                if(!mission)return null;
                var step=mission.Step;
                return step!=null&&step.item?step.item:mission.caller;
            }
        }
        public static float Flat(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
        public RouteMissionProgress Progress(string id)
        {
            var run = SibucaoSession.Instance.physicalDecisions.Data;
            if (run.sideMissions == null) run.sideMissions = new List<RouteMissionProgress>();
            var state = run.sideMissions.Find(p => p.id == id);
            if (state == null) { state = new RouteMissionProgress { id = id }; run.sideMissions.Add(state); }
            return state;
        }
        public SibucaoSideMission Focused()
        {
            var s = SibucaoSession.Instance; var c = Current;
            if (!s || !c || !s.player || !s.player.view) return null;
            var cam = s.player.view.transform;
            var hits = Physics.RaycastAll(cam.position, cam.forward, 3, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(s.player.transform)) continue;
                var mission = hit.collider.GetComponentInParent<SibucaoSideMission>();
                return mission && mission.section == c && mission.CanUse(hit.transform) ? mission : null;
            }
            return null;
        }
        public bool Interact()
        {
            if (HeldBy) { HeldBy.ReleaseCarry(); return true; }
            var mission = Focused();
            return mission && mission.Interact();
        }
        public void Begin(SibucaoSideMission mission)
        {
            if (Working && Working != mission) Working.Cancel();
            var branch = mission.section.station.EngagedBranch;
            if (branch) branch.CancelInteraction();
            Working = mission;
            SibucaoSession.Instance.physicalDecisions.Data.activeSideMission=mission.id;
            SibucaoSession.Instance.physicalDecisions.Data.sideWorkActive=true;
            var manager = SibucaoSession.Instance.physicalDecisions;
            manager.EnterCausalStage(mission.section.station.index);
            manager.ApplyPending(mission.section.station.index);
            var p = mission.State;
            if (!p.started) { p.started = true; p.startedAt = SibucaoSession.Instance.state.homeSeconds; }
        }
        public void CaptureProgress()
        {
            var data=SibucaoSession.Instance.physicalDecisions.Data;
            if(Working)data.activeSideMission=Working.id;
            data.sectionLifts=new float[sections.Length];
            for(int n=0;n<sections.Length;n++)
            {
                var section=sections[n];data.sectionLifts[n]=section.FloodLift;
                foreach(var mission in new[]{section.left,section.right})
                {if(!mission.State.started)continue;mission.State.companionPositions=new Vector3[mission.companions.Length];mission.State.companionRotations=new Quaternion[mission.companions.Length];for(int i=0;i<mission.companions.Length;i++)if(mission.companions[i]){mission.State.companionPositions[i]=mission.transform.InverseTransformPoint(mission.companions[i].position);mission.State.companionRotations[i]=Quaternion.Inverse(mission.transform.rotation)*mission.companions[i].rotation;}}
            }
        }
        public SibucaoSideMission RecoverableMission
        {
            get {if(Working)return Working;var c=Current;if(!c)return null;string id=SibucaoSession.Instance.physicalDecisions.Data.activeSideMission;return c.left.id==id?c.left:c.right.id==id?c.right:null;}
        }
        public void ResumeSavedWork()
        {
            var data=SibucaoSession.Instance.physicalDecisions.Data;
            for(int i=0;i<sections.Length;i++)if(data.sectionLifts!=null&&i<data.sectionLifts.Length)sections[i].RestoreFloodLift(data.sectionLifts[i]);
            Working=data.sideWorkActive?RecoverableMission:null;
            if(Working)Working.Restore();
        }
        public void SetHeld(SibucaoSideMission mission) { HeldBy = mission; }
        public void Release(SibucaoSideMission mission)
        {
            if (HeldBy == mission) HeldBy = null;
            if (Working == mission) {Working = null;SibucaoSession.Instance.physicalDecisions.Data.sideWorkActive=false;}
        }
        public bool AllowsCenter(PhysicalDecisionStation station, bool deliberate)
        {
            if (station.supplementary || sections == null) return true;
            var section = Array.Find(sections, c => c && c.station == station);
            if (!section) return true;
            if (Working) return false;
            if (deliberate) return true;
            // Side visitors cannot accidentally select a center walk branch through overlapping sensors.
            var p = station.transform.InverseTransformPoint(SibucaoSession.Instance.player.transform.position);
            return Mathf.Abs(p.x) < 2.8f && p.z > -5;
        }
        public void ApplyLayout()
        {
            Working = HeldBy = null; PreviewSelection = 1; nextCall = 0; callCursor = 0;
            if (sections == null) return;
            foreach (var section in sections) if (section) section.ApplyLayout(SibucaoSession.Instance.state.homeIndex);
        }
        void Update()
        {
            var s = SibucaoSession.Instance;
            if(sections!=null)foreach(var section in sections)if(section&&section.centerVoice)section.centerVoice.volume=SibucaoAudioMix.Speech;
            if (!s || s.Mode != SibucaoMode.Playing || s.home && s.home.MovementLocked) return;
            if (s.inventory && s.inventory.BlocksInput || s.missionGuide && s.missionGuide.BlocksInput) return;
            bool cancel = Keyboard.current != null && StormSim.Sibucao.SibucaoControls.Button(UnityEngine.InputSystem.Key.Q).wasPressedThisFrame;
#if UNITY_EDITOR
            if (SibucaoEditorInput.Active) cancel |= SibucaoEditorInput.Cancel;
#endif
            if (cancel && Working) Working.Cancel();
            if (Working)
            {
                if (!Working.WithinWorkArea(s.player.transform.position)) Working.Cancel();
                else Working.Tick(Time.deltaTime);
            }
            var c = Current;
            if (!c || Working) return;
            float approachDistance=Flat(s.player.transform.position,c.Approach);
            if(approachDistance<=20&&!c.station.Active)c.station.Activate(true);
            if(Time.time < nextCall || Time.unscaledTime < s.subtitleUntil) return;
            if (approachDistance > 20) return;
            int cue = callCursor++ % 3;
            if (c.station.approachSource && c.station.approachSource.isPlaying) { nextCall = Time.time + 3; return; }
            if (cue == 1)
            {
                StopVoices();s.Say("Ahead: " + c.centerLine, Mathf.Max(6,c.centerVoice&&c.centerVoice.clip?c.centerVoice.clip.length+.5f:6));
                if (c.centerVoice && c.centerVoice.clip){c.centerVoice.volume=SibucaoAudioMix.Speech;c.centerVoice.Play();}
                nextCall = Time.time + 11; return;
            }
            var mission = cue == 0 ? c.left : c.right;
            if (!mission) { nextCall = Time.time + 3; return; }
            mission.Call(); nextCall = Time.time + 11;
        }
        public void Complete(SibucaoSideMission mission)
        {
            var manager = SibucaoSession.Instance.physicalDecisions;
            if (manager.NextCoreStage != mission.section.station.index || mission.State.complete ||
                mission.State.step < mission.steps.Length || Flat(SibucaoSession.Instance.player.transform.position, mission.section.Merge) >= 3) return;
            mission.State.complete = true;
            manager.CompleteSideMission(mission);
            Release(mission);
            SibucaoSession.Instance.RequestCheckpoint();
        }
    }

    public sealed partial class DecisionTreeManager
    {
        public void CompleteSideMission(SibucaoSideMission mission)
        {
            int stage = mission.section.station.index;
            if (NextCoreStage != stage || !mission.State.complete) return;
            var s = SibucaoSession.Instance;
            // 100/101 are explicit side outcomes, never aliases for a center rescue or crossing.
            int result = mission.side == 0 ? 100 : 101;
            Data.results[stage] = result;
            SetFlag("side_completed:" + mission.id);
            SetFlag("center_unattempted_" + stage);
            var category = mission.teamwork ? ScoreCategory.Teamwork : ScoreCategory.Evacuation;
            int before = s.state.scores[(int)category],decisionBefore=s.state.scores[(int)ScoreCategory.DecisionMaking];
            SibucaoDecisionEvaluation.ApplyPoints(s.state.scores, category, 30);
            var record = new PhysicalChoiceResult { node = stage, choice = result,
                nodeId = "route_section_" + stage, choiceId = mission.id, category = category.ToString(),
                success = true, chance = 1, rawPoints = 30, appliedPoints = s.state.scores[(int)category] - before,
                startedAt = mission.State.startedAt, finishedAt = s.state.homeSeconds, activeSeconds = mission.State.activeSeconds,
                healthBefore = Data.health, healthAfter = Data.health, staminaBefore = Data.stamina, staminaAfter = Data.stamina,
                consequence = "Side mission completed; center outcome remains unconfirmed", difficulty = s.state.difficultyLevel };
            record.scoringRecorded=true;record.secondaryPoints=s.state.scores[(int)ScoreCategory.DecisionMaking]-decisionBefore;
            Data.history.Add(record); RecordCausalChoice(record);
            if (stage == 5 || stage == 7)
                AddConsequenceNotice("center_unconfirmed_" + stage, 9,
                    stage == 5 ? "You assisted people on the side passage. The wheelchair resident's arrival is still unconfirmed; report their location at the school." :
                    "You completed the side mission. The collapsed evacuee's arrival is still unconfirmed; report their location at the school.");
            s.Say(mission.completionLine, 5);
        }
    }
}
