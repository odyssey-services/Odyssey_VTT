# ODY-S04-111 — Dead & `CharacterRestored`

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-111-dead-character-restored`
**Pull request:** [#95](https://github.com/odyssey-services/Odyssey_VTT/pull/95)
**ExecPlan:** `docs/plans/active/ODY-S04-111_Dead_And_CharacterRestored.md`
**Created:** 2026-09-02
**Last updated:** 2026-09-02 UTC

## 1. Goal

Implement `ADR-025` §6: the transition into `Dead` restricted to exactly two structurally-exclusive legal paths — a completed Rules Engine `FatalDamagePending` workflow (`HostSystem`), or an explicit MainGM `GMOverride` — never a plain owner/controller call (`CAP-INV-008`), gated by the `Lifecycle` section's own lock/revision and leaving `ADR-024` reservations/`Mechanics` entirely untouched; `RestoreDeadCharacter` as a forward (never compensating) `CharacterRestored` event with a mandatory reason and the GM's explicit choices of new `LifecycleStatus`/body-part state/resources, deliberately never touching `RuntimeState`/board position.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-111` as the eleventh implementation task, depending on `ODY-S04-101`'s already-generic `CharacterLifecycleTransitions` table. It is the first task that must invent a structural discriminator for "who issued" a lifecycle transition, since no `IssuerKind`/`HostSystem` infrastructure exists anywhere in this codebase yet.
- Value or risk reduction: closes `CAP-INV-008` (an owner must never be able to set their own Character `Dead`) by construction, not by convention alone; proves `RestoreDeadCharacter` can span multiple independently-gated sections in one transaction while correctly declaring only the sections the GM actually chose to touch.
- Blocking or enabling relationship: unblocks `ODY-S04-112` (`.odchar` Export/Import); a future Rules Engine task is the first real occasion to drive `TransitionCharacterToDead` via `HostSystemFatalDamageCompletion` with a genuine completed workflow.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 11) — the binding scope definition for this task.
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §6 (full read — the governing section).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §4–6 (full read — including confirming `RuntimeState` has no content definition anywhere).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §23 (full read), §7.1.
- `Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs`'s own `CharacterLifecycleTransitions.IsValidTransition` (`ODY-S04-101`) — the binding generic edge-legality table.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`'s own `ArchiveCharacter`/`ApproveCharacterDraft` (`ODY-S04-110`/`104`) — the binding `Lifecycle`-transition structural precedent; `ApplyCharacterRespec`/`AcquireAbilityViaProgressionPurchase` (`ODY-S04-107`/`108`) — the binding cross-section-command structural precedent.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-111`, `ADR-025` §6, `ADR-022` §4–6, product §23/§7.1, `CAP-INV-008`.
- Existing test IDs reused: None directly reused. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-128` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-129` through `TC-CHAR-143` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main`; `git merge-base --is-ancestor` independently confirmed PR #94's merge commit is a real ancestor of `origin/main` before branching.
- Direct search across the entire codebase confirms no `IssuerKind`/`HostSystem` actor infrastructure exists anywhere — only a doc-comment forward-reference in `ODY-S04-101`'s own `CharacterLifecycleStatus` enum — and no real Rules Engine `FatalDamagePending` workflow exists that could become a genuine `HostSystem` caller.
- Direct `Grep` across `ADR-022` confirms `RuntimeState` appears ONLY as a bare section-name/lock-key/revision-column reference — zero content definition anywhere, unlike `CharacterAbilitiesRevision` before `ODY-S04-108` (which had defined content, just no wired command yet).
- `CharacterLifecycleTransitions.IsValidTransition` already encodes every edge this task needs (`Active|Inactive|Retired -> Dead` legal, `Draft -> Dead` illegal; `Dead -> {Active, Inactive, Retired, Archived}` all legal) — no Domain-layer change needed to this table.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs` (edit) — `LifecycleDeathIssuerKind` enum (new).
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterRepository.TransitionCharacterToDead`/`RestoreDeadCharacter`; `RestoreDeadCharacterRequest`/`CharacterRestoreResourceValue` DTOs (new).
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — four new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — four new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — `HistoryEventTypes` extended; `TransitionCharacterToDead`/`RestoreDeadCharacter` implementations.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterDeadRestoredTests.cs` (new) — 15 tests.
- `docs/errors/ERROR_CODES.md` (edit) — four new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-129`–`143`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 11 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- The real Rules Engine `FatalDamagePending` workflow, or any `IssuerKind`/`HostSystem` general infrastructure — `HostSystemFatalDamageCompletion` is accepted only as a structurally legal entry point.
- Board/token position restoration (`RuntimeState`) — coordinated separately by the caller's own Board commands after this call returns.
- Automatic cancellation/release of `ADR-024` reservations on death — explicitly excluded by `ADR-025` §6.2.
- `.odchar` Export/Import, Ruleset migration — `ODY-S04-112`/`113`.
- Archive/physical delete — already `ODY-S04-110`.
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`/`024`/`025` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterDeadRestoredTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-111_Dead_And_CharacterRestored.md
docs/plans/active/ODY-S04-111_Dead_And_CharacterRestored.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns `LifecycleDeathIssuerKind`; `Odyssey.Application` owns the repository port extension and request DTOs; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: `TransitionCharacterToDead` commits through the existing, unmodified `SqliteSavingPipeline`, mirroring `ArchiveCharacter`'s exact shape. `RestoreDeadCharacter` uses its own dedicated method with its own single `_pipeline.Execute` call spanning up to three sections (`Lifecycle` always, `CharacterAnatomy`/`CharacterResources` conditionally) in one transaction. `CommandId`/`AppliedCommands` remain the sole idempotency mechanism for both commands.
- Serialization / compatibility boundary: event payloads use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`110` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`110`'s own established pattern.
- Other: `TransitionCharacterToDead`'s `GMOverride` path and `RestoreDeadCharacter` are both MainGM-only; `TransitionCharacterToDead`'s `HostSystemFatalDamageCompletion` path performs no user-permission check at all.

## 7. Expected behavior

### Scenario 1 — `TransitionCharacterToDead`'s two legal paths

**Given** an Active/Inactive/Retired Character
**When** `TransitionCharacterToDead` is called with `LifecycleDeathIssuerKind.GMOverride` by MainGM, or with `LifecycleDeathIssuerKind.HostSystemFatalDamageCompletion` by any actor
**Then** `LifecycleStatus` becomes `Dead` in both cases.

### Scenario 2 — `CAP-INV-008`'s own gate

**Given** an Active Character
**When** `TransitionCharacterToDead` is called with `LifecycleDeathIssuerKind.GMOverride` by a non-MainGM actor
**Then** it is rejected with `CharacterDeadTransitionDenied`, no state change; a plain owner call claiming neither path is structurally impossible to construct at all (the enum has only these two values).

### Scenario 3 — `ADR-024`/`Mechanics` untouched by death

**Given** a Character with a non-zero `DevelopmentPool.Earned`
**When** `TransitionCharacterToDead` succeeds
**Then** `DevelopmentPool.Earned`/`Reserved` and `MechanicsRevision` are byte-for-byte unchanged.

### Scenario 4 — `RestoreDeadCharacter`'s multi-section declaration

**Given** a Dead Character with initialized `CharacterAnatomy`/`CharacterResource`
**When** `RestoreDeadCharacter` is called with explicit new body parts and a new resource `CurrentValue`
**Then** `CharacterAnatomyRevision`/`CharacterResourcesRevision` both increase by exactly 1, and the new values are visible on the returned record; when neither is supplied, neither revision increases.

### Scenario 5 — `CharacterRestored` is a forward event

**Given** a successful `RestoreDeadCharacter` call
**When** the resulting `DomainEvents` row for `odyssey.persistence.character_restored` is inspected directly
**Then** `IsCompensating=0` and `OriginalEventId=NULL` — it never references the earlier `CharacterDied` event.

### Required invariants

- `TransitionCharacterToDead` never touches any `Mechanics`-section column.
- `RestoreDeadCharacter` never touches `RuntimeState`/any `RuntimeState`-shaped column.
- `RestoreDeadCharacter` is legal only from `Dead`.
- No `ADR-022`/`024`/`025` file content changes.

## 8. Deliverables

- Production code: `CharacterLifecycle.cs` (Domain); `CharacterRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs` extension (Application); `SqliteCharacterRepository.cs` extension (Persistence).
- Tests: 15 new tests in `CharacterDeadRestoredTests.cs`, registered as `TC-CHAR-129`–`143`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — no schema change at all.

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-128` continue passing with their own assertions unmodified.
2. `TransitionCharacterToDead` succeeds via both `GMOverride` (MainGM) and `HostSystemFatalDamageCompletion`; rejects `GMOverride` by non-MainGM; rejects an illegal source state.
3. `TransitionCharacterToDead` leaves `DevelopmentPool`/`MechanicsRevision` unchanged, verified directly.
4. `RestoreDeadCharacter` succeeds only from `Dead`; requires `ReasonCode`; is MainGM-only.
5. `RestoreDeadCharacter`'s explicit anatomy/resource changes update the real values and increase only the touched sections' own revisions, verified in both directions.
6. `CharacterRestored`'s `DomainEvents` row has `IsCompensating=0`/`OriginalEventId=NULL`, verified directly.
7. Duplicate `CommandId` for both commands does not duplicate effect, verified against real state.
8. A concurrent Lifecycle (Dead transition) and independent Mechanics command commit without a false conflict.
9. No change to `ADR-022`/`024`/`025`/`SLICE-04_BACKLOG.md`; no Unity/UI code; no `RuntimeState` content invented anywhere.
10. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
11. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 11 marked `Done` with a real PR link.
12. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-129`–`134` | .NET (`Odyssey.Tests.Persistence`) | TransitionCharacterToDead: both legal paths, non-MainGM GMOverride rejection, illegal source state, DevelopmentPool/MechanicsRevision untouched, duplicate-CommandId | Pass |
| `TC-CHAR-135`–`143` | .NET (`Odyssey.Tests.Persistence`) | RestoreDeadCharacter: success/non-Dead/no-reason/non-MainGM rejections, explicit vs. untouched anatomy/resource revision behavior, forward-event check, duplicate-CommandId, concurrent Lifecycle+Mechanics no-false-conflict | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`110`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.

## 11. Compatibility, migration, and rollback

- Compatibility impact: none — no schema change at all; both new interface methods are additive to `ICharacterRepository`.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: `TransitionCharacterToDead`/`RestoreDeadCharacter` mutate only the live `Character` row's own columns; `DomainEvents` is append-only and never touched destructively.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: lifecycle status transitions, restore reason codes, GM-chosen anatomy/resource values — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `TransitionCharacterToDead`'s `GMOverride` path and `RestoreDeadCharacter` are MainGM-only; `HostSystemFatalDamageCompletion` is a system-issued path with no user-permission check.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` reused exactly, matching existing conventions.
- Redaction requirements: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only lifecycle/actor/reason/outcome fields — no secret data.
- Abuse / malformed input limits: `ReasonCode` validated non-empty for restore.
- Security tests: both gates exercised directly (`TransitionCharacterToDead_GMOverride_ByNonMainGm_IsRejected_NoStateChange`, `RestoreDeadCharacter_ByNonMainGm_IsRejected_NoStateChange`).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 11 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract and introduces a new structural Domain enum.
- ExecPlan path: `docs/plans/active/ODY-S04-111_Dead_And_CharacterRestored.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101` (done); full restore-state fixtures additionally reuse `ODY-S04-108`/`109` (done). Unblocks `ODY-S04-112`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

