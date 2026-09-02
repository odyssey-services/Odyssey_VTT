# ODY-S04-107 — Advancement Revert & CharacterRespec

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-107-advancement-revert-respec`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Пункт 0 (retroactive gap fix): `ADR-024` §3.3/§5.1 step 4 requires every successful `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/approved `ResolveAdvancementRecommendation` to co-commit an `AdvancementPurchase` record; this was never implemented in `ODY-S04-105`/`106`'s already-merged code (confirmed by `Grep`). Separately, `ADR-012` §6's compensating-event mechanism (`OriginalEventId`/`CompensationGroupId`/`IsCompensating`) has no real `DomainEvents` schema columns and has never been used anywhere in the codebase. Both gaps had to be closed before any new functionality could be built on top of them.

Goal (after the gap fix): implement `ADR-024` §6.2/§7.2 — `RevertAdvancementPurchase` as a compensating command (`ADR-012` §6) with a minimal, explicitly-bounded dependency check; `PreviewCharacterRespec` (read-only Query) and `ApplyCharacterRespec`, producing an ordered batch of compensating and forward events grouped by one trailing `CharacterRespecCompleted` event. Seventh implementation task of `SLICE-04`.

## 2. Task contract

- Goal: close the ODY-S04-105/106 `AdvancementPurchase` gap without reopening their purchase business logic, extend `DomainEvents` with ADR-012 §6's compensating-event columns, then implement `RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec` on top of that substrate.
- Acceptance criteria: all pre-existing ODY-S04-105/106 tests pass with their own assertions unmodified; every purchase path (ordinary attribute, ordinary skill, approved recommendation) co-commits a correctly-populated `AdvancementPurchase`; `RevertAdvancementPurchase` returns a purchase's addressed entry to `FromValue`, refunds `Cost`, leaves the original event un-mutated, and rejects a purchase with a later dependent purchase or a missing `ReasonCode`; `PreviewCharacterRespec` produces no event/state change; `ApplyCharacterRespec` recomputes its plan server-side from scratch (never trusting a client value), commits one compensating/forward event per undone/new purchase (individually visible, never collapsed) plus exactly one trailing `CharacterRespecCompleted`; every command is `CommandId`-idempotent, verified against real balances/status/event-counts; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-012`/`ADR-024` content change.
- Requirement IDs: `ODY-S04-107`, `ADR-024` §3.3/§6.2/§7.2, `ADR-012` §6.
- In scope: `DomainEvents` schema extension (Persistence), `AdvancementPurchaseId` (Domain Identity), `AdvancementPurchase`/`AdvancementOperationKind`/`AdvancementPurchaseStatus` (Domain), `ICharacterRepository` extension + `CharacterRespecTarget`/`CharacterRespecPlanEntry`/`CharacterRespecPreview` (Application), `SqliteCharacterRepository`/`SqliteSavingPipeline` extension (Persistence), retroactive edits to `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`ResolveAdvancementRecommendation`, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: ability/resource/anatomy (`ODY-S04-108`/`109`), archive/delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`, though Ruleset migration is expected to reuse this same compensating-batch pattern), the concrete dependency graph for revert-checking (Rules Engine content — only a minimal, explicitly-flagged check), any Unity/UI code, any `ADR-012`/`024`/backlog content change beyond the status row.
- Required authorities: `ADR-024` §3.3/§6.2/§7.2 (full read), `ADR-012` §6 (full read), `ADR-002` §21.2 (compensation metadata), product §13.2 (`AdvancementPurchase` schema)/§13.5 (`CharacterRespec`'s 8 steps), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`106`'s own code as binding convention (`MutateMechanics`, `DevelopmentTransactionKind`, `SqliteSavingPipeline`).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded and PR #90 (`ODY-S04-106`) independently confirmed a real ancestor of `origin/main` via `git merge-base --is-ancestor` before branching.
- `Grep` for `AdvancementPurchase` across `CharacterRepositoryContracts.cs`/`SqliteCharacterRepository.cs` returned nothing prior to this task — the gap is real, not a documentation lag.
- The real `DomainEvents` schema had no `OriginalEventId`/`CompensationGroupId`/`IsCompensating` columns; `ADR-012` §6's compensating mechanism has never been used anywhere in the codebase.
- `DevelopmentTransactionKind` already reserves `Refund=5`/`RespecReturn=7`/`RespecSpend=8` specifically for this task (per that enum's own doc comment) — reused, not redefined.
- Cross-checking `ODY-S04-105`/`106`'s own precedent for side-table read-model layering found it inconsistent: `DevelopmentTransaction` (105) has both a Domain class and an Application `DevelopmentTransactionRecord`; `CriticalSuccessEvidence`/`AdvancementRecommendation` (106) have only Application-layer `*Record` classes, no Domain-layer counterpart. This task's own `AdvancementPurchase` follows a third, simpler shape: a single Domain class, used directly by the Application port (no duplicate wrapper) — see Decisions §7.

Assumptions: none.

## 4. Proposed approach

- Persistence schema: `DomainEvents` gains `OriginalEventId INTEGER`, `CompensationGroupId TEXT`, `IsCompensating INTEGER NOT NULL DEFAULT 0` via the existing `CREATE TABLE IF NOT EXISTS` extension convention (no new migration mechanism; no production data exists yet). `SqliteSavingPipeline.AppendDomainEvent`/`ComputeSha256Hex` changed from `private static` to `internal static` with three new optional trailing parameters, specifically so `ApplyCharacterRespec` (which must commit more than one `DomainEvents` row per call, exceeding `Execute<T>`'s own one-event-per-call contract) can append every non-final batch event through the identical code path, never a duplicated `INSERT`. `PipelineWrite<T>` gained the same three optional properties.
- Domain: `AdvancementPurchaseId` (canonical `advpur_` + 32-hex instance id, `Identity/DomainIdentity.cs`); `AdvancementPurchase`/`AdvancementOperationKind`/`AdvancementPurchaseStatus` (new `Character/AdvancementPurchase.cs`, mirroring `AttributeValue`/`CharacterSkill`'s own constructor-validation style). `Cost` is relaxed to `>= 0` (not `> 0`) because ADR-024 §6.1 branch 3's fully-evidence-funded approval genuinely spends zero development points.
- Application: `ICharacterRepository` gains `GetAdvancementPurchases`/`RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec`, plus `CharacterRespecTarget`/`CharacterRespecPlanAction`/`CharacterRespecPlanEntry`/`CharacterRespecPreview`. The Domain `AdvancementPurchase` class is used directly as the port's own read-model type (see Decisions §7) — no `AdvancementPurchaseRecord` wrapper.
- Persistence — pkt 0 gap fix: a new `AdvancementPurchase` SQLite table; `InsertAdvancementPurchase`/`UpdateAdvancementPurchaseStatus`/`SelectAdvancementPurchaseForUpdate`/`SelectAdvancementPurchasesForCharacter` helpers, mirroring `AdvancementRecommendation`'s own helper shape. `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`ResolveAdvancementRecommendation`'s approve branch each mint an `AdvancementPurchase` and embed `purchaseId` in their own existing event payload, with zero change to their existing cost/cap/balance/revision logic.
- Persistence — Block Б: `MechanicsMutation` gains three optional compensating-metadata fields, threaded through `MutateMechanics`'s own `PipelineWrite<CharacterRecord>` construction — `RevertAdvancementPurchase` reuses `MutateMechanics` directly (one compensating event per call fits its existing one-event contract). `FindOriginatingEventSequence` locates a purchase's own original forward event by scanning `DomainEvents` for the matching `EventType` + payload `purchaseId` (mirrors `GetCharacterHistory`'s own "no dedicated AggregateId column" convention). `ComputeRespecPlan` is one private static helper shared identically by `PreviewCharacterRespec` (no pipeline, plain read) and `ApplyCharacterRespec` (recomputed fresh inside its own transaction — there is no client-supplied preview parameter on `ApplyCharacterRespec` at all, so nothing to trust or distrust). `ApplyCharacterRespec` has its own dedicated `_pipeline.Execute` call: every undo/repurchase event is appended directly via the now-`internal` `AppendDomainEvent`, sharing one `CompensationGroupId` (the call's own `CommandId`); only the trailing `CharacterRespecCompleted` event goes through the normal single-event `Execute<T>` path.
- Tests: pkt 0 regression (unmodified existing files, confirmed via a full `dotnet test` run before writing any new test) plus new `AdvancementPurchase`-creation coverage for all three purchase paths (including the dismiss branch producing none); revert success/dependent-rejection/reason-required/duplicate-`CommandId`; preview no-op; respec end-to-end with individually-visible batch events, server-side-only recomputation, and duplicate-`CommandId` idempotency.

No Unity/UI code, no `ADR-012`/`024` content change, no concrete requirements-graph/Rules Engine content.

## 5. Milestones

### M1 — Gap fix (pkt 0)

- [x] `DomainEvents` schema extended (`OriginalEventId`/`CompensationGroupId`/`IsCompensating`); `SqliteSavingPipeline` compensating-metadata plumbing (`AppendDomainEvent`/`ComputeSha256Hex` made `internal`, `PipelineWrite<T>` extended) — verified backward-compatible via a full `dotnet build`/`dotnet test` run immediately after.
- [x] `AdvancementPurchaseId` (Domain Identity); `AdvancementPurchase`/`AdvancementOperationKind`/`AdvancementPurchaseStatus` (Domain).
- [x] `ICharacterRepository` extended (`GetAdvancementPurchases`/`RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec`, `CharacterRespecTarget`/`CharacterRespecPlanEntry`/`CharacterRespecPreview`); `PersistenceFailures`/`ErrorCodes` additions (five new entries).
- [x] `AdvancementPurchase` SQLite table + helpers; `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`ResolveAdvancementRecommendation` retrofitted, business logic unchanged.
- [x] Full pre-existing suite re-run green with zero assertion changes (145/145 persistence tests, including the 132 pre-existing ODY-S04-105/106 tests).

### M2 — Revert/Respec (Block Б) and tests

- [x] `MechanicsMutation` extended with compensating-metadata fields; `RevertAdvancementPurchase` (reusing `MutateMechanics`) with its minimal dependency check.
- [x] `ComputeRespecPlan` shared helper; `PreviewCharacterRespec` (read-only); `ApplyCharacterRespec` (dedicated multi-event batch).
- [x] 13 new tests in `CharacterAdvancementRevertRespecTests.cs`, all passing on first run.
- [x] `dotnet build`/`dotnet test` full suite green (145 persistence tests total, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-059`–`071`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check against §9's own expectations.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-02 -- Preflight confirmed PR #90's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-107-advancement-revert-respec`.
- 2026-09-02 -- Read `ADR-024` §3.3/§6.2/§7.2, `ADR-012` §6, `ADR-002` §21.2/21.3, product §13.2/§13.5 in full; confirmed via `Grep` that `AdvancementPurchase` is genuinely absent from all production code.
- 2026-09-02 -- Extended `DomainEvents` schema and `SqliteSavingPipeline`'s compensating-metadata plumbing; verified zero regression via a full `dotnet build`/`dotnet test` run before adding any new Domain type.
- 2026-09-02 -- Added `AdvancementPurchaseId`/`AdvancementPurchase`; resolved the Domain-vs-Application-layering question in favor of using the Domain class directly (see Decisions §7), after finding ODY-S04-105/106's own precedent was itself inconsistent between the two shapes.
- 2026-09-02 -- Extended `ICharacterRepository`/`CampaignRepositoryContracts.cs` (new error codes) and `SqliteCharacterRepository.cs` (table, retrofits, `RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec`); `dotnet build` passed on first attempt.
- 2026-09-02 -- Wrote and ran 13 new tests; all 13 passed on the first run. Full suite green (145/145 persistence tests, no regression against the pre-task baseline).
- 2026-09-02 -- Added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green.

## 7. Decisions

- 2026-09-02 -- Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1.
- 2026-09-02 -- Decision: `AdvancementPurchase` is a single Domain class (`Odyssey.Domain.Character.AdvancementPurchase`), used directly as `ICharacterRepository`'s own return type — no separate Application-layer `AdvancementPurchaseRecord` wrapper. Authority: product §13.2's own schema is one flat record (`OperationKind`/`TargetDefinitionId` discriminator, not two near-duplicate types); the codebase's own precedent for this choice is itself split between two shapes (`DevelopmentTransaction` duplicates into an Application record, `CriticalSuccessEvidence`/`AdvancementRecommendation` do not) — the simpler, more common of the two (no duplicate wrapper, matching `AttributeValue`/`CharacterSkill`/`CharacterOwnership`'s own direct-use convention) was chosen to avoid adding a third inconsistent variant.
- 2026-09-02 -- Decision: `AdvancementPurchase` addresses its target with one generic `TargetDefinitionId` string plus an `AdvancementOperationKind` discriminator (`AttributeIncrease`/`SkillLevelPurchase`), not two near-duplicate purchase-record types or an interface/union. Authority: product §13.2's own literal schema.
- 2026-09-02 -- Decision: `DomainEvents` schema extension follows the existing `CREATE TABLE IF NOT EXISTS` in-place-extension convention (not a new migration mechanism), matching every prior `SLICE-04` task's own schema change. Authority: no production data exists yet; this is the established convention `SqliteCampaignRepository`/`SqliteCharacterRepository` already use for every other schema addition.
- 2026-09-02 -- Decision: `AdvancementPurchase.Cost` is validated `>= 0`, not `> 0`. Authority: `ADR-024` §6.1 branch 3 — an advancement approved without spending `Reserved` points (fully funded by consumed evidence) still produces a purchase record with a genuinely zero cost.
- 2026-09-02 -- Decision: `RevertAdvancementPurchase`'s dependency check is exactly "does the addressed entry's current value still equal this purchase's own `ToValue`" — no cross-entry/prerequisite graph. Authority: `ADR-024` §6.2's own explicit text deferring the exact dependency graph to a future Rules Engine/ruleset ("not an architectural concern"); this is the smallest mechanically-necessary check achievable without one.
- 2026-09-02 -- Decision: `ApplyCharacterRespec`'s batch events are appended directly via `SqliteSavingPipeline.AppendDomainEvent` (made `internal`) rather than reusing `MutateMechanics`, because `MutateMechanics`'s own callback contract commits exactly one event per call and a multi-purchase respec batch must exceed that; only the batch's own trailing `CharacterRespecCompleted` event goes through the normal `Execute<T>` path, so every event (batch or not) is still written by the identical code.
- 2026-09-02 -- Decision: `ApplyCharacterRespec`'s "snapshot before operation" (product §13.5 step 5) is the before/after configuration summary embedded directly in `CharacterRespecCompleted`'s own event payload, not a call to `SqliteBackupRepository`'s full-file campaign backup. Authority: `ADR-024` §7.2 (the ADR directly authoritative for this command) frames the snapshot as event-payload data; `ADR-022` §7 separately prohibits a full-Character-sheet-copy event.
- 2026-09-02 -- Decision: every batch event in `ApplyCharacterRespec` shares one `CompensationGroupId`, set to the call's own `CommandId` (already unique, already the natural idempotency key) — no separate id minted for grouping.

## 8. Discoveries and deviations

- No open architectural question was found that `ADR-012`/`ADR-024` do not already answer — the escape hatch this task's own ТЗ explicitly offered ("if modeling `AdvancementPurchase` without reopening `ADR-024`/`ADR-022` turns out to be impossible, stop") was not triggered; product §13.2's own literal schema resolved the one open modeling question directly.
- ODY-S04-105/106's own precedent for Domain-vs-Application layering of a side-table read-model type was found to be internally inconsistent (see Decisions §7) — this is noted as a pre-existing minor inconsistency in already-merged code, not fixed retroactively (out of scope for this task; flagged here for visibility only).

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 145/145 `Odyssey.Tests.Persistence` (13 new), zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — the new `DomainEvents` columns are additive with a default (`IsCompensating` defaults to 0, the other two are nullable); the new `AdvancementPurchase` table is new and unused by any other code path if reverted; no existing column altered; `SqliteSavingPipeline`'s new parameters are all optional with backward-compatible defaults.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. `ODY-S04-110`–`113` (Ruleset migration) is expected to reuse this task's own compensating-batch pattern (`AppendDomainEvent` called directly for a multi-event batch, grouped by `CompensationGroupId`).
