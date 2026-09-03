# ADR-027 - Content Catalog & Item/Equipment System

**Документ:** `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`
**ADR:** ADR-027
**Версия:** 1.0
**Дата:** 2026-09-03
**Статус:** Accepted
**Область:** Content Catalog/ContentDefinition vs runtime item/equipment/effect instances, Inventory aggregate boundary, item/equipment ownership and location invariants, item-sourced abilities/effects, ItemDefinition migration preview/confirm, and SLICE-04 item-dependency stub closure for `SLICE-05`
**Связанные этапы:** Roadmap Stage 6 (`SLICE-05`), backlog `ODY-S05-001`
**Базовые документы:** `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md`; `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14 and section 16.9; `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` sections 16-18; `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`; `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`; `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`; `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`; `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`; `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`; `docs/adr/ADR-013_Migration_Runner_v1.0.md`; `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`; `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`; `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`; `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`; `docs/adr/ADR-026_Character_Export_Import_File_Format_And_Redaction_v1.0.md`; `docs/tasks/SLICE-05_BACKLOG.md`

---

# 1. Решение

Odyssey VTT fixes the `SLICE-05` boundary between versioned content definitions and runtime item/equipment/effect state before any item, inventory, equipment, or full-attack implementation begins.

Обязательные решения:

1. **Content Catalog vs runtime instances:** Content Catalog stores versioned `ContentDefinition` records only. `ItemDefinition`, typed item definitions (`WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`), `AbilityDefinition`, and `EffectDefinition` are catalog definitions. Runtime `Inventory`, `ItemInstance`, `ItemStack`, equipment state, `CharacterAbility`, and `ActiveEffect` are authoritative runtime state, not content definitions.
2. **SLICE-05 catalog scope:** the catalog scope for this slice includes `ItemDefinition` plus Weapon/Armor/Ammo typed item definitions, `AbilityDefinition`, `EffectDefinition`, and references to `Resource`/`BodyPart` structural definitions where item mechanics require them. Concrete balanced MVP catalog entries are explicitly out of scope.
3. **ContentDefinition archive/delete lifecycle:** unused Draft definitions may be physically deleted. Published or referenced definitions are archived, not physically deleted. Archived definitions remain loadable for existing `ItemInstance`, `ItemStack`, `ActiveEffect`, history, previews, and migrations. Physical deletion is allowed only when no catalog dependency and no runtime reference exists, reusing `11_Content_Block_System` lifecycle rules.
4. **Item snapshots:** every `ItemInstance` stores a full mechanics snapshot copied from the exact published ItemDefinition version used to create or migrate it. Publishing a new ItemDefinition version never changes existing instances or stacks by itself.
5. **Stacks:** `ItemStack` may use one shared mechanics snapshot only when every unit in the stack is mechanically identical: same definition/version snapshot, same stackable runtime state, and no per-unit durability, charge, ammo, effect, equipment, hidden modifier, or other state that can affect mechanics independently. Otherwise the items are represented as distinct `ItemInstance` records or split stacks.
6. **Inventory aggregate boundary:** Inventory is a separate campaign aggregate root, not a section inside `Character`. Character keeps only derived/read references needed for Character projection; Inventory owns item/stack containment, transfer, equipment location, and inventory revision.
7. **Equipment:** equipment is inventory-owned location state referencing equipment slots and/or body parts on the owning Character. One item or stack is in exactly one place: contained, equipped, scene-dropped, consumed/destroyed, or otherwise in one explicit lifecycle/location state. Equipping requires ownership/location validity and cannot duplicate the item into Character state.
8. **Item-sourced abilities/effects:** equipping or using an item may create or remove `CharacterAbility` rows with existing `SourceKind=Item` and source item reference. Applying an item effect creates a future `ActiveEffect` aggregate whose source is the item/equipment/action and whose mechanics snapshot is copied at application time. Character, Inventory, and ItemInstance store references/projections only; they do not own authoritative ActiveEffect state.
9. **SLICE-04 stubs:** this ADR explicitly unblocks the documented item/inventory dependency check stub in `RemoveBodyPart` and the inventory/item dependency checker extension point for `DeleteCharacterPermanently`.
10. **ItemDefinition migration:** migration from one ItemDefinition version to another is MainGM-only, preview/confirm based, backup-backed, revision-guarded, and blocked on incompatibilities until the definition or migration rules are fixed. Successful migration updates matching `ItemInstance` and mechanically identical `ItemStack` snapshots atomically while preserving runtime state. After successful migration there is no rollback command; correction is a later definition version plus another confirmed migration.
11. **ActiveEffect snapshot difference:** existing `ActiveEffect` aggregates never mass-migrate to a new `EffectDefinition`. They retain the mechanics snapshot captured at application until expiry, removal, compensation, or replacement. This intentionally differs from ItemDefinition migration.
12. **Permissions baseline:** MainGM-only for ItemDefinition publish and mass migration. AssistantGM may publish only where existing Content permissions explicitly allow; AssistantGM may not run ItemDefinition mass migration in this ADR. Ordinary players cannot mutate authoritative inventory offline or submit trusted final item/equipment/damage state.
13. This ADR does **not** implement product code, schema, DTOs, Unity UI, real item/equipment commands, full attack pipeline, full Content Editor UI, marketplace, arbitrary scripts, or concrete balanced catalog entries.

This ADR is the normative authority for the `SLICE-05` content-catalog/item/equipment boundary. It is `Accepted`; see section 20 for the recorded product-owner approval and its normative consequences for implementation tasks.

---

# 2. Контекст и проблема

`SLICE-04` delivered Character, abilities, resources, anatomy, export/import, lifecycle, and ruleset migration using minimal fixtures and documented item/inventory integration as future work. `SLICE-05` is the first roadmap stage where inventory, item definitions, item instances, equipment, effects, ammo, armor, and full attack are primary mechanics.

Existing authorities provide most vocabulary:

- `11_Content_Block_System` defines `ContentDefinition`, mechanical definition types (`Item`, `Weapon`, `Armor`, `Ammo`, `Ability`, `Effect`), structural `BodyPart`/`Resource` definitions, entry points, effect duration, item/effect conditions, and Content permissions.
- Roadmap section 14 names Inventory, `ItemStack`, `ItemInstance`, equipment, ammo/weapon/armor state, definition snapshots, active-effect snapshots, and the full attack vertical slice.
- Domain Model sections 16-18 define Content, Inventory/Item, ItemDefinition migration, and ActiveEffect concepts.
- `ADR-022`, `ADR-024`, and `ADR-025` define Character section revisions, existing `CharacterAbility` source handling, development economy, lifecycle, and ruleset migration patterns that item/equipment work must reuse without reopening.

The missing prerequisite decisions are:

1. whether Content Catalog owns runtime item/equipment state or only definitions;
2. which definitions are in the first SLICE-05 catalog;
3. where Inventory's aggregate boundary sits;
4. how snapshots, stacks, equipment, and item-sourced ability/effect references work;
5. how ItemDefinition migration preview/confirm differs from ActiveEffect snapshot immutability;
6. how the SLICE-04 item dependency stubs are closed.

---

# 3. Термины

## 3.1 Content Catalog

The campaign/ruleset catalog of versioned, published or draft `ContentDefinition` records. It owns definitions and dependency metadata, not runtime campaign state created from those definitions.

## 3.2 ItemDefinition and typed item definitions

`ItemDefinition` is the common content definition for item mechanics. `WeaponDefinition`, `ArmorDefinition`, and `AmmoDefinition` are typed item definitions represented as `ContentDefinition.DefinitionType` plus typed properties/entry points, not independent runtime aggregate kinds.

## 3.3 Runtime item state

`ItemInstance`, `ItemStack`, Inventory containment, equipment location, durability, charges, loaded ammo, armor damage, and active item effects are runtime state. They are mutated only through authoritative commands and persisted as campaign state, never by editing a `ContentDefinition`.

## 3.4 Mechanics snapshot

An immutable copy of definition mechanics needed to use an item or effect without consulting the latest catalog definition. Snapshots are smaller than a full content package but complete enough for deterministic runtime behavior.

---

# 4. Content Catalog and runtime boundary

**Decision:** Content Catalog owns definitions only. Runtime instances are separate authoritative state.

Catalog definitions for `SLICE-05`:

```text
ContentDefinition
├── ItemDefinition
│   ├── WeaponDefinition
│   ├── ArmorDefinition
│   └── AmmoDefinition
├── AbilityDefinition
├── EffectDefinition
├── Resource references
└── BodyPart references
```

Rules:

1. `ItemDefinition`, `AbilityDefinition`, and `EffectDefinition` use the Content Block System lifecycle: Draft, Published, Archived; published versions are immutable.
2. Runtime entities reference exact definition versions for origin, UI, audit, dependency analysis, and migration.
3. Runtime mechanics are read from snapshots after creation/application, not from mutable latest definitions.
4. `Resource` and `BodyPart` remain structural definition references where needed by item costs, armor coverage, effects, damage, or equipment requirements; this ADR does not create balanced concrete Resource/BodyPart catalogs.
5. ContentDependency records track definition-to-definition dependencies; runtime dependency checks additionally inspect ItemInstances, ItemStacks, Inventory, equipment, and ActiveEffects.

This keeps `ADR-003`'s explicit-contract rule intact: catalog DTOs and runtime DTOs are separate, versioned contracts. Domain aggregates are not serialized directly.

## 4.1 ContentDefinition archive and physical deletion lifecycle

**Decision:** definition archive/delete behavior reuses `11_Content_Block_System` section 6 lifecycle rules. This ADR applies those rules explicitly to the SLICE-05 catalog definitions: `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, and `EffectDefinition`.

