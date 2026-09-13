# ODY-S05-113 — Decompose Full Attack Pipeline Implementation Tasks (602+)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-05, Block 3  
**Owner:** Codex (agent)  
**Branch:** `feat/ody-s05-113-decompose-attack-pipeline`  
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/145 (Draft)  
**Plan:** `docs/plans/active/ODY-S05-113_Decompose_Full_Attack_Pipeline_Implementation_Tasks.md` (Brief plan)

## 1. Goal

Decompose accepted `ADR-029` into implementation tasks `ODY-S05-602`–`608`, without code or individual child contracts.

## 2. Why this task exists

`601` is accepted; future work needs explicit, non-overlapping ownership and dependencies.

## 3. Authorities and requirement references

`AGENTS.md`, `PLANS.md`, task template, `ADR-029` §1/§4–§12, `ADR-028` §12–§15, backlog §10/§11/§16, and `ODY-S05-111` as direct precedent. Requirement IDs: `ODY-S05-113`, `SLICE-05`.

## 4. Verified current state

`git fetch origin` confirmed `origin/main` `daf4e4d` merged PR #144; `ADR-029`/`601` are accepted; no child contracts for `602+` exist.

## 5. Scope

Only this contract, Brief plan, and backlog. No production code/tests/ADR edits/child contracts, Ruleset formulae, or `ADR-028` redesign.

## 6. Technical constraints

Assign accepted decisions only: preview purity, authoritative RNG/idempotency, `ADR-012` atomicity, append-only compensation, and redaction.

## 7. Expected behavior

Seven tasks cover timeline, evaluation, pending/apply, combat durations, combat effects, compensation/projections, and final fixtures; every `ADR-029` §11 rule has named ownership.

## 8. Deliverables

Documentation only: backlog, contract, plan.

## 9. Acceptance criteria

`602`–`608` have boundaries/dependencies; `608` proves §12; §16 stale status is corrected; no code/ADR/child contracts change.

## 10. Tests and validation

Run `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`, and `verify-repository.ps1`; manually verify docs-only diff and §11 coverage.

## 11. Compatibility, migration, and rollback

None; revert the docs branch.

## 12. Dependencies and licensing

None.

## 13. Security, privacy, and hidden information

Public planning docs only; no runtime behavior changes.

## 14. Planning and execution mode

Brief plan: one documentation area, clear path, no runtime/public/persisted/dependency change.

## 15. Documentation and versioning impact

Only backlog, contract, plan. No version change.

## 16. Definition of Done

- [x] Range/boundaries documented.
- [x] Required validation passed.
- [ ] PR evidence recorded.
- [ ] Owner review completes; Codex does not merge.

## 17. Completion evidence

`verify-format`, `check-repository-policy`, `verify-test-structure`, and `verify-repository` passed on 2026-09-13. Draft PR #145 is open. No child contracts created; final diff is documentation-only.

## 18. Blockers, decisions, and change control

### Blockers

None.

### Decisions made during execution

- 2026-09-13 — Brief plan under `PLANS.md` §1.1.
- 2026-09-13 — Seven tasks use the independent ADR timeline, evaluation, apply, duration, effect, compensation/projection, and fixture seams.
- 2026-09-13 — Child contracts remain deferred, matching `ODY-S05-111`.

### Approved task changes

None.
