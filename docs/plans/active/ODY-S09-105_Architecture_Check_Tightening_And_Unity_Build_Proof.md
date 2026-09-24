# ExecPlan — ODY-S09-105 Architecture-Check Tightening + Unity Build Proof

## 1. Purpose
Make the `dotnet build` that CI already runs see the same reference closure a Unity asmdef sees, guarantee that setting cannot be removed silently, and prove that the original defect (Unity client does not compile) is gone.

## 2. Scope
`DisableTransitiveProjectReferences` on six production projects; a guard and self-test in `verify-test-structure.ps1`; backlog closure; this contract and plan; three recorded proofs.

## 3. Non-goals
CI policy (`verify-ci.ps1`), Unity in CI, `.asmdef` files, ADRs, any C# source, moving finished task files.

## 4. Architecture
No new mechanism: the MSBuild property does the enforcing inside the existing build step. The script only guards that the property stays set, on all six projects.

## 5. Milestones
1. Property on all six projects; strict build and tests. 2. Temporary violation: failure shown, then removed. 3. Script guard, self-test, real-repo removal checks for Persistence and Networking. 4. Unity batchmode run on a fresh `Library`; undo its working-tree side effects. 5. Backlog closure, contract, plan. 6. Full validation, PR.

## 6. State and data flow
None (build configuration only).

## 7. Error handling
A forbidden dependency now fails `dotnet build` with `CS0234`/`CS0246`; a removed property fails `verify-test-structure.ps1` with a message naming the project and the property.

## 8. Test strategy
Direct demonstration instead of new unit tests: strict build, temporary violation caught, guard caught, Unity run; the script's own controlled-invalid fixture.

## 9. Validation and acceptance evidence
See the contract, section 9.

## 10. Recovery and rollback
Revert the PR; nothing is persisted.
