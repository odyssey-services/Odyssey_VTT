# Brief plan — ODY-S05-112: Decompose Full Attack Pipeline Block (Block 3)

**Task contract:** `docs/tasks/active/ODY-S05-112_Decompose_Full_Attack_Pipeline_Block.md`
**Planning mode:** Brief plan (per `PLANS.md` §1.1 — docs/planning-only task, no runtime code, no public contract, no ADR change).

## 1. What this task does

Decompose the reserved-but-undecomposed "full attack pipeline" block (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §8) into a two-tier structure, mirroring `ODY-S05-110`'s own decomposition of the item-sourced abilities/effects block:

1. One activated task now: `ODY-S05-601` — an ADR-specification task (produces a document, not code).
2. An explicitly reserved, explicitly not-yet-decomposed remainder (`ODY-S05-602`–`ODY-S05-60N`), left to a future backlog revision once `601`'s ADR is accepted.

This mirrors `110`/`111`'s own precedent even more strongly than `110` mirrored anything before it: `110` found *partial* ADR coverage (`ADR-027` §8) to decompose around; this task finds **zero** ADR coverage at all for the full attack pipeline — only exclusion language in `ADR-027`/`ADR-028`, and one secondary historical quote in the closed `SLICE-05_BACKLOG.md` §3.2.

## 2. Steps

1. Independently verify (not trust from the ТЗ) that:
   - No ADR exists under `docs/adr/` for the attack pipeline.
   - `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` (cited by `ADR-027` as the scope source) does not exist anywhere in the tracked repository.
   - `ADR-027` names the pipeline only as an exclusion, at exactly three sites (§1 rule 13, §13 Non-goals, §18 Deferred-but-not-open).
   - `ADR-028` §13/§14 are the only accepted-ADR text with any real integration content (six reserved `EffectDurationType` values; "how a combat roll decides to apply an effect" left undecided).
   - `docs/tasks/SLICE-05_BACKLOG.md` §3.2 is the only in-repo text enumerating the pipeline's own named parts (quote verbatim, do not paraphrase).
2. Edit `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`:
   - §8: remove the "full attack pipeline" reserved-but-undecomposed bullet; point to new §16.
   - §9: add a non-goals bullet for this task (mirroring `110`'s own analogous bullet); fix only the one cross-reference that specifically concerned the pipeline's own future section number; leave every unrelated pre-existing stale cross-reference untouched.
   - §10: add a dependency-rule line for `ODY-S05-601`.
   - §11: reserve `ODY-S05-601`–`ODY-S05-60N` as an explicitly open range (mirroring `501`-`50N`'s own original precedent, not `401`-`404`'s closed one).
   - §14/§15: correct only the specific cross-references that named the pipeline's own future decomposition location (now §16), as a direct consequence of the §8 edit — not unrelated drift.
   - Append new §16 (ordered backlog, one row: `ODY-S05-601`) and §16.1 (task boundaries), mirroring §14/§14.1's structure and depth.
3. Write this task's own contract (`ODY-S05-112_*.md`), full 18 sections, citing all verified evidence directly.
4. Write `ODY-S05-601`'s own task contract — Status `Proposed`, requirements-only (no architecture decided), explicitly requiring the future ADR to ground itself in `SLICE-05_BACKLOG.md` §3.2 and `ADR-028` §13/§14, and to raise the missing roadmap file as an open product-owner question if genuinely needed rather than inventing its content.
5. Run required validation commands (`dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`).
6. Confirm `git diff --name-status` against `main` is confined to the four allowed doc paths.
7. Commit (implementation commit), push, open Draft PR (never merge). Poll CI.
8. Doc-sync follow-up commit if any header/status placeholder needs the real PR number.
9. Clean up worktree once CI is confirmed green.
10. Deliver final Russian-language report.

## 3. Risks and mitigations

- **Risk:** accidentally deciding pipeline architecture while writing `601`'s own contract. **Mitigation:** every substantive question (turn/round structure, conveyor steps, `ActiveEffect` integration) is phrased strictly as a requirement on the future ADR, never as this contract's own decision — checked against Scenario 4 in the task contract.
- **Risk:** inventing content from the non-existent roadmap file. **Mitigation:** cite only `SLICE-05_BACKLOG.md` §3.2's own verbatim quote and `ADR-028` §13/§14's own boundary text; explicitly flag the missing file as an open question for `601`'s own future executor.
- **Risk:** over-fixing unrelated stale cross-references beyond this task's own scope. **Mitigation:** fix only cross-references that specifically named the pipeline's own future section number as a direct consequence of the §8 edit; leave all other stale references (e.g. the unrelated ItemDefinition migration §9 bullet) untouched, per `110`'s own established discipline.

## 4. Validation plan

Same four commands as every other task this session: `dotnet test DotNet\Odyssey.Core.sln`, `.\scripts\verify-format.ps1`, `.\scripts\check-repository-policy.ps1`, `.\scripts\verify-test-structure.ps1`. No new tests are added (docs-only task); the full suite is run only to confirm the docs-only diff leaves it green.
