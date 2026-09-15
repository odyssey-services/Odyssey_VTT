# ExecPlan — ODY-S05-610 Combat Stacking Conflict GM Resolution

## 1. Purpose
Give `ODY-S05-503`'s own `ActiveEffectStackConflict` (`ADR-028` §7 rule 7's own `RequestGMResolution` pending record) durable, cross-session persistence, and a real MainGM resolution command -- closing `606`'s own disclosed gap where a combat-sourced `RequestGMResolution` collision created no row and reached no GM.

## 2. Scope
A new `CombatStackConflict` table inside `SqliteAttackApplyRepository` (not a separate repository -- the gap is combat-specific, `ResolveStacking` has exactly one call site); persistence of the pending conflict inside `606`'s own atomic-apply transaction; a new root command, `ResolveStackConflict`, reusing the existing `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` and a shared, extracted `ApplyStackDecisionMutation` helper; tests.

## 3. Non-goals
No change to `ActiveEffectStackingRules`'s own two pure functions (`ResolveStacking`/`ResolveActiveEffectStackConflict`, reused exactly as-is); no new Domain-level identity type (keyed by the existing `CommandId`/`ActiveEffectId` pair instead); no method added to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`; no `605`/`607`/`609` territory touched; no Game Log entry for the resolution command.

## 4. Architecture
Application: new `CombatStackConflictRecord` type and `IAttackApplyRepository.ResolveStackConflict` contract in `AttackApplyRepositoryContracts.cs`; `AttackApplyService.ResolveStackConflict` delegates unchanged. Persistence: `SqliteAttackApplyRepository` gains the `CombatStackConflict` table (embedding the candidate `ActiveEffect` via `SqliteActiveEffectRepository`'s existing `AddParameters`/`SelectColumns`/`ReadRecord`, reused verbatim, plus conflict-specific columns); `ApplyEffectCandidates`'s own `RequestGmResolution` branch now calls `RecordStackConflict` instead of a bare `break`; the switch's other five cases are extracted into a shared `ApplyStackDecisionMutation` helper called by both `ApplyEffectCandidates` and the new `ResolveStackConflict`; `ResolveStackConflict` mirrors `ResolveAttackIntervention`'s own shape exactly (MainGM-gate first, CAS-guarded conditional UPDATE, `SqliteSavingPipeline`-driven idempotency, own separate transaction).

## 5. Milestones
M1 read `ActiveEffectStackingRules.cs`/`ApplyEffectCandidates`/`ResolveAttackIntervention` verbatim, confirm `ResolveStacking`'s single call site and `SqliteActiveEffectRepository`'s reusable internal helpers; M2 add `CombatStackConflictRecord`/`IAttackApplyRepository.ResolveStackConflict`/`AttackApplyService` delegation/error codes; M3 add the `CombatStackConflict` table and `RecordStackConflict`, wire it into `ApplyEffectCandidates`'s `RequestGmResolution` branch; M4 extract `ApplyStackDecisionMutation`, implement `ResolveStackConflict`/`ReadStackConflict`/`ReplayStackConflictResolution`; M5 real tests (`TC-ATTACK-084`-`092`); M6 validation/PR.

## 6. State and data flow
`606`'s own `ApplyEffectCandidates` calls `ActiveEffectStackingRules.ResolveStacking` as before (unmodified) -> when the decision is `RequestGmResolution`, `RecordStackConflict` writes a durable pending row (embedding the candidate `ActiveEffect`) plus a `combat_stack_conflict_raised` DomainEvent, inside the SAME transaction as the rest of that attack's own commit -> later, a MainGM calls `ResolveStackConflict` with the raising `CommandId`/conflicting `ActiveEffectId`/chosen `ActiveEffectStackConflictResolution` -> a NEW transaction reads the pending row, CAS-guards it, calls the existing `ActiveEffectStackingRules.ResolveActiveEffectStackConflict`, applies the resulting decision via the shared `ApplyStackDecisionMutation`, and marks the pending row `Resolved` (never deleted).

## 7. Error handling
MainGM-gate failure, pending-record-not-found, and already-resolved each return a distinct error before any write. A guard failure inside `ResolveStackConflict`'s own apply lambda leaves no partial row (same `SqliteSavingPipeline` machinery as every other command in this class). `ResolveStackConflict` is itself idempotent under the pipeline's own replay-by-`CommandId` mechanism (a retry with the same resolving `CommandId` returns the original resolution, not a second application) -- distinct from, and not weakening, the guard against a SECOND, DIFFERENT resolving command resolving the same already-resolved conflict.

## 8. Test strategy
Real SQLite fixtures extending `606`'s own `AttackEffectApplicationTests.cs` pattern (same `SetUp`/`Active`/`ItemFor`/`CreateEncounter`/`Request`/`Command`/`User`/`Candidate`/`Rules` helpers): a pending record created atomically with the rest of attack apply (direct SQL check); the existing `TC-ATTACK-054` invariant (unresolved conflict doesn't block commit) reconfirmed plus the new persisted-record assertion; each of the three resolution outcomes (`ApplyAsIndependentInstance`/`Replace`/`Ignore`) producing exactly the expected `ActiveEffect` mutation; non-MainGM denial with zero mutation; CAS failure on a second resolution attempt; idempotent replay on a same-`CommandId` retry; an architecture guard (source-text scan, comments stripped) proving no reference to the public `CreateActiveEffect` in `ResolveStackConflict`'s own method body.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` full solution green (1001 tests: 992 pre-existing + 9 new); `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` green; `git diff --name-status` against `main` confined to the ТЗ's allowed paths, no `602`-`609` (beyond the one `ApplyEffectCandidates` branch)/`610`-adjacent territory or ADR documents touched.

## 10. Recovery and rollback
One new standalone table (`CombatStackConflict`); no change to `ActiveEffect`/`AttackOutcome`'s own existing schema. `IAttackApplyRepository` gains one new method (backward compatible); `SqliteAttackApplyRepository`'s constructor is unchanged. Reverting this task's source and table leaves `602`-`609` and all prior campaign state unaffected -- an unresolved conflict returns to the pre-`610` silent-no-durable-record behavior, not a data-loss condition.
