# ODY-S03-001 — ADR: Board Geometry and Movement Determinism

**Status:** In Review
**Roadmap stage / slice:** SLICE-03 (prerequisites)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s03-001-adr-board-geometry-and-movement-determinism`
**Pull request:** Draft — [#55](https://github.com/odyssey-services/Odyssey_VTT/pull/55) (open, awaiting owner review)
**ExecPlan:** `docs/plans/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md`
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## 1. Goal

Produce `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` — fixing cross-platform deterministic geometry arithmetic, exact Square/Hex/None distance formulas, a versioned epsilon convention for LOS/cover/wall intersection, and a spatial-index approach for occupancy/obstacle/visibility queries at MVP scale — the two questions `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §13.4 and §25.1 explicitly name as "implementation ADR," not part of the product document itself.

## 2. Why this task exists

- Problem or dependency being addressed: `08_Scenes_And_Board` §13.4 and §25.1 explicitly defer epsilon convention and spatial-index structure to a separate implementation ADR; no ADR currently fixes either. `ADR-002`/`ADR-017` already fix the command/event model and delta-delivery mechanics that board commands (e.g., `board.token.move v1`) use, but neither fixes the geometry math those commands validate against.
- Value or risk reduction: without a fixed, versioned geometry contract, a future implementation task would have to invent epsilon values, distance formulas, and an index structure ad hoc, risking silent divergence between Unity and pure-.NET compilation targets and making `08_Scenes_And_Board` BT-079's "restart restore identically" requirement unprovable by a stable golden-vector test.
- Blocking or enabling relationship: `SLICE-03_BACKLOG.md` §6 — `ODY-S03-001` has no dependency on `ODY-S03-002` (mutually independent). Blocks the future `SLICE-03` vertical-slice implementation backlog's board/movement work, which needs this contract as its geometric foundation.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` (full document — §6.1, §7.7, §13.4, §21.6/BT-079, §25.1, §25.4, §4.4)
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (`board.token.move v1` example — command model not reopened)
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (structural/rigor precedent for cross-platform determinism and versioned-constant style, reused here)
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 (board-delta delivery — not reopened)
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (structural format template — most recently accepted ADR)
- `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md` §4 (Verified facts this ADR answers)
- `docs/tasks/SLICE-03_BACKLOG.md` §5, §6 (this task's fixed boundary and dependency rules)

### Requirement and test IDs

- Requirement IDs: `SLICE-03` (prerequisites), backlog `ODY-S03-001`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure ADR-authoring task, no production code).

### Task-safe private context

- Approved summary / references: `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`'s content is summarized/quoted (short customary phrases and direct quotes clearly attributed) into this task and `ADR-020`. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `ODY-S03-000` (PR #54) is merged to `main`; `SLICE-03_BACKLOG.md` lists `ODY-S03-001`/`ODY-S03-002` as the two required prerequisite ADRs, mutually independent — confirmed by `git log --oneline -10` and reading `SLICE-03_BACKLOG.md` before branching.
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §13.4 states verbatim: "Exact epsilon is implementation ADR, not user-facing campaign data." §25.1 states verbatim: "Конкретная структура — implementation ADR." — confirmed by direct `Read` of the full 3070-line document.
- `08_Scenes_And_Board` §7.7 names exactly three Square distance metrics (`Euclidean` default, `ChebyshevDiagonalEqualsOne`, `AlternatingOneTwo`) plus hex-distance and None-Euclidean, but gives no exact mathematical formula for any of them — confirmed by `Read`.
- `08_Scenes_And_Board` §4.4 states that Core geometry services must be reproducible independent of Unity Physics ("authoritative result воспроизводится детерминированной geometry-библиотекой Core") — the direct textual basis for why cross-platform determinism is required, not merely convenient.
- `08_Scenes_And_Board` §21.6 and BT-079 require Scene/Board state (including geometry-derived fog/token positions) to restore identically after restart — a concrete, testable determinism requirement already present in the product document.
- `ADR-002` §26 already documents `board.token.move v1` as an ordinary authoritative command — confirmed by `Read`; this ADR does not reopen the command model, only the geometry computation used inside its validation steps.
- `ADR-017` §1 already scopes board-delta delivery generically ("projection-состояния," not scene-specific) — confirmed by prior-session `Read`; not reopened here.
- No ADR file numbered `ADR-020` exists prior to this task — confirmed by `ls docs/adr/`; `ADR-020` is the next available number.
- `08_Scenes_And_Board` §25.4 (Unity rendering) and `OPEN-BOARD-004`/`OPEN-BOARD-005` (fog representation, circular footprint rasterization) are explicitly out of scope for this ADR per the product document's own labeling (presentation-layer / non-blocking open item), confirmed by `Read`.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`git log` before and during this task.

