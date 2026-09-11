# ODY-S05-306 — Equipment Runtime Integration Fixtures

**Status:** In Review
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-306-equipment-runtime-integration-fixtures`
**Pull request:** [#128](https://github.com/odyssey-services/Odyssey_VTT/pull/128)
**Planning mode:** Brief plan (no new public contract, no new persistence shape, no new architecture — pure integration proof over `ODY-S05-301`–`305`, mirroring `ODY-S05-207`)
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Prove `ODY-S05-301`–`305` work together as one block: a Published Armor/Weapon catalog definition → a runtime `ItemInstance` → `EquipmentService.Equip` succeeds and blocks `RemoveBodyPart` on the referenced body part → `EquipmentService.Unequip` succeeds → `RemoveBodyPart` then succeeds. No production code — a composition proof, closing the Equipment runtime block, exactly the way `ODY-S05-207` closed the Inventory runtime block.

## 2. Task contract

- Goal / acceptance / scope / allowed paths: `docs/tasks/active/ODY-S05-306_Equipment_Runtime_Integration_Fixtures.md`.
- Authorities: `ODY-S05-207` task+plan (structural template), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12 row 6 / §12.1, `ADR-027` §7, `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Non-goals: any new production code, new `ErrorCode`, new table/column, weapon/armor mechanical effects, `ActiveEffect`/attack composition, Unity/UI, `docs/adr/**` edits, `ODY-S05-301`–`305` file edits, duplicating their own isolated unit tests.
- Validation: `dotnet build`; `dotnet test`; `verify-format.ps1`; `check-repository-policy.ps1`; `verify-test-structure.ps1`.

## 3. Current state

