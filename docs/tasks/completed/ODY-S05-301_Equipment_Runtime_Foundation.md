# ODY-S05-301 — Equipment Runtime Foundation

**Status:** Done (PR #123, merged into main)
**Roadmap stage / slice:** SLICE-05 (Equipment runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-301-equipment-runtime-foundation`
**Pull request:** [#123](https://github.com/odyssey-services/Odyssey_VTT/pull/123)
**ExecPlan:** `docs/plans/active/ODY-S05-301_Equipment_Runtime_Foundation.md`
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Add the minimal Domain vocabulary for Equipment runtime state — `ADR-027` section 7's `EquippedEntry` shape (exact item/stack reference, equipment slot token, optional body-part references, and the audit/revision fields) — with rule 1 ("one item or stack is in exactly one place") expressed as a checkable, type-level invariant. This task intentionally does not implement persistence, repositories, Equip/Unequip commands, the rule-4 body-part-existence check, or the `RemoveBodyPart` dependency check.

## 2. Why this task exists

- Problem or dependency being addressed: the Equipment runtime block (`ODY-S05-301`–`306`) needs stable domain vocabulary before persistence (`ODY-S05-302`) can store it, mirroring exactly why `ODY-S05-201` preceded `ODY-S05-202` for Inventory.
- Value or risk reduction: gives future Equip/Unequip/RemoveBodyPart-closure tasks a small, tested contract surface without deciding persistence schema or command behavior in the foundation task.
- Blocking or enabling relationship: unblocks `ODY-S05-302` (Equipment Persistence Foundation) and, transitively, `ODY-S05-303`–`306`.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12/§12.1 (`ODY-S05-301`'s own row and task-boundary paragraph).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §5 (Inventory aggregate boundary — `EquippedEntries` already named as an `Inventory` field), §7 (Equipment model, the normative source for this task, full text re-verified against the file), §9.1 (`RemoveBodyPart` stub this block will eventually close), §14 (module ownership — Domain owns "equipment placement" and "one-place item rules").
- `docs/tasks/active/ODY-S05-201_Inventory_Runtime_Foundation.md` and `docs/plans/active/ODY-S05-201_Inventory_Runtime_Foundation.md` — the structural template this task follows near one-to-one.
- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`, `Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs`, `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs`, `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`RemoveBodyPart`) — read directly to confirm current-state facts, not paraphrased from this ТЗ.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-301`, `SLICE-05`; `ADR-027` §5/§7/§14.
- Existing test IDs: `TC-INVENTORY-001`–`094` (re-verified unmodified, one existing scope-guard assertion updated — see §18).
- New test IDs introduced: `TC-INVENTORY-095`–`100`.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` confirmed `origin/main` at `7c5094c` (merge of PR #122, `ODY-S05-108`); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 (`ODY-S05-301`) reads `Proposed`.
- `ADR-027` §7, read in full directly from the file: `EquippedEntry` (`InventoryId`, `ItemRef`, `EquipmentSlotRef`, `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision`) and six numbered rules, matching this task's own ТЗ quotation verbatim.
- `ADR-027` §5 already names `EquippedEntries` as a conceptual field of the `Inventory` aggregate (alongside `StackEntries`/`UniqueItemIds`), confirming Equipment state belongs on the existing Inventory aggregate, not a new root.
- `ADR-027` §14: "`Odyssey.Domain` owns pure identity/value invariants for `Inventory`, `ItemInstance`, `ItemStack`, equipment placement, snapshot identity, and one-place item rules." — Domain, not only Application, is explicitly assigned "equipment placement" and "one-place item rules" ownership.
- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`: `InventoryLocationRef.Equipped(InventoryId, string equipmentSlotRef)` (lines ~144-149) is a bare location-kind marker `(Kind=Equipped, TargetRef=inventoryId, DetailRef=equipmentSlotRef)` — no `BodyPartRefs[]`/`EquippedByUserId`/`EquippedAt`/`Revision` field exists on it or anywhere else. `InventoryOwnerRef.IsToken(string?)` (lines ~102-112) is the existing canonical-token validator (lowercase alnum + `_`/`-`, ≤96 chars, no surrounding whitespace) already reused by `Contained`/`Equipped`/`SceneDropped`.
- `Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs`: `BodyPartId` (lines ~54-72) is a validated `readonly struct` (`^[A-Za-z][A-Za-z0-9_]{0,63}$`), already the sole body-part-reference type in this codebase — reused directly, not duplicated. `CharacterAnatomy` (lines ~204-236) is the direct Domain precedent for a `sealed class` carrying `IReadOnlyList<T>` fields plus a `long Revision` field with a `revision < 1` throw guard — this task's own `EquippedEntry` follows that exact shape.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs`: `UserId` (lines ~82-93) exists with `Parse`/`TryParse` but **no `NewId` method** — confirmed directly, not assumed; test code constructs one via `UserId.Parse("user_" + Guid.NewGuid().ToString("N"))`, the same pattern already used throughout `Odyssey.Tests.Persistence`.
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`: `ArmorDefinition.EquipmentSlotKey`/`CoveredBodyPartIds` (lines ~181-207) are catalog metadata (what an item requires), not runtime "what is currently equipped" state; its own doc-comment states directly "no `EquipmentSlot` catalog type exists". Not touched by this task.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `RemoveBodyPart` (lines ~4061-4148): still only checks internal `AttachedToBodyPartId` dependencies; its own doc-comment defers the real item/equipment check to "the Equipment runtime block", i.e. this decomposition and its later tasks (`ODY-S05-305`). Not touched by this task.
- `git grep -il equipment` across the tracked repository (outside `docs/`) returns only the files already named above plus prose comments and negative/guard-rail tests asserting no Equipment table/type/class exists — re-confirmed unchanged since `ODY-S05-108`'s own decomposition.
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs`'s existing scope-guard test (`InventoryRuntimeScope_AllowsOnlyMoveBehaviorBeforeLaterInventoryTasks`) asserted `Any(...Equipment...) Is.False` over `Packages/**/Runtime/Inventory/**` filenames — this must be updated to admit this task's own new file, the same way it was already updated for `ODY-S05-204`'s Move service and `ODY-S05-205`'s Stack files.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read`/`git grep`/`dotnet build`/`dotnet test` during this task.

## 5. Scope

### In scope

- One new Domain type, `EquippedEntry`, in `Packages/com.odyssey.domain/Runtime/Inventory/EquippedEntry.cs` (new file, not appended to `InventoryRuntime.cs` — see §18): a sealed class carrying `InventoryId`, `InventoryItemRef ItemRef`, `string EquipmentSlotRef` (canonical token, reusing `InventoryOwnerRef.IsToken`), `IReadOnlyList<BodyPartId> BodyPartRefs` (reusing the existing `BodyPartId`, optional/may be empty, rejects invalid entries and duplicates), `UserId EquippedByUserId`, `UtcInstant EquippedAt`, `long Revision` (`>= 1`).
- `EquippedEntry.ToLocationRef()` — the checkable, type-level expression of rule 1: deterministically maps an `EquippedEntry` to the single `InventoryLocationRef.Equipped(InventoryId, EquipmentSlotRef)` value it corresponds to.
- Domain tests (`DotNet/Tests/Odyssey.Tests.Domain/Inventory/EquippedEntryTests.cs`, mirroring `InventoryRuntimeFoundationTests.cs`'s own style): construction/property tests, validation-rejection tests (each invalid field), duplicate-`BodyPartRefs` rejection, `ToLocationRef()` rule-1 consistency (matches only its own inventory/slot, differs otherwise), and instance-vs-stack `ItemRef` coverage.
- One existing scope-guard test updated (`InventoryRuntimeRecordTests.cs`) to admit exactly the one new file this task adds — no behavior change to that test's other assertions.
- `TC-INVENTORY-095`–`100` registered in `Tests/Metadata/test-catalog.json`.
- This task contract, its ExecPlan, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 status update.

### Out of scope

- Any persistence: SQL schema, tables, `IInventoryRepository`-shaped contracts, SQLite implementation (`ODY-S05-302`'s own job).
- Any command/service: `Equip`, `Unequip`, any MainGM-authorized service (`ODY-S05-303`/`304`'s own job).
- The real `RemoveBodyPart` dependency check (`ODY-S05-305`'s own job).
- The rule-4 "body parts currently exist on the owning Character" check as executable logic (`ODY-S05-303`'s own job) — this task only carries the `BodyPartId` vocabulary that check will later query against.
- Any change to `ArmorDefinition`, `WeaponDefinition`, `BodyPart`, or `CharacterAnatomy`.
- Any `docs/adr/**` edit.
- Unity/UI.
- Moving `ODY-S05-201`–`207`'s own task files from `active/` to `completed/` — observed still pending (pre-existing, unrelated documentation-sync debt); not this task's scope, not performed here.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Inventory/**
DotNet/Tests/Odyssey.Tests.Domain/Inventory/**
DotNet/Tests/Odyssey.Tests.Unit/Inventory/**
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S05-301_Equipment_Runtime_Foundation.md
docs/plans/active/ODY-S05-301_Equipment_Runtime_Foundation.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.persistence/**
Packages/com.odyssey.domain/Runtime/Character/**
Packages/com.odyssey.domain/Runtime/Content/**
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` stays serializer-free and Unity-free (`ADR-027` §14); `EquippedEntry` references only existing Domain types (`InventoryId`, `InventoryItemRef`, `BodyPartId` from `Odyssey.Domain.Character`, `UserId`/`UtcInstant`), all within the same `com.odyssey.domain` assembly — no new cross-assembly dependency.
- Authoritative-state and transaction boundary: not applicable — no command/service is implemented; this layer only defines data shape.
- Serialization / compatibility boundary: no DTO codec or persistence schema is introduced (`ADR-003`/`ADR-011`/`ADR-012`/`ADR-013` remain `ODY-S05-302`'s own concern).
- Time / RNG rule: `EquippedAt` accepts a caller-supplied `UtcInstant`; no global clock read (`ADR-008`), matching `InventoryRecord`'s own `CreatedAt`/`UpdatedAt` convention (unvalidated, caller-supplied).
- Unity / thread / lifetime rule: no Unity code.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: no private documentation, secrets, hidden campaign content, or personal data may be committed.
- Performance or platform constraint: Not applicable.
- Other: `EquippedEntry` must not decode typed catalog definitions or read `ArmorDefinition`/`WeaponDefinition` — it is pure runtime placement vocabulary.

## 7. Expected behavior

### Scenario 1 — `EquippedEntry` constructs and validates

**Given** a valid `InventoryId`, `InventoryItemRef`, canonical slot token, `BodyPartId` list, `UserId`, `UtcInstant`, and `Revision >= 1`
**When** an `EquippedEntry` is constructed
**Then** it exposes every field unchanged, and rejects each invalid input (invalid `InventoryId`/`ItemRef`, non-canonical `EquipmentSlotRef`, `null`/invalid/duplicate `BodyPartRefs`, invalid `EquippedByUserId`, non-positive `Revision`) with the same exception shapes the rest of this codebase already uses.

### Scenario 2 — optional body-part references

**Given** a slot that does not require body-part-specific placement
**When** an `EquippedEntry` is constructed with an empty `BodyPartRefs` list
**Then** construction succeeds — `ADR-027` §7 names body-part references as optional.

### Scenario 3 — rule 1 as a checkable invariant

**Given** an `EquippedEntry` and an item's own independently-constructed `InventoryLocationRef`
**When** `EquippedEntry.ToLocationRef()` is compared against that `InventoryLocationRef`
**Then** they are equal only when the location is `Equipped`, at the same `InventoryId`, in the same slot — any other inventory, slot, or location kind is unequal, giving future tasks a single-equality way to confirm an item and its equipped-entry agree on exactly one place.

### Required invariants

- Equipment remains Inventory-owned location state (`ADR-027` §5/§7), never Character-owned authoritative state and never a new aggregate root.
- `BodyPartRefs` reuses `BodyPartId` directly; no parallel/duplicate body-part type is introduced.
- No persistence, repository, command, or `RemoveBodyPart`/rule-4 executable logic is introduced.

## 8. Deliverables

- Production code: `EquippedEntry` (one new Domain type).
- Tests: `EquippedEntryTests.cs` (`TC-INVENTORY-095`–`100`); one existing scope-guard test updated.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1.
- Generated evidence or build artifacts: validation command output in §17.
- Migration / recovery material: None (no schema change).

## 9. Acceptance criteria

1. `EquippedEntry` exists in `Odyssey.Domain.Inventory`, carrying exactly `ADR-027` §7's field list (`InventoryId`, `ItemRef`, `EquipmentSlotRef`, `BodyPartRefs`, `EquippedByUserId`, `EquippedAt`, `Revision`).
2. `BodyPartRefs` reuses `BodyPartId` (`Odyssey.Domain.Character`) directly — no duplicate type.
3. Rule 1 ("one item or stack is in exactly one place") is expressed as a checkable, type-level invariant (`ToLocationRef()`), verified by a test asserting both agreement and disagreement cases.
4. Construction rejects: invalid `InventoryId`; invalid `ItemRef`; non-canonical/blank `EquipmentSlotRef`; `null` `BodyPartRefs`; any invalid `BodyPartId` within `BodyPartRefs`; duplicate `BodyPartId` values within `BodyPartRefs`; invalid `EquippedByUserId`; `Revision < 1`.
5. An empty `BodyPartRefs` list is accepted (optional per `ADR-027` §7).
6. No persistence, repository interface, command service, `RemoveBodyPart` real check, or rule-4 executable check is introduced.
7. `Tests/Metadata/test-catalog.json` contains `TC-INVENTORY-095`–`100`.
8. Task contract and ExecPlan for `ODY-S05-301` are added.
9. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 is updated to `In Review` with the PR link after the Draft PR is opened.
10. `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all pass with real recorded output.
11. `git diff --name-status` against `main` shows only §5's allowed paths.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-095` | .NET / NUnit (Domain) | `EquippedEntry` constructs with valid fields and exposes them unchanged | Pass |
| `TC-INVENTORY-096` | .NET / NUnit (Domain) | Empty `BodyPartRefs` is accepted (optional per `ADR-027` §7) | Pass |
| `TC-INVENTORY-097` | .NET / NUnit (Domain) | Construction rejects every invalid field | Pass |
| `TC-INVENTORY-098` | .NET / NUnit (Domain) | Construction rejects duplicate `BodyPartRefs` | Pass |
| `TC-INVENTORY-099` | .NET / NUnit (Domain) | `ToLocationRef()` is rule 1's checkable form — agrees only for the same inventory/slot/location kind | Pass |
| `TC-INVENTORY-100` | .NET / NUnit (Domain) | `ItemRef` supports either an `ItemInstanceId` or an `ItemStackId` reference | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no persistence/schema file, Unity file, accepted ADR file, `Character/`/`Content/` file, or command-service file changed.

### Required environments / profiles

- OS / architecture: Windows x64 development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation — no Unity files change.
- SQLite recovery/migration rehearsal — no persistence schema or repository implementation is added.

## 11. Compatibility, migration, and rollback

- Compatibility impact: new Domain contract type only; no persisted format exists yet.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: future persistence task (`ODY-S05-302`) must define schema/contracts separately.
- Rollback method: revert this branch/PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public runtime contract vocabulary and synthetic tests only.
- Trust boundaries: no runtime trust boundary is implemented.
- Authorization / audience checks: no command or permission behavior is implemented.
- Redaction requirements: no hidden fields or private data are added.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: constructor rejects malformed/blank tokens, invalid IDs, and duplicate body-part references.
- Security tests: scope-guard test confirms no persistence/command/RemoveBodyPart behavior enters this task.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1 requires it explicitly, and this task introduces a new public Domain contract type for the first time in this block — the same trigger that required an ExecPlan for `ODY-S05-201`.
- ExecPlan path: `docs/plans/active/ODY-S05-301_Equipment_Runtime_Foundation.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #122 (`ODY-S05-108`) and the fully-merged Inventory runtime block (`ODY-S05-201`–`207`); precedes `ODY-S05-302`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 1.
- Documents that must not change: accepted ADRs; `ODY-S05-108` and earlier task contracts' own content.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None — new in-process Domain contract only.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

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

- `Packages/com.odyssey.domain/Runtime/Inventory/EquippedEntry.cs` (new)
- `DotNet/Tests/Odyssey.Tests.Domain/Inventory/EquippedEntryTests.cs` (new, TC-INVENTORY-095..100)
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` (scope-guard update, one assertion)
- `Tests/Metadata/test-catalog.json` (6 new entries)
- `docs/tasks/active/ODY-S05-301_Equipment_Runtime_Foundation.md`, `docs/plans/active/ODY-S05-301_Equipment_Runtime_Foundation.md` (new)
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (§12 row 1 status only)

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | 719 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 433) |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` (all `REPO-POLICY-*`/`TC-CI-*` sub-checks PASS) |
| `.\scripts\verify-test-structure.ps1` | PASS | exit code 0; all `TC-ARCH-*` sub-checks PASS |
| CI (`gh pr checks 123`) | PASS | `dotnet-restore-build-test`, `repository-policy-format-structure`, `unity-project-package-static`, `buildidentity-provenance` all pass |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1..11 | Met | `EquippedEntry` carries exactly `ADR-027` §7's field list; `BodyPartRefs` reuses `BodyPartId`; rule 1 is checkable via `ToLocationRef()`; invalid fields rejected (`TC-INVENTORY-097`); empty `BodyPartRefs` accepted (`TC-INVENTORY-096`); duplicate `BodyPartRefs` rejected (`TC-INVENTORY-098`); no persistence/command/`RemoveBodyPart` behavior added (scope-guard test, diff review); metadata and docs updated; all required validation commands pass. |

### Build and artifact evidence

- `dotnet build`/`dotnet test` output recorded above; no build artifacts published (library/test project only).

### Known limitations

- No persistence, repository interface, command service, or behavior beyond foundation value validation is implemented by design.
- Rule 4 (body parts must currently exist on the owning Character) and rule 5 (`RemoveBodyPart` dependency closure) are not enforced by this task — `ODY-S05-303`/`305`'s own jobs.
- `ODY-S05-201`–`207`'s own task/plan files remain in `docs/tasks/active/`/`docs/plans/active/` despite `Done` backlog status — a pre-existing, unrelated documentation-sync gap, observed but not fixed here (out of this task's scope per this ТЗ).

### Follow-up tasks

- `ODY-S05-302` — Equipment Persistence Foundation.

### Self-review summary

- Scope review: diff limited to `Packages/com.odyssey.domain/Runtime/Inventory/**`, the two test files, test metadata, and planning docs; no persistence/schema/Unity/ADR/Character/Content file changed.
- Architecture review: `ADR-027` §5/§7/§14 preserved; Equipment stays Inventory-owned location vocabulary; `BodyPartId` reused, not duplicated.
- Test review: 6 new `TC-INVENTORY-*` checks added; one existing scope-guard test updated to admit exactly the one new file.
- Security/privacy review: no private material, hidden campaign data, secrets, or user data added.
- Documentation/version review: test metadata and planning docs updated; no application/schema/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — **Design decision (required by this ТЗ §3): `EquippedEntry` is a standalone Domain type, not a typed extension of `InventoryLocationRef.Equipped`.** `InventoryLocationRef` is shared by `Contained`/`SceneDropped`/`Other`, where `BodyPartRefs`/`EquippedByUserId`/`EquippedAt`/`Revision` would be meaningless unused fields; embedding them there would pollute every non-equipped location. `InventoryLocationRef.Equipped(...)` itself is left completely unchanged (zero blast radius on `SqliteInventoryRepository`/`InventoryMovementService`/`InventoryStackOperationService`, all of which already construct/read it). Authority: `ADR-027` §7's own framing of `EquippedEntry` as a distinct record, plus direct inspection of `InventoryLocationRef`'s existing four-kind shared shape.
- 2026-09-11 — **Design decision: `EquippedEntry` lives entirely in `Odyssey.Domain.Inventory` as one sealed class, not split into a Domain value type plus an Application-layer `EquippedEntryRecord`.** `ADR-027` §14 explicitly assigns "equipment placement" and "one-place item rules" to `Odyssey.Domain` ownership (not merely Application), and `CharacterAnatomy` (`Odyssey.Domain.Character`) is the direct, already-accepted precedent for a Domain-owned `sealed class` carrying an `IReadOnlyList<T>` field and a `Revision` guard. Unlike `InventoryRecord`/`ItemStackRecord` (which exist mainly to add a repository-facing `CampaignId` `ADR-027`'s own minimum `EquippedEntry` record does not name), there is no persistence-facing field this task needs to add — so no Application-layer companion record is created here; `ODY-S05-302` decides whether persistence needs one, exactly how `ItemStackRecord` added `CampaignId` over its Domain-value equivalents. Authority: `ADR-027` §7/§14, direct code inspection.
- 2026-09-11 — Decision: rule 1 is expressed via `EquippedEntry.ToLocationRef()` (a deterministic derived value), not a separate throwing guard method. This reuses `InventoryLocationRef.Equals` (already implemented) rather than inventing a new guard class, and is directly testable by construction and comparison alone. Authority: this session's own design choice, consistent with minimizing new surface area.
- 2026-09-11 — Decision: `BodyPartRefs` rejects duplicate `BodyPartId` values. Rationale: two references to the same body part on one equipped entry cannot mean anything real and most likely signals a caller defect — the same "reject nonsensical duplicate structural data" instinct `ADR-027`/this codebase already applies elsewhere. Authority: this ТЗ §3's own explicit request to decide and document duplicate handling.
- 2026-09-11 — Decision: continue the existing `TC-INVENTORY-*` series (`095`–`100`) rather than starting a new `TC-EQUIPMENT-*` prefix. Rationale: `ADR-027` §5 frames Equipment as Inventory-owned state, not a separate aggregate, and every prior Inventory-runtime-block task (`201`–`207`, spanning creation/movement/stack-ops/deletion-checks/integration) already shares one `TC-INVENTORY-*` series regardless of sub-concern. Authority: existing numbering precedent.
- 2026-09-11 — Decision: `EquippedEntry` lives in its own new file (`EquippedEntry.cs`), not appended to `InventoryRuntime.cs`. Rationale: matches how every later Inventory task (`204`, `205`, `206`) added its own new file rather than growing the foundation file; keeps this task's diff fully isolated and easy to review/revert. Authority: existing file-organization precedent.
- 2026-09-11 — Updated `InventoryRuntimeRecordTests.cs`'s existing scope-guard assertion (previously "no file containing 'Equipment' may exist") to admit exactly `EquippedEntry.cs` (also checked for the substring "Equipped", which the prior assertion did not need to consider). No other assertion in that test changed. Authority: same rolling-scope-guard convention already used for `ODY-S05-204`'s Move service and `ODY-S05-205`'s Stack files in the same test method.

### Approved task changes

- None.
