# ODY-S08-103 — Asset Pool + File Picker + In-App Drag Onto the Board (SLICE-08 blocks 3+4, reformulated)

## 1. Task identity
`ODY-S08-103`; status: In Review (Draft PR, merge deferred to the product owner). Reformulates and implements blocks 3 (its OS-drag part only) and 4 of `SLICE-08` (`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md`) as one combined block; follows `ODY-S08-102` (block 2, merged).

## 2. Goal
Let a player get an image onto the board without ever calling `RegisterAsset` programmatically: pick a file from disk into an asset pool, then drag an already-loaded pool item onto a token (sets its portrait) or onto the empty board (sets the scene background).

## 3. Authority
This task's governing ТЗ §0-§7; `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` blocks 3/4 (now reformulated, section 6 point 6); product-owner decision to replace OS-level file drag (confirmed impossible in a Standalone build) with a file-picker + in-app-drag two-step flow.

## 4. In scope
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs`: new `ISceneRepository.ListAssets`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs`: `ListAssets` implementation only.
- `Assets/Odyssey/Client/Runtime/ThirdParty/StandaloneFileBrowser/`: the vendored third-party library (source + `.meta` files, mirrored verbatim from upstream, including its own `.meta` GUIDs where upstream provided them).
- `Assets/Odyssey/Client/Runtime/NativeFileDialog.cs` (new): the library's single call site.
- `Assets/Odyssey/Client/Runtime/AssetTextureCache.cs` (new): the texture read+decode+cache pattern, extracted from `BoardScreenPresenter` so `AssetPoolPresenter` reuses it.
- `Assets/Odyssey/Client/Runtime/AssetPoolPresenter.cs` (new): the pool panel.
- `Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs`: refactored to use `AssetTextureCache`; new `BoardArea` accessor and `ApplyDroppedAsset`.
- `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs`: composes `AssetPoolPresenter` alongside the board.
- New/updated tests: `AssetPoolPresenterTests.cs` (new), additions to `BoardScreenPresenterTests.cs` and `SqliteSceneRepositoryTests.cs`.
- `Tests/Metadata/test-catalog.json`; `THIRD_PARTY_NOTICES.md` (see §18 -- outside the ТЗ's own §4 list, added anyway per `AGENTS.md`'s explicit policy); backlog reformulation; this contract and plan.

