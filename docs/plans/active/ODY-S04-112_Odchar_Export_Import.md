# ODY-S04-112 — `.odchar` Export & Import

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-112-odchar-export-import`
**Pull request:** TBD
**Last updated:** 2026-09-02 UTC

## 1. Purpose and user-visible outcome

Implement `ExportCharacter` (a redacted `.odchar` bundle per `ADR-026`) and `ImportCharacter` (a fresh local Draft via `ODY-S04-103`'s unmodified `BindDraftToCampaign`, `RulesetVersion` re-pinned per `ADR-025` §7.6) — a full export-then-import round trip that never carries `CharacterOwnership`/`CharacterId`/`CampaignId` across campaigns and preserves every mechanics/anatomy/resource value. Twelfth implementation task of `SLICE-04`.

## 2. Task contract

- Goal: implement both operations per `ADR-026` (file format/redaction) and `ADR-025` §7.6 (import Draft-creation/`RulesetVersion` pinning, reused unmodified), resolving the task contract's own open implementation questions (repository-port shape, bundle mechanics, exact test IDs) directly from those ADRs' text and this codebase's existing conventions.
- Acceptance criteria: per the task contract's own §9 (12 items) — see that document; summarized here as: `ExportCharacter` writes a `.odchar` directory bundle (`manifest.json`+`character.json`) whose `character.json` never contains `CharacterOwnership`/`CharacterId`/`CampaignId` and is byte-identical regardless of exporting actor's role; `ImportCharacter` creates a fresh `CharacterId`/Draft via `BindDraftToCampaign` unmodified, pins `RulesetVersion` to the target campaign, requires fresh approval, rejects an incompatible Ruleset via the same compatibility function `BindDraftToCampaign` already uses, and preserves every attribute/skill/ability/resource/anatomy value from the export.
- Requirement IDs: `ODY-S04-112`, `ADR-026` (all sections), `ADR-025` §7.6, `ADR-023` §5–6.
- In scope: two new Application-layer files (`CharacterExportContracts.cs`, `RedactCharacterForExport.cs`) plus `ICharacterRepository.ExportCharacter`/`ImportCharacter` extension in `CharacterRepositoryContracts.cs`; `SqliteCharacterRepository.cs` extension for the actual bundle file I/O and the new cross-section mechanics-apply step; tests; error registry/test-catalog additions; backlog status update.
- Out of scope: `.odchar` `FormatVersion` 2+, bundle compression (a plain directory is used — `ADR-026` §6/§11 explicitly defer this); any concrete GM-only/secret Character field (`ADR-026` §5/§10.2); `BindDraftToCampaign`'s own decision logic (reused unmodified); `ADR-024` `Reserved`/pending `AdvancementRecommendation` rows (never exported — see Decisions); Ruleset Migration (`ODY-S04-113`); any Unity/UI code; any change to `ADR-022`/`023`/`025`/`026` content or `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond the status row.
- Required authorities: `ADR-026` (full read), `ADR-025` §7.6 (full read), `ADR-023` §5–6 (full read), `ADR-022`'s `CharacterRecord` shape, `SLICE-04_IMPLEMENTATION_BACKLOG.md` row 12, `BindDraftToCampaign`/`CharacterCreationSeed`/`CharacterTemplateCompatibility` (`ODY-S04-103`) — read in full for this task.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `ODY-S04-111`'s own merge commit (PR #95), independently verified via `git merge-base --is-ancestor`.
- `ADR-026` is `Accepted` (2026-09-02); the task contract (`docs/tasks/active/ODY-S04-112_Odchar_Export_Import.md`) already exists, provided by the product owner.
- Direct inspection of `BindDraftToCampaign`'s own implementation reveals a significant existing gap: `CharacterCreationSeed.Items` (`CopiedCharacterSeedItem[]`) is stored ONLY as `SeedCopyJson` provenance — `BindDraftToCampaign` NEVER applies seed items to real typed `Attributes`/`Skills`/`Abilities`/`Resources`/`Anatomy`; a freshly bound Character always starts with all of these empty/null and `DevelopmentPool.Empty()`, regardless of the seed passed. This is confirmed intentional (`CharacterTemplateSeedItem`'s own doc comment: "a later task may translate copied items... it is not required to consume this shape at all") but means `ImportCharacter` cannot rely on `BindDraftToCampaign`/`CharacterCreationSeed` alone to satisfy `ADR-026` §9 item 4's round-trip-preservation requirement — a second, explicit step is needed after bind (section 4 below).
- `BindDraftToCampaign`'s Ruleset-compatibility gate (`CharacterTemplateCompatibility.IsCompatible`, a pure string-comparison function, no template-row DB lookup) only runs `if (request.Seed.TemplateId.HasValue)`. Import's seed has no real `CharacterTemplateId`, so routing it through that exact gate would require either minting a fake `CharacterTemplateId` (misleading provenance) or modifying `BindDraftToCampaign`/`CharacterCreationSeed`'s own shape (risking "unmodified" per `ADR-026` §8 rule 6). Resolved in section 4 below by calling the same compatibility function directly from `ImportCharacter`, before ever calling `BindDraftToCampaign` — zero changes to `ODY-S04-103`'s own code.
- `CharacterAbilityId`/`CharacterResourceId`/`PermanentModificationId` are minted GUIDs (`NewId(now)`); `BodyPartId` is a human-readable catalog-style string key (`Parse`), not minted per-instance. `AttributeValue`/`CharacterSkill` have no instance ID at all (keyed purely by `AttributeDefinitionId`/`SkillDefinitionId`).
- `DevelopmentPool.Reserved` has no counterpart anywhere on `CharacterRecord` connecting it to a specific pending `AdvancementRecommendationRecord` (a wholly separate, non-exported record/table) — carrying a nonzero `Reserved` value across import without the matching recommendation rows would be meaningless.

