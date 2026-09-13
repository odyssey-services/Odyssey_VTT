# ODY-S05-601 — ADR: Full Attack Pipeline Specification

**Status:** Proposed
**Roadmap stage / slice:** SLICE-05 (full attack pipeline, Block 3)
**Owner:** Codex (agent)
**Requested by:** Product owner (activated by `ODY-S05-112`)
**Branch:** `feat/ody-s05-601-full-attack-pipeline-adr`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/144 (Draft)
**Plan:** Brief plan in §14 — the change is limited to one documentation area, has a single ADR deliverable and validation path, creates no runtime/public/persisted contract or dependency, and requires no migration/recovery procedure.
**Created:** 2026-09-13 (contract only; task not yet started)
**Last updated:** 2026-09-13 UTC

> **This is a task contract for future work, not a record of completed work.** It was created by `ODY-S05-112` to activate the first tier of the full attack pipeline block's decomposition. No ADR content, no architecture, and no code exist yet. Sections below describe *requirements* on the future ADR and the future execution, not decisions already made. Do not treat any example or possibility mentioned here as a decision — every substantive architectural question named below is explicitly left open for this task's own future executor and the ADR review process, not settled by this contract.

## 1. Goal

Produce and gain acceptance for a new ADR that specifies the full attack pipeline (SLICE-05 Block 3): a document deliverable, not code. The ADR must define, at minimum, the turn/round structure (which does not exist in any currently accepted ADR), the full action conveyor named by `docs/tasks/SLICE-05_BACKLOG.md` §3.2 (action intent → preview → range → modifiers → roll → hit → body part → armor → damage → costs → effect application → intervention → atomic apply → compensation → game log), and this pipeline's own integration with the already-accepted `ActiveEffect` mechanism (`ADR-028`) — specifically the six reserved turn/round-based `EffectDurationType` values and `ADR-028` §13's own explicitly undecided "how a combat roll decides to apply an effect" rule. This task does not implement any of it in code.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-112` found that the full attack pipeline block has zero ADR coverage of any kind — `ADR-027`/`ADR-028` only exclude it from their own scope. No implementation task in this block can be safely decomposed or executed without an accepted ADR to decompose against, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4's own standing rule against deciding architecture inline inside an implementation task.
- Value or risk reduction: prevents a future implementation task from inventing turn/round structure, conveyor ordering, or `ActiveEffect` integration rules ad hoc, which would risk direct contradiction with `ADR-028`'s own already-accepted `ActiveEffect` aggregate design.
- Blocking or enabling relationship: depends on `ODY-S05-112` (this decomposition) and the fully merged item-sourced abilities/effects range (`ODY-S05-501`–`507`, providing the `ActiveEffect` mechanism this pipeline must integrate with). Enables a future backlog revision to decompose `ODY-S05-602` onward into concrete implementation tasks once this ADR is accepted.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §16/§16.1 (this block's own decomposition, added by `ODY-S05-112`), §9 (global non-goals), §10 (dependency rules).
- `docs/tasks/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md` (the decomposition task that activated this contract) — read in full before starting, not paraphrased from memory.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §1 rule 13, §13 (Non-goals), §18 ("Открытые вопросы" → "Deferred but not open here") — every place this ADR excludes the pipeline from its own scope.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md` §13 ("Boundary with the full attack pipeline (Block 3)") and §14 (Non-goals) — the only accepted-ADR text with real integration content for this pipeline: the six reserved turn/round-based `EffectDurationType` values, and the explicitly undecided "how a combat roll decides to apply an effect" rule.
- `docs/tasks/SLICE-05_BACKLOG.md` §3.2 — the only in-repository text enumerating the pipeline's own named conveyor parts. Quote verbatim; treat as a secondary historical citation of an external roadmap document, not as a complete specification in itself.
- Whatever ADR authoring template/process convention this repository uses for other accepted ADRs (`ADR-001` through `ADR-028`) — the new ADR must follow the same numbering, structural, and acceptance conventions as its predecessors.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-601`, `SLICE-05`, Block 3 ("full attack pipeline").
- Existing test IDs: None affected — this task produces no test-catalog entries.
- New test IDs to introduce: None — this is a document-only deliverable; any test IDs belong to a future implementation task once this ADR is accepted.

### Task-safe private context

- Approved summary / references: none beyond the public authorities listed above. If the executor believes private/hidden product documentation (e.g. an actual roadmap document) is genuinely required to complete this ADR responsibly, that must be raised as an explicit open question to the product owner — see §4 and §9 below — not assumed or invented.

## 4. Verified current state

### Verified facts

- No ADR of any kind exists under `docs/adr/` for the full attack pipeline, turn/round structure, or combat resolution, as of `origin/main` at the merge of PR #142.
- `ADR-027` cites `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §14 as the ultimate scope authority for the pipeline; this directory/file does not exist anywhere in the tracked repository (confirmed via direct repository-wide search during `ODY-S05-112`).
- The only in-repository text enumerating the pipeline's own parts is the closed `docs/tasks/SLICE-05_BACKLOG.md` §3.2: "Roadmap section 14.6 names action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, and game log. Those are full implementation and likely further ADR/task-decomposition work. `ADR-027` deliberately fixes only the item/equipment/content-catalog substrate needed before that pipeline can safely reference items, equipment, ammo, abilities, and effects."
- `ADR-028` §13 ("Boundary with the full attack pipeline (Block 3)") reserves exactly six turn/round-based `EffectDurationType` values and explicitly states it does not specify how a combat roll decides to apply an effect — this is the one piece of real, binding integration content this ADR must honor.
- Re-verified at task start after `git fetch origin`: `origin/main` is `3eedfc5` (PR #143 merged); `ODY-S05-112`, this task contract, and `ADR-028` are tracked on the base. The highest ADR is `ADR-028`; `ADR-029` is available. `Documentation/` remains absent.

### Assumptions the future executor must re-verify, not trust from this contract

- That the above facts still hold at the time this task is actually started — re-run the same repository searches (`docs/adr/` listing, `Documentation/` existence check) rather than trusting this contract's own snapshot, since `main` will have moved on by then.
- Whether any private/external roadmap document genuinely needs to be consulted to complete this ADR responsibly is explicitly **not** assumed here — if the executor believes it does, that must become an open question to the product owner (see §9), not a silent assumption.

## 5. Scope

### In scope (requirements on the future ADR and its acceptance process)

- Author a new, numbered ADR (next available number after the current highest accepted ADR) specifying, at minimum:
  - The turn/round structure for combat (does not exist in any currently accepted ADR).
  - The full action conveyor named in `SLICE-05_BACKLOG.md` §3.2: action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, game log — as an ordered pipeline, with clear boundaries between planning/preview (non-authoritative) and committed/authoritative steps.
  - This pipeline's own integration with the already-accepted `ActiveEffect` aggregate (`ADR-028`): the six reserved turn/round-based `EffectDurationType` values' own semantics, and a decision for `ADR-028` §13's own explicitly undecided "how a combat roll decides to apply an effect" rule.
  - Module ownership and dependency direction for any new module(s) this pipeline requires, consistent with `ADR-001`'s own architecture rules.
  - Authoritative-state and transaction boundaries for the pipeline's own commit step (which layer owns "atomic apply" and "compensation" from `SLICE-05_BACKLOG.md` §3.2's own enumeration).
  - Non-goals / explicit exclusions, mirroring the style of every prior accepted ADR.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` once the ADR is accepted, marking `ODY-S05-601` as done and (if this task's own executor judges it appropriate) proposing the next backlog revision's own task-decomposition boundaries for `ODY-S05-602` onward — but only as a proposal; actually decomposing `602`+ into concrete task IDs is explicitly a **future, separate task**, not this one's own job (see Out of scope).
- Raise, as an explicit open question to the product owner, whether the external roadmap document (`Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, cited by `ADR-027` but absent from this repository) needs to be supplied before this ADR can be considered complete.

