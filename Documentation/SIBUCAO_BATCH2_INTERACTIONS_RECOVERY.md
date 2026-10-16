# Batch 2 — Interactions and progression

Scope: B2.1–B2.6 from the improvement checklist. Keep the low-poly presentation and a 16:9 Game view. Reference sizes: 1920×1080 and 1280×720. Implementation began 29 September; validation continued 30 September 2026.

## Implemented behavior

- Side mechanisms name their drag direction (right), show a percentage, allow reversing with left drag, and require releasing E at the end stop. Reach uses the object's collider surface rather than its pivot.
- Carry prompts name the destination and remaining distance, then show readiness to place. Placement rejects intervening environment walls. A small target indicator uses the existing HUD style.
- Preview, work in progress, and work finished/outcome pending have separate messages. A side route records its outcome only after its work and merge have both been reached.
- Q preserves side-task progress. Pause → Recover task can return the player to supported footing at the earliest unfinished junction, reset the current prop, regroup an escort at its previous waypoint, or recall loose junction tools. Recovery does not grant completion credit or replace lost/consumed supplies.
- Escorts check short movements for obstructions, attempt small side steps, and explain waiting or blocked states. Broader pacing, animations, crowd behavior, and authored route/flood changes remain in later batches.
- Early school arrival opens an explanation of the earliest unfinished junction, with recovery actions. It does not silently stop or award uncompleted outcomes.
- The bag's Item status view names stored, equipped, dropped, lost, consumed, and damaged items. It remains available without an equipped bag. Taking an item out latches it in hand when possible.
- Continue and Pause offer separate manual and checkpoint slots, with dates and backup versions. Checkpoints are requested by completed actions, route outcomes, inventory changes and recovery, throttled to five seconds; otherwise they occur about every minute during evacuation. Leaving through Pause → Main menu also saves a checkpoint.
- Writes use a flushed temporary file and atomic replacement. Each slot keeps its previous valid version as `.bak`. A corrupt primary does not replace a good backup. Loading a corrupt primary tries its backup and explicitly reports recovery. Both invalid files leave the current run selected.
- Saves retain partial mechanisms, carried-prop positions, paused versus active side work, exact escort positions, and current raised-deck height. Grips are deliberately released on load; the player re-grips with E/F.
- The project's default window size is now 1920×1080, matching the required 16:9 presentation. The previous 1440×900 setting was 16:10. The Game view and all Batch 2 captures use 16:9.

## Validation record

All six Batch 2 tasks are implemented and technically verified. Human playtest acceptance remains pending.

- Fresh-session baseline, before Batch 2 edits: 18/18 side-route fixtures passed. The seven failures recorded in Batch 1 did not reproduce in that session. This does not establish their cause or prove they are permanently resolved.
- Baseline evidence: `Captures/Batch2_20260929/baseline-interactions.txt`.
- New validation fixture: `StormSim/Validation/Batch 2 recovery and saves` in Play Mode. Save fixtures use uniquely named subfolders under `Captures/Batch2_20260929/`; they do not use player save files.
- Final compilation and fixture execution succeeded without warnings or errors in the final validation calls. All **39/39** recovery, interaction, inventory and save assertions passed: [final report](../Captures/Batch2_20260929/recovery-save-checks.txt).
- All **18/18** side-route fixtures passed again: [final routes](../Captures/Batch2_20260929/route-interactions.txt). These exercise focused physical actions, idle/release behavior, distinct work completion versus route outcome, and save round trips.
- Structural validation passed for **9 centers, 18 unique physical side missions, 90 action steps and 72 home-specific paths**, including ordered center/left/right progression, school arrival and partial-save round trips: [structure report](../Captures/Batch2_20260929/route-structure.txt).
- Earlier iterations are retained for traceability. The first recovery/save pass passed 35/36 assertions and found an escort blocked-state bug. The corrected escort check passed in the second pass (38/39). Its remaining tool-position assertion used a position already affected by physics; repeated resets returned to the same authored pickup point. The fixture baseline was corrected, and resetting a kinematic tool no longer assigns unsupported velocities. Both corrections passed the final 39/39 run.
- Final captures were visually inspected: [school recovery, 1280×720](../Captures/Batch2_20260929/01-school-recovery-1280.png), [save selection, 1280×720](../Captures/Batch2_20260929/02-save-selection-1280.png), [item states, 1280×720](../Captures/Batch2_20260929/03-item-status-1280.png), [mechanism direction/progress, 1280×720](../Captures/Batch2_20260929/04-mechanism-1280.png), and [school recovery, 1920×1080](../Captures/Batch2_20260929/05-school-recovery-1920.png). The new panels and interaction prompt are readable. These are staged runtime references, not a continuous gameplay recording.
- Both the Game view and default player size were verified at **1920×1080**. All Batch 2 PNGs are 16:9.
- Editor cleanup completed: Play Mode stopped, SIBUCAO_RAIN is clean (no unsaved scene changes), development controls are hidden, and the normal domain-reload option is restored. See [final editor state](../Captures/Batch2_20260929/final-editor-state.txt). Player save slots were not used by these fixtures.

Source backups and one-shot implementation scripts are in `Backups/Batch2_20260929/`. Do not rerun those scripts against already edited files.

## Limits

Scripted fixtures use synthetic input and teleports. They do not replace a continuous, unaided player run, a standalone build test, or XR validation. Flood layout design remains Batch 3; this batch only preserves the current lift when resuming a saved task.

The original 1280×720 mechanism capture shows crowded distance/footing text in the navigation panel. At Batch 2 closure this was recorded for B6.5/B6.6; the interaction prompt itself remained readable, and no Batch 3 or Batch 6 work had started. **Resolved during Batch 3 on 30 September 2026:** separate direction, distance, footing and current rows were verified at 1280×720 and 1920×1080. See the [Batch 3 validation record](SIBUCAO_BATCH3_FLOOD_MOVEMENT.md).
