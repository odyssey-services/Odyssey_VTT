# Odyssey VTT — SLICE-02 Network Prototype Prerequisites Backlog

**Status:** Prerequisite backlog — Draft, no child task activated
**Slice:** `SLICE-02 — Network Prototype (prerequisites)`
**Parent task:** `docs/tasks/active/ODY-S02-000_SLICE_02_Network_Prototype_Prerequisites.md`
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 11.2/11.3's prerequisite list into small, reviewable tasks. It does **not** add product features, and it does **not** cover the `SLICE-02` vertical-slice implementation described in roadmap section 11.6 (GM hosts → player joins → GM assigns a role → player receives the permitted scene → player moves a token → host validates the command → both clients see the same result → player loses connection → player reconnects → player receives current state without re-applying the command). That implementation work begins only in a **future backlog revision**, created once all seven criteria in section 2 below are satisfied.

This revision's only outcome is five accepted ADRs (Transport Abstraction, Rendezvous/Relay Strategy, Snapshot/Delta/Reconnect, Identity Baseline, Permissions Baseline) and two complete, owner-reviewed technical spike reports (`SP-03 — Internet Connectivity`, `SP-04 — Hidden Data Boundary`).

## 2. Slice exit criteria (this backlog revision only)

This prerequisite backlog revision is complete only when all of the following are proven:

1. ADR — Transport Abstraction is `Accepted`.
2. ADR — Rendezvous/Relay Strategy is `Accepted`.
3. ADR — Snapshot/Delta/Reconnect Model is `Accepted`.
4. ADR — Identity Baseline is `Accepted`.
5. ADR — Permissions Baseline is `Accepted`.
6. `SP-03 — Internet Connectivity` spike report is complete and owner-reviewed.
7. `SP-04 — Hidden Data Boundary` spike report is complete and owner-reviewed.

These are **not** the full `SLICE-02` exit criteria (roadmap section 11.7). The full slice exit criteria — including "прototype works over the internet, not just localhost," "host is the sole authority," "duplicate delivery does not repeat the operation," and the other roadmap section 11.7 conditions — apply only once the vertical-slice implementation backlog (a separate future revision, created only after this one closes) is also complete.

## 3. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-S02-001` | ADR: Transport Abstraction | In Review | None | ExecPlan | `ISessionTransport` interface, in-process/mock transport for automated tests, reliable + optional/unreliable channel shape, message framing, protocol version handshake, timeout/retry policy — stack-agnostic, does not select the concrete relay/rendezvous provider |
| 2 | `ODY-S02-002` | Technical Spike SP-03: Internet Connectivity | In Review | 001 | Brief plan | Report: at least two real external connections through a candidate relay-first flow — host without manual port forwarding, join by code/invite, authenticated relay session establishment, reconnect after interruption, host-disconnect behavior, access-descriptor expiry/renewal, 100–200 MB asset transfer separate from gameplay traffic — with measurements, feeding the Rendezvous/Relay Strategy ADR's decision. Delivered as real measurements of the achievable subset (NAT traversal, repeated round trips, large-payload transfer) plus an explicit, reasoned `NOT_VERIFIED` list for the rest (no linked Unity Gaming Services project, no second real network available) — see the report for the full gap and its recommendation's resulting confidence level |
| 3 | `ODY-S02-003` | ADR: Rendezvous/Relay Strategy | In Review | 002 | ExecPlan | Unity Relay fixed as `Accepted`, honestly qualified with a normative pre-production-integration empirical gate given `SP-03`'s partial (not full) coverage — unlike `ADR-011` v1.0 section 12.1's clean `SP-02`-backed closure, `ADR-016`'s Status line itself states the qualification and section 9 enumerates all 8 `SP-03` `NOT_VERIFIED` items as named operational risks, not one collapsed sentence |
| 4 | `ODY-S02-004` | ADR: Snapshot/Delta/Reconnect Model | In Review | 001 | ExecPlan | Scene snapshot contract, delta batch/operation shape, gap detection, duplicate-delta handling, late join, disconnect/reconnect flow, delta-or-full-snapshot fallback rule — fixes a bounded host-side delta buffer plus full-snapshot fallback as the reconnect model (not always-full-snapshot), buffer size left as an implementation parameter; explicitly disambiguates network `ProjectionSnapshot` from `ADR-012`'s persistence `Snapshot` and from `AppliedCommands` dedup; visibility/redaction explicitly out of scope (`ODY-S02-006`) |
| 5 | `ODY-S02-005` | ADR: Identity Baseline | Draft | None | Not yet determined | Stable `UserId`, dev identity for local/automated tests, minimal Supabase Auth integration or an approved mock boundary, the rule that a JWT never becomes part of campaign state, and confirmation that no service-role key ever reaches the client |
| 6 | `ODY-S02-006` | ADR: Permissions Baseline | Draft | 005 | Not yet determined | Main GM / Player / Observer roles, read/action permission check, redacted scene projection per connection, the rule that a revoked permission removes the corresponding data from the current client state |
| 7 | `ODY-S02-007` | Technical Spike SP-04: Hidden Data Boundary | Draft | 004, 006 | Not yet determined | Report: a real test proving a host-hidden object never reaches a Player's snapshot, delta, runtime state, local cache, or diagnostic export — and that granting/revoking permission correctly adds/removes it from client state |

