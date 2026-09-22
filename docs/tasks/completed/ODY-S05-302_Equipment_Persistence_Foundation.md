# ODY-S05-302 — Equipment Persistence Foundation

**Status:** Done (PR #124, merged into main)
**Roadmap stage / slice:** SLICE-05 (Equipment runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-302-equipment-persistence-foundation`
**Pull request:** [#124](https://github.com/odyssey-services/Odyssey_VTT/pull/124)
**ExecPlan:** `docs/plans/active/ODY-S05-302_Equipment_Persistence_Foundation.md`
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Give the `ODY-S05-301` `EquippedEntry` Domain type a SQLite persistence foundation: repository contracts and SQLite storage with transactional `CommandId` idempotency and CAS (`Revision`) optimistic concurrency on every mutating primitive, with no Equip/Unequip command semantics and no `RemoveBodyPart` check.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-301` introduced the `EquippedEntry` Domain type but intentionally did not persist it.
- Value or risk reduction: `ODY-S05-303`/`304` (Equip/Unequip command MVPs) can build their business logic on a small, tested storage boundary without inventing schema, idempotency, or CAS behavior themselves.
- Blocking or enabling relationship: unblocks `ODY-S05-303` (Equip Command MVP) and `ODY-S05-304` (Unequip Command MVP); `ODY-S05-305`/`306` depend transitively on both.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §12 row 2, §12.1.
- `docs/tasks/active/ODY-S05-301_Equipment_Runtime_Foundation.md` (predecessor: `EquippedEntry` Domain type).
- `docs/tasks/active/ODY-S05-202_Inventory_Persistence_Foundation.md` (structural template for this task).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 5, 7, and 14.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`.
- Existing Inventory persistence patterns: `SqliteInventoryRepository.cs` (`EnsureInventoryTables`, `MoveItem<T>`, `RunStackOperation`, ledger/replay helpers).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-302`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`100` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-101`-`117`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-302` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed.
- `origin/main` contains merge commit `af7a323`, PR #123 (`ODY-S05-301`).
- Branch `feat/ody-s05-302-equipment-persistence-foundation` was created from `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 2 (`ODY-S05-302`) is `Proposed` and scopes this task to persistence/idempotency/CAS, explicitly separate from Equip/Unequip command semantics.
- `EquippedEntry` (`Packages/com.odyssey.domain/Runtime/Inventory/EquippedEntry.cs`) is unchanged since `ODY-S05-301`: `InventoryId`, `ItemRef` (`InventoryItemRef`), `EquipmentSlotRef` (string), `BodyPartRefs` (`IReadOnlyList<BodyPartId>`), `EquippedByUserId` (`UserId`), `EquippedAt` (`UtcInstant`), `Revision` (`long`); `ToLocationRef()` derives `InventoryLocationRef.Equipped(InventoryId, EquipmentSlotRef)`. Its own doc-comment explicitly defers the `CampaignId` persistence decision to this task.
- `IInventoryRepository` (`InventoryRepositoryContracts.cs`) has no Equipment-related member; direct code read confirms its current 16 members are all Inventory/ItemInstance/ItemStack/dependency-check primitives.
- `SqliteInventoryRepository.EnsureInventoryTables` currently creates exactly `Inventory`, `ItemInstance`, `ItemStack`, `InventoryCommandLedger`, `InventoryMoveCommandLedger`, `InventoryStackCommandLedger`, and two list indexes — confirmed by direct code read, no Equipment table exists.
- `ItemInstanceId` uses prefix `iinst_`, `ItemStackId` uses prefix `istack_` (both 32-hex-char canonical ids) — confirmed in `InventoryRuntime.cs`; the two prefixes are disjoint, so `ItemInstanceId.ToString()`/`ItemStackId.ToString()` values can never collide, making a single string column a safe unique identity key across both item kinds.
- `BodyPartId.ValidPattern` is `^[A-Za-z][A-Za-z0-9_]{0,63}$` (`Anatomy.cs`) — no comma is a legal character, so a comma-joined list of `BodyPartId.ToString()` values round-trips through a single `TEXT` column without ambiguity or a JSON payload.
- `InventoryStackOperationServiceTests.cs`'s `ThrowingInventoryRepository` fake implements every current `IInventoryRepository` member; adding new members to the interface requires updating that fake too (the same cross-PR gap already fixed once during the ODY-S05-119/120 merge in this session).
- The existing `SqliteInventoryRepositoryTests.InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses` test (added by `ODY-S05-202`) asserts no SQLite table name or `Odyssey.Persistence.Sqlite` assembly type name contains the substring `"Equipment"` — this task's own `EquippedEntry` table/type names will fail that guard unless it is explicitly narrowed, the same rolling-scope-guard pattern already used in `InventoryRuntimeRecordTests.cs` for `ODY-S05-204`/`205`/`301`.

### Assumptions

- None.

## 5. Scope

### In scope

- Application `EquippedEntryRecord` (Domain `EquippedEntry` + `CampaignId`).
- `IInventoryRepository` extension: `CreateEquippedEntry`, `GetEquippedEntry`, `ReplaceEquippedEntry`, `DeleteEquippedEntry`, `ListEquippedEntries`.
- `SqliteInventoryRepository` implementation of the above.
- Tables: `EquippedEntry`, `EquipmentCommandLedger`.
- Idempotency ledger behavior for create/replace/delete primitives; CAS (`Revision`) optimistic concurrency for replace/delete.
- One list index needed to list equipped entries by `CampaignId + InventoryId`.
- Updating `ThrowingInventoryRepository` (`InventoryStackOperationServiceTests.cs`) to implement the five new interface members.
- Updating the existing `ODY-S05-202` no-Equipment-table/type guard test to admit this task's additions.
- New error codes for genuinely new conditions, registered in `docs/errors/ERROR_CODES.md`.
- Persistence tests and test metadata `TC-INVENTORY-101`-`117`.
- Task contract, ExecPlan, and backlog status updates.

### Out of scope

- Equip/Unequip command services (no `EquipmentCommandService`/`EquipmentMovementService`-style class).
- Authorization or MainGM/AssistantGM checks.
- Rule 4 (referenced body parts currently exist on the owning Character).
- `RemoveBodyPart` dependency closure (rule 5).
- Weapon/armor mechanical effects, attack pipeline, ActiveEffect.
- Changes to `EquippedEntry.cs` (the Domain type stays exactly as `ODY-S05-301` left it).
- `ArmorDefinition`/`WeaponDefinition`/`BodyPart`/`CharacterAnatomy` changes.
- Unity/UI.
- `.odcontent`.
- ADR edits.
- Moving `ODY-S05-201`-`207`'s own task/plan files to `completed/` (pre-existing, unrelated documentation-sync gap; out of this task's scope).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/**
Packages/com.odyssey.application/Runtime/Inventory/**
Packages/com.odyssey.persistence/Runtime/Sqlite/**
DotNet/Tests/Odyssey.Tests.Persistence/**
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/active/ODY-S05-302_Equipment_Persistence_Foundation.md
docs/plans/active/ODY-S05-302_Equipment_Persistence_Foundation.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: Application owns the repository port; Persistence owns the SQLite implementation and tables (`ADR-001`, `ADR-027` section 14).
- Authoritative-state and transaction boundary: `CommandId` is the idempotency key; ledger write and row write happen in one transaction (`ADR-002`, `ADR-012`). `Revision` is the CAS key for every mutating primitive.
- Serialization / compatibility boundary: store typed searchable fields as SQLite columns; `BodyPartRefs` is a comma-joined `TEXT` column, not an opaque JSON payload, since its elements are already canonical single-token ids (`ADR-003`).
- Time / RNG rule: repository timestamps come from injected `IWallClock`; no randomness beyond caller-provided IDs.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: reuse existing `Microsoft.Data.Sqlite`; no dependency changes (`ADR-011` v1.1).
- Security / privacy / redaction rule: no hidden campaign data, private docs, raw exceptions, or secrets in tests/docs.
- Performance or platform constraint: add only the one list index needed by acceptance.
- Other: repository does not validate rule 4 (body-part existence on the Character) or implement `RemoveBodyPart` dependency behavior; `ODY-S05-303`/`305` own those.

## 7. Expected behavior

### Scenario 1 — Equipped entry round-trip

**Given** a caller has an already-built `EquippedEntryRecord`
**When** `CreateEquippedEntry` stores it and `GetEquippedEntry` reads it back by `ItemRef`
**Then** the same inventory id, item ref, slot, body-part refs, equipped-by user, equipped-at timestamp, and revision round-trip.

### Scenario 2 — Rule 1 as a storage-level conflict

**Given** an item is already equipped (a row exists for its `ItemRefId`)
**When** a second, different `CommandId` calls `CreateEquippedEntry` for the same item
**Then** the repository rejects it with a dedicated conflict, and no second row or ledger entry for a different command is created.

### Scenario 3 — CAS-protected replace and delete

**Given** an equipped entry exists at a known `Revision`
**When** `ReplaceEquippedEntry` or `DeleteEquippedEntry` is called with a stale `Revision`
**Then** the repository rejects it as a revision conflict and the stored row is unchanged.

### Scenario 4 — Idempotent replay

**Given** a create, replace, or delete call is replayed with the same `CommandId` and the same target/expected-revision identity
**When** the repository receives the replay
**Then** it returns the same outcome as the original call without re-applying the mutation.

### Required invariants

- Equipment persistence is a storage primitive only; it does not implement Equip/Unequip business rules, MainGM authorization, rule 4, or the `RemoveBodyPart` check.
- A reused `CommandId` for a different target/expected-revision identity is rejected with `CommandIdentityMismatch`.
- `EquippedEntry` rows are keyed by `ItemRefId`, physically preventing more than one equipped-state row per item (rule 1).
- List queries are scoped by `CampaignId + InventoryId`.
- Create/list calls reject a `CampaignId` that differs from the open `CampaignHandle.CampaignId`.
- Create calls require an existing parent `Inventory` row in the same campaign database.
- No Equip/Unequip command class, rule-4 check, or `RemoveBodyPart` behavior is introduced.

## 8. Deliverables

- Production code: `EquippedEntryRecord`, `IInventoryRepository` extension, `SqliteInventoryRepository` implementation.
- Tests: persistence tests for round-trip, rule-1 conflict, CAS conflict/replay on replace and delete, campaign-boundary/parent-inventory guards, list scoping, schema/type guard.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata, error registry.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; the table is created by the repository `EnsureInventoryTables` pattern for this foundation task.

## 9. Acceptance criteria

1. `EquippedEntryRecord` exists, composing the Domain `EquippedEntry` with a `CampaignId`.
2. `IInventoryRepository` gains `CreateEquippedEntry`, `GetEquippedEntry`, `ReplaceEquippedEntry`, `DeleteEquippedEntry`, `ListEquippedEntries`.
3. `SqliteInventoryRepository` stores `EquippedEntry` and `EquipmentCommandLedger` tables in `campaign.db`.
4. An equipped entry round-trips through SQLite unchanged.
5. Creating a second equipped entry for an already-equipped item (different `CommandId`) is rejected without creating a duplicate row.
6. `ReplaceEquippedEntry`/`DeleteEquippedEntry` with a stale `Revision` is rejected as a revision conflict and the stored row is unchanged.
7. Replaying create/replace/delete with the same `CommandId` and matching target/expected-revision identity returns the same outcome without re-applying the mutation.
8. Reusing the same `CommandId` for a different target or expected revision is rejected with `CommandIdentityMismatch`.
9. Create/list calls reject a `CampaignId` that differs from the open `CampaignHandle.CampaignId`.
10. Create calls reject a missing parent `InventoryId` as a `Result.Failure`, not a raw SQLite/provider failure.
11. List queries return only rows for the requested `CampaignId + InventoryId`.
12. Every ledger write and its corresponding row write happen in one SQLite transaction.
13. Physical schema contains only the allowed Equipment tables/index added by this task, with no Equip/Unequip command class, rule-4 check, or `RemoveBodyPart` behavior.
14. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-101`-`117`.
15. Task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md` are updated.
16. Required validation commands pass and diff review confirms no Unity files, ADR edits, `EquippedEntry.cs` changes, command services, or `RemoveBodyPart`/rule-4 behavior.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-101` | .NET / NUnit (Persistence) | CreateEquippedEntry (ItemInstance) then GetEquippedEntry round-trip | Pass |
| `TC-INVENTORY-102` | .NET / NUnit (Persistence) | CreateEquippedEntry (ItemStack) then GetEquippedEntry round-trip | Pass |
| `TC-INVENTORY-103` | .NET / NUnit (Persistence) | CreateEquippedEntry rejects record/campaign boundary mismatch | Pass |
| `TC-INVENTORY-104` | .NET / NUnit (Persistence) | CreateEquippedEntry rejects a missing parent Inventory row as a Result failure | Pass |
| `TC-INVENTORY-105` | .NET / NUnit (Persistence) | CreateEquippedEntry for an already-equipped item (different CommandId) is rejected; no duplicate row | Pass |
| `TC-INVENTORY-106` | .NET / NUnit (Persistence) | CreateEquippedEntry replay with the same CommandId returns the current stored record and does not duplicate | Pass |
| `TC-INVENTORY-107` | .NET / NUnit (Persistence) | CreateEquippedEntry reused CommandId for a different target is rejected | Pass |
| `TC-INVENTORY-108` | .NET / NUnit (Persistence) | ReplaceEquippedEntry updates slot/body-part refs under a correct revision guard | Pass |
| `TC-INVENTORY-109` | .NET / NUnit (Persistence) | ReplaceEquippedEntry with a stale revision is rejected as a revision conflict; row unchanged | Pass |
| `TC-INVENTORY-110` | .NET / NUnit (Persistence) | ReplaceEquippedEntry replay with the same CommandId returns the same outcome without double-applying | Pass |
| `TC-INVENTORY-111` | .NET / NUnit (Persistence) | DeleteEquippedEntry removes the row under a correct revision guard | Pass |
| `TC-INVENTORY-112` | .NET / NUnit (Persistence) | DeleteEquippedEntry with a stale revision is rejected as a revision conflict; row unchanged | Pass |
| `TC-INVENTORY-113` | .NET / NUnit (Persistence) | DeleteEquippedEntry replay with the same CommandId returns success without a second delete attempt | Pass |
| `TC-INVENTORY-114` | .NET / NUnit (Persistence) | ListEquippedEntries returns only rows for the requested campaign/inventory | Pass |
| `TC-INVENTORY-115` | .NET / NUnit (Persistence) | GetEquippedEntry for an unknown ItemRef returns not-found | Pass |
| `TC-INVENTORY-116` | .NET / NUnit (Persistence) | Physical SQLite schema contains only the allowed Equipment tables/index | Pass |
| `TC-INVENTORY-117` | .NET / NUnit (Persistence) | Equipment persistence introduces no Equip/Unequip command class or RemoveBodyPart behavior | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no Unity files, ADR edits, `EquippedEntry.cs` changes, command services, or `RemoveBodyPart`/rule-4 behavior.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Full migration rehearsal because this task follows the existing repository `EnsureInventoryTables` foundation pattern and does not add migration runner behavior.
- Authorization or command handler tests because command services are out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: adds Equipment persistence tables to `campaign.db` when the repository is used.
- Version fields affected: no manifest/application/schema version is bumped in this task.
- Migration or upcaster: none; no migration runner step is added.
- Forward / backward behavior: older builds will not use these tables; future tasks may add migration-managed evolution separately.
- Rollback method: revert this branch/PR before merge; after merge, remove the repository/table addition only through a reviewed follow-up.
- Data-loss risk and protection: low for this foundation; all mutations are transactional and CAS/idempotency-protected.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic Equipment test records; no real player data.
- Trust boundaries: repository receives already-built records from future Application commands; this task implements no user-facing trust boundary.
- Authorization / audience checks: out of scope.
- Redaction requirements: no diagnostics or network projections added.
- Log-safe fields: no logging added.
- Abuse / malformed input limits: record/repository parameter checks reject invalid IDs/refs/null payloads.
- Security tests: `CommandId` reuse mismatch rejection, rule-1 conflict rejection, CAS revision-conflict rejection.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task changes the Application port, adds persisted SQLite tables and CAS/idempotency behavior, and touches the Persistence implementation.
- ExecPlan path: `docs/plans/active/ODY-S05-302_Equipment_Persistence_Foundation.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: must follow merged PR #123 and precede `ODY-S05-303`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/errors/ERROR_CODES.md`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds foundation SQLite tables and Application repository port members; no manifest/schema version bump or protocol/ruleset change.
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

- `Packages/com.odyssey.application/Runtime/Inventory/InventoryRuntimeRecords.cs` — `EquippedEntryRecord`.
- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — `IInventoryRepository` Equipment primitives.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — `PersistenceFailures.EquipmentEntryNotFound`/`EquipmentEntryAlreadyEquipped`/`EquipmentEntryRevisionConflict`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — three new Equipment persistence error codes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — `EquippedEntry`/`EquipmentCommandLedger` tables, CAS/idempotency implementation of all five new primitives.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteEquipmentRepositoryTests.cs` (new) — `TC-INVENTORY-101`-`117`.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — `ThrowingInventoryRepository` fake updated with the five new interface members.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs` — two pre-existing `ODY-S05-202` schema/scope guards narrowed to admit this task's `EquippedEntry`/`EquipmentCommandLedger`/`IX_EquippedEntry_Campaign_Inventory` additions.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs` — one pre-existing scope guard narrowed to admit `EquipmentCommandLedger`.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-101`-`117` entries.
- `docs/errors/ERROR_CODES.md` — three new registry rows.
- This task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 450 (736 total, 0 failed). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` on first run (no missing error-registry entries). |
| `.\scripts\verify-test-structure.ps1` | Passed | Exit code 0; `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `EquippedEntryRecord` in `InventoryRuntimeRecords.cs`. |
| AC-2 | Passed | `IInventoryRepository` gains `CreateEquippedEntry`/`GetEquippedEntry`/`ReplaceEquippedEntry`/`DeleteEquippedEntry`/`ListEquippedEntries`. |
| AC-3 | Passed | `SqliteInventoryRepository.EnsureInventoryTables` creates `EquippedEntry` and `EquipmentCommandLedger`. |
| AC-4 | Passed | `TC-INVENTORY-101`-`102`. |
| AC-5 | Passed | `TC-INVENTORY-105`; uses `EquipmentEntryAlreadyEquipped`. |
| AC-6 | Passed | `TC-INVENTORY-109`, `TC-INVENTORY-112`; uses `EquipmentEntryRevisionConflict`. |
| AC-7 | Passed | `TC-INVENTORY-106`, `TC-INVENTORY-110`, `TC-INVENTORY-113`. |
| AC-8 | Passed | `TC-INVENTORY-107`; uses `CommandIdentityMismatch`. |
| AC-9 | Passed | `TC-INVENTORY-103` (create), `ListEquippedEntries` campaign-boundary check (shares `TryValidateCampaignBoundary`). |
| AC-10 | Passed | `TC-INVENTORY-104`; uses `PersistenceInventoryNotFound`. |
| AC-11 | Passed | `TC-INVENTORY-114`. |
| AC-12 | Passed | Every mutating primitive writes its target row and ledger row inside one `SqliteTransaction`. |
| AC-13 | Passed | `TC-INVENTORY-116`-`117`; diff review confirms no Equip/Unequip command class, rule-4, or `RemoveBodyPart` behavior. |
| AC-14 | Passed | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-101`-`117`. |
| AC-15 | Passed | This task contract, ExecPlan, and backlog updated with Draft PR link (filled after PR opens). |
| AC-16 | Passed | `git diff --name-status` review found no Unity files, ADR edits, `EquippedEntry.cs` changes, or Equip/Unequip/RemoveBodyPart/rule-4 behavior; only allowed paths touched. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- No Equip/Unequip command service, rule-4 check, or `RemoveBodyPart` dependency behavior is implemented by design — `ODY-S05-303`/`304`/`305`'s own jobs.
- `ReplaceEquippedEntry`/`DeleteEquippedEntry` perform no ownership/authorization/destination validation; they are physical CAS-protected primitives only.
- `ODY-S05-201`-`207`'s own task/plan files remain in `docs/tasks/active/`/`docs/plans/active/` despite `Done` backlog status — a pre-existing, unrelated documentation-sync gap, observed but not fixed here (out of this task's scope per this ТЗ).

### Follow-up tasks

- `ODY-S05-303` — Equip Command MVP.

### Self-review summary

- Scope review: diff limited to Application persistence contracts/records/results, SQLite persistence, tests, metadata, error registry, and task/plan/backlog docs; no Unity files, ADR edits, or `EquippedEntry.cs` changes.
- Architecture review: Application owns the port, Persistence owns SQLite; `CommandId` idempotency and `Revision` CAS enforced on every mutating primitive; rule 1 physically enforced via the `ItemRefId` primary key; no Equip/Unequip/rule-4/`RemoveBodyPart` behavior added.
- Test review: `TC-INVENTORY-101`-`117` added; three pre-existing scope guards (in `SqliteInventoryRepositoryTests.cs` x2, `InventoryCreationServiceTests.cs` x1) narrowed to admit exactly this task's new names; full `dotnet test` passed.
- Security/privacy review: no private material, hidden campaign data, secrets, logging, diagnostics, or network projections added.
- Documentation/version review: test metadata, error registry, task/plan/backlog updated; no application/schema/protocol/ruleset version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — **Design decision (required by this ТЗ §2, part а): add an Application-layer `EquippedEntryRecord` composing `CampaignId` + the existing Domain `EquippedEntry`, rather than duplicating `EquippedEntry`'s fields flatly or adding `CampaignId` to the Domain type itself.** `EquippedEntry.cs`'s own doc-comment explicitly defers this decision to `ODY-S05-302`. `InventoryRecord`/`ItemStackRecord` need `CampaignId` purely for repository-facing campaign-boundary scoping, a persistence concern `ADR-027` §14 does not assign to Domain. Composing over duplicating avoids re-validating fields `EquippedEntry`'s constructor already validates. Authority: `EquippedEntry.cs` doc-comment; `ADR-027` §14; direct comparison with `InventoryRecord`/`ItemStackRecord`.
- 2026-09-11 — **Design decision (required by this ТЗ §2, part в): extend `IInventoryRepository` in place rather than adding a sibling `IEquipmentRepository`.** Every prior Inventory-block persistence task (`ODY-S05-202`/`204`/`205`/`206`) added its new members directly to this one interface and to `SqliteInventoryRepository.cs`; a sibling interface would fragment a single campaign-scoped storage boundary for no benefit, since Equipment state is Inventory-owned per `ADR-027` §5. Authority: existing interface-extension precedent; `ADR-027` §5.
- 2026-09-11 — **Design decision (required by this ТЗ §2, part б): the `EquippedEntry` table's primary key is `ItemRefId` — the equipped item's own canonical id string (`ItemInstanceId` or `ItemStackId`), not `(InventoryId, EquipmentSlotRef)`.** `ItemInstanceId`/`ItemStackId` use disjoint canonical prefixes (`iinst_`/`istack_`), so a single string column is a safe, collision-free identity key across both item kinds. Keying by item identity directly makes rule 1 ("one item is in exactly one place") a physical primary-key constraint: a second `CreateEquippedEntry` for the same item can only ever be a `CommandId` replay or a genuine conflict, never a silent second row. Keying by `(InventoryId, EquipmentSlotRef)` instead would allow two different items to collide on the same slot key without any physical constraint catching it, and would not express rule 1 at all (rule 1 is about the item's place, not the slot's occupant). Authority: `ADR-027` §7 rule 1; direct inspection of `ItemInstanceId`/`ItemStackId` canonical prefixes.
- 2026-09-11 — Decision: add a dedicated `EquipmentCommandLedger` table rather than reusing `InventoryCommandLedger`. Rationale: every prior mutating-primitive group in this block (create, move, stack-split/merge) got its own ledger table; `EquipmentCommandLedger` needs an `ExpectedRevision` column the create-only `InventoryCommandLedger` does not carry, since Replace/Delete are CAS-protected transitions, not idempotent creates. Authority: existing `InventoryMoveCommandLedger`/`InventoryStackCommandLedger` precedent.
- 2026-09-11 — Decision: `ReplaceEquippedEntry` and `DeleteEquippedEntry` are physical CAS-protected primitives only (no source/destination validation, no ownership/authorization check, no rule-4 body-part-existence check) — the same division `ODY-S05-202`'s `MoveItem<T>` scaffolding could not fully anticipate is instead handled explicitly here per this ТЗ's own instruction that this task must supply the physical transition primitive even though the actual Equip/Unequip business rules belong to `ODY-S05-303`/`304`. Authority: this ТЗ §2's explicit requirement; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12.1's task-boundary text.
- 2026-09-11 — Decision: `BodyPartRefs` is stored as a single comma-joined `TEXT` column, not a JSON payload. Rationale: `BodyPartId`'s own canonical pattern forbids commas, so the join is unambiguous and avoids introducing a JSON-array convention this codebase does not otherwise use for typed-id lists. Authority: direct inspection of `BodyPartId.ValidPattern`.
- 2026-09-11 — Decision: reuse `PersistenceInventoryNotFound`, `PersistenceInventoryCampaignMismatch`, `CommandIdentityMismatch`, and `PersistenceInventoryIoFailed` for the conditions they already cover (missing parent Inventory row, campaign-boundary violation, ledger replay mismatch, I/O failure); add three new codes only for genuinely new conditions: `persistence.equipment_entry.not_found`, `persistence.equipment_entry.already_equipped` (rule-1 conflict), `persistence.equipment_entry.revision_conflict` (CAS loss on Replace/Delete). Authority: `ODY-S05-202`'s own precedent of reusing `CommandIdentityMismatch` rather than minting a duplicate code for an existing category; `check-repository-policy.ps1`'s requirement that every production `ErrorCode` be registered.
- 2026-09-11 — Decision: no `TryReplayCreateEquippedEntry`-style side-effect-free probe method is added in this task, unlike `TryReplayCreateItemInstance`/`TryReplayCreateItemStack`. Rationale: those probes exist because `InventoryCreationService` (a command service, `ODY-S05-203`) needs an early idempotency check before doing catalog validation work; no Equipment command service exists yet, so no consumer needs this probe. `ODY-S05-303` can add it if and when it needs one, exactly how `ODY-S05-203` added its own. Authority: direct inspection of `InventoryCreationService.cs`'s use of the existing probe methods; avoiding speculative unconsumed surface area.
- 2026-09-11 — Decision: update the existing `InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses` guard test (`ODY-S05-202`) to admit exactly this task's new table/type names, narrowing rather than removing the guard, the same rolling-scope-guard pattern already used across `ODY-S05-204`/`205`/`301`.
- 2026-09-11 — Decision: update `ThrowingInventoryRepository` (`InventoryStackOperationServiceTests.cs`) with throwing stubs for the five new `IInventoryRepository` members, applying the lesson already learned once this session (the ODY-S05-119/120 merge build gap) proactively rather than discovering it as a build break.
- 2026-09-11 — Discovery: two additional pre-existing scope guards broke, beyond the one anticipated in the ExecPlan. `SqliteInventoryRepositoryTests.InventorySchema_ContainsOnlyAllowedInventoryRuntimeTablesAndIndexes` matches any `sqlite_master` name containing `"Inventory"`, which also matches the new `IX_EquippedEntry_Campaign_Inventory` index name (scoped by `InventoryId`, not by table identity); narrowed its `allowed` list to include that one index name, with a comment explaining the new `EquippedEntry`/`EquipmentCommandLedger` tables themselves are covered by their own dedicated schema guard (`TC-INVENTORY-116`) instead. `InventoryCreationServiceTests.InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` forbids any table name containing `"Equipment"`; narrowed to exclude exactly `EquipmentCommandLedger` from that check via a documented `Where` filter, leaving every other forbidden fragment (`Transfer`, `Attack`, `ActiveEffect`, `ItemDefinitionMigration`) enforced unchanged.

### Approved task changes

- None.
