# ODY-S01-004 — ADR-014 Owner Key Storage Baseline Authored and Proposed

**Status:** In Progress  
**Owner:** Codex  
**Branch:** `feat/ody-s01-004-adr-owner-key-storage-baseline`  
**Pull request:** Not yet opened  
**Last updated:** 2026-08-20

## 1. Purpose and user-visible outcome

When this plan is complete, `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` exists as a `Proposed` normative decision defining the concrete Windows mechanism (Windows Credential Manager, `CurrentUser` scope) for storing campaign owner key material, the stored entity's format, rotation policy, loss/unavailability behavior, and an explicit resolution of `ADR-012` §12.1 (backup encryption at rest) — implementing, without altering, the principle already confirmed in `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5. Once accepted, this closes the last of the four ADRs required for the `SLICE-01` prerequisite backlog's exit criteria.

No implementation code is delivered. The observable outcome is a reviewable ADR proposal, not a running feature.

## 2. Task contract

Governing task: `docs/tasks/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md`.

- Goal: Author `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` per that task's section 1.
- Acceptance criteria: That task's section 9, AC-1 through AC-8.
- Requirement IDs: `SLICE-01`, roadmap section 10.2, backlog `ODY-S01-004`.
- In scope: ADR content (concrete Windows mechanism, stored-entity format, rotation, loss behavior, `ADR-012` §12.1 resolution, threat-model boundary confirmation, reinstall/machine-change/export behavior); this ExecPlan; backlog status update.
- Out of scope: Any implementation code; campaign format/snapshot/migration content beyond resolving `ADR-012` §12.1; the cryptographic use of the key; UX/application-level behavior on key absence; cross-platform storage; marking the ADR `Accepted`.
- Required authorities: `Documentation/21_Security_And_Privacy_Odyssey_VTT_v0.1.md` §5–6; `05_Persistence_Odyssey_VTT_v0.8.md` `PE-INV-010`, §30–31; `ADR-011` §1 п.13, `ADR-012` §12.1, `ADR-009`, `ADR-004`; `docs/tasks/SLICE-01_BACKLOG.md`.
- Required validation commands: `scripts/verify-format.ps1`, `scripts/check-repository-policy.ps1`.

## 3. Current state

### Verified facts

- `ADR-011`, `ADR-012`, `ADR-013` are all `Accepted` on `main` (merge commit `bf99bf4`); `ODY-S01-001`–`003` backlog rows are all `Done`.
- `docs/adr/` contains ADR-001 through ADR-013; `ADR-014` is the next free number.
- `ADR-009` confirms Windows Standalone x86-64 as the sole MVP production platform.
- No owner-key-storage implementation code exists anywhere in the repository.

### Assumptions

- None.

## 4. Proposed approach

Author `ADR-014` directly from `21_Security_And_Privacy` §5–6 (quoting §5 verbatim as the binding principle, per the same approved-summary pattern already used for `ODY-S01-000`) and `05_Persistence` `PE-INV-010`/§30–31, translating the deferred "concrete mechanism" decision into a binding ADR choice: Windows Credential Manager (generic credential) over a self-managed DPAPI file, justified by survivability across app reinstall and avoidance of custom encryption/IV management. Fix `CurrentUser` scope, justified directly against the already-accepted `21_Security_And_Privacy` §6 threat-model boundary (protection between local users, not against compromise of the same account). Resolve `ADR-012` §12.1 explicitly and definitively (no backup encryption in MVP), reasoning from the already-accepted `05_Persistence` §30.1 / `21_Security_And_Privacy` §4.1 decision not to encrypt the campaign container at all. Explicitly confirm, not silently alter, the §6 threat-model boundary. Describe persistence-layer behavior for reinstall/machine-change/export, while explicitly flagging UX/application-level behavior on key absence as `[OPEN]` rather than inventing networking/account-layer behavior not yet designed.

No code changes are made; no module ownership or dependency direction changes as a result of this plan.

## 5. Milestones

### M1 — `ADR-014` drafted and internally consistent with its sources

- [x] `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` created, `Status: Proposed`.
- [x] Content covers the concrete Windows mechanism, stored-entity format, rotation, loss behavior, and an explicit `ADR-012` §12.1 resolution as binding decisions.
- [x] `21_Security_And_Privacy` §6 threat-model boundary explicitly confirmed as unaltered.
- [x] Cryptographic key usage, UX/application-level absence behavior, and cross-platform storage explicitly excluded/flagged `[OPEN]`, not invented.

### M2 — Task/backlog evidence and validation recorded

- [x] `docs/tasks/active/ODY-S01-004_ADR_Owner_Key_Storage_Baseline.md` completion evidence section drafted.
- [ ] `docs/tasks/SLICE-01_BACKLOG.md` `ODY-S01-004` status updated (Draft → In Review, Planning mode `ExecPlan`).
- [ ] `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1` both pass with real recorded results.

### M3 — Draft PR opened for owner review

- [ ] Draft PR opened; CI green on all required checks.
- [ ] PR not moved to Ready for Review without separate confirmation.

### M4 — ADR accepted and task closed (future, separate confirmation required)

- [ ] Product owner reviews and decides on `ADR-014` (accept as-is, or requests changes).
- [ ] If accepted: Status `Proposed` → `Accepted`; task and this ExecPlan moved to `completed/`; backlog status updated to `Done`. This closes all four ADRs required by `SLICE-01_BACKLOG.md` §2's exit criteria.

## 6. Progress log

