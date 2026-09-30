# ODY-S10-115 — Obstacles Filtered By Fog Of War (Same As Tokens)

## 1. Task identity
`ODY-S10-115`; status: In Review (Draft PR, merge deferred to the product owner). A gap-fix task, not a named product block -- found at Block 6's own close-out (`ODY-S10-111`-`114`): the client draws every obstacle in the Scene unconditionally (geometry plus open/closed door state), relying only on the fog overlay's own 82%-opacity darkening to visually hide it. A determined player could still read a never-explored door's existence/state through the translucency. The product owner decided this deserves a real fix, filtering obstacles by map memory the same way tokens already are (`ODY-S10-113`).

## 2. Goal
A non-MainGm player must not see (geometry or open/closed state) an obstacle none of whose sample points has ever been inside their own explored map memory. MainGm continues to see everything, unconditionally. Server-side computations that need the true, complete obstacle set (line-of-sight, cover) must be entirely unaffected.

## 3. Authority
This task's governing ТЗ; `PlayerVisibilityService.cs`'s own `ListExploredReveals`/`ComputeVisibleTokens` self-scoped-or-MainGm pattern, reused exactly for the new `ListExploredObstacles`; `CoverGeometry`'s own multi-sample-point technique as the direct analogy for this task's own three-point (both endpoints, midpoint) obstacle sampling; `BoardScreenPresenter.Refresh()`'s own existing "a second, additive authorized read layered on top of an already-unfiltered one" relationship between `ListTokens` and `ComputeVisibleTokens`, reused identically for `ListObstacles`/`ListExploredObstacles`.

## 4. In scope
- **`PlayerVisibilityService.ListExploredObstacles`** (`Packages/com.odyssey.application/Runtime/Board/PlayerVisibilityService.cs`, the only server-side change): self-scoped-or-MainGm gated; MainGm gets the full, unfiltered `IObstacleRepository.ListObstacles` result; anyone else gets it filtered to obstacles with at least one of three sample points inside any of their own `ListExploredReveals` circles. New `ListExploredObstaclesRequest` in the same file.
- The corresponding server-side test file (`PlayerVisibilityServiceTests.cs`, point-edited).
- **`BoardScreenPresenter.cs`** (point-edit): `Refresh()` calls `ListExploredObstacles` (self-scoped) for `!LocalActorIsMainGm` and feeds `RenderObstacles` the filtered list instead of the raw one.
- A new client-side EditMode test file.
- Tests, catalogue `TC-PERSIST-142`-`147`/`TC-BOARD-135`-`136`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
- A "frozen at last observation" obstacle-state memory model -- the current, live state (including `IsOpen`) is always returned for a "known" obstacle; a future task's own scope if ever needed.
- Any change to `IObstacleRepository.ListObstacles` itself, or to any of its own callers (`TokenVisionService.ComputeLineOfSight`, `CoverSuggestionService.SuggestCover`) -- both continue reading the true, complete, unfiltered set.
- Partial obstacle visibility (e.g. "only one endpoint of a wall is visible") -- an obstacle is either shown whole or not at all.

## 6-8. Client/server layers touched
Server: `PlayerVisibilityService.cs` (point-edit, one new method + one new request type). Client runtime: `BoardScreenPresenter.cs` (point-edit). Tests: `PlayerVisibilityServiceTests.cs` (point-edit), `BoardScreenPresenterObstacleFogTests.cs` (new).

## 9. Tests and validation
`TC-PERSIST-142`-`147` (`PlayerVisibilityServiceTests.cs`, real `SqliteObstacleRepository`/`SqliteFogOfWarRepository`/`SqliteCampaignRepository` against a temp-directory campaign): an obstacle with no sample point inside any reveal is not returned; a reveal covering only the first endpoint, only the second endpoint, or only the midpoint each independently is enough to reveal the whole obstacle; a MainGm target gets the full, unfiltered list regardless of exploration; an ordinary player is denied passing another player's `TargetUserId`. `TC-BOARD-135`-`136` (`BoardScreenPresenterObstacleFogTests.cs`, real repositories, never a mock): a non-MainGm actor does not render an unexplored obstacle while a MainGm actor sees it unconditionally; an obstacle that becomes explored via a token move's own best-effort `RecordExploration` appears on the following `Refresh()`. Regression: the full pre-existing EditMode/PlayMode suites (Block 5/6: cover, HP, obstacle drawing, fog, facing/FOV) pass unchanged.

One deliberate mutation: the three-sample-point check in `IsAnySamplePointExplored` collapsed to midpoint-only -- `TC-PERSIST-143`/`144` (the two endpoint-only-coverage tests) failed as expected while `TC-PERSIST-145` (midpoint) kept passing, confirming each of the three points is independently load-bearing; reverted.

`dotnet test` (`Odyssey.Tests.Persistence`, full suite): 947/948 passing -- the one failure (`SqliteBackupRepositoryTests.CreateBackup_KilledMidCopy_NeverPromotesPartialBackup_SourceUntouched`) is a second, unrelated pre-existing harness-build-ordering gap (needs `Odyssey.Tests.Persistence.BackupKillHarness.csproj` built first in a fresh worktree), independently confirmed by building that harness and re-running the test in isolation, which then passed -- the same category of gap `ODY-S10-113` already found and disclosed for `Odyssey.Tests.Persistence.RecoveryHarness`. Unity 6000.4.0f1 batchmode (`scripts/test-unity.ps1`): compile/EditMode/PlayMode counts recorded in the task's own PR.

## 10-17. (see plan)

## 18. Change control

### Decisions made during execution
- **Three sample points per obstacle segment (both endpoints, midpoint)**, by direct analogy to `CoverGeometry`'s own four-sample-point technique for a target's cover degree -- not a reuse of that specific code (a different geometric question), just the same "a few representative points, not a full boundary scan" shape, as the ТЗ itself suggested.
- **The obstacle's current, live state is always returned for a "known" obstacle**, not a snapshot frozen at first discovery -- explicitly the ТЗ's own accepted simplification, disclosed rather than silently assumed. A player who has ever seen a door sees its real-time open/closed state even while it is currently outside their own live line of sight.
- **`Refresh()` keeps its own pre-existing unfiltered `ListObstacles` read** (needed for the MainGm branch and as the fallback on a filtered-read failure) and adds a second, additive `ListExploredObstacles` call for the non-MainGm branch -- deliberately mirroring the exact shape `ListTokens`/`ComputeVisibleTokens` already have in the same method, not a new pattern.

### Findings (reported, not fixed silently)
- A second, unrelated pre-existing harness-build-ordering gap was found in the same fresh-worktree `dotnet test` run that first surfaced this category of issue in `ODY-S10-113`: `SqliteBackupRepositoryTests` needs `Odyssey.Tests.Persistence.BackupKillHarness.csproj` built explicitly first, the same way `SqliteSavingPipelineTests` needed `Odyssey.Tests.Persistence.RecoveryHarness.csproj`. Confirmed unrelated to this task's own files by building that harness and re-running the affected test alone, which then passed. Not fixed (out of this task's scope).
- No file under `Packages/com.odyssey.domain/**`, `Packages/com.odyssey.persistence/**`, `Packages/com.odyssey.rules/**`, or any `Odyssey.Application` file other than `PlayerVisibilityService.cs`, is in this diff. `IObstacleRepository`/`SqliteObstacleRepository.cs`/`IFogOfWarRepository` are used exactly as they already existed.

### Blockers
None.
