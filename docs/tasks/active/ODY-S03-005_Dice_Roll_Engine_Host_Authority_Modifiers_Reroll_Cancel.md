# ODY-S03-005 — Dice Roll Engine, Host Authority, Modifiers & Reroll/Cancel

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (vertical slice implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-005-dice-roll-engine-host-authority-modifiers-reroll-cancel`
**Pull request:** Draft — [#59](https://github.com/odyssey-services/Odyssey_VTT/pull/59) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Implement a host-authoritative dice roll engine covering roadmap §12.6 steps 2–6 and 10: roll intent submission with permission validation, a d100/formula result generated only host-side, a modifier proposal/decision pipeline with no hidden numeric GM adjustment, `GMOverride` as a separate, reasoned, immutable record, and full reroll/cancellation that preserve the original roll. Closes exit criteria 3 ("бросок рассчитывается только host") and 6 ("GM Override всегда оставляет audit trail").

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 fixes this as the second child task, independent of `ODY-S03-004`. No dice-roll logic exists anywhere in the repository yet.
- Value or risk reduction: without a host-authoritative roll engine, roadmap §12.6's central mechanic (a bribeable, host-computed d100 result) has no implementation, and exit criteria 3/6 have no code to satisfy them. Fixing the modifier/override/reroll/cancel model now, atop `ADR-008`'s already-accepted RNG, avoids a future task inventing an ad hoc, un-auditable adjustment path.
- Blocking or enabling relationship: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §6 — no dependency (independent of `ODY-S03-004`, may run in parallel; confirmed already merged). Blocks `ODY-S03-006` (audience-aware delivery needs a `DiceRoll` to redact) and `ODY-S03-007` (persistence needs roll/log entities to persist).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` (full document — §7, §8, §12, §13, §14, §17, §18, §19, §32)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (full document — RNG contract reused unchanged, §38 point 4)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §6.1 (two-point action-check pattern, extended not reopened)
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (append-only principle, not reopened)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`Result<T>`/`SafeReasonCode`, reused not extended)
- `Packages/com.odyssey.application/Runtime/Random/RngContracts.cs` (`ADR-008`'s already-implemented RNG API, reused as-is)
- `Packages/com.odyssey.application/Runtime/Networking/Command/TokenMoveContracts.cs` (`ODY-S02-011`) — read as the closest prior art for the two-point authorization pattern
- `docs/tasks/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md` — structural/stylistic template
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1/§5 (this task's fixed boundary, already set by the prior task — executed, not reopened)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (vertical slice implementation), backlog `ODY-S03-005`, roadmap §12.6 steps 2–6/10, §12.7 exit criteria 3/6.
- Existing test IDs: None reused (new `TC-DICE-*` series, first use).
- New test IDs introduced: `TC-DICE-001` through `TC-DICE-018`.

### Task-safe private context

- Approved summary / references: `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and the production code's doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `SLICE-03_IMPLEMENTATION_BACKLOG.md` is on `main`, listing `ODY-S03-005` as independent of `ODY-S03-004` — confirmed by `Read` before branching; `ODY-S03-004` (PR #58) is merged to `main` — confirmed by `git log --oneline -10`.
- `RngContracts.cs` already fully implements `ADR-008`'s RNG architecture (`IAuthoritativeRandomStreamFactory`, `IAuthoritativeRandomStream.NextInclusive`, `RandomDecisionContext.Create`, `DeterministicRandomStreamFactory`, HMAC-SHA-256 stream derivation, xoshiro256** v1) — confirmed by `Read`; this task calls it, introduces no new algorithm.
- No `Odyssey.Application.Dice` or `Odyssey.Domain.Dice` namespace exists anywhere in the repository prior to this task — confirmed by `Grep`.
- `09_Dice_And_Game_Log` §7.4's limits (`MaxDiceCount=100`, `MaxDiceGroups=20`, `MinSides=2`, `MaxSides=1000`) and §7.3's forbidden-syntax examples are explicit, machine-checkable constraints — confirmed by `Read`.
- `09_Dice_And_Game_Log` §12.3 states plainly: "MVP не поддерживает `HiddenGMModifier`... Любое число, участвующее в открываемом расчёте: хранится отдельным `ModifierEntry`" — confirmed by `Read`, the direct textual basis for this task's proposal/decision design.
- `09_Dice_And_Game_Log` §19.2 states: "исходный roll не редактируется; reason обязателен" — confirmed by `Read`, the direct textual basis for `RollOverride` being a separate record and the mandatory-reason check.
- `TokenMoveContracts.cs`'s `MoveTokenService` already establishes a real, working two-point (submission + pre-commit) authorization pattern over a synchronous in-memory command, with an explicit doc-comment justification for why that in-memory store does not persist — confirmed by `Read`; this task's `DiceRollStore` follows the same justified pattern for a different concept with no durable counterpart yet.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs` (new) — `09_Dice_And_Game_Log` §7's MVP formula grammar, limits, and `TryParse`/`Parse`.
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollStore.cs`, `DiceRollService.cs` (new) — `SubmitRoll`, `ProposeModifier`, `DecideModifier`, `ApplyOverride`, `RequestFullReroll`, `CancelRoll`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 11 new codes.
- New tests: `DotNet/Tests/Odyssey.Tests.Domain/Dice/DiceFormulaParserTests.cs`, `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs`.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` registry updates.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row status).

### Out of scope

- Audience-aware delivery/redaction of the roll result (`ODY-S03-006`).
- `GameLogEntry`/`DiceRoll` durable persistence and reconnect-replay (`ODY-S03-007`).
- Any `Odyssey.Networking` code.
- Full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2).
- The full asynchronous `RollRequest` state machine (`AwaitingPlayer`/`AwaitingGMConfirmation`/`Ready`) — `SubmitRoll` is one atomic host-authoritative command; the interactive round-trip is a known, explicitly-flagged limitation.
- `CampaignUserGroup`/session/role infrastructure — `ActorCanCreateRoll`/`ActorIsMainGm` are caller-supplied booleans.
- Any edit to `ADR-004`/`ADR-008`/`ADR-012`/`ADR-019`.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Dice/**
Packages/com.odyssey.application/Runtime/Dice/**
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
DotNet/Tests/Odyssey.Tests.Domain/Dice/**
DotNet/Tests/Odyssey.Tests.Unit/Dice/**
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md
docs/plans/active/ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md
docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `DiceFormula`/`DiceFormulaParser` live in `Odyssey.Domain` (no dependency, `ADR-001` §5) as pure text parsing with no ruleset semantics, matching `ODY-S03-004`'s `BoardGeometry` placement reasoning; failure is a Domain-only enum, never `Result<T>` (Domain must not reference Application). `DiceRollService`/`DiceContracts`/`DiceRollStore` live in `Odyssey.Application.Dice` (depends on Domain + Rules for `RulesetVersion`), mirroring `Odyssey.Application.Board`.
- Authoritative-state and transaction boundary: `DiceRollStore` is in-memory, single-process — no distributed transaction concern; `DiceRollService` is the sole caller of `IAuthoritativeRandomStream` (§14.2's "только host вызывает production RNG").
- Serialization / compatibility boundary: Not applicable — no persisted schema, no wire format.
- Time / RNG rule: reuses `ADR-008`'s RNG contract unchanged — no new algorithm, no new derivation, `RngAlgorithmVersion`/`RngProofData` recorded per die as already defined.
- Unity / thread / lifetime rule: not applicable — no Unity-side code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: this task's own `DiceRoll`/`ModifierEntry`/`RollOverride` carry no audience/visibility field yet — audience-aware redaction is `ODY-S03-006`'s explicit scope; this task's own store returns full, unredacted data to any caller (acceptable since nothing in this task reaches a network boundary).
- Performance or platform constraint: Not applicable at this scale (single roll, ≤100 dice per §7.4).
- Other: `ADR-019`'s two-point (submission + pre-commit) authorization discipline is applied even though this synchronous path has no real intervening concurrency window — documented explicitly as deliberate, not overlooked redundancy.

## 7. Expected behavior

### Scenario 1 — authorized actor submits a roll, host generates the result

**Given** an actor flagged `ActorCanCreateRoll`
**When** they submit `SubmitRoll` with a valid formula (e.g. `1d100`)
**Then** the result succeeds, `NaturalResults` are drawn only via `IAuthoritativeRandomStream`, and `FinalTotal` equals the sum of dice and constants (exit criterion 3).

### Scenario 2 — unauthorized actor cannot generate a roll

**Given** an actor not flagged `ActorCanCreateRoll`
**When** they submit `SubmitRoll`
**Then** the result is a typed `dice.roll.denied` failure and no `DiceRoll` is created or stored.

### Scenario 3 — modifier proposed and decided as two separate, visible steps

**Given** a resolved roll
**When** a player proposes a modifier via `ProposeModifier`, then a MainGM-flagged actor decides it via `DecideModifier`
**Then** the proposal does not count toward `FinalTotal` until explicitly decided; a `Changed`/`Rejected` decision without a reason is rejected (§12.2/§12.3's "no hidden numeric GM modifier").

### Scenario 4 — `GMOverride` requires a reason and never rewrites the original roll

**Given** a resolved roll
**When** a MainGM-flagged actor calls `ApplyOverride` without a reason
**Then** the result is a typed `dice.override.reason_required` failure; with a reason, a separate `RollOverride` record is created and the original roll's `NaturalResults`/`FinalTotal` are unchanged, only its `Status` flips to `Overridden` (exit criterion 6).

### Scenario 5 — full reroll and cancellation preserve the original

**Given** a resolved roll
**When** `RequestFullReroll` or `CancelRoll` is called by the original actor or a MainGM-flagged actor
**Then** a reroll produces a *new* `DiceRoll` chained via `PreviousRollId`, with the original's `Status` flipped to `SupersededByReroll`; a cancellation flips `Status` to `Cancelled`; in both cases the original's own data (`NaturalResults`, `FormulaOriginal`) is preserved, never deleted or rewritten (roadmap §12.6 step 10).

### Required invariants

- No `DiceRoll`'s `NaturalResults`/`FormulaOriginal`/`FormulaNormalized`/`BaseTotal` ever changes after creation.
- Every number contributing to `FinalTotal` is a visible, sourced `ModifierEntry` — no hidden adjustment path exists.
- `ADR-004`, `ADR-008`, `ADR-012`, `ADR-019` files are unmodified.
- No new `Odyssey.Networking` reference is introduced anywhere in this task's diff.

## 8. Deliverables

- Production code: `DiceFormula.cs`, `DiceContracts.cs`, `DiceRollStore.cs`, `DiceRollService.cs`, extended `ErrorCodes.cs`.
- Tests: `DiceFormulaParserTests.cs` (12 test methods, `TC-DICE-001`–`004`), `DiceRollServiceTests.cs` (15 test methods, `TC-DICE-005`–`018`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `ERROR_CODES.md`, `test-catalog.json`, `SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row).
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable (no persistence).

## 9. Acceptance criteria

1. `DiceFormulaParser` implements exactly `09_Dice_And_Game_Log` §7.1's grammar and §7.4's limits, with golden-vector/negative tests (`TC-DICE-001`–`004`).
2. `DiceRollService.SubmitRoll` generates `NaturalResults` only via `IAuthoritativeRandomStream`, rejects an unauthorized actor with `dice.roll.denied` before any RNG use, and rejects an invalid formula with `dice.formula.invalid` (`TC-DICE-005`–`007`).
3. `ProposeModifier`/`DecideModifier` implement §12's two-step visible modifier model; a `Changed`/`Rejected` decision without a reason is rejected (`TC-DICE-008`–`010`).
4. `ApplyOverride` requires a non-empty reason, requires `ActorIsMainGm`, produces a separate `RollOverride` record, and never rewrites the original roll's `NaturalResults`/`FinalTotal` (`TC-DICE-011`–`013`).
5. `RequestFullReroll` produces a new `DiceRoll` chained via `PreviousRollId`, flips the original's `Status` to `SupersededByReroll`, and rejects an unauthorized actor (`TC-DICE-014`–`015`).
6. `CancelRoll` requires a reason for an already-resolved roll, flips `Status` to `Cancelled` without deleting data, and rejects an unauthorized actor (`TC-DICE-016`–`018`).
7. `docs/errors/ERROR_CODES.md` and `Tests/Metadata/test-catalog.json` are updated for all 11 new error codes and all 18 new test cases.
8. `ADR-004`, `ADR-008`, `ADR-012`, and `ADR-019` files are unmodified by this task's diff.
9. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test` all pass.
10. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-DICE-001` | `.NET` / `dotnet test` | Single dice group parsing | Pass |
| `TC-DICE-002` | `.NET` / `dotnet test` | Compound/mixed-sign formulas | Pass |
| `TC-DICE-003` | `.NET` / `dotnet test` | Forbidden syntax rejected | Pass |
| `TC-DICE-004` | `.NET` / `dotnet test` | Dice/sides limits enforced; d100 valid | Pass |
| `TC-DICE-005` | `.NET` / `dotnet test` | Host-only result generation | Pass |
| `TC-DICE-006` | `.NET` / `dotnet test` | Unauthorized roll rejected | Pass |
| `TC-DICE-007` | `.NET` / `dotnet test` | Invalid formula rejected | Pass |
| `TC-DICE-008` | `.NET` / `dotnet test` | Modifier proposed/accepted separately | Pass |
| `TC-DICE-009` | `.NET` / `dotnet test` | Changed/Rejected requires reason | Pass |
| `TC-DICE-010` | `.NET` / `dotnet test` | Modifier decision requires MainGM | Pass |
| `TC-DICE-011` | `.NET` / `dotnet test` | Override requires reason | Pass |
| `TC-DICE-012` | `.NET` / `dotnet test` | Override requires MainGM | Pass |
| `TC-DICE-013` | `.NET` / `dotnet test` | Override preserves original roll | Pass |
| `TC-DICE-014` | `.NET` / `dotnet test` | Reroll creates new record, preserves original | Pass |
| `TC-DICE-015` | `.NET` / `dotnet test` | Reroll requires authorization | Pass |
| `TC-DICE-016` | `.NET` / `dotnet test` | Cancel requires reason | Pass |
| `TC-DICE-017` | `.NET` / `dotnet test` | Cancel preserves original data | Pass |
| `TC-DICE-018` | `.NET` / `dotnet test` | Cancel requires authorization | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- Read `DiceRollService.cs` end-to-end to confirm every mutating method routes through the two-point authorization pattern where applicable, and that `RngContracts.cs`'s existing API is reused unmodified.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity-side code; `.meta` files added for repository consistency.
- Network topology or database fixture: Not applicable — in-memory only.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Unity Editor compile/EditMode/PlayMode — no Unity-side code.
- Any networking or persistence test — `ODY-S03-006`/`007`'s scope.
- Any test of the full asynchronous `RollRequest` state machine — not built by this task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — new module, no existing contract changed.
- Version fields affected: None.
- Migration or upcaster: Not applicable — no persistence.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: Formula text, dice results, modifier labels/values, override reasons — no secret, credential, or personal data.
- Trust boundaries: `DiceRollService` is the host-authoritative trust boundary for roll generation and override/reroll/cancel authorization — no client-supplied result or permission decision is trusted.
- Authorization / audience checks: `ActorCanCreateRoll`/`ActorIsMainGm`/`DecidedByUserIsMainGm` checks are this task's only authorization surface; no audience/visibility redaction exists yet (`ODY-S03-006`'s scope).
- Redaction requirements: Not applicable to this task's own execution.
- Log-safe fields: `Error` responses use only the existing `SafeReasonCode`/`UserMessageKey` vocabulary; no raw `RollId`/`UserId` embedded in a message string beyond the already-typed `Error` fields.
- Abuse / malformed input limits: Formula length/dice-count/dice-group/sides limits (§7.4) are enforced before any RNG use.
- Security tests: `TC-DICE-006`, `TC-DICE-009`, `TC-DICE-010`, `TC-DICE-012`, `TC-DICE-015`, `TC-DICE-018` — all confirm no state mutation occurs on a rejected request, not merely that an error is returned.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2. This task introduces a new Application port/module (`Odyssey.Application.Dice`) and a new Domain module (`Odyssey.Domain.Dice`), affects authoritative RNG use (`ADR-008`), and required real investigation before the implementation path was known — determining the persistence-scope boundary (in-memory vs. durable, requiring a fresh comparison against both `ODY-S03-004`'s and `ODY-S02-011`'s prior reasoning), the modifier proposal/decision shape (two-step vs. the full async `RollRequest` machine), and reuse of the already-implemented `RngContracts.cs` API. This directly matches §1.2's "affects... time, or randomness," "introduces or changes an Application port," and "requires investigation before the implementation path is known" triggers.
- ExecPlan path: `docs/plans/active/ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: no dependency on `ODY-S03-004` (mutually independent per `SLICE-03_IMPLEMENTATION_BACKLOG.md` §6). Blocks `ODY-S03-006` (audience-aware delivery) and `ODY-S03-007` (persistence).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row).
- Documents that must not change: `docs/adr/ADR-004`/`ADR-008`/`ADR-012`/`ADR-019`, `docs/tasks/active/ODY-S03-000`–`004_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first `Odyssey.Application.Dice` contract (new module, no existing contract version bumped).
- Documentation version changes: None.
- Changelog or release-note requirement: None.

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

- `Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs` — new.
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollStore.cs`, `DiceRollService.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — extended (11 new codes).
- `DotNet/Tests/Odyssey.Tests.Domain/Dice/DiceFormulaParserTests.cs`, `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs` — new (27 tests total).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — 11 new registry rows, 18 new test-catalog entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | All 6 test projects passed: Contracts 1/1, Domain 27/27 (includes 12 new `TC-DICE-001`–`004` tests), Networking 67/67, Unit 99/99 (includes 15 new `TC-DICE-005`–`018` tests), Architecture 2/2, Persistence 55/55. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005` (error registry, 11 new codes). |
| CI — PR #59, commit `6aaf778` | Passed | Run [32987266220](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32987266220): `repository-policy-format-structure`, `dotnet-restore-build-test` (real production build+test), `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-DICE-001`–`004`: single/compound formulas, forbidden syntax, limits, d100 — all pass. |
| AC-2 | Passed | `TC-DICE-005`–`007`: host-only generation, unauthorized-rejected, invalid-formula-rejected. |
| AC-3 | Passed | `TC-DICE-008`–`010`: propose/accept as separate steps, reason required for Changed/Rejected, MainGM required to decide. |
| AC-4 | Passed | `TC-DICE-011`–`013`: override reason/MainGM required, original roll unchanged, separate `RollOverride` record. |
| AC-5 | Passed | `TC-DICE-014`–`015`: new record chained via `PreviousRollId`, original flips to `SupersededByReroll`, unauthorized rejected. |
| AC-6 | Passed | `TC-DICE-016`–`018`: reason required, data preserved on cancel, unauthorized rejected. |
| AC-7 | Passed | `ERROR_CODES.md` (11 rows), `test-catalog.json` (18 entries) both updated. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-004`/`008`/`012`/`019` file touched. |
| AC-9 | Passed | See Validation results table above — all four commands pass. |
| AC-10 | Passed | `git status --porcelain` matches §5's Allowed paths exactly. |
| AC-11 | Passed | Draft PR [#59](https://github.com/odyssey-services/Odyssey_VTT/pull/59) open; all 4 required CI checks `SUCCESS` on run 32987266220 (commit `6aaf778`); PR remains Draft pending explicit owner confirmation before any merge. |

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-08-26 — Place `DiceFormulaParser` in `Odyssey.Domain` (pure, no dependency), `DiceRollService` in `Odyssey.Application.Dice` — Authority: `ADR-001` §5's dependency matrix; `ODY-S03-004`'s `BoardGeometry`/`Odyssey.Application.Board` placement precedent.
- 2026-08-26 — Keep `DiceRollStore` in-memory, not routed through SLICE-01's SQLite journal — Authority: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5's own text assigning durable persistence to `ODY-S03-007`; `ODY-S02-011`'s precedent for a justified fresh in-memory store when no durable counterpart exists yet.
- 2026-08-26 — Implement modifier proposal/decision as two explicit service calls rather than the full asynchronous `RollRequest` state machine — Authority: `09_Dice_And_Game_Log` §12.3's "no hidden numeric GM modifier" rule is fully provable by the two-call model; the full interactive round-trip (`AwaitingPlayer`/`AwaitingGMConfirmation`) is a materially larger scope not required by roadmap §12.6's own step list.
- 2026-08-26 — `ActorCanCreateRoll`/`ActorIsMainGm` as caller-supplied booleans, not resolved from a real session — Authority: mirrors `ODY-S03-004`'s `MoveTokenRequest.ActorIsMainGm` exact precedent, avoiding a dependency on the not-yet-reopened session/role infrastructure.

### Approved task changes

- None.
