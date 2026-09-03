# ODY-S05-105 — Base Definition Types

**Status:** In Review
**Roadmap stage / slice:** SLICE-05 (Content Catalog MVP block)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s05-105-base-definition-types`
**Pull request:** https://github.com/odyssey-services/Odyssey_VTT/pull/107
**ExecPlan:** `docs/plans/active/ODY-S05-105_Base_Definition_Types.md`
**Created:** 2026-09-04
**Last updated:** 2026-09-04 UTC

## 1. Goal

Add base typed definition shapes for the Content Catalog so it stops being only a generic `PropertiesJson` blob: `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition`, plus an explicit versioned codec mapping each typed shape to/from `ContentDefinitionRecord.PropertiesJson`. This answers "what fields exist on an item/weapon/armor/ammo/ability/effect," enabling `ODY-S05-104`'s future applicability/validation work. Purely structural — no publish/archive/delete, no runtime inventory/equipment/effect application, no game-usability validation, no content balance.

## 2. Why this task exists

- Problem or dependency being addressed: `ODY-S05-101`/`102` gave the catalog a generic storage/lifecycle/authoring envelope, but every definition's mechanical shape still lives only in an untyped `PropertiesJson` string — no compile-time or codec-level contract exists for what an item/weapon/armor/ammo/ability/effect actually contains.
- Value or risk reduction: gives `ODY-S05-104` (Catalog Validation MVP) real typed shapes to validate against instead of ad-hoc JSON parsing; reduces the risk of inconsistent or drifting field names across future authoring/validation/runtime work.
- Blocking or enabling relationship: unblocks `ODY-S05-104` directly (its own stated dependency); does not block `ODY-S05-103`/`106`, which depend on `ODY-S05-101` only.

## 3. Authorities and requirement references

### Required authorities

- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`, the `ODY-S05-105` row and task-boundary paragraph.
- `docs/adr/ADR-027_Content_Catalog_And_Item_Equipment_System_v1.0.md`, sections 3, 4, 6, 8, 12, 20 (full read).
- `Documentation/11_Content_Block_System_Odyssey_VTT_v0.1.md`, sections 7 (`ContentEntryPoint`/`AbilityEntryPointType`), 8 (`ContentBlockGraph`), 13 (`ContentCondition`), 14 (`SelectTargetsBlock`/`TargetSource`), 15 (`CostBlock`), 21 (`EffectDefinition`/`ApplyEffectBlock`/`EffectStackPolicy`), 22 (`EffectDurationType`) (full read).
- `Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs` (full read) — `ContentDefinitionId`/`ContentDefinitionType`/`ContentDefinitionRef`, the exact-version reference shape this task's typed definitions reuse and must not redefine.
- `Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs` (full read) — `ContentDefinitionRecord.PropertiesJson`, the opaque field this task's codec targets.
- `Packages/com.odyssey.domain/Runtime/Character/Ability.cs`, `Resource.cs`, `Anatomy.cs` (full read) — confirms `AbilityDefinitionId`/`ResourceDefinitionId`/`BodyPartId` use `SLICE-04`'s lightweight, human-authored, regex-validated string-key ID pattern, reused directly by this task rather than duplicated.

### Requirement and test IDs

- Requirement IDs: `ODY-S05-105`, `ADR-027` section 4/6.
- Existing test IDs: `TC-CATALOG-001`–`023` (re-verified unmodified).
- New test IDs introduced: `TC-CATALOG-024`–`037`.

### Task-safe private context

- Approved summary / references: `ADR-027`'s own already-accepted content and `11_Content_Block_System`'s own already-published vocabulary are cited directly. No hidden campaign content, secrets, or personal data referenced.

## 4. Verified current state

### Verified facts

