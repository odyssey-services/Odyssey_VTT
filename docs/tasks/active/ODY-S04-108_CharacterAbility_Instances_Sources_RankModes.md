# ODY-S04-108 — CharacterAbility Instances, Sources & Rank Modes

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-108-character-ability-instances`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-108_CharacterAbility_Instances_Sources_RankModes.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-02 UTC

## 1. Goal

Section 1 (mandatory, before any new functionality): make `RevertAdvancementPurchase`/`ApplyCharacterRespec`/`ComputeRespecPlan`'s `AdvancementOperationKind` branching exhaustive, so the new third enum value (`AbilityAcquisition`) this task introduces cannot silently mis-parse an ability's `TargetDefinitionId` as a `SkillDefinitionId`. Then implement product section 16: `AbilityDefinition`/`CharacterAbility` split, `AcquireAbility` for all six `SourceKind` values, `RemoveAbility` (legality gated by `SourceKind`), `RankMode` (`None`/`Numeric`/`Named`) validated independently per mode.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-108` as the eighth implementation task, depending on `ODY-S04-101` (the reserved `CharacterAbilitiesRevision` section) and `ODY-S04-105` (ability-via-purchase spends `DevelopmentPool`). This task's own preparation discovered a real defensive gap in already-merged `ODY-S04-107` code that had to be closed first.
- Value or risk reduction: proves `ADR-022`'s `CharacterAbilities` section is a real, independently-revisioned section (not routed through `Mechanics` the way `ODY-S04-105`/`106` chose for ledger data); proves the slice's first genuine cross-section (two independently-gated revisions in one transaction) command.
- Blocking or enabling relationship: unblocks `ODY-S04-109` (`CharacterResource`/`AnatomyProfile`, the next backlog task) and a future Item/Inventory/ActiveEffect task, which would become the first real caller of `AcquireAbility`'s `Item`/`ActiveEffect` `SourceKind` values.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 8) — the binding scope definition for this task.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §16 (full read — `AbilityDefinition`/`CharacterAbility`, `SourceKind`, `RankMode`).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §5–6 (`CharacterAbilities` section, `CharacterAbility:<CharacterAbilityId>` lock key).
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` §5.1/§9 (`AcquireAbility` purchase pipeline, module boundaries).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`ODY-S04-101`–`107`'s own code) — read in full as the binding structural precedent, especially `MutateOwnership`/`MutateMechanics`/`ApplyCharacterRespec` and both existing `AdvancementOperationKind` branch sites.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-108`, product section 16, `ADR-022` §5–6, `ADR-024` §5.1/§9.
- Existing test IDs reused: None directly reused; the `ProgressionPurchase` permission gate reuses `ODY-S04-102`'s `CharacterOwnershipAssignment.IsAssignedCharacter` production code (not its tests). All pre-existing `TC-CHAR-*` tests through `TC-CHAR-071` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-072` through `TC-CHAR-092` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`; `git merge-base --is-ancestor` independently confirmed PR #91's merge commit is a real ancestor of `origin/main` before branching.
- `Character` table already carries a real `CharacterAbilitiesRevision` column (`ADR-022` §5, present from `ODY-S04-101` onward) that no prior task ever incremented — confirmed by `Grep`.
- `RevertAdvancementPurchase`/`ComputeRespecPlan`/`ApplyCharacterRespec` (`ODY-S04-107`, already merged) branch on `OperationKind` with a plain two-way `if/else` at five call sites — confirmed by direct read.
- No Item/Inventory/ActiveEffect/template-copy system exists anywhere in this codebase — confirmed by `Grep` — so four of `AcquireAbility`'s six `SourceKind` values have no real caller yet.
- No ability-cost catalog exists anywhere in this codebase — confirmed by `Grep`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Character/AdvancementPurchase.cs` (edit) — `AdvancementOperationKind.AbilityAcquisition` (additive).
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `CharacterAbilityId` (additive).
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs` (new) — `AbilityDefinitionId`, `SourceKind`, `RankMode`, `CharacterAbility`.
- `Packages/com.odyssey.rules/Runtime/Character/AbilityCostRules.cs` (new) — explicitly-flagged flat test fixture.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `CharacterRecord.Abilities`; `ICharacterRepository.AcquireAbility`/`RemoveAbility`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — four new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — four new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — `AbilitiesJson` column, `SerializeAbilities`/`DeserializeAbilities`, `WithRevisions` extension, `MutateAbilities`, `AcquireAbilityViaProgressionPurchase`, `AcquireAbility`, `RemoveAbility`; the section-1 exhaustiveness fix to `RevertAdvancementPurchase`/`ComputeRespecPlan`/`ApplyCharacterRespec`; every existing `CharacterRecord` construction call site updated for the new `Abilities` parameter (mechanical, no behavior change).
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterAbilityInstancesTests.cs` (new) — 24 tests (22 methods, one parameterized ×4).
- `docs/errors/ERROR_CODES.md` (edit) — four new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-072`–`092`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 8 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Automatic ability creation/removal from equip/unequip or effect on/off — no Item/Inventory/ActiveEffect system exists.
- Real revert/respec support for `AbilityAcquisition` purchases — only the defensive explicit rejection (section 1).
- Reusing `BindDraftToCampaign` for template ability copy.
- `CharacterResource`/`AnatomyProfile` — `ODY-S04-109`.
- Archive/delete, Dead/restore, `.odchar`, Ruleset migration — `ODY-S04-110`–`113`.
- Concrete ability catalog/costs — this task uses an explicitly-flagged minimal test fixture only.
- Any Unity/UI code — this task is purely Domain/Rules/Application/Persistence.
- Any change to `ADR-022`/`024` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Character/AdvancementPurchase.cs
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/Ability.cs
Packages/com.odyssey.rules/Runtime/Character/AbilityCostRules.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterAbilityInstancesTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-108_CharacterAbility_Instances_Sources_RankModes.md
docs/plans/active/ODY-S04-108_CharacterAbility_Instances_Sources_RankModes.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns `AbilityDefinitionId`/`CharacterAbilityId`/`CharacterAbility`/`SourceKind`/`RankMode` (no serializer, no Unity/SQLite reference); `Odyssey.Rules` owns `AbilityCostRules`; `Odyssey.Application` owns the repository port extension; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: `RemoveAbility`/non-`ProgressionPurchase` `AcquireAbility` commit through the new `MutateAbilities` helper (mirrors `MutateOwnership`'s single-section shape); `AcquireAbility(ProgressionPurchase)` opens its own dedicated transaction via `_pipeline.Execute`, checking both `MechanicsRevision` and `CharacterAbilitiesRevision` independently per `ADR-022` §5 rule 2 (a command depending on several sections lists all required section revisions), and commits the pool decrement, the new `AdvancementPurchase`, and the new `CharacterAbility` atomically. `CommandId`/`AppliedCommands` remain the sole idempotency mechanism for every command in this task.
- Serialization / compatibility boundary: `AbilitiesJson` uses `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`107` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`107`'s own established pattern.
- Other: `AcquireAbility(ProgressionPurchase)` reuses `PurchaseAttributeIncrease`/`PurchaseSkillLevel`'s own MainGM-or-assigned-user permission convention; every other `SourceKind` (including the four with no real caller yet) is MainGM-only, matching `GMGrant`'s own product-specified gate.

## 7. Expected behavior

### Scenario 1 — section 1's exhaustiveness fix

**Given** an `AdvancementPurchase` (or a `CharacterRespecTarget`) with `OperationKind=AbilityAcquisition`
**When** `RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec` is called against/with it
**Then** it is rejected with `CharacterAdvancementOperationKindNotSupported` — never the misleading `CharacterAdvancementPurchaseHasDependent`, and `TargetDefinitionId` is never mis-parsed as a `SkillDefinitionId`.

### Scenario 2 — `AcquireAbility(ProgressionPurchase)` is a real cross-section transaction

**Given** sufficient `DevelopmentPool.Available` and matching `Mechanics`/`CharacterAbilities` expected revisions
**When** `AcquireAbility` is called with `SourceKind=ProgressionPurchase`
**Then** the pool decreases by the fixture cost, a `CharacterAbility` is created, an `AdvancementPurchase` (`OperationKind=AbilityAcquisition`) is created, and both `MechanicsRevision`/`CharacterAbilitiesRevision` increase — atomically, in one transaction.

### Scenario 3 — `AcquireAbility(GMGrant)` touches only `CharacterAbilities`

**Given** a MainGM actor
**When** `AcquireAbility` is called with `SourceKind=GMGrant`
**Then** a `CharacterAbility` is created, `CharacterAbilitiesRevision` increases, `DevelopmentPool` is unchanged, and no `AdvancementPurchase` is created.

### Scenario 4 — `RankMode` validated independently per mode

**Given** any combination of `RankMode`/`NumericRank`/`NamedRankKey`
**When** a `CharacterAbility` is constructed
**Then** `None` requires both null, `Numeric` requires only `NumericRank` set, `Named` requires only `NamedRankKey` set — any other combination throws.

### Scenario 5 — `RemoveAbility` legality by `SourceKind`

**Given** an existing `CharacterAbility`
**When** `RemoveAbility` is called
**Then** it succeeds only for `SourceKind=Item`/`ActiveEffect`; every other `SourceKind` is rejected with `CharacterAbilityRemovalNotAllowed`, no state change.

### Required invariants

- `RevertAdvancementPurchase`/`ApplyCharacterRespec`/`ComputeRespecPlan` never mis-parse `TargetDefinitionId` for an unsupported `OperationKind`.
- `CharacterAbilitiesRevision` increases on every committed `AcquireAbility`/`RemoveAbility`, and only on those.
- A permanent purchased/granted ability is never removed by `RemoveAbility`.
- No `ADR-022`/`024` file content changes.

## 8. Deliverables

- Production code: `AdvancementPurchase.cs`/`DomainIdentity.cs`/`Ability.cs` (Domain), `AbilityCostRules.cs` (Rules), `CharacterRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence).
- Tests: 24 new tests (22 methods) in `CharacterAbilityInstancesTests.cs`, registered as `TC-CHAR-072`–`092`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `AbilitiesJson` column only; `CharacterAbilitiesRevision` was already a real column.

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-071` continue passing with their own assertions unmodified.
2. `RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec` reject `AbilityAcquisition` explicitly, verified against the specific error code (not the misleading dependent-purchase one).
3. `AcquireAbility(ProgressionPurchase)` succeeds/rejects correctly on balance, creates the ability + `AdvancementPurchase` atomically, is `CommandId`-idempotent.
4. `AcquireAbility(GMGrant)` is MainGM-only, touches only `CharacterAbilities`, creates no `AdvancementPurchase`.
5. `CharacterAbilitiesRevision` genuinely increments on every `AcquireAbility`/`RemoveAbility`, verified by direct value check.
6. `RankMode` validated independently per mode (four rejection shapes, two success shapes).
7. `RemoveAbility` legal only for `Item`/`ActiveEffect`, not-found handled, `CommandId`-idempotent.
8. A concurrent `CharacterAbilities` edit and `Mechanics` edit commit without a false conflict.
9. No change to `ADR-022`/`024` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
10. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
11. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 8 marked `Done` with a real PR link.
12. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-072`–`075` | .NET (`Odyssey.Tests.Persistence`) | AcquireAbility(ProgressionPurchase): balance, AdvancementPurchase creation, duplicate-CommandId | Pass |
| `TC-CHAR-076`–`077` | .NET (`Odyssey.Tests.Persistence`) | AcquireAbility(GMGrant): permission, no pool/purchase change | Pass |
| `TC-CHAR-078` | .NET (`Odyssey.Tests.Persistence`) | CharacterAbilitiesRevision genuinely increments | Pass |
| `TC-CHAR-079`–`084` | .NET (`Odyssey.Tests.Persistence`) | RankMode validated independently per mode | Pass |
| `TC-CHAR-085`–`089` | .NET (`Odyssey.Tests.Persistence`) | RemoveAbility legality by SourceKind, not-found, duplicate-CommandId | Pass |
| `TC-CHAR-090`–`091` | .NET (`Odyssey.Tests.Persistence`) | Section 1 regression: Revert/Preview/Apply reject AbilityAcquisition explicitly | Pass |
| `TC-CHAR-092` | .NET (`Odyssey.Tests.Persistence`) | Concurrent CharacterAbilities + Mechanics edit, no false conflict | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`107`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — one new column on `Character` (`AbilitiesJson`); the existing `CharacterAbilitiesRevision` column is now genuinely written, previously always `1`.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new column only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new column is simply unused by any other code path if reverted, and `CharacterAbilitiesRevision` reverts to its prior always-`1` state.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: ability acquisition/removal metadata, source provenance (item/effect instance refs) — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `AcquireAbility(ProgressionPurchase)` is MainGM-or-assigned-user; every other `SourceKind` and `RemoveAbility` are MainGM-only.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` and `CharacterOwnershipAssignment.IsAssignedCharacter` reused, matching existing conventions exactly.
- Redaction requirements: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only ability/source/actor/outcome fields — no secret data.
- Abuse / malformed input limits: `AbilityDefinitionId` validated against a safe identifier pattern; `Configuration` validated non-null.
- Security tests: MainGM gate exercised directly (`AcquireAbility_GMGrant_ByNonMainGm_IsRejected`).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 8 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract, introduces new persisted schema, and implements the slice's first genuine cross-section transaction.
- ExecPlan path: `docs/plans/active/ODY-S04-108_CharacterAbility_Instances_Sources_RankModes.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101`/`105`/`107` (done). Unblocks `ODY-S04-109`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character.AbilitiesJson` column; no versioned schema migration.
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
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 168 (145 pre-existing + 24 new) — 399 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `CharacterAdvancementRevertRespecTests.cs` and every earlier test file unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-090`/`091`. |
| AC-3 | Passed | `TC-CHAR-072`–`075`. |
| AC-4 | Passed | `TC-CHAR-076`/`077`. |
| AC-5 | Passed | `TC-CHAR-078`. |
| AC-6 | Passed | `TC-CHAR-079`–`084`. |
| AC-7 | Passed | `TC-CHAR-085`–`089`. |
| AC-8 | Passed | `TC-CHAR-092`. |
| AC-9 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-10 | Passed | See Validation results above. |
| AC-11 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 8 status/PR link updated. |
| AC-12 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `AbilityCostRules.CostPerAbility` is an explicitly-flagged flat test fixture, not production Ruleset balance data.
- `AcquireAbility`'s four "no real caller yet" `SourceKind` values are gated MainGM-only by this task's own default decision — a future Item/Inventory/ActiveEffect/template-copy task should revisit this explicitly once it exists.
- Reverting/respeccing an `AbilityAcquisition` `AdvancementPurchase` is explicitly unsupported (rejected, not implemented) — a future task would need to extend `RevertAdvancementPurchase`/`ApplyCharacterRespec` deliberately, not merely remove the new guard.

### Follow-up tasks

- `ODY-S04-109` — `CharacterResource` & `AnatomyProfile`.
- A future Item/Inventory/ActiveEffect task — first real caller of `AcquireAbility`'s `Item`/`ActiveEffect` `SourceKind` values; should revisit the MainGM-only default decision above.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`024` or `SLICE-04_BACKLOG.md` change; no Unity/UI code; no production ability catalog authored.
- Architecture review: the section-1 exhaustiveness fix was completed and verified regression-free before any new ability code was written, per this task's own explicit ordering instruction; `AcquireAbility(ProgressionPurchase)` is the slice's first genuine two-section transaction, isolated to its own dedicated method rather than forcing a third generalization onto either single-section helper.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; the section-1 regression, `CharacterAbilitiesRevision` increment, and duplicate-`CommandId` idempotency (both commands) are exercised for real, not simulated.
- Security/privacy review: every gate reuses/extends existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-02 — Decision: fix ODY-S04-107's `AdvancementOperationKind` exhaustiveness gap before any new ability code — Authority/approval: this task's own explicit §1.3 instruction and ordering.
- 2026-09-02 — Decision: `ComputeRespecPlan` returns `Result<CharacterRespecPreview>` instead of a bare value — Authority/approval: matches this codebase's own established `Result<T>`-not-exceptions convention for expected-but-invalid input.
- 2026-09-02 — Decision: `AcquireAbility(ProgressionPurchase)` gets its own dedicated cross-section method rather than extending either single-section helper — Authority/approval: `ApplyCharacterRespec`'s own established precedent (`ODY-S04-107`) for a genuinely cross-cutting case.
- 2026-09-02 — Decision: `CharacterAbilitiesRevision` is genuinely incremented for real, unlike `ODY-S04-105`/`106`'s own `AttributeValuesRevision`/`CharacterSkillsRevision` choice — Authority/approval: `ADR-024` §4.1/4.2's justification is specific to pool ledger data and does not extend to abilities.
- 2026-09-02 — Decision: `AcquireAbility`'s four no-real-caller-yet `SourceKind` values are gated MainGM-only, matching `GMGrant` — Authority/approval: this task's own engineering judgment in the absence of any real caller to validate a narrower gate against.

### Approved task changes

- None.
