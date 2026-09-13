# ODY-S05-507 — Item-Sourced Abilities/Effects Integration Fixtures

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block — final task)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-507-integration-fixtures`
**Pull request:** [odyssey-services/Odyssey_VTT#142](https://github.com/odyssey-services/Odyssey_VTT/pull/142) (Draft)
**Plan:** `docs/plans/active/ODY-S05-507_Item_Sourced_Abilities_Effects_Integration_Fixtures.md` (Brief plan)
**Created:** 2026-09-13
**Last updated:** 2026-09-13 UTC

## 1. Goal

Prove the item-sourced abilities/effects block (`ODY-S05-502`-`506`) composes end-to-end, mirroring `ODY-S05-207`/`306`/`404`'s own integration-fixture pattern: publish real `EffectDefinition`s through the real catalog lifecycle, create an `ActiveEffect` through a genuinely real item-equip (never `ActiveEffectDirectCommandService`), exercise a real `RequestGMResolution` conflict and its resolution, exercise a real `ForDuration` expiry, suspend/resume a `WhileItemEquipped` effect through real `Equip`/`Unequip`, and exercise `RemoveActiveEffect` (rejected for a non-MainGM actor, successful for MainGM). No new production behavior.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-502`-`506` each have their own isolated unit tests, but nothing exercises the full chain a real MainGM session would actually walk — publish → equip → stack conflict → duration expiry → unequip/re-equip → explicit removal — against genuinely persisted records rather than synthetic, hand-built inputs.
- Value or risk reduction: closes the one meaningful composition gap left in the item-sourced abilities/effects block before the range (`ODY-S05-501`-`507`) can be considered complete.
- Blocking or enabling relationship: this is the final task in the block; nothing in this backlog depends on it.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 6.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §8.1/§8.2 (read only, not modified).
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §5/§6/§7 rule 7/§8/§9/§10 (read only, not modified).
- Existing patterns: `ItemDefinitionMigrationIntegrationFixtureTests.cs` (`ODY-S05-404`) — the exact structural precedent this file mirrors (real temp-directory SQLite campaign, manually-constructed repositories with no composition root, `"composition only, no new production code"` doc-comment convention, raw-SQL verification, `// ---- helpers (private methods, no new production type) ----` section); `InventoryRuntimeIntegrationFixtureTests.cs`/`EquipmentRuntimeIntegrationFixtureTests.cs` (`ODY-S05-207`/`306`) — the same precedent `404` itself already mirrored; `ActiveEffectStackingRulesTests.cs` (`ODY-S05-503`) — the precedent for composing `ActiveEffectStackingRules` against a genuinely-persisted existing row via hand-built pure-function inputs, since no repository method applies a stacking decision (that task's own deliberate design).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-507`, `SLICE-05`.
- Existing test IDs: `TC-ACTIVEEFFECT-001`-`078` (`ODY-S05-502`-`506`) as predecessor evidence.
- New test IDs introduced: `TC-ACTIVEEFFECT-079`-`084`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-507` task brief only.

## 4. Verified current state

### Verified facts

- `origin/main` at `2dfb7fa`, the merge of PR #141 (`ODY-S05-506`) — confirmed via `git fetch origin` immediately before starting. Backlog row 6 in §15 (`ODY-S05-507`) was `Proposed`.
- Direct read of `ODY-S05-502`-`506`'s own doc comments confirmed the governing ТЗ's own premise: none of them reserved a "fixture hook" of any kind — all five describe closed, complete implementations. Confirmed during implementation (not merely assumed in advance): every operation this task's own 6 tests needed already existed as public API.
- `IActiveEffectRepository` (direct code read) has all 7 methods this task composes: `CreateActiveEffect`, `GetActiveEffect`, `ListActiveEffectsByTarget`, `ListActiveEffectsBySource`, `ExpireActiveEffect`, `SetItemEffectEquipped`, `RemoveActiveEffect`. `ActiveEffectDirectCommandService.CreateDirectActiveEffect` exists but is deliberately not used here — the backlog's own scenario begins with real item use, not direct GM creation.
- `ItemEffectLifecycleService.OnItemEquipped`'s own inline comment ("`503` currently provides decisions, not atomic stacking writes. Do not silently bypass a policy that requires those writes.") confirmed by direct code read: a second real item-use attempt for a non-`IndependentInstances`-policy effect that already has a live row would make `OnItemEquipped` itself fail the whole reaction, not compose with `ActiveEffectStackingRules`. This task's own `RequestGMResolution` scenario therefore composes `ActiveEffectStackingRules.ResolveStacking`/`ResolveActiveEffectStackConflict` directly against a genuinely-persisted first application (from a real `OnItemEquipped` call), exactly the way `ActiveEffectStackingRulesTests.cs` (`ODY-S05-503`) itself already does — not a gap in this task's own composition, but the same, already-accepted pattern that task's own tests established for exercising a decision layer with no corresponding repository write method.
- `EquippedEntry`'s own constructor (direct code read, `EquippedEntry.cs:59`) requires `equipmentSlotRef` to satisfy `InventoryOwnerRef.IsToken` — lowercase letters/digits/`_`/`-` only, confirmed by direct code read of `IsToken`'s own character-class check (`InventoryRuntime.cs:108`). A first test-authoring attempt used mixed-case slot keys (`"slotA"`) and failed; fixed to lowercase-token slot keys (`"slot_a"`, etc.) — a test-fixture correction, not a production concern.
- A first test-authoring attempt for the `ForDuration` scenario hardcoded an absolute `ExpiresAt` string close to the session's own real calendar date; since `ItemEffectLifecycleService.OnItemEquipped` itself rejects a `ForDuration` effect whose resolved expiry is not strictly after the real `EquippedAt` (real wall-clock time at test-run time), the hardcoded absolute timestamp intermittently preceded "now" and failed. Fixed by computing `ExpiresAt`/comparison timestamps relative to the real `Clock.GetUtcNow()` captured at equip time, matching the pattern already used elsewhere in this task's own fixture.
- No Composition root or DI container exists anywhere in the codebase (re-confirmed by direct search of `RuntimeComposition.cs`/`OdysseyRuntimeHost.cs` — zero `ActiveEffect` references) — this fixture constructs every repository by hand (`new SqliteContentCatalogRepository(...)`, `new SqliteInventoryRepository(...)`, `new SqliteCharacterRepository(...)`, `new SqliteActiveEffectRepository(...)`), exactly as `ODY-S05-207`/`306`/`404` already do.

### Assumptions

- None.

## 5. Scope

### In scope

- New test file `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ActiveEffectIntegrationFixtureTests.cs`.
- Test metadata and backlog/docs updates.

### Out of scope

- Any change to `IActiveEffectRepository`/`SqliteActiveEffectRepository`, `ItemEffectLifecycleService`, `ActiveEffectStackingRules`, `ActiveEffectExpiryRules`, `ActiveEffectDirectCommandService`, `EquipmentService`, or `ContentCatalogLifecycleService`/`ContentCatalogAuthoringService`/`InventoryCreationService` — all called, none modified.
- Any turn/round-based `EffectDurationType` value or combat mechanic (out of scope for this entire block, `ADR-028` §13).
- `docs/adr/ADR-028_...md` — not touched.
- A composition root / DI container — not created; the fixture constructs its own participants by hand, exactly like its three precedents.
- Any new production business rule. No fixture hook was needed — confirmed during implementation, not merely assumed in advance (§18).

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Integration/ActiveEffectIntegrationFixtureTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-507_Item_Sourced_Abilities_Effects_Integration_Fixtures.md
docs/plans/active/ODY-S05-507_Item_Sourced_Abilities_Effects_Integration_Fixtures.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectStackingRulesTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectExpiryRulesTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/ItemEffectLifecycleTests.cs
DotNet/Tests/Odyssey.Tests.Persistence/ActiveEffectGmCommandTests.cs
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: test-only; no production module touched.
- Authoritative-state and transaction boundary: no new write path; every mutation goes through already-accepted public repository/service methods.
- Serialization / compatibility boundary: not applicable.
- Time / RNG rule: `IWallClock Clock = new SystemWallClock()`, matching `ODY-S05-207`/`306`/`404`'s own test convention; every expiry timestamp is computed relative to a real captured `Clock.GetUtcNow()`, never a hardcoded absolute string (§4's own recorded fix).
- Unity / thread / lifetime rule: no Unity files.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable (test-only, synthetic data).
- Other: a real, temp-directory SQLite campaign per test (via `SqliteCampaignRepository.Create`), never in-memory or mocked — the exact `ODY-S05-207`/`306`/`404` precedent; every repository is constructed by hand, no composition root.

## 7. Expected behavior

### Scenario 1 — item-triggered creation

**Given** a real, published `EffectDefinition` and a real `ItemDefinition` whose `BuiltInEffectRefs` names it
**When** the item is equipped through `EquipmentService.Equip` and `ItemEffectLifecycleService.OnItemEquipped` reacts
**Then** a real `ActiveEffect` row is created, `Active`, sourced `ForEquippedItem`, verified by raw SQL.

### Scenario 2 — WhileItemEquipped suspend/resume

**Given** a real `WhileItemEquipped` effect created via Scenario 1's own path
**When** the item is unequipped (`EquipmentService.Unequip` + `OnItemUnequipped`) and then re-equipped (`EquipmentService.Equip` + `OnItemEquipped`)
**Then** the same row (`ActiveEffectId` unchanged) transitions `Active → Suspended → Active`, never a duplicate — verified by raw SQL row count.

### Scenario 3 — RequestGMResolution conflict and resolution

**Given** a real, already-persisted `ActiveEffect` whose `EffectDefinition` has `StackPolicy = RequestGMResolution`
**When** a second, hand-built-but-realistic candidate application is resolved via `ActiveEffectStackingRules.ResolveStacking`/`ResolveActiveEffectStackConflict` (`ApplyAsIndependentInstance`)
**Then** a real second, independent row is created via the unmodified `CreateActiveEffect` — verified by raw SQL row count for the shared `EffectDefinitionRef`.

### Scenario 4 — ForDuration expiry

**Given** a real `ForDuration` effect created via item-equip with a host-supplied expiry resolver
**When** `ActiveEffectExpiryRules.CheckForDurationExpiry` is checked before and after the real `ExpiresAt`, and `ExpireActiveEffect` is applied once it reports `Expired`
**Then** the row transitions to `Expired`, survives as history (raw SQL), never physically deleted.

### Scenario 5 — RemoveActiveEffect denied for a non-MainGM actor

**Given** a real, `Active` `ActiveEffect` row
**When** `RemoveActiveEffect` is called by a non-MainGM actor
**Then** it fails with `persistence.active_effect.operation_denied`, and the real row is left completely unmutated (raw SQL `Status`/`Revision` check, plus an unchanged `DomainEvents` row count).

### Scenario 6 — RemoveActiveEffect succeeds for MainGM

**Given** the same setup
**When** `RemoveActiveEffect` is called by a MainGM actor
**Then** the row transitions to `Removed`, `Revision` increments, survives as history (raw SQL).

### Required invariants

- Every `EffectDefinition`/`ItemDefinition` is published through the real catalog lifecycle, never constructed as a given `ContentDefinitionRecord` directly.
- `ActiveEffect` creation happens only through `ItemEffectLifecycleService.OnItemEquipped` (item-triggered) or the unmodified `CreateActiveEffect` (the stacking-resolution outcome) — `ActiveEffectDirectCommandService` is never used, since the backlog's own scenario begins with real item use.
- A denied `RemoveActiveEffect` attempt never mutates any row and never appends a `DomainEvents` row.
- No new production code exists anywhere in this task's diff.

## 8. Deliverables

- Production code: none.
- Tests: `ActiveEffectIntegrationFixtureTests.cs`, `TC-ACTIVEEFFECT-079`-`084`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, Brief plan, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none.

## 9. Acceptance criteria

1. A new integration fixture test file exists under `DotNet/Tests/Odyssey.Tests.Persistence/Integration/`, following the `ODY-S05-207`/`306`/`404` structural precedent.
2. All 6 backlog-named scenarios are covered — item-triggered creation, `WhileItemEquipped` suspend/resume, a real `RequestGMResolution` conflict + resolution, a real `ForDuration` expiry, `RemoveActiveEffect` denied for non-MainGM, `RemoveActiveEffect` succeeds for MainGM.
3. No mock/fake stands in anywhere a real service already exists.
4. A denied `RemoveActiveEffect` attempt is proven to leave zero mutation, including an unchanged `DomainEvents` row count.
5. `TC-ACTIVEEFFECT-079`-`084` are registered in `Tests/Metadata/test-catalog.json` with descriptions matching actual test behavior.
6. No new production business logic exists in the diff; no fixture hook was needed (confirmed, not merely assumed).
7. `dotnet test` is green; backlog row `ODY-S05-507` → `In Review`, completing the item-sourced abilities/effects range (`501`-`507`).
8. PR is Draft; merge is the product owner's decision.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-079` | .NET / NUnit (Persistence Integration, real SQLite) | Equipping an item with BuiltInEffectRefs creates a real ActiveEffect through ItemEffectLifecycleService | Pass |
| `TC-ACTIVEEFFECT-080` | .NET / NUnit (Persistence Integration, real SQLite) | Unequip then re-equip suspends then resumes the same WhileItemEquipped row through real Equip/Unequip | Pass |
| `TC-ACTIVEEFFECT-081` | .NET / NUnit (Persistence Integration, real SQLite) | Reapplying an effect with RequestGMResolution raises a real conflict, resolved through a real second row | Pass |
| `TC-ACTIVEEFFECT-082` | .NET / NUnit (Persistence Integration, real SQLite) | A ForDuration effect expires for real through CheckForDurationExpiry and ExpireActiveEffect | Pass |
| `TC-ACTIVEEFFECT-083` | .NET / NUnit (Persistence Integration, real SQLite) | RemoveActiveEffect by a non-MainGM actor is rejected with no mutation in the real database | Pass |
| `TC-ACTIVEEFFECT-084` | .NET / NUnit (Persistence Integration, real SQLite) | RemoveActiveEffect by MainGM succeeds, transitioning the real row to Removed | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm no file under `Packages/**` changed.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: not applicable.
- Scripting backend: not applicable.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database.
- Other: pure .NET build/test path.

### Validation not required by this task

- Unity Editor/Player validation because no Unity files change.
- Any re-verification of `ODY-S05-502`-`506`'s own unit-level correctness — only that they compose.

## 11. Compatibility, migration, and rollback

- Compatibility impact: none — test-only.
- Version fields affected: none.
- Migration or upcaster: none.
- Forward / backward behavior: unaffected.
- Rollback method: revert the branch/PR before merge.
- Data-loss risk and protection: none — no production code changes.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory/character/effect test records; no real player data.
- Trust boundaries: none new.
- Authorization / audience checks: exercised (MainGM-only for `RemoveActiveEffect`), not modified.
- Redaction requirements: not applicable.
- Log-safe fields: not applicable.
- Abuse / malformed input limits: not applicable (test-only).
- Security tests: none new (this task is a composition proof, not a security boundary) — it re-exercises `506`'s own MainGM gate through a realistic scenario.

## 14. Planning and execution mode

- Planning mode: `Brief`.
- Reason for selected mode: one module (tests only), no new public contract/schema/permissions/architecture, one PR, no data migration — matching `ODY-S05-207`/`306`/`404`'s own precedent exactly, per `PLANS.md` §1.1.
- Brief plan path: `docs/plans/active/ODY-S05-507_Item_Sourced_Abilities_Effects_Integration_Fixtures.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #141; closes the item-sourced abilities/effects range (`501`-`507`).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, Brief plan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs; any file under `Packages/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: none.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/ActiveEffectIntegrationFixtureTests.cs` — new file, `TC-ACTIVEEFFECT-079`-`084`.
- `Tests/Metadata/test-catalog.json` — 6 new entries registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-507` updated to `In Review` (matching the existing convention observed for the two most recently closed blocks, `Equipment`/`ItemDefinition migration`, neither of which received a separate "completed" marker beyond their own final row's own status — no such note is added here either).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 612/612 (606 predecessor + 6 new). |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | No new `ERROR_CODES.md` rows required (test-only task). |
| `.\scripts\verify-test-structure.ps1` | PASS | |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (structural precedent followed) | Met | Doc-comment and `SetUp`/`TearDown` mirror `ItemDefinitionMigrationIntegrationFixtureTests.cs` exactly. |
| AC-2 (all 6 scenarios covered) | Met | `TC-ACTIVEEFFECT-079`-`084`, one per scenario. |
| AC-3 (no mock/fake where a real service exists) | Met | Code review; every repository is a real `Sqlite*` implementation. |
| AC-4 (denied removal proven zero-mutation) | Met | `TC-ACTIVEEFFECT-083`. |
| AC-5 (test metadata registered, descriptions accurate) | Met | `Tests/Metadata/test-catalog.json`. |
| AC-6 (no new production logic; no fixture hook needed) | Met | `git diff --name-status` touches only `DotNet/Tests/**`/`Tests/Metadata/**`/`docs/**`. |
| AC-7 (dotnet test green, backlog updated) | Met | Table above; backlog row updated. |
| AC-8 (Draft PR) | Met | PR #142 opened as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- None specific to this task — it is a pure composition proof over already-accepted, already-tested surfaces.

### Follow-up tasks

- None within `SLICE-05`'s item-sourced abilities/effects range — `501`-`507` is complete after this PR merges. `ODY-S05-505-F01` (`CharacterAbility` suspend/resume) and the pre-existing Unity/IL2CPP asmdef gap (both recorded by `ODY-S05-505`) remain their own separate, unscheduled follow-ups, outside this range's own numbered sequence.

### Self-review summary

- Scope review: diff touches only test/doc/metadata paths; zero production files changed; confirmed no fixture hook was needed.
- Architecture review: no new abstractions; every operation composes already-accepted public repository/service methods; no composition root introduced.
- Test review: all 6 required scenarios covered with genuinely-persisted (not synthetic) records wherever a real repository call could produce them; raw SQL verifies real persistence, not just in-memory return values.
- Security/privacy review: not applicable (test-only, synthetic data); re-exercises `506`'s own MainGM gate realistically.
- Documentation/version review: task contract, Brief plan, test catalog, and backlog all updated; no schema/manifest/protocol version bump.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-13 — **Design decision: the `RequestGMResolution` scenario composes `ActiveEffectStackingRules` directly against a genuinely-persisted first application, rather than attempting to trigger the conflict through a second real `OnItemEquipped` call.** Direct code read of `ItemEffectLifecycleService.OnItemEquipped`'s own inline comment confirmed this is not merely this task's own convenience shortcut: `OnItemEquipped` itself deliberately refuses to silently bypass a non-`IndependentInstances` stacking policy that would need an atomic write no repository method provides — a second real item-use attempt for the same effect would make the whole reaction fail outright, not produce a conflict to resolve. Composing `ActiveEffectStackingRules.ResolveStacking`/`ResolveActiveEffectStackConflict` directly against the real, already-persisted first row (loaded via the unmodified `GetActiveEffect`) is not a workaround invented for this task — it is the exact same pattern `ActiveEffectStackingRulesTests.cs` (`ODY-S05-503`) itself already established for exercising this decision layer, since `503`'s own deliberate design left no repository method to apply a stacking decision. Authority: `ItemEffectLifecycleService.cs`'s own inline comment; `ActiveEffectStackingRulesTests.cs`'s own established composition pattern.
- 2026-09-13 — **Design decision: the `WhileItemEquipped` and `RequestGMResolution`/`ForDuration` scenarios use separate items, never the same item's own `BuiltInEffectRefs` list.** An early design considered combining both effect definitions onto one item to reduce setup code, but tracing `OnItemEquipped`'s own per-index "existing" retention logic showed a real re-equip of a multi-effect item could spuriously re-trigger the very stacking-collision rejection this task deliberately composes around by hand elsewhere, coupling two independently-meaningful scenarios' own state in a fragile way. Separate items keep each scenario's own real persisted state fully independent, matching the backlog's own framing of six distinct scenario points, not one entangled one. Authority: direct trace of `OnItemEquipped`'s own retention-loop logic against a hypothetical shared-item design, performed before writing the test, not discovered by a failing run.
- 2026-09-13 — **Test-fixture fix: `EquippedEntry`'s own constructor requires a lowercase-token `EquipmentSlotRef` (`InventoryOwnerRef.IsToken`'s own character-class check) -- an initial mixed-case slot key (`"slotA"`) failed.** Fixed to lowercase-token slot keys (`"slot_a"`, `"slot_b"`, etc.) across every scenario. A test-authoring correction, not a production concern — confirmed by direct code read that `IsToken` has never accepted uppercase letters, in this task's diff or any predecessor's.
- 2026-09-13 — **Test-fixture fix: the `ForDuration` scenario's `ExpiresAt`/comparison timestamps are computed relative to the real `Clock.GetUtcNow()` captured at equip time (`UtcInstant.Add(TimeSpan)`), not a hardcoded absolute timestamp string.** An initial hardcoded absolute `ExpiresAt` intermittently preceded the real wall-clock time at test-run time, tripping `OnItemEquipped`'s own "expiry must be strictly after `EquippedAt`" guard non-deterministically depending on when the suite happened to run. Fixed once, verified by re-running the full suite afterward. A test-authoring correction, not a production concern.
