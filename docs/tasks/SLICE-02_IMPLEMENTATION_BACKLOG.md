# Odyssey VTT — SLICE-02 First Network Implementation Backlog

**Status:** Implementation revision — Draft, no child task activated
**Slice:** `SLICE-02 — First Network (vertical slice implementation)`
**Parent task:** `docs/tasks/active/ODY-S02-008_SLICE_02_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-02_BACKLOG.md` (prerequisite ADR/spike revision — closed 2026-08-25, historical; not rewritten by this document)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 11.6 (the ten-step "Первая сеть" vertical slice) and section 11.7 (exit criteria) into small, reviewable implementation tasks. It is the implementation-revision that `docs/tasks/SLICE-02_BACKLOG.md` section 1 reserved for creation once its own (prerequisite) revision closed — which it did on 2026-08-25, with `ADR-015`–`019` all `Accepted` and both `SP-03`/`SP-04` spike reports owner-accepted (`SLICE-02_BACKLOG.md` §2.1).

This backlog does **not** itself implement anything. It only decomposes the vertical slice into ordered child tasks, each of which will be its own separate task contract and pull request, activated one at a time — the same convention `SLICE-01_IMPLEMENTATION_BACKLOG.md` and every prior backlog revision in this repository used. No child task contract file is created by this document; it only reserves numbers, titles, and boundaries.

Its sources of scope are, exclusively:

- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 (the ten-step vertical slice scenario) — private local reference, not committed to the repository.
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.7 (exit criteria).
- The already-`Accepted` ADRs governing each area: `ADR-015` (transport port), `ADR-016` (relay strategy, carrying a normative pre-integration gate — see section 2.1), `ADR-017` (snapshot/delta/reconnect), `ADR-018` (identity), `ADR-019` (permissions baseline).

No child task in this backlog reopens any decision `ADR-015`–`019` already made; each builds directly on those contracts as fixed.

## 2. Scope decisions requiring explicit justification

### 2.1 Real Unity Relay transport integration — **gated behind `ADR-016` §14, not started by default**

`ADR-016` §1 point 9/§14 fixed a **normative** (not advisory) condition: no production task may integrate the Unity Relay SDK into `Odyssey.Networking` before a dedicated follow-up empirical spike — real Unity Gaming Services credentials, two genuinely independent peers, two real networks — closes the gaps `SP-03` (`ODY-S02-002`) left `NOT_VERIFIED` (join-by-code against a live session, authenticated establishment, host-disconnect from a second real peer, access-descriptor expiry/renewal, a second real network). That follow-up spike was **not** commissioned as part of closing the prerequisite revision (`SLICE-02_BACKLOG.md` §2.1) — the product owner accepted `SP-03`'s report at its stated lower confidence level instead, deliberately, not by oversight.

**Decision:** this backlog reserves the real-transport swap as its own task (`ODY-S02-014`), explicitly marked `Blocked` (not `Draft`) until the `ADR-016` §14 follow-up spike exists and closes those gaps — a decision for the product owner to commission, not something this backlog schedules on its own authority. Every other child task in this revision is built and integration-tested against `ADR-015`'s already-accepted `InProcessSessionTransport`, which is real, functional code (not a stub), just not internet-reaching. This mirrors `SLICE-01_IMPLEMENTATION_BACKLOG.md` §2.1's narrowing of the migration runner to a registry baseline: build and prove what can honestly be proven now, defer what a real, named gap blocks, and say so plainly rather than quietly building against an unmet precondition.

**Consequence for slice closure:** roadmap §11.7's "сетевой прототип работает через интернет, а не только localhost" exit criterion **cannot** be satisfied until `ODY-S02-014` completes — and `ODY-S02-014` cannot start until the gate is met. This backlog's own closure task (`ODY-S02-015`) must report this honestly if the gate remains unmet at that time, the same way `SLICE-02_BACKLOG.md` §2.1 reported `SP-03`'s gap rather than glossing over it — not force a false "all exit criteria met."

### 2.2 Asset channel — **not a separate implementation task in this revision**

`06_Networking_and_Session_Sync` §5.3 (asset channel: signed access, chunk/range download, resume, checksum, expiry, audience auth) is explicitly excluded from `ADR-017`'s own scope (`ADR-017` §12) and is not named as a step in roadmap §11.6's ten-step scenario — the scenario never transfers a large asset. Roadmap §11.7's "asset transfer не блокирует критический игровой трафик в прототипе" exit criterion is satisfiable at the architecture level already established by `ADR-015` (reliable and realtime channels are already structurally separate, `ADR-015` §5.1/§5.2) without a dedicated asset-channel implementation — a full asset channel (signed URLs, chunking, resumable transfer) is a materially larger scope than this ten-step slice requires, and `OPEN-NW-002` (temporary asset storage provider) remains a fully separate, unresolved question this revision does not need to answer.