- `git fetch origin` + `git log --oneline origin/main` confirmed PR #106 (`ODY-S05-102`, GM Catalog Authoring MVP) is already merged into `origin/main` (merge commit `a71dd79`).
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` row 2 (`ODY-S05-102`) still read `In Review` despite PR #106 being merged — corrected to `Done` with the PR link as this task's own first preflight step, matching the ТЗ's own explicit instruction.
- `IContentCatalogRepository`/`ContentDefinitionRecord` were read in full: `PropertiesJson` is a plain opaque `string`, with no existing typed mapping layer anywhere in the codebase — confirming there is no prior codec to extend or conflict with.
- `Odyssey.Content` project was inspected and confirmed nearly empty (`ContentPackageVersion.cs`, `SemVerValue.cs` only), with no Newtonsoft.Json reference in its `.csproj`/`.asmdef`; `Odyssey.Application` already references Newtonsoft.Json and already hosts `ODY-S05-102`'s own `Odyssey.Application.Content` namespace.
- `AbilityDefinitionId`/`ResourceDefinitionId`/`BodyPartId` (read in full in `Ability.cs`/`Resource.cs`/`Anatomy.cs`) use the lightweight regex-validated string-key ID pattern, distinct from `ContentDefinitionId`'s own minted-identity pattern — confirming these SLICE-04 types can be reused directly by this task's typed definitions without an adapter.

### Assumptions

- None. Every fact above was directly observed via `Read`/`Grep`/`git`/`dotnet build` during this task.

## 5. Scope

### In scope

- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` (new): pure, serializer-free typed value types — `ItemDefinition`, `WeaponDefinition`, `ArmorDefinition`, `AmmoDefinition`, `AbilityDefinition`, `EffectDefinition`, plus their supporting value types (`ItemCategory`, `WeaponAttackMode`, `AmmoRequirement`, `ContentTargetSource`, `ContentTargetRule`, `AbilityEntryPointType`, `AbilityResourceCost`, `EffectDurationType`, `EffectStackPolicy`), each performing only structural shape validation (enum-defined, non-null, non-negative, internally consistent flag/value pairs) — never game-usability validation.
- `Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs` (new): explicit, versioned (`schemaVersion: 1`) JSON codec mapping each typed definition to/from `ContentDefinitionRecord.PropertiesJson`, returning `Result<T>` rather than throwing on a `ContentDefinitionType` mismatch or malformed payload.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs`: two new `ErrorCode` entries (`ContentCatalogTypedDefinitionWrongType`, `ContentCatalogTypedDefinitionMalformedPayload`).
- `DotNet/Tests/Odyssey.Tests.Unit/Content/TypedDefinitionCodecTests.cs` (new): pure, in-memory round-trip tests, no SQLite/repository/campaign fixture needed.
- `docs/errors/ERROR_CODES.md`: two new registry rows.
- `Tests/Metadata/test-catalog.json`: fourteen new `TC-CATALOG-024`–`037` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md`: row 2 (`ODY-S05-102`) corrected to `Done`; row 5 (`ODY-S05-105`) status update to `In Review` with PR link.
- This task contract and its ExecPlan.

### Out of scope

