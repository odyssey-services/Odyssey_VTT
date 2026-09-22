# ODY-S05-303 — Equip Command MVP

**Status:** Done (PR #125, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-303-equip-command-mvp`
**Pull request:** [#125](https://github.com/odyssey-services/Odyssey_VTT/pull/125)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Give MainGM a real Equip transition: move an already-created, contained `ItemInstance`/`ItemStack` into an equipped-location state, atomically creating its `EquippedEntry` row and updating the item's own `LocationRef` to match, enforcing rule 1 (exclusive one-place) and rule 4 (referenced body parts currently exist on the owning Character).

## 2. Task contract

- Goal: a MainGM-only, CAS-guarded, idempotent Equip command that leaves the item's own `LocationRef` and its `EquippedEntry.ToLocationRef()` consistently `Equipped`.
- Acceptance criteria: rule 4 enforced (including `Anatomy == null` and non-Character owners); rule 1 enforced (physically, via `ODY-S05-302`'s `ItemRefId` primary key, and functionally via a pre-insert existence check); source must be `Contained` in the stated Inventory at the expected revision; MainGM-only; idempotent by `CommandId`; no partial application; no Unequip, weapon/armor mechanics, or `RemoveBodyPart` behavior; `EquippedEntry.cs` unchanged; metadata/docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-303`, `SLICE-05`.
- In scope: one new repository primitive (`IInventoryRepository.EquipItem`) implemented in `SqliteInventoryRepository.cs`, reusing the existing `EquipmentCommandLedger` table with a new `"Equip"` operation kind; a new `Odyssey.Application.Inventory.EquipmentService` (+ request/failure types); persistence tests; metadata; docs.
- Out of scope: Unequip, weapon/armor mechanical effects, `RemoveBodyPart` dependency closure, `EquippedEntry.cs` changes, ADR edits, Unity/UI, updating `RemoveBodyPart`'s stale doc-comment (reserved for `ODY-S05-305`).
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 3 / §12.1; `ODY-S05-204`'s own task contract/ExecPlan (structural template); `ADR-027` §5/§7/§14; `InventoryMovementService.cs`, `InventoryStackOperationService.cs`, `SqliteInventoryRepository.cs` (`MoveItem<T>`, Equipment persistence from `ODY-S05-302`), `CharacterRepositoryContracts.cs`, `Anatomy.cs`.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `6ee7267` (merge of PR #124, `ODY-S05-302`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 3 (`ODY-S05-303`) is `Proposed`.
- `IInventoryRepository.CreateEquippedEntry` only inserts into the `EquippedEntry` table; it never touches `ItemInstance`/`ItemStack` rows. Nothing in the current codebase updates an item's own `LocationRef` to `Equipped` — confirmed by direct code read (no `UPDATE ItemInstance`/`UPDATE ItemStack` call site sets `LocationKind='Equipped'` anywhere).
- `MoveItem<T>` (used by `MoveItemInstance`/`MoveItemStack`) requires `InventoryLocationKind.Contained` on both source and destination and always writes `LocationKind=Contained` on the destination — `ODY-S05-204`'s own task contract §7 explicitly named `Equipped` as a rejected destination, deferring it to this task.
- `EquippedEntry.ToLocationRef()`'s own doc-comment (`ODY-S05-301`) anticipates a future reader comparing it against the item's own stored `LocationRef` for equality — this task is what makes that comparison meaningful by keeping both in sync.
- `ICharacterRepository.GetCharacter` returns `CharacterRecord` with `Anatomy` typed `CharacterAnatomy?` (nullable) and, when non-null, `Anatomy.BodyParts` (`IReadOnlyList<BodyPart>`), each carrying its own `BodyPartId`.
- No existing Application service in `Odyssey.Application.Inventory` reads `ICharacterRepository`; both interfaces live in the same `Odyssey.Application` assembly with no namespace-level dependency barrier in `ADR-001`/`ADR-027` §14.

Assumptions: none.

## 4. Proposed approach

- **§1 (LocationRef sync) — Variant A, one new repository primitive.** Add `IInventoryRepository.EquipItem(CampaignHandle, EquipTransition, CorrelationId)`, implemented as one new private generic `EquipItem<T>` core in `SqliteInventoryRepository.cs` (mirroring `MoveItem<T>`'s shape: select/inventoryOf/locationOf/revisionOf delegates, `table`/`idColumn` strings), branching on `EquipTransition.Record.Entry.ItemRef.Kind` for `ItemInstance` vs `ItemStack`. In one `SqliteTransaction`: verify the target exists, is `Contained` in the stated `InventoryId`, and is at the expected revision (CAS); verify no `EquippedEntry` row already exists for this item (rule 1, functional check ahead of the physical PK); `UPDATE` the target's `LocationKind/LocationTargetRef/LocationDetailRef` to `Equipped`/`InventoryId`/`EquipmentSlotRef` with `Revision=Revision+1` under the same CAS predicate `MoveItem<T>` uses; `INSERT` the `EquippedEntry` row; write one `EquipmentCommandLedger` row (new `"Equip"` operation kind, reusing the existing table/column shape from `ODY-S05-302` — `ExpectedRevision` here means the item's own expected revision, the same column already carries different per-operation-kind meanings for `Create`/`Replace`/`Delete`); commit once. This is a single physically-atomic primitive, not two separate calls from Application — the ТЗ's own preference, and consistent with `MoveItem<T>`'s existing "one transaction per business transition" convention.
- **`EquipTransition`** wraps an already-built `EquippedEntryRecord` (constructed and validated by the Domain `EquippedEntry` constructor) plus `ExpectedTargetRevision` (the item's own revision) and `CommandId` — symmetric with `CreateEquippedEntry`'s own `(campaign, record, commandId, correlationId)` shape, so the repository does not re-validate fields the Domain type already validates.
- **§2 (rule 4) — direct `ICharacterRepository` call from `EquipmentService`, not a checker-port.** `EquipmentService` (new, `Odyssey.Application.Inventory`) takes both `IInventoryRepository` and `ICharacterRepository` as parameters. Rationale: both interfaces already live in one `Odyssey.Application` assembly with no enforced namespace boundary; a checker-port (`ODY-S05-206`'s inversion-of-control pattern) exists because `SqliteInventoryRepository` itself must not depend on `ICharacterRepository` at the Persistence layer — but here the cross-repository read happens in Application, one level up, where no such constraint exists. Introducing a port for a single, currently-unique consumer would be unconsumed abstraction.
- Rule 4 applies only when `BodyPartRefs` is non-empty (ADR-027 §7 already calls body-part references optional for slots that do not need them). When non-empty: the owning Inventory's `OwnerRef` must be `Character`-kind (a non-Character owner with non-empty `BodyPartRefs` is a caller-contract violation — rejected as a validation failure, not treated as "not applicable"); the Character's `Anatomy` must be initialized (`null` reuses the existing `CharacterAnatomyNotInitialized` error — the identical underlying condition `ODY-S04-109` already names); every referenced `BodyPartId` must exist in `Anatomy.BodyParts` (a new `EquipmentFailures.BodyPartNotFound` otherwise).
- MainGM gate is the very first check in `EquipmentService.Equip`, before any repository call (matching `ODY-S05-204`'s own "fails before repository mutation or replay probe" invariant) and reuses `InventoryMovementFailures.Denied` (no new error).
- `EquipRequest` mirrors `InventoryMove`/`InventoryStackOperation`'s shape: `ItemRef`, `InventoryId` (must currently be `Contained` there), `ExpectedTargetRevision`, `EquipmentSlotRef`, `BodyPartRefs`, `EquippedByUserId`, `EquippedAt`, actor identity, `CommandId`, `CorrelationId`.
- New test IDs `TC-INVENTORY-118`–`~130` (exact count decided during implementation) covering: success with `LocationRef`/`EquippedEntry` consistency, rule-1 conflict, rule-4 body-part-missing, rule-4 anatomy-not-initialized, rule-4 non-Character-owner-with-body-parts, source-not-Contained, CAS conflict, replay, MainGM denial.

No Unequip, weapon/armor mechanical effect, or `RemoveBodyPart` behavior is added. `EquippedEntry.cs` is not touched.

## 5. Milestones

### M1 — Repository primitive

- [x] Add `EquipTransition` and `IInventoryRepository.EquipItem`.
- [x] Implement `EquipItem<T>` in `SqliteInventoryRepository.cs` (CAS source check, rule-1 check, `LocationRef` update, `EquippedEntry` insert, ledger, one transaction).
- [x] Build the solution.

### M2 — Application service and rule 4

- [x] Add `EquipmentService.Equip`, `EquipRequest`, `EquipmentFailures` (rule-4 errors).
- [x] Build the solution.

### M3 — Tests, metadata, docs, PR

- [x] Add tests for `TC-INVENTORY-118`-`130`.
- [x] Register test metadata.
- [x] Run `dotnet test`.
- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #124 merged and `origin/main` at `6ee7267`, verified `ODY-S05-303` backlog row is `Proposed`, created `feat/ody-s05-303-equip-command-mvp` from `origin/main`.
- 2026-09-11 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1, `ODY-S05-204` task contract/ExecPlan in full, `InventoryMovementService.cs`, `InventoryStackOperationService.cs`, `SqliteInventoryRepository.cs` (`MoveItem<T>`, Equipment persistence), `CharacterRepositoryContracts.cs` (`ICharacterRepository.GetCharacter`, `CharacterRecord.Anatomy`), `Anatomy.cs` (`CharacterAnatomy.BodyParts`, `BodyPart.BodyPartId`).
- 2026-09-11 — Chose Variant A (one atomic repository primitive) for §1 and a direct `ICharacterRepository` call (not a checker-port) for §2. Full reasoning recorded in task contract §18.
- 2026-09-11 — Implemented `IInventoryRepository.EquipItem` (generic `EquipItemCore<T>` in `SqliteInventoryRepository.cs`, reusing `EquipmentCommandLedger` with a new `"Equip"` operation kind) and `EquipmentService.Equip`/`EquipRequest`/`EquipmentFailures`; build passed on first attempt after fixing `ThrowingInventoryRepository`'s fake (expected, per the ODY-S05-119/120 lesson).
- 2026-09-11 — Added `EquipmentServiceTests.cs` (`TC-INVENTORY-118`-`130`); all 13 passed on first run. Full suite run then surfaced two more pre-existing scope guards this task's new file/type names broke (`InventoryRuntimeRecordTests.cs`'s file-name guard and `InventoryCreationServiceTests.cs`'s type-name guard); narrowed both, then full suite passed: 749 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 463).
- 2026-09-11 — Registered `TC-INVENTORY-118`-`130` and the two new error codes in `docs/errors/ERROR_CODES.md`. Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1` (passed on first run), `verify-test-structure.ps1`. Diff review confirmed only allowed paths touched except one unavoidable one-line guard narrowing in `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` (documented in task contract §18).
- 2026-09-11 — Committed, pushed `feat/ody-s05-303-equip-command-mvp`, opened Draft PR [#125](https://github.com/odyssey-services/Odyssey_VTT/pull/125). Doc-sync follow-up: updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 3 to `In Review (PR #125)` and this task contract/ExecPlan headers with the PR link.

## 7. Decisions

See task contract §18 for the full decision log (Variant A over Variant B; `EquipTransition` wrapping a pre-built `EquippedEntryRecord`; reusing `EquipmentCommandLedger` with a new `"Equip"` operation kind rather than a new ledger table; direct `ICharacterRepository` call over a checker-port; rule-4 scope for empty `BodyPartRefs`/non-Character owner; reused vs. new error codes).

## 8. Discoveries and deviations

- Two more pre-existing scope guards broke beyond the one anticipated: `InventoryRuntimeRecordTests.InventoryRuntimeScope_AllowsOnlyMoveBehaviorBeforeLaterInventoryTasks` (its file-name allow-list didn't yet admit `EquipmentService.cs`) and `InventoryCreationServiceTests.InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities` (its type-name forbidden-fragment check matched the new `EquipmentService`/`EquipmentFailures` type names). Both narrowed with an explanatory comment; no other assertion in either file changed. The first file (`DotNet/Tests/Odyssey.Tests.Unit/Inventory/**`) is outside this task's nominal allowed paths — treated as unavoidable technical necessity and documented in task contract §18.
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 463 (749 total, 0 failed).
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed.` on the first run.
- `.\scripts\verify-test-structure.ps1`: passed with exit code 0 and `TC-ARCH-001 PASS valid ADR-001 graph passes`.
- Diff review: `Packages/com.odyssey.application/Runtime/Inventory/EquipmentService.cs` (new), `InventoryRepositoryContracts.cs`, `ErrorCodes.cs`, `SqliteInventoryRepository.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/**`, one line in `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs`, test metadata, error registry, and planning docs changed. No Unity/ADR/`EquippedEntry.cs`/`SqliteCharacterRepository.cs` file changed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. The Equip transition writes the item's `LocationRef` update, the `EquippedEntry` insert, and the ledger row inside one SQLite transaction; a pre-commit failure leaves none of them durable.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-304` — Unequip Command MVP.
