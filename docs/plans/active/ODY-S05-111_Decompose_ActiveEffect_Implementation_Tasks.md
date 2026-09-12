# ODY-S05-111 — Decompose ActiveEffect Implementation Tasks (502+)

**Status:** In Progress
**Owner:** Codex (agent)
**Branch:** `feat/ody-s05-111-decompose-activeeffect-implementation`
**Pull request:** Not opened
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Decompose the item-sourced abilities/effects block's implementation range (`ODY-S05-502` onward) into 6 small implementation tasks, now that `ADR-028` (`ODY-S05-501`) is `Accepted`. Unlike `ODY-S05-110`'s own two-tier decomposition (forced by `ADR-027` §8's own incompleteness), this task decomposes straight into implementation tasks, mirroring `ODY-S05-109`'s own relationship to `ADR-027` §10. Planning-only: no product code, no tests, no ADR edits, no task-contract files for `502`–`507` themselves.

## 2. Task contract

- Goal: add the `ODY-S05-111` task contract/Brief plan and update `SLICE-05_IMPLEMENTATION_BACKLOG.md` so the implementation range is decomposed into a new ordered section (§15/§15.1).
- Acceptance criteria: `502`–`507` decomposed with purpose/dependencies/planning mode/scope; §15.1 gives each task a non-overlapping boundary covering all of `ADR-028` §5-§13; turn/round-based `EffectDurationType` values and combat-triggered effects never appear as any task's scope; `503`–`506` depend on `502`, `507` depends on `502`-`506`; §9/§10/§11/§14.1 updated point-correctively, §1–§8/§12/§13 untouched in substance; no code; no `502`–`507` task-contract files created; Draft PR.
- Requirement IDs: `ODY-S05-111`, `SLICE-05`.
- In scope: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan.
- Out of scope: product code, schema, tests, Unity/UI, `502`–`507`'s own task-contract files, `docs/adr/**` edits, turn/round-based durations or combat effects in any task's own scope.
- Required authorities: `ODY-S05-109`/`110`'s own task contracts/plans (structural templates), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §9/§10/§11/§13/§13.1/§14/§14.1, `ADR-027` §8.1/§8.2, `ADR-028` (full), `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Required validation commands: `dotnet test DotNet\Odyssey.Core.sln`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed; branch `feat/ody-s05-111-decompose-activeeffect-implementation` created from `origin/main` at `45cba26` (merge of PR #135, `ODY-S05-501`) — `ADR-028` is `Accepted`.
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` §14.1 (before this task) explicitly deferred decomposing `502` onward until `ADR-028` was accepted — that precondition is now satisfied.
- `ADR-028` §5-§13 fully re-read: aggregate/persistence (§5/§6), 7 `EffectStackPolicy` values (§7), 15 `EffectDurationType` values — 9 presently implementable + `WhileItemEquipped` already solved + 6 reserved for Block 3 (§8), removal (§9), 2-rule permission model (§10), fail-closed (§11), mandatory `SqliteSavingPipeline` reuse (§12), Block 3 boundary (§13).

Assumptions: none.

## 4. Proposed approach

Update only planning docs:

- Add new §15 "Ordered backlog (Item-sourced abilities/effects — implementation)" after §14.1, at the same structural depth as §7/§12/§13: an intro naming that `ADR-028`'s acceptance now permits direct decomposition (unlike `110`'s own two-tier approach), a table of 6 tasks (`ODY-S05-502`–`507`), and §15.1 "Item-sourced abilities/effects implementation task boundaries" (one paragraph per task, mirroring §7.1/§12.1/§13.1's own style), closing with an explicit restatement of the Block 3 boundary.
- Reserve `ODY-S05-502`–`507`:
  1. **ActiveEffect Foundation** — aggregate + standalone repository, CRUD only (`ADR-028` §5/§6/§12).
  2. **Stacking Policy Resolution** — all 7 `EffectStackPolicy` behaviors (`ADR-028` §7), consuming `502`.
  3. **Non-Combat Duration/Expiry** — 9 presently-implementable `EffectDurationType` mechanisms + fail-closed rule (`ADR-028` §8/§11), excluding `WhileItemEquipped` and the 6 Block-3-reserved values.
  4. **WhileItemEquipped Wiring + Item-Triggered Creation** — `ADR-027` §8.2 rule 3's own already-decided mechanism, plus item-triggered creation under the existing item-use permission model (`ADR-028` §10 rule 1).
  5. **RemoveActiveEffect Command + Direct-Creation Permission Gates** — both MainGM-only gates (`ADR-028` §9/§10 rules 2-3).
  6. **Integration Fixtures** — end-to-end proof mirroring `ODY-S05-207`/`306`/`404`. Brief plan, no production code.
- Point-correct §9 (add one non-goal bullet for this task; fix the stale "beyond `ODY-S05-501`" framing on the existing item-use/`ActiveEffect` bullet), §10 (6 new dependency-rule bullets), §11 (record `501`–`507` as fully decomposed), §14.1 (fix "a future backlog revision" to the fact of this revision) — no rewrite of unrelated existing sentences.
- Record this task contract and validation evidence.

No production code, no Unity/UI, no new persistence table, no new `ErrorCode`, no `ADR` change, no `502`–`507` task-contract files.

## 5. Milestones

### M1 — Backlog and task docs

- [x] Add `docs/tasks/active/ODY-S05-111_Decompose_ActiveEffect_Implementation_Tasks.md`.
- [x] Add this Brief plan.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (§9/§10/§11/§14.1 point corrections, new §15/§15.1).

### M2 — Validation and PR

- [ ] Review diff for docs-only scope.
- [ ] Run `dotnet test DotNet\Odyssey.Core.sln`.
- [ ] Run `.\scripts\verify-format.ps1`.
- [ ] Run `.\scripts\check-repository-policy.ps1`.
- [ ] Run `.\scripts\verify-test-structure.ps1`.
- [ ] Commit, push, and open Draft PR.

## 6. Progress log

- 2026-09-12 — Preflight: fetched `origin`, created branch from `origin/main` (`45cba26`), re-read `ODY-S05-109`/`110`'s task contracts/plans as the structural template.
- 2026-09-12 — Re-read `ADR-028` §5-§13 and `ADR-027` §8.1/§8.2 in full; confirmed the 6-task grouping against `ADR-028`'s own section boundaries before accepting the governing brief's own proposal.
- 2026-09-12 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md` (§9/§10/§11/§14.1 point corrections, new §15/§15.1).

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

None remain open for this task. The exact C# form of `IActiveEffectRepository`, `ActiveEffectStackConflict`, and the `UntilSceneChange`/`UntilSessionEnd` subscription events are left to each of `502`/`503`/`504`'s own future task contract, per `ADR-028`'s own conceptual (not literal-code) specification.

## 12. Outcome and follow-up

Draft PR to be opened. Next planned implementation task: `ODY-S05-502` — ActiveEffect Foundation.
