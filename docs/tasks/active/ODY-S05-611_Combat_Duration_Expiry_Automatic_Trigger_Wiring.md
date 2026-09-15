# ODY-S05-611 — Combat Duration Expiry Automatic Trigger Wiring

> **This is a task contract for future work, not a record of completed work.** It was created by `ODY-S05-608`'s own post-review confirmation (2026-09-16, product-owner approved) to close a real, disclosed gap `ODY-S05-605` itself first named and `ODY-S05-606`/`608` each independently confirmed still open: `CombatEffectExpiryService.EvaluateAndExpireIfDue` (`605`) is never called automatically anywhere in production code -- `605`'s own doc comment already states plainly that wiring a real call site "is explicitly out of this task's own scope," `606` did not pick it up when it added combat effect creation, and `608` (a Brief-plan, tests-only integration task, forbidden from introducing new production behavior) proved the service works correctly via a **direct** call and explicitly left the gap open rather than silently closing or hiding it (see `608`'s own task contract, §18 decision log and "Product-owner decision" note, for the full finding). Every section below describes a requirement on this future task, not a decision already made.

## 1. Task identity
`ODY-S05-611`; status: Proposed (not started).

## 2. Goal
Give `ODY-S05-605`'s own `CombatEffectExpiryService.EvaluateAndExpireIfDue` a real, automatic production call site, so a combat-duration-bound `ActiveEffect` (`ForRounds`/`ForTurns`/`UntilSourceTurnStart`/`UntilSourceTurnEnd`/`UntilTargetTurnStart`/`UntilTargetTurnEnd`) actually expires as encounters are played, rather than only when a test (or some other future caller) happens to call the service directly. This closes the gap `605` itself disclosed, `606` did not pick up, and `608` independently reconfirmed still open.

## 3. Authority
`ADR-029` §7 (the six combat duration boundary mechanisms, unmodified); `ODY-S05-605`'s own task contract/doc comments (`CombatEffectExpiryService.cs`'s own class-level doc comment: "wiring that call site is explicitly out of this task's own scope"); `ODY-S05-608`'s own task contract (§18 decision log and "Product-owner decision" note, 2026-09-16) for the exact finding and product-owner decision that created this task; the `ODY-S05-604`→`609` and `ODY-S05-606`→`610` precedent this backlog already established for a task's own disclosed, honestly-left-open gap becoming a separately reserved follow-up rather than a silent oversight or an unauthorized scope expansion of the task that found it.

