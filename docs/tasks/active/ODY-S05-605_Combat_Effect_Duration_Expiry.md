# ODY-S05-605 — Combat Effect Duration Expiry

## 1. Task identity
`ODY-S05-605`; status: In Progress.

## 2. Goal
Implement `ADR-029` §7's own six turn/round-based `EffectDurationType` boundary-check mechanisms (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`, `UntilSourceTurnEnd`, `UntilTargetTurnStart`, `UntilTargetTurnEnd`) as pure decision functions, plus a minimal, real (not stubbed) caller that reuses the existing `ExpireActiveEffect` to apply that decision. This task decides only *whether* a combat-duration effect has expired -- it never decides *whether* to create or apply an effect (`ADR-029` §8, `ODY-S05-606`'s own territory).

## 3. Authority
`ADR-029` §7 (verbatim table for the six duration types and their bindings); `ADR-028` §11 (fail-closed rule, reconfirmed by `ADR-029` §7's own closing paragraph); `ODY-S05-504`'s own `ActiveEffectExpiryRules.cs` (the established "pure sibling function per duration mechanism, no event bus" idiom this task extends); `ODY-S05-602`'s own `CombatEncounterRecord`/`CombatEncounterLifecycleEvent` (the only state surfaces this task reads); `ODY-S05-502`'s own `IActiveEffectRepository.ExpireActiveEffect` (reused unmodified).

## 4. In scope
Six new pure sibling methods in `ActiveEffectExpiryRules.cs`; a new `CombatDurationBinding` value struct on the existing `ActiveEffect` Domain type (a new optional field, not a parallel aggregate); a new, standalone `ICombatEncounterLifecycleReader`/`SqliteCombatEncounterLifecycleReader` read seam over the existing `CombatEncounterLifecycleEvent` audit table; a new `CombatEffectExpiryService.EvaluateAndExpireIfDue` orchestrator that dispatches to the right pure function and calls the existing `ExpireActiveEffect` when due; tests.

## 5. Out of scope
Any event bus/publisher/subscriber infrastructure (none exists in this codebase; this task does not introduce one -- confirmed by direct repository search, `grep` for `Subscribe(`/`IEventPublisher`/`IDomainEventPublisher` stays empty). Wiring `EvaluateAndExpireIfDue` into an actual automatic trigger point (e.g. inside `CombatEncounterService.Advance` or a "list active effects" call site) -- the orchestrator is complete and tested, but calling it automatically at a specific moment is left to whichever future task first needs that (most plausibly `606`/`608`). `ADR-029` §8 (`EffectApplicationDecision`, effect creation/application) -- `ODY-S05-606`'s own territory. `ODY-S05-604`'s atomic-apply/`ResolveAttackIntervention`, `ODY-S05-609`'s delta-commit. Any method added to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`. Any new method on `IActiveEffectRepository` (the existing `ExpireActiveEffect` signature already suffices -- see §18).

## 6. Domain contract
`CombatDurationBinding` (new, `Packages/com.odyssey.domain/Runtime/Effects/ActiveEffect.cs`): `EncounterId`, `SourceCombatantId`/`TargetCombatantId` (nullable, at least one required), `AppliedRoundOrdinal`, `AppliedLifecycleEventId` (a `CombatEncounterLifecycleEvent` high-water-mark), `RequiredCount` (the Ruleset-content-supplied `N` for `ForRounds`/`ForTurns`, unused for the four boundary-only values). `ActiveEffect` gains one new, trailing, optional (`= null`) constructor parameter, `combatBinding` -- every existing caller compiles unchanged.

## 7. Application contract
`ActiveEffectExpiryRules` (existing file) gains six new pure static methods, one per duration type, each taking already-derived scalars/booleans/counts and returning the existing `ActiveEffectExpiryDecision`. `ICombatEncounterLifecycleReader` (new) is the read seam an orchestrator uses to derive those booleans/counts from the audit log; `CombatEffectExpiryService` (new) is that orchestrator -- it reads the live `CombatEncounterRecord`, treats a missing/inconclusive read as fail-closed `NotExpired`, and calls `IActiveEffectRepository.ExpireActiveEffect` only when a pure function returns `Expired`.

## 8. Persistence boundary
`SqliteCombatEncounterLifecycleReader` (new, standalone) composes the existing `ICombatEncounterRepository.Get` (confirms the encounter exists / tables exist) and then reads the existing `CombatEncounterLifecycleEvent` table directly -- no method is appended to `ICombatEncounterRepository`, mirroring `ODY-S05-603`'s own `SqliteAttackStateReader` precedent of a new standalone reader over existing tables rather than a foreign-repository extension. No new table is created by this task.

## 9. Tests and validation
`TC-ATTACK-035`-`045` (see §18 for the prefix decision): six pure-function tests (`035`-`038`), five real-SQLite orchestrator tests proving each boundary fires exactly when `ADR-029` §7 specifies and that `ExpireActiveEffect` is genuinely reused (`039`-`044`, including two fail-closed tests), and one architecture guard proving the six pure functions contain no SQL/I-O (`045`). Full `dotnet test` and the repository validation scripts must be green.

## 10. Compatibility and rollback
One new optional Domain field (backward compatible), two new files, six new pure methods on an existing file. No existing schema/contract changes; no new table. Reverting this task's source leaves every prior task's own behavior and data unaffected.

## 11. Security and privacy
No new authorization surface -- `EvaluateAndExpireIfDue` performs no permission check itself (it decides/applies an already-authorized `ActiveEffect`'s own expiry, the same authorization-free posture `ODY-S05-504`'s own `CheckForDurationExpiry`/`CheckWhileConditionExpiry` already have). No RNG, no hidden data exposure.

## 12. Observability
No Game Log or diagnostic event is introduced; expiry remains a plain `Status -> Expired` transition through the existing `ExpireActiveEffect`, exactly as `ODY-S05-504` already established.

## 13. Performance
Each `EvaluateAndExpireIfDue` call performs at most one `CombatEncounterRecord` read and one `CombatEncounterLifecycleEvent` count query; no polling loop, no scheduler, no cache.

## 14. Dependencies
`ODY-S05-602` (`CombatEncounterRecord`/`CombatEncounterLifecycleEvent`, unmodified), `ODY-S05-502`/`504` (`ActiveEffect`, `IActiveEffectRepository.ExpireActiveEffect`, `ActiveEffectExpiryRules.cs`, unmodified except for the six additive sibling methods and one additive Domain field).

## 15. Dependencies (packages)
None new.

## 16. Implementation plan
See the active ExecPlan.

## 17. Completion evidence
Implemented `CombatDurationBinding` (Domain, additive optional field on `ActiveEffect`), six pure boundary-check methods (`ActiveEffectExpiryRules.cs`), `ICombatEncounterLifecycleReader`/`SqliteCombatEncounterLifecycleReader` (new standalone read seam), and `CombatEffectExpiryService.EvaluateAndExpireIfDue` (orchestrator, reuses the existing `ExpireActiveEffect` unmodified). No method was appended to `ICombatEncounterRepository`/`IInventoryRepository`/`ICharacterRepository`/`IActiveEffectRepository`. No `EffectApplicationDecision`/effect-creation/atomic-apply/delta-commit logic was implemented.

## 18. Change control

### Decisions made during execution

- **Test-ID prefix: `TC-ATTACK-035` onward, not a new `TC-ACTIVEEFFECT-085` range.** Per the governing ТЗ's own explicit, non-discretionary instruction (its section 5): this task is part of the attack-pipeline block (backlog §17, depends on `602`, not on `504`/`506`/`507`) even though it physically extends `504`'s own `ActiveEffectExpiryRules.cs` file -- the boundary logic is combat/encounter-specific (round/turn ordinals of a specific `CombatEncounter`), conceptually a continuation of the attack-pipeline test sequence, not the `ActiveEffect`-aggregate one.
- **`CombatDurationBinding` is a new field on the existing `ActiveEffect` type, not a parallel structure.** `ADR-029` §7's own closing paragraph requires capturing "the encounter ID, source/target bindings, relevant ordinal ... with the application snapshot" -- no such field existed on `ActiveEffect` before this task (confirmed by direct inspection: `ActiveEffectSourceRef` carries no `CharacterId` at all for `Action`/`GMDirect` sources, and `ActiveEffectTargetRef` only carries one for `Character`-kind targets, neither shaped for a *combat participant* binding distinct from source/target-kind). Mirrors `ExpiresAt`'s own existing "populated only when this duration type needs it" idiom exactly, added as a new trailing optional constructor parameter so every existing caller compiles unchanged.
- **Turn-boundary detection reads the `CombatEncounterLifecycleEvent` audit log rather than computing turn ordinals arithmetically from participant order.** `ADR-029` §7's own closing paragraph explicitly allows participant order/eligibility to change over the encounter's lifetime (joins/leaves/skips, per `ADR-029` §2); a purely arithmetic "source's Nth turn is at ordinal X" computation from a fixed participant list would silently break under those changes. Reading the actual `TurnStarted`/`TurnEnded` audit rows for the specific bound `CharacterId`, filtered by a captured `AppliedLifecycleEventId` high-water-mark, is robust to that and is exactly the kind of "additional audit-log data, justified" reading the governing ТЗ's own section 4 anticipated. `ForRounds` needed no such read -- its own boundary (`AppliedRoundOrdinal + N + 1`) is pure arithmetic against the encounter's own live, monotonic `RoundOrdinal`.
- **`ExpireActiveEffect`'s existing signature was sufficient; no repository interface change was needed at all**, not even a backward-compatible extension. `CombatEffectExpiryService` already has the `ActiveEffectId`/`Revision`/`CampaignId` it needs from the candidate `ActiveEffectRecord` it was handed.
- **The real caller (`CombatEffectExpiryService.EvaluateAndExpireIfDue`) is genuinely complete and tested, but its own automatic trigger point is deliberately not wired.** The governing ТЗ's own section 2 explicitly allows this ("caller may be minimal/stub ... main thing is that the boundary-check functions themselves are complete, tested, reusable"); this task chose to make the caller itself fully real (not a stub) since a half-real caller would likely draw the same kind of independent-review finding `ODY-S05-604`'s own delta-commit gap did -- but deciding *when* (e.g. every `Advance`, or lazily when effects are listed) is a design question for whichever future task first needs the automatic behavior, not decided here.
- **Fail-closed for "bound combatant no longer in the encounter" is proven with a combatant who was never a participant of that encounter**, not by removing one mid-encounter -- no participant-removal command exists anywhere in this codebase today, so a never-a-participant binding is the closest faithful stand-in for that scenario without inventing new production behavior to test with.
- **Self-caught persistence gap, fixed before review: `CombatDurationBinding` did not originally survive a real `SqliteActiveEffectRepository` round-trip.** Adding the field to the Domain `ActiveEffect` type alone was not sufficient -- `ActiveEffect`'s own SQLite table/`InsertColumns`/`SelectColumns`/`AddParameters`/`ReadRecord` had no columns for it, so a fresh `GetActiveEffect` after `CreateActiveEffect` would have silently returned `CombatBinding = null`, discarding the very data this task exists to add. Six new nullable columns (`CombatEncounterId`, `CombatSourceCombatantId`, `CombatTargetCombatantId`, `CombatAppliedRoundOrdinal`, `CombatAppliedLifecycleEventId`, `CombatRequiredCount`) were added to the existing `ActiveEffect` table and its shared insert/select/read helpers (all of `CreateActiveEffect`/`ExpireActiveEffect`/`SetItemEffectEquipped`/`RemoveActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource` route through these same shared helpers, so one edit covers every call site). `TC-ATTACK-046` proves the round-trip directly (fresh `GetActiveEffect`, not the in-memory `CreateActiveEffect` return value). This is the same category of gap `ODY-S05-604`'s own independent review caught (a Domain-level type that looks complete but does not actually persist/round-trip) -- caught and fixed here before submission rather than left for review to find.

### Blockers

- None.
