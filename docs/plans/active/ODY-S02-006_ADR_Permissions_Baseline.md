# ODY-S02-006 — ADR: Permissions Baseline

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s02-006-adr-permissions-baseline`
**Pull request:** Draft — [#43](https://github.com/odyssey-services/Odyssey_VTT/pull/43)
**Last updated:** 2026-08-25 UTC

## 1. Purpose and user-visible outcome

Fixes the first concrete role model (Main GM/Player/Observer), where host-side read/action checks happen, how redacted per-connection projections are computed, and how a revoked permission removes data from a client's current state — closing the exact point `ADR-017` §12 left open. No user-visible product behavior changes yet; this unblocks `ODY-S02-007` (`SP-04`), which will empirically test this contract.

## 2. Task contract

- Goal: produce `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` (Accepted), fixing the baseline role model and its integration with already-accepted mechanisms (`ADR-002`'s command pipeline, `ADR-004`'s `SafeReasonCode`, `ADR-017`'s delta operations) — explicitly excluding delegation, arbitrary `PermissionKey`/`Scope`, and `AssistantGM`.
- Acceptance criteria: see task contract §9.
- Requirement IDs: `SLICE-02` (prerequisites), backlog `ODY-S02-006`.
- In scope: ADR-019, its task contract, this ExecPlan, `SLICE-02_BACKLOG.md`'s `ODY-S02-006` row.
- Out of scope: any code, any delegation/scope system, `AssistantGM`, `SP-04` itself.
- Required authorities: `07_Permissions_Odyssey_VTT_v0.7.md` (full), `17_Roadmap...` §11.3, `ADR-017` §1/§11/§12, `ADR-018` §4, `ADR-004` (`SafeReasonCode`), `ADR-002` (command pipeline), `SLICE-02_BACKLOG.md` §4.
- Required validation commands: `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `dotnet build`/`dotnet test` on `DotNet/Odyssey.Core.sln` (expected unaffected — no code changes).

## 3. Current state

- `ODY-S02-001`–`005` (`ADR-015`–`018`) are all merged to `main` — confirmed by `git log` before branching.
- `07_Permissions_Odyssey_VTT_v0.7.md` (3101 lines) documents a full, general permissions model far beyond MVP scope; roadmap §11.3 names exactly 6 items as this stage's baseline.
- `ADR-017` §12 explicitly deferred visibility/redaction mechanics to this task; §1 point 8 already fixed "reconnect redaction always by current permissions" (not reopened here); §11 assumed payload is "already redacted before reaching the transport layer" without defining where.
- `07_Permissions` §37.2's own documented pipeline (`Membership → PermissionDecision → VisibilityPolicy → ClientProjection`) and §19.1's "host builds a separate projection per connection" already establish the single-authoritative-state-plus-per-connection-filter mechanism this task formalizes.
- `ADR-017` §5's `Operations[]` already includes `RemoveFromProjection` and `RemoveCapability` — exactly the operations needed to implement "revoked permission removes data," confirmed by re-reading `ADR-017`.
- `ADR-004`'s existing `SafeReasonCode` enum already contains all five values `PERM-INV-012` requires (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) — confirmed against the already-established value list recorded earlier this session.
- `ADR-002` mentions "permissions" only as a generic pipeline step, not a concrete role model — confirmed by `grep`, matching `ODY-S02-000`'s earlier finding.

## 4. Proposed approach

Accept 8 of `PERM-INV-001`–`012` (as a full rule or, for `003`, as a principle only) that are decidable without a delegation/scope/ownership system; defer the other 4 with named reasons. Fix action check inside `ADR-002`'s existing pipeline step, and read/visibility check at `ProjectionSnapshot`/`ProjectionDeltaBatch` construction time in the Application layer. Fix redaction as a single-authoritative-state-plus-per-connection-filter mechanism, matching `06_Networking...` §37.2's own pipeline. Fix revocation-removes-data as direct reuse of `ADR-017`'s existing delta operations, introducing no new mechanism. See `ADR-019` itself for the full reasoning.

## 5. Milestones

### M1 — ADR-019 written, baseline subset justified, ADR-017 integration point closed

- [x] `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` written, mirroring `ADR-015`–`018`'s format.
- [x] §4 gives the exact `PERM-INV-*` subset with per-item justification for inclusion/deferral.
- [x] §6 fixes the two check points (action, visibility).
- [x] §7 fixes the redaction mechanism.
- [x] §8 fixes revocation-removes-data as reuse of `ADR-017`'s existing operations.
- [x] §9 confirms no new `SafeReasonCode` is needed.

