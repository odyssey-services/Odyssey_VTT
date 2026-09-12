# ADR-028 - ActiveEffect Aggregate Specification

**Документ:** `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`
**ADR:** ADR-028
**Версия:** 1.0
**Дата:** 2026-09-12
**Статус:** Accepted
**Область:** the `ActiveEffect` aggregate itself — field shape, persistence/repository contract, stacking-rule integration with `EffectStackPolicy`, duration/expiry mechanism for `EffectDurationType`, explicit removal, and permissions. Extends `ADR-027` §8.1/§8.2; does not reopen them.
**Связанные этапы:** `SLICE-05` item-sourced abilities/effects block, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14/§14.1, task `ODY-S05-501`
**Базовые документы:** `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`; `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`; `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`; `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`; `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`; `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`; `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`; `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (parent ADR — §8.1/§8.2, §11, §12 in particular); `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14.6 (full attack pipeline, the deliberately excluded sibling block)

---

# 1. Решение

`ADR-027` §8.2 fixes how `ActiveEffect` *integrates* with items and Characters, but never specifies the aggregate itself. This ADR specifies it, without reopening any `ADR-027` §8.1/§8.2/§11/§12 rule.

Обязательные решения:

1. **Aggregate shape:** `ActiveEffect` is a standalone campaign aggregate root — not owned by Inventory or Character (`ADR-027` §8.2 rule 2) — with the minimum field set in section 5.
2. **Persistence:** a new `IActiveEffectRepository` contract and `SqliteActiveEffectRepository` implementation, structurally parallel to `IInventoryRepository`/`ICharacterRepository`, not an extension of either.
3. **Stacking:** each of `EffectStackPolicy`'s 7 existing values maps to one specific, concretely-described aggregate behavior (section 7), including a new lightweight pending-conflict record for `RequestGMResolution` — not a vague "GM decides somehow."
4. **Duration/expiry:** each of `EffectDurationType`'s 15 existing values maps to an explicit expiry mechanism or an explicit statement that the mechanism belongs to the full attack pipeline block (section 8). No value is left undecided by omission.
5. **Removal:** an explicit `RemoveActiveEffect` command ends an effect before its own natural expiry (section 9).
6. **Permissions:** creating an `ActiveEffect` through item use inherits the existing item-use permission model (`ADR-027` §12 rule 4); creating one directly (not through an item) and removing one early are both MainGM-only, new restrictions this ADR introduces (section 10).
7. **Fail-closed expiry:** an expiry check that cannot be conclusively evaluated never silently expires the effect (section 11).
8. **Atomicity:** `SqliteActiveEffectRepository` routes every mutation through the same shared `ADR-012` §5 single-transaction commit pipeline (`SqliteSavingPipeline`) already used by four other repositories — not a new one-off mechanism (section 12).
9. **Block 3 boundary:** this ADR specifies only the generic `ActiveEffect` mechanism and its item integration (`ADR-027` §8.1/§8.2); combat/damage-sourced effects and the six turn/round-based `EffectDurationType` values remain the full attack pipeline block's own territory (section 13).

This ADR does **not** implement product code, schema, DTOs, Unity UI, or command handlers. It does not revisit `ADR-027` §7 (Equipment), §10 (`ItemDefinition` migration), §11 (`ActiveEffect` never mass-migrates — reused as-is, see section 4), or §12 (permissions baseline — extended, not amended).

---

# 2. Context and problem

`docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 (added by `ODY-S05-110`) found, by direct repository and ADR search, that `ADR-027` §8 is not a complete specification the way §10 (`ItemDefinition` migration) is: no `ActiveEffect` class exists anywhere in the codebase, and no ADR besides `ADR-027` mentions `ActiveEffect` at all. §8.1/§8.2 state the item-integration *rules* (creation trigger, reference-only storage, `WhileItemEquipped` lifecycle coupling) but never the aggregate's own field shape, persistence, stacking-rule integration, expiry mechanism for the other 14 duration values, removal command, or authorization model. `ODY-S05-110` decomposed this gap into exactly one ADR-specification task, `ODY-S05-501` — this document is that task's own result.

---

# 3. Terms

## 3.1 ActiveEffect

A campaign-authoritative record that a specific `EffectDefinition`, at a specific published version, was applied to a specific target, at a specific time, with a mechanics snapshot captured at that moment — analogous to `ItemInstance`'s `DefinitionMechanicsSnapshot` (`ADR-027` §6.1), but for effects instead of items.

## 3.2 EffectMechanicsSnapshot

An immutable copy of an `EffectDefinition`'s mechanics (`TargetRule`, `DurationType`, `DurationValue`, `StackPolicy`, `MechanicsPayloadRef`-resolved payload), captured at application time — mirroring `ADR-027` §3.4's mechanics-snapshot concept and `ItemMechanicsSnapshot`'s own field shape (`SourceDefinitionRef`, `DefinitionSnapshotVersion`, `ContentType`, `Payload`), reinvented for effects rather than copied verbatim.

## 3.3 SourceRef / TargetRef

Extensible kind-tagged references, mirroring `InventoryItemRef`'s own discriminated-union shape (`ItemInstance`/`ItemStack` kinds today). `SourceRef` names what created the effect (an item, an equipped item, an action, or a direct GM application). `TargetRef` names what the effect affects (a Character, an `ItemInstance`, or a scene object). Both are extensible enums with a reserved-but-unimplemented kind where the referenced entity type does not exist yet (section 5.3).

---

# 4. Relationship to `ADR-027`

This ADR extends `ADR-027` §8.1/§8.2 "vglub'" (deeper), without amending them. Every rule below is additive to, and consistent with, the parent ADR's own three §8.2 rules, quoted here for reference only (not re-decided):

> 1. Character, ItemInstance, and SceneObject store only `ActiveEffect` references or derived projections.
> 2. Existing ActiveEffects are not owned by Inventory or Character, and do not mass-migrate when their source `EffectDefinition` changes.
> 3. Effects with duration `WhileItemEquipped` subscribe to authoritative `ItemEquipped`/`ItemUnequipped` events; they expire, suppress, or remove through ActiveEffect lifecycle commands/events, not by directly mutating Character or item snapshots.

`ADR-027` §11's "`ActiveEffect` never mass-migrates" rule is reused as-is: this ADR's own snapshot design (section 5) is exactly what makes that rule implementable — an `ActiveEffect` retains its own `EffectMechanicsSnapshot` from application time, unaffected by a later `EffectDefinition` publish, the same relationship `ItemInstance` already has with `ItemDefinition`.

**Citation correction, not repeated here:** `ADR-027` §14/§19 state that `ADR-024` "governs... existing `CharacterAbility` acquisition patterns" / "`CharacterAbility` acquisition/source patterns." Direct search of `ADR-024`'s own text finds zero mentions of `CharacterAbility` anywhere — that citation is already-accepted `ADR-027`'s own inaccuracy, not corrected here (amending `ADR-027`'s own text is out of this ADR's scope; see section 14). This ADR does not repeat the error: where it cites `ADR-024`, it cites it only for what `ADR-024` actually governs (development economy / progression transactions), never for `CharacterAbility` semantics, which `ADR-027` §8.1 (not `ADR-024`) already owns.

---

# 5. The `ActiveEffect` aggregate

**Decision:** `ActiveEffect` is a standalone campaign aggregate root, structured by direct analogy to `ADR-027` §7's own `EquippedEntry` precedent (a minimum field record plus numbered rules, not a narrative description).

## 5.1 Minimum record

```text
ActiveEffect
├── ActiveEffectId
├── CampaignId
├── EffectDefinitionRef
├── EffectMechanicsSnapshot
├── SourceRef
├── TargetRef
├── Status
├── StackCount
├── AppliedByUserId
├── AppliedAt
├── ExpiresAt
└── Revision
```

## 5.2 Rules

1. `EffectDefinitionRef` pins the exact published `EffectDefinition` version that created this effect, the same exact-version discipline `ADR-027` §6.1 already requires of `ItemInstance.SourceItemDefinitionRef`.
2. `EffectMechanicsSnapshot` is captured once, at application time, from the pinned `EffectDefinitionRef`'s `PropertiesJson`. It never changes for the lifetime of this `ActiveEffect`, including after the source `EffectDefinition` is republished, archived, or migrated (`ADR-027` §11).
3. `Status` is one of `Active`, `Expired`, `Suspended`, `Removed`. `Suspended` exists only for `WhileItemEquipped` effects while the source item is unequipped but not yet destroyed/consumed (`ADR-027` §8.2 rule 3's own "expire, suppress, or remove" trichotomy) — a suspended effect is not mechanically active but is not yet permanently gone, and resumes to `Active` if the same item is re-equipped before any other terminal transition. Every other duration type (section 8) only ever transitions `Active → Expired` or `Active → Removed`, never through `Suspended`.
4. `StackCount` starts at 1 on creation and is the only field `EffectStackPolicy.IncreaseStacks` (section 7) ever increments; every other stacking policy leaves it at 1 for the lifetime of the row (a policy that creates a genuinely independent second `ActiveEffect` row, like `IndependentInstances`, does so as a second row with its own `StackCount = 1`, never by incrementing the first row's).
5. `Revision` is the optimistic-concurrency guard for every mutation (stacking update, suspend/resume, expiry, removal) — the same CAS pattern every other aggregate in this codebase already uses (`InventoryRecord.Revision`, `EquippedEntry.Revision`, etc.).
6. `ExpiresAt` is populated only for duration types with a computable absolute host-clock timestamp at application time (`ForDuration`; optionally `UntilSessionEnd`/`UntilSceneChange` if the future implementation task chooses to precompute an estimate for UI display) — it is never the sole authority for whether an effect has actually expired; the authoritative check is always the duration-specific mechanism named in section 8, since several duration types (`WhileCondition`, `WhileItemEquipped`, `WhileSourceExists`, turn/round-based ones) have no fixed timestamp at all. A null `ExpiresAt` is not itself a bug; it means this effect's expiry is event/condition-driven, not clock-driven.
7. Deleting a `CampaignId`'s data (backup restore, campaign deletion) removes `ActiveEffect` rows the same way it already removes every other campaign-scoped aggregate; no cross-campaign `ActiveEffect` reference is created by this ADR.

## 5.3 `SourceRef` / `TargetRef` shape

Both are extensible, kind-tagged reference structs, mirroring `InventoryItemRef`'s own two-kind discriminated union:

```text
SourceRef: Kind ∈ { Item, EquippedItem, Action, GMDirect } + the referenced id (ItemInstanceId/ItemStackId for Item/EquippedItem kinds; none for GMDirect)
TargetRef: Kind ∈ { Character, ItemInstance, SceneObject } + the referenced id
```

`SceneObject` is reserved as a `TargetRef` kind, per `ADR-027` §8.2's own text naming it as a supported target — but no `SceneObject` domain type exists anywhere in the codebase today (confirmed by direct search). The future implementation task that first needs a real scene-object target must add that type itself and wire this reserved kind to it; until then, `TargetRef.Kind = SceneObject` is a declared-but-unconstructable enum value, exactly the same "structurally accepted, not yet automatically producible" pattern `Ability.cs`'s own `SourceKind.ActiveEffect`/`CharacterTemplate` values already use for their own not-yet-wired cases.

---

# 6. Persistence and repository contract

**Decision:** a new `IActiveEffectRepository` (`Odyssey.Application.Persistence`) and `SqliteActiveEffectRepository` (`Odyssey.Persistence.Sqlite`), not an extension of `IInventoryRepository` or `ICharacterRepository`.

Rules:

1. `ActiveEffect` is not Inventory-owned or Character-owned data (`ADR-027` §8.2 rule 2, restated, not reopened) — it therefore gets its own repository contract, the same way `IInventoryRepository` itself is its own contract distinct from `ICharacterRepository`, rather than a method bag bolted onto an unrelated aggregate's own repository.
2. Minimum contract shape (method existence only — exact signatures are the future implementation task's own decision, this ADR fixes only that these operations exist and what they mean): create an `ActiveEffect` from an application request; read one by id; list all `ActiveEffect`s for a given `TargetRef` (needed by every duration-expiry and stacking check); list all for a given `SourceRef` (needed when an item is unequipped/consumed/destroyed, per `ADR-027` §8.1 rule 2's analogous `CharacterAbility` cleanup requirement, now applied to effects); apply a stacking update (section 7); transition `Status` (expire/suspend/resume/remove); each CAS-guarded by `Revision`.
3. Cross-campaign boundary and CAS-guard conventions mirror `IInventoryRepository`'s own existing conventions (`CampaignHandle` first parameter, `CorrelationId` last, `Result<T>` return, `CommandId`-keyed idempotency) — no new persistence idiom is invented.

---

# 7. Stacking: `EffectStackPolicy` integration

**Decision:** each of the 7 existing `EffectStackPolicy` values maps to one specific behavior when a new application targets the same `TargetRef` with the same `EffectDefinitionRef` as an existing `Active` (or `Suspended`) `ActiveEffect`:

1. **`IndependentInstances`** — the new application always creates a new, separate `ActiveEffect` row. Existing rows are untouched. Multiple instances of the same effect on the same target coexist independently (each with its own `AppliedAt`/`ExpiresAt`/lifecycle).
2. **`RefreshDuration`** — no new row is created. The existing row's `AppliedAt`/`ExpiresAt` (and, for event/condition-driven durations, nothing besides `AppliedAt`, since those have no fixed `ExpiresAt` to refresh) are reset as if freshly applied now; `Revision` increments. `StackCount` is unaffected (stays at whatever it already was).
3. **`ReplaceIfStronger`** — the host does not decide "stronger" itself (no generic potency scalar exists on `EffectMechanicsSnapshot`, and none is invented by this ADR). `Odyssey.Rules` (already the deterministic-calculation owner per `ADR-027` §14) compares the new application's snapshot against the existing row's snapshot using Ruleset-defined logic over the opaque `MechanicsPayloadRef`-resolved payload, and returns a boolean the host then acts on: if the new one is stronger, behave as `ReplaceExisting` (below); otherwise, behave as `IgnoreNewApplication` (below).
4. **`ReplaceExisting`** — the existing row transitions to `Removed` (not physically deleted — `ADR-012`'s append-only-history discipline extends to this aggregate's own row lifecycle the same way it already does for every other campaign aggregate) and a new `ActiveEffect` row is created in the same transaction, atomically.
5. **`IncreaseStacks`** — no new row is created. The existing row's `StackCount` increments by 1, `Revision` increments, `AppliedAt` is unchanged (per-application timing is not separately tracked once stacked; the future implementation task may reconsider this only if a genuine Ruleset need appears). A per-`EffectDefinition` maximum stack count, if the content author wants one, is Ruleset-defined data inside the same opaque `MechanicsPayloadRef`-resolved payload `EffectMechanicsSnapshot` already carries — this ADR does not add a new strongly-typed `MaxStacks` field to the already-accepted `EffectDefinition` type (`ODY-S05-105`); a future implementation task may add one if the opaque-payload approach proves insufficient, but that is that task's own decision to make, not this ADR's.
6. **`IgnoreNewApplication`** — no new row is created, no existing row is mutated. The application command still succeeds (the *item use* succeeded; the *effect* was a no-op) and returns the existing `ActiveEffect` unchanged, mirroring `ADR-027` §10's own general "silent success on an already-applied idempotent action" convention rather than surfacing this as an error.
7. **`RequestGMResolution`** — the only genuinely nontrivial case. No `ActiveEffect` row is created or mutated immediately. Instead, the host creates a lightweight `ActiveEffectStackConflict` pending record (`TargetRef`, the new application's own `EffectDefinitionRef`/`EffectMechanicsSnapshot`/`SourceRef`, the conflicting existing `ActiveEffectId`, `RaisedAt`) and emits it for MainGM review — the future implementing task's own UI/notification concern, out of this ADR's scope beyond naming that the record must exist and be queryable. MainGM resolves it via an explicit `ResolveActiveEffectStackConflict` command choosing one of: `ApplyAsIndependentInstance` (behaves as rule 1), `Replace` (behaves as rule 4), or `Ignore` (behaves as rule 6, but the pending record is what gets discarded, not a live `ActiveEffect`). Until resolved, the *item use itself* still succeeds (the item was consumed/used); only the *effect's own* stacking decision is pending — this mirrors `ADR-027` §10's own "preview lists a pending decision, does not silently guess" discipline, reused here for a much smaller decision than a full migration preview.

---

# 8. Duration and expiry: `EffectDurationType` integration

**Decision:** every one of the 15 existing `EffectDurationType` values maps to an explicit expiry mechanism, an explicit "already solved" pointer, or an explicit "full attack pipeline block's own territory" boundary marker — none is left undecided by omission.

| `EffectDurationType` | Expiry mechanism | Owning block |
|---|---|---|
| `Instant` | **No `ActiveEffect` row is created at all.** An instant effect has no ongoing existence to track; its mechanical result resolves immediately at the point of application (an attack-roll/damage/utility resolution step) and there is nothing left to expire. This is a deliberate decision, not an oversight: persisting a row only to mark it `Expired` in the same transaction would add bookkeeping with no behavioral value. | This block (no persisted state) |
| `Permanent` | No automatic expiry check ever runs. Ends only via explicit `RemoveActiveEffect` (section 9). | This block |
| `UntilRemoved` | Identical runtime mechanism to `Permanent` — no automatic expiry check ever runs, ends only via explicit removal. The two values are a Ruleset/content-authoring distinction (whether the effect is framed as "permanent" vs. "lasts until someone removes it"), not two different runtime mechanisms; a future implementation task must not invent a behavioral difference between them that this ADR does not require. | This block |
| `WhileItemEquipped` | Already solved by `ADR-027` §8.2 rule 3: subscribes to `ItemEquipped`/`ItemUnequipped` events; unequip transitions `Suspended` (section 5.2 rule 3), not `Expired`; a terminal item destroy/consume while unequipped transitions `Removed`. | This block (already specified) |
| `UntilSceneChange` | Subscribes to an authoritative scene-change event (e.g. `SceneActivated`/equivalent) for the campaign, the same event-subscription *pattern* `WhileItemEquipped` already establishes — the specific event name/shape is the future implementing task's own decision; no such event is confirmed to exist yet by this ADR's own research and must be added or reused as that task finds appropriate. | This block |
| `UntilSessionEnd` | Subscribes to an authoritative session-end signal — no such signal is confirmed to exist yet; the future implementing task defines or reuses one, following the same event-subscription pattern. | This block |
| `WhileCondition` | Re-evaluated by `Odyssey.Rules` (deterministic calculation owner, `ADR-027` §14) whenever state the condition depends on changes, or at minimum on every explicit `CheckActiveEffectExpiry`-shaped host call the future implementing task defines — this ADR does not fix the exact trigger cadence, only that the evaluator is `Odyssey.Rules`, never a direct Character/item mutation, and that an inconclusive evaluation fails closed (section 11). | This block (mechanism), content-defined condition semantics are Ruleset data |
| `WhileSourceExists` | Subscribes to the source entity's own deletion/destruction/consumption event (`SourceRef`'s referenced `ItemInstance` consumed/destroyed, or the source Character removed) — reuses whichever such event already exists for that source kind (e.g. item consumption already has its own command/event surface) rather than inventing a parallel one. | This block |
| `ForDuration` | A host-clock-based expiry: `ExpiresAt` is computed at application time (`AppliedAt` + the Ruleset-defined duration read from `EffectMechanicsSnapshot`'s payload) and checked by a host-side sweep or on-demand check the future implementing task defines — no existing "ticking clock" mechanism was found by this ADR's own research (confirmed: no turn/round/game-clock type exists anywhere in the codebase today), so this is genuinely new infrastructure, scoped here only as "an `ExpiresAt` timestamp exists and something checks it," not a full scheduler design. | This block (wall-clock case only) |
| `ForRounds` | Requires a round-counter concept that does not exist anywhere in the codebase today (confirmed by direct search) — rounds are a combat-turn-structure concept. **Reserved for the full attack pipeline block.** This ADR only reserves the enum value's meaning; it specifies no mechanism. | **Full attack pipeline (Block 3)** |
| `ForTurns` | Same reasoning as `ForRounds` — turn-counting is a combat-turn-structure concept with no existing infrastructure. **Reserved for the full attack pipeline block.** | **Full attack pipeline (Block 3)** |
| `UntilSourceTurnStart` | Requires knowing whose turn it currently is — a combat-turn-order concept with no existing infrastructure. **Reserved for the full attack pipeline block.** | **Full attack pipeline (Block 3)** |
| `UntilSourceTurnEnd` | Same as `UntilSourceTurnStart`. **Reserved for the full attack pipeline block.** | **Full attack pipeline (Block 3)** |
| `UntilTargetTurnStart` | Same as `UntilSourceTurnStart`, keyed to the target's turn instead of the source's. **Reserved for the full attack pipeline block.** | **Full attack pipeline (Block 3)** |
| `UntilTargetTurnEnd` | Same as `UntilTargetTurnStart`. **Reserved for the full attack pipeline block.** | **Full attack pipeline (Block 3)** |

Six of the fifteen values (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`, `UntilSourceTurnEnd`, `UntilTargetTurnStart`, `UntilTargetTurnEnd`) are explicitly out of this ADR's own implementable scope, per section 13's own boundary — this table still specifies their *meaning* (so `EffectDefinition` authors and this ADR's own future implementer are not left guessing), but not their *mechanism*, since that mechanism does not exist until the full attack pipeline block builds it.

---

# 9. Explicit removal

**Decision:** an explicit `RemoveActiveEffect` command ends an effect before its own natural expiry, transitioning `Status → Removed` with a `Revision` CAS guard, the same pattern every other explicit lifecycle-ending command in this codebase already uses (e.g. `DeleteCharacterPermanently`'s own CAS-guarded transition). It never physically deletes the row — `ADR-012`'s append-only-history discipline applies to `ActiveEffect` the same way it already applies to every other campaign aggregate (Character rows stay queryable via history after `DeleteCharacterPermanently`, per `ADR-025` §5.2/§5.3; `ActiveEffect` rows stay queryable via `Status = Removed` after this command, by direct analogy).

`RemoveActiveEffect`'s own authorization is section 10's own decision (MainGM-only), not decided here separately.

---

# 10. Permissions

**Decision:** this ADR makes two different, explicitly justified permission choices for two different operations, using both of this project's own existing idiomatic patterns (`ADR-025` §5.1's "inherit the existing model" pattern, and `ADR-025` §5.2's "new MainGM-only restriction" pattern) — not one uniform rule applied everywhere.

1. **Creating an `ActiveEffect` through item use** (the ordinary, frequent, gameplay-routine path — drinking a potion, triggering a weapon's on-hit effect) is checked normally under the existing item-use permission model `ADR-027` §12 rule 4 already fixes ("Player item actions are command intents. Host validates permission, ownership/control, current inventory/equipment state..."); this ADR does not restrict it beyond what that rule already implies — the same "inherit, do not add a new restriction" pattern `ADR-025` §5.1 already used for `Character.Archive`. **Rationale:** this is a routine, frequent, player-triggered action already governed by an existing permission pathway (ownership/control of the item being used); requiring MainGM approval for every potion a player drinks would make ordinary play unworkable, and nothing about "the item's effect now persists as a tracked row instead of resolving instantly" changes who is authorized to use the item in the first place.
2. **Creating an `ActiveEffect` directly, not through any item** (a new capability this ADR's own aggregate makes possible for the first time — e.g. a MainGM narratively applying "poisoned" to a Character with no in-fiction item cause) **is MainGM-only**, the same "new, stricter-than-general restriction" pattern `ADR-025` §5.2 already used for `DeleteCharacterPermanently`. **Rationale:** no existing permission pathway authorizes "apply an arbitrary mechanical effect to any target" today; this is new authority this ADR introduces, and it is exactly the kind of narrative-override power this project already reserves to MainGM elsewhere (`ADR-027` §12 rule 1's own migration precedent).
3. **`RemoveActiveEffect` (explicit early removal) is MainGM-only**, for the same reason as (2): removing an effect before its own natural expiry is an override action with narrative weight (curing a curse, canceling a buff mid-fight) that no existing permission already authorizes for a non-GM actor.
4. AssistantGM's own scope for all three of the above is exactly whatever `ADR-019`'s existing role/permission model already grants AssistantGM for MainGM-reserved actions in general — this ADR does not carve out a special AssistantGM exception the way `ADR-027` §12 rule 2 deliberately did not for migration.
5. Ordinary players cannot create an `ActiveEffect` directly or remove one early, matching `ADR-027` §12 rule 3's own "ordinary players cannot... submit trusted final... state" principle, extended here from items to effects.

---

# 11. Fail-closed expiry and removal

**Decision:** an expiry or removal check that cannot be conclusively evaluated never silently ends the effect. This mirrors `ADR-025` §5.2's own fail-closed principle — a dependency check that cannot conclusively rule out a blocking dependency blocks the (destructive) delete — applied here to expiry instead of deletion, since for `ActiveEffect` the *destructive* direction is losing the effect's state, not keeping it.

Concretely: if a `WhileCondition` evaluation fails (missing referenced state, a `Rules`-layer error, an unavailable dependency), the effect remains `Active` — it is never marked `Expired` from an inconclusive check, and it is never left in an ambiguous intermediate state either. The same principle applies to any other duration mechanism's own expiry check: an inconclusive check is treated as "not yet expired," re-attempted on the next relevant trigger, never as "expired by default" or "removed defensively."

---

# 12. Atomicity

**Decision:** `SqliteActiveEffectRepository`'s every mutation (create, stack update, status transition, removal) routes through the same shared `SqliteSavingPipeline` — the `ADR-012` §5 single-transaction journal-projection commit pipeline already used by `SqliteCampaignRepository`, `SqliteCharacterRepository`, `SqliteGameLogRepository`, and `SqliteSceneRepository` (and, as of `ODY-S05-403`, `SqliteInventoryRepository`'s own migration-apply path) — not a new, sixth, one-off transaction/event mechanism. Every `ActiveEffect` mutation therefore commits its projection row, its `DomainEvents` audit row, and its idempotency record together, in one transaction, or none of the three lands.

---

# 13. Boundary with the full attack pipeline (Block 3)

**Decision:** this ADR specifies only the generic `ActiveEffect` mechanism and its item integration (`ADR-027` §8.1/§8.2) — never combat/damage-sourced effects, and never the turn/round-structure infrastructure six `EffectDurationType` values depend on.

Explicitly reserved for the full attack pipeline block (roadmap §14.6), not this ADR:

- The six turn/round-based `EffectDurationType` values (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`, `UntilSourceTurnEnd`, `UntilTargetTurnStart`, `UntilTargetTurnEnd`) — section 8's own table names these explicitly; their *meaning* is fixed here, their *mechanism* is not, because no turn/round-order infrastructure exists yet.
- Damage-over-time, on-hit, and other combat-roll-triggered effect applications themselves — this ADR specifies the `ActiveEffect` aggregate any effect (combat-sourced or not) is stored in, but not how a combat roll decides to apply one.
- Any interaction between `ActiveEffect` stacking/duration and to-hit/damage-roll modifiers — a `Rules`-layer calculation concern the full attack pipeline block owns, not this ADR.

This ADR's own scope is fully implementable — items applying non-combat effects (buffs, debuffs, status conditions with event/condition/wall-clock-based expiry, `WhileItemEquipped` effects) — before the full attack pipeline block begins.

---

# 14. Non-goals

Explicitly out of scope:

- amending `ADR-027` itself, including its own `ADR-024`/`CharacterAbility` citation inaccuracy (section 4) — that is a separate, explicit future decision if the product owner ever wants it corrected, not an implicit side effect of this ADR;
- the full attack pipeline and its own turn/round infrastructure (section 13);
- decomposing `ODY-S05-502` onward into implementation task IDs — that is a future backlog revision, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1's own explicit deferral, not this ADR's job;
- product code, tests, persistence schema, migrations, DTO files, or concrete command handlers;
- a full UI for `ActiveEffectStackConflict` resolution — only that the record and resolution command must exist;
- balancing concrete `EffectDefinition` content or `MaxStacks`/potency payload schema — Ruleset/content-authoring concerns, not this ADR's.

---

# 15. Соответствие module boundaries (`ADR-001`) and existing ADRs

Future implementation must preserve `ADR-027` §14's own module assignments, extended here only where `ActiveEffect` needs a home:

- `Odyssey.Domain` owns the pure `ActiveEffect`/`SourceRef`/`TargetRef` identity/value invariants (section 5) — serializer-free, Unity-free, consistent with every other Domain aggregate in this codebase.
- `Odyssey.Rules` owns the `ReplaceIfStronger` comparison (section 7 rule 3) and `WhileCondition` evaluation (section 8) — deterministic calculation, no state commit, consistent with `ADR-027` §14's own Rules-layer assignment.
- `Odyssey.Application` owns `ActiveEffect` creation/stacking/expiry/removal orchestration and permission checks (sections 6-11) — `ADR-027` §14 already explicitly names "ActiveEffect creation orchestration" as an `Odyssey.Application` responsibility; this ADR is that assignment's own detailed specification.
- `Odyssey.Persistence` owns `SqliteActiveEffectRepository`'s physical table(s)/indexes — it does not decide stacking/expiry legality, only stores what `Odyssey.Application` decides, consistent with `ADR-027` §14's own Persistence-layer assignment.
- `Odyssey.Networking`/`Odyssey.Unity.Client` are unaffected beyond `ADR-027` §14's own existing redacted-projection/display-only assignments, extended to cover `ActiveEffect` projections the same way they already cover inventory/equipment ones.

Relationship to existing ADRs, extending `ADR-027` §14's own list:

- `ADR-001` governs module dependency direction (unchanged).
- `ADR-002` governs commands/idempotency/root-command boundaries — `ActiveEffect` commands (`RemoveActiveEffect`, `ResolveActiveEffectStackConflict`) follow it without modification.
- `ADR-003` governs explicit versioned DTOs — `EffectMechanicsSnapshot`'s own wire/storage shape, when a future task implements it, is a new explicit contract, not a direct Domain serialization.
- `ADR-012` governs the single-transaction commit pipeline this ADR requires `SqliteActiveEffectRepository` to reuse (section 12), and the append-only-history discipline `RemoveActiveEffect`/expiry transitions must respect (section 9).
- `ADR-019` governs the baseline permission/redaction model section 10's "inherit existing model" rulings defer to.
- `ADR-024` is cited only for what it actually governs — development economy/progression transactions — never for `CharacterAbility` semantics (section 4's citation-correctness note); this ADR does not otherwise depend on it.
- `ADR-025` supplies both permission-phrasing precedents section 10 explicitly chooses between, and the fail-closed precedent section 11 reuses.
- `ADR-027` is this ADR's own parent; §8.1/§8.2/§11/§12 are extended, not reopened (section 4).

---

# 16. Rules for Codex

Codex must, when a future implementation task activates this ADR:

1. Model `ActiveEffect` as its own aggregate root, never an Inventory or Character section (section 5, section 6 rule 1).
2. Give it its own `IActiveEffectRepository`/`SqliteActiveEffectRepository`, never bolt its methods onto `IInventoryRepository`/`ICharacterRepository` (section 6).
3. Implement every one of the 7 `EffectStackPolicy` behaviors exactly as section 7 specifies, including the `ActiveEffectStackConflict` pending record for `RequestGMResolution` — not a simplified subset.
4. Implement or explicitly defer every one of the 15 `EffectDurationType` values exactly as section 8's table specifies — never silently ignore a value, and never implement a turn/round-based value before the full attack pipeline block supplies real turn/round infrastructure.
5. Implement `RemoveActiveEffect` as a CAS-guarded, `Removed`-transitioning (never physically deleting) command (section 9).
6. Gate item-triggered `ActiveEffect` creation under the existing item-use permission model, and gate direct creation plus explicit removal as MainGM-only, exactly as section 10 specifies — never uniformly one or the other.
7. Fail closed on any inconclusive expiry/removal check (section 11) — never expire or remove defensively.
8. Route every `ActiveEffect` mutation through the shared `SqliteSavingPipeline` (section 12) — never a new one-off transaction mechanism.
9. Never implement combat-roll-triggered effect application or turn/round infrastructure under this ADR's own authority (section 13) — that is the full attack pipeline block's job.
10. Never amend `ADR-027` itself under this ADR's own authority, including its `ADR-024` citation (section 4/14) — that is a separate, explicit decision if ever made.
11. Never decompose `ODY-S05-502` onward under this ADR's own authority — that is a future backlog revision (section 14).

---

# 17. Definition of Done for future implementation tasks

Implementation tasks using this ADR must prove, with tests where applicable:

1. Applying a non-`Instant` effect creates one `ActiveEffect` row (or updates an existing one, per its `EffectStackPolicy`) with a snapshot captured at application time; publishing a new `EffectDefinition` version afterward does not change it (`ADR-027` §11, re-proven here for the concrete aggregate).
2. Each of the 7 `EffectStackPolicy` values produces exactly the behavior section 7 specifies when a second application targets the same `TargetRef`+`EffectDefinitionRef`.
3. `WhileItemEquipped` effects transition `Suspended` on unequip and resume `Active` on re-equip before any terminal transition, per section 5.2 rule 3 and `ADR-027` §8.2 rule 3.
4. At least the non-combat duration types this ADR fully specifies (`Permanent`, `UntilRemoved`, `ForDuration`, `WhileCondition`, `WhileSourceExists`, plus `UntilSceneChange`/`UntilSessionEnd` if their trigger events already exist by the time of implementation) actually expire through their own named mechanism, not merely via `RemoveActiveEffect`.
5. `RemoveActiveEffect` transitions `Status → Removed` without physically deleting the row, and a non-MainGM attempt is rejected with no state change.
6. Direct (non-item-sourced) `ActiveEffect` creation is rejected for a non-MainGM actor with no state change.
7. Item-triggered `ActiveEffect` creation succeeds under the same permission check an equivalent item-use command already passes, with no additional MainGM gate.
8. An inconclusive `WhileCondition` (or other) expiry check leaves the effect `Active`, never `Expired`, on that inconclusive check.
9. Every `ActiveEffect` mutation commits its projection, `DomainEvents` row, and idempotency record together in one transaction (via `SqliteSavingPipeline`), verified by the same kill/rollback-style test precedent already used for other repositories.
10. No turn/round-based `EffectDurationType` value's expiry mechanism is implemented before the full attack pipeline block exists.
11. Core `ActiveEffect` logic compiles without Unity dependencies in the pure .NET path.

---

# 18. Рассмотренные альтернативы

## 18.1 Extend `IInventoryRepository` with `ActiveEffect` methods instead of a new repository

**Considered:** since `ActiveEffect` is created by item use, add its persistence methods to the already-existing `IInventoryRepository`. **Rejected** because `ADR-027` §8.2 rule 2 explicitly states `ActiveEffect` is not Inventory-owned, and an `ActiveEffect` can be sourced by an action or applied directly by MainGM with no item involved at all (section 5.3) — coupling its persistence to Inventory's own repository would misrepresent its ownership and force every non-item-sourced effect through an Inventory-shaped contract that does not fit it.

**Accepted:** a standalone `IActiveEffectRepository` (section 6).

## 18.2 One uniform permission rule for all `ActiveEffect` operations

**Considered:** either make all `ActiveEffect` operations MainGM-only (matching migration's own strictness), or make all of them inherit the existing item-use model (matching how routine item use already works). **Rejected** both as too coarse: uniform MainGM-only would make ordinary item-triggered effects (a potion, a weapon's on-hit poison) require MainGM intervention for every single use, unworkable for live play; uniform inheritance would let any player narratively inject an arbitrary effect onto any target with no item cause, a new unauthorized capability this ADR does not intend to grant.

**Accepted:** two different rules for two different operations (section 10), using both of this project's own existing idiomatic permission-phrasing patterns rather than inventing a third.

## 18.3 A generic numeric "potency" field for `ReplaceIfStronger`

**Considered:** add a strongly-typed `Potency` numeric field to `EffectMechanicsSnapshot`/`EffectDefinition` so the host itself can compare "stronger" generically. **Rejected** because no such generic cross-Ruleset potency scale exists anywhere in this codebase's already-accepted content model, and inventing one here would silently decide Ruleset-specific game-design semantics this ADR has no authority over.

**Accepted:** defer the comparison to `Odyssey.Rules` over the opaque payload (section 7 rule 3), consistent with `ADR-027` §14's own assignment of deterministic calculation to that module.

## 18.4 Physically delete `ActiveEffect` rows on expiry/removal

**Considered:** since an expired/removed effect has no further mechanical relevance, physically delete its row rather than keeping a `Status = Expired`/`Removed` history row. **Rejected** because `ADR-012`'s append-only-history discipline applies uniformly across this codebase's aggregates (Character rows survive `DeleteCharacterPermanently` as history, per `ADR-025` §5.2/§5.3) and a GM/player may reasonably want to see "what effects were previously active" in history/GameLog rendering.

**Accepted:** `Status`-transition lifecycle, never physical deletion (section 9).

---

# 19. Открытые вопросы

No open questions for this ADR's own scope (the `ActiveEffect` aggregate, its item integration, stacking, non-combat duration expiry, removal, and permissions).

Deferred but not open here (explicitly reserved for the full attack pipeline block or a future backlog revision, not silently unresolved):

- turn/round-based `EffectDurationType` mechanism (`ForRounds`/`ForTurns`/`UntilSourceTurnStart`/`UntilSourceTurnEnd`/`UntilTargetTurnStart`/`UntilTargetTurnEnd`) — section 13;
- combat-roll-triggered effect application itself — section 13;
- concrete persistence schema, migrations, and command handler implementation — a future implementation task's own job, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1;
- the exact scene-change/session-end event names/shapes `UntilSceneChange`/`UntilSessionEnd` subscribe to, if they do not already exist by the time of implementation — section 8;
- a full UI for `ActiveEffectStackConflict` resolution;
- correcting `ADR-027`'s own `ADR-024`/`CharacterAbility` citation inaccuracy (section 4) — a separate, explicit future decision, not this ADR's to make.

---

# 20. Трассировка

ADR реализует и уточняет:

- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §8.1/§8.2 (item-sourced abilities/effects integration rules, extended not reopened), §11 (`ActiveEffect` never mass-migrates, reused as-is), §12 (permissions baseline, extended for `ActiveEffect`-specific operations), §14 (module boundaries, extended to name `ActiveEffect`'s own home in each module).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14/§14.1 (`ODY-S05-110`'s own decomposition, which reserved this ADR task and fixed its own scope boundary).
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14.6 (full attack pipeline — the sibling block this ADR's own section 13 draws a boundary against).

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency vocabulary;
- `ADR-003` for explicit serialized contracts;
- `ADR-012` for the single-transaction commit pipeline and append-only-history discipline;
- `ADR-019` for the baseline permission/redaction model item-triggered `ActiveEffect` creation inherits;
- `ADR-024` for development economy/progression transactions (cited correctly, not for `CharacterAbility`, per section 4);
- `ADR-025` for both permission-phrasing precedents (§5.1/§5.2) and the fail-closed precedent (§5.2) this ADR reuses.

---

# 21. Нормативное действие

**This ADR is `Accepted`.** It is the direct result of `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14's own decomposition (`ODY-S05-110`), which reserved exactly this ADR-specification task (`ODY-S05-501`) and explicitly forbade decomposing the block's remaining implementation tasks until this ADR is accepted.

Now that this ADR is `Accepted`:

- a future backlog revision may decompose `ODY-S05-502` onward into concrete implementation tasks (aggregate/persistence foundation, stacking, duration/expiry for the non-combat cases this ADR fully specifies, removal, permission gates, integration fixtures) — mirroring the two-tier→multi-task pattern every other block in `SLICE-05_IMPLEMENTATION_BACKLOG.md` already used once its own governing ADR existed;
- those future tasks must implement every decision in sections 5-13 as fixed, not renegotiate them inline;
- the six turn/round-based `EffectDurationType` values and combat-roll-triggered effect application remain unimplementable until the full attack pipeline block (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §8's own remaining reserved block) supplies turn/round infrastructure;
- changing any decision in this ADR requires an amendment or superseding ADR, not silent implementation drift, the same discipline `ADR-027` §20 already established for itself.

---

**Конец документа**
