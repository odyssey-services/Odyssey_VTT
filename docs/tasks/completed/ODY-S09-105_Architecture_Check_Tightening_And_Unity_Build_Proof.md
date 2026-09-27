# ODY-S09-105 — Architecture-Check Tightening + Proof That Unity Builds (SLICE-09 block 5, last)

## 1. Task identity
`ODY-S09-105`; status: Done (PR #178, merged into main). Block 5 -- the last -- of `SLICE-09` (`docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md`); follows `ODY-S09-104` (block 4, merged).

## 2. Goal
Close the hole that let the `Persistence -> Rules` violation live for 22 days: `dotnet build` resolves project references transitively, a Unity asmdef does not, and the repository's structure check compared only the declared graphs. Make the `dotnet build` that CI already runs see the reference closure Unity sees, make sure that setting cannot be dropped silently, and prove -- not argue -- that the original problem ("the Unity client does not compile") is gone.

## 3. Authority
This task's governing ТЗ §0-§7; `docs/research/Unity_Rules_Asmdef_Break_Investigation.md` (PR #173: the mechanism, experiment E3, and the recommendation to set `DisableTransitiveProjectReferences` on the production projects); `ADR-001` §5 (read, not revised).

## 4. In scope
- `<DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>` in all six `DotNet/Projects/*.csproj`.
- `scripts/verify-test-structure.ps1`: a guard requiring the property on all six, plus a controlled-invalid self-test (§1.3 of the ТЗ allows this if justified; see §18).
- The three proofs (§9): the strict build, the regression catch, the Unity batchmode build.
- `SLICE-09_IMPLEMENTATION_BACKLOG.md`: block 5 row, the four merged blocks marked Done with their PR numbers, track status.
- This contract and plan.

## 5. Out of scope
`verify-ci.ps1` and the policy that CI does not run Unity; every `.asmdef`; every ADR; every already-decoupled method; `SLICE-05/06/07/08` backlogs; embedding the Unity build in CI; moving the finished `ODY-S09-10x` task files to `completed/` (proposed as a follow-up, §18).

## 6. Domain contract
Unchanged.

## 7. Application contract
Unchanged.

## 8. Persistence boundary
Unchanged (no C# source is modified by this task).

## 9. Tests and validation (the evidence)
1. **Strict build.** With the property on all six production projects, `dotnet build DotNet/Odyssey.Core.sln` (production and test projects): 0 errors, 0 warnings. `dotnet test` green: Persistence 842/842, Unit 179, Domain 90, Networking 67, Architecture 10, Contracts 1. This is the direct proof that blocks 1-4 left no place where a project relies on transitive access.
2. **Regression catch.** A temporary file in `Packages/com.odyssey.persistence/Runtime/Sqlite/` using `Odyssey.Rules.Character.AbilityCostRules.CostPerAbility` (removed afterwards; not in the diff):
   - with the property on: `dotnet build DotNet/Projects/Odyssey.Persistence.csproj` fails with 1 error: `TempRegressionDemo.cs(5,40): error CS0234: The type or namespace name 'Rules' does not exist in the namespace 'Odyssey' (are you missing an assembly reference?)`;
   - the same code with `-p:DisableTransitiveProjectReferences=false` (i.e. the situation before this task): builds with 0 errors -- the hole, shown.
3. **Guard regression catch.** With the property deleted from `Odyssey.Persistence.csproj` (or `Odyssey.Networking.csproj`) in the working tree, `verify-test-structure.ps1` exits 1 with `Odyssey.Persistence.csproj must set <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences> so that dotnet build sees the same reference closure as Unity (ADR-001 section 5, ODY-S09-105).`; restored, it exits 0. The self-test `TC-ARCH-002 PASS controlled removal of DisableTransitiveProjectReferences rejected` is part of the script's own run.
4. **Unity.** Unity 6000.4.0f1 (8cf496087c8f), `-batchmode -nographics -runTests -testPlatform EditMode` on a fresh `Library` in a clean worktree of this branch: process exit code 0; results `total=70 passed=70 failed=0`; no compile error in any Odyssey assembly (`Odyssey.Rules`, `Odyssey.Application`, `Odyssey.Persistence`, `Odyssey.Unity.Client` all compiled). Log noted below (§18, findings). This is the first successful Unity build of the client since `ODY-S04-105` (PR #89), 22 days and 87 merge commits earlier.

## 10. Compatibility and rollback
The property changes what `dotnet build` accepts, not what it produces: no source or reference changed and the whole solution still builds. Rollback: revert the PR (the six one-line additions and the script guard).

## 11. Security and privacy
No change.

## 12. Observability
No change.

## 13. Performance
No effect on runtime; build resolution is marginally cheaper.

## 14. Dependencies
`ODY-S09-104` (the property can only land after the last real violation is gone; before block 4 it would have turned CI red).

## 15. Dependencies (packages)
None.

## 16. Implementation plan
`docs/plans/active/ODY-S09-105_Architecture_Check_Tightening_And_Unity_Build_Proof.md`.

## 17. Completion evidence
§9. `git diff --name-status` against `main` (`94ee43c`): six `.csproj`, `scripts/verify-test-structure.ps1`, the backlog, this contract and plan.

## 18. Change control

### Decisions made during execution
- **All six production projects, not only Persistence** (ТЗ §1.1 asked for a justified choice). The investigation's experiment E3 already showed that under the property Domain, Rules, Content, Application and Networking build clean and only Persistence failed; this task re-verified it on the current tree (0 errors). The property costs one line per project, the projects it protects are exactly the ones whose ADR-001 §5 rows forbid dependencies, and a future regression in any of them (for example Content leaking into Domain through Application) would otherwise be invisible to `dotnet` in exactly the same way. The test projects are deliberately not covered: they reference several production projects on purpose.
- **`DisableTransitiveProjectReferences` and not something newer.** It was already verified by the investigation, needs no new tooling, and rides on the `dotnet build` step of the existing `dotnet-restore-build-test` job -- so no new CI job or script is needed to catch a regression in the code itself (ТЗ §1.3).
- **The script still had to change, for a reason the ТЗ did not anticipate.** Without a guard the property can be deleted by any later edit and nothing notices. And the script's existing project check (`Test-BridgeProject`) covers only `Domain`, `Rules`, `Content` and `Application`: **`Persistence.csproj` and `Networking.csproj` had no csproj check at all** (their ProjectReference lists, package references and compile includes are not verified by `verify-test-structure.ps1`). The new guard therefore has two call sites: inside `Test-BridgeProject` for the four bridge modules, and a separate loop for the two others (skipped only if the file is absent, which is the case in the script's synthetic fixture). The wider gap is reported, not fixed here.

### Findings (reported, not fixed -- out of scope)
- **Persistence/Networking csproj are unchecked by the script** (above). Verifying their reference lists, package references and compile includes against ADR-001 would be a separate small task.
- **First compile pass of Unity on a fresh `Library` logs 15 `error CS0246: 'GUID'` lines, all in `Library/PackageCache` (HDRP / ShaderGraph editor code), none in project code.** The very same log then shows `Tundra build success` on the next passes and the test run completes 70/70 with exit code 0. The investigation reported the same 15 lines only for runs in which project scripts failed to compile and not in its successful run E2; here they appear in an otherwise successful run. This looks like a package-import ordering artifact of a fresh `Library` for HDRP/ShaderGraph and is not root-caused. Nothing in this task depends on it; it is recorded because a reader of the raw log would otherwise see `error CS` and not know it is unrelated.
- **Unity leaves side effects in the working tree** (untracked `.meta` files for every package source file, two deleted `.meta` files, a modified `ProjectSettings/ShaderGraphSettings.asset`). They were reverted/removed before commit and are not part of the diff. Whether the untracked `.meta` files should be ignored by the repository is a repository-hygiene question for the product owner.
- **Housekeeping (proposed, not done).** `docs/tasks/active/` and `docs/plans/active/` still hold the `ODY-S09-101`-`104` contracts (statuses still "In Review" though merged) together with the still-stale `ODY-S07-103`-`106` and `ODY-S08-101` files. The precedent is PR #167 (sync statuses to Done and move to `completed/`). Doing it for all of them in one separate housekeeping PR seemed better than moving only this track's files here; the ТЗ allowed either.
- **Recommendation for `ADR-001` (not edited).** §5 states the allowed graph but not how it is enforced. A one-line addition -- "production `.csproj` files set `DisableTransitiveProjectReferences` so that `dotnet build` and Unity see the same reference closure; `verify-test-structure.ps1` enforces the property" -- would make this mechanism a documented part of the standard. Left to the product owner, per the ТЗ.

### Blockers
None.
