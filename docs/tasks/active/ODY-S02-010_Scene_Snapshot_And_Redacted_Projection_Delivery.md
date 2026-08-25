# ODY-S02-010 — Scene Snapshot & Redacted Projection Delivery

**Status:** In Review
**Roadmap stage / slice:** SLICE-02, roadmap §11.6 step 4
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-010-scene-snapshot-and-redacted-projection-delivery`
**Pull request:** Draft — [#47](https://github.com/odyssey-services/Odyssey_VTT/pull/47)
**ExecPlan:** `docs/plans/active/ODY-S02-010_Scene_Snapshot_And_Redacted_Projection_Delivery.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Deliver, over `InProcessSessionTransport`, a `ProjectionSnapshot` for a newly admitted, role-assigned actor (`ODY-S02-009`) that contains exactly the scene entities their `ADR-019` baseline role permits — a real `Odyssey.Application`-layer `VisibilityPolicy`/`ProjectionSnapshot` implementation, not a reuse of `ODY-S02-007`'s test-only harness types.

## 2. Why this task exists

- Problem or dependency being addressed: roadmap §11.6 step 4 ("player получает разрешённую сцену") has no production implementation yet — only `ODY-S02-007`'s harness proved the underlying `ADR-017`/`ADR-019` contract is implementable.
- Value or risk reduction: proves the redaction pipeline works end-to-end over a real (in-process) transport before `ODY-S02-011` builds command/delta handling on top of it.
- Blocking or enabling relationship: depends on `ODY-S02-009` (Membership/role must exist before a scene can be addressed to an actor); blocks `ODY-S02-011` (commands act on scene state that must already be deliverable).

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §4 (`ProjectionSnapshot` identity), §7 (redaction always computed from current state, principle reused here), §11 (module boundaries), §12 (explicitly out of scope), §14 (implementation DoD, scoped to this task's portion only — delta/reconnect items are `ODY-S02-011`/`012`)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §3.3–3.5 (`PermissionDecision`/`VisibilityPolicy`/`ClientProjection` terms), §5 (three baseline roles), §6.2 (read/visibility check point), §7 (redacted scene projection pipeline), §11 (module boundaries), §12 (rules for Codex), §13 (implementation DoD)
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6 (`Odyssey.Networking` makes no permission decision)
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` §3 (hand-written canonical JSON codecs)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`Result`/`Error`/`SafeReasonCode`)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1/§6.1 (`ISessionTransport`, `NetworkEnvelope`)
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S02-010` boundary, fixed, not redefined here), §2.3 (`ODY-S02-007` harness reuse prohibition)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-010`.
- Existing test IDs: `None` — this is the first `ProjectionSnapshot`/`VisibilityPolicy` production implementation.
- New test IDs to introduce: `TC-NET-012`, `TC-NET-013`, `TC-NET-014` (registered in `Tests/Metadata/test-catalog.json`), plus additional pure-logic tests not individually catalog-registered (see §10).

### Task-safe private context

- Approved summary / references: roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 step 4 ("player получает разрешённую сцену") — private local reference, summarized only, not pasted verbatim.

## 4. Verified current state

### Verified facts

- `ODY-S02-009` is merged to `main` (`e4aa99c`): `SessionAdmissionService`/`SessionAdmissionState`/`SessionMember`/`BaselineRole` exist and are real, tested over `InProcessSessionTransport`.
- No `ProjectionSnapshot`/`VisibilityPolicy`/scene-content production code exists anywhere in the repository prior to this task — verified by `Grep` across `Packages/com.odyssey.application` and `Packages/com.odyssey.networking`.
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/*.cs` is explicitly test-project-scoped (its own README: "Not production code... no future implementation task may `ProjectReference` or otherwise depend on this project").
- `JsonObjectReader` (`Odyssey.Application.Serialization`) is a flat key-value reader with no array support; `ManifestAndDiagnosticCodecs.cs`'s `LogEventV1JsonCodec` already establishes a hand-rolled `JsonTextWriter`/`JsonTextReader` array-walk pattern for exactly this need (`safeProperties`).
- `ISessionTransport.SendReliableAsync`/`DrainReliable` and `NetworkEnvelope` (`SessionTransportContracts.cs`) are unchanged since `ODY-S02-001`/`009` and reused directly, with the same constructor signatures.

