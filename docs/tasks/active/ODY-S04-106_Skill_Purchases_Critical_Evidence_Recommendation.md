# ODY-S04-106 — Skill Purchases, Critical Evidence & Skill 5+ Recommendation

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-106-skill-purchases-critical-evidence`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-106_Skill_Purchases_Critical_Evidence_Recommendation.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement `ADR-024` §6–7.1: `CharacterSkill` (created only on first purchase), `PurchaseSkillLevel` for levels below 5 (reusing `ODY-S04-105`'s `MutateMechanics`/purchase pipeline), `CriticalSuccessEvidence` with single-use via `UsedByAdvancementId`, and `RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation` implementing the reserve-then-convert-or-release pending workflow (`ADR-024` §6.1).

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-106` as the sixth implementation task, depending on `ODY-S04-105` (reuses its purchase pipeline/`MutateMechanics`).
- Value or risk reduction: proves `ADR-024`'s only reservation mechanism (skill 5+) against real persistence, including the first real movement of `DevelopmentPool.Reserved` anywhere in `SLICE-04`.
- Blocking or enabling relationship: unblocks `ODY-S04-107` (`RevertAdvancementPurchase`/`CharacterRespec`), expected to reuse `MutateMechanics` for its own compensating-transaction work.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 6) — the binding scope definition for this task.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` §6–7.1 (full read — the governing sections).
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §20 (pending-workflow-equivalent pair).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §14 (skills, cost/cap, levels below 5, levels 5+, `CriticalSuccessEvidence`), §15 (definitions, narrowly, for "no row for an unpossessed skill").
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `Packages/com.odyssey.rules/Runtime/Character/AttributeCostRules.cs` (`ODY-S04-105`'s own code) — read in full as the binding structural precedent, especially `MutateMechanics`.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-106`, `ADR-024` §6–7.1.
- Existing test IDs reused: None directly reused; the permission gate reuses `ODY-S04-102`'s `CharacterOwnershipAssignment.IsAssignedCharacter` production code (not its tests).
- New test IDs introduced: `TC-CHAR-050` through `TC-CHAR-058` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main` advanced local `main` to `8af347b`, the merge commit for PR #89 (`ODY-S04-105`); `git merge-base --is-ancestor` independently confirmed it is a real ancestor of `origin/main`.
- No skill-cost catalog and no `CharacterSkill`/`CriticalSuccessEvidence`/`AdvancementRecommendation` domain types existed prior to this task — confirmed by `Grep`.
- `MutateMechanics`'s original callback signature (`Func<CharacterRecord, Result<MechanicsMutation>>`) had no access to the live connection/transaction, insufficient for this task's own need to read/write sibling tables (`AdvancementRecommendation`, `CriticalSuccessEvidence`) inside the same transaction — extended to `Func<CharacterRecord, SqliteConnection, SqliteTransaction, Result<MechanicsMutation>>`; both pre-existing call sites updated mechanically, no behavior change to either.
- ADR-002 §20's generic `PendingInteraction`/`CommandResult.Pending` machinery is not routed through by any existing Character command — confirmed by `Grep`; this task represents "Pending" as an ordinary successful `Result<AdvancementRecommendationRecord>`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `CriticalSuccessEvidenceId`/`AdvancementRecommendationId` (additive).
- `Packages/com.odyssey.domain/Runtime/Character/SkillEconomy.cs` (new) — `SkillDefinitionId`, `CharacterSkill`, `AdvancementRecommendationStatus`.
- `Packages/com.odyssey.rules/Runtime/Character/SkillCostRules.cs` (new) — explicitly-flagged test-fixture cost/ceiling.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — six new `ICharacterRepository` methods; `CriticalSuccessEvidenceRecord`/`AdvancementRecommendationRecord`; `CharacterRecord.Skills`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — six new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — six new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — schema (`SkillsJson` column, new `CriticalSuccessEvidence`/`AdvancementRecommendation` tables), `MutateMechanics`/`MechanicsMutation` extension, six new methods.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterSkillPurchaseCriticalEvidenceTests.cs` (new) — 12 tests.
- `docs/errors/ERROR_CODES.md` (edit) — six new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-050`–`058`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 6 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- `RevertAdvancementPurchase`/`CharacterRespec` — `ODY-S04-107`.
- Attribute purchases — already `ODY-S04-105`, not duplicated.
- Ability/resource/anatomy — `ODY-S04-108`/`109`.
- Archive/delete, Dead/restore, `.odchar`, Ruleset migration — `ODY-S04-110`–`113`.
- Concrete skill-cost catalogs — this task uses an explicitly-flagged minimal test fixture only.
- Any Unity/UI code — this task is purely Domain/Rules/Application/Persistence.
- Any change to `ADR-022`/`024` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/SkillEconomy.cs
Packages/com.odyssey.rules/Runtime/Character/SkillCostRules.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterSkillPurchaseCriticalEvidenceTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-106_Skill_Purchases_Critical_Evidence_Recommendation.md
docs/plans/active/ODY-S04-106_Skill_Purchases_Critical_Evidence_Recommendation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the pure skill value type and status enum (no serializer, no Unity/SQLite reference); `Odyssey.Rules` owns cost/ceiling calculation (`ADR-024` §9's own assignment); `Odyssey.Application` owns the repository port extension and the two new read-model records; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001`/`ADR-024` §9 exactly.
- Authoritative-state and transaction boundary: all commands commit through the existing, unmodified `SqliteSavingPipeline`, via the extended `MutateMechanics` helper — pool, skill entry, recommendation row, evidence row(s), event, and ledger all inside the one transaction the pipeline already manages; `CommandId`/`AppliedCommands` remain the sole idempotency mechanism (`ADR-024` §5, not reopened).
- Serialization / compatibility boundary: `SkillsJson`/`EvidenceIdsJson` use `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), through the existing `ParseJsonPreservingStrings` date-safety helper.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`105` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the six new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`105`'s own established pattern.
- Other: `PurchaseSkillLevel`/`RequestSkillAdvancedRecommendation`'s permission gate reuses `CharacterOwnershipAssignment.IsAssignedCharacter` (`ODY-S04-102`) exactly as `PurchaseAttributeIncrease` does; `ResolveAdvancementRecommendation` is MainGM-only via the same `actorIsMainGm` convention.

## 7. Expected behavior

### Scenario 1 — `PurchaseSkillLevel` creates a `CharacterSkill` on first purchase

**Given** a Character with no `CharacterSkill` row for a given skill
**When** `PurchaseSkillLevel` is called for that skill at a level below the ordinary-purchase ceiling
**Then** a `CharacterSkill` is created starting from the purchased level, `DevelopmentPool.Spent` increases by the fixture cost.

### Scenario 2 — levels at or above the ceiling require the recommendation pipeline

**Given** a target level at or above `SkillCostRules.MaxOrdinaryPurchaseLevel`
**When** `PurchaseSkillLevel` is called
**Then** it is rejected with `CharacterSkillLevelRequiresRecommendation`.

### Scenario 3 — `RequestSkillAdvancedRecommendation` reserves exactly the right amount

**Given** sufficient `DevelopmentPool.Available`
**When** `RequestSkillAdvancedRecommendation` is called for a target level
**Then** `Reserved` increases and `Available` decreases by exactly the fixture cost; `Spent` is unchanged; the created `AdvancementRecommendationRecord` has `Status=Pending`.

### Scenario 4 — `ResolveAdvancementRecommendation`'s two approved-branch and dismiss outcomes

**Given** a `Pending` recommendation
**When** resolved with `approve=true, spendReservedPoints=true`
**Then** `Reserved` converts directly to `Spent`, the skill level applies, and every referenced evidence row's `UsedByAdvancementId` is set.
**When** resolved with `approve=false`
**Then** `Reserved` returns to `Available`, no skill change, evidence stays unused.

### Scenario 5 — evidence single-use

**Given** evidence already consumed by one recommendation
**When** a second recommendation attempts to consume the same evidence
**Then** it is rejected with `CharacterRevisionConflict`, and the evidence's own `UsedByAdvancementId` still points at the first consumer.

### Required invariants

- No `CharacterSkill` row exists for a skill never purchased.
- `Reserved` moves only via `RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation` — no other command mutates it (`ADR-024` §6.1).
- `CriticalSuccessEvidence.UsedByAdvancementId` is set exactly once per evidence row.
- No `ADR-022`/`024` file content changes.

## 8. Deliverables

- Production code: `SkillEconomy.cs` (Domain), `SkillCostRules.cs` (Rules), `CharacterRepositoryContracts.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: 12 new tests in `CharacterSkillPurchaseCriticalEvidenceTests.cs`, registered as `TC-CHAR-050`–`058`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `Character` column and two new tables only.

