# ADR-030 - Rules Engine / Mechanics Execution Architecture

**Документ:** `docs/adr/ADR-030_Rules_Engine_Mechanics_Execution_Architecture_v1.0.md`
**ADR:** ADR-030
**Версия:** 1.0
**Дата:** 2026-09-17
**Статус:** Accepted
**Область:** the architecture that turns declarative content (`WeaponDefinition.DamageExpression`, `AbilityDefinition.MechanicsPayloadRef`, `EffectDefinition.MechanicsPayloadRef`) into real mutating outcomes -- the real `IAttackRulesEvaluator` implementation, a shared expression-formula grammar, a minimal mechanics-primitive interpreter, a `RulesetId@RulesetVersion` rules registry, and the `ActivateAbility` root-command architecture. This ADR does not amend `ADR-001`-`ADR-029`; it fills the gap those ADRs each deliberately left open for exactly this purpose.
**Связанные этапы:** `SLICE-06`, task `ODY-S06-101`
**Базовые документы:** `ADR-001` §6.2 (`Odyssey.Rules` module boundary); `ADR-002` (commands, idempotency); `ADR-008` (deterministic host RNG); `ADR-009`/`09_Dice_And_Game_Log` §7 (`DiceFormulaParser`); `ADR-012` §5 (single-transaction commit pipeline); `ADR-025` §7.2 (`RulesetDefinitionCatalog`, ruleset-migration diff data -- distinguished, not reused, by this ADR); `ADR-027` §6/§8.1/§8.2 (item-triggered `ActiveEffect` creation, `DefinitionMechanicsSnapshot`); `ADR-028` (`ActiveEffect` aggregate, `EffectMechanicsSnapshot`, `EffectStackPolicy`, `EffectDurationType`); `ADR-029` (`IAttackRulesEvaluator`, `AttackEvaluationSnapshot`, `AttackDelta`, the full attack pipeline this ADR's own real evaluator plugs into); `11_Content_Block_System` §7/§8/§14/§21/§22 (the full `ContentBlockGraph` this ADR deliberately narrows away from).

---

# 1. Решение

Odyssey VTT interprets all declarative Ruleset content -- attack resolution, ability activation, and effect application -- through one shared, deterministic, side-effect-free **Rules Engine** living in `Odyssey.Rules`, never through three independent, incompatible mechanisms. The following decisions are mandatory:

1. **One execution model, three call sites.** A single family of pure, `Odyssey.Rules`-layer types -- a shared expression-formula grammar (section 6) and a shared mechanics-primitive interpreter (section 7) -- is the one and only way declarative content becomes a concrete numeric/effect outcome. The real `IAttackRulesEvaluator` implementation (section 5, next task `ODY-S06-102`), `ActivateAbility`'s own interpretation step (section 9, task `ODY-S06-103`), and item-use `Instant`-effect application (section 10, roadmap block D, a later task) each call into this same shared engine. No call site re-implements formula parsing or primitive interpretation on its own.
2. **`IAttackRulesEvaluator`'s contract is not touched.** `Preview(AttackIntent, AttackEvaluationSnapshot) -> ProposedAttackResolution` and `Evaluate(AttackIntent, AttackEvaluationSnapshot, AttackRandomSample) -> ProposedAttackResolution` (`Packages/com.odyssey.rules/Runtime/Combat/AttackRulesContracts.cs`) are reused verbatim. This ADR designs what implements that interface, never a change to the interface itself.
3. **Formula grammar: a documented superset of the existing `DiceFormulaParser`, not a second competing grammar and not a silent reuse.** Section 6 records the concrete investigation of `09_Dice_And_Game_Log`'s own `DiceFormulaParser` (`Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs`) and the specific, code-verified reason it cannot be reused unmodified for `WeaponDefinition.DamageExpression`/mechanics-primitive magnitudes (no attribute-reference term exists in its grammar, and extending it would reopen `ADR-009`'s own already-accepted MVP dice-roll grammar, out of this ADR's scope). A new, `Odyssey.Rules`-layer parser is introduced that recognizes the identical dice-group/integer terms plus one new attribute-reference term, reusing `DiceFormulaParser`'s own published limits/constants rather than duplicating them.
4. **`AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef`: a minimal, closed, typed mechanics-primitive list -- not `11_Content_Block_System`'s future `ContentBlockGraph`.** Section 7 fixes a small, versioned JSON envelope naming exactly two primitives sufficient for the `SLICE-06` MVP scenario (adjust a Character resource; apply a published `ActiveEffect`), each primitive's numeric magnitude expressed in the same formula grammar section 6 defines. The list is explicitly extensible by adding new primitive kinds later; it is explicitly not an executable program, a scripting language, or a graph of composable blocks.
5. **Rules resolution is keyed by `RulesetId@RulesetVersion`, reusing the existing cache-key convention verbatim.** Section 8 defines a small, pure, in-process registry (no database table, no I/O) that resolves a campaign's pinned `campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion` key (the exact string shape `CatalogValidationContracts.cs`'s own `IsCompatibleWithActiveRuleset` already produces) to the concrete `IAttackRulesEvaluator`/mechanics-interpreter instance for that ruleset. Exactly one ruleset is registered for `SLICE-06`'s own MVP; the registry's own shape does not hard-code that assumption.
6. **`RulesetDefinitionCatalog` (`ADR-025` §7.2) is a different thing and is not reused, extended, or renamed.** It is a per-Character ruleset-migration diff fixture (which definition IDs a target ruleset version recognizes, by category) with no formula, primitive, or rules-execution content at all. The new rules registry (decision 5) is introduced as its own, separate type family; this ADR does not touch `RulesetMigrationRules.cs`/`RulesetDefinitionCatalog`.
7. **`ActivateAbility` is a new, standalone root command, not bound to `CombatEncounter`.** Section 9 specifies it as a new `ADR-002` root command that resolves `CharacterAbilityId -> AbilityDefinitionId -> AbilityDefinition` through the existing content catalog, interprets `MechanicsPayloadRef` through the shared engine, and applies the result atomically -- for `SLICE-06`'s own MVP scope, always outside `CombatEncounter` state (no turn/round/current-participant check). Using an ability as a combat action is an explicit, disclosed non-goal (section 12), not an oversight.
8. **Formula evaluation that needs RNG reuses `ADR-008`'s existing authoritative RNG contracts verbatim.** Neither the formula grammar nor the mechanics-primitive interpreter introduce a second randomness mechanism; a dice-group term in any formula, wherever it is evaluated (attack damage, ability magnitude), consumes the same `IAuthoritativeRandomStreamFactory`/`AttackRandomSample`-shaped derivation already established, with the same no-reroll-on-retry guarantee.
9. **Item-use (`Instant` effect via an active item, roadmap block `D1`) is named as a future consumer of this same engine, not designed here.** Section 10 records this explicitly so a future task does not have to independently rediscover that it should reuse, not reinvent, this ADR's own mechanics interpreter.

