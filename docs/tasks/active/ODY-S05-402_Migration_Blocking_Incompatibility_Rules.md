# ODY-S05-402 — Migration Blocking Incompatibility Rules

**Status:** In Progress
**Roadmap stage / slice:** SLICE-05
**Owner:** Codex
**Requested by:** Product owner
**Branch:** codex/ody-s05-402-migration-blocking
**Pull request:** Not opened
**ExecPlan:** docs/plans/active/ODY-S05-402_Migration_Blocking_Incompatibility_Rules.md
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Compute a separate blocking-issue report from an existing item migration preview for equipment slot occupancy and individual stack capacity.

## 2. Why this task exists

ODY-S05-403 needs the supported incompatibility checks before confirmation; ODY-S05-401 already builds the preview.

## 3. Authorities and requirement references

AGENTS.md; PLANS.md; Active Documentation Baseline v2.2; Technical Development Baseline v0.5; ADR-001; ADR-003 v1.1; ADR-004; ADR-006; ADR-027 sections 10, 12, 14; SLICE-05_IMPLEMENTATION_BACKLOG.md sections 13 and 13.1.
Requirement reference: ADR-027 section 10. Existing tests: TC-INVENTORY-157 through 168. New tests: TC-INVENTORY-169 through 178. Task-safe context: owner-approved limited coverage, summarized here without private excerpts.

## 4. Verified current state

Base origin/main is 4ecf6a6 (merged ODY-S05-401). BuildPreview exists. EquippedEntry stores the occupied slot. Stack members retain quantity. TypedDefinitionCodec decodes all four item shapes. RuntimeState is opaque; creation writes an empty object. No typed loaded-ammo, runtime armor damage, custom-state interpreter, or hidden-mechanics comparison representation exists. IInventoryRepository provides GetEquippedEntry and ListEquippedEntries, not a slot query. No assumptions.

## 5. Scope

Allowed paths: Packages/com.odyssey.application/Runtime/Inventory/ItemDefinitionMigrationRules.cs; DotNet/Tests/Odyssey.Tests.Persistence/ItemDefinitionMigrationRulesTests.cs; DotNet/Tests/Odyssey.Tests.Persistence/InventoryCreationServiceTests.cs; Tests/Metadata/test-catalog.json; docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md; this contract and corresponding active plan.
Out of scope: changes to ItemDefinitionMigrationPreview, repository interfaces/SQLite, backup/apply, ADRs, UI, dependencies, and attack mechanics. Other paths require a new scope decision.

## 6. Technical constraints

Pure Application calculation with no I/O or mutation. Reuse existing typed codecs; no new serialization, persisted format, clocks, RNG, dependencies, or Unity configuration. Invalid caller inputs fail as programmer preconditions. Reports are host-side data requiring audience filtering before UI/transport/logging.

## 7. Expected behavior

An affected equipped instance blocks if the decoded target has no Armor slot or its slot differs ordinally. An unequipped instance does not block. Each stack member exceeding a non-null target MaxStackSize gets one issue. Equality, smaller quantities, null capacity, and empty previews do not block. Definition durability flags alone do not imply runtime damage.

## 8. Deliverables

Three public report/issue types and ComputeBlockingIssues in the existing file; tests and metadata; exact guard allow-list additions; task, plan and backlog update. No scripts/configuration/migration artifacts.

## 9. Acceptance criteria

1. Both supported rules produce typed issues identifying the exact inventory/item.
2. Existing preview remains unchanged and calculation performs no I/O.
3. Four missing runtime checks and future extension are explicit in code and decisions.
4. All seven requested scenarios plus caller-precondition and report tests pass and are registered.
5. Guard forbidden lists remain unchanged.
6. Required available checks pass; missing checks are reported honestly.
7. Draft PR and backlog In Review link are provided; no merge.

## 10. Tests and validation

TC-INVENTORY-169 through 178: changed/absent slot, preserved slot, unequipped, excess capacity for all four shapes, inclusive/null capacity, empty preview, deliberately deferred runtime checks, malformed target, target/equipment mismatch, immutable report.
Commands: scripts/test-fast.ps1 (full dotnet solution), scripts/verify-format.ps1, scripts/verify-repository.ps1, scripts/verify-docs.ps1 if present. Manual: complete diff and unchanged preview/forbidden arrays. Windows x64/.NET. No new codec or Player-specific behavior; Unity/IL2CPP execution not part of this pure calculation check. verify-docs.ps1 and test-all.ps1 are absent at base.

## 11. Compatibility, migration, and rollback

Additive public API only. No version fields or persisted data change. Revert the task commit to roll back code; no data recovery needed.

## 12. Dependencies and licensing

None added or changed.

## 13. Security, privacy, and hidden information

Descriptions are fixed strings without payload interpolation. Record references stay host-side; callers must filter before delivery. No logging, transport or permissions changes.

## 14. Planning and execution mode

ExecPlan required for public types and migration-related multi-stage work. One Draft PR; plan at the path above.

## 15. Documentation and versioning impact

Only task, plan and the 402 backlog row change. ADRs and all version fields remain unchanged.

## 16. Definition of Done

- [x] Implementation and registered tests complete.
- [x] Required checks and scope review recorded.
- [ ] Draft PR ready for owner review; no merge.
- [ ] Owner review complete.

## 17. Completion evidence

Implementation complete. verify-format.ps1 and verify-repository.ps1 passed; test-fast.ps1 passed: 806 tests, zero failed/skipped (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 520); build had zero warnings/errors. TRX evidence: Logs/ODY-S00-008/dotnet/. Preview class comparison against origin/main and git diff --check passed. Only the three exact guard type names were allowed; forbidden arrays and persistence remain unchanged. verify-docs.ps1 and test-all.ps1 are missing and were not run. Unity/IL2CPP not run; existing owner local Unity merge gate remains. Initial sandbox SDK/Git access errors and initial NUnit overload compilation errors are recorded in the plan; corrected before final validation.

## 18. Blockers, decisions, and change control

2026-09-12 — Owner-approved scope implements only slot occupancy and stack capacity. Loaded ammo and armor runtime damage await the future attack pipeline; custom state and hidden mechanics await specified runtime representations. Extend ComputeBlockingIssues when those exist; ODY-S05-403/404 must not interpret an empty report as proof of all six checks. No future attack task ID is reserved in the backlog; record that work through a future scoped contract rather than inventing an ID.
2026-09-12 — Caller collects found EquippedEntryRecord values by GetEquippedEntry for each affected instance's InventoryItemRef.ForInstance; calculation does no I/O. InventoryId is checked separately because ForInstance accepts only the instance ID.
2026-09-12 — Separate report preserves the accepted preview API. Decode target by its own type; malformed payload is a caller precondition failure, including malformed Armor. A valid non-Armor target has no slot. Null MaxStackSize produces no capacity finding per owner scope, even though the current domain permits null only on non-stackable definitions.
2026-09-12 — Use isolated worktree to preserve unrelated existing work. Missing verify-docs/test-all scripts will be reported, not created outside scope.
