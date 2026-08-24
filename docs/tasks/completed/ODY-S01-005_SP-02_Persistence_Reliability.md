# ODY-S01-005 — Technical Spike SP-02: Persistence Reliability

**Status:** Done  
**Roadmap stage / slice:** SLICE-01 (prerequisites)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-005-sp-02-persistence-reliability`  
**Pull request:** Draft — [#26](https://github.com/odyssey-services/Odyssey_VTT/pull/26)  
**ExecPlan:** Not required (Brief plan) — confirmed not applicable; no ExecPlan file was ever created for this task (see §14), so there is nothing to move to `docs/plans/completed/`.  
**Created:** 2026-08-24  
**Last updated:** 2026-08-24 UTC

## 1. Goal

Produce a reproducible, evidence-backed report (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md`) empirically exercising the six roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §10.4 scenarios (WAL/transaction mode, crash during a critical operation, interrupted backup, migration failure and rollback, snapshot size/speed, corrupted-database recovery) against the exact PRAGMA profile `ADR-011` §7 fixes, using a throwaway, evidence-only test harness — and to produce a justified (non-binding) recommendation on SQLite provider-library selection for `ADR-011` §12.1.

This is an investigative spike, not an implementation task. It produces no production code and selects nothing on the product owner's behalf.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-011` §12.1 explicitly defers SQLite provider-library selection to `SP-02`'s findings; `ADR-011` §7.1 explicitly states its PRAGMA profile may only be revised "только после durability-тестов — то есть только по результатам SP-02." Without this spike, both remain unverified assumptions rather than tested claims, and the `SLICE-01` prerequisite backlog cannot close (`SLICE-01_BACKLOG.md` §2, criterion 5).
- Value or risk reduction: proves — with real, killed-process crash/backup/migration runs, not just re-reading ADR prose — that the already-accepted `ADR-011`/`012`/`013` reliability claims actually hold before any production persistence code is written against them, catching a potential nonconformance now rather than after implementation.
- Blocking or enabling relationship: closes the last of the five criteria in `SLICE-01_BACKLOG.md` §2 (four ADRs already `Accepted`, this spike is the fifth and final prerequisite). Enables the future `SLICE-01` vertical-slice implementation backlog revision (`SLICE-01_BACKLOG.md` §1), which this task does not itself create.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1, §7 (investigation and spikes)
- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.4 (the six required scenarios), §23 (spike closure principle: "не кодом как таковым, а принятым решением и воспроизводимым доказательством") — private local reference, not committed to the repository
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (Accepted) §7 (PRAGMA profile under test), §12.1 (open question this spike informs)
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (Accepted) §8 (snapshot/backup contract exercised by scenarios 3 and 5)
- `docs/adr/ADR-013_Migration_Runner_v1.0.md` (Accepted) §7 (migration failure/rollback pattern exercised by scenario 4)
- `05_Persistence_Odyssey_VTT_v0.8.md` §22 (integrity validation, exercised by scenario 6) — private local reference
- `docs/tasks/completed/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md` — structural/documentation-style reference for a prior spike task in this repository (consulted, not copied — see §14 for why its Planning mode was not reused here)
- `docs/tasks/completed/ODY-S00-010_Traceability_and_Quality_Report.md` — structural reference for placing an evidence-heavy report as a sibling file to its governing task, rather than only inside the task contract's own Completion evidence section

### Requirement and test IDs

