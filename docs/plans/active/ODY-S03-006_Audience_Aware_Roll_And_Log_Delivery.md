# ExecPlan — ODY-S03-006: Audience-Aware Roll & Log Delivery

**Governing task contract:** `docs/tasks/active/ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 — full section: the four audience kinds (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`), §16.2's "Main GM всегда имеет доступ к gameplay event," §16.4's evaluation-time membership rule, §16.5's all-or-nothing-record-or-nothing projection rule.
- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` — full document; §4 (`CampaignUserGroup` narrow read-model), §5 (integration atop `ADR-019` §7's pipeline, not a parallel mechanism), §6 (evaluation-time-not-creation-time resolution), §3.3 (each consumer keeps its own audience-kind vocabulary — no forced-unified enum), §8 (`PERM-INV-012` safe denial unchanged).
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §6.2/§7 — applied as-is, not reopened: the Application-layer-only visibility-check point before any payload reaches `Odyssey.Networking`.
- `Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionContracts.cs` (`ODY-S02-010`) — read in full as the direct structural precedent (`VisibilityPolicy.ComputeVisibleEntities`'s pure-function, MainGM-sees-all, safe-denial-via-omission shape).
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollService.cs` (`ODY-S03-005`) — the `DiceRoll` being redacted; both edited by this task to carry a required `Audience` field.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S03-006`'s already-fixed task boundary — not reopened).

## Investigation performed

1. Read `09_Dice_And_Game_Log` §16 in full: confirmed the four audience kinds and, critically, §16.2's direct textual statement that MainGM always has access to a gameplay event regardless of audience kind — resolving the task contract's own explicit open question about whether MainGM sees `SelectedParticipants` rolls it is not itself listed in.
2. Read `ADR-021` in full: confirmed §5 frames the extended audience model as additional inputs to `ADR-019` §7's existing pipeline, not a parallel mechanism; confirmed §4 fixes `CampaignUserGroup` as a narrow read-model (id/campaign/members/status/revision) with lifecycle commands explicitly deferred as ordinary future work, not an architectural question this task reopens; confirmed §6's evaluation-time rule (current membership, not membership frozen at roll-creation time) and §3.3's explicit non-unification of per-consumer audience-kind vocabularies.
3. Read `SceneProjectionContracts.cs` in full: confirmed `VisibilityPolicy.ComputeVisibleEntities`/`IsVisible` is a pure static function taking a single authoritative state plus a per-connection `ActorVisibilityContext`, with MainGM checked first and unconditionally, and safe denial expressed purely by omission from the returned collection (never a distinguishable error) — this shape is mirrored, not literally reused, because `SceneEntityVisibility`'s two-kind vocabulary is incompatible with `DiceRollAudienceKind`'s four-kind vocabulary (`ADR-021` §3.3).
4. Read `DiceContracts.cs`/`DiceRollService.cs` (`ODY-S03-005`) in full: confirmed `DiceRoll` had no audience field yet (explicitly flagged as `ODY-S03-006`'s scope in that task's own §13 security section) — this task adds `DiceRollAudienceKind`/`DiceRollAudience` and a required `Audience` field/constructor parameter on both `DiceRoll` and `SubmitRollRequest`, threading it through `SubmitRoll` and `RequestFullReroll` (a reroll carries the original's audience forward unchanged — a reroll is not itself a disclosure/revocation decision).
5. Decided architectural placement (task contract §3): a new `Odyssey.Application.Dice.DiceRollVisibilityPolicy` (not an extension of the existing `VisibilityPolicy`) — justified by `ADR-021` §3.3's explicit rule that each consumer keeps its own audience-kind vocabulary; forcing `DiceRoll` through `SceneEntityVisibility`'s two-kind model would mean inventing a lossy translation for no architectural benefit.
6. Decided the `CampaignUserGroup` fixture scope (task contract §3): a new shared `Odyssey.Application.Audience` namespace (not Dice-specific) holding `CampaignUserGroupStatus`/`CampaignUserGroup`/`ICampaignUserGroupDirectory`/`InMemoryCampaignUserGroupDirectory` — a query-only port and minimal in-memory fixture, matching `ADR-021` §4's own scoping (lifecycle commands are ordinary future `ADR-002` work, not this task's or that ADR's concern). Placed in a shared namespace, not under `Dice/`, because `ADR-021` frames the mechanism as cross-cutting (roll/board/log all consume the same audience concept), anticipating reuse by a future `ODY-S03-007` task for `GameLogEntry` audience resolution without requiring that task to import `Odyssey.Application.Dice`.
7. Decided the redaction-delivery boundary (task contract §3): a pure function pair, `DiceRollVisibilityPolicy.TryGetVisibleRoll`/`ComputeAudienceViews`, returning either the full unredacted `DiceRoll` wrapped in a `DiceRollView` or nothing at all (§16.5's baseline is all-or-nothing; field-level partial redaction is not implemented and is recorded as a known limitation) — no wire codec is built; a future task wires this policy's output to `Odyssey.Networking`, matching `ADR-019` §6.2's existing Application-layer-only check point.
8. Confirmed via `Grep` that no prior task introduced any `Odyssey.Application.Audience` namespace or `DiceRollVisibilityPolicy`, avoiding accidental duplication.

## Intended change

- Changed: `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs` — `DiceRollAudienceKind` enum, `DiceRollAudience` sealed class, `DiceRoll`/`SubmitRollRequest` extended with a required `Audience` field/parameter.
- Changed: `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs` — `SubmitRoll`/`RequestFullReroll` thread `Audience` through.
- New: `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` (+ folder and file `.meta`s) — `CampaignUserGroupStatus`, `CampaignUserGroup`, `ICampaignUserGroupDirectory`, `InMemoryCampaignUserGroupDirectory`.
- New: `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs` (+ `.meta`) — `DiceRollView`, `DiceRollVisibilityPolicy.TryGetVisibleRoll`/`ComputeAudienceViews`.
- Changed: `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs` — all 15 pre-existing `SubmitRollRequest` construction sites updated for the new required `Audience` argument (`DiceRollAudience.Public()`), no behavioral change to those tests.
- New: `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollVisibilityPolicyTests.cs` (`TC-DICE-019`–`023`, 6 test methods) — all four audience kinds plus safe denial.
- Registry updates: `Tests/Metadata/test-catalog.json` (5 new `TC-DICE-01[9]`–`023` entries covering the 6 new test methods). No new `ErrorCode`s introduced (safe denial is boolean/omission-based, matching `VisibilityPolicy`'s existing convention), so `docs/errors/ERROR_CODES.md` is unchanged.
- New: this task's contract, this ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row → `Done`, `ODY-S03-006` row → `In Review`).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

## Explicit non-goals

- No `CampaignUserGroup` lifecycle commands (create/rename/archive) — `ADR-021` §4 already deferred these as ordinary future `ADR-002` commands, not this task's or that ADR's architectural concern.
- No persistence or reconnect-replay of `DiceRoll`/audience state — `ODY-S03-007`'s scope.
- No full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2, not reopened).
- No new network/transport code and no new wire codec — if a wire format is ever needed for this policy's output, it is tested via the already-existing `InProcessSessionTransport`, not a new transport; this task does not itself touch `Odyssey.Networking`.
- No field-level partial redaction within a visible `DiceRollView` — §16.5's baseline is all-or-nothing; partial redaction (if the contract ever explicitly allows it) is a future, separately-scoped task.
- No edit to `ADR-019`/`ADR-021` — applied as-is.
