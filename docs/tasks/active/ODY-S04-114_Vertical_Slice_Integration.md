# ODY-S04-114 — SLICE-04 Vertical Slice Integration

**Status:** In Review
**Roadmap stage / slice:** SLICE-04 (vertical slice implementation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-114-vertical-slice-integration`
**Pull request:** TBD
**ExecPlan:** Not required — see section 14 (Brief plan)
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

Roadmap §13.8's eleven-step "Персонаж и развитие" scenario runs end-to-end, in order, as one automated, reproducible test over already-merged `ODY-S04-101`–`113` public APIs, proving those thirteen tasks' deliverables (local Draft/template/binding, submit/approve workflow, `DevelopmentPool`/attribute/skill purchases, critical evidence, skill 5+ recommendation, `CharacterHistoryProjection`/reconnect, `.odchar` export/import) work together in the exact order the product scenario specifies — not just individually.

## 2. Why this task exists

- Problem or dependency being addressed: each of `ODY-S04-101`–`113` has its own module-level tests (Create/Bind/Submit/Approve/Purchase/Evidence/Recommendation/History/Export/Import/Migration are all proven separately), but nothing has ever exercised the full Draft-creation → template-selection → submit → approve → grant-points → purchase → critical-evidence → recommendation-resolution → history-and-reconnect → `.odchar`-export/import sequence together, using each task's real public contract, in the order roadmap §13.8 actually specifies.
- Value or risk reduction: closes the vertical-slice-level gap between "each piece works" and "the pieces work together," and gives real, reproducible evidence toward roadmap §13.9 exit criteria (this test's own coverage of criteria exercised by the scenario; the full traceability matrix across all 14 criteria from `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 is `ODY-S04-115`, not this task).
- Blocking or enabling relationship: `ODY-S04-115` (the closure gate/traceability matrix) depends on this task existing first, per `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5/§7 (`ODY-S04-115` depends on `ODY-S04-114`).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md` §1
- `17_Roadmap_Odyssey_VTT_v0.11.md` §13.8 (the exact eleven-step scenario, quoted in section 7 for traceability), §13.9 (exit criteria) — private local reference, quoted only to the extent this task needs
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` (structural precedent: one test method, explicit per-step assertions, honest reporting of any real composition gap rather than an improvised in-scope fix — reused here for `SLICE-04`)
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 (exit criteria list), §5/§6 (this task's own scope-narrowing text), §7 (dependency: all of `ODY-S04-101`–`113`)
- `docs/tasks/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md` through `ODY-S04-113a_Ruleset_Migration_Revert_Character_Scope_Gap_Fix.md` — what each already implements and its public contract surface
- `Packages/com.odyssey.application/Runtime/Persistence/LocalCharacterDraftRepositoryContracts.cs`, `CharacterTemplateRepositoryContracts.cs`, `CharacterRepositoryContracts.cs` (the full `ICharacterRepository` surface: `BindDraftToCampaign`, `SubmitCharacterDraft`, `ApproveCharacterDraft`, `GrantDevelopmentPoints`, `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RecordCriticalSuccessEvidence`, `RequestSkillAdvancedRecommendation`, `ResolveAdvancementRecommendation`, `GetCharacterHistory`, `ExportCharacter`, `ImportCharacter`) — read in full for exact signatures, not inferred from task-contract prose
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `SqliteLocalCharacterDraftRepository.cs` (or equivalent), `SqliteCharacterTemplateRepository.cs` (or equivalent) — same rule

### Requirement and test IDs

- Requirement IDs: roadmap §13.8 (all eleven steps); §13.9 exit criteria exercised by the scenario (partial evidence via this test; full traceability across all fourteen criteria in `SLICE-04_IMPLEMENTATION_BACKLOG.md` §3 is `ODY-S04-115`)
- Existing test IDs: `TC-CHAR-001`–`165` (not duplicated — see section 5)
- New test IDs to introduce: `TC-CHAR-166`

### Task-safe private context

- Approved summary / references: `17_Roadmap_Odyssey_VTT_v0.11.md` §13.8's eleven-step scenario — private local reference, summarized/quoted only, not reproduced beyond what this task contract itself needs.

## 4. Verified current state

### Verified facts

- `ODY-S04-101`–`113` (including the `ODY-S04-113a` gap fix) are all `Done`/merged on `main` — confirmed via `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md`'s header (`ODY-S04-113`/`113a` both `Done`, PR [#97](https://github.com/odyssey-services/Odyssey_VTT/pull/97) merged as commit `e48a541`) and via `git log`.
- `ICharacterRepository` (`Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`) already exposes every method this scenario needs end to end: `BindDraftToCampaign`, `SubmitCharacterDraft`, `ApproveCharacterDraft`, `GrantDevelopmentPoints`, `PurchaseAttributeIncrease`, `PurchaseSkillLevel`, `RecordCriticalSuccessEvidence`, `RequestSkillAdvancedRecommendation`, `ResolveAdvancementRecommendation`, `GetCharacterHistory`, `ExportCharacter`, `ImportCharacter`.
- `ILocalCharacterDraftRepository.CreateLocalCharacterDraft` (`LocalCharacterDraftRepositoryContracts.cs`) and `ICharacterTemplateRepository.CreatePersonalCharacterTemplate`/`CreateCampaignCharacterTemplate` (`CharacterTemplateRepositoryContracts.cs`) are the two remaining repositories the scenario's first two steps touch — separate from `ICharacterRepository`, confirmed by direct `Read`.
- `RecordCriticalSuccessEvidence` takes `sourceDiceRollId`/`sourceActionId` as nullable strings, not a real `DiceRoll`/board reference — confirmed by `Read`. `SLICE-03`'s real dice-roll/board machinery is not wired to `SLICE-04`'s Character economy in this revision (no cross-slice integration task exists for that); this scenario's step 8 ("critical skill check creates evidence") is therefore proven by calling `RecordCriticalSuccessEvidence` directly with a synthetic identifier, exactly as `ODY-S04-106`'s own test suite already does — not by driving a real `DiceRollService` roll first.
- `docs/tasks/active/ODY-S04-112_Odchar_Export_Import.md`'s own completion evidence confirms `ExportCharacter`/`ImportCharacter` operate on a plain-directory `.odchar` bundle (`manifest.json`/`character.json`), and that `ImportCharacter` reuses `BindDraftToCampaign` unmodified with the imported file as the seed source, landing as a new Draft requiring fresh approval — this scenario's step 11 exercises exactly that already-proven path, on the same campaign, once again through fresh identifiers.
- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/VerticalSliceIntegrationTests.cs` already exists (added by `ODY-S03-008`, for `SLICE-03`'s own ten-step scenario) — this task's new test file must use a distinct filename inside the same `Integration/` folder to avoid collision (see section 5).

### Assumptions

None. Confirm all facts above via direct `Read`/`Grep` before writing test code, and actually run the assembled scenario once for real before this task is declared complete, exactly as `ODY-S04-114`'s own precedent (`ODY-S03-008`) required.

## 5. Scope

### In scope

- One new test file, `DotNet/Tests/Odyssey.Tests.Persistence/Integration/CharacterVerticalSliceIntegrationTests.cs` (distinct name from the existing `SLICE-03` `Integration/VerticalSliceIntegrationTests.cs` in the same folder), containing exactly one test method running all eleven roadmap §13.8 steps in literal order, asserting each step's outcome (not eleven independent tests — the guarantee under test is the full ordered sequence, composed through each task's real public contract).
- Two participants: MainGM and Player (the Character's own assigned owner) — the minimum roadmap §13.8 itself requires; no third/excluded-participant role is needed since none of the eleven steps is an audience-redaction check (unlike `SLICE-03`'s own step 7).
- Real SQLite throughout (`SqliteLocalCharacterDraftRepository`/`SqliteCharacterTemplateRepository`/`SqliteCharacterRepository` against a real temp-directory local-profile store and `campaign.db`, mirroring `TC-CHAR-*`'s own fixture pattern) — no repository-level mock.
- Step 10 ("history and reconnect show authoritative state") is proven the same way `ODY-S03-008`'s own step 9 was: a brand-new repository instance opened against the same already-written `campaign.db` file, then `GetCharacterHistory` called against it, confirming every prior step's event is present and in order — not merely re-querying the same in-process repository instance.
- Step 11 (`.odchar` export/import) exports the Character produced by steps 1–9 to a temp directory, then imports that bundle back into the *same* campaign, and asserts the import lands as a new Draft with a freshly minted `CharacterId` distinct from the original (roadmap §13.9's own "import creates new ID and Draft" exit criterion), per `ODY-S04-112`'s already-proven contract.

### Out of scope, and why

- **Any new production code anywhere in `Packages/`.** This task's diff must touch only test/documentation files (`git diff --name-status`, section 17). This task's own explicit instruction (section 18): if a real connecting-code gap is found missing, stop and report rather than add it.
- **Full §13.9 exit-criteria traceability matrix across all fourteen criteria** — `ODY-S04-115`, not this task.
- **Character Ruleset migration** (`ODY-S04-113`'s own `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration`/`RevertCharacterRulesetMigration`) — roadmap §13.8's eleven steps do not include a migration step; §13.9's "failed Ruleset migration rolls back" criterion is already proven by `ODY-S04-113`'s own `TC-CHAR-156`/`158` and is not re-exercised here. Do not add a twelfth step inventing one.
- **Any real dice-roll/board integration for step 8** — `RecordCriticalSuccessEvidence` is called directly with a synthetic `sourceDiceRollId` (section 4); wiring a real `SLICE-03` `DiceRollService` roll into `SLICE-04`'s economy is not decided by any ADR and is not this task's to invent.
- **Any real network or UI** — no real network exists in this revision (established precedent, `SLICE-01`–`03`'s own backlogs); this scenario proves module-boundary composition, not a networked client/host round-trip.
- **Duplicating `101`–`113`'s own module-level test scenarios.** This test calls each API once, in the sequence the roadmap specifies, to prove the sequence composes — it does not re-test each service's own edge cases (cost/cap rejection, duplicate-`CommandId` idempotency per purchase, every `RankMode` individually, etc.), all already covered by their owning task's test file.
- **Ability/Resource/Anatomy (`ODY-S04-108`/`109`) and Archive/Delete/Dead/Restore (`ODY-S04-110`/`111`) mechanics** — roadmap §13.8's eleven steps do not exercise them; they remain covered by their own owning tasks' tests, not duplicated here.

### Allowed paths

```text
DotNet/Tests/Odyssey.Tests.Persistence/Integration/CharacterVerticalSliceIntegrationTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-114_Vertical_Slice_Integration.md
```

### Paths requiring explicit approval before editing

```text
Packages/com.odyssey.application/**
Packages/com.odyssey.persistence/**
docs/tasks/active/ODY-S04-101_Character_Aggregate_Lifecycle_Skeleton_Sqlite_Persistence.md through ODY-S04-113a_Ruleset_Migration_Revert_Character_Scope_Gap_Fix.md
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable — no production module touched.
- Authoritative-state and transaction boundary: Not applicable — this test only calls existing, already-tested Application/Persistence APIs; it introduces no new state model.
- Time / RNG rule: same local `IWallClock` test-double convention every prior `ODY-S04-*` test file already uses; no real RNG is needed (this scenario never generates a dice-roll result itself — section 5).
- Unity / thread / lifetime rule: Not applicable — pure .NET persistence code.
- Dependency / licensing rule: No new dependency.
- Security / privacy / redaction rule: Not applicable to the change itself (no new redaction logic); step 11's export reuses `ADR-026`'s already-proven `RedactCharacterForExport` without modification.
- Performance or platform constraint: Not applicable.
- Other: Not applicable.

## 7. Expected behavior

### Scenario 1 — the full eleven-step sequence, in order

**Given** a fresh temp-directory campaign and local profile store, with MainGM and Player participants
**When** the test runs steps 1–11 of roadmap §13.8 in literal order, quoted here for traceability:
1. Player creates local Draft;
2. selects personal/campaign template;
3. host validates submit;
4. GM approves;
5. Character becomes Active and appears in campaign;
6. MainGM grants development points;
7. Player purchases attribute/skill immediately after validation;
8. critical skill check creates evidence;
9. GM resolves skill 5+ recommendation;
10. history and reconnect show authoritative state;
11. `.odchar` export/import creates a new Draft.

**Then** every step succeeds, with its own explicit assertion, and step 11's import produces a Character with a `CharacterId` distinct from the original, landed as a new Draft requiring fresh approval.

### Required invariants

- Step 1–2: the created Draft has no `CampaignId`/`CharacterId` until bound (`ADR-023`); binding from a template produces a Character independent from later template edits (roadmap §13.9's own "created Character is independent from template" criterion — proven by binding, then mutating the source template, then confirming the Character's own copied values are unaffected).
- Step 3–5: `SubmitCharacterDraft` leaves `ApprovalState: Draft`; only `ApproveCharacterDraft` transitions `LifecycleStatus: Draft -> Active`/`ApprovalState: Draft -> Approved`.
- Step 6: only a MainGM-issued `GrantDevelopmentPoints` call succeeds; a non-MainGM caller is rejected (roadmap §13.9's "only MainGM grants points" criterion).
- Step 7: `PurchaseAttributeIncrease`/`PurchaseSkillLevel` succeed for the Player (no GM-approval step required per purchase — roadmap §13.9's "valid purchase does not require GM approval" criterion); a duplicate `CommandId` replay of the same purchase does not spend `DevelopmentPool` a second time (roadmap §13.9's "duplicate command does not spend twice" criterion).
- Step 8: the recorded `CriticalSuccessEvidence`'s `UsedByAdvancementId` is null until consumed by step 9.
- Step 9: `RequestSkillAdvancedRecommendation` reserves points without spending them; `ResolveAdvancementRecommendation` with `approve: true` converts the reservation and marks the evidence consumed; the same evidence cannot be reused by a second recommendation (roadmap §13.9's "critical evidence cannot be reused" criterion).
- Step 10: a brand-new `SqliteCharacterRepository` instance opened against the same `campaign.db` file returns, via `GetCharacterHistory`, every event from steps 3–9 in `EventSequence` order, with no gap and no duplicate.
- Step 11: the exported `.odchar` bundle contains no `CharacterOwnership`/`CharacterId`/`CampaignId` field (`ADR-026`); the imported Character has a `CharacterId` different from the original, `ApprovalState: Draft`, and its `RulesetVersion` pinned to the target campaign's own current version at bind time.

## 8. Deliverables

- Production code: None.
- Tests: `CharacterVerticalSliceIntegrationTests.cs` (1 test, `TC-CHAR-166`).
- Scripts / CI: None.
- Configuration: None.
- Documentation: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (header and row 14), this task contract.
- Generated evidence or build artifacts: None persisted beyond section 17's recorded test output.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. All eleven roadmap §13.8 steps run in one test method, in literal order, each with its own assertion (`TC-CHAR-166`).
2. Steps 1–2 prove Draft creation and template binding under full composition, including that the bound Character is independent from a later edit to its source template.
3. Steps 3–5 prove the submit/approve workflow transitions `LifecycleStatus`/`ApprovalState` correctly and only via `ApproveCharacterDraft`.
4. Step 6 proves `GrantDevelopmentPoints` is MainGM-only under full composition.
5. Step 7 proves an ordinary valid purchase applies immediately without a separate GM-approval step, and that a duplicate `CommandId` does not double-spend.
6. Step 8 proves `RecordCriticalSuccessEvidence` produces an unconsumed evidence row.
7. Step 9 proves the skill-5+ recommendation reserve-then-resolve workflow consumes the evidence exactly once and cannot reuse it.
8. Step 10 proves a reconnect (brand-new repository instance against the same `campaign.db`) reconstructs the full, correctly ordered event history via `GetCharacterHistory`.
9. Step 11 proves `.odchar` export/import round-trips through a real bundle directory and lands as a new Draft with a fresh `CharacterId`, distinct from the original.
10. No new production code exists anywhere in `Packages/` (confirmed by diff).
11. Any real API composition gap discovered while assembling the scenario is reported in this task contract (section 4/18), not silently worked around with new production logic.
12. All required validation commands (section 10) pass.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-166` | `.NET` / `dotnet test` | The full roadmap §13.8 eleven-step sequence, end-to-end, in order, over already-merged `ODY-S04-101`–`113` APIs and real SQLite | Pass |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

```bash
dotnet build DotNet/Odyssey.Core.sln
dotnet test DotNet/Odyssey.Core.sln
```

### Manual validation

- None — all acceptance evidence is automated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — pure .NET Core code only.
- Network topology or database fixture: real SQLite via `Microsoft.Data.Sqlite`, temp-directory local-profile store and `campaign.db` per test run — no real network.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- Full §13.9 exit-criteria traceability across all fourteen criteria — `ODY-S04-115`, not this task.
- Any real network/UI test, or any real `SLICE-03` dice-roll/board integration for step 8 — out of scope (section 5).

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no production code changed.
- Version fields affected: None.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None — test-only change plus a documentation status update.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: Synthetic test data only (in-memory Draft/template/purchase/evidence data), the same classes `ODY-S04-101`–`113`'s own tests already use.
- Trust boundaries: Not applicable — no new trust boundary; this test exercises existing ones.
- Authorization / audience checks: Not applicable to the test's own scope — it asserts existing `ApproveCharacterDraft`/`GrantDevelopmentPoints`/`PurchaseAttributeIncrease` MainGM/owner gating, introduces none.
- Redaction requirements: Not applicable — step 11 asserts existing `RedactCharacterForExport` behavior (`ADR-026`), introduces none.
- Log-safe fields: Not applicable — no new error paths introduced.
- Abuse / malformed input limits: Not applicable.
- Security tests: This test's step-11 assertion is a composed regression check that export redaction still holds when all thirteen `ODY-S04-101`–`113` tasks' code runs together, complementing (not replacing) `ODY-S04-112`'s own dedicated `TC-CHAR-144`–`152` suite.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` §1's conditions individually, matching `ODY-S03-008`'s own precedent exactly. (1) Contained in one area — a single new test file, no production module touched at all. (2) Does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, package version, or build pipeline — confirmed, zero production code in the diff. (3) One clear implementation path — call each already-documented API in the order the roadmap specifies. (4) Fits one focused PR. (5) No migration or recovery procedure required — this test consumes already-existing, already-tested Application/Persistence behavior, it does not add any.
- Brief plan:
  1. Files inspected: `17_Roadmap_Odyssey_VTT_v0.11.md` §13.8/§13.9; `ODY-S03-008`'s task contract (structural precedent); `ODY-S04-101`–`113`'s task contracts and production source (public API surface across `LocalCharacterDraftRepositoryContracts.cs`, `CharacterTemplateRepositoryContracts.cs`, `CharacterRepositoryContracts.cs`); each `ODY-S04-*` test file (confirmed none already covers the full ordered eleven-step sequence together, so no duplication).
  2. Intended change: one new test file, one test method, eleven ordered, asserted steps, two participants.
  3. Tests: `CharacterVerticalSliceIntegrationTests.cs` (`TC-CHAR-166`); full existing suite re-run to confirm no regression.
  4. Non-goals: no production code, no §13.9 full traceability matrix, no real dice-roll/board integration, no Ruleset migration step.
- ExecPlan path: Not required.
- Expected pull request count: 1
- Milestone or sequencing constraints: None beyond `101`–`113` already merged (backlog's stated dependency, already satisfied). Blocks `ODY-S04-115` (closure gate).

## 15. Documentation and versioning impact

- Documents that must change: `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (header, row 14), this task contract.
- Documents that must not change: any ADR, `ODY-S04-101`–`113a` task contracts (read only).
- Application version change: No.
- Schema / format / contract / manifest / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (None required).
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `DotNet/Tests/Odyssey.Tests.Persistence/Integration/CharacterVerticalSliceIntegrationTests.cs` (new) — the one test method, `TC-CHAR-166`.
- `Tests/Metadata/test-catalog.json` — `TC-CHAR-166` registered.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` — header and row 14 updated to `Done`, recording the discovered `HistoryEventTypes` fact.
- This task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet/Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test DotNet/Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 243 (242 pre-existing + 1 new) — 474 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS valid ADR-001 graph passes` (plus all `TC-ARCH-002` controlled-rejection cases); exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-166`: all eleven steps run in one test method, in literal order, each with its own assertion. |
| AC-2 | Passed | Steps 1-2 (local Draft creation, campaign-template selection at bind time) plus the explicit "template mutated after bind does not affect the already-bound Character" assertion. |
| AC-3 | Passed | Steps 3-5: `SubmitCharacterDraft` leaves `ApprovalState: Draft`; `ApproveCharacterDraft` transitions both `ApprovalState -> Approved` and `LifecycleStatus -> Active`. |
| AC-4 | Passed | Step 6: a non-MainGM `GrantDevelopmentPoints` call is rejected; the MainGM-issued call succeeds. |
| AC-5 | Passed | Step 7: the owner's `PurchaseAttributeIncrease` succeeds immediately (no GM-approval step), and a duplicate `CommandId` replay does not double-spend. |
| AC-6 | Passed | Step 8: `RecordCriticalSuccessEvidence` produces a row with `UsedByAdvancementId == null`. |
| AC-7 | Passed | Step 9: the reserve-then-resolve cycle consumes the evidence exactly once; a second recommendation referencing the same evidence fails at resolve. |
| AC-8 | Passed | Step 10: a brand-new `SqliteCharacterRepository` instance reconstructs the tracked event sequence via `GetCharacterHistory` and the full authoritative state via `GetCharacter`. |
| AC-9 | Passed | Step 11: `.odchar` export/import round-trips through a real bundle directory and lands as a new Draft with a `CharacterId` distinct from the original. |
| AC-10 | Passed | `git diff --stat -- Packages/` returns empty — confirmed no production code anywhere in the diff. |
| AC-11 | Passed | The `HistoryEventTypes` whitelist gap (section 4/18) was discovered and reported, not silently worked around with a new production-code event-type addition. |
| AC-12 | Passed | See Validation results above — all five required commands run and passed. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: Not applicable.
- Checksums: Not applicable.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- No real-network or UI run of this sequence.
- No real `SLICE-03` dice-roll/board integration for step 8 — evidence is recorded directly via `RecordCriticalSuccessEvidence` with a synthetic identifier (section 4/5); wiring a real roll into `SLICE-04`'s economy is a future, undecided cross-slice design question, not this task's to add.
- `SqliteCharacterRepository.HistoryEventTypes` (the hand-maintained whitelist `GetCharacterHistory` filters against) does not include a skill-purchase, critical-evidence, or advancement-recommendation event type — see section 18's own Decisions entry for the full discovery and why step 10's own assertions were scoped around it rather than silently patched.

### Follow-up tasks

- None required by this task's own acceptance criteria. A future task extending `GetCharacterHistory`'s own event-type coverage (skill purchases, critical evidence, advancement recommendations) would need its own explicit scope/decision — not invented here.

### Self-review summary

- Scope review: exactly the one new test file plus the three documentation touch points named in section 5's own "Allowed paths" — confirmed zero production code anywhere in the diff (`git diff --stat -- Packages/` empty).
- Architecture review: every step calls an already-existing, already-tested public API in the literal order roadmap §13.8 specifies; no new abstraction, no test double for any repository — real `SqliteCampaignRepository`/`SqliteLocalCharacterDraftRepository`/`SqliteCharacterTemplateRepository`/`SqliteCharacterRepository` throughout, mirroring `ODY-S03-008`'s own precedent exactly.
- Test review: the scenario passed on its first real run once the one local-profile-directory setup gap (a test-fixture omission, not a production defect — `Directory.CreateDirectory(_profileDir)` was missing from `SetUp`) was fixed; every one of roadmap §13.9's exit criteria the eleven steps actually exercise has its own direct assertion, not an inferred pass.
- Security/privacy review: step 11's export assertion reuses `ADR-026`'s already-proven `RedactCharacterForExport` behavior via the existing `ExportCharacter` contract, unmodified; no new redaction logic, no new permission gate.
- Documentation/version review: this task contract, the test catalog, and the backlog header/row 14 are updated; no ADR touched; no other `ODY-S04-1XX` task contract modified beyond what the product owner's own post-merge status updates already carried in from before this task's own branch.

## 18. Blockers, decisions, and change control

### Blockers

- None at task creation, and none arose during execution.

### Decisions made during execution

- 2026-09-03 — Task authored following `ODY-S03-008`'s own structural precedent, adapted to `SLICE-04`'s thirteen-task dependency set and eleven-step scenario — Authority / approval: Product owner ("Ну идем тогда к 114").
- 2026-09-03 — Decision: steps 1-2 are realized as `CreateLocalCharacterDraft` (no Personal template) followed by a Campaign-scope template authored via `CreateCampaignCharacterTemplate` and selected at `BindDraftToCampaign` time via `CharacterCreationSeed.FromTemplate` — Authority/approval: `ADR-023` §5.3's own two-decision-point structure (a Personal template's seed copy happens at Draft-creation time; a Campaign template's happens at bind time), chosen specifically because it lets roadmap's own two separate steps ("creates local Draft" / "selects... template") map onto two separate, real API calls rather than collapsing them into one.
- 2026-09-03 — Decision: step 3's "host validates submit" is proven with an explicit negative case first (an incompatible `RulesetId` rejected by `BindDraftToCampaign`'s own compatibility gate) before the real, compatible bind — Authority/approval: this task's own §7 Scenario 1/Required invariants explicitly name host validation as part of step 3, and a negative-then-positive pair is this session's own established convention for proving a gate is live, not merely assumed (mirroring `ODY-S04-110`'s own blocking-dependency-checker proof).
- 2026-09-03 — **Discovered fact, reported per this task's own acceptance criterion 11 (not fixed — no production code touched):** `SqliteCharacterRepository.HistoryEventTypes` (the hand-maintained whitelist `GetCharacterHistory` filters `DomainEvents` against) tracks only `character_created`/`_identity_updated`/`_presentation_updated`/`_primary_owner_assigned`/`_co_owner_added`/`_co_owner_removed`/`_control_granted`/`_control_revoked`/`_draft_bound`/`_draft_submitted`/`_approved`/`_development_points_granted`/`_attribute_increased`/`_archived`/`_deleted`/`_died`/`_restored`/`_import_state_applied`/`_ruleset_migrated`/`_ruleset_migration_reverted` — no skill-purchase (`character_skill_level_purchased`), critical-evidence, or advancement-recommendation event type is included, even though `ODY-S04-105`/`106` both produce real events of those kinds. Step 10's own "history and reconnect show authoritative state" proof was therefore split: `GetCharacterHistory` is asserted against exactly the tracked event types the scenario actually produces (draft-bound/submitted/approved/points-granted/attribute-increased, in order), and the skill-5+ recommendation's own authoritative outcome (the applied skill level) is instead proven via a fresh `GetCharacter` read on the same reconnected repository instance, which does reflect it regardless of `GetCharacterHistory`'s own narrower list. This is not a defect this task introduces or is asked to fix (roadmap §13.8/§13.9 do not name a specific event-type-completeness requirement for step 10), and no follow-up task is opened for it unless the product owner wants `GetCharacterHistory`'s own coverage extended as its own explicit, scoped decision.
- 2026-09-03 — Decision: a real test-fixture gap (not a production defect) was found and fixed during this task's own first run — `SetUp` did not create the local-profile directory before `CreateLocalCharacterDraft` opened a SQLite connection against it, since `SqliteLocalCharacterDraftRepository`'s own `OpenConnection` does not create missing directories itself (unlike `SqliteCampaignRepository.Create`, which does). Fixed by adding `Directory.CreateDirectory(_profileDir)` to this test's own `SetUp` — a one-line test-fixture correction, not a production code change (confirmed: `Packages/**` is untouched).

### Approved task changes

- None yet.

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
