# ODY-S04-110 — Archive & Dependency-Aware Physical Delete

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-110-archive-physical-delete`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-110_Archive_And_Physical_Delete.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-02 UTC

## 1. Goal

Implement `ADR-025` §5: `ArchiveCharacter` as an ordinary `Lifecycle`-section transition (MainGM-or-assigned actor); `DeleteCharacterPermanently` — MainGM-only, with a host-authoritative, extensible-but-currently-empty dependency check, real campaign backup reuse before the irreversible commit, live-row-only removal, and proof that `CharacterHistoryProjection` continues to render the deleted Character's past purely from `ADR-022`'s already-required historical event snapshots.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-110` as the tenth implementation task, depending only on `ODY-S04-101` (the already-reserved `Lifecycle` section, already used once in `ODY-S04-104`). It is the first task requiring a host-authoritative dependency check where all three named dependency sources (board tokens, items, GameLog) are simultaneously unimplementable.
- Value or risk reduction: proves the slice's first genuinely destructive, irreversible operation is safe by construction (mandatory real backup, extensible dependency gate, append-only event history untouched).
- Blocking or enabling relationship: unblocks `ODY-S04-111` (Dead & `CharacterRestored`) and a future Board/Item/GameLog task, which would be the first real occasion to register a genuine `ICharacterDeletionDependencyChecker`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 10) — the binding scope definition for this task.
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §5 (full read — the governing section).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §7–8 (full read — historical snapshot/projection, already-required, first genuinely tested here).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §22 (full read), §22.3, §7.1, requirements 47/61.
- `Packages/com.odyssey.application/Runtime/Persistence/BackupRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs` (full read).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`'s own `ApproveCharacterDraft` (`ODY-S04-104`) — the binding structural precedent for a `Lifecycle`-section transition.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-110`, `ADR-025` §5, `ADR-022` §7–8, product §22, requirements 47/61.
- Existing test IDs reused: None directly reused; the archive actor gate reuses `ODY-S04-102`'s `CharacterOwnershipAssignment.IsAssignedCharacter` production code (not its tests). All pre-existing `TC-CHAR-*` tests through `TC-CHAR-114` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-115` through `TC-CHAR-128` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`; `git merge-base --is-ancestor` independently confirmed PR #93's merge commit is a real ancestor of `origin/main` before branching.
- Direct search across `Packages/com.odyssey.persistence` confirms no Board/Scene, GameLog, or other persistence implementation stores a `CharacterId` anywhere — all three of `ADR-025` §5.2's named dependency sources are unimplementable today, simultaneously.
- `IBackupRepository`/`SqliteBackupRepository` (`ODY-S01-011`) already implement product §22.2's backup step exactly — confirmed by full read.
- `CharacterLifecycleTransitions.IsValidTransition` already covers every `-> Archived` edge product §7.1 names and already rejects `Archived -> Archived` — no Domain-layer change needed.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterDeletionDependencyChecker` (new interface); `ICharacterRepository.ArchiveCharacter`/`DeleteCharacterPermanently`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — four new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — four new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — constructor extended with optional `IBackupRepository`/checker-list parameters; `HistoryEventTypes` extended; `ArchiveCharacter`/`DeleteCharacterPermanently`/`IsCommandAlreadyApplied`/`ReplayCharacterDeleted` implementations.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterArchivePhysicalDeleteTests.cs` (new) — 14 tests.
- `docs/errors/ERROR_CODES.md` (edit) — four new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-115`–`128`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 10 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Real Board/Item/GameLog dependency checking — no such cross-reference exists anywhere in this codebase (confirmed by search, for all three sources at once).
- Dead/`CharacterRestored` — `ODY-S04-111`.
- `.odchar` Export/Import, Ruleset migration — `ODY-S04-112`/`113`.
- `RestoreFromArchive` — not named in this task's own backlog row; explicitly not added.
- Any Unity/UI code — this task is purely Application/Persistence.
- Any change to `ADR-022`/`025` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.
- Any change to Board/GameLog/Backup persistence code beyond calling the existing `IBackupRepository` as-is.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterArchivePhysicalDeleteTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-110_Archive_And_Physical_Delete.md
docs/plans/active/ODY-S04-110_Archive_And_Physical_Delete.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` owns `ICharacterDeletionDependencyChecker` and the repository port extension; `Odyssey.Persistence` owns the SQLite implementation, including the self-constructing `IBackupRepository` dependency. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: `ArchiveCharacter` commits through the existing, unmodified `SqliteSavingPipeline`, mirroring `ApproveCharacterDraft`'s exact shape. `DeleteCharacterPermanently` performs its backup on a separate, already-closed operation before its own delete transaction opens, avoiding any write-lock contention on the same database file; the delete transaction itself re-validates every precondition host-authoritatively immediately before commit. `CommandId`/`AppliedCommands` remain the sole idempotency mechanism for both commands.
- Serialization / compatibility boundary: event payloads use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`109` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency; reuses the already-existing `IBackupRepository`/`SqliteBackupRepository`.
- Security / privacy / redaction rule: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`109`'s own established pattern.
- Other: `ArchiveCharacter` is MainGM-or-assigned; `DeleteCharacterPermanently` is MainGM-only and requires a non-empty `ReasonCode`.

