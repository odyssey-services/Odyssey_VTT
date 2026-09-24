# Investigation: Unity build break `Odyssey.Persistence` -> `Odyssey.Rules`

**Type:** research report. No production code, `.asmdef`, ADR, test or test-catalog file was changed by this work.
**Investigated commit:** `50701d5` (`main`, merge of PR #172). All file:line references below are at that commit unless another commit is named.
**Date:** 2026-09-24
**Location note:** the repository had no research-notes location (`docs/` contained only `adr`, `errors`, `plans`, `tasks`), so `docs/research/` was created as the task instructed.
**Origin:** reported as a finding by `ODY-S08-101` (`docs/tasks/active/ODY-S08-101_Asset_Content_Read_And_Texture_Rendering.md` section 18; `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` section 6 point 5).

---

## 0. Summary

1. **What is broken.** `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` uses `Odyssey.Rules.Character.*` directly, but `Odyssey.Persistence.asmdef` does not reference `Odyssey.Rules`. `dotnet` hides this through transitive project references; Unity does not, so the Unity project does not compile on `main`.
2. **Since when.** Bisected with real Unity batchmode compiles: `592fd38` (merge of PR #88) compiles clean; `4c34479` (`ODY-S04-105`, merged by PR #89 on 2026-09-02, merge commit `8af347b`) fails. The break is 22 days and 87 merge commits on `main` old.
3. **The asmdef is right and the code is wrong.** `ADR-001` explicitly forbids `Persistence -> Rules` (section 5 matrix) and says any dependency absent from its graph "запрещена до принятия нового ADR". The asmdef has matched the ADR since it was created on 2026-08-10. `ADR-024`/`ADR-025` additionally say Persistence "does not decide whether a purchase ... is legal"; the code does exactly that.
4. **Scope.** Exactly one file and one assembly pair (compiler-verified, not just scanned). But it is not only internal calls: 13 call sites in 9 public methods, and **two methods on the Application-owned port `ICharacterRepository` carry Rules types in their signatures**, so any implementer must compile against Rules.
5. **Candidate A** (add the reference): **verified in Unity 6000.4.0f1** (fresh `Library`, exit 0, zero `error CS`, all 70 EditMode tests pass) and proven cycle-free from the real `.asmdef` files. Technically trivial, but per `ADR-001` section 15 it is legal only with a new ADR, and it legalises behaviour that `ADR-024`/`ADR-025` say should live in Application.
6. **Candidate B** (decouple the code): the ADR-conforming end state, and the shape slices S05-S07 already use. It is a multi-part refactor of the largest file in the repo (13 call sites, 2 port signatures, 138 test call lines in 23 test files; 79 constructor sites if done by injection).
7. **Recommendation.** B is the correct end state. A is the only quick, verified way to make the Unity client build again. Recommended path: **A as an explicit, ADR-recorded, time-boxed bridge plus an automatic guard now (small), B as a separate tracked task (large).** If the product owner does not want to amend `ADR-001`, B is the only conforming path and the Unity client stays unbuildable until it lands. Details in section 6.

---

## 1. Evidence gathered

| # | Experiment | Result |
|---|---|---|
| E1 | Unity 6000.4.0f1 batchmode compile of `main` (`50701d5`), fresh `Library` | **Fails.** 11 distinct errors, all in `SqliteCharacterRepository.cs` (lines 14, 15, 16, 17, 18, 19 `CS0234`; 1392 twice, 1438 twice `CS0246`; 48 `CS0012`). Also 15 unrelated-looking `CS0246 'GUID'` lines from `Library/PackageCache` (HDRP/ShaderGraph editor code); they appeared only in runs where project scripts failed to compile and are absent in E2 and E4. |
| E2 | Same, **Candidate A** patch (`"Odyssey.Rules"` added to `Odyssey.Persistence.asmdef` `references`), `-runTests -testPlatform EditMode`, fresh `Library` | **Passes.** Exit 0, zero `error CS`, **70/70 EditMode tests passed** (whole client suite, includes the 12 `BoardScreenPresenterTests`). |
| E3 | `dotnet build` of each production project with `DisableTransitiveProjectReferences=true` (scratch copy) | **Only `Odyssey.Persistence` fails**: 11 errors, all in `SqliteCharacterRepository.cs` (same lines/codes as E1). Domain, Rules, Content, Application, Networking build clean. Adding `<ProjectReference Include="Odyssey.Rules.csproj" />` to Persistence makes it build with 0 errors. |
| E4 | Unity compile of `592fd38` (parent of the suspect commit; merge of PR #88) | **Clean.** Exit 0, zero `error CS`. |
| E5 | Unity compile of `4c34479` (`ODY-S04-105`) | **Fails.** Exit 1, exactly one distinct error: `SqliteCharacterRepository.cs(13,41): error CS0234 ... 'Rules' does not exist in the namespace 'Odyssey'`. |
| E6 | Namespace-vs-`references` scan of every `.asmdef` in the repo (11 assemblies) | One violation cluster: `Odyssey.Persistence` uses `Odyssey.Rules.Character` (6 qualified references, 1 file). Nothing else. |
| E7 | Transitive closure / cycle check computed from the real `.asmdef` files | `Rules -> {Domain}`, `Domain -> {}`; no path from Rules to Persistence; no cycle in the current graph; **no cycle after adding `Persistence -> Rules`**. |

All experiments ran in scratch worktrees that have been removed. Experiment artifacts (Unity logs, result XML) were kept outside the repository. A background batch runner and manual runs overlapped, so two of the runner's Unity invocations (the Candidate A and the pre-break projects) aborted with "another Unity instance is running with this project open"; those runner results were discarded. E2 and E4 are from the separate manual runs that completed and produced result files; E1 and E5 failed identically in both the runner and manual runs. No reported number comes from an aborted run.

---

## 2. Question 1.1 - architectural intent

### 2.1 What ADR-001 says

File: `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`.

- Line 32: "Единственный разрешённый граф compile-time dependencies:", followed (lines 49-52) by
  `Odyssey.Persistence -> Odyssey.Domain, Odyssey.Content, Odyssey.Application` (no `Odyssey.Rules`).
- Line 68: "Любая dependency, отсутствующая в этом графе, запрещена до принятия нового ADR."
- Section 5 matrix, line 185: `| Persistence | ✓ | — | ✓ | ✓ | — | — | — |` where the header (line 179) is `From / To | Domain | Rules | Content | Application | Persistence | Networking | Unity Client`, so the Persistence -> Rules cell is `—`, defined on line 177 as "запрещена". Line 197 adds "прямые циклы и транзитивные циклы запрещены."
- Section 6.5 (lines 309-327): Persistence "реализует persistence ports, объявленные Application" and does "mapping между persisted representation и Domain/Content/Application contracts" (line 323); line 325: "Persistence не владеет игровыми инвариантами и не вызывает Networking."
- Section 6.2 (lines 236-253): "`Odyssey.Rules` владеет детерминированными вычислениями системы правил." Line 253: "Rules не публикует network messages и не сохраняет результаты."
- Section 6.4 (line 293): "Application определяет port на стороне потребителя."
- Section 11 (line 560): "Изменение dependency graph выполняется отдельным pull request и требует обновления ADR либо нового ADR."
- Section 13 (lines 581-604): CI "обязан проверять границы автоматически", minimum checks 1-9, and line 604: "Ручное review не заменяет автоматическую проверку."
- Section 15 (lines 631-643): "Новый ADR обязателен для: ... изменения стрелки в матрице;" (line 636).

**Later ADRs, same intent.**
- `ADR-024` line 192: "`Odyssey.Rules` owns ... cost/cap/requirement calculations used by purchase validation. It does not commit state or write history." Line 193: "`Odyssey.Application` owns `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`AcquireAbility`/... command handlers, permission/revision/lock checks, transaction orchestration". Line 194: "`Odyssey.Persistence` owns the physical ... tables ... It does not decide whether a purchase, reservation, revert, or respec is legal."
- `ADR-025` line 185: "`Odyssey.Rules` owns ... Ruleset value/definition mapping computation used by migration preview/apply." Line 186: Application owns the `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration` command handlers and transaction orchestration. Line 187: "`Odyssey.Persistence` ... does not decide whether an ownership change, delete, restore, or migration is legal".

No other ADR was found that permits `Persistence -> Rules` (searched `docs/adr/*.md` for Persistence/Rules pairings).

### 2.2 Deliberate boundary or oversight?

**The boundary is deliberate; the code is the deviation.**

- `Odyssey.Persistence.asmdef` history (`git log`): created `6f9dc68` (2026-08-10, `ODY-S00-003`), adjusted `46a2852` (same day). It has not changed since. Its `references` (`Odyssey.Domain`, `Odyssey.Content`, `Odyssey.Application`) equal the ADR-001 graph.
- The boundary is also encoded three more ways: `scripts/verify-test-structure.ps1:30` (`'Odyssey.Persistence' = @('Odyssey.Domain', 'Odyssey.Content', 'Odyssey.Application')`), `DotNet/Projects/Odyssey.Persistence.csproj` (3 `ProjectReference`s), and `Packages/com.odyssey.persistence/package.json` (3 dependencies).
- `git log -S"Odyssey.Rules"` on `SqliteCharacterRepository.cs`: the file was created by `e904c94` (2026-09-01, `ODY-S04-101`) without any Rules use. The first commit adding `using RulesAttributeCostRules = Odyssey.Rules.Character.AttributeCostRules;` is **`4c34479` (2026-09-01, `ODY-S04-105`)**. That commit touched no `.asmdef`, `.csproj` or `package.json`. Later Rules uses were added by `ODY-S04-106`, `-108`, `-109`, `-113`.
- `ODY-S04-105`'s own contract asserted conformance: `docs/tasks/completed/ODY-S04-105_DevelopmentPool_Attribute_Purchases.md:114` ends "Matches `ADR-001`/`ADR-024` §9 exactly." The same sentence correctly notes Rules "referencing only `Odyssey.Domain`", so the review checked Rules' own dependencies but not the new Persistence -> Rules edge.
- **Root cause behind the coupling.** ADR-024/025 assign command handlers and legality decisions to Application. SLICE-04 instead implemented each command as a method on the persistence-owned repository (`PurchaseAttributeIncrease`, `PurchaseSkillLevel`, ... on `ICharacterRepository`/`SqliteCharacterRepository`), so the rule evaluation ended up inside the repository's transaction. Slices S05-S07 later used the ADR-conforming shape (section 5.3).

### 2.3 What `Odyssey.Rules` is and who may use it

Per ADR-001 section 6.2 it owns deterministic calculations and never persists. Its own asmdef references only `Odyssey.Domain` (`Packages/com.odyssey.rules/Runtime/Odyssey.Rules.asmdef`). Legitimate referencers (from the real `.asmdef` files): `Odyssey.Content`, `Odyssey.Application`, `Odyssey.Unity.Client`, `Odyssey.Unity.Client.Editor`, `Odyssey.Tests.Unity.EditMode`. ADR-001 section 5 does not allow Persistence (or Networking) to reference it.

---

## 3. Question 1.2 - scale of the problem

### 3.1 Is it the only occurrence?

**Yes, for every assembly that exists today - established two independent ways.**

1. **Compiler-authoritative (E3).** With `DisableTransitiveProjectReferences=true` on all six production projects, only `Odyssey.Persistence` fails, and only in `SqliteCharacterRepository.cs`. This covers Domain, Rules, Content, Application, Persistence, Networking. It does **not** cover the Unity-only assemblies (`Odyssey.Unity.Client`, `.Editor`, the test asmdefs), which are not in the .NET solution.
2. **Source scan (E6).** A throw-away script (not committed) mapped every declared `namespace` to its owning assembly and checked every `Odyssey.*` `using`/qualified name in each assembly's `.cs` files against that assembly's `.asmdef` `references`. It scanned 11 assemblies (Unity Client 17 files, Client.Editor 3, EditMode tests 9, PlayMode tests 1, SerializationAot tests 1, Application 97, Content 3, Domain 25, Networking 6, Persistence 27, Rules 17) and reported the one Persistence -> Rules cluster (6 qualified references on lines 14-19, all `Odyssey.Rules.Character`). It is a heuristic (namespace-name based, strips comments/strings, does not see non-`Odyssey.*` assemblies), but it independently agrees with E3 and covers the Unity assemblies E3 cannot.

**The project has no automated check for this defect class.** `scripts/verify-test-structure.ps1` compares *declared* graphs only: asmdef `references` against the allowed set (lines 963, 983-984), package/asmdef parity and asmdef/csproj parity (1064-1068), Persistence<->Networking (1072), cycles (1076). Because the csproj and asmdef lists agree with each other (both omit Rules), parity passes while the code needs Rules. `ADR-001` section 13's own minimum checks (1-9) do not include "source usage matches references" either, so this is a gap in the ADR's specified enforcement, not only in the script. This absence is itself a finding.

### 3.2 Every use of `Odyssey.Rules` in Persistence

All in `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (6,229 lines):

| Lines | Rules type / member | Role |
|---|---|---|
| 14-19 | `using Odyssey.Rules.Character;` + 5 aliases (`AttributeCostRules`, `SkillCostRules`, `AbilityCostRules`, `ResourceInitializationRules`, `AnatomyInitializationRules`) | imports |
| 48 | class declaration implements `ICharacterRepository` | `CS0012`: interface members mention Rules types |
| 1392, 1438 | `CharacterRulesetMigrationPlan`, `RulesetDefinitionCatalog` in **public signatures** of `PreviewCharacterRulesetMigration` / `ApplyCharacterRulesetMigration` | dictated by the Application port (below) |
| 1419, 1480 | `RulesetMigrationRules.BuildPlan(...)` | build a ruleset-migration plan; at 1480 recomputed under lock ("never trust the client-supplied plan", CAP-INV-004) |
| 2220, 2225 | `AttributeCostRules.ExceedsNormalCap`, `.CostForIncrease` in `PurchaseAttributeIncrease` | cap check + cost |
| 2333, 2361 | `SkillCostRules.RequiresRecommendation`, `.CostForIncrease` in `PurchaseSkillLevel` | recommendation gate + cost |
| 2543 | `SkillCostRules.CostForIncrease` in `RequestSkillAdvancedRecommendation` | reserved amount |
| 3010, 3015 | `AttributeCostRules`/`SkillCostRules.CostForIncrease` in `ComputeRespecPlan` | respec plan costs |
| 3491 | `AbilityCostRules.CostForAcquisition` in `AcquireAbilityViaProgressionPurchase` | cost |
| 3805 | `ResourceInitializationRules.DefaultBaseMaximum` (x2), `DefaultMinimumValue`, `DefaultRecoveryRule` in `InitializeCharacterResource` | test-fixture defaults |
| 4042, 4043 | `AnatomyInitializationRules.DefaultAnatomyProfileVersion`, `.DefaultHumanoidBodyParts()` in `InitializeCharacterAnatomy` | test-fixture defaults |

Namespaces used: only `Odyssey.Rules.Character` (no other Rules namespace). Counted: **13 code call sites in 9 public methods**, plus imports and 2 port-signature uses.

**Application port coupling.** `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs:671` and `:685` declare `PreviewCharacterRulesetMigration` / `ApplyCharacterRulesetMigration` with `Odyssey.Rules.Character.CharacterRulesetMigrationPlan` and `...RulesetDefinitionCatalog` in their signatures (legal there: Application references Rules). The types are declared in `Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs:23` and `:95`. Any assembly implementing `ICharacterRepository` must therefore compile against `Odyssey.Rules`, independent of how the *internal* calls are refactored. The only implementers are `SqliteCharacterRepository` and one test decorator (`DotNet/Tests/Odyssey.Tests.Persistence/CheckIntegrationTests.cs:347`).

---

## 4. Question 1.3 - the two candidates, tested

### 4.1 Candidate A: add `Odyssey.Rules` to `Odyssey.Persistence.asmdef`

**Practical verification (E2).** With exactly one added line (`"Odyssey.Rules"` in `references`), Unity 6000.4.0f1 batchmode `-runTests -testPlatform EditMode` on a fresh `Library` finished with exit code 0, zero `error CS`, **70 of 70 tests passed**. The whole client compiles and runs. The same change to `Odyssey.Persistence.csproj` (`ProjectReference` to `Odyssey.Rules.csproj`) builds with 0 errors under `DisableTransitiveProjectReferences` (E3).

**Cycle check (E7), from the real files.** Transitive closure of `Odyssey.Rules` = `{Odyssey.Domain}`; of `Odyssey.Domain` = `{}`. Only `Odyssey.Unity.Client`, `.Client.Editor` and the two Unity test asmdefs reach `Odyssey.Persistence`. So Rules cannot reach Persistence and adding the edge creates no cycle; the compiler agreed (no cycle error in E2). No cycle error text exists to report because none occurred.

**Architectural conformity: not neutral, prohibited as written.** ADR-001 section 5 marks the cell `—`; line 68 forbids any dependency absent from the graph "до принятия нового ADR"; section 15 (line 636) makes "изменения стрелки в матрице" a new-ADR event. A also runs against `ADR-024:194`, `ADR-025:187` and ADR-001 section 6.5 (line 325): it would formalise a repository that decides purchase/migration legality. Technically acceptable, ADR-wise only through a new ADR.

**Everything A would have to touch:** `Odyssey.Persistence.asmdef` (+1 reference); `DotNet/Projects/Odyssey.Persistence.csproj` (+1 `ProjectReference`); `Packages/com.odyssey.persistence/package.json` (+1 dependency, ADR-001 section 11); `scripts/verify-test-structure.ps1:30` (allowed set, which also drives the package/csproj parity checks); a new ADR amending ADR-001 sections 1, 5 and 11. Roughly 4 config lines plus one ADR.

### 4.2 Candidate B: remove Persistence's dependency on Rules

**Why Persistence uses these types at all.**

1. *Cost / cap / recommendation-gate calculations* (`AttributeCostRules`, `SkillCostRules`, `AbilityCostRules`): the purchase methods read the character inside a `SqliteSavingPipeline` transaction, check the section revision, evaluate the rule, and write pool/section/ledger in one commit. The rule call is a legality decision executed inside the repository - the thing ADR-024:194 says Persistence must not do.
2. *Fixture defaults* (`ResourceInitializationRules`, `AnatomyInitializationRules`): constants and a body-part list flagged in code as "TEST FIXTURE ONLY". `InitializeCharacterAnatomy` is also the method the SLICE-07 backlog (block 3) says a future task will wire to the new anatomy catalog, at which point the Rules fixture call disappears.
3. *Ruleset-migration planning* (`RulesetMigrationRules.BuildPlan`, plan/catalog types): preview reads live state and builds a plan; apply **recomputes the plan under the transaction lock and compares hashes** (`SqliteCharacterRepository.cs:1476-1478`). This is the hardest part: the computation must be callable inside the locked transaction.
4. *Port-signature coupling* (section 3.2): unrelated to computation; caused by the two Rules types being part of the Application contract.

**Can it be decoupled by an adapter/DTO?** Yes, but it must include the Application port, not only the repository internals, because of (4). Two ADR-conformant shapes:

- **B-1, caller-computed values.** Application computes cost / cap / recommendation / defaults via Rules and passes the results as parameters. The repository keeps its existing optimistic-concurrency revision check, which already guards against state drift between Application's read and the write (`expectedAttributeRevision`, `expectedSkillRevision`, ...). Changes 7 repository signatures. It does not work for `ApplyCharacterRulesetMigration` (plan must be recomputed under lock).
- **B-2, consumer-owned ports injected into the repository** (ADR-001 sections 6.4 and 8.1: "Application определяет port на стороне потребителя"): Application defines small interfaces (economy rules, ruleset-migration planner) with Rules-backed implementations in Application; `SqliteCharacterRepository` receives them by constructor and may call them inside its transaction. Fits recompute-under-lock. Changes the constructor (79 call sites).
- In both, the data types `CharacterRulesetMigrationPlan` / `RulesetDefinitionCatalog` would move to `Odyssey.Domain` (Domain is allowed for Persistence; Rules -> Domain is allowed, so Rules keeps building them) so the port signatures no longer mention Rules. A namespace change touching Rules, Application and tests.

**In-repo precedent for the conforming shape.** Slices S05-S07 already work this way: `Packages/com.odyssey.application/Runtime/Checks/CheckService.cs` (references `Odyssey.Rules`) calls the `ICheckRulesEvaluator` from `Odyssey.Rules.Checks` and drives Persistence through Application-owned ports (`ICheckStateReader`, `ICheckRepository`); `SqliteCheckRepository`/`SqliteCheckStateReader` contain no Rules reference. The same holds for `ActivateAbilityService` and `AttackApplyService`. The scan (E6) confirms `SqliteCharacterRepository` is the only Persistence file using Rules. SLICE-04's character economy is the outlier, not the norm.

**Scope numbers for a future implementation ТЗ (estimate only, nothing implemented):**

| Item | Count |
|---|---|
| Rules code call sites in Persistence | 13, in 9 public methods, 1 file (6,229 lines) |
| Application port methods with Rules types in signatures | 2 (`CharacterRepositoryContracts.cs:671`, `:685`); implementers 2 (`SqliteCharacterRepository`, `CheckIntegrationTests` decorator) |
| Types to move to Domain (if signatures are decoupled) | 2 (`CharacterRulesetMigrationPlan`, `RulesetDefinitionCatalog`) |
| Test call lines of the 7 affected methods | 138 in 23 test files (`PreviewCharacterRulesetMigration` 14, `ApplyCharacterRulesetMigration` 13, `PurchaseAttributeIncrease` 34, `PurchaseSkillLevel` 13, `RequestSkillAdvancedRecommendation` 16, `InitializeCharacterResource` 20, `InitializeCharacterAnatomy` 28); `ComputeRespecPlan` and `AcquireAbilityViaProgressionPurchase` have none |
| `new SqliteCharacterRepository(` sites (matter for B-2) | 79 in 40 files |

Risks: it is the largest and most intricate file in the repository; the migration recompute-under-lock semantics (CAP-INV-004) must be preserved exactly; ~140 test call lines change. Natural split: (1) move plan types + planner port and re-point the two migration methods, (2) economy rules (7 sites), (3) fixture defaults - coordinated with the future anatomy-catalog wiring so `InitializeCharacterAnatomy` is not refactored twice.

### 4.3 Other options considered

- *Provider-specific sub-assembly inside Persistence that references Rules* (ADR-001 section 15 line 650 allows sub-assemblies "если граф не меняется"): the graph does change, so it needs the same ADR as A and gains nothing over A.
- *Leave the asmdef, suppress the Unity build*: not an option; the client cannot run.

---

## 5. Question 1.4 - side questions

### 5.1 Should an automatic check be added? Yes, as a separate ticket

Findings that make it cheap:

1. **`DisableTransitiveProjectReferences=true` on the six production `.csproj` files** (verified in E3): it makes plain `dotnet build` - which CI already runs - reproduce exactly the Unity failure (same 11 errors, same lines) and found no other violation. It can only land together with, or after, the fix (before it, the build would fail). Effort: 6 property lines + one assertion in `verify-test-structure.ps1` that the property stays set. Small. Limitation: covers only the projects in the .NET solution, not the Unity-only assemblies.
2. **A namespace-based source-vs-asmdef scanner** (prototyped here in ~100 lines; found no other violation): covers the Unity assemblies E3 cannot, and would satisfy the gap in ADR-001 section 13. Heuristic; false negatives possible for non-`Odyssey.*` references. Effort: small-to-medium as a `scripts/` check wired into the existing repository-policy job.
3. **A real Unity compile in CI is forbidden by policy**: `scripts/verify-ci.ps1:122-123` rejects `Unity.exe`, `unity -batchmode`, `game-ci`, `UNITY_LICENSE`, and `compile` in the Unity job block, and `.github/workflows/ci.yml:64-74` runs only the static `verify-unity-project.ps1`. Changing that is a policy/licensing decision, not an engineering one. A lighter alternative is a documented pre-merge step running `scripts/test-unity.ps1` locally for Unity-touching PRs.

Corroborating evidence that the Unity project is not routinely opened: 121 of 175 `.cs` files under `Packages/com.odyssey.*/Runtime` have no committed `.meta` (Unity writes them on open); the regular `.meta` commits stop at 2026-08-28 (`ODY-UI-01-007`), with a single stray file on 2026-09-13.

### 5.2 How long undetected

Bisected by real compiles (E4, E5): last good `592fd38` (merge of PR #88); first bad `4c34479`, merged in PR #89 (`8af347b`, 2026-09-02). The tip of `main` is dated 2026-09-24: **22 days**, **87 merge commits** on `main` (`git log --merges 8af347b..origin/main`). The last Unity-executed client work in history is `ODY-UI-01-002` ("real Unity test run", 2026-08-27) and its follow-ups through 2026-08-29. The error count grew from 1 (E5, one alias `using`) to 11 as later tasks (`S04-106`, `-108`, `-109`, `-113`) added Rules uses and the two Rules-typed port methods.

---

## 6. Recommendation

**Correct end state: B.** It is the only option conforming to ADR-001 (matrix + section 6.5) and to the ownership statements in ADR-024/ADR-025, and it matches how S05-S07 are built. **Fastest verified unblock: A**, which is legal only through a new ADR (ADR-001 section 15).

Recommended sequence:

1. **Small task now - A as a bridge, with its guard.** A new ADR that amends the ADR-001 matrix to allow `Persistence -> Rules`, *explicitly time-boxed* to the legacy SLICE-04 character-economy handlers and stating that removal is tracked by the B task (without the sunset clause ADR-001 would stop describing reality). In the same change: the 4 config edits in section 4.1, `DisableTransitiveProjectReferences` on the six production `.csproj` files, and the assertion in `verify-test-structure.ps1`. Verified in this investigation: Unity compiles, 70/70 EditMode tests pass, no cycle. This unblocks Unity verification of SLICE-08 blocks 2-5.
2. **Separate, larger task(s) - B.** Split as in section 4.2; remove the bridge when done.
3. **Separate ticket - the source-vs-asmdef scanner** and a decision on how Unity compilation is gated before merge (section 5.1 items 2-3).

If the product owner declines to amend ADR-001, then B is the only conforming path; until it lands the Unity client cannot be built, so SLICE-08 UI work can be written but not run in Unity.

A new ADR is needed for the bridge; per this task's constraints none was created here.

---

## 7. Limitations

- Line numbers refer to `50701d5`. The Unity results come from one machine (Windows, Unity 6000.4.0f1); they were not run in CI, which by policy cannot run Unity.
- E2 ran the EditMode suite only (70 tests); PlayMode tests and a Player build were not run.
- The source scanner is heuristic (section 3.1); the compiler-based E3 is the authoritative result for the six production projects.
- The `GUID` package errors in E1 are reported as observed (present only in failing-compile runs), not root-caused.
- The scope numbers in section 4.2 are estimates for planning, not a design.

## 8. Reproduction

```powershell
# E1 / E5 (baseline and suspect commit): Unity batchmode compile
& 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe' -batchmode -quit -projectPath <worktree> -logFile <log>

# E2 (Candidate A): add "Odyssey.Rules" to Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef references, then
& 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe' -batchmode -projectPath <worktree> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>

# E3: in a scratch copy, add <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>
#     to each DotNet/Projects/*.csproj PropertyGroup, then: dotnet build DotNet/Projects/Odyssey.Persistence.csproj
```

Bisect endpoints: `git worktree add --detach <path> 592fd38` (clean) and `4c34479` (fails).
