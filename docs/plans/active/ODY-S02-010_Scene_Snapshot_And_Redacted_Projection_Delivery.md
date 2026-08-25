# ODY-S02-010 — Scene Snapshot & Redacted Projection Delivery

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-010-scene-snapshot-and-redacted-projection-delivery`
**Pull request:** Not yet opened
**Last updated:** 2026-08-26 UTC

## 1. Purpose and user-visible outcome

Implements roadmap §11.6 step 4: a newly admitted, role-assigned actor (`ODY-S02-009`) is delivered the scene assigned to them, correctly redacted by their `ADR-019` baseline role. Real `Odyssey.Application`-layer `ProjectionSnapshot`/`VisibilityPolicy` code (`ADR-017` §4, `ADR-019` §6.2/§7) — not a reuse of `ODY-S02-007`'s (SP-04) test-only harness types. Unblocks `ODY-S02-011` (command/delta broadcast), which needs a scene already delivered to act on.

## 2. Task contract

- Goal: real `Odyssey.Application`/`Odyssey.Networking` `ProjectionSnapshot` construction and `Membership → PermissionDecision inputs → VisibilityPolicy → ClientProjection` pipeline (`ADR-019` §7), delivered over `InProcessSessionTransport`.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (implementation), backlog `ODY-S02-010`.
- In scope: `Odyssey.Application.Networking.Projection` (scene model, `VisibilityPolicy`, `ProjectionSnapshot`, wire codec), `Odyssey.Networking.Projection` adapter, tests, task contract, ExecPlan, backlog row update.
- Out of scope: `ProjectionDeltaBatch`/command handling (`ODY-S02-011`), reconnect/gap-repair/delta-buffer (`ODY-S02-012`), full game-content model, harness reuse.
- Required authorities: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5/§2.3, `ADR-017` §4/§7/§11/§12, `ADR-019` §5/§6.2/§7/§11/§12, `ADR-015`, `ADR-001` §6.6, `ADR-003` (canonical codecs), `ADR-004` (Result/Error).
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln`.

## 3. Current state

- `ODY-S02-009` merged to `main`: `SessionAdmissionService`/`SessionAdmissionState`/`SessionMember`/`BaselineRole` exist in `Odyssey.Application.Networking.Session`, real over `InProcessSessionTransport`.
- No `ProjectionSnapshot`/`VisibilityPolicy`/scene-content production code exists yet anywhere in the repository — only `ODY-S02-007`'s explicitly test-only harness (`DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/`), which proved the `ADR-017`/`ADR-019` contract is implementable, not that its own types are the production shape (`SLICE-02_IMPLEMENTATION_BACKLOG.md` §2.3).
- `ADR-017` §4 fixes `ProjectionSnapshot`'s four-dimension identity (`SnapshotId`; `BaseSessionSequence`/`ProjectionRevision`/`PermissionRevision`; `PayloadHash`) field-for-field; §5–§9 (delta batches, gap detection, reconnect) are explicitly out of this task's scope.
- `ADR-019` §7 fixes the `Membership → PermissionDecision inputs → VisibilityPolicy → (character-assignment) → Scene assignments → ClientProjection` pipeline as the normative mechanism; §6.2 fixes that the read/visibility check happens in the Application layer, before `Odyssey.Networking` ever sees the payload.
- `JsonObjectReader` (`Odyssey.Application.Serialization`) is flat-only (no array support) — `ManifestAndDiagnosticCodecs.cs`'s `LogEventV1JsonCodec` already establishes the hand-rolled `JsonTextWriter`/`JsonTextReader` array-walk pattern needed for `ProjectionSnapshot.VisibleEntities`.

## 4. Proposed approach

