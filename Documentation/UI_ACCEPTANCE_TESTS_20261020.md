# UI acceptance test run — 20 October 2026

This run exercised the production UI in Unity Play Mode in Baybayon and Riverside. It used controlled states, synthetic keyboard/gamepad input and isolated save directories. Screenshots are 1280 × 720; graphics were reduced for memory use. These results do not establish native-device usability, full participant journeys or headset acceptance.

## Results

- Both locations: 14 assertions passed per location. Preparation starts; Escape opens/closes preparation settings; settings stop preparation time and movement; the editor handoff reaches gameplay; Escape and synthetic gamepad Start pause/resume gameplay; route preview activates; manual save and checkpoint write; tested health/stamina fields restore. See each location's `checks.txt` in `Captures/UIAcceptance/`.
- Baybayon: visually inspected the corrected item panel with simultaneous notice, prompt and subtitle at 130% text. Zero reported overflow. L01 closed for this configuration.
- Riverside: visually inspected actual route cards with warning, narration and vitals at 100% and 130% text. Zero reported overflow. L02 closed for this configuration.
- Actual Riverside → main menu → Baybayon Continue: overlay, duplicate-load rejection, target scene and menu input restoration passed. **Failed:** after Continue opens ResumeSelection, time scale is 1 instead of 0. Loading completion overrides the target modal's pause. See `Captures/UIAcceptance/transitions.txt`.
- Baybayon restart: confirmation and programmatic cancellation passed. The actual reload stalled before completion assertions; this is incomplete, not a successful restart. Unity stopped responding promptly to the validation queue during the reload. Riverside restart was not subsequently attempted.
- Riverside full-journey helper: aborted with NullReferenceException in `SibucaoJourneyChecks.Tick`, at `help.Pending`, because the expected assistance component was absent. This is a test-helper compatibility failure, not evidence that the actual player journey fails. See `Captures/UIAcceptance/RIVERSIDE_RAIN/journey.txt`.
- Fresh device enumeration: XR inactive, zero XR devices, keyboard and mouse present. XR simulator settings disable automatic instantiation and have no assigned simulator prefab. No simulator or headset test passed.
- Previously reproduced XR narration conversion failure remains: the control-label conversion changes the pronoun “I” into “B”. No production code was changed in this test run.

## Disposition of every remaining acceptance item

| ID | Result / remaining coverage |
| --- | --- |
| D01 | Partial evidence only. All-screen style and state review remains open. |
| L03 | Blocked by unstable scene-load performance at reduced detail; default-quality Riverside was not attempted. |
| L04 | Partial: simultaneous item/notice/prompt/subtitle and route/warning/narration/vitals tested. Exhaustive long-objective stress remains. |
| L05 | Untested across the complete bright/dark/rain/flood matrix. |
| L06 | Existing three-resolution fixture evidence remains; supported-target agreement and comprehensive margins review remain. |
| L07 | Untested across every scroll screen and input method. |
| I01 | Failed: Continue loses paused time scale after real load. |
| I02 | Partial: Baybayon confirmation/cancel passed; reload incomplete; Riverside restart untested. |
| I03 | Partial: real return to main menu passed. Exit/cancel and progress preservation remain. |
| I04 | Partial: preparation clock/movement frozen in settings; gameplay pause/resume passed in both locations. Physical-work and all-phase suspension remain. |
| I05 | Partial: synthetic Escape checks passed in both locations. Complete keyboard/mouse focus, cursor and navigation remain. |
| I06 | Partial: synthetic Start pause/resume passed in both locations. Navigation, scrolling, back and route activation remain. |
| I07 | Untested as an exhaustive every-button acceptance matrix. |
| I08 | Untested as complete live collection/objective/decision feedback acceptance. |
| F01 | Full Baybayon participant journey not completed. Editor handoff/route fixtures are insufficient. |
| F02 | Attempted; journey helper incompatible with this scene. Full Riverside journey not completed. |
| F03 | Partial: isolated manual and checkpoint vitals restored in both locations. Position, inventory, objectives and decision comparisons remain. |
| F04 | Untested against decisions from completed runs. |
| A01 | Untested across the complete persistence/location-change matrix. Test scripts restore their temporary settings. |
| A02 | Existing reproduced failure remains; prose/control-label distinction needs fixing and retesting. |
| A03 | Blocked: simulator disabled/unassigned; runtime XR inactive. |
| A04 | Blocked: no headset for readability/placement acceptance. |
| A05 | Blocked: no controllers for hand/pointing acceptance. |
| A06 | Blocked: no headset for seated/standing/recenter acceptance. |
| A07 | Blocked: no headset for comfort acceptance. |
| Q01 | Partial captures added. Full final screen matrix after fixes remains. |
| Q02 | Open: failures and untested coverage remain. |

The tracker now has 13 checked items and 27 open items. This is a checklist count, not a design/implementation percentage. Only L01 and L02 were closed by this run; limited assertions were not promoted into broad acceptance passes.

## Evidence and cleanup

Scripts and results are in `Captures/UIAcceptance/`. Original editor graphics values are recorded in `editor-state.json` (mip limit 0, shadows 2, MSAA 2). `cleanup.txt` confirms Play Mode stopped, those values were restored and the editor returned to `Assets/Scenes/RIVERSIDE_RAIN.unity`. The restart attempt was stopped without completion evidence. No scene save, asset refresh or production-source edit was requested during the tests. `ProjectSettings/QualitySettings.asset` has no Git diff.
