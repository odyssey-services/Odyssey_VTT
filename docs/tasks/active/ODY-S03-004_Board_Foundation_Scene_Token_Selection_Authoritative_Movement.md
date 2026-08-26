# ODY-S03-004 — Board Foundation: Scene, Token Selection & Authoritative Movement

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (vertical slice implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-004-board-foundation-scene-token-selection-movement`
**Pull request:** Draft — [#58](https://github.com/odyssey-services/Odyssey_VTT/pull/58) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Implement a persisted-campaign, host-authoritative Board foundation covering roadmap §12.6 step 1 ("Player selects own token") and exit criteria 2/7: token creation with control ownership, `MoveToken` validated host-side using `ADR-020`'s geometry, and Undo as a fresh compensating command that re-validates permission and revision — not a blind rollback.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 fixes this as the first child task, with no dependency, building the foundation `ODY-S03-005`–`007` sit on. The existing `SLICE-01` `ISceneRepository`/`SqliteSceneRepository` is explicitly documented as a minimal, bare-position port with no control-ownership concept and — a genuine gap, not by design — no `ExpectedRevision` check on `MoveToken` at all.
- Value or risk reduction: without a fixed control-ownership + revision-checked movement contract, exit criteria 2 ("a Player cannot move another entity's token without control") and 7 ("Undo/Redo does not bypass permissions and host validation") have no code to satisfy them, and the missing revision check is a real correctness gap for any future concurrent-write scenario.
- Blocking or enabling relationship: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §6 — no dependency (independent of `ODY-S03-005`, may run in parallel). Blocks `ODY-S03-006` (audience-aware delivery needs roll/log artifacts, not board state, so no direct dependency there) and `ODY-S03-007` (persistence/reconnect needs board state to persist, direct dependency).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` (full document — §11, §6.1, §12.4, §21.5, §21.6/BT-079)
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (`board.token.move v1` example — command model not reopened)
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (full document — distance formulas, `GeometryEpsilonV1`, not reopened)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`Result<T>`/`SafeReasonCode`, reused not extended)
- `Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionContracts.cs` (`ODY-S02-010`) — read to fix the relationship between this task's domain model and the existing network projection (§3 below)
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`/`SqliteSavingPipeline.cs` (`SLICE-01`) — extended by this task, decision justified in §3 below
- `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md`, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1/§5 (this task's fixed boundary, already set by the prior task — executed, not reopened)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (vertical slice implementation), backlog `ODY-S03-004`, roadmap §12.6 step 1, §12.7 exit criteria 2/7.
- Existing test IDs: None reused (new `TC-BOARD-*` series, first use).
- New test IDs introduced: `TC-BOARD-001` through `TC-BOARD-013`.

### Task-safe private context

- Approved summary / references: `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and the production code's doc comments. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `SLICE-03_IMPLEMENTATION_BACKLOG.md` is merged to `main`, listing `ODY-S03-004` as the first ordered child task with no dependency — confirmed by `git log --oneline -10` and `Read` before branching.
- `ISceneRepository`/`SqliteSceneRepository`'s own XML doc comment states: "ODY-S01-008 minimal Scene/Token/Asset persistence port... not the full Scene/Board/Layer/SceneObject/Component domain model... only identity, name, status/revision, and bare token position" — confirmed by `Read`.
- `SqliteSceneRepository.MoveToken` (prior to this task) took no `ExpectedRevision` parameter — it re-read the current revision from the database only to compute `newRevision = previousRevision + 1`, never comparing it against a caller-supplied expectation — confirmed by `Read`; a genuine optimistic-concurrency gap (`ADR-002` §10.2 requires this check), not a documented, deliberate simplification.
- `ODY-S02-011`'s `MoveTokenService` (`TokenMoveContracts.cs`) operates on `SceneMutableState`, an in-memory, non-durable store — its own doc comment justifies this explicitly by the absence of a persisted campaign in that network-only prototype ("without creating any dependency on CampaignHandle/SQLite... see this task's own decision log for why campaign persistence is not wired in at this prototype stage") — confirmed by `Read`; this task's premise is the opposite (durable campaign persistence), so that same non-reuse reasoning does not transfer here — `SqliteSceneRepository` is extended in place instead (§3 below).
- `ODY-S02-010`'s `Odyssey.Application.Networking.Projection.Scene`/`SceneEntity` is a redaction-aware wire-projection type (identity/visibility, immutable per snapshot build) — confirmed by `Read`; structurally and purposefully distinct from the persisted, mutable `Scene`/`Token` aggregate this task builds.
- 4 existing test files call `ISceneRepository.CreateToken`/`MoveToken` at 12 call sites total — confirmed by `Grep`; all updated for this task's breaking signature change (§17).
- No `TokenId`/`SceneId` footprint or grid-cell model exists yet — `TokenId`'s own doc comment already documents "Full SceneObject/TokenComponent fields (footprint, facing, layer, components...) are not implemented... only identity and a bare position" as a deliberate `SLICE-01` scope boundary, not reopened here.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` (new) — `ADR-020`'s `GridType=None` geometry primitives (`GeometryEpsilonV1`, `EuclideanDistance`, `IsFinite`, `AlmostEqual`, `SamePosition`).
- `Packages/com.odyssey.application/Runtime/Board/BoardContracts.cs`, `BoardMovementService.cs` (new) — `MoveTokenRequest`, `BoardFailures`, host-authoritative `MoveToken`/`UndoMoveToken`.
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs` — `ISceneRepository`/`TokenRecord` extended: `ControllerUserId` on `CreateToken`/`TokenRecord`, `ExpectedRevision` on `MoveToken`, new `GetToken` read method.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — new `PersistenceFailures.TokenRevisionConflict`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — 4 new codes (`persistence.token.revision_conflict`, `board.token.move_denied`, `board.token.destination_invalid`, `board.token.destination_occupied`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` — `CreateToken`/`MoveToken` signature/implementation changes, new `GetToken`, `ControllerUserId` column, shared `ReadTokenRecord` helper.
- 4 existing test files updated for the breaking signature change (call-site fixes only, no new assertions added to them).
- New tests: `DotNet/Tests/Odyssey.Tests.Domain/Geometry/BoardGeometryTests.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/Board/BoardMovementServiceTests.cs`.
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` registry updates for the 4 new error codes / 13 new test cases.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-004` row status).

