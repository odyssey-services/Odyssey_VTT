# Odyssey VTT — SLICE-06 Rules Engine, Mechanics Execution, and MVP Scenario Implementation Backlog

**Status:** Implementation revision — OPEN. First block (`ODY-S06-101`-`10N`, Rules Engine / Mechanics Execution) is being decomposed by this document. All later blocks (roadmap letters B-G below) are named and reserved only, not yet decomposed.
**Slice:** `SLICE-06 — Rules Engine, Mechanics Execution, and the two-character MVP scenario (implementation)`
**Parent task:** `docs/tasks/active/ODY-S06-101_ADR030_Rules_Engine_Mechanics_Execution_Architecture.md`
**Predecessor backlog:** None -- see section 1's own explicit process simplification.
**ExecPlan:** Not required for this document itself (Brief plan for the backlog document; each child task chooses its own planning mode).
**Created:** 2026-09-17
**Last updated:** 2026-09-17 UTC

## 1. Purpose and explicit process simplification

This backlog converts the `SLICE-06` MVP scenario -- "two characters, gear, abilities, combat, movement, a repeated combat" -- into small, reviewable implementation tasks, the same convention `SLICE-01_IMPLEMENTATION_BACKLOG.md` through `SLICE-05_IMPLEMENTATION_BACKLOG.md` already used.

**Explicit simplification relative to the `SLICE-05` precedent:** `SLICE-05` used a two-phase process -- a separate prerequisite `docs/tasks/SLICE-05_BACKLOG.md` proposing `ADR-027`, a distinct product-owner approval cycle, and only then this implementation backlog created in a later task. `SLICE-06` does not repeat that two-phase split. `ODY-S06-101`'s own governing ТЗ explicitly authorized creating both `ADR-030` and this implementation backlog in the same task/PR, because `SLICE-06` opens with exactly one governing ADR (`ADR-030`, unlike `SLICE-05`'s eventual four -- `ADR-027`/`028`/`029`/this one's own predecessor pattern), and the product owner chose to skip the separate propose-then-approve-then-decompose cycle for that one ADR. **There is no separate `SLICE-06_BACKLOG.md` prerequisite document, and none is planned** -- this file is the only backlog document for this slice.

This backlog does **not** itself implement anything. It decomposes the slice into ordered child tasks, each its own separate task contract and pull request, activated one at a time. No child task contract file beyond `ODY-S06-101`'s own (already created by this same task) is created by this document; it only reserves numbers, titles, and boundaries.

Its sources of scope are, exclusively:

- `docs/adr/ADR-030_Rules_Engine_Mechanics_Execution_Architecture_v1.0.md` (`Accepted`) -- the Rules Engine architecture: the real `IAttackRulesEvaluator` implementation boundary, the shared formula grammar, the mechanics-primitive schema, the `RulesetId@RulesetVersion` registry, and the `ActivateAbility` architecture.
- The product owner's own MVP scenario, restated here without embellishment: two Characters, each with gear (`SLICE-05`'s own Inventory/Equipment) and at least one ability, fight one combat encounter (`SLICE-05`'s own full attack pipeline) using real computed damage/effects (not test-fixture `Rules` classes), move between encounters, and fight a second encounter -- proven by a real, automated end-to-end test/harness, not a playable UI.
- Every already-`Accepted`/merged `SLICE-05` ADR and implementation (`ADR-027`/`028`/`029`, `ODY-S05-101`-`611`), reused as fixed, unmodified prerequisite infrastructure -- this backlog does not reopen any of it.

No child task in this backlog reopens any decision `ADR-027`/`028`/`029`/`030` already made; each builds directly on those contracts as fixed.

## 2. Exit criteria for this revision (Rules Engine / Mechanics Execution block only)

This is **not** the full `SLICE-06` exit criteria for the whole MVP scenario -- only for the first block this revision decomposes in detail:

- `ADR-030` is `Accepted` (this task).
- `ODY-S06-102` (real `IAttackRulesEvaluator` implementation + formula grammar) and `ODY-S06-103` (`ActivateAbility`) are numbered and reserved, with enough scope named in section 9 below for their own future task contracts to be written without re-deciding `ADR-030`'s own architecture.
- Every later roadmap block (B through G, section 8) is named and reserved -- not scoped, not detailed, per the same "named, not scoped" convention `SLICE-05` section 8 already established for its own future blocks.

## 3. Roadmap block mapping

The product owner's own MVP scenario decomposes into lettered blocks; this document reserves task-number ranges under each letter as that block is activated for detailed decomposition. Letter **A** (this block) is decomposed in full below (section 9); letters **B**-**G** are named only (section 8).

| Letter | Block | Status |
|---|---|---|
| A | Rules Engine / Mechanics Execution (`ADR-030`, real `IAttackRulesEvaluator`, `ActivateAbility`) | Decomposed by this revision (section 9) |
| B | Equipment/armor effect on attack resolution | Named, reserved (section 8) |
| C | Movement/distance in combat | Named, reserved (section 8) |
| D | Item use (`D1` = active-item `Instant` effect use, reusing `ADR-030`'s own engine per that ADR's section 10) | Named, reserved (section 8) |
| E | Real MVP content (a real weapon, a real ability, a real effect, authored through the existing `SLICE-05` catalog pipeline) | Named, reserved (section 8) |
| F | End-to-end MVP scenario test/harness (two characters, gear, abilities, combat, movement, second combat) | Named, reserved (section 8) |
| G | Ability activation command (`ActivateAbility`) | Decomposed by this revision as `ODY-S06-103`, under block A's own number range (see section 9's own note on why `G`'s own task lives numerically inside `A`'s range) |

