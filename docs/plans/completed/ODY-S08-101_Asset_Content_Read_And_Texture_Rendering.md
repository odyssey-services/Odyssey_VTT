# ExecPlan — ODY-S08-101 Asset Content Read API + Minimal Texture Rendering

## 1. Purpose
Create the `SLICE-08` backlog and implement its first block: an asset read API (the missing counterpart of `RegisterAsset`) and minimal texture rendering of the scene background and token portraits in `BoardScreenPresenter`.

## 2. Scope
`SLICE-08_IMPLEMENTATION_BACKLOG.md`; `ISceneRepository.ReadAssetContent`/`GetScene` and their SQLite implementations; two new error codes and registry rows; `BoardScreenPresenter` texture rendering with a lifetime cache; tests `TC-BOARD-033`-`046`; `test-catalog.json`; this task's contract and plan.

## 3. Non-goals
Camera/pan/zoom, drag-and-drop, file picker, token z-order/scale/selection, async loading, cache invalidation, format validation beyond `ImageConversion.LoadImage`, character portraits, movement logic, ADRs, geometry, and any change to `RegisterAsset` (see the contract's finding).

## 4. Architecture
Persistence: two read-only methods on `ISceneRepository`. `ReadAssetContent` = manifest lookup in the campaign's own database (isolation gives the cross-campaign check) -> path confinement -> `File.ReadAllBytes` -> SHA-256 compare. Presenter: `Refresh()` = list tokens -> `ApplySceneBackground()` (`GetScene`, then `LoadTexture` if a background id is set) -> `RenderTokens()` (`LoadTexture` per token portrait). `LoadTexture` consults a per-presenter `Dictionary<string, Texture2D>` first; on a miss it calls `ReadAssetContent`, decodes with `ImageConversion.LoadImage`, and caches only successes. Textures are applied as `style.backgroundImage` (cover) on the board area / token element; with no asset the original fill code paths run unchanged; with a failing asset the fallback visual is drawn and the first typed error is returned after rendering.

## 5. Milestones
M1 read: `BoardScreenPresenter` and its tests, `RegisterAsset`/`AssetManifestEntries`, `SLICE-07` backlog in full, `SLICE-UI-01` files, the Unity asmdefs and `scripts/test-unity.ps1`, `ADR`-independent facts (presenter is Unity-only, not built by `dotnet`; Unity 6000.4.0f1 is installed locally). M2 contracts + error codes. M3 SQLite implementation. M4 backend tests, run. M5 presenter changes. M6 Unity EditMode tests, run in batchmode. M7 catalog, registry rows, backlog, contract/plan. M8 full validation (`dotnet test`, the three verify scripts, the Unity EditMode run), commit, Draft PR, CI.

## 6. State and data flow
Persisted: `AssetManifestEntries` row + file under `Assets/Objects`. Read: `ReadAssetContent` returns bytes only after existence, confinement and hash checks. Presenter memory: texture cache keyed by `AssetId` string for the presenter's life, destroyed on `Dispose`.

## 7. Error handling
Backend: `AssetNotFound` (reused), `AssetFileMissing`, `AssetIntegrityFailed`, `SceneIoFailed`. Presenter: an asset error never aborts rendering; fallback visuals are drawn, the status label shows the safe reason, `Refresh()` returns the first error. `SceneNotFound` from `GetScene` keeps the old empty-board `Success`.

## 8. Test strategy
Backend against real temp campaigns, including a second real campaign and real file deletion/overwrite, and a path-traversal case where the hash would otherwise match. Presenter against a real SQLite campaign with real PNG bytes, asserting `style.backgroundImage.value.texture` directly (not inferred), fallback regression tests for both element kinds, and a counting `ISceneRepository` decorator for the cache proof.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` (whole solution); `verify-format.ps1`, `verify-repository.ps1`, `verify-test-structure.ps1`; the Unity EditMode run for `BoardScreenPresenterTests` in batchmode against Unity 6000.4.0f1 (the CI Unity job does not run the Editor); `git diff --name-status` against `main`.

## 10. Recovery and rollback
Additive: two interface methods, two error codes, presenter additions. Reverting restores the previous solid-color rendering; no data migration and no schema change are involved.
