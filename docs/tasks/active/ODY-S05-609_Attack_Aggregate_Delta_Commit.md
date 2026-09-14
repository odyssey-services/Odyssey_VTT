# ODY-S05-609 — Attack Aggregate Delta Commit

> **This is a task contract for future work, not a record of completed work.** It was created by `ODY-S05-604`'s own post-review amendment (2026-09-14, product-owner approved) to close a real, disclosed gap that task's independent review found: `ADR-029` §1 rule 5, §6 stage 13, and §12 item 4 all name Character/Item state as part of what atomic apply must commit, but `ODY-S05-604` deliberately does not apply any `AttackDelta` to Character/Item state (see that task's own contract, "Post-review amendment" section, for the full finding and the product owner's exact decision). Every section below describes a requirement on this future task, not a decision already made.

## 1. Task identity
`ODY-S05-609`; status: Proposed (not started).

## 2. Goal
Apply the Character/Item state deltas an accepted attack outcome already carries -- `AttackDelta.Value`, already computed by `ODY-S05-603`'s own Rules evaluation and persisted by `ODY-S05-604`'s `AttackOutcome`/`SqliteAttackApplyRepository` -- through the already-existing generic write paths (`ICharacterRepository.SetResourceCurrentValue`, and an analogous Item-side mechanism if one is needed), so that `ODY-S05-608`'s own Definition of Done item 4 (`ADR-029` §12) can be genuinely proven. This task resolves an `AttackDelta.TargetRef` string to a concrete `CharacterResourceId`/`ItemInstanceId` and writes the already-computed value; it does not invent, choose, or evaluate any Ruleset formula.

## 3. Authority
`ADR-029` §1 rule 5, §6 stage 13 (atomic apply's own affected-aggregate list), §12 item 4 (Definition of Done: "immediate and intervened attacks each commit every approved aggregate delta ... atomically or commit none"); `ODY-S05-604`'s own task contract ("Post-review amendment" section, 2026-09-14) for the exact finding and product-owner decision that created this task; `ODY-S05-603`'s `AttackDelta`/`ProposedAttackResolution` (unmodified); `ODY-S05-604`'s `AttackOutcomeRecord`/`IAttackApplyRepository` (unmodified, consumed as the source of accepted deltas).

## 4. In scope
- Resolving an `AttackDelta.TargetRef` string to a concrete `CharacterResourceId` (and, if the delta targets an item, a concrete `ItemInstanceId`) -- this is identity resolution, not formula evaluation.
- Applying the already-computed `AttackDelta.Value` through `ICharacterRepository.SetResourceCurrentValue` (existing method, unmodified signature) for Character-targeted deltas, and an analogous existing Item-side write path for Item-targeted deltas if the executor's own investigation finds one is needed and already exists.
- Extending `ODY-S05-604`'s own atomic-apply transaction (or composing a second, causally-linked transaction against the same accepted `AttackOutcome`, if the executor determines that is the safer design -- an explicit open question for this task's own future executor, not decided here) so delta application is atomic with, or a directly-verifiable consequence of, the accepted outcome.
- Tests proving the applied delta matches the accepted outcome's own recorded value, exactly once per accepted outcome (no double-application on retry).

## 5. Out of scope
- Choosing, evaluating, or inventing any Ruleset formula (hit/damage/cost/armor/body-part) -- `AttackDelta.Value` is already computed by the time this task touches it.
- `ODY-S05-605`'s own territory (combat duration expiry mechanisms/bindings).
- `ODY-S05-606`'s own territory (creating/updating `ActiveEffect` rows from an `EffectApplicationDecision`).
- `ODY-S05-607`'s own territory (compensation commands, audience-filtered Game Log projections/rendering).
- Rewriting `ODY-S05-602`/`603`/`604`'s own already-accepted behavior.
- Appending a method to `ICombatEncounterRepository`/`IInventoryRepository`/`IActiveEffectRepository` beyond what already exists (`ICharacterRepository.SetResourceCurrentValue` already exists and requires no new method; if an analogous Item-side write path does not already exist, the executor must treat that as an open question rather than inventing a new cross-cutting method silently).
- Revising `ADR-008`/`ADR-012`/`ADR-029`.

## 6. Domain contract
To be determined by this task's own executor. No new Domain type is assumed necessary -- `AttackDelta` (Domain, `ODY-S05-603`) already carries `TargetRef`/`Value`; this task's own work is primarily identity resolution and Application/Persistence-layer orchestration, not new Domain vocabulary. If the executor finds a genuine need for a new Domain type (e.g., a typed `TargetRef` discriminated union instead of today's opaque string), that is a decision for this task's own contract amendment at execution time, not assumed here.

## 7. Application contract
To be determined. At minimum: a mechanism that, given an accepted `AttackOutcomeRecord`, resolves each of its deltas' `TargetRef` values to concrete identities and calls the existing `ICharacterRepository.SetResourceCurrentValue` (and any analogous Item-side path) with the already-computed value. Must not call one root command's own handler from inside another (`ADR-029` §9, the same rule `ODY-S05-604` already follows).

## 8. Persistence boundary
Reads `ODY-S05-604`'s own `AttackOutcome` state as the source of truth for accepted deltas (via `IAttackApplyRepository`, unmodified). Writes only through the already-existing `ICharacterRepository`/`IInventoryRepository` write paths already accepted by prior tasks -- no new standalone table is assumed necessary, unlike `ODY-S05-604`'s own new `AttackOutcome` table, since this task's own job is applying values through paths that already exist.

## 9. Tests and validation
To be determined by this task's own executor. Minimum expectation: an accepted attack outcome's own recorded deltas are applied exactly once to the correct resource/item identity with the correct value; a retry of the same command does not double-apply; an outcome that is `Pending`/`Rejected`/`Cancelled` has no delta applied. `dotnet test` and the repository validation scripts must be green.

## 10. Compatibility and rollback
To be determined by this task's own executor once its own concrete design (single extended transaction vs. a second causally-linked one) is chosen.

## 11. Security and privacy
No new authorization surface is assumed; delta application should occur only for an outcome already accepted through `ODY-S05-604`'s own authorization gates (MainGM for intervention resolution; actor-control check for the immediate path). The executor must not invent a new, separate permission gate without citing why the existing one is insufficient.

## 12. Observability
Whether a delta-application event is a genuinely new DomainEvent or an extension of `ODY-S05-604`'s own committed event is an open design question for this task's own executor.

## 13. Performance
Not decided; likely no additional cache/scheduler dependency given the existing write paths already used.

## 14. Dependencies
`ODY-S05-604` (source of accepted deltas), `ICharacterRepository.SetResourceCurrentValue` (existing, unmodified), and whatever existing Item-side write path the executor's own investigation identifies as the correct analog (an explicit open question if none obviously exists -- to be raised, not invented).

## 15. Dependencies (packages)
None anticipated.

## 16. Implementation plan
To be chosen by this task's own future executor at activation time (likely ExecPlan, given it introduces real production code touching multiple aggregates, mirroring `ODY-S05-604`'s own planning-mode choice).

## 17. Completion evidence
Not applicable -- this task has not started. This section will be filled in by the future executor once implemented.

## 18. Change control

### Decisions made during execution

- None yet -- this task has not started. The future executor must record their own decisions here, including how `TargetRef` resolution is designed and whether delta application extends `ODY-S05-604`'s own transaction or uses a second, causally-linked one.

### Blockers

- None yet identified beyond the open design questions named in sections 4/6/7/10/14 above, which this task's own future executor must resolve or explicitly escalate (not silently assume).
