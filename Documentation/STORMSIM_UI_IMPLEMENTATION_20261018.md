# StormSim UI implementation record

**Started:** 18 October 2026  
**Reference direction:** Curio's Pages visual and interaction review, adapted to StormSim's calm emergency-training tone  
**Primary runtime:** `StormglassUI`, `StormSimMainMenuScene`, and `StormglassXR`

## Design position

StormSim uses Curio's Pages as a reference for cohesive theming, short contextual guidance, visible objectives, consequence feedback, and a structured end-of-level review. It does not copy Curio's parchment styling, mascot, quiz presentation, stars, or leaderboard. StormSim retains its dark coastal Stormglass language, emphasizes safety and legibility, and presents confirmed outcomes separately from assumptions.

## Implemented foundation

- Stable world-space full menus with explicit recentering.
- Active-hand controller ownership, visible ray/cursor, hover and activation haptics.
- Hover and activation audio for gameplay UI and the standalone main menu.
- Sora headings and Inter body copy through TMP/SDF assets.
- Shared primary, secondary, disabled, focus, pressed, warning, and meter presentations.
- Short current objective, contextual interaction prompt, subtitles, alerts, optional item information, and conditional vitals.
- Priority-based HUD notices so urgent breath warnings override ordinary notifications.
- Adjustable subtitle background opacity.
- Visible scroll position with disabled top/bottom controls.
- Controller-aware text replacement for XR-visible gameplay prompts.

## VR comfort and onboarding

- Snap turning at 15, 30, or 45 degrees.
- Optional smooth turning with adjustable speed.
- Head-relative or controller-relative movement.
- Left- or right-hand preference.
- Seated or standing play position.
- Toggle-grip option.
- Configurable peripheral comfort vignette during artificial locomotion.
- Reduced motion and reduced flashes.
- Three-step first-run VR setup: play position and hand, movement comfort, then interaction confirmation.
- VR settings split into short Movement & Comfort and Hands & Interactions pages.

## Completion and reflection

The previous long completion panel is replaced by five pages:

1. Outcome scores and arrival status.
2. Recorded evacuation timeline.
3. Completed physical decisions and recorded consequences.
4. Residents, unresolved situations, and supplies at handover.
5. Later consequences and reflection prompts.

The school reception panel now gives a short handover summary rather than displaying the full debrief before arrival is confirmed.

## Validation status

- Source whitespace check: passed.
- Runtime C# project validation: passed with 0 errors (66 existing warnings).
- Editor C# project validation: passed with 0 errors (184 existing warnings).
- Unity 6000.5.7f1 imported the project and completed three successful Tundra script builds. The headless editor did not exit after package initialization and was stopped after the log became idle; its earlier Meta XR Audio `UnityEditor.GUID` package errors are outside the StormSim sources and did not recur in the final script builds.
- Desktop interaction and updated layout capture: pending an interactive Unity session; the automated headless editor cannot provide a trustworthy visual acceptance result for world-space VR panels.
- XR simulator review: pending.
- Physical-headset review: required before thesis participant evaluation.

## Required acceptance tests

- No large menu follows every head movement.
- Active controller highlight and activation always refer to the same target.
- No desktop key names appear in XR-visible prompts.
- Every menu remains usable at maximum text size.
- Subtitles, prompts, alerts, and vitals do not overlap during representative hazards.
- Vignette appears only during artificial movement and respects the configured strength.
- Seated and standing players can read, point at, and activate every control.
- Completion pages preserve the distinction between completed actions, recorded consequences, and unresolved resident status.
- Physical-headset participants can complete preparation, a route decision, recovery, pause/settings, and debrief without assistance.



## Coastal Signal low-poly UI pass - 19 October 2026

Approved direction: adapt the presented Higgsfield concept while retaining the game's low-poly visual style. Reference reviewed: Curio's Pages, PDF viewer pages 265-274 (printed pages 248-257).