## 4. Global non-goals (this revision)

- A full, balanced, shippable game system's worth of content, formulas, or Ruleset rules -- only the architecture (`ADR-030`) and the minimum real content block E's own future task needs for the MVP scenario.
- A playable Unity UI for any block -- the product owner confirmed the MVP scenario is proven by an automated test/harness (block F), matching the same "documentation/test proof only, no Unity UI" convention every `SLICE-05` integration-fixture task already used.
- `11_Content_Block_System`'s own full `ContentBlockGraph` -- `ADR-030` §7.3 fixes this as out of scope for the whole slice's own MVP scope, not only this first block.
- Reopening any `SLICE-05` decision, `ADR-027`/`028`/`029`, or the `SLICE-05` backlog itself -- `SLICE-05` is closed and is reused as fixed prerequisite infrastructure only.
- Reopening `ADR-025`'s own `RulesetDefinitionCatalog`/character-ruleset-migration mechanism -- `ADR-030` §4/§8 explicitly distinguishes it from this slice's own new rules registry and does not touch it.

## 5. No new prerequisite ADR-proposal backlog needed

Unlike `SLICE-05`'s own `docs/tasks/SLICE-05_BACKLOG.md` (a dedicated document proposing `ADR-027` before any implementation backlog existed), `SLICE-06` opens directly with this single implementation backlog, because `ADR-030` itself is proposed and accepted within the very first task of this backlog (`ODY-S06-101`), by explicit product-owner authorization recorded in that task's own contract. See section 1's own "explicit process simplification" for the full reasoning. This section exists only to make the absence of a separate prerequisite backlog document a conscious, documented choice, not a silent gap a future reader might mistake for an oversight.

## 6. Scope decisions requiring explicit justification

1. **One ADR, not a phased sequence of several.** `SLICE-05` needed four ADRs (`027`/`028`/`029`, each unblocking the next) because its own scope (catalog, inventory, effects, attack) was large enough that each sub-domain genuinely needed its own architectural decision before the next could safely build on it. `SLICE-06`'s own first block is narrower -- one architectural question ("how does declarative content become a mutating outcome") -- so one ADR (`ADR-030`) suffices; this is not a precedent for skipping ADRs generally, only a reflection of this block's own genuinely smaller scope.
2. **`ActivateAbility` (roadmap block G) is decomposed as `ODY-S06-103`, inside block A's own number range, not as its own separate lettered block's number range.** `ADR-030` §9 fixes `ActivateAbility`'s entire architecture as part of the Rules Engine ADR itself (it is one of the three call sites the "one engine, three call sites" decision names) -- there is no independent architectural question left for a separate block G decomposition to resolve that block A's own `ADR-030` has not already answered. Numbering it under block A keeps the task-number sequence contiguous with the ADR that actually specifies it.

## 7. Dependency rules

- `ODY-S06-101` has no dependency -- it is the foundational architecture (`ADR-030`) every later task in this slice builds on. It depends on, and does not reopen, the completed `SLICE-05` (`ADR-027`/`028`/`029`, `ODY-S05-101`-`611`).
- `ODY-S06-102` depends on `ODY-S06-101` (needs `ADR-030`'s own formula-grammar and evaluator-architecture decisions to implement against) and on the completed full attack pipeline (`ODY-S05-601`-`611`, the existing `IAttackRulesEvaluator` consumer and `AttackDelta`/`AttackEffectCandidate` shapes it must produce).
- `ODY-S06-103` depends on `ODY-S06-101` (needs `ADR-030`'s own mechanics-primitive schema and `ActivateAbility` architecture) and, for its own `AdjustResource`/`ApplyEffect` primitive application, on the completed `ODY-S05-609` (`AttackDelta`-style signed resource adjustment idiom, reused) and `ODY-S05-502` (`IActiveEffectRepository.CreateActiveEffect`, reused).
- Later blocks (B/C/D/E/F, section 8) are not yet decomposed into numbered tasks; their own future decomposition task will record their own dependency chain against `ODY-S06-101`/`102`/`103` and each other at that time.

## 8. Reserved future blocks (not decomposed in this revision)

Per section 3's own lettering:

- **Block B -- Equipment/armor effect on attack resolution.** Named by the roadmap; not scoped, not numbered beyond this reservation. A future decomposition task extends `ODY-S06-102`'s own real `IAttackRulesEvaluator` to consult equipped armor/weapon state (`SLICE-05`'s own Equipment runtime, `ODY-S05-301`-`306`) rather than the MVP-scope placeholder behavior `ADR-030` §5 explicitly allows for its own first version.
- **Block C -- Movement/distance in combat.** Named by the roadmap; not scoped. A future decomposition task extends the attack pipeline's own range/targeting evaluation to consult real combat-position/movement state, which does not exist anywhere in this codebase yet.
- **Block D -- Item use.** Named by the roadmap; not scoped beyond `ADR-030` §10's own explicit forward reference (item-use `Instant`-effect application reuses this slice's own mechanics engine, never a third independent execution path). `D1` specifically is the MVP-scenario-relevant sub-item (using one active item once).
- **Block E -- Real MVP content.** Named by the roadmap; not scoped. A future decomposition task authors, through the already-existing `SLICE-05` catalog authoring/validation/publish pipeline (unmodified), the specific weapon/ability/effect definitions the MVP scenario (block F) actually exercises.
- **Block F -- End-to-end MVP scenario test/harness.** Named by the roadmap; not scoped. A future decomposition task proves the whole scenario (two characters, gear, abilities, combat, movement, second combat) end-to-end, mirroring `SLICE-05`'s own `ODY-S05-608`-style integration-fixture precedent -- composing already-accepted public services, introducing no new production behavior unless a genuine, disclosed gap forces a tiny, explicitly-justified fixture hook.

