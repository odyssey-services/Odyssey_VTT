# ExecPlan — ODY-S10-107 SLICE-10 Block 2: Obstacle Geometry

## 1. Purpose
Add a wall/door/window obstacle data type scoped to a Scene, plus `CreateObstacle`/`ToggleDoorState`/`ListObstacles` commands with real, correctly-scoped authorization from day one -- no client-flag transitional period, unlike every prior task in the `ODY-S10-101`–`106` track.

## 2. Scope
Four new production files (`ObstacleGeometry.cs`, `ObstacleRepositoryContracts.cs`, `ObstacleAuthoringService.cs`, `SqliteObstacleRepository.cs`), two shared-infrastructure additions (`ObstacleId` in `DomainIdentity.cs`, six codes in `ErrorCodes.cs`), three new test files, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Line-of-sight/intersection (Block 3); drawing UI (Block 6); fog of war (Block 4); cover (Block 5); polygons; the full flag set; `DeleteObstacle`/geometry edits; any existing Scene/Token/Board file; any Unity client file.

## 4. Architecture
`ObstacleGeometry` is pure `Odyssey.Domain.Geometry` (no dependency on any other module); its `BlocksVision`/`BlocksMovement` compare only `ObstacleKind`/`bool?`, so no epsilon tolerance is needed and `BoardGeometry.GeometryEpsilonV1` is not referenced there — `ObstacleRecord` is the actual reuse point, via `BoardGeometry.IsFinite` on the segment endpoints. `ObstacleAuthoringService` is a static Application-layer class taking `ICampaignRepository` as a method parameter (the `BoardMovementService`/`DiceRollService` shape): `CreateObstacle` decides via `CampaignMembershipAuthorization.IsMainGm`, `ToggleDoorState` via a plain `GetMemberRole(...).IsMember` check (any registered role), `ListObstacles` performs no check at all. `SqliteObstacleRepository` is a standalone file/table, routed through the existing `SqliteSavingPipeline`, revision-gated exactly like `SqliteSceneRepository.MoveToken`; it re-confirms `Kind == Door` itself before a toggle.

## 5. Milestones
1. Recon confirmation of `BoardMovementService`/`ContentCatalogAuthoringService`/`SqliteSceneRepository` shapes (done in the task's own §0). 2. Domain layer (`ObstacleGeometry.cs`, `ObstacleId`). 3. Application layer (`ObstacleRepositoryContracts.cs`, `ObstacleAuthoringService.cs`, `ErrorCodes.cs` entries). 4. Persistence layer (`SqliteObstacleRepository.cs`). 5. Tests (`TC-PERSIST-065`–`079`) and a mutation check. 6. Catalogue, backlog, contract, plan. 7. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 192/192, Persistence 901/901); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the `CreateObstacle` authorization test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR; no schema or data change to any existing table (only a new `Obstacle` table is added).
