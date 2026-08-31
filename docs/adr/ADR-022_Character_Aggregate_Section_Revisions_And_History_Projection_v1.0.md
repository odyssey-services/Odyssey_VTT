# ADR-022 — Character Aggregate, Section Revisions, and History Projection

**Документ:** `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`  
**ADR:** ADR-022  
**Версия:** 1.0  
**Дата:** 30 августа 2026 года  
**Статус:** Accepted  
**Область:** Character aggregate boundary, section revisions and narrow write locks, minimum Character event historical snapshots, and `CharacterHistoryProjection` as a rebuildable projection over the append-only journal  
**Связанные этапы:** Roadmap Stage 5 (`SLICE-04`), Milestone `M5`, backlog `ODY-S04-001`  
**Базовые документы:** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.3/§13.8/§13.9, `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` Character aggregate, editing/concurrency, history, persistence, networking, and readiness sections, `Documentation/04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` §40, `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` Character/Progression sections, `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`, `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `docs/adr/ADR-013_Migration_Runner_v1.0.md`, `docs/tasks/SLICE-04_BACKLOG.md`

---

# 1. Решение

Odyssey VTT фиксирует `Character` как единый campaign-authoritative aggregate root with several independently revised sections inside that aggregate, not as several independent aggregate roots joined only by `CharacterId`. Character history remains a projection over authoritative state and `ADR-012` DomainEvents, never a second mutable source of truth.

Обязательные решения:

1. **Aggregate boundary:** `Character` is one aggregate root for the persistent campaign Character. Identity, presentation, custom fields, ownership reference state, creation info, mechanics reference state, runtime state, visibility policy, lifecycle status, approval state, aggregate revision, and per-section revisions are inside one authoritative boundary (section 4). Persistence may store these sections in multiple tables, but Application loads and validates the Character as one aggregate when a command requires cross-section invariants.
2. **Section revisions:** the aggregate has one monotonic `CharacterRevision` and a fixed set of section revisions: `IdentityRevision`, `PresentationRevision`, `CustomFieldsRevision`, `MechanicsRevision`, `AttributeValuesRevision`, `CharacterSkillsRevision`, `CharacterAbilitiesRevision`, `CharacterResourcesRevision`, `CharacterAnatomyRevision`, `OwnershipRevision`, `LifecycleRevision`, and `RuntimeStateRevision` (section 5). A command declares exactly the section revision(s) it depends on. Unrelated sections can be edited in parallel because their expected revisions are checked separately.
3. **Section locks:** locks are narrow, temporary write gates over section keys, not whole-sheet locks by default (section 6). The minimum section keys are `Identity`, `Presentation`, `CustomFields`, `Mechanics`, `AttributeValue:<AttributeDefinitionId>`, `CharacterSkill:<SkillDefinitionId>`, `CharacterAbility:<CharacterAbilityId>`, `CharacterResource:<CharacterResourceId>`, `CharacterAnatomy`, `Ownership`, `Lifecycle`, and `RuntimeState`. A command may take several locks only when its invariant spans those sections.
4. **Draft review and revalidation locks:** submit/review/approve, progression validation, duplicate command handling, lifecycle changes, and reconnect do not introduce a separate locking system. They reuse expected section revisions, `ADR-002` `CommandId` idempotency, and the narrow locks from section 6. Duplicate commands are deduplicated by `AppliedCommands`, not by locks.
5. **Character event snapshots:** every Character event payload contains enough immutable historical data to render history after later rename, portrait change, archive, death, restore, or physical delete: `CharacterId`, `CharacterKind`, `LifecycleStatusAfter?`, `DisplayNameSnapshot`, `PortraitReferenceSnapshot?`, `RulesetRef?` or `RulesetVersion?` when mechanics are involved, affected section key(s), previous and new section revisions, relevant before/after value snapshots for the changed field(s), and event-specific references such as source template, command, compensation, or evidence IDs when applicable (section 7). A full Character sheet copy in every event is prohibited.
6. **Projection contract:** `CharacterHistoryProjection` is a rebuildable, audience-filtered/read-authorized projection from the append-only Domain Event Store plus current authorized Character projection, committed in the same transaction as the Character state/event when maintained eagerly (section 8). It has no independent mutation command, no independent lifecycle, and no authority to change Character state.
7. **Reconnect contract:** reconnect returns the current authorized Character projection and allowed history entries through the existing `ADR-017` snapshot/delta path, ordered by `ADR-012` `EventSequence`, with section revisions sufficient for the client to detect conflicts and refresh local forms (section 9). Reconnect never trusts a client's local CharacterHistory copy as authoritative state.
8. This ADR does **not** decide Draft/template approval architecture (`ADR-023`), DevelopmentPool/economy transactions (`ADR-024`), ownership/lifecycle/ruleset-migration operations (`ADR-025`), ability/resource/anatomy mechanics beyond their section revision membership, or any production code/schema/test implementation.

This ADR is the normative authority for the Character aggregate/revision/history boundary. It specializes `ADR-002`, `ADR-003`, `ADR-012`, and `ADR-013`; it does not replace their generic command, serialization, journal, snapshot, or database schema migration rules.

---

# 2. Контекст и проблема

`SLICE-04` introduces the first full Character and progression lifecycle. The product documents already name `CharacterKind`, lifecycle states, identity, presentation, custom fields, owner/co-owner/controller concepts, section revisions and locks, and `CharacterHistoryProjection`. Existing ADRs provide the shared substrate:

- `ADR-002` fixes command/event/idempotency, aggregate revisions, and atomic event batches.
- `ADR-003` fixes versioned DTOs and prohibits direct Domain aggregate serialization.
- `ADR-012` fixes append-only DomainEvents, transactionally updated current-state projections, command receipts, and compensating events.
- `ADR-013` fixes database schema migration and explicitly separates schema migration from ruleset migration.

Those ADRs do not answer four Character-specific questions that implementation must not invent ad hoc:

1. whether a campaign Character is one aggregate with section revisions or many independently authoritative records;
2. which section locks exist at the authoritative boundary;
3. which Character event snapshots are required so history remains renderable after later Character changes;
4. how `CharacterHistoryProjection` remains a projection rather than a second source of truth, including reconnect behavior.

This ADR answers only those questions.

---

# 3. Термины

## 3.1 `Character`

The campaign-authoritative aggregate root representing one PlayerCharacter, NPC, or Creature. Vehicles, mechs, doors, traps, and other interactive objects are not Characters, though they may reuse compatible components in their own aggregates.

## 3.2 Section

A named sub-area of a Character that can be independently revision-checked and, when needed, locked for a short authoritative write operation. A section is not a separate aggregate root.

## 3.3 Section revision

A monotonic revision for one Character section. It is used by commands as optimistic concurrency evidence. Section revision changes are always accompanied by the enclosing `CharacterRevision` changing in the same transaction.

## 3.4 Section lock

A short-lived authoritative write gate for one Character section or one addressed mechanics entry. Locks prevent conflicting commands from committing while an operation requiring a stable section is pending. Locks are durable enough to survive host process recovery when they protect a durable pending operation; ordinary synchronous commands may use transaction-scoped locks only.

## 3.5 Historical value snapshot

The event payload subset needed to render the historical fact later without consulting mutable current fields that may have changed or been removed. It is not a full aggregate snapshot and does not replace current-state projection or database backup.

## 3.6 `CharacterHistoryProjection`

A read model derived from DomainEvents and current authorization/projection state. It groups and redacts Character-significant history entries for UI/reconnect/search surfaces, but it never owns Character state.

---

# 4. Character aggregate boundary

**Decision:** Character is one aggregate root with several revised sections inside it.

The aggregate owns these conceptual areas:

```text
Character
├── CharacterId
├── CampaignId
├── CharacterKind
├── LifecycleStatus
├── ApprovalState
├── Identity
├── Presentation
├── Ownership
├── CreationInfo
├── Mechanics
├── RuntimeState
├── CustomFieldValues
├── VisibilityPolicy
├── CreatedAt
├── UpdatedAt
├── CharacterRevision
└── SectionRevisions
```

Persistence may normalize this data across tables for indexing and migration safety. That physical storage choice does not split authority: Application command handlers still treat the affected Character plus required sections as one aggregate boundary for invariant checks. Examples:

- approving a submitted Character may update lifecycle, approval, identity validation state, creation info, ownership, and history atomically;
- a mechanics edit may require a mechanics section revision, a specific attribute/skill/resource revision, and the current lifecycle status;
- a lifecycle transition may require lifecycle, runtime, and visible history snapshots together.

This is intentionally not full event sourcing. `ADR-002` and `ADR-012` already choose current state plus append-only journal. The Character current-state projection is authoritative together with its event journal and command receipt transaction.

Why not several independent aggregate roots? Because `SLICE-04` needs cross-section invariants: only an approved active Character appears in the campaign; historical snapshots must reflect the Character identity visible at the time of the event; lifecycle/archive/dead state constrains mechanics edits; reconnect must deliver one coherent Character projection. Splitting identity, presentation, mechanics, and lifecycle into unrelated aggregates would push these invariants into orchestration glue and increase the chance of partially valid Character state.

Parallel editing is still supported by section revisions. A biography edit does not conflict with a resource recovery merely because both belong to the same Character. It conflicts only when the command's expected section revisions no longer match or a required section lock is held.

---

# 5. Section revisions

Every Character has:

```text
CharacterRevision
```

and these first-version section revisions:

```text
IdentityRevision
PresentationRevision
CustomFieldsRevision
MechanicsRevision
AttributeValuesRevision
CharacterSkillsRevision
CharacterAbilitiesRevision
CharacterResourcesRevision
CharacterAnatomyRevision
OwnershipRevision
LifecycleRevision
RuntimeStateRevision
```

`CharacterRevision` changes for any committed Character state change. The relevant section revision(s) change only for the section(s) modified by that command. A mechanics-wide revision changes when a command changes mechanics-level metadata such as pinned ruleset reference, derived value snapshot, or active effect references. Entry-level mechanics revisions may additionally change for addressed entries such as one attribute, skill, ability, or resource.

Commands must declare expected revisions with enough granularity for their invariant:

```text
ExpectedCharacterRevision?           -- only when the command needs the whole Character stable
ExpectedSectionRevisions[]           -- normal path
ExpectedMechanicsEntryRevisions[]    -- for addressed mechanics entries
```

Rules:

1. A command that edits only one section checks that section's expected revision and the lifecycle status needed for legality.
2. A command that depends on several sections lists all required section revisions.
3. A stale unrelated section does not reject the command.
4. A stale required section rejects with `CharacterRevisionConflict` or the task-specific safe error mapped through `ADR-004`.
5. A duplicate command with the same `CommandId` returns the stored result through `ADR-002`/`ADR-012`; it does not re-check revisions and does not reapply the effect.
6. A duplicate command with the same `CommandId` but different actor or semantic payload is a command identity mismatch under `ADR-002`, not a Character section conflict.

This model is the minimum needed to satisfy both constraints from the product docs: unrelated edits can proceed in parallel, and duplicate commands do not spend or mutate twice.

---

# 6. Section locks

Section locks are write locks over the smallest section that protects the command's invariant.

Minimum section lock keys:

```text
Identity
Presentation
CustomFields
Mechanics
AttributeValue:<AttributeDefinitionId>
CharacterSkill:<SkillDefinitionId>
CharacterAbility:<CharacterAbilityId>
CharacterResource:<CharacterResourceId>
CharacterAnatomy
Ownership
Lifecycle
RuntimeState
```

Required behavior:

1. Whole-Character lock is not the default. It is allowed only for commands whose invariant genuinely spans most sections, such as physical delete preparation, full restore/migration preview, or future tooling explicitly justified by an ADR/task.
2. Locks are never the primary idempotency mechanism. Duplicate prevention belongs to `CommandId`/`AppliedCommands`.
3. Locks are never the primary history mechanism. Committed facts belong to `DomainEvents`.
4. Locks may be transaction-scoped for synchronous commands and durable for pending workflows that must preserve a stable section between preview and answer.
5. A durable lock records at minimum `CharacterId`, `SectionLockKey`, `LockOwnerCommandId` or `PendingInteractionId`, `ActorUserId?`, `ReasonCode`, `CreatedAtHost`, and `ExpiresAtHost?` when a timeout policy exists. Host clock rules follow `ADR-008`.
6. A command encountering a held required section lock rejects or waits only according to its own command contract. Silent overwrite is forbidden.
7. Read operations do not require section locks. They use current authorized projection and revisions.

Draft submit/review/approve uses locks only around the sections it commits into the campaign Character. Progression preview/revalidation uses the addressed mechanics entry locks plus any required mechanics-wide lock. Ownership/lifecycle operations use ownership/lifecycle locks and are further specified by `ADR-025`; this ADR only defines the available lock keys.

---

# 7. Character event historical snapshots

Character events must be renderable later even if the Character is renamed, its portrait changes, it is archived/dead/restored, or a dependency is physically removed according to a future approved operation. Therefore Character-significant DomainEvent payloads include a minimum historical snapshot.

Required common fields for Character-significant events:

```text
CharacterId
CharacterKind
DisplayNameSnapshot
PortraitReferenceSnapshot?
AffectedSectionKeys[]
PreviousCharacterRevision?
NewCharacterRevision
PreviousSectionRevisions[]
NewSectionRevisions[]
RulesetRef? or RulesetVersion?       -- required for mechanics/rules events
LifecycleStatusBefore?
LifecycleStatusAfter?
RelevantValueSnapshots[]
```

`RelevantValueSnapshots[]` contains only the values needed to understand the event: for example old/new display name for rename, old/new base attribute value for attribute change, old/new resource current/maximum for resource change, old/new anatomy part state for anatomy change, or old/new lifecycle status for state changes. It does not contain the full Character sheet unless the specific future command contract explicitly requires a compact before/after snapshot for a high-risk compensation or migration operation.

Rules:

1. Stored event payload bytes are immutable and versioned according to `ADR-003`.
2. Event order and rebuild order use `ADR-012` `EventSequence`, never timestamps.
3. Current display fields may be used to render current Character lists, but historical entries render from event snapshots when the event describes the past.
4. Full database snapshot/backup remains the `ADR-012` mechanism. Event snapshots are not backups.
5. Event payloads must not include hidden fields beyond the event's authorized/full DomainEvent needs; client-visible history is built by projection/redaction, not by truncating the stored event.

This is the minimum that satisfies the product requirement that historical events show the old name/snapshot after later changes, while avoiding a full Character copy in every event.

---

# 8. `CharacterHistoryProjection` contract

`CharacterHistoryProjection` is a materialized/read-time projection, not a source of truth.

Allowed inputs:

```text
DomainEvents ordered by EventSequence
Current Character projection/state tables
Visibility/permission policy
Campaign membership and role/control data needed for redaction
```

Forbidden inputs:

```text
Client local history cache as authority
GameLogEntry as authority for Character state
Manual writes that do not correspond to a DomainEvent
Independent "edit history entry" commands
```

When maintained eagerly, Character state changes, related DomainEvent(s), relevant GameLog/history rows, outbox entries, and `AppliedCommands` result are committed in the same transaction required by `ADR-012`. When rebuilt lazily, the projection must be reproducible from the same ordered events and current authorization inputs. Both implementations are valid if they produce the same authorized result for the same `EventSequence` boundary.

Projection rows may store denormalized display text, grouping keys, search-safe fields, redaction status, source `DomainEventId`, `EventSequence`, `CharacterId`, affected section key, and visibility/audience metadata needed for efficient reads. They must not store independently mutable Character values.

If a projection row is missing, stale, or corrupt, recovery is rebuild from the append-only journal plus current authorized Character projection. Recovery must not invent compensating events or mutate Character state merely to repair the read model.

---

# 9. Reconnect behavior

Reconnect uses the existing snapshot/delta model from `ADR-017` and the journal/projection guarantees from `ADR-012`.

For a reconnecting user, the host sends:

1. the current authorized Character projection;
2. section revisions needed to validate or reject local forms;
3. authorized `CharacterHistoryProjection` entries after the client's acknowledged sequence, or a resync snapshot when the delta range is unavailable;
4. projection removals/hiding for entries or fields no longer visible to that user.

Rules:

1. The host orders deltas by `EventSequence`.
2. The client may keep local unsubmitted forms, but commit requires current section revision validation.
3. The client's cached history never becomes authoritative evidence that an event still exists or is still visible.
4. Reconnect may rebuild missing history rows before sending the snapshot/delta, but rebuild is a projection maintenance operation, not a new DomainEvent.
5. If authorization changes between disconnect and reconnect, the returned history is filtered by current permission/audience rules. Hidden existence is not confirmed beyond the safe denial rules already required by permissions ADRs.

This satisfies the `SLICE-04` vertical-slice requirement that history and reconnect show authoritative state without adding a Character-specific sync channel.

---

# 10. Не входит в ADR-022

Явно исключено из объёма этого ADR:

- **Drafts/templates/approval workflow architecture** — future `ADR-023` (`ODY-S04-002`).
- **Development economy, points, purchases, critical evidence, advancement revert, and respec** — future `ADR-024` (`ODY-S04-003`).
- **Full ownership/control/lifecycle operation contract, physical delete, Dead/restore, and Character Ruleset migration** — future `ADR-025` (`ODY-S04-004`).
- **Ability/resource/anatomy mechanics design** beyond their section revision/lock membership — product specs and Rules Engine already cover the mechanics fork.
- **Concrete database schema** for Character tables — implementation task under `ADR-003`, `ADR-011`, `ADR-012`, and this ADR.
- **Concrete command/event payload DTO files, tests, Unity UI, character sheet layout, `.odchar` import/export, or content catalogs**.
- **Real internet transport work** — remains separate under earlier networking tasks and blocked items.

---

# 11. Соответствие module boundaries (`ADR-001`) and existing ADRs

This ADR does not introduce code, but future implementation must preserve these boundaries:

- `Odyssey.Domain` owns Character aggregate invariants, section revision semantics, and domain event payload semantics. It remains serializer-free and Unity-free.
- `Odyssey.Rules` owns deterministic derived/effective value calculations used by Character commands. It does not commit state or write history.
- `Odyssey.Application` owns Character command handlers, permission/revision/lock checks, transaction orchestration, and `CharacterHistoryProjection` rebuild/refresh ports.
- `Odyssey.Persistence` owns the physical Character tables, DomainEvents storage, projection table implementation, applied commands, and transaction mechanics. It does not decide whether a Character command is legal.
- `Odyssey.Networking` owns transport projection delivery/redaction/reconnect adapters. It does not receive raw DomainEvents as unrestricted client payloads.
- `Odyssey.Unity.Client` owns local forms, previews, and rendering. It does not store authoritative Character state.

Relationship to existing ADRs:

- `ADR-002` remains authoritative for command identity, idempotency, event batches, aggregate revision checks, and duplicate command behavior.
- `ADR-003` remains authoritative for versioned DTOs, event payload bytes, upcasters, canonical JSON, and the prohibition on direct Domain aggregate serialization.
- `ADR-012` remains authoritative for append-only DomainEvents, journal/projection transaction boundaries, snapshots, command receipts, and compensation.
- `ADR-013` remains authoritative for database schema migration. Character Ruleset migration is intentionally left to `ADR-025`.
- `ADR-017` remains authoritative for snapshot/delta/reconnect transport shape.

---

# 12. Правила для Codex

Codex обязан:

1. Implement Character as one authoritative aggregate root with section revisions inside that aggregate; do not create independent authoritative roots for identity, presentation, mechanics, or history under this ADR.
2. Use section revisions to allow unrelated edits in parallel; do not require whole-Character expected revision unless the command invariant truly spans the whole Character.
3. Use the lock keys from section 6; do not introduce broad whole-sheet locks for ordinary biography, resource, attribute, skill, presentation, or custom field edits.
4. Treat locks as concurrency gates only; never as idempotency receipts or historical facts.
5. Include the minimum historical snapshots from section 7 in Character-significant events; do not store a full Character sheet in every event.
6. Build or rebuild `CharacterHistoryProjection` from `DomainEvents`/current projection inputs; do not add commands that mutate history independently of events.
7. Use `ADR-012` transaction boundaries when maintaining projection rows eagerly.
8. Use `ADR-017` reconnect/snapshot/delta paths; do not add a Character-specific sync channel.
9. Do not implement Draft/template, DevelopmentPool, ownership/lifecycle, Dead/restore, physical delete, Ruleset migration, or production schema/code under this ADR task unless a later task explicitly scopes it.

---

# 13. Definition of Done для будущей implementation-задачи

Implementation tasks using this ADR must prove, with tests where applicable:

1. Editing identity and recovering a resource on the same Character can proceed in parallel when each command's expected section revision is current for its own section.
2. Two conflicting edits to the same addressed section or mechanics entry produce a revision conflict or section-lock rejection without partial state change.
3. A duplicate Character command with the same `CommandId` returns the stored result and does not reapply section revisions, mechanics changes, resource spend, or history entries.
4. Character-significant events include `DisplayNameSnapshot` and relevant before/after value snapshots; a historical entry still renders the old display name after the Character is renamed.
5. A `CharacterHistoryProjection` rebuild from DomainEvents produces the same authorized entries as the eagerly maintained projection for the same `EventSequence` boundary.
6. Reconnect returns current authorized Character projection, current section revisions, and authorized history entries/deltas ordered by `EventSequence`.
7. Projection repair does not create DomainEvents, mutate Character state, or use GameLogEntry as source of truth.
8. Core Character aggregate and history logic compile without Unity dependencies in the pure .NET path.

---

# 14. Рассмотренные альтернативы

## 14.1 Single aggregate root with section revisions

Accepted. It keeps Character invariants in one authoritative boundary while still allowing unrelated edits to proceed through section revision checks. It matches the product model, which names `Character` as the aggregate root and treats history as a projection.

## 14.2 Multiple independently versioned records joined by `CharacterId`

Rejected. It looks attractive for parallel editing, but it pushes cross-section invariants into orchestration glue and makes approval, lifecycle, history snapshots, reconnect, archive/dead state, and mechanics/lifecycle checks easier to commit partially or inconsistently. Physical normalization remains allowed in Persistence, but not independent authority.

## 14.3 Optimistic concurrency only through revisions

Partially accepted. Section revisions are the normal concurrency mechanism and the only mechanism needed for most synchronous edits. They are not enough for durable pending workflows that require a section to remain stable between preview and answer; those workflows need narrow locks.

## 14.4 Explicit section locks as the primary concurrency mechanism

Rejected as the default. Broad or routine locking would violate the roadmap requirement that unrelated edits proceed in parallel and would turn ordinary form editing into a host-held lock problem. Locks are reserved for short authoritative writes and durable pending operations.

## 14.5 Whole-Character lock for every edit

Rejected. It is simpler to implement but fails the parallel editing requirement and would make biography/presentation edits block unrelated resource or mechanics updates.

## 14.6 Full Character copy in every event

Rejected. It would preserve history rendering but bloats the journal, risks leaking fields into payloads where only a small historical value is required, and duplicates current-state projection/backup responsibilities already covered by `ADR-012`.

## 14.7 Delta-only event payloads with no historical display snapshots

Rejected. A later rename, portrait change, archive, or physical delete could make historical entries impossible to render honestly. Character events need small historical snapshots for the fields relevant to the event.

## 14.8 `CharacterHistoryProjection` materialized at every write

Partially accepted. Eager materialization is allowed when it is committed in the same transaction as state/events. It is not required as the only implementation because a rebuildable/read-time projection can produce the same result from the journal.

## 14.9 `CharacterHistoryProjection` generated on every read only

Partially accepted. It is valid if it produces the same authorized result from `EventSequence`-ordered events and current permission inputs. It may be too slow for all UI/history/search surfaces; implementation may choose eager materialization without changing the source-of-truth contract.

## 14.10 Independently mutable persisted history table

Rejected. It would create a second source of truth and directly conflict with `ADR-012` append-only journal and compensation rules.

---

# 15. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- exact Draft/template approval contracts are `ADR-023`;
- DevelopmentPool and progression economy contracts are `ADR-024`;
- ownership/lifecycle/Ruleset migration operation contracts are `ADR-025`;
- concrete Character table schema and DTO implementation belong to later implementation tasks.

---

# 16. Трассировка

ADR реализует и уточняет:

- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.3 Character foundation, §13.8 steps 5 and 10, and §13.9 parallel edits/history/reconnect-related criteria;
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` Character aggregate, editing/concurrency, history, persistence, networking/reconnect, and readiness criteria;
- `Documentation/04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md` §40, for stored vs computed Character mechanics and current anatomy/resource revision use;
- `Documentation/03_Domain_Model_Odyssey_VTT_v0.25.md` Character aggregate, Character mechanics, historical snapshot, CharacterHistory, command/event envelopes, and projection sections;
- `docs/tasks/SLICE-04_BACKLOG.md` §3.1, closing the first prerequisite ADR slot.

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency/revision foundations;
- `ADR-003` for versioned durable DTOs and immutable event payload bytes;
- `ADR-012` for append-only journal, journal/projection transactions, snapshots, and command receipts;
- `ADR-013` for database schema migration and the separation from ruleset migration;
- `ADR-017` for reconnect snapshot/delta delivery.

