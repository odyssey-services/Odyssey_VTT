# ExecPlan — ODY-S10-105 Stored MainGM Check in the Dice/Inventory Subsystem

## 1. Purpose
Replace the client-supplied `actorIsMainGm` flag at the four strictly-MainGM-only dice points and the eight strictly-MainGM-only inventory points with the stored campaign-membership check, closing the five-subsystem sub-track this ТЗ names as its final step.

## 2. Scope
Two dice contract/service files, four inventory contract/service files, the Persistence repository and its interface, the one real client caller and its only production caller, ~24 test files (10 named with real new coverage, ~14 mechanical-only), the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
`SubmitRoll`; `ConsumeItemUnit` gaining its own gate; an "owner acts on their own item" path; `MG-3b` (`UseItemService`/`ActivateAbilityService`/`AttackEvaluationService`); `RedactCharacterForExport.cs`; characters/board/combat-effects/content-catalog; Unity client beyond the one real caller; ADRs.

## 4. Architecture
Application services receive an `ICampaignRepository` parameter (the `ODY-S10-101` shape); the Persistence repository receives it as a required constructor argument (the `ODY-S10-102` shape). All decisions go through `CampaignMembershipAuthorization.IsMainGm` and fail closed. `RequestFullReroll`/`CancelRoll` keep their pre-existing "own roll first" fast path, now backed by a real lookup only on the path that needs one. Six inventory request types gain a real `ActorUserId` in place of a validated-but-discarded constructor argument. The one real production client caller (`RollPanelPresenter.cs`) is rewired to carry a real `CampaignHandle`/`ICampaignRepository` instead of a bare id, sourced from its only caller's already-existing demo-campaign handle.

## 5. Milestones
1. Dice contracts/service migration (4 points) with fast-path order preserved. 2. Inventory contracts/service migration (8 points, 6 new `ActorUserId` properties). 3. `SqliteInventoryRepository` constructor + `ApplyItemDefinitionMigration` migration; `ConsumeItemUnit` dead-parameter removal. 4. Client wiring (`RollPanelPresenter.cs`/`TrialScreenPresenter.cs` + 2 Unity tests, scope expansion approved after the ТЗ's own flagged recon finding). 5. Mechanical migration of ~14 test files outside the named list, with a scoping check against `MG-3b`'s identically-shaped flag. 6. New tests `TC-DICE-024`–`028`/`TC-INVENTORY-197`–`200` and a mutation check. 7. Catalogue, backlog, contract, plan. 8. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 183/183, Persistence 883/883); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving `TC-DICE-024` bites; `git diff --name-status origin/main` reviewed against the forbidden-path list, including a targeted grep for `actorIsMainGm`/`ActorIsMainGm` to confirm the one caught-and-reverted `MG-3b` scoping mistake did not survive into the final diff.

## 10. Recovery and rollback
Revert the PR; no schema or data change.
