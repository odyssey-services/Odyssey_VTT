# ODY-S09-104 — Decoupling of Ruleset Migration from `Odyssey.Rules` (SLICE-09 block 4)

## 1. Task identity
`ODY-S09-104`; status: In Review (Draft PR, merge deferred to the product owner). Block 4 of `SLICE-09` (`docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md`); follows `ODY-S09-103` (block 3, merged).

## 2. Goal
Remove the last `Odyssey.Rules` references from `SqliteCharacterRepository`: `PreviewCharacterRulesetMigration` and `ApplyCharacterRulesetMigration` (both called `RulesetMigrationRules.BuildPlan`, and both port signatures carried Rules types). After this task the whole file, and the whole `com.odyssey.persistence` package, has no `Odyssey.Rules` reference.

## 3. Authority
This task's governing ТЗ §0-§7; backlog block 4; `ADR-001` §5, `ADR-024`/`ADR-025` (read, not revised); the block 2-3 pattern.

## 4. In scope
- `CharacterAdvancementService`: `PreviewCharacterRulesetMigration`, `ApplyCharacterRulesetMigration`. Blocks 1-3 unchanged.
- `ICharacterRepository`: `PreviewCharacterRulesetMigration` removed; `ApplyCharacterRulesetMigration` re-signed with flat values only.
- `SqliteCharacterRepository`: both methods reworked/removed, `using Odyssey.Rules.Character;` removed.
- 25 existing test call sites in `CharacterRulesetMigrationTests.cs` moved to the service; the stale-plan test rewritten (see §18); the two hand-written repository decorators followed the signatures.
- Tests `TC-CHAR-210`-`216` (new file `CharacterAdvancementRulesetMigrationServiceTests.cs`); `test-catalog.json`; backlog; this contract and plan.

## 5. Out of scope
The Rules layer itself (`CharacterRulesetMigrationPlan`, `PreviewHash`, `RulesetMigrationRules` are unchanged and still used by the service); the architecture scripts (block 5); `RevertCharacterRulesetMigration` (checked, see §18); blocks 1-3; ADRs; `.asmdef` / `.csproj`.

**Stated consequence, not hidden:** Persistence no longer references `Odyssey.Rules`, but the Unity client is still not proven buildable, and the architecture checks still do not detect this class of violation. Both are block 5.

## 6. Domain contract
Unchanged.

## 7. Application contract
- **Preview**: pure read (`GetCharacter`, then `BuildPlan`), no revision, no gate; removed from the port because it needs nothing from Persistence beyond an existing read (same reasoning as `PreviewCharacterRespec`, block 3; no production caller of the port method exists, only tests).
- **Apply** takes no plan from the caller: target id/version, catalog, actor, command. It reads the Character once, builds the plan from that read, and **always calls the repository**, passing the target version, `hasUnresolvedDecisions`, the mapping count and the state the plan was built from. It does not short-circuit on unresolved decisions (the ТЗ asked for an explicit choice): the repository has always reported "denied" ahead of "unresolved decisions" and "not found" after them, and a service-side short-circuit would change what a non-MainGM actor or a missing character gets. This is the block-2/3 pattern, chosen for the same reason. The unresolved check is still made by Rules (in the service) and enforced by the repository before any database access.

## 8. Persistence boundary
`ApplyCharacterRulesetMigration(campaign, characterId, targetRulesetVersion, decidedSourceRulesetVersion, decidedMechanicsRevision, decidedCharacterAbilitiesRevision, decidedCharacterResourcesRevision, hasUnresolvedDecisions, definitionMappingCount, actor, isMainGm, commandId, correlationId)`. No Rules type, no plan, no catalog, no hash. Order of failures is unchanged: MainGM-only, unresolved decisions (both before the database is touched), not found, stale plan. Stale plan = locked source `RulesetVersion` or any of the three section revisions differs from the decided value -> `CharacterRulesetMigrationStalePlan` (existing code). The write is unchanged (`RulesetVersion`, `CharacterRevision`, one forward `CharacterRulesetMigrated` event; `definitionMappingCount` in its payload).

