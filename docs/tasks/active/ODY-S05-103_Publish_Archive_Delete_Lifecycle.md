# ODY-S05-103 — Publish/Archive/Delete Lifecycle

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-103-publish-archive-delete-lifecycle`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/110
**ExecPlan:** `docs/plans/active/ODY-S05-103_Publish_Archive_Delete_Lifecycle.md`
**Created:** 2026-09-04
**Last updated:** 2026-09-04 UTC (amended twice: DeleteDraftDefinition idempotency fix; shared-ledger CommandId reuse now rejected; final pre-merge cleanup: schema-guard test renamed/wording updated for `ContentDefinitionDeleteLedger`)

## 1. Goal

Implement the Content Catalog's publish/archive/physical-delete lifecycle commands: `PublishDefinition` (a valid Draft becomes an immutable Published version, gated by `ODY-S05-104`'s own server-side validation), `ArchiveDefinition` (a Published definition is archived, never physically deleted, and stays fully loadable), physical delete (restricted to unused Drafts only), and a dedicated Archived-list query/data shape for MainGM. No UI, no runtime Inventory/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`, no attack pipeline, no balanced content fixtures.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-101`/`102` gave the catalog storage and Draft authoring; `ODY-S05-104` gave it real usability validation; `ODY-S05-105` gave it typed shapes. Nothing yet lets a Draft actually become a real, immutable, referenceable Published definition, or lets MainGM clean up unused Drafts or retire Published content without losing it.
- Value or risk reduction: closes the Content Catalog MVP block itself (the last of `101`-`105` before `106`'s own end-to-end fixture proof); enforces `ADR-027` section 4.1's archive/physical-delete invariants at the code level for the first time.
- Blocking or enabling relationship: unblocks `ODY-S05-106` (needs a real publish/archive path to prove end-to-end); depends on `ODY-S05-101` (lifecycle state to transition), `ODY-S05-102` (a Draft must exist to publish), and `ODY-S05-104` (publish is gated by validation).

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, section 6's `ODY-S05-103` task-boundary paragraph and section 3.5 (Archived list is data/query only).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 4.1, 9, 12, 20 (full read).
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md`, section 6 (Draft/Published/Archived lifecycle, `Revision`/`Version` semantics, publication/deletion rules) (full read).
- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs` (full read) -- `ContentDefinitionStatus`/`ContentDefinitionRef`'s exact-version shape this task's delete-dependency scan and publish/archive transitions must not redefine.
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` (full read) -- `IContentCatalogRepository`/`ContentDefinitionRecord`, extended by this task.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs` (full read) -- `ContentCatalogAuthoringService`'s exact structural precedent this task's own lifecycle service follows, and its `NotMainGm` failure this task reuses directly.
- `Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs` (full read) -- `CatalogValidationService.ValidateDraftForPublish`, the real server-side publish gate this task integrates, never re-implements.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` (full read) -- the foundation implementation this task extends, including its `ContentDefinitionCommandLedger` idempotency mechanism this task's own new methods reuse (and, for delete, adapt).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-103`, `ADR-027` section 4.1/9/12/20.
- Existing test IDs: `TC-CATALOG-001`-`077` (re-verified unmodified).
- New test IDs introduced: `TC-CATALOG-078`-`097`.

### Task-safe private context

- Approved summary / references: `ADR-027`/`11_Content_Block_System`'s own already-accepted/published content is cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed PR #105 (`ODY-S05-101`), PR #106 (`ODY-S05-102`), PR #107 (`ODY-S05-105`), PR #108, and follow-up PR #109 (`ODY-S05-104`, including both of its own amendments) are all already merged into `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 4 (`ODY-S05-104`) still described PR #109 as "In Review" despite it already being merged -- corrected to `Done` (both PR #108 and #109 links) as this task's own first preflight step.
- `IContentCatalogRepository` (read in full) had no `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` method at all before this task; `UpdateDraftContentDefinition` already enforces Published-immutability (rejects non-Draft), which this task's own `PublishDefinition` must not weaken.
- `11_Content_Block_System` section 6.2 (read in full): "`Version` меняется только при публикации новой механической версии" -- confirmed against `ODY-S05-102`'s own `CreateNextDraftVersionFromPublished`, which mints an entirely new `ContentDefinitionId` (not an in-place version bump) for a "next version." This means, in this codebase's own established model, a single `ContentDefinitionId` row is published at most once and its own `Version` therefore only ever needs to become `1` -- there is no in-place "publish version 2 of the same row" operation anywhere in this codebase to design around.
- A `ContentDefinitionRef` (`ContentCatalog.cs`, unmodified) requires `Version >= 1` at construction -- confirmed this means no reference (typed or the generic `DependencyRefs` envelope field) can ever legitimately target a genuine Draft (`Version == 0`, true of every Draft by construction) through the public API today. This directly affects the physical-delete dependency check's own real-world reachability (see section 18's recorded decision).
- No runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table exists anywhere in this codebase (confirmed by the same reflection/schema scan convention `ODY-S05-101`-`105` each already used) -- `ADR-027` section 4.1 rule 5's runtime-reference check for physical delete has nothing to check against yet; this is recorded as an explicit, not-yet-implementable future extension boundary (section 18), not a skipped check.

