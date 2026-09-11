# ODY-S05-302 — Equipment Persistence Foundation

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-302-equipment-persistence-foundation`
**Pull request:** [#124](https://github.com/odyssey-services/Odyssey_VTT/pull/124)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Give the `EquippedEntry` Domain type (`ODY-S05-301`) a SQLite persistence foundation: storage schema, idempotent create, CAS-protected replace/delete transition primitives, and list/get reads — with no Equip/Unequip command semantics and no `RemoveBodyPart` check. This creates a storage boundary, not a system, the same character as `ODY-S05-202` for Inventory.

## 2. Task contract

- Goal: add SQLite persistence for `EquippedEntry`-shaped state: an Application record wrapper, repository contract methods, SQLite tables, an idempotency ledger, and CAS-protected mutation primitives.
- Acceptance criteria: `EquippedEntryRecord` (Domain `EquippedEntry` + `CampaignId`) exists; `IInventoryRepository` gains Create/Get/Replace/Delete/List primitives for it; SQLite schema stores exactly this state plus its own ledger; every mutating primitive is CAS-protected on `Revision`; create/replace/delete are idempotent by `CommandId`; campaign-boundary and parent-Inventory guards match the `ODY-S05-202` pattern; no Equip/Unequip command service, no rule-4 check, no `RemoveBodyPart` behavior; metadata and docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-302`, `SLICE-05`.
- In scope: `Packages/com.odyssey.application/Runtime/Persistence/**`, `Packages/com.odyssey.application/Runtime/Inventory/**`, `Packages/com.odyssey.persistence/Runtime/Sqlite/**`, `DotNet/Tests/Odyssey.Tests.Persistence/**`, test metadata, error registry, task/plan/backlog docs.
- Out of scope: Equip/Unequip command services, rule 4 (body-part-existence) check, `RemoveBodyPart` dependency closure, `EquippedEntry.cs` changes (domain type stays as `ODY-S05-301` left it), ADR edits, Unity/UI, `Character/**`, `Content/**`.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 2 / §12.1; `ADR-027` §5/§7/§14; `ODY-S05-202`'s own task contract/ExecPlan (structural template); `EquippedEntry.cs`, `InventoryRuntimeRecords.cs`, `InventoryRepositoryContracts.cs`, `SqliteInventoryRepository.cs`, `CampaignRepositoryContracts.cs`, `ErrorCodes.cs`.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `af7a323` (merge of PR #123, `ODY-S05-301`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 2 (`ODY-S05-302`) is `Proposed`.
- `EquippedEntry` (Domain, `Odyssey.Domain.Inventory`) exists with no persistence, no `CampaignId`, no repository contract, no SQLite table.
- `IInventoryRepository` has no Equipment-related method.
- `SqliteInventoryRepository.EnsureInventoryTables` creates `Inventory`, `ItemInstance`, `ItemStack`, `InventoryCommandLedger`, `InventoryMoveCommandLedger`, `InventoryStackCommandLedger`, and two list indexes — no Equipment table.
- `ODY-S05-202`'s `MoveItem<T>`/stack-operation methods are the direct CAS/idempotency precedent: per-call connection, `EnsureInventoryTables`, one `SqliteTransaction`, a dedicated ledger table keyed by `CommandId`, `UPDATE ... WHERE Id=$id AND Revision=$expected` as the CAS predicate, rollback on `ExecuteNonQuery() != 1`.
- `ItemInstanceId`/`ItemStackId` use distinct canonical prefixes (`iinst_`/`istack_`), so a string built from either is unique across both spaces — usable as a single-column identity key for equipped state, matching rule 1 ("one item is in exactly one place") as a physical primary key.
- `BodyPartId`'s canonical pattern (`^[A-Za-z][A-Za-z0-9_]{0,63}$`) cannot contain a comma, so a comma-joined `BodyPartRefs` column round-trips safely without a JSON payload.

Assumptions: none.

## 4. Proposed approach

