# ODY-S05-608 — Full Attack Pipeline Integration Fixtures

## 1. Task identity
`ODY-S05-608`; status: In Review (Draft PR, see the backlog row for the link once opened).

## 2. Goal
Prove the full attack pipeline block (`ODY-S05-602`-`607`, `609`) composes end-to-end against one real SQLite database, mirroring `ODY-S05-404`'s/`507`'s own integration-fixture precedent exactly: one new test file that runs several `ADR-029` §12 Definition-of-Done items TOGETHER in connected scenarios, using only already-accepted public services (`AttackEvaluationService`, `AttackApplyService`, `SqliteAttackApplyRepository`'s four public methods, `CombatEffectExpiryService`, `GameLogReconnectService`/`DiceRollVisibilityPolicy`). This task introduces zero new production behavior -- every individual §12 item already has an isolated test in its own owning task; the only real gap this task closes is composition, since every predecessor task resets state in its own `SetUp()`.

## 3. Authority
`ADR-029` §12 (all nine Definition-of-Done items, verbatim: preview purity; retry-no-reroll; stale-preview rejection; atomic delta/event/idempotency/Game-Log commit or none; durable pending resolution with no nested handler/reroll; combat duration boundary behavior; on-hit effect application gated by an explicit decision; compensating history without mutation; audience-filtered projections); the `ODY-S05-404`/`507` Brief-plan precedent (`PLANS.md` §1.1: one area, no new public contract/schema/permissions/dependency graph, one clear path, one PR, no migration -- a Brief plan, not an ExecPlan); backlog §17.1's own point clarification (2026-09-15) that `608`'s own DoD does not name stacking-conflict resolution, so `608` is not made to depend on `610`.

## 4. In scope
One new integration test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/AttackPipelineIntegrationFixtureTests.cs`, with 8 tests (`TC-ATTACK-076`-`083`), each composing 2-3 already-accepted §12 items in one connected scenario against a real, temp-directory SQLite campaign (never mocked/in-memory), following the exact structural precedent `ItemDefinitionMigrationIntegrationFixtureTests.cs` (`404`) and `ActiveEffectIntegrationFixtureTests.cs` (`507`) already established. Test metadata/backlog updates.

## 5. Out of scope
Any change to `Packages/com.odyssey.*/Runtime/**` -- this task composes existing public APIs only, it does not add, modify, or wire any production behavior. No automatic wiring point for `CombatEffectExpiryService.EvaluateAndExpireIfDue` (see §18 -- `605` explicitly deferred this, `606` did not add one, this task does not either; the direct-call composition pattern `605`'s own tests already use is reused as-is). No attempt to make an item-targeted `AttackDelta` succeed (`609`'s own disclosed, escalated blocker -- proven here only as a rejection). Re-verifying `602`-`607`/`609`'s own unit-level correctness (each already has its own isolated tests) -- only that they compose. `610`'s own territory (stacking-conflict GM resolution) -- not a dependency, not touched, per the backlog's own point clarification. Any real `IAttackRulesEvaluator` production implementation -- none exists anywhere in this codebase (`ADR-029` §10's own non-goal for this whole block); this task supplies its own hand-written fixture, exactly like every predecessor task already does, and says so honestly rather than presenting it as more than it is.

## 6. Domain contract
No Domain-layer changes. Every type used (`AttackIntent`, `AttackDelta`, `AttackEffectCandidate`, `CombatDurationBinding`, `ActiveEffect`, etc.) is reused exactly as `602`-`607`/`609` already defined it.

## 7. Application contract
No Application-layer changes. The fixture calls `AttackEvaluationService.PreviewAttack`/`EvaluateAttack` (`603`), `AttackApplyService.ResolveAttack`/`ResolveAttackIntervention`/`CompensateAttackOutcome` (`604`/`607`), `CombatEncounterService.Create`/`Advance` (`602`), `CombatEffectExpiryService.EvaluateAndExpireIfDue` (`605`), and `GameLogReconnectService.GetVisibleEntries` (`607`) -- all unmodified, all already-public.

## 8. Persistence boundary
No Persistence-layer changes. The fixture constructs `SqliteCharacterRepository`/`SqliteCombatEncounterRepository`/`SqliteInventoryRepository`/`SqliteActiveEffectRepository`/`SqliteAttackStateReader`/`SqliteAttackApplyRepository`/`SqliteGameLogRepository`/`SqliteCombatEncounterLifecycleReader` by hand (no composition root exists anywhere in this codebase, confirmed by direct search, matching `404`/`507`'s own precedent) against one real, temp-directory SQLite campaign per test.

## 9. Tests and validation
`TC-ATTACK-076`-`083` (continuing the existing series, no new prefix): a full happy-path scenario proving preview purity plus atomic immediate-accept commit of outcome/Game-Log/effect/delta together (§12 items 1, 4, 7); a full intervention-path scenario proving the deferred delta/effect application with no reroll (§12 items 4, 5, 7); full-pipeline retry-idempotency (§12 item 2); a stale-preview rejection at real apply time (§12 item 3); a real `ForRounds` `ActiveEffect` (created by a real attack apply) expiring via a direct `EvaluateAndExpireIfDue` call, fail-closed before its boundary (§12 item 6); compensation of a completed full cycle (§12 item 8); expanded-audience visibility on a real committed full-cycle entry (§12 item 9); an item-targeted delta failing the whole `ResolveAttack` call with no partial commit (§12 item 4's own "or commit none" half, `609`'s own disclosed blocker, proven as a rejection only). Full `dotnet test` (984 pre-existing + 8 new = 992 total) and the repository validation scripts are green. Zero production files changed (`git diff --name-status` confirms).

## 10. Compatibility and rollback
No production code touched -- nothing to be compatible with or roll back beyond deleting the new test file and reverting the three metadata/doc files. No schema change.

## 11. Security and privacy
No new authorization surface; the fixture re-exercises already-accepted gates (MainGM-only intervention approval/compensation) through a realistic connected scenario, it does not modify them.

## 12. Observability
No new Game Log entry type or diagnostic event; the fixture reads/asserts on the already-existing `AttackResolved`/`AttackCompensated` entries `604`/`607` already produce.

## 13. Performance
Test-only; no production hot path touched.

## 14. Dependencies
`ODY-S05-602`-`607`, `ODY-S05-609` (all merged, all composed unmodified). `610` is explicitly NOT a dependency (backlog §17.1's own point clarification: `608`'s own DoD does not name stacking-conflict resolution among the outcomes it must prove).

## 15. Dependencies (packages)
None new.

## 16. Implementation plan
See the active Brief plan (`docs/plans/active/ODY-S05-608_Full_Attack_Pipeline_Integration_Fixtures.md`) -- Brief plan, not ExecPlan, per `PLANS.md` §1.1 and the `404`/`507` precedent (one area/tests-only, no new public contract/schema/permissions/dependency graph, one clear implementation path, one PR, no migration/recovery procedure).

## 17. Completion evidence
Implemented `AttackPipelineIntegrationFixtureTests.cs` with 8 real, connected scenarios composing 2-3 `ADR-029` §12 items each against a real SQLite database. Verified: `PreviewAttack` draws no RNG, persists no outcome, writes no Game Log entry, and is repeatable; a real immediate `ResolveAttack` commits the `AttackOutcome` row, Game Log entry, `ActiveEffect` row, and `CharacterResource` delta together in one transaction; a `RequiresIntervention` path defers both until `ResolveAttackIntervention`'s Approve step, with zero RNG re-draws; a `CommandId` retry on the full pipeline neither re-draws RNG nor duplicates any row; a stale `ExpectedEncounterRevision` is rejected at real apply time; a real `ForRounds` effect stays `Active` before its boundary round and transitions to `Expired` exactly at it via a direct `EvaluateAndExpireIfDue` call; `CompensateAttackOutcome` appends a corrective row without mutating the original; a non-attacker/non-target combat participant sees a real committed entry while an outsider does not; an item-targeted delta fails the whole `ResolveAttack` call with zero partial commit. `git diff --name-status` against `main` touches only test/metadata/doc files -- zero production files. Full solution test suite green.

## 18. Change control

### Decisions made during execution

- **This task's own contract stays a full 18-section document even though its plan is a Brief plan, not an ExecPlan.** The governing ТЗ was explicit that "Brief plan" describes only the accompanying plan document's own format (`PLANS.md` §1.1's 4-point structure: files to inspect / intended change / tests and validation / explicit non-goals), never a license to shrink the task contract itself -- this contract follows the same compact 18-section shape every other task in this block (`602`-`607`, `609`) already used.
- **No automatic wiring point for `CombatEffectExpiryService.EvaluateAndExpireIfDue` was added, and this remains an explicitly unassigned gap -- not `605`'s, not `606`'s, and not `608`'s own to close.** `605`'s own task contract already deferred choosing a real call site (e.g. inside `CombatEncounterService.Advance`) as future work; `606` did not pick it up either. Adding a real wiring point here would be new production behavior, which this Brief-plan, tests-only task is explicitly forbidden from introducing. `TC-ATTACK-080` proves the service itself works correctly when called directly, exactly the way `605`'s own tests already do -- it does not, and cannot, prove anything about production automatic invocation, because no such call site exists anywhere in this codebase.
- **The item-targeted delta scenario (`TC-ATTACK-083`) proves only the rejection path, never a successful application.** `609`'s own task contract already escalated this as a disclosed blocker: `ItemInstanceRecord` has no numeric mutable field at the Domain level to write a delta into. This task does not attempt to close that gap (forbidden path, explicitly named in the governing ТЗ) -- it proves the existing, correct rejection behavior (`PersistenceAttackOutcomeDeltaItemTargetUnsupported`, no partial commit) composes correctly within a full pipeline run, which is a different and legitimate thing to prove.
- **The fixture's own `Rules` class is explicitly documented, in its own doc comment, as a hand-written stand-in, never presented as "a real Ruleset."** Direct search confirmed (again, as every predecessor task already confirmed for itself) that no production implementation of `IAttackRulesEvaluator` exists anywhere in this codebase -- `ADR-029` §10's own explicit non-goal for this entire block. This task's own value is proving the real HOST services compose against a real database when handed a Rules decision, not proving anything about what a real Ruleset would decide.
- **`Count(table)` in the fixture checks `sqlite_master` before counting**, because `AttackOutcome`/`GameLogEntries`/`DiceRolls` are created lazily by `SqliteAttackApplyRepository.EnsureAttackApplyTables` on first use -- a scenario that calls only `PreviewAttack` before ever calling into `SqliteAttackApplyRepository` (proving preview never touches persistence at all) would otherwise throw `no such table` on its own zero-row assertion. A test-fixture-only accommodation, not a production concern.

### Blockers

- None.
