# ODY-S02-005 — ADR: Identity Baseline

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-005-adr-identity-baseline`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S02-005_ADR_Identity_Baseline.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce `docs/adr/ADR-018_Identity_Baseline_v1.0.md` — fixing a stable, provider-independent `UserId` semantics (without changing existing code), an approved dev/mock identity boundary for tests, the mock-vs-real Supabase Auth integration decision for `SLICE-02`, the concretized "JWT never in campaign state" rule, and the checkable "service-role key never reaches the client" rule — while explicitly enumerating (not inventing) the items genuinely blocked by `Documentation/18_Account_And_Identity.md`'s continued absence.

## 2. Why this task exists

- Problem or dependency being addressed: `06_Networking_and_Session_Sync` §6.1 defers identity detail to `18_Account_And_Identity.md`, which does not exist; `SLICE-02_BACKLOG.md` §4 nonetheless fixes a narrower `ODY-S02-005` boundary that this task confirmed is decidable without that document.
- Value or risk reduction: gives `ODY-S02-006` (Permissions Baseline) a fixed `UserId` contract to design against, and gives any future auth implementation two binding, checkable secret-handling rules before any code is written — rather than leaving both open indefinitely because one adjacent document is missing.
- Blocking or enabling relationship: `SLICE-02_BACKLOG.md` §5 — `ODY-S02-005` does not depend on `ODY-S02-001`–`004` (identity/auth is orthogonal to transport/session-protocol shape). Blocks `ODY-S02-006`, which depends on `ODY-S02-005`.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` §6 (identity/session directory fields)
- `21_Security_And_Privacy_Odyssey_VTT_v0.1.md` (full document, especially §3–6 data classification/`PE-INV-010` and §7.1's explicit stub)
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md` §10 (inherited data classification)
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` §9.1 (domain identifier principle, applied to `UserId`)
- `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` (structural/risk-rigor reference for sensitive identity artifacts)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §3.3/§4 (`AudienceUserId`/`UserId`, not redefined by this task)
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's fixed boundary)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), backlog `ODY-S02-005`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `06_Networking_and_Session_Sync` §6 and `21_Security_And_Privacy`'s content are summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-018`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S02-001`–`004` (`ADR-015`–`017`, PRs #38/#39/#40/#41) are all merged to `main` — confirmed by `git log --oneline -10` before branching.
- `Documentation/18_Account_And_Identity.md` does not exist anywhere in the repository — reconfirmed by search, matching `ODY-S02-000`'s earlier finding.
- `21_Security_And_Privacy` §7.1 itself explicitly stubs "Auth, tokens и account identity redaction," deferring it to "Этап 3 (networking), вместе с `06_Networking_and_Session_Sync`, `18_Account_And_Identity.md`, `07_Permissions`" and instructing "не заполнять преждевременно" — confirmed by `Read` in full.
- `06_Networking_and_Session_Sync` §6.1 itself defers "Email confirmation и дополнительные требования" to the missing Account/Identity document — confirmed by `Read`.
- `Odyssey.Domain.Identity.UserId` already exists in code (`Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs`, prefix `user_`, canonical hex format, no `NewId()` factory, existing comment calls it "externally assigned") — confirmed by `grep`; this task does not modify it.
- `21_Security_And_Privacy` §4.3's `PE-INV-010` pattern already explicitly lists "сессионные токены" (session tokens) among data never stored in `campaign.db`/`.odcamp`/backup — a direct textual basis for extending the rule to JWT (a session token) without inventing new policy.
- `SLICE-02_BACKLOG.md` §4's `ODY-S02-005` boundary is fully decidable from the sources above; the items requiring `18_Account_And_Identity.md` (email confirmation, account recovery, multi-device behavior, full auth/token redaction table) fall outside that fixed boundary.

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-018_Identity_Baseline_v1.0.md` (new).
- `docs/tasks/active/ODY-S02-005_ADR_Identity_Baseline.md` (this file), `docs/plans/active/ODY-S02-005_ADR_Identity_Baseline.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-005` row status only.

### Out of scope

- Any production code (Supabase Auth integration, mock identity provider implementation) — a separate future implementation task.
- Permission/role model against this identity — `ODY-S02-006`'s scope; this ADR defines only identity, not what it may do.
- Inventing content for `18_Account_And_Identity.md` (email confirmation, account recovery, multi-device behavior) — explicitly flagged as open questions instead (`ADR-018` §12).
- Any edit to `ADR-015`/`016`/`017`, `Odyssey.Domain.Identity.UserId`'s existing code, or any `ODY-S02-001`–`004` file — this task only reads them.

### Allowed paths

```text
docs/adr/ADR-018_Identity_Baseline_v1.0.md
docs/tasks/active/ODY-S02-005_ADR_Identity_Baseline.md
docs/plans/active/ODY-S02-005_ADR_Identity_Baseline.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-018` §9 documents that a future implementation must keep `UserId` in `Odyssey.Domain.Identity` unchanged and follow `ADR-015`'s mock-vs-real-provider pattern for identity providers; this task does not itself touch either module.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable — no DTO or codec introduced.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: this task's own deliverable is itself a security-relevant ADR (JWT/service-role-key rules) — verified present and stated as checkable rules, not vague guidance, in the Completion evidence section.
- Performance or platform constraint: not applicable.
- Other: `ADR-018` must not invent content for the missing `18_Account_And_Identity.md` — verified by cross-checking every decision in the ADR traces to an already-available source or established precedent, not to assumed/invented product requirements.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — `UserId` semantics are fixed without contradicting existing code

**Given** `Odyssey.Domain.Identity.UserId` already exists, marked "externally assigned"
**When** `ADR-018` §4 is written
**Then** it clarifies (not changes) that "externally assigned" means application-issued outside a single campaign's context, not borrowed from a provider's own ID, and states the rationale (historical-attribution stability across provider changes) without modifying the existing type.

### Scenario 2 — mock identity is approved, real integration is deferred with a stated reason

**Given** `18_Account_And_Identity.md`'s absence and `SLICE-02_BACKLOG.md`'s prerequisites-only scope
**When** `ADR-018` §5 is written
**Then** it states explicitly that mock/dev identity is sufficient for this stage, real Supabase Auth integration is deferred, and gives the reasons (missing document, backlog scope) rather than treating the deferral as unexplained.

### Scenario 3 — JWT and service-role-key rules are checkable, not vague

**Given** the task's explicit instruction to formulate both as checkable architectural rules
**When** `ADR-018` §6/§7 are written
**Then** each states exactly what is forbidden, where (if anywhere) exceptions apply, and how a future implementation task would verify compliance (a test or structural scan, mirroring `ADR-014`'s own precedent).

### Scenario 4 — genuine gaps are listed, not invented

**Given** `18_Account_And_Identity.md`'s absence blocks some adjacent questions
**When** `ADR-018` §12 is written
**Then** it lists exactly the items this task could not responsibly decide, with the specific missing source named for each — not silently omitted, not filled in with invented content.

### Required invariants

- `ADR-018` does not modify `Odyssey.Domain.Identity.UserId`'s existing format.
- `ADR-018` does not define any permission/role rule anywhere in its text.
- `ADR-018` does not state as fact anything that would require `18_Account_And_Identity.md`'s content.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-018_Identity_Baseline_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-005` row status.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-018_Identity_Baseline_v1.0.md` exists, `Status: Accepted` (with its scope qualification stated), mirroring `ADR-015`–`017`'s structural format.
2. `ADR-018` §4 fixes `UserId` semantics without modifying its existing code representation, justified by the domain-identifier principle already established in `ADR-011` §9.1.
3. `ADR-018` §5 states dev/mock identity is approved for this stage, and gives an explicit, sourced reason for deferring real Supabase Auth integration.
4. `ADR-018` §6 concretizes "JWT never in campaign state" as a direct, textually-grounded extension of `21_Security_And_Privacy` §4.3's `PE-INV-010` pattern.
5. `ADR-018` §7 formulates "service-role key never reaches the client" as a checkable rule, naming where (if anywhere) a service-role key may legitimately be used.
6. `ADR-018` §12 lists every genuine open question blocked by `18_Account_And_Identity.md`'s absence, with the specific missing source named — no invented content anywhere in the document.
7. `ADR-018` §10 confirms no permission/role model is defined by this ADR.
8. `ADR-015`/`016`/`017` and all `ODY-S02-001`–`004` files are unmodified by this task's diff.
9. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass unchanged (no code touched).
10. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

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

- Read `ADR-018` end-to-end after writing to confirm the `UserId` clarification, mock-vs-real decision, both secret rules, and the open-questions section are all present and substantive, per this task's own explicit instructions.
- Cross-check `Odyssey.Domain.Identity.UserId`'s existing code once more to confirm no code-level contradiction was introduced.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`), used only to confirm the existing solution is unaffected.

### Validation not required by this task

- Any test of a real or mock identity provider implementation — no code exists yet; deferred to a future implementation task.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-018` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task. `ADR-018` names Supabase Auth as a future dependency for a later implementation task, which will record its own licensing/approval entry when it actually adds a package reference.

## 13. Security, privacy, and hidden information

- Data classes handled: None directly — this task touches no code, credential, or campaign data. Its own deliverable (`ADR-018`) classifies `UserId`/provider identity/JWT/service-role key per the already-accepted `ADR-010` §10 scheme (§8 of the ADR) without introducing a new classification.
- Trust boundaries: Not applicable to this task's own execution; `ADR-018` §7 fixes the client/server trust boundary for service-role keys as a normative rule for future code.
- Authorization / audience checks: Not applicable — `ADR-018` explicitly defers all permission/authorization content to `ODY-S02-006`.
- Redaction requirements: `ADR-018` §6 extends the existing `PE-INV-010` redaction/storage-exclusion pattern to JWT explicitly; this task's own deliverable does not weaken or reinterpret that pattern.
- Log-safe fields: Not applicable to this task's own execution.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable to this task itself; `ADR-018` §6/§7 each state what a future implementation task's own security test must prove.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone. This task directly matches an explicit §1.2 trigger — it "affects ... security, permissions, hidden information" by name, fixing two binding secret-handling rules (JWT, service-role key) and a stable identity contract that downstream security-relevant decisions (`ODY-S02-006`) will depend on. It also required real investigative judgment before the content was known: confirming `18_Account_And_Identity.md`'s absence, discovering `21_Security_And_Privacy` §7.1's own explicit stub, and determining exactly which parts of the fixed `ODY-S02-005` boundary were and were not decidable without that missing document — matching §1.2's "requires investigation before the implementation path is known" trigger. This mirrors the same reasoning `ODY-S02-001`/`003`/`004`'s own ExecPlan justifications gave for comparable ADR-authoring decisions with real security/architecture weight, and contrasts with `ODY-S02-000`'s Brief-plan classification (which "decides no technical question") — this task decides several.
- ExecPlan path: `docs/plans/active/ODY-S02-005_ADR_Identity_Baseline.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-018`, this task contract, and the backlog row update).
- Milestone or sequencing constraints: does not depend on `ODY-S02-001`–`004` per `SLICE-02_BACKLOG.md` §5 (identity/auth is orthogonal to transport/session-protocol shape) — confirmed not contradicted during this task. Blocks `ODY-S02-006` (Permissions Baseline), which depends on this task's `UserId` contract.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-018_Identity_Baseline_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-005` row only).
- Documents that must not change: `ADR-001`–`017`, `Documentation/18_Account_And_Identity.md` (does not exist; not created by this task), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything else under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None — `UserId`'s existing code format is unchanged; the ADR only fixes semantics/policy, not a new wire or storage format.
- Documentation version changes: `ADR-018` is a new document (v1.0); no existing ADR changes version.
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

