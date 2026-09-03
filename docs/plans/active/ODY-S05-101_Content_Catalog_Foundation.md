# ODY-S05-101 — Content Catalog Foundation

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-101-content-catalog-foundation`
**Pull request:** <to be filled after `gh pr create`>
**Last updated:** 2026-09-03 UTC

## 1. Purpose and user-visible outcome

Implement the minimal Content Catalog foundation `ADR-027` (Accepted) requires before any GM authoring, publish/archive/delete workflow, validation, typed properties, or fixtures can be built: a generic `ContentDefinition` envelope, Draft/Published/Archived lifecycle status, version/revision fields, an exact-version reference shape, and minimal SQLite persistence — proving structurally and by direct test that catalog definitions never carry runtime item/equipment/effect state.

## 2. Task contract

- Goal: a compiling, tested `ContentDefinition` foundation in Domain/Application/Persistence, following `SqliteCharacterTemplateRepository`'s exact structural precedent for a single-table, non-event-sourced aggregate.
- Acceptance criteria: generic envelope + Draft/Published/Archived status + Version/Revision + exact-version reference + SQLite storage/read all implemented and covered by real tests; no authoring/publish/archive/delete/validation/typed-property business logic; no runtime item/inventory/equipment/effect type or table; `ADR-001`–`027` unmodified; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1`/`verify-test-structure.ps1` all pass.
- Requirement IDs: `ODY-S05-101`, `ADR-027` §4/4.1.
- In scope: `Odyssey.Domain.Content` (`ContentDefinitionId`/`ContentDefinitionStatus`/`ContentDefinitionOrigin`/`ContentDefinitionType`/`ContentDefinitionRef`), `IContentCatalogRepository`/`ContentDefinitionRecord`/`CreateDraftContentDefinitionRequest` (Application), `SqliteContentCatalogRepository` (Persistence), `PersistenceFailures`/`ErrorCodes` additions, `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json` additions, real tests, `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 status update, this task's own contract/plan.
- Out of scope: GM authoring (`ODY-S05-102`), publish/archive/delete lifecycle (`ODY-S05-103`), validation (`ODY-S05-104`), typed Weapon/Armor/Ammo/Ability/Effect properties (`ODY-S05-105`), test catalog fixtures (`ODY-S05-106`), any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect runtime, any Unity/UI code, any `ADR-001`–`027` content change.
- Required authorities: `ADR-027` §1/4/4.1/14/15/20, `11_Content_Block_System` §5/6, `ADR-001`/`002`/`003`/`011`/`012` (substrate, not reopened), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §5/§6, and `SqliteCharacterTemplateRepository.cs`/`ICharacterTemplateRepository`/`DomainIdentity.cs` as the binding structural precedent.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at PR #104's merge commit (`ODY-S05-002`, `ADR-027` acceptance + `SLICE-05_IMPLEMENTATION_BACKLOG.md`), independently verified via `git merge-base --is-ancestor`.
- `SqliteCharacterTemplateRepository`/`ICharacterTemplateRepository`/`DomainIdentity.cs` were read in full as the binding structural precedent: per-call short-lived `SqliteConnection`, `EnsureXTables` with `CREATE TABLE IF NOT EXISTS`, a manual `LastCommandId` idempotency column (no `DomainEvents`/journal participation — the closest existing sibling to this task's own non-event-sourced scope), a shared `SelectColumns`/`ReadXRecord` convention, and `Newtonsoft.Json.Linq` (`JArray`) as the already-approved low-level JSON API for structured list columns.
- `DomainIdentity.cs` confirmed two distinct existing ID patterns: the minted `Prefix + Uuid7.NewHex32` aggregate-identity pattern (`CharacterId`/`CharacterTemplateId`) and the lightweight human-authored string-key pattern (`SkillDefinitionId`/`AttributeDefinitionId`/`AbilityDefinitionId`, `SLICE-04`'s own fixture-only Ruleset keys with no backing table). `ContentDefinitionId` follows the former, since this is a genuine new aggregate root.
- No global/cross-campaign Ruleset-store mechanism exists anywhere in this codebase (`grep` across `Packages/com.odyssey.persistence/`) — every repository stores its own campaign-scoped content inside that campaign's own `campaign.db`.

Assumptions: none.

## 4. Proposed approach

- Domain: `ContentDefinitionId` (minted), `ContentDefinitionStatus`/`ContentDefinitionOrigin`/`ContentDefinitionType` enums, `ContentDefinitionRef` (exact `DefinitionId + Version`, `ToString`/`Parse` round-trip, no "latest" concept).
- Application: `IContentCatalogRepository` (`CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`GetContentDefinition`/`ListContentDefinitions`), `CreateDraftContentDefinitionRequest`/`ContentDefinitionRecord`, four `PersistenceFailures`/`ErrorCodes` entries (`ContentDefinitionNotFound`/`IoFailed`/`RevisionConflict`/`NotDraft`).
- Persistence: `SqliteContentCatalogRepository` — one `ContentDefinition` table (no `CampaignId` column, physically per-campaign, logically Ruleset-scoped), `LastCommandId` idempotency, `UpdateDraftContentDefinition` refusing any non-Draft row (`ADR-027` §4.1 Published-immutability, enforced at the foundation level).
- Tests: `ContentDefinitionRefTests` (pure Domain: round-trip, equality, malformed-input rejection) and `SqliteContentCatalogRepositoryTests` (real SQLite: create/read/list, revision increment/conflict/idempotency, Published/Archived immutability via direct SQL seeding, exact-reference round-trip, and direct schema/table-list proof that no runtime item/inventory/equipment/effect state exists).
- Registry: four new `ErrorCode`s registered in both `ErrorCodes.cs` and `ERROR_CODES.md`, referencing nine new `TC-CATALOG-001`–`009` entries added to `test-catalog.json`.
- Backlog: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 (`ODY-S05-101`) marked with PR link/evidence.

