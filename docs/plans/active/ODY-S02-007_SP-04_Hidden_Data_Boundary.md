# ODY-S02-007 — SP-04 Hidden Data Boundary

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-007-sp-04-hidden-data-boundary`
**Pull request:** Draft — [#44](https://github.com/odyssey-services/Odyssey_VTT/pull/44)
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

Proves, with real, permanent, CI-wired test code (not reasoning on paper), that the `ADR-017`/`ADR-019` contract genuinely prevents a hidden entity from reaching an unauthorized client's snapshot, delta, runtime state, local cache, or diagnostic export — and that granting/revoking permission correctly adds/removes it. This is the last prerequisite task in `SLICE-02_BACKLOG.md`; its acceptance (a separate future closure task) closes the prerequisite revision.

## 2. Task contract

- Goal: build a real harness plus NUnit tests proving all five roadmap §11.5 surfaces, over the real `InProcessSessionTransport` (`ADR-015`), and produce a spike report stating whether `ADR-017`/`ADR-019` are confirmed implementable or a gap was found.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (prerequisites), backlog `ODY-S02-007`.
- In scope: harness code (test-project-scoped, not production), the tests, the report, the task contract, this ExecPlan, `SLICE-02_BACKLOG.md`'s `ODY-S02-007` row.
- Out of scope: any production integration beyond the test's own needs, any real relay transport, closing `SLICE-02_BACKLOG.md` as a whole.
- Required authorities: `ADR-017`, `ADR-019`, `ADR-015` (`InProcessSessionTransport`), `ADR-010`/`DiagnosticBundlePlanner`, roadmap §11.5, `SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln`.

## 3. Current state

- `ODY-S02-001`–`006` (`ADR-015`–`019`) are all merged to `main`.
- No production code exists yet implementing `ADR-017`/`ADR-019`'s permission/projection pipeline — this task must write a minimal, functional (not stubbed) version confined to the test project.
- `Odyssey.Application.Diagnostics.DiagnosticBundlePlanner` (`ADR-010`) already exists as the one real diagnostic-export mechanism, with an existing text-substring safety scan.
- `Odyssey.Tests.Networking.csproj` already exists, already referenced by `DotNet/Odyssey.Core.sln`, already CI-wired.
- Unlike `SP-03`, this task requires no external environment — everything is deterministic, in-process code.

## 4. Proposed approach

Write the harness inside `DotNet/Tests/Odyssey.Tests.Networking/HiddenDataBoundary/` (not `Tools/Spikes/`) since, unlike `SP-02`/`SP-03`, this spike doesn't measure an external/uncontrollable environment — it exercises deterministic, already-accepted code and a purpose-built minimal implementation of the ADR contract, and the property it proves is exactly the kind of security regression CI should keep re-checking. Model `ProjectionSnapshot`/`ProjectionDeltaBatch`/`VisibilityPolicy` minimally per `ADR-017`/`ADR-019`, deliver real wire bytes over `InProcessSessionTransport`, and assert absence/presence across five genuinely distinct client-side surfaces (snapshot bytes, delta bytes, runtime state, a separately-modeled local cache, and a real diagnostic-export call). See the report for the full reasoning and results.

## 5. Milestones

### M1 — Harness and all five-surface tests pass

- [x] `Harness/ProjectionModel.cs`, `ClientState.cs`, `WireCodec.cs` written.
- [x] `HiddenDataBoundaryTests.cs` written: snapshot, delta, runtime+cache, diagnostic export, grant, revoke, capability revoke, MainGM control case — 8 tests.
- [x] All 8 new tests pass; all 14 pre-existing `Odyssey.Tests.Networking` tests remain passing (22/22 total); full solution 155/155.

### M2 — Report, task contract, backlog row complete

- [x] Spike report written, confirming no `ADR-017`/`ADR-019` gap found.
- [ ] `docs/tasks/active/ODY-S02-007_SP-04_Hidden_Data_Boundary.md` written, all 18 sections.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-007` row updated.
- [ ] Validation run and recorded; diff-scope confirmed.
- [ ] Draft PR opened, CI green.

