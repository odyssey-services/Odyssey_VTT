# ODY-S05-403 — Migration Confirm/Apply Command

**Status:** Done (PR #132, merged into main)
**Owner:** Codex (agent)
**Branch:** `codex/ody-s05-403-migration-apply`
**Pull request:** [odyssey-services/Odyssey_VTT#132](https://github.com/odyssey-services/Odyssey_VTT/pull/132) (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Give `ItemDefinition` migration its final write path: `ADR-027` §10 steps 5-9 — a MainGM-only, backup-guarded, host-authoritatively-revalidated, all-or-nothing apply of a migration `ODY-S05-401` previewed and `ODY-S05-402` checked. After this task, a confirmed migration actually rewrites every affected `ItemInstance`/`ItemStack`'s definition-origin data, with no rollback command afterward.

## 2. Task contract

- Goal: `IInventoryRepository.ApplyItemDefinitionMigration` + SQLite implementation, atomically applying a `ItemDefinitionMigrationTransition` (preview + `CommandId`) after MainGM check, mandatory backup, triple revision recheck, and blocking-issue recheck.
- Acceptance criteria: see task contract §9 (11 items) — MainGM-first, backup-before-transaction with abort-on-failure, host-authoritative revision/blocking rechecks both before backup and inside the transaction, atomic multi-row update, idempotent ledger, audit event, no rollback command, 10 passing tests, 3 scope guards narrowed, required validation commands green.
- Requirement IDs: `ODY-S05-403`, `SLICE-05`.
- In scope: `IInventoryRepository` port + SQLite implementation, 2 new DTOs, 1 new table, 3 new error codes, 1 new optional constructor parameter, tests/metadata/docs/backlog.
- Out of scope: `ODY-S05-401`/`402`'s own preview/blocking-rule code (called, not modified); any rollback command; `SqliteBackupRepository` itself.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §13/§13.1 row 3; `ADR-027` §10 steps 5-9, §12, §14; `SqliteCharacterRepository.DeleteCharacterPermanently` (backup-before-mutate precedent); `SqliteSavingPipeline` (shared commit pipeline).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Branch `codex/ody-s05-403-migration-apply` was created by a prior session off `origin/main` at `b36246c` (merge of PR #131, `ODY-S05-402`) and already contained a substantial, uncommitted implementation attempt when this session resumed the task. Rather than restart from scratch, the existing diff was read in full, verified line-by-line against real precedent, and corrected/completed rather than discarded — most of its architectural choices (SqliteSavingPipeline reuse, optional backup-repository parameter, dedicated ledger table, pre-backup + in-transaction dual recheck) held up under verification and are kept.
- Three concrete defects were found and fixed during verification (all recorded in the task contract §18): (1) the backup `reason` string used a static, non-identifying literal and would have exceeded `SqliteBackupRepository`'s 96-character limit had the ТЗ's own suggested format been used verbatim — fixed to `"pre-migration:" + sourceId + "->" + targetId` (90 chars); (2) a third scope guard (`InventoryCreationServiceTests.cs`'s own table-name filter) needed a point allow-list addition the original diff had missed; (3) no tests existed yet for the new apply path at all.
- Direct code read confirmed a premise correction: `SqliteInventoryRepository.cs`'s existing multi-row mutations (`MergeItemStacks`/`SplitItemStack`/`EquipItemCore<T>`) do not write `DomainEvents` or use `SqliteSavingPipeline` at all, contrary to this ТЗ's own §8 assumption — see task contract §4/§18 for the full finding and the resulting decision to reuse the shared pipeline (already used by 4 other repositories) rather than invent a new one-off event mechanism.
- `ReplaceEquippedEntry` only ever touches the `EquippedEntry` row's own revision, never the underlying item's — confirmed by direct code read — which is why a blocking issue can appear without tripping any item-revision CAS check, and why the host-authoritative `ComputeBlockingIssues` recheck is not redundant with the revision rechecks.
- `ItemInstance`/`ItemStack` table columns (`SourceItemDefinitionRef`, `MechanicsSourceDefinitionRef`, `MechanicsDefinitionSnapshotVersion`, `MechanicsContentType`, `MechanicsPayload`) were confirmed by direct schema read to match exactly what the existing draft's `UpdateMigrationSnapshot` SQL already wrote — no schema mismatch found.

Assumptions: none.

## 4. Proposed approach

- MainGM check remains the literal first statement of `ApplyItemDefinitionMigration`, before any argument validation that could touch I/O (there is none) and before `OpenConnection` is ever called.
- Idempotency: a lightweight pre-backup check against the new `ItemDefinitionMigrationCommandLedger` table (plus a cross-ledger collision check against every other existing ledger table and `AppliedCommands`, so a `CommandId` reused across unrelated operations is rejected as an identity mismatch, not silently treated as a fresh migration) short-circuits BEFORE any backup is created, per the ТЗ's own explicit ordering requirement. The apply transaction itself additionally routes through `SqliteSavingPipeline.Execute`, whose own `AppliedCommands`-based replay check guards the narrower window where a race could insert the same `CommandId` during the backup call.
- Backup: `IBackupRepository.CreateBackup(campaign, reason, correlationId)` is called on its own connection (via the injected/default `IBackupRepository`), strictly before the apply transaction opens; its failure aborts the whole operation with the transaction never opened. `reason = "pre-migration:" + sourceDefinitionId + "->" + targetDefinitionId` (90 chars, under the 96-char limit, still identifying both definitions in the backup list).
- Triple revision recheck (source definition, every affected inventory, every affected item/stack) is implemented as one `CheckMigrationState` helper, called BOTH before the backup (fail fast, avoid a wasted backup for a doomed migration) AND again inside the apply transaction (closing the narrower TOCTOU window during the backup call itself) — it rebuilds a fresh preview via `ItemDefinitionMigrationRules.BuildPreview` over freshly-read rows and compares its recomputed fingerprint against the original, so any drift in any field (not just the three named revision fields) is caught.
- Blocking-issue recheck: `CheckMigrationState` itself, at its own end, re-runs `ItemDefinitionMigrationRules.ComputeBlockingIssues` against freshly-read `EquippedEntry` rows (queried only for affected instances, matching `ComputeBlockingIssues`'s own precondition) — meaning both the pre-backup and in-transaction calls to `CheckMigrationState` already include this recheck. A blocking issue present by the time `Apply` is called is caught pre-backup (no backup wasted); a blocking issue introduced strictly during the backup call itself is caught only by the in-transaction call — proven by `TC-INVENTORY-187`'s test-only backup-wrapper race injection.
- Atomic update: for each affected instance, then each affected stack member, one CAS-guarded `UPDATE ... WHERE Id=$id AND CampaignId=$campaign AND InventoryId=$inventory AND Revision=$revision` inside the one apply transaction; any `ExecuteNonQuery() != 1` fails the whole `apply` callback, and since `SqliteSavingPipeline.Execute`'s own `using SqliteTransaction` is never committed on a `Result.Failure`, ADO.NET's own dispose-without-commit behavior rolls back everything already written in that same transaction.
- Audit event: `SqliteSavingPipeline.Execute`'s own `apply` callback returns a `PipelineWrite<ItemDefinitionMigrationApplyResult>` naming event type `"odyssey.persistence.item_definition_migrated"` and a JSON payload (via the same `Newtonsoft.Json`-based `JsonTextWriter` pattern already used elsewhere in this file) containing `sourceDefinitionRef`/`targetDefinitionRef`/`backupId`/`actorUserId`/updated counts.
- No rollback command: `ApplyItemDefinitionMigration`'s own doc comment states `ADR-027` §10 step 9 verbatim; no compensating/Undo type or method is introduced anywhere in the diff.
- Tests: one new real-SQLite test file, `ItemDefinitionMigrationApplyTests.cs`, covering all 10 scenarios in the task contract §7 (the 9 the ТЗ's own §10 named, plus one covering the `PreviewRevision` tamper-sanity check called out separately in §4 item 4). Setup mirrors `EquipmentRuntimeIntegrationFixtureTests.cs`'s established real-Character/real-Equip pattern for the two scenarios needing genuine equipment state (184/187); simulated concurrent drift (revision bumps) uses the same raw-SQL-column-bump technique `SqliteInventoryRepositoryTests.cs`'s own `SetInventoryRevisionDirectly` already established as this codebase's precedent for simulating "someone else changed this meanwhile," rather than inventing a new technique.
- Scope guards: point allow-list additions to the two guards the ТЗ's own §11 named, plus a third (`InventoryCreationServiceTests.cs`'s own table-name filter) discovered by running the full suite rather than assumed safe.

No change to `ItemDefinitionMigrationPreview`/`ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues`, `EquipItemCore<T>`/`UnequipItemCore<T>`/`EquipmentCommandLedger`, or `SqliteBackupRepository`'s own implementation.

## 5. Milestones

### M1 — Verify and correct the existing draft

- [x] Read the full existing uncommitted diff line-by-line against real precedent (table schemas, `SqliteSavingPipeline`, `PersistenceFailures`/`InventoryMovementFailures` members, `ComputeBlockingIssues` signature, `IBackupRepository`/`BackupRecord` shapes).
- [x] Fix the backup `reason` string to fit the 96-character limit while still identifying both definitions.
- [x] Fix the third (previously missed) scope guard in `InventoryCreationServiceTests.cs`.
- [x] Build the solution.

### M2 — Tests

- [x] Write `ItemDefinitionMigrationApplyTests.cs` covering all 10 scenarios.
- [x] Iterate against real failures (definition-type-unsupported for stacks, actor-identity mismatch on replay, incorrect backup-count expectations) until all 10 pass in isolation.
- [x] Run the full suite; confirm no regressions.

### M3 — Metadata, docs, validation, PR

- [x] Register `TC-INVENTORY-179`-`188` in `Tests/Metadata/test-catalog.json`.
- [x] Run all 5 required validation commands; record real results.
- [x] Rewrite the task contract and this ExecPlan to full depth with every verified fact and decision.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 to `In Review`.
- [x] Review `git diff --name-status` for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog status.

## 6. Progress log

- 2026-09-12 — Resumed from a prior session's uncommitted worktree at `D:\Game_Dev\Odyssey_VTT\ody-s05-403`, branch `codex/ody-s05-403-migration-apply`, based on `origin/main` at `b36246c` (confirmed via `git merge-base`).
- 2026-09-12 — Read the full existing diff (8 files) and verified every referenced precedent by direct code read: `SqliteSavingPipeline`, `PersistenceFailures`/`InventoryMovementFailures` member existence, `ItemInstance`/`ItemStack`/`ContentDefinition` table columns, `ComputeBlockingIssues` signature and precondition, `ReplaceEquippedEntry`'s scope of mutation, `IBackupRepository`/`BackupRecord`/`BackupId` shapes, `SqliteBackupRepository`'s 96-character reason limit. `dotnet build` already succeeded on the untouched draft.
- 2026-09-12 — Found and fixed the backup-reason length defect and the third scope-guard gap; `dotnet build` green again.
- 2026-09-12 — Wrote and iterated `ItemDefinitionMigrationApplyTests.cs`: 3 of 10 initial test runs failed (Armor cannot back a stack; replay must reuse the same actor; the pre-backup check already recomputes blocking issues, so backup count in the "blocking issue present at Apply time" scenario is 0, not 1). Redesigned the "blocking issue introduced between preview and confirm" scenario (`TC-INVENTORY-187`) to inject the change strictly inside a wrapped `IBackupRepository`'s own post-backup callback, since the simpler "equip differently before calling Apply" version is caught by the PRE-backup check and never proves the in-transaction recheck's own necessity.
- 2026-09-12 — Full suite green (Persistence 530/530, Unit 136/136, Architecture 2/2, Domain 80/80, Networking 67/67, Contracts 1/1). All 5 required validation commands PASS.
- 2026-09-12 — Rewrote the task contract and this ExecPlan to full 18/12-section depth.

## 7. Decisions

See task contract §18 for the full decision log: `SqliteSavingPipeline` reuse (correcting the ТЗ's own §8 premise); optional `IBackupRepository?` constructor parameter; dual-layer idempotency (dedicated ledger checked pre-backup + shared pipeline's `AppliedCommands` checked in-transaction); the corrected backup-reason format; `PreviewRevision` treated as a sanity check only, never a CAS substitute; the third scope guard discovery; and the backup-window-race test design for `TC-INVENTORY-187`.

## 8. Discoveries and deviations

- This ТЗ's own §8 premise ("study how `MergeItemStacks`/`SplitItemStack`/`EquipItem` write `DomainEvents`") does not hold: none of them do. See task contract §4/§18.
- This ТЗ's own §3 suggested backup-reason format would have exceeded `SqliteBackupRepository`'s real 96-character limit. See task contract §18.
- A third scope guard beyond this ТЗ's own §11 two named ones needed a point allow-list addition, found only by running the full test suite. See task contract §18.
- `TC-INVENTORY-187`'s literal scenario ("second of two affected items fails mid-loop, first rolls back") is architecturally unreachable through the public API in a single-threaded test, because the in-transaction `CheckMigrationState` recheck (including `ComputeBlockingIssues`) runs to completion BEFORE the per-row update loop even starts — meaning a blocking issue or revision drift is always caught before any row is written, never after some rows are written but before others. This is a genuinely safer design than the ТЗ's own literal scenario envisioned. `TC-INVENTORY-187` instead proves the closely related and still fully DoD-relevant property: a blocking issue introduced strictly during the backup's own time window (which the pre-backup check structurally cannot see) is still caught by the separate in-transaction recheck, and neither of two affected instances is written.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 530/530.
- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS (including the 3 new `ERROR_CODES.md` rows).
- `.\scripts\verify-test-structure.ps1`: PASS, exit code 0.
- Diff review: `git diff --name-status` confirmed only allowed paths changed; PR #132 opened as Draft.

## 10. Recovery and rollback

Rollback of THIS PR is a normal revert before merge. The migration operation itself is deliberately irreversible once confirmed (`ADR-027` §10 step 9); its only recovery path is the mandatory pre-migration backup this task creates, restorable via the existing, unmodified `IBackupRepository.RestoreBackup` — not re-tested here since that method is out of scope and already covered by its own existing test suite.

## 11. Open questions and blockers

None remain open for this task. Recorded for `ODY-S05-404` (not resolved here): the integration-fixture task's own composition of `BuildPreview` → `ComputeBlockingIssues` → `ApplyItemDefinitionMigration` end-to-end, mirroring `ODY-S05-207`/`306`'s own fixture pattern.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-404` — Migration Integration Fixtures, the final task in the `ItemDefinition` migration block.
