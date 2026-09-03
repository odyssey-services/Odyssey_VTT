# ODY-S05-105 — Base Definition Types

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-105-base-definition-types`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/107
**Last updated:** 2026-09-04 UTC

## 1. Purpose and user-visible outcome

Give the Content Catalog typed shapes for its six mechanical definition kinds (item, weapon, armor, ammo, ability, effect), plus an explicit versioned codec mapping them to/from the existing generic `PropertiesJson` envelope — so the catalog stops being only an untyped string blob and `ODY-S05-104` has a real typed contract to validate against. No publish/archive/delete, no runtime item/inventory/equipment/effect behavior, no game-usability validation.

## 2. Task contract

- Goal: compiling, tested typed definition contracts in `Odyssey.Domain.Content` plus an explicit versioned JSON codec in `Odyssey.Application.Content`, storing typed data through the existing `ContentDefinitionRecord.PropertiesJson` field with no new persistence table.
- Acceptance criteria: all 6 typed definitions exist and round-trip through `PropertiesJson`; each type includes its ТЗ-specified minimum field set; decode rejects a `ContentDefinitionType` mismatch and malformed JSON safely (`Result<T>`, no raw exception); exact-version `ContentDefinitionRef`s round-trip pinned to their exact version; `SLICE-04` `Resource`/`BodyPart` types are reused, not duplicated; no validation/publish/archive/delete/runtime/Unity code is introduced; `ADR-001`–`027` unmodified; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-105`, `ADR-027` §4/6.
- In scope: `TypedDefinitions.cs` (Domain, new), `TypedDefinitionCodec.cs` (Application, new), two new `ErrorCodes` entries, `TypedDefinitionCodecTests.cs` (new), `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 2/5 status update, this task's own contract/plan.
- Out of scope: publish/archive/delete (`ODY-S05-103`), validation/applicability checking (`ODY-S05-104`), any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect runtime, any Unity/UI code, any `ADR-001`–`027` content change, minimal test-catalog content fixtures (`ODY-S05-106`).
- Required authorities: `ADR-027` §3/4/6/8/12/20, `11_Content_Block_System` §7/8/13/14/15/21/22, `ContentCatalog.cs`/`ContentCatalogRepositoryContracts.cs` (`ODY-S05-101` foundation, full read), `Ability.cs`/`Resource.cs`/`Anatomy.cs` (`SLICE-04` ID conventions, full read), `SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main`, which already includes PR #106 (`ODY-S05-102`, GM Catalog Authoring MVP, merged) atop PR #105 (`ODY-S05-101`, Content Catalog Foundation, merged).
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 still read `In Review` despite PR #106 being merged — corrected to `Done` as this task's own first step.
- `ContentCatalog.cs`/`ContentCatalogRepositoryContracts.cs` read in full: `ContentDefinitionRef` is an exact-version `{DefinitionId, Version}` struct with no "latest" concept; `ContentDefinitionRecord.PropertiesJson` is a plain opaque string with no existing typed mapping layer.
- `Odyssey.Content` project inspected: nearly empty (`ContentPackageVersion.cs`, `SemVerValue.cs` only), no Newtonsoft.Json reference; `Odyssey.Application` already references Newtonsoft.Json and already hosts `Odyssey.Application.Content` (from `ODY-S05-102`).
- `Ability.cs`/`Resource.cs`/`Anatomy.cs` read in full: `AbilityDefinitionId`/`ResourceDefinitionId`/`BodyPartId` use `SLICE-04`'s lightweight regex-validated string-key ID pattern, directly reusable by this task's typed definitions.
- `11_Content_Block_System` §7/8/13/14/15/21/22 read in full: source of the exact enum vocabularies (`AbilityEntryPointType`, `EffectDurationType`, `EffectStackPolicy`, a narrowed `ContentTargetSource`) embedded verbatim in `TypedDefinitions.cs`. §8 (`ContentBlockGraph`) confirms full block-graph execution modeling is out of this task's scope.
- `DotNet/Tests/Odyssey.Tests.Unit/Odyssey.Tests.Unit.csproj` confirmed to already reference `Odyssey.Domain`/`Odyssey.Rules`/`Odyssey.Content`/`Odyssey.Application` — a ready-to-use home for pure, in-memory codec round-trip tests with no project-file change needed.

Assumptions: none.

## 4. Proposed approach

- Domain (`Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`): pure, serializer-free value types — `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition` — each validating only structural shape (enum-defined, non-null, non-negative, internally consistent flag/value pairs) in its constructor. Supporting types: `ItemCategory`, `WeaponAttackMode`, `AmmoRequirement`, `ContentTargetSource`, `ContentTargetRule`, `AbilityEntryPointType`, `AbilityResourceCost`, `EffectDurationType`, `EffectStackPolicy`. `AbilityResourceCost` reuses `ResourceDefinitionId`; `ArmorDefinition` reuses `BodyPartId` — both from `Odyssey.Domain.Character`, no duplication.
- Application (`Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs`): static codec with `Encode*`/`Decode*` pairs for all six types, each payload embedding `schemaVersion: 1`. `Decode*` takes the record's actual `ContentDefinitionType` and rejects a mismatch with `ContentCatalogTypedDefinitionWrongType` before ever parsing JSON; malformed/incomplete JSON is caught and mapped to `ContentCatalogTypedDefinitionMalformedPayload` via `Result<T>`, never a raw exception. Weapon/Armor/Ammo share `WriteItemPayload`/`ReadItemPayload` for their embedded `ItemDefinition`; Ability/Effect share `WriteTargetRule`/`ReadTargetRule`.
- Registry: two new `ErrorCode`s registered in `ErrorCodes.cs`, `ERROR_CODES.md`, and referenced by `test-catalog.json`'s new `TC-CATALOG-032`/`034` entries.
- Tests (`DotNet/Tests/Odyssey.Tests.Unit/Content/TypedDefinitionCodecTests.cs`): pure in-memory round-trip tests for all six types (including an ammo-required weapon variant and an Instant-duration effect variant), two wrong-type rejections, a four-case malformed-JSON parameterized test, a missing-required-field test, an exact-version-reference-preservation test, and a reflection-based scan proving no runtime item/inventory/equipment/effect type exists in `Odyssey.Domain.Content`/`Odyssey.Application.Content`.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 corrected to `Done`; row 5 (`ODY-S05-105`) marked `In Review` with the PR link once opened.

No Unity/UI code, no publish/archive/delete/validation/runtime behavior, no `ADR-001`–`027` content change.

## 5. Milestones

### M1 — Domain typed definitions

- [x] `TypedDefinitions.cs` (6 typed definitions + supporting enums/value types).
- [x] `dotnet build` passes on first attempt.

### M2 — Application codec and registry

- [x] `TypedDefinitionCodec.cs` (Encode/Decode for all 6 types).
- [x] `ErrorCodes.cs` two new entries.
- [x] `dotnet build` passes.

### M3 — Tests and registry

- [x] `TypedDefinitionCodecTests.cs` (17 test methods, all passing on first run).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CATALOG-024`–`037`).