### Out of scope

- Any production code, schema, or test implementation — this task produces a document only.
- Decomposing `ODY-S05-602` onward into concrete implementation task IDs — that is a future, separate backlog-revision task, once this ADR is accepted (mirroring exactly how `ODY-S05-111` only decomposed `502`-`507` after `ADR-028`/`501` was accepted).
- Inventing content attributed to `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` — if this task's own executor cannot respond to a requirement without that file, the correct action is to raise it as an open question to the product owner, not to guess at its content.
- Re-opening or amending any already-accepted ADR's own decisions (`ADR-001`–`ADR-028`) beyond what is needed to state this new ADR's own boundary/integration points with them (e.g., referencing `ADR-028` §13, not editing it).

### Allowed paths (once this task is activated)

```text
docs/adr/ADR-0XX_Full_Attack_Pipeline_Specification_v1.0.md   (new file; exact number TBD at activation time)
docs/tasks/active/ODY-S05-601_ADR_Full_Attack_Pipeline_Specification.md   (this contract, updated to reflect real execution)
docs/plans/active/ODY-S05-601_*.md   (if the executor's chosen planning mode requires a separate plan file)
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md   (status update only, once the ADR is accepted)
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001 through ADR-028 (any existing ADR — reference only, do not amend without separate explicit authorization)
Packages/**
DotNet/**
Tests/**
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: the new ADR itself must state this pipeline's own module ownership and dependency direction, consistent with `ADR-001`.
- Authoritative-state and transaction boundary: the ADR must specify which layer owns the pipeline's own atomic-apply/compensation step; not decided by this contract.
- Serialization / compatibility boundary: any new DTO/schema shape is the new ADR's own decision, not this contract's.
- Time / RNG rule: turn/round timing and any dice-roll RNG source is the new ADR's own decision.
- Unity / thread / lifetime rule: Not decided by this contract.
- Dependency / licensing rule: no dependency changes are anticipated for a document-only deliverable; if the executor's process requires tooling, that must go through the same dependency-approval rule as any other task.
- Security / privacy / redaction rule: the ADR text itself must remain sanitized — no hidden campaign content, secrets, or private documentation excerpts.
- Other: the ADR must explicitly ground its own scope in `SLICE-05_BACKLOG.md` §3.2's own verbatim quote and `ADR-028` §13/§14's own boundary text — it must not present the non-existent roadmap file's content as if directly consulted.

## 7. Expected behavior (requirements on the future ADR's own content, not decisions made here)

### Scenario 1 — turn/round structure is specified

**Given** no currently accepted ADR defines turn/round structure
**When** this task's ADR is written
**Then** it defines the turn/round structure completely enough for a future implementation task to build against without inventing further architecture inline.

### Scenario 2 — the full conveyor is specified end-to-end

**Given** `SLICE-05_BACKLOG.md` §3.2 names fourteen conveyor steps (action intent through game log)
**When** this task's ADR is written
**Then** it specifies each step's own responsibility, ordering, and authoritative/non-authoritative boundary, sufficient for future implementation tasks to be decomposed against it without re-deciding architecture.

### Scenario 3 — `ActiveEffect` integration is specified, not left open

**Given** `ADR-028` §13 reserves six turn/round-based `EffectDurationType` values and explicitly leaves "how a combat roll decides to apply an effect" undecided
**When** this task's ADR is written
**Then** it resolves that specific open question and defines the six values' own combat semantics, without contradicting any already-accepted `ADR-028` decision.

### Scenario 4 — the missing roadmap file is handled honestly

**Given** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` does not exist in this repository
**When** this task's executor cannot resolve a requirement without it
**Then** the executor raises this explicitly as an open question to the product owner in the ADR's own "Open questions" section (or equivalent), rather than inventing the file's presumed content.

