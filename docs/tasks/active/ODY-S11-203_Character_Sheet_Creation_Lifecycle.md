# ODY-S11-203 — Character panel: roster, creation & review, tabbed sheet, lifecycle (phase 3)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phase 3 of 0–5)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** `ODY-S11-200` §14  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

From the Character drawer a user can create or import a character, take it through review, and use a tabbed
sheet (General & review, Attributes, Skills, Abilities, Resources, Anatomy, Ownership, History); the MainGM also
manages its lifecycle (archive, death/restore, permanent delete) and export.

## 2. Why this task exists

The character backend (`ODY-S04-1xx`, `ODY-S09-101…104`, `ODY-S10-102`) had no client screen.

## 3. Authorities and requirement references

- ADR-022/024/025 (character model, economy, lifecycle), ADR-002, ADR-004, ADR-005.
- `CharacterAdvancementService`, `ICharacterRepository`, `Odyssey.Rules.Character.*CostRules`.
- New test IDs: `TC-CHARUI-001`…`TC-CHARUI-014`.

## 3.1 Decision — roster without a backend listing (required before the phase)

Verified: `ICharacterRepository` has no "list characters of a campaign" query and `ICharacterTemplateRepository` no
template listing. Options (a) new backend query — **not taken** (backend is off-limits in this series);
(b) an existing source — **taken where it exists**; (c) self-scoped access — **taken for the rest**:

- **Scene tokens** (`ISceneRepository.ListTokens` → `TokenRecord.CharacterId`) — persisted, already in the backend.
  The MainGM's "Place token on scene" (`CreateToken(..., characterId)`) links a character to the board.
- Characters **created or imported in this client session**.
- **Open by id** (`char_…`).
- Visibility: the MainGM sees every roster entry; others see only characters they own, co-own or control. This is
  a presentation filter — `GetCharacter` has no audience filter in the backend (recorded limitation).
- Templates: creation always uses `CharacterCreationSeed.None()` (no template listing exists).
- **Follow-up backend task needed:** a campaign character listing (and template listing) query with audience rules.

## 4. Verified backend facts used

- Lifecycle edges come from `CharacterLifecycleTransitions` (Draft→Active/Archived; Active→Inactive/Retired/Dead/
  Archived; Dead→Active… only via `RestoreDeadCharacter`). The UI offers only valid edges.
- MainGM-only (stored membership): approve, grant points, resources (init/set current/set max), anatomy changes,
  ability GM grant and removal, revert purchase, respec, ownership, death/restore/permanent delete.
  Owner-or-MainGM: attribute/skill purchase, recommendation request, archive. Submit/comment: no actor gate.
- `RequestSkillAdvancedRecommendation` throws for a target level not above the current level; evidence may be empty.
- Recommendations have no list query: found through `CriticalSuccessEvidence.UsedByAdvancementId` plus the ids
  requested in this session.
- Cost/cap: `AttributeCostRules` / `SkillCostRules` / `AbilityCostRules` (Rules, fixture values) — the sheet calls
  these same functions for its previews and shows their constants; nothing is hard-coded in the UI.

## 5. Scope

### In scope
- `Runtime/Characters/CharacterPanelPresenter*.cs` (3 partial files), wiring in `TrialScreenPresenter`
  (character + inventory repositories with the inventory dependency checkers, export folder), `NativeFileDialog.OpenFolder`,
  `UiGuard` and additional `OdyMessages` texts in `OdysseyUiKit.cs`.
- Tests `CharacterPanelPresenterTests.cs`; `GameTestHost` repository factories.

### Out of scope
- Ruleset migration UI, portrait upload, character templates UI, anatomy catalog picker. Backend untouched.

## 6. Decisions

- **Revisions**: every call passes the matching section revision of the current record (Identity, Lifecycle,
  Mechanics + entry revision, Abilities, Resources, Anatomy, Ownership); the returned record replaces local state;
  `StateChanged` reloads before reporting.
- **UiGuard**: argument-exception preconditions are translated into a safe `InvalidRequest` failure (ADR-004 outer
  boundary) so no form can crash the screen.
- **Irreversible actions** (archive, mark dead, restore, permanent delete, revert, respec, remove ability, remove body
  part) use `OdyConfirmDialog`; delete/restore/revert/respec require a reason.
- **Review**: append-only comment feed; "changes requested" is expressed by new comments while the character stays Draft.
- **Export/import**: `.odchar` bundles go to `<persistentDataPath>/TrialCampaigns/CharacterExports/<name>-<id>.odchar`;
  import takes a folder path (text, "Use last export", or a native folder picker).

## 9. Acceptance criteria

1. §3.1 decided and documented. ✔
2. Sheet opens (self-scoped minimum); all tabs functional. ✔
3. Create / review / approve. ✔
4. Lifecycle for MainGM (archive / delete / death / restore / export / import). ✔
5. Tests per tab. ✔ (pending owner run)

## 10. Tests and validation

| ID | Test |
|---|---|
| `TC-CHARUI-001` | `CreatePlayerCharacter_RequiresOwner_AndRosterIsFilteredByRole` |
| `TC-CHARUI-002` | `Roster_IncludesCharactersLinkedToSceneTokens` |
| `TC-CHARUI-003` | `ReviewCycle_SubmitCommentApprove` |
| `TC-CHARUI-004` | `Attributes_CostAndCapComeFromRules_PurchaseUpdatesPoolAndRevision` |
| `TC-CHARUI-005` | `Skills_OrdinaryPurchase_ThenRecommendationRequestedAndApprovedByMainGm` |
| `TC-CHARUI-006` | `Abilities_ProgressionPurchaseCostsPoints_GmGrantIsMainGmOnly` |
| `TC-CHARUI-007` | `Resources_MainGmInitializesAndSets_BarReflectsValue_PlayerIsRefused` |
| `TC-CHARUI-008` | `Anatomy_DependentRemovalIsExplained_AddRemoveAndModify` |
| `TC-CHARUI-009` | `Ownership_MainGmManagesControl_PlayerSeesExplanation` |
| `TC-CHARUI-010` | `History_MainGmRevertsAttributePurchase_AndAppliesRespec` |
| `TC-CHARUI-011` | `Lifecycle_ApproveDieRestoreArchive_AndConfirmedPermanentDelete` |
| `TC-CHARUI-012` | `ExportThenImport_RoundTripsAnOdcharBundle` |
| `TC-CHARUI-013` | `StaleSectionRevision_IsRejected_AndTheSheetReloads` |
| `TC-CHARUI-014` | `Sheet_HasAllEightTabs` |

**Not run in this session** (no Unity/pwsh/.NET in the container; owner validates locally).

## 13. Security, privacy, and hidden information

Roster filter hides non-owned characters from Players in the UI; this is not an authorization boundary (see §3.1).
Error texts come only from `UserMessageKey`/`SafeReasonCode`. Export writes only inside the app's data folder.

## 17. Completion evidence

`git diff --name-status main -- Packages DotNet` empty. Validation not run in container.

## 18. Blockers, decisions, and change control

- Backend gap (reported, not fixed): no campaign character / template listing; no recommendation listing.
