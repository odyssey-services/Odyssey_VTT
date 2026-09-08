# ODY-S05-203 — Create Item/Stack From Published Definition

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Inventory runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-203-create-item-stack-from-published-definition`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/115
**ExecPlan:** `docs/plans/active/ODY-S05-203_Create_Item_Stack_From_Published_Definition.md`
**Created:** 2026-09-06
**Last updated:** 2026-09-07 UTC

## 1. Goal

Add the first Application-level Inventory creation flow: MainGM-only requests create a runtime `ItemInstanceRecord` or `ItemStackRecord` from an already Published catalog definition by validating the definition, pinning its exact version, copying its mechanics snapshot, and delegating persistence to `IInventoryRepository`.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-201`/`202` introduced runtime records and persistence primitives, but no Application flow turns Published catalog definitions into runtime Inventory state.
- Value or risk reduction: future Inventory movement/stack/equipment work can rely on item/stack rows whose source definition version and mechanics snapshot are fixed at creation time.
- Blocking or enabling relationship: unblocks `ODY-S05-204` movement/transfer and `ODY-S05-205` split/merge; create-from-Published semantics remain separate from later gameplay.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, row `ODY-S05-203`.
- `docs/tasks/active/ODY-S05-201_Inventory_Runtime_Foundation.md`.
- `docs/tasks/active/ODY-S05-202_Inventory_Persistence_Foundation.md`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 4, 5, 6, 12, and 14.
- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`.
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryRuntimeRecords.cs`.
- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs`.
- `Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs`.
- `Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs`.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs`.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogLifecycleContracts.cs`.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-203`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`026` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-027`-`042`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-203` task brief only.

## 4. Verified current state

### Verified facts

- `git fetch origin --prune` completed.
- `origin/main` contains merge commit `8cd0bcf`, PR #114 (`ODY-S05-202`).
- Branch `feat/ody-s05-203-create-item-stack-from-published-definition` was created from `origin/main`.
- `ODY-S05-201` provides Inventory IDs, owner/location refs, mechanics snapshots, quantities, and immutable Application read records.
- `ODY-S05-202` provides `IInventoryRepository`, `SqliteInventoryRepository`, parent Inventory existence checks, and idempotent create primitives.
- Existing Content Catalog services already use thin Application services over repositories, MainGM booleans supplied by callers, server-side validation, and `Result<T>` failures.
- `ItemDefinition` has `IsStackable`/`MaxStackSize`; `AmmoDefinition` embeds an `ItemDefinition`.

### Assumptions

- None.

## 5. Scope

### In scope

- Application request contracts for `CreateItemInstanceFromDefinition` and `CreateItemStackFromDefinition`, including stable runtime target IDs for repository replay.
- Application service that checks MainGM, loads a catalog definition, rejects non-Published/missing/wrong-type/invalid definitions, copies the snapshot, builds runtime records, and calls `IInventoryRepository`.
- Minimal Inventory creation error codes and registry docs.
- Persistence/application tests using real SQLite content catalog and inventory repositories.
- Test metadata, task contract, ExecPlan, and backlog status updates.

### Out of scope

