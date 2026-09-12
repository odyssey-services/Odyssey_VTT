# ODY-S05-501 — ADR Addendum: ActiveEffect Aggregate Specification

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects — ADR foundation)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-501-activeeffect-adr`
**Pull request:** [odyssey-services/Odyssey_VTT#135](https://github.com/odyssey-services/Odyssey_VTT/pull/135) (Draft)
**Plan:** `docs/plans/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md` (Brief plan)
**Created:** 2026-09-12
**Last updated:** 2026-09-12 UTC

## 1. Goal

Produce a new ADR document specifying the `ActiveEffect` aggregate itself — the exact gap `ODY-S05-110` identified in `ADR-027` §8 (which specifies only how `ActiveEffect` integrates with items, never the aggregate's own field shape, persistence, stacking-rule integration, duration-expiry mechanism, removal command, or authorization model). This task produces **no production or test code** — its only deliverable is the ADR document, plus the required backlog status update. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1 already fixes this task's own boundary, quoted verbatim: "`ODY-S05-501` owns the `ActiveEffect` aggregate specification only (ADR document, no code). It must not implement any runtime code, and it must not decompose the remaining implementation tasks of this block — that is a future backlog revision once this ADR is accepted."

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-110` decomposed the item-sourced abilities/effects block into exactly one task (this one) precisely because no accepted ADR specifies the `ActiveEffect` aggregate — confirmed by repository-wide search (no `ActiveEffect` type exists in code) and ADR-wide search (no ADR besides `ADR-027` mentions `ActiveEffect` at all, and `ADR-027` itself only ever describes integration, never the aggregate).
- Value or risk reduction: without this ADR, a future implementation task would have to silently decide effect-stacking semantics, duration-expiry mechanics, removal authorization, and permissions inline — exactly the anti-pattern `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4 forbids ("any child task discovering a genuine gap must stop and request a dedicated ADR task, not decide it inline").
- Blocking or enabling relationship: follows `ODY-S05-110` (merged, PR #134); once this ADR is accepted, it enables a future backlog revision to decompose `ODY-S05-502` onward into concrete implementation tasks.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14/§14.1 (this task's own governing scope, written by `ODY-S05-110`).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, full re-read, in particular §7 (`EquippedEntry` — the structural form precedent this ADR's own aggregate specification follows), §8.1/§8.2 (the rules this ADR extends, not reopens), §11 (`ActiveEffect` never mass-migrates — reused as-is), §12 (permissions baseline — the precedent this ADR's own permission decision must explicitly follow or diverge from), §14 (module boundaries — already assigns "ActiveEffect creation orchestration" to `Odyssey.Application`).
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §5.1/§5.2, full re-read — the two existing idiomatic permission-phrasing patterns this ADR must explicitly choose between (not invent a third), and the fail-closed precedent (§5.2) this ADR's own expiry-check rule reuses.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md`, searched directly for `"CharacterAbility"` to verify the citation-inaccuracy claim before repeating or correcting any reference to it.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` §5 (the single-transaction commit-pipeline precedent this ADR's own atomicity decision reuses).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` — read directly to confirm its own doc comment's exact wording and which repositories already use it, not paraphrased from memory.
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs` and `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` — read directly (already fully quoted in `ODY-S05-110`'s own task contract) for `SourceKind`, `EffectDurationType` (15 values), `EffectStackPolicy` (7 values), and `EffectDefinition`'s exact shape.
- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`'s `InventoryItemRef` — read directly as the structural precedent for this ADR's own extensible `SourceRef`/`TargetRef` kind-tagged union design.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-501`, `SLICE-05`; `ADR-027` §8.
- Existing test IDs: None directly changed by this ADR-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `581c980` (merge of PR #134, `ODY-S05-110`) — the item-sourced abilities/effects block's own decomposition is closed, and `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1 (`ODY-S05-501`) is `Proposed`.
- Next free ADR number confirmed by listing `docs/adr/`: the highest existing file is `ADR-027_...md`; no `ADR-028` exists yet.
- `ADR-027` §7 (`EquippedEntry`) read directly and used as this ADR's own structural form precedent: a fenced minimum-record block plus numbered plain-language rules, not narrative prose.
- `ADR-027` §8.1/§8.2 read directly and quoted verbatim in the new ADR's own section 4, exactly matching the text this ТЗ itself quoted — confirmed, not assumed.
- `ADR-025` §5.1 ("`Character.Archive`... is checked normally under `ADR-019`'s existing role/permission model; this ADR does not restrict it beyond what the permission itself already implies") and §5.2 ("`DeleteCharacterPermanently` is available only to MainGM... regardless of what a client-side... preview showed the user beforehand") read directly, confirmed exact wording matches this ТЗ's own citation.
- `docs/adr/ADR-024_Development_Economy_And_Progression_Transactions_v1.0.md` searched directly for `"CharacterAbility"`: zero matches, confirmed by `grep -c`. `ADR-027` §14/§19 both claim `ADR-024` "governs... `CharacterAbility` acquisition/source patterns" — a citation inaccuracy already present in already-accepted `ADR-027`, not introduced by this task. This new ADR does not repeat it: every citation of `ADR-024` in the new document is scoped to what `ADR-024` actually governs (development economy/progression transactions).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs` read directly: its own doc comment states verbatim "the ADR-012 section 5 single-transaction journal-projection commit pipeline. Every mutating repository method (SqliteCampaignRepository, SqliteSceneRepository) routes its projection write through `Execute<T>`..."; `SqliteCharacterRepository`/`SqliteGameLogRepository` also route through it (confirmed by grep for `_pipeline.Execute` call sites in each file during the earlier `ODY-S05-403` session, re-confirmed relevant here), and `SqliteInventoryRepository`'s own migration-apply path (`ODY-S05-403`) is its most recent adopter — five repositories total, not a one-off mechanism.
- Repository-wide search for `class SceneObject`/`struct SceneObject` across every tracked `.cs` file: zero matches, confirming `ADR-027` §8.2's own "scene object" target kind has no backing domain type yet — informs this ADR's own `TargetRef.SceneObject` "reserved but unconstructable" design decision.
- Repository-wide search for any turn/round/game-clock tracking type (`class.*Turn`, `RoundNumber`, `CombatRound`, etc.): zero matches, confirming the ТЗ's own claim that the six turn/round-based `EffectDurationType` values have no existing infrastructure to build on — informs this ADR's own Block 3 boundary (section 13 of the new ADR).
- `TypedDefinitions.cs`'s `EffectDurationType` (15 values) and `EffectStackPolicy` (7 values) enums read directly and enumerated exactly in the new ADR's own sections 7/8 — every value addressed explicitly, none left to omission.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read`/repository-wide search against `origin/main` during this task.