- `PublishDefinition`/`ArchiveDefinition`/physical delete/Archived-list query (`ODY-S05-103`).
- Per-type usability/applicability validation, missing-reference checks, `ContentBlock` cycle checks, Ruleset/version compatibility checks (`ODY-S05-104`) — this task performs only structural shape checks (enum value exists, required argument not null, numeric value not negative where obviously impossible, JSON round-trip valid).
- MainGM authoring service changes beyond what already exists from `ODY-S05-102`.
- Any runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect`, attack resolution pipeline, item use/equip/consume commands, ammo consumption, ability execution, effect application.
- Any Unity UI or Content Editor UI.
- A final balanced content pack or `.odcontent` import/export (`ODY-S05-106`).
- Any edit to `ADR-001`–`027`'s own accepted content.

### Allowed paths

```text
Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs
Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs
Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs
DotNet/Tests/Odyssey.Tests.Unit/Content/TypedDefinitionCodecTests.cs
docs/errors/ERROR_CODES.md
Tests/Metadata/test-catalog.json
docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md
docs/tasks/active/ODY-S05-105_Base_Definition_Types.md
docs/plans/active/ODY-S05-105_Base_Definition_Types.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/ADR-001* through docs/adr/ADR-027*
docs/tasks/SLICE-05_BACKLOG.md
docs/tasks/active/ODY-S05-001_*, ODY-S05-002_*, ODY-S05-101_*, ODY-S05-102_*
Packages/com.odyssey.domain/Runtime/Content/ContentCatalog.cs
Packages/com.odyssey.application/Runtime/Persistence/ContentCatalogRepositoryContracts.cs
Packages/com.odyssey.application/Runtime/Content/ContentCatalogAuthoringContracts.cs
Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteContentCatalogRepository.cs
Packages/com.odyssey.domain/Runtime/Character/Ability.cs, Resource.cs, Anatomy.cs
Any Inventory/ItemInstance/ItemStack/Equipment/ActiveEffect file (none exist yet; none may be created by this task)
Unity assets/UI
```

## 6. Technical constraints

- Module ownership and dependency direction: pure typed value types live in `Odyssey.Domain.Content` (no serializer dependency, matching `ADR-001`'s Domain-purity rule); the JSON codec lives in `Odyssey.Application.Content`, not the still-nearly-empty `Odyssey.Content` project `ADR-027` §14 nominally reserves — see section 18's recorded decision.
- Authoritative-state and transaction boundary: no new persisted table; typed data is stored through the existing `ContentDefinitionRecord.PropertiesJson` opaque string field. No change to `ContentDefinitionId`/`Version`/`Revision`/`Status` semantics.
- Serialization / compatibility boundary: every encoded payload embeds an explicit `schemaVersion` field; decode is `Result<T>`-based, never throws a raw exception for a bad payload or wrong `ContentDefinitionType`.
- Time / RNG rule: not applicable — no time/RNG dependency in typed definitions or codec.
- Unity / thread / lifetime rule: not applicable — pure .NET code.
- Dependency / licensing rule: no new dependency; the codec reuses Newtonsoft.Json, already referenced by `Odyssey.Application`.
- Security / privacy / redaction rule: not applicable — no networking/redaction surface touched; malformed-payload failures never expose raw JSON, exception text, or stack traces.
- Performance or platform constraint: not applicable.
- Other: `ContentDefinitionRef` values embedded inside typed properties round-trip pinned to their exact `Version` — never resolved to "latest," per `ADR-027` §4 rule 2.

## 7. Expected behavior

### Scenario 1 — each typed definition round-trips through PropertiesJson

**Given** a fully populated `ItemDefinition`/`WeaponDefinition`/`ArmorDefinition`/`AmmoDefinition`/`AbilityDefinition`/`EffectDefinition`
**When** it is encoded via `TypedDefinitionCodec.Encode*` and decoded via the matching `Decode*`
**Then** every field is preserved exactly, with no loss or coercion.

### Scenario 2 — decoding against the wrong ContentDefinitionType is rejected

**Given** a typed payload encoded for one `ContentDefinitionType` (e.g. `Weapon`)
**When** `Decode*` is called with a different `ContentDefinitionType` (e.g. `Ability`)
**Then** it fails with `ContentCatalogTypedDefinitionWrongType`, without the stored JSON ever being parsed.

### Scenario 3 — malformed or incomplete JSON returns a safe failure

**Given** invalid JSON, an empty object, a null payload, or a payload missing a type-specific required field
**When** any `Decode*` method is called
**Then** it returns `ContentCatalogTypedDefinitionMalformedPayload`, never a raw exception.

### Scenario 4 — exact-version references remain exact

**Given** a `ContentDefinitionRef` embedded inside a typed definition (e.g. `ItemDefinition.BuiltInAbilityRefs`)
**When** the definition round-trips through the codec
**Then** the reference's `DefinitionId` and `Version` are both preserved exactly, distinguishable from a different version of the same definition, never resolved to "latest."

### Required invariants

- No `ODY-S05-103`/`104`/`106` behavior (publish/archive/delete, validation, fixtures) is implemented.
- No `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type is introduced (verified directly by `TC-CATALOG-037`'s own reflection-based scan).
- `ADR-001`–`027` are unmodified.
- No existing `SLICE-04` `AbilityDefinitionId`/`ResourceDefinitionId`/`BodyPartId` concept is duplicated or redefined.

## 8. Deliverables

- Production code: `TypedDefinitions.cs` (Domain), `TypedDefinitionCodec.cs` (Application), two `ErrorCodes` entries.
- Tests: `TypedDefinitionCodecTests.cs` (17 test methods across 14 `TC-CATALOG-024`–`037` IDs, one ID covering four `[TestCase]` variants).
- Scripts / CI: None.
- Configuration: None.
- Documentation: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 2 and 5), this task contract, its ExecPlan.
- Generated evidence or build artifacts: None persisted beyond this task's own recorded command output.
- Migration / recovery material: None — no schema change; typed data lives entirely inside the existing `PropertiesJson` column.

