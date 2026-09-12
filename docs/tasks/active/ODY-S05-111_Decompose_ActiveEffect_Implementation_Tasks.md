# ODY-S05-111 — Decompose ActiveEffect Implementation Tasks (502+)

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects — implementation planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-111-decompose-activeeffect-implementation`
**Pull request:** [odyssey-services/Odyssey_VTT#136](https://github.com/odyssey-services/Odyssey_VTT/pull/136) (Draft)
**Plan:** `docs/plans/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md` (Brief plan)
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Decompose the item-sourced abilities/effects block's remaining implementation range (`ODY-S05-502` onward) into small, reviewable implementation tasks, now that `ADR-028` (`ODY-S05-501`) is `Accepted` and supplies the complete specification `ADR-027` §8 alone did not. This task is docs/planning-only: it does not implement any runtime code, and it does not create task-contract files for `502`–`507` themselves — those are each created when their own task is activated, the same precedent `ODY-S05-109` already set for `401`–`404`.

## 2. Why this task exists

- Problem or dependency being addressed: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1 (written by `ODY-S05-110`) explicitly deferred decomposing this block's implementation range until `ADR-028` was accepted — that condition is now met (PR #135, merged).
- Value or risk reduction: gives future agents small, single-responsibility implementation contracts for the `ActiveEffect` aggregate/persistence foundation, stacking resolution, non-combat duration/expiry, `WhileItemEquipped`/item-triggered creation wiring, removal/direct-creation permission gates, and integration fixtures — without pulling in turn/round-based durations or the full attack pipeline, which `ADR-028` §13 explicitly reserves elsewhere.
- Blocking or enabling relationship: follows the merged `ODY-S05-501` (PR #135, `ADR-028` accepted) and enables future `ODY-S05-502`–`507` task contracts.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md` and `docs/tasks/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md` (and their Brief plans) — the structural templates this task follows: `109` for the "fully-specified block decomposes straight into implementation tasks" pattern (this task now qualifies, unlike `110`), `110` for this exact block's own prior two-tier decision and its own §14/§14.1 text this task extends.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §9 (non-goals, as it existed before this task), §11 (backlog change control / reserved number ranges), §13/§13.1 (the immediately-prior "fully-specified block → direct task-table decomposition" precedent), §14/§14.1 (this block's own two-tier decision, which this task's own precondition — `ADR-028` acceptance — now satisfies).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §8.1/§8.2 — re-read to confirm this decomposition assigns, but does not reopen, its already-decided rules.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, full re-read — the now-complete specification this decomposition maps onto task boundaries: §5/§6 (aggregate/persistence), §7 (`EffectStackPolicy`, 7 values), §8 (`EffectDurationType`, 15 values — 9 presently implementable, `WhileItemEquipped` already solved by `ADR-027`, 6 reserved for Block 3), §9 (removal), §10 (permissions, 2 rules requiring different gates), §11 (fail-closed), §12 (atomicity), §13 (the Block 3 boundary this decomposition must not cross).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-111`, `SLICE-05`; `ADR-027` §8, `ADR-028`.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `45cba26` (merge of PR #135, `ODY-S05-501`) — `ADR-028` is `Accepted`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1 (before this task) stated: "Once `ODY-S05-501` is accepted, a future backlog revision decomposes `ODY-S05-502` onward..." — this task is that revision, its own precondition now satisfied.
- `ADR-028` re-read in full (already fully quoted/summarized in `ODY-S05-501`'s own task contract, re-confirmed here): §5/§6 fix the `ActiveEffect` aggregate's minimum record and standalone repository contract; §7 fixes all 7 `EffectStackPolicy` behaviors including a new `ActiveEffectStackConflict`/`ResolveActiveEffectStackConflict` pair for `RequestGMResolution`; §8 fixes 9 of 15 `EffectDurationType` mechanisms as presently implementable (`Instant`, `Permanent`, `UntilRemoved`, `WhileItemEquipped` [already solved by `ADR-027` §8.2 rule 3], `UntilSceneChange`, `UntilSessionEnd`, `WhileCondition`, `WhileSourceExists`, `ForDuration`) and 6 as explicitly reserved for the full attack pipeline block (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`/`End`, `UntilTargetTurnStart`/`End`); §9 fixes an explicit CAS-guarded, non-physically-deleting `RemoveActiveEffect` command; §10 fixes two different permission rules (item-triggered creation inherits the existing item-use model per rule 1; direct creation and explicit removal are MainGM-only per rules 2-3); §11 fixes fail-closed behavior on an inconclusive expiry check; §12 fixes mandatory `SqliteSavingPipeline` reuse; §13 fixes the Block 3 boundary this decomposition must not cross.
- Confirmed by direct re-read that `ADR-027` §8.1/§8.2's own three integration rules (item use may create `CharacterAbility`/`ActiveEffect`; Character/ItemInstance/SceneObject store only references; `WhileItemEquipped` subscribes to `ItemEquipped`/`ItemUnequipped`) are unchanged by `ADR-028` and are not reopened by this decomposition — they are assigned to `ODY-S05-505` as the task that *implements* them, not redecides them.
- Confirmed the immediately-prior fully-specified-block precedent (`ODY-S05-109`, decomposing `ADR-027` §10 into `401`–`404` directly, without any two-tier ADR-first step) is the correct structural template for this task, since `ADR-028`'s own acceptance is exactly the condition `ODY-S05-110` named for switching from its own two-tier approach back to this direct one.
- No repository-wide code search was required for this task's own findings (unlike `109`/`110`, which each verified a "clean slate" or "gap exists" claim by direct search) — this task's own scope is purely mapping `ADR-028`'s already-verified, already-accepted decisions onto task boundaries, not discovering a new architectural fact.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read` against `origin/main` and `ADR-028`'s own accepted text during this task.

## 5. Scope

### In scope

- Add this task contract.
- Add a Brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: point-correct §9's non-goals (add one bullet for this task, correct the existing item-use/`ActiveEffect` bullet's own stale "beyond `ODY-S05-501`" framing), extend §10's dependency rules for `502`–`507`, update §11's reserved-range sentence to record the range as now fully decomposed, correct §14.1's closing sentence from "a future backlog revision" to the fact of this revision, and add a new ordered-backlog section (§15) decomposing `502`–`507` with a task-boundaries subsection (§15.1) mirroring §7.1/§12.1/§13.1's own structural depth.
- Reserve task IDs `ODY-S05-502`–`507` for the item-sourced abilities/effects implementation range.

### Out of scope

- Any C# production code: `ActiveEffect` domain types, persistence, stacking resolution, duration/expiry mechanisms, commands, services.
- Any new tests requiring new runtime code.
- Unity/UI.
- Creating individual task-contract files for `ODY-S05-502`–`507` — each is created and activated separately when picked up, per `ODY-S05-109`'s own precedent for `401`–`404`.
- Editing `docs/adr/**` — `ADR-028` is already `Accepted`; this task only references it.
- The six turn/round-based `EffectDurationType` values or any combat/damage-sourced effect application, in any task's own scope — `ADR-028` §13 reserves both for the full attack pipeline block, unchanged by this decomposition.
- Rewriting `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions in §1–§8, §12, §13 — only additive/point-corrective text in §9/§10/§11/§14.1, and a new §15/§15.1, are introduced.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md
docs/plans/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-501_* (already Done/In Review/completed, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; future implementation tasks must preserve `ADR-028` §15's own module assignments (`Odyssey.Domain`/`Rules`/`Application`/`Persistence`), consistent with `ADR-001`/`ADR-027` §14.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here; future tasks must reuse `SqliteSavingPipeline` per `ADR-028` §12, not invent a new mechanism.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here.
- Time / RNG rule: Not applicable to this task.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited unless a real contradiction is found; none was found (`ADR-028` already fully specifies the target this decomposition sequences toward, the same relationship `ODY-S05-109` had to `ADR-027` §10).

## 7. Expected behavior

### Scenario 1 — implementation range decomposed directly, no further two-tier step

**Given** `ADR-028` is `Accepted` and supplies the complete specification `ADR-027` §8 alone did not
**When** this task decomposes the block
**Then** `502`–`507` are decomposed directly into a task table with purpose/dependencies/planning mode/scope, the same way `ODY-S05-109` decomposed `401`–`404` directly — no further ADR-first tier is introduced.

### Scenario 2 — every task has a non-overlapping, explicit boundary

**Given** `ADR-028` §5-§13 fixes 8 distinct decision areas
**When** this task assigns them to 6 tasks
**Then** §15.1 states, for each task, what it owns and what it explicitly does not implement, with no decision area assigned to more than one task and no decision area left unassigned.

### Scenario 3 — turn/round-based durations and combat effects never appear as any task's scope

**Given** `ADR-028` §13 explicitly reserves the six turn/round-based `EffectDurationType` values and combat/damage-triggered effect application for the full attack pipeline block
**When** this task writes §15/§15.1
**Then** none of `502`–`507`'s own scope descriptions include any of those six values or combat-triggered application, and §15.1's own closing paragraph states this boundary explicitly.

### Scenario 4 — dependencies are correct and explicit

**Given** `502` is the aggregate/persistence foundation and `503`–`506` each mutate or read that same aggregate
**When** this task fills the `Depends on` column
**Then** `503`–`506` each list `502` as a dependency, and `507` lists `502`–`506`, mirroring exactly how `402`–`404` listed their own dependencies on `401`/`401`-`402`/`401`-`403`.

### Scenario 5 — no task-contract files are created for `502`–`507`

**Given** `ODY-S05-109`'s own precedent of decomposing `401`–`404` without creating their task-contract files
**When** this task completes
**Then** no new file exists under `docs/tasks/active/ODY-S05-50[2-7]_*` — only this task's own `111` contract and plan.

### Required invariants

- `ADR-027` §8.1/§8.2 and `ADR-028` §5-§13 are assigned to tasks, never reopened or redecided.
- No product code, schema, runtime test implementation, or accepted ADR is changed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions in §1–§8, §12, §13 are not rewritten — only additive/point-corrective text in §9/§10/§11/§14.1, plus a new §15/§15.1.
- The six turn/round-based `EffectDurationType` values and combat/damage-triggered effect application never appear as any of `502`–`507`'s own in-scope work.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its Brief plan, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s implementation-range decomposition.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §11 records `ODY-S05-501`–`507` as fully decomposed, no number in that range left reserved-but-unscoped.
2. A new ordered-backlog section (§15) decomposes the block's implementation range into exactly 6 tasks (`502`–`507`), each with purpose, dependencies, proposed planning mode, and primary result, at the same structural depth as §7/§12/§13.
3. §15.1 states each task's own explicit boundary (what it owns, what it explicitly does not implement), with no overlap and no gap across `ADR-028` §5-§13's own 8 decision areas.
4. No task's own scope includes any of the six turn/round-based `EffectDurationType` values or combat/damage-triggered effect application; §15/§15.1 states this boundary explicitly.
5. `503`–`506` each depend on `502`; `507` depends on `502`–`506`.
6. §9/§10/§11/§14.1 are edited only additively/point-correctively; §1–§8, §12, §13 are not rewritten in substance.
7. No product code, schema, or test implementation is added.
8. No file under `docs/adr/**` is changed.
9. No new file exists under `docs/tasks/active/ODY-S05-50[2-7]_*` — task contracts for `502`–`507` are each created when their own task activates.
10. This task contract and Brief plan are added.
11. `git diff --name-status` against `main` shows only §5's allowed paths.
12. PR description states clearly that this is planning/decomposition only, with no implementation.

## 10. Tests and validation

### Required automated tests

None. This is a docs/planning-only decomposition task and does not add runtime behavior.

### Required commands

```powershell
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the allowed documentation/planning files changed.
- Confirm `ADR-028` §5-§13 and `ADR-027` §8.1/§8.2 were read in full and are accurately assigned (not reopened) across the new §15 task table.
- Confirm no task's own scope description contains any of the six turn/round-based `EffectDurationType` value names or references combat/damage-triggered application.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`: not required on its own merits (no code changes), but `dotnet test` is still run as an extra confirmation this docs-only diff leaves the whole suite green, matching `ODY-S05-109`/`110`/`501`'s own practice.
- Unity validation: not required — no Unity files change.

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
- Authorization / audience checks: not implemented here; future implementation tasks must preserve `ADR-028` §10's own two-rule permission model.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, and no accepted ADR change — identical reasoning to `ODY-S05-107`/`108`/`109`/`110`.
- Plan path: `docs/plans/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #135; future implementation of `502`–`507` begins once each task is separately activated.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this task's Brief plan.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`501` task contracts' own content, `SLICE-05_IMPLEMENTATION_BACKLOG.md` §1–§8, §12, §13.
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed; backlog `Last updated` remains current.
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

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — point-corrects §9 (one added bullet, one corrected bullet), §10 (6 new dependency-rule bullets), §11 (reserved-range sentence updated to record full decomposition), §14.1 (closing sentence updated from "a future backlog revision" to the fact of this revision); adds §15 (implementation-range ordered backlog, 6 tasks) and §15.1 (task boundaries).
- `docs/tasks/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md` — this task contract.
- `docs/plans/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md` — this task's Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors (harness projects included). |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 534/534 — identical count to before this docs-only diff. |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Met | §11 now reads "`ODY-S05-501` through `ODY-S05-507` are reserved... No further number in this block remains reserved-but-unscoped after this revision." |
| AC-2 | Met | §15 table has exactly 6 rows (`502`–`507`) with purpose/dependencies/planning mode/primary result, matching §7/§12/§13's own column structure. |
| AC-3 | Met | §15.1 gives each task an explicit "owns"/"must not" boundary; verified by re-reading against `ADR-028` §5-§13's own 8 decision areas — no overlap, no gap. |
| AC-4 | Met | `grep` against §15's own content confirms the six turn/round-based values and combat-triggered application appear exactly once, only in the closing exclusion statement. |
| AC-5 | Met | §15's own table: `503`/`504`/`505`/`506` each list `502` in `Depends on`; `507` lists `502-506`. |
| AC-6 | Met | `git diff` confirms §1–§8/§12/§13 unchanged in substance; §9/§10/§11/§14.1 edits are additive/point-corrective only. |
| AC-7 | Met | No `.cs`/`.json`/test file in the diff. |
| AC-8 | Met | No `docs/adr/**` file in the diff. |
| AC-9 | Met | No file exists under `docs/tasks/active/ODY-S05-50[2-7]_*`. |
| AC-10 | Met | This contract and Brief plan exist. |
| AC-11 | Met | `git diff --name-status` limited to §5's allowed paths. |
| AC-12 | Met | PR #136 body states planning/decomposition only. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves and decomposes future implementation tasks only; it does not create the individual `ODY-S05-502`–`507` task contract files.
- The exact fields/methods of `IActiveEffectRepository`, the exact event names `UntilSceneChange`/`UntilSessionEnd` subscribe to, and the exact `ActiveEffectStackConflict` shape are not decided here — `ADR-028` fixes their conceptual shape; the individual `502`/`503`/`504` tasks decide their concrete C# form when activated.

### Follow-up tasks

- `ODY-S05-502` — ActiveEffect Foundation.
- `ODY-S05-503` — Stacking Policy Resolution.
- `ODY-S05-504` — Non-Combat Duration/Expiry.
- `ODY-S05-505` — WhileItemEquipped Wiring + Item-Triggered Creation.
- `ODY-S05-506` — RemoveActiveEffect Command + Direct-Creation Permission Gates.
- `ODY-S05-507` — Item-Sourced Abilities/Effects Integration Fixtures.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this Brief plan. No `Packages/**`, `DotNet/**`, `Tests/**`, `docs/adr/**`, or new `docs/tasks/active/ODY-S05-50[2-7]_*` file touched.
- Architecture review: no accepted ADR changed; decomposition assigns `ADR-028` §5-§13 and `ADR-027` §8.1/§8.2 to tasks without reopening any of them; the Block 3 boundary is preserved explicitly.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts run and recorded.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog additive/point-corrective edits only; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — Decision: use Brief plan, not ExecPlan, for this task itself — identical reasoning to `ODY-S05-107`/`108`/`109`/`110` (docs/planning-only, no public contract or ADR change). Authority: `PLANS.md` §1.1.
- 2026-09-12 — **Decision: decompose `502`–`507` directly into a task table, without any further two-tier ADR-first step, since `ADR-028`'s own acceptance is exactly the condition `ODY-S05-110` named for returning to the direct decomposition pattern.** This is the same structural relationship `ODY-S05-109` had to `ADR-027` §10: a fully-specified governing document decomposed straight into implementation tasks. Authority: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1's own explicit precondition text ("Once `ODY-S05-501` is accepted, a future backlog revision decomposes `ODY-S05-502` onward...").
- 2026-09-12 — **Decision: 6 tasks (`502`–`507`), matching the governing task brief's own proposed decomposition, grouping `WhileItemEquipped` wiring with item-triggered creation into one task (`505`) rather than two, and grouping `RemoveActiveEffect` with the direct-creation permission gate into one task (`506`) rather than two.** Rationale for the first grouping: both halves of `505` share the same equip/unequip event seam and neither has independent value without the other being wired at the same time. Rationale for the second grouping: both halves of `506` are MainGM-only permission gates over the same aggregate with no shared code dependency on each other beyond the gate pattern itself, so splitting them would add task-count without reducing any single task's own complexity. Authority: this ТЗ §3's own explicit 6-task proposal and stated rationale for each grouping, verified against `ADR-028`'s own section boundaries (§5/§6 → `502`; §7 → `503`; §8/§11 → `504`; `ADR-027` §8.1/§8.2 + `ADR-028` §10 rule 1 → `505`; `ADR-028` §9/§10 rules 2-3 → `506`) before accepting it, not merely copied without verification.
- 2026-09-12 — Decision: `502` (Foundation) explicitly excludes stacking, expiry, and removal — it owns only the aggregate shape and basic CRUD persistence, mirroring exactly how `ODY-S05-401` (Migration Preview Foundation) and `ODY-S05-301`/`ODY-S05-502`'s own sibling `ODY-S05-201`/`ODY-S05-301` (Inventory/Equipment Runtime Foundation) each excluded command/mutation logic from their own foundation-tier scope. Authority: this ТЗ §3 item 1's own explicit boundary; direct structural analogy to `ODY-S05-201`/`301`/`401`'s own precedent.
- 2026-09-12 — Decision: `503` (Stacking) is a pure-function task consuming, not reimplementing, `502`'s own aggregate/repository — the same architectural relationship `ODY-S05-402` (`ComputeBlockingIssues`) already had to `ODY-S05-401` (`BuildPreview`): a computation task strictly downstream of a foundation task, never folded into it. Authority: this ТЗ §3 item 2's own explicit citation of the `402`/`401` precedent.
- 2026-09-12 — Decision: `504` (Non-Combat Duration/Expiry) explicitly excludes `WhileItemEquipped` (assigned to `505`, since `ADR-027` §8.2 rule 3 already ties it to the equip/unequip event seam `505` owns) and all six turn/round-based values (out of this whole range's scope per `ADR-028` §13). Authority: this ТЗ §3 item 3's own explicit exclusions; `ADR-028` §8's own table and §13's own boundary.
- 2026-09-12 — Decision: no repository-wide code search was performed as part of this task's own "verified facts," unlike `109`/`110`. Rationale: this task's own factual basis is `ADR-028`'s already-accepted, already-verified text (verified during `ODY-S05-501` itself), not a new architectural gap or clean-slate claim requiring independent verification — re-running the same searches `501` already ran would not produce new information relevant to a pure task-boundary decomposition. Authority: `ADR-028` (`ODY-S05-501`) already `Accepted`, its own verification stands.
- 2026-09-12 — Decision: extend `SLICE-05_IMPLEMENTATION_BACKLOG.md` §9/§10/§11/§14.1 point-correctively (fixing stale "not yet decomposed"/"beyond `ODY-S05-501`" framing that this task's own decomposition makes inaccurate) rather than leaving those sentences to silently drift out of sync with §15's new content. Authority: this ТЗ §5's own explicit instruction to update §11/§14.1's own stale framing, and §9's own instruction to "sverit' formulirovku... i skorrektirovat' tochechno" (check the wording and correct it point-by-point) if it now contradicts the new task IDs.
- 2026-09-12 — Decision: do not create task-contract files for `ODY-S05-502`–`507` in this task. Authority: this ТЗ §6's own explicit instruction, citing `ODY-S05-109`'s own identical precedent for `401`–`404`.
- 2026-09-12 — Decision: do not edit `docs/adr/**`. Rationale: `ADR-028` is already `Accepted`; this task only assigns its already-decided content to task boundaries. Authority: this ТЗ §7's own explicit forbidden-path list.

### Approved task changes

- None.
