# Preparedness, inventory and optional home activities

The scene uses the same HouseholdBackpack object before and during the storm. Sleeping stages it on a clear part of the dining table. Its packed objects become inventory entries without being replaced. After waking, collect the bag with E; I or Tab opens the storm inventory. E collects additional reachable supplies. Open the closet or chest with E to retrieve supplies left inside. The go-bag's 8 kg supply limit also applies during the storm. Select food and choose Eat selected to reduce hunger.

Preparation controls: F picks up a supply/tool, E uses the focused object, hold E to perform a tool task, B opens the carried bag, P packs, Q puts down the bag, G unpacks, Tab shows the checklist, Shift runs and Space jumps. VR uses grip for picking up and trigger for interactions; when the right hand carries the bag, use the left hand for the supply/tool.

## Ten optional activities

1. Clear both drain grates with the stiff house brush.
2. Sweep all four marked floor areas with the household broom.
3. Seal the document pouch and pack it in the go-bag.
4. Patch the small surface wall crack using the compound/scraper on the television cabinet.
5. Close and latch both window banks using their handles.
6. Fetch the folding ladder beside the west side of the house. Hold E at its placement point to secure it. Use the installed ladder to climb to its working platform, then hold E on the loose roof edge panel while holding the screwdriver. Use the ladder again to descend.
7. Disconnect the lamp in dry conditions and raise it and the keepsake onto the high shelf.
8. Bring both loose outdoor belongings to the indoor storage area.
9. Tighten the cabinet restraint with the screwdriver.
10. Use the waste-bag tool to bag the household refuse, open the indoor bin with an empty hand, bring the filled bag to it and use E, then close the lid. Tongs holding collected litter can also be emptied into the open bin.

The roof work is a bounded simulation interaction with a supported platform. The wall task represents a small surface crack; neither task models structural engineering strength.

## Scores and saved outcomes

At the storm transition the twenty readiness checks award 25 points each, individual household tasks award 5, confirmed neighbor contacts award 2 and each completed optional activity awards 40. Preparedness is capped at the existing 1,500-point category limit. The ledger is committed once. New supplies collected during the storm award 5 Resource Management points once per item, within that category's existing cap; repacking a carried supply does not award points again. The existing storm decision-tree scores and consequences remain active.

RunState.preparationActions stores completed and skipped activities, evidence, points, phase and aftermath keys. The preparation snapshot stores individual object conditions and positions. Existing PhysicalRunData.history, pending consequences and inventory loss/consumption flags retain during-storm outcomes. These are inputs for the future aftermath scene; an aftermath scene or quantitative building-damage model is not implemented by this change.

Health displays the existing storm consequence health. Running drains stamina and resting/walking recovers it. Hunger increases with active simulation time, faster when running; a food pack reduces hunger by 35 and is consumed once. Hunger is 0 when full and 100 when hungry. Temporary desktop and VR displays expose the same saved values for the future UI replacement.

## Validation

PreparednessContinuityChecks.Run exercises the same-object bag handoff, inventory contents, optional task gating, bin disposal, score idempotency, later pickups, food use and a serialized storm save/load round trip. Results are written to output/preparedness/continuity-checks.txt. Play mode is required. The preparation save file is preserved by the check.
