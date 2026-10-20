# StormSim VR UI and interaction audit

**Audit date:** 17 October 2026  
**Thesis scope:** UI/UX changes needed when converting the current desktop StormSim experience to VR  
**Current project:** Unity 6000.5.7f1, Meta XR/OpenXR packages present

## What was tested and reviewed

- Launched `output/DesktopCandidate/StormSim.exe` in a 1280x720 window with Direct3D 11. The package starts and creates a responsive `StormSim` window when forced to windowed D3D11.
- Reviewed the complete supplied capture set for the main menu, preparation HUD, guide, go-bag, role selection, comfort/settings, controls, encounters, storm navigation, arrival and debrief at 720p and 1080p, including 130% text.
- Reviewed the current VR UI and interaction implementation in `StormglassUI`, `StormglassXR`, `StormglassPointer`, `PreparednessPlayer`, `PhysicalGrabber`, `PreparationIntro` and `StormSurvivalVitals`.
- Checked the latest preparation validation record: 112 automated assertions, zero recorded runtime/editor compile errors, and a recorded status of `PASS`; the record explicitly says physical-headset and participant acceptance remain pending.
- The September desktop candidate is older than the October source and validation captures. It is useful as a diagnostic build, but it is not evidence that the current source is headset-ready.

This audit is therefore a strong desktop/capture/source review, not a substitute for a real headset usability and comfort test.

## Executive result

The desktop UI is visually consistent and readable on a monitor, and the project already contains a useful VR foundation: world-space UI, two controller rays, large menu targets, snap turning, controller-specific instruction replacement, subtitles, text scaling, reduced flashes, reduced motion and separate audio levels.

It is **not ready for thesis-level VR evaluation yet**. The highest-risk problems are the head-locked canvas, inconsistent interaction model, missing VR comfort settings, excessive reading/scrolling, lack of UI haptic/audio feedback, and absence of physical-headset validation.

## Priority backlog

