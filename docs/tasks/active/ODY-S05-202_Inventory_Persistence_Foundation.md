# ODY-S05-202 — Inventory Persistence Foundation

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Inventory runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-202-inventory-persistence-foundation`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/114
**ExecPlan:** `docs/plans/active/ODY-S05-202_Inventory_Persistence_Foundation.md`
**Created:** 2026-09-06
**Last updated:** 2026-09-06 17:45 UTC

## 1. Goal

Add the minimal SQLite persistence foundation for runtime Inventory state: repository contracts and SQLite storage for `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord`, with transactional `CommandId` idempotency and focused persistence tests.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-201` introduced Inventory runtime records but intentionally did not persist them.
- Value or risk reduction: future Inventory commands can build on a small, tested storage boundary without inventing schema or replay behavior.
- Blocking or enabling relationship: unblocks `ODY-S05-203` create-from-Published-definition command semantics and later movement/stack/equipment dependency tasks.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, row `ODY-S05-202`.
- `docs/tasks/active/ODY-S05-201_Inventory_Runtime_Foundation.md`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 5, 6, and 14.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`.
- `docs/adr/ADR-013_Migration_Runner_v1.0.md`.
- Existing SQLite repository and test patterns: `SqliteContentCatalogRepository`, `ContentCatalogRepositoryContracts`, and nearby persistence tests.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-202`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`009` as predecessor evidence.
- New test IDs to introduce: `TC-INVENTORY-010`-`019`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-202` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin --prune` completed.
- `origin/main` contains merge commit `2178249`, PR #113 (`ODY-S05-201`).
- Branch `feat/ody-s05-202-inventory-persistence-foundation` was created from `origin/main`.
- `ODY-S05-201` provides `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row `ODY-S05-202` scopes this task to SQLite persistence/contracts and explicitly excludes create-from-catalog command semantics, equipment commands, and attack pipeline.
- Existing content catalog persistence uses Application repository ports, `Microsoft.Data.Sqlite`, per-call short-lived connections, `CREATE TABLE IF NOT EXISTS`, transactional ledger rows, and `Result<T>` failures.

### Assumptions

- None.

## 5. Scope

### In scope

- Application `IInventoryRepository` port for create/get/list primitives over the `ODY-S05-201` records.
- SQLite `SqliteInventoryRepository` implementation.
- Tables: `Inventory`, `ItemInstance`, `ItemStack`, and `InventoryCommandLedger`.
- Idempotency ledger behavior for create primitives.
- Indexes needed to list instances/stacks by `CampaignId + InventoryId`.
- Persistence tests and test metadata `TC-INVENTORY-010`-`019`.
- Task contract, ExecPlan, and backlog status updates.

### Out of scope

- Command services.
- Authorization or MainGM/AssistantGM checks.
- Create-from-Published-definition behavior.
- Catalog validation/status checks.
- Move/transfer.
- Split/merge.
- Equip/unequip behavior.
- ActiveEffect.
- Attack pipeline.
- ItemDefinition migration.
- Unity/UI.
- `.odcontent`.
- ADR edits.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/**
Packages/com.odyssey.persistence/Runtime/Sqlite/**
DotNet/Tests/Odyssey.Tests.Persistence/**
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-202_Inventory_Persistence_Foundation.md
docs/plans/active/ODY-S05-202_Inventory_Persistence_Foundation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Assets/**
Any command service, equipment behavior, attack, ActiveEffect, or ItemDefinition migration implementation path
```

## 6. Technical constraints

- Module ownership and dependency direction: Application owns repository ports; Persistence owns SQLite implementation and tables (`ADR-001`, `ADR-027` section 14).
- Authoritative-state and transaction boundary: `CommandId` is the idempotency key; ledger write and row write happen in one transaction (`ADR-002`, `ADR-012`).
- Serialization / compatibility boundary: store typed searchable fields as SQLite columns and keep mechanics/runtime payloads opaque; no direct Domain aggregate serialization (`ADR-003`).
- Time / RNG rule: repository timestamps come from injected `IWallClock`; no randomness beyond caller-provided IDs.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: reuse existing `Microsoft.Data.Sqlite`; no dependency changes (`ADR-011` v1.1).
- Security / privacy / redaction rule: no hidden campaign data, private docs, raw exceptions, or secrets in tests/docs.
- Performance or platform constraint: add only list indexes needed by acceptance.
- Other: repository does not inspect catalog status or typed definition validity; `ODY-S05-203` owns creation command semantics.

## 7. Expected behavior

### Scenario 1 — Inventory round-trip

