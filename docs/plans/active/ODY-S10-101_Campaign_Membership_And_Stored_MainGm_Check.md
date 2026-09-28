# ExecPlan — ODY-S10-101 Campaign Membership + Stored MainGM Check

## 1. Purpose
Persist campaign participants with roles, make campaign creation register its creator as MainGM atomically, and authorize non-controller token moves by the stored role instead of a client-supplied flag.

## 2. Scope
`CampaignRepositoryContracts.cs`, `SqliteCampaignRepository.cs`, new `CampaignMembershipAuthorization.cs`, `BoardContracts.cs`, `BoardMovementService.cs`, the client wiring (`BoardScreenPresenter.cs`, `TrialScreenPresenter.cs`), all `CreateCampaignRequest` call sites, tests, catalogue, error-code registry, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
The four other MainGM-flag subsystems, participant UI, role change/removal, `CharacterOwnership`/`ControllerUserId`/`RoleSelection` changes, ADRs.

## 4. Architecture
A `CampaignMembership` table (one row per user per campaign file) behind three `ICampaignRepository` methods; a static predicate `CampaignMembershipAuthorization.IsMainGm` as the single definition of "MainGM"; `Create` writes the host row inside its own pipeline transaction; `BoardMovementService.CheckAuthorization` = controller, else stored MainGm, else denied, lookup failure fails closed.

## 5. Milestones
1. Contracts and storage. 2. Predicate and `Create` bootstrap. 3. Board movement pilot and `MoveTokenRequest` change. 4. Mechanical call-site updates. 5. Client wiring. 6. Test rework and new tests `TC-PERSIST-037`–`040`. 7. Catalogue, registry, backlog, contract, plan. 8. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full); Unity 6000.4.0f1 batchmode EditMode run; `verify-format`/`verify-repository`/`verify-test-structure`; `git diff --name-status origin/main` reviewed line by line against the forbidden-path list.

## 10. Recovery and rollback
Revert the PR. Campaign files created by the new code carry an extra `CampaignMembership` table harmlessly; files created before it are unaffected by the revert.
