# ADR-026 — Character Export/Import File Format and Redaction

**Документ:** `docs/adr/ADR-026_Character_Export_Import_File_Format_And_Redaction_v1.0.md`
**ADR:** ADR-026
**Версия:** 1.0
**Дата:** 2026-09-02
**Статус:** Accepted
**Область:** структура `.odchar`-файла (`manifest.json`/`character.json`/`portrait/`/`referenced-assets/`) и правило редакции полей при экспорте одного `Character` — то, что `ADR-025` §7.6/§8/§13 явно оставляет вне своего объёма
**Связанные этапы:** Roadmap Этап 5 (`SLICE-04`), backlog `ODY-S04-112`
**Базовые документы:** `docs/adr/ADR-022_Character_Aggregate_Section_Revisions_And_History_Projection_v1.0.md` (aggregate boundary, `CharacterRecord` shape), `docs/adr/ADR-023_Character_Drafts_Templates_And_Approval_Workflow_v1.0.md` (local-Draft/`BindDraftToCampaign` pipeline, reused unmodified by import), `docs/adr/ADR-025_Character_Ownership_Lifecycle_And_Ruleset_Migration_Operations_v1.0.md` §7.6/§8/§13 (import's Draft-creation/`RulesetVersion`-pinning already fixed there; this ADR fills the file-format/export-redaction gap it explicitly names), `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §3.4/§7 (`VisibilityPolicy` principle reused, not its live-connection mechanism), `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §24, `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 12)

---

# 1. Решение

`.odchar` — a directory-shaped bundle (`manifest.json` + `character.json` + optional `portrait/` + optional `referenced-assets/`) whose `character.json` is a redacted snapshot of one `CharacterRecord`. Redaction at export time reuses `ADR-019` §3.4's `VisibilityPolicy` **principle** — one authoritative state plus a role-driven filter, not two divergent copies — implemented as a new, local, synchronous filter over `CharacterRecord` (not `ADR-017`'s live `ClientProjection`/snapshot-delta machinery, which requires a connection/Membership/Scene context that a local file export does not have). `CharacterOwnership` is never carried into the file — a `UserId` is not portable across campaigns/accounts, and import already creates a fresh `CharacterId` via `ADR-023`'s pipeline (`ADR-025` §7.6). No field on today's `CharacterRecord` is classified as GM-only-visible or secret/credential-bearing by any accepted ADR; this ADR does not invent such a classification to satisfy product §24.1's wording — it fixes the mechanism (a named redaction filter with a defined extension point) so a future task that does introduce such a field wires it into this filter without reopening this ADR.

---

# 2. Контекст и проблема

`ADR-025` §7.6 fixes `.odchar` **import**'s Draft-creation and `RulesetVersion`-pinning behavior, reusing `ADR-023` unmodified. Its own §8 ("Не входит") and §13 ("Открытые вопросы") explicitly say the file format itself and the export side's field-visibility rule are out of that ADR's scope. `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` row 12 (`ODY-S04-112`) nonetheless requires `ExportCharacter`, described as "permission-filtered, GM-only fields excluded without the right, secrets excluded" — a real product requirement (`Documentation/10_...md` §24.1) with no ADR deciding *how*. Per `PLANS.md` §1.2/§3, a task must not invent this behavior inline; this ADR resolves it before `ODY-S04-112` is scoped as an implementation task.

---

# 3. Термины

## 3.1 `.odchar` bundle

A directory (or, at export time, a zip of that directory — the compression/container format is an implementation detail left to the task, not fixed by this ADR) containing exactly `manifest.json`, `character.json`, an optional `portrait/` file, and an optional `referenced-assets/` directory, per product §24's own tree.

## 3.2 Export redaction filter

A pure function `RedactCharacterForExport(CharacterRecord, ExportActorContext) -> CharacterExportPayload` run entirely inside `Odyssey.Application` before serialization — no network/session/connection dependency. `ExportActorContext` carries the acting `UserId`, whether that actor is MainGM, and the actor's ownership/control relationship to the exported Character (already available from `CharacterOwnership`, `ADR-022`/`ADR-025`).

## 3.3 GM-only export field

A field on `CharacterRecord` that a future ADR/task explicitly marks visible only to MainGM in an exported file. None exists today (section 5).

---

# 4. `.odchar` file structure

```text
character.odchar/
├── manifest.json
├── character.json
├── portrait/            (optional)
└── referenced-assets/   (optional)
```

`manifest.json` fields (minimum, additive-only going forward):

