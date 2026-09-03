# ODY-S05-102 — GM Catalog Authoring MVP

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-102-gm-catalog-authoring-mvp`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/106
**ExecPlan:** `docs/plans/active/ODY-S05-102_GM_Catalog_Authoring_MVP.md`
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

Implement the first MVP authoring layer for the Content Catalog `ODY-S05-101` (merged, PR #105) laid the foundation for: MainGM-only Application-level commands to create a Draft definition, edit an existing Draft with revision-guarded optimistic concurrency, and create the next Draft version from an already-Published definition without editing that Published source in place. No publish/archive/delete workflow, no validation rules, no typed properties, no runtime Inventory/Equipment/`ItemInstance`/`ActiveEffect` — all reserved for `ODY-S05-103`–`106` and later backlog blocks.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-101` gave the catalog a generic storage/lifecycle envelope but only a bare, permission-free `UpdateDraftContentDefinition` primitive proving the Revision mechanism — no MainGM-gated authoring surface exists yet, and no way exists yet to branch a new Draft off a Published definition.
- Value or risk reduction: gives MainGM the actual ability to author catalog content in the MVP (an explicit product-owner requirement recorded in `ADR-027` section 20), while keeping the permission check in a distinct Application-layer service — following `BoardMovementService`'s own established precedent — so a denied request never reaches the repository at all.
- Blocking or enabling relationship: unblocks a real authoring workflow for `ODY-S05-106`'s own future test-catalog fixtures; does not block `ODY-S05-103`/`104`/`105`, which each depend on `ODY-S05-101` directly, not on this task.

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially the `ODY-S05-102` row (section 5) and task-boundary paragraph (section 6).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 4, 4.1, 12, 20 (full read).
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` (full read) — the `ODY-S05-101` foundation contract this task builds on.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` (full read) — the foundation implementation, including its `ContentDefinitionCommandLedger` idempotency mechanism (`ODY-S05-101` amendment), which this task's own new repository method must reuse, not bypass.
- `Packages/com.odyssey.application/Runtime/Board/BoardMovementService.cs`/`BoardContracts.cs` (full read) — the binding structural precedent for an Application-layer service that checks authorization before calling a repository, with a distinct `XFailures` class for the authorization error.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-102`, `ADR-027` section 4.1/12.
- Existing test IDs: `TC-CATALOG-001`–`012` (re-verified unmodified).
- New test IDs introduced: `TC-CATALOG-013`–`023`.

### Task-safe private context

- Approved summary / references: `ADR-027`'s own already-accepted content is cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git merge-base --is-ancestor` confirmed PR #105 (`ODY-S05-101`, Content Catalog Foundation, including its idempotency-fix amendment) is a real ancestor of `origin/main`.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 still read `In Review` despite PR #105 being merged — corrected to `Done` with the PR link as this task's own first preflight step, per this task's explicit instruction.
- `IContentCatalogRepository` (confirmed by `Read`) has no `CreateNextDraftVersionFromPublished`-shaped method and no `PublishDefinition` method at all — confirming the ТЗ's own expectation that Published sources for this task's own tests must be seeded directly at the SQL level, the same technique `ODY-S05-101`'s own tests already established (`MarkStatusDirectly`).
- `BoardMovementService`/`BoardContracts.cs` were read in full and confirmed as the binding structural precedent: a static Application-layer service performing authorization before calling the repository, a request DTO carrying `ActorIsMainGm` (the same "caller supplies the baseline role, no real session/role model exists yet" simplification `ADR-019`/`SLICE-02` leave to a later revision), and a dedicated `XFailures` class distinct from `PersistenceFailures` for the authorization error, since the check happens outside the repository.

### Assumptions