## 5. Out of scope
Dragging an already-placed token across the board (block 3's other half -- split out as block 3b, still not started); `BoardCamera.cs`/`BoardPointerGesture.cs` (untouched); `RegisterAsset`/`ReadAssetContent`/`SetSceneBackground`/`SetTokenPortrait` signatures/logic (only called); token z-order/scale/selection (block 5); persistence of pool-panel UI state; multi-select; delete/rename/search/sort in the pool; showing the asset's original file name (`AssetManifestEntryRecord` still does not carry it).

## 6. Domain contract
Unchanged.

## 7. Application contract
`ISceneRepository.ListAssets(CampaignHandle, CorrelationId) : Result<IReadOnlyList<AssetManifestEntryRecord>>` -- read-only, oldest-first, campaign-scoped by the same "one campaign, one database file" mechanism every other method here already relies on (no additional ACL check exists anywhere in this interface to mirror).

## 8. Persistence boundary
`SqliteSceneRepository.ListAssets`: `SELECT ... FROM AssetManifestEntries ORDER BY rowid` (the table has no timestamp column; `rowid` reflects insertion order for this ordinary table, matching `RegisterAsset`'s own INSERT). No change to any other method's behavior.

## 9. Tests and validation
`TC-BOARD-065`-`071` (§3 below). Regression: the full existing EditMode suite (blocks 1-2's tests, unrelated presenter tests) and the full `dotnet test` suite pass unchanged.

## 10. Compatibility and rollback
`ISceneRepository` gains a method; no existing signature changes. `BoardScreenPresenter`'s public surface gains `BoardArea` and `ApplyDroppedAsset`; every existing public member (`Camera`, `SelectToken`, `TryMoveSelectedTokenTo`, the pointer/wheel gesture methods) is unchanged. Rollback: revert the PR; the vendored library is inert until `NativeFileDialog`/`AssetPoolPresenter` call it, so a partial revert cannot leave a dangling reference.

## 11. Security and privacy
The native dialog only returns a local file path the player explicitly chose; nothing is sent anywhere. No credentials, no personal data. `RegisterAsset` already validates and content-addresses whatever file is registered (`ODY-S08-101`'s own fix for the same-name collision, since patched further in the standalone content-addressing fix).

## 12. Observability
No new event type. `SetTokenPortrait`/`SetSceneBackground` already emit their own events; a drop just calls them.

## 13. Performance
One extra `ListAssets` read per pool refresh; one extra `ReadAssetContent`+decode per pool item not already in the shared texture cache. No effect on the board's own render loop.

## 14. Dependencies
`ODY-S08-102`.

## 15. Dependencies (packages)
`gkngkc/UnityStandaloneFileBrowser` (MIT) -- see §18 and `THIRD_PARTY_NOTICES.md`.

## 16. Implementation plan
`docs/plans/active/ODY-S08-103_Asset_Pool_File_Picker_And_In_App_Drag_Onto_Board.md`.

## 17. Completion evidence
Unity 6000.4.0f1 batchmode EditMode run: exit code 0, 90/90 passed (7 new `TC-BOARD-065`-`071` plus every pre-existing EditMode test, including blocks 1-2's own). `dotnet build`/`dotnet test` unaffected and confirmed green; no production `.csproj` references any Unity assembly or the vendored library (verified by grep and by the `.asmdef`/`.csproj` structure itself -- see §18). `git diff --name-status` limited to the paths in §4 plus the disclosed `THIRD_PARTY_NOTICES.md` addition.

## 18. Change control

### Decisions made during execution
- **No new asmdef, no client-asmdef edit at all -- stricter than the ТЗ's own allowance for one.** The ТЗ anticipated needing to add a reference to `Odyssey.Unity.Client.Runtime.asmdef` (its §4's one explicit exception to "don't touch `.asmdef`"). Reading `verify-test-structure.ps1` (forbidden to touch, §5) first: it enforces an exact, closed reference set for `Odyssey.Unity.Client` (`Assert-SetEquals ... $allowed['Odyssey.Unity.Client']`), and the only sanctioned way to add an external reference is `$approvedExternalAsmdefReferences`, defined in that same forbidden file -- so exercising the ТЗ's own allowed exception would have required editing the very file §5 forbids. Resolved by not creating a separate asmdef at all: the vendored library's `.cs` files are placed inside `Assets/Odyssey/Client/Runtime/ThirdParty/StandaloneFileBrowser/`, i.e. inside `Odyssey.Unity.Client`'s own asmdef-scoped folder, so they compile into that assembly directly. No new reference, no asmdef-reference edit, `verify-test-structure.ps1` untouched, and the wrapper (`NativeFileDialog.cs`) still isolates every other file in the codebase from the library exactly as the ТЗ asked.
- **Third-party library vendored as a faithful mirror of upstream's own `Assets/StandaloneFileBrowser/` folder** (source `.cs` files, their own `.meta` sidecars with upstream's original GUIDs, and the two Windows-relevant native/managed plugin DLLs `Ookii.Dialogs.dll`/`System.Windows.Forms.dll` with their own already-correct `PluginImporter` `.meta` settings, Editor+Standalone-Win/Win64 enabled, everything else excluded) -- confirmed via the GitHub API (`api.github.com/repos/gkngkc/UnityStandaloneFileBrowser`): license MIT, not archived, default branch `master`; full `LICENSE.txt` fetched and reproduced in `THIRD_PARTY_NOTICES.md`. Mac/Linux/WebGL platform files and the upstream Sample project were left out (this project targets Windows only); each remaining platform file is self-contained behind its own `#if UNITY_STANDALONE_*`/`#if UNITY_EDITOR`, so omitting the others does not affect compilation.
- **`AssetTextureCache` extracted from `BoardScreenPresenter`'s own texture-loading code** (ТЗ §1.3's explicit ask: "не писать вторую независимую реализацию"). `BoardScreenPresenter.LoadTexture` is now a one-line forward to it; behavior (cache key, fallback-on-failure, decode-failure error, disposal) is unchanged -- proven by the full existing texture-rendering test suite (block 1) passing unchanged.
- **Drop-target decision (`ApplyDroppedAsset`) uses the same pure, camera-math hit test the rest of the board already uses, not `VisualElement.worldBound`/`ContainsPoint`.** A token is "hit" by comparing the drop's board-local pixel coordinates against that token's own current on-screen square, computed from its world position and the live `BoardCamera` -- exactly the data the renderer itself used to place it. This keeps `ApplyDroppedAsset` fully unit-testable with plain numbers, with no dependency on UI Toolkit's layout pass having run (this codebase's EditMode tests never assert on `worldBound`), consistent with the block-2 precedent (`BeginBoardPointerGesture` etc. also take plain board-local pixel numbers, not panel-space ones). The real cross-panel wiring in `AssetPoolPresenter` (which does use `WorldToLocal`/`ContainsPoint` to decide whether a drop lands on the board at all) is the untested "real event plumbing" half of the same split every other gesture in this presenter already has (pointer-down/move/up, wheel) -- see the next point.
- **The in-app drag's own pointer wiring in `AssetPoolPresenter` is not unit-tested**, by the same established precedent as `BoardScreenPresenter`'s pointer/wheel callbacks (no test in this codebase simulates a UI Toolkit pointer event). What is tested directly: the upload flow (`UploadFromDialog`, with an injected dialog delegate) and the drop decision (`BoardScreenPresenter.ApplyDroppedAsset`, with plain board-local coordinates) -- the two places real logic lives; the event glue connecting them to actual mouse input is read, not executed, exactly as block 2's `OnBoardPointerDown`/`OnBoardWheel` already were.
- **`UploadFromDialog` returns `Result<AssetManifestEntryRecord>?`** (a nullable struct, not `Result<AssetManifestEntryRecord?>`, which would need a `notnull`-constraint-violating nullable type argument): `null` means "the user cancelled, nothing happened at all"; a non-null `Result` carries either the registered asset or a real `RegisterAsset` failure. This keeps "cancelled" and "failed" distinguishable without conflating them.
- **No ADR is needed** for the first third-party library (task contract invariant, ТЗ §2): it is not a DI framework, changes no composition-root/lifetime rule `ADR-005` governs, and introduces no new module dependency `ADR-001` §5 tracks (it lives entirely inside the existing `Odyssey.Unity.Client` module). `ADR-011`'s own precedent (approving `Microsoft.Data.Sqlite`) shows a third-party dependency is recorded in `THIRD_PARTY_NOTICES.md` citing its approving task; the same pattern is used here.

### Findings (reported, not fixed -- out of scope)
- **`THIRD_PARTY_NOTICES.md` is outside the ТЗ's own §4 path list, but `AGENTS.md` line 218 explicitly requires updating it "for every accepted third-party dependency."** Updated it and disclosed here rather than silently either skipping it or silently expanding scope.
- Vendoring omits the upstream `Sample` folder and the Mac/Linux/WebGL platform files; if a future task needs those platforms, it adds the corresponding vendored files the same way this one did (fetch from the same upstream commit, keep the `.meta` GUIDs).
- The pool panel's items show only a thumbnail (no filename, no delete/rename) -- exactly the ТЗ's own non-goals; a future task can extend `AssetManifestEntryRecord` with an `OriginalFileName` field if that UX is wanted, without touching this task's own code.

### Blockers
None.