- 2026-08-20 UTC - Confirmed `ODY-S01-003` closure (PR #24) merged into `main`; verified `ADR-011`/`ADR-012`/`ADR-013` all `Accepted` and `ODY-S01-001`–`003` backlog rows all `Done` directly against `main` before branching. Read `21_Security_And_Privacy_Odyssey_VTT_v0.1.md` in full (§5 principle, §6 threat-model boundary, §4.1/§4.3 local storage context), `05_Persistence` `PE-INV-010`/§30–31, `ADR-011` §1 п.13, `ADR-012` §12.1, and `ADR-009` Windows platform baseline. Authored `docs/adr/ADR-014_Owner_Key_Storage_Baseline_v1.0.md` covering the Windows Credential Manager mechanism choice (`CurrentUser` scope), stored-entity format, MVP rotation policy (no automatic rotation, explicit regenerate only), loss/unavailability behavior (unrecoverable at persistence layer, safe-error reporting), an explicit resolution of `ADR-012` §12.1 (no backup encryption in MVP), explicit confirmation that §6 is unaltered, persistence-layer reinstall/machine-change/export behavior, and explicit exclusions/open questions. Created the governing task contract and this ExecPlan.

## 7. Decisions

- 2026-08-20 — Decision: Use `ExecPlan` planning mode, justified via the "security" trigger in `PLANS.md` §1.2, not the schema/persisted-format trigger used for `ODY-S01-001`/`002` or the migration trigger used for `ODY-S01-003`. Rationale: this ADR introduces no new persisted schema/table/manifest field (owner key material is deliberately kept out of every campaign file), so the schema trigger does not apply; `PLANS.md` §1.2 separately and explicitly names "security" as a standalone trigger, and this task's entire subject is a security-storage mechanism decision. Authority: `PLANS.md` §1.2, evaluated fresh against both plausible triggers per task instruction, not presumed by analogy.
- 2026-08-20 — Decision: Choose Windows Credential Manager (generic credential, `CurrentUser` scope) over a self-managed DPAPI file. Rationale: Credential Manager is a system-level store independent of application files, survives app reinstall without requiring the application to manage encryption/IV/file-location logic itself; internally still DPAPI-backed, so no security capability is lost versus a raw DPAPI file, only implementation risk is reduced. Authority: `21_Security_And_Privacy` §5 (defers mechanism to this ADR); no third-party dependency introduced (`Advapi32.dll` is part of Windows).
- 2026-08-20 — Decision: Resolve `ADR-012` §12.1 (backup encryption at rest) with a definitive "no" for MVP, not a re-deferral. Rationale: `05_Persistence` §30.1 / `21_Security_And_Privacy` §4.1 already independently establish that MVP does not encrypt the campaign container at all; owner key material is a small opaque secret, not designed or sized to serve as a full-database encryption key, so there is no technical dependency requiring backup encryption to wait on this ADR beyond formally closing the question. Authority: `05_Persistence_Odyssey_VTT_v0.8.md` §30.1; `ADR-012` §12.1's own text, which explicitly defers to this ADR.
- 2026-08-20 — Decision: Do not design a cross-platform secure-storage abstraction in this ADR. Rationale: MVP has exactly one production platform (Windows Standalone x86-64, `ADR-009`); introducing an abstraction without a second real implementation to validate it against would be premature and risks guessing wrong about a future platform's actual API shape. Authority: `ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`; task instruction to justify explicitly rather than block Windows baseline for a hypothetical future platform.

## 8. Discoveries and deviations

- None so far. `21_Security_And_Privacy` §5–6, `05_Persistence` `PE-INV-010`/§30–31, and `ADR-011`/`ADR-012`/`ADR-009` were internally consistent with each other and did not require reconciling conflicting guidance. No conflict was found between this ADR's content and `21_Security_And_Privacy` §6, so no explicit-conflict escalation was needed (the task instruction's contingency for that case did not trigger).

## 9. Validation and acceptance evidence

To be filled with real command output before this plan's M2/M3 milestones are checked off (see the governing task's section 17 for the authoritative record; this section will not duplicate it beyond a pointer once validation runs).

## 10. Recovery and rollback

Not applicable. This plan produces a documentation-only ADR proposal; no persisted state, migration, or runtime behavior is created. If `ADR-014` is rejected or requires material revision, the plan is updated in place (new decision entry, revised milestones) rather than abandoned, unless the owner directs a full restart.

## 11. Open questions and blockers

- No blockers remaining for this plan's M1 milestone.
- UX/application-level behavior when owner key material is absent on a new machine or after loss (ADR-014 §11.3/§12.2), and the source of the non-secret `TargetName` reference identifier (ADR-014 §12.3), remain open questions carried by the ADR itself, by design — not blockers to this plan's completion.

## 12. Outcome and follow-up

Current outcome: `ADR-014_Owner_Key_Storage_Baseline_v1.0.md` drafted at `Proposed`. Task contract and this ExecPlan created. Backlog update, validation, commit, push, and Draft PR remain to be executed (M2–M3).

Next action: run `verify-format.ps1` and `check-repository-policy.ps1`, update the `ODY-S01-004` backlog row, perform the diff-scope check, commit, push, and open a Draft PR — mirroring the `ODY-S01-001`–`003` workflow. Once accepted, all four ADRs required by `SLICE-01_BACKLOG.md` §2's exit criteria will be `Accepted`, leaving only `ODY-S01-005` (`SP-02` spike) to close the prerequisite backlog revision.
