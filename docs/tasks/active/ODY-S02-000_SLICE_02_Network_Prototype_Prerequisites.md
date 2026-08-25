# ODY-S02-000 - SLICE-02 Network Prototype Prerequisites

**Status:** Draft
**Roadmap stage / slice:** SLICE-02
**Owner:** Unassigned
**Requested by:** Product owner
**Branch:** `feat/ody-s02-000-slice-02-network-prototype-prerequisites`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-08-25
**Last updated:** 2026-08-25 UTC

## 1. Goal

Close all roadmap Stage 3 prerequisite requirements — five ADRs (transport abstraction, rendezvous/relay strategy, snapshot/delta/reconnect model, identity baseline, permissions baseline) and two technical spikes (`SP-03 — Internet Connectivity`, `SP-04 — Hidden Data Boundary`) with their reports — before any `SLICE-02` vertical-slice implementation work (GM hosts, player joins, role assignment, scene sync, movement, reconnect) begins.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-01`/`M2` is closed (owner-accepted 2026-08-25, PR #36 merged), but no `SLICE-02` organizational structure exists yet. Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` section 11.2 requires four prerequisite product documents (three already exist, one does not — see section 4) plus three ADR categories before Stage 3 implementation can begin; section 11.3 additionally names Identity baseline and Permissions baseline as required "Входит" categories that this task treats as their own ADRs (see section 5 for the count justification).
- Value or risk reduction: `SLICE-02` introduces a fundamentally different risk class than `SLICE-01` — persistence was deterministic and local; networking introduces non-determinism, external services (relay/rendezvous, Supabase Auth), and a real security perimeter (hidden-data boundary across an untrusted client). Deciding transport shape, relay strategy, identity/auth boundary, and permissions model before writing networking code is the same architecture-first discipline `SLICE-01` already used for persistence.
- Blocking or enabling relationship: Blocks all `SLICE-02` vertical-slice work (roadmap section 11.6, the ten-step "Первая сеть" scenario). Enables a future implementation backlog revision once all five ADRs are `Accepted` and both spike reports are complete and owner-reviewed.

## 3. Authorities and requirement references

### Required authorities

- `17_Roadmap_Odyssey_VTT_v0.11.md`, section 11 (Этап 3 — Network Prototype, Identity Baseline и Reconnect) in full — section 11.1 (goal), 11.2 (prerequisite documents), 11.3 (scope: Transport / Session / Identity baseline / Permissions baseline), 11.4 (`SP-03` scope), 11.5 (`SP-04` scope), 11.6 (the ten-step vertical slice, referenced only — not started by this task), 11.7 (exit criteria, referenced only), 11.8 (Milestone M3 statement, referenced only).
- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` — private, non-tracked (gitignored `Documentation/`, same convention as every other product document this session has cited), read in full for section headers; context source for the Transport, Session, Snapshot/Delta/Reconnect ADRs. Exact sections cited per child task in `docs/tasks/SLICE-02_BACKLOG.md` section 4.
- `07_Permissions_Odyssey_VTT_v0.7.md` — private, non-tracked; context source for the Permissions baseline ADR (confirms `PERM-INV-001`–`012`, `RolePreset`, `MainGM` already exist as *product* documentation, but no ADR yet fixes the *technical* baseline subset roadmap section 11.3 scopes to — Main GM/Player/Observer, read/action check, redacted scene projection).
- `21_Security_And_Privacy_Odyssey_VTT_v0.1.md` — private, non-tracked; already used in `ODY-S01-000`/`004` for the owner-key principle; here it is the source for the auth/JWT/service-role-key boundary the Identity baseline ADR implements (confirmed: section on campaign-backup exclusions already lists "OAuth/сессионные токены" as never persisted).
- `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` sections 4, 6.6 — confirms `Odyssey.Networking`'s module boundary is already normatively defined (`Persistence → Networking` forbidden, `Networking → Persistence` forbidden, `Networking` implements session/network ports Application declares, never touches SQLite or Domain state directly) even though no task has implemented it yet (verified fact, section 4 below).
- `docs/tasks/SLICE-02_BACKLOG.md` (this task's governed backlog).
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`.
- `AGENTS.md`, `PLANS.md`, `docs/tasks/TASK_TEMPLATE.md`.
- `docs/tasks/active/ODY-S01-000_SLICE_01_Local_Campaign_Prerequisites.md` and `docs/tasks/SLICE-01_BACKLOG.md` — direct structural precedent for this parent task and its backlog, per this task's own instruction.