- `origin/main` at `83c08d4` (merge of PR #127, `ODY-S05-305`) — all of `ODY-S05-301`–`305` are merged, so unlike `ODY-S05-207` this branch needs no local stacking of unmerged sibling PRs.
- No `ODY-S05-306` file exists in the working copy; this contract + plan are authored fresh.
- Verified no production fixture hook is reserved for this task anywhere in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §12.1 or `ODY-S05-301`–`305`; a repository-wide search for "306" outside `docs/tasks/**`/`docs/plans/**` found nothing — this task adds only a test file + metadata.
- `ContentDefinitionType.Armor`/`Weapon`, `ArmorDefinition`/`WeaponDefinition`, and `TypedDefinitionCodec.EncodeArmor`/`EncodeWeapon` already exist and are already validated by `CatalogValidationService`; `InventoryCreationService.CreateItemInstanceFromDefinition`'s `IsInstanceDefinitionType` already allows `Armor`/`Weapon` alongside `Item`.
- Armor is the simpler fixture choice (no `AmmoRequirement`/ammo-catalog dependency, unlike Weapon).

## 4. Proposed approach

- One new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/EquipmentRuntimeIntegrationFixtureTests.cs`, real temp SQLite campaign, private helper methods (no new production type):
  - `PublishArmor` — `ContentCatalogAuthoringService.CreateDraftDefinition` (`actorIsMainGm: true`, `RulesetCompatibility = ["ruleset.core@1.0.0"]`) + `ContentCatalogLifecycleService.PublishDefinition`, over a `TypedDefinitionCodec.EncodeArmor` fixture.
  - `CreateInitializedCharacter` — through `SqliteCharacterRepository` (constructed with both `InventoryCharacterDeletionDependencyChecker` and the new `InventoryBodyPartRemovalDependencyChecker`) + `InitializeCharacterAnatomy`, mirroring `BodyPartRemovalDependencyCheckerTests.cs`'s own helper.
  - `CreateInventory` — direct `SqliteInventoryRepository.CreateInventory` (no Application-layer wrapper exists, matching `ODY-S05-207`/`305`'s own precedent).
- The main sequence, entirely through public Application services (never a raw repository Equip/Unequip call):
  1. Publish an Armor definition covering `Head`.
  2. `InventoryCreationService.CreateItemInstanceFromDefinition` → runtime `ItemInstance`, `Contained` in a fresh Character's Inventory.
  3. `EquipmentService.Equip` (MainGM-gated) → success; `BodyPartRefs = ["Head"]`.
  4. `RemoveBodyPart("Head")` → rejected, `CharacterBodyPartHasDependent`.
  5. `EquipmentService.Unequip` (MainGM-gated) → success.
  6. `RemoveBodyPart("Head")` → succeeds.
  `Head`, not `Torso`, is used because `Torso` has `LeftArm`/`RightArm` attached to it in the default humanoid fixture and can never be removed regardless of equipment state, which would confound this round trip with the pre-existing internal dependency check.
- Additional tests: a Weapon variant (no ammo requirement) proving the path is not Armor-specific; a negative scoping test (`RemoveBodyPart` on an unrelated body part succeeds while the referenced one is still blocked, through the full stack); a sanity check that the item's `LocationRef` genuinely returns to `Contained` and the item is not lost after the round trip.
- Register `TC-INVENTORY-153`+ in `test-catalog.json`; mark backlog row 6 `In Review` with the PR link.

No production code change, no Unity/UI, no new table, no new `ErrorCode`, no `ADR` change.

## 5. Milestones

### M1 — Fixture and tests
- [x] `EquipmentRuntimeIntegrationFixtureTests.cs` (4 cases) — one revision-math bug fixed (`UnequipRequest.ExpectedTargetRevision` needs the item's own post-Equip revision, not `EquippedEntry.Revision`), then all 4 passed.
- [x] `Tests/Metadata/test-catalog.json` entries `TC-INVENTORY-153`–`156`.

### M2 — Validation and review readiness
- [x] `dotnet build DotNet\Odyssey.Core.sln`.
- [x] `dotnet test DotNet\Odyssey.Core.sln`.
- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `.\scripts\verify-test-structure.ps1`.
- [x] `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 6 → `In Review (PR #128)`.
- [x] Commit, push, open Draft PR.
- [ ] Record CI status.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, verified PR #127 merged and `origin/main` at `83c08d4`, verified `ODY-S05-306` backlog row is `Proposed`, created `feat/ody-s05-306-equipment-runtime-integration-fixtures` from `origin/main`.
- 2026-09-11 — Read `ODY-S05-207` task+plan and `InventoryRuntimeIntegrationFixtureTests.cs` as the structural template; confirmed `IsInstanceDefinitionType` already allows `Armor`/`Weapon`; confirmed no reserved fixture hook.
- 2026-09-11 — Wrote `EquipmentRuntimeIntegrationFixtureTests.cs`; discovered `Torso` can never be removed regardless of equipment state (the default humanoid fixture attaches `LeftArm`/`RightArm` to it), so switched the round trip to `Head`; first run of the round-trip tests failed Unequip (wrong revision passed for `ExpectedTargetRevision`), fixed to the item's own post-Equip revision; all 4 tests passed.
- 2026-09-11 — Registered `TC-INVENTORY-153`–`156`; ran all five validation commands (§9), all passed; confirmed via `git diff --stat` that zero `Packages/com.odyssey.*` files changed.

## 7. Decisions

See task contract §18 for the full decision log.

## 8. Discoveries and deviations

- `Torso` cannot be used as the round-trip target body part: the default humanoid anatomy fixture attaches `LeftArm`/`RightArm` to it, so the pre-existing internal Character-only dependency check always blocks its removal regardless of equipment state. Switched to `Head` (no internal dependent).
- One test-authoring bug (not a production defect): `UnequipRequest.ExpectedTargetRevision` needs the item's own revision after Equip (`instance.Revision + 1`), not `EquippedEntry.Revision` — the two are independent counters. Fixed before finalizing.

## 9. Validation and acceptance evidence

- `dotnet build DotNet\Odyssey.Core.sln` — passed, 0 warnings, 0 errors.
- `dotnet test DotNet\Odyssey.Core.sln` — passed, 775 total, 0 failed (Contracts 1, Domain 80, Networking 67, Unit 136, Architecture 2, Persistence 489).
- `.\scripts\verify-format.ps1` — `FORMAT-001 PASS`.
- `.\scripts\check-repository-policy.ps1` — `Repository policy check passed.`
- `.\scripts\verify-test-structure.ps1` — exit code 0, `TC-ARCH-001 PASS`.
- `git diff --stat` confirms zero changes under `Packages/com.odyssey.*`.

## 10. Recovery and rollback

Normal revert of the branch/PR — test-only change, no production code, no schema, nothing to migrate back.

## 11. Open questions and blockers

- None. Unlike `ODY-S05-207`, no PR stacking is needed — `ODY-S05-301`–`305` are all already merged.

## 12. Outcome and follow-up

Draft PR [#128](https://github.com/odyssey-services/Odyssey_VTT/pull/128). Closes the Equipment runtime block (`ODY-S05-301`–`306`).