### Assumptions

- None. Every fact above was directly observed via `Read`/`Grep`/`git`/`dotnet build`/`dotnet test` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs`: extend `IContentCatalogRepository` with `PublishDefinition`, `ArchiveDefinition`, `DeleteDraftDefinition`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs`: one new `PersistenceFailures.ContentDefinitionReferenced` entry; doc-comment update to `ContentDefinitionNotPublished` noting its reuse by Archive.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs`: two new `ErrorCode` entries (`PersistenceContentDefinitionReferenced`, `ContentCatalogPublishValidationFailed`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs`: implement `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`, plus the physical-delete catalog-dependency scan and a delete-specific ledger-existence check.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogLifecycleContracts.cs` (new): `ContentCatalogLifecycleService` (`PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition`/`ListArchivedDefinitions`), its four request DTOs, and `ContentCatalogLifecycleFailures.PublishValidationFailed`.
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogLifecycleServiceTests.cs` (new): real, SQLite-backed tests against the real repository (20 cases).
- `docs/errors/ERROR_CODES.md`: two new registry rows; one doc-note update to the reused `not_published` row.
- `Tests/Metadata/test-catalog.json`: twenty new `TC-CATALOG-078`-`097` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 4 (`ODY-S05-104`) corrected to `Done` (PR #108/#109); row 3 (`ODY-S05-103`) status update with PR link.
- This task contract and its ExecPlan.

### Out of scope

- Runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` of any kind, or any dependency check against them (section 4.1 rule 5 -- an explicit future extension boundary, no such state exists yet).
- Attack resolution; item use/equip/consume commands.
- Any Unity UI, including a real Archived-list UI (`ListArchivedDefinitions` is data/API only).
- Balanced content fixtures (`ODY-S05-106`'s own job).
- `.odcontent` import/export.
- Campaign-specific catalog overrides.
- `ContentBlock` execution.
- Any new ADR or change to accepted `ADR-001`-`027` architecture.
- Any change to the meaning of `ContentDefinitionId`/`ContentDefinitionRef`/`Version`/`Revision`/`Status`/`PropertiesJson`.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.application/Runtime/Content/ContentCatalogLifecycleContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogLifecycleServiceTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-103_Publish_Archive_Delete_Lifecycle.md
docs/plans/active/ODY-S05-103_Publish_Archive_Delete_Lifecycle.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*, ODY-S05-101_*, ODY-S05-102_*, ODY-S05-104_*, ODY-S05-105_*
Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs
Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs
Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs
Packages/com.odyssey.application/Runtime/Content/CatalogValidationContracts.cs
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` owns the new `ContentCatalogLifecycleService`/request DTOs and the `IContentCatalogRepository` interface extension; `Odyssey.Persistence` owns the new repository methods' implementation. No Domain change (`ADR-001`).
- Authoritative-state and transaction boundary: `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` each commit in one transaction with `CommandId`-based ledger replay, matching `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`CreateNextDraftVersionFromPublished`'s own established shape (`ADR-002`). `DeleteDraftDefinition`'s own existence-check-and-delete for the catalog-dependency scan happens inside the same transaction as the physical `DELETE`, so no other command can insert a new reference between the check and the delete.
- Serialization / compatibility boundary: no new persisted contract shape beyond the existing `ContentDefinitionRecord` columns; no direct Domain serialization; the physical-delete dependency scan uses a plain SQL substring match on the already-existing `DependencyRefsJson`/`PropertiesJson` columns, not a new schema or a new `TypedDefinitionCodec` dependency from Persistence.
- Time / RNG rule: reuses the injected `IWallClock` already threaded through the repository.
- Unity / thread / lifetime rule: not applicable -- pure .NET code, no Unity API used.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `ADR-027` section 12's MainGM-only baseline is enforced, not extended or redefined -- `ContentCatalogLifecycleService` reuses `ContentCatalogAuthoringFailures.NotMainGm` directly rather than minting a duplicate authorization error.
- Performance or platform constraint: not applicable.
- Other: publish validation never trusts a client-side result -- `ContentCatalogLifecycleService.PublishDefinition` always re-runs `CatalogValidationService.ValidateDraftForPublish` server-side against the actual stored Draft, and never calls the repository's own `PublishDefinition` at all when validation reports the Draft invalid, guaranteeing zero row mutation on a rejected publish.

## 7. Expected behavior

### Scenario 1 -- MainGM publishes a valid Draft

**Given** a Draft that `CatalogValidationService.ValidateDraftForPublish` reports valid
**When** `ContentCatalogLifecycleService.PublishDefinition` is called by MainGM
**Then** it succeeds, `Status` becomes `Published`, `Version` becomes `1`, `Revision` increments by 1, `PublishedByUserId`/`PublishedAt` are stamped, and the definition can no longer be edited through `UpdateDraftContentDefinition`.

### Scenario 2 -- publishing an invalid Draft never mutates it

**Given** a Draft that validation reports invalid (e.g. a Weapon requiring ammo with no compatible ammo)
**When** `PublishDefinition` is called
**Then** it fails with `ContentCatalogPublishValidationFailed`, the repository's own `PublishDefinition` is never called, and the row's `Status`/`Revision`/`Version` are all completely unchanged.

### Scenario 3 -- non-MainGM cannot publish/archive/delete

**Given** a non-MainGM actor
**When** any of `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` is called
**Then** it is rejected with `ContentCatalogAuthoringDenied` and the repository's own state is provably unchanged.

### Scenario 4 -- MainGM archives a Published definition, which remains loadable

**Given** a Published definition
**When** `ArchiveDefinition` is called by MainGM
**Then** `Status` becomes `Archived`, `ArchivedAt`/`ArchiveReason` are stamped, the row is never physically removed, and it remains loadable through `GetContentDefinition` and visible through `ListContentDefinitions`/`ListArchivedDefinitions` afterward.

### Scenario 5 -- physical delete is restricted to safe, unused Drafts

**Given** a Draft, a Published definition, an Archived definition, and a Draft referenced by another catalog definition's own `DependencyRefsJson`
**When** `DeleteDraftDefinition` is called against each
**Then** only the genuinely unused Draft succeeds (the row becomes unreadable via `GetContentDefinition` afterward); the Published and Archived targets fail with `PersistenceContentDefinitionNotDraft`; the referenced Draft fails with `PersistenceContentDefinitionReferenced`.

### Scenario 6 -- every lifecycle command is idempotent

**Given** a `CommandId` already successfully applied to any of the three new lifecycle methods
**When** the same command is replayed
**Then** the same result is returned (for delete, success even though the row itself is now gone) and no duplicate mutation, double revision increment, or exception occurs.

### Required invariants

- No `ODY-S05-106` behavior (balanced fixtures) is implemented.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type, table, or dependency check is introduced (verified directly by `TC-CATALOG-096`/`097`'s own reflection/schema scans).
- `ADR-001`-`027` are unmodified.
- No new role or permission extension beyond MainGM-only.
- `ContentDefinitionId`/`ContentDefinitionRef`/`Version`/`Revision`/`Status`/`PropertiesJson` all keep their existing established meaning.

## 8. Deliverables

- Production code: `ContentCatalogLifecycleContracts.cs` (Application), `IContentCatalogRepository.PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` extension + implementation, two `PersistenceFailures`/`ErrorCodes` entries plus one new `ContentCatalogLifecycleFailures` entry.
- Tests: `ContentCatalogLifecycleServiceTests.cs` (22 cases, amended: +2) -- `TC-CATALOG-078`-`099`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 3/4), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None -- no schema change beyond code already covered by `ODY-S05-101`'s own `CREATE TABLE IF NOT EXISTS`.

## 9. Acceptance criteria

1. `PublishDefinition` succeeds only for a Draft that passes `ODY-S05-104`'s own server-side validation (`TC-CATALOG-078`/`079`).
2. A successful publish sets `Status=Published`, `Version=1`, stamps `PublishedByUserId`/`PublishedAt`, and increments `Revision` consistently with the existing optimistic-concurrency convention (`TC-CATALOG-078`).
3. After publish, `UpdateDraftContentDefinition` still rejects the now-Published row (`TC-CATALOG-081`).
4. `ArchiveDefinition` succeeds for a Published definition, sets `ArchivedAt`/`ArchiveReason`, never physically deletes the row, and the row remains loadable/listable afterward (`TC-CATALOG-084`/`085`).
5. A dedicated Archived-list query returns Archived definitions only, separate from Draft/Published (`TC-CATALOG-086`).
6. Physical delete succeeds only for an unused Draft; Published, Archived, and referenced-Draft targets are all rejected (`TC-CATALOG-089`-`092`).
7. Every lifecycle command is idempotent via `CommandId` (`TC-CATALOG-083`, `TC-CATALOG-095`).
8. Non-MainGM cannot publish/archive/delete, with no repository state change (`TC-CATALOG-080`/`087`/`094`).
9. Publish validation is never trusted client-side -- the Application-layer service always re-runs `CatalogValidationService.ValidateDraftForPublish` itself (`TC-CATALOG-079`).
10. No runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type or table is introduced (`TC-CATALOG-096`/`097`).
11. No Unity/UI code is touched (confirmed by diff-scope review).
12. No `ADR-001`-`027` file is modified.
13. New tests are registered in `Tests/Metadata/test-catalog.json`.
14. This task contract and its ExecPlan exist.
15. `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-104` `Done` (PR #108/#109) and `ODY-S05-103` `In Review` with PR link.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-078` | .NET / NUnit (Persistence) | MainGM publish of a valid Draft succeeds with correct field updates | Pass |
| `TC-CATALOG-079` | .NET / NUnit (Persistence) | Publish of an invalid Draft fails without mutation | Pass |
| `TC-CATALOG-080` | .NET / NUnit (Persistence) | Non-MainGM publish denied, no state change | Pass |
| `TC-CATALOG-081` | .NET / NUnit (Persistence) | Published row rejects `UpdateDraftContentDefinition` | Pass |
| `TC-CATALOG-082` | .NET / NUnit (Persistence) | Publish of a non-Draft (fresh command) fails | Pass |
| `TC-CATALOG-083` | .NET / NUnit (Persistence) | Publish command replay stable, no double increment | Pass |
| `TC-CATALOG-084` | .NET / NUnit (Persistence) | MainGM archive of Published succeeds | Pass |
| `TC-CATALOG-085` | .NET / NUnit (Persistence) | Archived remains loadable via `GetContentDefinition` | Pass |
| `TC-CATALOG-086` | .NET / NUnit (Persistence) | Archived-list query returns Archived only | Pass |
| `TC-CATALOG-087` | .NET / NUnit (Persistence) | Non-MainGM archive denied, no state change | Pass |
| `TC-CATALOG-088` | .NET / NUnit (Persistence) | Archive of a Draft fails | Pass |
| `TC-CATALOG-089` | .NET / NUnit (Persistence) | Delete of unused Draft succeeds | Pass |
| `TC-CATALOG-090` | .NET / NUnit (Persistence) | Delete of Published rejected | Pass |
| `TC-CATALOG-091` | .NET / NUnit (Persistence) | Delete of Archived rejected | Pass |
| `TC-CATALOG-092` | .NET / NUnit (Persistence) | Delete of referenced Draft rejected | Pass |
| `TC-CATALOG-093` | .NET / NUnit (Persistence) | Delete not over-blocked by an unrelated reference | Pass |
| `TC-CATALOG-094` | .NET / NUnit (Persistence) | Non-MainGM delete denied, no state change | Pass |
| `TC-CATALOG-095` | .NET / NUnit (Persistence) | Delete command replay safe after row is gone | Pass |
| `TC-CATALOG-096`/`097` | .NET / NUnit (Persistence) | No runtime item/inventory/equipment/effect type or table; the three allowed catalog lifecycle tables (`ContentDefinition`, `ContentDefinitionCommandLedger`, `ContentDefinitionDeleteLedger`) are confirmed present | Pass |
| `TC-CATALOG-098` | .NET / NUnit (Persistence) | A CommandId reused from a create/update/publish/archive of the same still-existing Draft fails with CommandIdentityMismatch, leaves the Draft readable | Pass |
| `TC-CATALOG-099` | .NET / NUnit (Persistence) | A CommandId reused from a different definition's own successful delete fails with CommandIdentityMismatch, deletes neither | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`-`027` file, and no Unity/UI file is touched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable -- no Unity/UI code.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign database, the same fixture convention every sibling persistence/service test already uses.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation -- no Unity code touched.
- Any test of `ODY-S05-106`'s own future behavior -- none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None -- purely additive methods; no existing table or column altered (the `ContentDefinition` table already has every column `PublishDefinition`/`ArchiveDefinition` write to, from `ODY-S05-101`'s own original schema).
- Version fields affected: None beyond the already-existing `Version`/`Revision` columns, used exactly as `ODY-S05-101`/`102` already established.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: `DeleteDraftDefinition` is the only irreversible operation this task introduces, and it is restricted (both by the Application-layer authorization check and the repository's own re-verification) to unused Drafts only -- no Published or Archived data can ever be physically removed by this task's own code.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: `UserId` (already-established audit-field pattern, now also used for `PublishedByUserId`).
- Trust boundaries: the Application-layer `ContentCatalogLifecycleService` is the trust boundary for MainGM-only lifecycle commands, checked before any repository mutation -- matching `ContentCatalogAuthoringService`'s own convention exactly.
- Authorization / audience checks: `ADR-027` section 12's MainGM-only baseline, enforced via the reused `ContentCatalogAuthoringFailures.NotMainGm`; no new role, no AssistantGM/player lifecycle path.
- Redaction requirements: Not applicable -- no networking/redaction surface touched.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: publish never trusts a client-supplied validation result -- it is always re-derived server-side from the actual stored Draft at command time.
- Security tests: `TC-CATALOG-080`/`087`/`094` directly prove denial with no state change; `TC-CATALOG-079` proves a rejected publish never mutates the row.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: this task introduces new public Application-layer contracts (`ContentCatalogLifecycleService` and its four request types) and extends `IContentCatalogRepository` with three new persisted-state-producing/removing methods -- both `ExecPlan` triggers `PLANS.md` §1 already names, matching every sibling `SLICE-05` MVP-block task's own reasoning.
- ExecPlan path: `docs/plans/active/ODY-S05-103_Publish_Archive_Delete_Lifecycle.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ODY-S05-101`/`102`/`104` are merged into `main` (all confirmed in section 4). Unblocks `ODY-S05-106`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 3/4), this task contract, its ExecPlan.
- Documents that must not change: `ADR-001`-`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*`/`ODY-S05-002_*`/`ODY-S05-101_*`/`ODY-S05-102_*`/`ODY-S05-104_*`/`ODY-S05-105_*`.
- Application version change: No.
- Schema / format / contract / protocol / Ruleset version change: None.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that runtime Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect and balanced content fixtures are deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` -- `PublishDefinition`/`ArchiveDefinition`/`DeleteDraftDefinition` added to `IContentCatalogRepository`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` -- one new `PersistenceFailures` entry, one doc-comment update.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` -- two new `ErrorCode` entries.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogLifecycleContracts.cs` -- new.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` -- three new method implementations plus helpers, including the amended `ContentDefinitionDeleteLedger` table and `TryFindDeleteLedgerTarget`/`InsertDeleteLedgerEntry` helpers.
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogLifecycleServiceTests.cs` -- new, 22 tests (amended: +2).
- `docs/errors/ERROR_CODES.md` -- three new rows/doc-notes (two original, one amendment doc-note reusing `application.command.identity_mismatch`).
- `Tests/Metadata/test-catalog.json` -- twenty-two new `TC-CATALOG-078`-`099` entries (amended: +2 IDs, `098`-`099`).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` -- row 4 corrected to `Done`, row 3 status update.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (611/611, amended: +2, then TC-CATALOG-098 semantics corrected), including 22 `ContentCatalogLifecycleServiceTests` cases, no regression |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `TC-CATALOG-078`/`079`. |
| AC-2 | Pass | `TC-CATALOG-078`. |
| AC-3 | Pass | `TC-CATALOG-081`. |
| AC-4 | Pass | `TC-CATALOG-084`/`085`. |
| AC-5 | Pass | `TC-CATALOG-086`. |
| AC-6 | Pass | `TC-CATALOG-089`-`092`. |
| AC-7 | Pass | `TC-CATALOG-083`, `TC-CATALOG-095`, `TC-CATALOG-098`/`099` (amendments: CommandId reuse from a non-delete operation, or from a different definition's own delete, both correctly rejected with `CommandIdentityMismatch`, never a false replay/false delete). |
| AC-8 | Pass | `TC-CATALOG-080`/`087`/`094`. |
| AC-9 | Pass | `TC-CATALOG-079`. |
| AC-10 | Pass | `TC-CATALOG-096`/`097`. |
| AC-11 | Pass | No Unity/UI path in Allowed paths or diff. |
| AC-12 | Pass | `git status --porcelain` confirms no `ADR-001`-`027` file touched. |
| AC-13 | Pass | Twenty `TC-CATALOG-078`-`097` entries added. |
| AC-14 | Pass | This task contract and ExecPlan exist. |
| AC-15 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 3/4 updated, row 3 with PR [#110](https://github.com/odyssey-services/Odyssey_VTT/pull/110). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table (to be completed after the full validation suite runs).

### Amendment (2026-09-04) — DeleteDraftDefinition idempotency fix

Product-owner review found that `DeleteDraftDefinition`'s original idempotency check (`CommandLedgerContainsCommandId`) only tested whether the caller's `CommandId` existed anywhere in the *shared* `ContentDefinitionCommandLedger` -- the same table `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`PublishDefinition`/`ArchiveDefinition`/`CreateNextDraftVersionFromPublished` all also write to. Since that table's `CommandId` values are meant to be globally unique per logical command instance, reusing a `CommandId` already recorded by any of those *other* operations -- or by a *different* definition's own prior delete -- made `DeleteDraftDefinition` return `Success` without ever deleting the row the caller actually asked for.