**Decision:** no dedicated asset-channel task is created. `ODY-S02-015` (closure gate) proves the "does not block critical traffic" criterion architecturally (channel separation already exists) rather than through an actual large-asset transfer scenario, and states this narrowing explicitly rather than silently reinterpreting the exit criterion.

### 2.3 `ODY-S02-007`'s harness types are not reused as production code

The `SP-04` hidden-data-boundary harness (`DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/`) is explicitly test-project-scoped (`ADR-019`-adjacent, not `ADR-019` itself, and its own README states it is not production code). `ODY-S02-010` (Scene Snapshot & Redacted Projection Delivery) writes a fresh, real `Odyssey.Application`-layer implementation of `ADR-017`/`ADR-019`'s contract — the harness proved the contract is implementable, not that its own minimal types are the production shape. This is stated explicitly so no future task assumes the harness can simply be "promoted" wholesale.

## 3. Slice exit criteria

`SLICE-02` (vertical-slice implementation) is complete only when all of the following, taken verbatim from roadmap §11.7, are proven:

1. Сетевой прототип работает через интернет, а не только localhost — **gated**, see section 2.1; not satisfiable until `ODY-S02-014` completes.
2. Host является единственным авторитетом состояния (host is the sole authority over state).
3. Duplicate delivery не повторяет операцию (duplicate delivery does not repeat the operation).
4. Reconnect восстанавливает назначенную сцену и роль (reconnect restores the assigned scene and role).
5. Version mismatch имеет понятную ошибку (a version mismatch produces a clear error).
6. Hidden data test проходит (the hidden-data test passes) — **already satisfied**, `SP-04`/`ODY-S02-007`, closed with the prerequisite revision (`SLICE-02_BACKLOG.md` §2.1, criterion 7). This backlog's closure task re-confirms it still holds against whatever production code this revision adds, not merely cites the prior spike result unchecked.
7. Relay не хранит campaign state (the relay does not store campaign state) — architectural consequence of `ADR-001`/`ADR-016` already accepted; re-confirmed, not re-decided, by `ODY-S02-015`.
8. Asset transfer не блокирует критический игровой трафик в прототипе (asset transfer does not block critical gameplay traffic) — satisfied architecturally per section 2.2, not via a dedicated asset-transfer task.
9. Выбранная стратегия зафиксирована ADR (the chosen strategy is fixed by an ADR) — **already satisfied**, `ADR-016`, closed with the prerequisite revision.

Criteria 6 and 9 are already met as of this backlog's creation (inherited from the closed prerequisite revision); criterion 1 is structurally gated (section 2.1) and may remain unmet at this revision's closure if the `ADR-016` §14 spike has not been commissioned by then — that is an honest, expected possible outcome, not a defect of this backlog's design.

## 4. Ordered backlog

