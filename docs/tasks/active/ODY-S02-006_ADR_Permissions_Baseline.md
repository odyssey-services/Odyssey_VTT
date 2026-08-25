# ODY-S02-006 — ADR: Permissions Baseline

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-006-adr-permissions-baseline`
**Pull request:** Draft — [#43](https://github.com/odyssey-services/Odyssey_VTT/pull/43)
**ExecPlan:** `docs/plans/active/ODY-S02-006_ADR_Permissions_Baseline.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Produce `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` — fixing the Main GM/Player/Observer role model, host-side read/action permission checks and where exactly they occur, the redacted per-connection scene projection mechanism, and how a revoked permission removes the corresponding data from a client's current state, using only already-accepted mechanisms (`ADR-002`, `ADR-004`, `ADR-017`) — the technical baseline subset of `07_Permissions_Odyssey_VTT_v0.7.md` that roadmap §11.3 scopes this stage to, not that document's full generality.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-017` §12 explicitly deferred visibility/redaction mechanics to this task without answering it itself; no prior ADR defines any concrete role model (`ADR-002` mentions "permissions" only as a generic pipeline step).
- Value or risk reduction: gives `ODY-S02-007` (`SP-04`) a fixed contract to empirically test, and gives a future implementation task a concrete, minimal role model to build rather than the full generality of `07_Permissions` (delegation, arbitrary scopes, ownership/control transfer) that this prototype stage does not need.
- Blocking or enabling relationship: `SLICE-02_BACKLOG.md` §5 — depends on `ODY-S02-005` (a permission check needs a stable actor identity). Blocks `ODY-S02-007` (`SP-04` — Hidden Data Boundary), which needs this contract to test against.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `07_Permissions_Odyssey_VTT_v0.7.md` (full document — `PERM-INV-001`–`012`, §3, §6–10, §33–37)
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.3 (Permissions baseline, the exact list scoping this ADR)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 point 8 (reconnect redaction by current permissions, not reopened), §5 (`Operations[]`, reused not extended), §11 (payload-already-redacted assumption, closed here), §12 (visibility/redaction, explicitly deferred here)
- `docs/adr/ADR-018_Identity_Baseline_v1.0.md` §4 (`UserId`, the actor)
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`SafeReasonCode`, reused not extended)
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (command pipeline, "permissions" as a generic step only — confirmed not a role model)
- `docs/tasks/SLICE-02_BACKLOG.md` §4 (this task's fixed boundary)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), roadmap section 11.3, backlog `ODY-S02-006`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `07_Permissions_Odyssey_VTT_v0.7.md`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-019`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S02-001`–`005` (`ADR-015`–`018`, PRs #38/#39/#40/#41/#42) are all merged to `main` — confirmed by `git log --oneline -10` before branching.
- `07_Permissions_Odyssey_VTT_v0.7.md` is 3101 lines, documenting a full, general permissions model (arbitrary `PermissionKey`/`Scope` namespace, delegation, ownership/control transfer workflows, field-level audience visibility, temporary permissions, groups) far beyond MVP scope — confirmed by reading its section headers and then §3, §6–10, §33–37 in depth.
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.3 names exactly six items as this stage's baseline: Main GM, Player, Observer, read/action check, redacted scene projection, revoked-permission-removes-data — confirmed by `Read`; notably names only three roles, not `07_Permissions` §6.1's four `BaseRoleKind` values (which include `AssistantGM`).
- `ADR-017` §12 explicitly assigns visibility/redaction rules to `ODY-S02-006`; §1 point 8 already fixed that reconnect redaction always uses current, not saved, permissions (not reopened by this task); §11 assumed payload reaching `Odyssey.Networking` is "already redacted" without defining the point — confirmed by `Read`.
- `07_Permissions` §37.2's own documented pipeline (`Membership → PermissionDecision → VisibilityPolicy → ClientProjection`) and §19.1 ("host строит отдельную projection с учётом membership/roles/permissions") already establish the single-authoritative-state-plus-per-connection-filter mechanism — confirmed by `Read`.
- `ADR-017` §5's `Operations[]` already includes `RemoveFromProjection` and `RemoveCapability` — confirmed by re-reading `ADR-017`, directly usable for revocation-removes-data without introducing a new operation type.
- `ADR-004`'s existing `SafeReasonCode` enum already contains all five values `PERM-INV-012` (§36.2 of `07_Permissions`) requires (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) — confirmed against the already-established value list from earlier session work on `ErrorCodes.cs`.
- `ADR-002` mentions "permissions"/"authoritative permissions" only as a generic pipeline-step concept (e.g., "Check authoritative permissions and control grants"), never a concrete role model — confirmed by `grep`, matching `ODY-S02-000`'s earlier finding and `SLICE-02_BACKLOG.md` §4's own statement of this fact.

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (new).
- `docs/tasks/active/ODY-S02-006_ADR_Permissions_Baseline.md` (this file), `docs/plans/active/ODY-S02-006_ADR_Permissions_Baseline.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-006` row status only.

### Out of scope

- Any production code (permission-check runtime, redaction filter implementation) — a separate future implementation task.
- Delegation, arbitrary `PermissionKey`/`Scope` beyond the three baseline roles, `AssistantGM`, ownership/control transfer model — all explicitly deferred (`ADR-019` §10).
- `SP-04` (`ODY-S02-007`) itself — this task only fixes the contract that spike will empirically test.
- Any edit to `ADR-015`/`016`/`017`/`018`, or any `ODY-S02-001`–`005` file — this task only reads them.

### Allowed paths

```text
docs/adr/ADR-019_Permissions_Baseline_v1.0.md
docs/tasks/active/ODY-S02-006_ADR_Permissions_Baseline.md
docs/plans/active/ODY-S02-006_ADR_Permissions_Baseline.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-019` §11 documents that a future implementation must keep permission/visibility decisions in the Application layer, never in `Odyssey.Networking` (`ADR-001` §6.6); this task does not itself touch either module.
- Authoritative-state and transaction boundary: not applicable to this task's own execution; `ADR-019` §6.1 integrates action check into `ADR-002`'s existing command-pipeline recheck points without introducing a new boundary.
- Serialization / compatibility boundary: not applicable — no DTO or codec introduced.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: this task's own deliverable is itself a security-relevant ADR — verified to reuse (not duplicate) `ADR-004`'s `SafeReasonCode` vocabulary and `ADR-017`'s delta operations, in the Completion evidence section.
- Performance or platform constraint: not applicable.
- Other: `ADR-019` must not silently expand scope beyond `SLICE-02_BACKLOG.md` §4's fixed boundary (e.g., by including `AssistantGM` or delegation) — verified in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — exactly three roles, with a justified `PERM-INV-*` subset

**Given** `07_Permissions` §6.1's four `BaseRoleKind` values and roadmap §11.3's three-role list
**When** `ADR-019` §5 is written
**Then** it fixes exactly `MainGM`/`Player`/`Observer`, explicitly excludes `AssistantGM`, and §4 gives a per-invariant justification for each of the 8 accepted and 4 deferred `PERM-INV-*` items.

### Scenario 2 — the `ADR-017` §11 integration point is closed, not reopened elsewhere

**Given** `ADR-017` §11's unresolved "payload already redacted" assumption
**When** `ADR-019` §6.2 is written
**Then** it states the exact point (Application-layer, at `ProjectionSnapshot`/`ProjectionDeltaBatch` construction, before `Odyssey.Networking`) without touching `ADR-017`'s own file.

### Scenario 3 — revocation reuses `ADR-017`'s existing delta operations, no new mechanism

**Given** `ADR-017` §5's already-accepted `RemoveFromProjection`/`RemoveCapability` operations
**When** `ADR-019` §8 is written
**Then** it states the revocation mechanism as direct reuse of those operations, with no new operation type, channel, or parallel mechanism introduced.

### Scenario 4 — no new `SafeReasonCode`

**Given** `ADR-004`'s existing `SafeReasonCode` enum already covering `PERM-INV-012`'s required vocabulary
**When** `ADR-019` §9 is written
**Then** it confirms this directly, naming the five existing values, and states no new code is needed for baseline.

### Required invariants

- `ADR-019` does not modify `ADR-015`'s, `ADR-016`'s, `ADR-017`'s, or `ADR-018`'s own files.
- `ADR-019` does not introduce `AssistantGM`, delegation, or arbitrary `PermissionKey`/`Scope` anywhere in its normative text.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-006` row status.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` exists, `Status: Accepted`, mirroring `ADR-015`–`018`'s structural format.
2. `ADR-019` fixes exactly three baseline roles (`MainGM`/`Player`/`Observer`), explicitly excluding `AssistantGM`, sourced to roadmap §11.3's exact list.
3. `ADR-019` §4 gives the exact accepted/deferred `PERM-INV-001`–`012` subset with a stated reason for each.
4. `ADR-019` §6 fixes two distinct host-side check points (action check inside `ADR-002`'s pipeline; visibility check at Application-layer projection-construction time, before `Odyssey.Networking`).
5. `ADR-019` §7 fixes redaction as a single-authoritative-state-plus-per-connection-filter mechanism, not N independently maintained copies.
6. `ADR-019` §8 fixes revocation-removes-data as direct reuse of `ADR-017`'s existing `RemoveFromProjection`/`RemoveCapability` operations, introducing no new mechanism.
7. `ADR-019` §9 confirms no new `SafeReasonCode` is introduced, naming the five reused values.
8. `ADR-015`/`016`/`017`/`018` and all `ODY-S02-001`–`005` files are unmodified by this task's diff.
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

- Read `ADR-019` end-to-end after writing to confirm the role list, `PERM-INV-*` subset table, both check-point sections, the redaction mechanism, the revocation mechanism, and the `SafeReasonCode` confirmation are all present and substantive, per this task's own explicit instructions.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`), used only to confirm the existing solution is unaffected.

