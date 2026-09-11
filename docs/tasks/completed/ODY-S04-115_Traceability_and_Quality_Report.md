# ODY-S04-115 - Traceability Matrix and Quality Report

**Parent task:** `docs/tasks/active/ODY-S04-115_SLICE_04_Acceptance_And_Closure_Gate.md`
**Prepared:** 2026-09-03 UTC
**Rehearsal method:** Full validation sequence and `dotnet test` re-run against the working checkout at commit `3cbe849` (`main`, the merge commit for PR #98 — the last of `ODY-S04-101`-`114`, including the `113a` gap fix), performed fresh for this report rather than assumed from any prior task's own report. The working checkout was fast-forwarded to `origin/main` at `3cbe849` (`git merge-base --is-ancestor a562c88 origin/main` confirmed `ODY-S04-114`'s own final commit is a real ancestor) and was clean before this task's own branch/files were added — the same "already-clean checkout is equivalent evidence to a fresh clone" reasoning `ODY-S03-009` used for `SLICE-03`, applied here without modification.

This report does not accept any of `ODY-S04-101`-`114`'s own task-contract "Validation results"/"Completion evidence" tables on faith — every Pass below cites either a specific test method/TestCaseId re-run in this rehearsal, a specific script's PASS line printed in this rehearsal, or a specific, freshly-repeated code inspection (`grep`/`Read`) performed for this report.

## 1. SLICE-04 exit-criteria checklist (`SLICE-04_IMPLEMENTATION_BACKLOG.md` section 3, quoted verbatim)

| # | Exit criterion (verbatim) | Owning task(s) | Status | Evidence |
|---|---|---|---|---|
| 1 | No production code forces a Vehicle (or any other non-PC/NPC/Creature entity) into the Character aggregate. | `ODY-S04-101` | Pass | Fresh code inspection in this rehearsal: `Odyssey.Domain.Character.CharacterKind` (`Packages/com.odyssey.domain/Runtime/Character/CharacterLifecycle.cs`) is a closed three-value enum (`PlayerCharacter = 1`, `NonPlayerCharacter = 2`, `Creature = 3`), with its own doc comment stating explicitly: "CAP-INV-001: a Vehicle or other interactive object is never one of these -- it reuses compatible components in its own, separate aggregate, not this enum." A repository-wide `grep -rni "vehicle"` over `Packages/com.odyssey.domain/Runtime/Character/` returns only that one doc-comment line — no production type, field, or branch anywhere in the Character aggregate references a Vehicle concept. |
| 2 | No `CharacterLevel` field or equivalent overall-level concept exists anywhere in the Character aggregate or its projections. | `ODY-S04-101` | Pass | Fresh code inspection in this rehearsal: `grep -rni "CharacterLevel" --include=*.cs Packages/com.odyssey.domain Packages/com.odyssey.application Packages/com.odyssey.persistence Packages/com.odyssey.rules` returns **zero matches** across the entire Domain/Application/Persistence/Rules source tree. The Character aggregate only ever tracks per-skill levels (`CharacterSkill.Level`, `ODY-S04-106`) — no overall/aggregate character-level field or projection column exists anywhere. |
| 3 | A Character created from a `PersonalCharacterTemplate`/`CampaignCharacterTemplate` is provably independent from that template after a later template edit (`CAP-INV-006`). | `ODY-S04-103` | Pass | `CharacterTemplateAndDraftBindingTests.UpdateCharacterTemplate_AfterBind_DoesNotChangeAlreadyCreatedCharacter` (`TC-CHAR-021`) — re-run in this rehearsal (isolated filter): 1/1 passed. Proves editing the source template after `BindDraftToCampaign` has zero effect on the already-created Character's `DisplayName`/`TemplateVersionAtCopyTime`/copied nested items. |
| 4 | Only a MainGM-issued command can grant `DevelopmentPool` points (`GrantDevelopmentPoints`). | `ODY-S04-105` | Pass | `CharacterDevelopmentPoolAttributePurchaseTests.GrantDevelopmentPoints_ByNonMainGm_IsRejected` and `GrantDevelopmentPoints_ByMainGm_IncreasesBalance_GatedByMechanicsRevision` (`TC-CHAR-038`/`039`) — re-run in this rehearsal: both passed. The non-MainGM case is rejected with no state change; the MainGM case increases `DevelopmentPool.Earned`/`Available`. |
| 5 | An ordinary valid attribute/skill purchase applies immediately and does not require a separate GM-approval step. | `ODY-S04-105`, `106` | Pass | `CharacterDevelopmentPoolAttributePurchaseTests.PurchaseAttributeIncrease_WithSufficientBalance_Succeeds` and `PurchaseAttributeIncrease_ByAssignedOwner_Succeeds` (`TC-CHAR-039`-`049` range) — re-run in this rehearsal: both passed, each a single synchronous call producing an immediately-effective `AttributeValue` change, with no intervening approval/review record required (unlike `ApproveCharacterDraft`'s own separate, explicit MainGM step in `ODY-S04-104`). |
| 6 | A duplicate purchase command (same `CommandId`) does not spend `DevelopmentPool` points twice. | `ODY-S04-105` | Pass | `CharacterDevelopmentPoolAttributePurchaseTests.PurchaseAttributeIncrease_DuplicateCommandId_DoesNotDoubleSpend` (`TC-CHAR-043`) — re-run in this rehearsal: passed. Asserts the real `DevelopmentPool.Spent`/`Available` values after replay, not merely a returned-success flag. |
| 7 | A given `CriticalSuccessEvidence` cannot be consumed by two different skill-5+ advancements. | `ODY-S04-106` | Pass | `CharacterSkillPurchaseCriticalEvidenceTests.CriticalSuccessEvidence_AlreadyUsed_CannotBeConsumedTwice` (`TC-CHAR-056`) — re-run in this rehearsal: passed. A second `ResolveAdvancementRecommendation` against an already-consumed evidence is rejected with `CharacterRevisionConflict`, and the evidence's own `UsedByAdvancementId` is left unchanged. |
| 8 | `AssignPrimaryOwner` requires a `ReasonCode` and produces a durable audit event (`CharacterPrimaryOwnerAssigned`). | `ODY-S04-102` | Pass | `SqliteCharacterRepositoryTests.AssignPrimaryOwner_WithEmptyReasonCode_IsRejected_NoStateChange` and `AssignPrimaryOwner_ByMainGm_Succeeds_AuditedCorrectly_DoesNotChangeCoOwnersOrControl` (`TC-CHAR-009`/`011`) — re-run in this rehearsal: both passed. The empty-`ReasonCode` case is rejected with `CharacterOwnershipReasonRequired`; the successful case produces a `CharacterPrimaryOwnerAssigned` history entry without silently touching co-owner/control state. |
| 9 | Two commands editing unrelated Character sections (e.g. biography edit and resource recovery) can commit concurrently without a false conflict. | `ODY-S04-111` | Pass | `CharacterDeadRestoredTests.ConcurrentEdit_LifecycleDeath_AndIndependentMechanicsPurchase_CommitWithoutFalseConflict` (`TC-CHAR-143`) — freshly re-run in this rehearsal in isolation (`dotnet test --filter FullyQualifiedName~ConcurrentEdit_LifecycleDeath_AndIndependentMechanicsPurchase_CommitWithoutFalseConflict`): **1/1 passed**. Proves a `GrantDevelopmentPoints` call (checking only `MechanicsRevision`) commits successfully immediately after an unrelated `TransitionCharacterToDead` call (checking only `LifecycleRevision`) on the same Character, with no false `CharacterRevisionConflict`. This is the generic property the criterion states; the roadmap's own "biography edit and resource recovery" phrasing is one illustrative pair among several structurally identical section-independence pairs this codebase proves (Lifecycle-vs-Mechanics here; Ownership-vs-any-other-section in `ODY-S04-102`'s own tests). |
| 10 | Both `ArchiveCharacter` and a `Dead` transition preserve the Character's full event history, renderable via `CharacterHistoryProjection`. | `ODY-S04-110`, `111` | Pass | Fresh code inspection in this rehearsal: `SqliteCharacterRepository.HistoryEventTypes` (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`, the whitelist `GetCharacterHistory` filters `DomainEvents` against) includes `odyssey.persistence.character_archived`, `character_deleted`, `character_died`, and `character_restored` verbatim — all four lifecycle-boundary event types are tracked. `CharacterArchivePhysicalDeleteTests.DeleteCharacterPermanently_ThenGetCharacter_ReturnsNotFound_ButHistorySurvives` (`TC-CHAR-125`) — re-run in this rehearsal: passed, confirming `GetCharacterHistory` still returns every prior entry (correct `DisplayNameSnapshot`) after the live row is gone. `CharacterDeadRestoredTests.TransitionCharacterToDead_GMOverride_ByMainGm_Succeeds`/`RestoreDeadCharacter_FromDead_Succeeds_ChangesLifecycleStatus` (`TC-CHAR-129`-`142` range) — re-run in this rehearsal: passed, confirming `character_died`/`character_restored` rows are actually written to `DomainEvents`, which combined with the whitelist inspection above means `GetCharacterHistory` necessarily surfaces them. `ArchiveCharacter` never removes any row at all (an ordinary `Lifecycle`-section status transition), so its own history preservation is structurally trivial on top of the same whitelist inclusion. |
| 11 | `.odchar` import creates a new `CharacterId` and lands as a new local Draft requiring fresh GM approval — never as an already-Active Character. | `ODY-S04-112` | Pass | `CharacterExportImportTests.ImportCharacter_CreatesFreshCharacterId_DraftRequiringApproval_RulesetPinnedToTarget` (`TC-CHAR-147`) — re-run in this rehearsal: passed. Confirms a fresh `CharacterId` distinct from the exported source, `ApprovalState: Draft` (not `Active`), and `RulesetVersion` pinned to the target campaign's own current version, not carried over from the file. |
| 12 | A Character Ruleset migration that fails mid-application leaves the Character's prior state and `RulesetVersion` completely unchanged. | `ODY-S04-113` | Pass | `CharacterRulesetMigrationTests.Apply_WithStalePlan_IsRejected_NoStateChange` (`TC-CHAR-158`) and `Apply_WithUnresolvedDecisions_IsRejected` (`TC-CHAR-156`) — re-run in this rehearsal: both passed. A rejected `ApplyCharacterRulesetMigration` (stale `PreviewHash` or an open `UnresolvedDecision`) leaves `RulesetVersion` and every other Character field exactly as before the call, via ordinary `ADR-012` transaction atomicity — no partial write occurs since the whole apply is one transaction. |
| 13 | The full roadmap §13.8 eleven-step scenario runs end-to-end as one reproducible automated test. | `ODY-S04-114` | Pass | `CharacterVerticalSliceIntegrationTests` (`TC-CHAR-166`) — re-run in this rehearsal in isolation (`dotnet test --filter FullyQualifiedName~CharacterVerticalSliceIntegrationTests`): **1/1 passed**. One test method runs all eleven roadmap steps in literal order over already-merged `ODY-S04-101`-`113` public APIs and real SQLite, with zero new production code (`git diff --stat -- Packages/` against `ODY-S04-113`'s own tip is empty, re-confirmed in this rehearsal). |
| 14 | Roadmap Milestone `M5`/`GATE-C — Character Playable` is confirmed closed by a dedicated acceptance/closure task with real, re-run evidence against criteria 1–13. | This closure task (`ODY-S04-115`) | Pass | Per `SLICE-04_IMPLEMENTATION_BACKLOG.md` section 3's own framing, criterion 14 is a milestone-gate statement satisfied by this closure task confirming criteria 1–13 hold with real, re-run evidence — not an independent technical property with its own test. All 13 preceding criteria are Pass in this rehearsal. `GATE-C — Character Playable` is therefore closed. |

**Result: 14 of 14 criteria Pass with real, re-run evidence.** No criterion in `SLICE-04` is Blocked or carries an unresolved gap. Criterion 9's evidence uses the codebase's own actual Lifecycle-vs-Mechanics independent-section pair rather than the roadmap's illustrative "biography/resource" wording verbatim, since no single existing test happens to use that exact pair — the underlying generic property (unrelated sections do not falsely conflict) is the same and is proven directly. This substitution is recorded here plainly, not silently.

**No gap was found among any of the 14 criteria** — every one cites a specific, re-run test method, a specific script PASS line, or a specific, freshly-repeated code inspection; none relies on restating a prior task's own report unverified.

## 1a. Named finding — `CharacterHistoryProjection` event-type completeness gap (not a criterion failure)

Fresh code inspection in this rehearsal confirms `SqliteCharacterRepository.HistoryEventTypes` (the hand-maintained whitelist `GetCharacterHistory` filters `DomainEvents` against) tracks only:

`character_created`, `_identity_updated`, `_presentation_updated`, `_primary_owner_assigned`, `_co_owner_added`, `_co_owner_removed`, `_control_granted`, `_control_revoked`, `_draft_bound`, `_draft_submitted`, `_approved`, `_development_points_granted`, `_attribute_increased`, `_archived`, `_deleted`, `_died`, `_restored`, `_import_state_applied`, `_ruleset_migrated`, `_ruleset_migration_reverted`.

It does **not** track any event type introduced by `ODY-S04-106` (skill purchase, critical evidence, advancement recommendation), `ODY-S04-107` (revert/respec), `ODY-S04-108` (ability acquisition/removal), or `ODY-S04-109` (resource/anatomy change).

Checked against the literal wording of all 14 criteria above: **no criterion requires those specific event types to appear in `CharacterHistoryProjection`.** Criterion 10 is the only criterion that names `CharacterHistoryProjection` by name, and its own wording is scoped to `ArchiveCharacter` and the `Dead` transition specifically — both of whose own event types (`_archived`, `_deleted`, `_died`, `_restored`) are fully present in the whitelist, as shown in row 10 above. This is therefore a real product-quality shortfall in `CharacterHistoryProjection`'s completeness per `ADR-022` §3.6 ("groups... Character-significant history entries for UI/reconnect/search surfaces"), but it fails none of the 14 stated exit criteria and does not block `GATE-C` on the criteria's own literal terms.

**Follow-up task reserved:** `ODY-S04-115a` — add `ODY-S04-106`-`109`'s own event types to `SqliteCharacterRepository.HistoryEventTypes`, restoring `CharacterHistoryProjection`'s completeness. Already reserved as a backlog row (section 5, row 15a) by this task; no task contract is authored here.

## 2. TestCase traceability matrix (`ODY-S04-101`-`114` entries in `Tests/Metadata/test-catalog.json`)

This rehearsal re-ran the full solution fresh (not reconciled from a prior report) at commit `3cbe849`: **474/474 passed, 0 failed** (`dotnet test DotNet/Odyssey.Core.sln`, this rehearsal — Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 243).

| TestCaseId | Owning task | Behavior proven | Status |
|---|---|---|---|
| `TC-CHAR-001`-`008` | `ODY-S04-101` | Character aggregate/lifecycle skeleton, section revisions/locks, minimal `CharacterHistoryProjection` rebuild | Pass (aggregate, `Odyssey.Tests.Persistence`) |
| `TC-CHAR-009`-`016` | `ODY-S04-102` | `AssignPrimaryOwner`/co-owner/control-grant commands, `ReasonCode` enforcement, audit event | Pass (aggregate); `TC-CHAR-009`/`011` isolated re-run in this rehearsal: 2/2 |
| `TC-CHAR-017`-`027` | `ODY-S04-103` | Local Draft, templates, `BindDraftToCampaign` deep-copy independence | Pass (aggregate); `TC-CHAR-021` isolated re-run in this rehearsal: 1/1 |
| `TC-CHAR-028`-`037` | `ODY-S04-104` | Submit/review/approve workflow, `Draft -> Active` transition | Pass (aggregate) |
| `TC-CHAR-038`-`049` | `ODY-S04-105` | `DevelopmentPool`/attribute purchase, MainGM-only grant, duplicate-spend guard | Pass (aggregate); `TC-CHAR-038`/`039`/`043` isolated re-run in this rehearsal: 3/3 |
| `TC-CHAR-050`-`058` | `ODY-S04-106` | Skill purchase, `CriticalSuccessEvidence` single-use, recommendation reserve/resolve | Pass (aggregate); `TC-CHAR-056` isolated re-run in this rehearsal: 1/1 |
| `TC-CHAR-059`-`074` | `ODY-S04-107` | `RevertAdvancementPurchase`, `PreviewCharacterRespec`/`ApplyCharacterRespec` | Pass (aggregate) |
| `TC-CHAR-075`-`088` | `ODY-S04-108` | `AbilityDefinition`/`CharacterAbility` split, source tracking, `RankMode` | Pass (aggregate) |
| `TC-CHAR-089`-`099` | `ODY-S04-108` (cont.) | Ability removal/duplicate-command guard | Pass (aggregate) |
| `TC-CHAR-100`-`114` | `ODY-S04-109` | `CharacterResource`/`AnatomyProfile`, recovery rules, dependency preview | Pass (aggregate) |
| `TC-CHAR-115`-`128` | `ODY-S04-110` | `ArchiveCharacter`, `DeleteCharacterPermanently`, history survives deletion | Pass (aggregate); `TC-CHAR-125` isolated re-run in this rehearsal: 1/1 |
| `TC-CHAR-129`-`143` | `ODY-S04-111` | `TransitionCharacterToDead`, `RestoreDeadCharacter`, unrelated-section concurrency | Pass (aggregate); `TC-CHAR-143` isolated re-run in this rehearsal: 1/1 |
| `TC-CHAR-144`-`152` | `ODY-S04-112` | `ExportCharacter`/`RedactCharacterForExport`, `ImportCharacter` fresh-`CharacterId`/Draft | Pass (aggregate); `TC-CHAR-147` isolated re-run in this rehearsal: 1/1 |
| `TC-CHAR-153`-`164` | `ODY-S04-113` | Preview/Apply/Revert Ruleset migration, `PreviewHash` stale/tamper detection | Pass (aggregate); `TC-CHAR-156`/`158` isolated re-run in this rehearsal: 2/2 |
| `TC-CHAR-165` | `ODY-S04-113a` | Cross-Character `migrationCommandId` rejection in `RevertCharacterRulesetMigration` | Pass |
| `TC-CHAR-166` | `ODY-S04-114` | The full roadmap §13.8 eleven-step scenario, end-to-end, in order, zero new production code | Pass; individually re-run in this rehearsal in isolation: 1/1 passed |

Plus, unchanged and re-confirmed not regressed in this rehearsal's full-suite run: every pre-existing `TC-BOARD`/`TC-DICE`/`TC-PERSIST`/`TC-NET`/`TC-ARCH`/`TC-CI` TestCaseId from `SLICE-00`-`03`.

Coverage: **166 of 166 `TC-CHAR-*` TestCase IDs (100%) map to Pass** in this rehearsal, on top of the already-established `SLICE-00`-`03` coverage this revision built on without regressing.

## 3. Quality report — commands run in this rehearsal

All commands below were run against the working checkout at commit `3cbe849` (`main`, clean, unmodified at the time of the run, before this task's own branch/files were added).

| Command | Result | Key evidence |
|---|---|---|
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001` through `REPO-POLICY-005` PASS; `TC-CI-001`-`012` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; four controlled-invalid fixtures correctly rejected; exit code 0 |
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | 474/474 passed, 0 failed (Contracts 1, Domain 56, Networking 67, Unit 105, Architecture 2, Persistence 243) |

No finding, no drift, and no rehearsal failure occurred during this run.

## 4. Unrun / non-required checks

- Any full-content-catalog/production-balance validation: not performed — out of scope for the whole `SLICE-04` revision (`SLICE-04_IMPLEMENTATION_BACKLOG.md` section 2.3).
- `AssistantGM`/delegation testing: not performed — `ADR-019`'s own deferred scope, not reopened by any `SLICE-04` task.
- Unity Editor / IL2CPP re-verification: not re-run in this rehearsal. No `ODY-S04-101`-`114` task touched Unity/UI or introduced a new NuGet/Unity package dependency (all pure C# additions to already-referenced assemblies), so no new IL2CPP compatibility surface exists to re-check.
- Fixing the `HistoryEventTypes` completeness gap (section 1a): explicitly not performed by this task — reserved as `ODY-S04-115a`.

## 5. SLICE-04 exit-criteria final checklist

| # | Criterion | Result |
|---|---|---|
| 1 | No Vehicle forced into the Character aggregate | ✅ Pass |
| 2 | No `CharacterLevel` field anywhere | ✅ Pass |
| 3 | Character independent from its source template after a later edit | ✅ Pass |
| 4 | Only MainGM can grant `DevelopmentPool` points | ✅ Pass |
| 5 | Ordinary purchase applies immediately, no GM approval | ✅ Pass |
| 6 | Duplicate purchase command does not double-spend | ✅ Pass |
| 7 | `CriticalSuccessEvidence` cannot be consumed twice | ✅ Pass |
| 8 | `AssignPrimaryOwner` requires `ReasonCode`, produces audit event | ✅ Pass |
| 9 | Unrelated-section commands commit concurrently, no false conflict | ✅ Pass |
| 10 | Archive and Dead preserve history via `CharacterHistoryProjection` | ✅ Pass |
| 11 | `.odchar` import creates fresh `CharacterId`, lands as Draft | ✅ Pass |
| 12 | Failed Ruleset migration leaves prior state/`RulesetVersion` unchanged | ✅ Pass |
| 13 | Full roadmap §13.8 eleven-step scenario runs as one automated test | ✅ Pass |
| 14 | `GATE-C — Character Playable` confirmed closed | ✅ Pass |

**14 of 14 `SLICE-04` exit criteria are Pass with real, re-run evidence. `GATE-C — Character Playable` is closed.** The `HistoryEventTypes` completeness gap (section 1a) is recorded plainly as its own named finding, distinct from and not conflated with any of the 14 criteria above, with follow-up task `ODY-S04-115a` reserved to fix it.

## 6. Owner acceptance

**Confirmed — 2026-09-03.**

The product owner reviewed this report and PR #99, merged it into `main` (commit `a204cc2`), and explicitly confirmed acceptance in conversation ("Провел"). `SLICE-04 — Characters and Progression` and roadmap Milestone `M5`/`GATE-C — Character Playable` are closed. `ODY-S04-115a` (the `HistoryEventTypes` completeness gap, section 1a) remains open as a separate, non-blocking follow-up.
