# UI completion audit — 19 October 2026

This report replaces percentage estimates with explicit implementation and verification evidence. Existing unrelated scene/gameplay changes were preserved. The user confirmed that no VR headset is connected.

## Scope and implementation

| Area | Implementation in the project | Verification required |
| --- | --- | --- |
| Low-poly styling | Shared chamfer sprite, flat navy/seafoam/amber palette, Inter/Sora typography, labeled warnings, focus/pressed/disabled states | Captures across all screens and environments |
| Main menu and locations | Authored home retained; location selection and separate saves; settings now also exposes controls guide, credits, and exit | Actual main-menu scene and navigation |
| Onboarding | Three VR setup pages, play position, hand, turning and pointing explanation | Hardware pointing, comfort and seated/standing acceptance |
| Preparation | Objective, physical field guide, bag, advisory phone, departure and conversation panels; instruction card now follows text scale | Preparation captures and physical interactions |
| HUD | Objectives, action prompts, item details, subtitles, priority notices and labeled vitals; stacked layout and route-card clearance | Concurrent-message and maximum-text captures |
| Route choices | Three preview cards with distance and written footing/closed status; keyboard, pointer and gamepad D-pad selection | Active preview in both locations; actual passage commitment |
| Pause and settings | Resume/save/load/recovery/settings/menu; added restart confirmation; menus scroll focused actions into view | Callback, scrolling and paused-time checks |
| Restart and replay | Confirmed scene reload starts a new preparation run; copy explains checkpoint replacement and retained manual saves | Scene transition and save preservation |
| Recovery and arrival | Assistance/recovery options, save selection and school handover use existing run data | Captures plus actual rescue/arrival flow |
| Debrief | Five pages based on recorded outcomes; long cards grow in scrolling content; reflection offers replay and another location | Every page, including long recorded outcomes |
| Responsive layouts | Gameplay canvas uses a minimum logical area; main-menu canvas now uses Expand; button height scales with text; scrollable menu content replaces fixed-height overflow | 16:9 and 4:3 captures at normal/maximum text |
| Input | Shared focus feedback; offscreen selection scrolls into view; gamepad pause/back and route guidance; XR pointer rejects clipped/disabled/hidden targets | Hardware-independent ray tests and real device acceptance |
| Accessibility | Text scale, subtitle background, reduced motion/flashes, labeled values and warnings, saved audio/comfort settings | All-lighting, full input and physical-headset acceptance |

## Changes made during this pass

- Shared modal actions and paired actions expand with text scale.
- Debrief/message cards no longer have a fixed 420-unit height ceiling.
- All shared modals expose scrolling; keyboard/controller focus brings the selected action into view and rebuilds retain matching action focus.
- Main-menu supporting pages have a masked, scrollable reading area, selected-action visibility, mouse/stick scrolling and clipped XR hit rejection.
- Main-menu text blocks measure their content; settings buttons expand with text size.
- Changing an On/Off setting retains focus on that control even when its label changes. A new screen starts at its first available action.
- Controls guide, credits and exit are reachable from the authored home through Settings.
- Route-card header/footer text areas scale; gamepad D-pad switches preview guidance.
- Gamepad B and Start provide shared-modal back/pause/resume behavior.
- Preparation instructions and phone labels honor text scale.
- Pause supports confirmed restart; final debrief offers replay and another-location actions.
- Location title boxes were enlarged after the first maximum-text capture identified three overflows. The recheck passed.
- Preparation guide uses a concise heading and a contrasting warning-level label. Completion notices restack item details immediately, including notices added after the main HUD update.
- Expanded `CoastalGameplayCapture` captures actual runtime UI in controlled states and records overflow/focus checks. Constructed review pages are not evidence of completing the route.

## Evidence

### Follow-up implementation — 20 October

- Location changes, restart and return to the main menu now use `StormglassSceneTransition`: asynchronous loading with a persistent chamfered status panel, input suspension and duplicate-request protection. Desktop uses a screen overlay; XR places the panel in world space. Scene-transition runtime and headset validation are pending.
- The item-information sizing is shared with an isolated editor check, `StormglassItemLayoutChecks.Run`, which uses the production font, content and sizing method without loading the environment. Its output is kept separate from in-game evidence under `Captures/UICompletion/ItemLayoutRecheck`.
- That isolated check passed at 100% and 130% text: the longest observed description has no overflow (panel heights 225.6 and 346.59 logical units). The 130% screenshot was visually inspected and shows the entire description. Eight pointer checks and the saved-experience assertions also passed in this follow-up. This closes the isolated clipping regression, while full in-game concurrent-panel acceptance remains open.
- Both updated validation projects compiled with zero errors (64 runtime warnings, 184 editor warnings). Unity's remote command timed out while the editor was reloading; the local validation queue is being used for follow-up checks.
- The new loading panel was rendered at maximum text size without overflow (`Captures/UICompletion/loading-maximum-text.png`). The first edit-mode cleanup assertion failed because that preview does not receive the player's automatic destruction lifecycle. The corrected check explicitly invokes the production cleanup and passed: text fits, simulation is suspended, and cleanup restores the prior time scale. Evidence: `Captures/UICompletion/loading-check.txt`; repeatable test: `Captures/UICompletion/loading-layout-check.cs.txt`. Full scene loading and automatic player lifecycle remain unverified.
- Verification is resource-limited: Windows reported 37–43 MB free physical RAM during the follow-up, with multi-minute Unity import/domain reloads. The user has been asked whether additional memory can be freed. Do not convert these pending tests into a 100% completion claim.

