# ODY-S04-113a — Ruleset Migration Revert Character-Scope Gap Fix

**Status:** Done  
**Roadmap stage / slice:** SLICE-04  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s04-113-character-ruleset-migration` (amended onto PR #97's own still-open branch — see section 18 Decisions)  
**Pull request:** [#97](https://github.com/odyssey-services/Odyssey_VTT/pull/97)  
**ExecPlan:** Not required (Brief plan)  
**Created:** 2026-09-03  
**Last updated:** 2026-09-03 (merged) UTC

## 1. Goal

Make `RevertCharacterRulesetMigration` reject a `migrationCommandId` whose `character_ruleset_migrated` event belongs to a different `CharacterId` than the one passed to the call, instead of silently reverting the wrong Character's `RulesetVersion` and writing an orphaned compensating event.

## 2. Why this task exists

- Problem or dependency being addressed: PR [#97](https://github.com/odyssey-services/Odyssey_VTT/pull/97) (`ODY-S04-113`) review found that `SqliteCharacterRepository.FindRulesetMigrationByCommandId` looks up the migration event by `CommandId` + `EventType` only, with no check that the event's own `characterId` payload field matches the `characterId` argument of `RevertCharacterRulesetMigration`. `DomainEvents` has no dedicated `AggregateId` column (confirmed by `GetCharacterHistory`'s own existing payload-`characterId` filter) — payload-field comparison is the only correct scoping mechanism in this schema, and it is missing here.
- Value or risk reduction: without this check, a caller that supplies a `migrationCommandId` belonging to a different Character in the same campaign (wrong value from a stale UI list, copy-paste error, or malicious input) causes `RevertCharacterRulesetMigration` to overwrite the target Character's `RulesetVersion` with the *other* Character's prior version, and to write a compensating event whose `OriginalEventId` points at an event with a different `characterId` in its own payload. `GetCharacterHistory` filters strictly by payload `characterId`, so that compensating event becomes a orphan in the target Character's history — a forward event that its own compensating event claims to revert never appears there. This is silent data corruption, not a rejected/logged error.
- Blocking or enabling relationship: blocks merging PR #97 as reviewed; does not block anything else in the `SLICE-04` backlog (`ODY-S04-114`/`115` do not depend on this specific defect).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §7 (migration), §9 (module ownership)
- `docs/adr/ADR-012_...md` §6 (compensating events reference the original event of the same aggregate — the invariant this gap violates)
- `docs/tasks/active/ODY-S04-113_Character_Ruleset_Migration.md` (the task this fixes a gap in)

### Requirement and test IDs

- Requirement IDs: `ODY-S04-113a`
- Existing test IDs: `TC-CHAR-153`–`164` (from `ODY-S04-113`, PR #97) — must continue to pass unmodified.
- New test IDs to introduce: `TC-CHAR-165`, `TC-CHAR-166` (reserve two; use only what is needed)

### Task-safe private context

- Approved summary / references: gap found during owner-directed review of PR #97; owner decision was to fix it as its own task contract rather than folding it silently back into `ODY-S04-113`'s branch. Fix ships either as an amendment to the still-open, still-Draft PR #97's own branch, or as a small follow-up PR stacked on it — either is acceptable; see section 14.

## 4. Verified current state

### Verified facts

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, method `FindRulesetMigrationByCommandId` (added by PR #97): query is `SELECT EventSequence, PayloadJson FROM DomainEvents WHERE CommandId = $commandId AND EventType = 'odyssey.persistence.character_ruleset_migrated' LIMIT 1;` — no `characterId` filter of any kind.
- The same file's `GetCharacterHistory` method already establishes the correct pattern for this exact schema constraint: it reads `PayloadJson`, parses `payload["characterId"]`, and explicitly skips any row whose payload `characterId` does not match the target Character, with an inline comment stating `DomainEvents` carries no dedicated `AggregateId` column.
- `RevertCharacterRulesetMigration`'s caller passes both `characterId` (the Character being reverted) and `migrationCommandId` (the original migration's `CommandId`) as two independent parameters — nothing upstream of `FindRulesetMigrationByCommandId` ties them together.
- Existing test coverage (`CharacterRulesetMigrationTests.cs`, `TC-CHAR-163`, `Revert_OnUnknownMigrationCommandId_IsRejected`) covers only a `CommandId` with no matching event at all — it does not cover a `CommandId` that matches a real `character_ruleset_migrated` event belonging to a *different* Character.
- `character_ruleset_migrated`'s event payload already includes a `characterId` field (added by PR #97, `ApplyCharacterRulesetMigration`'s `eventPayload` construction) — the fix needs no new payload field, only a new comparison.

### Assumptions

None.

## 5. Scope

### In scope

- Add a check inside `FindRulesetMigrationByCommandId` (or immediately at its call site in `RevertCharacterRulesetMigration`) that rejects when the found event's payload `characterId` does not equal the `characterId` argument, using the same `PersistenceFailures.CharacterRulesetMigrationNotFound` error the "no such CommandId at all" case already returns (a cross-Character `CommandId` is observably identical to "no such migration for this Character" from the caller's point of view — do not invent a second, more specific error code that would leak the existence of another Character's command history).
- Add at least one regression test proving: given two Characters A and B in the same campaign, each with their own successful `character_ruleset_migrated` event, calling `RevertCharacterRulesetMigration` for Character A with Character B's `migrationCommandId` is rejected with `PersistenceCharacterRulesetMigrationNotFound`, and neither Character's `RulesetVersion`/`CharacterRevision` changes.
- Reserve and use `TC-CHAR-165` for that test; use `TC-CHAR-166` only if a second scenario (for example, confirming Character B's own valid revert still succeeds unaffected, if not already covered) is genuinely needed.

### Out of scope

- Any other change to `ODY-S04-113`'s scope, behavior, or test coverage — this task fixes exactly the one gap in section 2, nothing else.
- Adding an `AggregateId` column to `DomainEvents` or any other schema change — out of scope for this repository-wide (`ADR-012` §5 already fixed the table shape); the payload-field comparison is the established, correct workaround, not a defect in the schema itself.
- Any change to `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration` — the gap is specific to `RevertCharacterRulesetMigration`'s own lookup.
- Any change to `ODY-S04-107`'s `RevertAdvancementPurchase`/`ApplyCharacterRespec` even though they may share a similar lookup shape — verify only as a read-only check (see Definition of Done); do not touch that code unless verification finds the identical defect there, in which case stop and report back rather than silently expanding scope.

### Allowed paths

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterRulesetMigrationTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/active/ODY-S04-113a_Ruleset_Migration_Revert_Character_Scope_Gap_Fix.md
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs — RevertAdvancementPurchase/ApplyCharacterRespec sections, if the verification step in Definition of Done finds the same defect there
```

