# ODY-S05-110 — Decompose Item-Sourced Abilities/Effects Block

**Status:** Done (PR #134, merged into main)
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-110-decompose-abilities-effects-block`
**Pull request:** [odyssey-services/Odyssey_VTT#134](https://github.com/odyssey-services/Odyssey_VTT/pull/134) (Draft)
**Plan:** `docs/plans/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md` (Brief plan)
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Decompose the reserved "item-sourced abilities/effects runtime" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 (per the product owner's own chosen block order: migration block first, then this block, then the full attack pipeline). Unlike `ODY-S05-107`/`108`/`109`'s own decompositions, this block cannot be decomposed straight into implementation tasks — `ADR-027` §8 specifies only how `ActiveEffect` *integrates* with items, never the `ActiveEffect` aggregate itself. This task therefore decomposes the block into two tiers: one dedicated ADR-specification task (`ODY-S05-501`), scoped now, and the block's remaining implementation tasks, deliberately reserved but **not** decomposed by this revision. This task is docs/planning-only: it does not implement any runtime code, and it does not itself write or edit any ADR — that is `ODY-S05-501`'s own job.

## 2. Why this task exists

- Problem or dependency being addressed: the `ItemDefinition` migration block (`ODY-S05-401`–`404`) is fully merged into `main` (PR #130–#133); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names item-sourced abilities/effects as reserved but not decomposed. Direct re-reading of `ADR-027` §8.1/§8.2 confirmed it specifies only the item-integration boundary (creation trigger, reference-only storage, `WhileItemEquipped` lifecycle coupling), never the `ActiveEffect` aggregate's own field shape, persistence, stacking-rule integration, duration-expiry mechanism for the other 11 `EffectDurationType` values, removal command, or authorization model.
- Value or risk reduction: prevents a future implementation task from silently deciding ADR-level architecture (effect stacking/duration/removal semantics, permissions) inline while trying to implement a concrete feature — the exact anti-pattern `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4 already forbids ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline").
- Blocking or enabling relationship: follows the merged `ItemDefinition` migration block (PR #130–#133) and enables a future `ODY-S05-501` ADR task contract; that task, once accepted, in turn enables a future backlog revision to decompose `ODY-S05-502` onward into real implementation tasks.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md` (and its Brief plan) — the structural template this task follows near one-to-one, adapted for the two-tier (ADR-then-implementation) decomposition shape this block requires that `109`'s block did not.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially §13/§13.1 (the immediately prior decomposition precedent), §8 (reserved future blocks, as it existed before this task), §9 (global non-goals), §10 (dependency rules), §11 (backlog change control / reserved number ranges), §4 ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline").
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, full re-read of §8.1/§8.2 (item-sourced abilities/effects, verbatim source for this decomposition's central finding), §9 (closure of `SLICE-04` stubs — for context on how this ADR frames a documented future-work boundary generally), §10/§11 (`ItemDefinition` migration and its explicit `ActiveEffect`-never-mass-migrates distinction — the only other place this ADR mentions `ActiveEffect`), §12 (permissions baseline — cited as the precedent this block's own future ADR task must explicitly decide whether to follow or diverge from).
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs` — read directly to verify `SourceKind.ActiveEffect = 5`'s exact doc-comment wording, not paraphrased from memory.
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` — read directly to verify `EffectDefinition`'s, `ItemDefinition.BuiltInAbilityRefs`'s, and `ItemDefinition.BuiltInEffectRefs`'s exact doc-comment wording, and the full 15-value `EffectDurationType` enum, not paraphrased from memory.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-110`, `SLICE-05`; `ADR-027` §8.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `1dd40fe` (merge of PR #133, `ODY-S05-404`) — the `ItemDefinition` migration block is fully closed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8, before this task, named two reserved-but-undecomposed blocks: item-sourced abilities/effects runtime, and the full attack pipeline. This task removes only the first bullet, replacing it with a pointer to the new §14.
- `ADR-027` §8, read in full and quoted verbatim below and in this task's backlog update:

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

- **Central finding (the reason this decomposition differs structurally from `ODY-S05-109`'s own): `ADR-027` §8 is incomplete as a specification to decompose directly.** Unlike §10 (`ItemDefinition` migration), which gives a complete 9-step workflow and blocking-incompatibility list, §8 only ever describes how `ActiveEffect` *integrates* with items and Characters — it never specifies: the `ActiveEffect` aggregate's own persisted field shape or repository contract; how repeated application of the same `EffectDefinitionRef` against the same target interacts with the already-existing `EffectStackPolicy` vocabulary (`TypedDefinitions.cs`'s `EffectStackPolicy` enum: `IndependentInstances`/`RefreshDuration`/`ReplaceIfStronger`/`ReplaceExisting`/`IncreaseStacks`/`IgnoreNewApplication`/`RequestGMResolution`); how any of the 11 non-`WhileItemEquipped` `EffectDurationType` values (`ForRounds`/`ForTurns`/`ForDuration`/`UntilSceneChange`/`UntilSessionEnd`/`UntilSourceTurnStart`/`UntilSourceTurnEnd`/`UntilTargetTurnStart`/`UntilTargetTurnEnd`/`WhileCondition`/`WhileSourceExists`, out of 15 total) actually expire — §8.2 rule 3 only ever specifies the `WhileItemEquipped` case; the command/event for explicit early removal and who may issue it; and whether creating/removing an `ActiveEffect` requires the same MainGM-only baseline `ADR-027` §12 already fixes for migration, or a broader, combat-usable right (relevant since effects plausibly need to be applied mid-combat by more than just MainGM).
- A repository-wide search for `class ActiveEffect`/`struct ActiveEffect`/`interface IActiveEffect` across every tracked `.cs` file found **zero matches** — no `ActiveEffect` type of any kind exists anywhere in the codebase today, confirmed by direct search, not assumed.
- A repository-wide search of every file under `docs/adr/*.md` for the string `"ActiveEffect"` found matches **only** in `ADR-027` — no other accepted ADR (022 through 026, or any earlier one) mentions `ActiveEffect` at all. `ADR-027` §8.2 and §11 (the migration-never-mass-migrates-`ActiveEffect` distinction) are the only two normative references anywhere, and both explicitly frame `ActiveEffect` as integration/boundary text around a future aggregate, never the aggregate's own specification.
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs`'s `SourceKind` enum (`ODY-S04-108`), read directly: `ActiveEffect = 5` is one of six values, with the enum's own doc comment stating verbatim that `CharacterTemplate`/`Item`/`ActiveEffect`/`RulesetAdvancement` "are structurally accepted by `AcquireAbility`... but this task implements no automatic acquisition through them."
- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`, read directly:
  - `EffectDefinition` (`ODY-S05-105`) carries `TargetRule`/`DurationType`/`DurationValue`/`StackPolicy`/`MechanicsPayloadRef`; its own doc comment states verbatim that `MechanicsPayloadRef` is "the snapshot-relevant mechanics placeholder `ADR-027` section 6's `DefinitionMechanicsSnapshot`/future `ActiveEffect.EffectMechanicsSnapshot` will eventually copy from -- no `ActiveEffect` aggregate or snapshot-copy mechanism is implemented by this task."
  - `ItemDefinition.BuiltInAbilityRefs`'s own doc comment: "`ADR-027` section 8.1's `CharacterAbility SourceKind=Item` integration point for a future task; this task only proves the reference itself exists and round-trips."
  - `ItemDefinition.BuiltInEffectRefs`'s own doc comment: "`ADR-027` section 8.2's future `ActiveEffect` creation integration point; this task only proves the reference itself exists and round-trips."
  - `EffectDurationType` has exactly 15 values (confirmed by direct enumeration); `EffectStackPolicy` has exactly 7.
- Consequence: this decomposition cannot follow `ODY-S05-107`/`108`/`109`'s own single-tier pattern (decompose the whole block into implementation tasks immediately). Doing so would force whichever future task first implements `ActiveEffect` to decide, inline and without dedicated review, the exact architectural questions (stacking-integration precision, duration-expiry mechanism, removal authorization) `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4 explicitly requires be resolved by "a dedicated ADR task," not inline.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read`/repository-wide search against `origin/main` during this task.

## 5. Scope

### In scope

- Add this task contract.
- Add a Brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: remove "item-sourced abilities/effects runtime" from §8's reserved-but-undecomposed list (replacing it with a pointer to the new §14); add a new ordered-backlog section (§14) decomposing the block's first tier into exactly one task, `ODY-S05-501` (an ADR-specification task, not an implementation task), with a task-boundaries subsection (§14.1) mirroring §7.1/§12.1/§13.1's own structural depth; extend §9's non-goals, §10's dependency rules, and §11's reserved-number-range sentence additively (no rewrite of existing decisions).
- Reserve the task-ID range `ODY-S05-501`–`ODY-S05-50N` for the whole item-sourced abilities/effects block, explicitly recording in text that only `501` is decomposed by this revision — the remaining count and scope of implementation tasks in that range is a future backlog revision, not decided here.

### Out of scope

- Any C# production code.
- Any ADR document (creating or editing `docs/adr/**`) — that is `ODY-S05-501`'s own job, not this decomposition task's.
- Decomposing the block's implementation tasks (`ODY-S05-502` onward) into concrete task IDs — explicitly forbidden by this ТЗ, deferred to a future backlog revision once `501`'s own ADR is accepted.
- Any new tests requiring new runtime code.
- Unity/UI.
- The full attack pipeline — stays reserved and undecomposed, unchanged from the current §8, remaining the product owner's next block in sequence after this one.
- Rewriting `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions in §1–§7, §12, or §13 — only additive text (a new §14/§14.1, and additive sentences in §8/§9/§10/§11) is introduced.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md
docs/plans/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-404_* (already Done/In Review/completed, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; a future `ActiveEffect` implementation must follow `ADR-001` and whatever module-ownership rule `ODY-S05-501`'s own ADR fixes.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited by this task under any circumstance — that decision belongs entirely to `ODY-S05-501`, which this task only reserves the number for.

## 7. Expected behavior

### Scenario 1 — item-sourced abilities/effects block decomposed into two tiers

**Given** `ADR-027` §8 specifies only item/`ActiveEffect` integration, never the `ActiveEffect` aggregate itself
**When** this planning task updates the backlog
**Then** the block is decomposed into one ADR-specification task (`ODY-S05-501`) plus an explicitly-reserved, explicitly-undecomposed remainder — not a full set of implementation task IDs.

### Scenario 2 — the ADR gap is verified, not assumed

**Given** the ТЗ's own claim that no `ActiveEffect` aggregate specification exists anywhere
**When** this task searches the tracked repository and every accepted ADR directly
**Then** the decomposition records, with search evidence, that no `ActiveEffect` type exists in code and no ADR besides `ADR-027` mentions it, and cites the exact existing placeholder doc comments (`Ability.cs`'s `SourceKind`, `TypedDefinitions.cs`'s `EffectDefinition`/`BuiltInAbilityRefs`/`BuiltInEffectRefs`).

### Scenario 3 — the remaining implementation tasks stay genuinely undecomposed

**Given** this ТЗ's explicit prohibition on decomposing `502` onward into real task IDs
**When** this task writes §14
**Then** the table contains exactly one row (`501`), and the surrounding text states in plain language that the rest of the range is reserved-but-unscoped, to be decomposed by a future backlog revision.

### Scenario 4 — the Block 3 (attack pipeline) boundary is named for `501`'s own future ADR to inherit

**Given** damage-over-time and other combat-sourced effects plausibly overlap with the generic `ActiveEffect` mechanism
**When** this task scopes `ODY-S05-501`'s own primary result
**Then** it explicitly states that `501` specifies only the generic mechanism and its item integration (`ADR-027` §8.1/§8.2), not combat/damage-sourced effects, which remain Block 3's (full attack pipeline's) territory.

### Scenario 5 — later blocks stay deferred

**Given** the full attack pipeline is a separate, larger reserved block the product owner has sequenced after this one
**When** this task decomposes item-sourced abilities/effects
**Then** the full attack pipeline remains named and reserved, unchanged, in §8.

### Required invariants

- No product code, schema, runtime test implementation, or accepted ADR architecture section is changed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — only additive text is introduced, plus the one required §8 bullet replacement.
- The new §14 table names exactly one decomposed task (`501`); no row for `502` or higher with a real title/scope/dependency exists anywhere in this task's diff.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its Brief plan, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s item-sourced abilities/effects decomposition.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 no longer lists "item-sourced abilities/effects runtime" as an undecomposed reserved block; the full attack pipeline remains.
2. A new ordered-backlog section (§14) decomposes the block's first tier into exactly one task (`ODY-S05-501`, an ADR-specification task), at the same structural depth as §7/§7.1, §12/§12.1, and §13/§13.1, but explicitly not decomposing `502` onward.
3. The decomposition quotes `ADR-027` §8.1/§8.2 verbatim and names the specific architectural gaps (aggregate shape/persistence, stacking integration, non-`WhileItemEquipped` duration expiry, removal command/authorization) that make this block's own ADR §8 insufficient to decompose directly, unlike §10 for the migration block.
4. The task contract states, with direct search evidence, that no `ActiveEffect` type exists anywhere in code and no ADR besides `ADR-027` mentions `ActiveEffect`, citing the exact existing placeholder doc comments in `Ability.cs`/`TypedDefinitions.cs`.
5. `ODY-S05-501`'s own scope explicitly draws the boundary against Block 3 (full attack pipeline) — generic mechanism and item integration only, not combat/damage-sourced effects.
6. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — diff confined to additive text plus the one §8 bullet replacement.
7. No product code, schema, or test implementation is added.
8. No file under `docs/adr/**` is changed.
9. This task contract and Brief plan are added.
10. `git diff --name-status` against `main` shows only §5's allowed paths.
11. PR description states clearly that this is planning/decomposition only, with no implementation and no ADR content.

## 10. Tests and validation

### Required automated tests

None. This is a docs/planning-only decomposition task and does not add runtime behavior.

### Required commands

```powershell
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the allowed documentation/planning files changed.
- Confirm `ADR-027` §8.1/§8.2 were read in full and are quoted accurately (byte-for-byte) in this contract and the backlog update.
- Confirm the "`ActiveEffect` does not exist / no other ADR mentions it" finding was verified directly against source (repository-wide search), not assumed from the ТЗ.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`: not required on its own merits, but `dotnet test` is run anyway as an extra confirmation that a docs-only diff leaves the whole suite green, matching `ODY-S05-109`'s own practice of running the full validation set even for a planning-only task.
- Unity validation: not required — no Unity files change.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this documentation-only branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository planning documentation only.
- Trust boundaries: no runtime trust boundary is changed.
- Authorization / audience checks: not implemented here; `ODY-S05-501`'s own future ADR must explicitly decide whether `ActiveEffect` creation/removal follows `ADR-027` §12's MainGM-only baseline or a broader right.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, no permissions behavior, and no accepted ADR architecture change — identical reasoning to `ODY-S05-107`/`108`/`109`.
- Plan path: `docs/plans/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #133; the future `ODY-S05-501` ADR task starts once this decomposition is accepted; the full attack pipeline remains the product owner's next block in sequence after this one and `501`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this task's Brief plan.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`404` task contracts' own content.
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed; backlog `Last updated` remains 2026-09-12 UTC (already current).
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are correctly marked not applicable.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — removes "item-sourced abilities/effects runtime" from §8, replacing it with a pointer to §14; adds §14 (item-sourced abilities/effects ADR-foundation ordered backlog) and §14.1 (task boundaries); additive sentences in §9 (non-goals), §10 (dependency rules), and §11 (reserved number range).
- `docs/tasks/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md` — this task contract.
- `docs/plans/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md` — this task's Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors (harness projects included). |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 534/534 — identical count to before this docs-only diff. |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Met | §8's "item-sourced abilities/effects runtime" bullet replaced with a pointer to §14; the full attack pipeline bullet is untouched (confirmed via `git diff`). |
| AC-2 | Met | New §14/§14.1 decompose the block into exactly one task (`501`), at the same structural depth as §7/§7.1, §12/§12.1, §13/§13.1. |
| AC-3 | Met | §14's quote of `ADR-027` §8.1/§8.2 diffed byte-for-byte against the ADR source (only difference: the quote's trailing `---` vs. this document's own following commentary paragraph); the specific architectural gaps are named in the same paragraph. |
| AC-4 | Met | §4 above and §18 decision log record both repository-wide search findings (no `ActiveEffect` type in code; no ADR besides `ADR-027` mentions it) with exact evidence, plus the three placeholder doc-comment quotes. |
| AC-5 | Met | `ODY-S05-501`'s own table row and §14.1 explicitly name the Block 3 (full attack pipeline) boundary. |
| AC-6 | Met | `git diff` shows §1–§7, §12, §13 untouched; all edits are additive (new §14/§14.1, additive sentences in §8/§9/§10/§11). |
| AC-7 | Met | No `.cs`/`.json`/test file in the diff. |
| AC-8 | Met | No `docs/adr/**` file in the diff. |
| AC-9 | Met | This contract and Brief plan exist. |
| AC-10 | Met | `git diff --name-status` limited to §5's allowed paths (one backlog file + this task's own two new files). |
| AC-11 | Met | Draft PR body states planning/decomposition only, with no implementation and no ADR content. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves the future implementation-task range only; it does not create `ODY-S05-501`'s own task contract file (that is `501`'s own job once activated), nor does it decompose `502` onward.
- The exact form of the `ActiveEffect` aggregate is not decided here — `ODY-S05-501`'s own future ADR decides it.

### Follow-up tasks

- `ODY-S05-501` — ADR Addendum: ActiveEffect Aggregate Specification.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this Brief plan. No `Packages/**`, `DotNet/**`, `Tests/**`, or `docs/adr/**` file touched.
- Architecture review: no accepted ADR changed; decomposition follows `ADR-027` §8's own text and does not decide new runtime architecture — that is explicitly deferred to `ODY-S05-501`.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts run and recorded.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog additive edits only; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — Decision: use Brief plan, not ExecPlan, for this task itself — identical reasoning to `ODY-S05-107`/`108`/`109` (docs/planning-only, no public contract or ADR change). Authority: `PLANS.md` §1.1.
- 2026-09-12 — **Decision (the central architectural finding of this task, required by the product owner's own explicit instruction): the item-sourced abilities/effects block cannot be decomposed the way the migration block was, because `ADR-027` §8 is not a complete specification.** Direct re-read of §8.1/§8.2 confirmed it specifies only item/`ActiveEffect` integration rules, never the `ActiveEffect` aggregate's own field shape, persistence contract, stacking-rule integration, non-`WhileItemEquipped` duration-expiry mechanism, removal command, or authorization model. Decomposing straight into implementation tasks (as `107`/`108`/`109` did for their own, fully-specified blocks) would force a future implementation task to decide this architecture inline, which `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4 already forbids. Authority: `ADR-027` §8's own text (a negative finding — the absence of a workflow/list comparable to §10's); `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4's own explicit rule.
- 2026-09-12 — Decision: decompose this block into exactly one task now (`ODY-S05-501`, an ADR-specification task) and explicitly defer the rest of the block's decomposition to a future backlog revision, rather than guessing at implementation task boundaries (foundation/persistence/commands/fixtures) without the ADR that would make such a split well-founded. Authority: this ТЗ's own explicit two-tier decomposition instruction; direct analogy to how this same block was itself "named, not scoped" in §8 before today (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §11's own precedent sentence for reserved blocks).
- 2026-09-12 — Decision: `ODY-S05-501`'s own scope explicitly names the boundary against Block 3 (full attack pipeline) — it specifies only the generic `ActiveEffect` mechanism and item integration (`ADR-027` §8.1/§8.2), not combat/damage-sourced effects. Authority: this ТЗ's own explicit instruction to fix this boundary as text, not leave it implied; roadmap §14.6's own scoping of the attack pipeline as a separate vertical slice.
- 2026-09-12 — Decision: verify directly, not assume, that (a) no `ActiveEffect` type exists anywhere in the tracked codebase, and (b) no ADR besides `ADR-027` mentions `ActiveEffect` at all. Both repository-wide searches returned exactly the results this ТЗ's own §1 claimed, confirmed independently rather than copied from the ТЗ text. Authority: direct repository search, this session.
- 2026-09-12 — Decision: reserve `ODY-S05-501` through `ODY-S05-50N` (an open, not-yet-fixed count) for this block, rather than a closed range like the migration block's exact `401`–`404`. Rationale: unlike the migration block (whose 4-task shape could be inferred directly from `ADR-027` §10's own 9-step grouping before any implementation began), this block's implementation-task count cannot be responsibly fixed before `501`'s own ADR exists to decompose against. Authority: this ТЗ's own explicit `ODY-S05-501`–`ODY-S05-50N` phrasing; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §11's own existing convention of reserving a range before exact scope is known.
- 2026-09-12 — Decision: do not touch the pre-existing, already-stale "`ItemDefinition` migration workflow implementation (section 8)" non-goal bullet in §9, even though the migration block has since been decomposed into §13 and closed. Rationale: this ТЗ §7 restricts §9 edits to the one specific cross-reference it names (the "item use, ActiveEffect execution... (section 8)" line); fixing unrelated pre-existing drift is out of this task's own explicit scope, the same restraint `ODY-S05-109` applied to not performing unrelated `active/`→`completed/` housekeeping. Authority: this ТЗ §7's own explicit scope restriction.
- 2026-09-12 — Decision: extend `SLICE-05_IMPLEMENTATION_BACKLOG.md` additively (new §14/§14.1; additive sentences in §8/§9/§10/§11) rather than renumber existing sections. Authority: this ТЗ's own explicit instruction, consistent with `ODY-S05-107`/`108`/`109`'s own precedent.
- 2026-09-12 — Decision: do not edit `docs/adr/**`. Rationale: this is a decomposition task, not the ADR-writing task itself; writing or editing `ADR-027`/`ADR-028` is `ODY-S05-501`'s own job. Authority: this ТЗ §2's own explicit statement ("Эта задача не пишет production-код... ADR-документ для `501` пишется задачей `501`, не этой декомпозиционной задачей") and §7's explicit forbidden-path list.

### Approved task changes

- None.
