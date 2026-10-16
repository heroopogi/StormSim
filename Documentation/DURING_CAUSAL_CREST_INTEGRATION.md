# DURING: persistent B1â€“B9 branches and Crest integration

Implemented in `Assets/Scenes/SIBUCAO_RAIN.unity`, 28 September 2026.

## What the model means

This is an authored, state-dependent branching decision system. Core encounters stay in B1â€“B9 order. Each committed choice appends A/B/C/D to its parent history. The same physical B4 can therefore be reached as B4[AAC], B4[ABC], and so on, with different inherited conditions. It is an implicit tree of histories implemented using reusable scene encounters, not thousands of copied GameObjects and not a machine-learning classification tree.

The 30 current choices permit 46,656 nominal complete choice strings before item, water-tier and other availability restrictions. This count is not a claim that every string is reachable in one run. Current B1/B9 gap actions and B2's fourth swimming action follow the game's assets rather than the older sketch's exact wording.

Each previous choice has an explicit next-stage consumer. The transition records its parent path, explanation, next-stage success-chance adjustment, and additional exertion. Exertion reduces stamina once, when the next core stage is activated. No artificial time is added and the storm is not reset. Existing movement, physical-work requirements, item checks, contact consequences, injury, supply loss, assistance reporting and scoring remain in use. Optional encounters remain optional.

The authorable model is `Assets/StormSim/Sibucao/Data/CausalDuring/DuringBranchModel.asset`. The numeric effects below are initial game-design tuning, not measured disaster probabilities. They need playtesting and thesis review. They do not imply that a dangerous route becomes safe.

## Downstream rules

Chance adjustments are additive, then the existing evaluation clamps nondeterministic chance. Deterministic actions remain deterministic. Low stamina (below 40) and injury (health below 70) add persistent bounded penalties to subsequent probabilistic choices. B9 feeds the arrival state/report instead of another action roll. Transition messages describe the preceding action; its actual success/failure, health and item effects remain in the result record.

| Choice | Actual in-game action | Consumer | Chance adjustment | Extra transition exertion | Visible explanation |
|---|---|---|---:|---:|---|
| B1A | Probe beside the boards | B2 | .08 | 0 | You checked the water before crossing. That careful footing helps at the stalled cars. |
| B1B | Cross the road | B2 | .01 | 1 | The road crossing leaves a little more exertion to carry into the car passage. |
| B1C | Cross through the damaged railing gap | B2 | -.08 | 3 | The damaged railing crossing leaves you less steady at the stalled cars. |
| B2A | Clear the blocked car passage | B3 | .06 | 1 | Clearing the car passage gives you a steadier approach to the canal. |
| B2B | Squeeze between the cars | B3 | -.03 | 2 | Squeezing between the cars leaves you less steady approaching the canal. |
| B2C | Climb across the hood | B3 | -.06 | 3 | Climbing the hood adds exertion before the canal crossing. |
| B2D | Swim beside the submerged cars | B3 | -.1 | 4 | Swimming past the cars leaves extra fatigue at the canal. |
| B3A | Speak to the resident and take the lane | B4 | .08 | 0 | The resident's directions help you approach the next obstruction carefully. |
| B3B | Wade along the main road | B4 | -.05 | 2 | Wading the main road adds exertion before the fallen cable. |
| B3C | Follow the side alley | B4 | .03 | 1 | The side alley gives you a steadier approach to the fallen cable. |
| B3D | Use the covered passage | B4 | .06 | .5 | Using the covered passage leaves less exposure before the fallen cable. |
| B4A | Follow the outside lane | B5 | .06 | 0 | The outside lane leaves you steadier for handling the damaged bag. |
| B4B | Pass beside the cable | B5 | -.04 | 2 | Passing beside the cable leaves you less steady for handling supplies. |
| B4C | Step across the wire | B5 | -.1 | 3 | Crossing the wire leaves extra strain at the damaged bag. Any injury remains with you. |
| B5A | Thread, tighten and fasten the strap | B6 | .05 | 1 | The strap repair attempt costs some effort before helping at the curb. Only supplies actually retained remain usable. |
| B5B | Gather the scattered contents | B6 | -.02 | 2 | Recovering scattered contents adds effort before helping at the curb. Check what you actually recovered. |
| B5C | Leave the bag and continue | B6 | -.06 | 0 | You left the bag behind. Its lost supplies stay unavailable during later assistance. |
| B6A | Lift the chair over the curb | B7 | -.04 | 3 | Lifting the chair adds fatigue on the approach to the rescue checkpoint. |
| B6B | Guide the chair around the ramp | B7 | .04 | 1 | The ramp approach conserves effort before the rescue checkpoint. |
| B6C | Continue past the resident | B7 | -.02 | 0 | You continued without assisting the wheelchair resident. Their location still needs to be reported. |
| B7A | Speak to the responder and follow the landing | B8 | .08 | 0 | The responder's directions help you approach the collapsed evacuee. |
| B7B | Board the boat | B8 | .03 | 1 | You reached the next area by boat. Its landing and passenger details remain part of your arrival report. |
| B7C | Stay on the original road | B8 | -.06 | 3 | Staying on the road adds fatigue before reaching the collapsed evacuee. |
| B8A | Carry the evacuee to the landing | B9 | -.07 | 3 | Carrying the evacuee adds exertion before the creek boundary. |
| B8B | Fetch and lead a neighbour | B9 | .05 | 1 | Fetching a neighbour shares the assistance work before the creek boundary. |
| B8C | Continue along the street | B9 | -.02 | 0 | You left the collapsed evacuee behind. Report their location at the school. |
| B8D | Apply a kit and carry the evacuee | B9 | -.03 | 2 | Treatment and carrying add exertion before the creek. The used kit remains consumed. |
| B9A | Turn inland | Arrival | 0 | 1 | You arrived by the inland route. The arrival record retains your remaining health, energy and supplies. |
| B9B | Follow the creek edge | Arrival | 0 | 2 | You arrived along the creek edge. Any injuries and missing supplies carry into the arrival record. |
| B9C | Cross the storm-damaged bank gap | Arrival | 0 | 4 | You arrived through the damaged bank gap with added exertion. Earlier assistance and losses remain on record. |

