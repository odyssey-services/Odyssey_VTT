# ODY-S04-106 — Skill Purchases, Critical Evidence & Skill 5+ Recommendation

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-106-skill-purchases-critical-evidence`
**Pull request:** [#90](https://github.com/odyssey-services/Odyssey_VTT/pull/90)
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-024` §6–7.1: `CharacterSkill` (no row for an unpossessed skill), `PurchaseSkillLevel` for levels below 5 (reusing `ODY-S04-105`'s `MutateMechanics`/purchase pipeline), `CriticalSuccessEvidence` with single-use via `UsedByAdvancementId`, `RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation` implementing ADR-024 §6.1's reserve-then-convert-or-release pending workflow. Sixth implementation task of `SLICE-04`.

## 2. Task contract

- Goal: extend `ICharacterRepository`/`SqliteCharacterRepository` with skill purchases and the skill-5+ reservation pending workflow, reusing `ODY-S04-105`'s `MutateMechanics` helper (extended to give its callback direct access to the connection/transaction, so it can read/write sibling tables in the same transaction) rather than inventing a parallel ledger/transaction mechanism.
- Acceptance criteria: `PurchaseSkillLevel` creates a `CharacterSkill` on first purchase, rejects levels requiring recommendation; `RequestSkillAdvancedRecommendation` reserves exactly the right amount (`Available` down, `Reserved` up, `Spent` unchanged); `ResolveAdvancementRecommendation`'s two approved outcomes and dismissal are each implemented atomically per ADR-024 §6.1; evidence is single-use, verified against real state, not just rejection; duplicate `CommandId` never duplicates any effect, verified against real balances; `Mechanics` and other sections don't false-conflict; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`024` content change.
- Requirement IDs: `ODY-S04-106`, `ADR-024` §6–7.1.
- In scope: `SkillDefinitionId`/`CharacterSkill`/`AdvancementRecommendationStatus` (Domain), `SkillCostRules` (Rules, explicitly-flagged test fixture), `ICharacterRepository` extension + `CriticalSuccessEvidenceRecord`/`AdvancementRecommendationRecord` (Application), `SqliteCharacterRepository` extension + `MutateMechanics` signature extension (Persistence), tests, error registry/test-catalog additions, backlog status update.
- Out of scope: `RevertAdvancementPurchase`/`CharacterRespec` (`ODY-S04-107`), attribute purchases (already `ODY-S04-105`), ability/resource/anatomy (`ODY-S04-108`/`109`), archive/delete/Ruleset migration (`ODY-S04-110`–`113`), concrete skill-cost catalogs, any Unity/UI code, any `ADR-022`/`024` content change.
- Required authorities: `ADR-024` §6–7.1 (full read), `ADR-002` §20 (pending-workflow-equivalent pair), product §14 (skills)/§15 (definitions, narrowly), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-105`'s own `MutateMechanics`/`AttributeCostRules.cs` as the binding structural precedent.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `8af347b` (PR #89, `ODY-S04-105`), independently verified via `git merge-base --is-ancestor`.
- `MutateMechanics`'s callback originally only received `CharacterRecord current` -- extended this task to also pass the live `SqliteConnection`/`SqliteTransaction`, so `RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation` can read/write the new `AdvancementRecommendation`/`CriticalSuccessEvidence` tables inside the exact same transaction, rather than growing a special-cased side-effect slot on `MechanicsMutation` for one caller.
- No skill-cost catalog and no `CharacterSkill`/`CriticalSuccessEvidence`/`AdvancementRecommendation` domain types existed prior to this task -- confirmed by `Grep`.
- ADR-002 §20's generic `PendingInteraction` machinery is not routed through by any existing Character command (confirmed -- `SqliteSavingPipeline`'s own doc comment already explains why direct repository calls don't use `CommandContracts.CommandResult`); this task represents "Pending" as an ordinary successful `Result<AdvancementRecommendationRecord>` carrying a `Pending`-status record, consistent with that established precedent rather than wiring in the separate command-dispatch layer.
- `ADR-024` §6.1 explicitly does not decide which of its two approved branches (`Spend` vs. `ReleaseReservation`-with-level-still-applied) a real `SkillAdvancementRule` would choose -- `ResolveAdvancementRecommendation`'s own `spendReservedPoints` parameter is this task's explicit stand-in for that not-yet-implemented Rules Engine decision, so both ADR-named outcomes are implemented correctly rather than guessed.

Assumptions: none.

## 4. Proposed approach

- Domain (`SkillEconomy.cs`, new): `SkillDefinitionId` (mirrors `AttributeDefinitionId`), `CharacterSkill` (mirrors `AttributeValue`), `AdvancementRecommendationStatus`.
- Rules (`SkillCostRules.cs`, new): explicitly-flagged test-fixture cost/ordinary-purchase ceiling, mirroring `AttributeCostRules.cs` exactly.
- Application: `ICharacterRepository.PurchaseSkillLevel`/`RecordCriticalSuccessEvidence`/`GetCriticalSuccessEvidence`/`RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation`/`GetAdvancementRecommendation`; `CriticalSuccessEvidenceRecord`/`AdvancementRecommendationRecord`; `CharacterRecord.Skills`.
- Persistence: `MutateMechanics`'s callback signature extended to receive the connection/transaction; two new tables (`CriticalSuccessEvidence`, `AdvancementRecommendation`), a new `SkillsJson` column on `Character`. `PurchaseSkillLevel` mirrors `PurchaseAttributeIncrease` exactly. `RequestSkillAdvancedRecommendation` mints the recommendation, inserts it inside the same `MutateMechanics` transaction that moves `Available`->`Reserved`, and (since `MutateMechanics` only returns `Result<CharacterRecord>`) captures the created record via an outer-scope variable rather than a second DB round-trip; a duplicate `CommandId` (which skips the callback entirely via `MutateMechanics`'s own replay path) falls back to a plain lookup by `CommandId` on the `AdvancementRecommendation` table. `ResolveAdvancementRecommendation` reads the recommendation and every referenced evidence row inside the same transaction, validates all evidence is unused before marking any of it used (all-or-nothing), and implements exactly the three ADR-024 §6.1 outcomes (dismiss/approve-with-spend/approve-without-spend).
- Tests: skill-created-from-first-purchase, sufficient/insufficient balance, above-ceiling rejection, reservation exact-amount, both resolve outcomes (spend and dismiss), single-use evidence (checked against real `UsedByAdvancementId` state, not just rejection), duplicate-`CommandId` for all three commands (checked against real `Available`/`Reserved`/`Spent`), Mechanics/Identity no-false-conflict.

No Unity/UI code, no `ADR-022`/`024` content change, no concrete skill-cost catalog.

## 5. Milestones

### M1 — Domain/Rules/Application extension

- [x] `SkillEconomy.cs` (Domain): `SkillDefinitionId`/`CharacterSkill`/`AdvancementRecommendationStatus`.
- [x] `SkillCostRules.cs` (Rules): explicitly-flagged test fixture.
- [x] `ICharacterRepository` extended with six methods; `CriticalSuccessEvidenceRecord`/`AdvancementRecommendationRecord`; `CharacterRecord.Skills`.
- [x] `PersistenceFailures`/`ErrorCodes` additions (six new entries).

### M2 — Persistence and tests

- [x] `MutateMechanics`/`MechanicsMutation` extended (connection/transaction access, `NewSkills`); two existing call sites (`GrantDevelopmentPoints`/`PurchaseAttributeIncrease`) updated for the new signature.
- [x] Schema: `SkillsJson` column, new `CriticalSuccessEvidence`/`AdvancementRecommendation` tables.
- [x] Six new methods implemented.
- [x] 12 new tests in `CharacterSkillPurchaseCriticalEvidenceTests.cs`, all passing on first run.
- [x] `dotnet build`/`dotnet test` full suite green, no regression (351 -> 363).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-050`–`058`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [x] Commit, push, and open Draft PR — [#90](https://github.com/odyssey-services/Odyssey_VTT/pull/90).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 -- Preflight confirmed PR #89's merge commit is a real ancestor of `origin/main` at `8af347b`; created branch `feat/ody-s04-106-skill-purchases-critical-evidence`.
- 2026-09-01 -- Read `ADR-024` §6–7.1 in full, `ADR-002` §20, product §14 (and §15 narrowly), backlog §5–7, and `ODY-S04-105`'s own `MutateMechanics`/`AttributeCostRules.cs` in full.
- 2026-09-01 -- Confirmed via search: no skill-cost catalog and no `CharacterSkill`/`CriticalSuccessEvidence`/`AdvancementRecommendation` types existed prior to this task.
- 2026-09-01 -- Decided to extend `MutateMechanics`'s callback signature (connection/transaction access) rather than add a special-cased side-effect slot, after recognizing the recommendation/evidence tables need same-transaction reads/writes the existing callback shape could not provide.
- 2026-09-01 -- Implemented Domain/Rules/Application/Persistence extension; `dotnet build` passed on first attempt after the interface implementation gap was filled.
- 2026-09-01 -- Wrote and ran 12 new tests; all 12 passed on the first run, including the single-use-evidence race scenario and all three duplicate-CommandId cases.
- 2026-09-01 -- Full suite green (363/363, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries and created this task's own contract/ExecPlan proactively before the final validation pass.

## 7. Decisions

- 2026-09-01 -- Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1. Authority: `PLANS.md` §1, backlog row 6.
- 2026-09-01 -- Decision: `SkillCostRules` (`CostPerSkillPoint=3`, `MaxOrdinaryPurchaseLevel=4`) is an explicitly-flagged test fixture, mirroring `AttributeCostRules`'s own exact disclaimer. Authority: this task's own explicit ТЗ instruction; confirmed by search that no skill-cost catalog exists.
- 2026-09-01 -- Decision: extend `MutateMechanics`'s callback to receive the live connection/transaction rather than adding a generic side-effect delegate to `MechanicsMutation`. Authority: this task's own code-quality judgment -- the callback itself performing the extra table reads/writes inline is simpler and more direct than a second abstraction layer, and keeps the helper's own shape general for `ODY-S04-107`.
- 2026-09-01 -- Decision: "Pending" (ADR-002 §20.1) is represented as an ordinary successful `Result<AdvancementRecommendationRecord>` carrying the created `Pending`-status record, not `Odyssey.Application.Commands.CommandResult.Pending`. Authority: no existing Character command routes through that not-yet-wired-in command-dispatch layer; `SqliteSavingPipeline`'s own doc comment gives the identical reasoning for its own design.
- 2026-09-01 -- Decision: `ResolveAdvancementRecommendation`'s `spendReservedPoints` parameter is this task's own explicit stand-in for `ADR-024` §6.1's own undecided `SkillAdvancementRule` computation -- the method correctly implements both ADR-named outcomes rather than guessing which a real rule would choose. Authority: `ADR-024` §6.1's own explicit "not decided numerically by this ADR" text; this task's own explicit instruction not to invent a Rules Engine.
- 2026-09-01 -- Decision: evidence single-use is enforced by reading each referenced evidence row fresh inside the same transaction (validating `UsedByAdvancementId == null` for all referenced evidence before marking any of it used) plus a defensive `WHERE Revision = $expectedRevision` on the marking `UPDATE` itself -- no separate caller-supplied "expected evidence revision" parameter, since SQLite serializes writers and the read+write share one transaction. Authority: `ADR-024` §7.1's own "guarded by the row's own revision" requirement, satisfied without adding API surface the ADR does not require.

## 8. Discoveries and deviations

- No open architectural question was found that `ADR-024`/`ADR-002` do not already answer. The two points this task's own ТЗ explicitly flagged as open engineering decisions (skill-cost fixture; how `Reserved` is protected from a race between two concurrent `RequestSkillAdvancedRecommendation` calls) were both resolved within the existing substrate (an explicitly-flagged fixture; SQLite's own writer serialization inside `MutateMechanics`'s existing `MechanicsRevision` gate), not architectural gaps.
- `MutateMechanics`'s signature change (adding connection/transaction to its callback) is a deliberate, backward-compatible extension -- both pre-existing call sites (`GrantDevelopmentPoints`/`PurchaseAttributeIncrease`) were updated mechanically, with no behavior change to either.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 363/363 (12 new tests), zero regression.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: to be confirmed in the final validation pass (registry entries and task contract prepared proactively).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- the new `Character.SkillsJson` column is additive; the new `CriticalSuccessEvidence`/`AdvancementRecommendation` tables are new and unused by any other code path if reverted; no existing column altered.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S04-107` (`RevertAdvancementPurchase`/`CharacterRespec`), expected to reuse `MutateMechanics` for its own compensating-transaction work.
