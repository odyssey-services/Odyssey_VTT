# ODY-S04-102 — Character Ownership: Primary Owner Assignment, Co-Owners & Control Grants

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-102-character-ownership`
**Pull request:** [#86](https://github.com/odyssey-services/Odyssey_VTT/pull/86)
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-025` §4's administrative ownership commands over an already-existing `Character` (`AssignPrimaryOwner`, `AddCharacterCoOwner`/`RemoveCharacterCoOwner`, `GrantPermanentCharacterControl`/`GrantTemporaryCharacterControl`/`RevokeCharacterControl`), all `Character.ManageOwnership`-gated (MainGM-only), using `ODY-S04-101`'s already-reserved `Ownership` section. Second implementation task of `SLICE-04`.

## 2. Task contract

- Goal: extend `ICharacterRepository`/`SqliteCharacterRepository` (not a parallel repository) with the six ownership/control commands, following the exact section-revision-gated, single-pipeline-transaction convention `ODY-S04-101` established for Identity/Presentation.
- Acceptance criteria: all six commands implemented and MainGM-gated; `AssignPrimaryOwner` requires a `ReasonCode` and produces an audit event without touching co-owner/control state; a stale `expectedOwnershipRevision` is rejected; Ownership edits don't false-conflict with Identity/Presentation edits; `CharacterOwnershipAssignment.IsAssignedCharacter` implemented and tested; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`025` content change.
- Requirement IDs: `ODY-S04-102`, `ADR-025` §4.
- In scope: `Odyssey.Domain.Character.CharacterOwnership`/`CharacterTemporaryControlGrant`/`CharacterOwnershipAssignment`, `ICharacterRepository` extension, `SqliteCharacterRepository` extension, `PersistenceFailures`/`ErrorCodes` additions, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: initial owner as a Draft field (`ODY-S04-103`), Draft/template/submit/review/approve, development economy, ability/resource/anatomy, archive/delete/Dead/restore/Ruleset migration, `AssistantGM`/role-model extension, any Unity/UI code, any `ADR-022`/`025` content change.
- Required authorities: `ADR-025` §4 (full read), `ADR-022` §5–6 (Ownership section, not reopened), `ADR-019` §5 (MainGM baseline, not redefined), `10_Characters_And_Progression` §19/§26–28, `ODY-S04-101`'s own `CharacterRepositoryContracts.cs`/`SqliteCharacterRepository.cs` as the binding structural precedent, `BoardMovementService.cs`/`DiceRollService.cs` for the existing `actorIsMainGm` permission-check convention.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` was fast-forwarded to `origin/main` at `09f4307` (PR #85, `ODY-S04-101`), independently verified via `git merge-base --is-ancestor`.
- No existing "PermissionDecision"/role-lookup service exists in the codebase — `BoardMovementService.CheckAuthorization`/`DiceRollService`'s MainGM-gated operations all use a caller-supplied `bool actorIsMainGm` on the request, checked first (`ADR-019`'s own accepted baseline simplification). This task follows the identical convention rather than inventing a permission-decision service.
- No "assigned character"/`IsAssignedCharacter`-style predicate exists anywhere in the codebase yet — `ADR-025` §4.3 itself is the first place this concept is architecturally fixed; this task is the first to implement it for real, not a case of reusing a pre-existing mechanism.
- Product §19's `CharacterOwnership` schema names `TemporaryControlGrants` but does not itself define an expiry-enforcement mechanism; `ADR-025` §4 does not decide one either — confirmed by re-reading both in full.
- `CharacterRecord` (from `ODY-S04-101`) has exactly 4 direct construction call sites, all inside `SqliteCharacterRepository.cs` — updating its constructor to add an `Ownership` field is a contained, mechanical change.
- The shared `DomainEvents` table has no `AggregateId` column (confirmed again, same as `ODY-S04-101`'s own finding) — `GetCharacterHistory`'s existing filter-by-payload approach already generalizes to the new ownership event types without any repository-level change beyond adding their type strings to the filtered list.

Assumptions: none.

## 4. Proposed approach

- Domain (`CharacterOwnership.cs`): `CharacterOwnership` (four fields per product §19), `CharacterTemporaryControlGrant` (`UserId`, `GrantedAt`, optional `ExpiresAt`), `CharacterOwnershipAssignment.IsAssignedCharacter` (pure predicate: primary owner, co-owner, permanent controller, or an unexpired temporary grant).
- "Temporary" control expiry (engineering decision, not an ADR decision): `ExpiresAt` is caller-supplied, stored provenance only; no background sweep/auto-revocation job. `IsAssignedCharacter` evaluates expiry lazily against a caller-supplied "now," so a lapsed grant simply stops counting the next time anything checks it — no state ever needs to be proactively cleaned up.
- Application: extend `ICharacterRepository`/`CharacterRecord` (add `Ownership` field) with the six commands; `AssignPrimaryOwner` gets its own dedicated method (mandatory reason); the remaining five share one private `MutateOwnership` helper (gate → load → revision check → caller-supplied pure mutation → commit) to avoid five near-identical repeats of the same sequence.
- Persistence: extend the existing `Character` table with `PrimaryOwnerUserId`/`CoOwnerUserIdsJson`/`PermanentControllerUserIdsJson`/`TemporaryControlGrantsJson` columns; JSON arrays via `Newtonsoft.Json.Linq`, matching `SqliteGameLogRepository`'s existing convention.
- Tests: reason-required rejection, MainGM-gate rejection (parameterized true/false), successful assignment with audit-event/no-silent-side-effect proof, stale-revision rejection, three-section (Identity+Ownership) no-false-conflict extension of `ODY-S04-101`'s own test, co-owner add/remove with de-duplication, permanent/temporary control grant/revoke cycles, and a direct `IsAssignedCharacter` test.

No Unity/UI code, no `ADR-022`/`025` content change, no role-model extension.

## 5. Milestones

### M1 — Domain/Application/Persistence extension

- [x] `CharacterOwnership.cs` (Domain).
- [x] `ICharacterRepository`/`CharacterRecord` extended (Application).
- [x] `PersistenceFailures`/`ErrorCodes` ownership entries.
- [x] `SqliteCharacterRepository` extended with six commands + shared `MutateOwnership` helper.

### M2 — Tests, a real bug found and fixed, and registry

- [x] 12 new tests added.
- [x] Real bug found during test execution (Newtonsoft's default date auto-parsing corrupting `UtcInstant` round-trips inside JSON arrays) — fixed with an explicit `JsonTextReader`/`DateParseHandling.None` helper, not worked around in the test.
- [x] A second, genuine test-design flaw found and fixed (attempting to construct an already-expired grant, which the domain constructor correctly rejects) — fixed the test's own scenario, not the domain validation.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [x] `dotnet build`/`dotnet test` full suite green, no regression.

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `.\scripts\check-repository-policy.ps1`.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [x] Commit, push, and open Draft PR — [#86](https://github.com/odyssey-services/Odyssey_VTT/pull/86).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 — Preflight confirmed PR #85's merge commit is a real ancestor of `origin/main` at `09f4307`; created branch `feat/ody-s04-102-character-ownership`.
- 2026-09-01 — Read `ADR-025` §4 in full (re-confirmed), `ADR-022` §5–6/`ADR-019` §5 (re-confirmed relevant sections), `10_Characters_And_Progression` §19/§26–28, `ODY-S04-101`'s own `CharacterRepositoryContracts.cs`/`SqliteCharacterRepository.cs` in full, and `BoardMovementService.cs`/`BoardContracts.cs`/`DiceRollService.cs` for the existing MainGM-gate convention.
- 2026-09-01 — Implemented Domain/Application/Persistence extension; `dotnet build` passed on first run.
- 2026-09-01 — First test run found a real bug (Newtonsoft date auto-parsing corrupting stored `UtcInstant` values read back from a JSON array) — fixed with an explicit `JsonTextReader`-based parse helper.
- 2026-09-01 — Second test run found the fix worked but one test's own scenario was invalid (tried to construct an already-past-expiry grant, correctly rejected by domain validation) — fixed the test.
- 2026-09-01 — Full suite green (313/313, no regression); `check-repository-policy.ps1` first run failed on the two new `ErrorCode`s' missing registry/catalog entries (same class of gap `ODY-S04-101` already hit); fixed, second run passed.
- 2026-09-01 — `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 2 marked `Done`.

## 7. Decisions

- 2026-09-01 — Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1. Authority: `PLANS.md` §1, backlog row 2.
- 2026-09-01 — Decision: reuse `BoardMovementService`/`DiceRollService`'s existing caller-supplied `bool actorIsMainGm` convention for the `Character.ManageOwnership` gate, not a new permission-decision service. Authority: this task's own explicit ТЗ instruction; `ADR-019`'s own accepted baseline simplification, already used consistently across the codebase.
- 2026-09-01 — Decision: "temporary" control expiry is enforced lazily, at `IsAssignedCharacter` evaluation time, against a caller-supplied current time — no background sweep, no auto-revocation event. Authority: this task's own engineering decision, explicitly not an ADR decision — `ADR-025` §4 and product §19 both leave the exact expiry mechanism open; flagged in the final report as required by the ТЗ.
- 2026-09-01 — Decision: extend `ICharacterRepository`/`SqliteCharacterRepository` directly rather than a second Character-focused repository. Authority: this task's own explicit ТЗ instruction and `ODY-S04-101`'s own established single-repository convention.
- 2026-09-01 — Decision: five of the six commands (all but `AssignPrimaryOwner`, which needs a mandatory-reason check) share one private `MutateOwnership` helper taking a pure mutation function, rather than five near-identical gate/load/check/commit blocks. Authority: this task's own code-quality judgment, consistent with the DRY discipline the rest of this codebase already follows (e.g. `SqliteSceneRepository`'s shared `ReadTokenRecord`).

## 8. Discoveries and deviations

- **Real bug found and fixed during test execution:** Newtonsoft.Json's default `JArray.Parse`/`JObject.Parse` auto-detects date-like strings and silently reformats them using .NET's culture-default `DateTimeOffset.ToString()` when read back, corrupting `UtcInstant`'s exact round-trip format for values stored inside a JSON array (the `TemporaryControlGrantsJson` column). `JsonLoadSettings` has no `DateParseHandling` property in the approved Newtonsoft.Json 13.0.2 (`ADR-003`) — fixed with an explicit `JsonTextReader` configured with `DateParseHandling.None`, applied to every JSON parse call site in this file (grants, user-ID lists, and event-history payloads) for consistency, not just the one that happened to fail first.
- A second, genuine test-design flaw (not a production bug): an early version of the `IsAssignedCharacter` test tried to directly construct an already-expired `CharacterTemporaryControlGrant` (`ExpiresAt` before `GrantedAt`), which the domain constructor correctly rejects as nonsensical. Fixed the test to grant with a real future expiry, then evaluate `IsAssignedCharacter` at a later "now" — the actually realistic "has since expired" scenario.
- `docs/errors/ERROR_CODES.md`/`Tests/Metadata/test-catalog.json` registry requirement (discovered by `ODY-S04-101`) recurred identically for this task's two new `ErrorCode`s — fixed the same way, no surprise this time.
- No open architectural question was found that `ADR-025`/`ADR-019` do not already answer.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 313/313, zero regression (12 new tests, `TC-CHAR-009`–`016`).
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed` (after adding the `ERROR_CODES.md`/test-catalog entries the first run's failure required).

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — the new `Character` table columns are additive (`CREATE TABLE IF NOT EXISTS` already covers this, since no production data exists yet); no existing column altered.

## 11. Open questions and blockers

None. No architectural question was found that `ADR-025`/`ADR-019` do not already answer. The "temporary control expiry mechanism" question was explicitly flagged by the ТЗ as potentially open — resolved here as an engineering (not architectural) decision, documented in section 7 and the final report, not left ambiguous.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks nothing new directly (`ODY-S04-103` already depended only on `ODY-S04-101`), but closes the second of 15 `SLICE-04` implementation tasks.
