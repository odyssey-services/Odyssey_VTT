# SP-04 — Hidden Data Boundary harness

**This is a real, functional harness, not a stub or a paper argument.** It is not production `Odyssey.Application`/`Odyssey.Networking` code either — see "What this is not" below.

## Why this lives in `Odyssey.Tests.Networking`, not `Tools/Spikes/`

`SP-02` and `SP-03` both lived under `Tools/Spikes/`, isolated from `DotNet/Odyssey.Core.sln` and CI, because each measured something the harness itself couldn't guarantee stayed true over time: `SP-02` measured real SQLite crash/backup behavior on one development machine; `SP-03` measured real external-internet conditions this environment could not fully control. Both were explicitly one-shot evidence-gathering exercises: "safe to delete... a future implementation task... must not `ProjectReference` or otherwise depend on this project."

`SP-04` is different in kind, not just degree. It does not measure anything about an external or uncontrollable environment — it exercises deterministic, already-accepted, already-in-solution code (`InProcessSessionTransport`, `ADR-015`; `DiagnosticBundlePlanner`, `ADR-010`) plus a minimal, purpose-built implementation of the `ADR-017`/`ADR-019` contract this task itself writes. The property under test — "a hidden entity never reaches an unauthorized client's snapshot, delta, runtime state, local cache, or diagnostic export" — is exactly the kind of security-relevant regression a CI suite should keep proving on every future change to this codebase, not a fact established once and then left to rot as a historical log file. Placing it in `Odyssey.Tests.Networking` (already referenced by `DotNet/Odyssey.Core.sln`, already wired into `dotnet-restore-build-test`) means every future PR that touches `ISessionTransport`, the eventual real snapshot/delta implementation, or the diagnostic export path re-proves this contract automatically.

## What this is

- `Harness/ProjectionModel.cs` — a minimal, functional (not stubbed) implementation of `ADR-017`'s `ProjectionSnapshot`/`ProjectionDeltaBatch`/`Operations[]` shape and `ADR-019`'s `VisibilityPolicy`/permission-change-delta pipeline, scoped to exactly the baseline this task's two governing ADRs describe (three roles, `RemoveFromProjection`/`RemoveCapability`, no delegation or arbitrary scope).
- `Harness/ClientState.cs` — a client-side runtime-state store and a *separately modeled* local-cache store, so the roadmap 11.5 "runtime-state" and "local cache" surfaces are genuinely distinct assertions, not the same check asked twice.
- `Harness/WireCodec.cs` — real JSON serialization of the harness's snapshot/delta types to the actual bytes carried inside `NetworkEnvelope.Payload`, so "the hidden entity is not in the snapshot" is checked against real wire bytes, not just an in-memory object graph.
- `HiddenDataBoundaryTests.cs` — the NUnit tests themselves, run over the real, already-accepted `InProcessSessionTransport` (`ADR-015`).

## What this is not

- **Not production code.** None of `Harness/*.cs`'s types are referenced by `Odyssey.Application` or `Odyssey.Networking`, and none should be. Per this task's own scope, no production integration of this harness is authorized — only what the test itself needs.
- **Not the real permission/redaction implementation.** A future implementation task will write the real `Odyssey.Application`-layer `PermissionDecision`/`VisibilityPolicy` code; this harness's `Harness/ProjectionModel.cs` proves the *contract* (`ADR-017`/`ADR-019`) is implementable as described, not that any particular future implementation is correct.
- **Not a delegation/scope/ownership system.** `ActorPermissionState` supports exactly what `ADR-019`'s baseline needs (a role, explicit visibility grants, explicit capabilities) — nothing beyond it.

## How to run

```powershell
dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~HiddenDataBoundary"
```

Or as part of the full suite: `dotnet test DotNet/Odyssey.Core.sln`.

## What the tests prove (see the report for full detail)

Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.5's five surfaces, each with its own test:

1. **Snapshot** — a hidden entity's id and content never appear in the wire bytes of a `ProjectionSnapshot` built for a Player without a grant.
2. **Delta** — the same, for an ordinary (non-permission-related) `ProjectionDeltaBatch`.
3. **Runtime state** — after real delivery over `InProcessSessionTransport`, the client's in-memory projection never contains the hidden entity.
4. **Local cache** — the client's separately-modeled persisted-cache structure never contains it either.
5. **Diagnostic export** — a diagnostic log built only from the client's own (correctly redacted) runtime state cannot contain it; as defense-in-depth, a forced leak attempt is independently rejected by the already-existing `DiagnosticBundlePlanner` safety scan.

Plus: granting visibility delivers the entity via `AddEntity`; revoking it delivers `RemoveFromProjection` and purges both runtime state and local cache; revoking a capability delivers `RemoveCapability`.
