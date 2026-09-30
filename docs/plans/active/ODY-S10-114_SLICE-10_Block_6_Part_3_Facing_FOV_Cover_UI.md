# ExecPlan — ODY-S10-114 SLICE-10 Block 6 Part 3: Token Facing/FOV/View-Distance UI + Cover Preview

## 1. Purpose
Give a token's controller/MainGM a real UI to set facing/FOV/view-distance (already-existing, unmodified server commands) and any participant a cover preview between two tokens, closing out Block 6 with no new server-side capability needed.

## 2. Scope
One doc-comment fix (`CoverSuggestionService.cs`), one new client presenter (`BoardTokenInspectorPresenter.cs`), point-edits to `BoardScreenPresenter.cs`, one new EditMode test file, the catalogue, the backlog, this plan and the contract.

## 3. Non-goals
A live draft-facing/FOV line-of-sight preview; a symmetric attacker/target cover picker; wiring cover preview into a combat flow that does not exist; final indicator art; any authorization change to the three reused server commands.

## 4. Architecture
`BoardTokenInspectorPresenter` owns only UI Toolkit fields/buttons and fires plain callbacks with raw values -- `BoardScreenPresenter` remains the only place that talks to `TokenVisionService`/`CoverSuggestionService` or decides authorization gates (presentational only, server re-checks regardless). `ShowTokenVisionInspectorIfSelected` mirrors `ShowObstacleInspectorIfSelected`'s own selected-then-shown shape exactly, gated on the existing single-selection `SelectedTokenId` (never the multi-select `_selectedTokenIds` group, per the task's own explicit invariant). Field population happens once per selection change (`_lastInspectedTokenId` tracking), not every `Refresh()`, so an in-progress edit is never clobbered. `TryApplyTokenFacing`/`TryApplyTokenVisionParameters` each re-read `TokenVisionSettingsRecord.Revision` fresh immediately before writing -- the `ODY-S10-112` lesson, reapplied. `TryCheckCover` is a direct, unauthorized `SuggestCover` call with no `Refresh()` (nothing persisted). The facing/FOV-cone indicator reuses `PositionSegmentElement`'s exact rotation technique unchanged.

## 5. Milestones
1. Recon: confirm `SetTokenFacing`/`SetTokenVisionParameters`/`SuggestCover`'s exact signatures/authorization against the ТЗ's own stated facts, confirm the obstacle-inspector pattern to replicate, confirm no token display-name convention exists anywhere in the client. 2. `CoverSuggestionService.cs` doc-comment fix; `dotnet build` to confirm it compiles. 3. `BoardTokenInspectorPresenter.cs`. 4. `BoardScreenPresenter.cs` point-edits: `_tokenControllersByTokenId` bookkeeping, `ShowTokenVisionInspectorIfSelected`, the vision indicator, the three `Try*` command methods, the three button callbacks. 5. `BoardScreenPresenterTokenVisionInspectorTests.cs` (6 EditMode tests). 6. Two deliberate mutations, each reverted. 7. Catalogue, backlog, contract, plan. 8. Full Unity batchmode compile + EditMode + PlayMode validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract section 9 and section 18.

## 9. Validation and acceptance evidence
`dotnet build` on `Odyssey.Application` after the doc-comment edit; Unity 6000.4.0f1 batchmode (`scripts/test-unity.ps1`): compile, EditMode, PlayMode, recorded in the task's own PR; two mutations (the facing-editability gate inverted; the fog-repository-failure test's own fault removed); `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` reviewed against the forbidden-path list, with `git diff` on `CoverSuggestionService.cs` specifically checked to contain no non-comment change.

## 10. Recovery and rollback
Revert the PR. No schema or server-behavior change at all (the one server file touched is a doc comment), so rollback is a pure code revert with zero data-migration concern.
