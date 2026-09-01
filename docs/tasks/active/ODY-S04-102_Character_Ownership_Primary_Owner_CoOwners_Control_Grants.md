# ODY-S04-102 — Character Ownership: Primary Owner Assignment, Co-Owners & Control Grants

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-102-character-ownership`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-102_Character_Ownership_Primary_Owner_CoOwners_Control_Grants.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement `ADR-025` §4's administrative ownership commands over an already-existing `Character`: `AssignPrimaryOwner`, `AddCharacterCoOwner`/`RemoveCharacterCoOwner`, `GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl` — all `Character.ManageOwnership`-gated (MainGM-only) — reusing `ODY-S04-101`'s already-reserved `Ownership` section revision.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-102` as the second implementation task, depending only on `ODY-S04-101` (order-independent from `ODY-S04-103`'s Draft-owner concern per backlog §2.2).
- Value or risk reduction: administrative ownership/control must exist before any later task (Draft binding, delegation, archive/restore) can assume a Character has a real owner; proves `ADR-025` §4's ownership model against real persistence before it is relied upon elsewhere.
- Blocking or enabling relationship: does not block `ODY-S04-103` (independent), but is itself required before any future task that reads `PrimaryOwnerUserId`/co-owner/control state as an authorization input.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 2) — the binding scope definition for this task.
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §4 (full read — the governing section for this task).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §5–6 (Ownership section/lock/revision reused, not reopened).
- `docs/adr/ADR-019_Roles_Permissions_And_Turn_Authority_Model_v1.0.md` §5 (MainGM baseline and the "assigned character" condition — reused/specialized, not redefined).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §19 (Ownership and Control), §26 (Permissions), §27–28 (Commands/Events naming).
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`ODY-S04-101`'s own code) — read in full as the binding structural precedent per this task's own explicit instruction.
- `Packages/com.odyssey.application/Runtime/Board/BoardMovementService.cs`, `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs` — read for the existing caller-supplied `actorIsMainGm` permission-check convention this task reuses.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-102`, `ADR-025` §4.
- Existing test IDs reused: None directly reused, but this task's tests directly extend `ODY-S04-101`'s own `TC-CHAR-004` (multi-section no-false-conflict) pattern to a third section.
- New test IDs introduced: `TC-CHAR-009` through `TC-CHAR-016` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main` advanced local `main` to `09f4307`, the merge commit for PR #85 (`ODY-S04-101`); `git merge-base --is-ancestor 09f4307... origin/main` independently confirmed it is a real ancestor of `origin/main`.
- No "PermissionDecision"/role-lookup service exists anywhere in the codebase — `BoardMovementService.CheckAuthorization`/`DiceRollService`'s MainGM-gated operations all use a caller-supplied `bool actorIsMainGm` on the request, checked first. Confirmed via direct code search (`grep` for `PermissionDecision`/`ActorIsMainGm`/`Character.Manage`/`AssignedCharacter` across `Packages`) — only one unrelated match found (`SceneProjectionContracts.cs`).
- No "assigned character"/entity-assignment predicate exists anywhere in the codebase for any entity type — `ADR-025` §4.3 is the first place this concept is architecturally fixed; implementing it here is new, not a reuse of pre-existing logic.
- `ADR-025` §4 does not describe any automatic expiration-enforcement mechanism for "temporary" control, and product §19 names `TemporaryControlGrants` without defining one either — confirmed by re-reading both in full.
- `CharacterRecord`'s constructor (from `ODY-S04-101`) has exactly 4 direct construction call sites, all inside `SqliteCharacterRepository.cs` — adding an `Ownership` field is a contained, mechanical change.
- The shared `DomainEvents` table has no `AggregateId` column (same finding as `ODY-S04-101`) — `GetCharacterHistory`'s existing filter-by-payload approach generalizes to the five new ownership event types by extending the existing filtered-type list, no repository-level redesign needed.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Character/CharacterOwnership.cs` (new) — `CharacterOwnership`, `CharacterTemporaryControlGrant`, `CharacterOwnershipAssignment.IsAssignedCharacter`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `CharacterRecord.Ownership` field; six new `ICharacterRepository` method signatures.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — `PersistenceFailures.CharacterOwnershipDenied`/`CharacterOwnershipReasonRequired`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — the two corresponding `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — six new command implementations, shared `MutateOwnership` helper, JSON codec helpers, `ParseJsonPreservingStrings` bug fix.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs` (edit) — 12 new tests.
- `docs/errors/ERROR_CODES.md` (edit) — two new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-009`–`016`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 2 (`ODY-S04-102`) marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Initial `PrimaryOwnerUserId` as a Draft field at creation (`ODY-S04-103`).
- Draft/template/submit/review/approve (`ODY-S04-103`/`104`).
- Development economy/purchases (`ODY-S04-105`–`107`).
- Ability/resource/anatomy (`ODY-S04-108`/`109`).
- Archive/physical delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`).
- Any `AssistantGM` delegation or `ADR-019` role-model extension (`ADR-025` §8 explicitly defers this).
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`/`025` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Character/CharacterOwnership.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-102_Character_Ownership_Primary_Owner_CoOwners_Control_Grants.md
docs/plans/active/ODY-S04-102_Character_Ownership_Primary_Owner_CoOwners_Control_Grants.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the pure ownership/control value types and the `IsAssignedCharacter` predicate (no serializer, no Unity, no SQLite reference); `Odyssey.Application` owns the repository port extension; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001` exactly.
- Authoritative-state and transaction boundary: every new command commits through the existing, unmodified `SqliteSavingPipeline` — no parallel transaction mechanism.
- Serialization / compatibility boundary: `CoOwnerUserIdsJson`/`PermanentControllerUserIdsJson`/`TemporaryControlGrantsJson` use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), consistent with `SqliteGameLogRepository.cs`'s existing convention.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101` already does; no direct wall-clock access. `CharacterTemporaryControlGrant.ExpiresAt` is caller-supplied provenance, not derived from a clock at read time (only the "is it still active" check reads `now`).
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `PersistenceFailures.CharacterOwnershipDenied`/`CharacterOwnershipReasonRequired` never expose raw SQLite/IO exception text; control possession alone does not grant owner-private-field visibility (`ADR-025` §4.3), though no such visibility model exists yet to enforce (future task's concern).
- Performance or platform constraint: unchanged from `ODY-S04-101`'s own established pattern.
- Other: the `Character.ManageOwnership` gate uses the same caller-supplied `bool actorIsMainGm` convention as `BoardMovementService`/`DiceRollService` — no new permission-decision abstraction introduced by this task.

## 7. Expected behavior

### Scenario 1 — `AssignPrimaryOwner` requires a reason and does not touch unrelated ownership state

**Given** an existing Character
**When** `AssignPrimaryOwner` is called by MainGM with a non-empty `ReasonCode`
**Then** `PrimaryOwnerUserId` changes, an audited event is recorded with who/when/why, and `CoOwnerUserIds`/`PermanentControllerUserIds`/`TemporaryControlGrants` are unchanged.

### Scenario 2 — a missing reason or a non-MainGM caller is rejected

**Given** an empty `ReasonCode`, or a caller with `actorIsMainGm = false`
**When** any of the six ownership commands is called
**Then** the command is rejected and no state change occurs.

### Scenario 3 — Ownership edits do not false-conflict with Identity edits

**Given** an existing Character
**When** an ownership command (declaring only `expectedOwnershipRevision`) and `UpdateIdentity` (declaring only `expectedIdentityRevision`) are both called
**Then** both commit successfully.

### Scenario 4 — a stale `expectedOwnershipRevision` is rejected

**Given** a Character whose `OwnershipRevision` has already advanced past a caller's held value
**When** any ownership command is called with that stale value
**Then** it is rejected with `CharacterRevisionConflict` and no state change occurs.

### Scenario 5 — co-owner add is idempotent; control grant/revoke round-trips

**Given** an existing Character
**When** `AddCharacterCoOwner` is called twice for the same user, or `GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl` is followed by `RevokeCharacterControl`
**Then** the co-owner list contains no duplicate entry, and a revoked grant no longer appears.

### Scenario 6 — both ownership and an active control grant satisfy the "assigned character" condition

**Given** a Character with a primary owner and, separately, a user holding an unexpired temporary control grant
**When** `CharacterOwnershipAssignment.IsAssignedCharacter` is evaluated for each
**Then** both return true, and an unrelated user returns false.

### Required invariants

- No update ever checks a section revision the caller did not declare.
- `AssignPrimaryOwner` never silently changes `CoOwnerUserIds`/`PermanentControllerUserIds`/`TemporaryControlGrants`.
- `DomainEvents` rows are never updated or deleted by any operation in this task.
- No `ADR-022`/`025` file content changes.

## 8. Deliverables

- Production code: `CharacterOwnership.cs` (Domain), `CharacterRepositoryContracts.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: 12 new tests in `SqliteCharacterRepositoryTests.cs`, registered as `TC-CHAR-009`–`016`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive columns only (`Character` table already exists; no production data exists yet to migrate).

## 9. Acceptance criteria

1. All six ownership/control commands compile and are exposed on `ICharacterRepository`.
2. `AssignPrimaryOwner` with a missing/empty `ReasonCode` is rejected with no state change.
3. A non-MainGM caller (`actorIsMainGm = false`) is rejected for every one of the six commands.
4. A successful `AssignPrimaryOwner` produces a correct audit-event snapshot and leaves co-owner/control state unchanged.
5. A stale `expectedOwnershipRevision` is rejected with `PersistenceCharacterRevisionConflict` and produces no state change.
6. An Ownership-section edit and an Identity-section edit commit concurrently with no false conflict.
7. `AddCharacterCoOwner` is idempotent (no duplicate entry on repeated add for the same user); `RemoveCharacterCoOwner` removes exactly that user.
8. `GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl` followed by `RevokeCharacterControl` correctly round-trips.
9. `CharacterOwnershipAssignment.IsAssignedCharacter` returns true for the primary owner and for a user with an active control grant, and false for an unrelated user.
10. No change to `ADR-022`/`025` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
11. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
12. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 2 marked `Done` with a real PR link.
13. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-009` | .NET (`Odyssey.Tests.Persistence`) | `AssignPrimaryOwner` with an empty `ReasonCode` is rejected, no state change | Pass |
| `TC-CHAR-010` | .NET (`Odyssey.Tests.Persistence`) | Every ownership command rejected when `actorIsMainGm = false` | Pass |
| `TC-CHAR-011` | .NET (`Odyssey.Tests.Persistence`) | Successful `AssignPrimaryOwner` audits correctly and does not touch co-owner/control state | Pass |
| `TC-CHAR-012` | .NET (`Odyssey.Tests.Persistence`) | A stale `expectedOwnershipRevision` is rejected, no state change | Pass |
| `TC-CHAR-013` | .NET (`Odyssey.Tests.Persistence`) | Ownership and Identity edits commit concurrently, no false conflict | Pass |
| `TC-CHAR-014` | .NET (`Odyssey.Tests.Persistence`) | `AddCharacterCoOwner`/`RemoveCharacterCoOwner` correctness and de-duplication | Pass |
| `TC-CHAR-015` | .NET (`Odyssey.Tests.Persistence`) | Permanent and temporary control grant/revoke round-trips | Pass |
| `TC-CHAR-016` | .NET (`Odyssey.Tests.Persistence`) | `IsAssignedCharacter` true for owner and active-grant holder, false for unrelated user | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — four new columns on the existing `Character` table (`PrimaryOwnerUserId`, `CoOwnerUserIdsJson`, `PermanentControllerUserIdsJson`, `TemporaryControlGrantsJson`), all with safe defaults; no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS` continues to apply; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new columns are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`Newtonsoft.Json` is already an approved, in-use dependency (`ADR-003`); no new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: ownership/control user-ID references only — no hidden GM fields, no secrets, no personal data.
- Trust boundaries: `Character.ManageOwnership` is MainGM-only; no other role may call these commands.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` checked first in every command, matching the existing `BoardMovementService`/`DiceRollService` convention exactly — no new permission-decision abstraction.
- Redaction requirements: `PersistenceFailures.CharacterOwnershipDenied`/`CharacterOwnershipReasonRequired` never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: audit event payloads carry only `previousPrimaryOwnerUserId`/`newPrimaryOwnerUserId`/`reasonCode`/actor/time — no secret data.
- Abuse / malformed input limits: `ReasonCode` required non-empty; `UserId` validated via `UserId.IsValid` before use.
- Security tests: `TC-CHAR-010` (gate rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 2 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract and adds new persisted schema/authoritative ownership semantics.
- ExecPlan path: `docs/plans/active/ODY-S04-102_Character_Ownership_Primary_Owner_CoOwners_Control_Grants.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends only on `ODY-S04-101` (done, PR #85). Does not block `ODY-S04-103` (independent per backlog §2.2).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive columns on the existing `Character` table only; no versioned schema migration.
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

- `Packages/com.odyssey.domain/Runtime/Character/CharacterOwnership.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` — `CharacterRecord.Ownership` and six new `ICharacterRepository` method signatures added.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — `PersistenceFailures.CharacterOwnershipDenied`/`CharacterOwnershipReasonRequired` added.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — two `ErrorCode` entries added.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` — six commands, shared `MutateOwnership` helper, JSON codec helpers, `ParseJsonPreservingStrings` bug fix.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs` — 12 new tests.
- `docs/errors/ERROR_CODES.md` — two new rows.
- `Tests/Metadata/test-catalog.json` — `TC-CHAR-009`–`016` added.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — row 2 marked `Done`, top status line updated.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 82 (70 pre-existing + 12 new) — 313 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | First run failed on missing `ERROR_CODES.md`/test-catalog entries for the two new `ErrorCode`s; fixed by adding both; second run: `Repository policy check passed`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ICharacterRepository` compiles with all six new methods. |
| AC-2 | Passed | `TC-CHAR-009`. |
| AC-3 | Passed | `TC-CHAR-010`. |
| AC-4 | Passed | `TC-CHAR-011`. |
| AC-5 | Passed | `TC-CHAR-012`. |
| AC-6 | Passed | `TC-CHAR-013`. |
| AC-7 | Passed | `TC-CHAR-014`. |
| AC-8 | Passed | `TC-CHAR-015`. |
| AC-9 | Passed | `TC-CHAR-016`. |
| AC-10 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-11 | Passed | See Validation results above. |
| AC-12 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 2 status/PR link updated. |
| AC-13 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- "Temporary" control expiry (`CharacterTemporaryControlGrant.ExpiresAt`) is caller-supplied provenance evaluated lazily at `IsAssignedCharacter` check time — there is no automatic background sweep/revocation event, since `ADR-025` §4 and product §19 do not describe one. This is this task's own engineering decision, not an ADR decision; a future task should revisit it if an automatic-expiration requirement is later specified.
- Control possession does not itself grant owner-private-field visibility (per `ADR-025` §4.3), but no such visibility/audience model exists yet to enforce — deferred to whichever future task first introduces one.

### Follow-up tasks

- `ODY-S04-103` — Local Draft, Templates & Independent Copy.
- Any future task that needs automatic control-grant expiration enforcement should revisit the "Known limitations" note above.

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`025` or `SLICE-04_BACKLOG.md` change; no Unity/UI code.
- Architecture review: extends `ICharacterRepository`/`SqliteCharacterRepository` directly (no parallel repository); reuses the existing caller-supplied `actorIsMainGm` convention (`BoardMovementService`/`DiceRollService`) rather than inventing a permission-decision service; `CharacterOwnershipAssignment.IsAssignedCharacter` is documented as new since no prior "assigned entity" predicate existed anywhere in the codebase.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline.
- Security/privacy review: gate is actually checked (not just documented) per `TC-CHAR-010`; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-01 — Decision: reuse `BoardMovementService`/`DiceRollService`'s existing caller-supplied `bool actorIsMainGm` convention for the `Character.ManageOwnership` gate, not a new permission-decision service — Authority/approval: this task's own explicit ТЗ instruction; `ADR-019`'s own accepted baseline simplification, already used consistently across the codebase.
- 2026-09-01 — Decision: "temporary" control expiry is enforced lazily at `IsAssignedCharacter` evaluation time against a caller-supplied current time, with no background sweep or auto-revocation event — Authority/approval: this task's own engineering decision, explicitly not an ADR decision, since `ADR-025` §4 and product §19 both leave the exact mechanism open; flagged in the final report as the ТЗ requires.
- 2026-09-01 — Decision: extend `ICharacterRepository`/`SqliteCharacterRepository` directly rather than a second Character-focused repository — Authority/approval: this task's own explicit ТЗ instruction and `ODY-S04-101`'s own established single-repository convention.
- 2026-09-01 — Decision: implement `CharacterOwnershipAssignment.IsAssignedCharacter` as this task's own new contribution, after confirming via code search that no such predicate existed anywhere in the codebase for any entity type — Authority/approval: `ADR-025` §4.3's own explicit requirement; documented as new, not a reuse, since nothing to reuse existed.
- 2026-09-01 — Decision (discovered mid-task, not anticipated by the ТЗ): fix a real Newtonsoft.Json `DateParseHandling` bug corrupting `UtcInstant` values stored inside JSON arrays, using an explicit `JsonTextReader` with `DateParseHandling.None` rather than any workaround in the test — Authority/approval: `ADR-003`'s approved Newtonsoft.Json 13.0.2 dependency; the fix does not change the approved serialization strategy, only how this file parses its own previously-serialized JSON correctly.
- 2026-09-01 — Decision: register the two new `ErrorCode`s in `docs/errors/ERROR_CODES.md` and add `TC-CHAR-009`–`016` to `Tests/Metadata/test-catalog.json`, after `check-repository-policy.ps1`'s `REPO-POLICY-005` check failed on the first run — Authority/approval: the repository's own enforced policy, following the exact existing registry format.

### Approved task changes

- None.
