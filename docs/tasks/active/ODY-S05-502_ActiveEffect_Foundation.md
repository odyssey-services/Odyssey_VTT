# ODY-S05-502 — ActiveEffect Foundation

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-502-activeeffect-foundation`
**Pull request:** TBD (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-502_ActiveEffect_Foundation.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Introduce the `ActiveEffect` domain aggregate exactly per `ADR-028` §5, plus a standalone `IActiveEffectRepository`/`SqliteActiveEffectRepository` (`ADR-028` §6) with create/read-by-id/list-by-`TargetRef`/list-by-`SourceRef` primitives, `Revision`-CAS-guarded and routed through `SqliteSavingPipeline` (`ADR-028` §12). No stacking-policy resolution, no duration/expiry mechanism, and no removal command — creation and basic persistence only, per the backlog's own §15.1 boundary.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-501`'s `ADR-028` specifies the `ActiveEffect` aggregate's shape and persistence contract, but no `ActiveEffect` type or storage exists anywhere in the codebase yet. This is the first implementation task in the item-sourced abilities/effects range decomposed by `ODY-S05-111`.
- Value or risk reduction: establishes the foundation every later task in this range (`503`-`507`) depends on — stacking resolution, duration/expiry, `WhileItemEquipped` wiring, removal, and integration fixtures all need the aggregate and repository to exist first.
- Blocking or enabling relationship: unblocks `ODY-S05-503` (Stacking Policy Resolution), `ODY-S05-504` (Non-Combat Duration/Expiry), `ODY-S05-505` (WhileItemEquipped Wiring + Item-Triggered Creation), and `ODY-S05-506` (RemoveActiveEffect Command + Direct-Creation Permission Gates).

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 1.
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §5 (aggregate shape), §6 (repository contract), §12 (persistence conventions), §15 (module boundaries).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (item/`ActiveEffect` integration rules — read only, not modified here).
- `docs/adr/ADR-001` (module dependency direction).
- Existing patterns: `InventoryItemRef`/`ItemMechanicsSnapshot` (`Odyssey.Domain.Inventory`, discriminated-union and mechanics-snapshot precedent); `EquippedEntry`/`EquippedEntryRecord` (Domain/Application field-split precedent); `SqliteSceneRepository` (standalone self-owned aggregate + `SqliteSavingPipeline` structural precedent, not `SqliteInventoryRepository`); `SqliteSavingPipeline` (the shared ADR-012 single-transaction journal/event/idempotency commit pipeline, already used by `SqliteCampaignRepository`/`SqliteCharacterRepository`/`SqliteGameLogRepository`/`SqliteSceneRepository`/`SqliteInventoryRepository`'s migration-apply path).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-502`, `SLICE-05`.
- Existing test IDs: none from this aggregate family — `ActiveEffect` is a standalone aggregate, not an Inventory/Character extension, so it uses a new prefix rather than continuing `TC-INVENTORY-*`.
- New test IDs introduced: `TC-ACTIVEEFFECT-001`-`019`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-502` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main`/this branch's merge-base is `528f002`, the merge of PR #136 (`ODY-S05-111`, decomposition of `502`-`507`). Backlog row 1 in §15 (`ODY-S05-502`) was `Proposed`.
- No `ActiveEffect` type, table, or repository exists anywhere in the codebase prior to this task — confirmed by `grep` across `Packages/` and `DotNet/`.
- `InventoryItemRef` (`Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`, direct code read): `enum InventoryItemRefKind { ItemInstance=1, ItemStack=2 }`, `readonly struct InventoryItemRef` with `Kind` + conditionally-valid id fields, `ForInstance`/`ForStack` factories, `IsValid` checking exactly one id field populated — the exact discriminated-union shape `ActiveEffectSourceRef`/`ActiveEffectTargetRef` mirror, without reusing `InventoryItemRef` itself as the top-level type (per `ADR-028` §5.1/§5.2's own new-type requirement).
- `ItemMechanicsSnapshot` (same file, direct code read): `readonly struct` with `SourceDefinitionRef` (`ContentDefinitionRef`)/`DefinitionSnapshotVersion` (long ≥1)/`ContentType` (`ContentDefinitionType`)/`Payload` (non-empty string) — the exact 4-field shape `EffectMechanicsSnapshot` mirrors, reusing `ContentDefinitionRef` itself rather than inventing a new reference type.
- `EquippedEntry`/`EquippedEntryRecord` (direct code read): the Domain `EquippedEntry` carries 7 fields with no `CampaignId`; a separate Application-layer `EquippedEntryRecord(CampaignId, EquippedEntry)` wrapper adds the campaign scope. `ADR-028` §5.1 lists `CampaignId` as one of `ActiveEffect`'s own 12 fields, but this codebase's established split keeps `CampaignId` off the pure Domain type — resolved by following the `EquippedEntry`/`EquippedEntryRecord` precedent exactly: 11 fields on the Domain `ActiveEffect` class, `CampaignId` on a new Application-layer `ActiveEffectRecord(CampaignId, ActiveEffect)` wrapper (see §18).
- `SqliteSceneRepository.cs` (full file read): constructor `public SqliteSceneRepository(IWallClock clock) { _clock = clock; _pipeline = new SqliteSavingPipeline(clock); }`; `CreateScene` uses `_pipeline.Execute` with `tryReplay` querying `WHERE LastCommandId = $commandId`; plain SELECTs for reads with no pipeline; `EnsureSceneTokenTables` DDL pattern with `CREATE TABLE IF NOT EXISTS`; `OpenConnection` with `PRAGMA journal_mode=WAL; foreign_keys=ON; synchronous=FULL; busy_timeout=5000`. `SqliteActiveEffectRepository` mirrors this structure exactly — a self-owned aggregate with its own table, not an extension of `SqliteInventoryRepository`/`SqliteCharacterRepository`.
- `SqliteSavingPipeline.Execute<T>` (direct code read): `internal Result<T> Execute<T>(SqliteConnection connection, CampaignId campaignId, CommandId commandId, CorrelationId correlationId, Func<SqliteTransaction, Result<T>> tryReplay, Func<SqliteTransaction, Result<PipelineWrite<T>>> apply) where T : notnull;` — `internal sealed`, so `SqliteActiveEffectRepository` must live in `Odyssey.Persistence.Sqlite`.
- `ContentDefinitionType` enum (direct code read): includes `Effect = 7`, confirming `EffectMechanicsSnapshot.ContentType` has a real value to use in tests and production code.
- The four scope-guard files named by this task (`SqliteInventoryRepositoryTests.cs`, `SqliteEquipmentRepositoryTests.cs`, `InventoryCreationServiceTests.cs`, `EquipmentServiceTests.cs`) each independently scan either the whole `Odyssey.Persistence` assembly or the `Odyssey.Application.Inventory` namespace for the literal string `"ActiveEffect"` in a `forbidden`/`forbiddenTypeFragments` array — confirmed by direct code read at each file's own guard method before editing.
- **Discovery during implementation (real `dotnet test` failures, not merely assumed):** the first `dotnet test` run against the new `ActiveEffectRepositoryTests.cs` failed 9/10 with `System.InvalidOperationException: Must add values for the following parameters: $lastCommandId` — the INSERT statement's parameter list included `$lastCommandId` but the shared `AddParameters` helper never bound it; the binding was mistakenly left to a caller that never added it either. Fixed by binding `insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString())` directly at the one call site in `CreateActiveEffect`, immediately after `AddParameters(insert, record, now)` (see §18).
- **Discovery during implementation:** `CreateActiveEffect_WithMismatchedCampaignId_IsRejected` failed with `SqliteException: no such table: ActiveEffect` — a campaign-boundary rejection correctly returns before `EnsureActiveEffectTables` is ever called (no connection/table-creation happens for a rejected write), but the test's own `CountRows` helper queried the table unconditionally. Fixed by having `CountRows` check `sqlite_master` first and treat a missing table as zero rows, since "the table doesn't even exist yet" is itself proof no row was written (see §18).
- **Discovery during validation:** `check-repository-policy.ps1`'s `REPO-POLICY-005` requires every `ERROR_CODES.md` `TestReference` to match `TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-[0-9]{3}` (exactly three digits) and to exist verbatim in `Tests/Metadata/test-catalog.json`; the initial test IDs (`TC-ACTIVEEFFECT-1` through `-19`) were not zero-padded and did not match this codebase's own established convention (confirmed by `grep` over existing catalog entries, e.g. `TC-BOARD-001`, `TC-INVENTORY-179`). Renumbered every test annotation and `ERROR_CODES.md` reference to the zero-padded `TC-ACTIVEEFFECT-001`-`019` form (see §18).
- **Discovery during validation:** `verify-test-structure.ps1` requires every `Tests/Metadata/test-catalog.json` entry to reference an existing task contract by `taskId`; it failed until this task contract itself existed. No code implication — purely a documentation-sequencing requirement being satisfied here.

### Assumptions

- None.

## 5. Scope

### In scope

- New `Odyssey.Domain.Effects` namespace: `ActiveEffect` aggregate, `ActiveEffectId`, `ActiveEffectStatus`, `ActiveEffectSourceKind`/`ActiveEffectSourceRef`, `ActiveEffectTargetKind`/`ActiveEffectTargetRef`, `EffectMechanicsSnapshot`.
- New `Odyssey.Application.Effects` namespace: `ActiveEffectRecord` wrapper.
- New `Odyssey.Application.Persistence.IActiveEffectRepository` port (create/get/list-by-target/list-by-source).
- New `Odyssey.Persistence.Sqlite.SqliteActiveEffectRepository` implementation, new `ActiveEffect` table.
- Three new `ErrorCodes`/`PersistenceFailures`/`ERROR_CODES.md` entries: `persistence.active_effect.not_found`, `persistence.active_effect.io_failed`, `persistence.active_effect.campaign_mismatch`.
- Point narrowing of the four named scope guards (removing only the `"ActiveEffect"` string from each array).
- Tests (`TC-ACTIVEEFFECT-001`-`019`), test metadata, task/plan docs, backlog row.

### Out of scope

- Stacking-policy resolution (`ODY-S05-503`'s own territory, per `ADR-028` §7).
- Any duration/expiry mechanism (`ODY-S05-504`/`505`'s own territory, per `ADR-028` §8/§11).
- The `RemoveActiveEffect` command or any direct-creation permission gate (`ODY-S05-506`'s own territory, per `ADR-028` §9/§10).
- Any turn/round-based `EffectDurationType` value or combat-triggered effect application (the full attack pipeline's own territory, out of scope for this entire range per the backlog's §15 boundary statement).
- `docs/adr/ADR-028_...md` itself — already `Accepted`; implemented here, not amended.
- `IInventoryRepository`/`ICharacterRepository` or their implementations — never extended for `ActiveEffect`, per `ADR-028` §6's explicit standalone-repository mandate.
- ADR edits beyond what is already accepted, Unity/UI.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Effects/ActiveEffect.cs
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectRecord.cs
Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs
DotNet/Tests/Odyssey.Tests.Domain/Effects/ActiveEffectTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/SqliteEquipmentRepositoryTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/EquipmentServiceTests.cs
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-502_ActiveEffect_Foundation.md
docs/plans/active/ODY-S05-502_ActiveEffect_Foundation.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.rules/**
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: pure aggregate/value-type identity and invariants stay in `Odyssey.Domain`; the `CampaignId`-carrying wrapper and repository contract stay in `Odyssey.Application`; the SQLite implementation stays in `Odyssey.Persistence` — per `ADR-028` §15 and `ADR-001`.
- Authoritative-state and transaction boundary: `CreateActiveEffect` is the only mutation in this task; it runs entirely inside one `SqliteSavingPipeline.Execute` transaction (insert + `DomainEvents` + `AppliedCommands`), all-or-nothing.
- Serialization / compatibility boundary: no new serializer; the audit event payload is a small hand-built JSON string, matching `SqliteInventoryRepository`'s own migration-apply precedent.
- Time / RNG rule: `UtcInstant now = _clock.GetUtcNow()` via the injected `IWallClock`, matching every other repository in the file.
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency — `Microsoft.Data.Sqlite` is already referenced.
- Security / privacy / redaction rule: no raw JSON, exception text, or stack traces in any returned `Error`.
- Other: `IActiveEffectRepository` is standalone — it must not extend or be layered onto `IInventoryRepository`/`ICharacterRepository`, per `ADR-028` §6.

## 7. Expected behavior

### Scenario 1 — round trip for each of the four SourceRef kinds and each of the two constructible TargetRef kinds

**Given** a valid `ActiveEffectRecord` with an Item-sourced/Character-targeted, EquippedItem-sourced/ItemInstance-targeted, Action-sourced, or GMDirect-sourced `ActiveEffect`
**When** `CreateActiveEffect` then `GetActiveEffect` are called
**Then** the fetched record is field-equal to the original, and an Action/GMDirect source persists with no item reference at all.

### Scenario 2 — GetActiveEffect with a missing id

**Given** an `ActiveEffectId` that was never created
**When** `GetActiveEffect` is called
**Then** it fails with `persistence.active_effect.not_found`.

### Scenario 3 — cross-campaign isolation

**Given** an `ActiveEffect` created in campaign A
**When** `GetActiveEffect` is called against campaign B's own `CampaignHandle` with campaign A's `ActiveEffectId`
**Then** it fails with `persistence.active_effect.not_found` — never a cross-campaign leak, since each campaign owns its own physical database file.

### Scenario 4 — list-by-target and list-by-source exact-match filtering

**Given** several `ActiveEffect` rows with overlapping but distinct `TargetRef`/`SourceRef` values
**When** `ListActiveEffectsByTarget`/`ListActiveEffectsBySource` are called with one specific ref
**Then** only the rows matching that exact ref are returned.

### Scenario 5 — campaign-mismatched create is rejected

**Given** an `ActiveEffectRecord` whose own `CampaignId` does not match the `CampaignHandle` passed to `CreateActiveEffect`
**When** `CreateActiveEffect` is called
**Then** it fails with `persistence.active_effect.campaign_mismatch` and writes no row.

### Scenario 6 — replay idempotency

**Given** a successful `CreateActiveEffect` under `CommandId` X
**When** `CreateActiveEffect` is called again with the same `CommandId` X and the same record
**Then** it returns the same record without creating a second row.

### Required invariants

- `IActiveEffectRepository`/`SqliteActiveEffectRepository` are standalone — never an `IInventoryRepository`/`ICharacterRepository` extension.
- Only four methods exist: create, get-by-id, list-by-target, list-by-source. No stacking, expiry, or removal logic.
- The mutation (`CreateActiveEffect`) commits through `SqliteSavingPipeline`, never a bespoke transaction.
- `SceneObject` is declared in `ActiveEffectTargetKind` but has no factory method on `ActiveEffectTargetRef` — it is unreachable in practice, per `ADR-028`'s own future-extension framing.

## 8. Deliverables

- Production code: `ActiveEffect`/`ActiveEffectSourceRef`/`ActiveEffectTargetRef`/`EffectMechanicsSnapshot` (Domain), `ActiveEffectRecord` (Application), `IActiveEffectRepository` (Application), `SqliteActiveEffectRepository` (Persistence), three new error codes.
- Tests: `DotNet/Tests/Odyssey.Tests.Domain/Effects/ActiveEffectTests.cs` (`TC-ACTIVEEFFECT-011`-`019`), `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectRepositoryTests.cs` (`TC-ACTIVEEFFECT-001`-`010`).
- Scripts / CI: none.
- Configuration: none.
- Documentation: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: no migration runner step; the new table is `CREATE TABLE IF NOT EXISTS`, additive only.

## 9. Acceptance criteria

1. `ActiveEffect` matches `ADR-028` §5 exactly: 12 fields total (11 on the Domain type, `CampaignId` on the `ActiveEffectRecord` wrapper per the `EquippedEntry` precedent), `Revision`-CAS-guarded (`revision >= 1`).
2. `ActiveEffectSourceRef`/`ActiveEffectTargetRef` are genuinely new discriminated-union types (not `InventoryItemRef` reuse), following its own kind-tag + factories + `IsValid` pattern; `SceneObject` is declared in the enum but has no constructing factory.
3. `EffectMechanicsSnapshot` is a genuinely new type mirroring `ItemMechanicsSnapshot`'s own 4-field shape, reusing `ContentDefinitionRef` for `EffectDefinitionRef`.
4. `IActiveEffectRepository`/`SqliteActiveEffectRepository` are standalone — no inheritance or extension of `IInventoryRepository`/`ICharacterRepository`.
5. Exactly four methods are implemented: create, get-by-id, list-by-target, list-by-source — no stacking, expiry, or removal logic.
6. `CreateActiveEffect` commits through `SqliteSavingPipeline`, CAS via `Revision`.
7. All four named scope guards are point-narrowed (only the `"ActiveEffect"` string removed from each array; nothing else touched; no fifth guard file created).
8. Tests use the new `TC-ACTIVEEFFECT-*` prefix, not `TC-INVENTORY-*`; `Tests/Metadata/test-catalog.json` is synced.
9. `dotnet test` is green across the full solution.
10. Backlog row `ODY-S05-502` → `In Review (PR #NNN)`; PR is Draft.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-001` | .NET / NUnit (Persistence, real SQLite) | Round trip, Item source / Character target | Pass |
| `TC-ACTIVEEFFECT-002` | .NET / NUnit (Persistence, real SQLite) | Round trip, EquippedItem source / ItemInstance target | Pass |
| `TC-ACTIVEEFFECT-003` | .NET / NUnit (Persistence, real SQLite) | Round trip, Action source, no item reference persisted | Pass |
| `TC-ACTIVEEFFECT-004` | .NET / NUnit (Persistence, real SQLite) | Round trip, GMDirect source | Pass |
| `TC-ACTIVEEFFECT-005` | .NET / NUnit (Persistence, real SQLite) | Missing id returns NotFound | Pass |
| `TC-ACTIVEEFFECT-006` | .NET / NUnit (Persistence, real SQLite) | Cross-campaign id lookup returns NotFound, not a leak | Pass |
| `TC-ACTIVEEFFECT-007` | .NET / NUnit (Persistence, real SQLite) | ListActiveEffectsByTarget exact-match filtering | Pass |
| `TC-ACTIVEEFFECT-008` | .NET / NUnit (Persistence, real SQLite) | ListActiveEffectsBySource exact-match filtering | Pass |
| `TC-ACTIVEEFFECT-009` | .NET / NUnit (Persistence, real SQLite) | Campaign-mismatched create rejected, no row written | Pass |
| `TC-ACTIVEEFFECT-010` | .NET / NUnit (Persistence, real SQLite) | Replay with same CommandId is idempotent | Pass |
| `TC-ACTIVEEFFECT-011` | .NET / NUnit (Domain) | ActiveEffect constructs with valid fields | Pass |
| `TC-ACTIVEEFFECT-012` | .NET / NUnit (Domain) | Rejects mismatched EffectMechanicsSnapshot | Pass |
| `TC-ACTIVEEFFECT-013` | .NET / NUnit (Domain) | Rejects StackCount below one | Pass |
| `TC-ACTIVEEFFECT-014` | .NET / NUnit (Domain) | ForItem/ForEquippedItem require a valid InventoryItemRef | Pass |
| `TC-ACTIVEEFFECT-015` | .NET / NUnit (Domain) | ForAction/ForGMDirect carry no item reference and are valid | Pass |
| `TC-ACTIVEEFFECT-016` | .NET / NUnit (Domain) | ForCharacter/ForItemInstance require their own id | Pass |
| `TC-ACTIVEEFFECT-017` | .NET / NUnit (Domain) | SceneObject declared but unconstructable | Pass |
| `TC-ACTIVEEFFECT-018` | .NET / NUnit (Domain) | EffectMechanicsSnapshot constructs with valid fields | Pass |
| `TC-ACTIVEEFFECT-019` | .NET / NUnit (Domain) | EffectMechanicsSnapshot rejects empty Payload | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no ADR edits, no `IInventoryRepository`/`ICharacterRepository` change, no stacking/expiry/removal logic.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any stacking-policy, expiry, or removal test, since none of that logic exists here.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — one new aggregate, one new port, one new table.
- Version fields affected: no manifest/application/schema version bumped.
- Migration or upcaster: none; `CREATE TABLE IF NOT EXISTS` only.
- Forward / backward behavior: older builds never reference the new type/table; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: none — purely additive, no existing table or column is touched.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory/character test records; no real player data.
- Trust boundaries: `campaign.CampaignId` vs. `record.CampaignId` is checked on every call; a mismatch is rejected before any I/O beyond opening the connection.
- Authorization / audience checks: none introduced by this task — `ActiveEffect` creation's own permission model is `ODY-S05-505`/`506`'s territory.
- Redaction requirements: no raw exception text or stack trace in any returned `Error`.
- Log-safe fields: `ActiveEffectId`/`EffectDefinitionRef`/`CampaignId` only in the audit event payload — no `Payload` (mechanics JSON) content.
- Abuse / malformed input limits: every DTO constructor validates its own invariants and fails fast.
- Security tests: `TC-ACTIVEEFFECT-006` (cross-campaign isolation), `TC-ACTIVEEFFECT-009` (campaign-mismatch rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new aggregate, a new standalone persistence contract, and a new SQL table — multiple `PLANS.md` §1.2 triggers.
- ExecPlan path: `docs/plans/active/ODY-S05-502_ActiveEffect_Foundation.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #136 (`ODY-S05-111`) and precede `ODY-S05-503`-`507`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs (`ADR-027`, `ADR-028`); `IInventoryRepository`/`ICharacterRepository` and their implementations.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one table and one new port; no manifest/schema version bump or protocol/ruleset change.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

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
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Effects/ActiveEffect.cs` — new file: `ActiveEffect` aggregate, `ActiveEffectId`, `ActiveEffectStatus`, `ActiveEffectSourceKind`/`ActiveEffectSourceRef`, `ActiveEffectTargetKind`/`ActiveEffectTargetRef`, `EffectMechanicsSnapshot`.
- `Packages/com.odyssey.application/Runtime/Effects/ActiveEffectRecord.cs` — new file: `ActiveEffectRecord` wrapper.
- `Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs` — new file: `IActiveEffectRepository`.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` — three new `PersistenceFailures` factory methods.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — three new error codes.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs` — new file: full implementation, new `ActiveEffect` table.
- `DotNet/Tests/Odyssey.Tests.Domain/Effects/ActiveEffectTests.cs` — new file, `TC-ACTIVEEFFECT-011`-`019`.
- `DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectRepositoryTests.cs` — new file, `TC-ACTIVEEFFECT-001`-`010`.
- `DotNet/Tests/Odyssey.Tests.Persistence/SqliteInventoryRepositoryTests.cs`, `SqliteEquipmentRepositoryTests.cs`, `InventoryCreationServiceTests.cs`, `EquipmentServiceTests.cs` — point removal of `"ActiveEffect"` from each scope-guard array.
- `docs/errors/ERROR_CODES.md` — three new rows.
- `Tests/Metadata/test-catalog.json` — `TC-ACTIVEEFFECT-001`-`019` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-502` updated to `In Review`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90 (80 predecessor + 10 new), Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 544/544 (534 predecessor + 10 new). |
| `.\scripts\verify-format.ps1` | PASS | `dotnet format --verify-no-changes` exit 0. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` (three new `ERROR_CODES.md` rows accepted after zero-padding TC references.) |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0, after this task contract was written. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (12-field aggregate, Revision-CAS) | Met | `TC-ACTIVEEFFECT-011`; code review of `ActiveEffect`/`ActiveEffectRecord`. |
| AC-2 (new SourceRef/TargetRef, SceneObject unconstructable) | Met | `TC-ACTIVEEFFECT-014`/`015`/`016`/`017`. |
| AC-3 (new EffectMechanicsSnapshot) | Met | `TC-ACTIVEEFFECT-018`/`019`. |
| AC-4 (standalone repository) | Met | Code review of `IActiveEffectRepository`/`SqliteActiveEffectRepository`. |
| AC-5 (exactly four methods, no extra logic) | Met | Code review. |
| AC-6 (SqliteSavingPipeline, Revision CAS) | Met | `TC-ACTIVEEFFECT-010`; code review of `CreateActiveEffect`. |
| AC-7 (four scope guards point-narrowed) | Met | Diff review of all four guard files. |
| AC-8 (TC-ACTIVEEFFECT-* prefix, catalog synced) | Met | Table above; `Tests/Metadata/test-catalog.json` diff. |
| AC-9 (dotnet test green) | Met | Table above. |
| AC-10 (backlog In Review, Draft PR) | Met | Backlog row updated; PR to be opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- No stacking, expiry, or removal behavior exists yet — by design, deferred to `ODY-S05-503`/`504`/`505`/`506`.
- `ActiveEffectTargetKind.SceneObject` remains unreachable until a future task introduces a `SceneObject` domain type and a corresponding factory.

### Follow-up tasks

- `ODY-S05-503` — Stacking Policy Resolution.
- `ODY-S05-504` — Non-Combat Duration/Expiry.
- `ODY-S05-505` — WhileItemEquipped Wiring + Item-Triggered Creation.
- `ODY-S05-506` — RemoveActiveEffect Command + Direct-Creation Permission Gates.

### Self-review summary

- Scope review: diff touches only allowed paths; no ADR, `IInventoryRepository`/`ICharacterRepository` change.
- Architecture review: reuses `SqliteSavingPipeline` (already shared by 5 other repositories/paths) via the `SqliteSceneRepository` structural precedent, not `SqliteInventoryRepository`; `ActiveEffectSourceRef`/`ActiveEffectTargetRef`/`EffectMechanicsSnapshot` are genuinely new types following existing precedents, not reused or renamed existing types.
- Test review: all scenarios in §7 covered by real-domain and real-SQLite tests; two genuine implementation defects (missing `$lastCommandId` binding, a test-helper querying a not-yet-created table) were found and fixed by running the tests, not merely by code review.
- Security/privacy review: campaign-boundary check is enforced on every call; no payload leakage in errors or logs.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog all updated; no schema/manifest/protocol version bump required.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Design decision: `CampaignId` lives only on the new Application-layer `ActiveEffectRecord(CampaignId, ActiveEffect)` wrapper, not on the Domain `ActiveEffect` class itself, even though `ADR-028` §5.1 lists it as one of `ActiveEffect`'s own 12 fields.** This follows the `EquippedEntry`/`EquippedEntryRecord` split already established in this codebase (and consistent with `ADR-028` §15's own "Domain owns pure identity/value invariants" framing) rather than adding a Domain-layer campaign dependency `EquippedEntry` deliberately avoids. Authority: direct code read of `EquippedEntry`/`EquippedEntryRecord`; `ADR-028` §15.
- 2026-09-12 — **Design decision: `ActiveEffect`/`ActiveEffectRecord` live in new dedicated namespaces, `Odyssey.Domain.Effects`/`Odyssey.Application.Effects`, rather than the ТЗ's own suggested `Odyssey.Domain.Content`/`Odyssey.Application.Content` path.** Direct code read confirmed `Odyssey.Application.Content` is exclusively used for catalog/authoring concerns (`CatalogValidationContracts.cs`, `ContentCatalogAuthoringContracts.cs`, `ContentCatalogLifecycleContracts.cs`, `TypedDefinitionCodec.cs`); placing a runtime aggregate there would blur that boundary. New dedicated namespaces mirror how `Odyssey.Domain.Inventory`/`Odyssey.Application.Inventory` are dedicated to their own aggregate family. Authority: direct code read of `Odyssey.Application.Content`'s existing members; `ADR-001`'s module-boundary framing.
- 2026-09-12 — **Design decision: `ActiveEffectSourceRef`/`ActiveEffectTargetRef` are brand-new discriminated-union structs, reusing `InventoryItemRef` only internally (as the payload for the Item/EquippedItem source kinds), never as the top-level type itself.** This matches `ADR-028` §5.1/§5.2's own explicit requirement for new types following the `InventoryItemRef` pattern, not a direct reuse. Authority: `ADR-028` §5.1/§5.2; direct code read of `InventoryItemRef`'s own shape.
- 2026-09-12 — **Design decision: `ActiveEffectTargetKind.SceneObject` is declared in the enum with an explanatory doc comment but has no `ForSceneObject(...)` factory on `ActiveEffectTargetRef`.** No `SceneObject` domain type exists to accept as a parameter; stubbing a placeholder id type would misrepresent the enum value as usable today. This mirrors the `BuiltInAbilityRefs`/`BuiltInEffectRefs` "integration point for a future task" precedent named by `ODY-S05-501`. Authority: `ADR-028` §5.2's own explicit "declared but unconstructable" instruction; `ODY-S05-501`'s own precedent citation.
- 2026-09-12 — **Design decision: a third error code, `persistence.active_effect.campaign_mismatch`, was added beyond the two the task's own architecture implied were minimally sufficient (`not_found`/`io_failed`).** `TryValidateCampaignBoundary`'s own rejection (mirroring `SqliteInventoryRepository`'s precedent) is a `Validation`/`InvalidRequest` scenario, not an I/O failure, and reusing `io_failed` for it would miscategorize the error. Matches `persistence.inventory.campaign_mismatch`'s own exact precedent shape. Authority: direct code read of `PersistenceFailures.InventoryCampaignMismatch`'s own category/reason/retry conventions.
- 2026-09-12 — **Bug found and fixed during test execution: the `CreateActiveEffect` INSERT statement referenced `$lastCommandId` in its column-value list, but neither `AddParameters` nor its caller ever bound that parameter, causing every real insert to throw `System.InvalidOperationException: Must add values for the following parameters: $lastCommandId`.** Fixed by binding `insert.Parameters.AddWithValue("$lastCommandId", commandId.ToString())` at the call site in `CreateActiveEffect`, immediately after `AddParameters(insert, record, now)` — matching `AddParameters`'s own doc comment, which already stated (but the code did not yet implement) that the trailing parameter is bound by the caller separately. Authority: real `dotnet test` failure output (9/10 `ActiveEffectRepositoryTests` failures before the fix, 10/10 passing after).
- 2026-09-12 — **Bug found and fixed in the test file itself: `CreateActiveEffect_WithMismatchedCampaignId_IsRejected`'s own `CountRows` helper queried the `ActiveEffect` table unconditionally, but a campaign-boundary rejection correctly returns before `EnsureActiveEffectTables` is ever called (no connection/table-creation happens for a rejected write) — so the table may not exist yet, and the raw query threw `SqliteException: no such table: ActiveEffect`.** Fixed by having `CountRows` first check `sqlite_master` for the table's existence and return 0 if absent, since a not-yet-created table is itself proof no row was written — the exact property the test asserts. Authority: real `dotnet test` failure output; the test's own stated assertion intent ("a rejected create must not write any row").
- 2026-09-12 — **Decision: all `TC-ACTIVEEFFECT-*` test IDs were renumbered from an initial unpadded form (`-1` through `-19`) to the zero-padded `-001` through `-019` form, matching this codebase's own established convention (confirmed via `grep` over `Tests/Metadata/test-catalog.json`, e.g. `TC-BOARD-001`, `TC-INVENTORY-179`) and required by `check-repository-policy.ps1`'s own `REPO-POLICY-005` regex (`TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-[0-9]{3}`, exactly three digits).** Every test-method comment, `ERROR_CODES.md` `TestReference` cell, and `Tests/Metadata/test-catalog.json` entry was updated consistently. Authority: real `check-repository-policy.ps1` failure output; direct inspection of the existing catalog's own ID convention.