**Given** a caller has an already-built `InventoryRecord`  
**When** `CreateInventory` stores it and `GetInventory` reads it  
**Then** the same ID, campaign, owner ref, revision, and timestamps round-trip.

### Scenario 2 — Runtime item round-trip

**Given** a caller has already-built item instance and stack records with mechanics snapshots  
**When** the repository stores and reads them  
**Then** source definition refs, mechanics snapshot fields, runtime/stack state, quantity, location, revision, and timestamps round-trip without catalog inspection.

### Scenario 3 — Create idempotency

**Given** a create call is replayed with the same `CommandId` and same target  
**When** the repository receives the replay  
**Then** it returns the current stored record and does not create a duplicate.

### Required invariants

- Inventory persistence is a storage primitive only; it does not create items from catalog definitions.
- A reused `CommandId` for a different target is rejected with `CommandIdentityMismatch`.
- `ItemInstance`/`ItemStack` list queries are scoped by `CampaignId + InventoryId`.
- No equipment, attack, ActiveEffect, or ItemDefinition migration tables/classes are introduced.

## 8. Deliverables

- Production code: Application repository port and SQLite repository implementation.
- Tests: persistence tests for round-trip, list, idempotency, schema guard, and no catalog inspection.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; tables are created by repository `Ensure*Tables` pattern for this foundation task.

## 9. Acceptance criteria

1. `IInventoryRepository` exists with create/get/list primitives for `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord`.
2. `SqliteInventoryRepository` stores `Inventory`, `ItemInstance`, `ItemStack`, and `InventoryCommandLedger` tables in `campaign.db`.
3. Inventory, item instance, and item stack records round-trip through SQLite.
4. Item instance and item stack list queries return only rows for the requested `CampaignId + InventoryId`.
5. Create replay with the same `CommandId` and same target returns the current stored record and does not duplicate rows.
6. Reusing the same `CommandId` for a different target is rejected with `CommandIdentityMismatch`.
7. Ledger write and row write happen in one SQLite transaction.
8. Physical schema contains only the allowed Inventory runtime tables added by this task, with no speculative Equipment/ActiveEffect/Attack/ItemDefinitionMigration tables.
9. Repository code does not inspect catalog status, decode typed definitions, check permissions, create mechanics snapshots, or implement move/transfer/split/merge/equip/attack semantics.
10. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-010`-`019`.
11. Task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md` are updated.
12. Required validation commands pass and diff review confirms no Unity files, ADR edits, command services, equipment/attack/ActiveEffect/migration implementation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-010` | .NET / NUnit (Persistence) | Inventory create/get round-trip | Pass |
| `TC-INVENTORY-011` | .NET / NUnit (Persistence) | ItemInstance create/get round-trip | Pass |
| `TC-INVENTORY-012` | .NET / NUnit (Persistence) | ItemStack create/get round-trip | Pass |
| `TC-INVENTORY-013` | .NET / NUnit (Persistence) | List item instances by campaign inventory | Pass |
| `TC-INVENTORY-014` | .NET / NUnit (Persistence) | List item stacks by campaign inventory | Pass |
| `TC-INVENTORY-015` | .NET / NUnit (Persistence) | Same create `CommandId` replay returns current stored record and does not duplicate | Pass |
| `TC-INVENTORY-016` | .NET / NUnit (Persistence) | Same `CommandId` reused for another target is rejected | Pass |
| `TC-INVENTORY-017` | .NET / NUnit (Persistence) | Physical SQLite schema contains only allowed inventory runtime tables | Pass |
| `TC-INVENTORY-018` | .NET / NUnit (Persistence) | Schema/type guard confirms no Equipment/ActiveEffect/Attack/ItemDefinitionMigration implementation | Pass |
| `TC-INVENTORY-019` | .NET / NUnit (Persistence) | Repository does not inspect catalog status or typed definition validity | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no Unity files, ADR edits, command services, equipment/attack/ActiveEffect/migration implementation.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Full migration rehearsal because this task follows the existing repository `Ensure*Tables` foundation pattern and does not add migration runner behavior.
- Authorization or command handler tests because command services are out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: adds Inventory runtime tables to `campaign.db` when the repository is used.
- Version fields affected: no manifest/application/schema version is bumped in this task.
- Migration or upcaster: none; no migration runner step is added.
- Forward / backward behavior: older builds will not use these tables; future tasks may add migration-managed evolution separately.
- Rollback method: revert this branch/PR before merge; after merge, remove the repository/table addition only through a reviewed follow-up.
- Data-loss risk and protection: low for this foundation; all creates are transactional and idempotent.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic Inventory/item test records and opaque mechanics/runtime JSON payloads.
- Trust boundaries: repository receives already-authorized, already-built records from future Application commands; this task implements no user-facing trust boundary.
- Authorization / audience checks: out of scope.
- Redaction requirements: no diagnostics or network projections added.
- Log-safe fields: no logging added.
- Abuse / malformed input limits: record constructors and repository parameter checks reject invalid IDs/refs/null payloads.
- Security tests: `CommandId` reuse mismatch rejection and scope guards.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task changes Application port, persisted SQLite tables, idempotency, and Persistence implementation.
- ExecPlan path: `docs/plans/active/ODY-S05-202_Inventory_Persistence_Foundation.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: must follow merged PR #113 and precede `ODY-S05-203`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds foundation SQLite tables and Application repository port; no manifest/schema version bump or protocol/ruleset change.
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

- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — `IInventoryRepository` create/get/list storage port.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — SQLite Inventory runtime tables, mapping, idempotency ledger, and read/list implementation.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — Inventory persistence failure helpers.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — Inventory not-found/I/O persistence error codes.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs` — `TC-INVENTORY-010`-`019` persistence tests.
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` — stale `ODY-S05-201` guard narrowed so later persistence tasks are allowed while record-layer command/equipment/migration guards remain.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-010`-`019` entries and `TC-INVENTORY-008` wording aligned with post-202 scope.
- `docs/errors/ERROR_CODES.md` — required registry entries for new Inventory persistence error codes.
- This task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 363. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Final rerun passed after adding required Inventory persistence entries to `docs/errors/ERROR_CODES.md`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `IInventoryRepository` in `InventoryRepositoryContracts.cs`. |
| AC-2 | Passed | `SqliteInventoryRepository.EnsureInventoryTables` creates `Inventory`, `ItemInstance`, `ItemStack`, and `InventoryCommandLedger`. |
| AC-3 | Passed | `TC-INVENTORY-010`-`012`. |
| AC-4 | Passed | `TC-INVENTORY-013`-`014`. |
| AC-5 | Passed | `TC-INVENTORY-015`. |
| AC-6 | Passed | `TC-INVENTORY-016`; uses `ErrorCodes.CommandIdentityMismatch`. |
| AC-7 | Passed | Create methods write target row and ledger row inside one `SqliteTransaction`. |
| AC-8 | Passed | `TC-INVENTORY-017`-`018`. |
| AC-9 | Passed | `TC-INVENTORY-019`; diff review confirms no command/equipment/attack/ActiveEffect/migration implementation. |
| AC-10 | Passed | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-010`-`019`. |
| AC-11 | Passed | This task contract, ExecPlan, and backlog are updated with Draft PR [#114](https://github.com/odyssey-services/Odyssey_VTT/pull/114). |
| AC-12 | Passed | `git diff --name-status`/`git status --short` review found no Unity files or ADR edits, and no command services/equipment/attack/ActiveEffect/migration implementation. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- `ODY-S05-203` owns create-from-Published-definition command semantics.
- This task adds repository primitives only; there is still no inventory command service, permission check, move/transfer, split/merge, equip/unequip, attack, ActiveEffect, or ItemDefinition migration behavior.

### Follow-up tasks

- `ODY-S05-203` — create-from-Published-definition command semantics.

### Self-review summary

- Scope review: diff limited to Application persistence contracts/results, SQLite persistence, tests, metadata, error registry, and task/plan/backlog docs; no Unity files or ADR edits.
- Architecture review: Application owns the port, Persistence owns SQLite; `CommandId` idempotency ledger and record writes share one transaction; catalog definition status/typed decoding stay out of repository.
- Test review: `TC-INVENTORY-010`-`019` added and full `dotnet test` passed.
- Security/privacy review: no private material, hidden campaign data, secrets, logging, diagnostics, or network projections added.
- Documentation/version review: test metadata, error registry, task/plan/backlog updated; no application/schema/protocol/ruleset version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-06 — Decision: use the existing `CommandIdentityMismatch` error code for reused Inventory create `CommandId` with a different target. Authority / approval: task brief allows the existing convention; `ADR-002` defines reused command identity mismatch.
- 2026-09-06 — Decision: do not add migration runner steps or bump schema version in this foundation task. Authority / approval: existing SQLite foundation repository pattern and task scope.
- 2026-09-06 — Decision: update `docs/errors/ERROR_CODES.md` for new Inventory persistence not-found/I/O error codes. Authority / approval: repository policy checker requires production `ErrorCode` registry completeness.

### Approved task changes

- None.
