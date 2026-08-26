# ExecPlan — ODY-S03-005: Dice Roll Engine, Host Authority, Modifiers & Reroll/Cancel

**Governing task contract:** `docs/tasks/active/ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` — full document, especially §7 (formula grammar/limits), §8 (d100), §12 (modifiers, no-hidden-GM-modifier rule), §13 (`DiceRoll` entity), §14 (RNG contract, host-only), §17 (full reroll), §18 (cancellation), §19 (`GMOverride`), §32 (`Roll.*` permission keys).
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` — full document; §38 point 4 confirms dice reuses the accepted RNG algorithm unchanged.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §6.1 (two-point action-check pattern, extended not reopened).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (append-only principle — `DiceRoll`/override are new events, not edits).
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`.
- `Packages/com.odyssey.application/Runtime/Random/RngContracts.cs` (`ADR-008`'s already-implemented RNG API) — read in full to reuse `IAuthoritativeRandomStreamFactory`/`IAuthoritativeRandomStream`/`RandomDecisionContext` exactly as-is.
- `Packages/com.odyssey.application/Runtime/Networking/Command/TokenMoveContracts.cs` (`ODY-S02-011`) — read as the closest prior art for the two-point authorization pattern applied to a host-authoritative command.
- `docs/tasks/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md` — read as the structural/stylistic template for this same revision's task contract and `Result`/`Error` layering.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S03-005` task boundary, fixed by the prior task — executed, not reopened).

## Investigation performed

1. Read `09_Dice_And_Game_Log` in full (2009 lines), focused on the roll lifecycle, formula grammar (§7.1's exact EBNF), modifier proposal/decision model (§12), `GMOverride` (§19), full reroll (§17), cancellation (§18), and the `Roll.*` permission key list (§32).
2. Read `RngContracts.cs` in full: confirmed `IAuthoritativeRandomStreamFactory`/`IAuthoritativeRandomStream`/`RandomDecisionContext`/`RandomSample`/`RngProofData` are already fully implemented per `ADR-008` (HMAC-SHA-256 stream derivation, xoshiro256** v1, rejection-sampling inclusive mapping) — this task reuses them exactly as-is via `NextInclusive(min, max, drawIndex)`, no new algorithm.
3. Read `TokenMoveContracts.cs`'s `MoveTokenService` in full as the closest prior art for a two-point (submission + pre-commit) authorization check on a host-authoritative in-memory command — the pattern this task's `DiceRollService` reuses.
4. Determined the architectural placement (task contract §3): `Odyssey.Domain.Dice.DiceFormulaParser` (pure, no dependency, matching `ODY-S03-004`'s `BoardGeometry` placement reasoning) for the formula grammar; `Odyssey.Application.Dice` (mirroring `Odyssey.Application.Board`) for the orchestration service, contracts, and in-memory store.
5. Determined the persistence-scope decision (task contract §3): in-memory only, not routed through SLICE-01's SQLite journal — unlike `ODY-S03-004` (which extended the already-durable `SqliteSceneRepository`), no durable dice-roll store exists yet to extend, and building one is explicitly `ODY-S03-007`'s job per `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5's own text. Mirrors `ODY-S02-011`'s justified use of a fresh in-memory store for a concept with no durable counterpart yet.
6. Determined the modifier proposal/decision shape (task contract §3): two explicit, separate service calls (`ProposeModifier` then `DecideModifier`), each producing a visible `ModifierEntry` — directly proving §12.3's "no hidden numeric GM modifier" rule, without building the full asynchronous `RollRequest` state machine (§10.2's `Requested`/`AwaitingPlayer`/`AwaitingGMConfirmation`/`Ready` statuses), which this task does not reopen — `SubmitRoll` itself is one atomic host-authoritative command, matching `ADR-002`'s command model directly.
7. Confirmed via `Grep` that no prior task introduced any `Odyssey.Application.Dice` code, avoiding any accidental duplication.

## Intended change

- New: `Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs` (+ `.meta`).
- New: `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollStore.cs`, `DiceRollService.cs` (+ `.meta`s).
- Changed: `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (11 new codes).
- New tests: `DotNet/Tests/Odyssey.Tests.Domain/Dice/DiceFormulaParserTests.cs` (`TC-DICE-001`–`004`), `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs` (`TC-DICE-005`–`018`).
- Registry updates: `docs/errors/ERROR_CODES.md` (11 new rows), `Tests/Metadata/test-catalog.json` (18 new `TC-DICE-*` entries).
- New: this task's contract, this ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row status).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

## Explicit non-goals

- No audience-aware delivery/redaction of the roll result — `ODY-S03-006`'s scope.
- No `GameLogEntry`/`DiceRoll` durable persistence or reconnect-replay — `ODY-S03-007`'s scope.
- No networking code — this task stays within `Odyssey.Domain`/`Odyssey.Application`.
- No full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2, not reopened).
- No full asynchronous `RollRequest` state machine (`AwaitingPlayer`/`AwaitingGMConfirmation`) — `SubmitRoll` is one atomic command; the interactive round-trip UX is a known, explicitly-flagged limitation, not silently incomplete.
- No `CampaignUserGroup`/session/role infrastructure — `ActorCanCreateRoll`/`ActorIsMainGm` are caller-supplied booleans, mirroring `ODY-S03-004`'s `MoveTokenRequest.ActorIsMainGm` simplification exactly.
