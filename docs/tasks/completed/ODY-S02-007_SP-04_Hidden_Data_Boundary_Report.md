# ODY-S02-007 — SP-04 Hidden Data Boundary: Spike Report

**Parent task:** `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md`
**Prepared:** 2026-08-25 UTC
**Spike ID:** `SP-04` (`17_Roadmap_Odyssey_VTT_v0.11.md` §23: "Real hidden-data redaction")
**Harness:** `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/` (see its `README.md` for what it is, what it is not, and why it lives here rather than `Tools/Spikes/`)
**Evidence:** 22 automated NUnit tests (8 new `HiddenDataBoundary` tests plus the 14 pre-existing `InProcessSessionTransport` tests, unaffected), run directly via `dotnet test`, CI-wired — reproducible on every future run, not a one-shot log capture like `SP-02`/`SP-03`.

Unlike `SP-02` and `SP-04`'s environment-constrained predecessor `SP-03`, this spike required no external environment, no account provisioning, and found no `NOT_VERIFIED` gap — every roadmap §11.5 requirement was directly, automatically testable against real (though intentionally minimal) code built on top of already-accepted contracts (`ADR-015`, `ADR-017`, `ADR-019`, `ADR-010`).

---

## 0. Owner decision

Pending. Per this repository's established pattern (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §0, `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md` §0), this section is completed by a follow-up point-fix once the product owner reviews this report. Per this task's own explicit instruction, closing `SLICE-02_BACKLOG.md` as a whole (the `ODY-S01-014`-style closure gate) is a separate future task, performed only after that acceptance.

---

## 1. What was tested and how

Roadmap §11.5 requires a test proving: a host-hidden object is absent from a Player's (1) snapshot, (2) delta, (3) runtime state, (4) local cache, (5) diagnostic export; that granting permission makes it appear; that revoking permission removes it.

Before writing any test, this task confirmed what already exists to build on:

- `ADR-017` already fixes `ProjectionSnapshot`/`ProjectionDeltaBatch`'s shape and `Operations[]` (including `RemoveFromProjection`/`RemoveCapability`).
- `ADR-019` already fixes the three-role baseline and the `Membership → PermissionDecision → VisibilityPolicy → ClientProjection` pipeline, plus the revocation-via-delta mechanism.
- `ADR-015`'s `InProcessSessionTransport` is the one transport this task is authorized to use — real, already-accepted, already in `DotNet/Odyssey.Core.sln`.
- `Odyssey.Application.Diagnostics.DiagnosticBundlePlanner` (`ADR-010`) already exists as the repository's one real diagnostic-export mechanism, complete with a text-substring safety scan that already denylists words including `"hidden"`, `"secret"`, `"private"`, `"gmnote"`.

No production `Odyssey.Application`/`Odyssey.Networking` code exists yet implementing `ADR-017`/`ADR-019`'s `PermissionDecision`/`VisibilityPolicy`/projection-builder logic — this spike had to write a minimal, functional (not stubbed) version of exactly that, scoped to what the test needs, living entirely inside the test project (`DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/`), per this task's own explicit "no production integration beyond what the test itself needs" boundary.

**Harness location decision**: placed in `Odyssey.Tests.Networking` (CI-wired, permanent), not `Tools/Spikes/` (isolated, throwaway) — see the harness `README.md` for the full reasoning. In short: `SP-02`/`SP-03` each measured something about an external or uncontrollable environment that a one-shot log capture was the right evidence format for; `SP-04` exercises only deterministic, already-accepted, already-in-solution code, and the property it proves (hidden data never leaks) is exactly the kind of regression a CI suite should keep re-proving on every future change, not a historical fact.

