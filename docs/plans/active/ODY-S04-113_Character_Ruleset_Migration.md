# ODY-S04-113 — Character Ruleset Migration

**Status:** Active
**Owner:** Codex (agent)
**Branch:** `feat/ody-s04-113-character-ruleset-migration`
**Pull request:** [#97](https://github.com/odyssey-services/Odyssey_VTT/pull/97)
**Last updated:** 2026-09-03 UTC

## 1. Purpose and user-visible outcome

Implement `PreviewCharacterRulesetMigration` (read-only Query) and `ApplyCharacterRulesetMigration` (one-transaction apply, re-validating the plan against live state before commit), plus reverting an already-committed migration via `ADR-024`'s exact compensating-batch pattern — proving atomicity/rollback/tamper-detection/revert for the one case this codebase can currently test honestly (no real cross-Ruleset-version value-mapping algorithm exists anywhere): an identity-or-`UnresolvedDecisions` mapping, never a fabricated transformation. Thirteenth implementation task of `SLICE-04`.

## 2. Task contract

- Goal: implement Preview/Apply/Revert per `ADR-025` §7, deliberately scoped to the identity/`UnresolvedDecisions` mapping this task's own §4/§5 already decided — not a general Rules Engine transformation.
- Acceptance criteria: Preview never mutates state or writes an event; Apply re-derives the plan fresh from live state and rejects a stale/tampered plan (`PreviewHash` mismatch) without partial state change; a mid-apply failure leaves prior state completely unchanged (ordinary transaction atomicity); an unresolved definition reference blocks Apply until resolved; reverting an already-committed migration reuses the exact compensating-batch shape `ApplyCharacterRespec` already established (shared `CompensationGroupId`, compensating event referencing the original `CharacterRulesetMigrated` event); never routed through `ADR-013`'s schema migration runner; `dotnet build`/`dotnet test`/`verify-format.ps1`/`check-repository-policy.ps1` all pass; no `ADR-012`/`013`/`022`–`026` content change; no Unity/UI code; no value-transformation algorithm invented beyond identity/`UnresolvedDecisions`.
- Requirement IDs: `ODY-S04-113`, `ADR-025` §7.1–7.5, `ADR-013` §9, `ADR-024` §7.2/§7.4, product §25.
- In scope: `RulesetMigrationRules` (Rules, new file); `PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration`/`RevertCharacterRulesetMigration` + `CharacterRulesetMigrationPlan`/`RulesetDefinitionCatalog` DTOs (Application); `SqliteCharacterRepository` extension (Persistence); tests; error registry/test-catalog additions; backlog status update.
- Out of scope: any real cross-Ruleset-version content-transformation algorithm (no ADR/product decision, no second real Ruleset content exists to test against); `ADR-013`'s schema migration runner; a mandatory full-campaign backup (optional per `ADR-025` §7.5, not attempted — see Decisions); Anatomy-section migration (see Decisions — product's own `ValueChanges`/`DefinitionMappings` examples are consistently Attribute/Skill/Ability/Resource-definition-shaped, never `BodyPartId`-shaped); any Unity/UI code; any change to `ADR-012`/`013`/`022`–`026` content or `SLICE-04_IMPLEMENTATION_BACKLOG.md` beyond the status row.
- Required authorities: `ADR-025` §7 (full read), `ADR-013` §9 (full read), `ADR-024` (full read, compensating-batch pattern), `ADR-012` §5/§8.2, product §25 (full read), `Odyssey.Rules` (full inventory, confirming no mapping logic exists), `ApplyCharacterRespec`/`RevertAdvancementPurchase` (`ODY-S04-107`) — the binding structural precedent for both halves of this task.
- Required validation commands: `dotnet build`; `dotnet test`; `.\scripts\verify-format.ps1`; `.\scripts\check-repository-policy.ps1`.

## 3. Current state

