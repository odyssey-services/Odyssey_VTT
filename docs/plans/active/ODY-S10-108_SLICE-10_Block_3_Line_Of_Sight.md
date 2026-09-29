# ExecPlan — ODY-S10-108 SLICE-10 Block 3: Line-of-Sight Service

## 1. Purpose
Add pure-domain line-of-sight geometry (segment intersection on the ADR-020 orientation primitive, a directional FOV cone, range) and the Application/Persistence layers for per-token facing/vision parameters and a `ComputeLineOfSight` read query, continuing directly from Block 2's obstacle data (`ODY-S10-107`, unmodified here).

## 2. Scope
Two new Domain files (`SegmentIntersection.cs`, `LineOfSight.cs`), two new Application files (`TokenVisionRepositoryContracts.cs`, `TokenVisionService.cs`) plus six `ErrorCodes.cs` entries, one new Persistence file (`SqliteTokenVisionRepository.cs`) plus one sanctioned point-edit to `SqliteSceneRepository.CreateToken`, three new test files, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Persistent per-player fog of war (Block 4); the vision-cone/fog UI (Block 6); combat cover (Block 5); `SpatialIndexV1`/performance work beyond the ADR-sanctioned naive scan; the closed-door-visible-through-the-wall-behind-it question (still deferred); polygonal obstacles; any change to `BoardGeometry.cs`/`ObstacleGeometry.cs`/existing Obstacle files/`BoardMovementService.cs`/`ObstacleAuthoringService.cs`/campaign membership files; any Unity client file.

## 4. Architecture
`SegmentIntersection`/`LineOfSight` are pure `Odyssey.Domain.Geometry` (no dependency on any other module, ADR-001) — the exact `BoardGeometry`/`ObstacleGeometry` shape, reusing `BoardGeometry.GeometryEpsilonV1`/`EuclideanDistance`/`IsFinite`. `TokenVisionService` is a static Application-layer class taking repositories as method parameters (`BoardMovementService`/`ObstacleAuthoringService` shape): `SetTokenFacing` checks the token's own controller first, falling back to `CampaignMembershipAuthorization.IsMainGm` (owner-or-MainGM, `BoardMovementService.CheckAuthorization`'s exact shape); `SetTokenVisionParameters` checks `IsMainGm` only; `ComputeLineOfSight` performs no check, reading `ISceneRepository`/`ITokenVisionRepository`/`IObstacleRepository` and mapping `ObstacleRecord` to the Domain-layer `ObstacleSegment` value before calling `LineOfSight.CanSee`. `SqliteTokenVisionRepository` is a standalone file/table routed through `SqliteSavingPipeline`, revision-gated like `SqliteObstacleRepository.ToggleDoorState`; `SqliteSceneRepository.CreateToken` seeds a default row in the same transaction via an internal, same-assembly call to `SqliteTokenVisionRepository.EnsureTokenVisionSettingsTable` (Path A — see contract §18 for the Path A vs. Path B reasoning).

## 5. Milestones
1. Recon confirmation of `ADR-020` section 6/7, `SqliteAttackStateReader`/`CoreAttackRulesEvaluator`'s two-layer shape, and that `TokenRecord` has no existing facing/FOV/range field (done in the task's own §0). 2. Domain layer (`SegmentIntersection.cs`, `LineOfSight.cs`) plus `SegmentIntersectionTests.cs`. 3. `LineOfSightTests.cs`. 4. Application layer (`TokenVisionRepositoryContracts.cs`, `TokenVisionService.cs`, `ErrorCodes.cs` entries). 5. Persistence layer (`SqliteTokenVisionRepository.cs`) and the `CreateToken` point-edit. 6. `TokenVisionServiceTests.cs` (authorization + full integration) and a mutation check. 7. Catalogue, `ERROR_CODES.md`, backlog, contract, plan. 8. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 214/214, Persistence 909/909); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the `SetTokenVisionParameters` MainGM-gate test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list (including confirming the `CreateToken` point-edit is the only touch to any Block-2/Scene file).

## 10. Recovery and rollback
Revert the PR; no schema or data change to any existing table other than the additive `TokenVisionSettings` INSERT folded into `CreateToken`'s existing transaction (a new table, not a new column — this project's standing convention).
