# ODY-S10-103 — Stored MainGM Check in the Combat/Effects Subsystem

## 1. Task identity
`ODY-S10-103`; status: In Review (Draft PR, merge deferred to the product owner). Step MG-3 of the "replace the client-supplied MainGM flag" sub-track of `SLICE-10` (`docs/tasks/SLICE-10_IMPLEMENTATION_BACKLOG.md`).

## 2. Goal
Make the seven strictly-MainGM-only points of the combat/effects subsystem depend on the stored campaign membership (`CampaignMembershipAuthorization.IsMainGm`) instead of a `bool actorIsMainGm` the caller claims, without breaking the internal rollback of failed ability activations/item uses, which used to lean on a forged `actorIsMainGm: true`.

## 3. Authority
This task's governing ТЗ; `ODY-S10-101` (Application-layer pattern, `BoardMovementService`) and `ODY-S10-102` (Persistence-layer pattern, `SqliteCharacterRepository`).

## 4. In scope
- **Application layer:** `CombatEncounterService.Create`/`Advance` and `ActiveEffectDirectCommandService.CreateDirectActiveEffect` take an `ICampaignRepository` (second parameter, like `BoardMovementService.MoveToken`); `CreateCombatEncounterRequest`/`AdvanceCombatEncounterRequest` lose their `ActorIsMainGm`; the decision is `CampaignMembershipAuthorization.IsMainGm(...)`, a failure is returned as-is (fail closed).
- **Persistence layer:** `SqliteActiveEffectRepository` and `SqliteAttackApplyRepository` take a required `ICampaignRepository` constructor argument (`ArgumentNullException` on `null`); `RemoveActiveEffect`, `ResolveAttackIntervention`, `CompensateAttackOutcome`, `ResolveStackConflict` look the role up and fail closed. `IActiveEffectRepository`/`IAttackApplyRepository` and the `AttackApplyService` wrappers lose the parameter; no logic was added to the wrappers.
- **System rollback (the one substantive decision, §18):** new `IActiveEffectRepository.RemoveActiveEffectAsSystemRollback`; `SqliteActivateAbilityRepository.CompensateAbilityActivation` (and, necessarily, `SqliteUseItemRepository`'s identical compensation) call it instead of `RemoveActiveEffect(..., actorIsMainGm: true, ...)`.
- Tests, catalogue `TC-PERSIST-051`–`058`, the `SLICE-10` backlog, this contract and its plan.

## 5. Out of scope
Any alternative access path (a turn-order or effect-source based right to advance/create/remove) — none exists today for the seven points and none was introduced; characters, board, content catalog, dice/inventory production code; `AttackEvaluationService`/`ActivateAbilityService`/`UseItemService` and their request flags (§18, MG-3b); Unity client; ADRs; `.asmdef`/`.csproj`; verify scripts.

## 6-8. Domain / Application / Persistence
Application: `CombatEncounterContracts.cs`, `ActiveEffectDirectCommandService.cs`, `AttackApplyService.cs`, `AttackApplyRepositoryContracts.cs`, `ActiveEffectRepositoryContracts.cs`. Persistence: `SqliteActiveEffectRepository.cs`, `SqliteAttackApplyRepository.cs`, `SqliteActivateAbilityRepository.cs`, `SqliteUseItemRepository.cs`, `SqliteCombatEncounterRepository.cs` (default construction only). No Domain change.

## 9. Tests and validation
New (`TC-PERSIST-051`–`058`): `051` encounter `Create`/`Advance` (unregistered/Player/Observer denied before any repository call; host and an added MainGm succeed); `052` direct effect creation and `RemoveActiveEffect`; `053` the three attack-apply gates; `054` fail-closed in the Application layer; `055` fail-closed in the Persistence layer (attack apply); `056` fail-closed for `RemoveActiveEffect`/`CreateDirectActiveEffect`, plus proof that the system rollback works with an unreadable membership and performs no lookup; `057` **a failed ability activation by an ordinary Player is fully rolled back** (created effects removed, cost reversed); `058` the same for a failed item use (effect removed, unit restored). A deliberate mutation (the compensation routed back through the MainGM-gated `RemoveActiveEffect`) makes `057` fail (`Expected: 0, But was: 2` attached effects); it was reverted.

**Existing tests changed, and why:** every construction of `SqliteActiveEffectRepository`/`SqliteAttackApplyRepository` gained a campaign repository; every call of the seven operations and of the two request constructors lost the flag — `true` became the campaign's registered host as the acting user (`DevIdentityProvider.AssignHost()`), `false` simply dropped the flag so the actor is judged by the stored role; the two `IActiveEffectRepository` test doubles that fail the Nth removal (`ActivateAbilityIntegrationTests`, `UseItemIntegrationTests`) now count and fail `RemoveActiveEffectAsSystemRollback` — the call compensation really makes — so the "compensation itself fails partway" tests keep their meaning.

## 10-17. (see plan)
Same shape as `ODY-S10-101`/`102`.

## 18. Change control

### Decisions made during execution
- **System rollback: a separate interface method, not `internal` and not a flag.** The ТЗ preferred an `internal` core method reached from `SqliteActivateAbilityRepository`. That does not work here: both callers hold the collaborator as `IActiveEffectRepository`, and two test doubles implement that interface to inject a failing Nth removal — an `internal` side channel would bypass them and silently change what the compensation tests test (and a test assembly cannot implement an internal interface; the project has no `InternalsVisibleTo` and `.csproj` is off-limits). The ТЗ's fallback alternative was a "system rollback" *parameter*; that would be a second way for any caller to claim the exemption, i.e. the forged flag again. So: a distinct operation, `RemoveActiveEffectAsSystemRollback`, with **no role parameter at all**, sharing one private `RemoveActiveEffectCore` with the gated `RemoveActiveEffect`. Its trade-off is stated plainly: it is on the public interface (not reachable from any user command, but callable by any code holding the repository). The doc comment says it is for persistence-layer compensation only.
- **Application services take the repository as a parameter,** persistence classes as a constructor argument — exactly the two precedents.
- **`SqliteCombatEncounterRepository`'s default effect repository** (`activeEffects ?? new SqliteActiveEffectRepository(_clock)`) needed a campaign repository; it now defaults to `new SqliteActiveEffectRepository(_clock, new SqliteCampaignRepository(_clock))`. That class only ever lists/expires effects through it, so no MainGM lookup happens there.

### Findings (reported, not fixed silently)
- **`AttackEvaluationService.cs:36` is a live authorization point, not a dead field** (the ТЗ expected it to be unused). `AttackRequest.ActorIsMainGm` is read at line 113 in `AuthorizeAndRead`: `if (!request.ActorIsMainGm) { …CanControlActor… }` — a *controller-or-MainGM* gate. The same shape exists in `ActivateAbilityService.cs:183` and `UseItemService.cs:152`. Per the ТЗ's instruction to record a doubtful field as a finding rather than touch it blindly, none of the three was changed and `AttackEvaluationService.cs` is **not in this diff**. They are a different shape (the controller test goes through a reader port) and belong together in a follow-up, recorded in the backlog as MG-3b. Consequence: until then, attack evaluation, ability activation and item use still trust the caller's MainGM claim.
- **A second rollback caller** the ТЗ did not list: `SqliteUseItemRepository.cs` used the same forged `actorIsMainGm: true` in its own compensation; it had to be switched too (else it would have started denying ordinary Players).
- **Paths outside the ТЗ list (necessary):** `SqliteUseItemRepository.cs` (above), `SqliteCombatEncounterRepository.cs` (default construction), `ActivateAbilityContracts.cs` (one stale doc comment that described the forged flag), the twenty test files that construct the two repositories or call the seven operations (the ТЗ named sixteen; the extra ones only needed the constructor argument or a flag removal: `ActiveEffectExpiryRulesTests`, `ActiveEffectRepositoryTests`, `ActiveEffectStackingRulesTests`, `ItemEffectLifecycleTests`).
- No file of the characters, board, content-catalog or dice/inventory subsystems was touched.
- Unity was not re-run: no client file changed, and the production edits are plain C# in the Application/Persistence assemblies that `dotnet build` compiles from the same sources.

### Blockers
None.
