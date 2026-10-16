# SIBUCAO_RAIN improvement checklist

Created: 29 September 2026  
Last updated: 30 September 2026  
Scope: the seven agreed improvement batches for SIBUCAO_RAIN, preserving its low-poly style.

**Display requirement: keep the game and Game view at 16:9 for all remaining batches. Use 1920×1080 as the reference resolution and other 16:9 sizes for validation.** The earlier non-16:9 captures were stress tests, not the intended presentation.

Detailed findings: [Gameplay review](SIBUCAO_RAIN_GAMEPLAY_REVIEW_20260929.md).

## Progress

**Overall: 35 / 43 tasks verified (81%). Batches accepted: 0 / 7.**

Batches 1–6 currently have thirty-five technically verified tasks; B6.2 and B6.4 were reopened after continuous traversal exposed a hidden pet interaction and a deck-to-walkway gap. The fresh final regression pass has 162 passing assertions, 18/18 side-route fixtures and valid route structure. Batch 7 continuous route and rebuilt-player validation are in progress. Human playtest acceptance remains pending.

| Batch | Focus | Verified tasks | Status | Acceptance check |
| --- | --- | --- | --- | --- |
| 1 | UI and controls | 7 / 7 | Implemented and verified | First-time-player check pending |
| 2 | Interactions and progression | 6 / 6 | Implemented and verified | Continuous player recovery/resume check pending |
| 3 | Flood and movement | 6 / 6 | Implemented and verified | Continuous player check pending |
| 4 | Outcomes and learning | 6 / 6 | Live logic and final 720p/1080p layout checks passed | Participant reflection pending |
| 5 | NPCs and mission pacing | 6 / 6 | Fresh NPC, assistance and audio checks passed | Human playtest acceptance pending |
| 6 | Low-poly presentation and accessibility | 4 / 6 | Pet visibility and deck connection corrections under physical preview; other checks pass | Pending fixes and human acceptance |
| 7 | Performance and release validation | 0 / 6 | Live regressions running; player build/profile pending | Pending |

**Next task: finish continuous coverage, rebuild and repeat the corrected desktop benchmark and recovery checks. The user authorized Batches 4–7 and selected this PC as the first desktop target. Human acceptance remains pending.**

Check a task only after its change is implemented and verified. Until then, keep it unchecked and record any partial progress or blocker in the work log. A batch is accepted only after its tasks and its acceptance check are complete. Update the counts, status, next task, and work log together; the totals in this document are maintained manually. Record an explicit scope decision if a proposed feature is intentionally omitted.

## Batch 1 — Clear UI and consistent controls

- [x] **B1.1** Simplify the HUD to one primary objective, one contextual interaction prompt, and compact navigation.
- [x] **B1.2** Use consistent grab, carry, pack, release, and inventory controls across preparation and evacuation.
- [x] **B1.3** Replace the long control strip with progressive, contextual instructions.
- [x] **B1.4** Clarify the first preparation task and distinguish optional household warnings from required work.
- [x] **B1.5** Reduce duplicate route labels and resize/reposition oversized signs.
- [x] **B1.6** Hide temporary skip and storm-clock controls in player builds while retaining deliberate development access.
- [x] **B1.7** Fix overlapping UI and reserve safe areas at different desktop resolutions and aspect ratios.

**Acceptance:** A first-time player can start, pack, leave home, and understand the first junction without assistance. Objective, action, navigation, vitals, and attribution remain readable without overlapping.

Validation: Runtime/editor compilation passed; 19 existing preparation flow checks and 11 new preparation control checks passed. Live-frame packing, evacuation carrying/packing, Q release, side-mission F handling, development gating, and all 18 sign dimensions were checked in Play Mode. Final guide/carry-priority regression checks passed across live frames. The actual Unity HUD rectangles passed six-resolution overlap/safe-area assertions. Screenshots were inspected at 1280×720 and 688×329, including both bag panels and the guide.

Evidence: [Implementation and validation notes](SIBUCAO_BATCH1_UI_CONTROLS.md), [screenshots and test reports](../Captures/Batch1_20260929/).
Remaining acceptance: a first-time player must complete the stated flow without assistance. A full player build and XR playtest were not part of this desktop pass.

## Batch 2 — Reliable interactions and progression

- [x] **B2.1** Make dragging direction, placement targets, and action completion clear and consistent.
- [x] **B2.2** Clearly distinguish previewing a route, beginning work, and committing to an outcome.
- [x] **B2.3** Provide recovery for dropped tools, cancelled actions, and blocked escorts.
- [x] **B2.4** Explain unfinished encounters that prevent school completion and provide an appropriate recovery path.
- [x] **B2.5** Improve inventory state feedback and identify lost, consumed, damaged, equipped, and stored items.
- [x] **B2.6** Add checkpoint saving with recoverable backups and verify mid-task loading without silently overwriting a manual save.

