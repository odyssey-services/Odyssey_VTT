# ADR-024 — Development Economy and Progression Transactions

**Документ:** `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`  
**ADR:** ADR-024  
**Версия:** 1.0  
**Дата:** 30 августа 2026 года  
**Статус:** Accepted  
**Область:** `DevelopmentPool`/`DevelopmentTransaction` ledger boundary, atomicity/duplicate-spend prevention for advancement purchases, reservation and error-cancellation shape, `CriticalSuccessEvidence` single-use mechanism, and `CharacterRespec` compensation shape  
**Связанные этапы:** Roadmap Stage 5 (`SLICE-04`), Milestone `M5`, backlog `ODY-S04-003`  
**Базовые документы:** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.5/§13.9, `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-002`/`009`/`010`), §11–14, §26–28, `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`, `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, `docs/tasks/SLICE-04_BACKLOG.md`

---

# 1. Решение

Odyssey VTT fixes the development economy as an in-aggregate mechanics ledger reusing `ADR-022`'s section-revision/lock model and `ADR-002`'s command/event/compensation model — not a parallel economy-specific consistency or idempotency mechanism.

Обязательные решения:

1. **Ledger boundary (section 4):** `DevelopmentPool` current-state fields (`Earned`, `Spent`, `Reserved`, `Available`) live inside the `ADR-022` Character aggregate's existing `Mechanics` section, bumping `MechanicsRevision`. `DevelopmentTransaction` rows are **not** `DomainEvent`s and are **not** a second source of truth — they are a rebuildable ledger/read-model, committed in the same transaction as the authoritative `DomainEvent` that caused them, exactly matching `ADR-022`'s `CharacterHistoryProjection` pattern.
2. **Atomicity and duplicate-spend prevention (section 5):** every purchase is one `ADR-002` command declaring `ExpectedSectionRevisions=[Mechanics, <addressed entry>]`, committed in one transaction that updates `DevelopmentPool`, the addressed mechanics entry, and the resulting `DomainEvent`(s) together. `CommandId`/`AppliedCommands` (`ADR-002`) remain the **sole** idempotency mechanism; no second, economy-specific dedup key or nonce is introduced.
3. **Reservation and error cancellation (section 6):** `Reserved` moves only for genuinely pending operations — `AdvancementRecommendationCreated`/`Resolved` are this domain's own `ADR-002` §20 pending-workflow-equivalent event pair. Error cancellation (`RevertAdvancementPurchase`) is an `ADR-012` §6 compensating command producing a new `AdvancementPurchaseReverted` event (`CompensatesEventIds` referencing the original) — the original event is never edited or deleted, and revert is rejected if a later purchase now depends on it.
4. **Evidence single-use and `CharacterRespec` shape (section 7):** `CriticalSuccessEvidence.UsedByAdvancementId` is set exactly once, guarded by the evidence row's own optimistic-concurrency revision (`ADR-022`'s per-entry revision pattern applied to evidence entries) — not a separate spent-evidence registry. `CharacterRespec` is `PreviewCharacterRespec` (a read-only `ADR-002` §4.2 Query, no events) followed by `ApplyCharacterRespec` (one transaction producing an ordered batch of compensating `RespecReturn` events, forward `RespecSpend` events, and one grouping `CharacterRespecCompleted` event) — not a single opaque event that hides the underlying per-purchase detail `CAP-INV-005` requires to stay auditable.
5. This ADR does **not** decide the Character aggregate boundary/section locks/history (`ADR-022`, already Accepted), local Draft/template/approval architecture (`ADR-023`, already Accepted), ownership/lifecycle/Ruleset-migration operations (`ADR-025`), concrete ability/resource/anatomy mechanics, concrete numeric attribute/skill costs, or any production code/schema/test implementation.

This ADR is the normative authority for the development-economy/progression-transaction boundary. It specializes `ADR-002`, `ADR-012`, and `ADR-022`; it does not replace their generic command, journal, or aggregate-boundary rules.

---

# 2. Контекст и проблема

`ADR-022` (`ODY-S04-001`) fixed the Character aggregate boundary, section revisions/locks, event historical snapshots, and `CharacterHistoryProjection`. `ADR-023` (`ODY-S04-002`) fixed local Draft/template/approval architecture. Neither decides how the development economy — `DevelopmentPool`, `DevelopmentTransaction`, immediate valid purchases, reservations for genuinely pending operations, critical evidence, skill 5+ recommendation, compensating revert, and full respec — is bounded and made transactionally safe. Existing ADRs supply the shared substrate:

- `ADR-002` fixes command/event/idempotency, compensation vocabulary, and `Pending` as a committed terminal result for suspended workflows.
- `ADR-012` fixes the append-only journal, the one-transaction journal↔projection boundary, and the `CompensatingCommand → CompensatingEvent` correction mechanism.
- `ADR-022` fixes the Character aggregate boundary, section revisions/locks (including the `Mechanics` section and per-entry mechanics locks), and the minimum event historical snapshot.

Those ADRs do not answer four development-economy-specific questions that implementation must not invent ad hoc:

1. whether `DevelopmentPool`/`DevelopmentTransaction` is a section of the same Character aggregate or an independently authoritative subordinate aggregate/entity;
2. how one purchase atomically updates the pool, the mechanics entry, and history without a parallel idempotency mechanism;
3. what exactly is reserved for pending advancement operations, and what a correct error-cancellation shape looks like;
4. how `CriticalSuccessEvidence` is guaranteed single-use, and what shape `CharacterRespec`'s compensation takes.

This ADR answers only those questions.

---

# 3. Термины

## 3.1 `DevelopmentPool`

The current-state accounting for one Character's development points: `Earned`, `Spent`, `Reserved`, `Available = Earned - Spent - Reserved` (product §12). It is current-state data inside the Character aggregate's `Mechanics` section, not a standalone aggregate.

## 3.2 `DevelopmentTransaction`

A ledger/read-model row (`TransactionId`, `Kind`, `Amount`, `Reason`, `ActorUserId`, `RulesetVersion`, `CorrelationId`, product §12.1) recording one accounting movement. It is derived from and co-committed with the authoritative `DomainEvent` that caused it; it is not itself a `DomainEvent` and carries no independent authority.

## 3.3 `AdvancementPurchase`

A record of one purchase attempt (`PurchaseId`, `FromValue`, `ToValue`, `Cost`, `RequirementsSnapshot`, `Status` = `Applied`/`Reverted`/`SupersededByRespec`, product §13.2) — the historical-snapshot-bearing entity a compensating revert or respec references.

## 3.4 `AdvancementRecommendation`

The durable, pending record created when a skill's critical-evidence threshold is reached (product §14.3): a Character-specific pending-workflow entity, analogous to `ADR-002`'s generic `PendingInteraction` but using the product's own named events (`AdvancementRecommendationCreated`/`Resolved`), per `ADR-002` §20's explicit allowance for "`PendingInteractionCreated` либо эквивалентный event."

## 3.5 `CriticalSuccessEvidence`

An immutable record of a skill's critical success (`EvidenceId`, `SkillDefinitionId`, `SourceDiceRollId`, `UsedByAdvancementId?`, `Revision`, product §14.4). `UsedByAdvancementId` set (non-null) means the evidence has been consumed by exactly one skill-5+ advancement.

---

# 4. Ledger boundary

**Decision:** `DevelopmentPool`/`DevelopmentTransaction` live inside the `ADR-022` Character aggregate's `Mechanics` section — not a separately authoritative subordinate aggregate.

## 4.1 Why a section, not a subordinate aggregate

- The product's own schema (§12) scopes `DevelopmentPool` by `CharacterId` — it has no independent, cross-Character-addressable identity of its own; it exists only in relation to one Character.
- `ADR-022` §8/§9 requires `CharacterHistoryProjection` to be rebuildable purely from `DomainEvents` plus current authorized Character projection, with **no independent mutation command** and no second source of truth. If `DevelopmentPool` were a separate aggregate, its own event stream would either duplicate Character history (two journals recording the same fact) or require cross-aggregate event correlation at read time — both directly work against `ADR-022`'s already-accepted "projection, not a second source of truth" contract.
- A purchase inherently spans two concerns at once — the pool (`Spent`/`Available`) and one mechanics entry (an attribute's `BaseValue`, a skill's `Level`). `ADR-002` §12.2 forbids nested command handlers inside one root transaction; keeping both concerns inside one aggregate's section-revision model lets one command handler validate and commit both changes in the single transaction `ADR-002`/`ADR-012` already require, with no orchestration glue between two aggregate roots.

## 4.2 Section-revision mapping

`DevelopmentPool` accounting fields (`Earned`/`Spent`/`Reserved`/`Available`) are `Mechanics`-level metadata under `ADR-022` §5's own description ("Mechanics-wide revision changes when a command changes mechanics-level metadata such as pinned ruleset reference, derived value snapshot, or active effect references") — every change to `DevelopmentPool` bumps `MechanicsRevision`. No new `ADR-022` section-lock key is introduced for the pool itself; it is protected by the existing `Mechanics` lock key (`ADR-022` §6) when a command's invariant requires the pool to stay stable during a multi-step operation (for example, while a reservation is pending).

A purchase command additionally declares the entry-level expected revision for the addressed attribute/skill/ability (`AttributeValue:<AttributeDefinitionId>` or `CharacterSkill:<SkillDefinitionId>`, already-existing `ADR-022` §5/§6 keys) — reusing `ADR-022`'s multi-section-revision rule (§5 rule 2: "A command that depends on several sections lists all required section revisions") rather than inventing a pool-specific concurrency mechanism.

## 4.3 `DevelopmentTransaction` as a ledger projection, not a second journal

`DevelopmentTransaction` rows are committed in the **same** `ADR-012` §5 transaction as the `DomainEvent` describing the same fact (`DevelopmentPointsGranted`, `AttributeIncreased`, `SkillLevelPurchased`, etc. — product §28). If a `DevelopmentTransaction` row is lost, stale, or corrupt, it is rebuilt from `DomainEvents` plus current authorized projection — exactly `ADR-022` §8's recovery rule for `CharacterHistoryProjection`, applied to the same ledger. There is no independent "correct the ledger" command; ledger corrections happen only by the same compensating-event mechanism (section 6) that corrects any other Character-significant fact.

---

# 5. Atomicity and duplicate-spend prevention

**Decision:** reuse `ADR-002`'s `CommandId`/`AppliedCommands` as the sole idempotency mechanism and `ADR-012`'s one-transaction journal↔projection boundary as the sole atomicity mechanism. No parallel "economy" idempotency or transaction concept is introduced.

## 5.1 The purchase pipeline

For `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`AcquireAbility`, the command handler follows `ADR-002` §11's normative pipeline unchanged:

