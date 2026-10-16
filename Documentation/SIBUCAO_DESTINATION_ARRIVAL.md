> Historical implementation/test record. The current nine-node, 30-choice rebuild and latest per-option results are in [SIBUCAO_BRANCHING_REBUILD.md](SIBUCAO_BRANCHING_REBUILD.md). Earlier single-route PASS results do not verify every alternative.

# Destination arrival and progression fix

SIBUCAO_RAIN:
- Arrival is detected within 5m horizontally / 4m vertically of the actual interaction point. It shows Destination reached and an action-specific instruction.
- Minimap distance measures actual distance to the interaction target; it no longer reports zero from route projection alone. Resident encounters target the resident, and assisted residents/chairs point toward their safe destination while being held.
- Water observations begin their visible 15-second assessment on arrival. E inspects the situation, and a progress bar shows the assessment. Reading an inventory/guide does not advance the observation.
- E can talk to the two route speakers with proximity, facing and line-of-sight checks. Existing gaze dialogue remains available.
- Resolutions display Route updated; the minimap selects the next available task.
- The physical route endpoint reliably opens the completion screen. Bypassed unresolved encounters are recorded as missed rather than leaving a dead waypoint. A bag event that was never applicable is retained as not applicable.

Verified:
- All nine stations registered arrival and displayed their action hints through the manager's spatial arrival checks, with a bag present for the strap encounter.
- E action dispatch at the flooded-junction resident produced dialogue and resolved the encounter.
- Observation inspection worked, remained unresolved at 50%, completed at its threshold, and selected task 2 afterward.
- Reaching the authored route endpoint entered Complete and recorded remaining outcomes.
- Arrival/inspection/progress UI visually reviewed in Captures/destination-arrival-assessment.png.
