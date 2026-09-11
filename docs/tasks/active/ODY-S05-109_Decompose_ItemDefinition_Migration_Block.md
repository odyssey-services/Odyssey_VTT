# ODY-S05-109 — Decompose ItemDefinition Migration Block

**Status:** In Progress
**Roadmap stage / slice:** SLICE-05 (ItemDefinition migration planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `docs/ody-s05-109-decompose-item-definition-migration-block`
**Pull request:** Not opened
**Plan:** `docs/plans/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md` (Brief plan)
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Decompose the reserved "`ItemDefinition` migration preview/confirm" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 into small, reviewable implementation tasks, mirroring both `ODY-S05-107` and `ODY-S05-108`, since this is the third such decomposition in the series. This task is docs/planning-only: it does not implement any migration type, persistence, commands, tests, or Unity UI, and it does not edit any accepted ADR.

## 2. Why this task exists

- Problem or dependency being addressed: the Equipment runtime block (`ODY-S05-301`–`306`) is fully merged into `main`; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names `ItemDefinition` migration as reserved but not decomposed into executable tasks. The product owner has chosen the order for the three remaining reserved blocks: `ItemDefinition` migration first, then item-sourced abilities/effects, then the full attack pipeline.
- Value or risk reduction: gives future agents small, single-responsibility implementation contracts for migration preview construction, blocking-incompatibility rules, the confirm/apply command, and integration fixtures — without pulling in item-sourced abilities/effects or the attack pipeline, and without confusing this workflow with the deliberately different `ActiveEffect` non-migration rule (`ADR-027` §11).
- Blocking or enabling relationship: follows the merged Equipment runtime block (PR #123–#128) and enables future `ODY-S05-401`–`404` task contracts.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` and `docs/tasks/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md` (and their Brief plans) — the structural templates this task follows near one-to-one.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially §7/§7.1 and §12/§12.1 (the two prior decomposition precedents), §8 (reserved future blocks, as it existed before this task), §9 (global non-goals), §10 (dependency rules), §11 (backlog change control / reserved number ranges).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, full re-read of §9 (closure of `SLICE-04` stubs — for context on how this ADR frames "closing a documented stub" generally), §10 (`ItemDefinition` migration preview/confirm, verbatim source for this decomposition), §11 (`ActiveEffect` migration difference — the boundary this decomposition must not blur), §12 (permissions baseline — MainGM-only), §15 (Rules for Codex, item 11 — the implementation constraint this decomposition sequences toward), §16 (Definition of Done for future implementation tasks, items 1/2/3/10 — migration-specific).
- `Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`ApplyCharacterRulesetMigration`) — read directly to verify the structural-precedent claim in §4, not paraphrased from memory.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs`, `DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs` — read directly to confirm the existing `"ItemDefinitionMigration"` forbidden-fragment scope guards, which future `401`–`404` implementation tasks will need to narrow (not remove), the same pattern already used when `EquippedEntry`/`EquipmentCommandLedger` were introduced.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-109`, `SLICE-05`; `ADR-027` §10, §11.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `643b734` (merge of PR #128, `ODY-S05-306`) — the Equipment runtime block is fully closed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names three reserved-but-undecomposed blocks: item-sourced abilities/effects, `ItemDefinition` migration preview/confirm, and the full attack pipeline. This task removes only the second bullet. §12/§12.1 (Equipment runtime decomposition) are the most recent addition and unchanged by this task; §13/§13.1 are unused — the document ends at line 276 (§12.1).
- `ADR-027` §10 ("ItemDefinition migration preview/confirm"), read in full and quoted verbatim below and in this task's backlog update:

  > **Decision:** ItemDefinition migration is a MainGM-only preview/confirm workflow over runtime item snapshots. It is not automatic publication side effect and not database schema migration.
  >
  > Workflow:
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

- `ADR-027` §11 ("ActiveEffect migration difference"), read in full and quoted verbatim: existing `ActiveEffect` aggregates **never mass-migrate** to a new `EffectDefinition` (mid-duration/mid-combat/turn-timing/stacking state makes bulk rewriting unsafe); `ItemDefinition` migration is the opposite — explicit MainGM preview/confirm with blocked incompatibilities and runtime-state preservation is *only* possible because items are not mid-combat-timing-sensitive the way `ActiveEffect` is. This decomposition must name this distinction explicitly so `401`–`404`'s future implementer does not attempt to build a generic "migrate anything" mechanism that would also apply to `ActiveEffect`.
- `ADR-027` §12 ("Permissions baseline") rules 1-2, read in full: MainGM can publish `ItemDefinition` versions and run migration preview/confirm; AssistantGM may publish only where existing `Content.Publish` permission scope already allows it, but this ADR does **not** grant AssistantGM the right to run mass `ItemDefinition` migration under any circumstance — a distinct, narrower permission than publish itself.
- `ADR-027` §16 ("Definition of Done for future implementation tasks") items 1/2/3/10, read in full: (1) publishing a new version does not change existing `ItemInstance`/`ItemStack` snapshots; (2) archived/Published/referenced/runtime-referenced definitions remain loadable for existing runtime state/history/previews/migrations, even though physically-unused Drafts may be deleted; (3) confirmed migration updates all matching snapshots atomically and preserves runtime state; (10) a non-MainGM mass-migration attempt is rejected with no state change.
- A repository-wide search of every tracked `.cs` file for `"ItemDefinitionMigration"` and `"MigrationPreview"` found matches only inside two existing forbidden-fragment scope-guard test arrays — `SqliteInventoryRepositoryTests.cs` (`InventoryPersistence_IntroducesNoEquipmentActiveEffectAttackOrItemDefinitionMigrationTablesOrClasses`, array `{ "Equipment", "ActiveEffect", "Attack", "ItemDefinitionMigration" }`) and `InventoryCreationServiceTests.cs` (`InventoryCreationScope_DoesNotIntroduceLaterInventoryCapabilities`, array `{ "Transfer", "Equipment", "Attack", "ActiveEffect", "ItemDefinitionMigration" }`). **No `ItemDefinitionMigrationPreview` type, apply command, persistence table, or any other migration-specific code exists anywhere in the codebase** — a genuinely clean slate, confirmed by direct search, not assumed. Both arrays actively forbid such code from appearing today; whichever future task (`401`–`404`) first introduces migration-specific type/table names must narrow these guards, the same rolling-scope-guard pattern already used when `EquippedEntry`/`EquipmentCommandLedger` were introduced in `ODY-S05-301`/`302`.
- `RulesetMigrationRules.BuildPlan`/`ComputePreviewHash` (`Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs`) + `SqliteCharacterRepository.ApplyCharacterRulesetMigration` (`ODY-S04-113`), read directly: `CharacterRulesetMigrationPlan` carries `DefinitionMappings` (`RulesetDefinitionMapping` list), `UnresolvedDecisions` (`RulesetUnresolvedDecision` list, surfaced to the GM rather than silently dropped/guessed), three `ExpectedXRevision` fields (mechanics/abilities/resources) used as a confirm-time CAS guard, a `PreviewHash` computed from every input, and a derived `HasUnresolvedDecisions` flag. `ApplyCharacterRulesetMigration` takes the already-built plan directly as a parameter. This is a real, already-accepted preview/plan/apply-separation precedent for the shape `401`–`404` should follow **by analogy** — the domain is different (Character definition-category remapping vs. item mechanics snapshots), so the concrete fields/types must be reinvented for items, not copied verbatim. This is the same "cite the precedent, don't literally copy it" relationship `ODY-S05-108` had to `ODY-S05-107`.
- `TypedDefinitionCodec` already branches on `ContentDefinitionType` with `DecodeItem`/`DecodeWeapon`/`DecodeArmor` (each rejecting a type mismatch), and `CatalogValidationContracts` already branches per type with separate validators (`ValidateWeapon`/`ValidateArmor`/etc.) — confirmed by direct code read. `ADR-027` §10's own blocking-incompatibility list itself names type-specific conditions ("removed ammo type currently loaded" is Weapon-specific; "removed equipment slot currently occupied"/"removed armor/body-part coverage with runtime damage" are Armor-specific) alongside generic ones (reduced capacity, unrecognizable custom state, uncomparable hidden mechanics). **This decomposition must not treat "the definition changed" as one uniform check** — it reserves a dedicated task (`ODY-S05-402`) for the type-specific and generic incompatibility rules together, rather than folding them into the preview-construction task.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read`/repository-wide search against `origin/main` during this task.

## 5. Scope

### In scope

- Add this task contract.
- Add a Brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: remove "`ItemDefinition` migration preview/confirm" from §8's reserved-but-undecomposed list; add a new ordered-backlog section (§13) decomposing it into `ODY-S05-401`–`404`, with a task-boundaries subsection (§13.1) mirroring §7.1/§12.1; extend §9's non-goals and §10's dependency rules and §11's reserved-number-range sentence additively (no rewrite of existing decisions).
- Reserve task IDs `ODY-S05-401`–`404` for the future `ItemDefinition` migration implementation block.

### Out of scope

- Any C# production code: migration preview/plan types, persistence, commands, services.
- Any new tests requiring new runtime code.
- Unity/UI.
- Implementing the actual `ItemDefinitionMigrationPreview` construction, blocking-incompatibility computation, confirm/apply command, or integration fixtures.
- Editing `docs/adr/**`.
- Item-sourced abilities/effects (`ActiveEffect`) and the full attack pipeline — these stay reserved and undecomposed, unchanged from the current §8, and remain the product owner's next two blocks in sequence after this one.
- Rewriting `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions — only additive text (a new §13, and additive sentences in §9/§10/§11) is introduced.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md
docs/plans/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-306_* (already Done or In Review, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; future migration tasks must follow `ADR-001` and `ADR-027` §14 module-ownership rules (`Odyssey.Application` owns preview/confirm orchestration and transaction boundaries; `Odyssey.Persistence` owns physical tables for migration previews/reports).
- Authoritative-state and transaction boundary: no command/state behavior is implemented here; future confirm/apply commands must follow `ADR-002`/`ADR-012` (atomic transaction, backup-before-review).
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited unless a real contradiction is found; none was found (`ADR-027` §10 already fully specifies the target workflow this decomposition sequences toward).

## 7. Expected behavior

### Scenario 1 — `ItemDefinition` migration block decomposed

**Given** `ADR-027` §9/§10/§11/§12/§16 define the migration workflow, its permissions, and its Definition of Done
**When** this planning task updates the backlog
**Then** the `ItemDefinition` migration block is broken into small tasks covering preview construction, blocking-incompatibility rules, confirm/apply, and integration fixtures — each with purpose, dependencies, planning mode, and explicit in/out-of-scope boundaries.

### Scenario 2 — clean slate verified, not assumed

**Given** the ТЗ's own claim that no migration code exists yet
**When** this task searches the tracked repository directly
**Then** the decomposition records, with search evidence, that only two forbidden-fragment scope-guard arrays currently reference `"ItemDefinitionMigration"`, and no actual implementation exists — and that those guards will need narrowing (not removal) by whichever future task first introduces real migration code.

### Scenario 3 — type-specific incompatibility rules get their own task

**Given** `ADR-027` §10's blocking-incompatibility list names both Weapon-specific and Armor-specific conditions alongside generic ones
**When** this task decomposes the migration block
**Then** blocking-incompatibility computation is reserved as its own task (`ODY-S05-402`), separate from preview construction, so type-specific rules are not silently folded into a single "definition changed" check.

### Scenario 4 — `ActiveEffect` boundary named explicitly

**Given** `ADR-027` §11 states `ActiveEffect` instances never mass-migrate, unlike `ItemDefinition` runtime snapshots
**When** this task decomposes the migration block
**Then** this distinction is named in §13's intro, so a future implementer of `401`–`404` does not generalize the mechanism to `ActiveEffect`.

### Scenario 5 — later blocks stay deferred

**Given** item-sourced abilities/effects (`ActiveEffect`) and the full attack pipeline are separate, larger reserved blocks the product owner has sequenced after this one
**When** this task decomposes `ItemDefinition` migration
**Then** those two remain named and reserved, unchanged, in §8.

### Required invariants

- `ItemDefinition` migration remains a MainGM-only preview/confirm workflow over runtime snapshots, never automatic publication side effect and never database schema migration (`ADR-027` §10's own opening decision).
- No product code, schema, runtime test implementation, or accepted ADR architecture section is changed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — only additive text is introduced, plus the one required §8 bullet removal.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its Brief plan, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s `ItemDefinition` migration decomposition.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 no longer lists "`ItemDefinition` migration preview/confirm" as an undecomposed reserved block.
2. A new ordered-backlog section (§13) decomposes it into small implementation tasks (`ODY-S05-401`–`404`), each with purpose, dependencies, proposed planning mode, in-scope summary, and explicit out-of-scope boundaries, at the same structural depth as §7/§7.1 and §12/§12.1.
3. The decomposition preserves `ADR-027` §10's nine-step workflow and blocking-incompatibility list verbatim, and names §11's `ActiveEffect`-never-mass-migrates distinction explicitly.
4. The task contract states, with direct search evidence, that no migration-specific code exists anywhere today (a clean slate), and identifies the two existing forbidden-fragment scope guards that a future implementation task will need to narrow.
5. `RulesetMigrationRules`/`ApplyCharacterRulesetMigration` (`ODY-S04-113`) is named as the structural precedent for the preview/plan/apply separation, with an explicit note that its concrete fields/types must be reinvented for item snapshots, not copied.
6. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s existing recorded decisions are not rewritten — diff confined to additive text plus the one §8 bullet removal.
7. No product code, schema, or test implementation is added.
8. No file under `docs/adr/**` is changed.
9. This task contract and Brief plan are added.
10. `git diff --name-status` against `main` shows only §5's allowed paths.
11. PR description states clearly that this is planning/decomposition only, with no implementation.

## 10. Tests and validation

### Required automated tests

None. This is a docs/planning-only decomposition task and does not add runtime behavior.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the allowed documentation/planning files changed.
- Confirm `ADR-027` §9/§10/§11/§12/§15/§16 were read in full and are quoted/paraphrased accurately in this contract and the backlog update.
- Confirm the "clean slate" finding was verified directly against source (repository-wide search), not assumed from the ТЗ.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`/`dotnet test`: not required — no product code, test code, schema, contracts, or project files change.
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
- Authorization / audience checks: not implemented here; future migration tasks must preserve `ADR-027` §12 MainGM-only baseline (rule 1) and its explicit AssistantGM exclusion (rule 2).
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, no permissions behavior, and no accepted ADR architecture change — identical reasoning to `ODY-S05-107`/`108`.
- Plan path: `docs/plans/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #128; future `ItemDefinition` migration implementation starts at `ODY-S05-401`. The product owner has sequenced item-sourced abilities/effects and the full attack pipeline to follow after this block, not concurrently.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this task's Brief plan.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`306` task contracts' own content.
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed; backlog `Last updated` changes to 2026-09-12 UTC.
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

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — removes "`ItemDefinition` migration preview/confirm" from §8; adds §13 (`ItemDefinition` migration ordered backlog) and §13.1 (task boundaries); additive sentences in §9 (non-goals), §10 (dependency rules), and §11 (reserved number range); `Last updated` timestamp bumped.
- `docs/tasks/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md` — this task contract.
- `docs/plans/active/ODY-S05-109_Decompose_ItemDefinition_Migration_Block.md` — this task's Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.`; `REPO-POLICY-*`/`TC-CI-*` checks passed. |
| `.\scripts\verify-test-structure.ps1` | Passed | Exit code 0; `TC-ARCH-001 PASS valid ADR-001 graph passes`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | §8's "`ItemDefinition` migration preview/confirm" bullet removed (confirmed via `git diff` — the only bullet-level removal in the whole diff). |
| AC-2 | Passed | New §13/§13.1 decompose the block into `ODY-S05-401`–`404`, same structural depth as §7/§7.1 and §12/§12.1. |
| AC-3 | Passed | §13's intro quotes `ADR-027` §10's nine-step workflow and blocking-incompatibility list verbatim — programmatically diffed against the actual ADR text during authoring and confirmed byte-for-byte identical after fixing one missing blank blockquote line. |
| AC-4 | Passed | §4 above and §18 decision log record the repository-wide search finding (only two forbidden-fragment scope-guard arrays reference `"ItemDefinitionMigration"`; no real implementation exists) with exact file names. |
| AC-5 | Passed | §4/§18 name `RulesetMigrationRules.BuildPlan`/`ApplyCharacterRulesetMigration` (`ODY-S04-113`) as the structural precedent, with an explicit "cite, don't copy" caveat. |
| AC-6 | Passed | `git diff` shows §3 untouched; all edits are additive (new §13, additive sentences in §9/§10/§11) plus the one required §8 bullet removal and the `Last updated` metadata bump. |
| AC-7 | Passed | No `.cs`/`.json`/test file in the diff. |
| AC-8 | Passed | No `docs/adr/**` file in the diff. |
| AC-9 | Passed | This contract and Brief plan exist. |
| AC-10 | Passed | `git diff --name-status` limited to §5's allowed paths (one backlog file + this task's own two new files). |
| AC-11 | Passed | Draft PR body states planning/decomposition only. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves future implementation tasks only; it does not create the individual `ODY-S05-401`–`404` task contract files.
- The exact fields of a future `ItemDefinitionMigrationPreview` type are not decided here — `ODY-S05-401` decides them, using `RulesetMigrationRules`'s shape only as an analogy.

### Follow-up tasks

- `ODY-S05-401` — Migration Preview Foundation.
- `ODY-S05-402` — Migration Blocking Incompatibility Rules.
- `ODY-S05-403` — Migration Confirm/Apply Command.
- `ODY-S05-404` — Migration Integration Fixtures.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, and this Brief plan. No `Packages/**`, `DotNet/**`, `Tests/**`, or `docs/adr/**` file touched.
- Architecture review: no accepted ADR changed; decomposition follows `ADR-027` §9/§10/§11/§12/§16 and does not decide new runtime architecture.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts passed.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog `Last updated` changed to 2026-09-12 UTC; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — Decision: use Brief plan, not ExecPlan, for this task itself — identical reasoning to `ODY-S05-107`/`108` (docs/planning-only, no public contract or ADR change). Authority: `PLANS.md` §1.1.
- 2026-09-12 — **Decision (required by DoD): the codebase is a genuine clean slate for `ItemDefinition` migration.** A repository-wide search for `"ItemDefinitionMigration"`/`"MigrationPreview"` across every tracked `.cs` file found matches only in two existing forbidden-fragment scope-guard test arrays (`SqliteInventoryRepositoryTests.cs`, `InventoryCreationServiceTests.cs`), both of which currently *forbid* such code from existing. No `ItemDefinitionMigrationPreview` type, apply command, table, or any other migration-specific implementation exists anywhere. Recorded so the future `401` implementer knows to narrow (not remove) these two guards, the same pattern `ODY-S05-301`/`302` already established for `Equipped*` names. Authority: direct repository search, this session.
- 2026-09-12 — Decision: decompose the `ItemDefinition` migration block into four tasks (`ODY-S05-401`–`404`: Preview Foundation, Blocking Incompatibility Rules, Confirm/Apply Command, Integration Fixtures) rather than six, unlike Equipment runtime's six-task decomposition. Rationale: `ADR-027` §10 is the most procedurally self-contained and complete of the three remaining reserved blocks, with a direct, already-closed, single-task working precedent in the codebase (`ODY-S04-113`'s Ruleset migration, one task/one PR). Preview/Rules/Apply/Fixtures is the minimum division that keeps each task single-responsibility without artificially splitting the workflow further than `ADR-027` §10's own nine steps naturally group (steps 1-4 → Preview; the blocking-incompatibility list → Rules; steps 5-9 → Confirm/Apply; end-to-end proof → Fixtures). Authority: `ADR-027` §10's own step grouping; `ODY-S04-113` as a closed, comparably-scoped precedent.
- 2026-09-12 — Decision: reserve blocking-incompatibility computation (`ODY-S05-402`) as its own task, separate from preview construction (`ODY-S05-401`). Rationale: `ADR-027` §10's own blocking-incompatibility list names genuinely type-specific conditions (Weapon: "removed ammo type currently loaded"; Armor: "removed equipment slot currently occupied", "removed armor/body-part coverage with runtime damage") alongside generic ones (reduced capacity, unrecognizable custom state, uncomparable hidden mechanics) — confirmed by direct inspection that `TypedDefinitionCodec`/`CatalogValidationContracts` already branch per `ContentDefinitionType` for exactly this reason. Folding incompatibility computation into the preview-construction task would risk treating "the definition changed" as one uniform check instead of the type-aware set §10 actually requires. Authority: `ADR-027` §10's own blocking-incompatibility list; direct code read of `TypedDefinitionCodec`/`CatalogValidationContracts`.
- 2026-09-12 — Decision: name `ADR-027` §11's `ActiveEffect`-never-mass-migrates rule explicitly in §13's intro, not merely in this task contract. Rationale: without an explicit textual boundary, a future implementer skimming only the `ItemDefinition` migration block's task table could plausibly assume the same preview/confirm mechanism generalizes to `ActiveEffect` — which `ADR-027` §11 explicitly and deliberately forbids (mid-duration/mid-combat/turn-timing state makes bulk `ActiveEffect` rewriting unsafe). Authority: `ADR-027` §11's own explicit "Reason" paragraph.
- 2026-09-12 — Decision: name `RulesetMigrationRules`/`ApplyCharacterRulesetMigration` (`ODY-S04-113`) as the structural precedent for preview/plan/apply separation, with an explicit caveat that its concrete fields (`RulesetDefinitionMapping`, `RulesetUnresolvedDecision`, three `ExpectedXRevision` fields, `PreviewHash`) describe a *different* domain (Character Ruleset-version definition remapping) and must be reinvented for item mechanics snapshots, not copied verbatim. Authority: direct code read of `RulesetMigrationRules.cs`; the same "cite, don't copy" relationship `ODY-S05-108` already had to `ODY-S05-107`.
- 2026-09-12 — Decision: do not perform any `ODY-S05-1xx`/`2xx`/`3xx` housekeeping (e.g. moving already-merged task files from `active/` to `completed/`) in this task, even if such debt is noticed. Authority: this ТЗ §7's own explicit instruction not to fix such debt here without a separate explicit decision.
- 2026-09-12 — Decision: extend `SLICE-05_IMPLEMENTATION_BACKLOG.md` additively (new §13/§13.1; additive sentences in §9/§10/§11) rather than renumber existing sections. Authority: this ТЗ's own explicit instruction, consistent with `ODY-S05-107`/`108`'s own precedent.
- 2026-09-12 — Decision: do not edit `docs/adr/**`. Rationale: no contradiction found in `ADR-027` §10/§11/§12/§16. Authority: this ТЗ §0 ("без изменений в `docs/adr/**`").
- 2026-09-12 — Discovery: an initial draft of the §13 verbatim quote was missing one blank line inside the blockquote (between "Workflow:" and its numbered list), which a programmatic diff against the actual ADR text caught immediately — fixed before finalizing. Recorded because the product owner explicitly stated they would verify word-for-word quote accuracy independently.

### Approved task changes

- None.