"Planning mode" is intentionally left "Not yet determined": each child task's Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` section 1, not pre-decided by this scaffold.

## 4. Task boundaries

### ODY-S02-001 — ADR: Transport Abstraction

Defines `ISessionTransport` (the Application-level port `Odyssey.Networking` implements, per `ADR-001` section 6.6), an in-process/mock transport implementation for automated tests only, the shape of a reliable ordered channel and an optional/unreliable preview channel, message framing, protocol version handshake, and timeout/retry policy — per `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` sections 4 (architecture), 5 (channels), 10 (version negotiation), and 11 (envelopes). Does not select a concrete relay/rendezvous provider or internet-transport implementation — that is `ODY-S02-003`'s decision, informed by `ODY-S02-002`'s empirical findings. Does not define the snapshot/delta/reconnect protocol running on top of these channels — that is `ODY-S02-004`.

### ODY-S02-002 — Technical Spike SP-03: Internet Connectivity

Investigates real external connectivity through at least two different networks, per roadmap section 11.4: hosting without manual port forwarding, join by code/invite metadata, authenticated relay session establishment, reconnect after interruption, host-disconnect behavior, access-descriptor expiry and renewal, and a 100–200 MB test-asset transfer kept separate from gameplay traffic. Produces a report with a recommended relay/rendezvous stack, reconnect model, and operational constraints. Does not implement production networking code. Direct IP/LAN is explicitly not the MVP transport and must not become a hidden architectural dependency of this spike's own harness.

### ODY-S02-003 — ADR: Rendezvous/Relay Strategy

Fixes the concrete relay/rendezvous stack, reconnect model, and operational constraints as `Accepted`, on `SP-03`'s empirical recommendation — the ADR is not marked `Accepted` on a candidate stack before `SP-03`'s report exists, the same ordering discipline `SLICE-01` used for `ADR-011`/`SP-02`. Does not redefine the transport abstraction interface itself (`ODY-S02-001`'s scope) or the snapshot/delta/reconnect application-level protocol (`ODY-S02-004`'s scope) — this ADR only fixes which relay/rendezvous stack implements the already-defined abstraction.

### ODY-S02-004 — ADR: Snapshot/Delta/Reconnect Model

Defines the scene snapshot contract (initial/chunked snapshot, snapshot identity), delta batch shape and operations, gap detection, duplicate-delta handling, late join, the disconnect/reconnect flow, and the delta-or-full-snapshot fallback rule, per `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` sections 15 (ordering/revisions), 16 (snapshot protocol), 17 (delta protocol), and 18 (reconnect). Depends on `ODY-S02-001` for channel semantics (reliable vs. unreliable delivery), not on the concrete relay choice `ODY-S02-003` fixes. Does not define visibility/redaction rules for what a given connection is allowed to see in its snapshot/delta — that is `ODY-S02-006`'s scope; this ADR defines the mechanism, not who receives what.

### ODY-S02-005 — ADR: Identity Baseline

Defines a stable `UserId`, a dev identity usable for local/automated tests without a live auth provider, the minimal Supabase Auth integration shape or an explicitly approved mock boundary, the rule that a JWT never becomes part of campaign state (`campaign.db`, `manifest.json`, `.odcamp`, or backup — extending the same `PE-INV-010`-style boundary `ADR-014`/`21_Security_And_Privacy` already established for the owner key), and confirmation that no Supabase service-role key ever reaches the client. Does not depend on `ODY-S02-001`–`004`; identity/auth is orthogonal to transport/session-protocol shape. Does not define permission/role checks against this identity — that is `ODY-S02-006`.

### ODY-S02-006 — ADR: Permissions Baseline

Defines the Main GM / Player / Observer role model, the read/action permission check performed host-side, redacted scene projection per connection, and the rule that revoking a permission removes the corresponding data from the current client state — the technical baseline subset of `07_Permissions_Odyssey_VTT_v0.7.md`'s already-documented `PERM-INV-001`–`012`/`RolePreset`/`MainGM` model that roadmap section 11.3 scopes this stage to, not that document's full generality (arbitrary `PermissionKey`/`Scope`/delegation system). Depends on `ODY-S02-005` (a permission check needs a stable actor identity to check against). No prior ADR defines any part of this — confirmed by reading `ADR-002` in full, which mentions "permissions" only as a generic authoritative-check concept, not a concrete role model — so this is new ADR content, not an extension of an existing one.

