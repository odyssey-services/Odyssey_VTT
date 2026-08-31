# ODY-S04-002 — ADR Character Drafts, Templates, and Approval Workflow

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-002-adr-drafts-templates-approval`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md`
**Created:** 2026-08-30
**Last updated:** 2026-08-30 UTC

## 1. Goal

Accept `ADR-023 — Character Drafts, Templates, and Approval Workflow`, resolving the local-Draft-vs-campaign-authoritative-Character boundary, `PersonalCharacterTemplate`/`CampaignCharacterTemplate` storage/lifecycle and independent-copy mechanism, template compatibility validation, and the minimum submit/review/comment/approve command/event flow.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_BACKLOG.md` §3.2 identifies Drafts/templates/approval as the second prerequisite ADR gap — `ADR-002` can carry the commands once the model is known and `ADR-003` can serialize the resulting contracts, but neither decides the architectural split between local profile storage and campaign-authoritative state, nor the template-compatibility boundary that turns local data into a campaign Character.
- Value or risk reduction: prevents implementation tasks from inventing incompatible Draft/template storage boundaries, a live/lazy template reference that would violate `CAP-INV-006`, or an ad hoc submit/review/approve state machine beyond what the product specification already fixes.
- Blocking or enabling relationship: depends on `ODY-S04-001` (`ADR-022`, Accepted) because Draft approval creates a campaign Character and must know the target aggregate/revision/history boundary. Enables `ODY-S04-004` (ownership/lifecycle interacts with approval/template lifecycle); `SLICE-04` implementation remains blocked until `ADR-024`/`ADR-025` are also accepted.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/SLICE-04_BACKLOG.md` §3.2.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.4 (Drafts and templates), §13.8 (vertical slice steps 1–5), §13.9 (independent-template-copy exit criterion).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7 (lifecycle, `ApprovalState`, Draft/Active), §8 (player character creation), §9 (templates, `TemplateScope`, independent copy), §20 (editing/concurrency — confirmed unchanged, not reopened), §26–28 (permissions/commands/domain events).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (full read — the aggregate/revision/history boundary this ADR must join, not redefine).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (full read — command/event/idempotency model reused).
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` (relevant sections — versioned DTO contract reused).
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (relevant sections — three-role baseline reused, not redefined).
- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md`/`ADR-022` as ADR format/depth precedents.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-002`, `ADR-023`, `SLICE-04` prerequisite backlog item 2.
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, local private path outside the repository, secret, personal data, or hidden campaign content is copied into this task, the plan, or the ADR.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, and `git merge --ff-only origin/main` advanced local `main` to `cdaeb9c`, the merge commit for PR #80 (`ODY-S04-001`/`ADR-022`).
- `git log --oneline -10` confirmed PR #80 is in `main` and contains `ADR-022`.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-002` as the second prerequisite task, depending on `ODY-S04-001`, with future `ADR-023`.
- No `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` existed before this task.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7.2 states `Submitted`/`ChangesRequested`/`Rejected` are not stable `ApprovalState` values for MVP — only `Draft`/`Approved` exist; review feedback is represented by commands/comments while the Character remains `Draft`.
- §27's command list already names `CreateLocalCharacterDraft`, `BindDraftToCampaign`, `SubmitCharacterDraft`, `AddCharacterReviewComment`, `ApproveCharacterDraft` as five distinct commands (not fewer) — confirming a local-creation step and a campaign-binding step are already product-distinguished, not merged into one command.
- §26 does not list `Character.Approve` among its explicit MainGM-only-in-MVP bullet list, but separately states approval "may be delegated to AssistantGM if CampaignPolicy allows it."
- `ADR-019` §5/§10/§14.1 confirms the accepted baseline has exactly three roles (`MainGM`/`Player`/`Observer`), no `AssistantGM`, and explicitly defers delegation (`PERM-INV-009`) — cross-checked directly against `ADR-023`'s own role decision (section 7.3) to avoid contradicting `ADR-019`.
- `ADR-022` §10 explicitly names `ADR-023` as the deferred scope for Draft/template/approval architecture — confirming this task's scope boundary against the already-Accepted ADR, not just the backlog.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep` during this task.

