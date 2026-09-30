# ODY-S11-205 — Combat UI (phase 5)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phase 5 of 0–5)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** `ODY-S11-200` §14  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

From the Combat drawer the MainGM creates an encounter with a hand-made order and advances turns; the acting
participant attacks (preview → confirm), activates abilities, uses items on self and rolls checks; the MainGM decides
pending attacks, resolves stacking conflicts and corrects combat-log entries.

## 3. Authorities and requirement references

- ADR-030/031 (attack pipeline, checks), ADR-028 (effects/stacking), ADR-002, ADR-004, ADR-008 (authoritative RNG).
- `CombatEncounterService`, `AttackEvaluationService`, `AttackApplyService`, `ActivateAbilityService`,
  `UseItemService`, `CheckService`, `GameLogReconnectService`.
- New test IDs: `TC-COMBATUI-001`…`TC-COMBATUI-007`.

## 4. Verified backend facts reflected (no invented mechanics)

- **Initiative** is not computed: `CreateCombatEncounterRequest(ParticipantOrder)` — the MainGM orders by hand (▲/▼).
- **Action economy** is not checked by the engine (see decision §6).
- **No hit locations** (`BodyPart` proposal is never produced) and **no "% to hit"**: `AttackRangeResult.IsInRange`
  decides the hit; the preview shows distance, in-range + reason, outcome, modifiers, armor, damage/cost deltas and
  effect candidates only.
- The attack weapon must be an **equipped** weapon instance of the actor (`SqliteAttackStateReader`); only equipped
  weapons are offered, with a hint when a weapon exists but is not equipped.
- **UseItem** has no target: always the acting character.
- **Compensate** = "Correct log entry": adds an `AttackCompensated` entry with a mandatory reason next to the original
  `AttackResolved` entry; damage/resources are never rolled back. The button and dialog say so.
- MainGM-only: create/advance encounter, attack intervention, log correction, stack-conflict resolution.
- An ability is activatable only once linked to a published catalog Ability (`LinkAbilityActivationSource`); the
  character sheet's Abilities tab now offers that link to the MainGM.

## 5. Scope

### In scope
- `Runtime/Combat/CombatPorts.cs`, `Runtime/Combat/CombatPanelPresenter*.cs` (3 partial files); combat composition in
  `TrialScreenPresenter` (same wiring as the backend MVP scenario: active-effect, encounter, attack reader/apply,
  core rules evaluator, ability/use-item readers and repositories, check reader/repository, the trial's dice store,
  game log, user groups, RNG factory and epoch).
- `CharacterPanelPresenter`: optional catalog parameter + "Link to catalog Ability" (MainGM).
- Tests `CombatPanelPresenterTests.cs`.

### Out of scope
- Encounter listing/closing, reactions/triggers UI, hit locations, action economy enforcement. Backend untouched.

## 6. Decisions

- **Action economy (required decision).** Client-side, **non-authoritative hint**: the panel sums the action cost
  (weapon `ActionCost`, ability `ActionCost`, 1 for an item) each participant used in the current round/turn in
  this session and shows "Actions used this turn: N of a suggested B (hint only, not enforced by the server)".
  It never blocks a command. Enforcement would be a separate backend task.
- **Session-scoped lists (backend gaps).** No listing exists for encounters, pending attack interventions or stack
  conflicts. The panel keeps the encounter it created (plus "open by id"), the attacks it resolved (refreshed with
  `GetOutcome`), and derives conflict *candidates* from those attacks' effect candidates and the targets' active
  effects of the same definition; resolving a pair that is not a pending conflict fails readably.
- **Combat log** = `ListGameLog` filtered by `GameLogReconnectService.GetVisibleEntries` (the backend's audience rules),
  showing `AttackResolved` and `AttackCompensated` entries; corrections are separate rows, originals stay.
- **Targets**: participant toggles; ability self-targeting follows the definition's `AllowSelf`, and the ability card
  shows the target rule through the shared `TargetRuleEditor.Describe` (phase 2 widget).
- **Revisions**: the attack intent pins a freshly read encounter revision; ability/item intents pass the actor's
  current abilities/resources revisions and the item/inventory revisions of freshly loaded state.
- **Acting for a character**: actions are offered to the MainGM or the acting character's owner/controller; others
  see who acts now.

## 9. Acceptance criteria

1. Create encounter → turn → attack with preview → apply → (if needed) MainGM intervention, through the UI. ✔
2. Abilities / items / checks available in combat. ✔
3. Log visible; stacking conflicts resolvable by the MainGM (session-derived candidates). ✔
4. Tests; regression of earlier phases. ✔ (pending owner run)

## 10. Tests and validation

| ID | Test |
|---|---|
| `TC-COMBATUI-001` | `Encounter_ManualInitiativeOrder_CreateAndAdvance_AreMainGmOnly` |
| `TC-COMBATUI-002` | `Attack_PreviewWritesNothing_ResolveAppliesDamage_JournalAndActionHintUpdate` |
| `TC-COMBATUI-003` | `Attack_OutOfRange_ShowsNoHitAndNoDamage` |
| `TC-COMBATUI-004` | `CorrectLogEntry_AddsACorrectionNextToTheOriginal_AndNeverUndoesDamage` |
| `TC-COMBATUI-005` | `Ability_LinkedInTheSheet_ThenActivated_AndItemUsedOnSelf` |
| `TC-COMBATUI-006` | `Check_ShowsPassOrFail` |
| `TC-COMBATUI-007` | `Intervention_And_StackConflicts_AreMainGmOnly_AndOnlyForRealPendingItems` |

Tests use a fixed random stream (the backend MVP scenario's own technique) so damage is exact.

**Not run in this session** (no Unity/pwsh/.NET in the container; owner validates locally). A real `Pending` attack
needs content whose effect candidates require intervention; it is covered by the backend's own tests, and the UI path
(pending list → Approve/Reject/Cancel with confirmation) is exercised only up to the "not pending" refusal here.

Manual validation (owner, Play mode): Catalog → publish a Weapon; Character → create two characters, approve, add
`health`, place tokens; Inventory → create inventories, create and equip the weapon; Combat → order, start, preview,
confirm, Next turn, correct the log entry.

## 13. Security, privacy, and hidden information

The combat log uses the backend audience filter; check rolls take an explicit audience; no internal ids or error
texts beyond `OdyMessages`.

## 17. Completion evidence

`git diff --name-status main -- Packages DotNet` empty. Validation not run in container.

## 18. Blockers, decisions, and change control

- Backend gaps (reported, not fixed): no encounter / pending-attack / stack-conflict listing; no action economy.