### Assumptions

- `None`.

## 5. Scope

### In scope

- `Odyssey.Application.Networking.Projection`: `SceneEntity`, `Scene`, `ActorVisibilityContext`, `VisibilityPolicy`, `ProjectionSnapshot`, `SceneProjectionBuilder`, `ProjectionSnapshotWireCodec`.
- `Odyssey.Networking.Projection`: `SceneProjectionHostChannel.SendSnapshotAsync`, `SceneProjectionClientChannel.DrainSnapshots`.
- Tests proving: MainGM sees the full scene (control case); a newly admitted Observer's snapshot is redacted (no hidden entities); a Player sees their own assigned hidden entity but not another's; repeated snapshot builds/deliveries for unchanged state carry the same `PayloadHash`.
- `Tests/Metadata/test-catalog.json` registration for the three required transport-level scenarios.
- This task contract, its ExecPlan, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-010` row.

### Out of scope

- `ProjectionDeltaBatch`, gap detection, dedup, delta-buffer (`ADR-017` §5–§8) — `ODY-S02-011`.
- Reconnect flow, permission-recheck-at-reconnect mechanics, snapshot fallback selection (`ADR-017` §9) — `ODY-S02-012`.
- Command/action authorization (`PermissionDecision` as "may Actor perform this command", `ADR-019` §3.3/§6.1) — `ODY-S02-011`.
- Any reuse of `ODY-S02-007`'s (SP-04) harness types as production code.
- Any full game-content/persistence model (characters, ownership/control transfer, campaign storage) — `SLICE-03`+.
- Editing `ADR-015`–`019` or `ODY-S02-009`'s own code (only its public API is consumed).

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Networking/Projection/
Packages/com.odyssey.networking/Runtime/Projection/
DotNet/Tests/Odyssey.Tests.Networking/SceneProjection/
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S02-010_Scene_Snapshot_And_Redacted_Projection_Delivery.md
docs/plans/active/ODY-S02-010_Scene_Snapshot_And_Redacted_Projection_Delivery.md
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` declares/computes the redacted `ProjectionSnapshot`; `Odyssey.Networking` only transports the already-built payload (`ADR-001` §6.6, `ADR-017` §11, `ADR-019` §6.2/§11/§12).
- Authoritative-state and transaction boundary: a single in-memory authoritative `Scene` per session, not a per-connection copy (`ADR-019` §7, rejecting §14.2's alternative).
- Serialization / compatibility boundary: hand-written canonical JSON via `CanonicalJsonWriter`/hand-rolled `JsonTextWriter` array walk, not `System.Text.Json` (`ADR-003` §3).
- Time / RNG rule: `SnapshotId` uses `Guid.NewGuid()` directly (local opaque identifier exemption, not `ADR-008`'s gameplay RNG stream) — same precedent as `ODY-S02-009`'s `JoinCode`/`ODY-S01-007`'s `CampaignId`.
- Unity / thread / lifetime rule: Not applicable — pure .NET Core code, no Unity API usage.
- Dependency / licensing rule: no new dependency introduced.
- Security / privacy / redaction rule: redaction computed entirely in `Odyssey.Application`, before any payload reaches `Odyssey.Networking` (`ADR-019` §6.2, §12 rule 3); no new `SafeReasonCode`/`ErrorCode` needed since this task's only failure path is malformed wire payload (already-existing `SerializationFailures`).
- Performance or platform constraint: Not applicable at this scale (in-memory, `InProcessSessionTransport` only).
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Observer receives a redacted scene

**Given** a session created by a host and a Player admitted by join code (default `Observer` role, `ODY-S02-009`), and a scene with one `Public` entity and two `HiddenGameplay` entities (one unassigned, one assigned to the Player)
**When** the host builds and sends a `ProjectionSnapshot` for that actor over `InProcessSessionTransport`
**Then** the client's drained snapshot contains only the `Public` entity — neither `HiddenGameplay` entity, including the one assigned to this same actor (`ADR-019` §5.3: Observer never sees hidden data)

### Scenario 2 — MainGM receives the full scene (control case)

**Given** the same scene as Scenario 1
**When** the host builds and sends a `ProjectionSnapshot` for the session's `MainGM`
**Then** the client's drained snapshot contains all three entities, including both `HiddenGameplay` ones

### Scenario 3 — Repeated snapshot delivery is consistent

**Given** the same scene, audience, and sequence/revision numbers, unchanged between two calls
**When** the host builds and sends two `ProjectionSnapshot`s for the same actor
**Then** both delivered snapshots carry the same `PayloadHash` (their `SnapshotId`/`CreatedAtHostTime` may differ — those identify the build event, not the content)

### Required invariants

- `Odyssey.Networking.Projection` never computes visibility itself — it only encodes/sends/drains/decodes an already-built `ProjectionSnapshot`.
- A `ProjectionSnapshot`'s `PayloadHash` never changes for identical scene/audience/sequence-number inputs.

## 8. Deliverables

- Production code: `SceneProjectionContracts.cs`, `SceneProjectionWireCodec.cs` (`Odyssey.Application.Networking.Projection`); `SceneProjectionChannels.cs` (`Odyssey.Networking.Projection`).
- Tests: `VisibilityPolicyTests.cs` (6 pure-logic tests), `SceneProjectionTransportTests.cs` (3 transport-level tests, `InProcessSessionTransport`).
- Scripts / CI: `None` — no changes.
- Configuration: `None`.
- Documentation: this task contract; its ExecPlan; `SLICE-02_IMPLEMENTATION_BACKLOG.md`'s `ODY-S02-010` row.
- Generated evidence or build artifacts: `None` beyond the PR/CI record.
- Migration / recovery material: `None` — no persisted format introduced.

## 9. Acceptance criteria

1. `VisibilityPolicy.ComputeVisibleEntities` returns the full entity set for `MainGM`, for any scene.
2. `VisibilityPolicy.ComputeVisibleEntities` returns only `Public` entities for `Observer`, never `HiddenGameplay` ones, regardless of assignment.
3. `VisibilityPolicy.ComputeVisibleEntities` returns `Public` entities plus a `Player`'s own assigned `HiddenGameplay` entity, but not another actor's assigned entity.
4. `SceneProjectionBuilder.BuildSnapshot` called twice with identical scene/context/sequence-number inputs produces the same `PayloadHash` both times.
5. `SceneProjectionBuilder.BuildSnapshot` called for two different audiences (same scene/sequence numbers) produces different `PayloadHash` values when their visible entity sets differ.
6. A newly admitted Observer's snapshot, delivered over real `InProcessSessionTransport`, contains no hidden entities (negative/boundary criterion, real transport).
7. A MainGM's snapshot, delivered over the same real transport, contains every scene entity (control case).
8. Two transport deliveries of an unchanged scene/audience/sequence-number snapshot carry the same `PayloadHash` on the receiving side.
9. `Odyssey.Networking.Projection` contains no visibility/redaction logic — verified by code review and by the fact `SceneProjectionHostChannel.SendSnapshotAsync` takes an already-built `ProjectionSnapshot`, never a `Scene`/`ActorVisibilityContext`.
10. No new `ErrorCode`/`SafeReasonCode` is introduced (validation criterion — `git diff` shows no change to `ErrorCodes.cs`/`ERROR_CODES.md`).
11. `git status --porcelain` shows only files listed in §5's Allowed paths — no `ADR-015`–`019` file touched, no `ODY-S02-009` file touched.
12. Draft PR opened; CI green on all required checks (validation criterion, confirmed via `gh pr view`).

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-NET-012` | `.NET / dotnet test` | Newly admitted Observer's delivered snapshot excludes every `HiddenGameplay` entity, over real transport | Pass |
| `TC-NET-013` | `.NET / dotnet test` | MainGM's delivered snapshot contains every entity including hidden ones (control case), over real transport | Pass |
| `TC-NET-014` | `.NET / dotnet test` | Two deliveries of an unchanged snapshot carry the same `PayloadHash` | Pass |
| (uncatalogued) | `.NET / dotnet test` | `VisibilityPolicyTests.cs`'s 6 pure-logic cases (MainGM/Observer/Player/other-Player visibility, `PayloadHash` consistency and divergence) | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- `None` — no UI/manual-only surface; all behavior is covered by automated `.NET` tests.

### Required environments / profiles

- OS / architecture: Windows, .NET 10 SDK (matches CI).
- Unity editor or Player profile: Not applicable — pure .NET Core code only, no Unity-side change.
- Scripting backend: Not applicable.
- Network topology or database fixture: `InProcessSessionTransport` only, no real network.
- Other: `None`.

### Validation not required by this task

- Unity Editor/PlayMode compile or test run — no Unity-side file changed.
- Real network/relay integration — blocked behind `ADR-016` §14 (`ODY-S02-014`), not this task's concern.
- Migration rehearsal — no persisted format introduced.
- Performance profiling — in-memory, small fixed test scenes only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: introduces the first `odyssey.projection.snapshot` wire contract (`contractVersion` 1) — no prior version to migrate from.
- Version fields affected: `None` at the application/package level.
- Migration or upcaster: `None`.
- Forward / backward behavior: `Not applicable` — no deployed clients depend on this new contract yet.
- Rollback method: revert this task's commits; nothing outside this task's own files depends on the new code yet.
- Data-loss risk and protection: `None` — no persisted state.
- Recovery rehearsal required: `No`.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: scene entity display data, classified `Public`/`HiddenGameplay` (GM-only secret content) per `ADR-019` §5.
- Trust boundaries: host (authoritative, sees all) vs. each connected client (sees only its own redacted `ProjectionSnapshot`).
- Authorization / audience checks: `VisibilityPolicy.ComputeVisibleEntities`, computed per `ActorVisibilityContext` (role + audience `UserId`), entirely in `Odyssey.Application` before the payload reaches `Odyssey.Networking` (`ADR-019` §6.2).
- Redaction requirements: `HiddenGameplay` entities excluded from `Observer`'s and non-owning `Player`'s snapshot; only `MainGM` and the assigned `Player` see a given hidden entity.
- Log-safe fields: `None` new — no logging added by this task.
- Abuse / malformed input limits: wire codec bounded by `JsonPayloadLimits.EventPayloadBytes`, rejects malformed/oversized/unsupported-contract payloads via `SerializationFailures`, same as every other production codec in this repository.
- Security tests: Scenario 1/`TC-NET-012` (Observer never receives hidden entities) is this task's direct security-relevant regression test, in the same spirit as `ODY-S02-007`'s (SP-04) hidden-data-boundary suite.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: matches multiple explicit `PLANS.md` §1.2 triggers — it changes more than one production module (`Odyssey.Application` and `Odyssey.Networking` both gain new content); it affects networking, security, permissions, and redaction directly (this is the first production implementation of `ADR-017`/`ADR-019`'s read/visibility contract); and it required real design judgment (minimal scene/entity model shape, namespace placement, `PayloadHash` field selection, `SnapshotId` generation) documented as five real decisions in the ExecPlan — not a mechanical transcription of an already-fully-specified design.
- ExecPlan path: `docs/plans/active/ODY-S02-010_Scene_Snapshot_And_Redacted_Projection_Delivery.md`
- Expected pull request count: 1 (single Draft PR covering all production code, tests, and registry updates).
- Milestone or sequencing constraints: depends on `ODY-S02-009` (merged); blocks `ODY-S02-011` (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §6).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (`ODY-S02-010` row only).
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/SLICE-02_BACKLOG.md`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, `docs/errors/ERROR_CODES.md` (no new `ErrorCode`), anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first `odyssey.projection.snapshot` wire contract (`contractVersion` 1, new) — no prior version to migrate from.
- Documentation version changes: None — no ADR changes version.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionWireCodec.cs` — new.
- `Packages/com.odyssey.networking/Runtime/Projection/SceneProjectionChannels.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Networking/SceneProjection/VisibilityPolicyTests.cs`, `SceneProjectionTransportTests.cs` — new.
- `Tests/Metadata/test-catalog.json` — 3 new entries (`TC-NET-012`–`014`).
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — `ODY-S02-010` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~SceneProjection"` | Passed | 9/9 new tests, 0 failed. |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 179/179, 0 failed (1 Contracts + 1 Domain + 46 Networking [37 pre-existing + 9 new] + 84 Unit + 2 Architecture + 45 Persistence), including `RepositoryStructurePassesArchitectureGuard`. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, all `TC-ARCH-002` controlled-fixture checks pass; catalog cross-check for `TC-NET-012`–`014` resolves now that this task contract exists. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `VisibilityPolicyTests.ComputeVisibleEntities_MainGM_SeesAllEntities_IncludingHidden`. |
| AC-2 | Passed | `VisibilityPolicyTests.ComputeVisibleEntities_Observer_SeesOnlyPublicEntities_NoHiddenGmData`. |
| AC-3 | Passed | `VisibilityPolicyTests.ComputeVisibleEntities_Player_SeesOwnAssignedHiddenEntity_ButNotOthers`, `..._OtherPlayer_DoesNotSeeEntityAssignedToDifferentPlayer`. |
| AC-4 | Passed | `VisibilityPolicyTests.BuildSnapshot_RepeatedCallSameSceneAndContext_ProducesSamePayloadHash_WhenStateUnchanged`. |
| AC-5 | Passed | `VisibilityPolicyTests.BuildSnapshot_DifferentAudience_ProducesDifferentPayloadHash`. |
| AC-6 | Passed | `TC-NET-012`, `SceneProjectionTransportTests.NewlyAdmittedObserver_ReceivesRedactedSnapshot_NoHiddenEntities_OverRealTransport`. |
| AC-7 | Passed | `TC-NET-013`, `SceneProjectionTransportTests.MainGM_ReceivesFullSnapshot_ControlCase_OverRealTransport`. |
| AC-8 | Passed | `TC-NET-014`, `SceneProjectionTransportTests.RepeatedSnapshotDelivery_SameUnchangedState_YieldsSamePayloadHash_OverRealTransport`. |
| AC-9 | Passed | `SceneProjectionHostChannel.SendSnapshotAsync(ISessionTransport, ConnectionHandle, ProjectionSnapshot, ...)` — signature takes only an already-built `ProjectionSnapshot`; code review confirms no `Scene`/`VisibilityPolicy` reference in `Odyssey.Networking.Projection`. |
| AC-10 | Passed | `git diff --stat` shows no change to `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` or `docs/errors/ERROR_CODES.md`. |
| AC-11 | Passed | `git status --porcelain` shows only: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`, `DotNet/Tests/Odyssey.Tests.Networking/SceneProjection/`, `Packages/com.odyssey.application/Runtime/Networking/Projection/`, `Packages/com.odyssey.networking/Runtime/Projection/`, this task contract, and its ExecPlan — all within §5's Allowed paths; no `ADR-015`–`019` or `ODY-S02-009` file touched. |
| AC-12 | Passed | PR [#47](https://github.com/odyssey-services/Odyssey_VTT/pull/47) (Draft) — CI run [32907596616](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32907596616) green on all 4 required checks, confirmed via fresh `gh pr view 47 --json state,isDraft,statusCheckRollup`: `repository-policy-format-structure` [SUCCESS](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32907596616/job/97994945299), `dotnet-restore-build-test` [SUCCESS](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32907596616/job/97994945266), `unity-project-package-static` [SUCCESS](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32907596616/job/97994945090), `buildidentity-provenance` [SUCCESS](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32907596616/job/97994945247). |

## 18. Blockers, risks, and open decisions

- Blocker: `None`.
- Open decision (deliberate, not a blocker): the two-tier `Public`/`HiddenGameplay` classification plus direct `AssignedToUserId` is this task's own minimal stand-in for `ADR-019` §7's "character-assignment" pipeline step — a future task introducing a real `Character`/ownership aggregate would revisit `SceneEntity`'s shape, not treat it as untouchable.
- Risk: none identified beyond what is already named as future scope (`ProjectionDeltaBatch`, reconnect, real content model) in `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5.