- Requirement IDs: `SLICE-01`, roadmap section 10.4, backlog `ODY-S01-005`, spike registry `SP-02` (roadmap §23).
- Existing test IDs: None (this task does not touch the `Tests/Metadata/test-catalog.json` `TC-*` registry — the harness's scenario-level pass/fail lines are spike evidence, not registered TestCase IDs).
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: roadmap §10.4's six-item list and §23's closure principle are summarized (not pasted verbatim beyond short phrases already customary in this repository's task contracts) into this task and its report. `05_Persistence` §22 (integrity validation levels) is summarized into the report's section 2.6 discussion. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`, `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `ADR-013_Migration_Runner_v1.0.md`, and `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` all carry `**Статус:** Accepted` on `main` at commit `080680e`, confirmed by `grep` before branching.
- `docs/tasks/SLICE-01_BACKLOG.md` rows for `ODY-S01-001`–`004` all read `Done` on `main`, confirmed by `Read`.
- No `docs/tasks/active/ODY-S01-005_*`, `Tools/`, or `Tools/Spikes/*` path existed on `main` prior to this task.
- No existing SLICE-01-level governing ExecPlan exists (`ODY-S01-000`'s own Planning mode is `Brief plan`, confirmed by `grep`) — unlike `ODY-S00-007`, which attached its spike as a milestone to an already-existing `ODY-S00-000` `SLICE-00` ExecPlan, there is no equivalent SLICE-01 ExecPlan to attach to here.
- `DotNet/Odyssey.Core.sln` and every CI-wired script (`scripts/restore.ps1`, `scripts/test-fast.ps1`, `scripts/verify-format.ps1`, `.github/workflows/ci.yml`) reference only paths under `DotNet/Odyssey.Core.sln`'s own project list — confirmed by `grep`; a new standalone `.csproj` outside that solution is not picked up by any of them automatically.
- `.gitignore` already excludes `bin/`/`obj/`; `Directory.Build.props` sets `UseArtifactsOutput=true`, and `scripts/check-repository-policy.ps1`'s `REPO-POLICY-002` forbidden-pattern list already excludes any path under `artifacts/` — confirmed by `Read`, so no build output from a new standalone project risks being tracked.
- No prior `.csproj` in the repository references any SQLite NuGet package — confirmed by `grep`.

### Assumptions

- "Realistic MVP campaign volume" for scenario 5 (snapshot size/speed) is not sourced from an authoritative figure in `05_Persistence_Odyssey_VTT_v0.8.md` or `02_MVP_Scope_Odyssey_VTT_v0.10.md` — neither document states one. This is stated as an explicit, harness-documented assumption (three bracketing points: 5,000 / 50,000 / 250,000 `DomainEvents`-shaped rows), not presented as a verified fact. See the report §2.5 for the full justification.

## 5. Scope

### In scope

- `Tools/Spikes/SP-02-PersistenceReliability/SP02.Harness/` (new): standalone `net10.0` console test harness exercising all six roadmap §10.4 scenarios against the `ADR-011` §7 PRAGMA profile.
- `Tools/Spikes/SP-02-PersistenceReliability/README.md` (new): explains the harness is not production code, how to reproduce it, and its explicit scope/limitations.
- `Tools/Spikes/SP-02-PersistenceReliability/evidence/*.log` (new): raw stdout from two independent harness runs, backing the report's numbers.
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md` (this file).
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` (separate report file — see §14 for the placement rationale).
- `docs/tasks/SLICE-01_BACKLOG.md` §3 — update only the `ODY-S01-005` row (Status, Planning mode).
- `THIRD_PARTY_NOTICES.md` — record the spike-scope-only `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` NuGet dependency (justified addition beyond the task's literal file list — see §14).

### Out of scope

- Any production persistence code (migration runner, snapshot writer, Domain Event Store, backup rotation) — this remains future `SLICE-01` implementation-backlog scope, not this task's.
- Selecting/pinning the SQLite provider library as a binding decision — this task produces a recommendation only (§9, AC-7); the actual decision is the product owner's, expressed either directly or as an `ADR-011` §12.1 amendment.
- Amending `ADR-011`, `ADR-012`, or `ADR-013` content or status — if a nonconformance had been found, this task would stop and flag it, not edit the affected ADR (none was found; see the report §4).
- Wiring the spike harness into `DotNet/Odyssey.Core.sln`, `.github/workflows/ci.yml`, or any repository script.
- Any change to `ODY-S00-*` files, `docs/tasks/completed/`, `docs/plans/`, or `Documentation/`.

### Allowed paths

```text
Tools/Spikes/SP-02-PersistenceReliability/**
docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability.md
docs/tasks/active/ODY-S01-005_SP-02_Persistence_Reliability_Report.md
docs/tasks/SLICE-01_BACKLOG.md
THIRD_PARTY_NOTICES.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: the spike harness is deliberately outside every `ADR-001` module boundary (`Odyssey.Domain`/`Rules`/`Content`/`Application`/`Persistence`/`Networking`/`Unity.Client`) — it has no `ProjectReference` to any of them and none of them may reference it.
- Authoritative-state and transaction boundary: Not applicable to production state — the harness's own synthetic databases are throwaway, evidence-only, and never touch a real campaign.
- Serialization / compatibility boundary: Not applicable — the harness's synthetic `DomainEvents`/`Items` tables are simplified stand-ins for measurement purposes only, not `ADR-003`-governed production contracts.
- Time / RNG rule: the harness's corruption scenario uses a seeded `Random` (fixed seed `12345`) for deterministic, reproducible byte corruption — not `ADR-008`'s deterministic Clock/RNG contracts, which do not apply to this throwaway tool.
- Unity / thread / lifetime rule: Not applicable — pure `net10.0` console app, no Unity/IL2CPP involvement, consistent with `ADR-011`/`012`/`013` being Persistence-layer contracts independent of the Unity client.
- Dependency / licensing rule: `Microsoft.Data.Sqlite` (MIT) and its pinned `SQLitePCLRaw.bundle_e_sqlite3` transitive dependency (pinned to `3.0.3` to resolve a NuGet-audit-flagged vulnerability in the `2.1.x` chain `Microsoft.Data.Sqlite` 9.0.x pulls in by default) are recorded in `THIRD_PARTY_NOTICES.md`, explicitly scoped to this spike directory only, not a production dependency.
- Security / privacy / redaction rule: no real campaign data, secrets, or personal data is used anywhere in the harness — all data is synthetic (`"row-" + i`, `"e"` repeated, etc.).
- Performance or platform constraint: measurements are illustrative for a single Windows development machine, not a certified benchmark — the report states this explicitly.
- Other: must not silently amend `ADR-011` §7's PRAGMA profile or §12.1's open question — both remain the product owner's / a future ADR-amendment's decision, per §9 AC-8/AC-9 below.

## 7. Expected behavior

This is an investigative spike; "behavior" is expressed as required evidence content rather than a production feature's runtime scenarios.

### Required invariants

- The harness applies the exact `ADR-011` §7 PRAGMA profile in every scenario, verified by reading back each PRAGMA value, not merely setting it.
- Scenario 2 (crash) kills a live child process mid-transaction (after `BEGIN`/`INSERT`, before `COMMIT`) and verifies, on reopen, exactly the expected number of committed rows, `PRAGMA integrity_check = ok`, and no partial/uncommitted row — run at least 5 times per evidence run, with an explicit N-of-N pass count reported.
- Scenario 3 (interrupted backup) kills a live child process mid-copy and verifies the temp → validate → atomic-move pattern (`ADR-012` §8.4/8.8) prevents a partial backup from ever being treated as valid, and that the source database is untouched.
- Scenario 4 (migration failure) forces a real exception mid-chain against a migration temp copy and verifies, via file hash comparison, that the working database is provably untouched and the pre-migration snapshot remains valid.
- Scenario 5 (snapshot size/speed) reports actual measured file sizes and elapsed times for at least three distinct data-volume brackets, with the volume assumption stated explicitly, not presented as an authoritative figure.
- Scenario 6 (corrupted database) corrupts real file bytes, verifies `PRAGMA integrity_check` and a live query both surface the corruption (not silently tolerate it), and verifies recovery restores into a separate file without modifying the corrupted original.
- The report separates every scenario's measured numbers from the recommendation (§3 of the report) and from the nonconformance-findings section (§4 of the report), and the recommendation is explicitly labeled non-binding.
- The harness is proven reproducible by capturing and comparing two independent full runs, not a single run.

## 8. Deliverables

- Production code: None.
- Tests: None (the harness's own scenario pass/fail checks are spike evidence, not a registered automated test suite).
- Scripts / CI: None — the harness is deliberately not wired into any CI-run script.
- Configuration: None.
- Documentation: `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md` (this file), `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md`, `Tools/Spikes/SP-02-PersistenceReliability/README.md`, the `ODY-S01-005` row update in `docs/tasks/SLICE-01_BACKLOG.md`, the `THIRD_PARTY_NOTICES.md` addition.
- Generated evidence or build artifacts: `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-01.log`, `run-2026-08-24-02.log`; validation command output recorded in §17.
- Migration / recovery material: None (this task describes and empirically tests, but does not implement, migration/backup/recovery mechanisms).

## 9. Acceptance criteria

1. `Tools/Spikes/SP-02-PersistenceReliability/SP02.Harness/` builds successfully (`dotnet build -c Release`) as a standalone project, not referenced by `DotNet/Odyssey.Core.sln`.
2. Running the built harness executes all six roadmap §10.4 scenarios and prints an explicit `PASS`/`SUMMARY` line with concrete measured numbers for each — not general statements without numbers.
3. Scenario 2 (crash) and scenario 3 (interrupted backup) each demonstrably kill a live child process mid-operation (not merely simulate a kill in-process) and verify post-crash/post-interruption state by reopening/re-reading real files.
4. Scenario 4 (migration failure) demonstrably forces a real SQL failure mid-chain and verifies working-database non-mutation via file hash comparison, not merely by inspecting in-memory state.
5. Scenario 6 (corrupted database) demonstrably corrupts real file bytes and verifies both `PRAGMA integrity_check` and a live query surface the corruption.
6. The report (`docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md`) documents, per scenario, concrete measured numbers from at least two independent harness runs, and states the scenario-5 volume assumption explicitly.
7. The report's SQLite provider-library recommendation is explicitly labeled a recommendation, not a decision, and is grounded in this spike's own findings plus stated reasoning, not asserted without justification.
8. Any finding of nonconformance with `ADR-011`/`012`/`013` is documented in the report's dedicated section rather than resolved by silently editing the affected ADR; the report explicitly states whether any nonconformance was found (none was).
9. `ADR-011`, `ADR-012`, and `ADR-013` content and status are unmodified by this task.
10. `docs/tasks/SLICE-01_BACKLOG.md` §3 shows the `ODY-S01-005` row updated to a non-`Done` status with a determined Planning mode; no other row is touched.
11. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass.
12. `git diff --name-status` against `main` shows only the files listed in §5's Allowed paths.
13. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| None | — | This task introduces no registered `TC-*` automated tests; the spike harness's own scenario checks are evidence, not part of the `Tests/Metadata/test-catalog.json` registry | — |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```powershell
# Spike harness build/run (evidence generation, not part of repository CI):
cd Tools\Spikes\SP-02-PersistenceReliability\SP02.Harness
dotnet build -c Release
..\..\..\..\artifacts\bin\SP02.Harness\release\SP02.Harness.exe
```

### Manual validation

- Ran the harness twice independently and confirmed identical pass/fail outcomes across both runs (see report §1 and the two saved evidence logs) — the timing numbers differ slightly as expected, but every scenario's `PASS`/`SUMMARY` verdict is identical.
- Cross-read the report against `ADR-011` §7/§12.1, `ADR-012` §8, and `ADR-013` §7 to confirm no scenario's setup deviates from what those ADRs actually specify.
- Confirmed via `grep` that no CI-wired script or `.sln` references the new `Tools/Spikes/` directory.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (development machine) — matches the `ADR-009` MVP target platform.
- Unity editor or Player profile: Not applicable — pure `net10.0` console harness.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable — all databases are synthetic, created and destroyed within a single harness run under the OS temp directory.
- Other: `dotnet` SDK matching `global.json` (`10.0.302`, roll-forward `latestPatch`).

### Validation not required by this task

- Unity/IL2CPP build or Player smoke: not required — this task touches no Unity/production code.
- `dotnet test .\DotNet\Odyssey.Core.sln`: not required to change — the harness is outside that solution by design; ran `dotnet build -c Release` inside the harness's own directory instead, and separately confirmed the main solution's own validation commands (`verify-format.ps1`, `check-repository-policy.ps1`) still pass unaffected.
- Cross-machine or cross-filesystem reproduction: not attempted — explicitly noted as a scope limitation in the harness `README.md` and the report.

## 11. Compatibility, migration, and rollback

Not applicable. This task produces no persisted production format, schema, contract, protocol, package, or deployable artifact — only an investigative report, a throwaway evidence harness, and administrative status updates. If the report's findings later inform an `ADR-011`/`012`/`013` amendment, that amendment's own task contract will separately complete this section.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| `Microsoft.Data.Sqlite` | 9.0.10 (nuget.org) | Spike-scope-only SQLite access for the `SP02.Harness` evidence tool | MIT | This task (`ODY-S01-005`); recorded in `THIRD_PARTY_NOTICES.md`, scoped explicitly to `Tools/Spikes/SP-02-PersistenceReliability/` only |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.3 (nuget.org, transitive, explicitly pinned) | Native SQLite bundle for the same harness, pinned above the `2.1.x` range NuGet audit flags as vulnerable (advisory `GHSA-2m69-gcr7-jv3q`) | MIT / Apache-2.0 | This task (`ODY-S01-005`); same scope as above |

Neither package is referenced by `DotNet/Odyssey.Core.sln` or any production `.csproj`; both are confined to the spike's own standalone project file.

## 13. Security, privacy, and hidden information

- Data classes handled: none real — every database, row, and file the harness creates is synthetic evidence data (`"row-" + i`, repeated filler characters), not real campaign content, secrets, or personal data.
- Trust boundaries: Not applicable — the harness runs entirely within a single local process/child-process pair under the invoking user's own OS temp directory; no network, no multi-user boundary.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable — nothing sensitive is logged; the evidence logs are safe to commit as-is (spike-synthetic data only).
- Log-safe fields: the two saved evidence logs contain only synthetic file paths (OS temp directory) and measured numbers — no real user paths, secrets, or personal data were captured.
- Abuse / malformed input limits: Not applicable — the harness is a manually-invoked local developer tool, not a network-facing or user-input-driven surface.
- Security tests: None (this task is not itself security-relevant beyond the NuGet-audit vulnerability pin recorded in §12).

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2's triggers, not presumed from either precedent this task's ТЗ pointed to. `ODY-S00-007` (a prior spike) used an **ExecPlan update** — but that was only possible because a governing `SLICE-00` ExecPlan (`docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`) already existed for it to attach to as a milestone; no equivalent `SLICE-01`-level ExecPlan exists here (`ODY-S01-000` itself chose `Brief plan`, confirmed in §4), so that specific mechanism is unavailable, not merely undesirable. Evaluated independently against `PLANS.md` §1.2's own trigger list: this task does not span multiple milestones/PRs (single Draft PR); does not change more than one production module (it changes zero production modules — the harness is explicitly outside every `ADR-001` boundary); does not introduce or change an Application port, public DTO, event, command, **persisted** schema, protocol, manifest, package, build profile, or migration (the harness's own synthetic tables are throwaway, not a persisted product contract); and, most directly relevant, does not itself **affect** authoritative state, persistence, security, or the other §1.2-listed concerns — it *investigates* whether already-accepted decisions about those concerns hold, without changing any of them (§9 AC-9 requires `ADR-011`/`012`/`013` remain unmodified). It also has one clear, linear implementation path (build harness → run twice → write report → update backlog) and completes in one focused pull request, matching every positive criterion `PLANS.md` §1.1 lists for Brief plan eligibility. `PLANS.md` §7's own guidance for spikes ("a bounded question and a deliverable... the smallest experiment... files that may be temporary") describes exactly this task's shape without itself mandating ExecPlan mode.
- ExecPlan path: Not required.
- Expected pull request count: 1 (single Draft PR covering the harness, evidence, report, and backlog/notice updates; the product owner's eventual decision on the provider-library recommendation and on closing `ADR-011` §12.1, if made, is a separate future action, not a second PR from this task).
- Milestone or sequencing constraints: depends on `ODY-S01-001` and `ODY-S01-002` per `SLICE-01_BACKLOG.md` §5 (both `Accepted`); benefits from, but per that same section is not hard-blocked by, `ODY-S01-003`'s design for the migration-failure scenario (`ADR-013` is also `Accepted`, so this dependency is fully satisfied in practice, not merely non-blocking). Closes the final criterion of `SLICE-01_BACKLOG.md` §2's exit criteria once the product owner records a closing decision on this spike, per roadmap §23's "принятое решение" requirement.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md` (this file), `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md`, `Tools/Spikes/SP-02-PersistenceReliability/README.md` (new), `docs/tasks/SLICE-01_BACKLOG.md` (`ODY-S01-005` row only), `THIRD_PARTY_NOTICES.md` (spike-scope dependency entries).
- Documents that must not change: `ADR-011`/`012`/`013`/`014`, `ODY-S01-001`–`004` task/ExecPlan (already `completed/`), `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No — this task does not touch `Odyssey.*` production code or `BuildIdentity`.
- Schema / format / contract / protocol / ruleset version change: None — the harness's synthetic schemas are throwaway and never versioned as product contracts.
- Documentation version changes: None — no versioned document (ADR, baseline) changes version by this task.
- Changelog or release-note requirement: None — pre-implementation spike, no production-facing change.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass. (None registered — see §10.)
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid. (Harness confirmed outside every `ADR-001` module boundary.)
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable. (Not applicable — see §11.)
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced. (Spike-scope `Microsoft.Data.Sqlite`/`SQLitePCLRaw` recorded and scoped in §12/`THIRD_PARTY_NOTICES.md`; now the actual `ADR-011` v1.1 provider-library decision, documentation-only — no `.csproj` dependency added.)
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [x] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Tools/Spikes/SP-02-PersistenceReliability/SP02.Harness/` — new standalone spike harness (`SP02.Harness.csproj`, `Program.cs`).
- `Tools/Spikes/SP-02-PersistenceReliability/README.md` — new, explains scope/reproduction/limitations.
- `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-01.log`, `run-2026-08-24-02.log` — new, raw evidence from two independent harness runs.
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability.md` (this file) — moved to `docs/tasks/completed/` as part of formal closure.
- `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` — the spike's findings/recommendation report, `§0 Owner decision` added, moved to `docs/tasks/completed/` alongside this file.
- `docs/tasks/SLICE-01_BACKLOG.md` — `ODY-S01-005` row moved to `Done (report accepted, ADR-011 §12.1 amended)`; §1/§2 updated to record the prerequisite backlog revision as fully closed.
- `THIRD_PARTY_NOTICES.md` — spike-scope `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` entries added (original PR), wording reviewed at closure (see closure diff for whether reworded).
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` — new amendment ADR, closes §12.1 with a binding `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3` decision, sourced from this task's report §3.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §12.1 — pointer-only note added: closed by `ADR-011` v1.1; header updated per the `ADR-010` v1.1 "Active work must use..." convention.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` §12.2 — pointer-only note added: closed by `ADR-011` v1.1.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build -c Release` (harness) | Passed | 0 warnings, 0 errors; see build transcript captured during this task's execution. |
| Harness run 1 | Passed | All six scenarios reported `PASS`/`SUMMARY: True`; saved at `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-01.log`. |
| Harness run 2 | Passed | Identical pass/fail outcomes to run 1; saved at `Tools/Spikes/SP-02-PersistenceReliability/evidence/run-2026-08-24-02.log`. |
| `.\scripts\verify-format.ps1` (authoring PR #26) | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` (authoring PR #26) | Passed | `Repository policy check passed.` |
| `.\scripts\verify-format.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence. |
| `.\scripts\check-repository-policy.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `dotnet build -c Release` succeeded standalone, outside `DotNet/Odyssey.Core.sln`. |
| AC-2 | Passed | All six scenarios in both evidence logs print concrete numbers and an explicit `PASS`/`SUMMARY` verdict. |
| AC-3 | Passed | Scenario 2/3 use real `Process.Start`/`Process.Kill(entireProcessTree: true)` against a real child process, confirmed in `Program.cs`. |
| AC-4 | Passed | Scenario 4 uses a real invalid SQL statement caught as a real `SqliteException`, and SHA-256 file-hash comparison before/after. |
| AC-5 | Passed | Scenario 6 corrupts real file bytes via `FileStream` write; both `PRAGMA integrity_check` and a live `SELECT` demonstrably surface the corruption in both evidence logs. |
| AC-6 | Passed | Report §2 documents all six scenarios with numbers from both runs; §2.5 states the volume assumption explicitly. |
| AC-7 | Passed | Report §3 is explicitly labeled "This is a recommendation, not a decision," with stated reasoning. |
| AC-8 | Passed | Report §4 explicitly states no nonconformance was found, with per-ADR cross-checks. |
| AC-9 | Passed | Held for the entire authoring PR #26 diff (`ADR-011`/`012`/`013` untouched). At closure, the product owner explicitly approved amending `ADR-011` §12.1 and pointer-noting `ADR-012` §12.2 in this same iteration — a deliberate, owner-directed exception to this criterion's original scope, not a silent violation of it; see §18. |
| AC-10 | Passed | `SLICE-01_BACKLOG.md` `ODY-S01-005` row updated; other rows unchanged, confirmed via diff-scope check. |
| AC-11 | Passed | `verify-format.ps1` and `check-repository-policy.ps1` both passed (authoring and closure runs). |
| AC-12 | Passed | `git diff --name-status` against `main` limited to the files listed in `Changed files / areas` above, plus this closure's `ADR-011`/`ADR-012`/backlog/report updates, matching the closure ТЗ's own expected diff scope. |
| AC-13 | Passed | Draft PR #26 opened, all 4 required CI checks green; remained Draft through formal closure — not moved to Ready without separate confirmation. |

## 18. Blockers, risks, and open decisions

- Blocker: none. All prerequisite ADRs (`ADR-011`–`014`) are `Accepted` and `ODY-S01-001`–`004` are `Done`, confirmed in §4.
- Closure (2026-08-24): Product owner accepted the report and its §3 recommendation as-is, and explicitly approved amending `ADR-011` §12.1 on this recommendation's basis in this same iteration, not deferred. `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` created, closing §12.1 with `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3`. `ADR-011` v1.0 §12.1 and `ADR-012` §12.2 updated with pointer-only closure notes. Task Status moved to `Done`, moved to `docs/tasks/completed/` together with the report. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-005` row moved to `Done`; §1/§2 updated to record the `SLICE-01` prerequisite backlog revision as fully closed (all five exit criteria satisfied: four ADRs `Accepted`, `SP-02` report complete and owner-reviewed).
- Risk (retrospective, no longer open): measured timing numbers (throughput, elapsed milliseconds) are machine-specific and will vary on other hardware; the report states this explicitly and treats pass/fail outcomes, not exact timings, as the load-bearing evidence — the owner's acceptance was of the pass/fail findings and the qualitative recommendation reasoning, not a claim that these exact numbers are portable to other hardware.
