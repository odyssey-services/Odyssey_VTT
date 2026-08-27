# Odyssey VTT — SLICE-UI-01 Minimal UI Prerequisites Backlog

**Status:** Prerequisite backlog — **CLOSED** by this same revision (`ODY-UI-01-000`). No new ADR was required — see section 2.1. This backlog exists to record that finding explicitly, not to sequence ADR-authoring child tasks the way `SLICE-02_BACKLOG.md`/`SLICE-03_BACKLOG.md` did.
**Slice:** `SLICE-UI-01 — Minimal UI Prerequisites` (renamed from the original `SLICE-04` label on 2026-08-27; see section 0)
**Parent task:** `docs/tasks/active/ODY-UI-01-000_SLICE_UI_01_Minimal_UI_Prerequisites.md`
**ExecPlan:** Not required (Brief plan)
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 0. Naming history — renamed from `SLICE-04` to avoid roadmap collision

This precursor was originally created under the label `SLICE-04`. The private roadmap document (`17_Roadmap_Odyssey_VTT_v0.11.md`) already uses that exact label for a *different* vertical slice: line 216 of its milestone table names `SLICE-04` as "Rules Engine, персонажи и развитие" (Rules Engine, Characters and Progression), gated behind `GATE-C`, and roadmap section 13 ("Этап 5 — Characters and Progression," section 13.9: `SLICE-04 — Персонаж и развитие`) describes its own, unrelated ten-step scenario. Nothing in the roadmap names a UI-focused slice at all — there is no roadmap section describing client/interface architecture as its own vertical slice; this precursor's original `SLICE-04` label was always the product owner's own ad hoc assignment for this UI-prerequisites effort, not a roadmap-derived number.

**Resolution (2026-08-27):** to avoid confusion with the roadmap's own `SLICE-04`, the product owner decided to rename this entire branch of work from `SLICE-04`/`ODY-S04-XXX` to **`SLICE-UI-01`/`ODY-UI-01-XXX`** — a task-ID scheme that cannot collide with any roadmap-numbered slice (`ODY-S0X-YYY`), since no roadmap slice will ever carry the code `UI-01`. This is a pure rename, not a re-decision: every architectural/scope finding in section 2.1/section 3 below is unchanged from the original `SLICE-04` revision, only the label changed.

## 1. Purpose

This backlog converts the product owner's own explicit decision (`ODY-UI-01-000`'s ТЗ section 0) — build a minimal, throwaway-quality UI to exercise `SLICE-00`–`03`'s already-implemented mechanics by hand, not a production interface — into a small set of architectural confirmations, before any scene/script implementation work begins. It does **not** implement any UI code, screen, or scene. That implementation work begins only in a **future backlog revision** (`SLICE-UI-01_IMPLEMENTATION_BACKLOG.md`, analogous to `SLICE-03_IMPLEMENTATION_BACKLOG.md`), created only after this precursor closes.

This revision's only outcome is: (a) explicit confirmation that already-`Accepted` `ADR-001`/`ADR-002`/`ADR-008` fully answer the UI↔Application boundary questions without modification, and (b) a small set of scope decisions (minimal screen/action list, persistence choice, single-process role-switching convention) recorded here rather than left to be improvised ad hoc by whichever future implementation task picks up the first screen.

## 2. Slice exit criteria (this backlog revision only)

This prerequisite backlog revision is complete only when:

1. Every architectural question named in `ODY-UI-01-000`'s ТЗ section 3 has an explicit, justified answer — either "confirmed by an already-`Accepted` ADR, unmodified" or "a new ADR is required and has been drafted."
2. If any new ADR was required, it is `Accepted` before this backlog is considered closed.

**Result of this revision: criterion 1 is satisfied for all five questions; criterion 2 does not apply — no new ADR was required (section 2.1).**

## 2.1 Revision status and finding: no new ADR required