No Unity/UI code, no authoring/publish/archive/delete/validation/typed-property business logic, no `ADR-001`–`027` content change.

## 5. Milestones

### M1 — Domain/Application/Persistence foundation

- [x] `Odyssey.Domain.Content` (`ContentDefinitionId`, enums, `ContentDefinitionRef`).
- [x] `IContentCatalogRepository`/`ContentDefinitionRecord`/`CreateDraftContentDefinitionRequest`.
- [x] `PersistenceFailures`/`ErrorCodes` ContentDefinition entries.
- [x] `SqliteContentCatalogRepository` implementation.

### M2 — Tests and registry

- [x] `ContentDefinitionRefTests` (Domain, 12 cases).
- [x] `SqliteContentCatalogRepositoryTests` (Persistence, real SQLite, 17 cases).
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [x] `dotnet build`/`dotnet test` full suite green, no regression.

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 status update.
- [ ] Commit, push, and open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-03 — Preflight confirmed PR #104's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s05-101-content-catalog-foundation`.
- 2026-09-03 — Read `SLICE-05_IMPLEMENTATION_BACKLOG.md`, `ADR-027` §1/4/4.1/14/15/20, `11_Content_Block_System` §5/6, `SqliteCharacterTemplateRepository.cs`/`ICharacterTemplateRepository.cs`, `DomainIdentity.cs` in full.
- 2026-09-03 — Implemented Domain/Application/Persistence foundation; `dotnet build` passed on first full run.
- 2026-09-03 — Implemented both test files (12 Domain + 17 Persistence cases); first `dotnet test` run passed on first try after fixing an NUnit `Assert.Throws` lambda-overload ambiguity (`Action` cast required).
- 2026-09-03 — `check-repository-policy.ps1` first run failed on the four new `ErrorCode`s missing from `ERROR_CODES.md`; added registry rows plus nine `TC-CATALOG-*` catalog entries, second run passed.
- 2026-09-03 — `verify-test-structure.ps1` first run failed: `TC-CATALOG-001` referenced task contract `ODY-S05-101`, which did not exist yet; wrote this task's own contract, second run passed.

## 7. Decisions

- 2026-09-03 — Decision: use ExecPlan, per `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1 (new public contract, persistence schema, authoritative lifecycle-adjacent state). Authority: `PLANS.md` §1, backlog row 1.
- 2026-09-03 — Decision: `ContentDefinitionId` uses the minted `Prefix + Uuid7.NewHex32` pattern (`CharacterId`/`CharacterTemplateId`'s own convention), not the lightweight string-key pattern `SkillDefinitionId`/`AttributeDefinitionId` use. Authority: `DomainIdentity.cs`'s own two established patterns, matched to this task's actual aggregate shape (a genuine new aggregate root with its own persisted row, not a fixture-only Ruleset key).
- 2026-09-03 — Decision: no `DomainEvents`/journal participation for `ContentDefinition`, following `CharacterTemplate`'s own precedent for a non-event-sourced sibling aggregate. Authority: `SqliteCharacterTemplateRepository`'s own doc-comment reasoning, re-confirmed against `ADR-027`/`11_Content_Block_System` finding no contrary requirement.
- 2026-09-03 — Decision: `ContentDefinition` table has no `CampaignId` column and is physically stored per-campaign (no global Ruleset-store mechanism exists yet) — recorded as an honest known limitation, not silently assumed. Authority: `grep`-verified absence of any such mechanism, applied honestly per this codebase's own `ADR-013`/`ADR-026` "no X exists today" precedent.
- 2026-09-03 — Decision: `UpdateDraftContentDefinition` is a bare, permission-free repository primitive proving Revision/Published-immutability only — not the real GM authoring command (`ODY-S05-102`'s own job). Authority: this task's own ТЗ instruction on the minimal update operation.

## 8. Discoveries and deviations

- `DomainIdentity.cs` already established two genuinely different ID-type conventions for what looks superficially like the same concept ("DefinitionId") — the minted-aggregate-identity pattern and the lightweight fixture-string-key pattern. Confirming which one applied here required reading `SkillDefinitionId`'s own doc comment directly, not assuming from the name alone.
- No architectural question was found during implementation that `ADR-027`/`11_Content_Block_System` do not already answer — no ADR was touched or extended.
- `verify-test-structure.ps1`'s task-contract lookup requires the contract file to exist before any test-catalog entry can reference that `taskId` — the same lesson `ODY-S04-113a` and `ODY-S04-115a` already taught this session, applied proactively here rather than discovered via a CI failure.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: full suite passed, including 12 new Domain `ContentDefinitionRefTests` cases and 17 new Persistence `SqliteContentCatalogRepositoryTests` cases, no regression in any other test project.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed` (after adding the `ERROR_CODES.md`/test-catalog entries the first run's failure required).
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` (after adding this task's own contract file the first run's failure required).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema migration exists to roll back beyond `CREATE TABLE IF NOT EXISTS`, itself idempotent and additive; no existing table or column is altered.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-027`/`11_Content_Block_System` do not already answer.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S05-102`/`104`/`105`.
