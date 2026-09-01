# ADR-025 — Character Ownership, Lifecycle, and Ruleset Migration Operations

**Документ:** `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md`  
**ADR:** ADR-025  
**Версия:** 1.0  
**Дата:** 31 августа 2026 года  
**Статус:** Accepted  
**Область:** Character-specific owner/co-owner/controller semantics поверх `ADR-019`'s baseline, archive/dependency-aware physical delete, Dead/`CharacterRestored` invariants, and Character Ruleset migration preview/snapshot/rollback (including `.odchar` import interaction)  
**Связанные этапы:** Roadmap Stage 5 (`SLICE-04`), Milestone `M5`, backlog `ODY-S04-004`  
**Базовые документы:** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.7/§13.9, `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-007`/`008`/`010`), §19, §22–25, `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`, `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`, `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md`, `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`, `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `docs/adr/ADR-013_Migration_Runner_v1.0.md`, `docs/tasks/SLICE-04_BACKLOG.md`

---

# 1. Решение

Odyssey VTT fixes Character ownership, lifecycle-boundary operations (archive, physical delete, Dead, restore), and Ruleset migration as specializations of `ADR-019`'s existing role baseline and `ADR-022`'s existing aggregate sections — not as a new consistency mechanism, a new role model, or a mechanism parallel to `ADR-013`'s schema migration runner.

Обязательные решения:

1. **Owner/co-owner/controller (section 4):** `CharacterOwnership` (`PrimaryOwnerUserId`, `CoOwnerUserIds`, `PermanentControllerUserIds`, `TemporaryControlGrants`) lives inside `ADR-022`'s already-reserved `Ownership` section/lock/`OwnershipRevision` — no new section is introduced. `AssignPrimaryOwner`/`AddCharacterCoOwner`/`RemoveCharacterCoOwner`/`GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl` are all `Character.ManageOwnership`-gated (MainGM-only under the product's own explicit MVP rule and `ADR-019`'s three-role baseline — no delegation, no `AssistantGM`). Ownership (Primary/Co-owner) and Control (Permanent/Temporary grants) both satisfy `ADR-019`'s "assigned character" condition for ordinary Player action-access, without redefining `ADR-019` — Control does not additionally grant owner-private data visibility.
2. **Archive/dependency-aware physical delete (section 5):** `ArchiveCharacter` is an ordinary `Lifecycle`-section transition (`ADR-022`'s already-reserved `Lifecycle` lock/`LifecycleRevision`). `DeleteCharacterPermanently` is MainGM-only, requires a host-authoritative dependency check (board tokens, inventory/item references, GameLog references) re-validated server-side (never trusting a client-supplied "no dependencies" claim), and removes only the Character's **live** current-state row and live cross-references — it never deletes `DomainEvents` (`ADR-012` append-only). `CharacterHistoryProjection` continues to render the Character's past existence purely from its already-required `ADR-022` §7 historical event snapshots — exactly the scenario `ADR-022` §7 was already worded to survive ("a dependency is physically removed according to a future approved operation").
3. **Dead/`CharacterRestored` (section 6):** the transition to `Dead` uses the already-reserved `Lifecycle` section lock/revision and is restricted to a completed Rules Engine `FatalDamagePending` workflow (`HostSystem` issuer) or an explicit `GMOverride` (MainGM) — never a plain owner/controller command (`CAP-INV-008`). It does not touch `Mechanics`/`Ownership` sections and does not automatically cancel outstanding `ADR-024` reservations — those remain frozen, resolved independently by their own ordinary commands, preserving `ADR-022`'s parallel-section-editing guarantee. `RestoreDeadCharacter` is a **forward** `CharacterRestored` event (not an `ADR-012` §6 compensating event undoing the death event — `CAP-INV-008` explicitly rules out "ordinary Undo"), declaring every section it actually sets (`Lifecycle` plus whichever of `CharacterAnatomy`/`CharacterResource`/`RuntimeState` the GM's explicit restore choices touch).
4. **Ruleset migration (section 7):** Character Ruleset migration is a **distinct domain workflow from `ADR-013`'s database schema migration runner** (`ADR-013` §9's own stated boundary) — it changes `RulesetVersion`, not `DatabaseSchemaVersion`. `PreviewCharacterRulesetMigration` is a read-only `ADR-002` §4.2 Query (no events, no `BackupRecord`); `ApplyCharacterRulesetMigration` commits in one `ADR-012` §5 transaction, so a failure during application rolls back for free via ordinary transaction atomicity — no new rollback mechanism is needed for that case. Reverting an *already-committed* migration reuses `ADR-024`'s exact compensating-batch pattern (the same shape as `CharacterRespec`), not a third parallel undo mechanism. If a full campaign backup is additionally wanted before a large/bulk migration, it must use `ADR-012`'s existing SQLite Backup API/`BackupRecord` mechanism — per `ADR-013` §9's own stated integration point — never a bespoke copy. `.odchar` import creates a new Draft via `ADR-023`'s unmodified local-Draft/`BindDraftToCampaign`/compatibility-validation pipeline, with the imported file as the seed source in place of a `CharacterTemplate` — the imported Character's `RulesetVersion` is re-pinned to the target campaign's current Ruleset at bind time exactly as `ADR-023` §6 already requires, never blindly carried over from the file.
5. This ADR does **not** decide the Character aggregate boundary/section locks/history (`ADR-022`, already Accepted), local Draft/template/approval architecture (`ADR-023`, already Accepted), development economy/progression transactions (`ADR-024`, already Accepted), ability/resource/anatomy mechanics themselves, the `.odchar` file format, any extension of `ADR-019`'s role model (`AssistantGM`/delegation), or any production code/schema/test implementation.

This ADR is the normative authority for Character ownership/lifecycle-boundary/Ruleset-migration operations. It specializes `ADR-002`, `ADR-012`, `ADR-013`, `ADR-019`, `ADR-022`, `ADR-023`, and `ADR-024`; it does not replace their generic rules.

---

# 2. Контекст и проблема

`ADR-022` (`ODY-S04-001`) fixed the Character aggregate boundary and reserved `Ownership`/`Lifecycle` as sections with their own revisions/locks, without specifying their Character-specific semantics. `ADR-023` (`ODY-S04-002`) fixed Draft/template/approval architecture. `ADR-024` (`ODY-S04-003`) fixed the development-economy ledger and its compensating-event pattern. None of the three decides: how `ADR-019`'s deliberately simplified baseline role/character-assignment model is specialized into concrete owner/co-owner/controller semantics; what dependency checks gate archive/physical delete and what remains of a deleted Character's history; what invariants protect the Dead/restore boundary; or how Character Ruleset migration relates to `ADR-013`'s schema migration runner. Existing ADRs supply the shared substrate:

- `ADR-019` fixes the three-role baseline (`MainGM`/`Player`/`Observer`) and explicitly defers the full ownership/control model (`PERM-INV-007`/`008`) as future scope.
- `ADR-022` fixes the Character aggregate boundary, including already-reserved `Ownership` and `Lifecycle` sections/locks/revisions and the historical-event-snapshot minimum.
- `ADR-023` fixes the local-Draft/campaign-binding/compatibility-validation pipeline that `.odchar` import must reuse.
- `ADR-024` fixes the compensating-event batch pattern (`CharacterRespec`) that a post-commit Ruleset migration revert must reuse.
- `ADR-012` fixes the append-only journal, compensating-event mechanism, and snapshot/backup contract.
- `ADR-013` fixes the database schema migration runner and its explicit boundary with (undefined) ruleset migration.

Those ADRs do not answer four ownership/lifecycle/migration-specific questions that implementation must not invent ad hoc:

1. how `ADR-019`'s baseline specializes into concrete `PrimaryOwnerUserId`/`CoOwnerUserIds`/control-grant semantics for Character, without silently expanding the role model itself;
2. what dependency checks gate archive vs. physical delete, and what a deleted Character's historical identity still looks like;
3. what section revision/lock protects the Dead/restore boundary, and what happens to pending `ADR-024` reservations at death;
4. how Character Ruleset migration's preview/snapshot/rollback relates to `ADR-013`'s schema migration runner, and how `.odchar` import's new-Draft creation relates to `ADR-023`'s Ruleset-pinning rule.

This ADR answers only those questions.

---

# 3. Термины

## 3.1 `CharacterOwnership`

The `Ownership`-section state of a Character aggregate (`ADR-022`): `PrimaryOwnerUserId?`, `CoOwnerUserIds`, `PermanentControllerUserIds`, `TemporaryControlGrants`, guarded by `OwnershipRevision`.

## 3.2 Ownership vs. Control

Ownership (Primary/Co-owner) is a durable administrative relationship, assignable only by MainGM. Control (Permanent/Temporary grants) is an action-eligibility relationship — a controller may act as the Character without being an owner and without gaining owner-private data visibility (product §19.4).

## 3.3 Physical delete

The `DeleteCharacterPermanently` operation: removal of a Character's live current-state row and live cross-references, after a host-authoritative dependency check, while its `DomainEvents` remain permanently in the append-only journal (`ADR-012`).

## 3.4 Database schema migration vs. Character Ruleset migration

Two distinct mechanisms (`ADR-013` §9): database schema migration changes `DatabaseSchemaVersion` via `ADR-013`'s runner; Character Ruleset migration changes a Character's `RulesetVersion` via this ADR's own preview/apply commands. Neither substitutes for the other.

---

# 4. Owner/co-owner/controller semantics over `ADR-019`

**Decision:** `CharacterOwnership` lives inside `ADR-022`'s already-reserved `Ownership` section; ownership/control-grant commands are `Character.ManageOwnership`-gated (MainGM-only); `ADR-019`'s baseline is specialized, not redefined or expanded.

## 4.1 Section mapping — no new consistency primitive

`ADR-022` §5/§6 already reserve `OwnershipRevision` and the `Ownership` lock key. This ADR does not add a new section, lock, or revision for ownership/control — `CharacterOwnership`'s four fields (product §19) are exactly the `Ownership` section's content.

## 4.2 Primary owner assignment (`CAP-INV-007`)

`AssignPrimaryOwner`: MainGM-only (`Character.ManageOwnership`, already MainGM-only per product §26's explicit MVP rule), requires a mandatory `ReasonCode`, does not require confirmation from the old or new owner (product §19.3 — "Подтверждение старого или нового владельца технически не требуется"), and does not silently change `CoOwnerUserIds`/`PermanentControllerUserIds`/`TemporaryControlGrants` (`CAP-INV-007`'s own explicit clause). It commits atomically with a `CharacterPrimaryOwnerAssigned` event (already named, product §28) carrying `ADR-022`'s minimum historical snapshot (old/new `PrimaryOwnerUserId`, actor, reason, `OccurredAtHost`) — this is the audit trail roadmap §13.9 requires ("owner assignment requires MainGM reason/audit"), reusing `ADR-002`'s standard event actor/time fields rather than a bespoke audit record.

## 4.3 Co-owner and control grants

`AddCharacterCoOwner`/`RemoveCharacterCoOwner`/`GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl` are likewise `Character.ManageOwnership`-gated — the product specification does not name a distinct permission constant for granting control separately from managing ownership, and this ADR resolves that gap conservatively by keeping all ownership/control-membership changes under the single already-MainGM-only permission, rather than inventing a new delegation pathway (for example, letting a mere owner grant control to a third party without MainGM). This keeps the model exactly as narrow as `ADR-019`'s deliberately deferred ownership/control scope, closing only what roadmap §13.7 actually requires now.

Both ownership (Primary/Co-owner) and an active control grant (Permanent/Temporary) satisfy `ADR-019` §5.2's "assigned character" condition for ordinary Player action-access — this specializes `ADR-019`'s intentionally simple baseline concept into a concrete rule without redefining `ADR-019` itself (which left the exact ownership/assignment shape open, `ADR-019` §10, `PERM-INV-007`/`008`). Control does not, by itself, grant visibility into owner-private fields (`VisibilityPolicy`, `ADR-019` §3.4) — a controller can act as the Character but does not automatically see everything a Primary/Co-owner sees.

## 4.4 What remains explicitly deferred

`AssistantGM` and any delegation mechanism remain outside this ADR's scope, exactly as `ADR-019` and `ADR-023` already state — this ADR does not introduce a fourth role or a delegated `Character.ManageOwnership`/`Character.Approve` grant. Multiple simultaneous controllers of one Character are permitted (product §19.4); conflicting concurrent control operations are resolved by `ADR-022`'s existing lock/revision/idempotency mechanism (section revisions plus `CommandId`/`AppliedCommands`), not a new concurrency primitive.

---

# 5. Archive and dependency-aware physical delete

**Decision:** archive is an ordinary `Lifecycle`-section transition; physical delete is MainGM-only, host-revalidated, dependency-gated, and removes only live state — `ADR-022`'s historical event snapshots (already required to survive exactly this case) keep the Character's past visible in `CharacterHistoryProjection`.

## 5.1 Archive

`ArchiveCharacter` transitions `LifecycleStatus → Archived` (already a valid transition in `ADR-022`'s inherited lifecycle enumeration, product §7.1: "Draft|Active|Inactive|Retired|Dead -> Archived"). It uses the already-reserved `Lifecycle` lock/`LifecycleRevision` — no new section. `Character.Archive` (product §26) is checked normally under `ADR-019`'s existing role/permission model; this ADR does not restrict it beyond what the permission itself already implies.

## 5.2 Physical delete

`DeleteCharacterPermanently` is available only to MainGM (product §22.2's plain statement, stronger than the general permission list). Before committing, the host authoritatively re-checks dependencies — board token references, inventory/item references, GameLog references, and any other live cross-reference — regardless of what a client-side "dependency preview" (a read-only composition over already-existing repositories, not a new command type) showed the user beforehand (`CAP-INV-004`, host authority). If a blocking dependency exists, the command is rejected with no state change; the GM must resolve the dependency (for example, by first removing the token from the board) or use archive instead.

On success, the transaction removes the Character's live current-state row and live cross-references, and commits a `CharacterDeleted` event (already named, product §28) carrying `ADR-022`'s minimum historical snapshot. It does **not** delete any `DomainEvents` for that `CharacterId` — `ADR-012` §4.2's append-only guarantee has no "Character deleted" exception.

## 5.3 Historical identity after physical delete

`ADR-022` §7 already requires Character-significant events to remain renderable "even if... a dependency is physically removed according to a future approved operation" — this ADR is that operation. `CharacterHistoryProjection` (`ADR-022` §8) continues to render this Character's past entries purely from event historical snapshots (`DisplayNameSnapshot`, etc.); only *current*-state display (which requires a live row) becomes unavailable, exactly matching `ADR-022` §7 rule 3's existing distinction between current-field rendering and historical-entry rendering. No separate "archive of deleted Characters" history store is created — the same projection contract already suffices.

---

# 6. Dead and `CharacterRestored` invariants

**Decision:** the `Lifecycle` section lock/revision (already reserved by `ADR-022`) protects the Dead transition; `Mechanics`/`ADR-024` reservations are untouched by it; `CharacterRestored` is a forward event, not a compensating one.

## 6.1 Transition into Dead

Per product §23.1, the only legitimate paths to `LifecycleStatus=Dead` are a completed Rules Engine `FatalDamagePending` workflow (`IssuerKind=HostSystem`, `ADR-002` §6.4) or an explicit `GMOverride` (`IssuerKind=User`, `ActorUserId`=MainGM) — never a plain owner/controller-issued command (`CAP-INV-008`, "Владелец не устанавливает Dead вручную"). Both paths are `ChangeCharacterLifecycleStatus`-shaped commands declaring the `Lifecycle` section's expected revision; a stale `LifecycleRevision` rejects the transition rather than racing a concurrent lifecycle change.

## 6.2 Pending operations at death

This ADR does **not** require the Dead transition to automatically cancel or resolve outstanding `ADR-024` `AdvancementRecommendation` reservations. The transition declares and touches only the `Lifecycle` section; `Mechanics`/reservation state is a separate section under `ADR-022`'s own parallel-editing model and is left exactly as it was. If a GM later decides a pending recommendation should be dismissed because the Character died, that is an ordinary `ResolveAdvancementRecommendation` (Dismissed) call — not a cascade this ADR forces automatically. This preserves roadmap §13.9's "unrelated edits can proceed in parallel" property even across a lifecycle-boundary event.

## 6.3 `CharacterRestored`

`RestoreDeadCharacter` produces a **forward** `CharacterRestored` event (already named, product §28) — it is deliberately **not** modeled as an `ADR-012` §6 compensating event referencing the original Dead-transition event, because `CAP-INV-008` explicitly rules out treating restoration as "ordinary Undo." It requires a mandatory reason and the GM's explicit choice of new `LifecycleStatus`, body-part/damage state, resources, effects, and position (product §23.2) — the command therefore declares every section its explicit choices actually touch (`Lifecycle` plus whichever of `CharacterAnatomy`/`CharacterResource`/`RuntimeState` apply), reusing `ADR-022` §5 rule 2's existing multi-section-revision declaration rule rather than requiring the whole-Character lock exception (the touched-section set is bounded and explicit, not "most sections").

---

# 7. Ruleset migration: preview/snapshot/rollback

**Decision:** Character Ruleset migration is `PreviewCharacterRulesetMigration` (Query) then `ApplyCharacterRulesetMigration` (one `ADR-012` transaction), distinct from `ADR-013`'s schema migration runner, reusing `ADR-024`'s compensating-batch pattern for post-commit reversal and `ADR-012`'s snapshot contract for any additional backup.

## 7.1 Boundary with `ADR-013`

`ADR-013` §9 already states this boundary explicitly: changing `RulesetVersion` is not automatically a database schema migration, and Character Ruleset migration is "a separate domain workflow... not decided by this ADR [`ADR-013`]... must be defined by a separate ADR or task contract when Rules Engine/Content Domain reach that stage." This ADR is that separate ADR. `ADR-013`'s runner remains exclusively responsible for `DatabaseSchemaVersion`; this ADR is exclusively responsible for a Character's `RulesetVersion`. Neither substitutes for the other, and a Character Ruleset migration must not be routed through `ADR-013`'s `SchemaHistory`/temp-copy machinery.

## 7.2 Preview

`PreviewCharacterRulesetMigration` is a read-only `ADR-002` §4.2 Query — it builds `CharacterRulesetMigrationPlan` (`SourceRulesetVersion`, `TargetRulesetVersion`, `ValueChanges`, `DefinitionMappings`, `UnresolvedDecisions`, `PreviewHash`, product §25) without creating events, without a `BackupRecord`, and without mutating the Character. `PreviewHash` lets `ApplyCharacterRulesetMigration` later verify the GM is committing exactly the previewed plan, not a stale or tampered one.

## 7.3 Apply and failure-during-application rollback

`ApplyCharacterRulesetMigration` re-validates the plan against current authoritative state (`CAP-INV-004` — a client-cached preview is never trusted as final) and, on success, commits in **one** `ADR-012` §5 transaction: the Character's mechanics-affected sections (per `ValueChanges`/`DefinitionMappings`), the pinned `RulesetVersion`, and a `CharacterRulesetMigrated` event (already named, product §28) carrying `ADR-022`'s minimum historical snapshot plus `SourceRulesetVersion`/`TargetRulesetVersion`. If application fails at any point before commit, ordinary `ADR-012` §5 transaction atomicity already guarantees nothing partial persists — the Character remains exactly as it was, and a retry with the same `CommandId` is safe (`ADR-002` §9). This alone satisfies roadmap §13.9's "failed Ruleset migration rolls back" exit criterion; no new rollback mechanism is introduced for this case.

## 7.4 Reverting an already-committed migration

Undoing a migration that already committed successfully (a later policy decision, not a failure) is not "rollback" in the transactional sense above — it reuses `ADR-024` §7.2's exact compensating-batch pattern established for `CharacterRespec`: an ordered batch of compensating events restoring the prior mechanics values, grouped by one event referencing the original `CharacterRulesetMigrated` event's `CompensationGroupId` (`ADR-012` §6). This is not a third parallel undo mechanism — it is the same batch-compensation shape this ADR series already uses twice.

## 7.5 Optional full-campaign backup

Nothing in `CharacterRulesetMigrationPlan` requires a full campaign `BackupRecord`, and this ADR does not mandate one for every single-Character migration (unlike `ADR-012` §8.2 point 5's mandatory pre-*schema*-migration snapshot). If an implementation additionally wants a full-campaign safety net before a large or bulk Ruleset migration, it must create that backup exclusively through `ADR-012`'s existing SQLite Backup API/`BackupRecord` mechanism — per `ADR-013` §9's own stated single point of contact between the two workflows — never a bespoke file-copy mechanism.

## 7.6 `.odchar` import and `RulesetVersion` pinning

Product §24.2 already specifies that `.odchar` import creates a new `CharacterId`, reassigns nested IDs, checks the Ruleset, and creates a `Draft` requiring new GM approval. This is architecturally the same independent-copy mechanism `ADR-023` §5.3 already defines for templates — the imported file is simply the seed source in place of a `CharacterTemplate`. Import therefore reuses `ADR-023`'s unmodified pipeline: a local Draft is created from the imported payload (fresh nested identifiers, per `CAP-INV-006`'s same spirit), then `BindDraftToCampaign` performs `ADR-023` §6's compatibility validation and pins `RulesetVersion` to the **target campaign's current Ruleset** — never blindly carried over from the imported file's own possibly-stale `RulesetVersion`. If the campaign's Ruleset is incompatible with what the import needs, `ADR-023`'s existing bind-time rejection applies unchanged; this ADR does not add a second, import-specific compatibility check.

---

# 8. Не входит в ADR-025

Явно исключено из объёма этого ADR:

- **Character aggregate boundary/section locks/history** — already decided, `ADR-022` (`ODY-S04-001`), not reopened.
- **Local Draft vs campaign Character, templates, submit/review/approve** — already decided, `ADR-023` (`ODY-S04-002`), not reopened; `.odchar` import reuses it unmodified (section 7.6).
- **Development economy, purchases, respec mechanics themselves** — already decided, `ADR-024` (`ODY-S04-003`), not reopened; only its compensating-batch pattern is reused (section 7.4).
- **Ability/resource/anatomy mechanics** — already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4.
- **The `.odchar` file format itself** (structure of `manifest.json`/`character.json`/`portrait/`/`referenced-assets/`) — only its effect on ownership/lifecycle/Draft creation is in scope.
- **Any extension of `ADR-019`'s role model** — `AssistantGM`, delegation, or any new permission constant beyond `Character.ManageOwnership`'s already-stated scope remain `ADR-019`'s own future amendment, not decided or worked around here.
- **Concrete UI for ownership management, delete confirmation, restore, or Ruleset-migration preview screens.**
- **Concrete database schema for `CharacterOwnership`/`SchemaHistory`-adjacent Ruleset-migration tables** — implementation task under `ADR-003`, `ADR-011`, `ADR-012`, `ADR-013`, `ADR-022`, and this ADR.
- **Concrete command/event payload DTO files, tests, Unity UI, or content catalogs.**

---

# 9. Соответствие module boundaries (`ADR-001`) and existing ADRs

This ADR does not introduce code, but future implementation must preserve these boundaries:

- `Odyssey.Domain` owns `CharacterOwnership`/lifecycle-transition/Ruleset-migration invariants and domain event payload semantics. It remains serializer-free and Unity-free.
- `Odyssey.Rules` owns `FatalDamagePending` resolution logic and Ruleset value/definition mapping computation used by migration preview/apply. It does not commit state or write history.
- `Odyssey.Application` owns `AssignPrimaryOwner`/`AddCharacterCoOwner`/`RemoveCharacterCoOwner`/`GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl`/`ArchiveCharacter`/`DeleteCharacterPermanently`/`ChangeCharacterLifecycleStatus`/`RestoreDeadCharacter`/`PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration`/`ImportCharacter` command handlers, permission/revision/lock/dependency checks, and transaction orchestration reusing `ADR-022`'s aggregate/section-revision contract and `ADR-023`'s Draft-binding pipeline for import.
- `Odyssey.Persistence` owns the physical `Ownership`/`Lifecycle` section tables (already `ADR-022`-assigned) and any Ruleset-migration-specific tables. It does not decide whether an ownership change, delete, restore, or migration is legal, and does not implement Character Ruleset migration through `ADR-013`'s schema-migration runner.
- `Odyssey.Networking` owns transport/redaction/reconnect delivery of ownership/lifecycle/migration projections. It does not decide legality.
- `Odyssey.Unity.Client` owns ownership-management, delete-confirmation, restore, and migration-preview UI. It does not store authoritative ownership/lifecycle state and does not compute dependency lists as authoritative (only as a convenience preview the host re-validates).

Relationship to existing ADRs:

- `ADR-002` remains authoritative for command identity, idempotency, issuer kinds, and `Query`/compensation vocabulary — reused unmodified.
- `ADR-012` remains authoritative for the append-only journal, one-transaction boundary, compensating-event mechanism, and snapshot/`BackupRecord` contract — reused unmodified for both physical-delete history survival and any optional Ruleset-migration backup.
- `ADR-013` remains authoritative for database schema migration; this ADR fills exactly the "separate ADR" gap `ADR-013` §9 itself names for Character Ruleset migration, without touching schema migration's own runner.
- `ADR-019` remains authoritative for the three-role baseline; this ADR specializes "assigned character"/ownership into concrete Character semantics without adding a role or delegation mechanism `ADR-019` itself defers.
- `ADR-022` remains authoritative for the Character aggregate boundary, including the `Ownership`/`Lifecycle` sections this ADR fills in — no new section, lock, or revision is introduced.
- `ADR-023` remains authoritative for the local-Draft/`BindDraftToCampaign`/compatibility-validation pipeline `.odchar` import reuses unmodified.
- `ADR-024` remains authoritative for the compensating-batch pattern a post-commit Ruleset-migration revert reuses unmodified.

---

# 10. Правила для Codex

Codex обязан:

1. Model `CharacterOwnership` inside `ADR-022`'s already-reserved `Ownership` section; do not introduce a new section, lock key, or revision for ownership/control.
2. Gate `AssignPrimaryOwner`/`AddCharacterCoOwner`/`RemoveCharacterCoOwner`/`GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl` behind `Character.ManageOwnership` (MainGM-only); do not invent a delegation pathway for these operations.
3. Re-validate `DeleteCharacterPermanently`'s dependency check host-side before commit; never trust a client-supplied dependency preview as authoritative.
4. Never delete `DomainEvents` for a physically deleted Character; remove only the live current-state row and live cross-references.
5. Restrict the transition to `Dead` to `HostSystem` (post-`FatalDamagePending`) or MainGM `GMOverride` issuers; never accept a plain owner/controller command for this transition.
6. Do not cascade-cancel `ADR-024` reservations automatically on death; leave `Mechanics`/reservation state to its own independent commands.
7. Implement `RestoreDeadCharacter` as a forward `CharacterRestored` event, never as an `ADR-012` compensating event referencing the death event.
8. Implement Character Ruleset migration exclusively through `PreviewCharacterRulesetMigration`(Query)/`ApplyCharacterRulesetMigration`(one transaction); never route it through `ADR-013`'s schema-migration runner.
9. Reuse `ADR-024`'s compensating-batch pattern for reverting an already-committed migration; do not invent a third parallel undo mechanism.
10. Use `ADR-012`'s snapshot/`BackupRecord` mechanism exclusively if a Ruleset migration additionally creates a full-campaign backup; never a bespoke file-copy.
11. Implement `.odchar` import's Draft creation through `ADR-023`'s unmodified local-Draft/`BindDraftToCampaign` pipeline, re-pinning `RulesetVersion` to the target campaign at bind time; do not carry over the imported file's own `RulesetVersion` uncompared.
12. Do not implement the Character aggregate boundary, Draft/template/approval, development economy, ability/resource/anatomy mechanics, `.odchar` file format, or any `ADR-019` role extension under this ADR task unless a later task explicitly scopes it.

---

# 11. Definition of Done для будущей implementation-задачи

Implementation tasks using this ADR must prove, with tests where applicable:

1. A non-MainGM actor's `AssignPrimaryOwner`/co-owner/control-grant/`DeleteCharacterPermanently` command is rejected under `ADR-019`'s existing permission-check mechanism, with no state change.
2. `AssignPrimaryOwner` commits atomically with a `CharacterPrimaryOwnerAssigned` event carrying actor/reason/time, and does not change `CoOwnerUserIds`/control grants as a side effect.
3. `DeleteCharacterPermanently` against a Character with a live board-token/inventory/GameLog dependency is rejected with no state change; against a Character with none, it removes the live row while its prior `DomainEvents` remain queryable and `CharacterHistoryProjection` still renders its historical entries.
4. A plain owner/controller command attempting to set `LifecycleStatus=Dead` is rejected; a completed `FatalDamagePending` workflow or `GMOverride` succeeds.
5. An outstanding `AdvancementRecommendation` reservation is untouched by a Dead transition and can still be resolved afterward by its own command.
6. `RestoreDeadCharacter` produces a forward `CharacterRestored` event (not `IsCompensating=true`) reflecting the GM's explicit chosen state.
7. `ApplyCharacterRulesetMigration` failing mid-application leaves the Character's prior state and `RulesetVersion` completely unchanged, verified by a fresh read after a simulated failure.
8. `.odchar` import produces a local Draft with fresh nested identifiers and a `RulesetVersion` pinned to the target campaign (not the imported file's own version) once bound, following the same acceptance evidence `ADR-023`'s own Definition of Done already requires.
9. Core ownership/lifecycle/migration logic compiles without Unity dependencies in the pure .NET path.

---

# 12. Рассмотренные альтернативы

## 12.1 Primary owner assignment as a direct MainGM command vs. a workflow requiring new-owner confirmation

**Considered:** require the new owner to accept/confirm the assignment before it takes effect (an `ADR-002` §20 pending-workflow, analogous to a Draft's submit/approve). **Rejected** — product §19.3 explicitly states "Подтверждение старого или нового владельца технически не требуется," and roadmap §13.7/`CAP-INV-007` frame this as a direct MainGM administrative action with audit, not a negotiated handoff; adding a confirmation step would be unapproved scope beyond what the product already fixes.

**Accepted:** a direct, atomic `AssignPrimaryOwner` command with mandatory reason and audit event (section 4.2).

## 12.2 Dependency-checked physical delete vs. soft-delete-only (no physical delete at all)

**Considered:** never physically remove a Character's live row — only ever archive, relying entirely on `Archived`/`Dead` lifecycle states for anything resembling "removal." **Rejected** — product §22.2 explicitly names physical delete as a distinct, MainGM-only operation with its own dependency/backup/confirmation requirements; roadmap §13.7 lists "archive and dependency-aware delete" as two separate concerns, not one. Soft-delete-only would leave no way to satisfy a legitimate GM request to permanently remove erroneous or unwanted data (for example, a test Character created by mistake) without conflating it with the narrative `Archived`/`Dead` states.

**Accepted:** archive as an ordinary lifecycle transition; physical delete as a separate, dependency-gated, MainGM-only operation that still preserves history via `ADR-022`'s event snapshots (section 5).

## 12.3 Character Ruleset migration as an independent mechanism vs. reusing `ADR-013`'s schema migration runner directly

**Considered:** route Character Ruleset migration through `ADR-013`'s existing `SchemaHistory`/temp-copy/migration-registry machinery, treating a Ruleset version bump like a schema version bump. **Rejected** — `ADR-013` §9 itself explicitly forbids this conflation ("риск, что реализация Rules Engine попытается провести ruleset migration через тот же migration runner, что и schema migration, смешивая два разных по риску и workflow процесса"); a schema migration operates on the whole `campaign.db`'s physical structure via a temp-copy-then-replace pattern, while a Ruleset migration operates on one Character's mechanics values via an ordinary transactional command — forcing the latter through the former's temp-copy/whole-database machinery would be architecturally incoherent and contradicts an ADR already Accepted.

**Accepted:** Character Ruleset migration as its own `Preview`/`Apply` command pair, using ordinary `ADR-012` transaction atomicity for failure rollback and `ADR-024`'s compensating-batch pattern for post-commit reversal (section 7).

---

# 13. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- `AssistantGM` role and any delegation mechanism remain a future `ADR-019` amendment, not this ADR's scope;
- the `.odchar` file format itself remains outside this ADR — only its Draft-creation/Ruleset-pinning interaction is fixed here;
- concrete `CharacterOwnership`/Ruleset-migration table schema and DTO implementation belong to later implementation tasks.

---

# 14. Трассировка

ADR реализует и уточняет:

- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.7 Ownership and lifecycle operations, and §13.9's owner-assignment-audit, Archive/Dead-history-preservation, `.odchar`-import-new-Draft, and failed-Ruleset-migration-rollback exit criteria;
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-007`, `CAP-INV-008`, `CAP-INV-010`), §19 (ownership/control), §22 (archive/physical delete/historical identity), §23 (Dead/`CharacterRestored`), §24 (`.odchar` export/import, Draft-creation aspect only), §25 (Ruleset migration), §26–28 (permissions/commands/domain events used for these operations);
- `docs/tasks/SLICE-04_BACKLOG.md` §3.5, closing the fourth and final prerequisite ADR slot.

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency/issuer/`Query` foundations;
- `ADR-012` for the append-only journal, compensating-event mechanism, and snapshot/`BackupRecord` contract;
- `ADR-013` for the database schema migration runner and its own stated boundary with (this ADR's) Character Ruleset migration;
- `ADR-019` for the three-role baseline, specialized (not redefined) into concrete ownership/control semantics;
- `ADR-022` for the Character aggregate boundary, including the `Ownership`/`Lifecycle` sections this ADR fills in;
- `ADR-023` for the local-Draft/`BindDraftToCampaign`/compatibility-validation pipeline `.odchar` import reuses;
- `ADR-024` for the compensating-batch pattern a post-commit Ruleset-migration revert reuses.

With this ADR's acceptance, all four `SLICE-04_BACKLOG.md` §2 prerequisite exit criteria are met: `ADR-022`, `ADR-023`, `ADR-024`, and `ADR-025` are all `Accepted`.

---

# 15. Нормативное действие

Принято как ADR этой задачи (`ODY-S04-004`) без ожидания технического спайка — обоснование: задача разрешает границы модели/контракта поверх уже принятых role, aggregate-boundary, compensation, и migration-runner субстратов (`ADR-002`, `ADR-012`, `ADR-013`, `ADR-019`, `ADR-022`, `ADR-023`, `ADR-024`); ни один эмпирический неизвестный фактор не виден до реализации — то же обоснование, которым уже руководствовались `ADR-022`, `ADR-023`, и `ADR-024` при принятии до какого-либо спайка для этой же серии задач `SLICE-04`.

С даты принятия (`Accepted`):

- `SLICE-04` implementation tasks must model `CharacterOwnership` inside `ADR-022`'s already-reserved `Ownership` section, gate all ownership/control-grant commands behind `Character.ManageOwnership` (MainGM-only), and must not add a delegation pathway or new role;
- physical delete must re-validate dependencies host-side and must never delete `DomainEvents`; `CharacterHistoryProjection` must continue rendering a deleted Character's past purely from `ADR-022`'s existing historical event snapshots;
- the transition to `Dead` must be restricted to `HostSystem`(`FatalDamagePending`)/MainGM(`GMOverride`) issuers and must not cascade-cancel `ADR-024` reservations; `CharacterRestored` must be a forward event, never a compensating one;
- Character Ruleset migration must remain a distinct `Preview`/`Apply` workflow from `ADR-013`'s schema migration runner, using ordinary transaction atomicity for failure rollback and `ADR-024`'s compensating-batch pattern for post-commit reversal;
- `.odchar` import must create its Draft through `ADR-023`'s unmodified pipeline, re-pinning `RulesetVersion` to the target campaign at bind time;
- changing this ownership/lifecycle/Ruleset-migration boundary requires an amendment or superseding ADR, not silent implementation drift;
- `SLICE-04_BACKLOG.md` §2's four prerequisite ADR exit criteria are all satisfied as of this ADR's acceptance — the prerequisite backlog revision is complete; `SLICE-04`'s own vertical-slice implementation backlog remains a separate, not-yet-started future task.

---

**Конец документа**