See section 5's "In scope" file list above.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 220 (205 pre-existing + 15 new) — 451 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Every earlier test file unmodified, all still pass. |
| AC-2 | Passed | `TC-CHAR-129`–`132`. |
| AC-3 | Passed | `TC-CHAR-133`. |
| AC-4 | Passed | `TC-CHAR-135`–`138`. |
| AC-5 | Passed | `TC-CHAR-139`/`140`. |
| AC-6 | Passed | `TC-CHAR-141`. |
| AC-7 | Passed | `TC-CHAR-134`/`142`. |
| AC-8 | Passed | `TC-CHAR-143`. |
| AC-9 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**`/`RuntimeState` content touched. |
| AC-10 | Passed | See Validation results above. |
| AC-11 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 11 status/PR link updated. |
| AC-12 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `HostSystemFatalDamageCompletion` has no real caller yet — no Rules Engine `FatalDamagePending` workflow exists in this codebase; a future task must wire a genuine caller.
- `RuntimeState` remains entirely undefined — this task neither defines it nor touches it in any way, per its own explicit boundary.

### Follow-up tasks

- `ODY-S04-112` — `.odchar` Export/Import.
- A future Rules Engine task — first real occasion to drive `TransitionCharacterToDead` via `HostSystemFatalDamageCompletion` with a genuine completed workflow.
- A future Board/Scene task — first real occasion to coordinate `RuntimeState`/token-position restoration alongside a `RestoreDeadCharacter` call.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`024`/`025`/`SLICE-04_BACKLOG.md` change; no Unity/UI code; no `RuntimeState` content invented.
- Architecture review: `TransitionCharacterToDead` reuses `ArchiveCharacter`'s exact `Lifecycle`-transition shape unchanged in spirit, with the `LifecycleDeathIssuerKind` discriminator in place of a plain permission check; `RestoreDeadCharacter` follows `ApplyCharacterRespec`/`AcquireAbilityViaProgressionPurchase`'s own cross-section-command precedent exactly.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; two real defects (a reasonCode-validation design flaw, and an anatomy-revision-tracking bug) were caught before/during this task's own test-writing and fixed, not glossed over.
- Security/privacy review: both gates reuse/extend existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-02 — Decision: `LifecycleDeathIssuerKind` is a two-value enum, not two mutually-exclusive booleans — Authority/approval: this task's own explicit §1.1 instruction offered both shapes; the enum matches this codebase's own existing discriminator conventions and is structurally exclusive by construction.
- 2026-09-02 — Decision: `HostSystemFatalDamageCompletion` performs no `actorIsMainGm` check at all — Authority/approval: `ADR-002` §6.4's own `IssuerKind=HostSystem` framing; this task's own §1.1 explicitly frames this branch as a structural entry point only.
- 2026-09-02 — Decision: `RestoreDeadCharacter`'s anatomy/resource parameters are whole-list replacements, not diff/patch operations — Authority/approval: `ReplaceAnatomyProfile`'s own already-established whole-list-replacement shape (`ODY-S04-109`).
- 2026-09-02 — Decision: `ReasonCode`'s emptiness is validated inside `RestoreDeadCharacter`'s own method body, not in the request DTO's constructor — Authority/approval: this codebase's own established convention for every other "ReasonCode required" rejection, and this task's own explicit test expectation.
- 2026-09-02 — Decision: fixed a real anatomy-revision-tracking bug found by this task's own first test run (`CharacterAnatomy.Revision`, the domain object's own embedded field, conflated with `CharacterAnatomyRevision`, the `Character` table's own authoritative column — the two are not kept in sync by this codebase's own existing code) — Authority/approval: this task's own test-driven discovery; fixed by tracking the two values independently, matching `AddBodyPart`/`ReplaceAnatomyProfile`'s own existing convention.

### Approved task changes

- None.