Rules:

1. An unused Draft definition may be physically deleted.
2. A Published definition, or any definition referenced by another catalog definition, runtime entity, event history, preview, report, or migration artifact, is archived rather than physically deleted.
3. Archived definitions remain loadable for existing `ItemInstance` snapshots, shared `ItemStack` snapshots, `ActiveEffect` snapshots, history rendering, dependency previews, ItemDefinition migration preview/confirm, and compatibility/migration tools.
4. Physical deletion of a definition is allowed only when both checks pass: no catalog dependency exists, and no runtime reference exists.
5. Runtime references include at minimum `ItemInstance`, `ItemStack`, Inventory/equipment state, `CharacterAbility SourceKind=Item`, `ActiveEffect`, history/projection payloads that need the definition for rendering, saved previews, and migration reports.
6. Archive/delete lifecycle rules in this section do not implement code, schema, migrations, commands, or UI; they are prerequisite architecture only.

---

# 5. Inventory aggregate boundary

**Decision:** Inventory is a separate campaign aggregate root for `SLICE-05`, not a Character section.

Domain Model section 17.1 allowed either implementation. This ADR chooses a separate root because `SLICE-05` operations span more than one Character section and more than one owner/location type:

- a Character can own or equip items;
- an item can be dropped into a Scene location;
- transfer crosses two inventories or inventory plus scene location;
- attack consumes ammo/effects and may update Character resources, ItemInstance state, ActiveEffect roots, GameLog, and DomainEvents in one authoritative transaction;
- `DeleteCharacterPermanently` and `RemoveBodyPart` need item/inventory dependency checks without loading item state as Character-owned data.

