# ODY-S02-003 — ADR: Rendezvous/Relay Strategy

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-003-adr-rendezvous-relay-strategy` (branched from `ODY-S02-002`'s still-open `feat/ody-s02-002-sp-03-internet-connectivity`, per this task's own preflight fallback instruction — PR #39 was not yet merged at task start)
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` — fixing Unity Relay as the chosen relay/rendezvous provider, `SessionEndpoint`'s final shape (closing `ADR-015` §12.2), and `TransportTimeoutPolicy`'s status as Provisional (partially addressing `ADR-015` §12.1) — while honestly reflecting `SP-03`'s (`ODY-S02-002`) incomplete empirical coverage (5 of 7 roadmap §11.4 checklist items `NOT_VERIFIED`) directly in the decision text itself, not only in an Open Questions footnote.

## 2. Why this task exists

- Problem or dependency being addressed: `06_Networking_and_Session_Sync` §51 `OPEN-NW-001` leaves the relay/rendezvous provider unselected; `ADR-015` §12.1/§12.2 explicitly defer `TransportTimeoutPolicy` and `SessionEndpoint` to this ADR. `ODY-S02-004` (Snapshot/Delta/Reconnect Model) cannot be designed against a named transport provider until this decision exists.
- Value or risk reduction: gives `ODY-S02-004` a concrete provider to design against, while binding any future production integration to a mandatory empirical gate — so the real gap `SP-03` left is not silently forgotten once this ADR is `Accepted`.
- Blocking or enabling relationship: `SLICE-02_BACKLOG.md` §5 sequences this task after `ODY-S02-002`'s report exists (it does) and before `ODY-S02-004`. Product owner explicitly accepted `SP-03`'s report as-is, including its confidence-gap disclosure, and approved proceeding to this ADR without a follow-up spike first — the decision this task's own ТЗ context records.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §51 (`OPEN-NW-001`, evaluation criteria)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1/§5.2 (channel split), §12.1 (`TransportTimeoutPolicy`, open), §12.2 (`SessionEndpoint`, open)
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md` (full report, all sections including `NOT_VERIFIED`)
- `Tools/Spikes/SP-03-InternetConnectivity/README.md` §"Scope and limitations"
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` — structural and risk-honesty reference (how a real amendment ADR reflects a spike's findings, including what it does and does not close)
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's boundary), §5 (sequencing)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), roadmap section 11.4/§51, backlog `ODY-S02-003`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `06_Networking_and_Session_Sync` §51's evaluation criteria list and `SP-03`'s report content are summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-016`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S02-002` (PR #39) was still `OPEN`/Draft at this task's start, not yet merged into `main` — confirmed via `gh pr view 39` before branching; this task's branch was created from `ODY-S02-002`'s own branch, not from `main`, per this task's own preflight fallback instruction.
- `docs/tasks/active/ODY-S02-002_SP-03_Internet_Connectivity_Report.md` §0/§8 explicitly states that 5 of 7 roadmap §11.4 checklist items are `NOT_VERIFIED`, with named root causes (no Unity Gaming Services project linked to this repository; no second real network available in the spike's environment).
- `06_Networking_and_Session_Sync...` §51 `OPEN-NW-001` names no candidate provider and lists "регионы; latency; 12 participants; pricing; reconnect semantics; reliable/unreliable channels; Unity SDK stability" as its evaluation criteria — confirmed by `Read`.
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §12.1 (`TransportTimeoutPolicy`) and §12.2 (`SessionEndpoint`) are both `[OPEN]` on the branch this task builds from — confirmed by `Read`.
- Next available ADR number is `ADR-016` (`docs/adr/` contains ADR-001 through ADR-015, confirmed by directory listing).
- Per this task's own ТЗ context, the product owner already explicitly accepted `SP-03`'s report as-is, including its confidence-gap disclosure, and approved proceeding to this ADR without commissioning a follow-up spike first.

### Assumptions

- None. All facts above were directly observed via `Read`/`gh pr view`/directory listing before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` (new).
- `docs/tasks/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md` (this file), `docs/plans/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-003` row status only.

### Out of scope

- Any production Unity Relay/UGS SDK integration into `Odyssey.Networking` — a separate future implementation task, explicitly gated by `ADR-016` §1 point 9/§14 (a follow-up empirical spike is a prerequisite, not this task's job).
- Any re-run of `SP-03` with real credentials — an explicit product-owner decision already made (declined at this step, per this task's own context) — not performed by this task.
- Snapshot/delta/reconnect application-level protocol (`ODY-S02-004`), identity/permissions code (`ODY-S02-005`/`006`).
- Any edit to `ADR-015`, `ODY-S02-001`, or `ODY-S02-002`'s own files (`Tools/Spikes/SP-03-InternetConnectivity/**`, its task contract, or its report) — this task only reads them.
- Any change to `SessionTransportContracts.cs` or any other production code — `SessionEndpoint`'s shape is fixed as a normative proposal for a future implementation task, not implemented here.

### Allowed paths

```text
docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md
docs/tasks/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md
docs/plans/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-016` §10 documents that any future implementation must still respect `ADR-001` §6.6/`ADR-015`'s already-fixed boundaries; this task does not itself touch them.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable — `SessionEndpoint`'s proposed shape (`ADR-016` §6) is documentation only, no codec or DTO is introduced.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable — no Unity or IL2CPP code is touched.
- Dependency / licensing rule: no new dependency is introduced by this task (the eventual Unity Relay package dependency is a future implementation task's concern, gated by `ADR-016` §14).
- Security / privacy / redaction rule: not applicable — no code, no secret, no hidden data is touched.
- Performance or platform constraint: not applicable.
- Other: `ADR-016`'s Status line itself must state its qualification (pre-production-integration gate), not bury it only in an Open Questions section — this task's own explicit instruction, verified in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — `OPEN-NW-001` is closed with an honestly-qualified decision

**Given** `06_Networking_and_Session_Sync` §51's open relay-provider question and `SP-03`'s partial empirical coverage
**When** `ADR-016` is written
**Then** its Status line states both `Accepted` and the qualification (pre-production-integration empirical gate) in the same place, and its Context section quotes `SP-03`'s own confidence-gap language directly, not merely references it.

### Scenario 2 — every `SP-03` `NOT_VERIFIED` item becomes a named operational risk

**Given** `SP-03`'s report lists 5–6 `NOT_VERIFIED` checklist items with named root causes
**When** `ADR-016` §9 is written
**Then** each item appears as its own numbered risk entry with its own root cause and consequence, not folded into one summary sentence.

### Scenario 3 — future production integration is normatively gated

**Given** the real gap in empirical coverage
**When** `ADR-016` §1/§14 are written
**Then** they state, as a normative (not advisory) condition, that no production task may integrate Unity Relay before a follow-up empirical spike closes the named gaps.

### Required invariants

- `ADR-016` does not modify `ADR-015`'s own file or code (`SessionTransportContracts.cs`).
- `ADR-016` does not present `SP-03`'s partial coverage as full confidence anywhere in its text.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-003` row status.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` exists, `Status: Accepted` with its qualification stated in the same line, mirroring `ADR-015`'s 17-ish-section structural format (adapted where content requires additional sections, e.g. the risk enumeration).
2. `ADR-016`'s Context section (§2) directly cites/quotes `SP-03`'s report's own confidence-gap language, not just a paraphrase.
3. `ADR-016` §9 (or equivalent) lists every `SP-03` `NOT_VERIFIED` item as its own named operational risk with root cause, not a single collapsed sentence.
4. `ADR-016` fixes `SessionEndpoint`'s shape per `SP-03`'s own proposal, without implementing it in code.
5. `ADR-016` does not fabricate final `TransportTimeoutPolicy` values; it reclassifies the existing `ADR-015` defaults as Provisional, explicitly not final.
6. `ADR-016` states a normative (not advisory) pre-production-integration empirical gate for any future Unity Relay code integration.
7. `ADR-015` and all `ODY-S02-001`/`002` files are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass unchanged (no code touched).
9. `git diff --name-status` against this task's own base (`ODY-S02-002`'s branch tip) shows only files listed in §5's Allowed paths.
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

- Read `ADR-016` end-to-end after writing to confirm the Status-line qualification and §9 risk enumeration are both present and not diluted into vague language, per this task's own explicit instruction.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`), used only to confirm the existing solution is unaffected.

### Validation not required by this task

- Any empirical test of Unity Relay itself — explicitly out of scope (§5), gated to a future task by `ADR-016` §14.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-016` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task. `ADR-016` names Unity Relay as a future dependency for a later implementation task, which will record its own licensing/approval entry when it actually adds the package reference.

## 13. Security, privacy, and hidden information

- Data classes handled: None — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from `ODY-S02-002`'s Brief-plan precedent. Unlike `ODY-S02-002` (a purely investigative spike that changed no accepted decision), this task directly closes an open architectural question (`06_Networking_and_Session_Sync` §51 `OPEN-NW-001`) and two `ADR-015` open items (§12.1 reclassification, §12.2 closure) — a real decision with "meaningful ... operational risk" (`PLANS.md` §1.2), documented explicitly as 8 named risks in `ADR-016` §9, and it "affects ... networking" directly by fixing the transport provider `ODY-S02-004` will design against. It also does not have `PLANS.md` §1.1's "one clear implementation path" quality in the trivial sense: deciding how to honestly qualify a partially-confirmed recommendation (Accepted-with-a-binding-gate, vs. downgrading the status, vs. declining to decide) required weighing real alternatives (`ADR-016` §13), the same kind of judgment call `ODY-S02-001`'s own ExecPlan justification described for its own port-signature decision. This matches `ODY-S02-000`'s own explicit distinction: that scaffold task used Brief plan because it "decides no technical question"; this task does decide one.
- ExecPlan path: `docs/plans/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-016`, this task contract, and the backlog row update; stacked on `ODY-S02-002`'s still-open PR #39 at the time of opening — see §17 for how this is resolved once #39 merges).
- Milestone or sequencing constraints: depends on `ODY-S02-002`'s report existing (it does, on the branch this task builds from) per `SLICE-02_BACKLOG.md` §5; blocks `ODY-S02-004` from being designed against a named transport provider until this ADR exists.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-003` row only).
- Documents that must not change: `ADR-001`–`015`, `docs/tasks/active/ODY-S02-002_*` (report and task contract — read only), `Tools/Spikes/SP-03-InternetConnectivity/**`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: `SessionEndpoint`'s shape is fixed as a normative proposal (documentation only, not code) — no code-level version bump, since no code changes.
- Documentation version changes: `ADR-016` is a new document (v1.0); `ADR-015` is unmodified (its own §12.1/§12.2 remain textually `[OPEN]` in that file — `ADR-016` supersedes them in effect per its own §16 Normative Action, the same pattern `ADR-011` v1.1 used relative to `ADR-011` v1.0 §12.1, without editing the earlier file's text).
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

- `docs/adr/ADR-016_Rendezvous_Relay_Strategy_v1.0.md` — new.
- `docs/tasks/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md` (this file), `docs/plans/active/ODY-S02-003_ADR_Rendezvous_Relay_Strategy.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-003` row status.

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
| AC-1 | Passed | `ADR-016` Status line: "Accepted — с обязательным пред-production-интеграционным условием". |
| AC-2 | Passed | `ADR-016` §2 directly quotes `SP-03` report §3's confidence-gap sentence. |
| AC-3 | Passed | `ADR-016` §9 lists 8 individually-numbered risks with root causes. |
| AC-4 | Passed | `ADR-016` §6 adopts `SP-03` report §4's `SessionEndpoint` shape verbatim; no code changed. |
| AC-5 | Passed | `ADR-016` §8 reclassifies `ADR-015` defaults as Provisional; proposes no new numbers. |
| AC-6 | Passed | `ADR-016` §1 point 9/§14: normative pre-integration gate. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-015` or `ODY-S02-001`/`002` file touched. |
| AC-8 | Passed | See Validation results table above — all four commands pass. |
| AC-9 | Passed | `git diff --name-status` against this task's own base shows only `ADR-016`, this task contract, its ExecPlan, and the one `SLICE-02_BACKLOG.md` row. |
| AC-10 | Pending | PR not yet opened. |

## 18. Blockers, risks, and open decisions

- Blocker (resolved): `ODY-S02-002`'s PR #39 was still open at task start — resolved per this task's own explicit fallback instruction by branching from its branch instead of unmerged `main`.
- Open decision (the product owner's, not this task's): whether/when to commission the follow-up empirical spike `ADR-016` §14 requires before any production Unity Relay integration begins.
- Risk: this task's branch is stacked on `ODY-S02-002`'s still-open PR #39 — until #39 merges, this task's own PR diff will show #39's commits too. Documented here so it is not mistaken for scope creep; resolved once #39 merges (see §17 once the PR is opened).
