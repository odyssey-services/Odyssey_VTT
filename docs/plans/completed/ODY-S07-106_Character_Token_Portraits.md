# ExecPlan — ODY-S07-106 Character and Token Portraits

## 1. Purpose
Add validated `PortraitAssetId` fields and `SetCharacterPortrait`/`SetTokenPortrait` commands, each following `ODY-S07-105`'s `SetSceneBackground`, leaving the opaque `PortraitReference` string and its export/event contracts untouched.

## 2. Scope
Character and scene repository contracts and SQLite implementations, `Character`/`Token` DDL, shared read paths, 18 rebuild-from-current `CharacterRecord` sites, tests `TC-CHAR-173`-`180` and `TC-BOARD-024`-`032`, `test-catalog.json`, backlog block 5/row 6, this task's contract and plan. One forced one-line forward in a test decorator (contract §18).

## 3. Non-goals
`PortraitReference`/`UpdatePresentation`/export/redaction/`PortraitReferenceSnapshot`; token-from-character portrait inheritance; new error codes; ADR changes; `MigrationRegistry.cs`; deprecating `PortraitReference`; asset deletion/reference reconciliation.

## 4. Architecture
Two independent mirrors of `SetSceneBackground`. Character: gated by `PresentationRevision` (bumps it and `CharacterRevision`), reads via the shared `SelectForUpdate`, replays via `ReplayCharacter`. Token: gated by `TokenRecord.Revision`, replays via `ReplayToken` keyed by `TokenId` (same as `MoveToken`). Both: existence lookup in `AssetManifestEntries` (also the cross-campaign check), `UPDATE`, then `AssetReferences` delete-then-optionally-reinsert with `ReferencedByType` `"Character"`/`"Token"`, all inside one `SqliteSavingPipeline` transaction with a new DomainEvent.

## 5. Milestones
M1 research by direct read: `UpdatePresentation`, `CharacterRecord` ctor, `SelectColumns`/`ReadCharacterRecord`/`ReplayCharacter`/`SelectForUpdate`, all 21 `new CharacterRecord(` sites, `Character`/`Token` DDL, `MoveToken`/`ReplayToken`/`ReadTokenRecord`, existing `PersistenceFailures`. M2 contracts (both records, both interface methods). M3 Character persistence (DDL, read path, 18 pass-through sites via a scripted, count-checked replace, new method). M4 Token persistence (DDL, four SELECTs, `ReadTokenRecord`, `MoveToken` partial read, new method). M5 build; fix the forced test-decorator forward. M6 tests (Character 8, Token 9). M7 targeted runs, then full suite. M8 catalog, backlog, contract/plan (before the test-structure check), validation scripts, commit, Draft PR.

## 6. State and data flow
`SetX` -> `SqliteSavingPipeline.Execute` -> `tryReplay` (stored outcome) or `apply`: select -> revision check -> asset existence -> `UPDATE` -> `AssetReferences` sync -> `PipelineWrite` with the new DomainEvent, committed atomically with the `AppliedCommands` row.

## 7. Error handling
Existing codes only: `CharacterNotFound`/`TokenNotFound`, `CharacterRevisionConflict`/`TokenRevisionConflict`, `AssetNotFound`, `CharacterIoFailed`/`SceneIoFailed`.

## 8. Test strategy
Per command, `ODY-S07-105`'s eight scenarios (real temp campaigns; a second real campaign for the cross-campaign case; direct SQL for `AssetReferences`), plus regression guards for the rebuild pass-through (`UpdatePresentation` after `SetCharacterPortrait`; `MoveToken` after `SetTokenPortrait`), `PortraitReference` independence, and no token-from-character inheritance.

## 9. Validation and acceptance evidence
`dotnet build` clean; targeted then full `dotnet test` green; `verify-format.ps1`, `verify-repository.ps1`, `verify-test-structure.ps1` green; `git diff --name-status` confined to the contract's §4 list.

## 10. Recovery and rollback
Optional trailing constructor parameters and two added interface methods; reverting restores the pre-`106` shape with no data dependency from later tasks.
