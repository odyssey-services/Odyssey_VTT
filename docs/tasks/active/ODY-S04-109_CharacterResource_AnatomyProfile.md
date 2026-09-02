# ODY-S04-109 — CharacterResource & AnatomyProfile

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-109-character-resource-anatomy`
**Pull request:** [#93](https://github.com/odyssey-services/Odyssey_VTT/pull/93)
**ExecPlan:** `docs/plans/active/ODY-S04-109_CharacterResource_AnatomyProfile.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-02 UTC

## 1. Goal

Implement product section 17 (`CharacterResource` — computed `EffectiveMaximum`, typed `RecoveryRule`, maximum-decrease-clamps-current with no automatic restore) and section 18 (`CharacterAnatomy` — an independent per-Character snapshot, journaled modifications, `RemoveBodyPart`'s dependency preview bounded to what this codebase can actually check). Two independently-revisioned sections of deliberately different shapes: `CharacterResources` (multi-entry, mirrors `CharacterAbilities`) and `CharacterAnatomy` (single snapshot object, mirrors `Ownership`/`Lifecycle`).

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-109` as the ninth implementation task, depending only on `ODY-S04-101` (the aggregate skeleton reserving both sections). It is the first task to introduce two independent new sections at once, and the first where one of them (`CharacterAnatomy`) is a single-object section rather than a collection.
- Value or risk reduction: proves ADR-022's section-revision model handles a genuinely different section shape correctly (single-object vs. collection) without inventing a third mechanism; proves the maximum-decrease-clamps-current invariant structurally, not just by convention.
- Blocking or enabling relationship: unblocks `ODY-S04-110` (Archive & Dependency-Aware Physical Delete) and a future Item/Inventory task, which would be the first real occasion to extend `RemoveBodyPart`'s dependency check.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 9) — the binding scope definition for this task.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §17 (full read — `CharacterResource`, §17.1 maximum decrease, §17.2 `RecoveryRule`), §18 (full read — Anatomy), §20.1 (locks), requirements 41–51.
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §5–6 (`CharacterResourcesRevision`/`CharacterAnatomyRevision`, lock keys `CharacterResource:<id>` vs. un-parameterized `CharacterAnatomy`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`ODY-S04-101`–`108`'s own code) — read in full as the binding structural precedent, especially `MutateAbilities`/`MutateOwnership`.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterTemplateAndDraftBindingTests.cs`'s `UpdateCharacterTemplate_AfterBind_DoesNotChangeAlreadyCreatedCharacter` (`ODY-S04-103`) — the pattern for this task's own snapshot-independence test.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-109`, product section 17/18, requirements 41–51, `ADR-022` §5–6.
- Existing test IDs reused: None directly reused. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-092` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-093` through `TC-CHAR-114` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`; `git merge-base --is-ancestor` independently confirmed PR #92's merge commit is a real ancestor of `origin/main` before branching.
- `Character` table already carries real `CharacterResourcesRevision`/`CharacterAnatomyRevision` columns (`ADR-022` §5, present from `ODY-S04-101` onward) that no prior task ever incremented — confirmed by `Grep`.
- No `ResourceDefinition`/`AnatomyProfileDefinition` catalog exists anywhere in this codebase — confirmed by `Grep`.
- No Item/Inventory system exists anywhere in this codebase — confirmed by `Grep`.
- `ADR-022` §6 reserves the un-parameterized `CharacterAnatomy` lock key (not `CharacterAnatomy:<id>`), confirming its single-object shape is intentional, not an oversight.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Character/Resource.cs` (new) — `ResourceDefinitionId`, `RecoveryRule`, `CharacterResource`.
- `Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs` (new) — `AnatomyProfileDefinitionId`, `BodyPartId`, `BodyPart`, `PermanentModification`, `AnatomyMigrationEntry`, `CharacterAnatomy`.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `CharacterResourceId`, `PermanentModificationId` (additive).
- `Packages/com.odyssey.rules/Runtime/Character/ResourceInitializationRules.cs` (new), `AnatomyInitializationRules.cs` (new) — explicitly-flagged test fixtures.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `CharacterRecord.Resources`/`Anatomy`; nine new `ICharacterRepository` methods.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — nine new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — nine new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — `ResourcesJson`/`AnatomyJson` columns, serialize/deserialize helpers, `WithRevisions` extension, `MutateResources`/`MutateAnatomy`, nine command implementations, mechanical `CharacterRecord` construction-site updates for the two new parameters.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterResourceAnatomyTests.cs` (new) — 22 tests.
- `docs/errors/ERROR_CODES.md` (edit) — nine new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-093`–`114`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 9 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- `CharacterAbility` — already `ODY-S04-108`.
- Real item-dependency checking for `RemoveBodyPart` — no Item system exists; documented stub only (section 1.3 of this task's own ТЗ).
- Automatic resource recovery on any timer/scene/session trigger — only the explicit command.
- Archive/delete, Dead/restore, `.odchar`, Ruleset migration — `ODY-S04-110`–`113`.
- Concrete `ResourceDefinition`/`AnatomyProfileDefinition` catalogs — this task uses explicitly-flagged minimal test fixtures only.
- Any Unity/UI code — this task is purely Domain/Rules/Application/Persistence.
- Any change to `ADR-022` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.
- Any edit to already-merged `ODY-S04-101`–`108` files' own logic (unlike `107`/`108`, this task requires no retroactive fix).

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Character/Resource.cs
Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.rules/Runtime/Character/ResourceInitializationRules.cs
Packages/com.odyssey.rules/Runtime/Character/AnatomyInitializationRules.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterResourceAnatomyTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-109_CharacterResource_AnatomyProfile.md
docs/plans/active/ODY-S04-109_CharacterResource_AnatomyProfile.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns `CharacterResource`/`CharacterAnatomy` and their id types (no serializer, no Unity/SQLite reference); `Odyssey.Rules` owns the initialization fixtures; `Odyssey.Application` owns the repository port extension; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: `MutateResources` (multi-entry, mirrors `MutateAbilities`) and `MutateAnatomy` (single-object, mirrors `MutateOwnership`) each commit through the existing, unmodified `SqliteSavingPipeline`. `CommandId`/`AppliedCommands` remain the sole idempotency mechanism for every command in this task.
- Serialization / compatibility boundary: `ResourcesJson`/`AnatomyJson` use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`108` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the nine new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`108`'s own established pattern.
- Other: every resource and anatomy command in this task is MainGM-only — product section 18 explicitly says "GM может" for anatomy; resource commands follow the same default this task's own ExecPlan documents.

## 7. Expected behavior

### Scenario 1 — `CharacterResourcesRevision`/`CharacterAnatomyRevision` genuinely increment

**Given** a freshly-created Character
**When** `InitializeCharacterResource`/`InitializeCharacterAnatomy` is called
**Then** the respective section revision increases by exactly 1, verified by direct value comparison, not merely command success.

### Scenario 2 — maximum decrease clamps current value (requirement 44)

**Given** a `CharacterResource` at full `EffectiveMaximum`
**When** `SetResourceMaximum` lowers `EffectiveMaximum` below the current `CurrentValue`
**Then** `CurrentValue` is clamped to the new `EffectiveMaximum` in the same commit.

### Scenario 3 — a later maximum increase does not restore the clamped value (requirement 45)

**Given** a resource whose `CurrentValue` was clamped by scenario 2
**When** `SetResourceMaximum` raises `EffectiveMaximum` back to (or above) the original value
**Then** `CurrentValue` remains at the clamped value.

### Scenario 4 — `CharacterAnatomy` is an independent snapshot (requirements 48–49)

**Given** an initialized `CharacterAnatomy`
**When** the source fixture is consulted again (as a future definition edit would)
**Then** the Character's own already-initialized `AnatomyProfileVersion`/`BodyParts` are unaffected — pinned at initialization time, mirroring `ODY-S04-103`'s own template-independence pattern.

### Scenario 5 — `RemoveBodyPart`'s bounded dependency preview (requirements 50–51)

**Given** a body part another body part or permanent modification is attached to
**When** `RemoveBodyPart` is called on it
**Then** it is rejected with `CharacterBodyPartHasDependent`, no state change; an independent body part removes cleanly. Item-system dependencies are never checked — documented stub, no such system exists.

### Required invariants

- A `CharacterResource`'s `CurrentValue` can never be constructed outside `[MinimumValue, EffectiveMaximum]`.
- `CharacterAnatomy.MigrationHistory` gains exactly one entry per anatomy-mutating command.
- Every resource/anatomy command is MainGM-only.
- No `ADR-022` file content changes; no already-merged `101`–`108` file's own logic changes.

## 8. Deliverables

- Production code: `Resource.cs`/`Anatomy.cs`/`DomainIdentity.cs` (Domain), `ResourceInitializationRules.cs`/`AnatomyInitializationRules.cs` (Rules), `CharacterRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence).
- Tests: 22 new tests in `CharacterResourceAnatomyTests.cs`, registered as `TC-CHAR-093`–`114`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `ResourcesJson`/`AnatomyJson` columns only.

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-092` continue passing with their own assertions unmodified.
2. `InitializeCharacterResource`/`InitializeCharacterAnatomy` genuinely increment their own section revision.
3. Requirement 44: maximum decrease clamps `CurrentValue` immediately.
4. Requirement 45: a later maximum increase does not auto-restore the clamped value.
5. Requirements 46–47: `CurrentValue` changes only via the explicit `SetResourceCurrentValue` command.
6. Requirements 48–49: `CharacterAnatomy` is an independent snapshot.
7. Requirements 50–51: `RemoveBodyPart`'s dependency preview rejects an internally-dependent part, succeeds for an independent one, and never invents an item-dependency check.
8. `MigrationHistory` accumulates one entry per anatomy command.
9. Duplicate `CommandId` for one resource and one anatomy command does not duplicate effect.
10. A concurrent `CharacterResources`/`CharacterAnatomy` edit (or either against another section) commits without a false conflict.
11. No change to `ADR-022`/`SLICE-04_BACKLOG.md`; no Unity/UI code; no already-merged `101`–`108` file's own logic touched.
12. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
13. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 9 marked `Done` with a real PR link.
14. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-093`–`100` | .NET (`Odyssey.Tests.Persistence`) | CharacterResource: init/revision, permission, bounds, clamp, no-auto-restore, not-found, duplicate-CommandId | Pass |
| `TC-CHAR-101`–`114` | .NET (`Odyssey.Tests.Persistence`) | CharacterAnatomy: init/revision, already-initialized, independence, AddBodyPart/RemoveBodyPart (dependency, not-found, already-exists)/UpdateBodyPart/ReplaceAnatomyProfile/ApplyPermanentModification, MigrationHistory, duplicate-CommandId, concurrent-section no-conflict | Pass |

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`108`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — two new columns on `Character` (`ResourcesJson`, `AnatomyJson`); the existing `CharacterResourcesRevision`/`CharacterAnatomyRevision` columns are now genuinely written, previously always `1`.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new columns only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new columns are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: resource current/maximum values, anatomy body-part/modification metadata — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: every command in this task is MainGM-only.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm`, matching existing conventions exactly.
- Redaction requirements: the nine new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only resource/body-part/actor/outcome fields — no secret data.
- Abuse / malformed input limits: `ResourceDefinitionId`/`AnatomyProfileDefinitionId`/`BodyPartId` validated against safe identifier patterns; `Name`/`Description`/`Kind` length-bounded.
- Security tests: MainGM gate exercised directly (`InitializeCharacterResource_ByNonMainGm_IsRejected`, `AddBodyPart_RequiresInitializedAnatomy_MainGmOnly`).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 9 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract, introduces new persisted schema, and implements two new independent section shapes.
- ExecPlan path: `docs/plans/active/ODY-S04-109_CharacterResource_AnatomyProfile.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101` (done). Unblocks `ODY-S04-110`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character.ResourcesJson`/`AnatomyJson` columns; no versioned schema migration.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

