# ExecPlan — ODY-S05-611 Combat Duration Expiry Automatic Trigger Wiring

## 1. Purpose
Give `605`'s own `CombatEffectExpiryService.EvaluateAndExpireIfDue` a real, automatic production call site, so a combat-duration-bound `ActiveEffect` genuinely expires as encounters are played -- closing the gap `605` disclosed, `606` did not pick up, and `608` reconfirmed still open. Final task of the full attack pipeline block.

## 2. Scope
Wiring inside `SqliteCombatEncounterRepository.Advance` (not the thin `CombatEncounterService.Advance` wrapper) that, after the round/turn-advance transaction commits, enumerates current participants' combat-bound `Active` effects and calls the existing, unmodified `CombatEffectExpiryService.EvaluateAndExpireIfDue`. Two new optional constructor parameters with real defaults; tests.

## 3. Non-goals
No change to `CombatEffectExpiryService`/`ActiveEffectExpiryRules` (605) or `ExpireActiveEffect`'s signature/connection strategy (502); no `606`/`607`/`609`/`610` territory; no batching/caching/scheduler; no new event bus; no widened `ICombatEncounterRepository.Advance` public contract.

## 4. Architecture
`SqliteCombatEncounterRepository` gains `IActiveEffectRepository`/`ICombatEncounterLifecycleReader` as optional constructor parameters (defaulting to real `SqliteActiveEffectRepository`/`SqliteCombatEncounterLifecycleReader(this)` instances) so all 9 existing single-argument call sites keep compiling. `Advance` commits its own existing transaction unchanged, then calls a new private `ExpireDueCombatEffects(campaign, result, correlationId)`: for each current participant, `ListActiveEffectsByTarget`, filter to `Active && CombatBinding.HasValue && CombatBinding.EncounterId == this encounter`, decode `EffectDurationType` via `TypedDefinitionCodec.DecodeEffect` (the same source `ItemEffectLifecycleService`/505 already uses), skip non-combat-duration types, and call `EvaluateAndExpireIfDue` with a freshly-synthesized `CommandId` per candidate.

## 5. Milestones
M1 read `CombatEffectExpiryService.cs`/`SqliteCombatEncounterRepository.cs`/`SqliteActiveEffectRepository.ExpireActiveEffect`/`ItemEffectLifecycleService.cs`'s own `TypedDefinitionCodec.DecodeEffect` usage verbatim, confirm the single call site and connection-strategy facts the governing ТЗ asserted; M2 add the two optional constructor parameters; M3 implement `ExpireDueCombatEffects` and wire it into `Advance` after commit; M4 real tests (`TC-ATTACK-093`-`100`); M5 fix the one pre-existing architecture test this wiring genuinely conflicts with; M6 validation/PR.

## 6. State and data flow
A MainGM-authorized `Advance` call commits its own round/turn-advance transaction as before (unchanged) -> `ExpireDueCombatEffects` reads the now-current encounter's own participant list -> for each participant, lists `ActiveEffect` rows and filters to combat-bound, currently-`Active`, this-encounter candidates -> decodes each candidate's own `EffectDurationType` from its pinned mechanics snapshot -> for a recognized combat duration type, calls `EvaluateAndExpireIfDue`, which itself (unmodified) decides Expired/NotExpired and, only if Expired, calls `ExpireActiveEffect` in its own separate transaction.

## 7. Error handling
A read, decode, or expiry failure for any one candidate is skipped (continue to the next), never thrown -- `Advance`'s own round/turn-advance commit has already happened by the time this code runs and is never rolled back by a downstream expiry failure. A fresh `CommandId` per candidate avoids `SqliteSavingPipeline`'s own replay mechanism silently skipping a second candidate's real expiry (a genuine correctness risk identified during implementation, not assumed).

## 8. Test strategy
Real SQLite fixtures (temp-directory campaign, real `SqliteCombatEncounterRepository`/`SqliteActiveEffectRepository`/`SqliteCharacterRepository`) constructing real `ActiveEffect` rows via `CreateActiveEffect` with real `TypedDefinitionCodec.EncodeEffect`-produced mechanics payloads: a real `Advance` call (not a direct service call) expires a due `ForRounds` effect and leaves a not-yet-due one `Active`; non-combat and already-terminal effects are untouched; the round/turn advance commits and a sibling candidate still expires despite one candidate's own processing failure (a malformed payload); multiple due candidates across participants are all handled; an effect on a character outside the current roster is left alone (documented gap); the pre-existing MainGM gate is unaffected; both a round-boundary (`ForRounds`) and turn-boundary (`UntilTargetTurnStart`) duration type are evaluated by the same wiring.

## 9. Validation and acceptance evidence
`dotnet build`/`dotnet test` full solution green (1009 tests: 1001 pre-existing + 8 new); `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` green; `git diff --name-status` against `main` confined to the ТЗ's allowed paths (plus the one necessarily-updated pre-existing architecture test), no `606`/`607`/`609`/`610` territory or ADR documents touched.

## 10. Recovery and rollback
No schema change; `SqliteCombatEncounterRepository`'s constructor gains two backward-compatible optional parameters; `ICombatEncounterRepository.Advance`'s own public contract is unchanged. Reverting this task's source leaves `602`-`610` and all prior campaign state unaffected -- combat-bound effects simply stop auto-expiring again, returning to the pre-`611` (already-accepted, if incomplete) behavior.
