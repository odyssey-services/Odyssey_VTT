# ExecPlan — ODY-S10-106 Stored MainGM/Owner Check in Attack Evaluation, Ability Activation and Item Use

## 1. Purpose
Replace the client-supplied `ActorIsMainGm` flag at the three remaining owner-or-MainGM gates with the stored campaign-membership check, closing the whole `ActorIsMainGm` pattern found across the `ODY-S10-101`–`105` sub-track (except the deferred `RedactCharacterForExport.cs` question).

## 2. Scope
Three production files (plus `AttackApplyService.cs`'s pass-through parameter), ~13 test files (3 named production suites with new tests, ~10 mechanical-only), the catalogue, the `SLICE-10` backlog, this plan and the contract.

## 3. Non-goals
`CanControlActor`; the system-rollback compensation path; the already-closed `ODY-S10-103` combat points; `CharacterOwnershipAssignment.IsAssignedCharacter`; `RedactCharacterForExport.cs`; any client file (confirmed by recon to be unnecessary).

## 4. Architecture
`ICampaignRepository` is a method parameter on all three static Application-layer services (the `DiceRollService` shape), threaded to the one `Authorize`/`AuthorizeAndRead` helper in each that decides via `CampaignMembershipAuthorization.IsMainGm`, fail closed, falling back to the untouched `CanControlActor` only when the lookup says "not MainGm" — the exact order each method already had.

## 5. Milestones
1. Recon re-verification (no real client/server caller exists for any of the three points). 2. Production migration of the three points plus `AttackApplyService`'s pass-through. 3. Mechanical migration of ~13 test files (3 primary with new tests, ~10 mechanical-only). 4. New tests `TC-ATTACK-127`–`129`/`TC-ABILITY-015`–`016`/`TC-USEITEM-013`–`014` and a mutation check. 5. Catalogue, backlog, contract, plan. 6. Full validation and Draft PR.

## 6-8. State/flow, error handling, test strategy
See task contract §9 and §18.

## 9. Validation and acceptance evidence
`dotnet test` (full: Unit 186/186, Persistence 887/887); `verify-format`/`verify-repository`/`verify-test-structure`; a mutation proving the new attack test bites; `git diff --name-status origin/main` reviewed against the forbidden-path list; confirmation that the existing `ODY-S10-103` owner-path regression tests still pass unchanged.

## 10. Recovery and rollback
Revert the PR; no schema or data change.
