# School backpack inventory - SIBUCAO_RAIN

- Four carry baskets replaced by school backpacks with rounded compartments, front pocket, seams, zipper pulls, shoulder straps and carry handle. Reusable visual prefab: Assets/StormSim/Sibucao/Art/Expansion/SchoolBackpack_Visual.prefab.
- E equips the bag first. Without it, collecting supplies is refused and the world object remains available.
- With a bag, E immediately stores supplies, first aid, sticks and small loose items in a 27-slot inventory. No basket placement required.
- I or Tab opens the inventory. Click a slot, then Hold selected or Drop selected. Keys 1-9 equip the first nine slots; E returns a held item to the bag. Wheelchairs and residents remain physical assistance interactions.
- Inventory IDs, bag ownership and selected slot are saved with the run. Held inventory items return to their slots on load. Existing packed-item score flags prevent scoring duplicates.
- The bag-break encounter drops the actual backpack and its inventory contents. The bag cannot be equipped again during the active encounter until its strap is repaired. Use E on the strap for physical handling; repair, retrieve the bag and choose what to recover.
- Reading and inventory controls updated in the household paper. First-aid kits moved onto the dining table clear of the backpack.

Verified in Unity:
- E interaction dispatch equips each of the four backpacks, then collects all four supplies/kit objects in each home directly into slots with empty hands.
- No-bag refusal, duplicate prevention, selecting/equipping, switching held items, dropping and recollecting.
- Run reset, JSON save/load of inventory and save/load of spilled contents.
- Four stored items spill and can be recovered after repair resolution; no duplicate proxies enter the inventory.
- A kit equipped from inventory is consumed by the existing medical interaction and removed from its slot.
- Full inventory refuses a pickup without hiding the world item.
- Backpack and inventory UI visually checked; clean C# compilation.

Screenshots: Captures/school-backpack-in-home.png and Captures/school-bag-inventory.png.