Fixed (first pass) by introducing a dedicated `ContentDefinitionDeleteLedger` table (`CommandId` primary key -> `ContentDefinitionId`, `DeletedAt`), written and checked only by `DeleteDraftDefinition` itself -- completely independent of the shared ledger. On each call: if the `CommandId` is not yet in this delete-only ledger, the method proceeds to actually check/delete the row; if it is already present, the recorded `ContentDefinitionId` is compared against the caller's own target -- a match is a genuine replay (`Success`, row already gone), a mismatch is a real `CommandId` reuse across two different delete targets, rejected with the existing `CommandIdentityMismatch` code (the same one `CommandContracts.cs`'s own general command pipeline already uses for this exact class of violation -- no new `ErrorCode` minted).

**This first pass was itself incomplete** -- see the second Amendment note immediately below, which closes the remaining gap it left open.

### Amendment (2026-09-04, second) — DeleteDraftDefinition must also reject CommandIds already used by non-delete operations

Product-owner review found that the first pass above, by checking *only* the new delete-only ledger, no longer consulted the shared `ContentDefinitionCommandLedger` at all -- so a `CommandId` already used by `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`/`PublishDefinition`/`ArchiveDefinition`/`CreateNextDraftVersionFromPublished` on a still-existing row would never appear in the delete-only ledger, and `DeleteDraftDefinition` would incorrectly treat it as a genuinely new request and *actually delete the row* -- a real, unintended physical delete triggered by an accidentally-reused `CommandId` from an unrelated operation.

