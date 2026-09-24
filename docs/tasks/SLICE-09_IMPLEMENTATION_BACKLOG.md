# Odyssey VTT — SLICE-09 Persistence/Rules Boundary Remediation Implementation Backlog

**Status:** Implementation revision — OPEN. Block 1 (Application layer for character advancement + decoupling the two "light" methods) is merged (`ODY-S09-101`); block 2 (the four cost methods) is In Review (`ODY-S09-102`); three blocks remain named, not scoped (respec, ruleset migration, architecture-check tightening).
**Slice:** `SLICE-09 — Persistence/Rules boundary remediation: moving the character-economy decisions that SqliteCharacterRepository makes through Odyssey.Rules into an Application-layer service, one method group at a time, until Odyssey.Persistence no longer depends on Odyssey.Rules and the boundary is enforced automatically`
**Parent task:** `docs/tasks/active/ODY-S09-101_Character_Advancement_Service_And_Light_Method_Decoupling.md`
**Predecessor backlog:** None in the usual sequential sense. This slice is motivated by an independent investigation of a Unity build break (PR #173, report `docs/research/Unity_Rules_Asmdef_Break_Investigation.md`, on that pull request's branch until it is merged) that was itself a finding of `SLICE-08`'s first task (`ODY-S08-101`). It is independent of `SLICE-08`'s remaining blocks in code, but it gates their verification in the Unity client (section 6 point 6). There is no separate `SLICE-09_BACKLOG.md` prerequisite document -- see section 1's explicit process simplification.
**ExecPlan:** Not required for this document itself (brief plan for the backlog document; each child task chooses its own planning mode).
**Created:** 2026-09-24
**Last updated:** 2026-09-24 UTC -- created by `ODY-S09-101`, which is row 1 and is moved to `In Review` by the same change.

## 1. Purpose and explicit process simplification

The investigation in PR #173 established that `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` uses `Odyssey.Rules.Character.*` directly, while `ADR-001` section 5 allows `Odyssey.Persistence` to depend only on `Odyssey.Domain`, `Odyssey.Content` and `Odyssey.Application`, and `ADR-024` (lines 192-194) and `ADR-025` (lines 185-187) assign cost/cap/plan decisions to Application and say Persistence "does not decide whether a purchase ... is legal". `dotnet` hides the violation because project references are transitive through `Odyssey.Application -> Odyssey.Rules`; Unity does not, so the Unity client does not compile on `main`. The product owner decided to fix it the ADR-conforming way (the investigation's "Candidate B": decouple the code) and not to amend `ADR-001`. That is too large for one task (13 Rules call sites in 9 methods of the largest file in the repository, two Application-port signatures that carry Rules types, and ~140 test call lines), so it is decomposed into the five blocks below.

**Explicit simplification, following `ODY-S07-101`'s and `ODY-S08-101`'s precedent:** this slice does not use `SLICE-05`'s two-phase process (a separate prerequisite backlog proposing an ADR, a distinct approval cycle, only then an implementation backlog). `ODY-S09-101`'s governing ТЗ explicitly authorizes creating this backlog in the same task/PR as the first block's implementation. **There is no separate `SLICE-09_BACKLOG.md` prerequisite document, and none is planned** -- this file is the only backlog document for this slice.

This backlog does **not** itself implement anything. It decomposes the slice into ordered child tasks, each its own separate task contract and pull request, activated one at a time, each delivered with a green CI. Only block 1 is detailed (as `ODY-S09-101`); blocks 2-5 are named and reserved.

Its sources of scope are, exclusively:

- The investigation report of PR #173 and its "Candidate B" (decouple the code, keep `ADR-001` unchanged).
- The product owner's decision to do only that, and to split it into sequential blocks.
- `ADR-001` (section 5 matrix, section 6.4/6.5 ownership), `ADR-024` and `ADR-025`, read as the target ownership, not revised.
- The existing, working precedent of the target shape: `Odyssey.Application.Checks.CheckService` (`ODY-S07-102`) uses `Odyssey.Rules` itself and reaches Persistence only through Application-owned ports whose signatures contain no Rules types; `SqliteCheckRepository`/`SqliteCheckStateReader` contain no Rules reference.

No child task in this backlog reopens any decision `ADR-001`-`ADR-031` already made.

## 2. Exit criteria for this revision

- This backlog exists, with all five blocks (section 3) named and reserved -- only block 1 detailed, the rest not scoped, per the same "named, not scoped" convention earlier slice backlogs established.
- The block order and its justification are recorded (section 6), including why the architecture-check tightening is deliberately last.
- Block 1 is decomposed into its own numbered task (`ODY-S09-101`, section 9).

## 3. Roadmap block mapping

| Order | Block | Status |
|---:|---|---|
| 1 | Application layer for character advancement + decoupling the two "light" methods that only read default constants (`InitializeCharacterResource`, `InitializeCharacterAnatomy`) | In Review (`ODY-S09-101`, section 9 row 1) |
| 2 | Decoupling the cost methods (`PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RequestSkillAdvancedRecommendation`, `AcquireAbility`/`AcquireAbilityViaProgressionPurchase`) -- real legality/balance decisions move into the Application service | In Review (`ODY-S09-102`, section 9 row 2) |
| 3 | Decoupling respec (`ComputeRespecPlan`, `PreviewCharacterRespec`, `ApplyCharacterRespec`) | Named, reserved (section 8) |
| 4 | Decoupling ruleset migration (`PreviewCharacterRulesetMigration`, `ApplyCharacterRulesetMigration`, the CAP-INV-004 stale-plan-hash check) -- the hardest block | Named, reserved (section 8) |
| 5 | Tightening the architecture check so it catches transitive type leakage through legal references, and confirming `Odyssey.Persistence` cannot compile against `Odyssey.Rules` by any path | Named, reserved (section 8) |

## 4. Global non-goals (this revision)

- Amending or superseding `ADR-001` (the product owner explicitly chose not to; the investigation's "Candidate A" is not part of this slice).
- Changing any game rule, cost, default value or persisted result: every block is a structural refactor; behavior must be unchanged and demonstrably so.
- Any Unity-client change. No character command is called from the Unity client today (only from tests), so no client consumer needs rewiring.
- A composition root or DI container. None exists in the project; the target service is a static class in the `CheckService` form, with dependencies passed explicitly by the caller (today, tests).
- Implementing any of blocks 2-5 inside block 1.

## 5. No new prerequisite ADR-proposal backlog needed

The slice implements the ownership `ADR-001`, `ADR-024` and `ADR-025` already state; it needs no new ADR and revises none. If a later block finds that its design genuinely requires an architectural decision (block 4's replacement of the hash-based stale-plan check is the likely candidate), that task reports it as a finding rather than creating an ADR silently, per the convention `ODY-S07-103` established.

## 6. Scope decisions requiring explicit justification

1. **Only the ADR-conforming fix.** The product owner chose "Candidate B" from the investigation and to leave `ADR-001` untouched. Adding the missing `Persistence -> Rules` reference (Candidate A) would have been faster but is not conforming without a new ADR, and would legalise the repository deciding purchase/migration legality.
2. **Block order 1 -> 4 is by increasing complexity.** Block 1 only replaces reads of constants with caller-supplied values (no decision is made by Rules; nothing is recomputed under a lock); block 2 moves real legality/balance decisions (cap, recommendation gate, cost); block 3 reuses block 2's cost rules inside a plan; block 4 is the hardest (a plan must be recomputed under the transaction lock and compared by hash). Doing the easy blocks first also establishes the service and the test-migration pattern that the harder blocks reuse. Block 3 depends on block 2 (its plan uses the same cost rules); block 4 depends on blocks 1-3.
3. **Block 5 is deliberately last.** Until every Rules use has left `Odyssey.Persistence`, Persistence still legitimately (through the un-decoupled methods) resolves Rules types via `Odyssey.Application`; tightening the check earlier would turn CI red for as long as blocks 2-4 take, and every task in this slice must be delivered with a green CI. The check's own defect is recorded, not fixed here: `scripts/verify-test-structure.ps1` compares the declared `.asmdef`/`.csproj`/`package.json` graphs only, never the compiler's actual transitive closure, which is how the violation went unnoticed. A verified cheap way to make `dotnet build` catch this class (the investigation's experiment) is `DisableTransitiveProjectReferences` on the production projects; that and any source-vs-asmdef scan belong to block 5.
4. **Shape of the target service.** A static `Odyssey.Application` class in the `CheckService` form, taking `ICharacterRepository` as an explicit argument. For block 1, the values Rules supplies are passed to the repository as plain flat arguments (`baseMaximum`, `minimumValue`, `recoveryRule`; `anatomyProfileVersion`, `bodyParts`) rather than through constructor-injected ports: constructor injection would change 79 `new SqliteCharacterRepository(...)` sites in 40 files for no benefit, and there is no composition root to wire it. Blocks 2-4 choose their own mechanism; block 4 in particular may need a callable planner inside the transaction and must justify what it picks.
5. **Existing tests move to the new path, not around it.** Tests that used to call a decoupled repository method directly now call the service wrapper, with the same arguments and the same assertions; the repository methods additionally get their own direct tests proving they use exactly what the caller supplies.
6. **The Unity client stays unbuildable until the whole slice is done.** After block 1, eight public methods of `SqliteCharacterRepository` (and two methods on the `ICharacterRepository` port) still use `Odyssey.Rules` types, so `Odyssey.Persistence` cannot be given the strict dependency set and the Unity compile break from the investigation persists. This is an explicit, accepted consequence of doing the fix in green-CI steps, not an oversight.

## 7. Dependency rules

- `ODY-S09-101` has no dependency inside this slice -- it is the first task. It depends on `ODY-S04-109` (the two commands and their fixture defaults) and reuses `ODY-S07-102`'s `CheckService` as the structural precedent.
- Block 3 (respec) depends on block 2 (same cost rules). Block 4 (ruleset migration) depends on blocks 1-3 (same service and pattern). Block 5 (check tightening) depends on all of blocks 1-4.
- This slice does not depend on `SLICE-08`'s remaining blocks; it gates their verification in the Unity client (section 6 point 6).

## 8. Reserved future blocks (named, not scoped)

- **Block 1 -- Application layer for character advancement + light-method decoupling.** In Review (`ODY-S09-101`, section 9 row 1). Introduces `Odyssey.Application.CharacterAdvancement.CharacterAdvancementService` (static class, `CheckService` form) and changes `ICharacterRepository.InitializeCharacterResource`/`InitializeCharacterAnatomy` to take the initial values as arguments; the service reads the fixture defaults from `Odyssey.Rules` and calls the repository. Behavior unchanged (verified by a before/after snapshot). The other eight public methods are untouched.
- **Block 2 -- Cost methods.** In Review (`ODY-S09-102`, section 9 row 2). Pattern: the Application service reads the Character, decides with Rules, and the repository commits the decision, re-checking under its transaction lock the state value the decision was based on (see the row for the details and the one deliberate deviation from a pure "decide first, never touch Persistence on failure" flow). Scope as named by block 1: `PurchaseAttributeIncrease` (attribute cap + cost), `PurchaseSkillLevel` (recommendation gate + cost), `RequestSkillAdvancedRecommendation` (reserved amount), `AcquireAbility` via its private `AcquireAbilityViaProgressionPurchase` (cost). Six Rules call sites (block 1 of this backlog said five -- a miscount): `ExceedsNormalCap`, attribute `CostForIncrease`, `RequiresRecommendation`, skill `CostForIncrease` in the purchase, skill `CostForIncrease` in the recommendation, `CostForAcquisition`. They are real legality decisions and move into the Application service.
- **Block 3 -- Respec.** Named by this task; not scoped. The private `ComputeRespecPlan` (attribute and skill costs), feeding `PreviewCharacterRespec` and `ApplyCharacterRespec`.
- **Block 4 -- Ruleset migration.** Named by this task; not scoped. `PreviewCharacterRulesetMigration` and `ApplyCharacterRulesetMigration`: two `RulesetMigrationRules.BuildPlan` calls (the second recomputes the plan under the transaction lock and compares hashes, CAP-INV-004), and two Application-port signatures that carry `CharacterRulesetMigrationPlan`/`RulesetDefinitionCatalog`. It likely requires replacing the hash-based staleness check with something that needs no Rules type in Persistence (for example a plain revision check) and moving those two types out of `Odyssey.Rules`; that design is this block's own decision.
- **Block 5 -- Architecture-check tightening.** Named by this task; not scoped. Make the check catch transitive type leakage through legal references, and confirm `Odyssey.Persistence` cannot compile against `Odyssey.Rules` by any path. Last, for the reason in section 6 point 3.

None of blocks 2-5 is decomposed into a numbered implementation task by this revision -- decomposing any of them is a future backlog revision, per the same "named, not scoped" rule the earlier slice backlogs established.

## 9. Ordered backlog

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S09-101` | In Review | Investigation report of PR #173 ("Candidate B") + product-owner decision to fix it conformingly in sequential blocks (block 1, section 3) | SLICE-09 Backlog + Application Layer for Character Advancement + Decoupling of the Two Light Methods | `ODY-S04-109`; precedent `ODY-S07-102` | ExecPlan | Creates this backlog. Adds `CharacterAdvancementService` (`Odyssey.Application.CharacterAdvancement`, static class in `CheckService`'s form, taking `ICharacterRepository`) with `InitializeResourceWithDefaults` and `InitializeAnatomyWithDefaults`, which read the fixture defaults from `Odyssey.Rules` and call the repository. `ICharacterRepository.InitializeCharacterResource` now takes `baseMaximum`/`minimumValue`/`recoveryRule` and `InitializeCharacterAnatomy` takes `anatomyProfileVersion`/`bodyParts`; the two implementations no longer reference Rules. 46 existing test call sites in 15 files go through the service; 8 new tests (`TC-CHAR-181`-`188`). Behavior is unchanged, proven by a before/after snapshot diff of the persisted records and journal payloads (0 differences). The other eight public methods, all `.asmdef`/`.csproj` files and the architecture scripts are untouched, so the Unity client remains unbuildable until block 5. |
| 2 | `ODY-S09-102` | In Review | Block 2 of this backlog (section 3) | Decoupling of the Character "Cost" Methods from `Odyssey.Rules` | `ODY-S09-101` | ExecPlan | `CharacterAdvancementService` gains `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RequestSkillAdvancedRecommendation` and `AcquireAbility` (same argument lists as the repository methods had, plus the port as first argument). Each reads the Character through `GetCharacter`, applies `AttributeCostRules` / `SkillCostRules` / `AbilityCostRules`, and calls the changed repository method with the decision (`decidedFromValue`/`decidedFromLevel`, `exceedsNormalCap` / `requiresRecommendation`, `decidedCost` / `decidedReservedAmount`; `progressionPurchaseCost` for the ability). The four repository methods no longer reference Rules; under their transaction lock they re-check that the value the decision was based on still holds and otherwise return `CharacterRevisionConflict`, on top of the existing `MechanicsRevision` / entry-level revision gates. The repository keeps reporting failures in the original order (permission, revision, stale basis, cap / recommendation, balance), so callers see exactly what they saw before. 82 existing test call sites in 12 files go through the service; 14 new tests `TC-CHAR-189`-`202` (including four race tests proven to fail when the basis re-check is removed). Respec, ruleset migration, the architecture scripts and all `.asmdef`/`.csproj` are untouched, so the Unity client remains unbuildable until block 5. |


## 10. Backlog change control

- New work requires a task contract; this document reserves `ODY-S09-101` for the task that created it, and will reserve further numbers as each of blocks 2-5 (section 3/8) is activated for detailed decomposition, following the numbering convention the earlier slices established.
- Blocks 2-5 (section 8) are named, not scoped -- decomposing any of them into real task IDs is a future backlog revision, not an implicit extension of this one.
- Every task in this slice must be delivered with a green CI and must not touch the methods that belong to a later block, even when they are easy to fix in passing; such observations are reported as findings.
- A task may be split before implementation by updating this backlog, following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository. The real pull-request number for `ODY-S09-101` is recorded in section 9 row 1 after merge.
- This backlog does not replace any task's own acceptance criteria and does not itself decide any technical question beyond the scope decisions in section 6.
- If section 6's reasoning is later found incorrect, that is a new task/backlog-revision decision, not a silent edit -- this document would gain an explicit amendment note, not a rewritten section 6.
