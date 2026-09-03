# ODY-S04-113 — Character Ruleset Migration

**Status:** Ready
**Roadmap stage / slice:** SLICE-04
**Owner:** Unassigned
**Requested by:** Product owner
**Branch:** Not created
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S04-113_Character_Ruleset_Migration.md` (to be created by the implementing agent at task start, per `PLANS.md` §1.2)
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

Implement `PreviewCharacterRulesetMigration` (a read-only `Query` building `CharacterRulesetMigrationPlan`) and `ApplyCharacterRulesetMigration` (one `ADR-012` §5 transaction, re-validating the plan against current state before commit), plus reverting an already-committed migration through `ADR-024`'s existing compensating-batch pattern — proving the mechanism end-to-end for the one case this codebase can currently test honestly: no real cross-Ruleset value-mapping algorithm exists anywhere yet (section 4), so this task proves atomicity/rollback/preview-tamper-detection/revert against a same-family version bump with an explicit, auditable `UnresolvedDecisions` path for anything it cannot map automatically — not a general Rules Engine transformation.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-113` as the thirteenth implementation task, closing the last content-bearing item before integration/closure (`ODY-S04-114`/`115`).
- Value or risk reduction: proves a Character's `RulesetVersion` can change without corrupting state on a mid-application failure (ordinary transaction atomicity), and that a GM can safely undo an already-committed migration through the same compensating-batch mechanism `ODY-S04-107` already established — without conflating this with `ADR-013`'s unrelated database schema migration runner.
- Blocking or enabling relationship: unblocks `ODY-S04-114` (Vertical Slice Integration) in backlog order only; no later task has a real technical dependency on `ODY-S04-113`'s own mapping algorithm being general-purpose.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 13) — the binding scope definition for this task.
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §7 (full read — the governing section for Preview/Apply/revert/backup).
- `docs/adr/ADR-013_Migration_Runner_v1.0.md` §9 (full read — the explicit boundary this task must not cross).
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` (full read — the compensating-batch pattern reused unmodified for reverting an already-committed migration).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` §5/§8.2 (transaction atomicity; optional pre-migration backup via the existing Backup API/`BackupRecord`).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §25 (full read).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`'s own `ApplyCharacterRespec` (`ODY-S04-107`) — the binding structural precedent for both the one-transaction-apply shape and the compensating-batch revert shape.
- `Packages/com.odyssey.rules/Runtime/Character/*.cs` (`AttributeCostRules`/`SkillCostRules`/`AbilityCostRules`/`ResourceInitializationRules`/`AnatomyInitializationRules`) — read in full; confirm directly (do not assume) whether any cross-version value-mapping logic exists there before writing the ExecPlan.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-113`, `ADR-025` §7.1–7.5, `ADR-013` §9, `ADR-024` §7.2/§7.4, product §25.
- Existing test IDs reused: None directly reused. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-152` must continue passing unmodified.
- New test IDs introduced: `TC-CHAR-153` onward (`Tests/Metadata/test-catalog.json`) — exact count confirmed against the scenarios in section 7; do not under- or over-report the real count actually written.

### Task-safe private context

- Approved summary / references: non-tracked product documentation was read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, its plan, or production code.

## 4. Verified current state

### Verified facts

- Direct inspection of `Packages/com.odyssey.rules/Runtime/` (8 files total: `RulesetVersion`/`SemVerValue`, `AssemblyMarker`, and five Character cost/initialization rule files) confirms **no cross-Ruleset-version value-mapping or definition-mapping computation exists anywhere in this codebase** — every existing campaign fixture across `ODY-S04-101`–`112`'s own tests uses exactly one Ruleset (`ruleset.core`/`1.0.0`), never a second version or a second Ruleset family.
- `ADR-025` §7.2/§7.3 define `CharacterRulesetMigrationPlan`'s structural shape (`ValueChanges`/`DefinitionMappings`/`UnresolvedDecisions`/`PreviewHash`) but do not — and per `ADR-025` §8's own exclusion, deliberately do not — define the algorithm that populates `ValueChanges`/`DefinitionMappings` for a real content change between two Ruleset versions. Product §25 gives the same structural shape and process outline, with no algorithm either.
- This is not a blocking authority gap of the kind `ODY-S04-112` found (`ADR-026` was needed because product required specific behavior — export redaction — with zero decided mechanism). Here, `ADR-025` §7.2's own text is explicit that `PreviewCharacterRulesetMigration` builds `UnresolvedDecisions` for whatever it cannot resolve and lets the GM decide — an empty or minimal `ValueChanges`/`DefinitionMappings` result, with everything real routed to `UnresolvedDecisions`, is a **structurally valid, ADR-compliant outcome**, not an invented shortcut. Section 5 "Out of scope" below records this as the deliberate scope boundary for this task rather than leaving it ambiguous.
- `CharacterTemplateCompatibility.IsCompatible` (`ODY-S04-103`, reused by `ODY-S04-112`) already answers "is `RulesetId`+`RulesetVersion` A compatible with B" as a pure function — this task's Preview/Apply reuse it for the same binary compatibility gate `ADR-025` §7.3 requires re-validated at apply time (`CAP-INV-004`), not a second, divergent check.
- `ApplyCharacterRespec` (`ODY-S04-107`) is the direct structural precedent for both halves of this task: its own single-transaction, multi-section apply shape for `ApplyCharacterRulesetMigration`, and its own `RevertAdvancementPurchase`/compensating-batch-with-shared-`CompensationGroupId` shape for reverting an already-committed migration.
- `Tests/Metadata/test-catalog.json`'s last assigned Character test ID is `TC-CHAR-152` (`ODY-S04-112`).

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep` during this task's authoring; the implementing agent must re-verify them against the repository state at the time it starts — in particular, re-confirm no Rules-module mapping logic was added by an intervening task before assuming this task must build it from nothing.

## 5. Scope

### In scope

- `PreviewCharacterRulesetMigration(campaign, characterId, targetRulesetId, targetRulesetVersion, correlationId) -> Result<CharacterRulesetMigrationPlan>` — a read-only `Query` (no `CommandId`, no event, no mutation) in `ICharacterRepository`/`SqliteCharacterRepository`.
- `CharacterRulesetMigrationPlan` DTO (new, `ADR-025` §7.2's four fields plus `CharacterId`/`Status` per product §25's own tree) and any nested `ValueChange`/`DefinitionMapping`/`UnresolvedDecision` DTOs it needs.
- `ApplyCharacterRulesetMigration(campaign, characterId, plan, expectedRevisions..., commandId, correlationId) -> Result<CharacterRecord>` — re-validates `PreviewHash` and current Ruleset-compatibility/section revisions against live state (`CAP-INV-004`) before committing in one transaction; produces one `CharacterRulesetMigrated` forward event.
- Reverting an already-committed migration, reusing `ADR-024`'s exact compensating-batch pattern (`ApplyCharacterRespec`'s own shape: one shared `CompensationGroupId`, each compensating event referencing the original `CharacterRulesetMigrated` event).
- The deliberately minimal mapping rule this task actually implements and tests (see section 4): for the same `RulesetId` and either the same or a differing `RulesetVersion` where every currently-purchased attribute/skill/ability/resource definition still exists unchanged in the target version's own definition catalog, `ValueChanges`/`DefinitionMappings` reflect that nothing changed (an honest empty/identity result, not a fabricated transformation); any definition reference the target version's catalog does not recognize is reported in `UnresolvedDecisions`, never silently dropped or guessed at.
- `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json`/`SLICE-04_IMPLEMENTATION_BACKLOG.md` status updates.
- This task contract and its ExecPlan.

### Out of scope

- **Any real cross-Ruleset-version content-transformation algorithm** (for example, "attribute X's cost formula changed between version 1.0.0 and 2.0.0, recompute its `SpentDevelopmentPoints`") — no such algorithm is decided by any ADR or product document, no second real Ruleset/version content exists anywhere in this codebase to test it against, and inventing one now would be exactly the kind of undecided-behavior fabrication `PLANS.md` §1.2/§3 forbids. If a future task needs this, it requires its own dedicated design decision (a new ADR amendment or product-owner decision), not an inline invention here.
- `ADR-013`'s database schema migration runner — never routed through it (`ADR-025` §7.1's own explicit boundary).
- A full-campaign backup before migration — optional per `ADR-025` §7.5; implement only if it fits cleanly through `ADR-012`'s existing Backup API, never a bespoke copy, and do not treat its absence as a defect if the task's own time is better spent elsewhere.
- Any Unity/UI code (migration-preview screen) — this task is purely Domain/Application/Persistence.
- Any change to `ADR-012`/`013`/`022`–`026` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.rules/Runtime/Character/RulesetMigrationRules.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterRulesetMigrationTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-113_Character_Ruleset_Migration.md
docs/plans/active/ODY-S04-113_Character_Ruleset_Migration.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-026*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Rules` owns the actual value/definition-mapping computation (`ADR-025` §9) — a new `RulesetMigrationRules` (or similarly named) pure-function module, not logic embedded in `Odyssey.Persistence`. `Odyssey.Application` owns the `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration` port and `CharacterRulesetMigrationPlan` DTO. `Odyssey.Persistence` owns the transaction/commit and revert-batch mechanics. Matches `ADR-001`.
- Authoritative-state and transaction boundary: `PreviewCharacterRulesetMigration` never mutates state or writes an event. `ApplyCharacterRulesetMigration` commits in exactly one `ADR-012` §5 transaction; a failure before commit rolls back via ordinary atomicity — no new rollback mechanism. Reverting an already-committed migration reuses `ADR-024`'s compensating-batch pattern unmodified in shape.
- Serialization / compatibility boundary: `Newtonsoft.Json.Linq` directly (`ADR-003`), matching every prior `SLICE-04` task.
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`–`112` already do.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: new `PersistenceFailures` entries never expose raw SQLite/IO exception text.
- Performance or platform constraint: unchanged from prior `SLICE-04` tasks.
- Other: `ApplyCharacterRulesetMigration` re-validates `PreviewHash` and current section revisions against live state before committing (`CAP-INV-004` — a client-cached preview is never trusted as final).

## 7. Expected behavior

### Scenario 1 — Preview is read-only and reproducible

**Given** an Active Character pinned to `ruleset.core`/`1.0.0`
**When** `PreviewCharacterRulesetMigration` is called targeting `ruleset.core`/`1.0.0` again (or any target where every referenced definition still resolves)
**Then** the result is a `CharacterRulesetMigrationPlan` with empty `ValueChanges`/`UnresolvedDecisions`, no event is written, and no `Character` column changes.

### Scenario 2 — Apply commits atomically and re-validates the plan

**Given** a previously built `CharacterRulesetMigrationPlan` with its own `PreviewHash`
**When** `ApplyCharacterRulesetMigration` is called with that plan
**Then** it succeeds only if the Character's current section revisions and Ruleset still match what the plan was built against; a stale plan (Character mutated since preview) is rejected without partial state change.

### Scenario 3 — Mid-application failure leaves no partial state

**Given** an `ApplyCharacterRulesetMigration` call that is forced to fail after starting its transaction (simulated, per this task's own test harness convention)
**When** the failure occurs
**Then** the Character's prior `RulesetVersion` and every mechanics value are completely unchanged, verified by a fresh read.

### Scenario 4 — An unresolvable definition reference surfaces, never silently drops

**Given** a Character referencing a definition ID that the target Ruleset's own definition catalog does not recognize (constructed via this task's own minimal test fixture, section 4)
**When** `PreviewCharacterRulesetMigration` is called
**Then** that reference appears in `UnresolvedDecisions`, and `ApplyCharacterRulesetMigration` against a plan with any unresolved decision still open is rejected until the GM resolves it.

### Scenario 5 — Reverting an already-committed migration

**Given** a successfully applied `CharacterRulesetMigrated` event
**When** the GM later reverts it
**Then** an ordered compensating batch (shared `CompensationGroupId`, `ADR-012` §6) restores the prior mechanics values and `RulesetVersion`, exactly mirroring `ODY-S04-107`'s own revert shape.

### Required invariants

- `PreviewCharacterRulesetMigration` never mutates state or writes a `DomainEvents` row.
- `ApplyCharacterRulesetMigration` never routes through `ADR-013`'s schema migration runner.
- No `ADR-012`/`013`/`022`–`026` file content changes.
- No value-mapping transformation is invented beyond the identity/`UnresolvedDecisions` scope this task actually decides (section 4/5).

## 8. Deliverables

- Production code: `RulesetMigrationRules` (Rules), `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration`/`CharacterRulesetMigrationPlan` (Application), `SqliteCharacterRepository` extension (Persistence).
- Tests: new tests in a new test file, registered from `TC-CHAR-153` onward (exact count from what is actually written).
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md` (only if new codes added), `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed.
- Migration / recovery material: None — no schema change anticipated (confirm at implementation time; record under Decisions if one becomes necessary).

## 9. Acceptance criteria

1. All pre-existing `TC-CHAR-*` tests through `TC-CHAR-152` continue passing with their own assertions unmodified.
2. `PreviewCharacterRulesetMigration` never writes an event or mutates state, verified directly (Scenario 1).
3. `ApplyCharacterRulesetMigration` rejects a stale plan (Character mutated since preview) without partial state change (Scenario 2).
4. A simulated mid-application failure leaves the Character's prior state completely unchanged, verified by a fresh read (Scenario 3).
5. An unresolvable definition reference surfaces in `UnresolvedDecisions` and blocks `Apply` until resolved (Scenario 4) — no silent drop, no invented value.
6. Reverting an already-committed migration produces a compensating batch sharing one `CompensationGroupId`, restoring prior values (Scenario 5).
7. `ApplyCharacterRulesetMigration` is never routed through `ADR-013`'s schema migration runner — confirmed by inspection, not merely by absence of a failing test.
8. No `ADR-012`/`013`/`022`–`026` content change; no Unity/UI code; no cross-version value-transformation algorithm invented beyond section 4/5's own decided scope.
9. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
10. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 13 marked `Done` with a real PR link.
11. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-153` onward (indicative) | .NET (`Odyssey.Tests.Persistence`) | Preview read-only/reproducible, Apply atomicity/stale-plan rejection/mid-failure rollback, unresolved-decision blocking, revert compensating-batch, duplicate-`CommandId` idempotency for `Apply` | Pass |

The exact IDs/count must be confirmed against the tests actually written, not assumed from this table.

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign per test, matching `ODY-S04-101`–`112`'s own fixture convention; the unresolvable-definition scenario needs a deliberately minimal second target-Ruleset fixture constructed only for this task's own tests (section 4) — document this fixture explicitly as test-only, not a real product Ruleset.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior.
- Any test asserting a specific numeric value-transformation result across two real content-bearing Ruleset versions — no such versions exist in this codebase (section 4).

## 11. Compatibility, migration, and rollback

- Compatibility impact: none anticipated — no schema change; both operations are additive.
- Version fields affected: a Character's own `RulesetVersion` (already an existing field, not a new one).
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; separately, `ApplyCharacterRulesetMigration`'s own revert path is itself a deliverable (Scenario 5), not to be confused with reverting this task's code.
- Data-loss risk and protection: `Apply` mutates only the live `Character` row; `DomainEvents` is append-only and never touched destructively; a failed apply leaves prior state intact via ordinary transaction atomicity.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: Ruleset identifiers/versions, mechanics values, migration plan/decision data — no secrets, no personal data.
- Trust boundaries: this task does not decide who may initiate a migration beyond whatever `ADR-019` action check already applies to a Mechanics-affecting command — not a new mechanism.
- Authorization / audience checks: reuse existing MainGM-gated conventions if the task contract's own review determines one is needed; do not invent a new permission constant without recording it under Decisions.
- Redaction requirements: new `PersistenceFailures` entries never expose raw SQLite/IO exception text.
- Log-safe fields: migration plan/event payloads carry only Ruleset/definition identifiers and computed changes — no secret data.
- Abuse / malformed input limits: `Apply` against a tampered/mismatched `PreviewHash` is rejected gracefully.
- Security tests: stale-plan rejection (Scenario 2) is this task's own closest analog to a tamper-detection test.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: introduces a new `Odyssey.Rules` module file, a new public Application-layer contract/DTO, and a genuinely cross-cutting transaction/compensating-batch mechanism spanning Rules/Application/Persistence.
- ExecPlan path: `docs/plans/active/ODY-S04-113_Character_Ruleset_Migration.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends on `ODY-S04-101` (done) and `ODY-S04-107` (done, compensating-batch precedent). No real dependency on `ODY-S04-112`, though branching after its own merge keeps the backlog's sequential-main convention intact — confirm `origin/main` includes PR #96 before branching, or record under Decisions if starting from an earlier `main` was unavoidable.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md` (only if new codes added), `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-026`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed (none required).
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

To be filled by the implementing agent — not applicable at task creation time.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task. Note (not a blocker): `PR #96` (`ODY-S04-112`) was still Draft/unmerged as of this task's authoring — recommend merging it first to keep `main` sequential, though this task has no real code dependency on it.

### Decisions made during execution

- None yet — to be filled by the implementing agent (for example: exact `RulesetMigrationRules` shape; whether a full-campaign backup is attempted per section 5's optional item; exact new test IDs).

### Approved task changes

- None.
