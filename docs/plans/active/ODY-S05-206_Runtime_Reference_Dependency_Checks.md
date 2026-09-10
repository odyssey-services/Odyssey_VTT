# ODY-S05-206 — Runtime Reference Dependency Checks

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-206-runtime-reference-dependency-checks`
**Pull request:** Not opened
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
- [ ] Task contract + ExecPlan authored and committed before product code.

### M2 — Read primitives + checkers
- [ ] `HasAnyItemOwnedByCharacter` / `HasAnyRuntimeReferenceToDefinition` on `IInventoryRepository` + `SqliteInventoryRepository`.
- [ ] `InventoryCharacterDeletionDependencyChecker`, `IContentDefinitionDeletionDependencyChecker`, `InventoryContentDefinitionDependencyChecker`.
- [ ] `SqliteContentCatalogRepository` optional checker list + `DeleteDraftDefinition` call + new error code + registry row.

### M3 — Doc-comments + tests + evidence
- [ ] `RemoveBodyPart` and constructor doc-comments corrected; no behavior change.
- [ ] `TC-INVENTORY-079`–`090` added and registered.
- [ ] Five validation commands pass; contract §17 + this §9 filled; backlog row 6 updated; Draft PR opened.

## 6. Progress log

- 2026-09-11 UTC — Branched from `origin/main` `36d00e4`; read `ADR-027` §4.1/§5/§7/§9, `ADR-025` §5.2, backlog row 6 / §7.1 / §8, the character and catalog repositories, the inventory contracts, and the existing fake-checker test; authored this ExecPlan and the task contract.

## 7. Decisions

- 2026-09-11 — Both new checkers live in `Odyssey.Application.Inventory` (already depends on catalog + inventory contracts via `InventoryCreationService`), not a new `Runtime/Character/` folder. Authority: existing dependency precedent.
- 2026-09-11 — New error code named `persistence.content_definition.runtime_referenced` (not the ТЗ's suggested `content_catalog.delete.runtime_referenced`), to sit directly beside its sibling `persistence.content_definition.referenced` and match that registry family. Authority: ТЗ §6.4 ("add by analogy to the existing referenced check").

## 8. Discoveries and deviations

- _recorded during implementation_

## 9. Validation and acceptance evidence

- _filled with real command output at the end_

## 10. Recovery and rollback

- Additive only. Reverting the commits removes the new optional constructor parameter, interface, primitives, checkers, and error code; no persisted data is touched.

## 11. Open questions and blockers

- Whether `ADR-027` §9 should carry a short amendment note about the deferred `RemoveBodyPart` closure. `docs/adr/**` is outside allowed paths; recorded in the task contract §18 for the product owner.

## 12. Outcome and follow-up

- The deferred `RemoveBodyPart` equipment dependency and `ActiveEffect` deletion dependencies are anchored to the Equipment runtime block in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8; a task ID is assigned when that block is decomposed. `ODY-S05-207` (integration fixtures) is a separate future task.
