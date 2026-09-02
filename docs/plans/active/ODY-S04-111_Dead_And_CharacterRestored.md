# ODY-S04-111 — Dead & `CharacterRestored`

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-111-dead-character-restored`
**Pull request:** TBD
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-025` §6: the transition into `Dead` restricted to exactly two structurally-exclusive legal paths (a completed Rules Engine `FatalDamagePending` workflow, or an explicit MainGM `GMOverride` — never a plain owner/controller call, `CAP-INV-008`), gated by the `Lifecycle` section's own lock/revision and leaving `ADR-024` reservations/`Mechanics` untouched; `RestoreDeadCharacter` as a forward (never compensating) `CharacterRestored` event with a mandatory reason and the GM's explicit choices of new `LifecycleStatus`/body-part state/resources — deliberately never touching `RuntimeState`/board position. Eleventh implementation task of `SLICE-04`.

## 2. Task contract

- Goal: implement both commands per `ADR-025` §6, resolving the three design questions this task's own ТЗ explicitly delegates (§1.1's structural "who issued" discriminator, §1.2's `RuntimeState` non-content boundary, §1.3's forward-event construction) directly from `ADR-025`/product's own text, reusing the already-generic `CharacterLifecycleTransitions.IsValidTransition` table rather than re-deriving edge legality.
- Acceptance criteria: `TransitionCharacterToDead` accepts only `LifecycleDeathIssuerKind.GMOverride` (MainGM-checked) or `LifecycleDeathIssuerKind.HostSystemFatalDamageCompletion` (structural entry point, no user-permission check); a plain owner call claiming neither path is structurally impossible to construct; the transition is gated by `LifecycleRevision` and `CharacterLifecycleTransitions.IsValidTransition`; it touches only the `Lifecycle` section's own columns, leaving `DevelopmentPool`/`Reserved`/`MechanicsRevision` byte-for-byte unchanged. `RestoreDeadCharacter` is legal only from `Dead`, MainGM-only, requires a non-empty `ReasonCode` (rejected gracefully, not thrown), lets the GM choose any `IsValidTransition`-legal target status plus optional whole-list `CharacterAnatomy`/`CharacterResource` changes, declares `ExpectedCharacterAnatomyRevision`/`ExpectedCharacterResourcesRevision` only when those sections are actually touched, and produces exactly one ordinary forward `CharacterRestored` event (`IsCompensating=0`, `OriginalEventId=NULL`) — never referencing `RuntimeState`/board position at all. Both commands are `CommandId`-idempotent, verified against real state; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`024`/`025` content change; no Unity/UI code.
- Requirement IDs: `ODY-S04-111`, `ADR-025` §6, `ADR-022` §4–6, product §23/§7.1, `CAP-INV-008`.
- In scope: `LifecycleDeathIssuerKind` enum (Domain); `ICharacterRepository`/`SqliteCharacterRepository` extension (`TransitionCharacterToDead`/`RestoreDeadCharacter`); `RestoreDeadCharacterRequest`/`CharacterRestoreResourceValue` request DTOs; four new error codes/`PersistenceFailures` factories; `HistoryEventTypes` extended with the two new event types; tests; error registry/test-catalog additions; backlog status update.
- Out of scope: the real Rules Engine `FatalDamagePending` workflow (no such workflow, or any `IssuerKind`/`HostSystem` infrastructure, exists anywhere in this codebase — confirmed by search; this task accepts `HostSystemFatalDamageCompletion` only as a structurally legal entry point a future task could use); board/token position restoration (`RuntimeState` — coordinated separately by the caller's own Board commands after this call returns); automatic cancellation/release of `ADR-024` reservations on death (explicitly excluded by `ADR-025` §6.2); `.odchar` Export/Import, Ruleset migration (`ODY-S04-112`/`113`); Archive/physical delete (already `ODY-S04-110`); any Unity/UI code; any change to accepted `ADR-022`/`024`/`025`/`SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond the status row.
- Required authorities: `ADR-025` §6 (full read), `ADR-022` §4–6 (full read, including confirming `RuntimeState` has no defined content anywhere), product §23 (full read)/§7.1, `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `CharacterLifecycleTransitions.IsValidTransition` (`ODY-S04-101`), `ArchiveCharacter`/`ApproveCharacterDraft` (`Lifecycle`-transition pattern, `ODY-S04-110`/`104`), `ApplyCharacterRespec`/`AcquireAbilityViaProgressionPurchase` (cross-section command pattern, `ODY-S04-107`/`108`).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `ODY-S04-110`'s own merge commit (PR #94), independently verified via `git merge-base --is-ancestor`.
- Direct search confirms no `IssuerKind`/`HostSystem` actor infrastructure exists anywhere in this codebase — only a doc-comment forward-reference in `ODY-S04-101`'s own `CharacterLifecycleStatus` enum (explicitly anticipating this task's own addition here) — and no real Rules Engine `FatalDamagePending` workflow exists that could become a genuine `HostSystem` caller.
- Direct `Grep` across `ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` confirms `RuntimeState` appears ONLY as a bare section-name/lock-key/revision-column reference (lines ~21, 22, 99, 144, 188) — zero content definition anywhere, fundamentally different from a "reserved but unused" section like `CharacterAbilitiesRevision` was before `ODY-S04-108` (which DID have defined content, just no wired command yet).
- `CharacterLifecycleTransitions.IsValidTransition` (from `ODY-S04-101`) already encodes every edge this task needs: `Active|Inactive|Retired -> Dead` legal, `Draft -> Dead` illegal; `Dead -> {Active, Inactive, Retired, Archived}` all legal (the restore-path edges) — no Domain-layer change needed to this table at all.
- `GetCharacterHistory`'s own `HistoryEventTypes` array is a hand-maintained filter whitelist that every prior task extends with its own new event type(s) — an established, expected touch point.

Assumptions: none.

## 4. Proposed approach

- **§1.1 "who issued" discriminator:** `LifecycleDeathIssuerKind` (new Domain enum, `HostSystemFatalDamageCompletion=1`, `GMOverride=2`), added immediately after `CharacterLifecycleStatus` in `CharacterLifecycle.cs`. Chosen over the ТЗ's alternative "two mutually-exclusive booleans" option because an enum matches this codebase's own existing enum-discriminator conventions (`AdvancementOperationKind`, `SourceKind`, `RecoveryRule`) and structurally makes a third "plain owner" value impossible to construct at the type level — satisfying `CAP-INV-008` by construction, not by an extra runtime check alone. Permission split: `GMOverride` checks `actorIsMainGm` exactly like every other MainGM-gated command; `HostSystemFatalDamageCompletion` accepts unconditionally (no `actorIsMainGm` check at all), since it represents `IssuerKind=HostSystem` (`ADR-002` §6.4) — not a user-issued command, and there is no real caller today to validate a permission boundary against.
- **§1.2 `RuntimeState` boundary:** satisfied by omission, not by a special-cased guard — `RestoreDeadCharacter`'s own `UPDATE` statement and its `CharacterSectionRevisions`/`CharacterRecord` construction never reference `RuntimeStateRevision`/any `RuntimeState`-shaped column or field anywhere. Board/token position restoration is the calling code's own separate responsibility via Board commands issued after this call returns.
- **§1.3 forward-not-compensating event:** satisfied by construction — `PipelineWrite<CharacterRecord>`'s `originalEventId`/`compensationGroupId`/`isCompensating` parameters all default to `null`/`null`/`false`; `RestoreDeadCharacter`'s own `PipelineWrite` call simply omits them (exactly like every pre-`ODY-S04-107` command), producing an ordinary forward event with no reference to `CharacterDied` at all.
- **§1.4 `ADR-024` reservations untouched:** satisfied by omission — `TransitionCharacterToDead`'s own `UPDATE` statement lists only `LifecycleStatus`/`LifecycleRevision`/`CharacterRevision`/`UpdatedAt`/`LastCommandId`, deliberately never including `PoolEarned`/`PoolSpent`/`PoolReserved`/`MechanicsRevision` — those columns are left byte-for-byte unchanged by simple omission from the `SET` clause, mirroring `ArchiveCharacter`'s own `UPDATE` statement from `ODY-S04-110`.
- **§5 item 2 `RestoreDeadCharacter` parameter contract:** a request-DTO class (`RestoreDeadCharacterRequest`), mirroring `BindDraftToCampaignRequest`/`CreateCharacterRequest`'s own established many-parameter-command convention. `NewBodyParts`/`NewPermanentModifications` are nullable whole-list REPLACEMENTS (`null` = do not touch `CharacterAnatomy`; non-null replaces the entire list) — mirroring `ReplaceAnatomyProfile`'s own already-established whole-list-replacement shape (`ODY-S04-109`), not a diff/patch, for consistency and simplicity. `NewResourceCurrentValues` is a nullable/empty-meaning-untouched list of `CharacterRestoreResourceValue` entries (`CharacterResourceId` + `NewCurrentValue` pairs, wrapped in a named DTO class rather than a bare tuple, per this codebase's own public-API convention), each setting one resource's `CurrentValue` — `CharacterResource`'s own constructor provides `[MinimumValue, EffectiveMaximum]` bounds validation "for free." Per `ADR-022` §5 rule 2, `ExpectedLifecycleRevision` is always required; `ExpectedCharacterAnatomyRevision` is required only when `NewBodyParts != null`; `ExpectedCharacterResourcesRevision` is required only when `NewResourceCurrentValues` is non-null/non-empty — enforced both in the DTO constructor (structural pairing) and again as revision-conflict checks inside `RestoreDeadCharacter`'s own transaction.
- **Persistence — cross-section transaction structure:** `RestoreDeadCharacter` gets its own dedicated method with its own `_pipeline.Execute` call (the `ApplyCharacterRespec`/`AcquireAbilityViaProgressionPurchase` precedent from `ODY-S04-107`/`108`), since it can span up to three independently-gated sections (`Lifecycle` always; `CharacterAnatomy`/`CharacterResources` conditionally) in one commit — no existing single-section helper's contract could express this without a larger, riskier generalization. `TransitionCharacterToDead` is a direct structural copy of `ArchiveCharacter`'s own single-section `Lifecycle`-transition shape (load, revision check, `IsValidTransition` gate, one `UPDATE`, one event), with the §1.1 discriminator's own permission split in place of a plain `actorIsMainGm` check.
- **`ReasonCode` validation placement:** deliberately NOT thrown from `RestoreDeadCharacterRequest`'s own constructor (unlike every other required-string constructor argument in this codebase) — validated instead as the first statement inside `RestoreDeadCharacter`'s own method body, returning a graceful `Result<CharacterRecord>.Failure(PersistenceFailures.CharacterRestoreReasonRequired(...))`. This matches the established convention for every other "ReasonCode required" rejection (`DeleteCharacterPermanently`, `RevertAdvancementPurchase`, `ApplyCharacterRespec`) and this task's own ТЗ test expectation of a graceful rejection, not a thrown exception the caller cannot even construct a request to attempt.
- Tests: `TransitionCharacterToDead` via both legal paths (success), `GMOverride` by non-MainGm (rejected), a plain owner call claiming neither path is structurally unconstructable (proven by the enum's own two-value shape, not a runtime test), illegal source state (rejected via the existing table), `DevelopmentPool`/`MechanicsRevision` untouched (direct before/after check); `RestoreDeadCharacter` success/from-non-Dead/without-reason/by-non-MainGM rejections, explicit anatomy/resource changes (direct value + revision-only-when-touched checks, both directions), `IsCompensating=0`/`OriginalEventId=NULL` direct `DomainEvents` check, duplicate-`CommandId` for both commands, and a concurrent Lifecycle+Mechanics no-false-conflict check.

No Unity/UI code, no `ADR-022`/`024`/`025` content change, no content invented for `RuntimeState`.

## 5. A real design flaw found and fixed during this task

The first draft of `RestoreDeadCharacterRequest`'s own constructor threw `ArgumentException` on an empty `reasonCode` — inconsistent with this codebase's own established convention (every other "ReasonCode required" rejection returns a graceful `Result.Failure`, not a thrown exception) and with this task's own explicit test requirement ("`RestoreDeadCharacter` без `reasonCode` — отклоняется"). Fixed before any test was written: removed the constructor-level check (the constructor now accepts an empty `reasonCode`, deferring the decision) and added the actual rejection as the first statement inside `RestoreDeadCharacter`'s own method body.

A second, more subtle issue was caught by the test suite's own first run: `RestoreDeadCharacter`'s anatomy-touching branch initially computed the persisted `CharacterAnatomyRevision` column's new value from `current.Anatomy.Revision + 1` (the domain object's own embedded `Revision` field, serialized inside `AnatomyJson`) rather than from `current.Revisions.CharacterAnatomyRevision + 1` (the `Character` table's own authoritative column). These two values are NOT kept in sync by this codebase's own existing code — `InitializeCharacterAnatomy` always seeds `CharacterAnatomy.Revision` at a literal `1` regardless of the column's own value at that moment, so after initialization the two permanently diverge. `RestoreDeadCharacter_WithExplicitAnatomyAndResourceChanges_UpdatesValues_AndOnlyThoseRevisionsIncrease` caught this immediately (`Expected: 3, But was: 2`). Fixed by tracking the two values completely independently — `newAnatomyRevisionColumn` (from `current.Revisions.CharacterAnatomyRevision`, driving the persisted column and `WithRevisions`) versus the `CharacterAnatomy` object's own `Revision` field (from `current.Anatomy.Revision`, driving only the serialized domain object) — exactly mirroring `AddBodyPart`/`ReplaceAnatomyProfile`'s own existing (if quietly divergent) convention, rather than trying to unify them (which is a larger, pre-existing inconsistency well outside this task's own scope to fix).

## 6. Milestones

### M1 — Domain/Application extension

- [x] `LifecycleDeathIssuerKind` enum (Domain); `ICharacterRepository.TransitionCharacterToDead`/`RestoreDeadCharacter`; `RestoreDeadCharacterRequest`/`CharacterRestoreResourceValue` DTOs; `PersistenceFailures`/`ErrorCodes` additions (four new entries).
- [x] Confirmed via `Grep`: no `IssuerKind`/`HostSystem` infrastructure and no `RuntimeState` content definition exist anywhere; `CharacterLifecycleTransitions` already covers every `Dead`-related edge with no Domain change needed.

### M2 — Persistence and tests

- [x] `TransitionCharacterToDead`/`RestoreDeadCharacter` implemented in `SqliteCharacterRepository.cs`; `HistoryEventTypes` extended with the two new event types.
- [x] The `reasonCode`-validation design flaw (section 5) fixed before any test was written.
- [x] A real anatomy-revision-tracking bug (section 5) found by the first test run and fixed.
- [x] 15 new tests in `CharacterDeadRestoredTests.cs`, all passing after the fix.
- [x] `dotnet build`/`dotnet test` full suite green (220/220 persistence tests, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-129`–`143`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check against §9's own expectations.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 7. Progress log

- 2026-09-02 -- Preflight confirmed PR #94's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-111-dead-character-restored`.
- 2026-09-02 -- Read `ADR-025` §6, `ADR-022` §4–6, product §23/§7.1 in full; re-read `ArchiveCharacter`/`ApproveCharacterDraft`, `CharacterLifecycleTransitions.IsValidTransition`. Confirmed via `Grep` that no `IssuerKind`/`HostSystem` infrastructure and no `RuntimeState` content definition exist anywhere.
- 2026-09-02 -- Resolved all three delegated design questions (§1.1/§1.2/§1.3) and the §5 item 2 parameter contract directly from `ADR-025`/product's own text and this codebase's own existing conventions -- no open question needed escalating.
- 2026-09-02 -- Implemented `LifecycleDeathIssuerKind`, `ICharacterRepository` extension, `TransitionCharacterToDead`, `RestoreDeadCharacter`; `dotnet build` passed on first attempt.
- 2026-09-02 -- Found and fixed the `reasonCode`-validation design flaw before writing any test.
- 2026-09-02 -- Wrote 15 tests; one failed on first run (anatomy-revision-tracking bug, section 5), diagnosed and fixed it, all 15 passed after the fix.
- 2026-09-02 -- Full suite green (220/220 persistence tests, 451/451 total, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green; backlog status row updated.

## 8. Decisions

- 2026-09-02 -- Decision: `LifecycleDeathIssuerKind` is a two-value enum, not two mutually-exclusive booleans. Authority: this task's own §1.1 offered both shapes; the enum was chosen for consistency with this codebase's own existing discriminator conventions and because it makes a third "plain owner" value impossible to construct at the type level.
- 2026-09-02 -- Decision: `HostSystemFatalDamageCompletion` performs no `actorIsMainGm` check at all (unconditional acceptance). Authority: `ADR-002` §6.4's `IssuerKind=HostSystem` represents a system-issued call, not a user-issued command — there is no real caller today to validate a permission boundary against, and this task's own §1.1 explicitly frames this branch as "only a structurally legal entry point," not a permission-gated one.
- 2026-09-02 -- Decision: `RestoreDeadCharacter`'s anatomy/resource parameters are whole-list replacements, not diff/patch operations. Authority: `ReplaceAnatomyProfile`'s own already-established whole-list-replacement shape (`ODY-S04-109`) — consistency over inventing a second, divergent "patch" API shape.
- 2026-09-02 -- Decision: `ReasonCode`'s emptiness is validated inside `RestoreDeadCharacter`'s own method body, not in `RestoreDeadCharacterRequest`'s constructor. Authority: this codebase's own established convention for every other "ReasonCode required" rejection, and this task's own explicit test expectation of a graceful `Result.Failure` rather than a thrown exception.
- 2026-09-02 -- Decision: `RuntimeState` is never referenced anywhere in this task's own code, by omission rather than a special-cased guard. Authority: `ADR-022` has no content definition for it anywhere (confirmed by direct search), and product §23.2's "положения/токена" belongs to a separate Board/Scene aggregate, coordinated by the caller after this call returns.

## 9. Discoveries and deviations

- A real `reasonCode`-validation design flaw (a thrown exception instead of a graceful `Result.Failure`) was found and fixed before any test was written -- see section 5.
- A real anatomy-revision-tracking bug (conflating `CharacterAnatomy.Revision`, the domain object's own embedded field, with `CharacterAnatomyRevision`, the `Character` table's own authoritative column -- the two are not kept in sync by this codebase's own existing code) was found by this task's own first test run and fixed -- see section 5. This is a pre-existing, quiet divergence in the codebase (`InitializeCharacterAnatomy` always seeds the embedded field at `1`); this task did not attempt to unify the two values, only to track each correctly on its own terms, matching `AddBodyPart`/`ReplaceAnatomyProfile`'s own existing convention.
- No open architectural question was found that `ADR-022`/`024`/`025` do not already answer; all three of this task's own delegated design questions (§1.1/§1.2/§1.3) plus the §5 item 2 parameter contract were resolved directly from those ADRs'/product's own text.

## 10. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 220/220 `Odyssey.Tests.Persistence` (15 new), 451/451 total, zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 11. Recovery and rollback

Rollback is a normal revert of this branch/PR -- no schema change at all (no new column/table; `TransitionCharacterToDead`/`RestoreDeadCharacter` reuse the `Character` table's own existing `AnatomyJson`/`ResourcesJson`/section-revision columns). Both new interface methods are additive to `ICharacterRepository` -- no existing method's signature or behavior changes.

## 12. Open questions and blockers

None.

## 13. Outcome and follow-up

Draft PR: TBD. CI pending. `ODY-S04-112` (`.odchar` Export/Import) is the next task in the backlog; a future Rules Engine task would be the first real occasion to drive `TransitionCharacterToDead` via the `HostSystemFatalDamageCompletion` path with a genuine completed `FatalDamagePending` workflow, and a future Board/Scene task would be the first real occasion to coordinate `RuntimeState`/token-position restoration alongside a `RestoreDeadCharacter` call.
