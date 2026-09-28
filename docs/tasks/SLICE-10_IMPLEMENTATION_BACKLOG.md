# Odyssey VTT — SLICE-10 Visibility / Fog of War / Obstacle Geometry Implementation Backlog

**Status:** Implementation revision — OPEN. `ODY-S10-101` (campaign membership + a stored MainGM check piloted on board movement) is merged; `ODY-S10-102` (the characters subsystem) is in review; everything else is named/reserved, not scoped.
**Slice:** `SLICE-10 — per-player visibility: walls/doors/windows, line of sight, fog of war and cover, on top of a real "campaign participant" entity`
**Parent research:** `docs/research/SLICE-10_Visibility_FogOfWar_Research.md` (PR #187 — the factual map of today's code that this slice starts from).
**Predecessor backlog:** `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md` (board UX, complete) and `docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md`.
**ExecPlan:** Not required for this document itself; each child task chooses its own planning mode.
**Created:** 2026-09-28

## 1. Purpose

The product direction is a Smoke-and-Spectre-style board: the GM draws walls/doors/windows, the system computes line of sight per player, fog of war, and cover in combat. The research report found that the first thing this needs — a first-class "campaign participant" (a persisted user with a role) — does not exist: `UserId` is a bare value, membership exists only in memory, and the MainGM check throughout the backend is a client-supplied boolean (`ActorIsMainGm`) the host trusts on its word. This backlog therefore starts with that foundation.

## 2. Two tracks inside this slice

### 2.1 Product blocks (visibility / fog / geometry)

**The six product blocks of the product owner's decomposition were not part of the `ODY-S10-101` task text, and this document does not invent them.** The research report (§§1.1–1.8) and the product specification (`08_Scenes_And_Board`, slices `BOARD-04` structures and cover and `BOARD-05` fog and vision) are the inputs; the decomposition itself is the product owner's, and is to be entered here as blocks `P1`–`P6` when supplied. Until then they are **reserved, not scoped**:

| Block | Content | Status |
|---|---|---|
| P1–P6 | Product-owner decomposition of the visibility/fog/geometry work | **Reserved — content not supplied to `ODY-S10-101`; to be filled in by the product owner** |

`ODY-S10-101` is a prerequisite of the per-player parts of those blocks (a persisted participant is the subject of "visibility per player"), not one of the blocks itself.

### 2.2 Sub-track: replace the client-supplied MainGM flag with a stored role check

Today `ActorIsMainGm` is a boolean the caller passes in; about 47 real check points in 32 production files across five independent subsystems trust it (there is no single authorization point). This sub-track builds the stored entity and one shared predicate and moves the subsystems over one at a time.

| Step | Subsystem | Status |
|---|---|---|
| **MG-1** | Membership entity + shared predicate + creator becomes MainGM atomically + **pilot: token movement on the board** (`BoardMovementService`) | **Done** (`ODY-S10-101`, PR [#188](https://github.com/odyssey-services/Odyssey_VTT/pull/188), merged) |
| **MG-2** | Characters (`SqliteCharacterRepository.cs` — the largest cluster — and `CharacterRepositoryContracts`/`CharacterAdvancementService`) | **`ODY-S10-102`, In Review** |
| MG-3 | Combat and effects (`CombatEncounterContracts.cs`, attack evaluate/apply, `ActiveEffect*`, activate-ability, use-item) | Named, not started |
| MG-4 | Content catalog (`ContentCatalogAuthoringContracts.cs`, `ContentCatalogLifecycleContracts.cs`) | Named, not started |
| MG-5 | Dice and inventory (`DiceRollService.cs`, `InventoryStackOperationService.cs`, `InventoryMovementService.cs`, `EquipmentService.cs`, `InventoryCreationService.cs`, `InventoryRepositoryContracts`/`SqliteInventoryRepository.cs`) | Named, not started |

MG-3…MG-5 are provisional labels, not reserved task ids (`ODY-S10-102` was used for MG-2; the later numbers are left to the product owner's decomposition). Each will replace the boolean in its subsystem with `CampaignMembershipAuthorization.IsMainGm`, exactly as `ODY-S10-101` does for board movement. Until a subsystem is moved it stays on the client flag, byte for byte.

## 3. Global non-goals (this revision)

- Any wall/door/window/fog/vision/cover implementation (blocks P1–P6).
- UI for managing participants or roles; role change; member removal.
- Changing `CharacterOwnership`, `TokenRecord.ControllerUserId` or the role-selector development stub — parallel mechanisms, not touched by the sub-track.
- Any change to an ADR.

## 4. Dependency rules

- MG-2…MG-5 depend on `ODY-S10-101` (the entity, the predicate and the creator-becomes-MainGM rule) and are independent of each other.
- The per-player parts of the product blocks depend on `ODY-S10-101` (a persisted participant), not on MG-2…MG-5.

## 5. Ordered backlog

| Order | Task ID | Status | Source | Title | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|---|
| 1 | `ODY-S10-101` | Done (PR [#188](https://github.com/odyssey-services/Odyssey_VTT/pull/188), merged into main) | Research report `SLICE-10_Visibility_FogOfWar_Research.md` §1.4; product-owner decision to replace the MainGM flag | Campaign Membership Entity + Stored MainGM Check (Board Movement Pilot) | — | ExecPlan | `CampaignMembership` (`UserId`, `CampaignId`, `Role` `MainGm`/`Player`/`Observer`, `Revision`, timestamps) in a new `CampaignMembership` table (plain `CREATE TABLE IF NOT EXISTS`, ActiveEffect precedent; no migration), `ICampaignRepository.AddMember`/`ListMembers`/`GetMemberRole`, shared predicate `CampaignMembershipAuthorization.IsMainGm`. **`CreateCampaignRequest` now requires `HostUserId` and `Create` registers the host as MainGm in the same transaction as the Campaign row** (closing the bootstrap hole: `Create` used to know no user at all). `BoardMovementService` authorizes a non-controller only by the stored role; `MoveTokenRequest.ActorIsMainGm` is removed and `MoveToken`/`UndoMoveToken` gain an `ICampaignRepository`. 91 `Create` call sites in 69 files updated. Tests `TC-PERSIST-037`–`040`; existing board tests reworked. |
| 2 | `ODY-S10-102` | In Review | Sub-track MG-2 (section 2.2) | Stored MainGM Check in the Characters Subsystem | `ODY-S10-101` | ExecPlan | `SqliteCharacterRepository` takes a required `ICampaignRepository`; all 32 methods lose `bool actorIsMainGm` (also `RestoreDeadCharacterRequest.ActorIsMainGm` and the six `CharacterAdvancementService` wrappers' pass-through). The 24 check points now call `CampaignMembershipAuthorization.IsMainGm` and fail closed on a lookup failure. The four rule shapes are kept as they were: 18 strictly-MainGM-only gates, the conditional `GMOverride` gate of `TransitionCharacterToDead`, and five owner-or-MainGM points -- whose order is now **owner first, stored MainGM second** (no lookup for an owner). Seven operations that had no actor (`ApproveCharacterDraft`, `AssignPrimaryOwner`, `AddCharacterCoOwner`, `RemoveCharacterCoOwner`, `GrantPermanentCharacterControl`, `GrantTemporaryCharacterControl`, `RevokeCharacterControl`) gained `UserId actorUserId`. Tests `TC-PERSIST-041`-`049`; ~476 existing call sites reworked to act as the registered host. Also contains the comment-only fix of the displaced `TokenRevisionConflict` doc comment. |

## 6. Backlog change control

- New work requires a task contract. `ODY-S10-101` and `ODY-S10-102` are the only numbers used so far; the product blocks P1–P6 and MG-3…MG-5 get numbers when the product owner activates them.
- Filling in P1–P6 is a backlog revision by the product owner, not an implicit extension of `ODY-S10-101`.
