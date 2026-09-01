# ODY-S04-104 — Draft Submit/Review/Approve Workflow

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-104-draft-submit-review-approve`
**Pull request:** [#88](https://github.com/odyssey-services/Odyssey_VTT/pull/88)
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-023` §7: `SubmitCharacterDraft` (light revision check, `ApprovalState` stays `Draft`), `AddCharacterReviewComment` (conflict-free append, no section revision), and `ApproveCharacterDraft` (`Character.Approve`, MainGM-only, `LifecycleStatus: Draft -> Active`/`ApprovalState: Draft -> Approved` atomically). No `Reject`/`ChangesRequested` (`ADR-023` §7.4, not reopened). Fourth implementation task of `SLICE-04`.

## 2. Task contract

- Goal: extend `ICharacterRepository`/`SqliteCharacterRepository` with three commands plus a comment-thread read, on the same `CharacterId` `ODY-S04-103`'s `BindDraftToCampaign` created.
- Acceptance criteria: Submit succeeds on a Draft, rejected on non-Draft; comments never require/check a revision and never conflict with concurrent section edits; multiple comments from different authors all persist; Approve is MainGM-only, atomically flips both `LifecycleStatus`/`ApprovalState`, is gated by the real `CharacterLifecycleTransitions.IsValidTransition` call (not a duplicated check), and rejects a repeat call on an already-`Active` Character via that same table; duplicate `CommandId` on Approve does not reapply; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`023` content change.
- Requirement IDs: `ODY-S04-104`, `ADR-023` §7.
- In scope: three new `ICharacterRepository` methods (`SubmitCharacterDraft`, `AddCharacterReviewComment`, `ApproveCharacterDraft`) plus `GetCharacterReviewComments`, `CharacterReviewCommentRecord`, a new `SubmittedAt` field on `CharacterRecord`, a new `CharacterReviewComment` table, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: Local Draft/templates/`BindDraftToCampaign` (`ODY-S04-103`, not reopened), ownership commands (`ODY-S04-102`), development economy, ability/resource/anatomy, archive/delete/Ruleset migration, `AssistantGM` delegation, any `Reject`/`ChangesRequested` command or state, any Unity/UI code, any `ADR-022`/`023` content change.
- Required authorities: `ADR-023` §7 (full read), `ADR-022` §5–6 (`Lifecycle` section, first real use), `ADR-019` (MainGM-only `Character.Approve`, `actorIsMainGm` convention reused from `ODY-S04-102`), product §7.1–7.2/§8.1/§8.3–8.4, `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`103`'s own code as the binding structural precedent.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `f355d81` (PR #87, `ODY-S04-103`), independently verified via `git merge-base --is-ancestor`.
- `CharacterLifecycleTransitions.IsValidTransition` has existed since `ODY-S04-101` but no business command has ever called it until now — this task is the first real caller.
- `ApprovalState` has never advanced past `Draft` in any prior task — this task is the first to reach `Approved`.
- Product names no separate "Submitted" field/state on Character; `ApprovalState` stays `Draft` through submission (already decided, `ADR-023` §7.1/product §7.2, not reopened). This task's own decision: what `SubmitCharacterDraft` actually mutates is a new `SubmittedAt: UtcInstant?` field under the already-reserved `Lifecycle` section (no new `ADR-022` section key), gated by `LifecycleRevision`.
- `AddCharacterReviewComment` is modeled the same way `GameLogEntryRecord`/`IGameLogRepository.SaveDiceRollEntry` already is (own dedicated table, real `SqliteSavingPipeline`-backed event, `CommandId`-based idempotency via a `CommandId` column on the row itself since there is no single mutable row to key `LastCommandId` off) — confirmed as the closest existing precedent for "conflict-free append" in this codebase.
- No `AssistantGM`/delegation exists (`ADR-019`'s own deferred scope) — `Character.Approve` gate reuses the exact `actorIsMainGm` caller-supplied-boolean convention `ODY-S04-102`'s `AssignPrimaryOwner` already established.
- `scripts/verify-test-structure.ps1`'s `TC-ARCH-001` check (discovered `ODY-S04-103`) requires a task contract file for every `taskId` a `test-catalog.json` entry references — this ExecPlan/contract is created before the final validation pass, per that same discovery.

Assumptions: none.

## 4. Proposed approach

- Application: extend `ICharacterRepository` with `SubmitCharacterDraft`/`AddCharacterReviewComment`/`ApproveCharacterDraft`/`GetCharacterReviewComments`; add `CharacterReviewCommentRecord`; add `CharacterReviewCommentId` (Domain, canonical `Prefix + Uuid7.NewHex32` pattern matching sibling ID types); extend `CharacterRecord` with `SubmittedAt`.
- Persistence: `SubmitCharacterDraft` follows the exact single-section-gate pattern `UpdateIdentity`/`UpdatePresentation` already established, but gates on `LifecycleRevision` and requires `LifecycleStatus == Draft` as a plain precondition (not a `CharacterLifecycleTransitions` edge, since `LifecycleStatus` itself does not change). `AddCharacterReviewComment` uses `SqliteSavingPipeline` for its transactional commit (event + `AppliedCommands` receipt) but never checks any `Character` revision — a genuine conflict-free append, new `CharacterReviewComment` table. `ApproveCharacterDraft` gates on `actorIsMainGm` (checked first, before any DB access) then on `CharacterLifecycleTransitions.IsValidTransition(current.LifecycleStatus, Active)` as the sole state-legality check — deliberately not layering a redundant `ApprovalState == Draft` precondition on top, so the transition table is the real, exercised gate (a repeat call after the first successful approve is rejected because `IsValidTransition(Active, Active)` is false, per the table's own same-status rule). `LifecycleStatus`/`ApprovalState` change together in one `UPDATE` statement inside the one transaction `SqliteSavingPipeline.Execute` already commits atomically.
- Tests: Submit success/reject-on-Active/stale-revision; comment no-conflict-with-concurrent-edit, multi-author accumulation; Approve non-MainGM-rejected/atomic-both-fields/repeat-rejected-via-real-transition-table/duplicate-CommandId/stale-revision.

No Unity/UI code, no `ADR-022`/`023` content change, no `Reject`/`ChangesRequested` command or state.

## 5. Milestones

### M1 — Domain/Application extension

- [x] `CharacterReviewCommentId` (Domain, `DomainIdentity.cs`).
- [x] `ICharacterRepository` extended with four methods; `CharacterReviewCommentRecord`; `CharacterRecord.SubmittedAt`.
- [x] `PersistenceFailures`/`ErrorCodes` additions (`CharacterLifecycleTransitionInvalid`, `CharacterApprovalDenied`).

### M2 — Persistence and tests

- [x] `SqliteCharacterRepository` extended: schema (`SubmittedAt` column, new `CharacterReviewComment` table), `SelectColumns`/`ReadCharacterRecord`/`WithRevisions`, `SubmitCharacterDraft`/`AddCharacterReviewComment`/`ApproveCharacterDraft`/`GetCharacterReviewComments`.
- [x] 10 new tests in `CharacterDraftSubmitReviewApproveTests.cs`, all passing on first run.
- [x] `dotnet build`/`dotnet test` full suite green, no regression (329 -> 339).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-028`–`037`).
- [x] This task contract/ExecPlan, created before the final `check-repository-policy.ps1` pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [x] Commit, push, and open Draft PR — [#88](https://github.com/odyssey-services/Odyssey_VTT/pull/88).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 -- Preflight confirmed PR #87's merge commit is a real ancestor of `origin/main` at `f355d81`; created branch `feat/ody-s04-104-draft-submit-review-approve`.
- 2026-09-01 -- Read `ADR-023` §7 in full (re-confirmed), `ADR-022` §5–6 (Lifecycle section, re-confirmed), product §7.1–7.2/§8.1/§8.3–8.4, backlog §5–7, and `ODY-S04-101`–`103`'s own code in full.
- 2026-09-01 -- Decided `SubmitCharacterDraft`'s own mutation (`SubmittedAt` under `Lifecycle`) since product/ADR name no separate field.
- 2026-09-01 -- Implemented Domain/Application/Persistence extension; `dotnet build` passed on first attempt.
- 2026-09-01 -- Wrote and ran 10 new tests; all passed on first run against real SQLite fixtures.
- 2026-09-01 -- Added `ERROR_CODES.md`/`test-catalog.json` entries proactively before running `check-repository-policy.ps1`, having learned the registry-discipline pattern from `ODY-S04-101`–`103`.
- 2026-09-01 -- Created this ExecPlan/task contract before the final validation pass, having learned `verify-test-structure.ps1`'s `TC-ARCH-001` task-contract-reference requirement from `ODY-S04-103`.

## 7. Decisions

- 2026-09-01 -- Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1. Authority: `PLANS.md` §1, backlog row 4.
- 2026-09-01 -- Decision: `SubmitCharacterDraft` writes a new `SubmittedAt: UtcInstant?` field under the already-reserved `Lifecycle` section (gated by `LifecycleRevision`), since neither `ADR-023` nor product name a separate persisted field/state for "submitted." Authority: this task's own explicit ТЗ instruction to decide and justify; `ADR-023` §7.1's own text ("declares the expected revisions of whichever sections it is marking ready for review") naturally maps onto the one section this command's own effect actually touches.
- 2026-09-01 -- Decision: `AddCharacterReviewComment` uses `SqliteSavingPipeline` for its transactional commit but checks no `Character` revision at all — a genuine conflict-free append, modeled on `GameLogEntryRecord`'s own append-only precedent, with idempotency via a `CommandId` column on the comment row itself rather than a mutable `LastCommandId` row. Authority: `ADR-023` §7.1's own explicit "architecturally the same shape as a GameLogEntry append" text.
- 2026-09-01 -- Decision: `ApproveCharacterDraft`'s sole state-legality gate is `CharacterLifecycleTransitions.IsValidTransition(current.LifecycleStatus, Active)` — no separate `ApprovalState == Draft` precondition layered on top. Authority: this task's own explicit ТЗ instruction that the generic transition table must be the thing actually exercised (not duplicated ad hoc logic), and the test requirement that a repeat approve be rejected specifically via that table's own same-status rule.
- 2026-09-01 -- Decision: `LifecycleStatus` and `ApprovalState` are updated in the same single `UPDATE` statement, inside the one transaction `SqliteSavingPipeline.Execute` already commits atomically — no new atomicity mechanism invented. Authority: `ADR-012` §5's already-accepted one-transaction commit boundary, reused unmodified.
- 2026-09-01 -- Decision: no `Reject`/`ChangesRequested` command or state is introduced. Authority: `ADR-023` §7.4, already decided, explicitly not reopened by this task's own ТЗ.

## 8. Discoveries and deviations

- No open architectural question was found that `ADR-023`/`ADR-019` do not already answer. The two points the ТЗ explicitly asked this task to decide (what `SubmitCharacterDraft` mutates; how atomicity is proven for the double transition) were both resolved as ordinary engineering decisions within the existing substrate (`Lifecycle` section revision; single-transaction single-`UPDATE`), not architectural gaps.
- Confirmed via direct reading that `CharacterLifecycleTransitions.IsValidTransition` had never been called by any business command before this task (`ODY-S04-101`'s own tests exercise it directly, but no repository method did) — this task is the first real caller, as the ТЗ's own context section anticipated.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 339/339 (10 new tests), zero regression.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: to be confirmed in the final validation pass (registry entries and task contract prepared proactively based on prior tasks' own discoveries).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — the new `Character.SubmittedAt` column is additive; the new `CharacterReviewComment` table is new and unused by any other code path if reverted; no existing column altered.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S04-105` (`DevelopmentPool` & attribute purchases, which apply only to an already-`Active` Character — roadmap §13.8 step 6 follows this task's own step 5).
