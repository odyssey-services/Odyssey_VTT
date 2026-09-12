# ODY-S05-504 — Non-Combat Duration/Expiry

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-504-non-combat-duration-expiry`
**Pull request:** TBD (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Give 8 of `ADR-028` §8's 15 `EffectDurationType` values a real, explicit answer for how they end (or explicitly never do), implement the fail-closed rule as a genuine code path, and add the one repository transition (`Status → Expired`) this range's own task split had left unassigned. After this task, an `ActiveEffect` with a `ForDuration` duration can actually be checked against a wall clock and transitioned to `Expired` in the database — nothing yet calls this end-to-end, but every piece exists.

## 2. Task contract

- Goal: `ActiveEffectExpiryRules` (Application) covering all 8 owned duration values; `EffectConditionRules.Evaluate` (Rules) for `WhileCondition`; `IActiveEffectRepository.ExpireActiveEffect` + SQLite implementation.
- Acceptance criteria: see task contract §9 (9 items) — all 8 values explicit; fail-closed as a real code path; new CAS-guarded `Expired` transition via `SqliteSavingPipeline`; `WhileCondition` evaluator in `Odyssey.Rules`; event-subscription mechanisms are honest contract-only stubs; no out-of-scope logic; green tests; backlog updated including the "9"→"8" typo fix; Draft PR.
- Requirement IDs: `ODY-S05-504`, `SLICE-05`.
- In scope: two new pure-decision files (Application + Rules), one new repository method + error code, tests/metadata/docs/backlog (including a documentation typo fix).
- Out of scope: `WhileItemEquipped` (`505`), removal/permission gates (`506`), any turn/round-based value, stacking (`503`, already done), any real event publisher.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §15/§15.1 row 3; `ADR-028` §6 rule 2/§8/§11/§12/§15; `ActiveEffectStackingRules`/`EffectStackRules` (`ODY-S05-503`, direct structural precedent); `ItemDefinitionMigrationRules.ComputeBlockingIssues` (honest-partial-implementation precedent); `CharacterOwnership.IsActiveAt`/`DiagnosticBundleContracts.IsExpired` (now-as-parameter idiom).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Branch `feat/ody-s05-504-non-combat-duration-expiry` was created fresh off `origin/main` at `e378ca7` (merge of PR #138, `ODY-S05-503`). Backlog row 3 in §15 (`ODY-S05-504`) was `Proposed`, with a "9"-vs-"8" counting typo in both its own text and §15.1's prose (confirmed and fixed — task contract §4/§18).
- Read all required precedents in full before writing code: `ActiveEffect.cs`/`ActiveEffectRepositoryContracts.cs`/`SqliteActiveEffectRepository.cs` (post-503 state), `ActiveEffectStackingRules.cs`/`EffectStackRules.cs` (the direct structural and "honest fixture-level heuristic" precedents), `ItemDefinitionMigrationRules.ComputeBlockingIssues` (the "declare unimplemented cases honestly" precedent), `ADR-028` §6/§8/§11/§12/§15 in full, `CharacterOwnership.cs`/`DiagnosticBundleContracts.cs` (now-as-parameter idiom), `PersistenceFailures.EquipmentEntryRevisionConflict` (CAS-conflict error precedent).
- Confirmed by direct search that no `SceneActivated`/`SceneChanged`/session-end/`ItemConsumed`/`ItemDestroyed` event exists anywhere in the codebase — ruling out any real publisher for `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists`.
- Confirmed by direct review of `ODY-S05-505`/`506`'s own scope text in the backlog that neither owns the `Expired` transition, resolving (by exclusion) that this task is the correct and only place to add `IActiveEffectRepository.ExpireActiveEffect`.

Assumptions: none.

## 4. Proposed approach

- `ActiveEffectExpiryRules` (`Odyssey.Application.Effects`, same file-directory as `ActiveEffectStackingRules`): four pure static methods, one per handled shape of the 8 owned duration values — `CheckNoAutomaticExpiry` (Permanent/UntilRemoved, always `NotExpired`), `CheckForDurationExpiry` (real wall-clock check, `now` as a parameter), `CheckWhileConditionExpiry` (consumes `EffectConditionEvaluationResult`, implements the fail-closed rule as an explicit `switch` branch), `OnExternalTriggerFired` (the three event-subscription duration types, always `Expired` once called — no publisher wired). `Instant` deliberately has no corresponding method at all, since `ADR-028` §8's own table says no row is ever created for it.
- `EffectConditionRules.Evaluate` (`Odyssey.Rules.Effects`, new file alongside `EffectStackRules`): always returns `Inconclusive` today. This is the single most consequential design decision in this task (task contract §18 has the full reasoning) — two independent constraints (no condition schema exists; `Odyssey.Rules` has no JSON dependency) rule out any real evaluation today, and `ADR-028` §11's own text explicitly treats "inconclusive" as a fully valid outcome, not a placeholder failure.
- `IActiveEffectRepository.ExpireActiveEffect(campaign, campaignId, activeEffectId, expectedRevision, commandId, correlationId)`: added by exclusion-based reasoning (task contract §18) — `ADR-028` §6 rule 2 names the `Expired` transition as part of the minimum contract, and neither `505` nor `506` claims it. `SqliteActiveEffectRepository`'s own implementation reuses `EnsureActiveEffectTables`/`ReplayByCommandId`/`SelectColumns`/`ReadRecord` unchanged, adding one CAS-guarded `UPDATE ... SET Status=$status, Revision=Revision+1 ... WHERE ActiveEffectId=$id AND CampaignId=$campaign AND Revision=$expectedRevision`, committed via `SqliteSavingPipeline.Execute` exactly like `CreateActiveEffect`.
- One new error code, `persistence.active_effect.revision_conflict`, mirroring `persistence.equipment_entry.revision_conflict`'s exact convention for a CAS-guarded status transition.
- Tests: one new file, `ActiveEffectExpiryRulesTests.cs`, in `Odyssey.Tests.Persistence` (matching `ActiveEffectStackingRulesTests.cs`'s own placement), covering all 8 owned duration values' own explicit answer, the fail-closed rule as three distinct branches, and the new repository method (success, CAS conflict, replay idempotency, cross-campaign rejection) against a real SQLite database.
- Backlog fix: point-correct "9" → "8" in row 3 and §15.1's own prose only — a third occurrence in §14.1 (a different section, written by a different task) is deliberately left untouched.

No change to `ActiveEffectStackingRules.cs`, `ActiveEffect.cs`, `Create`/`Get`/`List` methods, any scope guard, or any `.csproj`/`.asmdef`.

## 5. Milestones

### M1 — Research, design, and backlog fix

- [x] Read `ActiveEffect.cs`/`ActiveEffectRepositoryContracts.cs`/`SqliteActiveEffectRepository.cs` (post-503), `ActiveEffectStackingRules.cs`/`EffectStackRules.cs`, `ItemDefinitionMigrationRules.ComputeBlockingIssues`, `ADR-028` §6/§8/§11/§12/§15 in full.
- [x] Confirm by direct search that no real event publisher exists for `UntilSceneChange`/`UntilSessionEnd`/`WhileSourceExists`.
- [x] Confirm by direct backlog review that `ODY-S05-505`/`506` do not own the `Expired` transition, resolving the exclusion-based decision to add `ExpireActiveEffect` here.
- [x] Point-correct the "9"→"8" typo in backlog row 3 and §15.1's own prose.

### M2 — Production code

- [x] Write `EffectConditionRules.cs` (`Odyssey.Rules.Effects`).
- [x] Write `ActiveEffectExpiryRules.cs` (`Odyssey.Application.Effects`).
- [x] Add `ExpireActiveEffect` to `IActiveEffectRepository`; add the new error code + `PersistenceFailures` factory + `ERROR_CODES.md` row.
- [x] Implement `ExpireActiveEffect` in `SqliteActiveEffectRepository`, reusing existing private helpers.
- [x] Build the solution — succeeded on the first attempt, 0 warnings, 0 errors.
- [x] Confirm no scope guard or architecture test references `SqliteActiveEffectRepository`/`IActiveEffectRepository` by method name or count (direct `grep`).

### M3 — Tests and full-suite verification

- [x] Run the full solution test suite once with only the new repository method (no new tests yet) to isolate any regression from the new method alone — green, no regressions.
- [x] Write `ActiveEffectExpiryRulesTests.cs` (`TC-ACTIVEEFFECT-036`-`050`).
- [x] Run the new tests — 17/17 passed on the first run (15 named tests + 3 `TestCase` variants for `TC-ACTIVEEFFECT-046`), no fixes needed.
- [x] Run the full solution test suite again — all green, no regressions.

### M4 — Metadata, docs, validation, PR

- [x] Register `TC-ACTIVEEFFECT-036`-`050` in `Tests/Metadata/test-catalog.json`.
- [x] Run all 5 required validation commands; record real results.
- [x] Write the task contract and this ExecPlan to full depth.
- [ ] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 3 in §15 to `In Review` (typo fix already applied in M1).
- [ ] Review `git diff --name-status` for scope.
- [ ] Commit, push, and open Draft PR.
- [ ] Record PR link and backlog status.

## 6. Progress log

- 2026-09-12 — Created worktree `D:\ody_s05_504_wt`, branch `feat/ody-s05-504-non-combat-duration-expiry`, off `origin/main` at `e378ca7` (merge of PR #138).
- 2026-09-12 — Read all required precedents; confirmed the "9"→"8" backlog typo by direct arithmetic and enumeration; fixed both named occurrences (row 3, §15.1), leaving §14.1's own separate occurrence untouched.
- 2026-09-12 — Confirmed by direct search: no scene-change/session-end/item-destroyed event exists anywhere; confirmed by direct backlog review: neither `505` nor `506` owns the `Expired` transition, resolving the decision to add `ExpireActiveEffect` here.
- 2026-09-12 — Wrote `EffectConditionRules.cs`, `ActiveEffectExpiryRules.cs`, the new `ExpireActiveEffect` interface method + SQLite implementation, and the new error code; `dotnet build` succeeded on the first attempt.
- 2026-09-12 — Ran the full test suite with only the new repository method (no new tests yet) — all green, confirming the new method introduced no regression on its own.
- 2026-09-12 — Wrote `ActiveEffectExpiryRulesTests.cs` (17 tests including `TestCase` variants, `TC-ACTIVEEFFECT-036`-`050`); ran them — 17/17 passed on the first run.
- 2026-09-12 — Full-suite `dotnet test DotNet\Odyssey.Core.sln`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 577/577 — all green, no regressions.
- 2026-09-12 — Registered `TC-ACTIVEEFFECT-036`-`050` in `Tests/Metadata/test-catalog.json`.
- 2026-09-12 — Wrote the task contract and this ExecPlan to full depth.

## 7. Decisions

See task contract §18 for the full decision log: the "9"→"8" backlog typo fix and its precise scope (row 3 + §15.1 only, not §14.1); the exclusion-based reasoning for adding `ExpireActiveEffect`; `EffectConditionRules.Evaluate`'s always-`Inconclusive` honest no-op and the two constraints behind it; and the single shared `ActiveEffectExternalExpiryTriggerKind` enum/method for all three event-subscription duration types instead of three separate interfaces.

## 8. Discoveries and deviations

- The backlog's own "9 presently-implementable" count was wrong in two places (row 3, §15.1), both inherited unchanged from `ODY-S05-111` — a real documentation defect this task was explicitly tasked with fixing, not merely working around.
- `EffectConditionRules.Evaluate` cannot do anything more than return `Inconclusive` today without either inventing an unauthorized condition-language schema or adding a JSON dependency to `Odyssey.Rules` — both outside this task's own authority, exactly as `ODY-S05-503` already found for the analogous `ReplaceIfStronger` comparison.
- No scope-guard or architecture test needed any edit for the new repository method — confirmed by direct search and a real full-suite test run, not merely assumed from the method's own namespace placement.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 577/577.
- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS (new `ERROR_CODES.md` row accepted).
- `.\scripts\verify-test-structure.ps1`: PASS.
- Diff review: pending final `git diff --name-status` confirmation before commit.

## 10. Recovery and rollback

Rollback of THIS PR is a normal revert before merge. `ExpireActiveEffect` never physically deletes a row (`ADR-012`'s append-only-history discipline), so there is no additional data-loss risk beyond what `ODY-S05-502`'s own `CreateActiveEffect` already carries.

## 11. Open questions and blockers

None remain open for this task. Recorded for later tasks (not resolved here): a future task with a real Ruleset condition language must replace `EffectConditionRules.Evaluate`'s own body without changing its signature; a future task must supply the real `SceneActivated`/session-end/`ItemConsumed`-or-`ItemDestroyed` events and call `ActiveEffectExpiryRules.OnExternalTriggerFired` at the right moment; whichever task first wires a real caller of `ActiveEffectExpiryRules`/`ExpireActiveEffect` end-to-end (most plausibly `ODY-S05-507`) does so against its own real call site, not one guessed at here.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation tasks in this range: `ODY-S05-505` (WhileItemEquipped Wiring + Item-Triggered Creation), `ODY-S05-506` (RemoveActiveEffect Command + Direct-Creation Permission Gates), `ODY-S05-507` (Item-Sourced Abilities/Effects Integration Fixtures) — none started proactively; each awaits its own ТЗ.