Conceptual aggregate:

```text
Inventory
├── InventoryId
├── CampaignId
├── OwnerEntityRef
├── StackEntries
├── UniqueItemIds
├── EquippedEntries
├── Revision
└── LocationIndex
```

Rules:

1. Inventory owns containment, stack membership, equipment location, and transfer revision.
2. `ItemInstance` remains its own aggregate root for unique items with per-item runtime state, consistent with Domain Model section 17.3.
3. `ItemStack` is inventory-owned stack state for stackable items; it is not a Character section.
4. Character projections may include derived inventory/equipment summaries for display or effective-stat computation, but Character does not own authoritative item location.
5. Multi-aggregate commands use one root command and one `ADR-012` transaction where state must change atomically; command handlers must not call other command handlers (`ADR-002`).

---

# 6. ItemInstance and ItemStack snapshots

## 6.1 ItemInstance

Every `ItemInstance` stores:

```text
ItemInstance
├── ItemInstanceId
├── CampaignId
├── SourceItemDefinitionRef
├── DefinitionMechanicsSnapshot
├── DefinitionSnapshotVersion
├── RuntimeState
├── OwnerOrLocationRef
├── ActiveEffectRefs
└── Revision
```

`DefinitionMechanicsSnapshot` includes all mechanics needed for item use, including item type/category, weapon/armor/ammo properties, damage/range/modes, body-part coverage, resource and action costs, capacity/reload rules, built-in abilities, created effects, requirements, ContentBlocks, consumption/repair/destroy rules, and hidden mechanics needed by the host.

