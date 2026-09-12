# ODY-S05-401 — Migration Preview Foundation

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-401-migration-preview-foundation`
**Pull request:** [odyssey-services/Odyssey_VTT#130](https://github.com/odyssey-services/Odyssey_VTT/pull/130) (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Give `ItemDefinition` migration (`ADR-027` §10) its first real piece: a `ItemDefinitionMigrationPreview` type and a pure builder that lists which `ItemInstance`/`ItemStack` records a candidate source→target definition migration would affect, and what their snapshot would become — steps 1/2/4 of §10's nine-step workflow. No blocking-incompatibility computation, no backup, no apply.

## 2. Task contract

- Goal: add `ItemDefinitionMigrationPreview` (+ supporting types) and `ItemDefinitionMigrationRules.BuildPreview`/`ComputePreviewRevision`, plus two new `IInventoryRepository` read primitives (`ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`) that find affected records campaign-wide, independent of which specific published version created them.
- Acceptance criteria: source/target resolved only via caller-supplied `ContentDefinitionRef`s (no auto-"next version" logic); `ItemStack` grouping for the preview uses `SourceItemDefinitionRef`+`MechanicsSnapshot`+`StackState` only, not `MergeItemStacks`'s wider ownership/location fields; `AffectedInventoryRevision` is a list of `(InventoryId, Revision)` pairs, not one number; "after" snapshot reuses the exact same `ItemMechanicsSnapshot` construction `InventoryCreationService.CopySnapshot` already uses against the target definition, no new `TypedDefinitionCodec` method; `PreviewRevision` is a deterministic digest over every field; zero blocking-incompatibility computation, zero `ADR-012` backup call, zero apply logic; required validation commands pass.
- Requirement IDs: `ODY-S05-401`, `SLICE-05`.
- In scope: `IInventoryRepository` port + SQLite implementation (2 new read methods), one new Application-layer file for the preview type/builder, tests, metadata, task/plan docs, backlog row.
- Out of scope: blocking-incompatibility rules (`ODY-S05-402`), confirm/apply/backup (`ODY-S05-403`), integration fixtures (`ODY-S05-404`), any change to Equipment code or `RulesetMigrationRules.cs`.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §13/§13.1; `ADR-027` §6.2, §10, §14; `RulesetMigrationRules.cs`/`ApplyCharacterRulesetMigration` (form precedent only); `HasAnyRuntimeReferenceToDefinition`, `MergeItemStacks`, `InventoryCreationService.CopySnapshot` (all in `SqliteInventoryRepository.cs`/`InventoryCreationService.cs`).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `98ced31` (merge of PR #129, `ODY-S05-109`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §13 row 1 (`ODY-S05-401`) is `Proposed`.
- No version-lineage link exists anywhere in the catalog domain (`PreviousVersionId`/`RootDefinitionId` do not exist); `CreateNextDraftVersionFromPublished` mints a structurally new `ContentDefinitionId` with no FK back to the definition it was branched from — confirmed by direct code read.
- `HasAnyRuntimeReferenceToDefinition` is the exact prefix-match precedent (`EscapeLike(definitionId.ToString()) + "/%"` against `SourceItemDefinitionRef LIKE ... ESCAPE '\\'`), but returns only `bool`, not the matching rows, and has no `List*` sibling.
- `MergeItemStacks`'s own "mechanically identical" check (`SqliteInventoryRepository.cs` ~line 738) compares seven fields, three of which (`InventoryId` both sides, `OwnerRef`, `LocationRef`) are merge-specific spatial/ownership constraints, not part of `ADR-027` §6.2's own prose definition of "mechanically identical" (same definition id+version+snapshot version, same stackability/quantity rules, no diverging per-unit state).
- `ADR-027` §14 assigns "preview/confirm orchestration" to `Odyssey.Application` explicitly — not `Odyssey.Rules`. `RulesetMigrationRules` (the cited form precedent) lives in `Odyssey.Rules.Character` only because `ADR-025`'s own module assignment differs from `ADR-027`'s; `RulesetMigrationRules.BuildPlan` also only ever consumes pure-`Odyssey.Domain` types (`AttributeValue`, `CharacterSkill`, etc.), never `Odyssey.Application` records — but this task's builder must consume `ItemInstanceRecord`/`ItemStackRecord`/`ContentDefinitionRecord`, which are themselves `Odyssey.Application` types (confirmed by direct code read: `Odyssey.Rules` must not depend on `Odyssey.Application`, per `ADR-001`'s dependency direction). The preview type/builder therefore belongs in `Odyssey.Application.Inventory`, cited-by-analogy to `RulesetMigrationRules`'s *form* only, not its literal module placement.
- `InventoryCreationService.CopySnapshot` (private) builds a runtime snapshot as `new ItemMechanicsSnapshot(sourceRef, definition.Version, definition.DefinitionType, definition.PropertiesJson)` — the exact same construction this task's "after" snapshot needs against the target definition; no new `TypedDefinitionCodec` method is needed.
- Placing the new type in `Odyssey.Application.Inventory` means `InventoryCreationServiceTests.cs`'s existing namespace-scoped type-name guard (`InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities`) will need a narrow allow-list addition; `SqliteInventoryRepositoryTests.cs`'s type-name guard scans only the `Odyssey.Persistence` assembly and will not see these new `Odyssey.Application` types at all — confirmed by direct code read of both guards' exact scan scope before deciding this needs no change.

Assumptions: none.

## 4. Proposed approach

- **Open Question 1 (source/target resolution)**: `ItemDefinitionMigrationRules.BuildPreview` takes already-fetched `ContentDefinitionRecord source`/`target` parameters (the caller resolves both via `IContentCatalogRepository.GetContentDefinition` beforehand) — no automatic "find the next version" logic is implemented or possible today (no lineage link exists). **Resolved, not deferred.**
- **Open Question 2 ("mechanically identical" for preview grouping)**: group affected `ItemStack` records by `SourceItemDefinitionRef` + `MechanicsSnapshot` + `StackState` only — deliberately narrower than `MergeItemStacks`'s field set, which adds `InventoryId`/`OwnerRef`/`LocationRef` for its own spatial merge constraint, not because those fields are part of "mechanically identical" itself. A preview reports "these N stacks across the campaign would end up in the same state," regardless of where they physically sit. **Resolved, not deferred.**
- **Open Question 3 (`AffectedInventoryRevision` shape)**: a list of `(InventoryId, Revision)` pairs, one per distinct `InventoryId` referenced by any affected instance/stack — a campaign-wide `ItemDefinition` migration realistically touches many Characters' inventories at once; a single number cannot express that. The builder derives the distinct `InventoryId` set from the affected records and requires the caller to supply a matching `InventoryRecord` for each (fail-fast if any is missing) — the same "caller pre-fetches, builder only computes" shape `RulesetMigrationRules.BuildPlan` already uses for its own `Expected*Revision` inputs. **Resolved, not deferred.**
- **Open Question 4 ("after" snapshot computation)**: reuse the exact `ItemMechanicsSnapshot` construction `InventoryCreationService.CopySnapshot` already uses (`new ItemMechanicsSnapshot(targetRef, target.Version, target.DefinitionType, target.PropertiesJson)`) — no new `TypedDefinitionCodec` method. This is a full, real snapshot (not a textual field-diff placeholder), since the confirm/apply step (`ODY-S05-403`) will need to write exactly this value; computing anything less here would just be redone in full there. **Resolved, not deferred.**
- New `IInventoryRepository` methods `ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`, implemented in `SqliteInventoryRepository.cs` by mirroring `HasAnyRuntimeReferenceToDefinition`'s exact prefix-match `LIKE`/`EscapeLike` pattern, but `SELECT`-ing and mapping full rows (reusing the existing `ItemInstanceSelectColumns`/`ReadItemInstance` etc.) instead of `EXISTS`.
- New file `Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs` (namespace `Odyssey.Application.Inventory`, next to `InventoryCreationService.cs`/`EquipmentService.cs`): `ItemDefinitionMigrationPreview`, `ItemDefinitionMigrationAffectedInstance`, `ItemDefinitionMigrationAffectedStackGroup` (+ a small stack-membership value type), `ItemDefinitionMigrationInventoryRevision`, and the static `ItemDefinitionMigrationRules` class (`BuildPreview`/`ComputePreviewRevision`).
- Update `ThrowingInventoryRepository` (`InventoryStackOperationServiceTests.cs`) with the two new interface members (expected, per this session's own established pattern).
- Narrow `InventoryCreationServiceTests.cs`'s type-name guard with an allow-list addition for the new type names; leave `SqliteInventoryRepositoryTests.cs`'s guards untouched (verified by full-suite test run, not merely assumed) since no new table and no new `Odyssey.Persistence`-assembly type is introduced.
- New test IDs `TC-INVENTORY-157`+ covering: preview build for a single affected instance; stack grouping (including a non-grouping negative case where `MechanicsSnapshot` diverges); the two new list-by-`DefinitionId` primitives matching across versions and across multiple inventories; `PreviewRevision` determinism (same input → same value) and sensitivity (any field change → different value).

No blocking-incompatibility computation, no `ADR-012` backup call, no apply logic, no change to `EquipItemCore<T>`/`UnequipItemCore<T>`/`HasAnyEquippedEntryReferencingBodyPart`/`RulesetMigrationRules.cs`.

## 5. Milestones

### M1 — Repository primitives

- [x] Add `IInventoryRepository.ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`.
- [x] Implement both in `SqliteInventoryRepository.cs`.
- [x] Update `ThrowingInventoryRepository` fake.
- [x] Build the solution.

### M2 — Preview type and builder

- [x] Add `ItemDefinitionMigrationRules.cs` (preview type, affected-record types, builder, `ComputePreviewRevision`).
- [x] Build the solution.

### M3 — Tests, metadata, docs, PR

- [x] Add tests for `TC-INVENTORY-157`-`168`.
- [x] Narrow `InventoryCreationServiceTests.cs`'s type-name guard.
- [x] Narrow `InventoryRuntimeRecordTests.cs`'s filename-fragment guard (discovered mid-implementation; see §8).
- [x] Register test metadata.
- [x] Run `dotnet test`.
- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-12 — Preflight: fetched `origin`, verified PR #129 merged and `origin/main` at `98ced31`, verified `ODY-S05-401` backlog row is `Proposed`, created `feat/ody-s05-401-migration-preview-foundation` from `origin/main`.
- 2026-09-12 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §13/§13.1, `RulesetMigrationRules.cs`/`ApplyCharacterRulesetMigration` in full, `HasAnyRuntimeReferenceToDefinition`, `MergeItemStacks`, `InventoryCreationService.CopySnapshot`, `ContentDefinitionRecord`/`ItemInstanceRecord`/`ItemStackRecord`/`ContentDefinitionRef` shapes, both existing scope-guard tests' exact scan scope.
- 2026-09-12 — Resolved all 4 required Open Questions (§4 above) before writing any code. Corrected an initial instinct to place the new type in `Odyssey.Rules` (matching `RulesetMigrationRules`'s literal location) after re-reading `ADR-027` §14, which explicitly assigns "preview/confirm orchestration" to `Odyssey.Application`, and confirming `Odyssey.Rules` cannot depend on the `Odyssey.Application` record types this builder must consume.
- 2026-09-12 — Implemented M1 (both new `IInventoryRepository` methods + SQLite implementation + `ThrowingInventoryRepository` fake update); `dotnet build` green.
- 2026-09-12 — Implemented M2 (`ItemDefinitionMigrationRules.cs`); `dotnet build` green.
- 2026-09-12 — Implemented M3: added `TC-INVENTORY-157`-`168` (a new pure-unit test file for the builder, plus 3 new SQLite-backed tests for the list primitives in `SqliteInventoryRepositoryTests.cs`). A full `dotnet test` run surfaced two scope-guard failures beyond the two guards this task's own ТЗ named: `InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` (as expected, needing the planned allow-list addition) and, unexpectedly, `Odyssey.Tests.Unit`'s `InventoryRuntimeRecordTests.InventoryRuntimeScope_AllowsOnlyMoveBehaviorBeforeLaterInventoryTasks`, a filename-fragment guard over `Packages/**/Runtime/Inventory/*.cs` this task's own investigation had not read. Both narrowed via the same point-allow-exception principle as the ТЗ's own two named guards (see §8). `SqliteInventoryRepositoryTests.cs`'s own type-name guard needed no change, confirmed by the same full-suite run. Full `dotnet test` then green (all 501 Persistence + 136 Unit + 2 Architecture tests passing). Ran all 5 required validation commands; all passed. Reviewed `git status`/`git diff --stat` for scope.

## 7. Decisions

See task contract §18 for the full decision log (module placement correction; all 4 Open Questions; scope-guard narrowing scope; new error codes, if any).

## 8. Discoveries and deviations

- A third scope guard exists beyond the two the governing ТЗ named: `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs`'s `InventoryRuntimeScope_AllowsOnlyMoveBehaviorBeforeLaterInventoryTasks` scans `Packages/**/Runtime/Inventory/*.cs` **file names** (not type names) for forbidden fragments, including `"Migration"`, and asserted zero matches. Adding `ItemDefinitionMigrationRules.cs` under `Packages/com.odyssey.application/Runtime/Inventory/` triggered it. Narrowed the same way the ТЗ mandated for its own two named guards: changed the blanket "no Migration file" assertion to an exact `Is.EquivalentTo(new[] { "ItemDefinitionMigrationRules.cs" })` allow-list, mirroring the pattern the same test already uses for `Move`/`Stack`/`Equipment` file names on the very same lines — not a deletion or weakening of the guard's own intent, since any *other* future Migration-named file would still fail it. This required touching `DotNet/Tests/Odyssey.Tests.Unit/**`, one path outside this task's originally-scoped `DotNet/Tests/Odyssey.Tests.Persistence/**` allowed path — an unavoidable, minimal (1-line) scope-guard narrowing of exactly the kind this ТЗ's own §7 anticipated, just in a file the initial investigation had not located.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Odyssey.Tests.Contracts 1/1, Odyssey.Tests.Domain 80/80, Odyssey.Tests.Networking 67/67, Odyssey.Tests.Unit 136/136, Odyssey.Tests.Architecture 2/2, Odyssey.Tests.Persistence 501/501.
- `.\scripts\verify-format.ps1`: PASS (`FORMAT-001 PASS repository text formatting checks passed`).
- `.\scripts\check-repository-policy.ps1`: PASS (`Repository policy check passed.`).
- `.\scripts\verify-test-structure.ps1`: PASS, exit code 0.
- Diff review: `git status --short`/`git diff --stat` confirm only files under the allowed paths (plus the one narrowly-justified `Odyssey.Tests.Unit` guard edit, §8) changed; no ADR, Equipment code, or `RulesetMigrationRules.cs` touched.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. No schema, migration, persistence write path, or command semantics are introduced — only read primitives and a pure in-memory builder.

## 11. Open questions and blockers

All four Open Questions the governing ТЗ required to be resolved before coding (source/target resolution; "mechanically identical" field set; `AffectedInventoryRevision` shape; "after"-snapshot computation) are resolved in §4 above and restated with full reasoning in the task contract §18. None remain open for this task. Recorded for `ODY-S05-403` (not resolved here): whether the confirm/apply command should re-validate `PreviewRevision` against a freshly-rebuilt preview or trust the stored value directly — a question for that task's own ExecPlan.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-402` — Migration Blocking Incompatibility Rules.
