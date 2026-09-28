# ExecPlan — ODY-S10-102 Stored MainGM Check in the Characters Subsystem

## 1. Purpose
Replace the client-supplied `actorIsMainGm` flag in the characters subsystem with the stored campaign-membership check, preserving the four authorization rule shapes.

## 2. Scope
`SqliteCharacterRepository.cs`, `CharacterRepositoryContracts.cs`, `CharacterAdvancementService.cs` (pass-through only), the character-related and constructor-dependent tests, the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
Combat/content/dice-inventory subsystems, board movement, membership entity/predicate changes, `CharacterOwnership`, Unity client, ADRs.

## 4. Architecture
`ICampaignRepository` injected into `SqliteCharacterRepository`; every MainGM decision goes through `CampaignMembershipAuthorization.IsMainGm(_campaignRepository, campaign, actorUserId, correlationId)`; a lookup failure is returned as-is. Owner-or-MainGM points check ownership first and look up the role only for non-owners. Seven no-actor operations gain `actorUserId`.

## 5. Milestones
1. Comment-only doc fix (separate commit). 2. Constructor dependency and signature migration. 3. The 24 checks. 4. Constructor/call-site migration of tests. 5. New tests `TC-PERSIST-041`–`049`. 6. Catalogue/backlog/contract/plan. 7. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full, harnesses built); `verify-format`/`verify-repository`/`verify-test-structure`; a deliberate mutation of a gate to prove the new tests bite; `git diff --name-status origin/main` reviewed against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR; no schema or data change.