### Requirement and test IDs

- Requirement IDs: `SLICE-02` (prerequisites revision only), Milestone `M3` (not closed by this task), roadmap `SP-03`, `SP-04`.
- Existing test IDs: None yet defined for `SLICE-02`.
- New test IDs to introduce: None by this task. Each ADR/spike child task defines its own if needed.

### Task-safe private context

- Approved summary / references: This task contract summarizes section-header structure and specific normative terms (`NW-INV-001`–`012`, `PERM-INV-001`–`012`, `RolePreset`, `MainGM`) from the private, non-tracked `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`/`07_Permissions_Odyssey_VTT_v0.7.md`/`21_Security_And_Privacy_Odyssey_VTT_v0.1.md`, the same level of reference this session's prior tasks (`ODY-S01-000`, `007`, `011`) already used for their own private-document sources. No verbatim content beyond section titles and named invariant IDs is copied into this tracked file.

## 4. Verified current state

### Verified facts

- `SLICE-01` is complete and `M2` is closed; the product owner explicitly accepted closure on 2026-08-25 (PR #36 merged, merge commit `032ea99`), per `docs/tasks/SLICE-01_IMPLEMENTATION_BACKLOG.md`'s own closure statement and `docs/tasks/active/ODY-S01-014_Traceability_and_Quality_Report.md` section 6.
- No `SLICE-02` task contract, backlog, or ADR exists anywhere in the repository as of this activation.
- `Documentation/18_Account_And_Identity.md` — the exact filename roadmap section 11.2 names — **does not exist**, under this name or any similar one, anywhere in the repository (confirmed by `find` across the full working tree, both tracked and gitignored paths; the only near-name matches were unrelated Unity build-cache files). This is recorded here as a real documentation gap, not invented or silently assumed to exist. `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`, `07_Permissions_Odyssey_VTT_v0.7.md`, and `21_Security_And_Privacy_Odyssey_VTT_v0.1.md` all exist locally (private, gitignored `Documentation/`, same convention as every other product document cited this session).
- `Packages/com.odyssey.networking/` physically exists in the repository (`Runtime/`, `package.json`) but has never been touched by any `ODY-S00-*`/`ODY-S01-*` task's actual implementation — confirmed by `grep` across every `docs/tasks/completed/`/`docs/tasks/active/` file: the only references are `ODY-S00-003`'s module-skeleton scaffold (created the empty package alongside the others) and `ODY-S00-004`–`007`'s/`ODY-S01-007`'s own `ProjectContractTests.cs` guard, which explicitly asserts `Odyssey.Networking.csproj` does **not** exist yet (`Exists=False`) — a deliberate Stage-3 gate, not an oversight.
- No `Odyssey.Networking.csproj` bridge project exists in `DotNet/Projects/`; no Permissions/roles ADR exists in `docs/adr/` (`ADR-002_Command_and_Domain_Event_Model_v1.0.md` mentions "permissions" only as a generic authoritative-check concept woven into command processing, not a concrete role model — confirmed by reading it).

### Assumptions

- None.

## 5. Scope

### In scope

- Creating this parent task contract (`ODY-S02-000`).
- Creating `docs/tasks/SLICE-02_BACKLOG.md`, listing and sequencing exactly seven child tasks: five ADR tasks (`ODY-S02-001` through `ODY-S02-005`) and two technical spike tasks (`ODY-S02-006`, `ODY-S02-007`). This parent task organizes and sequences them; it does not author their content.
- Determining and justifying the count and boundary of each prerequisite ADR — five, not the roadmap section 11.2's literal three ("ADR transport abstraction; ADR rendezvous/relay strategy; ADR snapshot/delta/reconnect"), because section 11.3's "Входит" list separately names Identity baseline and Permissions baseline as required scope categories with their own distinct concerns (auth/external-service boundary vs. authorization/role model) — the same reasoning `SLICE-01` used to split "Local Campaign Format" from "Owner Key Storage" into two ADRs rather than folding key custody into the format ADR.
- Determining and justifying the dependency order between the spikes and their related ADRs — specifically, that `SP-03` (Internet Connectivity) must run before the Rendezvous/Relay Strategy ADR can responsibly be marked `Accepted` on a concrete stack, mirroring `SP-02`'s empirical role in closing `ADR-011` v1.0 section 12.1 in `SLICE-01`.

### Out of scope

- Any networking implementation code, transport prototype, relay/rendezvous integration, or Supabase Auth integration — all deferred to a future implementation backlog revision, created only after all five ADRs below are `Accepted`.
- Any UI (lobby, session join flow, role-assignment UI), scene sync runtime, or movement-command runtime work.
- The `SLICE-02` vertical slice itself (roadmap section 11.6: GM hosts → player joins → role assignment → scene sync → movement → validation → reconnect) — not started by this task.
- Any ADR content. Each ADR's content is authored in its own separate child task, one at a time, by a separate future ТЗ. This task creates only the parent contract and backlog scaffold.
- Creating or modifying `docs/tasks/active/ODY-S02-001_...md` through `ODY-S02-007_...md`. These child task contract files are not created by this activation.
- Authoring or fetching `Documentation/18_Account_And_Identity.md` — its absence is recorded as a finding (section 4), not resolved by inventing its content.

### Allowed paths

```text
docs/tasks/active/ODY-S02-000_SLICE_02_Network_Prototype_Prerequisites.md
docs/tasks/SLICE-02_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/** (no ADR content is created by this task)
Documentation/06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md (private, non-tracked; read-only source)
Documentation/07_Permissions_Odyssey_VTT_v0.7.md (private, non-tracked; read-only source)
Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md (private, non-tracked; read-only source)
docs/plans/** (Brief plan mode; no ExecPlan is created)
docs/tasks/active/ODY-S02-001_*.md through ODY-S02-007_*.md (child task contracts; not created by this activation)
Packages/com.odyssey.networking/**, DotNet/Projects/** (no networking code, no bridge project)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable to this scaffold itself; `ADR-001`'s already-defined `Networking` module boundary (section 4 above) is the authority the future Transport ADR must respect, not something this task changes.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Not applicable; any wire-format/envelope decision belongs to the ADR child tasks, not this scaffold.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is introduced or approved by this contract. In particular, no relay/rendezvous vendor, Supabase SDK, or networking library is selected here — that is `SP-03`'s and the Rendezvous/Relay Strategy ADR's job.
- Security / privacy / redaction rule: `Documentation/06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`/`07_Permissions_Odyssey_VTT_v0.7.md`/`21_Security_And_Privacy_Odyssey_VTT_v0.1.md` remain private and non-tracked; only section-header structure and named invariant IDs are referenced in this tracked file, no verbatim private content beyond that.
- Performance or platform constraint: Not applicable.
- Other: None.

## 7. Expected behavior

### Scenario 1 - Parent task and backlog exist and are internally consistent

**Given** `SLICE-01`/`M2` is closed and no `SLICE-02` organizational structure exists
**When** this task contract and `docs/tasks/SLICE-02_BACKLOG.md` are created
**Then** the backlog lists exactly seven ordered child tasks (five ADRs plus two spikes), each with clear scope boundaries and dependency rules — including the `SP-03`-before-Relay-ADR ordering — and no child task contract file or ADR file exists as a result.

### Required invariants

- No ADR content is authored by this task.
- No implementation code, script, or configuration is introduced.
- The `SLICE-02` vertical-slice implementation backlog is explicitly deferred to a future backlog revision, not created here.
- `Documentation/18_Account_And_Identity.md`'s absence is recorded as a fact, not silently worked around.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract; `docs/tasks/SLICE-02_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/tasks/active/ODY-S02-000_SLICE_02_Network_Prototype_Prerequisites.md` exists, following `docs/tasks/TASK_TEMPLATE.md` with all 18 numbered sections present.
2. `docs/tasks/SLICE-02_BACKLOG.md` exists, mirrors the structure of `docs/tasks/SLICE-01_BACKLOG.md` (Purpose, Slice exit criteria, Ordered backlog table, Task boundaries, Dependency rules, Global non-goals, Backlog change control), and lists exactly 7 ordered child tasks with IDs `ODY-S02-001` through `ODY-S02-007`.
3. The backlog's dependency rules explicitly state and justify that `SP-03` precedes the Rendezvous/Relay Strategy ADR's `Accepted` status, not the reverse.
4. No child task contract file (`ODY-S02-001...md` through `ODY-S02-007...md`) exists as a result of this task.
5. No ADR file exists as a result of this task.
6. `Documentation/18_Account_And_Identity.md`'s non-existence is explicitly recorded in section 4 above, not silently assumed or invented.
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

- Owner review of the parent task contract and backlog scope/ordering before any `ODY-S02-00X` child task is activated.

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

No new dependency, GitHub Action, Unity package, executable, or download is approved by this contract. In particular, this task does not select a relay/rendezvous vendor or a Supabase SDK — those decisions belong to `SP-03` and the Rendezvous/Relay Strategy ADR.

## 13. Security, privacy, and hidden information

- Data classes handled: Section-header structure and named invariant IDs (`NW-INV-*`, `PERM-INV-*`) from private, already-approved product documents; no secrets, personal data, or hidden campaign content.
- Trust boundaries: Not applicable beyond the redaction rule below.
- Authorization / audience checks: Not applicable to this scaffold itself — this is exactly the subject matter the future Permissions baseline ADR decides.
- Redaction requirements: No secrets, personal data, local paths, or hidden campaign content may be introduced; only section titles and invariant IDs from the private source documents enter this tracked file.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None; concrete auth/identity/permissions mechanism decisions and their tests are deferred to the `ODY-S02-004` Identity Baseline and `ODY-S02-005` Permissions Baseline ADR child tasks.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` section 1.1's five conditions individually, not assumed by analogy to `ODY-S01-000` alone (though the same conclusion holds). (1) Contained in one area — a parent task contract plus a backlog scaffold, no production module touched. (2) Does not change a public contract, persisted format, protocol, permissions model, dependency graph, package version, or build pipeline — this task decides no technical question; it only organizes and sequences future decisions. (3) One clear implementation path — read the roadmap/product-document scope, determine and justify the ADR/spike count and ordering, write the two files. (4) Fits one focused pull request. (5) No migration or recovery procedure required. `PLANS.md` section 1.2's ExecPlan triggers do not apply: no port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed by this task itself — every one of those will be decided by a future child task, each of which will make its own Brief-plan-vs-ExecPlan decision independently, exactly as `SLICE-01_BACKLOG.md` section 3's closing note already established as the pattern.
- ExecPlan path: Not required
- Expected pull request count: 1 (this scaffold). Each subsequent ADR or spike child task will be its own separate task and pull request, not part of this activation.
- Milestone or sequencing constraints: Do not create any `ODY-S02-00X` child task contract until this parent task and backlog are reviewed. Do not begin ADR content authoring or `SP-03`/`SP-04` execution under this task.

## 15. Documentation and versioning impact

- Documents that must change: This task contract; `docs/tasks/SLICE-02_BACKLOG.md`.
- Documents that must not change: All ADRs, Technical Development Baseline, Active Documentation Baseline, product requirement documents, ExecPlans, and the three private `Documentation/` sources cited (read-only for this task).
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

Fill this section with real results before moving the task to `Done`. Not yet applicable — this activation only creates the parent task contract and backlog scaffold; no child task work has started.

### Changed files / areas

- This task contract and `docs/tasks/SLICE-02_BACKLOG.md` were created from repository authorities (roadmap section 11, `06_Networking_and_Session_Sync`, `07_Permissions`, `21_Security_And_Privacy`, `ADR-001`, and the `SLICE-01` structural precedent).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | See final report / commit evidence. |
| `.\scripts\check-repository-policy.ps1` | Passed | See final report / commit evidence. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 through AC-7 | Passed | Both files created per template/backlog structure; no child task or ADR file created; `18_Account_And_Identity.md` absence recorded; validation commands pass. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- This scaffold does not decide any technical question. All five ADRs and both spike reports remain to be authored in separate future child tasks.
- `Documentation/18_Account_And_Identity.md` does not exist; the Identity Baseline ADR child task (`ODY-S02-004`) will need to either source its content from elsewhere (e.g., `06_Networking_and_Session_Sync` section 6, `21_Security_And_Privacy`) or flag to the product owner that this named prerequisite document needs to be authored first — this task does not resolve that question, only surfaces it.

### Follow-up tasks

- `ODY-S02-001` through `ODY-S02-007`, to be created one at a time by separate future task activations, per `docs/tasks/SLICE-02_BACKLOG.md`.

### Self-review summary

- Scope review: Contract stays within organizational scaffold boundary; no ADR content, no implementation code, no vertical-slice work introduced.
- Architecture review: No architecture, ADR, or module-boundary change is introduced; `ADR-001`'s existing `Networking` boundary is only cited, not altered.
- Test review: No new TestCase IDs are introduced.
- Security/privacy review: Only section-header structure and named invariant IDs from the private source documents enter this tracked file; no other private content.
- Documentation/version review: No baseline, ADR, TDB, schema, protocol, ruleset, package, or application version is changed.

## 18. Blockers, decisions, and change control

### Blockers

- None at contract-creation. This contract requires owner review before any `ODY-S02-00X` child task is activated.
- Open question surfaced (not a blocker for this task, but flagged for whoever activates `ODY-S02-004`): `Documentation/18_Account_And_Identity.md` does not exist under this or any similar name.

### Decisions made during execution

- 2026-08-25 - Create the `ODY-S02-000` parent task contract and `docs/tasks/SLICE-02_BACKLOG.md` as an organizational scaffold only, mirroring the `ODY-S01-000`/`SLICE-01_BACKLOG.md` pattern, following explicit product owner request after `SLICE-01`/`M2` closure - Authority / approval: product owner instruction.
- 2026-08-25 - Decided on 5 ADRs (Transport Abstraction, Rendezvous/Relay Strategy, Snapshot/Delta/Reconnect, Identity Baseline, Permissions Baseline) rather than the roadmap section 11.2's literal 3, because section 11.3 separately names Identity baseline and Permissions baseline as required "Входит" categories with distinct concerns - Authority: roadmap section 11.2/11.3 read in full; same splitting reasoning `SLICE-01` used for Local Campaign Format vs. Owner Key Storage.
- 2026-08-25 - Decided `SP-03` (Internet Connectivity) must precede the Rendezvous/Relay Strategy ADR's `Accepted` status, mirroring `SP-02`'s role in `SLICE-01` (`ADR-011` v1.0 Accepted with an open provider-library question, later closed by `ADR-011` v1.1 on `SP-02`'s empirical recommendation) - Authority: roadmap section 11.4's own text ("Результат spike должен подтвердить конкретный Relay/rendezvous stack").

### Approved task changes

- None yet.
