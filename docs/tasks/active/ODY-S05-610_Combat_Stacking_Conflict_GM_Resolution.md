# ODY-S05-610 — Combat Stacking Conflict GM Resolution

> **This is a task contract for future work, not a record of completed work.** It was created by `ODY-S05-606`'s own post-review amendment (2026-09-15, product-owner approved) to close a real, disclosed gap that task's independent review confirmed: a combat effect candidate whose `EffectStackPolicy` resolves to `RequestGMResolution` (`ADR-028` §7) currently creates no row, blocks nothing, and gives the GM no durable signal at all -- because `ODY-S05-503`'s own `ActiveEffectStackConflict` has never had cross-session persistence anywhere in this codebase (see that task's own doc comment, and `ODY-S05-606`'s own contract "Post-review amendment" section, for the full finding and the product owner's exact decision). `ODY-S05-606` did not introduce this gap; it inherited and honestly documented it (its own §5 Out of scope, §9 `TC-ATTACK-054`, §18 decision log). Every section below describes a requirement on this future task, not a decision already made.

## 1. Task identity
`ODY-S05-610`; status: Proposed (not started).

## 2. Goal
Give `ODY-S05-503`'s own `ActiveEffectStackConflict` (the pending record `ActiveEffectStackingRules.ResolveStacking` already produces for `EffectStackPolicy.RequestGMResolution`) durable, cross-session persistence, and provide a real command a MainGM can use to resolve it (`ActiveEffectStackConflictResolution.ApplyAsIndependentInstance`/`Replace`/`Ignore`, already defined by `ODY-S05-503`, unconsumed by any repository today). This closes the gap `ODY-S05-606` found and disclosed: a combat-sourced `RequestGMResolution` collision currently reaches no GM at all.

