# ODY-S01-010 — Migration Registry Baseline

**Status:** In Review
**Roadmap stage / slice:** SLICE-01 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s01-010-migration-registry-baseline`
**Pull request:** Not yet opened
**ExecPlan:** Not required — see section 14 (Brief plan)
**Created:** 2026-08-24
**Last updated:** 2026-08-24 UTC

## 1. Goal

A freshly created campaign records a formal `SchemaHistory` entry (`0001_Initial`) proving it started on the current `DatabaseSchemaVersion`, and the registered-migrations list that entry comes from is itself internally well-formed and versioned — without building any part of the migration runner (execution, rollback, compatibility mode).

## 2. Why this task exists

- Problem: `SchemaHistory` existed only as placeholder DDL since `ODY-S01-007`, with no row ever written and no registry structure behind it.
- Value: closes roadmap §10.6's exit criterion "миграции имеют версию и тест" at the level `SLICE-01` can actually exercise (no real second migration exists yet to test a runner against).
- Enabling relationship: a future migration-runner task can register real migrations against a registry structure that already exists and is already proven well-formed, instead of inventing both the registry and the runner in the same task.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `ADR-013_Migration_Runner_v1.0.md` sections 4 (migration registry), 8 (`SchemaHistory`) — sections 5–7, 10 (runner workflow, transactionality, failure handling, read-only compatibility mode) are explicitly out of scope, not implemented
- `ADR-011_Local_Campaign_Format_v1.1.md` section 5 (`DatabaseSchemaVersion` wiring, already present)
- `05_Persistence_Odyssey_VTT_v0.8.md` section 25.1 (migration registry concept)
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` section 2.1 (the scope-narrowing decision this task must stay inside)

### Requirement and test IDs

