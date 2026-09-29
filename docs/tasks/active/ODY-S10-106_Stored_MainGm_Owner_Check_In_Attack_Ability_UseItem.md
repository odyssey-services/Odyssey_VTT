# ODY-S10-106 — Stored MainGM/Owner Check in Attack Evaluation, Ability Activation and Item Use

## 1. Task identity
`ODY-S10-106` (MG-3b); status: In Review (Draft PR, merge deferred to the product owner). The last of the `ActorIsMainGm` findings from the `ODY-S10-101`–`105` sub-track (`docs/tasks/SLICE-10_IMPLEMENTATION_BACKLOG.md`), closing the pattern project-wide except the deliberately-deferred `RedactCharacterForExport.cs` question.

## 2. Goal
Make the MainGM half of the three *owner-or-MainGM* gates (`AttackEvaluationService.AuthorizeAndRead`, `ActivateAbilityService.Authorize`, `UseItemService.Authorize`) depend on the stored campaign membership instead of a caller-supplied flag, without touching the owner half (`CanControlActor`), which already does not trust the client.

## 3. Authority
This task's governing ТЗ; `ODY-S10-101`'s Application-layer pattern (`DiceRollService`, parameter injection on a static class) for how the check is wired in; `ODY-S10-102`'s `CharacterOwnershipAssignment.IsAssignedCharacter` (reused unmodified underneath `CanControlActor`, not reopened here).

## 4. In scope
- **`AttackEvaluationService.AuthorizeAndRead`** (and its two callers, `PreviewAttack`/`EvaluateAttack`, plus `AttackApplyService.ResolveAttack` which passes the dependency through to `EvaluateAttack`): gains an `ICampaignRepository` parameter; the `if (!request.ActorIsMainGm)` gate is replaced by `CampaignMembershipAuthorization.IsMainGm(...)`, fail closed. `CanControlActor` runs only when the lookup says "not MainGm" — same order as before.
- **`ActivateAbilityService.Authorize`** (and its one caller, `ActivateAbility`): same shape.
- **`UseItemService.Authorize`** (and its one caller, `UseItem`): same shape.
- `bool ActorIsMainGm` removed from `AttackRequest`, `ActivateAbilityRequest`, `UseItemRequest`.
- Tests, catalogue `TC-ATTACK-127`–`129`/`TC-ABILITY-015`–`016`/`TC-USEITEM-013`–`014`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
- `CanControlActor` in all three implementations (`SqliteAttackStateReader`, `SqliteActivateAbilityStateReader`, `SqliteUseItemStateReader`) — already safe, not reopened.
- `RemoveActiveEffectAsSystemRollback`-based compensation (`SqliteActivateAbilityRepository`/`SqliteUseItemRepository`) — confirmed by recon to read nothing this task changes; untouched, and the existing `TC-PERSIST-057`-shaped regression tests (failed activation/use by an ordinary Player still fully rolled back) pass unchanged.
- The points already closed in `ODY-S10-103` (`ResolveAttackIntervention`/`CompensateAttackOutcome`/`ResolveStackConflict`/`CombatEncounterContracts`) — a different place in the pipeline, not reopened.
- `CharacterOwnershipAssignment.IsAssignedCharacter`/`CharacterOwnership.cs` — not touched.
- `RedactCharacterForExport.cs`/`CharacterExportContracts.cs` — still a separate, deliberately-deferred question for the product owner (a cosmetic export-origin label, not an access check), as recorded in `ODY-S10-103`'s own contract.
- Client code: recon before implementation, and this task's own final sweep, both confirmed none of the three points has a real production caller today (`AttackEvaluationService.EvaluateAttack`'s only caller is `AttackApplyService.ResolveAttack`, which itself has no further production caller; `ActivateAbilityService.ActivateAbility` and `UseItemService.UseItem` have none at all — a stray doc-comment mention of `ActivateAbilityService.ActivateAbility` in `CheckService.cs` was checked and is not a call). No client file is in this diff.

## 6-8. Domain / Application / Persistence
Application only: `AttackEvaluationService.cs`, `AttackApplyService.cs` (pass-through parameter only), `ActivateAbilityService.cs`, `UseItemService.cs`, `ActivateAbilityContracts.cs`, `UseItemContracts.cs`. No Domain or Persistence change.

## 9. Tests and validation
New: `TC-ATTACK-127` (an owner without MainGm still succeeds through `CanControlActor` — the regression proving the new check did not remove the existing owner path); `TC-ATTACK-128` (a registered-but-not-owning, non-MainGm Player is denied; a real MainGm succeeds even when `CanControlActor` would say no); `TC-ATTACK-129` (fail-closed on an unreadable membership). `TC-ABILITY-015`/`TC-USEITEM-013` mirror `TC-ATTACK-128`'s shape for the other two points; `TC-ABILITY-016`/`TC-USEITEM-014` mirror the fail-closed proof. A deliberate mutation (`AttackEvaluationService`'s gate short-circuited) made the new attack test fail and was reverted.

**Existing tests changed, and why:** every construction of the three request types lost the `bool` flag argument; every call to the three service methods (and `AttackApplyService.ResolveAttack`) gained the campaign repository. Where an actor argument was previously an unregistered/random `UserId` paired with a self-claimed `true`, it became the campaign's registered host (`DevIdentityProvider.AssignHost()`) — the same rewrite every prior task in this initiative made. The existing `ODY-S10-103` regression tests (`ActivateAbility_FailedActivationByAnOrdinaryPlayer_IsStillFullyRolledBack`, `UseItem_FailedUseByAnOrdinaryPlayer_IsStillFullyRolledBack`), which already register a Player and assert `IsMainGm == false` while relying on `CanControlActor`/ownership for success, pass unchanged — direct proof the owner path survived the migration. `Odyssey.Tests.Unit.dll`: 186/186 (was 183). `Odyssey.Tests.Persistence.dll`: 887/887 (was 883).

## 10-17. (see plan)
Same shape as `ODY-S10-101`–`105`.

## 18. Change control

### Decisions made during execution
- **Dependency injection followed `DiceRollService`'s shape, not `SqliteCharacterRepository`'s**, exactly as the ТЗ specified: all three services are static Application-layer classes, so `ICampaignRepository` is a method parameter, threaded from each public entry point down to the one `Authorize`/`AuthorizeAndRead` helper that needs it. `AttackApplyService.ResolveAttack` gained the same parameter purely as a pass-through, since it is `AttackEvaluationService.EvaluateAttack`'s only caller.
- **Recon's "no real production caller" claim was independently re-verified**, not assumed, exactly as the ТЗ required: a grep across `Assets/` for all three services and their request types found nothing; the one textual hit (`AttackEvaluationService.ActivateAbility` — actually `ActivateAbilityService.ActivateAbility` — mentioned in `CheckService.cs`) was read directly and confirmed to be a doc comment, not a call. No client file was touched.

### Findings (reported, not fixed silently)
- No new findings beyond the two already carried forward from `ODY-S10-103`/`105`: `RedactCharacterForExport.cs`/`CharacterExportContracts.cs` remains a deferred, separate question for the product owner.
- With this task, the `ActorIsMainGm` pattern is closed everywhere in the project except that one deliberately-deferred file.
- No file of the characters/board/content-catalog/dice-inventory subsystems, or of the already-closed `ODY-S10-103` combat-effects points, is in this diff.

### Blockers
None.