## Save and progression behavior

Core encounters cannot activate or commit ahead of the next unresolved core stage. Walking around them does not count as a completed choice. The objective/minimap continues pointing to the unresolved encounter, and entering the school does not automatically mark remaining core encounters missed.

The save contains the full branch string, parent/child IDs on new results, the downstream stage entries and their already-applied effects. Re-entering a stage or loading does not charge that transition again. Sequential older saves can be adopted; older saves that skipped or resolved core stages out of order are rejected before replacing the current run. Their file is preserved. Start a new run to experience the full model from B1.

## Crest and the cellular flood simulation

Crest is the rendering layer. The existing terrain-grid rainfall/runoff model still supplies flood heights, wet/dry cells, currents, swimming depth, buoyancy and decision water tiers. New height/clip/flow inputs feed that data to Crest. The coast retains the existing wave phase and shoreline geometry. No independent Crest wave spectrum changes the gameplay water height. The old surface renderers are hidden only while the configured Crest bridge is ready, and restored when it is disabled.

Crest height and clipping data are rasterized into its LOD textures, so visual height/boundary interpolation can differ slightly from exact CPU terrain triangles, particularly at distant LODs. Check shallow boundaries and shoreline seams during visual QA. Do not claim bit-exact geometry identity or certified headset performance.

Imported Crest compatibility changes guard legacy URP compatibility-pass methods on Unity 6000.5 and later, retaining the RenderGraph implementation. Changes are confined to five imported RenderGraph files; reinstalling/updating Crest may overwrite them. Original files and scene/runtime scripts are backed up under `Backups/2026-09-28-causal-crest`.

## Validation and limits

- `Reports/DuringCausalValidation.txt`: all 30 authored downstream rules, exactly-once transitions, JSON round trips, stage-order rejection, sample ancestry AACAABADA and downstream risk effects.
- `Reports/DuringResolverFixtures.txt`: all 30 choices through the live resolver with completed-attempt test fixtures. These fixtures are not physical playthroughs.
- `Reports/DuringPathCount.txt`: 46,656 distinct nominal complete path IDs, including AACAABADA.
- `Reports/DuringLiveGuards.txt`: live stage activation/bypass/arrival guards and dry-school query.
- `Reports/DuringPhysicalCrossing.txt`: production movement-input unfinished-work and endpoint-bypass guard regression; not a completed crossing. The initial walking-only probe is retained separately; it encountered the intended uncleared timber.
- `Reports/during-crest-runtime-1.png`: live flooded-street capture with Crest enabled; the storm clock was advanced for the visual test.

Full B1â€“B9 human playthrough, final balance, standalone build, and headset performance are not certified by these checks. Existing older probe scripts that mark other core results 98/99 are not valid ordered-model fixtures. The current integration checks intentionally use valid sequential prefixes instead.

The screenshot connector produced a Unity recursive PlayerLoop warning in its own capture helper. Its stack trace points to `MCPForUnity.Runtime.Helpers.ScreenshotUtility.CaptureCompositedAfterFrame`; it was not a compiler or branching exception.

Final editor state: scene saved, Play Mode stopped, compilation successful. The last Play Mode console check returned zero errors.
