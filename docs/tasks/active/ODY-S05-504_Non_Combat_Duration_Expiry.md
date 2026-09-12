# ODY-S05-504 — Non-Combat Duration/Expiry

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-504-non-combat-duration-expiry`
**Pull request:** TBD (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-504_Non_Combat_Duration_Expiry.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Implement the 8 presently-implementable, non-`WhileItemEquipped` `EffectDurationType` expiry mechanisms `ADR-028` §8 specifies (`Instant`, `Permanent`, `UntilRemoved`, `UntilSceneChange`, `UntilSessionEnd`, `WhileCondition`, `WhileSourceExists`, `ForDuration`), plus `ADR-028` §11's own fail-closed rule as an explicit code path, plus the new `SqliteActiveEffectRepository.ExpireActiveEffect` `Status → Expired` transition this task owns by exclusion.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-502` gave `ActiveEffect` an `ExpiresAt` field that nothing yet interprets, and `IActiveEffectRepository` has no way to transition a row to `Expired` at all. Without this task, nothing in the codebase can ever end a non-`WhileItemEquipped` effect through its own natural duration.
- Value or risk reduction: closes 8 of `ADR-028` §8's 15 duration values with an explicit, individually-documented answer for each (real mechanism, explicit no-op, or honest contract-only stub) — per `ADR-028` §8's own decision that "none is left undecided by omission." Implements the fail-closed rule (`ADR-028` §11) as a real code path, preventing a future caller from ever defensively expiring an effect on an inconclusive check.
- Blocking or enabling relationship: the new `ExpireActiveEffect` repository method is the only remaining `Status` transition this range's own task split leaves unassigned (`ODY-S05-505` owns only `Suspended`/`Active`; `ODY-S05-506` owns only `Removed`) — a later task (most plausibly `ODY-S05-507`'s integration fixtures, or a real caller `ODY-S05-505` introduces) will be the first to actually invoke it end-to-end.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 3 (corrected: this task also fixes a "9" → "8" counting typo inherited from `ODY-S05-111`, present in both this row and §15.1's own prose — see §4/§18).
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §6 rule 2 (minimum contract includes "transition `Status` (expire/suspend/resume/remove)"), §8 (the full 15-value duration table, verbatim), §11 (fail-closed rule, verbatim), §12 (mandatory `SqliteSavingPipeline` reuse), §15 (module boundaries — `Odyssey.Rules` owns `WhileCondition` evaluation).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (read only, not modified).
- `docs/adr/ADR-001` (module dependency direction).
- Existing patterns: `ActiveEffectStackingRules`/`EffectStackRules` (`ODY-S05-503`, the direct structural precedent for a new pure-decision file in the same directory/namespace, and for a new `Odyssey.Rules.Effects` evaluator with no established payload schema, documented as an honest fixture-level heuristic); `ItemDefinitionMigrationRules.ComputeBlockingIssues` (the "decide only, explicitly document unimplemented cases rather than pretend they're covered" precedent — its own doc comment: "Later tasks must not assume an empty report proves all six cases safe"); `SqliteInventoryRepository.ApplyItemDefinitionMigration`/`SqliteActiveEffectRepository.CreateActiveEffect` (`Revision`-CAS-via-`SqliteSavingPipeline` structural precedent for the new `ExpireActiveEffect` method); `CharacterOwnership.IsActiveAt`/`DiagnosticBundleContracts.IsExpired` (the "`now` passed as a parameter, never read from a clock inside the pure function" idiom).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-504`, `SLICE-05`.
- Existing test IDs: `TC-ACTIVEEFFECT-001`-`035` (`ODY-S05-502`/`503`) as predecessor evidence; this task continues the same prefix.
- New test IDs introduced: `TC-ACTIVEEFFECT-036`-`050`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-504` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main`/this branch's merge-base is `e378ca7`, the merge of PR #138 (`ODY-S05-503`). Backlog row 3 in §15 (`ODY-S05-504`) was `Proposed`.
- **Backlog typo confirmed and fixed by direct arithmetic check:** `ADR-028` §8 lists 15 `EffectDurationType` values; `WhileItemEquipped` is already solved (`ADR-027` §8.2), and 6 are turn/round-based (out of this whole range's scope, `ADR-028` §13). `15 − 1 − 6 = 8`, not 9. Both the backlog row 3 text ("The 9 presently-implementable...") and §15.1's own prose ("`ODY-S05-504` owns the 9 non-combat...") stated "9," inherited unchanged from `ODY-S05-111`'s own text — confirmed by direct read that both actually enumerate exactly 8 values (`Instant`, `Permanent`, `UntilRemoved`, `UntilSceneChange`, `UntilSessionEnd`, `WhileCondition`, `WhileSourceExists`, `ForDuration`). Both point-corrected to "8" (see §18); a third occurrence in §14.1 (line 397, written by `ODY-S05-110`, a different section this ТЗ's own scope does not name) was deliberately left untouched, per this session's own established discipline of only editing what the governing ТЗ explicitly names.
- `IActiveEffectRepository`/`ActiveEffect.cs` (direct code read, post-503) still have exactly the 4 `ODY-S05-502` methods and no `Status`-mutating method at all; `ActiveEffect.StackCount`'s own doc comment already anticipated `ODY-S05-503`'s own increment by name, and `ExpiresAt`'s own doc comment already states "`ODY-S05-504`/`505` own [computing/interpreting this]" — confirming this task's own ownership of interpreting (not computing) `ExpiresAt`.
- `ADR-028` §6 rule 2 (direct read) lists "transition `Status` (expire/suspend/resume/remove)" as part of the minimum repository contract. Direct review of the three sibling tasks' own scope statements (`ODY-S05-505`: "Suspended`/`Active` transitions per `ADR-028` §5.2 rule 3"; `ODY-S05-506`: "`RemoveActiveEffect` command... `Status → Removed`") confirms neither owns `Expired` — by exclusion, this task is the only remaining task in the range that can add it.
- **Genuine forward reference confirmed by direct search (not assumed), mirroring `ODY-S05-503`'s own finding for `EffectStackRules`:** no `SceneActivated`/`SceneChanged` event, no session-end signal, and no `ItemConsumed`/`ItemDestroyed` event exists anywhere in this codebase today (`grep` across `Packages/`/`DotNet/`) — confirming `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` can only be given a *contract shape*, never a real end-to-end subscription, in this task.
- `Odyssey.Rules.Effects` (direct code read, post-503) contains only `EffectStackRules.IsStronger`. No condition-evaluation code of any kind existed before this task — a genuine forward reference this task both writes and calls, exactly as `EffectStackRules` itself was for `ODY-S05-503`.
- `EffectDefinition` (`TypedDefinitions.cs`, direct code read) has no dedicated "condition" field — `WhileCondition`'s own condition semantics, like `ReplaceIfStronger`'s own "stronger" semantics, would have to live inside the same opaque `MechanicsPayloadRef`-resolved `EffectMechanicsSnapshot.Payload` with no established schema (confirmed by the same search `ODY-S05-503`'s own task contract already recorded: every existing `MechanicsPayloadRef` fixture is an opaque reference string). Combined with `Odyssey.Rules`'s own continued lack of a JSON library (re-confirmed by the same `.csproj`/`.asmdef` read `ODY-S05-503` already performed), no real condition language can be parsed today without inventing one out of thin air — which `ADR-028` §14's own non-goal ("balancing concrete `EffectDefinition` content or ... potency payload schema") forbids by the same reasoning `ODY-S05-503`'s task contract already applied to `ReplaceIfStronger`. See §18 for the resulting decision: an honestly-documented, always-`Inconclusive` evaluator.
- `CharacterOwnership.IsActiveAt` (`CharacterOwnership.cs:70`, direct code read): `public bool IsActiveAt(UtcInstant now) => !ExpiresAt.HasValue || now.CompareTo(ExpiresAt.Value) < 0;` and `DiagnosticBundleContracts.IsExpired(UtcInstant nowUtc)` (direct code read) both confirm the established idiom: a pure function taking "now" as a parameter, never reading a clock itself — `ActiveEffectExpiryRules.CheckForDurationExpiry` follows this exactly.
- `PersistenceFailures.EquipmentEntryRevisionConflict` (`CampaignRepositoryContracts.cs:887`, direct code read) is the exact precedent this task's own new `ActiveEffectRevisionConflict` error follows for a CAS-guarded status-transition conflict.

### Assumptions

- None.

## 5. Scope

### In scope

- New `Odyssey.Application.Effects.ActiveEffectExpiryRules` static class (same directory/namespace as `ODY-S05-503`'s own `ActiveEffectStackingRules`): `CheckNoAutomaticExpiry`, `CheckForDurationExpiry`, `CheckWhileConditionExpiry`, `OnExternalTriggerFired`, plus `ActiveEffectExpiryDecision`/`ActiveEffectExternalExpiryTriggerKind`.
- New `Odyssey.Rules.Effects.EffectConditionRules.Evaluate` + `EffectConditionEvaluationResult` — the `WhileCondition` evaluator `ADR-028` §15 assigns to `Odyssey.Rules`.
- New `IActiveEffectRepository.ExpireActiveEffect` + `SqliteActiveEffectRepository` implementation — `Revision`-CAS-guarded, routed through `SqliteSavingPipeline`.
- One new error code: `persistence.active_effect.revision_conflict`.
- Point correction of the "9" → "8" counting typo in the backlog row 3 and §15.1's own prose (not §14.1, outside this ТЗ's own named scope).
- Tests (`TC-ACTIVEEFFECT-036`-`050`), test metadata, task/plan docs, backlog row.

### Out of scope

- `WhileItemEquipped`'s own `Suspended`/`Active` transition (`ODY-S05-505`'s own territory).
- `RemoveActiveEffect` and any direct-creation permission gate (`ODY-S05-506`'s own territory).
- Any turn/round-based `EffectDurationType` mechanism or combat-triggered effect application (out of scope for this entire block, `ADR-028` §13).
- Stacking-policy resolution (`ODY-S05-503`, already implemented) — not reopened.
- Any real event publisher for `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` — contract shape only, per the governing ТЗ's own explicit instruction not to invent one.
- `docs/adr/ADR-028_...md` itself — already `Accepted`; implemented here, not amended.
- `Create`/`Get`/`List` methods on `IActiveEffectRepository`/`SqliteActiveEffectRepository` — not rewritten, only a new method added alongside them.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectExpiryRules.cs
Packages/com.odyssey.rules/Runtime/Effects/EffectConditionRules.cs
Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectExpiryRulesTests.cs
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-504_Non_Combat_Duration_Expiry.md
docs/plans/active/ODY-S05-504_Non_Combat_Duration_Expiry.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectStackingRules.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteEquipmentRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/EquipmentServiceTests.cs
Any `.csproj`/`.asmdef` file
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Rules` owns the `WhileCondition` evaluator (`ADR-028` §15); `Odyssey.Application` owns expiry-decision orchestration and the repository contract; `Odyssey.Persistence` owns the physical `Status → Expired` transition.
- Authoritative-state and transaction boundary: `ExpireActiveEffect`'s CAS update + `DomainEvents`/`AppliedCommands` rows commit together in one transaction via `SqliteSavingPipeline`, all-or-nothing, exactly like `CreateActiveEffect`.
- Serialization / compatibility boundary: none — no new wire/storage format.
- Time / RNG rule: `CheckForDurationExpiry` takes `now: UtcInstant` as a parameter (never reads a clock itself); `SqliteActiveEffectRepository.ExpireActiveEffect` uses the injected `IWallClock` for `UpdatedAt` only, matching `CreateActiveEffect`'s own precedent.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency — `EffectConditionRules.Evaluate` is deliberately designed not to need one (same reasoning `ODY-S05-503` already established for `EffectStackRules.IsStronger`).
- Security / privacy / redaction rule: no raw exception text or stack trace in any returned `Error`.
- Other: `ActiveEffectExpiryRules`'s own methods never call `IActiveEffectRepository` themselves — a caller supplies whatever already-loaded state each check needs, mirroring `ActiveEffectStackingRules`'s own "decide only" contract.

## 7. Expected behavior

### Scenario 1 — Permanent/UntilRemoved never expire automatically

**Given** `EffectDurationType.Permanent` or `.UntilRemoved`
**When** `CheckNoAutomaticExpiry` is called
**Then** it always returns `NotExpired`; calling it with any other duration type throws.

### Scenario 2 — ForDuration expires by wall clock

**Given** a computed `ExpiresAt`
**When** `CheckForDurationExpiry` is called with `now` at, before, or after `ExpiresAt`
**Then** it returns `NotExpired` while `now < ExpiresAt`, `Expired` once `now >= ExpiresAt`; a `null` `ExpiresAt` throws.

### Scenario 3 — WhileCondition fails closed on an inconclusive evaluation

**Given** `EffectConditionRules.Evaluate` returns `Inconclusive` (today, always — no real condition language exists yet)
**When** `CheckWhileConditionExpiry` is called with that result
**Then** it returns `NotExpired` — never a defensive `Expired`. `ConditionHolds`/`ConditionFailed` return `NotExpired`/`Expired` respectively, proving the fail-closed branch is a real, distinct code path, not merely the absence of an `Expired` branch.

### Scenario 4 — external-trigger contract for UntilSceneChange/UntilSessionEnd/WhileSourceExists

**Given** any of the three `ActiveEffectExternalExpiryTriggerKind` values
**When** `OnExternalTriggerFired` is called
**Then** it always returns `Expired` — there is no further condition once the (currently unpublished) trigger fires.

### Scenario 5 — ExpireActiveEffect transitions a real row

**Given** a persisted `Active` `ActiveEffect` row
**When** `ExpireActiveEffect` is called with its current `Revision`
**Then** the row's `Status` becomes `Expired`, `Revision` increments by 1, and the row remains loadable (never physically deleted).

### Scenario 6 — ExpireActiveEffect rejects a stale Revision

**Given** an `expectedRevision` that no longer matches the row's own current `Revision`
**When** `ExpireActiveEffect` is called
**Then** it fails with `persistence.active_effect.revision_conflict` and the row is left completely unmutated.

### Scenario 7 — ExpireActiveEffect replay is idempotent

**Given** a successful `ExpireActiveEffect` under `CommandId` X
**When** `ExpireActiveEffect` is called again with the same `CommandId` X
**Then** it returns the same already-expired record without a second transition.

### Required invariants

- Every one of the 8 `EffectDurationType` values this task owns has an explicit, individually-documented answer — no value is silently skipped.
- The fail-closed rule (`ADR-028` §11) is a real, distinct, testable code branch, never merely the absence of an `Expired` return.
- `ExpireActiveEffect` never physically deletes a row; it commits through `SqliteSavingPipeline`, CAS-guarded by `Revision`.
- No method in this task calls `IActiveEffectRepository` itself — every decision method is pure and caller-supplied-state-only.

## 8. Deliverables

- Production code: `ActiveEffectExpiryRules`/`ActiveEffectExpiryDecision`/`ActiveEffectExternalExpiryTriggerKind` (Application); `EffectConditionRules`/`EffectConditionEvaluationResult` (Rules); `IActiveEffectRepository.ExpireActiveEffect` + `SqliteActiveEffectRepository` implementation; one new error code.
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectExpiryRulesTests.cs`, `TC-ACTIVEEFFECT-036`-`050`.
- Scripts / CI: none.
- Configuration: none.
- Documentation: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, backlog row + typo fix.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none — no schema change beyond the already-additive `ActiveEffect` table's own existing columns.

## 9. Acceptance criteria

1. All 8 `EffectDurationType` values this task owns are explicitly handled — real logic (`ForDuration`, `WhileCondition`), explicit no-op (`Permanent`/`UntilRemoved`), or explicit contract-only stub (`UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists`); `Instant` is explicitly documented as never reaching this class.
2. The fail-closed rule is implemented as an explicit code path (an inconclusive check → `NotExpired`, never a defensive `Expired`).
3. A new `Status → Expired` transition method exists on `IActiveEffectRepository`/`SqliteActiveEffectRepository`, `Revision`-CAS-guarded, routed through `SqliteSavingPipeline`.
4. `WhileCondition`'s evaluator is new code in `Odyssey.Rules`, never hardcoded in the Application layer.
5. The three event-subscription mechanisms are contract/shape only, honestly documented as an integration point for a future task — no fake publisher.
6. No `WhileItemEquipped`, turn/round-based, stacking, or removal logic is introduced.
7. Tests `TC-ACTIVEEFFECT-036`+ pass; `dotnet test` is green across the full solution.
8. Backlog row `504` → `In Review`; the "9"→"8" typo is fixed in the row and in §15.1.
9. PR is Draft; merge decision left to the product owner.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-036` | .NET / NUnit (Persistence) | CheckNoAutomaticExpiry: Permanent → NotExpired | Pass |
| `TC-ACTIVEEFFECT-037` | .NET / NUnit (Persistence) | CheckNoAutomaticExpiry: UntilRemoved → NotExpired | Pass |
| `TC-ACTIVEEFFECT-038` | .NET / NUnit (Persistence) | CheckNoAutomaticExpiry rejects any other duration type | Pass |
| `TC-ACTIVEEFFECT-039` | .NET / NUnit (Persistence) | CheckForDurationExpiry: NotExpired before ExpiresAt | Pass |
| `TC-ACTIVEEFFECT-040` | .NET / NUnit (Persistence) | CheckForDurationExpiry: Expired at/after ExpiresAt | Pass |
| `TC-ACTIVEEFFECT-041` | .NET / NUnit (Persistence) | CheckForDurationExpiry rejects a null ExpiresAt | Pass |
| `TC-ACTIVEEFFECT-042` | .NET / NUnit (Persistence) | EffectConditionRules.Evaluate is always Inconclusive today | Pass |
| `TC-ACTIVEEFFECT-043` | .NET / NUnit (Persistence) | Fail-closed: Inconclusive → NotExpired | Pass |
| `TC-ACTIVEEFFECT-044` | .NET / NUnit (Persistence) | ConditionHolds → NotExpired | Pass |
| `TC-ACTIVEEFFECT-045` | .NET / NUnit (Persistence) | ConditionFailed → Expired | Pass |
| `TC-ACTIVEEFFECT-046` | .NET / NUnit (Persistence) | OnExternalTriggerFired → Expired for all 3 trigger kinds | Pass |
| `TC-ACTIVEEFFECT-047` | .NET / NUnit (Persistence, real SQLite) | ExpireActiveEffect transitions Status, increments Revision | Pass |
| `TC-ACTIVEEFFECT-048` | .NET / NUnit (Persistence, real SQLite) | ExpireActiveEffect rejects a stale Revision, no mutation | Pass |
| `TC-ACTIVEEFFECT-049` | .NET / NUnit (Persistence, real SQLite) | ExpireActiveEffect replay is idempotent | Pass |
| `TC-ACTIVEEFFECT-050` | .NET / NUnit (Persistence, real SQLite) | ExpireActiveEffect rejects a mismatched CampaignId | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no ADR edits, no `WhileItemEquipped`/removal/stacking logic, no `.csproj`/`.asmdef` change, and that the backlog typo fix touches only row 3 and §15.1.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database, used for `ExpireActiveEffect`'s own real round-trip tests.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any test of a real `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` publisher, since none exists or is wired in this task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — one new repository method, one new error code, two new pure-decision static classes.
- Version fields affected: none.
- Migration or upcaster: none — no schema change; `ExpireActiveEffect` operates on the already-existing `ActiveEffect` table.
- Forward / backward behavior: older builds never call the new method; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: none new — `ExpireActiveEffect` never physically deletes a row, matching `ADR-012`'s append-only-history discipline.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/effect test records; no real player data.
- Trust boundaries: `ExpireActiveEffect` validates the campaign boundary exactly like `CreateActiveEffect`; no new permission model is introduced (expiry is a system-driven transition, not a player/GM-authorized command).
- Authorization / audience checks: none introduced by this task — permission gates for direct-creation/removal remain `ODY-S05-506`'s own territory.
- Redaction requirements: no raw exception text or stack trace in any returned `Error`.
- Log-safe fields: `ActiveEffectId`/`CampaignId` only in the audit event payload.
- Abuse / malformed input limits: every new method validates its own invariants and fails fast.
- Security tests: `TC-ACTIVEEFFECT-050` (cross-campaign rejection), `TC-ACTIVEEFFECT-048` (CAS conflict rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new repository mutation method and a new cross-module (`Odyssey.Rules` + `Odyssey.Application`) decision mechanism — multiple `PLANS.md` §1.2 triggers.
- ExecPlan path: `docs/plans/active/ODY-S05-504_Non_Combat_Duration_Expiry.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #138 (`ODY-S05-503`); no hard dependency on/from `ODY-S05-505`/`506` (both depend only on `502`).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs (`ADR-027`, `ADR-028`); `ActiveEffect` aggregate; `Create`/`Get`/`List` methods on `IActiveEffectRepository`/`SqliteActiveEffectRepository`.
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

- `Packages/com.odyssey.application/Runtime/Effects/ActiveEffectExpiryRules.cs` — new file.
- `Packages/com.odyssey.rules/Runtime/Effects/EffectConditionRules.cs` — new file.
- `Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs` — new `ExpireActiveEffect` method.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — new `ActiveEffectRevisionConflict` factory.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — new error code.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs` — `ExpireActiveEffect` implementation.
- `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectExpiryRulesTests.cs` — new file, `TC-ACTIVEEFFECT-036`-`050`.
- `docs/errors/ERROR_CODES.md` — new row.
- `Tests/Metadata/test-catalog.json` — `TC-ACTIVEEFFECT-036`-`050` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-504` updated to `In Review`; "9"→"8" typo fixed in the row and §15.1.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors, first attempt. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 577/577 (560 predecessor + 17 new), all green, no regressions. |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | New `ERROR_CODES.md` row accepted. |
| `.\scripts\verify-test-structure.ps1` | PASS | |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (all 8 values explicitly handled) | Met | `TC-ACTIVEEFFECT-036`-`046`; code review of `ActiveEffectExpiryRules`'s own doc comment enumerating all 8. |
| AC-2 (fail-closed as explicit code path) | Met | `TC-ACTIVEEFFECT-043`/`044`/`045` (three distinct branches proven separately). |
| AC-3 (new Status→Expired method, CAS, SqliteSavingPipeline) | Met | `TC-ACTIVEEFFECT-047`-`049`; code review of `ExpireActiveEffect`. |
| AC-4 (WhileCondition evaluator in Odyssey.Rules) | Met | `TC-ACTIVEEFFECT-042`; code review of `EffectConditionRules`. |
| AC-5 (event-subscription contract only, honestly documented) | Met | `TC-ACTIVEEFFECT-046`; doc comment on `ActiveEffectExternalExpiryTriggerKind`. |
| AC-6 (no out-of-scope logic) | Met | `git diff --name-status`: `ActiveEffectStackingRules.cs`, guard test files, `.csproj`/`.asmdef` all unchanged. |
| AC-7 (tests pass, dotnet test green) | Met | Validation table above. |
| AC-8 (backlog In Review, typo fixed) | Met | Backlog diff. |
| AC-9 (Draft PR) | Met | PR to be opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- `EffectConditionRules.Evaluate` always returns `Inconclusive` today — an honest, documented no-op pending a real Ruleset condition language, matching the same limitation pattern `ODY-S05-503`'s own `EffectStackRules.IsStronger` already established for `ReplaceIfStronger`.
- `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` have no real publisher wired — `OnExternalTriggerFired` is a contract-only handler shape a future task must call from a real event.
- No real caller of `ActiveEffectExpiryRules`/`ExpireActiveEffect` exists yet — a future task (most plausibly `ODY-S05-507`'s integration fixtures, or a real caller `ODY-S05-505` introduces) is the first to wire one end-to-end.

### Follow-up tasks

- `ODY-S05-505` — WhileItemEquipped Wiring + Item-Triggered Creation.
- `ODY-S05-506` — RemoveActiveEffect Command + Direct-Creation Permission Gates.
- `ODY-S05-507` — Item-Sourced Abilities/Effects Integration Fixtures (the ТЗ's own suggested example composes a real `ForDuration` expiry — this task's own `CheckForDurationExpiry`/`ExpireActiveEffect` are the pieces that fixture will compose).

### Self-review summary

- Scope review: diff touches only allowed paths; no ADR, `ActiveEffect` aggregate, `Create`/`Get`/`List` method, or project-file change; the backlog typo fix touches only the two locations the governing ТЗ named.
- Architecture review: mirrors `ODY-S05-503`'s own precedent exactly — a new pure decision file in `Odyssey.Application.Effects`, a new honestly-documented evaluator in `Odyssey.Rules.Effects`, and (unlike `503`) one new repository method added by explicit exclusion-based reasoning, not guessed at.
- Test review: all 8 owned duration values, the fail-closed rule (as three distinct branches), and the new repository method (success, CAS conflict, replay, cross-campaign rejection) are covered by real tests.
- Security/privacy review: campaign-boundary check enforced on `ExpireActiveEffect`; no payload leakage in errors or logs.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog (including the typo fix) all updated; no schema/manifest/protocol version bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Backlog correction: "9" → "8" in row 3 and §15.1's own prose of `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.** Direct arithmetic check against `ADR-028` §8 (15 total values, minus 1 already-solved `WhileItemEquipped`, minus 6 Block-3-reserved turn/round values = 8) and direct enumeration of both passages' own listed values (exactly 8 each) confirmed the "9" was a genuine counting typo inherited unchanged from `ODY-S05-111`'s own text, not a deliberate count including some value this task also owns. A third occurrence in §14.1 (line 397, part of `ODY-S05-110`'s own decomposition text, a different section) was deliberately left untouched — the governing ТЗ names only "строку `504` и... §15.1," and this session's own established discipline is to touch only what a ТЗ explicitly names. Authority: `ADR-028` §8's own 15-row table; direct governing-ТЗ text naming exactly these two locations.
- 2026-09-12 — **Design decision: `IActiveEffectRepository`/`SqliteActiveEffectRepository` gain exactly one new method, `ExpireActiveEffect`, by exclusion-based reasoning, not by the backlog row's own literal text (which does not explicitly grant this task a repository-contract change).** `ADR-028` §6 rule 2 names "transition `Status` (expire/suspend/resume/remove)" as part of the minimum contract; direct review of `ODY-S05-505`'s own scope ("`Suspended`/`Active` transitions") and `ODY-S05-506`'s own scope ("`Status → Removed`") confirms neither covers `Expired` — this task is the only remaining task in the range that both needs and can add it, since every other task's own boundary text explicitly excludes it. Implemented via `SqliteSavingPipeline`, `Revision`-CAS, mirroring `CreateActiveEffect`'s own structure exactly (same `EnsureActiveEffectTables`/`ReplayByCommandId`/`SelectColumns` reuse, no duplication). Authority: `ADR-028` §6 rule 2's own minimum-contract text; direct review of `ODY-S05-505`/`506`'s own scope statements in the backlog confirming neither covers `Expired`.
- 2026-09-12 — **Design decision: `EffectConditionRules.Evaluate` always returns `Inconclusive` today — a deliberate, fully-documented no-op, not a partial implementation.** Two independent constraints, both already established by `ODY-S05-503`'s own analogous `ReplaceIfStronger` reasoning, apply identically here: (1) no Ruleset-defined condition language or schema exists anywhere in this codebase for `EffectMechanicsSnapshot.Payload`'s own opaque content (confirmed by the same search `503`'s own task contract already recorded); (2) `Odyssey.Rules` still has no JSON library dependency (re-confirmed by the same `.csproj`/`.asmdef` read), and `ADR-028` §14's own non-goal forbids inventing Ruleset-specific content semantics under this ADR's authority. `ADR-028` §11's own text explicitly permits this: an inconclusive evaluation is a fully valid, fail-closed-compliant outcome ("re-attempted on the next relevant trigger, never... 'expired by default'"), and `ItemDefinitionMigrationRules.ComputeBlockingIssues`'s own precedent already established the "declare unimplemented cases honestly, do not assume empty/no-op proves safety" pattern this doc comment follows verbatim. Authority: `ADR-028` §8's own `WhileCondition` row and §11's fail-closed text; `ADR-028` §14's own non-goal; `ODY-S05-503`'s own task contract §18 (the identical reasoning already applied to `ReplaceIfStronger`); `ItemDefinitionMigrationRules.ComputeBlockingIssues`'s own doc comment precedent.
- 2026-09-12 — **Design decision: `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` are represented by one shared `ActiveEffectExternalExpiryTriggerKind` enum + one `OnExternalTriggerFired` method, rather than three separate interfaces/handler signatures.** All three share the identical property that firing the (currently unpublished) external trigger event always ends the effect directly, with no further condition to evaluate (unlike `WhileCondition`) — three separate interfaces would each carry an identical trivial body, adding indirection without adding real distinct behavior. A future task wiring a real publisher for any of the three calls the same method with the matching `triggerKind`. Authority: `ADR-028` §8's own three table rows, each independently confirming "subscribes to \[event\]... ends the effect" with no further evaluation step named for any of the three; direct search confirming no publisher exists for any of them today.
- 2026-09-12 — Decision: no scope-guard file required any edit, and no architecture test needed updating for the new repository method. Confirmed by direct code read that none of the four `ODY-S05-502`-narrowed guards or `Odyssey.Tests.Architecture`'s own suite reference `SqliteActiveEffectRepository`/`IActiveEffectRepository` by method name or count, and by a real full-suite `dotnet test` run showing zero regressions after adding `ExpireActiveEffect`. Authority: direct code read (`grep` across all four guard files and the Architecture test project); real `dotnet test` confirmation.
