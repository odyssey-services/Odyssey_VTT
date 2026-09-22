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
Focused NUnit: 141/141 passed. Full `dotnet test DotNet\\Odyssey.Core.sln --no-restore` completed with all reported assemblies green; run repository gates after final formatting before opening the Draft PR.

## 10. Recovery and rollback
No persisted task-owned data; revert source only.

## Amendment — 2026-09-14

Changed production files: `AttackPipelineContracts.cs`, `AttackEvaluationService.cs`, and `SqliteAttackStateReader.cs`; tests: `AttackEvaluationServiceTests.cs`, `AttackScopeTests.cs`, and the test catalog. The reader is composition-only and invokes no write path. `ItemInstanceId` is the MVP source decision: its current runtime row must belong to the acting Character and exposes the exact pinned mechanics snapshot. Validation after amendment begins with Unit 143/143 and Architecture 5/5; full repository gates are run before push.

## Amendment — complete read-only evaluation inputs (2026-09-14)

The reader obtains compact `AttackParticipantState` values from current Character read models and fingerprints them with encounter and item snapshot state. There is no authoritative scene/topology or narrow armor/effect binding in the current runtime, so both are represented as explicit unavailable inputs. This is deliberately not a fallback formula or an empty modifier list.

## Amendment — closeout (2026-09-14)

`AttackParticipantState` carries only `CharacterId`/`LifecycleStatus`/`ApprovalState`; a `RulesetVersion` field sourced from `CharacterRecord` was removed as out of scope (see the task contract's RulesetVersion decision record). `SqliteAttackStateReaderTests.cs` replaces source-path references for `TC-ATTACK-016`/`017` with real temporary-SQLite tests, adds `TC-ATTACK-023`/`024`/`025`/`026`/`028`, and `AttackScopeTests.cs` adds `TC-ATTACK-027`. `TC-ATTACK-022`'s test now reflects over `AttackIntent`'s public surface. Full validation (`dotnet build`/`dotnet test`, format/policy/test-structure scripts) re-run before this amendment commit.
