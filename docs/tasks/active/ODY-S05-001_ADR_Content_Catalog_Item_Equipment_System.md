# ODY-S05-001 - ADR Content Catalog & Item/Equipment System

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 prerequisites
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `codex/ody-s05-001-adr-027-content-catalog`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/103
**ExecPlan:** `docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

Create the prerequisite architecture decision for `SLICE-05`: `ADR-027 - Content Catalog & Item/Equipment System`, plus the task/backlog documentation needed to review it before any product code or persistence schema is written.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-05` introduces inventory, items, equipment, item-sourced abilities/effects, ItemDefinition migration, and full attack. Existing product/domain documents describe the concepts but leave at least one implementation-critical boundary open: whether Inventory is part of Character or a separate aggregate root.
- Value or risk reduction: prevents future implementation tasks from silently inventing item snapshot, stack, equipment, migration, dependency-check, and permission behavior inside product code.
- Blocking or enabling relationship: unblocks later `SLICE-05` implementation backlog/task creation and closes the SLICE-04 documented item/inventory stubs for `RemoveBodyPart` and `DeleteCharacterPermanently`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md` sections 5, 6, 21, 22, 34, and 35.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14 and section 16.9.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` sections 16-18, command/event lists, invariants 24-66, aggregate-root list, and transaction notes for ItemDefinition migration.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`.
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`.
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`.
- `docs/adr/ADR-013_Migration_Runner_v1.0.md`.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`.
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`.
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`.
- `docs/adr/ADR-026_Character_Export_Import_File_Format_And_Redaction_v1.0.md`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` rows 8-13 and documented follow-up/stub notes.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-001`, `ADR-027`, `SLICE-05`.
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: roadmap/domain/content public repository documents are summarized by section and concept. No private prose, secrets, local private paths, or hidden campaign content are copied into this task.

## 4. Verified current state

### Verified facts