Publishing a new ItemDefinition version does not alter existing instances. Existing instances change their snapshot only through a confirmed ItemDefinition migration.

Runtime state is separate from the mechanics snapshot. Migration must not restore spent durability, charges, loaded ammo, consumed quantity, damage, or other runtime state.

## 6.2 ItemStack

`ItemStack` may share one mechanics snapshot only when all units are mechanically identical.

Mechanically identical means:

- same `ItemDefinitionId`, published definition version, and `DefinitionSnapshotVersion`;
- same stackable item type and quantity rules;
- no per-unit durability, charge, loaded ammo, active effect, equipment location, hidden modifier, or custom runtime state that can change mechanics for only one unit;
- same visible/hidden mechanics for all units in the stack.

If any unit diverges mechanically, the stack is split before the change or represented as unique `ItemInstance` state. Negative quantity is forbidden. Quantity reaches zero only through a command that consumes/destroys/removes the stack and records the corresponding event.

---

# 7. Equipment model

**Decision:** equipment is Inventory-owned location state over exact item/stack references, equipment slot references, and optional body-part references.

Minimum equipment record:

```text
EquippedEntry
├── InventoryId
├── ItemRef
├── EquipmentSlotRef
├── BodyPartRefs[]
├── EquippedByUserId
├── EquippedAt
└── Revision
```

Rules:

1. One item or stack is in exactly one place: contained in one Inventory, equipped in one EquipmentSlot/body-part placement, dropped at one Scene location, consumed, destroyed, or otherwise in one explicit lifecycle/location state.
2. Equipping moves an item from contained state to equipped state; it does not copy item mechanics into Character as authoritative state.
3. Unequipping moves it back into a valid containment location or another explicit location selected by the command.
4. Equipment must reference body parts that currently exist on the owning Character when body-part-specific placement or armor coverage is required.
5. Removing a body part is rejected while equipment or item state depends on that body part, unless the same future command explicitly and atomically resolves the dependency under an accepted task contract.
6. Weapon, armor, and ammo runtime state stay with the item/stack: loaded ammo, chamber/fire mode/jammed state for weapons; covered body parts, protection state, durability/broken state for armor.

---

# 8. Item-sourced abilities and effects

## 8.1 CharacterAbility integration

`SLICE-04` already introduced `CharacterAbility` with `SourceKind=Item` and `SourceKind=ActiveEffect`. This ADR does not add a parallel ability system.

Rules:

1. Equipping or using an item may create or activate `CharacterAbility` entries with `SourceKind=Item` and a source item reference.
2. Unequipping, consuming, destroying, transferring away, or migrating an item must remove, suppress, or revalidate only those `CharacterAbility` entries whose source is that item, while permanent progression-purchased abilities remain unaffected.
3. The item remains the source of the ability. Character stores the ability instance/reference needed by Character mechanics and projections, not the item's whole mechanics snapshot.

## 8.2 ActiveEffect integration

Applying an item effect creates an `ActiveEffect` aggregate using `EffectDefinitionRef` and `EffectMechanicsSnapshot` captured at application. The ActiveEffect source references the item/equipment/action that created it; the target references the affected Character, item, scene object, or other supported entity.

Rules:

