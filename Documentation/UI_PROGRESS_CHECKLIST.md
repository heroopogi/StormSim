# StormSim UI progress checklist

Updated: 20 October 2026. Scope: the agreed low-poly UI checklist for Baybayon and Riverside, including desktop and VR acceptance.

**Current: 13 checks passed; 27 acceptance tasks open.** These are tracking counts, not a weighted project percentage. The earlier 90% estimate is not a measured result. A feature can be implemented while its acceptance task remains unchecked. Latest live testing: [acceptance test report](UI_ACCEPTANCE_TESTS_20261020.md).

Check an item only after its acceptance condition passes and evidence is recorded. Use **Untested**, **Needs changes**, or **Blocked** beside an unchecked item. Reopen a checked item when a related change invalidates its evidence. Do not count isolated renders as full gameplay or headset tests.

## Verified baseline

- [x] V01 Baybayon capture matrix: 163 images, no reported text overflow; 1280 × 720, 1024 × 768 and 1920 × 1080 at 100% and 130% text. Controlled UI states, not a completed journey. Evidence: `Captures/UICompletion/BAYBAYON_RAIN/inspection.txt`.
- [x] V02 Main-menu capture matrix: 33 images, no reported text overflow. Evidence: `Captures/UICompletion/MainMenu/inspection.txt`.
- [x] V03 Riverside reduced-detail capture run completed: 159 images and 65 passing checks. The item-description failure and route-preview omissions are tracked below; this is not a blanket visual pass. Evidence: `Captures/UICompletion/RIVERSIDE_RAIN/inspection.txt`.
- [x] V04 Corrected longest observed item description fits at 100% and 130% text in the isolated production UI check. Evidence: `Captures/UICompletion/ItemLayoutRecheck/inspection.txt` and `item-1.3.png`.
- [x] V05 Loading panel fits maximum text; simulation suspension and explicitly invoked abort cleanup pass in edit mode. Evidence: `Captures/UICompletion/loading-check.txt`. Actual scene loading remains open.
- [x] V06 Eight hardware-independent pointer checks pass, including disabled, hidden and clipped targets. Evidence: `Captures/UICompletion/ItemLayoutRecheck/pointer-checks.txt`.
- [x] V07 Shared modal callbacks pass for Resume, settings pause and confirmation cancellation. Evidence: Riverside inspection report.
- [x] V08 Tested settings toggles retain focus and tested offscreen actions scroll into view. Evidence: Riverside inspection report.
- [x] V09 Isolated manual save/load restores recorded health; saved-experience assertions pass. Evidence: Riverside inspection report and completion audit. Full-state restoration remains open.
- [x] V10 Runtime and editor validation builds pass with zero errors; existing warnings remain. Evidence: `Captures/UICompletion/runtime-build.log` and `editor-build.log`.

## Design and screen coverage

- [ ] D01 **Untested** — Review every required screen for consistent chamfers, flat colors, borders, typography, spacing, hierarchy and button states. Record and fix exceptions.
- [x] D02 Screen inventory completed with implementation references, scoped acceptance results and remaining tests for each screen/state. Evidence: [UI screen inventory](UI_SCREEN_INVENTORY.md). Inventory completion does not accept the individual screens.

## Layout and readability

- [x] L01 Corrected item panel verified in Baybayon Play Mode with notice, prompt and subtitle at 130% text, 1280 × 720, reduced detail. Screenshot visually inspected; zero reported overflow. Riverside item/notice/prompt capture also passed. Evidence: `Captures/UIAcceptance/BAYBAYON_RAIN/preparation-item-notice-130.png` and both locations' `layout.txt`.
- [x] L02 Actual Riverside route preview verified with all cards, narration, warning and vitals at 100% and 130% text, 1280 × 720, reduced detail. Both screenshots visually inspected; zero reported overflow. Evidence: `Captures/UIAcceptance/RIVERSIDE_RAIN/route-stress.txt`, `route-stress-100.png`, `route-stress-130.png`.
- [ ] L03 **Blocked** — Repeat Riverside visual checks at default graphics quality without a memory failure. Requires sufficient available memory.
- [ ] L04 **Untested** — Stress long objectives, item descriptions, warnings and subtitles together; retain readable text and access to all content.
- [ ] L05 **Untested** — Verify contrast and non-color status cues in bright, dark, rainy and flooded environments.
- [ ] L06 **Untested** — Confirm the supported resolution/aspect-ratio list, then check screen margins and layout across it. Existing evidence covers three resolutions and two text-scale endpoints.
- [ ] L07 **Untested** — Verify every scrollable screen at maximum text size: all content and actions remain reachable with each supported input method.

## Transitions and input

- [ ] I01 **Needs changes** — Actual Riverside → menu → Baybayon Continue test reproduced time scale 1 behind ResumeSelection, which should remain paused. Loading overlay, duplicate-request protection and menu input restoration passed. Evidence: `Captures/UIAcceptance/transitions.txt`. Fix and retest both launch modes and both locations.
- [ ] I02 **Untested** — Restart each location and cancel a restart; verify the correct preparation state, checkpoint behavior and preserved manual saves.
- [ ] I03 **Untested** — Return to menu and exercise exit confirmation/cancellation; no stuck overlay, duplicate panel or unintended loss of progress.
- [ ] I04 **Untested** — Pause and open settings during preparation, evacuation and physical tasks; verify timers/work stop and resume correctly.
- [ ] I05 **Untested** — Complete keyboard/mouse navigation and panel open/close checks, including focus and cursor restoration.
- [ ] I06 **Untested** — Complete gamepad navigation, scrolling, activation, back/pause and route-selection checks.
- [ ] I07 **Untested** — Exercise every button in the screen inventory; verify its action, disabled/unavailable explanation and predictable focus.
- [ ] I08 **Untested** — Verify item collection, objective completion and decisions produce feedback, and HUD values match live health, stamina, inventory and objectives.

