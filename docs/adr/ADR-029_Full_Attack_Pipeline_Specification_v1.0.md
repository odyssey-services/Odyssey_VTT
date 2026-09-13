# ADR-029 - Full Attack Pipeline Specification

**Документ:** `docs/adr/ADR-029_Full_Attack_Pipeline_Specification_v1.0.md`  
**ADR:** ADR-029  
**Версия:** 1.0  
**Дата:** 2026-09-13  
**Статус:** Proposed — pending product-owner acceptance  
**Область:** authoritative combat turn/round timeline and attack resolution pipeline, including combat-sourced `ActiveEffect` application. This ADR extends the boundary left by `ADR-028` §13; it does not amend `ADR-001`–`ADR-028`.  
**Связанные этапы:** `SLICE-05`, Block 3, task `ODY-S05-601`  
**Базовые документы:** `ADR-001` §1–§5; `ADR-002` §3–§6; `ADR-003` §3–§7; `ADR-004` §3–§5; `ADR-008` §3–§6; `ADR-010` §3–§7; `ADR-012` §5; `ADR-019`; `ADR-027` §1 rule 13, §5, §8.2, §11–§14, §18; `ADR-028` §8, §12–§15, §19; `docs/tasks/SLICE-05_BACKLOG.md` §3.2.

---

# 1. Решение

Odyssey VTT resolves combat only on the authoritative host through one ordered, replay-safe attack pipeline. A combat encounter owns an ordered round/turn timeline. A submitted action is either a pure preview, a durable `Pending` intervention, or an accepted/rejected authoritative command outcome; the Unity client never owns a combat timeline or final result.

The historical in-repository anchor is quoted verbatim, without claiming access to its absent roadmap source:

> "Roadmap section 14.6 names action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, and game log. Those are full implementation and likely further ADR/task-decomposition work. `ADR-027` deliberately fixes only the item/equipment/content-catalog substrate needed before that pipeline can safely reference items, equipment, ammo, abilities, and effects."
>
> — `docs/tasks/SLICE-05_BACKLOG.md` §3.2

The following decisions are mandatory:

1. **Encounter timeline:** a `CombatEncounter` is an authoritative campaign aggregate with a stable ordered participant list, `RoundOrdinal`, current participant, and turn lifecycle. The host advances the timeline only through Application commands. Ruleset-specific initiative formulae, participant eligibility, and action allowance are `Odyssey.Rules` decisions; their evaluated order is stored in the encounter so retry and replay never reroll initiative.
2. **Round and turn:** a round is one complete pass through the encounter's current ordered participant list. A turn begins with `TurnStarted`, permits only Rules-authorized actions, and ends with `TurnEnded`; the final turn ending advances to the next `RoundStarted`. A join, leave, defeat, or initiative-order change is an explicit authoritative command and takes effect only at a Rules-defined safe boundary. It never silently mutates an in-flight attack.
3. **Preview is non-authoritative:** preview validates a proposed action against an identified state/revision and computes a provisional explanation. It performs no state mutation, does not consume costs, does not persist an idempotency outcome, and never consumes authoritative RNG. A preview can be discarded or recalculated freely.
4. **Commit recomputes:** an authoritative `ResolveAttack` command never trusts a preview result. It revalidates authorization, encounter/action-window state, references, range, modifiers, and costs from current authoritative state before it rolls. A stale preview is explanatory only, not a conflict or a source of authority.
5. **Pipeline and commit:** the source's ordered named sequence is specified in section 6. Stages through intervention create an immutable proposed resolution only. `atomic apply` is the first mutation point: it commits all accepted state changes, ordered DomainEvents, command outcome/idempotency record, and the Game Log projection in one `ADR-012` transaction, or commits none of them.
6. **Intervention:** Rules may declare a legal intervention window. If one is required, `ResolveAttack` records a durable `Pending` outcome and immutable proposed resolution, including already-derived random outcomes. A later `ResolveAttackIntervention` command is a new root command under `ADR-002`; it revalidates the pending resolution and either applies it atomically or rejects/cancels it by its recorded Rules outcome. No handler invokes another handler, and a pending attack is never rerolled.
7. **Compensation:** compensation is never rollback, event deletion, or journal rewriting. After an accepted apply, a separate, authorized compensating root command records new corrective state and causally linked new events. It is itself subject to normal validation, idempotency, permissions, redaction, and Game Log projection.
8. **Combat effects:** a combat roll does not implicitly apply every effect associated with a weapon, item, ability, or damage result. `Odyssey.Rules` returns an explicit `EffectApplicationDecision` for each candidate effect: `Apply`, `DoNotApply`, or `RequiresIntervention`, with the trigger condition and target(s) it evaluated. Only `Apply`, or an intervention-approved decision, creates/updates an `ActiveEffect` during atomic apply. Each non-instant effect captures its `EffectMechanicsSnapshot` at that point, as `ADR-027` §8.2 and §11 require.
9. **Duration integration:** the six values reserved by `ADR-028` §13 receive the timeline semantics in section 7. They are legal only when the effect binds both a source combat participant and target combat participant at application; otherwise the command is rejected before a roll or apply.
10. **Security and visibility:** actor controllers may submit an action only when existing `ADR-019` authorization permits control of that actor. MainGM may submit and resolve interventions; AssistantGM has only the scope `ADR-019` grants. A Rules-declared eligible controller may choose their own intervention option, but a MainGM resolves an ambiguous, contested, or expired intervention. Clients receive audience-filtered result/Game Log projections, never raw DomainEvents, hidden modifiers, RNG secrets, or unredacted pending resolutions.

