# ODY-S05-608 — Full Attack Pipeline Integration Fixtures (Brief plan)

Per `PLANS.md` §1.1: this task is contained in one area (tests), changes no public contract/schema/permissions/dependency graph, has one clear implementation path, fits in one PR, and needs no migration/recovery procedure — a Brief plan, not an ExecPlan, matching the exact precedent `ODY-S05-404`/`507` set for their own integration-fixture tasks.

## 1. Files or areas to inspect

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ItemDefinitionMigrationIntegrationFixtureTests.cs` (`ODY-S05-404`) / `ActiveEffectIntegrationFixtureTests.cs` (`ODY-S05-507`) — the exact structural precedent to mirror (real temp-directory SQLite, `SetUp`/`TearDown`, `"composition only"` doc comment, private helpers, raw-SQL verification, no new production type).
- `AttackEvaluationService.cs` (`ODY-S05-603`) — confirm `PreviewAttack`/`EvaluateAttack`/`AuthorizeAndRead`'s exact guard conditions (current-turn/revision checks), since the stale-preview scenario depends on them precisely.
- `AttackApplyService.cs`/`AttackApplyRepositoryContracts.cs`/`SqliteAttackApplyRepository.cs` (`ODY-S05-604`/`606`/`607`/`609`) — confirm the four public `IAttackApplyRepository` methods' current signatures, including `609`'s own `DamageDeltas`/`CostDeltas` parameters.
- `CombatEffectExpiryService.cs`/`ActiveEffectExpiryRules.cs` (`ODY-S05-605`) — confirm `EvaluateAndExpireIfDue`'s exact signature and `CheckForRoundsExpiry`'s boundary arithmetic (`appliedRoundOrdinal + requiredCount + 1`).
- `GameLogReconnectService.cs`/`DiceRollVisibilityPolicy.cs` (`ODY-S05-607`) — confirm `GetVisibleEntries`'s exact signature, reused unmodified from `607`'s own test file.
- `SqliteCombatEncounterRepository.cs`'s `Advance` implementation — confirm exactly how many `Advance` calls move a 2-participant encounter's `RoundOrdinal` forward (round increments only when the next participant index wraps to the front of the order), needed to hit the `ForRounds` expiry boundary deterministically.

## 2. Intended change

Add one new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/AttackPipelineIntegrationFixtureTests.cs`, with 8 tests (`TC-ATTACK-076`-`083`), each composing several `ADR-029` §12 Definition-of-Done items in one connected scenario:

1. Full happy-path, no intervention: `PreviewAttack` proven pure (no RNG, no persisted outcome, no Game Log entry, repeatable) → real `ResolveAttack` commits `AttackOutcome`/Game Log/`ActiveEffect`/resource-delta atomically.
2. Full intervention path: `ResolveAttack` produces a durable `Pending` outcome with nothing applied yet → `ResolveAttackIntervention`'s Approve step applies the delta/effect, with zero additional RNG draws.
3. Full-pipeline retry-idempotency: a `CommandId` retry of `ResolveAttack` neither re-draws RNG nor duplicates any row.
4. Stale preview: a request built against an encounter revision that is then genuinely advanced is rejected by `ResolveAttack`'s own real re-authorization, not silently applied.
5. Combat duration expiry: a real `ForRounds` `ActiveEffect`, created by a real attack apply, is proven `Active` before its boundary round (fail-closed) and `Expired` exactly at it, via a direct `CombatEffectExpiryService.EvaluateAndExpireIfDue` call (no new wiring point).
6. Compensation after a full cycle: `CompensateAttackOutcome` appends a corrective Game Log row without mutating the original.
7. Expanded audience on a full cycle: a non-attacker/non-target combat participant sees the real committed entry; an outsider does not.
8. Item-targeted delta: fails the whole `ResolveAttack` call with no partial commit (`609`'s own disclosed blocker, proven only as a rejection).

No production file changes. No fixture hook needed (confirmed during implementation).

## 3. Tests or validation commands

- New tests: `TC-ATTACK-076`-`083`, registered in `Tests/Metadata/test-catalog.json`.
- `dotnet build DotNet\Odyssey.Core.sln`
- `dotnet test DotNet\Odyssey.Core.sln`
- `.\scripts\verify-format.ps1`
- `.\scripts\check-repository-policy.ps1`
- `.\scripts\verify-test-structure.ps1`

## 4. Explicit non-goals

- No change to any file under `Packages/com.odyssey.*/Runtime/**` — every service/repository is called, none modified.
- No automatic wiring point for `CombatEffectExpiryService.EvaluateAndExpireIfDue` — `605` explicitly deferred this, `606` did not add one, this task does not either; proven only via a direct call, exactly as `605`'s own tests already do.
- No attempt to make an item-targeted `AttackDelta` succeed — `609`'s own disclosed, escalated blocker stays open; this task proves the rejection path only.
- No re-verification of `602`-`607`/`609`'s own unit-level correctness — only that they compose.
- No composition root / DI container — the fixture constructs its own participants by hand, exactly like `404`/`507`.
- `610`'s own territory (stacking-conflict GM resolution) is not touched and is not a dependency (backlog §17.1's own point clarification).

## Progress log

- Read `AttackEvaluationService.AuthorizeAndRead`'s own guard conditions (current-turn/revision checks) and `SqliteCombatEncounterRepository.Advance`'s own round-increment logic (round increments only when the next participant index wraps to the front) before writing the `ForRounds`/stale-preview scenarios, to compute the exact number of `Advance` calls needed rather than guessing.
- Wrote `AttackPipelineIntegrationFixtureTests.cs`; first run failed 2 of 8 tests (`Full_happy_path...`, `A_stale_preview...`) with `SQLite Error 1: 'no such table: AttackOutcome'` — the `Count(table)` helper was invoked before any `SqliteAttackApplyRepository` call in scenarios that check row counts around a `PreviewAttack`-only step, and that table is created lazily on first use; fixed by checking `sqlite_master` first. Second run: 8/8 passed.
- Full suite green (Persistence 682/682 = 674 predecessor + 8 new; Contracts 1/1, Domain 90/90, Networking 67/67, Unit 144/144, Architecture 8/8 — 992 total). All 5 required validation commands PASS. `git diff --name-status` against `main` confirmed: only the new test file plus `Tests/Metadata/test-catalog.json`/`docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`/this task's own contract and plan.

## Outcome

Draft PR [odyssey-services/Odyssey_VTT#153](https://github.com/odyssey-services/Odyssey_VTT/pull/153) opened. This closes the full attack pipeline block (`ODY-S05-602`-`609`, `608`) — `ODY-S05-610` (`RequestGMResolution` stacking-conflict GM resolution, `606`'s own disclosed gap) remains its own separate, already-reserved follow-up task, explicitly not a dependency of `608`. The `CombatEffectExpiryService.EvaluateAndExpireIfDue` automatic-wiring gap (`605`'s own disclosed limitation) also remains open and unassigned — not closed by `605`, `606`, or `608`.