### M2 — Task contract and backlog row complete

- [ ] `docs/tasks/active/ODY-S02-006_ADR_Permissions_Baseline.md` written, all 18 sections.
- [ ] `SLICE-02_BACKLOG.md`'s `ODY-S02-006` row updated (status/planning mode only).
- [ ] Validation run and recorded.
- [ ] Draft PR opened, CI green.

## 6. Progress log

- 2026-08-25 — Preflight confirmed `ADR-015`–`018` all merged to `main`; branched cleanly.
- 2026-08-25 — Read `07_Permissions_Odyssey_VTT_v0.7.md` in full (headers scanned, then §3, §6–10, §33–37 read in depth); read roadmap §11.3's exact baseline list; re-read `ADR-017` §12/§1 point 8/§11/§15.2 and `ADR-018` §4; confirmed `ADR-002`'s generic-only permissions mention.
- 2026-08-25 — Decided the exact `PERM-INV-*` subset and confirmed `AssistantGM` is not part of roadmap §11.3's list.
- 2026-08-25 — `ADR-019` written.

## 7. Decisions

- 2026-08-25 — Decision: baseline includes only `MainGM`/`Player`/`Observer`, not `AssistantGM`. Rationale: roadmap §11.3 names exactly "Main GM; Player; Observer" — three roles, not `07_Permissions` §6.1's four `BaseRoleKind` values. Authority: `17_Roadmap...` §11.3; `ADR-019` §5, §14.1.
- 2026-08-25 — Decision: 8 of 12 `PERM-INV-*` invariants accepted (`001`, `002`, `003`-as-principle, `005`, `006`, `010`, `011`, `012`); `004`, `007`, `008`, `009` deferred. Rationale: the accepted set is exactly what's needed for a role-only (no override/scope/ownership) permission model; the deferred set each requires a subsystem (Allow/Deny override resolution, ownership/control model, delegation) not named by roadmap §11.3. Authority: `ADR-019` §4.
- 2026-08-25 — Decision: revocation-removes-data reuses `ADR-017`'s existing `RemoveFromProjection`/`RemoveCapability` delta operations; no new mechanism is introduced. Rationale: both operations already exist in `ADR-017` §5's `Operations[]`, specifically suited to this case; a parallel mechanism would create two sources of truth for "what the client should forget," contradicting `ADR-017` §15.3's own rejected-alternative reasoning against bypassing the delta protocol. Authority: `ADR-017` §5; `ADR-019` §8, §14.3.
- 2026-08-25 — Decision: no new `SafeReasonCode` value is introduced; the five needed by `PERM-INV-012` already exist in `ADR-004`. Rationale: confirmed directly against the already-established enum; introducing duplicates would violate `ADR-004`'s single-vocabulary principle. Authority: `ADR-004`; `ADR-019` §9, §14.4.

## 8. Discoveries and deviations

- Discovery: roadmap §11.3's own baseline list names only three roles ("Main GM; Player; Observer"), not `07_Permissions` §6.1's four `BaseRoleKind` values (which include `AssistantGM`) — resolved by explicitly excluding `AssistantGM` from this ADR's baseline, not silently including it by copying the product document's fuller list.
- Discovery: `ADR-017`'s already-accepted `Operations[]` vocabulary (`RemoveFromProjection`, `RemoveCapability`) and `ADR-004`'s already-accepted `SafeReasonCode` vocabulary both turned out to already fully cover this ADR's mechanical needs — no gap requiring new machinery was found, simplifying the decision to "reuse, don't invent" throughout.

## 9. Validation and acceptance evidence

Recorded in the task contract's §17 once the full validation suite is run.

## 10. Recovery and rollback

Not applicable — this task introduces no code, no persisted schema, no migration. Reverting this task's commits removes the ADR and its task contract with no compatibility or data-loss risk, since no production code depends on it yet.

## 11. Open questions and blockers

- `ADR-019` §10 lists everything explicitly deferred (delegation, `AssistantGM`, ownership/control, arbitrary `PermissionKey`/`Scope`, temporary permissions, `CampaignUserGroup`, field-level audience) — all future scope, not blockers for this task's own closure.
- No blockers for this task itself.

## 12. Outcome and follow-up

Pending — this plan is updated to `Completed` once the task contract's remaining acceptance items are confirmed and the Draft PR is opened with green CI.
