# Sibucao minimap, scene guide and E interaction

Scope: SIBUCAO_RAIN, desktop keyboard/mouse.

Controls:
- E picks up the aimed movable item; a second E puts it down. The original held-mouse grip remains available.
- E still opens/closes the aimed door and reads the household paper. J reopens the guide.
- The pickup prompt identifies a usable item; walls and fixed furniture are not pickups.
- The minimap is north-up, uses a baked map of the actual streets/buildings, tracks player facing, and highlights the path to the current task. Amber diamond = target; pale blue line = route.
- The household paper opens three pages: complete mission sequence, all nine route situations, and controls. It explains how to interact without revealing scores or prescribing which supplies to bring.

Collision repair:
The previous missing-collider patch covered the main house detail batches only. Additional patchwork wall sheets, reference house facades and porches needed collision. CharacterController tests also reproduced one-sided traversal through thin wall meshes. Reverse-face collision meshes preserve the actual openings while blocking the other approach direction; the four playable home doorways retain their existing colliders.

Editor setup utility: SibucaoNavigationBuilder.RepairBuildings and BakeMap. Generated collision assets and the street map are under Assets/StormSim/Sibucao/Navigation. Original visual meshes are retained.

Validation completed:
- 42 wall samples across seven neighborhoods blocked CharacterController movement from both directions; the reverse-side traversal reproduced before the fix no longer occurred.
- All four closed home doors blocked movement, and all four open doors permitted exit.
- Sampling the authored route every 5m found no house collision overlapping a player-sized capsule.
- The player's E interaction dispatch picked up and released all 20 household grabbables, both outdoor sticks, the wheelchair and the evacuee assist point. Outdoor checks also verified toggle-hold stays latched. These were automated action-dispatch checks, not a physical keyboard playthrough.
- All three guide pages and minimap overlays visually reviewed at game-view scale. Paper targeting confirmed; synthetic keyboard events were suppressed while the Editor was paused/unfocused, so guide rendering was opened directly for screenshots.
- Compilation and runtime error checks for Sibucao scripts clean.

Screenshots: Captures/sibucao-minimap-review.png, sibucao-full-guide-mission.png, sibucao-full-guide-route.png, sibucao-full-guide-controls.png.
