# ODY-S01-013 — Vertical Slice Integration

**Status:** In Review
**Roadmap stage / slice:** SLICE-01 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s01-013-vertical-slice-integration`
**Pull request:** Not yet opened
**ExecPlan:** Not required — see section 14 (Brief plan)
**Created:** 2026-08-24
**Last updated:** 2026-08-24 UTC

## 1. Goal

Roadmap section 10.5's nine-step `SLICE-01` scenario runs end-to-end, in order, as one automated, reproducible test, proving `ODY-S01-007`–`012`'s deliverables work together — not just individually.

## 2. Why this task exists

- Problem: each of `ODY-S01-007`–`012` has its own module-level tests, but nothing had ever exercised the full create→import→scene→tokens→move→close→reopen→verify→restore sequence together, in the order the roadmap actually specifies.
- Value: closes the vertical-slice-level gap between "each piece works" and "the pieces work together," and gives real, reproducible evidence toward roadmap §10.6 exit criteria 1–3 (scene state survives restart; a confirmed transaction is not lost; a failed transaction leaves no partial state — the last of these already covered by `ODY-S01-009`'s own tests, the first two directly exercised here).
- Enabling relationship: `ODY-S01-014` (the full exit-criteria traceability matrix, not this task) can cite this test as concrete evidence for the criteria it covers, instead of re-deriving that evidence from scratch.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `17_Roadmap_Odyssey_VTT_v0.11.md` section 10.5 (the exact nine-step scenario, quoted verbatim, not paraphrased), section 10.6 (exit criteria, for reference only — full traceability is `ODY-S01-014`)
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` section 5 (this task's own scope-narrowing text: "does not introduce new persistence behavior beyond what ODY-S01-007–012 already implement")

### Requirement and test IDs

- Requirement IDs: roadmap §10.5 (all nine steps); §10.6 exit criteria 1–3 (partial evidence; full traceability is `ODY-S01-014`)
- Existing test IDs: `TC-PERSIST-001`–`030` (not duplicated — see section 5)
- New test IDs to introduce: `TC-PERSIST-031`

### Task-safe private context

- Approved summary / references: None

## 4. Verified current state

### Verified facts

- `007`–`012` are `Done`/merged on `main` (`git log` shows `b148713` = merge of PR #34 for `012`).
- `SceneRepositoryContracts.cs`'s own XML doc (written during `ODY-S01-008`, unchanged since) already states: `"roadmap section 10.5 steps 2-5 (import one test map, create a scene, place two tokens, change their positions)"` directly above `ISceneRepository`'s declaration — confirming `RegisterAsset` is the intended API for roadmap step 2 ("import one test map"), not `IExportRepository.ImportCampaign` (`ODY-S01-012`), which imports a whole `.odcamp` archive into a brand-new campaign — a different operation from importing one map asset into an already-open campaign.
- Running the nine-step sequence once, for real, against the actual merged `007`–`012` code (not a dry run or a plan) found **no blocker**: every step succeeded on the first attempt using only already-existing APIs exactly as documented. No production code gap was discovered.
- `docs/tasks/active/ODY-S01-008`–`012` still show stale pre-merge `Pull request` header text — the same recurring desync noted in every prior task in this session, not addressed here either (out of scope, not requested).

### Assumptions

- None.

## 5. Scope

### In scope

- One new test file, `DotNet/Tests/Odyssey.Tests.Persistence/VerticalSliceIntegrationTests.cs`, containing exactly one test method running all nine roadmap §10.5 steps in literal order, asserting each step's outcome (not nine independent tests — the guarantee under test is the full ordered sequence).
- A minimal, self-contained fixture file (12 raw bytes with a PNG magic-number header, written to a temp path by the test itself) standing in for "one test map" — no external resource required.
- A backup checkpoint (step 9's prerequisite), created after step 5 (move tokens) and before step 6 (close) — see section 18 for the placement rationale.

### Out of scope, and why

- **Any new production code in `Packages/com.odyssey.persistence/`, `Packages/com.odyssey.application/`, or anywhere else.** Confirmed: this task's diff touches only test/documentation files (`git diff --name-status` in section 17).
- **Full §10.6 exit-criteria traceability matrix** — `ODY-S01-014`, not this task.
- **A real Unity/IL2CPP end-to-end run.** This test is a standard `dotnet test` (NUnit) run — it covers the pure-.NET path only, the same path `ODY-S01-007`'s own IL2CPP preflight already proved compatible for the underlying `Microsoft.Data.Sqlite` dependency this test exercises indirectly. Whether roadmap §10.5 also requires a literal Unity Play Mode run of this same nine-step sequence is an open question this task does not resolve — flagged explicitly here, not silently assumed either way (see section 10, "Validation not required").
- **Duplicating `007`–`012`'s own module-level test scenarios.** This test calls each API once, in sequence, to prove the sequence works — it does not re-test each repository method's edge cases (idempotency, rejection paths, rotation, corruption fixtures, etc.), all already covered by their owning task's test file.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/VerticalSliceIntegrationTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S01-013_Vertical_Slice_Integration.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/**
Packages/com.odyssey.application/**
docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md
docs/tasks/active/ODY-S01-008_Scene_And_Token_Minimal_Model.md
docs/tasks/active/ODY-S01-009_Saving_Pipeline.md
docs/tasks/active/ODY-S01-010_Migration_Registry_Baseline.md
docs/tasks/active/ODY-S01-011_Backups.md
docs/tasks/active/ODY-S01-012_Export_Baseline.md
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable — no production module touched.
- Authoritative-state and transaction boundary: Not applicable — this test only calls existing, already-transactional repository APIs; it introduces no new transaction boundary.
- Time / RNG rule: `IWallClock` (the existing `SystemWallClock` test double already used across this test project) — no new time source.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency.
- Security / privacy / redaction rule: Not applicable.
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — The full nine-step sequence, in order

**Given** a fresh, empty temp directory
**When** the test runs steps 1–9 of roadmap §10.5 in literal order
**Then** every step succeeds, and step 8's verified post-reopen state matches what step 9's restored backup copy independently confirms.

### Required invariants

- The two placed tokens' moved positions are distinct from each other and from their initial positions (a real state change, not a no-op).
- The restored backup copy's state exactly matches the state independently verified after close/reopen (step 8), not just some other earlier point.

## 8. Deliverables

- Production code: None.
- Tests: `VerticalSliceIntegrationTests.cs` (1 test, `TC-PERSIST-031`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 7), this task contract.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. All nine roadmap §10.5 steps run in one test method, in literal order, each with its own assertion (`TC-PERSIST-031`).
2. Step 2 uses `ISceneRepository.RegisterAsset`, justified by `SceneRepositoryContracts.cs`'s own pre-existing XML doc naming it for this exact roadmap step.
3. The two tokens' moved positions are distinct from their initial positions and from each other.
4. Step 8 (verify saved state) confirms scene, both tokens (at moved positions), and the registered map asset all survive a real close/reopen cycle via a fresh repository instance.
5. Step 9 (restore from backup) restores into a brand-new directory (never the original) and the restored copy's token positions match the backup checkpoint exactly.
6. No new production code exists anywhere in `Packages/` (confirmed by diff).
7. If any step had failed due to a real gap in `007`–`012`, this task would stop and report the blocker instead of adding new production logic — not applicable here, since no blocker was found (see section 4).
8. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSIST-031` | .NET / `dotnet test` | The full roadmap §10.5 nine-step sequence, end-to-end, in order | Pass |

### Required commands

```powershell
.\scripts\restore.ps1
.\scripts\verify-format.ps1
.\scripts\verify-test-structure.ps1
.\scripts\test-fast.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-repository.ps1
```

### Manual validation

- None — all acceptance evidence is automated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution on `ubuntu-latest`.
- Unity editor or Player profile: Not applicable — this test does not run inside Unity; see section 5 "Out of scope" for the explicit open question about whether roadmap §10.5 also expects a literal Unity Play Mode run.
- Scripting backend: Not applicable.
- Network topology or database fixture: A single local campaign directory under `Path.GetTempPath()`, cleaned up per test.
- Other: None.

### Validation not required by this task

- A Unity Play Mode / IL2CPP run of this same nine-step sequence — flagged as an open question (section 5), not implemented. No prior `007`–`012` task included a Play Mode test of its own persistence API, so adding one here would be new test-infrastructure scope beyond "prove the already-merged APIs compose," which is this task's actual boundary.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no production code changed.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None — test-only change.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: Synthetic test data only (a 12-byte fixture file, synthetic scene/token positions).
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable — no new error paths introduced.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` section 1.1's five conditions individually, not assumed from the ТЗ's own hint. (1) Contained in one area — a single new test file, no production module touched at all. (2) Does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, package version, or build pipeline — confirmed, zero production code in the diff. (3) One clear implementation path — call each already-documented API in the order the roadmap specifies. (4) Fits one focused PR. (5) No migration or recovery procedure required — this test consumes already-existing, already-tested persistence behavior, it does not add any. All five conditions are met more cleanly than `ODY-S01-010` (which still touched one production file's DDL) — this task touches none. `PLANS.md` section 1.2's ExecPlan triggers do not apply: no Application port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed; the "affects authoritative state/persistence" trigger is read here as *modifying* that behavior, not merely *exercising* it through existing public APIs — a test-only change with zero production diff does not carry the same risk that trigger exists to flag.
- Brief plan:
  1. Files inspected: `17_Roadmap_Odyssey_VTT_v0.11.md` §10.5/10.6; `SceneRepositoryContracts.cs`'s XML doc (confirms `RegisterAsset` for step 2); `SqliteCampaignRepositoryTests.cs`/`SqliteSceneRepositoryTests.cs`/`SqliteBackupRepositoryTests.cs` (confirmed not to already cover the full ordered sequence, so no duplication).
  2. Intended change: one new test file, one test method, nine ordered, asserted steps.
  3. Tests: `VerticalSliceIntegrationTests.cs` (`TC-PERSIST-031`); full existing suite re-run to confirm no regression.
  4. Non-goals: no production code, no §10.6 traceability matrix, no Unity Play Mode run.
- ExecPlan path: Not required.
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `007`–`012` already merged (backlog's stated dependency).

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` (row 7 only).
- Documents that must not change: any ADR, `007`–`012` task contracts.
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (None required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. — pending Draft PR creation.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/VerticalSliceIntegrationTests.cs` — new, 1 test covering all nine steps.
- `Tests/Metadata/test-catalog.json` — `TC-PERSIST-031` added.
- `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md` — row 7 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\restore.ps1` | Passed | All projects restored. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001`/`TC-ARCH-002` all PASS. |
| `.\scripts\test-fast.ps1` | Passed | `Odyssey.Tests.Persistence.dll`: 45/45 (up from 44); `Odyssey.Tests.Unit.dll`: 84/84; `Odyssey.Tests.Architecture.dll`: 2/2; others: 1/1 each. |
| `.\scripts\check-repository-policy.ps1` | Passed | No new ErrorCode, registry check unaffected. |
| `.\scripts\verify-repository.ps1` | Passed | — |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `NineStepSlice_CreateImportSceneTokensMoveCloseReopenVerifyRestore_AllStepsSucceed` — all 9 steps asserted in order |
| AC-2 | Passed | Class remarks cite `SceneRepositoryContracts.cs`'s own pre-existing doc comment |
| AC-3 | Passed | Explicit `Is.Not.EqualTo` assertions on both moved positions and against each other |
| AC-4 | Passed | Step 8 assertions: token count, positions, asset hash, all via a fresh repository instance after `Open()` |
| AC-5 | Passed | Step 9 assertions: restored path ≠ original, token positions match the checkpoint |
| AC-6 | Passed | `git diff --name-status` (section 17 below) shows zero files under `Packages/` |
| AC-7 | Passed (not applicable) | No blocker encountered — documented in section 4 |
| AC-8 | Passed | See Validation results above |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: `artifacts/bin/Odyssey.Tests.Persistence/debug/Odyssey.Tests.Persistence.dll`.
- Checksums: Not recorded — debug local build.
- Test or quality report: `dotnet test` console output (section above); the single test ran in ~1s in isolation, ~11s as part of the full 45-test Persistence suite.

### Known limitations

- No Unity Play Mode / IL2CPP run of this sequence — see section 10.
- This test proves the nine steps compose correctly against a single, small, freshly-created campaign; it does not stress-test the sequence at scale (many scenes/tokens/assets) — that kind of load testing was not part of `007`–`012`'s own scope either and is not this task's to add.

### Follow-up tasks

- None assigned. `ODY-S01-014` (full §10.6 exit-criteria traceability) can cite `TC-PERSIST-031` as evidence for criteria 1–2.

### Self-review summary

- Scope review: Zero production code touched; one test file, one ordered test method, no duplication of existing module-level tests.
- Architecture review: Not applicable — no architecture changed.
- Test review: Every one of the nine roadmap steps has its own explicit assertion; the backup-checkpoint placement decision is documented and justified in the test's own class remarks.
- Security/privacy review: Not applicable.
- Documentation/version review: Only the test catalog and one backlog row required updates.

## 18. Blockers, decisions, and change control

### Blockers

- None. The full nine-step sequence passed on the first real run against already-merged `007`–`012` code — no gap requiring an owner decision was found.

### Decisions made during execution

- 2026-08-24 — Decision: step 2 ("import one test map") uses `ISceneRepository.RegisterAsset`, not `IExportRepository.ImportCampaign`. Rationale: `RegisterAsset` imports one file into an already-open campaign (exactly "one test map"); `ImportCampaign` imports an entire `.odcamp` archive into a brand-new campaign directory — a different operation the roadmap step does not describe. `SceneRepositoryContracts.cs`'s own XML doc, written during `ODY-S01-008`, already names `RegisterAsset` for this exact roadmap step, confirming this reading rather than requiring a fresh guess. Authority: task contract section 4/9.
- 2026-08-24 — Decision: the backup checkpoint for step 9 is created after step 5 (move tokens), before step 6 (close). Rationale: this makes step 9's restored-copy assertion directly comparable to step 8's independently-verified post-reopen state (both should show the same moved-token positions), giving a stronger, more meaningful check than backing up at some earlier or unrelated point. Authority: task contract section 5/9; test class remarks.

### Approved task changes

- None.
