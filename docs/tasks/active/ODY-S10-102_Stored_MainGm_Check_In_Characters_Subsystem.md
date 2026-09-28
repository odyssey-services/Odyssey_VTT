# ODY-S10-102 — Stored MainGM Check in the Characters Subsystem

## 1. Task identity
`ODY-S10-102`; status: In Review (Draft PR, merge deferred to the product owner). Step MG-2 of the "replace the client-supplied MainGM flag" sub-track of `SLICE-10` (`docs/tasks/SLICE-10_IMPLEMENTATION_BACKLOG.md`). Delivered in one pull request together with a comment-only fix of a displaced XML doc comment in `CampaignRepositoryContracts.cs` (separate first commit, see §18).

## 2. Goal
Make every MainGM decision in `SqliteCharacterRepository` depend on the stored campaign membership (`CampaignMembershipAuthorization.IsMainGm`, `ODY-S10-101`) instead of a `bool actorIsMainGm` the caller claims, keeping each of the four authorization rule shapes of that file exactly as it was.

## 3. Authority
This task's governing ТЗ; `ODY-S10-101` (entity, predicate, fail-closed pattern, pilot); the cross-repository constructor-injection precedent already in this layer (`SqliteAttackStateReader`, `SqliteUseItemStateReader`, `SqliteCheckStateReader`).