- Branch `codex/ody-s05-001-adr-027-content-catalog` was created from fresh `origin/main` after `git fetch origin`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` records `SLICE-04` closed and identifies completed item-adjacent Character tasks.
- `SqliteCharacterRepository` lines around the `DeleteCharacterPermanently` constructor note say dependency checkers default to an empty list because no Board/Item/GameLog cross-reference implementation existed at that task.
- `SqliteCharacterRepository` lines around `RemoveBodyPart` explicitly document the item-system dependency check as not checked because no Item/Inventory system existed yet.
- Domain Model section 17.1 states Persistence may implement Inventory as part of Character or as a separate root; no accepted ADR resolves that option before this task.
- Domain Model sections 17-18 require full `ItemInstance` and `ActiveEffect` mechanics snapshots, ItemDefinition preview/confirm migration for items, and no mass migration for existing ActiveEffects.
- `docs/adr/README.md` existed but listed only ADR-001 through ADR-010, despite accepted ADR files through ADR-026 being present.

### Assumptions

- None.

## 5. Scope

### In scope

- Create `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`.
- Create this task contract.
- Create an ExecPlan because the ADR changes future aggregate, persistence, permission, and migration contracts.
- Update `docs/adr/README.md`.
- Create `docs/tasks/SLICE-05_BACKLOG.md` as the prerequisite backlog following repository slice pattern.

### Out of scope

- Product code, tests, persistence schema, migrations, DTO implementation, Unity assets, Unity UI, or package changes.
- Real item/equipment/inventory commands.
- Full attack pipeline.
- Full Content Editor UI, marketplace, arbitrary scripts, concrete balanced MVP catalog entries.
- Changing accepted ADR contents.

### Allowed paths

```text
docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md
docs/adr/README.md
docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-026*
```

## 6. Technical constraints

- Module ownership and dependency direction: future implementation must follow `ADR-001`; this task changes no code.
- Authoritative-state and transaction boundary: ADR must reuse `ADR-002` command idempotency and `ADR-012` single-transaction append-only journal semantics.
- Serialization / compatibility boundary: ADR must require explicit versioned DTOs and no direct Domain serialization under `ADR-003`.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: ADR must reuse `ADR-019` baseline and not grant ordinary players offline authoritative inventory mutation.
- Performance or platform constraint: not applicable.
- Other: ADR-027 must stay prerequisite architecture only and not implement product behavior.

## 7. Expected behavior

### Scenario 1 - ADR resolves the implementation-critical boundary

**Given** Domain Model section 17.1 leaves Inventory placement open
**When** ADR-027 is reviewed
**Then** it chooses one authoritative aggregate boundary for Inventory and states its consequences for Character, ItemInstance, equipment, migration, and dependency checks.

### Scenario 2 - SLICE-04 stubs are explicitly unblocked

**Given** `RemoveBodyPart` and `DeleteCharacterPermanently` both documented missing item/inventory checks in SLICE-04
**When** ADR-027 is reviewed
**Then** it states which future item/inventory dependency checkers must satisfy those stubs.

### Required invariants

- ADR-027 remains `Proposed` unless product-owner approval is explicitly recorded.
- No product code or schema files are changed.
- `docs/adr/README.md` lists ADR-027 consistently.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: ADR-027, this task contract, ExecPlan, ADR README update, `SLICE-05_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. ADR status is `Accepted` only if product owner approval is explicitly recorded; otherwise ADR-027 uses the repository's normal pre-acceptance status convention.
2. No product code or schema files changed.
3. `docs/adr/README.md` lists ADR-027 consistently.
4. ADR cites existing authorities: `11_Content_Block_System`, Roadmap section 14, Domain Model sections 16-18, `ADR-001`, `ADR-002`, `ADR-003`, `ADR-007`, `ADR-011`-`ADR-013`, `ADR-019`, `ADR-022`, `ADR-024`, `ADR-025`.
5. ADR explicitly says which `SLICE-04` documented stubs it unblocks.
6. ADR fixes Content Catalog/`ContentDefinition` vs runtime instance boundaries.
7. ADR lists SLICE-05 catalog definitions: `ItemDefinition`, typed item definitions for Weapon/Armor/Ammo, `AbilityDefinition`, `EffectDefinition`, and Resource/BodyPart references where needed.
8. ADR states `ItemInstance` stores a full mechanics snapshot and does not change after publishing a new ItemDefinition without separate preview/confirm migration.
9. ADR states `ItemStack` may share a snapshot only for mechanically identical stackable items.
10. ADR chooses Inventory as either part of Character or a separate aggregate root, with one clear decision.
11. ADR fixes equipment slot/body-part references, ownership/location invariant, and one item in exactly one place.
12. ADR connects item-sourced abilities/effects to existing `CharacterAbility SourceKind=Item`/`ActiveEffect` and future `ActiveEffect` aggregate.
13. ADR fixes ItemDefinition migration preview/confirm baseline: MainGM-only, backup/preview, revision guards, blocked incompatibilities, no rollback command after successful migration.
14. ADR states existing ActiveEffects never mass-migrate to new EffectDefinitions.
15. ADR includes permissions baseline and non-goals requested by the task.
16. Required validation commands pass or are recorded honestly.
17. ADR explicitly fixes ContentDefinition archive/delete lifecycle for ItemDefinition, Weapon/Armor/Ammo typed definitions, AbilityDefinition, and EffectDefinition, reusing `11_Content_Block_System` lifecycle rules without implementing code/schema.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR task; replacement evidence is repository formatting and policy validation plus diff review.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Review ADR-027 against all acceptance criteria in section 9.
- Review `git diff --name-status` to confirm docs-only scope.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: not applicable.
- Other: PowerShell validation only.

### Validation not required by this task