**Acceptance:** A player can make mistakes, cancel tasks, drop objects, leave and resume the game, and recover progress without restarting the entire run. School arrival does not leave an unexplained progression block.

Validation: Final runtime/editor compilation passed. All 39 recovery, interaction, inventory and save assertions passed; all 18 side-route fixtures passed. Structural validation passed for 9 centers, 90 action steps and 72 home-specific paths. Recovery, save selection, item status and mechanism prompts were visually inspected in 16:9 captures at 1280×720, with an additional school-recovery reference at 1920×1080. Default player size is 1920×1080.  
Evidence: [Batch 2 implementation record](SIBUCAO_BATCH2_INTERACTIONS_RECOVERY.md), [39/39 checks](../Captures/Batch2_20260929/recovery-save-checks.txt), [18/18 routes](../Captures/Batch2_20260929/route-interactions.txt), [route structure](../Captures/Batch2_20260929/route-structure.txt).  
Remaining acceptance: a continuous player run must demonstrate cancelling, dropping tools, recovering, leaving/resuming and reaching school without restarting. Scripted fixtures use synthetic input and teleports; standalone and XR validation remain pending.

## Batch 3 — Flood behavior and movement

- [x] **B3.1** Replace unexplained rising mission platforms with believable accessible structures or clearly communicated relocation.
- [x] **B3.2** Improve shallow/deep-water differences and current cues, documenting the flood model's intended scope.

  Agreed visual addition: adapt the existing ocean's colors, ripple texture, highlights and transparency to floodwater, retaining terrain-aware depth/current data and the low-poly style. Keep 16:9.
- [x] **B3.3** Rebalance ordinary walking stamina, exertion costs, recovery, and movement penalties.
- [x] **B3.4** Define and implement the intended hunger, zero-health, and swimming consequences, or explicitly simplify unnecessary meters.
- [x] **B3.5** Verify flood transitions, shore exits, ramps, companion access, and vehicle restrictions.
- [x] **B3.6** Profile and improve flood simulation and catch-up delays, including clear feedback during legitimate loading.

**Acceptance:** Flooding produces understandable challenges while the intended routes remain physically usable. Ordinary travel avoids excessive stop-and-rest cycles, and loading/catch-up does not look like unexplained broken movement.

Validation: Final runtime/editor compilation passed. All 63 flood/survival checks, 20 access checks, 10 compatibility checks, six swim/shore/refuge checks and three vehicle/low-breath checks passed. All 39 Batch 2 regressions and all 18 side routes passed. Physical ramp and companion support checks cover the first home across all five difficulties; static ramp grades cover all four homes. Direct field evaluation plus mesh upload measured 7.40 ms warm median in the Editor, with no historical replay on a one-hour restore. Final 1280×720 and 1920×1080 captures were inspected, including long subtitles, current labels, mandatory breath warnings and assistance recovery.  
Evidence: [Batch 3 implementation, reports and model scope](SIBUCAO_BATCH3_FLOOD_MOVEMENT.md), [screenshots and reports](../Captures/Batch3_20260930/).  
Remaining acceptance: a continuous human playtest must confirm travel, transitions and recovery. Standalone/XR performance and broader route coverage remain in Batch 7.

## Batch 4 — Outcomes, scoring, and learning

- [x] **B4.1** Normalize each score bar against its actual category maximum and display its denominator.
- [x] **B4.2** Review scoring consistency between center and side missions, including secondary Decision-Making credit.
- [x] **B4.3** Explain recorded decisions, random outcomes, and delayed consequences in the debrief.
- [x] **B4.4** Show rescued residents, unresolved situations, and remaining supplies in the results.
- [x] **B4.5** Explain practical role and difficulty differences without exposing assessment answers; verify intended starting-home availability.
- [x] **B4.6** Improve the school arrival, reception, and handover sequence before the debrief.

**Acceptance:** Players can explain what their actions changed and what they learned. Displayed scores match the recorded run and category limits; unresolved rescues remain accurately represented.

Validation: All 30 live release assertions passed, including score caps, signed secondary credit, resident status, home availability, route rejoin guidance and reception/handover order. All 48 synthetic UI captures were produced at 1280×720 and 1920×1080 with 100% and 130% text. All 32 layout assertions passed. The largest-text role, difficulty, score/debrief, controls and reception/handover views were inspected; the earlier clipping is corrected.