- `docs/adr/ADR-018_Identity_Baseline_v1.0.md` — new.
- `docs/tasks/active/ODY-S02-005_ADR_Identity_Baseline.md` (this file), `docs/plans/active/ODY-S02-005_ADR_Identity_Baseline.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-005` row status.

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
| AC-1 | Passed | `ADR-018` Status: `Accepted — в границах, доступных без 18_Account_And_Identity.md`. |
| AC-2 | Passed | `ADR-018` §4, no code edit to `DomainIdentity.cs`. |
| AC-3 | Passed | `ADR-018` §5, sourced to `SLICE-02_BACKLOG.md` §1/§2 and the missing document. |
| AC-4 | Passed | `ADR-018` §6 quotes `21_Security_And_Privacy` §4.3's `PE-INV-010` text directly. |
| AC-5 | Passed | `ADR-018` §7 names the trusted-server-only exception explicitly. |
| AC-6 | Passed | `ADR-018` §12, four items, each naming the missing source. |
| AC-7 | Passed | `ADR-018` §10 explicit exclusion. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-015`/`016`/`017` or `ODY-S02-001`–`004` file touched. |
| AC-9 | Passed | See Validation results table above — all four commands pass. |
| AC-10 | Passed | `git diff --name-status` against `main` shows only `ADR-018`, this task contract, its ExecPlan, and the one `SLICE-02_BACKLOG.md` row. |
| AC-11 | Pending | PR not yet opened. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (the product owner's, not this task's): whether/when `18_Account_And_Identity.md` will be authored, and whether a future implementation task should proceed with real Supabase Auth integration before or after it exists (`ADR-018` §5, §12).
- Risk: `ADR-018` §12.4 (the exact `UserId` ↔ provider-identity mapping mechanism) is left as an implementation parameter — a future task must not silently invent this without cross-checking `ADR-018` §4's stated principle (separate mapping, not equality).
