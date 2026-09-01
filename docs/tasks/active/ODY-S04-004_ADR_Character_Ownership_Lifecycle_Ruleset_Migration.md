# ODY-S04-004 — ADR Character Ownership, Lifecycle, and Ruleset Migration Operations

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-004-adr-ownership-lifecycle-migration`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md`
**Created:** 2026-08-31
**Last updated:** 2026-08-31 UTC

## 1. Goal

Accept `ADR-025 — Character Ownership, Lifecycle, and Ruleset Migration Operations`, resolving Character-specific owner/co-owner/controller semantics over `ADR-019`'s baseline, archive/dependency-aware physical delete, Dead/`CharacterRestored` invariants, and Character Ruleset migration preview/snapshot/rollback (including its boundary with `ADR-013` and its interaction with `.odchar` import).

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_BACKLOG.md` §3.5 identifies ownership/lifecycle/Ruleset-migration operations as the fourth and final prerequisite ADR gap — `ADR-019` deliberately fixed only a simplified role/character-assignment baseline and explicitly deferred the fuller ownership/control model; `ADR-002`/`ADR-012`/`ADR-013` cover generic commands, append-only events, compensation, and database schema migration, but none decides Character-specific owner/co-owner/controller semantics, dependency-aware physical delete, Dead/restore invariants, or the relationship between database migration and Character Ruleset migration.
- Value or risk reduction: prevents implementation tasks from silently expanding `ADR-019`'s role model, inventing a parallel history mechanism for deleted Characters, allowing an ordinary command to set `Dead`, or routing Character Ruleset migration through `ADR-013`'s schema-migration runner.
- Blocking or enabling relationship: depends on `ODY-S04-001` (`ADR-022`, lifecycle/identity/history), `ODY-S04-002` (`ADR-023`, approval/template-lifecycle interaction for `.odchar` import), and `ODY-S04-003` (`ADR-024`, respec/progression-compensation pattern reused for migration revert) — all three Accepted and on `main`. This is the last of the four `SLICE-04` prerequisite ADRs; its acceptance closes `SLICE-04_BACKLOG.md` §2's exit criteria.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/SLICE-04_BACKLOG.md` §3.5.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.7 (ownership and lifecycle operations), §13.9 (owner-assignment-audit, Archive/Dead-history-preservation, `.odchar`-import-new-Draft, failed-Ruleset-migration-rollback exit criteria).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-007`/`008`/`010`), §19 (ownership/control), §22 (archive/physical delete/historical identity), §23 (Dead/`CharacterRestored`), §24 (`.odchar` export/import, Draft-creation aspect), §25 (Ruleset migration), §26–28 (permissions/commands/domain events).
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (full read — the three-role baseline and its explicit deferral of `AssistantGM`/delegation/full ownership-control model, not redefined here).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (full read — already-reserved `Ownership`/`Lifecycle` sections/locks/revisions and historical-event-snapshot minimum this ADR fills in, not redefines).
- `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` (relevant sections — the local-Draft/`BindDraftToCampaign`/compatibility-validation pipeline `.odchar` import reuses).
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` (relevant sections — the compensating-batch pattern reused for post-commit Ruleset-migration reversal).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (relevant sections — compensating-event mechanism and snapshot/`BackupRecord` contract).
- `docs/adr/ADR-013_Migration_Runner_v1.0.md` (full read, especially §9 — the explicit database-schema-vs-ruleset-migration boundary this ADR fills the "separate ADR" gap for).
- `docs/adr/ADR-022_*`/`ADR-023_*`/`ADR-024_*` as ADR format/depth precedents.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-004`, `ADR-025`, `SLICE-04` prerequisite backlog item 4 (final).
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, local private path outside the repository, secret, personal data, or hidden campaign content is copied into this task, the plan, or the ADR.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, and `git merge --ff-only origin/main` advanced local `main` to `be28562`, the merge commit for PR #82 (`ODY-S04-003`/`ADR-024`, Accepted).
- `gh pr view 82 --json state,mergedAt,mergeCommit` confirmed `state: MERGED`, and `git merge-base --is-ancestor be28562... origin/main` confirmed the merge commit is a real ancestor of `origin/main` — not merely a GitHub-reported status on an unmerged branch (the explicit precaution this task's own preflight required, per the `SLICE-UI-01` lesson).
- `git log --oneline -10` confirmed PR #82 is in `main` and contains `ADR-024`.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-004` as the fourth and final prerequisite task, depending on `ODY-S04-001`, `ODY-S04-002`, and `ODY-S04-003`, with future `ADR-025`.
- No `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` existed before this task.
- `ADR-019` §10/`ADR-019`'s `PERM-INV-007`/`008` rows explicitly defer the full ownership/control model (co-owners, temporary control grant, transfer workflow) as future scope not covered by that ADR's own baseline — confirming this ADR is the intended, explicitly-anticipated place to close that gap for Character specifically, not an unapproved expansion of `ADR-019`.
- `ADR-022` §5/§6 already reserve `OwnershipRevision`/`Ownership` and `LifecycleRevision`/`Lifecycle` as section revisions/lock keys — confirming this ADR needs zero new section-lock primitives for any of its four questions.
- `ADR-022` §7 already requires Character-significant event snapshots to remain renderable "even if... a dependency is physically removed according to a future approved operation" — confirming physical delete's historical-identity question is already architecturally anticipated by `ADR-022`, not a gap this ADR must invent new machinery for.
- `ADR-013` §9 explicitly states the database-schema-vs-ruleset-migration boundary and explicitly names that Character Ruleset migration workflow "must be defined by a separate ADR or task contract when Rules Engine/Content Domain reach that stage" — confirming this ADR is exactly the anticipated separate ADR, and that the one mandatory integration point is reusing `ADR-012`'s snapshot/`BackupRecord` mechanism if a backup is taken at all.
- Product §26 does not name a distinct permission constant for granting Character control separately from `Character.ManageOwnership` — this task resolves that gap conservatively (section 4.3 of the ADR) rather than inventing a new delegation-capable permission.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh` during this task.

## 5. Scope

### In scope

- Create `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`.
- Create this task contract.
- Create an ExecPlan for the ADR task.
- Update the `ODY-S04-004` row in `docs/tasks/SLICE-04_BACKLOG.md` to `Done` and point to `ADR-025`.
- Run documentation-only validation.

### Out of scope

- Character aggregate boundary/section locks/history (`ADR-022`, already Accepted, not reopened).
- Local Draft vs campaign Character, templates, submit/review/approve (`ADR-023`, already Accepted, not reopened) — `.odchar` import reuses its pipeline unmodified.
- Development economy/purchases/respec mechanics themselves (`ADR-024`, already Accepted, not reopened) — only its compensating-batch pattern is reused.
- Ability/resource/anatomy mechanics (already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4).
- The `.odchar` file format itself (structure of `manifest.json`/`character.json`/`portrait/`/`referenced-assets/`).
- Any extension of `ADR-019`'s role model (`AssistantGM`, delegation, new permission constants beyond `Character.ManageOwnership`'s already-stated scope).
- Any concrete UI for ownership management, delete confirmation, restore, or Ruleset-migration preview screens.
- Any production code, tests, persistence schema, Unity assets, or DTO implementation.

### Allowed paths

```text
docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md
docs/plans/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md
docs/tasks/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md
docs/tasks/SLICE-04_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-024*
```

## 6. Technical constraints

- Module ownership and dependency direction: future implementation must keep `CharacterOwnership`/lifecycle-transition/Ruleset-migration invariants in Domain, `FatalDamagePending`/Ruleset-mapping computation in Rules, command orchestration/dependency checks in Application, physical storage in Persistence, delivery in Networking, and management/preview UI in Unity Client per `ADR-001`.
- Authoritative-state and transaction boundary: every command in this ADR is an ordinary `ADR-022` Character command operating inside the already-reserved `Ownership`/`Lifecycle` sections, committed in one `ADR-012` transaction; physical delete never deletes `DomainEvents`; Ruleset migration reuses `ADR-024`'s compensating-batch pattern for post-commit reversal, never a third mechanism.
- Serialization / compatibility boundary: ownership/migration payloads remain explicit/versioned DTOs under `ADR-003`; no direct Domain aggregate serialization (not reopened here, referenced only).
- Time / RNG rule: Not applicable — no clock/RNG-dependent decision in this ADR (host clock/actor/time fields reuse `ADR-002`'s existing envelope).
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no new dependency, tool, action, or package.
- Security / privacy / redaction rule: no new permission constant introduced; `Character.ManageOwnership`/`Character.Archive`/`Character.DeletePermanently`/`Character.RestoreDead`/`Character.MigrateRuleset` (product §26) are reused as-is under `ADR-019`'s existing role model.
- Performance or platform constraint: Not applicable.
- Other: do not extend `ADR-019`'s role model; do not route Character Ruleset migration through `ADR-013`'s schema-migration runner.

## 7. Expected behavior

### Scenario 1 — Ownership/control semantics are reviewable without expanding `ADR-019`

**Given** `SLICE-04_BACKLOG.md` §3.5 identifies Character ownership/control semantics as a prerequisite gap `ADR-019` deliberately left open
**When** `ADR-025` is reviewed
**Then** it specializes `ADR-019`'s baseline into concrete `CharacterOwnership` semantics inside `ADR-022`'s already-reserved `Ownership` section, without adding a role, delegation mechanism, or new section-lock primitive.

### Scenario 2 — Physical delete preserves history without a parallel mechanism

**Given** roadmap §13.9's "Archive and Dead preserve history" exit criterion
**When** `ADR-025` is reviewed
**Then** it specifies that physical delete removes only live state, never `DomainEvents`, and that `CharacterHistoryProjection` continues to render historical entries purely from `ADR-022`'s already-required event snapshots.

### Required invariants

- All four task-required questions (§4 of the ТЗ) are answered explicitly and separately.
- `ADR-025` reuses `ADR-002`, `ADR-012`, `ADR-013`, `ADR-019`, `ADR-022`, `ADR-023`, and `ADR-024` without redefining any of them.
- No code/schema/test implementation is introduced.
- No contradiction with `ADR-019`'s accepted three-role baseline or `ADR-013`'s schema-migration-runner boundary.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `ADR-025`, this task contract, ExecPlan, and `SLICE-04_BACKLOG.md` row update.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` exists and is `Accepted`.
2. ADR answers the four task-required questions separately: owner/co-owner/controller semantics over `ADR-019`, archive/dependency-aware physical delete, Dead/`CharacterRestored` invariants, and Ruleset migration preview/snapshot/rollback with the `ADR-013` boundary and `.odchar`-import interaction.
3. ADR includes considered alternatives for at least: direct MainGM primary-owner assignment vs. a new-owner-confirmation workflow; dependency-checked physical delete vs. soft-delete-only; Character Ruleset migration as an independent mechanism vs. reusing `ADR-013`'s schema migration runner directly.
4. ADR explicitly excludes the Character aggregate boundary (`ADR-022`), Draft/template/approval (`ADR-023`), development economy (`ADR-024`), ability/resource/anatomy mechanics, the `.odchar` file format itself, and any `ADR-019` role/permission extension.
5. ADR does not contradict `ADR-019`'s three-role baseline or `ADR-013`'s database-schema-migration boundary.
6. This task contract exists with all 18 numbered sections.
7. ExecPlan exists because `PLANS.md` §1 requires it for future public contract/authoritative state ADR work, consistent with `ODY-S04-001`/`002`/`003`'s own precedent.
8. `docs/tasks/SLICE-04_BACKLOG.md` marks `ODY-S04-004` as `Done` and points to `ADR-025`.
9. Diff contains only documentation files under `docs/adr`, `docs/plans`, and `docs/tasks`.
10. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` pass.
11. `ADR-025` §15 (Normative action) contains no premature claim of product-owner approval/sign-off — only the same task-acceptance rationale pattern `ADR-022`/`ADR-023`/`ADR-024` use.
12. `ADR-025`'s acceptance is confirmed to close all four `SLICE-04_BACKLOG.md` §2 prerequisite exit criteria.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR task; replacement evidence is repository formatting and policy validation plus PR review.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner reviews `ADR-025` and, on acceptance, confirms `SLICE-04_BACKLOG.md`'s prerequisite revision is complete before any future `SLICE-04` implementation backlog is opened.

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
- Version fields affected: `ADR-025` document version introduced as `1.0`; no application/schema/contract/protocol/ruleset version changes.
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

- Data classes handled: task-safe summaries of private product documentation, public repository ADR/task paths, and future ownership/lifecycle/Ruleset-migration architecture.
- Trust boundaries: private product docs are read-only and not copied verbatim into tracked files.
- Authorization / audience checks: no implementation; ADR reuses existing `Character.ManageOwnership`/`Archive`/`DeletePermanently`/`RestoreDead`/`MigrateRuleset` permissions (product §26) without introducing a new permission constant or delegation pathway.
- Redaction requirements: no private excerpts, secrets, credentials, personal data, or hidden campaign content in commits/PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `PLANS.md` §1 requires an ExecPlan because the ADR changes future public domain/persistence/permission contracts and affects authoritative ownership/lifecycle/Ruleset-migration semantics that later implementation tasks must follow. This matches `ODY-S04-001`/`002`/`003`'s own precedent for the three preceding ADRs in the same prerequisite series, and `SLICE-04_BACKLOG.md`'s own "ExecPlan expected" note for this task.
- ExecPlan path: `docs/plans/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: this is the fourth and final `SLICE-04` prerequisite ADR. Its acceptance closes `SLICE-04_BACKLOG.md` §2's exit criteria; `SLICE-04`'s own vertical-slice implementation backlog is a separate, not-yet-started future task, not part of this task's own scope.