- Earlier verified screenshots: `Captures/CoastalSignal/Updated`, Baybayon and Riverside gameplay/pause at 1280 × 720, text scale 1.3.
- Current runtime and editor compilation: zero errors; existing compiler warnings remain. Logs: `Captures/UICompletion/runtime-build.log` and `editor-build.log`.
- Baybayon expanded capture: 163 images, zero reported text overflows, 63 passing checks, no failures. Resolutions: 1280 × 720, 1024 × 768 and 1920 × 1080; text scales 1.0 and 1.3. Includes top/bottom of long modals, all five debrief pages, preparation panels, settings categories, inventory, recovery, confirmation, saved-run selection, handover, HUD and actual route preview. Visual samples inspected include 4:3 route choices with narration/vitals, settings and the final debrief actions.
- Main-menu recheck: 33 images, zero reported text overflows; eight hardware-independent pointer checks passed. Includes location selection, accessibility, audio, controls, all onboarding pages, guide, credits and exit confirmation.
- Riverside reduced-detail expanded capture completed: 159 images and 65 passing checks. One overflow was found in the longest preparation item description at 1.3 text scale; its panel has been widened from 330 to 420 logical units and its maximum height increased from 340 to 600. The fix compiles, but its targeted recheck exhausted memory before producing screenshots. Six route-preview attempts could not activate the preview and remain untested. See `Captures/UICompletion/RIVERSIDE_RAIN/inspection.txt` and `finished.txt`.
- Shared modal checks exercised Resume, settings time pause, confirmation cancellation and scrolling the final selectable action into view. These are callback/layout checks, not native device usability tests.
- Riverside additionally passed setting-toggle focus retention and an isolated real manual save/load health restoration check. This does not establish full checkpoint restoration of every gameplay subsystem.
- Baybayon/main-menu transition again logged Crest and Meta XR cleanup diagnostics. These are recorded separately from UI results.
- The first expanded Riverside run exhausted memory while rendering its first preparation screenshot (texture allocation failure). It produced no acceptance captures. A reduced-detail retry uses only temporary runtime mip/shadow/MSAA overrides and explicitly releases each capture render target; it is not a default-graphics performance pass.
- The final preparation-only recheck also logged repeated `Could not allocate memory: System out of memory!` messages and failed import workers, despite the user freeing memory. Its dedicated Unity process was stopped after failure; no production scene was saved. Log: `Captures/UICompletion/riverside-prep-recheck.log`. Re-run `CoastalGameplayCapture.RiversidePreparationRecheck` with sufficient memory to verify the final item-description fix. Do not present the earlier clipped stress screenshot as a passing result.

## Acceptance boundaries

### Representative screenshots

- Baybayon route choice, narration and vitals at 1024 × 768 / 130% text: `Captures/UICompletion/BAYBAYON_RAIN/route-choice-1024x768-1.3.png`.
- Riverside gameplay at 1024 × 768 / 130% text: `Captures/UICompletion/RIVERSIDE_RAIN/HUD-1024x768-1.3.png` (reduced environment detail).
- Riverside preparation guide at 130% text: `Captures/UICompletion/RIVERSIDE_RAIN/prep-Preparation-guide-1.3.png`.
- Main-menu accessibility at 130% text: `Captures/UICompletion/MainMenu/menu-Settings-0-1.3.png`.
- Debrief replay and location actions: `Captures/UICompletion/BAYBAYON_RAIN/Complete-4-1280x720-1.3-bottom.png`.

No physical headset is connected. Desktop renders and injected pointer rays cannot establish headset legibility, seated/standing reach, device loss, real controller mappings, or comfort. Existing journey validation documents describe previous fixtures and must not be treated as new Baybayon/Riverside end-to-end UI acceptance. No new 100% claim is justified until the remaining acceptance checks have evidence.

### Remaining hands-on acceptance

- Complete a fresh run in each community using actual keyboard/mouse input: preparation and item handling, evacuation, physical route commitment, recovery/arrival and debrief.
- Activate and inspect Riverside's route-choice preview; the automated fixture could not reach that state. Recheck Riverside at default graphics quality on a machine with sufficient memory.
- Save, perform another action, then restore a checkpoint and compare inventory, objectives, decisions and position with the saved state.
- Exercise restart and return-to-menu confirmations, including cancellation, and confirm the selected location is retained.
- With the target headset connected: repeat menu navigation, object interaction, scrolling, recentering and settings with both hands; test seated and standing views and all supported comfort settings.
- Compare normal and maximum text during bright preparation, rain, warnings and narration. Record any obstruction or unreadable text; desktop fixture captures alone do not close this item.

### Palette calculation

Calculated sRGB luminance contrast for opaque theme colors: Ink/Surface 13.27:1, Ink/Subtle 9.80:1, Muted/Subtle 6.44:1, OnAccent/Accent 9.38:1, Warning/Surface 7.84:1, Danger/Surface 7.30:1. These calculations do not certify translucent subtitles against every environment or headset display.

## 20 October live acceptance follow-up

See [live acceptance test report](UI_ACCEPTANCE_TESTS_20261020.md) for per-item disposition and evidence. L01 and L02 passed; tracker is 13 checked / 27 open. Actual Continue loading reproduced the time-scale failure. Both locations passed 14 scoped assertions, but full journeys, complete input/save matrices, simulator and headset acceptance remain open. Play Mode stopped and original graphics settings were restored; see Captures/UIAcceptance/cleanup.txt.