Fixed by adding a second check, in order, right after the delete-only ledger check: if the `CommandId` exists anywhere in the shared `ContentDefinitionCommandLedger`, the request is rejected with `CommandIdentityMismatch` and nothing is deleted -- a `CommandId` recorded there was never actually used for a delete, so it can never be a legitimate delete replay, regardless of which definition it targeted. Only when the `CommandId` appears in *neither* ledger does the method proceed to its normal Draft/reference checks and the physical delete.

`TC-CATALOG-098` was rewritten to assert the corrected behavior (`CommandIdentityMismatch`, Draft left readable and untouched, rather than the previous "actually deletes it" assertion, which described the still-incomplete first-pass fix). `TC-CATALOG-099` (delete-ledger mismatch across two different delete targets) is unchanged. `ContentCatalogRepositoryContracts.cs`'s own `DeleteDraftDefinition` doc comment, which still described the single-ledger idempotency model, was updated to describe the two-ledger check.

### Known limitations

- Runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` reference checks for physical delete (`ADR-027` section 4.1 rule 5) are not implemented -- no such runtime state exists anywhere in this codebase yet. This is an explicit, honestly-recorded future extension boundary: once a later `SLICE-05` block introduces runtime item/inventory/equipment state, `DeleteDraftDefinition`'s own dependency check must be extended to also consult it before this method can be considered complete against the ADR's own full rule 5.
- The physical-delete catalog-dependency scan (`IsReferencedByAnotherDefinition`) matches by `ContentDefinitionId` alone, ignoring any referencing `ContentDefinitionRef`'s own pinned `Version`. Because a `ContentDefinitionRef` requires `Version >= 1` and a genuine Draft's own `Version` is always `0`, no reference can legitimately target a Draft through the public API today -- this scan is therefore a defensive, forward-compatible safety net rather than a scenario reachable through normal use right now (verified directly by `TC-CATALOG-092`, which seeds the referencing state via direct SQL, the same technique `ODY-S05-101`/`102`/`104` already established for constructing states the public API cannot yet produce).
- `ArchiveDefinition` only implements the Published-to-Archived transition. `ADR-027` section 4.1 rule 2 also names "any definition referenced by another catalog definition" as archivable, but nothing in this codebase can reference a still-Draft definition (the same `Version >= 1` constraint above), so this second archive trigger does not arise for any reachable state today.

### Follow-up tasks

- `ODY-S05-106` -- Minimal Test Catalog Fixtures (the direct consumer of this task's own publish/archive/delete commands, proving the full Foundation/Authoring/Validation/Publish pipeline end-to-end).
- A future runtime-Inventory/Equipment block, once activated, must extend `DeleteDraftDefinition`'s own dependency check to also consult runtime item/inventory/equipment/effect state, per `ADR-027` section 4.1 rule 5.

### Self-review summary

- Scope review: diff limited to the eleven files in section 5's Allowed paths; no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`-`027` file, no Unity file, no `ODY-S05-101`/`102`/`104`/`105`'s own foundation files touched.
- Architecture review: `ContentCatalogAuthoringService`'s own precedent followed exactly for the Application-service/repository split; `ODY-S05-101`'s own `ContentDefinitionCommandLedger` idempotency mechanism reused (and, for delete, adapted with a ledger-existence-only check since the row itself is gone after success).
- Test review: 20 new tests, full suite re-run green, no regression.
- Security/privacy review: MainGM-only enforced before any repository mutation; publish validation always re-derived server-side, never trusted from the caller.
- Documentation/version review: `ERROR_CODES.md`/test-catalog updated and cross-checked by `check-repository-policy.ps1`/`verify-test-structure.ps1`; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-04 — Decision: publishing sets `Version = 1` unconditionally on the very first (and, for this same `ContentDefinitionId` row, only ever) publish. Authority: `11_Content_Block_System` section 6.2 ("`Version` меняется только при публикации новой механической версии") read against `ODY-S05-102`'s own already-established `CreateNextDraftVersionFromPublished`, which mints an entirely new `ContentDefinitionId` for a "next version" rather than bumping the same row's `Version` a second time -- confirming no in-place "publish version 2" operation exists or is needed in this codebase's own model.
- 2026-09-04 — Decision: the Application-layer `ContentCatalogLifecycleService.PublishDefinition` only re-runs `CatalogValidationService.ValidateDraftForPublish` when the target's own current `Status` is still `Draft`. Authority: re-running Draft-only validation against an already-Published record would always report `DefinitionNotDraft` and incorrectly reject a legitimate `CommandId` replay of an already-successful publish; skipping validation once the target is no longer Draft lets the repository's own ledger-based idempotency (a genuine replay) or its own `PersistenceContentDefinitionNotDraft` check (a genuinely invalid new attempt against an already-published target) handle both remaining cases correctly. This bug was caught and fixed during this task's own design, not discovered via a failing test.
- 2026-09-04 — Decision: `ArchiveDefinition` reuses `PersistenceContentDefinitionNotPublished` (originally `ODY-S05-102`'s own `CreateNextDraftVersionFromPublished` rejection) rather than minting a new error code, since both share the exact same underlying meaning: "this operation requires a `Published` source and the target is not one." Authority: this task's own ТЗ explicit "add new `ErrorCode`s only if existing codes are insufficient" instruction.
- 2026-09-04 — Decision: the physical-delete catalog-dependency scan (`IsReferencedByAnotherDefinition`) lives entirely inside `SqliteContentCatalogRepository` as a plain SQL substring match over `DependencyRefsJson`/`PropertiesJson`, rather than in the Application-layer lifecycle service using `TypedDefinitionCodec`. Authority: this keeps the existence-check-and-delete atomic in one transaction (no TOCTOU race across a service/repository round trip), avoids a new cross-project dependency from the lifecycle service back into typed-decode logic it does not otherwise need, and needs no schema change -- the canonical `ContentDefinitionId` string already appears verbatim inside any JSON-embedded reference to it, so no decode is required to detect a match.
- 2026-09-04 — Decision: `DeleteDraftDefinition`'s own idempotent-replay check uses a new ledger-existence-only helper, not the existing `TryFindByCommandId` (which re-selects the target row and would incorrectly return `null` -- indistinguishable from "never happened" -- for an already-successfully-deleted row). Authority: this task's own ТЗ explicit "if the existing command ledger cannot replay a deleted row, add the smallest safe mechanism needed and document the reason" instruction. **Superseded/refined by the 2026-09-04 amendment below**: the original implementation of this helper (`CommandLedgerContainsCommandId`) checked existence in the *shared* `ContentDefinitionCommandLedger`, which was itself a genuine bug (see the amendment) -- the ledger-existence-only *approach* was correct, but it needed its own dedicated `ContentDefinitionDeleteLedger` table, not the shared one, to actually distinguish a real delete-replay from an unrelated `CommandId` collision.
- 2026-09-04 — Decision (explicit, requested by the ТЗ itself): a `ContentDefinitionRef`'s own `Version >= 1` constructor requirement means no reference can ever legitimately target a genuine Draft (`Version == 0`) through the public API today. The physical-delete dependency check is still implemented (matching by `ContentDefinitionId` alone, ignoring version) as a defensive, forward-compatible safety net, and is directly tested via a state constructed with test-only direct SQL (the same established technique prior `SLICE-05` tasks already use for states the public API cannot yet produce) -- not left unimplemented or faked.
- 2026-09-04 — Decision (amendment): `DeleteDraftDefinition`'s idempotency now uses a dedicated `ContentDefinitionDeleteLedger` table, written and checked only by this one method, comparing the recorded `ContentDefinitionId` against the caller's own target rather than trusting bare `CommandId` existence. A mismatch reuses the existing `CommandIdentityMismatch` error code (`CommandContracts.cs`'s own established convention for this exact class of violation) rather than minting a new one. Authority: product-owner-identified correctness bug in the original shared-ledger check; this task's own ТЗ "keep scope inside ODY-S05-103" and "add new ErrorCodes only if existing insufficient" instructions.
- 2026-09-04 — Decision (second amendment): `DeleteDraftDefinition` now additionally checks the *shared* `ContentDefinitionCommandLedger` for bare `CommandId` existence (no target comparison needed -- any hit there was never a delete) and rejects with `CommandIdentityMismatch`, in addition to the dedicated delete-only ledger check the first amendment introduced. Authority: product-owner-identified gap in the first amendment, which by checking only the new delete-only ledger stopped consulting the shared ledger entirely, letting a `CommandId` reused from a non-delete operation on a still-existing row trigger a real, unintended physical delete.

### Approved task changes

- 2026-09-04 — Product-owner-requested amendment to the already-open PR #110: fix `DeleteDraftDefinition`'s own idempotency to use a dedicated delete-only ledger instead of the shared `ContentDefinitionCommandLedger`, and correctly reject (rather than silently succeed on) a `CommandId` reused across two different delete targets (see the Amendment note in section 17). Scope stayed within this task's own Allowed paths (`SqliteContentCatalogRepository.cs`, `CampaignRepositoryContracts.cs`, `ContentCatalogLifecycleServiceTests.cs`, `ERROR_CODES.md`, `test-catalog.json`) -- no new project file, no new `ErrorCode` (reused `CommandIdentityMismatch`).
- 2026-09-04 — Second product-owner-requested amendment to the same PR #110: `DeleteDraftDefinition` must also reject any `CommandId` already used by a non-delete catalog command, closing a gap the first amendment left open (see the second Amendment note in section 17). `TC-CATALOG-098` rewritten to assert the corrected behavior; `ContentCatalogRepositoryContracts.cs`'s own stale `DeleteDraftDefinition` doc comment corrected. Scope stayed within `SqliteContentCatalogRepository.cs`, `ContentCatalogRepositoryContracts.cs`, `ContentCatalogLifecycleServiceTests.cs`, `test-catalog.json` -- no new file, no new `ErrorCode`.
