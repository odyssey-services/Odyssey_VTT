# ODY-S05-305 — RemoveBodyPart Dependency Closure

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-305-removebodypart-dependency-closure`
**Pull request:** [#127](https://github.com/odyssey-services/Odyssey_VTT/pull/127)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Close `ADR-027` §9.1's last open SLICE-04 stub: `RemoveBodyPart` now rejects removal (hard reject, no auto-Unequip) when an `EquippedEntry` still references the body part being removed, using a new, explicit, opt-in checker — the same wiring pattern `ODY-S05-206` already established for `DeleteCharacterPermanently`.

## 2. Task contract

- Goal: add `IBodyPartRemovalDependencyChecker`, its real `InventoryBodyPartRemovalDependencyChecker` implementation backed by a new `IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart` query, and wire it into `SqliteCharacterRepository.RemoveBodyPart` as an optional, explicitly-passed checker list.
- Acceptance criteria: substring match on the comma-joined `BodyPartRefs` column is correct at all three positions (first/middle/last) and does not false-positive on a prefix-sharing name; fail-closed on I/O error; no implicit composition root; existing two internal Character-only dependency checks unchanged; `DeleteCharacterPermanently`/`EquipItem`/`UnequipItem` untouched; metadata/docs updated; required validation commands pass.
- Requirement IDs: `ODY-S05-305`, `SLICE-05`.
- In scope: one new `IInventoryRepository` query method + SQLite implementation (reusing `EscapeLike`); one new checker interface + implementation; `SqliteCharacterRepository` constructor/field/call-site addition; doc-comment update; tests; metadata; backlog.
- Out of scope: any atomic auto-resolve (auto-Unequip) inside `RemoveBodyPart`; any change to `EquipItem`/`UnequipItem`/`EquipmentService`/`EquippedEntry.cs`; a composition root; `DeleteCharacterPermanently` and its existing checkers; ADR edits; Unity/UI.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 5 / §12.1; `ADR-027` §7 rule 5, §9.1; `ODY-S05-206`'s own checker pattern (`InventoryCharacterDeletionDependencyChecker`, `ICharacterDeletionDependencyChecker`, `HasAnyItemOwnedByCharacter`, `HasAnyRuntimeReferenceToDefinition`, `EscapeLike`); `SqliteCharacterRepository.RemoveBodyPart`/`MutateAnatomy`.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `ec9adc2` (merge of PR #126, `ODY-S05-304`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 5 (`ODY-S05-305`) is `Proposed`.
- `RemoveBodyPart`'s stub comment (direct code read, `SqliteCharacterRepository.cs` ~line 4102) explicitly documents the item/equipment dependency as unchecked, checking only two internal Character-only dependencies (`BodyPart.AttachedToBodyPartId`, `PermanentModification.AttachedToBodyPartId`) via the existing `PersistenceFailures.CharacterBodyPartHasDependent`.
- `ODY-S05-206` is the direct structural precedent: `ICharacterDeletionDependencyChecker`/`InventoryCharacterDeletionDependencyChecker` (opt-in, no composition root, fail-closed, `DependencyCheckCorrelation.Placeholder`), and `HasAnyItemOwnedByCharacter`/`HasAnyRuntimeReferenceToDefinition`/`EscapeLike` on `SqliteInventoryRepository`.
- `EquippedEntry` stores `BodyPartRefs` as a comma-joined string (`JoinBodyPartRefs`/`SplitBodyPartRefs`, `ODY-S05-302`), and `InventoryId`, not `CharacterId` — the same "join through `ItemInstance`/`ItemStack`'s `OwnerKind`/`OwnerTargetRef`" trick `HasAnyItemOwnedByCharacter` already uses is required here too, confirmed by direct code read that `EquipItemCore<T>`/`UnequipItemCore<T>` only ever touch `LocationKind`/`LocationTargetRef`/`LocationDetailRef`, never `OwnerKind`/`OwnerTargetRef`.
- `BodyPartId`'s canonical pattern allows `_`, a SQL LIKE wildcard — `EscapeLike` (already used by `HasAnyRuntimeReferenceToDefinition`) must be reused, not reimplemented.
- `DeleteCharacterPermanently`'s own §9.2 Inventory dependency is already fully closed by `ODY-S05-206` (`HasAnyItemOwnedByCharacter` has no `LocationKind` filter, so it already covers equipped items) — reconfirmed directly, no work needed there.

Assumptions: none.

## 4. Proposed approach

- **Hard rejection only (§2 of the ТЗ's own decision).** No auto-Unequip is implemented; `ODY-S05-206`'s own precedent for the same stub category (Character-deletion × Inventory) is a pure reject, and nothing in `ADR-027` §7 rule 5 or the backlog obligates auto-resolve for this MVP.
- **New query primitive** `IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart(CampaignHandle, CampaignId, CharacterId, BodyPartId, CorrelationId)`, implemented in `SqliteInventoryRepository.cs`: two `EXISTS` clauses (one joining `EquippedEntry` to `ItemInstance`, one to `ItemStack`, both via `ItemRefId`), filtered by `CampaignId` + `OwnerKind='Character'` + `OwnerTargetRef=characterId` on the item table, and `(',' || BodyPartRefs || ',') LIKE '%,' || escaped(bodyPartId) || ',%' ESCAPE '\'` on `EquippedEntry.BodyPartRefs` — wrapping both the stored value and the search pattern in commas makes first/middle/last position and the `"headBackup"` vs `"head"` boundary case both correct by construction. Fail-closed on I/O exception, matching `HasAnyItemOwnedByCharacter`.
- **New checker interface** `IBodyPartRemovalDependencyChecker` (`CharacterRepositoryContracts.cs`, next to `ICharacterDeletionDependencyChecker`) — a new, parallel interface, not an extension of the existing one, because its signature must carry `BodyPartId` (the existing interface's `CheckBlockingDependency(CampaignHandle, CharacterId)` cannot express "for this specific body part").
- **New implementation** `InventoryBodyPartRemovalDependencyChecker` (added to the existing `InventoryDeletionDependencyCheckers.cs`, next to its two siblings) — exact same fail-closed/`DependencyCheckCorrelation.Placeholder` shape as `InventoryCharacterDeletionDependencyChecker`.
- **Wiring**: a new, separate `_bodyPartRemovalDependencyCheckers` field/optional constructor parameter on `SqliteCharacterRepository`, defaulting to `Array.Empty<IBodyPartRemovalDependencyChecker>()` — not reusing or extending `_deletionDependencyCheckers` (different interface, different operation). Call site is inside `RemoveBodyPart`'s `MutateAnatomy` callback, exactly where the stub comment currently sits, iterating all checkers and returning `CharacterBodyPartHasDependent` (the existing code, not a new one) on the first hit.
- Doc-comment above `RemoveBodyPart` updated in place (not deleted) — the historical "why this was a stub" text stays, with an added paragraph stating the stub is now closed for callers that pass a checker, mirroring how `ODY-S05-206` amended `DeleteCharacterPermanently`'s own doc-comment rather than erasing it.
- New test IDs `TC-INVENTORY-143`+ covering the 8 minimum scenarios in the ТЗ: exact/first/middle/last substring match, prefix-boundary negative case, real reject via `EquipItem`, success after `UnequipItem`, no-checker backward compatibility, cross-repository wiring, fail-closed.

No change to `EquipItem`/`UnequipItem`/`EquipmentService`/`EquippedEntry.cs`, no composition root, `DeleteCharacterPermanently` untouched.

## 5. Milestones

### M1 — Query primitive and checker

- [x] Add `IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart` + SQLite implementation.
- [x] Add `IBodyPartRemovalDependencyChecker` + `InventoryBodyPartRemovalDependencyChecker`.
- [x] Build the solution.

### M2 — Wiring into RemoveBodyPart

- [x] Add `_bodyPartRemovalDependencyCheckers` field/constructor parameter to `SqliteCharacterRepository`.
- [x] Wire the checker loop into `RemoveBodyPart`; update its doc-comment.
- [x] Build the solution.

### M3 — Tests, metadata, docs, PR

- [x] Add tests for `TC-INVENTORY-143`-`152` covering all 8 minimum scenarios.
- [x] Register test metadata.
- [x] Run `dotnet test`.
- [x] Update task contract completion evidence.
- [x] Run required repository validation scripts.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog `In Review` status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #126 merged and `origin/main` at `ec9adc2`, verified `ODY-S05-305` backlog row is `Proposed`, created `feat/ody-s05-305-removebodypart-dependency-closure` from `origin/main`.
- 2026-09-11 — Read required sources: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1, `RemoveBodyPart`/`MutateAnatomy` (full), `HasAnyItemOwnedByCharacter`/`HasAnyRuntimeReferenceToDefinition`/`EscapeLike`, `ICharacterDeletionDependencyChecker`/`InventoryCharacterDeletionDependencyChecker`/`InventoryContentDefinitionDependencyChecker`, `SqliteCharacterRepository`'s constructor and `_deletionDependencyCheckers` field.
- 2026-09-11 — Chose hard-reject-only, a new parallel checker interface, and a new comma-wrapped LIKE pattern for correct substring-boundary matching. Full reasoning recorded in task contract §18.
- 2026-09-11 — Implemented `HasAnyEquippedEntryReferencingBodyPart`, `IBodyPartRemovalDependencyChecker`/`InventoryBodyPartRemovalDependencyChecker`, and the `SqliteCharacterRepository` wiring; build passed on first attempt after fixing `ThrowingInventoryRepository`'s fake (expected).
- 2026-09-11 — Added `BodyPartRemovalDependencyCheckerTests.cs` (`TC-INVENTORY-143`-`152`, 10 tests); all passed on first run. Full suite run then passed with no scope-guard breaks (this task added no file matching any existing forbidden-fragment guard): 771 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 485).
- 2026-09-11 — Registered `TC-INVENTORY-143`-`152`; no new error code needed (reused `CharacterBodyPartHasDependent`), so `docs/errors/ERROR_CODES.md` is unchanged. Validation passed: `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1` (passed on first run), `verify-test-structure.ps1`. Diff review of `SqliteCharacterRepository.cs` confirmed `DeleteCharacterPermanently` and every other method untouched — only the constructor and `RemoveBodyPart` changed.
- 2026-09-11 — Committed, pushed `feat/ody-s05-305-removebodypart-dependency-closure`, opened Draft PR [#127](https://github.com/odyssey-services/Odyssey_VTT/pull/127). Doc-sync follow-up: updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 5 to `In Review (PR #127)` and this task contract/ExecPlan headers with the PR link.

## 7. Decisions

See task contract §18 for the full decision log (hard reject vs. auto-resolve; new interface vs. extending `ICharacterDeletionDependencyChecker`; comma-wrapped LIKE pattern; separate checker-list field; reused error code).

## 8. Discoveries and deviations

- No pre-existing scope guard broke this time (unlike `ODY-S05-301`-`303`) — this task's new names/files did not match any `Packages/**/Runtime/Inventory/**` filename guard or "Equipment"-fragment forbidden-name guard.
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 485 (771 total, 0 failed).
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed.` on the first run.
- `.\scripts\verify-test-structure.ps1`: passed with exit code 0 and `TC-ARCH-001 PASS valid ADR-001 graph passes`.
- Diff review: `InventoryRepositoryContracts.cs`, `CharacterRepositoryContracts.cs`, `InventoryDeletionDependencyCheckers.cs`, `SqliteInventoryRepository.cs`, `SqliteCharacterRepository.cs` (constructor + `RemoveBodyPart` only, confirmed line-by-line), one new test file, `InventoryStackOperationServiceTests.cs`, test metadata, and planning docs changed. No Unity/ADR/`EquipItem`/`UnequipItem`/`EquippedEntry.cs`/`DeleteCharacterPermanently` file changed; no error registry change needed.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. `RemoveBodyPart`'s own transaction is unchanged in shape; the new checker call is a pure read inside the existing callback, adding one more possible rejection path before any write.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-306` — Equipment Runtime Integration Fixtures.
