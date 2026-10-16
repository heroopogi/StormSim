# Roadside surroundings, B1-B5

The station environment now includes solid storm-debris pockets: broken fence sections, fallen timber and drums, and folded corrugated roofing. These occupy open roadside space instead of introducing another interaction screen. Gaps between pockets let players retreat or pick their way around the wreckage. Existing authored side routes remain available; this does not add a new decision choice or award completion for walking around an encounter.

`SibucaoObstacleSurroundings` is attached to the five main stations. Generated objects are saved in the scene for editing/preview, and rebuilt when a run changes the station or route layout. Placement checks ground, existing solid scenery and all authored branch paths. Dense streets receive smaller clusters. Route clearance also reserves room for encounter pieces to be pulled aside. No invisible perimeter, trigger damage, new probability, inventory mutation or scoring rule is introduced. Debris is static and solid, not a new moving/damaging hazard.

The cost is physical navigation and existing elapsed time. The 60-second active encounter floor is unchanged. Detouring without finishing still follows the existing abandonment/missed-task rules. Existing score-cap concerns from the longer encounters remain open; no caps or time-cost data were adjusted here.

Validation: use `StormSim/Validation/Roadside surroundings B1-B5` to check presence, route clearance, solid colliders and materials. Use the existing physical-encounter validation for the 17 choice duration floors. Placement checks do not substitute for human traversal of every home/route combination.
