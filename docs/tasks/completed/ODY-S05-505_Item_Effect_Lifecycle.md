# ODY-S05-505 — WhileItemEquipped Wiring + Item-Triggered Creation

**Status:** Done (PR #140, merged into main)
**Roadmap stage / slice:** SLICE-05 (item-sourced abilities/effects block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `codex/ody-s05-505-item-effects`
**Pull request:** [odyssey-services/Odyssey_VTT#140](https://github.com/odyssey-services/Odyssey_VTT/pull/140) (Draft)
**ExecPlan:** `docs/plans/active/ODY-S05-505.md`
**Created:** 2026-09-12
**Last updated:** 2026-09-13 UTC

## 1. Goal

Implement two related mechanisms sharing the same equip/unequip event seam: (a) `WhileItemEquipped` — unequipping an item transitions every `Active` `ActiveEffect` sourced from that item with `EffectDurationType.WhileItemEquipped` to `Suspended`; re-equipping the same item resumes them to `Active`; (b) item-triggered creation — equipping an item whose pinned snapshot carries a non-empty `BuiltInEffectRefs` list creates the corresponding `ActiveEffect` rows.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-027` §8.2 rule 3 already decided that `WhileItemEquipped` effects "subscribe to authoritative `ItemEquipped`/`ItemUnequipped` events," but no such event, publisher, or subscriber exists anywhere in the codebase — a direct repository-wide search confirms only documentation prose mentions them. Without this task, `ActiveEffectStatus.Suspended` (declared by `ODY-S05-502`) is dead code, and `ItemDefinition.BuiltInEffectRefs` creates nothing.
- Value or risk reduction: gives the item-sourced abilities/effects block its first real, end-to-end-usable production behavior — a player equipping a magic ring can actually receive its built-in effect, and unequipping it correctly suspends (never expires or removes) the effect rather than silently leaving it `Active` with no item backing it.
- Blocking or enabling relationship: this is the third of four remaining implementation tasks in the range (`503`/`504` already done); `ODY-S05-507`'s own integration fixtures will be the first real end-to-end composition of this task's own service.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, §15/§15.1 row 4.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, §8.1 (`CharacterAbility` integration rules, verbatim), §8.2 (`ActiveEffect` integration rules, verbatim, including rule 3's own `WhileItemEquipped` text), §12 rule 4 (item-use permission model).
- `docs/adr/ADR-028_ActiveEffect_Aggregate_Specification_v1.0.md`, §5.2 rule 3 (`Suspended` lifecycle state), §6 (repository contract conventions), §8 (`WhileItemEquipped` row: "Already solved by `ADR-027` §8.2 rule 3"), §10 rule 1 (item-triggered creation inherits the existing item-use permission model, no new MainGM restriction), §12 (mandatory `SqliteSavingPipeline` reuse).
- Existing patterns: `SqliteActiveEffectRepository.ExpireActiveEffect`/`ActiveEffectRevisionConflict` (`ODY-S05-504`, the direct CAS-via-`SqliteSavingPipeline` status-transition precedent this task's own `SetItemEffectEquipped` mirrors); `InventoryCreationService` (the direct precedent for an Application-layer service orchestrating more than one repository sequentially); `EquipmentService.Equip`/`Unequip` (the real, already-existing equip/unequip surface this task reacts to the result of, rather than subscribing to a nonexistent event).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-505`, `SLICE-05`.
- Existing test IDs: `TC-ACTIVEEFFECT-001`-`050` (`ODY-S05-502`/`503`/`504`) as predecessor evidence.
- New test IDs introduced: `TC-ACTIVEEFFECT-051`-`068`.

### Task-safe private context

- Approved summary / references: the user-provided `ODY-S05-505` task brief and its own closeout addendum (this session's own governing instruction to finish, verify, and open the PR for an in-progress worktree left by a prior agent session).

## 4. Verified current state

### Verified facts

- `origin/main`'s tip at the time this closeout began was `65816a81afc2cc2f1c23ad39a76cae4c4ce8b213` (merge of PR #139, `ODY-S05-504`) — re-verified via `git fetch origin` immediately before continuing this task; `origin/main` had not moved further. The existing worktree (`D:/Game_Dev/Odyssey_VTT/ody-s05-505`, branch `codex/ody-s05-505-item-effects`) was confirmed via `git merge-base HEAD origin/main` to be based exactly on this same commit — no rebase was needed.
- **Key finding, independently re-confirmed (not merely trusted from the prior session's own log):** a full-repository search finds no `ItemEquipped`/`ItemUnequipped`/`SceneActivated`-shaped event type, publisher, or subscriber anywhere in `Packages/`/`DotNet/` — only `ADR-027` §8.2 rule 3's own prose. The real, already-existing equip/unequip surface is `EquipmentService.Equip`/`Unequip`, which call `SqliteInventoryRepository.EquipItem`/`UnequipItem` (persisted via `EquipmentCommandLedger` for idempotency only — never through `SqliteSavingPipeline`/`DomainEvents`).
- `IActiveEffectRepository` (pre-task, direct code read) had exactly 5 methods (`CreateActiveEffect`/`GetActiveEffect`/`ListActiveEffectsByTarget`/`ListActiveEffectsBySource`/`ExpireActiveEffect`, the last added by `ODY-S05-504`) — no `Suspended`/`Active` transition existed. `ActiveEffectStatus.Suspended` (declared by `ODY-S05-502`) was unused by any code before this task.
- `ItemDefinition.BuiltInAbilityRefs`/`BuiltInEffectRefs` (direct code read, `TypedDefinitions.cs`) are `IReadOnlyList<ContentDefinitionRef>`, already present on the type, already round-tripped by `TypedDefinitionCodec.EncodeItem`/`DecodeItem` — this task is the first to actually read `BuiltInEffectRefs` and act on it.
- `InventoryCreationService` (direct code read) is the established precedent for an Application-layer service that accepts more than one repository (`IContentCatalogRepository` + `IInventoryRepository`) and calls them sequentially, in the same style this task's own `ItemEffectLifecycleService` follows (accepting `IInventoryRepository` + `IContentCatalogRepository` + `IActiveEffectRepository`).
- **Independently re-verified Unity/IL2CPP finding (built by direct comparison, not trusted from the prior session's own claim):** `Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef` lists `references: ["Odyssey.Domain", "Odyssey.Content", "Odyssey.Application"]` — it does not reference `Odyssey.Rules`. `SqliteCharacterRepository.cs` (in the same package, `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs:13`) has `using Odyssey.Rules.Character;`. `com.odyssey.persistence/package.json` confirms this is a real, Unity-auto-discovered embedded package (not merely a `.NET`-only folder), so Unity's own asmdef-based compiler would need `Odyssey.Rules` on the classpath to compile this file and cannot get it from the declared references. **This exact same asmdef/using mismatch was confirmed present identically on a clean `origin/main` checkout** (`git show origin/main:Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef` and `git show origin/main:.../SqliteCharacterRepository.cs`, both compared byte-for-byte against this task's own untouched copies) — proving the defect is genuinely pre-existing and not introduced by any change in this task, this range, or this session. This task touches neither `SqliteCharacterRepository.cs` nor any `.asmdef` file (confirmed by `git diff --name-status`, §17) and does not attempt to fix this defect, per the governing closeout instruction's own explicit "do not fix, only document" directive.
- **`CharacterAbility` open question, independently re-confirmed:** direct read of `ICharacterRepository`'s own contract confirms only acquisition/removal methods exist for `CharacterAbility` — no suspend/resume method of any kind. `ADR-027` §8.1 rule 2 requires unequip to "remove, suppress, or revalidate" an item-sourced `CharacterAbility`, but with no existing suspend-shaped method, satisfying rule 2 without inventing new `CharacterAbility` persistence (forbidden by the governing ТЗ's own "не изобретать новую персистентность" instruction) is not possible in this task. Recorded as open follow-up `ODY-S05-505-F01` (§18), not silently skipped.

### Assumptions

- None.

## 5. Scope

### In scope

- New `Odyssey.Application.Effects.ItemEffectLifecycleService` static class: `OnItemEquipped` (creates `BuiltInEffectRefs`-sourced effects; resumes any `Suspended` `WhileItemEquipped` effects already sourced from the same item), `OnItemUnequipped` (suspends `Active` `WhileItemEquipped` effects sourced from the item). Both are explicit, host-invoked reactions to an already-successful `EquipmentService.Equip`/`Unequip` result — never a subscription to a nonexistent event.
- New `IActiveEffectRepository.SetItemEffectEquipped` + `SqliteActiveEffectRepository` implementation — `Revision`-CAS-guarded, `CommandId`-idempotent, routed through `SqliteSavingPipeline`, transitioning only between `Active`/`Suspended` for a `WhileItemEquipped`-durationed, item-sourced effect.
- Tests (`TC-ACTIVEEFFECT-051`-`068`), test metadata, task/plan docs, backlog row.

### Out of scope

- `SqliteInventoryRepository.EquipItem`/`UnequipItem`/`EquipmentCommandLedger` — called through `EquipmentService`, never modified.
- Stacking-policy resolution (`ODY-S05-503`) and non-`WhileItemEquipped` duration/expiry mechanisms (`ODY-S05-504`) — not reopened; `ItemEffectLifecycleService` explicitly rejects (fails) rather than silently bypasses a non-`IndependentInstances` `StackPolicy`, since `503`'s own `ActiveEffectStackingRules` currently provides decisions only, not an atomic write path this service could safely compose with.
- `RemoveActiveEffect` and direct (non-item) creation (`ODY-S05-506`'s own territory).
- Any turn/round-based `EffectDurationType` mechanism or combat-triggered effect application (out of scope for this entire block).
- Any new `CharacterAbility` suspend/resume persistence — recorded as open follow-up `ODY-S05-505-F01` (§18), not implemented here, per the governing ТЗ's own explicit instruction not to invent new persistence around the existing `ICharacterRepository`.
- The pre-existing Unity/IL2CPP `Odyssey.Rules`-via-`SqliteCharacterRepository` asmdef gap — confirmed pre-existing (§4), explicitly not fixed in this task (§18), honestly documented here and in the PR.
- `docs/adr/ADR-027_...md`/`ADR-028_...md` — already `Accepted`; implemented here, not amended.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Effects/ItemEffectLifecycleService.cs
Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/ItemEffectLifecycleTests.cs
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-505_Item_Effect_Lifecycle.md
docs/plans/active/ODY-S05-505.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/**
Packages/com.odyssey.domain/**
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectStackingRules.cs
Packages/com.odyssey.application/Runtime/Effects/ActiveEffectExpiryRules.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Any `.asmdef`/`.csproj` file
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: orchestration stays in `Odyssey.Application` (`ADR-028` §15); the new repository method stays a thin `Odyssey.Persistence` transition, deciding nothing itself.
- Authoritative-state and transaction boundary: `SetItemEffectEquipped`'s CAS update + `DomainEvents`/`AppliedCommands` rows commit together in one transaction via `SqliteSavingPipeline`. `OnItemEquipped`/`OnItemUnequipped` themselves are **not** atomic across multiple effect rows — each `CreateActiveEffect`/`SetItemEffectEquipped` call is its own independent transaction, and the host must retry/finish a reaction with identical inputs before processing the next equipment operation, exactly like `EquipmentService`'s own result-then-react seam.
- Serialization / compatibility boundary: the transition's own durable command-summary payload is a small, fixed-order, hand-written JSON string (via `Newtonsoft.Json`'s `JsonTextWriter`, already a dependency of this file since `ODY-S05-504`'s own `EquipmentTransitionPayload`-shaped precedent) — no reflection-based serialization.
- Time / RNG rule: `_clock.GetUtcNow()` via the injected `IWallClock`, matching every other mutation in `SqliteActiveEffectRepository`. `ItemEffectLifecycleService` never reads a clock itself — every timestamp it uses comes from the already-validated `EquippedEntryRecord`/host-supplied `resolveExpiry` callback.
- Unity / thread / lifetime rule: no new Unity/asmdef file is touched; the pre-existing Unity/IL2CPP gap (§4) is independently reconfirmed, not fixed.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: no raw exception text or stack trace in any returned `Error`; the transition's own durable summary contains only already-public-shaped identifiers (`CampaignId`/`ActiveEffectId`/`UserId`/`Revision`/`Status`), never mechanics payload content.
- Other: item-triggered creation inherits the existing item-use permission model (`ADR-028` §10 rule 1) — `ItemEffectLifecycleService` adds no permission check of its own; authorization is the invoking host's own responsibility, exactly as the governing ТЗ requires.

## 7. Expected behavior

### Scenario 1 — equip creates built-in effects

**Given** a successfully equipped item whose pinned snapshot has a non-empty `BuiltInEffectRefs`
**When** `OnItemEquipped` is called
**Then** one `ActiveEffect` is created per referenced, `Published`, non-`Instant`, non-combat-duration effect definition, sourced `ForEquippedItem`/`ForItem` matching the actual `InventoryItemRef` kind, targeting the caller-supplied `ActiveEffectTargetRef`.

### Scenario 2 — empty BuiltInEffectRefs creates nothing

**Given** an equipped item with an empty `BuiltInEffectRefs`
**When** `OnItemEquipped` is called
**Then** it succeeds with an empty result and no row is created.

### Scenario 3 — unequip suspends only WhileItemEquipped effects from that item

**Given** several `Active` effects sourced from an item, only some with `EffectDurationType.WhileItemEquipped`
**When** `OnItemUnequipped` is called
**Then** only the `WhileItemEquipped` ones transition to `Suspended`; every other duration type, and every effect sourced from a different item, is left completely untouched.

### Scenario 4 — re-equip resumes the same row

**Given** a `Suspended` `WhileItemEquipped` effect from a previously unequipped item
**When** the same item is re-equipped and `OnItemEquipped` is called
**Then** the same row (same `ActiveEffectId`) transitions back to `Active`, its own `EffectMechanicsSnapshot`/`AppliedAt` unchanged — never a duplicate row.

### Scenario 5 — the new repository transition is CAS-guarded and idempotent

**Given** a stale `expectedRevision`, or a replayed `CommandId`
**When** `SetItemEffectEquipped` is called
**Then** a stale revision fails with `persistence.active_effect.revision_conflict` and mutates nothing; a replay with the same `CommandId` (even after later, unrelated mutations of the same row) returns the originally committed revision without a second transition.

### Required invariants

- `WhileItemEquipped` effects are never `Expired`/`Removed` by an unequip — only `Suspended`.
- `SetItemEffectEquipped` only ever transitions `Active ↔ Suspended`, and only for a `WhileItemEquipped`-durationed, item-sourced effect — any other status or duration type is rejected, not silently accepted.
- No method in this task subscribes to, or claims the existence of, a real `ItemEquipped`/`ItemUnequipped` event.
- Every mutation this task introduces commits through `SqliteSavingPipeline`.

## 8. Deliverables

- Production code: `ItemEffectLifecycleService` (Application); `IActiveEffectRepository.SetItemEffectEquipped` + `SqliteActiveEffectRepository` implementation (Persistence).
- Tests: `DotNet/Tests/Odyssey.Tests.Persistence/ItemEffectLifecycleTests.cs`, `TC-ACTIVEEFFECT-051`-`068`.
- Scripts / CI: none.
- Configuration: none.
- Documentation: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, backlog row.
- Generated evidence or build artifacts: none persisted.
- Migration / recovery material: none — no schema change beyond the already-existing `ActiveEffect` table's own columns.

## 9. Acceptance criteria

1. The "no real equip/unequip event exists" finding and the chosen post-success-reaction design (not a fabricated event subscription) are explicitly documented in this contract and the PR — never presented as if a real event bus were used.
2. `SetItemEffectEquipped` is `Revision`-CAS-guarded and routed through `SqliteSavingPipeline`.
3. Equipping an item with `BuiltInEffectRefs` creates the corresponding `ActiveEffect` rows.
4. Unequipping transitions only that item's own `WhileItemEquipped` effects to `Suspended`; re-equipping resumes them to `Active`, same row.
5. `CharacterAbility` suspend/resume symmetry (`ADR-027` §8.1) is explicitly recorded as an open question (`ODY-S05-505-F01`) rather than silently omitted, since no suspend/resume method exists on `ICharacterRepository` and none is invented here.
6. `EquipItem`/`UnequipItem`/stacking/expiry/removal logic is not modified.
7. `dotnet test` is green across the full solution, including after every change made during this closeout (verified by a fresh full-suite run, not merely trusted from a prior session's own log).
8. The pre-existing Unity/IL2CPP `SqliteCharacterRepository`/`Odyssey.Rules`/asmdef gap is independently reconfirmed as pre-existing (present identically on a clean `origin/main`), not fixed, and honestly documented here and in the PR.
9. All three required validation scripts (`verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1`) pass.
10. `TC-ACTIVEEFFECT-051`-`068` cover creation, empty-refs, suspend, resume, unrelated-duration/source isolation, CAS conflict, replay, and command-reuse-rejection scenarios.
11. Backlog row `505` → `In Review`, with the PR link.
12. PR is opened as Draft on GitHub (it was not open at the start of this closeout).
13. No self-merge is performed.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-ACTIVEEFFECT-051` | .NET / NUnit (Persistence, real SQLite) | Equip creates built-ins from pinned snapshots; replay creates no duplicates | Pass |
| `TC-ACTIVEEFFECT-052` | .NET / NUnit (Persistence, real SQLite) | Empty BuiltInEffectRefs creates no effects | Pass |
| `TC-ACTIVEEFFECT-053` | .NET / NUnit (Persistence, real SQLite) | Unequip suspends both item source kinds, isolates another item | Pass |
| `TC-ACTIVEEFFECT-054` | .NET / NUnit (Persistence, real SQLite) | Re-equip resumes the same row, preserves snapshot/AppliedAt | Pass |
| `TC-ACTIVEEFFECT-055` | .NET / NUnit (Persistence, real SQLite) | Unequip leaves every other persistable duration untouched | Pass |
| `TC-ACTIVEEFFECT-056` | .NET / NUnit (Persistence, real SQLite) | Stale revision fails without projection/journal change | Pass |
| `TC-ACTIVEEFFECT-057` | .NET / NUnit (Persistence, real SQLite) | Replay survives a later resume and repository reopen | Pass |
| `TC-ACTIVEEFFECT-058` | .NET / NUnit (Persistence, real SQLite) | Command reuse with changed actor/effect/revision/direction fails | Pass |
| `TC-ACTIVEEFFECT-059` | .NET / NUnit (Persistence, real SQLite) | Non-equipment duration and terminal-status effects reject the transition | Pass |
| `TC-ACTIVEEFFECT-060` | .NET / NUnit (Persistence, real SQLite) | Cross-campaign transition is rejected | Pass |
| `TC-ACTIVEEFFECT-061` | .NET / NUnit (Persistence, real SQLite) | ItemStack source supports create/suspend/resume | Pass |
| `TC-ACTIVEEFFECT-062` | .NET / NUnit (Persistence, real SQLite) | Malformed pinned snapshot fails before any suspension | Pass |
| `TC-ACTIVEEFFECT-063` | .NET / NUnit (Persistence, real SQLite) | Missing referenced definition prevents partial creation | Pass |
| `TC-ACTIVEEFFECT-064` | .NET / NUnit (Persistence, real SQLite) | Instant duration creates no persistent row | Pass |
| `TC-ACTIVEEFFECT-065` | .NET / NUnit (Persistence, real SQLite) | ForDuration requires an explicit host-supplied Ruleset expiry | Pass |
| `TC-ACTIVEEFFECT-066` | .NET / NUnit (Persistence, real SQLite) | An already-Expired WhileItemEquipped effect is not resurrected on re-equip | Pass |
| `TC-ACTIVEEFFECT-067` | .NET / NUnit (Persistence, real SQLite) | A reaction CommandId cannot be reused for another item | Pass |
| `TC-ACTIVEEFFECT-068` | .NET / NUnit (Persistence, real SQLite) | Transition commits canonical event bytes, hash, command summary, and aggregate revision together | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review `git diff --name-status` against §5's own allowed/forbidden path lists — confirm `EquipItem`/`UnequipItem`/`EquipmentCommandLedger`, `SqliteCharacterRepository.cs`, every `.asmdef`, and `ActiveEffectStackingRules.cs`/`ActiveEffectExpiryRules.cs` are all untouched.
- Independently re-derive the Unity/IL2CPP finding by comparing `Odyssey.Persistence.asmdef`'s own `references` array against `SqliteCharacterRepository.cs`'s own `using` statements, on both this branch and a clean `origin/main` checkout.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine.
- Unity editor or Player profile: **not run** — the pre-existing Unity/IL2CPP asmdef gap (§4) means a real Unity compile of `Odyssey.Persistence` would fail today, independent of anything in this task; no claim of a successful Unity/Player build is made.
- Scripting backend: not applicable to the pure .NET path this task validates.
- Network topology or database fixture: local temp-directory campaign with a real SQLite database.
- Other: pure .NET build/test path is the actually-validated surface.

### Validation not required by this task

- Unity Editor/Player compilation/validation, because of the independently-reconfirmed pre-existing asmdef gap (§4/§18) — fixing or working around it is explicitly out of this task's own scope.
- `CharacterAbility` suspend/resume test coverage, since no such mechanism is implemented here (`ODY-S05-505-F01`).

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive — one new repository method, one new Application service, no existing type/table/method changed.
- Version fields affected: none.
- Migration or upcaster: none — no schema change; `SetItemEffectEquipped` operates on the already-existing `ActiveEffect` table.
- Forward / backward behavior: older builds never call the new method/service; no existing data format changes.
- Rollback method (of this PR): revert the branch/PR before merge.
- Data-loss risk and protection: none new — `SetItemEffectEquipped` never physically deletes a row, matching `ADR-012`'s append-only-history discipline already established by `ExpireActiveEffect`.
- Recovery rehearsal required: no.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: synthetic catalog/inventory/effect test records; no real player data.
- Trust boundaries: `ItemEffectLifecycleService` performs no authorization check of its own — it is invoked only after a caller's own successful `EquipmentService.Equip`/`Unequip`, and authorization for that already-happened equip/unequip is the invoking host's own responsibility, per `ADR-028` §10 rule 1's explicit "no new restriction" instruction.
- Authorization / audience checks: none added by this task.
- Redaction requirements: no raw exception text/stack trace in any returned `Error`; the transition's durable summary contains only identifiers, never mechanics payload content.
- Log-safe fields: `CampaignId`/`ActiveEffectId`/`UserId`/`Revision`/`Status` only.
- Abuse / malformed input limits: every new method validates its own invariants and fails fast; a malformed pinned snapshot on one row fails the whole reaction before any other row is mutated (`TC-ACTIVEEFFECT-062`).
- Security tests: `TC-ACTIVEEFFECT-060` (cross-campaign rejection), `TC-ACTIVEEFFECT-058` (command-reuse-with-changed-actor rejection).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new Application-layer orchestration service spanning three repositories and a new repository status-transition method — multiple `PLANS.md` §1.2 triggers.
- ExecPlan path: `docs/plans/active/ODY-S05-505.md`.
- Expected pull request count: 1.
- Milestone or sequencing constraints: must follow merged PR #139 (`ODY-S05-504`); no hard dependency on/from `ODY-S05-506` (depends only on `502`).

## 15. Documentation and versioning impact

- Documents that must change: this task contract, ExecPlan, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`.
- Documents that must not change: accepted ADRs (`ADR-027`, `ADR-028`); `EquipItem`/`UnequipItem`/`EquipmentCommandLedger`; `SqliteCharacterRepository.cs`; any `.asmdef`/`.csproj` file.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: adds one repository method; no manifest/schema version bump.
- Documentation version changes: none.
- Changelog or release-note requirement: none.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass.
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

- `Packages/com.odyssey.application/Runtime/Effects/ItemEffectLifecycleService.cs` — new file.
- `Packages/com.odyssey.application/Runtime/Persistence/ActiveEffectRepositoryContracts.cs` — new `SetItemEffectEquipped` method.
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteActiveEffectRepository.cs` — `SetItemEffectEquipped` implementation.
- `DotNet/Tests/Odyssey.Tests.Persistence/ItemEffectLifecycleTests.cs` — new file, `TC-ACTIVEEFFECT-051`-`068`.
- `Tests/Metadata/test-catalog.json` — `TC-ACTIVEEFFECT-051`-`068` registered.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row `ODY-S05-505` updated to `In Review`.
- `docs/tasks/active/ODY-S05-505_Item_Effect_Lifecycle.md` / `docs/plans/active/ODY-S05-505.md` — rewritten to full depth during this closeout.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | PASS | 0 warnings, 0 errors. |
| `dotnet test DotNet\Odyssey.Core.sln` | PASS | Contracts 1/1, Domain 90/90, Networking 67/67, Unit 136/136, Architecture 2/2, Persistence 595/595 (577 predecessor + 18 new). Re-run fresh after this closeout's own test-catalog correction, not merely trusted from the prior session's own log. |
| `.\scripts\verify-format.ps1` | PASS | |
| `.\scripts\check-repository-policy.ps1` | PASS | No new `ERROR_CODES.md` rows required (reuses `application.validation.invalid`). |
| `.\scripts\verify-test-structure.ps1` | PASS | |
| Unity/IL2CPP compile | **NOT GREEN — pre-existing, unrelated defect, honestly documented, not fixed** | `Odyssey.Persistence.asmdef` lacks an `Odyssey.Rules` reference that `SqliteCharacterRepository.cs` (untouched by this task) requires; independently confirmed identical on a clean `origin/main`. See §4/§18. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 (event-mechanism choice honestly documented) | Met | §4/§18; PR description. |
| AC-2 (SetItemEffectEquipped CAS + SqliteSavingPipeline) | Met | `TC-ACTIVEEFFECT-056`/`057`/`058`; code review. |
| AC-3 (BuiltInEffectRefs creation) | Met | `TC-ACTIVEEFFECT-051`. |
| AC-4 (suspend on unequip, resume on re-equip, source-scoped) | Met | `TC-ACTIVEEFFECT-053`/`054`/`055`/`061`. |
| AC-5 (CharacterAbility open question recorded) | Met | §18, `ODY-S05-505-F01`. |
| AC-6 (no out-of-scope mutation) | Met | `git diff --name-status`. |
| AC-7 (dotnet test green, re-run after all changes) | Met | Validation table above. |
| AC-8 (Unity/IL2CPP gap reconfirmed pre-existing, documented, not fixed) | Met | §4; validation table above. |
| AC-9 (all 3 scripts pass) | Met | Validation table above. |
| AC-10 (tests cover all named scenarios) | Met | §10 table. |
| AC-11 (backlog In Review + PR link) | Met | Backlog diff. |
| AC-12 (Draft PR opened) | Met | PR #140 opened during this closeout. |
| AC-13 (no self-merge) | Met | PR left as Draft. |

### Build and artifact evidence

- No build artifacts are persisted beyond the standard `artifacts/bin/**` output already produced by `dotnet build`.

### Known limitations

- `CharacterAbility` suspend/resume symmetry (`ADR-027` §8.1) is not implemented — `ODY-S05-505-F01`, an explicit, task-authorized deferral, not an oversight.
- The Unity/IL2CPP asmdef gap (`SqliteCharacterRepository.cs` needing `Odyssey.Rules` in `Odyssey.Persistence.asmdef`) remains unfixed — pre-existing, out of this task's own scope, and would need its own task to resolve (likely a small `.asmdef` reference addition, but that decision belongs to whoever owns that scope, not this task).
- `ItemEffectLifecycleService.OnItemEquipped`/`OnItemUnequipped` are not atomic across multiple affected effect rows — each row's own mutation is its own transaction; the host must retry/finish a reaction with identical inputs before the next equipment operation.
- No real caller of `ItemEffectLifecycleService` is wired to `EquipmentService.Equip`/`Unequip` yet — a future host/UI integration task must invoke these reactions; this task does not claim an automatic end-to-end wiring exists.

### Follow-up tasks

- `ODY-S05-505-F01` — define `CharacterAbility` suspend/resume semantics and a matching `ICharacterRepository` method, then wire the same item-source symmetry this task already gives `ActiveEffect`.
- A future, separately-scoped task to add `Odyssey.Rules` to `Odyssey.Persistence.asmdef`'s own references, resolving the pre-existing Unity/IL2CPP gap this task found but does not fix.
- `ODY-S05-506` — RemoveActiveEffect Command + Direct-Creation Permission Gates.
- `ODY-S05-507` — Item-Sourced Abilities/Effects Integration Fixtures (the first task to wire a real end-to-end caller of this service).

### Self-review summary

- Scope review: diff touches only allowed paths; `EquipItem`/`UnequipItem`/`EquipmentCommandLedger`, `SqliteCharacterRepository.cs`, every `.asmdef`, and `503`/`504`'s own decision-layer files are all untouched.
- Architecture review: mirrors `InventoryCreationService`'s own multi-repository orchestration precedent and `ExpireActiveEffect`'s own CAS-via-`SqliteSavingPipeline` precedent; no fabricated event-bus claim.
- Test review: all named scenarios covered with real SQLite integration tests reusing `EquipmentService.Equip`/`Unequip` for realistic fixtures, not merely in-memory construction.
- Security/privacy review: no new permission model; transition summary carries only identifiers, never payload content.
- Documentation/version review: task contract and ExecPlan rewritten to this session's own full-depth convention during this closeout; test catalog and backlog updated; both open questions (`CharacterAbility`, Unity/IL2CPP) explicitly and honestly recorded, not silently dropped.

## 18. Blockers, decisions, and change control

### Blockers

- None that block this task's own completion. Two genuine, explicitly-recorded open items exist outside this task's own authority to resolve (below).

### Decisions made during execution

- 2026-09-12/13 — **Design decision (inherited from the prior session, independently re-verified in this closeout): react to a successful `EquipmentService.Equip`/`Unequip` result via explicit, host-invoked Application-layer callbacks (`ItemEffectLifecycleService.OnItemEquipped`/`OnItemUnequipped`), rather than inventing a generic event-bus/publisher for `ItemEquipped`/`ItemUnequipped`.** A full-repository search (re-run in this closeout, not merely trusted) confirms no such event type, publisher, or subscriber exists anywhere — only `ADR-027` §8.2 rule 3's own prose describes the mechanism as if it already existed. `EquipItem`/`UnequipItem`/`EquipmentCommandLedger` are called through `EquipmentService`, never modified, per the governing ТЗ's own explicit prohibition. This is the single most consequential open architectural decision this task resolved, and it is honestly disclosed as such — never presented as if a real event subscription were used. Authority: direct repository-wide search (re-confirmed); `ADR-027` §8.2 rule 3's own text (decides the *mechanism's meaning*, not that a real event type already exists); the governing ТЗ's own explicit instruction to document, not fabricate, this choice.
- 2026-09-12/13 — **Design decision: `CharacterAbility` suspend/resume symmetry is explicitly NOT implemented — recorded as open follow-up `ODY-S05-505-F01`, not a new persistence mechanism invented around `ICharacterRepository`.** Direct read of `ICharacterRepository`'s own contract confirms only acquisition/removal exists for `CharacterAbility`; no suspend/resume method of any shape. Inventing new `CharacterAbility` persistence to work around this gap is explicitly forbidden by the governing closeout ТЗ ("не изобретать новую персистентность... в обход существующего репозитория"). This is a conscious, authorized, and clearly-labeled deferral, not a silently dropped requirement — `ADR-027` §8.1 rule 2's own "remove, suppress, or revalidate" language is satisfied for `ActiveEffect` (this task's own actual scope) but deliberately not yet for `CharacterAbility`. Authority: direct `ICharacterRepository` contract read confirming no suspend/resume method exists; the governing closeout ТЗ's own explicit "record as open question, do not invent persistence" instruction.
- 2026-09-13 — **Verification decision: the pre-existing Unity/IL2CPP `Odyssey.Rules`-via-`SqliteCharacterRepository` blocker was independently re-derived in this closeout, not accepted on the prior session's own say-so.** Compared `Odyssey.Persistence.asmdef`'s own `references` array against `SqliteCharacterRepository.cs`'s own `using Odyssey.Rules.Character;` statement, then repeated the identical comparison against a clean `git show origin/main:...` checkout of both files — byte-for-byte identical in both cases, proving the gap predates this task, this range, and this session entirely. Confirmed via `com.odyssey.persistence/package.json` that this is a real Unity-auto-discovered embedded package (not a `.NET`-only folder Unity would never try to compile), so the gap is a genuine Unity/IL2CPP compile blocker, not a false alarm. Per the governing closeout ТЗ's own explicit instruction, this task does not fix it (fixing it would mean editing a `.asmdef` file, outside this task's own allowed paths, and belongs to whoever owns that cross-cutting module-boundary concern) — it is honestly documented here, in the ExecPlan, and in the PR description instead. Authority: direct `.asmdef`/`.cs` file comparison on both this branch and a clean `origin/main`; `com.odyssey.persistence/package.json`'s own confirmation of real Unity package status; the governing closeout ТЗ's own explicit "document, do not fix" instruction.
- 2026-09-13 — **Housekeeping fix during this closeout: `Tests/Metadata/test-catalog.json`'s own newly-added `TC-ACTIVEEFFECT-051`-`068` rows were reformatted from a compact, no-space JSON style to match the file's own established spaced convention** (every other row in the file uses `{ "key": "value", ... }` with spaces after colons/commas and around braces). A first reformatting attempt using an overly broad regex briefly corrupted then (via a mistaken `git checkout --`) entirely discarded these 18 uncommitted rows; they were reconstructed verbatim from this same session's own prior tool output (which had already captured their exact original text) and re-verified against the test file list and `dotnet test`'s own reported new-test count (18) before proceeding. No test content, ID, or authority citation was altered — purely a whitespace-formatting correction, fully recovered and double-checked.
