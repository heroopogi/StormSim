# Main Menu review — screen 1 only

The Main Menu uses **world-space uGUI**. Its standalone prefab is:

`Assets/StormSim/Sibucao/UI/MainMenu/STORMSIM_MainMenu.prefab`

The supplied `stormsim_ui_kit.html` is retained as `Reference.html` beside the prefab. The four SVGs are extracted directly from that reference and retained beside their high-resolution transparent PNGs. Sora Bold and Inter Medium are included, with their SIL Open Font License files. The background, text, water accent, orb borders, icon strokes and hover colors come from the reference. The location subtitle is omitted to keep permanent text to the wordmark; action labels appear on hover or gaze.

## Editable main-menu scene

- Open `Assets/Scenes/STORMSIM_MainMenu.unity` to edit the menu. It contains the camera, XR Origin, tracked head pose, directional light, editable menu prefab, XR/mouse UI EventSystem, and runtime controller.
- Open `Assets/Scenes/SIBUCAO_RAIN.unity` and enter Play mode for the integrated menu. The center Play button starts a new run. Locations cycles the location name in its hover label. Settings opens the existing settings screen.
- On desktop, hover and click the circles. In a headset, gaze reveals labels and controller trigger/A activates the focused action. XR UI raycasters are attached so an existing XR UI-ray interactor can also drive the buttons.
- A working OpenXR/Meta loader must be enabled for the platform in Project Settings > XR Plug-in Management. The menu prefab does not change the project's platform/loader settings. This Unity installation currently lacks Android build support, so standalone Quest builds require that module; a compatible desktop XR loader/headset connection is a separate preview option.

The canvas is placed once, at **1.8 metres**, when the menu opens. It stays in world space rather than sticking to every head movement. The central action group is approximately 0.66 metres across (about 21 degrees); the decorative background is wider. This reading/interaction area can stay **flat**. Curvature is unnecessary for this menu. Check the physical scale and label legibility in the headset before approving the next screen.

The requested new HUD, Decision Prompt, Flood Alert, and Debrief designs are deliberately not implemented yet. Existing gameplay UI remains until each replacement is approved.

## Simulation changes in SIBUCAO_RAIN

- Six-second waking sequence with closed eyes, partial opening, another blink, and breathing motion.
- Sixty seconds of TV news, then a six-second sit-up/stand-up transition before movement unlocks. Head roll is not imposed when an XR device is active.
- Front door uses E on desktop, or the shared XR interaction button while gazing at it. Door movement remains reversible.
- Four furnished starting homes: northern courtyard, road-bend household, low-road family home, and school-approach household. New runs never select the immediately previous home. Home identity is retained in save data.
- Preparation windows are 180, 150, 210, and 120 seconds respectively, with different flood-rise timings and local water levels.
- Kitchenware, rice cookers, fans with rotating blades/cages, brooms with fibers, bag seams/straps, paper stacks, gutters/downpipes, masonry, roof fastenings, and meter fittings replace or supplement the earlier plain forms. The family frame now contains a modeled keepsake rather than a text label.
- Twenty-eight stylized adult residents have varied heights, builds, skin tones, everyday shirts/trousers/shorts, sandals, hairstyles and some backpacks. Their articulated procedural animation drives knees, elbows, foot lift, body motion and gaze. Behaviors include walking, running, initial panic followed by evacuation, and walking toward shelter. These are authored stylized models and procedural animation, not scanned people or motion-capture assets.

The existing flood implementation remains a timed local rising-water basin; these changes do not implement or validate the thesis Cellular Automata model. The desktop gameplay controller is also retained; the main-menu scene is XR-ready, but full gameplay locomotion and headset comfort need a separate headset validation pass.

## Package compatibility repair

Meta XR Core 205.0.0 was made a direct dependency and embedded under `Packages/com.meta.xr.sdk.core` to retain a small compatibility patch. The Performance Insight capture tool resolves Android editor settings through reflection when Android support exists. Its static ADB initialization no longer opens a missing-SDK modal on every desktop domain reload. Existing Android SDK lookup is retained for installations with Android support.

The user handled Meta's telemetry-consent dialog. No telemetry preference was chosen by the assistant.

## Validation completed

- Editor compilation: no C# compiler errors in the final console check.
- Four house interiors: checked for overlapping foreign collision geometry; none found.
- All four doors: interaction toggled open and closed; the character controller crossed the doorway in both directions.
- Opening: eyes and camera transitions exercised; movement unlocked with the camera restored to the standing 1.63 m local height.
- Random starts: twelve starts visited all four homes without consecutive repeats.
- Residents: all 28 updated their route progress; joint references and combined skinned renderers remained valid. Each scene resident now uses one articulated renderer with material submeshes, with the editable source parts retained but hidden.
- Main Menu: isolated scene confirmed three buttons, visible canvas, 1.8 m distance, and Play hover label. Captured `Captures/main-menu-final.png` and `Captures/residents-final.png` for review.

These are Editor checks, not headset performance benchmarks. Existing imported-city mesh warnings are outside these changes.

## Current development target: desktop
Mouse and keyboard are the default until the end-of-project VR conversion. The main menu and simulation use the standard Input System UI module with mouse point/click actions. Open `STORMSIM_MainMenu` to review or edit the menu and enter Play mode to launch the simulation. Use WASD to move, mouse to look, E to interact, and Escape to pause. Headset review is deferred.


## Player-led home preparation and neighbour warning

The home HUD no longer shows a packing checklist, item count, recommended item list, or a precise flood countdown. Supplies remain physical optional objects: looking at one shows the neutral `E: Take` prompt, taking it removes it from the world, and existing scoring stays silent. No items are required to open the door or leave. The fallback TV bulletin describes the local weather without listing what to pack.

At the end of the news (66 seconds including waking), the TV picture and glow switch off and playback pauses. A resident runs along the home's clear frontage to the doorstep, faces the player, and calls out: “The rain is getting worse! Floodwater is coming!” The player continues standing up and can move normally afterward; no dialogue menu or confirmation is involved. The resident leaves after the warning. All four homes have a warning actor using the existing Running resident prefab.

To add the recording: expand any `SIBUCAO Home` in the Hierarchy, select `Neighbour - post-bulletin warning`, then assign an AudioClip to **Warning Voice** on **Sibucao Neighbor Warning**. Repeat for the other homes. The voice uses a spatial AudioSource. Its warning duration expands to fit a longer clip; the temporary subtitle follows the existing subtitles setting. No synthesized speech was added.

Verified all four TV shutoffs, actor activation and warning states, all sampled frontage paths clear of body-height collisions, restart hiding, and pause preserving position/time. The neighbour's pose reuses the resident's procedural running animation. Screenshot: `Captures/neighbour-warning-review-1.png`.

This pass implements the home changes. The attached walkthrough's complete nine action-based encounters, new scoring model, role modifiers and CA simulation remain a separate overhaul; existing street dialogue encounters have not yet been replaced.

## Physical gameplay rebuild
The home-only milestone notes above are superseded by SIBUCAO_PHYSICAL_DECISIONS.md for gameplay. All nine physical encounter systems are now installed. Main Menu remains the only redesigned menu screen; development remains mouse/keyboard first.

