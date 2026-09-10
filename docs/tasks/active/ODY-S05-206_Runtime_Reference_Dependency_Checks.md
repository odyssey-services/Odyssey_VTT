# ODY-S05-206 — Runtime Reference Dependency Checks

**Status:** In Progress
**Roadmap stage / slice:** SLICE-05
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-206-runtime-reference-dependency-checks`
**Pull request:** Not opened
**ExecPlan:** `docs/plans/active/ODY-S05-206_Runtime_Reference_Dependency_Checks.md`
**Created:** 2026-09-11
**Last updated:** 2026-09-11 UTC

## 1. Goal

Close, or explicitly and concretely defer, the two `SLICE-04` dependency-check stubs that `ADR-027` §9 unblocks, and add the symmetric catalog-side check so physically deleting a Draft `ContentDefinition` can detect runtime `ItemInstance`/`ItemStack` references, not only catalog references.

## 2. Why this task exists

- Problem or dependency being addressed: `ADR-027` §9 names two `SLICE-04` stubs (`RemoveBodyPart` item dependency; `DeleteCharacterPermanently` inventory/item dependency checker) that could not be implemented before an Inventory runtime existed. `ODY-S05-201`–`204` now provide that runtime, so `ADR-027` §4.1 rule 4's literal "no catalog dependency exists, and no runtime reference exists" precondition can finally be satisfied by real code on the deletion paths.
- Value or risk reduction: an irreversible `DeleteCharacterPermanently` must fail closed when the character still owns Inventory items; a `DeleteDraftDefinition` must have a real (not commented-out) runtime-reference gate ready for the day an Archived-definition physical-delete path exists.
- Blocking or enabling relationship: closes the concrete follow-ups `ADR-027` §9 assigned to this task; leaves Equipment/`ActiveEffect`-shaped dependencies as named, deferred work anchored to `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`
- `AGENTS.md`
- `PLANS.md`
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md` §4.1 (archive / physical deletion of catalog definitions), §5 (Inventory is a separate aggregate root), §7 (Equipment model — described, not implemented), §9 (the exact text of both `SLICE-04` stub closures).
- `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §5.2 (host-authoritative re-check before `DeleteCharacterPermanently`; do not trust a client preview).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 6 (`ODY-S05-206`), §7.1 (task boundary — an unclosable stub must add an explicit follow-up task ID, not an unnamed TODO), §8 (Reserved future blocks — Equipment runtime, item-sourced abilities/effects runtime — the only existing anchor for the deferral).

### Requirement and test IDs

- Requirement IDs: `ODY-S05-206`, `SLICE-05`; `ADR-027` §4.1 rule 4/5, §9.1, §9.2; `ADR-025` §5.2.
- Existing test IDs: `TC-INVENTORY-001`–`060`, `TC-CATALOG-*`, `CharacterArchivePhysicalDeleteTests` suite.
- New test IDs to introduce: `TC-INVENTORY-079`–`090`.

### Task-safe private context

- Approved summary / references: sanitized product-owner task brief only. No hidden campaign content, secrets, or private paths.

## 4. Verified current state

### Verified facts

- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs`: the constructor already accepts `IReadOnlyList<ICharacterDeletionDependencyChecker>? deletionDependencyCheckers = null` (defaults to `Array.Empty<…>()`); `DeleteCharacterPermanently` already runs every registered checker twice — a non-authoritative pre-check before the backup, and a host-authoritative re-check inside the pipeline transaction immediately before commit. No real `ICharacterDeletionDependencyChecker` implementation exists.
- `RemoveBodyPart` (and its doc-comment) states "NO Item/Inventory system exists anywhere in this codebase (confirmed by search)". That is now factually wrong — Inventory/`ItemInstance`/`ItemStack` exist since `ODY-S05-201`/`202`. The method's real, kept check is the internal one (other body parts / permanent modifications attached to the part being removed).
- Equipment (`ADR-027` §7) is **not** implemented. `InventoryLocationRef.Equipped(...)` exists only as a location kind + an equipment-slot string; no `EquippedEntry` with `BodyPartRefs[]` exists anywhere. There is no way to ask "what is equipped on body part X". `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8 lists "Equipment runtime" as a Reserved future block with no task ID yet.
- `ActiveEffect` is **not** implemented (also a Reserved future block).
- `ItemInstance`/`ItemStack` store `OwnerRef` (`OwnerKind`, `OwnerTargetRef`, `OwnerLocationKey`) independently of `LocationRef`. `InventoryOwnerRef.ForCharacter(...)` → `OwnerKind='Character'`, `OwnerTargetRef=<characterId>`. A single existence query on `OwnerKind='Character' AND OwnerTargetRef=<characterId>` therefore covers `ADR-027` §9.2's "Inventory ownership, equipped items, scene-dropped items still owned by the Character" at once, because `OwnerRef` does not change with `LocationRef`.
- `SqliteContentCatalogRepository.DeleteDraftDefinition` already runs `IsReferencedByAnotherDefinition` (a `LIKE` scan of `DependencyRefsJson`/`PropertiesJson` for the definition id) before its `DELETE`; the helper's own doc-comment calls itself "a defensive, forward-compatible safety net … not dead code" because `ContentDefinitionRef` pins `Version >= 1` and only `Status == Draft` (`Version == 0`) rows are physically deleted, so a Draft cannot be runtime-referenced today by construction. The constructor is `SqliteContentCatalogRepository(IWallClock clock)` — no dependency-checker parameter.
- `ContentDefinitionId` canonical form is `"cdef_" + 32 lowercase hex` — it contains the `_` character, which is a `LIKE` single-character wildcard, so `LIKE` patterns built from it must use `ESCAPE`.
- `ContentDefinitionRef.ToString()` is `"<definitionId>/<version>"` (e.g. `cdef_…/3`); `ItemInstance.SourceItemDefinitionRef` / `ItemStack.SourceItemDefinitionRef` store exactly that string.
- No composition root / production wiring exists in this codebase (only future Unity/host code). Checkers are opted into by callers (tests) passing them to a repository constructor.
- No local uncommitted `ODY-S05-206*` work exists; this task's contract and ExecPlan are authored from scratch.

### Assumptions

- None. All facts above were read directly from the branch off `origin/main` (`36d00e4`).

## 5. Scope

### In scope

- `IInventoryRepository.HasAnyItemOwnedByCharacter(...)` and `HasAnyRuntimeReferenceToDefinition(...)` — narrow, read-only existence queries (explicit `CampaignId`, `bool` result), implemented in `SqliteInventoryRepository` with the repository's existing `try/catch → InventoryIoFailed` wrapper.
- `InventoryCharacterDeletionDependencyChecker : ICharacterDeletionDependencyChecker` — a real checker that fails closed on an unreadable Inventory store.
- `IContentDefinitionDeletionDependencyChecker` (new interface, mirrors `ICharacterDeletionDependencyChecker`) + `InventoryContentDefinitionDependencyChecker` implementation.
- `SqliteContentCatalogRepository` — new optional constructor parameter `IReadOnlyList<IContentDefinitionDeletionDependencyChecker>? runtimeDependencyCheckers = null` (default empty), invoked inside `DeleteDraftDefinition` after `IsReferencedByAnotherDefinition` and before the `DELETE`; a blocking checker → new error code, nothing deleted.
- New error code `persistence.content_definition.runtime_referenced` + registry row.
- `RemoveBodyPart` — doc-comment (method-level and inline) corrected to the honest reason (Inventory exists, but no Equipment layer binds an item to a body part); code unchanged.
- `SqliteCharacterRepository` constructor doc-comment — corrected to note a real `InventoryCharacterDeletionDependencyChecker` now exists and is opted into explicitly.
- Tests `TC-INVENTORY-079`–`090`; `Tests/Metadata/test-catalog.json` registration.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 6 status.

### Out of scope

- A real item/equipment dependency check for `RemoveBodyPart` (architecturally impossible without Equipment runtime — see §4 and Known limitations).
- `ActiveEffect`-sourced/targeted dependencies for `DeleteCharacterPermanently` (`ActiveEffect` does not exist).
- Any change to `SplitItemStack`/`MergeItemStacks`/`InventoryStackOperationService` (`ODY-S05-205`, PR #119).
- Behavior changes to existing `DeleteCharacterPermanently` / `RemoveBodyPart` logic (only a new optional checker mechanism for the first; only doc text for the second).
- Unity/UI; a production composition root.

### Allowed paths

```text
Packages/com.odyssey.application/Runtime/Inventory/
Packages/com.odyssey.application/Runtime/Persistence/CharacterRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Persistence/InventoryRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCharacterRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteInventoryRepository.cs
DotNet/Tests/Odyssey.Tests.Persistence/
DotNet/Tests/Odyssey.Tests.Unit/
Tests/Metadata/test-catalog.json
docs/errors/ERROR_CODES.md
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-206_Runtime_Reference_Dependency_Checks.md
docs/plans/active/ODY-S05-206_Runtime_Reference_Dependency_Checks.md
```

`SqliteCharacterRepository.cs` edits are limited to doc-comments (constructor + `RemoveBodyPart` method-level and inline) — no behavior change.

### Paths requiring explicit approval before editing

```text
docs/adr/**
Assets/**
```

## 6. Technical constraints

- Module ownership and dependency direction: the new checkers live in `Odyssey.Application.Inventory` (which already depends on both `Odyssey.Application.Persistence` catalog and inventory contracts via `InventoryCreationService`); persistence stays the implementer of the read primitives and does not decide command legality.
- Authoritative-state and transaction boundary: the new inventory primitives are single-statement reads on their own short-lived connection, no transaction, no CAS. `DeleteCharacterPermanently` and `DeleteDraftDefinition` keep their existing transaction shape; the checkers are invoked from the existing call sites only.
- Serialization / compatibility boundary: no new table, column, or JSON contract. `HasAnyRuntimeReferenceToDefinition` matches the stored `ContentDefinitionRef` string by its `DefinitionId` prefix with an escaped `LIKE`.
- Time / RNG rule: not applicable (reads only).
- Unity / thread / lifetime rule: each primitive opens and disposes its own `SqliteConnection`.
- Dependency / licensing rule: no new dependency.
- Security / privacy / redaction rule: `ADR-025` §5.2 — fail closed: an unreadable Inventory store must block the irreversible delete. Checker return strings are fixed, safe phrases; a failure surfaces only `Error.Code`, never raw SQLite text or local paths.
- Performance or platform constraint: not applicable — bounded `EXISTS` queries.
- Other: `ADR-027` §9.1 (`RemoveBodyPart`) cannot be closed here; it is deferred with a named anchor per `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7.1.

## 7. Expected behavior

### Scenario 1 — character still owns an item

**Given** a persisted character that owns at least one `ItemInstance` or `ItemStack` (contained, or scene-dropped but still `OwnerKind='Character'`)
**When** `DeleteCharacterPermanently` runs with an `InventoryCharacterDeletionDependencyChecker` registered
**Then** it fails with `persistence.character.deletion_has_dependent`; no campaign backup is created and the `Character` row still exists (both the pre-check and the host-authoritative re-check reject).

### Scenario 2 — character owns nothing

**Given** a persisted character that owns no `ItemInstance`/`ItemStack`
**When** `DeleteCharacterPermanently` runs with the checker registered
**Then** it succeeds exactly as before (existing `CharacterArchivePhysicalDeleteTests` behavior is unchanged).

### Scenario 3 — Draft definition with a runtime reference

**Given** a Draft `ContentDefinition` and (seeded directly in the database for the test) an `ItemInstance`/`ItemStack` whose `SourceItemDefinitionRef` pins that definition id
**When** `DeleteDraftDefinition` runs with an `InventoryContentDefinitionDependencyChecker` registered
**Then** it fails with `persistence.content_definition.runtime_referenced`; the definition row is not deleted and no delete-ledger entry is written.

### Scenario 4 — Draft definition with no runtime reference

**Given** a Draft `ContentDefinition` with no runtime references
**When** `DeleteDraftDefinition` runs with the checker registered
**Then** it deletes the row as before (existing `SqliteContentCatalogRepositoryTests` behavior is unchanged).

### Required invariants

- A dependency checker that reports a blocking dependency always prevents the delete, never merely gets consulted.
- An `InventoryIoFailed` result from a dependency probe blocks the delete (fail closed).
- No checker return value or error message contains raw provider text, exception detail, or a local path.
- `RemoveBodyPart` and `DeleteCharacterPermanently` runtime behavior is byte-for-byte unchanged except for the added optional checker registration path.

## 8. Deliverables

- Production code: two `IInventoryRepository` primitives + implementations; `IContentDefinitionDeletionDependencyChecker`; `InventoryCharacterDeletionDependencyChecker`; `InventoryContentDefinitionDependencyChecker`; `SqliteContentCatalogRepository` optional checker list + `DeleteDraftDefinition` call; one new `ErrorCode`.
- Tests: `TC-INVENTORY-079`–`090` in `Odyssey.Tests.Persistence`.
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/errors/ERROR_CODES.md` row; corrected doc-comments; this contract; ExecPlan; backlog row 6.
- Generated evidence or build artifacts: validation command output in §17.
- Migration / recovery material: None (no schema change).

## 9. Acceptance criteria

1. `DeleteCharacterPermanently` with `InventoryCharacterDeletionDependencyChecker` registered rejects (with `persistence.character.deletion_has_dependent`, no backup, no row deletion) when the character owns any `ItemInstance` or `ItemStack`, for both a contained item and a scene-dropped-but-owned item.
2. `DeleteCharacterPermanently` with the checker registered still succeeds when the character owns nothing, and every pre-existing `CharacterArchivePhysicalDeleteTests` test still passes.
3. `HasAnyItemOwnedByCharacter` returns `true`/`false` correctly for `ItemInstance` and `ItemStack`, across `LocationKind` values, and returns `InventoryIoFailed` on an unreadable store; the checker fails closed on that failure.
4. `DeleteDraftDefinition` with `InventoryContentDefinitionDependencyChecker` registered rejects with `persistence.content_definition.runtime_referenced` (no row deletion, no delete-ledger entry) when a seeded runtime reference to the definition exists, and still deletes normally when none exists; every pre-existing `SqliteContentCatalogRepositoryTests` test still passes.
5. `HasAnyRuntimeReferenceToDefinition` matches any pinned version of the definition id, escapes `LIKE` metacharacters, and returns `InventoryIoFailed` on an unreadable store.
6. `RemoveBodyPart` behavior is unchanged; its doc-comments no longer claim "no Item/Inventory system exists".
7. New `ErrorCode` is registered in `docs/errors/ERROR_CODES.md` with a catalog test reference; `check-repository-policy.ps1` passes.
8. `dotnet build`, `dotnet test`, `verify-format.ps1`, `check-repository-policy.ps1`, `verify-test-structure.ps1` all pass with real recorded output.
9. `git diff --name-status` against `main` shows only §5 allowed paths.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-INVENTORY-079`–`082` | `.NET / dotnet test` | `HasAnyItemOwnedByCharacter`: true/false for `ItemInstance` and `ItemStack`, contained vs scene-dropped, campaign-boundary guard | Pass |
| `TC-INVENTORY-083` | `.NET / dotnet test` | `HasAnyItemOwnedByCharacter` on an unreadable store → `InventoryIoFailed` | Pass |
| `TC-INVENTORY-084`–`085` | `.NET / dotnet test` | `DeleteCharacterPermanently` + real checker: blocked when character owns a contained item / a scene-dropped-but-owned item; no backup, no row deletion | Pass |
| `TC-INVENTORY-086` | `.NET / dotnet test` | `DeleteCharacterPermanently` + real checker: still succeeds when the character owns nothing | Pass |
| `TC-INVENTORY-087` | `.NET / dotnet test` | `InventoryCharacterDeletionDependencyChecker` fails closed on an `InventoryIoFailed` probe | Pass |
| `TC-INVENTORY-088`–`089` | `.NET / dotnet test` | `HasAnyRuntimeReferenceToDefinition`: true for a seeded `ItemInstance`/`ItemStack` reference (any version), false otherwise, `LIKE` escaping | Pass |
| `TC-INVENTORY-090` | `.NET / dotnet test` | `DeleteDraftDefinition` + real checker: blocked with `runtime_referenced` for a seeded reference; deletes normally without one | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- Review the final diff to confirm no `RemoveBodyPart`/`DeleteCharacterPermanently` behavior change and no edit outside §5.

### Required environments / profiles

- OS / architecture: Windows x64.
- Unity editor or Player profile: Not applicable.
- Scripting backend: Not applicable (pure .NET).
- Network topology or database fixture: local temporary SQLite databases only.
- Other: `dotnet` SDK per `global.json`.

### Validation not required by this task

- Unity/IL2CPP/PlayMode — no Unity surface touched.
- Migration rehearsal — no schema change.
- A real Equipment or `ActiveEffect` dependency check — out of scope (see Known limitations).

## 11. Compatibility, migration, and rollback

- Compatibility impact: additive only — new optional constructor parameter (defaulted), new interface, new read methods, one new error code. Every existing caller compiles and behaves unchanged.
- Version fields affected: None.
- Migration or upcaster: None.
- Forward / backward behavior: Not applicable.
- Rollback method: revert the commits; no persisted data is written by this task.
- Data-loss risk and protection: none — this task only *adds* pre-delete gates.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: `CharacterId`, `ContentDefinitionId`, item ownership rows — none classified `Secret`/`HiddenGameplay`.
- Trust boundaries: local single-user SQLite only.
- Authorization / audience checks: unchanged — MainGM gating for delete stays where it is; this task adds only dependency gates.
- Redaction requirements: checker strings are fixed safe phrases; failures surface only `Error.Code`.
- Log-safe fields: nothing new is logged.
- Abuse / malformed input limits: `LIKE` metacharacters in the definition id are escaped.
- Security tests: `TC-INVENTORY-087` (fail-closed on unreadable store).

## 14. Planning and execution mode

- Planning mode: `ExecPlan`.
- Reason for selected mode: introduces a new public persistence-port interface (`IContentDefinitionDeletionDependencyChecker`), changes a repository constructor signature, and adds new repository read primitives — the same "public persistence port/schema" trigger that required an ExecPlan for `ODY-S05-205`.
- ExecPlan path: `docs/plans/active/ODY-S05-206_Runtime_Reference_Dependency_Checks.md`.
- Expected pull request count: 1 (Draft).
- Milestone or sequencing constraints: independent of `ODY-S05-205` by code; branches from `main`.

## 15. Documentation and versioning impact

- Documents that must change: this contract, its ExecPlan, `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 6, in-code doc-comments.
- Documents that must not change: `docs/adr/**` (see §18), `ADR-027`/`ADR-025`, `ACTIVE_DOCUMENTATION_BASELINE_*`, root `README.md`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

_Filled at the end of implementation._

### Changed files / areas

- _pending_

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Not run | — |
| `dotnet test DotNet\Odyssey.Core.sln` | Not run | — |
| `.\scripts\verify-format.ps1` | Not run | — |
| `.\scripts\check-repository-policy.ps1` | Not run | — |
| `.\scripts\verify-test-structure.ps1` | Not run | — |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1..9 | Not run | Implementation in progress. |

### Build and artifact evidence

- _pending_

### Known limitations

- **`RemoveBodyPart` item/equipment dependency check is deferred, not closed.** Inventory exists, but no Equipment layer (`ADR-027` §7 `EquippedEntry.BodyPartRefs[]`) exists anywhere, so "what is equipped on this body part" cannot be asked. Deferred to the **Equipment runtime** block named in `SLICE-05_IMPLEMENTATION_BACKLOG.md` §8; a concrete task ID will be assigned when that block is decomposed. No `ODY-S05-2xx` number is invented here.
- **`ActiveEffect`-sourced/targeted dependencies for `DeleteCharacterPermanently` are not checked** — `ActiveEffect` is a Reserved future block (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §8). Same deferral anchor.
- **A Draft `ContentDefinition` cannot be runtime-referenced today** by construction (`ContentDefinitionRef` pins `Version >= 1`; only `Version == 0` Drafts are physically deleted). The new catalog-side check is a real, executing forward-compatible gate for a future Archived-definition physical-delete path, not a check that will fire under today's public API.

### Follow-up tasks

- Equipment runtime block (`SLICE-05_IMPLEMENTATION_BACKLOG.md` §8) — will subsume the deferred `RemoveBodyPart` item/equipment dependency check and `ActiveEffect` deletion dependencies. `ODY-S05-207` (integration fixtures) is unaffected.

### Self-review summary

- _pending_

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-09-11 — `RemoveBodyPart` stub closure is deferred to the Equipment runtime block; only its doc-comments are corrected. Authority: this ТЗ §4 + `SLICE-05_IMPLEMENTATION_BACKLOG.md` §7.1.
- 2026-09-11 — A single `OwnerKind='Character' AND OwnerTargetRef=<id>` existence query covers `ADR-027` §9.2's three named ownership shapes at once, because `OwnerRef` is independent of `LocationRef`. Authority: `ADR-027` §7 rule 1 + code inspection.

### Open questions for the product owner

- Whether a short amendment note ("`RemoveBodyPart` closure deferred, see backlog §8") should also be added directly to `ADR-027` §9, rather than only recorded in this contract and the backlog. `docs/adr/**` is outside this task's allowed paths; no ADR edit was made. This is the product owner's call.

### Approved task changes

- None.