### Required invariants

- The new ADR does not contradict any already-accepted ADR decision (`ADR-001`–`ADR-028`); where it must reference one, it cites the exact section, the same discipline `ODY-S05-112` itself followed.
- No production code, schema, or test is introduced by this task.
- `ODY-S05-602` onward remains undecomposed until a separate future backlog-revision task addresses it.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: the new ADR document; this contract updated to reflect real execution; a backlog status update once the ADR is accepted.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A new ADR is authored specifying turn/round structure, the full fourteen-step conveyor, and `ActiveEffect` integration (six `EffectDurationType` values, the combat-roll-application rule), per §7's scenarios.
2. The ADR is grounded explicitly in `SLICE-05_BACKLOG.md` §3.2's own verbatim quote and `ADR-028` §13/§14's own boundary text — it does not present invented content as if drawn from the non-existent roadmap file.
3. Any requirement the executor cannot resolve without the missing roadmap file is raised as an explicit open question to the product owner, not silently assumed.
4. The ADR does not contradict any already-accepted ADR (`ADR-001`–`ADR-028`).
5. `ODY-S05-602` onward is not decomposed by this task — that remains a future, separate backlog-revision task.
6. No production code, schema, or test is introduced.
7. The ADR is reviewed and accepted through this repository's normal ADR acceptance process before `SLICE-05_IMPLEMENTATION_BACKLOG.md` is updated to mark `601` done.

