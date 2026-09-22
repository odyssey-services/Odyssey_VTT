# ODY-S05-603 — Attack Intent, Preview, and Evaluation

## 1. Task identity
`ODY-S05-603`; status: Done (PR #147, merged into main).

## 2. Goal
Implement ADR-029 stages 1–11 as an immutable host-only proposal, with no durable attack application.

## 3. Authority
ADR-029 §§1, 5, 6, 8–12; ADR-008, ADR-019, ADR-028; completed ODY-S05-602.

## 4. In scope
Intent, preview, authoritative evaluation, supplied-RNG Rules contract, read-only state boundary, and tests.

## 5. Out of scope
Pending/outcome persistence, apply, costs/damage/effect mutation, Game Log, compensation, UI, networking, scheduler, or formulas.

## 6. Domain contract
Typed immutable intent, evaluation snapshot/fingerprint, proposal, modifiers, stage outputs, and effect decisions only.

## 7. Rules contract
Pure stages 3–11 consume supplied snapshots and random sample; they cannot read/write infrastructure or own RNG.

## 8. Application boundary
Authorize before reader/Rules/RNG; preview does not use authoritative RNG; evaluation recomputes live state.

## 9. Persistence boundary
Read-only authoritative state only. No attack table, ledger, event, DTO, or mutation method.

## 10. Tests and validation
The initial focused NUnit suite passes: 141/141. The full .NET solution run passes its reported assemblies; final repository gates remain to be recorded before review.

## 11. Compatibility and rollback
New in-memory contracts only; removing them leaves stored campaign state unchanged.

## 12. Security and privacy
Host-only proposal; no RNG secret, hidden modifier, or client DTO exposure.

## 13. Observability
No Game Log or durable diagnostics introduced.

## 14. Performance
One read snapshot and one deterministic stream per evaluation; no cache or scheduler.

## 15. Dependencies
Existing typed IDs, encounter reader, ownership authorization, runtime snapshots, and authoritative RNG contracts.

## 16. Implementation plan
See the active ExecPlan.

## 17. Completion evidence
Implemented immutable Domain proposal contracts, pure Rules seam, read-only Application state seam, authorization/current-turn preconditions, and deterministic host-RNG derivation. A persistence adapter (`SqliteAttackStateReader`) was added; it is read-only and owns no mutation entry point.

## 18. Change control
Any missing Ruleset formula is represented by a fake/test Rule implementation, never invented in production.

## Amendment — authoritative reads and complete proposal (2026-09-14)

`AttackIntent` now accepts only `ItemInstanceId`, not a client-supplied generic definition ref. `SqliteAttackStateReader` composes the existing encounter, inventory and character read ports: it requires the current Open/TurnOpen encounter, exact expected revision, current actor, encounter targets, campaign-owned item and `InventoryOwnerRef.ForCharacter(actor)`. The source mechanics are the item's already-pinned `ItemMechanicsSnapshot`; no latest catalog record is read.

The proposed resolution now carries typed range, modifiers, sample-presence, hit, body-part, armor, damage, cost and effect-candidate results. All remain in-memory proposals. TC-ATTACK-001–022 are registered; focused evidence: Unit 143/143 and Architecture 5/5 passed. No attack table, write SQL, ledger, event, Game Log, ActiveEffect, UI or networking surface was added.

## Amendment — complete read-only evaluation inputs (2026-09-14)

The snapshot now carries compact actor/target lifecycle and approval state and the exact action mechanics snapshot. Topology and armor/effect inputs use explicit `UnavailableNotBound` values until an existing authoritative binding can supply them; Rules must reject that state safely rather than treating it as an empty valid input. This remains an input-model completion only: no formula, roll, damage, cost or effect execution was added.

## Amendment — RulesetVersion decision, real reader tests, and closeout (ODY-S05-603 closure ТЗ, 2026-09-14)

**RulesetVersion decision (closes the open question).** `AttackEvaluationSnapshot.RulesetVersion` is sourced exclusively from `CombatEncounterRecord.RulesetVersion` (`SqliteAttackStateReader.Read`, unchanged). ADR-029 §§3.3, 4, and 5 require only that a proposed/preview resolution "carry ... the Ruleset version it used" -- none of the three sections say where that version must originate, and none requires cross-checking it against any Character-side value. Direct inspection of `CharacterRecord` (`Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`) found that it does carry its own `RulesetVersion` field (empty string for a bare skeleton created via `CreateCharacter`; a real pinned value only once bound to a campaign via `BindDraftToCampaign`, ADR-023 §6.2) -- so Character-side RulesetVersion is not absent from the domain model, contrary to an earlier assumption, but it is a *different* value with different lifecycle semantics from the encounter's own pinned RulesetVersion, and ADR-029 gives no rule for reconciling the two. Decision: the encounter remains the sole source for this task; Character-side RulesetVersion is not read, compared, or exposed anywhere in the attack evaluation surface. An interim local change (never pushed) added a `RulesetVersion` field to `AttackParticipantState` sourced from `CharacterRecord.RulesetVersion`; it was removed before this amendment specifically because it used Character-side RulesetVersion, which is out of this task's scope -- if a future task needs to reconcile Character-side and encounter-side RulesetVersion, that is a separate, future open question, not decided here.

**Real reader tests.** `TC-ATTACK-016`/`017` previously pointed at the production source file, not a test; `DotNet/Tests/Odyssey.Tests.Persistence/SqliteAttackStateReaderTests.cs` now exists with a real temporary SQLite fixture and covers both checks plus fingerprint sensitivity to encounter/item revision and participant lifecycle (`TC-ATTACK-023`), owner rejection with a SQL-level no-new-row proof (`TC-ATTACK-024`), the `UnavailableNotBound` marker's explicit (non-"valid-empty") semantics (`TC-ATTACK-025`), a whole-database no-mutation proof for `Read` (`TC-ATTACK-026`), and `CanControlActor` ownership behavior (`TC-ATTACK-028`), previously untested. `AttackScopeTests.cs` gained `TC-ATTACK-027`, asserting `SqliteAttackStateReader` is the only `Attack`-named type in the persistence assembly. `TC-ATTACK-022` now reflects over `AttackIntent`'s public properties and asserts the exact allowed set, so the catalog's own "no client-side preview/final/RNG/hit/damage/cost/effect field" claim is checked literally rather than by name alone.

**Housekeeping.** `SqliteEquipmentRepositoryTests.cs`/`SqliteInventoryRepositoryTests.cs`'s type-name scope guards now explicitly allow `SqliteAttackStateReader` (its name legitimately contains the forbidden `"Attack"` fragment they otherwise police); without this the pushed `b24ade5` commit's own CI was red on `dotnet-restore-build-test`. §17 above is corrected: it previously said no persistence adapter was added, which already contradicted the read-only `SqliteAttackStateReader` added by the first amendment.
