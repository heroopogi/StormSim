# SIBUCAO_RAIN preparation improvement checklist

Created: 13 October 2026  
Scope: preparation in SIBUCAO_RAIN, including its handoff to evacuation and visible consequences in the storm/aftermath.  
Goal: believable preparation that is easy to play and understand.

## Status and evidence

**Progress: all six desktop implementation batches complete; 34 / 36 checklist items technically verified. PREP-06 and PREP-36 await the user's participant playtest.**

**14 October all-batches completion:** The final combined run passed **112 automated checks** (50 remaining-batch, 17 coaching, 31 regression, 11 live presentation/handoff, and 3 cancellation). Bag/departure captures were inspected at 720p with 130% text; guide/handoff captures include 1080p. Final guide ordering, plain-language bag evidence, and a shorter HUD reminder were compiled after the captured version; these small presentation changes were not recaptured. See the [remaining implementation record](SIBUCAO_PREPARATION_REMAINING_IMPLEMENTATION.md) and [final playtest sheet](SIBUCAO_PREPARATION_FINAL_PLAYTEST.md).

**Batch 1 implementation is in place (PREP-01–08).** Core verification passed 31 regression checks and 9 live presentation/handoff checks. The final Q-cancel checks also passed. The tutorial still needs first-time-player acceptance (PREP-06). See the [Batch 1 implementation record](SIBUCAO_PREPARATION_BATCH1_IMPLEMENTATION.md).

**14 October follow-up:** Added an opening Guided/Explore choice, a visible tutorial progress checklist, small Explore hints, and coaching switches in the guide and settings. All 60 automated coaching/preparation checks passed; screenshots were inspected at 720p with larger text and at 1080p. A wrapping HUD reminder was shortened and compiled afterward. See the [coaching implementation and validation record](SIBUCAO_PREPARATION_COACHING.md). This does not close the first-time-player acceptance item.

This checklist records the preparation review of 13 October 2026. The review inspected current scripts and scene data; it was not a fresh continuous playtest. Existing features are foundations to improve or validate, not claims of missing functionality. The checked items now record source, automated, and visual technical verification with the limitations below. They do not establish participant comprehension or physical headset acceptance.

The older [whole-game checklist](SIBUCAO_RAIN_IMPROVEMENT_CHECKLIST.md) records earlier work. Preserve its historical results; reconcile any overlapping controls, guidance, accessibility, and continuity claims against the current build before closing items here.

| Batch | Priority | Focus | Verified |
| --- | --- | --- | --- |
| 1 | P0 — first | Flow, priorities, and teaching | 7 / 8 |
| 2 | P1 — next | Interaction and inventory | 9 / 9 |
| 3 | P1 — next | Supplies and household planning | 8 / 8 |
| 4 | P0 — before acceptance | Safety conditions and rewards | 3 / 3 |
| 5 | P2 — polish | Opening, language, accessibility, and layout | 4 / 4 |
| 6 | P1 — release checks | Handoff, recovery, and player comprehension | 3 / 4 |

Recommended order: Batch 1, Batch 4, Batch 2, Batch 3, Batch 5, then final Batch 6 acceptance. Test handoff and recovery throughout implementation.

Check an item only after implementation or confirmation of existing behavior, verification, and an evidence entry. Record intentional scope changes explicitly rather than marking omitted work complete. Update progress counts with each accepted item. Use 1920×1080 as the reference and 1280×720 for smaller-window checks, following the existing 16:9 project requirement.

## Batch 1 — Clear flow, priorities, and teaching

