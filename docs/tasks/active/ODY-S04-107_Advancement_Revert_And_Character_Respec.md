# ODY-S04-107 — Advancement Revert & CharacterRespec

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-107-advancement-revert-respec`
**Pull request:** [#91](https://github.com/odyssey-services/Odyssey_VTT/pull/91)
**ExecPlan:** `docs/plans/active/ODY-S04-107_Advancement_Revert_And_Character_Respec.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-02 UTC

## 1. Goal

Пункт 0 (retroactive gap fix): close the `ADR-024` §3.3/§5.1 step 4 gap — no `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/approved `ResolveAdvancementRecommendation` co-committed an `AdvancementPurchase` record in `ODY-S04-105`/`106`'s already-merged code — and extend `DomainEvents` with `ADR-012` §6's compensating-event columns, before implementing any new functionality on top of them. Then implement `ADR-024` §6.2/§7.2: `RevertAdvancementPurchase` as a compensating command with a minimal, explicitly-bounded dependency check; `PreviewCharacterRespec` (read-only Query) and `ApplyCharacterRespec`, producing an ordered batch of compensating and forward events grouped by one trailing `CharacterRespecCompleted`.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-107` as the seventh implementation task, depending on `ODY-S04-105`/`106` (reuses `MutateMechanics`, the `DevelopmentTransactionKind` values those tasks reserved in advance). This task's own preparation discovered a real, unshipped gap in the already-merged `105`/`106` code, which had to be closed first.
- Value or risk reduction: proves `ADR-012` §6's compensating-event mechanism against real persistence for the first time anywhere in the codebase; gives a GM a real, auditable way to correct or respec a Character's advancement history without ever mutating or deleting a committed event.
- Blocking or enabling relationship: `ODY-S04-110`–`113` (Ruleset migration) is expected to reuse this task's own compensating-batch pattern.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 7) — the binding scope definition for this task.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` §3.3, §6.2, §7.2 (full read).
- `docs/adr/ADR-012_Command_And_Event_Idempotency_And_Compensation_v1.0.md` §6 (full read).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §21.2/21.3 (compensation metadata).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §13.2 (`AdvancementPurchase` schema), §13.5 (`CharacterRespec`'s 8 steps).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `SqliteSavingPipeline.cs` (`ODY-S04-105`/`106`'s own code) — read in full as the binding structural precedent, especially `MutateMechanics`/`MechanicsMutation` and `DevelopmentTransactionKind`.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-107`, `ADR-024` §3.3/§6.2/§7.2, `ADR-012` §6.
- Existing test IDs reused: None directly reused; the permission gate reuses `ODY-S04-102`'s `actorIsMainGm` convention (not its tests). All pre-existing `TC-CHAR-*` tests through `TC-CHAR-058` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-059` through `TC-CHAR-071` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`; `git merge-base --is-ancestor` independently confirmed PR #90's merge commit is a real ancestor of `origin/main` before branching.
- `Grep` for `AdvancementPurchase` across `CharacterRepositoryContracts.cs`/`SqliteCharacterRepository.cs` returned nothing prior to this task — confirming the gap this task's own ТЗ discovered while preparing the spec.
- The real `DomainEvents` schema had no `OriginalEventId`/`CompensationGroupId`/`IsCompensating` columns; `ADR-012` §6's compensating mechanism has never been used anywhere in the codebase — confirmed by `Grep`.
- `DevelopmentTransactionKind` (`ODY-S04-105`'s `DevelopmentEconomy.cs`) already reserves `Refund=5`/`RespecReturn=7`/`RespecSpend=8` specifically for this task, per that enum's own doc comment — reused, not redefined.
- Cross-checking `ODY-S04-105`/`106`'s own side-table read-model layering precedent found it inconsistent: `DevelopmentTransaction` (105) has both a Domain class and an Application `DevelopmentTransactionRecord`; `CriticalSuccessEvidence`/`AdvancementRecommendation` (106) have only Application-layer `*Record` classes, no Domain-layer counterpart.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` (edit) — `DomainEvents` schema extension (`OriginalEventId`/`CompensationGroupId`/`IsCompensating`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` (edit) — `AppendDomainEvent`/`ComputeSha256Hex` made `internal`, three new optional parameters; `PipelineWrite<T>` extended.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `AdvancementPurchaseId` (additive).
- `Packages/com.odyssey.domain/Runtime/Character/AdvancementPurchase.cs` (new) — `AdvancementOperationKind`, `AdvancementPurchaseStatus`, `AdvancementPurchase`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — four new `ICharacterRepository` methods; `CharacterRespecTarget`/`CharacterRespecPlanAction`/`CharacterRespecPlanEntry`/`CharacterRespecPreview`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — five new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — five new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — `AdvancementPurchase` table + helpers, retrofits to `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`ResolveAdvancementRecommendation`, `MechanicsMutation` extension, `RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec`/`GetAdvancementPurchases`.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterAdvancementRevertRespecTests.cs` (new) — 13 tests.
- `docs/errors/ERROR_CODES.md` (edit) — five new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-059`–`071`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 7 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Ability/resource/anatomy — `ODY-S04-108`/`109`.
- Archive/delete, Dead/restore, `.odchar`, Ruleset migration — `ODY-S04-110`–`113` (Ruleset migration is expected to reuse this task's own compensating-batch pattern, not implemented here).
- The concrete dependency graph for revert-checking — Rules Engine content; only a minimal, explicitly-flagged check is implemented.
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-012`/`024` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/AdvancementPurchase.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterAdvancementRevertRespecTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-107_Advancement_Revert_And_Character_Respec.md
docs/plans/active/ODY-S04-107_Advancement_Revert_And_Character_Respec.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns `AdvancementPurchaseId`/`AdvancementPurchase` (no serializer, no Unity/SQLite reference); `Odyssey.Application` owns the repository port extension and the respec plan DTOs; `Odyssey.Persistence` owns the SQLite implementation and the `DomainEvents`/`SqliteSavingPipeline` schema extension. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: `RevertAdvancementPurchase` commits through the existing, unmodified `MutateMechanics`/`SqliteSavingPipeline`; `ApplyCharacterRespec` opens its own dedicated transaction via `_pipeline.Execute`, appending every non-final batch event directly through the now-`internal` `SqliteSavingPipeline.AppendDomainEvent` (still the identical code path every other event uses, never a duplicated `INSERT`) and only its own trailing `CharacterRespecCompleted` event through the normal single-event path. `CommandId`/`AppliedCommands` remain the sole idempotency mechanism (`ADR-024` §5, not reopened) for every command in this task.
- Serialization / compatibility boundary: event payloads use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`106` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the five new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`106`'s own established pattern.
- Other: `RevertAdvancementPurchase`/`ApplyCharacterRespec` are both MainGM-only via the same `actorIsMainGm` convention `ResolveAdvancementRecommendation` already uses; both require a non-empty `ReasonCode` (`ADR-002` §21.2).

## 7. Expected behavior

### Scenario 1 — every purchase path co-commits an `AdvancementPurchase`

**Given** a successful `PurchaseAttributeIncrease`/`PurchaseSkillLevel`, or an approved `ResolveAdvancementRecommendation`
**When** the command commits
**Then** an `AdvancementPurchase` row exists with the correct `FromValue`/`ToValue`/`Cost`/`Status=Applied`, in the same transaction; the dismiss branch of `ResolveAdvancementRecommendation` creates none.

### Scenario 2 — `RevertAdvancementPurchase` on an independent purchase

**Given** an `Applied` purchase whose addressed entry's current value still equals the purchase's own `ToValue`
**When** `RevertAdvancementPurchase` is called with a `ReasonCode`, by a MainGM
**Then** the entry returns to `FromValue`, `Available` increases by `Cost`, `Spent` decreases, the original forward event is neither deleted nor mutated, a compensating event referencing it via `OriginalEventId` is appended, and `AdvancementPurchase.Status` becomes `Reverted`.

### Scenario 3 — `RevertAdvancementPurchase` with a dependent later purchase

**Given** a later purchase has since raised the same entry's value beyond this purchase's own `ToValue`
**When** `RevertAdvancementPurchase` is called
**Then** it is rejected with `CharacterAdvancementPurchaseHasDependent`, no state change.

### Scenario 4 — `PreviewCharacterRespec` is a pure read

**Given** any Character state
**When** `PreviewCharacterRespec` is called
**Then** no `DomainEvents` row is appended, `MechanicsRevision` and the pool balance are unchanged.

### Scenario 5 — `ApplyCharacterRespec` end-to-end

**Given** several existing purchases and a set of desired target values
**When** `ApplyCharacterRespec` is called with a `ReasonCode`, by a MainGM
**Then** the plan is recomputed server-side from scratch (never trusting a client value); each undone purchase produces its own compensating event (`RespecReturn` ledger entry, `AdvancementPurchase.Status=SupersededByRespec`); each new purchase produces its own forward event (`RespecSpend` ledger entry, new `AdvancementPurchase.Status=Applied`); every batch event shares one `CompensationGroupId` and remains individually visible; exactly one trailing `CharacterRespecCompleted` event closes the batch.

### Required invariants

- No committed `DomainEvents` row is ever deleted or mutated by a compensating command.
- `RevertAdvancementPurchase`/`ApplyCharacterRespec` are MainGM-only and require a non-empty `ReasonCode`.
- `ApplyCharacterRespec` never accepts or consults a client-supplied preview value (CAP-INV-004).
- A respec batch's individual events are never collapsed into one opaque event (CAP-INV-005).
- No `ADR-012`/`024` file content changes.

## 8. Deliverables

- Production code: `SqliteCampaignRepository.cs`/`SqliteSavingPipeline.cs` schema/pipeline extension (Persistence), `AdvancementPurchase.cs` (Domain), `CharacterRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence).
- Tests: 13 new tests in `CharacterAdvancementRevertRespecTests.cs`, registered as `TC-CHAR-059`–`071`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `DomainEvents` columns and one new table only.

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-058` continue passing with their own assertions unmodified.
2. `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/approved `ResolveAdvancementRecommendation` each co-commit a correct `AdvancementPurchase`; the dismiss branch creates none.
3. `RevertAdvancementPurchase` succeeds for an independent purchase, verified against real balance/status/event state.
4. `RevertAdvancementPurchase` rejects a purchase with a dependent later purchase, no state change.
5. `RevertAdvancementPurchase` rejects a missing `ReasonCode`.
6. Duplicate `CommandId` for `RevertAdvancementPurchase` does not revert twice, verified against real balance.
7. `PreviewCharacterRespec` produces no event and no state change.
8. `ApplyCharacterRespec` end-to-end batch: individually-visible events, one trailing `CharacterRespecCompleted`.
9. `ApplyCharacterRespec` recomputes server-side from scratch, ignoring a stale preview.
10. Duplicate `CommandId` for `ApplyCharacterRespec` does not duplicate the batch.
11. No change to `ADR-012`/`024` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
12. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
13. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 7 marked `Done` with a real PR link.
14. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-059` | .NET (`Odyssey.Tests.Persistence`) | PurchaseAttributeIncrease co-commits AdvancementPurchase | Pass |
| `TC-CHAR-060` | .NET (`Odyssey.Tests.Persistence`) | PurchaseSkillLevel co-commits AdvancementPurchase | Pass |
| `TC-CHAR-061` | .NET (`Odyssey.Tests.Persistence`) | Resolve approve+spend co-commits AdvancementPurchase, Cost=ReservedAmount | Pass |
| `TC-CHAR-062` | .NET (`Odyssey.Tests.Persistence`) | Resolve approve without spend co-commits AdvancementPurchase, Cost=0 | Pass |
| `TC-CHAR-063` | .NET (`Odyssey.Tests.Persistence`) | Resolve dismiss creates no AdvancementPurchase | Pass |
| `TC-CHAR-064` | .NET (`Odyssey.Tests.Persistence`) | Revert independent purchase: value/balance/event/status all directly verified | Pass |
| `TC-CHAR-065` | .NET (`Odyssey.Tests.Persistence`) | Revert with dependent purchase rejected, no state change | Pass |
| `TC-CHAR-066` | .NET (`Odyssey.Tests.Persistence`) | Revert without ReasonCode rejected | Pass |
| `TC-CHAR-067` | .NET (`Odyssey.Tests.Persistence`) | Duplicate CommandId for Revert does not double-refund | Pass |
| `TC-CHAR-068` | .NET (`Odyssey.Tests.Persistence`) | Preview creates no event/state change | Pass |
| `TC-CHAR-069` | .NET (`Odyssey.Tests.Persistence`) | ApplyCharacterRespec batch: individually-visible events + one completion event | Pass |
| `TC-CHAR-070` | .NET (`Odyssey.Tests.Persistence`) | ApplyCharacterRespec recomputes server-side, ignores stale preview | Pass |
| `TC-CHAR-071` | .NET (`Odyssey.Tests.Persistence`) | Duplicate CommandId for ApplyCharacterRespec does not duplicate batch | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`106`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — three new `DomainEvents` columns (`OriginalEventId` nullable, `CompensationGroupId` nullable, `IsCompensating` defaulted `0`), one new table (`AdvancementPurchase`); no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new columns only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new columns/table are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched; `RevertAdvancementPurchase`/`ApplyCharacterRespec` never delete or mutate an existing `DomainEvents` row.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: purchase/reversion/respec amounts and reason codes — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `RevertAdvancementPurchase`/`ApplyCharacterRespec` are MainGM-only.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm`, matching `ResolveAdvancementRecommendation`'s already-tested convention exactly.
- Redaction requirements: the five new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only purchase/target/amount/actor/reason/outcome fields — no secret data.
- Abuse / malformed input limits: `AdvancementPurchaseId` validated against the canonical id pattern; `ReasonCode` validated non-empty.
- Security tests: MainGM gate exercised implicitly by every mutating test in this task (all pass `actorIsMainGm: true`); a dedicated non-MainGM rejection test was not required by this task's own explicit test list — the gate mirrors `ResolveAdvancementRecommendation`'s already-tested convention exactly.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 7 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract, introduces new persisted schema, and implements the slice's first compensating-event mechanism.
- ExecPlan path: `docs/plans/active/ODY-S04-107_Advancement_Revert_And_Character_Respec.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-105`/`106` (done). Unblocks `ODY-S04-110`–`113` (Ruleset migration, expected to reuse this task's own compensating-batch pattern).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `DomainEvents` columns and one new table; no versioned schema migration.
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
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 145 (132 pre-existing + 13 new) — 376 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `CharacterDevelopmentPoolAttributePurchaseTests.cs`/`CharacterSkillPurchaseCriticalEvidenceTests.cs` unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-059`–`063`. |
| AC-3 | Passed | `TC-CHAR-064`. |
| AC-4 | Passed | `TC-CHAR-065`. |
| AC-5 | Passed | `TC-CHAR-066`. |
| AC-6 | Passed | `TC-CHAR-067`. |
| AC-7 | Passed | `TC-CHAR-068`. |
| AC-8 | Passed | `TC-CHAR-069`. |
| AC-9 | Passed | `TC-CHAR-070`. |
| AC-10 | Passed | `TC-CHAR-071`. |
| AC-11 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-12 | Passed | See Validation results above. |
| AC-13 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 7 status/PR link updated. |
| AC-14 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `RevertAdvancementPurchase`'s dependency check is deliberately minimal (same-target-current-value-equals-ToValue only) — a real cross-entry prerequisite graph is Rules Engine content, explicitly out of scope per `ADR-024` §6.2's own text.
- `AdvancementPurchase.RequirementsSnapshot` is a fixed `"{}"` placeholder — no requirements-engine exists yet anywhere in this codebase to populate it with real content.
- `ApplyCharacterRespec`'s target repurchase is "fully undo then buy fresh to the desired level in one purchase" — it does not attempt to preserve or partially reuse an existing purchase's own `AdvancementPurchaseId`/history.

### Follow-up tasks

- `ODY-S04-108`/`109` — ability/resource/anatomy.
- `ODY-S04-110`–`113` — archive/delete, Dead/restore, `.odchar`, Ruleset migration (expected to reuse this task's own compensating-batch pattern).

### Self-review summary

- Scope review: limited to allowed files; no `ADR-012`/`024` or `SLICE-04_BACKLOG.md` change; no Unity/UI code; no production requirements-graph authored.
- Architecture review: the gap fix (pkt 0) was completed and verified regression-free before any Block Б code was written, per this task's own explicit ordering instruction; `RevertAdvancementPurchase` reuses `MutateMechanics` unchanged; `ApplyCharacterRespec`'s multi-event batch is the one genuinely new mechanism, isolated to its own dedicated method rather than forcing a fourth generalization onto `MutateMechanics`'s existing one-event contract.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; the dependent-purchase rejection, duplicate-`CommandId` idempotency (both commands), and stale-preview-ignored scenarios are exercised for real, not simulated.
- Security/privacy review: both new gates (MainGM-only, non-empty `ReasonCode`) reuse/extend existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-02 — Decision: `AdvancementPurchase` is a single Domain class, used directly as `ICharacterRepository`'s own return type, no Application-layer wrapper — Authority/approval: product §13.2's own flat schema; resolves the internal inconsistency between `ODY-S04-105`'s (`DevelopmentTransaction`, duplicated) and `ODY-S04-106`'s (`CriticalSuccessEvidence`/`AdvancementRecommendation`, not duplicated) own precedent by picking the simpler, more common shape.
- 2026-09-02 — Decision: `AdvancementPurchase.Cost` validated `>= 0`, not `> 0` — Authority/approval: `ADR-024` §6.1 branch 3's fully-evidence-funded approval genuinely spends zero development points.
- 2026-09-02 — Decision: `RevertAdvancementPurchase`'s dependency check is exactly "current value still equals this purchase's own ToValue," no cross-entry graph — Authority/approval: `ADR-024` §6.2's own explicit deferral of the exact dependency graph to a future Rules Engine.
- 2026-09-02 — Decision: `ApplyCharacterRespec` appends its batch events directly via the now-`internal` `SqliteSavingPipeline.AppendDomainEvent`, with only the trailing `CharacterRespecCompleted` event going through the normal `Execute<T>` path — Authority/approval: `MutateMechanics`'s own one-event-per-call contract cannot express a multi-purchase batch; every event, batch or not, is still written by the identical code.
- 2026-09-02 — Decision: `ApplyCharacterRespec`'s "snapshot before operation" (product §13.5 step 5) is realized as the before/after summary embedded in `CharacterRespecCompleted`'s own payload, not a `SqliteBackupRepository` file backup — Authority/approval: `ADR-024` §7.2 frames it as event-payload data; `ADR-022` §7 separately prohibits a full-Character-sheet-copy event.

### Approved task changes

- None.
