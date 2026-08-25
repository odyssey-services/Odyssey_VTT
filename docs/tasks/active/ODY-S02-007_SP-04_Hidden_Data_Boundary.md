# ODY-S02-007 — Technical Spike SP-04: Hidden Data Boundary

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-007-sp-04-hidden-data-boundary`
**Pull request:** Draft — [#44](https://github.com/odyssey-services/Odyssey_VTT/pull/44)
**ExecPlan:** `docs/plans/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md`
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Build a real, functional (not stubbed) harness and NUnit test suite proving roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.5's hidden-data-boundary requirement — a host-hidden object is absent from a Player's snapshot, delta, runtime state, local cache, and diagnostic export until granted, and disappears again on revocation — against real (in-process) delivery over the already-accepted `InProcessSessionTransport` (`ADR-015`), exercising the already-accepted `ADR-017`/`ADR-019` contracts. Produce a spike report stating whether those ADRs are confirmed implementable as described, or whether a real gap was found.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-017`/`ADR-019` fixed a redaction/revocation contract on paper; roadmap §11.5 (`SP-04`) requires empirical proof that contract actually holds, the same "не кодом как таковым, а принятым решением и воспроизводимым доказательством" principle (roadmap §23) `SP-02`/`SP-03` already followed.
- Value or risk reduction: catches a real gap now (before any production implementation is written against these ADRs) rather than after — and, being permanent CI-wired coverage rather than a one-shot spike log, continues catching a regression on any future change.
- Blocking or enabling relationship: the last of the seven `SLICE-02_BACKLOG.md` §2 prerequisite-revision exit criteria. Depends on `ODY-S02-004` (snapshot/delta mechanics) and `ODY-S02-006` (permissions model) — both needed to have something to test.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` (`ProjectionSnapshot`/`ProjectionDeltaBatch`/`Operations[]`)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (`VisibilityPolicy` pipeline, three roles, revocation mechanism)
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` (`InProcessSessionTransport` — the only transport this task may use)
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.5 (`SP-04` scope, verbatim requirements)
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md` (structural spike-task reference)
- `Packages/com.odyssey.application/Runtime/Diagnostics/DiagnosticBundleContracts.cs`/`DiagnosticsContracts.cs` (`ADR-010`, the one real diagnostic-export mechanism)

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites), roadmap section 11.5, backlog `ODY-S02-007`, spike registry `SP-04` (roadmap §23).
- Existing test IDs: None reused.
- New test IDs introduced: None registered in `Tests/Metadata/test-catalog.json` — the 8 new NUnit tests are themselves the evidence, matching `SP-02`'s precedent of not registering `TC-*` IDs for spike-scenario tests; unlike `SP-02`/`SP-03`, however, these tests are permanent CI-wired regression coverage, not one-shot evidence.

### Task-safe private context

- Approved summary / references: roadmap §11.5's five-surface list is quoted directly (short, customary). No hidden campaign content, secrets, or personal data referenced — the harness's "hidden entity" is a synthetic fixture (a trapdoor lever), not real product content.

## 4. Verified current state

### Verified facts

- `ODY-S02-001`–`006` (`ADR-015`–`019`, PRs #38–#43) are all merged to `main` — confirmed by `git log --oneline -10` before branching.
- No production code exists anywhere in `Odyssey.Application`/`Odyssey.Networking` implementing `ADR-017`'s `ProjectionSnapshot`/`ProjectionDeltaBatch` or `ADR-019`'s `VisibilityPolicy`/`PermissionDecision` — confirmed by `grep`; this task's harness had to write a minimal, functional (not stubbed) version itself, confined to the test project.
- `Odyssey.Application.Diagnostics.DiagnosticBundlePlanner`/`DiagnosticBundleContracts.cs` (`ADR-010`) already exists as the repository's one real diagnostic-export mechanism, including an existing text-substring safety scan (`PassesFinalExportSafetyScan`) that already denylists `"hidden"`, `"secret"`, `"private"`, `"gmnote"`, and others — confirmed by `Read`.
- `Odyssey.Tests.Networking.csproj` already exists, already referenced by `DotNet/Odyssey.Core.sln`, already wired into the `dotnet-restore-build-test` CI job — confirmed by `grep`/prior session work (`ODY-S02-001`).
- `Directory.Build.props` pins `LangVersion` to `9.0` repository-wide — confirmed by `grep`; file-scoped namespace syntax is unavailable, all new files use block-scoped namespaces.
- `ADR-015`'s `ISessionTransport`/`InProcessSessionTransport` API (`ConnectAsync`/`SendReliableAsync`/`DrainReliable`) was re-read directly from `SessionTransportContracts.cs` to confirm exact constructor/method signatures before writing the harness.

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/ProjectionModel.cs`, `ClientState.cs`, `WireCodec.cs` (new) — minimal, functional, test-project-scoped implementation of the `ADR-017`/`ADR-019` contract.
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/HiddenDataBoundaryTests.cs` (new) — 8 NUnit tests.
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/README.md` (new) — what the harness is/is not, and the location-decision reasoning.
- `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md` (this file), `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary_Report.md` (spike report), `docs/plans/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md` (governing ExecPlan).
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-007` row status only.

### Out of scope

- Any production integration of this harness's types into `Odyssey.Application`/`Odyssey.Networking` beyond what this task's own tests need.
- Any real relay/rendezvous transport — only `InProcessSessionTransport` is used.
- Closing `SLICE-02_BACKLOG.md` as a whole — a separate future closure task, by analogy to `ODY-S01-014`, performed only after the product owner explicitly accepts this report.
- Any edit to `ADR-015`/`016`/`017`/`018`/`019`.
- Delegation, arbitrary `PermissionKey`/`Scope`, field-level audience visibility, ownership/control-based visibility, multi-connection scenarios — all outside `ADR-019`'s own baseline scope, and so outside this spike's scope too.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/**
docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md
docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary_Report.md
docs/plans/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: harness types stay entirely inside `Odyssey.Tests.Networking`'s own namespace, never referenced by or referencing `Odyssey.Application`/`Odyssey.Networking` production code beyond what already exists (`InProcessSessionTransport`, `NetworkEnvelope`, `DiagnosticBundlePlanner`) — confirmed by inspection, no new production file created.
- Authoritative-state and transaction boundary: not applicable — the harness's `HostWorldState` is an in-memory test fixture, not persisted state.
- Serialization / compatibility boundary: `WireCodec` uses `System.Text.Json` directly against purpose-built wire DTOs, distinct from the harness's domain-shaped types — matching `ADR-017` §11's "wire DTO is not a domain entity" principle at harness scale; not `ADR-003`'s production canonical-codec requirement, since this is test-only code, not a persisted or production wire format.
- Time / RNG rule: `SystemWallClock` (already defined in the existing `Odyssey.Tests.Networking` test assembly, reused via `using Odyssey.Tests.Networking;`) is used for `MessageId.NewId`, matching the existing test file's own pattern — no new clock abstraction introduced.
- Unity / thread / lifetime rule: not applicable — pure .NET test code, no Unity/IL2CPP involvement.
- Dependency / licensing rule: no new third-party package dependency — `System.Text.Json` is part of the `net10.0` SDK already used throughout this repository.
- Security / privacy / redaction rule: this task's own deliverable directly tests the security-relevant hidden-data boundary; the harness's "hidden entity" is synthetic fixture data, not real product content, so no privacy concern applies to the test data itself.
- Performance or platform constraint: not applicable.
- Other: `LangVersion 9.0` (repository-wide, `Directory.Build.props`) required block-scoped namespaces in all new files, not file-scoped — followed throughout.

## 7. Expected behavior

### Scenario 1 — hidden entity absent from snapshot/delta for an unauthorized Player

**Given** a `Player`-role actor with no explicit grant, and a host world containing one `Public` and one `HiddenGameplay` entity
**When** a `ProjectionSnapshot` and an unrelated `ProjectionDeltaBatch` are built for that actor
**Then** the hidden entity's id and content are absent from both the decoded objects and the raw wire bytes.

### Scenario 2 — absent from runtime state, local cache, and diagnostic export after real delivery

**Given** the snapshot from Scenario 1, delivered over a real `InProcessSessionTransport` connection
**When** the client applies the received, decoded snapshot
**Then** its runtime-state store and its separately-modeled local-cache store both lack the hidden entity, and a diagnostic log built only from the client's own known-entity list — run through the real `DiagnosticBundlePlanner` — contains no trace of it; a deliberately forced leak attempt is independently rejected by the existing safety scan.

### Scenario 3 — grant, then revoke

**Given** the same setup
**When** the host grants visibility, builds a permission-change delta, and delivers it, then later revokes visibility and delivers the resulting delta
**Then** the client's runtime state and cache gain the entity on grant (via `AddEntity`) and lose it on revoke (via `RemoveFromProjection`), matching `ADR-017`/`ADR-019`'s revocation mechanism exactly.

### Required invariants

- No test silently passes by construction (e.g., the MainGM control case proves the harness can and does include the entity when authorized, so the Player-side exclusions are meaningful, not accidental).
- Every "absence" assertion checks either raw wire bytes or a real client-side data structure populated only through real delivery — never an assumption.

## 8. Deliverables

- Production code: None — see §5's explicit exclusion.
- Tests: `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/HiddenDataBoundaryTests.cs` — 8 new tests, all passing; full `Odyssey.Tests.Networking` suite 22/22; full solution 155/155.
- Scripts / CI: None changed — the new tests run automatically via the already-existing `Odyssey.Tests.Networking.csproj`'s CI wiring.
- Configuration: None.
- Documentation: this task contract, the spike report, the harness `README.md`, the governing ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` `ODY-S02-007` row status.
- Generated evidence or build artifacts: None beyond the test run output itself (recorded in §17).
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A real, functional (not stubbed) harness implements the `ADR-017`/`ADR-019` contract at baseline scope, confined to the test project.
2. All five roadmap §11.5 surfaces (snapshot, delta, runtime state, local cache, diagnostic export) are each covered by a real, passing test.
3. Grant and revoke are both proven to change client-visible state correctly, using `ADR-017`'s existing `AddEntity`/`RemoveFromProjection` operations.
4. A MainGM control case proves the harness does not simply omit everything — the exclusion is role-specific.
5. The diagnostic-export surface uses the real, already-existing `DiagnosticBundlePlanner` (`ADR-010`), not an invented parallel mechanism.
6. The spike report states plainly whether `ADR-017`/`ADR-019` are confirmed implementable, or names a real gap if one was found — no test was adjusted to force a green result over a genuine finding.
7. `ADR-015`/`016`/`017`/`018`/`019` are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass; `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` pass in full, including the 8 new tests.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test | Surface proven | Required result |
|---|---|---|
| `Snapshot_ForPlayerWithoutGrant_ExcludesHiddenEntity_BothInWireBytesAndDecodedPayload` | Snapshot | Pass |
| `Snapshot_ForMainGM_IncludesHiddenEntity_ControlCase` | Snapshot (control) | Pass |
| `UnrelatedChangeDelta_ForPlayerWithoutGrant_NeverMentionsHiddenEntity` | Delta | Pass |
| `ClientRuntimeStateAndLocalCache_ForPlayerWithoutGrant_NeverContainHiddenEntity_AfterRealTransportDelivery` | Runtime state, local cache | Pass |
| `DiagnosticExport_FromPlayerRuntimeState_NeverContainsHiddenEntity_AndPlannerRejectsAForcedLeak` | Diagnostic export | Pass |
| `GrantingVisibility_DeliversAddEntityDelta_ClientRuntimeAndCacheNowContainHiddenEntity` | Grant | Pass |
| `RevokingVisibility_DeliversRemoveFromProjectionDelta_ClientRuntimeAndCacheNoLongerContainHiddenEntity` | Revoke | Pass |
| `RevokingCapability_ProducesRemoveCapabilityOperation_ClientLosesAllowedCommand` | Capability revoke (bonus) | Pass |

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