- None. Every fact above was directly observed via `Read`/`grep`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs`: extend `IContentCatalogRepository` with `CreateNextDraftVersionFromPublished` — the minimal repository extension the ТЗ explicitly allows when no publish API exists yet.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs`: one new `PersistenceFailures.ContentDefinitionNotPublished` entry.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs`: two new `ErrorCode` entries (`PersistenceContentDefinitionNotPublished`, `ContentCatalogAuthoringDenied`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs`: implement `CreateNextDraftVersionFromPublished`, reusing the existing `ContentDefinitionCommandLedger`/`TryFindByCommandId`/`InsertCommandLedgerEntry` idempotency mechanism unmodified.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs` (new): `ContentCatalogAuthoringService` (`CreateDraftDefinition`/`UpdateDraftDefinition`/`CreateNextDraftVersionFromPublished`), its three request DTOs, and `ContentCatalogAuthoringFailures.NotMainGm`.
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogAuthoringServiceTests.cs` (new): real, SQLite-backed tests against the real repository, mirroring `BoardMovementServiceTests`'s own fixture convention.
- `docs/errors/ERROR_CODES.md`: two new registry rows.
- `Tests/Metadata/test-catalog.json`: eleven new `TC-CATALOG-013`–`023` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 1 (`ODY-S05-101`) corrected to `Done`; row 2 (`ODY-S05-102`) status update with PR link/evidence.
- This task contract and its ExecPlan.

### Out of scope

- `PublishDefinition`/`ArchiveDefinition`/physical delete/Archived-list query (`ODY-S05-103`).
- Per-type usability/applicability validation, missing-reference checks, `ContentBlock` cycle checks, Ruleset/version compatibility checks (`ODY-S05-104`).
- Typed properties for `WeaponDefinition`/`ArmorDefinition`/`AmmoDefinition`/`AbilityDefinition`/`EffectDefinition`/`Resource`/`BodyPart` (`ODY-S05-105`).
- Minimal test catalog fixtures (`ODY-S05-106`).
- Any runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`/item-sourced ability/`ItemDefinition` migration/attack pipeline implementation.
- Any Unity UI or Content Editor UI.
- Campaign-specific custom catalog or per-campaign overrides.
- `.odcontent` import/export.
- A final balanced content pack.
- Extending `ADR-027`'s own permission model or introducing a new role — MainGM-only, exactly as section 12 already fixes.
- Any edit to `ADR-001`–`027`'s own accepted content.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogAuthoringServiceTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-102_GM_Catalog_Authoring_MVP.md
docs/plans/active/ODY-S05-102_GM_Catalog_Authoring_MVP.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*, ODY-S05-101_*
Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs
DotNet/Tests/Odyssey.Tests.Domain/Content/ContentDefinitionRefTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteContentCatalogRepositoryTests.cs
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Application` owns the new `ContentCatalogAuthoringService`/request DTOs and the `IContentCatalogRepository` interface extension; `Odyssey.Persistence` owns the new repository method's implementation. No Domain change (`ADR-001`).
- Authoritative-state and transaction boundary: `CreateNextDraftVersionFromPublished` commits in one transaction with `CommandId`-based ledger replay, exactly mirroring `CreateDraftContentDefinition`/`UpdateDraftContentDefinition`'s own established shape (`ADR-002`).
- Serialization / compatibility boundary: no new persisted contract shape beyond the existing `ContentDefinitionRecord`; no direct Domain serialization.
- Time / RNG rule: reuses the injected `IWallClock` already threaded through the repository.
- Unity / thread / lifetime rule: not applicable — pure .NET code, no Unity API used.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `ADR-027` section 12's MainGM-only catalog-authoring baseline is enforced, not extended or redefined — no new role, no AssistantGM/player authoring path introduced.
- Performance or platform constraint: not applicable.
- Other: the authorization check happens in the Application-layer service, before the repository is ever called — a denied request causes zero repository state change and consumes no `CommandId` in the ledger, verified directly by tests (`TC-CATALOG-014`/`017`/`020`).

## 7. Expected behavior

### Scenario 1 — MainGM can create and edit a Draft

**Given** a MainGM actor
**When** `ContentCatalogAuthoringService.CreateDraftDefinition` then `UpdateDraftDefinition` are called
**Then** both succeed, the Draft's `Revision` increments by exactly 1 per update, and a stale `expectedRevision` is rejected with no state change.

