# ODY-S05-603 — Attack Intent, Preview, and Evaluation

## 1. Task identity
`ODY-S05-603`; status: In Progress.

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
Implemented immutable Domain proposal contracts, pure Rules seam, read-only Application state seam, authorization/current-turn preconditions, and deterministic host-RNG derivation. No persistence adapter or mutation entry point was added.

## 18. Change control
Any missing Ruleset formula is represented by a fake/test Rule implementation, never invented in production.

## Amendment — authoritative reads and complete proposal (2026-09-14)

`AttackIntent` now accepts only `ItemInstanceId`, not a client-supplied generic definition ref. `SqliteAttackStateReader` composes the existing encounter, inventory and character read ports: it requires the current Open/TurnOpen encounter, exact expected revision, current actor, encounter targets, campaign-owned item and `InventoryOwnerRef.ForCharacter(actor)`. The source mechanics are the item's already-pinned `ItemMechanicsSnapshot`; no latest catalog record is read.

The proposed resolution now carries typed range, modifiers, sample-presence, hit, body-part, armor, damage, cost and effect-candidate results. All remain in-memory proposals. TC-ATTACK-001–022 are registered; focused evidence: Unit 143/143 and Architecture 5/5 passed. No attack table, write SQL, ledger, event, Game Log, ActiveEffect, UI or networking surface was added.
