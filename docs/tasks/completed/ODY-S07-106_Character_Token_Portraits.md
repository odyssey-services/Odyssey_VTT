# ODY-S07-106 — Character and Token Portraits (`PortraitAssetId`, block 5)

## 1. Task identity
`ODY-S07-106`; status: Done (PR #171, merged into main). Sixth task of `SLICE-07`, the decomposition of block 5 -- the last of the five originally reserved blocks.

## 2. Goal
Give `CharacterRecord` and `TokenRecord` each a real, validated portrait reference -- a new nullable `PortraitAssetId` (`AssetId?`) -- set/cleared through two independent commands (`ICharacterRepository.SetCharacterPortrait`, `ISceneRepository.SetTokenPortrait`), each following `ODY-S07-105`'s `SetSceneBackground` exactly: idempotent, optimistically concurrent, fail-closed against the campaign's own `AssetManifestEntries`, keeping `AssetReferences` to one current row.

## 3. Authority
`docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md` §3/§8/§9 (block 5, row 6). This task's own governing ТЗ §0-§1.

## 4. In scope
`Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs` -- `CharacterRecord.PortraitAssetId`, `ICharacterRepository.SetCharacterPortrait`. `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` -- implementation, `Character` DDL column, shared `SelectColumns`/`ReadCharacterRecord`, and the 18 rebuild-from-current `CharacterRecord` construction sites. `Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs` -- `TokenRecord.PortraitAssetId`, `ISceneRepository.SetTokenPortrait`. `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs` -- implementation, `Token` DDL column, the four shared full-row `Token` SELECTs, `ReadTokenRecord`, `MoveToken`'s own partial read and record construction. `DotNet/Tests/Odyssey.Tests.Persistence/SqliteCharacterRepositoryTests.cs` (`TC-CHAR-173`-`180`), `SqliteSceneRepositoryTests.cs` (`TC-BOARD-024`-`032`). `Tests/Metadata/test-catalog.json`. `docs/tasks/SLICE-07_IMPLEMENTATION_BACKLOG.md`. This task's own contract and ExecPlan. One forced, mechanical touch outside the stated list -- see §18.

## 5. Out of scope
`CharacterRecord.PortraitReference` (opaque string), `UpdatePresentation`, `CharacterExportContracts.cs`/`RedactCharacterForExport.cs`, and the ADR-022 §7 `PortraitReferenceSnapshot` journal payload (written in four-plus places in `SqliteCharacterRepository.cs`) -- all untouched, not retyped, not marked deprecated (deprecation is an unconfirmed product decision). Automatic inheritance of a token's portrait from its linked Character -- no such concept exists in the codebase (`TokenRecord.CharacterId` is used only for `ListTokensByCharacter`), and inventing one would be unrequested product behavior. Origin/scale/placement, new `ContentDefinitionType`s. Existing `ICharacterRepository`/`ISceneRepository` signatures other than the two added methods. `SetSceneBackground`/`ODY-S07-102`-`105`. Any ADR. `SLICE-05`/`SLICE-06` backlogs. `MigrationRegistry.cs`.

## 6. Domain contract
No Domain-layer type. `CharacterRecord`'s and `TokenRecord`'s constructors each gain one optional trailing parameter (`AssetId? portraitAssetId = null`), validated only when supplied (`TokenRecord.CharacterId`'s and `SceneRecord.BackgroundAssetId`'s own precedent); every existing call site compiles unchanged.

**Interpretation chosen for the block's ambiguous wording.** The backlog described block 5 as wiring "real asset-registration validation/linking for `CharacterRecord`'s own already-existing but unwired `PortraitReference` field", readable as either "validate the existing field" or "add a new one". This task chose the second, with a stronger justification than `ODY-S07-103`'s analogous choice: `PortraitReference` participates in two already-stable contracts -- the portable character export/import, and the mandatory ADR-022 §7 `PortraitReferenceSnapshot?` event payload. An `AssetId` is campaign-scoped and not portable across campaigns/machines as the export contract implies, so retyping the field would break both.

## 7. Application contract
`ICharacterRepository.SetCharacterPortrait(campaign, characterId, AssetId? portraitAssetId, long expectedPresentationRevision, commandId, correlationId) -> Result<CharacterRecord>` -- a separate method, not an `UpdatePresentation` overload; gated by `Revisions.PresentationRevision` (the same section `UpdatePresentation` uses), bumping it and `CharacterRevision`. `ISceneRepository.SetTokenPortrait(campaign, tokenId, AssetId? portraitAssetId, long expectedRevision, commandId, correlationId) -> Result<TokenRecord>` -- single record revision, by exact precedent of `SetSceneBackground`/`MoveToken`. `null` clears in both.

## 8. Persistence boundary
Each command, inside one `SqliteSavingPipeline.Execute` transaction: read current row (existing `CharacterNotFound`/`TokenNotFound`) -> atomic revision check (existing `CharacterRevisionConflict`/`TokenRevisionConflict`) -> if non-null, a single `AssetManifestEntries` existence lookup (existing `AssetNotFound`, added by `ODY-S07-105`; campaigns are separate SQLite files, so this one lookup is also the cross-campaign check) -> `UPDATE` -> `DELETE FROM AssetReferences WHERE ReferencedByType = '<Character|Token>' AND ReferencedById = $id`, then conditional `INSERT` -- new `ReferencedByType` literals `"Character"` and `"Token"` (the column has no CHECK/enum constraint). New DomainEvents: `odyssey.persistence.character_portrait_set`, `odyssey.persistence.token_portrait_set`. `Character` and `Token` DDL each gain a nullable `PortraitAssetId TEXT` column. Read paths: `SqliteCharacterRepository`'s single shared `SelectColumns`/`ReadCharacterRecord` (column appended last, existing indexes unchanged) and `SqliteSceneRepository`'s four shared full-row `Token` SELECTs plus `ReadTokenRecord`; `MoveToken`'s own partial read now also fetches the column. `CharacterRecord` is rebuilt from `current` in 18 places; each now passes `current.PortraitAssetId`, so no other command's returned record silently drops the portrait (covered by the `UpdatePresentation`/`MoveToken` assertions in `TC-CHAR-173`/`TC-BOARD-024`).

## 9. Tests and validation
Character (`TC-CHAR-173`-`180`) and Token (`TC-BOARD-024`-`031`) each mirror `ODY-S07-105`'s eight scenarios: valid set (revision incremented), non-existent `AssetId`, `AssetId` from a genuinely separate campaign (a second real temp campaign, not a stub), clear-to-null (also removes the `AssetReferences` row), mismatched revision, same-`CommandId` replay (direct SQL count: no second row, revision not advanced), `AssetReferences` row present after success (direct SQL read), and replacement leaves exactly one row. `TC-CHAR-173` additionally proves `PortraitReference` is untouched and that a later `UpdatePresentation` does not clobber `PortraitAssetId`; `TC-BOARD-024` proves a later `MoveToken` carries it. `TC-BOARD-032` proves no token-from-character inheritance. Full solution `dotnet test` and `verify-format.ps1`/`verify-repository.ps1`/`verify-test-structure.ps1` green (counts in §17).

## 10. Compatibility and rollback
Optional trailing constructor parameters and two added interface methods only; no existing signature or behavior changes. Reverting returns both records and repositories to their pre-`106` shape; `AssetReferences` keeps only its `"Scene"` rows.

## 11. Security and privacy
No new permission concept (neither command performs a permission gate, matching `UpdatePresentation`/`SetSceneBackground`). `AssetId` is opaque; `AssetNotFound` exposes no path or raw id.

## 12. Observability
Two new DomainEvents through the existing, unmodified `SqliteSavingPipeline`. The character event payload carries `portraitAssetId` (deliberately additive and separate from the untouched `portraitReferenceSnapshot`).

## 13. Performance
Per command: one extra `AssetManifestEntries` lookup (non-null case) and one `DELETE` plus optional `INSERT` on `AssetReferences`, in the same single transaction.

## 14. Dependencies
`ODY-S07-105` (`SetSceneBackground`, `AssetNotFound`, the `AssetReferences` delete-then-reinsert idiom). `ODY-S04-101` (`UpdatePresentation`, Presentation-section revision, `SelectForUpdate`/`ReplayCharacter`). `ODY-S03-004`/`ODY-S06-104` (`MoveToken`, `TokenRecord.CharacterId`). `ADR-002` §10.2, `ADR-012` §5, `ADR-013`, `ADR-022` §5/§7.

## 15. Dependencies (packages)
None new.

## 16. Implementation plan
See `docs/plans/active/ODY-S07-106_Character_Token_Portraits.md`.

## 17. Completion evidence
Both commands work end-to-end with concurrency, idempotency, fail-closed (including genuine cross-campaign) validation and `AssetReferences` sync, proven by 17 new tests using direct SQL reads for the reference table. `PortraitReference`/`UpdatePresentation`/export/`PortraitReferenceSnapshot` code is unchanged apart from the mechanical `current.PortraitAssetId` pass-through argument at rebuild sites (the `PortraitReference` value itself is still passed exactly as before). Full solution `dotnet test`, `verify-format.ps1`, `verify-repository.ps1` (incl. `REPO-POLICY-005`), `verify-test-structure.ps1` green.

## 18. Change control

### Decisions made during execution

- **`DotNet/Tests/Odyssey.Tests.Persistence/CheckIntegrationTests.cs` was touched (one added line) although it is not in the governing ТЗ §5 allowed-paths list.** It contains `FailsFirstCriticalSuccessEvidenceCharacterRepository`, a hand-written `ICharacterRepository` decorator that forwards every member to an inner repository; adding an interface method forces it to implement `SetCharacterPortrait`, otherwise the test project does not compile. The edit is a single one-line forward (`=> _inner.SetCharacterPortrait(...)`), with no behavioral change to that test. Same class of "the list under-names a location the requirement cannot avoid" disclosure as `ODY-S07-102`/`105`. A search confirmed it is the only additional implementer of either repository interface.
- **No new error code.** The governing ТЗ §1.6 asked to check for reusable codes first: `CharacterRevisionConflict` and `TokenRevisionConflict` already exist and fit exactly, and `AssetNotFound` (from `ODY-S07-105`) covers both fail-closed cases -- so `ErrorCodes.cs`, `CampaignRepositoryContracts.cs` and `ERROR_CODES.md` are untouched.
- **Rebuild-site pass-through.** `CharacterRecord` is reconstructed from `current` in 18 command paths. Adding only a constructor default would have made every such command's *returned* record report `PortraitAssetId == null` while the database still held it. All 18 now pass `current.PortraitAssetId`; the two creation sites use the `null` default, and the single shared read path supplies the stored value.
- **`MigrationRegistry.cs` deliberately not touched** -- same disclosed boundary as `ODY-S07-105`: no `ADR-013` migration runner exists, only a one-entry identity registry, so the new columns are added through `CREATE TABLE IF NOT EXISTS`, the way every existing column arrived. As accepted for `ODY-S07-105`, this does not retroactively add the columns to a pre-existing `campaign.db` created by an older build.
- **Known, disclosed limitation (not built):** deleting a Character does not remove its `"Character"` `AssetReferences` row. No asset-deletion feature exists that would consult that table today, and character deletion is outside this task; a future asset-lifecycle task should reconcile it.
- **Replay semantics** match the existing per-aggregate convention: `SetTokenPortrait` replays through `ReplayToken` keyed by `TokenId` (as `MoveToken` does) and `SetCharacterPortrait` through `ReplayCharacter` keyed by `CharacterId AND LastCommandId`.

### Blockers

- None.