## 5. Scope

### In scope

- Create `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md`.
- Create this task contract.
- Create an ExecPlan for the ADR task.
- Update the `ODY-S04-002` row in `docs/tasks/SLICE-04_BACKLOG.md` to `Done` and point to `ADR-023`.
- Run documentation-only validation.

### Out of scope

- Character aggregate boundary/section locks/history (`ADR-022`, already Accepted, not reopened).
- Development economy/points/purchases/critical evidence/respec (`ODY-S04-003`/future `ADR-024`).
- Ownership/lifecycle operations/Dead/restore/physical delete/Ruleset migration (`ODY-S04-004`/future `ADR-025`).
- Ability/resource/anatomy mechanics (already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4).
- Any concrete UI for the approval screen, review-comment thread, or template picker.
- Any `AssistantGM` role or approval-delegation mechanism (remains `ADR-019`'s own future amendment scope).
- Any production code, tests, persistence schema, Unity assets, or DTO implementation.

### Allowed paths

```text
docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md
docs/plans/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md
docs/tasks/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md
docs/tasks/SLICE-04_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-022*
```

## 6. Technical constraints

- Module ownership and dependency direction: future implementation must keep local-Draft/`CharacterTemplate` invariants and copy semantics in Domain, command orchestration/compatibility validation in Application, physical storage in Persistence, delivery in Networking, and local forms/template pickers in Unity Client per `ADR-001`.
- Authoritative-state and transaction boundary: every campaign-bound command in this ADR is an ordinary `ADR-022` Character command reusing its section revisions/event snapshots; no parallel aggregate or history mechanism.
- Serialization / compatibility boundary: local-Draft payload and `CharacterTemplate` seed data remain explicit/versioned DTOs under `ADR-003`; no direct Domain aggregate serialization.
- Time / RNG rule: Not applicable — no clock/RNG-dependent decision in this ADR.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no new dependency, tool, action, or package.
- Security / privacy / redaction rule: `Character.Approve` permission-check reuses `ADR-019`'s existing three-role baseline; no new role or delegation mechanism introduced.
- Performance or platform constraint: Not applicable.
- Other: do not solve future `ADR-024`/`ADR-025` scopes inside `ADR-023`; do not extend or redefine `ADR-019`'s role model.

## 7. Expected behavior

### Scenario 1 — Local Draft vs campaign Character boundary is reviewable

**Given** `SLICE-04_BACKLOG.md` §3.2 identifies the local-Draft-vs-campaign-authoritative boundary as a prerequisite gap
**When** `ADR-023` is reviewed
**Then** it states a local, non-`ADR-022`-aggregate Draft before campaign binding, and exactly one permanent `ADR-022` Character aggregate instance from `BindDraftToCampaign` onward through Approve.

### Scenario 2 — Template independence is architectural, not declarative

**Given** `CAP-INV-006` requires a template to not change an already-created Character
**When** `ADR-023` is reviewed
**Then** it specifies a deep value copy with freshly minted nested identifiers and immutable `TemplateId`/`TemplateVersion` provenance, with no code path that re-resolves a live template reference for an existing Character.

### Required invariants

- All four task-required questions (§4 of the ТЗ) are answered explicitly and separately.
- `ADR-023` reuses `ADR-002`, `ADR-003`, `ADR-019`, and `ADR-022` without redefining them.
- No code/schema/test implementation is introduced.
- No contradiction with `ADR-019`'s accepted three-role baseline.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `ADR-023`, this task contract, ExecPlan, and `SLICE-04_BACKLOG.md` row update.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` exists and is `Accepted`.
2. ADR answers the four task-required questions separately: local-Draft-vs-campaign-Character boundary, template lifecycle/independent-copy mechanism, compatibility validation, and submit/review/comment/approve flow with roles.
3. ADR includes considered alternatives for at least: Draft as a separate aggregate type vs a state of the `ADR-022` aggregate; deep copy vs lazy template reference; synchronous vs deferred compatibility validation.
4. ADR explicitly excludes the Character aggregate boundary (`ADR-022`), development economy (`ADR-024`), ownership/lifecycle/ruleset migration (`ADR-025`), ability/resource/anatomy mechanics, `AssistantGM`/delegation, concrete UI, and code/schema/test implementation.
5. ADR's role decisions do not contradict `ADR-019`'s accepted three-role baseline.
6. This task contract exists with all 18 numbered sections.
7. ExecPlan exists because `PLANS.md` §1 requires it for future public contract/authoritative state ADR work, consistent with `ODY-S04-001`'s own precedent.
8. `docs/tasks/SLICE-04_BACKLOG.md` marks `ODY-S04-002` as `Done` and points to `ADR-023`.
9. Diff contains only documentation files under `docs/adr`, `docs/plans`, and `docs/tasks`.
10. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` pass.
11. `ADR-023` §15 (Normative action) contains no premature claim of product-owner approval/sign-off — only the same task-acceptance rationale pattern `ADR-021`/`ADR-022` use after their own correction.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR task; replacement evidence is repository formatting and policy validation plus PR review.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner reviews `ADR-023` before `ODY-S04-004` (which depends on it) proceeds.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: PowerShell validation only.

### Validation not required by this task

- `dotnet build`, `dotnet test`, `test-unity`, `build-dev`, migration rehearsal, and player smoke are not required because no code, test, Unity, schema, package, or CI file changes. No empirical unknown was discovered during analysis that would require a spike.

## 11. Compatibility, migration, and rollback

- Compatibility impact: future architectural contract only; no persisted state changes in this PR.
- Version fields affected: `ADR-023` document version introduced as `1.0`; no application/schema/contract/protocol/ruleset version changes.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this docs-only PR.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: task-safe summaries of private product documentation, public repository ADR/task paths, and future Draft/template/approval architecture.
- Trust boundaries: private product docs are read-only and not copied verbatim into tracked files.
- Authorization / audience checks: no implementation; ADR requires `Character.Approve` to remain MainGM-only under `ADR-019`'s existing baseline, and requires no new permission constant for review comments beyond what product §26 already names.
- Redaction requirements: no private excerpts, secrets, credentials, personal data, or hidden campaign content in commits/PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `PLANS.md` §1 requires an ExecPlan because the ADR changes future public domain/persistence/permission contracts and affects authoritative Draft/template/approval semantics that later implementation tasks must follow. This matches `ODY-S04-001`'s own precedent for the immediately preceding ADR in the same prerequisite series, and `SLICE-04_BACKLOG.md`'s own "ExecPlan expected" note for this task.
- ExecPlan path: `docs/plans/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: `ODY-S04-004` depends on this ADR (approval/template lifecycle interaction). `SLICE-04` implementation backlog still waits for `ADR-024` and `ADR-025` as well.

## 15. Documentation and versioning impact

- Documents that must change: `ADR-023`, this task contract, ExecPlan, `SLICE-04_BACKLOG.md`.
- Documents that must not change: `ADR-001` through `ADR-022`, private `Documentation/` sources, production code, tests, scripts, Unity assets.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none implemented; future Draft/template/approval command/event/DTO contract guidance is documented in the ADR only.
- Documentation version changes: `ADR-023` introduced as v1.0.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are explicitly not applicable.
- [x] Required manual checks are completed or assigned to owner review.
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

- `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` — new Character Draft/template/approval ADR.
- `docs/plans/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md` — ExecPlan for this ADR task.
- `docs/tasks/active/ODY-S04-002_ADR_Character_Drafts_Templates_Approval_Workflow.md` — this task contract.
- `docs/tasks/SLICE-04_BACKLOG.md` — marks `ODY-S04-002` as complete.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; includes required repository structure, forbidden tracked patterns, LFS policy, ErrorCode registry, workflow policy, and static Unity project/package/toolchain checks. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-023` file exists and status is `Accepted`. |
| AC-2 | Passed | ADR sections 4–7 answer all four required questions separately. |
| AC-3 | Passed | ADR section 12 records the three required alternatives plus a fourth (no `Reject` command). |
| AC-4 | Passed | ADR section 8 excludes future ADR scopes, `AssistantGM`/delegation, concrete UI, and implementation work. |
| AC-5 | Passed | ADR section 7.3 confirms `Character.Approve` is MainGM-only as a consequence of `ADR-019`'s baseline, not a redefinition of it. |
| AC-6 | Passed | This contract contains all 18 numbered sections. |
| AC-7 | Passed | ExecPlan exists under `docs/plans/active`. |
| AC-8 | Passed | `SLICE-04_BACKLOG.md` row updated for `ODY-S04-002`. |
| AC-9 | Passed | Diff scope is docs-only. |
| AC-10 | Passed | Required validation commands passed locally. |
| AC-11 | Passed | ADR section 15 states only task-acceptance rationale (no spike needed), no product-owner sign-off claim. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No production implementation is included. `ADR-024` and `ADR-025` remain open prerequisite tasks.
- `ADR-023` intentionally does not resolve `AssistantGM`-delegated approval — it remains blocked on a future `ADR-019` amendment.

### Follow-up tasks

- `ODY-S04-003` — `ADR-024` Development Economy and Progression Transactions.
- `ODY-S04-004` — `ADR-025` Character Ownership, Lifecycle, and Ruleset Migration Operations.
- A future `ADR-019` amendment to introduce `AssistantGM`/delegation, if the product owner decides delegated approval is needed before general availability — not scheduled by this task.

### Self-review summary

- Scope review: limited to allowed documentation files; no `ADR-022` or `ADR-019` redefinition.
- Architecture review: ADR reuses `ADR-002`/`003`/`019`/`022`; no replacement substrate introduced; local Draft vs campaign Character boundary is drawn at `BindDraftToCampaign`, matching product §7.3's plain reading.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied; `Character.Approve` remains MainGM-only, consistent with `ADR-019`.
- Documentation/version review: `ADR-023` v1.0 introduced; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.
- `SLICE-04` implementation remains blocked until `ADR-024` and `ADR-025` are also accepted.

### Decisions made during execution

- 2026-08-30 — Decision: draw the local-Draft-vs-campaign-Character boundary at `BindDraftToCampaign`, not at `ApproveCharacterDraft` — Authority/approval: `ADR-023` §4, product §7.3's "permanent CharacterId if the draft was local" and §27's five distinct commands (`CreateLocalCharacterDraft` separate from `BindDraftToCampaign`).
- 2026-08-30 — Decision: model `PersonalCharacterTemplate`/`CampaignCharacterTemplate` as one `CharacterTemplate` aggregate distinguished by `TemplateScope`, not two aggregate types — Authority/approval: product §9.1's own single schema definition already including a `TemplateScope` field.
- 2026-08-30 — Decision: independent copy is a deep value copy with freshly minted nested identifiers and immutable `TemplateId`/`TemplateVersion` provenance, never a live/lazy template reference — Authority/approval: `CAP-INV-006`, product §9.3's "no live binding, not changed by later template edits."
- 2026-08-30 — Decision: compatibility validation and `RulesetVersion` pinning occur synchronously at `BindDraftToCampaign`, not deferred to `ApproveCharacterDraft` — Authority/approval: product §8.1's own step ordering (host validation precedes submission, which precedes GM review/approve) and `CAP-INV-010`.
- 2026-08-30 — Decision: no `RejectCharacterDraft` command or `ChangesRequested` state is introduced — Authority/approval: product §7.2's explicit statement that these are not stable Character states for MVP.
- 2026-08-30 — Decision: `Character.Approve` remains MainGM-only under `ADR-019`'s currently accepted baseline; `AssistantGM`-delegated approval is explicitly not decided or implemented here — Authority/approval: `ADR-019` §5/§10/§14.1's own accepted three-role model and explicit deferral of `AssistantGM`/delegation, cross-checked directly to avoid contradicting an already-Accepted ADR.
- 2026-08-30 — Decision: `AddCharacterReviewComment` requires no `ExpectedCharacterRevision`/section revision and no new `ADR-022` section-lock key — Authority/approval: product §8.4 ("comment does not silently change the Draft"), treated as a conflict-free append analogous to `ADR-002` §17.1's `GameLogEntry`.

### Approved task changes

- None.
