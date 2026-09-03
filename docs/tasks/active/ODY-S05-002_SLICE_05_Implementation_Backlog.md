# ODY-S05-002 — Record ADR-027 Acceptance & Create SLICE-05 Implementation Backlog

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (prerequisite acceptance → implementation transition)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-002-slice-05-implementation-backlog`
**Pull request:** To be opened
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-09-03
**Last updated:** 2026-09-03 UTC

## 1. Goal

Record the product owner's explicit approval of `ADR-027 — Content Catalog & Item/Equipment System` (moving it from `Proposed` to `Accepted`), close `docs/tasks/SLICE-05_BACKLOG.md`'s prerequisite revision, and create `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — the active `SLICE-05` implementation backlog, ordering a Content Catalog MVP task group (`ODY-S05-101`–`106`) before any Inventory/`ItemInstance`/Equipment runtime task, per explicit product-owner sequencing direction. No product code, persistence schema, migrations, DTO implementation, Unity UI, assets, or tests are touched — this is PM/docs/architecture planning only.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-027` was proposed by `ODY-S05-001` (PR [#103](https://github.com/odyssey-services/Odyssey_VTT/pull/103), merged into `main`) but remained `Proposed` pending explicit product-owner approval; no `SLICE-05` implementation backlog exists yet, so there is no scaffolded starting point for any future `ODY-S05-1XX` child task.
- Value or risk reduction: gives `SLICE-05` implementation a decomposed, dependency-ordered first block (Content Catalog MVP) to pick up one task at a time — the same organizational discipline every prior slice in this repository used — while explicitly capturing the product owner's own MVP-scoping answers (base/Ruleset catalog only, MainGM authoring in MVP, Archived list requirement, real-usability validation) so they are not silently re-decided or lost when each child task is later authored.
- Blocking or enabling relationship: unblocks `ODY-S05-101` (the first `SLICE-05` implementation child task) from being authored; does not itself implement anything.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` (full read) — the sole prerequisite ADR this task records approval of.
- `docs/tasks/SLICE-05_BACKLOG.md` (full read) — the prerequisite backlog this task closes.
- `docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md` (full read) — direct predecessor task, structural precedent for this task's own contract.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` and `docs/tasks/active/ODY-S04-005_SLICE_04_Implementation_Backlog.md` (full read) — direct structural/procedural precedent for creating a slice implementation backlog from a closed prerequisite revision.
- `docs/adr/README.md` — read and updated for index consistency (ADR-027 moves from the "Proposed" list to the "accepted" list).

### Requirement and test IDs

- Requirement IDs: `SLICE-05` (implementation, Content Catalog MVP block), backlog `ODY-S05-002`.
- Existing test IDs: None reused.
- New test IDs introduced: None (pure documentation task, no production code).

### Task-safe private context