Evidence: [release checks](../Captures/Batches4-7_20260930/release-checks.txt), [UI capture log](../Captures/Batches4-7_20260930/UI/capture-log.txt), [screenshots](../Captures/Batches4-7_20260930/UI/). Participant reflection remains pending.

## Batch 5 — NPCs and mission pacing

- [x] **B5.1** Add missing alert, acknowledgement, pointing, and assistance reactions.
- [x] **B5.2** Improve wheelchair and injured-person handling, contact poses, and movement feedback.
- [x] **B5.3** Improve escort pacing, turning, spacing, obstruction recovery, and waiting/blocked messages.
- [x] **B5.4** Reduce repetitive interaction sequences and vary the rhythm of observation, decisions, assistance, and travel.
- [x] **B5.5** Improve crowd movement, foot contact, lane clearance, and behavior around flooding.
- [x] **B5.6** Add contextual voices, calls, mechanism sounds, placement feedback, and wet/dry movement sounds.

**Acceptance:** Residents respond to the player and the storm, escorts recover from ordinary obstructions, assistance looks purposeful, and mission sequences offer distinct experiences.

Validation: Fresh live checks pass for 69 humanoid actors, zero roaming full-height obstacle overlaps, three physical assistance flows (wheelchair, direct carry, treatment and transfer), 78 audio assertions and all 18 side-route fixtures. Pointing, seating and wet-weather movement captures were inspected. Procedural reactions and focused observation vary the authored interactions; first-time-player pacing and listening feedback remain unmeasured.  
Evidence: [NPC checks](../Captures/Batches4-7_20260930/NPC/checks.txt), [assistance](../Captures/Batches4-7_20260930/Assistance/checks.txt), [audio](../Captures/Batches4-7_20260930/Audio/checks.txt).

## Batch 6 — Low-poly presentation and accessibility

- [x] **B6.1** Unify material treatment, palette, proportions, and texture detail across the environment, characters, props, and hands.
- [ ] **B6.2** Improve lighting separation and interactable visibility while retaining the storm atmosphere.
- [x] **B6.3** Refine hand silhouettes and grip/reach/release poses, checking contact and clipping.
- [ ] **B6.4** Integrate decks, ramps, and mission props into the neighborhood with believable supports and transitions.
- [x] **B6.5** Improve landmarks and hazard readability through shape, color, movement, and environmental cues.
- [x] **B6.6** Add and verify remapping, text scaling, FOV, grip alternatives, reduced flashes, and separate speech/environment/effects volume controls; update the Controls page.

**Acceptance:** The low-poly art looks cohesive and remains readable during heavy weather. Each agreed accessibility option works across relevant gameplay phases without obscuring prompts or disrupting interactions.

Validation: Seven hand-geometry checks, 20 physical-access assertions, 45 packaged connection-data checks, 32 shared accessibility assertions and 12 preparation assertions at each of 720p/1080p pass. Maximum-text panels, assisted grips, material finish, supported decks and heavy-rain signs were inspected. Production rain collision reports zero nearby indoor drops. The 48 earlier UI captures and 32 layout assertions remain valid; affected preparation/guide/inventory screens were recaptured.  
Evidence: [hand geometry](../Captures/Batches4-7_20260930/hand-geometry.txt), [accessibility](../Captures/Batches4-7_20260930/live-accessibility.txt), [preparation](../Captures/Batches4-7_20260930/Preparation-720/), [visual review](../Captures/Batches4-7_20260930/NPC/visual-review.txt).

## Batch 7 — Performance and release validation

- [ ] **B7.1** Profile the standalone player on target hardware and optimize measured rendering, animation, physics, memory, and allocation bottlenecks.
- [ ] **B7.2** Resolve relevant desktop startup, duplicate-assembly, XR, and rendering warnings; distinguish editor-only issues from player issues.
- [ ] **B7.3** Record continuous route coverage for all 27 encounters, five difficulties, roles, and reachable starting layouts, identifying any untested combinations.
- [ ] **B7.4** Verify cancellation, dropped-object recovery, blocked escorts, saving/loading, late departures, and school arrival in the release candidate.
- [ ] **B7.5** Conduct first-time-player tests and record completion time, idle time, confusion, assistance needed, and remaining problems.
- [ ] **B7.6** Update the gameplay guide, controls, known issues, build identifier, and validation record to match the verified release candidate.

**Acceptance:** The intended player build meets a recorded performance target, passes the agreed gameplay coverage, has no unresolved progression blockers, and has documentation that matches its behavior.