---

# 2. Контекст и проблема

`ADR-027` deliberately excludes the full attack pipeline in §1 rule 13, §13, and §18. It nevertheless establishes prerequisites this ADR reuses: Inventory is a separate aggregate (§5); attacks may atomically affect inventory, Character resources, `ItemInstance`, `ActiveEffect`, Game Log, and DomainEvents (§5); runtime item/effect mechanics come from immutable application-time snapshots (§6, §8.2, §11); ordinary players do not submit trusted final damage state (§12).

`ADR-028` specifies the generic `ActiveEffect` aggregate, including its shared saving pipeline (§12), but explicitly reserves combat/damage-sourced effect application and the six turn/round duration mechanisms for this block (§13). Its §14 confirms the pipeline itself, its turn/round infrastructure, and implementation work were out of scope there.

No accepted ADR previously defines a combat timeline, an authoritative attack transaction, or the point at which a roll can apply an effect. This ADR supplies those boundaries without selecting a game system's hit formula, armor model, initiative formula, balance values, or content payload schema.

---

# 3. Термины

## 3.1 CombatEncounter

The authoritative campaign aggregate that records combat participation, evaluated turn order, round/turn ordinals, current lifecycle state, and the active turn. It is not a Unity scene object or a client-owned timer.

## 3.2 ActionIntent and preview

`ActionIntent` is the actor, selected action source, declared target(s), chosen legal options, and expected encounter/revision guards submitted for an attempted attack. A preview is a non-persisted, non-authoritative evaluation of such an intent against a supplied state view.

## 3.3 Proposed resolution and pending resolution

A proposed resolution is the immutable Rules/Application input and derived result prepared before apply. It includes the exact action, evaluated inputs, Ruleset version, snapshot references, random-result values, candidate effect decisions, and intervention deadline/choices when applicable. A pending resolution is its durable form after a `Pending` command outcome; it is not final state and cannot be edited by a client.

## 3.4 Combat participant bindings

`SourceCombatantRef` is the encounter participant acting through an action, even where an `ActiveEffect.SourceRef` is an item, ability, or action. `TargetCombatantRef` is the participant affected by the effect. Both are captured for the duration types in section 7.

---

# 4. Turn and round structure

The host serializes lifecycle commands for each `CombatEncounter`. No wall-clock callback directly changes combat state: a scheduler may request a new Application command to examine an expired intervention, as required by `ADR-008`, and that command rechecks current state.

