# ODY-S10-110 — SLICE-10 Block 5: Cover Suggestion (Graduated, Hint-Only)

## 1. Task identity
`ODY-S10-110`; status: In Review (Draft PR, merge deferred to the product owner). Block 5 of the `SLICE-10` track, continuing directly from `ODY-S10-108`/`ODY-S10-109` (Blocks 3/4, merged — line-of-sight and fog of war, not modified here).

## 2. Goal
A graduated (`None`/`Half`/`ThreeQuarters`/`Full`) cover *hint*, computed from Block 2's obstacle geometry, over Block 3's own reused blocking predicate. Product decisions of 2026-09-29: (1) graduated, not binary, requiring a new 4-sample geometry beyond Block 2/3's point-only model; (2) a hint only — not wired into the combat pipeline at all, which sharply limits this task's risk (it can add a new read-only query but cannot change any combat outcome). No UI (Block 6).

## 3. Authority
This task's governing ТЗ; the product decisions of 2026-09-29 above; `ADR-020` section 12.4 (cover must not invent its own blocking interpretation separate from line-of-sight's own — the same predicate, `ObstacleGeometry.BlocksVision`, must be reused, not re-derived); `LineOfSight.cs`/`SegmentIntersection.cs` (`ODY-S10-108`) as the reused, unmodified building blocks; `CoreAttackRulesEvaluator.cs` (confirmed by recon: `Modifiers` is always `Array.Empty<AttackModifierEntry>()`, `Hit` is literally `Range` — a documented MVP stub, not touched by this task at all, per the product decision that cover is a hint only).

