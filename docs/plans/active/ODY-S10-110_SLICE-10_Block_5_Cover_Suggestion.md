# ExecPlan — ODY-S10-110 SLICE-10 Block 5: Cover Suggestion

## 1. Purpose
Add a graduated cover *hint* (`None`/`Half`/`ThreeQuarters`/`Full`), computed from Block 2's obstacle geometry via a new 4-sample-around-the-target model, reusing Block 3's own `ObstacleGeometry.BlocksVision`/`SegmentIntersection.SegmentsIntersect` -- explicitly not wired into the combat pipeline (product decision 2026-09-29).

## 2. Scope
One new Domain file (`Cover.cs`), one new Application file (`CoverSuggestionService.cs`) plus one `ErrorCodes.cs` entry, no new Persistence file, two new test files, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Any automatic combat-pipeline integration (`CoreAttackRulesEvaluator`/`Modifiers`/`Hit`/damage); configurable per-token size; the full "4 attacker corners x 4 target corners" tabletop algorithm; the cover-hint UI (Block 6); cover degree differentiated by obstacle type beyond the existing `BlocksVision` predicate; whether the target is visible at all (a separate `ComputeLineOfSight` call, not this task's job).

## 4. Architecture
`CoverGeometry.ComputeCoverDegree` is pure `Odyssey.Domain.Geometry` (no dependency on any other module, ADR-001), the same two-layer shape `LineOfSight` already established: four fixed-offset sample points around the target, each tested via `SegmentIntersection.SegmentsIntersect` against obstacles for which `ObstacleGeometry.BlocksVision` is true (ADR-020 section 12.4: the same predicate as line-of-sight, not a new one), counted and mapped to a `CoverDegree`. `CoverSuggestionService.SuggestCover` is a thin Application-layer static method reading `ISceneRepository`/`IObstacleRepository` and calling the pure function -- no authorization (product decision: a hint, and scene-geometry reads are already open everywhere in this track) and no new storage.

## 5. Milestones
1. Recon confirmation of `CoreAttackRulesEvaluator`'s actual `Modifiers`/`Hit` state and that no extensible modifier-provider mechanism exists anywhere (done in the task's own §0). 2. `Cover.cs` and `CoverTests.cs`. 3. `CoverSuggestionService.cs` and the one `ErrorCodes.cs` entry. 4. `CoverSuggestionServiceTests.cs` and a mutation check. 5. Catalogue, `ERROR_CODES.md`, backlog, contract, plan. 6. Full validation (including the existing combat-pipeline test suite, to prove it is untouched) and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 220/220, Persistence 924/924, including the pre-existing combat-pipeline suite unchanged); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the sample-blocking test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list (confirming no combat-pipeline or Block 1-4 file is touched).

## 10. Recovery and rollback
Revert the PR; no schema or data change at all (this task adds no new storage).