## 4. In scope
- **Dependency:** `SqliteCharacterRepository(IWallClock clock, ICampaignRepository campaignRepository, …)` — required, `ArgumentNullException` on `null`, no default. The lookup lives in the Persistence layer where the 24 checks already lived; no Application service was built on top (no production caller of `ICharacterRepository` exists).
- **Signatures:** `bool actorIsMainGm` removed from all 32 methods (public, the private `MutateAnatomy`/`MutateOwnership` helpers, and the mirror in `ICharacterRepository`); `RestoreDeadCharacterRequest.ActorIsMainGm` removed (its `ActorUserId` was already there). Seven operations that had no acting user gained `UserId actorUserId` in the flag's slot: `ApproveCharacterDraft`, `AssignPrimaryOwner`, `AddCharacterCoOwner`, `RemoveCharacterCoOwner`, `GrantPermanentCharacterControl`, `GrantTemporaryCharacterControl`, `RevokeCharacterControl`; `MutateOwnership`/`MutateAnatomy` also receive `actorUserId`.
- **The 24 checks, four shapes kept:**
  1. 18 strictly-MainGM-only gates (approval, deletion, restore, ruleset migration apply/revert, development grant, advancement resolve/revert, respec, ability acquire/remove/link, resource initialize/set current/set maximum, anatomy via `MutateAnatomy`, primary-owner assignment, the five co-owner/control commands via `MutateOwnership`): the `if (!actorIsMainGm)` body is replaced in place by the lookup (a lookup failure is returned; `false` → the gate's own denial error).
  2. 5 owner-or-MainGM points (`ArchiveCharacter`, `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RequestSkillAdvancedRecommendation`, `AcquireAbilityViaProgressionPurchase`): the order is **inverted** from `actorIsMainGm || IsAssignedCharacter(...)` to owner first — `IsAssignedCharacter(...)` — and the stored-membership lookup only when that fails, so an owner never touches the database for the role.
  3. 1 conditional gate (`TransitionCharacterToDead`): still applies only when `issuerKind == GMOverride`; only the body changed.
  4. The seven no-actor operations described above.
- **Fail-closed everywhere:** a `Failure` from `IsMainGm` is returned as that failure — never treated as a pass, never converted into a fake denial.
- **`CharacterAdvancementService`:** the wrappers' pass-through of the removed flag was dropped (they already carried `actorUserId`); no logic added.
- **Tests:** reworked (§9); `TC-PERSIST-041`–`049`.

## 5. Out of scope
Combat/attack/effects, content catalog, dice/inventory production code (`ODY-S10-103`–`105`), `BoardMovementService`/`BoardScreenPresenter`/`BoardMovementServiceTests`, `CampaignMembershipAuthorization.cs`/`SqliteCampaignRepository.cs`, `CharacterOwnership.cs` (`IsAssignedCharacter`, `CharacterTemporaryControlGrant` reused as-is), every Unity client file, ADRs, `.asmdef`/`.csproj`, verify scripts.

## 6-8. Domain / Application / Persistence
Application: `CharacterRepositoryContracts.cs`, `CharacterAdvancementService.cs`. Persistence: `SqliteCharacterRepository.cs`. No Domain change.

## 9. Tests and validation
**New (`TC-PERSIST-041`–`049`):**
- `041` — one or more strictly-MainGM-only operations per logical group (ownership transfer and list, draft approval, deletion, ruleset migration, resource, anatomy, advancement): an unregistered user, a Player and an Observer are denied with the gate's own error code; the host and an added MainGm pass the gate.
- `042` — `TransitionCharacterToDead`: `GMOverride` denied for an unregistered user, allowed for the host, fail-closed on an unreadable membership; any other issuer kind succeeds while a repository whose lookup **throws** is never called (regression of the conditional structure).
- `043` — fail-closed: with a failing lookup every strict representative returns the lookup's own failure even for the host, and nothing changed.
- `044` — the seven operations that gained `actorUserId` really use it: a non-MainGm actor is denied even when the *target* is the MainGm; the MainGm actor passes even when the target is a stranger.
- `045`–`049` — one per owner-or-MainGM point (archive, attribute purchase, skill purchase, advanced-skill recommendation, ability acquisition): the assigned owner succeeds with a lookup that would throw never being called (`LookupCalls == 0`); a non-owner reaches the lookup and its failure is returned.
A deliberate mutation (a gate that ignores the lookup result) was confirmed to fail `041` and `044`, then reverted.

**Existing tests changed, and why:** every test calling one of the 32 methods, and every test constructing `SqliteCharacterRepository`, had to change (compile-level), and the semantics were reworked rather than left with a dead parameter: `actorIsMainGm: true` (about 440 uses) became the registered host (`DevIdentityProvider.AssignHost()`, a stored MainGm since `ODY-S10-101`) as the acting user; `actorIsMainGm: false` uses simply drop the flag, so the actor (an owner or an unregistered user) is judged by the stored role; the seven no-actor operations receive the host for `true` and an unregistered dev identity for `false`; the recording/forwarding `ICharacterRepository` test doubles in `CharacterResourceAnatomyTests.cs` and `CheckIntegrationTests.cs` were updated to the new signatures (the recorder no longer captures the removed flag); the parameterized helper tests that switched between "a GM" and "not a GM" now switch between the host and an unregistered user. `SqliteCharacterRepository` fixtures receive the same `SqliteCampaignRepository` instance their `[SetUp]` uses; fixtures of other subsystems that only need a character as setup got a fresh `SqliteCampaignRepository` (the lookup opens its own connection per call, so no shared state is needed).

## 10-17. (see plan)
Compatibility, security, observability, performance and dependencies follow the same shape as `ODY-S10-101`.

## 18. Change control

### Decisions made during execution
- **Lookup inside open transactions.** Five owner-or-MainGM checks run inside an open pipeline transaction (the character row must be read first). `GetMemberRole` opens its own short connection to the same campaign database; under the mandatory WAL profile this read did not block or time out in any test (the whole persistence suite is green), so the lookup was kept at the original check position instead of restructuring those methods.
- **Unregistered actor constant for tests:** `DevIdentityProvider.AssignJoiningActor(0)` (a fixed dev identity no test registers) is used where a "not a MainGm" acting user is needed for the seven no-actor operations.
- **Message wording.** Doc comments on the interface that described "the caller-supplied boolean convention" were rewritten to say the stored membership decides.

### Findings (reported, not fixed silently)
- **Paths outside the ТЗ list (necessary, mechanical):** the constructor now requires a campaign repository, so all 41 test files that construct `SqliteCharacterRepository` had to change; 27 of them are outside the 14 Character files named in the ТЗ (attack, combat, check, use-item, activate-ability, equipment/inventory/item-migration fixtures, `SqliteAttackStateReaderTests`, `SqliteSceneRepositoryTests`, …). In those files only the constructor argument and — where a fixture used `ApproveCharacterDraft`/`AssignPrimaryOwner`/… as setup — the call arguments changed; no production file of those subsystems was touched.
- **Second commit in this PR** (`CampaignRepositoryContracts.cs`, comment-only fix requested as a separate ТЗ) touches a file this ТЗ lists as forbidden; the ТЗ-2 rule "do not change `CampaignRepositoryContracts.cs`" was respected for the character work itself.
- **A test double that forwards `ICharacterRepository`** exists in two test files; they had to be updated by hand, like any implementer of the interface.
- Unity was not re-run: no client file changed and the production edits are plain C# in the Persistence/Application assemblies, which `dotnet build` compiles from the same sources.

### Blockers
None.
