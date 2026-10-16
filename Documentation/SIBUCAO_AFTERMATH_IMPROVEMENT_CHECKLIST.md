# Sibucao Aftermath Environment Progress

Updated: October 13, 2026

Target scene: `Assets/Scenes/SIBUCAO_AFTERMATH.unity`

Visual direction: the morning after the typhoon, with receding floodwater, visible damage, and residents beginning cleanup. Keep Sibucao recognizable and its playable routes readable.

Status: Phases 1 through 3 are complete for the first work area and the eight selected homes near the school approach. Phases 4 through 7 have not started. Phase 1 recorded a direct-entry mission prerequisite and performance concerns; those issues remain open. Phases 2 and 3 have editor visual and structural verification, with runtime acceptance and wider rollout still pending. Check a task only when its result has been inspected; record verification evidence in the progress log.

## Existing foundation

These completed items describe the October 9 static visual pass. They do not establish Play Mode readiness.

- [x] Configure aftermath weather with stopped precipitation and thunder, reduced wind, and calm sea settings.
- [x] Disable storm runoff and retain the school spawn.
- [x] Add school relief supplies, canopy, and initial roadside debris and puddles.
- [x] Add varied damage to eight selected homes, including six roof openings, exposed rafters, broken fences, and displaced roofing.
- [x] Apply aftermath facade materials and retain original wall geometry and collision.
- [x] Verify isolated roof meshes, matching roof colliders, and no new dressing colliders in the entrance corridor during static checks.

Evidence: [installation report](../Captures/Aftermath/install.txt), [scene verification](../Captures/Aftermath/verification.txt), and [house damage verification](../Captures/Aftermath/house-damage-verification.txt).

## Phase 1 Baseline and work area

- [x] Back up the current aftermath scene before editing and preserve existing manual work.
- [x] Capture baseline player-height views at the school gate, approach road, and nearby damaged houses.
- [x] Walk the area in Play Mode and record existing collision, NPC pose, weather, and performance issues.
- [x] Mark the playable corridor, low ground, damage focal points, and debris accumulation areas.

Evidence and limitations: [Phase 1 baseline report](SIBUCAO_AFTERMATH_PHASE1_BASELINE.md). Zones are marked in the external work-area map and coordinate file; the scene retains its existing dressing. The corrected walkthrough reached the arrival prerequisite with zero logged errors, and the final entrance stretch was checked separately with that prerequisite temporarily bypassed.

## Phase 2 Flood traces and ground surfaces

- [x] Add irregular silt and mud along road edges, yards, and drainage paths.
- [x] Add shallow puddles in measured low areas with varied shapes and damp margins.
- [x] Break up clean, dry road surfaces with restrained wetness and sediment variation.
- [x] Add watermarks to affected walls and fences consistent with local elevation and intended flood level.
- [x] Accumulate leaves and small rubbish against drains, fences, and corners; eliminate floating or visibly intersecting patches.

Evidence and scope: [Phase 2 ground pass report](SIBUCAO_AFTERMATH_PHASE2_GROUND_PASS.md). Six batches cover the first work area, including three shallow pool areas and edge/corner deposits. Six views were captured and inspected; no colliders or gameplay scripts were added. Duplicate streamed OSM building blocks were hidden in this scene after they obscured the custom school. A fresh Play Mode and performance comparison remains part of Phase 7.

## Phase 3 Building damage and debris

- [x] Establish minor, moderate, and severe damage variation across the selected homes.
- [x] Match displaced roof sheets, fence boards, and timber to nearby damaged structures.
- [x] Improve reusable debris shapes with bent corrugated metal, splintered timber, torn tarps, and waterlogged household items.
- [x] Replace visibly repeated debris arrangements with uneven clusters guided by wind and water direction.
- [x] Check roof openings, exposed interiors, material edges, and collision from player height.

Evidence and scope: [Phase 3 damage and debris report](SIBUCAO_AFTERMATH_PHASE3_DAMAGE_PASS.md). The existing severity pattern is reinforced with 147 shaped pieces, six exposed interior floors, and revised yard clusters. Eight player-height views and two roof overviews were inspected. Existing collider snapshots match; full runtime acceptance remains in Phase 7.

## Phase 4 Vegetation damage

- [ ] Add broken banana leaves, missing palm fronds, and tilted vegetation in exposed areas.
- [ ] Place a small number of snapped or fallen trees with believable ground contact and nearby fallen foliage.
- [ ] Preserve variation between sheltered and exposed planting while keeping the route visible.

## Phase 5 Evacuation center and recovery activity

- [ ] Refine the relief station with organized supplies, water containers, and clear access.
- [ ] Add drying blankets, cleanup tools, and temporary repairs where residents would use them.
- [ ] Verify NPC poses in Play Mode and add appropriate resting, waiting, or cleanup behavior using available animations.
- [ ] Keep the school entrance, interaction points, and movement corridor unobstructed.

## Phase 6 Lighting and sound

- [ ] Refine the sky and soft morning lighting so damage and wet surfaces remain readable.
- [ ] Tune haze, surface reflections, and material brightness from gameplay viewpoints.
- [ ] Add restrained dripping water, gentle wind, loose roofing creaks, and distant cleanup sounds.
- [ ] Confirm runtime scripts preserve the aftermath appearance and do not restart storm effects.

## Phase 7 First area acceptance and wider rollout

- [ ] Capture matching before-and-after views of the school gate, approach road, and nearby houses.
- [ ] Complete a Play Mode walkthrough covering spawn, routes, interactions, and collision.
- [ ] Compare frame time and rendering cost with the baseline on the intended target hardware; optimize added assets where needed.
- [ ] Verify shared assets and other scenes retain their original appearance and behavior.
- [ ] Finish the first area so flood traces, damage, and recovery activity are clearly visible at player height.
- [ ] Extend the established treatment to the remaining playable routes and repeat visual, collision, and performance checks.

## Completion criteria

The playable environment visibly communicates a recent typhoon through ground conditions, connected damage and debris, affected vegetation, and recovery activity. Routes remain usable, weather remains in aftermath mode, and verification evidence is saved.

Existing damage is static. Linking damage to saved preparation decisions or creating new recovery missions is a separate gameplay extension, outside this environment checklist.

## Progress log

| Date | Progress | Evidence or next step |
| --- | --- | --- |
| October 12, 2026 | Checklist created; existing static visual foundation recorded. | Begin Phase 1. No scene changes made for this checklist. |
| October 12, 2026 | Phase 1 complete: verified backup, three baseline views, terrain survey, work-zone map, runtime inspection, and editor performance sample. | See the Phase 1 report. Next: Phase 2 ground surfaces and flood traces. Track direct-entry arrival prerequisites and performance separately. |
| October 13, 2026 | Phase 2 complete in the first work area: silt, damp asphalt, shallow puddles, watermarks, and leaf/litter deposits. Disabled duplicate streamed OSM building blocks. | Six captures and structural checks saved in the Phase 2 report; scene saved in Edit Mode. Next: Phase 3 building damage and debris. |
| October 13, 2026 | Phase 3 complete across eight selected homes: reinforced damage levels, shaped roofing/timber/tarps, waterlogged items, fitted interior floors, and varied yard clusters. | Ten final captures, mesh/material checks, and matching collider snapshots. Next: Phase 4 vegetation damage. |
