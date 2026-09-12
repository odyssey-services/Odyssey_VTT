# ODY-S05-403 — Migration Confirm/Apply Command

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (ItemDefinition migration block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `codex/ody-s05-403-migration-apply`
**Pull request:** [odyssey-services/Odyssey_VTT#132](https://github.com/odyssey-services/Odyssey_VTT/pull/132) (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-403_Migration_Confirm_Apply.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Implement `ADR-027` §10 steps 5-9: the MainGM-only confirm/apply transition for an `ItemDefinition` migration — the triple host-authoritative revision guard, the mandatory pre-apply `ADR-012` backup, atomic multi-snapshot update of every affected `ItemInstance`/`ItemStack` in one transaction, and the documented, permanent absence of a rollback command after success. Consumes `ODY-S05-401`'s preview and `ODY-S05-402`'s blocking-incompatibility computation; re-implements neither.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-401`/`402` can build and validate a migration preview, but nothing yet actually applies one. This is the only task in the migration block that writes to the database.
- Value or risk reduction: closes the migration block's central risk (an irreversible, campaign-wide, MainGM-gated data mutation) with the same defense-in-depth pattern already proven by `DeleteCharacterPermanently` (backup-before-mutate, host-authoritative re-validation, fail-closed).
- Blocking or enabling relationship: unblocks `ODY-S05-404` (integration fixtures), the final task in the block.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §13/§13.1 row 3.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §10 steps 5-9, §12 rules 1-2, §14.
- `docs/adr/ADR-012` (backup/transaction section referenced by `ADR-027` §10 step 8) — read only, not modified.
- `docs/adr/ADR-001` (module dependency direction).
- Existing patterns: `SqliteCharacterRepository.DeleteCharacterPermanently` (backup-before-mutate, fail-closed two-phase check precedent); `SqliteInventoryRepository.EquipItemCore<T>`/`MergeItemStacks`/`SplitItemStack` (CAS-per-row, ledger-based idempotency precedent); `SqliteSavingPipeline` (the shared ADR-012 single-transaction journal/event/idempotency commit pipeline already used by `SqliteCampaignRepository`/`SqliteCharacterRepository`/`SqliteGameLogRepository`/`SqliteSceneRepository`).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-403`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`178` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-179`-`188`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-403` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main`/this branch's merge-base is `b36246c`, the merge of PR #131 (`ODY-S05-402`, `ComputeBlockingIssues`). Backlog row 3 (`ODY-S05-403`) was `Proposed`.
- **Corrected premise (direct code read, contradicting this ТЗ's own §8 assumption):** no existing multi-row mutation in `SqliteInventoryRepository.cs` (`MergeItemStacks`, `SplitItemStack`, `EquipItemCore<T>`, `Move*`) writes a `DomainEvents` row or uses `SqliteSavingPipeline`/`AppliedCommands` at all — confirmed via `grep` across the pre-403 file and via direct reads of `EquipItemCore<T>`'s full body. Every existing Inventory mutation's only "audit" trail is a small, operation-specific idempotency ledger table (`EquipmentCommandLedger`, `InventoryCommandLedger`, `InventoryMoveCommandLedger`, `InventoryStackCommandLedger`) with no event emission whatsoever. This ТЗ's §8 instruction to "study how `MergeItemStacks`/`SplitItemStack`/`EquipItem` write `DomainEvents`" therefore has no literal answer in this file — they don't. See §18 for the resulting decision.
- `SqliteSavingPipeline` (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs`, direct code read) is a real, already-shared, `internal` ADR-012 single-transaction commit helper — used today by `SqliteCampaignRepository`/`SqliteCharacterRepository`/`SqliteGameLogRepository`/`SqliteSceneRepository`, never by `SqliteInventoryRepository`. Its `Execute<T>` takes a `tryReplay` callback (invoked when `AppliedCommands` already has a `Completed` row for the `CommandId`) and an `apply` callback (invoked otherwise, returning a `PipelineWrite<T>` naming the event type/payload); on success it appends one `DomainEvents` row, upserts `AggregateRevisions`, and inserts one `AppliedCommands` row, all inside one `SqliteTransaction` that is disposed (and thus auto-rolled-back by ADO.NET) if never explicitly committed.
- `IBackupRepository.CreateBackup(CampaignHandle campaign, string reason, CorrelationId correlationId)` (direct code read) is the only member needed here; `SqliteBackupRepository.CreateBackup` (direct code read) rejects any `reason` longer than 96 characters. `ContentDefinitionId.ToString()` is always exactly 37 characters (`"cdef_"` + 32 hex, confirmed by direct code read of `ContentDefinitionId`); a reason of the form `"pre-migration:" + sourceId + "->" + targetId` is exactly 14 + 37 + 2 + 37 = 90 characters, safely under the limit while still naming both definitions in the backup list — the ТЗ's own suggested literal format (`"pre-migration-item-definition:<sourceId>->,<targetId>"`) would have been ~106 characters and rejected outright; this was found and fixed during implementation (§18).
- `SqliteCharacterRepository.DeleteCharacterPermanently` (direct code read, ~line 785) is the sole existing `CreateBackup` call site in the codebase; it opens the backup on a connection separate from the delete's own transaction, before that transaction opens, and aborts the whole delete if `CreateBackup` fails — the exact pattern this task's own `ApplyItemDefinitionMigration` reuses.
- `ItemInstance`/`ItemStack` table columns (direct schema read): `SourceItemDefinitionRef`, `MechanicsSourceDefinitionRef`, `MechanicsDefinitionSnapshotVersion`, `MechanicsContentType`, `MechanicsPayload`, `Revision`. No existing `UPDATE` anywhere in `SqliteInventoryRepository.cs` touches `SourceItemDefinitionRef` or any `Mechanics*` column before this task — confirmed by `grep` across the pre-403 file; every existing mutation only ever touches `LocationKind`/`LocationTargetRef`/`LocationDetailRef`/`Revision`/`Quantity`/`UpdatedAt`. This is genuinely the first task to rewrite an item's own definition-origin data.
- `ContentDefinition` table columns (direct schema read): `Revision`, `Version`, `Status`, `DefinitionType`, `PropertiesJson` — matching what a fresh in-transaction re-read needs to detect a race against the definition rows read just before backup.
- `ReplaceEquippedEntry` (direct code read, ~line 1333) mutates only the `EquippedEntry` row's own `Revision`/`EquipmentSlotRef`/`BodyPartRefs` columns — it never touches the underlying `ItemInstance`/`ItemStack` row's own `Revision`. This means an item's equipment slot can change without its own `ItemInstance.Revision` changing at all, so no revision-CAS check over the item itself can ever detect a slot change — only a live re-run of `ComputeBlockingIssues` (reading `EquippedEntry` fresh) can. This is the exact mechanism `TC-INVENTORY-184`/`187` exercise.
- `ComputeBlockingIssues(preview, targetDefinition, currentlyEquippedAffectedItems)` (`ODY-S05-402`, direct code read) only ever receives `EquippedEntry` rows for `preview.AffectedInstances` (via `InventoryItemRef.ForInstance`) — never for `AffectedStacks` — and throws `ArgumentException` if a supplied entry doesn't reference exactly one affected instance. The host-side `ReadMigrationEquipment` helper this task adds must query `EquippedEntry` only for affected instances, mirroring this precondition exactly.
- `InventoryCreationService`'s `IsInstanceDefinitionType`/`IsStackDefinitionType` predicates (direct code read) mean `Armor`/`Weapon` can only ever back an `ItemInstance`, and `Ammo` can only ever back an `ItemStack`; only `ContentDefinitionType.Item` (with `IsStackable = true`) can back both an instance and a stack from the same source definition — required for a single migration preview to exercise both affected-instance and affected-stack code paths at once (`TC-INVENTORY-179`).
- `SqliteInventoryRepositoryTests.cs`'s `InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses` (direct code read) has two independent checks: a table-name check (needs the new ledger table added to its `allowedEquipmentTables` array) and a type-name check scanning `typeof(SqliteInventoryRepository).Assembly.GetTypes()` (needs no change, since this task adds no new top-level `Odyssey.Persistence` type — only private members inside the existing `SqliteInventoryRepository` class).
- **Discovery during implementation (full-suite `dotnet test` run, not merely assumed):** `InventoryCreationServiceTests.cs`'s *own separate* table-name guard (`InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities`, filtering `tableNames.Where(name => name != "EquipmentCommandLedger")` before `AssertForbiddenFragments`) also needed the new ledger table added to its own filter — this is a *third* guard site, distinct from the two the ТЗ's own §11 named, found only by running the full suite rather than reasoning about scan scope alone. Fixed (§18).

### Assumptions

- None.

## 5. Scope

### In scope

- New `IInventoryRepository.ApplyItemDefinitionMigration` port method + SQLite implementation.
- New DTOs `ItemDefinitionMigrationTransition`/`ItemDefinitionMigrationApplyResult` in `ItemDefinitionMigrationRules.cs`.
- New `ItemDefinitionMigrationCommandLedger` table (idempotency).
- Optional `IBackupRepository?` constructor parameter on `SqliteInventoryRepository`, defaulting to `new SqliteBackupRepository(clock)`.
- Three new `ErrorCodes`/`ERROR_CODES.md` rows: `inventory.migration.denied`, `inventory.migration.preview_conflict`, `inventory.migration.blocked`.
- `ThrowingInventoryRepository` fake update.
- Point allow-list narrowing of three scope guards (see §11 discovery above).
- Tests, test metadata, task/plan docs, backlog row.

### Out of scope

- Any change to `ItemDefinitionMigrationPreview`/`ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues` (`ODY-S05-401`/`402`) beyond calling them.
- Any compensating/Undo/Rollback command — explicitly forbidden by `ADR-027` §10 step 9.
- `EquipItemCore<T>`/`UnequipItemCore<T>`/`EquipmentCommandLedger` — read-only precedent.
- `IBackupRepository`/`SqliteBackupRepository`'s own implementation — called, not modified.
- Integration fixtures (`ODY-S05-404`'s own job).
- ADR edits, Unity/UI.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/**
DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs (only if a new file under Runtime/Inventory is added -- it is not, so unused)
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-403_Migration_Confirm_Apply.md
docs/plans/active/ODY-S05-403_Migration_Confirm_Apply.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.rules/**
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteBackupRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: confirm/apply orchestration stays in `Odyssey.Application`/`Odyssey.Persistence` per `ADR-027` §14; no new `Odyssey.Rules` dependency.
- Authoritative-state and transaction boundary: the backup is created on a separate connection/transaction from the apply transaction (never inside it); the apply transaction is the sole owner of the CAS snapshot updates, the ledger insert, and (via `SqliteSavingPipeline`) the `DomainEvents`/`AppliedCommands` rows — all commit or none do.
- Serialization / compatibility boundary: the migration fingerprint and audit payload use the project's existing `Newtonsoft.Json`-based `JsonTextWriter` (already a dependency of this same file, e.g. `TypedDefinitionCodec`), not a new serializer.
- Time / RNG rule: `UtcInstant now = _clock.GetUtcNow()` via the injected `IWallClock`, matching every other mutation in the file.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency — `Newtonsoft.Json` and `Microsoft.Data.Sqlite` are both already referenced by this project.
- Security / privacy / redaction rule: no raw JSON, exception text, or stack traces in any returned `Error`; the audit event payload is host-side only (`DomainEvents`), never a client-facing projection.
- Other: `actorIsMainGm` is checked as the literal first statement of `ApplyItemDefinitionMigration`, before any argument validation that could itself touch I/O (there is none) and before opening any connection.

## 7. Expected behavior

### Scenario 1 — successful migration updates every affected record atomically

**Given** a Published source and target `ItemDefinition` of the same `ContentDefinitionType`, a clean preview with one affected `ItemInstance` and one affected `ItemStack`, and a MainGM actor
**When** `ApplyItemDefinitionMigration` is called
**Then** both records' `SourceItemDefinitionRef`/`MechanicsSnapshot` become the target's, both `Revision`s increment by exactly 1, the stack's `Quantity` is unchanged, a backup was created, and the result reports `UpdatedInstanceCount = 1`/`UpdatedStackCount = 1`.

### Scenario 2 — non-MainGM actor is denied before any I/O

**Given** `actorIsMainGm = false`
**When** `ApplyItemDefinitionMigration` is called
**Then** it fails with `inventory.migration.denied`, no backup is created, and the affected record's own row is untouched.

### Scenario 3 — stale source definition revision is rejected

**Given** the source definition's own `Revision` changed after the preview was built
**When** `ApplyItemDefinitionMigration` is called with the now-stale preview
**Then** it fails with `persistence.content_definition.revision_conflict` before any backup, and nothing is mutated.

### Scenario 4 — stale affected-inventory revision is rejected wholesale

**Given** two affected instances in two different inventories, one of whose `Inventory.Revision` changed after the preview was built
**When** `ApplyItemDefinitionMigration` is called
**Then** it fails with `persistence.inventory.revision_conflict`, and *neither* instance (not just the one in the drifted inventory) is updated.

### Scenario 5 — stale affected-item revision is rejected wholesale with no partial write

**Given** two affected instances sharing one inventory, one of whose own `Revision` changed after the preview was built
**When** `ApplyItemDefinitionMigration` is called
**Then** it fails with `persistence.inventory.item_revision_conflict`, and the *other*, non-drifted instance is also left untouched.

### Scenario 6 — a blocking issue present at confirm time is caught even though the preview was clean when built

**Given** an instance equipped in a slot the target still defines when the preview is built (no blocking issue), then re-slotted via `ReplaceEquippedEntry` to a slot the target does not define (which changes only the `EquippedEntry`'s own revision, not the item's)
**When** `ApplyItemDefinitionMigration` is called with the (now stale, but revision-wise still valid) preview
**Then** it fails with `inventory.migration.blocked`, and the affected instance's `SourceItemDefinitionRef` is unchanged.

### Scenario 7 — replay with the same `CommandId` and actor is idempotent

**Given** a successful migration already applied under `CommandId` X by actor U
**When** `ApplyItemDefinitionMigration` is called again with the same `CommandId` X, actor U, and preview
**Then** it returns the same `BackupId`/counts without creating a second backup or re-applying the write.

### Scenario 8 — a backup failure aborts everything before any transaction opens

**Given** an `IBackupRepository.CreateBackup` that always fails
**When** `ApplyItemDefinitionMigration` is called
**Then** it fails, no apply transaction is ever opened, and the affected record's own row is untouched.

### Scenario 9 — a blocking issue introduced during the backup window is caught by the separate in-transaction recheck

**Given** two affected instances, clean when the preview is built; a backup implementation that, immediately after a real successful backup, re-slots one instance into a now-incompatible slot (simulating a change landing strictly inside the backup's own time window)
**When** `ApplyItemDefinitionMigration` is called
**Then** the backup itself succeeds (proving the pre-backup check alone did not — and could not — catch this), but the in-transaction re-run of `ComputeBlockingIssues` rejects the whole migration with `inventory.migration.blocked` before either affected instance's row is written.

### Scenario 10 — a tampered `PreviewRevision` is rejected as a sanity check, distinct from the live revision CAS checks

**Given** a preview whose `PreviewRevision` field does not match a fresh recomputation over its own other fields
**When** `ApplyItemDefinitionMigration` is called
**Then** it fails with `inventory.migration.preview_conflict` before any backup.

### Required invariants

- `actorIsMainGm` is checked before any connection is opened.
- A backup is always created before the apply transaction opens, on its own connection, unless the command has already been applied (idempotent short-circuit checked first, per `DeleteCharacterPermanently`'s own precedent).
- Every one of: source definition revision, every affected `InventoryId`'s revision, every affected `ItemInstance`/`ItemStack`'s own revision, is re-read from the database (never trusted from the caller-supplied preview) both before the backup and again inside the apply transaction.
- `ComputeBlockingIssues` is re-run against freshly-read `EquippedEntry` rows both before the backup and again inside the apply transaction, immediately before any row is written.
- All snapshot updates, the ledger insert, and the `DomainEvents`/`AppliedCommands` rows commit together or not at all.
- No compensating/Undo/Rollback command exists anywhere in this task's diff.

## 8. Deliverables

- Production code: `IInventoryRepository` extension + SQLite implementation, `ItemDefinitionMigrationTransition`/`ItemDefinitionMigrationApplyResult` DTOs, new ledger table, optional backup-repository constructor parameter, three new error codes.
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationApplyTests.cs`, real-SQLite, covering all 10 scenarios above.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; the new ledger table is `CREATE TABLE IF NOT EXISTS`, additive only.

## 9. Acceptance criteria

1. `ApplyItemDefinitionMigration` checks `actorIsMainGm` as its first statement, before any I/O.
2. A mandatory `IBackupRepository.CreateBackup` call precedes the apply transaction, on a connection separate from it; its failure aborts the whole operation with no transaction ever opened.
3. Source definition revision, every affected inventory's revision, and every affected item/stack's own revision are re-read from the database and compared against the preview's own expectations, both before backup and again inside the apply transaction; any mismatch rejects the whole operation, never partially.
4. `ComputeBlockingIssues` is re-run against freshly-read equipment state immediately before commit, not merely trusted from the client-supplied preview.
5. Idempotency is provided by a new `ItemDefinitionMigrationCommandLedger` table, keyed by `CommandId`, following the `EquipmentCommandLedger` precedent; a replay with the same `CommandId` and actor returns the original result without a second backup or write.
6. Every affected `ItemInstance`/`ItemStack`'s `SourceItemDefinitionRef`/`MechanicsSnapshot`/`Revision` update lands in one transaction, all-or-nothing.
7. A `DomainEvents` row is appended (via the shared `SqliteSavingPipeline`, this file's first use of it) including `sourceDefinitionRef`/`targetDefinitionRef`/`backupId`/updated counts.
8. No compensating/Undo/Rollback command exists; `ApplyItemDefinitionMigration`'s own doc comment states `ADR-027` §10 step 9's absence-of-rollback rule verbatim.
9. `TC-INVENTORY-179`-`188` cover all 10 scenarios in §7; `dotnet test` is green.
10. All three affected scope guards (the two the ТЗ named plus the one discovered during implementation) are narrowed via point allow-list additions only, never editing a shared `forbidden` array.
11. Backlog row `ODY-S05-403` → `In Review (PR #NNN)`; PR is Draft.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-179` | .NET / NUnit (Persistence, real SQLite) | Successful migration atomically updates every affected ItemInstance/ItemStack | Pass |
| `TC-INVENTORY-180` | .NET / NUnit (Persistence, real SQLite) | Non-MainGM actor denied before any backup or DB access | Pass |
| `TC-INVENTORY-181` | .NET / NUnit (Persistence, real SQLite) | Stale SourceDefinitionRevision rejected before backup, no mutation | Pass |
| `TC-INVENTORY-182` | .NET / NUnit (Persistence, real SQLite) | Stale AffectedInventoryRevision rejected wholesale | Pass |
| `TC-INVENTORY-183` | .NET / NUnit (Persistence, real SQLite) | Stale affected item/stack revision rejected wholesale, no partial write | Pass |
| `TC-INVENTORY-184` | .NET / NUnit (Persistence, real SQLite) | Blocking issue at confirm time caught despite a clean built-time preview | Pass |
| `TC-INVENTORY-185` | .NET / NUnit (Persistence, real SQLite) | Replay with same CommandId/actor is idempotent, no second backup/write | Pass |
| `TC-INVENTORY-186` | .NET / NUnit (Persistence, real SQLite) | Backup failure aborts before any transaction opens | Pass |
| `TC-INVENTORY-187` | .NET / NUnit (Persistence, real SQLite) | Blocking issue introduced during the backup window caught by the in-transaction recheck; nothing written | Pass |
| `TC-INVENTORY-188` | .NET / NUnit (Persistence, real SQLite) | Tampered PreviewRevision rejected as a sanity check distinct from live revision CAS | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no ADR edits, no Equipment-core/`SqliteBackupRepository` changes, no compensating/Undo/Rollback command.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database and real `SqliteBackupRepository`-produced backup files.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any test of a rollback/Undo command, since none exists and none is introduced (`ADR-027` §10 step 9).
- Re-testing `ODY-S05-401`/`402`'s own preview/blocking-rule correctness — only that this task calls them host-authoritatively at the right points.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — one new port method, one new table, one new optional constructor parameter defaulting to existing behavior.
- Version fields affected: no manifest/application/schema version bumped.
- Migration or upcaster: none; `CREATE TABLE IF NOT EXISTS` only.
- Forward / backward behavior: older builds never call the new method; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: the migration itself is a real, irreversible data mutation by design (`ADR-027` §10 step 9) — its only recovery path is the mandatory pre-migration backup this task creates, restorable via the existing, unmodified `IBackupRepository.RestoreBackup`.
- Recovery rehearsal required: no (covered by existing backup/restore test coverage, not re-tested here).

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory/character test records; no real player data.
- Trust boundaries: `actorIsMainGm` is the sole authorization boundary, checked first and never inferred from any other field; a client-supplied preview is never trusted for revision or blocking-issue state, only for identifying which records to re-check.
- Authorization / audience checks: MainGM-only per `ADR-027` §12 rules 1-2; no AssistantGM escalation path exists or is added.
- Redaction requirements: the `DomainEvents` audit payload is host-side only; no raw exception text or stack trace is ever placed in a returned `Error`.
- Log-safe fields: `sourceDefinitionRef`/`targetDefinitionRef`/`backupId`/`actorUserId`/updated counts only — no `PropertiesJson` payload content.
- Abuse / malformed input limits: every DTO constructor validates its own invariants and fails fast; a `CommandId` colliding with a different operation's own ledger is rejected as an identity mismatch, never silently reused.
- Security tests: `TC-INVENTORY-180` (authorization gate) and `TC-INVENTORY-188` (tampered-preview rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces new persistence (a ledger table), a new `IInventoryRepository` port method, and a new SQL write path into columns no prior task has touched — multiple `PLANS.md` §1.2 triggers.
- ExecPlan path: `docs/plans/active/ODY-S05-403_Migration_Confirm_Apply.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #131 (`ODY-S05-402`) and precede `ODY-S05-404`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs; `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues`; `SqliteBackupRepository`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one table and one port method; no manifest/schema version bump or protocol/ruleset change.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

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
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — new `ApplyItemDefinitionMigration` port method.
- `Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs` — new `ItemDefinitionMigrationTransition`/`ItemDefinitionMigrationApplyResult` DTOs.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` + `docs/errors/ERROR_CODES.md` — three new error codes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — implementation, new ledger table, optional `IBackupRepository?` constructor parameter, corrected backup `reason` format (§18).
- `DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationApplyTests.cs` — new file, `TC-INVENTORY-179`-`188`.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — `ThrowingInventoryRepository` fake updated.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs` — two point allow-list additions (type-name guard for the new DTOs; table-name guard for the new ledger, discovered mid-implementation).
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs` — point allow-list addition to the table-name guard.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-179`-`188` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-403` updated to `In Review`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 530/530 (520 predecessor + 10 new). |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` (ErrorCode registry rows accepted.) |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (MainGM first, before I/O) | Met | `TC-INVENTORY-180`; code review of `ApplyItemDefinitionMigration`'s first statement. |
| AC-2 (mandatory backup, separate connection, aborts on failure) | Met | `TC-INVENTORY-186`. |
| AC-3 (triple host-authoritative revision recheck, wholesale rejection) | Met | `TC-INVENTORY-181`/`182`/`183`. |
| AC-4 (host-authoritative blocking recheck before commit) | Met | `TC-INVENTORY-184`/`187`. |
| AC-5 (idempotent ledger) | Met | `TC-INVENTORY-185`. |
| AC-6 (atomic multi-row update) | Met | `TC-INVENTORY-179`; rollback proven by `TC-INVENTORY-187`. |
| AC-7 (audit event via SqliteSavingPipeline) | Met | Code review; `TC-INVENTORY-179` implicitly exercises the pipeline commit path. |
| AC-8 (no rollback command; doc comment) | Met | Diff review; doc comment on `ApplyItemDefinitionMigration`. |
| AC-9 (10 tests, dotnet test green) | Met | Table above. |
| AC-10 (three scope guards narrowed by point exception) | Met | Diff review of all three guard files. |
| AC-11 (backlog In Review, Draft PR) | Met | PR #132 opened as Draft; backlog row updated. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- `ODY-S05-402`'s own four unsupported blocking-rule categories (loaded ammo, armor runtime damage, custom state, hidden mechanics) remain unsupported here too — this task does not simulate or claim coverage of them.
- No refresh-preview convenience exists; a caller whose revision check fails must call `BuildPreview` again and resubmit, per this ТЗ's own §4 explicit direction.

### Follow-up tasks

- `ODY-S05-404` — Migration Integration Fixtures (final task in the block).

### Self-review summary

- Scope review: diff touches only allowed paths; no ADR, `SqliteBackupRepository`, `EquipItemCore<T>`, or `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues` change.
- Architecture review: reuses `SqliteSavingPipeline` (already shared by 4 other repositories) rather than inventing a new event mechanism; optional constructor parameter preserves every existing `SqliteInventoryRepository` call site.
- Test review: all 10 scenarios in §7 covered by real-SQLite tests; `TC-INVENTORY-187`'s backup-window race is genuinely injected (via a wrapping `IBackupRepository`), not merely asserted.
- Security/privacy review: MainGM gate first; no payload leakage in errors or logs.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog all updated; no schema/manifest/protocol version bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Design decision (corrects this ТЗ's own §8 premise): the migration audit event is emitted via the shared `SqliteSavingPipeline`, not a bespoke event-writing convention copied from `MergeItemStacks`/`SplitItemStack`/`EquipItem`.** Direct code read confirms none of those methods write a `DomainEvents` row or use `SqliteSavingPipeline`/`AppliedCommands` at all — every existing Inventory mutation's only durability record is a small, operation-specific ledger table with no event emission. Since `ADR-027` §10 step 8 and this task's own DoD explicitly require an emitted event/audit, and no local convention exists to copy, the most architecturally sound choice is the SAME shared commit pipeline four other repositories (`SqliteCampaignRepository`/`SqliteCharacterRepository`/`SqliteGameLogRepository`/`SqliteSceneRepository`) already use for exactly this need — not a new, sixth, one-off mechanism. This is `SqliteInventoryRepository.cs`'s first-ever use of `SqliteSavingPipeline`. Authority: `ADR-027` §10 step 8; direct code read confirming the absence of any existing Inventory-file event convention to follow instead.
- 2026-09-12 — **Design decision: `IBackupRepository` is injected as an optional constructor parameter (`IBackupRepository? backupRepository = null`, defaulting to `new SqliteBackupRepository(clock)`), exactly mirroring `SqliteCharacterRepository`'s own constructor shape.** This avoids a breaking change to every existing `new SqliteInventoryRepository(clock)` call site across the codebase and test suite. Authority: this ТЗ §3's own explicit direction; direct code read of `SqliteCharacterRepository`'s constructor for the exact precedent shape.
- 2026-09-12 — **Design decision: idempotency uses a NEW, dedicated `ItemDefinitionMigrationCommandLedger` table (checked directly, before backup, via a lightweight non-transactional query), layered underneath the shared `SqliteSavingPipeline`'s own `AppliedCommands`-based replay check (used inside the apply transaction).** A pre-backup check against ONLY the new ledger table (plus a cross-ledger collision check against every other existing Inventory ledger table and `AppliedCommands`, mirroring `HasAnyRuntimeReferenceToDefinition`-era precedent for detecting a `CommandId` reused across unrelated operations) lets a replay short-circuit before ever creating a redundant backup, per this ТЗ's own §6 explicit requirement ("до бэкапа"); the pipeline's own `AppliedCommands`-based check then additionally protects against a race where the `CommandId` was inserted elsewhere during the backup window. Authority: this ТЗ §6's own explicit direction; `EquipmentCommandLedger`'s own precedent shape.
- 2026-09-12 — **Design decision: the backup `reason` string is `"pre-migration:" + sourceDefinitionId + "->" + targetDefinitionId` (90 characters), not this ТЗ's own suggested literal format.** `SqliteBackupRepository.CreateBackup` rejects any reason over 96 characters (direct code read); `ContentDefinitionId.ToString()` is always 37 characters, so the ТЗ's own suggested `"pre-migration-item-definition:<sourceId>->,<targetId>"` format (~106 characters) would have been rejected outright by the very repository this task calls. Found and fixed during implementation, not assumed correct from the ТЗ text. Authority: this ТЗ §3's own explicit requirement that the format merely be "≤96 characters... recognizable," with the format itself left to the executor.
- 2026-09-12 — **Design decision: `PreviewRevision` is checked as a pure content-tamper sanity digest (recomputed and compared to the preview's own claimed value) separately from, and in addition to, the three live revision CAS rechecks — never treated as a substitute for them.** This matches this ТЗ §4 item 4's own explicit instruction not to conflate the two. Authority: this ТЗ §4 item 4's own explicit text.
- 2026-09-12 — **Discovery and decision: a full `dotnet test` run (not merely reasoning about scan scope) surfaced a THIRD scope guard needing a point allow-list addition, beyond the two this ТЗ's own §11 named** — `InventoryCreationServiceTests.cs`'s OWN table-name check (`InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities`, which filters `tableNames.Where(name => name != "EquipmentCommandLedger")` before its own call to `AssertForbiddenFragments`) had no exception for the new `ItemDefinitionMigrationCommandLedger` table and failed. Narrowed via the identical point-allow-list-at-the-call-site pattern this ТЗ's own §11 mandated for the other two guards, replacing the single-name filter with a small allow-array — not editing the shared `AssertForbiddenFragments` helper's own `forbidden` array. Authority: this ТЗ §11's own point-allow-exception principle, applied to a guard the ТЗ itself did not name.
- 2026-09-12 — Decision: `SqliteInventoryRepositoryTests.cs`'s type-name check (scanning `typeof(SqliteInventoryRepository).Assembly.GetTypes()`) needed no change, confirmed by the same full-suite run — this task adds no new top-level type to `Odyssey.Persistence`, only private members inside the existing `SqliteInventoryRepository` class. Authority: direct code read of the guard's exact scan scope; full-suite test confirmation.
- 2026-09-12 — Decision: `TC-INVENTORY-187`'s "blocking issue appears between preview and confirm" scenario is tested via a genuine race injected through a test-only `IBackupRepository` wrapper that mutates live equipment state immediately after a REAL, successful backup completes — not via a simpler "equip a different slot before calling Apply" setup. The simpler setup was tried first and found to be caught by the PRE-backup `CheckMigrationState` call (which already recomputes `ComputeBlockingIssues`), meaning no backup would ever be created and the in-transaction recheck's own necessity would go unproven; injecting the change strictly inside the backup's own time window is the only way, absent real concurrency, to prove the in-transaction recheck independently catches what the pre-backup check structurally could not have seen. Authority: this ТЗ §5's own explicit requirement that the host-authoritative recheck happen "непосредственно перед фиксацией транзакции," and §15's own requirement that this be independently verifiable, not merely asserted.
