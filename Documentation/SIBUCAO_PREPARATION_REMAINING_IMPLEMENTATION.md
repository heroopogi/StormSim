# Remaining preparation batches — implementation record

14 October 2026. Scope: complete the preparation checklist for SIBUCAO_RAIN; the user will conduct participant acceptance after the batches are ready.

## Implementation map

| Checklist | Changes |
| --- | --- |
| PREP-09–11 | Target brackets, current-action prompts, a narrow forgiving focus cast with obstruction checks, destination-specific placement feedback, nearest free table placement, and existing out-of-bounds recovery. Decorative non-interactions do not receive an action target. |
| PREP-12–15 | Individually selectable bag contents; live cargo mass and the 8 kg scenario limit; item readiness and charge; specific evidence for incomplete checks. Only appropriate portable supplies can be packed. |
| PREP-16–17 | Hold or press-once work, visible percentage, interruption/cancellation preserving partial progress, saved work progress, confirmations before reopening sealed documents/windows or reconnecting electronics. Route review is idempotent. |
| PREP-18–20 | Separate 1 L travel bottle registered in both preparation and evacuation inventories; ten 5 L home reserve containers; an explicitly described household-day food abstraction; household help moves up to three nearby water containers per action if shelf space exists. |
| PREP-21–22 | Partial charge remains visible; 80% is a disclosed practice readiness target, 100% remains full; flashlight beam and text-confirmed radio/pouch/first-aid checks. No charging step overrides an evacuation instruction. |
| PREP-23–25 | Four-person care/contact/meeting card; primary and backup cards aligned with the actual school route and marked side passages; warning, needs and meeting-plan conversation records; no false confirmation of rescue or arrival. |
| PREP-26–28 | Scenario wind stages before the order; elevated work closes at rising wind, outdoor work closes at worsening conditions, descent remains available; optional activities award two points each, individual chores/contacts do not duplicate that credit, essential readiness and safe departure dominate. |
| PREP-29–32 | Shorter initial waits; reading the bulletin can start preparation; replayable text; consistent English and explicit simulation limits; scalable scrollable panels, remapped action text, non-color focus cues, press-once input, guide location hints, stable logical storage, and paused reading/charging/vitals. |
| PREP-33 | Optional departure/rest review lists packed and uncarried supplies plus the household plan; urgent ordered departure bypasses it. Missing checklist items never become a departure gate. |
| PREP-34 | Actual simulated water contact records wet belongings or sealed-pouch protection, with item tint and notifications; window rain-entry notices; inventory readiness and shelter follow-up expose the records in player-facing language. Raised belongings are not described as guaranteed safe. |
| PREP-35 | Atomic preparation saves with previous-file backup, active-time checkpoints, resume from the opening choice/guide, numeric/identity validation before mutation, migration of older saves without the added bottle, tool/charge/work persistence, and idempotent scoring. |
| PREP-06, PREP-36 | Tutorial and test instrumentation implemented. Continuous first-time-player comprehension acceptance is reserved for the user, with a separate final playtest sheet. |

## Guidance and deliberate simplifications

The 45.4 L practice quantity is approximately four people × three days × one US gallon. The [American Red Cross supply guidance](https://www.redcross.org/get-help/how-to-prepare-for-emergencies/survival-kit-supplies.html) distinguishes evacuation and longer home supplies. The game labels its three-day home exercise as a short training baseline, not a complete home emergency supply. The one-litre bottle is a demonstration share, not a household requirement. Nutrition, realistic carrying time, and charging time are not simulated.

[PAGASA flood guidance](https://www.pagasa.dost.gov.ph/learning-tools/floods) supports household responsibilities and early movement before access is cut off; [PAGASA storm-surge guidance](https://www.pagasa.dost.gov.ph/information/storm-surge) supports monitoring and following evacuation instructions. These references do not validate the game's routes, weather thresholds, or scoring. The preparation weather stages and 80% charge threshold are explicit gameplay choices.

The playable destination is Banago Elementary School I. The old preparation-only community-hall / different-school wording has been removed from runtime cards. Backup instructions use the existing marked side passages and rejoin the same school route; no second shelter is claimed. Household details and contact roles are fictional. English is the current implementation; reviewed localization can follow participant needs.

## Validation

Run **StormSim > Validation > All Preparation Batches** from SIBUCAO_RAIN in Edit Mode. Reports and captures are written to `Reports/PreparationRemaining_20261014/`. The fixture uses a separate save directory and restores its settings. Earlier coaching and Batch 1 assertions are included alongside remaining-batch checks and natural packing/departure frames.

The first combined live run passed 105 assertions: 45 remaining-batch checks, 17 coaching checks, 31 prior regression checks, nine live presentation/handoff checks, and three cancellation checks. Captures at 1080p and 720p with 130% text were inspected. That review led to fixes for immediate access to a newly carried bag, shorter retrieval/departure buttons, plain-language bag state, and putting the checklist before additional guide settings.

The second combined live run passed **112 assertions**: 50 remaining-batch checks, 17 coaching checks, 31 prior regression checks, 11 live checks, and three cancellation checks. This includes focus obstruction, checkpoint layout identity, overweight rejection, and portable-water packing and handoff. The refreshed bag and departure captures show usable selection buttons and readable action labels. The request was consumed and the runner requested exit from Play Mode.

Final runtime/editor source builds passed with **zero errors** (60 runtime and 178 editor warnings, primarily existing deprecated Unity APIs). The final guide ordering, plain-language bag evidence, and compact two-line HUD reminder were compiled after the captured version; they were not recaptured. No additional gameplay changes followed the 112-check run.

Evidence: `Reports/PreparationRemaining_20261014/{remaining,coaching,regression,live}-checks.txt`, `compile-runtime.txt`, `compile-editor.txt`, and PNG captures. The three cancellation assertions are in `Reports/PreparationBatch1_20261013/cancel-checks.txt`. The machine had approximately 8 GB RAM and severe memory pressure during editor reloads; timings are not a gameplay performance benchmark.

Do not treat implementation or scripted checks as participant acceptance. The final check sheet is [SIBUCAO_PREPARATION_FINAL_PLAYTEST.md](SIBUCAO_PREPARATION_FINAL_PLAYTEST.md). Actual first-time-player comprehension and physical headset interaction remain untested. Selected-item retrieval is implemented in the desktop bag panel; the existing VR last-item retrieval shortcut remains.

Backups before this pass: `Backups/PreparationRemaining_20261014/`. No scene asset was manually rewritten; runtime setup adds the travel bottle and registers it with both inventory and physical save tracking. Existing v2 preparation saves can be migrated when the bottle is the only missing item.