**Why this is equivalent to the hash it replaces.** `PreviewHash` covered the character id, source and target ids and versions, the three section revisions, the mappings and the unresolved decisions. The ids and target are the same values on both sides by construction; the mappings and unresolved decisions are a pure function of the attributes/skills/abilities/resources, which change only through mutations that bump the corresponding section revision (`MechanicsRevision` for attributes and skills, `CharacterAbilitiesRevision`, `CharacterResourcesRevision`). What remains variable is the source `RulesetVersion` and the three revisions -- which the repository now compares directly. `CharacterRevision` and the identity were not in the hash and are not compared (test `TC-CHAR-215`). The one thing that changes is where the plan can go stale: a plan handed in by a caller could be minutes old; now it is always the service's own fresh read, so the window is only read -> commit.

## 9. Tests and validation
The existing migration tests (plan, apply, unresolved, non-MainGM, duplicate command id, revert, revert twice, revert with another character's command id, no schema-migration involvement) pass through the service with unchanged assertions; the whole Persistence suite is green. New: exact plan and pure read (`210`); resolved apply (`211`); rejected cases and their order (`212`); real race with a competing purchase of an unrecognized skill between read and commit -- the stale plan would be fully resolved, the real state is not (`213`); real race with a competing migration, detectable only via the source version (`214`); no hash needed and a rename still does not stale a plan (`215`); port-only use with an interception double (`216`). Mutation checks: with the revision comparison disabled `213` and the existing stale-plan test fail; with the source-version comparison disabled `214` fails (both verified).

## 10. Compatibility and rollback
For callers going through the service, plans, mappings, unresolved decisions and the applied state are the same. Behaviour change, inherent to "the service accepts no plan": a caller can no longer hold a plan across a state change and have it rejected later, because there is no plan to hold; the operation simply recomputes on the current state (and reports unresolved decisions if there are new ones). Port: `PreviewCharacterRulesetMigration` removed, `ApplyCharacterRulesetMigration` re-signed; no production caller exists. Rollback: revert the PR.

## 11. Security and privacy
Unchanged: MainGM-only, checked before the database is touched.

## 12. Observability
Unchanged event and payload.

## 13. Performance
Apply performs one extra read before the transaction; Preview costs the same read as before.

## 14. Dependencies
`ODY-S09-103`.

## 15. Dependencies (packages)
None.

## 16. Implementation plan
`docs/plans/active/ODY-S09-104_Ruleset_Migration_Decoupling.md`.

## 17. Completion evidence
`dotnet test` green; `verify-format`, `verify-repository`, `verify-test-structure` PASS; grep over `Packages/com.odyssey.persistence` finds no `Odyssey.Rules`, `RulesetMigrationRules` or `Rules*` alias; `git diff --name-status` limited to the paths in §4.

## 18. Change control

### Decisions made during execution
- `PreviewCharacterRulesetMigration` removed from `ICharacterRepository` (no production caller; the only callers are tests). Same reasoning as block 3.
- The staleness guard is the source version plus the three section revisions, compared directly; `PreviewHash` is no longer read or written on the Persistence path. It remains a field of the Rules-layer plan (out of scope), still produced by `BuildPlan` and shown by Preview.
- `definitionMappingCount` (an `int`) is the only mapping information that crosses the port: it is all the write uses (the event payload). Passing the mapping list would add a parameter Persistence never reads.
- The former in-transaction "fresh plan has unresolved decisions" check was dropped: the flag is decided by the service from the same read the revisions come from, and the revision check guarantees that read is still current.
- The existing test `Apply_WithStalePlan_IsRejected_NoStateChange` could no longer be driven through the service (there is no plan to make stale), so it now calls the repository with the values of an old read, which is the situation the service would produce in a real race; `TC-CHAR-213`/`214` drive the real race through the service.

### Findings (reported, not fixed -- out of scope)
- **`RevertCharacterRulesetMigration` (ТЗ §1.4):** checked; it contains no reference to `Odyssey.Rules` and uses none of its types; it was not touched. No expansion of the perimeter was needed.
- `SqliteCharacterRepository.cs` and the whole `com.odyssey.persistence` package now contain no `Odyssey.Rules` reference (grep). Persistence's `.asmdef` and `.csproj` were not touched (forbidden by §5); whether they are clean of the reference chain is block 5's check.
- New test file `CharacterAdvancementRulesetMigrationServiceTests.cs`; the hand-written decorators in `CharacterResourceAnatomyTests.cs` and `CheckIntegrationTests.cs` followed the signatures; `TC-CHAR-188`'s expected method list was extended to the ten public service methods.
- The event payload `definitionMappingCount` is now the count the service decided; it equals the former in-transaction count whenever the plan is current (which the stale check guarantees).

### Blockers
None.