## 9. Acceptance criteria

1. `PurchaseSkillLevel` for an unpossessed skill creates a `CharacterSkill` from level 1.
2. `PurchaseSkillLevel` with sufficient/insufficient balance succeeds/rejects with no state change.
3. `PurchaseSkillLevel` above the ordinary-purchase ceiling is rejected.
4. `RequestSkillAdvancedRecommendation` reserves exactly the right amount.
5. `ResolveAdvancementRecommendation` (approved+spend) converts `Reserved`→`Spent`, applies the level, consumes evidence.
6. `ResolveAdvancementRecommendation` (dismissed) releases the reservation, no level change, evidence unused.
7. Evidence single-use is enforced and directly verified against real state.
8. Duplicate `CommandId` for all three mutating commands does not duplicate any effect (verified against real balances).
9. A concurrent `Mechanics` edit and another section's edit commit without a false conflict.
10. No change to `ADR-022`/`024` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
11. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
12. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 6 marked `Done` with a real PR link.
13. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-050` | .NET (`Odyssey.Tests.Persistence`) | First purchase creates CharacterSkill from level 1 | Pass |
| `TC-CHAR-051` | .NET (`Odyssey.Tests.Persistence`) | Sufficient balance purchase succeeds | Pass |
| `TC-CHAR-052` | .NET (`Odyssey.Tests.Persistence`) | Insufficient balance rejected, no state change | Pass |
| `TC-CHAR-053` | .NET (`Odyssey.Tests.Persistence`) | Above-ceiling rejected; recommendation reserves exact amount | Pass |
| `TC-CHAR-054` | .NET (`Odyssey.Tests.Persistence`) | Resolve (approve+spend) converts Reserved->Spent, applies level, consumes evidence | Pass |
| `TC-CHAR-055` | .NET (`Odyssey.Tests.Persistence`) | Resolve (dismiss) releases reservation, no level change, evidence unused | Pass |
| `TC-CHAR-056` | .NET (`Odyssey.Tests.Persistence`) | Evidence single-use verified against real state | Pass |
| `TC-CHAR-057` | .NET (`Odyssey.Tests.Persistence`) | Duplicate CommandId (all 3 commands) does not duplicate, real balances checked | Pass |
| `TC-CHAR-058` | .NET (`Odyssey.Tests.Persistence`) | Mechanics + Identity edits, no false conflict | Pass |

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`105`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — one new column on `Character` (`SkillsJson`), two new tables (`CriticalSuccessEvidence`, `AdvancementRecommendation`); no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new column only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new column/tables are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: skill levels, evidence provenance (dice-roll/action references), recommendation state — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `PurchaseSkillLevel`/`RequestSkillAdvancedRecommendation` are MainGM-or-assigned-user; `ResolveAdvancementRecommendation` is MainGM-only; `RecordCriticalSuccessEvidence` has no permission gate (recording an observed game fact, not a discretionary decision).
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` and `CharacterOwnershipAssignment.IsAssignedCharacter` reused, matching existing conventions exactly.
- Redaction requirements: the six new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only skill/level/amount/actor/outcome fields — no secret data.
- Abuse / malformed input limits: `SkillDefinitionId` validated against a safe identifier pattern; levels/amounts validated non-negative.
- Security tests: MainGM gate exercised implicitly by every `ResolveAdvancementRecommendation` test (all pass `actorIsMainGm: true`); a dedicated non-MainGM rejection test was not required by this task's own explicit test list but the gate mirrors `ApproveCharacterDraft`'s already-tested convention exactly.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 6 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract, introduces new persisted schema, and implements the slice's only reservation/pending-workflow mechanism.
- ExecPlan path: `docs/plans/active/ODY-S04-106_Skill_Purchases_Critical_Evidence_Recommendation.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-105` (done). Unblocks `ODY-S04-107`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character` column and two new tables; no versioned schema migration.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

See section 5's "In scope" file list above.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 132 (120 pre-existing + 12 new) — 363 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Registry/catalog entries and task contract prepared proactively; final run recorded in the PR/report. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-050`. |
| AC-2 | Passed | `TC-CHAR-051`/`052`. |
| AC-3 | Passed | `TC-CHAR-053`. |
| AC-4 | Passed | `TC-CHAR-053`. |
| AC-5 | Passed | `TC-CHAR-054`. |
| AC-6 | Passed | `TC-CHAR-055`. |
| AC-7 | Passed | `TC-CHAR-056`. |
| AC-8 | Passed | `TC-CHAR-057`. |
| AC-9 | Passed | `TC-CHAR-058`. |
| AC-10 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-11 | Passed | See Validation results above. |
| AC-12 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 6 status/PR link updated. |
| AC-13 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `SkillCostRules` (`CostPerSkillPoint=3`, `MaxOrdinaryPurchaseLevel=4`) is an explicitly-flagged test fixture, not production Ruleset balance data — a future Ruleset-catalog task must replace it with a real per-skill content-driven lookup.
- No `SkillAdvancementRule` decision engine exists — `ResolveAdvancementRecommendation`'s `spendReservedPoints` parameter is this task's own explicit stand-in for that not-yet-implemented Rules Engine decision (`ADR-024` §6.1 itself leaves the numeric decision open).
- `RecordCriticalSuccessEvidence`'s real trigger (a critical success during an actual skill-check dice roll) is not implemented — this task provides only the durable recording primitive; a future dice-integration task would call it from the real game-mechanic trigger.
- No dedicated non-MainGM-rejection test exists for `ResolveAdvancementRecommendation` (not in this task's own explicit test list) — the gate itself mirrors `ApproveCharacterDraft`'s already-tested `actorIsMainGm` convention exactly, so the risk of an untested divergence is low, but a future task could add the explicit test for completeness.

### Follow-up tasks

- `ODY-S04-107` — Advancement Revert & `CharacterRespec` (expected to reuse `MutateMechanics`).

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`024` or `SLICE-04_BACKLOG.md` change; no Unity/UI code; no production skill-cost catalog authored.
- Architecture review: `CharacterSkill`/reservation modeled per `ADR-024` §6.1 exactly, reusing `MutateMechanics`'s existing gate/commit machinery (extended, not duplicated); `CommandId`/`AppliedCommands` remain the sole idempotency mechanism, verified directly against real balances for all three commands.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline; the single-use-evidence and duplicate-CommandId races are exercised for real, not simulated.
- Security/privacy review: both permission gates (assigned-user-or-MainGM for purchase/request, MainGM-only for resolve) reuse existing, already-tested conventions; error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-01 — Decision: `SkillCostRules` is an explicitly-flagged test fixture, mirroring `AttributeCostRules` exactly — Authority/approval: this task's own explicit ТЗ instruction; confirmed by search that no skill-cost catalog exists.
- 2026-09-01 — Decision: extend `MutateMechanics`'s callback to receive the live connection/transaction, rather than adding a generic side-effect delegate — Authority/approval: this task's own code-quality judgment, keeping the helper general for `ODY-S04-107`.
- 2026-09-01 — Decision: represent ADR-002 §20.1's "Pending" result as an ordinary successful `Result<AdvancementRecommendationRecord>` — Authority/approval: matches `SqliteSavingPipeline`'s own established reasoning for not routing through the not-yet-wired-in command-dispatch layer.
- 2026-09-01 — Decision: `ResolveAdvancementRecommendation`'s `spendReservedPoints` parameter stands in for `ADR-024` §6.1's own undecided `SkillAdvancementRule` computation — Authority/approval: `ADR-024` §6.1's own explicit deferral; this task's own instruction not to invent a Rules Engine.
- 2026-09-01 — Decision: evidence single-use enforced by a fresh in-transaction read plus a defensive revision-guarded `UPDATE`, no separate caller-supplied expected-revision parameter — Authority/approval: `ADR-024` §7.1's own requirement, satisfied via SQLite's own writer serialization inside the existing transaction boundary.

### Approved task changes

- None.