- Approved summary / references: `ADR-027`'s own already-written content is quoted/cross-referenced directly; the product owner's MVP-scoping answers (catalog-first sequencing, MainGM authoring, base/Ruleset-only scope, Archived list, real-usability validation) are given verbatim in this task's own requesting conversation and recorded here and in `ADR-027` section 20. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline -5 origin/main` confirmed PR [#103](https://github.com/odyssey-services/Odyssey_VTT/pull/103) (`ODY-S05-001`, proposing `ADR-027`) is merged into `main`, and `git merge-base --is-ancestor` confirmed the prior PR (#102) is a real ancestor of `origin/main`.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` was `Proposed` (confirmed by `Read`), with section 20 explicitly stating it "must not be marked `Accepted` until explicit product-owner approval is recorded in the task, PR, or an accepted follow-up document."
- `docs/tasks/SLICE-05_BACKLOG.md` was `Prerequisite backlog — IN PROGRESS` (confirmed by `Read`), naming `ADR-027` as its sole exit criterion.
- `docs/adr/README.md` listed `ADR-027` under a separate "Proposed ADRs pending explicit product-owner approval" section (confirmed by `Read`).
- No `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` file existed prior to this task (confirmed by `find`/`Glob`).
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md`/`docs/tasks/active/ODY-S04-005_SLICE_04_Implementation_Backlog.md` were read in full as the structural template for this task's own backlog document and task contract, adapted for `SLICE-05`'s own single-block (Content Catalog MVP only) scope per explicit product-owner sequencing direction, rather than decomposing the whole slice at once the way `SLICE-04`'s own first implementation-backlog revision did.

### Assumptions

- None. Every fact above was directly observed via `Read`/`git`/`Glob` during this task. The product-owner approval itself is not an assumption — it is the explicit instruction this task was given and directly records.

## 5. Scope

### In scope

- Update `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`: status `Proposed` → `Accepted`; section 20 records the explicit product-owner approval (date, this task's own ID) and the MVP-scoping decisions the approval also fixed (catalog-first sequencing, MainGM authoring in MVP, base/Ruleset-only scope, Archived list requirement, real-usability validation requirement) as backlog-level scope choices, without rewriting any of `ADR-027`'s own already-decided architecture (sections 1–19 unchanged).
- Update `docs/adr/README.md`: move `ADR-027` from the "Proposed" list into the accepted-ADR list, for index consistency (the same update `ODY-S05-001` made in the opposite direction when the ADR was first proposed).
- Update `docs/tasks/SLICE-05_BACKLOG.md`: mark the prerequisite revision `COMPLETE`, record the `ADR-027` acceptance and this task's own row, and note that `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` now exists and is the active backlog going forward.
- Create `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (new) — decomposes the Content Catalog MVP block into `ODY-S05-101`–`106`, explicitly justifies the base/Ruleset-only, MainGM-authoring-in-MVP, dedicated-validation-task, and Archived-list-is-data-only scope decisions, names but does not decompose the later Inventory/Equipment/attack blocks, and states explicitly that no new ADR is needed for this decomposition.
- Create this task contract (`docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md`).

### Out of scope