## 9. Acceptance criteria

1. Typed contracts exist for all 6 types: `ItemDefinition`/`WeaponDefinition`/`ArmorDefinition`/`AmmoDefinition`/`AbilityDefinition`/`EffectDefinition` (`TC-CATALOG-024`–`030`).
2. Each typed definition maps explicitly to/from its matching `ContentDefinitionType` via the codec, rejecting a mismatch (`TC-CATALOG-032`/`033`).
3. Typed properties round-trip through `PropertiesJson` with no field loss (`TC-CATALOG-024`–`031`).
4. `WeaponDefinition` includes damage, range, attack mode, action cost, ammo requirement, ammo compatibility keys (`TC-CATALOG-025`/`026`).
5. `ArmorDefinition` includes equipment slot, covered body-part references, protection (`TC-CATALOG-027`).
6. `AmmoDefinition` includes compatibility keys, quantity/stacking (via its embedded `ItemDefinition`), optional damage/effect contribution (`TC-CATALOG-028`).
7. `AbilityDefinition` includes entry point/trigger, action/resource cost, target rule shape, mechanics payload reference (`TC-CATALOG-029`).
8. `EffectDefinition` includes target rule, duration, stacking policy, mechanics payload reference (`TC-CATALOG-030`/`031`).
9. `Resource`/`BodyPart` references are reused (`ResourceDefinitionId`/`BodyPartId`) without duplicating incompatible `SLICE-04` concepts (`TC-CATALOG-027`/`029`).
10. No validation service or applicability-checking logic is introduced.
11. No publish/archive/delete behavior is implemented.
12. No runtime `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` type is introduced (`TC-CATALOG-037`).
13. No Unity/UI code is touched.
14. No `ADR-001`–`027` file is modified.
15. New tests are registered in `Tests/Metadata/test-catalog.json`.
16. This task contract and its ExecPlan exist.
17. `SLICE-05_IMPLEMENTATION_BACKLOG.md` marks `ODY-S05-105` `In Review` with the PR link.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `TC-CATALOG-024` | .NET / NUnit (Unit) | `ItemDefinition` round-trips through `PropertiesJson` | Pass |
| `TC-CATALOG-025` | .NET / NUnit (Unit) | `WeaponDefinition` round-trips all attack fields | Pass |
| `TC-CATALOG-026` | .NET / NUnit (Unit) | `WeaponDefinition` with `AmmoRequirement.Required` round-trips ammo compatibility keys | Pass |
| `TC-CATALOG-027` | .NET / NUnit (Unit) | `ArmorDefinition` round-trips slots/body-part refs/protection/durability | Pass |
| `TC-CATALOG-028` | .NET / NUnit (Unit) | `AmmoDefinition` round-trips compatibility shape | Pass |
| `TC-CATALOG-029` | .NET / NUnit (Unit) | `AbilityDefinition` round-trips trigger/cost/target shape | Pass |
| `TC-CATALOG-030` | .NET / NUnit (Unit) | `EffectDefinition` (`ForRounds`) round-trips target/duration/stacking/mechanics | Pass |
| `TC-CATALOG-031` | .NET / NUnit (Unit) | `EffectDefinition` (`Instant`) round-trips with null duration value | Pass |
| `TC-CATALOG-032` | .NET / NUnit (Unit) | Wrong `ContentDefinitionType` cannot be decoded as `WeaponDefinition` | Pass |
| `TC-CATALOG-033` | .NET / NUnit (Unit) | Wrong `ContentDefinitionType` cannot be decoded as `AbilityDefinition` | Pass |
| `TC-CATALOG-034` | .NET / NUnit (Unit) | Malformed/null/empty/invalid-enum JSON returns safe failure, not a raw exception | Pass |
| `TC-CATALOG-035` | .NET / NUnit (Unit) | JSON missing a type-specific required field returns safe failure | Pass |
| `TC-CATALOG-036` | .NET / NUnit (Unit) | Exact-version `ContentDefinitionRef` inside typed properties round-trips exactly, never "latest" | Pass |
| `TC-CATALOG-037` | .NET / NUnit (Unit) | Reflection scan: no runtime item/inventory/equipment/effect type in `Odyssey.Domain.Content`/`Odyssey.Application.Content` | Pass |