## Full flow and saved progress

- [ ] F01 **Untested** — Complete Baybayon: preparation → evacuation → route choice → recovery or handover → debrief. Verify location-specific instructions throughout.
- [ ] F02 **Untested** — Complete Riverside through the same flow and verify its location-specific instructions.
- [ ] F03 **Untested** — Save, change progress, then restore manual saves and checkpoints in both locations; compare position, inventory, objectives, decisions and vitals with the saved state.
- [ ] F04 **Untested** — Compare debrief outcomes/timeline with decisions actually made in each completed run; verify replay and location-change actions.

## Accessibility and VR

- [ ] A01 **Untested** — Verify text size, subtitle, guidance, audio and comfort preferences apply consistently and persist across restart/location changes.
- [ ] A02 **Needs changes** — XR text conversion changes narration's pronoun “I” to controller label “B.” The exact source rule reproduced `I need to put this down first.` → `B need to put this down first.` Evidence: `Captures/UICompletion/vr-text-regression.txt`. Fix the conversion, retest prose and actual control labels, then complete input/subtitle/guidance acceptance. No headset was used for this rule-level reproduction.
- [ ] A03 **Blocked** — XR simulator configuration has automatic instantiation disabled and no simulator prefab assigned. Fresh runtime enumeration found XR inactive and zero XR devices. Configure and run simulator interaction checks for pointing, selection, activation, scrolling and panel transitions. Evidence: `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset`, `Captures/UIAcceptance/devices.json`.
- [ ] A04 **Blocked** — Check headset text readability, panel size/distance/placement, margins and environmental visibility at normal and maximum text. No headset connected at last confirmation.
- [ ] A05 **Blocked** — Verify both controllers, dominant-hand settings and pointing/activation agreement on the target headset.
- [ ] A06 **Blocked** — Verify seated and standing use, recentering and reachable controls on the target headset.
- [ ] A07 **Blocked** — Verify snap/smooth turning, vignette, reduced motion/flashes and other advertised comfort settings on the target headset.

## Final acceptance

- [ ] Q01 **Untested** — Save final normal/maximum-text captures for every major screen after fixes; remove placeholder copy, obsolete styling and unused visible UI found during the review. Inventory review confirmed the expanded fixture does not cover the preparation introduction, actual physical guide, conversation or Decision dialog.
- [ ] Q02 **Untested** — Resolve UI errors, retest affected items, separately record remaining non-UI issues, and close all required tasks with evidence.

## Next actions and dependencies

| Order | Action | Needed from |
| --- | --- | --- |
| 1 | Fix the reproduced Continue time-scale failure and narration conversion; restore responsive Unity testing | Codex; sufficient available memory |
| 2 | Complete desktop flows, saves, input and remaining visual acceptance | Codex and hands-on tester where native input is required |
| 3 | Run simulator checks, then headset acceptance | Simulator configuration and connected headset/controllers |
| 4 | Fix failures, capture evidence, update this checklist and close Q02 | Codex and tester |

Last observed blocker: severe RAM pressure during Unity verification. Recheck current availability before starting; do not treat historical memory readings as live measurements.

## Test record

Add a row for each new test. For a failure, include reproduction steps and the follow-up fix/retest reference. Record graphics quality, resolution, text scale and input/device so the result has a clear scope.

| Date | Checklist ID | Location and screen | Configuration | Pass or fail | Evidence path | Fix or follow-up |
| --- | --- | --- | --- | --- | --- | --- |
| 20 Oct 2026 | V04 | Isolated item panel | 100% and 130%; production font/sizing | Pass | Captures/UICompletion/ItemLayoutRecheck/inspection.txt | L01 remains open |
| 20 Oct 2026 | V05 | Isolated loading panel | Maximum text; edit-mode cleanup invoked explicitly | Pass | Captures/UICompletion/loading-check.txt | I01 remains open |
| 20 Oct 2026 | D02 | All required screen families | Source builders and existing capture reports | Pass for inventory completeness | Documentation/UI_SCREEN_INVENTORY.md | Individual acceptance remains scoped by row |
| 20 Oct 2026 | A02 | Narration conversion | Exact source regex/replacement executed in Unity's code runner; no headset | Fail | Captures/UICompletion/vr-text-regression.txt | Distinguish control labels from prose |

**Completion rule:** all required tasks must pass, with final fixes retested, before claiming 100% acceptance. Do not silently remove blocked headset tasks or substitute screenshots for interaction tests. Track implementation progress separately from acceptance coverage.

Detailed supporting evidence: [UI completion audit](UI_COMPLETION_AUDIT_20261019.md). The older [master checklist](STORMSIM_UI_MASTER_CHECKLIST_STATUS_20261018.md) is historical and includes additional proposals beyond this agreed acceptance scope.
