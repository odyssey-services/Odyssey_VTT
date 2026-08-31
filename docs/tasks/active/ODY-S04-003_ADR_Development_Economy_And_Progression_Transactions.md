# ODY-S04-003 — ADR Development Economy and Progression Transactions

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-003-adr-development-economy`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md`
**Created:** 2026-08-30
**Last updated:** 2026-08-30 UTC

## 1. Goal

Accept `ADR-024 — Development Economy and Progression Transactions`, resolving the `DevelopmentPool`/`DevelopmentTransaction` ledger boundary, atomicity/duplicate-spend prevention for advancement purchases, reservation and error-cancellation shape, `CriticalSuccessEvidence` single-use mechanism, and `CharacterRespec` compensation shape.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_BACKLOG.md` §3.3 identifies the development economy as the third prerequisite ADR gap — `ADR-002` already supplies `CommandId` idempotency, durable outcomes, event batches, and compensation; `ADR-012` supplies append-only event storage; that is necessary but not sufficient, because neither decides the economy ledger boundary, duplicate-spend prevention, one-transaction Character/DevelopmentPool/history updates, evidence single-use, or respec/revert compensation shape.
- Value or risk reduction: prevents implementation tasks from inventing an economy-specific idempotency/locking mechanism parallel to `ADR-002`/`ADR-022`, a `DevelopmentPool` that duplicates history as a second source of truth, or a respec/revert design that edits committed events.
- Blocking or enabling relationship: depends only on `ODY-S04-001` (`ADR-022`) — this task does not need `ODY-S04-002`/`ADR-023`, because development happens on an already-Active Character, after Draft/approval concerns are resolved. Enables `ODY-S04-004` (respec/progression compensation interacts with ownership/lifecycle operations); `SLICE-04` implementation remains blocked until `ADR-025` is also accepted.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/SLICE-04_BACKLOG.md` §3.3.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.5 (mechanics and progression), §13.9 (exit criteria — no `CharacterLevel`, duplicate purchase idempotency, critical evidence single-use).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-002`/`009`/`010`), §11 (attributes), §12 (`DevelopmentPool`/`DevelopmentTransaction`), §13 (purchase/reservation/revert/respec), §14 (skills/critical evidence), §26–28 (permissions/commands/domain events).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (full read — the aggregate/section-revision/lock/event-snapshot boundary this ADR must join, not redefine).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (full read — command/event/idempotency/compensation/pending model reused).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (full read — append-only journal and compensating-event mechanism reused).
- `docs/adr/ADR-022_*`/`ADR-023_*` as ADR format/depth precedents.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-003`, `ADR-024`, `SLICE-04` prerequisite backlog item 3.
- Existing test IDs: None.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, local private path outside the repository, secret, personal data, or hidden campaign content is copied into this task, the plan, or the ADR.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, and `git merge --ff-only origin/main` advanced local `main` to `f014961`, the merge commit for PR #81 (`ODY-S04-002`/`ADR-023`, Accepted).
- `git log --oneline -10` confirmed PR #81 is in `main` and contains `ADR-023`.
- `docs/tasks/SLICE-04_BACKLOG.md` lists `ODY-S04-003` as the third prerequisite task, depending on `ODY-S04-001` only (not `ODY-S04-002`), with future `ADR-024`.
- No `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` existed before this task.
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §12's `DevelopmentPool` schema is scoped by `CharacterId` with no independent cross-Character identity; §12.1's `DevelopmentTransaction.Kind` enum already includes `Grant`/`Spend`/`Reserve`/`ReleaseReservation`/`Refund`/`Correction`/`RespecReturn`/`RespecSpend` — confirming the full transaction vocabulary needed already exists in the product spec without this ADR inventing new kinds.
- §14.4's `CriticalSuccessEvidence.UsedByAdvancementId?` is already a nullable field on the evidence row itself in the product's own schema — directly supporting the flag-on-object single-use mechanism over a separate registry.
- §28's domain-event list does not name `DevelopmentTransaction` as an event type — confirming it is a ledger/projection row, not itself a `DomainEvent`, consistent with treating it the same way `ADR-022` treats `CharacterHistoryProjection`.
- `ADR-022` §5/§6 confirms `Mechanics` is an existing section revision/lock key with room for "mechanics-level metadata" — cross-checked directly to confirm `DevelopmentPool` fits there without a new section-lock key.
- `ADR-002` §20 confirms `PendingInteractionCreated` "либо эквивалентный event" is explicitly allowed — cross-checked to justify treating `AdvancementRecommendationCreated`/`Resolved` as this domain's own pending-workflow-equivalent pair rather than requiring the literal generic type.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep` during this task.