1. Character, ItemInstance, and SceneObject store only `ActiveEffect` references or derived projections.
2. Existing ActiveEffects are not owned by Inventory or Character, and do not mass-migrate when their source `EffectDefinition` changes.
3. Effects with duration `WhileItemEquipped` subscribe to authoritative `ItemEquipped`/`ItemUnequipped` events; they expire, suppress, or remove through ActiveEffect lifecycle commands/events, not by directly mutating Character or item snapshots.

---

# 9. Closure of SLICE-04 documented stubs

This ADR unblocks exactly these documented `SLICE-04` stubs:

1. `RemoveBodyPart` item dependency check: future implementation must check Inventory/equipment/ItemInstance state for equipped armor, worn gear, implants, item-granted modifications, item effects, or other item references to the body part before allowing removal. A removal command must reject while such dependencies exist, unless the same accepted command contract atomically resolves them.
2. `DeleteCharacterPermanently` inventory/item dependency checker: future implementation must provide real `ICharacterDeletionDependencyChecker`-style checks for Inventory ownership, equipped items, item effects, scene-dropped items still owned by the Character, GameLog/history-visible item references where relevant, and ActiveEffects sourced from or targeting the Character. Physical delete must remain host-revalidated and must not trust a client preview, per `ADR-025`.

These checks are dependency checks, not new Character ownership rules. They reuse the Inventory aggregate, ItemInstance/ItemStack location index, equipment state, ActiveEffect references, and append-only event/history constraints.

---

# 10. ItemDefinition migration preview/confirm

**Decision:** ItemDefinition migration is a MainGM-only preview/confirm workflow over runtime item snapshots. It is not automatic publication side effect and not database schema migration.

Workflow:

1. A new ItemDefinition version is published or selected as the migration target.
2. MainGM builds or refreshes `ItemDefinitionMigrationPreview`.
3. The system creates the required backup before migration review using `ADR-012`'s existing snapshot/`BackupRecord` mechanism.
4. Preview lists affected `ItemInstance` and mechanically identical `ItemStack` records, before/after snapshot changes, runtime-state compatibility checks, blocking issues, and required migration rules.
5. Confirmation requires current `SourceDefinitionRevision`, `AffectedInventoryRevision`, and `PreviewRevision`.
6. If revisions changed, host refreshes preview and requires new confirmation before starting the transaction.
7. If blocking incompatibilities remain, migration does not start.
8. Confirmed migration updates all matching item/stack snapshots in one `ADR-012` transaction and emits the required events/audit/report.
9. After successful migration there is no rollback command. A later correction is a new ItemDefinition version and another confirmed migration.

Blocking incompatibilities include at minimum: removed ammo type currently loaded, removed equipment slot currently occupied, reduced capacity below current content, removed armor/body-part coverage with runtime damage, custom state the new definition cannot interpret, or hidden mechanics that cannot be safely compared.

This workflow changes item mechanics snapshots only. It preserves runtime state and does not rewrite DomainEvents.

---

# 11. ActiveEffect migration difference

Existing `ActiveEffect` aggregates intentionally differ from item instances:

- applying an effect captures the full `EffectMechanicsSnapshot`;
- publishing a new `EffectDefinition` applies only to future ActiveEffects;
- existing ActiveEffects never mass-migrate to the new definition;
- manual and mass migration of already-active effects is not supported by this ADR.

Reason: ActiveEffects may be mid-duration, mid-combat, tied to turn timing, source/target state, stacking, and pending event subscriptions. Rewriting them in bulk would unpredictably change current combat/scene state. Items, by contrast, may be migrated only through explicit MainGM preview/confirm with blocked incompatibilities and runtime-state preservation.

---

# 12. Permissions baseline

Rules:

1. MainGM can publish ItemDefinition versions and run ItemDefinition migration preview/confirm.
2. AssistantGM can publish only where existing Content permissions explicitly allow `Content.Publish` in scope. This ADR does not create a new AssistantGM role rule and does not allow AssistantGM to run mass ItemDefinition migration.
3. Ordinary players cannot publish definitions, run mass migration, mutate authoritative inventory offline, or submit trusted final damage/equipment/ammo state.
4. Player item actions are command intents. Host validates permission, ownership/control, current inventory/equipment state, costs, range/attack prerequisites where applicable, and revisions before commit.
5. Migration previews, comparisons, notifications, and open item card refreshes are permission-filtered and must not expose hidden GM fields, unknown item properties, internal ContentBlocks, migration rules, technical revisions, or other characters' inaccessible data.

