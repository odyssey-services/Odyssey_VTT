# ODY-S05-203 — Create Item/Stack From Published Definition

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-203-create-item-stack-from-published-definition`
**Pull request:** Not opened
**Last updated:** 2026-09-06 23:12 UTC

## 1. Purpose and user-visible outcome

Published catalog definitions can produce runtime Inventory items/stacks through a MainGM-only Application service, with exact definition-version pinning and copied mechanics snapshots.

## 2. Task contract

- Goal: add `CreateItemInstanceFromDefinition` and `CreateItemStackFromDefinition` Application flows over existing catalog and inventory repository ports.
- Acceptance criteria: MainGM-only; missing/Draft/Archived/wrong-type/invalid definitions rejected; exact `ContentDefinitionRef` pinned; mechanics snapshot copied from Published `PropertiesJson`; repository idempotency used; no movement, split/merge, equipment, attack, ActiveEffect, migration, Unity, ADR, or schema scope.
- Requirement IDs: `ODY-S05-203`, `SLICE-05`.
- In scope: Application service/request contracts, minimal errors, focused tests, metadata/docs/backlog.
- Out of scope: new persistence schema, command services beyond this creation service, authorization model expansion, move/transfer, split/merge, equip/unequip, item use, ActiveEffect, attack pipeline, ItemDefinition migration, Unity/UI, `.odcontent`, balanced content, ADR edits.
- Required authorities: `SLICE-05_IMPLEMENTATION_BACKLOG.md` row `ODY-S05-203`; `ODY-S05-201`; `ODY-S05-202`; `ADR-027` sections 4/5/6/12/14; Inventory runtime/contracts; content typed codec/validation/authoring/lifecycle contracts.
- Required validation commands: `dotnet build DotNet\Odyssey.Core.sln`; `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `origin/main` contains merge commit `8cd0bcf`, PR #114 (`ODY-S05-202`).
- Branch `feat/ody-s05-203-create-item-stack-from-published-definition` was created from `origin/main`.
- `ODY-S05-201` provides Inventory runtime records and mechanics snapshot contracts.
- `ODY-S05-202` provides SQLite-backed `IInventoryRepository` create/get/list primitives, parent Inventory existence checks, and `CommandId` idempotency.
- Existing Content Catalog Application services are static thin wrappers that check `ActorIsMainGm` before repository mutation.
- `CatalogValidationService.ValidateContentDefinition` can validate Published definitions without requiring Draft status.
- `ItemDefinition` exposes `IsStackable`/`MaxStackSize`; `AmmoDefinition` embeds `ItemDefinition`.

Assumptions: none.

## 4. Proposed approach

- Add `InventoryCreationService` in `Packages/com.odyssey.application/Runtime/Inventory`.
- Add two request classes carrying the campaign, stable runtime target id, inventory, owner/location refs, definition id, actor identity/MainGM flag, command id, correlation id, and stack quantity where needed.
- Implement one shared path: reject non-MainGM, load definition, require `Published` and `Version >= 1`, check allowed definition type, run `CatalogValidationService.ValidateContentDefinition`, build exact `ContentDefinitionRef`, copy `PropertiesJson` into `ItemMechanicsSnapshot`, create record with revision 1 and injected clock timestamps, then call `IInventoryRepository`.
- Decode only when the creation rule needs typed stackability/quantity information. Do not re-encode or transform mechanics payloads.
- Add minimal service-level error codes for denied, not-Published, unsupported type, and validation failure.
- Test with real SQLite content catalog and inventory repositories so repository idempotency and parent checks are exercised.

## 5. Milestones

### M1 — Contract and service

- [x] Add request/service/error contracts.
- [x] Build solution.

### M2 — Tests and metadata

- [x] Add `TC-INVENTORY-027` onward covering success, rejection, idempotency, and scope guards.
- [x] Register test metadata.
- [x] Run full `dotnet test`.

### M3 — Docs, validation, PR