```text
FormatVersion            -- this ADR's format identity, starts at "1.0"
ExportedAt                -- UtcInstant
ExportedByRole             -- "MainGM" | "Owner" | "Controller" (ADR-019 role at export time)
SourceRulesetVersion       -- the exporting campaign's RulesetVersion at export time
```

`character.json` is `CharacterExportPayload` — the redacted projection of `CharacterRecord` (section 5). It carries the exported Character's own `RulesetVersion`/definitions references for `ADR-025` §7.6's own already-fixed re-pinning check at import/bind time; it does **not** carry `CharacterOwnership`, `CharacterId`, or `CampaignId` — import assigns all three fresh (`ADR-025` §7.6, `ADR-023`'s existing Draft pipeline), exactly mirroring `CAP-INV-006`'s existing spirit for templates.

`FormatVersion` bump rule: an additive field is a patch bump; a breaking structural change requires a superseding ADR, not a silent format drift — the same discipline `ADR-007` already applies to build identity.

---

# 5. Export field-visibility rule

**Decision:** export redaction is driven by the actor's `ADR-019` role/relationship to the Character, computed once per export call — not by a live per-connection `VisibilityPolicy`/`ClientProjection` (that mechanism assumes an active session and does not apply to a local file write).

- MainGM exporting any Character in their own campaign: full `CharacterExportPayload`, no field withheld.
- Owner/controller exporting their own assigned Character: full `CharacterExportPayload` as it stands today, because **no field on the current `CharacterRecord` is marked GM-only** (confirmed by direct inspection of `CharacterRepositoryContracts.cs`'s `CharacterRecord` constructor — identity, lifecycle, approval, ownership-excluded-per-section-4, mechanics, anatomy, resources, timestamps; nothing resembling a GM-hidden note or secret field exists yet). This is not a design gap in this ADR — it is an honest reflection of what the aggregate contains today.
- A future task that adds a GM-only-visible field (for example, hidden anatomy/damage detail, or a GM-private note) must route it through `RedactCharacterForExport`'s existing extension point (section 3.2) rather than adding a second, parallel redaction path.

`Documentation/10_...md` §24.1's "секретные токены и credentials исключаются" clause has no current target on `CharacterRecord` either — no credential-shaped field exists there (credentials belong to account/session identity, `ADR-018`, never to a Character). This ADR records the rule as a standing constraint on `RedactCharacterForExport` (never serialize a field of that shape if one is ever added) rather than asserting a test against data that does not exist.

---

# 6. Не входит в ADR-026

- `.odchar` import's own Draft-creation/`RulesetVersion`-pinning behavior — already fixed, `ADR-025` §7.6, unmodified here.
- Compression/container mechanics of the bundle (zip vs. directory), UI file-picker/save-dialog behavior, permission to *initiate* export/import as a command (an ordinary `ADR-019` action check, not a new mechanism).
- Any concrete GM-only or secret field — none is introduced by this ADR; a future ADR/task defines one if and when product requires it.
- Portrait/`referenced-assets/` file-integrity or size-limit handling.

---

# 7. Соответствие module boundaries (`ADR-001`) and existing ADRs

- `Odyssey.Domain` is untouched — no new domain invariant.
- `Odyssey.Application` owns `RedactCharacterForExport`, `ExportCharacter`, and `ImportCharacter`'s payload-to-Draft-seed mapping; `ImportCharacter` itself continues to hand off to `ADR-023`'s unmodified local-Draft/`BindDraftToCampaign` pipeline exactly as `ADR-025` §7.6 already fixed.
- `Odyssey.Persistence` reads the Character row to build the export payload; it does not decide redaction.
- `ADR-019` remains authoritative for role/permission semantics — this ADR reuses its `VisibilityPolicy` principle, not its `ClientProjection`/`ADR-017` delivery mechanism.
- `ADR-022` remains authoritative for `CharacterRecord`'s shape — this ADR only decides which of its already-defined fields cross the export boundary.
- `ADR-023`/`ADR-025` remain authoritative for import's Draft-creation and `RulesetVersion`-pinning — unmodified.

---

# 8. Правила для Codex

Codex обязан:

1. Implement `RedactCharacterForExport` as a plain synchronous Application-layer function taking `CharacterRecord` + `ExportActorContext`; never route export through `ADR-017`'s `ClientProjection`/snapshot-delta machinery.
2. Never serialize `CharacterOwnership`, `CharacterId`, or `CampaignId` into `character.json`; import always assigns fresh values via `ADR-023`'s existing pipeline.
3. Not invent a GM-only or secret field on `CharacterRecord` to satisfy product §24.1's wording; if no such field exists, export it plainly and say so in the task's own evidence, rather than fabricating a redaction test against non-existent data.
4. Give `RedactCharacterForExport` a single, named extension point for a future GM-only/secret field, so a later task does not need a second parallel redaction path.
5. Keep `manifest.json`'s `FormatVersion` additive-only for non-breaking changes; require a superseding ADR for a breaking structural change.
6. Continue routing `ImportCharacter`'s Draft creation through `ADR-023`'s unmodified pipeline and `RulesetVersion` re-pinning through `ADR-025` §7.6 exactly as already fixed — this ADR does not reopen either.

---

# 9. Definition of Done для будущей implementation-задачи

1. `ExportCharacter` by MainGM and by the Character's own owner both produce an identical `character.json` today (section 5), verified directly — proving the redaction filter runs, not that it is a no-op by omission.
2. Exported `character.json` never contains `CharacterOwnership`/`CharacterId`/`CampaignId`, verified directly against the serialized payload.
3. `ImportCharacter` against a previously exported file produces a fresh `CharacterId`, a Draft requiring approval, and a `RulesetVersion` pinned to the *target* campaign — reusing `ADR-023`'s/`ADR-025`'s own already-required acceptance evidence, not new evidence invented for this ADR.
4. Round-trip (`Export` then `Import` into a different campaign) preserves every mechanics/anatomy/resource value that `ADR-025` §7.6 does not otherwise require to change.
5. Core export/import logic compiles without Unity dependencies in the pure .NET path.

---

# 10. Рассмотренные альтернативы

## 10.1 Reuse `ADR-017`'s live `ClientProjection`/`VisibilityPolicy` pipeline directly for export

**Considered:** run the same Membership → `PermissionDecision` → `VisibilityPolicy` → `ClientProjection` pipeline `ADR-019` §7 already defines, treating an export as a one-shot "projection to a file" instead of to a socket. **Rejected** — that pipeline's inputs (Membership, Scene assignment, an active connection) do not exist for a local file export initiated against `campaign.db` directly, and forcing them into existence only to satisfy a pipeline built for a different problem would be a larger, riskier generalization than this ADR's actual scope needs.

**Accepted:** reuse the *principle* (single authoritative state, a redaction filter, no divergent copies) as a new, local, connection-free function (section 5).

## 10.2 Invent a GM-only/secret field now to make product §24.1's wording literally testable

**Considered:** add a placeholder "hidden GM note" field to `CharacterRecord` purely so `ExportCharacter`'s redaction has something real to withhold. **Rejected** — this would be unapproved scope expansion of `ADR-022`'s already-Accepted aggregate boundary, done only to manufacture test coverage, not because product currently requires that field to exist.

**Accepted:** record the extension point (section 5/8) and defer the actual field to whichever future task introduces it.

---

# 11. Открытые вопросы

No open questions for this ADR's scope.

Deferred but not open here:

- the concrete GM-only or secret Character field, if product ever requires one;
- `.odchar` bundle compression/container mechanics;
- `.odchar` `FormatVersion` 2+.

---

# 12. Трассировка

ADR реализует и уточняет:

- `Documentation/17_Roadmap_Odyssey_VTT_v0.11.md` §13.7/§13.9 (`.odchar`-import-new-Draft exit criterion, already covered by `ADR-025`; this ADR adds the export/file-format half);
- `Documentation/10_Characters_And_Progression_Odyssey_VTT_v0.2.md` §24 (`.odchar` export/import), the export-filtering half `ADR-025` explicitly left open;
- `docs/tasks/SLICE-04_IMPLEMENTATION_BACKLOG.md` (row 12, `ODY-S04-112`).

Existing ADRs reused without redefinition: `ADR-001` (module boundaries), `ADR-018` (identity/credentials live outside Character), `ADR-019` (`VisibilityPolicy` principle, role baseline), `ADR-022` (`CharacterRecord` shape), `ADR-023` (local-Draft/`BindDraftToCampaign` pipeline), `ADR-025` (import Draft-creation/`RulesetVersion`-pinning, unmodified).

---

# 13. Нормативное действие

Accepted by the product owner on 2026-09-02, before `ODY-S04-112` is scoped as an implementation task. With this acceptance:

- `.odchar` structure is exactly section 4's four entries, with `manifest.json`'s `FormatVersion` starting at `1.0`;
- export redaction runs through one named, extensible `RedactCharacterForExport` function, never through `ADR-017`'s live projection pipeline;
- `CharacterOwnership`/`CharacterId`/`CampaignId` are never serialized into `character.json`;
- no GM-only/secret Character field is introduced by this ADR — one is added only by a future ADR/task, through this ADR's own extension point;
- changing this file-format/redaction boundary requires an amendment or superseding ADR, not silent implementation drift.

---

**Конец документа**
