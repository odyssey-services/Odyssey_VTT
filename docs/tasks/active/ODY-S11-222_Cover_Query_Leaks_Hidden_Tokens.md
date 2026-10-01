# ODY-S11-222 — Backend answered cover queries for tokens hidden from the player (security fix)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 polish track; the only backend task in P0/P1/P2, authorised by the product owner  
**Owner:** Claude Code  
**Requested by:** Product owner (follow-up to the client-side fix in `ODY-S11-218`, commit `332860e`)  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract (one Application service and its request contract)  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal

`CoverSuggestionService.SuggestCover` answers a requester who is not the MainGm only about tokens that requester can
see. The MainGm is unrestricted, as before. A refusal must not leak through the error itself.

## 2. Defect (security)

- `SuggestCover(sceneRepo, obstacleRepo, request)` performed **no authorization**, documented as "by design".
- `SuggestCoverRequest` carried **no requesting user**.
- Anyone who knew or guessed a token id could therefore get a cover degree between any two tokens. A cover degree is
  information about where a hidden token is relative to the walls.
- The error path was an oracle too:
  - a missing id returned `persistence.token.not_found`;
  - a token on another Scene returned `cover_suggestion.attacker_and_target.not_in_same_scene`.
  - Together they disclosed existence and Scene placement.
- `ODY-S11-218` only stopped the client UI from offering hidden targets. It was not an authorization boundary.

## 3. Fix

- **Contract change.** `SuggestCoverRequest` now takes a `RequestingUserId`. `SuggestCover` takes the vision and
  campaign repositories it needs for the visibility rule. These are C# Application API signatures, with no persisted
  or network contract involved.
- **Rule** (reused, not invented):
  1. `CampaignMembershipAuthorization.IsMainGm(requester)`. If true, the old behaviour applies unchanged.
  2. Otherwise both tokens must be in `PlayerVisibilityService.ComputeVisibleTokens(requester)` for the attacker's
     Scene. That is the same live-visibility rule that already hides tokens from players on the board.
- **No oracle.** For a non-MainGm requester, a hidden token (as target **or** attacker), a missing token id and a
  target on another Scene all return **one identical error**:
  - code `cover_suggestion.token.unavailable`, category `NotFound`, `SafeReasonCode.TargetUnavailable`, one message
    key;
  - no ids, coordinates or other metadata.
  - Visibility is checked before the "different Scene" check, so that check is reachable only for tokens the
    requester already sees.
- **Errors passed through unchanged.**
  - A requester who is not a campaign member gets the existing membership or authorization error, which carries no
    token information.
  - Infrastructure failures (non-`NotFound`) keep their own errors.
- **Callers updated.**
  - `BoardScreenPresenter.TryCheckCover` passes `LocalActorUserId`. A refusal shows the generic "Cover check failed:
    TargetUnavailable".
  - `DotNet/Tests/.../CoverSuggestionServiceTests.cs`: the existing tests now run as the MainGm, with unchanged
    expectations.
  - `BoardScreenPresenterTokenVisionInspectorTests` (MainGm path).
- Error registry row in `docs/errors/ERROR_CODES.md`; test catalog.

### Not changed, recorded for review

- The attack pipeline computes cover itself (`SqliteAttackStateReader` → `CoreAttackRulesEvaluator`) and does not call
  `SuggestCover`. Whether resolving an attack against a target hidden from the attacker should be allowed is a
  combat-rules question beyond this task. It was not changed.

## 4. Tests and validation

| ID | Test (`DotNet/Tests/Odyssey.Tests.Persistence/Board/CoverSuggestionServiceTests.cs`) |
|---|---|
| `TC-COVERVIS-001` | `SuggestCover_PlayerAskingAboutAHiddenToken_IsRefused_WithTheSameErrorAsForAMissingOne` |
| `TC-COVERVIS-002` | `SuggestCover_MainGmAskingAboutTheSamePair_WorksAsBefore` |
| `TC-COVERVIS-003` | `SuggestCover_PlayerAskingAboutTokensTheyCanSee_GetsTheAnswer` |
| `TC-COVERVIS-004` | `SuggestCover_NonMemberRequester_IsRefused_WithoutAnyTokenInformation` |

The tests use a real Player member, real SQLite, and the real `ComputeVisibleTokens`:
- the player's own token is at the origin;
- a hidden token sits 500 units away, beyond the default 100-unit view distance, with a wall between.

**Run in this container** (real compile and run of the pure .NET backend; .NET SDK 10.0.112 instead of the pinned
10.0.302, in scratch projects that mirror `DotNet/Tests/*`):
- `Odyssey.Tests.Persistence`:
  - all 8 `CoverSuggestionService` tests pass, the 4 new and the 4 existing;
  - the full suite: **947 passed / 5 failed**.
  - The 5 failures are **identical on the unchanged baseline** (`git stash`: 943 passed / the same 5 failed). They
    are scratch-environment failures: source-scanning tests that expect the repository layout, and the kill/recovery
    harness executables, which are not built here. They are not caused by this change.
- `Odyssey.Tests.Domain` 90/90 and `Odyssey.Tests.Networking` 67/67 passed.
- `Odyssey.Tests.Unit` (14), `Architecture` (8) and `Contracts` (1): the failure sets are **identical before and
  after** this change. They are the same environmental class: tests that read repository files by path.
- Client: the SLICE-11/P0/P1 EditMode stand-in run, including the Phase 5 combat tests, **82/82 passed**.
  `BoardScreenPresenter.cs` and the inspector test: a stand-in compile reported no error on changed lines.
- **Client code not compiled against real UnityEngine.**

**Not run:** `test-unity.ps1`, `test-fast.ps1` (`TC-ARCH-001` + `dotnet test` with the pinned SDK in the real
layout), `verify-*.ps1` (including the `ERROR_CODES.md` registry gate). Owner run pending.

## 5. Security, privacy, compatibility

- **Security fix:** closes position and existence disclosure of tokens hidden from a player through the cover query.
- **Compatibility.** `SuggestCover` and `SuggestCoverRequest` signatures changed, and every in-repository caller is
  updated. No persisted data or network protocol is affected.
- Rollback = revert the commit, which reopens the leak.
