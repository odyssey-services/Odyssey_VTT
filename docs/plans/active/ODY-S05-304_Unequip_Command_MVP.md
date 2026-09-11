# ODY-S05-304 — Unequip Command MVP

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-304-unequip-command-mvp`
**Pull request:** [#126](https://github.com/odyssey-services/Odyssey_VTT/pull/126)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Give MainGM the reverse of `ODY-S05-303`'s Equip transition: atomically move an equipped `ItemInstance`/`ItemStack` back into a `Contained` location in the same Inventory and remove its `EquippedEntry` row, with a symmetric CAS/idempotency guard. No new Equip semantics; no rule-4/`RemoveBodyPart` behavior.

## 2. Task contract

- Goal: `IInventoryRepository.UnequipItem` (mirroring `EquipItem`/`EquipItemCore<T>`) + `EquipmentService.Unequip`.
- Acceptance criteria: `EquippedEntry` is authoritative for "is this item equipped"; the item's own `LocationRef` is cross-checked defensively, not blindly trusted; destination is `Contained` in the same Inventory only (MVP scope, non-Contained explicitly deferred); CAS on both the `EquippedEntry` revision and the item's own revision; `CommandId` idempotency; MainGM-only; no `ICharacterRepository` dependency; `EquipItem`/`EquipItemCore<T>` unchanged; metadata/docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-304`, `SLICE-05`.
- In scope: one new repository primitive (`UnequipItem`/`UnequipItemCore<T>`) reusing `EquipmentCommandLedger` with a new `"Unequip"` operation kind; `EquipmentService.Unequip` + `UnequipRequest`/`UnequipTransition` (same `EquipmentService.cs` file); persistence tests; metadata; docs.
- Out of scope: rule 4/`ICharacterRepository` dependency, `RemoveBodyPart` (`ODY-S05-305`), non-`Contained` destinations (`SceneDropped`/`Other` — explicitly deferred, see §18), weapon/armor mechanics, any change to `EquipItem`/`EquipItemCore<T>`, ADR edits, Unity/UI.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 4 / §12.1; `ODY-S05-303`'s own task contract/ExecPlan/code (structural template — mirror, don't reinvent); `ADR-027` §7 rule 3; `EquipItem`/`EquipItemCore<T>`, `ReplaceEquippedEntry`/`DeleteEquippedEntry`, `MoveItem<T>` (all in `SqliteInventoryRepository.cs`).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `856642c` (merge of PR #125, `ODY-S05-303`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 4 (`ODY-S05-304`) is `Proposed`.
- `EquipItem`/`EquipItemCore<T>` (direct code read) is the exact structural mirror to build from: ledger replay check, source-Contained + CAS check, rule-1 check, `LocationRef` `UPDATE`, `EquippedEntry` `INSERT`, one ledger write, one transaction.
- `DeleteEquippedEntry` deletes only the `EquippedEntry` row — never touches `ItemInstance`/`ItemStack` — the same gap `CreateEquippedEntry` had before `EquipItem` closed it for the Equip direction.
- `MoveItem<T>` cannot be reused as-is: it hardcodes `Contained` on both ends and manages two `Inventory` revisions (source/destination) that Unequip does not need (Unequip never changes `InventoryId`).
- `EquipmentCommandLedger` (`CommandId` PK, `OperationKind`, `ItemRefId`, `ExpectedRevision`, `CreatedAt`, `AppliedAt`) already carries a different `ExpectedRevision` meaning per operation kind (`0` for `Create`; the `EquippedEntry`'s own revision for `Replace`/`Delete`/`Equip`... actually `Equip`'s `ExpectedRevision` is the *item's* revision, not the `EquippedEntry`'s) — this task's `"Unequip"` kind will use the `EquippedEntry`'s own revision, matching `Replace`/`Delete`'s convention rather than `Equip`'s, since `EquippedEntry` is this task's chosen authority (see §18).
- No existing transactional method writes `SceneDropped`/`Other` as a destination; `ODY-S05-204`'s own task contract §7 explicitly rejects them as a destination for Move.

Assumptions: none.

## 4. Proposed approach

- **§2 (authority) — `EquippedEntry` is authoritative.** The transaction reads `EquippedEntry` first (the record specifically created to mean "this item is equipped"), CAS-checks its revision, then reads the item and defensively cross-checks that its `LocationRef` is `Equipped` and equals `EquippedEntry.ToLocationRef()`. A mismatch is treated as an inconsistency (`InventoryIoFailed`), not silently repaired — this should never happen if `EquipItem` is the only writer, and defensive code must say so loudly, not paper over it.
- **§3 (destination scope) — `Contained` only, MVP.** Unequip always returns the item to a `Contained` location in the *same* `InventoryId` it was equipped from (no destination `InventoryId` is accepted — nothing in the backlog's rule 3 or `ODY-S05-204`'s vocabulary establishes a precedent for moving to a *different* Inventory while unequipping, and doing so would reintroduce `MoveItem<T>`'s two-Inventory-revision bookkeeping this task deliberately avoids). Only the destination *container key* is caller-supplied. Non-`Contained` destinations (`SceneDropped`/`Other` — "drop to the floor on unequip") are explicitly out of scope and not implemented; see §18 for the deferred-follow-up note.
- **`UnequipTransition`** carries `ItemRef`, `InventoryId` (validated against the `EquippedEntry`'s own `InventoryId` inside the transaction — a mismatch reuses `InventoryMovementFailures.SourceInvalid` with the broader "the target is not where the caller expects" reading, avoiding a duplicate error code), `ExpectedTargetRevision` (item), `ExpectedEquippedEntryRevision`, `DestinationContainerKey`, `CommandId` — symmetric with `EquipTransition`'s shape, plus the second CAS value `ODY-S05-302`'s `ReplaceEquippedEntry`/`DeleteEquippedEntry` already established as the `EquippedEntry`'s own identity.
- **New repository primitive `IInventoryRepository.UnequipItem`**, implemented as `UnequipItemCore<T>` (generic over `ItemInstanceRecord`/`ItemStackRecord`, mirroring `EquipItemCore<T>`'s delegate-based shape): ledger replay (new `"Unequip"` operation kind, `ExpectedRevision` = the `EquippedEntry`'s own revision); read+CAS `EquippedEntry`; read+cross-check+CAS the item; `UPDATE` the item to `Contained`; `DELETE FROM EquippedEntry` (inlined in the same transaction, not a call to the public `DeleteEquippedEntry` method — the same reason `EquipItemCore<T>` inlined its `INSERT` instead of calling `CreateEquippedEntry`); one ledger write; one transaction.
- **`EquipmentService.Unequip`** (added to the same `EquipmentService.cs`, next to `Equip`): MainGM gate first (reusing `InventoryMovementFailures.Denied`), then delegates straight to `inventoryRepository.UnequipItem(...)`. No `ICharacterRepository` parameter — rule 4 is an Equip-only concern (a body part's *current* existence only matters when something is being newly attached to it; removing an item does not need to re-validate anatomy).
- No new error code beyond what already exists is required for the repository primitive's normal paths (`EquipmentEntryNotFound`, `EquipmentEntryRevisionConflict`, `ItemInstanceNotFound`/`ItemStackNotFound`, `ItemRevisionConflict`, `InventoryCommandIdentityMismatch`, `SourceInvalid`, `InventoryIoFailed` — all reused); `Denied` is reused for the MainGM gate.
- New test IDs `TC-INVENTORY-131`+ mirroring `EquipmentServiceTests.cs`'s structure: success (instance + stack), not-equipped, `EquippedEntry`-revision conflict, item-revision conflict, `CommandId` collision, replay, MainGM denial, `InventoryId` mismatch.

No rule 4, `RemoveBodyPart`, weapon/armor mechanics, or non-`Contained` destination is added. `EquipItem`/`EquipItemCore<T>` is not touched.

## 5. Milestones

### M1 — Repository primitive

- [x] Add `UnequipTransition` and `IInventoryRepository.UnequipItem`.
- [x] Implement `UnequipItemCore<T>` in `SqliteInventoryRepository.cs`.
- [x] Build the solution.

### M2 — Application service

- [x] Add `EquipmentService.Unequip`, `UnequipRequest`.
- [x] Build the solution.

### M3 — Tests, metadata, docs, PR

- [x] Add tests for `TC-INVENTORY-131`-`142`.
- [x] Register test metadata.
- [x] Run `dotnet test`.
- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #125 merged and `origin/main` at `856642c`, verified `ODY-S05-304` backlog row is `Proposed`, created `feat/ody-s05-304-unequip-command-mvp` from `origin/main`.
- 2026-09-11 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1, `EquipItem`/`EquipItemCore<T>` (full), `ReplaceEquippedEntry`/`DeleteEquippedEntry`, `MoveItem<T>`, `EquipmentService.cs` (full, current `Equip`).
- 2026-09-11 — Chose `EquippedEntry`-authoritative reads, `Contained`-only destination scope for MVP, and no `ICharacterRepository` dependency for Unequip. Full reasoning recorded in task contract §18.
- 2026-09-11 — Implemented `IInventoryRepository.UnequipItem` (generic `UnequipItemCore<T>` in `SqliteInventoryRepository.cs`, reusing `EquipmentCommandLedger` with a new `"Unequip"` operation kind anchored to the `EquippedEntry`'s own revision) and `EquipmentService.Unequip`/`UnequipRequest`/`UnequipTransition`; build passed on first attempt after fixing `ThrowingInventoryRepository`'s fake (expected).
- 2026-09-11 — Added `TC-INVENTORY-131`-`142` to `EquipmentServiceTests.cs`. One test (`UnequipItem_ReplayWithSameCommandId...`) initially failed on a wrong revision-math assertion (expected `instance.Revision + 1`, should be `+ 2` since Equip and Unequip each advance the item's revision once) — a test-authoring bug, not a production defect; fixed and re-ran green. Full suite then passed with no scope-guard breaks this time (no new file/type name matched any existing forbidden-fragment guard): 761 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 475).
- 2026-09-11 — Registered `TC-INVENTORY-131`-`142`; no new error code needed (every failure path reuses an existing registered code), so `docs/errors/ERROR_CODES.md` is unchanged. Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1` (passed on first run), `verify-test-structure.ps1`. Diff review confirmed only allowed paths touched.
- 2026-09-11 — Committed, pushed `feat/ody-s05-304-unequip-command-mvp`, opened Draft PR [#126](https://github.com/odyssey-services/Odyssey_VTT/pull/126). Doc-sync follow-up: updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 4 to `In Review (PR #126)` and this task contract/ExecPlan headers with the PR link.

## 7. Decisions

See task contract §18 for the full decision log (authority source for "is equipped"; destination scope and the deferred non-`Contained` follow-up; ledger `ExpectedRevision` meaning for `"Unequip"`; reused error codes; no `ICharacterRepository` dependency).

## 8. Discoveries and deviations

- A test-authoring revision-math bug (not a production defect) surfaced once during `TC-INVENTORY-137`'s first run: the assertion expected the item's revision to advance by 1 across an Equip+Unequip round trip, but it correctly advances by 2 (one per transition). Fixed the assertion, re-ran green.
- Unlike `ODY-S05-303`, this task's new names (`UnequipItem`, `UnequipItemCore`, `UnequipTransition`, `UnequipRequest`, `EquipmentOperationUnequip`) matched no existing forbidden-fragment scope guard — no guard-narrowing edits were needed this time.
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 475 (761 total, 0 failed).
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed.` on the first run.
- `.\scripts\verify-test-structure.ps1`: passed with exit code 0 and `TC-ARCH-001 PASS valid ADR-001 graph passes`.
- Diff review: `EquipmentService.cs`, `InventoryRepositoryContracts.cs`, `SqliteInventoryRepository.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/EquipmentServiceTests.cs` and `InventoryStackOperationServiceTests.cs`, test metadata, and planning docs changed. No Unity/ADR/`EquipItem`/`EquippedEntry.cs` file changed; no error registry change needed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. The item's `LocationRef` update, the `EquippedEntry` delete, and the ledger write happen in one SQLite transaction; a pre-commit failure leaves none of them durable.

## 11. Open questions and blockers

- Non-`Contained` Unequip destinations (`SceneDropped`/`Other` — e.g. "unequip and drop to the floor in one step") are not implemented. If/when needed, this requires a new, explicitly-numbered follow-up task (per `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own "no unnamed TODO" rule) — not a silent addition to this task or a later one without its own task ID.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-305` — RemoveBodyPart Dependency Closure.
