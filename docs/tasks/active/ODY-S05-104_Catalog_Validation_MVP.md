# ODY-S05-104 — Catalog Validation MVP

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-104-catalog-validation-mvp` (original PR #108, merged); follow-up: `fix/ody-s05-104-reference-ruleset-compatibility`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/108 (merged at commit `58d95c8`, only the first amendment); follow-up: https://github.com/odyssey-services/Odyssey_VTT/pull/109 (carries the second amendment, commit `742bae0` cherry-picked as `a364085`)
**ExecPlan:** `docs/plans/active/ODY-S05-104_Catalog_Validation_MVP.md`
**Created:** 2026-09-04
**Last updated:** 2026-09-04 UTC (amended twice: weapon ammo-applicability ruleset check (merged in PR #108); referenced-definition ruleset compatibility (follow-up PR, not yet merged))

## 1. Goal

Implement a single, authoritative, side-effect-free catalog validation layer proving a Content Catalog definition's real usability/applicability -- not just required-field presence (`SLICE-05_IMPLEMENTATION_BACKLOG.md` section 3.4, `ADR-027` section 20). This task returns a structured validation result only; it does not publish, archive, or delete anything (`ODY-S05-103`'s own job), and it does not implement any runtime Inventory/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` behavior or execute attacks/abilities/effects/`ContentBlock` graphs.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-101`/`102` give the catalog storage and authoring; `ODY-S05-105` gives it typed shapes. Nothing yet proves a stored definition is actually *usable* -- e.g. a Weapon with `AmmoRequirement.Required` and no compatible `AmmoDefinition` anywhere in the catalog, or a typed property referencing a `ContentDefinitionId` that does not exist.
- Value or risk reduction: gives `ODY-S05-103`'s own future publish gate one authoritative source of truth to call before a Draft becomes an immutable Published version, preventing unusable content from ever being published.
- Blocking or enabling relationship: unblocks `ODY-S05-103` (publish is gated by validation) and `ODY-S05-106` (fixture proof needs real validation to prove against); depends on `ODY-S05-101` (definitions to validate) and `ODY-S05-105` (real typed properties to validate against, not a placeholder shape).

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, section 6's `ODY-S05-104` task-boundary paragraph (the eight verbatim product-owner validation expectations) and section 3.4.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 4, 4.1, 12, 20 (full read).
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md`, sections 7, 8, 25, 29 (full read) -- `ContentEntryPoint`, `ContentBlockGraph`, Static validation's own checklist, `ContentDependency`.
- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs` (full read) -- `ContentDefinitionRef`'s exact-version shape this task validates against.
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` (full read) -- the six typed shapes (`ODY-S05-105`) this task decodes and validates.
- `Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs` (full read) -- the codec this task calls, never re-implements.
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` (full read) -- `IContentCatalogRepository`/`ContentDefinitionRecord`, this task's own read-only dependency surface.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` (full read) -- confirms `GetContentDefinition` does not filter by `Status` (so an Archived target still resolves) and that no historical-version table exists (a `ContentDefinitionId` row's own current `Version` field is the only version this repository can ever report -- see section 18's recorded decision on exact-version lookup).
- `Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs` (full read) -- confirms `BodyPartId` is a per-Character structural slot (`ODY-S04-109`) with no backing Ruleset-wide registry anywhere in this codebase.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-104`, `ADR-027` section 4/20.
- Existing test IDs: `TC-CATALOG-001`-`041` (re-verified unmodified).
- New test IDs introduced: `TC-CATALOG-042`-`071`.

### Task-safe private context

- Approved summary / references: `ADR-027`/`11_Content_Block_System`'s own already-accepted/published content is cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed PR #107 (`ODY-S05-105`, Base Definition Types) is already merged into `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 still read `In Review` despite PR #107 being merged -- corrected to `Done` as this task's own first preflight step.
- `IContentCatalogRepository` has no exact-version historical lookup: `GetContentDefinition` returns one row per `ContentDefinitionId`, whose own `Version` column is mutated in place (there is no `PublishDefinition` command yet, so `Version` only ever changes via direct test-only SQL). Comparing a `ContentDefinitionRef.Version` against the resolved target's own current `Version` field is therefore sufficient and correct for this MVP -- no new repository method or persistence table is needed (recommended-design point 6/8 satisfied without any repository change).
- `AbilityDefinition`/`EffectDefinition` (`ODY-S05-105`) carry no `ContentDefinitionRef` field of their own -- only `ItemDefinition` (embedded in Item/Weapon/Armor/Ammo) and `AmmoDefinition.EffectContributionRefs` do. Cross-references for Ability/Effect can only come through the generic `ContentDefinitionRecord.DependencyRefs` envelope field.
- `BodyPartId` (`ODY-S04-109`) and `ResourceDefinitionId` (`ODY-S04-108`) are both SLICE-04's own lightweight, fixture-only, regex-validated Ruleset keys with no backing catalog table anywhere in this codebase -- there is no live registry against which `ArmorDefinition.CoveredBodyPartIds`/`AbilityResourceCost.ResourceDefinitionId` existence could be checked. This is recorded as an explicit MVP boundary (section 18), not a skipped check.
- No real `11_Content_Block_System` section 8 `ContentBlockGraph` exists anywhere in this codebase -- `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef` are `ODY-S05-105`'s own opaque placeholder strings. Section 25's own static-validation checklist (DAG/cycles, resolved pinned references, Ruleset compatibility, etc.) is honored at the level this codebase can actually support today: exact-reference existence/version/type-correctness and cycle detection across the real `ContentDefinitionRef` graph, plus a structural (non-blank) check on the opaque `MechanicsPayloadRef` itself.