- [x] Update task contract, ExecPlan, backlog, and error registry.
- [x] Run required repository validation scripts.
- [x] Review diff scope.
- [ ] Commit, push, open Draft PR, and record PR link.

## 6. Progress log

- 2026-09-06 22:56 UTC — Preflight: fetched `origin --prune`, verified `origin/main` contains merge commit `8cd0bcf` for PR #114, and created `feat/ody-s05-203-create-item-stack-from-published-definition` from `origin/main`.
- 2026-09-06 22:56 UTC — Read required sources: backlog row `ODY-S05-203`, `ODY-S05-201`, `ODY-S05-202`, `ADR-027` sections 4/5/6/12/14, Inventory runtime/contracts, content typed codec, validation, authoring/lifecycle, repository contracts, `TASK_TEMPLATE.md`, and `PLANS.md`.
- 2026-09-06 22:56 UTC — Created task contract and ExecPlan before production code changes.
- 2026-09-06 23:05 UTC — Added `InventoryCreationService`, request contracts, creation failures/error codes, `InventoryCreationServiceTests`, and `TC-INVENTORY-027`-`040` metadata.
- 2026-09-06 23:12 UTC — Registered the two additional stackability/max-stack tests as `TC-INVENTORY-041`-`042`.
- 2026-09-06 23:12 UTC — Diff review confirmed no Unity files, ADR edits, persistence schema changes, movement/split/merge/equipment/attack/ActiveEffect/migration implementation.
- 2026-09-06 23:05 UTC — Validation passed: `dotnet build DotNet\Odyssey.Core.sln` and `dotnet test DotNet\Odyssey.Core.sln`.
- 2026-09-06 23:12 UTC — Validation passed: `verify-format.ps1`, `check-repository-policy.ps1`, and `verify-test-structure.ps1`.

## 7. Decisions

- 2026-09-06 — Decision: keep `CommandId` replay behavior delegated to `IInventoryRepository`; the creation service adds no ledger. Rationale: `ODY-S05-202` already owns persistence idempotency. Authority: task scope and `ADR-002`.
- 2026-09-06 — Decision: use `CatalogValidationService.ValidateContentDefinition`, not `ValidateDraftForPublish`, because this flow consumes already Published definitions. Rationale: the service must reject Draft before validation and then validate the Published definition's usability. Authority: task validation section and existing validation contract.
- 2026-09-06 — Decision: decode only for type/stackability gates and copy raw `PropertiesJson` into the snapshot. Rationale: the snapshot must preserve the Published definition payload exactly. Authority: task snapshot section and `ADR-027` section 6.
- 2026-09-06 — Decision: request contracts include stable `ItemInstanceId`/`ItemStackId`. Rationale: if the service minted a new target id on every retry, the existing repository ledger would correctly reject the same `CommandId` as a different target instead of replaying it. Authority: task idempotency acceptance and `ODY-S05-202`.

## 8. Discoveries and deviations

- First full `dotnet test` failed because the `ODY-S05-201` foundation guard still rejected every `*Service.cs` under Application Inventory. It was narrowed to continue rejecting movement/transfer/split/merge/equipment/attack/ActiveEffect/ItemDefinition migration scope while allowing the explicitly authorized `InventoryCreationService`.
- The first ODY-S05-203 scope guard was too broad because the campaign database legitimately contains an existing `MigrationRecords` infrastructure table. The guard now rejects `ItemDefinitionMigration` specifically rather than any infrastructure migration table.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln`: passed with 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln`: passed. Assemblies: Contracts 1, Domain 74, Networking 67, Unit 136, Architecture 2, Persistence 386.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` and controlled invalid cases rejected.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. Runtime creates are delegated to `IInventoryRepository`, which writes the target row and ledger row transactionally. No schema or migration change is introduced by this task.

## 11. Open questions and blockers

None.

## 12. Outcome and follow-up

Not complete yet. `ODY-S05-204` remains owner of movement/transfer, and `ODY-S05-205` remains owner of split/merge.
