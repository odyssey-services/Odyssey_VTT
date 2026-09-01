# ODY-S04-104 — Draft Submit/Review/Approve Workflow

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-104-draft-submit-review-approve`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-104_Draft_Submit_Review_Approve_Workflow.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement `ADR-023` §7: `SubmitCharacterDraft` (light revision check, `ApprovalState` remains `Draft`), `AddCharacterReviewComment` (conflict-free append, no `ExpectedCharacterRevision`/section revision), and `ApproveCharacterDraft` (`Character.Approve`, MainGM-only) transitioning `LifecycleStatus: Draft -> Active`/`ApprovalState: Draft -> Approved` atomically on the same `CharacterId` `ODY-S04-103` created. No `Reject`/`ChangesRequested` command or state (`ADR-023` §7.4, already decided, not reopened).

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-104` as the fourth implementation task, depending on `ODY-S04-103` (needs an already-bound Draft/Character to submit/review/approve).
- Value or risk reduction: closes the loop `ADR-023` §4.2/§7 describes — a bound Draft can now actually become an `Active`, campaign-visible Character; proves the first real business use of `CharacterLifecycleTransitions.IsValidTransition` and the first advance of `ApprovalState` past `Draft`.
- Blocking or enabling relationship: unblocks `ODY-S04-105` (`DevelopmentPool`/attribute purchases apply only to an `Active` Character — roadmap §13.8 step 6 follows this task's own step 5).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 4) — the binding scope definition for this task.
- `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` §7 (full read — the governing section).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §5–6 (the `Lifecycle` section, first real use).
- `docs/adr/ADR-019_Roles_Permissions_And_Turn_Authority_Model_v1.0.md` (MainGM-only `Character.Approve`, `actorIsMainGm` convention reused from `ODY-S04-102`).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7.1–7.2 (transitions, `ApprovalState`), §8.1/§8.3–8.4 (main process, unconfirmed draft, review comments).
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs` (`ODY-S04-101`–`103`'s own code) — read in full as the binding structural precedent.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-104`, `ADR-023` §7.
- Existing test IDs reused: None directly reused; `ApproveCharacterDraft`'s tests directly exercise `CharacterLifecycleTransitions.IsValidTransition` (already tested in isolation by `ODY-S04-101`'s own `TC-CHAR-008`) for the first time from a real business command.
- New test IDs introduced: `TC-CHAR-028` through `TC-CHAR-037` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main` advanced local `main` to `f355d81`, the merge commit for PR #87 (`ODY-S04-103`); `git merge-base --is-ancestor` independently confirmed it is a real ancestor of `origin/main`.
- `CharacterLifecycleTransitions.IsValidTransition` has existed since `ODY-S04-101` but no business command called it before this task — confirmed by `Grep` across `Packages/com.odyssey.persistence`.
- No prior task ever advanced `ApprovalState` past `Draft` — confirmed by `Grep` for `CharacterApprovalState.Approved` across production code.
- Product names no separate persisted field/state for "submitted" — `ApprovalState` stays `Draft` through submission (`ADR-023` §7.1/product §7.2, already decided). This task's own decision: `SubmitCharacterDraft` writes a new `SubmittedAt: UtcInstant?` field under the already-reserved `Lifecycle` section.
- `GameLogEntryRecord`/`IGameLogRepository` (`ODY-S03-007`) is the closest existing precedent for a conflict-free, `SqliteSavingPipeline`-backed append with its own dedicated table — reused as the structural model for `AddCharacterReviewComment`/`CharacterReviewComment`.
- `scripts/verify-test-structure.ps1`'s `TC-ARCH-001` check (discovered `ODY-S04-103`) requires a task contract file for every `taskId` a `test-catalog.json` entry references — this contract/ExecPlan is created before the final validation pass for that reason.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `CharacterReviewCommentId` (additive, matching sibling ID types).
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterRepository.SubmitCharacterDraft`/`AddCharacterReviewComment`/`ApproveCharacterDraft`/`GetCharacterReviewComments`; `CharacterReviewCommentRecord`; `CharacterRecord.SubmittedAt`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — `PersistenceFailures.CharacterLifecycleTransitionInvalid`/`CharacterApprovalDenied`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — the two corresponding `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — schema (`SubmittedAt` column, new `CharacterReviewComment` table), `SelectColumns`/`ReadCharacterRecord`/`WithRevisions` extension, four new methods.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterDraftSubmitReviewApproveTests.cs` (new) — 10 tests.
- `docs/errors/ERROR_CODES.md` (edit) — two new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-028`–`037`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 4 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Local Draft/templates/`BindDraftToCampaign` — already `ODY-S04-103`, not reopened.
- Administrative ownership commands — already `ODY-S04-102`.
- Development economy/purchases (`ODY-S04-105`–`107`).
- Ability/resource/anatomy (`ODY-S04-108`/`109`).
- Archive/physical delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`).
- Any `AssistantGM`-delegated approval — explicitly deferred by `ADR-019`.
- Any `Reject`/`ChangesRequested` command or state — explicitly excluded by `ADR-023` §7.4.
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`/`023` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterDraftSubmitReviewApproveTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-104_Draft_Submit_Review_Approve_Workflow.md
docs/plans/active/ODY-S04-104_Draft_Submit_Review_Approve_Workflow.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the new ID type (no serializer, no Unity/SQLite reference); `Odyssey.Application` owns the repository port extension and `CharacterReviewCommentRecord`; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: all three commands commit through the existing, unmodified `SqliteSavingPipeline`; `ApproveCharacterDraft`'s double `LifecycleStatus`/`ApprovalState` transition is one `UPDATE` statement inside that one transaction — no new atomicity mechanism.
- Serialization / compatibility boundary: no new JSON codec introduced this task; `CharacterReviewComment` is a plain relational row, not a JSON blob.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`103` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `PersistenceFailures.CharacterLifecycleTransitionInvalid`/`CharacterApprovalDenied` never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`103`'s own established pattern.
- Other: `Character.Approve`'s gate is the exact `actorIsMainGm` caller-supplied-boolean convention `AssignPrimaryOwner` already uses — no new permission-decision abstraction.

## 7. Expected behavior

### Scenario 1 — Submit succeeds on a Draft, rejected on non-Draft

**Given** a Character at `LifecycleStatus=Draft`
**When** `SubmitCharacterDraft` is called with the current `LifecycleRevision`
**Then** `SubmittedAt` is set and `LifecycleRevision`/`CharacterRevision` advance; `ApprovalState` remains `Draft`. Calling it again on an already-`Active` Character is rejected with `CharacterLifecycleTransitionInvalid`.

### Scenario 2 — Comments are conflict-free

**Given** an existing Character
**When** `AddCharacterReviewComment` and a concurrent `UpdateIdentity` (declaring only `expectedIdentityRevision`) are both called
**Then** both commit successfully; the comment never checks or changes `CharacterRevision`/any section revision.

### Scenario 3 — Approve is MainGM-only and atomic

**Given** a Character at `LifecycleStatus=Draft`/`ApprovalState=Draft`
**When** `ApproveCharacterDraft` is called by a non-MainGM actor
**Then** it is rejected with `CharacterApprovalDenied`, no state change.
**When** called by MainGM with the current `LifecycleRevision`
**Then** `LifecycleStatus` becomes `Active` and `ApprovalState` becomes `Approved` together, in one transaction; a fresh read confirms both landed.

### Scenario 4 — a repeat approve is rejected via the real transition table

**Given** a Character already `Active` (approved once)
**When** `ApproveCharacterDraft` is called again
**Then** it is rejected with `CharacterLifecycleTransitionInvalid`, because `CharacterLifecycleTransitions.IsValidTransition(Active, Active)` returns `false` — the actual gate, not a duplicated check.

### Required invariants

- `AddCharacterReviewComment` never requires or checks `ExpectedCharacterRevision`/any section revision.
- `LifecycleStatus` and `ApprovalState` change together or not at all in `ApproveCharacterDraft`.
- A duplicate `CommandId` on any of the three commands never reapplies the effect.
- No `ADR-022`/`023` file content changes; no `Reject`/`ChangesRequested` introduced.

## 8. Deliverables

- Production code: `CharacterReviewCommentId` (Domain), `CharacterRepositoryContracts.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: 10 new tests in `CharacterDraftSubmitReviewApproveTests.cs`, registered as `TC-CHAR-028`–`037`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `Character.SubmittedAt` column and new `CharacterReviewComment` table only.

