# Sibucao Aftermath Phase 2 Ground Pass

Updated: October 13, 2026

Phase 2 adds flood residue to the first work area around the school gate, damaged homes, and final approach road in `Assets/Scenes/SIBUCAO_AFTERMATH.unity`. Coverage follows the [Phase 1 work-area map](../Captures/Aftermath/Phase1_20261012/work-area-map.svg); the wider route remains a later rollout task.

## Ground treatment

- Irregular brown silt follows road shoulders, low ground, and selected fence and building corners. Patch edges fade and follow the measured ground and asphalt elevations.
- Restrained dark wetness breaks up the dry asphalt and creates damp margins around residual water. Triangles crossing abrupt surface height changes are rejected.
- Three shallow pool areas occupy surveyed low ground near the gate. Water surfaces are level, with accepted depths of 1 to 40 mm. Their world elevations are approximately -1.613, -1.516, and -1.455 m; these are separate residual pools, not one active flood plane.
- Wall and gate residue uses a consistent historic world elevation of -0.05 m with uneven edges. This is an art-directed flood mark, not a simulated or surveyed historical water level.
- 315 small leaf shapes and 39 litter scraps collect near edges and corners. Placement samples the supporting surface and excludes the central movement band.
- Eight original pale puddle renderers are disabled in this scene; the original objects and assets remain available.

The scene groups the new work under `Aftermath - Phase 2 flood traces`. Its six mesh/material batches contain 22,821 triangles, with no colliders, rigidbodies, lights, or gameplay scripts added. The assets are isolated under `Assets/StormSim/Sibucao/Art/Aftermath/Phase2/`.

## Duplicate streamed buildings

Review exposed large white blocks covering the school courtyard. These came from hidden Cesium OpenStreetMap tile renderers, which remained visible with all normal scene renderers hidden. The scene's `Cesium OSM Buildings` object, asset 96188, is now inactive so streamed blocks cannot overlap the custom school and neighborhood. This scene-wide visibility override also hides raw OSM buildings beyond the first work area; the authored environment remains. No shared Cesium asset or other scene was edited for this override.

## Verification

The [installation record](../Captures/Aftermath/Phase2_20261012/installation.txt) records geometry counts, pool elevations, and surface queries. The [verification record](../Captures/Aftermath/Phase2_20261012/verification.txt) records valid mesh indices and finite vertices, isolated mesh/material paths, and no reported shader compilation errors. The visible scene collider count remains 7,717; the new root has zero colliders. This count is not a proof that every existing collider is unchanged.

The scene was saved and reloaded successfully. Three comparison views use the Phase 1 camera coordinates, field of view, and 1600 by 900 image size, with three additional close views:

| View | Before | After |
| --- | --- | --- |
| School gate | [Phase 1](../Captures/Aftermath/Phase1_20261012/school-gate.png) | [Phase 2](../Captures/Aftermath/Phase2_20261012/school-gate.png) |
| Approach road | [Phase 1](../Captures/Aftermath/Phase1_20261012/approach-road.png) | [Phase 2](../Captures/Aftermath/Phase2_20261012/approach-road.png) |
| Damaged homes | [Phase 1](../Captures/Aftermath/Phase1_20261012/damaged-houses.png) | [Phase 2](../Captures/Aftermath/Phase2_20261012/damaged-houses.png) |

Detail views: [gate ground](../Captures/Aftermath/Phase2_20261012/gate-flood-traces.png), [roadside deposits](../Captures/Aftermath/Phase2_20261012/roadside-detail.png), and [residual puddles](../Captures/Aftermath/Phase2_20261012/low-ground-puddles.png).

This phase has editor visual and structural checks. It does not supply a fresh Play Mode walkthrough or a frame-time comparison. The Phase 1 arrival prerequisite and editor performance concerns remain open for gameplay acceptance and Phase 7.

## Backup and authoring

The pre-edit scene is preserved at `Backups/Aftermath/Phase2_20261012/SIBUCAO_AFTERMATH.before-phase2.unity`, with SHA-256 `9749D52642C102AFEE7C46EA51983AA7A20D9C7D771E863F5B41CE968D7BBFC9`. The folder date records the start of the pass; completion continued after midnight on October 13.

The deterministic final authoring command is [aftermath-phase2-refine.cs.txt](../Tools/Validation/aftermath-phase2-refine.cs.txt). It rebuilds only the Phase 2 assets and reapplies the original puddle visibility override. The guarded initial installer uses the same final settings. Run these through the Unity editor command runner; they are not runtime components. [aftermath-phase2-verify.cs.txt](../Tools/Validation/aftermath-phase2-verify.cs.txt) captures and validates the result.

The separate [duplicate-building command](../Tools/Validation/aftermath-phase2-hide-duplicate-buildings.cs.txt) records the Cesium scene override. The `diagnostic` capture subfolder contains temporary isolation views, not final results.

Next: Phase 3 building damage and debris, following the [progress checklist](SIBUCAO_AFTERMATH_IMPROVEMENT_CHECKLIST.md).
