# ODY-S05-404 — Migration Integration Fixtures

**Status:** Done (PR #133, merged into main)
**Roadmap stage / slice:** SLICE-05 (ItemDefinition migration block — final task)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-404-migration-integration-fixtures`
**Pull request:** [odyssey-services/Odyssey_VTT#133](https://github.com/odyssey-services/Odyssey_VTT/pull/133) (Draft)
**Plan:** `docs/plans/active/ODY-S05-404_Migration_Integration_Fixtures.md` (Brief plan)
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Prove the `ItemDefinition` migration block (`ODY-S05-401`-`403`) composes end-to-end, mirroring `ODY-S05-207`/`306`'s own integration-fixture pattern: publish a target version through the real catalog lifecycle, build a real preview against genuinely persisted runtime records, discover a real blocking incompatibility through `ComputeBlockingIssues`, confirm `ApplyItemDefinitionMigration` refuses to proceed while it stands, resolve it through an already-accepted runtime mechanism, reconfirm, and apply successfully — verifying the updated snapshot columns and the `DomainEvents` table's append-only behavior by direct SQL. No new production behavior.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-401`-`403` each have their own isolated tests, including `ODY-S05-403`'s own `BuildRealPreview`-backed happy-path round trip — but nothing exercises a *genuinely discovered* blocking incompatibility (`ComputeBlockingIssues` against real data, not a synthetic `IncompatibilityReport`), nor the resolve-then-reconfirm cycle `ADR-027` §10 steps 5-7 require around one.
- Value or risk reduction: closes the one meaningful composition gap left in the migration block before it can be considered complete.
- Blocking or enabling relationship: this is the final task in the `ItemDefinition` migration block (`ODY-S05-401`-`404`); nothing in this backlog depends on it.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §13/§13.1 row 4.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §10 (full workflow), §16 item 2 (archived/Published definitions stay loadable).
- Existing patterns: `InventoryRuntimeIntegrationFixtureTests.cs` (`ODY-S05-207`) and `EquipmentRuntimeIntegrationFixtureTests.cs` (`ODY-S05-306`) — the exact structural precedent for this file (real temp-directory SQLite campaign, composition of already-accepted public services only, `"composition only, no new production code"` doc-comment convention); `CharacterArchivePhysicalDeleteTests.DeleteCharacterPermanently_DoesNotDeleteDomainEventsRows` — the exact `DomainEvents` count-before/count-after idiom.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-404`, `SLICE-05`.
- Existing test IDs: `TC-INVENTORY-001`-`188` as predecessor evidence.
- New test IDs introduced: `TC-INVENTORY-189`-`192`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-404` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main` at `1f5e909`, the merge of PR #132 (`ODY-S05-403`). Backlog row 4 (`ODY-S05-404`) was `Proposed`.
- Direct code read of `ItemDefinitionMigrationApplyTests.cs` (`ODY-S05-403`) confirms this ТЗ's own §1 premise: `BuildRealPreview` there already builds a genuine preview via the real `ItemDefinitionMigrationRules.BuildPreview`, and `TC-INVENTORY-179` is already a full `BuildPreview → Apply` round trip on real SQLite — the "happy path with no blocking issue" is already covered and must not be reinvented. None of `ODY-S05-403`'s own tests call `ComputeBlockingIssues` at all (they supply already-clean previews directly); `ComputeBlockingIssues` itself is only ever exercised against synthetic, hand-built records in `ItemDefinitionMigrationRulesTests.cs` (`ODY-S05-402`, `TC-INVENTORY-169`-`178`); no existing test publishes source/target through `ContentCatalogLifecycleService.PublishDefinition` and then migrates the resulting genuinely-persisted records; no existing test checks `DomainEvents` row count before/after a migration.
- `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues` and `SqliteInventoryRepository.ApplyItemDefinitionMigration`/`ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`/`GetEquippedEntry` signatures were confirmed unchanged from `ODY-S05-401`-`403`'s own final merged state by direct code read; no signature drift since those PRs merged.
- `ReplaceEquippedEntry`'s scope of mutation (only the `EquippedEntry` row's own revision) and `EquipItemCore<T>`/`UnequipItemCore<T>`'s own revision-bumping behavior on the underlying item (already established during `ODY-S05-403`) inform this task's own resolve-then-reconfirm design: `Unequip` bumps the item's own `Revision`, so the preview built before it is doubly stale (blocking issue AND revision) by the time it is rebuilt — a realistic exercise of `ODY-S05-403`'s own revision guard, not merely its blocking-issue recheck.
- `SqliteContentCatalogRepository` never uses `SqliteSavingPipeline` (confirmed by direct code read: zero `_pipeline.Execute` call sites) — `PublishDefinition`/`CreateDraftDefinition` never append a `DomainEvents` row. `SqliteCharacterRepository` uses the pipeline extensively (confirmed: 20 call sites) — `CreateCharacter`/`InitializeCharacterAnatomy` likely each append one. `EquipItemCore<T>`/`UnequipItemCore<T>` (Equipment) never use the pipeline (established during `ODY-S05-403`'s own investigation) — no event from Equip/Unequip either. Only `ApplyItemDefinitionMigration`'s own successful call appends a `DomainEvents` row anywhere in this task's own composed sequence. **Consequence for the `DomainEvents` count-diff test (§4 item 7 of this ТЗ):** counting from the very start of a test (before Character creation) would include an unpredictable, precedent-irrelevant number of Character-lifecycle events; the count window is instead scoped tightly around the migration-specific operations only (matching `CharacterArchivePhysicalDeleteTests`'s own scoping style, which also counts immediately around the single operation under test, not from the top of its file) — see §18 decision.
- `ComputeBlockingIssues`'s own precondition (`ODY-S05-402`, confirmed by direct code read): `currentlyEquippedAffectedItems` must reference only `AffectedInstances` (via `InventoryItemRef.ForInstance`), never stacks — the stack-capacity blocking case (`TC-INVENTORY-192`) is checked independently of any equipped-entry list.
- `InventoryStackOperationService.Split` (`ODY-S05-205`, already accepted, cited by this ТЗ's own §6 optional-test suggestion) is a legitimate existing runtime mechanism to resolve a stack-capacity blocking issue by reducing a single stack's own quantity below the new target's capacity — confirmed usable without any new production code.

### Assumptions

- None.

## 5. Scope

### In scope

- New test file `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs`.
- Test metadata and backlog/docs updates.

### Out of scope

- Any change to `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues` (`ODY-S05-401`/`402`).
- Any change to `SqliteInventoryRepository.ApplyItemDefinitionMigration` or any of `ODY-S05-403`'s own logic.
- Any change to `EquipmentService`/`EquipItemCore<T>`/`UnequipItemCore<T>`, `ContentCatalogLifecycleService.PublishDefinition`, or `InventoryStackOperationService.Split` — all called, none modified.
- Any new production business rule. No fixture hook was needed: every operation this task's tests require (`CreateItemInstance`/`CreateItemStack`, `Equip`/`Unequip`, `PublishDefinition`, `BuildPreview`, `ComputeBlockingIssues`, `ApplyItemDefinitionMigration`, `Split`, `GetEquippedEntry`, `GetContentDefinition`) already existed and was sufficient — confirmed during implementation, not merely assumed in advance (§18).
- ADR edits, Unity/UI.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-404_Migration_Integration_Fixtures.md
docs/plans/active/ODY-S05-404_Migration_Integration_Fixtures.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationRulesTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationApplyTests.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: test-only; no production module touched.
- Authoritative-state and transaction boundary: no new write path; every mutation goes through already-accepted public repository/service methods.
- Serialization / compatibility boundary: not applicable.
- Time / RNG rule: `IWallClock Clock = new SystemWallClock()`, matching `ODY-S05-207`/`306`/`403`'s own test convention.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable (test-only, synthetic data).
- Other: a real, temp-directory SQLite campaign per test (via `SqliteCampaignRepository.Create`), never in-memory or mocked — the exact `ODY-S05-207`/`306` precedent.

## 7. Expected behavior

### Scenario 1 — full round trip: publish, preview, block, reject, resolve, reconfirm, apply

**Given** an Armor item equipped in a slot the source definition defines, and a target version published with a different slot
**When** a preview is built and `ComputeBlockingIssues` is run against the item's real `EquippedEntry`, then `ApplyItemDefinitionMigration` is attempted, then the item is unequipped through `EquipmentService.Unequip`, a fresh preview is built, and `ApplyItemDefinitionMigration` is attempted again
**Then** the first attempt is genuinely blocked (`inventory.migration.blocked`) with no mutation, and the second attempt succeeds, with the `ItemInstance`'s `SourceItemDefinitionRef`/`MechanicsPayload` columns (read via raw SQL) matching the target definition.

### Scenario 2 — repeated rejected attempts append no `DomainEvents` rows

**Given** a real, unresolved blocking issue
**When** `ApplyItemDefinitionMigration` is attempted twice
**Then** the `DomainEvents` row count is unchanged after both attempts.

### Scenario 3 — the source definition remains loadable after migration

**Given** a successful migration moving items off the source definition
**When** `GetContentDefinition` is called for the source definition's own `ContentDefinitionId`
**Then** it still succeeds and reports `Status = Published`.

### Scenario 4 — a stack-capacity blocking issue is resolved through `Split`, not equipment

**Given** a stack whose quantity exceeds a target version's reduced capacity
**When** the preview is built, `ComputeBlockingIssues` reports `StackCapacityReducedBelowCurrentContent`, `Apply` is rejected, the stack is split via `InventoryStackOperationService.Split` into two parts each within the new capacity, and a fresh preview/apply cycle runs
**Then** the second attempt succeeds and both resulting stacks are migrated.

### Required invariants

- Every source/target definition in every test is published through the real `ContentCatalogLifecycleService.PublishDefinition`, never constructed as a given `ContentDefinitionRecord` directly.
- Every blocking issue asserted is one `ComputeBlockingIssues` actually computed against real persisted records, never a hand-built `ItemDefinitionMigrationIncompatibilityReport`.
- A rejected `Apply` attempt never mutates any affected record and never appends a `DomainEvents` row.
- No new production code exists anywhere in this task's diff.

## 8. Deliverables

- Production code: none.
- Tests: `ItemDefinitionMigrationIntegrationFixtureTests.cs`, `TC-INVENTORY-189`-`192`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, Brief plan, `Tests/Metadata/test-catalog.json`, backlog row (and the block-level completion note).
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none.

## 9. Acceptance criteria

1. A new integration fixture test file exists under `DotNet/Tests/Odyssey.Tests.Persistence/Integration/`, following the `ODY-S05-207`/`306` structural precedent (real temp-directory SQLite, `"composition only"` doc comment).
2. The full chain `PublishDefinition → BuildPreview → ComputeBlockingIssues (genuinely discovers the issue) → resolve via an existing runtime mechanism → rebuilt preview → ApplyItemDefinitionMigration → raw-SQL snapshot verification` passes in at least one end-to-end test.
3. `Apply` is explicitly demonstrated to be rejected while the blocking issue stands.
4. `DomainEvents` row count is checked before/after around the migration-specific operations and shown to increase by exactly the number of successful `Apply` calls, never decrease or otherwise change.
5. `TC-INVENTORY-189`-`192` are registered in `Tests/Metadata/test-catalog.json`.
6. No new production business logic exists in the diff; no fixture hook was needed (confirmed, not merely assumed).
7. `dotnet test` is green; backlog row `ODY-S05-404` → `In Review (PR #NNN)`; the migration block (`401`-`404`) is noted complete.
8. PR is Draft; merge is the product owner's decision.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-189` | .NET / NUnit (Persistence Integration, real SQLite) | Full migration block composes end-to-end through publish/preview/block/reject/resolve/reconfirm/apply/verify | Pass |
| `TC-INVENTORY-190` | .NET / NUnit (Persistence Integration, real SQLite) | Repeated rejected apply attempts append zero DomainEvents rows | Pass |
| `TC-INVENTORY-191` | .NET / NUnit (Persistence Integration, real SQLite) | Source ItemDefinition remains loadable after a successor is published and a migration applied | Pass |
| `TC-INVENTORY-192` | .NET / NUnit (Persistence Integration, real SQLite) | A real stack-capacity blocking issue is resolved through Split, proving the path is not equipment-specific | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no production file (anything under `Packages/**`) changed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any re-verification of `ODY-S05-401`-`403`'s own unit-level correctness — only that they compose.

## 11. Compatibility, migration, and rollback

- Compatibility impact: none — test-only.
- Version fields affected: none.
- Migration or upcaster: none.
- Forward / backward behavior: unaffected.
- Rollback method: revert the branch/PR before merge.
- Data-loss risk and protection: none — no production code changes.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory/character test records; no real player data.
- Trust boundaries: none new.
- Authorization / audience checks: exercised (MainGM-only), not modified.
- Redaction requirements: not applicable.
- Log-safe fields: not applicable.
- Abuse / malformed input limits: not applicable (test-only).
- Security tests: none new (this task is a composition proof, not a security boundary).

## 14. Planning and execution mode

- Planning mode: `Brief`.
- Reason for selected mode: one module (tests only), no new public contract/schema/permissions/architecture, one PR, no data migration — matching `ODY-S05-207`/`306`'s own precedent exactly, per `PLANS.md` §1.1.
- Brief plan path: `docs/plans/active/ODY-S05-404_Migration_Integration_Fixtures.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #132; closes the `ItemDefinition` migration block (`401`-`404`).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, Brief plan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs; any file under `Packages/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
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

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs` — new file, `TC-INVENTORY-189`-`192`.
- `Tests/Metadata/test-catalog.json` — 4 new entries registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-404` updated to `In Review`; block-completion note added.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 538/538 (534 predecessor + 4 new). |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0 (after this task contract was written — the verifier cross-checks `test-catalog.json` `taskId` references against an existing task contract file). |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (structural precedent followed) | Met | Doc-comment and `SetUp`/`TearDown` mirror `InventoryRuntimeIntegrationFixtureTests.cs`/`EquipmentRuntimeIntegrationFixtureTests.cs` exactly. |
| AC-2 (full chain, one end-to-end test) | Met | `TC-INVENTORY-189`. |
| AC-3 (Apply rejected while blocked, demonstrated) | Met | `TC-INVENTORY-189`/`190`/`192`. |
| AC-4 (DomainEvents count-diff) | Met | `TC-INVENTORY-189`/`190`. |
| AC-5 (test metadata registered) | Met | `Tests/Metadata/test-catalog.json`. |
| AC-6 (no new production logic; no fixture hook needed) | Met | `git diff --name-status` touches only `DotNet/Tests/**`/`Tests/Metadata/**`/`docs/**`. |
| AC-7 (dotnet test green, backlog updated, block noted complete) | Met | Table above; backlog row and block note updated. |
| AC-8 (Draft PR) | Met | PR #133 opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- None specific to this task — it is a pure composition proof over already-accepted, already-tested surfaces.

### Follow-up tasks

- None within `SLICE-05`'s `ItemDefinition` migration block — `401`-`404` is complete after this PR merges.

### Self-review summary

- Scope review: diff touches only test/doc/metadata paths; zero production files changed; confirmed no fixture hook was needed.
- Architecture review: no new abstractions; every operation composes already-accepted public repository/service methods.
- Test review: all 4 required scenarios covered with genuinely-discovered (not synthetic) blocking issues; `DomainEvents` count-diff uses the exact `CharacterArchivePhysicalDeleteTests` idiom, correctly scoped to avoid unrelated Character-lifecycle events.
- Security/privacy review: not applicable (test-only, synthetic data).
- Documentation/version review: task contract, Brief plan, test catalog, and backlog all updated; no schema/manifest/protocol version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Design decision: the `DomainEvents` count-diff check is scoped tightly around the migration-specific operations, not the whole test from `SetUp`.** Direct code read established that `SqliteCharacterRepository` (used here for `CreateCharacter`/`InitializeCharacterAnatomy`) routes extensively through `SqliteSavingPipeline` and therefore very likely appends its own `DomainEvents` rows for reasons unrelated to migration, while `SqliteContentCatalogRepository` (`PublishDefinition`) and Equipment's `EquipItemCore<T>`/`UnequipItemCore<T>` do not. Counting from the very top of a test would make the expected delta depend on incidental, migration-irrelevant Character-lifecycle event counts. Scoping the count window to immediately before the first `Apply` attempt and immediately after the whole resolve/reconfirm/apply sequence isolates exactly the claim `ADR-027` §10 step 8 and this ТЗ's own §4 item 7 care about: a rejected attempt appends nothing, a successful one appends exactly one row, and nothing already present is ever rewritten or removed. This matches `CharacterArchivePhysicalDeleteTests.DeleteCharacterPermanently_DoesNotDeleteDomainEventsRows`'s own scoping style (immediately around the one operation under test), not a whole-file count. Authority: this ТЗ §4 item 7's own explicit reference to that exact precedent test.
- 2026-09-12 — Decision: no fixture hook was needed anywhere in this task, confirmed (not merely assumed) after implementation — every operation the four tests require already existed as public API (`CreateItemInstanceFromDefinition`/`CreateItemStackFromDefinition`, `EquipmentService.Equip`/`Unequip`, `ContentCatalogLifecycleService.PublishDefinition`, `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues`, `SqliteInventoryRepository.ApplyItemDefinitionMigration`/`ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`/`GetEquippedEntry`, `InventoryStackOperationService.Split`, `IContentCatalogRepository.GetContentDefinition`). Authority: this ТЗ §5's own explicit default expectation that no hook would be needed.
- 2026-09-12 — Decision: `TC-INVENTORY-192` (the optional stack-capacity variant) was included, using `InventoryStackOperationService.Split` (`ODY-S05-205`) as the resolving mechanism, to prove the composed migration path is not specific to equipment/Armor — mirroring `ODY-S05-306`'s own choice to test both Armor and Weapon rather than only one item shape. Authority: this ТЗ §6 item 4's own optional suggestion, judged worth including given it reuses existing test helpers cheaply and materially strengthens the "genuinely composes across item shapes" claim.
- 2026-09-12 — Decision: `TC-INVENTORY-191` (source definition stays loadable, `ADR-027` §16 item 2) is a new, small, standalone test — confirmed by reading `ODY-S05-401`'s own test file (`ItemDefinitionMigrationRulesTests.cs`) that no existing test asserts this after a real migration `Apply`, so it is not a duplicate. Authority: this ТЗ §6 item 4's own instruction to confirm non-duplication before adding it.
