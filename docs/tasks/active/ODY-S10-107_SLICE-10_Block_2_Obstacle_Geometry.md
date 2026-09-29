# ODY-S10-107 — SLICE-10 Block 2: Obstacle Geometry (Walls/Doors/Windows), Data and Commands

## 1. Task identity
`ODY-S10-107`; status: In Review (Draft PR, merge deferred to the product owner). Block 2 of the `SLICE-10` track (`claude/SLICE-10-decomposition.md`) — the first task in this repository written from a clean slate with the `ODY-S10-101` stored-membership authorization pattern already in place from day one; no client-supplied flag ever existed here to migrate away from.

## 2. Goal
Add a new obstacle (wall/door/window) data type scoped to a Scene, plus three commands: `CreateObstacle` (MainGM-only), `ToggleDoorState` (any registered campaign participant), `ListObstacles` (unauthenticated read). Line-of-sight/intersection computation (Block 3), drawing UI (Block 6), fog of war (Block 4) and cover (Block 5) are explicitly out of scope.

## 3. Authority
This task's governing ТЗ; `ADR-020` (Board Geometry and Movement Determinism) for the `double`-only/`GeometryEpsilonV1` mandate and the per-segment (not polygon) wall representation; `BoardMovementService.cs` (`ODY-S03-004`) for the Application-layer authorization shape (repository parameter, not constructor injection); `ContentCatalogAuthoringContracts.cs` (`ODY-S05-102`/`ODY-S10-104`) for the cleaner MainGM-only-with-no-owner-fallback shape and for the "reads stay open" precedent.