1. Resolve duplicate by `CommandId` (`ADR-002` §9) — if a durable receipt with the same fingerprint exists, return the stored result; the handler does not run again, `Available` is not re-checked, and no second `DevelopmentTransaction`/`DomainEvent` is produced.
2. Load the current, authoritative `DevelopmentPool.Available` and the addressed mechanics entry's current value from the Character aggregate — never from client-claimed values (`CAP-INV-004`).
3. Validate expected section revisions (`Mechanics` + the addressed entry key), permission (`Character.SpendDevelopment`), requirements, cap, and Ruleset compatibility.
4. On success, within **one** transaction: decrement `DevelopmentPool.Spent`/increment `Available`'s consumption, update the addressed entry's value, append the resulting `DomainEvent`(s) (e.g. `AttributeIncreased`/`SkillLevelPurchased`), append the corresponding `DevelopmentTransaction` (`Kind=Spend`) ledger row, create the `AdvancementPurchase` record (`Status=Applied`), and persist the `AppliedCommands` receipt — all-or-nothing per `ADR-012` §5.

## 5.2 Why this needs no new mechanism

`ADR-002` §9.2 already guarantees that a duplicate `CommandId` with the same fingerprint returns the stored result without re-invoking the handler — this alone prevents a retried purchase from spending twice, satisfying roadmap §13.9's "duplicate command does not spend twice" exit criterion. `ADR-012` §5 already guarantees the pool update, entry update, event, ledger row, and receipt commit as one atomic group. Introducing a second "spend lock" or "economy nonce" on top of this would duplicate a guarantee `ADR-002`/`ADR-012` already provide and would create two idempotency sources that could disagree.

