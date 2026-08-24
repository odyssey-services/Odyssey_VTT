# ODY-S01-008 — Scene and Token Minimal Model Implementation

**Status:** In Progress  
**Owner:** Codex  
**Branch:** `feat/ody-s01-008-scene-token-minimal-model`  
**Pull request:** Not yet opened  
**Last updated:** 2026-08-24

## 1. Purpose and user-visible outcome

When this plan is complete, Odyssey VTT can create a scene, create and move tokens within it, list a scene's tokens, and register an imported asset — all through a new `ISceneRepository` Application port built on `ODY-S01-007`'s campaign storage foundation. No Board/Layer/Component/footprint/overlap model exists yet — those are later-slice scope.

## 2. Task contract

Governing task: `docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md`.

- Goal: implement `CreateScene`/`CreateToken`/`MoveToken`/`ListTokens`/`RegisterAsset` per that task's §1.
- Acceptance criteria: that task's §9, AC-1 through AC-9.
- Requirement IDs: `SLICE-01` (implementation), backlog `ODY-S01-008`.
- In scope: `SceneId`/`TokenId`/`AssetId` (Domain); `ISceneRepository`/records/failures (Application); `SqliteSceneRepository` (Persistence); new test file in the existing `Odyssey.Tests.Persistence` project; registry updates.
- Out of scope: full Scene/Board/Layer/SceneObject/Component model, footprint/overlap invariants, asset staging workflow, Domain Event Store, write-queue serialization.
- Required authorities: `SLICE-01_IMPLEMENTATION_BACKLOG.md` §5, `03_Domain_Model` §10.1/10.6/10.8, `ADR-011`, `ADR-001`, `ADR-004`, `ADR-008`.
- Required validation commands: `restore.ps1`, `verify-format.ps1`, `verify-test-structure.ps1`, `test-fast.ps1`, `check-repository-policy.ps1`, `verify-repository.ps1`.

## 3. Current state

### Verified facts

