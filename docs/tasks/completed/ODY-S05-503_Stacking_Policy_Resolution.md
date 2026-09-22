# ODY-S05-503 — Stacking Policy Resolution

**Status:** Done (PR #138, merged into main)
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-503-stacking-policy-resolution`
**Pull request:** [odyssey-services/Odyssey_VTT#138](https://github.com/odyssey-services/Odyssey_VTT/pull/138) (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-503_Stacking_Policy_Resolution.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Implement all 7 `EffectStackPolicy` behaviors (`ADR-028` §7) as a pure decision layer — `IndependentInstances`, `RefreshDuration`, `ReplaceIfStronger` (delegating the "stronger" comparison to `Odyssey.Rules`), `ReplaceExisting`, `IncreaseStacks`, `IgnoreNewApplication`, and `RequestGMResolution` (a new `ActiveEffectStackConflict` pending record + `ResolveActiveEffectStackConflict` resolution function). Consumes `ODY-S05-502`'s own `ActiveEffect` aggregate and `IActiveEffectRepository` (for realistic test fixtures only); does not reimplement or extend either.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-502` established the `ActiveEffect` aggregate and its standalone persistence foundation, but nothing decides what happens when the same effect is applied to the same target a second time. Without this task, every re-application would need ad-hoc, uncoordinated handling wherever item-use logic eventually lives.
- Value or risk reduction: centralizes all 7 `ADR-028` §7 stacking behaviors in one pure, testable decision layer, mirroring the migration block's own successful `ComputeBlockingIssues` (`ODY-S05-402`) precedent — decide first, apply later, in a separate task, against real persistence.
- Blocking or enabling relationship: `ODY-S05-505` (WhileItemEquipped Wiring + Item-Triggered Creation) is the most likely future consumer of this task's own `ActiveEffectStackingRules`/`ActiveEffectStackDecision`, since that is where a real item-triggered `ActiveEffect` application will first need a stacking decision. This task does not itself wire any real caller.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 2.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §7 (all 7 `EffectStackPolicy` behaviors, verbatim), §15 (module boundaries: `Odyssey.Rules` owns the `ReplaceIfStronger` comparison), §18.3 (rejected alternative: a generic numeric potency field).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (read only, not modified).
- `docs/adr/ADR-001` (module dependency direction).
- Existing patterns: `ItemDefinitionMigrationRules.ComputeBlockingIssues` (`ODY-S05-402`, pure decision-only static method, structural precedent this task mirrors exactly); `Odyssey.Rules.Character.AttributeCostRules` (the "TEST FIXTURE ONLY — not production Ruleset balance data" honesty precedent this task's own `EffectStackRules.IsStronger` follows); `TypedDefinitionCodec.DecodeEffect` (existing `EffectStackPolicy` decode path, reused by callers of this task's own function, not reimplemented here).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-503`, `SLICE-05`.
- Existing test IDs: `TC-ACTIVEEFFECT-001`-`019` (`ODY-S05-502`) as predecessor evidence; this task continues the same prefix (a stacking-decision layer over the same standalone aggregate, not a new aggregate family).
- New test IDs introduced: `TC-ACTIVEEFFECT-020`-`035`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-503` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main`/this branch's merge-base is `7e8507f`, the merge of PR #137 (`ODY-S05-502`). Backlog row 2 in §15 (`ODY-S05-503`) was `Proposed`.
- `IActiveEffectRepository` (`ActiveEffectRepositoryContracts.cs`, direct code read) exposes exactly `CreateActiveEffect`/`GetActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource` — no update/mutation method exists. `ActiveEffect`'s own constructor (`ActiveEffect.cs`, direct code read) accepts whatever `Status`/`StackCount`/`Revision` the caller supplies with no computed transitions; its own doc comment on `StackCount` already states "Only `ODY-S05-503`'s own `IncreaseStacks` policy ever increments it."
- **Genuine forward reference confirmed by direct search (not assumed):** `Odyssey.Rules` (`Packages/com.odyssey.rules`) contains only `Odyssey.Rules.Character.*` (attribute/skill/ability cost rules, anatomy/resource initialization, ruleset migration) and `Odyssey.Rules.Versions.*` — no effect-comparison code of any kind existed before this task. This task both writes and calls the new comparison, not merely calls an existing one.
- **Hard technical constraint discovered by direct project-file read:** `DotNet/Projects/Odyssey.Rules.csproj` and `Packages/com.odyssey.rules/Runtime/Odyssey.Rules.asmdef` both show `Odyssey.Rules` depends on `Odyssey.Domain` only — no JSON library (Newtonsoft.Json or otherwise) is referenced anywhere in the Rules module. `ADR-028` §7 rule 3's own text says the comparison happens "over the opaque `MechanicsPayloadRef`-resolved payload," which would ordinarily suggest parsing `EffectMechanicsSnapshot.Payload` as JSON for some game-design-specific field — but no such field/schema exists anywhere in this codebase (confirmed by search: every `EffectDefinition.MechanicsPayloadRef` in every existing test fixture is an opaque reference string like `"burn_snapshot_ref"`, never inline mechanics data), and adding a JSON dependency to `Odyssey.Rules` would expand that module's own dependency graph — a project/assembly-definition-file change this task's own allowed paths do not include. See §18 for the resulting decision.
- `ADR-028` §18.3 (direct read) explicitly **rejected** adding a strongly-typed generic `Potency` field to `EffectMechanicsSnapshot`/`EffectDefinition`, "because no such generic cross-Ruleset potency scale exists anywhere in this codebase's already-accepted content model, and inventing one here would silently decide Ruleset-specific game-design semantics this ADR has no authority over." This task's own comparison must not reintroduce that rejected field under a different name.
- `TypedDefinitionCodec.DecodeEffect` (`TypedDefinitionCodec.cs:276`, direct code read) already decodes an `EffectDefinition` (including its `StackPolicy`) from a `ContentDefinitionRecord`'s `PropertiesJson` — this task's own `ResolveStacking` accepts an already-decoded `EffectStackPolicy` from its caller, exactly matching `ComputeBlockingIssues`'s own "caller loads/decodes, this method only decides" contract; this task adds no second decode path.
- **Architectural precedent confirmed by direct code read of `ItemDefinitionMigrationRules.cs`:** `ComputeBlockingIssues` (`ODY-S05-402`) is a pure, static, no-I/O method that only *decides*; the actual database mutation was a *separate* later task's own new repository method (`ODY-S05-403`'s `ApplyItemDefinitionMigration`). This is the direct precedent for this task's own scope boundary — see §18 for the resulting decision not to add any new `IActiveEffectRepository`/`SqliteActiveEffectRepository` method in this task.
- `Odyssey.Tests.Persistence` (direct `.csproj` read) references `Odyssey.Application.csproj`, which itself references `Odyssey.Rules.csproj` (transitively, via .NET SDK-style `ProjectReference` transitivity) — confirmed this task's new `Odyssey.Rules.Effects.EffectStackRules` type is reachable from `Odyssey.Tests.Persistence` test code with no `.csproj` edit needed. `ItemDefinitionMigrationRulesTests.cs` (a pure-function test file with no I/O of its own) already lives in `Odyssey.Tests.Persistence`, not a dedicated Rules test project — this task's own new test file follows that same placement precedent.
- The four scope guards narrowed by `ODY-S05-502` (`SqliteInventoryRepositoryTests.cs`, `SqliteEquipmentRepositoryTests.cs`, `InventoryCreationServiceTests.cs`, `EquipmentServiceTests.cs`) scan only the `Odyssey.Persistence` assembly's own types or the `Odyssey.Application.Inventory` namespace — confirmed by direct code read and by running the two tests each guard belongs to after adding this task's own new files (both pass unmodified): this task's new types live in `Odyssey.Application.Effects`/`Odyssey.Rules.Effects`, outside every guard's own scan scope, and this task touches no `Odyssey.Persistence` file at all — no guard file needed any edit.

### Assumptions

- None.

## 5. Scope

### In scope

- New `Odyssey.Rules.Effects.EffectStackRules.IsStronger(EffectMechanicsSnapshot, EffectMechanicsSnapshot)` — the `ReplaceIfStronger` comparison `ADR-028` §15 assigns to `Odyssey.Rules`.
- New `Odyssey.Application.Effects.ActiveEffectStackingRules` static class: `ResolveStacking` (all 7 policies) and `ResolveActiveEffectStackConflict` (the 3 resolution outcomes).
- New `Odyssey.Application.Effects.ActiveEffectStackDecision`/`ActiveEffectStackDecisionKind`/`ActiveEffectStackConflict`/`ActiveEffectStackConflictResolution` types.
- Tests (`TC-ACTIVEEFFECT-020`-`035`), test metadata, task/plan docs, backlog row.

### Out of scope

- Any mutation of `IActiveEffectRepository`/`SqliteActiveEffectRepository` — no new method, no changed method (see §18 for the explicit decision not to add one in this task).
- Duration/expiry mechanisms (`ODY-S05-504`/`505`'s own territory).
- `WhileItemEquipped` wiring and item-triggered creation (`ODY-S05-505`'s own territory) — no real caller of `ActiveEffectStackingRules` is wired in this task.
- `RemoveActiveEffect` command and direct-creation permission gates (`ODY-S05-506`'s own territory).
- Any turn/round-based `EffectDurationType` value or combat-triggered effect application (out of scope for this entire block).
- `docs/adr/ADR-028_...md` itself — already `Accepted`; implemented here, not amended.
- Any project/assembly-definition-file change (`.csproj`/`.asmdef`) — this task's own `EffectStackRules.IsStronger` is deliberately designed to need none (see §4/§18).

### Allowed paths

```text
Packages/com.odyssey.rules/Runtime/Effects/EffectStackRules.cs
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectStackingRules.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectStackingRulesTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-503_Stacking_Policy_Resolution.md
docs/plans/active/ODY-S05-503_Stacking_Policy_Resolution.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.persistence/**
Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteEquipmentRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/EquipmentServiceTests.cs
Any `.csproj`/`.asmdef` file
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Rules` owns the `ReplaceIfStronger` comparison (`ADR-028` §15); `Odyssey.Application` owns stacking orchestration/decision (`ADR-028` §15). Neither `Odyssey.Domain` nor `Odyssey.Persistence` is touched.
- Authoritative-state and transaction boundary: no transaction exists in this task — every new method is pure, synchronous, and does no I/O.
- Serialization / compatibility boundary: none — no new wire/storage format is introduced.
- Time / RNG rule: the only time value used is the caller-supplied `UtcInstant now`, used solely to stamp `ActiveEffectStackConflict.RaisedAt` — never to compute duration/expiry.
- Unity / thread / lifetime rule: no Unity files; no threading concerns for pure functions.
- Dependency / licensing rule: no new dependency — `EffectStackRules.IsStronger` is deliberately designed not to need one (see §4).
- Security / privacy / redaction rule: not applicable — no `Error`/log output exists in this task's own code.
- Other: `ActiveEffectStackingRules.ResolveStacking` never itself queries `IActiveEffectRepository` — the caller supplies an already-loaded (or `null`) conflicting `ActiveEffectRecord`, exactly mirroring `ComputeBlockingIssues`'s own "caller loads, this decides" contract.

## 7. Expected behavior

### Scenario 1 — no existing conflicting effect

**Given** no `ActiveEffect` row yet exists for the candidate's own `TargetRef`+`EffectDefinitionRef`
**When** `ResolveStacking` is called with any `EffectStackPolicy`
**Then** the decision is always `CreateNewEffect` — every policy's own first-application behavior is identical.

### Scenario 2 — IndependentInstances

**Given** a conflicting existing effect
**When** `ResolveStacking` is called with `IndependentInstances`
**Then** the decision is `CreateNewEffect`, regardless of the existing row.

### Scenario 3 — RefreshDuration

**Given** a conflicting existing effect and a candidate representing a fresh application right now
**When** `ResolveStacking` is called with `RefreshDuration`
**Then** the decision is `RefreshExistingDuration`, carrying the existing row's own id plus the candidate's own `AppliedAt`/`ExpiresAt`; no new row is created.

### Scenario 4 — ReplaceIfStronger, stronger candidate

**Given** a conflicting existing effect whose snapshot the candidate's own snapshot is stronger than (per `EffectStackRules.IsStronger`)
**When** `ResolveStacking` is called with `ReplaceIfStronger`
**Then** the decision is `ReplaceExistingEffect`.

### Scenario 5 — ReplaceIfStronger, not-stronger candidate

**Given** the same setup but the candidate is not stronger (including a tie)
**When** `ResolveStacking` is called with `ReplaceIfStronger`
**Then** the decision is `IgnoreNewApplication`.

### Scenario 6 — ReplaceExisting

**Given** a conflicting existing effect
**When** `ResolveStacking` is called with `ReplaceExisting`
**Then** the decision is always `ReplaceExistingEffect`, regardless of relative strength.

### Scenario 7 — IncreaseStacks

**Given** a conflicting existing effect
**When** `ResolveStacking` is called with `IncreaseStacks`
**Then** the decision is `IncreaseExistingStack`, carrying only the existing row's own id; no new row.

### Scenario 8 — IgnoreNewApplication

**Given** a conflicting existing effect
**When** `ResolveStacking` is called with `IgnoreNewApplication`
**Then** the decision is `IgnoreNewApplication`, carrying no payload at all.

### Scenario 9 — RequestGMResolution and its full resolution cycle

**Given** a conflicting existing effect
**When** `ResolveStacking` is called with `RequestGMResolution`
**Then** the decision is `RequestGmResolution`, carrying a new `ActiveEffectStackConflict` (the candidate application, the conflicting id, `RaisedAt`); no row is created or mutated yet — the item-use command itself still succeeds.
**And when** `ResolveActiveEffectStackConflict` is later called with that conflict and each of `ApplyAsIndependentInstance`/`Replace`/`Ignore`
**Then** it returns `CreateNewEffect`/`ReplaceExistingEffect`/`IgnoreNewApplication` respectively, exactly as if that policy had been chosen directly.

### Required invariants

- Every `ActiveEffectStackDecision` combination of `Kind` + payload fields is validated by its own constructor — an invalid combination throws, never silently accepted.
- `ResolveStacking` rejects an `existingConflictingEffect` that does not share the candidate's own `TargetRef`/`EffectDefinitionRef`/`CampaignId` — a caller precondition failure, not a stacking outcome.
- No new row is ever implied for `RefreshDuration`/`IncreaseStacks`/`IgnoreNewApplication`/a not-stronger `ReplaceIfStronger`/`RequestGMResolution`.
- `EffectStackRules.IsStronger` is deterministic and total — every pair of valid snapshots produces a definite `true`/`false`, with a tie deliberately resolving to `false` (not stronger).

## 8. Deliverables

- Production code: `EffectStackRules.IsStronger` (Rules); `ActiveEffectStackingRules`, `ActiveEffectStackDecision`, `ActiveEffectStackDecisionKind`, `ActiveEffectStackConflict`, `ActiveEffectStackConflictResolution` (Application).
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectStackingRulesTests.cs`, `TC-ACTIVEEFFECT-020`-`035`.
- Scripts / CI: none.
- Configuration: none.
- Documentation: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none — no schema change.

## 9. Acceptance criteria

1. All 7 `EffectStackPolicy` behaviors are implemented by a pure function (or small function set), matching `ComputeBlockingIssues`'s own stylistic precedent (static, no I/O, caller-supplied already-loaded records).
2. `ReplaceIfStronger` delegates its comparison to new code in `Odyssey.Rules`, never inventing a comparison inside the Application layer.
3. `ActiveEffectStackConflict`/`ResolveActiveEffectStackConflict` are implemented; the chosen persistence variant (§4 of the governing ТЗ) — no new repository method, deferred to whichever future task actually needs one — is explicitly justified in this contract and the PR.
4. None of `ODY-S05-502`'s existing `IActiveEffectRepository`/`SqliteActiveEffectRepository` CRUD operations are modified.
5. Tests `TC-ACTIVEEFFECT-020`-`035` cover all 7 behaviors, the `IsStronger` comparison, and the full `RequestGMResolution` → `ResolveActiveEffectStackConflict` cycle with all three resolutions.
6. No duration/expiry/removal logic is introduced.
7. `dotnet test` is green across the full solution; backlog row `503` → `In Review`.
8. PR is Draft; merge decision left to the product owner.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-020` | .NET / NUnit (Persistence) | IndependentInstances always creates a new row | Pass |
| `TC-ACTIVEEFFECT-021` | .NET / NUnit (Persistence) | No existing conflicting effect → always CreateNew | Pass |
| `TC-ACTIVEEFFECT-022` | .NET / NUnit (Persistence) | RefreshDuration refreshes AppliedAt/ExpiresAt, no new row | Pass |
| `TC-ACTIVEEFFECT-023` | .NET / NUnit (Persistence) | ReplaceIfStronger replaces when candidate is stronger | Pass |
| `TC-ACTIVEEFFECT-024` | .NET / NUnit (Persistence) | ReplaceIfStronger ignores when candidate is not stronger | Pass |
| `TC-ACTIVEEFFECT-025` | .NET / NUnit (Persistence) | ReplaceExisting always replaces | Pass |
| `TC-ACTIVEEFFECT-026` | .NET / NUnit (Persistence) | IncreaseStacks increases StackCount, no new row | Pass |
| `TC-ACTIVEEFFECT-027` | .NET / NUnit (Persistence) | IgnoreNewApplication produces no mutation | Pass |
| `TC-ACTIVEEFFECT-028` | .NET / NUnit (Persistence) | RequestGMResolution raises a pending conflict, no immediate mutation | Pass |
| `TC-ACTIVEEFFECT-029` | .NET / NUnit (Persistence) | Resolve → ApplyAsIndependentInstance creates the candidate | Pass |
| `TC-ACTIVEEFFECT-030` | .NET / NUnit (Persistence) | Resolve → Replace replaces the original conflicting row | Pass |
| `TC-ACTIVEEFFECT-031` | .NET / NUnit (Persistence) | Resolve → Ignore produces no mutation | Pass |
| `TC-ACTIVEEFFECT-032` | .NET / NUnit (Persistence) | IsStronger true for a higher DefinitionSnapshotVersion | Pass |
| `TC-ACTIVEEFFECT-033` | .NET / NUnit (Persistence) | IsStronger false on a tie | Pass |
| `TC-ACTIVEEFFECT-034` | .NET / NUnit (Persistence) | ResolveStacking rejects a mismatched existing effect | Pass |
| `TC-ACTIVEEFFECT-035` | .NET / NUnit (Persistence) | ReplaceExisting against a genuinely persisted existing row | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no ADR edits, no `IActiveEffectRepository`/`SqliteActiveEffectRepository`/`ActiveEffect` aggregate change, no `.csproj`/`.asmdef` change, no duration/expiry/removal logic.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database, used only to build realistic "existing conflicting effect" fixtures via `ODY-S05-502`'s own unmodified `CreateActiveEffect`/`GetActiveEffect`.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any test of a real item-use/WhileItemEquipped caller, since none is wired in this task (`ODY-S05-505`'s own job).

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — two new static classes, a handful of new value/DTO types, no existing type or table changed.
- Version fields affected: none.
- Migration or upcaster: none — no schema change.
- Forward / backward behavior: older builds never reference the new types; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: none — no persistence write path exists in this task.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/effect test records; no real player data.
- Trust boundaries: not applicable — no I/O, no permission check in this task (permission gates for direct-creation/removal are `ODY-S05-506`'s own territory; item-triggered creation inherits the existing item-use model per `ADR-028` §10, wired by a future task).
- Authorization / audience checks: none introduced by this task.
- Redaction requirements: not applicable — no `Error`/log output.
- Log-safe fields: not applicable.
- Abuse / malformed input limits: every new type's constructor validates its own invariants and fails fast (`ArgumentException`/`ArgumentOutOfRangeException`).
- Security tests: not applicable to this task's own scope.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new cross-module (`Odyssey.Rules` + `Odyssey.Application`) decision mechanism with a genuinely new comparison algorithm — a `PLANS.md` §1.2 trigger, even though no persistence is touched.
- ExecPlan path: `docs/plans/active/ODY-S05-503_Stacking_Policy_Resolution.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #137 (`ODY-S05-502`); precedes `ODY-S05-505`'s own real-caller wiring (informally — no hard backlog dependency exists between `503` and `505`, both depending only on `502`).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs (`ADR-027`, `ADR-028`); `IActiveEffectRepository`/`SqliteActiveEffectRepository`/`ActiveEffect` aggregate.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.rules/Runtime/Effects/EffectStackRules.cs` — new file: `IsStronger`.
- `Packages/com.odyssey.application/Runtime/Effects/ActiveEffectStackingRules.cs` — new file: `ActiveEffectStackingRules`, `ActiveEffectStackDecision`, `ActiveEffectStackDecisionKind`, `ActiveEffectStackConflict`, `ActiveEffectStackConflictResolution`.
- `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectStackingRulesTests.cs` — new file, `TC-ACTIVEEFFECT-020`-`035`.
- `Tests/Metadata/test-catalog.json` — `TC-ACTIVEEFFECT-020`-`035` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-503` updated to `In Review`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors, on the first attempt. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 560/560 (544 predecessor + 16 new), all green on the first run. |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | No new `ERROR_CODES.md` rows required by this task. |
| `.\scripts\verify-test-structure.ps1` | PASS | |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (all 7 behaviors, ComputeBlockingIssues stylistic precedent) | Met | `TC-ACTIVEEFFECT-020`-`027`; code review of `ResolveStacking`. |
| AC-2 (ReplaceIfStronger delegates to new Odyssey.Rules code) | Met | `TC-ACTIVEEFFECT-023`/`024`/`032`/`033`; code review of `EffectStackRules`. |
| AC-3 (ActiveEffectStackConflict/Resolve implemented, variant justified) | Met | `TC-ACTIVEEFFECT-028`-`031`; §18 decision log. |
| AC-4 (no CRUD rewrite) | Met | `git diff --name-status`: `ActiveEffectRepositoryContracts.cs`/`SqliteActiveEffectRepository.cs`/`ActiveEffect.cs` unchanged. |
| AC-5 (tests cover all behaviors + comparison + full cycle) | Met | Table in §10. |
| AC-6 (no duration/expiry/removal logic) | Met | Diff review; no `ExpiresAt` computation, no `Status → Removed` transition beyond what `ReplaceExisting`'s own decision-carrying payload names for a future caller to apply. |
| AC-7 (dotnet test green, backlog In Review) | Met | Validation table above; backlog row updated. |
| AC-8 (Draft PR) | Met | PR #138 opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- `EffectStackRules.IsStronger` compares `EffectMechanicsSnapshot.DefinitionSnapshotVersion` rather than any real Ruleset-defined potency — the most defensible generic signal available today, given `ADR-028` §18.3's own rejection of a generic potency field and `Odyssey.Rules`'s own lack of a JSON dependency (§4/§18). A future task with a real Ruleset potency schema must replace this method's own body without changing its signature or `ResolveStacking`'s own call site.
- No real caller of `ActiveEffectStackingRules` exists yet — `ODY-S05-505`'s own job to wire one against real item-use/persistence.
- No cross-session persistence mechanism exists for a pending `ActiveEffectStackConflict` — it is a synchronous, in-memory value object; a future task must add one if a real GM workflow needs a conflict to survive across separate command invocations.

### Follow-up tasks

- `ODY-S05-504` — Non-Combat Duration/Expiry.
- `ODY-S05-505` — WhileItemEquipped Wiring + Item-Triggered Creation (most likely real consumer of this task's own decision layer).
- `ODY-S05-506` — RemoveActiveEffect Command + Direct-Creation Permission Gates.

### Self-review summary

- Scope review: diff touches only allowed paths; no ADR, `IActiveEffectRepository`/`SqliteActiveEffectRepository`/`ActiveEffect` aggregate, or project/assembly-definition-file change.
- Architecture review: mirrors `ComputeBlockingIssues`'s own decision-only precedent exactly; `Odyssey.Rules` gains a genuinely new comparison, not a relocated one; no new repository method, matching the migration block's own decide/apply task separation (`402`/`403`).
- Test review: all 7 policies, the comparison, and the full conflict-resolution cycle (all 3 outcomes) are covered; one test uses a genuinely persisted existing row (via `ODY-S05-502`'s own unmodified `CreateActiveEffect`/`GetActiveEffect`) rather than only in-memory construction.
- Security/privacy review: not applicable — no I/O, no `Error`/log output in this task's own code.
- Documentation/version review: task contract, ExecPlan, test catalog, and backlog all updated; no schema/manifest/protocol version bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Design decision: `ActiveEffectStackingRules`/`SqliteActiveEffectRepository` gain NO new persistence method in this task — Option (a) of the governing ТЗ's own §4, the option it names as "recommended."** Direct code read of `ItemDefinitionMigrationRules.ComputeBlockingIssues` (`ODY-S05-402`) confirmed the exact precedent this task follows: that method is a pure decision-only static function with zero I/O, and the actual database mutation was a *separate* later task's own new repository method (`ODY-S05-403`'s `ApplyItemDefinitionMigration`). By the same structural logic, `503` (this task) is the "decide" half of the stacking mechanism; whichever future task first needs to actually execute a decision against real persistence (most likely `505`, the first task with a real item-triggered creation caller) is free to add whatever repository method it needs at that time, informed by its own real call site — not guessed at here in the abstract. This also keeps `503` fully independent of `504`/`505`/`506` (all sibling tasks depending only on `502`, per the backlog's own dependency graph), avoiding a persistence method shaped around assumptions about how a future task will actually call it. Authority: `ItemDefinitionMigrationRules.cs`/`SqliteInventoryRepository.ApplyItemDefinitionMigration`'s own direct precedent; this task's own governing ТЗ §4(a)'s explicit "recommended" framing; the backlog's own sibling-task dependency structure (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §15, rows 2-5 each list only `502` as a dependency).
- 2026-09-12 — **Design decision: `ActiveEffectStackConflict` wraps a full `ActiveEffectRecord` (`CandidateApplication`) rather than duplicating `TargetRef`/`EffectDefinitionRef`/`EffectMechanicsSnapshot`/`SourceRef` as four separate properties.** `ADR-028` §7 rule 7 names those four fields explicitly, but `ActiveEffectRecord`/`ActiveEffect` already carry all four as their own fields — wrapping the whole candidate record is a faithful, non-redundant representation of the same information, and it is exactly the record a caller needs to actually create the effect if the conflict resolves as `ApplyAsIndependentInstance`/`Replace`, avoiding a second, parallel reconstruction step. Authority: `ADR-028` §7 rule 7's own four-field list; direct inspection of `ActiveEffectRecord`/`ActiveEffect`'s own existing fields showing full coverage.
- 2026-09-12 — **Design decision: `EffectStackRules.IsStronger` compares `EffectMechanicsSnapshot.DefinitionSnapshotVersion` (a higher version is "stronger"), not any parsed field inside `Payload`.** Two independent constraints ruled out a payload-content comparison: (1) `ADR-028` §18.3 explicitly rejected adding a generic potency field to `EffectMechanicsSnapshot`/`EffectDefinition`, precisely to avoid this ADR silently deciding Ruleset-specific game-design semantics; (2) direct read of `Odyssey.Rules.csproj`/`Odyssey.Rules.asmdef` confirmed the module has no JSON library dependency today, and adding one is a project-file change outside this task's own allowed paths, with no established payload schema to parse anyway (confirmed by search — every existing `MechanicsPayloadRef` fixture is an opaque reference string). `DefinitionSnapshotVersion` is the only Domain-owned, always-present, unambiguously-ordered numeric signal common to both snapshots being compared, and comparing it is a real, deterministic, testable operation over already-existing structured data (not an invented scale). This mirrors `AttributeCostRules`'s own "TEST FIXTURE ONLY — not production Ruleset balance data" honesty: a future task with a real Ruleset potency schema must replace this method's own body without touching its signature or `ResolveStacking`'s own call site. A tie is deliberately treated as "not stronger," matching `ReplaceIfStronger`'s own fallback to the least destructive outcome (`IgnoreNewApplication`) when the comparison cannot conclusively distinguish the two. Authority: `ADR-028` §7 rule 3 (assigns the comparison to `Odyssey.Rules`, "over the opaque... payload"); `ADR-028` §18.3 (rejects a generic potency field); direct `.csproj`/`.asmdef` read confirming no JSON dependency exists; `AttributeCostRules`'s own precedent for an honestly-documented fixture-level heuristic pending real Ruleset data.
- 2026-09-12 — **Decision: this task's new test file lives in `Odyssey.Tests.Persistence`, not a new dedicated Rules/Effects test project or `Odyssey.Tests.Unit`.** `ItemDefinitionMigrationRulesTests.cs` (a pure-function test file with no I/O of its own, testing `ComputeBlockingIssues`) already lives in `Odyssey.Tests.Persistence` — this task's own tests follow that exact placement precedent, and doing so let several tests use a genuinely persisted "existing conflicting effect" fixture (via `ODY-S05-502`'s own unmodified `CreateActiveEffect`/`GetActiveEffect`) without needing any new `.csproj` reference, since `Odyssey.Tests.Persistence` already references `Odyssey.Application.csproj`, which itself already references `Odyssey.Rules.csproj` transitively. Authority: direct code read confirming `ItemDefinitionMigrationRulesTests.cs`'s own location; direct `.csproj` read confirming the reference chain already reaches `Odyssey.Rules` with no edit needed.
- 2026-09-12 — Decision: no scope-guard file required any edit. Direct code read of all four guards `ODY-S05-502` narrowed confirmed each scans either the `Odyssey.Persistence` assembly's own types or the `Odyssey.Application.Inventory` namespace specifically; this task's new types live in `Odyssey.Application.Effects`/`Odyssey.Rules.Effects` and touch no `Odyssey.Persistence` file — confirmed by running both guard tests unmodified after adding this task's own files (both pass). Authority: direct code read of each guard's own scan scope; real `dotnet test` confirmation.
