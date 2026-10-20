# During-typhoon redesign — 17 October 2026

## Verified starting state

SIBUCAO_RAIN has a 732.13 m authored route. The live scene's decision manager has zero legacy stations. The active design is optional family assistance, flooded junction, drainage passage, injured resident, plus one runtime-seeded optional incident. The older nine-station description is not the current playable scene.

Source-confirmed problems: drainage choice inferred from lateral position; optional bypass incurs arbitrary stamina/score penalties; unmeasured traversal is classified as unsafe regardless of actual exposure; NPC escorts ignore distance and obstacles; gusts use a separate clock and direction from rain; brace expiration notification is unreachable; bracing accepts unsafe objects; temporary contact survives mode changes; XR lacks the desktop storm integration. Arrival still contains legacy task gates. Existing strengths include persistent flags, inventory continuity, an experience timeline, local runoff relief, physical carry/deposit, and responder alternatives.

Design hypotheses requiring human playtesting: repeated progress bars and bright choice props feel artificial; encounter spacing feels monotonous; route guidance competes with urgency. These are not measured usability findings.

## Implementation sequence

1. Correct shared storm/contact lifecycle and synchronize cues; cap involuntary displacement and honor XR comfort.
2. Make evacuation and reporting valid outcomes; remove hidden task selection and unjustified penalties.
3. Ground flood outcomes in observed exposure; make safe assistance and declining work explicit.
4. Improve work cancellation, nearby NPC handoff, persistence, and evidence-based reception.
5. Validate compilation, meaningful scenario checks, runtime placement and representative routes. Record limitations honestly.

## Route intent

Departure: orient to school and recognize exposure; optional family assistance remains available. Junction (~start+60 m): inspect from dry footing, mark uncertain access for reception, reroute; measurements never certify passage. Recovery (~start+115 m, where placement is clear): reassess equipment and destination. Drainage (~start+225 m): ask the volunteer, optionally handle loose dry material with protective equipment, or report and continue. The middle stretch gives room for travel and shared storm cues; the unvalidated surprise spawner is retired. Injured resident (~start+585 m): ask what help is needed, signal or offer prepared supplies with consent, and distinguish acknowledgment from responder arrival. Final approach: apply learned observation with fewer prompts. Reception: confirm arrival and report unresolved observations; distinguish acknowledged help from confirmed rescue.

## Educational sources and boundaries

- PAGASA, https://www.pagasa.dost.gov.ph/learning-tools/floods : early evacuation, avoiding unnecessary exposure and floodwater. Used for route guidance, never a numeric safe-wading threshold.
- PAGASA, https://pagasa.dost.gov.ph/information/storm-surge : heed warnings and evacuate to appropriate higher ground. A lull does not mean the storm has ended.
- American Red Cross, https://www.redcross.org/take-a-class/resources/learn-first-aid/bleeding-life-threatening-external : scene safety, consent, protection, calling help and direct pressure. The game does not teach diagnosis, injury relocation, or certify first-aid competence.
- Meralco, https://corporatepartners.meralco.com.ph/media/download/10243 : electrical typhoon/flood safety. Cable contact is never a rewarded solution. Local emergency contact details are not invented.

The storm cycle, footing meter, action durations and local drainage relief are gameplay approximations. A dry-footing check is a gameplay eligibility condition, not proof of real-world safety. Human review with local disaster educators and actual hardware testing remain release requirements.

## Acceptance evidence

## Implemented interaction contract

The school remains the main navigation destination. Local navigation takes over only for deliberately started pole work, drainage handling, or accompanying the evacuee. The full-screen plastic-sheet interruption and its indefinite 80% slowdown were removed; its owner now gives one nonblocking cue from the shared gust state and delegates optional encounter interactions.

- Junction: optional warning/measurements from dry footing, or mark the uncertain access for reception. No artificial stepping-stone crossing and no claim that a volunteer verified passage. Actual water contact is recorded independently of taking measurements. The center-line sign is moved to the roadside.
- Drain: first interaction briefs; second interaction reports and closes the optional task. Protective gear and dry footing gate loose-material handling. Carry to the sack; release/cancel interrupts the unfinished piece, while completed pieces persist. No automatic lateral-position choice and no bypass penalty. Relief grows over 45 seconds to at most 0.08 m locally, tapering to zero at 24 m. This is an authored gameplay effect.
- Evacuee: offer a prepared kit with permission, or request a responder using voice, radio or whistle. There is no medical diagnosis or simulated instant cure. Accompanying requires a separate consent interaction, nearby player, dry footing, ground support and clear movement. A responder acknowledgment and actual arrival are different flags. NPC locations persist in run data.
- Prototype incident spawner: removed from automatic attachment after live QA showed a fabricated household facade blocking the route and a magenta resident. Its source and existing save flags remain for future authoring, but these templates are NOT part of the default playable sequence. The optional family and authored evacuee provide the human decisions. Do not re-enable the prototype until placement, materials, and paths have been reviewed.
- Storm: saved-clock buildup, peak, lull and easing drive rain angle, wind audio and bounded movement resistance. Fixed support markers exclude movable objects, residents, electrical props and unreviewed scenery. Rain cover and wind shielding are distinct. Stationary players and XR/reduced-motion users receive no artificial wind drift. Bracing releases on movement, cancellation, elapsed contact, lost support or mode change.
- Reception: accepts arrival without optional task gates, records unresolved known locations once, and distinguishes assistance from confirmed arrival. Family check-in requires both people and cart at reception. Conservative NPC stepping stops at obstacles/water; it is not a full pathfinding solver.

