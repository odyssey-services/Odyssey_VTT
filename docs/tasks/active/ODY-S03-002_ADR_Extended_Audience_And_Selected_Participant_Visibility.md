# ODY-S03-002 — ADR: Extended Audience and Selected-Participant Visibility

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-002-adr-extended-audience-and-selected-participant-visibility`
**Pull request:** Draft — [#56](https://github.com/odyssey-services/Odyssey_VTT/pull/56) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Produce `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` — fixing how `SelectedParticipants`/`CampaignUserGroup`-based audience selection integrates with `ADR-019` §7's already-accepted single-authoritative-state-plus-per-connection-filter pipeline for roll visibility, board fog, and game-log disclosure, and how postfactum disclosure/revocation of an already-created artifact composes with `ADR-017`'s existing delta operations — the exact scope `ADR-019` §10 explicitly deferred, not the three baseline roles it already fixed.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-019` §10 explicitly defers `CampaignUserGroup` and arbitrary `PermissionKey`/`Scope` beyond the three baseline roles; no ADR currently fixes group-based or explicit-participant-list audience selection. Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §12.2's fourth prerequisite bullet names exactly this gap ("правила visibility броска, включая selected users/groups").
- Value or risk reduction: without a fixed integration contract, a future implementation task would have to invent how group membership feeds `VisibilityPolicy` and how postfactum disclosure/revocation of a roll or log entry is delivered, risking a parallel redaction mechanism alongside `ADR-017`'s already-proven one instead of reusing it.
- Blocking or enabling relationship: `SLICE-03_BACKLOG.md` §6 — `ODY-S03-002` has no dependency on `ODY-S03-001` (mutually independent; `ODY-S03-001`/`ADR-020` is already `Done`, merged via PR #55). Depends only on already-accepted `ADR-017`/`ADR-019`. This is the second and last of the two ADRs `SLICE-03_BACKLOG.md` fixes as required prerequisites — its acceptance closes this backlog revision.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `07_Permissions_Odyssey_VTT_v0.7.md` §16 (`CampaignUserGroup` full aggregate), §30 (Private events and audiences — six game audiences)
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (roll visibility, four audience kinds, §16.4's evaluation-time rule), §27 (full-text search security invariant), §28 (postfactum audience change), §36.5 (revocation networking contract)
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §16.3 (fog `AudienceKey` — second consumer of this ADR)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §7 (pipeline, extended not reopened), §10 (explicitly deferred scope this ADR closes)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §5 (`Operations[]`, reused not extended)
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (structural/stylistic template, same task wave)
- `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md` §4, `docs/tasks/SLICE-03_BACKLOG.md` §5 (this task's fixed boundary, already set by the prior task — executed, not reopened)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (prerequisites), backlog `ODY-S03-002`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `07_Permissions_Odyssey_VTT_v0.7.md`/`09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`/`08_Scenes_And_Board_Odyssey_VTT_v0.5.md`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-021`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S03-001` (`ADR-020`, PR #55) is merged to `main` by the product owner — confirmed by `git log --oneline -10` before branching; this constitutes the owner-review this task's own §0 context references.
- `ADR-019` §10 states verbatim: "Произвольные `PermissionKey`/`Scope` за пределами трёх baseline-ролей... `CampaignUserGroup` (`07_Permissions` §16) — групповые assignments" — confirmed by direct re-`Read`.
- `07_Permissions_Odyssey_VTT_v0.7.md` §16.1 documents a full `CampaignUserGroup` aggregate (`CampaignUserGroupId`/`CampaignId`/`Name`/`Description?`/`MemberUserIds`/`Status`/`CreatedByUserId`/`CreatedAt`/`UpdatedAt`/`Revision`) with a full lifecycle (§16.4/§16.5: membership change bumps revision and recomputes `ClientProjection`; archive stops applying to new decisions) — confirmed by `Read`.
- `07_Permissions` §30.1 documents six game audiences (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`/`CampaignUserGroup`/`SceneParticipants`); `09_Dice_And_Game_Log` §16.1 documents four for rolls specifically (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`) — confirmed by `Read`; roadmap §12.2's fourth bullet independently names this gap.
- `09_Dice_And_Game_Log` §16.4 states verbatim: "Audience хранит стабильные ссылки на users/groups, а projection вычисляется по текущим permissions и membership" — direct textual basis for the evaluation-time rule this ADR generalizes.
- `09_Dice_And_Game_Log` §28.1/§28.2 document `LogEntryDisclosureChanged` (disclosure, original record not edited in place) and revocation (with explicit "already may have been seen" warning); §36.5 confirms the networking contract does not promise erasure of previously-seen content — confirmed by `Read`.
- `09_Dice_And_Game_Log` §27.2's full-text search security invariant is already phrased as a direct application of the "safe denial never confirms a hidden entity's existence" principle (no leaked count/snippet/timing/EntityId for inaccessible entries) — confirmed by `Read`.
- `ADR-017` §5's `Operations[]` already includes `AddEntity`, `AddJournalEntry`, and `RemoveFromProjection` — confirmed by `Read`, directly usable for disclosure/revocation without introducing a new operation type.
- No ADR file numbered `ADR-021` exists prior to this task — confirmed by `ls docs/adr/`; `ADR-021` is the next available number.

### Assumptions

- None. All facts above were directly observed via `Read`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` (new).
- `docs/tasks/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md` (this file), `docs/plans/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md` (governing ExecPlan).
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row (`In Review` → `Done`, per §0's owner-merge context) and `ODY-S03-002` row status.

### Out of scope

- Any production code (`VisibilityPolicy` extension, `CampaignUserGroup` repository, disclosure/revocation handlers, unit tests) — a separate future implementation task.
- Reopening `ADR-019`'s three baseline roles (MainGM/Player/Observer) — only §10's explicitly-deferred scope is extended.
- Full `CampaignUserGroup` lifecycle-command design (create/rename/archive/membership-change as separately designed commands) — ordinary `ADR-002` commands, not an architecturally new question this ADR must fix.
- Full permission-aware full-text search design (`09_Dice_And_Game_Log` §27.3's searchable fields, indexing/engine choice) — only confirmation that the safe-denial principle extends, per this task's own §3 boundary.
- Any edit to `ADR-002`/`ADR-004`/`ADR-012`/`ADR-017`/`ADR-019`/`ADR-020`, or `ODY-S03-000`/`ODY-S03-001`'s own files — this task only reads them.
- Any technical spike — `SLICE-03_BACKLOG.md` §3 already justifies "no spike required"; this ADR's own rejected-alternatives section (§13.6) confirms this is an additional input dimension on an already `SP-04`-proven mechanism.

### Allowed paths

```text
docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md
docs/tasks/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md
docs/plans/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md
docs/tasks/SLICE-03_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-021` §10 documents that `VisibilityPolicy`-computation remains in the Application layer (`ADR-019` §6.2, not reopened); this task does not itself touch either module.
- Authoritative-state and transaction boundary: not applicable to this task's own execution; `ADR-021` extends only the input parameters of `ADR-019`'s already-existing `VisibilityPolicy` function, not its computation point or the transaction boundary.
- Serialization / compatibility boundary: not applicable — no DTO or codec introduced; `ADR-021` reuses `ADR-017`'s existing `Operations[]` unchanged.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: this task's own deliverable is itself a security-relevant ADR — verified to reuse (not duplicate) `ADR-004`'s `SafeReasonCode` vocabulary and `ADR-017`'s delta operations, and to extend (not reinvent) `PERM-INV-012`'s safe-denial principle, in the Completion evidence section.
- Performance or platform constraint: not applicable.
- Other: `ADR-021` must not silently expand scope beyond `SLICE-03_BACKLOG.md` §5's fixed boundary (e.g., by designing full-text search or `CampaignUserGroup` lifecycle commands) — verified in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — `CampaignUserGroup` scope decision is explicit, not presumed

**Given** `07_Permissions` §16.1's full aggregate versus this task's narrower audience-resolution need
**When** `ADR-021` §4 is written
**Then** it fixes a narrow read-model subset (`CampaignUserGroupId`/`CampaignId`/`MemberUserIds`/`Status`/`Revision`) with explicit reasoning, and explicitly defers lifecycle-command design as ordinary `ADR-002` command work, not silently assumed either way.

### Scenario 2 — audience integrates as an additional `VisibilityPolicy` input, not a parallel mechanism

**Given** `ADR-019` §7's already-accepted pipeline
**When** `ADR-021` §5 is written
**Then** it states `SelectedParticipants`/`CampaignUserGroup` membership as additional parameters to the existing `VisibilityPolicy` function, computed at the same point (`ADR-019` §6.2), not a second independent check.

### Scenario 3 — postfactum disclosure/revocation reuses `ADR-017`'s existing operations

**Given** `ADR-017` §5's already-accepted `AddEntity`/`AddJournalEntry`/`RemoveFromProjection` operations
**When** `ADR-021` §7 is written
**Then** it states disclosure as `AddJournalEntry`/`AddEntity` and revocation as `RemoveFromProjection`, with no new operation type introduced.

### Scenario 4 — safe denial extends to search without a new mechanism

**Given** `09_Dice_And_Game_Log` §27.2's own security invariant, already phrased as an application of `PERM-INV-012`
**When** `ADR-021` §8 is written
**Then** it confirms this directly, without designing the search implementation itself, and without introducing a new `SafeReasonCode`.

### Required invariants

- `ADR-021` does not modify `ADR-002`'s, `ADR-004`'s, `ADR-012`'s, `ADR-017`'s, `ADR-019`'s, or `ADR-020`'s own files.
- `ADR-021` does not reopen `ADR-019`'s three baseline roles or introduce a new `ADR-017` operation type or new `SafeReasonCode` anywhere in its normative text.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-03_BACKLOG.md` (`ODY-S03-001`/`ODY-S03-002` row status).
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` exists, `Status: Accepted`, mirroring `ADR-020`/`ADR-019`'s structural format.
2. `ADR-021` fixes an explicit, justified `CampaignUserGroup` scope decision (narrow read-model, not full lifecycle contract).
3. `ADR-021` fixes `SelectedParticipants`/`CampaignUserGroup` as additional `VisibilityPolicy` inputs atop `ADR-019` §7's pipeline, not a parallel mechanism.
4. `ADR-021` fixes the evaluation-time rule (current, not stored, membership/permissions) as a generalization of `ADR-019` §1 point 8, citing `09_Dice_And_Game_Log` §16.4's direct textual basis.
5. `ADR-021` fixes postfactum disclosure/revocation as direct reuse of `ADR-017`'s `AddJournalEntry`/`AddEntity`/`RemoveFromProjection`, introducing no new operation type.
6. `ADR-021` confirms `PERM-INV-012`/`ADR-019`'s safe-denial principle extends to the new audience model, including full-text search, without designing search itself and without a new `SafeReasonCode`.
7. `ADR-002`, `ADR-004`, `ADR-012`, `ADR-017`, `ADR-019`, and `ADR-020` files are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.
11. `SLICE-03_BACKLOG.md`'s `ODY-S03-001` row shows `Done` (reflecting the product owner's merge of PR #55) and `ODY-S03-002` row reflects this task's own CI-green status, without presuming `Done` before owner review.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Read `ADR-021` end-to-end after writing to confirm the `CampaignUserGroup` scope decision, the `VisibilityPolicy` integration, the evaluation-time rule, the disclosure/revocation composition with `ADR-017`, the safe-denial extension, the explicit exclusions, and the Definition of Done for the future implementation task are all present and substantive.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test` — no production or test code is touched by this task.
- Any empirical test of the audience model or search — future implementation task's scope, proven via the Definition of Done (`ADR-021` §12) this task fixes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-021` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: None directly — this task touches no code, credential, or campaign data. Its own deliverable (`ADR-021`) is itself the normative source for how selected-participant/group-based hidden-information visibility is decided.
- Trust boundaries: Not applicable to this task's own execution; `ADR-021` §5/§7 fix the host-authoritative trust boundary for the extended audience model for future code.
- Authorization / audience checks: `ADR-021` is itself the audience-model-extension ADR — its own content is the deliverable being reviewed for correctness.
- Redaction requirements: `ADR-021` §7 fixes the disclosure/revocation redaction mechanism (reusing `ADR-017`); this task's own execution introduces no redaction-relevant code.
- Log-safe fields: Not applicable to this task's own execution.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable to this task itself; `ADR-021` §12 states what a future implementation task's own security tests must prove.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone (the closest precedents, `ODY-S02-006` and `ODY-S03-001`, both pure-documentation ADR-authoring tasks, independently reached the same conclusion for the same class of reason). This task matches §1.2's "affects... security, permissions, hidden information" trigger directly — it fixes how the hidden-information redaction boundary extends to a new input dimension. It also matches "requires investigation before the implementation path is known": determining whether `CampaignUserGroup` needs a full aggregate or a narrower representation, and how postfactum disclosure/revocation composes with `ADR-017`'s existing operations without inventing a parallel mechanism, required reading three separate product documents (`07_Permissions` §16/§30, `09_Dice_And_Game_Log` §16/§27/§28, `08_Scenes_And_Board` §16.3) and reconciling their independently-named audience vocabularies into one integration principle — genuine design-tradeoff work (`ADR-021` §13, six rejected alternatives), not mechanical transcription.
- ExecPlan path: `docs/plans/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-021`, this task contract, its ExecPlan, and both backlog row updates).
- Milestone or sequencing constraints: no dependency on `ODY-S03-001` (`SLICE-03_BACKLOG.md` §6 — mutually independent; already `Done`). This is the last of the two prerequisite ADRs — its acceptance closes `SLICE-03_BACKLOG.md`'s prerequisite revision; the `SLICE-03` vertical-slice implementation backlog is a separate future revision, not started by this task.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-03_BACKLOG.md` (`ODY-S03-001`/`ODY-S03-002` rows and header status).
- Documents that must not change: `ADR-001`–`020`, `07_Permissions_Odyssey_VTT_v0.7.md`, `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`, `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`, `docs/tasks/active/ODY-S03-000_*`/`ODY-S03-001_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything else under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first concrete extended-audience-integration contract (documentation only, no code-level version bump, since no code changes).
- Documentation version changes: `ADR-021` is a new document (v1.0); no existing ADR changes version.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; no code touched).
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

- `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md` — new.
- `docs/tasks/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md` (this file), `docs/plans/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md` — new.
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row (`Done`) and `ODY-S03-002` row status/header.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| CI — PR #56, commit `f86d37c` | Passed | Run [32969885094](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32969885094): `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`, confirmed via `gh pr view 56 --json state,isDraft,statusCheckRollup`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-021` Status: `Accepted`, structural format mirrors `ADR-020`/`ADR-019`. |
| AC-2 | Passed | `ADR-021` §4 — narrow read-model (`CampaignUserGroupId`/`CampaignId`/`MemberUserIds`/`Status`/`Revision`), lifecycle commands explicitly deferred as ordinary `ADR-002` work. |
| AC-3 | Passed | `ADR-021` §5 — `SelectedParticipants`/`CampaignUserGroup` fixed as additional `VisibilityPolicy(...)` inputs atop `ADR-019` §7's pipeline. |
| AC-4 | Passed | `ADR-021` §6 — evaluation-time rule, citing `09_Dice_And_Game_Log` §16.4 and generalizing `ADR-019` §1 point 8. |
| AC-5 | Passed | `ADR-021` §7 — disclosure = `AddJournalEntry`/`AddEntity`, revocation = `RemoveFromProjection`, no new operation type. |
| AC-6 | Passed | `ADR-021` §8 — `PERM-INV-012` extension confirmed for search, citing `09_Dice_And_Game_Log` §27.2 verbatim; no new `SafeReasonCode`. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-002`/`004`/`012`/`017`/`019`/`020` file touched. |
| AC-8 | Passed | See Validation results table above — both commands pass. |
| AC-9 | Passed | `git status --porcelain` shows only `ADR-021`, this task contract, its ExecPlan, and `SLICE-03_BACKLOG.md`. |
| AC-10 | Passed | Draft PR [#56](https://github.com/odyssey-services/Odyssey_VTT/pull/56) open; all 4 required CI checks `SUCCESS` on run 32969885094 (commit `f86d37c`); PR remains Draft pending explicit owner confirmation before any merge. |
| AC-11 | Passed | `SLICE-03_BACKLOG.md`'s `ODY-S03-001` row now `Done` (owner merged PR #55); `ODY-S03-002` row reflects this task's own status, not presumed `Done`. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (deliberately left to future tasks, not this one): whether `CampaignUserGroup`'s narrow read-model representation remains sufficient once the full lifecycle-command implementation task is undertaken, or whether a broader representation becomes justified — `ADR-021` §4/§15 states this requires amendment or a superseding ADR, not silent expansion.
- Risk: this ADR's integration principle has not yet been empirically exercised by real code — that is exactly the future implementation task's job, proven against this ADR's own Definition of Done (§12); this task's own risk is limited to the contract being wrong in a way that task would then discover, which is the expected and intended division of labor, not a defect of this task.
- This is the last of the two prerequisite ADRs `SLICE-03_BACKLOG.md` fixes — its acceptance closes this backlog revision; the `SLICE-03` vertical-slice implementation backlog is a separate future revision, not started by this task.
