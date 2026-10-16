# Sibucao desktop mission guidance

Implemented in SIBUCAO_RAIN only.

- Persistent short current objective, contextual encounter/control hint and route bearing arrow.
- Physical household guide clipped inside each of the four homes, beside the front door. Look at it within 2.8m and press E. J opens the same guide anywhere after the wake-up sequence. E, J, Esc or the Close button dismiss it.
- Reading blocks looking, movement and grabbing, releases held objects, and leaves the storm running. No packing checklist or revealed decision scores.
- Route encounters now have a nearby spatial activation fallback (12m horizontal / 4m vertical), alongside existing physical sensors. Merely projecting beyond a route distance no longer activates and immediately scores an unseen event. Active observation events retain their observation window; other encounters can be left behind after 12 seconds and 20m separation.
- Added 103 missing exact mesh colliders to the neighborhood house detail batches, including door/frame geometry. Existing structural colliders retained; no bounding boxes across usable home entrances.

Validation in Unity Play mode:
- All four home back walls block a CharacterController approaching from outside (stopped ~0.4m from wall center).
- All four closed front doors block; all four open front doors allow exit.
- Physical paper targeted successfully with an unobstructed ray; guide opened, closed and reopened.
- Observation event stayed unresolved four seconds after activation even after passing the old +12m cutoff.
- Automated CharacterController movement along the school-approach route reached completion and displayed all eight applicable situation instructions. Broken-bag event correctly did not appear without a carried bag. This was an automated traversal, not manual validation of every possible choice.
- UI and physical paper screenshots reviewed. C# compilation clean.

Preview: Open Assets/Scenes/SIBUCAO_RAIN.unity, press Play, choose Play in the menu. After the bulletin/standing sequence, use WASD and mouse. Look beside the door and press E to read, or J for the guide. Follow the route arrow; nearby situations replace the general travel objective. Esc pauses normally when the guide is closed.