- `ODY-S01-007` merged into `main` (PR #28); `ICampaignRepository`/`SqliteCampaignRepository`/`IWallClock`-routing pattern all in place, 99/99 tests passing.
- No Scene/Token/Asset code existed before this task.

### Assumptions

- None.

## 4. Proposed approach

Reuse `ODY-S01-007`'s established patterns exactly: typed IDs following the `CampaignId`/`CampaignPublicId` `NewId(UtcInstant)` convention, an Application-layer port with plain data records, a Persistence-layer SQLite implementation taking `IWallClock` via constructor and routing every timestamp through it, and `PersistenceFailures`-style typed error factories. Keep the domain model deliberately minimal against `03_Domain_Model` §10's much richer Scene/SceneObject definition — record the narrowing explicitly rather than silently under-implementing it. Add tests to the existing `Odyssey.Tests.Persistence` project (no new `.csproj`, no new repository-policy guard to update, unlike `ODY-S01-007`).

## 5. Milestones

### M1 — Domain/Application/Persistence implementation

- [x] `SceneId`, `TokenId`, `AssetId` added to `Odyssey.Domain`, following the existing `NewId(UtcInstant)` pattern.
- [x] `ISceneRepository`/`TokenPosition`/`SceneRecord`/`TokenRecord`/`AssetManifestEntryRecord` added to `Odyssey.Application`.
- [x] Three new `PersistenceFailures` factories added alongside the existing four, no existing member changed.
- [x] `SqliteSceneRepository` implemented in `Odyssey.Persistence`, using `IWallClock` for every timestamp.
- [x] Three new `ErrorCode`s added and registered in `docs/errors/ERROR_CODES.md`.

### M2 — Tests and registry updates

- [x] Six tests added to the existing `Odyssey.Tests.Persistence` project (`TC-PERSIST-005`–`007`), all passing.
- [x] Three `TC-PERSIST-*` test case IDs registered in `Tests/Metadata/test-catalog.json`, cross-referenced from `ERROR_CODES.md`.
- [x] Full `dotnet test DotNet/Odyssey.Core.sln`: 105/105 passing.

### M3 — Validation and evidence

- [ ] All six required validation scripts (`restore.ps1` through `verify-repository.ps1`) pass with real recorded results.
- [ ] Diff-scope check confirms only the expected files changed.
- [ ] Draft PR opened; CI green on all required checks; remains Draft.

## 6. Progress log

- 2026-08-24 UTC - Confirmed `ODY-S01-007` merged into `main` (99/99 tests passing) and `ODY-S01-008`'s backlog row was `Draft`. Read `03_Domain_Model_Odyssey_VTT_v0.25.md` §10.1 (`Scene`), §10.6 (`SceneObject`), §10.8 (Token invariants) to establish the explicit scope-narrowing baseline. Added `SceneId`/`TokenId`/`AssetId` to `Odyssey.Domain`, `ISceneRepository`/records/three new `PersistenceFailures` factories to `Odyssey.Application`, and `SqliteSceneRepository` to `Odyssey.Persistence`, reusing `ODY-S01-007`'s `IWallClock`-routing and typed-error patterns exactly. Fixed two build issues along the way: `Convert.ToHexString` is unavailable on `netstandard2.1` (replaced with a manual hex-encoding helper, mirroring the fix already needed for `File.Move`'s 3-arg overload in `ODY-S01-007`); an unused/incorrect placeholder field left in the new test file during drafting was corrected to reuse the existing `SystemWallClock` test double. Added six tests to the existing `Odyssey.Tests.Persistence` project; full solution suite 105/105 passing on first successful build.

## 7. Decisions

- 2026-08-24 — Decision: `Token` position is a bare `(X, Y)` pair with no footprint/grid-snap/overlap enforcement, and `Scene`/`Token` carry only identity, name/position, status/revision, and timestamps — not the full `03_Domain_Model` §10.1/§10.6 field set (Board, Layers, Components, VisibilityPolicy, etc.). Rationale: `SLICE-01_IMPLEMENTATION_BACKLOG.md` §5's own task boundary text scopes this task to exactly "the minimal domain model roadmap §10.5 steps 3–5 require," and roadmap §10.5's steps never require footprint/grid/layer/visibility behavior — only that a scene exists and two tokens can be created and repositioned. Authority: `SLICE-01_IMPLEMENTATION_BACKLOG.md` §5; `17_Roadmap_Odyssey_VTT_v0.11.md` §10.5.
- 2026-08-24 — Decision: `RegisterAsset` copies directly into `Assets/Objects/`, bypassing the `Assets/Staging`/`Trash`/`Quarantine` workflow those directories (already created by `ODY-S01-007`) exist for. Rationale: this task's need is "one imported test map" for a single-user, no-failure-injection vertical-slice scenario; the full staging/quarantine workflow (partial-import recovery, virus-scan-style quarantine, etc.) is a materially larger feature with no roadmap §10.5 step requiring it yet. Authority: `SLICE-01_IMPLEMENTATION_BACKLOG.md` §5 ("asset manifest entry" as the only asset-related deliverable named); task's own explicit non-goal recorded rather than silently narrowed.
- 2026-08-24 — Decision: no repeat Unity/IL2CPP compatibility preflight was performed. Rationale: this task introduces no new native dependency, Unity plugin, or scripting-backend-sensitive code — it reuses `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` exactly as `ODY-S01-007` already proved compatible; re-running that same proof would be redundant, not more rigorous. Authority: `ODY-S01-007`'s own completed preflight evidence, unchanged dependency surface.

## 8. Discoveries and deviations

- `Convert.ToHexString` (used for the asset SHA-256 hash's lowercase hex encoding) is unavailable on `netstandard2.1`, the same category of BCL API-surface gap `ODY-S01-007` hit with `File.Move`'s 3-arg overload. Fixed with a small manual hex-encoding helper rather than reaching for a wider target-framework change.
- No deviation from the planned approach otherwise; the `ODY-S01-007` pattern reuse worked cleanly on the first build once the two BCL gaps above were fixed.

## 9. Validation and acceptance evidence

See the governing task's §17 for the authoritative record. Summary: `dotnet test DotNet/Odyssey.Core.sln` 105/105; remaining wrapped validation scripts to be recorded in full before this plan's M3 is checked off.

## 10. Recovery and rollback

Not applicable (no production campaign data exists yet). Revert this task's commits if needed; `ODY-S01-009` has not yet built on top of `ISceneRepository`/`SqliteSceneRepository`.

## 11. Open questions and blockers

- No blockers remaining.
- `campaign.lock`/write-queue serialization (§7 decision) remains open, deliberately deferred to `ODY-S01-009`, not resolved here.

## 12. Outcome and follow-up

Current outcome: Scene/Token creation, movement, listing, and asset registration implemented and passing 105/105 in the full solution test suite. Remaining before this plan's completion: full recorded validation-script run, diff-scope check, commit, push, and Draft PR.

Next action: complete §17's remaining validation rows, perform the diff-scope check, commit, push, and open a Draft PR — then `ODY-S01-009` (Saving Pipeline) may begin, depending on this task's Create/Move operations to persist through the Domain Event Store transactional pipeline.
