# ExecPlan — ODY-S05-607 Compensation and Game Log Projections

## 1. Purpose
Implement ADR-029 section 1 rule 7 and section 6 stages 14-15: a MainGM-only, reason-coded compensating root command for an already-accepted attack's Game Log/AttackOutcome bookkeeping, plus expansion of the attack Game Log entry's audience to all combat-encounter participants + GM via the existing DiceRollAudienceKind mechanism.

## 2. Scope
New CompensateAttackOutcome repository method/contract and its SqliteAttackApplyRepository implementation (new separate transaction/CommandId, never mutates the original record); audience computation at write time in WriteAcceptedAttackGameLogEntry; three new error codes; tests. No 609 resource-delta compensation infrastructure, no field-level redaction, no ICombatEncounterRepository method addition, no rewrite of DiceRollVisibilityPolicy/GameLogReconnectService/SqliteGameLogRepository.

## 3. Non-goals
No new ICombatEncounterRepository/ICharacterRepository constructor dependency on SqliteAttackApplyRepository; no RNG re-roll; no compensation of Character/Item resource state; no per-scenario-specific compensation command (general-purpose only); no rewrite of 604's RecordAttackOutcome/ResolveAttackIntervention/GetOutcome.

## 4. Architecture
Application: new AttackCompensationRecord type and IAttackApplyRepository.CompensateAttackOutcome contract in AttackApplyRepositoryContracts.cs; AttackApplyService.CompensateAttackOutcome delegates unchanged. Persistence: SqliteAttackApplyRepository.CompensateAttackOutcome mirrors RevertCharacterRulesetMigration's own shape (MainGM-gate first, mandatory reason code, original-record lookup, re-compensation guard, new PipelineWrite with originalEventId/compensationGroupId/isCompensating: true); a new ComputeEncounterAudience helper reads CombatEncounterParticipant/Character via raw SQL on the same connection/transaction and is called both by the new compensation path and by the existing WriteAcceptedAttackGameLogEntry (replacing its hardcoded PlayerAndGM literal). Read side unchanged: SqliteGameLogRepository.ListGameLog/GameLogReconnectService.GetVisibleEntries/DiceRollVisibilityPolicy already handle SelectedParticipants correctly.

## 5. Milestones
M1 read RevertCharacterRulesetMigration/DiceRollVisibilityPolicy/GameLogReconnectService/SqliteGameLogRepository/DomainEvents schema/Character schema verbatim; M2 add AttackCompensationRecord/IAttackApplyRepository.CompensateAttackOutcome/AttackApplyService delegation/error codes; M3 implement ComputeEncounterAudience + wire it into WriteAcceptedAttackGameLogEntry; M4 implement CompensateAttackOutcome + helpers (ReplayCompensation/FindCommittingEventSequence/IsAlreadyCompensated/WriteCompensatingGameLogEntry); M5 real tests (TC-ATTACK-056-065); M6 validation/PR.

## 6. State and data flow
MainGM issues CompensateAttackOutcome(resolveAttackCommandId, reasonCode, correctedSummaryPayload) -> gate checked -> original AttackOutcomeRecord looked up and validated (Accepted, not already compensated) -> new DiceRolls + GameLogEntries rows written inside a brand-new SqliteSavingPipeline.Execute call with its own CommandId, linked to the original committing event via originalEventId/compensationGroupId/isCompensating. Independently, at original-attack-commit time, WriteAcceptedAttackGameLogEntry now calls ComputeEncounterAudience to populate AudienceKind/AudienceSelectedUserIdsJson with the full encounter-participant-owner list instead of a hardcoded PlayerAndGM value -- the existing GameLogReconnectService/DiceRollVisibilityPolicy read path requires no change to correctly filter by this wider audience.

## 7. Error handling
MainGM-gate failure, empty reason/summary, original record not found, original record not Accepted, and already-compensated are each a distinct new-or-reused error code returned before any write. A guard failure inside CompensateAttackOutcome's apply lambda leaves no partial row (single transaction, same SqliteSavingPipeline machinery as every other command in this codebase). Compensation is itself idempotent under the SqliteSavingPipeline's own replay-by-CommandId mechanism (a retry with the same compensating CommandId returns the original correction, not a second one) -- this is distinct from, and does not weaken, the guard against a SECOND, DIFFERENT compensating command correcting the same already-compensated event.

## 8. Test strategy
Real SQLite fixtures extending 604's/606's own AttackApplyTests.cs/AttackEffectApplicationTests.cs pattern (same SetUp/Active/ItemFor/CreateEncounter/Request/Command/User helpers): compensation creates a new row without mutating the original (direct SQL check); non-MainGM/empty-reason denial with no mutation; repeat-compensation guard; separate-transaction/CommandId plus replay-idempotency check; expanded-audience visibility for a non-attacker participant via GameLogReconnectService.GetVisibleEntries; fail-closed for an outsider; an architecture guard (source-text scan of the CompensateAttackOutcome method body) proving no ICharacterRepository/SetResourceCurrentValue/IInventoryRepository/AttackDelta reference; compensating a non-Accepted outcome rejected; an all-NPC encounter falling back to GMOnly audience.

## 9. Validation and acceptance evidence
dotnet build/dotnet test full solution green (974 tests); verify-format.ps1/check-repository-policy.ps1/verify-test-structure.ps1 green; git diff --name-status against main confined to the ТЗ's allowed paths, no 605/606/609/610 territory or ADR documents touched.

## 10. Recovery and rollback
No schema change (DomainEvents.CompensationGroupId/IsCompensating and GameLogEntries/DiceRolls already exist from prior tasks). IAttackApplyRepository gains one new method (backward compatible); SqliteAttackApplyRepository's constructor is unchanged. Reverting this task's source leaves 602-606 and all prior campaign state unaffected.