Assumptions: none beyond what is directly observed above.

## 4. Proposed approach

- **`.odchar` bundle mechanics:** a plain directory (per `ADR-026` §3.1/§6, deferring compression) containing `manifest.json` and `character.json` (no `portrait/`/`referenced-assets/` in this task — no portrait/asset content exists to export yet; the directory slots are simply not created, matching `ADR-026`'s own "optional" framing). Both files are `Newtonsoft.Json.Linq` `JObject`s serialized with `Formatting.Indented` for human-readability (a bundle is meant to be portable/inspectable), read back the same way on import.
- **Where the file I/O lives:** `Odyssey.Persistence`'s `SqliteCharacterRepository.ExportCharacter`/`ImportCharacter` read the `Character` row / write the new Draft's row (exactly as every other method in that file already does) AND read/write the bundle's two JSON files on disk — mirroring `SqliteBackupRepository`'s own precedent of file I/O living in the concrete Persistence implementation, not a separate layer. `Odyssey.Application` owns `RedactCharacterForExport` (a pure function, `CharacterRecord` + `ExportActorContext` → `CharacterExportPayload`, no I/O) and the DTOs (`CharacterExportManifest`/`CharacterExportPayload`/`ImportCharacterRequest`), in two new files (`CharacterExportContracts.cs`, `RedactCharacterForExport.cs`) under `Packages/com.odyssey.application/Runtime/Persistence/` — the same folder `CharacterCreationSeed.cs` already established as a legitimate new-file location for this port, even though the task's own "Allowed paths" block does not itemize every new file individually (matching `ODY-S04-103`'s own precedent of `CharacterCreationSeed.cs`/`CharacterTemplate.cs` being new files under an implied, not literally-itemized, scope).
- **`ExportCharacter` shape:** `ICharacterRepository.ExportCharacter(CampaignHandle campaign, CharacterId characterId, string bundleDirectoryPath, ExportActorContext actorContext, CorrelationId correlationId) -> Result<CharacterExportBundle>`. A read-only `ADR-002` §4.2 Query (no `CommandId`, no event, no transaction) — reads the `Character` row, calls `RedactCharacterForExport`, computes `ExportedByRole` (MainGM > Owner > Controller, reusing `CharacterOwnershipAssignment`'s existing role predicates — see Decisions), writes `manifest.json`/`character.json` to `bundleDirectoryPath`, and returns the in-memory `CharacterExportBundle` (manifest + payload) so a test can assert on it directly without re-parsing the files it also writes.
- **`RedactCharacterForExport`:** exactly `ADR-026` §3.2's pure function. Never serializes `CharacterOwnership`/`CharacterId`/`CampaignId` (checked directly by a test that inspects the written JSON's own raw text/keys). Per `ADR-026` §5, produces the FULL remaining payload today (no field withheld) for every actor — MainGM and the Character's own owner produce byte-identical `character.json`, proven by a direct test. The function's own single parameter list is `ADR-026` §8 rule 4's named extension point: a future GM-only/secret field is added as one more conditional inside this same function, never a second parallel filter.
- **`CharacterExportPayload`:** `CharacterKind`, `DisplayName`, `PortraitReference`, `AnatomyProfileRef`, `RulesetVersion` (the exported Character's own pinned value, per `ADR-026` §4's own text — used by `ImportCharacter` only as informational provenance, never re-pinned from), `DevelopmentPool.Earned`/`Spent` (never `Reserved` — see Decisions), `Attributes`/`Skills`/`Abilities`/`Resources`/`Anatomy` (typed export-shaped mirrors of the domain lists, sufficient to reconstruct each domain object with a fresh instance ID where one exists).
- **`CharacterExportManifest`:** `ADR-026` §4's four minimum fields (`FormatVersion="1.0"`, `ExportedAt`, `ExportedByRole`, `SourceRulesetVersion`) plus one additive field, `SourceRulesetId` (the exporting campaign's `Manifest.RulesetId`) — needed for the Ruleset-compatibility check on import, additive per `ADR-026` §4's own "minimum, additive-only going forward" rule.
- **`ImportCharacter`'s Ruleset-compatibility check:** `ImportCharacter` calls `CharacterTemplateCompatibility.IsCompatible(manifest.SourceRulesetId, manifest.SourceRulesetVersion, campaign.Manifest.RulesetId, campaign.Manifest.RulesetVersion)` — the exact same pure function `BindDraftToCampaign` itself calls — directly, BEFORE ever calling `BindDraftToCampaign`, and returns the same `PersistenceFailures.CharacterDraftRulesetIncompatible` failure on rejection. `BindDraftToCampaign` itself is then called with `CharacterCreationSeed.None()` (its compatibility gate never fires, since `Seed.TemplateId` is null — already checked above by `ImportCharacter` itself) — zero changes to `BindDraftToCampaign`/`CharacterCreationSeed`'s own code, fully satisfying `ADR-026` §8 rule 6/`ADR-025` §7.6's "reuses ADR-023's existing bind-time rejection... does not add a second, import-specific compatibility check" in effect (same function, same error code, same decision), while keeping `ODY-S04-103`'s own files completely untouched.
- **`ImportCharacter`'s mechanics/anatomy/resource reconstruction:** after `BindDraftToCampaign` successfully creates the fresh, empty Draft, a second, dedicated step (`ApplyImportedCharacterState`, its own `_pipeline.Execute` call with its own `CommandId`) directly sets `DevelopmentPool` (`Earned`/`Spent` from the payload, `Reserved` always `0`), `Attributes`/`Skills` (no instance ID to freshen — copied by value), `Abilities`/`Resources` (each minted a **fresh** `CharacterAbilityId`/`CharacterResourceId`, never reusing the exporting campaign's own instance ID — the same CAP-INV-006 spirit `ADR-023` §5.3 already applies to template-copied nested identifiers), and `Anatomy` (`BodyPartId`/`PermanentModificationId` — `BodyPartId` is a catalog-style string key, kept as-is exactly like `ReplaceAnatomyProfile`'s own whole-list-replace convention; `PermanentModificationId` is minted fresh). Bumps `MechanicsRevision`/`AttributeValuesRevision`/`CharacterSkillsRevision`/`CharacterAbilitiesRevision`/`CharacterResourcesRevision`/`CharacterAnatomyRevision` each exactly once if the corresponding payload section is non-empty, producing one `odyssey.persistence.character_import_state_applied` forward event (no compensating-event machinery — an ordinary creation-time fact, not a correction). This is the same "one dedicated cross-section method" precedent `RestoreDeadCharacter`(`ODY-S04-111`)/`ApplyCharacterRespec`(`ODY-S04-107`) already established.
- **`ImportCharacter`'s own two-`CommandId` shape:** `ImportCharacter(ImportCharacterRequest request, CommandId bindCommandId, CommandId applyStateCommandId, CorrelationId correlationId)` — two real, independent writes (the `BindDraftToCampaign` insert, then the mechanics-apply update) each need their own `AppliedCommands` row for `SqliteSavingPipeline`'s own idempotency mechanism (globally unique by `CommandId`, confirmed by direct inspection of `IsCommandAlreadyApplied`'s own query) — reusing one `CommandId` for both writes is not a safe shortcut; the caller supplies two, exactly as it would for any two sequential domain commands.
- **`ImportCharacterRequest`:** `CampaignHandle targetCampaign`, `string bundleDirectoryPath`, `UserId? initialPrimaryOwnerUserId` (required for a `PlayerCharacter`, exactly mirroring `BindDraftToCampaignRequest`'s own rule — ownership never crosses the file, so the importing caller must supply a fresh owner). `CharacterKind`/`DisplayName`/`AnatomyProfileRef` are read from the imported payload itself, not re-specified by the caller. No `actorIsMainGm`/permission parameter — `ImportCharacter`, like `BindDraftToCampaign` itself, performs no actor-permission gate (`ADR-026` §6: "permission to *initiate* export/import... an ordinary ADR-019 action check, not a new mechanism" — out of this ADR's/this task's scope).
- Tests: bundle structure/no-identity-leakage, role-invariant output (MainGM vs. owner byte-identical `character.json`), fresh `CharacterId`/Draft/approval-required on import, `RulesetVersion` pinned to target (not the file's own value when they differ), incompatible-Ruleset rejection (same error code `BindDraftToCampaign` already returns), full round-trip value preservation (attributes/skills/abilities/resources/anatomy, including fresh instance IDs for abilities/resources/permanent-modifications), malformed-bundle rejection (graceful `Result.Failure`, not a thrown exception), duplicate-`CommandId` idempotency for both of `ImportCharacter`'s own internal steps.

No Unity/UI code, no `ADR-022`/`023`/`025`/`026` content change, no GM-only/secret field invented.

## 5. A real architectural gap found and resolved during this task

`BindDraftToCampaign` (`ODY-S04-103`) never actually applies `CharacterCreationSeed.Items` to any real typed section — it only stores them as opaque `SeedCopyJson` provenance. This was intentional at the time (no ability/resource/anatomy typed schema existed yet when `ODY-S04-103` was built) but means `ADR-026` §9 item 4's round-trip-preservation requirement cannot be satisfied by the bind call alone. Resolved by adding a second, explicit, dedicated step (`ApplyImportedCharacterState`) run immediately after a successful bind, using the same "one dedicated cross-section method for a genuinely cross-cutting operation" precedent already established by `RestoreDeadCharacter`/`ApplyCharacterRespec` — not by retrofitting `BindDraftToCampaign`/`CharacterCreationSeed` themselves, which stay completely unmodified per `ADR-026` §8 rule 6.

A second, smaller gap: `BindDraftToCampaign`'s Ruleset-compatibility gate only activates when `CharacterCreationSeed.TemplateId.HasValue` — a real, minted `CharacterTemplateId`, which import does not have (and minting a fake one would corrupt `CharacterRecord.TemplateId`'s own provenance meaning). Resolved by having `ImportCharacter` call the exact same underlying pure function (`CharacterTemplateCompatibility.IsCompatible`) directly, before ever calling `BindDraftToCampaign` — same decision, same error code, zero changes to `ODY-S04-103`'s own files.

A third, real bug was caught by this task's own first duplicate-`CommandId` test run: `BindDraftToCampaign`'s own generic replay lookup (`"LastCommandId = $commandId"` on the live row, no `CharacterId` filter) is correct for every existing caller, none of which runs a second write against the same freshly-bound row afterward. `ImportCharacter` deliberately does exactly that (`ApplyImportedCharacterState`'s own later write overwrites `LastCommandId` with its own `applyStateCommandId`) — so replaying the original `bindCommandId` a second time finds no row whose `LastCommandId` still equals it, and `BindDraftToCampaign`'s own replay fails even though the bind genuinely already succeeded. This is the same class of bug `ODY-S04-110` found in `DeleteCharacterPermanently` (a later write invalidating an earlier command's own live-row-based replay lookup). Resolved the same way: `ImportCharacter` checks `AppliedCommands` for `bindCommandId` directly before ever calling `BindDraftToCampaign`; if already applied, it resolves the bound `CharacterId` straight from the already-committed `odyssey.persistence.character_draft_bound` `DomainEvents` row (which carries `CommandId` as its own column) instead of re-invoking `BindDraftToCampaign`'s own live-row replay path at all — again, zero changes to `BindDraftToCampaign`/`ODY-S04-103`'s own files.

## 6. Milestones

### M1 — Application-layer contracts

- [x] `CharacterExportContracts.cs` (new): `ExportActorContext`, `CharacterExportManifest`, `CharacterExportPayload` (+ nested export-shaped attribute/skill/ability/resource/anatomy DTOs), `CharacterExportBundle`, `ImportCharacterRequest`.
- [x] `RedactCharacterForExport.cs` (new): the pure redaction function + `ExportedByRole` resolution helper.
- [x] `ICharacterRepository.ExportCharacter`/`ImportCharacter` added to `CharacterRepositoryContracts.cs`.
- [x] New error code/`PersistenceFailures` entry added (malformed bundle).

### M2 — Persistence and tests

- [x] `SqliteCharacterRepository.ExportCharacter`/`ImportCharacter`/`ApplyImportedCharacterState` implemented.
- [x] `HistoryEventTypes` extended with the new import event type.
- [x] Tests written in `CharacterExportImportTests.cs` (9 tests), all passing after fixing the two design gaps (section 5) and the third real bug (replay-clobbering, section 5).
- [x] `dotnet build`/`dotnet test` full suite green (460/460, no regression).

### M3 — Validation and review readiness

- [x] `.\scripts\verify-format.ps1`.
- [x] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries (`TC-CHAR-144`–`152`).
- [x] `.\scripts\check-repository-policy.ps1` final green run.
- [x] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check.
- [ ] Commit, push, Draft PR.
- [ ] Record CI status.

## 7. Progress log

- 2026-09-02 -- Preflight: verified PR #95's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-112-odchar-export-import`; preserved the product owner's own prep changes (`ADR-026`, task contract, `PLANS.md`/backlog updates) via stash across the branch switch.
- 2026-09-02 -- Read `ADR-026` (full), `ADR-025` §7.6 (full), `ADR-023` §5–6 (full), and the task contract in full.
- 2026-09-02 -- Direct inspection of `BindDraftToCampaign`/`CharacterCreationSeed`/`CharacterTemplateCompatibility` revealed the two gaps in section 5 above; resolved both without touching `ODY-S04-103`'s own files.
- 2026-09-02 -- This ExecPlan authored before any code change, per `PLANS.md` §1.2.
- 2026-09-02/03 -- Implemented `CharacterExportContracts.cs`/`RedactCharacterForExport.cs` (Application), `ICharacterRepository.ExportCharacter`/`ImportCharacter` extension, and `SqliteCharacterRepository.ExportCharacter`/`ImportCharacter`/`ApplyImportedCharacterState` (Persistence); `dotnet build` passed on the first attempt.
- 2026-09-03 -- Wrote 9 tests; one failed on the first run (the third bug in section 5 -- `BindDraftToCampaign`'s own replay lookup broken by `ApplyImportedCharacterState`'s later `LastCommandId` overwrite), diagnosed and fixed without touching `BindDraftToCampaign`/`ODY-S04-103`'s own files, all 9 passed after the fix.
- 2026-09-03 -- Full suite green (229/229 persistence, 460/460 total, no regression); added `ERROR_CODES.md`/`test-catalog.json` entries; `check-repository-policy.ps1`/`verify-format.ps1` both green; backlog status row updated.

## 8. Decisions

- 2026-09-02 -- Decision: `.odchar` bundle mechanics: a plain directory, no compression. Authority: `ADR-026` §3.1/§6/§11 explicitly defer this to the implementation, listing it as "not fixed by this ADR."
- 2026-09-02 -- Decision: `ExportCharacter`/`ImportCharacter` fold directly into `ICharacterRepository` (no new mini-interface). Authority: matches every prior `SLICE-04` task's own convention (`ArchiveCharacter`/`TransitionCharacterToDead`/etc. all extend the same interface rather than spinning off a new port).
- 2026-09-02 -- Decision: bundle file I/O lives in `SqliteCharacterRepository.cs` (Persistence), not a new layer. Authority: `SqliteBackupRepository`'s own precedent of file I/O living in the concrete Persistence implementation; `ADR-026` §7's own module-boundary text ("`Odyssey.Persistence` reads the Character row to build the export payload").
- 2026-09-02 -- Decision: `ImportCharacter`'s Ruleset-compatibility check calls `CharacterTemplateCompatibility.IsCompatible` directly, never touching `BindDraftToCampaign`/`CharacterCreationSeed`'s own code. Authority: section 5 above; keeps `ODY-S04-103` genuinely unmodified per `ADR-026` §8 rule 6/`ADR-025` §7.6.
- 2026-09-02 -- Decision: mechanics/anatomy/resource reconstruction is a second, dedicated cross-section step (`ApplyImportedCharacterState`) after `BindDraftToCampaign`, not a change to `BindDraftToCampaign`/`CharacterCreationSeed` themselves. Authority: section 5 above; matches `RestoreDeadCharacter`/`ApplyCharacterRespec`'s own "one dedicated method for a genuinely cross-cutting operation" precedent.
- 2026-09-02 -- Decision: `DevelopmentPool.Reserved` is never exported/imported (always `0` on import). Authority: `Reserved` has no meaning without its corresponding pending `AdvancementRecommendationRecord` rows, which are a wholly separate, non-exported table/record; ADR-025 §6.2's own "reservations are per-Character, per-campaign state" spirit.
- 2026-09-02 -- Decision: `CharacterAbilityId`/`CharacterResourceId`/`PermanentModificationId` are minted fresh on import; `BodyPartId` is kept as-is. Authority: the former three are minted GUIDs scoped to a specific database (reusing one across campaigns risks collision and misrepresents provenance); `BodyPartId` is a catalog-style human-readable key already reused as-is by `ReplaceAnatomyProfile`'s own existing convention.
- 2026-09-03 -- Decision: a replayed `bindCommandId` is resolved from the already-committed `odyssey.persistence.character_draft_bound` `DomainEvents` row's own `CommandId` column, never by re-invoking `BindDraftToCampaign`'s own live-row-based replay path. Authority: section 5's third bug — `BindDraftToCampaign`'s own replay lookup is broken for this specific two-write chain by `ApplyImportedCharacterState`'s later `LastCommandId` overwrite; this fix touches only `ImportCharacter`'s own code, keeping `BindDraftToCampaign` genuinely unmodified.

## 9. Discoveries and deviations

- Two real architectural gaps (section 5) were found and resolved without touching any `ODY-S04-103` file — see Decisions above for the exact resolution and its authority.
- No open architectural question was found that `ADR-025`/`ADR-026` do not already answer.

## 10. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 229/229 `Odyssey.Tests.Persistence` (9 new), 460/460 total, zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 11. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema change beyond the `Character` table's already-existing columns (this task writes no new column); `ExportCharacter` is read-only; `ImportCharacter` only ever creates a new Draft, never mutates an existing Character.

## 12. Open questions and blockers

None — both gaps found during this task's own investigation were resolved directly from `ADR-025`/`ADR-026`'s own text and this codebase's existing conventions (section 5/8 above).

## 13. Outcome and follow-up

To be filled once the PR is opened and CI status is known.
