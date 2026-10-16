# Sibucao Aftermath Phase 3 Damage and Debris

Completed: October 13, 2026

Phase 3 improves the eight selected damaged homes near the school approach in `Assets/Scenes/SIBUCAO_AFTERMATH.unity`. The existing two intact roofs and six roof openings retain their geometry and collision. New yard dressing, loose roof strips, and visible interior floors make the damage levels easier to distinguish.

## Completed work

- Replaced repeated fence-and-flat-sheet arrangements with unequal surviving fence lengths, splintered pickets, fallen rafters, and irregular clusters of bent corrugated sheets.
- Matched displaced metal colors to each home's sampled roof color; surviving and fallen pickets share the same weathered finish.
- Added torn blue tarps, soaked bedding at the severe sites, and small washing basins where clear space allowed. Small debris tends toward one shared direction with angular variation, as an artistic wind/water cue rather than a simulated trajectory.
- Added fitted damp floors and fallen interior timber beneath the six open roofs. These floors are visual dressing inside existing structures, not newly playable interiors.
- Seated six curled sheet strips along existing roof rafters. Corrected 25 surviving fence boards so their broken ends point upward and their bases contact the ground.
- Retained the original red barriers, roof rafters, walls, and roof collision. Hid 90 superseded dressing renderers instead of deleting the original objects.

## Damage distribution

The severity assignments and roof openings existed before this phase; this pass reinforces them through the amount and type of surrounding damage.

| Home | Damage level | New shaped pieces | Player-height view |
| --- | --- | ---: | --- |
| House 1446 | Severe | 25 | [View](../Captures/Aftermath/Phase3_20261013/House-1446-player.png) |
| House 1511 | Moderate | 18 | [View](../Captures/Aftermath/Phase3_20261013/House-1511-player.png) |
| House 1420 | Minor | 9 | [View](../Captures/Aftermath/Phase3_20261013/House-1420-player.png) |
| House 1559 | Moderate | 19 | [View](../Captures/Aftermath/Phase3_20261013/House-1559-player.png) |
| House 1572 | Severe | 26 | [View](../Captures/Aftermath/Phase3_20261013/House-1572-player.png) |
| House 1589 | Minor | 9 | [View](../Captures/Aftermath/Phase3_20261013/House-1589-player.png) |
| House 1579 | Moderate | 15 | [View](../Captures/Aftermath/Phase3_20261013/House-1579-player.png) |
| House 1608 | Severe | 26 | [View](../Captures/Aftermath/Phase3_20261013/House-1608-player.png) |

Minor sites keep their roofs intact and have light fence/yard damage. Moderate sites retain partial roof loss with smaller debris groups. Severe sites retain larger openings and have heavier roof-sheet, timber, tarp, and bedding deposits.

Roof comparisons: [south before](../Captures/Aftermath/Phase3_20261013/before-roofs-south.png), [south after](../Captures/Aftermath/Phase3_20261013/after-roofs-south.png), [north before](../Captures/Aftermath/Phase3_20261013/before-roofs-north.png), [north after](../Captures/Aftermath/Phase3_20261013/after-roofs-north.png).

## Verification and limits

- Eight player-height views and two roof overview views were captured and inspected. Obstructed review cameras were repositioned; existing scene objects were retained.
- The 147 pieces are combined into 49 mesh/material batches with 20,694 triangles. Ninety original renderers were disabled, but this is not a measured frame-time improvement.
- All new mesh vertices and indices passed structural checks, materials reference Phase 3 assets, and their shaders report no compilation errors.
- No colliders, rigidbodies, or runtime scripts were added. Before/after serialized collider snapshots match exactly, and the existing roof MeshCollider references match their MeshFilters. Their snapshot SHA-256 is `E16A8F142C2CC4121B3F044E292FA158B27EDEA3BF25BE7D1EA0C1A8E26AB8C9`.
- Ground placement checks reject overhead obstructions and reserve the route center. The nearest recorded loose-item center is 2.514 m from the authored route; this is a center distance, not an edge-clearance measurement.
- These are editor visual and structural checks. A fresh Play Mode walkthrough and performance comparison remain part of Phase 7. The Phase 1 mission-entry prerequisite remains unresolved. NPC outstretched poses in these Edit Mode images are not a runtime animation test.

Evidence: [installation record](../Captures/Aftermath/Phase3_20261013/installation.txt), [refinements](../Captures/Aftermath/Phase3_20261013/refinement.txt), [verification](../Captures/Aftermath/Phase3_20261013/verification.txt), and [placement coordinates](../Captures/Aftermath/Phase3_20261013/placements.csv).

## Assets and recovery

New scene root: `Aftermath - Phase 3 damaged homes`, organized by home and severity. New meshes and materials live under `Assets/StormSim/Sibucao/Art/Aftermath/Phase3/`. Reusable generation routines for boards, corrugated sheets, tarps, bedding, basins, and fitted floors are recorded in the [installer](../Tools/Validation/aftermath-phase3-install.cs.txt). It is guarded against reapplying the pass. The separate [one-time refinement](../Tools/Validation/aftermath-phase3-refine.cs.txt) produces the final orientation/contact corrections; the [verification command](../Tools/Validation/aftermath-phase3-verify.cs.txt) records the final views and checks. These commands run in the editor and are not included as runtime components.

The pre-edit scene is preserved at `Backups/Aftermath/Phase3_20261013/SIBUCAO_AFTERMATH.before-phase3.unity`, SHA-256 `B286F98784492205B1DFBADC053508447E474824DC57B5ECAAD0F692778C9E35`. Restore only when intentionally discarding later scene work; the original disabled dressing also remains available for targeted reversal.

Next: Phase 4 vegetation damage. See the [progress checklist](SIBUCAO_AFTERMATH_IMPROVEMENT_CHECKLIST.md).