### Scenario 2 — non-MainGM is rejected before the repository is ever touched

**Given** a non-MainGM actor
**When** any of the three authoring commands is called
**Then** it is rejected with `ContentCatalogAuthoringDenied` and the repository's own state is provably unchanged (no new row, no revision change, no ledger entry).

### Scenario 3 — Published/Archived definitions cannot be edited in place

**Given** a definition whose `Status` is `Published` or `Archived`
**When** `UpdateDraftDefinition` is called against it, even by MainGM
**Then** it is rejected with `PersistenceContentDefinitionNotDraft`.

### Scenario 4 — MainGM can branch a next Draft version from a Published definition

**Given** a Published definition
**When** `CreateNextDraftVersionFromPublished` is called by MainGM
**Then** a new Draft is created with its own `ContentDefinitionId`, `Status=Draft`, `Version=0`, `Revision=1`, copying the source's own fields as its starting point, and the Published source's own row is completely unchanged.

### Scenario 5 — every new command is idempotent

**Given** a `CommandId` already successfully applied by any of the three new authoring paths
**When** the same command is replayed
**Then** the same result is returned and no duplicate row, double mutation, or double revision increment occurs — reusing `ODY-S05-101`'s own `ContentDefinitionCommandLedger`, not a new or parallel idempotency mechanism.

### Required invariants

- No `ODY-S05-103`–`106` behavior (publish/archive/delete, validation, typed properties, fixtures) is implemented.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type, table, or column is introduced (verified directly by `TC-CATALOG-023`'s own reflection-based scan).
- `ADR-001`–`027` are unmodified.
- No new role or permission extension beyond MainGM-only.

## 8. Deliverables

- Production code: `ContentCatalogAuthoringContracts.cs` (Application), `IContentCatalogRepository.CreateNextDraftVersionFromPublished` extension + implementation, two `PersistenceFailures`/`ErrorCodes` entries.
- Tests: `ContentCatalogAuthoringServiceTests.cs` (14 cases) — `TC-CATALOG-013`–`023`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 1 and 2), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None — no schema change beyond code already covered by `ODY-S05-101`'s own `CREATE TABLE IF NOT EXISTS`.

## 9. Acceptance criteria