### ODY-S02-007 — Technical Spike SP-04: Hidden Data Boundary

Builds a real test where the host has a hidden object and a Player connection: proves the object is absent from the Player's snapshot, absent from delta, absent from runtime state, absent from local cache, and absent from diagnostic export; then proves the object appears after permission is granted and is removed after permission is revoked — per roadmap section 11.5. Depends on `ODY-S02-004` (needs the snapshot/delta mechanism to test against) and `ODY-S02-006` (needs the permissions/redaction model to test against). Does not implement production networking or permissions code — the spike proves the already-fixed ADRs' contracts are testable and correct as specified, the same empirical-verification role `SP-02` played for `SLICE-01`'s persistence ADRs.

## 5. Dependency rules

- `ODY-S02-001` has no dependency; it is the foundational transport-abstraction decision. `ODY-S02-002` and `ODY-S02-004` both build on the channel/framing shape it defines.
- `ODY-S02-002` depends on `ODY-S02-001` (the spike needs a transport abstraction shape to prototype a candidate relay-first flow against) and must complete — with a real, owner-reviewed report — **before** `ODY-S02-003` is marked `Accepted`. Roadmap section 11.4 states the spike "должен подтвердить конкретный Relay/rendezvous stack" — the ADR cannot responsibly fix that choice as `Accepted` ahead of the evidence that is supposed to confirm it. This mirrors `SLICE-01`'s `SP-02`→`ADR-011` v1.1 ordering exactly: `SP-02` ran and reported before the SQLite provider-library question was closed, not after.
- `ODY-S02-003` depends on `ODY-S02-002`'s completed report. It does not depend on `ODY-S02-004` or `ODY-S02-006` — the relay/rendezvous stack choice is independent of the application-level sync protocol and the permissions model.
- `ODY-S02-004` depends on `ODY-S02-001` (channel semantics). It has a practical, non-blocking relationship with `ODY-S02-003` — the concrete relay stack may have latency/ordering characteristics worth accounting for in the reconnect model — but this backlog does not require `ODY-S02-003` `Accepted` before `ODY-S02-004` begins; the two should be reconciled before either is finalized if their content conflicts, the same non-blocking-but-reconcile pattern `SLICE-01_BACKLOG.md` used between `ODY-S01-002` and `ODY-S01-003`.
- `ODY-S02-005` may begin independently of `ODY-S02-001`–`004`; identity/auth is orthogonal to transport and session-protocol shape.
- `ODY-S02-006` depends on `ODY-S02-005` (a permission check needs a stable actor identity to check against).
- `ODY-S02-007` depends on `ODY-S02-004` and `ODY-S02-006` both being `Accepted`, since the spike specifically exercises the snapshot/delta mechanism (`004`) under the permissions/redaction model (`006`). This is the direct analogue of `SLICE-01`'s `ODY-S01-005` (`SP-02`) depending on `ODY-S01-001`/`002` being `Accepted` before it could meaningfully exercise them under failure scenarios.

## 6. Global non-goals

This backlog revision excludes:

- Networking implementation code, transport prototype implementation, relay/rendezvous vendor integration, and Supabase Auth integration as executable code;
- Lobby, session-join, role-assignment UI, scene-sync runtime, and movement-command runtime;
- The `SLICE-02` vertical slice itself (roadmap section 11.6: GM hosts, player joins, role assignment, scene sync, movement, validation, reconnect) — deferred entirely to a future implementation backlog revision, created only after all five ADRs in section 3 above are `Accepted`;
- Any ADR content — each ADR's content is authored in its own child task, one at a time, by a separate future task activation; this backlog only organizes and sequences them, it does not decide any technical question itself;
- Authoring `Documentation/18_Account_And_Identity.md`, which roadmap section 11.2 names as a prerequisite document but which does not exist anywhere in the repository (confirmed by `ODY-S02-000`'s own verified-facts section) — whoever activates `ODY-S02-005` (Identity Baseline) must either source its content from `06_Networking_and_Session_Sync` section 6 / `21_Security_And_Privacy`, or flag the gap to the product owner; this backlog does not resolve that question itself;
- Public release or compatibility promises to end users.

## 7. Backlog change control

- New work requires a new `ODY-S02-XXX` task contract.
- A task may be split before implementation by updating this backlog and, if a governing ExecPlan exists for that specific child task, that ExecPlan too.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review.
- This backlog does not replace task acceptance criteria or ADR content; it does not itself decide any technical question.
- The `SLICE-02` implementation backlog (vertical slice) is a separate future backlog revision, created only after all five ADRs listed in section 3 are `Accepted` and both spike reports are complete and owner-reviewed; it is entirely out of scope for this revision.