| ID | Priority | Improvement needed | Evidence / current issue | VR-ready acceptance criterion |
| --- | --- | --- | --- | --- |
| VR-01 | P0 | Stop attaching menus to the head every frame | `StormglassUI.Anchor()` moves and rotates the canvas to the eye every `LateUpdate`. The panel follows every head movement and can cause discomfort. | Open a menu once in front of the player, keep it stable in world space, and only offer an explicit **Recenter UI** action. Small status indicators may be head-relative but should use smoothing and dead zones. |
| VR-02 | P0 | Use one consistent pointing/direct-touch model | Menus use controller rays, preparation often uses hand/controller transforms, but storm grabbing and several interactions raycast from the headset/camera. A grip press can act on what the player is looking at rather than what the controller points at. | The same visible controller/hand that highlights an object must operate it. Use near/direct interaction for reachable props and a controller ray for distant UI. Keep gaze only as an optional accessibility mode. |
| VR-03 | P0 | Add a complete VR comfort page | Current VR settings expose subtitles, prompts, flashes, reduced motion, text size and rain detail, but not turn angle/type, vignette, movement direction, dominant hand, seated mode or height calibration. | Before starting, the player can choose snap/smooth turn, snap angle, comfort vignette strength, head/hand-relative movement, dominant hand, seated/standing mode and recalibration. Settings are changeable while paused. |
| VR-04 | P0 | Validate with a physical headset | Existing records explicitly state that physical headset testing is pending. Desktop screenshots cannot verify stereo depth, vergence, controller aim, readable angular size, tracking origin or sickness. | Test on the target headset with at least five representative users. Record task success, mis-selections, reading distance, discomfort score, recovery use and completion time. |
| VR-05 | P0 | Build a current VR candidate | The available player is dated 30 September while relevant source and preparation reports are dated 14 October. The package also required forced windowed D3D11 to expose its desktop window. | Produce a clean current OpenXR/Meta build with the intended loader and target platform. It starts without command-line workarounds and its build ID matches the tested commit/source snapshot. |
| VR-06 | P1 | Reduce simultaneous HUD panels | Desktop captures show objective, route guidance, prompt, subtitle, notification, map and vitals competing for attention. Stormglass reduces this, but still permits objective, prompt, item info, subtitle, alert and vitals together across a wide head-relative canvas. | Normal play shows at most one primary instruction, one short prompt and essential safety state. Secondary information opens on demand. No important item lives outside a comfortable central viewing cone. |
| VR-07 | P1 | Convert long panels into short pages | Guide, bag, settings, arrival and debrief contain long paragraphs and scrolling lists. Reading large text blocks in a headset is tiring and hides the environment. | Each page communicates one decision or one topic, normally under 40 words. Use cards, icons and progressive disclosure. Provide visible page position such as `2 of 5`; never require repeated blind scrolling to find an action. |
| VR-08 | P1 | Make controller scrolling obvious and robust | Stormglass provides `Up` and `Down` buttons, but no thumbstick scroll, grab-scroll, page position or disabled end state. | Right/left stick scroll works while a panel is focused; Up/Down controls remain as an alternative. Show the scroll thumb/page indicator and disable controls at the top/bottom. |
| VR-09 | P1 | Strengthen focus, selection and disabled states | Focus is mainly a thin outline. Both controller rays can be active, and disabled options rely strongly on color. | Focus changes fill/brightness and adds a small scale or border change; press state is distinct; disabled state includes an icon/text reason. Only the active hand's ray is prominent. |
| VR-10 | P1 | Add UI haptics and spatial/non-spatial audio cues | Preparation actions have some haptic support, but Stormglass menu hover/click handling has no controller haptic or UI sound confirmation. | A subtle pulse occurs on focus change, a firmer pulse/click on activation, and a distinct error cue on disabled actions. All cues obey the effects-volume setting and can be disabled. |
| VR-11 | P1 | Replace text-only controller instructions with icons | Current instructions use prose such as `Left Menu`, `Y`, `X`, `B`, `A`, `left grip` and `right grip`. One preparation HUD string appends `J / X`, leaving a desktop key visible in VR. | Use device-aware controller glyphs and short verbs. No desktop key names appear while XR is active. Run an automated string scan for `WASD`, `Mouse`, `Esc`, `E`, `F`, `Q`, `P`, `I`, `J` and `R` in all XR-visible text. |
| VR-12 | P1 | Separate persistent HUD from full menus | The same 1280x1000 world canvas hosts both large menus and edge HUD elements, and the entire canvas follows the head. | Full menus are stable world panels; subtitles and safety warnings use a separate, gently stabilized presentation; vitals are wrist-mounted, body-anchored or glanceable rather than permanently face-locked. |
| VR-13 | P1 | Improve subtitle presentation | Desktop subtitles can span wide lower-screen boxes. In VR, long lines at the bottom require eye/head strain and may overlap interaction prompts. | Limit subtitles to roughly 32-42 characters per line and two lines, keep them near the lower center, include speaker identity, and allow size/background-opacity adjustment. Do not overlap prompts or warnings. |
| VR-14 | P1 | Redesign vitals for VR | The fallback VR vitals use legacy `UnityEngine.UI.Text`, while Stormglass places a compact always-visible card near the lower edge. Both are presentation fallbacks rather than a deliberate VR instrument. | Use an SDF/TMP wrist or chest display with large numerals, icon + color + text redundancy, and an urgent but non-flashing low-health/breath state. It is hidden when irrelevant and quick to summon. |
| VR-15 | P1 | Protect the center of vision during hazards | Storm/flood gameplay needs the center view for depth judgment. Large prompts or notices can obscure the path when the risk is highest. | Safety prompts sit below or beside the central 15-degree cone, never cover footing or a manipulated object, and collapse automatically after acknowledgement. Critical alerts use spatial audio/haptics as well as text. |
| VR-16 | P1 | Add a safe pause behavior | In preparation, the controller menu button can open settings rather than an explicit pause screen. The storm and preparation phases should behave consistently. | Menu pauses simulation timers and physical work, dims but does not replace the scene, and offers Resume, Recenter, Comfort, Save/Recovery and Return to Menu. |
| VR-17 | P1 | Prevent accidental double-hand input | Two rays and two trigger sources are processed. A same-frame guard reduces duplicate activation, but simultaneous hover and ownership remain visually ambiguous. | First deliberate input assigns the active UI hand until released or idle. The other ray fades. Switching hands is immediate and does not click or drag unexpectedly. |
| VR-18 | P1 | Avoid forced/animated camera motion | Snap turn is fixed at 30 degrees, and other desktop camera feedback is disabled in XR, which is good. There is no user control over snap angle and no comfort vignette during locomotion. | No script rotates/translates the headset camera. Offer 15/30/45-degree snap and optional smooth turn; apply configurable vignette during artificial motion; keep fades comfortable and never flash full white. |
| VR-19 | P2 | Use VR-quality text rendering everywhere | `PreparationIntro` and fallback vitals use `LegacyRuntime.ttf`, which can blur in a headset. | All XR-visible text uses TMP/SDF fonts with tested atlas resolution and mipmaps. Verify legibility at the final headset render scale, including 100%, 130% and the largest accessibility size. |
| VR-20 | P2 | Recheck physical scale and viewing angle | The XR canvas is approximately 1.73 m wide at 1.8 m, about 51 degrees across. The modal is tall and can require substantial vertical scanning. | Main reading content remains inside a comfortable central cone; body text has a headset-tested angular size; panels can be moved/recentered without resizing unpredictably. |
| VR-21 | P2 | Do not render all UI through geometry by default | The code sets GUI Z-test to `Always`, so UI can appear through walls/objects. This preserves legibility but weakens spatial coherence. | Full menus intentionally fade/dim the world. World markers obey occlusion or show an edge indicator. Only life-critical alerts may override occlusion, and the reason is visually clear. |
| VR-22 | P2 | Make alerts non-color-dependent | Orange/teal/green/red communicate state, but rain, dark environments and color-vision differences can reduce recognition. | Every alert and meter combines color with an icon, label, pattern or shape. Contrast passes the team's chosen accessibility target in both bright interior and dark storm scenes. |
| VR-23 | P2 | Simplify role, difficulty and debrief copy | These screens are readable on desktop but are dense for a headset. The debrief can become a long scroll of probabilities, scores and consequences. | Role and difficulty use one-sentence summaries and visual comparison cards. Debrief first shows 3-5 key outcomes, then optional details. Put thesis learning/reflection prompts on separate pages. |
| VR-24 | P2 | Provide clear recovery from lost tracking or wrong height | Current code requests floor tracking and clamps collider height, but exposes no UI for recenter, seated height, controller loss or boundary issues. | Detect missing tracking/controllers, pause safely, explain the problem without moving the player, and provide Recenter/Height/Resume actions. |
| VR-25 | P2 | Make destructive navigation harder to trigger | Return-to-menu confirmation exists, but save/load/recovery and exit wording should be explicit in VR where ray mis-clicks are possible. | High-impact actions have separated placement, descriptive confirmation and a safe default. The confirmation states what progress is preserved or lost. |
| VR-26 | P2 | Add localization-safe layouts | Current English text often approaches panel limits at 130% scale. Translation can expand strings further. | Test pseudo-localized text at 30-40% expansion. Buttons grow vertically, no text truncates, and controller glyphs remain paired with their labels. |
| VR-27 | P2 | Remove development UI from participant builds | Captures contain `Skip to DURING (Temporary)`, a temporary clock and Cesium attribution/upgrade overlays. These distract participants and can invalidate UI observations. | Thesis builds contain no temporary skip/clock/debug controls. Required third-party attribution is presented cleanly in Credits/About without covering gameplay. |
| VR-28 | P2 | Add a VR onboarding calibration scene | The current opening assumes the player understands rays, gaze, grip, trigger, both sticks and several face buttons. | A two-minute onboarding teaches recenter, point/select, grab/release, movement, snap turn, guide/bag and comfort menu. It can be repeated from Pause and skipped by returning users. |
| VR-29 | P2 | Instrument UI usability | Existing fixtures validate state and layout, but not human confusion, hover time, wrong-target activation or sickness. | Log anonymized local test metrics: screen opened, time to first valid action, mis-clicks, repeated opens, recovery use and abandoned step. Obtain participant consent and keep identifiers separate. |
| VR-30 | P2 | Optimize UI update work | `Anchor()` iterates through child graphics and changes render material state every frame. Large rebuilt text lists and dual rays can add avoidable headset cost. | Apply static material properties only when panels rebuild, pool common cards where practical, and profile CPU/GPU/frame timing on the target headset. Hold the selected target frame rate under rain, flood and debrief loads. |

