# ADR-023 — Character Drafts, Templates, and Approval Workflow

**Документ:** `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md`  
**ADR:** ADR-023  
**Версия:** 1.0  
**Дата:** 30 августа 2026 года  
**Статус:** Accepted  
**Область:** Границы local Draft vs campaign-authoritative Character, модель хранения/жизненного цикла `PersonalCharacterTemplate`/`CampaignCharacterTemplate`, архитектурный механизм независимой копии шаблона, ruleset-compatibility validation при создании Draft, и минимальный submit/review/comment/approve command/event flow  
**Связанные этапы:** Roadmap Stage 5 (`SLICE-04`), Milestone `M5`, backlog `ODY-S04-002`  
**Базовые документы:** `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.4/§13.8/§13.9, `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7–9, §20, §26–28, `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md`, `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`, `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`, `docs/tasks/SLICE-04_BACKLOG.md`

---

# 1. Решение

Odyssey VTT фиксирует Draft/template/approval workflow as a two-phase model that reuses `ADR-022`'s aggregate boundary rather than inventing a parallel one:

1. **Local Draft vs campaign-authoritative Character (section 4):** a **local Draft** is a client-owned record that exists *before* it is bound to any campaign — it has no `CampaignId`, no `CharacterId`, and is not an instance of the `ADR-022` Character aggregate. It follows `ADR-002`'s single Application command path (even single-player "local" mode is host-authoritative per `ADR-002` §23.1), but its authoritative boundary is the user's own personal profile storage, not a campaign database, and it may be lost if that local storage is deleted (matching the product's own §8.3 description). The moment a local Draft is bound to a campaign (`BindDraftToCampaign`), the host creates a **new, permanent `ADR-022` Character aggregate instance** — `LifecycleStatus=Draft`, `ApprovalState=Draft` — and every later transition (submit, comment, approve → `Active`) mutates that same aggregate instance and the same `CharacterId`. There is no second, parallel "Draft aggregate" that gets replaced or converted at approval.
2. **Template lifecycle and storage (section 5):** `PersonalCharacterTemplate` and `CampaignCharacterTemplate` are the same `CharacterTemplate` aggregate type distinguished only by `TemplateScope` (`Personal` | `Campaign`), exactly as the product schema already shows. A `Personal`-scope template lives in the same personal/local-profile storage boundary as local Drafts (owned by `OwnerUserId`, no `CampaignId`); a `Campaign`-scope template lives inside a specific campaign's authoritative storage as its own aggregate, versioned independently of any Character it seeds.
3. **Independent copy mechanism (section 5.3):** creating a Draft/Character from a template performs a **deep value copy** of the template's seed data into the new Draft/Character's own section state, minting **fresh identifiers** for every nested instance (skills, abilities, resources, custom fields). The template reference is recorded only as **immutable provenance** (`TemplateId`, `TemplateVersion` captured at copy time) — never as a live pointer that is re-resolved when the Character is later rendered or edited. This is the concrete mechanism that makes `CAP-INV-006` ("a template does not change an already-created Character") true by construction, not merely by convention.
4. **Compatibility validation (section 6):** the host validates ruleset compatibility **synchronously at `BindDraftToCampaign`** — the earliest point the host authoritatively knows both the target campaign and the chosen template — not deferred to `ApproveCharacterDraft`. On success, the resulting Character's `RulesetVersion` is **pinned** to the target campaign's own current ruleset version at that moment (not silently re-read later). Drift between a pinned Character `RulesetVersion` and a campaign's ruleset that changes afterward is explicitly `ADR-025`'s Ruleset-migration concern, not decided here.
5. **Submit/review/comment/approve flow (section 7):** the minimum command/event set is exactly the set the product specification already names in §27/§28 — `CreateLocalCharacterDraft`, `BindDraftToCampaign`, `SubmitCharacterDraft`, `AddCharacterReviewComment`, `ApproveCharacterDraft` — carried on `ADR-002`'s command/event envelope and `ADR-022`'s section revisions/event-snapshot minimum. No `Reject`/`ChangesRequested` command or state is introduced: per the product's own §7.2, review feedback is comments while the Character remains `ApprovalState=Draft`; a GM "sends a Draft back" simply by not approving it. `Character.Approve` remains MainGM-only under `ADR-019`'s currently accepted three-role baseline (`MainGM`/`Player`/`Observer`); the product document's mention of AssistantGM-delegated approval is an explicitly deferred capability of `ADR-019` itself (no `AssistantGM` role, no delegation model exists yet) and is not decided or implemented by this ADR.
6. This ADR does **not** decide the Character aggregate boundary/section locks/history mechanism (`ADR-022`, already Accepted), development economy/points/purchases (`ADR-024`), ownership/lifecycle/Ruleset-migration operations (`ADR-025`), ability/resource/anatomy mechanics (already closed per `SLICE-04_BACKLOG.md` §3.4), any concrete UI for the approval screen, or any production code/schema/test implementation.

This ADR is the normative authority for the Draft/template/approval architectural boundary. It specializes `ADR-002`, `ADR-003`, `ADR-019`, and `ADR-022`; it does not replace their generic command, serialization, permission, or aggregate-boundary rules.

---

# 2. Контекст и проблема

`ADR-022` (`ODY-S04-001`) fixed the Character aggregate boundary, section revisions/locks, event historical snapshots, and `CharacterHistoryProjection` — but explicitly deferred Draft/template/approval architecture to this ADR (`ADR-022` §10). The product documents already name local Drafts, `PersonalCharacterTemplate`, `CampaignCharacterTemplate`, `TemplateScope`, compatibility validation, submit/review/approve, review comments, and independent-copy/no-live-binding semantics (`Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7–9). Existing ADRs supply the shared substrate:

- `ADR-002` fixes command/event/idempotency, aggregate revisions, and atomic event batches.
- `ADR-003` fixes versioned DTOs and prohibits direct Domain aggregate serialization.
- `ADR-019` fixes the baseline permission/role model (`MainGM`/`Player`/`Observer`) and explicitly defers `AssistantGM`/delegation.
- `ADR-022` fixes the Character aggregate boundary, section revisions, section locks, and event snapshot minimum that a bound Draft must join.

Those ADRs do not answer four Draft/template-specific questions that implementation must not invent ad hoc:

1. where exactly the architectural boundary sits between a local, not-yet-campaign-bound Draft and a campaign-authoritative Character, and what happens to identity/history at each transition;
2. what the storage/lifecycle model for `PersonalCharacterTemplate`/`CampaignCharacterTemplate` is, and what concretely makes a created Character's copy of a template *independent*;
3. what compatibility validation a Draft-from-`CampaignCharacterTemplate` must perform, and when;
4. the minimum commands/events for submit/review/comment/approve, and who (which role) may perform each step.

This ADR answers only those questions.

---

# 3. Термины

## 3.1 Local Draft

A client-owned, pre-campaign-binding record capturing a player's in-progress character concept: chosen `CharacterKind`, minimal required fields (§8.2), and — if created from a `PersonalCharacterTemplate` — a deep value copy of that template's seed data plus its `TemplateId`/`TemplateVersion`. It has no `CampaignId`, no `CharacterId`, and is not an `ADR-022` Character aggregate instance.

## 3.2 Bound Draft

The state of an `ADR-022` Character aggregate instance immediately after `BindDraftToCampaign`: it has a real `CampaignId` and `CharacterId`, `LifecycleStatus=Draft`, `ApprovalState=Draft`, and a pinned `RulesetVersion`. "Submitted" is not a separate `LifecycleStatus`/`ApprovalState` value — it is represented purely by the `CharacterDraftSubmitted` event/command history while the aggregate's `ApprovalState` remains `Draft` (product §7.2).

## 3.3 `CharacterTemplate`

A campaign- or personal-scoped aggregate (`TemplateScope` = `Personal` | `Campaign`) that supplies seed data for creating a Draft. It is versioned independently of any Character it seeds (own `Version`/`Status`/`Revision`, product §9.1).

## 3.4 Independent copy

The property that a Draft/Character created from a template holds only immutable, point-in-time provenance (`TemplateId`, `TemplateVersion`) and freshly-identified copies of the template's seed values — never a live reference resolved again at read or edit time (`CAP-INV-006`).

## 3.5 Compatibility validation

The host-side check, performed at `BindDraftToCampaign`, that a chosen template's ruleset reference is usable with the target campaign's own pinned ruleset, and the pinning of the resulting Character's `RulesetVersion` to the campaign's current ruleset version at that moment.

---

# 4. Local Draft vs campaign-authoritative Character boundary

**Decision:** the boundary is the `BindDraftToCampaign` command, not `ApproveCharacterDraft`.

## 4.1 Before binding — local Draft

A local Draft is created by `CreateLocalCharacterDraft`. This is still an ordinary `ADR-002` Application command (there is exactly one command path for local and networked host operation, `ADR-002` §23.1) — but it targets the user's own personal profile storage, not a specific campaign's authoritative database, and it carries no `CampaignId`/`CharacterId`. Consequences:

- it is **not** an `ADR-022` Character aggregate instance — it has no `CharacterRevision`, no section revisions, and does not participate in `CharacterHistoryProjection` or `DomainEvents`;
- it may legitimately be lost if the user's local data is deleted, matching product §8.3's plain statement — this is architecturally intentional, not an oversight, because it has never been durably persisted campaign-side;
- it may reference a `PersonalCharacterTemplate` (copied by value, section 5.3) or start from a blank required-field set (product §8.2);
- local, uncommitted biography/custom-field editing on this record follows `ADR-022`/product §20.2's "local forms" convention — it is not sent to any host per keystroke, because there is no campaign host to send it to yet.

## 4.2 The binding moment — `BindDraftToCampaign`

`BindDraftToCampaign` takes the local Draft's captured payload, a target `CampaignId`, and the resolved template reference (if any), performs compatibility validation (section 6), and — on success — creates a **new, permanent** `ADR-022` Character aggregate instance:

```text
CharacterId          -- freshly allocated, permanent from this point on
CampaignId
CharacterKind
LifecycleStatus       = Draft
ApprovalState         = Draft
RulesetVersion        -- pinned, section 6
TemplateId?           -- immutable provenance, section 5.3
TemplateVersion?      -- immutable provenance, section 5.3
```

This is the first `DomainEvent` for that `CharacterId` — there is no local pre-history to reconcile, because the local Draft never had campaign-authoritative history to begin with. From this point, the aggregate's `IdentityRevision`/`PresentationRevision`/`MechanicsRevision`/etc. (`ADR-022` section 5) begin at their first value and increase exactly as `ADR-022` already specifies for any other Character edit.

## 4.3 After binding — the same aggregate through submit/review/approve/Active

`SubmitCharacterDraft`, `AddCharacterReviewComment`, and `ApproveCharacterDraft` (section 7) all address the **same** `CharacterId` created by `BindDraftToCampaign`. `ApproveCharacterDraft` changes `LifecycleStatus: Draft → Active` and `ApprovalState: Draft → Approved` on that same instance — it does not mint a new `CharacterId`, does not create a second aggregate, and does not require an identity-transfer mechanism. This directly matches product §7.3's plain reading: a permanent `CharacterId` is assigned "if the draft was local" (i.e., at the binding moment for a Draft that went through the local phase) — a Draft created directly inside a campaign without a local phase (for example, a GM drafting an NPC) already has its `CharacterId` from creation and simply reaches the same `Draft` lifecycle state by a shorter path.

## 4.4 A GM-created, never-local Draft

Nothing in this ADR requires every campaign Draft to have passed through a local phase. A GM (or any actor with `Character.CreateDraft` inside a campaign, e.g. for an NPC) may create a Character directly with `LifecycleStatus=Draft` inside the campaign boundary — architecturally this is simply `BindDraftToCampaign` invoked without a preceding `CreateLocalCharacterDraft`, and section 4.2's aggregate-creation contract applies identically.

---

# 5. `CharacterTemplate` storage, scope, and independent copy

## 5.1 One aggregate type, two scopes

`PersonalCharacterTemplate` and `CampaignCharacterTemplate` are **not** two different aggregate types. They are the single `CharacterTemplate` aggregate the product schema already defines (§9.1), distinguished by `TemplateScope` (§9.2):

```text
TemplateScope = Personal   -- OwnerUserId set, no CampaignId; usable across campaigns after compatibility validation
TemplateScope = Campaign   -- CampaignId set, has a VisibilityAudience; scoped to one campaign
```

`CharacterTemplate` has its own `Version`/`Status`/`Revision` (product §9.1), following `ADR-002`'s normal aggregate-revision optimistic-concurrency pattern for its own edits (`UpdateCharacterTemplate`, `ArchiveCharacterTemplate`) — this revision counter is entirely independent of any Character's `CharacterRevision`.

## 5.2 Storage boundary

A `Personal`-scope `CharacterTemplate` lives in the same personal/local-profile storage boundary as a local Draft (section 4.1) — it is not campaign-authoritative data, and creating/updating it does not require an active campaign session. A `Campaign`-scope `CharacterTemplate` lives inside the owning campaign's authoritative storage as its own campaign-scoped aggregate — a sibling of Character, not a section of it.

## 5.3 The independent-copy mechanism

**Decision:** deep value copy with fresh nested identifiers, template reference kept only as immutable provenance.

When a Draft/Character is created from a `CharacterTemplate` (at `CreateLocalCharacterDraft` for a `Personal` template, or at `BindDraftToCampaign` for a `Campaign` template — or both, if a local Draft created from a `Personal` template is later bound), the command handler:

1. reads the template's current seed data (`AttributeSeeds`, `SkillSeeds`, `AbilitySeeds`, `ResourceSeeds`, `StartingContentRefs`, `CustomFieldDefinitions`, `RequiredFieldRules`) as of that moment;
2. copies each value into the new Draft/Character's own owned section state;
3. mints a **fresh identifier** for every nested instance the copy creates (a new `CharacterSkillId` for each copied skill seed, a new `CharacterAbilityId` for each copied ability seed, and so on) — never reusing a template-scoped identifier as a Character-scoped one;
4. records `TemplateId` and the template's `Version` **at copy time** as `TemplateVersion` — an immutable, non-resolved provenance pair on the Draft/Character, matching product §7.3's "TemplateId, TemplateVersion сохраняются."

Because the copy is by value with fresh identifiers, and the template reference is never re-read to render or validate an already-created Character, a later `UpdateCharacterTemplate` on the source template has **zero effect** on any Character already created from it. This is what makes `CAP-INV-006` true as an architectural consequence, not merely an unenforced product statement — there is no code path in this design that resolves `TemplateId` back into live template state for an existing Character.

## 5.4 Compatibility validation is a template-application concern, not a template-storage concern

A `Personal`-scope template may be applied in a different campaign than the one it was first used in (product §9.2) — this is exactly why compatibility validation (section 6) exists as a separate, explicit step rather than being folded into template storage rules: the same template row can be validated compatible with one campaign and incompatible with another at different times.

---

# 6. Compatibility validation

**Decision:** synchronous validation and pinning at `BindDraftToCampaign`, not deferred to `ApproveCharacterDraft`.

## 6.1 What is validated

At `BindDraftToCampaign`, given the target `CampaignId` and the resolved template's `RulesetRef` (if a template was used), the host validates that the template's ruleset reference is usable with the campaign's own pinned ruleset (the same `RulesetId`/`RulesetVersion` concept the campaign itself was created with — already an existing `Odyssey.Persistence`/`Odyssey.Application` concept, not introduced here). This is a deterministic, rules-catalog-driven check, not a GM judgment call.

## 6.2 What is pinned

On success, the resulting Character's `RulesetVersion` is set to the **campaign's own current ruleset version at that moment** — not necessarily the template's own recorded version, and not a live pointer that is re-evaluated later. This satisfies `CAP-INV-010` ("Ruleset version pinned") at the Draft-creation boundary. If the campaign's ruleset later changes, the pinned `RulesetVersion` on this Character does not silently follow it; reconciling that drift is `ADR-025`'s Character Ruleset-migration concern (`docs/tasks/SLICE-04_BACKLOG.md` §3.5), explicitly not decided here.

## 6.3 Why at bind, not at approve

Product §8.1 places "host validates compatible payload" as its own step, before "Draft is submitted" and before GM review/approve. Validating early:

- gives the player fast feedback before investing time filling out a Draft against an incompatible template/campaign pairing;
- avoids spending GM review effort on a submission that could never have legally bound to the campaign in the first place;
- keeps the check a deterministic host-side gate, separate from the GM's own judgment-based review (comments, approve).

`SubmitCharacterDraft` and `ApproveCharacterDraft` still perform their own ordinary precondition/revision checks per `ADR-002`'s command-processing pipeline (section 11 of that ADR) — this is routine command-handler discipline already required generally, not a second, redundant compatibility re-validation invented by this ADR.

---

# 7. Submit/review/comment/approve command/event flow

**Decision:** exactly the command/event set the product specification already names (§27/§28), carried on `ADR-002`'s envelope and `ADR-022`'s section revisions/event snapshots. No parallel mechanism, no invented `Reject`/`ChangesRequested` state.

## 7.1 Commands

```text
CreateLocalCharacterDraft   -- local boundary (section 4.1); optional TemplateId/TemplateVersion (Personal scope)
BindDraftToCampaign         -- creates the ADR-022 Character aggregate instance (section 4.2); performs compatibility validation (section 6)
SubmitCharacterDraft        -- campaign boundary; ApprovalState remains Draft (product §7.2); makes the Draft visible to GM review
AddCharacterReviewComment   -- campaign boundary; does not change ApprovalState/LifecycleStatus (product §8.4)
ApproveCharacterDraft       -- campaign boundary, MainGM-only (section 7.3); LifecycleStatus Draft->Active, ApprovalState Draft->Approved
```

`CreateLocalCharacterDraft` uses the local-boundary command path (section 4.1). The remaining four commands are ordinary `ADR-022`-scoped Character commands: they declare `ExpectedSectionRevisions[]` for the sections they touch, exactly as any other Character command does, with two clarifications specific to this workflow:

- `SubmitCharacterDraft` declares the expected revisions of whichever sections it is marking ready for review — it is a light revision check, not a lock, and does not change `ApprovalState`.
- `AddCharacterReviewComment` requires no `ExpectedCharacterRevision`/section revision at all. A review comment is a conflict-free append (multiple comments may be added in parallel without conflicting), architecturally the same shape as a `GameLogEntry` append (`ADR-002` §17.1) rather than a section edit. It needs no new `ADR-022` section-lock key.
- `ApproveCharacterDraft` declares the expected `LifecycleRevision` (and any other section revisions the review actually depended on) so a concurrent edit the GM has not seen cannot be silently approved.

## 7.2 Events

```text
CharacterDraftSubmitted     -- already named product §28; ADR-022 minimum historical snapshot (section 7)
CharacterReviewCommentAdded -- already named product §28; carries CommentId/AuthorUserId/Text, no state-field snapshot needed beyond ADR-022's common fields
CharacterApproved           -- already named product §28; LifecycleStatusBefore=Draft, LifecycleStatusAfter=Active, approving actor/time via the standard ADR-002 event Actor/OccurredAtHost fields
```

`BindDraftToCampaign`'s own creation event reuses `ADR-022`'s minimum historical snapshot fields (section 7 of that ADR) even though it is a creation rather than an edit — `PreviousCharacterRevision` is absent (there is no prior revision), `NewCharacterRevision` is the aggregate's first revision.

## 7.3 Roles — reusing `ADR-019`, not redefining it

`ADR-019`'s accepted baseline has exactly three roles (`MainGM`, `Player`, `Observer`), no `AssistantGM`, and no delegation model (`ADR-019` §10). This ADR does not add a fourth role or a delegation mechanism. Applying that baseline to this workflow:

- **`Character.CreateDraft` / `SubmitCharacterDraft` / editing one's own still-`Draft` Character's identity/presentation/custom fields:** available to any campaign member with the `Player` role, for their own Draft. `ADR-019` §5.2 frames Player action-access around an *already-assigned* character; creating a brand-new Draft necessarily precedes any character-assignment (assignment happens at `ApproveCharacterDraft`, or, for a GM-drafted NPC, at draft creation itself). This ADR treats draft-creation/submission/self-editing as an ordinary campaign-membership-level Player action, not gated on a pre-existing assignment — `ADR-019` left this adjacent case open (it explicitly deferred the full ownership/assignment model, `ADR-019` §10), and this is the smallest necessary clarification for this ADR's own scope; it does not decide or extend `ADR-019`'s general ownership/control model, which remains `ADR-025`'s job.
- **`Character.Approve`:** MainGM-only under the currently accepted baseline. Product §26 does not list `Character.Approve` among its explicitly MainGM-only-in-MVP bullet list, and separately mentions that approval "may be delegated to AssistantGM if CampaignPolicy allows it" — but since `ADR-019` models no `AssistantGM` role and no delegation mechanism at all, no other role in the current baseline has any approval-granting capability. `Character.Approve` is therefore MainGM-only as a direct consequence of `ADR-019`'s already-accepted three-role model, not a new restriction invented here. Delegated approval to an `AssistantGM` remains unimplementable until a future amendment to `ADR-019` itself introduces that role/delegation — this ADR does not attempt it.
- **`AddCharacterReviewComment`:** available to MainGM (review authority, already covered by `ADR-019`'s full MainGM access) and to the Draft's own author/owner-to-be (so the player can respond in the same thread). No new permission constant is required beyond what product §26 already names — `Character.View` already authorizes reading the thread, and the actor's own authorship of the Draft already authorizes adding to it.

## 7.4 No `Reject`/`ChangesRequested` command or state

Product §7.2 states plainly that `Submitted`, `ChangesRequested`, and `Rejected` are not stable Character states — submission and review feedback are commands/comments while the Character remains `ApprovalState=Draft`. This ADR does not introduce a `RejectCharacterDraft` command or a `ChangesRequested` value. A GM "sends a Draft back" simply by adding a review comment and not calling `ApproveCharacterDraft`; the player edits the still-`Draft` Character and may call `SubmitCharacterDraft` again. This keeps the model exactly as small as the product specification already fixed it.

---

# 8. Не входит в ADR-023

Явно исключено из объёма этого ADR:

- **Character aggregate boundary/section locks/history** — already decided, `ADR-022` (`ODY-S04-001`), not reopened.
- **Development economy, points, purchases, critical evidence, advancement revert, and respec** — future `ADR-024` (`ODY-S04-003`).
- **Full ownership/control/lifecycle operation contract, physical delete, Dead/restore, and Character Ruleset migration** — future `ADR-025` (`ODY-S04-004`), including the exact mechanism for reconciling a pinned `RulesetVersion` against a campaign's later ruleset change.
- **Ability/resource/anatomy mechanics** — already closed without a new prerequisite ADR, `SLICE-04_BACKLOG.md` §3.4.
- **`AssistantGM` role or any delegation mechanism for `Character.Approve`** — remains `ADR-019`'s own future amendment scope, not decided or worked around here.
- **Concrete UI for the approval screen, review-comment thread rendering, or template picker** — product/UX concern, not an architectural boundary.
- **Concrete database schema for Draft/`CharacterTemplate` tables** — implementation task under `ADR-003`, `ADR-011`, `ADR-012`, `ADR-022`, and this ADR.
- **Concrete command/event payload DTO files, tests, Unity UI, or content catalogs.**

---

# 9. Соответствие module boundaries (`ADR-001`) and existing ADRs

This ADR does not introduce code, but future implementation must preserve these boundaries:

- `Odyssey.Domain` owns local-Draft and `CharacterTemplate` invariants, the deep-copy-with-fresh-identifiers semantics, and Character-creation domain event payload semantics. It remains serializer-free and Unity-free.
- `Odyssey.Application` owns `CreateLocalCharacterDraft`/`BindDraftToCampaign`/`SubmitCharacterDraft`/`AddCharacterReviewComment`/`ApproveCharacterDraft` command handlers, compatibility-validation orchestration, permission checks against `ADR-019`'s existing roles, and transaction orchestration reusing `ADR-022`'s aggregate/section-revision contract for every campaign-bound command.
- `Odyssey.Persistence` owns the physical local-Draft/`CharacterTemplate` tables and the Character tables `ADR-022` already assigns to it. It does not decide compatibility, copy semantics, or approval legality.
- `Odyssey.Networking` owns transport/redaction/reconnect delivery of Draft/review/approval projections. It does not receive raw local-Draft payloads as an unrestricted client-to-client channel and does not decide permission.
- `Odyssey.Unity.Client` owns local Draft forms, template pickers, and review-comment UI. It does not store campaign-authoritative Character state and does not resolve a live template reference on its own.

Relationship to existing ADRs:

- `ADR-002` remains authoritative for command identity, idempotency, event batches, revision checks, and duplicate command behavior — reused unmodified for every command in section 7.
- `ADR-003` remains authoritative for versioned DTOs and event payload bytes — the local-Draft payload and `CharacterTemplate` seed data are versioned DTOs under this ADR, not a new serialization mechanism.
- `ADR-019` remains authoritative for the three-role baseline and the explicit deferral of `AssistantGM`/delegation — this ADR only clarifies how `Character.CreateDraft`/`SubmitDraft` apply to a not-yet-assigned Character and confirms `Character.Approve` is MainGM-only under that baseline; it does not extend or redefine `ADR-019`.
- `ADR-022` remains authoritative for the Character aggregate boundary, section revisions/locks, and event historical snapshot minimum — every campaign-bound command in section 7 is an ordinary `ADR-022` Character command.

---

# 10. Правила для Codex

Codex обязан:

1. Model a local Draft (before `BindDraftToCampaign`) as a client/personal-profile-boundary record, not as an `ADR-022` Character aggregate instance; do not assign a `CharacterId` before binding.
2. Create exactly one, permanent `ADR-022` Character aggregate instance at `BindDraftToCampaign`; do not mint a second `CharacterId` at `ApproveCharacterDraft`, and do not build a separate "Draft aggregate" that is converted or replaced at approval.
3. Model `PersonalCharacterTemplate`/`CampaignCharacterTemplate` as the single `CharacterTemplate` aggregate distinguished by `TemplateScope`; do not create two separate aggregate types.
4. Implement template application as a deep value copy with freshly minted nested identifiers, recording `TemplateId`/`TemplateVersion` only as immutable provenance; do not store or resolve a live reference back to the template for an already-created Character.
5. Perform compatibility validation and `RulesetVersion` pinning at `BindDraftToCampaign`; do not defer it to `ApproveCharacterDraft`, and do not silently re-pin `RulesetVersion` if the campaign's ruleset changes later (that is `ADR-025`'s job).
6. Implement exactly the commands/events in section 7.1/7.2; do not introduce a `Reject`/`ChangesRequested` command or state.
7. Enforce `Character.Approve` as MainGM-only under `ADR-019`'s current baseline; do not implement `AssistantGM`-delegated approval without a prior `ADR-019` amendment.
8. Require no `ExpectedCharacterRevision`/section revision for `AddCharacterReviewComment`; do not introduce a new `ADR-022` section-lock key for review comments.
9. Do not implement DevelopmentPool/economy, ownership/lifecycle/Ruleset-migration operations, or ability/resource/anatomy mechanics under this ADR task unless a later task explicitly scopes it.

---

# 11. Definition of Done для будущей implementation-задачи

Implementation tasks using this ADR must prove, with tests where applicable:

1. A local Draft created without a campaign has no `CampaignId`/`CharacterId` and is not visible through any campaign-scoped query.
2. `BindDraftToCampaign` creates exactly one Character aggregate instance with `LifecycleStatus=Draft`, `ApprovalState=Draft`, and a pinned `RulesetVersion`; the same `CharacterId` persists unchanged through `SubmitCharacterDraft`, `AddCharacterReviewComment`, and `ApproveCharacterDraft`.
3. Two Characters created from the same `CharacterTemplate` do not share any nested instance identifier (skill/ability/resource), and editing the template afterward does not change either already-created Character.
4. Binding a Draft against an incompatible campaign/template ruleset pairing is rejected before any Character aggregate is created, with no partial state.
5. `ApproveCharacterDraft` from a non-MainGM actor is rejected under `ADR-019`'s existing permission-check mechanism, with no state change.
6. `AddCharacterReviewComment` does not change `ApprovalState`/`LifecycleStatus` and does not require a section-revision check to succeed when added by an authorized actor.
7. A duplicate `ApproveCharacterDraft`/`SubmitCharacterDraft`/`BindDraftToCampaign` command with the same `CommandId` returns the stored result and does not reapply the effect (`ADR-002`).
8. Core Draft/template/approval logic compiles without Unity dependencies in the pure .NET path.

---

# 12. Рассмотренные альтернативы

## 12.1 Draft as a separate aggregate type vs Draft as a state of the `ADR-022` Character aggregate

**Considered (a):** treat everything, including the pre-binding phase, as a state of the `ADR-022` Character aggregate from the first keystroke. **Rejected** — it would force campaign-authoritative persistence and a permanent `CharacterId` for data the product explicitly allows to be lost with local storage (§8.3), and it would require choosing a campaign before a player has necessarily chosen one, contradicting §8.1's own step ordering (create Draft, *then* select campaign and template).

**Considered (b):** treat the entire Draft lifecycle, even after campaign binding, as a wholly separate `DraftAggregate` type distinct from `Character`, converted/promoted into a `Character` at `ApproveCharacterDraft`. **Rejected** — it would require inventing an identity-transfer mechanism at approval time, contradicts product §7.3's plain reading that the same `CharacterId` persists across Draft and Active, and would duplicate `ADR-022`'s revision/lock/event-snapshot machinery for a second aggregate type that converges with Character immediately after approval anyway.

**Accepted:** the hybrid in section 4 — a non-aggregate local Draft before binding, the same `ADR-022` Character aggregate instance (in its already-reserved `Draft` `LifecycleStatus`) from binding onward.

## 12.2 Deep copy at Draft creation vs lazy reference to the template

**Considered:** store only a `TemplateId` reference on the Draft/Character and resolve the template's current seed data on demand whenever the Character is rendered, edited, or validated. **Rejected** — it directly violates `CAP-INV-006` the moment the template is edited: every Character created from it would silently change. "No live binding" (product §9.3) would become an unenforced convention rather than an architectural guarantee.

**Accepted:** deep value copy with fresh nested identifiers at creation time, template reference kept only as immutable provenance (section 5.3).

## 12.3 Synchronous compatibility validation at bind/submit vs deferred validation at approve

**Considered:** defer ruleset-compatibility checking entirely until `ApproveCharacterDraft`, alongside the GM's own judgment-based review. **Rejected** — it wastes GM review effort on submissions that could never have legally bound to the campaign, gives the player no early feedback, and contradicts §8.1's own step ordering, which places host validation before submission.

**Accepted:** synchronous validation and `RulesetVersion` pinning at `BindDraftToCampaign` (section 6), with ordinary per-command precondition re-validation (not a second compatibility check) at later steps per `ADR-002`'s normal pipeline discipline.

## 12.4 Inventing a `RejectCharacterDraft` command / `ChangesRequested` state

**Considered:** add an explicit `RejectCharacterDraft` command and/or a `ChangesRequested` `ApprovalState` value to make GM feedback a first-class state transition. **Rejected** — product §7.2 explicitly states these are not stable Character states; adding them would be inventing scope beyond what the product specification already fixed, contradicting this ADR's own mandate to decide only the open architectural questions, not add new product behavior.

**Accepted:** review feedback is `AddCharacterReviewComment` while `ApprovalState` remains `Draft`; a GM withholds `ApproveCharacterDraft` to signal "not yet."

---

# 13. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- concrete DevelopmentPool/economy contracts are `ADR-024`;
- ownership/lifecycle/Ruleset-migration operation contracts, including reconciling a pinned `RulesetVersion` against a later campaign ruleset change, are `ADR-025`;
- `AssistantGM` role and approval-delegation mechanics remain a future `ADR-019` amendment, not this ADR's scope;
- concrete local-Draft/`CharacterTemplate` table schema and DTO implementation belong to later implementation tasks.

---

# 14. Трассировка

ADR реализует и уточняет:

- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.4 Drafts and templates, §13.8 steps 1–5, and §13.9's independent-template-copy exit criterion;
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7 (lifecycle/`ApprovalState`/Draft-Active), §8 (player character creation, minimum fields, unconfirmed draft, review comments), §9 (templates, `TemplateScope`, independent copy), §20 (confirmed unchanged, reused as-is from `ADR-022`), §26–28 (permissions/commands/domain events used for this workflow);
- `docs/tasks/SLICE-04_BACKLOG.md` §3.2, closing the second prerequisite ADR slot.

Existing ADRs reused without redefinition:

- `ADR-001` for module boundaries;
- `ADR-002` for command/event/idempotency/revision foundations;
- `ADR-003` for versioned durable DTOs;
- `ADR-019` for the three-role permission baseline and its explicit deferral of `AssistantGM`/delegation;
- `ADR-022` for the Character aggregate boundary, section revisions/locks, and event historical snapshot minimum that every campaign-bound Draft command joins.

Related future tasks:

```text
ODY-S04-003  ADR-024: Development Economy and Progression Transactions
ODY-S04-004  ADR-025: Character Ownership, Lifecycle, and Ruleset Migration Operations
```

---

# 15. Нормативное действие

Принято как ADR этой задачи (`ODY-S04-002`) без ожидания технического спайка — обоснование: задача разрешает границы модели/контракта поверх уже принятых command, permission, и aggregate-boundary субстратов (`ADR-002`, `ADR-019`, `ADR-022`); ни один эмпирический неизвестный фактор не виден до реализации — то же обоснование, которым уже руководствовалась `ADR-022` при принятии до какого-либо спайка для этой же серии задач `SLICE-04`.

С даты принятия (`Accepted`):

- `SLICE-04` Character implementation tasks must model the pre-binding phase as a non-`ADR-022` local Draft, and must create exactly one permanent Character aggregate instance at `BindDraftToCampaign`, carried unchanged through submit/review/approve;
- `PersonalCharacterTemplate`/`CampaignCharacterTemplate` must be implemented as one `CharacterTemplate` aggregate type distinguished by `TemplateScope`, never as two independent aggregate types;
- template application must use the deep-copy-with-fresh-identifiers mechanism from section 5.3; a live/lazy template reference for an already-created Character is an architectural defect under this ADR;
- compatibility validation and `RulesetVersion` pinning must occur at `BindDraftToCampaign`, not deferred to `ApproveCharacterDraft`;
- the command/event set is limited to section 7.1/7.2; no `Reject`/`ChangesRequested` command or state may be added without amending this ADR;
- `Character.Approve` remains MainGM-only until a future `ADR-019` amendment introduces `AssistantGM`/delegation;
- changing this Draft/template/approval boundary requires an amendment or superseding ADR, not silent implementation drift.

---

**Конец документа**
