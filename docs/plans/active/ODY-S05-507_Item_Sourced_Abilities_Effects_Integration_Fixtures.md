# ODY-S05-507 — Item-Sourced Abilities/Effects Integration Fixtures (Brief plan)

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-507-integration-fixtures`
**Pull request:** [odyssey-services/Odyssey_VTT#142](https://github.com/odyssey-services/Odyssey_VTT/pull/142) (Draft)
**Last updated:** 2026-09-13 UTC

Per `PLANS.md` §1.1: this task is contained in one area (tests), changes no public contract/schema/permissions/dependency graph, has one clear implementation path, fits in one PR, and needs no migration/recovery procedure — a Brief plan, not an ExecPlan, matching the exact precedent `ODY-S05-207`/`306`/`404` set for their own integration-fixture tasks.

## 1. Files or areas to inspect

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs` (`ODY-S05-404`) — the exact structural precedent to mirror (real temp-directory SQLite, `SetUp`/`TearDown`, `"composition only"` doc comment, private helpers, raw-SQL verification, no new production type).
- `IActiveEffectRepository`/`SqliteActiveEffectRepository` (`ODY-S05-502`/`504`/`505`/`506`) — confirm all 7 methods' current signatures.
- `ItemEffectLifecycleService.cs` (`ODY-S05-505`) — read in full, including its own inline comment about not silently bypassing a non-`IndependentInstances` stacking policy, since this directly shapes how the `RequestGMResolution` scenario must be composed.
- `ActiveEffectStackingRules.cs`/`ActiveEffectStackingRulesTests.cs` (`ODY-S05-503`) — confirm the exact pattern for composing a stacking decision against a genuinely-persisted row with no repository write method.
- `ActiveEffectExpiryRules.cs` (`ODY-S05-504`) — confirm `CheckForDurationExpiry`'s exact signature.
- `ActiveEffectDirectCommandService.cs` (`ODY-S05-506`) — confirmed it exists but is not used in this task's own main scenario (item-triggered creation, not direct GM creation).
- `EquippedEntry.cs`/`InventoryRuntime.cs` — the `EquipmentSlotRef` canonical-token validation, to avoid a slot-key formatting mistake.

## 2. Intended change

Add one new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ActiveEffectIntegrationFixtureTests.cs`, with 6 tests (`TC-ACTIVEEFFECT-079`-`084`), one per backlog-named scenario:

1. Equip a real item whose `BuiltInEffectRefs` names a real published `WhileItemEquipped` `EffectDefinition` → `ItemEffectLifecycleService.OnItemEquipped` creates a real `ActiveEffect`.
2. Unequip (`OnItemUnequipped`) then re-equip (`OnItemEquipped`) the same item → the same row suspends then resumes, never duplicated.
3. A second, hand-built-but-realistic candidate application of a real `RequestGMResolution`-policy effect, against a genuinely-persisted first row → `ActiveEffectStackingRules.ResolveStacking`/`ResolveActiveEffectStackConflict` → a real second row via the unmodified `CreateActiveEffect`.
4. A real `ForDuration` effect, created via item-equip with a host-supplied expiry resolver → `ActiveEffectExpiryRules.CheckForDurationExpiry` before/after → `ExpireActiveEffect` applies the real expiry.
5. `RemoveActiveEffect` by a non-MainGM actor → denied, zero mutation (raw SQL + `DomainEvents` count).
6. `RemoveActiveEffect` by MainGM → succeeds, `Status → Removed`.

No production file changes. No fixture hook was needed (confirmed during implementation).

## 3. Tests or validation commands

- New tests: `TC-ACTIVEEFFECT-079`-`084`, registered in `Tests/Metadata/test-catalog.json`.
- `dotnet build DotNet\Odyssey.Core.sln`
- `dotnet test DotNet\Odyssey.Core.sln`
- `.\scripts\verify-format.ps1`
- `.\scripts\check-repository-policy.ps1`
- `.\scripts\verify-test-structure.ps1`

## 4. Explicit non-goals

- No change to `IActiveEffectRepository`/`SqliteActiveEffectRepository`, `ItemEffectLifecycleService`, `ActiveEffectStackingRules`, `ActiveEffectExpiryRules`, `ActiveEffectDirectCommandService`, `EquipmentService`, or the catalog/inventory-creation services — all called, none modified.
- No re-verification of `ODY-S05-502`-`506`'s own unit-level correctness — only that they compose.
- No new production business rule or fixture hook unless genuinely required (none was).
- No composition root / DI container — the fixture constructs its own participants by hand.

## Progress log

- 2026-09-13 — Read `ItemEffectLifecycleService.cs` in full, including its own inline comment about not bypassing non-`IndependentInstances` stacking policies; confirmed via `ActiveEffectStackingRulesTests.cs` that composing `ActiveEffectStackingRules` directly against a persisted row (rather than via a second real `OnItemEquipped` call) is the established, accepted pattern, not a workaround invented for this task.
- 2026-09-13 — Wrote `ActiveEffectIntegrationFixtureTests.cs`; first run failed 5 of 6 tests on an `EquippedEntry` slot-key format error (mixed-case slot keys are not canonical tokens) — fixed to lowercase-token slot keys; second run failed the `ForDuration` test on a hardcoded absolute `ExpiresAt` timestamp intermittently preceding real wall-clock time — fixed to compute all expiry timestamps relative to a real captured `Clock.GetUtcNow()`; third run: 6/6 passed.
- 2026-09-13 — Full suite green (Persistence 612/612, 606 predecessor + 6 new). All 5 required validation commands PASS.

## Outcome

Draft PR to be opened. This closes the item-sourced abilities/effects range (`ODY-S05-501`-`507`) — no further planned task in this backlog area. `ODY-S05-505-F01` (`CharacterAbility` suspend/resume) and the pre-existing Unity/IL2CPP asmdef gap (both recorded by `ODY-S05-505`) remain their own separate, unscheduled follow-ups.