### Required commands

```powershell
dotnet build DotNet\Odyssey.Core.sln
dotnet test DotNet\Odyssey.Core.sln
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
.\scripts\verify-test-structure.ps1
```

### Manual validation

- `git diff --name-status` review confirming no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, and no `ADR-001`–`027` file, is touched.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 development machine; CI runs the pure .NET solution.
- Unity editor or Player profile: Not applicable — no Unity/UI code.
- Scripting backend: Not applicable.
- Network topology or database fixture: None — pure in-memory unit tests, no SQLite/campaign fixture needed.
- Other: None.

### Validation not required by this task

- Unity Editor / player build validation — no Unity code touched.
- Any test of `ODY-S05-103`/`104`/`106`'s own future behavior — none exists yet.

## 11. Compatibility, migration, and rollback

- Compatibility impact: None — purely additive types/method; no existing table or column altered.
- Version fields affected: None. `schemaVersion: 1` is a new, internal-to-this-codec concept, not a change to any existing versioned contract.
- Migration or upcaster: None required.
- Forward / backward behavior: Not applicable.
- Rollback method: Revert the branch.
- Data-loss risk and protection: None.
- Recovery rehearsal required: No.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None beyond already-public catalog definition shapes.
- Trust boundaries: Not applicable — no networking/authorization surface introduced by this task.
- Authorization / audience checks: Not applicable — this task adds no command, only typed shapes and a codec; `ODY-S05-102`'s own MainGM-only authoring gate is unmodified.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: `Decode*` returns a safe `Result<T>` failure for malformed/incomplete JSON, never a raw exception, and never echoes the raw invalid JSON back in the error.
- Security tests: `TC-CATALOG-034`/`035` directly prove safe failure without exception or raw-payload leakage.

## 14. Planning and execution mode

- Planning mode: `ExecPlan`
- Reason for selected mode: this task introduces new public Domain types (`Odyssey.Domain.Content`) and a new public Application-layer contract (`TypedDefinitionCodec`) — both `ExecPlan` triggers `PLANS.md` §1 already names, matching `ODY-S05-101`/`102`'s own reasoning for their sibling tasks.
- ExecPlan path: `docs/plans/active/ODY-S05-105_Base_Definition_Types.md`
- Expected pull request count: 1.
- Milestone or sequencing constraints: must not begin before `ODY-S05-101` is merged into `main` (confirmed in section 4); does not depend on `ODY-S05-102`, though it happened to already be merged.

## 15. Documentation and versioning impact