## 5. Scope

### In scope

- Add the new ADR document `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md` (a new standalone ADR was chosen over an `ADR-027` addendum section — see §18 decision log for the justification).
- Add this task contract.
- Add a Brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: change §14 row 1 (`ODY-S05-501`)'s status from `Proposed` to `In Review (PR #NNN)` only — no other edit to the backlog file.

### Out of scope

- Any C# production or test code.
- Decomposing `ODY-S05-502` onward into concrete task IDs — explicitly forbidden by this task's own governing backlog boundary (§14.1) and by this ТЗ's own §7.
- Amending `docs/adr/ADR-027_...md` itself, including correcting its own `ADR-024`/`CharacterAbility` citation inaccuracy — a separate, explicit future decision, not silently bundled into this task.
- Any other edit to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` beyond the one status-cell change — §8/§9/§10/§11/§12/§13 are already finalized and stay untouched.
- Unity/UI.

### Allowed paths

```text
docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md
docs/plans/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001_* through docs/adr/ADR-027_* (any accepted ADR other than the new ADR-028 this task adds)
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-110_* (already Done/In Review/completed, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; the new ADR's own section 15 fixes future module ownership (`Odyssey.Domain`/`Rules`/`Application`/`Persistence`) consistent with `ADR-001` and `ADR-027` §14's own existing assignments.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here; the new ADR's own section 12 fixes that a future implementation must reuse the shared `SqliteSavingPipeline` (`ADR-012` §5), not invent a new transaction mechanism.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here; the new ADR's own section 3.2/5.1 fixes `EffectMechanicsSnapshot`'s conceptual shape for a future task to implement per `ADR-003`.
- Time / RNG rule: Not applicable to this task; the new ADR's own section 8 explicitly defers all game-clock/turn/round mechanism design to a future implementation task (non-combat wall-clock case) or the full attack pipeline block (combat turn/round cases).
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section (`ADR-001` through `ADR-027`) may be edited by this task under any circumstance — this task only adds a new ADR file and references the existing ones.

## 7. Expected behavior

### Scenario 1 — every required decision is closed explicitly, not left open by omission

**Given** `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14's own primary-result column names 8 required decisions (aggregate shape/persistence, stacking-policy integration, duration-expiry mechanism, removal command, permissions, fail-closed behavior, Block 3 boundary, atomicity)
**When** this task writes the new ADR
**Then** every one of the 8 has its own explicit, numbered decision — none is answered only by silent omission or left as an unresolved "TBD."

### Scenario 2 — the aggregate form follows the `EquippedEntry` precedent

**Given** `ADR-027` §7's own `EquippedEntry` minimum-record-plus-numbered-rules form
**When** this task specifies the `ActiveEffect` aggregate
**Then** the new ADR's own section 5 uses the identical structural form (a fenced minimum-record block, then numbered plain-language rules), not a different narrative style.

### Scenario 3 — every `EffectStackPolicy`/`EffectDurationType` value is addressed by name

**Given** `EffectStackPolicy` has exactly 7 values and `EffectDurationType` has exactly 15
**When** this task specifies stacking and duration rules
**Then** the new ADR names every one of the 22 values individually and states its concrete behavior (or, for the 6 turn/round-based duration values, its explicit deferral to the full attack pipeline block) — none is grouped away or skipped.

### Scenario 4 — the permission decision is explicit and justified against both existing patterns

**Given** this project already has two idiomatic permission-phrasing patterns (`ADR-025` §5.1's "inherit existing model" and §5.2's "new MainGM-only restriction")
**When** this task decides `ActiveEffect` creation/removal permissions
**Then** the new ADR explicitly names both patterns, picks one (or a justified mix, as here) per operation, and states why — never leaving the choice implicit.

### Scenario 5 — the `ADR-024` citation inaccuracy is not repeated

**Given** `ADR-027` already incorrectly attributes `CharacterAbility` semantics to `ADR-024`
**When** this task's new ADR cites `ADR-024`
**Then** every citation is scoped to what `ADR-024` actually governs (development economy), and the new ADR explicitly notes the existing inaccuracy without silently repeating or amplifying it.

### Scenario 6 — the Block 3 boundary is explicit text, not an implied gap

**Given** damage-over-time and other combat-sourced effects plausibly overlap with the generic `ActiveEffect` mechanism
**When** this task specifies the aggregate's scope
**Then** the new ADR states in its own dedicated section which `EffectDurationType`/behavior cases belong to the full attack pipeline block, by name, not merely by absence of a decision.

### Required invariants

- No product code, schema, runtime test implementation, or any other accepted ADR (`ADR-001`–`ADR-027`) is changed.
- The new ADR does not reopen `ADR-027` §7, §8.1, §8.2, §10, §11, §12 (baseline), or §13 — it only extends §8.1/§8.2 and cross-references the others.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`'s diff is confined to the one `501` status-cell change.
- `502` onward is not decomposed into any task ID with a real title/scope/dependency anywhere in this task's diff.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, this task contract, its Brief plan, the one-cell backlog status update.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. A new ADR document exists (`docs/adr/ADR-028_...md`) and resolves all 8 required decisions from `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14's own primary-result column explicitly, not by default/omission.
2. The aggregate's form is given in `ADR-027` §7's own precedent style (minimum record + numbered rules).
3. The boundary with the full attack pipeline block is named explicitly, including which `EffectDurationType`/behavior cases belong to it.
4. The permission decision is explicit, cites and chooses between both existing idiomatic patterns (`ADR-025` §5.1/§5.2), and is justified per operation, not applied uniformly without reasoning.
5. The `ADR-024`/`CharacterAbility` citation inaccuracy already present in `ADR-027` is not repeated in the new document.
6. `502` onward is not decomposed in the backlog by this task; the backlog diff is confined to `501`'s own status cell.
7. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1 (`501`) reads `In Review (PR #NNN)`.
8. No product/test code exists anywhere in this task's diff.
9. No file under `docs/adr/**` other than the new `ADR-028` file is changed.
10. This task contract and Brief plan are added.
11. `git diff --name-status` against `main` shows only §5's allowed paths.
12. PR is Draft; merge is the product owner's decision.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only ADR-specification task and does not add runtime behavior.

### Required commands

```powershell
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the allowed documentation/planning files (plus the one new ADR file) changed.
- Confirm `ADR-027` §7/§8.1/§8.2/§11/§12/§14 and `ADR-025` §5.1/§5.2 were read in full and quoted/paraphrased accurately.
- Confirm the `ADR-024`/`CharacterAbility` citation-inaccuracy claim was verified directly (`grep -c "CharacterAbility"` against `ADR-024`'s own text), not assumed from the ТЗ.
- Confirm every one of `EffectStackPolicy`'s 7 values and `EffectDurationType`'s 15 values is individually addressed in the new ADR (a manual line-count/name-match check against `TypedDefinitions.cs`'s own enum declarations).

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`: not required on its own merits (no code changes), but `dotnet test` is still run as an extra confirmation the docs-only diff leaves the whole suite green, matching `ODY-S05-109`/`110`'s own practice.
- Unity validation: not required — no Unity files change.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this documentation-only branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository architecture documentation only.
- Trust boundaries: no runtime trust boundary is changed by this task; the new ADR's own section 10 fixes a future trust-boundary decision (permissions) for implementation tasks to enforce.
- Authorization / audience checks: not implemented here; the new ADR's own section 10 is the specification a future implementation task must follow.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation files (one new ADR, task contract, plan, one backlog status cell); it introduces no code, no public runtime contract, no persistence schema change, and — while it does add a new ADR document — that document's own approval is the task's entire deliverable, not a multi-milestone implementation path requiring an ExecPlan's own tracking machinery. Matches `ODY-S05-107`/`108`/`109`/`110`'s own Brief-plan reasoning, adapted for an ADR-writing (not backlog-decomposition) task.
- Plan path: `docs/plans/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #134; a future backlog revision (not this task) decomposes `ODY-S05-502` onward once this ADR is accepted.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md` (new), `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (one status cell), this task contract, this task's Brief plan.
- Documents that must not change: `docs/adr/ADR-001_*` through `ADR-027_*`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`110` task contracts' own content, `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8/§9/§10/§11/§12/§13.
- Application version change: No — docs only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: the new ADR is versioned `v1.0`, matching this repository's own ADR file-naming convention.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are correctly marked not applicable.
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

- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md` — new ADR document.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — §14 row 1 (`501`) status cell changed from `Proposed` to `In Review (PR #NNN)`.
- `docs/tasks/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md` — this task contract.
- `docs/plans/active/ODY-S05-501_ActiveEffect_Aggregate_Specification.md` — this task's Brief plan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors (harness projects included). |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 534/534 — identical count to before this docs-only diff. |
| `.\scripts\verify-format.ps1` | PASS | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | PASS | `Repository policy check passed.` |
| `.\scripts\verify-test-structure.ps1` | PASS | Exit code 0. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Met | `ADR-028` §5-13 resolve all 8 required decisions explicitly, each in its own numbered section. |
| AC-2 | Met | `ADR-028` §5.1 uses the identical fenced-record + numbered-rules form as `ADR-027` §7's `EquippedEntry`. |
| AC-3 | Met | `ADR-028` §13 names the Block 3 boundary explicitly, including which `EffectDurationType` values it reserves. |
| AC-4 | Met | `ADR-028` §10 cites both `ADR-025` §5.1/§5.2 patterns by name and justifies choosing between them per operation. |
| AC-5 | Met | `ADR-028` §4 explicitly notes, without repeating, `ADR-027`'s own `ADR-024`/`CharacterAbility` citation inaccuracy; verified by direct `grep -c` search returning 0. |
| AC-6 | Met | `git diff` confirms `SLICE-05_IMPLEMENTATION_BACKLOG.md`'s only change is the `501` status cell; no `502`+ row added. |
| AC-7 | Met | Backlog §14 row 1 now reads `In Review (PR #135)`. |
| AC-8 | Met | No `.cs`/`.json`/test file in the diff. |
| AC-9 | Met | Only one new file under `docs/adr/**` (`ADR-028`); no existing ADR edited. |
| AC-10 | Met | This contract and Brief plan exist. |
| AC-11 | Met | `git diff --name-status` limited to §5's allowed paths. |
| AC-12 | Met | PR #135 opened as Draft; product owner review pending. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This ADR does not itself implement the `ActiveEffect` aggregate — a future backlog revision decomposes `ODY-S05-502` onward for that.
- The six turn/round-based `EffectDurationType` values and combat-roll-triggered effect application remain unimplementable until the full attack pipeline block supplies turn/round infrastructure — explicitly deferred, not silently unresolved.
- `ADR-027`'s own `ADR-024`/`CharacterAbility` citation inaccuracy is documented but not corrected — that is a separate, explicit future decision.

### Follow-up tasks

- A future backlog revision decomposing `ODY-S05-502` onward into concrete `ActiveEffect` implementation tasks, once this ADR is accepted.

### Self-review summary

- Scope review: diff limited to the new ADR file, this task contract, this Brief plan, and the one `501` backlog status cell. No `Packages/**`, `DotNet/**`, `Tests/**`, or other `docs/adr/**` file touched.
- Architecture review: the new ADR extends `ADR-027` §8.1/§8.2 without reopening them or any other accepted ADR; every required decision from `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 is closed explicitly.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts run and recorded.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: new ADR versioned `v1.0`; backlog additive one-cell edit only; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-12 — **Decision: a new standalone `ADR-028`, not an `ADR-027` addendum section.** The specified material (aggregate shape, persistence contract, 7-value stacking integration, 15-value duration integration, removal, permissions, fail-closed behavior, atomicity, Block 3 boundary) is comparable in volume and independent decision-density to `ADR-027` §7 (Equipment model) on its own, not a narrow one-paragraph clarification — this ТЗ's own §1 explicitly offered this as the criterion for choosing a separate document over an addendum. Authority: this ТЗ §1's own explicit decision criterion.
- 2026-09-12 — **Decision: `ActiveEffect` gets its own `IActiveEffectRepository`, not an extension of `IInventoryRepository`.** `ADR-027` §8.2 rule 2 already states `ActiveEffect` is not Inventory-owned or Character-owned; an effect can be sourced by a direct GM action or an "Action" source kind with no item involved at all, so coupling its persistence to Inventory's own repository would misrepresent ownership. Authority: `ADR-027` §8.2 rule 2's own explicit text; direct analogy to how `IInventoryRepository` itself is already a standalone contract distinct from `ICharacterRepository`.
- 2026-09-12 — **Decision: two different permission rules for two different `ActiveEffect` operations, not one uniform rule.** Item-triggered creation inherits the existing item-use permission model (`ADR-025` §5.1's "inherit, do not add a new restriction" pattern) because it is routine, frequent, player-triggered gameplay already governed by an existing permission pathway; direct (non-item) creation and explicit early removal are both MainGM-only (`ADR-025` §5.2's "new stricter restriction" pattern) because they are new capabilities/override actions with no existing permission precedent. Authority: this ТЗ §4 item 5's own explicit citation of both patterns and instruction to choose (not invent a third); `ADR-027` §12 rule 4's own existing item-use permission text.
- 2026-09-12 — **Decision: `EffectStackPolicy.ReplaceIfStronger`'s "stronger" comparison is delegated to `Odyssey.Rules` over the opaque `EffectMechanicsSnapshot` payload, not decided by a new generic host-side potency field.** No generic cross-Ruleset potency scale exists anywhere in the already-accepted content model; inventing one would silently decide Ruleset-specific game-design semantics outside this ADR's authority. Authority: `ADR-027` §14's own assignment of deterministic calculation to `Odyssey.Rules`; direct search confirming no existing potency-comparison mechanism.
- 2026-09-12 — **Decision: `EffectStackPolicy.RequestGMResolution` introduces one new lightweight `ActiveEffectStackConflict` pending record + `ResolveActiveEffectStackConflict` command, not a full new sub-system.** This ТЗ §4 item 2 explicitly demands a concrete answer to "who and how requests GM permission," not a vague deferral; the pending-record shape mirrors `ADR-027` §10's own "preview lists a pending decision, does not silently guess" discipline at a much smaller scale. Authority: this ТЗ §4 item 2's own explicit demand for concreteness.
- 2026-09-12 — **Decision: 6 of 15 `EffectDurationType` values (`ForRounds`/`ForTurns`/`UntilSourceTurnStart`/`UntilSourceTurnEnd`/`UntilTargetTurnStart`/`UntilTargetTurnEnd`) are explicitly reserved for the full attack pipeline block, with their meaning fixed here but their mechanism deferred — not silently grouped away.** Direct repository search confirmed no turn/round-tracking infrastructure exists anywhere today; specifying a mechanism for these six values would require inventing combat-turn-structure architecture this ADR has no mandate to design. Authority: this ТЗ §4 item 3's own explicit instruction to group by mechanism and fix the responsibility boundary rather than leave an implicit gap; direct search confirming no existing infrastructure.
- 2026-09-12 — **Decision: `Instant`-duration effects create no persisted `ActiveEffect` row at all.** An instant effect has no ongoing existence to track once its mechanical result resolves; persisting a row only to immediately mark it `Expired` would add bookkeeping with no behavioral value and was not required by any cited authority. Authority: direct reasoning from `ADR-027` §8.2's own "creates an ActiveEffect aggregate" text, read in the context of what "ongoing" state an `Instant` effect could possibly have (none) — a gap-filling decision this ADR is explicitly tasked with making.
- 2026-09-12 — **Decision: fail-closed on inconclusive expiry/removal checks, reusing `ADR-025` §5.2's own fail-closed precedent, applied in the opposite direction (favor keeping the effect Active, not favor blocking a delete).** Authority: this ТЗ §4 item 6's own explicit citation of the `DeleteCharacterPermanently` precedent and instruction to specify the analogous behavior for expiry.
- 2026-09-12 — **Decision: `SqliteActiveEffectRepository` must reuse the shared `SqliteSavingPipeline`, not a new transaction mechanism.** Confirmed by direct code read that this pipeline is already used by 5 repositories (including `SqliteInventoryRepository`'s own migration-apply path from `ODY-S05-403`), making it the established convention, not a one-off choice. Authority: this ТЗ §4 item 8's own explicit citation of `SqliteSavingPipeline`; direct code read confirming its current adoption count.
- 2026-09-12 — **Decision: do not repeat, and do not correct, `ADR-027`'s own `ADR-024`/`CharacterAbility` citation inaccuracy.** Verified directly (`grep -c "CharacterAbility"` against `ADR-024`'s own text returns 0) that the inaccuracy is real, not assumed from the ТЗ. Correcting `ADR-027` itself is out of this task's own scope (amending an already-accepted ADR other than adding this new one); the new ADR instead cites `ADR-024` only for what it actually governs. Authority: this ТЗ §5's own explicit instruction; direct verification.
- 2026-09-12 — Decision: update only the `501` status cell in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14, touching no other line — per this ТЗ §6's own explicit restriction ("только перевод строки `501` в `In Review`, никаких других правок").
- 2026-09-12 — Decision: do not decompose `ODY-S05-502` onward under this task's own authority, even though the new ADR now makes such a decomposition possible — that is explicitly a future backlog revision's job per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1 (already fixed by `ODY-S05-110`) and this ТЗ §7's own explicit prohibition.

### Approved task changes

- None.
