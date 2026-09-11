# ODY-S05-109 — Decompose ItemDefinition Migration Block

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `docs/ody-s05-109-decompose-item-definition-migration-block`
**Pull request:** Not opened
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Decompose the reserved "`ItemDefinition` migration preview/confirm" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 into small, reviewable future implementation tasks — the third such decomposition in this backlog, mirroring both `ODY-S05-107` (Inventory runtime) and `ODY-S05-108` (Equipment runtime). Planning-only: no product code, no tests, no ADR edits.

## 2. Task contract

- Goal: add the `ODY-S05-109` task contract/Brief plan and update `SLICE-05_IMPLEMENTATION_BACKLOG.md` so the `ItemDefinition` migration block is decomposed out of §8's reserved list into a new ordered section (§13/§13.1).
- Acceptance criteria: `ItemDefinition` migration bullet removed from §8; decomposed into small implementation tasks (`ODY-S05-401`–`404`), each with purpose/dependencies/planning mode/in-scope/out-of-scope; `ADR-027` §10's nine-step workflow and blocking-incompatibility list preserved verbatim; §11's ActiveEffect-never-mass-migrates distinction named explicitly so `401`–`404` is not confused with a future ActiveEffect migration mechanism; the "clean slate" (no existing migration code anywhere) verified by grep, not assumed; `RulesetMigrationRules`/`ApplyCharacterRulesetMigration` (`ODY-S04-113`) named as the structural precedent, with an explicit note that fields/types must be reinvented for item snapshots, not copied; no product code/schema/test implementation or accepted ADR changes; Draft PR states planning/decomposition only.
- Requirement IDs: `ODY-S05-109`, `SLICE-05`.
- In scope: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan.
- Out of scope: product code, schema, migrations, runtime tests, Unity/UI, real `ItemDefinitionMigrationPreview`/apply commands, item-sourced abilities/effects or attack-pipeline decomposition, accepted ADR edits.
- Required authorities: `ODY-S05-107`/`ODY-S05-108`'s own task contracts/plans (structural template), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7/§7.1/§8/§9/§10/§11/§12/§12.1, `ADR-027` §9/§10/§11/§12/§15/§16, `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed; branch `docs/ody-s05-109-decompose-item-definition-migration-block` created from `origin/main`.
- `origin/main` is at commit `643b734` (merge of PR #128, `ODY-S05-306`) — the Equipment runtime block (`301`–`306`) is fully merged and closed.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names "`ItemDefinition` migration preview/confirm" as reserved/undecomposed, alongside two other blocks (item-sourced abilities/effects, full attack pipeline) that stay reserved, unchanged. §13/§13.1 are unused — the document currently ends at §12.1.
- `ADR-027` §10/§11/§12/§16 fully re-read and quoted verbatim below (§4 of this task's own contract). No contradiction found.
- `git grep`-equivalent search across all tracked `.cs` files for "ItemDefinitionMigration"/"MigrationPreview" found matches only in two existing forbidden-fragment scope-guard test arrays (`SqliteInventoryRepositoryTests.cs`, `InventoryCreationServiceTests.cs`) — no actual implementation, type, table, or command exists anywhere. Confirmed by direct search, not assumed.
- `RulesetMigrationRules.BuildPlan`/`ComputePreviewHash` + `SqliteCharacterRepository.ApplyCharacterRulesetMigration` (`ODY-S04-113`) confirmed as a real, already-accepted preview/plan/apply-separation precedent: `CharacterRulesetMigrationPlan` carries `DefinitionMappings`, `UnresolvedDecisions`, three `ExpectedXRevision` fields, `PreviewHash`, and `HasUnresolvedDecisions`; `ApplyCharacterRulesetMigration` takes the built plan directly.

Assumptions: none.

## 4. Proposed approach

Update only planning docs:

- Remove the "`ItemDefinition` migration preview/confirm" bullet from §8's reserved-but-undecomposed list (the other two bullets stay, unchanged).
- Add new §13 "Ordered backlog (`ItemDefinition` migration block)" after §12.1, at the same structural depth as §7/§12: an intro naming `ADR-027` §10's nine-step workflow verbatim, the blocking-incompatibility list verbatim, §11's ActiveEffect-distinction, and a table of four tasks (`ODY-S05-401`–`404`); §13.1 "`ItemDefinition` migration task boundaries" (one paragraph per task ID, mirroring §7.1/§12.1's style).
- Reserve `ODY-S05-401`–`404`:
  1. **Migration Preview Foundation** — `ItemDefinitionMigrationPreview`-shaped type + builder (steps 1-4 of §10), modeled after `RulesetMigrationRules.BuildPlan`'s shape but reinvented for item/stack snapshots. No apply, no backup call, no blocking rules.
  2. **Migration Blocking Incompatibility Rules** — the type-specific and generic blocking-incompatibility computations named in §10's own list (Weapon ammo, Armor slot/body-part coverage, generic capacity/custom-state/hidden-mechanics). Computation only, inside Preview.
  3. **Migration Confirm/Apply Command** — MainGM-only confirm (steps 5-9): triple revision guard, mandatory `ADR-012` backup before review, atomic multi-snapshot update, no rollback command.
  4. **Migration Integration Fixtures** — end-to-end proof mirroring `ODY-S05-207`/`306`. No production code.
- Add small additive sentences to §9 (non-goals: "implementing `ItemDefinition` migration under `ODY-S05-109` itself; section 13 only decomposes future implementation tasks"), §10 (dependency-rule bullets for `401`–`404`), and §11 (extend the reserved-number-range sentence to include `ODY-S05-401`–`404`) — no rewrite of existing sentences.
- Record this task contract and validation evidence.

No production code, no Unity/UI, no new persistence table, no new `ErrorCode`, no `ADR` change.

## 5. Milestones

### M1 — Backlog and task docs

- [x] Add `docs/tasks/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md`.
- [x] Add this Brief plan.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (§8 removal, new §13/§13.1, additive §9/§10/§11 sentences).

### M2 — Validation and PR

- [x] Review diff for docs-only scope.
- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [x] Run `.\scripts\verify-test-structure.ps1`.
- [ ] Commit, push, and open Draft PR.

## 6. Progress log

- 2026-09-12 — Preflight: fetched `origin`, created branch from `origin/main` (`643b734`), re-read `ODY-S05-107`/`108`'s task contracts/plans as the structural template.
- 2026-09-12 — Re-read `ADR-027` §9/§10/§11/§12/§15/§16 in full; confirmed via search that no `ItemDefinitionMigration`/`MigrationPreview` implementation exists anywhere (only forbidden-fragment guard-test arrays); confirmed `RulesetMigrationRules`/`ApplyCharacterRulesetMigration` as the real structural precedent.
- 2026-09-12 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` (§8 bullet removal, new §13/§13.1, additive §9/§10/§11 sentences); discovered and fixed a placement bug during editing (the new §13 was initially inserted before a trailing paragraph that actually belongs at the end of §12.1, stranding it after the new section — reordered so §12.1's own closing paragraph stays with it); programmatically diffed the §13 quoted `ADR-027` §10 text against the actual ADR file and found/fixed one missing blank blockquote line.
- 2026-09-12 — Ran all three required validation scripts, all passed.

## 7. Decisions

See task contract §18 for the full decision log.

## 8. Discoveries and deviations

- A placement bug: the new §13 heading was initially inserted immediately after `ODY-S05-306`'s own paragraph, which stranded §12.1's actual closing paragraph ("If any of `ODY-S05-301`–`306` discovers...") after the new §13/§13.1 content instead of at the end of §12.1 where it belongs. Fixed by moving that paragraph back to the end of §12.1 before the new §13 heading.
- A verbatim-quote formatting bug: the §13 quote of `ADR-027` §10 was initially missing one blank line inside the blockquote (between "Workflow:" and its numbered list). Caught by a programmatic line-by-line diff against the actual ADR file, not by visual inspection alone. Fixed; final quote confirmed byte-for-byte identical to the ADR source.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed.`
- `.\scripts\verify-test-structure.ps1`: passed with exit code 0 and `TC-ARCH-001 PASS valid ADR-001 graph passes`.
- Diff review: changed only `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this Brief plan. No product code, schema, tests, Unity files, or accepted ADR files changed.

## 10. Recovery and rollback

Rollback is a normal revert of this documentation-only branch. No schema, migration, build artifact, dependency, or runtime state is changed.

## 11. Open questions and blockers

None for this task.

## 12. Outcome and follow-up

Draft PR to be opened. Follow-up implementation tasks reserved: `ODY-S05-401` through `ODY-S05-404`.