## 6. Technical constraints

- Module ownership and dependency direction: fix stays entirely inside `Odyssey.Persistence` (the defect is in a private helper method); no change to `Odyssey.Domain`/`Odyssey.Rules`/`Odyssey.Application` contracts.
- Authoritative-state and transaction boundary: no change — the fix only adds a read-time comparison before the existing transaction proceeds; it does not alter `ADR-012`'s one-transaction-per-command shape.
- Serialization / compatibility boundary: no change to `character_ruleset_migrated`'s payload shape — the `characterId` field already exists; this task only reads it earlier.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable — pure .NET persistence code.
- Dependency / licensing rule: Not applicable — no new dependency.
- Security / privacy / redaction rule: the fix must not leak whether a given `CommandId` belongs to another Character — reuse `CharacterRulesetMigrationNotFound` for both "no such command" and "command belongs to a different Character" (section 5).
- Performance or platform constraint: Not applicable — this lookup is already a single indexed-or-scanned row read inside a transaction; comparing one more string field is negligible.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — Cross-Character migrationCommandId is rejected

**Given** Character A and Character B both exist in the same campaign, and Character B has a successful, unreverted `character_ruleset_migrated` event with `CommandId = X`  
**When** `RevertCharacterRulesetMigration` is called for Character A with `migrationCommandId = X`  
**Then** the call fails with `PersistenceCharacterRulesetMigrationNotFound`, and neither Character A's nor Character B's `RulesetVersion`/`CharacterRevision`/history changes.

### Scenario 2 — Same-Character revert still works (no regression)

