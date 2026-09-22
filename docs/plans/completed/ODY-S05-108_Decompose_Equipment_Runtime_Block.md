# ODY-S05-108 — Decompose Equipment Runtime Block

**Status:** Done (PR #122, merged into main)
**Owner:** Codex (agent)
**Branch:** `docs/ody-s05-108-decompose-equipment-runtime-block`
**Pull request:** Not opened
**Last updated:** 2026-09-11 UTC

## 1. Purpose and user-visible outcome

Prepare the next `SLICE-05` implementation wave after the Inventory runtime block's closure by decomposing the reserved Equipment runtime block into small, reviewable future tasks. The result is a planning-only backlog update and task contract, not product code — exactly mirroring `ODY-S05-107`'s own decomposition of the Inventory runtime block.

## 2. Task contract

- Goal: add the `ODY-S05-108` task contract/Brief plan and update `SLICE-05_IMPLEMENTATION_BACKLOG.md` so Equipment runtime is decomposed out of §8's reserved list into a new ordered section.
- Acceptance criteria: Equipment runtime removed from §8's undecomposed list; decomposed into small implementation tasks (`ODY-S05-301`–`306`), each with purpose/dependencies/planning mode/in-scope/out-of-scope; `ADR-027` §5/§7 boundaries preserved (Inventory-owned location state, `EquippedEntry` shape, all six rules); `DeleteCharacterPermanently` equipment-dependency question explicitly resolved with code evidence, not assumed; `RemoveBodyPart` closure given an explicit task ID; no product code/schema/test implementation or accepted ADR changes; Draft PR states planning/decomposition only.
- Requirement IDs: `ODY-S05-108`, `SLICE-05`.
- In scope: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan; optionally (pre-authorized) relocating `ODY-S05-107`'s own task contract/plan to `completed/`.
- Out of scope: product code, schema, migrations, runtime tests, Unity/UI, real Equip/Unequip commands, `RemoveBodyPart`'s real check, `DeleteCharacterPermanently` changes, ActiveEffect/ItemDefinition-migration/attack-pipeline decomposition, accepted ADR edits.
- Required authorities: `ODY-S05-107`'s own task contract/plan (structural template), `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7/§7.1/§8/§9/§10/§11, `ADR-027` §5/§6/§7/§9, `AGENTS.md`, `PLANS.md`, `TASK_TEMPLATE.md`.
- Required validation commands: `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`; `.\scripts\verify-test-structure.ps1`.

## 3. Current state

- `git fetch origin` completed; branch `docs/ody-s05-108-decompose-equipment-runtime-block` created from `origin/main`.
- `origin/main` is at commit `ef5a57f` (SLICE-05-205/206/207 doc-sync, itself following the merge of PR #119/#120/#121).
- `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names "Equipment runtime" as reserved/undecomposed; this task removes it and adds §12/§12.1.
- `ADR-027` §5/§7/§9 fully re-read; no contradiction found. §7's `EquippedEntry` shape and six rules are the normative target this decomposition sequences toward.
- Direct source read confirms: no `EquippedEntry`/Equip/Unequip/Equipment persistence exists anywhere; `InventoryLocationRef.Equipped` is a bare location-kind marker; `ArmorDefinition.EquipmentSlotKey`/`CoveredBodyPartIds` are catalog metadata, not runtime state; `RemoveBodyPart`'s item/equipment dependency check remains an open, documented stub; `InventoryCharacterDeletionDependencyChecker`'s `HasAnyItemOwnedByCharacter` query is `OwnerRef`-only (no `LocationKind` filter) and therefore already covers equipped items, since equipping never changes `OwnerRef`.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` is not present in this tracked checkout (`Documentation/` is on `check-repository-policy.ps1`'s forbidden-tracked-path list — intentionally private/untracked); no Equipment-specific requirement beyond `ADR-027` §7 could be verified from the tracked repository.

Assumptions: none.

## 4. Proposed approach

Update only planning docs:

- Remove the "Equipment runtime" bullet from §8's reserved-but-undecomposed list.
- Add new §12 "Ordered backlog (Equipment runtime block)" after §11, at the same structural depth as §7: an intro naming the `ADR-027` §7 `EquippedEntry` shape and the six rules it preserves, a table of six tasks (`ODY-S05-301`–`306`), and §12.1 "Equipment runtime task boundaries" (one paragraph per task ID, mirroring §7.1's style).
- Reserve `ODY-S05-301`–`306`:
  1. **Equipment Runtime Foundation** — domain type(s) for `EquippedEntry` (or a typed extension of `InventoryLocationRef.Equipped`) carrying `BodyPartRefs[]`/`EquippedByUserId`/`EquippedAt`/`Revision`. No persistence, no commands.
  2. **Equipment Persistence Foundation** — schema/read-write primitives, CAS pattern mirroring `ODY-S05-202`/`204`. No command semantics.
  3. **Equip Command MVP** — MainGM-only contained→equipped transition; must enforce rule 4 (body parts must currently exist).
  4. **Unequip Command MVP** — reverse transition per rule 3.
  5. **RemoveBodyPart Dependency Closure** — finally closes the real stub per rule 5.
  6. **Equipment Runtime Integration Fixtures** — mirrors `ODY-S05-207`: Equip → `RemoveBodyPart` rejected while equipped → Unequip → `RemoveBodyPart` succeeds.
- Explicitly record, with code evidence, that no separate `DeleteCharacterPermanently` sub-task is needed (§18 decision log in the task contract; brief cross-reference in §12.1).
- Add small additive sentences to §9 (non-goals: "implementing Equipment runtime under `ODY-S05-108` itself; section 12 only decomposes future implementation tasks"), §10 (dependency-rule bullets for `301`–`306`), and §11 (extend the reserved-number-range sentence to include `ODY-S05-301`–`306`) — no rewrite of existing sentences.
- (Pre-authorized housekeeping) relocate `ODY-S05-107`'s own task contract and Brief plan from `active/` to `completed/`, updating only their `Status:` lines to `Done (PR #112, merged into main)`.
- Record this task contract and validation evidence.

No production code, no Unity/UI, no new persistence table, no new `ErrorCode`, no `ADR` change.

## 5. Milestones

### M1 — Backlog and task docs

- [x] Add `docs/tasks/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md`.
- [x] Add this Brief plan.
- [x] Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (§8 removal, new §12/§12.1, additive §9/§10/§11 sentences).
- [x] Relocate `ODY-S05-107`'s task contract/plan to `completed/` with an updated Status line (housekeeping).

### M2 — Validation and PR

- [x] Review diff for docs-only scope.
- [x] Run `.\scripts\verify-format.ps1`.
- [x] Run `.\scripts\check-repository-policy.ps1`.
- [x] Run `.\scripts\verify-test-structure.ps1`.
- [x] Commit, push, and open Draft PR.

## 6. Progress log

- 2026-09-11 — Preflight: fetched `origin`, created branch from `origin/main` (`ef5a57f`), re-read `ODY-S05-107`'s task contract/plan as the structural template.
- 2026-09-11 — Re-read `ADR-027` §5/§6/§7/§9 in full; verified directly in source that `InventoryLocationRef.Equipped`, `ArmorDefinition`, `BodyPart`, and `RemoveBodyPart` are exactly as described, and that no `EquippedEntry`/Equip/Unequip surface exists anywhere (`git grep -il equipment`).
- 2026-09-11 — Verified `InventoryCharacterDeletionDependencyChecker`/`HasAnyItemOwnedByCharacter` directly: the SQLite query has no `LocationKind` filter, so it already covers equipped items; recorded this as the required decision-log entry and used it to exclude a redundant sub-task.
- 2026-09-11 — Updated `SLICE-05_IMPLEMENTATION_BACKLOG.md`, added this task contract and Brief plan, performed the `ODY-S05-107` housekeeping relocation.
- 2026-09-11 — Validation passed: `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`.
- 2026-09-11 — Opened Draft PR.

## 7. Decisions

- 2026-09-11 — Decision: use Brief plan, not ExecPlan. Rationale: docs/planning-only; no public runtime contract, schema, permission behavior, production code, or ADR change. Authority: `PLANS.md` §1.1, identical to `ODY-S05-107`.
- 2026-09-11 — Decision: reserve `ODY-S05-301`–`306` for the Equipment runtime block (foundation, persistence, Equip, Unequip, `RemoveBodyPart` closure, integration fixtures) — six tasks, no separate `DeleteCharacterPermanently` sub-task. Rationale: `ADR-027` §5/§7/§9, plus the direct code finding that `ODY-S05-206`'s checker already covers equipped items. Authority: this session's own source verification.
- 2026-09-11 — Decision: extend the backlog document additively (new §12/§12.1; additive sentences in §9/§10/§11) rather than renumber §8–§11 into §9–§12. Rationale: the governing ТЗ explicitly forbids rewriting §3's already-recorded decisions and, by extension, any invasive renumbering that would touch unrelated existing text across multiple sections. Authority: this ТЗ §5.
- 2026-09-11 — Decision: perform the pre-authorized `ODY-S05-107` housekeeping relocation. Rationale: its own PR #112 has been merged since commit `3cd44ea`; the files were simply never moved. Authority: this ТЗ §0.
- 2026-09-11 — Decision: do not edit `docs/adr/**`. Rationale: no contradiction found; the open `ODY-S05-206`-§18 amendment question remains the product owner's decision. Authority: user out-of-scope instruction.

## 8. Discoveries and deviations

- `DeleteCharacterPermanently`'s equipment dependency (`ADR-027` §9.2) is already closed by `ODY-S05-206` — a genuine discovery requiring direct code verification (this ТЗ flagged it as a hypothesis to check, not a given). Documented with file/line evidence in the task contract §4/§18.
- `InventoryDeletionDependencyCheckerTests.cs` does not name `InventoryLocationRef.Equipped` explicitly in its own test cases, even though the query it exercises is provably location-agnostic. Recorded as a non-blocking, optional future observation — not a required sub-task, since the current tests already prove the query's shape is `OwnerRef`-only via the Contained/SceneDropped cases it does cover.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md`, cited as an authority by prior `SLICE-05` task contracts, is not present in this tracked checkout; confirmed this is an intentional repository-policy exclusion (`Documentation/` is forbidden-tracked), not a missing/broken reference. `ADR-027` remains the sufficient, available authority for this decomposition.
- No architecture contradiction was found in `ADR-027`.

## 9. Validation and acceptance evidence

- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed with `Repository policy check passed`.
- `.\scripts\verify-test-structure.ps1`: passed with `TC-ARCH-001 PASS valid ADR-001 graph passes` and controlled invalid cases rejected.
- Diff review: changed only `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan, and the `ODY-S05-107` relocation. No product code, schema, tests, Unity files, or accepted ADR files changed.

## 10. Recovery and rollback

Rollback is a normal revert of this documentation-only branch. No schema, migration, build artifact, dependency, or runtime state is changed.

## 11. Open questions and blockers

None for this task. Recorded for the product owner (not resolved here): whether `ADR-027` §9 should gain a short amendment note cross-referencing this decomposition and `ODY-S05-206`'s own closure finding, per the open question already raised in `ODY-S05-206`'s §18.

## 12. Outcome and follow-up

Draft PR opened for `ODY-S05-108`. Follow-up implementation tasks reserved: `ODY-S05-301` through `ODY-S05-306`.
