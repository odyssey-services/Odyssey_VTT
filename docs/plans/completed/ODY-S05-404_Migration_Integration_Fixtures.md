# ODY-S05-404 — Migration Integration Fixtures (Brief plan)

**Status:** Done (PR #133, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-404-migration-integration-fixtures`
**Pull request:** [odyssey-services/Odyssey_VTT#133](https://github.com/odyssey-services/Odyssey_VTT/pull/133) (Draft)
**Last updated:** 2026-09-12 UTC

Per `PLANS.md` §1.1: this task is contained in one area (tests), changes no public contract/schema/permissions/dependency graph, has one clear implementation path, fits in one PR, and needs no migration/recovery procedure — a Brief plan, not an ExecPlan, matching the exact precedent `ODY-S05-207`/`306` set for their own integration-fixture tasks.

## 1. Files or areas to inspect

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs` (`ODY-S05-207`) and `.../EquipmentRuntimeIntegrationFixtureTests.cs` (`ODY-S05-306`) — the exact structural precedent to mirror (real temp-directory SQLite, `SetUp`/`TearDown`, `"composition only"` doc comment, private helpers, no new production type).
- `DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationApplyTests.cs` (`ODY-S05-403`) — read in full to confirm what it already covers (`BuildRealPreview`, a real `BuildPreview → Apply` happy-path round trip) so this task does not duplicate it.
- `DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationRulesTests.cs` (`ODY-S05-401`/`402`) — confirm `ComputeBlockingIssues` is only ever exercised there against synthetic records, and that no existing test covers `ADR-027` §16 item 2 (source definition stays loadable after migration).
- `CharacterArchivePhysicalDeleteTests.DeleteCharacterPermanently_DoesNotDeleteDomainEventsRows` — the exact `DomainEvents` count-before/count-after idiom to reuse verbatim.
- Current signatures of `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues`, `SqliteInventoryRepository.ApplyItemDefinitionMigration`/`ListItemInstancesBySourceDefinitionId`/`ListItemStacksBySourceDefinitionId`/`GetEquippedEntry`, `EquipmentService.Equip`/`Unequip`, `InventoryStackOperationService.Split`, `ContentCatalogLifecycleService.PublishDefinition` — confirm unchanged since `ODY-S05-401`-`403` merged.

## 2. Intended change

Add one new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs`, with 4 tests (`TC-INVENTORY-189`-`192`):

1. Full round trip: publish source Armor → equip it for real → publish a target Armor version with a different slot → build a real preview → `ComputeBlockingIssues` genuinely discovers the equipment-slot incompatibility against the item's real `EquippedEntry` → `Apply` is rejected → `Unequip` through the real Equipment runtime → rebuild the preview (now revision-stale too) → `ComputeBlockingIssues` is clean → `Apply` succeeds → raw-SQL `SELECT` confirms the `ItemInstance`'s snapshot columns now match the target.
2. Repeated rejected `Apply` attempts append zero `DomainEvents` rows.
3. The source definition remains loadable (`GetContentDefinition`, `Status = Published`) after a successful migration moves items off of it.
4. A real stack-capacity blocking issue (a stackable `Item`, not Armor) is resolved through `InventoryStackOperationService.Split`, proving the composed path is not equipment-specific.

No production file changes. No fixture hook is expected to be needed (confirmed during implementation).

## 3. Tests or validation commands

- New tests: `TC-INVENTORY-189`-`192`, registered in `Tests/Metadata/test-catalog.json`.
- `dotnet build DotNet\Odyssey.Core.sln`
- `dotnet test DotNet\Odyssey.Core.sln`
- `.\scripts\verify-format.ps1`
- `.\scripts\check-repository-policy.ps1`
- `.\scripts\verify-test-structure.ps1`

## 4. Explicit non-goals

- No change to `ItemDefinitionMigrationRules.BuildPreview`/`ComputeBlockingIssues`, `SqliteInventoryRepository.ApplyItemDefinitionMigration`, `EquipmentService`, `ContentCatalogLifecycleService.PublishDefinition`, or `InventoryStackOperationService.Split` — all called, none modified.
- No re-verification of `ODY-S05-401`-`403`'s own unit-level correctness — only that they compose.
- No new production business rule or fixture hook unless genuinely required (none was).

## Progress log

- 2026-09-12 — Read `ODY-S05-403`'s own `ItemDefinitionMigrationApplyTests.cs` in full, confirming the happy-path round trip is already covered and must not be duplicated; confirmed `ComputeBlockingIssues` is never exercised against real `BuildPreview` output anywhere in the existing suite.
- 2026-09-12 — Wrote and ran `ItemDefinitionMigrationIntegrationFixtureTests.cs`; all 4 tests passed on first run (no iteration needed, unlike `ODY-S05-403`'s own test-writing pass) — confirmed no fixture hook was needed.
- 2026-09-12 — Full suite green (Persistence 538/538). All 5 required validation commands PASS.

## Outcome

Draft PR to be opened. This closes the `ItemDefinition` migration block (`ODY-S05-401`-`404`) — no further planned task in this backlog area.
