# SIBUCAO_RAIN preparation — Batch 1

Date: 13 October 2026  
Scope: PREP-01 through PREP-08 in the [preparation checklist](SIBUCAO_PREPARATION_IMPROVEMENT_CHECKLIST.md).

## Implemented behavior

- Players can recheck the TV and leave early through the exit without finishing optional chores or waiting for an order.
- Continued preparation triggers an evacuation advisory after 720 active preparation seconds by default. The value is configurable on `PreparednessSession`. Settings and reading panels pause preparation time; elapsed time is saved and restored.
- An active order supersedes property work and rest. Departure does not force a return to the television after the evacuation instruction has already been announced.
- The bed first explains the choice, then requires a second interaction to confirm. Rest advances to a playable morning advisory, with supplies where the player left them. The player then collects the bag and leaves.
- Early departure starts evacuation with up to 60 seconds of extra compressed scenario time before flood rise. Ordered and morning departures start at flood onset. This is a gameplay timing choice, not a real-world forecast.
- The departure readiness check accepts early, ordered, and morning departures. Broader reward balancing remains Batch 4 work.
- The guide has five expandable groups: Supplies, Go-bag, Home safety, Household plan, and Departure. Controls are in a separate expandable section. Redundant activity summaries are omitted where readiness rows already cover the action.
- Essential preparation is presented before optional property work and neighbor visits. No checklist-completion gate was added to departure.
- With Contextual hints enabled, objectives respond to missing equipment, bag state, held objects, route review, and advisories. The first document pouch provides a pickup/check/pack example. Guidance use and introductory action progress are saved. Hints-off play retains essential warnings and a general objective.
- Desktop E picks up, checks, and packs supplies, while world fixtures and battery insertion retain context priority. Q puts down; I/Tab opens the bag; J opens the guide. F and P remain optional carry/pack shortcuts shared with evacuation. The old desktop B/G aliases were removed. Tracked-hand grip/trigger behavior remains separate.
- A bag carried at departure stays equipped after the controller handoff. Supplies already packed retain their inventory entries.
- Departure saves now record the fade/transition state, allowing a saved departure to resume as a transition rather than reverting to preparation.

## Verification

- Runtime source compilation: passed with zero errors; existing deprecation warnings remain.
- Editor source compilation: passed with zero errors; existing deprecation warnings remain.
- Core scene regression: 31 checks passed. [Report](../Reports/PreparationBatch1_20261013/regression-checks.txt).
- Live presentation and handoff: 9 checks passed, including actual packing animation, natural early-departure fade, equipped bag, retained pouch, and flood head start. [Report](../Reports/PreparationBatch1_20261013/live-checks.txt).
- Visual review: inspected the collapsed guide at 1080p, expanded/scrolled guide at 720p with 130% text, preparation HUD, and early-departure handoff. Guide content scrolls at the smaller size; close controls remain accessible. Existing developer controls were enabled in this Editor session and appear in the captures.
- Final Q-cancel wiring: 3 preview-scene component checks passed against the refreshed Unity assembly. Q cancels work without a held tool, cancelled work cannot commit completion, and the task can be restarted successfully. [Report](../Reports/PreparationBatch1_20261013/cancel-checks.txt). This is component/action validation, not a physical keyboard playtest.
- First-time-player acceptance: pending. Scripted checks are not participant evidence.

**Total: 43 automated checks passed; runtime and editor source builds have zero errors.** PREP-01, 02, 03, 04, 05, 07, and 08 are technically verified. PREP-06 remains open for a first-time player to repeat a similar action without coaching. Batch 1 is not yet accepted as a whole.

The core pass used action-dispatch fixtures and controlled positioning, followed by a live packing/fade/handoff sequence. It was not a continuous keyboard traversal of the home or evacuation route. Unity's connector returned a missing named-pipe error; subsequent asset imports/domain reloads were slow. The source build and the loaded-assembly checks are therefore recorded separately rather than treating compilation as gameplay verification.

The final cancellation fixture ran after Unity imported the final controller source. Both one-shot test requests were consumed; the runner returned to Edit Mode. No queued test request remains.

Screenshots: [guide at 1080p](../Reports/PreparationBatch1_20261013/guide-1080.png), [guide at 720p](../Reports/PreparationBatch1_20261013/guide-720-expanded.png), [scrolled guide](../Reports/PreparationBatch1_20261013/guide-720-bottom.png), [preparation HUD](../Reports/PreparationBatch1_20261013/preparation-hud-1080.png), [early-departure handoff](../Reports/PreparationBatch1_20261013/early-departure-1080.png).

Existing preparation and evacuation saves were backed up before the live pass and their hashes matched afterward. The latest combined fixture also redirects evacuation saves to its report directory.

Compile reports: [runtime](../Reports/PreparationBatch1_20261013/compile-runtime.txt), [editor](../Reports/PreparationBatch1_20261013/compile-editor.txt).

The regression fixture is `PreparationBatch1Checks.Run()` in Play Mode with preparation active. It preserves the prior preparation save and restores its baseline after testing. It covers departures, timed advisory dispatch through the session update, reading/settings pause, rest/morning flow, guide coverage, hints-off behavior, E-key action dispatch, packing, and progress/save compatibility.

For the combined live/capture run, open SIBUCAO_RAIN in Edit Mode and choose **StormSim > Validation > Preparation Batch 1**. The runner enters Play Mode, executes regression checks, captures the guide at 1080p and 720p, exercises actual packing and early-departure handoff, and returns to Edit Mode. Results are written under `Reports/PreparationBatch1_20261013`. The one-shot `request.txt` file in that directory can also queue the same run for the next asset import; it is consumed by the runner.

## Preserved boundaries

Inventory item selection, detailed readiness feedback, placement aids, charging balance, optional activity scoring, audiovisual polish, and the broader recovery work remain in their later batches. The 720-second advisory timing and 60-second head start need participant pacing validation. Shared preparation scripts may also affect other coastal scenes; this batch's live acceptance target is SIBUCAO_RAIN.

Original edited scripts were copied to `Backups/PreparationBatch1_20261013` before modification. New source and verification files are additional to those backups.
