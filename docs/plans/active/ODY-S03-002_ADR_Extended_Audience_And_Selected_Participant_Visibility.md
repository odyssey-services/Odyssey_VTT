# ExecPlan — ODY-S03-002: ADR: Extended Audience and Selected-Participant Visibility

**Governing task contract:** `docs/tasks/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md`
**Status:** Complete (deliverable produced; PR pending CI/review)
**Created:** 2026-08-26
**Last updated:** 2026-08-26 UTC

## Authorities

- `07_Permissions_Odyssey_VTT_v0.7.md` §16 (full `CampaignUserGroup` aggregate), §30 (Private events and audiences — six game audiences).
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (Видимость — four audience kinds for rolls), §27 (full-text search security invariant), §28 (postfactum audience change), §36.5 (revocation networking contract).
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §16.3 (fog `AudienceKey`) — second consumer of this ADR beyond roll/log.
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §7 (pipeline, extended not reopened), §10 (explicitly deferred scope this ADR closes).
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §5 (`Operations[]`, reused not extended).
- `docs/tasks/active/ODY-S03-000_SLICE_03_Playable_Foundation_Prerequisites.md` §4 and `docs/tasks/SLICE-03_BACKLOG.md` §5 (fixed task boundary — executed, not reopened).
- `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` — structural/stylistic template from the same task wave.

## Investigation performed

1. Read `07_Permissions_Odyssey_VTT_v0.7.md` §16 in full — confirmed `CampaignUserGroup` is a fully documented aggregate (`CampaignUserGroupId`/`CampaignId`/`Name`/`Description?`/`MemberUserIds`/`Status`/`CreatedByUserId`/`CreatedAt`/`UpdatedAt`/`Revision`) with a full lifecycle (create/rename/membership-change/archive), each step already tied to revision bumps and `ClientProjection` recompute.
2. Read §30 (Private events and audiences) — confirmed six game audiences (`Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants`/`CampaignUserGroup`/`SceneParticipants`), broader than the roll-specific four in `09_Dice_And_Game_Log` §16.1.
3. Read `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (roll visibility, including §16.4's explicit evaluation-time rule), §27 (full-text search security invariant — already phrased as a direct application of `PERM-INV-012`), §28 (disclosure/revocation postfactum, `LogEntryDisclosureChanged`), §36.5 (revocation networking contract).
4. Cross-checked `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §16.3's fog `AudienceKey` vocabulary (`User:<id>`/`Group:<id>`/`CharacterOwners:<characterId>`/`CharacterControllers:<characterId>`/`SceneParticipants`) as a second, independently-named consumer of the same structural concept — confirmed the ADR should fix one integration *principle*, not force naming unification across consumers that have each already fixed their own vocabulary in their own product document.
5. Re-read `ADR-019` §7 (pipeline) and §10 (deferred scope) to confirm the exact deferred boundary this ADR closes, and `ADR-017` §5's `Operations[]` list to confirm `AddJournalEntry`/`AddEntity`/`RemoveFromProjection` already cover both disclosure and revocation without a new operation type.
6. Determined the `CampaignUserGroup` scope question (full aggregate vs. narrow representation) by analogy to how `ADR-019` §3.1 already scoped `RolePreset` — reused only the read-model fields needed for audience resolution (`CampaignUserGroupId`/`CampaignId`/`MemberUserIds`/`Status`/`Revision`), leaving lifecycle commands as ordinary `ADR-002` commands not requiring their own architectural decision.
7. Confirmed via `SLICE-03_BACKLOG.md` §3 that no spike is needed — this is an additional input dimension on an already `SP-04`-proven mechanism, not a new architectural risk class.

## Intended change

- New file: `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md`, `Status: Accepted`, mirroring `ADR-020`/`ADR-019`'s structural format.
- New file: `docs/tasks/active/ODY-S03-002_ADR_Extended_Audience_And_Selected_Participant_Visibility.md` (task contract, all 18 `TASK_TEMPLATE.md` sections).
- This file (ExecPlan).
- `docs/tasks/SLICE-03_BACKLOG.md` — `ODY-S03-001` row (`In Review` → `Done`, both change-control conditions now satisfied since the product owner merged PR #55) and `ODY-S03-002` row (own status/Planning-mode/result columns).

## Tests or validation commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

No `dotnet build`/`dotnet test` required — no production/test code is touched by this task.

## Explicit non-goals

- No production code, no unit tests — future implementation task's job (`ADR-021` §12, Definition of Done).
- No reopening of `ADR-019`'s three baseline roles (MainGM/Player/Observer) — only its explicitly-deferred §10 scope is extended.
- No full `CampaignUserGroup` lifecycle-command design (create/rename/archive) — ordinary `ADR-002` commands, not this ADR's content.
- No full permission-aware full-text search design (`09_Dice_And_Game_Log` §27.3's searchable fields, indexing/engine choice) — only confirmation that the safe-denial principle extends.
- No technical spike — `SLICE-03_BACKLOG.md` §3's justification already covers this ADR's content; re-confirmed in `ADR-021` §13.6.
