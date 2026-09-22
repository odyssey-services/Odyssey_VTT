# ODY-S05-502 — ActiveEffect Foundation

**Status:** Done (PR #137, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-502-activeeffect-foundation`
**Pull request:** [odyssey-services/Odyssey_VTT#137](https://github.com/odyssey-services/Odyssey_VTT/pull/137) (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Give the item-sourced abilities/effects block its first real production type: the `ActiveEffect` aggregate (`ADR-028` §5) and a standalone `IActiveEffectRepository`/`SqliteActiveEffectRepository` (`ADR-028` §6) that can create and read `ActiveEffect` records, list them by target or by source, all `Revision`-CAS-guarded and committed through the shared `SqliteSavingPipeline`. After this task, nothing in the game can yet stack, expire, or remove an effect — but an `ActiveEffect` can exist, persist, and be found.

## 2. Task contract

- Goal: `ActiveEffect` domain aggregate + `EffectMechanicsSnapshot`/`ActiveEffectSourceRef`/`ActiveEffectTargetRef` (Domain), `ActiveEffectRecord` (Application), `IActiveEffectRepository`/`SqliteActiveEffectRepository` (Application/Persistence) with create/get/list-by-target/list-by-source only.
- Acceptance criteria: see task contract §9 (10 items) — 12-field aggregate matching `ADR-028` §5 exactly, genuinely new discriminated-union ref types, genuinely new mechanics-snapshot type, standalone repository, exactly four methods, `SqliteSavingPipeline` commit with `Revision` CAS, four scope guards point-narrowed, `TC-ACTIVEEFFECT-*` prefix, green tests, backlog updated.
- Requirement IDs: `ODY-S05-502`, `SLICE-05`.
- In scope: the aggregate, its two ref types, its mechanics-snapshot type, the standalone repository contract + SQLite implementation, three new error codes, four scope-guard point edits, tests/metadata/docs/backlog.
- Out of scope: stacking (`503`), expiry (`504`), `WhileItemEquipped`/item-triggered creation (`505`), removal/direct-creation gates (`506`), any turn/round-based `EffectDurationType`, any `IInventoryRepository`/`ICharacterRepository` change.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` §15/§15.1 row 1; `ADR-028` §5/§6/§12/§15; `InventoryItemRef`/`ItemMechanicsSnapshot` (ref/snapshot precedent); `EquippedEntry`/`EquippedEntryRecord` (Domain/Application split precedent); `SqliteSceneRepository` (standalone-aggregate structural precedent); `SqliteSavingPipeline` (shared commit pipeline).
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- Branch `feat/ody-s05-502-activeeffect-foundation` was created fresh off `origin/main` at `528f002` (merge of PR #136, `ODY-S05-111`). Backlog row 1 in §15 (`ODY-S05-502`) was `Proposed`.
- No `ActiveEffect` type, table, or repository existed anywhere in the codebase before this task, confirmed by `grep`.
- All required precedents were read in full before writing any code: `InventoryItemRef`/`ItemMechanicsSnapshot` (`InventoryRuntime.cs`), `ContentDefinitionRef` (`ContentCatalog.cs`), the full `SqliteSceneRepository.cs`, `SqliteSavingPipeline.Execute<T>`/`PipelineWrite<T>` signatures, `EquippedEntry`/`EquippedEntryRecord`, `PersistenceFailures`/`ErrorCodes` conventions for Scene/Token/Inventory-campaign-mismatch, `ContentDefinitionType` (confirmed `Effect=7`), and all four named scope-guard files' exact surrounding context.
- Two genuine implementation defects were found only by running the tests, not by code review alone (both recorded in the task contract §18): (1) the `CreateActiveEffect` INSERT statement referenced `$lastCommandId` but nothing ever bound it, causing every real create to throw; (2) the campaign-mismatch test's own `CountRows` helper queried the `ActiveEffect` table unconditionally, but a rejected create never creates that table, so the raw query threw `no such table`.
- A documentation-sequencing gap was found by `verify-test-structure.ps1`: it requires every test-catalog entry's `taskId` to resolve to an existing task contract file, so this task contract had to exist before the script could pass — expected, and satisfied by this same work item.
- A test-ID formatting gap was found by `check-repository-policy.ps1`'s `REPO-POLICY-005`: `ERROR_CODES.md` `TestReference` cells must match a zero-padded three-digit `TC-*-NNN` pattern that also exists verbatim in the catalog. The initial `TC-ACTIVEEFFECT-1`..`-19` numbering did not match this codebase's own established convention and was renumbered to `-001`..`-019` throughout.

Assumptions: none.

## 4. Proposed approach

- Aggregate shape: 11 of `ADR-028` §5's 12 fields sit on the Domain `ActiveEffect` class (`ActiveEffectId`, `EffectDefinitionRef`, `EffectMechanicsSnapshot`, `SourceRef`, `TargetRef`, `Status`, `StackCount`, `AppliedByUserId`, `AppliedAt`, `ExpiresAt`, `Revision`); the 12th, `CampaignId`, sits only on the new Application-layer `ActiveEffectRecord(CampaignId, ActiveEffect)` wrapper — the exact `EquippedEntry`/`EquippedEntryRecord` split already established in this codebase.
- Ref types: `ActiveEffectSourceRef` (`Kind` ∈ `{Item, EquippedItem, Action, GMDirect}` + an `InventoryItemRef` payload valid only for `Item`/`EquippedItem`) and `ActiveEffectTargetRef` (`Kind` ∈ `{Character, ItemInstance, SceneObject}` + `CharacterId`/`ItemInstanceId`, with `SceneObject` declared but never constructible) are brand-new discriminated-union `readonly struct`s, built by direct structural analogy to `InventoryItemRef` (kind tag + factories + `IsValid`), never a reuse of `InventoryItemRef` as the top-level type.
- Mechanics snapshot: `EffectMechanicsSnapshot` is a new `readonly struct` with the same 4-field shape as `ItemMechanicsSnapshot` (`SourceDefinitionRef`/`DefinitionSnapshotVersion`/`ContentType`/`Payload`), reusing `ContentDefinitionRef` itself for the definition reference rather than inventing a new ref type.
- Repository: `IActiveEffectRepository` (Application) declares four methods only — `CreateActiveEffect`, `GetActiveEffect`, `ListActiveEffectsByTarget`, `ListActiveEffectsBySource` — all `CampaignHandle`-first, `CorrelationId`-last, `Result<T>`-returning, `CommandId`-keyed for the one mutation. `SqliteActiveEffectRepository` (Persistence) mirrors `SqliteSceneRepository`'s own structure exactly: a constructor building its own `SqliteSavingPipeline`, `EnsureActiveEffectTables` DDL run at the top of every method, plain SELECTs for the three read methods, and `CreateActiveEffect` routed through `_pipeline.Execute` with a `tryReplay` querying `WHERE LastCommandId = $commandId` and an `apply` callback doing the INSERT.
- Table: a single `ActiveEffect` table with one column per Domain/Record field plus `UpdatedAt`/`LastCommandId`, and the `SourceItemRefKind`/`SourceItemRefId` / `TargetCharacterId`/`TargetItemInstanceId` nullable-column pattern already established by `SqliteInventoryRepository`'s own `EquippedEntry.ItemRefId`/`ItemRefKind` columns for a conditional discriminated-union payload. Two indexes support the two list queries.
- Error codes: `persistence.active_effect.not_found`/`io_failed` mirror `persistence.scene.*`'s exact convention; a third, `persistence.active_effect.campaign_mismatch`, was added (beyond the two initially assumed sufficient) once implementation showed `TryValidateCampaignBoundary`'s own rejection is a `Validation`/`InvalidRequest` case, not an I/O failure — mirroring `persistence.inventory.campaign_mismatch` exactly.
- Scope guards: point removal of the literal `"ActiveEffect"` string from each of the four named guard arrays (`SqliteInventoryRepositoryTests.cs`, `SqliteEquipmentRepositoryTests.cs`, `InventoryCreationServiceTests.cs`'s shared `AssertForbiddenFragments` array, `EquipmentServiceTests.cs`), each with an explanatory comment; no fifth guard file created for `ActiveEffect` itself, per the task's own explicit instruction.
- Tests: `ActiveEffectTests.cs` (Domain, `TC-ACTIVEEFFECT-011`-`019`) covers the aggregate's and value types' own construction/validation invariants with no persistence. `ActiveEffectRepositoryTests.cs` (Persistence, `TC-ACTIVEEFFECT-001`-`010`) covers real-SQLite round trips for every source/target kind combination reachable through the four constructible ref kinds, not-found, cross-campaign isolation, list filtering, campaign-mismatch rejection, and replay idempotency.

No change to `IInventoryRepository`/`ICharacterRepository` or their implementations, no stacking/expiry/removal logic, no turn/round-based `EffectDurationType` handling.

## 5. Milestones

### M1 — Aggregate, record, repository contract, and SQLite implementation

- [x] Read all required precedents in full (`InventoryItemRef`/`ItemMechanicsSnapshot`, `ContentDefinitionRef`, `SqliteSceneRepository`, `SqliteSavingPipeline`/`PipelineWrite<T>`, `EquippedEntry`/`EquippedEntryRecord`, `PersistenceFailures` conventions, `ContentDefinitionType`, all four scope-guard files).
- [x] Write `ActiveEffect.cs` (Domain): `ActiveEffectId`, `ActiveEffectStatus`, `ActiveEffectSourceKind`/`ActiveEffectSourceRef`, `ActiveEffectTargetKind`/`ActiveEffectTargetRef`, `EffectMechanicsSnapshot`, `ActiveEffect`.
- [x] Write `ActiveEffectRecord.cs` (Application).
- [x] Write `ActiveEffectRepositoryContracts.cs` (Application): `IActiveEffectRepository`.
- [x] Add three new error codes to `ErrorCodes.cs`, their `PersistenceFailures` factories to `CampaignRepositoryContracts.cs`, and their rows to `ERROR_CODES.md`.
- [x] Write `SqliteActiveEffectRepository.cs` (Persistence): full implementation, table DDL, indexes.
- [x] Point-narrow all four scope guards.
- [x] Build the solution — succeeded on the first attempt, 0 warnings, 0 errors.

### M2 — Tests

- [x] Write `ActiveEffectTests.cs` (Domain, `TC-ACTIVEEFFECT-011`-`019`); fix 7 `Assert.Throws<T>` CS0121 ambiguity errors by extracting each lambda into a named `Action` variable (the established fix pattern in this codebase's NUnit version); 10/10 pass.
- [x] Write `ActiveEffectRepositoryTests.cs` (Persistence, `TC-ACTIVEEFFECT-001`-`010`); first run: 9/10 failed with `Must add values for the following parameters: $lastCommandId`. Diagnosed and fixed the missing parameter binding in `CreateActiveEffect`.
- [x] Second run surfaced one more failure (`CreateActiveEffect_WithMismatchedCampaignId_IsRejected`, `no such table: ActiveEffect`); fixed the test's own `CountRows` helper to treat a missing table as zero rows.
- [x] Third run: 10/10 pass.
- [x] Run the full solution test suite; confirm no regressions.

### M3 — Metadata, docs, validation, PR

- [x] Register `TC-ACTIVEEFFECT-001`-`019` in `Tests/Metadata/test-catalog.json`.
- [x] Run `.\scripts\verify-format.ps1` — pass.
- [x] Run `.\scripts\check-repository-policy.ps1` — first run failed `REPO-POLICY-005` (unpadded test-reference IDs); renumbered all `TC-ACTIVEEFFECT-*` references to zero-padded three-digit form across the two test files and `ERROR_CODES.md`; second run passed.
- [x] Run `.\scripts\verify-test-structure.ps1` — first run failed (`TC-ACTIVEEFFECT-001` references missing task contract `ODY-S05-502`); this task contract's own existence resolves it.
- [x] Write the task contract and this ExecPlan to full depth.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 1 in §15 to `In Review`.
- [x] Review `git diff --name-status` for scope — every changed file matches the task contract's allowed-paths list exactly.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog status.

## 6. Progress log

- 2026-09-12 — Created worktree `D:\ody_s05_502_wt`, branch `feat/ody-s05-502-activeeffect-foundation`, off `origin/main` at `528f002` (merge of PR #136).
- 2026-09-12 — Read all required precedents by direct code inspection; confirmed no `ActiveEffect` type/table exists anywhere.
- 2026-09-12 — Wrote all production files (`ActiveEffect.cs`, `ActiveEffectRecord.cs`, `ActiveEffectRepositoryContracts.cs`, `SqliteActiveEffectRepository.cs`) and the three new error codes; `dotnet build` succeeded on the first attempt.
- 2026-09-12 — Point-narrowed all four named scope guards.
- 2026-09-12 — Wrote `ActiveEffectTests.cs` (Domain); fixed 7 `Assert.Throws<T>` CS0121 ambiguities; 10/10 pass.
- 2026-09-12 — Wrote `ActiveEffectRepositoryTests.cs` (Persistence); first run 9/10 failed on a missing `$lastCommandId` parameter binding in `CreateActiveEffect` — fixed by binding it explicitly at the call site.
- 2026-09-12 — Second run surfaced `CreateActiveEffect_WithMismatchedCampaignId_IsRejected` failing on `no such table: ActiveEffect` — fixed the test's own `CountRows` helper to treat a missing table as proof of zero rows.
- 2026-09-12 — Third run: 10/10 persistence tests pass. Full-suite `dotnet test DotNet\Odyssey.Core.sln`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 544/544 — all green, no regressions.
- 2026-09-12 — `verify-format.ps1` passed directly. `check-repository-policy.ps1` first failed `REPO-POLICY-005` on unpadded `TC-ACTIVEEFFECT-*` references; renumbered to zero-padded three-digit form in both test files, `ERROR_CODES.md`, and `Tests/Metadata/test-catalog.json`; rebuilt and re-ran — all green; second `check-repository-policy.ps1` run passed. `verify-test-structure.ps1` first failed on the missing task contract; resolved by writing it.
- 2026-09-12 — Wrote the task contract and this ExecPlan to full depth.

## 7. Decisions

See task contract §18 for the full decision log: `CampaignId`-on-wrapper-not-aggregate (mirroring `EquippedEntry`/`EquippedEntryRecord`); the new `Odyssey.Domain.Effects`/`Odyssey.Application.Effects` namespace choice (avoiding `Odyssey.Application.Content`'s catalog/authoring-only boundary); `ActiveEffectSourceRef`/`ActiveEffectTargetRef` as genuinely new types; `SceneObject`'s declared-but-unconstructable design; the third error code (`ActiveEffectCampaignMismatch`); the two real bugs found and fixed via test execution (`$lastCommandId` binding, `CountRows`' missing-table handling); and the test-ID zero-padding renumbering.

## 8. Discoveries and deviations

- The `CreateActiveEffect` INSERT statement's own `$lastCommandId` parameter was never bound anywhere — a real defect only surfaced by running the tests, not visible from a build-only pass (parameter binding mismatches are a runtime, not compile-time, SQLite error). See task contract §18.
- The campaign-mismatch test's own `CountRows` helper assumed the `ActiveEffect` table always exists by the time it runs; a rejected create (which returns before ever creating the table) proved that assumption wrong. See task contract §18.
- `check-repository-policy.ps1`'s `REPO-POLICY-005` enforces a stricter test-ID format (zero-padded three digits, cross-checked against the catalog) than this task's initial numbering used — not previously visible until the script was actually run. See task contract §18.
- `verify-test-structure.ps1` enforces that every catalog entry's `taskId` resolves to an existing task contract file — a natural sequencing dependency (the contract must exist before the script can pass), not a defect.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: PASS, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 544/544.
- `.\scripts\verify-format.ps1`: PASS.
- `.\scripts\check-repository-policy.ps1`: PASS (after the zero-padding fix; including the 3 new `ERROR_CODES.md` rows).
- `.\scripts\verify-test-structure.ps1`: PASS (after this task contract was written).
- Diff review: `git diff --name-status 528f002 HEAD` confirmed every changed file matches the task contract's allowed-paths list exactly; PR #137 opened as Draft.

## 10. Recovery and rollback

Rollback of THIS PR is a normal revert before merge. No existing data or table is touched — the new `ActiveEffect` table is additive (`CREATE TABLE IF NOT EXISTS`) and no other repository or table is modified.

## 11. Open questions and blockers

None remain open for this task. Recorded for later tasks in this range (not resolved here): `ODY-S05-503`'s own `EffectStackPolicy` implementation will read/write `StackCount` on the same aggregate this task persists; `ODY-S05-504`/`505` will add expiry/suspend-resume transitions to `Status`/`ExpiresAt`; `ODY-S05-506` will add `RemoveActiveEffect` and direct-creation permission gates.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation tasks: `ODY-S05-503` (Stacking Policy Resolution), `ODY-S05-504` (Non-Combat Duration/Expiry), `ODY-S05-505` (WhileItemEquipped Wiring + Item-Triggered Creation), `ODY-S05-506` (RemoveActiveEffect Command + Direct-Creation Permission Gates) — none started proactively; each awaits its own ТЗ.
