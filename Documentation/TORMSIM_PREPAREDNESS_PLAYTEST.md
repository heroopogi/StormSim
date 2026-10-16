Current game entry point: Assets/Scenes/SIBUCAO_RAIN.unity. See TORMSIM_SIBUCAO_INTEGRATION.md. The instructions below describe the earlier standalone prototype.

# Gather, rest, then pack

Open Assets/StormSim/Preparedness/TORMSIM_Preparedness.unity and press Play.

The preparation scene uses the original home's furniture layout, dining table and standing marker beside the daybed. The display sideboard and added supply table were removed. A closed household closet and lidded chest contain mixed supplies; other items sit at irregular angles on ordinary household surfaces. The dining table starts empty. The arrangement is authored with seeded variation, not reshuffled during play or loading.

After the TV bulletin, search storage with E, use F to carry individual supplies, then place them in a clear spot on the dining table with E. Placement retains your chosen orientation and position within the table bounds; it does not sort objects into rows. You can also drop an object with F onto the table and let it settle. Objects overlap neither by automatic arrangement nor by automatic stacking: choose another spot if one is occupied.

Recheck the TV, free your hand and use the bed to rest. The next morning is playable in the same home. The table retains your placements; uncollected objects remain in storage. Retrieve the bag yourself, open it, then pick up and pack each gathered item. Supplies are never packed automatically. You can return for forgotten items. Recheck the TV before departing. Completion of every task is not required. An evacuation instruction overrides the normal morning sequence so departure is not delayed for gathering.

Desktop: WASD/mouse move and look; F grab/drop; E use/place; G unpack; Tab checklist; F5 save; F9 load; Ctrl+R restart; Esc release mouse. Devices still need batteries/testing/charging. Water belongs in household storage rather than the go-bag.

XR controls: left stick movement, right stick snap turn, right grip hold/release, right trigger use, right secondary button unpack, left primary button checklist. Headset reach, mappings, comfort and hand poses remain unverified.

Save version 2 preserves table placements, storage access, gathered-item flags, bag retrieval and morning phase. It uses TORMSIM_Preparedness_v2.json in Unity's persistent data folder. Earlier prototype saves are separate.

The existing typhoon scene and its scripts remain unchanged. Departure still ends at the saved handoff; storm consequences and aftermath gameplay remain pending. The TV bulletin is fictional, charging is time-compressed, and the food/medicine checks remain simplified. See TORMSIM_PREPAREDNESS_DESIGN.md for source references and limitations.

