# Sibucao Aftermath Phase 1 Baseline

Date: October 12, 2026

Scene: `Assets/Scenes/SIBUCAO_AFTERMATH.unity`

The first improvement area covers the school gate, approach road, and nearby damaged homes. The baseline preserves the existing scene and establishes camera positions, terrain measurements, and work zones for later visual comparisons.

## Backup and preservation

- Backup: `Backups/Aftermath/Phase1_20261012/SIBUCAO_AFTERMATH.before-phase1.unity`.
- Original scene metadata is preserved as `SIBUCAO_AFTERMATH.before-phase1.unity.meta.txt` to avoid introducing a duplicate Unity asset GUID.
- Scene and backup SHA256: `9749D52642C102AFEE7C46EA51983AA7A20D9C7D771E863F5B41CE968D7BBFC9`.
- The scene was clean before capture. Temporary capture cameras were removed, and the saved scene was reloaded after the first runtime diagnostic to clear their editor dirty flag.
- Work zones are recorded externally; no decorative assets or permanent markers were added to the scene.
- Final verification: Unity is back in Edit Mode, the scene is clean, no temporary baseline cameras remain, and the saved scene still matches the backup hash. See [editor state](../Captures/Aftermath/Phase1_20261012/final-editor-state.txt) and [backup verification](../Captures/Aftermath/Phase1_20261012/backup-verification.json).

## Baseline views

The three static images use a 65 degree field of view, 1600 by 900 output, and a camera position 1.63 metres above measured terrain. Exact positions and look targets are saved in the [camera manifest](../Captures/Aftermath/Phase1_20261012/camera-manifest.csv).

| View | Evidence | Starting observation |
| --- | --- | --- |
| School gate | [Baseline image](../Captures/Aftermath/Phase1_20261012/school-gate.png) | Relief supplies are established; the courtyard and approach still look largely clean. |
| Approach road | [Baseline image](../Captures/Aftermath/Phase1_20261012/approach-road.png) | Broad dry-looking road surface, repeated branch shapes, and isolated pale puddles. |
| Damaged homes | [Baseline image](../Captures/Aftermath/Phase1_20261012/damaged-houses.png) | Existing facade stains, damaged roofing, fence fragments, and displaced sheets provide a foundation for more connected damage. |

The outstretched NPC poses in static captures do not establish a runtime animation defect. Residents were observed moving with animated poses during the first Play Mode walkthrough.

## Work area and placement constraints

See the [work area map](../Captures/Aftermath/Phase1_20261012/work-area-map.svg) and [zone coordinates](../Captures/Aftermath/Phase1_20261012/work-zones.csv).

- Survey bounds: world X -58 to 18; world Z 642 to 696.
- Terrain survey: 1,092 samples at 2 metre spacing, with measured Y values from -1.8008 to 1.0013 metres.
- Protected movement route: the final 80 route-distance units approaching the school, recorded in [route.csv](../Captures/Aftermath/Phase1_20261012/route.csv). The proposed clear band extends 1.5 metres to each side of that polyline. It is a placement constraint, not a claim that every point in that band has been collision-tested.
- Lower ground candidate L1 lies near the gate, X -12 to 8 and Z 642 to 656. Terrain sampling includes ground beneath buildings; exposed surface and drainage checks are required before selecting puddle locations.
- Damage focal areas D1 and D2 cover the existing damaged houses beside the approach. Use their existing damage as the source for displaced roofing and timber.
- Debris candidates A1, A2, and A3 cover roadside and entrance-side deposits. Apply the protected route constraint before placing assets.
- Recovery area R1 surrounds the existing relief table at X 8, Z 661. Keep its access clear.

The map's terrain colors describe world elevation, not flood depth. Its zones propose future work; they do not represent newly placed scene objects or a simulated water-flow solution.

## Runtime inspection

At Play Mode startup, the player remained at the school spawn, approximately `(0, -1.48, 657)`. Aftermath mode was enabled, rain and thunder audio were stopped, and the flood simulation component remained disabled. See [runtime startup state](../Captures/Aftermath/Phase1_20261012/runtime-start.txt).