**Given** Character A has its own successful, unreverted `character_ruleset_migrated` event with `CommandId = Y`  
**When** `RevertCharacterRulesetMigration` is called for Character A with `migrationCommandId = Y`  
**Then** the call succeeds exactly as `TC-CHAR-160` already proves — this scenario is regression coverage, not new behavior; do not weaken or duplicate `TC-CHAR-160` itself.

### Required invariants

- A compensating `odyssey.persistence.character_ruleset_migration_reverted` event's `OriginalEventId` always points to a `character_ruleset_migrated` event whose own payload `characterId` equals the compensating event's own payload `characterId` (`ADR-012` §6, restated here as the invariant this task restores).

## 8. Deliverables

- Production code: the character-scope check in `FindRulesetMigrationByCommandId` (or its call site), `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`.
- Tests: `TC-CHAR-165` (required), `TC-CHAR-166` (only if needed) in `CharacterRulesetMigrationTests.cs`; `Tests/Metadata/test-catalog.json` updated to match.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract's own Completion evidence (section 17); `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 13 note updated to record the gap-fix, mirroring how `ODY-UI-01-007a` was recorded against `ODY-UI-01-007`.
- Generated evidence or build artifacts: `dotnet build`/`dotnet test` output as required by section 10.
- Migration / recovery material: None — no persisted data shape changes.

## 9. Acceptance criteria