- [x] **PREP-01 — Resolve early-departure contradictions.** Align the television, exit, objectives, and scoring so an advertised early departure is actually available and has a meaningful outcome. Verify departure before an order, departure after an order, and continued preparation.
- [x] **PREP-02 — Make evacuation advisories reachable.** Connect the advisory to normal scenario progression. Demonstrate it without editor calls, debug skips, or manually changing state; confirm updated instructions and departure behavior.
- [x] **PREP-03 — Explain how preparation ends.** Replace the generic direction to use the bed with clear preparation, rest, and departure choices appropriate to the current conditions. Verify players understand which action advances the phase and what carries forward.
- [x] **PREP-04 — Organize the checklist.** Group the existing readiness checks, optional activities, and household entries into Supplies, Go-bag, Home safety, Household plan, and Departure. Show a short summary with expandable details and no contradictory duplicate entries.
- [x] **PREP-05 — Distinguish essential and optional work.** Label priorities consistently in objectives, the guide, and task prompts. Verify players understand that visiting every neighbor and completing every property task are unnecessary for progression.
- [ ] **PREP-06 — Teach interactions progressively.** Guide the first storage opening, pickup, item check, and packing action. Provide replayable help; verify the next similar action can be completed without coaching.
- [x] **PREP-07 — Unify controls across phases.** Use a consistent primary interaction, release/cancel action, and inventory access during preparation and evacuation. Audit legacy shortcuts, remapping, prompts, and guide text against actual behavior.
- [x] **PREP-08 — Make guidance respond to unfinished work.** Give specific next steps in guided play, including recovery from missing prerequisites. If assessment mode is used, keep assistance appropriate to the assessment and record its use.

**Acceptance:** A new player can explain the warning, identify the next useful action, distinguish optional work, and choose how to end preparation. No advertised departure choice is blocked by contradictory logic.

## Batch 2 — Easier interaction and inventory

- [x] **PREP-09 — Identify interactable objects.** Provide restrained focus feedback, readable names, and the currently available action. Verify decorative objects do not suggest unavailable interactions.
- [x] **PREP-10 — Show valid placement locations.** Add clear placement feedback for shelves, charging, household supplies, and the bag. Explain unsuitable objects, insufficient space, and unreachable destinations.
- [x] **PREP-11 — Make handling forgiving.** Verify generous focus targets, stable placement, and recoverable drops with small supplies, cabinet doors, and crowded tables. Retain recovery for items falling outside the playable area.
- [x] **PREP-12 — Retrieve a selected packed item.** Replace last-item-only retrieval with individual selection in the desktop bag panel. The existing VR last-item shortcut remains; headset interaction has not been validated in this pass. Verify removing any item preserves the remaining contents and does not duplicate objects.
- [x] **PREP-13 — Show capacity before rejection.** Display current bag weight, the scenario limit, and selected-item weight. Verify the display agrees with packing checks; identify the 8 kg limit as a scenario constraint.
- [x] **PREP-14 — Show item readiness in the bag.** Display relevant states such as tested, batteries missing, sealed, partially charged, and ready. Verify packed-but-unprepared items remain distinguishable from ready items.
- [x] **PREP-15 — Explain incomplete tasks specifically.** Replace generic verification messages with the actual missing condition and location where appropriate. Verify first aid, batteries, documents, charging, cable, and storage examples.
- [x] **PREP-16 — Improve work progress and input comfort.** Show progress clearly, provide an accessible alternative to sustained holding, and preserve partial work where appropriate. Verify interruption, cancellation, resumption, and completion behavior.
- [x] **PREP-17 — Prevent confusing reversals.** Label actions that reopen, reconnect, or undo completed preparation. Verify repeated interaction cannot silently invalidate a completed task and that the checklist updates when reversal is intentional.

**Acceptance:** Players can prepare, pack, inspect, retrieve, and place an item using understandable prompts. Input precision and inventory friction do not dominate the learning task.

## Batch 3 — Believable supplies and household planning