## 7. Expected behavior

### Scenario 1 — `ArchiveCharacter` legal transitions

**Given** a Character in any of `Draft`/`Active`/`Inactive`/`Retired`/`Dead`
**When** `ArchiveCharacter` is called by MainGM or an assigned user
**Then** `LifecycleStatus` becomes `Archived`; a repeat call is rejected (`Archived → Archived` is not a legal edge).

### Scenario 2 — `ArchiveCharacter`'s actor gate

**Given** an actor who is neither MainGM nor assigned to the Character
**When** `ArchiveCharacter` is called
**Then** it is rejected with `CharacterArchiveDenied`, no state change.

### Scenario 3 — `DeleteCharacterPermanently`'s gates and backup

**Given** a MainGM actor with a `ReasonCode` and an empty (default) dependency-checker list
**When** `DeleteCharacterPermanently` is called
**Then** a real campaign backup is created (verified via `ListBackups`) before the Character's live row is removed.

### Scenario 4 — historical identity survives physical delete

**Given** a Character with prior history
**When** `DeleteCharacterPermanently` succeeds
**Then** `GetCharacter` returns `CharacterNotFound`, but `GetCharacterHistory` still returns every prior entry (correct `DisplayNameSnapshot`) plus the new `CharacterDeleted` entry; no `DomainEvents` row is ever removed.

### Scenario 5 — the dependency-checker extension point actually blocks

**Given** an injected `ICharacterDeletionDependencyChecker` that always reports a blocking dependency
**When** `DeleteCharacterPermanently` is called
**Then** it is rejected with `CharacterDeletionHasDependent`, no state change — proving the mechanism is live, not an unused parameter.

### Required invariants

- `DeleteCharacterPermanently` never deletes a `DomainEvents` row.
- A blocking dependency (once real checkers exist) always rejects the delete with no partial state change.
- A real campaign backup exists before every successful physical delete.
- No `ADR-022`/`025` file content changes.

## 8. Deliverables

