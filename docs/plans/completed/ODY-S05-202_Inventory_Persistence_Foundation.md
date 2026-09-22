# ODY-S05-202 — Inventory Persistence Foundation

**Status:** Done (PR #114, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-202-inventory-persistence-foundation`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/114
**Last updated:** 2026-09-06 18:10 UTC

## 1. Purpose and user-visible outcome

Persist the Inventory runtime records introduced by `ODY-S05-201` in the campaign SQLite database, with create replay protection and read/list primitives ready for future Application commands.

## 2. Task contract

- Goal: add minimal SQLite persistence foundation for `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord`.
- Acceptance criteria: Application port; SQLite repository and tables; round-trip/list/idempotency tests; campaign-boundary and parent Inventory guards; no catalog status/typed decoding/permissions/commands/equipment/attack/ActiveEffect/migration behavior; metadata/docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-202`, `SLICE-05`.
- In scope: Application persistence contract, `SqliteInventoryRepository`, `Inventory`/`ItemInstance`/`ItemStack`/`InventoryCommandLedger` tables, focused persistence tests, test metadata, task/plan/backlog docs.
- Out of scope: command services, authorization, catalog validation/status checks, create-from-Published-definition semantics, move/transfer, split/merge, equip/unequip, ActiveEffect, attack pipeline, ItemDefinition migration, Unity/UI, `.odcontent`, ADR edits.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row `ODY-S05-202`; `ODY-S05-201` task contract; `ADR-027` sections 5/6/14; `ADR-002`; `ADR-003`; `ADR-011`; `ADR-012`; `ADR-013`; existing content catalog SQLite patterns.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` contains merge commit `2178249`, PR #113 (`ODY-S05-201`).
- `ODY-S05-201` provides Domain IDs/value refs and Application immutable read records.
- No `IInventoryRepository`, Inventory SQLite table, or Inventory persistence tests exist yet.
- Existing content catalog persistence already demonstrates the repository-port + SQLite implementation + ledger-in-transaction pattern.
- The current branch is `feat/ody-s05-202-inventory-persistence-foundation`.

Assumptions: none.

## 4. Proposed approach

- Add one Application port file, `InventoryRepositoryContracts.cs`, with create/get/list methods over existing records and `CommandId`.
- Add one SQLite repository file, `SqliteInventoryRepository.cs`, using per-call connections, `EnsureInventoryTables`, typed columns for IDs/refs/revisions/timestamps, and opaque JSON payload columns for mechanics/runtime state.
- Use one `InventoryCommandLedger` keyed by `CommandId`, storing target kind/id and applied timestamp. On replay, select the current row for the recorded target; on different target/kind, return `CommandIdentityMismatch`.
- Add only two list indexes: item instances by `(CampaignId, InventoryId)` and item stacks by `(CampaignId, InventoryId)`.
- Add one focused persistence test fixture with `TC-INVENTORY-010`-`026`.
- Update metadata, task contract, ExecPlan, and backlog. After opening the Draft PR, update the PR link and `ODY-S05-202` backlog status.

No command handler, catalog lookup, typed-definition decode, equipment behavior, attack behavior, ActiveEffect runtime, or ItemDefinition migration code is added.

## 5. Milestones

### M1 — Contracts and schema

- [x] Add `IInventoryRepository`.
- [x] Add `SqliteInventoryRepository` tables, mapping, and ledger.
- [x] Build the solution.

### M2 — Persistence tests and metadata

- [x] Add tests for `TC-INVENTORY-010`-`026`.
- [x] Register test metadata.
- [x] Run `dotnet test`.

### M3 — Docs, validation, PR

- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-06 17:26 UTC — Preflight: fetched `origin --prune`, verified `origin/main` contains merge commit `2178249` for PR #113, and created `feat/ody-s05-202-inventory-persistence-foundation` from `origin/main`.
- 2026-09-06 17:26 UTC — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row `ODY-S05-202`, `ODY-S05-201` task/plan, `ADR-027` sections 5/6/14, `ADR-002`, `ADR-003`, `ADR-011`, `ADR-012`, `ADR-013`, content catalog persistence contracts/repository/tests, `TASK_TEMPLATE.md`, and `PLANS.md`.
- 2026-09-06 17:26 UTC — Created task contract and ExecPlan before production code changes.
- 2026-09-06 — Added `IInventoryRepository`, `SqliteInventoryRepository`, Inventory persistence error helpers/codes, `TC-INVENTORY-010`-`019`, and test metadata.
- 2026-09-06 — Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, and `verify-test-structure.ps1`.
- 2026-09-06 17:45 UTC — Opened Draft PR #114 and updated task contract, ExecPlan, and backlog with the PR link.
- 2026-09-06 18:10 UTC — Amended PR #114 with campaign-boundary guards, parent Inventory existence checks, SQLite InventoryId foreign keys, `TC-INVENTORY-020`-`026`, and updated traceability/error docs.

## 7. Decisions

- 2026-09-06 — Decision: reuse the existing `CommandIdentityMismatch` error code for Inventory create `CommandId` reused against a different target. Rationale: the task explicitly allows existing convention and no new code is needed for a distinct user-facing category. Authority: task brief and `ADR-002`.
- 2026-09-06 — Decision: use repository `EnsureInventoryTables` rather than migration runner wiring. Rationale: this matches current foundation-level SQLite repository pattern; `ADR-013` migration-runner workflow is not requested by this task. Authority: existing `SqliteContentCatalogRepository` pattern and task scope.
- 2026-09-06 — Decision: register new Inventory persistence not-found/I/O error codes in `docs/errors/ERROR_CODES.md`. Rationale: repository policy requires every production `ErrorCode` literal to be machine-checkable. Authority: `check-repository-policy.ps1`.
- 2026-09-06 — Decision: add `persistence.inventory.campaign_mismatch` for Inventory persistence record/list calls that cross the open campaign boundary. Rationale: no existing error code represented this safe validation failure cleanly. Authority: PR #114 owner-review amendment.
- 2026-09-06 — Decision: reuse `persistence.inventory.not_found` for missing parent Inventory rows and add compatible SQLite foreign keys from item tables to `Inventory`. Rationale: the parent row is the target prerequisite, and ADR-011 already enables SQLite foreign keys per connection. Authority: PR #114 owner-review amendment and existing SQLite patterns.

## 8. Discoveries and deviations

- Initial `dotnet test` failed because the `ODY-S05-201` guard test globally rejected future Inventory persistence; narrowed that guard to the original Domain/Application record files while preserving command/equipment/attack/ActiveEffect/migration checks.
- Initial `dotnet test` also failed because the schema guard counted SQLite internal `sqlite_autoindex_*` entries; the guard now ignores internal autoindexes and checks only explicit Inventory runtime tables/indexes.
- Initial `check-repository-policy.ps1` failed until the new Inventory persistence error codes were registered in `docs/errors/ERROR_CODES.md`.
- PR #114 owner review found that repository calls could accept a record/list `CampaignId` that did not match the open campaign and item rows could be attempted before their parent Inventory row existed. The amendment adds explicit `Result.Failure` guards and FK schema evidence without adding command/update/delete behavior.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 370.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` and controlled invalid cases rejected.
- Diff review: no Unity files, accepted ADR edits, command services, equipment/attack/ActiveEffect implementation, or ItemDefinition migration workflow changed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. Repository create calls write each target row and its ledger row in one SQLite transaction; a pre-commit failure leaves neither row durable. No migration runner step or irreversible data migration is introduced.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/114. `ODY-S05-203` remains the owner of create-from-Published-definition command semantics.
