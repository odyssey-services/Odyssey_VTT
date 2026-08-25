# ODY-S02-002 — Technical Spike SP-03: Internet Connectivity

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-002-sp-03-internet-connectivity`
**Pull request:** Draft — [#39](https://github.com/odyssey-services/Odyssey_VTT/pull/39)
**ExecPlan:** Not required (Brief plan) — no ExecPlan file was created for this task (see §14).
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce a reproducible, evidence-backed report (`docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`) investigating real internet connectivity per roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.4, using a throwaway, evidence-only harness — and to produce a justified (non-binding) recommendation on a relay/rendezvous stack for `ODY-S02-003`, plus non-binding proposals responding to `ADR-015` §12.1 (`TransportTimeoutPolicy`) and §12.2 (`SessionEndpoint` shape).

This is an investigative spike, not an implementation task. It produces no production code, no ADR content, and selects nothing on the product owner's behalf. Unlike `SP-02`, this spike's own environment could not exercise most of roadmap §11.4's checklist for real (no Unity Gaming Services project is linked to this repository, and only one outbound network path is available) — this is documented as an explicit, honest limitation (report §2, §8), not worked around with a silent simulation.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-015` §12.1/§12.2 explicitly defer `TransportTimeoutPolicy`'s exact values and `SessionEndpoint`'s full shape to this spike and `ODY-S02-003`; `06_Networking_and_Session_Sync` `OPEN-NW-001` explicitly leaves the concrete relay/rendezvous provider open. Without this spike, `ODY-S02-003` would have to fix a relay/rendezvous stack as `Accepted` with no empirical grounding at all.
- Value or risk reduction: even though full end-to-end relay testing was not achievable in this environment, this spike still produces real, reproducible evidence for the network primitives (NAT traversal, sustained large-payload transfer, repeated round-trip latency) any relay-first flow depends on, and it precisely documents the remaining gap so `ODY-S02-003` does not mistake this report for more evidence than it actually contains.
- Blocking or enabling relationship: `SLICE-02_BACKLOG.md` §5 sequences `ODY-S02-002` before `ODY-S02-003` (Rendezvous/Relay Strategy ADR) — `ODY-S02-003` must not be marked `Accepted` on a candidate stack before this spike's report exists, mirroring `SLICE-01`'s `SP-02` → `ADR-011` v1.1 ordering.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1, §7 (investigation and spikes)
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.4 (the required checklist), §23 (spike closure principle: "не кодом как таковым, а принятым решением и воспроизводимым доказательством") — private local reference, not committed to the repository
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §4.1/§4.2 (Internet-first, Relay-first architecture), §4.3 (transport abstraction sketch), §5.3 (asset channel, chunk/range design), §6.2/§6.3 (join code, session directory entry fields), §51 `OPEN-NW-001`/`OPEN-NW-002` (open relay-provider/asset-storage questions) — private local reference
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (Accepted) §5.2 (realtime channel), §12.1 (`TransportTimeoutPolicy`, open), §12.2 (`SessionEndpoint`, open)
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md`, `ODY-S01-005_SP-02_Persistence_Reliability_Report.md` — structural reference for a spike's task-contract/report split and harness-as-reproducible-evidence pattern
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's boundary as scaffolded by `ODY-S02-000`), §5 (sequencing)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), roadmap section 11.4, backlog `ODY-S02-002`, spike registry `SP-03` (roadmap §23).
- Existing test IDs: None (this task does not touch the `Tests/Metadata/test-catalog.json` `TC-*` registry — the harness's scenario-level pass/fail lines are spike evidence, not registered TestCase IDs, matching `SP-02`'s precedent).
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: roadmap §11.4's checklist and §23's closure principle, and `06_Networking_and_Session_Sync`'s §4/§5/§6/§51 content, are summarized (not pasted verbatim beyond short customary phrases) into this task, the harness's own comments/output, and the report. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/tasks/SLICE-02_BACKLOG.md` on `main` (merged via `ODY-S02-001`, PR #38) lists `ODY-S02-002` as depending on `ODY-S02-001` only, which is `Done`/merged — confirmed by `git log`/`Read` before branching.
- `06_Networking_and_Session_Sync...` §51 `OPEN-NW-001` explicitly leaves the concrete relay provider unselected and lists "регионы; latency; 12 participants; pricing; reconnect semantics; reliable/unreliable channels; Unity SDK stability" as its evaluation criteria — confirmed by `grep`/`Read`; no relay/rendezvous product is named anywhere in the document (confirmed by a broad `grep` for common candidate names).
- `ProjectSettings/ProjectSettings.asset` line 934 (`cloudProjectId:`) is empty — no Unity Gaming Services project is linked to this repository, confirmed by direct inspection.
- Unity Editor 6000.4.0f1 is installed in this environment (matching `ODY-S01-007`'s prior evidence), but a Unity Editor install alone does not provide Unity Gaming Services credentials or a linked cloud project — those require a Unity account/organization decision this agent does not make unilaterally (this agent's own operating rules explicitly prohibit creating or linking accounts on the product owner's behalf).
- This environment has real outbound internet access (confirmed: `curl https://www.google.com` → `HTTP 200`) but exactly one outbound network path — no second machine, VPN, or cloud instance is available to this agent to provide a genuinely distinct second network.
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §12.1/§12.2 are both `[OPEN]`, explicitly deferred to this spike and `ODY-S02-002`/`003` — confirmed by `Read`.

### Assumptions

- None stated as fact-assumptions beyond what section 1 of the report already flags explicitly (e.g., that measurements from this one machine's network path are not representative of a real player's home/mobile network — stated as an explicit caveat, not a hidden assumption).

## 5. Scope

### In scope

- `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/` (new): standalone `net10.0` console harness measuring real outbound-internet primitives (STUN NAT-traversal, repeated real UDP round trips, a real ~150 MB HTTPS transfer on a separate connection), and explicitly printing every roadmap §11.4 item it could not exercise, with the reason why.
- `Tools/Spikes/SP-03-InternetConnectivity/README.md` (new): explains the harness is not production code, what it does and does not measure, how to reproduce it, and its explicit scope/limitations.
- `Tools/Spikes/SP-03-InternetConnectivity/evidence/*.log` (new): raw stdout from two independent harness runs, backing the report's numbers.
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity.md` (this file).
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md` (separate report file — see §14 for the placement rationale, mirroring `SP-02`'s precedent).
- `docs/tasks/SLICE-02_BACKLOG.md` §3 — update only the `ODY-S02-002` row (Status, Planning mode).

### Out of scope

- Any production networking code, relay/rendezvous SDK integration, or change to `Odyssey.Networking`/`ISessionTransport` — this remains future `ODY-S02-002`(implementation, if any)/`ODY-S02-003`/`004` scope, not this task's.
- Selecting/pinning a relay/rendezvous stack as a binding decision, or fixing `TransportTimeoutPolicy`/`SessionEndpoint` as normative — this task produces a recommendation and proposals only (report §3/§4/§5); the actual decision is `ODY-S02-003`'s ADR.
- Amending `ADR-015` content or status — if a nonconformance had been found, this task would stop and flag it, not edit the ADR (none was found; see the report §6).
- Creating, linking, or configuring any Unity Gaming Services project, cloud account, or third-party relay/rendezvous SaaS account or credentials, per this agent's own operating rules against creating accounts on the product owner's behalf.
- Wiring the spike harness into `DotNet/Odyssey.Core.sln`, `.github/workflows/ci.yml`, or any repository script.
- Snapshot/delta/reconnect protocol (`ODY-S02-004`), identity/permissions code (`ODY-S02-005`/`006`).
- Any change to `ODY-S02-001`, `ADR-015`, or `docs/tasks/completed/`.

### Allowed paths

```text
Tools/Spikes/SP-03-InternetConnectivity/**
docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity.md
docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: the harness lives entirely under `Tools/Spikes/`, outside every `Packages/com.odyssey.*` module and outside `ADR-001`'s dependency matrix; it does not reference `Odyssey.Networking`, `Odyssey.Application`, or `ISessionTransport` at all — it measures raw network primitives, not the Application-layer port.
- Authoritative-state and transaction boundary: not applicable — no persisted or authoritative state is touched by this task.
- Serialization / compatibility boundary: not applicable — the harness exchanges only raw STUN protocol bytes and plain HTTPS payload bytes, no product DTO or codec is involved.
- Time / RNG rule: the harness is a standalone spike tool outside `Packages/com.odyssey.*/Runtime`, so `ADR-008`'s forbidden-global-API scan does not apply to it (confirmed: `scripts/verify-test-structure.ps1`'s `Test-ForbiddenGlobalApis` only scans `Packages/com.odyssey.*/Runtime`) — it uses `System.Diagnostics.Stopwatch` and `Random.Shared` directly, matching `SP-02`'s harness's own precedent of not routing spike-only code through `IWallClock`.
- Unity / thread / lifetime rule: not applicable — pure .NET console app, no Unity/IL2CPP involvement (confirmed: no Unity scene, asmdef, or Player build is part of this task's deliverable).
- Dependency / licensing rule: no new third-party package dependency — the harness uses only BCL types (`System.Net.Sockets`, `System.Net.Http`).
- Security / privacy / redaction rule: not applicable — no secret, credential, or hidden campaign data is touched; the harness makes only outbound calls to public, unauthenticated third-party services (Google STUN, Cloudflare speed-test edge).
- Performance or platform constraint: not applicable.
- Other: the harness must not create, link, or authenticate against any account or credentialed service, per this agent's own operating rules (§5 Out of scope).

## 7. Expected behavior

### Scenario 1 — STUN external-address discovery

**Given** this machine, with no inbound port forwarding configured
**When** the harness sends a real RFC 5389 STUN Binding Request to a public STUN server
**Then** it receives a Binding Success Response carrying this machine's externally-visible address/port, proving outbound NAT traversal works without any manual port-forwarding configuration.

### Scenario 2 — repeated real round trips as a reconnect-latency proxy

**Given** the same real network path
**When** the harness performs ten independent STUN exchanges, each on a freshly opened socket
**Then** all ten succeed, and the harness reports the real min/max/average round-trip latency across them.

### Scenario 3 — a real ~150 MB transfer, separate from the control-plane-shaped exchange

**Given** a connection distinct from the STUN exchanges above
**When** the harness downloads 150 MB (as three real 50 MB chunks, per the empirically discovered per-request size limit of the endpoint used) over HTTPS
**Then** all 150 MB arrive intact, and the harness reports real elapsed time and throughput.

### Scenario 4 — explicit non-verified checklist

**Given** the roadmap §11.4 checklist items this environment cannot exercise (live relay session, join code, second real peer, host-disconnect, access-descriptor expiry/renewal, a genuinely second network)
**When** the harness runs
**Then** it prints an explicit `NOT_VERIFIED` line for each, with the concrete reason, so the gap is visible in the raw evidence log itself, not only in report prose.

### Required invariants

- No scenario silently substitutes a loopback/in-memory simulation for a checklist item it cannot really test — every unmeasurable item is explicitly labeled `NOT_VERIFIED`, both in the harness's own printed output and in the report.
- Every number in the report is either printed directly by the harness or a simple arithmetic derivation (min/max/average) of numbers the harness printed, never an estimate presented as measured.

## 8. Deliverables

- Production code: None.
- Tests: None (spike-only harness, matching `SP-02`'s precedent of not registering `TC-*` IDs for scenario-level pass/fail lines).
- Scripts / CI: None — the harness is not wired into any CI-run script, matching `SP-02`'s precedent (a CI job making live third-party network calls on every PR would be both unreliable and out of scope for this task).
- Configuration: `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/SP03.Harness.csproj` (new, standalone, not added to `DotNet/Odyssey.Core.sln`).
- Documentation: this task contract, the spike report (`docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`), the harness `README.md`, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-002` row status.
- Generated evidence or build artifacts: `Tools/Spikes/SP-03-InternetConnectivity/evidence/run-2026-08-25-01.log`, `run-2026-08-25-02.log` — raw stdout from two independent harness runs.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. The harness performs real (not simulated) outbound network calls over the actual internet — no loopback stand-in presented as external connectivity evidence.
2. Every roadmap §11.4 checklist item this environment cannot exercise is explicitly enumerated as `NOT_VERIFIED`, with a concrete reason, both in the harness's own output and in the report — not silently omitted.
3. The report's every measured number is either printed directly by the harness or a straightforward arithmetic derivation from printed numbers, cross-checked against the saved evidence logs.
4. The report gives a relay/rendezvous stack recommendation for `ODY-S02-003`, explicitly labeled as a recommendation (not a decision), with its confidence level stated honestly given what could and could not be empirically validated.
5. The report gives non-binding proposals responding to `ADR-015` §12.1 (`TransportTimeoutPolicy`) and §12.2 (`SessionEndpoint`), each explicitly not implemented or fixed as normative by this task.
6. `ADR-015` is not modified by this task; no nonconformance with it was found (or, if one had been, this task would have stopped and flagged it instead of silently editing the ADR).
7. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass unchanged (the spike harness is outside that solution).
8. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
9. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

None registered (spike-only evidence, per `SP-02`'s precedent — see §8).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/`: `dotnet build -c Release`, then run the built executable twice independently; save both runs' stdout as evidence.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity code is touched by this task.
- Network topology or database fixture: real outbound internet access from the development machine (no fixture — see §4 for what is and is not available).
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Any CI-run execution of the spike harness itself (manual evidence only, per §8).
- Any Unity Player/IL2CPP build or run (no Unity code in this task's deliverable).

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no production code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the harness is fully self-contained and referenced by nothing else in the repository.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new third-party package dependency was introduced; the harness uses only BCL types already available in the `net10.0` SDK. It makes outbound network calls to two public, unauthenticated third-party services (Google's public STUN servers, Cloudflare's public speed-test edge) — neither requires an account, API key, or paid tier, and neither is a production dependency of this repository.

## 13. Security, privacy, and hidden information

- Data classes handled: None classified as `Secret`/`HiddenGameplay` per `ADR-010` §10 — the harness exchanges only its own machine's externally-visible network address (via STUN) and public speed-test payload bytes; no credential, campaign content, or personal data is touched.
- Trust boundaries: the harness makes outbound-only calls to public, unauthenticated third-party services; it opens no listening socket and accepts no inbound connection.
- Authorization / audience checks: Not applicable — no permissions model exists at this stage.
- Redaction requirements: Not applicable — no error/log redaction contract is exercised by this task.
- Log-safe fields: the harness prints its own machine's externally-visible IP:port (via STUN) to stdout/evidence logs — this is the same information any inbound network connection to this machine would already reveal to its counterparty, not a new disclosure; evidence logs are retained under `Tools/Spikes/`, a non-authoritative spike-evidence location, matching `SP-02`'s precedent.
- Abuse / malformed input limits: Not applicable — the harness is a client only, never a server; it validates STUN response structure defensively (bounds-checked parsing) before use.
- Security tests: Not applicable — no security-relevant production code is introduced.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2's triggers, not presumed from `SP-02`'s precedent alone. This task does not change any production module (zero `Packages/com.odyssey.*` or `DotNet/Projects/**` files touched); does not introduce or change an Application port, public DTO, event, command, persisted schema, protocol, manifest, package, build profile, or migration (the harness's `SessionEndpoint`/`TransportTimeoutPolicy` proposals in the report are non-binding prose, not code); and does not itself change any already-accepted decision — `ADR-015` remains unmodified (§9 AC-6). It investigates whether a real internet path exhibits certain measurable properties, without changing any accepted contract, matching `SP-02`'s own §14 reasoning for the same mode almost exactly. It has one clear, linear implementation path (investigate feasibility during preflight → build harness → run twice → write report → update backlog row) and completes in one focused pull request, matching every positive `PLANS.md` §1.1 criterion. The one difference from `SP-02` — that a real environment-feasibility question (whether live relay/dual-network testing was possible at all) had to be resolved before the harness's shape was known — was resolved during preflight/research (§4), not mid-implementation; once resolved, the remaining implementation was linear, so it does not independently push this task into ExecPlan territory the way an implementation-path uncertainty discovered *during* coding would.
- ExecPlan path: Not required.
- Expected pull request count: 1 (single Draft PR covering the harness, evidence, report, and backlog row update).
- Milestone or sequencing constraints: depends on `ODY-S02-001` (merged, PR #38) per `SLICE-02_BACKLOG.md` §5. Blocks `ODY-S02-003` (Rendezvous/Relay Strategy ADR) from being marked `Accepted` on a candidate stack before this spike's report exists, per that same section's ordering.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, the spike report, the harness `README.md`, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-002` row only).
- Documents that must not change: `ADR-001`–`015`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`, `ProjectSettings/ProjectSettings.asset` (this task inspects `cloudProjectId` but does not set it).
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None — the `SessionEndpoint`/`TransportTimeoutPolicy` proposals in the report are explicitly non-binding and do not change `ADR-015` or any code.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required by this task; existing suite unaffected).
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

- `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/SP03.Harness.csproj`, `Program.cs` — new.
- `Tools/Spikes/SP-03-InternetConnectivity/README.md` — new.
- `Tools/Spikes/SP-03-InternetConnectivity/evidence/run-2026-08-25-01.log`, `run-2026-08-25-02.log` — new.
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity.md` (this file), `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-002` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build -c Release` (harness) | Passed | 0 warnings, 0 errors. |
| Harness run 1 | Passed | Scenarios 1–3 all `PASS=True`; see `evidence/run-2026-08-25-01.log`. |
| Harness run 2 | Passed | Scenarios 1–3 all `PASS=True`; see `evidence/run-2026-08-25-02.log`. |
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors, unaffected by the spike (outside the solution). |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 147/147, 0 failed — unchanged from `ODY-S02-001`'s closing state. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Harness makes real UDP/HTTPS calls to public external servers; no loopback stand-in used. |
| AC-2 | Passed | Scenario 4 prints six `NOT_VERIFIED` lines with reasons; report §2/§8 restate them. |
| AC-3 | Passed | Report §2's tables are drawn directly from `evidence/run-2026-08-25-01.log`/`-02.log`. |
| AC-4 | Passed | Report §3 recommends Unity Relay, explicitly labeled as unconfirmed end-to-end. |
| AC-5 | Passed | Report §4/§5 give proposals for `SessionEndpoint`/`TransportTimeoutPolicy`, explicitly non-binding. |
| AC-6 | Passed | `ADR-015` file untouched by this task's diff (see diff-scope check). |
| AC-7 | Passed | See Validation results table above — all four commands pass. |
| AC-8 | Passed | `git status --porcelain` shows only `Tools/Spikes/SP-03-InternetConnectivity/**`, this task contract, its report, and `SLICE-02_BACKLOG.md` — exactly §5's Allowed paths. |
| AC-9 | Pending | PR [#39](https://github.com/odyssey-services/Odyssey_VTT/pull/39) opened as Draft; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- Blocker (structural, documented not silently bypassed): most of roadmap §11.4's checklist could not be empirically exercised in this environment — no Unity Gaming Services project is linked to this repository, and no second real network is available to this agent. This is not a blocker this task can resolve itself (per its own explicit instruction and this agent's operating rules against creating accounts); it is documented in full in the report (§2, §8) for the product owner's decision.
- Open decision (the product owner's, not this task's): whether to provision real relay/rendezvous credentials and re-run a follow-up spike with a genuinely second network before `ODY-S02-003` marks its ADR `Accepted`, or to accept this report's recommendation at its stated lower confidence level. Report §8 states this explicitly.
- Risk: the relay/rendezvous stack recommendation (Unity Relay) rests on document-criteria fit and structural compatibility, not on the kind of direct measurement `SP-02`'s recommendation had — `ODY-S02-003` must not treat this report as equivalent-strength evidence to `SP-02`'s; the report itself says so in section 3's closing paragraph.
