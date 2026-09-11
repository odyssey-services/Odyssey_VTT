# Odyssey VTT — SLICE-05 Content Catalog, Inventory, Items, Abilities, Effects, and Full Attack Implementation Backlog

**Status:** Implementation revision — OPEN. Content Catalog MVP (`ODY-S05-101`–`106`) complete; Inventory runtime block (`ODY-S05-201`–`207`) decomposed for the next implementation wave.
**Slice:** `SLICE-05 — Inventory, Items, Abilities, Effects, and Full Attack (implementation)`
**Parent task:** `docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md`
**Predecessor backlog:** `docs/tasks/SLICE-05_BACKLOG.md` (prerequisite ADR revision — `COMPLETE` as of `ODY-S05-002`/`ADR-027`; not rewritten by this document)
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-09-03
**Last updated:** 2026-09-06 UTC

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

Per section 3.1's explicit sequencing decision, the following `SLICE-05` blocks remain named and reserved but deliberately **not** decomposed into task IDs by this revision. Each becomes its own backlog revision-block once its prerequisites are accepted and closed, unless the product owner explicitly changes sequencing:

- **Item-sourced abilities/effects runtime** — `CharacterAbility SourceKind=Item` integration and future `ActiveEffect` aggregate creation (`ADR-027` section 8).
- **`ItemDefinition` migration preview/confirm** — MainGM workflow over runtime snapshots (`ADR-027` section 10).
- **Full attack pipeline** — roadmap section 14.6's action/preview/range/modifier/roll/hit/damage/effect-application vertical slice.

## 9. Global non-goals

This backlog revision excludes:

- campaign-specific custom content or per-campaign catalog overrides (section 3.2);
- a full visual node editor for content authoring;
- a marketplace or content-package distribution mechanism;
- `.odcontent` import/export implementation;
- a full, balanced MVP content pack (`106` is a proof fixture, not a content pack);
- implementing Inventory runtime under `ODY-S05-107` itself; section 7 only decomposes future implementation tasks;
- implementing Equipment runtime under `ODY-S05-108` itself; section 12 only decomposes future implementation tasks;
- Equipment runtime implementation beyond minimal location vocabulary needed by Inventory runtime tasks (section 8);
- item use, ActiveEffect execution, and the full attack pipeline (section 8);
- ItemDefinition migration workflow implementation (section 8);
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

## 11. Backlog change control

- New work requires a task contract; this document reserves numbers `ODY-S05-101` through `ODY-S05-106` for the completed Content Catalog MVP block, `ODY-S05-201` through `ODY-S05-207` for the completed Inventory runtime block, and `ODY-S05-301` through `ODY-S05-306` for the Equipment runtime block (section 12).
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
| 1 | `ODY-S05-301` | Proposed | `ADR-027` §5/7 | Equipment Runtime Foundation | 108, 201-207 | ExecPlan | Domain type(s) modeling `EquippedEntry` (or a typed extension of `InventoryLocationRef.Equipped` carrying the same fields) — `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision` — alongside the existing `EquipmentSlotRef` token. Establishes the one-place invariant (rule 1) and body-part-reference vocabulary only. No persistence schema, no Equip/Unequip commands, no `RemoveBodyPart` dependency check. |
| 2 | `ODY-S05-302` | Proposed | `ADR-027` §5/7/14 | Equipment Persistence Foundation | 301 | ExecPlan | SQLite schema/contracts for persisting `EquippedEntry`-shaped state, read/write primitives on `IInventoryRepository` or a sibling contract, optimistic-concurrency (CAS) pattern mirroring `ODY-S05-202`/`204`. No Equip/Unequip command semantics, no `RemoveBodyPart` check. |
| 3 | `ODY-S05-303` | Proposed | `ADR-027` §7 rules 1/2/4 | Equip Command MVP | 301, 302 | ExecPlan | MainGM-only command moving an item/stack from a valid contained location into an equipped location state, populating `EquipmentSlotRef`/`BodyPartRefs[]`/`EquippedByUserId`/`EquippedAt` with an atomic revision guard. Must enforce rule 4 (referenced body parts currently exist on the owning Character) and rule 1 (exclusive one-place location). No Unequip, no weapon/armor mechanical effects, no `RemoveBodyPart` check. |
| 4 | `ODY-S05-304` | Proposed | `ADR-027` §7 rule 3 | Unequip Command MVP | 301, 302, 303 | ExecPlan | MainGM-only command moving an equipped item/stack back into a valid contained (or other explicit) location per rule 3, with a symmetric revision guard, reusing `ODY-S05-204`'s move/location vocabulary where it applies. No new Equip semantics beyond the reverse transition, no `RemoveBodyPart` check. |
| 5 | `ODY-S05-305` | Proposed | `ADR-027` §7 rule 5; §9.1 | RemoveBodyPart Dependency Closure | 301, 302, 303, 304 | ExecPlan | Finally closes the `SqliteCharacterRepository.RemoveBodyPart` item/equipment-dependency stub (documented, not silent, since the original `SLICE-04` task and re-documented by `ODY-S05-206`): a real check for Equipment/item state depending on the body part being removed, rejecting the removal unless the same command atomically resolves the dependency. Does not touch `DeleteCharacterPermanently` (already closed by `ODY-S05-206`, see this section's intro). |
| 6 | `ODY-S05-306` | Proposed | Roadmap §14 | Equipment Runtime Integration Fixtures | 301-305 | Brief plan | Minimal integration fixtures proving the Equipment runtime block works end-to-end, mirroring `ODY-S05-207`: a Published weapon/armor definition creates a runtime item, Equip succeeds and blocks `RemoveBodyPart` on a dependent body part, Unequip succeeds, and `RemoveBodyPart` then succeeds. Composes existing surfaces only; no new production behavior unless an earlier task deliberately reserved a tiny fixture hook. |

### 12.1 Equipment runtime task boundaries

`ODY-S05-301` owns the domain vocabulary for equipped-location state: the `EquippedEntry` shape (or equivalent typed extension of `InventoryLocationRef.Equipped`), `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision`. It must not introduce persistence schema or commands whose behavior belongs to later tasks.

`ODY-S05-302` owns persistence, idempotency, and optimistic concurrency for Equipment state. It must keep schema/contracts separate from the Equip/Unequip command semantics themselves.

`ODY-S05-303` owns the Equip transition and rule 4's body-part-existence check. It must not implement Unequip, weapon/armor mechanical effects, or the `RemoveBodyPart` check.

`ODY-S05-304` owns the Unequip transition and rule 3's valid-destination check. It must not re-implement Equip beyond the symmetric reverse transition.

`ODY-S05-305` owns closing the real `RemoveBodyPart` item/equipment-dependency stub (rule 5; `ADR-027` §9.1) — the one `SLICE-04` stub this block is responsible for. It does not touch `DeleteCharacterPermanently`, whose equivalent dependency (§9.2, "equipped items") this block's own decomposition task (`ODY-S05-108`) already verified is closed by `ODY-S05-206`.

`ODY-S05-306` is the integration proof for the Equipment runtime block, mirroring `ODY-S05-207` for the Inventory runtime block. It should compose existing surfaces and avoid new production behavior unless an earlier task deliberately reserved a tiny fixture hook.

If any of `ODY-S05-301`–`306` discovers that a stub or dependency cannot be fully closed without item-sourced abilities/effects (`ActiveEffect`) or another still-reserved block, that task must add an explicit follow-up task ID instead of leaving an unnamed TODO, per the same rule `ODY-S05-206` already followed for `RemoveBodyPart`'s own original deferral.