## 6. Progress log

- 2026-08-25 — Preflight confirmed `ADR-015`–`019` all merged; branched cleanly.
- 2026-08-25 — Read `ADR-017`, `ADR-019`, `ADR-015`, roadmap §11.5, `SP-02` task contract as structural reference.
- 2026-08-25 — Investigated existing diagnostic export code (`DiagnosticBundlePlanner`) and confirmed it's the one real artifact for that surface.
- 2026-08-25 — Decided harness location (`Odyssey.Tests.Networking`, not `Tools/Spikes/`) with explicit reasoning.
- 2026-08-25 — Wrote harness and tests; fixed three real bugs found during first test run (namespace C# 9 compatibility, a `SafePropertyKey`/registry-schema mismatch, and a mutable-HashSet-aliasing bug in the capability-revocation test) — all test-code bugs, no production/ADR gap.
- 2026-08-25 — All 22 `Odyssey.Tests.Networking` tests pass; full solution 155/155.
- 2026-08-25 — Report written: no `ADR-017`/`ADR-019` gap found.

## 7. Decisions

- 2026-08-25 — Decision: harness lives in `Odyssey.Tests.Networking` (CI-permanent), not `Tools/Spikes/` (throwaway). Rationale: unlike `SP-02`/`SP-03`, this spike measures nothing about an external/uncontrollable environment; it exercises deterministic in-solution code, and the property under test (hidden data never leaks) deserves continuous CI re-verification, not a one-shot log. Authority: harness `README.md`; this task's own explicit instruction to reason about it rather than copy `SP-03`'s pattern blindly.
- 2026-08-25 — Decision: the minimal `ProjectionSnapshot`/`VisibilityPolicy`/etc. harness types stay entirely inside the test project's own namespace, never referencing or being referenced by `Odyssey.Application`/`Odyssey.Networking` production code. Rationale: this task's own explicit "no production integration beyond what the test needs" boundary.
- 2026-08-25 — Decision: model runtime state and local cache as two genuinely separate structures, not one assertion asked twice. Rationale: roadmap §11.5 lists them as distinct surfaces; conflating them would understate what was actually proven, particularly for the revocation case (cache must be actively purged, not merely left unread).

## 8. Discoveries and deviations

- Discovery: the repository's `Directory.Build.props` pins `LangVersion` to `9.0` repository-wide (deliberate, not accidental) — file-scoped namespace syntax (C# 10+) is unavailable; all new harness/test files use block-scoped namespaces, matching the rest of the codebase's existing style anyway.
- Discovery: `DiagnosticBundlePlanner`'s registered `DiagnosticsProbeEmitted` event code fixes an exact schema (subsystem `"diagnostics"`, a single `"probe"` property) — the first test draft used a custom subsystem/property name and failed with a generic "invalid" error until corrected to reuse the existing registered schema verbatim. Not a gap in `ADR-017`/`ADR-019` — a correct use of an already-existing, unrelated diagnostics contract.
- Discovery: a capability-revocation test initially failed because `ActorPermissionState.Capabilities` returns a live, mutable reference, not a snapshot — capturing "previous capabilities" before mutating the same instance silently observed the post-mutation state too. Fixed by adding an explicit `SnapshotCapabilities()` copy method. A test-authoring bug, not a production/ADR issue.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run and diff-scope is confirmed.

## 10. Recovery and rollback

Reverting this task's commits removes the harness/tests/report with no compatibility or data-loss risk — nothing outside this task's own files depends on the harness types (by design, per the "no production integration" scope).

## 11. Open questions and blockers

- No blockers. No `ADR-017`/`ADR-019` gap was found (report §3/§6).
- Deferred, not blocking: multi-connection scenarios, field-level audience visibility, delegation — all out of `ADR-019`'s baseline scope, so out of this spike's scope too (report §4).

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI. Closing `SLICE-02_BACKLOG.md` as a whole is a separate future task, per this task's own explicit scope boundary.
