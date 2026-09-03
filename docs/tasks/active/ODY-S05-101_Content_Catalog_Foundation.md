# ODY-S05-101 — Content Catalog Foundation

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-101-content-catalog-foundation`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/105
**ExecPlan:** `docs/plans/active/ODY-S05-101_Content_Catalog_Foundation.md`
**Created:** 2026-09-03
**Last updated:** 2026-09-03 (idempotency-fix amendment) UTC

## 1. Goal

Implement the minimal Content Catalog foundation `SLICE-05_IMPLEMENTATION_BACKLOG.md` reserves as `ODY-S05-101`: a generic `ContentDefinition` envelope, Draft/Published/Archived lifecycle status, version/revision fields, an exact-version reference shape for future runtime pinning, and minimal SQLite persistence/read operations — proving catalog definitions are stored completely separately from any runtime item/equipment/effect state. No authoring business rules, no publish/archive/delete workflow, no per-type validation, and no typed Weapon/Armor/Ammo/Ability/Effect properties — all reserved for `ODY-S05-102`–`106`.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-027` (Accepted) fixes the Content Catalog/runtime boundary architecturally, but no storage/contract implementation exists yet for the generic `ContentDefinition` envelope every later `SLICE-05` catalog task (authoring, lifecycle, validation, typed properties, fixtures) needs to build on.
- Value or risk reduction: gives `ODY-S05-102`–`106` a real, tested foundation to build on rather than each independently inventing the envelope shape; proves early (via direct schema/table-existence tests) that no runtime item/inventory/equipment/effect state accidentally enters the catalog layer, the central invariant `ADR-027` section 4 requires.
- Blocking or enabling relationship: unblocks `ODY-S05-102` (GM Catalog Authoring MVP), `ODY-S05-104` (Catalog Validation MVP, depends on `105`'s types built on this envelope), and `ODY-S05-105` (Base Definition Types, extends this envelope).

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially the `ODY-S05-101` row (section 5) and task-boundary paragraph (section 6).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 1, 4, 4.1, 14, 15, 20 (full read).
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md`, sections 5 and 6 (full read) — the generic `ContentDefinition` envelope shape and Draft/Published/Archived lifecycle vocabulary this task implements structurally.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` — Domain/Application/Persistence boundaries.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` — command/idempotency vocabulary (`CommandId` replay).
- `docs/adr/ADR-003_Serialization_Strategy_v1.1.md` — explicit versioned contracts, no direct Domain serialization.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md` — campaign storage boundary (no separate global Ruleset store exists).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` — substrate; this task's own table deliberately does not participate in `DomainEvents` (no ADR/product requirement mandates it for catalog definitions).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterTemplateRepository.cs` and `Packages/com.odyssey.application/Runtime/Persistence/CharacterTemplateRepositoryContracts.cs` (full read) — the binding structural precedent: a single-table aggregate with a manual `LastCommandId` idempotency column and no `DomainEvents` participation, the closest existing sibling to this task's own scope.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (full read) — the canonical `Prefix + Uuid7.NewHex32` aggregate-identity pattern this task's own `ContentDefinitionId` follows.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-101`, `ADR-027` section 4/4.1.
- Existing test IDs: None reused.
- New test IDs introduced: `TC-CATALOG-001`–`012`.

### Task-safe private context

- Approved summary / references: `ADR-027`'s own already-accepted content and `11_Content_Block_System`'s already-public envelope/lifecycle vocabulary are cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git merge-base --is-ancestor` confirmed PR #104 (`ODY-S05-002`, `ADR-027` acceptance + `SLICE-05_IMPLEMENTATION_BACKLOG.md` creation) is a real ancestor of `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` section 6's own `ODY-S05-101` boundary was confirmed by direct `Read`: "only the generic `ContentDefinition` envelope and lifecycle state machine" — no authoring, publish/archive/delete, validation, or typed properties.
- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` was read in full: `SkillDefinitionId`/`AttributeDefinitionId`/`AbilityDefinitionId` (etc.) use a lightweight, human-authored string-key pattern with no backing persisted table (`ODY-S04-106`/`108`'s own fixture-only Ruleset keys) — deliberately a different pattern from `CharacterId`/`CharacterTemplateId`'s own minted `Prefix + Uuid7.NewHex32` aggregate-identity pattern. `ContentDefinitionId` follows the latter, since this ADR-027 Content Catalog is a genuine new aggregate root with its own persisted row and lifecycle, not a fixture-only Ruleset key.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterTemplateRepository.cs` was read in full and confirmed as the closest existing sibling: a single-table aggregate with manual `LastCommandId` idempotency and no `DomainEvents` participation ("no ADR/product requirement makes `CharacterTemplate` participate in `DomainEvents`/history"). No ADR or product document requires `ContentDefinition` to participate in `DomainEvents` either — confirmed by direct re-read of `ADR-027` section 4 and `11_Content_Block_System` section 6, neither of which mentions an event-sourced catalog history requirement.
- No existing global/cross-campaign Ruleset-store mechanism exists anywhere in this codebase (confirmed by `grep` across `Packages/com.odyssey.persistence/`) — every repository, including `CharacterTemplate`'s own `Campaign`-scope option, stores its own campaign-scoped content inside that campaign's own `campaign.db`. This task's own `ContentDefinition` table follows the same physical pattern, recorded explicitly as a decision (section 18) rather than silently assumed.

### Assumptions

- None. Every fact above was directly observed via `Read`/`grep`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs` (new): `ContentDefinitionId` (minted identity), `ContentDefinitionStatus` (Draft/Published/Archived), `ContentDefinitionOrigin` (RulesetPackage/Campaign — only `RulesetPackage` ever produced), `ContentDefinitionType` (mechanical + structural vocabulary from `11_Content_Block_System` sections 5.1/5.2, identity/discriminator only, no typed properties), `ContentDefinitionRef` (exact `DefinitionId + Version` reference, round-trippable, no "latest" concept).
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` (new): `IContentCatalogRepository` (`CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`GetContentDefinition`/`ListContentDefinitions`), `CreateDraftContentDefinitionRequest`, `ContentDefinitionRecord`.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` (new): SQLite implementation, one `ContentDefinition` table, a durable `ContentDefinitionCommandLedger` table (`CommandId` primary key → `ContentDefinitionId`) as the sole idempotency source of truth — **not** a mutable `LastCommandId` column on the `ContentDefinition` row itself, since a single such column would be overwritten by every later create/update on the same row and silently stop recognizing an older command's replay once a newer one has touched that row (found and fixed as an amendment during review — see section 18). No `DomainEvents` participation.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs`: four new `PersistenceFailures` entries (`ContentDefinitionNotFound`/`ContentDefinitionIoFailed`/`ContentDefinitionRevisionConflict`/`ContentDefinitionNotDraft`).
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs`: four new `ErrorCode` entries.
- `docs/errors/ERROR_CODES.md`: four new registry rows.
- `Tests/Metadata/test-catalog.json`: twelve new `TC-CATALOG-001`–`012` entries.
- `DotNet/Tests/Odyssey.Tests.Domain/Content/ContentDefinitionRefTests.cs` (new).
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteContentCatalogRepositoryTests.cs` (new).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 1 (`ODY-S05-101`) status update with PR link/evidence.
- This task contract and its ExecPlan.

### Out of scope

- MainGM authoring business rules, permission checks, or a "create next Draft version from Published" operation (`ODY-S05-102`). `UpdateDraftContentDefinition` here is a bare, permission-free repository primitive proving the Revision mechanism only.
- `PublishDefinition`/`ArchiveDefinition`/physical delete rules/Archived-list-specific query (`ODY-S05-103`). Published immutability is proven only as `UpdateDraftContentDefinition` refusing a non-Draft row.
- Per-type usability/applicability validation, missing-reference checks, `ContentBlock` cycle checks, Ruleset/version compatibility checks (`ODY-S05-104`).
- Typed properties for `WeaponDefinition`/`ArmorDefinition`/`AmmoDefinition`/`AbilityDefinition`/`EffectDefinition`/`Resource`/`BodyPart` (`ODY-S05-105`).
- Minimal test catalog fixtures proving weapon/armor/ammo/ability/effect/resource/body-part references work together (`ODY-S05-106`).
- Any runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`/item-sourced ability/`ItemDefinition` migration/attack pipeline implementation.
- Any Unity UI or asset.
- Campaign-specific custom catalog or per-campaign overrides.
- `.odcontent` import/export.
- Marketplace/content-package implementation.
- Any edit to `ADR-001`–`027`'s own accepted content.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
DotNet/Tests/Odyssey.Tests.Domain/Content/ContentDefinitionRefTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteContentCatalogRepositoryTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-101_Content_Catalog_Foundation.md
docs/plans/active/ODY-S05-101_Content_Catalog_Foundation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*
Packages/com.odyssey.domain/Runtime/Character/**
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns pure identity/lifecycle-value invariants only (`ContentDefinitionId`/`ContentDefinitionStatus`/`ContentDefinitionOrigin`/`ContentDefinitionType`/`ContentDefinitionRef`); `Odyssey.Application` owns the repository contract/DTOs; `Odyssey.Persistence` owns SQLite storage. No Unity dependency anywhere in this task's own code (`ADR-001`).
- Authoritative-state and transaction boundary: `CreateDraftContentDefinition`/`UpdateDraftContentDefinition` each commit in one transaction with `CommandId`-based replay, mirroring `SqliteCharacterTemplateRepository`'s exact convention (`ADR-002`).
- Serialization / compatibility boundary: `ContentDefinitionRecord` is an explicit Application-layer read shape, never a direct Domain serialization; `PropertiesJson`/`RulesetCompatibilityJson`/`TagsJson`/`DependencyRefsJson` are opaque JSON columns, matching `ADR-003`'s explicit-contract rule.
- Time / RNG rule: `ContentDefinitionId.NewId`/timestamps use the injected `IWallClock`, no direct `DateTime.UtcNow`.
- Unity / thread / lifetime rule: not applicable — pure .NET code, no Unity API used.
- Dependency / licensing rule: no new dependency; reuses `Microsoft.Data.Sqlite`/`Newtonsoft.Json` already referenced by sibling repositories.
- Security / privacy / redaction rule: not applicable — no new trust boundary; `CreatedByUserId` is stored the same way every other aggregate's audit `UserId` field already is.
- Performance or platform constraint: not applicable.
- Other: no `DomainEvents`/append-only journal participation for this table — recorded explicitly as a decision (section 18), matching `CharacterTemplate`'s own established precedent for a non-event-sourced sibling aggregate.

## 7. Expected behavior

### Scenario 1 — a generic ContentDefinition can be created, read, and listed

**Given** a fresh campaign
**When** `CreateDraftContentDefinition` is called
**Then** the resulting record has `Status=Draft`, `Version=0` (never published), `Revision=1`, `Origin=RulesetPackage`, and can be read back identically via `GetContentDefinition`/`ListContentDefinitions`, including against a brand-new repository instance.

### Scenario 2 — the Revision/optimistic-concurrency mechanism works

**Given** an existing Draft definition
**When** `UpdateDraftContentDefinition` is called with the correct `expectedRevision`
**Then** the update succeeds and `Revision` increments by exactly 1; a stale `expectedRevision` is rejected with no state change; a duplicate `CommandId` does not increment `Revision` twice.

### Scenario 3 — Published immutability is enforced at the foundation level

**Given** a definition whose `Status` is `Published` or `Archived` (seeded directly at the SQL level, since no publish command exists yet)
**When** `UpdateDraftContentDefinition` is called against it
**Then** it is rejected with `PersistenceContentDefinitionNotDraft` and no state change.

### Scenario 4 — the exact-version reference shape round-trips

**Given** a `ContentDefinitionRef` (an exact `DefinitionId + Version` pair, never a "latest" pointer)
**When** it is stored inside a definition's own `DependencyRefs` and read back, or serialized/parsed directly
**Then** it round-trips exactly, including distinguishing two references to the same `DefinitionId` at different `Version`s.

### Scenario 5 — no runtime item/equipment/effect state exists anywhere in this task's own diff

**Given** this task's complete diff
**When** the `ContentDefinition` table schema and the campaign database's full table list are inspected directly
**Then** no column or table references `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` in any form.

### Required invariants

- No `ODY-S05-102`–`106` behavior (authoring business rules, publish/archive/delete, validation, typed properties, fixtures) is implemented.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type, table, or column is introduced.
- `ADR-001`–`027` are unmodified.
- `Version` is 0 for every definition this task's own code ever writes (never published by this task).

## 8. Deliverables

- Production code: `ContentCatalog.cs` (Domain), `ContentCatalogRepositoryContracts.cs` (Application), `SqliteContentCatalogRepository.cs` (Persistence), four `PersistenceFailures`/`ErrorCodes` entries.
- Tests: `ContentDefinitionRefTests.cs` (12 cases), `SqliteContentCatalogRepositoryTests.cs` (20 cases) — `TC-CATALOG-001`–`012`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 1 status), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None — additive `CREATE TABLE IF NOT EXISTS` only.

## 9. Acceptance criteria

1. `ContentDefinition` foundation exists as generic catalog definition state (`ContentCatalog.cs` + `ContentCatalogRepositoryContracts.cs` + `SqliteContentCatalogRepository.cs`).
2. Status values `Draft`/`Published`/`Archived` exist and persist (`TC-CATALOG-001`).
3. Definition `Version` and `Revision` fields exist and persist (`TC-CATALOG-001`/`005`).
4. Exact-version `ContentDefinitionRef` reference shape exists and round-trips (`TC-CATALOG-007`/`009`).
5. Storage is base/Ruleset-catalog scoped only — every definition this task's code produces has `Origin=RulesetPackage`; no campaign-specific override mechanism exists (`TC-CATALOG-001`).
6. No runtime item/equipment/effect state is created — no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table anywhere in this task's diff (`TC-CATALOG-008`).
7. Published immutability is represented/enforced at the foundation level without implementing the full publish lifecycle (`TC-CATALOG-006`).
8. Tests prove create/read/list foundation persistence paths (`TC-CATALOG-001`–`004`).
9. Tests prove exact-version reference round-trip (`TC-CATALOG-007`/`009`).
10. Tests and direct diff review prove no runtime item/inventory/equipment implementation slipped in (`TC-CATALOG-008`; `git diff --name-status` review).
11. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-101` `In Review`/`Done` per repository convention and records the PR link/evidence.
12. No accepted ADR content (`ADR-001`–`027`) is edited.
13. Required validation commands (section 10) pass.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-001` | .NET / NUnit (Persistence) | `CreateDraftContentDefinition` produces Draft/Version=0/Revision=1/Origin=RulesetPackage | Pass |
| `TC-CATALOG-002` | .NET / NUnit (Persistence) | `GetContentDefinition` round-trips, including across a fresh repository instance; unknown-ID NotFound | Pass |
| `TC-CATALOG-003` | .NET / NUnit (Persistence) | Duplicate `CommandId` for create does not duplicate the row | Pass |
| `TC-CATALOG-004` | .NET / NUnit (Persistence) | `ListContentDefinitions` with/without status filter | Pass |
| `TC-CATALOG-005` | .NET / NUnit (Persistence) | `UpdateDraftContentDefinition` Revision increment/stale-revision rejection/duplicate-CommandId idempotency/unknown-ID NotFound | Pass |
| `TC-CATALOG-006` | .NET / NUnit (Persistence) | Published/Archived immutability enforced by `UpdateDraftContentDefinition` | Pass |
| `TC-CATALOG-007` | .NET / NUnit (Persistence) | `ContentDefinitionRef`/string-list round-trip through storage | Pass |
| `TC-CATALOG-008` | .NET / NUnit (Persistence) | No runtime item/inventory/equipment/effect column or table exists | Pass |
| `TC-CATALOG-009` | .NET / NUnit (Domain) | `ContentDefinitionId`/`ContentDefinitionRef` construction/round-trip/equality/malformed-input rejection | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, and no `ADR-001`–`027` file, is touched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable — no Unity/UI code.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the same fixture convention every sibling persistence test already uses.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation — no Unity code touched.
- Any test of `ODY-S05-102`–`106`'s own future behavior — none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — purely additive `CREATE TABLE IF NOT EXISTS`, no existing table/column altered.
- Version fields affected: None (application/schema/protocol/Ruleset version unchanged).
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable — new table only.
- Rollback method: Revert the branch; the new table simply stops being created for future campaigns (existing test campaigns are ephemeral temp directories).
- Data-loss risk and protection: None — additive only.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: `CreatedByUserId` (already-established `UserId` audit-field pattern, no new data class).
- Trust boundaries: Not applicable — no new trust boundary introduced.
- Authorization / audience checks: Not applicable — this foundation task has no permission-gated command; `ODY-S05-102`/`103` own real MainGM/AssistantGM enforcement per `ADR-027` section 12.
- Redaction requirements: Not applicable — no networking/redaction surface touched.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Basic field-shape checks only (`Name` length, non-null); real usability/applicability validation is `ODY-S05-104`'s own job.
- Security tests: Not applicable — no new security-relevant behavior.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s own row for this task designates `ExecPlan`, and checked fresh against `PLANS.md` §1: this task introduces a new public contract (`IContentCatalogRepository`), new persistence schema (`ContentDefinition` table), and new authoritative lifecycle-adjacent state (Draft/Published/Archived, Revision) — all `ExecPlan` triggers `PLANS.md` §1 already names.
- ExecPlan path: `docs/plans/active/ODY-S05-101_Content_Catalog_Foundation.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ADR-027` is `Accepted` and `SLICE-05_IMPLEMENTATION_BACKLOG.md` exists (both confirmed in section 4). Unblocks `ODY-S05-102`/`104`/`105`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (row 1 status), this task contract, its ExecPlan.
- Documents that must not change: `ADR-001`–`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*`/`ODY-S05-002_*`.
- Application version change: No.
- Schema / format / contract / protocol / Ruleset version change: New `ContentDefinition` table (additive); no existing schema/format/contract/protocol/Ruleset version changed.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that authoring/publish/archive/delete/validation/typed-definitions/runtime/attack are deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` — new.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — four new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — four new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` — new.
- `DotNet/Tests/Odyssey.Tests.Domain/Content/ContentDefinitionRefTests.cs` — new, 12 tests.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteContentCatalogRepositoryTests.cs` — new, 20 tests (17 original + 3 added by the idempotency-fix amendment).
- `docs/errors/ERROR_CODES.md` — four new rows.
- `Tests/Metadata/test-catalog.json` — twelve new `TC-CATALOG-001`–`012` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row 1 status update.
- This task contract and its ExecPlan.
- **Amendment (idempotency fix, post-initial-review):** `SqliteContentCatalogRepository.cs` — replaced the mutable `LastCommandId` column on the `ContentDefinition` row with a durable `ContentDefinitionCommandLedger` table (`CommandId` primary key → `ContentDefinitionId`), written in the same transaction as every create/update. See section 18's own decision record.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (512/512) including 12 new Domain + 20 new Persistence tests, no regression |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `ContentCatalog.cs`/`ContentCatalogRepositoryContracts.cs`/`SqliteContentCatalogRepository.cs`. |
| AC-2 | Pass | `TC-CATALOG-001` — Status persists as Draft; `TC-CATALOG-006` — Published/Archived enforced. |
| AC-3 | Pass | `TC-CATALOG-001`/`005` — Version=0 at creation, Revision increments via `UpdateDraftContentDefinition`. |
| AC-4 | Pass | `TC-CATALOG-007`/`009` — `ContentDefinitionRef` round-trips through storage and `ToString`/`Parse`. |
| AC-5 | Pass | `TC-CATALOG-001` asserts `Origin=RulesetPackage`; no campaign-override code path exists. |
| AC-6 | Pass | `TC-CATALOG-008` — direct schema/table-list inspection finds no runtime item/inventory/equipment/effect reference. |
| AC-7 | Pass | `TC-CATALOG-006` — `UpdateDraftContentDefinition` rejects non-Draft rows. |
| AC-8 | Pass | `TC-CATALOG-001`–`004`. |
| AC-9 | Pass | `TC-CATALOG-007`/`009`. |
| AC-10 | Pass | `TC-CATALOG-008` plus `git diff --name-status` review (validation-results section). |
| AC-11 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 updated. |
| AC-12 | Pass | `git status --porcelain` confirms no `ADR-001`–`027` file touched. |
| AC-14 (amendment) | Pass | Idempotency is durable per-command via `ContentDefinitionCommandLedger`, not a mutable per-row `LastCommandId` column — `TC-CATALOG-010`/`011` prove an older command's replay is still recognized after a later command has touched the same row (both for create and for update, including no false stale-revision conflict); `TC-CATALOG-012` proves `CommandId` uniqueness is enforced at the database level via the ledger's own primary key. |
| AC-13 | Pass | Validation-results table above. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table.

### Known limitations

- No authoring/publish/archive/delete/validation/typed-property/fixture behavior exists yet — each is its own reserved future task (`ODY-S05-102`–`106`).
- The `ContentDefinition` table is physically stored per-campaign (inside `campaign.db`), not in a true cross-campaign shared Ruleset store — recorded honestly as a known limitation (section 4/18), since no such shared-store mechanism exists anywhere in this codebase yet. Revisiting this is a future, separately-scoped decision if the product owner ever requests true cross-campaign catalog sharing.

### Follow-up tasks

- `ODY-S05-102` — GM Catalog Authoring MVP (depends on this task).
- `ODY-S05-104` — Catalog Validation MVP (depends on this task and `105`).
- `ODY-S05-105` — Base Definition Types (depends on this task).

### Self-review summary

- Scope review: diff limited to the twelve files in section 5's Allowed paths; no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`–`027` file, no Unity file touched.
- Architecture review: `Odyssey.Domain`/`Odyssey.Application`/`Odyssey.Persistence` boundaries followed exactly (`ADR-001`); `SqliteCharacterTemplateRepository`'s own structural precedent reused rather than inventing a new pattern.
- Test review: 32 new tests (12 Domain + 20 Persistence), full suite re-run green, no regression.
- Security/privacy review: no new trust boundary, no redaction surface, no permission-gated command in this foundation-only scope.
- Documentation/version review: `ERROR_CODES.md`/test-catalog updated and cross-checked by `check-repository-policy.ps1`/`verify-test-structure.ps1`; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-03 — Decision: `ContentDefinitionId` uses the canonical `Prefix + Uuid7.NewHex32` minted-identity pattern (`CharacterId`/`CharacterTemplateId`'s own convention), not the lightweight human-authored string-key pattern `SkillDefinitionId`/`AttributeDefinitionId`/`AbilityDefinitionId` use — this ADR-027 Content Catalog is a genuine new aggregate root with its own persisted row and lifecycle, unlike those `SLICE-04` fixture-only Ruleset keys with no backing table. Authority: `DomainIdentity.cs`'s own two established patterns, matched to this task's own actual aggregate shape.
- 2026-09-03 — Decision: `ContentDefinition` does not participate in `DomainEvents`/the append-only journal — follows `CharacterTemplate`'s own established precedent for a non-event-sourced sibling aggregate, since no ADR or product document requires catalog definitions to have event-sourced history. Authority: `SqliteCharacterTemplateRepository`'s own explicit doc-comment reasoning, re-confirmed against `ADR-027`/`11_Content_Block_System` finding no contrary requirement.
- 2026-09-03 — Decision: the `ContentDefinition` table is stored inside each campaign's own `campaign.db`, with no `CampaignId` column — logically Ruleset-scoped (no code path distinguishes one campaign's catalog from another's structurally) but physically per-campaign, since no global/cross-campaign Ruleset-store mechanism exists anywhere in this codebase yet. Recorded explicitly as a known limitation (section 17) rather than silently assumed; a future, separately-scoped decision would be needed for true cross-campaign sharing. Authority: this task's own explicit `grep`-verified finding that no such mechanism exists, applied honestly rather than papered over (mirrors `ADR-013`/`ADR-026`'s own "no X exists today" honesty pattern already established in this codebase).
- 2026-09-03 — Decision: `UpdateDraftContentDefinition` is included as a bare, permission-free repository primitive (not a real authoring command) specifically to prove the Revision/optimistic-concurrency mechanism and Published-immutability guard at the foundation level, per this task's own explicit acceptance criteria 3/7. It takes no actor/permission parameter at all — `ODY-S05-102` will wrap real MainGM-gated business commands around it (or replace it) as needed. Authority: this task's own ТЗ §"In scope" instruction ("Tests proving: ... revision persists and changes only through this task's own minimal foundation operations, if any update operation is needed").
- 2026-09-03 — **Amendment, post-initial-review — decision: replaced `LastCommandId` idempotency with a durable `ContentDefinitionCommandLedger` table.** Product-owner review found a real idempotency defect: `LastCommandId` was a single mutable column on the `ContentDefinition` row itself, overwritten by every later create/update on that row. Once a newer command had written to a row, replaying an *older* command's own `CommandId` stopped being recognized as a replay — `CreateDraftContentDefinition` would mint a genuine duplicate row for a resent create, and `UpdateDraftContentDefinition` would either re-apply an already-applied mutation or fail with a false `PersistenceContentDefinitionRevisionConflict`. Fixed by introducing `ContentDefinitionCommandLedger` (`CommandId` primary key → `ContentDefinitionId`), written in the same transaction as the create/update it accompanies; `TryFindByCommandId` now looks up this ledger first, for both operations, and returns the definition's own *current* state on a hit rather than ever re-running the original mutation. `LastCommandId` was removed entirely from the `ContentDefinition` table — no dual source of idempotency truth remains. Three new tests (`TC-CATALOG-010`–`012`) prove: a create-CommandId replay after a later update still resolves to the same single row; an update-CommandId replay after a later update returns the current (not the older) state with no stale-revision conflict; and the ledger's own `CommandId` column is the table's real SQLite primary key. Scope was not widened — no inventory/item/equipment/effect/runtime schema, no `ADR-027` architecture-section edit, no authoring-permission logic was added; this stays a pure persistence-correctness fix inside the same Foundation boundary. Authority: product-owner review finding, ТЗ "amendment к PR #105 / ODY-S05-101".

### Approved task changes

- 2026-09-03 — Amendment approved: replace `LastCommandId`-column idempotency with a durable `ContentDefinitionCommandLedger` table, per product-owner review. Scope, allowed paths, and acceptance criteria unchanged except for the addition of the idempotency-fix acceptance criterion (AC-14) and the corresponding three new tests (`TC-CATALOG-010`–`012`).