- `dotnet build`, `dotnet test`, `test-unity`, `build-dev`, migration rehearsal, and player smoke are not required because no code, schema, Unity, package, or CI file changes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: future architectural contract only; no persisted state changes in this PR.
- Version fields affected: ADR-027 document version introduced as 1.0; no application/schema/contract/protocol/ruleset version changes.
- Migration or upcaster: None.
- Forward / backward behavior: not applicable.
- Rollback method: revert this docs-only PR.
- Data-loss risk and protection: none.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository documentation and future item/equipment architecture only.
- Trust boundaries: no private product prose or hidden campaign content copied into tracked files.
- Authorization / audience checks: no implementation; ADR records MainGM-only publish/migration and baseline inventory mutation restrictions.
- Redaction requirements: future notifications/previews must permission-filter details, but no redaction code is changed here.
- Log-safe fields: not applicable.
- Abuse / malformed input limits: not applicable.
- Security tests: none.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `PLANS.md` section 1.2 applies because ADR-027 changes future public contracts, aggregate boundaries, persistence expectations, migration behavior, permissions, and authoritative inventory semantics.
- ExecPlan path: `docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: do not begin SLICE-05 implementation backlog or item/equipment code until ADR-027 is owner-approved or the owner explicitly changes sequencing.

## 15. Documentation and versioning impact

- Documents that must change: ADR-027, this task contract, ExecPlan, ADR README, `SLICE-05_BACKLOG.md`.
- Documents that must not change: accepted ADR content, production code, test code, schema, Unity assets, private documents.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none implemented; future contract guidance only.
- Documentation version changes: ADR-027 introduced as v1.0 Proposed.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied or explicitly awaiting product-owner approval for Accepted status.
- [x] Required automated tests pass or are explicitly not applicable.
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

- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` - new proposed ADR.
- `docs/adr/README.md` - ADR index updated.
- `docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md` - ExecPlan.
- `docs/tasks/SLICE-05_BACKLOG.md` - prerequisite backlog.
- `docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md` - this task contract.
- Pull request: https://github.com/odyssey-services/Odyssey_VTT/pull/103.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`. |
| `git status --short` / scope review | Passed | Intended commit scope is limited to ADR/task/plan/backlog docs; unrelated untracked `Claude outputs/` remains excluded. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | ADR-027 status is `Proposed`; no product-owner approval was provided in the task. |
| AC-2 | Passed | Scope review found no product code or schema changes; only docs files are intended for commit. |
| AC-3 | Passed | `docs/adr/README.md` updated with ADR-027 under proposed ADRs. |
| AC-4 | Passed | ADR-027 traceability lists all required authorities. |
| AC-5 | Passed | ADR-027 section 9 names both SLICE-04 stubs. |
| AC-6-15 | Passed | ADR-027 sections 4-13 cover the requested boundaries. |
| AC-16 | Passed | Required validation commands passed locally. |
| AC-17 | Passed | ADR-027 section 4.1 fixes ContentDefinition archive/delete lifecycle and states no code/schema is implemented. |

### Build and artifact evidence

- Build identity: not applicable.
- Artifact path / name: none.
- Checksums: none.
- Test or quality report: not applicable.

### Known limitations

- ADR-027 is not `Accepted` until explicit product-owner approval is recorded.
- No production implementation is included.

### Follow-up tasks

- Future `SLICE-05` implementation backlog after ADR-027 approval.

### Self-review summary

- Scope review: docs-only; unrelated untracked `Claude outputs/` excluded from commit.
- Architecture review: ADR reuses existing ADRs and resolves only the requested prerequisite boundary.
- Test review: no tests changed; repository validation passed.
- Security/privacy review: no private content or secrets added.
- Documentation/version review: ADR-027 v1.0 Proposed; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for PR preparation.
- Product-owner approval is required before ADR-027 can become `Accepted`.

### Decisions made during execution

- 2026-09-03 - Decision: mark ADR-027 `Proposed`, not `Accepted`. Authority / approval: task acceptance check requiring explicit product-owner approval for `Accepted`.
- 2026-09-03 - Decision: create `SLICE-05_BACKLOG.md`. Authority / approval: repository pattern from prior slices and user request to create/update prerequisite backlog if the pattern requires it.
- 2026-09-03 - Decision: choose Inventory as a separate aggregate root in ADR-027. Authority / approval: Domain Model section 17.1 left the option open; roadmap section 14 requires transfer/equipment/attack operations spanning Character, Scene, ItemInstance, and effects.

### Approved task changes

- None.