## Screen-by-screen changes

### Main menu and opening

- Place the menu once in world space at a comfortable distance; add **Recenter** instead of continuous head following.
- Show `Start`, `Continue`, `Location`, `Comfort` and `Credits` as large cards with controller glyphs.
- Keep the title/opening fade dark and gradual. Replace the current legacy-font opening with TMP/SDF text.
- Ask for seated/standing mode, dominant hand and comfort preset before the first simulation.

### Preparation phase

- Replace the wide desktop tutorial card with one small current-step card plus optional expanded guide.
- Show a visible outline and controller-side icon on the focused physical object.
- Use direct/controller-point interaction consistently for picking up, checking, packing and placing items.
- Make the go-bag a wrist/body panel or a world panel that opens beside the physical bag.
- Replace `J / X` and other mixed desktop/VR strings with device-aware glyphs.
- Keep task-complete feedback short: chime + haptic + one-line confirmation.

### During-storm phase

- Put navigation direction in the environment where possible: road signs, beacons and spatial audio, not several overlay boxes.
- Keep vitals glanceable rather than permanently occupying the view.
- Do not show long text during movement, flooding, swimming or carrying.
- Use controller-point or direct hand interaction for rescue tools and residents; do not mix head gaze with hand grip.
- Add vignette strength, turn options and movement-direction settings.
- Use an unmistakable breath indicator with sound/haptics before it becomes critical.

