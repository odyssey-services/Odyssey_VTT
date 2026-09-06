# ODY-S05-107 — Decompose Inventory Runtime Block

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Inventory runtime planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `docs/ody-s05-107-inventory-runtime-backlog`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/112
**Plan:** `docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` (Brief plan)
**Created:** 2026-09-06
**Last updated:** 2026-09-06 UTC

## 1. Goal

Decompose the next `SLICE-05` Inventory runtime block into small, reviewable implementation tasks after Content Catalog MVP closure. This task is docs/planning-only: it creates the Inventory runtime backlog shape without implementing runtime code, persistence schema, commands, tests that require new runtime code, Unity UI, or accepted ADR changes.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-05_IMPLEMENTATION_BACKLOG.md` previously reserved Inventory runtime as a future block after `ODY-S05-101`-`106`, but did not break it into executable tasks.
- Value or risk reduction: gives the next agents small implementation contracts for Inventory, `ItemInstance`, `ItemStack`, persistence, basic commands, and dependency checks without mixing Equipment, ActiveEffect execution, ItemDefinition migration, or attack-pipeline scope into the first runtime block.
- Blocking or enabling relationship: follows merged PR #111 (`ODY-S05-106`) and enables future `ODY-S05-201`-`207` task contracts.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially section 7 reserved future blocks as it existed before this task.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, especially sections 5, 6, 7, 8, 9, 10, 11, 12, and 20.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md`, sections 16-18.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, section 14.
- Current Content Catalog MVP task contracts `docs/tasks/active/ODY-S05-101_*` through `docs/tasks/active/ODY-S05-106_*`.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-107`, `SLICE-05`.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: the user-provided task brief for `ODY-S05-107` only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation.
- Branch `docs/ody-s05-107-inventory-runtime-backlog` was created from `origin/main`.
- `origin/main` is at merge commit `57274d5`, which is PR #111 (`ODY-S05-106`).
- PR #105 (`ODY-S05-101`), #106 (`ODY-S05-102`), #107 (`ODY-S05-105`), #108 and #109 (`ODY-S05-104`), #110 (`ODY-S05-103`), and #111 (`ODY-S05-106`) are all merged.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` still marked `ODY-S05-106` as `In Review` before this task; this task updates that row to `Done` with PR #111.
- `ADR-027` already decides Inventory as a separate aggregate root, item/stack mechanics snapshots, Equipment as Inventory-owned location state, item-sourced ability/effect integration, runtime dependency-check stub closure, ItemDefinition migration preview/confirm, ActiveEffect non-migration, and permissions baseline.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` sections 16-18 define ContentDefinition, Inventory, ItemStack, ItemInstance, ItemDefinition migration preview, weapon/armor state, InventoryTransaction, and ActiveEffect concepts.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14 names Inventory, definitions/snapshots, effects/resources, and attack pipeline scope for `SLICE-05`.

### Assumptions

- None.

## 5. Scope

### In scope

- Add this task contract.
- Add a brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` to mark Content Catalog MVP complete and add the Inventory runtime task decomposition.
- Reserve future implementation task IDs for the next Inventory runtime block.

### Out of scope

- Product code.
- New C# production types.
- Persistence schema or migrations.
- Runtime tests requiring new Inventory/ItemInstance/ItemStack code.
- Unity/UI.
- Actual item creation commands.
- Equipment behavior.
- Attack resolution.
- ActiveEffect runtime.
- ItemDefinition migration workflow.
- Balanced content pack.
- `.odcontent` import/export.
- Accepted ADR architecture changes.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md
docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-106_*
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; future tasks must follow `ADR-001` and `ADR-027` section 14.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here; future commands must follow `ADR-002`.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here; future persistence work must follow `ADR-003`, `ADR-011`, `ADR-012`, and `ADR-013`.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized and must not include private documentation, secrets, hidden campaign content, or user data.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited unless a real contradiction is found; no contradiction was found.

## 7. Expected behavior

### Scenario 1 — Content Catalog MVP closure recorded

**Given** PR #111 is merged into `main`  
**When** this planning task updates the `SLICE-05` implementation backlog  
**Then** `ODY-S05-106` is marked `Done` with PR #111 and the Content Catalog MVP block is presented as complete.

### Scenario 2 — Inventory runtime block decomposed

**Given** `ADR-027` sections 5-12 and Roadmap section 14 define the Inventory/runtime boundaries  
**When** this planning task updates the backlog  
**Then** the next Inventory runtime block is broken into small tasks covering Inventory foundation, ItemInstance/ItemStack runtime foundation, persistence, create-from-Published-definition commands, move/transfer, split/merge, runtime dependency checks, and integration fixtures.

### Scenario 3 — Later blocks stay deferred

**Given** Equipment runtime, attack pipeline, item use, ActiveEffect execution, and ItemDefinition migration are related but larger follow-on areas  
**When** this planning task decomposes Inventory runtime  
**Then** those areas remain explicitly out of scope except where minimal location/dependency vocabulary is needed for Inventory runtime sequencing.

### Required invariants

