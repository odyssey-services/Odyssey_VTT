# Odyssey VTT - SLICE-05 Inventory, Effects, and Full Attack Prerequisites Backlog

**Status:** Prerequisite backlog - COMPLETE. `ADR-027` was proposed by `ODY-S05-001` (PR [#103](https://github.com/odyssey-services/Odyssey_VTT/pull/103), merged into `main`) and is now `Accepted` — the product owner explicitly approved moving forward with `ADR-027` on 2026-09-03, recorded by `ODY-S05-002`. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` now exists and is the active backlog for `SLICE-05` implementation work; this document remains a historical record of the prerequisite ADR revision and is not edited further except for this closure note.
**Slice:** `SLICE-05 - Inventory, Items, Abilities, Effects, and Full Attack (prerequisites)`
**Parent task:** `docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`
**ExecPlan:** `docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md`
**Created:** 2026-09-03
**Last updated:** 2026-09-03 (prerequisite revision closed; ADR-027 Accepted) UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 14's `SLICE-05` prerequisite gap into a reviewable ADR task before any inventory, equipment, item command, persistence schema, effect lifecycle, or full attack implementation starts.

`SLICE-04` closed with working Character, ability, resource, anatomy, lifecycle, export/import, and ruleset-migration mechanics. It deliberately left item/inventory integration points as documented stubs where no item system existed yet. `SLICE-05` cannot safely start by filling those stubs inline because item definitions, runtime item instances, inventory ownership/location, equipment slots/body parts, item-sourced abilities/effects, and ItemDefinition migration all affect public contracts and authoritative state.

## 2. Slice exit criteria for this prerequisite revision

This prerequisite backlog revision is complete now that:

1. `ADR-027 - Content Catalog & Item/Equipment System` is accepted by explicit product-owner approval. **Done** — `Accepted` 2026-09-03, recorded in `ADR-027` section 20 and in `ODY-S05-002`'s own completion evidence.
2. `ADR-027` fixes the Content Catalog/ContentDefinition vs runtime instance boundary for SLICE-05. **Done** — `ADR-027` section 4.
3. `ADR-027` explicitly unblocks the `SLICE-04` documented stubs for `RemoveBodyPart` item dependency checks and `DeleteCharacterPermanently` inventory/item dependency checkers. **Done** — `ADR-027` section 9.

These were not the full `SLICE-05` exit criteria from roadmap section 14.8. Full attack implementation remains a future implementation-backlog block after the Content Catalog MVP block `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` now scaffolds — see that document's own section 2 for the current, narrower exit criteria this first revision actually covers.

## 3. Scope decisions requiring explicit justification

### 3.1 Content catalog and item/equipment boundary requires ADR-027

Roadmap section 14 names inventory, `ItemStack`, `ItemInstance`, equipment, ammo/weapon/armor state, definitions/snapshots, effects/resources, and a full attack pipeline. Domain Model sections 16-18 already describe `ContentDefinition`, Inventory, `ItemInstance`, ItemDefinition migration preview/confirm, weapon/armor state, and `ActiveEffect`. The existing Content Block System document already names mechanical definitions (`Item`, `Weapon`, `Armor`, `Ammo`, `Ability`, `Effect`) and structural references (`BodyPart`, `Resource`).

Those authorities are necessary but not sufficient for implementation decomposition because the Domain Model deliberately still allowed Persistence to implement Inventory either inside `Character` or as a separate root. `SLICE-05` also needs one accepted decision for whether existing `ItemInstance` snapshots move when new definitions are published, how stacks share snapshots, how one item is in exactly one place, how item-sourced abilities/effects connect to existing `CharacterAbility SourceKind=Item`/`ActiveEffect`, and how ItemDefinition migration differs from ActiveEffect snapshot immutability.

Therefore this prerequisite backlog creates `ODY-S05-001` for `ADR-027`.

### 3.2 Attack pipeline implementation is not part of this prerequisite backlog

Roadmap section 14.6 names action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, and game log. Those are full implementation and likely further ADR/task-decomposition work. `ADR-027` deliberately fixes only the item/equipment/content-catalog substrate needed before that pipeline can safely reference items, equipment, ammo, abilities, and effects.

## 4. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-S05-001` | ADR: Content Catalog & Item/Equipment System | Done (PR [#103](https://github.com/odyssey-services/Odyssey_VTT/pull/103), merged into `main`) | `SLICE-04` closed, `ADR-026` accepted | ExecPlan | Proposed `ADR-027` (`docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`). Fixes Content Catalog/ContentDefinition vs runtime instance boundaries, ItemDefinition/typed item definitions/AbilityDefinition/EffectDefinition SLICE-05 catalog scope, full `ItemInstance` mechanics snapshots, stack snapshot sharing, Inventory as a separate aggregate root, equipment ownership/location invariants, item-sourced abilities/effects integration, SLICE-04 stub closure, ItemDefinition migration preview/confirm, and ActiveEffect snapshot non-migration. |
| 2 | `ODY-S05-002` | Record ADR-027 Acceptance & Create SLICE-05 Implementation Backlog | In Review | `ADR-027` proposed | Brief plan | Records explicit product-owner approval of `ADR-027` (now `Accepted`), closes this prerequisite backlog's own exit criteria (section 2), and creates `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — the active `SLICE-05` implementation backlog, ordering a Content Catalog MVP task group (`ODY-S05-101`-`106`) before any Inventory/ItemInstance/Equipment runtime task. |

Each later child task must create its own task contract and decide its own Brief-plan-vs-ExecPlan mode when activated. This backlog does not authorize product code, persistence schema, or real item/equipment commands.

## 5. Global non-goals

This prerequisite backlog excludes:

- product code, tests, persistence schema, migrations, Unity UI, or concrete DTO implementation;
- real `CreateItemInstance`, `TransferItem`, `EquipItem`, `ConsumeItem`, `ReloadWeapon`, `RepairArmor`, or attack-resolution commands;
- the full attack pipeline;
- full Content Editor UI;
- marketplace or content-package implementation;
- arbitrary scripts in content definitions;
- balancing concrete MVP catalog entries;
- changing `SLICE-04` accepted ADRs or implementation tasks.

## 6. Backlog change control

- New `SLICE-05` work requires a new `ODY-S05-XXX` task contract.
- `ADR-027` is now `Accepted` (section 2). This document remains historical and is not further edited except for closure notes already recorded above.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` now exists and is the active backlog for all further `SLICE-05` implementation work; new child tasks are reserved and decomposed there, not in this prerequisite backlog.
- If a later implementation task discovers another architectural gap, it must stop and request a dedicated ADR task rather than deciding it inline.