1. MainGM can create a Draft definition through an application-level authoring command (`TC-CATALOG-013`).
2. MainGM can edit an existing Draft with a revision guard (`TC-CATALOG-015`/`016`).
3. Non-MainGM cannot create/edit/create-next-draft (`TC-CATALOG-014`/`017`/`020`).
4. Published/Archived definitions cannot be edited in place (`TC-CATALOG-018`).
5. MainGM can create the next Draft version from a Published definition without mutating the Published source (`TC-CATALOG-019`).
6. Command replay is idempotent for create, update, and create-next-draft (`TC-CATALOG-013`/`015`/`022`).
7. New behavior is covered by real, SQLite-backed tests, not contract stubs.
8. No publish/archive/delete behavior is implemented.
9. No runtime item/equipment/inventory/effect schema or type is introduced (`TC-CATALOG-023`).
10. No `ADR-001`–`027` file is modified.
11. `SLICE-05_IMPLEMENTATION_BACKLOG.md` reflects `ODY-S05-101` as `Done` and `ODY-S05-102` as `In Review` with PR link.
12. This task contract and its ExecPlan exist.
13. Required validation commands (section 10) pass.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-013` | .NET / NUnit (Persistence) | MainGM create succeeds, persists foundation fields, replay idempotent | Pass |
| `TC-CATALOG-014` | .NET / NUnit (Persistence) | Non-MainGM create denied, no state change | Pass |
| `TC-CATALOG-015` | .NET / NUnit (Persistence) | MainGM update succeeds, Revision +1, replay idempotent | Pass |
| `TC-CATALOG-016` | .NET / NUnit (Persistence) | Stale-revision update rejected, no state change | Pass |
| `TC-CATALOG-017` | .NET / NUnit (Persistence) | Non-MainGM update denied, no state change | Pass |
| `TC-CATALOG-018` | .NET / NUnit (Persistence) | Update on Published/Archived rejected | Pass |
| `TC-CATALOG-019` | .NET / NUnit (Persistence) | Create-next-draft copies source fields, new identity, source untouched | Pass |
| `TC-CATALOG-020` | .NET / NUnit (Persistence) | Non-MainGM create-next-draft denied, no new row | Pass |
| `TC-CATALOG-021` | .NET / NUnit (Persistence) | Create-next-draft on non-Published source rejected | Pass |
| `TC-CATALOG-022` | .NET / NUnit (Persistence) | Create-next-draft replay idempotent | Pass |
| `TC-CATALOG-023` | .NET / NUnit (Persistence) | Reflection scan: no runtime item/inventory/equipment/effect type in `Odyssey.Application.Content` | Pass |

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
- Network topology or database fixture: real temp-directory SQLite campaign database, the same fixture convention every sibling persistence/service test already uses.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation — no Unity code touched.
- Any test of `ODY-S05-103`–`106`'s own future behavior — none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — purely additive method/type; no existing table or column altered.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: `ActorUserId` (already-established audit-field pattern).
- Trust boundaries: the Application-layer service is the trust boundary for MainGM-only authoring — checked before any repository call, matching `BoardMovementService`'s own convention.
- Authorization / audience checks: `ADR-027` section 12's MainGM-only baseline, enforced by `ContentCatalogAuthoringFailures.NotMainGm`; no new role, no AssistantGM/player authoring path.
- Redaction requirements: Not applicable — no networking/redaction surface touched.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Basic field-shape checks only, inherited from `ODY-S05-101`'s own request validation.
- Security tests: `TC-CATALOG-014`/`017`/`020` directly prove denial with no state change.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: this task introduces a new public Application-layer contract (`ContentCatalogAuthoringService` and its three request types) and extends `IContentCatalogRepository` with a new persisted-state-producing method — both `ExecPlan` triggers `PLANS.md` §1 already names, matching `ODY-S05-101`'s own reasoning for its sibling foundation task.
- ExecPlan path: `docs/plans/active/ODY-S05-102_GM_Catalog_Authoring_MVP.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ODY-S05-101` is merged into `main` (confirmed in section 4). Does not block `ODY-S05-103`/`104`/`105`, which depend on `ODY-S05-101` directly.

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 1/2), this task contract, its ExecPlan.
- Documents that must not change: `ADR-001`–`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*`/`ODY-S05-002_*`/`ODY-S05-101_*`.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that publish/archive/delete/validation/typed-definitions/runtime are deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` — `CreateNextDraftVersionFromPublished` added to `IContentCatalogRepository`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — one new `PersistenceFailures` entry.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — two new `ErrorCode` entries.
- `Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs` — new.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs` — `CreateNextDraftVersionFromPublished` implementation.
- `DotNet/Tests/Odyssey.Tests.Persistence/Content/ContentCatalogAuthoringServiceTests.cs` — new, 14 tests.
- `docs/errors/ERROR_CODES.md` — two new rows.
- `Tests/Metadata/test-catalog.json` — eleven new `TC-CATALOG-013`–`023` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row 1 corrected to `Done`, row 2 status update.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (526/526) including 14 new tests, no regression |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `TC-CATALOG-013`. |
| AC-2 | Pass | `TC-CATALOG-015`/`016`. |
| AC-3 | Pass | `TC-CATALOG-014`/`017`/`020`. |
| AC-4 | Pass | `TC-CATALOG-018`. |
| AC-5 | Pass | `TC-CATALOG-019`. |
| AC-6 | Pass | `TC-CATALOG-013`/`015`/`022`. |
| AC-7 | Pass | 14 real SQLite-backed tests, no stubs. |
| AC-8 | Pass | No `PublishDefinition`/`ArchiveDefinition`/delete method exists anywhere in this task's diff. |
| AC-9 | Pass | `TC-CATALOG-023`. |
| AC-10 | Pass | `git status --porcelain` confirms no `ADR-001`–`027` file touched. |
| AC-11 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 1/2 updated. |
| AC-12 | Pass | This task contract and ExecPlan exist. |
| AC-13 | Pass | Validation-results table above. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table.

### Known limitations

- No publish/archive/delete/validation/typed-property/fixture behavior exists yet — each is its own reserved future task (`ODY-S05-103`–`106`).
- `CreateNextDraftVersionFromPublished`'s own tests seed a Published source directly at the SQL level (`MarkStatusDirectly`), since no `PublishDefinition` command exists yet — the same technique `ODY-S05-101`'s own tests already established, explicitly sanctioned by this task's own ТЗ.

### Follow-up tasks

- `ODY-S05-103` — Publish/Archive/Delete Lifecycle (will give `CreateNextDraftVersionFromPublished` a real, command-driven Published source instead of direct SQL seeding in tests).
- `ODY-S05-104` — Catalog Validation MVP.
- `ODY-S05-105` — Base Definition Types.

### Self-review summary

- Scope review: diff limited to the twelve files in section 5's Allowed paths; no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`–`027` file, no Unity file, no `ODY-S05-101`'s own foundation files (`ContentCatalog.cs`, its own tests) touched.
- Architecture review: `BoardMovementService`'s own precedent followed exactly for the Application-service/repository split; `ODY-S05-101`'s own `ContentDefinitionCommandLedger` idempotency mechanism reused unmodified, not duplicated.
- Test review: 14 new tests, full suite re-run green, no regression.
- Security/privacy review: MainGM-only enforced before any repository call; no new role, no redaction surface.
- Documentation/version review: `ERROR_CODES.md`/test-catalog updated and cross-checked by `check-repository-policy.ps1`/`verify-test-structure.ps1`; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-03 — Decision: follow `BoardMovementService`'s exact structural precedent — a static Application-layer service checking `ActorIsMainGm` before ever calling the repository, with its own distinct `ContentCatalogAuthoringFailures` class (not `PersistenceFailures`), since the check happens outside the repository. Authority: this task's own ТЗ "Expected design" section, matched against the existing `BoardMovementService`/`BoardContracts.cs` precedent.
- 2026-09-03 — Decision: extend `IContentCatalogRepository` with `CreateNextDraftVersionFromPublished` as a minimal, purpose-built repository method (reads the Published source once, writes a new Draft row, never touches the source), rather than composing it from `GetContentDefinition` + `CreateDraftContentDefinition` at the service layer — the latter would require two round trips and could not use one atomic transaction with the `ContentDefinitionCommandLedger`. Authority: this task's own ТЗ explicitly allowing a minimal repository extension when no publish API exists yet, and `ADR-002`'s own single-transaction-per-command discipline.
- 2026-09-03 — Decision: `CreateNextDraftVersionFromPublished`'s own tests seed a Published source directly via SQL (`MarkStatusDirectly`), the same technique `ODY-S05-101`'s own tests already established, since no `PublishDefinition` command exists yet. Authority: this task's own ТЗ explicit instruction ("Если foundation repository не имеет публичного publish API, можно покрыть Published source в тестах прямым SQL setup, как уже сделано в `ODY-S05-101` тестах").
- 2026-09-03 — Decision: `UpdateDraftDefinition` only ever touches `Name`/`Description`/`PropertiesJson` — the exact three fields `ODY-S05-101`'s own `UpdateDraftContentDefinition` already supports — deliberately not extending the repository to also let authoring edit `RulesetCompatibility`/`Tags`/`DependencyRefs` in this task. Authority: this task's own ТЗ ("Обновляет только foundation-поля, которые уже есть в 101... tags/ruleset/dependency refs только если для этого уже есть аккуратная repository support") — no such support exists yet, so those fields remain out of this task's own scope.

### Approved task changes

- None yet.