None of these five blocks is decomposed into real task IDs by this revision -- decomposing any of them is a future backlog revision, per the same "named, not scoped" rule `SLICE-05` section 8 already established and this document reuses verbatim.

## 9. Ordered backlog (Rules Engine / Mechanics Execution block)

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S06-101` | In Review | Product-owner MVP scenario; this document's own section 1 | ADR-030 — Rules Engine / Mechanics Execution Architecture | None (first task of `SLICE-06`) | ExecPlan | `ADR-030`, deciding the real `IAttackRulesEvaluator` implementation boundary, the shared formula grammar (a documented `DiceFormulaParser` superset), the `AbilityDefinition`/`EffectDefinition` mechanics-primitive schema, the `RulesetId@RulesetVersion` rules registry, and the `ActivateAbility` architecture. No production code. Creates this backlog document. |
| 2 | `ODY-S06-102` | Proposed | `ADR-030` §5/§6 | Real `IAttackRulesEvaluator` Implementation + Formula Grammar | 101 | ExecPlan (expected -- introduces real production code touching the attack pipeline's own consumer) | The concrete `IAttackRulesEvaluator` implementation (`ADR-030` §5) and the new formula-grammar parser (`ADR-030` §6), computing real `AttackDelta`/`AttackEffectCandidate` values from `WeaponDefinition.DamageExpression` and referenced effects, plugged into the already-existing, unmodified `AttackEvaluationService`/full attack pipeline. |
| 3 | `ODY-S06-103` | Proposed | `ADR-030` §7/§9 (roadmap block G) | `ActivateAbility` Command | 101 | ExecPlan (expected -- introduces a new root command and real production mutation) | The `ActivateAbility` root command (`ADR-030` §9): resolves `CharacterAbilityId` through the content catalog, interprets `MechanicsPayloadRef` through the mechanics-primitive interpreter (`ADR-030` §7), and applies the result atomically, outside `CombatEncounter` state per `ADR-030` §9.3's own explicit MVP decision. |

### 9.1 Rules Engine block task boundaries

`ODY-S06-101` owns the architecture only (an ADR document, no code) -- it must not implement the evaluator, the parser, the primitive interpreter, or `ActivateAbility` itself, and it must not decompose blocks B-F beyond naming/reserving them (section 8).

`ODY-S06-102` owns the real `IAttackRulesEvaluator` and the formula grammar. It must not implement `ActivateAbility`, must not modify `DiceFormulaParser`/`DiceFormula.cs` (`ADR-030` §6.1's own explicit constraint), and must not implement equipment/armor or movement/distance consultation (blocks B/C) beyond whatever MVP-scope placeholder `ADR-030` §5 already allows.

`ODY-S06-103` owns `ActivateAbility` and the mechanics-primitive interpreter's own first real implementation (shared with, but not owned exclusively by, `102` -- the two tasks' own contracts must agree on which one first implements the shared interpreter code `ADR-030` §7 specifies, recorded explicitly in whichever task's contract lands second, not left ambiguous). It must not implement combat-turn-consuming ability activation (`ADR-030` §9.3's own disclosed non-goal) or item use (block D).

## 10. Backlog change control

- New work requires a task contract; this document reserves numbers `ODY-S06-101` through `ODY-S06-103` for the Rules Engine / Mechanics Execution block (section 9), decomposed by `ODY-S06-101` itself in the same task/PR that created this document (section 1's own explicit process simplification).
- Blocks B, C, D, E, and F (section 8) are named, not scoped -- decomposing any of them into real task IDs is a future backlog revision, not an implicit extension of this one, per the same rule `SLICE-05` section 8/§11 already established.
- A task may be split before implementation by updating this backlog, following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or `ADR-030`'s content; it does not itself decide any technical question beyond the two explicit scope decisions in section 6.
- If this document's own section 6 narrowing decisions are later found incorrect, that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning -- this document would gain an explicit amendment note, not a rewritten section 6, mirroring `SLICE-05`'s own established convention for this exact situation.
