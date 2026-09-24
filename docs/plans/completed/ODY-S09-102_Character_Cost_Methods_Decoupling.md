# ExecPlan — ODY-S09-102 Character Cost Methods Decoupling

## 1. Purpose
Move legality and cost decisions of `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RequestSkillAdvancedRecommendation` and `AcquireAbility` (ProgressionPurchase) from `SqliteCharacterRepository` into `CharacterAdvancementService`, closing the resulting read-then-commit race.

## 2. Scope
Four service methods; four port/repository signatures; migration of 82 test call sites; tests `TC-CHAR-189`-`202`; `test-catalog.json`; backlog status; this contract and plan.

## 3. Non-goals
Respec, ruleset migration, architecture-check tightening, block-1 code, any formula or cap change, `.asmdef` / `.csproj`, ADRs.

## 4. Architecture
Service: guards -> `GetCharacter` -> Rules (cap / recommendation / cost from the current value) -> repository. Repository: existing pipeline and gates; the Rules calls are replaced by the passed values; a re-check under the lock that the value the decision was based on is unchanged, else `CharacterRevisionConflict`. The ability path needs no state, only a passed constant cost.

## 5. Milestones
1. Inventory of the four methods and their failure order. 2. Signatures and repository change. 3. Service methods. 4. Mechanical test migration and decorator updates. 5. Full test run (813 old tests unchanged). 6. New tests, catalog, mutation check of the race tests. 7. Docs, validation scripts, PR.

## 6. State and data flow
Caller -> service (guards, read, Rules) -> repository transaction (gate, permission, ordered failures, write, journal) unchanged after the decision values are supplied.

## 7. Error handling
Same typed failures in the same order as before; stale decision -> `CharacterRevisionConflict`; invalid arguments throw in the service before any read; a failed read returns its own failure.

## 8. Test strategy
Literal numbers; full-state snapshots around every rejected command; a direct stale-decision test with current revisions; an injected race between the service's read and commit; an interception double proving port-only use; mutation check of the race tests.

## 9. Validation and acceptance evidence
`dotnet test` green; verify scripts PASS; grep of the four methods; diff scope; the race tests fail with the re-check disabled.

## 10. Recovery and rollback
Revert the PR; no data migration.