1. **Encounter setup:** an authorized setup command creates the encounter and evaluates its initial participant order with a Ruleset version and, if randomness is required, the injected authoritative RNG. The evaluated order is persisted. An implementation must not call `System.Random`, Unity RNG, direct wall clock, or UI timing in authoritative logic (`ADR-008`).
2. **Round start:** `RoundStarted(RoundOrdinal)` occurs before the first eligible participant's `TurnStarted`. Effects whose scheduled boundary is this round start are evaluated before actions in that round. An inconclusive expiry check leaves an effect active, following `ADR-028` §11.
3. **Turn start:** `TurnStarted(Participant, TurnOrdinal)` activates that participant's Rules-defined action window. Effects ending at that participant's turn start expire before normal actions; effects beginning from a committed application are never retroactively expired by the boundary in which they were created.
4. **Turn end:** `TurnEnded(Participant, TurnOrdinal)` closes the normal action window after pending work that Rules require to resolve before turn completion. Effects ending at turn end are evaluated after the turn's final committed action.
5. **Advance:** after the last eligible turn ends, the encounter advances to the next `RoundStarted`. Skipped/ineligible participants have an explicit recorded lifecycle result; the host does not silently alter ordinal sequence or resolve their actions.
6. **Interruption:** an attack awaiting intervention retains its pending resolution. It either resolves before its recorded Rules-defined deadline/boundary or is completed/cancelled by the explicit expiry outcome the Rules recorded. The resolution is not recalculated merely because a client reconnects or a presentation timer elapses.

---

# 5. Preview, command, and authority boundaries

Preview may calculate range, a modifier explanation, conditional branch descriptions, estimated costs, and a clearly labelled *non-final* potential outcome. It must carry the state/revision and Ruleset version it used, but it must not reserve ammunition/resources, create `ActiveEffect`, write Game Log, emit DomainEvents, allocate a durable command outcome, or derive an authoritative random result.

`ResolveAttack` is an Application command with `CommandId` and semantic fingerprint under `ADR-002`. Reusing its `CommandId` with differing actor or semantic content is an error; an exact retry returns the original durable `Accepted`, `Pending`, or `Rejected` outcome. Expected invalid intent, authorization, rules, conflict, and unavailable-reference outcomes are typed `Result` errors/safe reasons under `ADR-004`. An unexpected technical failure before a durable outcome is an outer failure, not a fabricated `Rejected` result.

The Application layer authorizes and orchestrates. `Odyssey.Rules` makes pure deterministic computations from supplied snapshots, including range legality, ordered modifiers, hit/body-part/armor/damage calculations, action/cost legality, effect trigger evaluation, and intervention options. Rules never commit campaign state, open a transaction, select an audience, or query Unity. Random draws are supplied by the Application-owned authoritative RNG port using independent streams derived as `ADR-008` requires from campaign RNG key, command identity, decision ordinal, purpose, and Ruleset version; the secret never enters a projection, diagnostic event, or DTO.

---

# 6. Ordered attack pipeline

The historical quote contains fifteen named labels although task material sometimes calls it a "fourteen-step" conveyor. This ADR preserves every label and specifies fifteen ordered stages; it does not merge, rename, or silently omit any source label.

