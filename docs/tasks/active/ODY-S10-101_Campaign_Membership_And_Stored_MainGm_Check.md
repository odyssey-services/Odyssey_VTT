# ODY-S10-101 — Campaign Membership Entity + Stored MainGM Check (Board Movement Pilot)

## 1. Task identity
`ODY-S10-101`; status: In Review (Draft PR, merge deferred to the product owner). First task of `SLICE-10` (`docs/tasks/SLICE-10_IMPLEMENTATION_BACKLOG.md`), and step MG-1 of its "replace the client-supplied MainGM flag" sub-track.

## 2. Goal
Introduce a persisted campaign participant with a role, one shared predicate for "is this user the campaign's MainGM", make campaign creation register its creator as MainGM atomically, and pilot the stored check on the smallest subsystem — token movement on the board — so no client can simply claim MainGM with a flag.

## 3. Authority
This task's governing ТЗ; the research report `docs/research/SLICE-10_Visibility_FogOfWar_Research.md` §1.4; precedents: `ActiveEffect` for a new table created with `CREATE TABLE IF NOT EXISTS`, `SetTokenPortrait` for the `Result`/`CommandId`/`CorrelationId` shape, `CharacterOwnershipAssignment.IsAssignedCharacter` for a canonical static predicate.

## 4. In scope
- **Entity and storage:** `CampaignMembership` (`UserId`, `CampaignId`, `Role`, `Revision`, `CreatedAt`, `UpdatedAt`), enum `CampaignMembershipRole { MainGm, Player, Observer }` (stored as the enum name), table `CampaignMembership` (`UserId` primary key — one row per user, each campaign being its own database file). No migration machinery: a campaign file created before this task simply has no membership rows, so nobody is MainGM in it.
- **`ICampaignRepository`:** `AddMember` (duplicate `UserId` rejected with the new `PersistenceCampaignMembershipAlreadyExists`; same `CommandId` replays), `ListMembers` (oldest first), `GetMemberRole` (single-user point lookup; "not a member" is a success).
- **Shared predicate:** `CampaignMembershipAuthorization.IsMainGm(repository, campaign, actor, correlationId) : Result<bool>` in `Odyssey.Application.Identity`. Location rationale: next to `DevIdentityProvider`, not in the Character namespace — it concerns campaign participants, not a character's ownership, and depends only on the Application repository port. A storage failure is a failure (fail closed), not "false".
- **Bootstrap:** `CreateCampaignRequest` gains a required trailing `UserId hostUserId` (`HostUserId`); `SqliteCampaignRepository.Create` inserts the host's `MainGm` row inside the same pipeline `apply` transaction that writes the Campaign row. A required parameter (not optional) so a campaign can never again be created without its MainGM.
- **Pilot:** `MoveTokenRequest.ActorIsMainGm` removed (breaking change, no `[Obsolete]`); `BoardMovementService.MoveToken`/`UndoMoveToken` take an `ICampaignRepository` and authorize: token controller → allowed without a lookup; anyone else → stored `MainGm` required, otherwise `BoardTokenMoveDenied`; lookup failure → the error is returned (fail closed). `MoveTokenRequest.ActorUserId` was already the acting user's id and is the value checked.
- **Client wiring:** `BoardScreenPresenter` constructors take an `ICampaignRepository` (after `campaign`); the demo campaign creation passes `DevIdentityProvider.AssignHost()` as host (first place `AssignHost()` and `Create` are connected) and the demo handle exposes its repository; `TrialScreenPresenter` passes it through.
- All `new CreateCampaignRequest(...)` call sites, tests, catalogue, `ERROR_CODES.md`/`ErrorCodes.cs` for the new error, the new `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
The four other subsystems (characters, combat/effects, content catalog, dice/inventory) — still on the client flag, untouched: `SqliteCharacterRepository.cs`, `CombatEncounterContracts.cs`, `ContentCatalogAuthoringContracts.cs`, `ContentCatalogLifecycleContracts.cs`, `DiceRollService.cs`, `InventoryStackOperationService.cs`, `InventoryMovementService.cs`, `EquipmentService.cs`, `InventoryCreationService.cs`, `SqliteAttackApplyRepository.cs`, `SqliteActiveEffectRepository.cs`, `ActiveEffectDirectCommandService.cs`, `SqliteInventoryRepository.cs`; UI for participants/roles; role change or member removal; `CharacterOwnership`, `ControllerUserId`, `RoleSelection`, `DevIdentityProvider` (used as-is); ADRs; `.asmdef`/`.csproj`; verify scripts.

## 6. Domain / 7. Application / 8. Persistence
Application: `CampaignRepositoryContracts.cs` (types, port, request, failure factory), `CampaignMembershipAuthorization.cs`, `BoardContracts.cs`, `BoardMovementService.cs`. Persistence: `SqliteCampaignRepository.cs`. No Domain change.

## 9. Tests and validation
New: `TC-PERSIST-037` (member operations, duplicate rejected, replay, per-campaign isolation), `038` (`Create` registers the host as MainGm atomically — same `LastCommandId` on the Campaign and membership rows — and survives reopen), `039` (predicate: true for host and an added MainGm; false for Player, Observer and a user with no record), `040` (movement authorization follows the stored membership: no-membership/Player/Observer denied, position unchanged; an `AddMember`'d MainGm is allowed from the next call; a failing lookup fails closed). The forged-flag scenario is closed structurally — the flag no longer exists — and by `040`: an unregistered actor cannot move someone else's token however it presents itself.

**Existing tests changed, and why:**
- `Board/BoardMovementServiceTests.cs` — every `actorIsMainGm: true/false` argument removed (the parameter is gone); the service calls pass the campaign repository; `TC-BOARD-006` ("MainGM moves anyone's token") previously used an arbitrary unregistered `UserId` plus `actorIsMainGm: true`, which would now be a plain denial — it now acts as the campaign's host, who is registered as MainGm at `Create`. The other tests use a controller or an unregistered outsider and keep their meaning.
- `Integration/VerticalSliceIntegrationTests.cs`, `MvpTwoCharacterCombatScenarioTests.cs` — the `actorIsMainGm` argument removed from `MoveTokenRequest`; the MVP scenario's retreat move (which relied on the flag with a random user) now acts as the host.
- Unity `BoardScreenPresenterTests.MainGmActor_MovesForeignToken_Succeeds` — the actor is now the host instead of a random user with `LocalActorIsMainGm = true`.
- All ~90 other `CreateCampaignRequest` constructions gained the host argument mechanically; the presenter tests gained the repository argument.

## 10-17. (see plan)
Compatibility, security, observability, performance, dependencies and completion evidence follow the same shape as earlier tasks.

## 18. Change control

### Decisions made during execution
- **`Result<CampaignMembershipRole?>` is not possible:** `Result<T>` constrains `T` to `notnull`, which excludes `Nullable<T>`. `GetMemberRole` returns `Result<CampaignMemberLookup>` (a tiny struct with `IsMember` and `Role`) — same information, "not a member" still a success.
- **Host id is an explicit required constructor argument,** appended as the last parameter so the ~90 call sites are a mechanical insertion. Tests and the demo pass `DevIdentityProvider.AssignHost()` (a fixed dev identity) rather than a per-site fresh id; that id equals `RoleSelection.DefaultMainGmUserId`, so the role selector's "MainGM" identity is the campaign's real MainGm.
- **Membership is written by the same pipeline transaction as the Campaign row** (inside `Create`'s `apply`), so the two cannot diverge; the campaign-created event/journal entry is unchanged (payload untouched).
- **`GetMemberRole` opens a short connection per call,** like the scene repository does; a per-move lookup happens only for non-controllers, and only in the two authorization checks the service already performed.
- **`LocalActorIsMainGm` on `BoardScreenPresenter` remains** (the role selector still drives it and other UI reads it) but is now presentation state: movement no longer reads it.
- **`CampaignMembershipRole` is its own persisted vocabulary,** parallel to the in-memory session role `BaselineRole` (`Networking/Session`), not a reuse — the persistence contract should not depend on the networking session type. Two role enums now exist; unifying them is a design question left to the product owner.

### Findings (reported, not fixed silently)
- **Allowed-path gaps (necessarily touched outside the ТЗ §4 list):** `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` and `docs/errors/ERROR_CODES.md` (the new `persistence.campaign_membership.already_exists` code and its registry row — the repository policy check requires both); `Assets/Odyssey/Client/Runtime/TrialScreenPresenter.cs` (one argument: it builds the board presenter, which now needs the campaign repository); `Assets/Odyssey/Client/Tests/EditMode/AssetPoolPresenterTests.cs`, `RoleSelectorPresenterTests.cs`, `GameLogPresenterTests.cs` and other test files that construct campaigns or presenters. No other subsystem file was touched.
- **The estimate of ~40 `Create` call sites was low:** there were **91 `new CreateCampaignRequest(...)` constructions in 69 files** (1 production, 90 tests).
- **Old campaign files:** a campaign database created before this task has no `CampaignMembership` rows, so nobody in it is a MainGm and only token controllers can move tokens there. There is no migration or "adopt existing host" path (none exists in the project); such a campaign must be recreated or have its MainGm added with `AddMember`.
- **`SLICE-10` product blocks:** the six product-block decomposition was not provided in this task's text; the new backlog reserves them explicitly rather than inventing them.
- Two role vocabularies (`CampaignMembershipRole` vs `BaselineRole`) and two "who controls" sources (`ControllerUserId`, `CharacterOwnership`) still exist, as the research report noted.

### Blockers
None.