---

# 2. Контекст и проблема

`SLICE-05` closed the entire full attack pipeline (`ADR-029`, tasks `ODY-S05-601`-`611`): a combat encounter timeline, preview/evaluate/apply stages, intervention, atomic commit, compensation, audience-filtered Game Log projections, aggregate delta commit, and combat stacking-conflict resolution are all real, tested, merged production code. Independent verification for this task confirmed directly against the tracked repository that every one of those mechanisms is genuinely wired end-to-end -- except the one piece `ADR-029` itself never specified: `IAttackRulesEvaluator` (`Packages/com.odyssey.rules/Runtime/Combat/AttackRulesContracts.cs`) has no production implementation anywhere in this codebase. Every task in the full attack pipeline block, including its own integration fixtures (`ODY-S05-608`), supplied its own hand-written test-only `Rules` class implementing this interface -- confirmed, not assumed, by direct inspection of every test file in that block. `ADR-029` §10 explicitly named this as a non-goal ("this ADR supplies those boundaries without selecting a game system's hit formula, armor model, initiative formula, balance values, or content payload schema"), deferring it to exactly the future task this ADR now specifies the architecture for.

The same underlying gap recurs in two other places `SLICE-05` deliberately, honestly left open:

- `WeaponDefinition.DamageExpression` (`ODY-S05-105`, `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`) is an opaque, non-empty-validated `string` -- its own doc comment explicitly defers to "`09_Dice_And_Game_Log`'s own `DiceFormulaParser` grammar being a separate, already-implemented concern this task does not reuse or duplicate," naming the exact investigation this ADR now performs (section 6).
- `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef` (`ODY-S05-105`) are nullable placeholder strings, each doc comment explicitly deferring to a not-yet-built mechanism (`11_Content_Block_System` §8's future `ContentBlockGraph`, and `ADR-027` §6's `DefinitionMechanicsSnapshot`/`ActiveEffect.EffectMechanicsSnapshot` respectively).

A Character can already acquire an ability (`SqliteCharacterRepository.AcquireAbility`, `ODY-S04-108`) -- `CharacterAbility` durably stores its `AbilityDefinitionId`, `SourceKind`, rank, and enablement -- but no service anywhere activates one. `SLICE-06`'s own product-owner-approved MVP scenario ("two characters, gear, abilities, combat, movement, a second combat") cannot be built until something decides what "attack" and "activate this ability" actually compute. Three independent tasks each re-deciding this in isolation (the attack evaluator, ability activation, and effect interpretation) would produce three incompatible, duplicated mechanisms solving the identical underlying problem -- "how does declarative Ruleset content become a concrete mutating outcome." This ADR exists to decide that once, as one architecture, before any of `ODY-S06-102`/`103`/the roadmap's own block D task starts writing code.

---

# 3. Термины

## 3.1 Rules Engine

The collective name for the shared, deterministic, side-effect-free `Odyssey.Rules`-layer machinery this ADR specifies: the formula grammar (section 6), the mechanics-primitive interpreter (section 7), and the ruleset registry (section 8). Not a new assembly, not a new package -- it lives inside the already-existing `Odyssey.Rules` module (`ADR-001` §6.2), organized by the same domain-folder convention (`Combat/`, `Character/`, `Effects/`, `Versions/`) that module already uses.

## 3.2 Mechanics expression

A formula string (`WeaponDefinition.DamageExpression`, or a magnitude field inside a mechanics primitive, section 7) written in the grammar section 6 defines: dice groups, integer constants, and named attribute/resource references, combined with `+`/`-`.

## 3.3 Mechanics primitive

One typed, closed-vocabulary instruction inside an `AbilityDefinition`/`EffectDefinition`'s own `MechanicsPayloadRef` JSON envelope (section 7) -- e.g. "adjust this resource by this expression" or "apply this published effect." Not an executable program; a short, explicit, versioned list.