| Stage | Responsibility | Authority / side effects |
|---|---|---|
| 1. action intent | Receive actor, action source, declared targets/options, expected revisions, and `CommandId`; validate request shape and caller authority. | Preview: pure. Command: validation only; no mutation. |
| 2. preview | Explain a provisional result against an identified snapshot/revision. | Non-authoritative, discardable, no RNG/cost/effect/log side effect. |
| 3. range | Rules evaluates topology/distance/line constraints from the authoritative scene/encounter snapshot. | Recomputed by command; result enters proposed resolution only. |
| 4. modifiers | Rules collects applicable snapshot-based modifiers in stable Rules-defined order, including active effects and equipment. | Recomputed; hidden modifiers remain host-only. |
| 5. roll | Application requests the necessary independent authoritative RNG stream(s); Rules maps draws to outcome. | Command-only; values are retained in a pending resolution and never rerolled. |
| 6. hit | Rules evaluates hit/miss/other Ruleset outcome from range, modifiers, and roll. | Proposed resolution only. |
| 7. body part | When the Ruleset and hit outcome require it, Rules selects/validates a body part from the captured legal set. | Proposed resolution only; no anatomy mutation. |
| 8. armor | Rules evaluates relevant armor/equipment snapshot and protection state. | Proposed resolution only; no durability mutation. |
| 9. damage | Rules calculates resulting damage/state deltas, including protected/hidden components. | Proposed resolution only. |
| 10. costs | Rules validates and computes action costs (ammunition, charge, resource, durability, or other snapshot-defined costs). | Proposed resolution only; no reservation or deduction yet. |
| 11. effect application | Rules evaluates each explicitly declared candidate effect and produces its `EffectApplicationDecision`. | Proposed resolution only; no `ActiveEffect` exists yet. |
| 12. intervention | Rules supplies legal choices/deadline. Application records `Pending` if a choice is required; otherwise records no intervention. | Durable pending outcome only when needed; never client-owned final state. |
| 13. atomic apply | On immediate acceptance or later valid intervention resolution, Application rechecks guards and commits all accepted deltas. | First mutation; one `ADR-012` transaction or none. |
| 14. compensation | Correct a completed result through a separate causally linked root command. | New authoritative transaction/events; never rollback/delete prior history. |
| 15. game log | Build the audience-filtered narrative/mechanical projection of the committed result. | Produced as part of atomic apply; never raw DomainEvents or hidden data. |

At atomic apply, the transaction includes all affected aggregate revisions and only the deltas approved by the resolution: encounter state, item/ammo/armor state, character resources/anatomy, `ActiveEffect` rows, append-only DomainEvents, idempotency outcome, and Game Log projection. If an optimistic guard, item/effect snapshot, target state, or authorization no longer validates, the command produces its typed conflict/rejection before any partial state commits. A technical commit failure leaves no durable command outcome; a retry is not an excuse to reuse or reroll a partially observed result.

---

# 7. `EffectDurationType` combat semantics

`ADR-028` §13 reserves these six values while §8 remains the authority for all other duration types. The following rules give only the combat timeline mechanism that §13 reserved; they do not alter `ActiveEffect` ownership, stacking, snapshot, fail-closed, or removal rules.

| Duration type | Required value / bindings | Expiry semantics |
|---|---|---|
| `ForRounds` | Positive whole-round count `N`; source and target combat participant bindings. | The partial round in which the effect is committed is not counted. The effect remains active for the next `N` complete encounter rounds and expires at the next `RoundStarted` after them. Thus an effect committed in round `R` with `N = 1` expires before actions in round `R + 2`. |
| `ForTurns` | Positive whole-turn count `N`; target combat participant binding. | Count the next `N` complete turns of the target that start strictly after application. The effect expires at the `TurnEnded` boundary of the Nth counted target turn. |
| `UntilSourceTurnStart` | Source combat participant binding. | Expires immediately before that source participant's first `TurnStarted` strictly after application. |
| `UntilSourceTurnEnd` | Source combat participant binding. | Expires immediately after that source participant's first `TurnEnded` strictly after application. |
| `UntilTargetTurnStart` | Target combat participant binding. | Expires immediately before that target participant's first `TurnStarted` strictly after application. |
| `UntilTargetTurnEnd` | Target combat participant binding. | Expires immediately after that target participant's first `TurnEnded` strictly after application. |

All six durations capture the encounter ID, source/target bindings, relevant ordinal, and Ruleset version with the application snapshot. A participant becoming ineligible does not silently erase an effect: the normal explicit lifecycle/Rules outcome determines the next relevant boundary or a compensating/removal command. A duration that cannot bind to required combat participants is invalid rather than guessed. A trigger that cannot be conclusively evaluated fails closed and leaves the effect active, per `ADR-028` §11.

---

# 8. `ActiveEffect` and combat effect application

An effect candidate is part of the action/equipment/ability snapshot selected by the action intent; it is not discovered by loading the latest content definition. Ruleset content declares the candidate's trigger and requirements. During stage 11, Rules evaluates the candidate against the already-computed resolution and returns an `EffectApplicationDecision` containing:

