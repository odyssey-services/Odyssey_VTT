# ODY-S10-114 — SLICE-10 Block 6 Part 3: Token Facing/FOV/View-Distance UI + Cover Preview

## 1. Task identity
`ODY-S10-114`; status: In Review (Draft PR, merge deferred to the product owner). Block 6 of the `SLICE-10` track, part 3 -- the last named part of Block 6. Unlike part 2 (`ODY-S10-113`), this task needs no new server-side call at all: `TokenVisionService.SetTokenFacing`/`SetTokenVisionParameters`/`ComputeLineOfSight` (Block 3, `ODY-S10-108`) and `CoverSuggestionService.SuggestCover` (Block 5, `ODY-S10-110`) already exist with the exact signatures and authorization this task needs.

## 2. Goal
Let a token's own controller (or MainGM) set its facing, let MainGM set its FOV/view-distance, both through a new inspector panel on the board; show a minimal direction/FOV-cone indicator for the inspected token; and let any participant preview graduated cover between the inspected token (attacker) and a dropdown-selected target, via the existing, unauthorized `SuggestCover`.

## 3. Authority
This task's governing ТЗ; `BoardScreenPresenter.cs`'s own obstacle-inspector pattern (`_selectedObstacleId`/`BuildObstacleInspector`/`ShowObstacleInspectorIfSelected`) as the direct precedent for the new token inspector; `PositionSegmentElement`'s rotation technique (Block 6 part 1) reused unchanged for the facing/FOV indicator; `BoardFogOfWarPresenter.cs`/`BoardToolbarPresenter.cs` as the precedent for a small, focused presenter class; `ODY-S10-112`'s own "read the Revision fresh immediately before a revision-gated write, never from a render-time cache" lesson, applied here for the first time to `TokenVisionSettingsRecord.Revision`.

## 4. In scope
- **`CoverSuggestionService.cs`** (doc-comment only, the sole server-side change): corrects the stale claim that `CoreAttackRulesEvaluator`/`Modifiers`/`Hit` are untouched "by this task at all" -- true of `SuggestCover` itself, no longer true of the pipeline in general since Block 5 Part C.
- **`BoardTokenInspectorPresenter.cs`** (new): the facing/FOV/view-distance fields and buttons, the cover-target dropdown/button/result label -- a small presenter, no repository/authorization logic of its own.
- **`BoardScreenPresenter.cs`** (point-edits): `ShowTokenVisionInspectorIfSelected`, the facing/FOV-cone indicator (`RenderTokenVisionIndicator`/`AddVisionIndicatorLine`), `TryApplyTokenFacing`/`TryApplyTokenVisionParameters`/`TryCheckCover`, a new `_tokenControllersByTokenId` bookkeeping dictionary (the same in-memory-mirror convention `_tokenZOrdersByTokenId`/`_tokenScalesByTokenId` already use) for the facing-editability presentational gate.
- Tests, catalogue `TC-BOARD-129`-`134`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
- A live preview of line-of-sight using not-yet-saved (draft) facing/FOV values -- `ComputeLineOfSight` only accepts already-persisted settings; adding a separate draft-preview path is a future task if ever needed.
- A symmetric attacker/target picker for cover preview -- only the inspected token is ever the attacker.
- Wiring cover preview into any real combat/attack flow -- none exists in this client.
- Final art for the direction/FOV-cone indicator -- minimal lines, Block 6 part 1's own precedent.
- Any change to `SetTokenFacing`/`SetTokenVisionParameters`/`SuggestCover`'s own authorization.

## 6-8. Client/server layers touched
Server: `CoverSuggestionService.cs` (doc comment only, no code change). Client runtime: `BoardTokenInspectorPresenter.cs` (new); `BoardScreenPresenter.cs` (point-edits). Tests: `BoardScreenPresenterTokenVisionInspectorTests.cs` (new).