Unlike `SLICE-02_BACKLOG.md` (five new ADRs: `ADR-015`/`016`/`017`/`018`/`019`) and `SLICE-03_BACKLOG.md` (two new ADRs: `ADR-020`/`021`), this revision fixes **zero** new ADRs. This is not an undercount by omission — section 3 below justifies, question by question against `ODY-UI-01-000`'s own ТЗ section 3, why each is already fully answered by already-`Accepted` `ADR-001`/`ADR-002`/`ADR-008`, plus one Client-layer-only decision that does not rise to ADR-level (touches no Application/Domain/Persistence contract) and one product-scope decision (neither touches architecture).

**This is a genuinely cleaner outcome than either prior slice's own prerequisite revision, stated explicitly because it is the more unusual result, not the default one to assume.** The reason it holds here and did not for `SLICE-02`/`SLICE-03`: `SLICE-02` introduced an entirely new concern (networking) with no prior ADR coverage at all, and `SLICE-03` introduced new domain math (board geometry) and a new audience-selection dimension neither `ADR-002` nor `ADR-019` covered. `SLICE-UI-01` introduces no new domain concept, no new Application port, and no new persisted format — it only adds a *view* onto contracts `ADR-001`/`002`/`008` already fully specify how a Unity client may consume. `ADR-001` §6.7 in particular was written during `SLICE-00` specifically to govern `Odyssey.Unity.Client`, and already names UI Toolkit, thin Application-calling integration code, and a service-locator prohibition as settled decisions — this revision does not amend a single word of it.

## 3. Architectural questions and their resolution (`ODY-UI-01-000` ТЗ section 3)

### 3.1 UI technology — confirmed by `ADR-001` §6.7, not reopened

`ADR-001` §6.7 ("Odyssey.Unity.Client") already lists "UI Toolkit views" among what `Odyssey.Unity.Client` is permitted to own, alongside "presenters/view models" and "thin integration code для вызова Application." This is not a hypothetical permission — `Assets/Odyssey/Client/UI/AppShell.uxml`/`AppShell.uss`/`OdysseyPanelSettings.asset` and `Assets/Odyssey/Client/Runtime/DeveloperShellPresenter.cs` (the existing "Developer Shell" diagnostics screen, `SLICE-00`-era) already build a working UI Toolkit view programmatically over a `UIDocument`. **Decision: UI Toolkit, confirmed, not reopened.** No UGUI equivalent exists anywhere in the repository to migrate away from.

### 3.2 UI↔Application boundary — confirmed by `ADR-001` §6.7, not reopened

`ADR-001` §6.7 explicitly permits "thin integration code для вызова Application" and explicitly forbids "service locator как основной способ composition." `DeveloperShellPresenter` (the only existing precedent) is a plain C# class (not a `MonoBehaviour`) taking its dependencies (a `UIDocument`, an `IDeveloperShellFacade`) directly through its constructor, and calling straight into Application-layer `Result<T>`-returning methods from button-click handlers — no adapter layer, no command-dispatch queue, no DI container. `Odyssey.Unity.Client.Runtime.asmdef` already references `Odyssey.Application`/`Odyssey.Persistence`/`Odyssey.Networking`/`Odyssey.Domain`/`Odyssey.Rules` directly. **Decision: presenters call Application services (`BoardMovementService.MoveToken`, `DiceRollService.SubmitRoll`, `SqliteGameLogRepository.SaveDiceRollEntry`, etc.) directly, constructing a fresh `CommandId`/`CorrelationId` per user-initiated attempt exactly as every existing test helper already does (`ADR-002` §4.1/4.4 — `CommandId` is caller-supplied per logical attempt, no dispatch-layer changes needed) — confirmed, not reopened. No new adapter/service-locator layer is introduced for one minimal trial screen.**

### 3.3 Single local process, host-authoritative role-switching — confirmed by `ADR-008` §11/§13, plus one Client-layer-only decision

