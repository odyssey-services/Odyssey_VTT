# ODY-S10-112 — Point Fix: `ToggleObstacleDoor` Used a Stale (Cached) Revision

## 1. Task identity
`ODY-S10-112`; status: In Review (Draft PR, merge deferred to the product owner). Same branch/PR as `ODY-S10-111` (SLICE-10 Block 6 part 1, `feat/slice10-block6-obstacle-drawing-ui`, PR #201) -- a follow-up fix for a finding from that PR's own independent review, landed as a new commit on the same branch, per this track's own established convention for a same-scope point fix (`ODY-S10-105`'s own follow-up fix is the direct precedent).

## 2. Goal
`BoardScreenPresenter.ToggleObstacleDoor` built `ToggleDoorStateRequest` from the `RenderObstacles`-cached `ObstacleRecord` (last `Refresh()`'s own render-time snapshot) rather than re-reading the obstacle fresh immediately before the write -- the one place in the whole file that broke its own established "always re-read the revision fresh right before a revision-gated command" rule (`TryMoveTokenTo`, `SetTokenZOrder`, `SetTokenScale`, and `TryApplyObstacleDamage` -- added by the very same PR -- all already follow it). Consequence: not data corruption (the server's own revision-gated CAS in `SqliteObstacleRepository.ToggleDoorState` fails closed either way), but a spurious "denied" toggle whenever another participant had already toggled the same door since the presenter's last `Refresh()`.

## 3. Authority
The independent review finding on PR #201; `TryApplyObstacleDamage`'s own fresh `GetObstacleDurability` read (added in the very same PR) as the exact precedent to follow.

## 4. In scope
- **`BoardScreenPresenter.ToggleObstacleDoor`** (point-edit): before building `ToggleDoorStateRequest`, calls `IObstacleRepository.ListObstacles` (the only read option available -- `IObstacleRepository` has no single-obstacle-by-id lookup, only `ListObstacles`/Scene and `GetObstacleDurability`/id for durability specifically; adding a new method to the server-side contract for a client-only fix was explicitly out of scope) and scans the fresh result for the matching `ObstacleId`. If not found, or no longer a `Door`, follows the exact same "status message + `Refresh()`" failure path every other rejected command in this file already uses -- no crash, no special case.
- One new test (`TC-BOARD-122`) reproducing the exact race: toggle the door directly through the repository between the presenter's `Refresh()` and its own click, and assert the click still succeeds using the fresh state.

## 5. Out of scope
- Any file under `Packages/com.odyssey.domain/**`, `Packages/com.odyssey.application/**`, `Packages/com.odyssey.persistence/**`, `Packages/com.odyssey.rules/**` -- the fix is entirely in how the client sources the `Revision`/`IsOpen` values it already had access to via the existing, unmodified `ListObstacles` contract; no server-side change of any kind. No change to `ToggleDoorState`'s own authorization (still open to any registered campaign participant, exactly as `ODY-S10-111` left it). No behavior change to any other command (`CreateObstacle`, `ApplyObstacleDamage`, token movement, etc.).

## 6-8. Client layers touched
Runtime: `BoardScreenPresenter.cs` (point-edit only). Tests: `BoardScreenPresenterObstacleTests.cs` (one new test).

## 9. Tests and validation
`TC-BOARD-122`: creates a Door, `Refresh()`es (caching Revision 1/closed), then toggles the same door directly through `SqliteObstacleRepository.ToggleDoorState` (simulating another participant, bumping it to Revision 2/open) without a second `Refresh()`, then clicks the door through the presenter's own `Select`-mode click path. Asserts the click succeeds (Revision 3) and produces the correct result (closes the door, since it was actually open from the race) -- both assertions fail under the old, stale-cache behavior (a `RevisionConflict` denial using the cached Revision 1). A deliberate mutation (forcing the fresh-read result to be discarded in favor of the stale cached record, reproducing the original bug exactly) made this one test fail and was reverted -- no other test was affected, confirming the fix is precisely scoped. Regression: the full pre-existing 136-test EditMode suite (now 137 with this task's own new test) and 6-test PlayMode suite from `ODY-S10-111` pass unchanged.

`Odyssey.Tests.Unity.EditMode`: 137/137 (was 136, +1). `Odyssey.Tests.Unity.PlayMode`: 6/6 (unchanged).

## 10-17. (see plan)

## 18. Change control

### Decisions made during execution
- **No single-obstacle-by-id read exists on `IObstacleRepository`** (confirmed by reading the contract directly): only `ListObstacles(campaign, sceneId, ...)` and `GetObstacleDurability(campaign, obstacleId, ...)` (durability only, not the `ObstacleRecord` itself). Per the ТЗ's own explicit instruction, this fix uses the existing `ListObstacles` and scans for the matching id, rather than adding a new method to the server-side contract for what is otherwise a purely client-side fix.
- **The "obstacle not found / no longer a Door" case reuses the exact same status-message-plus-`Refresh()` failure path** every other rejected command in this file already uses -- not a new UI affordance, not a crash.

### Findings (reported, not fixed silently)
- No file under `Packages/com.odyssey.domain/**`, `Packages/com.odyssey.application/**`, `Packages/com.odyssey.persistence/**`, `Packages/com.odyssey.rules/**` is in this diff.
- No behavior change to `CreateObstacle`, `ApplyObstacleDamage`, token movement, or `ToggleDoorState`'s own authorization.

### Blockers
None.
