# Connected evacuation experience

Initial implementation notes, superseded by [DuringTyphoonRedesign.md](DuringTyphoonRedesign.md) for current behavior and executed validation. The raised-passage confirmation, roof clearing, treatment claims, drainage magnitude, and test-status statements below describe the earlier pass, not the revised design.

## Player flow

The scene starts at its normal menu and preparation phase. Outdoor auto-start, skip preparation and the test clock are disabled in scene settings. Preparation retains its 8 kg allowance and essential supplies; radio, flashlight, gloves, first aid and whistle share three specialist-tool spaces. The same constraint applies to storm pickups.

The main route contains the family, flooded junction, blocked drainage passage and injured evacuee. One seeded optional incident (dark household, separated child or stalled tricycle) adds variation between main encounters. Existing physical bag repair and other legacy station mechanisms remain in the project.

Water readings are taken from dry footing and retained in the review. The advisory can instead be used to request confirmation of the raised passage. This counts as an intentional assisted response. Pole/hand movement, numerical readings and interaction sounds make assessment visible. Depth measurements do not certify floodwater as safe.

Drain pieces use grip, carry and deposit: hold the interaction control, move the piece toward the sack, and finish the deposit. Cancel returns the unfinished piece. After completed drain work, a local authored relief effect grows over 25 seconds, reduces visible and queried water depth by up to 0.32 m at the blockage, and tapers to zero over 24 m. This is a gameplay consequence layered onto the flood surface, not a recalibrated hydraulic solver or regional forecast.

First-aid equipment enables direct treatment. Otherwise the responder signal is available with a voice fallback. Equipped radio/whistle improves the established signaling approach. Escort completion now requires the evacuee's actual movement; running ahead cannot mark them delivered. Calling a responder receives equal teamwork completion credit.

The helped family keeps following the route, waits behind the player, retains its route position across saves, and is only recorded at school when its cart reaches reception. The route review distinguishes assistance from confirmed arrival.

Sheltered route boards are placed beside the route when space is available. Dry covered footing softens rain/wind audio, provides extra stamina recovery when still, and suspends ambient surprise interruptions. The storm clock continues. Gear-dependent radio updates and storm-stage notices explain changing conditions.

## Presentation and replay

Desktop and Stormglass use concise objectives and contextual prompts. Idle control paragraphs are removed. Survival panels appear when health/stamina requires attention. Hunger no longer increases during the short evacuation. Existing reduced-motion and reduced-flash settings remain in effect.

The saved experience timeline records observations, actions, outcomes and learning connections. Reviews show it before category totals. Replay actions include new preparation/loadout, stronger storm and a flooded-junction snapshot restoring equipment, physical state and weather time. A snapshot does not recursively embed itself. Older saves without this snapshot omit the junction retry.

## Verification and remaining acceptance work

Runtime source and editor source compile successfully against Unity 6000.5.7f1 using the project's actual compiler response files, with isolated outputs under tmp/experience-validation. The first editor compilation was also observed with zero errors.

`StormSim > Validation > Check evacuation experience` provides eight checks covering saved timeline, measured values, family/drain state, nonrecursive replay snapshots, specialist versus essential supplies, local drainage falloff and legacy run readability. The checks compile; execution and live visual verification were blocked by the Unity editor connection disconnecting and the Windows helper failing to initialize (HRESULT 0x80070020).

Acceptance still needs a complete desktop playthrough, VR comfort/interaction testing, shelter-placement inspection at each spawn, timing and difficulty tuning, and verification of the final family/reception path. New professional character art, recorded voice performances and hardware haptic tuning are not included in this code pass. Existing hand and participant animation systems are reused.
