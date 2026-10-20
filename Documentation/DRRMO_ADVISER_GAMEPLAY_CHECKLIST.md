# DRRMO and adviser gameplay improvement checklist

Scope: requested improvements to preparation, the go-bag, difficulty progression, and flood-safety learning in `SIBUCAO_RAIN`.

Use this checklist to track the adviser’s feedback alongside the flood-preparedness advice in the DRRMO leaflet. Checked items reflect features found in the current source; unchecked items are proposed changes or decisions still to make. The 16 October update has Unity compile errors, so source presence does not confirm that the update currently runs.

## Current preparation and role flow

- [x] Preparation includes a weather bulletin, household activities, go-bag packing, optional neighbor contact, and a departure or rest choice.
- [x] The current menu starts the player as Resident without offering a role choice; difficulty selection remains.
- [x] Players can choose Guided or Explore preparation; guidance can be changed later.
- [x] Player role selection is already bypassed in the current menu flow. The legacy role field remains for compatibility.
- [x] The update adds a one-day-before opening, voice/subtitle guidance support, a physical handbook with a progress pencil, and an item-information panel.
- [x] Replace the prescriptive “What to Prepare” list with a short current objective, the active warning, and contextual item guidance.
- [x] Optional preparation can be left unfinished, and an evacuation instruction takes priority over household tasks.

## Go-bag and item interactions

- [x] The desktop bag panel shows packed-item names, weight, readiness, bag capacity, and individual retrieval actions.
- [x] The departure review summarizes packed supplies and the household plan.
- [x] Retrieving an item restores it as a physical object in the player’s hand; the player can then drop it manually.
- [x] The storm inventory has separate “Take out” and “Drop selected” actions.
- [x] Add useful inventory grouping or sorting, such as communication, health, food and water, and documents.
- [x] Confirm that the new preparation item-information panel has a valid purpose description for every relevant supply.
- [x] Change the bag’s removal action to place the item directly in a safe, reachable place in the world, with no loss or duplication.
- [x] Show a brief pickup explanation and connect representative supplies to an in-game check or action where appropriate.

## Flood difficulty and task progression

- [x] The storm has five authored difficulty profiles, and floodwater rises over the scenario timeline.
- [x] Make water-height changes and their effects on routes clear at relevant mission stages.
- [x] Define how mission stage and selected difficulty affect the water profile and route availability.
- [x] Keep optional chores from causing sudden water increases; evacuation remains the priority as conditions worsen.
- [x] Keep flood depth and route changes tied to the authored game scenario. Do not present scenario values as calibrated local flood predictions.

## Apply the DRRMO flood guidance

- [x] Use the leaflet’s Monitoring, Alert, and Evacuate states to drive consistent radio, TV, and objective updates.
- [x] An evacuation instruction already overrides unfinished preparation and optional activities.
- [ ] Provide a safe route to higher ground when flooding blocks a route; do not reward walking or driving through floodwater.
- [ ] If an aftermath activity is included, gate return and inspection behind an all-clear and teach players to avoid and report hazards such as downed wires.
- [ ] Confirm local wording, warning stages, routes, and emergency information with DRRMO before presenting them as official guidance.

## Decisions to confirm before implementation

- [x] Confirm that “remove roles” means removing the player role-selection screen.
- [x] Confirm that “remove what to prepare” means removing the prescriptive checklist from the UI, while retaining preparation gameplay and contextual item explanations.
- [x] Flood height stages advance with the nine ordered required mission phases; optional side missions do not advance the required-stage index.

## Acceptance notes

- The bag clearly explains what the player packed, why relevant items matter, and how to retrieve or drop them.
- The opening flow no longer asks the player to choose a role.
- The preparation UI gives useful next steps without relying on the removed prescriptive list.
- Flood progression is visible, follows the selected difficulty, and supports evacuation decisions.
- The DRRMO lessons appear in player choices and consequences across preparation and flooding.

## Implementation update — 17 October 2026

- [x] Source inspection confirms the role chooser is bypassed and the player role is set to Resident.
- [x] New onboarding and presentation components are present: opening title, narration/subtitle fallback, physical handbook, and focused item information.
- [x] Bag summaries, item readiness, and individual retrieval are present.
- [x] The preparation guide now shows only the current objective, warning level, bag status, contextual item information, and optional help.
- [x] “Remove from bag” validates a clear nearby placement, updates the single item state, and safely restores it to the world.
- [x] Preparation bag contents are grouped and sorted by purpose category and item name.
- [x] All five difficulties define ten monotone water depths: departure plus each of the nine ordered required mission phases. Timeline rise remains as the storm envelope.
- [x] The duplicate `PrepWorkSave` definition and duplicate `Serializable` compiler error are removed.
- [x] `ItemPurpose` now covers all preparation kinds and every poster supply, while allowing authored per-item text to override the fallback.
- [x] Monitoring, Alert, and Evacuate wording is shared by preparation objectives, television/radio interactions, and route-condition guidance. Evacuate overrides unfinished work.

Runtime and editor C# assemblies compile with zero errors. A continuous first-time-player playthrough remains the final human acceptance step.

## Project references

- [Preparation improvement checklist](SIBUCAO_PREPARATION_IMPROVEMENT_CHECKLIST.md)
- [Preparation coaching notes](SIBUCAO_PREPARATION_COACHING.md)
- [Flood movement and difficulty notes](SIBUCAO_BATCH3_FLOOD_MOVEMENT.md)
- [Preparation integration notes](TORMSIM_SIBUCAO_INTEGRATION.md)
