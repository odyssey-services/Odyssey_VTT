# ExecPlan — ODY-S05-609 Attack Aggregate Delta Commit

## 1. Purpose
Close 604's own disclosed gap (ADR-029 section 1 rule 5, section 6 stage 13, section 12 item 4): apply an accepted attack's own already-computed AttackDelta values (DamageDeltas/CostDeltas) against concrete Character resource state, in the same atomic-apply transaction as 604's own outcome/Game Log commit.

## 2. Scope
A new, explicit TargetRef addressing convention (character:{id}:{resourceKind} / item:{id}:{resourceKind}); persisting DamageDeltas/CostDeltas on AttackOutcomeRecord/AttackOutcome (new JSON columns, mirroring 606's own EffectCandidatesJson); embedded delta application inside SqliteAttackApplyRepository's own transaction (never the public SetResourceCurrentValue); escalating (not building) the item-targeted path as a disclosed blocker; tests.

## 3. Non-goals
No Ruleset formula computation (603's own job, unmodified); no new ICombatEncounterRepository/IInventoryRepository method (the item-side blocker is escalated, not built around); no new Domain type; no rewrite of 602/603/604/606/607's own already-accepted production behavior; no compensation infrastructure for 609's own deltas.

## 4. Architecture
Application: AttackOutcomeRecord/IAttackApplyRepository carry DamageDeltas/CostDeltas through (mirroring EffectCandidates); AttackApplyService passes them unchanged. Persistence: SqliteAttackApplyRepository gains DamageDeltasJson/CostDeltasJson columns and, inside its own apply lambdas (RecordAttackOutcome Accepted path, ResolveAttackIntervention Approve path), a new ApplyAttackDeltas step that resolves each delta's TargetRef (character: or item: prefix, exactly three colon-segments) and applies character-targeted deltas via a minimal raw SQL SELECT/UPDATE against Character.ResourcesJson/CharacterResourcesRevision/CharacterRevision on the same connection/transaction, reusing SqliteCharacterRepository.SerializeResources/DeserializeResources (made internal). Item-targeted deltas always fail as a disclosed blocker.

## 5. Milestones
M1 verify 604's actual current state against this contract's own original (found to be inaccurate) draft, confirm no delta persistence/application exists yet; M2 read SetResourceCurrentValue/MutateResources/CharacterResource/ResourceDefinitionId/ItemInstanceRecord verbatim to confirm the transaction-boundary problem, the MainGM-gate problem, and the item-side Domain gap; M3 persist DamageDeltas/CostDeltas on AttackOutcomeRecord/IAttackApplyRepository/AttackApplyService/SqliteAttackApplyRepository (mirroring EffectCandidatesJson); M4 implement TargetRef resolution + ApplyAttackDeltas/ApplyCharacterResourceDelta, make SerializeResources/DeserializeResources internal; M5 fix three pre-existing test fixtures' own placeholder AttackDelta values (empty lists) once regression breakage is found; M6 real tests (TC-ATTACK-066-075); M7 validation/PR.

## 6. State and data flow
Rules (603, unmodified) produces DamageDeltas/CostDeltas as part of ProposedAttackResolution -> AttackApplyService.ResolveAttack passes them to RecordAttackOutcome (persisted as DamageDeltasJson/CostDeltasJson) -> on Accepted (immediate or via ResolveAttackIntervention's Approve), ApplyAttackDeltas iterates DamageDeltas then CostDeltas, resolving each TargetRef and applying the already-computed Value against the resolved CharacterResource -- all inside the same transaction as the AttackOutcome row and Game Log entry.

## 7. Error handling
An unparseable/unrecognized TargetRef, a not-found character/resource, an out-of-range resulting value, or an item-targeted delta each returns a distinct typed failure from the apply lambda itself before any commit -- SqliteSavingPipeline rolls back the WHOLE transaction, including the just-inserted AttackOutcome row, exactly like any other guard failure in this class. Multiple deltas on the same character apply sequentially within the same transaction, so no CAS/revision conflict between them is possible.

## 8. Test strategy
Real SQLite fixtures extending 604's/606's/607's own AttackApplyTests.cs/AttackEffectApplicationTests.cs/AttackCompensationAndAudienceTests.cs pattern (same SetUp/Active/ItemFor/CreateEncounter/Request/Command/User helpers, plus new InitResource/CurrentValue helpers and a Rules fixture that accepts injectable delta lists): persistence across the pending/resolved round trip (direct SQL check); immediate-accept applies the delta; approved-intervention applies it only on that step; rejected-intervention never applies it; cross-character and same-character multi-delta atomicity; item-targeted delta rejected as the disclosed blocker; unparseable TargetRef rejected; out-of-range value rolls back the whole transaction; an architecture guard (source-text scan, comments stripped) proving no SetResourceCurrentValue/ICharacterRepository/IInventoryRepository reference in the delta-application code path.

## 9. Validation and acceptance evidence
dotnet build/dotnet test full solution green (984 tests: 974 pre-existing + 10 new, after fixing three pre-existing fixtures' own placeholder delta values); verify-format.ps1/check-repository-policy.ps1/verify-test-structure.ps1 green; git diff --name-status against main confined to the task's own allowed paths, no 605/606/607/610 territory or ADR documents touched.

## 10. Recovery and rollback
Two new nullable-safe columns (DEFAULT '[]') on the existing AttackOutcome table; no new table. SqliteCharacterRepository's public surface/constructor unchanged (two private methods made internal); SqliteAttackApplyRepository's constructor unchanged. Reverting this task's source and columns leaves 602-607 and all prior campaign state unaffected.
