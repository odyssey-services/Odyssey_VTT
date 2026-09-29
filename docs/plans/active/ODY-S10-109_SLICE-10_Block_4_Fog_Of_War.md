# ExecPlan — ODY-S10-109 SLICE-10 Block 4: Fog of War

## 1. Purpose
Add two independent per-player states — live token visibility (a thin composition over Block 3's `TokenVisionService.ComputeLineOfSight`) and persistent, monotonic map memory (an append-only `FogReveal` table) — keyed directly on `UserId`, with a first-in-this-codebase self-scoped-or-MainGM read authorization and an unconditional MainGM bypass on both.

## 2. Scope
Two new Application files (`FogOfWarRepositoryContracts.cs`, `PlayerVisibilityService.cs`), one new Persistence file (`SqliteFogOfWarRepository.cs`), two shared-infrastructure additions (`FogRevealId` in `DomainIdentity.cs`, two codes in `ErrorCodes.cs`), one new test file, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
The vision/fog UI (Block 6); combat cover (Block 5); manual reveal grants; `Group`/`CharacterOwners` audience model; circle union/merge into a combined polygon; performance optimization beyond the naive linear scans this task's own scale assumption sanctions; wiring `RecordExploration` into `MoveToken`/`SetTokenFacing`; any change to Block 1/2/3 files.

## 4. Architecture
`PlayerVisibilityService` is a static Application-layer class taking repositories as method parameters (`TokenVisionService`/`ObstacleAuthoringService` shape). `ComputeVisibleTokens` composes `TokenVisionService.ComputeLineOfSight` per (observer token, candidate token) pair for the target user's own tokens, unioning the results — no new storage. `RecordExploration`/`IsPointExplored` read/write `IFogOfWarRepository`, a standalone file/table routed through `SqliteSavingPipeline` (INSERT-only — no `UPDATE`/`DELETE` ever issued against `FogReveal`, and no `Revision` column, since a row is immutable once written). Both self-scoped reads and the MainGM bypass reuse `CampaignMembershipAuthorization.IsMainGm` directly — no new authorization primitive. The circle-containment coverage test is a private static helper in `PlayerVisibilityService.cs`, reusing `BoardGeometry.EuclideanDistance`/`GeometryEpsilonV1` rather than a new Domain file (not named in the ТЗ's allowed-path list).

## 5. Milestones
1. Recon confirmation of `TokenVisionService.ComputeLineOfSight`'s exact shape, the `VisibilityPolicy.IsVisible` MainGM-shortcut precedent, and that no "roster"/audience concept beyond `ControllerUserId` exists anywhere in the codebase (done in the task's own §0). 2. `FogRevealId` (`DomainIdentity.cs`) and `FogOfWarRepositoryContracts.cs`. 3. `PlayerVisibilityService.cs` and the two `ErrorCodes.cs` entries. 4. `SqliteFogOfWarRepository.cs`. 5. `PlayerVisibilityServiceTests.cs` (`TC-PERSIST-109`–`119`) and a mutation check. 6. Catalogue, `ERROR_CODES.md`, backlog, contract, plan. 7. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 214/214, Persistence 920/920); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the self-scoped-read test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list (confirming no Block 1/2/3 file is touched).

## 10. Recovery and rollback
Revert the PR; no schema or data change to any existing table (only the new, additive `FogReveal` table).
