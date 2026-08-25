# ODY-S02-013 — Vertical Slice Integration

**Status:** In Review
**Roadmap stage / slice:** SLICE-02 (implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s02-013-vertical-slice-integration`
**Pull request:** Not yet opened
**ExecPlan:** Not required — see section 14 (Brief plan)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Roadmap §11.6's ten-step "Первая сеть" scenario runs end-to-end, in order, as one automated, reproducible test over real `InProcessSessionTransport`, proving `ODY-S02-009`–`012`'s deliverables work together — not just individually.

## 2. Why this task exists

- Problem: each of `ODY-S02-009`–`012` has its own module-level tests, but nothing had ever exercised the full host-starts→join→role→scene→move→validate→converge→disconnect→reconnect→resume sequence together, in the order the roadmap actually specifies, using each task's real wire channels.
- Value: closes the vertical-slice-level gap between "each piece works" and "the pieces work together," and gives real, reproducible evidence toward roadmap §11.7 exit criteria 2–4 (host is the sole authority; duplicate delivery does not repeat the operation; reconnect restores the assigned scene and role).
- Enabling relationship: `ODY-S02-015` (the full exit-criteria traceability matrix, not this task) can cite this test as concrete evidence for the criteria it covers, instead of re-deriving that evidence from scratch. `ODY-S02-014` (real transport swap) depends on this task existing first, per `SLICE-02_IMPLEMENTATION_BACKLOG.md` §6.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6 (the exact ten-step scenario, quoted verbatim in section 7, not paraphrased), §11.7 (exit criteria — criterion 1, real internet, is explicitly out of scope, gated behind `ADR-016` §14/`ODY-S02-014`)
- `docs/tasks/active/ODY-S01-013_Vertical_Slice_Integration.md` (structural precedent from `SLICE-01`: one test method, explicit per-step assertions, honest reporting of any real composition gap rather than an improvised in-scope fix)
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` §5 (`ODY-S02-013`'s own scope-narrowing text: the ten-step scenario over `InProcessSessionTransport`, composing `009`–`012`'s already-merged public APIs)

### Requirement and test IDs

- Requirement IDs: roadmap §11.6 (all ten steps); §11.7 exit criteria 2–4 (partial evidence; full traceability is `ODY-S02-015`)
- Existing test IDs: `TC-NET-001`–`024` (not duplicated — see section 5)
- New test IDs to introduce: `TC-NET-025`

### Task-safe private context

- Approved summary / references: roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6's ten-step scenario — private local reference, summarized/quoted only, not reproduced beyond what this task contract itself needs.

## 4. Verified current state

### Verified facts

- `009`–`012` are `Done`/merged on `main` (`git log` shows `4d3d1fb` = merge of PR #49 for `012`).
- Running the ten-step sequence once, for real, against the actual merged `009`–`012` code (not a dry run or a plan) found **one real test-design pitfall, not a production gap**: an initial draft had the Player move an unassigned `Public` scene entity; `MoveTokenService.CheckAuthorization` (`ODY-S02-011`) correctly rejected it, since a baseline `Player` may only move an entity assigned to them (`ADR-019` §5.2), regardless of that entity's visibility classification (`ODY-S02-010`, an orthogonal axis). This is the authorization and visibility models composing *correctly*, not a gap — fixed by assigning the moved entity to the Player (see section 18).
- Two composition frictions were found and are reported, not fixed (see section 18): (1) `TokenMoveOutcome` (`ODY-S02-011`) has no adapter into `ContinuityBroadcastPlanner` (`ODY-S02-012`), which takes raw `(entityId, position, revision)` rather than the outcome object directly; (2) `SceneMutableState` (`ODY-S02-011`, held by `TokenMoveSessionState`) and `SessionDeltaBuffer` (`ODY-S02-012`, held by `ReconnectSessionState`) are two independent authoritative position/revision stores for the same scene's entities, with nothing in either API enforcing that a call to one is paired with a call to the other.
- `docs/tasks/active/ODY-S02-009`–`012` still show their own historical `Pull request` header text (already correct, pointing at their respective merged PRs) — no desync found this time, unlike the pattern `ODY-S01-013` noted for `SLICE-01`.

### Assumptions

- None.

## 5. Scope

### In scope

- One new test file, `DotNet/Tests/Odyssey.Tests.Networking/Integration/VerticalSliceIntegrationTests.cs`, containing exactly one test method running all ten roadmap §11.6 steps in literal order, asserting each step's outcome (not ten independent tests — the guarantee under test is the full ordered sequence, composed through each task's real wire channels).
- Three participants: MainGM (host, local authority, no transport pair of its own), Player (the actor for steps 4–10), and Observer (default admission preset) — used to cross-check redaction at step 4 and convergence at step 7, the same three-role pattern `ODY-S02-010`/`011`/`012`'s own test suites already established.

### Out of scope, and why

- **Any new production code anywhere in `Packages/`.** Confirmed: this task's diff touches only test/documentation files (`git diff --name-status` in section 17).
- **Fixing the two composition frictions named in section 4/18.** Per this task's own explicit instruction: a found gap is reported, not improvised around within this task's scope.
- **Full §11.7 exit-criteria traceability matrix** — `ODY-S02-015`, not this task.
- **Roadmap §11.7 exit criterion 1 (real internet, not just localhost)** — gated behind `ADR-016` §14, `ODY-S02-014`'s concern, not testable over `InProcessSessionTransport`.
- **Duplicating `009`–`012`'s own module-level test scenarios.** This test calls each API once, in sequence, to prove the sequence works — it does not re-test each service's edge cases (duplicate-CommandId mismatch, capacity limits, buffer eviction boundaries, etc.), all already covered by their owning task's test file.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Networking/Integration/VerticalSliceIntegrationTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S02-013_Vertical_Slice_Integration.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.application/**
Packages/com.odyssey.networking/**
docs/tasks/active/ODY-S02-009_Identity_And_Session_Admission.md
docs/tasks/active/ODY-S02-010_Scene_Snapshot_And_Redacted_Projection_Delivery.md
docs/tasks/active/ODY-S02-011_Authoritative_Command_And_Delta_Broadcast.md
docs/tasks/active/ODY-S02-012_Reconnect_Delta_Continuity_Duplicate_Delivery_Idempotency.md
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable — no production module touched.
- Authoritative-state and transaction boundary: Not applicable — this test only calls existing, already-tested Application/Networking APIs; it introduces no new state model.
- Time / RNG rule: `IWallClock` (the existing `SystemWallClock` test double already used across this test project) — no new time source.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency.
- Security / privacy / redaction rule: Not applicable to the change itself (no new redaction logic) — the test exercises and asserts existing redaction behavior at steps 4 and 7.
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — The full ten-step sequence, in order

**Given** a fresh in-memory session and two connected `InProcessSessionTransport` pairs (Player, Observer)
**When** the test runs steps 1–10 of roadmap §11.6 in literal order, quoted here verbatim for traceability:
1. host создаёт сессию;
2. player подключается по коду;
3. GM назначает роль;
4. player получает разрешённую сцену;
5. player двигает токен;
6. host валидирует команду;
7. оба клиента видят одинаковый результат;
8. player теряет соединение;
9. player переподключается;
10. player получает текущее состояние без повторного применения команды.

**Then** every step succeeds, with its own explicit assertion, and step 10's reconnect catch-up delivers the state that changed while the Player was offline without the Player ever resubmitting the original move command.

### Required invariants

- Observer's step-4 snapshot never contains the Player's `HiddenGameplay` entity, while Player's own snapshot does (redaction holds under full composition, not just in isolation).
- Step 7's two independently connected clients receive byte-identical position/revision for the same move.
- Step 10's reconnect delivers exactly the one move that happened while the Player was offline — never a duplicate of the already-applied step-5/6 move, never a full snapshot (the missed range stays within the default delta-buffer capacity).

## 8. Deliverables

- Production code: None.
- Tests: `VerticalSliceIntegrationTests.cs` (1 test, `TC-NET-025`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (row 5), this task contract.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. All ten roadmap §11.6 steps run in one test method, in literal order, each with its own assertion (`TC-NET-025`).
2. Step 2's join and step 3's role assignment both go over real `InProcessSessionTransport` wire channels (`ODY-S02-009`), not direct `SessionAdmissionService` calls bypassing the network layer.
3. Step 4 proves redaction under full composition: Player's delivered snapshot contains the `HiddenGameplay` entity assigned to them; Observer's does not.
4. Step 5's move command is authorized because the moved entity is assigned to the Player (`ADR-019` §5.2) — not an unassigned entity, which `MoveTokenService` correctly rejects (confirmed during this task's own first real run, see section 4).
5. Step 7 shows two independently connected clients (Player, Observer) converging on identical position and revision for the same move.
6. Step 8/9 model "loses connection"/"reconnects" per `ODY-S02-012`'s own established pattern: removal from the connected-audience set, then a brand-new transport pair for the same stable `UserId` — not `ISessionTransport.Disconnect` (which does not gate delivery in this mock transport, per `ODY-S02-012`'s own finding).
7. Step 10 confirms the Player receives exactly the one move that occurred while offline via buffered catch-up (not a full snapshot, not a duplicate), and that this matches the authoritative `SceneMutableState`'s current position — proving convergence without the original command replaying.
8. No new production code exists anywhere in `Packages/` (confirmed by diff).
9. Any real API composition gap discovered while assembling the scenario is reported in this task contract (section 4/18), not silently worked around with new production logic — two such frictions were found and are reported (section 18); no blocker prevented the scenario from completing.
10. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-NET-025` | .NET / `dotnet test` | The full roadmap §11.6 ten-step sequence, end-to-end, in order, over real `InProcessSessionTransport` | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- None — all acceptance evidence is automated.

### Required environments / profiles

- OS / architecture: Windows, .NET 10 SDK (matches CI).
- Unity editor or Player profile: Not applicable — pure .NET Core code only.
- Scripting backend: Not applicable.
- Network topology or database fixture: `InProcessSessionTransport` only, no real network — roadmap §11.7 criterion 1 (real internet) is explicitly not tested here.
- Other: None.

### Validation not required by this task

- Roadmap §11.7 exit criterion 1 (real internet, not localhost) — gated behind `ADR-016` §14, `ODY-S02-014`'s concern.
- Full §11.7 exit-criteria traceability — `ODY-S02-015`, not this task.

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

- Data classes handled: Synthetic test data only (in-memory scene entities, synthetic positions), the same classes `ODY-S02-010`/`011`/`012`'s own tests already use.
- Trust boundaries: Not applicable — no new trust boundary; this test exercises existing ones.
- Authorization / audience checks: Not applicable to the test's own scope — it asserts existing `ODY-S02-009`/`010`/`011` behavior, introduces none.
- Redaction requirements: Not applicable — asserts existing `VisibilityPolicy` behavior (steps 4/7), introduces none.
- Log-safe fields: Not applicable — no new error paths introduced.
- Abuse / malformed input limits: Not applicable.
- Security tests: This test's step-4/7 assertions are a composed regression check that redaction still holds when all four tasks' code runs together, complementing (not replacing) `ODY-S02-007`'s (SP-04) and `010`/`011`/`012`'s own dedicated hidden-data-boundary suites.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` §1.1's five conditions individually, matching `ODY-S01-013`'s own precedent exactly. (1) Contained in one area — a single new test file, no production module touched at all. (2) Does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, package version, or build pipeline — confirmed, zero production code in the diff. (3) One clear implementation path — call each already-documented API in the order the roadmap specifies. (4) Fits one focused PR. (5) No migration or recovery procedure required — this test consumes already-existing, already-tested networking behavior, it does not add any. `PLANS.md` §1.2's ExecPlan triggers do not apply: no Application port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed; the "affects authoritative state/networking/security/permissions" trigger is read here, as `ODY-S01-013` read its persistence analogue, as *modifying* that behavior, not merely *exercising* it through existing public APIs — a test-only change with zero production diff does not carry the same risk that trigger exists to flag.
- Brief plan:
  1. Files inspected: `17_Roadmap_Odyssey_VTT_v0.11.md` §11.6/§11.7; `ODY-S01-013`'s task contract (structural precedent); `ODY-S02-009`–`012`'s task contracts and production source (public API surface); `SessionAdmissionTransportTests.cs`/`SceneProjectionTransportTests.cs`/`TokenMoveTransportTests.cs`/`ReconnectTransportTests.cs` (confirmed none already covers the full ordered ten-step sequence together, so no duplication).
  2. Intended change: one new test file, one test method, ten ordered, asserted steps, three participants.
  3. Tests: `VerticalSliceIntegrationTests.cs` (`TC-NET-025`); full existing suite re-run to confirm no regression.
  4. Non-goals: no production code, no §11.7 traceability matrix, no real-network run.
- ExecPlan path: Not required.
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `009`–`012` already merged (backlog's stated dependency).

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` (row 5 only), this task contract.
- Documents that must not change: any ADR, `009`–`012` task contracts.
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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Networking/Integration/VerticalSliceIntegrationTests.cs` — new, 1 test covering all ten steps.
- `Tests/Metadata/test-catalog.json` — `TC-NET-025` added.
- `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md` — row 5 status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Tests/Odyssey.Tests.Networking/Odyssey.Tests.Networking.csproj --filter "FullyQualifiedName~VerticalSliceIntegration"` | Passed | 1/1, 0 failed (after fixing a test-design authorization mismatch found on first run — see section 4/18). |
| `dotnet test DotNet/Odyssey.Core.sln` (full suite) | Passed | 200/200, 0 failed (1 Contracts + 1 Domain + 67 Networking [66 pre-existing + 1 new] + 84 Unit + 2 Architecture + 45 Persistence), including `RepositoryStructurePassesArchitectureGuard`. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All checks pass, including `REPO-POLICY-005`. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS`, all `TC-ARCH-002` controlled-fixture checks pass; catalog cross-check for `TC-NET-025` resolves now that this task contract exists. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TenStepSlice_HostStartsSession_ThroughReconnectWithoutReplay_AllStepsSucceed` — all 10 steps asserted in order. |
| AC-2 | Passed | Steps 2/3 use `SessionAdmissionClientChannel.SendJoinRequestAsync`/`SendRoleAssignmentRequestAsync` + `SessionAdmissionHostChannel.ProcessPendingRequestsAsync`, over real `InProcessSessionTransport` pairs. |
| AC-3 | Passed | Step 4 assertions: Player's snapshot `VisibleEntities.Count == 2`, Observer's `== 1`. |
| AC-4 | Passed | Section 4's documented first-run finding; the moved entity ("token_marker") is assigned to the Player in the final version. |
| AC-5 | Passed | Step 7 assertions: Player's and Observer's drained deltas have identical `X`/`Y`/`EntityRevision`. |
| AC-6 | Passed | Step 8 removes `PlayerUser` from the `connections` dictionary; step 9 opens a brand-new `ConnectPairAsync()` pair for the same `PlayerUser`. |
| AC-7 | Passed | Step 10 assertions: exactly 1 catch-up delta, 0 snapshots, revision/position match `moveState.MutableState`'s authoritative current state. |
| AC-8 | Passed | `git diff --name-status` (section 17 below) shows zero files under `Packages/`. |
| AC-9 | Passed | Section 18 documents both frictions found; neither blocked the scenario, both are reported, neither triggered an improvised fix. |
| AC-10 | Passed | See Validation results above. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: `artifacts/bin/Odyssey.Tests.Networking/debug/Odyssey.Tests.Networking.dll`.
- Checksums: Not recorded — debug local build.
- Test or quality report: `dotnet test` console output (section above); the single test ran in under 100ms in isolation.

### Known limitations

- No real-network run of this sequence — roadmap §11.7 criterion 1 remains gated behind `ADR-016` §14/`ODY-S02-014`, as this backlog's own §2.1 already established.
- This test proves the ten steps compose correctly for one session with three participants; it does not stress-test the sequence at scale (many concurrent players, large scenes) — that kind of load testing was not part of `009`–`012`'s own scope either and is not this task's to add.

### Follow-up tasks

- None assigned as new tasks. The two composition frictions (section 18) are recorded for whichever future task next touches `MoveTokenService`/`ContinuityBroadcastPlanner` composition or introduces real persistence — not urgent enough to warrant a dedicated task on their own, since both compose correctly today with a small amount of manual care at the call site.

### Self-review summary

- Scope review: Zero production code touched; one test file, one ordered test method, no duplication of existing module-level tests.
- Architecture review: Not applicable — no architecture changed; composition-only.
- Test review: Every one of the ten roadmap steps has its own explicit assertion; the two composition frictions found during assembly are documented, not silently patched around.
- Security/privacy review: Redaction (steps 4/7) asserted as a composed regression check, not newly introduced.
- Documentation/version review: Only the test catalog and one backlog row required updates.

## 18. Blockers, decisions, and change control

### Blockers

- None. The full ten-step sequence passed on the second real run against already-merged `009`–`012` code, after correcting this task's own test-scenario design (see below) — no production gap requiring an owner decision was found.

### Decisions made during execution

- 2026-08-26 — Decision/finding: the first real run of the composed scenario had the Player attempt to move an unassigned `Public` entity, which `MoveTokenService.CheckAuthorization` (`ODY-S02-011`) correctly rejected — a baseline `Player` may only move an entity assigned to them (`ADR-019` §5.2), independent of that entity's `VisibilityPolicy` classification (`ODY-S02-010`). This is the two models composing *correctly*, not a defect — fixed in this task's own test by assigning the moved entity ("token_marker") to the Player while keeping it `Public` (so both Player and Observer remain entitled recipients for step 7's convergence proof). Authority: `ADR-019` §5.2; confirmed by the real, unmodified `MoveTokenService` behavior.
- 2026-08-26 — Finding (not a blocker, reported per this task's own explicit instruction): `TokenMoveOutcome` (`ODY-S02-011`) has no adapter into `ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast` (`ODY-S02-012`), which accepts raw `(entityId, position, revision)` rather than the outcome object directly. Composing them (as this test's steps 7/8 do) requires manually unpacking `outcome.EntityId`/`outcome.Position`/`outcome.Revision` at each call site — a minor, non-blocking stitching friction, not fixed here (would be new production code, out of this task's scope).
- 2026-08-26 — Finding (not a blocker, reported per this task's own explicit instruction): `SceneMutableState` (`ODY-S02-011`, held by `TokenMoveSessionState`) and `SessionDeltaBuffer` (`ODY-S02-012`, held by `ReconnectSessionState`) are two independent authoritative position/revision stores for the same `Scene`'s entities — nothing in either API's own contract enforces that a call to `MoveTokenService.Execute` is always paired with a call to `ContinuityBroadcastPlanner.RecordAndPlanImmediateBroadcast`. This test's own steps 6–8 must call both explicitly, in the right order, for every move, to keep the two stores synchronized; the composition works when done carefully, but neither type's API prevents a caller from updating one and forgetting the other. Not fixed here (would be new production code — a unifying abstraction over both stores — out of this task's scope); recorded as a real, named limitation for a future task that next touches this composition.

### Approved task changes

- None.
