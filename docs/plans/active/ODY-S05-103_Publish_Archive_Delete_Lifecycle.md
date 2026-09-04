# ODY-S05-103 — Publish/Archive/Delete Lifecycle

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-103-publish-archive-delete-lifecycle`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/110
**Last updated:** 2026-09-04 UTC

## 1. Purpose and user-visible outcome

Give the Content Catalog its own publish/archive/physical-delete lifecycle commands: a valid Draft becomes an immutable Published version (gated by `ODY-S05-104`'s own server-side validation), a Published definition can be archived without losing readability, an unused Draft can be physically removed, and MainGM gets a dedicated Archived-list query. No UI, no runtime item/inventory/equipment/effect state, no attack pipeline.

## 2. Task contract

- Goal: a compiling, tested `ContentCatalogLifecycleService` in `Odyssey.Application.Content` (`PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`/`ListArchivedDefinitions`) plus three new `IContentCatalogRepository` methods, following `ContentCatalogAuthoringService`'s exact structural precedent.
- Acceptance criteria: publish gated by real server-side validation with zero mutation on rejection; archive never physically deletes and keeps the row loadable; physical delete restricted to unused Drafts, rejecting Published/Archived/referenced targets; every command idempotent via `CommandId`; MainGM-only, no new role; no runtime item/inventory/equipment/effect code; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-103`, `ADR-027` §4.1/9/12/20.
- In scope: `ContentCatalogLifecycleContracts.cs` (new), three repository method implementations, two new `ErrorCode`s (+ one doc-note update to a reused code), real tests, `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/4 status update, this task's own contract/plan.
- Out of scope: runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect (and any dependency check against them), attack resolution, item use/equip/consume, any Unity/UI, balanced content fixtures (`ODY-S05-106`), `.odcontent` import/export, campaign-specific overrides, `ContentBlock` execution, any `ADR-001`-`027` content change.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §6 (`ODY-S05-103` boundary paragraph)/§3.5, `ADR-027` §4.1/9/12/20, `11_Content_Block_System` §6, `ContentCatalog.cs`/`ContentCatalogRepositoryContracts.cs`/`ContentCatalogAuthoringContracts.cs`/`CatalogValidationContracts.cs`/`SqliteContentCatalogRepository.cs` (full reads).
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main`, which already includes PR #109 (`ODY-S05-104`'s own follow-up, merged) atop PR #108/#107/#106/#105.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 still described PR #109 as "In Review" despite it already being merged -- corrected to `Done` (both #108/#109 links) as this task's own first step.
- `IContentCatalogRepository`/`SqliteContentCatalogRepository` read in full: `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`GetContentDefinition`/`ListContentDefinitions`/`CreateNextDraftVersionFromPublished` exist; no publish/archive/delete method exists yet. `ContentDefinitionCommandLedger` (`CommandId` -> `ContentDefinitionId`) is the sole idempotency source of truth, via `TryFindByCommandId`/`InsertCommandLedgerEntry` helpers -- reusable as-is for publish/archive, but `TryFindByCommandId` re-selects the target row, which will not exist after a successful delete.
- `ContentCatalogAuthoringContracts.cs`/`CatalogValidationContracts.cs` read in full: the exact structural precedent (Application-layer service, MainGM check before any repository call, `ValidateDraftForPublish`'s own `Result<CatalogValidationResult>` shape) this task's own lifecycle service follows and integrates.
- `11_Content_Block_System` §6.2 read in full: `Version` only changes at publication; confirmed against `ODY-S05-102`'s own `CreateNextDraftVersionFromPublished` (mints a new `ContentDefinitionId` for a "next version" rather than bumping the same row) that a single row is published at most once, so `Version` only ever needs to become `1`.
- `ContentCatalog.cs`'s own `ContentDefinitionRef` constructor requires `Version >= 1` -- confirmed this means no reference can ever legitimately target a genuine Draft (`Version == 0`) through the public API today, directly informing the physical-delete dependency-check design (see decisions).

Assumptions: none.

## 4. Proposed approach

- Application (`ContentCatalogLifecycleContracts.cs`): `ContentCatalogLifecycleService` (static, mirrors `ContentCatalogAuthoringService`) with `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`/`ListArchivedDefinitions`, each checking `request.ActorIsMainGm` before calling `IContentCatalogRepository` (reusing `ContentCatalogAuthoringFailures.NotMainGm` directly, no duplicate authorization error). `PublishDefinition` fetches the current record first; only when it is still `Draft` does it call `CatalogValidationService.ValidateDraftForPublish` and refuse to call the repository's own `PublishDefinition` at all if invalid -- once the record is no longer Draft, validation is skipped entirely so a legitimate `CommandId` replay is never mistaken for an invalid publish attempt. Four request DTOs (`PublishDefinitionRequest`/`ArchiveDefinitionRequest`/`DeleteDraftDefinitionRequest`/`ListArchivedDefinitionsRequest`) and `ContentCatalogLifecycleFailures.PublishValidationFailed` (new `ErrorCode`).
- Application (repository contract extension): `IContentCatalogRepository.PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`.
- Persistence: `SqliteContentCatalogRepository.PublishDefinition` (Draft-only, revision-guarded, sets `Status=Published`/`Version=1`/`PublishedByUserId`/`PublishedAt`, increments `Revision`); `.ArchiveDefinition` (Published-only, sets `Status=Archived`/`ArchivedAt`/`ArchiveReason`, `Revision` unchanged); `.DeleteDraftDefinition` (Draft-only, atomically checks a new `IsReferencedByAnotherDefinition` SQL substring scan over `DependencyRefsJson`/`PropertiesJson` before the physical `DELETE`, all in one transaction; idempotent via a new ledger-existence-only `CommandLedgerContainsCommandId` check since the row is gone after success, not the existing row-reselecting `TryFindByCommandId`).
- Registry: two new `ErrorCode`s (`PersistenceContentDefinitionReferenced`, `ContentCatalogPublishValidationFailed`) registered in both `ErrorCodes.cs` and `ERROR_CODES.md`; `ContentDefinitionNotPublished`'s own doc-comment updated to note its reuse by `ArchiveDefinition`.
- Tests (`ContentCatalogLifecycleServiceTests.cs`, real SQLite, mirroring `ContentCatalogAuthoringServiceTests`'s own fixture convention): MainGM/non-MainGM for all three mutating commands; valid/invalid publish; post-publish immutability; non-Draft publish; publish replay; archive on Published/Draft; Archived-list separation; delete on unused/Published/Archived/referenced Drafts; delete replay after row is gone; a reflection scan + a schema scan against runtime item/inventory/equipment/effect state.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 corrected to `Done`; row 3 (`ODY-S05-103`) marked `In Review` with PR link once opened.

No Unity/UI code, no runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect behavior, no `ADR-001`-`027` content change, no new persistence table.

## 5. Milestones

### M1 — Repository lifecycle primitives

- [x] `IContentCatalogRepository.PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` extension.
- [x] `SqliteContentCatalogRepository` implementations, `IsReferencedByAnotherDefinition`, `CommandLedgerContainsCommandId`.
- [x] `PersistenceFailures`/`ErrorCodes` new entries.
- [x] `dotnet build` passes on first attempt.

### M2 — Application service and tests

- [x] `ContentCatalogLifecycleContracts.cs` (service, four requests, failures class).
- [x] `ContentCatalogLifecycleServiceTests.cs` (Persistence, real SQLite, 20 cases) -- one fixture fix needed (lazy table creation), then all passed.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.

### M3 — Validation and review readiness

- [x] `dotnet build DotNet\Odyssey.Core.sln` (full solution).
- [x] `dotnet test DotNet\Odyssey.Core.sln` (full suite).
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/4 status update (row 3 `In Review`; PR link to follow).
- [x] Commit, push, and open Draft PR (PR #110).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-04 — Preflight: `git fetch origin` confirmed PR #109 already merged; fast-forwarded `main`; created branch `feat/ody-s05-103-publish-archive-delete-lifecycle`. Corrected `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 (`ODY-S05-104`) from stale "In Review" to `Done` (PR #108/#109 both linked).
- 2026-09-04 — Read `SLICE-05_IMPLEMENTATION_BACKLOG.md` §6/§3.5, `ADR-027` §4.1/9/12/20, `11_Content_Block_System` §6, `ContentCatalog.cs`, `ContentCatalogRepositoryContracts.cs`, `ContentCatalogAuthoringContracts.cs`, `CatalogValidationContracts.cs`, `SqliteContentCatalogRepository.cs` in full.
- 2026-09-04 — Designed and confirmed (via the `Version >= 1` constructor check on `ContentDefinitionRef`) that a genuine Draft can never be the target of any existing reference through the public API -- directly shaping the physical-delete dependency-scan design (see decisions).
- 2026-09-04 — Caught and fixed a design bug before writing any code: naively re-running `ValidateDraftForPublish` on every `PublishDefinition` call (including replays) would incorrectly reject a legitimate `CommandId` replay once the record is no longer Draft (validation would always report `DefinitionNotDraft`). Fixed by only validating while the target's current status is still Draft.
- 2026-09-04 — Implemented the three repository methods plus `IsReferencedByAnotherDefinition`/`CommandLedgerContainsCommandId`; `dotnet build` passed on first attempt (0 warnings, 0 errors).
- 2026-09-04 — Implemented `ContentCatalogLifecycleContracts.cs`; `dotnet build` passed on the next run.
- 2026-09-04 — Implemented `ContentCatalogLifecycleServiceTests.cs` (20 cases); first `dotnet test` run failed one test (`LifecycleLayer_IntroducesNoNewPersistenceTable` -- the `ContentDefinition` table is created lazily on first repository use, and this test's own fixture had not yet triggered that); fixed by seeding one Draft first, second run passed 20/20.
- 2026-09-04 — Added `docs/errors/ERROR_CODES.md` rows (plus a doc-note on the reused `not_published` code) and `Tests/Metadata/test-catalog.json` entries `TC-CATALOG-078`-`097`, referencing this task contract by `taskId` before running `verify-test-structure.ps1`.
- 2026-09-04 — Wrote this task's own contract and ExecPlan.

## 7. Decisions

- 2026-09-04 — Decision: publishing sets `Version = 1` unconditionally, the first and only published version this row will ever carry. Authority: `11_Content_Block_System` §6.2 plus `ODY-S05-102`'s own `CreateNextDraftVersionFromPublished` precedent (a "next version" mints a new `ContentDefinitionId`, never bumps this row's own `Version` again).
- 2026-09-04 — Decision: `ContentCatalogLifecycleService.PublishDefinition` only calls `CatalogValidationService.ValidateDraftForPublish` while the target is still `Draft`; once Published, validation is skipped and the repository's own ledger-replay or `NotDraft` check handles the remaining cases correctly. Authority: avoids a genuine correctness bug (a legitimate replay would otherwise always be rejected as invalid) found and fixed during design, before any test exposed it.
- 2026-09-04 — Decision: `ArchiveDefinition` reuses `PersistenceContentDefinitionNotPublished` rather than a new error code. Authority: identical underlying meaning to its existing use in `CreateNextDraftVersionFromPublished`; this task's own ТЗ explicit "add new codes only if existing insufficient" instruction.
- 2026-09-04 — Decision: the physical-delete catalog-dependency scan is a plain SQL substring match inside `SqliteContentCatalogRepository`, not an Application-layer `TypedDefinitionCodec`-based decode. Authority: keeps the check-and-delete atomic in one transaction (no TOCTOU race), avoids an unnecessary new dependency direction, and the canonical `ContentDefinitionId` string is always present verbatim in any JSON-embedded reference to it.
- 2026-09-04 — Decision: `DeleteDraftDefinition`'s own idempotent-replay check uses a new ledger-existence-only helper, not the existing row-reselecting `TryFindByCommandId`. Authority: this task's own ТЗ explicit "if the existing command ledger cannot replay a deleted row, add the smallest safe mechanism needed" instruction.
- 2026-09-04 — Decision (explicitly requested by the ТЗ): the delete-dependency check matches by `ContentDefinitionId` alone (ignoring version), and is implemented and tested even though a `ContentDefinitionRef`'s own `Version >= 1` requirement means no reference can currently target a genuine Draft through the public API -- a defensive, forward-compatible safety net, verified via a test-only direct-SQL-seeded scenario, not dead code.

## 8. Discoveries and deviations

- No architectural question was found during implementation that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101`/`102`/`104`'s own foundation do not already answer -- no ADR was touched or extended.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 was stale (`In Review` despite PR #109 already merged) -- corrected as this task's own explicit preflight step.
- One test fixture bug (lazy table creation not yet triggered) was found and fixed during this task's own first test run, before any code review -- not a defect in the production code.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- Full-suite `dotnet test DotNet\Odyssey.Core.sln`: 609/609 passed, no regression.
- `.\scripts\verify-format.ps1`: `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1`: `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: `TC-ARCH-001 PASS valid ADR-001 graph passes`.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- no schema migration exists to roll back; all new methods are purely additive over `ODY-S05-101`'s own existing `ContentDefinition`/`ContentDefinitionCommandLedger` tables.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-027`/`11_Content_Block_System`/`ODY-S05-101`/`102`/`104` do not already answer. The runtime-reference-check gap for physical delete (`ADR-027` §4.1 rule 5) is an explicit, recorded future extension boundary, not an open question -- no runtime item/inventory/equipment state exists anywhere in this codebase yet to check against.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/110 (amended twice). Enables `ODY-S05-106` (Minimal Test Catalog Fixtures) to prove the full Foundation/Authoring/Validation/Publish pipeline end-to-end for the first time.

## 13. Amendment (2026-09-04) — DeleteDraftDefinition idempotency fix

- Defect: `DeleteDraftDefinition`'s original idempotency check tested only whether the caller's `CommandId` existed anywhere in the *shared* `ContentDefinitionCommandLedger` -- the same table create/update/publish/archive all write to. Reusing a `CommandId` already recorded by any of those other operations, or by a *different* definition's own prior delete, made the method return `Success` without ever deleting the row actually requested.
- Fix (first pass, itself incomplete -- see §14): a new, dedicated `ContentDefinitionDeleteLedger` table (`CommandId` primary key -> `ContentDefinitionId`, `DeletedAt`), written and checked only by `DeleteDraftDefinition`. A hit compares the recorded `ContentDefinitionId` against the caller's own target: a match is a genuine replay (`Success`); a mismatch is a real identity violation, rejected with the existing `CommandIdentityMismatch` code (no new `ErrorCode`). No hit meant the method proceeded to actually check/delete the row -- but this alone stopped checking the shared ledger at all (see §14).
- Tests added: `TC-CATALOG-098`/`099` (later `098` was rewritten -- see §14).
- Validation re-run: `dotnet build` (0/0), `dotnet test` full suite (611/611, no regression), `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.

## 14. Amendment (2026-09-04, second) — DeleteDraftDefinition must also reject CommandIds already used by non-delete operations

- Defect: the first pass's own delete-only ledger, checked in isolation, no longer consulted the shared `ContentDefinitionCommandLedger` -- so a `CommandId` already used by a non-delete operation (create/update/publish/archive/`CreateNextDraftVersionFromPublished`) on a still-existing row would never appear in the delete-only ledger, and `DeleteDraftDefinition` would incorrectly proceed to *actually delete* that row -- a real, unintended physical delete.
- Fix: added a second check, right after the delete-only ledger check: any hit in the shared `ContentDefinitionCommandLedger` (bare existence, no target comparison needed -- a hit there was never a delete) is rejected with `CommandIdentityMismatch`, and nothing is deleted. Only when the `CommandId` appears in neither ledger does the method proceed normally.
- `TC-CATALOG-098` rewritten: reusing a `CreateDraftContentDefinition` `CommandId` against the same still-existing Draft must now fail with `CommandIdentityMismatch` and leave the Draft readable/untouched (previously asserted "actually deletes it," describing the still-incomplete first-pass fix). `TC-CATALOG-099` unchanged. `ContentCatalogRepositoryContracts.cs`'s own stale `DeleteDraftDefinition` doc comment (still describing the single-ledger model) corrected to describe the two-ledger check.
- Validation re-run: `dotnet build` (0/0), `dotnet test` full suite (611/611, no regression), `verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- PR #110 stays Draft pending re-review.