- Shared runtime panels and focus outlines now use a cached nine-sliced chamfer sprite, flat shading, navy surfaces, seafoam actions, and amber warnings. The original serialized rounded sprite is retained for asset compatibility.
- The standalone menu has a Prepare / Evacuate / Recover journey, a Start preparation action, and a procedurally drawn coastal backdrop with stepped color bands.
- Gameplay cards have slim signal edges; message cards use a distinct surface. The evacuation objective stays visible when guidance hints are enabled, yields to route choices, and expands warning details while the objective control is held. Preparation retains its existing single instruction card.
- Debrief headings emphasize the recorded arrival and reviewing decisions. Scores and recorded outcomes continue to come from the existing run.
- No new standalone practice mode or phase-unlock system was introduced. Existing onboarding, saves, location selection, input bindings, and gameplay transitions are reused.
- Runtime and editor C# validation passed with zero errors (64 runtime and 184 editor warnings on full validation). Unity completed successful script builds. The 1920 x 1080 main-menu capture was inspected: no text overflow. All eight existing pointer checks passed. Gameplay HUD overlap, maximum text scale, and physical-headset readability remain to be checked in play mode. The batch capture emitted edit-mode cursor cleanup warnings; it is a menu render, not an end-to-end playthrough.
- Batch preview entry point: StormSim.Editor.CoastalSignalPreview.Capture. Writes Captures/CoastalSignal/main-menu.png and validation.txt without saving scenes.


## Baybayon / Riverside gameplay readability follow-up

- Baybayon was opened in the interactive editor and captured in Play Mode at 1280 x 720 with text scale 1.3. The controlled stress fixture enabled subtitles and reduced vitals; it was not a completed player run.
- Initial evidence: Captures/CoastalSignal/BAYBAYON_RAIN/gameplay-large-text.png and pause-large-text.png. These are BEFORE the follow-up fixes. The inspection reported a truncated interaction prompt. The loaded runtime still used the old 70% HUD scale because editor compilation failed.
- Source fixes: remove extra HUD downscaling; enlarge prompt/subtitle height allowance; make vitals respect text scale; place item details below the actual alert height; scale route-card text and card height together; position subtitles/vitals above the resulting route panel height. Shared chamfered panels and flat colors are preserved.
- Latest runtime source validation: 0 errors, 64 warnings. Whitespace validation passed.
- NOT YET VERIFIED: corrected Baybayon visuals, Riverside gameplay, route-choice stress state, and physical headset readability. Do not present the initial captures as after-fix evidence.
- Unity reported CS1504 insufficient system resources while compiling editor sources, then a fatal native/Mono crash during recovery. The crashed editor was stopped and our pending validation request removed. No scene was saved by this pass.
- The fixture also logged DecisionTreeManager index errors because it entered Playing directly without a normal initialized run; next capture should start via the regular run/phase initialization before exercising UI states.

## Successful desktop capture retry - 19 October 2026

This supersedes the earlier blocked after-fix desktop capture status above.

- Both Baybayon and Riverside were run in Unity Play Mode using normal run initialization and the existing editor preparation-skip transition. Each was captured at 1280 x 720 with text scale 1.3, with controlled warning, subtitle, and reduced-vitals states.
- Updated evidence is under Captures/CoastalSignal/Updated/BAYBAYON_RAIN and RIVERSIDE_RAIN. Both gameplay and pause captures report zero TMP text overflows. Visual inspection of all four images found readable text and no overlapping panels in these states; the flat colors and chamfered low-poly panel style remain intact.
- Baybayon's image includes the transient flood restoration prompt. Riverside's capture waits for flood catch-up to finish and shows the awning interaction prompt. These are runtime camera/canvas renders, not design mockups or completed participant runs.
- The retry confirms the earlier shared HUD readability fixes. No additional production UI changes were needed based on these four images. CoastalGameplayCapture.cs provides repeatable editor capture entry points without saving scenes and directs run checkpoints into the capture folder.
- Riverside emitted a Crest OceanBuilder.CleanUp NullReferenceException and Meta MRUK cleanup messages during EditorApplication.Exit after captures completed. These are shutdown diagnostics; they are not evidence of a UI failure during the captured gameplay.
- Still unverified: route-choice stress layout, full route completion, XR simulator interactions, and physical-headset readability. Desktop screenshots do not establish VR acceptance.
