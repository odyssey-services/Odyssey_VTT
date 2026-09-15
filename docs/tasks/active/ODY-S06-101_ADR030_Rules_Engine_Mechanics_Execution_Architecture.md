# ODY-S06-101 — ADR-030: Rules Engine / Mechanics Execution Architecture

## 1. Task identity
`ODY-S06-101`; status: In Review (Draft PR [#157](https://github.com/odyssey-services/Odyssey_VTT/pull/157), CI green, merge left to the product owner). First task of `SLICE-06`.

## 2. Goal
Produce and accept `ADR-030`, the architecture that turns declarative Ruleset content into real mutating outcomes: the real `IAttackRulesEvaluator` implementation boundary, a shared formula grammar for `WeaponDefinition.DamageExpression`, a minimal mechanics-primitive schema for `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef`, a `RulesetId@RulesetVersion` rules registry, and the `ActivateAbility` root-command architecture -- one shared engine, not three independent mechanisms. Also create `docs/tasks/SLICE-06_IMPLEMENTATION_BACKLOG.md` and a `PLANS.md` §13 amendment in the same task/PR (product-owner-authorized simplification of `SLICE-05`'s own two-phase propose-then-decompose process). This task produces no production code.

## 3. Authority
`ADR-029`'s own §10 non-goal ("without selecting a game system's hit formula ... or content payload schema") naming exactly this gap; `ADR-027`/`ODY-S05-105`'s own `WeaponDefinition.DamageExpression`/`AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef` doc comments, each explicitly deferring to a not-yet-built mechanism this ADR now specifies; `09_Dice_And_Game_Log` §7 (`DiceFormulaParser`, investigated directly, section 6 of the ADR); `ADR-001` §6.2 (`Odyssey.Rules` module boundary); `ADR-025` §7.2 (`RulesetDefinitionCatalog`, distinguished, not reused); `ODY-S05-601`'s own direct precedent (an ADR-only task preceding a whole implementation block).

## 4. In scope
`docs/adr/ADR-030_Rules_Engine_Mechanics_Execution_Architecture_v1.0.md` (18-section ADR template, matching `ADR-028`/`029`'s own structure): the "one engine, three call sites" decision; the real `IAttackRulesEvaluator` implementation's own architecture (not its code); a documented formula-grammar superset of `DiceFormulaParser`, with the investigation of that parser's own code recorded as the reason it cannot be reused unmodified; a minimal, closed mechanics-primitive schema (`AdjustResource`/`ApplyEffect`) for `MechanicsPayloadRef`; a `RulesetId@RulesetVersion` rules registry design reusing the existing `CatalogValidationContracts.cs` cache-key convention; the `ActivateAbility` root-command architecture, explicitly decided as non-combat-bound for MVP; an explicit forward reference to item-use (roadmap block D) reusing the same engine. `docs/tasks/SLICE-06_IMPLEMENTATION_BACKLOG.md` (new file, structurally modeled on `SLICE-05_IMPLEMENTATION_BACKLOG.md`): header, dependency rules, backlog change control, the Rules Engine block (`101`-`103`) fully decomposed, later roadmap blocks (B-G) named and reserved only. `PLANS.md` §13.2 amendment recording `SLICE-06`'s insertion.

## 5. Out of scope
Any production code (`Packages/**`, `DotNet/**`, `Assets/**`) -- this task designs, it does not implement. Revising `ADR-027`/`028`/`029` (referenced, not amended). `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (closed, not touched). `RulesetDefinitionCatalog`/`RulesetMigrationRules.cs` (`ADR-025`'s own territory, referenced by naming-convention analogy only). `11_Content_Block_System`'s own full `ContentBlockGraph`. Detailed task contracts for `ODY-S06-102`/`103` or any roadmap block B-G task -- this task reserves numbers/names, it does not write their contracts. Changing `IAttackRulesEvaluator`'s own interface signature.

## 6. Domain contract
No Domain-layer changes. The ADR references existing Domain types verbatim (`WeaponDefinition`, `AbilityDefinition`, `EffectDefinition`, `CharacterAbility`, `AttributeValue`, `ResourceDefinitionId`, `ContentDefinitionRef`, `DiceFormula`/`DiceFormulaParser`) and specifies that the new formula grammar/mechanics-primitive schema introduce zero new Domain identity types -- they reuse `ResourceDefinitionId`/`ContentDefinitionRef` exactly as already accepted.

## 7. Application contract
No Application-layer changes. The ADR specifies (for future tasks to implement) that `ActivateAbility` mirrors `AttackApplyService`'s own existing Application-layer orchestration shape, and that the ruleset registry is consulted by whichever Application-layer/host-composition code already reads `campaign.Manifest.RulesetId`/`RulesetVersion` today.

## 8. Persistence boundary
No Persistence-layer changes. The ADR specifies that the mechanics-primitive envelope (`MechanicsPayloadRef`'s own JSON content) is decoded by a new, separate decode method -- deliberately not folded into `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect`, which decode the already-accepted catalog shape -- and that `ApplyEffect`/`AdjustResource` primitive application reuses already-accepted persistence idioms verbatim (`ODY-S05-502`'s `CreateActiveEffect`, `ODY-S05-609`'s signed-delta convention).

## 9. Tests and validation
No new tests -- a document-only deliverable, matching `ODY-S05-601`'s own precedent exactly. Validation is `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` (confirming the doc-only diff introduces no policy violation) plus manual review: that `DiceFormulaParser`'s own code was genuinely read (not assumed) before the reuse decision in ADR §6.1, that all six of the governing ТЗ's own §2 design questions are resolved (not deferred) in the ADR text, that `MechanicsPayloadRef`'s own schema is genuinely narrow (not a `ContentBlockGraph`), and that `IAttackRulesEvaluator`'s own interface text is quoted unchanged.

## 10. Compatibility and rollback
No production code touched -- nothing to be compatible with or roll back beyond reverting the new ADR/backlog/`PLANS.md` amendment/task-contract/ExecPlan files. No schema change.

## 11. Security and privacy
Not applicable -- a document-only deliverable. The ADR itself specifies (for `ActivateAbility`, a future task) that authorization reuses the existing `ADR-019` actor-control/MainGM model verbatim, no new permission concept.

## 12. Observability
Not applicable -- a document-only deliverable.

## 13. Performance
Not applicable -- a document-only deliverable. The ADR itself notes the ruleset registry is a pure in-process dictionary lookup with no I/O, and the formula grammar/primitive interpreter are pure functions with no additional cache/scheduler dependency.

## 14. Dependencies
The completed `SLICE-05` (`ADR-027`/`028`/`029`, `ODY-S05-101`-`611`, all merged) as fixed, unmodified prerequisite infrastructure. `09_Dice_And_Game_Log` §7 (`DiceFormulaParser`, investigated, reused by reference to its own published constants only). `ADR-001` §6.2 (module boundary rules applied directly).

## 15. Dependencies (packages)
None new.

## 16. Implementation plan
See the active ExecPlan (`docs/plans/active/ODY-S06-101_ADR030_Rules_Engine_Mechanics_Execution_Architecture.md`) -- ExecPlan, not Brief plan, per this task's own governing ТЗ (an architectural task of comparable scope to `ODY-S05-601`, which itself chose Brief plan under different governing instructions; this task's own ТЗ explicitly directs ExecPlan instead).

## 17. Completion evidence
Authored `ADR-030` (18 sections, matching `ADR-028`/`029`'s own structural template), resolving all six of the governing ТЗ's own §2 design questions explicitly: one shared Rules Engine architecture (§2.1); a documented, code-verified decision that `DiceFormulaParser` cannot be reused unmodified for `WeaponDefinition.DamageExpression` (no attribute-reference term in its grammar, `DiceTerm`'s constructor is `internal`, extending it would reopen `ADR-009`), with a new superset grammar decided instead, reusing `DiceFormulaParser`'s own published constants by reference (§2.2); a minimal two-primitive (`AdjustResource`/`ApplyEffect`) mechanics schema for `MechanicsPayloadRef`, explicitly bounded away from `11_Content_Block_System`'s own `ContentBlockGraph` (§2.3); a `RulesetId@RulesetVersion` registry reusing `CatalogValidationContracts.cs`'s own existing cache-key convention verbatim, distinguished explicitly from `RulesetDefinitionCatalog` (§2.4); the `ActivateAbility` architecture, explicitly decided as non-combat-bound for MVP (§2.5); item-use named as a future consumer of the same engine (§2.6). Created `SLICE-06_IMPLEMENTATION_BACKLOG.md` with the Rules Engine block (`101`-`103`) decomposed and roadmap blocks B-G named/reserved. Added `PLANS.md` §13.2. `IAttackRulesEvaluator`'s own interface text is quoted verbatim, unchanged. Zero production files touched (`git diff --name-status` confirms).

## 18. Change control

### Decisions made during execution

- **`DiceFormulaParser` was read in full before any reuse decision was made -- its grammar (`expression = signedTerm, {("+"|"-"), term}; term = diceGroup | integer`) has no attribute-reference production, `DiceTermKind` is an exhaustive two-value enum, and `DiceTerm`'s own constructor is `internal`.** This is a concrete, code-verified fact, not an assumption -- confirmed by direct file read before writing `ADR-030` §6.1, exactly as the governing ТЗ required ("не оставлять этот вопрос на усмотрение последующей задачи ... с реальной проверкой его кода, а не предположением"). The decision that followed (a new, `Odyssey.Rules`-layer superset parser, referencing `DiceFormulaParser`'s own published constants rather than duplicating them or modifying that file) is the direct, documented consequence of that finding.
- **The mechanics-primitive schema was deliberately kept to exactly two primitives (`AdjustResource`, `ApplyEffect`), both reusing already-accepted Domain identity types (`ResourceDefinitionId`, `ContentDefinitionRef`) rather than inventing new ones.** This satisfies the governing ТЗ's own explicit requirement that the schema be "минимально достаточная для MVP ... не полноценный `ContentBlockGraph`" -- verified by the ADR's own §7.3, which lists concretely what the schema does NOT have (conditionals, loops, block composition, target-rule execution) rather than merely asserting narrowness.
- **`ActivateAbility` was decided as unconditional on `CombatEncounter` state for MVP, not left open.** The governing ТЗ explicitly required one explicit answer, not an open question -- `ADR-030` §9.3/§15.4 records both the decision and the rejected alternative (combat-only activation), with the reasoning (no existing `AbilityDefinition`/`CharacterAbility` field requires an encounter context; forcing one would couple the MVP scenario's own "activate an ability" beat to unrelated combat setup).
- **`SLICE-06_IMPLEMENTATION_BACKLOG.md` and `ADR-030` were created in the same task/PR, explicitly diverging from `SLICE-05`'s own two-phase (`SLICE-05_BACKLOG.md` proposal, then a later, separate implementation-backlog-creation task) precedent.** This is not a silent shortcut -- the governing ТЗ explicitly authorized it ("упрощение относительно двухфазного прецедента SLICE-05"), and the backlog document's own section 1/5 record the reasoning (one ADR, not several, for this first block) so a future reader does not mistake the absence of a separate prerequisite backlog for an oversight.
- **Roadmap block G (`ActivateAbility`) is numbered `ODY-S06-103`, inside block A's own number range, rather than starting its own separate lettered number range.** `ADR-030` §9 fixes `ActivateAbility`'s entire architecture as one of the three call sites the "one engine, three call sites" decision names -- there is no independent architectural question for a separate block G decomposition to resolve that the ADR has not already answered, so keeping its task number contiguous with the ADR that specifies it (rather than reserving a disjoint range for a letter with no separate architecture of its own) was judged clearer. Recorded explicitly in the backlog's own section 6 point 2, not left as an unexplained numbering choice.

### Blockers

- None.
