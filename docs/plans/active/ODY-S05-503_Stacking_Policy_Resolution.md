# ODY-S05-503 — Stacking Policy Resolution

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-503-stacking-policy-resolution`
**Pull request:** TBD (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Give the item-sourced abilities/effects block a real answer to "what happens when the same effect is applied to the same target twice": all 7 `ADR-028` §7 `EffectStackPolicy` behaviors, decided by one pure function, plus the `RequestGMResolution` pending-conflict record and its own resolution function. After this task, nothing yet *executes* a stacking decision against the database — that remains a future task's own job, exactly as the migration block split `ComputeBlockingIssues` (decide) from `ApplyItemDefinitionMigration` (apply) across two separate tasks.

## 2. Task contract

- Goal: `ActiveEffectStackingRules.ResolveStacking` (all 7 policies) + `ResolveActiveEffectStackConflict` (3 resolutions), plus `Odyssey.Rules.Effects.EffectStackRules.IsStronger`.
- Acceptance criteria: see task contract §9 (8 items) — all 7 behaviors via a pure function mirroring `ComputeBlockingIssues`; `ReplaceIfStronger` delegates to new `Odyssey.Rules` code; `ActiveEffectStackConflict`/`Resolve` implemented with the persistence variant explicitly justified; no `502` CRUD rewritten; tests cover all 7 + comparison + full cycle; no duration/expiry/removal logic; green tests; Draft PR.
- Requirement IDs: `ODY-S05-503`, `SLICE-05`.
- In scope: one new Rules-layer comparison, one new Application-layer decision static class + its DTOs, tests/metadata/docs/backlog.
- Out of scope: any `IActiveEffectRepository`/`SqliteActiveEffectRepository` change; duration/expiry (`504`); `WhileItemEquipped`/item-triggered creation (`505`); removal/permission gates (`506`); any project/assembly-definition-file change.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §15/§15.1 row 2; `ADR-028` §7/§15/§18.3; `ItemDefinitionMigrationRules.ComputeBlockingIssues` (structural precedent); `AttributeCostRules` (fixture-honesty precedent).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Branch `feat/ody-s05-503-stacking-policy-resolution` was created fresh off `origin/main` at `7e8507f` (merge of PR #137, `ODY-S05-502`). Backlog row 2 in §15 (`ODY-S05-503`) was `Proposed`.
- Read all required precedents in full before writing code: `ActiveEffect.cs`/`ActiveEffectRecord.cs`/`ActiveEffectRepositoryContracts.cs`/`SqliteActiveEffectRepository.cs` (confirming the exact CRUD surface not to be touched), `ItemDefinitionMigrationRules.cs`'s full `ComputeBlockingIssues` implementation (the structural precedent), `TypedDefinitionCodec.cs`'s `EffectStackPolicy` decode path, `Odyssey.Rules.Character.AttributeCostRules` (fixture-honesty precedent), `ADR-028` §7/§15/§18.3 in full.
- Two hard constraints were discovered by direct inspection, not assumed, and shaped this task's own design (both recorded in the task contract §4/§18): (1) `Odyssey.Rules.csproj`/`.asmdef` show no JSON library dependency exists in the Rules module, ruling out parsing `EffectMechanicsSnapshot.Payload` for the `ReplaceIfStronger` comparison; (2) `ADR-028` §18.3 already explicitly rejected a generic potency field, ruling out adding one under a different name.
- Confirmed via direct `.csproj` read that `Odyssey.Tests.Persistence` already transitively references `Odyssey.Rules` (through `Odyssey.Application.csproj`), so this task's new `Odyssey.Rules.Effects.EffectStackRules` type is testable from the same test project `ItemDefinitionMigrationRulesTests.cs` already uses for an analogous pure-function precedent — no `.csproj` edit needed.

Assumptions: none.

## 4. Proposed approach

- `EffectStackRules.IsStronger(EffectMechanicsSnapshot candidate, EffectMechanicsSnapshot existing)` (`Odyssey.Rules.Effects`, new namespace): compares `DefinitionSnapshotVersion` — a higher value is "stronger," a tie is not. Chosen specifically because it is the only Domain-owned, always-present, unambiguously-ordered signal common to both snapshots that does not require a new JSON dependency or a rejected generic potency field (task contract §18 records the full reasoning). Documented explicitly as a fixture-level heuristic pending real Ruleset-driven potency, mirroring `AttributeCostRules`'s own honesty.
- `ActiveEffectStackingRules.ResolveStacking(EffectStackPolicy, ActiveEffectRecord? existingConflictingEffect, ActiveEffectRecord candidateApplication, UtcInstant now)` (`Odyssey.Application.Effects`, new namespace): a static, no-I/O method mirroring `ComputeBlockingIssues`'s own shape exactly — the caller has already loaded/decoded everything this method needs. If `existingConflictingEffect` is `null`, every policy's own first-application behavior is identical (`CreateNewEffect`); otherwise a `switch` on the 7 `EffectStackPolicy` values produces one of 6 `ActiveEffectStackDecisionKind` outcomes (`ReplaceIfStronger` itself produces either of two, depending on `IsStronger`'s own result). Precondition checks reject an `existingConflictingEffect` that does not share the candidate's own `TargetRef`/`EffectDefinitionRef`/`CampaignId`.
- `ActiveEffectStackDecision`: an immutable result type whose constructor validates that only the fields meaningful for its own `Kind` are populated — the same validate-in-constructor style `ItemDefinitionMigrationRules.cs`'s own DTOs (`ItemDefinitionMigrationAffectedInstance`, etc.) already use, rather than factory methods.
- `ActiveEffectStackConflict`: wraps a full candidate `ActiveEffectRecord` plus the conflicting id and `RaisedAt`, rather than duplicating `ADR-028` §7 rule 7's own four named fields separately — they are already all present on the wrapped record (task contract §18 records this reasoning).
- `ResolveActiveEffectStackConflict(ActiveEffectStackConflict, ActiveEffectStackConflictResolution)`: a second pure function returning the exact same `ActiveEffectStackDecision` shape `ResolveStacking` itself produces, so any future caller has one decision type to act on regardless of source.
- **No new `IActiveEffectRepository`/`SqliteActiveEffectRepository` method is added** — the single most consequential design decision in this task, made by direct analogy to `ODY-S05-402`/`403`'s own decide/apply split (task contract §18's first decision entry has the full reasoning). Tests instead use `ODY-S05-502`'s own unmodified `CreateActiveEffect`/`GetActiveEffect` to build realistic already-persisted "existing conflicting effect" fixtures, proving the decision layer against real loaded records without needing to execute any decision against the database.
- Tests: one new file, `ActiveEffectStackingRulesTests.cs`, in `Odyssey.Tests.Persistence` (matching `ItemDefinitionMigrationRulesTests.cs`'s own placement precedent for a pure-function test file), covering all 7 policies, both `IsStronger` branches, the full `RequestGMResolution` → `ResolveActiveEffectStackConflict` cycle (all 3 outcomes), one precondition-rejection test, and one test that reloads a genuinely persisted row independently before feeding it into `ResolveStacking`.

No change to `ADR-028`, `IActiveEffectRepository`/`SqliteActiveEffectRepository`/`ActiveEffect`, any `.csproj`/`.asmdef`, duration/expiry/removal logic, or any scope guard.

## 5. Milestones

### M1 — Research and design

- [x] Read `ActiveEffect.cs`/`ActiveEffectRecord.cs`/`ActiveEffectRepositoryContracts.cs`/`SqliteActiveEffectRepository.cs` in full.
- [x] Read `ItemDefinitionMigrationRules.cs`'s full `ComputeBlockingIssues` implementation and its own DTO constructor style.
- [x] Read `ADR-028` §7/§15/§18.3 in full.
- [x] Confirm (by direct `.csproj`/`.asmdef` read) that `Odyssey.Rules` has no JSON dependency, ruling out a payload-content comparison.
- [x] Confirm (by direct `.csproj` read) that `Odyssey.Tests.Persistence` already reaches `Odyssey.Rules` transitively.
- [x] Decide the persistence variant for `ActiveEffectStackConflict`: no new repository method, by direct analogy to `402`/`403`'s own decide/apply split.

### M2 — Production code

- [x] Write `EffectStackRules.cs` (`Odyssey.Rules.Effects`).
- [x] Write `ActiveEffectStackingRules.cs` (`Odyssey.Application.Effects`): `ActiveEffectStackDecisionKind`, `ActiveEffectStackDecision`, `ActiveEffectStackConflict`, `ActiveEffectStackConflictResolution`, `ActiveEffectStackingRules`.
- [x] Build the solution — succeeded on the first attempt, 0 warnings, 0 errors.

### M3 — Tests and scope verification

- [x] Write `ActiveEffectStackingRulesTests.cs` (`TC-ACTIVEEFFECT-020`-`035`).
- [x] Run the new tests — 16/16 passed on the first run.
- [x] Run the two scope-guard tests `ODY-S05-502` narrowed, unmodified, to confirm this task's new files trip neither — both pass.
- [x] Run the full solution test suite — all green, no regressions.

### M4 — Metadata, docs, validation, PR

- [x] Register `TC-ACTIVEEFFECT-020`-`035` in `Tests/Metadata/test-catalog.json`.
- [x] Run all 5 required validation commands; record real results.
- [x] Write the task contract and this ExecPlan to full depth.
- [ ] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 in §15 to `In Review`.
- [ ] Review `git diff --name-status` for scope.
- [ ] Commit, push, and open Draft PR.
- [ ] Record PR link and backlog status.

## 6. Progress log

- 2026-09-12 — Created worktree `D:\ody_s05_503_wt`, branch `feat/ody-s05-503-stacking-policy-resolution`, off `origin/main` at `7e8507f` (merge of PR #137).
- 2026-09-12 — Read all required precedents; confirmed via direct project-file read that `Odyssey.Rules` has no JSON dependency, which — combined with `ADR-028` §18.3's own rejection of a generic potency field — ruled out a payload-content comparison for `ReplaceIfStronger`.
- 2026-09-12 — Decided (task contract §18) to add no new `IActiveEffectRepository`/`SqliteActiveEffectRepository` method, following `ODY-S05-402`/`403`'s own decide/apply split precedent exactly.
- 2026-09-12 — Wrote `EffectStackRules.cs` and `ActiveEffectStackingRules.cs`; `dotnet build` succeeded on the first attempt.
- 2026-09-12 — Wrote `ActiveEffectStackingRulesTests.cs` (16 tests, `TC-ACTIVEEFFECT-020`-`035`); ran them — 16/16 passed on the first run, no fixes needed.
- 2026-09-12 — Ran the two scope-guard tests `ODY-S05-502` narrowed (unmodified) to confirm this task's new files trip neither — both pass.
- 2026-09-12 — Full-suite `dotnet test DotNet\Odyssey.Core.sln`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 560/560 — all green, no regressions.
- 2026-09-12 — Registered `TC-ACTIVEEFFECT-020`-`035` in `Tests/Metadata/test-catalog.json`.
- 2026-09-12 — Wrote the task contract and this ExecPlan to full depth.

## 7. Decisions

See task contract §18 for the full decision log: no new `IActiveEffectRepository`/`SqliteActiveEffectRepository` method (the decide/apply split, by analogy to `402`/`403`); `ActiveEffectStackConflict` wrapping a full candidate record instead of four separate fields; `EffectStackRules.IsStronger`'s own `DefinitionSnapshotVersion`-based heuristic and the two constraints (`ADR-028` §18.3, no JSON dependency) that ruled out the alternative; and placing the new test file in `Odyssey.Tests.Persistence` rather than a new project.

## 8. Discoveries and deviations

- `Odyssey.Rules` has no JSON library dependency today — a genuine constraint discovered by direct `.csproj`/`.asmdef` read, not assumed from `ADR-028`'s own "opaque payload" wording. See task contract §18.
- `ADR-028` §18.3 had already anticipated and rejected the most obvious alternative (a generic potency field) before this task began — this task's own design had to actively avoid reintroducing that rejected idea under a different name (e.g., a payload-embedded "magnitude" key would have been functionally equivalent to the rejected field).
- No scope-guard file needed any edit — confirmed by running both guard tests unmodified after this task's own files were added, not merely by reasoning about scan scope in the abstract.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 560/560.
- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS (no new `ERROR_CODES.md` rows required).
- `.\scripts\verify-test-structure.ps1`: PASS.
- Diff review: pending final `git diff --name-status` confirmation before commit.

## 10. Recovery and rollback

Rollback of THIS PR is a normal revert before merge. No persistence write path exists in this task at all, so there is no data-loss risk to protect against.

## 11. Open questions and blockers

None remain open for this task. Recorded for later tasks in this range (not resolved here): whichever task first needs to execute an `ActiveEffectStackDecision` against real persistence (most likely `ODY-S05-505`) must design its own repository method informed by its own real call site; a future task with real Ruleset-driven effect potency must replace `EffectStackRules.IsStronger`'s own body without changing its signature; a future task may need a cross-session persistence mechanism for a pending `ActiveEffectStackConflict` if a real GM workflow requires one.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation tasks in this range: `ODY-S05-504` (Non-Combat Duration/Expiry), `ODY-S05-505` (WhileItemEquipped Wiring + Item-Triggered Creation), `ODY-S05-506` (RemoveActiveEffect Command + Direct-Creation Permission Gates) — none started proactively; each awaits its own ТЗ.