### Assumptions

- None. Every fact above was directly observed via `Read`/`Grep`/`git`/`dotnet build`/`dotnet test` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs` (new): `CatalogValidationService` (`ValidateContentDefinition`/`ValidateDraftForPublish`), `ValidateContentDefinitionRequest`, `CatalogValidationResult`, `CatalogValidationIssue`, `CatalogValidationSeverity`, `CatalogValidationIssueCode` (13-value explicit vocabulary).
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/CatalogValidationServiceTests.cs` (new): real, SQLite-backed tests against the real repository (30 cases).
- `Tests/Metadata/test-catalog.json`: thirty new `TC-CATALOG-042`-`071` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 5 (`ODY-S05-105`) corrected to `Done`; row 4 (`ODY-S05-104`) status update with PR link.
- This task contract and its ExecPlan.

### Out of scope

- `PublishDefinition`/`ArchiveDefinition`/physical delete/Archived-list query (`ODY-S05-103`'s own job -- this task returns a validation result only, it never calls a repository write method).
- Any MainGM authoring service change (`ODY-S05-102`'s own surface is untouched by this task).
- Any new typed definition field beyond what `ODY-S05-105` already declared.
- Any runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`, attack resolution, ability execution, or effect application.
- Any real `ContentBlockGraph` execution engine, script engine, or arbitrary code execution.
- Any Unity UI or Content Editor UI.
- Balanced content fixtures (`ODY-S05-106`'s own job).
- `.odcontent` import/export.
- Any edit to `ADR-001`-`027`'s own accepted content.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs
DotNet/Tests/Odyssey.Tests.Persistence/Content/CatalogValidationServiceTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-104_Catalog_Validation_MVP.md
docs/plans/active/ODY-S05-104_Catalog_Validation_MVP.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*, ODY-S05-101_*, ODY-S05-102_*, ODY-S05-105_*
Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs
Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs
Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs
Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
docs/errors/ERROR_CODES.md (no new ErrorCode is introduced by this task -- see section 18)
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` owns the entire validation layer; no `Odyssey.Domain`/`Odyssey.Persistence` change (`ADR-001`). `CatalogValidationService` depends only on `IContentCatalogRepository` (an existing interface) and `TypedDefinitionCodec` (an existing codec) -- no new dependency.
- Authoritative-state and transaction boundary: not applicable -- this service performs reads only (`GetContentDefinition`/`ListContentDefinitions`), never a write, and participates in no transaction of its own.
- Serialization / compatibility boundary: no new persisted contract; decodes exclusively through the already-versioned `TypedDefinitionCodec`.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable -- pure .NET code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: every `CatalogValidationIssue.MessageKey` is a `UserMessageKey`, the same public-safe convention `Error` already uses; no raw JSON, exception text, or stack trace is ever placed in an issue.
- Performance or platform constraint: dependency-graph traversal is bounded by a currently-on-stack cycle check (mathematically guaranteed to terminate) plus a defensive flat node-count cap (256) as an additional safety net.
- Other: `CatalogValidationIssueCode` is a plain enum, deliberately not a registered `ErrorCode` -- see section 18's recorded decision; `check-repository-policy.ps1`'s `ErrorCode` registry check is therefore unaffected by this task.

## 7. Expected behavior

### Scenario 1 -- a fully valid typed definition passes

**Given** a Draft `ItemDefinition`/`WeaponDefinition`/`ArmorDefinition`/`AmmoDefinition`/`AbilityDefinition`/`EffectDefinition` with no usability gap
**When** `ValidateDraftForPublish` is called
**Then** it returns `Result.Success` with `CatalogValidationResult.IsValid == true` and an empty issue list.

### Scenario 2 -- a Weapon requiring ammo with nothing to fire

**Given** a Weapon Draft with `AmmoRequirement.Required` and either empty `CompatibleAmmoKeys` or no matching `AmmoDefinition` anywhere in the catalog
**When** `ValidateDraftForPublish` is called
**Then** it returns `IsValid == false` with `WeaponAmmoCompatibilityKeysRequired`/`WeaponNoCompatibleAmmoInCatalog` respectively.

### Scenario 3 -- a missing or wrong-version or wrong-type exact reference

**Given** a typed definition whose own `ContentDefinitionRef` (built-in ability/effect ref, ammo effect contribution ref) points at a definition that does not exist, exists at a different `Version`, or exists but is the wrong `ContentDefinitionType`
**When** validated
**Then** it returns `ReferenceMissing`/`ReferenceVersionMismatch`/`ReferenceWrongType` respectively -- the target's own `ContentDefinitionStatus` (including Archived) never blocks resolution.

### Scenario 4 -- a dependency cycle

**Given** two definitions whose combined typed/generic references form a cycle
**When** validated
**Then** the traversal terminates deterministically and returns `DependencyCycleDetected`, never an infinite loop or a `StackOverflowException`.

### Scenario 5 -- ContentBlock/mechanics payload MVP boundary

**Given** an `AbilityDefinition`/`EffectDefinition` whose `MechanicsPayloadRef` is `null` (no mechanics implemented yet -- allowed) or a non-blank string (a structurally-acceptable opaque reference)
**When** validated
**Then** no mechanics-payload issue is produced; a present-but-blank `MechanicsPayloadRef` produces `AbilityMechanicsPayloadRefInvalid`/`EffectMechanicsPayloadRefInvalid`. No real `ContentBlockGraph` DAG/cycle/operation-name check exists to run, since no such graph exists in this codebase yet -- this boundary is validated, not silently skipped.

### Scenario 6 -- non-Draft cannot pass publish-time validation

**Given** a Published (or Archived) definition
**When** `ValidateDraftForPublish` is called
**Then** it returns `IsValid == false` with `DefinitionNotDraft`; `ValidateContentDefinition` (no Draft requirement) does not produce that issue for the same record.

### Required invariants

- No `ODY-S05-103`/`102` behavior (publish/archive/delete, authoring) is implemented or modified.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table is introduced (`TC-CATALOG-070`/`071`).
- `ADR-001`-`027` are unmodified.
- Validation never mutates a catalog row (`TC-CATALOG-069`).
- A missing target definition being validated is a `Result.Failure` (an operation failure), never folded into `CatalogValidationResult.Issues` (`TC-CATALOG-068`).

## 8. Deliverables

- Production code: `CatalogValidationContracts.cs` (Application) -- one static service, five supporting types, zero new `ErrorCode`.
- Tests: `CatalogValidationServiceTests.cs` (36 cases, amended: +2, +4) -- `TC-CATALOG-042`-`077`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 4/5), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None -- no schema change; this task adds no table and no column.

## 9. Acceptance criteria

1. A catalog validation service/contract exists in the Application layer (`CatalogValidationService`).
2. It validates Draft definitions for publish-readiness without publishing them (`ValidateDraftForPublish`, `TC-CATALOG-067`).
3. It decodes the correct typed definition based on `ContentDefinitionType` (`TC-CATALOG-042`-`047`).
4. It returns structured validation issues, not raw exceptions (`CatalogValidationResult`/`CatalogValidationIssue`, `TC-CATALOG-048`).
5. Weapon validation covers damage/range/mode/action-cost (via decode) and ammo requirement/compatibility (`TC-CATALOG-049`-`052`).
6. Armor validation covers equipment slot, body-part references, protection, durability (via decode/ctor guarantees; `TC-CATALOG-053`).
7. Ammo validation covers compatibility keys and effect contribution refs (`TC-CATALOG-054`/`055`).
8. Ability validation covers entry point, trigger, cost, target rules (via decode), resource refs (structural boundary recorded), and mechanics payload (`TC-CATALOG-057`/`060`).
9. Effect validation covers target rules, duration, stacking policy (via decode), and mechanics payload (`TC-CATALOG-058`/`059`).
10. Missing definition references are rejected (`TC-CATALOG-055`).
11. Exact-version mismatches are rejected (`TC-CATALOG-061`).
12. ContentBlock/mechanics payload MVP boundary is explicitly validated (`TC-CATALOG-059`/`060`), not faked.
13. Ruleset/version compatibility is checked against the active campaign ruleset/version (`TC-CATALOG-064`-`066`).
14. Validation is side-effect-free (`TC-CATALOG-069`).
15. No publish/archive/delete behavior is implemented.
16. No runtime item/equipment/inventory/effect implementation is introduced (`TC-CATALOG-070`/`071`).
17. No Unity/UI code is touched.
18. No accepted ADR architecture sections are modified.
19. New tests are registered in `Tests/Metadata/test-catalog.json`.
20. Task contract and ExecPlan for `ODY-S05-104` are added.
21. `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-105` as `Done` and `ODY-S05-104` as `In Review` with PR link.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-042`-`047` | .NET / NUnit (Persistence) | Valid Item/Weapon/Armor/Ammo/Ability/Effect Draft each pass validation | Pass |
| `TC-CATALOG-048` | .NET / NUnit (Persistence) | Malformed typed JSON returns `TypedPayloadMalformed`, not a raw exception | Pass |
| `TC-CATALOG-049`-`052` | .NET / NUnit (Persistence) | Weapon damage/ammo-compatibility usability | Pass |
| `TC-CATALOG-053` | .NET / NUnit (Persistence) | Armor missing required fields fails | Pass |
| `TC-CATALOG-054` | .NET / NUnit (Persistence) | Ammo missing compatibility key fails | Pass |
| `TC-CATALOG-055` | .NET / NUnit (Persistence) | Ammo effect contribution ref to missing Effect fails | Pass |
| `TC-CATALOG-056` | .NET / NUnit (Persistence) | Reference target wrong type rejected | Pass |
| `TC-CATALOG-057` | .NET / NUnit (Persistence) | Ability missing trigger fails | Pass |
| `TC-CATALOG-058`-`060` | .NET / NUnit (Persistence) | Effect invalid duration fails; ContentBlock/mechanics payload boundary (negative + positive) | Pass |
| `TC-CATALOG-061`/`062` | .NET / NUnit (Persistence) | Exact-version mismatch rejected / exact match accepted | Pass |
| `TC-CATALOG-063` | .NET / NUnit (Persistence) | Dependency cycle detected, no infinite loop | Pass |
| `TC-CATALOG-064`-`066` | .NET / NUnit (Persistence) | Ruleset compatibility (incompatible/compatible/unrestricted) | Pass |
| `TC-CATALOG-067` | .NET / NUnit (Persistence) | Non-Draft fails `ValidateDraftForPublish`, not `ValidateContentDefinition` | Pass |
| `TC-CATALOG-068` | .NET / NUnit (Persistence) | Missing definition returns `Result.Failure`, not an issue | Pass |
| `TC-CATALOG-069` | .NET / NUnit (Persistence) | Validation does not mutate the catalog row | Pass |
| `TC-CATALOG-070`/`071` | .NET / NUnit (Persistence) | No runtime item/inventory/equipment/effect type or table introduced | Pass |
| `TC-CATALOG-072` | .NET / NUnit (Persistence) | Weapon ammo applicability: matching ammo compatible with the active ruleset passes | Pass |
| `TC-CATALOG-073` | .NET / NUnit (Persistence) | Weapon ammo applicability: matching-key ammo scoped to a different ruleset does not satisfy the requirement | Pass |
| `TC-CATALOG-074` | .NET / NUnit (Persistence) | Referenced-definition ruleset compatibility: a typed ref to a definition scoped to a different ruleset fails | Pass |
| `TC-CATALOG-075`/`076` | .NET / NUnit (Persistence) | Referenced-definition ruleset compatibility: unrestricted / active-ruleset-compatible target passes | Pass |
| `TC-CATALOG-077` | .NET / NUnit (Persistence) | Referenced-definition ruleset compatibility: the generic `DependencyRefs` field is checked the same way as a typed ref | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, and no `ADR-001`-`027` file, is touched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable -- no Unity/UI code.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the same fixture convention `ODY-S05-101`/`102`'s own tests already use.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation -- no Unity code touched.
- Any test of `ODY-S05-103`/`106`'s own future behavior -- neither exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None -- purely additive service; no existing table, column, or contract altered.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None -- this service performs no write.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None beyond already-public catalog definition shapes.
- Trust boundaries: Not applicable -- this task adds no command, no authorization surface change; `ODY-S05-102`'s own MainGM-only authoring gate is unmodified and unrelated to this read-only validation service.
- Authorization / audience checks: Not applicable -- validation itself has no permission model (any caller with a `CampaignHandle` and repository reference can validate; `ODY-S05-103`'s own future publish command is where a permission check belongs, unchanged by this task).
- Redaction requirements: Not applicable.
- Log-safe fields: Every `CatalogValidationIssue.MessageKey` is a `UserMessageKey`; `FieldPath` is a best-effort structural hint, never raw payload content.
- Abuse / malformed input limits: decode failures are caught by the existing `TypedDefinitionCodec` safety net (never a raw exception); dependency traversal is bounded by cycle detection plus a defensive node-count cap.
- Security tests: `TC-CATALOG-048` (malformed JSON safe failure), `TC-CATALOG-063` (cycle does not hang/crash).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: this task introduces a new public Application-layer contract (`CatalogValidationService` and its supporting types) that `ODY-S05-103`'s own future publish gate will depend on -- `PLANS.md` §1's own "new public contract" trigger, matching `ODY-S05-101`/`102`/`105`'s own reasoning for their sibling tasks.
- ExecPlan path: `docs/plans/active/ODY-S05-104_Catalog_Validation_MVP.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ODY-S05-101` is merged into `main` (confirmed in section 4); depends on `ODY-S05-105`'s own typed shapes (also already merged).

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 4/5), this task contract, its ExecPlan.
- Documents that must not change: `docs/errors/ERROR_CODES.md` (no new `ErrorCode` -- section 18), `ADR-001`-`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*`/`ODY-S05-002_*`/`ODY-S05-101_*`/`ODY-S05-102_*`/`ODY-S05-105_*`.
- Application version change: No.
- Schema / format / contract / protocol / Ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that publish/archive/delete/runtime/ContentBlock-execution are deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs` -- new.
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/CatalogValidationServiceTests.cs` -- new, 36 tests (amended: +2, +4).
- `Tests/Metadata/test-catalog.json` -- thirty-six new `TC-CATALOG-042`-`077` entries (amended: +2 IDs `072`-`073`, +4 IDs `074`-`077`).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` -- row 5 corrected to `Done`, row 4 status update.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (589/589, amended: +2, +4), including 36 `CatalogValidationServiceTests` cases, no regression |
| `.\scripts\verify-format.ps1` | Pass | First run failed on one whitespace formatting issue in `CatalogValidationContracts.cs`; fixed via `dotnet format`, second run passed with `FORMAT-001 PASS` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS (no new `ErrorCode`, confirmed no registry impact); `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `CatalogValidationContracts.cs`. |
| AC-2 | Pass | `TC-CATALOG-067`. |
| AC-3 | Pass | `TC-CATALOG-042`-`047`. |
| AC-4 | Pass | `TC-CATALOG-048`. |
| AC-5 | Pass | `TC-CATALOG-049`-`052`, `072`/`073` (ammo ruleset-applicability amendment). |
| AC-6 | Pass | `TC-CATALOG-053`. |
| AC-7 | Pass | `TC-CATALOG-054`/`055`. |
| AC-8 | Pass | `TC-CATALOG-057`/`060`. |
| AC-9 | Pass | `TC-CATALOG-058`/`059`. |
| AC-10 | Pass | `TC-CATALOG-055`, `074`/`077` (referenced-definition ruleset-compatibility amendment). |
| AC-11 | Pass | `TC-CATALOG-061`. |
| AC-12 | Pass | `TC-CATALOG-059`/`060`. |
| AC-13 | Pass | `TC-CATALOG-064`-`066`. |
| AC-14 | Pass | `TC-CATALOG-069`. |
| AC-15 | Pass | No `PublishDefinition`/`ArchiveDefinition`/delete method exists anywhere in this task's diff. |
| AC-16 | Pass | `TC-CATALOG-070`/`071`. |
| AC-17 | Pass | No Unity/UI path in Allowed paths or diff. |
| AC-18 | Pass | `git status --porcelain` confirms no `ADR-001`-`027` file touched. |
| AC-19 | Pass | Thirty-six `TC-CATALOG-042`-`077` entries added. |
| AC-20 | Pass | This task contract and ExecPlan exist. |
| AC-21 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 marked `In Review` with PR [#108](https://github.com/odyssey-services/Odyssey_VTT/pull/108). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table (to be completed after the full validation suite runs).

### Amendment (2026-09-04) — weapon ammo-applicability ruleset check

Product-owner review found that `CatalogHasCompatibleAmmo` treated a candidate `AmmoDefinition` as satisfying a Weapon's `AmmoRequirement.Required` on a plain `CompatibilityKeys` string match alone, without checking that the candidate ammo's own `RulesetCompatibility` actually included the active campaign ruleset -- a Weapon could pass `ValidateDraftForPublish` on the strength of ammo scoped to an entirely different Ruleset. Fixed by factoring `ValidateRulesetCompatibility`'s own compatibility rule into a shared `IsCompatibleWithActiveRuleset(campaign, rulesetCompatibility)` helper, and having `CatalogHasCompatibleAmmo` skip any candidate whose own `RulesetCompatibility` does not include (or leave unrestricted) the active `campaign.Manifest.RulesetId@RulesetVersion` before checking its compatibility keys. Two new tests (`TC-CATALOG-072`/`073`) cover the positive (ammo explicitly compatible with the active ruleset) and negative (matching key, incompatible ruleset -> `WeaponNoCompatibleAmmoInCatalog`) cases; the existing empty-RulesetCompatibility control case (`TC-CATALOG-052`) continues to pass unchanged.

### Amendment (2026-09-04, second) — referenced-definition ruleset compatibility

Product-owner review found that `ValidateReferencesAndCycles` checked a referenced definition's existence, exact version, and target type, but never its own `RulesetCompatibility` against the active campaign -- an Item/Ammo/etc. could pass publish validation while referencing an Ability/Effect/other definition scoped to an incompatible Ruleset. Fixed by inserting an `IsCompatibleWithActiveRuleset(campaign, child.RulesetCompatibility)` check in the traversal, right after the version/type checks and before recursing into the child, reusing the shared helper the first amendment already introduced. An incompatible target adds the existing `CatalogValidationIssueCode.RulesetIncompatible` issue -- no new issue code -- but with `FieldPath` pinned to the exact reference (e.g. `properties.builtInEffectRefs[0]`, `dependencyRefs[0]`), not the generic `"rulesetCompatibility"` path `ValidateRulesetCompatibility` itself uses for the definition being directly validated. Archived targets remain loadable exactly as before (`GetContentDefinition` does not filter by status); this check runs only after existence/version/type all already passed. Four new tests (`TC-CATALOG-074`-`077`) cover a typed-reference negative case, two positive control cases (unrestricted target; target explicitly compatible with the active ruleset), and a negative case through the generic `DependencyRefs` field (the only cross-reference mechanism Ability/Effect have of their own).

**Delivery note:** this amendment's own commit (`742bae0`) was pushed to `feat/ody-s05-104-catalog-validation-mvp` while PR #108 was already being reviewed; the reviewer merged PR #108 at its then-current head (`58d95c8`, only the first amendment) before this second amendment's commit was picked up by GitHub's PR/CI machinery, so `742bae0` never actually reached `main`. Recovered as a follow-up: cherry-picked `742bae0` onto a fresh branch (`fix/ody-s05-104-reference-ruleset-compatibility`) from an up-to-date `main`, verified the diff touches only this task's own 5 allowed files, re-ran the full required validation suite, and opened a new Draft PR explicitly stating it is a follow-up to #108.

### Known limitations

- `ArmorDefinition.CoveredBodyPartIds`/`AbilityResourceCost.ResourceDefinitionId` are validated only for structural (regex) validity, already guaranteed by their own domain constructors -- no Ruleset-wide anatomy-profile/resource registry exists anywhere in this codebase to check their existence against (`BodyPartId`/`ResourceDefinitionId` are SLICE-04's own fixture-only, per-Character/per-Ruleset keys with no backing catalog table). This is an honestly recorded MVP boundary, not an oversight; closing it is a future, separately-scoped decision if the product owner ever introduces a real Ruleset-wide Resource/BodyPart catalog.
- `MechanicsPayloadRef` is validated only as a structurally-acceptable opaque reference (non-null implies non-blank) -- no real `ContentBlockGraph` DAG/cycle/unsupported-operation-name validation exists anywhere in this codebase yet to run (`11_Content_Block_System` section 8/25's own full static-validation checklist is not yet implementable). This boundary is explicitly validated and tested (`TC-CATALOG-059`/`060`), not silently skipped.
- Weapon/ammo compatibility matching is a plain string key overlap (`ODY-S05-105`'s own design), not a Ruleset-aware category taxonomy -- exact string equality is the only compatibility rule this MVP implements.

### Follow-up tasks

- `ODY-S05-103` -- Publish/Archive/Delete Lifecycle (the direct consumer of `ValidateDraftForPublish` as its own publish gate).
- `ODY-S05-106` -- minimal test-catalog fixtures (proves the full Foundation/Authoring/Validation/Publish pipeline end-to-end).

### Self-review summary

- Scope review: diff limited to the six files in section 5's Allowed paths; no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`-`027` file, no Unity file, no `ODY-S05-101`/`102`/`105`'s own foundation files touched.
- Architecture review: pure Application-layer, read-only service; no new persistence table/column; reuses `TypedDefinitionCodec`/`IContentCatalogRepository` exactly as they already exist.
- Test review: 30 new tests, all passing on first run; full-suite `dotnet test` and remaining validation scripts to be run before PR.
- Security/privacy review: no new authorization surface; every issue message is a `UserMessageKey`, never raw content; malformed-input handling reuses the codec's own existing safety net.
- Documentation/version review: `test-catalog.json` updated; `ERROR_CODES.md` deliberately NOT touched (no new `ErrorCode`); no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-04 — Decision: represent validation findings as a plain `CatalogValidationIssueCode` enum, not registered `ErrorCode`s. Authority: `docs/errors/ERROR_CODES.md`'s own registry governs `Error`/`Result.Failure` outcomes of an *operation*; a validation run that finds usability issues is still a successful `Result<CatalogValidationResult>.Success` carrying a structured issue list, not an operation failure -- introducing 13 new registry rows for values that never appear as an `Error.Code` would misuse the registry's own purpose and add unnecessary `check-repository-policy.ps1` surface for no safety benefit.
- 2026-09-04 — Decision: no new repository method for "exact version lookup" -- reuse the existing `GetContentDefinition` and compare its returned `Version` field directly against the requested `ContentDefinitionRef.Version`. Authority: `SqliteContentCatalogRepository` (read in full) keeps exactly one row per `ContentDefinitionId` with no historical-version table; since no `PublishDefinition` command exists yet, a definition's own current `Version` *is* the only version this repository can ever report, making a separate "by exact version" method redundant for this MVP -- matches the ТЗ's own explicit "add the smallest read helper needed... if not directly supported" instruction by adding none, since the existing primitive already suffices.
- 2026-09-04 — Decision: `ArmorDefinition.CoveredBodyPartIds`/`AbilityResourceCost.ResourceDefinitionId` are validated only structurally (regex-shape, already ctor-guaranteed), not against a live Ruleset-wide registry. Authority: `Anatomy.cs`/`Resource.cs` (read in full) confirm both types are SLICE-04's own fixture-only, per-Character/per-Ruleset keys with no backing catalog table anywhere in this codebase -- there is nothing to check existence against without inventing a new registry, which is explicitly out of this task's own scope ("new typed definition fields beyond what validation minimally needs").
- 2026-09-04 — Decision: `MechanicsPayloadRef` is validated only as a structurally-acceptable opaque reference (non-null implies non-blank), not against a real `ContentBlockGraph`. Authority: this task's own ТЗ explicit "ContentBlock / mechanics payload MVP" section allowing exactly this -- "validate it as an opaque known/allowed reference or explicitly record that no full graph exists yet" -- confirmed by `11_Content_Block_System` section 8 that no such graph implementation exists anywhere in this codebase.
- 2026-09-04 — Decision: dependency-cycle test fixtures use direct SQL writes (`MarkPublishedDirectly`, including `PropertiesJson`/`DependencyRefsJson`) to construct a Published definition with a specific exact `Version` and cross-references, mirroring `ODY-S05-101`/`102`'s own `MarkStatusDirectly` convention -- no `PublishDefinition`/authoring-update-for-DependencyRefs command exists yet to construct these states through the public API.

### Approved task changes

- 2026-09-04 — Product-owner-requested amendment to the already-open PR #108: check the candidate `AmmoDefinition`'s own `RulesetCompatibility` when determining weapon ammo applicability (see the first Amendment note in section 17). Scope stayed within this task's own Allowed paths (`CatalogValidationContracts.cs`, `CatalogValidationServiceTests.cs`, `test-catalog.json`, this contract/plan) -- no new file, no new `ErrorCode`.
- 2026-09-04 — Second product-owner-requested amendment to the same PR #108: check every referenced definition's own `RulesetCompatibility` inside `ValidateReferencesAndCycles`, not only existence/version/type (see the second Amendment note in section 17). Same scope constraints as the first amendment -- no new file, no new `ErrorCode` (reused `RulesetIncompatible`, with `FieldPath` pinned to the exact reference).
- 2026-09-04 — Follow-up: PR #108 was merged before this second amendment's own commit (`742bae0`) reached `main` (see the second Amendment note's own "Delivery note" in section 17). Recovered via a new branch (`fix/ody-s05-104-reference-ruleset-compatibility`) cherry-picking `742bae0` from an up-to-date `main`, with a fresh Draft PR explicitly stated as a follow-up to #108. No product scope change -- identical diff to the orphaned commit.
