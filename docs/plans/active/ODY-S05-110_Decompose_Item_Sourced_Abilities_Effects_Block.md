# ODY-S05-110 — Decompose Item-Sourced Abilities/Effects Block

**Status:** In Progress
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-110-decompose-abilities-effects-block`
**Pull request:** Not opened
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Decompose the reserved "item-sourced abilities/effects runtime" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 — the fourth such decomposition in this backlog, following `ODY-S05-107`/`108`/`109`, but structurally different from all three: `ADR-027` §8 specifies only item/`ActiveEffect` integration, never the `ActiveEffect` aggregate itself, so this decomposition splits the block into one ADR-specification task (`ODY-S05-501`) plus a deliberately-undecomposed remainder. Planning-only: no product code, no tests, no ADR edits (writing the ADR itself is `ODY-S05-501`'s own job).

## 2. Task contract

- Goal: add the `ODY-S05-110` task contract/Brief plan and update `SLICE-05_IMPLEMENTATION_BACKLOG.md` so the item-sourced abilities/effects block is decomposed out of §8's reserved list into a new ordered section (§14/§14.1) naming exactly one task, `ODY-S05-501`.
- Acceptance criteria: "item-sourced abilities/effects runtime" bullet removed from §8 (full attack pipeline stays); §14 quotes `ADR-027` §8.1/§8.2 verbatim and names the specific architectural gaps that make it insufficient to decompose directly (unlike §10 for migration); `ODY-S05-501` is the only decomposed task, with an explicit textual boundary against Block 3 (full attack pipeline); the "no `ActiveEffect` type exists / no other ADR mentions it" finding verified by direct search, not assumed; `502` onward explicitly recorded as reserved-but-unscoped, not decided by this revision; no product code/schema/test implementation or accepted ADR changes; Draft PR states planning/decomposition only, with no ADR content.
- Requirement IDs: `ODY-S05-110`, `SLICE-05`.
- In scope: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan.
- Out of scope: product code, schema, migrations, runtime tests, Unity/UI, any ADR document (that is `501`'s own job), decomposing `502` onward into real task IDs.
- Required authorities: `ODY-S05-107`/`108`/`109`'s own task contracts/plans (structural template), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §4/§8/§9/§10/§11/§13/§13.1, `ADR-027` §8/§9/§11/§12, `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Required validation commands: `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed; branch `feat/ody-s05-110-decompose-abilities-effects-block` created from `origin/main`.
- `origin/main` is at commit `1dd40fe` (merge of PR #133, `ODY-S05-404`) — the `ItemDefinition` migration block (`401`–`404`) is fully merged and closed.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 named two reserved/undecomposed blocks before this task: item-sourced abilities/effects runtime, and the full attack pipeline. §14/§14.1 were unused — the document ended at §13.1.
- `ADR-027` §8.1/§8.2 fully re-read and quoted verbatim in this task's own contract §4. Central finding: unlike §10 (migration), §8 never specifies the `ActiveEffect` aggregate's own field shape, persistence contract, stacking-rule integration, non-`WhileItemEquipped` duration-expiry mechanism, removal command, or authorization model — only how it integrates with items/Characters.
- Repository-wide search for `class ActiveEffect`/`struct ActiveEffect`/`interface IActiveEffect` across every tracked `.cs` file: zero matches. Repository-wide search of `docs/adr/*.md` for `"ActiveEffect"`: matches only in `ADR-027`. Both confirmed by direct search, not assumed.
- `Ability.cs`'s `SourceKind` enum and `TypedDefinitions.cs`'s `EffectDefinition`/`ItemDefinition.BuiltInAbilityRefs`/`BuiltInEffectRefs` doc comments read directly and quoted verbatim in this task's own contract §4 — all three explicitly frame themselves as placeholders for a future `ActiveEffect` aggregate/integration task, none implementing it.

Assumptions: none.

## 4. Proposed approach

Update only planning docs:

- Replace the "item-sourced abilities/effects runtime" bullet in §8's reserved-but-undecomposed list with a pointer to the new §14 (the full attack pipeline bullet stays, unchanged).
- Add new §14 "Ordered backlog (Item-sourced abilities/effects — ADR foundation)" after §13.1, at the same structural depth as §7/§12/§13: an intro naming the central finding (§8 is incomplete as a specification, unlike §10), `ADR-027` §8.1/§8.2 quoted verbatim, the direct-search evidence for the "no `ActiveEffect` type/no other ADR mentions it" claim, and a table containing exactly one task (`ODY-S05-501`); §14.1 "Item-sourced abilities/effects task boundaries" naming `501`'s own scope and explicitly stating that `502` onward is a future backlog revision.
- Reserve `ODY-S05-501`–`ODY-S05-50N` (an open range, count not yet fixed):
  1. **ADR Addendum — ActiveEffect Aggregate Specification** — a new ADR document (addendum to `ADR-027` or a new `ADR-028`, executor's choice, justified in the PR) specifying the `ActiveEffect` aggregate's field shape/persistence contract, its integration with the existing `EffectStackPolicy` vocabulary, how each `EffectDurationType` value expires (not only `WhileItemEquipped`), the removal command/authorization model, and an explicit boundary against Block 3 (full attack pipeline) — generic mechanism and item integration only, not combat/damage-sourced effects. No production code.
- Add small additive sentences to §9 (non-goals: split the stale combined "item use, ActiveEffect execution... (section 8)" line into two, one pointing at `501`/§14, one keeping the attack pipeline at §8; add "implementing the item-sourced abilities/effects block under `ODY-S05-110` itself; section 14 only decomposes one ADR-specification task"), §10 (dependency-rule bullet for `501`), and §11 (extend the reserved-number-range sentence to include `ODY-S05-501`–`ODY-S05-50N`, explicitly noting only `501` is decomposed) — no rewrite of existing sentences.
- Record this task contract and validation evidence.

No production code, no Unity/UI, no new persistence table, no new `ErrorCode`, no ADR change.

## 5. Milestones

### M1 — Backlog and task docs

- [x] Add `docs/tasks/active/ODY-S05-110_Decompose_Item_Sourced_Abilities_Effects_Block.md`.
- [x] Add this Brief plan.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (§8 bullet replacement, new §14/§14.1, additive §9/§10/§11 sentences).

### M2 — Validation and PR

- [ ] Review diff for docs-only scope.
- [ ] Run `dotnet test DotNet\Odyssey.Core.sln`.
- [ ] Run `.\scripts\verify-format.ps1`.
- [ ] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Run `.\scripts\verify-test-structure.ps1`.
- [ ] Commit, push, and open Draft PR.

## 6. Progress log

- 2026-09-12 — Preflight: fetched `origin`, created branch from `origin/main` (`1dd40fe`), re-read `ODY-S05-109`'s task contract/plan as the structural template.
- 2026-09-12 — Re-read `ADR-027` §8.1/§8.2 in full; confirmed via direct search that no `ActiveEffect` type exists anywhere in code and no ADR besides `ADR-027` mentions `ActiveEffect`; read `Ability.cs`'s `SourceKind` enum and `TypedDefinitions.cs`'s `EffectDefinition`/`BuiltInAbilityRefs`/`BuiltInEffectRefs` doc comments directly, confirming all three explicitly defer `ActiveEffect` to a future task.
- 2026-09-12 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` (§8 bullet replacement, new §14/§14.1, additive §9/§10/§11 sentences).

## 7. Decisions

See task contract §18 for the full decision log.

## 8. Discoveries and deviations

- To be updated as validation proceeds.

## 9. Validation and acceptance evidence

- `dotnet test DotNet\Odyssey.Core.sln`: pending.
- `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `.\scripts\verify-test-structure.ps1`: pending.
- Diff review: pending.

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR before merge. No production code, schema, or ADR is touched.

## 11. Open questions and blockers

None remain open for this task. `ODY-S05-501`'s own future ADR must resolve: the `ActiveEffect` aggregate's exact field shape/persistence; its integration with `EffectStackPolicy`; how non-`WhileItemEquipped` durations expire; the removal command/authorization model; and the Block 3 boundary — none of these are resolved here, by design.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned task: `ODY-S05-501` — ADR Addendum: ActiveEffect Aggregate Specification.
