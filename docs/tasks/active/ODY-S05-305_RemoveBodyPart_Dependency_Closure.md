# ODY-S05-305 — RemoveBodyPart Dependency Closure

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Equipment runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-305-removebodypart-dependency-closure`
**Pull request:** [#127](https://github.com/odyssey-services/Odyssey_VTT/pull/127)
**ExecPlan:** `docs/plans/active/ODY-S05-305_RemoveBodyPart_Dependency_Closure.md`
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Close `ADR-027` §9.1's last open SLICE-04 stub (rule 5): `RemoveBodyPart` rejects removal when an `EquippedEntry` still references the body part being removed, via a new, explicit, opt-in `IBodyPartRemovalDependencyChecker`, hard-reject only (no atomic auto-Unequip).

## 2. Why this task exists

- Problem or dependency being addressed: `RemoveBodyPart`'s own doc-comment has honestly documented, since `ODY-S04-109`/re-documented by `ODY-S05-206`, that the item/equipment dependency check does not exist because no Equipment layer existed yet. `ODY-S05-301`-`304` built that layer; this task is what actually closes the stub.
- Value or risk reduction: without this task, a GM could remove a body part out from under an equipped item, leaving `EquippedEntry.BodyPartRefs` referencing a body part that no longer exists on the Character.
- Blocking or enabling relationship: the last piece before `ODY-S05-306`'s integration fixture can prove a full Equip→(blocked RemoveBodyPart)→Unequip→(successful RemoveBodyPart) round trip.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §12 row 5, §12.1.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §7 rule 5, §9.1.
- `docs/tasks/active/ODY-S05-301_Equipment_Runtime_Foundation.md` through `ODY-S05-304_Unequip_Command_MVP.md` (predecessors).
- Existing pattern: `ICharacterDeletionDependencyChecker`/`InventoryCharacterDeletionDependencyChecker`/`InventoryContentDefinitionDependencyChecker` (`ODY-S05-206`), `HasAnyItemOwnedByCharacter`/`HasAnyRuntimeReferenceToDefinition`/`EscapeLike` (`SqliteInventoryRepository.cs`), `SqliteCharacterRepository.RemoveBodyPart`/`MutateAnatomy`.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-305`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`142` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-143`-`152`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-305` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed.
- `origin/main` contains merge commit `ec9adc2`, PR #126 (`ODY-S05-304`).
- Branch `feat/ody-s05-305-removebodypart-dependency-closure` was created from `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 5 (`ODY-S05-305`) is `Proposed`, scoping this task to closing the `RemoveBodyPart` item/equipment-dependency stub only, explicitly not touching `DeleteCharacterPermanently` (already closed by `ODY-S05-206`).
- `RemoveBodyPart` (`SqliteCharacterRepository.cs`, direct code read, ~line 4079) currently checks only two internal Character-only dependencies inside its `MutateAnatomy` callback (`BodyPart.AttachedToBodyPartId`, `PermanentModification.AttachedToBodyPartId`), both via the existing `PersistenceFailures.CharacterBodyPartHasDependent`; the item/equipment dependency is a documented, honest stub at that exact call site.
- `HasAnyItemOwnedByCharacter`/`HasAnyRuntimeReferenceToDefinition` (`SqliteInventoryRepository.cs`, direct code read) are the exact structural precedent for a fail-closed, `EnsureInventoryTables`-first, campaign-boundary-checked existence query; `EscapeLike` is a private static helper already used by the second of these for exactly the same "canonical id contains `_`" reason `BodyPartId` also has.
- `EquippedEntry.BodyPartRefs` is a comma-joined string column (`JoinBodyPartRefs`/`SplitBodyPartRefs`, `ODY-S05-302`); `EquippedEntry` stores `InventoryId`, not `CharacterId` — confirmed no direct "Inventory by owning Character" primitive exists, so the join must go through `ItemInstance`/`ItemStack`'s own `OwnerKind`/`OwnerTargetRef` columns by `ItemRefId`, the same trick `HasAnyItemOwnedByCharacter` already uses.
- `EquipItemCore<T>`/`UnequipItemCore<T>` (direct code read, confirmed again for this task) only ever write `LocationKind`/`LocationTargetRef`/`LocationDetailRef`, never `OwnerKind`/`OwnerTargetRef` — so an item's owner does not change across Equip/Unequip, making the `OwnerKind`/`OwnerTargetRef` join stable regardless of current equip state.
- `ICharacterDeletionDependencyChecker.CheckBlockingDependency(CampaignHandle, CharacterId)` cannot express "for this specific body part" — confirmed by direct code read of its one method signature — so a new, parallel interface is required, not an extension.
- `SqliteCharacterRepository`'s constructor (direct code read, ~line 118) already has one optional checker-list parameter (`deletionDependencyCheckers`, defaulting to `Array.Empty<ICharacterDeletionDependencyChecker>()`) with no composition root anywhere in the codebase — this task's new parameter follows the identical shape for a different interface.
- `DeleteCharacterPermanently`'s own §9.2 Inventory dependency is already fully closed by `ODY-S05-206` — reconfirmed directly: `HasAnyItemOwnedByCharacter`'s SQL filters only on `CampaignId`/`OwnerKind`/`OwnerTargetRef`, never `LocationKind`, so it already counts equipped items. No work needed there.

### Assumptions

- None.

## 5. Scope

### In scope

- `IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart` + `SqliteInventoryRepository` implementation (reusing `EscapeLike`).
- `IBodyPartRemovalDependencyChecker` (`CharacterRepositoryContracts.cs`) + `InventoryBodyPartRemovalDependencyChecker` (`InventoryDeletionDependencyCheckers.cs`).
- `SqliteCharacterRepository`: new `_bodyPartRemovalDependencyCheckers` field + optional constructor parameter; wiring into `RemoveBodyPart`'s `MutateAnatomy` callback; doc-comment update (additive, not a rewrite).
- Updating any existing fake/mock implementation of `IInventoryRepository` in tests to add the new interface member.
- Persistence tests and test metadata `TC-INVENTORY-143`-`152`.
- Task contract, ExecPlan, and backlog status updates.

### Out of scope

- Any atomic auto-resolve (auto-Unequip) inside `RemoveBodyPart` — hard rejection only, per this ТЗ's own explicit decision.
- Any change to `EquipItem`/`UnequipItem`/`EquipmentService`/`EquippedEntry.cs` — this task only reads `EquippedEntry` through a new query primitive; it writes nothing to it.
- A composition root — the checker remains an explicitly-passed, opt-in constructor argument, exactly like `_deletionDependencyCheckers`.
- `DeleteCharacterPermanently` and its existing checkers (`InventoryCharacterDeletionDependencyChecker`/`InventoryContentDefinitionDependencyChecker`) — already closed, not touched.
- The two existing internal Character-only `RemoveBodyPart` dependency checks (`AttachedToBodyPartId` for `BodyPart`/`PermanentModification`) — unchanged, the new check is additive.
- Syncing stale `docs/tasks/active/`/`docs/plans/active/` files for prior tasks (pre-existing, unrelated debt).
- `ODY-S05-306` (Equipment Runtime Integration Fixtures) — the next, separate task.
- ADR edits, Unity/UI.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/**
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-305_RemoveBodyPart_Dependency_Closure.md
docs/plans/active/ODY-S05-305_RemoveBodyPart_Dependency_Closure.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: Application owns the checker interface/implementation; Persistence owns the SQLite query and the `RemoveBodyPart` call site (`ADR-001`, `ADR-027` §14).
- Authoritative-state and transaction boundary: the new checker call is a pure read inside `RemoveBodyPart`'s existing `MutateAnatomy` transaction callback — it adds a rejection path, not a write.
- Serialization / compatibility boundary: no new persisted shape; reads the existing `ODY-S05-302` `EquippedEntry` schema unchanged.
- Time / RNG rule: no new time/randomness usage.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: fail-closed on I/O error; no raw provider text or local paths in errors.
- Other: no composition root; the checker list stays an explicit, opt-in constructor argument.

## 7. Expected behavior

### Scenario 1 — real reject via an equipped item

**Given** an item equipped with `BodyPartRefs` including the target body part, and `SqliteCharacterRepository` constructed with `InventoryBodyPartRemovalDependencyChecker`
**When** `RemoveBodyPart` is called for that body part
**Then** it fails with `CharacterBodyPartHasDependent`; `CharacterAnatomy` and the `EquippedEntry` row are both unchanged.

### Scenario 2 — success after Unequip

**Given** the same setup, but the item has since been unequipped
**When** `RemoveBodyPart` is called again for the same body part
**Then** it succeeds.

### Scenario 3 — no-checker backward compatibility

**Given** `SqliteCharacterRepository` constructed without a checker list (or an empty one)
**When** `RemoveBodyPart` is called on a body part an equipped item references
**Then** it behaves exactly as before this task (the two internal checks still apply; the new one is silently absent, not silently bypassed — there is nothing to bypass since it was never passed).

### Scenario 4 — fail-closed

**Given** `HasAnyEquippedEntryReferencingBodyPart` returns a `Result.Failure`
**When** `RemoveBodyPart` is called
**Then** it is rejected (fails closed), not silently allowed.

### Required invariants

- Substring matching on comma-joined `BodyPartRefs` is correct for first/middle/last position and does not false-positive on a prefix-sharing name (e.g. searching `"head"` must not match `"headBackup"`).
- The query is scoped to the specific Character (via `OwnerKind`/`OwnerTargetRef` on the joined item row), not any Character's equipped items.
- No partial write: a rejected `RemoveBodyPart` changes nothing.
- The existing two internal Character-only checks are unaffected.
- `EquipItem`/`UnequipItem`/`EquippedEntry.cs`/`DeleteCharacterPermanently` are unchanged.

## 8. Deliverables

- Production code: `HasAnyEquippedEntryReferencingBodyPart` (interface + SQLite implementation), `IBodyPartRemovalDependencyChecker`, `InventoryBodyPartRemovalDependencyChecker`, `SqliteCharacterRepository` wiring + doc-comment update.
- Tests: the 8 minimum scenarios from the ТЗ (exact/first/middle/last substring match, prefix-boundary negative, real reject, success after unequip, no-checker compatibility, cross-repository wiring, fail-closed).
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; no schema change.

## 9. Acceptance criteria

1. `IBodyPartRemovalDependencyChecker` exists; `InventoryBodyPartRemovalDependencyChecker` implements it via the new `HasAnyEquippedEntryReferencingBodyPart` primitive.
2. `SqliteCharacterRepository` accepts an optional `IReadOnlyList<IBodyPartRemovalDependencyChecker>` via its constructor, defaulting to empty; existing call sites without this parameter are unaffected.
3. `RemoveBodyPart` invokes every passed checker and rejects with `PersistenceFailures.CharacterBodyPartHasDependent` if any finds a dependency, before any write.
4. Substring matching correctly identifies the searched `BodyPartId` at the first, middle, and last position of a comma-joined `BodyPartRefs` value, and does not false-positive on a prefix-sharing name.
5. `HasAnyEquippedEntryReferencingBodyPart` fails closed (`Result.Failure`) on I/O error, and `RemoveBodyPart` rejects rather than proceeds when a checker reports that failure.
6. The two existing internal Character-only dependency checks (`AttachedToBodyPartId`) are unchanged.
7. No auto-Unequip, no `EquipItem`/`UnequipItem`/`EquippedEntry.cs` change, no composition root, `DeleteCharacterPermanently` untouched.
8. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-143`-`152`.
9. Task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md` are updated.
10. Required validation commands pass and diff review confirms only allowed paths touched.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-143` | .NET / NUnit (Persistence) | HasAnyEquippedEntryReferencingBodyPart is true for a single-value BodyPartRefs match | Pass |
| `TC-INVENTORY-144` | .NET / NUnit (Persistence) | True when the searched BodyPartId is first in a multi-value comma-joined BodyPartRefs | Pass |
| `TC-INVENTORY-145` | .NET / NUnit (Persistence) | True when the searched BodyPartId is in the middle | Pass |
| `TC-INVENTORY-146` | .NET / NUnit (Persistence) | True when the searched BodyPartId is last | Pass |
| `TC-INVENTORY-147` | .NET / NUnit (Persistence) | False for a prefix-sharing name (searching "head" against "headBackup" does not false-positive) | Pass |
| `TC-INVENTORY-148` | .NET / NUnit (Persistence) | RemoveBodyPart with a real equipped item and a wired checker is rejected with CharacterBodyPartHasDependent; no partial write | Pass |
| `TC-INVENTORY-149` | .NET / NUnit (Persistence) | RemoveBodyPart succeeds after the item is unequipped | Pass |
| `TC-INVENTORY-150` | .NET / NUnit (Persistence) | RemoveBodyPart with no checker (default constructor) behaves exactly as before; existing internal checks unaffected | Pass |
| `TC-INVENTORY-151` | .NET / NUnit (Persistence) | Cross-repository wiring: SqliteCharacterRepository + InventoryBodyPartRemovalDependencyChecker wrapping a real SqliteInventoryRepository, one campaign, end-to-end | Pass |
| `TC-INVENTORY-152` | .NET / NUnit (Persistence) | Fail-closed: a Result.Failure from the dependency query rejects RemoveBodyPart, not silently allowed | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only allowed paths touched; `EquipItem`/`UnequipItem`/`EquippedEntry.cs`/`DeleteCharacterPermanently` untouched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- `DeleteCharacterPermanently`/auto-resolve testing because those are out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: no schema change; adds one query primitive and one optional constructor parameter.
- Version fields affected: no manifest/application/schema version is bumped in this task.
- Migration or upcaster: none.
- Forward / backward behavior: existing callers of `SqliteCharacterRepository`'s constructor without the new parameter are unaffected.
- Rollback method: revert this branch/PR before merge.
- Data-loss risk and protection: none; the new code path is read-only until it decides to reject.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic Character/Equipment test records; no real player data.
- Trust boundaries: none new; this task adds a read-only dependency check inside an existing MainGM-gated command.
- Authorization / audience checks: unchanged — `RemoveBodyPart`'s own existing MainGM gate applies.
- Redaction requirements: checker failure messages surface only the safe `Error.Code`, never raw provider text or local paths (matching `InventoryCharacterDeletionDependencyChecker`'s own convention).
- Log-safe fields: no logging added.
- Abuse / malformed input limits: `EscapeLike` reused, not reimplemented, avoiding a LIKE-injection-style bug via unescaped `_`/`%`.
- Security tests: fail-closed (`TC-INVENTORY-152`).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task adds a new repository query primitive, a new cross-module checker interface/implementation, and a constructor-level wiring change spanning two repositories.
- ExecPlan path: `docs/plans/active/ODY-S05-305_RemoveBodyPart_Dependency_Closure.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: must follow merged PR #126 and precede `ODY-S05-306`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`. `RemoveBodyPart`'s own doc-comment (additive update, not a rewrite).
- Documents that must not change: accepted ADRs; `EquippedEntry.cs`; `EquipItem`/`UnequipItem`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one repository query primitive and one checker interface; no manifest/schema version bump or protocol/ruleset change.
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

- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — `IInventoryRepository.HasAnyEquippedEntryReferencingBodyPart`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` — `IBodyPartRemovalDependencyChecker`.
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs` — `InventoryBodyPartRemovalDependencyChecker` added next to its two siblings.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — SQLite implementation of the new query (reuses `EscapeLike`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` — new field/constructor parameter, `RemoveBodyPart` call site, additive doc-comment update.
- `DotNet/Tests/Odyssey.Tests.Persistence/BodyPartRemovalDependencyCheckerTests.cs` (new) — `TC-INVENTORY-143`-`152`.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — `ThrowingInventoryRepository` fake updated with the new interface member.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-143`-`152` entries.
- No `docs/errors/ERROR_CODES.md` change: the new checker reuses the existing `persistence.character.body_part_has_dependent` code.
- No `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` change needed: this task added no file under `Packages/**/Runtime/Inventory/**`.
- This task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 485 (771 total, 0 failed). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` on first run (no new error codes to register). |
| `.\scripts\verify-test-structure.ps1` | Passed | Exit code 0; `TC-ARCH-001 PASS valid ADR-001 graph passes`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `InventoryBodyPartRemovalDependencyChecker` implements `IBodyPartRemovalDependencyChecker` via `HasAnyEquippedEntryReferencingBodyPart`. |
| AC-2 | Passed | `SqliteCharacterRepository`'s new constructor parameter defaults to `Array.Empty<IBodyPartRemovalDependencyChecker>()`; `TC-INVENTORY-150` proves unchanged behavior with no checker. |
| AC-3 | Passed | `TC-INVENTORY-148`, `TC-INVENTORY-151`. |
| AC-4 | Passed | `TC-INVENTORY-143`-`147` (single/first/middle/last/prefix-boundary). |
| AC-5 | Passed | `TC-INVENTORY-152` (fail-closed via `BreakItemInstanceOwnerColumn`). |
| AC-6 | Passed | `TC-INVENTORY-150`'s second assertion (Torso removal still blocked by `LeftArm.AttachedToBodyPartId`). |
| AC-7 | Passed | Diff review: no `EquipItem`/`UnequipItem`/`EquippedEntry.cs` change, no composition root, `DeleteCharacterPermanently` untouched (confirmed by isolated diff review of `SqliteCharacterRepository.cs`). |
| AC-8 | Passed | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-143`-`152`. |
| AC-9 | Passed | This task contract, ExecPlan, and backlog updated with Draft PR link (filled after PR opens). |
| AC-10 | Passed | `git diff --name-status` review found only allowed paths touched. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- No atomic auto-resolve (auto-Unequip) is implemented — hard rejection only, by design.
- `ODY-S05-201`-`207`'s own task/plan files remain in `docs/tasks/active/`/`docs/plans/active/` despite `Done` backlog status — a pre-existing, unrelated documentation-sync gap, observed but not fixed here (out of this task's scope).

### Follow-up tasks

- `ODY-S05-306` — Equipment Runtime Integration Fixtures.
- (Unreserved, not yet numbered) A future task would be needed if atomic auto-resolve (auto-Unequip inside `RemoveBodyPart`) is ever required — deliberately not implemented here.

### Self-review summary

- Scope review: diff limited to the new query primitive, the new checker interface/implementation, `SqliteCharacterRepository`'s constructor/`RemoveBodyPart` call site, tests, metadata, and task/plan/backlog docs; no Unity files, ADR edits, or `EquipItem`/`UnequipItem`/`EquippedEntry.cs`/`DeleteCharacterPermanently` changes.
- Architecture review: Application owns the checker interface/implementation; Persistence owns the SQLite query and the `RemoveBodyPart` call site; no composition root; the two existing internal Character-only checks are unaffected.
- Test review: `TC-INVENTORY-143`-`152` added, covering all 8 minimum scenarios from the ТЗ plus a scoping test (151); full `dotnet test` passed with no scope-guard breaks.
- Security/privacy review: fail-closed on I/O error; `EscapeLike` reused, not reimplemented; no private material, secrets, logging, or diagnostics added.
- Documentation/version review: test metadata and task/plan/backlog updated; `RemoveBodyPart`'s doc-comment amended additively; no error registry change needed; no application/schema/protocol/ruleset version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — **Design decision (required by this ТЗ §2): hard rejection only, no atomic auto-resolve.** `ODY-S05-206`'s own precedent for the same stub category (Character-deletion × Inventory) is a pure reject with no auto-unwinding; `ADR-027` §7 rule 5 leaves auto-resolve as an option, not an obligation. Implementing it would require deciding how to select a destination for the auto-unequipped item — a materially different, unscoped decision — so it is deliberately not implemented; a future, not-yet-numbered task would own it if ever requested. Authority: this ТЗ §2's own explicit direction and citation of the `ODY-S05-206` precedent.
- 2026-09-11 — Decision: new, parallel `IBodyPartRemovalDependencyChecker` interface, not an extension of `ICharacterDeletionDependencyChecker`. Rationale: the existing interface's `CheckBlockingDependency(CampaignHandle, CharacterId)` signature has no way to carry a `BodyPartId`; changing it would break every existing implementation/call site for an unrelated operation (`DeleteCharacterPermanently`). Authority: this ТЗ §5's own explicit direction; direct inspection of the existing interface signature.
- 2026-09-11 — Decision: `HasAnyEquippedEntryReferencingBodyPart`'s substring match wraps both the stored `BodyPartRefs` value and the search pattern in commas (`(',' || BodyPartRefs || ',') LIKE '%,' || escaped + ',%'`), making first/middle/last position and the `"head"` vs. `"headBackup"` boundary case all correct by construction, verified by direct test coverage at all three positions plus the negative case. Authority: this ТЗ §4's own explicit correctness requirement.
- 2026-09-11 — Decision: a separate `_bodyPartRemovalDependencyCheckers` field/constructor parameter, not reusing or extending `_deletionDependencyCheckers`. Rationale: different interface, different operation (`RemoveBodyPart` vs. `DeleteCharacterPermanently`); conflating them would force every future `ICharacterDeletionDependencyChecker` implementation to also implement `IBodyPartRemovalDependencyChecker` or vice versa, for no shared reason. Authority: this ТЗ §5.3's own explicit instruction not to reuse the existing parameter.
- 2026-09-11 — Decision: reuse the existing `PersistenceFailures.CharacterBodyPartHasDependent` error code for the new checker's rejection, the same code the two existing internal checks already use. Rationale: uniform GM-facing error category matters more than distinguishing the cause at the error-code level; the checker's own `blockingDependency` description string exists for diagnostic purposes but has no carrier field in today's `Result`/`Error` shape, matching the two existing checks' own limitation. Authority: this ТЗ §5.4's own explicit instruction.
- 2026-09-11 — Decision: `RemoveBodyPart`'s doc-comment is amended additively (a new paragraph appended), not rewritten — the historical record of why the stub existed and what changed it stays intact, mirroring `ODY-S05-206`'s own amendment of `DeleteCharacterPermanently`'s doc-comment for the identical kind of stub-closure event. Authority: this ТЗ §5.4's own explicit instruction; `ODY-S05-206` precedent.

### Approved task changes

- None.