## 10. Tests and validation

### Required automated tests

None — document-only deliverable.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
.\scripts\verify-repository.ps1
```

`verify-docs.ps1` is not present in this checkout and is therefore recorded as not run rather than substituted with a private command. Runtime test suites are not required: this task adds no production code or test behavior.

### Manual validation

- Confirm the ADR's own citations to `SLICE-05_BACKLOG.md` §3.2 and `ADR-028` §13/§14 are accurate (direct re-read, not memory).
- Confirm no contradiction exists with any already-accepted ADR.
- Confirm the missing roadmap file was either not needed, or was explicitly raised as an open product-owner question — not silently assumed.

### Required environments / profiles

To be determined by the executor; likely identical to every other planning task this session (Windows development machine, authenticated GitHub CLI for opening a Draft PR).

## 11. Compatibility, migration, and rollback

- Compatibility impact: a new ADR does not itself change runtime compatibility; any compatibility impact belongs to the future implementation tasks it enables.
- Version fields affected: None at this task's own level.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable to a document-only deliverable.
- Rollback method: revert the ADR-authoring branch/PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None anticipated | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository documentation only.
- Trust boundaries: the ADR must itself define the pipeline's own permission/authorization model as part of its required content (e.g. who may resolve an ambiguous hit/damage roll) — not decided by this contract.
- Authorization / audience checks: to be specified by the new ADR.
- Redaction requirements: no hidden campaign content, secrets, or private documentation excerpts in the ADR text.
- Log-safe fields: Not applicable to a document-only deliverable.
- Abuse / malformed input limits: to be specified by the new ADR where relevant to the pipeline's own future implementation.
- Security tests: Not applicable to this task.

## 14. Planning and execution mode

- Planning mode: Brief plan, recorded here rather than in a separate plan file. Rationale: the work changes one documentation area, has one clear ADR-authoring/review path, introduces no code, public runtime contract, persistence format, dependency, migration, or recovery operation, and can be validated in one focused PR. This is proportionate to `PLANS.md` §1.1 even though the task is documentation-only.
- Brief plan: (1) re-verify ADR number, required contract/dependency state, absent roadmap, and exact source citations; (2) author `ADR-029` with the pipeline, turn/round, `ActiveEffect`, atomicity, and permission decisions; (3) update this contract with decisions/evidence; (4) review document-only diff and run documentation/repository validation. Do not update the backlog status or decompose `602`+ before product-owner acceptance.
- Plan path: Not applicable — no standalone ExecPlan is required.
- Expected pull request count: at least 1 (the ADR itself); a follow-up backlog-status-update commit/PR may be needed once accepted, mirroring the two-commit pattern used elsewhere in this backlog.
- Milestone or sequencing constraints: depends on `ODY-S05-112` (this decomposition) and the fully merged item-sourced abilities/effects range (`ODY-S05-501`–`507`). Must be accepted before any future task decomposes or implements `ODY-S05-602` onward.

## 15. Documentation and versioning impact

- Documents that must change: a new `docs/adr/ADR-0XX_*.md` file; this task contract, updated to reflect real execution; `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own status for `601`, once accepted.
- Documents that must not change: any already-accepted ADR's own decisions (`ADR-001`–`ADR-028`); production code, schema, tests.
- Application version change: to be determined by the executor per this repository's own ADR-acceptance versioning convention.
- Schema / format / contract / protocol / ruleset version change: None at this task's own level — any such change belongs to the future implementation tasks this ADR enables.
- Documentation version changes: the new ADR's own version header, per this repository's own ADR template convention.
- Changelog or release-note requirement: to be determined by the executor per this repository's own convention for ADR acceptance.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass or are correctly marked not applicable.
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