Related future tasks:

```text
ODY-S04-002  ADR-023: Character Drafts, Templates, and Approval Workflow
ODY-S04-003  ADR-024: Development Economy and Progression Transactions
ODY-S04-004  ADR-025: Character Ownership, Lifecycle, and Ruleset Migration Operations
```

---

# 17. Нормативное действие

Принято как ADR этой задачи (`ODY-S04-001`) without technical spike. Rationale: the task resolves model/contract boundaries over already accepted command, journal, projection, serialization, and migration substrates; no empirical unknown is visible before implementation.

С даты принятия (`Accepted`):

- `SLICE-04` Character implementation tasks must model campaign Character as one aggregate root with section revisions inside the aggregate;
- implementation may normalize physical storage but must not turn Character sections or CharacterHistory into independent authoritative roots;
- section revision and lock behavior from sections 5 and 6 is the baseline for Character concurrency;
- Character event payloads must include the minimum historical snapshots from section 7;
- `CharacterHistoryProjection` must remain rebuildable from authoritative events/current projection inputs and cannot accept independent mutation commands;
- reconnect behavior for Character/history must use the existing `ADR-017`/`ADR-012` mechanisms;
- changing this aggregate/revision/history contract requires an amendment or superseding ADR, not silent implementation drift.

---

**Конец документа**
