# ODY-S05-201 — Inventory Runtime Foundation

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Inventory runtime block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-201-inventory-runtime-foundation`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/113
**ExecPlan:** `docs/plans/active/ODY-S05-201_Inventory_Runtime_Foundation.md`
**Created:** 2026-09-06
**Last updated:** 2026-09-06 UTC

## 1. Goal

Add the minimal Domain and Application contracts needed by the next Inventory runtime tasks: Inventory, item-instance, and item-stack identities; owner/location vocabulary; item/stack references; mechanics snapshot container; positive stack quantity; and read-record shapes. This task intentionally does not implement persistence, repositories, command services, item creation, movement, split/merge, equipment behavior, ActiveEffect behavior, attack pipeline, migration workflow, or UI.

## 2. Why this task exists

- Problem or dependency being addressed: after Content Catalog MVP closure, Inventory runtime needs stable vocabulary before SQLite persistence (`ODY-S05-202`) can store it.
- Value or risk reduction: gives future tasks a small, tested contract surface without deciding command behavior or schema in the foundation task.
- Blocking or enabling relationship: unblocks `ODY-S05-202` Inventory Persistence Foundation and later `ODY-S05-203`-`207` tasks.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, section 7 / `ODY-S05-201`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 5, 6, 7, 9, 14, and 20.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md`, sections 17.1-17.3.
- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs`.
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` and nearby Application contract patterns.
- Local canonical ID, immutable value object, test metadata, task contract, and ExecPlan patterns.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-201`, `SLICE-05`.
- Existing test IDs: `TC-CATALOG-001`-`111` as predecessor evidence only.
- New test IDs to introduce: `TC-INVENTORY-001` onward.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-201` task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin --prune` completed.
- `origin/main` contains merge commit `3cd44ea`, PR #112 (`ODY-S05-107`), merged 2026-09-06.
- Branch `feat/ody-s05-201-inventory-runtime-foundation` was created from `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row `ODY-S05-201` is `Proposed` and scopes this task to Domain/Application contracts only.
- `ContentDefinitionId` in `ContentCatalog.cs` uses the repository's canonical `Prefix + Uuid7.NewHex32` ID pattern.
- Application read records are currently sealed immutable classes with constructor validation.

### Assumptions

- None.

## 5. Scope

### In scope

- Domain Inventory namespace/folder with minimal IDs and value objects:
  `InventoryId`, `ItemInstanceId`, `ItemStackId`, `InventoryOwnerRef`, `InventoryLocationRef`, `InventoryItemRef`, `ItemMechanicsSnapshot`, and `ItemStackQuantity`.
- Application Inventory contracts:
  `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord`.
- Unit tests for the new foundation behavior and guard rails.
- Test metadata entries.
- Task contract, ExecPlan, and backlog status update.

### Out of scope

