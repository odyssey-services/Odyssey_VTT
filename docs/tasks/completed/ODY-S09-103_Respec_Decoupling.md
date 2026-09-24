# ODY-S09-103 — Decoupling of Respec from `Odyssey.Rules` (SLICE-09 block 3)

## 1. Task identity
`ODY-S09-103`; status: Done (PR #176, merged into main). Block 3 of `SLICE-09` (`docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md`); follows `ODY-S09-102` (block 2, merged).

## 2. Goal
Remove the respec plan computation, and with it the last Rules use in the respec path, from `SqliteCharacterRepository`: `ComputeRespecPlan` (private helper), `PreviewCharacterRespec` and `ApplyCharacterRespec`. After this task none of the three contains an `Odyssey.Rules` reference and Persistence no longer computes the plan (`ADR-001` §5; `ADR-024`/`ADR-025`).

## 3. Authority
This task's governing ТЗ §0-§7; backlog block 3; `ADR-001` §5, `ADR-024` 192-194, `ADR-025` 185-187 (read, not revised); the block-2 pattern (decision in `CharacterAdvancementService`, Persistence always called and checks the decided values against its own invariants).

## 4. In scope
- `CharacterAdvancementService`: `ComputeRespecPlan` moved in (private, verbatim in outcome), `PreviewCharacterRespec`, `ApplyCharacterRespec`. Block-1/2 methods unchanged.
- `ICharacterRepository`: `PreviewCharacterRespec` removed; `ApplyCharacterRespec(..., targets, decidedPlan, decidedMechanicsRevision, reasonCode, ...)`.
- `SqliteCharacterRepository`: `ComputeRespecPlan` and `PreviewCharacterRespec` removed; `ApplyCharacterRespec` executes the decided plan after checks.
- 9 existing test call sites in 2 files moved to the service; the two hand-written repository decorators followed the signatures.
- Tests `TC-CHAR-203`-`209` (new file `CharacterAdvancementRespecServiceTests.cs`); `test-catalog.json`; backlog; this contract and plan.

## 5. Out of scope
`PreviewCharacterRulesetMigration` / `ApplyCharacterRulesetMigration` / `RevertCharacterRulesetMigration` (block 4); the architecture scripts (block 5); block-1/2 methods; any formula change; ADRs; `.asmdef` / `.csproj`.

**Stated consequence, not hidden:** `SqliteCharacterRepository` still has `using Odyssey.Rules.Character;` and Rules calls in ruleset migration (`RulesetMigrationRules.BuildPlan`), so the Unity client stays unbuildable until blocks 4-5. The attribute/skill cost aliases (`RulesAttributeCostRules`, `RulesSkillCostRules`) are now unused in the file and were removed with the code that used them.

## 6. Domain contract
Unchanged.

## 7. Application contract
- **Preview** is a pure read: `GetCharacter` then `GetAdvancementPurchases`, then the plan. It has no revision argument and no gate, exactly as the repository query had (two autocommit SELECTs there as well, so the snapshot guarantee is unchanged). Because it needed nothing from Persistence beyond those two existing reads, it no longer has a repository method.
- **Apply recomputes the plan on every call** and takes no preview from the caller. Reason: a plan built earlier by the caller can be stale (the existing test `ApplyCharacterRespec_RecomputesServerSide_IgnoringAnyStalePreview` pins that), and recomputing from the current state is the CAP-INV-004 rule this method already followed. The recomputation is done in the service; the repository verifies it.
- The service reads the character first and the purchase history second and tags the plan with the character's `MechanicsRevision`: any mutation in between (every purchase, revert, resolution and respec bumps it) makes the tag stale.
- Nothing short-circuits: the repository is always called. On a failed read, or an unsupported target kind, it is called with an empty plan (and revision 0 for a failed read), and reports the failure the old single-method implementation reported, in the same order (reason required, main-GM only, not found, revision, unsupported kind).

## 8. Persistence boundary
`ApplyCharacterRespec` holds no Rules reference and computes no plan. Inside its transaction and after the existing character/revision checks it (1) rejects an unsupported target kind, (2) requires `MechanicsRevision == decidedMechanicsRevision` else `CharacterRevisionConflict` (existing code, no new one), (3) checks every plan entry against the locked purchase history: a Return entry must name an Applied purchase of the same kind, target and cost; a Spend entry must belong to an addressed target with a positive desired value and carry no source purchase (else `CharacterRevisionConflict`). It then writes the plan exactly as before (events, ledger, purchase statuses, snapshots, totals). The persisted results, event payloads and history are unchanged.

## 9. Tests and validation
The 828 existing Persistence tests pass through the service with unchanged assertions. New: multi-target preview with literal numbers (`203`); preview is a pure read, only reads reach the repository, nothing changes (`204`); legal multi-target apply with literal resulting state, ledger and purchase statuses (`205`); rejected cases write nothing and keep their typed errors and ordering (`206`); stale plan / superseded purchases rejected (`207`); real race injected between the service's read and its commit, both with the caller holding the start revision and the fresh one (`208`); port-only use with an interception double, always-call-apply, and sentinels (`209`). Mutation check: with the `decidedMechanicsRevision` check disabled, `207` and `208` fail (verified); in `208` a stale read would otherwise build an empty plan and report success.

## 10. Compatibility and rollback
Outcomes (plan, cost, applied state, events) are unchanged for callers going through the service. `ICharacterRepository` loses `PreviewCharacterRespec` and changes `ApplyCharacterRespec`; no production caller exists, tests only. Rollback: revert the PR.

## 11. Security and privacy
Unchanged: only the main GM may apply; that check and the reason check still run before any state is read by the repository.

## 12. Observability
Unchanged journal events and payloads.

## 13. Performance
Apply performs two extra reads (character, purchases) before the transaction; Preview costs the same two reads it always did.

## 14. Dependencies
`ODY-S09-102`.

## 15. Dependencies (packages)
None.

## 16. Implementation plan
`docs/plans/active/ODY-S09-103_Respec_Decoupling.md`.

## 17. Completion evidence
`dotnet test` green; `verify-format`, `verify-repository`, `verify-test-structure` PASS; grep: no `Odyssey.Rules` / `Rules*CostRules` in the respec code of `SqliteCharacterRepository` (only ruleset migration remains); `git diff --name-status` limited to the paths in §4 (plus the disclosed items below).

## 18. Change control

### Decisions made during execution
- `PreviewCharacterRespec` was **removed from `ICharacterRepository`** rather than given a new signature. The ТЗ §4 speaks of changing signatures; a signature that would still let Persistence compute the Return half of the plan contradicts §2 ("Persistence must not compute the plan"), and a signature that takes the plan makes no sense for a query that returns it. The service builds it from two reads the port already has.
- Apply recomputes the plan (ТЗ §1.1 asked for a justified choice): see §7.
- The staleness guard is one revision tag on the whole plan (`decidedMechanicsRevision`), not per-value bases as in block 2, because a respec plan depends on several attributes and skills plus the purchase history, all of which change only through mutations that bump `MechanicsRevision`. This relies on that invariant; the per-entry check against the locked purchase history is a second, independent net for the purchase side.
- Pattern of block 2 kept: no short-circuit, the repository is always called (see §7).

### Findings (reported, not fixed -- out of scope)
- New test file `CharacterAdvancementRespecServiceTests.cs` (ТЗ §4 lists existing test files); it reuses the `DispatchProxy` double from `CharacterAdvancementCostServiceTests`.
- Hand-written repository decorators in `CharacterResourceAnatomyTests.cs` and `CheckIntegrationTests.cs` followed the signatures (forced by the interface change); `TC-CHAR-188`'s expected method list was extended to the eight public service methods.
- A respec `TargetDefinitionId` that cannot be parsed as an id now throws in the service before the repository is reached; before, the same exception was thrown from inside the repository transaction. The exception type is the same; the order relative to the reason / permission checks differs only for such malformed input.
- Unity remains unbuildable on `main` until blocks 4-5.

### Blockers
None.