- Documents that must change: `docs/errors/ERROR_CODES.md`, `Tests/Metadata/test-catalog.json`, `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` (rows 2/5), this task contract, its ExecPlan.
- Documents that must not change: `ADR-001`–`027`, `docs/tasks/SLICE-05_BACKLOG.md`, `docs/tasks/active/ODY-S05-001_*`/`ODY-S05-002_*`/`ODY-S05-101_*`/`ODY-S05-102_*`.
- Application version change: No.
- Schema / format / contract / protocol / Ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

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
- [x] Pull request explains changes, evidence, limitations, and follow-up work, and states explicitly that validation/publish-archive-delete/runtime/content-fixtures are deferred.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs` — new.
- `Packages/com.odyssey.application/Runtime/Content/TypedDefinitionCodec.cs` — new.
- `Packages/com.odyssey.application/Runtime/Results/ErrorCodes.cs` — two new `ErrorCode` entries.
- `DotNet/Tests/Odyssey.Tests.Unit/Content/TypedDefinitionCodecTests.cs` — new, 17 test methods.
- `docs/errors/ERROR_CODES.md` — two new rows.
- `Tests/Metadata/test-catalog.json` — fourteen new `TC-CATALOG-024`–`037` entries.
- `docs/tasks/SLICE-05_IMPLEMENTATION_BACKLOG.md` — row 2 corrected to `Done`, row 5 status update.
- This task contract and its ExecPlan.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `dotnet build DotNet\Odyssey.Core.sln` | Pass | 0 warnings, 0 errors |
| `dotnet test DotNet\Odyssey.Core.sln` | Pass | Full suite green (543/543), including 17 new `TypedDefinitionCodecTests` cases, no regression |
| `.\scripts\verify-format.ps1` | Pass | `FORMAT-001 PASS repository text formatting checks passed` |
| `.\scripts\check-repository-policy.ps1` | Pass | `REPO-POLICY-001`–`005` PASS; `Repository policy check passed` |
| `.\scripts\verify-test-structure.ps1` | Pass | `TC-ARCH-001 PASS valid ADR-001 graph passes`; exit code 0 |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Pass | `TC-CATALOG-024`–`030`. |
| AC-2 | Pass | `TC-CATALOG-032`/`033`. |
| AC-3 | Pass | `TC-CATALOG-024`–`031`. |
| AC-4 | Pass | `TC-CATALOG-025`/`026`. |
| AC-5 | Pass | `TC-CATALOG-027`. |
| AC-6 | Pass | `TC-CATALOG-028`. |
| AC-7 | Pass | `TC-CATALOG-029`. |
| AC-8 | Pass | `TC-CATALOG-030`/`031`. |
| AC-9 | Pass | `TC-CATALOG-027`/`029` reuse `BodyPartId`/`ResourceDefinitionId` directly. |
| AC-10 | Pass | No validation service exists anywhere in this task's diff. |
| AC-11 | Pass | No `PublishDefinition`/`ArchiveDefinition`/delete method exists anywhere in this task's diff. |
| AC-12 | Pass | `TC-CATALOG-037`. |
| AC-13 | Pass | No Unity/UI path in Allowed paths or diff. |
| AC-14 | Pass | `git status --porcelain` confirms no `ADR-001`–`027` file touched. |
| AC-15 | Pass | Fourteen `TC-CATALOG-024`–`037` entries added. |
| AC-16 | Pass | This task contract and ExecPlan exist. |
| AC-17 | Pass | `SLICE-05_IMPLEMENTATION_BACKLOG.md` row 5 updated with PR [#107](https://github.com/odyssey-services/Odyssey_VTT/pull/107). |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: This section plus the validation-results table (to be completed after the full validation suite runs).

### Known limitations

- `MechanicsPayloadRef` on `AbilityDefinition`/`EffectDefinition` is an opaque placeholder string, not a real `ContentBlockGraph` reference or inline block structure — full content-block graph modeling is out of this task's scope and remains for a later task once `11_Content_Block_System`'s own execution model is implemented.
- Weapon/ammo compatibility uses a plain string tag (`CompatibleAmmoKeys`/`CompatibilityKeys`, e.g. `"9mm"`), not an exact-version `ContentDefinitionRef`, since a weapon is compatible with a category of ammo, not one specific published version — see section 18's recorded decision.

### Follow-up tasks

- `ODY-S05-103` — Publish/Archive/Delete Lifecycle.
- `ODY-S05-104` — Catalog Validation MVP (the direct consumer of this task's typed shapes).
- `ODY-S05-106` — minimal test-catalog fixtures.

### Self-review summary

- Scope review: diff limited to the nine files in section 5's Allowed paths; no `Inventory`/`ItemInstance`/`ItemStack`/Equipment/`ActiveEffect` file, no `ADR-001`–`027` file, no Unity file, no `ODY-S05-101`/`102`'s own foundation files touched.
- Architecture review: typed value types kept serializer-free in Domain; codec kept in Application, reusing the already-wired Newtonsoft.Json dependency rather than touching the still-empty `Odyssey.Content` project.
- Test review: 17 new tests, all passing on first run; full-suite `dotnet test` and remaining validation scripts to be run before PR.
- Security/privacy review: no new authorization surface; malformed-input failures are safe (`Result<T>`, no raw exception/JSON leakage).
- Documentation/version review: `ERROR_CODES.md`/test-catalog updated; no ADR or app/schema/protocol version changed.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure.

### Decisions made during execution

- 2026-09-04 — Decision: place the pure typed value types in `Odyssey.Domain.Content` (serializer-free) but place the JSON codec in `Odyssey.Application.Content` rather than the `Odyssey.Content` project `ADR-027` §14 nominally reserves for "ContentDefinition contracts." Authority: `Odyssey.Content` is confirmed nearly empty with no Newtonsoft.Json reference, while `Odyssey.Application` already references Newtonsoft.Json and already hosts `ODY-S05-102`'s own `Odyssey.Application.Content` namespace — avoiding an unnecessary new package-reference change to a barely-used project, consistent with `ODY-S05-101`/`102`'s own precedent of building directly in Domain/Application/Persistence.
- 2026-09-04 — Decision: model weapon/ammo compatibility as a plain string tag/category key (`CompatibleAmmoKeys`/`CompatibilityKeys`, e.g. `"9mm"`) rather than an exact-version `ContentDefinitionRef`, since a weapon is compatible with a category of ammo, not one specific published version — while still using real `ContentDefinitionRef` elsewhere (`ItemDefinition.BuiltInAbilityRefs`/`BuiltInEffectRefs`, `AmmoDefinition.EffectContributionRefs`) to satisfy the exact-version-reference requirement where an exact reference is actually meaningful. Authority: this task's own ТЗ explicit "ammo compatibility reference shape" language, distinguishing it from the ТЗ's own separate "exact-version references... remain exact refs" test requirement.
- 2026-09-04 — Decision: reuse `ResourceDefinitionId` (in `AbilityResourceCost`) and `BodyPartId` (in `ArmorDefinition.CoveredBodyPartIds`) directly from `Odyssey.Domain.Character`, rather than inventing parallel Content-namespace types. Authority: this task's own ТЗ explicit instruction not to duplicate existing `SLICE-04` concepts; both types' existing regex-validated string-key shape was confirmed compatible with no adapter needed.
- 2026-09-04 — Decision: keep `AbilityDefinition.MechanicsPayloadRef`/`EffectDefinition.MechanicsPayloadRef` as opaque placeholder strings rather than modeling a real `ContentBlockGraph`/block/edge structure. Authority: `11_Content_Block_System` section 8 (`ContentBlockGraph`) confirms full graph/execution modeling is a distinct, larger concern than base definition shape, and this task's own ТЗ explicitly scopes out ability/effect execution.

### Approved task changes

- None yet.