## 5. Scope

### In scope

- Create `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`.
- Create this task contract.
- Create an ExecPlan for the ADR task.
- Update the `ODY-S04-003` row in `docs/tasks/SLICE-04_BACKLOG.md` to `Done` and point to `ADR-024`.
- Run documentation-only validation.

### Out of scope

- Character aggregate boundary/section locks/history (`ADR-022`, already Accepted, not reopened).
- Local Draft vs campaign Character, templates, submit/review/approve (`ADR-023`, already Accepted, not reopened).
- Ownership/lifecycle operations/Dead/restore/physical delete/Ruleset migration (`ODY-S04-004`/future `ADR-025`).
- Ability/resource/anatomy mechanics themselves (already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4) — only how their development transactions are bounded/committed is in scope.
- Concrete numeric attribute/skill costs, caps, or `SkillAdvancementRule` decision tables.
- Any concrete UI for purchase, recommendation-review, or respec-preview screens.
- Any production code, tests, persistence schema, Unity assets, or DTO implementation.

### Allowed paths

```text
docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md
docs/plans/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md
docs/tasks/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md
docs/tasks/SLICE-04_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-023*
```

## 6. Technical constraints

- Module ownership and dependency direction: future implementation must keep `DevelopmentPool`/`AdvancementPurchase`/`CriticalSuccessEvidence`/`AdvancementRecommendation` invariants in Domain, `SkillAdvancementRule` decisions in Rules, command orchestration/ledger-projection rebuild in Application, physical storage in Persistence, delivery in Networking, and purchase/respec-preview UI in Unity Client per `ADR-001`.
- Authoritative-state and transaction boundary: every command in this ADR is an ordinary `ADR-022` Character command operating inside the `Mechanics` section, committed in one `ADR-012` transaction; no parallel economy-specific transaction/idempotency mechanism.
- Serialization / compatibility boundary: ledger/evidence/recommendation payloads remain explicit/versioned DTOs under `ADR-003`; no direct Domain aggregate serialization (not reopened here, referenced only).
- Time / RNG rule: Not applicable — no clock/RNG-dependent decision in this ADR (critical success itself is a dice-roll concern already resolved elsewhere; this ADR only fixes how the resulting evidence is consumed).
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no new dependency, tool, action, or package.
- Security / privacy / redaction rule: Not applicable — no new permission constant introduced; `Character.SpendDevelopment`/`Character.GrantDevelopment`/`Character.Respec`/`Character.RevertAdvancement` already named in product §26 are reused as-is.
- Performance or platform constraint: Not applicable.
- Other: do not solve future `ADR-025` scope inside `ADR-024`; do not decide concrete numeric ruleset balances.

## 7. Expected behavior

### Scenario 1 — Ledger boundary is reviewable

**Given** `SLICE-04_BACKLOG.md` §3.3 identifies the `DevelopmentPool` ledger boundary as a prerequisite gap
**When** `ADR-024` is reviewed
**Then** it states `DevelopmentPool`/`DevelopmentTransaction` as `Mechanics`-section data inside the existing `ADR-022` Character aggregate, not an independently authoritative subordinate aggregate.

### Scenario 2 — Duplicate purchase does not spend twice

**Given** roadmap §13.9's "duplicate command does not spend twice" exit criterion
**When** `ADR-024` is reviewed
**Then** it specifies that `CommandId`/`AppliedCommands` (`ADR-002`) alone prevent a retried purchase from re-applying its effect, with no second, economy-specific idempotency mechanism.

### Required invariants