| Order | Task ID | Roadmap step(s) | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S02-009` | 1–3 | Identity & Session Admission | In Review | None | ExecPlan | Dev/mock `UserId` assignment wired into a real (not test-only) admission flow; session directory/join-code minimal structure (`06_Networking` §6.3); Lobby state machine (Admitted → RoleAssigned, default Observer preset per §37.1); host-only `RolePreset` assignment restricted to the three `ADR-019` baseline roles (MainGM/Player/Observer), MainGM never reassignable — all over `InProcessSessionTransport`, hand-written `ADR-003` canonical JSON wire codecs |
| 2 | `ODY-S02-010` | 4 | Scene Snapshot & Redacted Projection Delivery | In Review | 009 | ExecPlan | Real `Odyssey.Application`-layer `ProjectionSnapshot`/`VisibilityPolicy`/`PermissionDecision` implementation (per `ADR-017`/`ADR-019`, not a reuse of `ODY-S02-007`'s test-only harness — section 2.3); scene assignment delivered to a newly admitted Player, correctly redacted |
| 3 | `ODY-S02-011` | 5–7 | Authoritative Command & Delta Broadcast | In Review | 010 | ExecPlan | Token-move command validated host-side (`ADR-002` pipeline + `ADR-019` action check, both points from `ADR-019` §6.1); `ProjectionDeltaBatch` broadcast to multiple connections; both clients converge to the same authoritative result |
| 4 | `ODY-S02-012` | 8–10 | Reconnect, Delta Continuity & Duplicate-Delivery Idempotency | In Review | 011 | ExecPlan | Real reconnect flow (`ADR-017` §9's 10 steps), permission recheck by current (not saved) state at reconnect (`ADR-017` §1 point 8), delta-buffer-vs-full-snapshot fallback (`ADR-017` §8), duplicate-batch dedup by `SequenceFrom`/`SequenceTo` (`ADR-017` §6) — satisfies exit criteria 3 and 4 |
| 5 | `ODY-S02-013` | 1–10 | Vertical Slice Integration | Draft | 009–012 | Not yet determined | The roadmap §11.6 ten-step scenario as one automated, reproducible end-to-end check over `InProcessSessionTransport`: host starts → player joins by code → role assigned → scene delivered → token moved → host validates → both clients converge → player disconnects → reconnects → resumes without replaying the command |
| 6 | `ODY-S02-014` | — | Real Transport Integration (Unity Relay) | **Blocked** — pending `ADR-016` §14 follow-up spike, not yet commissioned | 013 | Not yet determined | Swaps `InProcessSessionTransport` for a real Unity Relay-backed `ISessionTransport` implementation; the only task in this revision touching a real network. **Must not start** before the gate in section 2.1 is met — this row's `Status` reflects that, not an oversight |
| 7 | `ODY-S02-015` | — | SLICE-02 Acceptance and Closure Gate | Draft | 009–014 | Not yet determined | Traceability matrix, all nine roadmap §11.7 exit criteria checked with real evidence, honest reporting if criterion 1 remains unmet (section 2.1/3), owner acceptance — mirrors `ODY-S01-014`'s closure pattern |

"Planning mode" is intentionally left "Not yet determined" for every child task: each task's own Brief-plan-vs-ExecPlan decision is made when that task's own contract is authored, per `PLANS.md` section 1, not pre-decided by this scaffold — the same convention `SLICE-01_IMPLEMENTATION_BACKLOG.md` and both `SLICE-0X_BACKLOG.md` prerequisite revisions used.

No `ODY-S02-009`–`015` task contract file exists yet. This backlog only reserves their numbers, titles, and boundaries; each is created and activated as its own separate task, one at a time, when picked up.

## 5. Task boundaries

### ODY-S02-009 — Identity & Session Admission

Implements a real (not test-only) admission flow: a connecting actor is assigned a dev/mock `UserId` (`ADR-018` §5's approved boundary — real Supabase Auth remains out of scope, per `ADR-018` §5's own deferral), a minimal session directory entry is created (`06_Networking` §6.3's field subset actually needed: `SessionId`, `HostUserId`, `JoinCodeHash`, `Status`), the Lobby state machine admits a joining Player, and the host (MainGM by default) assigns a `RolePreset` restricted to the three `ADR-019` baseline roles. Does not implement scene delivery (`ODY-S02-010`), command handling (`ODY-S02-011`), or reconnect (`ODY-S02-012`). Uses `InProcessSessionTransport` (`ADR-015`) as the transport — no real network.

### ODY-S02-010 — Scene Snapshot & Redacted Projection Delivery

Implements the real `Odyssey.Application`-layer `ProjectionSnapshot` construction and `VisibilityPolicy`/`PermissionDecision` pipeline per `ADR-017` §4/§7 and `ADR-019` §6.2/§7 — a fresh implementation, not a reuse of `ODY-S02-007`'s test-only harness types (section 2.3). Delivers the scene a newly admitted Player is assigned, correctly redacted per their role. Does not implement delta/command handling (`ODY-S02-011`) or reconnect (`ODY-S02-012`).

### ODY-S02-011 — Authoritative Command & Delta Broadcast

Implements token-move command validation entirely host-side (`ADR-002`'s existing command pipeline, with the action-check point `ADR-019` §6.1 fixes), and `ProjectionDeltaBatch` broadcast to every connected, entitled connection (`ADR-017` §5). Proves both clients (or, in this revision's `InProcessSessionTransport`-based tests, both simulated connections) converge to the identical authoritative result — satisfying exit criterion 2 ("host is the sole authority"). Does not implement reconnect or duplicate-delivery handling (`ODY-S02-012`).

### ODY-S02-012 — Reconnect, Delta Continuity & Duplicate-Delivery Idempotency

Implements `ADR-017` §9's full 10-step reconnect flow, the permission recheck-by-current-state rule (`ADR-017` §1 point 8 — already fixed, not reopened here), the delta-buffer-vs-full-snapshot fallback (`ADR-017` §8), and duplicate-batch deduplication by `SequenceFrom`/`SequenceTo` range (`ADR-017` §6) — satisfying exit criteria 3 (duplicate delivery) and 4 (reconnect restores scene/role). Does not implement the real-transport-level reconnect semantics `ADR-016` §5 left for a real provider (that is `ODY-S02-014`'s concern once unblocked) — this task's reconnect is application-level, transport-agnostic, proven over `InProcessSessionTransport`.

### ODY-S02-013 — Vertical Slice Integration

Implements the roadmap §11.6 ten-step scenario as a single, automated, reproducible end-to-end check exercising every prior task's deliverable together, over `InProcessSessionTransport` — the same "integration proof, not a new feature" role `ODY-S01-013` played for `SLICE-01`. Does not introduce new networking behavior beyond what `ODY-S02-009`–`012` already implement.

### ODY-S02-014 — Real Transport Integration (Unity Relay)

**Blocked pending the `ADR-016` §14 follow-up empirical spike** — see section 2.1. Once unblocked, swaps `InProcessSessionTransport` for a real Unity Relay-backed `ISessionTransport` implementation and re-validates the vertical slice (or a materially equivalent scenario) against a genuinely real network. The only task in this revision that touches a real network or a real third-party SDK. Does not redefine `ISessionTransport`'s signature (`ADR-015`, unchanged) or the relay strategy decision (`ADR-016`, unchanged) — only implements against them.

### ODY-S02-015 — SLICE-02 Acceptance and Closure Gate

Produces a traceability matrix and quality report mirroring `ODY-S01-014`'s pattern, checks all nine roadmap §11.7 exit criteria against real evidence from `ODY-S02-009`–`014`, and records explicit product-owner acceptance. Must report honestly if criterion 1 (real internet, not just localhost) remains unmet because `ODY-S02-014` is still blocked at that time — not force a false "all criteria met" to close the revision prematurely. Does not implement new product behavior — closure/evidence only.

## 6. Dependency rules

- `ODY-S02-009` has no dependency — it is the foundational admission/identity task every other task in this revision builds on.
- `ODY-S02-010` depends on `ODY-S02-009` (a scene can only be delivered to an admitted, role-assigned actor).
- `ODY-S02-011` depends on `ODY-S02-010` (commands act on scene/entity state that must already be deliverable).
- `ODY-S02-012` depends on `ODY-S02-011` (reconnect resumes a session that must already support commands/deltas to have meaningful continuity).
- `ODY-S02-013` depends on all of `ODY-S02-009`–`012` (it is the integration proof exercising every prior deliverable together).
- `ODY-S02-014` depends on `ODY-S02-013` (the real-transport swap re-validates an already-proven-over-mock scenario) **and** on the external `ADR-016` §14 gate (section 2.1) — the latter is not a task in this backlog, but a precondition this backlog cannot itself satisfy.
- `ODY-S02-015` depends on all of `ODY-S02-009`–`014` (closure requires every deliverable, including the real-transport task if by then unblocked, or an honest report that it remains blocked).

## 7. Global non-goals

This backlog revision excludes:

- Real Supabase Auth integration — `ADR-018` §5 already deferred it; not reopened here.
- `AssistantGM`, delegation, arbitrary `PermissionKey`/`Scope`, ownership/control-based visibility, temporary permissions, `CampaignUserGroup` — all outside `ADR-019`'s own baseline scope (`ADR-019` §10), and so outside this revision's scope too.
- A dedicated asset channel (`06_Networking` §5.3) — see section 2.2.
- Character sheets, combat, dice, board rendering, or any content/rules-engine system — roadmap §11.6 does not include them; they belong to `SLICE-03`/Stage 4 (`17_Roadmap` §12) entirely.
- Any UI/UX polish beyond what is needed to prove the roadmap §11.6 scenario programmatically.
- Full `07_Permissions_Odyssey_VTT_v0.7.md` generality beyond `ADR-019`'s already-fixed baseline — not reopened by any task in this revision.
- Starting `ODY-S02-014` before its gate (section 2.1) is met, under any circumstance, including schedule pressure.

## 8. Backlog change control

- New work requires a new `ODY-S02-0XX` task contract; this document only reserves numbers `ODY-S02-009` through `ODY-S02-015`.
- A task may be split before implementation by updating this backlog (and, if a governing ExecPlan exists for that specific child task, that ExecPlan too), following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or any ADR's content; it does not itself decide any technical question beyond the three explicit scope decisions in section 2.
- The predecessor `docs/tasks/SLICE-02_BACKLOG.md` (prerequisite ADR/spike revision) is not rewritten by this document beyond its own closure section (§2.1, added by `ODY-S02-008`) — it remains otherwise a closed, historical artifact, per this repository's convention of not retroactively editing completed backlog revisions.
- `ODY-S02-014`'s `Blocked` status may only change to `Draft`/active once the `ADR-016` §14 follow-up spike genuinely exists and closes the named gaps — not by editing this document's status column alone without that evidence existing first.
- If this document's section 2 narrowing decisions (or the real-transport gate itself) are later found incorrect or resolved sooner than expected, that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning — this document would gain an explicit amendment note, not a rewritten section 2.
