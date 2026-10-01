# ODY-S11-213 — Derived initiative order (UI polish P0, item 4): not applicable now

**Status:** Closed — not applicable now (no client change)  
**Roadmap stage / slice:** SLICE-11 polish P0 (items 1–5: `ODY-S11-210`…`ODY-S11-214`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** Not required  
**Created:** 2026-10-01  
**Last updated:** 2026-10-01

## 1. Goal (as requested)

Show the initiative order as derived from a number: the client re-sorts participants by their numeric priority, with
a test. This applies only if the backend can add a participant or update a participant's numeric priority during
combat. Otherwise the limitation is documented, the item closes as "not applicable now", and the client is left
unchanged.

## 2. Verified backend facts (`origin/main`, unchanged in this series)

- **`Packages/com.odyssey.application/Runtime/Combat/CombatEncounterContracts.cs`**
  - The only requests are `CreateCombatEncounterRequest(ParticipantOrder, ActorUserId, CommandId)` and
    `AdvanceCombatEncounterRequest(EncounterId, ExpectedRevision, ActorUserId, CommandId)`.
  - `ICombatEncounterRepository` has exactly `Create`, `Advance` and `Get`.
  - `CombatEncounterService` exposes only `Create` and `Advance`, both MainGM-only.
- **`Packages/com.odyssey.domain/Runtime/Combat/CombatEncounter.cs`**
  - `CombatParticipant` holds only `CharacterId` and an integer `Order`, the position in the list given at creation.
  - There is **no numeric initiative or priority value** anywhere in the contract.
- **`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteCombatEncounterRepository.cs`**
  - `CombatEncounterParticipant` rows are inserted **only** inside `Create`.
  - The `Update` helper used by `Advance` changes only the encounter row (revision, round, turn, status, phase, current
    participant).
  - No path inserts, deletes or reorders participants afterwards.
- A repository-wide search for `Initiative`, `Priority`, `AddParticipant` or `ReorderParticipants` in production
  `Packages/**` finds nothing.

**Conclusion:** the backend can neither add a participant mid-combat nor update a numeric priority (there is none).
The order is fixed at creation. The client already shows it exactly as stored: `ODY-S11-205`, where the MainGM orders
the participants by hand before `CreateEncounter`.

## 3. Decision

- **Item closed as "not applicable now".** No client code changed, because a client-side sort by a number the backend
  does not store would be invented product behaviour.
- The `Odyssey_Initiative` repository named in the request was not reachable in this session. Nothing was taken from it.
- **Follow-up (backend, separate task, needs owner approval):**
  - a numeric initiative value per participant;
  - commands to add a participant mid-combat and to change its value, with the ordering rule and tie-break defined by
    the rules owner.
  - Only then can the client derive and re-sort the order. That client work is a small change in
    `CombatPanelPresenter` plus a test.

## 4. Validation

- No code changed, so there are no tests.
- `git diff origin/main -- Packages DotNet` is empty.
- `verify-docs.ps1` was not run (no PowerShell in the container).