## 4. In scope
- A real, automatic call site that invokes `CombatEffectExpiryService.EvaluateAndExpireIfDue` for combat-duration-bound `ActiveEffect` rows at an appropriate point in the encounter lifecycle -- most plausibly (not mandated) inside `CombatEncounterService.Advance` (`ODY-S05-602`) or an equally natural turn/round-boundary point this task's own executor identifies and justifies, given that every one of the six duration types is itself defined relative to a turn/round boundary (`ADR-029` §7).
- Whatever minimal read/enumeration is needed to find the candidate `ActiveEffectRecord`(s) to check at that call site (e.g. every `Active`, combat-bound effect targeting or sourced from the encounter's own participants) -- reusing `IActiveEffectRepository`'s existing read methods (`ListActiveEffectsByTarget`/`ListActiveEffectsBySource`, `ODY-S05-502`), not a new query mechanism invented from scratch unless the executor finds a genuine, justified need.
- Tests proving the real wiring actually fires: advancing a real encounter past a real combat-bound effect's own boundary genuinely expires it in production code, with no direct test-only call required -- the missing half of what `605`'s own tests and `608`'s own `TC-ATTACK-080` could not prove.

## 5. Out of scope
- Revising `ActiveEffectExpiryRules`'s own six pure boundary-check functions (`605`, unmodified) -- this task wires an existing, already-correct decision layer to a real call site, it does not re-decide when a duration boundary is crossed.
- Reopening `ODY-S05-606`'s own effect-application/stacking territory -- this task only adds an expiry check, it does not touch how or when an `ActiveEffect` is created or stacked.
- `ODY-S05-607`'s own territory (compensation commands, audience-filtered Game Log projections).
- `ODY-S05-609`'s own territory (`AttackDelta`-to-Character/Item-state application).
- `ODY-S05-610`'s own territory (`RequestGMResolution` stacking-conflict persistence/resolution) -- a different, already-separately-reserved gap.
- Revising `ADR-028`/`ADR-029`.
- Any new event bus/publisher/subscriber -- this codebase has none (confirmed by direct repository search, per every predecessor task in this block), and this task does not introduce one; the wiring is a direct call from whatever call site is chosen, exactly like every other composition in this block.

## 6. Domain contract
No new Domain type is assumed necessary -- `ActiveEffect`/`CombatDurationBinding`/`EffectDurationType` (`605`, unmodified) already carry everything `CombatEffectExpiryService.EvaluateAndExpireIfDue` needs. If the executor finds a genuine need for a new Domain-level concept (e.g. to record which duration type a given `ActiveEffect` uses, since today's `EvaluateAndExpireIfDue` signature takes `durationType` as an explicit caller-supplied parameter rather than reading it off the record), that is a decision for this task's own contract amendment at execution time, not assumed here -- see §18.

## 7. Application contract
To be determined by this task's own executor. At minimum: the chosen call site (most plausibly `CombatEncounterService.Advance`, per §4) must, for each relevant combat-bound `ActiveEffect`, call the existing, unmodified `CombatEffectExpiryService.EvaluateAndExpireIfDue` with the correct `EffectDurationType` for that effect. Must not call one root command's own handler from inside another in a way that violates `ADR-029` §9 -- if `Advance` itself is the chosen site, the executor must confirm this composition (a service call from within another service's own already-accepted orchestration, not a nested root-command invocation) is the right shape, by direct analogy to how `606`'s own `ApplyEffectCandidates` already composes inside `SqliteAttackApplyRepository`'s own transaction rather than calling a second root command.

## 8. Persistence boundary
No new table is assumed necessary -- `ActiveEffect`/`CombatEncounterParticipant` (existing) already carry what is needed to find candidates. Whether the automatic expiry check runs inside the same transaction as `Advance`'s own turn/round mutation, or as a separate, immediately-following step, is an open design question for this task's own executor to resolve and justify (see §18) -- `ExpireActiveEffect` (`ODY-S05-502`, unmodified) is itself already transactional per call.

## 9. Tests and validation
To be determined by this task's own executor. Minimum expectation: advancing a real encounter (via the real, wired call site) past a combat-bound effect's own real boundary genuinely transitions it to `Expired` in the database, with no test-only direct `EvaluateAndExpireIfDue` call required to make it happen; an effect not yet past its boundary remains `Active` after a real `Advance` (fail-closed, matching `605`'s own pure-function guarantee); an effect with no combat binding, or one already `Expired`/`Suspended`/`Removed`, is left untouched by the automatic check. `dotnet test` and the repository validation scripts must be green.

## 10. Compatibility and rollback
To be determined by this task's own executor. Expected: no schema change; `CombatEncounterService.Advance`'s own existing callers are unaffected in their own success/failure contract (the expiry check should not turn an otherwise-successful `Advance` into a failure for a reason unrelated to the encounter's own advancement, unless the executor finds and justifies a reason it must). Reverting this task's source should leave `602`-`609` and `610` (if merged by then) unaffected -- combat-bound effects would simply stop auto-expiring again, returning to today's already-accepted (if incomplete) behavior, not a data-loss condition.

## 11. Security and privacy
No new authorization gate is assumed necessary -- automatic expiry is a system-driven consequence of an already-authorized `Advance` command, not a new player-facing action requiring its own permission check. The executor must not invent a new gate without citing why the existing authorization on the chosen call site is insufficient.

## 12. Observability
Whether an automatic expiry produces a genuinely new DomainEvent, reuses `ExpireActiveEffect`'s own existing event, or needs no additional event beyond what that method already appends is an open design question for this task's own executor.

## 13. Performance
Not decided; likely a small additional read (candidate effect enumeration) plus zero or more `ExpireActiveEffect` calls per `Advance` invocation. The executor should confirm this does not turn a normally-cheap turn-advancement command into an unbounded-cost operation for an encounter with many combat-bound effects (e.g. by scoping candidates to the encounter's own current participants, not every `ActiveEffect` in the campaign).

## 14. Dependencies
`ODY-S05-605` (`CombatEffectExpiryService`/`ActiveEffectExpiryRules`/`ICombatEncounterLifecycleReader`, unmodified -- this task wires it, does not revise it), `ODY-S05-602` (`CombatEncounterService`/`ICombatEncounterRepository`, the most plausible but not mandated call-site owner).

## 15. Dependencies (packages)
None anticipated.

## 16. Implementation plan
To be chosen by this task's own future executor at activation time (likely ExecPlan, given it changes real production control flow inside an already-accepted command, unlike `608`'s own Brief-plan composition-only scope).

## 17. Completion evidence
Not applicable -- this task has not started. This section will be filled in by the future executor once implemented.

## 18. Change control

### Decisions made during execution

- None yet -- this task has not started. The future executor must record their own decisions here, including the exact chosen call site (and why), whether the expiry check shares `Advance`'s own transaction or runs as a following step, and how candidate combat-bound effects are enumerated without scanning the whole campaign.

### Blockers

- None yet identified beyond the open design questions named in sections 6/7/8/16 above, which this task's own future executor must resolve or explicitly escalate (not silently assume).
