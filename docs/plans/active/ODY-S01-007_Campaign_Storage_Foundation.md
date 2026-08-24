# ODY-S01-007 — Campaign Storage Foundation Implementation

**Status:** In Progress  
**Owner:** Codex  
**Branch:** `feat/ody-s01-007-campaign-storage-foundation`  
**Pull request:** Not yet opened  
**Last updated:** 2026-08-24

## 1. Purpose and user-visible outcome

When this plan is complete, Odyssey VTT can create and reopen a local campaign on disk: a physical folder tree, a PRAGMA-profiled `campaign.db`, an atomically-written `manifest.json`, and `CampaignId`/`CampaignPublicId` identity — all reachable through the `ICampaignRepository` Application port, implemented by `SqliteCampaignRepository` in `Odyssey.Persistence`, backed by the `ADR-011` v1.1-mandated `Microsoft.Data.Sqlite` provider library, verified compatible with the actual Windows IL2CPP Unity Player build target before any of this code was written.

No Scene/Token model, saving pipeline, migration, backup, or export exists yet — those are `ODY-S01-008`–`012`.

## 2. Task contract

Governing task: `docs/tasks/active/ODY-S01-007_Campaign_Storage_Foundation.md`.

- Goal: implement campaign Create/Open per that task's §1.
- Acceptance criteria: that task's §9, AC-1 through AC-10.
- Requirement IDs: `SLICE-01` (implementation), backlog `ODY-S01-007`.
- In scope: `CampaignPublicId`/`Uuid7` (Domain); `CampaignManifest`/`ICampaignRepository`/`CampaignHandle`/`PersistenceFailures` (Application); `SqliteCampaignRepository` (Persistence); new `Odyssey.Persistence`/`Odyssey.Tests.Persistence` projects; registry updates; the mandatory IL2CPP preflight.
- Out of scope: Scene/Token, saving pipeline, migration, backup, export, owner key storage (excluded from the whole revision).
- Required authorities: `ADR-011` v1.0/v1.1, `ADR-001`, `ADR-003`, `ADR-004`, `ADR-006`, `ADR-008`, `ADR-009`.
- Required validation commands: `restore.ps1`, `verify-format.ps1`, `verify-test-structure.ps1`, `test-fast.ps1`, `check-repository-policy.ps1`, `verify-repository.ps1`.

## 3. Current state

### Verified facts

- `SLICE-01_IMPLEMENTATION_BACKLOG.md` is on `main`; `ODY-S01-007` row was `Draft`.
- All four `SLICE-01` prerequisite ADRs are `Accepted`.
- No Persistence bridge project, `ICampaignRepository`, or campaign storage code existed before this task.

### Assumptions

- None.

## 4. Proposed approach

Perform the mandatory IL2CPP compatibility preflight first, as a throwaway, fully-cleaned-up verification, before writing any of this task's actual deliverable code — if it had failed, this plan would have stopped at that point and reported only the blocker. Since it passed cleanly, proceed to implement the campaign storage vertical: Domain identifier additions (kept pure, no direct wall-clock reads, per `ADR-008`'s forbidden-global-API scan which covers every Core module package including the newly added `com.odyssey.persistence`), an Application-layer port with an explicit ADR-003-compliant manifest codec, and a Persistence-layer SQLite implementation using the `ADR-011` v1.1-mandated provider library. Add the first real `Odyssey.Persistence`/`Odyssey.Tests.Persistence` .NET projects (narrowly un-blocking two pre-existing repository guards that intentionally rejected them until this exact vertical slice), and register the resulting `ErrorCode`s/`TestCaseId`s in the existing repository-policy-checked registries.

## 5. Milestones

### M1 — IL2CPP compatibility preflight (mandatory stop condition)

- [x] `Microsoft.Data.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` vendored as a throwaway Unity plugin.
- [x] Windows x64 IL2CPP Player built successfully (`BuildResult.Succeeded`, 0 errors).
- [x] Built Player run headless; PRAGMA/INSERT/SELECT smoke returned `PASS`, logged and written to a result file.
- [x] All preflight scaffolding deleted; unintended Unity project-setting side effects reverted; working tree confirmed clean before branching for this task's real deliverable.

### M2 — Domain/Application/Persistence implementation

- [x] `CampaignPublicId`, `Uuid7`, `CampaignId.NewId(UtcInstant)`/`CampaignPublicId.NewId(UtcInstant)` added to `Odyssey.Domain`.
- [x] `CampaignManifest`/`CampaignManifestV1Codec`/`CampaignSettings`/`ICampaignRepository`/`CreateCampaignRequest`/`CampaignHandle`/`PersistenceFailures` added to `Odyssey.Application`.
- [x] `SqliteCampaignRepository` implemented in `Odyssey.Persistence`, using `IWallClock` (not a direct global-clock call) for all timestamps.
- [x] Four `ErrorCode`s added and registered in `docs/errors/ERROR_CODES.md`.