- candidate/effect snapshot reference and target selection;
- evaluation result (`Apply`, `DoNotApply`, or `RequiresIntervention`);
- the public-safe reason category and the host-only factual inputs needed to reproduce it;
- duration bindings where a combat duration is selected; and
- any stacking key/options consumed by `ADR-028`'s existing `EffectStackPolicy` handling.

The Application handler may create or stack an `ActiveEffect` only when the final decision is `Apply`. It does so through the standalone `IActiveEffectRepository` and shared `SqliteSavingPipeline` required by `ADR-028` §12 and §15, as one participant in the attack's atomic apply transaction. It never appends methods to Inventory or Character repositories and never lets a client submit an effect snapshot or final application decision as trusted state. `Instant` outcomes are applied as the Rules-calculated atomic state delta, not represented as a spurious persistent `ActiveEffect`.

---

# 9. Module boundaries, contracts, and persistence

This ADR creates no new production module and preserves `ADR-001` dependency direction:

- `Odyssey.Domain` owns pure encounter/timeline, pending-resolution, identity, and invariant vocabulary. It has no Unity, persistence, networking, serializer, RNG, clock, or logging dependency.
- `Odyssey.Rules` owns deterministic evaluation of pipeline stages 3–11 and intervention choices. It may depend only on Domain inputs and cannot mutate state.
- `Odyssey.Application` owns command handlers, authorization, idempotency, injected clocks/RNG/scheduler, transaction orchestration, and audience-projection requests. It does not call one handler from another.
- `Odyssey.Persistence` owns repository implementations and one shared transaction that persists Application-approved state. It does not decide attack legality or communicate with Networking.
- `Odyssey.Networking` maps approved audience-filtered projections to transport; `Odyssey.Unity.Client` presents previews/projections and submits intents. Neither is authoritative.

Any future storage/network contracts are explicit versioned DTOs with `ADR-003` codecs and compatibility limits. They never serialize Domain aggregates directly or contain CLR type names. Diagnostics follow `ADR-010`: structured allowlisted fields only, with no user content, hidden target/body-part/modifier information, full paths, RNG secret, stack trace, or raw object `ToString()` output.

---

# 10. Non-goals

This ADR does not:

- implement production code, tests, schema, migrations, DTOs, commands, UI, or a transport protocol;
- choose a Ruleset's initiative, hit, range, armor, damage, modifier, cost, body-part, or effect-content formula;
- define a generic potency scale, effect payload schema, or balanced catalog content;
- revise `ActiveEffect` aggregate ownership, stacking policy, non-combat duration semantics, explicit removal, or fail-closed rule in `ADR-028`;
- allow a client to apply trusted combat results, alter a durable pending resolution, or see hidden combat information;
- turn a compensating command into rollback or permission to rewrite persisted DomainEvents; or
- decompose `ODY-S05-602` or higher task IDs. That is a future, separate backlog revision after this ADR is accepted.

---

# 11. Rules for Codex

Future implementation tasks must:

1. use the ordered stages and preview/commit boundary in section 6, never applying costs/effects during preview;
2. recompute authoritative resolution from current state and never trust client previews or final damage/effect values;
3. use injected `ADR-008` RNG and preserve pending random outcomes; never reroll a retry, replay, reconnect, or intervention;
4. implement all six duration types exactly as section 7 specifies, and reject missing participant bindings;
5. create/stack combat `ActiveEffect` only through section 8's explicit decision and `ADR-028` mechanisms;
6. route all atomic state through one `ADR-012` transaction and correct completed state only by a compensating command;
7. produce redacted Game Log and transport projections from committed state, never raw DomainEvents; and
8. leave `ODY-S05-602` onward for a separately approved backlog-decomposition task.

---

# 12. Definition of Done for future implementation tasks

Implementation decomposition and tests must prove at minimum:

