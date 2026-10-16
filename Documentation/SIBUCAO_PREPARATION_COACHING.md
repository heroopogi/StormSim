# SIBUCAO_RAIN preparation choices

Implemented 2026-10-14 as a follow-up to preparation Batch 1.

## Player experience

Starting a new SIBUCAO_RAIN preparation run presents two choices before the household introduction continues:

- **Guided preparation** (click or press **1**): a hands-on tutorial follows the existing storage, pickup, checking, and packing actions. The objective suggests the next action. The guide displays completed tutorial steps and explains why checking and packing matter.
- **Explore on your own** (click or press **2**): a brief preparation objective and a single contextual action hint. The player chooses what to do next.

Preparation clocks and actions pause while the choice is open. Both choices have the same supplies, readiness checks, consequences, and evacuation advisories.

Press **J** to open the guide and use **Coaching: ON/OFF** to switch. **Esc > Settings > Comfort > Preparation coaching** controls the same preference. Switching preserves items, preparation time, readiness, and tutorial progress. Interaction prompts retain their separate accessibility setting. Opening the guide in Explore mode does not turn coaching on.

The existing v2 preparation save now includes `coachingMode` (1 guided, 2 explore). Restoring a save restores coaching. Older saves without this field retain the current hints preference. New runs present the choice again.

For tracked controllers, the opening prompt offers left primary for Guided and right primary for Explore. Hold left primary to view the guide; right secondary switches coaching while the guide is open. Headset usability still needs a physical-device check.

## Validation

Run **StormSim > Validation > Preparation Coaching** with SIBUCAO_RAIN open in Edit Mode. The fixture runs dedicated coaching checks, the existing Batch 1 regressions, and live packing/departure checks. It captures the choice, Explore HUD, and coaching guide at 1280×720 with 130% text, plus the existing 1080p views. It restores settings and exits Play Mode.

Reports: `Reports/PreparationCoaching_20261014/`.

After the user refreshed Unity on 14 October, the validation completed with **60 passing assertions**: 17 coaching checks, 31 preparation regressions, 9 live presentation/handoff checks, and 3 cancellation checks. The fixture restored its settings/save backup, removed the queued request, and requested exit from Play Mode.

The choice screen and guide captures were visually inspected at 1280×720 with 130% text and at 1920×1080. Both choices and the coaching switch are readable; the larger guide scrolls to its remaining content. The Explore capture exposed a wrapping HUD reminder; its text was shortened afterward to `I/Tab Bag · J Guide` / `Esc Settings`. This text-only correction was compiled, but the capture predates it. Existing development-only skip controls are visible in these Editor captures.

Final source compilation: runtime and editor assemblies build with zero errors (58 runtime warnings and 178 editor warnings, primarily existing deprecated Unity APIs). No automatic test request remains queued. The automated input checks invoke production actions; they do not represent a continuous physical-keyboard playthrough.

These are scripted regression checks, not evidence that a first-time player can finish without coaching. The participant acceptance item in the Batch 1 checklist remains open.
