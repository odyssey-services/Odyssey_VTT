# ExecPlan — ODY-S08-107 Board Controls Rework

## 1. Purpose
Rework the board's mouse layout: left drag on empty board = selection box, Shift+click multi-select, middle-button pan, right-button local player marker.

## 2. Scope
`BoardScreenPresenter.cs`, new `BoardBoxSelectGesture.cs`, `BoardScreenPresenterTests.cs`, `Tests/Metadata/test-catalog.json`, the SLICE-08 backlog (row 7), this plan and its task contract.

## 3. Non-goals
Any network/persistence of the marker; changes to `BoardCamera`, `BoardPointerGesture`, `BoardMovementService`, backend, networking, asmdefs, verify scripts, ADRs.

## 4. Architecture
Button decision in `HandleBoardButtonDown/Up` with one explicit active-button state (tokens act on the left button only). Box gesture is a small pure type; membership is computed in world coordinates through `BoardCamera`. Pan reuses `BoardPointerGesture` as-is. The marker is a world-anchored `VisualElement` repositioned with the tokens.

## 5. Milestones
1. Button separation and gesture ownership. 2. Box gesture and selection. 3. Shift modifier. 4. Middle pan. 5. Right marker. 6. Tests `TC-BOARD-094`-`102` and catalog. 7. Backlog/contract/plan. 8. Validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (unaffected, green); Unity 6000.4.0f1 batchmode EditMode run; `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` limited to the allowed paths (plus the new type's `.meta`).

## 10. Recovery and rollback
Revert the PR; no data or schema involved.
