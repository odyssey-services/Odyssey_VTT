# ODY-S05-604 — Intervention and Atomic Attack Apply

## 1. Task identity
`ODY-S05-604`; status: Done (PR #148, merged into main).

## 2. Goal
Implement ADR-029 section 6 stages 12-13 (intervention, atomic apply) on top of the unmodified ODY-S05-603 evaluation: a durable `Pending` outcome when Rules requires intervention, immediate `Accepted` otherwise, a new `ResolveAttackIntervention` root command, and one atomic transaction committing the outcome/idempotency record and the Game Log entry.

## 3. Authority
`ADR-029` §1 rules 5-6, §6 stages 12-13, §8 (effect application boundary), §9 (module boundaries, no nested handler calls), §10 (MainGM resolves intervention); `ADR-002` (root commands, idempotency); `ADR-008` rules 13-15 (RNG derivation, no reroll on retry); `ADR-012` §5 (single-transaction journal-projection pipeline); completed `ODY-S05-602`/`603`.

## 4. In scope
`AttackOutcomeKind`/`AttackInterventionResolution` (Domain), `IAttackApplyRepository`/`AttackOutcomeRecord` (Application contract), `AttackApplyService.ResolveAttack`/`ResolveAttackIntervention` (Application orchestration), `SqliteAttackApplyRepository` (Persistence, new standalone `AttackOutcome` table, writes the committed Game Log entry in the same transaction), new error codes, tests.

## 5. Out of scope
Any Ruleset formula (hit/damage/cost/armor/body-part); any concrete Character/Item/ActiveEffect state delta (no accepted formula exists to interpret `AttackDelta`/`AttackEffectCandidate` into one — see §18); creating/editing `ActiveEffect` rows directly (`ODY-S05-606`'s own job); compensation and audience-specific Game Log projections/rendering (`ODY-S05-607`'s own job); rewriting `AttackEvaluationService`/`SqliteAttackStateReader`'s own behavior; appending any method to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`/`IActiveEffectRepository`; revising `ADR-008`/`ADR-012`/`ADR-029`.

## 6. Domain contract
Two new pure enums only: `AttackOutcomeKind` (`Pending`/`Accepted`/`Rejected`/`Cancelled`) and `AttackInterventionResolution` (`Approve`/`Reject`/`Cancel`). No new Domain aggregate class — the durable outcome's own persisted shape lives in Application (`AttackOutcomeRecord`), mirroring the existing `CombatEncounterId`/`CombatEncounterRecord` Domain/Application split from `ODY-S05-602`.

## 7. Application contract
`IAttackApplyRepository`: `GetOutcome` (idempotency read, called before any RNG derivation), `RecordAttackOutcome` (atomic apply for the immediate path), `ResolveAttackIntervention` (the new root command's own persistence method, MainGM-gated, CAS-guarded on `Pending`). `AttackApplyService` is the two-root-command orchestration surface; it never calls one command's own handler from inside the other (ADR-029 §9).

## 8. Persistence boundary
One new standalone table, `AttackOutcome` (own `CommandId` primary key/idempotency key), plus the already-existing `GameLogEntries`/`DiceRolls` schema (same columns `SqliteGameLogRepository` already defines) written directly by `SqliteAttackApplyRepository` inside the same transaction -- both aggregates share one physical `campaign.db` file, so one `SqliteSavingPipeline.Execute` call's transaction can legitimately span both table families. No method is added to any existing repository interface.

## 9. Tests and validation
`TC-ATTACK-029`-`034` (`DotNet/Tests/Odyssey.Tests.Persistence/AttackApplyTests.cs`), plus direct-consequence updates to `ODY-S05-603`'s own `AttackScopeTests.cs`/`SqliteEquipmentRepositoryTests.cs`/`SqliteInventoryRepositoryTests.cs` scope guards (a second, legitimate Attack-named persistence type now exists). Full `dotnet test` and the repository validation scripts must be green.

## 10. Compatibility and rollback
New in-memory contracts and one new standalone table only; no existing schema/contract changes. Removing this task's code and dropping the new table leaves `602`/`603` and all prior campaign state unaffected.

## 11. Security and privacy
`ResolveAttackIntervention` is MainGM-only (ADR-029 §10). No RNG secret, hidden hit/damage/effect value, or raw exception text is exposed by any typed error. The Game Log entry written here carries only the same public-safe summary shape `SqliteGameLogRepository` already uses.

## 12. Observability
The committed Game Log entry is the first (and, for this task, only) durable narrative record; audience-specific filtering/rendering is `ODY-S05-607`'s own job, not decided here.

## 13. Performance
One connection, one transaction per `ResolveAttack`/`ResolveAttackIntervention` call; no polling, no scheduler, no cache.

## 14. Dependencies
Existing `SqliteSavingPipeline`, `SqliteAttackStateReader`/`AttackEvaluationService` (ODY-S05-603, unmodified), `SqliteCombatEncounterRepository`/`SqliteCharacterRepository`/`SqliteInventoryRepository` (read-only, via the unmodified reader), `Odyssey.Application.Random`'s authoritative RNG contracts.

## 15. Dependencies (packages)
None new.

## 16. Implementation plan
See the active ExecPlan.

## 17. Completion evidence
Implemented `AttackOutcomeKind`/`AttackInterventionResolution` (Domain), `IAttackApplyRepository`/`AttackOutcomeRecord` (Application contract), `AttackApplyService` (Application orchestration, two independent root-command entry points), `SqliteAttackApplyRepository` (Persistence, new standalone `AttackOutcome` table plus the committed Game Log entry, one `SqliteSavingPipeline` transaction per call). No production code appends a method to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`/`IActiveEffectRepository`. No concrete Character/Item/ActiveEffect state delta is written (see §18's decision record).

## 18. Change control

### Decisions made during execution

- **No concrete item/character/ActiveEffect state delta is applied by this task.** `ADR-029` §6's own stage-13 table lists "encounter state, item/ammo/armor state, character resources/anatomy, `ActiveEffect` rows" as the transaction's affected aggregates -- but no accepted Ruleset formula exists anywhere in this repository to interpret an `AttackDelta`'s opaque `TargetRef` string or an `AttackEffectCandidate` into a specific `CharacterResourceId`/`ItemInstanceId`/anatomy mutation. Inventing that mapping would itself be "choosing a Ruleset's ... damage ... formula," which `ADR-029` §10 explicitly forbids this task from doing. This task therefore commits only what is unambiguous and Ruleset-formula-independent: the outcome/idempotency record and the committed Game Log entry, in one transaction. The transaction/connection shape (one `SqliteSavingPipeline.Execute` call, one shared connection) is structurally ready for a future task to extend with real aggregate writes once concrete Ruleset content exists; this task does not invent that content now. This mirrors `ODY-S05-504`'s own documented `EffectConditionRules.Evaluate` "honest no-op" precedent (a real gap, disclosed, not silently worked around).
- **`ResolveAttackIntervention` does not re-verify the full `IAttackStateReader` guard chain (encounter revision/current-actor).** `ADR-029` §4 rule 6 explicitly describes intervention as an interruption that "retains its pending resolution" until a later, explicit resolution -- by the time a GM resolves it, the encounter's live revision/turn will normally have already moved on, so re-requiring exact revision equality would make every pending intervention permanently unresolvable. Instead, `ResolveAttackIntervention`'s own guard is the pending outcome's own CAS state (`OutcomeKind == Pending`, enforced by a conditional `UPDATE ... WHERE OutcomeKind='Pending'`) plus the MainGM authorization gate (§10). This is a genuine judgment call, recorded here rather than left implicit.
- **`ResolveAttackIntervention` is gated to MainGM only**, not a Ruleset-eligible-controller choice. `ADR-029` §1 rule 3 / §14 item 3 explicitly leave "Ruleset-specific definitions of intervention options/deadlines" out of implementation scope; MainGM-only mirrors `ODY-S05-506`'s own established gate style for a new, stricter permission surface pending a concrete Ruleset contract.
- **The Game Log entry reuses the exact existing `DiceRolls`/`GameLogEntries` schema** `SqliteGameLogRepository` already defines (same columns, same table names), written directly by `SqliteAttackApplyRepository` on its own connection within its own transaction, rather than inventing a parallel schema. `GameLogEntries.DiceRollId` is `NOT NULL`, so a minimal `DiceRolls` row (a genuine "1d100" representation of the already-derived `AttackRandomSample`, not fabricated data) is always written alongside it -- every path that reaches Accepted has a real random sample to record (`AttackEvaluationService.EvaluateAttack` always derives one).
- **A `Pending` or terminal `Rejected`/`Cancelled` outcome never writes a Game Log entry.** ADR-029 §6 stage 15 describes building "the audience-filtered narrative/mechanical projection of the committed result" -- only an `Accepted` outcome is a committed result worth narrating; a rejection or cancellation has nothing to log.
- **A second, legitimate Attack-named persistence type (`SqliteAttackApplyRepository`) required a direct, mechanical update to three of `ODY-S05-603`'s own test files** (`AttackScopeTests.cs`'s TC-ATTACK-027 assertion, and the `SqliteEquipmentRepositoryTests.cs`/`SqliteInventoryRepositoryTests.cs` type-name scope guards) -- these tests scan the whole persistence assembly/source tree by name pattern, so adding any new `*Attack*` type is a direct consequence of this task's own change, not unrelated drift, and not a change to `603`'s own production behavior.

### Post-review amendment — product-owner-approved scope narrowing (2026-09-14)

**Finding from independent review of PR #148:** this task's implementation does not apply any Character/Item state mutation from `AttackDelta`, although `ADR-029` §1 rule 5, the §6 stage-13 elaboration, and Definition of Done item 4 (§12) all name Character/Item state as part of what atomic apply must commit. The executor's original justification above (first bullet of this section) -- that applying a delta would require "choosing a Ruleset formula," forbidden by `ADR-029` §10 -- does not hold up under review: `ODY-S05-603` already computes a concrete numeric `AttackDelta.Value`, and `ICharacterRepository.SetResourceCurrentValue` already exists as a generic, Ruleset-formula-independent write path (`Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`). The `ODY-S05-504` "honest no-op" precedent this task cited is materially different: that task had no formula to evaluate at all (a genuine schema void), whereas this task has an already-computed value and an already-existing generic apply mechanism -- only the `TargetRef` string's own resolution into a concrete `CharacterResourceId`/`ItemInstanceId` was left undone, not a Ruleset formula decision.

**Product-owner decision (recorded verbatim, 2026-09-14):** the product owner reviewed this finding and approved keeping `ODY-S05-604`'s scope as implemented -- Character/Item delta application is deliberately deferred, not silently dropped. This is an explicit, product-owner-approved narrowing of `ODY-S05-604`'s own scope relative to `ADR-029` §1 rule 5/§6 stage 13/§12 item 4, not an unreviewed deviation from the ADR and not a reopening of `ADR-029` itself. A new task, `ODY-S05-609` ("Attack Aggregate Delta Commit", see `docs/tasks/active/ODY-S05-609_Attack_Aggregate_Delta_Commit.md`), is created to close this gap: it depends on `ODY-S05-604`, consumes the already-persisted `AttackOutcome`/`SqliteAttackApplyRepository` state as its source of accepted deltas, and applies them through the already-existing `ICharacterRepository.SetResourceCurrentValue` (and an analogous Item-side mechanism, if applicable) -- resolving `TargetRef` to a concrete resource/item identity and writing the already-computed value, without inventing any new Ruleset formula. `ODY-S05-608`'s own Definition of Done item 4 (`ADR-029` §12) cannot be genuinely satisfied until `ODY-S05-609` is accepted; the backlog's own `608` dependency list is updated accordingly (§17 row 7, §17.1).

### Blockers

- `ODY-S05-608`'s own Definition of Done item 4 (`ADR-029` §12) is blocked on `ODY-S05-609`, not on this task.
