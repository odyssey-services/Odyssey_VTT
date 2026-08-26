# ExecPlan — ODY-S03-004: Board Foundation: Scene, Token Selection & Authoritative Movement

**Governing task contract:** `docs/tasks/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` — full document, especially §11 (token/control ownership), §6.1 (`WorldPosition`), §12.4 (movement validation pipeline), §21.5 (Undo/Redo), §21.6/BT-079 (restart determinism).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (`board.token.move v1` example).
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` — full document.
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`.
- `Packages/com.odyssey.application/Runtime/Networking/Command/TokenMoveContracts.cs` (`ODY-S02-011`) — read as the closest prior art for the two-point authorization pattern, adapted here to a durable repository instead of in-memory session state.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` (`SLICE-01`, `ODY-S01-008`/`009`) — the existing minimal persistence port, extended (not replaced) by this task.
- `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S03-004` task boundary, fixed by the prior task — executed, not reopened).

## Investigation performed

1. Read `08_Scenes_And_Board` §11 (token/control), §12.4 (13-point validation pipeline, narrowed to points 2/6/7/12 — membership/lock/traversal are SLICE-02/future concerns), §21.5 (Undo/Redo), §21.6/BT-079.
2. Read `SceneRepositoryContracts.cs`/`SqliteSceneRepository.cs` in full: confirmed the existing `ISceneRepository` is explicitly documented as "ODY-S01-008 minimal... not the full Scene/Board/Layer/SceneObject/Component domain model" and that `MoveToken` had **no** `ExpectedRevision` parameter at all — a genuine, real optimistic-concurrency gap, not a stylistic choice, fixed by this task (task contract §3).
3. Read `TokenMoveContracts.cs` (`ODY-S02-011`'s in-memory `MoveTokenService`) to confirm the two-point authorization pattern (submission + pre-commit) and revision-conflict shape already established for the network prototype — reused as the pattern for this task's own `BoardMovementService`, not copied wholesale (that class operates on non-durable `SceneMutableState`, explicitly justified there by the absence of a persisted campaign — the opposite of this task's premise).
4. Determined the domain-model-vs-network-projection relationship (task contract §3): the persisted `Scene`/`Token` aggregate this task builds is the source of truth; `ODY-S02-010`'s `Odyssey.Application.Networking.Projection.Scene`/`SceneEntity` remains the redaction-aware wire projection; a future task (`ODY-S03-006`/`007`) is expected to teach the network layer to build that projection from this task's durable model (e.g., extending `SceneProjectionBuilder`) — not something this task itself does (no networking touched here).
5. Determined the `SqliteSceneRepository` reuse decision (task contract §3): extend it in place (adding `ControllerUserId` to `CreateToken`/`TokenRecord`, `ExpectedRevision` to `MoveToken`, a new `GetToken` read method) rather than building a parallel store — the opposite reasoning from `ODY-S02-011`'s fresh `MoveTokenService`, because this task's whole premise is durable campaign persistence, which `SqliteSceneRepository` already is. This mirrors `ODY-S01-009`'s own precedent of breaking and evolving the `ODY-S01-008` signature once already.
6. Scoped `Odyssey.Domain.Geometry.BoardGeometry` to `ADR-020`'s `GridType=None` case only (Euclidean distance, finite-check, epsilon-tolerant equality) — Square/Hex metrics and grid-coordinate snapping are not exercised by anything in `SLICE-03_IMPLEMENTATION_BACKLOG.md`'s minimal vertical slice (no grid/hex UI task exists in this revision), so implementing them now would be unused, untested surface area.
7. Scoped BOARD-INV-009 (token overlap) to exact epsilon-equal-position conflict, since no footprint/grid-cell model exists yet (`TokenId`'s own doc comment already documents this as a deferred SLICE-01 scope boundary) — the minimal-viable reading available before a footprint model exists.
8. Confirmed via `grep` every existing call site of `ISceneRepository.CreateToken`/`MoveToken` (4 test files, 12 call sites) and updated all of them for the breaking signature change, following `ODY-S01-009`'s own precedent of a full, non-partial breaking-change update.

## Intended change

- New: `Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` (+ `.meta`).
- New: `Packages/com.odyssey.application/Runtime/Board/BoardContracts.cs`, `BoardMovementService.cs` (+ `.meta`).
- Changed (breaking, in place): `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs` (`ISceneRepository`, `TokenRecord`), `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (`PersistenceFailures.TokenRevisionConflict`), `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (4 new codes), `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`.
- Updated (breaking-change call sites): `DotNet/Tests/Odyssey.Tests.Persistence/{SqliteSceneRepositoryTests,SqliteSavingPipelineTests,SqliteExportRepositoryTests,VerticalSliceIntegrationTests}.cs`.
- New tests: `DotNet/Tests/Odyssey.Tests.Domain/Geometry/BoardGeometryTests.cs` (`TC-BOARD-001`–`003`), `DotNet/Tests/Odyssey.Tests.Persistence/Board/BoardMovementServiceTests.cs` (`TC-BOARD-004`–`013`).
- Registry updates: `docs/errors/ERROR_CODES.md` (4 new rows), `Tests/Metadata/test-catalog.json` (13 new `TC-BOARD-*` entries).
- New: this task's contract, this ExecPlan, `docs/tasks/SLICE-03_IMPLEMENTATION_BACKLOG.md` (`ODY-S03-004` row status).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

## Explicit non-goals

- No drawing, ruler, pointer, object-lock-as-a-feature, layer management UI, or grid-type-switching UI (`SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1, already fixed).
- No dice rolls, audience-aware delivery, or reconnect — `ODY-S03-005`–`007`'s scope.
- No networking code or `Odyssey.Networking` changes — this task stays within `Odyssey.Domain`/`Odyssey.Application`/`Odyssey.Persistence`.
- No Square/Hex distance metrics, grid-coordinate snapping, or `SpatialIndexV1`'s uniform-hash bucket structure beyond the exact-position occupancy check — deferred to a task that actually needs a grid (see Investigation point 6).
- No `CampaignUserGroup`/session/role infrastructure — `ActorIsMainGm` is a caller-supplied boolean, not a resolved role from a real session model (SLICE-02 concern, not reopened here).