- Creating any `ODY-S05-101`–`106` task contract file — this task only reserves their numbers, titles, and boundaries in the new backlog document.
- Starting implementation of any reserved child task, or of any reserved future block (Inventory, `ItemInstance`/`ItemStack`, Equipment, item-sourced abilities/effects, `ItemDefinition` migration, full attack pipeline).
- Any product code, persistence schema, migrations, DTO implementation, Unity UI, Unity assets, or tests.
- Reopening any decision in `ADR-027`'s own sections 1–19, or any earlier-accepted ADR (`ADR-001`–`026`).
- Any campaign-specific catalog/override design (explicitly deferred per the product owner's own MVP answer, recorded as a backlog scope decision, not an ADR amendment).
- A full visual node editor, marketplace, `.odcontent` import/export, or a balanced MVP content pack — all explicitly named as non-goals in the new backlog's own section 8.

### Allowed paths

```text
docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md
docs/adr/README.md
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md
```

### Paths requiring explicit approval before editing

```text
Assets/**
Packages/**
DotNet/**
ProjectSettings/**
Documentation/**
docs/adr/ADR-001* through docs/adr/ADR-026*
docs/tasks/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md
docs/plans/active/ODY-S05-001_ADR_Content_Catalog_Item_Equipment_System.md
```

## 6. Technical constraints

- Module ownership and dependency direction: not applicable — no code.
- Authoritative-state and transaction boundary: not applicable.
- Serialization / compatibility boundary: not applicable.
- Time / RNG rule: not applicable.
- Unity / thread / lifetime rule: not applicable.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: not applicable to this task's own execution; the new backlog correctly builds each future child task atop `ADR-027`'s already-accepted permissions baseline (MainGM-only publish/migration) without reopening it.
- Performance or platform constraint: not applicable.
- Other: if any genuine architectural gap is found while recording acceptance or decomposing the catalog MVP block, this task must stop and report it rather than deciding it inline — verified explicitly in section 4/18; none was found.

## 7. Expected behavior

This is a pure documentation task; "expected behavior" means the new/edited documents' own normative content, not runtime behavior.

### Scenario 1 — ADR-027 acceptance is explicit, not inferred

**Given** `ADR-027` section 20's own explicit precondition for `Accepted` status
**When** this task records product-owner approval
**Then** section 20 states the approval explicitly (date, approving party, recording task ID) before the status line changes, and the MVP-scoping decisions the approval also fixed are recorded as backlog-level scope choices distinct from `ADR-027`'s own sections 1–19.

### Scenario 2 — the new backlog sequences Content Catalog MVP before runtime blocks

**Given** the product owner's explicit "catalog first" direction
**When** `SLICE-05_IMPLEMENTATION_BACKLOG.md` is written
**Then** its ordered backlog table contains only `ODY-S05-101`–`106` (Content Catalog MVP), its dependency rules keep every dependency inside that same block, and Inventory/`ItemInstance`/Equipment/attack are named as reserved future blocks, not decomposed into task IDs.

### Scenario 3 — every product-owner MVP answer is captured, not lost

**Given** the five explicit product-owner MVP-scoping answers (catalog-first, MainGM authoring, base/Ruleset-only, Archived list, real-usability validation)
**When** the new backlog's section 3 is written
**Then** each answer maps to its own explicitly-justified scope decision and to the specific child task(s) that implement it.

### Required invariants

- `ADR-027` sections 1–19 are unmodified; only the status line, the introductory "once accepted" sentence, and section 20 change.
- No `ODY-S05-101`–`106` task contract file is created by this task.
- `ADR-001`–`026` files are unmodified.
- No product code, schema, or test file is touched.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `ADR-027` (status/section-20 update), `docs/adr/README.md`, `docs/tasks/SLICE-05_BACKLOG.md` (closure update), `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `ADR-027` status is `Accepted`, with explicit product-owner approval recorded in section 20 (date, approving party, recording task ID).
2. `docs/tasks/SLICE-05_BACKLOG.md` reflects prerequisite completion (status line, exit-criteria section, ordered-backlog row, and change-control section all updated consistently).
3. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` exists and orders Content Catalog MVP tasks (`ODY-S05-101`–`106`) before any Inventory/`ItemInstance`/Equipment/attack task — the latter are named as reserved future blocks, not decomposed.
4. The product owner's MVP-scoping answers (catalog-first sequencing, MainGM authoring in MVP, base/Ruleset-only scope with no campaign overrides, Archived list visible to GM, real-usability validation) are each captured as an explicit, justified scope decision in the new backlog's section 3, and mapped to the specific task(s) implementing them.
5. This task's own diff is docs-only — `git diff --name-status` against `main` shows only files listed in section 5's Allowed paths.
6. `.\scripts\verify-format.ps1` passes.
7. `.\scripts\check-repository-policy.ps1` passes.
8. The pull request description states explicitly that code/schema/runtime implementation is intentionally deferred to the reserved future `ODY-S05-1XX` child tasks.
9. `docs/adr/README.md` lists `ADR-027` consistently under accepted ADRs, with no residual "Proposed" listing.
10. No `ADR-001`–`026` file, and no `ODY-S05-101`–`106` task contract file, is created or modified by this task.

## 10. Tests and validation

### Required automated tests

None (pure documentation task).

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Read `ADR-027` end-to-end after editing to confirm sections 1–19 are byte-for-byte unchanged except the status line and the single introductory sentence naming section 20.
- Read `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` end-to-end after writing to confirm the five product-owner MVP answers are substantively captured (Scenario 3), not just present as headings.
- `git diff --name-status` review to confirm docs-only scope limited to section 5's Allowed paths.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: PowerShell validation only.

### Validation not required by this task

- `dotnet build`, `dotnet test`, `test-unity`, `build-dev`, migration rehearsal, and player smoke — not required because no code, schema, Unity, package, or CI file changes.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — no code, schema, or protocol is touched. `ADR-027` becoming `Accepted` is a documentation-status change with no persisted-state effect until a future implementation task exists.
- Version fields affected: None. `ADR-027` remains document version `1.0`.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; `ADR-027`'s status would revert to `Proposed` and the new backlog document would cease to exist, with no other repository state affected.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None — this task touches no code, credential, or campaign data.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task records an approval and organizes future work; it does not implement or change any authorization mechanism. `ADR-027`'s own MainGM-only publish/migration permissions baseline is cited, not altered.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: evaluated fresh against `PLANS.md` §1's triggers, following the same reasoning `ODY-S04-005` used for its own analogous create-implementation-backlog task. This task introduces no new architecture, module, public contract, persisted format, protocol, permissions model, dependency graph, Unity/package version, or build pipeline change beyond what `ADR-027` (already accepted-in-substance, now recorded as such) already fixed — it records an approval and organizes future task numbers and boundaries. It does not span multiple milestones or PRs (single Draft PR), does not change any production module (zero code touched), has one clear implementation path (record the approval, close the prerequisite backlog, write the new implementation backlog), and completes in one focused pull request with no migration or recovery procedure required.
- ExecPlan path: Not required.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `docs/tasks/SLICE-05_BACKLOG.md`'s prerequisite ADR (`ADR-027`) has explicit product-owner approval to record (given directly in this task's own requesting instruction). Unblocks `ODY-S05-101` (the first `SLICE-05` implementation child task, no dependency per the new backlog's own section 9).

## 15. Documentation and versioning impact

- Documents that must change: `ADR-027` (status/section 20), `docs/adr/README.md`, `docs/tasks/SLICE-05_BACKLOG.md` (closure), `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (new), this task contract.
- Documents that must not change: `ADR-001`–`026`, `ADR-027` sections 1–19, `docs/tasks/active/ODY-S05-001_*`, `docs/plans/active/ODY-S05-001_*`, `docs/tasks/completed/*`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, anything under `Documentation/`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: `ADR-027` status changes `Proposed` → `Accepted`; its own document version stays `1.0`.
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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that code/schema/runtime implementation is intentionally deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` — status `Proposed` → `Accepted`; section 20 records explicit product-owner approval and the MVP-scoping decisions it also fixed.
- `docs/adr/README.md` — `ADR-027` moved from the "Proposed" list into the accepted-ADR list.
- `docs/tasks/SLICE-05_BACKLOG.md` — status, exit criteria, ordered-backlog row, and change-control section updated to reflect prerequisite completion.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — new. Content Catalog MVP block (`ODY-S05-101`–`106`), scope decisions, dependency rules, reserved future blocks, non-goals.
- `docs/tasks/active/ODY-S05-002_SLICE_05_Implementation_Backlog.md` (this file) — new.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| `git diff --name-status` scope review | Pass | Confirmed exactly the five files in section 5's Allowed paths are touched; no `ADR-001`–`026`, no `ODY-S05-101`–`106` contract, no code/schema/test file. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `ADR-027` line 7 reads `Accepted`; section 20 records the 2026-09-03 approval, approving party (product owner), and this task's own ID. |
| AC-2 | Pass | `SLICE-05_BACKLOG.md` status line, §2, ordered-backlog row 2, and §6 all updated consistently. |
| AC-3 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` §5 lists only `ODY-S05-101`–`106`; §7 names Inventory/`ItemInstance`/Equipment/attack as reserved, not decomposed. |
| AC-4 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` §3.1–3.5, each mapped to its owning task(s). |
| AC-5 | Pass | `git diff --name-status` confirms docs-only scope, limited to §5's Allowed paths. |
| AC-6 | Pass | Validation-results table above. |
| AC-7 | Pass | Validation-results table above. |
| AC-8 | Pass | Pull request description states code/schema/runtime implementation is intentionally deferred to `ODY-S05-101`–`106` and the reserved future blocks. |
| AC-9 | Pass | `docs/adr/README.md` lists `ADR-027` once, under accepted ADRs; no "Proposed" section remains. |
| AC-10 | Pass | `git status --porcelain` confirms no `ADR-001`–`026` file and no `ODY-S05-101`–`106` contract file touched. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- No `ODY-S05-101`–`106` task contract exists yet — each is created and activated one at a time, per the new backlog's own scaffold-only intent.
- Inventory/`ItemInstance`/Equipment/attack blocks are named but not decomposed; a future backlog revision will decompose each once the Content Catalog MVP block is accepted and closed.

### Follow-up tasks

- `ODY-S05-101` — Content Catalog Foundation (first child task, no dependency).
- A future backlog revision decomposing the reserved Inventory/`ItemInstance`/Equipment/attack blocks (section 7 of the new backlog), once the Content Catalog MVP block closes.

### Self-review summary

- Scope review: limited to the five allowed documentation files; `ADR-001`–`026`, `ADR-027` sections 1–19, and `ODY-S05-001`'s own task/plan files left untouched.
- Architecture review: decomposition reuses `ADR-027` and earlier substrate without redefinition; no new ADR proposed; no unresolved architectural gap found. Base/Ruleset-only, MainGM-authoring, dedicated-validation-task, and Archived-list-is-data-only decisions are backlog-level scope choices explicitly attributed to the product owner's own MVP answers, not silent architecture.
- Test review: no tests changed; required docs/policy validation passed.
- Security/privacy review: no private excerpts copied beyond direct citation of already-public repository documents; no authorization mechanism changed; `ADR-027`'s own MainGM-only baseline is cited, not altered.
- Documentation/version review: `ADR-027` status changed `Proposed` → `Accepted`; no app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-03 — Decision: record `ADR-027` as `Accepted` with the product owner's explicit approval given directly in this task's own requesting instruction ("Product owner approved moving forward with `ADR-027`"). Authority / approval: Product owner, this task's own §2 context.
- 2026-09-03 — Decision: capture the product owner's five MVP-scoping answers (catalog-first sequencing, MainGM authoring in MVP, base/Ruleset-only scope, Archived list requirement, real-usability validation requirement) as backlog-level scope decisions in the new `SLICE-05_IMPLEMENTATION_BACKLOG.md` (section 3), and cross-reference them from `ADR-027` section 20, rather than treating them as an amendment to `ADR-027`'s own sections 1–19 — none of them contradicts or narrows `ADR-027`'s own already-decided architecture; they only choose which parts of that architecture this first implementation revision exercises. Authority / approval: Product owner, this task's own §2 context.
- 2026-09-03 — Decision: decompose only the Content Catalog MVP block (`ODY-S05-101`–`106`) in this revision, rather than the whole `SLICE-05` slice at once the way `SLICE-04`'s own first implementation-backlog revision did — explicit product-owner sequencing direction ("Catalog MVP must be technical foundation first"). Inventory/`ItemInstance`/Equipment/attack are named and reserved (section 7) for a future backlog revision. Authority / approval: Product owner, this task's own §2 context.
- 2026-09-03 — Decision: split Catalog Validation (`ODY-S05-104`) into its own dedicated task rather than folding it into Foundation (`101`) or Authoring (`102`), because the product owner's own explicit "real usability, not just required fields" answer requires materially larger, type-specific rules than storage/authoring plumbing, and `103`'s own publish gate needs one authoritative validation source to call. Authority / approval: Product owner's own MVP answer, applied the same way `ODY-S04-113a`/`ODY-S04-115a`'s own precedent split a materially different concern into its own task rather than folding it into an adjacent one.

### Approved task changes

- None yet.