- 2026-09-13 — Re-synchronized with `origin`; `origin/main` advanced to `3eedfc5`, the merge of PR #143 (`ODY-S05-112`). Created a clean task worktree/branch from that base to avoid unrelated local `ODY-S05-205` changes.
- 2026-09-13 — Re-read required task/backlog/ADR authorities, confirmed `ADR-029` is the next available number, and confirmed the required external `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` is not tracked.
- 2026-09-13 — Authored proposed `docs/adr/ADR-029_Full_Attack_Pipeline_Specification_v1.0.md`. It defines the timeline, all labels from the historical pipeline quote, preview/commit boundary, authoritative RNG/idempotency/intervention/atomicity/compensation, `ActiveEffect` application decision, and six reserved duration semantics. Validation evidence is completed only after commands and manual review.
- 2026-09-13 — Passed `.\scripts\verify-format.ps1` (`FORMAT-001`), `.\scripts\check-repository-policy.ps1` (repository policy checks), `.\scripts\verify-test-structure.ps1` (`TC-ARCH-001`/`TC-ARCH-002`), and `.\scripts\verify-repository.ps1` (`REPOSITORY-VERIFY PASS`). `verify-docs.ps1` was not run because the script is absent. Runtime tests are not applicable to a documentation-only diff.
- 2026-09-13 — Manual review confirmed the diff is limited to this contract and the new ADR; no backlog status, `ODY-S05-602`+ decomposition, production code, schema, or tests changed.
- 2026-09-13 — Opened Draft PR [#144](https://github.com/odyssey-services/Odyssey_VTT/pull/144). Product-owner ADR acceptance remains required; this task did not merge the PR or change the backlog status.

## 18. Blockers, decisions, and change control

### Blockers

- `ODY-S05-112` is now merged into `origin/main`; its dependency no longer blocks authoring.
- The missing external roadmap is an acceptance blocker unless the product owner confirms that its unavailable content has no further mandatory requirements. The exact request is recorded in `ADR-029` §14; no roadmap content was invented.
- The historical quote contains fifteen labels while task material calls the pipeline fourteen-step. `ADR-029` preserves every named label and asks the product owner to confirm whether this is editorial or a required grouping.

### Decisions made during execution

- 2026-09-13 — Selected Brief plan for the documented reasons in §14. Authority: `PLANS.md` §1.1 and documentation-only scope.
- 2026-09-13 — Kept `ADR-029` status `Proposed`; did not alter `SLICE-05_IMPLEMENTATION_BACKLOG.md` because the contract requires product-owner acceptance first.
- 2026-09-13 — Treated `SLICE-05_BACKLOG.md` §3.2 as a verbatim historical anchor only and `ADR-028` §13/§14 as binding integration boundary; did not infer content from the absent roadmap.

### Approved task changes

- None yet.
