# ODY-S05-102 — GM Catalog Authoring MVP

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-102-gm-catalog-authoring-mvp`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/106
**Last updated:** 2026-09-03 UTC

## 1. Purpose and user-visible outcome

Give MainGM a real, application-level authoring surface over the `ODY-S05-101` Content Catalog foundation: create a Draft, edit an existing Draft with revision-guarded optimistic concurrency, and branch a new Draft off an already-Published definition without editing that Published source in place. No publish/archive/delete, no validation, no typed properties, no runtime item/inventory/equipment/effect behavior.

## 2. Task contract

- Goal: a compiling, tested MainGM catalog authoring layer in `Odyssey.Application` (service + contracts) plus one minimal `Odyssey.Persistence` repository extension, following `BoardMovementService`'s exact structural precedent for an Application-layer service sitting above a repository.
- Acceptance criteria: MainGM create/edit/create-next-draft all implemented and covered by real tests; non-MainGM rejected before any repository call; Published/Archived cannot be edited in place; create-next-draft copies fields without mutating the source; all three commands idempotent via the existing `ContentDefinitionCommandLedger`; no publish/archive/delete/validation/typed-property/runtime behavior; `ADR-001`–`027` unmodified; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-102`, `ADR-027` §4.1/12.
- In scope: `ContentCatalogAuthoringService`/its three request DTOs/`ContentCatalogAuthoringFailures` (Application, new), `IContentCatalogRepository.CreateNextDraftVersionFromPublished` extension + `SqliteContentCatalogRepository` implementation, two new `PersistenceFailures`/`ErrorCodes` entries, real tests, `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 1/2 status update, this task's own contract/plan.
- Out of scope: publish/archive/delete (`ODY-S05-103`), validation (`ODY-S05-104`), typed properties (`ODY-S05-105`), fixtures (`ODY-S05-106`), any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect runtime, any Unity/UI code, any `ADR-001`–`027` content change, any new role/permission extension.
- Required authorities: `ADR-027` §4/4.1/12/20, `ContentCatalogRepositoryContracts.cs`/`SqliteContentCatalogRepository.cs` (`ODY-S05-101` foundation, full read), `BoardMovementService.cs`/`BoardContracts.cs` (binding structural precedent, full read), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §5/§6.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at PR #105's merge commit (`ODY-S05-101`, Content Catalog Foundation + idempotency-fix amendment), independently verified via `git merge-base --is-ancestor`.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 still read `In Review` despite PR #105 being merged — corrected to `Done` as this task's own first step, per the ТЗ's explicit preflight instruction.
- `IContentCatalogRepository`/`SqliteContentCatalogRepository` were read in full: `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`GetContentDefinition`/`ListContentDefinitions` exist; no `PublishDefinition` and no `CreateNextDraftVersionFromPublished`-shaped method exists yet. The `ContentDefinitionCommandLedger` (`ODY-S05-101` amendment) is the sole idempotency source of truth, via `TryFindByCommandId`/`InsertCommandLedgerEntry` helpers reusable as-is by a new repository method.
- `BoardMovementService`/`BoardContracts.cs` read in full: static Application-layer service, authorization checked before the repository is called, request DTO carries `ActorIsMainGm` (caller-supplied baseline role, no real session/role model — `ADR-019`/`SLICE-02` scope, not reopened), and a dedicated `BoardFailures` class distinct from `PersistenceFailures`.

Assumptions: none.

## 4. Proposed approach

