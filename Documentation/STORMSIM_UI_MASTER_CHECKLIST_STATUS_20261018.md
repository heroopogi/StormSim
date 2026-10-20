# StormSim UI master checklist status

**Current progress tracker:** [UI progress checklist](UI_PROGRESS_CHECKLIST.md) contains the active acceptance tasks and test log. Keep the historical findings below as reference.

**Update, 20 October:** See [UI completion audit](UI_COMPLETION_AUDIT_20261019.md) for the latest implementation changes and capture evidence. The historical status below predates the expanded Baybayon/Riverside checks; it is not a completion percentage.

**Reviewed:** 18 October 2026  
**Evidence used:** current StormSim source, the VR UI audit, the UI implementation record, and runtime/editor C# validation with zero errors.

`Finished` below means the behavior exists in the current source and compiles. It does not mean it has passed physical-headset or participant acceptance unless explicitly stated.

| Area | Finished in the current source | Partial or still open |
| --- | --- | --- |
| 1. Planning and scope | Screen/capture/source inventory; launch-to-debrief flow review; desktop/VR/shared review; Curio adaptation boundary; calm emergency-training tone; existing changes preserved | New after screenshots are still required |
| 2. Design system | Stormglass palette; background/surface/primary/warning/danger/success colors; Sora headings; Inter body; reusable cards/buttons; normal/hover/pressed/selected/disabled foundations | Controller glyph artwork, complete icon family, color-independent alert audit, headset typography acceptance, full token specification |
| 3. Main menu | Labels, hover/selection feedback, haptics/audio, stable world anchoring, Recenter, Start/Continue/Location/Settings/How to Play/Credits/Exit, exit confirmation, progress-loss wording, XR-safe prompt copy | Functional menu is card-based rather than the earlier orb layout; headset scale and both-hand acceptance remain open |
| 4. Location selection | Large cards; location names; generated/loaded preview images; descriptions; selected and unavailable states; remembered selection | Completed/locked states, lock requirements, duration/difficulty, explicit launch confirmation |
| 5. First-time onboarding | Seated/standing choice; dominant hand; recenter tutorial; one-topic-at-a-time three-page setup; movement/turning explanation; replay setup | Height calibration, controller glyphs, individual grab/carry practice, Skip, headset-fit/boundary reminders, live readability confirmation |
| 6. In-game HUD | Stable full panels; one recallable primary objective; contextual secondary details; conditional vitals; priority-based notices; low-priority timeout; critical breath override; reduced simultaneous content | Physical-headset central-cone and overlap acceptance |
| 7. Interaction prompts | Controller ray for UI; mixed `J / X` label removed; focus outline/scale; pressed state; unavailable reason for used first aid; hover/activation haptics; active-hand ray ownership; same-frame double-activation guard | Consistent direct interaction for every physical object, optional gaze mode, controller glyphs, distinct invalid-action cue, all-lighting visibility testing |
| 8. Preparation | Current-step card; physical supplies remain discoverable; contextual item information; short objective guidance; optional/required wording; completion feedback; bag/inventory world panel; leave-with-current-supplies feedback | Wrist/body bag redesign and physical-headset preparation acceptance |
| 9. Navigation | Current route/decision presentation and existing environmental guidance remain available | Minimap redesign, hide/summon control, north indicator, off-route recovery, marker/icon system, heavy-rain headset visibility |
| 10. Decisions | One decision screen at a time; large choice actions; short labels; distinct focus/activation; unavailable-state reason; immediate recorded outcome and consequence; physical actions remain tied to decisions | Headset pacing, irreversible-choice confirmation audit, optional deeper-explanation flow |
| 11. Guide/assistance | StormSim guide/radio-style help retained without a mascot; fixed help content; immediate close; instructions distinguished from optional information; preparation narration can replay | Short-page limit, hazard-coverage acceptance, complete reopen-position verification, generated-response loading state if generation is ever added |
| 12. Alerts | Warning/danger/success colors and labels; concise priority notices; effects-volume-aware UI audio; reduced-flash/reduced-motion settings; timed ordinary notices | Complete icon/pattern redundancy, urgent warning haptics/audio, alert-history review, headset obstruction testing |
| 13. Subtitles | Adjustable global text scale; adjustable subtitle background opacity; dynamic prompt placement above subtitles; independent master/speech/environment/effects volume | Enforced two-line length, speaker identity everywhere, complete timing/coverage audit, narration replay outside supported preparation cues |
| 14. Vitals | TMP/SDF Stormglass card; hidden when irrelevant; health/stamina/hunger labels and values; non-flashing low-breath warning; conditional presentation | Wrist/body mounting, icon redundancy, environment/headset visibility testing |
| 15. Pause/settings | Resume; settings/accessibility; load/recovery; return-to-menu confirmation; preservation/loss wording; consistent menu construction; Recenter and comfort controls available | End-to-end verification that every timer/physical action pauses in every phase; headset placement acceptance |
| 16. VR comfort | Snap/smooth turn; 15/30/45-degree snap; turn speed; vignette strength; head/controller-relative movement; dominant hand; seated/standing; Recenter; no forced tracked-camera animation | Height recalibration and safe tracking/controller-loss recovery |
| 17. Failure/recovery | Scenario-aware assistance screen; visible cause; non-shaming language; completed-work preservation; checkpoint/load support; return path; recovery consequences explained; guarded activation | Final action hierarchy and accidental-selection testing in headset |
| 18. Debrief | Outcome-first review; five pages for outcome, timeline, decisions, people/supplies, and reflection; page position; no leaderboard/stars; preparedness and learning measures; Return to Menu | Exact requested page ordering differs; narration/read-aloud, Replay and New Scenario actions, optional detailed-score expansion |
| 19. Accessibility | Multiple text sizes; strong dark/light contrast foundation; subtitle controls; audio levels; reduced motion/flashes; left-hand preference; toggle grip; plain language; optional work and evacuation timing guidance | Largest-size headset QA, complete non-color audit, optional gaze, pseudo-localization/expanded-text testing |
| 20. Technical implementation | Reusable UI builders; priority notice manager; modal/HUD state separation; separate objective/prompt/subtitle/notice elements; stable anchoring; Recenter; active hand; centralized UI sounds/haptics; TMP/SDF Stormglass UI; saved comfort/accessibility settings | Standard controller glyph resolver, removal of every legacy text fallback, font atlas/mipmap inspection, occlusion policy, participant-overlay audit, automated XR string test |
| 21. Validation | Runtime compile: 0 errors; editor compile: 0 errors; whitespace check passed; Unity completed successful script builds | Current distributable build, complete updated desktop capture/pass, XR Simulator, headset, performance, tracking loss, both-hand, seated/standing, maximum-text and five-participant testing |

## Fully implemented backlog IDs

- [x] VR-01 — stable world-space menus and Recenter.
- [x] VR-03 — core VR comfort settings.
- [x] VR-06 — reduced simultaneous HUD information.
- [x] VR-08 — controller scrolling and visible scroll state.
- [x] VR-17 — active-hand UI ownership.
- [x] VR-18 — configurable turning and locomotion vignette.
- [x] VR-23 — paged debrief and reflection.
- [x] VR-28 — replayable first-run VR setup.

## Highest-priority unfinished work

1. Tracking/controller-loss and height-recovery flow.
2. Controller glyph and icon asset system.
3. Updated interactive desktop and XR Simulator visual pass.
4. Current OpenXR/Meta candidate build and performance profile.
5. Physical-headset testing with at least five representative participants.
6. Approved after screenshots and thesis documentation.
