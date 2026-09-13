# ODY-S05-506 — RemoveActiveEffect Command + Direct-Creation Permission Gates

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-506-remove-and-direct-creation`
**Pull request:** TBD (Draft)
**Last updated:** 2026-09-13 UTC

## 1. Purpose and user-visible outcome

Give a MainGM two new, explicit powers over `ActiveEffect`: end one early (`RemoveActiveEffect`) and create one with no item cause at all (direct creation). After this task, both `ActiveEffectSourceRef.ForGMDirect()` and `ActiveEffectStatus.Removed` — both declared since `ODY-S05-502` but unused for this purpose — have a real, MainGM-gated production path.

## 2. Task contract

- Goal: `IActiveEffectRepository.RemoveActiveEffect` (new repository method) + `ActiveEffectDirectCommandService.CreateDirectActiveEffect` (new Application wrapper over the unmodified `CreateActiveEffect`).
- Acceptance criteria: see task contract §9 (9 items) — CAS-guarded removal via `SqliteSavingPipeline`; MainGM check as literal first statement on both paths; direct creation delegates to unmodified `CreateActiveEffect`; denied operations cause zero mutation; the `503`-era stacking-conflict permission gap is re-recorded, not closed; no out-of-scope logic; green tests; backlog updated; Draft PR.
- Requirement IDs: `ODY-S05-506`, `SLICE-05`.
- In scope: one new repository method, one new Application service, one new error code, tests/metadata/docs/backlog.
- Out of scope: `ItemEffectLifecycleService.cs` (`505`, deliberately ungated); `ActiveEffectStackingRules.cs`/`ActiveEffectExpiryRules.cs` (`503`/`504`); `ResolveActiveEffectStackConflict`'s own permission gap; any project-file change.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §15/§15.1 row 5; `ADR-028` §6 rule 2/§9/§10/§12; `ADR-025` §5.1/§5.2 (the two permission idioms); `DeleteCharacterPermanently` (MainGM-gate placement precedent); `ExpireActiveEffect`/`SetItemEffectEquipped` (CAS-via-`SqliteSavingPipeline` precedent); `InventoryMovementFailures.Denied` (shared-error-code precedent).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Branch `feat/ody-s05-506-remove-and-direct-creation` was created fresh off `origin/main` at `3910ba1` (merge of PR #140, `ODY-S05-505`). Backlog row 5 in §15 (`ODY-S05-506`) was `Proposed`.
- Read all required precedents in full before writing code: `IActiveEffectRepository`/`SqliteActiveEffectRepository` (post-505 state, 6 methods), `ActiveEffect.cs`'s own `ForGMDirect()` doc comment (already naming this task), `ItemEffectLifecycleService.cs`'s own class doc comment and a `grep` confirming zero `MainGm`/`actorIsMainGm` references (the deliberately-ungated contrast), `DeleteCharacterPermanently`'s own exact MainGM-gate placement, a `grep` across four MainGM-gated files confirming no shared `RequireMainGm` helper exists, `InventoryMovementFailures.Denied` vs. `CharacterDeletionDenied`'s own two error-code styles, `PersistenceFailures.ActiveEffectRevisionConflict` (`504`, reused unchanged), and `ActiveEffectStackingRules.ResolveActiveEffectStackConflict`'s own confirmed lack of any permission check.

Assumptions: none.

## 4. Proposed approach

- `SqliteActiveEffectRepository.RemoveActiveEffect(campaign, campaignId, activeEffectId, actorUserId, actorIsMainGm, expectedRevision, commandId, correlationId)`: `actorIsMainGm` checked as the literal first statement (before even argument-shape validation that touches no I/O anyway, matching `DeleteCharacterPermanently`'s own ordering), then campaign-boundary validation, then a `SqliteSavingPipeline.Execute` transaction that reads the current row, rejects if its own `Status` is not `Active`/`Suspended` (already-terminal `Expired`/`Removed` → `ActiveEffectRevisionConflict`), then a CAS `UPDATE ... SET Status='Removed', Revision=Revision+1 ... WHERE ... AND Revision=$expectedRevision`, reusing `EnsureActiveEffectTables`/`ReplayByCommandId`/`SelectColumns`/`ReadRecord`/`ReadOneById` unchanged.
- `ActiveEffectDirectCommandService.CreateDirectActiveEffect(effects, campaign, campaignId, effectDefinitionRef, effectMechanicsSnapshot, target, actorUserId, actorIsMainGm, appliedAt, expiresAt, commandId, correlationId)`: `actorIsMainGm` checked first, before constructing anything; then builds a full `ActiveEffect`/`ActiveEffectRecord` with `SourceRef = ActiveEffectSourceRef.ForGMDirect()`, `Status = Active`, `StackCount = 1`, `Revision = 1`; delegates entirely to the unmodified `IActiveEffectRepository.CreateActiveEffect`.
- One new error code, `persistence.active_effect.operation_denied`, shared by both operations — following `InventoryMovementFailures.Denied`'s own "one code, several operations of one domain" precedent, since the backlog's own text explicitly groups these two operations together.
- Tests: one new file, `ActiveEffectGmCommandTests.cs`, in `Odyssey.Tests.Persistence` (matching the placement precedent of every prior `ActiveEffect*` test file in this range), covering both operations' success, denial-with-zero-mutation-proof, CAS conflict, terminal-status rejection, replay idempotency, and cross-campaign rejection.

No change to `ItemEffectLifecycleService.cs`, `ActiveEffectStackingRules.cs`/`ActiveEffectExpiryRules.cs`, `SqliteInventoryRepository.cs`/`SqliteCharacterRepository.cs`, any ADR, or any `.asmdef`/`.csproj`.

## 5. Milestones

### M1 — Research and design

- [x] Read `IActiveEffectRepository`/`SqliteActiveEffectRepository` (post-505), `ActiveEffect.cs`'s own `ForGMDirect()` doc comment, `ItemEffectLifecycleService.cs`'s own class doc comment (confirmed no MainGM gate by direct `grep`), `DeleteCharacterPermanently`'s own gate placement, the four MainGM-gated files (confirmed no shared helper), `InventoryMovementFailures.Denied`/`CharacterDeletionDenied`'s two error-code styles, and `ResolveActiveEffectStackConflict`'s own confirmed lack of a permission check.
- [x] Decide the error-code style (shared, per `InventoryMovementFailures.Denied`'s own precedent) and the gate-placement split (repository-level for removal, Application-wrapper for creation).

### M2 — Production code

- [x] Add `PersistenceActiveEffectOperationDenied` to `ErrorCodes.cs` and `PersistenceFailures.ActiveEffectOperationDenied` to `CampaignRepositoryContracts.cs`.
- [x] Add `RemoveActiveEffect` to `IActiveEffectRepository`; implement in `SqliteActiveEffectRepository`.
- [x] Write `ActiveEffectDirectCommandService.cs`.
- [x] Build the solution — succeeded on the first attempt after fixing a missing `using Odyssey.Application.Time;` in the new test file.
- [x] Confirm no scope guard or architecture test references `SqliteActiveEffectRepository`/`IActiveEffectRepository` by method name or count (direct `grep`, and a real full-suite `dotnet test` run before writing new tests).

### M3 — Tests and full-suite verification

- [x] Write `ActiveEffectGmCommandTests.cs` (`TC-ACTIVEEFFECT-069`-`078`).
- [x] Run the new tests — one failure on the first run (hardcoded `UserId` test fixtures used non-hex characters, failing canonical parsing); fixed by using valid hex-only fixture ids; 11/11 passed on the second run.
- [x] Run the full solution test suite — all green, no regressions.

### M4 — Metadata, docs, validation, PR

- [x] Register `TC-ACTIVEEFFECT-069`-`078` in `Tests/Metadata/test-catalog.json`.
- [x] Run all 5 required validation commands; record real results.
- [x] Write the task contract and this ExecPlan to full depth.
- [ ] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 in §15 to `In Review`.
- [ ] Review `git diff --name-status` for scope.
- [ ] Commit, push, and open Draft PR.
- [ ] Record PR link and backlog status.

## 6. Progress log

- 2026-09-13 — Created worktree `D:\ody_s05_506_wt`, branch `feat/ody-s05-506-remove-and-direct-creation`, off `origin/main` at `3910ba1` (merge of PR #140).
- 2026-09-13 — Read all required precedents; confirmed by direct `grep` that `ItemEffectLifecycleService.cs` genuinely has no MainGM gate and that no shared `RequireMainGm` helper exists anywhere in the codebase.
- 2026-09-13 — Decided the shared-error-code style and the repository-vs-Application gate-placement split; added the new error code, `RemoveActiveEffect`, and `ActiveEffectDirectCommandService`; `dotnet build` succeeded on the first attempt after one missing `using` fix in the new test file.
- 2026-09-13 — Ran the full solution test suite with only the new production code (no new tests yet) to isolate any regression — green, no regressions.
- 2026-09-13 — Wrote `ActiveEffectGmCommandTests.cs`; first run failed one test's own `SetUp` (hardcoded `UserId` fixtures used non-hex characters `m`/`p`, failing `UserId.Parse`'s own canonical-hex validation) — fixed by using valid hex-only fixture ids; second run: 11/11 passed.
- 2026-09-13 — Full-suite `dotnet test DotNet\Odyssey.Core.sln`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 606/606 — all green, no regressions.
- 2026-09-13 — Registered `TC-ACTIVEEFFECT-069`-`078` in `Tests/Metadata/test-catalog.json`.
- 2026-09-13 — Wrote the task contract and this ExecPlan to full depth.

## 7. Decisions

See task contract §18 for the full decision log: the repository-level (not Application-wrapper) placement of `RemoveActiveEffect`'s own MainGM gate, mirroring `DeleteCharacterPermanently` exactly; the Application-layer wrapper (not a new repository method) for direct creation; the shared-error-code style over the per-operation style; `RemoveActiveEffect` accepting both `Active` and `Suspended` as valid source statuses; and the re-recorded (not newly discovered) `ResolveActiveEffectStackConflict` permission gap.

## 8. Discoveries and deviations

- One test-authoring mistake (non-hex characters in hardcoded `UserId` test fixtures) was caught by the first real test run and fixed immediately — not a design issue, purely a test-fixture typo.
- No other discovery diverged from the governing ТЗ's own expectations — the ТЗ's own structural analysis (no new repository method needed for creation; a new one needed for removal; no shared MainGM helper exists; the stacking-conflict gap is real and out of scope) all held up under direct verification.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 606/606.
- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS (new `ERROR_CODES.md` row accepted).
- `.\scripts\verify-test-structure.ps1`: PASS.
- Diff review: pending final `git diff --name-status` confirmation before commit.

## 10. Recovery and rollback

Rollback of THIS PR is a normal revert before merge. `RemoveActiveEffect` never physically deletes a row (`ADR-012`'s append-only-history discipline, already established by `ExpireActiveEffect`/`SetItemEffectEquipped`).

## 11. Open questions and blockers

None remain open for this task's own scope. Recorded, not resolved here: `ActiveEffectStackingRules.ResolveActiveEffectStackConflict` still has no permission check — a future task must decide whether/how it should be gated, if ever needed.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task in this range: `ODY-S05-507` (Item-Sourced Abilities/Effects Integration Fixtures) — not started proactively; awaits its own ТЗ.
