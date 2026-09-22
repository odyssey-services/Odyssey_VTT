# ODY-S05-501 — ADR Addendum: ActiveEffect Aggregate Specification

**Status:** Done (PR #135, merged into main)
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-501-activeeffect-adr`
**Pull request:** [odyssey-services/Odyssey_VTT#135](https://github.com/odyssey-services/Odyssey_VTT/pull/135) (Draft)
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Produce a new ADR (`docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`) specifying the `ActiveEffect` aggregate itself — the gap `ODY-S05-110` found in `ADR-027` §8. No production code; the ADR document is the entire deliverable, per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1's own governing boundary for this task.

## 2. Task contract

- Goal: write `ADR-028`, resolving all 8 required decisions named in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14's own primary-result column for `ODY-S05-501` (aggregate shape/persistence, `EffectStackPolicy` integration, `EffectDurationType` integration, removal command, permissions, fail-closed behavior, Block 3 boundary, atomicity); update the task contract/Brief plan; flip `501`'s own backlog status cell to `In Review`.
- Acceptance criteria: all 8 decisions closed explicitly (not by omission); aggregate form matches `ADR-027` §7's own `EquippedEntry` precedent style; every one of `EffectStackPolicy`'s 7 and `EffectDurationType`'s 15 values individually addressed; permission decision explicitly chooses between `ADR-025` §5.1/§5.2's two existing patterns, justified per operation; `ADR-024`/`CharacterAbility` citation inaccuracy not repeated; `502` onward not decomposed; only `501`'s backlog status cell changes; no code anywhere in the diff; Draft PR.
- Requirement IDs: `ODY-S05-501`, `SLICE-05`.
- In scope: `docs/adr/ADR-028_...md`, this task contract, this Brief plan, one backlog status cell.
- Out of scope: any code, decomposing `502+`, amending `ADR-027` or any other existing ADR, any other backlog edit.
- Required authorities: `ADR-027` §7/§8.1/§8.2/§11/§12/§14; `ADR-025` §5.1/§5.2; `ADR-024` (searched, not assumed); `ADR-012` §5; `SqliteSavingPipeline.cs`; `Ability.cs`; `TypedDefinitions.cs`; `InventoryRuntime.cs`'s `InventoryItemRef`; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14/§14.1.
- Required validation commands: `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed; branch `feat/ody-s05-501-activeeffect-adr` created from `origin/main` at `581c980` (merge of PR #134, `ODY-S05-110`).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1 (`501`) was `Proposed`. Next free ADR number confirmed as `028` by listing `docs/adr/`.
- `ADR-027` §7 (`EquippedEntry`), §8.1/§8.2, §11, §12, §14 fully re-read; `ADR-025` §5.1/§5.2 fully re-read; `ADR-024` searched directly for `"CharacterAbility"` (zero matches, confirming the citation-inaccuracy claim); `SqliteSavingPipeline.cs`'s doc comment re-read for exact wording; `Ability.cs`'s `SourceKind` and `TypedDefinitions.cs`'s `EffectDurationType`/`EffectStackPolicy`/`EffectDefinition` re-read; repository-wide searches confirmed no `ActiveEffect`/`SceneObject`/turn-round-tracking type exists anywhere in the codebase.

Assumptions: none.

## 4. Proposed approach

Write one new ADR document, structured to mirror `ADR-027`'s own section numbering (Decision → Context → Terms → per-topic decision sections → Alternatives → Open questions → Traceability → Normative action), scoped as an extension of `ADR-027` §8.1/§8.2 rather than a full independent ADR that re-derives everything from scratch:

- **Aggregate shape** (§5 of the new ADR): `ActiveEffect` minimum record + numbered rules, in `ADR-027` §7's own `EquippedEntry` style — `ActiveEffectId`/`CampaignId`/`EffectDefinitionRef`/`EffectMechanicsSnapshot`/`SourceRef`/`TargetRef`/`Status`/`StackCount`/`AppliedByUserId`/`AppliedAt`/`ExpiresAt`/`Revision`.
- **Persistence** (§6): new `IActiveEffectRepository`/`SqliteActiveEffectRepository`, not an `IInventoryRepository` extension, per `ADR-027` §8.2 rule 2's own "not Inventory/Character-owned" text.
- **Stacking** (§7): all 7 `EffectStackPolicy` values individually specified, including a new lightweight `ActiveEffectStackConflict` pending record + `ResolveActiveEffectStackConflict` command for `RequestGMResolution`, and delegating `ReplaceIfStronger`'s comparison to `Odyssey.Rules` over the opaque payload rather than inventing a new potency field.
- **Duration/expiry** (§8): all 15 `EffectDurationType` values in one table — mechanism, or explicit "no persisted row" (`Instant`), or explicit "reserved for the full attack pipeline block" (the 6 turn/round-based values) — confirmed by direct search that no turn/round infrastructure exists today.
- **Removal** (§9): explicit `RemoveActiveEffect`, CAS-guarded, `Status → Removed`, never physical deletion (`ADR-012` append-only-history discipline).
- **Permissions** (§10): item-triggered creation inherits the existing item-use model (`ADR-025` §5.1 pattern); direct creation and explicit removal are MainGM-only (`ADR-025` §5.2 pattern) — two different rules, explicitly justified, not one uniform choice.
- **Fail-closed** (§11): an inconclusive expiry check never silently expires the effect, reusing `ADR-025` §5.2's fail-closed precedent in the opposite direction (favor keeping Active, not favor blocking a delete).
- **Atomicity** (§12): future `SqliteActiveEffectRepository` must reuse the shared `SqliteSavingPipeline` (`ADR-012` §5), already adopted by 5 repositories.
- **Block 3 boundary** (§13): explicit list of what's reserved (6 duration values, combat-roll-triggered application, to-hit/damage-roll interaction).
- **Citation correctness** (§4 of the new ADR): explicitly note, without repeating or amplifying, `ADR-027`'s own `ADR-024`/`CharacterAbility` citation inaccuracy.

Update `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1's status cell only. Write the task contract and this plan with full decision-log evidence.

No production code, no Unity/UI, no other ADR edited, no `502+` decomposition.

## 5. Milestones

### M1 — Research and verification

- [x] Re-read `ADR-027` §7/§8.1/§8.2/§11/§12/§14 in full.
- [x] Re-read `ADR-025` §5.1/§5.2 in full.
- [x] Verify the `ADR-024`/`CharacterAbility` citation-inaccuracy claim by direct search.
- [x] Re-read `SqliteSavingPipeline.cs`'s doc comment and confirm its current adoption count.
- [x] Re-read `Ability.cs`'s `SourceKind` and `TypedDefinitions.cs`'s `EffectDurationType`/`EffectStackPolicy`/`EffectDefinition`.
- [x] Confirm no `ActiveEffect`/`SceneObject`/turn-round-tracking type exists anywhere, by direct search.

### M2 — Write the ADR

- [x] Write `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, resolving all 8 required decisions.
- [x] Verify every one of `EffectStackPolicy`'s 7 and `EffectDurationType`'s 15 values is individually named.

### M3 — Task docs, validation, PR

- [x] Update `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1's status cell only.
- [x] Write this task contract and Brief plan with full decision-log evidence.
- [x] Run `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`.
- [x] Review diff for scope.
- [x] Commit, push, and open Draft PR.
- [x] Record PR link and backlog status.

## 6. Progress log

- 2026-09-12 — Preflight: fetched `origin`, created branch from `origin/main` (`581c980`), confirmed next free ADR number is `028`.
- 2026-09-12 — Re-read every required authority listed in §4/M1 above; verified the `ADR-024`/`CharacterAbility` citation inaccuracy directly (`grep -c` returns 0); confirmed no `ActiveEffect`/`SceneObject`/turn-round type exists anywhere.
- 2026-09-12 — Wrote `ADR-028`, resolving all 8 required decisions; wrote this task contract and Brief plan with the full decision log.
- 2026-09-12 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14 row 1's status cell only.

## 7. Decisions

See task contract §18 for the full decision log: standalone `ADR-028` vs. addendum; standalone `IActiveEffectRepository`; two-tier permission rule; `ReplaceIfStronger` delegated to `Odyssey.Rules`; `RequestGMResolution`'s new pending-record shape; the 6 Block-3-reserved duration values; `Instant` creates no persisted row; fail-closed expiry; shared `SqliteSavingPipeline` reuse; the `ADR-024` citation-correctness decision.

## 8. Discoveries and deviations

- Confirmed `EffectDurationType` truly has exactly 15 values and `EffectStackPolicy` exactly 7 by direct enumeration of `TypedDefinitions.cs` (matching the ТЗ's own counts, not merely trusted from the ТЗ text).
- Confirmed `SqliteInventoryRepository`'s own `ODY-S05-403` migration-apply path is the most recent adopter of `SqliteSavingPipeline`, making 5 total repositories using it as of this task — one more than could have been cited from `ADR-027`'s own original text alone, since `ODY-S05-403` postdates `ADR-027`.

## 9. Validation and acceptance evidence

- `dotnet build`/`dotnet test DotNet\Odyssey.Core.sln`: PASS — Contracts 1/1, Domain 80/80, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 534/534.
- `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `.\scripts\verify-test-structure.ps1`: all PASS.
- Diff review: `git diff --name-status` confirmed only §5's allowed paths changed; PR #135 opened as Draft.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. No production code, schema, or other ADR is touched.

## 11. Open questions and blockers

None remain open for this task. `ADR-028` §19 records what remains deferred (turn/round duration mechanism, combat-triggered application, `ODY-S05-502`+ decomposition, `ADR-027`'s own citation-correction) — none of these are open questions of this task's own, only recorded forward-references.

## 12. Outcome and follow-up

Draft PR to be opened. Once accepted, a future backlog revision (not this task) decomposes `ODY-S05-502` onward into concrete `ActiveEffect` implementation tasks.