- `dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~HiddenDataBoundary"` run directly to confirm the 8 new tests in isolation, in addition to the full-solution run.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable — `InProcessSessionTransport` performs no real network I/O.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`).

### Validation not required by this task

- Any real relay/rendezvous transport test.
- Any production `Odyssey.Application`/`Odyssey.Networking` implementation test — that code doesn't exist yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no production code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the harness and tests are self-contained, referenced by nothing else in the repository.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`System.Text.Json` is used but is part of the `net10.0` SDK already referenced throughout this repository — no new package reference was added.

## 13. Security, privacy, and hidden information

- Data classes handled: the harness's "hidden entity" is synthetic fixture data (`HiddenGameplay` classification per `ADR-010` §10), not real product content — no actual campaign secret is touched.
- Trust boundaries: this task's own deliverable directly tests the client/host trust boundary for hidden data — its passing result is the evidence that boundary holds at baseline scope.
- Authorization / audience checks: exercised, not defined, by this task — `ADR-019` already defines them; this task proves they work as described.
- Redaction requirements: the diagnostic-export test directly exercises `ADR-010`'s existing redaction/safety-scan mechanism, confirming it as a real backstop (§10.2 result).
- Log-safe fields: the harness's diagnostic log candidates reuse the existing registered `DiagnosticsProbeEmitted` schema verbatim (subsystem `"diagnostics"`, property `"probe"`) rather than inventing a new one.
- Abuse / malformed input limits: not applicable — the harness is test-only code, not a production input-handling path.
- Security tests: this task's entire deliverable is a security test suite for the hidden-data boundary.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from `SP-02`/`SP-03`'s precedent alone. Unlike those two (each a Brief-plan-eligible investigative measurement of an external/uncontrollable environment), this task writes real, functional code implementing a security-relevant contract (`ADR-017`/`ADR-019`'s hidden-data boundary) as permanent CI-wired test infrastructure — directly matching `PLANS.md` §1.2's explicit "affects ... security, permissions, hidden information" trigger. It also required a genuine location-and-shape design decision (test-project-permanent vs. `Tools/Spikes/`-throwaway, and how to model five distinct surfaces without collapsing any of them into a duplicate assertion) rather than a single obvious implementation path, and several real bugs were found and fixed during development (§8 of the ExecPlan) — the kind of investigative, judgment-call-heavy work `PLANS.md` §1.2 describes, not a mechanical transcription task.
- ExecPlan path: `docs/plans/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md`
- Expected pull request count: 1 (single Draft PR covering the harness, tests, report, and backlog row update).
- Milestone or sequencing constraints: depends on `ODY-S02-004`/`ODY-S02-006` (both merged) per `SLICE-02_BACKLOG.md` §5. Is the last prerequisite-revision task; its acceptance enables (but does not itself perform) a future `SLICE-02_BACKLOG.md` closure task.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, the spike report, the harness `README.md`, its ExecPlan, `docs/tasks/SLICE-02_BACKLOG.md` (`ODY-S02-007` row only).
- Documents that must not change: `ADR-001`–`019`, `docs/tasks/active/ODY-S02-001`–`006_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None — the harness's wire DTOs are test-only, not a production or persisted format.
- Documentation version changes: None — no ADR changes version; the two new task documents and the report are new files.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
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

- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/Harness/ProjectionModel.cs`, `ClientState.cs`, `WireCodec.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/HiddenDataBoundaryTests.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/README.md` — new.
- `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md` (this file), `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary_Report.md`, `docs/plans/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md` — new.
- `docs/tasks/SLICE-02_BACKLOG.md` — `ODY-S02-007` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test` (`Odyssey.Tests.Networking` only) | Passed | 22/22 (14 pre-existing + 8 new), 0 failed. |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 155/155, 0 failed. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS` (after normalizing new files to LF line endings, matching repo convention). |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `Harness/*.cs`, test-project-scoped. |
| AC-2 | Passed | 5 surfaces, each with a dedicated passing test (§10). |
| AC-3 | Passed | Grant/revoke tests, both passing. |
| AC-4 | Passed | `Snapshot_ForMainGM_IncludesHiddenEntity_ControlCase`. |
| AC-5 | Passed | Real `DiagnosticBundlePlanner.CreateManifest` call, real registered schema reused. |
| AC-6 | Passed | Report §3: no gap found, stated plainly. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-015`–`019` file touched. |
| AC-8 | Passed | See Validation results table above — all four commands pass. |
| AC-9 | Passed | `git status --porcelain` shows only files listed in §5's Allowed paths. |
| AC-10 | Pending | PR [#44](https://github.com/odyssey-services/Odyssey_VTT/pull/44) opened as Draft; CI status to be confirmed. |

## 18. Blockers, risks, and open decisions

- No blockers. No `ADR-017`/`ADR-019` gap found.
- Open decision (the product owner's, not this task's): whether to accept this report and proceed to a future `SLICE-02_BACKLOG.md` closure task (`ODY-S01-014`-style), per this task's own explicit scope boundary.
- Risk: the harness's own minimal permission/projection implementation is not the eventual production implementation — a future implementation task must not assume this harness's code can be lifted directly into `Odyssey.Application`/`Odyssey.Networking`; it proves the contract, not the production shape.
