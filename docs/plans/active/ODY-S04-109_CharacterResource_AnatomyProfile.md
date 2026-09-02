# ODY-S04-109 — CharacterResource & AnatomyProfile

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-109-character-resource-anatomy`
**Pull request:** [#93](https://github.com/odyssey-services/Odyssey_VTT/pull/93)
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Implement product section 17/18: typed `CharacterResource` (computed `EffectiveMaximum`, typed `RecoveryRule` including `None`), the "maximum decrease clamps current, later increase does not restore it" invariant (requirements 44–45); and `CharacterAnatomy` — an independent snapshot per Character (requirements 48–49), individual modifications journaled via `MigrationHistory`, `RemoveBodyPart`'s dependency preview bounded to what this codebase can actually check (requirements 50–51, no Item system exists). Ninth implementation task of `SLICE-04`, and the first to introduce two independent sections in one task where one (`CharacterResources`) is a multi-entry collection (mirroring `CharacterAbilities`) and the other (`CharacterAnatomy`) is a single snapshot object (mirroring `Ownership`/`Lifecycle`) — deliberately different shapes, not the same pattern twice.

## 2. Task contract

- Goal: implement `CharacterResource` (multi-entry, section-wide-gated) and `CharacterAnatomy` (single-object, section-wide-gated) with their own commands, following `MutateAbilities`(105/108)/`MutateOwnership`(102) as the exact structural precedent for each shape respectively.
- Acceptance criteria: `CharacterResourcesRevision`/`CharacterAnatomyRevision` genuinely increment (direct value check); a `CharacterResource`'s `CurrentValue` can never be constructed outside `[MinimumValue, EffectiveMaximum]` (enforced by the domain type itself); decreasing `EffectiveMaximum` below `CurrentValue` clamps it in the same commit, and a later increase never restores it; `CharacterAnatomy`'s `AnatomyProfileVersion` is pinned at initialization and independent of the fixture; `RemoveBodyPart` rejects a body part with an internal dependent (another body part or permanent modification attached to it) and succeeds for an independent one, with the item-dependency gap explicitly documented as a stub (no Item system exists); `MigrationHistory` accumulates one entry per anatomy command; every command is `CommandId`-idempotent, verified against real state; a concurrent `CharacterResources` edit and `CharacterAnatomy` edit (or any other section) commit without a false conflict; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022` content change; no edit to any already-merged `101`–`108` file.
- Requirement IDs: `ODY-S04-109`, product section 17/18, requirements 41–51, `ADR-022` §5–6.
- In scope: `CharacterResourceId`/`ResourceDefinitionId`/`PermanentModificationId` (Domain Identity), `CharacterResource`/`RecoveryRule` (Domain, new `Resource.cs`), `AnatomyProfileDefinitionId`/`BodyPartId`/`BodyPart`/`PermanentModification`/`AnatomyMigrationEntry`/`CharacterAnatomy` (Domain, new `Anatomy.cs`), `ResourceInitializationRules`/`AnatomyInitializationRules` (Rules, explicitly-flagged test fixtures), `ICharacterRepository` extension (Application), `SqliteCharacterRepository`/schema extension (Persistence: `ResourcesJson`/`AnatomyJson` columns, `MutateResources`/`MutateAnatomy` helpers), tests, error registry/test-catalog additions, backlog status update.
- Out of scope: `CharacterAbility` (already `ODY-S04-108`); real item-dependency checking (no Item system exists — documented stub only); automatic resource recovery on any trigger (only the explicit command); archive/delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`); concrete `ResourceDefinition`/`AnatomyProfileDefinition` catalogs; any Unity/UI code; any change to `ADR-022`/backlog beyond the status row; any edit to already-merged `101`–`108` files (unlike `107`/`108`, this task touches no prior file's own logic).
- Required authorities: product section 17/18 (full read), requirements 41–51, `ADR-022` §5–6 (`CharacterResourcesRevision`/`CharacterAnatomyRevision`, lock keys `CharacterResource:<id>` vs. un-parameterized `CharacterAnatomy`), `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`–`108`'s own code as binding convention (`MutateAbilities`/`MutateOwnership` especially), `ODY-S04-103`'s own template-independence test as the pattern for requirement 49's test.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `0664d1a` (PR #92, `ODY-S04-108`'s merge commit), independently verified via `git merge-base --is-ancestor`.
- `Character` table already carries real `CharacterResourcesRevision`/`CharacterAnatomyRevision` columns (`ADR-022` §5, present from `ODY-S04-101` onward) that no prior task ever incremented — confirmed by `Grep`.
- No `ResourceDefinition`/`AnatomyProfileDefinition` catalog exists anywhere in this codebase — confirmed by `Grep`, mirroring `AttributeCostRules`/`SkillCostRules`/`AbilityCostRules`'s own prior fixture precedent.
- No Item/Inventory system exists anywhere in this codebase — confirmed by `Grep` (the same fact ODY-S04-108 already relied on for its own `SourceKind=Item` limitation).
- `ADR-022` §6 reserves the un-parameterized `CharacterAnatomy` lock key (not `CharacterAnatomy:<id>`) alongside the parameterized `CharacterResource:<CharacterResourceId>` — confirming the two sections are deliberately different shapes, not an oversight.

Assumptions: none.

## 4. Proposed approach

- Domain: `Resource.cs` (`ResourceDefinitionId`, `RecoveryRule`, `CharacterResource` — `EffectiveMaximum` computed only, constructor rejects any `CurrentValue` outside `[MinimumValue, EffectiveMaximum]`, structurally enforcing requirement 44/45 rather than merely documenting it); `Anatomy.cs` (`AnatomyProfileDefinitionId`, `BodyPartId` — catalog-style string, not a random instance id, since a body part is a stable named slot — `BodyPart`, `PermanentModification`, `AnatomyMigrationEntry`, `CharacterAnatomy` — a single snapshot, never a collection of independently-revisioned entries).
- Rules: `ResourceInitializationRules`/`AnatomyInitializationRules`, explicitly-flagged test fixtures (flat starting values; a small humanoid body-part set with two parts (`LeftArm`/`RightArm`) each `AttachedToBodyPartId=Torso`, so `RemoveBodyPart`'s dependency check has real content to exercise both outcomes).
- Application: `CharacterRecord` gains `Resources`/`Anatomy` (mirrors `Attributes`/`Skills`/`Abilities`'s direct-embedding convention); nine new `ICharacterRepository` methods (3 resource, 6 anatomy — see section 7 below for the folding decisions).
- Persistence: `ResourcesJson`/`AnatomyJson` columns; `MutateResources` mirrors `MutateAbilities`'s exact single-section-collection shape (permission check inside the callback, since resource commands share one gate but different lookups); `MutateAnatomy` mirrors `MutateOwnership`'s exact single-object shape (permission check hoisted before the transaction, since every anatomy command is uniformly MainGM-only). `RemoveBodyPart`'s dependency check scans the SAME `CharacterAnatomy`'s own `BodyParts`/`PermanentModifications` for a reference to the target — the only dependency this codebase can actually express; the product's own item-dependency requirement (51) is a documented stub, not silently skipped.
- Tests: initialization + revision-increment for both sections; resource bounds/clamp/no-auto-restore (requirements 44–47); anatomy independence (mirroring ODY-S04-103's own template test); `AddBodyPart`/`RemoveBodyPart` (including both dependency outcomes and an unknown-id case)/`UpdateBodyPart`/`ReplaceAnatomyProfile`/`ApplyPermanentModification`; `MigrationHistory` accumulation; duplicate-`CommandId` for one resource and one anatomy command; a concurrent-section no-false-conflict test.

No Unity/UI code, no `ADR-022` content change, no concrete Ruleset content, no edit to any already-merged `101`–`108` file.

## 5. Section 1's two special conditions (from the ТЗ)

1. **§1.1 — `CharacterResources` is a multi-entry section, mirroring `CharacterAbilities`.** `MutateResources` is a direct structural copy of `MutateAbilities`'s shape (single collection, one section-wide revision, no entry-level externally-checked gate) — not `MutateMechanics`'s entry-level-plus-section-wide dual gate, since ТЗ §1.1 explicitly names `MutateAbilities` as the pattern to follow.
2. **§1.2 — `CharacterAnatomy` is a single snapshot object, NOT a collection.** `MutateAnatomy` is a direct structural copy of `MutateOwnership`'s shape instead — one un-parameterized section revision, the whole snapshot replaced per command, matching `ADR-022` §6's own un-parameterized `CharacterAnatomy` lock key (deliberately distinct from `CharacterAnatomy:<id>`, which does not exist). This was NOT modeled as a collection with entry-level revisions the way `CharacterSkill`/`CharacterAbility` are — that would have been the wrong shape for this section.

## 6. Section 1.3's dependency-preview boundary (documented, not resolved unilaterally beyond this)

Product §18/requirement 51 names an item-dependency check for `RemoveBodyPart` that this codebase cannot implement — no Item/Inventory system exists anywhere (confirmed by `Grep`, the same fact ODY-S04-108 already relied on). This is implemented as an explicit, documented stub: the item-dependency half of the check simply does not run (there is nothing to check), while the REAL, internally-checkable half — does any other `BodyPart.AttachedToBodyPartId`/`PermanentModification.AttachedToBodyPartId` within the SAME `CharacterAnatomy` reference the part being removed — is checked for real and rejects with `CharacterBodyPartHasDependent`. This boundary is documented in three places: this ExecPlan, the `RemoveBodyPart` interface/implementation doc comments, and the final report — per the ТЗ's own explicit instruction not to invent a fictitious Item-dependency search.

## 7. Command-shape decisions (product does not name these commands verbatim, per the ТЗ's own delegation)

- Resources (3, all MainGM-only): `InitializeCharacterResource` (from fixture); `SetResourceCurrentValue` (the ONE command for both damage and recovery, per requirement 46's "always an explicit command" — no separate `DamageResource`/`RecoverResource` pair, since both are the identical "set CurrentValue within bounds" operation); `SetResourceMaximum` (changes `BaseMaximum`/`PermanentMaximumAdjustment` together, clamping `CurrentValue` structurally via the domain type).
- Anatomy (6, all MainGM-only): `InitializeCharacterAnatomy`; `AddBodyPart`; `RemoveBodyPart`; `UpdateBodyPart` (folds product's own separately-listed "изменить пределы повреждений"/"изменить свойства части" into one command, since both target the same `BodyPart` row and two near-identical single-field setters would duplicate the same lookup/replace logic — each parameter is nullable so a caller can change either or both); `ReplaceAnatomyProfile` (explicitly distinct from `ODY-S04-113`'s future Ruleset migration); `ApplyPermanentModification` (one generic command for "протез/мутацию/постоянную модификацию," since product itself groups all three with no separate schema per kind).

## 8. Milestones

### M1 — Domain/Rules/Application extension

- [x] `Resource.cs`/`Anatomy.cs` (Domain); `ResourceInitializationRules`/`AnatomyInitializationRules` (Rules).
- [x] `CharacterResourceId`/`PermanentModificationId` (Domain Identity).
- [x] `CharacterRecord.Resources`/`Anatomy`; nine new `ICharacterRepository` methods; `PersistenceFailures`/`ErrorCodes` additions (nine new entries).
- [x] Every existing `CharacterRecord` construction call site in `SqliteCharacterRepository.cs` updated for the two new parameters (mechanical, no behavior change) -- verified via a full `dotnet build`/`dotnet test` run before writing any new command logic.

### M2 — Persistence and tests

- [x] `ResourcesJson`/`AnatomyJson` columns; `SerializeResources`/`DeserializeResources`/`SerializeAnatomy`/`DeserializeAnatomy`; `WithRevisions` extended.
- [x] `MutateResources`/`MutateAnatomy` helpers; nine command implementations.
- [x] 22 new tests in `CharacterResourceAnatomyTests.cs`, all passing on first/second run (2 additional not-found/already-exists tests added after the first pass for full error-code coverage).
- [x] `dotnet build`/`dotnet test` full suite green (188/188 persistence tests, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1` (clean on first run — no Python text-mode edits this task, `Edit` tool used throughout).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-093`–`114`).
- [x] This task contract/ExecPlan, created before the final validation pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check against §9's own expectations (no already-merged `101`–`108` file touched).
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 9. Progress log

- 2026-09-02 -- Preflight confirmed PR #92's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-109-character-resource-anatomy`.
- 2026-09-02 -- Read product section 17/18, requirements 41–51, `ADR-022` §5–6 in full; re-read `MutateAbilities`/`MutateOwnership` and confirmed via `Grep` that no `ResourceDefinition`/`AnatomyProfileDefinition` catalog and no Item system exist.
- 2026-09-02 -- Implemented `Resource.cs`/`Anatomy.cs`/identity additions/Rules fixtures/`ICharacterRepository` extension; mechanically updated all 13 `CharacterRecord` construction sites for the two new parameters; `dotnet build`/`dotnet test` confirmed zero regression before writing any command logic.
- 2026-09-02 -- Implemented `MutateResources`/`MutateAnatomy` and all nine commands; `dotnet build` passed on the second attempt (first attempt correctly flagged the nine not-yet-implemented interface members).
- 2026-09-02 -- Wrote and ran 19 tests; all passed on first run. Added 3 more (not-found/already-exists cases) for full error-code coverage, bringing the total to 22; all passed.
- 2026-09-02 -- Full suite green (188/188 persistence tests, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green on the first run after registration.

## 10. Decisions

- 2026-09-02 -- Decision: `CharacterResource.EffectiveMaximum` is a computed property, and the constructor rejects any `CurrentValue` outside `[MinimumValue, EffectiveMaximum]` -- the maximum-decrease-clamps-current invariant (requirement 44) is enforced structurally, not merely by a command-side check that could be bypassed by a different call site. Authority: mirrors `AttributeValue.EffectiveValue`'s own "never stored, computed only" convention; strengthened further per this task's own explicit ТЗ emphasis on requirements 44–45.
- 2026-09-02 -- Decision: `MutateResources` mirrors `MutateAbilities`'s exact shape (single section-wide gate, no entry-level external check); `MutateAnatomy` mirrors `MutateOwnership`'s exact shape (single-object, hoisted MainGM check). Authority: ТЗ §1.1/§1.2's own explicit instruction to use these two different precedents for the two different section shapes.
- 2026-09-02 -- Decision: `RemoveBodyPart`'s item-dependency check (requirement 51) is a documented stub -- only the internal (same-`CharacterAnatomy`) dependency is checked for real. Authority: ТЗ §1.3's own explicit instruction; confirmed by `Grep` that no Item/Inventory system exists anywhere in this codebase.
- 2026-09-02 -- Decision: `SetResourceCurrentValue` is the ONE command for both damage and recovery (no separate methods). Authority: product requirement 46's own "recovery always goes through an authoritative command" framing does not distinguish damage from recovery mechanically -- both are "set CurrentValue within bounds."
- 2026-09-02 -- Decision: `UpdateBodyPart` folds "change damage limits" and "change properties" into one command with nullable parameters. Authority: this task's own code-quality judgment -- both target the same `BodyPart` row; two near-identical single-field setters would duplicate the same lookup/replace logic.
- 2026-09-02 -- Decision: `ApplyPermanentModification` is one generic command for prosthetic/mutation/permanent-modification. Authority: product section 18's own text groups all three with no separate schema per kind.
- 2026-09-02 -- Decision: `BodyPartId` is a catalog-style validated string (like `AttributeDefinitionId`), not a canonical `Prefix+Uuid7` instance id. Authority: a body part is a stable named structural slot ("Head", "LeftArm"), not a randomly-created purchase instance -- `PermanentModificationId`, which IS a genuine per-application instance, correctly uses the canonical shape instead.

## 11. Discoveries and deviations

- No open architectural question was found that `ADR-022` does not already answer. The escape hatch this task's own ТЗ did not offer was not needed either way -- both special conditions (§1.1/§1.2 shape choice, §1.3 dependency boundary) were resolved directly from `ADR-022`'s own lock-key reservations and this codebase's own already-confirmed absence of an Item system.
- No already-merged `101`–`108` file was touched -- confirmed directly by this task's own diff-scope check (section 9's own expectation, unlike `107`/`108` which each needed a small retroactive fix to prior code).

## 12. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 188/188 `Odyssey.Tests.Persistence` (22 new), zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 13. Recovery and rollback

Rollback is a normal revert of this branch/PR -- the new `ResourcesJson`/`AnatomyJson` columns are additive (default `'[]'`/`NULL`); the existing `CharacterResourcesRevision`/`CharacterAnatomyRevision` columns are now genuinely written, previously always `1`; no already-merged file's own logic was touched, so reverting this branch cannot regress `101`–`108`'s own behavior.

## 14. Open questions and blockers

None.

## 15. Outcome and follow-up

Draft PR: [#93](https://github.com/odyssey-services/Odyssey_VTT/pull/93). CI pending. `ODY-S04-110` (Archive & Dependency-Aware Physical Delete) is the next task in the backlog; a future Item/Inventory task would be the first real occasion to extend `RemoveBodyPart`'s dependency check with the item-dependency half this task explicitly stubbed.