## 5. Scope

### In scope

- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (new).
- `docs/tasks/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md` (this file), `docs/plans/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md` (governing ExecPlan).
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row status only.

### Out of scope

- Any production code (geometry service implementation, unit tests) — a separate future implementation task.
- Full `Scene`/`Board`/`SceneObject`/`Token` domain schema — implementation-task content built atop this ADR's math, not this ADR's content.
- Unity rendering-performance decisions (`08_Scenes_And_Board` §25.4).
- Any edit to `ADR-002`/`ADR-008`/`ADR-017`, or `ODY-S03-000`'s own files — this task only reads them.
- `ODY-S03-002` (Extended Audience and Selected-Participant Visibility) — independent task, not touched here.
- Any technical spike — `SLICE-03_BACKLOG.md` §3 already justifies "no spike required"; this ADR's own Definition of Done (§11) confirms every decision is provable by golden-vector/brute-force-reference tests.

### Allowed paths

```text
docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md
docs/tasks/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md
docs/plans/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md
docs/tasks/SLICE-03_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — this task introduces no code. `ADR-020` §9 documents that a future implementation must keep Core geometry services free of `UnityEngine`/Unity Physics dependency (`ADR-001`); this task does not itself touch either module.
- Authoritative-state and transaction boundary: not applicable to this task's own execution; `ADR-020` fixes only the internal geometric computation used inside `ADR-002`'s already-existing command validation steps, not the transaction boundary itself.
- Serialization / compatibility boundary: not applicable — no DTO or codec introduced.
- Time / RNG rule: not applicable; `ADR-020` reuses `ADR-008`'s IEEE-754 `double` numeric contract without extending it to a new domain-specific RNG concern.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable — geometry math carries no hidden-information classification of its own; visibility/redaction remains `ADR-019`'s and the future `ODY-S03-002`'s domain.
- Performance or platform constraint: `ADR-020` §7 fixes a spatial-index approach reasoned from `08_Scenes_And_Board` §25's stated MVP scale (≤200 tokens); it does not fix a numeric performance target (that remains `08_Scenes_And_Board` §25.4's implementation-tuning concern).
- Other: `ADR-020` must not silently expand scope beyond `SLICE-03_BACKLOG.md` §5's fixed boundary (e.g., by defining domain schema or Unity rendering) — verified in the Completion evidence section.

## 7. Expected behavior

This is a pure documentation/decision-authoring task; "expected behavior" here means the ADR's own normative content, not runtime behavior.

### Scenario 1 — epsilon and spatial-index questions are closed with concrete, versioned decisions

**Given** `08_Scenes_And_Board` §13.4/§25.1's explicit "implementation ADR" deferrals
**When** `ADR-020` §6/§7 are written
**Then** they fix `GeometryEpsilonV1 = 1e-6` world units with a fail-closed boundary rule, and `SpatialIndexV1` = uniform spatial hash, each with stated rejected alternatives.

### Scenario 2 — distance formulas are exact, not merely named

**Given** `08_Scenes_And_Board` §7.7 names but does not define `Euclidean`/`ChebyshevDiagonalEqualsOne`/`AlternatingOneTwo`/hex-distance
**When** `ADR-020` §5 is written
**Then** each metric has an exact, unambiguous formula, with no metric introduced beyond the four/five named by the product document.

### Scenario 3 — cross-platform determinism is fixed as a testable guarantee

**Given** `08_Scenes_And_Board` §4.4's requirement that Core geometry be reproducible independent of Unity Physics, and BT-079's restart-restore requirement
**When** `ADR-020` §4 and §11 (Definition of Done) are written
**Then** they fix the exact numeric contract (`System.Double`/`System.Math` only, fixed operation order) and require a golden-vector cross-compilation-target test as acceptance evidence for the future implementation task.

### Required invariants

- `ADR-020` does not modify `ADR-002`'s, `ADR-008`'s, or `ADR-017`'s own files.
- `ADR-020` does not introduce Scene/Board/SceneObject/Token domain schema content or a Unity rendering-performance decision anywhere in its normative text.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md`, this task contract, its ExecPlan, `docs/tasks/SLICE-03_BACKLOG.md` (`ODY-S03-001` row status).
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` exists, `Status: Accepted`, mirroring `ADR-019`'s structural format.
2. `ADR-020` fixes an exact, versioned epsilon convention (`GeometryEpsilonV1`) with a stated value, rationale, and a fail-closed rule for the epsilon-boundary case.
3. `ADR-020` fixes exact formulas for all three Square distance metrics named by `08_Scenes_And_Board` §7.7, plus hex-distance and None-Euclidean — no metric introduced beyond those named.
4. `ADR-020` fixes a versioned spatial-index approach (`SpatialIndexV1`) with a stated rationale against the rejected alternative (R-tree/quadtree).
5. `ADR-020` fixes the cross-platform numeric contract (`System.Double`/`System.Math` only, fixed operation order, floor-based grid-coordinate rounding) as the basis for deterministic reproducibility across compilation targets.
6. `ADR-020` explicitly excludes domain schema, Unity rendering optimization, network delta delivery, and the command model from its own scope, citing `ADR-002`/`ADR-017` as the authorities not reopened.
7. `ADR-002`, `ADR-008`, and `ADR-017` files are unmodified by this task's diff.
8. `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` pass.
9. `git diff --name-status` against `main` shows only files listed in §5's Allowed paths.
10. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Read `ADR-020` end-to-end after writing to confirm the epsilon convention, all distance formulas, the spatial-index decision, the cross-platform determinism contract, the explicit exclusions, and the Definition of Done for the future implementation task are all present and substantive, per this task's own explicit instructions.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test` — no production or test code is touched by this task.
- Any empirical test of geometry performance or correctness — that is the future implementation task's scope, proven via the Definition of Done (`ADR-020` §11) this task fixes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-020` is a new, standalone document referenced by nothing else in the repository yet.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