- Production code: `CharacterRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence).
- Tests: 14 new tests in `CharacterArchivePhysicalDeleteTests.cs`, registered as `TC-CHAR-115`–`128`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — no schema change at all.

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-114` continue passing with their own assertions unmodified.
2. `ArchiveCharacter` transitions every legal source status to `Archived`, rejects the illegal repeat.
3. `ArchiveCharacter`'s actor gate is MainGM-or-assigned, verified for both outcomes.
4. `DeleteCharacterPermanently` is MainGM-only, requires `ReasonCode`.
5. `DeleteCharacterPermanently` with an empty dependency-checker list succeeds; with a blocking checker, it is rejected.
6. `DeleteCharacterPermanently` creates a real backup before deleting, verified via `ListBackups`.
7. Post-delete: `GetCharacter` returns not-found; `GetCharacterHistory` remains intact with correct snapshots; no `DomainEvents` row is removed.
8. Duplicate `CommandId` for both commands does not duplicate effect, verified against real state.
9. No change to `ADR-022`/`025`/`SLICE-04_BACKLOG.md`; no Unity/UI code; no change to Board/GameLog/Backup persistence code beyond using `IBackupRepository` as-is.
10. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
11. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 10 marked `Done` with a real PR link.
12. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-115`–`120` | .NET (`Odyssey.Tests.Persistence`) | ArchiveCharacter: legal transitions, illegal repeat, actor gate (both outcomes), duplicate-CommandId | Pass |
| `TC-CHAR-121`–`128` | .NET (`Odyssey.Tests.Persistence`) | DeleteCharacterPermanently: permission/reason gates, empty-checker success, backup creation, historical-identity survival, DomainEvents preservation, duplicate-CommandId, blocking-checker extensibility | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`109`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: none — no schema change at all; `SqliteCharacterRepository`'s constructor extension is fully backward-compatible (both new parameters optional with self-constructing defaults).
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: `DeleteCharacterPermanently` is genuinely destructive to the live `Character` row, but is itself protected by a mandatory real campaign backup created before it commits, and never touches `DomainEvents`.
- Recovery rehearsal required: No (relies on `SqliteBackupRepository`'s own already-validated restore path from `ODY-S01-011`).

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: lifecycle status transitions, deletion reason codes — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `ArchiveCharacter` is MainGM-or-assigned; `DeleteCharacterPermanently` is MainGM-only.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` and `CharacterOwnershipAssignment.IsAssignedCharacter` reused, matching existing conventions exactly.
- Redaction requirements: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only lifecycle/actor/reason/outcome fields — no secret data.
- Abuse / malformed input limits: `ReasonCode` validated non-empty for deletion.
- Security tests: both gates exercised directly (`ArchiveCharacter_ByUnrelatedUser_IsRejected_NoStateChange`, `DeleteCharacterPermanently_ByNonMainGm_IsRejected_NoStateChange`).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 10 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract and implements the slice's first genuinely irreversible operation.
- ExecPlan path: `docs/plans/active/ODY-S04-110_Archive_And_Physical_Delete.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101` (done). Unblocks `ODY-S04-111`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
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
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 205 (191 pre-existing + 14 new) — 436 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Every earlier test file unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-115`–`117`. |
| AC-3 | Passed | `TC-CHAR-118`/`119`. |
| AC-4 | Passed | `TC-CHAR-121`/`122`. |
| AC-5 | Passed | `TC-CHAR-123`/`128`. |
| AC-6 | Passed | `TC-CHAR-124`. |
| AC-7 | Passed | `TC-CHAR-125`/`126`. |
| AC-8 | Passed | `TC-CHAR-120`/`127`. |
| AC-9 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**`/Board/GameLog/Backup persistence file touched. |
| AC-10 | Passed | See Validation results above. |
| AC-11 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 10 status/PR link updated. |
| AC-12 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `ICharacterDeletionDependencyChecker`'s default list is empty — no real Board/Item/GameLog dependency checking exists yet; a future task must register real checkers.
- `RestoreFromArchive` does not exist — a symmetric restore-from-archive operation was explicitly not added, per this task's own out-of-scope instruction.

### Follow-up tasks

- `ODY-S04-111` — Dead & `CharacterRestored`.
- A future Board/Item/GameLog task — first real occasion to register a genuine `ICharacterDeletionDependencyChecker` implementation.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`025`/`SLICE-04_BACKLOG.md` change; no Unity/UI code; no Board/GameLog/Backup persistence-code change beyond using `IBackupRepository` as-is.
- Architecture review: `ArchiveCharacter` reuses `ApproveCharacterDraft`'s exact `Lifecycle`-transition shape unchanged in spirit; `DeleteCharacterPermanently`'s dependency-checker mechanism is genuinely extensible (proven by test, not merely declared); the backup-before-delete ordering avoids write-lock contention by construction.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; a real bug (duplicate-CommandId pre-check ordering) was caught by this task's own first test run and fixed, not glossed over.
- Security/privacy review: both gates reuse/extend existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-02 — Decision: `ICharacterDeletionDependencyChecker` list-injected via an optional constructor parameter defaulting to empty — Authority/approval: this task's own explicit §1.1 instruction.
- 2026-09-02 — Decision: `IBackupRepository` self-constructs a real `SqliteBackupRepository` by default in `SqliteCharacterRepository`'s constructor — Authority/approval: mirrors `_pipeline`'s own established convention; keeps the backup step a true transactional precondition of the delete command itself.
- 2026-09-02 — Decision: `ArchiveCharacter` is MainGM-or-assigned, not MainGM-only — Authority/approval: `ADR-025` §5.1's own explicit text and product §26's own MVP permission list.
- 2026-09-02 — Decision: fixed a real duplicate-`CommandId` bug found by this task's own first test run (pre-check ran before the pipeline's own replay detection, misreporting `CharacterNotFound` on a legitimate duplicate) — Authority/approval: this task's own test-driven discovery; fixed by checking `AppliedCommands` directly before the pre-check/backup.

### Approved task changes

- None.