## 3.4 Ruleset key

The string `RulesetId + "@" + RulesetVersion`, reusing `CatalogValidationContracts.cs`'s own `IsCompatibleWithActiveRuleset` convention verbatim (`campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion`) as the lookup key into the rules registry (section 8). Not to be confused with `RulesetDefinitionCatalog` (`ADR-025` §7.2), a ruleset-migration diff fixture with no formula/primitive content.

---

# 4. Существующая инфраструктура (verified, not assumed)

Direct code inspection for this task confirmed the following facts, each of which shapes a decision below:

- `IAttackRulesEvaluator` (`AttackRulesContracts.cs:1-13`) has exactly the two-method shape quoted in decision 2. `AttackEvaluationService.PreviewAttack`/`EvaluateAttack` (`ODY-S05-603`) already call it exactly this way; no consumer needs the signature to change.
- `DiceFormulaParser`/`DiceFormula`/`DiceTerm` (`Packages/com.odyssey.domain/Runtime/Dice/DiceFormula.cs`) implement `09_Dice_And_Game_Log` §7.1's own accepted MVP grammar verbatim: `expression = signedTerm, {("+"|"-"), term}; term = diceGroup | integer; diceGroup = [positiveInteger], ("d"|"D"), positiveInteger; integer = digit, {digit}`. There is no third term production for a named identifier/attribute reference anywhere in this grammar or its `DiceTermKind` enum (`DiceGroup = 1, Constant = 2`, exhaustive). `DiceFormula.Terms` is `IReadOnlyList<DiceTerm>`, and `DiceTerm`'s own constructor is `internal` -- only `DiceFormulaParser` itself can construct one, so no external caller can smuggle a third term kind in without editing this file directly. `DiceFormulaParser.MaxDiceCount`/`MaxDiceGroups`/`MinSides`/`MaxSides`/`MaxFormulaLength`/`ParserVersion` are all `public const`/readonly, safely referenceable by another module without copying their values.
- `WeaponDefinition.DamageExpression` (`TypedDefinitions.cs:130-166`) is validated only for non-empty; no grammar is enforced at the Domain layer today.
- `AbilityDefinition.MechanicsPayloadRef` (`TypedDefinitions.cs:318-348`) and `EffectDefinition.MechanicsPayloadRef` (`TypedDefinitions.cs:390-419`) are both nullable `string?`, unvalidated beyond their containing type's own constructor (which does not inspect their content at all).
- `AttackEvaluationSnapshot` (`Packages/com.odyssey.domain/Runtime/Combat/AttackPipelineContracts.cs`) already carries both `RulesetId`/`RulesetVersion` as plain strings, alongside `ActionMechanics` (the weapon/item's own already-pinned `ItemMechanicsSnapshot`), `Actor`/`Targets` (`AttackParticipantState`), and range/armor/effect input flags. A real `IAttackRulesEvaluator` has everything it needs from this one parameter to resolve which ruleset's rules apply and which weapon it is resolving -- no new snapshot field is required by this ADR.
- `CatalogValidationContracts.cs`'s own `IsCompatibleWithActiveRuleset` (private helper) already builds `campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion` as a plain string comparison key for content-ruleset-compatibility checks -- the exact, already-accepted convention section 8 reuses.
- `RulesetDefinitionCatalog` (`Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs:23-41`) is, per its own doc comment, "a caller-supplied fixture naming which definition IDs the TARGET Ruleset version recognizes, per category ... TEST FIXTURE SHAPE ONLY." It carries four `IReadOnlyCollection<string>` id lists (attributes/skills/abilities/resources) and nothing else -- no formula, no primitive, no rule. It exists to support `ODY-S04-113`'s character-ruleset-migration diff, a wholly different problem from resolving execution rules for a live campaign.
- `CharacterAbility` (`Packages/com.odyssey.domain/Runtime/Character/Ability.cs:87-163`) stores only `AbilityDefinitionId` (a catalog key) plus `SourceKind`/rank/enablement/configuration/uses-state -- never the `AbilityDefinition` itself. Any activation must resolve the id through the content catalog at activation time, exactly the same pattern `606`'s own `ItemEffectLifecycleService` already established for `EffectDefinitionRef`.
- `SourceKind` (`Ability.cs:58-66`) already reserves `Item`/`ActiveEffect`/`RulesetAdvancement`/`CharacterTemplate` values, its own doc comment noting they are "structurally accepted ... but this task implements no automatic acquisition through them" -- `ActivateAbility` (section 9) activates an ability regardless of which `SourceKind` acquired it; it does not add or change any `SourceKind` value.
- `AttributeValue.EffectiveValue`'s own doc comment (`Packages/com.odyssey.domain/Runtime/Character/DevelopmentEconomy.cs:103-112`) already names "ADR-001's Rules Engine boundary" in describing why effective values must be computed, never hand-edited -- confirming this ADR's own naming and boundary are a direct continuation of an already-established principle, not a new one.
- `ADR-001` §6.2 already assigns `Odyssey.Rules` ownership of "формулы; expression evaluation; roll/calculation trace; ruleset execution contracts; targeting и outcome calculations; deterministic validation; pure calculators" and explicitly forbids it from reading the clock/RNG/database/session state itself ("Rules получает время, RNG outcomes и внешнее состояние через явные входные параметры"). Every type this ADR introduces is designed to satisfy that boundary directly.
- `TypedDefinitionCodec` (`Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs`) already establishes the JSON envelope convention this ADR's own mechanics-primitive payload reuses: a top-level `schemaVersion` field, checked on every decode path since `ODY-S05-105`'s own amendment (`RequireSupportedSchemaVersion`), rejecting a missing/unsupported version as a malformed payload rather than silently accepting it.

---

# 5. The real `IAttackRulesEvaluator` implementation (architecture only; `ODY-S06-102`'s own job to build)

A concrete, `Odyssey.Rules.Combat`-namespaced class (name left to `ODY-S06-102`'s own contract, e.g. `CoreAttackRulesEvaluator`) implements `IAttackRulesEvaluator` exactly as declared (decision 2). Its `Preview`/`Evaluate` methods:

1. read `AttackEvaluationSnapshot.ActionMechanics` (the weapon/item's own already-pinned `ItemMechanicsSnapshot`) and decode it as a `WeaponDefinition` (reusing `TypedDefinitionCodec`'s own existing decode path, unmodified);
2. evaluate `WeaponDefinition.DamageExpression` through the shared formula grammar (section 6), resolving any attribute-reference term against `AttackEvaluationSnapshot.Actor`'s own already-loaded attribute state (no new database read -- the snapshot already carries what `AttackEvaluationService`'s own caller loaded);
3. produce `AttackDelta` entries (`ODY-S05-609`'s own already-accepted `TargetRef` convention, `character:{characterId}:{resourceKind}`, reused verbatim -- this ADR does not revise it) for the computed damage/cost, and `AttackEffectCandidate` entries (`ODY-S05-603`/`606`'s own already-accepted shape, reused verbatim) for any effect the weapon/ability names, each candidate's own `EffectApplicationDecision` computed by a small, explicit trigger-condition check (e.g. "on hit" -- already representable by `AttackHitResult`, no new Domain vocabulary needed for the MVP scenario);
4. never itself calls `IAuthoritativeRandomStreamFactory` -- `Evaluate`'s own `AttackRandomSample` parameter is the ONLY randomness input, exactly as `IAttackRulesEvaluator`'s own existing contract already requires, and it is the sole source for any dice-group term the formula grammar evaluates during this call.

This section deliberately does not fix armor/body-part/range resolution logic (roadmap blocks B/C, explicit non-goal, section 12) -- the MVP evaluator's own first version may treat a hit as unconditional and armor as absent, since the encounter/attack pipeline itself already tolerates that (`AttackArmorProposal`/`AttackBodyPartProposal` already default-constructible with placeholder values in every existing test fixture). `ODY-S06-102`'s own task contract fixes the exact MVP-scope simplifications; this ADR only fixes that the formula/primitive/registry machinery it uses is the one this document specifies.

---

# 6. Formula grammar for `WeaponDefinition.DamageExpression` and mechanics-primitive magnitudes

## 6.1 Why `DiceFormulaParser` is not reused unmodified

Section 4's own verified facts are conclusive: `DiceFormulaParser`'s grammar has exactly two term kinds, `DiceGroup` and `Constant` -- no identifier/attribute-reference production exists, `DiceTermKind` is an exhaustive two-value enum, and `DiceTerm`'s own constructor is `internal` (only `DiceFormulaParser` itself can construct one). A weapon-damage-style expression such as `1d6+STR` (a dice group plus a named attribute modifier) cannot be represented by this grammar at all -- not "parses incorrectly," but structurally inexpressible. Extending `DiceTermKind`/`DiceTerm`/`DiceFormulaParser` to add a third term kind would mean revising `09_Dice_And_Game_Log` §7.1's own already-accepted MVP grammar and its own `ParserVersion = 1` contract (used by Game Log dice-roll persistence, `ODY-S03-005`, unrelated to weapon damage) -- reopening an accepted ADR-009 decision this ADR is not authorized to revise, and risking a real behavior change for the unrelated dice-roll-command feature that already ships on that exact grammar.

## 6.2 The new grammar

A new, `Odyssey.Rules`-layer parser (e.g. `MechanicsExpressionParser`, exact name left to `ODY-S06-102`'s own contract) defines its own grammar as a strict token-level superset of `DiceFormulaParser`'s own:

```
expression      = signedTerm, { ("+" | "-"), term } ;
signedTerm      = ["+" | "-"], term ;
term            = diceGroup | integer | attributeReference ;
diceGroup       = [positiveInteger], ("d" | "D"), positiveInteger ;
integer         = digit, { digit } ;
attributeReference = letter, { letter | digit | "_" } ;
```

The `diceGroup`/`integer` productions, and their own numeric limits, are IDENTICAL to `DiceFormulaParser`'s own -- this new parser references `DiceFormulaParser.MaxDiceCount`/`MaxDiceGroups`/`MinSides`/`MaxSides` directly (a `Packages/com.odyssey.rules` -> `Packages/com.odyssey.domain` reference, already permitted by `ADR-001`'s own dependency matrix) rather than duplicating those constants, so a future change to the dice-roll limits cannot silently diverge from the weapon-damage limits without an explicit second edit. The new `attributeReference` term is resolved, at evaluation time (never at parse time -- parsing is pure syntax, resolution needs loaded character state), against a small resolution context the caller supplies: for the attack evaluator (section 5), the acting/target `AttackParticipantState`'s own already-loaded attribute snapshot; for the mechanics-primitive interpreter (section 7), the activating/target character's own already-loaded attribute snapshot. An attribute reference that does not resolve (unknown identifier, character has no such attribute) is a typed evaluation failure, never a silent zero -- fail-closed, matching this codebase's own established convention (e.g. `CombatEffectExpiryService`'s own fail-closed expiry rule, `ODY-S05-605`).