### Playing the revised phase

Open `Assets/Scenes/SIBUCAO_RAIN.unity`, enter Play Mode, and start through the normal menu/preparation flow. Follow the school route; use the contextual interaction control (desktop E), hold only for the indicated sustained actions, and Q/release to interrupt. Speak to the drainage volunteer again to report rather than work. At the evacuee, the signal action remains available without equipment. Check in at reception and confirm the report. `J` opens the updated scenario guide. There is no requirement to complete every local task.

### Verification log

Executed so far: the 16-check editor regression set passes (serialization, legacy data, replay, equipment rules, local relief, gust envelope, and heading resistance). Five additional live method-level checks passed: penalty-free drain continuation, no fabricated exposure for unmeasured continuation, decision deduplication, reception without optional completion, and report deduplication. These are not five full playthroughs.

In Play Mode, actual target interactions successfully briefed the drainage volunteer and reported the blockage, and a voice-only signal reached acknowledgment. After correcting the adult responder's initial position, the responder traversed clear ground and reached within 0.81 m of the evacuee; the handoff flag then became true. The scene reported zero console errors during that pass. Earlier startup contained a `Failed to create global context` message, and editor assembly reloads repeatedly took tens of seconds, temporarily disconnecting the editor connector.

Visual QA found and prompted fixes for oversized overlapping labels, a warning sign obstructing the route center at 69.7 m, and a legacy full-screen plastic interruption reducing movement to 20%. The first traversal required interventions and is explicitly NOT a clean full-route pass. A synthetic route-following fixture uses real CharacterController collision and the production runoff model; it does not write player saves. Screenshots under `Assets/Screenshots/typhoon-*.png` are diagnostic captures, not promotional claims.

Hardware XR, human usability/learning effectiveness, and all five difficulties remain separate acceptance requirements. Numerical checks and direct method invocations do not establish those outcomes.

Further executed checks: two live physics fixtures confirmed clear-ground stepping and wall blocking; three brace checks confirmed eligible fixed support, cancellation, and zero stationary wind drift. A checkpoint was written and loaded through the game's actual save system under `tmp/typhoon-validation-saves`, restoring a completed debris flag and saved encounter positions. Normal player save slots were not used. The fresh route test passed the former sign obstruction and low drainage section at an early storm age, then exposed the prototype wall at 366.8 m. That run also is not counted as an uninterrupted completion.

Final default-route traversal after retiring the prototype: **reached actual school reception**, 729.2 m along the 732.13 m route (inside the authored arrival radius), **186.4 simulation seconds**, normal difficulty, initial storm clock zero. Automated controls walked with the real CharacterController and production runoff, with local obstacle steering and no teleports after departure. No optional tasks were completed, no junction water contact was recorded, health/stamina were 100/100, and console errors were zero. Both reception interactions completed the run and recorded unresolved junction/drain/evacuee follow-up. Screenshot: `Assets/Screenshots/typhoon-reception-validated.png`. This is one early-departure automated traversal, not a human usability study or a normal-menu/preparation-to-arrival playthrough.

An additional recovery fixture exposed a null legacy route-choice owner in `SibucaoRecovery`; it is now guarded. The retest returned successfully with 50 health, 60 stamina and needsRescue cleared. The no-equipment handling fixture correctly refused debris work. Grip progress paused at about 38% until transport. A straight approach to the sack was blocked by the authored concrete drain, not completed automatically. A subsequent real-controller walk around that obstruction deposited a piece and set `route_drain_piece_1` on dry footing. The fixture initially watched piece 0, so its per-piece success counter was false; inspection of the actual state confirmed piece 1 completion. This verifies one actual pickup/carry/deposit, not a complete three-piece clearing playthrough. Player/carried-item collision is ignored only during carrying and restored on cancellation or deposit.

At that validation stage scripts compiled with zero console errors and all 16 editor regression checks passed. Follow-up preparation, companion and high-water testing is recorded in [JourneyValidation.md](JourneyValidation.md), including the expanded 20-check suite, normal preparation-to-arrival walking pass, evacuee walking escort, family assisted arrival and clean late-departure assisted arrival. That report also records a recurring graphics-buffer error and the remaining acceptance boundaries. Every interruption/save combination, all difficulties, hardware XR, performance and human learning/usability still need separate validation. The implemented redesign is not a certified disaster simulation.
