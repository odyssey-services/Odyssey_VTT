# ExecPlan — ODY-S09-101 Character Advancement Service + Light Method Decoupling

## 1. Purpose
Create the `SLICE-09` backlog and implement its first block: an Application-layer `CharacterAdvancementService` and the decoupling of `InitializeCharacterResource` / `InitializeCharacterAnatomy` from `Odyssey.Rules`.

## 2. Scope
`SLICE-09_IMPLEMENTATION_BACKLOG.md`; `CharacterAdvancementService`; the two `ICharacterRepository` signatures and their SQLite implementations; migration of 46 test call sites; tests `TC-CHAR-181`-`188`; `test-catalog.json`; this contract and plan.

## 3. Non-goals
The other methods with Rules references; architecture-check tightening; asmdef/csproj changes; ADR changes.

## 4. Architecture
Service in `Odyssey.Application` reads Rules defaults (legal: Application may use Rules) and calls the port with plain values. Persistence receives values and decides nothing. Form follows `CheckService`.

## 5. Milestones
1. Golden before-snapshot of both operations. 2. Service, port and repository change. 3. Migrate tests. 4. New tests and catalog. 5. After-snapshot diff. 6. Backlog, contract, plan. 7. Full validation, PR.

## 6. State and data flow
Caller -> `CharacterAdvancementService.Initialize*WithDefaults` -> `ICharacterRepository.Initialize*` -> SQLite write and journal, unchanged.

## 7. Error handling
Argument guards in the repository, before DB access; existing `Result` errors unchanged.

## 8. Test strategy
Literal-value and Rules-constant equality; caller-value pass-through; guard tests; recording decorator; reflection assembly check; existing tests migrated.

## 9. Validation and acceptance evidence
Snapshot diff 0 differences; `dotnet test` green; verify scripts PASS; grep of the two methods vs. the rest; diff scope.

## 10. Recovery and rollback
Revert the PR; no data migration involved.