### M4 — Validation and review readiness

- [x] `dotnet build DotNet\Odyssey.Core.sln` (full solution).
- [x] `dotnet test DotNet\Odyssey.Core.sln` (full suite).
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 status update with PR link.
- [x] Commit, push, and open Draft PR (PR #107).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-04 — Preflight: `git fetch origin` confirmed PR #106 already merged; fast-forwarded `main`; created branch `feat/ody-s05-105-base-definition-types`. Corrected `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 (`ODY-S05-102`) from stale `In Review` to `Done`.
- 2026-09-04 — Read `SLICE-05_IMPLEMENTATION_BACKLOG.md`, `ADR-027` §3/4/6/8/12/20, `11_Content_Block_System` §7/8/13/14/15/21/22, `ContentCatalog.cs`, `ContentCatalogRepositoryContracts.cs`, `Ability.cs`/`Resource.cs`/`Anatomy.cs`, `ADR-001` §1 (dependency graph) in full.
- 2026-09-04 — Decided typed types go in `Odyssey.Domain.Content` (serializer-free); codec goes in `Odyssey.Application.Content` rather than the nearly-empty `Odyssey.Content` project (recorded in section 7 and the task contract's own §18).
- 2026-09-04 — Implemented `TypedDefinitions.cs`; `dotnet build` passed on first attempt (0 warnings, 0 errors).
- 2026-09-04 — Implemented `TypedDefinitionCodec.cs` and the two `ErrorCodes.cs` entries; `dotnet build` passed on the next run.
- 2026-09-04 — Implemented `TypedDefinitionCodecTests.cs` (17 test methods) in `DotNet/Tests/Odyssey.Tests.Unit/Content/`, confirming via `grep` on `Odyssey.Tests.Unit.csproj` that it already references `Odyssey.Application`, avoiding any project-file change. Filtered `dotnet test` run: 17/17 passed on first try.
- 2026-09-04 — Added `docs/errors/ERROR_CODES.md` rows and `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-024`–`037`, referencing this task contract by `taskId` before running `verify-test-structure.ps1` — proactively applying the "task contract must exist before test-catalog can reference its `taskId`" lesson already learned from `ODY-S04-113a`/`115a`/`ODY-S05-101`/`102`.
- 2026-09-04 — Wrote this task's own contract and ExecPlan.
- 2026-09-04 — Full-suite `dotnet test` (543/543 passed), `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all passed on first run. Staged only this task's own 9 files (a stray, untracked `Claude outputs/` directory left over from an earlier unrelated cleanup task was confirmed present but deliberately not touched or committed). Committed, pushed `feat/ody-s05-105-base-definition-types`, opened Draft PR #107. Updated this contract/plan/backlog with the real PR link.

## 7. Decisions

- 2026-09-04 — Decision: place pure typed value types in `Odyssey.Domain.Content`, the JSON codec in `Odyssey.Application.Content` rather than the `Odyssey.Content` project `ADR-027` §14 nominally reserves. Authority: `Odyssey.Content` confirmed nearly empty with no Newtonsoft.Json reference; `Odyssey.Application` already references it and already hosts `Odyssey.Application.Content`; consistent with `ODY-S05-101`/`102`'s own precedent of building directly in Domain/Application/Persistence without touching `Odyssey.Content`.
- 2026-09-04 — Decision: model weapon/ammo compatibility as a plain string tag/category key rather than an exact-version `ContentDefinitionRef`, reserving exact refs for places where an exact reference is actually meaningful (`ItemDefinition.BuiltInAbilityRefs`/`BuiltInEffectRefs`, `AmmoDefinition.EffectContributionRefs`). Authority: this task's own ТЗ distinguishes "ammo compatibility reference shape" from its separate "exact-version references remain exact" test requirement.
- 2026-09-04 — Decision: reuse `ResourceDefinitionId`/`BodyPartId` directly from `Odyssey.Domain.Character` rather than inventing parallel Content-namespace types. Authority: this task's own ТЗ explicit instruction not to duplicate existing `SLICE-04` concepts.
- 2026-09-04 — Decision: keep `MechanicsPayloadRef` as an opaque placeholder string, not a real `ContentBlockGraph` structure. Authority: `11_Content_Block_System` §8 confirms full graph/execution modeling is a distinct, larger concern out of this task's scope.

## 8. Discoveries and deviations

- No architectural question was found during implementation that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101`'s own foundation do not already answer — no ADR was touched or extended.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 was stale (`In Review` despite PR #106 already merged) — corrected as this task's own explicit preflight step, per the ТЗ's own instruction.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- Filtered `dotnet test` run (`TypedDefinitionCodecTests`): 17/17 passed.
- Full-suite `dotnet test DotNet\Odyssey.Core.sln`: 543/543 passed, no regression.
- `.\scripts\verify-format.ps1`: `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1`: `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: `TC-ARCH-001 PASS valid ADR-001 graph passes`.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema migration exists to roll back; all new types/methods are purely additive over `ODY-S05-101`'s own existing `PropertiesJson` column.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101` do not already answer.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/107. CI pending. Enables `ODY-S05-104` (Catalog Validation MVP) to validate against real typed shapes instead of ad-hoc JSON parsing.
