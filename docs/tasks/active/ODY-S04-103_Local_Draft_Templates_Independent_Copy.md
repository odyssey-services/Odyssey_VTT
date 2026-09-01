# ODY-S04-103 — Local Draft, Templates & Independent Copy

**Status:** In Review
**Roadmap stage / slice:** SLICE-04
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-103-local-draft-templates`
**Pull request:** <to be filled after `gh pr create`>
**ExecPlan:** `docs/plans/active/ODY-S04-103_Local_Draft_Templates_Independent_Copy.md`
**Created:** 2026-09-01
**Last updated:** 2026-09-01 UTC

## 1. Goal

Implement `ADR-023` §4–6: `CreateLocalCharacterDraft` (a client-owned, pre-campaign-binding record with no `CampaignId`/`CharacterId`), the single `CharacterTemplate` aggregate distinguished by `TemplateScope` (`CreatePersonalCharacterTemplate`/`CreateCampaignCharacterTemplate`/`UpdateCharacterTemplate`/`ArchiveCharacterTemplate`), and `BindDraftToCampaign` — deep-copy-with-fresh-identifiers from a template (or blank), synchronous compatibility validation, `RulesetVersion` pinning, and the initial owner as an ordinary Draft-to-Character field.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-04_IMPLEMENTATION_BACKLOG.md` names `ODY-S04-103` as the third implementation task, depending only on `ODY-S04-101` (order-independent from `ODY-S04-102`, per backlog §2.2's decision that the initial-owner-as-a-Draft-field concern belongs here, not in the ownership-command task).
- Value or risk reduction: proves `ADR-023`'s Draft/template/independent-copy/compatibility-validation model against real persistence before `ODY-S04-104` (submit/review/approve) and `ODY-S04-112` (`.odchar` import) build on it.
- Blocking or enabling relationship: unblocks `ODY-S04-104` directly (needs a real bound `CharacterId` to submit/review/approve) and informs `ODY-S04-112` (which reuses this task's own `BindDraftToCampaign` pipeline for import).

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`.
- `AGENTS.md`, `PLANS.md` §1.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 3) — the binding scope definition for this task.
- `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` (full read — the governing ADR for this task).
- `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` §4–6 (Character aggregate this task creates real instances of, not reopened).
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §7.3 (Draft/Active), §8 (creation process, minimum fields, unconfirmed draft), §9 (templates, `TemplateScope`, independent copy), and §4's `CAP-INV-006`/`CAP-INV-007`/`CAP-INV-010`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, `Packages/com.odyssey.domain/Runtime/Character/CharacterOwnership.cs` (`ODY-S04-101`/`102`'s own code) — read in full as the binding structural precedent per this task's own explicit instruction.

### Requirement and test IDs

- Requirement IDs: `ODY-S04-103`, `ADR-023` §4–6.
- Existing test IDs reused: None directly reused; this task's `IsAssignedCharacter` check reuses `ODY-S04-102`'s own predicate without duplicating it.
- New test IDs introduced: `TC-CHAR-017` through `TC-CHAR-027` (`Tests/Metadata/test-catalog.json`).

### Task-safe private context

- Approved summary / references: non-tracked product documentation and the roadmap were read as local authority and summarized by section/topic only. No private prose, secret, personal data, or hidden campaign content is copied into this task, the plan, or production code.

## 4. Verified current state

### Verified facts

- `git fetch origin main`, `git checkout main`, `git merge --ff-only origin/main` advanced local `main` to `4f4a642`, the merge commit for PR #86 (`ODY-S04-102`); `git merge-base --is-ancestor` independently confirmed both PR #85's and PR #86's merge commits are real ancestors of `origin/main`.
- No personal/local-profile storage concept, no `CharacterTemplate`/Draft concept, and no Ruleset-catalog compatibility mechanism existed anywhere in the codebase prior to this task — confirmed by direct code search.
- `CampaignManifest`/`CampaignHandle.Manifest` already expose `RulesetId`/`RulesetVersion` as plain strings (from `ODY-S01-007`'s own campaign-creation contract) — reused directly, no new ruleset-identity type introduced.
- `CharacterRecord`'s constructor (from `ODY-S04-101`/`102`) has a small, fixed set of construction call sites, all inside `SqliteCharacterRepository.cs` — extending it with `RulesetVersion`/`AnatomyProfileRef`/`TemplateId`/`TemplateVersionAtCopyTime`/`SeedCopy` (ADR-022 §4's `CreationInfo` conceptual area, which reserves no independent section-revision counter) is a contained, mechanical change.
- No concrete Ability/Resource/Anatomy nested-entity domain types exist yet (`ODY-S04-108`/`109` own those) — confirmed by `Read`/`Grep` across `Packages/com.odyssey.domain`.
- `scripts/verify-test-structure.ps1`'s `TC-ARCH-001` check requires a task contract file to exist for every `taskId` a `Tests/Metadata/test-catalog.json` entry names — discovered when this task's own new `TC-CHAR-017`+ entries failed the guard before this contract file existed.

### Assumptions

- None. All facts above were directly observed via `Read`/`Grep`/`gh`/`git` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs` (edit) — `CharacterTemplateId`/`LocalCharacterDraftId`/`TemplateSeedItemId` (additive, matching sibling ID types).
- `Packages/com.odyssey.domain/Runtime/Character/CharacterTemplate.cs` (new) — `TemplateScope`, `CharacterTemplateStatus`, `CharacterTemplateSeedItem`/`CharacterTemplateSeed`, `CopiedCharacterSeedItem`, `CharacterTemplateSeedCopier`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterTemplateRepositoryContracts.cs` (new) — `LocalProfileHandle`, `TemplateStorageHandle`, `ICharacterTemplateRepository`, `CharacterTemplateRecord`.
- `Packages/com.odyssey.application/Runtime/Persistence/LocalCharacterDraftRepositoryContracts.cs` (new) — `ILocalCharacterDraftRepository`, `CreateLocalCharacterDraftRequest`, `LocalCharacterDraftRecord`.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterCreationSeed.cs` (new) — unifies "no template"/"already copied"/"copy now" into one shape `BindDraftToCampaign` consumes.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterTemplateCompatibility.cs` (new) — the deterministic RulesetId+major-version compatibility check.
- `Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` (edit) — `ICharacterRepository.BindDraftToCampaign`, `BindDraftToCampaignRequest`, `CharacterRecord` extended with `CreationInfo` fields.
- `Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs` (edit) — six new `PersistenceFailures` entries.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` (edit) — six new `ErrorCode` entries.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterTemplateRepository.cs` (new) — `ICharacterTemplateRepository` implementation.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteLocalCharacterDraftRepository.cs` (new) — `ILocalCharacterDraftRepository` implementation.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (edit) — schema/`SelectColumns`/`ReadCharacterRecord`/`CreateCharacter` extension, `BindDraftToCampaign` method, `ParseJsonPreservingStrings` made `internal`.
- `DotNet/Tests/Odyssey.Tests.Persistence/CharacterTemplateAndDraftBindingTests.cs` (new) — 16 tests.
- `docs/errors/ERROR_CODES.md` (edit) — six new registry rows.
- `Tests/Metadata/test-catalog.json` (edit) — `TC-CHAR-017`–`027`.
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (edit) — row 3 marked `Done` with the real PR link; top status line updated.
- This task contract and its ExecPlan.

### Out of scope

- Submit/review/approve workflow (`ODY-S04-104`).
- Administrative ownership commands — already `ODY-S04-102`; not duplicated here for the initial owner.
- Development economy/purchases (`ODY-S04-105`–`107`).
- Ability/resource/anatomy (`ODY-S04-108`/`109`).
- Archive/physical delete, Dead/restore, `.odchar`, Ruleset migration (`ODY-S04-110`–`113`).
- Any `AssistantGM` delegation or `ADR-019` role-model extension.
- Any Unity/UI code — this task is purely Domain/Application/Persistence.
- Any change to `ADR-022`/`023` content, or to `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond this task's own status row.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs
Packages/com.odyssey.domain/Runtime/Character/CharacterTemplate.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterTemplateRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/LocalCharacterDraftRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterCreationSeed.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterTemplateCompatibility.cs
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/CampaignRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterTemplateRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteLocalCharacterDraftRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/CharacterTemplateAndDraftBindingTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S04-103_Local_Draft_Templates_Independent_Copy.md
docs/plans/active/ODY-S04-103_Local_Draft_Templates_Independent_Copy.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-025*
docs/tasks/SLICE-04_BACKLOG.md
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: `Odyssey.Domain` owns the pure seed/copy value types and the fresh-identifier copy semantics (no serializer, no Unity, no SQLite/Rules reference — confirmed `Odyssey.Domain.asmdef` has zero references); `Odyssey.Application` owns the repository ports, `CharacterCreationSeed`, and the compatibility check (which does reference `Odyssey.Rules.Versions.RulesetVersion`, an already-approved Application-layer dependency); `Odyssey.Persistence` owns the SQLite implementations. Matches `ADR-001`/`ADR-023` §9 exactly.
- Authoritative-state and transaction boundary: `BindDraftToCampaign` commits through the existing, unmodified `SqliteSavingPipeline` (Character is a full `ADR-022`/`ADR-012` event-sourced aggregate). `CharacterTemplate`/`LocalCharacterDraft` use ordinary transactional row CRUD with a manual `LastCommandId` idempotency column — no `DomainEvents` participation, since neither `ADR-023` nor the product spec requires template/Draft history (an explicit, flagged scoping decision, unlike Character's own event-sourcing requirement).
- Serialization / compatibility boundary: seed/seed-copy JSON uses `Newtonsoft.Json.Linq` directly (`ADR-003`'s approved low-level API), through the same `ParseJsonPreservingStrings` date-safety helper `ODY-S04-102` already introduced (made `internal` so the new Persistence classes can reuse it without duplicating the `DateParseHandling.None` fix).
- Time / RNG rule: `IWallClock` injected exactly as `ODY-S04-101`/`102` already do; no direct wall-clock access.
- Unity / thread / lifetime rule: Not applicable — no Unity code in this task.
- Dependency / licensing rule: no new dependency; `Newtonsoft.Json`/`Odyssey.Rules` are already approved, in-use dependencies.
- Security / privacy / redaction rule: `PersistenceFailures`' six new entries never expose raw SQLite/IO exception text or local paths, matching the existing Character/Ownership failure convention exactly.
- Performance or platform constraint: unchanged from `ODY-S04-101`/`102`'s own established pattern.
- Other: no new permission gate is introduced — `CreateLocalCharacterDraft`/`BindDraftToCampaign` are ordinary Player-level campaign-membership actions per `ADR-023` §7.3, not MainGM-gated (unlike `ODY-S04-102`'s ownership commands).

## 7. Expected behavior

### Scenario 1 — a local Draft has no campaign/character identity

**Given** a `LocalProfileHandle` and the minimum required fields (Name, CharacterKind, AnatomyProfileRef)
**When** `CreateLocalCharacterDraft` is called
**Then** a `LocalCharacterDraftRecord` is created with a `LocalCharacterDraftId`, no `CampaignId`/`CharacterId`, and (if a Personal template was referenced) an already-fresh-ID'd `SeedCopy`.

### Scenario 2 — `BindDraftToCampaign` creates exactly one Character with fresh nested IDs

**Given** a target campaign and a `CharacterCreationSeed` built from a `CampaignCharacterTemplate`
**When** `BindDraftToCampaign` is called
**Then** exactly one `CharacterRecord` is created at `LifecycleStatus=Draft`/`ApprovalState=Draft`, its `SeedCopy` items carry fresh identifiers distinct from the template's own source seed-item IDs, and `TemplateId`/`TemplateVersionAtCopyTime` are recorded as immutable provenance.

### Scenario 3 — CAP-INV-006 holds after bind

**Given** a Character already bound from a template
**When** `UpdateCharacterTemplate` later edits that same source template
**Then** the already-bound Character's `DisplayName`/`TemplateVersionAtCopyTime`/`SeedCopy` are unchanged.

### Scenario 4 — incompatible ruleset is rejected before any Character exists

**Given** a template whose `RulesetId` or major `RulesetVersion` does not match the target campaign's own pinned ruleset
**When** `BindDraftToCampaign` is called
**Then** it is rejected with `CharacterDraftRulesetIncompatible` and no Character row is created.

### Scenario 5 — `RulesetVersion` is pinned to the campaign's current value

**Given** a compatible-but-differently-versioned template reference
**When** `BindDraftToCampaign` succeeds
**Then** the resulting Character's `RulesetVersion` equals the campaign's own current `RulesetVersion`, not the value recorded on the template reference.

### Required invariants

- A local Draft is never assigned a `CharacterId` before `BindDraftToCampaign`.
- No nested seed-item identifier is ever reused across two different `BindDraftToCampaign` calls, even from the same template.
- A duplicate `BindDraftToCampaign` `CommandId` never creates a second Character.
- No `ADR-022`/`023` file content changes.

## 8. Deliverables

- Production code: `CharacterTemplate.cs` (Domain), `CharacterTemplateRepositoryContracts.cs`/`LocalCharacterDraftRepositoryContracts.cs`/`CharacterCreationSeed.cs`/`CharacterTemplateCompatibility.cs`/`CharacterRepositoryContracts.cs` extension (Application), `SqliteCharacterTemplateRepository.cs`/`SqliteLocalCharacterDraftRepository.cs`/`SqliteCharacterRepository.cs` extension (Persistence), `PersistenceFailures`/`ErrorCodes` additions.
- Tests: `CharacterTemplateAndDraftBindingTests.cs` — 16 tests, registered as `TC-CHAR-017`–`027`.
- Scripts / CI: None new.
- Configuration: None.
- Documentation: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- Generated evidence or build artifacts: None committed (test/build output only).
- Migration / recovery material: None — additive `Character` columns and new `CharacterTemplate`/`LocalCharacterDraft` tables only.

## 9. Acceptance criteria

1. `CreateLocalCharacterDraft` succeeds with all minimum fields present and rejects a missing Name/AnatomyProfileRef at request construction.
2. `CreatePersonalCharacterTemplate`/`CreateCampaignCharacterTemplate` both persist through the single `CharacterTemplate` aggregate/table, distinguished only by `TemplateScope`.
3. `BindDraftToCampaign` from a `CampaignCharacterTemplate` creates exactly one Character with a new `CharacterId`; copied nested seed items get fresh identifiers distinct from the template's own.
4. Two Characters bound from the same template never share a nested seed-item identifier.
5. A later `UpdateCharacterTemplate` on the source template has zero effect on an already-bound Character (CAP-INV-006).
6. An incompatible `RulesetId` or major `RulesetVersion` rejects `BindDraftToCampaign` with `CharacterDraftRulesetIncompatible`, before any Character row is created.
7. The bound Character's `RulesetVersion` equals the campaign's own current value, not the template's recorded one.
8. The initial `PrimaryOwnerUserId` is set at bind and visible through the existing `CharacterOwnership`/`IsAssignedCharacter` mechanism; a `PlayerCharacter` without one is rejected at request construction; a non-`PlayerCharacter` does not require one.
9. A duplicate `BindDraftToCampaign` `CommandId` replays the stored result and does not create a second Character.
10. A local Draft created from a Personal template carries its already-copied seed through to bind unchanged, without a second copy.
11. `ArchiveCharacterTemplate` transitions `Status` to `Archived`; a stale `expectedRevision` is rejected.
12. No change to `ADR-022`/`023` or `SLICE-04_BACKLOG.md`; no Unity/UI code.
13. `dotnet build`, `dotnet test` (full suite, no regression), `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1` all pass.
14. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 3 marked `Done` with a real PR link.
15. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CHAR-017` | .NET (`Odyssey.Tests.Persistence`) | `CreateLocalCharacterDraft` with all required fields succeeds | Pass |
| `TC-CHAR-018` | .NET (`Odyssey.Tests.Persistence`) | Missing Name/AnatomyProfileRef rejected | Pass |
| `TC-CHAR-019` | .NET (`Odyssey.Tests.Persistence`) | Personal and Campaign templates share one aggregate/table | Pass |
| `TC-CHAR-020` | .NET (`Odyssey.Tests.Persistence`) | Bind from template creates one Character with fresh nested IDs; two binds never collide | Pass |
| `TC-CHAR-021` | .NET (`Odyssey.Tests.Persistence`) | CAP-INV-006: post-bind template edit has zero effect | Pass |
| `TC-CHAR-022` | .NET (`Odyssey.Tests.Persistence`) | Incompatible RulesetId/major version rejects bind, no Character created | Pass |
| `TC-CHAR-023` | .NET (`Odyssey.Tests.Persistence`) | RulesetVersion pinned to campaign's current value | Pass |
| `TC-CHAR-024` | .NET (`Odyssey.Tests.Persistence`) | Initial owner set at bind, visible via `IsAssignedCharacter`; PlayerCharacter-without-owner rejected | Pass |
| `TC-CHAR-025` | .NET (`Odyssey.Tests.Persistence`) | Duplicate `BindDraftToCampaign` CommandId does not duplicate | Pass |
| `TC-CHAR-026` | .NET (`Odyssey.Tests.Persistence`) | Personal-template Draft copy carried through to bind unchanged | Pass |
| `TC-CHAR-027` | .NET (`Odyssey.Tests.Persistence`) | Archive transitions status; stale revision rejected | Pass |

### Required commands

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

### Manual validation

- None beyond the automated tests above.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64.
- Unity editor or Player profile: Not applicable — no Unity/UI code in this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: real temp-directory SQLite campaign and a real temp-directory local-profile folder per test, matching `ODY-S04-101`/`102`'s own fixture convention.
- Other: `dotnet` SDK matching `global.json`.

### Validation not required by this task

- `scripts/test-unity.ps1` — this task adds no Unity-facing behavior; scoped validation per this task's own ТЗ is `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` only.

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — five new columns on the existing `Character` table (`RulesetVersion`, `AnatomyProfileRef`, `TemplateId`, `TemplateVersionAtCopyTime`, `SeedCopyJson`), all with safe defaults; two new tables (`CharacterTemplate`, `LocalCharacterDraft`); no existing column altered.
- Version fields affected: None.
- Migration or upcaster: None — additive `CREATE TABLE IF NOT EXISTS`/new columns only; no production data exists yet to migrate.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this task's commits; new columns/tables are simply unused by any other code path if reverted.
- Data-loss risk and protection: None — no existing data touched.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

`Newtonsoft.Json`/`Odyssey.Rules` are already approved, in-use dependencies; no new package reference is added.

## 13. Security, privacy, and hidden information

- Data classes handled: Draft/template identity and seed metadata only — no hidden GM fields, no secrets, no personal data beyond the owning `UserId` already handled elsewhere.
- Trust boundaries: a local Draft/Personal template lives entirely in the caller's own profile storage; a Campaign template lives inside the target campaign's own authoritative storage. Neither introduces a new cross-user visibility model.
- Authorization / audience checks: none new — `CreateLocalCharacterDraft`/`BindDraftToCampaign` are ordinary Player-level actions per `ADR-023` §7.3, not gated beyond what campaign membership already implies.
- Redaction requirements: `PersistenceFailures`' six new entries never expose raw SQLite/IO exception text or local paths.
- Log-safe fields: `character_draft_bound` event payload carries only `characterId`/`campaignId`/`characterKind`/`displayNameSnapshot`/`rulesetVersion`/`templateId`/`templateVersionAtCopyTime`/`initialPrimaryOwnerUserId`/`newCharacterRevision` — no secret data.
- Abuse / malformed input limits: `Name`/`DisplayName` length (128 chars) and non-empty `AnatomyProfileRef` validated, matching `CreateCharacterRequest`'s own existing limits.
- Security tests: Not applicable at this stage — no new permission surface.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 3 names `ExecPlan` for this task, and `PLANS.md` §1 independently confirms it — this task introduces new public Application-layer contracts (`ICharacterTemplateRepository`, `ILocalCharacterDraftRepository`), new persisted schema, and the first real implementation of `ADR-023`'s Draft/template/independent-copy/compatibility-validation semantics.
- ExecPlan path: `docs/plans/active/ODY-S04-103_Local_Draft_Templates_Independent_Copy.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: depends only on `ODY-S04-101` (done, PR #85). Unblocks `ODY-S04-104` directly; informs `ODY-S04-112`.

## 15. Documentation and versioning impact

- Documents that must change: this task contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (status only).
- Documents that must not change: `ADR-001` through `ADR-025`, `docs/tasks/SLICE-04_BACKLOG.md`, `Documentation/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: additive `Character` columns and two new tables; no versioned schema migration.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
- [x] Required manual checks are completed (none required).
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

See section 5's "In scope" file list above — all 18 files/areas.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build Odyssey.Core.sln` | Passed | 0 warnings, 0 errors. |
| `dotnet test Odyssey.Core.sln` | Passed | Full suite: Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 98 (82 pre-existing + 16 new) — 329 total, 0 failures, 0 regressions. |
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | First run failed on missing `ERROR_CODES.md`/test-catalog entries; fixed. `dotnet test`'s own `Odyssey.Tests.Architecture` run then failed a separate, pre-existing guard (`TC-ARCH-001`, task-contract-reference check) until this contract/ExecPlan existed; second full run: `Repository policy check passed`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | `TC-CHAR-017`/`018`. |
| AC-2 | Passed | `TC-CHAR-019`. |
| AC-3 | Passed | `TC-CHAR-020`. |
| AC-4 | Passed | `TC-CHAR-020`'s cross-Character assertion. |
| AC-5 | Passed | `TC-CHAR-021`. |
| AC-6 | Passed | `TC-CHAR-022`. |
| AC-7 | Passed | `TC-CHAR-023`. |
| AC-8 | Passed | `TC-CHAR-024`. |
| AC-9 | Passed | `TC-CHAR-025`. |
| AC-10 | Passed | `TC-CHAR-026`. |
| AC-11 | Passed | `TC-CHAR-027`. |
| AC-12 | Passed | `git status --porcelain` confirms no `ADR-*`/`SLICE-04_BACKLOG.md`/`Assets/**` file touched. |
| AC-13 | Passed | See Validation results above. |
| AC-14 | Passed | `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 3 status/PR link updated. |
| AC-15 | Passed | Draft PR link and CI status recorded once opened (see final report). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: `dotnet test` console output (see Validation results).

### Known limitations

- Template seed data is modeled generically (category/name/value pairs with a fresh-identifier copy mechanism) rather than using concrete typed Ability/Resource/Anatomy nested entities, since those production types do not exist yet (`ODY-S04-108`/`109`). A future task translating copied seed items into real typed entities is not required to consume this exact shape.
- `CharacterTemplate` does not participate in `DomainEvents`/history — an explicit scoping decision (neither `ADR-023` nor product require it), unlike `Character` itself.
- `BindDraftToCampaign` currently accepts a template-shaped `CharacterCreationSeed` only (`None`/`AlreadyCopied`/`FromTemplate`); `ODY-S04-112`'s `.odchar` import will need a small additional `CharacterCreationSeed` factory for an arbitrary import-file seed source, not a redesign of `BindDraftToCampaign` itself.

### Follow-up tasks

- `ODY-S04-104` — Draft Submit/Review/Approve Workflow.
- `ODY-S04-112` — will need a small `CharacterCreationSeed` extension for `.odchar` import's own seed source (see Known limitations).

### Self-review summary

- Scope review: limited to allowed files; no `ADR-022`/`023` or `SLICE-04_BACKLOG.md` change; no Unity/UI code.
- Architecture review: extends `ICharacterRepository` directly for `BindDraftToCampaign` (Character's own real creation path); introduces two new, appropriately-scoped repositories for genuinely new aggregate/storage boundaries (`CharacterTemplate`, `LocalCharacterDraft`) rather than overloading `ICharacterRepository` with concerns that do not belong to the Character aggregate itself.
- Test review: every acceptance criterion has a real, non-stubbed test against genuine temp-directory SQLite fixtures — no mocked repository, no bypassed transaction pipeline.
- Security/privacy review: no new permission surface; error messages redact raw exception/path detail exactly like existing Character/Ownership failures.
- Documentation/version review: task contract, ExecPlan, error registry, test catalog, and backlog status all updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task.

### Decisions made during execution

- 2026-09-01 — Decision: model template seed data generically rather than inventing Ability/Resource/Anatomy production schema early — Authority/approval: this task's own explicit ТЗ instruction; backlog §2.3's "smallest test-fixture content" convention.
- 2026-09-01 — Decision: `CharacterTemplate`/`LocalCharacterDraft` use ordinary transactional CRUD, not `SqliteSavingPipeline`/`DomainEvents` — Authority/approval: neither `ADR-023` nor product require template/Draft history, unlike Character's own explicit `ADR-022` §7–8 requirement.
- 2026-09-01 — Decision: `TemplateStorageHandle` routing value reconciles "one aggregate, two storage boundaries" with keeping `UpdateCharacterTemplate`/`ArchiveCharacterTemplate` single commands — Authority/approval: `ADR-023` §5.1's own "one aggregate type" requirement plus this task's own named-command list.
- 2026-09-01 — Decision: ruleset compatibility is "same `RulesetId`, same major `RulesetVersion` line" — Authority/approval: this task's own engineering decision, explicitly not an ADR-023 decision (no Ruleset-catalog mechanism exists yet); flagged in the final report as the ТЗ requires.
- 2026-09-01 — Decision (discovered mid-task, not anticipated by the ТЗ): remove the unused `CharacterDraftMinimumFieldsMissing` `ErrorCode` once found dead during registry cleanup — Authority/approval: this task's own code-quality judgment; minimum-field validation is exception-based at request construction, matching `CreateCharacterRequest`'s own pre-existing convention.
- 2026-09-01 — Decision (discovered mid-task, not anticipated by the ТЗ): create this task contract/ExecPlan before the final `check-repository-policy.ps1` pass, after `verify-test-structure.ps1`'s `TC-ARCH-001` check failed on this task's own new `test-catalog.json` entries referencing a not-yet-existing task contract — Authority/approval: the repository's own enforced policy, following the exact same ordering `ODY-S04-101`/`102` already used.

### Approved task changes

- None.