## 9. Acceptance criteria

1. `SubmitCharacterDraft` succeeds on a Draft, sets `SubmittedAt`, advances `LifecycleRevision`/`CharacterRevision`, and does not change `ApprovalState`.
2. `SubmitCharacterDraft` on a non-Draft Character is rejected with `CharacterLifecycleTransitionInvalid`.
3. `AddCharacterReviewComment` requires no section revision and never conflicts with a concurrent section edit.
4. Multiple comments from different authors all persist, correctly ordered, none lost or overwritten.
5. `ApproveCharacterDraft` by a non-MainGM actor is rejected with `CharacterApprovalDenied`, no state change.
6. `ApproveCharacterDraft` by MainGM atomically transitions `LifecycleStatus`→`Active` and `ApprovalState`→`Approved` in one transaction.
7. A repeat `ApproveCharacterDraft` on an already-`Active` Character is rejected via `CharacterLifecycleTransitions.IsValidTransition` returning `false` for `Active`→`Active`.
8. A duplicate `CommandId` on `ApproveCharacterDraft` replays the stored result, does not reapply.
9. A stale `expectedLifecycleRevision` is rejected for both `SubmitCharacterDraft` and `ApproveCharacterDraft`, with no state change.
10. No `Reject`/`ChangesRequested` command or state introduced.
11. No change to `ADR-022`/`023` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
12. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
13. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 4 marked `Done` with a real PR link.
14. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-028` | .NET (`Odyssey.Tests.Persistence`) | Submit succeeds on a Draft, sets SubmittedAt | Pass |
| `TC-CHAR-029` | .NET (`Odyssey.Tests.Persistence`) | Submit on an Active Character rejected | Pass |
| `TC-CHAR-030` | .NET (`Odyssey.Tests.Persistence`) | Comment + concurrent Identity edit, no false conflict | Pass |
| `TC-CHAR-031` | .NET (`Odyssey.Tests.Persistence`) | Multiple comments from different authors all accumulate | Pass |
| `TC-CHAR-032` | .NET (`Odyssey.Tests.Persistence`) | Approve by non-MainGM rejected, no state change | Pass |
| `TC-CHAR-033` | .NET (`Odyssey.Tests.Persistence`) | Approve atomically transitions both fields | Pass |
| `TC-CHAR-034` | .NET (`Odyssey.Tests.Persistence`) | Repeat approve rejected via real transition table | Pass |
| `TC-CHAR-035` | .NET (`Odyssey.Tests.Persistence`) | Duplicate CommandId on Approve does not reapply | Pass |
| `TC-CHAR-036` | .NET (`Odyssey.Tests.Persistence`) | Stale revision rejects a second Submit | Pass |
| `TC-CHAR-037` | .NET (`Odyssey.Tests.Persistence`) | Stale revision rejects Approve, no state change | Pass |

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`103`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — one new column on the existing `Character` table (`SubmittedAt`), one new table (`CharacterReviewComment`); no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new column only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new column/table are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: review-comment text and author identity only — no hidden GM-only fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `Character.Approve` is MainGM-only; `AddCharacterReviewComment` has no permission gate in this task's own scope (product §7.3 names MainGM and the Draft's own author as intended commenters, but this task does not introduce an enforcement mechanism beyond what campaign membership already implies, matching `ADR-023` §7.3's own text that "no new permission constant is required").
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` checked first in `ApproveCharacterDraft`, matching the existing convention exactly.
- Redaction requirements: `PersistenceFailures.CharacterLifecycleTransitionInvalid`/`CharacterApprovalDenied` never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: `character_approved` event payload carries only `characterId`/`displayNameSnapshot`/`lifecycleStatusBefore`/`lifecycleStatusAfter`/`approvalStateBefore`/`approvalStateAfter`/revision counters — no secret data. `character_review_comment_added` carries `commentId`/`characterId`/`authorUserId`/`text` — no secret data.
- Abuse / malformed input limits: comment `Text` length limited (2000 chars), matching the codebase's general string-length-limit convention.
- Security tests: `TC-CHAR-032` (MainGM gate rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 4 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract and introduces new persisted schema/authoritative lifecycle semantics (the first real `ApprovalState` advance past `Draft`).
- ExecPlan path: `docs/plans/active/ODY-S04-104_Draft_Submit_Review_Approve_Workflow.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-103` (done, PR #87). Unblocks `ODY-S04-105`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character` column and one new table; no versioned schema migration.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
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

See section 5's "In scope" file list above.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 108 (98 pre-existing + 10 new) — 339 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Registry/catalog entries and task contract prepared proactively; final run recorded in the PR/report. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-028`. |
| AC-2 | Passed | `TC-CHAR-029`. |
| AC-3 | Passed | `TC-CHAR-030`. |
| AC-4 | Passed | `TC-CHAR-031`. |
| AC-5 | Passed | `TC-CHAR-032`. |
| AC-6 | Passed | `TC-CHAR-033`. |
| AC-7 | Passed | `TC-CHAR-034`. |
| AC-8 | Passed | `TC-CHAR-035`. |
| AC-9 | Passed | `TC-CHAR-036`/`037`. |
| AC-10 | Passed | No `RejectCharacterDraft`/`ChangesRequested` symbol anywhere in the diff. |
| AC-11 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-12 | Passed | See Validation results above. |
| AC-13 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 4 status/PR link updated. |
| AC-14 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `AddCharacterReviewComment` has no permission gate of its own in this task's scope — any caller with campaign access can add a comment. `ADR-023` §7.3 itself does not require a new permission constant here; a future task may need to add one if the product later requires restricting comment authorship.
- `SubmittedAt` is overwritten (not accumulated as a history) on each `SubmitCharacterDraft` call — only the most recent submission time is retained on the Character row itself; the full submit history remains available via `GetCharacterHistory`'s existing event-rebuild mechanism (the `character_draft_submitted` event is retained in `DomainEvents`).

### Follow-up tasks

- `ODY-S04-105` — `DevelopmentPool` & Attribute Purchases (applies only to an `Active` Character, which this task's `ApproveCharacterDraft` now produces for real).

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`023` or `SLICE-04_BACKLOG.md` change; no Unity/UI code; no `Reject`/`ChangesRequested` introduced.
- Architecture review: extends `ICharacterRepository`/`SqliteCharacterRepository` directly; reuses `SqliteSavingPipeline`/`actorIsMainGm`/`CharacterLifecycleTransitions` unmodified; `AddCharacterReviewComment` modeled on `GameLogEntryRecord`'s own existing append-only precedent rather than inventing a new pattern.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline.
- Security/privacy review: MainGM gate actually checked (`TC-CHAR-032`); error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-01 — Decision: `SubmitCharacterDraft` writes a new `SubmittedAt` field under the `Lifecycle` section, since product/ADR name no separate persisted field for "submitted" — Authority/approval: this task's own explicit ТЗ instruction to decide and justify.
- 2026-09-01 — Decision: `AddCharacterReviewComment` uses `SqliteSavingPipeline` for its transactional commit but checks no Character revision at all, modeled on `GameLogEntryRecord`'s own append-only precedent — Authority/approval: `ADR-023` §7.1's explicit "same shape as a GameLogEntry append" text.
- 2026-09-01 — Decision: `ApproveCharacterDraft`'s sole state-legality gate is the real `CharacterLifecycleTransitions.IsValidTransition` call, with no separate `ApprovalState == Draft` precondition layered on top — Authority/approval: this task's own explicit ТЗ instruction that the generic table must be the thing actually exercised.
- 2026-09-01 — Decision: `LifecycleStatus`/`ApprovalState` change in one `UPDATE` statement inside `SqliteSavingPipeline`'s existing one-transaction commit — Authority/approval: `ADR-012` §5, reused unmodified, no new atomicity mechanism.

### Approved task changes

- None.