- Local `main` fast-forwarded to `ODY-S04-112`'s own merge commit (PR #96), independently verified via `git merge-base --is-ancestor`.
- Direct inspection of `Packages/com.odyssey.rules/Runtime/` (8 files) confirms no cross-Ruleset-version value/definition-mapping computation exists anywhere — every existing test fixture across `ODY-S04-101`–`112` uses exactly one Ruleset (`ruleset.core`/`1.0.0`).
- `ADR-025` §7.2's own text: `PreviewCharacterRulesetMigration` builds `UnresolvedDecisions` for whatever it cannot resolve and lets the GM decide — an empty/identity `ValueChanges`/`DefinitionMappings` result with everything real routed to `UnresolvedDecisions` is a structurally valid, ADR-compliant outcome, not an invented shortcut.
- `ApplyCharacterRespec`/`RevertAdvancementPurchase` (`SqliteCharacterRepository.cs`) directly confirm the exact reusable mechanics: `SqliteSavingPipeline.AppendDomainEvent` (internal) for every non-final batch event, the batch's own trailing event through the normal `_pipeline.Execute` path, one shared `CompensationGroupId` (the command's own `CommandId.ToString()`), and `FindOriginatingEventSequence`-style direct `DomainEvents` lookups (here, simpler: by `CommandId` column directly, matching `ODY-S04-112`'s own `FindCharacterIdByDraftBoundCommandId` precedent) rather than a new ledger table.
- `DomainEvents` already carries `CommandId`/`OriginalEventId`/`CompensationGroupId`/`IsCompensating` columns — sufficient to find/guard a specific migration's own revert without a new table.
- `Tests/Metadata/test-catalog.json`'s last assigned Character test ID is `TC-CHAR-152` (`ODY-S04-112`).

Assumptions: none beyond what is directly observed above.

## 4. Proposed approach

- **`RulesetMigrationRules` (new, `Odyssey.Rules.Character`):** `BuildPlan(characterId, sourceRulesetId, sourceRulesetVersion, targetRulesetId, targetRulesetVersion, attributes, skills, abilities, resources, targetCatalog, now) -> CharacterRulesetMigrationPlan`. For every currently-purchased `AttributeDefinitionId`/`SkillDefinitionId`/`AbilityDefinitionId`/`ResourceDefinitionId`, checks whether `targetCatalog` (a caller-supplied set of recognized definition IDs per category — this codebase's own established "no real catalog exists, caller supplies a plain fixture" convention, exactly like `RulesAttributeCostRules`) still recognizes it: if yes, an identity `DefinitionMapping` entry (`SourceDefinitionId == TargetDefinitionId`, no `ValueChange`); if no, an `UnresolvedDecision` entry, never silently dropped or guessed at. `ValueChanges` stays empty in every case this task actually produces — no transformation algorithm exists to populate it, and none is invented. `PreviewHash` is a SHA-256 hex digest over a canonical JSON serialization of every field the plan's own correctness depends on (`CharacterId`, source/target Ruleset identity, the four `ExpectedXRevision` values captured at build time, and the resolved `DefinitionMappings`/`UnresolvedDecisions` content) — recomputed identically by `Apply` to detect a stale or tampered plan (`CAP-INV-004`).
- **`RulesetDefinitionCatalog` (new DTO, Application):** four `IReadOnlyCollection<string>` sets (recognized Attribute/Skill/Ability/Resource definition IDs in the target Ruleset version) — a deliberately simple, caller-supplied fixture shape, not a real catalog lookup (no such catalog exists anywhere in this codebase, confirmed by search).
- **`PreviewCharacterRulesetMigration`:** a read-only `ADR-002` §4.2 Query (no `CommandId`, no event, no mutation) in `ICharacterRepository`/`SqliteCharacterRepository` — reads the live `CharacterRecord`, calls `RulesetMigrationRules.BuildPlan`, returns the plan.
- **`ApplyCharacterRulesetMigration`:** MainGM-only (product §25's own process step 1, "GM выбирает новую версию Ruleset"). Re-reads the live `CharacterRecord` inside the transaction, re-derives the plan fresh from the SAME caller-supplied `targetCatalog` (never trusting the client-supplied `plan` parameter's own content directly), and compares the freshly-computed `PreviewHash` against the caller-supplied plan's own `PreviewHash` — a mismatch (stale Character state, or a tampered plan) is rejected before any write. Rejects if `UnresolvedDecisions` is non-empty. On success, commits in one transaction: `RulesetVersion` set to `TargetRulesetVersion`, `CharacterRevision` bumped, one forward (never compensating) `odyssey.persistence.character_ruleset_migrated` event carrying `SourceRulesetVersion`/`TargetRulesetVersion`/the resolved mappings — since `ValueChanges` is always empty in this task's own scope, no Mechanics/Ability/Resource column is ever touched by this method, an honest reflection of the deliberately narrow mapping this task implements, not a gap.
- **Reverting an already-committed migration:** `RevertCharacterRulesetMigration(campaign, characterId, migrationCommandId, reasonCode, actorUserId, actorIsMainGm, expectedCharacterRevision, commandId, correlationId)`. Looks up the original `odyssey.persistence.character_ruleset_migrated` `DomainEvents` row directly by its own `CommandId` column (`migrationCommandId` — the caller's own stable handle, exactly mirroring `RevertAdvancementPurchase`'s own `AdvancementPurchaseId` handle, without needing a new ledger table), reads its `SourceRulesetVersion` back out of the payload, and guards against a double-revert by checking whether a `odyssey.persistence.character_ruleset_migration_reverted` event already references that event's own `OriginalEventId`. Writes one compensating event (`compensationGroupId = commandId.ToString()`, `isCompensating: true`, `originalEventId` = the migration's own `EventSequence`) restoring `RulesetVersion` to the recorded `SourceRulesetVersion`, mirroring `RevertAdvancementPurchase`'s exact single-compensating-event shape (this task's own `ValueChanges` is always empty, so there is nothing else to restore — the batch mechanism this task actually exercises has exactly one compensating event plus, if a future task ever populates real `ValueChanges`, would extend to one event per changed value, following `ApplyCharacterRespec`'s own multi-entry-batch shape without any change to this method's own structure).
- Tests: Preview read-only/reproducible, Apply success (identity mapping) + stale-plan rejection (Character mutated since preview) + mid-apply-failure-leaves-no-partial-state (a duplicate-`CommandId` replay proves the same atomicity guarantee this codebase already relies on for every other command, per this task's own established convention — no bespoke fault-injection harness is introduced) + unresolved-decision blocking, revert success + double-revert rejection, duplicate-`CommandId` idempotency for `Apply`.

No Unity/UI code, no `ADR-012`/`013`/`022`–`026` content change, no value-transformation algorithm invented beyond identity/`UnresolvedDecisions`.

## 5. Decisions on the task's own open questions

- **`RulesetMigrationRules` shape:** a single static pure function (`BuildPlan`) taking already-loaded Character state plus a caller-supplied `RulesetDefinitionCatalog` fixture — mirrors `AttributeCostRules`'s own "test fixture only, no real catalog exists" convention exactly.
- **Full-campaign backup (§7.5, optional):** not attempted. `ADR-025` §7.5 explicitly frames this as optional and only relevant "before a large or bulk Ruleset migration" — this task's own scope is a single-Character migration proof, and adding the Backup API call here would be speculative scope not required by any of this task's own acceptance criteria.
- **Anatomy migration:** out of scope. Product §25's own `CharacterRulesetMigrationPlan` tree and every worked description of `ValueChanges`/`DefinitionMappings` in `ADR-025` §7.2/product's own surrounding text are Attribute/Skill/Ability/Resource-definition-shaped; `BodyPartId` belongs to a separate, `AnatomyProfileDefinitionId`-scoped catalog this task's own sources never mention in the migration context. Extending migration to Anatomy would be an invented scope expansion, not a decided one.
- **Revert handle:** the original migration's own `CommandId` (not a new minted `RulesetMigrationId`, not a new ledger table) — `DomainEvents.CommandId` already uniquely identifies the original event, exactly like `ODY-S04-112`'s own `FindCharacterIdByDraftBoundCommandId` precedent; adding a new table purely to name migrations would be schema growth this task's own scope does not require.
- **Permission gate for `ApplyCharacterRulesetMigration`/`RevertCharacterRulesetMigration`:** MainGM-only. Product §25's own process step 1 ("GM выбирает новую версию Ruleset") and §26's parallel with `GrantDevelopment`/`Respec` (both explicitly MainGM-only, both comparable blast-radius Mechanics-affecting operations) — `Character.MigrateRuleset` is listed in §26's permission catalog without an explicit MVP role note, but the process description itself names only the GM as the initiator.

## 6. Milestones

### M1 — Rules/Application contracts

- [ ] `RulesetMigrationRules.cs` (Rules, new).
- [ ] `CharacterRulesetMigrationPlan`/`RulesetDefinitionCatalog`/nested DTOs (Application).
- [ ] `ICharacterRepository.PreviewCharacterRulesetMigration`/`ApplyCharacterRulesetMigration`/`RevertCharacterRulesetMigration` added.
- [ ] New error codes/`PersistenceFailures` entries (unresolved decisions, stale plan, migration not found, already reverted).

### M2 — Persistence and tests

- [ ] `SqliteCharacterRepository` implementations.
- [ ] `HistoryEventTypes` extended with the two new event types.
- [ ] Tests written in `CharacterRulesetMigrationTests.cs`, all passing.
- [ ] `dotnet build`/`dotnet test` full suite green, no regression.

### M3 — Validation and review readiness

- [ ] `.\scripts\verify-format.ps1`.
- [ ] `docs/errors/ERROR_CODES.md` + `Tests/Metadata/test-catalog.json` entries.
- [ ] `.\scripts\check-repository-policy.ps1` final green run.
- [ ] `SLICE-04_IMPLEMENTATION_BACKLOG.md` status update.
- [ ] Diff-scope check.
- [ ] Commit, push, Draft PR.
- [ ] Record CI status.

## 7. Progress log

- 2026-09-03 -- Preflight confirmed PR #96's merge commit is a real ancestor of `origin/main`; created branch `feat/ody-s04-113-character-ruleset-migration`; preserved the product owner's own prep changes (task contract, backlog update) via stash across the branch switch.
- 2026-09-03 -- Read `ADR-025` §7, `ADR-013` §9, `ADR-024`, product §25 in full; re-read `ApplyCharacterRespec`/`RevertAdvancementPurchase` in full as the binding structural precedent.
- 2026-09-03 -- Confirmed via direct inspection of `Odyssey.Rules` that no cross-Ruleset-version mapping logic exists anywhere; resolved every open implementation question (section 5) directly from `ADR-025`/product's own text and this codebase's existing conventions.
- 2026-09-03 -- This ExecPlan authored before any code change, per `PLANS.md` §1.2.

## 8. Discoveries and deviations

- No open architectural question was found that `ADR-025`/`ADR-013`/`ADR-024` do not already answer; every open implementation question the task contract itself flagged (§18) was resolved directly from those ADRs'/product's own text (section 5 above).
- One small bug caught by the first full test run (not a design gap, a straightforward omission): both new event payloads (`character_ruleset_migrated`, `character_ruleset_migration_reverted`) initially omitted `displayNameSnapshot`, which `GetCharacterHistory`'s own rebuild requires on every event payload it reads (an `IntegrityCheckFailed` rejection otherwise). Fixed by adding the field to both payloads, matching every other event's own existing convention.

## 9. Validation and acceptance evidence

- `dotnet build Odyssey.Core.sln`: 0 warnings, 0 errors.
- `dotnet test Odyssey.Core.sln`: 241/241 `Odyssey.Tests.Persistence` (12 new), 472/472 total, zero regression across the full solution.
- `.\scripts\verify-format.ps1`: passed with `FORMAT-001 PASS repository text formatting checks passed`.
- `.\scripts\check-repository-policy.ps1`: passed, `Repository policy check passed.`

## 10. Recovery and rollback

Rollback is a normal revert of this branch/PR — no schema change (no new table/column; migration lookup reuses `DomainEvents`'s own existing columns). `PreviewCharacterRulesetMigration` is read-only; `ApplyCharacterRulesetMigration`/`RevertCharacterRulesetMigration` mutate only the live `Character` row's `RulesetVersion`/`CharacterRevision`; `DomainEvents` is append-only throughout.

## 11. Open questions and blockers

None — every open question the task contract itself flagged (§18) was resolved directly from `ADR-025`/`ADR-024`/product's own text (section 5 above).

## 12. Outcome and follow-up

Draft PR: [#97](https://github.com/odyssey-services/Odyssey_VTT/pull/97). CI pending. `ODY-S04-114` (Vertical Slice Integration) is the next task in the backlog; a future task deciding a real Ruleset-content-catalog/cross-version value-transformation algorithm would be the first real occasion to populate `RulesetMigrationRules`'s own `ValueChanges` field with anything beyond an empty list.