---

# 6. Reservation and error cancellation

**Decision:** `Reserved` moves only for genuinely pending operations, modeled as this domain's own `ADR-002` §20 pending-workflow-equivalent pair; error cancellation is an `ADR-012` §6 compensating command, never a direct edit.

## 6.1 What is reserved

Per product §13.3, `Reserved` does not change for an ordinary immediate purchase (section 5). It changes only when a command creates a genuinely pending operation — the product's own named case is the skill 5+ path:

1. `RequestSkillAdvancedRecommendation` validates `Available` against the amount the recommendation may eventually cost, then — in one transaction — creates `DevelopmentPointsReserved` (`DevelopmentTransaction.Kind=Reserve`, `Reserved` increases, `Available` decreases by the same amount) and `AdvancementRecommendationCreated` (the pending record, section 3.4). The command's result is `Pending` (`ADR-002` §13.3) — a committed terminal result for *this* command; a duplicate of this same `CommandId` returns the same `Pending` result and does not create a second reservation.
2. `ResolveAdvancementRecommendation` is the continuation command (new `CommandId`, inherited `RootCommandId`/`CorrelationId`, `ParentCommandId` per `ADR-002` §20.2) a GM issues. In one transaction it produces `AdvancementRecommendationResolved` plus exactly one of:
   - **Dismissed:** `DevelopmentReservationReleased` (`Kind=ReleaseReservation`) — the reserved amount returns to `Available`; no skill level change.
   - **Approved, and `SkillAdvancementRule` decides points are additionally spent:** the already-reserved amount converts directly from `Reserved` to `Spent` (a single `Kind=Spend` transaction consuming the existing reservation, not a `ReleaseReservation`+new `Reserve`+`Spend` sequence that would leave a window where the points are neither reserved nor spent) plus `SkillLevelPurchased`.
   - **Approved, and the rule decides no additional points are required:** the reservation is released (`ReleaseReservation`) and `SkillLevelPurchased` is still produced, referencing the consumed evidence (section 7) rather than a `DevelopmentTransaction` spend.

   Which branch applies is a deterministic `SkillAdvancementRule` computation over already-approved data — not decided numerically by this ADR (explicitly out of scope: concrete numeric balances).

