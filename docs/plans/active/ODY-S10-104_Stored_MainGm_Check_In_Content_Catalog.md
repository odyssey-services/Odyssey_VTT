# ExecPlan — ODY-S10-104 Stored MainGM Check in the Content Catalog Subsystem

## 1. Purpose
Replace the client-supplied `actorIsMainGm` flag at the seven strictly-MainGM-only points of the content catalog subsystem with the stored campaign-membership check, leaving the two catalog read operations exactly as unauthenticated as before.

## 2. Scope
The two named contract files, the 12 named test files (2 primary suites with new tests, 10 mechanical-only), the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Reads (`GetContentDefinition`/`ListContentDefinitions`); the Persistence-layer repository/validation service; characters/board/combat-effects; Dice/Inventory (reserved for `ODY-S10-105`); Unity client; ADRs.

## 4. Architecture
Application services receive an `ICampaignRepository` parameter, the same shape as `BoardMovementService.MoveToken` (`ODY-S10-101`) — no Persistence-layer constructor change was needed since this subsystem's seven points are all Application-layer already. All decisions go through `CampaignMembershipAuthorization.IsMainGm` and fail closed. Three request types gain a new `UserId ActorUserId` in place of the removed flag; no internal/system-caller bypass exists or was introduced.

## 5. Milestones
1. Production migration of the seven points (two contract files). 2. Mechanical migration of constructors and call sites in the two primary test suites. 3. Test doubles (`PoisonedMembershipCampaignRepository`). 4. New tests `TC-PERSIST-060`–`064` and a mutation check. 5. Mechanical-only migration of the ten fixture test files (with a scoping check against the unrelated Dice/Inventory `actorIsMainGm`). 6. Catalogue, backlog, contract, plan. 7. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full, harnesses built); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving `TC-PERSIST-060` bites; `git diff --name-status origin/main` reviewed against the forbidden-path list, including a targeted grep for `actorIsMainGm`/`ActorIsMainGm` outside the seven catalog request types to confirm no Dice/Inventory contamination survived.

## 10. Recovery and rollback
Revert the PR; no schema or data change.