Validation: Runtime/editor and development/release player-symbol source compilation passed. Thirty-two pure production-function checks passed; these verify scoring, binding rules, rescue status and consequence descriptions, not Unity integration or visual behavior.  
Evidence / remaining issues: [Batches 4–7 implementation and limits](SIBUCAO_BATCHES4_7_IMPLEMENTATION_20260930.md). Live checks, remaining NPC/art refinements, standalone measurements and participant acceptance are pending.

## Work log

Use task IDs when recording work. Link the relevant screenshot, test result, playtest note, or changed file where useful. Keep implementation and verification distinct.

| Date | Task / batch | Progress or decision | Verification / evidence | Next action |
| --- | --- | --- | --- | --- |
| 2026-09-29 | Planning | Created the seven-batch checklist from the agreed roadmap. No improvement tasks implemented yet. | Gameplay review dated 2026-09-29. | Begin B1.1. |
| 2026-09-29 | Batch 1 | Implemented shared HUD, consistent desktop actions, contextual teaching, optional-warning wording, smaller approach signs, and explicit development gating. Source backups are under Backups/Batch1_20260929. | All seven tasks technically verified. See Captures/Batch1_20260929. | Complete first-time-player acceptance. |
| 2026-09-29 | B2.1 / B2.4 | Broader route fixture reported 11 passes and seven failures in focused interaction, escort, or completion steps. These are unresolved findings, not claimed to be regressions or pre-existing defects without a baseline comparison. | SIBUCAO_THREE_WAY_INTERACTION_CHECKS.txt | Reproduce and diagnose during Batch 2. |
| 2026-09-30 | Batch 2 | User authorized proceeding while Batch 1 human acceptance remains pending. Implemented all six task areas; five have passed technical verification. | 35/36 new checks passed; 18/18 side routes passed; three 16:9 screens inspected. | Verify corrected escort blocking, added tool/write-failure checks and final screens. |
| 2026-09-30 | Batch 2 complete | All six tasks implemented and technically verified. Corrected blocked-escort detection, added tool/write-failure checks, and preserved 16:9 with a 1920×1080 player default. | 39/39 final checks, 18/18 routes and the route-structure validator passed. Final 1280×720 and 1920×1080 screenshots inspected; see Batch 2 record. | Continuous player acceptance; Batch 3 awaits authorization. |
| 2026-09-30 | Batch 3 | User authorized Batch 3 after discussing ocean-style flood graphics. Implemented fixed supported access, ocean-derived shading, terrain depth/current cues, revised exertion, breath/assistance recovery, vehicle restrictions and direct scenario restoration. | Initial compile succeeded; baseline grid step 16.06 ms, with up to 24 old steps per frame. Runtime and visual verification pending. | Run flood, movement, access and save regressions; inspect 16:9 references. |
| 2026-09-30 | Batch 3 verification | Verified flood/survival rules and continuous swim-to-ramp transitions. Found and fixed a deck-endpoint placement obstruction, hidden legacy-save notice and long subtitle overflow. Corrected access fixture starting-home setup and its unsupported 35° assumption; actual authored maximum is 37.6° and the player limit is 55°. | 62 flood/survival assertions, six swim/shore/refuge checks and 39 Batch 2 assertions passed. Four tasks verified; final compilation and regressions pending. | Finish B3.1/B3.5 and final 16:9 review. |
| 2026-09-30 | Batch 3 complete | All six tasks implemented and technically verified. Added underwater bag/guide closure so panels cannot block swimming while breath runs out. Preserved low-poly presentation and 16:9. | 63/63 flood/survival, 20/20 access, 10/10 compatibility, 6/6 swim/shore/refuge, 3/3 vehicle/low-breath, 39/39 Batch 2 regressions and 18/18 routes passed. Final 720p/1080p screenshots inspected. | Continuous human acceptance remains pending; Batch 4 awaits instruction. |
| 2026-09-30 | Batches 4–7 in progress | User authorized all remaining batches and chose desktop on this PC first. Implemented scoring/debrief/reception, shared accessibility settings and remapping, NPC/escort feedback, foley, focused-object outlines and a limited material/support pass. Broader NPC/art work remains. | Runtime/editor compilation and 29 pure checks passed. Unity app control was not approved; refresh requested. Regression pipeline, development build and staged benchmark are prepared but have not run. | Refresh Unity; run and repair live regressions, inspect 16:9 UI/art, complete desktop build/profile and human acceptance. |
| 2026-09-30 | Batches 4–7 continuation | Continued per user request. Added preparation settings/footsteps, fixed remapping display/alias conflicts, regenerated 27 obsolete narration clips, improved crowd/assistance/support/sign behavior, excluded duplicate local compiler dependencies and corrected a development-only compile error. Coverage is now derived from actual assets: 720 rows. | Runtime/editor and both player-symbol compilations passed; 32 production checks and 39 WAV/caption checks passed. No new Unity or player execution. | Unity control again rejected by tool; user refresh remains required for live regression, visual checks, build/profile and participant acceptance. |
| 2026-09-30 | Batches 4–7 live validation | Unity imported the runner. The first live pass passed 26 release, 39 interaction/save, 63 flood, 20 access and 10 compatibility assertions. Two of 18 side routes failed. Identified stale stroller rotation on reset and a three-person/two-slot escort deadlock; fixes compiled. Also fixed debrief side-mission lookup and large-text navigation/button scaling. | [Live release checks](../Captures/Batches4-7_20260930/release-checks.txt), [side diagnostics](../Captures/Batches4-7_20260930/side-diagnostics.json), [stroller reset confirmation](../Captures/Batches4-7_20260930/stroller-reset-confirmation.txt), [Unity compilation](../Captures/Batches4-7_20260930/unity-import-20260930.txt). | Rerun full pipeline; inspect UI/art and real input; build and benchmark; record continuous coverage and external acceptance honestly. |
| 2026-09-30 | B4.3 / B4.4 / B4.6 and regressions | Final full regression pass is clean: 26 release, 39 recovery/save, 63 flood, 20 access, 10 compatibility assertions, 18 side routes and route structure. Another 32 Input System/settings/debrief checks pass. Captured forty 16:9 release screens; debrief and reception/handover are readable. Found and corrected maximum-text clipping in difficulty, score notes, navigation and vitals; recapture pending. | [Live accessibility](../Captures/Batches4-7_20260930/live-accessibility.txt), [UI captures](../Captures/Batches4-7_20260930/UI/), [18 routes](../Captures/Batches4-7_20260930/side-interactions.txt). | Finish continuous route driver, art/pose review, corrected UI recapture and actual player build/profile. |