- Inventory remains a separate aggregate root, not a Character section.
- Runtime items do not depend on latest catalog definitions.
- `ItemInstance`/`ItemStack` store mechanics snapshots, with shared stack snapshots only for mechanically identical stackable items.
- Equipment remains Inventory-owned location state, not Character-owned state.
- Runtime references must eventually protect catalog definitions from physical delete.
- No product code, schema, runtime test implementation, or accepted ADR architecture section is changed.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, brief plan, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` Inventory runtime decomposition.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. Content Catalog MVP (`ODY-S05-101`-`106`) is marked complete.
2. Inventory runtime block is decomposed into small implementation tasks.
3. Each Inventory runtime task has purpose, dependencies, expected planning mode, in-scope summary, and explicit out-of-scope boundaries.
4. The decomposition preserves `ADR-027`: Inventory is a separate aggregate root; runtime items do not depend on latest catalog definitions; `ItemInstance`/`ItemStack` store mechanics snapshots; Equipment is Inventory-owned location state, not Character-owned state; runtime references must eventually protect catalog definitions from physical delete.
5. No product code, schema, or test implementation is added.
6. No accepted ADR architecture section is changed.
7. This task contract and brief plan are added.
8. PR description states clearly that this is planning/decomposition only.

## 10. Tests and validation

### Required automated tests

None. This is a docs/planning-only decomposition task and does not add runtime behavior.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the three allowed documentation/planning files changed.
- Confirm PR #105-#111 merge status and that PR #111 closes the Content Catalog MVP block.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI is used only to verify PR merge status and open the Draft PR.

### Validation not required by this task

- `dotnet build DotNet\Odyssey.Core.sln` and `dotnet test DotNet\Odyssey.Core.sln`: not required because no product code, test code, schema, contracts, or project files change.
- Unity validation: not required because no Unity files change.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this documentation-only branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository planning documentation only.
- Trust boundaries: no runtime trust boundary is changed.
- Authorization / audience checks: not implemented here; future tasks must preserve `ADR-027` section 12.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data to repository docs or PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, no permissions behavior, and no accepted ADR architecture change.
- Plan path: `docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #111 is merged; future Inventory runtime implementation starts at `ODY-S05-201`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this task's brief plan.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, and existing Content Catalog MVP task contracts.
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed; backlog `Last updated` changes to 2026-09-06 UTC.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are correctly marked not applicable.
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

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — marks `ODY-S05-106` Done with PR #111 and adds the `ODY-S05-201`-`207` Inventory runtime decomposition.
- `docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` — this task contract.
- `docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` — this task's brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Pass | `Repository policy check passed`; `REPO-POLICY-*` and `TC-CI-*` checks passed. |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid dependency/version/duplicate-ID cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-106` Done with PR #111. |
| AC-2 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` section 7 decomposes Inventory runtime into `ODY-S05-201`-`207`. |
| AC-3 | Pass | Section 7 task table and section 7.1 list purpose, dependencies, planning mode, in-scope summary, and out-of-scope boundaries. |
| AC-4 | Pass | Section 7 preserves `ADR-027` Inventory aggregate, snapshot, Equipment-location, and runtime-reference physical-delete protections. |
| AC-5 | Pass | Diff limited to documentation/planning files; no product code, schema, or test implementation added. |
| AC-6 | Pass | No files under `docs/adr/**` changed. |
| AC-7 | Pass | This task contract and brief plan are added. |
| AC-8 | Pass | Draft PR [#112](https://github.com/odyssey-services/Odyssey_VTT/pull/112) states the work is planning/decomposition only and lists validation. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves future implementation tasks only; it does not create the individual `ODY-S05-201`-`207` task contract files.

### Follow-up tasks

- `ODY-S05-201` — Inventory Runtime Foundation.
- `ODY-S05-202` — Inventory Persistence Foundation.
- `ODY-S05-203` — Create Item/Stack From Published Definition.
- `ODY-S05-204` — Inventory Move / Transfer MVP.
- `ODY-S05-205` — Stack Split/Merge MVP.
- `ODY-S05-206` — Runtime Reference Dependency Checks.
- `ODY-S05-207` — Inventory Runtime Integration Fixtures.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this task's brief plan; unrelated untracked `Claude outputs/` left untouched.
- Architecture review: no accepted ADR changed; decomposition follows `ADR-027` sections 5-12 and does not decide new runtime architecture.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts passed.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog `Last updated` changed to 2026-09-06 UTC; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-06 — Decision: use Brief plan because this task changes only planning documentation and does not implement or change public runtime contracts. Authority: `PLANS.md` section 1.1 and this task's docs/planning-only scope.
- 2026-09-06 — Decision: decompose Inventory runtime into `ODY-S05-201`-`207`, keeping Equipment runtime, ActiveEffect execution, ItemDefinition migration workflow, attack pipeline, balanced content, `.odcontent`, and UI as later blocks. Authority: `ADR-027` sections 5-12 and Roadmap section 14.
- 2026-09-06 — Decision: do not edit `ADR-027` because no contradiction was found; this task applies its accepted decisions to backlog sequencing only. Authority: user out-of-scope instruction and `AGENTS.md`.

### Approved task changes

- None.
