# ODY-S02-004 — ADR: Snapshot/Delta/Reconnect Model

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-004-adr-snapshot-delta-reconnect-model`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` — fixing the application-level protocol for scene snapshot delivery (`ProjectionSnapshot` identity, chunking), incremental delta delivery (`ProjectionDeltaBatch` shape and operations), gap detection, duplicate-delta handling, late join, the 10-step disconnect/reconnect flow, and the bounded-delta-buffer-plus-full-snapshot-fallback rule — per `06_Networking_and_Session_Sync` §15–18, extending `ADR-016` §5 exactly where it deferred to this task.

## 2. Why this task exists

- Problem or dependency being addressed: `06_Networking_and_Session_Sync` §15–18 describes the protocol's intent, but is a product document, not a normative ADR; `ADR-016` §5 explicitly hands the application-level reconnect question ("what gets replayed") to this task without answering it itself.
- Value or risk reduction: gives `ODY-S02-005`/`006` and a future implementation task a fixed payload contract and reconnect semantics to design against, so they do not have to invent gap-detection or reconnect behavior mid-implementation.
- Blocking or enabling relationship: `SLICE-02_BACKLOG.md` §5 — depends on `ODY-S02-001` (channel semantics), has a practical, non-blocking relationship with `ODY-S02-003` (the chosen relay's latency/ordering characteristics may inform reconnect timing, though this ADR does not depend on `ODY-S02-003`'s completion). Blocks `ODY-S02-006` (Permissions Baseline) from defining visibility/redaction rules against a fixed snapshot/delta payload shape.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §15 (ordering/revisions), §16 (snapshot protocol), §17 (delta protocol), §18 (reconnect)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1/§5.2 (channels), §6 (`NetworkEnvelope`, payload carrier)
- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` §5 (transport-level reconnect model — this ADR extends it exactly where deferred, does not contradict it)
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` §3.4 (persistence-layer `Snapshot`, disambiguated from this ADR's `ProjectionSnapshot`), §7 (`AppliedCommands`, disambiguated from this ADR's delta-batch dedup)
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's boundary, explicitly excluding visibility/redaction)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), roadmap section 15–18, backlog `ODY-S02-004`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `06_Networking_and_Session_Sync` §15–18's content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-017`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ADR-015` (`ODY-S02-001`, PR #38) and `ADR-016` (`ODY-S02-003`, PR #40) are both merged to `main` — confirmed by `git log --oneline -10` before branching.
- `06_Networking_and_Session_Sync...` §15–18 gives detailed, mostly `[CONFIRMED]` content for `SessionSequence`/`AggregateRevision` (§15), snapshot identity and chunking (§16), delta batch/operations/gap-detection/duplicates (§17), and a literal 10-step reconnect flow (§18) — confirmed by `Read` in full.
- `ADR-016` §5 explicitly states the application-level reconnect question ("what gets replayed/restored — missed deltas, snapshot fallback") is `ODY-S02-004`'s scope, not its own — confirmed by `Read`.
- `ADR-012` §3.4 defines `Snapshot` as a persistence-layer `campaign.db` backup artifact (`BackupId`-identified) — a different concept from this ADR's `ProjectionSnapshot` (a network wire artifact) despite sharing the English word — confirmed by `Read`; this task's own instruction requires explicit disambiguation, which `ADR-017` §3.1 provides.
- `ADR-012` §7 defines `AppliedCommands` as the host-side, `CommandId`-keyed, single normative exactly-once-effect mechanism for command delivery, explicitly forbidding an alternative dedup mechanism as a substitute — confirmed by `Read`; this task's delta-batch dedup (client-side, range-keyed) is a different layer, not a substitute, disambiguated in `ADR-017` §6.
- Next available ADR number is `ADR-017` (`docs/adr/` contains ADR-001 through ADR-016, confirmed by directory listing).

### Assumptions

- None. All facts above were directly observed via `Read`/`git log`/directory listing before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` (new).
- `docs/tasks/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md` (this file), `docs/plans/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-004` row status only.

### Out of scope

- Any production code implementing this protocol over `ISessionTransport` — a separate future implementation task.
- Visibility/redaction rules (who sees what in a given `ProjectionSnapshot`/`ProjectionDeltaBatch`) — `ODY-S02-006`'s scope; this ADR defines only the delivery/recovery mechanism, explicitly assuming payload is already redacted before reaching the transport layer.
- Identity baseline (`UserId`, dev identity, JWT boundary) — `ODY-S02-005`, not defined here beyond referencing already-existing field names (`AudienceUserId`).
- Any integration of the real Unity Relay SDK — a separate future task, gated by `ADR-016` §14, and not a dependency of this ADR (this protocol is transport-provider-agnostic).
- Any edit to `ADR-015`, `ADR-016`, or any `ODY-S02-001`/`002`/`003` file — this task only reads them.
- Fixing a specific numeric delta-buffer size/duration — left as an implementation parameter (`ADR-017` §8 point 4), not this ADR's decision.

### Allowed paths

```text
docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md
docs/tasks/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md
docs/plans/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-017` §11 documents that a future implementation must keep `ProjectionSnapshot`/`ProjectionDeltaBatch` construction in the Application layer, with `Odyssey.Networking` only transporting already-built payload — this task does not itself touch either module.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable — `ADR-017`'s payload shapes are documentation only; no codec or DTO is introduced by this task.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable to this task's own deliverable — `ADR-017` itself explicitly excludes redaction rules from its scope (§12), deferring them to `ODY-S02-006`.
- Performance or platform constraint: not applicable.
- Other: `ADR-017` must explicitly disambiguate its `ProjectionSnapshot` from `ADR-012`'s persistence-layer `Snapshot`, and its delta-batch dedup from `ADR-012`'s `AppliedCommands` — both verified present in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — snapshot identity is composite, not a single field

**Given** `06_Networking_and_Session_Sync` §16.2's `ProjectionSnapshot` field list
**When** `ADR-017` §4 is written
**Then** it fixes `SnapshotId` + `BaseSessionSequence`/`ProjectionRevision`/`PermissionRevision` + `PayloadHash` together as the identity, with an explicit justification for why a single number or a single hash alone would be insufficient.

### Scenario 2 — the two "Snapshot" concepts are never conflated

**Given** `ADR-012`'s persistence-layer `Snapshot` and this ADR's network-layer `ProjectionSnapshot` sharing the same English word
**When** `ADR-017` §3.1 is written
**Then** it states explicitly, in its own subsection, that these are unrelated artifacts on different layers, never interchangeable.

### Scenario 3 — reconnect uses buffered catchup with a full-snapshot fallback, not one exclusive path

**Given** `06_Networking_and_Session_Sync` §18.2's own "delta replay if window available, else full snapshot" text
**When** `ADR-017` §8 is written
**Then** it normatively requires a bounded host-side delta buffer (not "always full snapshot"), with a full-snapshot fallback for out-of-buffer gaps, and justifies the choice for MVP scale without fabricating a specific buffer size.

### Scenario 4 — visibility/redaction is explicitly out of scope, not silently assumed

**Given** `SLICE-02_BACKLOG.md` §4's explicit boundary that `ODY-S02-006` owns visibility/redaction
**When** `ADR-017` §12 is written
**Then** it states this exclusion explicitly and confirms the ADR's own content (mechanics of delivery/recovery) does not encroach on it.

### Required invariants

- `ADR-017` does not modify `ADR-015`'s or `ADR-016`'s own files.
- `ADR-017` does not define any visibility/redaction rule anywhere in its text.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-004` row status.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` exists, `Status: Accepted`, mirroring `ADR-015`/`ADR-016`'s structural format.
2. `ADR-017` §3.1 explicitly disambiguates `ProjectionSnapshot` (network) from `ADR-012`'s `Snapshot` (persistence) as unrelated artifacts.
3. `ADR-017` §6 explicitly disambiguates delta-batch deduplication (client-side, `SequenceFrom`/`SequenceTo`-keyed) from `ADR-012`'s `AppliedCommands` (host-side, `CommandId`-keyed), stating both are required simultaneously, neither substitutes for the other.
4. `ADR-017` §8 normatively requires a bounded delta buffer plus full-snapshot fallback (not an "always full snapshot" design), justified for MVP scale, without fixing a specific numeric buffer size as final.
5. `ADR-017` §7/§3.5 states late join always uses the primary snapshot path, never delta-replay of history.
6. `ADR-017` §12 explicitly excludes visibility/redaction rules from its scope, confirmed not to overlap with `ODY-S02-006`.
7. `ADR-015` and `ADR-016` (and all `ODY-S02-001`/`002`/`003` files) are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass unchanged (no code touched).
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

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

- Read `ADR-017` end-to-end after writing to confirm the two disambiguation sections (§3.1, §6) and the buffer-decision section (§8) are present and substantive, per this task's own explicit instructions.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`), used only to confirm the existing solution is unaffected.