- Requirement IDs: roadmap §10.6 exit criterion "миграции имеют версию и тест"
- Existing test IDs: `TC-PERSIST-001`–`011`
- New test IDs to introduce: `TC-PERSIST-012`, `TC-PERSIST-013`

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `SqliteCampaignRepository.CreateSystemTables` (unchanged since `ODY-S01-007`) already creates `SchemaHistory` with exactly the `ADR-013` section 8 column set (`MigrationId`, `FromVersion`, `ToVersion`, `CodeChecksum`, `StartedAt`, `CompletedAt`, `Status`, `ApplicationVersion`, `BackupId`, `FailureCode`) — no DDL change is needed, only a row insert.
- `manifest.json`'s `DatabaseSchemaVersion` field (`CampaignManifest.DatabaseSchemaVersion`) has been wired and persisted since `ODY-S01-007`; `SqliteCampaignRepository.DatabaseSchemaVersion` is the internal constant (`"1.0.0"`) that both the manifest and (as of this task) `SchemaHistory` derive from.
- No row was ever inserted into `SchemaHistory` by any prior task — confirmed by reading `SqliteCampaignRepository.cs`/`SqliteSceneRepository.cs` in full before this task; neither references the table outside its `CREATE TABLE IF NOT EXISTS` statement.
- `007`, `008`, `009` are `Done`/merged on `main` (`git log` shows `788ef44` = merge of PR #31 for `009`; `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` rows 1–3 all show merged/PR-open status).
- `docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md` and `ODY-S01-009_Saving_Pipeline.md` still show their pre-merge `Pull request` text (`Draft — open`) even though PR #29 and #31 are both now merged — a stale-status desync of the same kind fixed twice before by dedicated status-sync tasks. Not fixed here: out of this task's explicit scope, and not requested by this ТЗ.

### Assumptions

- None.

## 5. Scope

### In scope

- `MigrationRegistry`/`MigrationDescriptor` (`Packages/com.odyssey.persistence/Runtime/Sqlite/MigrationRegistry.cs`): a minimal, well-formed, versioned list of registered migrations — today exactly one entry, `0001_Initial`.
- Inserting the `0001_Initial` `SchemaHistory` row inside `SqliteCampaignRepository.Create`'s existing pipeline transaction (same commit as the `Campaign` row and its `DomainEvent`/`AppliedCommands` entry — a natural, no-extra-machinery place to put it, not a new transactional mechanism).
- Tests proving: the row is created exactly once at `Create` time with the correct `FromVersion`/`ToVersion`/`CodeChecksum`/`Status`; `Open` never duplicates or rewrites it; the registry list itself has no duplicate IDs, is monotonically ordered, and every entry carries a non-empty checksum.

### Out of scope

- The migration runner itself: temp-copy execution pattern, per-step transactionality beyond what already exists, rollback-on-failure workflow, the 7-step normative open-with-old-schema workflow (`ADR-013` section 5).
- Read-only compatibility mode for a campaign newer than the client (`ADR-013` section 10).
- Any second, schema-changing migration (`0002_...`+) — only the identity `0001_Initial`.
- Any change to `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` — neither needed touching; `SchemaHistory` is inserted directly in `SqliteCampaignRepository.Create`'s existing pipeline `apply` callback, which already had access to the open transaction and connection.
- Any interface/contract change (`ICampaignRepository`/`ISceneRepository` unchanged) — this task adds an internal write, not a new capability callers invoke.
- Fixing `008`/`009`'s stale `Pull request` header text (see section 4) — a different, already-established class of fix, not requested by this task's ТЗ.

### Allowed paths

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/MigrationRegistry.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/MigrationRegistryTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-010_Migration_Registry_Baseline.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/tasks/active/ODY-S01-009_Saving_Pipeline.md
```

## 6. Technical constraints

- Module ownership and dependency direction: entirely within `Odyssey.Persistence`; `MigrationRegistry`/`MigrationDescriptor` are `public` (so the test project, a separate assembly, can assert well-formedness directly) but introduce no new Application-layer port — nothing outside `Odyssey.Persistence` needs to know about migrations yet (`ADR-001` respected: no port added where no cross-module consumer exists).
- Authoritative-state and transaction boundary: the `SchemaHistory` insert rides inside the same `ADR-012` section 5 pipeline transaction `Create`'s `Campaign` row already uses — not a new boundary, reuses the existing one from `ODY-S01-009`.
- Time / RNG rule: `StartedAt`/`CompletedAt` both use the existing `IWallClock` already threaded through `SqliteCampaignRepository`; no new time or RNG source.
- Unity / thread / lifetime rule: Not applicable — no new dependency, no new connection lifetime pattern.
- Dependency / licensing rule: No new dependency (`System.Security.Cryptography.SHA256`, already used elsewhere in this module for `PayloadHash`/asset hashing).
- Security / privacy / redaction rule: Not applicable — `SchemaHistory` fields are non-sensitive build/version metadata.
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Fresh campaign gets its identity migration record

**Given** a new campaign folder
**When** `Create` succeeds
**Then** `SchemaHistory` contains exactly one row: `MigrationId = "0001_Initial"`, `FromVersion = ToVersion = manifest.DatabaseSchemaVersion`, `Status = "Completed"`, `BackupId`/`FailureCode` null.

### Scenario 2 — Reopening does not duplicate the record

**Given** a campaign already created (one `SchemaHistory` row present)
**When** `Open` is called
**Then** `SchemaHistory` still has exactly one row, with the same `StartedAt` as before.

### Required invariants

- The registered-migrations list has no duplicate `MigrationId`, is strictly ordered, and every entry has a non-empty `FromVersion`/`ToVersion`/`CodeChecksum`.

## 8. Deliverables

- Production code: `MigrationRegistry.cs` (new); `SqliteCampaignRepository.cs` (point-fix: `InsertInitialSchemaHistoryRow`, `DatabaseSchemaVersion` changed `private`→`internal`).
- Tests: `MigrationRegistryTests.cs` (`TC-PERSIST-012`, `TC-PERSIST-013`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 4), this task contract.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: None — no real schema-changing migration exists yet.

## 9. Acceptance criteria

1. `MigrationRegistry.Registered` has no duplicate `MigrationId`, is strictly ordered by `MigrationId`, and every entry has a non-empty `FromVersion`/`ToVersion`/`CodeChecksum` (`TC-PERSIST-012`).
2. `MigrationRegistry.Initial` is a true identity migration: `FromVersion == ToVersion` (`TC-PERSIST-012`).
3. `Create` inserts exactly one `SchemaHistory` row whose `FromVersion`/`ToVersion` equal the manifest's `DatabaseSchemaVersion`, `Status = "Completed"`, `BackupId`/`FailureCode` null (`TC-PERSIST-013`).
4. `Open` does not insert a second `SchemaHistory` row or rewrite the existing one (`TC-PERSIST-013`).
5. No runner behavior (execution, rollback, compatibility mode) is implemented or claimed by any test.
6. All prior `TC-PERSIST-*` tests continue to pass unmodified.
7. `SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` are untouched.
8. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-012` | .NET / `dotnet test` | Registry well-formedness (no duplicates, ordered, checksums present); `0001_Initial` is an identity migration | Pass |
| `TC-PERSIST-013` | .NET / `dotnet test` | `Create` writes exactly one correct `SchemaHistory` row; `Open` does not duplicate/rewrite it | Pass |

### Required commands

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\test-fast.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-repository.ps1
```

### Manual validation

- None — all acceptance evidence is automated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution on `ubuntu-latest`.
- Unity editor or Player profile: Not applicable — no new dependency, no IL2CPP-relevant API surface added beyond what `ODY-S01-007` already proved compatible.
- Scripting backend: Not applicable.
- Network topology or database fixture: Local SQLite file per test, `Path.GetTempPath()`-based.
- Other: None.

### Validation not required by this task

- A migration-runner rehearsal (applying a real schema-changing migration) — no such migration exists yet; explicitly deferred per backlog section 2.1.
- A second IL2CPP preflight — no new NuGet dependency, no new platform-sensitive API.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — `SchemaHistory`'s column set is unchanged from `ODY-S01-007`'s already-reserved DDL; only a row insert is added, and no campaign created before this task exists to be affected.
- Version fields affected: None — `DatabaseSchemaVersion`/`CampaignFormatVersion` values themselves are unchanged.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable — pre-release, no compatibility surface yet.
- Rollback method: Revert the branch; no persisted data depends on the new row.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No — this task adds no new failure-prone runner logic to rehearse recovery for.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: Build/version metadata only (`MigrationId`, version strings, application version, timestamps).
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable.
- Log-safe fields: All `SchemaHistory` fields are already non-sensitive by `ADR-013`'s own definition.
- Abuse / malformed input limits: Not applicable — no user input reaches this path.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` section 1.2's ExecPlan triggers individually, not by analogy to `007`/`008`/`009`: this task is contained in one production module (`Odyssey.Persistence` only — no `Odyssey.Application` change, unlike `009`'s port signature change); it does not change a public contract, protocol, package, Unity/package version, or build pipeline (`ICampaignRepository`/`ISceneRepository` are untouched); `SchemaHistory`'s column set was already reserved by `ODY-S01-007`, so this task fills in an already-planned table rather than introducing a new persisted format; it has one clear implementation path (insert a row inside an existing transaction); it fits one focused PR; and it requires no migration or recovery procedure (no real migration exists to rehearse). `PLANS.md` section 1.1's five Brief-plan conditions are all satisfied. This meets `PLANS.md` section 1.1's Brief-plan bar; the plan is recorded directly in this section per section 1.1's own allowance ("may live in the task response or pull request description").
- Brief plan:
  1. Files inspected: `SqliteCampaignRepository.cs` (`CreateSystemTables`, `Create`, `InsertCampaignRow`), `SqliteSceneRepository.cs` (confirmed no touch needed), `ADR-013` sections 4/8, `05_Persistence` section 25.1.
  2. Intended change: add `MigrationRegistry`/`MigrationDescriptor`; insert the `0001_Initial` row inside `Create`'s existing pipeline transaction; change `DatabaseSchemaVersion` from `private` to `internal` so the registry can reference the live constant.
  3. Tests: `MigrationRegistryTests.cs` (`TC-PERSIST-012`, `013`); full existing suite re-run.
  4. Non-goals: no runner, no read-only compatibility mode, no second migration, no interface change.
