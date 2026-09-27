# ExecPlan — ODY-S08-103 Asset Pool + File Picker + In-App Drag Onto the Board

## 1. Purpose
Replace the impossible OS-level drag-a-file-onto-the-board idea with a file-picker-into-a-pool, then in-app-drag-from-pool-onto-board flow.

## 2. Scope
`ListAssets`; vendored `StandaloneFileBrowser`; `NativeFileDialog`; `AssetTextureCache`; `AssetPoolPresenter`; `BoardScreenPresenter.ApplyDroppedAsset`/`BoardArea`; `TrialScreenPresenter` composition; tests `TC-BOARD-065`-`071`; catalog; `THIRD_PARTY_NOTICES.md`; backlog reformulation; this contract and plan.

## 3. Non-goals
Token drag-and-drop on the board (block 3b), camera/gesture code, backend method signatures beyond the one addition, token z-order/scale/selection, pool UI persistence.

## 4. Architecture
Backend: one new read method. Client: `NativeFileDialog` (sole SFB call site) -> `AssetPoolPresenter.UploadFromDialog` -> `RegisterAsset` -> `Refresh` (via `ListAssets` + shared `AssetTextureCache`). Drag: `AssetPoolPresenter`'s own pointer wiring (real event plumbing, untested) -> `BoardScreenPresenter.ApplyDroppedAsset` (pure decision, tested) -> `SetTokenPortrait`/`SetSceneBackground`, revision read fresh each time.

## 5. Milestones
1. Vendor the third-party library; confirm it compiles into `Odyssey.Unity.Client` with no asmdef/verify-test-structure.ps1 change. 2. Backend `ListAssets`. 3. `NativeFileDialog`, `AssetTextureCache` (extract from `BoardScreenPresenter`). 4. `AssetPoolPresenter` (list, upload, in-app drag wiring). 5. `BoardScreenPresenter.ApplyDroppedAsset`/`BoardArea`; compose in `TrialScreenPresenter`. 6. Tests. 7. Unity batchmode run; `dotnet` unaffected check; docs; PR.

## 6. State and data flow
Pool panel <-> `ListAssets`/`RegisterAsset`/`ReadAssetContent` (via `AssetTextureCache`). Drop -> `ApplyDroppedAsset` -> fresh read + `SetTokenPortrait`/`SetSceneBackground` -> `Refresh()`.

## 7. Error handling
Every backend call already returns typed `Result`s; the presenter surfaces the first failure through its status label, matching `BoardScreenPresenter`'s own convention. A cancelled dialog is not an error (`null`, not a failure).

## 8. Test strategy
Backend `ListAssets` test (persistence, real SQLite, campaign-scoped). Presenter-level: upload with an injected dialog delegate (chosen file / cancelled); drop decision with plain board-local pixel numbers over a token vs. the empty board; a same-token double-drop proving the revision is never cached. Real pointer-event wiring is not simulated, by this codebase's own established precedent.

## 9. Validation and acceptance evidence
Unity 6000.4.0f1 batchmode EditMode run (exit code, pass/fail counts); `dotnet build`/`dotnet test` green and unaffected; grep confirms no production `.csproj` references the vendored library; diff-scope check.

## 10. Recovery and rollback
Revert the PR; the vendored library is inert until called, and no data migration is involved.
