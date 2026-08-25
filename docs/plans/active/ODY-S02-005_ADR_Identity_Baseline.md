# ODY-S02-005 — ADR: Identity Baseline

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-005-adr-identity-baseline`
**Pull request:** Not opened
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

Fixes a stable, provider-independent `UserId` contract, an approved mock/dev identity boundary for testing, and two checkable secret-handling rules (JWT never in campaign state, service-role key never on client) — while honestly flagging what genuinely cannot be decided without the still-missing `18_Account_And_Identity.md`. Unblocks `ODY-S02-006` (Permissions Baseline), which needs a fixed identity contract to design against.

## 2. Task contract

- Goal: produce `docs/adr/ADR-018_Identity_Baseline_v1.0.md` (Accepted, within the scope decidable from existing sources), fixing `UserId` semantics, dev identity, the mock-vs-real Supabase Auth boundary for `SLICE-02`, and the two secret-handling rules — with explicit open questions where `18_Account_And_Identity.md`'s absence genuinely blocks a decision.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (prerequisites), backlog `ODY-S02-005`.
- In scope: ADR-018, its task contract, this ExecPlan, `SLICE-02_BACKLOG.md`'s `ODY-S02-005` row.
- Out of scope: any code, any permission/role model (`ODY-S02-006`), any production Supabase Auth integration, any invented content for `18_Account_And_Identity.md`.
- Required authorities: `06_Networking_and_Session_Sync` §6, `21_Security_And_Privacy` (full), `ADR-010` §10, `ADR-011` §9.1, `ADR-014` (structural reference), `ADR-017` §3.3/§4 (`UserId`/`AudienceUserId`, not redefined), `SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` (expected unaffected — no code changes).

## 3. Current state

- `ODY-S02-001`–`004` (`ADR-015`–`017`) are all merged to `main` — confirmed by `git log` before branching.
- `Documentation/18_Account_And_Identity.md` reconfirmed absent by search, matching `ODY-S02-000`'s earlier finding.
- `21_Security_And_Privacy` §7.1 itself stubs out "Auth, tokens и account identity redaction," explicitly deferring to the same missing document plus `06_Networking...`/`07_Permissions` — confirmed by `Read`.
- `Odyssey.Domain.Identity.UserId` already exists in code (prefix `user_`, canonical hex, no `NewId()` factory, comment calls it "externally assigned") — confirmed by `grep`; this task does not change it, only clarifies its intended semantics.
- `SLICE-02_BACKLOG.md` §4's `ODY-S02-005` boundary (`UserId`, dev identity, mock-vs-real boundary, JWT rule, service-role-key rule) is fully decidable from already-available sources without `18_Account_And_Identity.md`'s content — confirmed by cross-checking each item against `06_Networking...` §6, `21_Security_And_Privacy` §3–6, and existing precedent ADRs.

## 4. Proposed approach

Decide everything the task's own boundary requires from already-accepted sources and precedent (the `ADR-011` §9.1 domain-identifier principle applied to `UserId`; the `ADR-015` mock-transport precedent applied to dev identity; the `PE-INV-010`/`ADR-014` secret-boundary pattern extended to JWT and service-role keys), and explicitly enumerate — not invent — the items that genuinely require `18_Account_And_Identity.md` (email confirmation, account recovery, multi-device behavior, full auth/token redaction table). See `ADR-018` itself for the full reasoning.

## 5. Milestones

### M1 — ADR-018 written, decidable items resolved, genuine gaps flagged not invented

- [x] `docs/adr/ADR-018_Identity_Baseline_v1.0.md` written, mirroring `ADR-015`–`017`'s format.
- [x] `UserId` semantics fixed without changing existing code.
- [x] Dev identity and mock-vs-real Supabase boundary decided and justified.
- [x] JWT-never-in-campaign-state rule concretized as an extension of `PE-INV-010`.
- [x] Service-role-key rule formulated as a checkable architectural rule.
- [x] §12 lists the genuine open questions blocked by `18_Account_And_Identity.md`'s absence, not invented content.

### M2 — Task contract and backlog row complete

- [ ] `docs/tasks/active/ODY-S02-005_ADR_Identity_Baseline.md` written, all 18 sections.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-005` row updated (status/planning mode only).
- [ ] Validation run and recorded.
- [ ] Draft PR opened, CI green.

## 6. Progress log

- 2026-08-25 — Preflight confirmed `ADR-015`–`017` all merged to `main`; branched cleanly.
- 2026-08-25 — Reconfirmed `18_Account_And_Identity.md`'s absence; read `06_Networking...` §6 and `21_Security_And_Privacy` in full; confirmed §7.1's own explicit stub.
- 2026-08-25 — Checked existing `UserId` code to avoid contradicting it.
- 2026-08-25 — `ADR-018` written.

## 7. Decisions

- 2026-08-25 — Decision: `UserId` is not equal to any auth provider's own ID; a separate, updatable mapping exists between them. Rationale: preserves historical attribution in `campaign.db`/`DomainEvents` across a future provider change, consistent with the append-only principle (`ADR-012`) and the established "domain IDs are application-issued, not borrowed" pattern (`ADR-011` §9.1). Authority: `ADR-011` §9.1; `ADR-018` §4.
- 2026-08-25 — Decision: an explicit mock/dev identity boundary is sufficient for `SLICE-02`; real Supabase Auth integration is deferred to a future implementation task. Rationale: `SLICE-02_BACKLOG.md` §1/§2 itself scopes this revision as prerequisites-only (no real auth code required by any of its 7 exit criteria), and `18_Account_And_Identity.md`'s absence makes a responsible real-integration design impossible right now. Authority: `SLICE-02_BACKLOG.md` §1/§2; `ADR-018` §5.
- 2026-08-25 — Decision: rather than deferring the entire ADR until `18_Account_And_Identity.md` exists, decide everything the task's own fixed boundary allows now, and explicitly flag only the genuinely blocked items as open questions. Rationale: the fixed `ODY-S02-005` boundary (`SLICE-02_BACKLOG.md` §4) is fully decidable without that document; deferring the whole ADR would needlessly block `ODY-S02-006`, which only needs the `UserId` contract this ADR can already provide. Authority: `ADR-018` §13.3.

## 8. Discoveries and deviations

- Discovery: `21_Security_And_Privacy` §7.1 itself explicitly stubs the exact auth/token redaction content this task might otherwise have been tempted to write in full — confirming the gap is real and documented by the source material itself, not merely an absence this task is inferring. Used directly as evidence for the open-questions section rather than worked around.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Not applicable — this task introduces no code, no persisted schema, no migration. Reverting this task's commits removes the ADR and its task contract with no compatibility or data-loss risk, since no production code depends on it yet.

## 11. Open questions and blockers

- `ADR-018` §12 lists 4 genuine open questions, all blocked by `18_Account_And_Identity.md`'s absence except §12.4 (an implementation parameter deferred to the future real-integration task) — not blockers for this task's own closure, since the task's own fixed boundary does not require resolving them.
- No blockers for this task itself.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
