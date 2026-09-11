# ODY-S05-301 — Equipment Runtime Foundation

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-301-equipment-runtime-foundation`
**Pull request:** [#123](https://github.com/odyssey-services/Odyssey_VTT/pull/123)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Introduce the smallest Domain vocabulary the Equipment runtime block needs: `ADR-027` section 7's `EquippedEntry` shape, with rule 1 ("one item or stack is in exactly one place") expressed as a checkable type-level invariant. This creates a type, not a system — the same character as `ODY-S05-201` for Inventory.

## 2. Task contract

- Goal: add the `EquippedEntry` Domain type, tests, metadata, task contract, ExecPlan, and backlog status.
- Acceptance criteria: `EquippedEntry` carries exactly `ADR-027` §7's field list; `BodyPartRefs` reuses `BodyPartId` directly; rule 1 is a checkable invariant (`ToLocationRef()`); every invalid field is rejected; empty `BodyPartRefs` is accepted; no persistence/command/`RemoveBodyPart`/rule-4 behavior is introduced; metadata and docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-301`, `SLICE-05`.
- In scope: `Packages/com.odyssey.domain/Runtime/Inventory/**`, `DotNet/Tests/Odyssey.Tests.Domain/Inventory/**`, `DotNet/Tests/Odyssey.Tests.Unit/Inventory/**` (one existing guard update), test metadata, task/plan docs, backlog row.
- Out of scope: persistence, repository interface/implementation, Equip/Unequip commands, rule-4 executable check, `RemoveBodyPart` real check, `ArmorDefinition`/`WeaponDefinition`/`BodyPart`/`CharacterAnatomy` changes, Unity/UI, ADR edits.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1; `ADR-027` §5/§7/§9.1/§14; `ODY-S05-201`'s own task contract/ExecPlan (structural template); `InventoryRuntime.cs`, `Anatomy.cs`, `DomainIdentity.cs`, `TypedDefinitions.cs`, `SqliteCharacterRepository.cs` (`RemoveBodyPart`).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `7c5094c` (PR #122, `ODY-S05-108`, merged); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 (`ODY-S05-301`) is `Proposed`.
- The full Inventory runtime block (`ODY-S05-201`–`207`) is merged into `main`.
- No `EquippedEntry`, Equip/Unequip, or Equipment persistence exists anywhere in the codebase (re-confirmed via `git grep -il equipment`).
- `InventoryLocationRef.Equipped` is a bare location-kind marker with no `BodyPartRefs`/`EquippedByUserId`/`EquippedAt`/`Revision`.
- `BodyPartId` (`Odyssey.Domain.Character`), `UserId` (`Odyssey.Domain.Identity`, no `NewId`), `InventoryItemRef`/`InventoryOwnerRef.IsToken` (`Odyssey.Domain.Inventory`) all already exist and are reused, not duplicated.
- `CharacterAnatomy` is the direct Domain precedent for a sealed class with an `IReadOnlyList<T>` field and a `Revision` guard.

Assumptions: none.

## 4. Proposed approach

- Add one new Domain file, `EquippedEntry.cs`, in `Odyssey.Domain.Inventory`: a sealed class carrying `InventoryId`, `InventoryItemRef ItemRef`, `string EquipmentSlotRef`, `IReadOnlyList<BodyPartId> BodyPartRefs`, `UserId EquippedByUserId`, `UtcInstant EquippedAt`, `long Revision`, with constructor validation mirroring `CharacterAnatomy`/`ItemStackRecord`'s own style (throw on invalid/blank/duplicate/non-positive fields).
- Add `EquippedEntry.ToLocationRef()` — a pure derived value (`InventoryLocationRef.Equipped(InventoryId, EquipmentSlotRef)`) that gives rule 1 a checkable, equality-based form without inventing a new guard class.
- Add `DotNet/Tests/Odyssey.Tests.Domain/Inventory/EquippedEntryTests.cs`, mirroring `InventoryRuntimeFoundationTests.cs`'s style: construction/property test, empty-`BodyPartRefs` test, invalid-field rejection test, duplicate-`BodyPartRefs` rejection test, `ToLocationRef()` rule-1-consistency test, instance-vs-stack `ItemRef` test.
- Update `InventoryRuntimeRecordTests.cs`'s existing scope-guard test to admit exactly the one new file (`EquippedEntry.cs`), matching the same rolling-scope-guard pattern already used for `ODY-S05-204`/`205`'s own files. No other assertion in that test changes.
- Register `TC-INVENTORY-095`–`100` (continuing the existing series — no new `TC-EQUIPMENT-*` prefix, since Equipment is Inventory-owned state per `ADR-027` §5, not a new aggregate).
- Update the backlog row only after PR opening so `ODY-S05-301` is `In Review` with the link.

