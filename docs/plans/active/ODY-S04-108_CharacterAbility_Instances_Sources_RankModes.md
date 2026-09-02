# ODY-S04-108 — CharacterAbility Instances, Sources & Rank Modes

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-108-character-ability-instances`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Section 1 (mandatory retroactive defense, before any new functionality): adding `AdvancementOperationKind.AbilityAcquisition` — a third value on an enum ODY-S04-107's already-merged `RevertAdvancementPurchase`/`ApplyCharacterRespec` branch on with an exhaustive-looking `if/else` — would silently mis-parse a real ability's `TargetDefinitionId` as a `SkillDefinitionId` and return the wrong, misleading `CharacterAdvancementPurchaseHasDependent` error. Both branches (plus the shared `ComputeRespecPlan` helper) are made exhaustive first, rejecting the unsupported kind explicitly, before `AcquireAbility(ProgressionPurchase)` starts producing real `AbilityAcquisition` purchases.

Then: implement product section 16's `AbilityDefinition`/`CharacterAbility` split, `AcquireAbility` for all six `SourceKind` values, `RemoveAbility` (legality gated by `SourceKind`), and `RankMode` (`None`/`Numeric`/`Named`) validated independently per mode. Eighth implementation task of `SLICE-04`, and the first to actually use the `CharacterAbilities` section and the first cross-section (`Mechanics` + `CharacterAbilities`) command in the slice.

## 2. Task contract

- Goal: fix ODY-S04-107's two-branch `AdvancementOperationKind` exhaustiveness gap first; then implement `CharacterAbility`/`AcquireAbility`/`RemoveAbility`/`RankMode`, with `AcquireAbility(SourceKind=ProgressionPurchase)` as a genuine two-section (`Mechanics`+`CharacterAbilities`) transaction and every other path a single-section `CharacterAbilities` mutation via a new `MutateAbilities` helper that actually increments `CharacterAbilitiesRevision`.
- Acceptance criteria: `RevertAdvancementPurchase`/`ApplyCharacterRespec`/`ComputeRespecPlan` reject `AbilityAcquisition` explicitly (not the misleading dependent-purchase error), verified by a real regression test; `AcquireAbility(ProgressionPurchase)` spends the pool, creates an `AdvancementPurchase` (`OperationKind=AbilityAcquisition`), and creates the ability atomically, checking both section revisions independently; `AcquireAbility(GMGrant)` is MainGM-only, touches only `CharacterAbilities`, creates no `AdvancementPurchase`; `RemoveAbility` is legal only for `Item`/`ActiveEffect`; `CharacterAbilitiesRevision` genuinely increments on every `AcquireAbility`/`RemoveAbility` call (direct value check); `RankMode` validated independently per mode; every command is `CommandId`-idempotent, verified against real state; a concurrent `CharacterAbilities` edit and `Mechanics` edit commit without a false conflict; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`024` content change.
- Requirement IDs: `ODY-S04-108`, product section 16, `ADR-022` §5–6, `ADR-024` §5.1/§9.
- In scope: `AbilityDefinitionId`/`CharacterAbilityId` (Domain Identity), `CharacterAbility`/`SourceKind`/`RankMode` (Domain), `AbilityCostRules` (Rules, explicitly-flagged test fixture), `ICharacterRepository` extension (Application), `SqliteCharacterRepository`/schema extension (Persistence: `AbilitiesJson` column, `MutateAbilities` helper, cross-section `AcquireAbilityViaProgressionPurchase`), the retroactive `RevertAdvancementPurchase`/`ApplyCharacterRespec`/`ComputeRespecPlan` exhaustiveness fix, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: automatic ability creation/removal from equip/unequip or effect on/off (no Item/Inventory/ActiveEffect system exists); real revert/respec for `AbilityAcquisition` purchases (only the defensive rejection); reusing `BindDraftToCampaign` for template ability copy; `CharacterResource`/`AnatomyProfile` (`ODY-S04-109`); archive/delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`); concrete ability catalog/costs; any Unity/UI code; any `ADR-022`/`024`/backlog content change beyond the status row.
- Required authorities: product section 16 (full read), `ADR-022` §5–6 (`CharacterAbilities` section/lock key), `ADR-024` §5.1/§9 (`AcquireAbility` purchase pipeline, module boundaries), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`107`'s own code (`MutateOwnership`/`MutateMechanics`/`ApplyCharacterRespec`, `AdvancementPurchase.cs`, the two `AdvancementOperationKind` branches) as binding convention.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `19d05e8` (PR #91, `ODY-S04-107`'s merge commit), independently verified via `git merge-base --is-ancestor`.
- `Character` table already has real `AttributeValuesRevision`/`CharacterSkillsRevision`/`CharacterAbilitiesRevision` columns (`ADR-022` §5's twelve section revisions, present from `ODY-S04-101` onward) but `ODY-S04-105`/`106` never incremented any of the first two — both route entirely through `MechanicsRevision` instead, a deliberate choice `ADR-024` §4.2 justifies specifically for pool ledger data. No such justification exists for `CharacterAbilities` — confirmed by reading `ADR-024` §4.1/4.2 in full; abilities are not ledger data.
- `RevertAdvancementPurchase`/`ApplyCharacterRespec` (both in `SqliteCharacterRepository.cs`, merged in `ODY-S04-107`) each branch on `AdvancementPurchase.OperationKind`/`CharacterRespecTarget.OperationKind`/`CharacterRespecPlanEntry.OperationKind` with a plain two-way `if (... == AttributeIncrease) {...} else {...as Skill...}` — confirmed by direct read of all five call sites (`RevertAdvancementPurchase` once, `ComputeRespecPlan` twice, `ApplyCharacterRespec` twice).
- No Item/Inventory/ActiveEffect/template-copy system exists anywhere in this codebase — confirmed by `Grep` — so `AcquireAbility`'s four non-`ProgressionPurchase`/`GMGrant` `SourceKind` values have no real caller yet.

Assumptions: none.

## 4. Proposed approach

- Section 1 fix (before any new code): `AdvancementOperationKind` gains `AbilityAcquisition = 3`. `RevertAdvancementPurchase`'s branch becomes `if/else if/else`, returning a new `CharacterAdvancementOperationKindNotSupported` error for the `else`. `ComputeRespecPlan` is changed from returning a bare `CharacterRespecPreview` to `Result<CharacterRespecPreview>`, with the same explicit rejection for an unsupported target `OperationKind` (both of its own two internal branches guarded by the same check at the top of its per-target loop) — `PreviewCharacterRespec`/`ApplyCharacterRespec` both adapt to the new `Result<...>` return type. `ApplyCharacterRespec`'s own inner loop additionally gets a defense-in-depth guard (structurally unreachable once `ComputeRespecPlan` rejects upstream, but named explicitly by this task's own ТЗ).
- Domain: `AbilityDefinitionId` (catalog key, mirrors `AttributeDefinitionId`/`SkillDefinitionId`, `Character/Ability.cs`); `CharacterAbilityId` (canonical `charab_` + 32-hex instance id, `Identity/DomainIdentity.cs`); `SourceKind`/`RankMode` enums (product section 16, values reused verbatim); `CharacterAbility` (constructor validates `RankMode`'s three independent shapes).
- Rules: `AbilityCostRules` (flat `CostPerAbility` fixture — an ability is owned or not, no per-point formula like attributes/skills).
- Application: `ICharacterRepository.AcquireAbility`/`RemoveAbility`; `CharacterRecord` gains `Abilities` (mirrors `Attributes`/`Skills`'s own direct-embedding convention, no separate getter).
- Persistence: new `AbilitiesJson` column on `Character` (existing `CharacterAbilitiesRevision` column, never previously written, is now genuinely used); `MutateAbilities` (mirrors `MutateMechanics`'s exact gate/load/callback/commit shape for the single `CharacterAbilities` section, the same way `MutateOwnership` already does for `Ownership`) backs `RemoveAbility` and every `AcquireAbility` `SourceKind` except `ProgressionPurchase`. `AcquireAbility(ProgressionPurchase)` gets its own dedicated `AcquireAbilityViaProgressionPurchase` method with its own `_pipeline.Execute` call — the same "one genuinely cross-cutting case gets its own method" precedent `ApplyCharacterRespec` (`ODY-S04-107`) already established, since neither `MutateMechanics` nor `MutateAbilities` alone can check/commit two independent section revisions in one transaction.
- Permission model for `AcquireAbility`'s four "no real caller yet" `SourceKind` values (`CharacterTemplate`/`Item`/`ActiveEffect`/`RulesetAdvancement`): gated identically to `GMGrant` (MainGM-only) — the smallest, safest default a future Item/ActiveEffect/template-copy system can revisit explicitly when it is actually built.
- Tests: the section-1 regression (both commands reject `AbilityAcquisition` explicitly, not as a misleading dependent-purchase); `AcquireAbility(ProgressionPurchase)` balance/creation/`AdvancementPurchase`/duplicate-`CommandId`; `AcquireAbility(GMGrant)` permission/no-pool-change/no-purchase; `CharacterAbilitiesRevision` genuinely increments; all four `RankMode` validation failure shapes plus two success shapes; `RemoveAbility` legality by `SourceKind` (including not-found and duplicate-`CommandId`); a concurrent `CharacterAbilities`+`Mechanics` edit no-false-conflict test.

No Unity/UI code, no `ADR-022`/`024` content change, no concrete ability catalog, no Item/Inventory/ActiveEffect automatic-acquisition implementation.

## 5. Milestones

### M1 — Section 1 exhaustiveness fix

- [x] `AdvancementOperationKind.AbilityAcquisition` added with a doc comment explaining why it is deliberately unsupported by revert/respec.
- [x] `RevertAdvancementPurchase`'s branch made exhaustive; new `CharacterAdvancementOperationKindNotSupported` error code/`PersistenceFailures` entry.
- [x] `ComputeRespecPlan` changed to `Result<CharacterRespecPreview>`, both internal branches guarded; `PreviewCharacterRespec`/`ApplyCharacterRespec` adapted; `ApplyCharacterRespec`'s own loop given a defense-in-depth guard too.
- [x] Full pre-existing suite re-run green with zero assertion changes (145/145 persistence tests) before any new Domain/Application/Persistence code was written.

### M2 — CharacterAbility/AcquireAbility/RemoveAbility

- [x] `AbilityDefinitionId`/`CharacterAbilityId` (Domain Identity); `CharacterAbility`/`SourceKind`/`RankMode` (Domain); `AbilityCostRules` (Rules).
- [x] `CharacterRecord.Abilities`; new `AbilitiesJson` column; `ICharacterRepository.AcquireAbility`/`RemoveAbility`.
- [x] `MutateAbilities` helper; `AcquireAbilityViaProgressionPurchase` cross-section method; `RemoveAbility`.
- [x] `PersistenceFailures`/`ErrorCodes` additions (four new entries).
- [x] 24 new tests in `CharacterAbilityInstancesTests.cs` (22 methods, one parameterized ×4), all passing on first/second run (one `Assert.Throws` overload-ambiguity fix, no logic change).
- [x] `dotnet build`/`dotnet test` full suite green (168 persistence tests total, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1` (one CRLF-corruption self-fix after a Python-based mechanical edit, same class of issue as `ODY-S04-106`'s own earlier incident — caught and fixed before this milestone closed).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-072`–`092`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check against §9's own expectations.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-02 -- Preflight confirmed PR #91's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-108-character-ability-instances`.
- 2026-09-02 -- Read product section 16, `ADR-022` §5–6, `ADR-024` §5.1/§9 in full; re-read `MutateOwnership`/`MutateMechanics`/`ApplyCharacterRespec`/`AdvancementPurchase.cs` and both `AdvancementOperationKind` branch sites.
- 2026-09-02 -- Fixed both branches (`RevertAdvancementPurchase`, `ComputeRespecPlan`+`ApplyCharacterRespec`) to be exhaustive before writing any ability code; full suite re-run green (145/145) confirming zero regression from the fix alone.
- 2026-09-02 -- Implemented `AbilityDefinitionId`/`CharacterAbilityId`/`CharacterAbility`/`SourceKind`/`RankMode`/`AbilityCostRules`; extended `CharacterRecord`/`ICharacterRepository`; `dotnet build` passed on first attempt.
- 2026-09-02 -- Implemented `MutateAbilities`/`AcquireAbilityViaProgressionPurchase`/`AcquireAbility`/`RemoveAbility`; `dotnet build` passed on first attempt.
- 2026-09-02 -- Wrote 24 new tests; one compile error (`Assert.Throws` overload ambiguity on a bare lambda) fixed by assigning to a named `Action` local first, matching this codebase's own existing convention; all 24 passed after the fix.
- 2026-09-02 -- A Python-based mechanical edit (fixing the `Assert.Throws` ambiguity) converted the new test file's line endings to CRLF -- caught by `verify-format.ps1`, fixed with the same binary-mode LF-normalization approach used for `ODY-S04-106`'s own earlier identical incident, re-verified via `file` and a full test re-run.
- 2026-09-02 -- Full suite green (168/168 persistence tests, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green.

## 7. Decisions

- 2026-09-02 -- Decision: fix the `AdvancementOperationKind` exhaustiveness gap in already-merged `ODY-S04-107` code BEFORE writing any new ability functionality, per this task's own explicit §1.3 instruction and ordering. Authority: this task's own ТЗ.
- 2026-09-02 -- Decision: `ComputeRespecPlan` returns `Result<CharacterRespecPreview>` instead of a bare value, so an unsupported `OperationKind` produces a proper `Result.Failure` instead of an unhandled exception. Authority: this codebase's own established convention of using `Result<T>` for expected-but-invalid input, not exceptions, for every other rejection path in this class.
- 2026-09-02 -- Decision: `AcquireAbility(SourceKind=ProgressionPurchase)` gets its own dedicated cross-section method (`AcquireAbilityViaProgressionPurchase`) with its own `_pipeline.Execute` call, rather than extending `MutateMechanics` or `MutateAbilities` to handle two sections generically. Authority: `ApplyCharacterRespec`'s own precedent (`ODY-S04-107`) for "one genuinely cross-cutting case gets its own method"; neither existing single-section helper's contract can express two independently-gated section revisions in one call without a larger, riskier generalization.
- 2026-09-02 -- Decision: `CharacterAbilitiesRevision` is genuinely incremented by `MutateAbilities`/`AcquireAbilityViaProgressionPurchase` on every call -- unlike `ODY-S04-105`/`106`'s own choice to route `AttributeValuesRevision`/`CharacterSkillsRevision` through `MechanicsRevision` instead. Authority: `ADR-024` §4.1/4.2's own justification for that choice is specific to pool ledger data and does not extend to abilities; `ADR-022` §5 reserves `CharacterAbilitiesRevision` as its own section revision with no such carve-out.
- 2026-09-02 -- Decision: `AcquireAbility`'s four `SourceKind` values with no real caller yet (`CharacterTemplate`/`Item`/`ActiveEffect`/`RulesetAdvancement`) are gated identically to `GMGrant` (MainGM-only). Authority: this task's own engineering judgment -- product section 16 only names `GMGrant` MainGM-only explicitly, but no Item/Inventory/ActiveEffect/template-copy system exists anywhere in this codebase to call this command on a player's behalf (confirmed by search), so the safest default is chosen rather than an ungated permission surface with no real caller to validate it against.
- 2026-09-02 -- Decision: `AbilityCostRules` is a flat, explicitly-flagged test fixture (`CostPerAbility`, not a per-point formula) -- an ability is owned or not owned, with no intermediate level in this task's own scope. Authority: product section 16 names no per-ability numeric cost or rank-based cost curve; confirmed by search that no ability-cost catalog exists anywhere in this codebase.

## 8. Discoveries and deviations

- No open architectural question was found that `ADR-022`/`ADR-024` do not already answer.
- A Python-based mechanical text edit converted a new file's line endings from LF to CRLF, caught by `verify-format.ps1` -- the same class of incident this session already hit once in `ODY-S04-106`; fixed the same way (binary-mode read/replace `\r\n`->`\n`/write), and every other file touched this task was independently re-verified via `file` to confirm none of the others were affected.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 168/168 `Odyssey.Tests.Persistence` (24 new across 22 methods), zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- the new `AbilitiesJson` column is additive with a default (`'[]'`); `CharacterAbilitiesRevision` was already a real column, now genuinely written but never previously relied upon by any other code path; the `AdvancementOperationKind.AbilityAcquisition` enum value and the exhaustiveness fix are additive/defensive and do not change either existing branch's own `AttributeIncrease`/`SkillLevelPurchase` behavior.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. `ODY-S04-109` (`CharacterResource`/`AnatomyProfile`) is the next task in the backlog; a future Item/Inventory/ActiveEffect task would be the first real caller of `AcquireAbility`'s `Item`/`ActiveEffect` `SourceKind` values and should revisit this task's own MainGM-only default for them.
