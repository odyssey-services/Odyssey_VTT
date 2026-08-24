# ODY-S01-008 — Scene and Token Minimal Model

**Status:** In Review  
**Roadmap stage / slice:** SLICE-01 (implementation)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-008-scene-token-minimal-model`  
**Pull request:** Draft — [#29](https://github.com/odyssey-services/Odyssey_VTT/pull/29) (open, awaiting owner review; all 4 required CI checks passed)  
**ExecPlan:** `docs/plans/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md`  
**Created:** 2026-08-24  
**Last updated:** 2026-08-24 UTC

## 1. Goal

Add the minimal domain model roadmap §10.5 steps 2–5 require: one `Scene` record, two `Token` records with position fields, and one registered asset manifest entry (for an imported test map) — reachable through a new `ISceneRepository` Application port implemented by `SqliteSceneRepository` in `Odyssey.Persistence`, built on `ODY-S01-007`'s `ICampaignRepository`/`CampaignHandle`.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-01_IMPLEMENTATION_BACKLOG.md` §5/§6 reserves `ODY-S01-008` as the second Campaign Storage child task; nothing in the repository yet creates a scene, a token, or registers an imported asset.
- Value or risk reduction: proves the minimal Scene/Token/Asset persistence primitives roadmap §10.5's vertical slice needs, without prematurely committing to the full `03_Domain_Model_Odyssey_VTT_v0.25.md` §10 Scene/Board/Layer/SceneObject/Component model, which belongs to later slices.
- Blocking or enabling relationship: blocks `ODY-S01-009` (Saving Pipeline, which persists the operations this task introduces) and `ODY-S01-013` (Vertical Slice Integration, which exercises this task's Create/Move/List operations end-to-end).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S01-008` boundary: "one `Scene` record, two `SceneObject`/token records with position fields, and one asset manifest entry... Does not implement combat, dice, character sheets, content systems, or any gameplay rule beyond position storage")
- `03_Domain_Model_Odyssey_VTT_v0.25.md` §10.1 (`Scene`), §10.6 (`SceneObject`), §10.8 (Token invariants) — private local reference; used only as the source of field names this task deliberately narrows from, not as a full-implementation requirement
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §4.2 (relative paths only — no absolute source path ever persisted), §7.1 (PRAGMA profile), §8.1 (hybrid current-state-table schema principle), §8.2 (`AssetManifestEntries` system table, already created by `ODY-S01-007`), §9.1 (identifier format)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §5/§6.5/§10 (repository interfaces as Application ports)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (typed `Result`/`Error`)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (wall-clock via `IWallClock`, not a direct global-clock call — the same discipline `ODY-S01-007` already established and this task must not regress)
- `docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md` — this task's direct predecessor; `ICampaignRepository`/`CampaignHandle`/`IWallClock`-routing/`PersistenceFailures` pattern reused, not reinvented

### Requirement and test IDs

- Requirement IDs: `SLICE-01` (implementation revision), backlog `ODY-S01-008`.
- Existing test IDs: None reused (builds on `ODY-S01-007`'s campaign fixtures but does not modify `TC-PERSIST-001`–`004`).
- New test IDs introduced: `TC-PERSIST-005` (scene creation, independent two-token creation/move/list), `TC-PERSIST-006` (typed not-found errors for scene/token), `TC-PERSIST-007` (asset registration: copy, hash, relative-path-only persistence, missing-source error) — registered in `Tests/Metadata/test-catalog.json` and `docs/errors/ERROR_CODES.md`.

### Task-safe private context

- Approved summary / references: `03_Domain_Model_Odyssey_VTT_v0.25.md` §10.1/§10.6/§10.8 field lists are summarized (not pasted beyond short customary phrases) to justify which fields this task deliberately excludes. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S01-007` (PR #28) is merged into `main`; `ICampaignRepository`, `SqliteCampaignRepository`, `CampaignHandle`, `PersistenceFailures`, and the `IWallClock`-routing pattern all exist and pass 99/99 tests, confirmed by `Read`/`dotnet test` before branching.
- `SLICE-01_IMPLEMENTATION_BACKLOG.md`'s `ODY-S01-008` row reads `Draft`, `ODY-S01-007` row reads `In Review` (merged, not yet formally closed — this task's dependency is satisfied by the merge itself, per the backlog's own dependency rule wording, not by a separate closure step; no such closure ТЗ has been issued for implementation tasks the way it was for ADR tasks).
- `AssetManifestEntries` system table already exists (created by `ODY-S01-007`'s `SqliteCampaignRepository.Create`), with columns `AssetId, RelativePath, Hash, SizeBytes` — this task's asset registration writes into that existing table, does not redefine it.
- No `Scene`, `Token`, `SceneId`, `TokenId`, `AssetId`, or `ISceneRepository` code existed on `main` prior to this task.
- `03_Domain_Model_Odyssey_VTT_v0.25.md` §10.1/§10.6 define a much richer `Scene`/`SceneObject` model (Board, LayerDefinitions, FogSettings, Components, etc.) than this task implements — confirmed by `Read`, used as the explicit basis for this task's scope-narrowing decision (§5).

### Assumptions

- None. All facts above were directly observed via `Read`/`dotnet test` on the current `main` branch before branching for this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `SceneId`, `TokenId`, `AssetId` typed identifiers (`NewId(UtcInstant)` pattern, matching `CampaignId`/`CampaignPublicId`).
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs` — `ISceneRepository` port, `TokenPosition`, `SceneRecord`, `TokenRecord`, `AssetManifestEntryRecord`.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — three new persistence `ErrorCode`s.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — three new `PersistenceFailures` factories (`SceneNotFound`, `SceneIoFailed`, `TokenNotFound`), added alongside the existing ones, no existing member changed.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` — the `ISceneRepository` implementation: `Scene`/`Token` current-state tables, `CreateScene`, `CreateToken`, `MoveToken`, `ListTokens`, `RegisterAsset` (file copy into `Assets/Objects`, SHA-256 hash, relative-path-only persistence).
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteSceneRepositoryTests.cs` (new test file in the existing test project — no new `.csproj`).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry entries for the three new `ErrorCode`s and three new `TC-PERSIST-*` test case IDs.
- `docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md` (this file), `docs/plans/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md` (governing ExecPlan).
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — `ODY-S01-008` row status only.

### Out of scope

- `Board`, `GridSettings`, `BackgroundSettings`, `LayerDefinition`, `VisibilityPolicy`, `PermissionOverrides`, `FogSettings`, `AudioBindings` (`03_Domain_Model` §10.1–10.5) — no Board/Layer/visibility model exists yet.
- `SceneObject`'s full field set and every non-`Token` `ObjectKind` (`Marker`, `Prop`, `WallSegment`, `Door`, `Window`, `Text`, `Drawing`, `AreaEffect`, `Portal`, `Interactive`) and every Component (`VisualComponent`, `TokenComponent`, obstacle components, `CoverComponent`, etc. — §10.6/§10.7) — this task's `Token` is a bare position record, not a `SceneObject` with components.
- Footprint/grid-snap/facing/overlap invariants (§10.8) — position is a free-form `(X, Y)` pair; "final position strictly centered on a cell" and "token overlap forbidden" are not enforced by this task.
- `SceneCommandHistory`/Undo-Redo (§10.11) — no compensating-command mechanism exists yet; that is `ADR-012`/`ODY-S01-009` territory.
- Full asset import pipeline: staging/quarantine workflow, thumbnails, duplicate-detection beyond a plain `File.Copy(overwrite: false)` failure — `RegisterAsset` copies directly into `Assets/Objects`, it does not implement the `Assets/Staging`/`Assets/Trash`/`Assets/Quarantine` workflow those directories (already created by `ODY-S01-007`) exist for.
- Domain Event Store / transactional journal-projection commit (`ADR-012` §5) — `ODY-S01-009` scope; this task's writes are plain current-state-table writes, matching how `ODY-S01-007`'s `Campaign` row is also written without event sourcing.
- `campaign.lock` / cross-repository write-queue serialization (`ADR-011` §7.2's single logical write queue per campaign, across `ICampaignRepository`/`ISceneRepository`) — each repository call opens its own short-lived connection; full serialization is deferred to `ODY-S01-009`, matching `ODY-S01-007`'s own recorded deferral of the same concern.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteSceneRepositoryTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/plans/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `ISceneRepository`/`SceneRecord`/`TokenRecord`/`AssetManifestEntryRecord` live in `Odyssey.Application` (port); `SqliteSceneRepository` lives in `Odyssey.Persistence` (implementation) — same split as `ICampaignRepository`/`SqliteCampaignRepository`.
- Authoritative-state and transaction boundary: each repository method commits its own single SQLite transaction (implicit auto-commit per statement/connection in this minimal design); no cross-method or cross-repository transaction spans yet — `ADR-012`'s journal↔projection rule does not apply since no `DomainEvents` are written by this task.
- Serialization / compatibility boundary: Not applicable — no JSON contract introduced by this task; `Scene`/`Token` rows are plain typed SQLite columns.
- Time / RNG rule: `ADR-008` — `SqliteSceneRepository` takes `IWallClock` via constructor, exactly like `SqliteCampaignRepository`; `SceneId`/`TokenId`/`AssetId.NewId()` are pure functions of an explicit `UtcInstant`, matching `CampaignId`/`CampaignPublicId`.
- Unity / thread / lifetime rule: each `ISceneRepository` method opens and disposes its own `SqliteConnection` within the method body; no connection is cached or exposed across calls (see §5 out-of-scope note on write-queue serialization).
- Dependency / licensing rule: no new dependency — reuses `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` already approved and referenced by `ODY-S01-007`.
- Security / privacy / redaction rule: `RegisterAsset` never persists the caller-supplied absolute source path — only the post-copy relative path under `Assets/Objects/` (`ADR-011` §4.2); `PersistenceFailures` errors never surface raw `SqliteException`/`IOException` text or local paths.
- Performance or platform constraint: Not applicable beyond what `ODY-S01-007` already established (Windows x64, IL2CPP-compatible dependency chain — no new native/plugin surface is introduced by this task, so no repeat IL2CPP preflight is required).
- Other: `Token` uniqueness/overlap/footprint invariants are explicitly not enforced (§5 out-of-scope) — this must not be silently claimed as "invariant-complete" anywhere in this task's documentation.

## 7. Expected behavior

### Scenario 1 — Create a scene

**Given** an open `CampaignHandle`  
**When** `ISceneRepository.CreateScene` is called with a valid name  
**Then** a new `Scene` row exists with `Status = "Draft"`, `Revision = 1`, and the returned `Result<SceneRecord>` carries a valid `SceneId`.

### Scenario 2 — Create and move two tokens independently

**Given** an existing scene  
**When** two tokens are created at distinct initial positions and one of them is moved  
**Then** each token has a distinct `TokenId`; the moved token's position and `Revision` update; `ListTokens` returns both tokens with their correct, independent positions.

### Scenario 3 — Not-found handling

**Given** a `SceneId`/`TokenId` that does not exist in the campaign  
**When** `CreateToken`/`MoveToken` is called with it  
**Then** the call fails with `persistence.scene.not_found`/`persistence.token.not_found` respectively — never a raw exception.

### Scenario 4 — Register an imported asset

**Given** a real source file on disk (simulating "import one test map")  
**When** `RegisterAsset` is called with that file's absolute path  
**Then** the file is copied into `Assets/Objects/` under the campaign root, its SHA-256 hash and size are computed, an `AssetManifestEntries` row is inserted, and the returned record's `RelativePath` never contains the original absolute source path.

### Required invariants

- No public `ISceneRepository` method ever throws a raw `SqliteException`/`IOException`/`UnauthorizedAccessException` to its caller.
- `SceneId`/`TokenId`/`AssetId` values are always well-formed per their own `TryParse` rules.
- The absolute source path passed to `RegisterAsset` never appears in the persisted `AssetManifestEntries.RelativePath` value or anywhere else this task writes to disk/database.

## 8. Deliverables

- Production code: `SceneId`/`TokenId`/`AssetId`/`NewId()` (Domain); `ISceneRepository`/`TokenPosition`/`SceneRecord`/`TokenRecord`/`AssetManifestEntryRecord`/three new `PersistenceFailures` factories (Application); `SqliteSceneRepository` (Persistence); three new `ErrorCodes` entries.
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/SqliteSceneRepositoryTests.cs` (6 tests, `TC-PERSIST-005`–`007`).
- Scripts / CI: None changed (no new project, no new guard to update — this task adds to the `Odyssey.Tests.Persistence` project `ODY-S01-007` already created).
- Configuration: None (no new `.csproj`).
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md` additions, `Tests/Metadata/test-catalog.json` additions, `SLICE-01_IMPLEMENTATION_BACKLOG.md` `ODY-S01-008` row status.
- Generated evidence or build artifacts: validation command output recorded in §17.
- Migration / recovery material: None (`Scene`/`Token` tables are newly introduced, not migrated from a prior version).

## 9. Acceptance criteria

1. `ISceneRepository.CreateScene` produces a `Scene` row at `Status = "Draft"`, `Revision = 1`, returned as a `Result<SceneRecord>` success.
2. Two tokens created in the same scene have distinct `TokenId`s; moving one updates only that token's position/`Revision`; `ListTokens` returns both with correct, independent positions.
3. `CreateToken` on a non-existent `SceneId` and `MoveToken` on a non-existent `TokenId` both return typed `NotFound` errors, never a raw exception.
4. `RegisterAsset` copies the source file into `Assets/Objects/`, computes a 64-character lowercase-hex SHA-256 hash and correct byte size, and the persisted `RelativePath` never contains the original absolute source path; a missing source file returns a typed error, not an exception.
5. No public `ISceneRepository` method leaks a raw provider exception; all failures surface as typed `Result`/`Error` per `ADR-004`.
6. `dotnet test DotNet/Odyssey.Core.sln` passes in full (all existing suites plus the six new tests in the existing `Odyssey.Tests.Persistence` project).
7. `.\scripts\restore.ps1`, `.\scripts\verify-format.ps1`, `.\scripts\verify-test-structure.ps1`, `.\scripts\test-fast.ps1`, `.\scripts\check-repository-policy.ps1`, and `.\scripts\verify-repository.ps1` all pass.
8. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
9. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-005` | `.NET / dotnet test` | Scene creation at Draft/revision 1; two independently created tokens move and persist independent positions | Pass |
| `TC-PERSIST-006` | `.NET / dotnet test` | `CreateToken`/`MoveToken` on non-existent scene/token return typed `NotFound` errors | Pass |
| `TC-PERSIST-007` | `.NET / dotnet test` | Asset registration: copy, hash/size, relative-path-only persistence; missing source file returns typed error | Pass |

### Required commands

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\test-fast.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-repository.ps1
```

### Manual validation

- `dotnet build`/`dotnet test DotNet/Odyssey.Core.sln` run directly, confirming all pre-existing suites (including `ODY-S01-007`'s `RepositoryStructurePassesArchitectureGuard`) remain green alongside the six new tests.
- Manual inspection confirming `SqliteSceneRepository` routes every timestamp through `IWallClock`, not a direct global-clock call (the same discipline the repository's forbidden-global-API scan already enforced on `ODY-S01-007`).

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no new native/plugin surface; `ODY-S01-007`'s IL2CPP preflight already covers the SQLite dependency chain this task reuses unchanged.
- Scripting backend: Not applicable (pure C#, no new backend-specific concern).
- Network topology or database fixture: Not applicable — local, temporary SQLite databases and files under the OS temp directory only.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- A repeat Unity/IL2CPP compatibility preflight — no new native dependency or Unity-side wiring is introduced; `ODY-S01-007`'s preflight evidence covers the unchanged SQLite dependency chain this task reuses.
- Full asset staging/quarantine workflow testing — out of scope per §5.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces the first `Scene`/`Token` tables (new, no prior version). No prior production consumer exists.
- Version fields affected: None — this task does not change `CampaignFormatVersion`/`DatabaseSchemaVersion`.
- Migration or upcaster: None; new tables, not a change to an existing shipped schema.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; no production campaign data exists yet that this task could affect.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | Reuses `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` already approved and referenced by `ODY-S01-007` | — | `ADR-011` v1.1 / `ODY-S01-007` |

## 13. Security, privacy, and hidden information

- Data classes handled: `SceneId`/`TokenId`/`AssetId`/token positions/asset file bytes and hashes — none classified as `Secret`/`HiddenGameplay` per `ADR-010` §10.
- Trust boundaries: local single-user filesystem/SQLite only, unchanged from `ODY-S01-007`.
- Authorization / audience checks: Not applicable — no permissions model exists at this stage.
- Redaction requirements: `RegisterAsset` never persists the absolute source path (`ADR-011` §4.2); `PersistenceFailures` errors never include raw local paths or provider exception text.
- Log-safe fields: None logged by this task's production code.
- Abuse / malformed input limits: `CreateScene` rejects empty/overlong (`>128` char) names; `RegisterAsset` requires the source file to actually exist before attempting any copy.
- Security tests: `TC-PERSIST-007` proves the absolute source path never leaks into the persisted relative path.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not inherited from `ODY-S01-007`. This task introduces new persisted schema (`Scene`, `Token` tables) and a new Application port (`ISceneRepository`) — a direct match to §1.2's "introduces or changes ... a schema" trigger, and to "affects authoritative state, persistence" since it is the second piece of code creating authoritative local campaign state. Unlike `ODY-S01-006` (the pure organizational scaffold, correctly Brief plan), this task changes production code and a persisted format, disqualifying Brief plan under `PLANS.md` §1.1's own criteria ("does not change ... a persisted format ... dependency graph"). It does not, however, carry `ODY-S01-007`'s additional platform-compatibility-spike complexity (no new native dependency, no repeat IL2CPP build required) — a materially smaller ExecPlan than `ODY-S01-007`'s, but still independently ExecPlan-eligible on the schema/persistence trigger alone.
- ExecPlan path: `docs/plans/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md`
- Expected pull request count: 1 (single Draft PR).
- Milestone or sequencing constraints: Must not begin before `ODY-S01-007` is merged into `main` (verified in §4). Blocks `ODY-S01-009` (Saving Pipeline) and `ODY-S01-013` (Vertical Slice Integration).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (`ODY-S01-008` row only).
- Documents that must not change: `ADR-011`–`014`, `docs/tasks/completed/*`, `docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces `Scene`/`Token` tables (new, first commit of these tables) — not a change to a previously shipped version.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` — `SceneId`, `TokenId`, `AssetId` added.
- `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — three new `PersistenceFailures` factories added.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — three new codes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteSceneRepositoryTests.cs` — new (6 tests).
- `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json` — registry additions.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — `ODY-S01-008` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 105/105 total (1 Contracts + 1 Domain + 17 Persistence + 84 Unit + 2 Architecture), 0 failed. |
| `.\scripts\restore.ps1` | Passed | All 10 `.csproj` restored, including `Odyssey.Persistence`/`Odyssey.Tests.Persistence`. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, `TC-ARCH-002 PASS` (all four controlled-invalid fixtures). |
| `.\scripts\test-fast.ps1` | Passed | All five `dotnet test` suites green via the wrapped fast-CI path, 105 total. |
| `.\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-005 PASS` (registry complete, including the three new codes and `TC-PERSIST-005`–`007` references). |
| `.\scripts\verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS repository checks passed`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-PERSIST-005` (`CreateScene_ReturnsDraftScene_AtRevisionOne`). |
| AC-2 | Passed | `TC-PERSIST-005` (`CreateTwoTokens_ThenMoveThem_PersistsIndependentPositions`). |
| AC-3 | Passed | `TC-PERSIST-006`. |
| AC-4 | Passed | `TC-PERSIST-007`. |
| AC-5 | Passed | `TC-PERSIST-006`/`007` (typed errors, no raw exceptions observed). |
| AC-6 | Passed | Full `dotnet test` 105/105. |
| AC-7 | Passed | All six required validation scripts passed (see Validation results above). |
| AC-8 | Passed | `git diff --cached --name-status` against `main` at commit time showed exactly the 11 files in §5's Allowed paths. |
| AC-9 | Passed | PR #29 opened as Draft; all 4 required CI checks passed (`buildidentity-provenance`, `dotnet-restore-build-test`, `repository-policy-format-structure`, `unity-project-package-static` — https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32731856998); open, awaiting owner review. |

## 18. Blockers, risks, and open decisions

- Blocker: none. `ODY-S01-007` is merged into `main`, confirmed in §4.
- Open decision (deliberate, not a blocker): `campaign.lock`/write-queue serialization across `ICampaignRepository`/`ISceneRepository` remains deferred, consistent with `ODY-S01-007`'s own recorded deferral of the same concern — not resolved here.
- Risk: `Token`'s bare `(X, Y)` position, with no footprint/grid-snap/overlap enforcement, is intentionally provisional; a future slice implementing the full `SceneObject`/`TokenComponent` model (§10.6–10.8) may need to `ALTER TABLE Token` or migrate this data — expected and acceptable per this task's own explicit scope narrowing, not a defect.
