# Odyssey VTT — SLICE-08 Owlbear-Style Board UX Implementation Backlog

**Status:** Implementation revision — OPEN. Block 1 (asset content read API + minimal texture rendering) is In Review (`ODY-S08-101`); four blocks remain named, not scoped (camera pan/zoom, drag-and-drop, file picker, token z-order/scale/selection).
**Slice:** `SLICE-08 — Owlbear-style board UX: making the already-built scene-background and token-portrait backend visible and interactive on the board (asset content read + texture rendering first, then camera, drag-and-drop, file picker, and token z-order/scale/selection)`
**Parent task:** `docs/tasks/active/ODY-S08-101_Asset_Content_Read_And_Texture_Rendering.md`
**Predecessor backlog:** `docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md` (backend groundwork: `SceneRecord.BackgroundAssetId`, `TokenRecord.PortraitAssetId`, `RegisterAsset`/`AssetManifestEntries`/`AssetReferences` -- all merged into `main`) and `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (the trial `BoardScreenPresenter` this slice extends). Neither is reopened; both are reused as fixed prerequisite infrastructure. There is no separate `SLICE-08_BACKLOG.md` prerequisite document -- see section 1's own explicit process simplification.
**ExecPlan:** Not required for this document itself (brief plan for the backlog document; each child task chooses its own planning mode).
**Created:** 2026-09-23
**Last updated:** 2026-09-23 UTC -- created by `ODY-S08-101`, which is row 1 and is moved to `In Review` by the same change.

## 1. Purpose and explicit process simplification

The product owner set a new product orientation: a board experience in the spirit of Owlbear Rules -- an infinite board with a draggable map and draggable tokens. An independent code audit on 2026-09-23 confirmed that the backend layer for this is ready (`SceneRecord.BackgroundAssetId`, `TokenRecord.PortraitAssetId`, `RegisterAsset`/`AssetManifestEntries`/`AssetReferences`, all delivered by `SLICE-07`) and that the UI layer is not: `BoardScreenPresenter` draws tokens as solid colored squares and the board as a solid fill -- neither a background nor a portrait is ever shown as an image. The audit also found the first blocker for any image rendering: `RegisterAsset` has no paired read operation. `AssetManifestEntries` stores only `AssetId`/`RelativePath`/`Hash`/`SizeBytes`, and nothing in `ISceneRepository` or anywhere else in the codebase returns an asset's bytes. This backlog decomposes the path from that state to the intended experience into five ordered blocks, each its own task contract and pull request.

**Explicit simplification, following `ODY-S07-101`'s own precedent** (which created `SLICE-07`'s backlog and resolved its first block in the same task): this slice does not use `SLICE-05`'s two-phase process (a separate prerequisite backlog proposing an ADR, a distinct approval cycle, only then an implementation backlog). `ODY-S08-101`'s governing ТЗ explicitly authorizes creating this backlog in the same task/PR as the first block's implementation. **There is no separate `SLICE-08_BACKLOG.md` prerequisite document, and none is planned** -- this file is the only backlog document for this slice.

This backlog does **not** itself implement anything. It decomposes the slice into ordered child tasks, each its own separate task contract and pull request, activated one at a time. Only block 1 is detailed (as `ODY-S08-101`); blocks 2-5 are named and reserved.

Its sources of scope are, exclusively:

- The product owner's orientation toward an Owlbear-Rules-style board, treated as a UX direction, not a technical specification (section 6 point 4).
- The independent code audit of 2026-09-23 described above.
- Every already-merged `SLICE-07` and `SLICE-UI-01` deliverable, reused as fixed, unmodified prerequisite infrastructure -- this backlog does not reopen any of it.

No child task in this backlog reopens any decision `ADR-001`-`ADR-031` already made.

## 2. Exit criteria for this revision

- This backlog exists, with all five blocks (section 3) named and reserved -- only block 1 detailed, the rest not scoped, per the same "named, not scoped" convention `SLICE-05`/`SLICE-06`/`SLICE-07` section 8 already established.
- The block order and its justification are recorded (section 6), including the one place it deliberately differs from the product owner's natural reading order.
- Block 1 is decomposed into its own numbered task (`ODY-S08-101`, section 9).

## 3. Roadmap block mapping

The product-owner direction decomposes into five blocks.

| Order | Block | Status |
|---:|---|---|
| 1 | Asset content read API + minimal texture rendering (scene background and token portraits drawn as images, no camera/pan/zoom -- the same fixed transform as today) | In Review (`ODY-S08-101`, section 9 row 1) |
| 2 | Camera: pan/zoom -- replace the fixed `OriginOffsetPixels`/`PixelsPerUnit` with a real viewport controlled by mouse/touch | Named, reserved (section 8) |
| 3 | Drag-and-drop -- dragging a token across the board with the mouse; dragging a file from the OS onto the board (for a background/portrait) | Named, reserved (section 8) |
| 4 | File picker -- loading an asset from the user's disk through the UI (`RegisterAsset` is today called only programmatically and from tests) | Named, reserved (section 8) |
| 5 | Token z-order, token scale, persistent selection/multi-selection (`TokenRecord` carries none of these fields today) | Named, reserved (section 8) |

## 4. Global non-goals (this revision)

- Implementing any of blocks 2-5 -- named and reserved only, per section 3.
- A camera, drag-and-drop, file picker, token z-order/scale, or persistent selection inside block 1 -- each is its own later block.
- A GameObject/`SpriteRenderer`-based board. The UI Toolkit `VisualElement` approach `BoardScreenPresenter` already uses (and `ADR-001` section 6.7 names) stays; changing rendering technology is not part of this slice.
- Grid, hex-grid, board-geometry or visibility/fog rules -- `ADR-020` (Board Geometry And Movement Determinism) is a separate, not-yet-started track independent of this slice, and no block here implements or contradicts it.
- Reopening any `SLICE-05`/`SLICE-06`/`SLICE-07` decision, or any ADR `ADR-001`-`ADR-031`.
- Asynchronous/streaming asset loading. The project's persistence layer is synchronous throughout; small images are the only current scenario.

## 5. No new prerequisite ADR-proposal backlog needed

`SLICE-08` opens directly with this single implementation backlog because none of its five blocks raises an architectural question of the size an ADR resolves -- each extends an already-accepted mechanism (`ISceneRepository`/`AssetManifestEntries` for block 1; the existing UI Toolkit presenter for blocks 2-5). If a later block does surface such a question, that task reports it as a finding rather than creating an ADR silently, per the convention `ODY-S07-103` established. This section exists only to make the absence of a separate prerequisite backlog a conscious, documented choice.

## 6. Scope decisions requiring explicit justification

1. **Block 1 is the mandatory first step.** Without a way to read an asset's bytes and turn them into a texture, no image can be rendered at all; every later block builds visible behavior on top of it.
2. **Blocks 2 and 3 are deliberately swapped relative to the product owner's natural reading order (drag-and-drop before camera).** Dragging a token across the board is meaningless without a working camera coordinate system: a drag gesture must translate screen coordinates into world coordinates, and today's world/screen mapping is a pair of hard-coded constants. Building drag first would hard-code it against the fixed transform and force a rewrite when the camera lands. This is an engineering decision about complexity and dependencies, not a product decision; the product owner is free to reorder.
3. **Block 1 uses the existing UI Toolkit presenter and existing fixed transform, and adds no camera.** Setting a `Texture2D` as an element's `style.backgroundImage` is the minimal change that makes the already-stored `BackgroundAssetId`/`PortraitAssetId` visible, and it keeps the click-select/click-to-move flow (`TryMoveSelectedTokenTo`/`BoardMovementService.MoveToken`) untouched.
4. **The Owlbear Rules orientation is recorded as a UX direction and an inspiration source only, not a binding technical specification.** As with the body-part-modularity reference `SLICE-07` recorded, this codebase's own accepted contracts remain the actual starting point; an external product is a suggestion, not a specification.
5. **A pre-existing blocker was found while implementing block 1 and is recorded here because it gates the whole slice.** The Unity project on `main` does not compile: `Odyssey.Persistence`'s Unity `.asmdef` (and the dependency matrix in `scripts/verify-test-structure.ps1`) omit `Odyssey.Rules`, yet `SqliteCharacterRepository.cs` uses `Odyssey.Rules.Character.*` directly; `dotnet` hides this through transitive project references, and CI's Unity job does not run the Editor. `ODY-S08-101` did not fix it (an architectural decision, outside its scope) and reports it in its task contract section 18. Resolving it -- by amending the `ADR-001` matrix or by refactoring the persistence layer -- is a prerequisite for actually running any block of this slice in the Unity client, and is for the product owner to schedule; it is deliberately not one of the five blocks. The investigation is complete: `docs/research/Unity_Rules_Asmdef_Break_Investigation.md` (root cause bisected to `ODY-S04-105`/PR #89, both fix candidates tested in Unity, recommendation and scope estimates); scheduling the fix remains the product owner's decision.

## 7. Dependency rules

- `ODY-S08-101` depends on `SLICE-07`'s merged asset/scene/token backend (`ODY-S07-105`, `ODY-S07-106`) and on `SLICE-UI-01`'s trial `BoardScreenPresenter`. It has no dependency inside this slice -- it is the first task.
- Block 3 (drag-and-drop) depends on block 2 (camera): see section 6 point 2.
- Block 1 does not depend on blocks 2-5, and none of blocks 2, 4, 5 is known to depend on block 1's texture rendering beyond building on the same presenter; this backlog does not commit to a relative order among 4 and 5 beyond what section 3 records.

## 8. Reserved future blocks (named, not scoped)

- **Block 1 -- Asset content read API + minimal texture rendering.** In Review (`ODY-S08-101`, section 9 row 1). Adds the missing read side of the asset registry (`ISceneRepository.ReadAssetContent`, fail-closed by campaign and existence, SHA-256 integrity check, typed errors), a `GetScene` read that the presenter needs to see `BackgroundAssetId`, and minimal texture rendering in `BoardScreenPresenter` for the scene background and token portraits with a lifetime texture cache and fallback to today's solid visuals when no asset is set. No camera, drag-and-drop, file picker, or token z-order/scale/selection.
- **Block 2 -- Camera: pan/zoom.** Named by this task; not scoped. A future decomposition task replaces `BoardScreenPresenter`'s fixed `OriginOffsetPixels`/`PixelsPerUnit` with a real viewport controlled by mouse/touch, and is the prerequisite of block 3.
- **Block 3 -- Drag-and-drop.** Named by this task; not scoped. A future decomposition task adds mouse dragging of a token across the board and dragging a file from the OS onto the board for a background/portrait. Depends on block 2.
- **Block 4 -- File picker.** Named by this task; not scoped. A future decomposition task adds loading an asset from the user's disk through the UI; `RegisterAsset` is today called only programmatically and from tests, with no UI caller.
- **Block 5 -- Token z-order, scale, persistent selection.** Named by this task; not scoped. `TokenRecord` carries no such fields today; a future decomposition task adds them.

None of blocks 2-5 is decomposed into a numbered implementation task by this revision -- decomposing any of them is a future backlog revision, per the same "named, not scoped" rule the earlier slice backlogs established.

## 9. Ordered backlog

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S08-101` | In Review | Product-owner Owlbear-style board orientation + independent code audit of 2026-09-23 (block 1, section 3) | SLICE-08 Backlog + Asset Content Read API + Minimal Texture Rendering | `SLICE-07` (`ODY-S07-105`/`106`), `SLICE-UI-01` | ExecPlan | Creates this backlog. Adds `ISceneRepository.ReadAssetContent` (whole file as `byte[]`; fail-closed against this campaign's own `AssetManifestEntries` -- a single lookup, since each campaign is a separate database file; SHA-256 recomputed and compared with the stored hash; a manifest path that escapes `Assets/Objects` is rejected) with typed errors: reused `AssetNotFound`, new `AssetFileMissing` and `AssetIntegrityFailed`. Adds `ISceneRepository.GetScene` (a second, read-only method beyond the single one the ТЗ named -- forced, because the presenter otherwise cannot read `BackgroundAssetId` at all; disclosed in the task contract). `BoardScreenPresenter` renders the scene background and token portraits as `Texture2D` `style.backgroundImage` through a per-presenter texture cache (one disk read per `AssetId` for the presenter's lifetime), with fallback to the solid fill/ownership-colored square when no asset is set, and fallback plus a reported typed error when an asset cannot be loaded. Click-select/click-to-move, the fixed coordinate transform, and every existing signature are unchanged. |

## 10. Backlog change control

- New work requires a task contract; this document reserves `ODY-S08-101` for the task that created it, and will reserve further numbers as each of blocks 2-5 (section 3/8) is activated for detailed decomposition, following the numbering convention `SLICE-05`/`SLICE-06`/`SLICE-07` already established.
- Blocks 2-5 (section 8) are named, not scoped -- decomposing any of them into real task IDs is a future backlog revision, not an implicit extension of this one.
- A task may be split before implementation by updating this backlog, following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- The real pull-request number for `ODY-S08-101` is recorded in section 9 row 1 after merge, by the housekeeping convention earlier slices used.
- This backlog does not replace any task's own acceptance criteria and does not itself decide any technical question beyond the scope decisions in section 6.
- If section 6's reasoning is later found incorrect, that is a new task/backlog-revision decision, not a silent edit -- this document would gain an explicit amendment note, not a rewritten section 6, mirroring the earlier slices' convention.
