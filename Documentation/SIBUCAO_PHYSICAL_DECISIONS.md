> Historical implementation/test record. The current nine-node, 30-choice rebuild and latest per-option results are in [SIBUCAO_BRANCHING_REBUILD.md](SIBUCAO_BRANCHING_REBUILD.md). Earlier single-route PASS results do not verify every alternative.

# Sibucao physical decisions — desktop build

Updated 26 September 2026. Scene: `Assets/Scenes/SIBUCAO_RAIN.unity`.

## Preview and controls

Open SIBUCAO_RAIN, enter Play Mode and click the central Play circle. Click the Game view for mouse capture. Four random starting homes retain the waking/blinking sequence, news and six-second stand-up. Space or the temporary Skip TV button skips the bulletin.

- WASD moves; mouse looks. E opens/closes the door, reads paper, talks, equips the school bag and stores routine supplies.
- Collect the school bag first. I/Tab opens 27 slots; 1–9 takes out a quick-slot item. Ordinary pickup never creates a drag joint. Taking out a medical kit enables its separate physical-use interaction.
- Hold left mouse to grip decision tools/assistance objects; aim and move, use the wheel for reach and R + mouse for orientation, then release to place.
- Space jumps on land. In water, WASD swims, Space rises, Ctrl/C dives. J opens the household guide; Escape pauses.

Scores are hidden until completion. The minimap leads to an encounter; its suggested route lines disappear while that encounter is active. Resident lines describe the surroundings without recommending a branch. Outcome notices share neutral text and appearance.

## Required physical input

Three invisible traversal gates assess approach, passage and exit in order. Moving directly to the last gate cannot complete a route; waiting never earns completion.

| Encounter | Sequence |
|---|---|
| Rising water | Optional held stick probes two points at least 0.65 m apart before bodily entry; then approach, traverse and exit. Movement records measured traversal or rushing. |
| Stalled cars | Approach the chosen gap/passage, move through it and clear the far side. Hood contact is recorded physically. |
| Canal | Optional resident conversation supplies information only; traverse three gates on the selected passage. |
| Fallen cable | Approach, navigate the obstruction and clear it; cable contact is assessed without a score-revealing flash. |
| Broken bag | Carried contents spill. Grip the loose strap, move it at least 0.18 m, align its connector with the buckle and release. Alternatively retrieve the bag/items and leave; partial recovery is recorded. No equipped bag makes breakage not applicable. |
| Wheelchair | Grip, move the body through the ramp checkpoint, position the entire chair/body in the destination and release. An early drop is retryable. |
| Rescue checkpoint | Optional conversation, approach, traverse the selected gangway/road passage, then reach its exit. |
| Collapsed evacuee | Grip, move through the ramp checkpoint, position the complete body and release. A separately equipped first-aid kit requires physical placement/release and is consumed once; treatment alone does not finish moving the person. |
| Creek | Optional two-point probe followed by ordered approach/crossing/exit. Afterward, continue to the actual school entrance; the creek no longer prematurely ends the session. |

Leaving an active situation records partial progress or abandonment. Internal time costs and the accepted score matrix are unchanged; they do not jump the weather clock. Attempts, conversations, probe samples, inventory, delivered objects and transforms are saved. The dry school arrival opens the debrief until a real follow-on scene is assigned.

## Residents

The scene contains 41 cast actors, including 32 background evacuees and four home-warning residents. All 12 imported casual variants and Nolant's avatar passed the existing Humanoid audit. Nolant Idle and Walk provide locomotion; the locally owned Invector Basic Locomotion FREE Run is assigned to all 32 background residents. Speed follows measured stride, with varied pace/phase and smooth playback blending. Nearby players no longer interrupt a moving evacuee with a missing stationary reaction.

Missing: suitable scared/alert gestures, reactions, injured/seated/assistance clips and an age-appropriate elderly model. Static seated/injured staging and procedural swimming remain explicitly temporary. Casual models are not verified Filipino likenesses. Four procedural PLACEHOLDER prefab backups remain unused in the scene.

## Flood, refuge and audio

`Floodwater.mat` / `StormSim/FloodTest/Depth Water` renders a 145 × 367 terrain grid (2 m cells), replacing the old 93-sample route ribbon. Uncovered cells receive rain; conservative four-neighbor transfers follow local surface head and baked obstruction crests, with coastal and authored refuge-drainage outlets. Simulation uses fixed 0.1 s steps and bounded catch-up. Water tiers use 0.15, 0.5, 1.0 and 1.5 m thresholds and drive swimming, movement and encounter eligibility.

Rain starts accumulating after the selected home's preparation window. Delivery accelerates as `0.2 + 2.4 t²` over its rise duration; the integrated initial rain budget is preserved and peak rainfall continues afterward. Weather particles, wind, visibility and thunder frequency build from the same scenario clock. The 1.85 setting is a rise-period rainfall budget, not a maximum local pool depth.

`Evacuation center - protected dry school grounds` defines the scenario's non-inundation area: X −29 to 95, Z 595 to 705. It covers the entrance, courtyard, court and classroom campus. The solver excludes those cells, water triangles are absent, and surface/depth/current queries remain dry. A 4 m perimeter drainage band receives neighboring runoff and counts it as discharged volume; it avoids pooling against an invisible vertical water wall. The drain capacity is an authored scenario assumption, not a measured capacity. The entrance arrival point uses the existing route endpoint. This authored constraint is **not** evidence that the real school is flood-safe.

The grid is a temporary local-cell runoff model using scene geometry and compressed scenario time. It still needs calibrated elevation/rainfall data and formal validation against the thesis methodology. Calm Water / BanagoSeaWaves remains enabled. Player swimming, submerged visibility/audio and procedural NPC treading use the shared water surface.

36 Rigidbody debris placeholders represent branches, roofing sheets, containers and footwear. Existing rain/wind/thunder/creek audio is used. Named empty scream/crowd slots at wheelchair, collapse, checkpoint and boat plus tier-change audio are wired; real recordings remain unassigned. Audio events depend on proximity or environment, not correctness.

## Assets and evidence

Higgsfield import previously completed: 12 validated prefabs, 20,072 triangles total, 1024px textures and 127 scene placements. See `HIGGSFIELD_PROP_BRIEFS.md`. Four home calendars are printed mesh props, not calendar TextMeshes.

- `SIBUCAO_GAMEPLAY_PROBE.txt`: current scripted production-input regression, including failures if encountered.
- `SIBUCAO_STORM_REFUGE_PROBE.txt`: current storm progression, dry-campus, mass-balance and repeatability checks.
- `SIBUCAO_RIG_AUDIT.json`, `SIBUCAO_FOOT_CONTACT_CHECK.txt`: prior avatar/retargeting/contact measurements.
- `SIBUCAO_SWIMMING_PROBE.txt`, `SIBUCAO_SWIM_SHORE_CHECK.txt`: prior swimming and shore traversal checks.

Tests use Editor input hooks that drive the production controller/physics. They are not native keyboard automation, participant TAM evaluation, every-branch/every-spawn coverage, hydrological calibration or headset validation. See the dated storm/refuge report for final results and remaining limitations.
