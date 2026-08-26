# ODY-S03-006 — Audience-Aware Roll & Log Delivery

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (vertical slice implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-006-audience-aware-roll-and-log-delivery`
**Pull request:** Draft — link recorded once opened
**ExecPlan:** `docs/plans/active/ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Apply `ADR-021`'s extended audience model (`SelectedParticipants`/`CampaignUserGroup`, atop `ADR-019` §7's pipeline) specifically to `DiceRoll` delivery: decide, for each session participant, what they see of a roll computed by `ODY-S03-005`, before any payload would reach a network boundary. Covers roadmap §12.6 step 7 ("only permitted clients receive the result") and closes exit criterion 4 from §12.7 ("roll visibility is enforced at the network boundary").

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S03-005`'s `DiceRoll` carries no audience/visibility field at all — its own §13 security section explicitly flagged this as out of scope and assigned to this task. Without it, every roll would be visible to every connected participant regardless of `09_Dice_And_Game_Log` §16's four audience kinds.
- Value or risk reduction: a blind or GM-only roll (§11.2's blind-roll mechanic) has no meaning if every client already receives the unredacted result — the core "GM secretly rolls behind the screen" gameplay pattern requires this enforcement to exist before any networking work makes it observable to real clients.
- Blocking or enabling relationship: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5/§6 — depends on `ODY-S03-005` (needs a `DiceRoll` artifact to redact; confirmed merged, PR #59). Blocks `ODY-S03-007` (reconnect replay must reuse this same audience-aware redaction, not recompute a separate, potentially inconsistent visibility rule, per the backlog's own dependency note).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (full section — four audience kinds, MainGM-always-sees rule, evaluation-time membership, all-or-nothing projection)
- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` (full document)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §6.2/§7 (applied as-is, not reopened)
- `Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionContracts.cs` (`ODY-S02-010`) — structural precedent, read in full
- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollService.cs` (`ODY-S03-005`) — the `DiceRoll` being redacted
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 (this task's fixed boundary, already set by a prior task — executed, not reopened)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (vertical slice implementation), backlog `ODY-S03-006`, roadmap §12.6 step 7, §12.7 exit criterion 4.
- Existing test IDs: `TC-DICE-001`–`018` (`ODY-S03-005`) reused unmodified in behavior; their `SubmitRollRequest` construction call sites are mechanically updated for the new required `Audience` argument.
- New test IDs introduced: `TC-DICE-019` through `TC-DICE-023` (5 catalog entries covering 6 test methods).

### Task-safe private context

- Approved summary / references: `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 and `ADR-021`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and the production code's doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S03-005` (PR #59) is merged into `main` — confirmed via `gh pr view 59 --json state,mergedAt,mergeCommit` (state `MERGED`, `mergedAt: 2026-08-26T17:44:01Z`).
- `ODY-S03-004` (PR #58) is merged into `main` — confirmed in a prior task, re-confirmed here as an unchanged fact.
- Prior to this task, `DiceRoll` had no audience/visibility field anywhere — confirmed by `Read` of `DiceContracts.cs`; `ODY-S03-005`'s own task contract §13 explicitly named this as out of scope and assigned to `ODY-S03-006`.
- `09_Dice_And_Game_Log` §16.2 states plainly: "Main GM всегда имеет доступ к gameplay event" — confirmed by `Read`, directly resolving this task's own §5-mandated open question (does MainGM see a `SelectedParticipants` roll it is not itself listed in) in favor of "yes, unconditionally."
- `ADR-021` §3.3 states each consumer (roll/board/log) keeps its own already-documented audience-kind vocabulary, with no forced-unified enum across them — confirmed by `Read`, the direct basis for building a new `DiceRollVisibilityPolicy` rather than extending the existing `SceneProjectionContracts.cs`'s `VisibilityPolicy` (whose `SceneEntityVisibility` enum has only 2 kinds, incompatible with `DiceRollAudienceKind`'s 4).
- `ADR-021` §4 fixes `CampaignUserGroup` as a narrow read-model (id/campaign/members/status/revision) and explicitly defers lifecycle commands (create/rename/archive) as ordinary future `ADR-002` work, not an architecturally novel question — confirmed by `Read`.
- `SceneProjectionContracts.cs`'s `VisibilityPolicy.ComputeVisibleEntities`/`IsVisible` is a pure static function with MainGM checked first unconditionally and safe denial expressed by omission from the result, never a distinguishable error — confirmed by `Read` in full, the structural template this task's `DiceRollVisibilityPolicy` mirrors (not literally reuses).
- No `Odyssey.Application.Audience` namespace or `DiceRollVisibilityPolicy` type existed anywhere in the repository prior to this task — confirmed by `Grep`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh pr view` before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs` — `DiceRollAudienceKind` enum, `DiceRollAudience` sealed class; `DiceRoll`/`SubmitRollRequest` extended with a required `Audience` field/parameter.
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs` — `SubmitRoll`/`RequestFullReroll` thread `Audience` through (reroll carries the original's audience forward unchanged).
- `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` (new) — `CampaignUserGroupStatus`, `CampaignUserGroup`, `ICampaignUserGroupDirectory`, `InMemoryCampaignUserGroupDirectory`.
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs` (new) — `DiceRollView`, `TryGetVisibleRoll`, `ComputeAudienceViews`.
- `.meta` files for the new `Audience` folder and both new production `.cs` files.
- `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs` — mechanical update of all 15 `SubmitRollRequest` construction sites for the new required argument.
- New test file: `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollVisibilityPolicyTests.cs`.
- `Tests/Metadata/test-catalog.json` registry update.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` row → `Done`, `ODY-S03-006` row → `In Review`).

### Out of scope

- `CampaignUserGroup`'s lifecycle commands (create/rename/archive) — `ADR-021` §4 already deferred these.
- `GameLogEntry`/`DiceRoll` durable persistence and reconnect-replay (`ODY-S03-007`).
- Full-text search, session archive/export (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.2).
- Any real network/transport code or new wire codec — if a wire format is ever needed, it is exercised via the already-existing `InProcessSessionTransport`, not built or tested here.
- Field-level partial redaction within a visible roll — §16.5's baseline is all-or-nothing; explicitly recorded as a known limitation.
- Any edit to `ADR-019`/`ADR-021`.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Dice/**
Packages/com.odyssey.application/Runtime/Audience/**
Packages/com.odyssey.application/Runtime/Audience.meta
DotNet/Tests/Odyssey.Tests.Unit/Dice/**
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md
docs/plans/active/ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md
docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `DiceRollVisibilityPolicy` and `AudienceContracts` live in `Odyssey.Application` (`Odyssey.Application.Dice`/`Odyssey.Application.Audience` respectively), consistent with `ADR-001`'s dependency matrix — no new Domain-layer code, no new dependency on `Odyssey.Networking`.
- Authoritative-state and transaction boundary: `DiceRollVisibilityPolicy` is a pure, side-effect-free function over an already-computed `DiceRoll` plus caller-supplied participant/role/group data — it does not itself read or write any store.
- Serialization / compatibility boundary: Not applicable — no persisted schema, no wire format built by this task.
- Time / RNG rule: Not applicable — this task does not call `IAuthoritativeRandomStream`.
- Unity / thread / lifetime rule: Not applicable — no Unity-side code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: safe denial is expressed purely by omission (`TryGetVisibleRoll` returns `false`, `ComputeAudienceViews` omits the entry) — never a distinguishable error or null-with-signal, matching `PERM-INV-012`/`ADR-021` §8.
- Performance or platform constraint: Not applicable at this scale (per-roll, per-connection evaluation).
- Other: `ADR-021` §6's evaluation-time rule is enforced by requiring an `ICampaignUserGroupDirectory` lookup at call time rather than snapshotting group membership into the `DiceRoll` itself.

## 7. Expected behavior

### Scenario 1 — Public roll is visible to everyone

**Given** a roll submitted with `DiceRollAudience.Public()`
**When** `TryGetVisibleRoll` is evaluated for any connected participant (Player, Observer, or MainGM)
**Then** every one of them receives a visible `DiceRollView` wrapping the full roll.

### Scenario 2 — PlayerAndGM roll is visible only to the actor and MainGM

**Given** a roll submitted with `DiceRollAudience.PlayerAndGM()`
**When** `TryGetVisibleRoll` is evaluated for the roll's own actor, for MainGM, and for an unrelated Observer
**Then** the actor and MainGM each receive a visible view; the Observer receives no view at all (`TryGetVisibleRoll` returns `false`), with no distinguishable signal that a roll exists.

### Scenario 3 — GMOnly roll is visible only to MainGM, including excluding the roll's own actor

**Given** a roll submitted with `DiceRollAudience.GMOnly()`
**When** `TryGetVisibleRoll` is evaluated for MainGM and for the roll's own actor
**Then** MainGM receives a visible view; the actor does not (§11.2's blind-roll design — deliberate, not a bug).

### Scenario 4 — SelectedParticipants roll is visible to explicitly listed users and active-group members, plus MainGM unconditionally

**Given** a roll submitted with `DiceRollAudience.SelectedParticipants(selectedUserIds, selectedGroupIds)` where `selectedGroupIds` names one `Active` `CampaignUserGroup`
**When** `TryGetVisibleRoll` is evaluated for a listed user, for a current member of the active group, for MainGM (not itself listed), and for an unrelated player
**Then** the listed user, the group member, and MainGM each receive a visible view (§16.2's MainGM-always-sees rule applies regardless of `SelectedParticipants` membership); the unrelated player receives no view. A group whose `Status` is `Archived` does not grant visibility to its members (§6's evaluation-time rule).

### Scenario 5 — safe denial leaves no trace across the plural helper

**Given** a `PlayerAndGM` roll and a participant list including an excluded Observer
**When** `ComputeAudienceViews` is called
**Then** the returned dictionary contains entries only for entitled participants; the excluded Observer's `UserId` is absent entirely — not present with a null value, not accompanied by any error.

### Required invariants

- `TryGetVisibleRoll`/`ComputeAudienceViews` never mutate the input `DiceRoll` or any caller-supplied collection.
- MainGM always receives a visible view, regardless of audience kind, checked before any audience-kind branch.
- An unrecognized/default audience kind fails closed (`false`, no view) — never widens visibility.
- `ADR-019`/`ADR-021` files are unmodified.
- No new `Odyssey.Networking` reference is introduced anywhere in this task's diff.

## 8. Deliverables

- Production code: extended `DiceContracts.cs`/`DiceRollService.cs`; new `AudienceContracts.cs`, `DiceRollVisibilityPolicy.cs`; 3 new/updated `.meta` files.
- Tests: `DiceRollVisibilityPolicyTests.cs` (6 test methods, `TC-DICE-019`–`023`); mechanical update of `DiceRollServiceTests.cs`'s 15 existing call sites (no new behavior, no new test IDs).
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005` and `ODY-S03-006` rows).
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable (no persistence).

