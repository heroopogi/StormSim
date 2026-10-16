# SIBUCAO_RAIN gameplay and low-poly presentation review

Inspected 29 September 2026. This is an improvement backlog, not a claim that every listed risk has been reproduced.

**Main finding:** prioritize legibility, consistent interactions, pacing, believable flood adaptation, and useful feedback. Higher polygon counts would not resolve the main problems.

**Evidence:** inspected the loaded Unity scene and current source; entered Play Mode; inspected menu, role/difficulty selection, preparation, and the first route junction; captured the junction at player eye level; advanced the scenario clock for a flood presentation check. Preparation was skipped through its existing testing control and the player was repositioned for inspection. This was not a continuous human playthrough of all branches. Earlier captures and documents were treated as historical evidence, not proof of current behavior.

**Evidence labels:** Observed = visible in this inspection; Confirmed = present in current code/scene data; Risk = needs a focused playtest or benchmark; Design = proposed improvement.

## First fixes

1. **Consolidate the HUD. [Observed / high]** The first junction simultaneously shows an objective, navigation/depth text, a three-way comparison panel, two floating mission labels, minimap, control hint, test clock, and vitals. Give the player one primary objective, one contextual action, and a compact navigation display. Expand the comparison only when needed. Keep the center of the street readable.

2. **Remove testing controls from participant/player builds. [Confirmed / high]** The saved scene enables both `showTemporaryClock` and `showTemporarySkip`. F8 can skip preparation or advance storm time. Gate these behind an explicit development configuration; do not rely on remembering to hide them before each build. Preserve intentional replay skipping as a separate designed feature.

3. **Unify preparation and evacuation controls. [Observed + Confirmed / high]** Preparation teaches F to grab, B to open the bag, P to pack, G to unpack, and Tab for a checklist. Evacuation uses E, I/Tab, mouse grip, R rotation, and E plus mouse dragging. Retain one vocabulary for interact, carry, stow, release, and cancel across both phases. Show only the action available on the focused object.

4. **Replace the long bottom control strip with progressive teaching. [Observed / high]** The preparation strip runs into the vitals and attribution area at the inspected viewport. Teach movement, one pickup, one packing action, and one mechanism in sequence. Keep the full reference in a readable help panel.

5. **Make the opening objective actionable immediately. [Observed / high]** Initial preparation displayed “New preparation run” and “0 / 100 contacts” while the TV introduction ran. Explain that the player is waiting for a bulletin, then point to the bag or first usable storage. Explicitly distinguish optional household warnings from requirements so the 100-contact counter does not imply that everyone must be visited.

6. **Make route progression and recovery explicit. [Confirmed + Risk / high]** Core encounters are ordered; `CheckEndDestination` refuses completion when `NextCoreStage < 9`, and ordinary bypass handling does not apply to core stations with the causal model enabled. Show the remaining stage and a clear return path. Provide an intentional, recorded withdrawal/skip where the design permits it. Test arriving at school after missing, abandoning, or becoming stuck in each stage. This review did not reproduce a final softlock.

7. **Rework automatic platform elevation. [Confirmed / high]** `SibucaoRouteSection.Update` starts raising side work areas once the flood limit reaches 0.08 m, aiming at a height based on the selected difficulty's eventual peak plus 0.5 m. Props, route markers, and decks move with it. This can make a neighborhood look like a moving obstacle course. Prefer permanent raised passages, clearly floating structures with visible support, or staged relocation by responders. Preserve access without unexplained moving buildings/mission areas.

8. **Decide what the flood model is meant to represent. [Confirmed / high]** The scene enables ground-relative inundation. `EnforceDepthLimit` fills every ordinary water cell up to the scenario limit and removes water above it. That largely overrides local pooling differences produced by rainfall and runoff. For gameplay, author distinct shallow routes, deeper pockets, visible current, and believable drainage. For thesis claims, document and validate the simplified model separately; the current presentation alone does not establish physical accuracy.

9. **Rebalance stamina for ordinary travel. [Confirmed / high]** Walking drains 2 points/second, running 5, and stationary rest restores 5. A full bar lasts about 50 seconds of ordinary movement before other costs; zero stamina also reduces movement speed. On a roughly 732 m base route plus side work, this encourages stop-and-rest cycles. Consider free or slowly recovering ordinary walking, with stamina spent on running, carrying, wading, and difficult assistance. Test the intended trip duration with new players.