**Test scenario, matching roadmap §11.5 literally**: a `HostWorldState` with one `Public` entity (a torch) and one `HiddenGameplay`-classified entity (`ADR-010` §10's classification) — a trapdoor lever, GM secret. A `Player`-role actor with no grant. Snapshot and delta construction, real delivery over `InProcessSessionTransport`, client-side application, and diagnostic export are all exercised for real, then a visibility grant, then a revocation, then a capability revocation.

---

## 2. Findings per roadmap §11.5 surface

### 2.1 Snapshot

**Test:** `Snapshot_ForPlayerWithoutGrant_ExcludesHiddenEntity_BothInWireBytesAndDecodedPayload`

**Result: Passed.** The hidden entity's id (`obj_hidden_trapdoor_002`) and its display content (`"trapdoor lever GM secret"`) are both asserted absent from the **actual serialized wire bytes** (`WireCodec.WireBytesContain`, a raw UTF-8 substring search over the exact bytes `ProjectionSnapshot` would carry in `NetworkEnvelope.Payload`), not merely absent from a decoded object graph. The decoded snapshot's entity list also excludes it while including the visible torch.

**Control case** (`Snapshot_ForMainGM_IncludesHiddenEntity_ControlCase`): **Passed.** MainGM's snapshot *does* include the hidden entity, proving the harness isn't simply omitting everything — the exclusion is role-specific, as `ADR-019` §5.1 requires.

### 2.2 Delta

**Test:** `UnrelatedChangeDelta_ForPlayerWithoutGrant_NeverMentionsHiddenEntity`

**Result: Passed.** A `ProjectionDeltaBatch` built for an ordinary gameplay change (moving the visible torch) — unrelated to any permission change — is asserted to contain no operation targeting the hidden entity, and the hidden entity's id never appears in the delta's wire bytes.

### 2.3 Runtime state

**Test:** `ClientRuntimeStateAndLocalCache_ForPlayerWithoutGrant_NeverContainHiddenEntity_AfterRealTransportDelivery`

**Result: Passed.** The snapshot is sent over a real `InProcessSessionTransport.CreatePair` connection (`SendReliableAsync`/`DrainReliable`, the real reliable-channel API `ADR-015` fixed), decoded from the real received bytes, and applied to a `ClientRuntimeState`. The hidden entity is confirmed absent from the client's in-memory projection; the visible entity is confirmed present.

### 2.4 Local cache

**Same test as 2.3.** `ClientLocalCache` is modeled as a structure genuinely separate from runtime state (see harness README) — updated at the same point a real client would update its persisted cache. The hidden entity is confirmed absent from it too, proving this is not the same assertion as 2.3 restated.

### 2.5 Diagnostic export

**Test:** `DiagnosticExport_FromPlayerRuntimeState_NeverContainsHiddenEntity_AndPlannerRejectsAForcedLeak`

**Result: Passed**, with two layers of evidence:

1. **Structural**: a diagnostic log summary is built only from `clientState.KnownEntityIdsForDiagnostics()` — the client's own runtime state, never host state. Since the hidden entity was never delivered to this client (2.3), it structurally cannot appear in anything derived from that client's own state. The resulting log line, run through the real `DiagnosticBundlePlanner.CreateManifest` (`ADR-010`), is accepted as legitimately export-safe.
2. **Defense-in-depth**: a second, deliberately constructed log line naming the hidden entity's id directly (simulating a hypothetical leak from a different code path) is rejected outright by `DiagnosticBundlePlanner`'s already-existing safety scan (`PassesFinalExportSafetyScan`'s substring denylist, which already includes `"hidden"`) — proving a real backstop exists even if the structural guarantee were somehow bypassed elsewhere.

### 2.6 Grant reveals the entity

**Test:** `GrantingVisibility_DeliversAddEntityDelta_ClientRuntimeAndCacheNowContainHiddenEntity`

**Result: Passed.** After `player.GrantVisibility(hiddenId)`, `ProjectionBuilder.BuildPermissionChangeDelta` produces an `AddEntity` operation; delivered over the real transport, the client's runtime state and cache both now contain the entity.

### 2.7 Revoke removes the entity

**Test:** `RevokingVisibility_DeliversRemoveFromProjectionDelta_ClientRuntimeAndCacheNoLongerContainHiddenEntity`

**Result: Passed.** Starting from a granted state (precondition asserted explicitly), revoking visibility produces a `RemoveFromProjection` operation (asserted by kind, not merely by side effect); delivered over the real transport, the client's runtime state **and** local cache both drop the entity — proving the cache is actively purged, not left stale.

### 2.8 Capability revocation (bonus coverage, not explicitly required by §11.5 but exercises the other `ADR-017` operation kind)

**Test:** `RevokingCapability_ProducesRemoveCapabilityOperation_ClientLosesAllowedCommand`

**Result: Passed.** A granted action capability (`Scene.Interact.TrapdoorLever`) is revoked, producing a `RemoveCapability` operation; the client's `AllowedCommands` set loses it.

---

## 3. Recommendation: `ADR-017`/`ADR-019` are confirmed implementable as described — no gap found

**No amendment to `ADR-017` or `ADR-019` is triggered by this spike's findings.** Every mechanism those ADRs describe — composite snapshot identity feeding a correctly-scoped payload, `Operations[]`'s `AddEntity`/`RemoveFromProjection`/`AddCapability`/`RemoveCapability`, the single-authoritative-state-plus-per-connection-filter redaction model, revocation via delta rather than a parallel channel — was directly exercised with real code and real (in-process) network delivery, and held in every case. This is a stronger result than `SP-02`'s (which also found no nonconformance) in one respect: because this harness is permanent, CI-wired test coverage rather than a one-shot log, this confirmation is continuously re-validated on every future change, not a historical snapshot of confidence.

Per this task's own instruction (§7 of its ТЗ): no gap was found requiring this section to report a blocker or an unresolved finding instead.

---

## 4. What this spike does not cover (explicitly, not silently)

- **The real (non-harness) `Odyssey.Application` permission/redaction implementation.** This spike proves the *contract* is implementable, not that any future production implementation of it will be correct — that implementation's own tests are its job, not this spike's.
- **A real relay/rendezvous transport.** Only `InProcessSessionTransport` was used, per this task's own explicit scope (`ADR-016`'s Unity Relay integration remains gated by its own empirical-spike requirement, unrelated to this task).
- **Field-level audience visibility, delegation, ownership/control-based visibility** — all explicitly out of `ADR-019`'s baseline scope (its §10), and so out of this spike's scope too.
- **Multi-connection interaction** (e.g., two Players with different grants observing the same host simultaneously) — each test uses one host/client pair; the underlying mechanism (per-connection `AudienceUserId`-scoped payload) is the same one that would serve multiple connections, but this spike did not construct a multi-connection scenario explicitly.

---

## 5. Where the harness lives and how to reproduce

- Code: `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/` — `Harness/` (`ProjectionModel.cs`, `ClientState.cs`, `WireCodec.cs`) and `HiddenDataBoundaryTests.cs`. Referenced by `DotNet/Odyssey.Core.sln` via the already-existing `Odyssey.Tests.Networking.csproj`.
- Documentation: `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/README.md` — what the harness is, what it is not, and the location decision's full reasoning.
- Reproduction command:

  ```powershell
  dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~HiddenDataBoundary"
  ```

  or as part of the full suite: `dotnet test DotNet/Odyssey.Core.sln`.
- Unlike `SP-02`/`SP-03`, this harness **is** wired into the standard CI path (`dotnet-restore-build-test`, already covering `Odyssey.Tests.Networking`) — a deliberate choice (§1), not an oversight.

---

## 6. Findings of nonconformance with already-accepted ADRs

**None found.** Every scenario's measured behavior matched what `ADR-015`, `ADR-017`, and `ADR-019` already specify — see section 3.

---

**End of report.**