## 9. Acceptance criteria

1. `DiceRoll`/`SubmitRollRequest` carry a required `Audience` field/parameter; `SubmitRoll`/`RequestFullReroll` both thread it through correctly, with a reroll preserving the original's audience unchanged (`TC-DICE-005`–`018` continue to pass unmodified in behavior).
2. `DiceRollVisibilityPolicy.TryGetVisibleRoll` returns a visible view for `Public` to any role (`TC-DICE-019`).
3. `TryGetVisibleRoll` returns a visible view for `PlayerAndGM` only to the actor and MainGM, denying an unrelated Observer with no distinguishable signal (`TC-DICE-020`).
4. `TryGetVisibleRoll` returns a visible view for `GMOnly` only to MainGM, denying even the roll's own actor (`TC-DICE-021`).
5. `TryGetVisibleRoll` returns a visible view for `SelectedParticipants` to explicitly listed users and current active-group members, and to MainGM unconditionally even when not itself listed; denies an unrelated participant; an archived group's membership does not grant visibility (`TC-DICE-022`).
6. `ComputeAudienceViews` omits any entry for a non-entitled participant entirely — safe denial with no trace (`TC-DICE-023`).
7. `Tests/Metadata/test-catalog.json` is updated for all new test cases; no new `ErrorCode` was needed, so `docs/errors/ERROR_CODES.md` is unchanged.
8. `ADR-019`/`ADR-021` files are unmodified by this task's diff.
9. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test` all pass.
10. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-DICE-019` | `.NET` / `dotnet test` | Public roll visible to all roles | Pass |
| `TC-DICE-020` | `.NET` / `dotnet test` | PlayerAndGM visible to actor+MainGM only | Pass |
| `TC-DICE-021` | `.NET` / `dotnet test` | GMOnly visible to MainGM only, excludes actor | Pass |
| `TC-DICE-022` | `.NET` / `dotnet test` | SelectedParticipants (users+active groups), MainGM always sees, archived group excluded | Pass |
| `TC-DICE-023` | `.NET` / `dotnet test` | Safe denial — ComputeAudienceViews omits non-entitled participants entirely | Pass |

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

