# STORMSIM — Purok Sibucao rain prototype

## Play

- Scene: `Assets/Scenes/SIBUCAO_RAIN.unity`. Open it and enter Play mode, then choose **Play**.
- Windows player: `Builds/SibucaoRain/STORMSIM.exe` (keep the complete build folder together).
- WASD moves, mouse looks, Shift walks faster, E interacts, Escape pauses or closes a decision. Focus loss pauses the game.
- Follow the northern residential streets and Felix Amante Drive to the school campus. The navigation hint follows the road and points back to missed encounters.
- Five starts are distributed over the northern 32 metres; consecutive runs cannot use the same start. Remaining route length is approximately 700–732 metres. Base walking speed is 4.1 m/s, before resource/companion effects.
- Pause → Save Game writes one local save. Main Menu → Continue resumes it. Save location is Unity's `Application.persistentDataPath/sibucao-rain-save.json`.

## Environment and destination

The level is a copy of the live `MAIN_GAME` environment. Its placed terrain, house meshes, civic buildings, roads, and props were reused. The source scene was not edited. New content is grouped under **SIBUCAO RAIN - standalone gameplay**.

Route source: existing mesh centre lines from `residential 784361376`, `residential 784361374`, and `Felix Amante Drive`, followed by a collision-checked approach through the existing campus opening. The final route is 732.13 m. The school trigger is centred at approximately `(0, -1.478, 657)`, beside the covered court. It avoids the closed green gate and classroom walls. The separate barangay hall is not the destination.

Existing survey cameras, the daylight override, and the sea-wave controller are disabled in this copy. The original coastal water/pond art remains; no flood simulation is active. Desktop XR automatic initialization is disabled in the Standalone XR settings; Android XR settings are retained. Desktop default resolution is 1440 × 900.

## Encounters

| Beat | Distance from northern route start | Interaction |
|---|---:|---|
| Storm Intensifying | Start | Automatic |
| Debris in the Road | 75 m | Inspect |
| Downed Power Line | 163 m | Inspect |
| Go-Bag Strap Breaks | 252 m | Pick Up |
| Neighbor Securing Their Roof | 357 m | Talk |
| Person Struggling Against the Wind | 454 m | Talk |
| Checkpoint Redirect | 556 m | Talk |
| Evacuee Injured by Blown Debris | 659 m | Talk |
| Arrival | School campus | Trigger |

Decision-specific art is deliberately lightweight: saved primitive prefabs with colliders, plus separate broad encounter trigger volumes. The player must face an unobstructed encounter within 11 m to receive its E prompt. Decision panels can be dismissed and reopened; each choice scores once.

Actions are resolved through neutral dialogue and outcome messages. Detours and repairs add their authored `timeCost` to the level clock; the prototype does not animate an entire detour or repair sequence. Injury can reduce movement speed, carrying loose supplies reduces speed, assisting a resident creates a following companion, and first aid consumes the single kit. Skipping encounters is allowed and recorded as not encountered in the debrief.

While assisting a resident, walking is capped at 2.6 m/s and sprint is disabled so the resident can keep up.

## Data and scoring

`Assets/StormSim/Sibucao/Data` contains nine DecisionNode assets and 21 separate DecisionChoice assets. Each choice exposes category, basePoints, baseSuccessChance, timeCost, roleModifiers, delayedConsequenceFlag, and future tier bounds. Tier filtering is deliberately inactive.

The previous scoring implementation and numerical bands were absent from the supplied project. The approved new framework uses editable prototype values: decision points currently range from −15 to +20, with additional separate Evacuation, Resource Management, and Teamwork credits. Failures deduct eight Decision-Making points by default and can queue an eight-point Evacuation consequence. The Resident role is the current role; role modifier arrays are available for extension.

Preparedness is always zero. Arrival awards 20 Evacuation points and bringing a companion awards 10 Teamwork points. Pending consequences are settled on arrival. Numerical scores, success odds, and option quality labels are never displayed during play. The Mission Complete screen alone exposes all five categories and a per-encounter response log. Its layout was newly composed with the approved reference styling because the supplied sheet contained no completion screen.

`StormSim.Sibucao.FloodSimulationManager` is a stub: `GetWaterTier()` is always `Tier1`; both depth overloads always return zero. No flood algorithm or water-height changes are present.

## Presentation

Roboto Regular/Bold are bundled with their Apache 2.0 license. UI uses the specified palette and 48/32/24/18/14 text sizes at a 1440 × 900 design resolution. It includes menu, dialogue/portrait, notifications, E prompts, checklist, tutorial, subtitles, storm alert, pause/save, four settings tabs, confirmation, error messaging, button states and preference controls. It is rendered as a desktop immediate-mode overlay, without VR UI dependencies.

World-space URP rain has terrain collision; the overcast sky is a dedicated shader/material. Wind and rain loops plus randomized thunder reuse existing project audio. Lightning briefly changes lighting only. The two first-person hands are low-poly skinned meshes with wrist/finger bones and interaction poses; no body/legs are rendered.

## Verification