### Validation not required by this task

- Any empirical test of the permission model — that is `ODY-S02-007` (`SP-04`)'s scope, not this task's.
- Any test of a real implementation — no code exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-019` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: None directly — this task touches no code, credential, or campaign data. Its own deliverable (`ADR-019`) is itself the normative source for what counts as `Secret`/`HiddenGameplay` visibility in the permission model, per already-accepted `ADR-010` classification (referenced, not redefined).
- Trust boundaries: Not applicable to this task's own execution; `ADR-019` §6/§7 fix the host-authoritative trust boundary for permission/visibility decisions for future code.
- Authorization / audience checks: `ADR-019` is itself the authorization-model ADR — its own content is the deliverable being reviewed for correctness, not a subject this task performs authorization checks against.
- Redaction requirements: `ADR-019` §7/§8 fix the redaction mechanism and its enforcement point; this task's own execution introduces no redaction-relevant code.
- Log-safe fields: Not applicable to this task's own execution.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable to this task itself; `ADR-019` §13 states what a future implementation task's own security tests must prove.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone. This task directly matches the explicit §1.2 trigger "affects ... security, permissions, hidden information" — it is, literally, the ADR introducing the first concrete permissions model in this repository. It also required real investigative work before the content was known: reading a 3101-line product document and determining exactly which of its 12 invariants and which of its role definitions belonged in a narrow MVP baseline versus its full generality, cross-checking against `ADR-017`'s specific unresolved integration point rather than inventing a parallel mechanism — matching §1.2's "requires investigation before the implementation path is known" trigger. It carries real, documented design tradeoffs (`ADR-019` §14, five rejected alternatives), the same kind of judgment-call weight `ODY-S02-001`/`003`/`004`/`005`'s own ExecPlan justifications described for comparable ADR-authoring decisions.
- ExecPlan path: `docs/plans/active/ODY-S02-006_ADR_Permissions_Baseline.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-019`, this task contract, and the backlog row update).
- Milestone or sequencing constraints: depends on `ODY-S02-005` (merged, PR #42) per `SLICE-02_BACKLOG.md` §5 (a permission check needs a stable actor identity to check against) — confirmed satisfied. Blocks `ODY-S02-007` (`SP-04`), which needs this contract to empirically test.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-006` row only).
- Documents that must not change: `ADR-001`–`018`, `07_Permissions_Odyssey_VTT_v0.7.md`, `docs/tasks/active/ODY-S02-001`–`005_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first concrete permissions/role-model contract (documentation only, no code-level version bump, since no code changes).
- Documentation version changes: `ADR-019` is a new document (v1.0); no existing ADR changes version.
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

- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` — new.
- `docs/tasks/active/ODY-S02-006_ADR_Permissions_Baseline.md` (this file), `docs/plans/active/ODY-S02-006_ADR_Permissions_Baseline.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-006` row status.

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
| AC-1 | Passed | `ADR-019` Status: `Accepted`, 16-section structure mirroring `ADR-015`–`018`. |
| AC-2 | Passed | `ADR-019` §5, `AssistantGM` explicitly excluded, sourced to roadmap §11.3. |
| AC-3 | Passed | `ADR-019` §4, two tables (accepted/deferred) with per-item reasons. |
| AC-4 | Passed | `ADR-019` §6.1/§6.2, two distinct points named. |
| AC-5 | Passed | `ADR-019` §7, single-state-plus-filter mechanism. |
| AC-6 | Passed | `ADR-019` §8, `RemoveFromProjection`/`RemoveCapability` reuse. |
| AC-7 | Passed | `ADR-019` §9, five existing `SafeReasonCode` values named. |
| AC-8 | Passed | `git status --porcelain` confirms no `ADR-015`–`018` or `ODY-S02-001`–`005` file touched. |
| AC-9 | Passed | See Validation results table above — all four commands pass. |
| AC-10 | Passed | `git diff --name-status` against `main` shows only `ADR-019`, this task contract, its ExecPlan, and the one `SLICE-02_BACKLOG.md` row. |
| AC-11 | Pending | PR [#43](https://github.com/odyssey-services/Odyssey_VTT/pull/43) opened as Draft; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (deliberately left to future tasks, not this one): whether/when to expand this baseline to `AssistantGM`, delegation, or the full `PermissionKey`/`Scope` system — `ADR-019` §10/§16 states this requires an amendment or superseding ADR, not silent expansion.
- Risk: `ADR-019`'s revocation mechanism (§8) has not yet been empirically proven — that is exactly `ODY-S02-007` (`SP-04`)'s job; this task's own risk is limited to the contract being wrong in a way `SP-04` would then discover, which is the expected and intended division of labor, not a defect of this task.