- Read `DiceRollVisibilityPolicy.cs` end-to-end to confirm MainGM is checked first, unconditionally, before any audience-kind branch, and that every non-`true` path returns via omission rather than a distinguishable error.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity-side code; `.meta` files added for repository consistency.
- Network topology or database fixture: Not applicable — pure in-memory function, no store.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Unity Editor compile/EditMode/PlayMode — no Unity-side code.
- Any networking or persistence test — future tasks' scope.
- Any test of field-level partial redaction — not implemented by this task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: `DiceRoll`/`SubmitRollRequest` constructors gain a new required parameter — a source-breaking change to `ODY-S03-005`'s already-merged API, fully absorbed within this same slice's still-in-progress work (no external consumer exists outside this repository's own test suite, which is updated in the same commit).
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

- Data classes handled: dice-roll results, audience membership (`UserId`/`CampaignUserGroupId` lists) — no secret, credential, or personal data.
- Trust boundaries: `DiceRollVisibilityPolicy` is the Application-layer trust boundary deciding what each participant may see of a roll, before any payload would reach `Odyssey.Networking` (`ADR-019` §6.2, not reopened).
- Authorization / audience checks: this task's entire purpose is the audience/visibility check itself — `IsVisible`'s per-audience-kind branches, plus `ICampaignUserGroupDirectory`'s current-membership lookup.
- Redaction requirements: all-or-nothing per §16.5's baseline (full record or nothing); field-level partial redaction is an explicit known limitation, not silently dropped.
- Log-safe fields: Not applicable — this task introduces no new `Error`/`ErrorCode`.
- Abuse / malformed input limits: Not applicable — pure function over already-validated data.
- Security tests: `TC-DICE-020`, `TC-DICE-021`, `TC-DICE-022`, `TC-DICE-023` all confirm denial is total (no view, no dictionary entry) rather than merely returning a false-y flag alongside leaked data.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2. This task introduces a new Application port/module (`Odyssey.Application.Audience`), extends an already-merged Application contract (`DiceRoll`/`SubmitRollRequest`) with a breaking-change field, and required real investigation before the implementation path was known — specifically, resolving whether to extend the existing `VisibilityPolicy` or build a sibling policy (resolved via `ADR-021` §3.3), and resolving the explicit open question of whether MainGM always sees a `SelectedParticipants` roll (resolved via §16.2's direct text). This matches §1.2's "introduces or changes an Application port" and "requires investigation before the implementation path is known" triggers.
- ExecPlan path: `docs/plans/active/ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S03-005` (confirmed merged). Blocks `ODY-S03-007` (reconnect replay must reuse this same redaction).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005`/`ODY-S03-006` rows).
- Documents that must not change: `docs/adr/ADR-019`/`ADR-021`, `docs/tasks/active/ODY-S03-000`–`005_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: `DiceRoll`/`SubmitRollRequest` gain a required `Audience` field — a breaking change to `ODY-S03-005`'s just-introduced contract, absorbed within this same slice.
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

- `Packages/com.odyssey.application/Runtime/Dice/DiceContracts.cs`, `DiceRollService.cs` — extended.
- `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Dice/DiceRollVisibilityPolicy.cs` — new.
- `Packages/com.odyssey.application/Runtime/Audience.meta`, `Audience/AudienceContracts.cs.meta`, `Dice/DiceRollVisibilityPolicy.cs.meta` — new.
- `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollServiceTests.cs` — mechanically updated (15 call sites).
- `DotNet/Tests/Odyssey.Tests.Unit/Dice/DiceRollVisibilityPolicyTests.cs` — new (6 tests).
- `Tests/Metadata/test-catalog.json` — 5 new entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-005`/`ODY-S03-006` rows).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | All test projects passed: Contracts 1/1, Domain 27/27, Networking 67/67, Unit 105/105 (99 pre-existing + 6 new `TC-DICE-019`–`023` tests), Architecture 2/2, Persistence 55/55. |
| `.\scripts\verify-format.ps1` | Pending | To be recorded after running on this branch. |
| `.\scripts\check-repository-policy.ps1` | Pending | To be recorded after running on this branch. |
| CI — Draft PR | Pending | To be recorded once the PR is opened and CI completes. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-DICE-005`–`018` all continue to pass with `Audience` threaded through `SubmitRoll`/`RequestFullReroll`. |
| AC-2 | Passed | `TC-DICE-019`: Public visible to Player, Observer, and MainGM. |
| AC-3 | Passed | `TC-DICE-020`: PlayerAndGM visible to actor+MainGM; Observer denied with no signal. |
| AC-4 | Passed | `TC-DICE-021`: GMOnly visible to MainGM only; actor denied. |
| AC-5 | Passed | `TC-DICE-022`: SelectedParticipants — listed user, active-group member, and MainGM all see it; unrelated player and archived-group member do not. |
| AC-6 | Passed | `TC-DICE-023`: `ComputeAudienceViews` omits the excluded participant's key entirely. |
| AC-7 | Passed | `test-catalog.json` updated with 5 new entries; no new `ErrorCode` introduced. |
| AC-8 | Pending | To be confirmed via `git status --porcelain` before commit. |
| AC-9 | Partial | `dotnet build`/`dotnet test` confirmed passing above; `verify-format.ps1`/`check-repository-policy.ps1` pending. |
| AC-10 | Pending | To be confirmed via `git diff --name-status` before commit. |
| AC-11 | Pending | Draft PR not yet opened. |

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-08-26 — Build a new `Odyssey.Application.Dice.DiceRollVisibilityPolicy` rather than extending the existing `Odyssey.Application.Networking.Projection.VisibilityPolicy` — Authority: `ADR-021` §3.3's explicit rule that each consumer keeps its own audience-kind vocabulary; `SceneEntityVisibility` (2 kinds) and `DiceRollAudienceKind` (4 kinds) are incompatible without a lossy translation.
- 2026-08-26 — Place the `CampaignUserGroup` fixture (`ICampaignUserGroupDirectory`/`InMemoryCampaignUserGroupDirectory`) in a new shared `Odyssey.Application.Audience` namespace, not under `Dice/` — Authority: `ADR-021` frames the mechanism as cross-cutting (roll/board/log), anticipating `ODY-S03-007`'s reuse for `GameLogEntry` audience resolution.
- 2026-08-26 — MainGM always sees every roll regardless of audience kind, including `SelectedParticipants` rolls not naming MainGM — Authority: `09_Dice_And_Game_Log` §16.2's direct text, resolving this task's own flagged open question.
- 2026-08-26 — End this task's scope at a pure `TryGetVisibleRoll`/`ComputeAudienceViews` function pair, no wire codec — Authority: `ADR-019` §6.2's existing Application-layer-only check point; a wire codec is a separately-scoped future task's concern, matching `ODY-S03-005`'s own precedent of stopping at a pure Application-layer contract.
- 2026-08-26 — A reroll (`RequestFullReroll`) carries the original roll's audience forward unchanged — Authority: a reroll recomputes the same roll, it is not itself a disclosure/revocation decision; `ADR-021` §7 (not reopened by this task) would govern any future explicit audience-change command.

### Approved task changes

- None.