### M3 — Bridge/test project wiring and registry updates

- [x] `DotNet/Projects/Odyssey.Persistence.csproj` and `DotNet/Tests/Odyssey.Tests.Persistence/` created, added to `DotNet/Odyssey.Core.sln`.
- [x] `DotNet/Tests/Odyssey.Tests.Contracts/ProjectContractTests.cs` and `scripts/verify-test-structure.ps1` updated to un-block the now-legitimate Persistence bridge/test projects (Networking guard left untouched).
- [x] Four `TC-PERSIST-*` test case IDs registered in `Tests/Metadata/test-catalog.json`, cross-referenced from `ERROR_CODES.md`.
- [x] `THIRD_PARTY_NOTICES.md` updated to reflect the first real production reference.

### M4 — Validation and evidence

- [x] `dotnet build`/`dotnet test DotNet/Odyssey.Core.sln` green (99/99).
- [ ] All six required validation scripts (`restore.ps1` through `verify-repository.ps1`) pass with real recorded results.
- [ ] Diff-scope check confirms only the expected files changed.
- [ ] Draft PR opened; CI green on all required checks; remains Draft.

## 6. Progress log

- 2026-08-24 UTC - Confirmed `ODY-S01-006` merged (`SLICE-01_IMPLEMENTATION_BACKLOG.md` on `main`, `ODY-S01-007` row `Draft`) and all four `SLICE-01` ADRs `Accepted`. Performed the mandatory IL2CPP compatibility preflight per this task's own instruction, before writing any deliverable code: verified via `ADR-001`'s dependency matrix (`Unity Client → Persistence` permitted) that Persistence code runs inside the Unity Client process and is subject to `ADR-006` dual-compilation and `ADR-009`'s mandatory IL2CPP validation (explicitly not satisfied by a Mono-only pass, per `ADR-009`'s own pitfall list). Downloaded and vendored `Microsoft.Data.Sqlite` 9.0.10 + `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 (managed + native win-x64 `e_sqlite3.dll`) as a throwaway Unity plugin, built a real Windows x64 IL2CPP Player via Unity 6000.4.0f1 batchmode (`BuildResult.Succeeded`, 0 errors), and ran the built `.exe` headless — it applied the exact `ADR-011` §7.1 PRAGMA profile, read back `journal_mode=wal`, and round-tripped a `CREATE TABLE`/`INSERT`/`SELECT`, logging `PASS`. No compatibility issue found. Deleted all preflight scaffolding and reverted unintended Unity project-setting side effects (scripting backend change, HDRP asset YAML re-serialization) before branching for the real task.
- 2026-08-24 UTC - Implemented `CampaignPublicId`/`Uuid7`/`NewId(UtcInstant)` in `Odyssey.Domain`, `CampaignManifest`/`CampaignManifestV1Codec`/`CampaignSettings`/`ICampaignRepository`/`CampaignHandle`/`PersistenceFailures` in `Odyssey.Application`, and `SqliteCampaignRepository` in `Odyssey.Persistence`. Discovered and fixed two build-time issues: `ContractType.Parse` rejects underscores (renamed the manifest contract type from `campaign_manifest` to `campaignmanifest`), and `File.Move(string,string,bool)` is unavailable on `netstandard2.1` (replaced with delete-then-move). Discovered, at `dotnet test` time, that `scripts/verify-test-structure.ps1`'s forbidden-global-API scan flagged direct `DateTimeOffset.UtcNow` use in both the new Domain identifier generator and the new Persistence repository; refactored both to route timestamps through the existing `IWallClock` port (`SqliteCampaignRepository` now takes `IWallClock` via constructor; `CampaignId.NewId`/`CampaignPublicId.NewId` became pure functions of an explicit `UtcInstant`, preserving `Odyssey.Domain`'s zero-dependency purity since `IWallClock` lives in `Odyssey.Application`).
- 2026-08-24 UTC - Created `DotNet/Projects/Odyssey.Persistence.csproj` and `DotNet/Tests/Odyssey.Tests.Persistence/` (11 tests), added both to `DotNet/Odyssey.Core.sln`. Discovered and fixed two pre-existing repository guards (`ProjectContractTests.cs`, `verify-test-structure.ps1`) that intentionally blocked `Odyssey.Persistence.csproj`/`Odyssey.Tests.Persistence` from existing "too early" — updated both narrowly to allow Persistence (this exact vertical slice, per `ADR-006` §24) while leaving the `Odyssey.Networking` guard untouched. Registered four new `ErrorCode`s in `docs/errors/ERROR_CODES.md` and four `TC-PERSIST-*` test case IDs in `Tests/Metadata/test-catalog.json`. Fixed CRLF line-ending issues introduced by an earlier scripted edit pass (`verify-format.ps1` caught them). Full `dotnet test DotNet/Odyssey.Core.sln`: 99/99 passed, including the previously-failing `RepositoryStructurePassesArchitectureGuard`.

## 7. Decisions

- 2026-08-24 — Decision: `CampaignId.NewId()`/`CampaignPublicId.NewId()` accept an explicit `UtcInstant` parameter rather than reading wall-clock time internally. Rationale: `Odyssey.Domain` has zero dependencies (`ADR-001` §5 matrix), so it cannot reference `Odyssey.Application`'s `IWallClock` port; making the generator a pure function of caller-supplied time keeps Domain pure while still routing the actual wall-clock read, at the Persistence call site, through the approved `IWallClock` port rather than a forbidden direct global-clock call. Authority: `ADR-001` §5 dependency matrix; `ADR-008`; `scripts/verify-test-structure.ps1`'s forbidden-global-API scan (empirically discovered, not merely inferred from ADR text).
- 2026-08-24 — Decision: minimal `ADR-011` §8.2 system tables are created with bare-presence-level columns only, not their eventual full contract. Rationale: `ADR-011` §8.2 itself explicitly defers full DDL to "реализующая задача и... последующими ADR" (`ADR-012` for `DomainEvents`/`AggregateRevisions`, `ADR-013` for `SchemaHistory`/`MigrationRecords`); inventing a complete, speculative DDL now for tables this task does not exercise would risk conflicting with those later tasks' own authoritative decisions. Authority: `ADR-011` §8.2's own text.
- 2026-08-24 — Decision: `campaign.lock` concurrent-access locking is not implemented in this task. Rationale: not named in this task's explicit scope list; no scenario in this task's own deliverable (single-process Create/Open/Close) requires it yet. Authority: task's own originating instruction (silent on locking); deferred as an explicit, recorded non-goal, not an oversight.

## 8. Discoveries and deviations

- Discovered mid-implementation (not anticipated at task start): two pre-existing repository guards (`ProjectContractTests.cs`, `verify-test-structure.ps1`) actively blocked the `Odyssey.Persistence` bridge/test projects from existing. These were guards intentionally placed by earlier tasks (`ODY-S00-003`-era) to prevent premature creation before `SLICE-01`'s vertical slice arrived — exactly now. Updated both narrowly, leaving the parallel `Odyssey.Networking` guard untouched since Networking remains Stage 3 scope.
- Discovered mid-implementation: `scripts/verify-test-structure.ps1`'s forbidden-global-API scan applies to `com.odyssey.persistence` (it was already registered in `$modulePackages`, anticipating this task), which required the `IWallClock`-routing refactor described in §7. This was not anticipated in the original task contract draft and was corrected in place once `dotnet test` surfaced it, rather than silently working around the check.
- No deviation from the mandatory IL2CPP preflight instruction: it was performed first, exactly as instructed, before any of this milestone's implementation code was written.

## 9. Validation and acceptance evidence

See the governing task's §17 for the authoritative record. Summary: IL2CPP preflight passed; `dotnet test DotNet/Odyssey.Core.sln` 99/99; remaining wrapped validation scripts to be recorded in full before this plan's M4 is checked off.

## 10. Recovery and rollback

Not applicable in the data-loss sense (no production campaign data exists yet). If this task's approach needs to be reverted, revert its commits; no other task has yet built on top of `ICampaignRepository`/`SqliteCampaignRepository`.

## 11. Open questions and blockers

- No blockers remaining. The one designated stop-condition (IL2CPP incompatibility) did not occur.
- `campaign.lock` concurrent-access locking (§7 decision) remains an open, deliberately deferred item for a future task, not resolved here.

## 12. Outcome and follow-up

Current outcome: campaign Create/Open/Close implemented and passing 99/99 in the full solution test suite, including the previously-blocking architecture guard. Remaining before this plan's completion: full recorded validation-script run, diff-scope check, commit, push, and Draft PR.

Next action: complete §17's remaining validation rows, perform the diff-scope check, commit, push, and open a Draft PR — then `ODY-S01-008` (Scene and Token Minimal Model) may begin, depending on this task's `Create`/`Open` primitives.
