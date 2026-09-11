# ODY-S05-304 — Unequip Command MVP

**Status:** In Progress
**Roadmap stage / slice:** SLICE-05 (Equipment runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-304-unequip-command-mvp`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S05-304_Unequip_Command_MVP.md`
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

A MainGM-only, CAS-guarded, `CommandId`-idempotent Unequip command that atomically moves an equipped `ItemInstance`/`ItemStack` back into a `Contained` location in the same Inventory and removes its `EquippedEntry` row — the symmetric reverse of `ODY-S05-303`'s Equip transition, with rule 3's valid-destination check.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-303` gave items a way into `Equipped` state but no way back out; `EquippedEntry` rows would be permanent once created.
- Value or risk reduction: without this task, `ODY-S05-305`'s `RemoveBodyPart` dependency closure would have nothing to test against (an item can never stop depending on a body part) and `ODY-S05-306`'s integration fixture cannot complete its Equip→Unequip→RemoveBodyPart round trip.
- Blocking or enabling relationship: unblocks `ODY-S05-305` (RemoveBodyPart dependency closure) and `ODY-S05-306` (integration fixtures).

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §12 row 4, §12.1.
- `docs/tasks/active/ODY-S05-303_Equip_Command_MVP.md` (structural template — mirror, don't reinvent).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, section 7 (rule 3), section 14.
- Existing patterns: `EquipItem`/`EquipItemCore<T>`, `ReplaceEquippedEntry`/`DeleteEquippedEntry`, `MoveItem<T>` (all in `SqliteInventoryRepository.cs`), `EquipmentService.cs`.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-304`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`130` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-131`-`142`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-304` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed.
- `origin/main` contains merge commit `856642c`, PR #125 (`ODY-S05-303`).
- Branch `feat/ody-s05-304-unequip-command-mvp` was created from `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 4 (`ODY-S05-304`) is `Proposed` and scopes this task to the Unequip transition and rule 3, explicitly excluding new Equip semantics and `RemoveBodyPart`.
- `EquipItem`/`EquipItemCore<T>` (`SqliteInventoryRepository.cs`, direct code read) is the exact structural precedent: ledger replay check, source-validity + CAS check, rule-1 check, `LocationRef` `UPDATE`, `EquippedEntry` `INSERT`, one ledger write, one transaction — all inside `EquipItemCore<T>` itself, not composed from separate public method calls.
- `DeleteEquippedEntry` only deletes the `EquippedEntry` row; it never touches `ItemInstance`/`ItemStack` — confirmed by direct code read, the same gap `CreateEquippedEntry` had for the Equip direction before `EquipItem` closed it.
- `MoveItem<T>` cannot be reused as-is: it hardcodes `InventoryLocationKind.Contained` as both the required source kind and the literal destination kind it writes, and it manages two separate `Inventory.Revision` CAS updates (source/destination) for cross-container transfer — none of which Unequip needs, since Unequip never changes `InventoryId`.
- `EquipmentCommandLedger` (`CommandId` PK, `OperationKind`, `ItemRefId`, `ExpectedRevision`, `CreatedAt`, `AppliedAt`) already carries a different `ExpectedRevision` meaning per operation kind: `0` for `Create`; the `EquippedEntry`'s own revision for `Replace`/`Delete`; the *item's* own revision for `Equip`. This task's `"Unequip"` kind uses the `EquippedEntry`'s own revision (matching `Replace`/`Delete`'s convention), since `EquippedEntry` is this task's chosen authority for "is this item currently equipped" (§18).
- `InventoryMovementFailures.SourceInvalid` (`ODY-S05-204`) is currently used only to mean "not `Contained` in the stated source"; this task reuses it with the broader reading "the target is not where the caller expects it to be" for an `InventoryId` mismatch between the caller's request and the `EquippedEntry`'s own stored `InventoryId`.
- No existing transactional method writes `InventoryLocationKind.SceneDropped`/`Other` as a destination anywhere in the codebase; `ODY-S05-204`'s own task contract §7 explicitly rejects both as Move destinations.

### Assumptions

- None.

## 5. Scope

### In scope

- `UnequipTransition` (Application record: `ItemRef`, `InventoryId`, `ExpectedTargetRevision`, `ExpectedEquippedEntryRevision`, `DestinationContainerKey`, `CommandId`).
- `IInventoryRepository.UnequipItem` and its `SqliteInventoryRepository` implementation (`UnequipItemCore<T>`, reusing `EquipmentCommandLedger` with a new `"Unequip"` operation kind).
- `EquipmentService.Unequip`, `UnequipRequest` (added to the existing `EquipmentService.cs`, next to `Equip`).
- Persistence tests and test metadata `TC-INVENTORY-131`-`142`.
- Task contract, ExecPlan, and backlog status updates.
- An explicit, documented decision that non-`Contained` Unequip destinations are out of scope and require a separate, explicitly-numbered follow-up task if ever needed.

### Out of scope

- Rule 4 (body-part existence) or any `ICharacterRepository` dependency in Unequip — removing an item does not need to re-validate anatomy.
- `RemoveBodyPart` dependency closure (rule 5, `ODY-S05-305`'s own job).
- Non-`Contained` Unequip destinations (`SceneDropped`/`Other`) — explicitly deferred (§11/§18), not implemented "by default out of curiosity."
- Weapon/armor mechanical effects.
- Any change to `EquipItem`/`EquipItemCore<T>` (the Equip direction, `ODY-S05-303`) without a found, documented contradiction (none was found).
- `EquippedEntry.cs` (the Domain type).
- ADR edits, Unity/UI.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Inventory/EquipmentService.cs
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
DotNet/Tests/Odyssey.Tests.Persistence/**
DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/active/ODY-S05-304_Unequip_Command_MVP.md
docs/plans/active/ODY-S05-304_Unequip_Command_MVP.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: Application owns command orchestration; Persistence owns the atomic SQLite transition (`ADR-001`, `ADR-027` §14).
- Authoritative-state and transaction boundary: the item's `LocationRef` update, the `EquippedEntry` delete, and the ledger write happen in one SQLite transaction. `CommandId` is the idempotency key; both the `EquippedEntry`'s own `Revision` and the item's own `Revision` are CAS keys.
- Serialization / compatibility boundary: no new persisted shape; reuses `ODY-S05-302`'s `EquippedEntry`/`EquipmentCommandLedger` schema.
- Time / RNG rule: repository timestamps come from injected `IWallClock`; no randomness beyond caller-provided IDs.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: no hidden campaign data, private docs, raw exceptions, or secrets in tests/docs.
- Other: `EquipmentService.Unequip` does not depend on `ICharacterRepository`; the repository primitive does not reimplement Equip semantics or accept a destination `InventoryId` different from the item's own.

## 7. Expected behavior

### Scenario 1 — successful Unequip

**Given** an equipped `ItemInstance`/`ItemStack` at the expected item and `EquippedEntry` revisions, and a MainGM actor
**When** `EquipmentService.Unequip` is called with a destination container key
**Then** the item's `LocationRef` becomes `Contained(InventoryId, destinationContainerKey)`, its `Revision` advances by one, and its `EquippedEntry` row no longer exists.

### Scenario 2 — not equipped

**Given** an item with no `EquippedEntry` row
**When** `EquipmentService.Unequip` is called for it
**Then** it is rejected with `EquipmentEntryNotFound`; no row changes.

### Scenario 3 — CAS conflict (either level)

**Given** a stale expected `EquippedEntry` revision or a stale expected item revision
**When** `EquipmentService.Unequip` is called
**Then** it is rejected as the corresponding revision conflict; no row changes.

### Scenario 4 — `InventoryId` mismatch

**Given** a caller-stated `InventoryId` that does not match the `EquippedEntry`'s own stored `InventoryId`
**When** `EquipmentService.Unequip` is called
**Then** it is rejected (`InventoryMovementFailures.SourceInvalid`); no row changes.

### Scenario 5 — non-MainGM denial

**Given** a non-MainGM actor
**When** `EquipmentService.Unequip` is called
**Then** it fails before any repository call.

### Scenario 6 — exact replay

**Given** a successful Unequip command
**When** replayed with the identical `CommandId`
**Then** it returns success without a second mutation.

### Required invariants

- MainGM-only; the check precedes every repository call.
- `EquippedEntry` is the authoritative source for "is this item currently equipped"; the item's own `LocationRef` is cross-checked defensively (mismatch is treated as an inconsistency, `InventoryIoFailed`, never silently repaired).
- Destination is always `Contained` in the item's own current `InventoryId` (no cross-Inventory transfer, no non-`Contained` destination).
- Failure leaves no partial update: the item's `LocationRef` and the `EquippedEntry` row's existence change together or not at all.
- No rule-4/`ICharacterRepository` dependency, `RemoveBodyPart` behavior, or weapon/armor mechanical effect is introduced.
- `EquipItem`/`EquipItemCore<T>` is unchanged.

## 8. Deliverables

- Production code: `UnequipTransition`, `IInventoryRepository.UnequipItem` + SQLite implementation, `EquipmentService.Unequip`, `UnequipRequest`.
- Tests: persistence/application-level tests for success (instance + stack), not-equipped, both CAS-conflict levels, `CommandId` collision, replay, MainGM denial, `InventoryId` mismatch.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; no new table.

## 9. Acceptance criteria

1. `UnequipItem` atomically updates the target's `LocationRef` to `Contained(InventoryId, DestinationContainerKey)` and removes its `EquippedEntry` row in one SQLite transaction.
2. `EquippedEntry` is read and CAS-checked as the authority; the item's `LocationRef` is defensively cross-checked against `EquippedEntry.ToLocationRef()` and a mismatch is rejected as `InventoryIoFailed`, not silently corrected.
3. An item with no `EquippedEntry` row is rejected with `EquipmentEntryNotFound`.
4. A stale `EquippedEntry` revision is rejected with `EquipmentEntryRevisionConflict`; a stale item revision is rejected with `ItemRevisionConflict`; neither row changes.
5. An `InventoryId` mismatch between the request and the `EquippedEntry`'s own stored `InventoryId` is rejected (`InventoryMovementFailures.SourceInvalid`).
6. Replaying the same `CommandId` with matching identity returns success without a second mutation; a different identity is rejected with `CommandIdentityMismatch`.
7. A non-MainGM actor is rejected before any repository call.
8. No rule-4/`ICharacterRepository` dependency, `RemoveBodyPart` behavior, weapon/armor mechanical effect, or non-`Contained` destination is introduced.
9. `EquipItem`/`EquipItemCore<T>` (`ODY-S05-303`) and `EquippedEntry.cs` (`ODY-S05-301`) are unchanged.
10. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-131`-`142`.
11. Task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md` are updated.
12. Required validation commands pass and diff review confirms no Unity files, ADR edits, `EquipItem`/`EquippedEntry.cs` changes, or rule-4/`RemoveBodyPart`/weapon-armor behavior.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-131` | .NET / NUnit (Persistence) | UnequipItem (ItemInstance) succeeds: LocationRef becomes Contained, EquippedEntry removed | Pass |
| `TC-INVENTORY-132` | .NET / NUnit (Persistence) | UnequipItem (ItemStack) succeeds with the same guarantee | Pass |
| `TC-INVENTORY-133` | .NET / NUnit (Persistence) | UnequipItem rejects an item with no EquippedEntry row | Pass |
| `TC-INVENTORY-134` | .NET / NUnit (Persistence) | UnequipItem rejects a stale expected EquippedEntry revision; row unchanged | Pass |
| `TC-INVENTORY-135` | .NET / NUnit (Persistence) | UnequipItem rejects a stale expected item revision; row unchanged | Pass |
| `TC-INVENTORY-136` | .NET / NUnit (Persistence) | UnequipItem rejects an InventoryId mismatch against the EquippedEntry's own InventoryId | Pass |
| `TC-INVENTORY-137` | .NET / NUnit (Persistence) | UnequipItem replay with the same CommandId returns success without a second mutation | Pass |
| `TC-INVENTORY-138` | .NET / NUnit (Persistence) | UnequipItem with the same CommandId reused for a different target is rejected with CommandIdentityMismatch | Pass |
| `TC-INVENTORY-139` | .NET / NUnit (Persistence) | EquipmentService.Unequip denies a non-MainGM actor before any repository call | Pass |
| `TC-INVENTORY-140` | .NET / NUnit (Persistence) | A full Equip-then-Unequip round trip leaves the item Contained in its original container key context and re-equippable | Pass |
| `TC-INVENTORY-141` | .NET / NUnit (Persistence) | UnequipItem rejects a source item that is not currently Equipped (e.g. still Contained) | Pass |
| `TC-INVENTORY-142` | .NET / NUnit (Unit) | Scope guard: no rule-4/RemoveBodyPart/weapon-armor type or ICharacterRepository dependency introduced by EquipmentService.Unequip | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no Unity files, ADR edits, `EquipItem`/`EquippedEntry.cs` changes, or rule-4/`RemoveBodyPart`/weapon-armor behavior.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Rule-4/`RemoveBodyPart`/weapon-armor testing because those systems are excluded.

## 11. Compatibility, migration, and rollback

- Compatibility impact: no new table; reuses `ODY-S05-302`'s `EquippedEntry`/`EquipmentCommandLedger` schema with a new ledger `OperationKind` value.
- Version fields affected: no manifest/application/schema version is bumped in this task.
- Migration or upcaster: none.
- Forward / backward behavior: older builds will not call the new primitive; no data format change.
- Rollback method: revert this branch/PR before merge.
- Data-loss risk and protection: low; the transition is fully transactional.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic Equipment test records; no real player data.
- Trust boundaries: `EquipmentService.Unequip` receives an already-authenticated actor identity/MainGM flag from its caller.
- Authorization / audience checks: MainGM-only, enforced before any repository call.
- Redaction requirements: no diagnostics or network projections added.
- Log-safe fields: no logging added.
- Abuse / malformed input limits: request/transition constructors reject invalid IDs/refs/null payloads.
- Security tests: MainGM denial, `CommandId` reuse mismatch rejection, both CAS-conflict levels.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task adds a new repository primitive and CAS/idempotency behavior spanning two persisted record kinds.
- ExecPlan path: `docs/plans/active/ODY-S05-304_Unequip_Command_MVP.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: must follow merged PR #125 and precede `ODY-S05-305`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs; `EquipItem`/`EquipItemCore<T>`; `EquippedEntry.cs`; `RemoveBodyPart`'s doc-comment (reserved for `ODY-S05-305`).
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one repository primitive and one new ledger operation-kind value; no manifest/schema version bump or protocol/ruleset change.
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

- `Packages/com.odyssey.application/Runtime/Inventory/EquipmentService.cs` — `EquipmentService.Unequip`, `UnequipTransition`, `UnequipRequest` added next to `Equip`/`EquipTransition`/`EquipRequest`.
- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — `IInventoryRepository.UnequipItem`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — `UnequipItem`/`UnequipItemCore<T>` (atomic reverse transition, reusing `EquipmentCommandLedger` with a new `"Unequip"` operation kind).
- `DotNet/Tests/Odyssey.Tests.Persistence/EquipmentServiceTests.cs` — `TC-INVENTORY-131`-`142` added alongside the existing Equip tests.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryStackOperationServiceTests.cs` — `ThrowingInventoryRepository` fake updated with `UnequipItem`.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-131`-`142` entries.
- No `docs/errors/ERROR_CODES.md` change: every failure path reuses an existing, already-registered error code.
- No `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` change needed this time: the file-name guard already admits `EquipmentService.cs` (from `ODY-S05-303`) and this task added no new file.
- This task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 475 (761 total, 0 failed). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` on first run (no new error codes to register). |
| `.\scripts\verify-test-structure.ps1` | Passed | Exit code 0; `TC-ARCH-001 PASS valid ADR-001 graph passes`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `UnequipItemCore<T>` updates `LocationRef` and deletes `EquippedEntry` in one `SqliteTransaction`. |
| AC-2 | Passed | `TC-INVENTORY-141` proves the defensive cross-check fires (`InventoryIoFailed`) rather than silently trusting either side. |
| AC-3 | Passed | `TC-INVENTORY-133`. |
| AC-4 | Passed | `TC-INVENTORY-134`-`135`. |
| AC-5 | Passed | `TC-INVENTORY-136`. |
| AC-6 | Passed | `TC-INVENTORY-137`-`138`. |
| AC-7 | Passed | `TC-INVENTORY-139`. |
| AC-8 | Passed | `TC-INVENTORY-142` (reflection: no `ICharacterRepository` parameter, no forbidden type fragment); diff review confirms no non-`Contained` destination path exists. |
| AC-9 | Passed | `git diff` confirms `EquipItemCore<T>`/`EquipItem` and `EquippedEntry.cs` untouched. |
| AC-10 | Passed | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-131`-`142`. |
| AC-11 | Passed | This task contract, ExecPlan, and backlog updated with Draft PR link (filled after PR opens). |
| AC-12 | Passed | `git diff --name-status` review found no Unity files, ADR edits, `EquipItem`/`EquippedEntry.cs` changes, or rule-4/`RemoveBodyPart`/weapon-armor behavior. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- Non-`Contained` Unequip destinations (`SceneDropped`/`Other`) are not implemented — see §18's deferred-follow-up note; a future task needs its own task ID if this is ever required.
- No rule-4/`RemoveBodyPart` dependency behavior is implemented by design — `ODY-S05-305`'s own job.
- `ODY-S05-201`-`207`'s own task/plan files remain in `docs/tasks/active/`/`docs/plans/active/` despite `Done` backlog status — a pre-existing, unrelated documentation-sync gap, observed but not fixed here (out of this task's scope).

### Follow-up tasks

- `ODY-S05-305` — RemoveBodyPart Dependency Closure.
- (Unreserved, not yet numbered) A future task would be needed if non-`Contained` Unequip destinations are ever required — deliberately not implemented here; no task ID exists for it yet.

### Self-review summary

- Scope review: diff limited to Application command orchestration, one new repository primitive, tests, metadata, and task/plan/backlog docs; no Unity files, ADR edits, or `EquipItem`/`EquippedEntry.cs` changes. No guard files needed touching this time.
- Architecture review: Application (`EquipmentService.Unequip`) owns MainGM gating only, no `ICharacterRepository` dependency; Persistence (`UnequipItemCore<T>`) owns the atomic reverse transition with `EquippedEntry` as its chosen authority; destination scope deliberately limited to `Contained` in the same Inventory.
- Test review: `TC-INVENTORY-131`-`142` added, including a genuine bug caught during authoring (a test assertion's own revision-math error, not a production defect — fixed before finalizing); full `dotnet test` passed.
- Security/privacy review: no private material, hidden campaign data, secrets, logging, diagnostics, or network projections added; MainGM gate precedes every repository call.
- Documentation/version review: test metadata, task/plan/backlog updated; no error registry change needed; no application/schema/protocol/ruleset version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — **Design decision (required by this ТЗ §2): `EquippedEntry` is the authoritative source for "is this item currently equipped," not the item's own `LocationRef`.** The transaction reads and CAS-checks `EquippedEntry` first, then reads the item and defensively cross-checks that its `LocationRef.Kind == Equipped` and equals `EquippedEntry.ToLocationRef()`. A mismatch is rejected as `InventoryIoFailed` (an unexpected-inconsistency category), never silently repaired in either direction — this state should be unreachable given `EquipItem` is the only writer of both records together, and defensive code must say so loudly rather than guess which side is "right." Authority: this ТЗ §2's own explicit recommendation.
- 2026-09-11 — **Design decision (required by this ТЗ §3, the main open question): Unequip destinations are `Contained`-only for this MVP; no cross-Inventory transfer, no `SceneDropped`/`Other` destination.** Neither `ADR-027` §7 rule 3 nor the `ODY-S05-204` vocabulary this task is told to reuse establishes any precedent for writing `SceneDropped`/`Other` transactionally (confirmed: no existing method ever does), and `ODY-S05-204`'s own task contract explicitly rejects both as Move destinations. Implementing "drop to the floor on unequip" would additionally require deciding how `OwnerRef` changes (unequipping onto the floor plausibly changes ownership, which is a materially different, unscoped decision) — out of proportion for this MVP. Per `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own rule that a task must name an explicit follow-up rather than leave an unnamed TODO, this decision is recorded here and in §17's "Follow-up tasks" as a not-yet-numbered future task, since no task ID is reserved for it and this task has no authority to mint one unilaterally. Authority: this ТЗ §3's own explicit recommendation and citation of the "no unnamed TODO" rule.
- 2026-09-11 — Decision: the `"Unequip"` `EquipmentCommandLedger` `ExpectedRevision` means the `EquippedEntry`'s own revision (matching `Replace`/`Delete`'s convention), not the item's revision (unlike `"Equip"`, which uses the item's revision since no `EquippedEntry` exists yet at that point to anchor identity to). Rationale: for Unequip, the `EquippedEntry` is the record about to be deleted and is this task's own chosen authority (see above), so anchoring replay identity to it is the natural choice; the item's own revision is still independently CAS-checked inside the transaction, just not part of ledger replay identity. Authority: internal consistency with the authority decision above.
- 2026-09-11 — Decision: reuse `InventoryMovementFailures.SourceInvalid` for an `InventoryId` mismatch between the caller's request and the `EquippedEntry`'s own stored `InventoryId`, under the broader reading "the target is not where the caller expects," rather than minting a new code. Rationale: the existing code already means exactly this kind of location-expectation mismatch for Move; no genuinely new condition exists here to justify a new code. Authority: this ТЗ §5's own explicit invitation to decide, and the precedent of `ODY-S05-202` reusing `CommandIdentityMismatch` rather than duplicating an existing category.
- 2026-09-11 — Decision: `EquipmentService.Unequip` takes no `ICharacterRepository` parameter. Rationale: rule 4 (a body part must currently exist) only matters when something is newly being attached to that body part; removing an already-equipped item does not depend on the Character's current anatomy at all. Authority: this ТЗ §4's own explicit direction; no counter-evidence found in `ADR-027` §7's rule text.
- 2026-09-11 — Decision: `UnequipItemCore<T>` inlines its `DELETE FROM EquippedEntry` directly rather than calling the public `DeleteEquippedEntry` method from within its own transaction. Rationale: matches `EquipItemCore<T>`'s own established precedent of inlining the `INSERT INTO EquippedEntry` rather than calling `CreateEquippedEntry` — a single physically-atomic transaction, not a composition of two public repository calls. Authority: `EquipItemCore<T>`'s own existing structure (`ODY-S05-303`).

### Approved task changes

- None.