10. **Explain outcomes in the completion screen. [Confirmed / high]** The active physical completion screen shows category totals and a total out of 5,000, but does not display the recorded decision history. Show what the player did, who reached safety, what supplies remained, what earlier choices changed, and one or two useful lessons. Keep numerical scores hidden during decisions if that is part of the assessment design.

11. **Fix score bar normalization. [Confirmed / high]** All five result bars divide by 1,000, while category limits are 1,500 Preparedness, 1,200 Evacuation, 1,000 Decision-Making, 1,000 Resource Management, and 300 Teamwork. A full Teamwork score therefore looks like 30%. Use the actual category maxima and label the denominator. Confirm that Decision-Making's additional 30% credit, including when it is itself the scoring category, is intentional.

## Interaction and mission quality

12. **Show manipulation direction and completion. [Confirmed + Design]** Side mechanisms interpret the dominant mouse axis and require near-100% progress followed by releasing E. Add a small directional cue and obvious physical end stop, with clear grip/release feedback. Avoid making players guess which direction is valid or whether the action registered.

13. **Improve placement affordances. [Confirmed + Design]** Carry delivery accepts an object within 0.8 m after movement. Show the intended socket/surface, a restrained placement preview, and a clear “place here” state. Use consistent placement tolerances appropriate to each object, with audible/visual confirmation and recoverable drops.

14. **Make assistance feel different from carrying a prop. [Confirmed + Design]** Prioritize convincing two-hand contact, wheelchair handles, body support, acceleration, and stopping behavior. The existing physical sequence should remain meaningful, but the player should perceive a person being assisted rather than a transform being dragged.

15. **Improve escort pacing and obstruction recovery. [Confirmed + Risk]** Side escorts move at 1.05 m/s toward waypoints and stop on a blocking linecast. Add smooth turns, spacing, “waiting for you”/“path blocked” feedback, and a recovery or alternate-path strategy. Test dropped objects, doors, crowds, and narrow corners; a permanent stall was not reproduced in this inspection.

16. **Vary mission rhythm. [Confirmed + Design]** Eighteen side missions contain many Talk/Check/Slide/Turn/Carry/Escort steps. Keep the variety of situations but avoid identical multi-step labor at every junction. Mix short observation, meaningful route choice, one substantial rescue, and quieter travel. Measure time spent deciding versus operating small parts.

17. **Communicate commitment and unfinished work. [Confirmed + Design]** The player may inspect alternatives, cancel work, and complete only the selected route outcome. Make it clear when a route is merely being previewed, when work has begun, and what completing it leaves unresolved elsewhere. Preserve the existing distinct left/right outcomes and reporting of unconfirmed rescues.

18. **Separate physical execution from random consequences in the debrief. [Confirmed + Design]** Center decisions can finish physically yet receive a seeded success/failure outcome modified by role, flood, weather, health, and stamina. Explain observable causes and uncertainty afterward. Audit losses/injuries so successful input does not feel arbitrarily invalidated. Do not expose a “correct answer” during the choice if that would compromise assessment.

19. **Make inventory changes specific. [Confirmed + Design]** Flood exposure can remove a carried item and currently says something slipped out. Show which item was lost, let the player inspect remaining supplies easily, and use consistent icons for stored, equipped, consumed, damaged, and dropped items. Test preparation-to-storm continuity for each state.

20. **Give survival meters a clear purpose. [Confirmed + Risk]** Hunger increases and food reduces it, but the inspected movement/consequence code does not use hunger to change capability. No explicit zero-health incapacitation or drowning/oxygen state was found in the inspected survival/swimming controllers. Decide whether these are active mechanics, assessment records, or unnecessary HUD elements; implement clear consequences and recovery consistent with that decision.

21. **Add checkpoint recovery and robust save behavior. [Confirmed + Risk]** Evacuation saving is exposed through the pause menu; no automatic `SaveRun` caller was found. Add checkpoint autosave, a recoverable backup, clear resume information, and protection against corrupt/incompatible saves. Verify mid-carry, mid-mechanism, escort, flood catch-up, and arrival reporting. Do not erase the existing manual save silently.

22. **Explain role and difficulty choices. [Observed + Confirmed]** Role selection offers three names without describing their effect. Difficulty mainly displays maximum water depth. Describe the practical experience and role responsibilities without revealing scoring advantages. Check whether the normal preparation handoff intentionally forces the first home, since other start layouts exist.

## Low-poly art, navigation, and sound

23. **Establish one visual style. [Observed + Design]** Detailed brick/concrete textures, household props, human models, blocky hands, and simple mission geometry have visibly different treatments. Define a shared palette, roughness range, texture detail level, proportions, and edge treatment. Low-poly geometry can remain; consistency matters more than extra triangles.