- All four task-required questions (§4 of the ТЗ) are answered explicitly and separately.
- `ADR-024` reuses `ADR-002`, `ADR-012`, and `ADR-022` without redefining them.
- No code/schema/test implementation is introduced.
- No contradiction with `ADR-022`'s already-accepted section-revision/lock model or `ADR-012`'s compensating-event mechanism.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `ADR-024`, this task contract, ExecPlan, and `SLICE-04_BACKLOG.md` row update.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` exists and is `Accepted`.
2. ADR answers the four task-required questions separately: ledger boundary, atomicity/duplicate-spend prevention, reservation/error-cancellation shape, and evidence single-use/respec compensation shape.
3. ADR includes considered alternatives for at least: `DevelopmentPool` as a Character-aggregate section vs a subordinate aggregate with its own identity; reservation as an explicit pending state vs optimistic apply-then-compensate; evidence single-use via a flag on the object vs a separate spent-evidence registry.
4. ADR explicitly excludes the Character aggregate boundary (`ADR-022`), Draft/template/approval (`ADR-023`), ownership/lifecycle/Ruleset migration (`ADR-025`), ability/resource/anatomy mechanics themselves, concrete numeric balances, and code/schema/test implementation.
5. ADR does not contradict `ADR-022`'s section-revision/lock model or `ADR-012`'s compensating-event/append-only mechanism.
6. This task contract exists with all 18 numbered sections.
7. ExecPlan exists because `PLANS.md` §1 requires it for future public contract/authoritative state ADR work, consistent with `ODY-S04-001`/`ODY-S04-002`'s own precedent.
8. `docs/tasks/SLICE-04_BACKLOG.md` marks `ODY-S04-003` as `Done` and points to `ADR-024`.
9. Diff contains only documentation files under `docs/adr`, `docs/plans`, and `docs/tasks`.
10. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` pass.
11. `ADR-024` §15 (Normative action) contains no premature claim of product-owner approval/sign-off — only the same task-acceptance rationale pattern `ADR-022`/`ADR-023` use.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR task; replacement evidence is repository formatting and policy validation plus PR review.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Product owner reviews `ADR-024` before `ODY-S04-004` (which depends on it) proceeds.

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
- Version fields affected: `ADR-024` document version introduced as `1.0`; no application/schema/contract/protocol/ruleset version changes.
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

- Data classes handled: task-safe summaries of private product documentation, public repository ADR/task paths, and future development-economy architecture.
- Trust boundaries: private product docs are read-only and not copied verbatim into tracked files.
- Authorization / audience checks: no implementation; ADR reuses existing `Character.SpendDevelopment`/`GrantDevelopment`/`Respec`/`RevertAdvancement` permissions (product §26) without introducing a new permission constant.
- Redaction requirements: no private excerpts, secrets, credentials, personal data, or hidden campaign content in commits/PR text.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `PLANS.md` §1 requires an ExecPlan because the ADR changes future public domain/persistence contracts and affects authoritative development-economy/progression-transaction semantics that later implementation tasks must follow. This matches `ODY-S04-001`/`ODY-S04-002`'s own precedent for the immediately preceding ADRs in the same prerequisite series, and `SLICE-04_BACKLOG.md`'s own "ExecPlan expected" note for this task.
- ExecPlan path: `docs/plans/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md`
- Expected pull request count: 1
- Milestone or sequencing constraints: `ODY-S04-004` depends on this ADR (respec/progression compensation interacts with ownership/lifecycle operations). `SLICE-04` implementation backlog still waits for `ADR-025` as well.

## 15. Documentation and versioning impact

- Documents that must change: `ADR-024`, this task contract, ExecPlan, `SLICE-04_BACKLOG.md`.
- Documents that must not change: `ADR-001` through `ADR-023`, private `Documentation/` sources, production code, tests, scripts, Unity assets.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none implemented; future development-economy command/event/DTO contract guidance is documented in the ADR only.
- Documentation version changes: `ADR-024` introduced as v1.0.
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

- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` — new development-economy/progression-transaction ADR.
- `docs/plans/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md` — ExecPlan for this ADR task.
- `docs/tasks/active/ODY-S04-003_ADR_Development_Economy_And_Progression_Transactions.md` — this task contract.
- `docs/tasks/SLICE-04_BACKLOG.md` — marks `ODY-S04-003` as complete.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; includes required repository structure, forbidden tracked patterns, LFS policy, ErrorCode registry, workflow policy, and static Unity project/package/toolchain checks. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-024` file exists and status is `Accepted`. |
| AC-2 | Passed | ADR sections 4–7 answer all four required questions separately. |
| AC-3 | Passed | ADR section 12 records the three required alternatives plus a fourth (respec batch vs opaque event). |
| AC-4 | Passed | ADR section 8 excludes `ADR-022`/`ADR-023` scope, future `ADR-025`, ability/resource/anatomy mechanics themselves, concrete numeric balances, and implementation work. |
| AC-5 | Passed | ADR sections 4.2/6/7.1 explicitly reuse `ADR-022`'s section-revision/lock model and `ADR-012`'s compensating-event mechanism without redefining either. |
| AC-6 | Passed | This contract contains all 18 numbered sections. |
| AC-7 | Passed | ExecPlan exists under `docs/plans/active`. |
| AC-8 | Passed | `SLICE-04_BACKLOG.md` row updated for `ODY-S04-003`. |
| AC-9 | Passed | Diff scope is docs-only. |
| AC-10 | Passed | Required validation commands passed locally. |
| AC-11 | Passed | ADR section 15 states only task-acceptance rationale (no spike needed), no product-owner sign-off claim. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No production implementation is included. `ADR-025` remains the last open prerequisite task.
- `ADR-024` intentionally does not decide concrete numeric attribute/skill costs or `SkillAdvancementRule` decision tables — these remain Rules Engine/ruleset content.

### Follow-up tasks

- `ODY-S04-004` — `ADR-025` Character Ownership, Lifecycle, and Ruleset Migration Operations.

### Self-review summary

- Scope review: limited to allowed documentation files; no `ADR-022`/`ADR-023` redefinition.
- Architecture review: ADR reuses `ADR-002`/`012`/`022`; no replacement substrate introduced; `DevelopmentPool` lives inside the `Mechanics` section, matching `ADR-022`'s own description of mechanics-level metadata.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied; no new permission constant introduced.
- Documentation/version review: `ADR-024` v1.0 introduced; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.
- `SLICE-04` implementation remains blocked until `ADR-025` is also accepted.

### Decisions made during execution

- 2026-08-30 — Decision: `DevelopmentPool`/`DevelopmentTransaction` are `Mechanics`-section data inside the `ADR-022` Character aggregate, not a subordinate aggregate — Authority/approval: `ADR-024` §4, `ADR-022` §5's own description of mechanics-level metadata, and `ADR-022` §8's "no second source of truth" projection contract.
- 2026-08-30 — Decision: `CommandId`/`AppliedCommands` (`ADR-002`) and one `ADR-012` transaction are the sole idempotency/atomicity mechanisms for purchases — no parallel economy-specific mechanism — Authority/approval: `ADR-002` §9/§11, `ADR-012` §5/§7, roadmap §13.9's duplicate-spend exit criterion.
- 2026-08-30 — Decision: reservation is limited to genuinely pending operations (the skill-5+ recommendation path), modeled as this domain's own `ADR-002` §20 pending-workflow-equivalent event pair — Authority/approval: product §13.3's explicit "reserve only for genuinely pending operations," `ADR-002` §20's explicit allowance for domain-specific equivalent events.
- 2026-08-30 — Decision: error cancellation (`RevertAdvancementPurchase`) and `CharacterRespec` use `ADR-012` §6's compensating-event mechanism exclusively, with a dependency check gating revert — Authority/approval: `CAP-INV-005`, product §13.4/§13.5, `ADR-012` §6.
- 2026-08-30 — Decision: `CriticalSuccessEvidence` single-use is enforced via the evidence row's own `UsedByAdvancementId` field guarded by its own revision, not a separate spent-evidence registry — Authority/approval: product §14.4's own schema already including this field; `ADR-022` §5's entry-level revision mechanism applied directly to evidence rows.
- 2026-08-30 — Decision: `CharacterRespec` produces an inspectable, ordered event batch grouped by one `CharacterRespecCompleted` event, not a single opaque event — Authority/approval: `CAP-INV-005`, `ADR-022` §7's prohibition on full-Character-sheet event payloads, product §13.5's "группирует события в одной истории."

### Approved task changes

- None.