See section 5's "In scope" file list above.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 188 (169 pre-existing + 22 new) — 419 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Every earlier test file unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-093`/`101`. |
| AC-3 | Passed | `TC-CHAR-097`. |
| AC-4 | Passed | `TC-CHAR-098`. |
| AC-5 | Passed | `TC-CHAR-095`/`096`. |
| AC-6 | Passed | `TC-CHAR-103`. |
| AC-7 | Passed | `TC-CHAR-104`–`109`. |
| AC-8 | Passed | `TC-CHAR-112`. |
| AC-9 | Passed | `TC-CHAR-100`/`113`. |
| AC-10 | Passed | `TC-CHAR-114`. |
| AC-11 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**`/already-merged `101`–`108` file touched. |
| AC-12 | Passed | See Validation results above. |
| AC-13 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 9 status/PR link updated. |
| AC-14 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `ResourceInitializationRules`/`AnatomyInitializationRules` are explicitly-flagged flat test fixtures, not production Ruleset content.
- `RemoveBodyPart`'s dependency preview never checks item dependencies — no Item system exists yet; a future Item/Inventory task must extend it explicitly.
- All resource/anatomy commands are MainGM-only by this task's own default decision, mirroring `ODY-S04-108`'s own precedent for undecided permission surfaces.

### Follow-up tasks

- `ODY-S04-110` — Archive & Dependency-Aware Physical Delete.
- A future Item/Inventory task — first real occasion to extend `RemoveBodyPart`'s dependency check with the item-dependency half this task stubbed.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`SLICE-04_BACKLOG.md` change; no Unity/UI code; no production Ruleset content authored; no already-merged `101`–`108` file's own logic touched.
- Architecture review: `CharacterResources`/`CharacterAnatomy` deliberately modeled with two different shapes per `ADR-022` §6's own lock-key reservations, each mirroring the correct existing precedent (`MutateAbilities`/`MutateOwnership` respectively) rather than forcing one shape onto both.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; the clamp/no-restore invariant and the dependency-preview boundary are exercised for real, not simulated.
- Security/privacy review: every command's MainGM gate reuses/extends existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-02 — Decision: `CharacterResource`'s constructor structurally enforces `CurrentValue ∈ [MinimumValue, EffectiveMaximum]` — Authority/approval: strengthens requirement 44/45 beyond a command-side convention.
- 2026-09-02 — Decision: `MutateResources`/`MutateAnatomy` each mirror a different existing precedent (`MutateAbilities`/`MutateOwnership`) — Authority/approval: ТЗ §1.1/§1.2's own explicit instruction.
- 2026-09-02 — Decision: `RemoveBodyPart`'s item-dependency check is a documented stub — Authority/approval: ТЗ §1.3's own explicit instruction; confirmed by search that no Item system exists.
- 2026-09-02 — Decision: `SetResourceCurrentValue` is one command for both damage and recovery; `UpdateBodyPart` folds two product-listed actions into one; `ApplyPermanentModification` is one generic command for three product-named kinds — Authority/approval: this task's own code-quality judgment, avoiding near-duplicate single-purpose commands.
- 2026-09-02 — Decision: `BodyPartId` is a catalog-style string, not a canonical instance id — Authority/approval: a body part is a stable named structural slot, not a randomly-created purchase instance.

### Approved task changes

- None.
