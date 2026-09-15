# Odyssey VTT — SLICE-06 Rules Engine, Mechanics Execution, and MVP Scenario Implementation Backlog

**Status:** Implementation revision — OPEN. First block (`ODY-S06-101`-`106`, Rules Engine / Mechanics Execution) is being decomposed by this document. All later blocks (roadmap letters D-F below) are named and reserved only, not yet decomposed.
**Slice:** `SLICE-06 — Rules Engine, Mechanics Execution, and the two-character MVP scenario (implementation)`
**Parent task:** `docs/tasks/active/ODY-S06-101_ADR030_Rules_Engine_Mechanics_Execution_Architecture.md`
**Predecessor backlog:** None -- see section 1's own explicit process simplification.
**ExecPlan:** Not required for this document itself (Brief plan for the backlog document; each child task chooses its own planning mode).
**Created:** 2026-09-17
**Last updated:** 2026-09-15 UTC -- see section 10's own `ODY-S06-102` restructuring entry.

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
- `ODY-S06-102` (attribute data in the attack snapshot), `ODY-S06-103` (equip status/armor wiring), `ODY-S06-104` (character↔token/position/range wiring), `ODY-S06-105` (the real `IAttackRulesEvaluator` implementation + formula grammar, now building on `102`-`104`'s own data rather than a MVP-scope placeholder), and `ODY-S06-106` (`ActivateAbility`) are numbered and reserved, with enough scope named in section 9 below for their own future task contracts to be written without re-deciding `ADR-030`'s own architecture.
- Every later roadmap block (D through F, section 8) is named and reserved -- not scoped, not detailed, per the same "named, not scoped" convention `SLICE-05` section 8 already established for its own future blocks.

## 3. Roadmap block mapping

The product owner's own MVP scenario decomposes into lettered blocks; this document reserves task-number ranges under each letter as that block is activated for detailed decomposition. Letter **A** (this block) is decomposed in full below (section 9); letters **D**-**F** are named only (section 8).

**Revision note (`ODY-S06-102`, 2026-09-15):** the product owner reordered block A's own internal sequence -- rather than shipping a minimal, unconditional-hit `IAttackRulesEvaluator` first and retrofitting armor/range later, all missing input data (attributes, armor, position/range) is wired into the attack snapshot first, and the real evaluator is built once, already consulting all of it. Blocks B and C (previously "named, reserved (section 8)" only) are therefore now decomposed into numbered, reserved tasks under block A's own range (`103`/`104`), not left in section 8. See section 10's own restructuring entry for the full reasoning.

| Letter | Block | Status |
|---|---|---|
| A | Rules Engine / Mechanics Execution (`ADR-030`, attribute/armor/position data wiring, real `IAttackRulesEvaluator`, `ActivateAbility`) | Decomposed by this revision (section 9) |
| B | Equipment/armor effect on attack resolution | Decomposed by this revision as `ODY-S06-103`, under block A's own number range |
| C | Movement/distance in combat | Decomposed by this revision as `ODY-S06-104`, under block A's own number range |
| D | Item use (`D1` = active-item `Instant` effect use, reusing `ADR-030`'s own engine per that ADR's section 10) | Named, reserved (section 8) |
| E | Real MVP content (a real weapon, a real ability, a real effect, authored through the existing `SLICE-05` catalog pipeline) | Named, reserved (section 8) |
| F | End-to-end MVP scenario test/harness (two characters, gear, abilities, combat, movement, second combat) | Named, reserved (section 8) |
| G | Ability activation command (`ActivateAbility`) | Decomposed by this revision as `ODY-S06-106`, under block A's own number range (see section 9's own note on why `G`'s own task lives numerically inside `A`'s range) |

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
2. **`ActivateAbility` (roadmap block G) is decomposed as `ODY-S06-106`, inside block A's own number range, not as its own separate lettered block's number range.** `ADR-030` §9 fixes `ActivateAbility`'s entire architecture as part of the Rules Engine ADR itself (it is one of the three call sites the "one engine, three call sites" decision names) -- there is no independent architectural question left for a separate block G decomposition to resolve that block A's own `ADR-030` has not already answered. Numbering it under block A keeps the task-number sequence contiguous with the ADR that actually specifies it.
3. **The real `IAttackRulesEvaluator` implementation (`ODY-S06-105`) is deliberately sequenced AFTER the data-wiring tasks it will consume (`102` attributes, `103` armor, `104` position/range), not before them.** The product owner reconsidered the originally reserved order (a minimal, unconditional-hit evaluator first, blocks B/C retrofitted later) and decided the evaluator should be built once, already consulting all the input data `ADR-030` §5 names, rather than shipped once as a placeholder and then revised three more times as each data source lands. This is a sequencing decision, not an `ADR-030` architecture change -- `ADR-030` §5's own "MVP evaluator's own first version may treat a hit as unconditional and armor as absent" language remains true only until `105` is actually built, at which point it should not need to be true anymore.