- Application: `ContentCatalogAuthoringService` (static, mirrors `BoardMovementService`) with `CreateDraftDefinition`/`UpdateDraftDefinition`/`CreateNextDraftVersionFromPublished`, each checking `request.ActorIsMainGm` before calling `IContentCatalogRepository`. Three request DTOs (`CreateDraftDefinitionRequest`/`UpdateDraftDefinitionRequest`/`CreateNextDraftVersionFromPublishedRequest`), each carrying `ActorUserId`/`ActorIsMainGm`/`CommandId`/`CorrelationId`. `ContentCatalogAuthoringFailures.NotMainGm` (new `ErrorCode`).
- Application (repository contract extension): `IContentCatalogRepository.CreateNextDraftVersionFromPublished(campaign, publishedDefinitionId, createdByUserId, commandId, correlationId)`.
- Persistence: `SqliteContentCatalogRepository.CreateNextDraftVersionFromPublished` — reuses `TryFindByCommandId`/`SelectForUpdate`/`InsertCommandLedgerEntry` unmodified; rejects a non-Published source with a new `PersistenceContentDefinitionNotPublished` error; mints a new `ContentDefinitionId` (`Status=Draft`, `Version=0`, `Revision=1`) copying the source's own `DefinitionType`/`Name`/`Description`/`RulesetCompatibility`/`Tags`/`PropertiesJson`/`DependencyRefs`; never writes to the source row.
- Tests: `ContentCatalogAuthoringServiceTests` (real SQLite, mirroring `BoardMovementServiceTests`'s own fixture convention) — MainGM/non-MainGM for all three commands, stale-revision rejection, Published/Archived-immutability rejection, source-untouched proof, idempotency for all three commands, and a reflection-based scan proving no runtime item/inventory/equipment/effect type exists in `Odyssey.Application.Content`.
- Registry: two new `ErrorCode`s registered in both `ErrorCodes.cs` and `ERROR_CODES.md`, referencing eleven new `TC-CATALOG-013`–`023` entries added to `test-catalog.json`.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 corrected to `Done`; row 2 (`ODY-S05-102`) marked with PR link/evidence.

No Unity/UI code, no publish/archive/delete/validation/typed-property/runtime behavior, no `ADR-001`–`027` content change.

## 5. Milestones

### M1 — Application service and repository extension

- [x] `ContentCatalogAuthoringContracts.cs` (service, three requests, failures class).
- [x] `IContentCatalogRepository.CreateNextDraftVersionFromPublished` extension.
- [x] `SqliteContentCatalogRepository.CreateNextDraftVersionFromPublished` implementation.
- [x] `PersistenceFailures`/`ErrorCodes` new entries.

### M2 — Tests and registry

- [x] `ContentCatalogAuthoringServiceTests` (Persistence, real SQLite, 14 cases).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [x] `dotnet build`/`dotnet test` full suite green, no regression.

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 1/2 status update.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-03 — Preflight confirmed PR #105's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s05-102-gm-catalog-authoring-mvp`. Corrected `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 (`ODY-S05-101`) from stale `In Review` to `Done`.
- 2026-09-03 — Read `SLICE-05_IMPLEMENTATION_BACKLOG.md`, `ADR-027` §4/4.1/12/20, `ContentCatalogRepositoryContracts.cs`, `SqliteContentCatalogRepository.cs`, `BoardMovementService.cs`/`BoardContracts.cs` in full.
- 2026-09-03 — Implemented `ContentCatalogAuthoringContracts.cs` and the repository extension; `dotnet build` passed on first full run.
- 2026-09-03 — Implemented `ContentCatalogAuthoringServiceTests.cs` (14 cases); first `dotnet test` run passed on first try.
- 2026-09-03 — `check-repository-policy.ps1` first run failed on the two new `ErrorCode`s missing from `ERROR_CODES.md`; added registry rows plus eleven `TC-CATALOG-013`–`023` catalog entries, second run passed.
- 2026-09-03 — Wrote this task's own contract and ExecPlan before running `verify-test-structure.ps1`, proactively avoiding the "task contract must exist before test-catalog can reference its `taskId`" failure this session already learned from `ODY-S05-101`/`ODY-S04-113a`/`ODY-S04-115a`.

## 7. Decisions

- 2026-09-03 — Decision: use ExecPlan, per `PLANS.md` §1 (new public Application contract, new persisted-state-producing repository method). Authority: `PLANS.md` §1, matching `ODY-S05-101`'s own reasoning for its sibling foundation task.
- 2026-09-03 — Decision: follow `BoardMovementService`'s exact structural precedent for the Application-service/repository split, with a distinct `ContentCatalogAuthoringFailures` class. Authority: this task's own ТЗ "Expected design" section, matched against the existing precedent.
- 2026-09-03 — Decision: extend `IContentCatalogRepository` with a purpose-built `CreateNextDraftVersionFromPublished` method rather than composing Get+Create at the service layer, so the whole read-check-write sequence stays inside one transaction with the command ledger. Authority: `ADR-002`'s single-transaction-per-command discipline; this task's own ТЗ explicitly allowing a minimal repository extension.
- 2026-09-03 — Decision: seed Published sources directly via SQL in tests (`MarkStatusDirectly`), since no `PublishDefinition` command exists yet. Authority: this task's own ТЗ explicit instruction, matching `ODY-S05-101`'s own established technique.

## 8. Discoveries and deviations

- No architectural question was found during implementation that `ADR-027`/`ODY-S05-101`'s own foundation do not already answer — no ADR was touched or extended.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 was stale (`In Review` despite PR #105 already merged) — corrected as this task's own explicit preflight step, per the ТЗ's own instruction, not left for a later task to notice.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: full suite passed (526/526), including 14 new `ContentCatalogAuthoringServiceTests` cases, no regression in any other test project.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed` (after adding the `ERROR_CODES.md`/test-catalog entries the first run's failure required).
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes`.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema migration exists to roll back; the new repository method is purely additive over `ODY-S05-101`'s own existing table.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-027`/`ODY-S05-101` do not already answer.

## 12. Outcome and follow-up

Draft PR: https://github.com/odyssey-services/Odyssey_VTT/pull/106. CI pending. Enables `ODY-S05-106`'s own future test-catalog fixtures to use a real authoring path instead of direct repository calls.
