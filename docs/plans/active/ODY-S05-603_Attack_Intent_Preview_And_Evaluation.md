# ExecPlan — ODY-S05-603 Attack Intent, Preview, and Evaluation

## 1. Purpose
Build ADR-029 stages 1–11 as a host-only proposed resolution.

## 2. Scope
Domain/Rules/Application contracts and tests; no durable application.

## 3. Non-goals
No pending state, mutations, events, Game Log, transport, UI, or combat formulas.

## 4. Architecture
Application authorizes then reads snapshots; Rules evaluates supplied snapshots/sample; result remains immutable in memory.

## 5. Milestones
M1 documentation gate; M2 inspect existing contracts; M3 minimal contracts/services; M4 real tests; M5 validation/PR.

## 6. State and data flow
Intent → authorization → read snapshot → preview or deterministic RNG sample → pure Rules → proposed resolution.

## 7. Error handling
Typed Result failures preserve correlation ID; no write-side recovery path exists.

## 8. Test strategy
Use fake reader, Rules, and RNG for ordering/no-write proof; use real encounter read where persistence claims require it.

## 9. Validation and acceptance evidence
Not run at documentation-first stage.

## 10. Recovery and rollback
No persisted task-owned data; revert source only.