### Validation not required by this task

- Any test of the actual protocol implementation — no code exists yet; deferred to a future implementation task per `ADR-017` §14's own Definition-of-Done gate for that future task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol implementation is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-017` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: None — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — `ADR-017` explicitly defers all authorization/visibility content to `ODY-S02-006`.
- Redaction requirements: Not applicable to this task's own deliverable (see above); `ADR-017` itself states redaction is computed before payload reaches the mechanics it defines (§1 point 8).
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone. This task directly matches multiple explicit §1.2 triggers: it "introduces or changes ... a protocol" (the snapshot/delta/reconnect application-level protocol, a real architectural decision, not merely investigative); it "affects ... networking" directly, fixing the payload contract `ODY-S02-006` and a future implementation task will build against; and it carries real design judgment calls documented in `ADR-017` §15 (Rassmotrennye alternatives) — e.g., whether to require a bounded buffer at all, whether to fix a specific buffer size now — matching the same kind of judgment-call reasoning `ODY-S02-001`'s and `ODY-S02-003`'s own ExecPlan justifications described for their respective decisions. Unlike `ODY-S02-000` (which "decides no technical question," per its own §14), this task decides several (snapshot identity composition, dedup-layer separation, buffer-vs-always-snapshot). It does not have `PLANS.md` §1.1's "one clear implementation path" quality in the trivial sense, since evaluating and rejecting five real alternatives (`ADR-017` §15) required weighing tradeoffs, not simply transcribing the product document.
- ExecPlan path: `docs/plans/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-017`, this task contract, and the backlog row update).
- Milestone or sequencing constraints: depends on `ODY-S02-001` (merged, PR #38) per `SLICE-02_BACKLOG.md` §5; has a practical, non-blocking relationship with `ODY-S02-003` (merged, PR #40) — this task does not depend on it, but this ADR's text does not contradict `ADR-016` §5, verified during preflight. Blocks `ODY-S02-006` (Permissions Baseline) from defining visibility/redaction against a fixed payload shape.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-004` row only).
- Documents that must not change: `ADR-001`–`016`, `docs/tasks/active/ODY-S02-001/002/003_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces a new application-level network protocol (`ProjectionSnapshot`/`ProjectionDeltaBatch` v1) — documentation only, no code-level version bump, since no code changes.
- Documentation version changes: `ADR-017` is a new document (v1.0); `ADR-015`/`ADR-016` are unmodified.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; existing suite unaffected).
- [x] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
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

- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` — new.
- `docs/tasks/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md` (this file), `docs/plans/active/ODY-S02-004_ADR_Snapshot_Delta_Reconnect_Model.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-004` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors — unaffected by this documentation-only task. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | 147/147, 0 failed — unchanged. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-017` Status: `Accepted`, 17-section structure mirroring `ADR-015`/`016`. |
| AC-2 | Passed | `ADR-017` §3.1 disambiguates `ProjectionSnapshot` from `ADR-012`'s `Snapshot`. |
| AC-3 | Passed | `ADR-017` §6 disambiguates delta-batch dedup from `AppliedCommands`. |
| AC-4 | Passed | `ADR-017` §8 requires bounded buffer + fallback, defers exact size to implementation. |
| AC-5 | Passed | `ADR-017` §3.5/§7 fix late join to primary-snapshot-only. |
| AC-6 | Passed | `ADR-017` §12 explicitly excludes visibility/redaction, names `ODY-S02-006`. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-015`/`016` or `ODY-S02-001`/`002`/`003` file touched. |
| AC-8 | Passed | See Validation results table above — all four commands pass. |
| AC-9 | Passed | `git diff --name-status` against `main` shows only `ADR-017`, this task contract, its ExecPlan, and the one `SLICE-02_BACKLOG.md` row. |
| AC-10 | Pending | PR not yet opened. |

## 18. Blockers, risks, and open decisions

- No blockers.
- Open decision (deliberately left to a future task, not this one): the exact delta-buffer size/duration — `ADR-017` §8 point 4/§15.5 states this explicitly, with a minimum requirement (must cover a typical transport-reconnect timeout) but no fixed number.
- Risk: this ADR's asset-manifest-diff step (reconnect flow step 8) is mentioned but not detailed, since `06_Networking_and_Session_Sync` gives it no dedicated normative section beyond the mention — flagged in `ADR-017` §12 as a known gap, not silently glossed over.