## 7. Dependency rules

- `ODY-S06-101` has no dependency -- it is the foundational architecture (`ADR-030`) every later task in this slice builds on. It depends on, and does not reopen, the completed `SLICE-05` (`ADR-027`/`028`/`029`, `ODY-S05-101`-`611`).
- `ODY-S06-102` depends on `ODY-S06-101` (`AttackParticipantState` is `Odyssey.Domain`, and `ADR-030` §6.2's own `attributeReference` term is what this data ultimately feeds) and on the completed full attack pipeline (`ODY-S05-601`-`611`, whose already-loaded `CharacterRecord` this task reads from -- no new database read).
- `ODY-S06-103` (equip status/armor wiring, roadmap block B) depends on `ODY-S06-101` and on `SLICE-05`'s own Equipment runtime (`ODY-S05-301`-`306`); does not depend on `102`, but is sequenced after it for delivery order, not architecture.
- `ODY-S06-104` (character↔token/position/range wiring, roadmap block C) depends on `ODY-S06-101`; does not depend on `102`/`103`, but is sequenced after them for delivery order, not architecture.
- `ODY-S06-105` (the real `IAttackRulesEvaluator` implementation + formula grammar) depends on `ODY-S06-101` (needs `ADR-030`'s own formula-grammar and evaluator-architecture decisions), `102` (attribute data, for `attributeReference` term resolution), `103` (armor data), and `104` (position/range data) -- this is the task order change section 6 point 3 records: the evaluator is built once other than as a placeholder, after its own real inputs exist.
- `ODY-S06-106` depends on `ODY-S06-101` (needs `ADR-030`'s own mechanics-primitive schema and `ActivateAbility` architecture) and, for its own `AdjustResource`/`ApplyEffect` primitive application, on the completed `ODY-S05-609` (`AttackDelta`-style signed resource adjustment idiom, reused) and `ODY-S05-502` (`IActiveEffectRepository.CreateActiveEffect`, reused). It does not technically depend on `102`-`105` and may be implemented in parallel with them; its number is sequential only for organizational continuity (section 6 point 2), not a dependency ordering.
- Later blocks (D/E/F, section 8) are not yet decomposed into numbered tasks; their own future decomposition task will record their own dependency chain against `ODY-S06-101`-`106` and each other at that time.

## 8. Reserved future blocks (not decomposed in this revision)

Per section 3's own lettering (blocks B and C were reserved here as of `ODY-S06-101`; `ODY-S06-102` moved them into section 9 as numbered tasks `103`/`104` -- see section 10's own restructuring entry):

- **Block D -- Item use.** Named by the roadmap; not scoped beyond `ADR-030` §10's own explicit forward reference (item-use `Instant`-effect application reuses this slice's own mechanics engine, never a third independent execution path). `D1` specifically is the MVP-scenario-relevant sub-item (using one active item once).
- **Block E -- Real MVP content.** Named by the roadmap; not scoped. A future decomposition task authors, through the already-existing `SLICE-05` catalog authoring/validation/publish pipeline (unmodified), the specific weapon/ability/effect definitions the MVP scenario (block F) actually exercises.
- **Block F -- End-to-end MVP scenario test/harness.** Named by the roadmap; not scoped. A future decomposition task proves the whole scenario (two characters, gear, abilities, combat, movement, second combat) end-to-end, mirroring `SLICE-05`'s own `ODY-S05-608`-style integration-fixture precedent -- composing already-accepted public services, introducing no new production behavior unless a genuine, disclosed gap forces a tiny, explicitly-justified fixture hook.

None of these three blocks is decomposed into real task IDs by this revision -- decomposing any of them is a future backlog revision, per the same "named, not scoped" rule `SLICE-05` section 8 already established and this document reuses verbatim.

## 9. Ordered backlog (Rules Engine / Mechanics Execution block)

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S06-101` | In Review | Product-owner MVP scenario; this document's own section 1 | ADR-030 — Rules Engine / Mechanics Execution Architecture | None (first task of `SLICE-06`) | ExecPlan | `ADR-030`, deciding the real `IAttackRulesEvaluator` implementation boundary, the shared formula grammar (a documented `DiceFormulaParser` superset), the `AbilityDefinition`/`EffectDefinition` mechanics-primitive schema, the `RulesetId@RulesetVersion` rules registry, and the `ActivateAbility` architecture. No production code. Creates this backlog document. |
| 2 | `ODY-S06-102` | In Review | `ADR-030` §6.2 (`attributeReference` term); this document's own restructuring, section 10 | Attribute Data in Attack Snapshot | 101 | ExecPlan | `AttackParticipantState` (`Odyssey.Domain`) gains real character-attribute `EffectiveValue` readings, keyed by the existing `AttributeDefinitionId` catalog key; `SqliteAttackStateReader.Read` copies them from the already-loaded `CharacterRecord.Attributes` for the actor and every target -- no new database read, no interpretation. Closes the first of three data gaps blocking `105`. |
| 3 | `ODY-S06-103` | Proposed | Roadmap block B | Equip Status + Armor Wiring Into Attack State | 101 | Not yet decided (future task's own contract) | Extends the attack snapshot with the acting/target characters' own currently-equipped armor/weapon state (`SLICE-05`'s own Equipment runtime, `ODY-S05-301`-`306`), closing the second data gap blocking `105`. Not scoped beyond this one-line reservation; a future decomposition task fixes the exact shape. |
| 4 | `ODY-S06-104` | Proposed | Roadmap block C | Character↔Token Link + Encounter Participant Position + Range Check Wiring | 101 | Not yet decided (future task's own contract) | Extends the attack snapshot with real combat-position/range data (character↔token linkage, participant position, a real range check), closing the third data gap blocking `105`. Not scoped beyond this one-line reservation; a future decomposition task fixes the exact shape. |
| 5 | `ODY-S06-105` | Proposed | `ADR-030` §5/§6 | Real `IAttackRulesEvaluator` Implementation + Formula Grammar | 101, 102, 103, 104 | ExecPlan (expected -- introduces real production code touching the attack pipeline's own consumer) | The concrete `IAttackRulesEvaluator` implementation (`ADR-030` §5) and the new formula-grammar parser (`ADR-030` §6), computing real `AttackDelta`/`AttackEffectCandidate` values from `WeaponDefinition.DamageExpression` and referenced effects, consulting `102`-`104`'s own real attribute/armor/position data rather than the MVP-scope unconditional-hit placeholder `ADR-030` §5 describes as a fallback only. Plugged into the already-existing, unmodified `AttackEvaluationService`/full attack pipeline. |
| 6 | `ODY-S06-106` | Proposed | `ADR-030` §7/§9 (roadmap block G) | `ActivateAbility` Command | 101 | ExecPlan (expected -- introduces a new root command and real production mutation) | The `ActivateAbility` root command (`ADR-030` §9): resolves `CharacterAbilityId` through the content catalog, interprets `MechanicsPayloadRef` through the mechanics-primitive interpreter (`ADR-030` §7), and applies the result atomically, outside `CombatEncounter` state per `ADR-030` §9.3's own explicit MVP decision. |

### 9.1 Rules Engine block task boundaries

`ODY-S06-101` owns the architecture only (an ADR document, no code) -- it must not implement the evaluator, the parser, the primitive interpreter, or `ActivateAbility` itself, and it must not decompose blocks D-F beyond naming/reserving them (section 8).

`ODY-S06-102` owns only the attribute-data wiring into `AttackParticipantState`/`SqliteAttackStateReader`. It must not implement the formula grammar, the mechanics-primitive interpreter, `CoreAttackRulesEvaluator`/the real evaluator itself (`105`'s territory), or `ActivateAbility` (`106`'s territory); it must not touch armor/equipment (`103`) or position/range (`104`) data.

`ODY-S06-103` owns armor/equipment data wiring into the attack snapshot only -- it must not implement the real evaluator's own consumption of that data (`105`'s territory) beyond making the data reachable.

`ODY-S06-104` owns position/range data wiring into the attack snapshot only -- it must not implement the real evaluator's own consumption of that data (`105`'s territory) beyond making the data reachable.

`ODY-S06-105` owns the real `IAttackRulesEvaluator` and the formula grammar, now consulting `102`-`104`'s own real data. It must not implement `ActivateAbility`, must not modify `DiceFormulaParser`/`DiceFormula.cs` (`ADR-030` §6.1's own explicit constraint), and must not re-wire `102`-`104`'s own data sources.

`ODY-S06-106` owns `ActivateAbility` and the mechanics-primitive interpreter's own first real implementation (shared with, but not owned exclusively by, `105` -- the two tasks' own contracts must agree on which one first implements the shared interpreter code `ADR-030` §7 specifies, recorded explicitly in whichever task's contract lands second, not left ambiguous). It must not implement combat-turn-consuming ability activation (`ADR-030` §9.3's own disclosed non-goal) or item use (block D).

## 10. Backlog change control

- **2026-09-15 restructuring (`ODY-S06-102`'s own governing ТЗ, product-owner directed).** The block A internal sequence originally reserved by `ODY-S06-101` (`102` = real evaluator, `103` = `ActivateAbility`) is replaced by this revision: the product owner decided to build the full evaluator once, already consulting attributes/armor/position, instead of shipping a minimal unconditional-hit evaluator first and revising it three more times as blocks B/C data arrived. Concretely: `102` is redefined from "Real `IAttackRulesEvaluator` + Formula Grammar" to "Attribute Data in Attack Snapshot" (this task, `ODY-S06-102`, already `In Review`); new reserved rows `103` ("Equip Status + Armor Wiring Into Attack State", formerly roadmap block B) and `104` ("Character↔Token Link + Encounter Participant Position + Range Check Wiring", formerly roadmap block C) are inserted; the real evaluator (formerly `102`) is renumbered `105`; `ActivateAbility` (formerly `103`) is renumbered `106` -- it has no technical dependency on `102`-`105` and may be implemented in parallel, but keeps a sequential number for organizational continuity (section 6 point 2, unchanged reasoning, renumbered). No separate file-based task-contract stub existed for the old `102` ("real evaluator") reservation -- it was a backlog-table row only, so nothing needed renaming or deleting beyond this table itself. `ADR-030` itself is not reopened or revised by this restructuring; every decision it already made (the evaluator's own architecture, the formula grammar, the mechanics-primitive schema) still governs `105`/`106` unchanged -- only the delivery ORDER of the data these decisions consume has changed.
- New work requires a task contract; this document reserves numbers `ODY-S06-101` through `ODY-S06-106` for the Rules Engine / Mechanics Execution block (section 9), decomposed by `ODY-S06-101` itself in the same task/PR that created this document (section 1's own explicit process simplification), restructured by `ODY-S06-102` per the entry above.
- Blocks D, E, and F (section 8) are named, not scoped -- decomposing any of them into real task IDs is a future backlog revision, not an implicit extension of this one, per the same rule `SLICE-05` section 8/§11 already established.
- A task may be split before implementation by updating this backlog, following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or `ADR-030`'s content; it does not itself decide any technical question beyond the two explicit scope decisions in section 6.
- If this document's own section 6 narrowing decisions are later found incorrect, that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning -- this document would gain an explicit amendment note, not a rewritten section 6, mirroring `SLICE-05`'s own established convention for this exact situation.
