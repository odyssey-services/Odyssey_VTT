# ExecPlan — ODY-S09-103 Respec Decoupling

## 1. Purpose
Move the respec plan computation out of `SqliteCharacterRepository` into `CharacterAdvancementService`, so `ComputeRespecPlan`, `PreviewCharacterRespec` and `ApplyCharacterRespec` no longer reference `Odyssey.Rules` in Persistence, and close the read-then-commit race for `ApplyCharacterRespec`.

## 2. Scope
Service methods; port and repository signatures; migration of 9 test call sites; tests `TC-CHAR-203`-`209`; `test-catalog.json`; backlog status; this contract and plan.

## 3. Non-goals
Ruleset migration, architecture-check tightening, block-1/2 code, formula changes, `.asmdef` / `.csproj`, ADRs.

## 4. Architecture
Service: guards -> `GetCharacter` -> `GetAdvancementPurchases` -> plan (Rules for repurchase costs) -> `ApplyCharacterRespec(plan, decidedMechanicsRevision)`. Repository: existing gates, unsupported-kind check, revision-tag check, per-entry check against the locked purchase history, then the unchanged batch write. Preview is service-only.

## 5. Milestones
1. Inventory of the three methods and the only Rules use (repurchase cost). 2. Contract and repository change. 3. Service methods. 4. Mechanical test migration and decorator updates. 5. Full run of the existing suite. 6. New tests, catalog, mutation check. 7. Docs, validation scripts, PR.

## 6. State and data flow
Caller -> service (reads, plan) -> repository transaction (gate, checks, batch write, journal) unchanged after the decided plan is accepted.

## 7. Error handling
Same typed failures in the same order; a stale or inconsistent plan -> `CharacterRevisionConflict`; a failed read still reaches the repository so the original failure and ordering are reproduced.

## 8. Test strategy
Literal numbers on a multi-target plan; preview purity; before/after snapshots around rejected commands; injected real race; interception double; mutation check.

## 9. Validation and acceptance evidence
`dotnet test` green; verify scripts PASS; grep of the respec code; diff scope; race tests fail with the revision check disabled.

## 10. Recovery and rollback
Revert the PR; no data migration.
