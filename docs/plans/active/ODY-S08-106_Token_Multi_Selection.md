# ExecPlan — ODY-S08-106 Token Multi-Selection

## 1. Purpose
Replace the single selected token with a session-only selection set, add Ctrl+click add/remove, and drag a selected group together, keeping every single-token behaviour of ODY-S08-104/105 unchanged.

## 2. Scope
`BoardScreenPresenter.cs`, `BoardScreenPresenterTests.cs`, `Tests/Metadata/test-catalog.json`, the SLICE-08 backlog, this plan and its task contract.

## 3. Non-goals
Click-to-place group move, marquee/Shift-range selection, persisted selection, atomic batch move API, and every file the ТЗ forbids.

## 4. Architecture
`_selectedTokenIds` (string-keyed set) plus per-gesture drag state (ctrl flag, group keys, start positions). `BeginTokenDrag` fixes the group from the selection at press time; `MoveTokenDrag`/`ApplyDragPreview` shifts every member's in-memory position by the same delta (no repository access); `EndTokenDrag` resolves a click (add/remove/replace by the Ctrl flag) or commits the group as sequential `TryMoveTokenTo` calls with destinations computed first.

## 5. Milestones
1. Selection set and click semantics. 2. Empty-board click rules. 3. Group drag preview and commit. 4. Tests `TC-BOARD-086`-`093`. 5. Catalog, backlog closure, contract/plan. 6. Validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18 (non-atomic group commit is the key disclosed behaviour).

## 9. Validation and acceptance evidence
`dotnet test` (unaffected, must stay green); Unity 6000.4.0f1 batchmode EditMode run; `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` limited to the allowed paths.

## 10. Recovery and rollback
Revert the PR; no data or schema involved.
