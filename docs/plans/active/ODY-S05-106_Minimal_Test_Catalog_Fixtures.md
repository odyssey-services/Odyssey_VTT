# ODY-S05-106 — Minimal Test Catalog Fixtures

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-106-minimal-test-catalog-fixtures`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/111
**Planning mode:** Brief plan (no new public contract, no new persistence shape, no new architecture -- pure integration proof over `ODY-S05-101`-`105`)
**Last updated:** 2026-09-04 UTC

## 1. Purpose and user-visible outcome

Prove that `ODY-S05-101`-`105` work together end-to-end: a minimal fixture graph (one Item, Weapon, Ammo, Armor, Ability, Effect) is authored through `ContentCatalogAuthoringService`, encoded through `TypedDefinitionCodec`, validated through `CatalogValidationService`, and published/archived/deleted through `ContentCatalogLifecycleService` -- closing out the Content Catalog MVP block. Not a final balanced content pack.

## 2. Task contract

- Goal: a real, SQLite-backed integration test suite proving the full Foundation/Authoring/Validation/Publish/Archive/Delete pipeline works together over a genuinely cross-referenced fixture graph.
- Acceptance criteria: full fixture graph publishes end-to-end; weapon/ammo applicability positive and negative cases pass; exact-version references survive round-tripping; Published/Archived fixtures remain loadable/listable; unused Draft fixture deletable, Published/Archived not; a broken fixture fails validation/publish safely; no runtime item/inventory/equipment/effect type/table introduced; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-106`, `ADR-027` §20 (exit criterion 6).
- In scope: `MinimalTestCatalogFixtureTests.cs` (new, 12 tests), `test-catalog.json` entries, `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/6 status update, this task's own contract/plan.
- Out of scope: final balanced content, marketplace/economy, `.odcontent`, Unity/UI, any runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect, new validation rules, new lifecycle semantics, campaign-specific overrides, new persistence tables, new ErrorCodes, any `ADR-001`-`027` content change.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §6 (`ODY-S05-106` boundary paragraph), `ADR-027` §4/9/12/20, `11_Content_Block_System`, `TypedDefinitions.cs`/`TypedDefinitionCodec.cs`/`ContentCatalogAuthoringContracts.cs`/`CatalogValidationContracts.cs`/`ContentCatalogLifecycleContracts.cs` (all re-confirmed unchanged full reads).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main`, which already includes PR #110 (`ODY-S05-103`, merged) atop PR #108/#109 (`ODY-S05-104`)/#107 (`ODY-S05-105`)/#106 (`ODY-S05-102`)/#105 (`ODY-S05-101`).
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 still read `In Review` with a stale `TC-CATALOG-078`-`097` test range (two amendments after PR #110 was merged extended it to `078`-`099`) -- corrected as this task's own first step.
- `ContentCatalogAuthoringService`/`CatalogValidationService`/`ContentCatalogLifecycleService`/`TypedDefinitionCodec`/`TypedDefinitions.cs` all re-confirmed unchanged from this session's own prior full reads of each -- every piece needed for the fixture already exists and is already individually tested; this task only wires them together.
- Confirmed `AbilityDefinition`/`EffectDefinition` carry no typed `ContentDefinitionRef` field of their own -- only the generic `DependencyRefs` envelope field can reference anything from them, directly shaping the fixture's own Ability-to-Effect wiring.

Assumptions: none.

## 4. Proposed approach

- Test file (`MinimalTestCatalogFixtureTests.cs`, real SQLite fixture mirroring every sibling `SLICE-05` persistence test's own convention): private helper methods build each of the six typed shapes via `TypedDefinitionCodec.EncodeX`, author them as Drafts via `ContentCatalogAuthoringService.CreateDraftDefinition` (`actorIsMainGm: true`, `RulesetCompatibility=["ruleset.core@1.0.0"]`), and publish them via `ContentCatalogLifecycleService.PublishDefinition`.
- Fixture graph: Effect ("Bleeding") published first; Ability ("Field Dressing") references it via generic `DependencyRefs`; Item ("Medkit") references it via typed `BuiltInEffectRefs`; Ammo ("9mm Rounds", `CompatibilityKeys=["9mm"]`); Weapon ("Service Pistol", `AmmoRequirement.Required`, `CompatibleAmmoKeys=["9mm"]`); Armor ("Light Vest").
- 12 tests cover: full-graph end-to-end publish; weapon/ammo applicability (matching ammo present/missing/ruleset-incompatible); exact-version reference survival through a codec round trip; Published-fixture loadability; Archived-fixture list separation; unused-Draft physical delete; Published/Archived physical-delete rejection; a broken fixture (missing referenced Effect) failing both validation and publish safely; a reflection scan and a schema scan against runtime item/inventory/equipment/effect state.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 corrected to `Done` with the accurate test range; row 6 (`ODY-S05-106`) marked `In Review` with the PR link once opened.

No production code change, no Unity/UI code, no new persistence table, no new `ErrorCode`, no `ADR-001`-`027` content change.

## 5. Milestones

### M1 — Fixture and tests

- [x] `MinimalTestCatalogFixtureTests.cs` (12 cases) -- one build fix needed (missing `using Odyssey.Domain.Character;` for `BodyPartId`), then all 12 passed on the next run.
- [x] `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-100`-`111`.

### M2 — Validation and review readiness

- [x] `dotnet build DotNet\Odyssey.Core.sln` (full solution).
- [x] `dotnet test DotNet\Odyssey.Core.sln` (full suite).
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/6 status update (row 6 `In Review`; PR link to follow).
- [x] Commit, push, and open Draft PR (PR #111).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-04 — Preflight: `git fetch origin` confirmed PR #110 already merged; fast-forwarded `main`; created branch `feat/ody-s05-106-minimal-test-catalog-fixtures`. Corrected `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 (`ODY-S05-103`) from stale `In Review`/wrong test range to `Done` with the accurate `TC-CATALOG-078`-`099` range.
- 2026-09-04 — Re-confirmed (via fresh reads, since files changed on disk since this session's own prior full reads) `ContentCatalogAuthoringContracts.cs`/`CatalogValidationContracts.cs`/`ContentCatalogLifecycleContracts.cs` all unchanged in their public surface -- no new architecture needed.
- 2026-09-04 — Designed the fixture graph and wrote `MinimalTestCatalogFixtureTests.cs`; first `dotnet build` failed on one missing `using Odyssey.Domain.Character;` for `BodyPartId` (used by the Armor fixture's `CoveredBodyPartIds`), fixed, second build passed (0 warnings, 0 errors). First `dotnet test` run: 12/12 passed on the first try.
- 2026-09-04 — Added `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-100`-`111`, referencing this task contract by `taskId` before running `verify-test-structure.ps1`.
- 2026-09-04 — Wrote this task's own contract and Brief plan.

## 7. Decisions

- 2026-09-04 — Decision: fixture lives as plain private C# helper methods on the test class, not a JSON asset or a new production factory type. Authority: "prefer the smallest maintainable shape" and "no new production feature surface unless strictly needed" from this task's own ТЗ.
- 2026-09-04 — Decision: both the fixture's typed reference (`Item.BuiltInEffectRefs`) and its generic-`DependencyRefs` reference (`Ability`) point at the *same* Effect fixture, rather than two separate Effects. Authority: keeps the graph minimal while still exercising both reference mechanisms this codebase actually has.
- 2026-09-04 — Decision: no new `ErrorCode`. Authority: every failure asserted (`WeaponNoCompatibleAmmoInCatalog`, `ReferenceMissing`, `ContentCatalogPublishValidationFailed`, `PersistenceContentDefinitionNotDraft`) already exists from `ODY-S05-103`/`104`.

## 8. Discoveries and deviations

- No architectural question was found during implementation -- every piece needed already exists and is already individually tested; this task only composes them.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 was stale in two ways (status and test range) -- corrected as this task's own explicit preflight step.
- One build error (missing `using`) was found and fixed on the first `dotnet build` attempt, before any test ran.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- Full-suite `dotnet test DotNet\Odyssey.Core.sln`: 623/623 passed, no regression.
- `.\scripts\verify-format.ps1`: `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1`: `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: `TC-ARCH-001 PASS valid ADR-001 graph passes`.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- test-only change, no production code, no schema, nothing to migrate back.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/111. CI pending. Closes the Content Catalog MVP block (`ODY-S05-101`-`106`). Future `SLICE-05` blocks (Inventory, `ItemInstance`/`ItemStack`, Equipment, full attack pipeline) remain reserved, not decomposed, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 7.
