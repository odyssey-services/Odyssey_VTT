# ExecPlan — ODY-S05-604 Intervention and Atomic Attack Apply

## 1. Purpose
Build ADR-029 section 6 stages 12-13 (intervention, atomic apply) on top of the unmodified ODY-S05-603 evaluation.

## 2. Scope
Domain enums, a new standalone Application/Persistence contract and its SQLite implementation, and tests; no Ruleset formula, no concrete Character/Item/ActiveEffect state delta, no compensation/audience projection.

## 3. Non-goals
No item/ammo/armor/character-resource mutation (no accepted Ruleset formula exists to interpret one); no `ActiveEffect` creation; no compensation; no audience-filtered Game Log rendering; no method added to any existing repository interface.

## 4. Architecture
`AttackApplyService.ResolveAttack` checks `IAttackApplyRepository.GetOutcome` first (idempotency, before any RNG draw), then calls the unmodified `AttackEvaluationService.EvaluateAttack` (revalidates guards, rolls), then records `Pending` or `Accepted` via `IAttackApplyRepository.RecordAttackOutcome` in one `SqliteSavingPipeline` transaction. `AttackApplyService.ResolveAttackIntervention` is an independent root command delegating straight to `IAttackApplyRepository.ResolveAttackIntervention`, which reuses the pending row's own saved random sample and never touches the RNG factory.

## 5. Milestones
M1 read ADR-029 §6/§9/§10, ADR-008 §13-15, ADR-012 §5, and `SqliteSavingPipeline`/`SqliteActiveEffectRepository.ExpireActiveEffect`/`SqliteGameLogRepository` as precedents; M2 Domain enums; M3 Application contract + orchestration; M4 `SqliteAttackApplyRepository`; M5 real tests + direct-consequence scope-guard updates; M6 validation/PR.

## 6. State and data flow
`ResolveAttack` request -> `GetOutcome` (idempotency short-circuit) -> (if none) `EvaluateAttack` (guards + roll) -> `RecordAttackOutcome` (Pending or Accepted, one transaction, Game Log entry only when Accepted). `ResolveAttackIntervention` request -> re-read pending row by `pendingCommandId` -> CAS guard (`OutcomeKind == Pending`) -> MainGM gate -> transition to Accepted/Rejected/Cancelled, one transaction, Game Log entry only when the transition is to Accepted, reusing the stored `RandomSampleValue`.

## 7. Error handling
Typed `Result` failures throughout; a guard failure inside `EvaluateAttack` returns before any RNG draw or repository write (no partial commit possible, because no write is attempted); a CAS conflict in `ResolveAttackIntervention` is a typed conflict, not a silent no-op.

## 8. Test strategy
Real temporary-SQLite fixtures (`AttackApplyTests.cs`), a fixture `IAttackRulesEvaluator` toggling `RequiresIntervention`, and a counting wrapper around `DeterministicRandomStreamFactory` to prove the RNG factory is queried at most once per logical roll, never again on retry or intervention resolution. Whole-database row-count checks prove atomicity and the absence of partial commits.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` full solution green; `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` green; `git diff --name-status` against `main` confined to the ТЗ's allowed paths.

## 10. Recovery and rollback
No persisted task-owned data beyond the new `AttackOutcome` table and the Game Log rows it writes; revert source and drop the table to roll back.