This new parser is a genuinely new implementation (it must tokenize a single string containing both dice-group and identifier terms, which `DiceFormulaParser` itself cannot do without modification) -- but it is not a competing, independently-designed grammar: it is a documented, deliberate superset, and its own dice/integer sub-grammar is pinned to the existing accepted one by direct reference to its published constants, not reimplemented from scratch.

## 6.3 Scope

This grammar is used for `WeaponDefinition.DamageExpression` (section 5) and for the numeric-magnitude field of an `AdjustResource` mechanics primitive (section 7.2). It is not used for, and does not replace, `09_Dice_And_Game_Log`'s own player-facing dice-roll command grammar (`DiceFormulaParser` itself, unmodified, continues to serve that unrelated feature).

---

# 7. Mechanics-primitive schema for `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef`

## 7.1 Envelope

`MechanicsPayloadRef`'s own string content (when non-null) is a JSON object following the same `schemaVersion`-gated envelope convention `TypedDefinitionCodec` already established:

```json
{
  "schemaVersion": 1,
  "primitives": [
    { "kind": "AdjustResource", "resourceKind": "health", "amountFormula": "-2d6" },
    { "kind": "ApplyEffect", "effectDefinitionRef": "cdef_0123456789abcdef0123456789abcdef/1" }
  ]
}
```

