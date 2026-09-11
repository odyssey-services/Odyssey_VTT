# ODY-S05-206 — Runtime Reference Dependency Checks

**Status:** Done (PR #120, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-206-runtime-reference-dependency-checks`
**Pull request:** Draft — [#120](https://github.com/odyssey-services/Odyssey_VTT/pull/120)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

An irreversible `DeleteCharacterPermanently` fails closed while the character still owns Inventory items, and a Draft `ContentDefinition` physical delete has a real (executing, not commented-out) runtime-reference gate. The `RemoveBodyPart` item-dependency stub is honestly re-documented and deferred to the future Equipment runtime block.

## 2. Task contract

- Goal and acceptance: `docs/tasks/active/ODY-S05-206_Runtime_Reference_Dependency_Checks.md`.
- Authorities: `ADR-027` §4.1/§5/§7/§9, `ADR-025` §5.2, `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 6 / §7.1 / §8, `AGENTS.md`, `PLANS.md`.
- Scope: two `IInventoryRepository` read primitives + SQLite implementations; `InventoryCharacterDeletionDependencyChecker`; new `IContentDefinitionDeletionDependencyChecker` + `InventoryContentDefinitionDependencyChecker`; `SqliteContentCatalogRepository` optional checker list + `DeleteDraftDefinition` call; one error code; doc-comment corrections; tests; backlog/evidence.
- Non-goals: real `RemoveBodyPart` equipment dependency, `ActiveEffect` dependencies, any `ODY-S05-205` change, Unity/UI, a production composition root, behavior changes to existing delete logic.

## 3. Current state

- `origin/main` at `36d00e4` (PR #116); PR #119 (`ODY-S05-205`) is still Draft and irrelevant to this task.
- `SqliteCharacterRepository` already accepts and double-invokes `ICharacterDeletionDependencyChecker`s; none are real.
- `SqliteContentCatalogRepository(IWallClock)` has no checker parameter; `DeleteDraftDefinition` runs only `IsReferencedByAnotherDefinition`.
- Inventory tables carry `OwnerKind`/`OwnerTargetRef` and `SourceItemDefinitionRef`; no new column/table is needed.
- Equipment and `ActiveEffect` runtime do not exist.

## 4. Proposed approach

1. Documentation first: author this ExecPlan and the task contract, commit them before any product code.
2. Add `HasAnyItemOwnedByCharacter` and `HasAnyRuntimeReferenceToDefinition` to `IInventoryRepository`; implement in `SqliteInventoryRepository` as single `EXISTS` reads on a short-lived connection with the existing `try/catch → InventoryIoFailed` wrapper. `HasAnyRuntimeReferenceToDefinition` matches `SourceItemDefinitionRef LIKE '<escaped id>/%' ESCAPE '\'`.
3. Add `InventoryCharacterDeletionDependencyChecker : ICharacterDeletionDependencyChecker` in `Odyssey.Application.Inventory` — fail closed (non-null block string) when the probe returns `IsFailure`; block string carries only `Error.Code`.
4. Add `IContentDefinitionDeletionDependencyChecker` to `ContentCatalogRepositoryContracts.cs` and `InventoryContentDefinitionDependencyChecker` alongside the character checker. Give `SqliteContentCatalogRepository` an optional `IReadOnlyList<IContentDefinitionDeletionDependencyChecker>? runtimeDependencyCheckers = null` (default empty) and invoke each in `DeleteDraftDefinition` after `IsReferencedByAnotherDefinition`, before the `DELETE`; a non-null result → `PersistenceFailures.ContentDefinitionRuntimeReferenced` (new), nothing deleted, no ledger entry.
5. Add the `persistence.content_definition.runtime_referenced` error code + registry row.
6. Correct the `RemoveBodyPart` method-level and inline doc-comments and the `SqliteCharacterRepository` constructor doc-comment. No code path changes.
7. Tests `TC-INVENTORY-079`–`090`, catalog-side test seeds a runtime reference by direct SQL insert (Draft cannot be referenced through the public API). Register in `test-catalog.json`.
8. Run all five validation commands; fill contract §17 and this plan's §9 with real output; set backlog row 6 to `In Review (PR #NNN)`; open Draft PR.

## 5. Milestones

### M1 — Documentation baseline
- [x] Task contract + ExecPlan authored and committed before product code.

### M2 — Read primitives + checkers
- [x] `HasAnyItemOwnedByCharacter` / `HasAnyRuntimeReferenceToDefinition` on `IInventoryRepository` + `SqliteInventoryRepository`.
- [x] `InventoryCharacterDeletionDependencyChecker`, `IContentDefinitionDeletionDependencyChecker`, `InventoryContentDefinitionDependencyChecker`.
- [x] `SqliteContentCatalogRepository` optional checker list + `DeleteDraftDefinition` call + new error code + registry row.

### M3 — Doc-comments + tests + evidence
- [x] `RemoveBodyPart` and constructor doc-comments corrected; no behavior change.
- [x] `TC-INVENTORY-079`–`090` added and registered.
- [x] Five validation commands pass; contract §17 + this §9 filled; backlog row 6 updated; Draft PR opened.

## 6. Progress log

- 2026-09-11 UTC — Branched from `origin/main` `36d00e4`; read `ADR-027` §4.1/§5/§7/§9, `ADR-025` §5.2, backlog row 6 / §7.1 / §8, the character and catalog repositories, the inventory contracts, and the existing fake-checker test; authored this ExecPlan and the task contract.
- 2026-09-11 UTC — Implemented the two `IInventoryRepository` read primitives + `SqliteInventoryRepository` bodies; `InventoryCharacterDeletionDependencyChecker`, `IContentDefinitionDeletionDependencyChecker`, `InventoryContentDefinitionDependencyChecker`; `SqliteContentCatalogRepository` optional checker list + `DeleteDraftDefinition` call; `persistence.content_definition.runtime_referenced` code + factory + registry row; corrected the `RemoveBodyPart` and constructor doc-comments; wrote `TC-INVENTORY-079`–`090`. All five validation commands pass (§9); opened Draft PR [#120](https://github.com/odyssey-services/Odyssey_VTT/pull/120).

## 7. Decisions

- 2026-09-11 — Both new checkers live in `Odyssey.Application.Inventory` (already depends on catalog + inventory contracts via `InventoryCreationService`), not a new `Runtime/Character/` folder. Authority: existing dependency precedent.
- 2026-09-11 — New error code named `persistence.content_definition.runtime_referenced` (not the ТЗ's suggested `content_catalog.delete.runtime_referenced`), to sit directly beside its sibling `persistence.content_definition.referenced` and match that registry family. Authority: ТЗ §6.4 ("add by analogy to the existing referenced check").

## 8. Discoveries and deviations

- **`ICharacterDeletionDependencyChecker` / `IContentDefinitionDeletionDependencyChecker` carry no caller `CorrelationId`.** The internal Inventory read still needs one, so both checkers use a fixed placeholder (`corr_0…0`), the same convention `SerializationFailures` / `PersistenceFailures` already use for codec-level calls with no caller correlation.
- **Cross-connection write vs. WAL snapshot-busy.** A checker invoked *inside* `DeleteDraftDefinition`'s (or the character delete pipeline's) open transaction opens its own connection. If that connection committed a write (e.g. `EnsureInventoryTables` DDL on a campaign that never used Inventory), the outer transaction's later `DELETE` could hit `SQLITE_BUSY_SNAPSHOT`. Mitigation: every test that reaches a checker through a delete path pre-creates one `Inventory` row, so `EnsureInventoryTables` is a pure no-op and the checker performs only `SELECT`s — no snapshot change. A real campaign with characters that have inventories is in exactly that state. No production code needed to change; noted so a future test author keeps the invariant.
- **`CampaignRepositoryContracts.cs` added to allowed paths** for the single `PersistenceFailures.ContentDefinitionRuntimeReferenced` factory — see task contract §18.
- **Deferral, not closure, for `RemoveBodyPart`.** Confirmed in code that no `EquippedEntry`/`BodyPartRefs[]` structure exists; the stub cannot be closed here. Anchored to `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 (Equipment runtime) with no invented task number, per §7.1.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln` — passed, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln` — passed, 691 total, 0 failed (`Odyssey.Tests.Persistence` carries the 12 new `TC-INVENTORY-079`–`090`).
- `.\scripts\verify-format.ps1` — `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1` — `REPO-POLICY-001`–`005 PASS`, `Repository policy check passed.`
- `.\scripts\verify-test-structure.ps1` — `TC-ARCH-001` / `TC-ARCH-002 PASS`.

## 10. Recovery and rollback

- Additive only. Reverting the commits removes the new optional constructor parameter, interface, primitives, checkers, and error code; no persisted data is touched.

## 11. Open questions and blockers

- Whether `ADR-027` §9 should carry a short amendment note about the deferred `RemoveBodyPart` closure. `docs/adr/**` is outside allowed paths; recorded in the task contract §18 for the product owner.

## 12. Outcome and follow-up

- The deferred `RemoveBodyPart` equipment dependency and `ActiveEffect` deletion dependencies are anchored to the Equipment runtime block in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8; a task ID is assigned when that block is decomposed. `ODY-S05-207` (integration fixtures) is a separate future task.
