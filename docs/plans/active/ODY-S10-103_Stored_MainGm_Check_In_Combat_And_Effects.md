# ExecPlan — ODY-S10-103 Stored MainGM Check in the Combat/Effects Subsystem

## 1. Purpose
Replace the client-supplied `actorIsMainGm` flag at the seven strictly-MainGM-only points of the combat/effects subsystem with the stored campaign-membership check, without breaking the internal rollback of failed ability activations and item uses.

## 2. Scope
The four named production files plus the contracts/wrapper, the rollback callers, the tests, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Alternative access paths, characters/board/content/dice-inventory, the three controller-or-MainGM Application services (recorded as MG-3b), Unity client, ADRs.

## 4. Architecture
Application services receive an `ICampaignRepository` parameter; Persistence classes receive it as a required constructor argument; all decisions go through `CampaignMembershipAuthorization.IsMainGm` and fail closed. The compensation of failed activations/uses uses a separate `RemoveActiveEffectAsSystemRollback` (no role parameter) sharing one core with the gated `RemoveActiveEffect`.

## 5. Milestones
1. Production migration of the seven points and the rollback split. 2. Mechanical migration of constructors and call sites in tests. 3. Test doubles. 4. New tests `TC-PERSIST-051`–`058` and a mutation check. 5. Catalogue, backlog, contract, plan. 6. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full, harnesses built); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the Player-rollback test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR; no schema or data change.