No dependency is introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: None directly — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable to this task's own execution.
- Authorization / audience checks: Not applicable — visibility/redaction remains `ADR-019`'s and the future `ODY-S03-002`'s domain, not this ADR's.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1.2, not presumed from precedent alone (the closest precedent, `ODY-S02-006`, also a pure-documentation ADR-authoring task, independently reached the same conclusion for the same class of reason). This task matches §1.2's "requires investigation before the implementation path is known" trigger directly: the exact formulas for the named distance metrics, the epsilon value, and the spatial-index structure were not given by `08_Scenes_And_Board` and had to be derived/chosen with justification, cross-checked against `ADR-008`'s existing determinism precedent and against `08_Scenes_And_Board`'s own stated MVP scale — genuine design-tradeoff work (`ADR-020` §12, five rejected alternatives), not a mechanical transcription. It also touches "authoritative state" in the `PLANS.md` §1.2 sense indirectly: the geometry this ADR fixes is the basis for authoritative movement/LOS/cover validation inside `ADR-002`'s command pipeline.
- ExecPlan path: `docs/plans/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md`
- Expected pull request count: 1 (single Draft PR covering `ADR-020`, this task contract, its ExecPlan, and the backlog row update).
- Milestone or sequencing constraints: no dependency on `ODY-S03-002` (`SLICE-03_BACKLOG.md` §6 — mutually independent). Blocks the future `SLICE-03` vertical-slice implementation backlog's board/movement work.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (new), this task contract, its ExecPlan, `docs/tasks/SLICE-03_BACKLOG.md` (`ODY-S03-001` row only).
- Documents that must not change: `ADR-001`–`019`, `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`, `docs/tasks/active/ODY-S03-000_*` (read only), `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything else under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: introduces the first concrete Board geometry/determinism contract (documentation only, no code-level version bump, since no code changes).
- Documentation version changes: `ADR-020` is a new document (v1.0); no existing ADR changes version.
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

- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` — new.
- `docs/tasks/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md` (this file), `docs/plans/active/ODY-S03-001_ADR_Board_Geometry_And_Movement_Determinism.md` — new.
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row status.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| CI — PR #55, commit `d20baff` | Passed | Run [32963124537](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/32963124537): `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`, confirmed via `gh pr view 55 --json state,isDraft,statusCheckRollup`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-020` Status: `Accepted`, structural format mirrors `ADR-019` (Решение/Контекст/Термины/normative sections/Не входит/module-boundary/Правила для Codex/Definition of Done/Альтернативы/Трассировка/Нормативное действие). |
| AC-2 | Passed | `ADR-020` §6 — `GeometryEpsilonV1 = 1e-6` world units, rationale given, §6.3 fail-closed rule for the epsilon-boundary case. |
| AC-3 | Passed | `ADR-020` §5 — exact formulas for `Euclidean`, `ChebyshevDiagonalEqualsOne`, `AlternatingOneTwo`, hex cube-distance, and None-Euclidean; no metric beyond `08_Scenes_And_Board` §7.7's named set. |
| AC-4 | Passed | `ADR-020` §7 — `SpatialIndexV1` = uniform spatial hash, §7.2 rejects R-tree/quadtree with stated rationale. |
| AC-5 | Passed | `ADR-020` §4 — `System.Double`/`System.Math` only, fixed operation order (§4.2), floor-based grid-coordinate rounding (§4.3). |
| AC-6 | Passed | `ADR-020` §8 — explicitly excludes domain schema, Unity rendering, network delta delivery, and command model, citing `ADR-002`/`ADR-017`. |
| AC-7 | Passed | `git status --porcelain` confirms no `ADR-002`/`008`/`017` file touched. |
| AC-8 | Passed | See Validation results table above — both commands pass. |
| AC-9 | Passed | `git status --porcelain` shows only `ADR-020`, this task contract, its ExecPlan, and the one `SLICE-03_BACKLOG.md` row. |
| AC-10 | Passed | Draft PR [#55](https://github.com/odyssey-services/Odyssey_VTT/pull/55) open; all 4 required CI checks `SUCCESS` on run 32963124537 (commit `d20baff`); PR remains Draft pending explicit owner confirmation before any merge. |

## 18. Blockers, risks, and open decisions

- No blockers for this task's own closure.
- Open decision (deliberately left to future tasks, not this one): whether `SpatialIndexV1`'s uniform-hash approach remains sufficient post-MVP at larger scale, or whether a future `SpatialIndexV2` (R-tree/quadtree) amendment becomes justified — `ADR-020` §7.2/§16 states this requires amendment or a superseding ADR, not silent expansion.
- Risk: this ADR's formulas/epsilon/index choice have not yet been empirically exercised by real code — that is exactly the future implementation task's job, proven against this ADR's own Definition of Done (§11); this task's own risk is limited to the contract being wrong in a way that task would then discover, which is the expected and intended division of labor, not a defect of this task.
