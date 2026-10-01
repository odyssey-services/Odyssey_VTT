# ODY-S11-218 — Token menu: object-specific verbs, nothing GM-sensitive on the first level (UI polish P1, item 8)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish P1 (items 6–11: `ODY-S11-216`…`ODY-S11-221`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (client UI only)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal (as requested)

1. Replace generic menu labels ("Edit", "Delete") with object-specific verbs where applicable.
2. Keep information the MainGM would not want shown to players during screen sharing (for example a true NPC name
   that differs from the player-visible one) off the menu's first level. This applies only if such a split exists in
   Odyssey's data model.

## 2. Verified current state

- **There is no context menu on the board.**
  - Right-click places the local player marker (`ODY-S08-107`).
  - A selected token shows the inspector panel `BoardTokenInspectorPresenter` (SLICE-10 Block 6 part 3).
  - A selected obstacle shows the obstacle inspector.
- **The labels are already object-specific**:
  - token inspector: "Apply Facing", "Apply Vision", "Check Cover", with fields "Facing", "FOV", "Range" and
    "Cover target";
  - obstacle inspector: "Apply Damage";
  - board tools: "Draw Wall" and the like.
  - No generic "Edit" or "Delete" exists. **Part 1: nothing to rename.**
- **No hidden-name mechanic exists.**
  - `TokenRecord` has no name field at all.
  - `CharacterRecord` has exactly one `DisplayName`; there is no true or player-visible split.
  - No other "GM-only" text field exists on tokens or characters.
  - **Part 2: not applicable. Odyssey has no hidden-name mechanic.** It was not invented here.
- **Hidden information that was on the first level (defect, fixed here).**
  - The token inspector's "Cover target" list was filled from **every** token on the board, including tokens hidden
    from a player by `ComputeVisibleTokens`.
  - A player who selected their own token therefore saw the ids and the count of tokens they cannot see.
  - "Check Cover" against such a token also returned positional information (`CoverSuggestionService` performs no
    visibility check).
  - This is exactly the "GM-hidden information on the first level" the item is about. It is a safe, local
    presentation fix, so it was made.

## 3. Scope

### In scope
- `BoardScreenPresenter.ShowTokenVisionInspectorIfSelected` offers only tokens in the local actor's visible set as
  cover targets. That set is filled by the render pass (`ODY-S11-214`); for the MainGM it is every token, as before.
- `BoardScreenPresenter.InspectorCoverTargets` and `BoardTokenInspectorPresenter.CoverTargetTokenIds` are exposed
  for tests.
- Test; test catalog.

### Out of scope
- Renaming labels (nothing generic exists), and inventing a hidden-name model.
- A server-side visibility check in `CoverSuggestionService`. That is backend work, off limits. It is a recorded
  follow-up: the client filter keeps the UI from offering hidden targets, but it is not an authorization boundary.
- Backend: untouched.

## 4. Tests and validation

| ID | Test |
|---|---|
| `TC-TOKENMENU-001` | `BoardScreenPresenterTokenVisionInspectorTests.CoverTargets_OfferOnlyTokensThePlayerCanSee_TheMainGmSeesAll` |

The test uses a real player member whose own token is at the origin:
- a token 3 units away is visible to the player;
- a token 500 units away, beyond the default 100-unit view distance, is hidden by the real `ComputeVisibleTokens`;
- the player is offered only the near token, and the MainGM's view of the same board offers both.

**Batchmode-verifiable:** the test above.

**Requires human visual confirmation in Play mode:**
1. As Player, select your own token.
2. The "Cover target" dropdown lists only tokens you can see on the map.
3. As MainGM it lists all tokens.

**Compilation and test status in this container** (no Unity or PowerShell):
- **Not compiled against real UnityEngine.**
- `BoardScreenPresenter.cs`, `BoardTokenInspectorPresenter.cs` and the test file: a stand-in compile reported **no
  error on any changed line**. A sanity check confirmed that this compile really binds the test bodies: a
  deliberately wrong member on a changed line was reported, then reverted.
- The test itself was **not run**: it needs Unity.
- `git diff origin/main -- Packages DotNet` is empty.

**Not run:** `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1`. Owner run pending.

## 5. Security, privacy, compatibility

- Fixes a hidden-information exposure in the UI (§2).
- The backend still answers cover queries for any token pair. That is the follow-up named above.
- No data or contract change. Rollback = revert the commit.
