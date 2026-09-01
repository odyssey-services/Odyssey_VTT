# ODY-S04-105 — `DevelopmentPool` & Attribute Purchases

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-105-development-pool-attribute-purchases`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-024` §4–5: `DevelopmentPool` as `Mechanics`-section ledger data inside the `Character` aggregate (not a subordinate aggregate), `GrantDevelopmentPoints` (MainGM-only), `PurchaseAttributeIncrease` (one transaction: pool + entry + event + ledger), `CommandId`/`AppliedCommands` as the sole duplicate-spend guard. Fifth implementation task of `SLICE-04`, first of the development-economy block.

## 2. Task contract

- Goal: extend `ICharacterRepository`/`SqliteCharacterRepository` with `GrantDevelopmentPoints`/`PurchaseAttributeIncrease`/`GetDevelopmentLedger`, plus a shared `MutateMechanics` helper (mirroring `ODY-S04-102`'s `MutateOwnership`) that future purchase commands (`ODY-S04-106`/`107`) reuse.
- Acceptance criteria: `GrantDevelopmentPoints` MainGM-only, gated by `MechanicsRevision`; `PurchaseAttributeIncrease` checks both `MechanicsRevision` (pool) and the addressed attribute's own entry-level `Revision`, validates cost/cap via an explicitly-flagged test fixture, rejects insufficient balance/cap-exceeded with no state change; a duplicate `CommandId` never double-spends (verified against the real balance, not just rejection); `Mechanics` and `Identity` edits don't false-conflict; ledger rows correctly reflect Kind/Amount/SourceRef; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`024` content change.
- Requirement IDs: `ODY-S04-105`, `ADR-024` §4–5.
- In scope: `AttributeDefinitionId`/`DevelopmentTransactionKind`/`DevelopmentPool`/`AttributeValue`/`DevelopmentTransaction` (Domain), `AttributeCostRules` (Rules, explicitly-flagged test fixture), `ICharacterRepository` extension + `DevelopmentTransactionRecord` (Application), `SqliteCharacterRepository` extension + `MutateMechanics` helper (Persistence), tests, error registry/test-catalog additions, backlog status update.
- Out of scope: skill purchases/`CriticalSuccessEvidence`/skill-5+ recommendation (`ODY-S04-106`), revert/`CharacterRespec` (`ODY-S04-107`), ability/resource/anatomy (`ODY-S04-108`/`109`), archive/delete/Ruleset migration (`ODY-S04-110`–`113`), concrete production balance tables, any Unity/UI code, any `ADR-022`/`024` content change.
- Required authorities: `ADR-024` §4–5 (full read), `ADR-022` §5 (`Mechanics` section, first real use), product §11 (attributes)/§12 (`DevelopmentPool`/`DevelopmentTransaction`)/§13.1–13.2 (immediate purchase), `CAP-INV-002` (no `CharacterLevel`), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`104`'s own code as the binding structural precedent, especially `ODY-S04-102`'s `MutateOwnership`.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `592fd38` (PR #88, `ODY-S04-104`), independently verified via `git merge-base --is-ancestor`.
- `MechanicsRevision` has existed since `ODY-S04-101` but no business command has ever used it until now — this task is the first real caller, matching `ADR-024` §4.2's own text.
- No Ruleset-catalog/attribute-cost-table mechanism exists anywhere in this codebase (confirmed by search) — `AttributeCostRules` (`CostPerAttributePoint=2`, `NormalDevelopmentCap=15`) is this task's own explicitly-flagged test fixture, matching product §11.2/§11.3's own literal "current Ruleset" values while explicitly not claiming to be a real catalog lookup.
- No `AttributeDefinitionId`/`AttributeValue`/`DevelopmentPool`/`DevelopmentTransaction` domain types existed prior to this task — confirmed by `Grep`.
- `CharacterOwnershipAssignment.IsAssignedCharacter` (`ODY-S04-102`) is reused unmodified for `PurchaseAttributeIncrease`'s own permission gate (product §13.1's "право развивать персонажа") rather than duplicating an ownership check.
- Found and fixed a real bug during test execution: `current.RulesetVersion` is empty for a Character created via `ODY-S04-101`'s own bare `CreateCharacter` skeleton path, but `DevelopmentTransactionRecord`'s constructor requires a non-empty `RulesetVersion` — fixed by using `campaign.Manifest.RulesetVersion` (the campaign's own live ruleset version) for the ledger row instead of the Character's own potentially-empty pinned value, which is also the more correct semantic choice (a purchase must be Ruleset-compatible against the *current* campaign ruleset, ADR-024 §5.1 step 3).

Assumptions: none.

## 4. Proposed approach

- Domain (`DevelopmentEconomy.cs`, new): `AttributeDefinitionId` (a stable catalog key, not a canonical random ID -- there is exactly one small Ruleset-fixed catalog of these), `DevelopmentTransactionKind` (product §12.1's full enum, most values reserved for later tasks), `DevelopmentPool` (Earned/Spent/Reserved, `Available` computed), `AttributeValue` (BaseValue/PermanentAdjustment/`EffectiveValue` computed/SpentDevelopmentPoints/Revision), `DevelopmentTransaction` (the ledger row shape).
- Rules (`AttributeCostRules.cs`, new, `Odyssey.Rules.Character`): the explicitly-flagged test-fixture cost/cap functions, per `ADR-024` §9's own module assignment ("`Odyssey.Rules` owns... cost/cap... calculations").
- Application: `ICharacterRepository.GrantDevelopmentPoints`/`PurchaseAttributeIncrease`/`GetDevelopmentLedger`; `DevelopmentTransactionRecord`; `CharacterRecord` extended with `DevelopmentPool`/`Attributes`.
- Persistence: a shared `MutateMechanics` helper (gate on `MechanicsRevision` → load → caller-supplied pure business logic → commit pool+attributes+event+ledger in one transaction), mirroring `MutateOwnership`'s own role for a different section. `GrantDevelopmentPoints` and `PurchaseAttributeIncrease` both reuse it; `PurchaseAttributeIncrease`'s own callback additionally checks the addressed attribute's entry-level `Revision` (an attribute never purchased before has revision `0`).
- Tests: MainGM gate, balance increase/gating, successful purchase, insufficient balance, cap exceeded, duplicate-`CommandId` no-double-spend (checked against the real balance), Mechanics/Identity no-false-conflict, stale-`MechanicsRevision`/stale-attribute-`Revision` rejection, ledger correctness, plus the product §13.1 permission checks (unrelated actor rejected, assigned owner succeeds).

No Unity/UI code, no `ADR-022`/`024` content change, no concrete production balance table.

## 5. Milestones

### M1 — Domain/Rules/Application extension

- [x] `DevelopmentEconomy.cs` (Domain): scope/kind/pool/attribute/transaction types.
- [x] `AttributeCostRules.cs` (Rules): explicitly-flagged test fixture.
- [x] `ICharacterRepository` extended with three methods; `DevelopmentTransactionRecord`; `CharacterRecord.DevelopmentPool`/`Attributes`.
- [x] `PersistenceFailures`/`ErrorCodes` additions (four new entries).

### M2 — Persistence and tests

- [x] `SqliteCharacterRepository` extended: schema (`PoolEarned`/`PoolSpent`/`PoolReserved`/`AttributesJson` columns, new `DevelopmentTransaction` table), `SelectColumns`/`ReadCharacterRecord`/`WithRevisions` extension, `MutateMechanics` shared helper, three new methods.
- [x] 12 new tests in `CharacterDevelopmentPoolAttributePurchaseTests.cs`.
- [x] Real bug found during first test run (`DevelopmentTransactionRecord`'s `RulesetVersion` validation failing against `CreateCharacter`'s empty pinned value) — fixed by sourcing the ledger's `RulesetVersion` from `campaign.Manifest.RulesetVersion` instead.
- [x] `dotnet build`/`dotnet test` full suite green, no regression (339 -> 351).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-038`–`049`).
- [x] This task contract/ExecPlan, created before the final validation pass (per `ODY-S04-103`/`104`'s own discovery of `verify-test-structure.ps1`'s `TC-ARCH-001` task-contract-reference requirement).
- [ ] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 -- Preflight confirmed PR #88's merge commit is a real ancestor of `origin/main` at `592fd38`; created branch `feat/ody-s04-105-development-pool-attribute-purchases`.
- 2026-09-01 -- Read `ADR-024` §4–5 (and the rest, for context) in full, `ADR-022` §5 (re-confirmed `Mechanics` section), product §11–13.2, `CAP-INV-002`, backlog §5–7, and `ODY-S04-101`–`104`'s own code in full, especially `ODY-S04-102`'s `MutateOwnership`.
- 2026-09-01 -- Confirmed via search: no Ruleset-catalog/attribute-cost mechanism and no `DevelopmentPool`/`AttributeValue` domain types existed prior to this task.
- 2026-09-01 -- Implemented Domain/Rules/Application/Persistence extension; `dotnet build` passed on first attempt.
- 2026-09-01 -- First test run found a real bug: `DevelopmentTransactionRecord`'s constructor rejects an empty `RulesetVersion`, which `current.RulesetVersion` legitimately is for a `CreateCharacter`-created Character (`ODY-S04-101`'s own bare skeleton path) -- fixed by using `campaign.Manifest.RulesetVersion` for the ledger row instead.
- 2026-09-01 -- Second test run: all 12 new tests passed; full suite green (351/351, no regression).
- 2026-09-01 -- Added `ERROR_CODES.md`/`test-catalog.json` entries and created this task's own contract/ExecPlan proactively before the final validation pass, having learned both discipline points from `ODY-S04-101`–`104`.

## 7. Decisions

- 2026-09-01 -- Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1. Authority: `PLANS.md` §1, backlog row 5.
- 2026-09-01 -- Decision: `AttributeCostRules` (`CostPerAttributePoint=2`, `NormalDevelopmentCap=15`) is an explicitly-flagged test fixture, not production Ruleset balance data -- placed in `Odyssey.Rules.Character` per `ADR-024` §9's own module assignment. Authority: this task's own explicit ТЗ instruction; confirmed by search that no Ruleset-catalog mechanism exists yet to consult instead.
- 2026-09-01 -- Decision: `PurchaseAttributeIncrease`'s permission gate is `actorIsMainGm || CharacterOwnershipAssignment.IsAssignedCharacter(...)`, reusing `ODY-S04-102`'s own predicate rather than duplicating an ownership check. Authority: product §13.1's "у пользователя есть право развивать персонажа"; this task's own instruction to reuse existing mechanisms, not duplicate them.
- 2026-09-01 -- Decision: `PurchaseAttributeIncrease` checks both `expectedMechanicsRevision` (the pool) and `expectedAttributeRevision` (the addressed `AttributeValue`'s own `Revision`, `0` for an attribute never purchased before) -- two independent gates, per `ADR-024` §4.2's own explicit text ("declares the entry-level expected revision for the addressed attribute"). Authority: `ADR-024` §4.2, not an invented mechanism.
- 2026-09-01 -- Decision: a shared `MutateMechanics` helper (gate/load/business-logic-callback/commit) is introduced now, mirroring `MutateOwnership`'s own role for the `Ownership` section, so `ODY-S04-106`/`107`'s own future purchase commands reuse it rather than each re-implementing the same sequence. Authority: this task's own explicit ТЗ instruction.
- 2026-09-01 -- Decision (discovered mid-task, not anticipated by the ТЗ): source the `DevelopmentTransaction` ledger row's own `RulesetVersion` from `campaign.Manifest.RulesetVersion` (the campaign's live current version), not `current.RulesetVersion` (the Character's own pinned-at-creation value, which can legitimately be empty for a bare `CreateCharacter`-created Character). Authority: this task's own real-bug fix; also the more correct semantic choice since a purchase's Ruleset-compatibility check (`ADR-024` §5.1 step 3) is inherently against the *current* campaign ruleset, not a historically pinned one.

## 8. Discoveries and deviations

- **Real bug found and fixed during test execution:** `DevelopmentTransactionRecord`'s own constructor validation (`RulesetVersion` required non-empty) surfaced a genuine gap between `ODY-S04-101`'s bare `CreateCharacter` path (which pins no ruleset, `RulesetVersion=""`) and this task's own ledger requirement. Fixed by reading the ledger's `RulesetVersion` from the campaign's own live manifest instead of the Character's own field, which also happens to be the architecturally correct source for this particular value.
- No open architectural question was found that `ADR-024`/`ADR-022` do not already answer. The two points this task's own ТЗ explicitly flagged as open engineering decisions (attribute cost/cap fixture; permission gate for purchases) were both resolved using the exact reuse-don't-duplicate pattern this session has followed throughout `SLICE-04`.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 351/351 (12 new tests), zero regression.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: to be confirmed in the final validation pass (registry entries and task contract prepared proactively).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- the new `Character` table columns (`PoolEarned`/`PoolSpent`/`PoolReserved`/`AttributesJson`) are additive; the new `DevelopmentTransaction` table is new and unused by any other code path if reverted; no existing column altered.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S04-106` (skill purchases, `CriticalSuccessEvidence`, skill-5+ recommendation) and `ODY-S04-107` (revert/`CharacterRespec`), both of which are expected to reuse this task's own `MutateMechanics` helper.
