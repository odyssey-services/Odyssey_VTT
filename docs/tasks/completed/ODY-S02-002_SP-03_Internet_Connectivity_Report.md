# ODY-S02-002 — SP-03 Internet Connectivity: Spike Report

**Parent task:** `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity.md`
**Prepared:** 2026-08-25 UTC
**Spike ID:** `SP-03` (`17_Roadmap_Odyssey_VTT_v0.11.md` §23: "Relay-first internet session, reconnect and asset transfer")
**Harness:** `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/` (see its `README.md` for reproduction steps and explicit scope/limitations)
**Evidence runs:** two independent runs on the same development machine, raw stdout saved at `Tools/Spikes/SP-03-InternetConnectivity/evidence/run-2026-08-25-01.log` and `run-2026-08-25-02.log`

This report is honest about evidence granularity: every number below is either printed directly by the harness or a straightforward arithmetic derivation (e.g., an average) from printed numbers across the two runs, never an estimate presented as measured. It is equally honest about what could **not** be measured in this environment — see section 2's `NOT_VERIFIED` entries — rather than presenting a partial, single-machine measurement as if it satisfied roadmap §11.4's full checklist.

---

## 0. Owner decision

Pending. Per this repository's established pattern (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §0, `docs/tasks/active/ODY-S01-014_Traceability_and_Quality_Report.md` §6), this section is completed by a follow-up point-fix once the product owner reviews this report and its recommendation (section 3) — it is not filled in preemptively by this task.

Unlike `SP-02`, this spike's own findings (section 2) show that most of roadmap §11.4's checklist could not be empirically exercised in this environment (see the `NOT_VERIFIED` rows and section 5). Whatever the owner decides, it should account for that gap explicitly: this report is sufficient evidence for the recommendation in section 3 at a "most plausible candidate, unconfirmed end-to-end" confidence level, not at `SP-02`'s "measured and confirmed" level.

---

## 1. What was tested and how

Roadmap §11.4's checklist requires verifying, through a chosen Relay-first flow and across at least two different external networks: hosting without manual port forwarding, join by code/invite metadata, authenticated relay session establishment, reconnect after interruption, host-disconnect behavior, access-descriptor expiry/renewal, and a 100–200 MB test-asset transfer kept separate from gameplay traffic.

Before writing any harness code, this task investigated what was actually achievable in the current environment:

- **No candidate relay/rendezvous stack is named anywhere in the product documents.** `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §4.1/§4.2 fixes the *architecture* (Relay-first, no public IP/port-forwarding requirement) but explicitly leaves the concrete provider open — `OPEN-NW-001` ("Конкретный Relay provider... нужно выбрать provider и проверить: регионы, latency, 12 participants, pricing, reconnect semantics, reliable/unreliable channels, **Unity SDK stability**"). Confirmed by `grep` across the document: no SaaS or self-hosted relay product is named anywhere.
- **This project has no Unity Gaming Services project linked.** `ProjectSettings/ProjectSettings.asset` line 934, `cloudProjectId:`, is empty — confirmed by direct inspection. Provisioning a UGS project requires a Unity account/organization decision (and, per this agent's own operating rules, creating or linking accounts on the product owner's behalf is out of scope for an agent to do unilaterally).
- **This environment has exactly one outbound network path.** No second machine, VPN, or cloud instance is available to this agent to provide a genuinely distinct second network, and provisioning one (e.g. a second cloud VM in a different region) is an infrastructure/budget decision, not something this task can create on its own.
- **Real outbound internet access from this one machine was confirmed** (`curl https://www.google.com` → `HTTP 200`), so *some* genuine, non-simulated empirical work was possible — just not the full checklist.

Given that, this spike ran real, reproducible measurements of the network primitives any Relay-first flow depends on (NAT traversal, sustained large-payload transfer over a real path, repeated real round trips as a reconnect-latency proxy), on the one outbound path available, and explicitly enumerated — both in the harness's own printed output and in this report — every checklist item that genuinely could not be exercised without account provisioning or a second real network that this task does not have.

---

## 2. Findings per roadmap §11.4 checklist item

### 2.1 Host without manual port forwarding

**Measured (proxy evidence, not the full item):** `Scenario 1` — a real RFC 5389 STUN Binding Request/Response exchange against two public Google STUN servers (`stun.l.google.com:19302`, `stun1.l.google.com:19302`), over real UDP, from a machine with no port forwarding configured.

