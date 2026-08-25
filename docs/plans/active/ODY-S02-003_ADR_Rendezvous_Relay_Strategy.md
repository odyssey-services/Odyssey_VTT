# ODY-S02-003 — ADR: Rendezvous/Relay Strategy

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-002-sp-03-internet-connectivity` (continuing `ODY-S02-002`'s still-open PR #39 branch, per this task's own preflight instruction — no separate branch was created from unmerged `main`)
**Pull request:** Draft — [#40](https://github.com/odyssey-services/Odyssey_VTT/pull/40) (stacked on `ODY-S02-002`'s still-open #39 until it merges)
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

Closes `06_Networking_and_Session_Sync` §51 `OPEN-NW-001` and `ADR-015` §12.2 (fully) / §12.1 (partially, reclassified to Provisional) with a normative, honestly-qualified decision: Unity Relay is the chosen relay/rendezvous provider, `SessionEndpoint`'s shape is fixed, and a binding pre-production-integration empirical gate is established given `SP-03`'s incomplete coverage. No user-visible product behavior changes — this unblocks `ODY-S02-004` (Snapshot/Delta/Reconnect Model) to design against a named transport provider.

## 2. Task contract

- Goal: produce `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` (Accepted, explicitly qualified) and its task contract, honestly reflecting `SP-03`'s partial empirical coverage rather than presenting full confidence.
- Acceptance criteria: see task contract §9 (`docs/tasks/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md`).
- Requirement IDs: `SLICE-02` (prerequisite revision), backlog `ODY-S02-003`.
- In scope: ADR-016, its task contract, `SLICE-02_BACKLOG.md`'s `ODY-S02-003` row.
- Out of scope: any production Unity Relay/UGS SDK integration, any re-run of `SP-03`, snapshot/delta/reconnect protocol (`ODY-S02-004`), identity/permissions code, any edit to `ADR-015` or `ODY-S02-001`/`002`.
- Required authorities: `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`, `06_Networking_and_Session_Sync` §51, `ADR-015` §12.1/§12.2, `ADR-011` v1.1 (structural/risk-honesty reference), `docs/tasks/SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` (expected unaffected — no code changes).

## 3. Current state

- `ODY-S02-002` (PR #39, `SP-03` spike) is still open/Draft at task start — not yet merged to `main`. Per this task's own preflight instruction, work continues on that branch rather than starting a new branch from unmerged `main`.
- `SP-03`'s report (`docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md`) documents that 5 of 7 roadmap §11.4 checklist items are `NOT_VERIFIED`, with named root causes (no linked Unity Gaming Services project, no second real network available).
- `ADR-015` §12.1 (`TransportTimeoutPolicy`) and §12.2 (`SessionEndpoint`) are both `[OPEN]`, explicitly deferred to this ADR.
- `06_Networking_and_Session_Sync` §51 `OPEN-NW-001` names no candidate provider; its evaluation criteria include "Unity SDK stability."

## 4. Proposed approach

Fix Unity Relay as `Accepted`, but qualify the Status line itself (not just an Open Questions footnote) with a binding pre-production-integration empirical gate (ADR-016 §1 point 9, §14), and enumerate all 8 operational risks derived from `SP-03`'s `NOT_VERIFIED` items explicitly in their own ADR section (§9), not folded into a single vague sentence. `SessionEndpoint` is fixed per `SP-03`'s own proposed shape. `TransportTimeoutPolicy` is not given new numbers — its `ADR-015` defaults are reclassified as "Provisional," not silently treated as final. See `ADR-016` itself for the full reasoning.

## 5. Milestones

### M1 — ADR-016 written, honestly reflecting SP-03's confidence gap

- [x] `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` written, mirroring `ADR-015`'s format.
- [x] Status line itself states the qualification (not buried in Open Questions).
- [x] Context section quotes/cites `SP-03`'s confidence-gap language directly, not just references it.
- [x] All 8 operational risks enumerated individually in their own section (§9).

### M2 — Task contract and backlog row complete

- [ ] `docs/tasks/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md` written, all 18 sections.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-003` row updated (status/planning mode only).
- [ ] Validation run and recorded.
- [ ] Draft PR opened/extended, CI green.

## 6. Progress log

- 2026-08-25 — Preflight confirmed PR #39 still open; switched to its branch per the ТЗ's own fallback instruction rather than branching from unmerged `main`.
- 2026-08-25 — Read `SP-03`'s report and harness README in full, `06_Networking...` §51, `ADR-015` §12.1/§12.2, `ADR-011` v1.1 as structural reference.
- 2026-08-25 — Decided: `Accepted` with a binding pre-production-integration gate (not a downgraded status, not silent full confidence) — see Decisions below.
- 2026-08-25 — `ADR-016` written.

## 7. Decisions

- 2026-08-25 — Decision: Unity Relay is `Accepted` as the chosen provider, but with a normative pre-production-integration empirical gate (ADR-016 §1 point 9, §14) rather than either (a) silently treating `SP-03`'s partial evidence as full confidence, or (b) declining to decide and leaving `OPEN-NW-001` open indefinitely. Rationale: the product owner already explicitly chose to accept `SP-03`'s report at its stated lower confidence level rather than commissioning a follow-up spike now (per this task's own context) — but `ODY-S02-004` still needs a named transport provider to design against, and indefinitely deferring the decision blocks it for no added safety, since the real risk (untested Unity Relay behavior) is better mitigated by a binding gate on the *next* task (production integration) than by delaying *this* architectural decision. Authority: `SP-03` report §8's own two stated paths; `ADR-011` v1.1's precedent of a real, honestly-qualified decision under partial evidence.
- 2026-08-25 — Decision: `TransportTimeoutPolicy` values are not changed from `ADR-015`'s defaults, but reclassified as "Provisional" rather than left ambiguous. Rationale: `SP-03`'s only relevant measurements (bare UDP round-trip latency on one network path) are not a valid empirical basis for final production timeout values; fabricating precision here would misrepresent the evidence. Authority: `SP-03` report §5's own explicit refusal to propose new values.

## 8. Discoveries and deviations

- Discovery: `ODY-S02-002`'s PR #39 was still open (not merged) at this task's start, unlike the ТЗ's default assumption. Followed the ТЗ's own explicit fallback: continued on PR #39's branch rather than opening a new branch from an unmerged `main`.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Not applicable — this task introduces no code, no persisted schema, no migration. Reverting this task's commits removes the ADR and its task contract with no compatibility or data-loss risk, since no production code depends on it yet (that dependency is explicitly gated behind ADR-016 §14, not yet satisfied).

## 11. Open questions and blockers

- `ADR-016` §12.1–§12.3 remain open (final `TransportTimeoutPolicy` values, real 12-participant capacity, `AccessDescriptor`'s real format) — deferred to the pre-production-integration gate (§14), not blockers for this task's own closure.
- No blockers for this task itself.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR (extending #39, or a new one if #39 merges first) is opened/updated with green CI.