`ADR-008` §11 fixes "production randomness вызывается только на host-authoritative path" and §13 fixes each authoritative random decision's stream derivation from "host-secret campaign key" — "host" here names an **authoritative role/process**, not a network-topology claim about how many physically distinct machines are involved. A single local process that is, in fact, the only process running is trivially "the host" under this definition; nothing in `ADR-008` (or `ADR-002`'s command model, or `ADR-019`'s permissions baseline, none reopened here) requires more than one physical process to exist for host-authoritative semantics to hold. **Decision: the trial UI runs entirely in one local process — no real network (`ODY-S02-014` remains a separate, still-deferred product-owner decision, per `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.3's identical framing carried forward unchanged), no multiple real processes emulating multiple players.** A UI-level "Playing as: [Player ▾ / MainGM ▾]" role selector lets one local human supply the `actorUserId`/`actorIsMainGm`/`ActorCanCreateRoll` values each Application call already requires as a plain caller-supplied parameter (exactly the same simplification `ODY-S03-004`'s `MoveTokenRequest.ActorIsMainGm` and `ODY-S03-005`'s `SubmitRollRequest.ActorCanCreateRoll` already documented as deliberate, since no real session/role infrastructure exists to resolve them from a real identity yet) — this selector is a pure `Odyssey.Unity.Client`-layer presentation concern (which caller-supplied values a button click passes), touching no Application/Domain contract, and does not rise to ADR level. Recorded here as an explicit backlog decision rather than an ADR, per `ADR-001` §6.7's own framing of presenters/view models as ordinary Client-layer content.

### 3.4 Minimal screen/action list — derived from `ODY-S03-008`'s own ten steps, not invented

Per `ODY-UI-01-000`'s ТЗ section 2, the source of requirements is `ODY-S03-008`'s already-proven ten-step scenario (roadmap §12.6), not independent UI-design judgment. The minimal set needed to walk that same scenario by hand:

- **One scene/board view**: renders the single active scene's tokens at their real `TokenPosition` coordinates (simple shapes/labels, `GridType=None` per `ADR-020`'s own only-implemented case — no hex/grid rendering). Clicking a token then clicking a destination calls `BoardMovementService.MoveToken` (step 1).
- **A role selector** (§3.3 above): "Playing as: Player / MainGM / Observer" — Observer included specifically to exercise the excluded-participant safe-denial case `ODY-S03-006`/`008` already prove in tests.
- **A roll panel**: a formula text field plus a "Roll" button, calling `DiceRollService.SubmitRoll` (steps 2–4).
- **A minimal modifier control**: a "Propose Modifier" affordance (label + value) and, when playing as MainGM, "Accept/Change/Reject" buttons calling `ProposeModifier`/`DecideModifier` (step 5).
- **An override control**, visible only when playing as MainGM: a reason field and an "Override" button calling `ApplyOverride`, which fails visibly without a reason (step 6).
- **A result display** that calls `DiceRollVisibilityPolicy.TryGetVisibleRoll` for the currently-selected role before showing anything — proving audience-aware delivery by hand, including the case where switching to Observer shows nothing (step 7).
- **A "Save & Reopen Campaign" action** that persists via `SqliteGameLogRepository.SaveDiceRollEntry` and then re-lists via a fresh repository instance (steps 8–9), the same "new instance against the same `campaign.db`" pattern `ODY-S03-007`/`008`'s own tests already use as "reconnect."
- **A simple game-log list**: an unstyled scrollable list of persisted `GameLogEntryRecord.SummaryPayload` values, filtered through the current role via `GameLogReconnectService.GetVisibleEntries` (steps 5/9).
- **Reroll/Cancel buttons** calling `RequestFullReroll`/`CancelRoll`, with the log still showing the original entry unchanged afterward (step 10).

