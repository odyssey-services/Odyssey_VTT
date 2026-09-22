# ODY-S05-112 — Decompose Full Attack Pipeline Block (Block 3)

**Status:** Done (PR #143, merged into main)
**Roadmap stage / slice:** SLICE-05 (full attack pipeline planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-112-decompose-attack-pipeline`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/143 (Draft)
**Plan:** `docs/plans/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md` (Brief plan)
**Created:** 2026-09-13
**Last updated:** 2026-09-13 UTC

## 1. Goal

Decompose the reserved "full attack pipeline" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 (per the product owner's own chosen block order: migration block, then item-sourced abilities/effects, then the full attack pipeline). This block has **no ADR coverage of any kind** — not even the partial coverage `ODY-S05-110` found before decomposing item-sourced abilities/effects. This task therefore decomposes the block into two tiers, exactly mirroring `ODY-S05-110`'s own structure: one dedicated ADR-specification task (`ODY-S05-601`), scoped now, and the block's remaining implementation tasks, deliberately reserved but **not** decomposed by this revision. This task is docs/planning-only: it does not implement any runtime code, and it does not itself write or edit any ADR — that is `ODY-S05-601`'s own job.

## 2. Why this task exists

- Problem or dependency being addressed: the item-sourced abilities/effects range (`ODY-S05-501`–`507`) is fully merged into `main` (PR #134–#142); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still named the full attack pipeline as reserved but not decomposed. Direct verification confirmed no ADR of any kind exists for it — worse coverage than item-sourced abilities/effects had (`ADR-027` §8 at least specified item-integration rules; nothing specifies the pipeline itself).
- Value or risk reduction: prevents a future implementation task from silently deciding ADR-level architecture (turn/round structure, the action→preview→range→modifier→roll→hit→damage→effect-application conveyor, its own integration with the already-accepted `ActiveEffect` mechanism) inline, the exact anti-pattern `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4 already forbids.
- Blocking or enabling relationship: follows the merged item-sourced abilities/effects range (PR #134–#142) and enables a future `ODY-S05-601` ADR task contract; that task, once accepted, in turn enables a future backlog revision to decompose `ODY-S05-602` onward into real implementation tasks.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md` (and its Brief plan) — the exact structural template this task follows near one-to-one, since both are two-tier (ADR-then-implementation) decompositions of a block with insufficient ADR coverage.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially §14/§14.1 (the immediately analogous prior decomposition, item-sourced abilities/effects), §8 (reserved future blocks, as it existed before this task), §9 (global non-goals), §10 (dependency rules), §11 (backlog change control / reserved number ranges), §4 ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline").
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §1 rule 13, §13 ("Non-goals"), §18 ("Открытые вопросы" / "Deferred but not open here") — every place this ADR names the full attack pipeline, all as an exclusion, never a specification.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §13 ("Boundary with the full attack pipeline (Block 3)", verbatim) and §14 ("Non-goals") — the only accepted ADR text naming the pipeline's own required integration with `ActiveEffect` (the six reserved `EffectDurationType` values, "how a combat roll decides to apply an effect").
- `docs/tasks/SLICE-05_BACKLOG.md` §3.2 — the only place in this repository that actually enumerates the pipeline's own named parts, read directly and quoted verbatim, not paraphrased from memory.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-112`, `SLICE-05`.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `07c7ace` (merge of PR #142, `ODY-S05-507`) — the entire item-sourced abilities/effects range (`ODY-S05-501`–`507`) is fully closed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8, before this task, named exactly one remaining reserved-but-undecomposed block: the full attack pipeline (the item-sourced abilities/effects bullet had already been removed by `ODY-S05-110`). This task removes that last bullet, replacing it with a pointer to the new §16.
- **Central finding, independently verified (not assumed from the ТЗ's own text): no ADR of any kind — draft, numbered, or stub — exists anywhere under `docs/adr/` for the full attack pipeline.** A direct directory listing of `docs/adr/` was performed; every existing file was cross-checked by name and none concerns the attack pipeline.
- `ADR-027` (direct re-read) names the full attack pipeline only as an exclusion, in exactly three places: §1 rule 13 ("This ADR does **not** implement... full attack pipeline..."); §13 "Non-goals" (lists "full attack pipeline" among items this ADR does not cover); §18 "Открытые вопросы", under "Deferred but not open here" (lists "full attack pipeline" first). None of the three specifies anything about the pipeline's own content — all three are negative/boundary statements.
- `ADR-028` (direct re-read) names the full attack pipeline in §13 ("Boundary with the full attack pipeline (Block 3)") and §14 ("Non-goals"). §13 is the single most substantive mention in any accepted ADR: it explicitly reserves "the six turn/round-based `EffectDurationType` values" and "damage-over-time, on-hit, and other combat-roll-triggered effect applications" for this block, and explicitly states `ADR-028` "specifies... not... how a combat roll decides to apply one." This is real information (a real currently-open integration point `601` must resolve), but it is still a boundary statement, not a specification of the pipeline itself.
- `ADR-027` §18 (direct re-read, "Open questions" section context) cites the ultimate authority for the pipeline's own scope as `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §14. A direct `git ls-tree`/directory listing of the tracked repository confirmed **no `Documentation/` directory exists anywhere** — that file is not available to consult, in this repository, at all.
- The only place in the tracked repository that actually enumerates what the pipeline's own vertical slice contains is the closed, historical `docs/tasks/SLICE-05_BACKLOG.md` §3.2, read directly and quoted verbatim: "Roadmap section 14.6 names action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, and game log. Those are full implementation and likely further ADR/task-decomposition work. `ADR-027` deliberately fixes only the item/equipment/content-catalog substrate needed before that pipeline can safely reference items, equipment, ammo, abilities, and effects." This is itself a secondary citation of the external roadmap document, not a specification `601` may treat as complete or authoritative — this task contract and the backlog's own new §16 both flag this explicitly, so `601`'s own executor does not mistake a closed-backlog quote for a real specification.
- Consequence: this decomposition cannot follow `ODY-S05-107`/`108`/`109`'s own single-tier pattern, and the case for a two-tier decomposition is **stronger** here than it was for `ODY-S05-110`'s own item-sourced abilities/effects block — that block at least had `ADR-027` §8's own item-integration rules to decompose around; this block has no comparable text anywhere in any accepted ADR.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/directory listing/repository-wide search/direct file read against `origin/main` during this task.

## 5. Scope

### In scope

- Add this task contract.
- Add a Brief plan for this task.
- Add a task contract for the future `ODY-S05-601` task (the contract only — not the ADR itself, and not `601`'s own execution).
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: remove "full attack pipeline" from §8's reserved-but-undecomposed list (replacing it with a pointer to the new §16); add a new ordered-backlog section (§16) decomposing the block's first tier into exactly one task, `ODY-S05-601` (an ADR-specification task, not an implementation task), with a task-boundaries subsection (§16.1) mirroring §14/§14.1's own structural depth; extend §9's non-goals, §10's dependency rules, and §11's reserved-number-range sentence additively (no rewrite of existing decisions); point-correct the three existing "(section 8)" cross-references that specifically named the full attack pipeline's own future location, now that it has moved to §16 (a direct, correct consequence of this task's own edit, not unrelated drift).
- Reserve the task-ID range `ODY-S05-601`–`ODY-S05-60N` for the whole full attack pipeline block, explicitly recording in text that only `601` is decomposed by this revision — the remaining count and scope of implementation tasks in that range is a future backlog revision, not decided here.

### Out of scope

- Any C# production code.
- Any ADR document (creating or editing `docs/adr/**`) — that is `ODY-S05-601`'s own job, not this decomposition task's.
- Deciding any architectural question about the pipeline itself (turn/round structure's own shape, the exact conveyor steps, permission model, how a combat roll applies an `ActiveEffect`) — every such question is explicitly deferred into `ODY-S05-601`'s own task contract as a requirement on the future ADR, never resolved inline by this task.
- Decomposing the block's implementation tasks (`ODY-S05-602` onward) into concrete task IDs — explicitly forbidden by this ТЗ, deferred to a future backlog revision once `601`'s own ADR is accepted.
- Any new tests requiring new runtime code.
- Unity/UI.
- Inventing content from `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, which does not exist in this repository — this task cites only `docs/tasks/SLICE-05_BACKLOG.md` §3.2's own secondary quote and `ADR-028` §13/§14's own boundary text; it does not guess at the roadmap document's own content beyond what those two sources already say.
- Rewriting `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions in §1–§7, §12, §13, §14, or §15 — only additive text (a new §16/§16.1, additive/corrective sentences in §8/§9/§10/§11/§14/§15) is introduced.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md
docs/tasks/active/ODY-S05-601_ADR_Full_Attack_Pipeline_Specification.md
docs/plans/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-507_* (already Done/In Review/completed, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; a future attack-pipeline implementation must follow `ADR-001` and whatever module-ownership rule `ODY-S05-601`'s own ADR fixes.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited by this task under any circumstance — that decision belongs entirely to `ODY-S05-601`, which this task only reserves the number and writes the task contract for.

## 7. Expected behavior

### Scenario 1 — full attack pipeline block decomposed into two tiers

**Given** no ADR of any kind exists for the full attack pipeline
**When** this planning task updates the backlog
**Then** the block is decomposed into one ADR-specification task (`ODY-S05-601`) plus an explicitly-reserved, explicitly-undecomposed remainder — not a full set of implementation task IDs.

### Scenario 2 — the total absence of ADR coverage is verified, not assumed

**Given** the ТЗ's own claim that no ADR of any kind exists for the pipeline, and that the cited roadmap file is absent from the repository
**When** this task searches the tracked `docs/adr/` directory and the repository's own file tree directly
**Then** the decomposition records, with direct search evidence, that zero pipeline ADRs exist, that `Documentation/` does not exist in this repository, and cites the exact three ADR-027 exclusion sites plus the two ADR-028 boundary sections and the one `SLICE-05_BACKLOG.md` §3.2 secondary quote — all located and quoted directly, not paraphrased from memory.

### Scenario 3 — the remaining implementation tasks stay genuinely undecomposed

**Given** this ТЗ's explicit prohibition on decomposing `602` onward into real task IDs
**When** this task writes §16
**Then** the table contains exactly one row (`601`), and the surrounding text states in plain language that the rest of the range is reserved-but-unscoped, to be decomposed by a future backlog revision.

### Scenario 4 — `ODY-S05-601`'s own task contract requires, but does not decide, the pipeline's architecture

**Given** the risk that a decomposition task could accidentally pre-decide real architecture (e.g. the exact shape of turn/round structure) while writing `601`'s own contract
**When** this task writes `docs/tasks/active/ODY-S05-601_ADR_Full_Attack_Pipeline_Specification.md`
**Then** every substantive architectural question (turn/round structure, the conveyor's own steps, `ActiveEffect` integration, permission model) is phrased as a requirement on the future ADR to resolve, never as an answer this contract itself supplies.

### Scenario 5 — the external roadmap document is flagged as an open question, not silently inferred

**Given** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` does not exist in this repository, and `ADR-027`/`ADR-028` both cite it as the ultimate scope authority
**When** this task writes `601`'s own task contract
**Then** it explicitly instructs `601`'s own future executor to raise the missing roadmap document as an open question to the product owner if genuinely needed, rather than inventing its content.

### Required invariants

- No product code, schema, runtime test implementation, or accepted ADR architecture section is changed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — only additive text is introduced, plus the required §8 bullet replacement and the three corrective section-number cross-reference fixes that are a direct consequence of this same edit.
- The new §16 table names exactly one decomposed task (`601`); no row for `602` or higher with a real title/scope/dependency exists anywhere in this task's diff.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its Brief plan, `ODY-S05-601`'s own task contract, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s full attack pipeline decomposition.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 no longer lists the full attack pipeline as an undecomposed reserved block.
2. A new ordered-backlog section (§16) decomposes the block's first tier into exactly one task (`ODY-S05-601`, an ADR-specification task), at the same structural depth as §14/§14.1, but explicitly not decomposing `602` onward.
3. The decomposition records, with direct search evidence, that no ADR of any kind exists for the pipeline anywhere in `docs/adr/`, that `Documentation/` does not exist in this repository, and cites the exact `ADR-027`/`ADR-028` exclusion/boundary text plus `SLICE-05_BACKLOG.md` §3.2's own secondary quote.
4. `ODY-S05-601`'s own task contract requires — but does not itself decide — the pipeline's turn/round structure, its own conveyor steps, and its integration with the already-accepted `ActiveEffect` mechanism (the six reserved `EffectDurationType` values, `ADR-028` §13's own undecided "how a combat roll decides to apply an effect" rule).
5. `ODY-S05-601`'s own task contract explicitly instructs its future executor to raise the missing external roadmap document as an open question to the product owner if genuinely needed, not to invent its content.
6. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — diff confined to additive text plus the required §8 bullet replacement and the three direct-consequence cross-reference corrections.
7. No product code, schema, or test implementation is added.
8. No file under `docs/adr/**` is changed.
9. This task contract, its Brief plan, and `ODY-S05-601`'s own task contract are added.
10. `git diff --name-status` against `main` shows only §5's allowed paths.
11. PR description states clearly that this is planning/decomposition only, with no implementation and no ADR content.

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
- Confirm `ADR-027` §1 rule 13/§13/§18 and `ADR-028` §13/§14 were read directly and are quoted/cited accurately in this contract and the backlog update.
- Confirm the "no ADR exists for the pipeline / `Documentation/` does not exist in this repository" finding was verified directly against source (directory listing/search), not assumed from the ТЗ.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`: not required on its own merits, but `dotnet test` is run anyway as an extra confirmation that a docs-only diff leaves the whole suite green, matching `ODY-S05-110`'s own practice of running the full validation set even for a planning-only task.
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
- Authorization / audience checks: not implemented here; `ODY-S05-601`'s own future ADR must explicitly decide the pipeline's own permission model.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, no permissions behavior, and no accepted ADR architecture change — identical reasoning to `ODY-S05-107`/`108`/`109`/`110`.
- Plan path: `docs/plans/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #142; the future `ODY-S05-601` ADR task starts once this decomposition is accepted; per the product owner's own chosen block order, no further block is currently sequenced after the full attack pipeline.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this task's Brief plan, `ODY-S05-601`'s own task contract.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`507` task contracts' own content.
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed.
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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — removes "full attack pipeline" from §8, replacing it with a pointer to §16; adds §16 (full attack pipeline ADR-foundation ordered backlog) and §16.1 (task boundaries); additive sentences in §9 (non-goals), §10 (dependency rules), and §11 (reserved number range); point-corrects three pre-existing "(section 8)" cross-references (in §9, §14, §15) that specifically named the pipeline's own future decomposition location, now §16.
- `docs/tasks/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md` — this task contract.
- `docs/tasks/active/ODY-S05-601_ADR_Full_Attack_Pipeline_Specification.md` — the future ADR task's own contract.
- `docs/plans/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md` — this task's Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 612/612 — identical count to before this docs-only diff. |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | |
| `.\scripts\verify-test-structure.ps1` | PASS | |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Met | §8's full attack pipeline bullet replaced with a pointer to §16 (confirmed via `git diff`). |
| AC-2 | Met | New §16/§16.1 decompose the block into exactly one task (`601`), at the same structural depth as §14/§14.1. |
| AC-3 | Met | §16 records the direct `docs/adr/` listing (zero pipeline ADRs), the missing `Documentation/` directory, and cites `ADR-027` §1 rule 13/§13/§18, `ADR-028` §13/§14, and `SLICE-05_BACKLOG.md` §3.2 verbatim. |
| AC-4 | Met | `ODY-S05-601`'s own task contract §5's "In scope" and §7's scenarios phrase every architectural question as a requirement on the future ADR, never as a decision this contract makes. |
| AC-5 | Met | `ODY-S05-601`'s own task contract explicitly instructs raising the missing roadmap document as an open question, not inventing its content. |
| AC-6 | Met | `git diff` shows §1–§7, §12, §13 untouched, and §14/§15 touched only at the three specific direct-consequence cross-reference points; all other edits are additive. |
| AC-7 | Met | No `.cs`/`.json`/test file in the diff. |
| AC-8 | Met | No `docs/adr/**` file in the diff. |
| AC-9 | Met | This contract, its Brief plan, and `ODY-S05-601`'s own contract exist. |
| AC-10 | Met | `git diff --name-status` limited to §5's allowed paths. |
| AC-11 | Met | Draft PR body states planning/decomposition only, with no implementation and no ADR content. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves the future implementation-task range only; it does not write `ODY-S05-601`'s own ADR (that is `601`'s own job once activated), nor does it decompose `602` onward.
- The exact form of the full attack pipeline's own architecture (turn/round structure, conveyor steps, permission model) is not decided here — `ODY-S05-601`'s own future ADR decides it.
- The external roadmap document (`Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`) remains unavailable in this repository; `601`'s own executor may need to raise this to the product owner as a genuine open question, not resolved by this task.

### Follow-up tasks

- `ODY-S05-601` — ADR: Full Attack Pipeline Specification.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, `ODY-S05-601`'s own contract, and this Brief plan. No `Packages/**`, `DotNet/**`, `Tests/**`, or `docs/adr/**` file touched.
- Architecture review: no accepted ADR changed; decomposition follows direct verification of the ADR gap and does not decide new runtime architecture — that is explicitly deferred to `ODY-S05-601`.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts run and recorded.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog additive edits only (plus three direct-consequence cross-reference corrections); no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-13 — Decision: use Brief plan, not ExecPlan, for this task itself — identical reasoning to `ODY-S05-107`/`108`/`109`/`110` (docs/planning-only, no public contract or ADR change). Authority: `PLANS.md` §1.1.
- 2026-09-13 — **Decision (the central architectural finding of this task): the full attack pipeline block has zero ADR coverage of any kind, a stronger case for two-tier decomposition than even `ODY-S05-110`'s own item-sourced abilities/effects block had.** Direct repository search confirmed no ADR of any kind exists for the pipeline; `ADR-027` and `ADR-028` each name it only as an exclusion (three sites in `ADR-027`, two in `ADR-028`), never a specification. Authority: direct `docs/adr/` directory listing; direct re-read of every cited `ADR-027`/`ADR-028` section.
- 2026-09-13 — Decision: decompose this block into exactly one task now (`ODY-S05-601`, an ADR-specification task) and explicitly defer the rest of the block's decomposition to a future backlog revision, mirroring `ODY-S05-110`'s own precedent exactly. Authority: this ТЗ's own explicit two-tier decomposition instruction; direct analogy to `ODY-S05-110`/`111`'s own already-accepted precedent for the immediately prior block.
- 2026-09-13 — **Decision: `ODY-S05-601`'s own task contract cites only `docs/tasks/SLICE-05_BACKLOG.md` §3.2's own secondary quote and `ADR-028` §13/§14's own boundary text as the pipeline's scope starting point — it does not invent content from `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, which this task independently confirmed does not exist anywhere in this repository.** The contract explicitly instructs `601`'s own future executor to raise the missing roadmap document as an open question to the product owner if genuinely needed for a complete specification, rather than guessing at its content. Authority: this ТЗ's own explicit instruction on this exact point; direct repository-wide directory listing confirming `Documentation/` does not exist.
- 2026-09-13 — Decision: reserve `ODY-S05-601` through `ODY-S05-60N` (an open, not-yet-fixed count) for this block, rather than a closed range. Rationale: this block's implementation-task count cannot be responsibly fixed before `601`'s own ADR exists to decompose against — the same reasoning `ODY-S05-110` already applied when it first reserved `ODY-S05-501`–`ODY-S05-50N` (later closed at `507` by `ODY-S05-111`, once `ADR-028` existed). Authority: this ТЗ's own explicit `ODY-S05-601`–`ODY-S05-60N` phrasing; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §11's own existing convention, now used a second time.
- 2026-09-13 — **Decision: point-correct three pre-existing "(section 8)" cross-references (in §9, §14, §15) that specifically named the full attack pipeline's own future decomposition location, updating them to "(section 16)".** These are a direct, correct consequence of this task's own edit to §8 — before this task, "the full attack pipeline" and "section 8" were the same thing; after this task, the pipeline has moved to its own section 16, and any cross-reference specifically pointing at "the full attack pipeline's own territory (section 8)" would otherwise become silently wrong. This is distinguished from `ODY-S05-110`'s own established discipline of *not* touching unrelated, already-stale cross-references (e.g. the pre-existing, unrelated "ItemDefinition migration workflow implementation (section 8)" bullet in §9, which this task also left untouched, since fixing that drift is not a consequence of this task's own edit). Authority: direct `grep` of every "(section 8)" occurrence in the backlog, checked individually for whether it specifically concerned the full attack pipeline (corrected) or something else entirely (left alone); `ODY-S05-110`'s own task contract §18, which already drew this same "fix direct consequences, not unrelated drift" line for its own edit.
- 2026-09-13 — Decision: do not edit `docs/adr/**`. Rationale: this is a decomposition task, not the ADR-writing task itself; writing or editing an ADR is `ODY-S05-601`'s own job. Authority: this ТЗ's own explicit statement and forbidden-path list, identical in spirit to `ODY-S05-110`'s own analogous restriction.

### Approved task changes

- None.