| 2026-09-30 | Batch 4 technical completion / Batches 5–7 continuation | Applied speech, hand, walkway, large-text and desktop-build corrections. The first development build succeeded with zero errors; its first two benchmark viewpoints were invalid and must be repeated. The school viewpoint met the target. | New live pass: 30 release, 39 recovery/save, 63 flood, 10 compatibility, 20 access assertions, 18 side routes and route structure all passed. Forty-eight 16:9 UI captures and 32 layout assertions passed; largest-text screens inspected. | Finish NPC/art/input/audio and continuous coverage; apply remaining observed corrections, rebuild, repeat actual-player checks and retain participant acceptance as pending. |

| 2026-09-30 | Batches 5–6 technical completion | Verified reaction rigs, crowd clearance, all three physical assistance flows, audio interruption, hand geometry, supported access, large-text preparation/guide/inventory and heavy-weather signs. Treatment now shares the clear direct-transfer lane after consuming the kit. | Fresh full regression: 162 assertions, 18 side routes and route structure pass without exceptions. See current NPC, Assistance, Audio, Preparation and Rain-rc2 reports. | Finish continuous routes and actual RC2 development/release player validation; participant acceptance remains pending. |

## Blockers and scope decisions

- Batch 1 acceptance remains pending a first-time-player run. Technical checks do not establish usability for an unfamiliar player.
- Batch 2 human acceptance remains pending a continuous recovery/resume run. The final 1280×720 mechanism capture also shows crowded distance/footing text in the navigation panel; carry this visual issue into B6.5/B6.6 and retest at both reference resolutions.
- Earlier route findings: B1_right_v2 (escort), B4_right (escort), B5_left (focus medicine), B6_left (focus volunteer), B8_right (shelter door), B9_left (focus warden), B9_right (group entrance). These did not reproduce in the fresh Batch 2 baseline or its first post-change fixture (18/18 passed in both). Their earlier cause remains unproven; continuous route coverage remains in Batch 7. The B-number prefixes here are mission IDs, not improvement-batch numbers. The current three-way interaction report contains the latest rerun.

- The local Unity validation queue is now loaded and has run live checks. Computer-use access remains unavailable; testing uses the authorized local Editor runner. The repaired full side-route rerun passes 18/18 routes. Thirty-two live input/settings/debrief assertions also pass. Player builds, performance, visuals and participant acceptance are still pending.
- Desktop on this PC is the first release-validation target. XR remains untested; it is not silently marked complete.