**Explicitly excluded** (per `ODY-UI-01-000`'s ТЗ section 4 and because none of the above steps need them): drawing/annotation tools, a ruler, drag-and-drop polish or animation of any kind, sound, multiple scenes or scene management UI, pan/zoom polish, hex-grid rendering, localization, mobile/web platform targets. A future, separate, later-prioritized Board-implementation effort (already named as deferred by `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.1) owns all of these, not this trial UI.

### 3.5 Persistence — real SQLite, not a parallel in-memory store

**Decision: the trial UI calls the real, already-built `SqliteCampaignRepository`/`SqliteSceneRepository`/`SqliteGameLogRepository` (not a new in-memory substitute).** Justification: (a) building a parallel in-memory store would be *more* implementation work than calling the already-tested real repositories, not less — there is no complexity-reduction argument for avoiding them; (b) the product owner's own stated goal is to verify already-built mechanics by hand, and persistence/reconnect (`ODY-S03-007` roadmap steps 8–9) is one of the ten mechanics explicitly worth touching, not a mechanic to route around; (c) `ADR-001` §6.7 forbids "прямое чтение/запись SQLite из UI" — the trial UI satisfies this by calling the same `Odyssey.Application.Persistence`-owned repository interfaces every existing test already calls, never touching `Microsoft.Data.Sqlite` directly itself. `DiceRollStore` (the in-memory pre-persistence roll-in-progress store `ODY-S03-005` already established) is still used for a roll's short-lived propose/decide/override lifecycle before it is persisted, exactly as `ODY-S03-008`'s own integration test already does — this is not a second competing decision, it is the same existing pattern, unmodified.

## 4. No technical spike required

None of the five questions above requires empirical measurement against something uncontrollable or previously unproven (the bar `SLICE-01`'s `SP-02`, `SLICE-02`'s `SP-03`/`SP-04` met). Each is either a direct re-application of an already-`Accepted` ADR's own explicit text, or a scope/product decision resolvable by reading `ODY-S03-008`'s own already-proven scenario. If a genuine empirical unknown surfaces once actual scene/script implementation begins (for example, a real Unity Editor/Player performance or IL2CPP-compatibility surprise), the implementation task that finds it may commission a spike at that point — this revision does not foreclose that, it only records that no such need is visible now.

## 5. Ordered backlog

| Order | Task ID | Title | Status | Depends on | Planning mode | Primary result |
|---:|---|---|---|---|---|---|
| 1 | `ODY-UI-01-000` | SLICE-UI-01 Minimal UI Prerequisites | Done | None | Brief plan | This backlog document and its own task contract. Confirms `ADR-001`/`002`/`008` fully answer the UI↔Application boundary (no new ADR); records the minimal screen/action list, the single-process role-switching convention, and the real-SQLite persistence decision. |

## 6. Dependency rules

- `ODY-UI-01-000` depends only on `SLICE-03`'s own closure (`ODY-S03-009`/`010`, both `Done` on `main`) — the public contracts this precursor cites (`BoardContracts`, `DiceContracts`, `GameLogRepositoryContracts`, `DiceRollVisibilityPolicy`, `GameLogReconnectService`) must already exist and be stable.
- No task in this backlog depends on `ODY-S02-014` (Real Transport Integration) or the `ADR-016` §14 follow-up spike — per product-owner decision, unchanged from `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.3's identical framing, carried forward here without modification.

## 7. Global non-goals

This backlog revision excludes:

- Any UI implementation code, Unity scene content, or screen — deferred entirely to a future `SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` revision, created only after this precursor closes (analogous to `ODY-S03-003`'s role for `SLICE-03`).
- Reopening any already-`Accepted` ADR (`ADR-001`, `ADR-002`, `ADR-004`, `ADR-008`, `ADR-012`, `ADR-017`, `ADR-019`, `ADR-020`, `ADR-021`) — each is cited as authority, never redecided.
- Real network integration (`ODY-S02-014`/`ADR-016` §14) — a separate, still-deferred product-owner decision.
- Final visual design, animation, audio, localization, or any non-desktop/non-Editor platform target.
- Any new game mechanic beyond what `SLICE-00`–`03` already implemented.

## 8. Backlog change control

- New work requires a new `ODY-UI-01-XXX` task contract.
- This document does not replace task acceptance criteria; it does not itself constitute implementation.
- The `SLICE-UI-01` implementation backlog (actual UI screens/scenes) is a separate future backlog revision, created only after the product owner accepts this precursor's closure — analogous to how `ODY-S03-003` created `SLICE-03_IMPLEMENTATION_BACKLOG.md` only after `SLICE-03_BACKLOG.md` closed.
