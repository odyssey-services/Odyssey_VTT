# ODY-S11-206 — RemoveBodyPart spurious "has dependency" denial on a cold inventory connection

**Status:** Draft
**Roadmap stage / slice:** SLICE-11 (found during phase 4/UI verification; the defect itself is backend, `Packages/com.odyssey.persistence` and `Packages/com.odyssey.application`)
**Owner:** Unassigned
**Requested by:** Product owner (found and reproduced during SLICE-11 all-phases verification, not fixed there per explicit instruction)
**Branch:** Not created
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-09-30
**Last updated:** 2026-09-30

## 1. Goal

Fix `SqliteCharacterRepository.RemoveBodyPart` so that, on a campaign whose inventory tables have never
been touched, the call completes in normal time and does not spuriously fail with
`persistence.character.body_part_has_dependent` when no inventory entry actually references the body part.

## 2. Why this task exists

- Problem: on a fresh campaign where no inventory action has ever run, `RemoveBodyPart` takes roughly
  30–33 seconds and then wrongly refuses with a "has dependency" error, even though no inventory entry
  exists at all.
- Value / risk reduction: without a workaround, this makes body-part removal unusable (looks hung, then
  fails) for any new campaign until an inventory has been touched once. The SLICE-11 UI client currently
  works around this by performing one inventory read at startup, purely client-side, so the UI-facing
  symptom is currently masked but the backend defect remains.
- Discovered during: `ODY-S11-203` (Character Sheet Creation & Lifecycle) implementation and independently
  reproduced during `ODY-S11-200..205` (SLICE-11 all-phases) verification. Not fixed in that branch per
  explicit instruction to leave the backend untouched.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- ADR-004 (Result/error model), ADR-005 (composition/lifetimes), ADR-022 (character model)

### Requirement and test IDs

- Requirement IDs: None
- Existing test IDs: `DotNet/Tests/Odyssey.Tests.Persistence/BodyPartRemovalDependencyCheckerTests.cs` (existing
  suite; every existing test case pre-touches `IInventoryRepository` during setup via `CreateInventory`,
  which is why none of them ever exercised the cold-connection path)
- New test IDs to introduce: one new persistence test covering `RemoveBodyPart` on a character whose
  campaign has never called any `IInventoryRepository` member before (suggested ID:
  `TC-PERSISTENCE-RemoveBodyPart-ColdInventoryConnection`)

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` around line 4229,
  `RemoveBodyPart`, runs its dependency-checker loop **inside** the open write transaction created by
  `MutateAnatomy`.
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs` lines 95–124,
  `InventoryBodyPartRemovalDependencyChecker.CheckBlockingDependency`, calls
  `_inventoryRepository.HasAnyEquippedEntryReferencingBodyPart(...)` synchronously from inside that same
  callback.
- `SqliteInventoryRepository.HasAnyEquippedEntryReferencingBodyPart` opens its **own** SQLite connection and,
  on a campaign whose inventory tables were never created, lazily runs `EnsureInventoryTables` — a schema
  migration/DDL step — while `SqliteCharacterRepository.RemoveBodyPart`'s write transaction is already open
  on the same database file. This produces SQLite busy-timeout contention (observed: ~30–33 seconds) and
  then the checker returns a value that results in a fail-closed `persistence.character.body_part_has_dependent`
  denial, even with zero actual inventory entries.
- Reproduced independently during SLICE-11 verification via a temporary, non-committed test
  (`DotNet/Tests/Odyssey.Tests.Persistence/ZZZ_TEMP_RemoveBodyPart_Repro.cs`, deleted before commit; no trace
  left in `DotNet`/`Packages`): `RemoveBodyPart` on a character with zero prior inventory interaction took
  `REPRO_ELAPSED_MS=33106` and returned `REPRO_SUCCESS=False`,
  `REPRO_ERROR_CODE=persistence.character.body_part_has_dependent`.
- `docs/tasks/active/ODY-S11-203_Character_Sheet_Creation_Lifecycle.md` §18 already documents this exact
  root cause and states the client works around it with one inventory read at startup, without touching
  the backend.
- Every existing case in `BodyPartRemovalDependencyCheckerTests.cs` calls `CreateInventory` (or another
  `IInventoryRepository` member) during setup before exercising `RemoveBodyPart`, which opens/creates the
  inventory tables ahead of time and hides the cold-connection race — explaining why no existing test has
  ever caught this.

### Assumptions

- None. All facts above were confirmed by direct code reading and a live, deleted reproduction test.

## 5. Scope

### In scope

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` (ensure inventory tables
  exist without racing/contending with a caller's already-open write transaction on the same file — e.g. by
  ensuring schema at repository/connection-factory initialization instead of lazily inside a hot read path,
  or by using a non-blocking/short-timeout check that cannot stall a foreign transaction).
- `Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs` if the fix
  requires changing how/when the dependency check opens its connection.
- New regression test in `DotNet/Tests/Odyssey.Tests.Persistence/BodyPartRemovalDependencyCheckerTests.cs`
  (or a new file) covering the cold-connection path.

### Out of scope

- The SLICE-11 UI client-side workaround (one inventory read at startup) — leave as-is or remove only if this
  fix makes it provably unnecessary; removing it is not required for this task's Definition of Done.
- Any other inventory/character persistence behavior not related to this contention path.

### Allowed paths

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs
DotNet/Tests/Odyssey.Tests.Persistence/BodyPartRemovalDependencyCheckerTests.cs
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
```

