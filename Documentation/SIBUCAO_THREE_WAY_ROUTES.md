# Three-way typhoon route

Implemented in `Assets/Scenes/SIBUCAO_RAIN.unity` on 28 September 2026.

Each of the nine existing encounters now has two different side missions, giving 27 encounters across nine decision points. Players can inspect alternatives and return before completing a route. All three route descriptions appear together at the shared approach; destination signs and spatial character calls reinforce the choice.

| Section | Existing center encounter | Left side mission | Right side mission |
|---|---|---|---|
| 1 | Rising water | Move crates, slide the bolt, open and secure the courtyard gate | Agree with the parent, release the stroller parking brake, carry a supply bag, accompany the parent and stroller |
| 2 | Stalled cars | Release the restraining strap, lower the loose shop shutter, fasten the storm bolt | Position a carrier, add food, coax the dog inside, latch and return the carrier |
| 3 | Canal | Carry three barriers around an exposed drain | Obtain permission, carry and turn the key, secure both passage doors |
| 4 | Fallen cable | Tune the radio, identify the landmark and transmit a report | Speak to a family, turn the diversion sign and accompany them |
| 5 | Damaged go-bag | Put medicine and documents in a waterproof box, close, latch and return it | Open the battery compartment, install batteries, switch on and hang the lantern |
| 6 | Wheelchair resident | Confirm a child's guardian by radio, accompany the child and volunteer, confirm handover | Ask how to assist a resident with low vision, walk with them, confirm handover |
| 7 | Rescue checkpoint | Distribute three life jackets, fasten their buckles, request a responder's fit check | Read the household list, confirm and mark three people, deliver the completed headcount |
| 8 | Collapsed evacuee | Release the retaining strap and deliver a folded stretcher to responders | Open the shelter gate, distribute three blankets and accompany the family |
| 9 | Creek boundary | Obtain the warden's direction, carry and align three inland diversion arrows | Obtain the caretaker's key, open the secondary school gate, move supplies and accompany evacuees |

## Player interaction

- Approach a junction to see LEFT / AHEAD / RIGHT descriptions without selecting a mission.
- Face an item or person within reach and press E to interact.
- For gates, brakes, switches and other moving parts: hold E and drag the mouse; release E to finish. Holding still does not perform the work, and reverse dragging reverses progress.
- For carried items: press E to pick up, walk to the stated destination, then press E to place. Q puts down the item or stops the current activity.
- Escorts require staying near the group along its passage.
- Complete the work and reach the passage's meeting point to progress to the next section.

## Implementation and scene changes

The scene contains 18 distinct side missions with 90 interaction steps. Four starting-home layouts provide 72 side routes. Routes are fitted around scene geometry; supported walkways are generated where the selected layout needs them. The original active neighborhood meshes, raised house floors and boundary walls are restored. Side passages are fitted around them without disabling houses. The stalled-car enclosure is lowered and opened at the ends for this three-way layout. Playable houses are excluded from frontage removal.

Side results use distinct values (100 left, 101 right) and causal path letters L/R. They do not pretend the center rescue was completed. Partial mission work is saved; ordinary center results remain supported. Flood depth is measured above the local ground. Side work areas and access decks rise together with the existing encounter platforms.

Editor menus: **StormSim > Route choices > Build all nine sections** and **Validate sections and save compatibility**. Rebuilding replaces the generated mission objects and refits the scene; use the scene backup before regenerating a separately art-directed layout.

Original scene and selected original runtime sources are preserved in `Backups/ThreeWayRoute_20260928/`.

## Presentation and validation scope

This is a playable prototype implementation. The new objects use simple constructed props and reused character models. Approach audio is temporary synthesized speech. Final character performances, detailed art and recorded voices remain polish work.

The structural check validates mission references, action types, layouts, ordered center/left/right progression, arrival and partial-save serialization. The Play Mode interaction fixture uses the player's interaction dispatcher, simulated input and short physical ticks, with teleports between focus/escort locations. It tests mission completion and duplicate protection, but is not a full continuous human walkthrough or a certification of real-world emergency procedures. Reports are stored beside this document.

Validation result: all 18 side missions passed the focused Play Mode interaction fixture (90 steps). All 36 approach checks passed across four starting homes, including preview without automatic center commitment and deliberate center availability. The new comparison panel was moved clear of the existing temporary test clock after screenshot review. These checks do not replace a continuous human playthrough of every route under each flood difficulty.

To repeat interactions: enter Play Mode, then choose **StormSim > Route choices > Run Play Mode interaction fixture**. The fixture temporarily substitutes in-memory run state, uses a dry-route threshold for the interaction checks, and restores the previous run afterward. It writes `SIBUCAO_THREE_WAY_INTERACTION_CHECKS.txt` in this folder.

Environment repair: 11 originally active neighborhood/floor/wall groups were restored from the original scene baseline. The builder no longer disables combined neighborhood meshes. Dry access and exit walks pass for all 72 side-route layouts. Raised access and exit walks pass for all 360 combinations of side route, home and difficulty. All 180 platform entrances and 180 exit connections also pass. See SIBUCAO_ROUTE_REPAIR.md for the flood model and repair verification.