- ExecPlan path: Not required.
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `007` already merged (backlog's stated dependency).

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 4 only).
- Documents that must not change: any ADR, `007`/`008`/`009` task contracts.
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (None required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. — pending Draft PR creation.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.persistence/Runtime/Sqlite/MigrationRegistry.cs` — new; `MigrationDescriptor`, `MigrationRegistry` with one entry, `0001_Initial`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCampaignRepository.cs` — `DatabaseSchemaVersion` visibility `private`→`internal`; `InsertInitialSchemaHistoryRow` called from `Create`'s pipeline `apply` callback.
- `DotNet/Tests/Odyssey.Tests.Persistence/MigrationRegistryTests.cs` — new, 4 tests.
- `Tests/Metadata/test-catalog.json` — `TC-PERSIST-012`, `013` added.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — row 4 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | All projects restored. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS` — no CRLF issue this time (files written directly, no scripted regex edit). |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` all PASS. |
| `.\scripts\test-fast.ps1` | Passed | `Odyssey.Tests.Persistence.dll`: 27/27 (up from 23); `Odyssey.Tests.Unit.dll`: 84/84; `Odyssey.Tests.Architecture.dll`: 2/2; `Odyssey.Tests.Domain.dll`/`Odyssey.Tests.Contracts.dll`: 1/1 each. |
| `.\scripts\check-repository-policy.ps1` | Passed | No new ErrorCode introduced, registry check unaffected. |
| `.\scripts\verify-repository.ps1` | Passed | `TC-ARCH-001` PASS after this contract/catalog entries existed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `Registry_IsWellFormed_NoDuplicateIds_MonotonicOrder_ChecksumPresent` |
| AC-2 | Passed | `Registry_InitialEntry_IsIdentityMigration_FromEqualsToEqualsCurrentSchemaVersion` |
| AC-3 | Passed | `Create_InsertsExactlyOneSchemaHistoryRow_MatchingDatabaseSchemaVersionInManifest` |
| AC-4 | Passed | `Open_DoesNotDuplicateOrRewriteInitialSchemaHistoryRow` |
| AC-5 | Passed | No runner code, no `SchemaHistory` `UPDATE`/temp-copy/rollback logic anywhere in the diff |
| AC-6 | Passed | 27/27 total, 0 failed |
| AC-7 | Passed | `git diff --name-status` confirms neither file touched |
| AC-8 | Passed | See Validation results above |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: `artifacts/bin/Odyssey.Persistence/debug/Odyssey.Persistence.dll`.
- Checksums: Not recorded — debug local build.
- Test or quality report: `dotnet test` console output (section above).

### Known limitations

- The registry has exactly one entry; its "monotonic order" and "no duplicates" checks are real but trivially satisfied with one item — they will matter once a real `0002_...` migration is registered by a future task.
- `BackupId` is `NULL` for `0001_Initial` since no pre-migration snapshot exists for a brand-new campaign; a real future migration's `SchemaHistory` row will need a genuine `BackupRecord` reference once `ODY-S01-011` (backups) exists.

### Follow-up tasks

- None assigned — the full `ADR-013` runner remains queued behind a real schema version increment, per backlog section 2.1's own framing ("activated only once a real schema version increment is needed").

### Self-review summary

- Scope review: Stayed within registry + one identity row; no runner, no compatibility mode, no second migration.
- Architecture review: No new Application-layer port; `SchemaHistory` write rides the existing `ODY-S01-009` transaction, no new transactional mechanism.
- Test review: Both the registry's own structure and the persisted row are tested; `Open`'s non-duplication is tested directly (not just implied by "no insert call exists there").
- Security/privacy review: No sensitive data touched.
- Documentation/version review: Only the test catalog and the one backlog row required updates.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-08-24 — Chose `Brief plan` over `ExecPlan`, checked individually against `PLANS.md` section 1.2's triggers rather than assumed by analogy to `007`/`008`/`009`. Authority: section 14 above.
- 2026-08-24 — `BackupId` left `NULL` for the identity migration rather than fabricating a placeholder `BackupRecord` reference. Authority: `ADR-013` section 8's `BackupId` traceability requirement applies to a real migration's pre-migration snapshot, which does not exist for a brand-new campaign.

### Approved task changes

- None.
