# ODY-S05-207 — Inventory Runtime Integration Fixtures

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-207-inventory-runtime-integration-fixtures`
**Pull request:** Draft — [#121](https://github.com/odyssey-services/Odyssey_VTT/pull/121)
**Planning mode:** Brief plan (no new public contract, no new persistence shape, no new architecture — pure integration proof over `ODY-S05-201`–`206`, mirroring `ODY-S05-106`)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Prove `ODY-S05-201`–`206` work together as one block: a Published catalog definition → a runtime `ItemStack` with a full copied snapshot → snapshot immutability across a successor definition publish → cross-inventory move → split/merge → permanent character deletion blocked while the character owns the stack. No production code — a composition proof, closing the Inventory runtime block, exactly the way `ODY-S05-106` closed the Content Catalog MVP block.

## 2. Task contract

- Goal / acceptance / scope / allowed paths: `docs/tasks/active/ODY-S05-207_Inventory_Runtime_Integration_Fixtures.md`.
- Authorities: `ODY-S05-106` task+plan (structural template), `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 7 / §7.1 / §9 / §10, `ADR-027` §4/§5/§6/§6.1/§9, `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Non-goals: any new production code, new `ErrorCode`, new table/column, Equipment/`ActiveEffect`/attack composition, Unity/UI, `docs/adr/**` edits, `ODY-S05-201`–`206` file edits, duplicating the isolated `201`–`206` tests.
- Validation: `dotnet build`; `dotnet test`; `verify-format.ps1`; `check-repository-policy.ps1`; `verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `36d00e4` (PR #116). PR #119 (`ODY-S05-205`) and PR #120 (`ODY-S05-206`) are open Draft, not merged — so this branch is `main` + a local merge of both feature branches, and this PR stacks on them (base `main`; diff collapses to the test file + metadata once they land).
- Merging #119 + #120 together surfaces one build gap: #119's fake `ThrowingInventoryRepository` does not implement #120's two new `IInventoryRepository` members — fixed with two throw-stub lines (task contract §18).
- No `ODY-S05-207` file exists in the working copy; this contract + plan are authored fresh.
- Verified no production fixture hook is reserved for this task anywhere in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7.1 or `ODY-S05-201`–`206` — this task adds only a test file + metadata.
- `SqliteContentCatalogRepository.PublishDefinition` always writes `Version = 1`; `CreateNextDraftVersionFromPublished` mints a new `ContentDefinitionId`. §6.1 is proven in the form the MVP supports (original id / `PropertiesJson` unchanged after a successor publish).

## 4. Proposed approach

- One new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/InventoryRuntimeIntegrationFixtureTests.cs`, real temp SQLite campaign, private helper methods (no new production type):
  - `PublishItem` — `ContentCatalogAuthoringService.CreateDraftDefinition` (`actorIsMainGm: true`, `RulesetCompatibility = ["ruleset.core@1.0.0"]`) + `ContentCatalogLifecycleService.PublishDefinition`, over a `TypedDefinitionCodec.EncodeItem` stackable consumable.
  - `CreateCharacter` / `CreateInventory` — through `SqliteCharacterRepository` (constructed with an `InventoryCharacterDeletionDependencyChecker`) and `SqliteInventoryRepository.CreateInventory`.
- Four tests, one coherent MainGM sequence plus focused slices:
  - `TC-INVENTORY-091`: catalog → `CreateItemStackFromDefinition` (assert full snapshot copy) → `MoveItemStack` A→B (assert stack + both inventory revision increments) → `Split` (assert quantity conservation + identical pinned snapshot/state/owner/location) → `Merge` (assert original total restored, consumed part `PersistenceItemStackNotFound`).
  - `TC-INVENTORY-092`: create a stack, branch + change + publish a successor definition, re-read the stack — snapshot still pins the original id / `PropertiesJson` (`ADR-027` §6.1).
  - `TC-INVENTORY-093`: create in A, move whole stack to B, `DeleteCharacterPermanently` blocked for B (`PersistenceCharacterDeletionHasDependent`), allowed for the emptied A.
  - `TC-INVENTORY-094`: `CreateItemStackFromDefinition` on a Draft → `InventoryCreateDefinitionNotPublished`, no `ItemStack` row.
- Register `TC-INVENTORY-091`–`094` in `test-catalog.json`; mark backlog row 7 `In Review` with the PR link.

No production code change, no Unity/UI, no new table, no new `ErrorCode`, no `ADR` change.

## 5. Milestones

### M1 — Fixture and tests
- [x] `InventoryRuntimeIntegrationFixtureTests.cs` (4 cases) — one assertion reworked (`TC-INVENTORY-092`) once the MVP's single-`Version`-1 publish behavior was confirmed by code, then all 4 passed.
- [x] `Tests/Metadata/test-catalog.json` entries `TC-INVENTORY-091`–`094`.

### M2 — Validation and review readiness
- [x] `dotnet build DotNet\Odyssey.Core.sln`.
- [x] `dotnet test DotNet\Odyssey.Core.sln`.
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 7 → `In Review (PR #121)`.
- [x] Commit, push, open Draft PR (#121).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-11 — Branched from `origin/main` `36d00e4`; merged `origin/feat/ody-s05-205-…` and `origin/feat/ody-s05-206-…` locally (two conflicts — `test-catalog.json` and `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 5/6 — resolved by keeping both sides); fixed the #119↔#120 fake-repo build gap with two throw-stub lines.
- 2026-09-11 — Read `ODY-S05-106` task+plan and `MinimalTestCatalogFixtureTests.cs` as the structural template; read `ADR-027` §4/§5/§6/§9, `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 7 / §7.1, and the Inventory/Catalog/Character public service surfaces; confirmed no reserved fixture hook.
- 2026-09-11 — Wrote `InventoryRuntimeIntegrationFixtureTests.cs`; first run failed one assertion (`Version == 2` after a successor publish) — code confirmed the MVP always publishes `Version = 1` and `CreateNextDraftVersionFromPublished` mints a new id, so `TC-INVENTORY-092` was reworked to assert the original id / `PropertiesJson` survives; all 4 pass.
- 2026-09-11 — Registered `TC-INVENTORY-091`–`094`; authored this Brief plan and the task contract; ran all five validation commands (§9); marked backlog row 7; opened Draft PR #121.

## 7. Decisions

- 2026-09-11 — Location `Integration/InventoryRuntimeIntegrationFixtureTests.cs` (existing `Integration/` folder), not the domain-named `Content/` pattern, since this is a cross-boundary integration proof. Authority: ТЗ §2 item 8.
- 2026-09-11 — `TC-INVENTORY-092` proves `ADR-027` §6.1 in the MVP-supported form (original id / properties unchanged after a successor publish); a true multi-version-of-one-id test is a follow-up for real definition versioning. Authority: code inspection.
- 2026-09-11 — Fixture is plain private C# helpers, not a JSON asset / new production factory — same as `ODY-S05-106`.

## 8. Discoveries and deviations

- The MVP has no "version 2 of the same `ContentDefinitionId`": `PublishDefinition` hard-codes `Version = 1`; `CreateNextDraftVersionFromPublished` copies the row under a new id. `TC-INVENTORY-092` was written to the behavior that actually exists.
- Stacking PR #119 + PR #120 locally is required (split/merge and the deletion checker both need to be present for the end-to-end proof). The one build gap it exposes (`ThrowingInventoryRepository` missing #120's members) is a pure merge artifact; two throw-stub lines fix it, and the same fix is owed to whichever of #119/#120 merges into `main` second. Recorded in the task contract §18.
- `test-catalog.json` and `SLICE-05_IMPLEMENTATION_BACKLOG.md` rows 5/6 conflicted in the local merge (both #119 and #120 append/edit near the same lines) — resolved by keeping both sides (`TC-INVENTORY-061`–`078` then `079`–`090`; row 5 = PR #119, row 6 = PR #120).

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln` — passed, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln` — passed, 713 total, 0 failed (`Odyssey.Tests.Persistence` carries the 4 new `TC-INVENTORY-091`–`094`, plus PR #119's 18 and PR #120's 12 stacked).
- `.\scripts\verify-format.ps1` — `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1` — `REPO-POLICY-001`–`005 PASS`, `Repository policy check passed.`
- `.\scripts\verify-test-structure.ps1` — `TC-ARCH-001` / `TC-ARCH-002 PASS`; `TC-INVENTORY-091`–`094` resolve to `ODY-S05-207`.

## 10. Recovery and rollback

Normal revert of the branch/PR — test-only change, no production code, no schema, nothing to migrate back.

## 11. Open questions and blockers

- None. Sequencing only: merge after PR #119 and PR #120.

## 12. Outcome and follow-up

Draft PR [#121](https://github.com/odyssey-services/Odyssey_VTT/pull/121). Closes the Inventory runtime block (`ODY-S05-201`–`207`). Reserved future `SLICE-05` blocks (Equipment runtime, item-sourced abilities/effects runtime, full attack pipeline) remain named, not decomposed, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8.
