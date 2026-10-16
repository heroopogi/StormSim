using System;
using System.Linq;
using UnityEngine;

namespace StormSim.Preparedness.Editor
{
    public static class PreparationExperienceChecks
    {
        public static string Run(PreparednessSession session)
        {
            void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);}
            session.EnsureInitial();
            Check(session.voiceDirector,"Preparation voice director is available");
            Check(session.voiceDirector.output,"Preparation voice output is available");
            Check(!session.voiceDirector.output.playOnAwake,"Voice never starts unexpectedly");
            Check(Mathf.Approximately(session.voiceDirector.output.spatialBlend,0),"Guidance voice is head-readable in VR");
            Check(PreparednessSession.Titles.Length==20&&PreparednessSession.Titles.All(t=>!string.IsNullOrWhiteSpace(t)),"All twenty preparation decisions retain readable task names");
            Check(PreparationNarration.ChecklistSteps.Length==20&&PreparationNarration.ChecklistSteps.All(t=>!string.IsNullOrWhiteSpace(t)),"All twenty checklist decisions have fixed recordable narration");
            Check(PreparationNarration.AfterTelevisionGuided.Contains("go-bag")&&PreparationNarration.AfterTelevisionGuided.Contains("press J"),"The post-television cue gives an immediate action and checklist control");
            Check(!session.television||string.IsNullOrEmpty(session.television.text),"Physical television has no text overlay");
            var television=session.GetComponent<PreparationTelevision>();
            Check(!television||television.dayBeforeNews,"Preparation television has a day-before broadcast assigned");
            Check(!television||television.PlaybackRequested,"Preparation television requests playback during the opening");
            var fieldGuide=session.player.GetComponent<PreparationChecklistProp>();
            Check(fieldGuide&&fieldGuide.BookReady&&fieldGuide.PageCount==2,"Animated two-page preparation book is attached to the player");
            Check(fieldGuide.HasPencil,"The handbook includes a physical pencil for completion checks");
            Check(fieldGuide.ChecklistFontName.Contains("RobotoMono-Bold"),"The physical checklist uses the selected readable handbook font");
            Check(session.items.Where(i=>i&&i.portable).All(i=>i.Body&&i.GetComponentInChildren<Collider>()),"Every portable preparation item has a rigidbody and collider");
            var inspectableItems=session.items.Where(i=>i&&!i.IsZone&&i.kind!=PrepKind.Decoration).ToArray();
            Check(inspectableItems.All(i=>i.GetComponentInChildren<Collider>()),"Every individual preparation item can be targeted by the crosshair");
            Check(session.items.Where(i=>i).All(i=>!string.IsNullOrWhiteSpace(session.ItemPurpose(i))),"Every preparation item has a readable purpose");
            Check(session.items.Where(i=>i).All(i=>session.player.ItemInformation(i).Contains("NAME\n")&&session.player.ItemInformation(i).Contains("PURPOSE\n")),"Every item inspection card explicitly labels its name and purpose");
            Check(!session.player.ItemInfoOpen,"Detailed item purpose is hidden by default");
            Check(session.items.Where(i=>i).All(i=>!session.player.ItemInformation(i).Contains("STATUS\n")),"Item inspection cards omit status");
            var inspectionTarget=inspectableItems.First();
            session.player.focused=inspectionTarget;
            Check(session.player.ToggleItemInfo()&&session.player.ItemInfoOpen,"R/info action reveals the focused item's details");
            var nextInspectionTarget=inspectableItems.First(i=>i!=inspectionTarget);
            session.player.focused=nextInspectionTarget;
            Check(!session.player.ItemInfoOpen,"Item details close when the crosshair moves to a different item");
            var zone=session.items.FirstOrDefault(i=>i&&i.IsZone);session.player.focused=zone;
            Check(!session.player.ToggleItemInfo(),"Placement zones never replace individual item information");
            session.player.focused=null;
            session.player.ClaimDesktopControl();
            Check(session.player.XRActive||Cursor.lockState==CursorLockMode.Locked,"Keyboard input can reclaim desktop control without a mouse click");
            session.SpeakCue("checklist.next.0","Household guide",PreparationNarration.ChecklistStep(0));
            Check(session.CurrentInstructionText==PreparationNarration.ChecklistStep(0),"Latest narrated instruction remains available as small persistent text");
            session.SpeakCue("validation.subtitle","Household guide","Audio-ready subtitle check");
            Check(session.message.Contains("Audio-ready subtitle check")&&session.voiceDirector.LastCueId=="validation.subtitle","A missing clip safely falls back to subtitles");
            Check(session.voiceDirector.SubtitleVisible&&PreparationVoiceDirector.SubtitleChunk("one two three four five six seven",0).Split(' ').Length<=6,"Narration subtitles are timed and limited to six words");
            Check(StormSim.Sibucao.SibucaoDesktopHud.NarrationSubtitleStyle.fontStyle==FontStyle.Italic,"Narration subtitles use the italic accessibility style");
            return "PASS: preparation voice director and silent fallback\nPASS: non-autoplay, readable VR voice output\nPASS: all twenty preparation task names\nPASS: all twenty checklist steps have fixed recordable narration\nPASS: post-television narration gives the first action and checklist control\nPASS: physical TV has video assigned and no text overlay\nPASS: television playback begins during the Preparation opening\nPASS: Option A animated two-page handbook is attached\nPASS: physical pencil records completed checks\nPASS: handbook uses readable Roboto Mono Bold\nPASS: every portable item has a rigidbody and collider\nPASS: every individual preparation item can be targeted by the crosshair\nPASS: every item has an explicitly labelled name and purpose\nPASS: item purpose stays hidden until requested\nPASS: item inspection cards omit status\nPASS: R/info action reveals only the focused item's details\nPASS: changing crosshair target closes the previous item's details\nPASS: placement zones do not replace individual item information\nPASS: keyboard input can reclaim desktop control\nPASS: latest narration remains visible as a small instruction\nPASS: imported narration receives italic six-word subtitles\nPASS: stable cue IDs preserve subtitles before narration is imported";
        }
    }
}