- [x] **PREP-18 — Separate home reserves and portable supplies.** Distinguish bulky household water from drinking water intended for evacuation. Verify appropriate storage/packing rules and clear player-facing labels for both.
- [x] **PREP-19 — Explain household quantities.** Relate water and food targets to household size, duration, and understandable containers or servings. Review the current 45.4-litre and three-food-pack checks; label scenario simplifications and record the guidance used.
- [x] **PREP-20 — Reduce repetitive hauling.** Streamline storing the ten 5-litre containers through a suitable grouped action or carrying aid. Preserve the learning objective and verify quantity, placement, and save-state accounting.
- [x] **PREP-21 — Make charging understandable.** Clearly show compressed charging and progress. Distinguish partial readiness from full charge without presenting a nearly charged device as wholly unprepared; verify scoring and readiness messages agree.
- [x] **PREP-22 — Demonstrate equipment checks.** Make flashlight testing, radio reception, pouch sealing, and similar checks visible or audible, with accessible text feedback. Explain the practical purpose briefly.
- [x] **PREP-23 — Give the household a practical plan.** Make the four-person household meaningful through a simple care/contact card, assistance needs, carrying responsibilities, and a reunion arrangement. Keep the information consistent across preparation and evacuation.
- [x] **PREP-24 — Make route planning concrete.** Show primary and backup destinations, recognizable landmarks, a meeting point, and reasons to use the backup. Verify these agree with the playable map and clearly distinguish fictional training locations.
- [x] **PREP-25 — Give neighbor conversations a purpose.** Support sharing the warning, learning a need, and agreeing on a plan with clear acknowledgements. Verify contact remains optional and repeated interaction does not create duplicate credit or imply guaranteed rescue.

**Acceptance:** Players can explain what stays at home, what they carry, why the selected supplies matter, and how the household plans to reach safety.

## Batch 4 — Safe priorities and appropriate rewards

- [x] **PREP-26 — Tie outdoor tasks to actual conditions.** Communicate worsening conditions and block unsuitable property work when conditions become unsafe, rather than relying solely on the evacuation-order flag. Verify safe cancellation and a clear return/departure instruction.
- [x] **PREP-27 — Review elevated maintenance tasks.** Keep ladder/platform work optional, clearly framed, and restricted to appropriate conditions. Verify it is not required for safe departure or presented as a priority over people and evacuation.
- [x] **PREP-28 — Reward preparedness and timely departure.** Review the existing 25-point readiness checks, 40-point optional activities, and additional task/contact credit. Compare essential-only, extended-chores, and timely-departure runs; ensure the game does not encourage unsafe delay or completion of every chore.

**Acceptance:** The instructions, restrictions, and rewards communicate the same priorities. An evacuation instruction takes precedence over unfinished property tasks.

## Batch 5 — Readable and approachable presentation

- [x] **PREP-29 — Improve the opening pace.** Review the 18-second peaceful period and 24-second bulletin. Provide a concise, replayable warning and useful introductory interaction; verify players understand when and how preparation begins.
- [x] **PREP-30 — Use plain, consistent language.** Replace internal identifiers and technical verification wording with household terms. Decide and record whether reviewed Filipino/Hiligaynon support is needed for the intended participants; keep chosen language coverage consistent.
- [x] **PREP-31 — Complete accessibility checks.** Verify readable text scaling, subtitles, remapped controls, cues beyond color, and comfortable interaction alternatives. In guided learning, allow enough time to read; define and communicate time behavior for panels and assessment play.
- [x] **PREP-32 — Make the home spatially legible.** Place supplies logically, preserve clear paths, and use lighting and object shapes to distinguish relevant objects. Inspect from the player viewpoint at reference and smaller 16:9 resolutions.

**Acceptance:** Instructions, controls, relevant objects, and pathways remain understandable at both tested resolutions. Players can access and reread help comfortably.

## Batch 6 — Continuity, recovery, and comprehension

- [x] **PREP-33 — Review readiness before transition.** Show packed supplies, items left at home, the household plan, and the selected departure/rest choice. Keep optional unfinished work from becoming a completion gate; avoid delaying urgent departure with a mandatory lengthy review.
- [x] **PREP-34 — Verify visible consequences.** Trace packed supplies and representative home actions—sealed documents, latched windows, raised belongings—through storm and aftermath. Show understandable consequences and distinguish implemented outcomes from recorded flags alone.
- [x] **PREP-35 — Verify saves and checkpoints.** Test carrying the bag, partially charged devices, held tools, interrupted tasks, and phase transitions. Add suitable checkpoints and verify resume causes no lost or duplicated items, repeated scoring, or incompatible phase state.
- [ ] **PREP-36 — Conduct first-time-player testing.** Record an actual continuous playthrough with build, hardware, role, difficulty, language, and assistance used. Observe finding the bag, preparing and packing an item, recovering a mistake, identifying optional work, explaining item purposes, and deciding when to leave. Address major confusion and retest.