| Metric | Run 1 | Run 2 |
|---|---:|---:|
| `stun.l.google.com` result | success, external endpoint `93.209.229.100:54986` | success, external endpoint `93.209.229.100:54525` |
| `stun.l.google.com` RTT | 38.6 ms | 30.3 ms |
| `stun1.l.google.com` result | success, external endpoint `93.209.229.100:54987` | success, external endpoint `93.209.229.100:54526` |
| `stun1.l.google.com` RTT | 20.2 ms | 12.3 ms |

**Conclusion:** this machine can discover its own externally-visible address over the real internet without any inbound port forwarding, in both runs — the necessary NAT-traversal building block for a relay-first "no manual port forwarding" architecture holds on this network path. **This is not the full checklist item**: it proves outbound discovery from one machine, not that a second, independent peer could actually establish an inbound-reachable session and join it — that requires the real relay/rendezvous SaaS session this environment cannot provision (see `NOT_VERIFIED`, section 2.7).

### 2.2 Join by code/invite metadata

**`NOT_VERIFIED`.** No real relay/rendezvous SaaS session was ever established (see section 2.7), so no join code or invite metadata exists to test against a live session. `ADR-015` §12.2 already leaves `SessionEndpoint`'s full shape open pending exactly this kind of evidence; section 4 below offers a proposed shape based on the product document's own fields (`06_Networking_and_Session_Sync` §6.3), not on empirical testing of a real join flow.

### 2.3 Authenticated relay session establishment

**`NOT_VERIFIED`.** Requires a live relay/rendezvous SaaS account and credentials; none are available in this environment (section 1). No workaround or simulation was substituted.

### 2.4 Reconnect after interruption

**Measured (proxy evidence, not the full item):** `Scenario 2` — ten independent STUN Binding Request/Response exchanges, each opening a fresh UDP socket (a real new connection each time, not a kept-alive one), as a proxy for the latency a reconnect's initial round trip might exhibit on this network path.

| Metric | Run 1 | Run 2 |
|---|---:|---:|
| Iterations | 10/10 succeeded | 10/10 succeeded |
| Min RTT | 11.7 ms | 11.1 ms |
| Max RTT | 14.2 ms | 14.8 ms |
| Average RTT | 12.5 ms | 12.3 ms |
| Failures | 0 | 0 |