1. `RevertCharacterRulesetMigration` rejects a `migrationCommandId` that resolves to a `character_ruleset_migrated` event whose payload `characterId` differs from the call's own `characterId` argument, with error code `PersistenceCharacterRulesetMigrationNotFound`.
2. Rejection under criterion 1 leaves both the target Character's and the other Character's `RulesetVersion`, `CharacterRevision`, and event history completely unchanged.
3. All previously passing `ODY-S04-113` tests (`TC-CHAR-153`–`164`) still pass unmodified — this is a targeted fix, not a rewrite.
4. `TC-CHAR-165` (cross-Character rejection) passes; `TC-CHAR-166` is added only if genuinely needed and, if added, passes.
5. No new error code was introduced — `PersistenceCharacterRulesetMigrationNotFound` (already defined by PR #97) is reused, per the security/privacy rule in section 6.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-165` | .NET / NUnit | Cross-Character `migrationCommandId` is rejected with `PersistenceCharacterRulesetMigrationNotFound`; neither Character's state changes | Pass |
| `TC-CHAR-166` | .NET / NUnit | (Only if needed) same-Character revert continues to succeed unaffected by the new check | Pass |

### Required commands

```powershell
dotnet build DotNet/Odyssey.sln
dotnet test DotNet/Odyssey.sln
pwsh scripts/verify-format.ps1
pwsh scripts/check-repository-policy.ps1
```

### Manual validation

None — this is a pure persistence-layer logic fix with full automated coverage.

### Required environments / profiles

- OS / architecture: same CI environment already used by `ODY-S04-113`/PR #97.
- Unity editor or Player profile: Not applicable — no Unity-side change.
- Scripting backend: Not applicable.
- Network topology or database fixture: same in-memory/temp-file SQLite fixture pattern `CharacterRulesetMigrationTests.cs` already uses.
- Other: None.

### Validation not required by this task

- PlayMode/Unity validation — reason: no Unity-side code is touched.
- Performance profiling — reason: fix adds one string comparison to an already-single-row read; no measurable impact.

## 11. Compatibility, migration, and rollback

Not applicable. No persisted schema, event payload shape, public contract, or protocol changes — the `characterId` field already exists in the event payload; this task only adds a read-time comparison against it. No `FormatVersion`/`RulesetVersion`/application version change.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: `CommandId` values and `characterId` payload fields already present in `DomainEvents` — no new data class introduced.
- Trust boundaries: the caller (application layer) is trusted to pass a `characterId` matching the actor's own request context; this fix defends against a mismatched `migrationCommandId` regardless of whether the mismatch is accidental or adversarial.
- Authorization / audience checks: no change — `actorIsMainGm` gating is unaffected by this fix.
- Redaction requirements: Not applicable.
- Log-safe fields: no new log statement introduced.
- Abuse / malformed input limits: the fix specifically closes an abuse path (a caller supplying another Character's `migrationCommandId`) — see section 5's explicit instruction to reuse the existing `NotFound` error rather than a distinguishing one, so the response does not confirm or deny another Character's command history.
- Security tests: `TC-CHAR-165` is the security-relevant regression test for this task.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: single-file production fix plus one to two test methods in an already-existing test file; no new module, no new public contract, no architectural decision — does not meet `PLANS.md`'s ExecPlan trigger bar.
- ExecPlan path: Not required
- Expected pull request count: 1 (either as a further commit on PR #97's own branch if it is still open and unmerged at the time this task is picked up, or as a new small branch/PR stacked on it — the owner has not mandated one specific mechanism; pick whichever the repository's branch state at pickup time makes cleaner, and record the choice in section 18)
- Milestone or sequencing constraints: should land before PR #97/`ODY-S04-113` is merged into `main`, so `main` never contains the unfixed defect.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/active/ODY-S04-113a_Ruleset_Migration_Revert_Character_Scope_Gap_Fix.md` (this file, completion evidence); `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 13 note, mirroring the `ODY-UI-01-007a` precedent of recording a gap-fix against its parent task's row).
- Documents that must not change: `docs/adr/ADR-025_...md`, `docs/adr/ADR-012_...md` — this task fixes an implementation gap against their already-decided invariants; it does not amend either ADR.
- Application version change: No — internal correctness fix, no observable contract change.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None beyond the two files above.
- Changelog or release-note requirement: None beyond this task's own completion evidence.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (None applicable here).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable (Not applicable, confirmed).
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] A read-only check was performed on `ODY-S04-107`'s `RevertAdvancementPurchase`/`ApplyCharacterRespec` lookup logic for the identical missing-character-scope defect; result (found or not found) is recorded in section 18 regardless of outcome, and if found, execution stops there and reports back rather than silently expanding this task's own scope.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` — `FindRulesetMigrationByCommandId` now takes `characterId` and rejects (returns `null`) when the found event's own payload `characterId` does not match; its one call site in `RevertCharacterRulesetMigration` passes `characterId` through.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterRulesetMigrationTests.cs` — one new regression test (`Revert_WithAnotherCharactersMigrationCommandId_IsRejected_NoStateChangeToEither`) covering both Scenario 1 (cross-Character rejection, no state change to either Character) and Scenario 2 (the targeted Character's own legitimate revert still succeeds) in a single test, since both assertions share the same two-Character fixture.
- `Tests/Metadata/test-catalog.json` — `TC-CHAR-165` added; `TC-CHAR-166` was not needed (Scenario 2's regression coverage is already inside `TC-CHAR-165`'s own test).
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — row 13 note updated recording this gap-fix against `ODY-S04-113`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 242 (241 pre-existing including all of `TC-CHAR-153`–`164` unmodified + 1 new) — 473 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |

(Note: the task's own §10 named `dotnet build DotNet/Odyssey.sln`/`pwsh scripts/...`; this repository's actual solution file and validation convention — confirmed against every prior `SLICE-04` task, including `ODY-S04-113` itself — is `DotNet/Odyssey.Core.sln` run from the repository root with `.\scripts\...ps1`, which is what was actually run and is recorded above.)

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-165`: cross-Character `migrationCommandId` rejected with `PersistenceCharacterRulesetMigrationNotFound`. |
| AC-2 | Passed | `TC-CHAR-165`: both Character A's and Character B's `RulesetVersion`/`CharacterRevision` confirmed unchanged after the rejected cross-Character attempt. |
| AC-3 | Passed | All pre-existing `TC-CHAR-153`–`164` pass unmodified (full suite run above). |
| AC-4 | Passed | `TC-CHAR-165` added and passing; `TC-CHAR-166` not added (not needed — see Changed files above). |
| AC-5 | Passed | No new error code introduced — `PersistenceCharacterRulesetMigrationNotFound` (already defined by PR #97) is reused for both "no such command" and "command belongs to a different Character." |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: Not applicable.
- Checksums: Not applicable.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- None found during implementation.

### Follow-up tasks

- None. The Definition-of-Done check on `ODY-S04-107`'s `RevertAdvancementPurchase`/`ApplyCharacterRespec` lookup logic (`SelectAdvancementPurchaseForUpdate`) found no identical defect — see section 18 for the verified reason.

### Self-review summary

- Scope review: limited to exactly the one gap in section 2 — `FindRulesetMigrationByCommandId`'s own missing character-scope check and its one call site; no other `ODY-S04-113` behavior touched; no schema change; no new error code.
- Architecture review: the fix mirrors `GetCharacterHistory`'s own already-established pattern (payload `characterId` comparison, since `DomainEvents` carries no dedicated `AggregateId` column) rather than inventing a new mechanism; stays entirely inside `Odyssey.Persistence`.
- Test review: `TC-CHAR-165` is a real, non-stubbed test against a genuine temp-directory SQLite campaign with two real Characters, each with their own real migration — proves both the rejection and the no-state-change invariant for both Characters, plus regression coverage that the legitimate same-Character revert still works.
- Security/privacy review: the fix specifically closes an information-leak-shaped abuse path — reusing the existing generic `NotFound` error (not a new, more specific one) so the response never confirms or denies that a given `CommandId` belongs to another Character's command history, per section 6's explicit instruction.
- Documentation/version review: this task contract's own completion evidence and the backlog row 13 note are updated; no ADR touched; no schema/protocol/application version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None at task creation, and none arose during execution.

### Decisions made during execution

- 2026-09-03 — Gap found during owner-directed review of PR #97 is fixed as its own task contract (`ODY-S04-113a`), following the `ODY-UI-01-002a`/`ODY-UI-01-007a` gap-fix precedent, rather than silently amended back into `ODY-S04-113`'s own already-reported completion evidence — Authority / approval: Product owner ("Давай поправим эту проблему в следующем тз").
- 2026-09-03 — Decision: the fix ships as a further commit on PR #97's own still-open, still-Draft branch (`feat/ody-s04-113-character-ruleset-migration`), not a separate stacked branch/PR — Authority/approval: section 14 explicitly left this choice to whichever the repository's branch state at pickup time makes cleaner; the branch was still open and unmerged when this task was picked up, so amending it directly keeps `main` from ever containing the unfixed defect (section 14's own stated goal) with the least ceremony.
- 2026-09-03 — Decision: the Definition-of-Done read-only check on `ODY-S04-107`'s `RevertAdvancementPurchase`/`ApplyCharacterRespec` lookup logic found **no identical defect**. `SelectAdvancementPurchaseForUpdate` (used by `RevertAdvancementPurchase`) queries its own dedicated `AdvancementPurchase` table with a real `CharacterId` column (`WHERE CharacterId = $characterId AND PurchaseId = $purchaseId`), unlike `RevertCharacterRulesetMigration`'s own `DomainEvents`-payload-based lookup (a deliberate `ODY-S04-113` design choice — "no new ledger table," ExecPlan section 5 — that introduced this specific new class of bug an existing dedicated table naturally avoids). No follow-up task is needed for `ODY-S04-107`'s own code.
- 2026-09-03 — Decision: only `TC-CHAR-165` was added, covering both Scenario 1 (cross-Character rejection) and Scenario 2 (same-Character regression) in one test, since both assertions share the same two-Character fixture and splitting them would only duplicate setup — `TC-CHAR-166` was reserved but not used, per section 3's own "reserve two; use only what is needed" instruction.
- 2026-09-03 — Owner reviewed the two landed commits (`a5c3219`, `31be6e4`) directly against the diff, confirmed the fix, test, and CI evidence in Codex's Final Report, and merged PR #97 into `main` (merge commit `e48a541`) — Authority/approval: Product owner ("Мердж провел"). Task moved to Done.

### Approved task changes

- None.

---

## Template completion rules

1. Remove instructional examples that do not apply, but keep all numbered section headings.
2. Write `None` or `Not applicable` instead of leaving an ambiguous blank.
3. A task may be marked `Ready` only when goal, scope, authorities, acceptance criteria, validation, and required decisions are complete.
4. A task may be marked `In Progress` only after the working branch exists and the required ExecPlan is created when applicable.
5. A task may be marked `In Review` only after completion evidence is filled honestly.
6. A task may be marked `Done` only after required review and all non-deferred acceptance criteria pass.
7. Deferred work requires an explicit follow-up Task ID; it cannot disappear into prose.
8. Never mark an unrun validation command as passed.
9. Never update golden files, snapshots, manifests, or expected outputs only to make a failing test green without verifying the intended behavior.
10. Never broaden MVP scope or create a new architectural rule inside a task; request an owner decision or ADR instead.
