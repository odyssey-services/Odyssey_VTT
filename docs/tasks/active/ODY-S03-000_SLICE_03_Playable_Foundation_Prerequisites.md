# ODY-S03-000 - SLICE-03 Playable Foundation Prerequisites

**Status:** Draft
**Roadmap stage / slice:** SLICE-03
**Owner:** Unassigned
**Requested by:** Product owner
**Branch:** `feat/ody-s03-000-slice-03-playable-foundation-prerequisites`
**Pull request:** Draft — [#54](https://github.com/odyssey-services/Odyssey_VTT/pull/54) (open, awaiting owner review)
**ExecPlan:** Not required
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Determine, and justify from first principles rather than the roadmap's literal wording, exactly how many new ADRs (and whether any technical spike) must close before `SLICE-03` ("Playable Foundation: Board, Dice and Game Log," roadmap section 12) vertical-slice implementation work begins, and record that decision as an ordered prerequisite backlog and parent task contract — no ADR content, no production code, no UI, and not the vertical slice itself.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-02` is closed (8 of 9 roadmap section 11.7 exit criteria confirmed with real evidence, owner-accepted 2026-08-26 per `docs/tasks/active/ODY-S02-015_SLICE_02_Acceptance_And_Closure_Gate.md` section 17; criterion 1 remains `Blocked` pending a separate, later product-owner decision on the `ADR-016` section 14 follow-up spike). The product owner has decided `SLICE-03` proceeds now, and the `ADR-016` section 14 spike / `ODY-S02-014` is deferred until `SLICE-03` plus a basic clickable UI atop it exist — that deferred item is explicitly not part of this task chain. No `SLICE-03` organizational structure exists yet.
- Value or risk reduction: unlike `SLICE-02` (which had to invent an entire networking architecture from nothing), `SLICE-03` sits atop an already-mature, already-`Accepted` architecture (`ADR-002`, `ADR-004`, `ADR-008`, `ADR-012`, `ADR-015`, `ADR-017`, `ADR-019`). Deciding, up front, exactly which parts of Board/Dice/Game-Log scope are genuinely new architectural ground versus already-closed ground protects against two opposite failure modes: silently reopening an already-`Accepted` decision, or silently treating a genuinely open question as already closed.
- Blocking or enabling relationship: Blocks all `SLICE-03` vertical-slice work (roadmap section 12.6, the "Бросок и журнал" scenario). Enables a future implementation backlog revision once the ADR(s) identified by this task are `Accepted`.

## 3. Authorities and requirement references

### Required authorities

- `17_Roadmap_Odyssey_VTT_v0.11.md`, section 12 (Этап 4 — Playable Foundation) in full — 12.1 (goal), 12.2 (prerequisite documents), 12.3-12.5 (Board/Dice/Log scope), 12.6 (the vertical slice, referenced only — not started by this task), 12.7 (exit criteria, referenced only), 12.8 (Milestone M4 statement, referenced only).
- `Documentation/08_Scenes_And_Board_Odyssey_VTT_v0.5.md` — private, non-tracked; read in full (3069 lines). Source for Board aggregate model, movement validation pipeline, LOS/cover, fog-of-war, Undo/Redo, and the two explicit "implementation ADR" flags (sections 13.4, 25.1) this task's ADR-count decision rests on.
- `Documentation/09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` — private, non-tracked; read in full (2009 lines). Source for the dice-roll lifecycle, formula grammar, audience-kind model, `GMOverride`, `ActionLogGroup`, persistence/reconnect-replay, and section 38 (10 non-blocking open technical decisions, none of which require an ADR).
- `Documentation/06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`, section 33 ("Приватные броски и игровой журнал") — private, non-tracked; read in full. Source confirming the game-log reconnect/delta vocabulary already matches `ADR-017`'s established model.
- `Documentation/07_Permissions_Odyssey_VTT_v0.7.md` — private, non-tracked; grepped for `SelectedParticipants`/`CampaignUserGroup` (section 16, lines ~912 and ~1582-1583). Confirms `CampaignUserGroup` is a fully product-documented concept already named as an audience mechanism, and cross-checked against `ADR-019`'s own explicit deferral of it.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` — read to confirm command/event model is reused unchanged for board commands and dice-roll commands, not reopened.
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` — read in full (1497 lines). Section 38 point 4 explicitly states dice/combat implementation uses this ADR "без выбора нового algorithm" — direct textual closure of the RNG-determinism question for dice.
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` — section headers read via grep; confirms a generic, reusable Domain Event Store contract applicable to Game Log persistence without a new ADR.
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` — already read in full in an earlier session; section 1's own scope statement is generic ("application-level протокол доставки и восстановления projection-состояния"), not scene-specific — the key textual evidence that board-state and game-log delivery reuse this ADR's mechanism without a new one.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` — already read in full in an earlier session; section 10 explicitly defers arbitrary `PermissionKey`/`Scope` and `CampaignUserGroup`-based group assignment as out of baseline scope — the key citation for the one genuine remaining gap this task identifies.
- `docs/tasks/SLICE-02_BACKLOG.md` and `docs/tasks/active/ODY-S02-000_SLICE_02_Network_Prototype_Prerequisites.md` — structural/procedural precedent for how ADR count/boundaries were determined by area-of-responsibility split, not the roadmap's literal list.
- `docs/tasks/SLICE-01_BACKLOG.md` — earlier, simpler precedent of the same document type.
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (prerequisites revision only), Milestone `M4` (not closed by this task).
- Existing test IDs: None yet defined for `SLICE-03`.
- New test IDs to introduce: None by this task. Each ADR child task defines its own if needed.

### Task-safe private context

- Approved summary / references: This task contract summarizes section-header structure and specific normative terms (`BOARD-INV-*`, `BT-*`, `DGL-PR-*`, `DGL-T-*`, `DGL-SLICE-*`) from the private, non-tracked `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`/`09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`/`06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`/`07_Permissions_Odyssey_VTT_v0.7.md`, the same level of reference this session's prior tasks (`ODY-S02-000` and others) already used for their own private-document sources. No verbatim content beyond section titles and named invariant/test IDs is copied into this tracked file.

## 4. Verified current state

### Verified facts

- `SLICE-02` is closed with 8 of 9 exit criteria confirmed by real re-run evidence; the product owner explicitly accepted this result on 2026-08-26, per `docs/tasks/active/ODY-S02-015_SLICE_02_Acceptance_And_Closure_Gate.md` section 17 and `docs/tasks/SLICE-02_IMPLEMENTATION_BACKLOG.md`'s own header/section 3.
- No `SLICE-03` task contract, backlog, or ADR exists anywhere in the repository as of this activation (confirmed by `Glob`/`Grep` across `docs/tasks/` and `docs/adr/`).
- Unlike `SLICE-02`'s discovered gap (`Documentation/18_Account_And_Identity.md` did not exist), both roadmap section 12.2 prerequisite product documents already exist locally in complete, substantial form: `Documentation/08_Scenes_And_Board_Odyssey_VTT_v0.5.md` (3069 lines) and `Documentation/09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` (2009 lines), each containing its own acceptance-criteria and test-vector sections (`08_Scenes_And_Board` section 27; `09_Dice_And_Game_Log` sections 40-41). No missing-document gap exists for this task to record.
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` explicitly names two decisions as "implementation ADR" rather than product-document content: section 13.4 ("Exact epsilon is implementation ADR, not user-facing campaign data") and section 25.1 ("Конкретная структура [spatial index] — implementation ADR"). This is direct textual evidence, not an inference, that board geometry/movement determinism is meant to be resolved by a new ADR.
- `ADR-008_Deterministic_Clock_and_RNG_v1.0.md` section 38 point 4 states dice/combat implementation uses this ADR without selecting a new algorithm — direct textual closure of the RNG-determinism question for `SLICE-03` dice rolls.
- `ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` section 1 scopes itself generically to "projection-состояния" delivery, not scene-specific delivery; `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` section 33.4 uses the same `LastReceivedSequence`/delta-or-snapshot/gap vocabulary already established by `ADR-017`, confirming Game Log reconnect/delta reuses the existing mechanism.
- `ADR-019_Permissions_Baseline_v1.0.md` section 10 explicitly defers arbitrary `PermissionKey`/`Scope` and `CampaignUserGroup`-based group assignment as out of its baseline scope. `07_Permissions_Odyssey_VTT_v0.7.md` section 16 (line ~912) defines a full `CampaignUserGroup` aggregate, and lines ~1582-1583 name "SelectedParticipants / CampaignUserGroup" as an audience concept for roll/log/fog visibility. Roadmap section 12.2's own fourth prerequisite bullet independently names "правила visibility броска, включая selected users/groups" as a prerequisite concern — three independent sources converging on the same one genuine gap.
- `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`'s section headers (confirmed via `Grep`) describe a generic Domain Event Store contract (append-only guarantee, `PayloadHash` integrity, `AppliedCommands` idempotency, Snapshot contract) with no scene- or dice-specific content, confirming it is reusable for Game Log persistence without a new ADR.

### Assumptions

- None.

## 5. Scope

### In scope

- Creating this parent task contract (`ODY-S03-000`).
- Creating `docs/tasks/SLICE-03_BACKLOG.md`, listing and sequencing exactly two child ADR tasks (`ODY-S03-001`, `ODY-S03-002`). This parent task organizes and sequences them; it does not author their content.
- Determining and justifying the count and boundary of each prerequisite ADR — two, not derived from any literal roadmap list (roadmap section 12.2 gives no explicit ADR list, unlike section 11.2 for `SLICE-02`) — by evaluating four candidate zones (board state/command model, dice roll model, game log persistence/replay, roll/board/log visibility) against the already-`Accepted` architecture and recording, for each, whether it is (a) a new ADR, (b) an extension of an existing ADR via a separate future task, or (c) already closed by an existing ADR with a concrete section citation.
- Determining and explicitly recording whether a technical spike is needed, analogous to `SP-02`/`SP-03`/`SP-04`, with justification either way — not silently skipped.

### Out of scope

- Any ADR content whatsoever. Each ADR's content is authored in its own separate child task, one at a time, by a separate future ТЗ. This task creates only the parent contract and backlog scaffold.
- Any production code, UI, or Board/Dice/Game-Log implementation.
- The `SLICE-03` vertical slice itself (roadmap section 12.6: player selects own token → sends roll intent → host validates permission → host generates d100 result → modifiers applied → GM can override with reason → only permitted clients receive result → event is persisted → reconnect restores visible log → original event remains after reroll/cancel) — not started by this task.
- Creating or modifying `docs/tasks/active/ODY-S03-001_...md`/`ODY-S03-002_...md`. These child task contract files are not created by this activation.
- Returning to `ADR-016` section 14 / `ODY-S02-014` (Real Transport Integration spike) — explicitly deferred by product-owner decision until `SLICE-03` plus a basic clickable UI exist; not part of this task chain.

### Allowed paths

```text
docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md
docs/tasks/SLICE-03_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/** (no ADR content is created by this task)
Documentation/08_Scenes_And_Board_Odyssey_VTT_v0.5.md (private, non-tracked; read-only source)
Documentation/09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md (private, non-tracked; read-only source)
Documentation/06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md (private, non-tracked; read-only source)
Documentation/07_Permissions_Odyssey_VTT_v0.7.md (private, non-tracked; read-only source)
docs/plans/** (Brief plan mode; no ExecPlan is created)
docs/tasks/active/ODY-S03-001_*.md, ODY-S03-002_*.md (child task contracts; not created by this activation)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable to this scaffold itself; any future Board/Dice/Log module boundary decision belongs to the ADR child tasks.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Not applicable; any wire-format decision belongs to the ADR child tasks, not this scaffold.
- Time / RNG rule: Not applicable to this task directly; this task records (does not decide) that `ADR-008`'s RNG model is reused unchanged for dice rolls.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is introduced or approved by this contract.
- Security / privacy / redaction rule: `Documentation/08_Scenes_And_Board_Odyssey_VTT_v0.5.md`/`09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`/`06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`/`07_Permissions_Odyssey_VTT_v0.7.md` remain private and non-tracked; only section-header structure and named invariant/test IDs are referenced in this tracked file.
- Performance or platform constraint: Not applicable.
- Other: None.

## 7. Expected behavior

### Scenario 1 - Parent task and backlog exist and are internally consistent

**Given** `SLICE-02` is closed and no `SLICE-03` organizational structure exists
**When** this task contract and `docs/tasks/SLICE-03_BACKLOG.md` are created
**Then** the backlog lists exactly two ordered child ADR tasks, each with clear scope boundaries and dependency rules, an explicit "no spike required" statement with justification, and no child task contract file or ADR file exists as a result.

### Required invariants

- No ADR content is authored by this task.
- No implementation code, script, or configuration is introduced.
- The `SLICE-03` vertical-slice implementation backlog is explicitly deferred to a future backlog revision, not created here.
- The `ADR-016` section 14 spike / `ODY-S02-014` is not reopened or scheduled by this task.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract; `docs/tasks/SLICE-03_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md` exists, following `docs/tasks/TASK_TEMPLATE.md` with all 18 numbered sections present.
2. `docs/tasks/SLICE-03_BACKLOG.md` exists, mirrors the structure of `docs/tasks/SLICE-02_BACKLOG.md` (Purpose, Slice exit criteria, Ordered backlog table, Task boundaries, Dependency rules, Global non-goals, Backlog change control), and lists exactly 2 ordered child tasks with IDs `ODY-S03-001` and `ODY-S03-002`.
3. Each of roadmap section 12.2's prerequisite-document concerns is explicitly mapped, in this contract or the backlog, to a decision of type (a) new ADR, (b) extension of an existing ADR, or (c) already closed by an existing ADR with a concrete section citation.
4. The backlog explicitly records "no technical spike required" with justification, not a silent omission.
5. No child task contract file (`ODY-S03-001...md`, `ODY-S03-002...md`) exists as a result of this task.
6. No ADR file exists as a result of this task.
7. `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass unchanged; this task introduces no new required-path expectations into either script.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only organizational task; no new test IDs are introduced.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Owner review of the parent task contract and backlog scope/ordering before any `ODY-S03-00X` child task is activated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (PowerShell validation only; no Unity or .NET build is required since no production/test/script/config/workflow file is touched).
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test`, Unity compile/EditMode/PlayMode, `verify-ci.ps1`, `verify-unity-project.ps1`, `verify-repository.ps1`, `verify-test-structure.ps1`, `verify-build-identity.ps1`, `test-serialization-aot.ps1`, `test-unity.ps1`, `build-dev.ps1`, `test-player-smoke.ps1`: none of these are affected because no production code, test code, script, Unity asset, package, or CI workflow file is touched by this task.

## 11. Compatibility, migration, and rollback

Not applicable. This task introduces no persisted state, public contract, protocol, package, Unity version, or build identity change.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | - | - | - | - |

No new dependency, GitHub Action, Unity package, executable, or download is approved by this contract.

## 13. Security, privacy, and hidden information

- Data classes handled: Section-header structure and named invariant/test IDs (`BOARD-INV-*`, `BT-*`, `DGL-PR-*`, `DGL-T-*`) from private, already-approved product documents; no secrets, personal data, or hidden campaign content.
- Trust boundaries: Not applicable beyond the redaction rule below.
- Authorization / audience checks: Not applicable to this scaffold itself — this is exactly the subject matter the future `ODY-S03-002` ADR decides.
- Redaction requirements: No secrets, personal data, local paths, or hidden campaign content may be introduced; only section titles and invariant/test IDs from the private source documents enter this tracked file.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None; concrete visibility/audience mechanism decisions and their tests are deferred to the `ODY-S03-002` ADR child task.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` section 1.1's conditions individually, following the same discipline `ODY-S02-000` used rather than assuming by analogy alone. (1) Contained in one area — a parent task contract plus a backlog scaffold, no production module touched. (2) Does not change a public contract, persisted format, protocol, permissions model, dependency graph, package version, or build pipeline — this task decides no technical question; it only organizes and sequences future decisions. (3) One clear implementation path — read the roadmap/product-document scope, determine and justify the ADR/spike count and ordering, write the two files. (4) Fits one focused pull request. (5) No migration or recovery procedure required. `PLANS.md` section 1.2's ExecPlan triggers do not apply: no port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed by this task itself — every one of those will be decided by a future child task, each of which will make its own Brief-plan-vs-ExecPlan decision independently.
- ExecPlan path: Not required
- Expected pull request count: 1 (this scaffold). Each subsequent ADR child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: Do not create any `ODY-S03-00X` child task contract until this parent task and backlog are reviewed. Do not begin ADR content authoring under this task. Do not begin `ODY-S02-014`/`ADR-016` section 14 spike work under this task chain.

## 15. Documentation and versioning impact

- Documents that must change: This task contract; `docs/tasks/SLICE-03_BACKLOG.md`.
- Documents that must not change: All ADRs, Technical Development Baseline, Active Documentation Baseline, product requirement documents, ExecPlans, and the four private `Documentation/` sources cited (read-only for this task).
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
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

Fill this section with real results before moving the task to `Done`.

### Changed files / areas

- This task contract and `docs/tasks/SLICE-03_BACKLOG.md` were created from repository authorities (roadmap section 12, `08_Scenes_And_Board`, `09_Dice_And_Game_Log`, `06_Networking_and_Session_Sync` section 33, `07_Permissions` section 16, `ADR-002`/`008`/`012`/`017`/`019`, and the `SLICE-02` structural precedent).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| CI — PR #54, commit `8c79eec` | Passed | Run [32960754649](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32960754649): `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`, confirmed via `gh pr view 54 --json state,isDraft,statusCheckRollup`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-7 | Passed | Both files created per template/backlog structure; no child task or ADR file created; `git status --porcelain` confirms diff scope is exactly these two files; validation commands pass. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- This scaffold does not decide any technical question. Both ADRs remain to be authored in separate future child tasks.

### Follow-up tasks

- `ODY-S03-001`, `ODY-S03-002`, to be created one at a time by separate future task activations, per `docs/tasks/SLICE-03_BACKLOG.md`.

### Self-review summary

- Scope review: Contract stays within organizational scaffold boundary; no ADR content, no implementation code, no vertical-slice work introduced.
- Architecture review: No architecture, ADR, or module-boundary change is introduced; `ADR-002`/`008`/`012`/`017`/`019`'s existing decisions are only cited, not altered.
- Test review: No new TestCase IDs are introduced.
- Security/privacy review: Only section-header structure and named invariant/test IDs from the private source documents enter this tracked file; no other private content.
- Documentation/version review: No baseline, ADR, TDB, schema, protocol, ruleset, package, or application version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- None at contract-creation. This contract requires owner review before any `ODY-S03-00X` child task is activated.

### Decisions made during execution

- 2026-08-26 - Create the `ODY-S03-000` parent task contract and `docs/tasks/SLICE-03_BACKLOG.md` as an organizational scaffold only, mirroring the `ODY-S02-000`/`SLICE-02_BACKLOG.md` pattern, following explicit product owner request after `SLICE-02` closure and the decision to defer the `ADR-016` section 14 spike - Authority / approval: product owner instruction.
- 2026-08-26 - Decided on 2 new ADRs (Board Geometry and Movement Determinism; Extended Audience and Selected-Participant Visibility) rather than deriving a count from any literal roadmap list, because roadmap section 12.2 gives no explicit ADR list (unlike section 11.2 for `SLICE-02`) - Authority: roadmap section 12 read in full; `08_Scenes_And_Board` sections 13.4/25.1's explicit "implementation ADR" flags; `ADR-019` section 10's explicit deferral of `CampaignUserGroup`; roadmap section 12.2's own fourth bullet naming "selected users/groups."
- 2026-08-26 - Decided no technical spike is required for this prerequisite revision, unlike `SLICE-01`'s `SP-02` and `SLICE-02`'s `SP-03`/`SP-04` - Authority: none of the remaining open questions require empirical measurement against an uncontrollable environment; board geometry is deterministic math provable by golden-vector tests (the same evidence class `ADR-008` already uses); the extended-audience model reuses an already-proven `ADR-017`/`ADR-019` mechanism (empirically validated by `SP-04`/`ODY-S02-007` and re-validated by `ODY-S02-010`-`013`'s own tests) rather than proving a brand-new mechanism for the first time.

### Approved task changes

- None yet.