- Unity compilation checked before assembly and after changes.
- `StormSim.Sibucao.Editor.SibucaoValidation.SceneAudit()` verifies the route at 0.5 m intervals (1,463 grounded clearance samples), five starts, 21 choices, hands, fonts, audio, and flood-stub contract.
- `GameplayAudit()` runs in Play mode: 30 varied starts, seven decisions, duplicate-score guards, kit gating, companion movement effect, JSON save-state roundtrip, arrival, pending-consequence settlement, and pause.
- The physical school trigger was exercised using the player CharacterController and fired Mission Complete. A full chosen-response run produced Preparedness 0, Evacuation 55, Decision-Making 138, Resource Management 30, Teamwork 65.
- Visual captures are in `Captures/Sibucao`. Native `ScreenCapture` was used to verify the complete UI because direct camera capture can omit overlays.
- Final Windows build succeeded with zero errors and six warnings. Launch `Builds/SibucaoRain/STORMSIM.exe`; keep its entire folder together when distributing. The standalone player was launched and its menu inspected; its startup log contains no runtime exceptions. Full end-to-end mouse/keyboard traversal in the standalone player has not been manually verified.

The source environment may emit Cesium editor connection-cancellation messages during domain reload. These are separate from prototype compilation/runtime errors.

An existing FastMesh demo script (`SceneViewText.cs`) also needed `UNITY_EDITOR` guards around its editor import and Scene View callbacks so it could be included in a Windows player build without referencing unavailable editor APIs. Its editor behaviour is unchanged.

## Driveable cars and tricycles

The prototype now enables 41 existing visible cars and tricycles throughout the copied environment. Look at a vehicle within 4.5 m and press E to enter; W/S accelerates forward/reverses, A/D steers, Space brakes, and E exits once stopped. Mouse look and Escape pause remain available. A vehicle cannot be exited into an occupied space. Get out to interact with decision encounters. Arrival also detects a driven vehicle entering the school trigger.

Vehicles use low-speed arcade movement with swept box collision checks and ground following: cars up to 9 m/s, tricycles 7 m/s, reverse 3 m/s. With an assisted companion, speed is limited to 2.5 m/s. Save Game stores vehicle positions, orientations and the occupied vehicle; restarting resets all vehicles. Vehicle art is restored on exit. Original exterior renderers are hidden for the driver and restored on exit. The original first-person cockpit upgrade is described below.

The empty `car 183 - ` object has no visible model and is excluded. `car 196 - ` originally intersected `Open sea-side concrete posts extension`; its position in this scene was shifted 2 m east and grounded to clear that obstruction. Other car/tricycle artwork and placement are retained. Jeepneys are outside this change.

`SibucaoVehicleValidation.Run()` passed in Play mode for all 41 vehicles (enter, acceleration, brake, exit, collider/art restoration), plus reverse/steering, JSON vehicle-state roundtrip and wall collision. Existing gameplay and 1,463-sample route checks also passed. `Tools/StormSim/Enable Existing Sibucao Vehicles` attaches the controller to supported placed models in this scene.

Vehicle-enabled Windows rebuild: succeeded on 2026-09-13 with zero errors and 202 warnings. See the Unity Console/build report for warning details. Full manual standalone driving traversal remains unverified; the vehicle checks above exercised the controller in Editor Play mode.

## Original first-person cockpits

Original `Car Cockpit.prefab` and `Tricycle Cockpit.prefab` now provide the occupied vehicle view on all 41 vehicles. The car has a visible dashboard, steering wheel, instrument needles, radio fascia, vents, door cards, seats, roof lining, pillars and bonnet. The tricycle has handlebars, brake levers, a speedometer, fuel tank and sidecar seating/canopy structure. Existing rigged hands attach to the steering controls and return to their walking parent on exit.

Mouse-look is limited to a seated range; C recenters the view. The car has animated wiper geometry and procedural rain-glass coverage (V toggles wipers). A rear-facing camera feeds a 256 x 128 mirror at 10 Hz. Engine sound is an original synthesized loop with RPM-dependent pitch, not a recorded engine. Camera sway responds subtly to acceleration and turning.

Driving now smooths steering input, uses speed-dependent steering limits and a wheelbase-based turning radius, limits lateral acceleration, coasts with drag, and brakes before changing direction. Cars/tricycles have different acceleration and turning limits. It remains a simplified kinematic controller: no physically solved suspension, wheel slip, damage, rollover or validated wet-tire dynamics. Interior art and hands are deliberately stylized and are not GTA-quality photoreal assets. Original exterior-only meshes are still hidden for the driver while the fitted cockpit is visible, then restored on exit.

Checks passed for all 41 vehicles, plus hand attachment/restoration, coasting without instantly stopping, and braking before reverse. Existing gameplay and 1,463-sample route checks passed. Final views: `Captures/Sibucao/car-interior-final.png` and `tricycle-interior-final.png`. Full manual standalone driving and performance benchmarking remain pending.


Cockpit Windows rebuild succeeded on 2026-09-13: zero errors, 231 warnings. All original source assets were retained; no external vehicle asset purchase was needed.
