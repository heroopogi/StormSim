# Batch 1 — UI and controls implementation

Implemented for the desktop SIBUCAO_RAIN flow on 29 September 2026. The low-poly assets, flood model and assessment rules are preserved.

## Controls

| Key | Preparation | Evacuation |
| --- | --- | --- |
| E | Use/open an object or place a supply | Use/open/place; direct collection remains available |
| F | Carry/release an item; collect the bag | Carry/release a tool or supply; handle side-mission carry tasks |
| Q | Put down the held supply, then the bag | Put down/cancel the current action |
| P | Pack the held supply into an open bag | Pack the held supply into the equipped bag |
| I / Tab | Inspect bag; open/close it or take out a supply | Inspect bag; take out, eat or drop a selected supply |
| J | Guide and preparation checklist | Scenario guide |

The evacuation bag remains equipped, while preparation retains its physical two-hand bag. Mouse-held tools and the existing preparation B/G shortcuts remain compatible. Inspecting the evacuation bag or guide no longer automatically packs a held item. F releases an already held physical item before starting another carry action.

## Presentation

- Shared desktop safe areas for the objective, action prompt, messages and vitals.
- Compact route map, with a comparison panel when approaching a junction.
- Contextual teaching replaces persistent control strips and repeated floating mission labels.
- Scrollable bag/guide panels remain inside the attribution safe area.
- Initial preparation points to household storage and the go-bag. Household warnings are explicitly optional.
- Junction signs become 1.55 m high with 1.6 m wide boards, positioned farther from the center of the approach. Existing signs are fitted when each route layout is applied; the route builder creates matching signs.

## Development access

Temporary preparation skip, television skip and the storm clock are off by default. In the editor, use **StormSim > Development > Show skip and clock controls**. A development player also requires the `-stormsim-dev-controls` launch argument. Normal player builds cannot enable these controls.

## Validation

Runtime and editor assemblies compiled successfully. The following technical checks passed:

- 19 existing preparation tests, covering closed storage, two-hand packing, save/load, warnings, sleep and handoff state.
- 11 new preparation checks, covering the initial objective, F/Q handling, bag-panel cursor state and development defaults.
- Packing across actual live frames.
- Evacuation F carry persistence, P packing, bag inspection without automatic packing, modal input blocking, Q release through the player Update loop, and F carry/release for a side-mission object.
- Development clock disabled by default and available after explicit opt-in.
- All 18 approach signs use the fitted dimensions.
- The actual Unity HUD rectangles and independent arithmetic checks pass at 640×360, 688×329, 1280×720, 1440×900, 1920×1080 and 2560×1080. The eight gameplay regions do not overlap and retain at least 44 actual pixels for attribution.
- Screenshots inspected at 1280×720 and 688×329, including preparation, evacuation, both bag panels and the guide.

Reports and screenshots: `Captures/Batch1_20260929/`. Final guide/carry-priority regressions passed: the guide preserved the held supply across live frames and blocked packing; F released that supply before a subsequent F began the side-mission carry. See `final-regression-checks.txt`.

First-time-player acceptance remains separate from technical verification: start a fresh run, retrieve/open the bag, carry/pack/drop a supply, inspect the bag, leave home, and explain the first junction using only the on-screen instructions.

## Broader route findings

The existing full route fixture completed 11 missions and reported seven failures: B1_right_v2, B4_right, B5_left, B6_left, B8_right, B9_left and B9_right. These involve escort progress, focus and completion steps. See `Documentation/SIBUCAO_THREE_WAY_INTERACTION_CHECKS.txt`. They remain unresolved Batch 2 findings; without a baseline comparison they are not classified as pre-existing defects or regressions. The mission IDs use B-number prefixes that are separate from the improvement-batch numbering.

No standalone player build or XR playtest was run in this desktop pass.
