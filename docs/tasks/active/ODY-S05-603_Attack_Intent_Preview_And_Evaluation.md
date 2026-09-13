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
Register TC-ATTACK-001–022. Before implementation all validation is **Not run**.

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
Documentation-first stage only; implementation evidence is **Not run**.

## 18. Change control
Any missing Ruleset formula is represented by a fake/test Rule implementation, never invented in production.
