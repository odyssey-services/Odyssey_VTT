# ODY-S05-108 — Decompose Equipment Runtime Block

**Status:** Done (PR #122, merged into main)
**Roadmap stage / slice:** SLICE-05 (Equipment runtime planning block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `docs/ody-s05-108-decompose-equipment-runtime-block`
**Pull request:** Not opened
**Plan:** `docs/plans/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md` (Brief plan)
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Decompose the reserved "Equipment runtime" block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 into small, reviewable implementation tasks, exactly mirroring how `ODY-S05-107` decomposed the Inventory runtime block into `ODY-S05-201`–`207`. This task is docs/planning-only: it does not implement Equipment domain types, persistence, commands, tests, or Unity UI, and it does not edit any accepted ADR.

## 2. Why this task exists

- Problem or dependency being addressed: the Inventory runtime block (`ODY-S05-201`–`207`) is fully merged into `main`; `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names Equipment runtime as reserved but not decomposed into executable tasks.
- Value or risk reduction: gives future agents small, single-responsibility implementation contracts for Equipment state, Equip/Unequip commands, and the still-open `RemoveBodyPart` dependency-check stub, without pulling in ActiveEffect execution, ItemDefinition migration, or the attack pipeline.
- Blocking or enabling relationship: follows the merged Inventory runtime block (PR #119/#120/#121, doc-sync `ef5a57f`) and enables future `ODY-S05-301`–`306` task contracts.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` and `docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` — the structural template this task follows near one-to-one.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, especially §7/§7.1 (the Inventory runtime decomposition precedent), §8 (reserved future blocks, as it existed before this task), §9 (global non-goals), §10 (dependency rules), §11 (backlog change control / reserved number ranges).
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, full re-read of §5 (Inventory aggregate boundary — `EquippedEntries` already named as an `Inventory` field), §6 (ItemInstance/ItemStack snapshots), §7 (Equipment model, verbatim source for this decomposition), §9 (closure of `SLICE-04` stubs).
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §14 — cited as an authority by prior `SLICE-05` task contracts, but not present in this tracked checkout (see Verified facts) — `check-repository-policy.ps1`'s forbidden-path list confirms `Documentation/` is intentionally untracked/private in this repository.
- `Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs`, `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs`, `Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs` (`RemoveBodyPart`), `Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs`, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` (`HasAnyItemOwnedByCharacter`) — read in full to verify the current-state claims in §4 directly against source, not by paraphrase.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-108`, `SLICE-05`; `ADR-027` §7, §9.1.
- Existing test IDs: None directly changed by this planning-only task.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private documentation excerpts are added.

## 4. Verified current state

### Verified facts

- `git fetch origin` completed before branch creation; `origin/main` is at commit `ef5a57f` (the SLICE-05-205/206/207 doc-sync commit that followed the merge of PR #119/#120/#121).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 still names "Equipment runtime — inventory-owned location state over slots/body parts (`ADR-027` section 7)" as reserved and not decomposed; §7/§7.1 (Inventory runtime) and §11 (reserved number ranges, currently `ODY-S05-101`–`106` and `ODY-S05-201`–`207`) are unchanged from the previous session.
- `ADR-027` §7 (Equipment model), read in full and quoted verbatim in this task's Brief plan: `EquippedEntry` (`InventoryId`, `ItemRef`, `EquipmentSlotRef`, `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, `Revision`) and six numbered rules (one-place exclusivity; equip does not copy mechanics into Character; unequip returns to a valid location; equipment must reference body parts that currently exist; removing a body part is rejected while equipment/item state depends on it, unless the same future command atomically resolves the dependency; weapon/armor/ammo runtime state stays with the item/stack). No `EquippedEntry`-shaped type, table, or command exists anywhere in the codebase.
- `ADR-027` §5 already names `EquippedEntries` as a conceptual field of the `Inventory` aggregate (alongside `StackEntries`/`UniqueItemIds`), confirming Equipment state belongs on the existing Inventory aggregate root, not a new one.
- `ADR-027` §9.1/§9.2 name the two `SLICE-04` stubs this ADR unblocks: `RemoveBodyPart`'s item dependency check, and `DeleteCharacterPermanently`'s inventory/item dependency checker (which explicitly must cover "equipped items" among other ownership shapes).
- `InventoryLocationKind.Equipped` and `InventoryLocationRef.Equipped(InventoryId inventoryId, string equipmentSlotRef)` (`Packages/com.odyssey.domain/Runtime/Inventory/InventoryRuntime.cs` lines ~144-148) exist today as a bare location-kind marker: `(Kind=Equipped, TargetRef=inventoryId, DetailRef=equipmentSlotRef)`. No `BodyPartRefs[]`, `EquippedByUserId`, `EquippedAt`, or `Revision` field exists on it or anywhere else.
- `ArmorDefinition` (`Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` lines ~181-207) carries `EquipmentSlotKey` (a plain string) and `CoveredBodyPartIds` (a list of `BodyPartId`) as **catalog** metadata describing what an armor item requires — its own doc-comment states directly that "no `EquipmentSlot` catalog type exists" and this "only stores the reference." This is not runtime "what is currently equipped" state.
- `BodyPart` (`Packages/com.odyssey.domain/Runtime/Character/Anatomy.cs` lines ~96-120): `BodyPartId`, `Name`, `DamageLimit`, `AttachedToBodyPartId` (body-part-to-body-part only), `Properties`. No field referencing an equipped item or slot.
- `SqliteCharacterRepository.RemoveBodyPart` (lines ~4061-4148, doc-comment re-written twice: first by the original `SLICE-04` stub, then by `ODY-S05-206`) checks only internal `AttachedToBodyPartId` dependencies (other body parts / permanent modifications attached to the part being removed). Its own doc-comment states the item/equipment dependency check is deferred to "the Equipment runtime block named in `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` section 8" — i.e., precisely this decomposition. This stub is **not closed**.
- `InventoryCharacterDeletionDependencyChecker.CheckBlockingDependency` (`Packages/com.odyssey.application/Runtime/Inventory/InventoryDeletionDependencyCheckers.cs`) calls `IInventoryRepository.HasAnyItemOwnedByCharacter`, whose SQLite implementation (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs` lines ~387-408) is: `SELECT 1 WHERE EXISTS (... ItemInstance WHERE CampaignId=$c AND OwnerKind='Character' AND OwnerTargetRef=$id) OR EXISTS (... ItemStack WHERE ...)` — **no `LocationKind` filter anywhere in the query**. Because equipping an item changes only its `LocationRef`, never its `OwnerRef` (`ADR-027` §7 rule 2: "it does not copy item mechanics into Character as authoritative state" — ownership stays with the Inventory the item is in), this existing query is already location-agnostic and therefore already covers an equipped item exactly the same way it covers a contained or scene-dropped one. **Conclusion: `DeleteCharacterPermanently`'s equipment dependency (`ADR-027` §9.2) is already closed by `ODY-S05-206`; this decomposition does not need a separate sub-task for it.** (`InventoryDeletionDependencyCheckerTests.cs` exercises Contained and SceneDropped explicitly but not `InventoryLocationRef.Equipped` by name — a minor test-coverage gap, not a functional one, since the query itself never branches on `LocationKind`; noted in §18 as an optional, non-blocking observation, not a required sub-task.)
- `git grep -il equipment` across the whole tracked repository (outside `docs/`) returns only: the files already named above, plus prose comments and negative/guard-rail tests in `Odyssey.Tests.Persistence`/`Odyssey.Tests.Unit` that assert no `Equipment` table, type, or class exists yet (scope guards, not scaffolding). No `EquippedEntry`, Equip/Unequip command, event, persistence table, or service exists anywhere.
- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` is not present in this tracked checkout; `scripts/check-repository-policy.ps1`'s forbidden-tracked-path list includes `^Documentation/`, confirming this is an intentionally untracked/private product document, not a missing file. No Equipment-specific requirement beyond `ADR-027` §7 could be verified from the tracked repository; none is assumed.

### Assumptions

- None. Every fact above was directly observed via `git fetch`/`Read`/`git grep` against `origin/main` during this task.

## 5. Scope

### In scope

- Add this task contract.
- Add a Brief plan for this task.
- Update `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: remove "Equipment runtime" from §8's reserved-but-undecomposed list; add a new ordered-backlog section (§12) decomposing it into `ODY-S05-301`–`306`, with a task-boundaries subsection (§12.1) mirroring §7.1; extend §9's non-goals and §10's dependency rules and §11's reserved-number-range sentence additively (no rewrite of existing decisions).
- Reserve task IDs `ODY-S05-301`–`306` for the future Equipment runtime implementation block.
- (Housekeeping, in scope per explicit product-owner note) move `ODY-S05-107`'s own task contract and Brief plan from `active/` to `completed/`, with a `Status:` line update to `Done (PR #112, merged into main)` — its own PR #112 (commit `3cd44ea`) has long been merged but the files were never relocated.

### Out of scope

- Any C# production code: domain types, persistence, commands, services.
- Any new tests requiring new runtime code.
- Unity/UI.
- Implementing Equip/Unequip, `RemoveBodyPart`'s real dependency check, or any `DeleteCharacterPermanently` change.
- Editing `docs/adr/**` — including the open "amendment note" question raised in `ODY-S05-206`'s own §18 about whether `ADR-027` §9 should cross-reference this decomposition. That remains the product owner's open decision; not resolved here.
- Item-sourced abilities/effects (`ActiveEffect`), `ItemDefinition` migration, and the full attack pipeline — these stay reserved and undecomposed, unchanged from the current §8.
- Rewriting `SLICE-05_IMPLEMENTATION_BACKLOG.md` §3's existing recorded decisions — only additive text (a new §12, and additive sentences in §9/§10/§11) is introduced.

### Allowed paths

```text
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md
docs/plans/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md
```

Plus, for the in-scope housekeeping noted above:

```text
docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md   (move to completed/, Status line only)
docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md   (move to completed/, Status line only)
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/**
DotNet/**
Tests/**
Assets/**
Documentation/**
docs/tasks/active/ODY-S05-101_* through docs/tasks/active/ODY-S05-106_* (already Done, out of this task's scope)
```

## 6. Technical constraints

- Module ownership and dependency direction: no production module is edited; future Equipment tasks must follow `ADR-001` and `ADR-027` §14 module-ownership rules.
- Authoritative-state and transaction boundary: no command/state behavior is implemented here; future Equip/Unequip commands must follow `ADR-002`/`ADR-012`.
- Serialization / compatibility boundary: no DTO, schema, or serialized contract is added here.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Not applicable.
- Dependency / licensing rule: no dependency changes.
- Security / privacy / redaction rule: PR/task text must remain sanitized; no private documentation, secrets, hidden campaign content, or user data added.
- Performance or platform constraint: Not applicable.
- Other: no accepted ADR section may be edited unless a real contradiction is found; none was found (`ADR-027` §7 already fully specifies the target shape this decomposition sequences toward).

## 7. Expected behavior

### Scenario 1 — Equipment runtime block decomposed

**Given** `ADR-027` §5/§7/§9 define the Equipment model and the stubs it unblocks
**When** this planning task updates the backlog
**Then** the Equipment runtime block is broken into small tasks covering domain foundation, persistence, Equip, Unequip, the `RemoveBodyPart` dependency closure, and integration fixtures — each with purpose, dependencies, planning mode, and explicit in/out-of-scope boundaries.

### Scenario 2 — `DeleteCharacterPermanently` gap explicitly resolved, not silently assumed

**Given** `ADR-027` §9.2 names "equipped items" among what `DeleteCharacterPermanently`'s dependency checker must cover
**When** this task inspects `InventoryCharacterDeletionDependencyChecker`/`HasAnyItemOwnedByCharacter` directly
**Then** the decomposition records, with code evidence, that this checker is already location-agnostic and already covers equipped items, and therefore does **not** add a redundant sub-task for it.

### Scenario 3 — `RemoveBodyPart` closure gets a real task ID

**Given** `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7.1's rule that an unclosable stub must gain "an explicit follow-up task ID instead of an unnamed TODO"
**When** this task decomposes Equipment runtime
**Then** `RemoveBodyPart`'s item/equipment-dependency closure appears as its own named task (`ODY-S05-305`), not a TODO or a footnote.

### Scenario 4 — Later blocks stay deferred

**Given** item-sourced abilities/effects (`ActiveEffect`), `ItemDefinition` migration, and the full attack pipeline are separate, larger reserved blocks
**When** this task decomposes Equipment runtime
**Then** those three remain named and reserved, unchanged, in §8.

### Required invariants

- Equipment remains Inventory-owned location state (`ADR-027` §5/§7), never a new aggregate root and never Character-owned authoritative state.
- No product code, schema, runtime test implementation, or accepted ADR architecture section is changed.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §3's existing recorded decisions are not rewritten — only additive text is introduced.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: this task contract, its Brief plan, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` Equipment runtime decomposition, and (housekeeping) the `ODY-S05-107` file relocation.
- Generated evidence or build artifacts: None.
- Migration / recovery material: None.

## 9. Acceptance criteria

1. `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 no longer lists "Equipment runtime" as an undecomposed reserved block.
2. A new ordered-backlog section decomposes Equipment runtime into small implementation tasks (`ODY-S05-301`–`306`), each with purpose, dependencies, proposed planning mode, in-scope summary, and explicit out-of-scope boundaries, at the same structural depth as §7/§7.1.
3. The decomposition preserves `ADR-027`: Equipment is Inventory-owned location state, not a Character section or new aggregate root; the `EquippedEntry` shape and all six §7 rules are named; runtime item/stack mechanics stay with the item, not copied to Character.
4. The task contract's decision log explicitly states, with code evidence, whether `InventoryCharacterDeletionDependencyChecker` already covers "character still owns an equipped item" for `DeleteCharacterPermanently`, and why the decomposition does or does not include a separate sub-task for it.
5. `RemoveBodyPart`'s item/equipment-dependency stub closure appears as one of the new sub-tasks under an explicit task ID.
6. `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` §3's existing recorded decisions are not rewritten — diff confined to additive text plus the one §8 bullet removal.
7. No product code, schema, or test implementation is added.
8. No file under `docs/adr/**` is changed.
9. This task contract and Brief plan are added.
10. `git diff --name-status` against `main` shows only §5's allowed paths.
11. PR description states clearly that this is planning/decomposition only, with no implementation.

## 10. Tests and validation

### Required automated tests

None. This is a docs/planning-only decomposition task and does not add runtime behavior.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` and confirm only the allowed documentation/planning files changed (plus the optional `ODY-S05-107` housekeeping move).
- Confirm `ADR-027` §5/§6/§7/§9 were read in full and are quoted/paraphrased accurately in this contract and the backlog update.
- Confirm the `InventoryCharacterDeletionDependencyChecker`/`HasAnyItemOwnedByCharacter` finding was verified directly against source, not assumed from this ТЗ.

### Required environments / profiles

- OS / architecture: Windows development machine.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable.
- Network topology or database fixture: Not applicable.
- Other: authenticated GitHub CLI used only to open the Draft PR.

### Validation not required by this task

- `dotnet build`/`dotnet test`: not required — no product code, test code, schema, contracts, or project files change.
- Unity validation: not required — no Unity files change.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert this documentation-only branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: public repository planning documentation only.
- Trust boundaries: no runtime trust boundary is changed.
- Authorization / audience checks: not implemented here; future Equipment tasks must preserve `ADR-027` §12 MainGM-authoring baseline.
- Redaction requirements: do not add private product docs, hidden campaign data, secrets, or personal data.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan.
- Reason for selected mode: this task changes only documentation/planning files, adds no product code, no public runtime contract, no persistence schema, no permissions behavior, and no accepted ADR architecture change — identical reasoning to `ODY-S05-107`.
- Plan path: `docs/plans/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must start from current `origin/main` after PR #119/#120/#121 and the doc-sync commit `ef5a57f`; future Equipment runtime implementation starts at `ODY-S05-301`.

## 15. Documentation and versioning impact

- Documents that must change: `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this task's Brief plan, and (housekeeping) `ODY-S05-107`'s task contract/Brief plan location and Status line.
- Documents that must not change: `docs/adr/**`, production code, schema, tests, Unity assets, existing `ODY-S05-101`–`207` task contracts' own content (only `107`'s Status line/location, per the explicitly authorized housekeeping).
- Application version change: No — docs/planning only.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: no formal version field changed; backlog `Last updated` changes to 2026-09-11 UTC.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass or are correctly marked not applicable.
- [x] Required manual checks are completed.
- [x] Required commands and their real results are recorded.
- [x] Architecture and dependency rules remain valid.
- [x] Security, privacy, redaction, and audience rules are verified where applicable.
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [x] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — removes "Equipment runtime" from §8; adds §12 (Equipment runtime ordered backlog) and §12.1 (task boundaries); additive sentences in §9 (non-goals), §10 (dependency rules), §11 (reserved number range), and the §1/§2 narrative.
- `docs/tasks/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md` — this task contract.
- `docs/plans/active/ODY-S05-108_Decompose_Equipment_Runtime_Block.md` — this task's Brief plan.
- `docs/tasks/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` → `docs/tasks/completed/` — housekeeping, Status line only.
- `docs/plans/active/ODY-S05-107_Decompose_Inventory_Runtime_Block.md` → `docs/plans/completed/` — housekeeping, Status line only.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | `Repository policy check passed`; `REPO-POLICY-*`/`TC-CI-*` checks passed. |
| `.\scripts\verify-test-structure.ps1` | Passed | `TC-ARCH-001 PASS valid ADR-001 graph passes`; controlled invalid cases rejected. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | §8's "Equipment runtime" bullet removed. |
| AC-2 | Passed | New §12/§12.1 decompose Equipment runtime into `ODY-S05-301`–`306`. |
| AC-3 | Passed | §12 intro names `EquippedEntry` and all six `ADR-027` §7 rules; preserves Inventory-owned location-state framing. |
| AC-4 | Passed | §4 above and §18 decision log record the `HasAnyItemOwnedByCharacter` code-level finding and its consequence for scope. |
| AC-5 | Passed | `ODY-S05-305` — RemoveBodyPart Dependency Closure. |
| AC-6 | Passed | Diff to §3 is none; all edits are additive (new §12, plus additive sentences elsewhere) or the single required §8 bullet removal. |
| AC-7 | Passed | No `.cs`/`.json`/test file changed. |
| AC-8 | Passed | No `docs/adr/**` file in the diff. |
| AC-9 | Passed | This contract and Brief plan exist. |
| AC-10 | Passed | `git diff --name-status` limited to §5's allowed paths. |
| AC-11 | Passed | Draft PR body states planning/decomposition only. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: validation-results table above.

### Known limitations

- This task reserves future implementation tasks only; it does not create the individual `ODY-S05-301`–`306` task contract files.
- `InventoryDeletionDependencyCheckerTests.cs` does not explicitly exercise `InventoryLocationRef.Equipped` by name (only Contained and SceneDropped) even though the underlying query is provably location-agnostic. Non-blocking; noted for whichever future task next touches that test file.

### Follow-up tasks

- `ODY-S05-301` — Equipment Runtime Foundation.
- `ODY-S05-302` — Equipment Persistence Foundation.
- `ODY-S05-303` — Equip Command MVP.
- `ODY-S05-304` — Unequip Command MVP.
- `ODY-S05-305` — RemoveBodyPart Dependency Closure.
- `ODY-S05-306` — Equipment Runtime Integration Fixtures.

### Self-review summary

- Scope review: diff limited to `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, this task contract, this Brief plan, and the `ODY-S05-107` housekeeping relocation.
- Architecture review: no accepted ADR changed; decomposition follows `ADR-027` §5/§7/§9 and does not decide new runtime architecture.
- Test review: no runtime tests added because no runtime code changed; required repository validation scripts passed.
- Security/privacy review: no private documentation excerpts, hidden campaign data, secrets, or personal data added.
- Documentation/version review: backlog `Last updated` changed to 2026-09-11 UTC; no application/schema/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — Decision: use Brief plan, not ExecPlan, for this task itself — identical reasoning to `ODY-S05-107` (docs/planning-only, no public contract or ADR change). Authority: `PLANS.md` §1.1.
- 2026-09-11 — **Decision (required by DoD): `DeleteCharacterPermanently`'s equipment dependency is already closed, no new sub-task needed.** `InventoryCharacterDeletionDependencyChecker.CheckBlockingDependency` calls `IInventoryRepository.HasAnyItemOwnedByCharacter`, whose SQLite query (`SqliteInventoryRepository.cs` lines ~387-408) filters only on `CampaignId`/`OwnerKind`/`OwnerTargetRef` — it never references `LocationKind` at all. Since equipping an item (`ADR-027` §7 rule 2) changes only `LocationRef`, never `OwnerRef`, this query already returns `true` for a character who has an item equipped, exactly as it does for contained or scene-dropped items. `ADR-027` §9.2's "equipped items" requirement for `DeleteCharacterPermanently` is therefore already satisfied by `ODY-S05-206`, verified directly in code (not assumed from the ТЗ). Authority: direct source read, this session.
- 2026-09-11 — Decision: decompose Equipment runtime into `ODY-S05-301`–`306` (foundation, persistence, Equip, Unequip, `RemoveBodyPart` closure, integration fixtures) — no separate `DeleteCharacterPermanently` sub-task, per the finding above. Authority: `ADR-027` §5/§7/§9 and the small-single-responsibility precedent already set by `ODY-S05-201`–`207`.
- 2026-09-11 — Decision: do not edit `docs/adr/**`, including the open `ODY-S05-206`-§18 question about an `ADR-027` §9 amendment note. That remains the product owner's own open decision. Authority: user out-of-scope instruction, this ТЗ §5.
- 2026-09-11 — Decision: perform the `ODY-S05-107` housekeeping move (active/ → completed/, Status line update) as explicitly pre-authorized optional scope in this ТЗ's §0. Authority: this ТЗ.
- 2026-09-11 — Decision: extend `SLICE-05_IMPLEMENTATION_BACKLOG.md` additively (new §12/§12.1, additive sentences in §9/§10/§11) rather than renumbering existing §8–§11 into §9–§12, to honor the explicit instruction not to rewrite already-accepted decision text. Authority: this ТЗ §5 ("только явное добавление нового раздела, без переписывания существующего текста").

### Approved task changes

- None.
