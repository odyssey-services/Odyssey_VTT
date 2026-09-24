# ExecPlan — ODY-S09-104 Ruleset Migration Decoupling

## 1. Purpose
Remove the last `Odyssey.Rules` references from `SqliteCharacterRepository` by moving ruleset-migration plan building into `CharacterAdvancementService` and replacing the hash-based staleness check with a direct comparison of the state the plan was built from.

## 2. Scope
Service methods; port and repository signatures; migration of 25 test call sites and the stale-plan test; tests `TC-CHAR-210`-`216`; `test-catalog.json`; backlog status; this contract and plan.

## 3. Non-goals
The Rules layer, the architecture scripts, `RevertCharacterRulesetMigration`, blocks 1-3, `.asmdef` / `.csproj`, ADRs.

## 4. Architecture
Service: guards -> `GetCharacter` -> `BuildPlan` -> `ApplyCharacterRulesetMigration(flat values, source version, three revisions)`. Repository: MainGM check, unresolved flag, then under the lock a comparison of the locked source version and revisions with the decided ones, then the unchanged write. Preview is service-only.

## 5. Milestones
1. Inventory of the Rules use and of what `PreviewHash` covers. 2. Contract and repository change. 3. Service methods. 4. Mechanical test migration and decorator updates. 5. Full run. 6. New tests, catalog, mutation checks. 7. Docs, validation, PR.

## 6. State and data flow
Caller -> service (one read, plan) -> repository transaction (checks, write, event) unchanged after acceptance.

## 7. Error handling
Same typed failures in the same order; a moved source version or revision -> `CharacterRulesetMigrationStalePlan`; a failed read still reaches the repository with an empty basis so the original failure and order are reproduced.

## 8. Test strategy
Literal plan values; pure-read proof; snapshots around rejections; two injected races; equivalence evidence for what the hash did and did not cover; interception double; mutation checks.

## 9. Validation and acceptance evidence
`dotnet test` green; verify scripts PASS; grep of the persistence package; diff scope; race tests fail with their checks removed.

## 10. Recovery and rollback
Revert the PR; no data migration.