---

# 13. Non-goals

Explicitly out of scope:

- full attack pipeline;
- full Content Editor UI;
- marketplace or package distribution;
- arbitrary scripts or runtime code execution inside content;
- balancing concrete MVP catalog entries;
- Unity UI;
- product code, tests, persistence schema, migrations, DTO files, or concrete command handlers;
- real `TransferItem`, `EquipItem`, `ConsumeItem`, `DropItem`, `PickUpItem`, `ReloadWeapon`, `RepairArmor`, `ApplyEffect`, or attack-resolution command implementation;
- changing accepted Character, progression, permissions, persistence, or migration ADRs.

---

# 14. Соответствие module boundaries (`ADR-001`) and existing ADRs

Future implementation must preserve:

- `Odyssey.Domain` owns pure identity/value invariants for `Inventory`, `ItemInstance`, `ItemStack`, equipment placement, snapshot identity, and one-place item rules. It remains serializer-free and Unity-free.
- `Odyssey.Rules` owns deterministic item/equipment/ability/effect calculations and attack availability calculations. It does not commit state.
- `Odyssey.Content` owns ContentDefinition contracts, dependency validation, compiled content graph metadata, and content execution definitions.
- `Odyssey.Application` owns item/inventory/equipment commands, permission checks, dependency checks, preview/confirm orchestration, ActiveEffect creation orchestration, and transaction boundaries.
- `Odyssey.Persistence` owns physical tables/indexes for inventories, item instances, stacks, equipment, migration previews/reports, and dependency queries. It does not decide command legality.
- `Odyssey.Networking` owns redacted delivery of inventory/equipment/item/effect projections. It never receives raw unrestricted DomainEvents as client payloads.
- `Odyssey.Unity.Client` owns item sheets, inventory UI, equipment UI, previews, and display only; it does not store authoritative inventory state.

Relationship to existing ADRs:

- `ADR-001` governs module dependency direction.
- `ADR-002` governs commands, idempotency, root command boundaries, and event batches.
- `ADR-003` governs explicit versioned DTOs, no direct Domain serialization, canonical JSON where required, and upcasters.
- `ADR-007` governs version identity discipline; definition version changes must not be confused with app/schema versions.
- `ADR-011` governs local campaign format/version dimensions; this ADR does not add schema.
- `ADR-012` governs append-only journal, transaction atomicity, compensation, snapshots, and backup records.
- `ADR-013` governs database schema migration; ItemDefinition migration is a domain/content workflow, not the schema migration runner.
- `ADR-019` governs baseline permissions and redaction principles.
- `ADR-022` governs Character sections/history; Inventory is not added as a Character section by this ADR.
- `ADR-024` governs development economy and existing ability acquisition patterns; item abilities reuse `CharacterAbility SourceKind=Item`, not a parallel ability store.
- `ADR-025` governs Character lifecycle/delete and ruleset migration; inventory/item dependency checks extend its delete preconditions.
- `ADR-026` governs `.odchar` export/import; this ADR does not change that file format.

---

# 15. Rules for Codex

Codex must:

1. Keep Content Catalog definitions separate from runtime instances.
2. Model Inventory as a separate aggregate root for SLICE-05.
3. Reuse Content Block System lifecycle rules: physically delete only unused Draft definitions; archive Published, referenced, or runtime-used definitions; keep archived definitions loadable for existing runtime state, history, previews, and migrations.
4. Keep one item or stack in exactly one place.
5. Store full mechanics snapshots on `ItemInstance`; never make existing item mechanics depend on the latest ItemDefinition.
6. Allow shared `ItemStack` snapshots only for mechanically identical stackable items.
7. Use equipment as Inventory-owned location state referencing slots/body parts; do not duplicate equipped item authority into Character.
8. Use existing `CharacterAbility SourceKind=Item` for item-granted abilities.
9. Use future `ActiveEffect` aggregate roots for item-applied effects and never mass-migrate existing ActiveEffects to a new EffectDefinition.
10. Close `RemoveBodyPart` and `DeleteCharacterPermanently` item/inventory dependency stubs through real dependency checkers when item/inventory implementation begins.
11. Implement ItemDefinition migration only through MainGM preview/confirm with backup, revision guards, blocked incompatibilities, atomic snapshot update, and no post-success rollback command.
12. Keep ordinary players from mutating authoritative inventory offline or submitting trusted final item/equipment/damage state.
13. Avoid product code, schema, command implementation, attack pipeline, Content Editor UI, marketplace, arbitrary scripts, concrete catalog balancing, or Unity UI under this ADR task.

---

# 16. Definition of Done for future implementation tasks

Implementation tasks using this ADR must prove, with tests where applicable:

1. Publishing a new ItemDefinition version does not change existing `ItemInstance` or `ItemStack` snapshots.
2. An unused Draft definition can be physically deleted, while Published, catalog-referenced, or runtime-referenced definitions are archived and remain loadable for existing runtime state/history/previews/migrations.
3. Confirmed ItemDefinition migration updates all matching item/stack snapshots atomically and preserves runtime state.
4. A mechanically divergent unit cannot remain in a shared `ItemStack`.
5. An item cannot be simultaneously equipped, contained, dropped, consumed, or destroyed.
6. Equipping armor/gear references valid body parts and blocks `RemoveBodyPart` until resolved.
7. `DeleteCharacterPermanently` rejects while inventory/equipment/item/effect dependencies remain.
8. Item-granted abilities appear through `CharacterAbility SourceKind=Item` and disappear/suppress correctly when the item source is no longer valid, without removing permanent abilities.
9. Item-applied effects create `ActiveEffect` snapshots and existing ActiveEffects do not change after publishing a new EffectDefinition.
10. Non-MainGM mass migration attempts are rejected with no state change.
11. Offline/player-supplied inventory or final damage state is never trusted as authoritative.
12. Core item/inventory/equipment logic compiles without Unity dependencies in the pure .NET path.

---

# 17. Рассмотренные альтернативы

## 17.1 Inventory inside Character vs separate aggregate root

**Considered:** store Inventory as another `ADR-022` Character section. **Rejected** for SLICE-05 because inventory can move between characters, scene locations, dropped objects, and item/effect workflows; equipment and attack commands would force broad Character locks and make dependency checks harder to compose.

**Accepted:** Inventory as a separate aggregate root with Character projections derived from it where needed.

## 17.2 Existing item instances read latest ItemDefinition on use

**Considered:** keep runtime items thin and always look up latest published ItemDefinition. **Rejected** because it would make publication silently change existing items and violate roadmap/domain snapshot requirements.

**Accepted:** full mechanics snapshot per ItemInstance, changed only by confirmed migration.

## 17.3 Migrate ActiveEffects like ItemInstances

**Considered:** mass-update existing ActiveEffects when EffectDefinition changes. **Rejected** because effects may be mid-duration and mid-combat, and Domain Model section 18 explicitly states existing ActiveEffects never migrate to new EffectDefinition.

**Accepted:** ActiveEffects retain application snapshots; only future applications use new definitions.

## 17.4 AssistantGM can run ItemDefinition mass migration when allowed to publish

**Considered:** treat mass migration as another Content publish operation available to AssistantGM with `Content.Publish`. **Rejected** because Domain Model section 17.4 says only MainGM can run ItemDefinition mass migration, and migration changes existing runtime campaign state, not just catalog definitions.

**Accepted:** AssistantGM publish remains only where existing Content permissions allow; mass migration is MainGM-only.

---

# 18. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- full attack pipeline;
- concrete persistence schema and migrations;
- concrete item/equipment commands;
- concrete content package/marketplace workflows;
- balanced MVP item, weapon, armor, ammo, ability, effect, resource, or body-part catalog entries;
- polished Unity UI.

