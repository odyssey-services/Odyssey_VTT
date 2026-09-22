# ADR-031 - Check / Contest Resolution Architecture

**Документ:** `docs/adr/ADR-031_Check_Contest_Resolution_Architecture_v1.0.md`
**ADR:** ADR-031
**Версия:** 1.0
**Дата:** 2026-09-22
**Статус:** Accepted
**Область:** the architecture for resolving an attribute/skill check (roll + modifier vs. a target difficulty) -- a mechanic that exists nowhere in this codebase today, in code or in any prior ADR. Fixes the RNG/dice substrate, the formula-grammar reuse, the check-time character snapshot (attributes AND skills), the pass/fail evaluator shape, the `CombatEncounter` binding, and the real wiring of the long-dormant `RecordCriticalSuccessEvidence` recording primitive. This ADR does not amend `ADR-001`-`ADR-030`; it fills a gap those ADRs never claimed to cover.
**Связанные этапы:** `SLICE-07`, task `ODY-S07-101`
**Базовые документы:** `ADR-001` §6.2 (`Odyssey.Rules` module boundary); `ADR-002` (commands, idempotency); `ADR-008` (deterministic host RNG); `ADR-009`/`09_Dice_And_Game_Log` §7 (`DiceFormulaParser`, `ODY-S03-005`'s `DiceRollService`); `ADR-019` (permissions/audience baseline); `ADR-030` (Rules Engine architecture -- the real `IAttackRulesEvaluator`, the shared `AttackDamageFormulaParser` grammar this ADR reuses verbatim, `ActivateAbility`'s own `CombatEncounter`-binding precedent).

---

# 1. Решение

Odyssey VTT resolves an attribute/skill check as a new, standalone root command, `SubmitCheck` (exact name left to the first implementation task, `ODY-S07-102`+), built entirely from already-accepted machinery -- no new RNG mechanism, no new formula grammar, no new random-purpose value, and no change to any existing production type. The following decisions are mandatory:

1. **The dice substrate is `DiceRollService.SubmitRoll` (`ODY-S03-005`), not a direct `IAuthoritativeRandomStream` draw.** Section 5 records the concrete reason: `DiceRollService` already provides GM-adjudicated modifier proposal/decision, mandatory-reason override, and full-reroll machinery (`ADR-019`'s own permission baseline, already wired) that a direct-RNG check command would otherwise have to reinvent from nothing. This reuses a real, tested, currently zero-production-caller feature rather than leaving it permanently unused while building a second, parallel roll mechanism beside it.
2. **`DiceRollService.SubmitRoll`'s own `formula` parameter is parsed by the ORIGINAL `DiceFormulaParser` (`ADR-009`), which has no attribute/skill-reference term.** A check formula such as `"1d20+Strength"` cannot be submitted to `SubmitRoll` as one string. Section 5.2 fixes the split: the check command parses the full expression through `AttackDamageFormulaParser` (`ADR-030` §6, reused verbatim, decision 3 below), submits only the dice-group sub-formula (`"1d20"`) as `SubmitRollRequest.Formula`, and passes the already-resolved attribute/skill modifier as one `AutomaticModifierRequest` -- a mechanism `DiceRollService` already has, built for exactly this shape ("system-determined modifiers applied at roll time without a separate GM decision").
3. **The formula grammar is `AttackDamageFormulaParser` (`ADR-030` §6.2) reused as-is -- no new parser, no `CheckFormulaParser`.** Its `attributeReference` term (`letter, {letter | digit | "_"}`) is a bare identifier lookup with no attribute-specific validation anywhere in the parser itself (`ADR-030` §6.2, confirmed again by direct code read for this ADR) -- it resolves equally well against a skill-keyed dictionary as an attribute-keyed one. This ADR names the term's own genericity explicitly: `AttackDamageFormulaParser`'s `attributeReference` production is a misleading name for what it actually is, a generic named-identifier term, and this ADR does not rename it -- continuing the same "reuse as named, note the mismatch, do not fork" technical-debt convention `ODY-S06-105`/`106`/`107` each already established for this exact parser.
4. **A new check snapshot carries BOTH attribute and skill values, by the same zero-extra-read copy `ODY-S06-102` established for attributes alone.** Section 6 fixes a new type (e.g. `CheckParticipantState`, exact name left to `ODY-S07-102`+) with two dictionaries -- `AttributeValues: IReadOnlyDictionary<AttributeDefinitionId, long>` and `SkillValues: IReadOnlyDictionary<SkillDefinitionId, long>` -- both copied from an already-loaded `CharacterRecord.Attributes`/`CharacterRecord.Skills` at read time, exactly mirroring `SqliteAttackStateReader.Read`'s own established `AttackParticipantState` population, never a second database query.
5. **The result is a new, `Odyssey.Rules`-layer pass/fail evaluator, not a `CoreAttackRulesEvaluator` extension.** Section 7 fixes a new `ICheckRulesEvaluator`/`CoreCheckRulesEvaluator` pair, structurally parallel to `IAttackRulesEvaluator`/`CoreAttackRulesEvaluator` but materially simpler: it never derives or consumes raw dice values itself (the dice substrate is `DiceRollService`, decision 1, an Application-layer concern outside `Odyssey.Rules` entirely) -- it resolves the non-dice (attribute/skill) portion of the formula (pure, no I/O, the same class of calculation `CoreAttackRulesEvaluator`'s own private formula evaluator already performs for attack damage) and, once `DiceRollService.SubmitRoll` returns a real `DiceRoll.FinalTotal`, compares that total against a caller-supplied difficulty class, producing at minimum pass/fail.
6. **Degrees of success (critical success/failure beyond plain pass/fail) and advantage/disadvantage (roll twice, take better/worse), including dedicated opposed-contest support, are CONFIRMED NON-GOALS for this first version.** Section 8 records both as named, disclosed non-goals, exactly the way `ADR-030` §12 disclosed "`ActivateAbility` as a combat-turn-consuming action" as a future extension rather than an oversight. Both were originally flagged for product-owner confirmation (this task's own best engineering judgment about MVP scope, not a claim of product authority the ADR did not have) and the product owner has since confirmed both (2026-09-22, доработка of this ADR after PR #165's own independent verification) -- see section 17 for the confirmed record.
7. **A check is a standalone command, never bound to `CombatEncounter` state -- the same argument `ADR-030` §9.3 already made for `ActivateAbility`, applying at least as strongly here.** Section 9 records that checks are used outside combat at least as often as in it (a locked-door Strength check, a Stealth check while sneaking, a Perception check while exploring -- none of these have a `CombatEncounter` to bind to), so requiring one would make the common case artificially depend on combat setup it does not need.
8. **A real critical success on a real check MUST call `RecordCriticalSuccessEvidence` with the real `DiceRoll.RollId` -- closing the gap `ODY-S04-106`/`114` explicitly left open.** Section 10 fixes the exact trigger condition (a single-die check-relevant dice-group term's own `NaturalResult.Value == NaturalResult.Sides`, i.e. a natural maximum roll, already representable by `DiceRoll.NaturalResults` with no new Domain field) and requires the real `DiceRoll.RollId` (a `string`, exactly `RecordCriticalSuccessEvidence`'s own `sourceDiceRollId: string?` parameter type) be passed -- never a synthetic literal, the way `ODY-S04-114`'s own test necessarily used one in the complete absence of a real dice-integrated caller.
9. **No new `RngPurpose` value is introduced.** Section 11 records that `DiceRollService.SubmitRoll` already fixes its own RNG-derivation purpose internally (`RngPurpose.Parse("dice.roll")`, a `private static readonly` field, not caller-controlled) regardless of the caller-supplied, purely descriptive `SubmitRollRequest.Purpose` string -- since decision 1 routes every check roll through `SubmitRoll`, the existing `"dice.roll"` purpose is reused automatically, with no code change and no new registered purpose string.
10. **Nothing already accepted is touched.** `IAttackRulesEvaluator`, `CoreAttackRulesEvaluator`, and `AttackDamageFormulaParser`'s own content are reused strictly by reference -- this ADR does not modify any of the three. `ActivateAbility`/`UseItem`'s own territory (`ADR-030` §9/§10, `ODY-S06-106`/`107`) is not touched or re-derived. `DiceRollService`'s own existing logic (`SubmitRoll`/`ProposeModifier`/`DecideModifier`/`ApplyOverride`/`RequestFullReroll`/`CancelRoll`) is consumed exactly as it stands today -- this ADR adds a new caller, not a new capability inside that service.

---

# 2. Контекст и проблема

A direct product-owner question about the platform's real, current capabilities -- independently verified for this task, not assumed -- confirmed that an attribute/skill check (roll a die, add a modifier, compare against a target difficulty) exists nowhere in this codebase: not in `Packages/**` production code, and not in any prior ADR. `ADR-030` itself, which built the entire Rules Engine / mechanics-execution architecture for attack resolution and ability activation, never claims to cover this -- its own §16 "Открытые вопросы" reads "None," and none of its sections 5-10 name checks, contests, or difficulty-class comparison anywhere. `CoreAttackRulesEvaluator.cs`'s own doc comment (`ODY-S06-105`) independently confirms the same gap from the opposite direction, describing why `Hit` currently mirrors `Range` exactly: "no separate to-hit mechanic exists yet (no armor-vs-hit roll, no attacker/defender contest)" -- the word "contest" appears in this codebase exactly once, in this sentence, documenting its own absence.

This is not a small gap. Attribute/skill checks are one of the most structurally fundamental mechanics any tabletop-style ruleset needs -- arguably more foundational than combat itself, since a check-shaped resolution (roll + modifier vs. target number) is how most non-combat player actions (persuasion, lockpicking, climbing, searching, sneaking) get resolved in the overwhelming majority of comparable systems, and checks are used at least as often outside combat as abilities are (`ADR-030` §9.3's own reasoning for unbinding `ActivateAbility` from `CombatEncounter` applies here with at least equal force). The product owner decided that closing this gap -- along with four smaller, independently-verified content/asset gaps found during the same review (a publishable skill catalog, a publishable body-part/anatomy catalog, a scene background/map field, and character/token portrait wiring) -- deserves its own formal slice, `SLICE-07`, following the exact `SLICE-05`/`SLICE-06` precedent (its own ID prefix, its own backlog document), with the check/contest system built first because it is, architecturally, the largest of the five: zero lines of production code exist for it today, unlike the other four blocks, each of which already has a partial, real foundation to extend (`ContentDefinitionType.Skill` already exists as a bare enum value; `AnatomyInitializationRules` already has a real, if hardcoded, body-part list; `SceneRecord`/`TokenRecord` already exist and only need one new field each).

This ADR exists to decide the check/contest architecture once, before any implementation task starts writing code -- the exact same one-ADR-then-backlog pattern `ADR-030`/`ODY-S06-101` already established and the product owner explicitly re-authorized for this task (section 0 of this task's own governing ТЗ).

---

# 3. Термины

## 3.1 Check

A single roll-plus-modifier resolution compared against a target difficulty (a "Difficulty Class" or equivalent target number), producing at minimum a pass/fail outcome. The subject of this ADR's own primary design (sections 5-10).

## 3.2 Contest

An opposed resolution: two characters' own checks compared against each other rather than against a static target number (e.g. a Stealth check opposed by a Perception check). Named in this ADR's own title because the underlying comparison machinery this ADR specifies (a resolved total compared against a threshold) is symmetric and already sufficient to express a contest as two checks compared against each other instead of one check compared against a fixed number -- but building a dedicated contest command is an explicit non-goal for this first version (section 8.3), not designed further here.

## 3.3 Difficulty Class (DC)

The caller-supplied target integer a check's own final total must meet or exceed to pass. Supplied by the command's own caller (a GM, or a rule the calling context already knows) -- this ADR does not introduce a published, catalog-authored "difficulty" content type; a DC is a plain number on the command request, the same way `AttackIntent` does not look up its own damage threshold from a catalog either.

## 3.4 Degrees of success

A richer outcome model than plain pass/fail (e.g. critical success, critical failure, or graduated success levels based on how far the total exceeds or falls short of the DC). Named here because it is an explicit non-goal for this ADR's own first version (section 8.2), not because this ADR designs it.

---

# 4. Существующая инфраструктура (verified, not assumed)

Direct code inspection for this task confirmed the following facts, each of which shapes a decision below:

- `DiceRollService.SubmitRoll` (`Packages/com.odyssey.application/Runtime/Dice/DiceRollService.cs:29-`) parses `SubmitRollRequest.Formula` through `DiceFormulaParser.TryParse` (line 42) -- the ORIGINAL `09_Dice_And_Game_Log` grammar (`ADR-009`), which has exactly two term kinds (`DiceGroup`, `Constant`) and no attribute/identifier-reference production at all (`ADR-030` §4/§6.1, re-confirmed here). `SubmitRoll` cannot accept `"1d20+Strength"` as one formula string.
- `SubmitRollRequest` (`DiceContracts.cs:274-`) already carries `IReadOnlyList<AutomaticModifierRequest>? automaticModifiers`, each a `(string sourceKind, string label, int value)` triple (`DiceContracts.cs:314-`) applied as a visible `ModifierEntry` with `Decision = Automatic` -- "system-determined modifiers applied at roll time without a separate GM decision... still visible... not a hidden adjustment" (line 310's own doc comment). This is an exact, already-built mechanism for supplying a resolved attribute/skill modifier alongside a pure-dice `SubmitRoll` call.
- `DiceRollService.SubmitRoll`'s own RNG-derivation purpose is hard-coded internally: `private static readonly RngPurpose RollPurpose = RngPurpose.Parse("dice.roll")` (`DiceRollService.cs:27`), used unconditionally inside `RandomDecisionContext.Create` (line 53-55) -- entirely independent of the caller-supplied, purely descriptive `SubmitRollRequest.Purpose` string field. A caller cannot make `SubmitRoll` derive under a different `RngPurpose` even if it wanted to.
- `DiceRoll.RollId` (`DiceContracts.cs:190`) is a plain, non-empty-validated `string` -- exactly the type `ICharacterRepository.RecordCriticalSuccessEvidence`'s own `sourceDiceRollId: string?` parameter (`CharacterRepositoryContracts.cs:210-218`) expects, confirmed by direct signature read.
- `DiceRoll.NaturalResults` carries `NaturalResult(int dieIndex, int groupIndex, int sides, int value)` entries (`DiceContracts.cs:14-28`) -- a single-die dice-group term's own natural maximum roll is directly detectable as `result.Value == result.Sides`, with no new Domain field required for critical-success detection.
- `RecordCriticalSuccessEvidence` (`CharacterRepositoryContracts.cs:210-218`) already exists, fully implemented, with "No permission gate -- recording an observed game fact is not a discretionary decision" (its own doc comment) -- it has simply never had a real caller. `ODY-S04-106`'s own task file states this explicitly: "`RecordCriticalSuccessEvidence`'s real trigger (a critical success during an actual skill-check dice roll) is not implemented -- this task provides only the durable recording primitive; a future dice-integration task would call it from the real game-mechanic trigger" (`docs/tasks/completed/ODY-S04-106_Skill_Purchases_Critical_Evidence_Recommendation.md`). `ODY-S04-114`'s own vertical-slice test calls it with a hand-authored literal, `sourceDiceRollId: "roll_vertical_slice_001"` (`DotNet/Tests/Odyssey.Tests.Persistence/Integration/CharacterVerticalSliceIntegrationTests.cs`) -- confirmed synthetic, not a real dice-pipeline identifier, exactly the gap this ADR's decision 8 closes.
- `AttackDamageFormulaParser` (`Packages/com.odyssey.rules/Runtime/Combat/AttackDamageFormulaParser.cs`, `ADR-030` §6.2) exposes `Parse(string)`/`TryParse(string?, out AttackDamageFormula, out AttackDamageFormulaParseError)`. Its `attributeReference` term (`letter, {letter | digit | "_"}`) is resolved purely by name -- `CoreAttackRulesEvaluator`'s own resolution code (`AttributeDefinitionId.TryParse(term.AttributeName, ...) -> actorAttributes.TryGetValue(...)`) is nothing more than a string-keyed dictionary lookup, with zero attribute-specific logic in the parser itself, confirmed by direct read for this ADR. `SkillDefinitionId`'s own validation pattern (`^[A-Za-z][A-Za-z0-9_]{0,63}$`) is identical in shape to `AttributeDefinitionId`'s own -- a skill name parses through the identical `attributeReference` grammar production without any change.
- `AttackParticipantState.AttributeValues` (`Packages/com.odyssey.domain/Runtime/Combat/AttackPipelineContracts.cs`, `ODY-S06-102`) is populated by `SqliteAttackStateReader.Read`'s own `State(CharacterRecord record)` helper via a plain `foreach` copy of `record.Attributes`' `EffectiveValue` into a `Dictionary<AttributeDefinitionId, long>` -- no second database read, since `CharacterRecord` already loads `Attributes` in the same `GetCharacter` call. `CharacterRecord.Skills` (`IReadOnlyList<CharacterSkill>`, `CharacterRepositoryContracts.cs`) is loaded by the identical `GetCharacter` call, alongside `Attributes`/`Abilities`/`Resources`/`Anatomy` -- confirmed by direct constructor-parameter read. `CharacterSkill.EffectiveLevel` (`Level + PermanentAdjustment`, `SkillEconomy.cs:53-79`) mirrors `AttributeValue.EffectiveValue`'s own computed-only shape exactly.
- `ContentDefinitionType.Skill` (`ContentCatalog.cs:81-`) exists only as a bare enum value (`= 11`) -- no `SkillDefinition` class and no `TypedDefinitionCodec.EncodeSkill`/`DecodeSkill` pair exists anywhere in this codebase, confirmed by a whole-repository search for this task. This ADR's own decision 4 (a check snapshot's skill dictionary) reads a Character's ALREADY-ACQUIRED `CharacterSkill` runtime state (`SkillDefinitionId`/`EffectiveLevel`), which requires no typed catalog `SkillDefinition` to exist at all -- the check architecture this ADR specifies has no dependency on the separate "publishable skill catalog" backlog block (`docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md` §8) this same review also found missing.
- `IAttackRulesEvaluator` (`AttackRulesContracts.cs`, `ADR-029`) has the two-method `Preview`/`Evaluate` shape `ADR-030` §4 already documented -- reused here only as a structural precedent for a new, separate `ICheckRulesEvaluator`, never extended or reused directly (a check has no `AttackIntent`/`AttackEvaluationSnapshot`/`AttackRandomSample`-shaped inputs; forcing it through the attack-shaped interface would require inventing meaningless placeholder values for every attack-specific field).
- `ADR-030` §9.3 ("Combat binding: explicitly decided, not left open") records that `ActivateAbility` is NOT bound to `CombatEncounter` state for the identical reason section 9 of this ADR restates for checks: "many abilities are plausibly non-combat... forcing combat-only activation would make the MVP scenario's own 'activate an ability' beat artificially depend on combat-encounter setup it does not otherwise need" (`ADR-030` §15.4). `ADR-030` §16's own "Открытые вопросы" section is exactly the single word "None" -- independently confirming, from the inside, that checks/contests are not addressed by that ADR at all.

---

# 5. Dice substrate: `DiceRollService.SubmitRoll`, not a direct `IAuthoritativeRandomStream` draw

## 5.1 The choice and its justification

A check's own die roll is submitted through `DiceRollService.SubmitRoll` (`ODY-S03-005`), never derived directly from `IAuthoritativeRandomStreamFactory` the way `CoreAttackRulesEvaluator`'s own `AttackRandomSample` is pre-derived by `AttackEvaluationService` before the Rules-layer evaluator ever runs. `DiceRollService` already provides, fully built and tested, exactly the machinery a check plausibly needs: a GM (or equivalently authorized actor) may propose/decide a situational modifier (`ProposeModifier`/`DecideModifier`, "GM... accepts, changes with reason, or rejects with reason"), apply a mandatory-reason override (`ApplyOverride`), or order a full reroll (`RequestFullReroll`, "the whole roll is redone -- never a partial/per-die reroll"). Every one of these already exists, is already tested, and today has -- confirmed by a whole-repository search for this task -- zero real production callers: `DiceRollService.SubmitRoll` is exercised only by its own test suite. Building a second, independent roll mechanism directly against `IAuthoritativeRandomStream` for checks would leave this entire, already-accepted feature permanently unused while duplicating a meaningful fraction of its own responsibility (deterministic RNG derivation, `NaturalResult` recording, `FinalTotal` computation) from scratch.

The alternative (direct `IAuthoritativeRandomStream`, `CoreAttackRulesEvaluator`'s own precedent) was seriously considered and is not without its own logic -- it is simpler, and it is the pattern every other Rules-Engine-adjacent mechanic in this codebase (`105`/`106`/`107`) already uses. It is rejected here specifically because none of those three mechanics had an existing, purpose-built, already-accepted adjudication feature sitting unused beside them; checks do. See section 16.1 for the full alternative-considered writeup.

## 5.2 The formula-split requirement

`DiceRollService.SubmitRoll` parses its own `formula` parameter through the ORIGINAL `DiceFormulaParser` (`ADR-009`), confirmed by direct code read (section 4) to have no attribute/skill-reference term at all -- `"1d20+Strength"` is not representable as one `SubmitRollRequest.Formula` string. The check command must therefore:

1. parse the full check formula (e.g. `"1d20+Strength"`, or `"1d20+Athletics"` for a skill check) through `AttackDamageFormulaParser` (`ADR-030` §6.2, section 6 below) -- the SAME shared parser every other Rules-Engine mechanic already uses, not a third grammar;
2. resolve every non-dice term (`attributeReference`) against the check snapshot's own attribute/skill dictionaries (section 7 below) into one concrete signed integer, exactly the same resolution logic `CoreAttackRulesEvaluator`'s own private formula evaluator already performs for attack damage, applied here to the non-dice portion only;
3. submit the formula's own dice-group sub-expression (e.g. `"1d20"`) as `SubmitRollRequest.Formula`, and the resolved attribute/skill total as one `AutomaticModifierRequest("AttributeModifier", <name>, <value>)` or `AutomaticModifierRequest("SkillModifier", <name>, <value>)` (the exact `sourceKind`/`label` string convention is `ODY-S07-102`+'s own contract decision, not fixed here) -- reusing `SubmitRollRequest.AutomaticModifiers`, a mechanism already designed for precisely "a modifier applied at roll time without a separate GM decision."

A check formula with more than one dice-group term, or with a dice-group term embedded in a more complex combination than "one dice group plus zero or more attribute/skill/constant terms," is out of scope for the first implementation task -- `AttackDamageFormulaParser`'s own grammar permits it structurally, but `SubmitRollRequest.Formula` accepts exactly one formula string handed to `DiceFormulaParser`, so a check formula with two independent dice groups (e.g. `"1d20+1d4+Strength"`) would need either two separate `SubmitRoll` calls or a `DiceFormulaParser`-compatible multi-group formula string (`DiceFormulaParser` itself already supports multiple dice groups in one string, per `ADR-030` §4's own confirmed grammar) -- this ADR does not resolve which; it is exactly the kind of formula-splitting edge case the first implementation task's own contract must decide and record, not silently assume away.

---

# 6. Formula grammar: `AttackDamageFormulaParser` reused verbatim

No new grammar, no new parser type, and no `CheckFormulaParser` is introduced. `AttackDamageFormulaParser` (`ADR-030` §6.2) already defines:

```
expression          = signedTerm, { ("+" | "-"), term } ;
signedTerm          = ["+" | "-"], term ;
term                = diceGroup | integer | attributeReference ;
diceGroup           = [positiveInteger], ("d" | "D"), positiveInteger ;
integer             = digit, { digit } ;
attributeReference  = letter, { letter | digit | "_" } ;
```

This is already sufficient for `"1d20+Strength"` (an attribute check) or `"1d20+Athletics"` (a skill check) with zero grammar changes -- `attributeReference` is a bare identifier production with no attribute-specific semantics baked into the parser itself (section 4). This ADR records explicitly, as a disclosed continuation of an already-accepted naming mismatch (not a new one): the term is named `attributeReference` and its resolved value type is `AttributeDefinitionId` inside `CoreAttackRulesEvaluator`'s own resolution code, but the PARSER ITSELF performs no attribute-specific validation -- resolution against a `SkillDefinitionId`-keyed dictionary instead (or in addition) requires no parser change, only a different (or combined) resolution dictionary at evaluation time. `ODY-S06-105`/`106`/`107` each already accepted a version of this same "the name is misleading, we reuse anyway, we do not fork" tradeoff for this exact parser; this ADR continues it rather than introducing a differently-misleading `CheckFormulaParser` that would still, underneath, be the identical grammar.

A check formula's own resolution therefore needs a caller-side decision this ADR fixes: if a formula's `attributeReference` term's name matches BOTH an attribute and a skill on the acting character (an unlikely but not impossible naming collision, since `AttributeDefinitionId`/`SkillDefinitionId` are separately-validated but textually overlapping identifier spaces), resolution must fail closed with a typed ambiguity error, never silently prefer one over the other -- the same fail-closed convention `ADR-030` §6.2 already established for an unresolved attribute reference.

---

# 7. Check snapshot: attribute AND skill resolution, by the `ODY-S06-102` precedent

A new snapshot type (e.g. `CheckParticipantState`, exact name/placement left to `ODY-S07-102`+'s own contract) carries:

- `AttributeValues: IReadOnlyDictionary<AttributeDefinitionId, long>` -- populated identically to `AttackParticipantState.AttributeValues` (`ODY-S06-102`): a plain `foreach` copy of the acting `CharacterRecord.Attributes`' own `EffectiveValue`, no second database read.
- `SkillValues: IReadOnlyDictionary<SkillDefinitionId, long>` -- the direct analog, newly required because no existing snapshot type carries it: a plain `foreach` copy of the acting `CharacterRecord.Skills`' own `EffectiveLevel`, populated in the SAME state-read call that already loads `CharacterRecord` in full (`GetCharacter` already returns `Skills` alongside `Attributes`, confirmed by direct constructor read, section 4) -- zero new database queries, exactly matching `102`'s own "copy already-loaded state, do not add a query" precedent.

This snapshot is deliberately NOT `AttackParticipantState` extended with a new field -- a check has no `LifecycleStatus`/`ApprovalState`-shaped combat-participant concept it needs from that specific type, and extending an attack-pipeline-named type to serve a structurally unrelated command would blur two independently-evolving contracts (the same reasoning `ADR-030` §7.1 already used to keep the mechanics-primitive payload decode separate from `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect`, rather than folding two independently-versioned concerns into one). `CheckParticipantState` is its own new type, in `Odyssey.Domain` (mirroring where `AttackParticipantState` itself lives), structurally similar but independent.

---

# 8. Result shape: a new, pass/fail Rules-layer evaluator

## 8.1 Shape

A new `ICheckRulesEvaluator`/`CoreCheckRulesEvaluator` pair (`Odyssey.Rules`, exact namespace/name left to `ODY-S07-102`+, structurally parallel to `IAttackRulesEvaluator`/`CoreAttackRulesEvaluator` but not derived from or coupled to it) performs, in the pure, no-I/O `Odyssey.Rules` layer:

1. resolve the check formula's own non-dice terms (section 5.2 step 2) against `CheckParticipantState`'s own attribute/skill dictionaries -- pure calculation, no RNG, no I/O, the exact class of work `ADR-001` §6.2 already reserves for `Odyssey.Rules`;
2. once the Application-layer orchestrator has called `DiceRollService.SubmitRoll` (section 5) and received a real `DiceRoll.FinalTotal`, compare that total against the caller-supplied difficulty class (section 3.3): `FinalTotal >= difficultyClass` is a pass, otherwise a fail -- the minimum outcome shape this ADR requires (Definition of Done, section 15);
3. detect a natural-maximum roll (section 4's own confirmed `NaturalResult.Value == NaturalResult.Sides` check) on the check's own dice-group term, surfacing it as a flag on the evaluator's own result type for the Application-layer orchestrator to act on (section 10).

The evaluator itself never calls `IAuthoritativeRandomStreamFactory`, `DiceRollService`, or any repository -- every input (the resolved modifier, the `DiceRoll`'s own already-computed `FinalTotal`/`NaturalResults`, the difficulty class) arrives as an explicit parameter, exactly as `ADR-001` §6.2 requires and exactly as `CoreAttackRulesEvaluator`'s own `Evaluate(intent, snapshot, randomSample)` already models for the attack pipeline (it receives an already-derived `AttackRandomSample`, never derives one itself).

## 8.2 Degrees of success: confirmed non-goal for this version

This ADR's own first version produces plain pass/fail plus the one natural-maximum-roll signal section 8.1/10 already requires for the `RecordCriticalSuccessEvidence` wiring -- it does NOT produce graduated degrees of success (e.g. "success by 5 or more is an exceptional success," or a distinct "critical failure" outcome for a natural minimum roll) beyond that one flag. This was recorded, when this ADR was first drafted, as a product decision made within this task's own engineering judgment and flagged for product-owner confirmation, not a silent omission -- **the product owner has since confirmed it (2026-09-22, доработка of this ADR after PR #165's own independent verification): degrees of success are out of scope for v1, plain pass/fail only.** Section 17 records this confirmed decision; it is not a permanent prohibition -- a later task remains free to add graduated outcomes on top of this same pass/fail comparison (section 8.1) without any redesign, exactly the same "disclosed non-goal, not an architectural block" shape `ADR-030` §12 already established for its own non-goals.

## 8.3 Advantage/disadvantage and contests: confirmed non-goal for this version

Rolling twice and taking the better or worse result (advantage/disadvantage), and opposed contests (section 3.2, one check compared against another instead of against a static DC), are both confirmed non-goals for the first implementation task. Advantage/disadvantage is structurally a `DiceRollService`-level concern (which roll of two "counts") this ADR does not design; a contest is architecturally a straightforward extension of section 8.1's own comparison (compare two resolved totals against each other instead of one total against a fixed number) but is not itself built here. Both were originally named in this ADR's own title and this section specifically so a future task would not have to independently rediscover that the underlying machinery (the shared formula grammar, the `DiceRollService` substrate, the pass/fail comparison shape) already supports extending toward either without redesign -- **the product owner has since confirmed (2026-09-22, доработка of this ADR after PR #165's own independent verification) that neither is in scope for v1.** Section 17 records this confirmed decision; as with section 8.2, this is a disclosed, revisitable non-goal for the first version, not an architectural prohibition on ever building either.

---

# 9. Combat binding: standalone, not tied to `CombatEncounter`

A check is a standalone `ADR-002` root command, never requiring an open `CombatEncounter`, a turn/round phase, or a combat-participant check -- restating `ADR-030` §9.3's own argument for `ActivateAbility` and extending it: checks are plausibly used OUTSIDE combat at least as often as abilities are, arguably more so (a locked door, a persuasion attempt, a climb, a search, a stealth approach are all check-shaped and none of them presuppose combat). Using a check as a turn-consuming combat action (e.g. "make an opportunity Perception check as a reaction") is an explicit, disclosed non-goal for a future task to decide, mirroring `ADR-030` §12's own identical disclosure for ability activation -- not an implicit consequence of this decision and not blocked by it either.

---

# 10. `RecordCriticalSuccessEvidence` wiring: closing the `ODY-S04-106`/`114` gap for real

The first implementation task that builds the check command MUST call `ICharacterRepository.RecordCriticalSuccessEvidence` when a real check's own dice-group term rolls its natural maximum (section 8.1 point 3), passing:

- the check's own `SkillDefinitionId` when the check's formula resolved a skill term (an attribute-only check, with no skill term at all, has no `SkillDefinitionId` to record against -- `RecordCriticalSuccessEvidence`'s own signature already accepts this as a required, non-nullable parameter, so an attribute-only check's own critical roll is out of this hook's own scope, not a gap this ADR needs to resolve differently);
- `sourceDiceRollId: DiceRoll.RollId` -- the REAL, `DiceRollService`-issued roll identifier, never a synthetic literal, closing the exact gap `ODY-S04-106`'s own task file named ("this task provides only the durable recording primitive; a future dice-integration task would call it from the real game-mechanic trigger") and `ODY-S04-114`'s own test necessarily worked around with a hand-authored string in the complete absence of a real caller;
- `sourceActionId`: the check command's own `CommandId` or an equivalent stable identifier, at the implementing task's own discretion -- this ADR does not fix which, since `RecordCriticalSuccessEvidence`'s own signature already accepts it as an optional, nullable `string?`.

This closes a real, multi-task-old, explicitly-disclosed gap -- not a new requirement this ADR invents. No change to `RecordCriticalSuccessEvidence`'s own signature, permission-free design, or persistence shape is needed or authorized by this ADR; it is called exactly as it already exists.

---

# 11. RNG purpose: no new value

`DiceRollService.SubmitRoll` already fixes its own RNG-derivation purpose internally as `RngPurpose.Parse("dice.roll")`, a `private static readonly` field never exposed to or overridable by any caller (section 4). Since decision 1 (section 5) routes every check roll through `SubmitRoll`, this existing purpose string is reused automatically -- no new `RngPurpose` value (e.g. a hypothetical `"check.roll"`) is registered or needed anywhere in this architecture. `RngPurpose`'s own dotted-lowercase, minimum-two-segment convention (`"combat.attack.roll"`, `"ability.activation.roll"`, `"item.usage.roll"`, `"dice.roll"`, section 4) is noted here only for completeness -- it does not apply to a new value this ADR does not introduce.

---

# 12. Module boundaries (`ADR-001` compliance)

`ICheckRulesEvaluator`/`CoreCheckRulesEvaluator` and `CheckParticipantState` live in `Odyssey.Rules`/`Odyssey.Domain` respectively (mirroring exactly where `IAttackRulesEvaluator`/`CoreAttackRulesEvaluator` and `AttackParticipantState` already live), performing only pure calculation with every external input (the resolved attribute/skill dictionaries, the already-computed `DiceRoll.FinalTotal`/`NaturalResults`, the difficulty class) arriving as an explicit parameter -- no clock, RNG, or database access inside either type, exactly as `ADR-001` §6.2 requires. The check command's own Application-layer orchestration (calling `DiceRollService.SubmitRoll`, then the Rules-layer evaluator, then conditionally `RecordCriticalSuccessEvidence`) is Application-layer code, mirroring `AttackApplyService`/`ActivateAbilityService`'s own established separation -- it calls into Rules and into `DiceRollService`, it does not live inside either.

---

# 13. Non-goals

This ADR does not:

- implement the check command, the evaluator, the snapshot, or the `RecordCriticalSuccessEvidence` wiring -- architecture only, exactly `ODY-S06-101`'s own precedent for `ADR-030` (implementation is `ODY-S07-102`+'s own job);
- modify `IAttackRulesEvaluator`, `CoreAttackRulesEvaluator`, or `AttackDamageFormulaParser`'s own content in any way -- all three are reused strictly by reference;
- modify `DiceRollService`'s own existing logic (`SubmitRoll`/`ProposeModifier`/`DecideModifier`/`ApplyOverride`/`RequestFullReroll`/`CancelRoll`) -- this ADR adds a new caller, not a new capability;
- modify `ActivateAbility`/`UseItem` (`ADR-030` §9/§10, `ODY-S06-106`/`107`) or any of their own territory;
- specify degrees of success beyond plain pass/fail plus one natural-maximum-roll signal (section 8.2) -- a confirmed non-goal for v1 (section 17);
- specify advantage/disadvantage or a dedicated opposed-contest command (section 8.3) -- confirmed non-goals for v1 (section 17);
- specify a published, catalog-authored "difficulty" content type -- a DC is a plain caller-supplied number (section 3.3);
- specify or implement the publishable skill catalog, the publishable body-part/anatomy catalog, the scene background/map field, or character/token portrait wiring -- each its own separate, named-not-scoped block in `docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md` §8, not designed by this ADR;
- revise `ADR-001`-`ADR-030` -- extends by reference only.

---

# 14. Rules for Codex

Future implementation tasks (`ODY-S07-102`+ and any later `SLICE-07` task touching checks) must:

1. implement the check command's own dice roll through `DiceRollService.SubmitRoll`, never a direct `IAuthoritativeRandomStream` draw (section 5) -- unless the product owner explicitly revises decision 1;
2. reuse `AttackDamageFormulaParser` exactly as it stands (section 6) -- never fork a `CheckFormulaParser`, never modify `AttackDamageFormulaParser.cs`/`DiceFormulaParser.cs` themselves;
3. resolve a check formula's own dice-group sub-expression separately from its attribute/skill terms (section 5.2), submitting only the dice portion to `SubmitRollRequest.Formula` and the resolved modifier via `AutomaticModifierRequest`;
4. build the check snapshot with both attribute AND skill dictionaries (section 7), copied from an already-loaded `CharacterRecord` with no new database read;
5. implement the pass/fail evaluator as a new, standalone `Odyssey.Rules` type (section 8.1), never as an extension of `IAttackRulesEvaluator`/`CoreAttackRulesEvaluator`;
6. treat degrees of success and advantage/disadvantage/contests as out of scope (section 8.2/8.3) unless the product owner explicitly revises those decisions before implementation starts;
7. implement the check command as standalone, never requiring `CombatEncounter` state (section 9), unless the product owner explicitly revises that decision;
8. call `RecordCriticalSuccessEvidence` with the REAL `DiceRoll.RollId` on a genuine natural-maximum roll for a skill check (section 10) -- never a synthetic identifier;
9. register no new `RngPurpose` value (section 11); and
10. never re-implement formula parsing, dice rolling, or GM-adjudication machinery independently -- always call into the already-accepted `AttackDamageFormulaParser`/`DiceRollService`.

---

# 15. Definition of Done for future implementation tasks

Implementation decomposition and tests must prove at minimum:

1. a check command resolves a formula such as `"1d20+Strength"` or `"1d20+Athletics"` by splitting it into a dice-only `SubmitRollRequest.Formula` and a resolved `AutomaticModifierRequest`, through the real, unmodified `DiceRollService.SubmitRoll`;
2. the resulting `DiceRoll.FinalTotal` is compared against a caller-supplied difficulty class by a new, standalone `Odyssey.Rules`-layer evaluator, producing a real pass/fail outcome;
3. an unresolved attribute/skill reference in a check formula fails closed (a typed error), never a silent zero, mirroring `ADR-030` §6.2's own established convention;
4. a genuine natural-maximum roll on a skill check's own dice-group term calls `RecordCriticalSuccessEvidence` with the real `DiceRoll.RollId`, verified by a real integration test reading the resulting `CriticalSuccessEvidenceRecord` back, not merely asserting the call happened;
5. a check command succeeds identically whether or not a `CombatEncounter` is open, with at least one test proving the no-encounter-open case explicitly; and
6. no new Rules Engine type reads the clock/RNG/database/session state directly -- verified by the same architecture-guard convention this codebase already established for every prior module-boundary guarantee.

---

# 16. Рассмотренные альтернативы

## 16.1 Direct `IAuthoritativeRandomStream` draw, mirroring `CoreAttackRulesEvaluator` exactly

**Rejected:** simpler and consistent with `105`/`106`/`107`'s own precedent, but it would leave `DiceRollService`'s own already-built, already-tested GM-adjudication machinery (modifier proposal/decision, override, full reroll) permanently unused while building a materially overlapping second mechanism from scratch specifically for checks -- the one class of roll (attribute/skill checks) where GM adjudication of the result is plausibly MOST wanted, not least.

**Accepted:** route through `DiceRollService.SubmitRoll` (section 5), accepting the one real cost this choice has -- the formula-split requirement (section 5.2), since `SubmitRoll`'s own formula parser predates and does not share `AttackDamageFormulaParser`'s attribute-reference term.

## 16.2 A new `CheckFormulaParser`, purpose-built for checks

**Rejected:** would be a third grammar (after `DiceFormulaParser` and `AttackDamageFormulaParser`) for a mechanic that needs nothing `AttackDamageFormulaParser` does not already provide -- `"1d20+Strength"`/`"1d20+Athletics"` parse under the existing grammar without any change, confirmed by direct grammar read (section 4/6).

**Accepted:** reuse `AttackDamageFormulaParser` verbatim, continuing the same reuse-despite-a-misleading-name tradeoff `105`/`106`/`107` already established for this parser (section 6).

## 16.3 Extend `AttackParticipantState` with a `SkillValues` field instead of a new `CheckParticipantState` type

**Rejected:** would couple an attack-pipeline-specific type (which also carries `LifecycleStatus`/`ApprovalState`, meaningful only in combat-adjacent contexts) to a structurally unrelated command, and would force every future attack-pipeline change to consider whether it also affects checks and vice versa -- the same "two independently-versioned concerns should not share one artifact" reasoning `ADR-030` §7.1 already used for the mechanics-primitive payload decode.

**Accepted:** a new, independent `CheckParticipantState` type (section 7), structurally similar to `AttackParticipantState` but not derived from or coupled to it.

## 16.4 Build degrees of success and advantage/disadvantage now, since the ADR's own title names "contest"

**Rejected:** no product-owner guidance exists today for what a degree-of-success margin should mean mechanically, whether critical failure exists, or how advantage/disadvantage should interact with `DiceRollService`'s own GM-adjudication flow -- building any of this speculatively risks a wrong guess the product owner then has to unwind, and none of it is required to prove the MVP-sufficient "roll, add a modifier, compare to a target number, pass or fail" mechanic this task's own governing ТЗ asked to unblock first.

**Accepted:** plain pass/fail plus the one natural-maximum-roll signal `RecordCriticalSuccessEvidence`'s own existing hook needs (section 8.2/8.3), with both richer mechanics named and disclosed rather than either silently built or silently dropped. The product owner has since confirmed (section 17) that this first version stays plain pass/fail -- the decision this alternative-analysis originally flagged for confirmation is now settled, not merely proposed.

---

# 17. Открытые вопросы

None. This ADR originally flagged two decisions here as product decisions made within this task's own engineering judgment, pending product-owner confirmation, rather than silently deferred. Both are now confirmed (**2026-09-22, доработка of this ADR following PR #165's own independent verification**):

1. **Degrees of success (section 8.2): CONFIRMED out of scope for v1.** The product owner confirmed that the first version of the check/contest system produces plain pass/fail only, plus the one natural-maximum-roll signal `RecordCriticalSuccessEvidence`'s own hook needs (section 8.1 point 3/section 10) -- no graduated success margins, no distinct critical-failure outcome. This is a disclosed non-goal for v1, not a permanent architectural prohibition: nothing in sections 5-11 blocks a later task from adding degrees of success on top of the same pass/fail comparison, the same "disclosed, revisitable non-goal" shape `ADR-030` §12 already established for its own non-goals.
2. **Advantage/disadvantage and dedicated contest support (section 8.3): CONFIRMED out of scope for v1.** The product owner confirmed that the first version builds neither rolling-twice-take-better/worse nor a dedicated opposed-check command. This is likewise a disclosed non-goal for v1, not a prohibition -- section 8.3 already records that both are architecturally straightforward extensions of this ADR's own machinery (the shared formula grammar, the `DiceRollService` substrate, the pass/fail comparison shape) whenever a future task takes them up.

Everything this ADR's own sections 5-11 decide -- including these two, now confirmed -- is settled: engineering decisions justified by direct precedent (`ADR-030`, `ODY-S06-102`/`105`/`106`/`107`) or direct code inspection, and these two product-scope decisions now settled by explicit product-owner confirmation. Nothing in this ADR remains open.

---

# 18. Трассировка

This ADR extends, without reopening:

- `ADR-001` §6.2 (`Odyssey.Rules` module boundary, applied directly to `ICheckRulesEvaluator`/`CoreCheckRulesEvaluator`/`CheckParticipantState`, section 12);
- `ADR-002` (commands, idempotency, reused unmodified by the new check root command);
- `ADR-008` (deterministic host RNG, reused indirectly through `DiceRollService.SubmitRoll`'s own already-accepted use of it, section 5);
- `ADR-009`/`09_Dice_And_Game_Log` §7 (`DiceFormulaParser`/`DiceRollService`, reused as the dice substrate, section 5, explicitly not modified);
- `ADR-019` (permissions/audience baseline, reused unmodified by `DiceRollService`'s own existing authorization checks and by the check command's own authorization step);
- `ADR-030` §6 (`AttackDamageFormulaParser`, reused verbatim, section 6); §7 (the mechanics-primitive schema's own `schemaVersion`-envelope/separate-decode-method convention, referenced as precedent though not directly reused, since a check has no comparable payload); §9.3 (`ActivateAbility`'s own `CombatEncounter`-unbinding argument, extended to checks, section 9); §16 (its own "None" open-questions confirmation, cited as independent evidence this gap was never addressed there).

Existing ADRs reused without redefinition: `ADR-004` (typed failures); `ODY-S04-106`/`114` (the `RecordCriticalSuccessEvidence` primitive itself, closed for real by section 10, not redesigned).

---

# 19. Нормативное действие

**This ADR is Accepted.** It is binding for `ODY-S07-102` and any later `SLICE-07` task implementing the check/contest command, evaluator, snapshot, or `RecordCriticalSuccessEvidence` wiring. This ADR authorizes creation of `docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md` and the corresponding `ODY-S07-101` backlog status update.