- SQLite schema, tables, migrations, or repository implementations.
- Repository interfaces unless proven absolutely necessary.
- Idempotency ledger.
- Command service or MainGM authorization.
- Create item/stack from Published definition.
- Catalog status validation for runtime creation.
- Moving/transferring items.
- Split/merge commands.
- Equip/unequip behavior.
- Attack pipeline, item use, ActiveEffect creation/execution, ItemDefinition migration workflow.
- Unity/UI.
- `.odcontent` import/export.
- Balanced content.
- Accepted ADR changes.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Inventory/**
Packages/com.odyssey.application/Runtime/Inventory/**
DotNet/Tests/Odyssey.Tests.Domain/Inventory/**
DotNet/Tests/Odyssey.Tests.Unit/Inventory/**
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-201_Inventory_Runtime_Foundation.md
docs/plans/active/ODY-S05-201_Inventory_Runtime_Foundation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.persistence/**
Assets/**
Any SQLite schema/migration file
Any Equipment/Attack/ActiveEffect/ItemDefinition migration implementation file
```

## 6. Technical constraints

- Module ownership and dependency direction: Domain owns pure identity/value invariants; Application may reference Domain and carries read-record contracts only (`ADR-001`, `ADR-027` section 14).
- Authoritative-state and transaction boundary: no commands are implemented; future commands must follow `ADR-002`.
- Serialization / compatibility boundary: no DTO codec or persistence schema is added; future persistence must follow `ADR-003`, `ADR-011`, `ADR-012`, and `ADR-013`.
- Time / RNG rule: new IDs accept caller-supplied `UtcInstant`; no global clock is read (`ADR-008`).
- Unity / thread / lifetime rule: no Unity code.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: no private documentation, secrets, hidden campaign content, or personal data may be committed.
- Performance or platform constraint: not applicable.
- Other: `ItemMechanicsSnapshot` stores opaque copied mechanics payload and exact `ContentDefinitionRef`; it must not decode typed definitions or refer to latest catalog state.

## 7. Expected behavior

### Scenario 1 — canonical runtime IDs

**Given** a caller supplies a valid `UtcInstant`  
**When** `InventoryId`, `ItemInstanceId`, or `ItemStackId` is minted  
**Then** each ID round-trips through `ToString`/`Parse`, rejects wrong prefixes, and rejects empty input.

### Scenario 2 — owner and location vocabulary

**Given** a Character or Scene context  
**When** an `InventoryOwnerRef` or `InventoryLocationRef` is created  
**Then** the value records a valid owner/location without making Inventory a Character section and without implementing equip/unequip behavior.

### Scenario 3 — runtime item references and snapshots

**Given** a runtime item or stack must refer to existing catalog mechanics  
**When** an `InventoryItemRef`, `ItemMechanicsSnapshot`, or `ItemStackQuantity` is created  
**Then** missing/ambiguous item refs are rejected, snapshots require exact `ContentDefinitionRef` plus positive snapshot version and opaque payload, and live stack quantity must be positive.

### Required invariants

- Inventory is a separate aggregate vocabulary, not a Character section.
- Runtime items pin exact definition versions and copied snapshots, never "latest."
- Equipment is representable only as a location kind in this task; equip/unequip behavior is not implemented.
- No persistence/repository/schema/command service is introduced.

## 8. Deliverables

- Production code: minimal Domain/Application contract files only.
- Tests: Domain/Application foundation tests with `TC-INVENTORY-*` IDs.
- Scripts / CI: None.
- Configuration: None.
- Documentation: task contract, ExecPlan, backlog status update, test metadata.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `InventoryId`, `ItemInstanceId`, and `ItemStackId` exist with canonical minted ID behavior and tests.
2. `InventoryOwnerRef` supports Character owner and Scene/location style owner refs, rejecting blank/invalid targets.
3. `InventoryLocationRef` supports contained, equipped, scene/dropped, and other explicit location vocabulary while implementing no equip behavior.
4. `InventoryItemRef` references either an `ItemInstanceId` or an `ItemStackId` and rejects missing/ambiguous refs.
5. `ItemMechanicsSnapshot` requires a valid exact `ContentDefinitionRef`, positive `DefinitionSnapshotVersion`, valid content type, and immutable opaque payload; no latest-definition concept exists.
6. `ItemStackQuantity` accepts positive quantities and rejects zero/negative quantities for live stacks.
7. Application records exist for `InventoryRecord`, `ItemInstanceRecord`, and `ItemStackRecord` and carry IDs, `CampaignId`, owner/location refs, source definition refs, mechanics snapshots, revision, and timestamps.
8. `ItemInstanceRecord` and `ItemStackRecord` reject `Contained` or `Equipped` locations whose `TargetRef` points at a different `InventoryId`, preserving the one-place/location invariant at the foundation record boundary.
9. Reflection/search guards prove this task does not introduce Inventory repository implementation, SQLite persistence/schema, Character-section ownership, Equipment command behavior, Attack behavior, ActiveEffect implementation, or ItemDefinition migration workflow.
10. `Tests/Metadata/test-catalog.json` contains new `TC-INVENTORY-*` entries for the tests added by this task.
11. Task contract and ExecPlan for `ODY-S05-201` are added.
12. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-201` `In Review` with the PR link after the Draft PR is opened.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-001` | .NET / NUnit (Domain) | Runtime IDs mint, parse, and reject invalid prefixes/input | Pass |
| `TC-INVENTORY-002` | .NET / NUnit (Domain) | `InventoryOwnerRef` supports valid Character and Scene/location owner refs and rejects invalid targets | Pass |
| `TC-INVENTORY-003` | .NET / NUnit (Domain) | `InventoryLocationRef` models contained/equipped/scene/other vocabulary and rejects invalid values without equip behavior | Pass |
| `TC-INVENTORY-004` | .NET / NUnit (Domain) | `InventoryItemRef` supports instance or stack refs and rejects missing/ambiguous refs | Pass |
| `TC-INVENTORY-005` | .NET / NUnit (Domain) | `ItemMechanicsSnapshot` requires exact definition ref, positive snapshot version, valid type, and opaque immutable payload | Pass |
| `TC-INVENTORY-006` | .NET / NUnit (Domain) | `ItemStackQuantity` accepts positive quantities and rejects zero/negative quantities | Pass |
| `TC-INVENTORY-007` | .NET / NUnit (Unit/Application) | Inventory Application records validate required IDs, refs, revisions, and timestamps | Pass |
| `TC-INVENTORY-008` | .NET / NUnit (Unit/Reflection) | No repository, SQLite, Character-section, Equipment command, Attack, ActiveEffect, or ItemDefinition migration implementation is introduced | Pass |
| `TC-INVENTORY-009` | .NET / NUnit (Unit/Application) | `ItemInstanceRecord`/`ItemStackRecord` reject contained/equipped locations pointing at another InventoryId | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no SQLite persistence/schema files, Unity files, accepted ADR files, equipment/attack/ActiveEffect implementation, or ItemDefinition migration workflow changed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: not applicable.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- SQLite recovery/migration rehearsal because no persistence schema or repository implementation is added.

## 11. Compatibility, migration, and rollback

- Compatibility impact: new Domain/Application contract types only; no persisted format exists yet.
- Version fields affected: none.
- Migration or upcaster: none.
- Forward / backward behavior: future persistence task must define schema/contracts separately.
- Rollback method: revert this branch/PR.
- Data-loss risk and protection: none.
- Recovery rehearsal required: no.

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
- Log-safe fields: not applicable.
- Abuse / malformed input limits: constructors/parsers reject malformed identifiers and blank refs.
- Security tests: guard test ensures no command/persistence behavior enters this task.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: this task introduces public Domain/Application contracts and is the first code task in a new runtime block.
- ExecPlan path: `docs/plans/active/ODY-S05-201_Inventory_Runtime_Foundation.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #112 and precede `ODY-S05-202` persistence.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: no schema/protocol/ruleset change; new in-process Domain/Application contracts only.
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

- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs` — minimal Inventory runtime IDs and value objects.
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryRuntimeRecords.cs` — minimal immutable Application read-record contracts.
- `DotNet/Tests/Odyssey.Tests.Domain/Inventory/InventoryRuntimeFoundationTests.cs` — Domain tests for `TC-INVENTORY-001`-`006`.
- `DotNet/Tests/Odyssey.Tests.Unit/Inventory/InventoryRuntimeRecordTests.cs` — Application record and scope-guard tests for `TC-INVENTORY-007`-`009`.
- `Tests/Metadata/test-catalog.json` — `TC-INVENTORY-001`-`009` entries.
- This task contract and ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | First sandboxed run failed before compilation due denied access to `C:\Users\alexx\AppData\Local\Microsoft SDKs`; escalated rerun passed with 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite passed: Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 353. |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | `Repository policy check passed`; `REPO-POLICY-*` and `TC-CI-*` checks passed. |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid dependency/version/duplicate-ID cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `InventoryRuntime.cs`; `TC-INVENTORY-001`. |
| AC-2 | Pass | `InventoryOwnerRef`; `TC-INVENTORY-002`. |
| AC-3 | Pass | `InventoryLocationRef`; `TC-INVENTORY-003`. |
| AC-4 | Pass | `InventoryItemRef`; `TC-INVENTORY-004`. |
| AC-5 | Pass | `ItemMechanicsSnapshot`; `TC-INVENTORY-005`. |
| AC-6 | Pass | `ItemStackQuantity`; `TC-INVENTORY-006`. |
| AC-7 | Pass | `InventoryRecord`, `ItemInstanceRecord`, `ItemStackRecord`; `TC-INVENTORY-007`. |
| AC-8 | Pass | `InventoryRecordGuards.RequireMatchingInventoryLocation`; `TC-INVENTORY-009`. |
| AC-9 | Pass | `TC-INVENTORY-008`; diff review confirms no persistence/schema/Unity/ADR/equipment/attack/ActiveEffect/migration files changed. |
| AC-10 | Pass | `Tests/Metadata/test-catalog.json` includes `TC-INVENTORY-001`-`009`. |
| AC-11 | Pass | This task contract and ExecPlan exist. |
| AC-12 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-201` In Review with PR [#113](https://github.com/odyssey-services/Odyssey_VTT/pull/113). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- No persistence, repository interface, command service, or behavior beyond foundation value validation is implemented by design.

### Follow-up tasks

- `ODY-S05-202` — Inventory Persistence Foundation.

### Self-review summary

- Scope review: diff limited to allowed Domain/Application contract files, tests, metadata, and planning docs; no persistence/schema/Unity/ADR files changed.
- Architecture review: `ADR-027` preserved; Inventory is a separate aggregate vocabulary, item snapshots use exact `ContentDefinitionRef`, and equipment is only a location kind.
- Test review: 9 new `TC-INVENTORY-*` checks added and full `dotnet test` passed.
- Security/privacy review: no private material, hidden campaign data, secrets, or user data added.
- Documentation/version review: test metadata and planning docs updated; no application/schema/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-06 — Decision: use `ItemStackQuantity >= 1` for live stack records. Authority / approval: `ADR-027` section 6.2 says negative quantity is forbidden and zero is reached only through a future consume/destroy/remove command; this task has no such command.
- 2026-09-06 — Decision: do not add `IInventoryRepository` in `ODY-S05-201`. Authority / approval: user task brief and `SLICE-05_IMPLEMENTATION_BACKLOG.md` assign persistence/repository work to `ODY-S05-202`.
- 2026-09-06 — Decision: keep equipment as `InventoryLocationKind.Equipped` vocabulary only. Authority / approval: `ADR-027` section 7 and this task's explicit no-equipment-behavior boundary.
- 2026-09-06 — Amendment: `ItemInstanceRecord` and `ItemStackRecord` now validate that `Contained` and `Equipped` locations target the same `InventoryId` carried by the record. Authority / approval: owner review on PR #113.

### Approved task changes

- None.