## 3. Authority
`ADR-028` §7 rule 7 (`RequestGMResolution`'s own pending-conflict/resolution-command requirement); `ODY-S05-503`'s own `ActiveEffectStackConflict`/`ActiveEffectStackConflictResolution`/`ActiveEffectStackingRules.ResolveActiveEffectStackConflict` (unmodified, already a complete pure decision layer with no persistence); `ODY-S05-606`'s own task contract ("Post-review amendment" section, 2026-09-15) for the exact finding and product-owner decision that created this task; `ODY-S05-604`'s own `AttackOutcome`/pending-intervention idiom (CAS-guarded, `CommandId`-idempotent, routed through `SqliteSavingPipeline`) as the directly analogous existing precedent this task should reuse the *pattern* of, not a novel mechanism.

## 4. In scope
- A new, standalone durable record of a pending `ActiveEffectStackConflict` (by direct analogy to `ODY-S05-604`'s own `AttackOutcome` table/`AttackOutcomeRecord`): at minimum the candidate application, the conflicting `ActiveEffectId`, `RaisedAt`, and enough of the triggering attack's own identity (e.g. the `ResolveAttack`/`ResolveAttackIntervention` `CommandId` that raised it) to trace it back to `ODY-S05-606`'s own apply step.
- A new root command (by direct analogy to `ODY-S05-604`'s own `ResolveAttackIntervention`) that lets a MainGM resolve a pending conflict via the existing `ActiveEffectStackConflictResolution` enum, calling the existing, unmodified `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` to get the resulting `ActiveEffectStackDecision`, then applying it (create/replace/ignore) atomically, reusing `ODY-S05-606`'s own established idiom of writing `ActiveEffect` rows via `SqliteActiveEffectRepository`'s internal SQL helpers rather than its public `CreateActiveEffect`.
- Wiring `ODY-S05-606`'s own `ApplyEffectCandidates` step so a `RequestGmResolution` stacking decision creates this new durable pending record (in the same atomic-apply transaction) instead of silently doing nothing.
- Tests proving: a `RequestGMResolution` collision creates a durable, readable pending record; a MainGM resolution command applies the chosen outcome exactly once (idempotent replay, no double-apply); a non-MainGM resolution attempt is denied; an unresolved pending conflict does not block the rest of the attack it arose from (the existing `ODY-S05-606` behavior, unchanged).

## 5. Out of scope
- `ODY-S05-605`'s own territory (combat duration expiry mechanisms/bindings).
- `ODY-S05-607`'s own territory (compensation commands, audience-filtered Game Log projections/rendering).
- `ODY-S05-609`'s own territory (`AttackDelta`-to-Character/Item-state application).
- Rewriting `ODY-S05-502`/`503`/`604`/`606`'s own already-accepted behavior -- this task consumes `ActiveEffectStackingRules`/`ActiveEffectStackConflict`/`ActiveEffectStackConflictResolution` exactly as `ODY-S05-503` already defined them.
- Appending a method to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository` -- if a new repository method is needed at all, it belongs on a new, standalone contract (by analogy to `IAttackApplyRepository`), never one of those three.
- Revising `ADR-028`/`ADR-029`.

## 6. Domain contract
To be determined by this task's own executor. No new Domain type is assumed necessary beyond what `ODY-S05-503` already defined (`ActiveEffectStackConflict`, `ActiveEffectStackConflictResolution`) -- this task's own work is primarily persistence and a resolution command, not new Domain vocabulary. If the executor finds a genuine need for a new Domain-level identity type (e.g. a canonical id for a persisted conflict row), that is a decision for this task's own contract amendment at execution time, not assumed here.

## 7. Application contract
To be determined. At minimum: a standalone repository contract (by analogy to `IAttackApplyRepository`, not an extension of `IActiveEffectRepository`) exposing something like `RecordStackConflict`/`GetStackConflict`/`ResolveStackConflict`, and an orchestration service (by analogy to `AttackApplyService`) exposing the new root command. Must not call one root command's own handler from inside another (`ADR-029` §9, the same rule `ODY-S05-604`/`606` already follow).

## 8. Persistence boundary
A new, standalone table for the pending conflict record (by analogy to `ODY-S05-604`'s own `AttackOutcome` table) -- not a column bolted onto `ActiveEffect` or `AttackOutcome`, since a conflict is its own durable entity with its own CAS/idempotency lifecycle. Reuses `SqliteSavingPipeline` for the resolution command's own atomic apply, and `SqliteActiveEffectRepository`'s internal SQL helpers (already made `internal` by `ODY-S05-606`) for the resulting `ActiveEffect` mutation -- never the public `CreateActiveEffect`, for the same reason `ODY-S05-606` already established.

## 9. Tests and validation
To be determined by this task's own executor. Minimum expectation: a `RequestGMResolution` stacking decision durably records a pending conflict readable after the fact; the resolution command is MainGM-only, CAS-guarded (an already-resolved conflict cannot be resolved twice), and idempotent by its own `CommandId`; the resulting `ActiveEffect` mutation (or its absence, for `Ignore`) matches `ActiveEffectStackingRules.ResolveActiveEffectStackConflict`'s own decision exactly; the triggering attack's own outcome is unaffected by an unresolved conflict (already true today, must remain true). `dotnet test` and the repository validation scripts must be green.

## 10. Compatibility and rollback
To be determined by this task's own executor. Expected: one new standalone table, no change to `ActiveEffect`/`AttackOutcome`'s own existing schema. Reverting this task's source and table should leave `ODY-S05-502`-`606`'s own behavior and data unaffected -- an unresolved `RequestGMResolution` collision would simply return to today's silent-no-op behavior, not a data-loss condition, since `606` itself creates no row for that candidate either way.

## 11. Security and privacy
The resolution command must be MainGM-only (`ADR-028` §7 rule 7's own "a MainGM resolves an ambiguous, contested, or expired intervention" convention, already established by `ODY-S05-604`'s own `ResolveAttackIntervention` for an analogous decision). The executor must not invent a broader eligible-controller-choice mechanism without citing why the existing MainGM-only precedent is insufficient.

## 12. Observability
Whether recording/resolving a conflict produces a genuinely new DomainEvent type or reuses an existing one is an open design question for this task's own executor.

## 13. Performance
Not decided; likely no additional cache/scheduler dependency given the existing `SqliteSavingPipeline`/connection-per-call pattern already used by `ODY-S05-604`/`606`.

## 14. Dependencies
`ODY-S05-606` (source of `RequestGmResolution` stacking decisions needing persistence), `ODY-S05-503` (`ActiveEffectStackConflict`/`ActiveEffectStackConflictResolution`/`ActiveEffectStackingRules.ResolveActiveEffectStackConflict`, unmodified), `ODY-S05-502`/`606` (`SqliteActiveEffectRepository`'s own internal SQL helpers, reused).

## 15. Dependencies (packages)
None anticipated.

## 16. Implementation plan
To be chosen by this task's own future executor at activation time (likely ExecPlan, given it introduces a new durable aggregate and a new root command, mirroring `ODY-S05-604`'s own planning-mode choice for an analogous pending/resolution pair).

## 17. Completion evidence
Not applicable -- this task has not started. This section will be filled in by the future executor once implemented.

## 18. Change control

### Decisions made during execution

- None yet -- this task has not started. The future executor must record their own decisions here, including the exact shape of the new persisted conflict record and whether the resolution command extends `ODY-S05-606`'s own `SqliteAttackApplyRepository` or lives in a wholly separate repository (the contract's own §8 leans toward a separate, standalone table/contract, by analogy to `IAttackApplyRepository` itself being separate from `IActiveEffectRepository` -- but this is a recommendation for the executor to confirm, not a decision already made).

### Blockers

- None yet identified beyond the open design questions named in sections 6/7/8/16 above, which this task's own future executor must resolve or explicitly escalate (not silently assume).
