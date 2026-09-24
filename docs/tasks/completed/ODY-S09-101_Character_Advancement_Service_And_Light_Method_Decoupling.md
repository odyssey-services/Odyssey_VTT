# ODY-S09-101 — SLICE-09 Backlog + Character Advancement Service + Decoupling of the Two Light Methods (block 1)

## 1. Task identity
`ODY-S09-101`; status: Done (PR #174, merged into main). First task of `SLICE-09`, which it creates; decomposes and implements block 1.

## 2. Goal
Start removing the `Odyssey.Persistence -> Odyssey.Rules` violation (`ADR-001` §5) found by the PR #173 investigation. Block 1 introduces the Application-layer service that will own character-advancement decisions and uses it to decouple the two methods that only read default constants: `InitializeCharacterResource` and `InitializeCharacterAnatomy`. Also creates `docs/tasks/SLICE-09_IMPLEMENTATION_BACKLOG.md` with all five blocks.

## 3. Authority
This task's governing ТЗ §1-§7; the PR #173 report `docs/research/Unity_Rules_Asmdef_Break_Investigation.md` (Candidate B); `ADR-001` §5, `ADR-024` 192-194, `ADR-025` 185-187 (read, not revised). Structural templates: `SLICE-07`/`SLICE-08` backlogs; precedent for the service form: `Odyssey.Application.Checks.CheckService`.

## 4. In scope
- `SLICE-09_IMPLEMENTATION_BACKLOG.md` (five blocks; block 1 detailed, 2-5 named and reserved; ordering rationale in its §6).
- `Odyssey.Application.CharacterAdvancement.CharacterAdvancementService`: static class, `CheckService` form, `ICharacterRepository` passed by the caller; `InitializeResourceWithDefaults` / `InitializeAnatomyWithDefaults` read `ResourceInitializationRules` / `AnatomyInitializationRules` and call the repository with plain values.
- `ICharacterRepository.InitializeCharacterResource` gains `baseMaximum`, `minimumValue`, `recoveryRule`; `InitializeCharacterAnatomy` gains `anatomyProfileVersion`, `bodyParts`. The SQLite implementations no longer reference Rules.
- 46 existing test call sites in 15 files moved to the service; assertions unchanged.
- Tests `TC-CHAR-181`-`188`; `test-catalog.json`; this contract and plan.

## 5. Out of scope
The other public methods of `SqliteCharacterRepository` that use `Odyssey.Rules` (cost methods, respec, ruleset migration: blocks 2-4); tightening `verify-test-structure.ps1`/`verify-ci.ps1` (block 5); any `.asmdef`/`.csproj`; any ADR; any other `ICharacterRepository` member; `CheckService`/`CheckContracts`.

**Stated consequence, not hidden:** `SqliteCharacterRepository` still has `using Odyssey.Rules.Character;` and Rules calls in the remaining methods, so the Unity project stays unbuildable until block 5 completes the track.

## 6. Domain contract
Unchanged. No domain type is added or modified.

## 7. Application contract
New `CharacterAdvancementService` (`Odyssey.Application.CharacterAdvancement`). Name rationale: `ADR-024`/`ADR-025` assign character advancement (purchases, respec, migration) to Application; blocks 2-4 will add their methods to the same class. Static class as in `CheckService`: no composition root exists, dependencies are passed explicitly. Wrappers null-guard the repository and pass every other argument through unchanged. Port signature changes as in §4.

## 8. Persistence boundary
The two repository methods hold no default of their own and read nothing from Rules. Guards for caller-supplied values live in the repository and run before any DB access: undefined `RecoveryRule` -> `ArgumentOutOfRangeException`; `baseMaximum < minimumValue` -> `ArgumentException`; blank version -> `ArgumentException`; null `bodyParts` -> `ArgumentNullException`. Stored records, history text, revisions and journal payloads are unchanged.

## 9. Tests and validation
`TC-CHAR-181`/`182`: service output equals the pre-change defaults (literal values and Rules constants). `183`/`184`: repository stores exactly what the caller supplies. `185`/`186`: invalid caller values rejected before DB access. `187`: recording `ICharacterRepository` decorator proves defaults and argument pass-through. `188`: reflection check that the service is in `Odyssey.Application`, exposes only the two wrappers, and takes the abstraction. Existing tests updated and passing. Before/after snapshot of persisted character records and journal payloads for both methods: 0 differences.

## 10. Compatibility and rollback
Behavior of both operations is unchanged for callers going through the service. The port signature change is source-breaking for direct callers (tests only; no production caller exists). Rollback: revert the PR.

## 11. Security and privacy
No change. No new inputs cross a trust boundary; no personal data.

## 12. Observability
Unchanged: same journal events and payloads.

## 13. Performance
Unchanged; one extra static call.

## 14. Dependencies
`ODY-S04-109`; precedent `ODY-S07-102`.

## 15. Dependencies (packages)
None.

## 16. Implementation plan
See `docs/plans/active/ODY-S09-101_Character_Advancement_Service_And_Light_Method_Decoupling.md`.

## 17. Completion evidence
`dotnet test` green; `verify-format`, `verify-repository`, `verify-test-structure` PASS; grep shows no `Odyssey.Rules` inside the two methods while the remaining methods still have it; `git diff --name-status` limited to the paths in this contract.

## 18. Change control

### Decisions made during execution
- Service name `CharacterAdvancementService`, static, namespace `Odyssey.Application.CharacterAdvancement` (see §7).
- Values passed as flat arguments, not via constructor-injected ports: injection would touch 79 `new SqliteCharacterRepository(...)` sites for no benefit.
- Guards for caller-supplied values live in the repository so it stays safe when called without the service.

### Findings (reported, not fixed -- out of scope)
- `DotNet/Tests/Odyssey.Tests.Persistence/CheckIntegrationTests.cs` is outside the ТЗ §4 list, but its hand-written `FailsFirstCriticalSuccessEvidenceCharacterRepository` decorator forwards the two changed members and would not compile; two forwarding lines were updated (forced by the interface change).
- Method inventory: `ComputeRespecPlan` and `AcquireAbilityViaProgressionPurchase` are private helpers behind `PreviewCharacterRespec`/`ApplyCharacterRespec` and `AcquireAbility`; their Rules call sites are untouched and belong to blocks 2-3.
- Unity remains unbuildable on `main` until block 5 (see §5).

### Blockers
None.