- New SQLite tables or repository schema changes.
- New repository methods.
- Authorization model beyond the existing caller-supplied `ActorIsMainGm` pattern.
- Create-from-Draft or create-from-Archived semantics.
- Move/transfer.
- Split/merge.
- Equip/unequip.
- Item use.
- ActiveEffect creation/execution.
- Attack pipeline.
- ItemDefinition migration.
- Unity/UI.
- `.odcontent`.
- Balanced content.
- Accepted ADR edits.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Inventory/**
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
DotNet/Tests/Odyssey.Tests.Persistence/**
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-203_Create_Item_Stack_From_Published_Definition.md
docs/plans/active/ODY-S05-203_Create_Item_Stack_From_Published_Definition.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.persistence/Runtime/Sqlite/**
Assets/**
Any move/split/merge/equipment/attack/ActiveEffect/ItemDefinition migration implementation path
```

## 6. Technical constraints

- Module ownership and dependency direction: Application owns this service; it may depend on Domain, Content contracts, and Application persistence ports, but not on SQLite (`ADR-001`, `ADR-027` section 14).
- Authoritative-state and transaction boundary: this service is the MainGM-authoritative Application entry point; `CommandId` idempotency remains in `IInventoryRepository` (`ADR-002`, `ODY-S05-202`).
- Serialization / compatibility boundary: mechanics snapshot copies the Published definition's raw `PropertiesJson`; no decode/re-encode or direct aggregate serialization (`ADR-003`, `ADR-027` section 6).
- Time / RNG rule: runtime IDs and timestamps use injected `IWallClock`; no direct wall-clock or randomness.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no dependencies.
- Security / privacy / redaction rule: failures use registered public-safe error codes and message keys; no raw validation issue data is embedded in `Error` details.
- Other: `ODY-S05-203` does not inspect runtime inventory beyond delegating to `IInventoryRepository`; movement and split/merge remain `ODY-S05-204`/`205`.

## 7. Expected behavior

### Scenario 1 — create unique runtime item

**Given** a MainGM requests an item instance from a Published `Item`, `Weapon`, or `Armor` definition  
**When** catalog validation passes  
**Then** the service creates an `ItemInstanceRecord` with exact `ContentDefinitionRef`, copied mechanics snapshot, caller owner/location refs, revision 1, and opaque empty runtime state.

### Scenario 2 — create runtime stack

**Given** a MainGM requests a stack from a Published `Ammo` definition or a Published stackable `Item` definition  
**When** catalog validation passes and quantity is legal for the typed definition  
**Then** the service creates an `ItemStackRecord` with exact `ContentDefinitionRef`, copied mechanics snapshot, caller owner/location refs, requested quantity, revision 1, and opaque empty stack state.

### Scenario 3 — reject unsafe creation

**Given** a request is non-MainGM, missing, Draft, Archived, wrong-type, validation-incompatible, or fails repository idempotency/parent checks  
**When** the service handles it  
**Then** it returns a `Result.Failure` and does not create a new runtime row.

### Required invariants

- Runtime records pin exact Published definition version; no floating latest reference exists.
- Snapshot payload is copied from the Published definition's `PropertiesJson`.
- Service re-runs `CatalogValidationService.ValidateContentDefinition` server-side before persistence.
- Repository idempotency handles replay; this task adds no new idempotency table.
- No move/split/merge/equipment/attack/ActiveEffect/migration implementation is introduced.

## 8. Deliverables

- Production code: `InventoryCreationService` and request/error contracts.
- Tests: persistence/application tests for creation, rejection, snapshot copy, idempotency, and scope guards.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog update, test metadata, error registry.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none.

## 9. Acceptance criteria

1. `InventoryCreationService.CreateItemInstanceFromDefinition` and `CreateItemStackFromDefinition` exist in Application and depend only on `IContentCatalogRepository`, `IInventoryRepository`, and `IWallClock`.
2. Requests require `CampaignHandle`, stable `ItemInstanceId` or `ItemStackId`, `InventoryId`, `InventoryOwnerRef`, `InventoryLocationRef`, `ContentDefinitionId`, actor user id, `ActorIsMainGm`, `CommandId`, `CorrelationId`, and stack quantity where applicable.
3. Non-MainGM requests are rejected before inventory mutation.
4. Missing definitions are rejected through existing repository not-found failure.
5. Draft and Archived definitions are rejected before inventory mutation.
6. Item instance creation accepts Published `Item`, `Weapon`, and `Armor`; stack creation accepts Published `Ammo` and stackable `Item` only.
7. Ability, Effect, Resource, BodyPart, Skill, Attribute, Perk, Action, Mechanic, and NpcTemplateData definitions are rejected for runtime item/stack creation.
8. `CatalogValidationService.ValidateContentDefinition` is run server-side and validation failures prevent inventory mutation.
9. Runtime records pin `ContentDefinitionRef(definitionId, published.Version)`, set snapshot version to `published.Version`, copy definition type and raw `PropertiesJson`, and do not store a latest reference.
10. `CreateItemStackFromDefinition` enforces stackability and `MaxStackSize` when the typed definition declares a maximum.
11. Inventory repository parent existence and idempotency remain delegated to `IInventoryRepository`.
12. Tests and metadata cover `TC-INVENTORY-027` onward.
13. Required validation commands pass and diff review confirms no Unity files, ADR edits, persistence schema changes, movement/split/merge/equipment/attack/ActiveEffect/migration implementation.
14. A successful creation replay is resolved by the inventory ledger before current definition lifecycle validation, so archiving the source definition does not invalidate that replay.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-027` | .NET / NUnit (Persistence/Application) | MainGM creates `ItemInstance` from Published `Item` | Pass |
| `TC-INVENTORY-028` | .NET / NUnit (Persistence/Application) | MainGM creates `ItemInstance` from Published `Weapon` | Pass |
| `TC-INVENTORY-029` | .NET / NUnit (Persistence/Application) | MainGM creates `ItemStack` from Published `Ammo` | Pass |
| `TC-INVENTORY-030` | .NET / NUnit (Persistence/Application) | Created runtime record pins exact `ContentDefinitionRef` | Pass |
| `TC-INVENTORY-031` | .NET / NUnit (Persistence/Application) | Created runtime snapshot copies `PropertiesJson` payload | Pass |
| `TC-INVENTORY-032` | .NET / NUnit (Persistence/Application) | Draft definition rejected without item/stack row | Pass |
| `TC-INVENTORY-033` | .NET / NUnit (Persistence/Application) | Archived definition rejected without item/stack row | Pass |
| `TC-INVENTORY-034` | .NET / NUnit (Persistence/Application) | Missing definition rejected | Pass |
| `TC-INVENTORY-035` | .NET / NUnit (Persistence/Application) | Wrong definition type rejected for item instance and stack | Pass |
| `TC-INVENTORY-036` | .NET / NUnit (Persistence/Application) | Validation failure rejected without runtime row | Pass |
| `TC-INVENTORY-037` | .NET / NUnit (Persistence/Application) | Non-MainGM rejected before mutation | Pass |
| `TC-INVENTORY-038` | .NET / NUnit (Persistence/Application) | Replay with same `CommandId` returns same stored item/stack without duplicate | Pass |
| `TC-INVENTORY-039` | .NET / NUnit (Persistence/Application) | Same `CommandId` reused for different target is rejected by inventory repository ledger | Pass |
| `TC-INVENTORY-040` | .NET / NUnit (Persistence/Application) | Scope guard confirms no move/split/merge/equipment/attack/ActiveEffect/migration implementation | Pass |
| `TC-INVENTORY-041` | .NET / NUnit (Persistence/Application) | Non-stackable Item cannot create ItemStack | Pass |
| `TC-INVENTORY-042` | .NET / NUnit (Persistence/Application) | ItemStack quantity cannot exceed typed Item max stack size | Pass |
| `TC-INVENTORY-043` | .NET / NUnit (Persistence/Application) | ItemInstance replay succeeds after source definition archive | Pass |
| `TC-INVENTORY-044` | .NET / NUnit (Persistence/Application) | ItemStack replay succeeds after source definition archive | Pass |
| `TC-INVENTORY-045` | .NET / NUnit (Persistence/Application) | Replay probe rejects a reused command for another target after archive | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no Unity files, ADR edits, new persistence schema, movement/split/merge/equipment/attack/ActiveEffect/migration implementation.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with real SQLite content catalog and inventory repositories.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- SQLite migration rehearsal because no new persistence schema is added.
- Attack/equipment/ActiveEffect behavioral tests because those flows remain out of scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: adds Application service/request/error contracts only; no new persisted table or format.
- Version fields affected: none.
- Migration or upcaster: none.
- Forward / backward behavior: future commands can call this service; older builds ignore it.
- Rollback method: revert this branch/PR before merge.
- Data-loss risk and protection: creation writes are delegated to existing transactional repository primitives.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog definitions, inventory records, and opaque mechanics/runtime JSON in tests.
- Trust boundaries: service is an authoritative Application boundary; it rejects non-MainGM before persistence.
- Authorization / audience checks: caller-supplied `ActorIsMainGm` matching existing catalog service pattern.
- Redaction requirements: errors use registered public-safe message keys; no raw SQL, exception text, private docs, or hidden data.
- Log-safe fields: no logging added.
- Abuse / malformed input limits: request constructors validate IDs/refs; existing typed codecs and catalog validation handle malformed definition payloads.
- Security tests: non-MainGM denial and scope guards.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: task adds an Application service/request contract and authoritative mutation flow.
- ExecPlan path: `docs/plans/active/ODY-S05-203_Create_Item_Stack_From_Published_Definition.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: follows merged PR #114 and precedes `ODY-S05-204`/`ODY-S05-205`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/errors/ERROR_CODES.md`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds Application request/service contracts only.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Inventory/InventoryCreationService.cs` — MainGM-only Application creation service, request contracts, failures, and pre-lifecycle replay path.
- `Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs` — read-only ledger-confirmed create replay probes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` — SQLite implementation of the read-only replay probes.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — Inventory creation service error codes.
- `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs` — `TC-INVENTORY-027`-`045` Application/Persistence tests.
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` — stale `ODY-S05-201` guard narrowed to allow the authorized ODY-S05-203 creation service while still blocking movement/equipment/attack/ActiveEffect/migration scope.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-027`-`045` entries.
- `docs/errors/ERROR_CODES.md` — required registry entries for Inventory creation service failures.
- This task contract, ExecPlan, and `SLICE-05_IMPLEMENTATION_BACKLOG.md`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Passed | Full suite passed: Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 386. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `InventoryCreationService` in Application; no SQLite dependency. |
| AC-2 | Passed | Request contracts include campaign, stable runtime target id, inventory, owner/location, definition, actor, MainGM flag, command/correlation ids, and stack quantity. |
| AC-3 | Passed | `TC-INVENTORY-037`. |
| AC-4 | Passed | `TC-INVENTORY-034`. |
| AC-5 | Passed | `TC-INVENTORY-032`-`033`. |
| AC-6 | Passed | `TC-INVENTORY-027`-`029`, plus non-stackable Item rejection. |
| AC-7 | Passed | `TC-INVENTORY-035`. |
| AC-8 | Passed | `TC-INVENTORY-036`. |
| AC-9 | Passed | `TC-INVENTORY-030`-`031`. |
| AC-10 | Passed | Stackability and max stack size tests in `InventoryCreationServiceTests`. |
| AC-11 | Passed | `TC-INVENTORY-038`-`039`; parent existence remains in `IInventoryRepository`. |
| AC-12 | Passed | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-027`-`042`. |
| AC-13 | Passed | Required commands passed; diff review confirms no Unity files, ADR edits, persistence schema changes, movement/split/merge/equipment/attack/ActiveEffect/migration implementation. |

### Known limitations

- `ODY-S05-204` owns movement/transfer.
- `ODY-S05-205` owns split/merge.
- This task does not create ActiveEffects, execute item abilities/effects, equip items, run attacks, or migrate ItemDefinitions.

### Self-review summary

- Scope review: diff is limited to Application Inventory creation contracts/service, tests, metadata, error registry, task/plan docs, and backlog status; no Unity files or ADR edits.
- Architecture review: Application owns the service; Persistence remains behind repository ports; no new schema or dependency is added.
- Test review: `TC-INVENTORY-027`-`045` added and full `dotnet test` passed.
- Security/privacy review: MainGM denial is checked before inventory mutation; no private material, hidden campaign data, raw exception text, logging, diagnostics, or network projections added.
- Documentation/version review: metadata, error registry, task/plan/backlog updated; no application/schema/protocol/ruleset version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-06 — Decision: use a single Application service over existing catalog and inventory repository ports, with no new repository method or persistence schema. Authority / approval: `ODY-S05-203` task scope and `ADR-001`.
- 2026-09-06 — Decision: use `ItemDefinition.IsStackable`/`MaxStackSize` for stackable `Item` creation, while `Ammo` stack creation still validates the embedded item stackability constraints. Authority / approval: task definition type rules and existing typed model.
- 2026-09-06 — Decision: include stable `ItemInstanceId`/`ItemStackId` in creation requests so a replay with the same `CommandId` reaches the existing repository ledger with the same target id. Authority / approval: `ODY-S05-203` idempotency acceptance and `ODY-S05-202` repository contract.
- 2026-09-07 — Amendment: probe the existing `InventoryCommandLedger` after MainGM validation and before catalog status/type validation. Rationale: a successful command must replay its stored runtime snapshot even after its source definition is archived; target identity mismatch remains a ledger failure. Authority / approval: owner amendment to PR #115 and `ADR-002`.

### Approved task changes

- None.