- Add `EquippedEntryRecord` to `InventoryRuntimeRecords.cs`: a sealed Application class composing `CampaignId` + the existing Domain `EquippedEntry` (not duplicating its fields), the same reason `InventoryRecord`/`ItemStackRecord` need a `CampaignId` the Domain type itself does not carry.
- Extend `IInventoryRepository` in place (matching the `ODY-S05-206` precedent of adding new members to the existing interface/file rather than a sibling interface) with: `CreateEquippedEntry`, `GetEquippedEntry`, `ReplaceEquippedEntry`, `DeleteEquippedEntry`, `ListEquippedEntries`.
- Add one SQLite table `EquippedEntry` keyed by `ItemRefId` (the `ItemInstanceId`/`ItemStackId` string, unique across both spaces) plus `ItemRefKind`, `CampaignId`, `InventoryId`, `EquipmentSlotRef`, `BodyPartRefs` (comma-joined), `EquippedByUserId`, `EquippedAt`, `Revision`, `CreatedAt`, `UpdatedAt`, with a foreign key to `Inventory` and one `(CampaignId, InventoryId)` list index — added into the existing `EnsureInventoryTables` script, matching how every prior task added its own tables to that one method rather than a separate `Ensure*` method.
- Add one dedicated `EquipmentCommandLedger` table (`CommandId` primary key, `OperationKind`, `ItemRefId`, `ExpectedRevision`, `CreatedAt`, `AppliedAt`) — its own ledger, not reusing `InventoryCommandLedger`, matching how `InventoryMoveCommandLedger`/`InventoryStackCommandLedger` each got their own table rather than sharing one.
- `CreateEquippedEntry`: CAS via ledger replay (`OperationKind="Create"`, `ExpectedRevision=0`); rejects a second create for an already-equipped `ItemRefId` (rule 1, physically a primary-key conflict) with a dedicated `EquipmentEntryAlreadyEquipped` error, distinct from `CommandIdentityMismatch`.
- `ReplaceEquippedEntry`/`DeleteEquippedEntry`: CAS on `Revision`, ledger-replayable by `(OperationKind, ItemRefId, ExpectedRevision)`, mirroring `MoveItem<T>`'s `UPDATE ... WHERE ... AND Revision=$expected` pattern. These are the physical "equip a different slot" / "unequip" primitives `ODY-S05-303`/`304` will call; no ownership/body-part/authorization validation is added here.
- `GetEquippedEntry`/`ListEquippedEntries`: plain reads, campaign-scoped the same way `GetItemInstance`/`ListItemInstances` are.
- Reuse existing error codes (`PersistenceInventoryNotFound` for a missing parent Inventory row, `PersistenceInventoryCampaignMismatch` for a campaign-boundary violation, `CommandIdentityMismatch` for a ledger replay mismatch, `PersistenceInventoryIoFailed` for I/O failures — the same codes `ItemInstance`/`ItemStack` persistence already reuses); add three new codes for genuinely new conditions: `EquipmentEntryNotFound`, `EquipmentEntryAlreadyEquipped`, `EquipmentEntryRevisionConflict`.
- Add `TC-INVENTORY-101`–`117` (continuing the series `ODY-S05-301` established) covering round-trip, campaign-boundary/parent-inventory guards, rule-1 conflict, CAS conflict and replay for all three mutating primitives, list scoping, and a narrowed schema/type guard (mirroring `ODY-S05-202`'s own schema-allowlist and no-command-behavior guards).
- Update the existing `InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses` guard test (added by `ODY-S05-202`) to admit exactly this task's new `EquippedEntry`/`EquipmentCommandLedger` tables and `EquippedEntryRecord`/repository-method additions, the same rolling-scope-guard pattern already used across this whole block.
- Update the backlog row only after PR opening so `ODY-S05-302` is `In Review` with the link.

No command handler, MainGM/authorization check, rule-4 body-part-existence check, or `RemoveBodyPart` dependency behavior is added.

## 5. Milestones

### M1 — Contracts and schema

- [x] Add `EquippedEntryRecord`.
- [x] Extend `IInventoryRepository` with Equipment primitives.
- [x] Add `SqliteInventoryRepository` Equipment table, ledger, and CAS-protected implementations.
- [x] Register new error codes.
- [x] Build the solution.

### M2 — Persistence tests and metadata

- [x] Add tests for `TC-INVENTORY-101`–`117`.
- [x] Update the existing `ODY-S05-202` no-Equipment-table/type guard tests to admit this task's additions (two guards, not the one originally anticipated — see discoveries).
- [x] Register test metadata.
- [x] Run `dotnet test`.

### M3 — Docs, validation, PR

- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #123 merged and `origin/main` at `af7a323`, verified `ODY-S05-302` backlog row is `Proposed`, created `feat/ody-s05-302-equipment-persistence-foundation` from `origin/main`.
- 2026-09-11 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1, `ADR-027` §5/§7/§14, `ODY-S05-202` task contract/ExecPlan in full, `EquippedEntry.cs`, `InventoryRuntimeRecords.cs`, `InventoryRepositoryContracts.cs`, `SqliteInventoryRepository.cs` (schema, `MoveItem<T>`, ledger/replay helpers, select/read mapping), `CampaignRepositoryContracts.cs`, `ErrorCodes.cs`, `SqliteInventoryRepositoryTests.cs`, `docs/errors/ERROR_CODES.md`.
- 2026-09-11 — Chose: Application-level `EquippedEntryRecord` composing the Domain `EquippedEntry` plus `CampaignId` (not duplicating fields); extend `IInventoryRepository` in place; `ItemRefId` (the item's own canonical id string) as the Equipment table's primary key, physically enforcing rule 1; a dedicated `EquipmentCommandLedger`. Full reasoning recorded in task contract §18.
- 2026-09-11 — Implemented `EquippedEntryRecord`, the five `IInventoryRepository` Equipment primitives, `EquippedEntry`/`EquipmentCommandLedger` tables, and three new error codes; build passed on first attempt.
- 2026-09-11 — Added `SqliteEquipmentRepositoryTests.cs` (`TC-INVENTORY-101`-`117`); all 17 passed on first run. Running the full suite surfaced two pre-existing scope guards this task's new names broke (beyond the one anticipated): `SqliteInventoryRepositoryTests.InventorySchema_ContainsOnlyAllowedInventoryRuntimeTablesAndIndexes` (the new list index's name contains "Inventory") and `InventoryCreationServiceTests.InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` (forbids any table name containing "Equipment"); narrowed both, then full suite passed: 736 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 450).
- 2026-09-11 — Registered `TC-INVENTORY-101`-`117` and the three new error codes in `docs/errors/ERROR_CODES.md`. Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1` (passed on first run), `verify-test-structure.ps1`. Diff review confirmed only allowed paths touched.
- 2026-09-11 — Committed, pushed `feat/ody-s05-302-equipment-persistence-foundation`, opened Draft PR [#124](https://github.com/odyssey-services/Odyssey_VTT/pull/124). Doc-sync follow-up: updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 2 to `In Review (PR #124)` and this task contract/ExecPlan headers with the PR link.

## 7. Decisions

See task contract §18 for the full decision log (Application record composition; `IInventoryRepository` extension over a sibling interface; `ItemRefId` primary key; dedicated ledger; CAS design for Replace/Delete as physical transition primitives; comma-joined `BodyPartRefs` column; reused vs. new error codes; scope-guard update).

## 8. Discoveries and deviations

- Two pre-existing scope guards broke beyond the one guard anticipated in §4/§6: `SqliteInventoryRepositoryTests.InventorySchema_ContainsOnlyAllowedInventoryRuntimeTablesAndIndexes` (its `%Inventory%` name filter also matches the new `IX_EquippedEntry_Campaign_Inventory` index) and `InventoryCreationServiceTests.InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` (forbids any table name containing `"Equipment"`, which `EquipmentCommandLedger` matches). Both narrowed with an explanatory comment, mirroring the rolling-scope-guard pattern already used across this block; no other assertion in either test changed.
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 450 (736 total, 0 failed).
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed.` on the first run.
- `.\scripts\verify-test-structure.ps1`: passed with exit code 0 and `TC-ARCH-001 PASS valid ADR-001 graph passes`.
- Diff review: only `Packages/com.odyssey.application/Runtime/{Inventory,Persistence,Results}/**`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/**`, test metadata, error registry, and planning docs changed. No Unity/ADR/`EquippedEntry.cs`/Character/Content file changed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. Every mutating primitive writes its target row and ledger row inside one SQLite transaction; a pre-commit failure leaves neither row durable. No migration runner step or irreversible data migration is introduced.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-303` — Equip Command MVP.