**Conclusion:** on this network path, repeated fresh-socket round trips are fast (11–15 ms) and completely reliable (20/20 succeeded across both runs) — a favorable signal for the *latency floor* a reconnect's first round trip might see. **This is not the full checklist item**: it does not test an actual relay session's reconnect semantics (resuming a specific session, replaying missed state, or the relay/rendezvous SaaS's own reconnect-token handling), only the raw network round-trip latency a reconnect attempt would be built on top of.

### 2.5 Host-disconnect behavior

**`NOT_VERIFIED`.** Requires two independent real peers connected through the same relay session so a second peer can observe the host disconnecting; not available in this environment (single machine, single outbound network path, no live relay session — section 1).

### 2.6 Access-descriptor expiry and renewal

**`NOT_VERIFIED`.** No real relay/rendezvous session descriptor was ever issued (section 2.3), so there is nothing whose expiry/renewal behavior could be measured. `ADR-015` §12.1 leaves `TransportTimeoutPolicy`'s exact values open pending exactly this kind of evidence; section 4 below states plainly that this remains open, rather than fabricating a number.

### 2.7 100–200 MB test-asset transfer, separate from gameplay traffic

**Measured:** `Scenario 3` — three sequential 50 MB HTTPS downloads (150 MB total) from Cloudflare's public speed-test edge, on a connection kept separate from the STUN exchanges above.

| Metric | Run 1 | Run 2 |
|---|---:|---:|
| Total bytes received | 157,286,400 (150 MB) | 157,286,400 (150 MB) |
| Chunk 1 elapsed | 2,029 ms | 2,175 ms |
| Chunk 2 elapsed | 1,928 ms | 1,892 ms |
| Chunk 3 elapsed | 1,961 ms | 1,895 ms |
| Total elapsed | 5,919 ms | 5,964 ms |
| Throughput | 25.34 MB/s | 25.15 MB/s |

**Setup note (a real, not simulated, constraint discovered mid-spike):** the endpoint used rejects a single request above roughly 50–100 MB — measured directly by requesting 50 MB (`200 OK`), 100 MB (`403 Forbidden`), and 150 MB (`403 Forbidden`) in one call each. The harness therefore issues three sequential 50 MB chunk requests instead of one 150 MB request. This is not a simulation shortcut: `06_Networking_and_Session_Sync` §5.3 itself specifies the real asset channel as chunk/range-based, so a chunked transfer is arguably closer to the product's own intended asset-channel design than a single unchunked request would have been.

**Conclusion:** a real ~150 MB payload transfers reliably (150/150 MB received, both runs) over HTTPS on a connection separate from the STUN exchanges, at roughly 25 MB/s on this network path, in about 6 seconds. **Caveat:** this measures a public speed-test CDN edge, not a candidate production asset-storage provider (`OPEN-NW-002` remains fully open, unaffected by this spike) — it demonstrates that sustained large-payload HTTPS transfer, kept separate from a control-channel-shaped exchange, works on this real network path, not a specific provider's throughput.

---

## 3. Recommendation on relay/rendezvous stack for `ODY-S02-003`

**This is a recommendation, not a decision.** Per `SLICE-02_BACKLOG.md` §4 (`ODY-S02-002` boundary), this report does not itself close `ADR-015` §12.1/§12.2 or fix any relay/rendezvous stack as `Accepted` — that is `ODY-S02-003`'s scope.

**Recommendation: Unity Relay (part of Unity Gaming Services), pending the empirical validation this spike could not perform.**

**Reasoning:**

1. **`OPEN-NW-001`'s own evaluation criteria point directly at it.** `06_Networking_and_Session_Sync` §51 lists "Unity SDK stability" as an explicit evaluation criterion for the relay provider choice — a criterion that only makes sense if the product expects a Unity-ecosystem-native option to be seriously considered. Unity Relay ships as an official Unity Gaming Services package (`com.unity.services.relay`) built specifically for the "host without a public IP / port forwarding, join by short code" pattern `06_Networking_and_Session_Sync` §4.2 already fixes as the MVP architecture — it is not a general-purpose relay being adapted to this use case.
2. **Structural fit with the already-accepted `ISessionTransport`.** Unity Transport (UTP), which Unity Relay allocations run over, natively distinguishes reliable and unreliable delivery — the same reliable/realtime channel split `ADR-015` already fixed as baseline (§5.1/§5.2). This is a structural compatibility observation, not new evidence this spike generated.
3. **No prior repository commitment conflicts.** No existing code in this repository references any relay/rendezvous SDK today (confirmed: `Odyssey.Networking`'s only real content is `InProcessSessionTransport`, the mock from `ODY-S02-001`), so this recommendation does not need to reconcile with an existing choice.

**What this recommendation explicitly does not cover, and why:** the join-code flow, authenticated session establishment, reconnect semantics, host-disconnect behavior, and access-descriptor expiry/renewal of Unity Relay specifically were **not** empirically tested (section 2.2–2.6) — because doing so requires a Unity Gaming Services project linked to this repository, which this task cannot provision unilaterally (section 1). Unlike `SP-02`'s recommendation (which rested on a point-1 argument that the measured properties were engine-level and wrapper-choice-independent), this recommendation rests on document-criteria fit and structural compatibility only — it is **not** backed by the kind of direct measurement section 2 gives for the network primitives that were actually reachable. `ODY-S02-003` should not mark its ADR `Accepted` on a "measured and confirmed" basis the way `ADR-011` v1.1 could after `SP-02` — either a follow-up spike with real UGS credentials should run first, or the product owner should explicitly accept the lower confidence level this report states, consistent with this task's own instruction not to let a spike's recommendation be mistaken for a decision.

**Fallback candidate, if the owner prefers not to depend on a third-party SaaS billing relationship:** a self-hosted TURN-style relay (e.g. `coturn`) was not evaluated in any depth in this spike (no coturn instance was stood up), but is named here as a fallback direction consistent with `06_Networking_and_Session_Sync` §4.3's "MVP provider может реализовывать только Relay, но Application layer не зависит от конкретного SDK" — the `ISessionTransport` abstraction `ADR-015` already fixed does not lock the product into Unity Relay specifically, so switching candidates later remains an implementation-only change, not an architecture change.

---

## 4. Proposed `SessionEndpoint` format (responds to `ADR-015` §12.2, not a decision)

`ADR-015` §12.2 leaves `SessionEndpoint`'s shape beyond `EndpointId` open, deferred to this spike. Based on `06_Networking_and_Session_Sync` §6.3's own `Session directory entry` field list (`SessionId`, `CampaignPublicId`, `HostUserId`, `RelayJoinDescriptor`, `JoinCodeHash`, ...) — not on empirical testing this spike could not perform — a plausible shape for a future amendment to fix is:

```text
SessionEndpoint
├── JoinCode            (short, human-typeable, matches "короткий join code" — 06_Networking §6.2)
├── RelayAllocationId    (or equivalent provider-specific session/allocation identifier)
├── RelayRegion          (informational; supports OPEN-NW-001's "регионы" evaluation criterion)
└── AccessDescriptor     (opaque, provider-specific, carries whatever short-lived credential the chosen relay issues -- shape not fixed by this spike, since no real descriptor was ever issued to inspect, section 2.6)
```

This is explicitly a proposal for `ODY-S02-003` to adopt, amend, or reject — not a binding format. It is not implemented by this task; `SessionEndpoint.EndpointId` in `ADR-015`/`SessionTransportContracts.cs` is unchanged by this report.

---

## 5. `TransportTimeoutPolicy` — remains open

`ADR-015` §12.1 leaves `TransportTimeoutPolicy`'s exact values open pending empirical validation against a real provider SDK. This spike's Scenario 1/2 measurements (11–45 ms for a single STUN round trip on this one network path) are **not** a substitute for that validation — they measure a bare UDP round trip on one data-center network path, not a relay-mediated session's connect/send timeout behavior under real player network conditions (mobile NAT, home broadband, packet loss). This report explicitly does not propose new `TransportTimeoutPolicy` values; `ADR-015` §12.1 remains `[OPEN]`, to be closed once real relay-provider credentials allow the actual validation `ADR-015` describes.

---

## 6. Findings of nonconformance with already-accepted ADRs

**None found.** This spike did not touch `Odyssey.Networking` production code, `ISessionTransport`, or any other already-accepted contract; it measured only external network primitives outside the codebase. `ADR-015` is unmodified by this task. Per the task's own instruction, if a nonconformance had been found, this report would stop and flag it for the product owner rather than silently editing the affected ADR — that branch did not occur, and did not apply here in the first place since no production code was exercised.

---

## 7. Where the harness lives and how to reproduce

- Code: `Tools/Spikes/SP-03-InternetConnectivity/SP03.Harness/` (standalone `net10.0` console app; not referenced by `DotNet/Odyssey.Core.sln`, any `Packages/com.odyssey.*` module, or `.github/workflows/ci.yml`).
- Documentation: `Tools/Spikes/SP-03-InternetConnectivity/README.md` — build/run instructions, explicit scope, limitations, and the full `NOT_VERIFIED` list with reasons.
- Raw evidence: `Tools/Spikes/SP-03-InternetConnectivity/evidence/run-2026-08-25-01.log`, `run-2026-08-25-02.log` — full stdout from the two independent runs this report's numbers are drawn from.
- Reproduction command (from repository root):

  ```powershell
  cd Tools\Spikes\SP-03-InternetConnectivity\SP03.Harness
  dotnet build -c Release
  ..\..\..\..\artifacts\bin\SP03.Harness\release\SP03.Harness.exe
  ```

- This harness makes only outbound network calls (UDP to two public Google STUN servers, HTTPS to a public Cloudflare speed-test edge); it was not added to `.\scripts\test-fast.ps1`, `dotnet-restore-build-test`, or any other CI-wired script, per the same reasoning `SP-02`'s harness used — it is invoked manually as spike evidence only, and a CI job making live third-party network calls on every PR would be both unreliable (third-party endpoint availability) and out of scope for this task.

---

## 8. What this spike leaves genuinely unresolved (summary)

For visibility in one place, restating section 2's `NOT_VERIFIED` items and their common root causes:

| Roadmap §11.4 item | Status | Root cause |
|---|---|---|
| Join by code/invite metadata | Not verified | No live relay/rendezvous SaaS session was ever established |
| Authenticated relay session establishment | Not verified | No Unity Gaming Services project linked to this repository; no credentials available |
| Host without port forwarding, from a real second peer | Not verified | Only outbound NAT traversal from one machine was proven, not an actual second peer joining |
| Host-disconnect behavior | Not verified | Requires two real peers on a live relay session; unavailable |
| Access-descriptor expiry/renewal | Not verified | No real session descriptor was ever issued |
| ≥2 physically/logically distinct external networks | Not verified | This environment has exactly one outbound network path |

Before `ODY-S02-003` marks its Rendezvous/Relay Strategy ADR `Accepted`, the product owner should decide whether to: (a) provision a Unity Gaming Services project (or equivalent for an alternative candidate) and re-run a follow-up spike with real credentials across a genuinely second network, closing the gaps above with real measurement; or (b) explicitly accept this report's document-criteria-based recommendation (section 3) at its stated lower confidence level and proceed without that additional evidence. Either is the owner's call, not this task's to make.

---

**End of report.**