No other command mutates `Reserved`. `ADR-022`'s `Mechanics` lock is held (or the reservation's own durable state checked) so a concurrent purchase cannot spend the same reserved points before the recommendation resolves.

## 6.2 Error cancellation — `RevertAdvancementPurchase`

`RevertAdvancementPurchase` is a compensating command under `ADR-012` §6's `OriginalEvent → CompensatingCommand → CompensatingEvent` mechanism — it never edits or deletes the original `AttributeIncreased`/`SkillLevelPurchased` event. In one transaction it:

1. loads the target `AdvancementPurchase` (must be `Status=Applied`) and validates no later purchase or acquisition now depends on the value being reverted (a dependency check — the exact dependency graph is a Rules Engine/ruleset concern, not decided numerically here);
2. requires a `ReasonCode` (`ADR-002` §21.2's mandatory compensation metadata);
3. reverts the addressed mechanics entry to `AdvancementPurchase.FromValue`;
4. creates `AdvancementPurchaseReverted` (`IsCompensating=true`, `CompensatesEventIds=[original event id]`) and a `DevelopmentTransaction.Kind=Refund` returning `Cost` to `Available` (`Earned` unchanged, `Spent` decreases);
5. sets `AdvancementPurchase.Status=Reverted`.

If the dependency check fails, the command is rejected with no state change — the original purchase and its dependents remain intact; a GM must resolve the dependency (e.g., by reverting the dependent first) before this purchase can be reverted, or must use `CharacterRespec` (section 7.2) for a broader reconfiguration.

---

# 7. Evidence single-use and `CharacterRespec` shape

## 7.1 `CriticalSuccessEvidence` single-use

**Decision:** a flag on the evidence row itself (`UsedByAdvancementId`), guarded by the row's own optimistic-concurrency revision — not a separate spent-evidence registry.

When `ResolveAdvancementRecommendation` approves an advancement that consumes specific evidence entries, the command declares `ExpectedMechanicsEntryRevisions` for every `EvidenceId` it intends to consume (reusing `ADR-022` §5's entry-level revision mechanism, applied to `CriticalSuccessEvidence` rows exactly as it already applies to attributes/skills/abilities/resources). In the same transaction as `AdvancementRecommendationResolved`, each consumed evidence row's `UsedByAdvancementId` is set exactly once and its revision advances. If a concurrently-committed transaction already set `UsedByAdvancementId` on the same evidence (stale expected revision), the command is rejected with a revision conflict — no partial state change, and the evidence is not double-spent.

This satisfies roadmap §13.9's "critical evidence cannot be reused" and `CAP-INV-009` directly: the evidence event itself remains immutable (`CriticalSuccessEvidence` rows are never deleted or edited beyond the one-time `UsedByAdvancementId` assignment), and history still shows exactly which evidence entries a given advancement consumed.

## 7.2 `CharacterRespec` shape

**Decision:** `PreviewCharacterRespec` is a read-only `Query` (`ADR-002` §4.2 — no events, no state change); `ApplyCharacterRespec` is one transaction producing an ordered batch of compensating and forward events, grouped by one `CharacterRespecCompleted` event — not a single opaque event.

Per product §13.5's eight steps, `ApplyCharacterRespec`:

1. re-validates the same preview computation server-side (never trusts a client-cached preview as authoritative, `CAP-INV-004`);
2. requires the GM's confirmation and a `ReasonCode`;
3. within one transaction, for every previously-`Applied` `AdvancementPurchase` the respec undoes: creates a compensating event (the same `AdvancementPurchaseReverted` shape as section 6.2, or a respec-scoped equivalent carrying `CompensationGroupId`, `ADR-012` §6) and a `DevelopmentTransaction.Kind=RespecReturn` (distinct from an ordinary `Refund` — this is a respec-driven return, not a single-purchase error correction, matching the product's own separate `Kind` value) and sets the superseded `AdvancementPurchase.Status=SupersededByRespec`;
4. for every new purchase the respec's target configuration re-applies: creates the ordinary forward event (`AttributeIncreased`/`SkillLevelPurchased`) plus a `DevelopmentTransaction.Kind=RespecSpend` (distinct from ordinary `Spend`) and a new `AdvancementPurchase` (`Status=Applied`);
5. creates exactly one `CharacterRespecCompleted` event referencing the full ordered list of the above events/`CompensationGroupId` and the before/after configuration snapshot, grouping the whole operation as one coherent history entry per product's "группирует события в одной истории" — without collapsing the individually-inspectable `RespecReturn`/`RespecSpend`/reverted-purchase detail `CAP-INV-005` requires to remain auditable.

Nothing in `CharacterRespec` invents a new correction mechanism beyond `ADR-012` §6's compensating-event pattern applied at batch scale; it does not overlap with `ODY-S04-004`'s Character Ruleset migration, which addresses a Ruleset *version* change, not a same-Ruleset reallocation of already-spent points.

---

# 8. Не входит в ADR-024

Явно исключено из объёма этого ADR:

- **Character aggregate boundary/section locks/history** — already decided, `ADR-022` (`ODY-S04-001`), not reopened.
- **Local Draft vs campaign Character, templates, submit/review/approve** — already decided, `ADR-023` (`ODY-S04-002`), not reopened.
- **Full ownership/control/lifecycle operation contract, physical delete, Dead/restore, and Character Ruleset migration** — future `ADR-025` (`ODY-S04-004`).
- **Ability/resource/anatomy mechanics themselves** (what they are) — already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4; this ADR only fixes how their *development transactions* are bounded/committed, reusing the same `Mechanics`/entry-level section-revision model.
- **Concrete numeric attribute/skill costs, caps, or `SkillAdvancementRule` decision tables** — Rules Engine/ruleset content, not an architectural boundary.
- **Concrete UI for the respec preview, recommendation review, or purchase screens.**
- **Concrete database schema for `DevelopmentPool`/`DevelopmentTransaction`/`AdvancementPurchase`/`CriticalSuccessEvidence` tables** — implementation task under `ADR-003`, `ADR-011`, `ADR-012`, `ADR-022`, and this ADR.
- **Concrete command/event payload DTO files, tests, Unity UI, or content catalogs.**

---

# 9. Соответствие module boundaries (`ADR-001`) and existing ADRs

This ADR does not introduce code, but future implementation must preserve these boundaries:

- `Odyssey.Domain` owns `DevelopmentPool`/`AdvancementPurchase`/`CriticalSuccessEvidence`/`AdvancementRecommendation` invariants, the reservation/conversion/compensation semantics, and domain event payload semantics. It remains serializer-free and Unity-free.
- `Odyssey.Rules` owns `SkillAdvancementRule`'s deterministic decision of whether additional points are spent at recommendation resolution, and cost/cap/requirement calculations used by purchase validation. It does not commit state or write history.
- `Odyssey.Application` owns `PurchaseAttributeIncrease`/`PurchaseSkillLevel`/`AcquireAbility`/`GrantDevelopmentPoints`/`RequestSkillAdvancedRecommendation`/`ResolveAdvancementRecommendation`/`RevertAdvancementPurchase`/`PreviewCharacterRespec`/`ApplyCharacterRespec` command handlers, permission/revision/lock checks, transaction orchestration, and `DevelopmentTransaction`/ledger-projection rebuild ports, reusing `ADR-022`'s aggregate/section-revision contract.
- `Odyssey.Persistence` owns the physical `DevelopmentPool`/`DevelopmentTransaction`/`AdvancementPurchase`/`CriticalSuccessEvidence`/`AdvancementRecommendation` tables (as Character-scoped storage per `ADR-022`). It does not decide whether a purchase, reservation, revert, or respec is legal.
- `Odyssey.Networking` owns transport/redaction/reconnect delivery of ledger/recommendation/respec projections. It does not decide economy legality.
- `Odyssey.Unity.Client` owns purchase/respec-preview/recommendation-review UI. It does not store authoritative `DevelopmentPool`/ledger state and does not compute `Available` locally as authoritative.

Relationship to existing ADRs:

- `ADR-002` remains authoritative for command identity, idempotency, `Pending`/continuation semantics, and compensation vocabulary — reused unmodified for every command in this ADR.
- `ADR-012` remains authoritative for the append-only journal, the one-transaction journal↔projection boundary, and the `CompensatingCommand`/`CompensatingEvent` mechanism — reused unmodified for revert/respec.
- `ADR-022` remains authoritative for the Character aggregate boundary, `Mechanics`/entry-level section revisions and locks, and the minimum event historical snapshot — every command in this ADR is an ordinary `ADR-022` Character command operating inside the `Mechanics` section.

---

# 10. Правила для Codex

Codex обязан:

1. Model `DevelopmentPool`/`DevelopmentTransaction` as `Mechanics`-section current-state/ledger data inside the `ADR-022` Character aggregate; do not create an independently authoritative subordinate aggregate for them.
2. Treat `DevelopmentTransaction` rows as a rebuildable ledger projection co-committed with the causing `DomainEvent`; do not treat them as `DomainEvent`s themselves or as an independent source of truth.
3. Use `CommandId`/`AppliedCommands` (`ADR-002`) as the sole purchase idempotency mechanism and one `ADR-012` transaction as the sole atomicity mechanism for pool + entry + event + ledger + receipt; do not introduce a second economy-specific dedup key or transaction concept.
4. Move `Reserved` only for genuinely pending operations (section 6.1); do not reserve points for an ordinary immediate purchase.
5. Implement `RevertAdvancementPurchase`/`CharacterRespec` exclusively through `ADR-012` §6's `CompensatingCommand`/`CompensatingEvent` mechanism; never edit or delete an original advancement event.
6. Enforce `CriticalSuccessEvidence` single-use via the evidence row's own `UsedByAdvancementId` field guarded by its own revision; do not create a separate spent-evidence registry table.
7. Implement `CharacterRespec` as `PreviewCharacterRespec` (Query, no events) followed by `ApplyCharacterRespec` (one transaction, ordered `RespecReturn`/`RespecSpend`/reverted-purchase events grouped by one `CharacterRespecCompleted`); do not collapse the batch into a single opaque event.
8. Do not implement the Character aggregate boundary, Draft/template/approval, ownership/lifecycle/Ruleset migration, or concrete ability/resource/anatomy mechanics under this ADR task unless a later task explicitly scopes it.

---

# 11. Definition of Done для будущей implementation-задачи

Implementation tasks using this ADR must prove, with tests where applicable:

1. Two concurrent purchases against the same `DevelopmentPool`/entry produce a revision conflict for the second one without partial state change, or serialize correctly with no double-spend.
2. A duplicate purchase command with the same `CommandId` returns the stored result and does not reapply the pool/entry change, event, or ledger row (`ADR-002`).
3. `RequestSkillAdvancedRecommendation` durably reserves points and returns `Pending`; `ResolveAdvancementRecommendation` (Approved-spend, Approved-no-spend, and Dismissed) each move `Reserved`/`Available`/`Spent` correctly with no window where the same points are both reserved and spendable elsewhere.
4. `RevertAdvancementPurchase` on a purchase with a later dependent is rejected with no state change; on an independent purchase it restores `FromValue`, refunds `Cost`, and leaves the original event unmodified in the journal.
5. Attempting to consume the same `CriticalSuccessEvidence` in two concurrently-committed advancements results in exactly one success and one revision-conflict rejection; the evidence's `UsedByAdvancementId` is set exactly once.
6. `ApplyCharacterRespec` produces an inspectable, ordered event batch (not one opaque event) grouped by `CharacterRespecCompleted`, and `CharacterHistoryProjection` rebuilt from `DomainEvents` shows the same reverted/re-applied detail as the eagerly-maintained projection.
7. Core development-economy logic compiles without Unity dependencies in the pure .NET path.

---

# 12. Рассмотренные альтернативы

## 12.1 `DevelopmentPool` as a section of the Character aggregate vs a subordinate aggregate with its own identity

**Considered:** model `DevelopmentPool`/`DevelopmentTransaction` as an independently authoritative aggregate keyed by `DevelopmentPoolId`, joined to Character only by `CharacterId`. **Rejected** — it would require either duplicating ledger events into a second journal or correlating two aggregates' event streams at `CharacterHistoryProjection` read time, directly working against `ADR-022`'s already-accepted "projection derived from one authoritative source, never a second source of truth" contract; it would also force a purchase (which inherently touches both the pool and a mechanics entry) into either a nested command handler (forbidden, `ADR-002` §12.2) or awkward two-aggregate orchestration.

**Accepted:** `DevelopmentPool` as `Mechanics`-section current-state data inside the same Character aggregate (section 4), with `DevelopmentTransaction` as a co-committed ledger projection.

## 12.2 Reservation as an explicit pending state vs optimistic apply-then-compensate without reservation

**Considered:** skip reservation entirely — let a skill-5+ advancement optimistically "apply" a tentative spend immediately, then compensate (refund) if the GM later dismisses the recommendation. **Rejected** — it would let `Available` briefly overstate what is actually free to spend on something else while a recommendation is still pending (a player could spend the same points twice — once optimistically, once for real — before the GM resolves the first), directly risking roadmap §13.9's duplicate-spend exit criterion; product §13.3 itself distinguishes "resérve only for genuinely pending operations" from ordinary immediate application, implying reservation is the intended mechanism for exactly this case.

**Accepted:** explicit `Reserved` state via `DevelopmentPointsReserved`/`DevelopmentReservationReleased`, held until `ResolveAdvancementRecommendation`'s continuation command resolves it (section 6.1).

## 12.3 Single-use evidence via a flag on the evidence object vs a separate spent-evidence registry

**Considered:** maintain a separate `SpentEvidenceIds` table/aggregate, checked before allowing an advancement to consume evidence. **Rejected** — it duplicates a fact already representable on the evidence row itself (`UsedByAdvancementId`), creating a second source of truth for the same one-time-use property, and would require an additional cross-table revision check beyond what `ADR-022`'s existing per-entry revision mechanism already gives for free when applied directly to evidence rows.

**Accepted:** `UsedByAdvancementId` on `CriticalSuccessEvidence` itself, guarded by the row's own `ADR-022`-style revision (section 7.1).

## 12.4 `CharacterRespec` as one opaque event vs an inspectable ordered batch

**Considered:** represent a full respec as a single `CharacterRespecCompleted` event carrying a complete before/after Character snapshot, with no individually-inspectable per-purchase revert/re-apply events. **Rejected** — `ADR-022` §7 already prohibits a full Character sheet copy in every event, and `CAP-INV-005` requires history to remain auditable at the level of individual committed facts, not collapsed into an unaudited blob; a GM or player asking "why did my Strength change" should be able to trace it to a specific reverted/re-applied purchase, not just "a respec happened."

**Accepted:** an ordered batch of `RespecReturn`/`RespecSpend`/reverted-purchase events, grouped for display by one `CharacterRespecCompleted` event referencing them (section 7.2).

---

# 13. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- ownership/lifecycle/Ruleset-migration operation contracts are `ADR-025`;
- concrete numeric attribute/skill costs, caps, and `SkillAdvancementRule` decision tables are Rules Engine/ruleset content, not an architectural decision;
- concrete `DevelopmentPool`/`DevelopmentTransaction`/`AdvancementPurchase`/`CriticalSuccessEvidence` table schema and DTO implementation belong to later implementation tasks.

---

# 14. Трассировка

ADR реализует и уточняет:

- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.5 Mechanics and progression, and §13.9's "no `CharacterLevel`," "duplicate purchase idempotency," and "critical evidence single-use" exit criteria;
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §4 (`CAP-INV-002`, `CAP-INV-009`, `CAP-INV-010`), §11 (attributes, as the ledger's spend target), §12 (`DevelopmentPool`/`DevelopmentTransaction`), §13 (purchase, reservation, revert, respec), §14 (skills, critical evidence), §26–28 (permissions/commands/domain events used for this economy);
- `docs/tasks/SLICE-04_BACKLOG.md` §3.3, closing the third prerequisite ADR slot.

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency/`Pending`/compensation foundations;
- `ADR-012` for the append-only journal, one-transaction journal↔projection boundary, and compensating-event mechanism;
- `ADR-022` for the Character aggregate boundary, `Mechanics`/entry-level section revisions and locks, and event historical snapshot minimum that every command in this ADR joins.

Related future tasks:

```text
ODY-S04-004  ADR-025: Character Ownership, Lifecycle, and Ruleset Migration Operations
```

---

# 15. Нормативное действие

Принято как ADR этой задачи (`ODY-S04-003`) без ожидания технического спайка — обоснование: задача разрешает границы модели/контракта поверх уже принятых command, journal, и aggregate-boundary субстратов (`ADR-002`, `ADR-012`, `ADR-022`); ни один эмпирический неизвестный фактор не виден до реализации — то же обоснование, которым уже руководствовались `ADR-022` и `ADR-023` при принятии до какого-либо спайка для этой же серии задач `SLICE-04`.

С даты принятия (`Accepted`):

- `SLICE-04` implementation tasks must model `DevelopmentPool`/`DevelopmentTransaction` as `Mechanics`-section current-state/ledger data inside the `ADR-022` Character aggregate, never as an independently authoritative subordinate aggregate;
- every purchase must reuse `CommandId`/`AppliedCommands` as the sole idempotency mechanism and one `ADR-012` transaction as the sole atomicity boundary for pool + entry + event + ledger + receipt;
- reservation must be limited to genuinely pending operations per section 6.1; an ordinary immediate purchase must not reserve;
- `RevertAdvancementPurchase`/`CharacterRespec` must use `ADR-012` §6's compensating-event mechanism exclusively — no direct edit/delete of an original advancement event;
- `CriticalSuccessEvidence` single-use must be enforced via the evidence row's own `UsedByAdvancementId`/revision, not a separate registry;
- `CharacterRespec` must produce an inspectable, ordered event batch grouped by `CharacterRespecCompleted`, not a single opaque event;
- changing this development-economy/progression-transaction boundary requires an amendment or superseding ADR, not silent implementation drift.

---

**Конец документа**
