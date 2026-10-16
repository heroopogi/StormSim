# Distinct obstacles immediately outside the northern house

The two repeated collapsed-roof obstacles in the reference image are supplemental encounters D33 and D34, at route distances of approximately 20 m and 50 m. They are separate from the later B1/B2 three-way junctions.

- **D33: fallen palm across the street.** A trunk, exposed root ball and fallen fronds replace the sheet metal and barrel. Space + forward climbs the trunk; release and press Space again for the later footing transfer. The original surrounding-lane route remains available.
- **D34: overturned supply cart.** An orange cart with visible wheels and two scattered sealed supplies replaces the second roof copy. Face the water bottle and food pack and press E to put them in the go-bag, then continue. The original surrounding-lane route remains available if the player chooses not to collect them.

Updated the encounter titles, instructions, branch labels and completion lines. The existing station IDs, home mask and bypass paths are retained. Backup: Backups/HomeExitUnique_20260928/.

The editor command **StormSim > Route choices > Make the northern home exit obstacles unique** reapplies this targeted change. It does not rebuild the 27 route tasks.

Validation is recorded in HOME_EXIT_UNIQUE_CHECKS.txt. The palm test drives the real CharacterController through the climb; the cart test checks collection gating, traversal and save state. Separate checks exercise focused E pickup and walk both existing side-lane bypasses. These are automated Play Mode fixtures, not a full manual playthrough.

During repeated automated walks and positioned camera captures, Unity logged one Graphics Ring Buffer exhaustion message. The scene saved cleanly and script compilation succeeded; a fresh Play Mode check follows separately.

The Graphics Ring Buffer error recurred during a fresh ordinary Play Mode session without positioned camera captures. This is an unresolved editor/rendering issue; all seven targeted gameplay checks passed. Final scene is saved, outside Play Mode, with no script compilation failure.