## 15. Documentation and versioning impact

- Documents that must change: `ADR-025`, this task contract, ExecPlan, `SLICE-04_BACKLOG.md`.
- Documents that must not change: `ADR-001` through `ADR-024`, private `Documentation/` sources, production code, tests, scripts, Unity assets.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none implemented; future ownership/lifecycle/Ruleset-migration command/event/DTO contract guidance is documented in the ADR only.
- Documentation version changes: `ADR-025` introduced as v1.0.
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

- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` — new ownership/lifecycle/Ruleset-migration ADR.
- `docs/plans/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md` — ExecPlan for this ADR task.
- `docs/tasks/active/ODY-S04-004_ADR_Character_Ownership_Lifecycle_Ruleset_Migration.md` — this task contract.
- `docs/tasks/SLICE-04_BACKLOG.md` — marks `ODY-S04-004` as complete.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `git merge-base --is-ancestor <PR #82 merge commit> origin/main` | Passed | Confirmed the merge commit is a real ancestor, not merely a GitHub-reported status. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; includes required repository structure, forbidden tracked patterns, LFS policy, ErrorCode registry, workflow policy, and static Unity project/package/toolchain checks. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-025` file exists and status is `Accepted`. |
| AC-2 | Passed | ADR sections 4–7 answer all four required questions separately. |
| AC-3 | Passed | ADR section 12 records the three required alternatives. |
| AC-4 | Passed | ADR section 8 excludes `ADR-022`/`ADR-023`/`ADR-024` scope, ability/resource/anatomy mechanics, the `.odchar` file format itself, and `ADR-019` role/permission extension. |
| AC-5 | Passed | ADR sections 4/6/7.1 explicitly reuse `ADR-019`'s baseline and `ADR-013`'s schema-migration boundary without redefining either. |
| AC-6 | Passed | This contract contains all 18 numbered sections. |
| AC-7 | Passed | ExecPlan exists under `docs/plans/active`. |
| AC-8 | Passed | `SLICE-04_BACKLOG.md` row updated for `ODY-S04-004`. |
| AC-9 | Passed | Diff scope is docs-only. |
| AC-10 | Passed | Required validation commands passed locally. |
| AC-11 | Passed | ADR section 15 states only task-acceptance rationale (no spike needed), no product-owner sign-off claim. |
| AC-12 | Passed | ADR section 14's closing line and this task's own report confirm all four `SLICE-04_BACKLOG.md` §2 prerequisite ADRs (`ADR-022`–`ADR-025`) are `Accepted`. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No production implementation is included. `SLICE-04`'s own vertical-slice implementation backlog (decomposition into `ODY-S04-XXX` feature tasks) has not yet been created — that is explicitly a separate future task, not part of this prerequisite backlog revision.
- `ADR-025` intentionally does not decide the `.odchar` file format itself, or any `ADR-019` role/delegation extension — both remain explicitly deferred.