A missing, null, non-integer, or unsupported `schemaVersion` is a malformed payload, rejected exactly as `TypedDefinitionCodec`'s own existing decode paths already reject one (`ODY-S05-105`'s own amendment, reused as the pattern, not the code). This payload is decoded by a new, separate decode method (e.g. `MechanicsPayloadCodec.DecodePrimitives`, exact placement left to `ODY-S06-102`/`103`'s own contracts) -- deliberately NOT folded into `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect` themselves, since those methods decode the CATALOG SHAPE (already accepted by `ODY-S05-105`/`ADR-027`), while this payload is MECHANICS EXECUTION content this ADR alone introduces; conflating the two would make a catalog-shape change and a mechanics-primitive-schema change the same versioned artifact when they are not.

## 7.2 The two primitives (MVP-sufficient, not final)

- **`AdjustResource`** -- `resourceKind` (a `ResourceDefinitionId`-shaped string, `ODY-S04-109`, reused verbatim) and `amountFormula` (section 6's grammar, evaluated with sign preserved -- a negative result damages the resource, a positive result restores it, mirroring `ODY-S05-609`'s own already-accepted `AttackDelta.Value` signed-delta convention exactly, not a new opposite-pair "Damage"/"Restore" primitive design).
- **`ApplyEffect`** -- `effectDefinitionRef` (an exact-version `ContentDefinitionRef`, `ODY-S05-101`'s own canonical reference shape, reused verbatim) naming an already-Published `EffectDefinition`. Applying it creates a real `ActiveEffect` row via the existing, unmodified `IActiveEffectRepository.CreateActiveEffect` (`ODY-S05-502`) -- this primitive names WHICH effect to apply; it does not re-specify duration/stacking, both of which the referenced `EffectDefinition` itself already carries.

Both primitives' own numeric/reference fields reuse types this codebase already accepted (`ResourceDefinitionId`, `ContentDefinitionRef`) -- no new Domain identity type is introduced by this ADR.

## 7.3 Explicit boundary against `11_Content_Block_System`

This schema is NOT `11_Content_Block_System` §8's future `ContentBlockGraph`: it has no conditionals, no loops, no composable block graph, no `SelectTargetsBlock`/`FilterConditions`/`RangeRule`/`VisibilityRule` execution, and no way to reference another primitive's own result. It is a short, flat, ordered list of independently-applied instructions -- deliberately narrow, chosen only because `SLICE-06`'s own MVP scenario ("deal damage, or heal, or apply one status effect") needs nothing more. A future task that needs conditionals, composition, or a richer targeting model must design that as its own, separate extension of `11_Content_Block_System` itself; this ADR does not attempt to anticipate that design and does not reserve syntax for it beyond the `"kind"` discriminator already being open to new values.

---

# 8. Ruleset registry: resolving rules by `RulesetId@RulesetVersion`

## 8.1 The lookup key

The registry's own lookup key is the identical string `CatalogValidationContracts.cs`'s own `IsCompatibleWithActiveRuleset` already builds: `campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion` (section 3.4's "ruleset key"). No new key format is introduced.

## 8.2 Shape

A small, pure, `Odyssey.Rules`-layer type (e.g. `IRulesEngineRegistry`/`RulesEngineRegistry`, exact name left to `ODY-S06-102`'s own contract) exposes a `TryResolve(string rulesetKey, out IAttackRulesEvaluator evaluator)`-shaped lookup (the same `TryParse`/`TryX` convention every other lookup in this codebase already uses), backed by a plain, in-process dictionary populated by explicit code-level registration -- never a database table, never a content-catalog record, never I/O of any kind, satisfying `ADR-001` §6.2's own "Rules does not read the database" rule directly. Building/registering entries in this dictionary is a pure, side-effect-free operation (constructing objects and adding them to a map); nothing in this ADR requires the registry itself to perform any I/O, even though the composition code that constructs and populates it (a host-level concern, no composition root exists anywhere in this codebase today, confirmed by every prior task in `SLICE-05` that investigated this) necessarily runs at some startup or test-setup point.

## 8.3 Who calls it

The registry is consulted by whichever caller already has both the campaign's own `RulesetId`/`RulesetVersion` (from `campaign.Manifest`, an Application/Persistence-layer read) and needs to hand a concrete `IAttackRulesEvaluator` into `AttackEvaluationService.PreviewAttack`/`EvaluateAttack` (which already accepts one as a plain parameter, unchanged) -- this is necessarily an Application-layer or host-composition responsibility, not something `Odyssey.Rules` does to itself (Rules never reads `campaign.Manifest`, an Application-layer type). The mechanics-primitive interpreter (section 7) and `ActivateAbility` (section 9) resolve their own rules the same way, via the same registry, keyed the same way.

## 8.4 MVP scope

`SLICE-06`'s own MVP scenario registers exactly one ruleset implementation. The registry's own shape -- a keyed lookup, not a single hard-coded instance -- exists specifically so a second ruleset can be added later purely by registering a second entry, without changing `AttackEvaluationService`, `ActivateAbility`, or any caller's own code. This ADR does not require, and `ODY-S06-102`/`103` do not need to build, more than the one MVP registration.

---

# 9. `ActivateAbility` architecture

## 9.1 Shape

A new `ADR-002` root command, `ActivateAbility` (Application-layer orchestration mirroring `AttackApplyService`'s own established shape; concrete method signature is `ODY-S06-103`'s own contract to fix), taking at minimum: `CharacterAbilityId` (which ability instance is being activated), the activating actor's identity/authorization, and the chosen target(s) (consistent with the referenced `AbilityDefinition.TargetRule`'s own already-existing `ContentTargetRule` shape -- no new targeting vocabulary).

## 9.2 Steps

1. Load the `CharacterAbility` by id; resolve its own `AbilityDefinitionId` through the existing content-catalog read path (`IContentCatalogRepository`, unmodified) to the Published `AbilityDefinition` it currently pins -- the same "resolve the id through the catalog, never trust a cached copy" pattern `606`'s own `ItemEffectLifecycleService` already established for `EffectDefinitionRef`.
2. Authorize: the actor controls the owning Character (`ADR-019`, reused unmodified) or is MainGM -- no new permission concept.
3. Consume `AbilityDefinition.ResourceCosts` (already-existing `AbilityResourceCost` list) by the same `AdjustResource`-shaped mechanism section 7 defines for the ability's own effect primitives -- not a duplicated cost-application code path.
4. Interpret `MechanicsPayloadRef` through the shared mechanics-primitive interpreter (section 7), resolved against the ruleset registry (section 8) keyed by the activating character's own campaign's ruleset key.
5. Apply every resulting primitive atomically, in one command/transaction (`ADR-002`/`ADR-012`), or apply none of them -- the same all-or-nothing guarantee every other root command in this codebase already provides.
6. If the resolved formula for any primitive contains a dice-group term, the command derives its own authoritative random sample via `ADR-008`'s existing `IAuthoritativeRandomStreamFactory` before that transaction, with the same idempotency guarantee `ResolveAttack` already established: a retry of the same `CommandId` returns the original outcome and never re-derives randomness.

## 9.3 Combat binding: explicitly decided, not left open

For `SLICE-06`'s own MVP scope, `ActivateAbility` is NOT bound to `CombatEncounter` state: it does not require the activating character to be a current combat participant, does not check turn/round phase, and does not consume a combat action-cost slot. It works identically whether or not a `CombatEncounter` happens to be open. Using an ability as an authorized action inside an active combat turn (e.g. "spend your turn's action activating this ability instead of attacking") is an explicit, disclosed non-goal (section 12) for a future task to decide -- not an implicit consequence of this decision, and not blocked by it either: nothing in this architecture prevents a future task from adding a combat-turn-consuming variant that still calls the exact same interpretation/application machinery this section specifies.

---

# 10. Item use (`Instant` effect, roadmap block `D1`) -- named, not designed

Using an active item to trigger an `Instant`-duration effect reuses this ADR's own mechanics-primitive interpreter (section 7) and formula grammar (section 6) exactly as `ActivateAbility` does -- the item's own `EffectDefinition` (referenced the same way `AmmoDefinition.EffectContributionRefs`/`ItemDefinition.BuiltInEffectRefs` already reference effects elsewhere in this codebase) is interpreted through the identical primitive list, never a third, independently-designed execution path. This ADR does not specify `D1`'s own command shape, authorization boundary, or item-consumption semantics (single-use vs. charges, etc.) -- those remain that future task's own decision. This section exists only so that future task does not have to independently rediscover that it must reuse, not reinvent, this engine.

---

# 11. Module boundaries (`ADR-001` compliance)

Every new type this ADR specifies lives in `Odyssey.Rules`, organized by the module's own existing domain-folder convention:

- the formula grammar (section 6) -- e.g. `Packages/com.odyssey.rules/Runtime/Combat/` or a new `Packages/com.odyssey.rules/Runtime/Mechanics/` folder (exact placement is `ODY-S06-102`'s own contract decision, guided by whether the grammar is judged combat-specific or genuinely shared -- this ADR requires it be genuinely shared code regardless of folder);
- the mechanics-primitive interpreter (section 7) -- the same folder, since it is pure, deterministic, no-I/O code exactly matching `ADR-001` §6.2's own list ("formulas; expression evaluation; ... pure calculators");
- the ruleset registry (section 8) -- the same folder; it performs no I/O itself (section 8.2).

None of these types read the clock, RNG, database, or session state directly -- every external input (attribute values, RNG samples, the ruleset key) arrives as an explicit parameter, exactly as `ADR-001` §6.2 requires. `ActivateAbility`'s own orchestration (section 9) is Application-layer code (mirroring `AttackApplyService`), not part of the Rules Engine itself -- it calls into Rules, it does not live inside it, the same separation `AttackApplyService`/`AttackEvaluationService` already model for the attack pipeline.

---

# 12. Non-goals

This ADR does not:

- specify or implement `11_Content_Block_System`'s own full `ContentBlockGraph`, `SelectTargetsBlock` execution, or conditional/composable block logic (section 7.3);
- specify, balance, or author a complete, playable game system's worth of formulas, primitives, or ruleset content -- only the architecture and the minimal vocabulary `SLICE-06`'s own MVP scenario needs;
- specify or implement any playable UI -- the product owner confirmed the MVP is proven by test/harness, not a UI;
- revise, extend, or rename `RulesetDefinitionCatalog`/`RulesetMigrationRules.cs` (`ADR-025` §7.2) -- a different, already-accepted mechanism for a different problem;
- specify equipment/armor's effect on attack resolution, or movement/range/distance in combat (roadmap blocks B/C) -- `ODY-S06-102`'s own real `IAttackRulesEvaluator` may need simplified placeholder behavior for these until those blocks' own tasks land, but this ADR does not design that behavior;
- specify `ActivateAbility` as a combat-turn-consuming action (section 9.3) -- an explicit, disclosed future extension, not designed here;
- specify item-use's own command shape (section 10, roadmap block `D1`) -- named as a future consumer of this same engine only;
- change `IAttackRulesEvaluator`'s own interface signature, or any other already-accepted `ADR-027`/`028`/`029` contract.

---

# 13. Rules for Codex

Future implementation tasks (`ODY-S06-102`, `103`, and any later task touching formulas/primitives/ability activation) must:

1. implement the real `IAttackRulesEvaluator` against its existing, unmodified interface (section 5) -- never propose changing `Preview`/`Evaluate`'s own signature;
2. implement the formula grammar as a documented superset referencing `DiceFormulaParser`'s own published constants directly, never duplicating its numeric limits and never modifying `DiceFormula.cs`/`DiceFormulaParser.cs`/`09_Dice_And_Game_Log`'s own accepted grammar (section 6);
3. implement the mechanics-primitive schema exactly as section 7 fixes it (the `AdjustResource`/`ApplyEffect` primitives, the `schemaVersion`-gated envelope) unless a genuine gap is found, in which case the finding and the product-owner decision must be recorded in that task's own contract, not silently decided;
4. resolve rules by the `RulesetId@RulesetVersion` key exactly as section 8 fixes it, never inventing a second key format;
5. keep every Rules Engine type free of clock/RNG/database/session-state access -- all such input arrives as an explicit parameter (section 11);
6. implement `ActivateAbility` as a standalone root command outside `CombatEncounter` state, exactly as section 9.3 fixes it, unless the product owner explicitly revises that decision; and
7. never re-implement formula parsing or primitive interpretation independently for a new call site (attack, ability, item use) -- always call into the one shared engine (decision 1).

---

# 14. Definition of Done for future implementation tasks

Implementation decomposition and tests must prove at minimum:

1. the real `IAttackRulesEvaluator` implementation computes `AttackDelta`/`AttackEffectCandidate` values from a weapon's own `DamageExpression` and referenced effects, through the shared formula grammar and mechanics-primitive interpreter, without any change to `IAttackRulesEvaluator`'s own interface;
2. the formula grammar correctly parses and evaluates at least one dice-group term, one integer constant, and one attribute-reference term in combination, with a resolution failure for an unknown attribute reference being fail-closed (a typed error), never a silent zero;
3. the mechanics-primitive interpreter correctly applies at least one `AdjustResource` primitive (both a damaging/negative and a restoring/positive case) and one `ApplyEffect` primitive, each through already-existing, unmodified persistence paths (`ODY-S05-609`'s delta-application idiom; `ODY-S05-502`'s `CreateActiveEffect`);
4. the ruleset registry resolves a real `RulesetId@RulesetVersion` key to a real evaluator instance, and a request for an unregistered key fails closed (a typed error), never a silent default/fallback ruleset;
5. `ActivateAbility` atomically resolves, authorizes, costs, interprets, and applies an ability activation, or applies none of it on any failure; and
6. no Rules Engine type reads the clock/RNG/database/session state directly -- verified by an architecture guard (source-text scan), mirroring this codebase's own established convention for every prior module-boundary guarantee in this backlog.

---

# 15. Рассмотренные альтернативы

## 15.1 Extend `DiceFormulaParser`/`DiceTerm` with a third, attribute-reference term kind

**Rejected:** this would revise `09_Dice_And_Game_Log` §7.1's own already-accepted MVP grammar and its `ParserVersion = 1` contract, which the player-facing dice-roll command feature (`ODY-S03-005`) already ships on. A silent grammar change there risks an unintended behavior change for an unrelated, already-shipped feature, and this ADR has no authority to revise `ADR-009`.

**Accepted:** a new, `Odyssey.Rules`-layer superset parser (section 6.2) that reuses `DiceFormulaParser`'s own published constants by direct reference, never modifying `DiceFormulaParser.cs`/`DiceFormula.cs` itself.

## 15.2 One giant, general-purpose expression/scripting language for all mechanics content

**Rejected:** this is exactly `11_Content_Block_System` §8's own future `ContentBlockGraph` scope -- a materially larger design question (conditionals, composition, block graphs) than `SLICE-06`'s own MVP scenario needs, and a decision this ADR is not authorized or resourced to make well. Building it prematurely risks guessing wrong about a much bigger future system's own real requirements.

**Accepted:** a short, closed, versioned list of two primitives (section 7), explicitly extensible by adding new `"kind"` values later, explicitly not a program.

## 15.3 A database-backed rules registry (rules-as-content, editable per campaign)

**Rejected:** this would give `Odyssey.Rules` an implicit database dependency, directly violating `ADR-001` §6.2's own "Rules does not read the database" rule, and is a materially larger scope than `SLICE-06`'s own single-ruleset MVP needs.

**Accepted:** a pure, in-process, code-registered dictionary (section 8.2) -- extensible to multiple rulesets by adding registrations, with no I/O inside `Odyssey.Rules` itself.

## 15.4 `ActivateAbility` requires an active `CombatEncounter` (combat-only activation)

**Rejected:** many abilities are plausibly non-combat (a healing ritual, a utility ability) and `AbilityDefinition`/`CharacterAbility` carry no encounter-context requirement today; forcing combat-only activation would make the MVP scenario's own "activate an ability" beat artificially depend on combat-encounter setup it does not otherwise need.

**Accepted:** `ActivateAbility` is unconditional on `CombatEncounter` state for MVP (section 9.3); combat-turn-consuming activation is an explicit, disclosed future extension.

## 15.5 Fold the mechanics-primitive payload decode into `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect` directly

**Rejected:** this would couple two independently-versioned concerns (the catalog shape `ODY-S05-105`/`ADR-027` already accepted, and the mechanics-execution payload this ADR introduces) into one decode method and one `schemaVersion`, making a future change to either force a version bump of both.

**Accepted:** a separate decode method for the mechanics-primitive envelope (section 7.1), called only by the Rules Engine at interpretation time, never by the catalog-validation/decode path.

---

# 16. Открытые вопросы

None. The product owner's own governing ТЗ for this task required every one of sections 5-10's own design questions to be resolved here, not deferred -- confirmed against that requirement before this ADR was finalized: the formula-grammar reuse decision (section 6.1) is based on direct code inspection, not assumption; the mechanics-primitive schema (section 7) is fixed, not left as a future open question; the registry key and shape (section 8) are fixed; `ActivateAbility`'s own combat-binding question (section 9.3) is explicitly decided, not left open.

---

# 17. Трассировка

This ADR extends, without reopening:

- `ADR-001` §6.2 (`Odyssey.Rules` module boundary and its own "no I/O" rule, applied directly to every new type this ADR specifies);
- `ADR-008` (deterministic host RNG, reused verbatim for any dice-group term evaluated anywhere by this engine);
- `ADR-009`/`09_Dice_And_Game_Log` §7 (`DiceFormulaParser`'s own accepted MVP grammar, investigated and explicitly not modified -- section 6.1);
- `ADR-012` §5 (single-transaction commit pipeline, reused by `ActivateAbility`'s own atomic apply, section 9.2);
- `ADR-025` §7.2 (`RulesetDefinitionCatalog`, explicitly distinguished from and not reused by this ADR's own registry, section 4/8);
- `ADR-027` §6/§8.1/§8.2 (`DefinitionMechanicsSnapshot`, item-triggered `ActiveEffect` creation pattern reused by section 10's own future item-use consumer);
- `ADR-028` (`ActiveEffect`/`EffectMechanicsSnapshot`/`EffectStackPolicy`/`EffectDurationType`, all reused verbatim by section 7.2's `ApplyEffect` primitive);
- `ADR-029` (`IAttackRulesEvaluator`, `AttackEvaluationSnapshot`, `AttackDelta`, `AttackEffectCandidate`, all reused verbatim by section 5's own real evaluator architecture); and
- `11_Content_Block_System` §7/§8/§14/§21/§22 (`ContentTargetRule`/`AbilityEntryPointType`/`EffectDurationType`/`EffectStackPolicy` vocabulary reused verbatim; §8's own `ContentBlockGraph` explicitly not implemented, section 7.3/12).

Existing ADRs reused without redefinition: `ADR-002` (commands, idempotency), `ADR-004` (typed failures), `ADR-019` (permissions/audience baseline, reused by `ActivateAbility`'s own authorization step).

---

# 18. Нормативное действие

**This ADR is Accepted.** It is binding for `ODY-S06-102` (the real `IAttackRulesEvaluator` implementation and formula grammar), `ODY-S06-103` (`ActivateAbility`), and any later `SLICE-06` task touching formula evaluation, mechanics-primitive interpretation, or ruleset resolution. This ADR authorizes creation of `docs/tasks/SLICE-06_IMPLEMENTATION_BACKLOG.md` and the corresponding `ODY-S06-101` backlog status update.
