# SP-03 — Internet Connectivity spike harness

**This is not production code.** It does not implement, select, or wire in a production relay/rendezvous SDK, session directory, or transport provider. It exists only to generate reproducible empirical evidence for `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity.md` and its report, `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`.

- Not referenced by `DotNet/Odyssey.Core.sln`.
- Not referenced by any `Packages/com.odyssey.*` module.
- Not wired into `.github/workflows/ci.yml` or any repository script.
- Does not depend on `ISessionTransport` (`ADR-015`) or any `Odyssey.Networking` code — it measures raw network primitives (UDP STUN, HTTPS transfer) that any future relay/rendezvous implementation would ultimately sit on top of, not the Application-layer port itself.
- Safe to delete in its entirety without affecting any production build, test, or CI job.

## What it does, and — just as importantly — what it does not do

Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.4 asks `SP-03` to verify, through a chosen Relay-first flow, across at least two different external networks: hosting without manual port forwarding, join by code/invite metadata, authenticated relay session establishment, reconnect after interruption, host-disconnect behavior, access-descriptor expiry/renewal, and a 100–200 MB test-asset transfer kept separate from gameplay traffic.

This harness's actual environment — a single sandboxed development machine with exactly one outbound network path, no Unity Gaming Services project linked to this repository (`ProjectSettings/ProjectSettings.asset`'s `cloudProjectId` is empty, confirmed by inspection), and no live relay/rendezvous SaaS credentials of any kind — cannot exercise most of that checklist for real. Per this task's own instruction, this harness does **not** quietly substitute a loopback or in-memory simulation for what it cannot really test. Instead it measures what genuinely **is** achievable as real, reproducible, over-the-internet evidence, and prints an explicit `NOT_VERIFIED` line — both to stdout and in this document — for everything it could not exercise, with the concrete reason why.

### What it does measure (real, over the internet, not simulated)

1. **STUN external-address discovery** (`Scenario 1`) — a real RFC 5389 UDP Binding Request/Response exchange against two public Google STUN servers, proving this machine can discover its externally-visible address/port over the real internet without any inbound port forwarding configured. This is the NAT-traversal building block every relay-first flow depends on.
2. **Repeated real UDP round trips as a reconnect-latency proxy** (`Scenario 2`) — ten independent STUN exchanges, each opening a fresh socket, as a real (not simulated) measurement of round-trip latency variance for a control-plane-shaped exchange over this machine's real network path.
3. **A real ~150 MB HTTPS transfer, chunked, on its own connection** (`Scenario 3`) — three sequential 50 MB downloads from Cloudflare's public speed-test edge (`speed.cloudflare.com/__down`), summing to 150 MB, on a connection kept separate from the STUN exchanges above. Chunking was not a simulation shortcut: this endpoint empirically rejects a single request above roughly 50–100 MB (measured directly — 50 MB succeeds, 100 MB and 150 MB in one request both return `403`), and `06_Networking_and_Session_Sync` §5.3 itself specifies the real asset channel as chunk/range-based — so a chunked transfer is a closer match to the product's own asset-channel design than a single unchunked request would have been anyway.

### What it does NOT measure (printed by the harness itself as `NOT_VERIFIED`, with reasons)

- Join by code/invite metadata against a real relay/rendezvous SaaS.
- Authenticated relay session establishment.
- Hosting without manual port forwarding, from a real second peer's point of view (this harness only proved outbound NAT traversal from one machine, not that a second peer could actually join an inbound session).
- Host-disconnect behavior observed by a second real peer.
- Access-descriptor expiry and renewal (no real session descriptor was ever issued, since no relay session was ever established).
- At least two physically or logically distinct external networks (this harness has exactly one outbound path available).

All six root causes trace back to the same two facts: no Unity Gaming Services project is linked to this repository, and no second independently-networked machine is available to this agent. Provisioning either is a product-owner decision (a Unity account/organization link, and/or a second cloud instance/VPN), not something this harness or its author create unilaterally.

## How to reproduce

```powershell
cd Tools\Spikes\SP-03-InternetConnectivity\SP03.Harness
dotnet build -c Release
..\..\..\..\artifacts\bin\SP03.Harness\release\SP03.Harness.exe
```

(The build uses the repository-wide `Directory.Build.props` `UseArtifactsOutput=true` convention, so build output lands under the repository's `artifacts/` directory, already excluded from Git and from `REPO-POLICY-002`'s tracked-file scan — nothing under `artifacts/` is or should be committed.)

The program makes only outbound network calls (UDP to two public Google STUN servers, HTTPS to `speed.cloudflare.com`); it opens no listening socket and touches no file under the repository. Each run is self-contained.

Raw stdout from two independent runs used to produce the report's numbers is saved under [`evidence/`](evidence/) for reproducibility comparison (`run-2026-08-25-01.log`, `run-2026-08-25-02.log`). Re-running the harness will reproduce the same qualitative outcomes; exact latency/throughput numbers will vary with the machine's own network path and the third-party endpoints' load at the time, which is expected — the report treats these numbers as illustrative measurements of this one environment, not as a guarantee for a real player's home network.

## Scope and limitations (read before citing this evidence elsewhere)

- All measurements were taken from a single machine with a single outbound network path — likely a data-center/cloud network, not representative of a real player's home broadband or mobile NAT/ISP characteristics. Treat the absolute latency/throughput numbers as an existence proof (STUN and large-payload HTTPS transfer work over this real internet path) and an order-of-magnitude reference, not a promise about end-user conditions.
- STUN is not a relay: it proves external-address discovery, one necessary building block of most relay/rendezvous flows, but is not itself the authenticated relay session roadmap §11.4 asks for.
- The Cloudflare `speed.cloudflare.com` endpoint is a public speed-test utility, not a game-traffic relay; it is used here purely as a real, freely-reachable, large-payload HTTPS source to produce a genuine throughput measurement, not as a candidate production asset-storage provider (`OPEN-NW-002` in `06_Networking_and_Session_Sync` remains fully open and unaffected by this spike).
- This harness does not exercise Unity/IL2CPP; it is a pure .NET console app, matching the fact that no candidate relay SDK's actual Unity integration was tested (see `NOT_VERIFIED` above).

## Retention

This directory is retained as spike evidence per roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §23 ("Spike закрывается не кодом как таковым, а принятым решением и воспроизводимым доказательством") — the code stays as the reproducibility proof backing the report's claims, not as a stepping stone toward a production implementation. A future implementation task for the actual relay/rendezvous transport (`ODY-S02-002` implementation successor, if any, or `ODY-S02-003`'s eventual implementation) must not `ProjectReference` or otherwise depend on this project.
