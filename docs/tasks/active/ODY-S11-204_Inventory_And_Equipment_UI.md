# ODY-S11-204 — Inventory and equipment UI (phase 4)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phase 4 of 0–5)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** `ODY-S11-200` §14  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

The Inventory drawer shows the open character's inventory and the scene ground, grouped by zone, and lets the MainGM
create items from published definitions, move, split, merge, equip and unequip them; everyone else sees why they
cannot.

## 3. Authorities and requirement references

- ADR-028/029 (inventory runtime, equipment), ADR-002, ADR-004; `InventoryCreationService`,
  `InventoryMovementService`, `InventoryStackOperationService`, `EquipmentService`, `IInventoryRepository`.
- New test IDs: `TC-INVUI-001`…`TC-INVUI-007`.

## 4. Verified backend facts used

- Every inventory mutation is MainGM-only (stored membership) — create, move, split, merge, equip, unequip.
- Instances: Item / Weapon / Armor. Stacks: stackable Item and Ammo (quantity ≤ MaxStackSize).
- One `InventoryId` per owner; zones via `LocationRef` (`Contained(inv, containerKey)`, `Equipped(inv, slot)`,
  `SceneDropped`, `Other`). Container/slot keys are canonical lowercase tokens.
- Name/description are not stored on items: the UI shows the **live catalog name** (`GetContentDefinition` by
  `SourceItemDefinitionRef`) and the **snapshot mechanics** (`MechanicsSnapshot`, decoded with `TypedDefinitionCodec`).
- No single "load inventory" query: `ListItemInstances` + `ListItemStacks` + `ListEquippedEntries` are aggregated.
- Equip does not validate slot vs. `EquipmentSlotKey`/`CoveredBodyPartIds`; body parts must exist on the character.
- `RemoveBodyPart` is blocked while equipment is worn on the part (`InventoryBodyPartRemovalDependencyChecker`,
  wired in the trial composition since `ODY-S11-203`).

## 5. Scope

### In scope
- `Runtime/Inventory/InventoryLocator.cs` (owner→inventory id, create, aggregate load, snapshot summary, armor hint),
  `Runtime/Inventory/InventoryPanelPresenter.cs`, wiring in `TrialScreenPresenter` (follows the Character panel's
  open character).
- Tests `InventoryPanelPresenterTests.cs`.

### Out of scope
- UseItem (combat, `ODY-S11-205`), drag-and-drop between panels, scene-dropped positions on the map. Backend untouched.

## 6. Decisions

- **Finding the inventory (backend gap).** There is no "inventory of owner X" query. The UI uses one stable
  client-side id convention for each owner's single inventory: `inv_` + the owner id's hex (`char_X` → `inv_X`,
  `scn_X` → `inv_X`), passed to the backend's own `CreateInventory`. The MainGM creates a missing inventory with
  one button; others see "the MainGM creates it". Recorded as a limitation pending a backend owner lookup.
- **Scene ground**: the current scene's inventory (`InventoryOwnerRef.ForScene(scene, "ground")`) — drop/pick-up
  target for moves.
- **Role**: explanation banner for non-MainGM ("…a backend rule. Ask your MainGM…"); actions refuse with a
  readable warning; no silent disabling.
- **Revisions**: every call uses the item/inventory/equipped-entry revisions of freshly loaded state; both
  inventories reload after every success; `StateChanged` reloads before reporting.
- **Equip hints**: for Armor the snapshot's slot key and covered parts pre-fill the form — a hint only. No client-side
  slot validation beyond what the backend checks (free slots are accepted, as the backend accepts them).
- Moving an equipped item asks to unequip first (the move service works on contained items).

## 9. Acceptance criteria

1. Inventory/equipment of the character is listed, grouped by zone. ✔
2. All §4.3 operations available to the MainGM, clearly unavailable to others. ✔
3. Tests. ✔ (pending owner run)

## 10. Tests and validation

| ID | Test |
|---|---|
| `TC-INVUI-001` | `NonMainGm_SeesWhyActionsAreUnavailable_AndCannotCreate` |
| `TC-INVUI-002` | `MainGm_CreatesInventories_AndItemsFromPublishedDefinitions_GroupedByZone` |
| `TC-INVUI-003` | `Stacks_SplitAndMerge` |
| `TC-INVUI-004` | `Move_BetweenCharacterAndSceneGround` |
| `TC-INVUI-005` | `Equip_ArmorHintPrefillsSlot_EquippedBlocksBodyPartRemoval_UnequipReturnsIt` |
| `TC-INVUI-006` | `Equip_FreeSlotIsAcceptedAsTheBackendAllows` |
| `TC-INVUI-007` | `StaleItemRevision_IsRejected_AndThePanelReloads` |

**Not run in this session** (no Unity/pwsh/.NET in the container; owner validates locally).

## 17. Completion evidence

`git diff --name-status main -- Packages DotNet` empty. Validation not run in container.

## 18. Blockers, decisions, and change control

- Backend gap (reported, not fixed): no inventory-by-owner query (client id convention used, §6).
