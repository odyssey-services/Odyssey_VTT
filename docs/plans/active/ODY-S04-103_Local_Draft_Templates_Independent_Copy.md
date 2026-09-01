# ODY-S04-103 — Local Draft, Templates & Independent Copy

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-103-local-draft-templates`
**Pull request:** [#87](https://github.com/odyssey-services/Odyssey_VTT/pull/87)
**Last updated:** 2026-09-01 UTC

## 1. Purpose and user-visible outcome

Implement `ADR-023` §4–6: `CreateLocalCharacterDraft` (personal-profile boundary, no `CampaignId`/`CharacterId`), the single `CharacterTemplate` aggregate distinguished by `TemplateScope` (`CreatePersonalCharacterTemplate`/`CreateCampaignCharacterTemplate`/`UpdateCharacterTemplate`/`ArchiveCharacterTemplate`), and `BindDraftToCampaign` (deep-copy-with-fresh-identifiers, synchronous compatibility validation, `RulesetVersion` pinning, initial owner as an ordinary field). Third implementation task of `SLICE-04`.

## 2. Task contract

- Goal: extend the Character/Persistence layer with a local-Draft repository, a `CharacterTemplate` repository, and a `BindDraftToCampaign` method on `ICharacterRepository` that creates the one real, permanent `Character` aggregate instance ADR-023 §4.2 requires.
- Acceptance criteria: local Draft has no `CampaignId`/`CharacterId`; `CharacterTemplate` is one aggregate/table for both scopes; `BindDraftToCampaign` creates exactly one Character with fresh nested seed-item IDs; CAP-INV-006 holds (a later template edit never changes an already-bound Character); an incompatible ruleset is rejected before any Character row exists; `RulesetVersion` is pinned to the campaign's current version, not the template's; the initial owner is visible through the existing `CharacterOwnership`/`IsAssignedCharacter` mechanism; a duplicate `BindDraftToCampaign` `CommandId` does not create a second Character; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-022`/`023` content change.
- Requirement IDs: `ODY-S04-103`, `ADR-023` §4–6.
- In scope: `Odyssey.Domain.Character.CharacterTemplate`/`CharacterTemplateSeed`/`CharacterTemplateSeedCopier`, three new Domain ID types, `ICharacterTemplateRepository`/`ILocalCharacterDraftRepository` Application ports, `CharacterCreationSeed`/`CharacterTemplateCompatibility` Application helpers, `ICharacterRepository.BindDraftToCampaign` extension, `SqliteCharacterTemplateRepository`/`SqliteLocalCharacterDraftRepository` new Persistence classes, `SqliteCharacterRepository` schema/record extension, tests, error registry/test-catalog additions, backlog status update.
- Out of scope: submit/review/approve workflow (`ODY-S04-104`), administrative ownership commands (already `ODY-S04-102`, not duplicated here for the initial owner), development economy, ability/resource/anatomy, archive/delete/Ruleset migration, `AssistantGM`/delegation, any Unity/UI code, any `ADR-022`/`023` content change.
- Required authorities: `ADR-023` (full read), `ADR-022` §4–6 (Character aggregate this task creates instances of, not reopened), `10_Characters_And_Progression` §7.3/§8/§9 and its CAP-INV-006/007/010, `SLICE-04_IMPLEMENTATION_BACKLOG.md` §5–7, `ODY-S04-101`/`102`'s own `CharacterRepositoryContracts.cs`/`SqliteCharacterRepository.cs`/`CharacterOwnership.cs` as the binding structural precedent.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `4f4a642` (PR #86, `ODY-S04-102`), independently verified via `git merge-base --is-ancestor` for both PR #85 and PR #86.
- No "PermissionDecision"/role-lookup service and no "assigned character" duplication concern here -- this task adds no new permission gate (`Character.CreateDraft`/`BindDraftToCampaign` are Player-level per `ADR-023` §7.3, not MainGM-gated).
- No Ruleset-catalog/compatibility mechanism exists anywhere in the codebase (confirmed by search across `Odyssey.Rules` -- only `RulesetVersion`/`SemVerValue` exist) -- `CharacterTemplateCompatibility` is this task's own minimal, deterministic decision, not an ADR-023 decision.
- `CampaignManifest`/`CampaignHandle.Manifest` already expose `RulesetId`/`RulesetVersion` as plain strings -- reused directly for both compatibility validation and pinning, no new ruleset-identity type introduced.
- `CharacterRecord`'s constructor (from `ODY-S04-101`/`102`) has a small, fixed set of construction call sites inside `SqliteCharacterRepository.cs` -- extending it with `RulesetVersion`/`AnatomyProfileRef`/`TemplateId`/`TemplateVersionAtCopyTime`/`SeedCopy` (ADR-022 §4's `CreationInfo` conceptual area, which reserves no independent section revision) is a contained, mechanical change; `CreateCharacter` (ODY-S04-101's own bare skeleton path) is left otherwise unmodified with safe defaults for the new fields.
- No concrete Ability/Resource/Anatomy nested-entity domain types exist yet (`ODY-S04-108`/`109` own those) -- template seed data is modeled generically (category/name/value) rather than inventing that production schema early; this is an explicit, flagged scoping decision.
- `scripts/verify-test-structure.ps1`'s `TC-ARCH-001` check requires a task contract file to exist for every `taskId` referenced in `Tests/Metadata/test-catalog.json` -- discovered when this task's own new `TC-CHAR-017`+ entries (referencing `ODY-S04-103`) failed the guard before this task's own contract file existed. Not a new discovery in kind (the same registry discipline `ODY-S04-101`/`102` already hit for `ErrorCodes`), but this is the first time it was the *task contract* reference rather than the `ErrorCode`/`TC-*` registry that was missing.

Assumptions: none.

## 4. Proposed approach

- Domain: three new canonical ID types (`CharacterTemplateId`, `LocalCharacterDraftId`, `TemplateSeedItemId`) following `CharacterId`'s exact `Prefix + Uuid7.NewHex32(now)` pattern; `CharacterTemplateSeedItem`/`CharacterTemplateSeed`/`CopiedCharacterSeedItem`/`CharacterTemplateSeedCopier` implementing ADR-023 §5.3's deep-copy-with-fresh-identifiers mechanism as a pure function.
- Application: `LocalProfileHandle` (mirrors `CampaignHandle` for the personal-profile storage boundary) and `TemplateStorageHandle` (routes a template call to whichever root/scope it belongs to, keeping `UpdateCharacterTemplate`/`ArchiveCharacterTemplate` single, scope-agnostic commands as named); `CharacterCreationSeed` (unifies "no template" / "already copied at Draft time" / "copy now, at bind time" into one shape `BindDraftToCampaign` consumes without needing to know which); `CharacterTemplateCompatibility` (the deterministic RulesetId+major-version check).
- Persistence: `SqliteCharacterTemplateRepository` (one table, opened from either `campaign.db` or a new `local_profile.db` depending on scope, ordinary transactional CRUD -- no `DomainEvents` participation, since neither ADR nor product requires `CharacterTemplate` history); `SqliteLocalCharacterDraftRepository` (same `local_profile.db`, same ordinary-CRUD style, performing the Personal-template deep copy once at Draft-creation time); `SqliteCharacterRepository.BindDraftToCampaign` (new method, real `SqliteSavingPipeline`-backed Character creation, reusing the existing per-section-revision/event-journal machinery).
- Tests: minimum-field validation (Draft level, and PlayerCharacter-without-owner at Bind level), both template scopes sharing one aggregate, fresh-ID copy on bind (single and cross-Character), CAP-INV-006 (post-bind template edit has zero effect), incompatible-ruleset rejection (RulesetId and major-version), RulesetVersion pinning to the campaign's own current value, initial-owner visibility through the existing `IsAssignedCharacter`, duplicate-`CommandId` no-second-Character, Personal-template-at-Draft-time copy carried through unchanged at bind, and template archive/revision-conflict.

No Unity/UI code, no `ADR-022`/`023` content change, no new permission gate.

## 5. Milestones

### M1 — Domain/Application extension

- [x] Three new ID types in `DomainIdentity.cs`.
- [x] `CharacterTemplate.cs` (Domain): scope/status/seed/copy types and copier.
- [x] `CharacterTemplateRepositoryContracts.cs`/`LocalCharacterDraftRepositoryContracts.cs`/`CharacterCreationSeed.cs`/`CharacterTemplateCompatibility.cs` (Application).
- [x] `ICharacterRepository.BindDraftToCampaign` + `BindDraftToCampaignRequest`; `CharacterRecord` extended with `CreationInfo` fields.
- [x] `PersistenceFailures`/`ErrorCodes` additions.

### M2 — Persistence and tests

- [x] `SqliteCharacterTemplateRepository`/`SqliteLocalCharacterDraftRepository` (new).
- [x] `SqliteCharacterRepository` extended: schema, `SelectColumns`/`ReadCharacterRecord`, `CreateCharacter`'s safe defaults, `BindDraftToCampaign`.
- [x] 16 new tests in `CharacterTemplateAndDraftBindingTests.cs`, all passing.
- [x] `dotnet build`/`dotnet test` full suite green, no regression (313 -> 329).
- [x] Discovered and removed one dead `ErrorCode` (`CharacterDraftMinimumFieldsMissing`) that was registered but never actually thrown -- minimum-field validation is exception-based at request construction, matching `CreateCharacterRequest`'s own existing convention.

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [x] This task contract, created before `check-repository-policy.ps1`'s `verify-test-structure.ps1`/`TC-ARCH-001` check (which requires a task contract file per referenced `taskId`) could pass.
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [x] Commit, push, and open Draft PR — [#87](https://github.com/odyssey-services/Odyssey_VTT/pull/87).
- [ ] Record CI status.

## 6. Progress log

- 2026-09-01 -- Preflight confirmed both PR #85 and PR #86 merge commits are real ancestors of `origin/main` at `4f4a642`; created branch `feat/ody-s04-103-local-draft-templates`.
- 2026-09-01 -- Read `ADR-023` in full, `ADR-022` §4–6 (re-confirmed), product §7.3/§8/§9 and §4's CAP-INV-006/007/010, backlog §5–7, and `ODY-S04-101`/`102`'s own code in full.
- 2026-09-01 -- Confirmed via search: no Ruleset-catalog compatibility mechanism, no personal/local-profile storage concept, and no `CharacterTemplate`/Draft concept existed anywhere prior to this task.
- 2026-09-01 -- Implemented Domain/Application/Persistence extension; `dotnet build` passed on first attempt.
- 2026-09-01 -- Wrote and ran 16 new tests; all passed on first run against real SQLite fixtures.
- 2026-09-01 -- `check-repository-policy.ps1` first run failed on the two new `ErrorCode`s' (initially seven, later six after removing one dead one) missing registry/catalog entries -- fixed.
- 2026-09-01 -- While adding registry entries, noticed `CharacterDraftMinimumFieldsMissing` was declared but never thrown anywhere (minimum-field validation is exception-based at construction, not a `Result.Failure`) -- removed the unused `ErrorCode`/`PersistenceFailures` entry and its registry row rather than leaving dead code.
- 2026-09-01 -- Full `dotnet test` run then failed a *different*, pre-existing check: `Odyssey.Tests.Architecture`'s `RepositoryStructurePassesArchitectureGuard` (`verify-test-structure.ps1`'s `TC-ARCH-001`), because this task's new `test-catalog.json` entries reference `taskId: "ODY-S04-103"` and no task contract file yet existed for that ID -- fixed by writing this ExecPlan and the task contract before the final validation pass, in the same order `ODY-S04-101`/`102` already established.

## 7. Decisions

- 2026-09-01 -- Decision: use ExecPlan, per `SLICE-04_IMPLEMENTATION_BACKLOG.md`'s own row for this task and `PLANS.md` §1. Authority: `PLANS.md` §1, backlog row 3.
- 2026-09-01 -- Decision: model template seed data generically (category/name/value, fresh IDs via a new `TemplateSeedItemId` type) rather than inventing Ability/Resource/Anatomy production schema early. Authority: this task's own explicit ТЗ instruction not to invent mechanics it does not own; backlog §2.3's "smallest test-fixture content needed to prove its own mechanism" convention.
- 2026-09-01 -- Decision: `CharacterTemplate` (both scopes) uses ordinary transactional CRUD with a manual `LastCommandId` idempotency column, not `SqliteSavingPipeline`/`DomainEvents` participation. Authority: neither `ADR-023` nor the product spec requires `CharacterTemplate` history/event-sourcing (unlike `Character`, which `ADR-022` §7–8 explicitly does require); this task's own minimal-scope engineering decision, flagged here rather than silently assumed.
- 2026-09-01 -- Decision: `TemplateStorageHandle` (Application-layer routing value, not a new architectural concept) reconciles "one `CharacterTemplate` aggregate, two storage boundaries" with keeping `UpdateCharacterTemplate`/`ArchiveCharacterTemplate` single, scope-agnostic commands, exactly as this task's own ТЗ names them. Authority: `ADR-023` §5.1's "one aggregate type" requirement plus this task's own explicit named-command list.
- 2026-09-01 -- Decision: `CharacterCreationSeed`'s three-case factory (`None`/`AlreadyCopied`/`FromTemplate`) is this task's own design to reconcile ADR-023 §5.3's "copy happens at Draft-creation for a Personal template, or at Bind for a Campaign template" into one shape `BindDraftToCampaign` consumes uniformly, never re-copying an already-copied Personal-template seed. Authority: ADR-023 §5.3's own text; this task's own code-quality judgment for how to implement it without special-casing inside `BindDraftToCampaign` itself.
- 2026-09-01 -- Decision: ruleset compatibility is "same `RulesetId`, same major `RulesetVersion` line" (`CharacterTemplateCompatibility`), reusing the same "same major line is compatible" convention `CompatibilityRange` already applies elsewhere in this codebase for a different version pair. Authority: this task's own engineering decision, explicitly not an ADR-023 decision -- `ADR-023` §6.1 requires "a deterministic, rules-catalog-driven check" without fixing the concrete rule, and no Ruleset-catalog mechanism exists yet to consult instead.
- 2026-09-01 -- Decision: remove the unused `CharacterDraftMinimumFieldsMissing` `ErrorCode` once discovered dead, rather than wire a call site into it artificially. Authority: this task's own code-quality judgment -- minimum-field validation is exception-based at request construction (`CreateLocalCharacterDraftRequest`/`BindDraftToCampaignRequest` constructors), matching `CreateCharacterRequest`'s own pre-existing convention; a `Result`-typed error for a condition the constructor already prevents from ever reaching persistence would be genuinely dead code, not a defensible layered check.

## 8. Discoveries and deviations

- No pre-existing "assigned character"/permission-gate concern applies to this task -- `Character.CreateDraft`/`BindDraftToCampaign` are ordinary Player-level campaign-membership actions per `ADR-023` §7.3, not MainGM-gated, so no `actorIsMainGm`-style parameter was added here (unlike `ODY-S04-102`'s ownership commands).
- `verify-test-structure.ps1`'s `TC-ARCH-001` task-contract-reference check (discovered this task, not previously documented in this session's own prior summaries) requires a task contract file to exist for every `taskId` a `test-catalog.json` entry names -- fixed by creating this task's own contract/ExecPlan before the final validation pass, now noted here for the next task's own preflight awareness.
- One genuinely dead `ErrorCode` (`CharacterDraftMinimumFieldsMissing`) was declared and then found unused during registry cleanup -- removed rather than left in, consistent with this task's own no-dead-code standard.
- No open architectural question was found that `ADR-023`/`ADR-022` do not already answer.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 329/329 after this task's fixes (16 new tests), zero regression.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed after (a) adding the six new `ErrorCode`s' registry/catalog entries and removing the one dead one, and (b) creating this task's own contract/ExecPlan so `verify-test-structure.ps1`'s `TC-ARCH-001` task-contract-reference check passes.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR -- the new `Character` table columns are additive; the new `CharacterTemplate`/`LocalCharacterDraft` tables are new, unused by any other code path if reverted; no existing column altered.

## 11. Open questions and blockers

None. The ruleset-compatibility rule and the generic seed-item shape were both explicitly anticipated as open engineering decisions by this task's own ТЗ and resolved here, documented above and in the final report, not left ambiguous.

## 12. Outcome and follow-up

Draft PR: <to be filled after `gh pr create`>. CI pending. Unblocks `ODY-S04-104` (Draft submit/review/approve workflow, depends on `ODY-S04-103`'s own `BindDraftToCampaign`-created `CharacterId`) and informs `ODY-S04-112` (`.odchar` import, which will need a small extension to accept an arbitrary seed source rather than only a `CharacterTemplate` -- see final report).