### Pause, guide, bag and settings

- Use a stable world panel with a persistent Back/Close control in the same location.
- Support thumbstick scroll, visible scroll position and page count.
- Put VR comfort settings first; remove desktop-only key rebinding from XR views.
- Add Recenter, Height, Dominant Hand and Seated/Standing options.
- Preserve the user's place when temporarily closing and reopening a long panel.

### Arrival and debrief

- First page: outcome summary with three to five key results.
- Second page: residents and unresolved needs.
- Third page: supplies and preparation consequences.
- Fourth page: decisions and learning/reflection prompts.
- Use paging instead of a single long scroll. Read important results aloud when narration is enabled.

## Suggested implementation order

1. Fix canvas anchoring and unify controller/direct interaction (`VR-01`, `VR-02`).
2. Add VR pause/recenter and comfort settings (`VR-03`, `VR-16`, `VR-18`, `VR-24`).
3. Split menus, HUD, subtitles and vitals into separate VR presentations (`VR-06`, `VR-12` to `VR-15`).
4. Replace long text and mixed key labels; add icons, paging, focus, haptics and sound (`VR-07` to `VR-11`, `VR-19`).
5. Remove participant-build debug UI and produce a current VR build (`VR-05`, `VR-27`, `VR-30`).
6. Run physical-headset usability/comfort testing and revise from evidence (`VR-04`, `VR-28`, `VR-29`).

## Minimum VR acceptance checklist for the thesis