## 6. Technical constraints

- Module ownership and dependency direction: fix stays inside `com.odyssey.persistence` /
  `com.odyssey.application`; must not introduce a new dependency from `Application` back onto
  `Persistence` internals beyond the existing repository interfaces.
- Authoritative-state and transaction boundary: `RemoveBodyPart`'s write transaction and the dependency
  check must not contend for the same SQLite file lock; prefer resolving this by not opening a second
  writer-capable connection mid-transaction rather than by lengthening timeouts.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable (pure .NET persistence layer).
- Security / privacy / redaction rule: Not applicable.
- Other: must not change the observable error contract for a *genuine* dependency (an actually-equipped
  item referencing the removed body part must still be rejected with the same error code).

## 7. Expected behavior

### Scenario 1 — cold inventory, no dependency

**Given** a campaign whose inventory tables have never been created and a character with a body part that
no inventory entry references
**When** `RemoveBodyPart` is called for that body part
**Then** it completes in normal time (no ~30s stall) and succeeds.

### Scenario 2 — genuine dependency, warm or cold inventory

**Given** a character with a body part that an equipped inventory entry genuinely references
**When** `RemoveBodyPart` is called for that body part
**Then** it is rejected with `persistence.character.body_part_has_dependent`, regardless of whether the
inventory tables pre-existed.

### Required invariants

- `RemoveBodyPart` must never block for the duration of a busy-timeout when the campaign's inventory has
  never been touched.

## 8. Deliverables

- Production code: fix in `SqliteInventoryRepository` (and/or the dependency checker) as scoped above.
- Tests: new regression test for the cold-connection path; existing `BodyPartRemovalDependencyCheckerTests.cs`
  cases must remain green.
- Scripts / CI: None.
- Configuration: None.
- Documentation: update `docs/tasks/active/ODY-S11-203_Character_Sheet_Creation_Lifecycle.md` §18 to record
  the fix once merged, and note whether the client-side startup-read workaround is still needed.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None expected (no schema/contract change anticipated).

## 9. Acceptance criteria

1. A new automated test reproduces the cold-connection scenario and fails against the current code before
   the fix (documented in the PR), then passes after the fix.
2. `RemoveBodyPart` on a campaign with zero prior inventory interaction completes without a multi-second
   stall (assert an explicit, generous upper bound, e.g. under 2 seconds).
3. A genuine dependency (equipped item referencing the body part) is still rejected with
   `persistence.character.body_part_has_dependent`, both when the inventory tables pre-exist and when they
   do not.
4. All existing `BodyPartRemovalDependencyCheckerTests.cs` cases remain green, unmodified in their assertions.
5. `dotnet test` for `Odyssey.Tests.Persistence` passes in full; `scripts/test-fast.ps1` remains green.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-PERSISTENCE-RemoveBodyPart-ColdInventoryConnection` | .NET / Odyssey.Tests.Persistence | RemoveBodyPart on a campaign with no prior inventory interaction succeeds quickly with no false dependency | Pass |

### Required commands

```powershell
dotnet test DotNet/Tests/Odyssey.Tests.Persistence/Odyssey.Tests.Persistence.csproj
scripts/test-fast.ps1
```

### Manual validation

- None required; this is a backend/persistence-only fix with full automated coverage.

### Required environments / profiles

- OS / architecture: Windows, .NET 10 (matches repository baseline).
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: local SQLite file-backed repository, matching existing test fixtures.

### Validation not required by this task

- Unity EditMode/PlayMode tests are not expected to change and are not required beyond the existing
  regression baseline.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None expected; no schema or contract shape change, only internal connection/
  transaction sequencing.
- Version fields affected: None expected.
- Migration or upcaster: Not applicable.
- Forward / backward behavior: Unaffected.
- Rollback method: Revert the commit; no persisted-data migration is introduced.
- Data-loss risk and protection: None; read-path/sequencing fix only.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Reason for selected mode: Localized, well-understood root cause; single-file fix with a bounded blast
  radius.
- ExecPlan path: Not required
- Expected pull request count: 1
- Milestone or sequencing constraints: None; independent of SLICE-11 UI work and can land before or after it.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/active/ODY-S11-203_Character_Sheet_Creation_Lifecycle.md` §18
  (mark the backend bug as fixed once this task lands).
- Documents that must not change: None.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: Optional, at owner's discretion.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed. (None required.)
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable. (Not applicable.)
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable. (Not applicable.)
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

Not started.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-30 — Bug written up as a separate task rather than fixed inline in `claude/pensive-gates-n18srp`,
  per explicit instruction to leave the backend untouched during SLICE-11 UI verification. — Authority /
  approval: product owner instruction (SLICE-11 all-phases verification mini-ТЗ, §3).

### Approved task changes

- None.
