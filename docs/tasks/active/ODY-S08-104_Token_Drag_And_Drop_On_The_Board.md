# ODY-S08-104 — Token Drag-and-Drop on the Board (SLICE-08 block 3b)

## 1. Task identity
`ODY-S08-104`; status: In Review (Draft PR, merge deferred to the product owner). Closes block 3b of `SLICE-08` (`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md`), split out of the original block 3 by `ODY-S08-103`; follows `ODY-S08-103` (blocks 3+4, merged).

## 2. Goal
Let a token be dragged with the mouse to a new position, as a faster alternative to the existing click-to-select/click-to-move flow, without breaking that flow and without interfering with the board's two other pointer-driven gestures (camera pan, asset-pool drag).

## 3. Authority
This task's governing ТЗ §0-§7; `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` block 3b (section 6 point 6/7, section 8); the established precedents this task explicitly reuses: `BoardPointerGesture`/`BoardCamera` (`ODY-S08-102`), `AssetPoolPresenter.OnItemPointerDown`'s pointer-capture shape (`ODY-S08-103`), `TryMoveSelectedTokenTo`'s error handling (`ODY-S08-101`/earlier).

## 4. In scope
`Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` only: each token captures its own pointer on `PointerDownEvent` and runs its own `BoardPointerGesture` instance; `PointerUp` selects (click) or commits via a new `TryMoveTokenTo` (drag); `PointerMove` updates only the in-memory visual preview; `PointerCaptureOutEvent` rolls back on an unsolicited capture loss. The now-unreliable `ClickEvent` handler is removed. Tests in `BoardScreenPresenterTests.cs`; `test-catalog.json`; the `SLICE-08` backlog (closing block 3b, and repairing a corruption disclosed in §18); this contract and plan.

## 5. Out of scope
`BoardPointerGesture.cs`/`BoardCamera.cs` (reused as-is, unmodified); `AssetPoolPresenter.cs`/`NativeFileDialog.cs`/`AssetTextureCache.cs` (untouched); `BoardMovementService.cs`/any backend/persistence code (only called, exactly as `TryMoveSelectedTokenTo` already did); token z-order/scale/selection (block 5); any ADR.

## 6. Domain / 7. Application / 8. Persistence
Unchanged. No domain, application, or persistence file is touched.

## 9. Tests and validation
`TC-BOARD-072`-`077` (see §3 of the ТЗ): click below threshold selects, no `MoveToken` call; drag above threshold commits exactly once (a `MoveCountingSceneRepository` decorator proves the count, not just the outcome); live preview during the drag updates the rendered element with zero `MoveToken` calls before `PointerUp`; an occupied destination (`BoardMovementService`'s own `BOARD-INV-009` check) is denied and the visual position rolls back to the last confirmed one (proven by re-reading the element's own `style.left`/`style.top` after the failed commit, not just the repository); dragging a never-selected token still commits; a full click-select-then-click-board-move regression run through the same wrapped repository proves the old flow's exact outcome (position, cleared selection, exactly one `MoveToken` call) is unchanged.

## 10-17. (see plan)
Unaffected: compatibility, security, observability, performance, dependencies, completion evidence follow the same shape as `ODY-S08-102`/`103`. Evidence recorded in §18 below and the PR description once validation finishes.

## 18. Change control

### Decisions made during execution
- **`ClickEvent` handler removed, not left dead.** Capturing the pointer on every token `PointerDownEvent` suppresses UI Toolkit's own `ClickEvent` for that gesture, so the handler would never fire correctly; keeping it would be misleading dead code. Selection now happens from `EndTokenDrag`/`OnTokenPointerUp` when the gesture reports a click.
- **Rollback on a failed/denied commit or an unsolicited capture loss reuses `Refresh()`, not a bespoke revert.** Nothing is ever persisted before `PointerUp`, so re-reading from the repository (exactly what `TryMoveSelectedTokenTo`'s own unconditional `Refresh()` already does on failure) restores the last confirmed position with no new mechanism.
- **`_draggingTokenId` is cleared *before* `ReleasePointer` is called in the real `PointerUp` handler**, specifically so that whatever `PointerCaptureOutEvent` results from that release (this codebase makes no assumption about whether Unity dispatches it synchronously) finds the flag already cleared and does nothing — only a capture loss that is *not* our own release is treated as unsolicited (task contract section 1.5).
- **New `TryMoveTokenTo(TokenId, TokenPosition)`** is a `TryMoveSelectedTokenTo`-symmetric public method that does not require or touch `_selectedTokenId` — dragging a token has never needed a prior selection, by the ТЗ's own explicit intent.

### Findings (reported, not fixed silently)
- **`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` was found corrupted on `main`**: a prior edit in this session's own `ODY-S08-103` work truncated it from 99 lines to 3 (a botched string-replace that was never caught before that PR merged). Restored from the last-known-good pre-`ODY-S08-103` revision (git history, commit `a288528`) and re-applied both the `ODY-S08-103` reformulation and this task's own block-3b closure cleanly, verifying the line count at each step this time. This is a real defect that reached `main`; disclosed explicitly rather than silently patched.

### Blockers
None known; full validation (Unity batchmode run, `dotnet test`, verify scripts) was in progress when this session's usage limit was reached — see the chat response for exact status.