---

# 19. Трассировка

ADR реализует и уточняет:

- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md` sections 5, 6, 21, 22, 34, and 35: mechanical definitions, structural definitions, Draft/Published/Archived lifecycle, deletion/archive rules, published version immutability, EffectDefinition/ApplyEffectBlock, effect duration including `WhileItemEquipped`, Content permissions, and Content commands.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14: Inventory, `ItemStack`, `ItemInstance`, equipment, ammo/weapon/armor state, definitions/snapshots, ActiveEffect snapshot, no implicit active-effect rewrite, full attack prerequisite; and section 16.9 for ItemDefinition migration workflow.
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` sections 16-18: ContentDefinition, Inventory, ItemStack, ItemInstance, ItemDefinition migration preview/confirm, weapon/armor state, InventoryTransaction, ActiveEffect, and ActiveEffect snapshot/migration difference.
- `docs/tasks/SLICE-05_BACKLOG.md`, creating the first prerequisite ADR slot for `SLICE-05`.

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency/transaction vocabulary;
- `ADR-003` for explicit serialized contracts and no direct Domain serialization;
- `ADR-007` for version identity discipline;
- `ADR-011` for campaign format/version dimensions;
- `ADR-012` for append-only journal, transaction atomicity, compensation, and backup/snapshot;
- `ADR-013` for database schema migration boundary;
- `ADR-019` for permissions/redaction baseline;
- `ADR-022` for Character aggregate/section/history boundaries;
- `ADR-024` for development economy and `CharacterAbility` acquisition/source patterns;
- `ADR-025` for Character lifecycle/delete and ruleset migration boundaries;
- `ADR-026` for `.odchar` export/import format.

SLICE-04 stubs unblocked:

```text
RemoveBodyPart item dependency check
DeleteCharacterPermanently inventory/item dependency checker
```

---

# 20. Нормативное действие

**This ADR is `Accepted`.** The product owner explicitly approved moving forward with `ADR-027` on 2026-09-03, recorded by `ODY-S05-002` (`docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md`) — the same task that closed `docs/tasks/SLICE-05_BACKLOG.md`'s prerequisite revision and created `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`. The product owner's approval additionally fixed these first-implementation-scope decisions, not previously decided by this ADR and recorded in `SLICE-05_IMPLEMENTATION_BACKLOG.md` rather than by amending this document:

- Content Catalog MVP is the required technical foundation and must be implemented before any Inventory/`ItemInstance`/Equipment runtime task begins.
- MainGM must be able to create and edit catalog content in the MVP (GM catalog authoring is in scope, not deferred).
- The MVP catalog is base/Ruleset-scoped only; campaign-specific custom catalog content or per-campaign overrides are explicitly out of scope for this first revision (this ADR's own "campaign/Ruleset catalog" wording in section 3.1 is not narrowed by this choice — it remains a future option this revision simply does not exercise yet).
- Archived definitions must be visible to MainGM in a separate Archived list (query/data-level requirement; no UI is implied or authorized by this ADR).
- Catalog validation before publication must check real usability/applicability of a definition (e.g., a weapon actually has coherent attack properties, an ammo definition is compatible with the weapons that reference it) — not merely that required fields are non-empty.

Now that this ADR is `Accepted`:

- `SLICE-05` implementation tasks must treat Content Catalog as definition-only and runtime item/equipment/effect state as separate authoritative campaign state;
- unused Draft definitions may be physically deleted, while Published, catalog-referenced, or runtime-referenced definitions must be archived and remain loadable for existing runtime state, history, previews, and migrations;
- Inventory must be implemented as a separate aggregate root;
- ItemInstance/ItemStack snapshot and migration rules from sections 6 and 10 become mandatory;
- ActiveEffect snapshot non-migration from section 11 becomes mandatory;
- future item/inventory work must close the `RemoveBodyPart` and `DeleteCharacterPermanently` stubs named in section 9;
- changing this boundary requires an amendment or superseding ADR, not silent implementation drift.

---

**Конец документа**