## 4. In scope
- **`ObstacleGeometry.cs`** (new, `Odyssey.Domain.Geometry`): `ObstacleKind` (`Wall`/`Door`/`Window`) and two pure functions, `BlocksVision`/`BlocksMovement`, reusing `BoardGeometry.GeometryEpsilonV1` (not duplicated) — no dependency on `Odyssey.Application`/`Odyssey.Persistence` (ADR-001).
- **`ObstacleRepositoryContracts.cs`** (new): `ObstacleRecord`, `IObstacleRepository` (`CreateObstacle`/`ToggleDoorState`/`ListObstacles`), `ObstacleFailures`.
- **`ObstacleAuthoringService.cs`** (new, `Odyssey.Application.Board`): `CreateObstacle` — MainGM-only, `CampaignMembershipAuthorization.IsMainGm`, fail closed, checked before the repository is ever called (`ContentCatalogAuthoringService.CreateDraftDefinition`'s exact shape: a denied request consumes no `CommandId`). `ToggleDoorState` — any registered campaign participant in any role (`ICampaignRepository.GetMemberRole(...).IsSuccess` and `.Value.IsMember`), not MainGM-only, per the product document's own "toggled by players/GM." `ListObstacles` — no authorization at all, by exact `ODY-S10-104` precedent for catalog reads.
- **`SqliteObstacleRepository.cs`** (new, separate file/table by `SqliteInventoryRepository`/`SqliteActiveEffectRepository` precedent): a new `Obstacle` table (`CREATE TABLE IF NOT EXISTS`, unversioned schema, same as every other `Ensure*Tables`), `CreateObstacle`/`ToggleDoorState` routed through `SqliteSavingPipeline` exactly as `SqliteSceneRepository.CreateToken`/`MoveToken` are; `ToggleDoorState` re-confirms `Kind == Door` itself (fails closed with a typed rejection if not) rather than trusting the Application-layer caller, and is revision-gated (`ObstacleRevisionConflict`) by exact precedent of `TokenRevisionConflict`.
- Two shared-infrastructure additions the allowed-path list did not separately name but implementation genuinely requires (see §18): a new `ObstacleId` identity type in `DomainIdentity.cs` (every other `*Id` type in this codebase already lives in that one file — `TokenId`/`SceneId`/`CharacterId`/etc. — there is no per-feature identity file to add one to instead), and six new `ErrorCodes.cs` entries (every error this task raises needs one; that shared, ever-growing static class already holds an entry for every past feature's own errors).
- Tests, catalogue `TC-PERSIST-065`–`079`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
- Line-of-sight/segment-intersection computation (Block 3); the drawing UI (Block 6, no Unity client file); fog of war (Block 4); cover (Block 5); polygons (only single-segment obstacles, per ADR-020's own "walls are polylines split into per-segment objects"); the full `Locked`/`Hidden`/`Destroyed`/`Broken`/`Boarded` flag set (only `Kind` + `IsOpen` for a Door); `DeleteObstacle`/geometry edits of an existing obstacle; whether a closed door is itself visible through the wall behind it (an open product question per `SLICE-10-decomposition.md`, deferred to Block 3).
- Any existing `Scene`/`Token` file or table (`SqliteSceneRepository.cs`, `SceneRepositoryContracts.cs`) — a new table, not a change to an existing one (a new column on an existing, unversioned-schema table would not be safe the same way a new table is).
- `BoardGeometry.cs`/`BoardMovementService.cs`/`BoardContracts.cs` — token movement is untouched.
- Any Unity client file — this task is server-side data/commands only.

## 6-8. Domain / Application / Persistence
Domain: `ObstacleGeometry.cs` (new), one new `ObstacleId` type in `DomainIdentity.cs`. Application: `ObstacleRepositoryContracts.cs` (new), `ObstacleAuthoringService.cs` (new), six new `ErrorCodes.cs` entries. Persistence: `SqliteObstacleRepository.cs` (new).

## 9. Tests and validation
`TC-PERSIST-065`–`072` (`SqliteObstacleRepositoryTests.cs`, repository layer, no authorization exercised there by design): create round-trip for every `Kind` (Door defaults closed, Wall/Window have `IsOpen=null`); `CommandId` idempotent replay; open/close with incrementing `Revision`; a stale `expectedRevision` is rejected without mutating the row; `ToggleDoorState` on a Wall/Window is a typed rejection, not an exception; an unknown `ObstacleId` is `NotFound`; `ListObstacles` round-trips several kinds scoped to one Scene (another Scene's obstacles are not returned); a regression proving the existing `SqliteSceneRepository` Scene/Token operations are unaffected by the new table. `TC-PERSIST-073`–`078` (`ObstacleAuthoringServiceTests.cs`, the real authorization): `CreateObstacle` denies an unregistered user and a registered Player for every `Kind`, with no repository state change, and lets the host and a second, separately-registered MainGm through; fail-closed on an unreadable membership; `ToggleDoorState` succeeds for a registered Player, Observer and the MainGm alike (not MainGM-only) and denies an unregistered actor, fail-closed; fail-closed on an unreadable membership for `ToggleDoorState` too, with the door provably unchanged; `ToggleDoorState` on a Wall/Window is still rejected even for a genuinely authorized MainGm (authorization success does not bypass the Kind check); `ListObstacles` performs no authorization at all. `TC-PERSIST-079` (`ObstacleGeometryTests.cs`, `Odyssey.Tests.Unit`): the full `Kind` x `IsOpen` truth table, including a defensive "null `IsOpen` on a Door fails closed" case no production path currently reaches. A deliberate mutation (`CreateObstacle`'s MainGM denial short-circuited) made the representative authorization test fail and was reverted.

`Odyssey.Tests.Unit.dll`: 192/192 (was 187, +5). `Odyssey.Tests.Persistence.dll`: 901/901 (was 887, +14).

## 10-17. (see plan)

## 18. Change control

### Decisions made during execution
- **`ObstacleId` was added to the shared `DomainIdentity.cs`, six error codes to the shared `ErrorCodes.cs`, and six matching rows to `docs/errors/ERROR_CODES.md`**, none named in the ТЗ's allowed-path list. All three are unavoidable, mechanical shared infrastructure: every `*Id` type in this codebase (`TokenId`, `SceneId`, `CharacterId`, `UserId`, ...) lives in that one file; every error any feature raises registers an entry in that one growing `ErrorCodes` class; and `verify-repository.ps1` (TC-CI's own registry check, not editable by this task) independently fails the build if a production `ErrorCode` literal has no matching, test-case-referenced row in `docs/errors/ERROR_CODES.md` -- discovered only by actually running that script, not by reading the ТЗ's own allowed-path list in advance. No logic beyond a new struct/constant/documentation row was added to any of the three files. Disclosed here rather than silently expanded.
- **`ToggleDoorState`'s "not a Door" check lives in the repository, not the Application service.** The service has no reason to pre-read an obstacle before authorizing (unlike `BoardMovementService`, which must read the token to check `ControllerUserId`); the repository already reads the row to perform its own revision-gated update, so the Kind check is a natural, single-read addition there, mirroring exactly how `SqliteSceneRepository.MoveToken` already folds its own domain checks into the same read-then-write transaction.
- **Test files placed exactly where the ТЗ's allowed-path list already named them** (`SqliteObstacleRepositoryTests.cs` at the top level, `Board/ObstacleAuthoringServiceTests.cs`, `Geometry/ObstacleGeometryTests.cs` under `Odyssey.Tests.Unit`) — no ambiguity needed resolving.
- **Test IDs continue `TC-PERSIST-*`** for all three new files, including the pure-function `Odyssey.Tests.Unit` geometry tests, per the ТЗ's own explicit instruction to continue that one prefix rather than invent a new one.

### Findings (reported, not fixed silently)
- No file of `SqliteSceneRepository.cs`/`SceneRepositoryContracts.cs`/`BoardGeometry.cs`/`BoardMovementService.cs`/`BoardContracts.cs` is in this diff.
- No Unity client file is in this diff — this task is server-side only, as specified.
- `CampaignMembershipAuthorization.cs`/`CampaignRepositoryContracts.cs`/`SqliteCampaignRepository.cs` are not touched; `ObstacleFailures`/`ObstacleRepositoryContracts.cs` is this task's own, separate error-factory class (the `PersistenceFailures` class that centralizes most other subsystems' errors lives inside the forbidden `CampaignRepositoryContracts.cs`, so a new, subsystem-owned class was used instead — the same shape `BoardFailures`/`InventoryMovementFailures` already established for their own subsystems).

### Blockers
None.
