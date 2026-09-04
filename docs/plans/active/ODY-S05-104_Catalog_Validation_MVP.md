# ODY-S05-104 — Catalog Validation MVP

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-104-catalog-validation-mvp`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/108
**Last updated:** 2026-09-04 UTC

## 1. Purpose and user-visible outcome

A single, authoritative, side-effect-free catalog validation service proving a Content Catalog definition's real usability -- not just required-field presence -- so `ODY-S05-103`'s own future publish gate has one place to call before a Draft becomes an immutable Published version. No publish/archive/delete, no runtime item/inventory/equipment/effect behavior, no `ContentBlock` execution.

## 2. Task contract

- Goal: a compiling, tested `CatalogValidationService` in `Odyssey.Application.Content` with `ValidateContentDefinition`/`ValidateDraftForPublish`, decoding through `ODY-S05-105`'s own `TypedDefinitionCodec` and reading only through the existing `IContentCatalogRepository`.
- Acceptance criteria: all six typed shapes get real per-type usability checks; missing/wrong-version/wrong-type exact references rejected; dependency cycles detected without infinite loop; ruleset compatibility checked; non-Draft rejected only at publish-time; validation never mutates a row; no publish/archive/delete/runtime/ContentBlock-execution; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-104`, `ADR-027` §4/20.
- In scope: `CatalogValidationContracts.cs` (new), `CatalogValidationServiceTests.cs` (new, 30 cases), `test-catalog.json` entries, `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 4/5 status update, this task's own contract/plan.
- Out of scope: publish/archive/delete (`ODY-S05-103`), authoring changes (`ODY-S05-102`'s surface untouched), new typed fields, any runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect, real ContentBlockGraph execution, any Unity/UI code, any `ADR-001`-`027` content change, balanced content fixtures (`ODY-S05-106`).
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §6 (`ODY-S05-104` boundary paragraph), `ADR-027` §4/4.1/12/20, `11_Content_Block_System` §7/8/25/29, `ContentCatalog.cs`/`TypedDefinitions.cs`/`TypedDefinitionCodec.cs`/`ContentCatalogRepositoryContracts.cs`/`SqliteContentCatalogRepository.cs` (full reads), `Anatomy.cs` (BodyPartId boundary confirmation).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main`, which already includes PR #107 (`ODY-S05-105`, Base Definition Types, merged) atop PR #106/#105.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 still read `In Review` despite PR #107 being merged -- corrected to `Done` as this task's own first step.
- `IContentCatalogRepository`/`SqliteContentCatalogRepository` read in full: `GetContentDefinition` resolves regardless of `Status` (Archived targets still load, per `ADR-027` §4.1 rule 3); no historical-version table exists, so a definition's own current `Version` column is the only version this repository can ever report -- sufficient for exact-version-reference comparison without any new repository method.
- `TypedDefinitions.cs`/`TypedDefinitionCodec.cs` read in full: `AbilityDefinition`/`EffectDefinition` carry no `ContentDefinitionRef` field of their own (only `ItemDefinition`'s embedded refs and `AmmoDefinition.EffectContributionRefs` do); cross-references for Ability/Effect can only flow through the generic `ContentDefinitionRecord.DependencyRefs` envelope field.
- `Anatomy.cs`/`Resource.cs`-adjacent code confirms `BodyPartId`/`ResourceDefinitionId` are SLICE-04's own fixture-only, regex-validated keys with no backing Ruleset-wide registry -- no live existence check is possible for these two reference kinds without inventing a new registry, which is out of this task's own scope.
- No real `ContentBlockGraph` implementation exists anywhere in this codebase -- `MechanicsPayloadRef` remains `ODY-S05-105`'s own opaque placeholder; `11_Content_Block_System` §25's full static-validation checklist is honored only at the level this codebase can actually support (exact-reference existence/version/type + cycle detection + a structural non-blank check on the opaque payload ref).

Assumptions: none.

## 4. Proposed approach

- Application (`CatalogValidationContracts.cs`): `CatalogValidationService.ValidateContentDefinition`/`ValidateDraftForPublish` (the latter adds a `DefinitionNotDraft` check on top of the former), each: (1) fetches the target via `repository.GetContentDefinition` (a miss is `Result.Failure`, not an issue); (2) checks `RulesetCompatibility` against `campaign.Manifest.RulesetId`/`RulesetVersion` (empty list = no restriction); (3) dispatches to a per-type validator (Item/Weapon/Armor/Ammo/Ability/Effect) that decodes via `TypedDefinitionCodec` and checks the genuine usability gaps `ODY-S05-105`'s own constructors leave open (weapon ammo-compatibility-keys-required and no-matching-ammo-in-catalog; ability/effect mechanics-payload-ref blank check); (4) runs one shared depth-first `ValidateReferencesAndCycles` traversal from the root definition across every typed + generic `ContentDefinitionRef` it can reach, checking existence/exact-version/target-type per edge and detecting cycles via a currently-on-stack set.
- Issue model: `CatalogValidationIssue` (issue code, severity, `UserMessageKey`, optional field path) + `CatalogValidationResult` (`IsValid` = no `Error`-severity issue, `Issues` list). `CatalogValidationIssueCode` is a plain 13-value enum, deliberately not a registered `ErrorCode` (validation findings are not `Error`/`Result.Failure` outcomes).
- Tests (`CatalogValidationServiceTests.cs`, real SQLite fixture mirroring `ContentCatalogAuthoringServiceTests`'s own convention): valid-definition-passes for all six types; malformed JSON; weapon ammo-compatibility (missing keys, no matching catalog ammo, matching ammo present); armor/ammo/ability/effect malformed-payload cases; missing/wrong-version/wrong-type reference cases; a genuine two-node dependency cycle (constructed via direct SQL, since no publish/update-dependency-refs command exists yet); ruleset compatibility (incompatible/compatible/unrestricted); non-Draft-fails-publish vs. general-validate; missing-definition-is-a-Result-failure; no-mutation-on-validate; two reflection/schema guards against runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect.
- Registry: no new `ErrorCode`; thirty new `TC-CATALOG-042`-`071` entries added to `test-catalog.json`.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 corrected to `Done`; row 4 (`ODY-S05-104`) marked `In Review` with the PR link once opened.

No Unity/UI code, no publish/archive/delete/authoring/runtime behavior, no `ADR-001`-`027` content change, no new persistence table/column.

## 5. Milestones

### M1 — Validation service and issue model

- [x] `CatalogValidationContracts.cs` (service, request, result, issue, severity, issue-code vocabulary).
- [x] `dotnet build` passes on first attempt after one missing-`using` fix.

### M2 — Tests and registry

- [x] `CatalogValidationServiceTests.cs` (Persistence, real SQLite, 30 cases) -- all passed on first run.
- [x] `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-042`-`071`.

### M3 — Validation and review readiness

- [x] `dotnet build DotNet\Odyssey.Core.sln` (full solution).
- [x] `dotnet test DotNet\Odyssey.Core.sln` (full suite).
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 status update (`In Review`; PR link to follow).
- [x] Commit, push, and open Draft PR (PR #108).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-04 — Preflight: `git fetch origin` confirmed PR #107 already merged; fast-forwarded `main`; created branch `feat/ody-s05-104-catalog-validation-mvp`. Corrected `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 (`ODY-S05-105`) from stale `In Review` to `Done`.
- 2026-09-04 — Read `SLICE-05_IMPLEMENTATION_BACKLOG.md` §6, `ADR-027` §4/4.1/12/20, `11_Content_Block_System` §7/8/25/29, `ContentCatalog.cs`, `TypedDefinitions.cs`, `TypedDefinitionCodec.cs`, `ContentCatalogRepositoryContracts.cs`, `SqliteContentCatalogRepository.cs`, `Anatomy.cs`, `ContentCatalogAuthoringContracts.cs` in full.
- 2026-09-04 — Decided: no new repository method for exact-version lookup (existing `GetContentDefinition` + `Version` comparison suffices, since no historical-version table exists); `CatalogValidationIssueCode` as a plain enum, not a registered `ErrorCode`; `BodyPartId`/`ResourceDefinitionId` validated structurally only (no live registry exists); `MechanicsPayloadRef` validated as a structurally-acceptable opaque reference only (no real `ContentBlockGraph` exists).
- 2026-09-04 — Implemented `CatalogValidationContracts.cs`; first `dotnet build` failed on one missing `using Odyssey.Domain.Identity;` for `CorrelationId`, fixed, second build passed (0 warnings, 0 errors).
- 2026-09-04 — Implemented `CatalogValidationServiceTests.cs` (30 cases) in `DotNet/Tests/Odyssey.Tests.Persistence/Content/`, mirroring `ContentCatalogAuthoringServiceTests`'s own fixture convention; a new `MarkPublishedDirectly` test helper (status + version + optional PropertiesJson/DependencyRefsJson, direct SQL) was added specifically to construct Published targets at an exact version and a genuine dependency cycle, since no `PublishDefinition`/dependency-ref-update command exists yet. Filtered `dotnet test` run: 30/30 passed on first try.
- 2026-09-04 — Added `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-042`-`071`, referencing this task contract by `taskId` before running `verify-test-structure.ps1` -- proactively applying the established "task contract must exist before test-catalog can reference its `taskId`" lesson.
- 2026-09-04 — Wrote this task's own contract and ExecPlan.
- 2026-09-04 — Full-suite `dotnet test` (583/583 passed), then `.\scripts\verify-format.ps1` failed on one whitespace formatting issue in `CatalogValidationContracts.cs`; fixed via `dotnet format DotNet\Odyssey.Core.sln --include Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs`, re-verified `dotnet build`/filtered `dotnet test` (30/30) still passed, then `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all passed. Staged only this task's own 6 files (the stray, untracked `Claude outputs/` directory from an earlier unrelated task confirmed present but deliberately not touched).

## 7. Decisions

- 2026-09-04 — Decision: `CatalogValidationIssueCode` is a plain enum, not a registered `ErrorCode`. Authority: `ERROR_CODES.md`'s registry governs `Error`/`Result.Failure` operation outcomes; a validation run that finds usability issues is still a successful `Result<CatalogValidationResult>.Success`.
- 2026-09-04 — Decision: no new repository method for exact-version lookup; reuse `GetContentDefinition` + compare its `Version` field. Authority: `SqliteContentCatalogRepository` keeps one row per `ContentDefinitionId` with no historical-version table -- a definition's own current `Version` is the only version this repository can ever report.
- 2026-09-04 — Decision: `BodyPartId`/`ResourceDefinitionId` validated structurally only, no live-registry existence check. Authority: both are SLICE-04's own fixture-only keys with no backing catalog table anywhere in this codebase; inventing a new registry is out of this task's own scope.
- 2026-09-04 — Decision: `MechanicsPayloadRef` validated only as a structurally-acceptable opaque reference. Authority: this task's own ТЗ explicit "ContentBlock / mechanics payload MVP" allowance; `11_Content_Block_System` §8 confirms no real graph implementation exists anywhere in this codebase.
- 2026-09-04 — Decision: dependency-cycle test fixture uses direct SQL (`MarkPublishedDirectly`, including `PropertiesJson`/`DependencyRefsJson`), mirroring `ODY-S05-101`/`102`'s own `MarkStatusDirectly` convention. Authority: no `PublishDefinition`/dependency-ref-update command exists yet through the public API to construct this state otherwise.

## 8. Discoveries and deviations

- No architectural question was found during implementation that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101`/`105`'s own foundation do not already answer -- no ADR was touched or extended.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 was stale (`In Review` despite PR #107 already merged) -- corrected as this task's own explicit preflight step.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- Full-suite `dotnet test DotNet\Odyssey.Core.sln`: 583/583 passed, no regression.
- `.\scripts\verify-format.ps1`: `FORMAT-001 PASS` (after one `dotnet format` fix).
- `.\scripts\check-repository-policy.ps1`: `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: `TC-ARCH-001 PASS valid ADR-001 graph passes`.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- no schema migration exists to roll back; this service is purely additive, read-only, and introduces no new table or column.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101`/`105` do not already answer.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/108 (amended). Enables `ODY-S05-103` (Publish/Archive/Delete Lifecycle) to gate publish on `ValidateDraftForPublish`, and `ODY-S05-106` to prove the full pipeline end-to-end.

## 13. Amendment (2026-09-04) — weapon ammo-applicability ruleset check

- Defect: `CatalogHasCompatibleAmmo` matched a candidate `AmmoDefinition` on `CompatibilityKeys` alone, never checking the candidate's own `RulesetCompatibility` against the active campaign ruleset -- a Weapon could pass publish validation on the strength of ammo scoped to a different Ruleset.
- Fix: factored `ValidateRulesetCompatibility`'s own rule into a shared `IsCompatibleWithActiveRuleset(campaign, rulesetCompatibility)` helper; `CatalogHasCompatibleAmmo` now skips any candidate ammo whose `RulesetCompatibility` does not include (or leave unrestricted) the active `campaign.Manifest.RulesetId@RulesetVersion` before checking its keys.
- Tests added: `TC-CATALOG-072` (matching ammo explicitly compatible with the active ruleset passes) and `TC-CATALOG-073` (matching-key ammo scoped to `other.ruleset@9.9.9` fails with `WeaponNoCompatibleAmmoInCatalog`).
- Validation re-run: `dotnet build` (0/0), `dotnet test` full suite (585/585, no regression), `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- PR #108 stays Draft pending re-review.
