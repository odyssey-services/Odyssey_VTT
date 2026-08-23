# ODY-S01-004 — ADR: Owner Key Storage Baseline

**Status:** Done  
**Roadmap stage / slice:** SLICE-01 (prerequisites)  
**Owner:** Codex (agent)  
**Requested by:** Product owner  
**Branch:** `feat/ody-s01-004-adr-owner-key-storage-baseline`  
**Pull request:** Draft — [#25](https://github.com/odyssey-services/Odyssey_VTT/pull/25)  
**ExecPlan:** `docs/plans/completed/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md`  
**Created:** 2026-08-20  
**Last updated:** 2026-08-20 UTC

## 1. Goal

Produce an `Accepted`-ready ADR (`ADR-014_Owner_Key_Storage_Baseline_v1.0.md`) that concretely defines the Windows OS mechanism for storing campaign owner key material (Windows Credential Manager, `CurrentUser` scope), the stored entity's format, rotation policy, loss/unavailability behavior, and an explicit resolution of `ADR-012` §12.1 (backup encryption at rest) — implementing, without altering, the principle already confirmed in `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5.

## 2. Why this task exists

- Problem or dependency being addressed: `21_Security_And_Privacy` §5 fixes the principle (owner key never enters the campaign file, stored via OS secure storage) but explicitly defers the concrete mechanism to this ADR. Without it, implementation tasks could each pick an inconsistent storage mechanism, and `ADR-012` §12.1 (backup encryption at rest) would remain unresolved indefinitely, since it is explicitly gated on this ADR's acceptance.
- Value or risk reduction: fixes a single, concrete Windows mechanism (Credential Manager, not a self-managed DPAPI file) before any code exists, closes `ADR-012` §12.1 with an explicit, justified answer, and defines safe, non-silent behavior for owner-key loss without inventing unneeded cross-platform abstraction.
- Blocking or enabling relationship: independent of `ODY-S01-001`–`003` per `SLICE-01_BACKLOG.md` §5; closes the last of the four ADRs required for the `SLICE-01` prerequisite backlog's exit criteria (`SLICE-01_BACKLOG.md` §2).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.2.md`
- `AGENTS.md`
- `PLANS.md` §1.2
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` (Accepted) §1 п.13 — confirms `PE-INV-010` boundary, forward-references this ADR
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (Accepted) §12.1 — open question this ADR resolves
- `docs/adr/ADR-013_Migration_Runner_v1.0.md` (Accepted) — no direct dependency, verified as context
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md` — Windows Standalone x86-64 MVP platform baseline, justifies a Windows-specific (not abstracted cross-platform) mechanism
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` — safe-error contract for owner-key-absent reporting
- `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5, §6 — private local reference; §5 is the principle this ADR implements, §6 is the threat-model boundary this ADR must not alter
- `05_Persistence_Odyssey_VTT_v0.8.md` `PE-INV-010`, §30–31 — private local reference, not committed to the repository

### Requirement and test IDs

- Requirement IDs: None (ADR-only task; no formal requirement ID registry entry exists yet for this contract)
- Existing test IDs: None
- New test IDs to introduce: None (this task produces no code)

### Task-safe private context

- Approved summary / references: `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5 is quoted verbatim in this task contract (per the same pattern already used for `ODY-S01-000`), as it is the exact principle this ADR is bound to implement without alteration:

  > «Owner key никогда не входит в файл кампании, `campaign.db`, `.odcamp` или backup. Owner key хранится через предоставляемое ОС защищённое хранилище (secure storage конкретной платформы), а не в виде обычного файла рядом с кампанией.
  >
  > Конкретный механизм хранения (какой именно OS API, формат, ротация, восстановление при потере) — предмет отдельной ADR owner key storage baseline. Эта ADR реализует принцип, зафиксированный здесь, и является источником истины для деталей реализации. Данный документ не дублирует и не предвосхищает решения этой ADR.»

  §6 (threat-model boundary — what MVP does not guarantee) is summarized, not quoted verbatim, into `ADR-014` §9, and is not altered by this ADR. `05_Persistence_Odyssey_VTT_v0.8.md` `PE-INV-010`/§30–31 are summarized into ADR-014 without pasting private document text verbatim beyond short normative phrases already customary in this repository's ADRs. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`, `ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md`, and `ADR-013_Migration_Runner_v1.0.md` all carry `**Статус:** Accepted` on `main` at commit `bf99bf4`, confirmed by `grep` before branching.
- `docs/tasks/SLICE-01_BACKLOG.md` rows for `ODY-S01-001`–`003` all read `Done` on `main`, confirmed by `Read`.
- `docs/adr/` contains ADR-001 through ADR-013; `ADR-014` is the next unused number, confirmed by directory listing.
- No `docs/tasks/active/ODY-S01-004_*` or `docs/adr/ADR-014_*` file existed on `main` prior to this task.
- `SLICE-01_BACKLOG.md` §5 confirms `ODY-S01-004` has no dependency on `ODY-S01-001`–`003` and may begin independently.
- `ADR-009_Unity_Project_and_Build_Baseline_v1.1.md` confirms the MVP production platform is Windows Standalone x86-64 only (no UWP/ARM64/Web/server in baseline), confirmed by `grep`.

### Assumptions

- None. All facts above were directly observed via `Read`/`grep`/directory listing on the current `main` branch before branching for this task.

## 5. Scope

### In scope

- `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` (new): concrete Windows storage mechanism (Windows Credential Manager, `CurrentUser` scope), stored-entity format, rotation policy, loss/unavailability behavior, explicit resolution of `ADR-012` §12.1, explicit confirmation that the `21_Security_And_Privacy` §6 threat-model boundary is unaltered, persistence-layer behavior on reinstall/machine change/campaign export, explicit exclusions and open questions.
- `docs/tasks/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md` (this file).
- `docs/plans/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md` (governing ExecPlan, see §14).
- `docs/tasks/SLICE-01_BACKLOG.md` §3 — update only the `ODY-S01-004` row (Status, Planning mode).

### Out of scope

- Any implementation code (C#, P/Invoke, Unity) for Credential Manager access.
- Campaign format content (`ADR-011`), snapshot/journal content beyond resolving `ADR-012` §12.1 (`ADR-012`), migration runner content (`ADR-013`).
- The exact cryptographic use of owner key material (command signing, GM Host authority proof, future networking authentication) — deferred to future Stage 3 (networking/account) ADRs.
- UX/application-level behavior when owner key material is absent on a new machine or after loss — explicitly left `[OPEN]`, not invented.
- Cross-platform (macOS Keychain, Linux Secret Service) storage — explicitly excluded given the Windows-only MVP baseline.
- Any change to `ADR-011`/`ADR-012`/`ADR-013` content or status.
- Any change to `ODY-S01-005` row in `SLICE-01_BACKLOG.md`.
- Any change under `docs/tasks/completed/`, `docs/plans/completed/`, `ODY-S00-*`, or `Documentation/` (beyond the verbatim §5 quote placed in this task contract, per §3 above — the source document itself is not edited).

### Allowed paths

```text
docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md
docs/tasks/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md
docs/plans/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md
docs/tasks/SLICE-01_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable (documentation-only; no code). Content must remain consistent with `ADR-001` (Persistence does not own secret custody outside campaign files; owner key storage is an OS-boundary concern, addressed here at the ADR level only).
- Authoritative-state and transaction boundary: Not applicable — owner key material is explicitly never part of `campaign.db`/journal/snapshot state.
- Serialization / compatibility boundary: this ADR explicitly forbids serializing owner key material into any campaign file format governed by `ADR-003`'s canonical-codec principle.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable (no code in this task; future implementation must respect Windows API threading/marshaling constraints, not decided here).
- Dependency / licensing rule: No new dependency — Windows Credential Manager is accessed via `Advapi32.dll`, part of the Windows OS, not a third-party package.
- Security / privacy / redaction rule: this is the primary subject of the ADR — owner key material must never appear in `manifest.json`, `campaign.db`, `.odcamp`, backup/snapshot, or diagnostic bundles (`ADR-010` redaction principle, `PE-INV-010`).
- Performance or platform constraint: Windows Standalone x86-64 only, per `ADR-009` — no cross-platform abstraction introduced.
- Other: must not silently alter `21_Security_And_Privacy` §6's threat-model boundary; must explicitly resolve, not merely re-defer, `ADR-012` §12.1.

## 7. Expected behavior

This is a documentation contract task; "behavior" is expressed as required normative content rather than runtime scenarios.

### Required invariants

- ADR-014 names a concrete Windows API/mechanism (Windows Credential Manager, generic credential, `CurrentUser` scope) with explicit justification, not an abstract "OS secure storage" placeholder.
- ADR-014 states the stored entity's format (opaque credential blob, non-secret `TargetName` reference) and scope (per-user, not per-machine), with justification tied to the already-confirmed `21_Security_And_Privacy` §6 threat-model boundary.
- ADR-014 states rotation policy for MVP (no automatic rotation; explicit user-initiated regenerate only) with justification.
- ADR-014 states loss/unavailability behavior for MVP (unrecoverable at the persistence layer; campaign data itself is not lost; safe-error reporting required) with justification.
- ADR-014 explicitly resolves `ADR-012` §12.1 (backup encryption at rest) with a definitive answer and justification, not a re-deferral.
- ADR-014 explicitly confirms it does not alter `21_Security_And_Privacy` §6's threat-model boundary, and states what would happen if a conflict were found (it was not).
- ADR-014 describes persistence-layer behavior for reinstall (same machine/user), machine change, and campaign export, and explicitly flags UX/application-level behavior on owner-key absence as `[OPEN]`, not invented.
- ADR-014 does not decide campaign format, snapshot/journal content beyond §12.1, migration runner content, the cryptographic use of the key, or cross-platform storage.

## 8. Deliverables

- Production code: None
- Tests: None
- Scripts / CI: None
- Configuration: None
- Documentation: `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md`, this task contract, the governing ExecPlan, and the `ODY-S01-004` row update in `docs/tasks/SLICE-01_BACKLOG.md`.
- Generated evidence or build artifacts: validation command output recorded in §17.
- Migration / recovery material: None (this ADR describes but does not implement the storage mechanism)

## 9. Acceptance criteria

1. `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` exists with `**Статус:** Proposed` and contains all required normative content listed in §7's invariants.
2. The ADR names a concrete Windows mechanism (Windows Credential Manager, `CurrentUser` scope) with explicit justification, not an abstract placeholder — verified by review.
3. The ADR explicitly resolves `ADR-012` §12.1 with a definitive, justified answer — verified by review against `ADR-012`.
4. The ADR does not alter `21_Security_And_Privacy` §6's threat-model boundary, and does not decide the cryptographic use of the key or cross-platform storage — verified by review of ADR-014 §9, §11.
5. `docs/tasks/SLICE-01_BACKLOG.md` §3 shows the `ODY-S01-004` row updated to a non-`Done` status with a determined Planning mode, and the `ODY-S01-005` row is byte-for-byte unchanged.
6. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass.
7. `git diff --name-status` against `main` shows only the four files listed in §5's Allowed paths.
8. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| None | — | Documentation-only task; no code paths exist to test | — |

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- Cross-read ADR-014 against `21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5–6 to confirm the principle is implemented, not altered, and the threat-model boundary is unchanged.
- Cross-read ADR-014 against `05_Persistence_Odyssey_VTT_v0.8.md` `PE-INV-010`/§30–31 to confirm no contradiction.
- Cross-read ADR-014 against `ADR-011` §1 п.13 and `ADR-012` §12.1 to confirm the forward-referenced boundary/open question are honored/resolved correctly.
- Cross-read ADR-014 against `ADR-009` to confirm the Windows-only platform justification is consistent with the actual MVP baseline.

### Required environments / profiles

- OS / architecture: Not applicable (documentation-only)
- Unity editor or Player profile: Not applicable
- Scripting backend: Not applicable
- Network topology or database fixture: Not applicable
- Other: None

### Validation not required by this task

- Build, EditMode/PlayMode tests, or Player smoke: not required — no code is touched by this task, matching the precedent set by `ODY-S01-001`–`003`.

## 11. Compatibility, migration, and rollback

Not applicable. This task produces a `Proposed` ADR and its task contract; it does not itself change any persisted format, schema, contract, protocol, package, or deployable artifact. Compatibility impact is assessed and recorded only when this ADR's content is implemented in a future task.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

Note: the ADR itself specifies future implementation will use `Advapi32.dll` (part of the Windows OS) via P/Invoke, not a third-party NuGet package — recorded here for traceability, not as a dependency introduced by this task.

## 13. Security, privacy, and hidden information

- Data classes handled: owner key material (a `Secret`-class datum per `ADR-010` §10 classification, inherited by `21_Security_And_Privacy` §3) is the entire subject of this ADR; no actual secret material is embedded in the ADR or this task contract, only its storage design.
- Trust boundaries: Windows OS-level `CurrentUser` boundary (protects between local Windows accounts on the same machine, not against malware running as the same user or OS/account compromise) — this ADR reaffirms, does not alter, `21_Security_And_Privacy` §6.
- Authorization / audience checks: Not applicable to this documentation task.
- Redaction requirements: ADR-014 explicitly forbids owner key material in `manifest.json`, `campaign.db`, `.odcamp`, backup/snapshot, and diagnostic bundles, consistent with `ADR-010`/`PE-INV-010`.
- Log-safe fields: ADR-014 §7 requires safe-error reporting (`ADR-004`) on owner-key absence, not raw exception detail.
- Abuse / malformed input limits: Not applicable.
- Security tests: None (no code).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: Per `PLANS.md` §1.2, this task is evaluated against both plausible triggers, not presumed by analogy to `ODY-S01-001`–`003`. **Schema/persisted-format trigger:** does *not* apply — this ADR introduces no new table, column, or `manifest.json` field; owner key material is deliberately kept out of every persisted campaign structure, so there is no schema change to point to. **Security trigger:** applies directly — `PLANS.md` §1.2 names "security" verbatim in its list of concerns whose introduction or change requires an ExecPlan ("affects authoritative state, persistence, networking, **security**, permissions, hidden information, redaction, diagnostics, time, or randomness"). This task's entire subject is a security-storage mechanism decision (which OS API custodies a `Secret`-class credential, and the explicit resolution of a related security open question, `ADR-012` §12.1). A Brief plan is disqualified: `PLANS.md` §1.1 requires that a Brief-eligible change "does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph" — while this task does not change a persisted format, it does bear directly on redaction/secret-handling design, which §1.1 also disqualifies from Brief plan. ExecPlan mode is therefore required via the security trigger specifically, not the schema trigger used to justify `ODY-S01-001`/`002`, nor the migration trigger used for `ODY-S01-003` — a genuinely distinct rationale, evaluated fresh.
- ExecPlan path: `docs/plans/completed/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md`
- Expected pull request count: 1 (single Draft PR covering ADR authoring; a second PR will later record owner acceptance and status/backlog closure, mirroring the `ODY-S01-001`–`003` pattern).
- Milestone or sequencing constraints: Independent of `ODY-S01-001`–`003` per `SLICE-01_BACKLOG.md` §5; no blocking predecessor. Closes the last of the four ADRs required for `SLICE-01_BACKLOG.md` §2's prerequisite-backlog exit criteria once `Accepted`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` (new), `docs/tasks/SLICE-01_BACKLOG.md` (`ODY-S01-004` row only).
- Documents that must not change: `ADR-011`/`ADR-012`/`ADR-013`, `ODY-S01-001`–`003` task/ExecPlan (already `completed/`), `ODY-S01-005` backlog row, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`, `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` itself (quoted, not edited), anything else under `Documentation/`.
- Application version change: No — this task does not touch `Odyssey.*` code or `BuildIdentity`.
- Schema / format / contract / protocol / ruleset version change: None — ADR-014 is `Proposed`, not implemented; no schema is created in code by this task, and none is introduced by the ADR's content itself.
- Documentation version changes: ADR-014 is created at v1.0, `Proposed`. No other document's version changes.
- Changelog or release-note requirement: None — pre-implementation ADR, consistent with the `ADR-011`–`013` precedent.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass. (None applicable — documentation-only.)
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable. (Not applicable — see §11.)
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [x] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` — new ADR, authored at `Proposed`, reviewed and accepted by product owner as-is, Status moved to `Accepted` with acceptance recorded in §17 Нормативное действие (date 2026-08-20, no content changes).
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` §12.1 — pointer-only note added: the "Backup encryption at rest" open question is now closed by `ADR-014` §8. No other content in `ADR-012` changed.
- `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §12 — reviewed; neither open item (§12.1 SQLite provider library, §12.2 `CampaignPublicId`) is directly resolved by `ADR-014`, so `ADR-011` was left untouched, per closure instructions.
- `docs/tasks/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md` (this file) — moved to `docs/tasks/completed/` as part of formal closure.
- `docs/plans/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md` — governing ExecPlan, moved to `docs/plans/completed/` with final progress-log entry recorded.
- `docs/tasks/SLICE-01_BACKLOG.md` — `ODY-S01-004` row updated `In Review (ADR Proposed, pending owner acceptance)` → `Done (ADR-014 Accepted)`.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed` (authoring PR #25, 2026-08-20) |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-00x`/`TC-CI-0xx` checks passed, `Repository policy check passed.` (authoring PR #25, 2026-08-20) |
| `.\scripts\verify-format.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |
| `.\scripts\check-repository-policy.ps1` (closure) | Passed | Re-run for closure diff — see closure PR evidence |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `ADR-014` created with `Status: Proposed`, all normative content per §7 present — confirmed by review during authoring PR #25. |
| AC-2 | Passed | ADR-014 §4 names Windows Credential Manager, `CurrentUser` scope, with explicit justification, not an abstract placeholder. |
| AC-3 | Passed | ADR-014 §8 explicitly resolves `ADR-012` §12.1 with a definitive "no backup encryption in MVP" — now also reflected as a pointer note in `ADR-012` §12.1 itself. |
| AC-4 | Passed | ADR-014 §9 confirms `21_Security_And_Privacy` §6 is unaltered; §11 excludes cryptographic key usage and cross-platform storage. |
| AC-5 | Passed | `SLICE-01_BACKLOG.md` `ODY-S01-004` row updated to `Done (ADR-014 Accepted)`; `ODY-S01-005` row unchanged, confirmed via diff-scope check. |
| AC-6 | Passed | `verify-format.ps1` and `check-repository-policy.ps1` both passed (authoring and closure runs). |
| AC-7 | Passed | `git diff --name-status` against `main` limited to `ADR-014`, `ADR-012` §12.1 pointer note, task/plan files (`active`→`completed` move), and `SLICE-01_BACKLOG.md`. |
| AC-8 | Passed | Draft PR #25 opened, all 4 required CI checks green (`repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance`); remained Draft through formal closure — not moved to Ready without separate confirmation. |

## 18. Blockers, risks, and open decisions

- Blocker: none. `ODY-S01-004` is independent of `ODY-S01-001`–`003` per `SLICE-01_BACKLOG.md` §5, and all three are `Accepted`/`Done` regardless.
- Open decision (deliberate, not a blocker): ADR-014 §12 records two new open questions (§12.2 UX/application-level behavior on owner-key absence; §12.3 source of the non-secret `TargetName` reference identifier) and notes §12.1 (SQLite provider library) as inherited/not directly relevant. These are intentional non-decisions, not omissions.
- Risk: none identified beyond the standard risk that the owner may request content changes during review before `Accepted`, matching the `ODY-S01-001`–`003` precedent (all accepted as-is).
- Closure (2026-08-20): Product owner reviewed `ADR-014` and accepted it as-is, no content changes requested. `ADR-014` Status `Proposed` → `Accepted`; acceptance recorded in the ADR's own §17 Нормативное действие. `ADR-012` §12.1 (Backup encryption at rest) updated with a pointer note that it is now closed by `ADR-014` §8 — no other `ADR-012` content changed. `ADR-011` §12 reviewed and left untouched — neither open item there is directly resolved by `ADR-014`. Task Status moved to `Done`, moved to `docs/tasks/completed/`. This ExecPlan moved to `docs/plans/completed/`. `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-004` row moved to `Done (ADR-014 Accepted)`.
