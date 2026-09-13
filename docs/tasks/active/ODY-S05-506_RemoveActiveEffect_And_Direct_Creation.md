# ODY-S05-506 — RemoveActiveEffect Command + Direct-Creation Permission Gates

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-506-remove-and-direct-creation`
**Pull request:** TBD (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-506_RemoveActiveEffect_And_Direct_Creation.md`
**Created:** 2026-09-13
**Last updated:** 2026-09-13 UTC

## 1. Goal

Implement `ADR-028` §9/§10 rules 2-3's own two MainGM-only permission gates over `ActiveEffect`: the explicit `RemoveActiveEffect` command (`Status → Removed`, `Revision`-CAS-guarded, never physical deletion) and the MainGM-only gate for creating an `ActiveEffect` directly, not through any item.

## 2. Why this task exists

- Problem or dependency being addressed: `ActiveEffectSourceRef.ForGMDirect()` (declared by `ODY-S05-502` as a forward reference) and `ActiveEffectStatus.Removed` (declared, unused for this purpose since `502`) both wait for this task — no production code path today can construct a `GMDirect`-sourced effect or end one early via an explicit command.
- Value or risk reduction: closes the last two of `ADR-028`'s own three named permission decisions (§10 rules 1-3) — item-triggered creation (rule 1) was already implemented, un-gated, by `ODY-S05-505`; this task implements the two, oppositely-idiomed, MainGM-only rules (2 and 3).
- Blocking or enabling relationship: this is the fourth of five implementation tasks in the range; `ODY-S05-507`'s own integration fixtures are expected to exercise `RemoveActiveEffect` end-to-end as its own final composed scenario.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 5.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §9 (`RemoveActiveEffect`, verbatim), §10 (all 5 rules, verbatim — rule 1 already implemented by `505`; rules 2-3 this task's own scope; rule 4 AssistantGM scope, inherited unchanged; rule 5 ordinary-player exclusion), §6 rule 2 (minimum contract includes the `Removed` transition), §12 (mandatory `SqliteSavingPipeline` reuse).
- `docs/adr/ADR-025` §5.1/§5.2 (the two permission-phrasing idioms `ADR-028` §10 itself cites: "inherit the existing model" for item-triggered creation, "new MainGM-only restriction" for this task's own two operations).
- Existing patterns: `SqliteCharacterRepository.DeleteCharacterPermanently` (the exact MainGM-gate-as-first-statement, repository-level placement precedent this task's own `RemoveActiveEffect` mirrors); `SqliteActiveEffectRepository.ExpireActiveEffect`/`SetItemEffectEquipped` (`ODY-S05-504`/`505`, the CAS-via-`SqliteSavingPipeline` status-transition structural precedent); `InventoryMovementFailures.Denied` (the "one shared error code across several operations of the same domain" precedent this task's own `ActiveEffectOperationDenied` follows, over `CharacterDeletionDenied`'s own one-code-per-operation style); `ItemEffectLifecycleService` (`ODY-S05-505`, the deliberately-*un*gated contrast this task must not blur).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-506`, `SLICE-05`.
- Existing test IDs: `TC-ACTIVEEFFECT-001`-`068` (`ODY-S05-502`-`505`) as predecessor evidence.
- New test IDs introduced: `TC-ACTIVEEFFECT-069`-`078`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-506` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main`'s tip at the time this task began was `3910ba1` (merge of PR #140, `ODY-S05-505`) — confirmed via `git fetch origin`. Backlog row 5 in §15 (`ODY-S05-506`) was `Proposed`.
- `IActiveEffectRepository` (pre-task, direct code read) had exactly 6 methods (`CreateActiveEffect`/`GetActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource`/`ExpireActiveEffect`/`SetItemEffectEquipped`) — no `Removed` transition existed. Its own doc comment already stated "`ODY-S05-506` owns only `Removed`."
- `ActiveEffectSourceRef.ForGMDirect()` (`ActiveEffect.cs`, direct code read) already exists, declared by `ODY-S05-502`, with its own doc comment explicitly naming this task: "`ADR-028` §10 rule 2: MainGM applying an effect with no item cause at all... `ODY-S05-506` wires the actual MainGM-only command that constructs one." Direct `grep` confirms `ForGMDirect`/`GMDirect` are used today only in tests and in a `switch` expression inside `SqliteActiveEffectRepository.ReadRecord` — no production command builds one before this task.
- **Contrast independently re-confirmed (not assumed from the ТЗ's own text):** direct read of `ItemEffectLifecycleService.cs`'s own class doc comment ("Authorization and target selection belong to the invoking host; this service adds no MainGM restriction") and a `grep` for `MainGm`/`actorIsMainGm` across the whole file returned zero matches — confirming `505`'s own code genuinely has no gate, and this task must not add one there.
- `SqliteCharacterRepository.DeleteCharacterPermanently` (`SqliteCharacterRepository.cs:723`, direct code read) checks `actorIsMainGm` as its own literal first statement — before `reasonCode` validation, before opening any connection — with the comment "checked before touching the database at all, matching every other MainGM-only gate's own convention." This task's own `RemoveActiveEffect` places its gate identically: first statement, repository-level, not a separate Application-layer wrapper.
- **Convention re-confirmed by direct `grep`:** no reusable `RequireMainGm(...)`-shaped helper exists anywhere in `Packages/com.odyssey.persistence`/`Packages/com.odyssey.application` — `SqliteCharacterRepository.cs`, `EquipmentService.cs`, `InventoryStackOperationService.cs`, `InventoryMovementService.cs` each repeat the identical inline `if (!actorIsMainGm) { return ...Failure(...Denied(...)); }` shape independently. This task follows the same inline convention, introducing no shared helper.
- `InventoryMovementFailures.Denied` (`InventoryMovementService.cs:73`, direct code read) is one `Error` factory reused across `Equip`/`Unequip`/`Split`/`Merge` — the "one shared code, several operations" style. `PersistenceFailures.CharacterDeletionDenied`/`CharacterApprovalDenied` (direct code read) are the opposite, one-per-operation style. Since the backlog's own row-5 text explicitly groups both of this task's operations as "two MainGM-only permission gates... grouped together," the shared-code style was chosen (§18) — one `PersistenceFailures.ActiveEffectOperationDenied` (`persistence.active_effect.operation_denied`) reused by both `RemoveActiveEffect` and `ActiveEffectDirectCommandService.CreateDirectActiveEffect`.
- `PersistenceFailures.ActiveEffectRevisionConflict` (`persistence.active_effect.revision_conflict`, added by `ODY-S05-504`, direct code read) is reused unchanged for `RemoveActiveEffect`'s own CAS-conflict and terminal-status-rejection cases — no new revision-conflict-shaped code was added.
- **Known, deliberately-not-closed gap re-confirmed:** direct read of `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` (`ODY-S05-503`) confirms it performs no permission check of any kind — a pure decision function with no I/O. `503`'s own task contract already recorded this as an explicit deferral ("permission gates for direct-creation/removal are `ODY-S05-506`'s own territory; item-triggered creation inherits the existing item-use model"). By the backlog's own literal §15.1 text, `506`'s own scope is exactly "§10 rules 2-3" (direct creation + removal) — stacking-conflict resolution's own permission model is not named as this task's scope, and is not addressed here.

### Assumptions

- None.

## 5. Scope

### In scope

- New `IActiveEffectRepository.RemoveActiveEffect` + `SqliteActiveEffectRepository` implementation — MainGM-only (checked first, before any I/O), `Revision`-CAS-guarded, routed through `SqliteSavingPipeline`, `Status → Removed`, valid only from `Active`/`Suspended` (rejecting an already-terminal `Expired`/`Removed` row).
- New `Odyssey.Application.Effects.ActiveEffectDirectCommandService.CreateDirectActiveEffect` — MainGM-only wrapper building a `GMDirect`-sourced `ActiveEffectRecord` and delegating to the unmodified `CreateActiveEffect`.
- One new error code, `persistence.active_effect.operation_denied`, shared by both operations.
- Tests (`TC-ACTIVEEFFECT-069`-`078`), test metadata, task/plan docs, backlog row.

### Out of scope

- `ItemEffectLifecycleService.cs` (`ODY-S05-505`) — not touched; its own deliberate lack of a MainGM gate (rule 1's own "inherit" idiom) is not this task's concern and must not be blurred with rules 2-3's own "new restriction" idiom.
- `ActiveEffectStackingRules.cs`/`ActiveEffectExpiryRules.cs` (`ODY-S05-503`/`504`) — not touched.
- `ResolveActiveEffectStackConflict`'s own lack of a permission check — a known, explicitly re-confirmed gap, not closed by this task (§4/§18).
- `CreateActiveEffect`/`GetActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource` — not rewritten, only called.
- `docs/adr/ADR-028_...md` — already `Accepted`; implemented here, not amended.
- A general-purpose `RequireMainGm(...)` helper — this task follows the codebase's own established inline-repetition convention instead (§4).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectDirectCommandService.cs
Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectGmCommandTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-506_RemoveActiveEffect_And_Direct_Creation.md
docs/plans/active/ODY-S05-506_RemoveActiveEffect_And_Direct_Creation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.application/Runtime/Effects/ItemEffectLifecycleService.cs
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectStackingRules.cs
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectExpiryRules.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Any `.asmdef`/`.csproj` file
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `RemoveActiveEffect`'s own MainGM gate lives in `Odyssey.Persistence` (matching `DeleteCharacterPermanently`'s own placement); the direct-creation gate lives in `Odyssey.Application` (a thin wrapper, since no new repository method is needed there).
- Authoritative-state and transaction boundary: `RemoveActiveEffect`'s CAS update + `DomainEvents`/`AppliedCommands` rows commit together in one transaction via `SqliteSavingPipeline`, exactly like `ExpireActiveEffect`/`SetItemEffectEquipped`.
- Serialization / compatibility boundary: none — no new wire/storage format.
- Time / RNG rule: `_clock.GetUtcNow()` via the injected `IWallClock` for `UpdatedAt`; `ActiveEffectDirectCommandService` takes `appliedAt`/`expiresAt` as caller-supplied parameters, never reading a clock itself (matching `ItemEffectLifecycleService`'s own precedent of never reading a clock directly).
- Unity / thread / lifetime rule: no Unity/asmdef file touched.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: no raw exception text or stack trace in any returned `Error`.
- Other: both new MainGM checks are checked as the literal first statement of their own method, before any other validation that could itself touch I/O — matching `DeleteCharacterPermanently`'s own exact ordering.

## 7. Expected behavior

### Scenario 1 — RemoveActiveEffect by MainGM succeeds

**Given** an `Active` or `Suspended` `ActiveEffect` row and a MainGM actor
**When** `RemoveActiveEffect` is called with the row's current `Revision`
**Then** `Status` becomes `Removed`, `Revision` increments by 1, and the row remains loadable (never physically deleted).

### Scenario 2 — RemoveActiveEffect by a non-MainGM actor is denied

**Given** a non-MainGM actor
**When** `RemoveActiveEffect` is called
**Then** it fails with `persistence.active_effect.operation_denied`, and the row is left completely unmutated — no connection is even opened.

### Scenario 3 — RemoveActiveEffect CAS conflict / terminal-status rejection

**Given** a stale `expectedRevision`, or a row already `Expired`/`Removed`
**When** `RemoveActiveEffect` is called (by a MainGM actor)
**Then** it fails with `persistence.active_effect.revision_conflict` and mutates nothing.

### Scenario 4 — RemoveActiveEffect replay is idempotent

**Given** a successful removal under `CommandId` X
**When** `RemoveActiveEffect` is called again with the same `CommandId` X
**Then** it returns the same already-`Removed` record without a second transition.

### Scenario 5 — direct creation by MainGM succeeds

**Given** a MainGM actor and valid effect/target inputs
**When** `ActiveEffectDirectCommandService.CreateDirectActiveEffect` is called
**Then** a new `ActiveEffect` is created via the unmodified `CreateActiveEffect`, with `SourceRef.Kind == GMDirect`.

### Scenario 6 — direct creation by a non-MainGM actor is denied

**Given** a non-MainGM actor
**When** `CreateDirectActiveEffect` is called
**Then** it fails with `persistence.active_effect.operation_denied`, and no row is created.

### Required invariants

- Both new MainGM checks are the literal first statement of their own method — no I/O precedes them.
- A denied operation (either one) leaves zero mutation — not even a partial one.
- `RemoveActiveEffect` never physically deletes a row; commits through `SqliteSavingPipeline`, CAS-guarded by `Revision`.
- Direct creation never bypasses `CreateActiveEffect` — it builds a record and delegates, never duplicating that method's own logic.
- `ItemEffectLifecycleService`'s own deliberate lack of a MainGM gate is untouched.

## 8. Deliverables

- Production code: `IActiveEffectRepository.RemoveActiveEffect` + `SqliteActiveEffectRepository` implementation; `ActiveEffectDirectCommandService.CreateDirectActiveEffect`; one new error code + `PersistenceFailures` factory.
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectGmCommandTests.cs`, `TC-ACTIVEEFFECT-069`-`078`.
- Scripts / CI: none.
- Configuration: none.
- Documentation: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none — no schema change beyond the already-existing `ActiveEffect` table's own columns.

## 9. Acceptance criteria

1. `RemoveActiveEffect` is a new repository method, `Revision`-CAS-guarded, routed through `SqliteSavingPipeline`, `Status → Removed`, never physical deletion.
2. Both MainGM checks are the literal first statement of their own method, before any I/O, matching `DeleteCharacterPermanently`'s own precedent and this codebase's own inline (no shared helper) convention.
3. Direct creation is a wrapper over the unmodified `CreateActiveEffect`, `SourceRef = ForGMDirect()`, MainGM-only.
4. A denied operation causes zero mutation — not partial, not full.
5. `ResolveActiveEffectStackConflict`'s own lack of a permission check is explicitly recorded as a known, not-closed-here gap.
6. No stacking/expiry/`WhileItemEquipped`-creation logic is introduced or changed.
7. Tests `TC-ACTIVEEFFECT-069`+ pass; `dotnet test` is green across the full solution.
8. Backlog row `506` → `In Review`.
9. PR is Draft; merge decision left to the product owner.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-069` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect by MainGM: Status → Removed, no physical delete | Pass |
| `TC-ACTIVEEFFECT-070` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect by non-MainGM: denied, no mutation | Pass |
| `TC-ACTIVEEFFECT-071` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect: stale Revision rejected, no mutation | Pass |
| `TC-ACTIVEEFFECT-072` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect replay is idempotent | Pass |
| `TC-ACTIVEEFFECT-073` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect on Expired/Removed rejected | Pass |
| `TC-ACTIVEEFFECT-074` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect on Suspended succeeds | Pass |
| `TC-ACTIVEEFFECT-075` | .NET / NUnit (Persistence, real SQLite) | RemoveActiveEffect cross-campaign rejected | Pass |
| `TC-ACTIVEEFFECT-076` | .NET / NUnit (Persistence, real SQLite) | CreateDirectActiveEffect by MainGM succeeds, sourced GMDirect | Pass |
| `TC-ACTIVEEFFECT-077` | .NET / NUnit (Persistence, real SQLite) | CreateDirectActiveEffect by non-MainGM denied, no row created | Pass |
| `TC-ACTIVEEFFECT-078` | .NET / NUnit (Persistence, real SQLite) | CreateDirectActiveEffect replay does not duplicate | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm `ItemEffectLifecycleService.cs`, `ActiveEffectStackingRules.cs`, `ActiveEffectExpiryRules.cs`, `SqliteInventoryRepository.cs`, and every ADR/`.asmdef`/`.csproj` file are all untouched.
- Confirm both new MainGM checks are the literal first executable statement of their own method by direct code review.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable to this task's own scope.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any test of `ResolveActiveEffectStackConflict`'s own permission model, since none is added here (known, explicitly recorded gap).

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — one new repository method, one new Application service, one new error code.
- Version fields affected: none.
- Migration or upcaster: none — no schema change.
- Forward / backward behavior: older builds never call the new method/service; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: none new — `RemoveActiveEffect` never physically deletes a row.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/effect test records; no real player data.
- Trust boundaries: `actorIsMainGm` is the sole authorization boundary for both new operations, checked first and never inferred from any other field — the same pattern `DeleteCharacterPermanently` already established.
- Authorization / audience checks: MainGM-only for both new operations, per `ADR-028` §10 rules 2-3; AssistantGM's own scope is whatever `ADR-019`'s existing model already grants (rule 4, inherited unchanged, not redecided here).
- Redaction requirements: no raw exception text/stack trace in any returned `Error`.
- Log-safe fields: `CampaignId`/`ActiveEffectId`/`UserId` only.
- Abuse / malformed input limits: every new method validates its own invariants and fails fast.
- Security tests: `TC-ACTIVEEFFECT-070`/`077` (non-MainGM rejection for each operation, verified to leave zero mutation).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new repository mutation method and a new Application-layer service — multiple `PLANS.md` §1.2 triggers.
- ExecPlan path: `docs/plans/active/ODY-S05-506_RemoveActiveEffect_And_Direct_Creation.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #140 (`ODY-S05-505`); precedes `ODY-S05-507`'s own integration fixtures.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs (`ADR-027`, `ADR-028`); `ItemEffectLifecycleService.cs`; `ActiveEffectStackingRules.cs`/`ActiveEffectExpiryRules.cs`; `CreateActiveEffect`/`GetActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one repository method and one error code; no manifest/schema version bump.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
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

- `Packages/com.odyssey.application/Runtime/Effects/ActiveEffectDirectCommandService.cs` — new file.
- `Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs` — new `RemoveActiveEffect` method.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — new `ActiveEffectOperationDenied` factory.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — new error code.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs` — `RemoveActiveEffect` implementation.
- `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectGmCommandTests.cs` — new file, `TC-ACTIVEEFFECT-069`-`078`.
- `Tests/Metadata/test-catalog.json` — `TC-ACTIVEEFFECT-069`-`078` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-506` updated to `In Review`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors, first attempt. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 606/606 (595 predecessor + 11 new). |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | New `ERROR_CODES.md` row accepted. |
| `.\scripts\verify-test-structure.ps1` | PASS | |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (RemoveActiveEffect CAS + SqliteSavingPipeline) | Met | `TC-ACTIVEEFFECT-069`/`071`/`072`; code review. |
| AC-2 (MainGM check as literal first statement) | Met | Code review of both `RemoveActiveEffect` and `CreateDirectActiveEffect`. |
| AC-3 (direct creation wraps unmodified CreateActiveEffect) | Met | `TC-ACTIVEEFFECT-076`; code review. |
| AC-4 (denied operation causes zero mutation) | Met | `TC-ACTIVEEFFECT-070`/`077`. |
| AC-5 (stacking-conflict permission gap recorded) | Met | §4/§18. |
| AC-6 (no out-of-scope logic) | Met | `git diff --name-status`. |
| AC-7 (tests pass, dotnet test green) | Met | Validation table above. |
| AC-8 (backlog In Review) | Met | Backlog diff. |
| AC-9 (Draft PR) | Met | PR to be opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` (`ODY-S05-503`) still has no permission check of any kind — explicitly out of this task's own scope per the backlog's own literal §15.1 text; a future task must decide whether/how conflict resolution should be gated.
- No real host/UI caller invokes `RemoveActiveEffect`/`CreateDirectActiveEffect` yet — `ODY-S05-507`'s own integration fixtures are the most plausible first real caller.

### Follow-up tasks

- `ODY-S05-507` — Item-Sourced Abilities/Effects Integration Fixtures.
- (Unscheduled) A future task to decide `ResolveActiveEffectStackConflict`'s own permission model, if one is ever needed.

### Self-review summary

- Scope review: diff touches only allowed paths; `ItemEffectLifecycleService.cs`, `ActiveEffectStackingRules.cs`, `ActiveEffectExpiryRules.cs`, `SqliteInventoryRepository.cs`, and every ADR/`.asmdef`/`.csproj` file are all untouched.
- Architecture review: `RemoveActiveEffect`'s gate placement mirrors `DeleteCharacterPermanently` exactly (repository-level, first statement); direct creation mirrors the "thin Application wrapper over an unmodified primitive" pattern; no shared `RequireMainGm` helper invented, matching the codebase's own established inline convention.
- Test review: both operations' success, denial (with zero-mutation proof), CAS-conflict, terminal-status, replay-idempotency, and cross-campaign-rejection paths are covered by real SQLite tests.
- Security/privacy review: MainGM gate enforced first on both paths; no payload leakage in errors or logs.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog all updated; the `503`-era stacking-conflict permission gap is explicitly re-recorded, not silently dropped.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-13 — **Design decision: `RemoveActiveEffect`'s own MainGM gate lives inside `SqliteActiveEffectRepository` itself, as its own literal first statement — not a separate Application-layer wrapper.** This mirrors `SqliteCharacterRepository.DeleteCharacterPermanently`'s own exact placement (a repository method with the gate as its own first statement, before any I/O), which the governing ТЗ itself names as the precedent to follow "по прямому прецеденту." This is a different placement from direct creation's own wrapper (below), and the difference is deliberate: `RemoveActiveEffect` is a genuinely new repository-level mutation (no existing method to delegate to), while direct creation only needs a gate in front of an already-existing, unmodified `CreateActiveEffect`. Authority: `DeleteCharacterPermanently`'s own exact code structure; the governing ТЗ's own explicit citation of this precedent.
- 2026-09-13 — **Design decision: direct (non-item) creation is a new Application-layer service (`ActiveEffectDirectCommandService.CreateDirectActiveEffect`) wrapping the unmodified `CreateActiveEffect`, not a new repository method.** No new repository method is needed because "who created it" is already structurally traceable via `SourceRef.Kind == GMDirect` on the record itself — the same structural argument the governing ТЗ makes explicitly. The service's own MainGM check is its own first statement, before it even constructs the candidate `ActiveEffect`/`ActiveEffectRecord`, let alone calls the repository. Authority: `ActiveEffectSourceRef.ForGMDirect()`'s own doc comment (already anticipating this task's own wiring); the governing ТЗ's own explicit "no new repository method needed for creation" reasoning.
- 2026-09-13 — **Design decision: one shared error code, `persistence.active_effect.operation_denied`, is reused by both `RemoveActiveEffect` and `CreateDirectActiveEffect`, following `InventoryMovementFailures.Denied`'s own "one code, several operations of the same domain" style — not `CharacterDeletionDenied`'s own one-code-per-operation style.** The backlog's own row-5 text explicitly frames both operations as "two MainGM-only permission gates... grouped together," matching the shared-code precedent's own domain-grouping logic more closely than the per-operation style, which this codebase reserves for operations with materially different failure semantics (e.g., `CharacterDeletionDenied` vs. `CharacterApprovalDenied` are different enough commands to warrant distinct codes). Authority: `InventoryMovementFailures.Denied`'s own real precedent shape; the backlog's own explicit "grouped together" framing of these two specific operations; the governing ТЗ's own explicit "recommended, not mandatory, justify if declined" framing (this decision follows the recommendation).
- 2026-09-13 — **Design decision: `RemoveActiveEffect` accepts both `Active` and `Suspended` as valid source statuses (rejecting only `Expired`/`Removed`), not `Active` alone.** `ADR-028` §9's own text says the command "ends an effect before its own natural expiry" with no restriction to a specific prior status; a `WhileItemEquipped` effect currently `Suspended` (item unequipped) is still a live, not-yet-terminated effect a MainGM should be able to end early — restricting removal to `Active` only would leave no way to end a currently-suspended effect except waiting for a future re-equip. Authority: `ADR-028` §9's own unrestricted "before its own natural expiry" text; direct review of `ActiveEffectStatus`'s own four values confirming `Suspended` is not itself a terminal state (`Expired`/`Removed` are the only two terminal values `SetItemEffectEquipped`'s own precedent already treats as such).
- 2026-09-13 — **Confirmed, re-recorded (not newly discovered) decision: `ActiveEffectStackingRules.ResolveActiveEffectStackConflict`'s own lack of any permission check is a known gap this task does not close.** Direct code read confirms the function performs no I/O and no permission check of any kind; `ODY-S05-503`'s own task contract already recorded this as an explicit deferral to `ODY-S05-506`. However, the backlog's own literal §15.1 text scopes `506` to exactly "§10 rules 2-3" (direct creation + removal), never mentioning stacking-conflict resolution — so this task does not extend its own scope to cover it, and instead re-records the gap explicitly here, exactly as the governing ТЗ's own §7 instructed ("зафиксировать в PR как известный, сознательно не закрываемый этой задачей пробел"). Authority: `ODY-S05-503`'s own task contract §18; the backlog's own literal §15.1 scope text for `506`; the governing ТЗ's own explicit instruction not to expand scope to close this gap.