**Acceptance:** A player can complete the preparation flow and continue into evacuation with the expected state. Participant observations demonstrate comprehension; scripted checks and screenshots alone do not establish this.

## Verification record

Use one row per verified item or reopened issue. Record evidence paths and remaining limitations. The existing [first-time playtest record](SIBUCAO_FIRST_TIME_PLAYTEST_RECORD.md) is a useful starting point, but its historical status is not evidence that testing has occurred.

| Date | Item IDs | Change or confirmed behavior | Build / scenario | Evidence and result | Remaining issue |
| --- | --- | --- | --- | --- | --- |
| 2026-10-13 | All | Checklist created from source and scene review | Current workspace; no new playtest | Planning only; 0 / 36 verified | Implementation and acceptance pending |
| 2026-10-13 | PREP-01–08 | Departure/rest flow, timed advisory, grouped guide, contextual E actions, and adaptive guidance implemented | Current workspace; SIBUCAO_RAIN target | Runtime/editor source builds: zero errors; live fixture queued | Live scene verification and first-time-player acceptance pending |
| 2026-10-13 | PREP-01–05, 08 | Core flow and guide technically verified | SIBUCAO_RAIN Editor; 1080p and 720p | 31 regression + 9 live checks passed; screenshots inspected; [evidence](SIBUCAO_PREPARATION_BATCH1_IMPLEMENTATION.md) | PREP-06 first-time tutorial acceptance and PREP-07 final Q-cancel recheck remain open; batch acceptance pending |
| 2026-10-13 | PREP-07 | Final Q-cancel behavior verified after source import | Isolated Unity preview scene | 3 component checks passed: cancel, prevent completion, restart; 43 total automated checks across the batch | Only PREP-06 first-time tutorial acceptance remains; batch acceptance pending |
| 2026-10-14 | PREP-09–35 | All remaining desktop implementation batches, including recovery and evacuation continuity | SIBUCAO_RAIN; Unity 6000.5.7f1; 1080p and 720p / 130% text | 112 checks passed; runtime/editor builds zero errors; [implementation and evidence](SIBUCAO_PREPARATION_REMAINING_IMPLEMENTATION.md) | Final small text/layout changes compiled after captures; physical VR and continuous participant acceptance remain untested |

## Review references

- [Preparation session and readiness logic](../Assets/StormSim/Preparedness/Runtime/PreparednessSession.cs)
- [Preparation controls, objectives, and panels](../Assets/StormSim/Preparedness/Runtime/PreparednessPlayer.cs)
- [Optional activity completion](../Assets/StormSim/Preparedness/Runtime/PreparednessActivity.cs)
- [Household task interaction](../Assets/StormSim/Preparedness/Runtime/PreparednessHouseTask.cs)
- [Neighbor conversations](../Assets/StormSim/Preparedness/Runtime/PreparednessNeighbor.cs)
- [Ladder interaction](../Assets/StormSim/Preparedness/Runtime/PreparednessLadder.cs)
- [Preparation transfer and scoring](../Assets/StormSim/Preparedness/Runtime/PreparationContinuity.cs)
- [Preparation-to-storm bridge](../Assets/StormSim/Preparedness/Runtime/SibucaoPreparationBridge.cs)
- [SIBUCAO_RAIN scene](../Assets/Scenes/SIBUCAO_RAIN.unity)
- [PAGASA storm-surge guidance](https://pagasa.dost.gov.ph/information/storm-surge)
- [Red Cross flood preparedness guidance](https://www.redcross.org/get-help/how-to-prepare-for-emergencies/types-of-emergencies/flood.html)

Use the external guidance to review safety teaching; validate local routes and household assumptions separately. Gameplay simplifications should be explicit and must not be presented as a validated physical or emergency-response model.
