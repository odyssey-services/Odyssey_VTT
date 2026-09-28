# ODY-S08-106 — Token Multi-Selection (SLICE-08 block 5b, last block)

## 1. Task identity
`ODY-S08-106`; status: In Review (Draft PR, merge deferred to the product owner). Closes block 5b of `SLICE-08` (`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md`) — the last block; the track is fully delivered with it.

## 2. Goal
Let several tokens be selected at once (session-only) and dragged together, without changing single-token click/select/drag/move, z-order, scale, or camera behaviour.

## 3. Authority
This task's governing ТЗ; backlog block 5b; precedents `ODY-S08-104` (token pointer gesture, preview-then-commit) and `ODY-S08-105` (modifier read on the wheel).

## 4. In scope
`Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` only:
- `_selectedTokenId` → `_selectedTokenIds` (set, session-only, never persisted). `SelectedTokenId` stays as a read-only property (the single selected token, else `null`); `SelectedTokenIds` added. The selected border in `RenderTokens` now tests set membership; the style is unchanged.
- Plain click (`SelectToken`): replaces the selection; on the only selected token it clears it (unchanged toggle). Ctrl+click (`ToggleTokenSelection`): adds the token or removes exactly it. The Ctrl state is read from `PointerDownEvent.ctrlKey` in `OnTokenPointerDown`, passed to `BeginTokenDrag(..., ctrl)` (optional parameter, existing callers unchanged) and used at pointer-up.
- Click on empty board: 2+ selected → clear selection, no move; exactly 1 → `TryMoveSelectedTokenTo` (unchanged); 0 → unchanged.
- Drag: a token that belongs to a selection of 2+ drags the whole group by the same world delta (grabbed token follows the pointer exactly as before; the others shift by its delta), preview only until pointer-up; a token outside the selection becomes the selection when the drag starts (in place, no `Refresh` under the pointer capture) and drags alone; a Ctrl-drag never edits the selection.
- Commit: one `TryMoveTokenTo` (hence one `BoardMovementService.MoveToken`) per group member, destinations computed before the first commit.

## 5. Out of scope
Group move by clicking a destination point; marquee (rubber-band) selection; Shift-range selection; persisting the selection; an atomic/batch move API; `BoardMovementService.cs`, `BoardCamera.cs`, `BoardPointerGesture.cs`, `AssetPoolPresenter.cs`, `NativeFileDialog.cs`, `AssetTextureCache.cs`, any backend/persistence code or schema, `.asmdef`/`.csproj`, verify scripts, every ADR.

## 6. Domain / 7. Application / 8. Persistence
Unchanged. No domain, application or persistence file is touched.

## 9. Tests and validation
`TC-BOARD-086` (Ctrl+click adds), `087` (Ctrl+click removes exactly one), `088` (plain click replaces; sole-selected toggle), `089` (empty board with 2+ selected clears, no move), `090` (empty board with 1 selected still moves — regression), `091` (group drag: same delta, no `MoveToken` before release, two calls after, unselected token unmoved), `092` (drag outside the selection replaces it and moves only that token), `093` (partial group failure: one member accepted and committed, one denied and rolled back, in the same test). Regression: `TC-BOARD-072`-`085` (ODY-S08-104/105) unchanged and passing. Add/remove/replace are three separate tests, not one.

## 10-17. (see plan)
Compatibility, security, observability, performance, dependencies and completion evidence follow the same shape as `ODY-S08-102`-`105`.

## 18. Change control

### Decisions made during execution
- **Group commit is NOT atomic (accepted, disclosed limitation).** `BoardMovementService` has no batch API and adding one is out of scope, so a group is N ordinary single moves. If one is denied (e.g. its destination is occupied) and others are accepted, the result is partial: accepted tokens stay moved and committed, and no undo of them is attempted. The denied token is rolled back visually by the `Refresh()` that each `TryMoveTokenTo` already performs, because nothing was persisted for it. The status line reports "Moved k of n tokens.".
- **Order dependence (also disclosed).** Members are committed one after another, in selection order. A member whose destination is currently held by another member that has not moved yet is denied by the occupancy rule; this is a consequence of non-atomic sequential commits, not handled specially.
- **Selection replacement on drag happens when the drag actually starts,** not at pointer-down: replacing it at pointer-down would turn the later click-release into a toggle-off of the token that was just selected.
- **The grabbed token lands exactly on the pointer,** as in ODY-S08-104 (single drag path is bit-for-bit the old destination); the other members move by (pointer world position − grabbed token's start position).
- **`SelectedTokenId` is kept** (returns the token only when exactly one is selected), so all pre-existing tests and callers compile and mean the same thing; `SelectedTokenIds` is the new full view.
- **Ctrl was chosen over Shift** because Shift already scales a token on the wheel (`ODY-S08-105`) and Ctrl+click is the desktop convention for add/remove.
- The z-order raise of `ODY-S08-105` still happens on pointer-down for the grabbed token only; group members are not raised.

### Findings (reported, not fixed silently)
- Real UI Toolkit wiring (`evt.ctrlKey` in `OnTokenPointerDown`) is not simulated in tests — the accepted precedent of `ODY-S08-102`-`105`; the tests drive the same public entry points with the `ctrl` argument.
- The selection is not pruned when a selected token disappears from the scene; it is session-only and a stale id simply never matches a rendered token.

### Blockers
None.