1. preview can be repeated/cancelled without mutation, cost consumption, persisted outcome, Game Log entry, or RNG draw;
2. the same `ResolveAttack` command retry returns its original outcome and does not reroll;
3. a stale preview is recomputed and cannot apply stale range/modifier/cost/effect data;
4. immediate and intervened attacks each commit every approved aggregate delta, event, idempotency outcome, and Game Log projection atomically or commit none;
5. an intervention produces a durable pending resolution and resolution/cancellation does not invoke another handler or reroll;
6. each combat duration boundary has the section 7 expiry behavior and inconclusive expiry remains active;
7. an on-hit/damage effect is applied only when an explicit Rules decision approves it and captures the application-time snapshot;
8. a compensating command appends corrective history without deleting or mutating prior events; and
9. client/audience projections omit hidden combat data and authoritative RNG material.

---

# 13. Рассмотренные альтернативы

## 13.1 Apply costs/effects while previewing

**Rejected:** a preview is intentionally cancellable and recalculable. Reserving ammunition, consuming resources, creating effects, or writing a log entry there would make presentation state authoritative and violate `ADR-002` root-command boundaries.

**Accepted:** preview is pure; all mutation occurs only in atomic apply.

## 13.2 Let the client supply a final roll/damage/effect result

**Rejected:** this conflicts with `ADR-027` §12's prohibition on ordinary players submitting trusted final damage state and with `ADR-008` deterministic host RNG.

**Accepted:** the host validates intent and derives all final values.

## 13.3 Roll again after a pending intervention

**Rejected:** it makes intervention/retry timing alter an authoritative random outcome and violates `ADR-008` duplicate/replay determinism.

**Accepted:** retain the derived outcome in the durable pending resolution.

## 13.4 Roll back an incorrect accepted attack

**Rejected:** `ADR-002` forbids mutation/deletion of persisted DomainEvents.

**Accepted:** an authorized compensating command records new corrective state/events.

---

# 14. Открытые вопросы

1. `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §14.6 is cited by `ADR-027` and the historical backlog, but no `Documentation/` directory or roadmap file is tracked in this repository. This ADR deliberately relies only on the verbatim `SLICE-05_BACKLOG.md` §3.2 quote and `ADR-028` §13/§14 boundary. **Product owner decision requested:** confirm whether the unavailable roadmap contains additional mandatory pipeline requirements before this ADR may be accepted.
2. The historical quote lists fifteen labels, while task material calls the conveyor "fourteen-step." This ADR preserves all fifteen labels rather than silently combine two. **Product owner decision requested:** confirm whether the count is merely editorial or whether a specific pair is intended to be one stage.
3. The Ruleset-specific definitions of intervention options/deadlines and initiative/order changes are intentionally not invented here. Future implementation must use the section 4/6 architecture and obtain a concrete Ruleset contract before implementing those payloads.

---

# 15. Трассировка

This ADR extends, without reopening:

- `ADR-027` §5 (multi-aggregate attack transaction context), §8.2 and §11 (application-time `ActiveEffect` snapshots), §12 (trusted-final-state boundary), §13 and §18 (pipeline was explicitly deferred), and §14 (module ownership);
- `ADR-028` §12 (shared saving pipeline), §13 (six durations and combat effect application reserved here), §14 (its non-goals), §15 (module responsibilities), and §19 (deferred combat items);
- `docs/tasks/SLICE-05_BACKLOG.md` §3.2, quoted verbatim in section 1 as the only tracked enumeration of the pipeline; and
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §16/§16.1, which reserves only `ODY-S05-601` for this ADR and leaves `ODY-S05-602` onward undecomposed.

Existing ADRs reused without redefinition: `ADR-001` (module boundaries), `ADR-002` (commands, idempotency, events), `ADR-003` (explicit contracts), `ADR-004` (typed failures), `ADR-008` (clock/RNG), `ADR-010` (diagnostics/redaction), `ADR-012` (single transaction and append-only journal), and `ADR-019` (permissions/audience baseline).

---

# 16. Нормативное действие

**This ADR is Proposed.** It becomes binding only after the repository's normal product-owner ADR acceptance process. Until then, it enables no implementation task and does not authorize any update of the `ODY-S05-601` backlog status.

Once accepted, a separate backlog-revision task may decompose `ODY-S05-602` onward. Those tasks must implement the decisions above rather than renegotiating them inline; a change requires an ADR amendment or superseding ADR.

---

**Конец документа**
