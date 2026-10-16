# Sibucao environment dressing — inventory and available-asset pass

Inspected the active `Assets/Scenes/SIBUCAO_RAIN.unity` scene and imported prefab/model assets before writing the placement script. Scope is northern Sibucao from the existing prototype, not Baybayon. No house transforms or terrain geometry were changed. This is an available-asset yard pass, not completion of every requested art category.

## Applied

550 imported small props, seed 130926, distributed around house-perimeter zones in the northern 325 m of the route, within a 40 m band and north of world Z 975. The generator considers 198 existing house authoring transforms; visible replacement architecture is also checked through scene colliders. Candidates reserve 7 m around the route centre, 6 m around cars/tricycles, and 13 m around encounters. The middle of each facade is reserved as an access gap; this is a geometric heuristic, not semantic door detection.

Reused: `pot plant.prefab`, `Crate_2.prefab`, `Fish_Market_Bucket_With_Fish.prefab`, `SM_FIREWOOD_LAYING.prefab`, `cycle.prefab`, `chair (school).prefab`, and `LPW_Grass_A1_40cm_01.prefab`. The chair is a metal/school chair, not a plastic monobloc. Fish buckets are coastal household props, not empty water pails. Source materials are preserved; selected legacy materials get separate local URP Lit copies with available source textures/colors. No primitive replacement art was generated.

Select **Sibucao yard dressing - imported assets** in the hierarchy. Tune seed, props/grass per yard, route band/end, maximum objects and prefab references; click **Rebake this seed**. Baking replaces only its own `Baked yards` child. Placement runs in the Editor, not every frame. Small grass uses no colliders/shadows; solid props have box colliders, and added LOD groups cull distant detail. A frame-rate benchmark has not been performed.

## Existing inventory

| Requested category | Verified project assets / scene content | Suitability |
|---|---|---|
| Household props | `Assets/Organized Assets/Containers`: crates, buckets, barrels; Furniture: chairs, benches; Nature: pots and firewood | Several usable; some materials require URP repair. Generic barrels do not establish availability of blue plastic drums. |
| Bicycles / vehicles | `Vehicles/Bicycles/Prefabs/cycle.prefab`, `Low-Poly Bicycle # 5.prefab`; `Vehicles/Scooters/Prefabs/Scooty.prefab`; scene cars/tricycles | Bicycles reused; 41 driveable cars/tricycles retained. Scooter requires visual suitability review before reuse as a motorcycle. |
| Grass / rocks | Three LPW grass prefabs; `Rock_3`, `Rock_4`, LPW boulder/wall assets | Grass used. Rocks exist, but no dedicated pebble scatter kit was confirmed. |
| Laundry | Active `Courtyard laundry lines` scene assembly, 139 entries across the scene; `House 1340 balcony laundry` | Already placed scene meshes; no standalone imported laundry prefab found. Can be packaged for reuse after selecting an appropriate complete assembly. |
| Tropical plants | Active `Coconut palms`, `Banana yard clusters`, `Banana courtyard clusters`, satellite-canopy trees | Existing meshes and LOD assets; retained. Counts are scene-wide, not Sibucao-only. No papaya prefab confirmed. |
| Drainage | `Banago Neighborhood Life - reference additions/drainage`, 66 scene entries | Existing scene assemblies; retained. No hydraulic outlet/grade validation or new ditches performed. |
| Roofs / additions | Existing rust/corrugated materials, patched GI meshes, lean-to and raised-porch assemblies in other reference areas | Scene content exists; no reusable close-up roof-repair/curtain/foundation kit confirmed. House foundations and roof meshes unchanged in this pass. |
| Shops | Existing `store ... - House ...` assemblies and sari-sari signs; market stall prefabs | Existing shop fronts retained. Market stall assets are not automatically appropriate Philippine sari-sari replacements. No branded snack/signage pack confirmed. |
| Basketball | `School campus and covered court/School court painted surface and basketball equipment` | Existing scene equipment retained; no separate improvised hoop prefab confirmed. |
| Electrical infrastructure | Utility-pole prefab, `electric box.prefab`, scene lines | General infrastructure exists; household meters, pole transformers and tangled cable kits not confirmed. |
| Water station | `City Assets/Water Pumps/Prefabs/water pump.prefab` | Visual inspection showed a red fire hydrant. Rejected and removed; it is not a communal hand pump. |
| Waste | `dustbin`, `TrashCan`, `Urn`, `Urn_2` prefabs | Bins available; no appropriate bagged-waste collection set confirmed. No random litter added. |
| Doors/windows | 48 door and 18 window prefabs | Mostly generic/building-kit assets; not verified as compatible replacements for this architecture. |

## Missing or unsuitable: sourcing brief

These are search phrases, not endorsements of specific products. Request URP materials, realistic metre scale, low-poly/LOD options, and close-up texture previews.

- **Water/storage:** `Philippines hand water pump low poly`, `blue plastic 200 litre water drum`, `plastic pail wash basin water jug URP`.
- **Ground:** `URP worn dirt path decals`, `wet ground puddle decal URP`, `tropical fallen palm fronds`, `small pebble ground scatter`.
- **Building details:** `weathered corrugated metal GI roof modular`, `patched tarpaulin roof`, `informal settlement lean to`, `concrete foundation blocks stilts`, `door curtain boarded window props`.
- **Utilities:** `residential electric meter`, `pole mounted transformer`, `tangled overhead cable bundle modular`, `open concrete drainage channel modular`.
- **Daily life:** `plastic monobloc chair`, `fish drying rack woven mat`, `tire planter tin can planter`, `sari sari store props Philippines`, `snack sachet display`, `covered motorcycle`.
- **Animals/vegetation:** `low poly tropical papaya tree`, `URP dog chicken animated`, `tropical scrub bush low poly`.
- **Collection / recreation:** `bagged household waste collection props`, `improvised basketball hoop`.

Worn inter-house trails, new graded drainage, roof damage/tarp patches, changed foundation heights, curtain/boarded-window variation, tangled wiring/meters/transformers, hand-pump station, animals and additional curated shop/recreation sets remain pending appropriate assets or detailed assembly work. No placeholders were substituted for these categories.

## Verification

- Unity scripts compiled without errors.
- Two bakes with the same seed produced identical names, positions and rotations.
- All existing `House NNNN` transform matrices remained unchanged through rebaking.
- Existing 1,463-sample route/ground clearance audit passed after placement.
- Sample yard chair/grass placement visually checked. The misleading water-pump asset was rejected after visual inspection.

Windows rebuild succeeded: zero errors, 231 warnings; 2026-09-13. Full standalone traversal and frame-rate benchmarking remain unverified.