24. **Improve lighting separation. [Observed + Design]** At the junction, residents, props, and walls sit in similar grey/brown values. Keep storm atmosphere while separating foreground interactables, walkable ground, and background buildings. Use restrained warm lights or color accents at shelter and task locations, plus modest contact shading.

25. **Reduce route-sign scale and duplication. [Observed + Design]** Large boards dominate the approach and repeat the HUD's labels; floating side labels repeat them again. Use smaller roadside signs, recognizable destinations, consistent symbols, and one active marker. Ensure signs are readable from the intended approach and do not clip above the player's view.

26. **Make hazards readable in the world. [Design]** Distinguish depth/current, live cable areas, unstable debris, gaps, and dry footing through silhouettes, movement, waterlines, sound, and environmental cues. Do not make the numerical depth readout or a minimap the only way to understand danger. Validate readability in fog and on lower display brightness.

27. **Integrate mission decks and set pieces into the street. [Observed + Design]** Repeated boards/ramps and staged actors can read as separate stations placed on the road. Add appropriate supports, surface transitions, drainage connections, and local environmental context while maintaining tested collision clearance. Give major junctions distinct landmarks.

28. **Improve first-person hands. [Observed + Design]** Current hands have a very boxy silhouette and a generic forward pose. Refine proportions and silhouette within a small polygon budget; add purpose-specific reach, grip, push, release, and bracing poses. Check tool contact and clipping before adding detail.

29. **Fill the missing character reactions. [Confirmed]** All 69 active `SibucaoActor` components inspected lacked assigned alert and player-reaction clips. Add a small coherent set for looking toward the player, calling, bracing against wind, pointing, and acknowledging assistance. Also finish appropriate seated/injured/assisted poses. Prioritize contextual acting over a large animation library.

30. **Polish locomotion and crowd placement. [Confirmed + Risk]** Preserve existing walk/run variation and LODs, but test foot contact, sliding, turning, overlap, blocked lanes, and behavior in rising water. Small deliberate groups and believable destinations will communicate urgency better than uniformly distributed actors.

31. **Complete contextual audio. [Confirmed]** Scene slots for screams, crowd urgency, tier changes, and distant crowd are unassigned; route documentation identifies synthesized approach voices as temporary. Add suitable localized performances, directional calls, mechanism/placement sounds, and wet/dry movement feedback. This inspection did not conduct an audible mix review.

32. **Improve the sound mix controls. [Confirmed + Design]** Settings expose one shared audio-volume control. Separate speech, environment, and effects; duck rain/wind when a nearby resident speaks; retain speaker-labelled subtitles. Avoid repeated calls competing with narration.

33. **Strengthen the shelter arrival payoff. [Confirmed + Design]** The current flow can show an arrival desk and then scores. Add a brief visible reception/handover, a clear sense of shelter, acknowledgements from accompanied residents, and a short transition before the debrief. Let the player's earlier actions be visible in the arrival state.

34. **Reconcile vehicles with the evacuation route. [Confirmed + Risk]** Forty-one enabled vehicle controllers were present. Test whether driving bypasses ordered encounters, traps the player near gates, or creates inconsistent flood/companion behavior. Clearly communicate where vehicles are usable and why travel on foot becomes necessary; do not add vehicle complexity unless it serves the intended learning loop.

35. **Provide accessibility and comfort options. [Confirmed + Design]** Add text/UI scaling, remapping, invert-look and FOV controls, alternatives to sustained grip/drag, separate speech volume, and reduced flash/camera motion. Use icon/shape cues alongside color. Current settings mainly cover subtitle/hint toggles, brightness, rain density, fullscreen, master volume, and mouse sensitivity. Update the incomplete Controls page.

36. **Resolve attribution and safe-area layout. [Observed + Design]** Cesium attribution overlaps or crowds the lower gameplay UI in inspected views. Reserve a legible safe area and preserve required attribution. Review all overlays at 16:9, 16:10, and smaller desktop windows; do not assume a single 1440×900 design canvas handles every aspect ratio.

## Performance and validation before release

37. **Profile the actual player build. [Observed + Risk]** The inspected active scene contained about 14,743 renderers, 8,435 colliders, 3,952 LODGroups, and 69 actors. These counts are not draw calls or a measured frame rate. Measure CPU/GPU frame time, memory, culling, shadows, overdraw, and physics on the target machine. Preserve useful existing LODs; optimize the measured bottleneck.