## 4. In scope
- **`Cover.cs`** (new, `Odyssey.Domain.Geometry`): `CoverDegree` enum (`None`/`Half`/`ThreeQuarters`/`Full`), `CoverGeometry.ComputeCoverDegree` (a pure function: one attacker point, four sample points around the target's own position at a fixed, non-configurable `DefaultTargetRadius = 0.5` world units, each attacker-to-sample segment tested against `ObstacleGeometry.BlocksVision`-passing obstacles via `SegmentIntersection.SegmentsIntersect` directly, no re-derived intersection test), `DefaultTargetRadius`.
- **`CoverSuggestionService.cs`** (new, `Odyssey.Application.Board`, static class): `SuggestCover` — reads both tokens via `ISceneRepository`, the scene's obstacles via `IObstacleRepository.ListObstacles`, calls `CoverGeometry.ComputeCoverDegree`; a typed error if attacker and target are not in the same Scene (by exact precedent of `TokenVisionFailures.ObserverAndTargetNotInSameScene`). No authorization at all, by exact precedent of `ObstacleAuthoringService.ListObstacles`/`TokenVisionService.ComputeLineOfSight` — a hint over already-open scene-geometry reads is no more sensitive than either.
- Tests, catalogue `TC-PERSIST-120`–`129`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
- Any automatic combat-pipeline integration — `CoreAttackRulesEvaluator.cs`/`AttackEvaluationService.cs`/`AttackApplyService.cs`/`SqliteAttackStateReader.cs`/`AttackPipelineContracts.cs`/`SqliteAttackApplyRepository.cs` are untouched, by the direct product decision that cover is a suggestion only. Configurable per-token size (a fixed constant on this task's own MVP). The full tabletop "4 attacker corners × 4 target corners" algorithm (this task's own simplified "1 attacker point × 4 target points" adaptation). The vision/fog UI (Block 6). Distinguishing cover degree by obstacle type beyond the existing binary `BlocksVision` predicate (e.g. "a window gives partial cover even though it does not block vision") — explicitly not modeled. Whether the target is visible at all (`TokenVisionService.ComputeLineOfSight`'s own job, a separate call if the caller needs it) — `ComputeCoverDegree` judges only the four samples, and returns `Full` even if the target's center happens to be visible some other way; a deliberate simplification, not a bug.
- `SegmentIntersection.cs`/`ObstacleGeometry.cs`/`LineOfSight.cs`/`BoardGeometry.cs` — reused, not modified. `TokenVisionService.cs`/`TokenVisionRepositoryContracts.cs`/`SqliteTokenVisionRepository.cs`, `PlayerVisibilityService.cs`/`FogOfWarRepositoryContracts.cs`/`SqliteFogOfWarRepository.cs`, `SqliteSceneRepository.cs`/`SceneRepositoryContracts.cs`/`SqliteObstacleRepository.cs`/`ObstacleRepositoryContracts.cs`, `CampaignMembershipAuthorization.cs`/`CampaignRepositoryContracts.cs`/`SqliteCampaignRepository.cs` — all untouched. Any Unity client file.

## 6-8. Domain / Application / Persistence
Domain: `Cover.cs` (new), no dependency on `Odyssey.Application`/`Odyssey.Persistence` (ADR-001). Application: `CoverSuggestionService.cs` (new), one new `ErrorCodes.cs` entry. No Persistence-layer file at all -- this task adds no new storage; it reads existing `ISceneRepository`/`IObstacleRepository` ports only.

## 9. Tests and validation
`TC-PERSIST-120`–`125` (`CoverTests.cs`, `Odyssey.Tests.Unit`, pure domain): no obstacles → `None`; a Wall blocking all four samples → `Full`; an open Door geometrically on the path → `None` (proves `BlocksVision`, not raw geometry, gates blocking); a Window on the path → `None`; a closed Door blocking all four samples → `Full`; the full 0/1/2/3/4-blocked-samples table walked explicitly, one obstacle at a time, mapping to `None`/`Half`/`Half`/`ThreeQuarters`/`Full`. `TC-PERSIST-126`–`129` (`CoverSuggestionServiceTests.cs`, `Odyssey.Tests.Persistence`, full integration): the service's result matches a direct `CoverGeometry.ComputeCoverDegree` call on the same coordinates/obstacles, for both the no-obstacle and full-cover cases; a typed error across two different Scenes; no mutation of token/obstacle state across repeated calls. A deliberate mutation (the sample-blocking intersection test itself short-circuited to never report a block) made three representative tests fail and was reverted. Regression: the existing combat-pipeline test suite (`AttackEvaluationServiceTests.cs`, `CoreAttackRulesEvaluatorIntegrationTests.cs`, `AttackPipelineIntegrationFixtureTests.cs`, part of the same full `dotnet test` run) and `LineOfSightTests.cs`/`ObstacleGeometryTests.cs` pass unchanged, proving the combat pipeline is genuinely untouched.

`Odyssey.Tests.Unit.dll`: 220/220 (was 214, +6). `Odyssey.Tests.Persistence.dll`: 924/924 (was 920, +4).

## 10-17. (see plan)

## 18. Change control

### Decisions made during execution
- **Sample orientation fixed at the four diagonals (45°/135°/225°/315° relative to the target's own position), not aligned to the attacker-target line.** The ТЗ's own §1.1 explicitly left this choice open ("конкретный выбор ориентации сэмплов задокументировать в реализации, не критично для корректности метода"). The diagonal orientation was chosen only because it is trivial to express as a fixed `(±offset, ±offset)` pair with no trigonometry against the attacker-target bearing needed — any other fixed, evenly-spaced-around-the-target orientation would be equally correct per the ТЗ's own algorithm description.
- **One new `ErrorCodes.cs`/`ERROR_CODES.md` entry** (`CoverSuggestionAttackerAndTargetNotInSameScene`) was added, not named in the ТЗ's allowed-path list — the same, disclosed, mechanical shared-infrastructure pattern every prior SLICE-10 task in this track has already established and disclosed.
- **No new Persistence-layer file.** Unlike every other SLICE-10 block so far, this task introduces no new storage at all (`SuggestCover` only reads through the already-existing `ISceneRepository`/`IObstacleRepository` ports) — §6-8 above reflects that there is simply nothing to add at that layer, not an omission.
- **`ComputeCoverDegree` calls `ObstacleGeometry.BlocksVision`, not a new predicate** — verified directly in `CoverTests.ComputeCoverDegree_OpenDoorOnThePath_IsNone_UsesBlocksVisionNotGeometry`/`ComputeCoverDegree_WindowOnThePath_IsNone`, which place an obstacle geometrically on the attacker-target line that does not block vision and confirm zero cover results, per ADR-020 section 12.4's own mandate.

### Findings (reported, not fixed silently)
- No file of `AttackEvaluationService.cs`/`AttackApplyService.cs`/`CoreAttackRulesEvaluator.cs`/`SqliteAttackStateReader.cs`/`AttackPipelineContracts.cs`/`SqliteAttackApplyRepository.cs`/`SegmentIntersection.cs`/`ObstacleGeometry.cs`/`LineOfSight.cs`/`BoardGeometry.cs`/`TokenVisionService.cs`/`TokenVisionRepositoryContracts.cs`/`SqliteTokenVisionRepository.cs`/`PlayerVisibilityService.cs`/`FogOfWarRepositoryContracts.cs`/`SqliteFogOfWarRepository.cs`/`SqliteSceneRepository.cs`/`SceneRepositoryContracts.cs`/`SqliteObstacleRepository.cs`/`ObstacleRepositoryContracts.cs`/`CampaignMembershipAuthorization.cs`/`CampaignRepositoryContracts.cs`/`SqliteCampaignRepository.cs` is in this diff.
- No Unity client file, no ADR file, is in this diff.
- `CoreAttackRulesEvaluator.Modifiers`/`Hit` are unchanged (confirmed by the full existing combat-pipeline test suite passing unmodified, alongside this task's own tests).

### Blockers
None.
