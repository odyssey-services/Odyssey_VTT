# Odyssey VTT — SLICE-05 Content Catalog, Inventory, Items, Abilities, Effects, and Full Attack Implementation Backlog

**Status:** Implementation revision — OPEN. Content Catalog MVP (`ODY-S05-101`–`106`) complete; Inventory runtime block (`ODY-S05-201`–`207`) decomposed for the next implementation wave.
**Slice:** `SLICE-05 — Inventory, Items, Abilities, Effects, and Full Attack (implementation)`
**Parent task:** `docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-05_BACKLOG.md` (prerequisite ADR revision — `COMPLETE` as of `ODY-S05-002`/`ADR-027`; not rewritten by this document)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-09-03
**Last updated:** 2026-09-12 UTC

## 1. Purpose

This backlog converts roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 14's `SLICE-05` scope into small, reviewable implementation tasks, now that `docs/tasks/SLICE-05_BACKLOG.md`'s own prerequisite revision is `COMPLETE` (`ADR-027 — Content Catalog & Item/Equipment System` is `Accepted`).

This backlog does **not** itself implement anything. It decomposes the slice into ordered child tasks, each of which will be its own separate task contract and pull request, activated one at a time — the same convention `SLICE-01_IMPLEMENTATION_BACKLOG.md` through `SLICE-04_IMPLEMENTATION_BACKLOG.md` used. No child task contract file is created by this document; it only reserves numbers, titles, and boundaries for the block it decomposes.

Unlike prior slices' own first implementation-backlog revision, this document initially decomposed only **one** block of `SLICE-05` — the Content Catalog MVP — rather than the whole slice at once. This followed explicit product-owner direction (section 3.1): the catalog is the technical foundation the rest of `SLICE-05` (Inventory, `ItemInstance`/`ItemStack`, Equipment, item-sourced abilities/effects, `ItemDefinition` migration, and the full attack pipeline) needs before those later blocks can safely reference real definitions. After the Content Catalog MVP closed in PR #111, `ODY-S05-107` added the next Inventory runtime decomposition block in section 7. After the Inventory runtime block closed (`ODY-S05-201`–`207`, merged into `main`), `ODY-S05-108` added the next Equipment runtime decomposition block in section 12.

Its sources of scope are, exclusively:

- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (`Accepted`) — the Content Catalog/runtime boundary, `ContentDefinition` archive/delete lifecycle, Inventory aggregate root, `ItemInstance`/`ItemStack` snapshot rules, equipment model, item-sourced ability/effect integration, `ItemDefinition` migration, and permissions baseline.
- The product owner's own explicit MVP-scoping decisions, recorded in `ADR-027` section 20 and restated here: Content Catalog MVP first; MainGM must be able to author content in the MVP; base/Ruleset catalog only, no campaign-specific catalog or overrides yet; Archived content must be visible to MainGM in a separate Archived list; validation must check real usability/applicability, not just required-field presence.
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md` sections 5, 6, 21, 22, 34, 35 — mechanical/structural definition vocabulary and Draft/Published/Archived lifecycle rules `ADR-027` section 4.1 already applies to the `SLICE-05` catalog.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14 — used to scope the Inventory runtime decomposition in section 7 and to keep Equipment/Effects/Attack as later reserved blocks in section 8.

No child task in this backlog reopens any decision `ADR-027` (or any earlier accepted ADR) already made; each builds directly on those contracts as fixed.

## 2. Exit criteria for this revision (Content Catalog MVP block only)

This is **not** the full `SLICE-05` exit criteria from roadmap section 14 — only for the Content Catalog MVP block this revision decomposes:

1. Content Catalog Foundation persists `ContentDefinition` records with a Draft/Published/Archived lifecycle and version/revision rules, storing no runtime item/equipment/effect state (`ADR-027` section 4).
2. MainGM can create and edit Draft definitions and create a new Draft version from a Published definition; the catalog is base/Ruleset-scoped only, with no campaign-specific catalog or override mechanism.
3. Publish/Archive/Delete lifecycle: a valid Draft can be published to an immutable Published version; Published or referenced definitions are archived, never physically deleted; only unused Drafts can be physically deleted; Archived definitions are visible to MainGM in a separate Archived list.
4. Catalog validation proves real usability/applicability — not just required-field presence — for item/weapon/armor/ammo/ability/effect definitions before publish, including missing-definition-reference checks, `ContentBlock` graph cycle/unsupported-operation rejection, and Ruleset/version compatibility checks.
5. Base definition types (`ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition`, plus `Resource`/`BodyPart` references) carry typed properties sufficient for later Inventory/Equipment/Attack tasks to consume without re-deciding catalog shape when those tasks are activated.
6. A minimal built-in/test catalog fixture set proves weapon, armor, ammo, ability, effect, resource, and body-part references and validation work end-to-end through the full Foundation/Authoring/Validation/Publish pipeline, without full balancing or a final MVP content pack.

Closing the full `SLICE-05` slice (Inventory/Equipment/full-attack) is explicitly **not** part of this revision — see section 7 (Inventory runtime decomposition), section 8 (reserved future blocks), and section 9 (non-goals).

## 3. Scope decisions requiring explicit justification

### 3.1 Content Catalog MVP is the first implementation block, before Inventory/`ItemInstance`/Equipment runtime

Product owner's explicit direction: "Catalog MVP must be technical foundation first, so GM can later create needed content." `ADR-027` already requires content definitions to exist and be validated before any runtime item/instance can reference or snapshot from them; sequencing catalog-first avoids inventing runtime item/inventory/equipment shapes ahead of the definitions they must snapshot from and avoids re-deciding catalog shape mid-way through a later block.

**Decision:** `ODY-S05-101`–`106` (Content Catalog MVP) were the only concretely-scoped child tasks in the first backlog revision. Once PR #111 merged, `ODY-S05-107` decomposed the next Inventory runtime block into `ODY-S05-201`–`207` (section 7). Equipment runtime, item use/effects, ItemDefinition migration workflow, and the full attack pipeline remain later blocks (section 8).

### 3.2 Base/Ruleset catalog only; no campaign-specific catalog or overrides in the MVP

`ADR-027` section 3.1 deliberately leaves the catalog scoped at either campaign or Ruleset level open ("The campaign/ruleset catalog of versioned... `ContentDefinition` records"). The product owner's explicit MVP answer narrows this for the first revision without contradicting or amending `ADR-027`: "For now: base/ruleset catalog only, no campaign-specific custom catalog/overrides."

**Decision:** `ODY-S05-101`/`102` implement catalog storage and authoring scoped to the Ruleset only. No campaign-specific override or custom-catalog mechanism is designed or implemented in this revision; it remains a future, separately-scoped decision if the product owner ever requests it.

### 3.3 GM authoring is in scope for the MVP, not deferred behind a static seed catalog

Product owner's explicit answer: "MainGM must be able to create/edit content in MVP." A narrower MVP could instead ship only a hard-coded seed catalog with no authoring commands until a later revision.

**Decision:** `ODY-S05-102` (GM Catalog Authoring MVP) is part of this same first block, not deferred — MainGM-issued create/edit/new-Draft-version commands are required in this revision, not merely a static loader.

### 3.4 Validation must prove usability/applicability, not just required-field presence

Product owner's explicit answer distinguishes "has all required fields" from "is actually usable" — e.g. a weapon definition with a non-empty `Damage` field but no valid `AmmoRef` when ammo is required is not usable even though every field is populated. `ADR-027` fixes the catalog/runtime boundary and archive/delete lifecycle but does not itself specify concrete per-type field-level validation rules.

**Decision:** `ODY-S05-104` (Catalog Validation MVP) is its own dedicated task, separate from Foundation (`101`) and Authoring (`102`), because usability validation needs real per-type rules (weapon attack properties, ammo compatibility, ability trigger/cost/target rules, effect duration/stacking, dependency-missing checks, `ContentBlock` cycle checks, Ruleset/version compatibility) — a materially larger and different concern than storage/authoring plumbing, and one `104` alone should own so `103`'s own publish gate has one authoritative source of truth to call.

### 3.5 The Archived list is a query/data requirement, not a UI task

Product owner's explicit answer: "Archived content must be visible to GM in a separate Archived list." No Unity UI exists yet for any part of `SLICE-05` (non-goal, section 9), matching every prior slice's own UI-deferral convention.

**Decision:** `ODY-S05-103` implements the archived-definitions query/data shape distinguishing Archived from Draft/Published, satisfying the product-owner requirement at the data layer; rendering that list in any UI is out of scope for this revision (`SLICE-10`-era UI work, the same convention `SLICE-01`–`04` already used for their own UI deferrals).

## 4. No new ADR needed

Every question this backlog's own Content Catalog MVP decomposition touches is already answered by `ADR-027` (catalog/runtime boundary, `ContentDefinition` archive/delete lifecycle, permissions baseline) plus the already-accepted substrate `ADR-027` itself builds on (`ADR-001`–`003`, `ADR-007`, `ADR-011`–`013`, `ADR-019`, `ADR-022`, `ADR-024`–`026`) and the pre-existing `11_Content_Block_System` product document (Draft/Published/Archived lifecycle, mechanical/structural definition vocabulary). The base/Ruleset-only scoping, GM-authoring-in-MVP, and usability-validation decisions in section 3 are backlog-level scope choices made directly by explicit product-owner instruction — they narrow *when* and *how much* of `ADR-027`'s own already-decided architecture this first revision implements, not new architecture, and none of them contradicts or reopens `ADR-027`. No open architectural question requiring a new ADR was found during this decomposition.

If a later implementation task, once activated, discovers a genuine architectural gap no accepted ADR answers, that task must stop and request a dedicated ADR task rather than deciding it inline — the same discipline `ADR-027` itself already established for its own successors.

## 5. Ordered backlog (Content Catalog MVP block)

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-101` | Done (PR [#105](https://github.com/odyssey-services/Odyssey_VTT/pull/105), merged into `main`) | `ADR-027` §4; `11_Content_Block_System` §6/21/22 | Content Catalog Foundation | None | ExecPlan | Storage/contracts for `ContentDefinition` records: Draft/Published/Archived lifecycle state, `Version`/`Revision` fields, Published-immutability enforced at the foundation level, and exact-version `ContentDefinitionRef` reference shape future runtime code will pin to. Explicit no-runtime-item-state boundary — the catalog stores definitions only, never `ItemInstance`/`ItemStack`/Inventory/equipment/`ActiveEffect` state (proven directly by schema/table-list tests). Base/Ruleset-scoped only (section 3.2). `SqliteContentCatalogRepository`/`IContentCatalogRepository`, 32 tests (`TC-CATALOG-001`–`012`), 4 new error codes. Amended post-review: idempotency reworked from a mutable `LastCommandId` row column to a durable `ContentDefinitionCommandLedger` table (`CommandId` primary key), fixing a real replay defect. |
| 2 | `ODY-S05-102` | Done (PR [#106](https://github.com/odyssey-services/Odyssey_VTT/pull/106), merged into `main`) | Product-owner MVP answer; `ADR-027` §4 | GM Catalog Authoring MVP | 101 | ExecPlan | `ContentCatalogAuthoringService` (Application layer, mirrors `BoardMovementService`'s own precedent): MainGM-only `CreateDraftDefinition`/`UpdateDraftDefinition`/`CreateNextDraftVersionFromPublished`, authorization checked before the repository is ever called. Repository extended with `CreateNextDraftVersionFromPublished` (copies a Published source's fields into a fresh Draft, never edits the source). All three commands reuse `ODY-S05-101`'s own `ContentDefinitionCommandLedger` for idempotency. Base/Ruleset catalog only — no campaign-specific catalog or per-campaign override mechanism (section 3.2). 14 new tests (`TC-CATALOG-013`–`023`), 2 new error codes. |
| 3 | `ODY-S05-103` | Done (PR [#110](https://github.com/odyssey-services/Odyssey_VTT/pull/110), merged into `main`) | `ADR-027` §4.1, §9; product-owner MVP answer | Publish/Archive/Delete Lifecycle | 101, 102, 104 | ExecPlan | `ContentCatalogLifecycleService` (Application layer, mirrors `ContentCatalogAuthoringService`'s own precedent): MainGM-only `PublishDefinition` (gated server-side by `104`'s own `CatalogValidationService.ValidateDraftForPublish`, zero mutation on rejection; sets `Status=Published`/`Version=1`/`PublishedByUserId`/`PublishedAt`), `ArchiveDefinition` (Published-only in this MVP; never physically deletes, row stays loadable), `DeleteDraftDefinition` (unused Drafts only; rejects Published/Archived/referenced targets via an atomic catalog-dependency scan; idempotent via a dedicated `ContentDefinitionDeleteLedger`, checked alongside the shared ledger so a `CommandId` reused from any non-delete operation is rejected with `CommandIdentityMismatch`, never a false replay), and `ListArchivedDefinitions` (data/query-only Archived list, no UI). Runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect dependency checks for delete are an explicit, recorded future extension boundary -- no such runtime state exists yet. 22 tests (`TC-CATALOG-078`–`099`), 2 new error codes. |
| 4 | `ODY-S05-104` | Done (PR [#108](https://github.com/odyssey-services/Odyssey_VTT/pull/108) and follow-up PR [#109](https://github.com/odyssey-services/Odyssey_VTT/pull/109), both merged into `main`) | Product-owner MVP answer; `ADR-027` §4 | Catalog Validation MVP | 101, 105 | ExecPlan | `CatalogValidationService` (`ValidateContentDefinition`/`ValidateDraftForPublish`, Application layer): real usability validation via `ODY-S05-105`'s own `TypedDefinitionCodec` for item/weapon/armor/ammo/ability/effect; missing/wrong-version/wrong-type exact-reference rejection with dependency-cycle detection across the real `ContentDefinitionRef` graph; Ruleset/version compatibility check for the definition being validated *and* (amendment, PR #109) for every resolved referenced definition and candidate ammo the traversal consults, so a definition can no longer pass by referencing/depending on content scoped to an incompatible Ruleset; ContentBlock/mechanics-payload MVP boundary validated structurally (no real `ContentBlockGraph` exists yet). Side-effect-free -- no repository write is ever called. 36 tests (`TC-CATALOG-042`–`077`), 0 new error codes (validation issues are a plain enum, not `ErrorCode`s). Consumed by `103`'s own future publish gate. |
| 5 | `ODY-S05-105` | Done (PR [#107](https://github.com/odyssey-services/Odyssey_VTT/pull/107), merged into `main`) | `ADR-027` §3.1, §4; `11_Content_Block_System` | Base Definition Types | 101 | ExecPlan | Typed catalog definitions: `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition`, plus `Resource` and `BodyPart` structural references, with typed properties sufficient for later Inventory/Equipment/Attack tasks to consume (attack properties, protection/durability, ammo compatibility, ability trigger/cost/target, effect duration/stacking) without re-deciding catalog shape when those future tasks are activated. No concrete balanced content. Explicit versioned `TypedDefinitionCodec` maps each typed shape to/from the existing `PropertiesJson` envelope; 14 new tests (`TC-CATALOG-024`–`037`), 2 new error codes. |
| 6 | `ODY-S05-106` | Done (PR [#111](https://github.com/odyssey-services/Odyssey_VTT/pull/111), merged into `main`) | Product-owner MVP answer | Minimal Test Catalog Fixtures | 102, 103, 104, 105 | Brief plan | A small built-in/test catalog (not a final content pack) proving weapon, armor, ammo, ability, effect, and cross-definition references (typed `BuiltInEffectRefs` and generic `DependencyRefs`) actually work end-to-end through the full Authoring/Validation/Publish/Archive/Delete pipeline -- authored via `ContentCatalogAuthoringService`, encoded via `TypedDefinitionCodec`, gated by `CatalogValidationService`, published/archived/deleted via `ContentCatalogLifecycleService`. Composes existing pieces only -- no new production code, no new lifecycle/validation semantics, no new error codes. 12 new tests (`TC-CATALOG-100`–`111`). No balancing, no marketplace, no `.odcontent` import/export. Closes the Content Catalog MVP block. |

"Planning mode" for tasks 1–5 reflects the expectation that each changes a future public contract, persistence schema, or authoritative catalog-lifecycle semantics — matching every prior slice's own precedent for its first-block tasks; each child task still makes and justifies its own Brief-plan-vs-ExecPlan decision per `PLANS.md` §1 when its own contract is authored. Task 6 is expected to be Brief plan (a fixture/proof task introducing no new architecture), mirroring `ODY-S04-114`/`ODY-S03-008`'s own precedent for integration-proof tasks.

Task contract files for completed `ODY-S05-101`-`106` and planning task `ODY-S05-107` already exist. Future `ODY-S05-201`-`207` task contract files are not created by this decomposition; each must be created and activated separately when picked up.

## 6. Task boundaries

### `ODY-S05-101` — Content Catalog Foundation

Implements `ADR-027` section 4's `ContentDefinition` storage/contracts: Draft/Published/Archived lifecycle field, `DefinitionVersion`/`Revision`, immutable Published rows, and the exact-version reference shape future runtime code will pin to. Does not implement authoring commands (`102`), publish/archive/delete transition rules (`103`), validation (`104`), or any typed definition's own properties (`105`) — only the generic `ContentDefinition` envelope and lifecycle state machine.

### `ODY-S05-102` — GM Catalog Authoring MVP

Implements MainGM-issued `CreateDraftDefinition`/`UpdateDraftDefinition`/`CreateNextDraftVersionFromPublished` commands over `101`'s own storage. Base/Ruleset catalog only. Does not implement publish/archive/delete (`103`) or validation rules (`104`) — a Draft may be saved incomplete or not-yet-usable; those become blocking only at publish time.

### `ODY-S05-103` — Publish/Archive/Delete Lifecycle

Implements `PublishDefinition` (gated by `104`'s validation), `ArchiveDefinition`, and physical-delete-Draft-only rules per `ADR-027` section 4.1/9, plus the Archived-list query surfacing archived definitions to MainGM. Does not implement the validation rules themselves (`104`) or typed definition properties (`105`). Must enforce, at minimum, these product-owner/`ADR-027`-section-4.1 invariants: **Published or runtime-used definitions must not be physically deleted** — only an unused Draft may be physically deleted; and **Archived definitions must remain loadable for existing runtime state, history, previews, and future migrations** — archiving must never make a definition unreadable to code that still needs to render or migrate against it.

### `ODY-S05-104` — Catalog Validation MVP

Implements per-type usability/applicability validation for item/weapon/armor/ammo/ability/effect definitions, missing-reference checks, `ContentBlock` cycle/unsupported-operation rejection, and Ruleset/version compatibility checks, consumed by `103`'s own publish gate. Depends on `105`'s typed properties to validate against real fields, not a placeholder shape.

This task's own contract must implement, at minimum, every one of these product-owner-specified validation expectations (verbatim, not paraphrased into something weaker):

- **Weapon definitions** must have usable attack properties: damage, range, mode, and action cost, and a valid ammo reference when ammo is required.
- **Armor definitions** must reference valid equipment slots/body parts and have protection/durability properties.
- **Ammo definitions** must be compatible with the weapons that reference them.
- **Ability definitions** must have usable entry point/trigger/cost/target rules where applicable.
- **Effect definitions** must define target rules, duration, stacking policy, and snapshot-relevant mechanics.
- **Definitions must not reference missing definitions.**
- **`ContentBlock` graphs must reject cycles and unsupported operations.**
- **Definitions must be compatible with the active Ruleset/version.**

Two further product-owner validation expectations are lifecycle invariants, not publish-time checks, and are `103`'s own responsibility instead: **Published/runtime-used definitions must not be physically deleted**, and **Archived definitions must remain loadable for existing runtime state, history, previews, and future migrations** (both already fixed by `ADR-027` section 4.1, restated in `103`'s own boundary above).

### `ODY-S05-105` — Base Definition Types

Implements the typed catalog definitions (`ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition`) plus `Resource`/`BodyPart` references, with properties sufficient for later Inventory/Equipment/Attack consumption. Does not implement validation rules (`104`) or any runtime item/equipment/ability/effect behavior — properties only, not mechanics execution.

### `ODY-S05-106` — Minimal Test Catalog Fixtures

Implements a small built-in/test catalog exercising `101`–`105` together end-to-end — the same "integration proof, not a new feature" role `ODY-S01-013`/`ODY-S02-013`/`ODY-S03-008`/`ODY-S04-114` played for their own slices. No new production code beyond what a proof needs; no balancing, no final content pack.

## 7. Ordered backlog (Inventory runtime block)

`ODY-S05-107` decomposes the next block after Content Catalog MVP closure. This block implements Inventory runtime foundations only: the separate Inventory aggregate root, runtime item/stack state, persistence, basic creation/move/stack commands, and runtime dependency checks needed before Equipment, item use/effects, ItemDefinition migration, and attack pipeline work can safely begin.

It preserves these `ADR-027` decisions:

- Inventory is a separate aggregate root, not a Character section.
- Runtime items pin an exact `ContentDefinitionRef` and store a copied mechanics snapshot; they do not depend on the latest catalog definition.
- `ItemInstance` stores its own mechanics snapshot; `ItemStack` may share one snapshot only for mechanically identical stackable items.
- Equipment is Inventory-owned location state; this block names it only where location modeling requires it.
- Runtime references must protect catalog definitions from physical delete once runtime item state exists.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-201` | Done (PR [#113](https://github.com/odyssey-services/Odyssey_VTT/pull/113)) | `ADR-027` §5/6; Domain Model §17.1-17.3 | Inventory Runtime Foundation | 101-106, 107 | ExecPlan | Domain/Application contracts for `InventoryId`, Inventory owner/location model, containment/location revision, `ItemInstance`/`ItemStack` identity boundaries, exact `ContentDefinitionRef`, and core one-place invariants. Establishes that Inventory is not a Character section and owns containment/location state. No persistence schema, no item creation commands, no equipment behavior, no attack pipeline. |
| 2 | `ODY-S05-202` | Done (PR [#114](https://github.com/odyssey-services/Odyssey_VTT/pull/114)) | `ADR-027` §5/6/14; `ADR-002`/`003`/`011`/`012`/`013` | Inventory Persistence Foundation | 201 | ExecPlan | SQLite persistence/contracts for Inventory runtime state: inventories, item instances, item stacks, locations/location index, revision fields, idempotency ledger, and optimistic concurrency boundaries. No creation-from-catalog command semantics beyond repository primitives, no equipment command, no attack pipeline. |
| 3 | `ODY-S05-203` | Done (PR [#115](https://github.com/odyssey-services/Odyssey_VTT/pull/115), merged into `main`) | `ADR-027` §4/6/12 | Create Item/Stack From Published Definition | 201, 202 | ExecPlan | MainGM-authoritative commands create `ItemInstance` or `ItemStack` from a Published catalog definition, pin the exact definition version, and copy a full mechanics snapshot into runtime state. Rejects Draft, Archived, missing, wrong-type, or validation-incompatible definitions. No transfer, split/merge, equipment, item use, ActiveEffect execution, or ItemDefinition migration. |
| 4 | `ODY-S05-204` | In Review (PR [#116](https://github.com/odyssey-services/Odyssey_VTT/pull/116)) | `ADR-027` §5/7; Domain Model §17.5 | Inventory Move / Transfer MVP | 201, 202, 203 | ExecPlan | MainGM-only contained-to-contained ItemInstance/ItemStack moves with atomic target/Inventory revision guards, persisted destination owner, and separate `InventoryMoveCommandLedger` replay identity. No equip/unequip, drop/pickup, split/merge, attack, item use, or ActiveEffect behavior. |
| 5 | `ODY-S05-205` | Done (PR [#119](https://github.com/odyssey-services/Odyssey_VTT/pull/119), merged into `main`) | `ADR-027` §6 | Stack Split/Merge MVP | 201, 202, 203, 204 | ExecPlan | Commands split stack quantities and merge only mechanically identical stacks. Rejects negative/zero-invalid quantities and mechanically divergent merges, including divergent definition snapshots or runtime state. No unique-item conversion workflow unless required by split/merge invariants; no equipment or attack behavior. |
| 6 | `ODY-S05-206` | Done (PR [#120](https://github.com/odyssey-services/Odyssey_VTT/pull/120), merged into `main`) | `ADR-027` §4.1/9; `ADR-025` | Runtime Reference Dependency Checks | 201, 202, 203 | ExecPlan | Extends catalog delete/archive dependency boundaries so physical delete can detect runtime `ItemInstance`/`ItemStack`/Inventory references once runtime item state exists. Closes, or explicitly schedules with concrete follow-up task IDs, the `RemoveBodyPart` item dependency check and `DeleteCharacterPermanently` inventory/item dependency checker stubs named by `ADR-027` §9. No new delete/archive semantics beyond `ADR-027`. |
| 7 | `ODY-S05-207` | Done (PR [#121](https://github.com/odyssey-services/Odyssey_VTT/pull/121), merged into `main`) | Roadmap §14; `ADR-027` §4/6 | Inventory Runtime Integration Fixtures | 201-206 | Brief plan | Minimal integration fixtures proving Published catalog definitions can create runtime item snapshots and Inventory state end-to-end through the new runtime foundation. Documentation/test proof only for the Inventory runtime block; no Equipment runtime, ActiveEffect execution, attack pipeline, ItemDefinition migration workflow, balanced content pack, `.odcontent`, or Unity UI. |

### 7.1 Inventory runtime task boundaries

`ODY-S05-201` owns the foundation vocabulary and aggregate boundary: `InventoryId`, owner/location refs, containment/location revision, runtime item/stack identities, and invariant wording. It must not introduce persistence schema or commands whose behavior belongs to later tasks.

`ODY-S05-202` owns persistence, idempotency, and optimistic concurrency. It must keep schema/contracts separate from catalog definitions and must not create the actual item/stack-from-definition command semantics.

`ODY-S05-203` owns creation from Published catalog definitions and snapshot copy. It is the first task that turns catalog definitions into runtime state, but it does not move, equip, use, migrate, or execute item mechanics.

`ODY-S05-204` owns item/stack movement and transfer across valid inventory locations. It may define the minimal location vocabulary needed to keep equipment as later Inventory-owned location state, but it must not implement equip/unequip behavior.

`ODY-S05-205` owns stack quantity and mechanically-identical split/merge rules. It must not weaken `ADR-027`'s shared-snapshot rule.

`ODY-S05-206` owns runtime dependency checks against physical catalog deletion and the `SLICE-04` stubs named by `ADR-027` §9. If a stub cannot be fully closed without Equipment or ActiveEffect runtime, this task must add an explicit follow-up task ID instead of leaving an unnamed TODO.

`ODY-S05-207` is the integration proof for the Inventory runtime block, mirroring `ODY-S05-106` for Content Catalog MVP. It should compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook.

## 8. Reserved future blocks (not decomposed in this revision)

Per section 3.1's explicit sequencing decision, `SLICE-05`'s remaining named block has now also been decomposed. Each such block becomes its own backlog revision-block once its prerequisites are accepted and closed, unless the product owner explicitly changes sequencing.

The item-sourced abilities/effects block (`CharacterAbility SourceKind=Item` integration and the `ActiveEffect` aggregate, `ADR-027` section 8) was decomposed by `ODY-S05-110` — see section 14.

The full attack pipeline block (roadmap section 14.6's action/preview/range/modifier/roll/hit/damage/effect-application vertical slice) was decomposed by `ODY-S05-112` — see section 16. No block remains named-but-undecomposed in this section after this revision.

## 9. Global non-goals

This backlog revision excludes:

- campaign-specific custom content or per-campaign catalog overrides (section 3.2);
- a full visual node editor for content authoring;
- a marketplace or content-package distribution mechanism;
- `.odcontent` import/export implementation;
- a full, balanced MVP content pack (`106` is a proof fixture, not a content pack);
- implementing Inventory runtime under `ODY-S05-107` itself; section 7 only decomposes future implementation tasks;
- implementing Equipment runtime under `ODY-S05-108` itself; section 12 only decomposes future implementation tasks;
- implementing `ItemDefinition` migration under `ODY-S05-109` itself; section 13 only decomposes future implementation tasks;
- implementing the item-sourced abilities/effects block under `ODY-S05-110` itself; section 14 only decomposes one ADR-specification task (`ODY-S05-501`) — the block's remaining implementation tasks are a future backlog revision, not decided by this one;
- implementing the item-sourced abilities/effects implementation range under `ODY-S05-111` itself; section 15 only decomposes future implementation tasks (`ODY-S05-502`–`507`);
- Equipment runtime implementation beyond minimal location vocabulary needed by Inventory runtime tasks (section 8);
- item use and `ActiveEffect` runtime implementation beyond what `ODY-S05-502`–`507` (section 15) decompose — the six turn/round-based `EffectDurationType` values and any combat/damage-sourced effect application remain the full attack pipeline's own territory (section 16), not this block's;
- ItemDefinition migration workflow implementation (section 8);
- implementing the full attack pipeline block under `ODY-S05-112` itself; section 16 only decomposes one ADR-specification task (`ODY-S05-601`) — the block's remaining implementation tasks are a future backlog revision, not decided by this one, exactly as section 14 already did for `ODY-S05-501`;
- any Unity UI, including any Archived-list UI (`103` is data/query only, section 3.5);
- any change to `ADR-001`–`026` — all remain accepted as-is; any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline (section 4).

## 10. Dependency rules

- `ODY-S05-101` has no dependency — it is the foundational `ContentDefinition` envelope/lifecycle every later catalog task builds on.
- `ODY-S05-102` depends on `ODY-S05-101` (needs the `ContentDefinition` envelope to author into).
- `ODY-S05-103` depends on `ODY-S05-101` (lifecycle state to transition), `ODY-S05-102` (a Draft must exist to publish), and `ODY-S05-104` (publish is gated by validation).
- `ODY-S05-104` depends on `ODY-S05-101` (definitions to validate) and `ODY-S05-105` (real typed properties to validate against, not a placeholder shape).
- `ODY-S05-105` depends on `ODY-S05-101` only — typed definitions extend the generic envelope independently of authoring/publish/validation plumbing.
- `ODY-S05-106` depends on `ODY-S05-102`, `ODY-S05-103`, `ODY-S05-104`, and `ODY-S05-105` (it proves the whole pipeline together).
- `ODY-S05-201` depends on the completed Content Catalog MVP block (`ODY-S05-101`–`106`) and this decomposition task (`ODY-S05-107`).
- `ODY-S05-202` depends on `ODY-S05-201` (foundation vocabulary and aggregate boundary).
- `ODY-S05-203` depends on `ODY-S05-201` and `ODY-S05-202` (runtime state contracts and persistence exist before creation commands).
- `ODY-S05-204` depends on `ODY-S05-201`, `ODY-S05-202`, and `ODY-S05-203` (items/stacks must exist before they can move).
- `ODY-S05-205` depends on `ODY-S05-201`, `ODY-S05-202`, `ODY-S05-203`, and `ODY-S05-204` (stack lifecycle builds on creation and location invariants).
- `ODY-S05-206` depends on `ODY-S05-201`, `ODY-S05-202`, and `ODY-S05-203` (runtime references exist before dependency checks can query them).
- `ODY-S05-207` depends on `ODY-S05-201`–`206` (integration proof for the completed Inventory runtime block).
- `ODY-S05-301` depends on the completed Inventory runtime block (`ODY-S05-201`–`207`) and this decomposition task (`ODY-S05-108`).
- `ODY-S05-302` depends on `ODY-S05-301` (foundation vocabulary before persistence).
- `ODY-S05-303` depends on `ODY-S05-301` and `ODY-S05-302` (runtime shape and persistence exist before the Equip command).
- `ODY-S05-304` depends on `ODY-S05-301`, `ODY-S05-302`, and `ODY-S05-303` (an item must be equippable before it can be unequipped).
- `ODY-S05-305` depends on `ODY-S05-301`, `ODY-S05-302`, `ODY-S05-303`, and `ODY-S05-304` (both directions of the equipped-location transition must exist before real body-part dependency data is meaningful to check).
- `ODY-S05-306` depends on `ODY-S05-301`–`305` (integration proof for the completed Equipment runtime block).
- `ODY-S05-401` depends on the completed Content Catalog MVP block (`ODY-S05-101`–`106`), the completed Inventory runtime block (`ODY-S05-201`–`207`), and this decomposition task (`ODY-S05-109`) — preview construction reads Published catalog definitions and runtime `ItemInstance`/`ItemStack` snapshots, both of which those blocks already provide.
- `ODY-S05-402` depends on `ODY-S05-401` (blocking-incompatibility rules evaluate the preview `ODY-S05-401` builds).
- `ODY-S05-403` depends on `ODY-S05-401` and `ODY-S05-402` (confirm/apply needs a complete, incompatibility-checked preview before it can act).
- `ODY-S05-404` depends on `ODY-S05-401`–`403` (integration proof for the completed `ItemDefinition` migration block).
- `ODY-S05-501` depends on the completed `ItemDefinition` migration block (`ODY-S05-401`–`404`) and this decomposition task (`ODY-S05-110`) — the `ActiveEffect` aggregate specification builds on a closed migration block precedent (`ADR-027` §11's own migration-vs-`ActiveEffect` distinction) and needs no other block's own code. Any future implementation task in this block (`ODY-S05-502` onward, not yet decomposed) will depend on `ODY-S05-501`'s own accepted ADR.
- `ODY-S05-502` depends on the accepted `ADR-028` (`ODY-S05-501`) and this decomposition task (`ODY-S05-111`) — the aggregate/repository foundation needs the ADR's own field shape and persistence contract, and nothing else in this block yet exists to depend on.
- `ODY-S05-503` depends on `ODY-S05-502` (the aggregate/repository must exist before stacking resolution can read or write it).
- `ODY-S05-504` depends on `ODY-S05-502` (non-combat duration/expiry mutates the same aggregate `502` persists).
- `ODY-S05-505` depends on `ODY-S05-502` (the `WhileItemEquipped` suspend/resume transition and item-triggered creation both write the same aggregate).
- `ODY-S05-506` depends on `ODY-S05-502` (`RemoveActiveEffect` and the direct-creation permission gate both act on the same aggregate).
- `ODY-S05-507` depends on `ODY-S05-502`–`506` (integration proof for the completed item-sourced abilities/effects implementation range).
- `ODY-S05-601` depends on the completed item-sourced abilities/effects range (`ODY-S05-501`–`507`) and this decomposition task (`ODY-S05-112`) — the full attack pipeline's own `ActiveEffect` integration (the six turn/round-based `EffectDurationType` values, combat-triggered effect application) builds on the already-accepted `ADR-028` and needs no other block's own code. Any future implementation task in this block (`ODY-S05-602` onward, not yet decomposed) will depend on `ODY-S05-601`'s own accepted ADR.
- `ODY-S05-602` depends on `ODY-S05-601` and `ODY-S05-113`; `603` on `602`; `604` on `602`/`603`; `605` on `602`/`502`; `606` on `603`–`605`; `607` on `604`; `609` on `604`; `610` on `606`; `608` on `602`–`607` and `609` (not `610` -- see section 17.1's own point clarification).

## 11. Backlog change control

- New work requires a task contract; this document reserves numbers `ODY-S05-101` through `ODY-S05-106` for the completed Content Catalog MVP block, `ODY-S05-201` through `ODY-S05-207` for the completed Inventory runtime block, `ODY-S05-301` through `ODY-S05-306` for the Equipment runtime block (section 12), and `ODY-S05-401` through `ODY-S05-404` for the `ItemDefinition` migration block (section 13).
- `ODY-S05-501` through `ODY-S05-507` are reserved for the item-sourced abilities/effects block: `ODY-S05-501` (section 14, the `ActiveEffect` aggregate ADR-specification task) was decomposed by `ODY-S05-110`; `ODY-S05-502`–`507` (section 15) were decomposed by `ODY-S05-111`, once `501`'s own `ADR-028` was accepted. No further number in this block remains reserved-but-unscoped.
- `ODY-S05-601` through `ODY-S05-608` are reserved for the full attack pipeline block: accepted ADR `601` plus `602`–`608`, decomposed by `ODY-S05-113` in section 17. `ODY-S05-609` is a targeted, point extension of this same range, reserved by `ODY-S05-604`'s own post-review amendment (2026-09-14, product-owner approved) to close the Character/Item `AttackDelta`-application gap that block's own independent review found — not a silent oversight and not a reopening of `ODY-S05-113`'s own decomposition. `ODY-S05-610` is a second such targeted, point extension, reserved by `ODY-S05-606`'s own post-review amendment (2026-09-15, product-owner approved) to close the `RequestGMResolution` stacking-conflict persistence/resolution gap that block's own independent review confirmed (inherited from `ODY-S05-503`, disclosed rather than introduced by `606`) — likewise not a silent oversight and not a reopening of `ODY-S05-113`'s own decomposition. `ODY-S05-611` is a third such targeted, point extension, reserved by `ODY-S05-608`'s own post-review confirmation (2026-09-16, product-owner approved) to close the `CombatEffectExpiryService.EvaluateAndExpireIfDue` automatic-wiring gap `ODY-S05-605` itself first disclosed as deferred future work, which `606` did not pick up and `608` (a Brief-plan, composition-only task forbidden from introducing new production behavior) independently reconfirmed still open rather than silently closing or hiding it — likewise not a silent oversight and not a reopening of `ODY-S05-113`'s own decomposition. No number remains reserved-but-unscoped.
- A task may be split before implementation by updating this backlog, following the same rule prior backlog revisions in this repository already use.
- A task may not be merged with unrelated cleanup merely to reduce task count.
- Completed task files move to `docs/tasks/completed/` only after required review, per the established convention in this repository.
- This backlog does not replace any task's own acceptance criteria or `ADR-027`'s content; it does not itself decide any technical question beyond the five explicit scope decisions in section 3.
- The reserved future blocks in section 8 are named, not scoped — decomposing any of them into real task IDs is a future backlog revision, not an implicit extension of this one.
- If this document's section 3 narrowing decisions are later found incorrect or resolved sooner than expected, that is a new task/backlog-revision decision, not a silent edit to this document's already-recorded reasoning — this document would gain an explicit amendment note, not a rewritten section 3.

## 12. Ordered backlog (Equipment runtime block)

`ODY-S05-108` decomposes the next block after the Inventory runtime block's closure (`ODY-S05-201`–`207`, merged into `main`). This block implements Equipment as `ADR-027` section 7 already normatively defines it — Inventory-owned location state over exact item/stack references, equipment slot references, and optional body-part references — plus the one real, still-open `SLICE-04` stub `ADR-027` section 9.1 assigns to this block:

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

It preserves these `ADR-027` section 7 rules:

1. One item or stack is in exactly one place at a time — contained, equipped, dropped, consumed, destroyed, or another explicit lifecycle/location state.
2. Equipping moves an item from contained to equipped state; it does not copy item mechanics into Character as authoritative state.
3. Unequipping moves it back into a valid containment location or another explicit location selected by the command.
4. Equipment must reference body parts that currently exist on the owning Character when body-part-specific placement or armor coverage is required.
5. Removing a body part is rejected while equipment or item state depends on that body part, unless the same future command explicitly and atomically resolves the dependency under an accepted task contract.
6. Weapon, armor, and ammo runtime state stays with the item/stack, not with Equipment placement itself.

`ODY-S05-108` also verified directly in code (`InventoryCharacterDeletionDependencyChecker`/`SqliteInventoryRepository.HasAnyItemOwnedByCharacter`) that `ADR-027` section 9.2's "equipped items" requirement for `DeleteCharacterPermanently` is **already closed** by `ODY-S05-206` — that dependency query filters only on `CampaignId`/`OwnerKind`/`OwnerTargetRef`, never on location kind, and equipping an item never changes its `OwnerRef`. This block therefore does not reserve a separate `DeleteCharacterPermanently` task; see `ODY-S05-108`'s own task contract section 18 for the full finding.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-301` | In Review (PR [#123](https://github.com/odyssey-services/Odyssey_VTT/pull/123)) | `ADR-027` §5/7 | Equipment Runtime Foundation | 108, 201-207 | ExecPlan | Domain type(s) modeling `EquippedEntry` (or a typed extension of `InventoryLocationRef.Equipped` carrying the same fields) — `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision` — alongside the existing `EquipmentSlotRef` token. Establishes the one-place invariant (rule 1) and body-part-reference vocabulary only. No persistence schema, no Equip/Unequip commands, no `RemoveBodyPart` dependency check. |
| 2 | `ODY-S05-302` | In Review (PR [#124](https://github.com/odyssey-services/Odyssey_VTT/pull/124)) | `ADR-027` §5/7/14 | Equipment Persistence Foundation | 301 | ExecPlan | SQLite schema/contracts for persisting `EquippedEntry`-shaped state, read/write primitives on `IInventoryRepository` or a sibling contract, optimistic-concurrency (CAS) pattern mirroring `ODY-S05-202`/`204`. No Equip/Unequip command semantics, no `RemoveBodyPart` check. |
| 3 | `ODY-S05-303` | In Review (PR [#125](https://github.com/odyssey-services/Odyssey_VTT/pull/125)) | `ADR-027` §7 rules 1/2/4 | Equip Command MVP | 301, 302 | ExecPlan | MainGM-only command moving an item/stack from a valid contained location into an equipped location state, populating `EquipmentSlotRef`/`BodyPartRefs[]`/`EquippedByUserId`/`EquippedAt` with an atomic revision guard. Must enforce rule 4 (referenced body parts currently exist on the owning Character) and rule 1 (exclusive one-place location). No Unequip, no weapon/armor mechanical effects, no `RemoveBodyPart` check. |
| 4 | `ODY-S05-304` | In Review (PR [#126](https://github.com/odyssey-services/Odyssey_VTT/pull/126)) | `ADR-027` §7 rule 3 | Unequip Command MVP | 301, 302, 303 | ExecPlan | MainGM-only command moving an equipped item/stack back into a valid contained (or other explicit) location per rule 3, with a symmetric revision guard, reusing `ODY-S05-204`'s move/location vocabulary where it applies. No new Equip semantics beyond the reverse transition, no `RemoveBodyPart` check. |
| 5 | `ODY-S05-305` | In Review (PR [#127](https://github.com/odyssey-services/Odyssey_VTT/pull/127)) | `ADR-027` §7 rule 5; §9.1 | RemoveBodyPart Dependency Closure | 301, 302, 303, 304 | ExecPlan | Finally closes the `SqliteCharacterRepository.RemoveBodyPart` item/equipment-dependency stub (documented, not silent, since the original `SLICE-04` task and re-documented by `ODY-S05-206`): a real check for Equipment/item state depending on the body part being removed, rejecting the removal unless the same command atomically resolves the dependency. Does not touch `DeleteCharacterPermanently` (already closed by `ODY-S05-206`, see this section's intro). |
| 6 | `ODY-S05-306` | In Review (PR [#128](https://github.com/odyssey-services/Odyssey_VTT/pull/128)) | Roadmap §14 | Equipment Runtime Integration Fixtures | 301-305 | Brief plan | Minimal integration fixtures proving the Equipment runtime block works end-to-end, mirroring `ODY-S05-207`: a Published weapon/armor definition creates a runtime item, Equip succeeds and blocks `RemoveBodyPart` on a dependent body part, Unequip succeeds, and `RemoveBodyPart` then succeeds. Composes existing surfaces only; no new production behavior unless an earlier task deliberately reserved a tiny fixture hook. |

### 12.1 Equipment runtime task boundaries

`ODY-S05-301` owns the domain vocabulary for equipped-location state: the `EquippedEntry` shape (or equivalent typed extension of `InventoryLocationRef.Equipped`), `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision`. It must not introduce persistence schema or commands whose behavior belongs to later tasks.

`ODY-S05-302` owns persistence, idempotency, and optimistic concurrency for Equipment state. It must keep schema/contracts separate from the Equip/Unequip command semantics themselves.

`ODY-S05-303` owns the Equip transition and rule 4's body-part-existence check. It must not implement Unequip, weapon/armor mechanical effects, or the `RemoveBodyPart` check.

`ODY-S05-304` owns the Unequip transition and rule 3's valid-destination check. It must not re-implement Equip beyond the symmetric reverse transition.

`ODY-S05-305` owns closing the real `RemoveBodyPart` item/equipment-dependency stub (rule 5; `ADR-027` §9.1) — the one `SLICE-04` stub this block is responsible for. It does not touch `DeleteCharacterPermanently`, whose equivalent dependency (§9.2, "equipped items") this block's own decomposition task (`ODY-S05-108`) already verified is closed by `ODY-S05-206`.

`ODY-S05-306` is the integration proof for the Equipment runtime block, mirroring `ODY-S05-207` for the Inventory runtime block. It should compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook.

If any of `ODY-S05-301`–`306` discovers that a stub or dependency cannot be fully closed without item-sourced abilities/effects (`ActiveEffect`) or another still-reserved block, that task must add an explicit follow-up task ID instead of leaving an unnamed TODO, per the same rule `ODY-S05-206` already followed for `RemoveBodyPart`'s own original deferral.

## 13. Ordered backlog (ItemDefinition migration block)

**Block status: complete** (`ODY-S05-401`–`404`, PR #130/#131/#132/#133) — pending product-owner review/merge of `ODY-S05-404`.

`ODY-S05-109` decomposes the next block after the Equipment runtime block's closure (`ODY-S05-301`–`306`, merged into `main`). This block implements `ADR-027` section 10's own fully-specified `ItemDefinition` migration preview/confirm workflow:

> **Decision:** ItemDefinition migration is a MainGM-only preview/confirm workflow over runtime item snapshots. It is not automatic publication side effect and not database schema migration.
>
> Workflow:
>
> 1. A new ItemDefinition version is published or selected as the migration target.
> 2. MainGM builds or refreshes `ItemDefinitionMigrationPreview`.
> 3. The system creates the required backup before migration review using `ADR-012`'s existing snapshot/`BackupRecord` mechanism.
> 4. Preview lists affected `ItemInstance` and mechanically identical `ItemStack` records, before/after snapshot changes, runtime-state compatibility checks, blocking issues, and required migration rules.
> 5. Confirmation requires current `SourceDefinitionRevision`, `AffectedInventoryRevision`, and `PreviewRevision`.
> 6. If revisions changed, host refreshes preview and requires new confirmation before starting the transaction.
> 7. If blocking incompatibilities remain, migration does not start.
> 8. Confirmed migration updates all matching item/stack snapshots in one `ADR-012` transaction and emits the required events/audit/report.
> 9. After successful migration there is no rollback command. A later correction is a new ItemDefinition version and another confirmed migration.
>
> Blocking incompatibilities include at minimum: removed ammo type currently loaded, removed equipment slot currently occupied, reduced capacity below current content, removed armor/body-part coverage with runtime damage, custom state the new definition cannot interpret, or hidden mechanics that cannot be safely compared.
>
> This workflow changes item mechanics snapshots only. It preserves runtime state and does not rewrite DomainEvents.

`ADR-027` section 11 draws a deliberate boundary this decomposition must not blur: existing `ActiveEffect` aggregates **never mass-migrate** to a new `EffectDefinition` (applying an effect captures a full snapshot; publishing a new `EffectDefinition` applies only to future ActiveEffects) — because ActiveEffects may be mid-duration, mid-combat, tied to turn timing, and stacking state that bulk rewriting would unpredictably disturb. `ItemDefinition` migration is possible only because items do not carry that same timing sensitivity. None of `ODY-S05-401`–`404` may generalize this preview/confirm mechanism to `ActiveEffect`.

`ODY-S05-109` verified directly against the tracked repository that this block starts from a genuine clean slate: no `ItemDefinitionMigrationPreview` type, apply command, persistence table, or any other migration-specific implementation exists anywhere today. The only existing references to `"ItemDefinitionMigration"` are two forbidden-fragment scope-guard test arrays (`SqliteInventoryRepositoryTests.cs`, `InventoryCreationServiceTests.cs`) that currently forbid such code from appearing — whichever of `ODY-S05-401`–`404` first introduces real migration-specific type/table names must narrow (not remove) those guards, the same pattern already used when `EquippedEntry`/`EquipmentCommandLedger` were introduced in `ODY-S05-301`/`302`.

`RulesetMigrationRules.BuildPlan`/`ComputePreviewHash` and `SqliteCharacterRepository.ApplyCharacterRulesetMigration` (`ODY-S04-113`, Character Ruleset-version migration) are the direct structural precedent for a preview/plan/apply separation with revision-guarded confirmation — cited by analogy only. That precedent's own concrete fields (`RulesetDefinitionMapping`, `RulesetUnresolvedDecision`, three `ExpectedXRevision` fields, `PreviewHash`) describe Character definition-category remapping, not item mechanics snapshots, and must be reinvented for this block's own domain, not copied.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-401` | In Review (PR #130) | `ADR-027` §10 steps 1-4 | Migration Preview Foundation | 109, 101-106, 201-207 | ExecPlan | A preview type (`ItemDefinitionMigrationPreview` or similarly named) and its builder, over already-Published source/target `ItemDefinition` versions and the runtime `ItemInstance`/`ItemStack` records they affect — modeled by analogy on `RulesetMigrationRules.BuildPlan`'s preview/plan shape, reinvented for item snapshots. Lists affected records and before/after snapshot changes. No apply, no `ADR-012` backup call, no blocking-incompatibility computation. |
| 2 | `ODY-S05-402` | In Review ([PR #131](https://github.com/odyssey-services/Odyssey_VTT/pull/131)) | `ADR-027` §10 (blocking incompatibilities) | Migration Blocking Incompatibility Rules | 401 | ExecPlan | The blocking-incompatibility computation named in §10's own list, including type-specific rules (Weapon: removed ammo type currently loaded; Armor: removed equipment slot currently occupied, removed armor/body-part coverage with runtime damage) and generic rules (reduced capacity below current content, unrecognizable custom state, uncomparable hidden mechanics). Computation feeding into the `ODY-S05-401` preview only — no apply. |
| 3 | `ODY-S05-403` | In Review (PR #132) | `ADR-027` §10 steps 5-9; §12 rules 1-2 | Migration Confirm/Apply Command | 401, 402 | ExecPlan | MainGM-only confirm/apply command: triple revision guard (`SourceDefinitionRevision`/`AffectedInventoryRevision`/`PreviewRevision`), mandatory `ADR-012` backup before migration review, refusal to start while any blocking incompatibility from `ODY-S05-402` remains, atomic update of every matching item/stack snapshot in one transaction with required events/audit, no rollback command after success (a later correction is a new version plus another confirmed migration). |
| 4 | `ODY-S05-404` | In Review (PR #133) | Roadmap-analog of `ODY-S05-207`/`306` | Migration Integration Fixtures | 401, 402, 403 | Brief plan | End-to-end integration proof mirroring `ODY-S05-207`/`306`: publish a new `ItemDefinition` version, build a preview against existing runtime items, discover a blocking incompatibility, resolve it, confirm, apply atomically, and verify the snapshots update while `DomainEvents` remain unrewritten. Composes existing surfaces only; no new production behavior unless an earlier task deliberately reserved a tiny fixture hook. |

### 13.1 ItemDefinition migration task boundaries

`ODY-S05-401` owns the preview type and its construction: which `ItemInstance`/`ItemStack` records a candidate migration would affect, and their before/after snapshot values. It must not compute blocking incompatibilities, call the `ADR-012` backup mechanism, or apply anything — those belong to later tasks.

`ODY-S05-402` owns the blocking-incompatibility rules named in `ADR-027` §10, including the type-specific ones (Weapon ammo, Armor slot/body-part coverage) alongside the generic ones (capacity, custom state, hidden mechanics). It computes a list of blocking issues against an `ODY-S05-401` preview; it does not build the preview itself and does not apply a migration.

`ODY-S05-403` owns the MainGM-only confirm/apply transition: the triple revision guard, the mandatory pre-review backup, atomic multi-snapshot update, and the explicit absence of a rollback command after success. It must not re-implement preview construction or blocking-rule computation — it consumes what `ODY-S05-401`/`402` already produce.

`ODY-S05-404` is the integration proof for the `ItemDefinition` migration block, mirroring `ODY-S05-207`/`306`. It should compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook.

## 14. Ordered backlog (Item-sourced abilities/effects — ADR foundation)

**Block status: ADR foundation only.** `ODY-S05-110` decomposes the next block after the `ItemDefinition` migration block's closure (`ODY-S05-401`–`404`). Unlike every prior block in this backlog, this one cannot be decomposed straight into implementation tasks the way `ODY-S05-107`/`108`/`109` decomposed theirs — `ADR-027` section 8 does not fully specify it.

`ADR-027` section 8, quoted verbatim in full:

> # 8. Item-sourced abilities and effects
>
> ## 8.1 CharacterAbility integration
>
> `SLICE-04` already introduced `CharacterAbility` with `SourceKind=Item` and `SourceKind=ActiveEffect`. This ADR does not add a parallel ability system.
>
> Rules:
>
> 1. Equipping or using an item may create or activate `CharacterAbility` entries with `SourceKind=Item` and a source item reference.
> 2. Unequipping, consuming, destroying, transferring away, or migrating an item must remove, suppress, or revalidate only those `CharacterAbility` entries whose source is that item, while permanent progression-purchased abilities remain unaffected.
> 3. The item remains the source of the ability. Character stores the ability instance/reference needed by Character mechanics and projections, not the item's whole mechanics snapshot.
>
> ## 8.2 ActiveEffect integration
>
> Applying an item effect creates an `ActiveEffect` aggregate using `EffectDefinitionRef` and `EffectMechanicsSnapshot` captured at application. The ActiveEffect source references the item/equipment/action that created it; the target references the affected Character, item, scene object, or other supported entity.
>
> Rules:
>
> 1. Character, ItemInstance, and SceneObject store only `ActiveEffect` references or derived projections.
> 2. Existing ActiveEffects are not owned by Inventory or Character, and do not mass-migrate when their source `EffectDefinition` changes.
> 3. Effects with duration `WhileItemEquipped` subscribe to authoritative `ItemEquipped`/`ItemUnequipped` events; they expire, suppress, or remove through ActiveEffect lifecycle commands/events, not by directly mutating Character or item snapshots.

Section 8 specifies only how `ActiveEffect` *integrates* with items (creation trigger, reference-only storage, `WhileItemEquipped` lifecycle coupling) — it never specifies the `ActiveEffect` aggregate itself: its persisted field shape, its repository contract, its stacking behavior against the already-existing `EffectStackPolicy` vocabulary, how non-`WhileItemEquipped` `EffectDurationType` values (`ForRounds`/`ForTurns`/`ForDuration`/`UntilSceneChange`/`UntilSessionEnd`/`UntilSourceTurnStart`/`UntilSourceTurnEnd`/`UntilTargetTurnStart`/`UntilTargetTurnEnd`/`WhileCondition`/`WhileSourceExists` — 11 of the 15 total values, per `TypedDefinitions.cs`'s own `EffectDurationType` enum) actually expire, who may create or remove an `ActiveEffect` directly (not only through an item), and what permission that requires. `ODY-S05-109` had `ADR-027` §10's own complete 9-step workflow to decompose directly into implementation tasks; this block has no equivalent complete specification to decompose against.

`ODY-S05-110` verified directly against the tracked repository that this gap is real, not assumed:

- No `ActiveEffect` class, struct, or interface exists anywhere in the codebase (repository-wide search).
- No ADR other than `ADR-027` mentions `ActiveEffect` at all (repository-wide search of `docs/adr/*.md`); `ADR-027` §8.2 and §11 (migration never mass-migrates `ActiveEffect`) are the only two normative references, and both explicitly describe `ActiveEffect` as integration/boundary text around a future aggregate, not the aggregate's own specification.
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs`'s `SourceKind` enum (`ODY-S04-108`) already reserves `SourceKind.ActiveEffect = 5`, with its own doc comment stating plainly that `Item`/`ActiveEffect`/`RulesetAdvancement` are "structurally accepted by `AcquireAbility`... but this task implements no automatic acquisition through them."
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`'s `EffectDefinition` (`ODY-S05-105`) already carries `TargetRule`/`DurationType`/`DurationValue`/`StackPolicy`/`MechanicsPayloadRef`, with its own doc comment stating `MechanicsPayloadRef` is "the snapshot-relevant mechanics placeholder `ADR-027` section 6's `DefinitionMechanicsSnapshot`/future `ActiveEffect.EffectMechanicsSnapshot` will eventually copy from -- no `ActiveEffect` aggregate or snapshot-copy mechanism is implemented by this task." `ItemDefinition.BuiltInAbilityRefs`/`BuiltInEffectRefs` carry the same "integration point for a future task" framing for their own two doc comments.

Per this backlog's own §4 discipline ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline"), decomposing this block straight into implementation tasks would force whichever task implements `ActiveEffect` first to silently decide this ADR-level architecture inline. `ODY-S05-110` instead decomposes this block into two tiers: one ADR-specification task (`ODY-S05-501`), decomposed now; and the block's remaining implementation tasks, deliberately **not** decomposed by this revision — reserved as a number range only, to be decomposed by a future backlog revision once `ODY-S05-501`'s own ADR is accepted, by direct analogy to how this whole block itself was "named, not scoped" in section 8 until today.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-501` | In Review (PR #135) | `ADR-027` §8 | ADR Addendum — ActiveEffect Aggregate Specification | 110, 401-404 | ADR task (produces a document; not code — outside `PLANS.md`'s own Brief/ExecPlan framework, which governs implementation planning) | A new ADR document (an addendum to `ADR-027` or a new `ADR-028`, the executor's choice, justified in the PR) specifying: the `ActiveEffect` aggregate's exact field shape and persistence/repository contract; how it integrates with the existing `EffectStackPolicy` vocabulary; how each `EffectDurationType` value (not only `WhileItemEquipped`) actually expires; the command/event for explicit early removal and who may issue it; whether `ActiveEffect` creation/removal requires MainGM-only authorization (matching `ADR-027` §12's migration precedent) or a broader, combat-usable right; and an explicit textual boundary against Block 3 (the full attack pipeline, roadmap §14.6) — this task specifies only the generic `ActiveEffect` mechanism and its item integration (`ADR-027` §8.1/§8.2), not combat/damage-sourced effects. No production code. |

### 14.1 Item-sourced abilities/effects task boundaries

`ODY-S05-501` owns the `ActiveEffect` aggregate specification only (ADR document, no code). It must not implement any runtime code, and it must not decompose the remaining implementation tasks of this block — that is a future backlog revision once this ADR is accepted.

`ODY-S05-501`'s own `ADR-028` is now accepted; `ODY-S05-502` onward is decomposed by `ODY-S05-111`, section 15 — the actual `ActiveEffect` persistence/runtime/lifecycle implementation tasks, `CharacterAbility SourceKind=Item` integration (`ADR-027` §8.1), and their own integration fixtures, mirroring the two-tier structure (foundation → persistence/commands → dependency closure → fixtures) every prior implementation block in this backlog already used.

## 15. Ordered backlog (Item-sourced abilities/effects — implementation)

`ODY-S05-111` decomposes the implementation range for this block now that `ADR-028` (`ODY-S05-501`) is `Accepted`. Unlike `ODY-S05-110`'s own two-tier decomposition (forced by `ADR-027` §8's own incompleteness), this revision can decompose straight into implementation tasks the same way `ODY-S05-107`/`108`/`109` did for their own, fully-specified blocks — `ADR-028` now supplies the complete specification `ADR-027` §8 alone did not.

This decomposition does not reopen anything `ADR-027` §8.1/§8.2 or `ADR-028` already decided. It only assigns each already-fixed decision to one implementation task:

- `ADR-027` §8.1/§8.2's own rules (item use may create `CharacterAbility`/`ActiveEffect`; Character/ItemInstance/SceneObject store only references; `WhileItemEquipped` subscribes to `ItemEquipped`/`ItemUnequipped`) are unchanged and are implemented, not redecided, by `ODY-S05-505`.
- `ADR-028`'s own aggregate shape/persistence (§5-6), `EffectStackPolicy` integration (§7), 9 presently-implementable `EffectDurationType` mechanisms out of 15 (§8 — `Instant`, `Permanent`, `UntilRemoved`, `WhileItemEquipped` [already solved by `ADR-027` §8.2], `UntilSceneChange`, `UntilSessionEnd`, `WhileCondition`, `WhileSourceExists`, `ForDuration`), `RemoveActiveEffect` (§9), permissions (§10), fail-closed expiry (§11), and mandatory `SqliteSavingPipeline` reuse (§12) are each assigned to exactly one task below.

**Out of scope for every task in this range, per `ADR-028` §13's own explicit boundary:** the six turn/round-based `EffectDurationType` values (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`, `UntilSourceTurnEnd`, `UntilTargetTurnStart`, `UntilTargetTurnEnd`) and any combat/damage-roll-triggered effect application. Both remain the full attack pipeline block's own territory (section 16) — no task in `502`–`507` may implement them in any form.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-502` | In Review (PR #137) | `ADR-028` §5/6/12 | ActiveEffect Foundation | 111, 501 | ExecPlan | The `ActiveEffect` domain aggregate (`ADR-028` §5's own minimum record and numbered rules — `ActiveEffectId`/`CampaignId`/`EffectDefinitionRef`/`EffectMechanicsSnapshot`/`SourceRef`/`TargetRef`/`Status`/`StackCount`/`AppliedByUserId`/`AppliedAt`/`ExpiresAt`/`Revision`), plus `IActiveEffectRepository`/`SqliteActiveEffectRepository` (`ADR-028` §6 — a standalone contract, not an `IInventoryRepository` extension) with create/read/list-by-`TargetRef`/list-by-`SourceRef` primitives, `Revision`-CAS-guarded, routed through `SqliteSavingPipeline` (`ADR-028` §12). No stacking logic, no expiry mechanism, no removal command — creation and basic persistence only. |
| 2 | `ODY-S05-503` | In Review (PR #138) | `ADR-028` §7 | Stacking Policy Resolution | 502 | ExecPlan | A pure function (or small function set) implementing all 7 `EffectStackPolicy` behaviors (`ADR-028` §7): `IndependentInstances`, `RefreshDuration`, `ReplaceIfStronger` (delegating the "stronger" comparison to `Odyssey.Rules`), `ReplaceExisting`, `IncreaseStacks`, `IgnoreNewApplication`, and `RequestGMResolution` (a new `ActiveEffectStackConflict` pending record + `ResolveActiveEffectStackConflict` command). Consumes `ODY-S05-502`'s own aggregate/repository; does not reimplement either. |
| 3 | `ODY-S05-504` | In Review (PR #139) | `ADR-028` §8/11 | Non-Combat Duration/Expiry | 502 | ExecPlan | The 8 presently-implementable `EffectDurationType` mechanisms (`ADR-028` §8's own table, excluding `WhileItemEquipped` and the 6 Block-3-reserved values): `Instant` (no persisted row), `Permanent`/`UntilRemoved` (no automatic expiry, ends only via `ODY-S05-506`'s own `RemoveActiveEffect`), `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists` (event-subscription expiry), `WhileCondition` (`Odyssey.Rules`-evaluated, fail-closed per `ADR-028` §11), and `ForDuration` (host-clock `ExpiresAt` check). Implements `ADR-028` §11's own fail-closed rule for every mechanism here. Does not touch `WhileItemEquipped` (`ODY-S05-505`) or any turn/round-based value (out of scope for this whole range). |
| 4 | `ODY-S05-505` | In Review (PR #140) | `ADR-027` §8.1/8.2; `ADR-028` §10 rule 1 | WhileItemEquipped Wiring + Item-Triggered Creation | 502 | ExecPlan | Implements `ADR-027` §8.2 rule 3's own already-decided mechanism via an explicit post-success reaction to `EquipmentService.Equip`/`Unequip` (no real `ItemEquipped`/`ItemUnequipped` event exists in this codebase, confirmed by direct search — see task `18`'s own decision log), `Suspended`/`Active` transitions per `ADR-028` §5.2 rule 3, and `ADR-027` §8.1/§8.2's own item-triggered `ActiveEffect` creation via `BuiltInEffectRefs` on equip, gated by the existing item-use permission model (`ADR-028` §10 rule 1 — no new MainGM restriction). `CharacterAbility`'s own analogous suspend/resume symmetry is explicitly deferred as `ODY-S05-505-F01` (no suspend/resume method exists on `ICharacterRepository`). |
| 5 | `ODY-S05-506` | In Review (PR #141) | `ADR-028` §9/10 rules 2-3 | RemoveActiveEffect Command + Direct-Creation Permission Gates | 502 | ExecPlan | The explicit `RemoveActiveEffect` command (`ADR-028` §9 — CAS-guarded, `Status → Removed`, never physical deletion, MainGM-only) and the MainGM-only gate for creating an `ActiveEffect` directly, not through any item (`ADR-028` §10 rules 2-3). Both are MainGM-only permission gates over the same aggregate, grouped together. |
| 6 | `ODY-S05-507` | In Review (PR #142) | Roadmap-analog of `ODY-S05-207`/`306`/`404` | Item-Sourced Abilities/Effects Integration Fixtures | 502-506 | Brief plan | End-to-end integration proof mirroring `ODY-S05-207`/`306`/`404`: publish an `EffectDefinition` → create an `ActiveEffect` through real item use → exercise one real `EffectStackPolicy` (e.g. `RequestGMResolution` with a real resolution) → exercise one real duration mechanism (e.g. `ForDuration` with a real expiry) → `WhileItemEquipped` suspend/resume through a real `Equip`/`Unequip` → `RemoveActiveEffect`, rejected for a non-MainGM actor and successful for MainGM. Composes existing surfaces only; no new production behavior unless an earlier task deliberately reserved a tiny fixture hook. |

### 15.1 Item-sourced abilities/effects implementation task boundaries

`ODY-S05-502` owns the `ActiveEffect` aggregate and its standalone repository/persistence foundation only. It must not implement stacking-policy resolution, any duration/expiry mechanism, or the removal command — those belong to later tasks in this range.

`ODY-S05-503` owns all 7 `EffectStackPolicy` behaviors, including the `RequestGMResolution` pending-conflict record and its resolution command. It consumes `ODY-S05-502`'s own aggregate/repository; it does not reimplement or extend either, and it does not touch duration/expiry or removal.

`ODY-S05-504` owns the 8 non-combat, non-`WhileItemEquipped` `EffectDurationType` mechanisms and the fail-closed expiry rule. It must not implement `WhileItemEquipped` (`ODY-S05-505`'s own job) or any turn/round-based value (out of this whole range's scope, per `ADR-028` §13).

`ODY-S05-505` owns wiring `ADR-027` §8.2 rule 3's own already-decided `WhileItemEquipped` mechanism to real `ItemEquipped`/`ItemUnequipped` events, and item-triggered `ActiveEffect`/`CharacterAbility` creation through `BuiltInEffectRefs`/`BuiltInAbilityRefs`. It must not add a new permission restriction beyond the existing item-use model, and it must not implement `RemoveActiveEffect` or direct (non-item) creation — those belong to `ODY-S05-506`.

`ODY-S05-506` owns the two MainGM-only permission gates this block introduces: explicit early removal, and direct (non-item) creation. It must not re-implement item-triggered creation (`ODY-S05-505`'s own job, which is deliberately *not* MainGM-gated) or any stacking/duration mechanism.

`ODY-S05-507` is the integration proof for the item-sourced abilities/effects implementation range, mirroring `ODY-S05-207`/`306`/`404`. It should compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook.

No task in `ODY-S05-502`–`507` may implement any of the six turn/round-based `EffectDurationType` values or any combat/damage-roll-triggered effect application — `ADR-028` §13 reserves both explicitly for the full attack pipeline block (section 16). If any of `502`–`507` discovers a dependency on that block it cannot defer cleanly, that task must add an explicit follow-up task ID instead of leaving an unnamed TODO, per the same rule `ODY-S05-206`/`301`–`306` already followed for their own forward deferrals.

## 16. Ordered backlog (Full attack pipeline — ADR foundation)

**Block status: ADR foundation accepted.** `ODY-S05-112` recorded the historical absence of a pipeline ADR; `ODY-S05-601` then produced accepted `ADR-029` (PR #144). The evidence below is historical context, not current coverage.

- No ADR (draft, numbered, or stub) for the full attack pipeline exists anywhere under `docs/adr/` (repository-wide directory listing).
- `ADR-027` and `ADR-028` each name the full attack pipeline only as a boundary they explicitly exclude from their own scope, never as something they specify: `ADR-027` §1 rule 13 ("This ADR does **not** implement... full attack pipeline..."), its own §13 "Non-goals" list and §18 "Deferred but not open here" list (both naming "full attack pipeline" explicitly), and `ADR-028` §13 ("Boundary with the full attack pipeline (Block 3)") and §14 ("the full attack pipeline and its own turn/round infrastructure") — all non-goals or deferrals, never specifications.
- `ADR-027` itself cites the ultimate source of the pipeline's own scope as `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` section 14 — but no `Documentation/` directory of any kind exists anywhere in this tracked repository (repository-wide directory listing). That roadmap file is not available to consult directly.
- The only place in this repository that actually enumerates the pipeline's own named parts is the closed, historical `docs/tasks/SLICE-05_BACKLOG.md` §3.2, quoted verbatim: "Roadmap section 14.6 names action intent, preview, range, modifiers, roll, hit, body part, armor, damage, costs, effect application, intervention, atomic apply, compensation, and game log. Those are full implementation and likely further ADR/task-decomposition work. `ADR-027` deliberately fixes only the item/equipment/content-catalog substrate needed before that pipeline can safely reference items, equipment, ammo, abilities, and effects." This is a secondary citation of the roadmap's own words, not a specification in its own right, and is the only textual anchor for pipeline scope this backlog can point to without inventing content from a document that is not in the repository.
- `ADR-028` §13 additionally names six specific `EffectDurationType` values (`ForRounds`, `ForTurns`, `UntilSourceTurnStart`, `UntilSourceTurnEnd`, `UntilTargetTurnStart`, `UntilTargetTurnEnd`) and "any combat/damage-roll-triggered effect application" as explicitly reserved for this block — the only place in any accepted ADR where the pipeline's own required integration with the already-accepted `ActiveEffect` mechanism is named at all.

Per this backlog's own §4 discipline ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline"), and following `ODY-S05-110`'s own precedent even more strongly (that block had partial ADR coverage to decompose around; this one has none), `ODY-S05-112` decomposes this block into two tiers: one ADR-specification task (`ODY-S05-601`), decomposed now; and the block's remaining implementation tasks, deliberately **not** decomposed by this revision — reserved as an open number range only, to be decomposed by a future backlog revision once `ODY-S05-601`'s own ADR is accepted.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-601` | Done (ADR-029 Accepted; PR [#144](https://github.com/odyssey-services/Odyssey_VTT/pull/144)) | `docs/tasks/SLICE-05_BACKLOG.md` §3.2; `ADR-028` §13/§14 | ADR: Full Attack Pipeline Specification | 112, 501-507 | ADR task (produces a document; not code — outside `PLANS.md`'s own Brief/ExecPlan framework, which governs implementation planning) | A new ADR document specifying the full attack pipeline vertical slice: the turn/round-structure infrastructure `ADR-028` §13 names as not-yet-existing (no accepted ADR introduces one today); the action-intent → preview → range → modifiers → roll → hit → body part → armor → damage → costs → effect-application → intervention → atomic-apply → compensation → game-log conveyor named by `docs/tasks/SLICE-05_BACKLOG.md` §3.2 (a secondary citation of the roadmap, not a specification the executor may treat as complete or final); and how this pipeline integrates with the already-accepted `ActiveEffect` mechanism (`ADR-028`) — at minimum the six reserved turn/round-based `EffectDurationType` values and `ADR-028` §13's own explicitly-undecided "how a combat roll decides to apply an effect" rule. If the executor genuinely needs the external roadmap document (`Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, not present in this repository) to specify this completely, that must be raised as an explicit open question to the product owner, not silently inferred. No production code. |

### 16.1 Full attack pipeline task boundaries

`ODY-S05-601` owns the full attack pipeline specification only (ADR document, no code). It must not implement any runtime code, and it must not decompose the remaining implementation tasks of this block — that is a future backlog revision once this ADR is accepted, by direct analogy to how `ODY-S05-501` owned only the `ActiveEffect` ADR and left `ODY-S05-502`–`507` to `ODY-S05-111`.

`ODY-S05-601` is accepted and `ODY-S05-602`–`608` are decomposed by `ODY-S05-113` in section 17; individual task contracts are created only on activation.

## 17. Ordered backlog (Full attack pipeline — implementation)

`ODY-S05-113` is Done (PR #145 merged) and maps accepted `ADR-029` seams without choosing Ruleset formulas or reopening `ADR-028`.

| Order | Task ID | Status | Roadmap/product source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S05-602` | Done (PR #146) | `ADR-029` §1 rules 1–2, §4 | Combat Encounter Timeline Foundation | 601, 113 | ExecPlan | Authoritative encounter, participants/order, round/turn lifecycle and command advancement; no attack evaluation. |
| 2 | `ODY-S05-603` | In Review (Draft PR #147) | `ADR-029` §1 rules 3–4, §5–§6 stages 1–11 | Attack Intent, Preview, and Evaluation | 602 | ExecPlan | Pure preview and host-recomputed Rules evaluation, RNG inputs, and `EffectApplicationDecision`; no persisted apply. |
| 3 | `ODY-S05-604` | In Review | `ADR-029` §1 rules 5–6, §6 stages 12–13 | Intervention and Atomic Attack Apply | 602, 603 | ExecPlan | Pending resolution, intervention command, no-reroll/idempotency, and one atomic transaction. |
| 4 | `ODY-S05-605` | In Review | `ADR-029` §7 | Combat Effect Duration Expiry | 602, 502 | ExecPlan | Six combat duration boundary mechanisms/bindings; no effect creation or `ADR-028` redesign. |
| 5 | `ODY-S05-606` | In Review | `ADR-029` §1 rule 8, §8 | Combat ActiveEffect Application | 603, 604, 605 | ExecPlan | Apply explicit combat effect decisions through existing ActiveEffect saving pipeline. |
| 6 | `ODY-S05-607` | In Review | `ADR-029` §1 rule 7/10, §6 stages 14–15 | Compensation and Game Log Projections | 604 | ExecPlan | Causally-linked compensation and redacted committed projections; never rollback history. |
| 7 | `ODY-S05-609` | In Review | `ADR-029` §1 rule 5, §6 stage 13, §12 item 4 | Attack Aggregate Delta Commit | 604 | ExecPlan | Applies accepted `AttackDelta` values to Character resource state, atomically inside the attack's own apply transaction (never the public `SetResourceCurrentValue`); item-targeted deltas are a disclosed, escalated blocker (no numeric field exists on `ItemInstanceRecord`). No new Ruleset formula. |
| 8 | `ODY-S05-610` | In Review | `ADR-028` §7 rule 7 | Combat Stacking Conflict GM Resolution | 606 | ExecPlan | Durable persistence and a real MainGM resolution command for `RequestGMResolution` stacking conflicts; closes `606`'s own disclosed gap. |
| 9 | `ODY-S05-608` | In Review | `ADR-029` §12 | Full Attack Pipeline Integration Fixtures | 602-607, 609 | Brief plan | End-to-end proof of all nine §12 outcomes without new production behavior. |
| 10 | `ODY-S05-611` | In Review | `ADR-029` §7 | Combat Duration Expiry Automatic Trigger Wiring | 605, 602 | ExecPlan | Gives `CombatEffectExpiryService.EvaluateAndExpireIfDue` a real automatic call site inside `SqliteCombatEncounterRepository.Advance`; closes `605`'s own disclosed deferred-wiring gap, reconfirmed still open by `608`. Final task of the full attack pipeline block. |

### 17.1 Full attack pipeline implementation task boundaries

`602` owns timeline; `603` owns pure preview/evaluation; `604` owns pending/intervention/atomic apply; `605` owns only combat durations; `606` owns only combat effect application; `607` owns compensation and redacted projections; `608` composes them. `604` prepares the committed Game Log entry and persists it in the same first atomic-apply transaction; `607` owns its audience-filtered projection/rendering and any compensation-log entry, so it does not move the initial log write outside atomic apply.

**Point clarification (product-owner-approved, 2026-09-14, see `ODY-S05-604`'s own task contract for the full finding and decision record):** `604`'s own "one atomic transaction" covers the pending/intervention outcome record, its idempotency row, and the committed Game Log entry only -- it deliberately does not include applying `AttackDelta` values to Character/Item state, although `ADR-029` §1 rule 5/§6 stage 13/§12 item 4 name that state as part of what atomic apply must commit. That gap is `ODY-S05-609`'s own job, not `604`'s or `608`'s.

**Point clarification (product-owner-approved, 2026-09-15, see `ODY-S05-606`'s own task contract for the full finding and decision record):** `606`'s own combat effect application deliberately does not give `ODY-S05-503`'s own `ActiveEffectStackConflict` (a `RequestGMResolution` stacking collision) any durable, cross-session persistence or GM resolution path -- that record has never had one anywhere in this codebase. `606` creates no row for that candidate and does not block the rest of the attack from committing; that gap is `ODY-S05-610`'s own job. `ODY-S05-608`'s own Definition of Done (`ADR-029` §12's nine items) does not name stacking-conflict resolution among the outcomes it must prove, so `608` is **not** made to depend on `610` -- unlike `609`, whose own gap directly blocked `608`'s own DoD item 4. If a future revision of `608`'s own scope decides otherwise, that is an explicit, separate decision, not an implicit consequence of this one.

`ADR-029` §11 is fully assigned: preview/recompute/RNG (`603`/`604`), durations (`605`), effects (`606`), atomicity/compensation (`604`/`607`), audience filtering (`607`), and integration (`608`). Rule 8 — leave `ODY-S05-602` onward for a separately approved backlog-decomposition task — is satisfied and owned by this `ODY-S05-113` decomposition itself: it creates exactly `602`–`608`, no child contracts, and no task outside the approved range. No task chooses Ruleset formulas or changes `ADR-028` ownership, stacking, non-combat duration, removal, or fail-closed rules.