38. **Budget flood simulation and loading. [Observed + Confirmed]** One Editor sample reported approximately 16.8 ms for a simulation step and 22.7 ms peak; these are isolated Editor measurements, not a player benchmark. Catch-up can run 24 steps in one frame and swimming holds movement while the saved water field catches up. Profile this path, reduce repeated full-grid work where appropriate, and provide clear loading feedback for legitimate restores. Clock-jump inspection exaggerated catch-up workload.

39. **Reduce avoidable runtime work after profiling. [Confirmed + Risk]** Actors use `AlwaysAnimate`; several focus/carry systems allocate with `RaycastAll`/`SphereCastAll`, and many independent behaviors poll every frame. Consider distance-based update rates, cached component references, non-allocating queries, and culling for inactive mission sections. Verify interaction and animation correctness after any optimization.

40. **Clean up desktop startup and rendering warnings. [Observed + Risk]** Entering Play Mode incurred a roughly 120-second domain reload in this session; logs included duplicate Roslyn assemblies and XR initialization failures, and the editor displayed a Crest opaque-downsampling warning. Diagnose these separately from gameplay performance. A tool connection interruption during reload is not proof of a standalone game crash.

41. **Run continuous acceptance playtests. [Confirmed gap]** Existing route fixtures use simulated input and teleportation and explicitly do not replace continuous play. Test all 27 center/side encounters, all reachable start layouts, all five difficulties, each role, late departures, dropped tools, blocked escorts, cancel/retry, save/resume, and arrival. Include first-time players and observe where they ask for help. Record completion time and idle time, not just PASS/FAIL.

42. **Update the current design and evidence record. [Confirmed]** Several documents describe obsolete flood stubs, old controls, and earlier encounter layouts, and some linked reports are absent from this checkout. Maintain one current gameplay guide, control map, known-issues list, build identifier, and validation matrix. Ensure thesis claims match the shipped version and distinguish scripted regression checks from participant evaluation.

## Suggested order

First address items 1–11, especially controls, mandatory-stage recovery, flood presentation, stamina, and results. Then complete mission feedback and a single consistent art/audio pass. Profile before extensive asset changes. Finish with continuous player-build playtests and update the documentation from the verified build.

Keep the low-poly direction. The most valuable art work is improved silhouettes, lighting, purposeful animation, and consistent materials.

## Primary evidence references

- Current first-junction capture: `Captures/GameplayAudit_20260929/03-first-junction.png`.
- Flood clock-jump capture: `Captures/GameplayAudit_20260929/04-flood-junction.png`; inspect alongside the catch-up limitation above. `05-flood-settled.png` was captured after the solver caught up (scenario clock about 540.5 s; simulated time about 540.4 s). The first section's side-area lift was about 1.46 m. These smaller-window captures also demonstrate why UI scaling needs attention.
- `Assets/StormSim/Sibucao/Runtime/SibucaoUI.cs:425`: physical results; line 430: score bars; line 517: settings.
- `Assets/StormSim/Sibucao/Runtime/SibucaoDecisionEvaluation.cs:13`: category limits and secondary credit.
- `Assets/StormSim/Preparedness/Runtime/StormSurvivalVitals.cs:11`: stamina; line 46: hunger.
- `Assets/StormSim/Sibucao/Runtime/SibucaoRouteSection.cs:43`: flood lift activation; line 62: target height.
- `Assets/StormSim/Sibucao/Runtime/FloodSimulationManager.cs:94`: catch-up; line 129: depth enforcement.
- `Assets/StormSim/Sibucao/Runtime/SibucaoSideMission.cs:105`: placement; line 176: manipulation completion; line 189: escort movement.
- `Assets/StormSim/Sibucao/Runtime/DecisionTreeManager.cs:184`: destination and ordered progression.
- `Assets/StormSim/Sibucao/Runtime/SibucaoSession.cs:224`: save/load entry points.
- `Assets/StormSim/Sibucao/Runtime/SibucaoActor.cs:14`: reaction clips; line 42: animation culling.
- `Assets/StormSim/Preparedness/Runtime/PreparednessSession.cs:46`: warning count; line 77: preparation instruction.
- `Assets/Scenes/SIBUCAO_RAIN.unity`: enabled testing controls, ground-relative inundation, and missing audio/animation assignments.
- `Documentation/SIBUCAO_THREE_WAY_ROUTES.md`: mission coverage and explicit testing limitations.

No gameplay scripts or scene assets were edited for this review.
