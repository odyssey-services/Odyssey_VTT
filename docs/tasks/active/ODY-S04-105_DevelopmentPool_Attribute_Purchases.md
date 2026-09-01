# ODY-S04-105 — `DevelopmentPool` & Attribute Purchases

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-105-development-pool-attribute-purchases`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-105_DevelopmentPool_Attribute_Purchases.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement `ADR-024` §4–5: `DevelopmentPool` as ledger data inside the `Character` aggregate's `Mechanics` section (not a subordinate aggregate), `GrantDevelopmentPoints` (MainGM-only), `PurchaseAttributeIncrease` (one transaction: pool + entry + event + ledger), with `CommandId`/`AppliedCommands` as the sole idempotency/duplicate-spend mechanism.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-105` as the fifth implementation task and the first of the development-economy block, depending on `ODY-S04-101` (Character aggregate/`Mechanics` section) and `ODY-S04-104` (purchases apply only to an `Active` Character, roadmap §13.8 step 6 following step 5).
- Value or risk reduction: proves `ADR-024`'s in-aggregate ledger model and duplicate-spend prevention against real persistence before `ODY-S04-106`/`107` build skill purchases/revert/respec on top of it.
- Blocking or enabling relationship: unblocks `ODY-S04-106` (skill purchases, `CriticalSuccessEvidence`) and `ODY-S04-107` (revert/`CharacterRespec`), both expected to reuse this task's own `MutateMechanics` helper.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 5) — the binding scope definition for this task.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` §4–5 (full read — the governing sections).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §5 (`Mechanics` section, first real use).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §11 (attributes), §12 (`DevelopmentPool`/`DevelopmentTransaction`), §13.1–13.2 (immediate purchase, `AdvancementPurchase`), `CAP-INV-002`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`ODY-S04-101`–`104`'s own code, especially `ODY-S04-102`'s `MutateOwnership`) — read in full as the binding structural precedent.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-105`, `ADR-024` §4–5.
- Existing test IDs reused: None directly reused; `PurchaseAttributeIncrease`'s permission tests reuse `ODY-S04-102`'s own `CharacterOwnershipAssignment.IsAssignedCharacter` (not its tests, its production code).
- New test IDs introduced: `TC-CHAR-038` through `TC-CHAR-049` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main` advanced local `main` to `592fd38`, the merge commit for PR #88 (`ODY-S04-104`); `git merge-base --is-ancestor` independently confirmed it is a real ancestor of `origin/main`.
- `MechanicsRevision` has existed since `ODY-S04-101` but no business command called it before this task — confirmed by `Grep`.
- No Ruleset-catalog/attribute-cost mechanism and no `AttributeDefinitionId`/`AttributeValue`/`DevelopmentPool`/`DevelopmentTransaction` domain types existed prior to this task — confirmed by `Grep` across `Packages`.
- `CharacterOwnershipAssignment.IsAssignedCharacter` (`ODY-S04-102`) satisfies product §13.1's "assigned character" permission concern directly, reused unmodified.
- A real bug was found and fixed during test execution: `DevelopmentTransactionRecord`'s constructor requires a non-empty `RulesetVersion`, but `current.RulesetVersion` is legitimately empty for a Character created via `ODY-S04-101`'s bare `CreateCharacter` path — fixed by sourcing the ledger's `RulesetVersion` from `campaign.Manifest.RulesetVersion` instead, which is also the architecturally correct source.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `DevelopmentTransactionId` (additive).
- `Packages/com.odyssey.domain/Runtime/Character/DevelopmentEconomy.cs` (new) — `AttributeDefinitionId`, `DevelopmentTransactionKind`, `DevelopmentPool`, `AttributeValue`, `DevelopmentTransaction`.
- `Packages/com.odyssey.rules/Runtime/Character/AttributeCostRules.cs` (new) — explicitly-flagged test-fixture cost/cap.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterRepository.GrantDevelopmentPoints`/`PurchaseAttributeIncrease`/`GetDevelopmentLedger`; `DevelopmentTransactionRecord`; `CharacterRecord.DevelopmentPool`/`Attributes`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — four new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — four new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — schema (`PoolEarned`/`PoolSpent`/`PoolReserved`/`AttributesJson` columns, new `DevelopmentTransaction` table), `SelectColumns`/`ReadCharacterRecord`/`WithRevisions` extension, `MutateMechanics` shared helper, three new methods.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterDevelopmentPoolAttributePurchaseTests.cs` (new) — 12 tests.
- `docs/errors/ERROR_CODES.md` (edit) — four new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-038`–`049`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 5 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Skill purchases, `CriticalSuccessEvidence`, skill-5+ recommendation — `ODY-S04-106`.
- Revert/`CharacterRespec` — `ODY-S04-107`.
- Ability/resource/anatomy — `ODY-S04-108`/`109`.
- Archive/delete, Dead/restore, `.odchar`, Ruleset migration — `ODY-S04-110`–`113`.
- Concrete production attribute cost/cap balance tables — this task uses an explicitly-flagged minimal test fixture only.
- Any Unity/UI code — this task is purely Domain/Rules/Application/Persistence.
- Any change to `ADR-022`/`024` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/DevelopmentEconomy.cs
Packages/com.odyssey.rules/Runtime/Character/AttributeCostRules.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterDevelopmentPoolAttributePurchaseTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-105_DevelopmentPool_Attribute_Purchases.md
docs/plans/active/ODY-S04-105_DevelopmentPool_Attribute_Purchases.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the pure pool/attribute/ledger value types (no serializer, no Unity/SQLite reference); `Odyssey.Rules` owns cost/cap calculation (`ADR-024` §9's own explicit assignment), referencing only `Odyssey.Domain`; `Odyssey.Application` owns the repository port extension and `DevelopmentTransactionRecord`; `Odyssey.Persistence` owns the SQLite implementation. Matches `ADR-001`/`ADR-024` §9 exactly.
- Authoritative-state and transaction boundary: both commands commit through the existing, unmodified `SqliteSavingPipeline` — pool, attribute entry, event, and ledger row all inside the one transaction the pipeline already manages; `CommandId`/`AppliedCommands` remain the sole idempotency mechanism (`ADR-024` §5, not reopened).
- Serialization / compatibility boundary: `AttributesJson` uses `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), through the existing `ParseJsonPreservingStrings` date-safety helper.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`104` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Performance or platform constraint: unchanged from `ODY-S04-101`–`104`'s own established pattern.
- Other: `PurchaseAttributeIncrease`'s permission gate reuses `CharacterOwnershipAssignment.IsAssignedCharacter` (`ODY-S04-102`) rather than duplicating an ownership check; cost/cap values come from `AttributeCostRules`, an explicitly-flagged test fixture, not production Ruleset balance data.

## 7. Expected behavior

### Scenario 1 — `GrantDevelopmentPoints` is MainGM-only and increases the balance

**Given** a Character
**When** `GrantDevelopmentPoints` is called by a non-MainGM actor
**Then** it is rejected with `CharacterDevelopmentGrantDenied`.
**When** called by MainGM with the current `MechanicsRevision`
**Then** `DevelopmentPool.Earned`/`Available` increase by the granted amount and `MechanicsRevision` advances.

### Scenario 2 — `PurchaseAttributeIncrease` succeeds with sufficient balance

**Given** a Character with sufficient `DevelopmentPool.Available`
**When** `PurchaseAttributeIncrease` is called for an attribute at its current entry-level revision
**Then** `Available` decreases by the fixture cost, the attribute's `BaseValue`/`EffectiveValue` update correctly, and a `DevelopmentTransaction` (`Kind=Spend`) ledger row is co-committed.

### Scenario 3 — insufficient balance / cap exceeded are rejected with no state change

**Given** insufficient `Available`, or a target value exceeding `NormalDevelopmentCap`
**When** `PurchaseAttributeIncrease` is called
**Then** it is rejected (`CharacterDevelopmentInsufficientBalance`/`CharacterAttributeCapExceeded`) and neither the pool nor the attribute is touched.

### Scenario 4 — duplicate `CommandId` never double-spends

**Given** a successful purchase
**When** the same `CommandId` is submitted again
**Then** the stored result is replayed and the real `DevelopmentPool.Spent`/`Available` values, re-read independently, show exactly one spend.

### Required invariants

- `DevelopmentPool`/`DevelopmentTransaction` live inside the `Character` aggregate's `Mechanics` section — no subordinate aggregate.
- `CommandId`/`AppliedCommands` are the sole idempotency mechanism — no second economy-specific dedup key.
- `AttributeValue.EffectiveValue` is always computed, never stored or settable directly.
- No `ADR-022`/`024` file content changes.

## 8. Deliverables

- Production code: `DevelopmentEconomy.cs` (Domain), `AttributeCostRules.cs` (Rules), `CharacterRepositoryContracts.cs` extension (Application), `SqliteCharacterRepository.cs` extension (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: 12 new tests in `CharacterDevelopmentPoolAttributePurchaseTests.cs`, registered as `TC-CHAR-038`–`049`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `Character` columns and a new `DevelopmentTransaction` table only.

## 9. Acceptance criteria

1. `GrantDevelopmentPoints` by a non-MainGM actor is rejected.
2. `GrantDevelopmentPoints` by MainGM increases the balance and is gated by `MechanicsRevision`.
3. `PurchaseAttributeIncrease` with sufficient balance succeeds; balance decreases by cost, `AttributeValue`/`EffectiveValue` update correctly.
4. `PurchaseAttributeIncrease` with insufficient balance is rejected, no state change.
5. `PurchaseAttributeIncrease` above the attribute cap is rejected.
6. A duplicate `CommandId` for `PurchaseAttributeIncrease` does not spend the balance a second time (verified against the real balance).
7. A concurrent edit to `Mechanics` and `Identity` commits without a false conflict.
8. A stale `expectedMechanicsRevision` is rejected without state change.
9. A stale `expectedAttributeRevision` (entry-level gate) is rejected without state change.
10. `DevelopmentTransaction`/ledger correctly reflects the purchase (amount, direction, addressed attribute).
11. No change to `ADR-022`/`024` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
12. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
13. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 5 marked `Done` with a real PR link.
14. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-038` | .NET (`Odyssey.Tests.Persistence`) | `GrantDevelopmentPoints` non-MainGM rejected | Pass |
| `TC-CHAR-039` | .NET (`Odyssey.Tests.Persistence`) | `GrantDevelopmentPoints` MainGM success, gated by `MechanicsRevision` | Pass |
| `TC-CHAR-040` | .NET (`Odyssey.Tests.Persistence`) | Purchase with sufficient balance succeeds | Pass |
| `TC-CHAR-041` | .NET (`Odyssey.Tests.Persistence`) | Purchase with insufficient balance rejected, no state change | Pass |
| `TC-CHAR-042` | .NET (`Odyssey.Tests.Persistence`) | Purchase above cap rejected | Pass |
| `TC-CHAR-043` | .NET (`Odyssey.Tests.Persistence`) | Duplicate CommandId does not double-spend (real balance checked) | Pass |
| `TC-CHAR-044` | .NET (`Odyssey.Tests.Persistence`) | Mechanics + Identity edits, no false conflict | Pass |
| `TC-CHAR-045` | .NET (`Odyssey.Tests.Persistence`) | Stale `expectedMechanicsRevision` rejected | Pass |
| `TC-CHAR-046` | .NET (`Odyssey.Tests.Persistence`) | Ledger reflects Grant/Spend correctly | Pass |
| `TC-CHAR-047` | .NET (`Odyssey.Tests.Persistence`) | Unrelated actor purchase rejected | Pass |
| `TC-CHAR-048` | .NET (`Odyssey.Tests.Persistence`) | Assigned owner (not MainGM) purchase succeeds | Pass |
| `TC-CHAR-049` | .NET (`Odyssey.Tests.Persistence`) | Stale `expectedAttributeRevision` rejected | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`104`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — four new columns on the existing `Character` table (`PoolEarned`/`PoolSpent`/`PoolReserved`/`AttributesJson`), one new table (`DevelopmentTransaction`); no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new columns only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; the new columns/table are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: development-points balances and attribute values only — no hidden GM fields, no secrets, no personal data beyond the already-handled `UserId`.
- Trust boundaries: `GrantDevelopmentPoints` is MainGM-only; `PurchaseAttributeIncrease` is MainGM-or-assigned-user.
- Authorization / audience checks: caller-supplied `bool actorIsMainGm` and `CharacterOwnershipAssignment.IsAssignedCharacter` reused, matching existing conventions exactly.
- Redaction requirements: the four new `PersistenceFailures` entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: event payloads carry only amounts/values/actor/revision counters — no secret data.
- Abuse / malformed input limits: `amount`/`toValue` validated non-negative/positive as appropriate; `AttributeDefinitionId` validated against a safe identifier pattern.
- Security tests: `TC-CHAR-038` (MainGM gate), `TC-CHAR-047` (purchase permission gate).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 5 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task extends a public Application-layer contract and introduces new persisted schema/authoritative economy semantics.
- ExecPlan path: `docs/plans/active/ODY-S04-105_DevelopmentPool_Attribute_Purchases.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101`/`104` (both done). Unblocks `ODY-S04-106`/`107`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character` columns and one new table; no versioned schema migration.
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
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 120 (108 pre-existing + 12 new) — 351 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | Registry/catalog entries and task contract prepared proactively; final run recorded in the PR/report. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-038`. |
| AC-2 | Passed | `TC-CHAR-039`. |
| AC-3 | Passed | `TC-CHAR-040`. |
| AC-4 | Passed | `TC-CHAR-041`. |
| AC-5 | Passed | `TC-CHAR-042`. |
| AC-6 | Passed | `TC-CHAR-043`. |
| AC-7 | Passed | `TC-CHAR-044`. |
| AC-8 | Passed | `TC-CHAR-045`. |
| AC-9 | Passed | `TC-CHAR-049`. |
| AC-10 | Passed | `TC-CHAR-046`. |
| AC-11 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-12 | Passed | See Validation results above. |
| AC-13 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 5 status/PR link updated. |
| AC-14 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- `AttributeCostRules` (`CostPerAttributePoint=2`, `NormalDevelopmentCap=15`) is an explicitly-flagged test fixture, not production Ruleset balance data — a future Ruleset-catalog task must replace it with a real content-driven lookup, without changing `PurchaseAttributeIncrease`'s own call site.
- No `TemporaryModifierRefs`/active-effect resolution exists yet — `AttributeValue.EffectiveValue` is computed as `BaseValue + PermanentAdjustment` only, deferred until an effect mechanism exists.
- Product's own `AttributeValue`/`DevelopmentPool` schema names a few fields this task does not populate (e.g. `DevelopmentPool.DevelopmentPoolId`/`LastTransactionSequence`, `AttributeValue.TemporaryModifierRefs`) since they have no consumer yet in this task's own scope; adding them is straightforward if a later task needs them.

### Follow-up tasks

- `ODY-S04-106` — Skill Purchases, Critical Evidence & Skill 5+ Recommendation (expected to reuse `MutateMechanics`).
- `ODY-S04-107` — Advancement Revert & `CharacterRespec` (expected to reuse `MutateMechanics`).

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`024` or `SLICE-04_BACKLOG.md` change; no Unity/UI code; no production balance table authored.
- Architecture review: `DevelopmentPool`/`DevelopmentTransaction` modeled as `Mechanics`-section data inside the Character aggregate, not a subordinate aggregate (`ADR-024` §4); `CommandId`/`AppliedCommands` are the sole idempotency mechanism, verified directly against the real balance after a duplicate call, not just via rejection; `MutateMechanics` mirrors `MutateOwnership`'s own DRY role for a new section.
- Test review: every acceptance criterion has a real, non-stubbed test against a genuine temp-directory SQLite campaign — no mocked repository, no bypassed transaction pipeline.
- Security/privacy review: both permission gates actually checked (`TC-CHAR-038`/`047`); error messages redact raw exception/path detail exactly like existing Character failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-01 — Decision: `AttributeCostRules` is an explicitly-flagged test fixture, placed in `Odyssey.Rules.Character` per `ADR-024` §9's own module assignment — Authority/approval: this task's own explicit ТЗ instruction; confirmed by search that no Ruleset-catalog mechanism exists to consult instead.
- 2026-09-01 — Decision: `PurchaseAttributeIncrease`'s permission gate reuses `CharacterOwnershipAssignment.IsAssignedCharacter` (`ODY-S04-102`) rather than duplicating an ownership check — Authority/approval: product §13.1's own requirement; this task's own reuse-don't-duplicate instruction.
- 2026-09-01 — Decision: `PurchaseAttributeIncrease` checks both `expectedMechanicsRevision` and the addressed attribute's own `expectedAttributeRevision` as two independent gates — Authority/approval: `ADR-024` §4.2's own explicit text.
- 2026-09-01 — Decision: introduce a shared `MutateMechanics` helper now, for reuse by `ODY-S04-106`/`107` — Authority/approval: this task's own explicit ТЗ instruction.
- 2026-09-01 — Decision (discovered mid-task, not anticipated by the ТЗ): source the ledger row's `RulesetVersion` from `campaign.Manifest.RulesetVersion`, not `current.RulesetVersion` — Authority/approval: a real bug fix found via a genuine failing test; also the architecturally correct source per `ADR-024` §5.1 step 3's own "current campaign ruleset" framing.

### Approved task changes

- None.