### Follow-up tasks

- A future `SLICE-04` vertical-slice implementation backlog, decomposing local Draft creation through Ruleset migration into concrete `ODY-S04-XXX` feature tasks — not scheduled by this task.
- A future `ADR-019` amendment to introduce `AssistantGM`/delegation, if the product owner decides it is needed before general availability — not scheduled by this task.

### Self-review summary

- Scope review: limited to allowed documentation files; no `ADR-019`/`ADR-022`/`ADR-023`/`ADR-024`/`ADR-013` redefinition.
- Architecture review: ADR reuses `ADR-002`/`012`/`013`/`019`/`022`/`023`/`024`; no replacement substrate introduced; `Ownership`/`Lifecycle` sections filled in exactly as `ADR-022` already reserved them.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied; no new permission constant or delegation pathway introduced.
- Documentation/version review: `ADR-025` v1.0 introduced; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.
- None remaining for the `SLICE-04` prerequisite backlog — all four ADRs are now `Accepted`. `SLICE-04`'s own vertical-slice implementation backlog is a separate future task.

### Decisions made during execution

- 2026-08-31 — Decision: verify PR #82's merge commit is a real ancestor of `origin/main` via `git merge-base --is-ancestor`, not just `gh pr view`'s reported state — Authority/approval: this task's own explicit preflight instruction, citing the `SLICE-UI-01` precedent where a GitHub-reported "Merged" status was discovered mid-session to require independent verification.
- 2026-08-31 — Decision: `CharacterOwnership` lives inside `ADR-022`'s already-reserved `Ownership` section; ownership/control-grant commands are `Character.ManageOwnership`-gated (MainGM-only), including control grants for which the product spec names no separate permission — Authority/approval: `ADR-025` §4, product §19/§26, `ADR-022` §5/§6's already-reserved section, `ADR-019`'s explicit deferral of the fuller ownership/control model as this ADR's own anticipated closure point.
- 2026-08-31 — Decision: physical delete removes only live state and re-validates dependencies host-side; `CharacterHistoryProjection` continues rendering history purely from `ADR-022`'s already-required event snapshots — Authority/approval: product §22.2/§22.3, `ADR-022` §7's own wording anticipating exactly this future operation, `ADR-012` §4.2's append-only guarantee.
- 2026-08-31 — Decision: the transition to `Dead` is restricted to `HostSystem`/`GMOverride` issuers, does not cascade-cancel `ADR-024` reservations, and `CharacterRestored` is a forward event, not a compensating one — Authority/approval: `CAP-INV-008`, product §23.1/§23.2, `ADR-022`'s parallel-section-editing philosophy.
- 2026-08-31 — Decision: Character Ruleset migration is its own `Preview`/`Apply` workflow, distinct from `ADR-013`'s schema migration runner, using ordinary transaction atomicity for failure rollback and `ADR-024`'s compensating-batch pattern for post-commit reversal; `.odchar` import reuses `ADR-023`'s unmodified pipeline with re-pinning at bind time — Authority/approval: `ADR-013` §9's own explicit statement that this exact separate ADR was anticipated, `ADR-024` §7.2's established compensating-batch pattern, `ADR-023` §6's compatibility-validation/pinning rule.

### Approved task changes

- None.
