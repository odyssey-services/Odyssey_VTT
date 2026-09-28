# ExecPlan — ODY-S08-105 Token Z-Order and Scale

## 1. Purpose
Persist a per-token draw order and visual scale, raise a token on pointer-down, scale it with Shift+wheel, and keep plain-wheel camera zoom and ODY-S08-104 drag/click/select unchanged.

## 2. Scope
`SceneRepositoryContracts.cs`, `SqliteSceneRepository.cs`, `BoardScreenPresenter.cs`, existing test files, `Tests/Metadata/test-catalog.json`, the SLICE-08 backlog, this plan and its task contract.

## 3. Non-goals
Multi-select (reserved `ODY-S08-106`), group operations, scale affecting movement/occupancy, send-to-back UI, schema migration, and every file the ТЗ forbids.

## 4. Architecture
Plain columns following the `PortraitAssetId` precedent; two revision-gated setters cloned from `SetTokenPortrait`. The presenter mirrors z-order/scale in memory (filled by `RenderTokens`, which sorts ascending by `ZOrder`), raises via `RaiseTokenToTop` (no call if already top; fresh revision read; in-place `BringToFront`), and scales via `ScaleTokenAt` (fresh revision; clamped; in-place resize), both reached from public number-driven entry points (`BeginTokenDrag`, `HandleBoardWheel`).

## 5. Milestones
1. Backend fields, DDL, setters, `CreateToken` max+1. 2. Backend tests `TC-BOARD-078`-`080`. 3. Presenter changes. 4. Presenter tests `TC-BOARD-081`-`085`. 5. Catalog, backlog decomposition, contract/plan. 6. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` (full); Unity 6000.4.0f1 batchmode EditMode run (exit code, counts); `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` limited to allowed paths.

## 10. Recovery and rollback
Revert the PR. Databases created by the new code carry two extra columns harmlessly; databases created before it are unsupported by the new code (see contract §18, no-migration decision).