No persistence, repository interface, command service, `RemoveBodyPart` real check, rule-4 executable check, or `ArmorDefinition`/`WeaponDefinition`/`BodyPart`/`CharacterAnatomy` change is added.

## 5. Milestones

### M1 — Contract

- [x] Add `EquippedEntry` to `Odyssey.Domain.Inventory`.
- [x] Build the solution.

### M2 — Tests and metadata

- [x] Add `EquippedEntryTests.cs` for `TC-INVENTORY-095`–`100`.
- [x] Update the existing `InventoryRuntimeRecordTests.cs` scope guard to admit the new file.
- [x] Register test metadata.
- [x] Run `dotnet test`.

### M3 — Docs, validation, PR

- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #122 merged and `origin/main` at `7c5094c`, verified `ODY-S05-301` backlog row is `Proposed`, created `feat/ody-s05-301-equipment-runtime-foundation` from `origin/main`.
- 2026-09-11 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1, `ADR-027` §5/§7/§9.1/§14 (re-verified verbatim against the file, not paraphrased), `ODY-S05-201`'s task contract/ExecPlan, `InventoryRuntime.cs`, `Anatomy.cs`, `DomainIdentity.cs`, `TypedDefinitions.cs`, `SqliteCharacterRepository.cs` (`RemoveBodyPart`), and the existing Inventory Domain/Unit test files for style.
- 2026-09-11 — Chose the standalone-Domain-type design (not a typed extension of `InventoryLocationRef.Equipped`, not an Application-layer companion record) — see task contract §18 for the full reasoning.
- 2026-09-11 — Added `EquippedEntry.cs`; build passed on first attempt (0 warnings, 0 errors).
- 2026-09-11 — Added `EquippedEntryTests.cs` (6 tests); updated `InventoryRuntimeRecordTests.cs`'s scope guard; all Inventory-scoped tests passed on first run after fixing two issues found before commit: `Assert.Throws(new TestDelegate(...))` is obsolete in this NUnit version (switched to `new Action(...)`, the codebase's own existing convention) and `UserId` has no `NewId` method (switched to `UserId.Parse("user_" + Guid.NewGuid().ToString("N"))`, the existing Persistence-test convention).
- 2026-09-11 — Registered `TC-INVENTORY-095`–`100`.
- 2026-09-11 — Ran all three required repository validation scripts (`verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`); all passed. Committed, pushed `feat/ody-s05-301-equipment-runtime-foundation`, opened Draft PR [#123](https://github.com/odyssey-services/Odyssey_VTT/pull/123). All four CI checks (`dotnet-restore-build-test`, `repository-policy-format-structure`, `unity-project-package-static`, `buildidentity-provenance`) passed.
- 2026-09-11 — Doc-sync follow-up: updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 to `In Review (PR #123)`, filled in task contract §16/§17 with real evidence, updated this ExecPlan's M3/§9. Re-ran the three validation scripts against the doc-only diff; all passed.

## 7. Decisions

See task contract §18 for the full decision log (standalone Domain type; whole shape in Domain, no Application companion; `ToLocationRef()` over a guard class; duplicate-`BodyPartRefs` rejection; continuing `TC-INVENTORY-*`; new file vs. appending to `InventoryRuntime.cs`; scope-guard update).

## 8. Discoveries and deviations

- `UserId` has no `NewId(UtcInstant)` method, unlike `InventoryId`/`ItemInstanceId`/`ItemStackId`/`CharacterId` — confirmed directly in `DomainIdentity.cs` before writing tests, not assumed.
- `Assert.Throws<T>(new TestDelegate(...))` triggers an obsolete-API build error in this repository's NUnit version; `new Action(...)` is the correct, already-established convention (seen directly in `InventoryRuntimeFoundationTests.cs`).
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed, 719 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 433 — Domain carries the 6 new `TC-INVENTORY-095`–`100`).
- `.\scripts\verify-format.ps1`: PASS (`FORMAT-001 PASS repository text formatting checks passed`).
- `.\scripts\check-repository-policy.ps1`: PASS (`Repository policy check passed.`).
- `.\scripts\verify-test-structure.ps1`: PASS (exit code 0, all `TC-ARCH-*` sub-checks PASS).
- CI on PR #123: all four checks (`dotnet-restore-build-test`, `repository-policy-format-structure`, `unity-project-package-static`, `buildidentity-provenance`) pass.
- Diff review: only `Packages/com.odyssey.domain/Runtime/Inventory/EquippedEntry.cs`, the two test files, test metadata, and planning docs changed. No persistence/schema/Unity/ADR/Character/Content file changed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR. No schema, migration, runtime persistence, build artifact, dependency, or data state is changed.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-302` — Equipment Persistence Foundation.
