# ODY-S02-004 — ADR: Snapshot/Delta/Reconnect Model

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-004-adr-snapshot-delta-reconnect-model`
**Pull request:** Not opened
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

Closes the application-level snapshot/delta/reconnect protocol contract (`06_Networking_and_Session_Sync` §15–18) so `ODY-S02-005`/`006` and a future implementation task have a fixed payload shape and reconnect semantics to build against. No user-visible product behavior changes yet — this is the architectural foundation `ODY-S02-006` (redaction) and the eventual production networking code both need.

## 2. Task contract

- Goal: produce `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` (Accepted), fixing snapshot identity, delta-batch/operation shape, gap detection, duplicate handling, late join, the 10-step reconnect flow, and the bounded-buffer-plus-fallback reconnect rule — with visibility/redaction explicitly out of scope.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (prerequisites), backlog `ODY-S02-004`.
- In scope: ADR-017, its task contract, this ExecPlan, `SLICE-02_BACKLOG.md`'s `ODY-S02-004` row.
- Out of scope: any code, any visibility/redaction rule (`ODY-S02-006`), any Unity Relay SDK integration, any edit to `ADR-015`/`016`.
- Required authorities: `06_Networking_and_Session_Sync` §15–18, `ADR-015`, `ADR-016` §5, `ADR-012` §3.4/§7 (terminology/dedup disambiguation), `SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` (expected unaffected — no code changes).

## 3. Current state

- `ODY-S02-001` (`ADR-015`) and `ODY-S02-003` (`ADR-016`) are both merged to `main` (PRs #38, #40), confirmed by `git log` before branching.
- `ADR-016` §5 explicitly hands the application-level "what gets replayed after reconnect" question to this task, without contradicting it.
- `06_Networking_and_Session_Sync` §15–18 gives a detailed, mostly `[CONFIRMED]` protocol description — this task formalizes it as an ADR rather than inventing new architecture from scratch.
- `ADR-012` §3.4 defines a completely different "Snapshot" (a persistence-layer `campaign.db` backup, `BackupId`-identified) that shares the English word with this ADR's `ProjectionSnapshot` (a network wire artifact) — disambiguated explicitly in ADR-017 §3.1, per this task's own instruction.
- `ADR-012` §7's `AppliedCommands` command-idempotency mechanism is a different layer from this ADR's delta-batch deduplication (client-side, range-keyed, not `CommandId`-keyed) — disambiguated explicitly in ADR-017 §6.

## 4. Proposed approach

Adopt `06_Networking_and_Session_Sync` §15–18's already-detailed, mostly `[CONFIRMED]` protocol description as the ADR's normative content essentially verbatim, adding: (a) explicit terminology disambiguation against `ADR-012`'s unrelated "Snapshot" concept and `AppliedCommands` dedup mechanism; (b) a normative decision that a bounded host-side delta buffer is required (not "always full snapshot"), justified by the product document's own explicit "delta replay if window available, else full snapshot" text, without fixing a specific buffer size (deferred to the implementation task, since no empirical data exists to justify a specific number). See `ADR-017` itself for the full reasoning.

## 5. Milestones

### M1 — ADR-017 written, terminology disambiguated, buffer decision made and justified

- [x] `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` written, mirroring `ADR-015`/`016`'s format.
- [x] §3.1 explicitly disambiguates `ProjectionSnapshot` from `ADR-012`'s `Snapshot`.
- [x] §6 explicitly disambiguates delta-batch dedup from `AppliedCommands`.
- [x] §8 makes and justifies the bounded-buffer-plus-fallback decision, without fabricating a specific buffer size.
- [x] §12 explicitly excludes visibility/redaction (confirmed it does not overlap with `ODY-S02-006`).

### M2 — Task contract and backlog row complete

- [ ] `docs/tasks/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md` written, all 18 sections.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-004` row updated (status/planning mode only).
- [ ] Validation run and recorded.
- [ ] Draft PR opened, CI green.

## 6. Progress log

- 2026-08-25 — Preflight confirmed `ADR-015`/`016` both merged to `main`; branched cleanly.
- 2026-08-25 — Read `06_Networking...` §15–18 in full, `ADR-015`, `ADR-016` §5, `ADR-012` §3.4/§7, `SLICE-02_BACKLOG.md` §4.
- 2026-08-25 — Decided: bounded delta buffer required (not always-full-snapshot), size left as an implementation parameter — see Decisions below.
- 2026-08-25 — `ADR-017` written.

## 7. Decisions

- 2026-08-25 — Decision: host must maintain a bounded delta buffer for both gap-repair and reconnect catchup, with full-snapshot as the fallback when the buffer doesn't cover the gap — not "always full snapshot." Rationale: the product document itself already states both paths as normative (`06_Networking` §18.2 steps 6–7), and always rebuilding a full snapshot for the common case of brief disconnects would be needlessly expensive. Authority: `06_Networking_and_Session_Sync` §18.2; `ADR-017` §8.
- 2026-08-25 — Decision: the exact buffer size/duration is not fixed by this ADR — left as an implementation parameter with a stated minimum requirement (must cover a typical transport-reconnect timeout). Rationale: neither the product document nor `SP-03` gives empirical grounding for a specific number, unlike `ADR-011`/`SP-02`'s precedent; fabricating a number would misrepresent the evidence, the same principle `ADR-015` §12.1 and `ADR-016` §8 already applied to `TransportTimeoutPolicy`. Authority: `ADR-017` §8 point 4, §15.5.
- 2026-08-25 — Decision: delta-batch deduplication (client-side, keyed by `SequenceFrom`/`SequenceTo` range) is explicitly not the same mechanism as `AppliedCommands` (host-side, keyed by `CommandId`) — both operate simultaneously, on different layers, neither substitutes for the other. Rationale: `ADR-012` §7.2 explicitly forbids inventing an alternative dedup mechanism instead of checking `AppliedCommands` for command-effect idempotency; this ADR's delta-batch dedup addresses a different problem (downstream projection-delivery deduplication, not command-effect idempotency) and does not violate that rule. Authority: `ADR-012` §7.2; `ADR-017` §6.

## 8. Discoveries and deviations

None — `06_Networking_and_Session_Sync` §15–18 already gave sufficiently detailed, mostly `[CONFIRMED]` content that no material investigation surprise occurred; the two disambiguation points (terminology, dedup layering) were anticipated by the task's own ТЗ instructions and confirmed, not discovered unexpectedly.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Not applicable — this task introduces no code, no persisted schema, no migration. Reverting this task's commits removes the ADR and its task contract with no compatibility or data-loss risk, since no production code depends on it yet.

## 11. Open questions and blockers

- `ADR-017` §12 leaves the exact delta-buffer size, visibility/redaction rules, and asset-manifest-diff protocol detail all open, deferred to `ODY-S02-006` or a future implementation task — not blockers for this task's own closure.
- No blockers for this task itself.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
