# ODY-S05-201 — Inventory Runtime Foundation

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-201-inventory-runtime-foundation`
**Pull request:** Not opened
**Last updated:** 2026-09-06 UTC (validation complete; PR pending)

## 1. Purpose and user-visible outcome

Introduce the smallest Domain/Application vocabulary future Inventory runtime tasks need: identities, owner/location refs, item/stack refs, mechanics snapshots, stack quantity, and immutable read records. This creates types, not a system.

## 2. Task contract

- Goal: add minimal Domain/Application Inventory runtime foundation contracts, tests, metadata, task contract, plan, and backlog status.
- Acceptance criteria: canonical IDs; valid owner/location/item refs; opaque mechanics snapshot; positive live stack quantity; Application records; guard tests proving no repository/SQLite/Character-section/equipment/attack/ActiveEffect/migration implementation; metadata and docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-201`, `SLICE-05`.
- In scope: `Packages/com.odyssey.domain/Runtime/Inventory/**`, `Packages/com.odyssey.application/Runtime/Inventory/**`, focused tests, test metadata, task/plan docs, backlog row.
- Out of scope: persistence, repository interface/implementation, commands, authorization, create-from-catalog flow, move/transfer, split/merge commands, equipment behavior, attack, item use, ActiveEffect runtime, ItemDefinition migration, Unity/UI, `.odcontent`, balanced content, ADR edits.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 7 / `ODY-S05-201`; `ADR-027` sections 5, 6, 7, 9, 14, 20; Domain Model sections 17.1-17.3; `ContentCatalog.cs`; Application contract patterns.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` includes merged PR #112 (`3cd44ea`), so `ODY-S05-107` is closed and the Inventory runtime backlog exists.
- `ODY-S05-201` is `Proposed` in `SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Content Catalog foundation provides `ContentDefinitionId`, `ContentDefinitionType`, and exact-version `ContentDefinitionRef`.
- No Inventory runtime Domain/Application types exist yet.
- No Inventory SQLite table/repository exists yet.

Assumptions: none.

## 4. Proposed approach

- Add one Domain file under `Odyssey.Domain.Inventory` for IDs and value objects.
- Add one Application file under `Odyssey.Application.Inventory` for immutable record shapes.
- Add focused Domain tests for IDs, owner/location refs, item refs, snapshot, and stack quantity.
- Add focused Unit/Application tests for record validation and guard rails.
- Register `TC-INVENTORY-001`-`008`.
- Update the backlog row only after PR opening so `ODY-S05-201` is `In Review` with the link.

No `IInventoryRepository`, SQLite schema, command service, equipment behavior, or migration surface is added.

## 5. Milestones

### M1 — Contracts

- [x] Add Domain Inventory value objects.
- [x] Add Application Inventory record contracts.
- [x] Build the solution.

### M2 — Tests and metadata

- [x] Add Domain tests for `TC-INVENTORY-001`-`006`.
- [x] Add Unit/Application tests for `TC-INVENTORY-007`-`008`.
- [x] Register test metadata.
- [x] Run `dotnet test`.

### M3 — Docs, validation, PR

- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [ ] Commit, push, and open Draft PR.
- [ ] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-06 — Preflight: fetched `origin --prune`, verified PR #112 merged into `origin/main`, verified `ODY-S05-201` backlog row is `Proposed`, and created `feat/ody-s05-201-inventory-runtime-foundation` from `origin/main`.
- 2026-09-06 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 7, `ADR-027` sections 5/6/7/9/14/20, Domain Model sections 17.1-17.3, `ContentCatalog.cs`, Application content contracts, canonical ID/test patterns, `TASK_TEMPLATE.md`, and `PLANS.md`.
- 2026-09-06 — Created task contract and ExecPlan before code changes.
- 2026-09-06 — Added `InventoryRuntime.cs` with runtime IDs, owner/location refs, item refs, mechanics snapshot, and stack quantity.
- 2026-09-06 — Added `InventoryRuntimeRecords.cs` with immutable Application read-record contracts only; no repository interface.
- 2026-09-06 — Added Domain/Unit tests and `TC-INVENTORY-001`-`008` test metadata.
- 2026-09-06 — Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, and `verify-test-structure.ps1`.

## 7. Decisions

- 2026-09-06 — Decision: `ItemStackQuantity` represents live stack quantity and rejects zero. Rationale: zero is only reached through future consume/destroy/remove command behavior, which is out of scope here. Authority: `ADR-027` section 6.2.
- 2026-09-06 — Decision: no `IInventoryRepository` in this task. Rationale: persistence and repository contracts belong to `ODY-S05-202`; records are enough for `ODY-S05-201`. Authority: user task brief and backlog row `ODY-S05-201`/`202`.
- 2026-09-06 — Decision: equipment appears only as a location kind. Rationale: future location vocabulary needs to name it, but equip/unequip behavior belongs to a later block. Authority: `ADR-027` section 7 and `ODY-S05-201` out-of-scope list.

## 8. Discoveries and deviations

- Initial sandboxed `dotnet build` failed before compilation because MSBuild could not access `C:\Users\alexx\AppData\Local\Microsoft SDKs`; the escalated rerun passed with 0 warnings and 0 errors.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors after sandbox-access rerun.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 74, Networking 67, Unit 134, Architecture 2, Persistence 353.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` and controlled invalid cases rejected.
- Diff review: no SQLite persistence/schema files, Unity files, accepted ADR files, Equipment/Attack/ActiveEffect implementation, or ItemDefinition migration workflow changed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR. No schema, migration, runtime persistence, build artifact, dependency, or data state is changed.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Pending. Next planned implementation task remains `ODY-S05-202 — Inventory Persistence Foundation`.
