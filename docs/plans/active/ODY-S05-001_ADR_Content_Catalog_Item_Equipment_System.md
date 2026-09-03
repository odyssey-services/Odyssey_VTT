# ODY-S05-001 - ADR Content Catalog & Item/Equipment System

**Status:** Completed
**Owner:** Codex (agent)
**Branch:** `codex/ody-s05-001-adr-027-content-catalog`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/103
**Last updated:** 2026-09-03 UTC

## 1. Purpose and user-visible outcome

Prepare the prerequisite ADR for `SLICE-05` so later inventory, equipment, item-definition migration, effect, and full-attack tasks have one accepted boundary to follow instead of inventing runtime item semantics inline.

## 2. Task contract

- Goal: create the `ODY-S05-001` task contract, proposed `ADR-027`, ADR README update, and `SLICE-05` prerequisite backlog where the repository pattern requires it.
- Acceptance criteria: `ADR-027` uses a pre-acceptance status unless product-owner approval is explicitly recorded; no product code/schema changes; `docs/adr/README.md` lists ADR-027 consistently; ADR cites the named authorities; ADR names the `SLICE-04` stubs it unblocks.
- Requirement IDs: `ODY-S05-001`, `ADR-027`, `SLICE-05` prerequisite backlog.
- In scope: docs-only architecture/task/backlog files.
- Out of scope: product code, persistence schema, migrations, real item/equipment commands, Content Editor UI, marketplace, concrete catalog balancing, Unity UI.
- Required authorities: active documentation baseline, `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`, `11_Content_Block_System`, roadmap section 14, Domain Model sections 16-18, `ADR-001`, `ADR-002`, `ADR-003`, `ADR-007`, `ADR-011`-`ADR-013`, `ADR-019`, `ADR-022`, `ADR-024`, `ADR-025`, `ADR-026`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Branch `codex/ody-s05-001-adr-027-content-catalog` was created from fresh `origin/main` after `git fetch origin`.
- `SLICE-04_IMPLEMENTATION_BACKLOG.md` records `SLICE-04` as closed and includes documented stubs for missing item/inventory integration.
- `SqliteCharacterRepository` documents two relevant stubs: `DeleteCharacterPermanently` currently has no Board/Item/GameLog checker implementations, and `RemoveBodyPart` explicitly does not check item/inventory dependencies because no item/inventory system exists yet.
- `docs/adr/README.md` existed but listed only ADR-001 through ADR-010, so this task must bring it back into a consistent index.
- No `SLICE-05_BACKLOG.md`, `ODY-S05-001` task contract, or `ADR-027` exists before this task.

Assumptions: none.

## 4. Proposed approach

Write a documentation-only ADR that specializes existing authorities:

- keep Content Catalog as versioned `ContentDefinition` records; runtime `ItemInstance`, `ItemStack`, Inventory, equipment, `CharacterAbility`, and `ActiveEffect` are not content definitions;
- choose Inventory as a separate aggregate root for SLICE-05 to resolve the Domain Model's open "part of Character or separate root" option;
- require `ItemInstance` full mechanics snapshots and stack shared snapshots only for mechanically identical stackable items;
- require one item/stack in exactly one place, with equipment as an inventory location/state over owner/body-part/slot references;
- connect item-sourced abilities/effects through existing `CharacterAbility SourceKind=Item` and future `ActiveEffect` aggregate references;
- fix ItemDefinition migration preview/confirm as MainGM-only with revision guards, backup/preview, blocked incompatibilities, no post-success rollback command, and runtime-state preservation;
- explicitly distinguish ActiveEffect snapshots from item migration: existing ActiveEffects never mass-migrate to new EffectDefinitions.

No code, schema, or commands are implemented.

## 5. Milestones

### M1 - Context and branch

- [x] Fetch `origin`.
- [x] Create branch from `origin/main`.
- [x] Read relevant ADR/task/product authorities and locate SLICE-04 stubs.

### M2 - Documents authored

- [x] Create `docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`.
- [x] Create `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`.
- [x] Create `docs/tasks/SLICE-05_BACKLOG.md`.
- [x] Update `docs/adr/README.md`.

### M3 - Validation and PR readiness

- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [x] Review `git diff --name-status` for docs-only scope.
- [x] Commit, push, and open PR if repository credentials allow it.

## 6. Progress log

- 2026-09-03 UTC - Fetched `origin` and created branch `codex/ody-s05-001-adr-027-content-catalog` from `origin/main`.
- 2026-09-03 UTC - Read active baseline, planning rules, task template, `SLICE-04` backlogs, `ADR-011`-`013`, `ADR-019`, `ADR-022`, `ADR-024`, `ADR-026`, roadmap section 14, Domain Model sections 16-18, and Content Block System sections for definitions/effects/permissions.
- 2026-09-03 UTC - Located the two SLICE-04 documented stubs in `SqliteCharacterRepository`: missing item dependency check for `RemoveBodyPart` and future dependency checker injection for `DeleteCharacterPermanently`.
- 2026-09-03 UTC - Authored ADR-027, task contract, ExecPlan, `SLICE-05_BACKLOG.md`, and ADR README update.
- 2026-09-03 UTC - Validation passed: `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1`.
- 2026-09-03 UTC - Scope review confirmed only ADR/task/plan/backlog docs are intended for commit; unrelated untracked `Claude outputs/` remains excluded.
- 2026-09-03 UTC - Created Draft PR #103: https://github.com/odyssey-services/Odyssey_VTT/pull/103.
- 2026-09-03 UTC - Amended ADR-027 before owner approval with explicit ContentDefinition archive/delete lifecycle rules reusing `11_Content_Block_System`.

## 7. Decisions

- 2026-09-03 UTC - Decision: use ExecPlan. Rationale: the ADR changes future public contracts, aggregate boundaries, persistence/permissions expectations, migration behavior, and authoritative state semantics. Authority: `PLANS.md` section 1.2.
- 2026-09-03 UTC - Decision: mark ADR-027 `Proposed`, not `Accepted`. Rationale: the task gives no explicit product-owner approval record; acceptance check requires Accepted only when approval is explicitly recorded. Authority: current task acceptance checks.
- 2026-09-03 UTC - Decision: create `SLICE-05_BACKLOG.md`. Rationale: every prior slice has a prerequisite/implementation backlog pattern, and the user asked to create/update prerequisite backlog if repository pattern requires it.

## 8. Discoveries and deviations

- `SLICE-04_IMPLEMENTATION_BACKLOG.md` previously said no dedicated content-catalog task was needed for SLICE-04 because that slice used minimal fixtures; this does not apply to SLICE-05, where item/inventory/equipment are first-class roadmap scope.
- The Domain Model explicitly allowed Inventory either inside Character or as a separate root; ADR-027 resolves this as separate aggregate root for SLICE-05.
- `docs/adr/README.md` was stale relative to accepted ADR files already present on `origin/main`; this task updates the index rather than touching accepted ADR contents.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- Diff scope review: passed; intended commit scope is limited to `docs/adr/README.md`, `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, `docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`, `docs/tasks/SLICE-05_BACKLOG.md`, and `docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`.

## 10. Recovery and rollback

Rollback is a normal docs-only revert of this branch. No product code, schema, migration, assets, dependencies, generated artifacts, or private content are created.

## 11. Open questions and blockers

None for this task. Product-owner approval remains required to change `ADR-027` from `Proposed` to `Accepted`.

## 12. Outcome and follow-up

Validation passed. Draft PR #103 opened for owner review.
