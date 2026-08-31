# ODY-S04-002 — ADR Character Drafts, Templates, and Approval Workflow

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-002-adr-drafts-templates-approval`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-08-30 UTC

## 1. Purpose and user-visible outcome

Accept `ADR-023`, which fixes the local-Draft-vs-campaign-authoritative-Character boundary, `PersonalCharacterTemplate`/`CampaignCharacterTemplate` storage/lifecycle and independent-copy mechanism, template compatibility validation, and the minimum submit/review/comment/approve command/event flow, before `SLICE-04` implementation decomposition begins for this concern.

## 2. Task contract

- Goal: create accepted `ADR-023` and task evidence for `ODY-S04-002`.
- Acceptance criteria: ADR exists, answers all four required questions, includes alternatives, excludes future `ADR-024`/`ADR-025` scopes and `AssistantGM`/delegation, does not contradict `ADR-019`, updates `SLICE-04_BACKLOG.md`, and passes required validation.
- Requirement IDs: `ODY-S04-002`, `ADR-023`, `SLICE-04` prerequisite backlog item 2.
- In scope: `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md`, task contract, this ExecPlan, and `SLICE-04_BACKLOG.md` status row.
- Out of scope: production code, tests, schema, DTOs, Unity UI, Character aggregate boundary (`ADR-022`, already Accepted), DevelopmentPool/progression economy, ownership/lifecycle/ruleset migration operations, ability/resource/anatomy mechanics, `AssistantGM`/delegation.
- Required authorities: `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`, `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`, `docs/tasks/SLICE-04_BACKLOG.md` §3.2, roadmap §13.4/§13.8/§13.9, Character/Progression §7–9/§20/§26–28, `ADR-002`, `ADR-003`, `ADR-019`, `ADR-022` (full read), and style precedent `ADR-022`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `cdaeb9c`, which includes PR #80 (`ODY-S04-001`/`ADR-022`, Accepted).
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-002` as the second prerequisite ADR task, depending on `ODY-S04-001`, and future `ADR-023`.
- `ADR-022` is fully read and provides the aggregate/revision/history boundary this ADR must join without redefining.
- `ADR-019` is fully read (relevant sections) and confirms the three-role baseline with `AssistantGM`/delegation explicitly deferred — checked directly to avoid contradicting it in the approval-role decision.
- No `ADR-023` file exists before this task.

Assumptions: none.

## 4. Proposed approach

Write a documentation-only ADR that specializes the already-accepted substrates instead of redefining them:

- draw the local-Draft-vs-campaign-Character boundary at `BindDraftToCampaign`, reusing the exact `ADR-022` aggregate from that point onward (one `CharacterId`, no parallel Draft aggregate type);
- model `PersonalCharacterTemplate`/`CampaignCharacterTemplate` as one `CharacterTemplate` aggregate distinguished by `TemplateScope`, per the product's own single schema;
- specify the independent-copy mechanism concretely (deep value copy, fresh nested identifiers, immutable provenance) so `CAP-INV-006` holds architecturally, not just declaratively;
- fix compatibility validation and `RulesetVersion` pinning at `BindDraftToCampaign`, deferring only Ruleset *migration* to `ADR-025`;
- fix the minimum command/event set exactly as product §27/§28 already names it, with no invented `Reject`/`ChangesRequested` mechanism;
- confirm `Character.Approve` stays MainGM-only under `ADR-019`'s existing baseline, without extending that ADR;
- update the parent backlog row to `Done`;
- create a task contract with validation evidence.

No production code, schema, tests, dependencies, or private product prose enters the repository.

## 5. Milestones

### M1 — ADR and contract authored

- [x] Create `ADR-023` with decisions, alternatives, exclusions, traceability, and normative action.
- [x] Create `ODY-S04-002` task contract with all 18 sections.
- [x] Update `SLICE-04_BACKLOG.md` row for `ODY-S04-002`.

### M2 — Validation and review readiness

- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-08-30 — Preflight confirmed `origin/main` includes PR #80 at `cdaeb9c`; created branch `feat/ody-s04-002-adr-drafts-templates-approval`.
- 2026-08-30 — Read roadmap §13.4/§13.8/§13.9, Character/Progression §7–9/§20/§26–28 in full, `ADR-022` in full, `ADR-002` in full, `ADR-003` §1–4, `ADR-019` §5/§9/§10/§14.1, and `SLICE-04_BACKLOG.md` §3.2.
- 2026-08-30 — Authored `ADR-023` resolving all four required questions plus considered alternatives.
- 2026-08-30 — Authored task contract and this ExecPlan; updated `SLICE-04_BACKLOG.md` row.
- 2026-08-30 — Local validation: `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` (see section 9).

## 7. Decisions

- 2026-08-30 — Decision: use ExecPlan. Rationale: this ADR changes future public domain/persistence/permission contracts, matching `ODY-S04-001`'s own precedent and `SLICE-04_BACKLOG.md`'s "ExecPlan expected" note. Authority: `PLANS.md` §1.
- 2026-08-30 — Decision: local Draft is not an `ADR-022` aggregate instance; the boundary is `BindDraftToCampaign`. Rationale: product §7.3/§8.3/§27 distinguish local creation from campaign binding as separate steps/commands, and local data may be lost without campaign-side durability implications. Authority: `ADR-023` §4.
- 2026-08-30 — Decision: no technical spike. Rationale: all remaining questions are model/contract decisions over already-accepted substrates (`ADR-002`, `ADR-003`, `ADR-019`, `ADR-022`). Authority: `docs/tasks/SLICE-04_BACKLOG.md` §5.

## 8. Discoveries and deviations

- Product §26 does not list `Character.Approve` among its explicit MainGM-only bullet list, and separately mentions AssistantGM delegation — cross-checking `ADR-019` directly (not just the product doc) was necessary to correctly conclude `Character.Approve` is MainGM-only under the *currently accepted* baseline, without either contradicting `ADR-019` or silently deciding a delegation model `ADR-019` itself left open. This is recorded explicitly in `ADR-023` §7.3 rather than smoothed over.
- Adding this ExecPlan creates one additional docs file beyond the brief's expected three-file diff; consistent with `ODY-S04-001`'s own precedent, required by `PLANS.md` because the ADR affects future public contracts.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.

## 10. Recovery and rollback

Rollback is a normal docs-only revert of this branch/PR. No production code, schema, migration, assets, dependencies, or generated artifacts are created.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending.