## 9. Tests and validation
`TC-BOARD-129`-`134` (`BoardScreenPresenterTokenVisionInspectorTests.cs`, real `SqliteTokenVisionRepository`/`SqliteObstacleRepository`/`SqliteFogOfWarRepository`, never a mock): selecting a token populates the inspector's fields from the real stored vision settings; `SetTokenFacing` succeeds for the token's own controller and is denied for a stranger, leaving the stored value unchanged; `SetTokenVisionParameters` succeeds only for MainGM, denied for the owner alone; `SetTokenFacing` succeeds even when the best-effort `RecordExploration` it triggers is artificially broken (a fog repository whose `ListReveals` always fails, isolating the fault from the facing command's own, separate `GetVisionSettings` call); a vision-settings `Revision` changed by another participant between `Refresh()` and the apply call does not cause a stale-revision denial (the `ODY-S10-112` lesson re-verified for token vision settings); the cover-preview command calls `SuggestCover` with the inspected token as attacker and the dropdown selection as target, matching a direct call on `CoverSuggestionServiceTests`' own known Full-cover geometry (wall at x=5 between attacker (0,0) and target (10,0)). Regression: the full pre-existing EditMode/PlayMode suites (Block 6 parts 1-2) pass unchanged.

Two deliberate mutations: (1) the facing-editability gate (`facingEditable`) inverted -- the owner-facing test failed as expected, reverted. (2) the fog-repository-failure test's own fault (`ListReveals` always failing) removed -- `TC-BOARD-132` no longer proved anything meaningful without the fault, confirming the mutation actually isolates `RecordExploration`'s own failure; restored.

## 10-17. (see plan)

## 18. Change control

### Decisions made during execution
- **Two separate buttons ("Apply Facing" / "Apply Vision"), not one combined "Apply" button** -- task contract section 2.1's own instruction that a simultaneous change to both must be two separate calls, never merged. Two buttons make this the only possible outcome by construction, rather than requiring `BoardScreenPresenter` to detect "which field actually changed" from a single combined submit.
- **Fields are not re-populated from the server on every `Refresh()`, only when the inspected token itself changes** (`_lastInspectedTokenId` tracking) -- an unrelated `Refresh()` (another participant moving elsewhere, this board's own fog recompute) must never silently overwrite a value the local user has typed but not yet applied. No precedent existed for this exact concern (the obstacle damage-amount field is not tied to any server-read value at all), so this is this task's own original design decision, disclosed here.
- **Cover-target identification uses the raw `TokenId` string** -- recon (task contract section 1) confirmed no display-name convention exists anywhere in this client for a token; `TokenRecord` itself has no name field. Introducing one would be new, unscoped product surface, not a client-only UI task's place to invent.
- **The facing/FOV-cone indicator computes its angle directly in pixel space** rather than converting a separately-computed world-space endpoint through the camera transform -- valid because `BoardCamera` scales both axes by the identical factor with no flip, so an angle is preserved exactly between world and pixel space; confirmed against `LineOfSight.IsWithinFovCone`'s own `Math.Atan2(dy, dx)` bearing convention so the drawn cone visually matches what the server actually computes.
- **The mutation test for `RecordExploration`'s best-effort invariant (`TC-BOARD-132`) breaks `IFogOfWarRepository.ListReveals`**, not `ITokenVisionRepository.GetVisionSettings` (the technique `ODY-S10-113`'s own equivalent test used) -- `TryApplyTokenFacing` itself already calls `GetVisionSettings` as its own fresh-revision read, so breaking that interface would fail the facing command itself before `RecordExploration` is ever reached. Breaking the fog repository instead isolates the fault to exactly the code path this test means to exercise.

### Findings (reported, not fixed silently)
- No file under `Packages/com.odyssey.domain/**`, `Packages/com.odyssey.persistence/**`, `Packages/com.odyssey.rules/**` is in this diff. The only `Odyssey.Application` file touched is `CoverSuggestionService.cs`, and only its doc comment -- `git diff` on that file contains no non-comment line changes.
- `TokenVisionService.cs`, `TokenVisionRepositoryContracts.cs`, `SqliteTokenVisionRepository.cs`, `PlayerVisibilityService.cs`, and every combat-pipeline file are used exactly as they already existed -- none is in this diff.

### Blockers
None.
