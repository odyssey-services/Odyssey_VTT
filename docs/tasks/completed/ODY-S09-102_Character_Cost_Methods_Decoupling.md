# ODY-S09-102 — Decoupling of the Character "Cost" Methods from `Odyssey.Rules` (SLICE-09 block 2)

## 1. Task identity
`ODY-S09-102`; status: Done (PR #175, merged into main). Block 2 of `SLICE-09` (`docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md`); follows `ODY-S09-101` (block 1, merged).

## 2. Goal
Move the legality and cost decisions of four `SqliteCharacterRepository` methods out of Persistence and into `CharacterAdvancementService` (Application), which is allowed to use `Odyssey.Rules`: `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RequestSkillAdvancedRecommendation`, and `AcquireAbility` (its private `AcquireAbilityViaProgressionPurchase`, i.e. only `SourceKind.ProgressionPurchase`). After this task those four repository methods contain no `Odyssey.Rules` reference (`ADR-001` §5; `ADR-024`/`ADR-025`: Persistence does not decide legality).

## 3. Authority
This task's governing ТЗ §0-§7; `docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md` block 2; `ADR-001` §5, `ADR-024` 192-194, `ADR-025` 185-187 (read, not revised); the revision-gated commit precedent of `ODY-S07-105`/`106`.

## 4. In scope
- Four new methods on `CharacterAdvancementService`, next to the block-1 ones (block-1 methods unchanged). Each takes the repository port first and otherwise the argument list of the corresponding repository method, so test migration is mechanical.
- Changed `ICharacterRepository` / `SqliteCharacterRepository` signatures of exactly these four methods:
  - `PurchaseAttributeIncrease(..., toValue, decidedFromValue, exceedsNormalCap, decidedCost, ...)`
  - `PurchaseSkillLevel(..., toLevel, decidedFromLevel, requiresRecommendation, decidedCost, ...)`
  - `RequestSkillAdvancedRecommendation(..., targetLevel, decidedFromLevel, decidedReservedAmount, evidenceIds, ...)`
  - `AcquireAbility(..., configuration, progressionPurchaseCost, ...)`
- 82 existing test call sites in 12 files moved to the service; assertions unchanged.
- Tests `TC-CHAR-189`-`202` (new file `CharacterAdvancementCostServiceTests.cs`); `test-catalog.json`; backlog row/status update; this contract and plan.

## 5. Out of scope
`PreviewCharacterRespec` / `ApplyCharacterRespec` / `ComputeRespecPlan` (block 3); ruleset migration (block 4); tightening `verify-test-structure.ps1` (block 5); `InitializeCharacterResource` / `InitializeCharacterAnatomy` and the block-1 service methods; any change of a cost formula, cap or default; any ADR; any `.asmdef` / `.csproj`; the non-`ProgressionPurchase` paths of `AcquireAbility` (they never used Rules and are untouched).

**Stated consequence, not hidden:** `SqliteCharacterRepository` still has `using Odyssey.Rules.Character;` and Rules calls in respec (`ComputeRespecPlan`) and ruleset migration, so the Unity client stays unbuildable until blocks 3-5 are done.

## 6. Domain contract
Unchanged.

## 7. Application contract
`CharacterAdvancementService` (`Odyssey.Application.CharacterAdvancement`, static, `CheckService` form) reads the Character through the existing `ICharacterRepository.GetCharacter` (no new read method), applies `AttributeCostRules` / `SkillCostRules` / `AbilityCostRules`, and calls the repository with the decision plus the value it was based on. Up-front argument guards are repeated in the service in the original order, so an invalid call throws the same exception before any repository call (including the read). The ability cost is a constant needing no Character state, so `AcquireAbility` does not read.

## 8. Persistence boundary
The four methods hold no Rules reference. Under the transaction lock each re-checks that the locked `BaseValue` / `Level` still equals `decidedFromValue` / `decidedFromLevel` and otherwise returns the existing `CharacterRevisionConflict` (no new error code: a suitable one already existed and is what the neighbouring revision gates return). This is in addition to the existing `MechanicsRevision`, entry-level revision and `CharacterAbilitiesRevision` gates, and closes the read-then-commit race independently of them: a decision taken from a stale read can never be written, even when the caller holds current revisions. Failure order is preserved: mechanics revision, permission, (skill: recommendation required), entry-level revision, stale basis, non-increase (argument error), (attribute: cap), balance.

## 9. Tests and validation
Per method: legal case with literal numbers (attribute 2/point: 0 -> 3 costs 6, 3 -> 5 costs 4; skill 3/point: 0 -> 2 and 2 -> 4 cost 6 each; recommendation 2 -> 5 reserves 9; ability 5); illegal cases with a full before/after snapshot (pool, attributes, skills, revisions, ledger and purchase counts) proving no write; stale-decision race (direct repository call with current revisions, and through the service with an injected race between its read and its commit); interception double proving the service uses only the port and passes the Rules decisions through. Mutation check: with the basis re-check disabled the four race tests fail (verified), so they are not tautological. The 813 pre-existing Persistence tests pass unchanged through the service.

## 10. Compatibility and rollback
Behaviour of the four operations is unchanged for callers going through the service, including failure precedence. The port signatures change (no production caller exists; tests only). Rollback: revert the PR.

## 11. Security and privacy
No change in who may act: the permission check stays in Persistence and precedes every Rules-derived failure, so an unauthorized actor still gets `CharacterDevelopmentPurchaseDenied` and learns nothing about caps.

## 12. Observability
Unchanged journal events and payloads.

## 13. Performance
Three of the four commands perform one extra `GetCharacter` read before the commit.

## 14. Dependencies
`ODY-S09-101`.

## 15. Dependencies (packages)
None.

## 16. Implementation plan
`docs/plans/active/ODY-S09-102_Character_Cost_Methods_Decoupling.md`.

## 17. Completion evidence
`dotnet test` green; `verify-format`, `verify-repository`, `verify-test-structure` PASS; grep of the four method bodies shows no `Odyssey.Rules` while `ComputeRespecPlan` and the migration methods still have it; `git diff --name-status` limited to the paths listed in §4 (plus the disclosed test files below).

## 18. Change control

### Decisions made during execution
- **Deliberate deviation from ТЗ §1.1 point 3 ("if illegal, return `Failure` without touching Persistence at all").** The service always hands the decision to the repository, which reports the failure. Reason: the ТЗ's own invariant "behaviour byte-for-byte the same" outranks it. Today an unauthorized actor asking for an over-cap purchase gets `CharacterDevelopmentPurchaseDenied`, and a stale attribute revision is reported before the cap; a service that short-circuits on the Rules verdict would answer `CharacterAttributeCapExceeded` instead, and would tell an unauthorized actor the caps. The permission check needs the persistence clock (`IsAssignedCharacter(ownership, actor, now)`), so it cannot be evaluated in the static service. Nothing is written on any failure (tested with full snapshots). The decision itself (cap, recommendation gate, cost) is still taken by Rules in the service; the repository does only ordering, the stale-basis check, and the balance comparison against the locked pool (pure arithmetic, no Rules). If the product owner prefers strict short-circuit and accepts the precedence change, that is a small follow-up.
- Decision inputs are exactly the state-derived values Rules needs (`from` value/level); cost and cap depend on nothing else, so the stale-basis check is a complete guard. The pool balance is compared in Persistence against the locked pool.
- The revision-gate technique is the existing `expectedRevision` + typed conflict pattern; no new error code, no new read method.
- `TC-CHAR-188` (block 1) asserted that the service exposes exactly two methods; that assertion was extended to the six public methods. Block-1 service code is unchanged.

### Findings (reported, not fixed -- out of scope)
- New test file `CharacterAdvancementCostServiceTests.cs` (the ТЗ §4 lists existing test files); it uses `DispatchProxy` over the real repository as the test double instead of a fourth hand-written 50-member decorator.
- Hand-written repository decorators in `CharacterResourceAnatomyTests.cs` (`RecordingCharacterRepository`) and `CheckIntegrationTests.cs` forward the four changed members and had to follow the signatures (forced by the interface change).
- The block-1 backlog text said the four methods contain five Rules call sites; the correct count is six (corrected in the backlog).
- The remaining doc comment on `PurchaseSkillLevel` in the port still says cost "comes from" `SkillCostRules`; kept as historical description, the new paragraph states where the decision now lives.
- Unity remains unbuildable on `main` until block 5.

### Blockers
None.