- [x] No menu or large panel is rigidly glued to head rotation. *(Source and compile verified: menus anchor once and expose Recenter.)*
- [ ] The highlighted object is always the object the active hand/controller will operate.
- [x] All desktop key labels disappear in XR mode. *(Source verified for Stormglass and preparation prompts; repeat during headset acceptance.)*
- [x] Recenter, seated/standing, dominant hand, snap angle and vignette are available. *(Source and compile verified in first-run setup, main-menu settings and paused settings.)*
- [ ] Text is readable at normal and maximum scale on the target headset.
- [x] Long result content is divided into five short pages, and scrollable panels show position plus disabled top/bottom controls. *(Source and compile verified; headset reading comfort remains part of the preceding item.)*
- [ ] Subtitles, prompts, alerts and vitals never overlap or block critical footing.
- [ ] Hover, select, success and error have distinct visual plus haptic/audio feedback.
- [ ] Controller loss and tracking-height problems pause safely and explain recovery.
- [ ] Target headset frame rate is maintained in preparation, heavy rain/flood and debrief scenes.
- [ ] A current build passes controller, save/recovery and full-route tests.
- [ ] At least five headset participants complete representative tasks; comfort and usability issues are recorded and resolved or documented as limitations.

### 18 October 2026 implementation status

The following backlog items are implemented in source and pass runtime/editor compilation, but remain subject to the unchecked headset acceptance criteria above:

- [x] **VR-01** — stable world-space menus with explicit recentering.
- [x] **VR-03** — snap/smooth turning, snap angle, vignette, movement direction, dominant hand and seated/standing settings.
- [x] **VR-06** — conditional vitals and priority-based notices reduce simultaneous HUD content.
- [x] **VR-08** — thumbstick scrolling, Up/Down alternatives, visible position and disabled end states.
- [x] **VR-17** — one active UI pointer owns selection; the other ray is hidden.
- [x] **VR-18** — configurable snap/smooth turning and locomotion vignette without scripted headset-camera animation.
- [x] **VR-23** — completion content is split into outcome, timeline, decisions, people/supplies and reflection pages.
- [x] **VR-28** — replayable three-step first-run VR setup for position, hand, movement comfort and point/select confirmation.

Partially implemented; do not mark accepted yet:

- [ ] **VR-02 / VR-09** — active-pointer ownership, focus scaling and clearer disabled reasons are implemented; every physical interaction still needs controller-to-highlight verification.
- [ ] **VR-07** — debrief and VR settings are paged, but every guide/bag panel has not completed headset reading-time acceptance.
- [ ] **VR-10** — hover and activation haptics/audio are implemented; distinct disabled/error feedback still needs completion.
- [ ] **VR-11** — mixed desktop labels were removed from XR prompts, but device-specific controller artwork/glyphs is not implemented.
- [ ] **VR-12 / VR-13 / VR-14 / VR-15** — stable menus, adjustable subtitle background, conditional TMP vitals, notice priority and dynamic prompt placement are implemented; headset overlap/central-cone acceptance remains pending.
- [ ] **VR-16** — pause/settings behavior exists, but complete timer and physical-work suspension needs a headset run.
- [ ] **VR-19 / VR-20** — Stormglass uses TMP/SDF and adjustable text scale; final angular-size and maximum-scale legibility require the target headset.
- [ ] **VR-21 / VR-22** — labels and shapes supplement several colors, but occlusion and contrast have not been accepted in all environments.
- [ ] **VR-25** — explicit return/exit wording exists; save/recovery mis-selection testing remains pending.
- [ ] **VR-30** — static material setup and single-pointer processing reduce UI work; target-device profiling remains pending.

Not completed by this implementation pass:

- [ ] **VR-04** — five-person physical-headset study.
- [ ] **VR-05** — current distributable OpenXR/Meta candidate build.
- [ ] **VR-24** — safe controller-loss, tracking-loss and wrong-height recovery screen.
- [ ] **VR-26** — pseudo-localization and 30–40% expansion testing.
- [ ] **VR-27** — final participant-build audit for all development overlays and required attribution placement.
- [ ] **VR-29** — consented local usability instrumentation and participant metrics.

## Thesis wording you can use

> The desktop interface provided adequate monitor-based readability, but VR conversion required more than transferring the same panels into world space. The audit identified head-locked presentation, inconsistent gaze-versus-controller targeting, long reading sequences, and incomplete locomotion comfort controls as the principal risks. The proposed VR interface therefore uses stable world-space menus, controller-consistent selection, progressive disclosure, multimodal feedback, configurable comfort options, and headset-based usability validation.