Split along the same `ADR-001` §6.6 boundary `ODY-S02-009` already established: pure, transport-independent projection logic (`VisibilityPolicy`, `SceneProjectionBuilder`) in `Odyssey.Application.Networking.Projection`, directly unit-testable; a thin `Odyssey.Networking.Projection` adapter that only encodes/sends/drains/decodes an already-built (already-redacted) `ProjectionSnapshot`, making no visibility decision itself. Minimal scene/entity model (two-tier classification — `Public`/`HiddenGameplay` — plus a direct per-entity `AssignedToUserId` as this task's stand-in for the full character-assignment/ownership model `ADR-019` §10 defers): enough to prove step 4 (scene delivered, correctly redacted by role), not a full content model. No new `ErrorCode`s or `SafeReasonCode`s: the wire codec reuses the already-existing `SerializationFailures.InvalidPayload`/`UnsupportedContract`; `VisibilityPolicy`/`SceneProjectionBuilder` are pure functions with no failure path.

## 5. Milestones

### M1 — Pure projection logic, real and fully tested

- [x] `SceneProjectionContracts.cs` written in `Odyssey.Application.Networking.Projection` (`SceneEntity`, `Scene`, `ActorVisibilityContext`, `VisibilityPolicy`, `ProjectionSnapshot`, `SceneProjectionBuilder`).
- [x] Pure-logic tests pass (MainGM sees all; Observer redacted to public-only; Player sees own assigned hidden entity but not another's; repeated `BuildSnapshot` calls yield the same `PayloadHash` for unchanged state; different audience yields a different `PayloadHash`).

### M2 — Real wire codec and transport adapter, exercised over InProcessSessionTransport

- [x] `SceneProjectionWireCodec.cs` written (hand-written canonical JSON, array-bearing, `ADR-003`-compliant, following `ManifestAndDiagnosticCodecs.cs`'s established pattern for array payloads).
- [x] `SceneProjectionChannels.cs` written in `Odyssey.Networking.Projection` (`SceneProjectionHostChannel.SendSnapshotAsync`, `SceneProjectionClientChannel.DrainSnapshots`).
- [x] 3 required transport-level tests pass, all over real `InProcessSessionTransport`: newly admitted Observer receives a redacted snapshot with no hidden entities; MainGM receives the full scene (control case); two deliveries of an unchanged scene/audience/sequence carry the same `PayloadHash`.

### M3 — Registries, validation, task contract complete

- [x] 3 new `test-catalog.json` entries (`TC-NET-012`–`014`) for the transport-level scenarios.
- [ ] Full solution build/test green; `verify-format.ps1`/`check-repository-policy.ps1` pass.
- [ ] Task contract, backlog row, Draft PR.

## 6. Progress log

- 2026-08-26 — Preflight confirmed `ODY-S02-009` merged; branched cleanly from `main`.
- 2026-08-26 — Read `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5/§2.3, `ADR-017` (full), `ADR-019` (full), `ODY-S02-007`'s harness (`Harness/ProjectionModel.cs`, README) as reference contract only, `ODY-S02-009`'s existing Session code, `ManifestAndDiagnosticCodecs.cs` for the array-wire-codec pattern, `SessionTransportContracts.cs` for exact `ISessionTransport`/`NetworkEnvelope` signatures.
- 2026-08-26 — Wrote pure projection logic, wire codec, transport adapter; all 9 new tests pass on first full run (no compile-error iteration beyond one missing `using` for `CollectionAssert`, replaced with `Assert.That(..., Does.Contain(...))`).
- 2026-08-26 — Full solution: 179 tests pass (170 pre-existing + 9 new).

## 7. Decisions

- 2026-08-26 — Decision: scene/entity model kept to two-tier classification (`Public`/`HiddenGameplay`) plus a direct nullable `AssignedToUserId` on `SceneEntity`, not a full `Character`/`Ownership` aggregate. Rationale: no persisted game-content model exists yet (`SLICE-03`+ scope); this is the minimal shape that lets `VisibilityPolicy` prove all three baseline roles behave distinctly (`ADR-019` §5) without inventing a content model this task does not own. Authority: `SLICE-02_IMPLEMENTATION_BACKLOG.md` §5's "minimal model" instruction; `ADR-019` §7's pipeline naming "character-assignment" as a step without fixing its shape.
- 2026-08-26 — Decision: `VisibilityPolicy`/`SceneProjectionBuilder` placed in `Odyssey.Application.Networking.Projection`, parallel to `ODY-S02-009`'s `Odyssey.Application.Networking.Session`, not nested inside `.Session`. Rationale: `ADR-017`/`ADR-019` name a distinct vocabulary (`ProjectionSnapshot`, `VisibilityPolicy`) from admission/lobby (`SessionMember`, `JoinCode`); a parallel namespace keeps each ADR's contract independently navigable, matching the existing `Odyssey.Application.Networking.*` sub-namespacing convention. Authority: `ADR-019` §6.2 (read/visibility check "in the Application layer"); `ODY-S02-009`'s own namespace precedent.
- 2026-08-26 — Decision: `PayloadHash` is computed over the deterministic content fields (`SessionId`, `AudienceUserId`, sequence/revision numbers, serialized visible entities) only — excluding `SnapshotId`/`CreatedAtHostTime`, which intentionally vary per build call. Rationale: `ADR-017` §4 requires `PayloadHash` as an integrity check independent of position/identity; the task's own required test ("repeated request gives a consistent result, same `PayloadHash` when state unchanged") is only satisfiable if the hash reflects content, not the per-call build event. Authority: `ADR-017` §4's own stated rationale for why `PayloadHash` is a separate dimension from `SnapshotId`.
- 2026-08-26 — Decision: `SnapshotId` generated via `Guid.NewGuid()` directly (`"psnap_" + Guid.NewGuid():N`), not `Odyssey.Application.Random`'s authoritative gameplay RNG. Rationale: same "local opaque identifier, not gameplay RNG" exemption already established for `ODY-S02-009`'s `JoinCode.Generate()` and `ODY-S01-007`'s `CampaignId`. Authority: precedent recorded in both of those tasks' own reasoning.
- 2026-08-26 — Decision: no new `ErrorCode`/`SafeReasonCode` introduced. Rationale: the wire codec's only failure path is malformed/unsupported payload, already covered by `SerializationFailures.InvalidPayload`/`UnsupportedContract`; `VisibilityPolicy`/`SceneProjectionBuilder` are pure functions with no failure path (argument validation throws, matching every other constructor in this codebase, not a `Result` failure). Authority: `ADR-019` §9's "no new `SafeReasonCode` required for baseline permission denials" principle, applied here since this task does not implement action/permission denial at all (that is `ODY-S02-011`'s action-check scope).

## 8. Discoveries and deviations

- Discovery: `JsonObjectReader`'s flat property model (used by `ODY-S02-009`'s `SessionAdmissionWireCodecs.cs`) cannot represent `ProjectionSnapshot.VisibleEntities` (an array of objects) — resolved by following `ManifestAndDiagnosticCodecs.cs`'s `LogEventV1JsonCodec`, which already establishes a hand-rolled `JsonTextWriter`/`JsonTextReader` walk for exactly this shape (`safeProperties` array), reused as the established pattern rather than inventing a new one.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Reverting this task's commits removes all new projection/visibility code and tests with no compatibility or data-loss risk — nothing outside this task's own files and `ODY-S02-011`/`012` (not yet started) depends on it.

## 11. Open questions and blockers

- No blockers.
- Deferred, not blocking: `ProjectionDeltaBatch`/gap detection/reconnect fallback (`ADR-017` §5–§9, `ODY-S02-011`/`012`), full ownership/control model (`ADR-019` §10), real game-content persistence (`SLICE-03`+).

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
