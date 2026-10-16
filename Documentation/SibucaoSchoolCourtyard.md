# School courtyard dressing

Scene: `Assets/Scenes/SIBUCAO_RAIN.unity`.

The root `School quadrangle - gardens shelters and assembly plaza` adds an assembly
plaza, five paved approaches, a covered walkway, an open lunch shelter with tables,
a six-tap wash station, six shade trees with planters, six benches, four vegetable
beds, covered waste bins and a notice board. These are static world geometry, not
new interaction tasks. Existing buildings, terrain, refuge triggers and decision
systems are not replaced or modified.

Generated meshes and material live in `Assets/StormSim/Sibucao/Art/SchoolCourtyard`.
The editor-only `SibucaoSchoolCourtyardBuilder.Install()` is idempotent. Passing
`true` deliberately rebuilds only its named root and generated assets; do not use
that option after hand-adjusting this root unless those edits may be discarded.
Trees reuse the project's procedural broadleaf geometry. This courtyard dressing
is locally authored; the separate Higgsfield classroom models are unchanged.

Verification: Unity compilation error check; rainy Play-mode aerial and ground
screenshots; 355 capsule samples along the five new paths, checking for added
solid furniture/planter obstructions (none). This is not a full evacuation
playthrough or navigation certification. Open circulation and assembly space are
intentional. Paint strips have no collision and sit above paving to avoid flicker.