### Out of scope

- Drawing, ruler, pointer, object-lock-as-a-feature, layer management UI, grid-type-switching UI (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1).
- Dice rolls, audience-aware delivery, reconnect/persistence-replay beyond restart groundwork (`ODY-S03-005`–`007`).
- Any `Odyssey.Networking` code or change to `ODY-S02-010`'s `Scene`/`SceneEntity` projection type — this task's domain model is the future *source* for that projection, per §3's decision, not a replacement built by this task.
- Square/Hex distance metrics, grid-coordinate snapping, `SpatialIndexV1`'s bucket structure beyond exact-position occupancy — no grid/hex UI exists in this revision to exercise them (ExecPlan Investigation point 6).
- `CampaignUserGroup`, session, or role infrastructure — `ActorIsMainGm` is caller-supplied, not resolved from a real session model.
- Any edit to `ADR-002`/`ADR-004`/`ADR-020`.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Geometry/**
Packages/com.odyssey.application/Runtime/Board/**
Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
DotNet/Tests/Odyssey.Tests.Domain/Geometry/**
DotNet/Tests/Odyssey.Tests.Persistence/Board/**
DotNet/Tests/Odyssey.Tests.Persistence/SqliteSceneRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteSavingPipelineTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteExportRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/VerticalSliceIntegrationTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md
docs/plans/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md
docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `BoardGeometry` lives in `Odyssey.Domain` (no dependency, `ADR-001` §5) since it is pure math free of `UnityEngine` (`ADR-020` §9). `BoardMovementService`/`BoardContracts` live in `Odyssey.Application` (depends on Domain + the `ISceneRepository` port it already owns) — no new module, no boundary violation.
- Authoritative-state and transaction boundary: `SqliteSceneRepository.MoveToken`'s own transaction is the final, atomic optimistic-concurrency guard (`ADR-002` §10.2); `BoardMovementService`'s own submission/pre-commit checks run outside that transaction and cannot themselves close a race — documented explicitly in code comments, not silently assumed safe.
- Serialization / compatibility boundary: `Token` SQLite table gains a new `NOT NULL` `ControllerUserId` column via the existing `CREATE TABLE IF NOT EXISTS` statement (no formal `ADR-013` migration exists for this ad hoc table today, consistent with current practice) — a fresh table for every new campaign; no existing production campaign data is migrated (none exists pre-release).
- Time / RNG rule: not applicable — no clock/RNG logic introduced.
- Unity / thread / lifetime rule: not applicable — no Unity-side code.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `BoardFailures.MoveDenied` does not distinguish "not found" from "not yours" beyond what `08_Scenes_And_Board` §24.1 already allows (this task has no hidden-token/fog model yet, so no additional leak surface exists to guard against here — noted, not silently ignored).
- Performance or platform constraint: occupancy check is O(tokens in scene) per move — acceptable at the documented MVP scale (`08_Scenes_And_Board` §25, ≤200 tokens); `SpatialIndexV1`'s full bucket structure is deferred per §5's out-of-scope note.
- Other: None.

## 7. Expected behavior

### Scenario 1 — controller moves their own token

**Given** a token created with `ControllerUserId = U1`
**When** `U1` submits `MoveToken` to a finite, unoccupied destination with the correct `ExpectedRevision`
**Then** the move succeeds, the token's position updates, and its revision advances by exactly one.

### Scenario 2 — non-controller, non-MainGM is rejected, no mutation

**Given** the same token
**When** a different actor `U2` (not `ActorIsMainGm`) submits `MoveToken`
**Then** the result is a typed `board.token.move_denied` failure and the token's position/revision are unchanged (BOARD-INV-027, exit criterion 2).

### Scenario 3 — stale revision is rejected atomically

**Given** a token already moved once (revision 2)
**When** a caller submits `MoveToken` with `ExpectedRevision = 1`
**Then** the result is a typed `persistence.token.revision_conflict` failure, checked inside `SqliteSceneRepository.MoveToken`'s own transaction, and no mutation occurs.

### Scenario 4 — Undo re-validates permission and revision, not a blind rollback

**Given** a token moved by its controller (revision 1 → 2)
**When** the controller submits `UndoMoveToken` restoring the original position
**Then** the result is a *new* compensating command (revision 3, not a rollback to revision 1); an `UndoMoveToken` by a non-controller, or against a now-stale revision, is rejected exactly as an ordinary move would be (exit criterion 7).

### Required invariants

- `MoveToken`/`UndoMoveToken` never mutate token state on a rejected request (verified by every negative test re-reading the token via `GetToken` afterward).
- `ADR-002`, `ADR-004`, `ADR-020` files are unmodified.
- No new `Odyssey.Networking` reference is introduced anywhere in this task's diff.

## 8. Deliverables

- Production code: `BoardGeometry.cs`, `BoardContracts.cs`, `BoardMovementService.cs`, extended `SceneRepositoryContracts.cs`/`CampaignRepositoryContracts.cs`/`ErrorCodes.cs`/`SqliteSceneRepository.cs`.
- Tests: `BoardGeometryTests.cs` (5 tests, `TC-BOARD-001`–`003`), `BoardMovementServiceTests.cs` (10 tests, `TC-BOARD-004`–`013`); 4 existing test files updated for the breaking signature change.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `ERROR_CODES.md`, `test-catalog.json`, `SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-004` row).
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable (fresh-table column addition, no existing data).

## 9. Acceptance criteria

1. `BoardGeometry.EuclideanDistance`/`IsFinite`/`AlmostEqual`/`SamePosition` are implemented exactly per `ADR-020` §4/§5.1/§5.3/§6, with golden-vector tests (`TC-BOARD-001`–`003`).
2. `ISceneRepository.CreateToken` requires a `ControllerUserId`; `TokenRecord` exposes it.
3. `ISceneRepository.MoveToken` requires an `ExpectedRevision`, validated atomically inside `SqliteSceneRepository.MoveToken`'s own transaction, returning a typed `persistence.token.revision_conflict` on mismatch.
4. `BoardMovementService.MoveToken` rejects a non-controller, non-MainGM actor with a typed `board.token.move_denied` failure, with no state mutation (`TC-BOARD-005`).
5. `BoardMovementService.MoveToken` rejects a non-finite destination before any repository call (`TC-BOARD-009`) and a destination occupied by another token (`TC-BOARD-008`, BOARD-INV-009).
6. `BoardMovementService.UndoMoveToken` is implemented as a call into the same `MoveToken` pipeline (not a distinct mechanism), re-validating permission and revision at undo time (`TC-BOARD-010`–`012`).
7. Token state survives a campaign close/reopen cycle unchanged (`TC-BOARD-013`).
8. All 4 pre-existing test files compile and pass against the new signatures; no test assertion in them is weakened to accommodate the change.
9. `docs/errors/ERROR_CODES.md` and `Tests/Metadata/test-catalog.json` are updated for all 4 new error codes and all 13 new test cases.
10. `ADR-002`, `ADR-004`, and `ADR-020` files are unmodified by this task's diff.
11. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`, `dotnet test` all pass.
12. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
13. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-BOARD-001` | `.NET` / `dotnet test` | `EuclideanDistance` matches hand-computed golden values | Pass |
| `TC-BOARD-002` | `.NET` / `dotnet test` | `IsFinite` rejects NaN/Infinity | Pass |
| `TC-BOARD-003` | `.NET` / `dotnet test` | `AlmostEqual`/`SamePosition` epsilon-boundary behavior | Pass |
| `TC-BOARD-004` | `.NET` / `dotnet test` | Controller moves own token, revision advances | Pass |
| `TC-BOARD-005` | `.NET` / `dotnet test` | Non-controller move rejected, no mutation | Pass |
| `TC-BOARD-006` | `.NET` / `dotnet test` | MainGM moves any token | Pass |
| `TC-BOARD-007` | `.NET` / `dotnet test` | Stale revision rejected atomically | Pass |
| `TC-BOARD-008` | `.NET` / `dotnet test` | Destination-occupied rejected | Pass |
| `TC-BOARD-009` | `.NET` / `dotnet test` | Non-finite destination rejected | Pass |
| `TC-BOARD-010` | `.NET` / `dotnet test` | Undo is a new compensating command | Pass |
| `TC-BOARD-011` | `.NET` / `dotnet test` | Undo re-validates permission | Pass |
| `TC-BOARD-012` | `.NET` / `dotnet test` | Undo re-validates revision, not blind rollback | Pass |
| `TC-BOARD-013` | `.NET` / `dotnet test` | Token state survives close/reopen | Pass |

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

- Read `BoardMovementService.cs` end-to-end to confirm the two-point authorization pattern and the fact that `UndoMoveToken` genuinely delegates to `MoveToken` (not a separate, divergent code path).

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity-side code; `.meta` files added for the new Runtime assets for repository consistency, not exercised by this task's own validation.
- Network topology or database fixture: Real SQLite via `SqliteSceneRepository`, temp-directory campaigns per test (`SetUp`/`TearDown`).
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Unity Editor compile/EditMode/PlayMode — no Unity-side code is introduced; `verify-unity-project.ps1`'s static check covers package/manifest structure only, already exercised by `check-repository-policy.ps1`.
- Any networking test — no `Odyssey.Networking` code is touched.
- Reconnect/persistence-replay beyond simple close/reopen — `ODY-S03-007`'s scope.

## 11. Compatibility, migration, and rollback

- Compatibility impact: `ISceneRepository.CreateToken`/`MoveToken` signatures change (breaking). No external caller exists outside this repository's own test suite (all 12 call sites updated, §17). No production campaign data exists pre-release to migrate.
- Version fields affected: None (application version unchanged; this is pre-release internal API evolution, not a public contract/schema/protocol version).
- Migration or upcaster: Not applicable — `Token` table uses `CREATE TABLE IF NOT EXISTS`; a fresh campaign gets the new `ControllerUserId` column from creation. No `ADR-013` migration step is registered for this ad hoc table, consistent with its current (pre-`ADR-013`-adoption) treatment.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits.
- Data-loss risk and protection: None — no existing data migrated.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: `UserId` (controller reference) stored in the `Token` table — no secret, credential, or personal data beyond the already-established opaque `UserId` identifier type.
- Trust boundaries: `BoardMovementService`'s authorization check is the host-authoritative trust boundary for token movement (`08_Scenes_And_Board` §2.3's "GM Host" authority) — no client-supplied permission decision is trusted.
- Authorization / audience checks: `CheckAuthorization` (controller-or-MainGM) is the only check this task introduces; no hidden-entity/fog model exists yet to require additional redaction.
- Redaction requirements: Not applicable — no network delivery in this task.
- Log-safe fields: `Error` responses use only the existing `SafeReasonCode`/`UserMessageKey` vocabulary; no raw `UserId`/`TokenId` value is embedded in a message string beyond the already-typed `Error` fields.
- Abuse / malformed input limits: Non-finite destinations are rejected before any repository call (`BoardFailures.InvalidDestination`); revision/controller checks bound state mutation to authorized, current-state requests only.
- Security tests: `TC-BOARD-005`, `TC-BOARD-007`, `TC-BOARD-011`, `TC-BOARD-012` — all confirm no state mutation occurs on a rejected request, not merely that an error is returned.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2. This task changes more than one production module (`Odyssey.Domain`, `Odyssey.Application`, `Odyssey.Persistence`), introduces a breaking change to an existing Application port (`ISceneRepository`) affecting 12 existing call sites, and required real investigation before the path was known — reading `SqliteSceneRepository`'s existing (and gap-carrying) implementation, `ODY-S02-011`'s in-memory prototype pattern, and `08_Scenes_And_Board`'s full validation pipeline to determine which of two genuinely different reuse strategies applied here versus there (§3/ExecPlan Investigation points 3–5). This directly matches §1.2's "changes more than one production module," "introduces or changes an Application port," and "requires investigation before the implementation path is known" triggers.
- ExecPlan path: `docs/plans/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: no dependency on `ODY-S03-005` (mutually independent per `SLICE-03_IMPLEMENTATION_BACKLOG.md` §6). Blocks `ODY-S03-007` (persistence/reconnect needs board state to persist).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-004` row).
- Documents that must not change: `docs/adr/ADR-002`/`ADR-004`/`ADR-020`, `docs/tasks/active/ODY-S03-000`–`003_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: `ISceneRepository`'s internal Application-port contract changes (breaking, pre-release, no external consumer) — not a public/persisted-format/protocol version this repository tracks separately.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [x] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
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

- `Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` — new.
- `Packages/com.odyssey.application/Runtime/Board/BoardContracts.cs`, `BoardMovementService.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs`, `CampaignRepositoryContracts.cs`, `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — extended.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` — extended (`ControllerUserId` column, `ExpectedRevision` check, `GetToken`, shared `ReadTokenRecord`).
- `DotNet/Tests/Odyssey.Tests.Persistence/{SqliteSceneRepositoryTests,SqliteSavingPipelineTests,SqliteExportRepositoryTests,VerticalSliceIntegrationTests}.cs` — updated for the breaking signature change (12 call sites).
- `DotNet/Tests/Odyssey.Tests.Domain/Geometry/BoardGeometryTests.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/Board/BoardMovementServiceTests.cs` — new (13 tests total).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — 4 new registry rows, 13 new test-catalog entries.
- This task contract, its ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-004` row).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | All 6 test projects passed: Contracts 1/1, Domain 6/6, Networking 67/67, Unit 84/84, Architecture 2/2, Persistence 55/55 (includes the 13 new `TC-BOARD-*` tests). |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005` (error registry). |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-11 | Passed | See Validation results table; production code and tests implement exactly the behavior described in AC-1–AC-8; `ADR-002`/`004`/`020` unmodified confirmed via `git status --porcelain`. |
| AC-12 | Passed | `git status --porcelain` matches §5's Allowed paths exactly. |
| AC-13 | Pending | PR to be opened as Draft; CI status to be confirmed. |

### Known limitations

- `Odyssey.Domain.Geometry.BoardGeometry` implements only the `GridType=None` case of `ADR-020` — Square/Hex distance metrics and grid-coordinate snapping are not implemented, since no task in this revision needs them (documented explicitly, not silently incomplete).
- BOARD-INV-009 (token overlap) is interpreted as exact epsilon-equal position, not footprint-based, since no footprint/grid-cell model exists yet.
- `ActorIsMainGm` is a caller-supplied boolean, not resolved from a real session/role model — this task has no session infrastructure of its own.
- No control-transfer command exists; `UndoMoveToken`'s "actor lost control" scenario (`TC-BOARD-011`) is tested via a non-controller actor from the start, not a literal mid-flow control transfer, since no such command exists to construct that scenario.

### Follow-up tasks

- `ODY-S03-005` (independent), `ODY-S03-006`/`007` (depend on this task's board state) — per `SLICE-03_IMPLEMENTATION_BACKLOG.md` §4/§6.
- A future task (not numbered in this revision) to teach the network layer to project this task's durable `Scene`/`Token` model into `ODY-S02-010`'s wire `Scene`/`SceneEntity` — explicitly noted, not started here.

### Self-review summary

- Scope review: stays within `ODY-S03-004`'s fixed boundary; no dice/audience/reconnect/networking content introduced.
- Architecture review: `Odyssey.Domain`→no dependency, `Odyssey.Application`→Domain only — both respect `ADR-001` §5's matrix; no new module.
- Test review: 13 new `TC-BOARD-*` IDs registered in `test-catalog.json`; every negative test confirms no state mutation, not just an error return.
- Security/privacy review: no secret/credential data introduced; authorization check is host-side only.
- Documentation/version review: no ADR, schema, protocol, or application version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-08-26 — Extend `SqliteSceneRepository`/`ISceneRepository` in place (breaking change) rather than building a parallel store, because this task's premise (durable campaign persistence) is the opposite of what justified `ODY-S02-011`'s fresh in-memory `MoveTokenService` — Authority: `ODY-S02-011`'s own doc comment; `ODY-S01-009`'s precedent of the same kind of breaking evolution.
- 2026-08-26 — Fix the missing `ExpectedRevision` check on `MoveToken` as part of this task, since it is a genuine correctness gap directly relevant to this task's own authoritative-movement goal, not a separate, unrelated cleanup — Authority: `ADR-002` §10.2.
- 2026-08-26 — Scope `BoardGeometry` to `ADR-020`'s `GridType=None` case only — Authority: `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1's own narrowing discipline, applied consistently here since no grid/hex task exists in this revision.
- 2026-08-26 — Interpret BOARD-INV-009 as exact epsilon-equal position, not footprint overlap — Authority: no footprint model exists yet (`TokenId`'s own doc comment, `SLICE-01` scope boundary).

### Approved task changes

- None.
