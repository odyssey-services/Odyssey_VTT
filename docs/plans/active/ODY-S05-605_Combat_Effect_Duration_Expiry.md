# ExecPlan — ODY-S05-605 Combat Effect Duration Expiry

## 1. Purpose
Implement ADR-029 section 7's six turn/round-based EffectDurationType boundary-check mechanisms as pure decision functions, plus a real caller that reuses the existing ExpireActiveEffect.

## 2. Scope
`ActiveEffectExpiryRules.cs` sibling methods, a new `CombatDurationBinding` field on `ActiveEffect`, a new standalone lifecycle-event reader, and an orchestrator; no effect creation, no `EffectApplicationDecision`, no new repository methods.

## 3. Non-goals
No event bus/publisher; no automatic wiring into `CombatEncounterService.Advance` or any other trigger point; no `ADR-028` redesign; no `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`/`IActiveEffectRepository` interface change.

## 4. Architecture
Domain (`ActiveEffect.CombatBinding`, pure value struct) -> Rules-adjacent pure decision layer (`ActiveEffectExpiryRules`'s six new methods, Application layer per this repo's existing placement of that file) -> a new read seam (`ICombatEncounterLifecycleReader`/`SqliteCombatEncounterLifecycleReader`, composes existing tables) -> an orchestrator (`CombatEffectExpiryService`) that derives booleans/counts, dispatches to the right pure function, and calls the existing `ExpireActiveEffect` only when `Expired`.

## 5. Milestones
M1 read `ADR-029` §7 verbatim, `ActiveEffectExpiryRules.cs`, `ActiveEffect.cs`, `ExpireActiveEffect`, `CombatEncounterRecord`/`CombatEncounterLifecycleEvent` schema; M2 derive the exact event sequence `CombatEncounterService.Advance` produces (TurnEnded-before-TurnStarted-per-Advance, RoundStarted only on wraparound) to design boundary detection without arithmetic-from-order; M3 Domain field + six pure methods; M4 reader + orchestrator; M5 real tests against a real advancing encounter; M6 validation/PR.

## 6. State and data flow
`CombatDurationBinding` captured once at (future) effect-application time -> `EvaluateAndExpireIfDue` reads the live `CombatEncounterRecord` (for `ForRounds`) or counts `CombatEncounterLifecycleEvent` rows since the captured high-water-mark (for the other five) -> dispatches to the matching pure `Check*` function -> `Expired` calls `ExpireActiveEffect`; `NotExpired` returns the candidate unchanged.

## 7. Error handling
Every inconclusive input (missing encounter, uninvolved combatant, a failed lifecycle read) is treated as fail-closed `NotExpired` by the orchestrator before it ever reaches a pure function with a defensible non-negative/true value; the pure functions themselves reject clearly invalid arguments (e.g. `requiredRounds < 1`) with `ArgumentException`/`ArgumentOutOfRangeException`, matching `ActiveEffectExpiryRules`'s own existing style.

## 8. Test strategy
Pure-function tests with fabricated ordinals/counts/booleans (no I/O) for all six methods; real-SQLite orchestrator tests that advance a genuine 2-participant `CombatEncounter` through the exact real event sequence and assert `ExpireActiveEffect` fires (`Status -> Expired`) exactly at each ADR-029 section 7 boundary, not one step early or late; two fail-closed tests (untracked combatant, failing reader); one architecture guard scanning `ActiveEffectExpiryRules.cs` for SQL/I-O fragments.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` full solution green; `test-fast.ps1`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` green; `git diff --name-status` against `main` confined to the ТЗ's allowed paths.

## 10. Recovery and rollback
No persisted task-owned data beyond one new optional column-equivalent field on the existing `ActiveEffect` row (serialized however `SqliteActiveEffectRepository` already persists new fields) and no new table; revert source to roll back.
