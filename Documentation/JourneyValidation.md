# Preparation, companions and high-water validation

Validation record. Test evidence below distinguishes public-action fixtures, on-foot controller travel, and abstracted transport. Historical failures are retained to explain the fixes; the latest results supersede their pending status.

## Scope

- Normal StartRun -> preparation bulletin completion event -> bag retrieval -> early departure -> production phase handoff -> door -> school reception.
- Family: initial movement, keeping pace, waiting, physical arrival, saved world poses and assistance.
- Evacuee: consent, full school escort (not a 16-metre completion shortcut), saved position and reception.
- Late departure: production runoff catch-up, dry meeting-point eligibility, explicit request/wait/board, factual arrival report.

## Important distinction

Assisted transport is an explicitly labeled training abstraction. It does not simulate a boat route, establish real rescue response times, or declare flooded access safe. The 12-second wait is interface pacing. Remote companions are not transported. Optional tasks remain unresolved unless actually completed.

## Repeatable integration fixture

In Play Mode call `StormSim.Sibucao.Editor.SibucaoJourneyChecks.Begin("solo")`.
Read `Status` or Editor SessionState key `StormSim.JourneyResult`; call `Stop()` to cancel.
The fixture injects video completion and invokes public preparation actions. It walks through the real CharacterController after handoff, with obstacle checks; no route teleport is used except the explicit assisted-transport gameplay action if access closes.
Saves are isolated under `tmp/journey-validation`. This is not a mouse/UI usability test.

## Results

- Initial normal-flow fixture: preparation bag retrieval, public early-departure action, fade, storm handoff, door interaction and physical outdoor walking succeeded. At storm clock 228.3, route progress 82.7 m, the dry-route fixture stopped. Ground-sampled depths across its forward steering options were approximately 0.028–0.034 m. This is a blocked test, not an arrival pass.
- Based on that result, early departure was moved from FloodStart minus 60 seconds to minus 180 seconds, bounded by the end of the home introduction. This is compressed scenario balance, not a forecast or evacuation-time recommendation.
- The phase handoff now requests the same budgeted flood-clock replay used by save loading. It does not substitute authored water depths for the production runoff model.
- The 18-check regression suite passes, including family pose and pending-assistance serialization. Final journey reruns are pending.
- Revised early-departure integration run completed preparation -> Playing -> reception -> Complete at home clock 218.3, bag retained, health 100, arrival report confirmed. It used explicit assisted transport after wet access blocked the dry-route fixture. This is an assisted completion, not an uninterrupted walking pass.
- Family movement fixture: supplied/consented starting state, real NPC steps and player controller, production runoff. Family passed 100 m. A real checkpoint save/load at family route progress 111.21 restored cart position with approximately 0.000004 m difference. Full companion arrival still pending.
- That family run stopped at route progress 229.2 m against the drainage signpost. The evacuee fixture walked beyond its former short completion segment and restored correctly at 642.4 m (approximately 0.000004 m error), then stopped at 715.5 m against the school annex. Neither is counted as a completed walking journey.
- These failures led to saved player-path trails plus bounded local steering instead of fixed route offsets. Ground, water and collision checks remain active. Companions now pause while the flood model catches up.
- A null-home error from PreparationExposure in the isolated fixture was traced and guarded.
- Early departure now stores a rainfall-clock offset equal to the skipped wake-up presentation time. The flood still runs continuously from that departure; late departure and old saves retain their existing storm age. This avoids counting skipped presentation as rainfall before an early outdoor journey.
- The expanded 20-check regression suite passed with zero compile errors before the final live reruns.
- Final early-flow integration: **PASS**, preparation through school report, on foot, home clock 266.0 (rainfall age approximately 194.0), assisted transport false, health 100, bag retained. A checkpoint reload at route progress 280.5 m preserved player position exactly, the bag and the 72-second presentation offset. Screenshot: `Assets/Screenshots/journey-full-preparation-arrival.png`. Video completion/public preparation actions are automated; this is not an input-device usability test.
- Late fixture used an ordered departure and replayed production runoff to storm age 600 with offset zero. It reached Complete at clock 613.5 with explicit assistance, bag retained and health 100. However, arrival initially fired skipped-route discovery/completion triggers; reporting must be retested after an immediate reception transition is added. Do not treat the first late report as clean evidence.
- During the long session Unity reported one graphics ring-buffer exhaustion error. No restart or rendering configuration change has been made to hide it; graphics/performance validation remains distinct from gameplay acceptance.
- Further family runs exposed a small road-edge collision and a signboard overhang. The lower NPC sweep was adjusted to permit only the existing bounded ground-step allowance. The fixture's head-height probe and the centre-lane sign placement also need the final rerun. A new family checkpoint at approximately 133 m restored its exact pose and all nine trail points.
- Negative assistance checks passed: boarding before requesting is rejected; a distant family cannot be transported or marked arrived.
- Evacuee full escort fixture: **PASS**, reached school and completed reception at clock 101.4, assisted transport false, health 100. Both the arrival flag and saved NPC position were inside the actual school arrival zone. Reception timeline correctly stated that the evacuee walked to school. The fixture initializes consent/supplies state; it is not a preparation/first-aid-interaction test.
- The head-height/signboard fix passed the former 229 m obstruction. A resumed family run reached 288.5 m before rising water stopped the companions. Assistance eligibility was checking only ahead of the player; it now checks 12 m behind as well as 24 m ahead, retaining the supported dry-footing requirement. The stopped run is not counted as an arrival pass.
- Fresh family fixture after that fix: **PASS**, Complete at clock 184.4, health 100, assisted transport true. Cart, parent and child positions each passed the actual reception-zone check. Timeline distinguished walking/roadside work from the abstracted assisted transfer and retained the unresolved drain report. This fixture initializes accepted/supplied family state and does not include preparation or a go-bag. Screenshot: `Assets/Screenshots/family-assisted-reception.png`.
- Unity's graphics ring-buffer exhaustion error recurred during the fresh family run; it remains an unresolved rendering/performance issue, not a script compile error.
- Clean late-departure rerun: **PASS**, preparation -> Complete at clock 613.7, bag retained, health 100, assisted transport true. No junction, drain, rescue or family discovery/completion flags were fabricated by the transfer. The timeline recorded the request, abstracted arrival and reception, with no unresolved *encountered* incidents. Screenshot: `Assets/Screenshots/late-high-water-clean-reception.png`.

## Acceptance boundaries

- Final-source evacuee rerun: **PASS**, Complete at clock 101.7, assisted false, health 100; saved escort position confirmed inside reception. All 20 regression checks passed again. Final console inspection found the known graphics ring-buffer error, with no script compile errors. Test input was reset and Play Mode stopped after validation.
- The normal early-departure preparation-to-school walking journey passed. The family journey passed with assistance when rising water cut off walking; this is not a full-route family walking pass.
- Companion fixtures initialize supplies/consent; they do not replace testing those interactions with a real input device. Preparation fixtures inject bulletin completion and use public actions, rather than clicking the complete preparation UI.
- Actual checkpoint restoration was exercised for the player, family and evacuee. Pending assistance has serialization coverage, not a full live pending-request reload test. Old saves without companion world poses were not fully journey-tested.
- Hardware XR, every difficulty/home combination, crowded transport landing clearance and rendering performance are not certified by these runs. The recurring graphics-buffer error remains open.