The automated walkthrough uses the existing `SibucaoEditorInput` path and normal player controller, following the last 76 route-distance units and a 4 metre entrance extension. It bypasses the original new-game flow and isolates test save paths. It is an environment movement check, not a complete mission or save-continuity test.

The first diagnostic reached all 21 waypoints, including the entrance extension. Its frame-time sample is rejected: bypassing new-game initialization left supplementary decision arrays undersized and produced repeated exceptions at `DecisionTreeManager.cs:189`. This was a diagnostic setup error, not evidence of a defect in the normal new-game flow. The corrected diagnostic calls the existing `EnsureNodeCapacity` method before enabling movement. Original results are retained under `diagnostic-first-run` for traceability.

The corrected diagnostic recorded zero errors. It reached the school entrance at approximately `(-0.33, -1.50, 654.35)` before the existing arrival prerequisite opened Recovery at waypoint 19 of 21. `DecisionTreeManager.CheckEndDestination` requires earlier evacuation stages to be completed. This is an inherited mission-flow restriction exposed by direct aftermath entry, not a collision obstruction or evidence of damage from the weather. At final inspection, health was 100, rescue was false, and water depth at the player was zero.

A separate entrance-only collision check temporarily bypassed that prerequisite in runtime state, walked the remaining entrance stretch, and restored the flag afterward. See [entrance check](../Captures/Aftermath/Phase1_20261012/entrance-check.txt). These checks do not establish that the scene has a finished aftermath entry flow.

Evidence: [walkthrough result](../Captures/Aftermath/Phase1_20261012/walkthrough-result.txt), [frame samples](../Captures/Aftermath/Phase1_20261012/walkthrough-frames.csv), [runtime approach image](../Captures/Aftermath/Phase1_20261012/runtime-approach.png), [runtime arrival image](../Captures/Aftermath/Phase1_20261012/runtime-walkthrough-end.png), and [final diagnostic state](../Captures/Aftermath/Phase1_20261012/runtime-final-state.json).

## Performance observations

The corrected run's 302 recorded Playing-mode frames after the first 20 warmup frames averaged 49.21 ms, with a median of 36.42 ms and a 95th percentile of 76.58 ms. The editor reported an NVIDIA GeForce RTX 2050, PC quality, and a 1920 by 1080 game surface. See [performance summary](../Captures/Aftermath/Phase1_20261012/playing-performance.json).

These are editor diagnostics, including editor and capture overhead. They are not a standalone-build benchmark. Recovery-mode frames are excluded from this summary because that mode pauses gameplay. The full walkthrough result also contains an aggregate across modes and must not be used as the gameplay-only baseline.

The captures also caused a shadow-atlas warning that additional punctual-light shadow resolution was reduced to fit six shadow maps in a 2048 by 2048 atlas. Revisit shadow cost and light coverage during later lighting and performance work.

## Follow-up priorities

1. Improve ground wetness, silt, and puddle integration around the measured route and low areas.
2. Connect existing damage to nearby debris and reduce repeated shapes and spacing.
3. Replace evacuation-oriented NPC activity with appropriate recovery activity in the later recovery pass.
4. Retain access through the school gate and around the relief station.
5. Compare performance with this diagnostic under matching editor conditions, then validate on the intended player build and hardware.
6. Before treating aftermath as an independent playable phase, define its entry state and decide how completed evacuation progress is handed off. Direct entry currently retains the original arrival prerequisites.

## Reproduction files

- `Tools/Validation/aftermath-phase1-baseline.cs.txt`: Unity command for baseline images and measurements. It writes to the dated capture folder; preserve that folder before rerunning.
- `Tools/Validation/aftermath-phase1-walkthrough.cs.txt`: Unity Play Mode movement diagnostic with isolated saves and a 90 second limit.
- `Tools/Validation/aftermath-phase1-map.ps1`: regenerates the map from recorded measurements and proposed zones.

The first Play Mode assembly reload took approximately 196 seconds; Unity's bridge timed out after 180 seconds and subsequently reconnected. This startup delay is separate from gameplay frame time.
