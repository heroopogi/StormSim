# StormSim UI screen inventory

Reviewed 20 October 2026 against the runtime builders and saved capture reports. This completes checklist D02's inventory requirement; it does not accept every screen's behavior.

Runtime references below are relative to `Assets/StormSim/Sibucao/Runtime/` unless otherwise stated. Capture names are relative to `Captures/UICompletion/`. A capture pass means the specified controlled state passed its recorded text-overflow checks, not that every button or gameplay path passed.

| Screen or state | Implementation reference | Existing acceptance result | Remaining checklist tasks |
| --- | --- | --- | --- |
| Authored main menu | `StormSimMainMenuScene.cs`, Home / authored-menu callbacks; `Assets/Scenes/STORMSIM_MainMenu.unity` | Home captures at 100%/130% passed reported overflow checks | D01, I05–I07 |
| Location selection | `StormSimMainMenuScene.cs`, Locations / SelectArea / Launch | Locations captures at both text scales passed | I01, I07 |
| Continue and save selection | `StormSimMainMenuScene.cs`, LatestSave / Launch; `StormglassUI.cs`, ResumeSelection; `SibucaoSessionSaves.cs`, LoadRun | ResumeSelection matrix captured; one isolated health restoration check passed | I01, F03 |
| First-time VR onboarding | `StormSimMainMenuScene.cs`, BuildOnboarding | All three page captures passed; hardware interaction untested | A03–A07 |
| Desktop how-to and controls | `StormSimMainMenuScene.cs`, HowTo / BuildDesktop; `StormglassUI.cs`, Controls | HowTo/Desktop captures passed | I05, A02 |
| Preparation introduction | `Assets/StormSim/Preparedness/Runtime/PreparationIntro.cs` | Expanded fixture ends the intro before preparation capture; not accepted by that fixture | F01, F02, Q01 |
| Preparation objectives | `StormglassPreparation.cs`, UpdatePreparationHud | Preparation UI captured; simultaneous final item-panel state still needs in-game recheck | L01, I08 |
| Physical preparation field guide | `Assets/StormSim/Preparedness/Runtime/PreparationChecklistProp.cs`; `StormglassUI.cs`, Key | The physical guide differs from the forced Preparation guide fixture page; physical interaction acceptance open | I05–I07, F01, F02 |
| Preparation guide summary | `StormglassUI.cs`, Preparation guide; `StormglassPreparation.cs`, BuildPreparationOverview | Revised heading/contrast captured in Riverside at 130% | D01, L05 |
| Preparation inventory and item selection | `StormglassUI.cs`, Preparation bag | Preparation-bag captures passed reported overflow checks | I07, I08, F03 |
| Preparation phone and advisory | `StormglassPhone.cs`, BuildPhone | Phone captures at both text scales passed | I07, F01, F02 |
| Departure confirmation | `StormglassUI.cs`, Departure | Departure captures passed; actual departure must be walked through | F01, F02 |
| Preparation conversation | `StormglassUI.cs`, BuildConversation | Not included in the expanded capture matrix | L04, I07, Q01 |
| Gameplay objectives and prompts | `StormglassUI.cs`, UpdateHud | HUD captures exist in both locations; Riverside uses reduced detail | L03–L06, I08 |
| Item details | `StormglassUI.cs`, FitItemInformation / PositionItemDetails | Corrected isolated 100%/130% checks passed; earlier Riverside stress capture clipped before this fix | L01, L04 |
| Vitals | `StormglassUI.cs`, UpdateHud | Controlled health/stamina states captured; not all live-value transitions verified | L05, I08 |
| Warnings and notifications | `StormglassUI.cs`, OfferNotice; `StormglassPreparation.cs`, UpdatePreparationHud | Controlled priority notice/preparation update states captured | L01, L04, L05, I08 |
| Subtitles and narration | `StormglassUI.cs`, UpdateHud / VRText | Desktop captures exist; source review found pronoun conversion risk in XR text | A02, L04, L05 |
| Route-choice cards | `StormglassRouteCards.cs`; `SibucaoRouteChoices.cs`, Previewing | Both locations activated in live testing; Riverside normal/max text with warning, narration and vitals visually passed at 1280 × 720 reduced detail | I06, F01, F02 |
| Decision dialog and feedback | `StormglassUI.cs`, Decision; `SibucaoSession.cs`, Choose | Decision dialog is not explicitly covered by the expanded modal capture matrix | I07, I08, F04, Q01 |
| Gameplay guide | `StormglassUI.cs`, Guide | Guide matrix captured | L07, I07 |
| Gameplay inventory | `StormglassUI.cs`, BuildBag | Bag matrix and scroll-bottom states captured | L07, I07, F03 |
| Pause | `StormglassUI.cs`, Paused | Captures and tested Resume callback pass | I04–I07 |
| Settings and accessibility | `StormglassUI.cs`, BuildSettings; `StormSimMainMenuScene.cs`, BuildSettings / BuildAccessibility / BuildAudio | Captures and selected-toggle focus test pass | A01, I04, I07 |
| VR controls and comfort | `StormSimMainMenuScene.cs`, BuildVR / BuildVRComfort / BuildVRControls | Desktop render of VR settings pages passed; no headset acceptance | A03–A07 |
| Recovery and assistance | `StormglassUI.cs`, Recovery | Recovery matrix captured | F01, F02, I07 |
| School reception and handover | `StormglassUI.cs`, ArrivalReview | Constructed handover states captured, not actual arrival completion | F01, F02, F04 |
| Results and debrief | `StormglassDebriefUI.cs`, outcome / timeline / decisions / people / reflection | All five pages captured, including scroll bottoms | F04, I07 |
| Restart and menu confirmation | `StormglassUI.cs`, Confirm; `SibucaoSession.cs`, ConfirmYes | Confirmation layout/cancel callback tested; actual restart transition open | I02, I03 |
| Exit confirmation | `StormSimMainMenuScene.cs`, ConfirmExit; `SibucaoSession.cs`, ConfirmYes | Main-menu confirmation captured | I03 |
| Async scene loading | `StormglassSceneTransition.cs`, ShowAndLoad | Maximum-text layout, suspension and explicit edit-mode cleanup checks pass | I01–I03 |
| Checkpoint restoration | `SibucaoSessionSaves.cs`; `SibucaoSession.cs`, ApplyLoadedRun; `StormglassUI.cs`, UpdateHud | Save-selection layout and isolated health restoration tested; full restore flow not accepted | F03, I01 |

## Findings that prevent blanket acceptance

1. **XR narration conversion:** `StormglassUI.VRText` replaces every standalone uppercase `I` with `B`; UpdateHud applies it to subtitles. Actual source dialogue includes “I need to put this down first.” The conversion must distinguish control labels from prose. Track under A02.
2. **Capture coverage gaps:** the expanded fixture does not establish acceptance for the preparation introduction, actual physical guide, conversation panel or Decision dialog. These belong in Q01 and their interaction checks.
3. **Loading time ownership failed runtime acceptance:** real menu → Baybayon Continue opens ResumeSelection but loading completion leaves time scale 1. Evidence: `Captures/UIAcceptance/transitions.txt`; I01 requires a fix and retest.
4. **VR simulator:** `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` disables automatic instantiation and has no assigned simulator prefab. Runtime enumeration found XR inactive and zero devices. Configuration and an actual run remain required under A03.

Supporting reports: [completion audit](UI_COMPLETION_AUDIT_20261019.md) and [progress checklist](UI_PROGRESS_CHECKLIST.md).
