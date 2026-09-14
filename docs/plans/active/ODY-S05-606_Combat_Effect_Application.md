# ExecPlan — ODY-S05-606 Combat Effect Application

## 1. Purpose
Implement ADR-029 section 8: apply an accepted attack's own Apply effect candidates as ActiveEffect rows, in the same atomic-apply transaction, routed through the existing ADR-028 EffectStackPolicy decision layer.

## 2. Scope
Backward-compatible AttackEffectCandidate extension, AttackOutcomeRecord/IAttackApplyRepository candidate persistence, embedded ActiveEffect insert/update inside SqliteAttackApplyRepository's own transaction; no new repository interface method, no AttackDelta/Character-Item state, no compensation/projection, no expiry-mechanism change.

## 3. Non-goals
No EffectApplicationDecision computation (603's own job, unmodified); no ICombatEncounterRepository/IInventoryRepository/ICharacterRepository/IActiveEffectRepository interface change; no durable ActiveEffectStackConflict persistence; no per-candidate intervention resolution.

## 4. Architecture
Domain: AttackEffectCandidate gains reason category/host-only detail/CombatDurationBinding/EffectStackPolicy/EffectMechanicsSnapshot via a new constructor overload (old one preserved). Application: AttackOutcomeRecord/IAttackApplyRepository carry EffectCandidates through; AttackApplyService passes them unchanged. Persistence: SqliteAttackApplyRepository's own apply lambdas (RecordAttackOutcome Accepted path, ResolveAttackIntervention Approve path) call a new ApplyEffectCandidates step that reads any conflicting ActiveEffect row on the same connection/transaction, calls the existing ActiveEffectStackingRules.ResolveStacking, and writes the resulting mutation via SqliteActiveEffectRepository's own SQL helpers (made internal), never its public CreateActiveEffect.

## 5. Milestones
M1 read ADR-029 section 8/9 verbatim, ActiveEffectStackingRules (503), CombatDurationBinding (605), SqliteSavingPipeline's own one-connection-per-Execute-call confirmation; M2 extend AttackEffectCandidate; M3 extend AttackOutcomeRecord/IAttackApplyRepository/AttackApplyService; M4 embed ActiveEffect apply + stacking in SqliteAttackApplyRepository; M5 real tests; M6 validation/PR.

## 6. State and data flow
Rules (603, unmodified) produces EffectCandidates as part of ProposedAttackResolution -> AttackApplyService.ResolveAttack passes them to RecordAttackOutcome (persisted as EffectCandidatesJson) -> on Accepted (immediate or via ResolveAttackIntervention's Approve), ApplyEffectCandidates iterates Apply-decided candidates, resolves stacking against any existing conflicting row, and inserts/updates ActiveEffect -- all inside the same transaction as the AttackOutcome row and Game Log entry.

## 7. Error handling
A guard failure inside the unmodified stage 1-11 evaluator rejects before RecordAttackOutcome is ever called (no partial commit possible). Inside the apply transaction, any failure rolls back everything (AttackOutcome, Game Log, ActiveEffect rows) via the same SqliteTransaction. RequestGMResolution collisions are a deliberate no-op for that one candidate, not a failure.

## 8. Test strategy
Real SQLite fixtures extending 604's own AttackApplyTests.cs pattern: Apply/DoNotApply/RequiresIntervention-approved/rejected row-creation behavior; direct SQL check of EffectCandidatesJson persistence across the pending-to-resolved round trip; whole-database row-count atomicity check under a guard failure; two real stacking scenarios (IncreaseStacks, RequestGMResolution) proving reuse of the existing decision layer; an architecture guard proving CreateActiveEffect is never called from the combat apply path.

## 9. Validation and acceptance evidence
dotnet build/dotnet test full solution green; verify-format.ps1/check-repository-policy.ps1/verify-test-structure.ps1 green; git diff --name-status against main confined to the ТЗ's allowed paths, no 605/607/609 territory touched.

## 10. Recovery and rollback
One new nullable-safe column on the existing AttackOutcome table; no new table. Reverting this task's source and column leaves 602-605 and all prior campaign state unaffected.
