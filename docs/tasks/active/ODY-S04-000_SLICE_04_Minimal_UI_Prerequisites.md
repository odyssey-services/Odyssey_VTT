# ODY-S04-000 — SLICE-04 Minimal UI Prerequisites

**Status:** In Review
**Roadmap stage / slice:** SLICE-04 (product-owner-assigned label; see task contract §4 and `SLICE-04_BACKLOG.md` §0 for a naming caveat against the roadmap's own, unrelated `SLICE-04`)
**Owner:** Codex (agent)
**Requested by:** Product owner
**Branch:** `feat/ody-s04-000-slice-04-minimal-ui-prerequisites`
**Pull request:** Draft — [#65](https://github.com/odyssey-services/Odyssey_VTT/pull/65) (open, CI green, awaiting owner review)
**ExecPlan:** Not required — see §14 (Brief plan)
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## 1. Goal

Before any minimal-trial-UI implementation code is written, explicitly resolve the architectural questions separating `Odyssey.Unity.Client` from the already-implemented `SLICE-00`–`03` Application/Persistence layer: UI technology, the UI↔Application call boundary, single-process host-authoritative role-switching for audience testing, the minimal screen/action set needed to walk `ODY-S03-008`'s own ten-step scenario by hand, and the persistence choice. For each, either confirm an already-`Accepted` ADR answers it unmodified, or author a new ADR. Record the result in `docs/tasks/SLICE-04_BACKLOG.md`. No production code is written by this task.

## 2. Why this task exists

- Problem or dependency being addressed: `SLICE-00`–`03` implemented all game logic (board/tokens, dice, visibility, persistence, network prototype) with zero UI — everything is proven only by tests. The product owner has decided to build a minimal, throwaway-quality UI to exercise these mechanics by hand (walking `ODY-S03-008`'s own ten-step scenario, but by eye and click rather than test code), and — following the `SLICE-02`/`SLICE-03` precedent — wants the UI↔Application architectural boundary questions settled before scene/script implementation begins, not improvised ad hoc by whichever screen gets built first.
- Value or risk reduction: `Odyssey.Unity.Client` already exists (`SLICE-00`-era bootstrap/diagnostics infrastructure, `Assets/Odyssey/Client/`) with an already-`Accepted` module boundary (`ADR-001` §6.7). Confirming, explicitly, that this boundary already answers every question a game-mechanic UI would raise — rather than silently assuming so, or silently reopening it — avoids both a wasted new ADR for already-settled ground and an undocumented architectural drift if a future screen quietly invents its own composition pattern.
- Blocking or enabling relationship: Blocks all `SLICE-04` UI implementation work. Enables a future `SLICE-04_IMPLEMENTATION_BACKLOG.md` (analogous to `ODY-S03-003`'s role for `SLICE-03`) once this precursor's findings are accepted.

## 3. Authorities and requirement references

### Required authorities

- `17_Roadmap_Odyssey_VTT_v0.11.md` — searched for a UI/client-architecture section; none exists (see §4 "Verified facts" and `SLICE-04_BACKLOG.md` §0 for the naming-collision finding this produced, reported rather than papered over).
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.7 ("Odyssey.Unity.Client") — read in full; the primary authority answering questions 3.1/3.2 below.
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §4.1/4.4/4.6 (`Command`, `Command receipt`, `Root command` — `CommandId` as a caller-supplied, per-attempt idempotency key) — confirms question 3.2's "no new command-dispatch layer" conclusion.
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` §11/§13 (host-authoritative RNG path; per-decision stream derivation from a host-secret campaign key) — the authority for question 3.3.
- `docs/tasks/active/ODY-S03-004_Board_Foundation_Scene_Token_Selection_Authoritative_Movement.md`, `ODY-S03-005_Dice_Roll_Engine_Host_Authority_Modifiers_Reroll_Cancel.md`, `ODY-S03-006_Audience_Aware_Roll_And_Log_Delivery.md`, `ODY-S03-007_Game_Log_And_Board_State_Persistence_Reconnect_Replay.md` — the public contracts (`BoardContracts`/`BoardMovementService`, `DiceContracts`/`DiceRollService`/`DiceRollVisibilityPolicy`, `GameLogRepositoryContracts`/`SqliteGameLogRepository`/`GameLogReconnectService`) this precursor confirms the UI will call directly.
- `docs/tasks/active/ODY-S03-008_Vertical_Slice_Integration.md` — the ten-step scenario this precursor's minimal screen/action list (question 3.4) is derived from, not invented independently.
- `Assets/Odyssey/Client/Runtime/RuntimeComposition.cs`/`DeveloperShellPresenter.cs`, `Assets/Odyssey/Client/Runtime/Odyssey.Unity.Client.Runtime.asmdef`, `Assets/Odyssey/Client/UI/AppShell.uxml`/`AppShell.uss` — the already-existing `SLICE-00`-era Unity client skeleton, read in full to confirm what composition/UI pattern is already established, not to be inferred from documentation alone.
- `docs/tasks/SLICE-03_BACKLOG.md` — structural precedent for how a prerequisite backlog is organized, including its own "no technical spike required" and "no new ADR" framing precedents.

### Requirement and test IDs

- Requirement IDs: `SLICE-04` (prerequisites revision only, product-owner label; see naming caveat above).
- Existing test IDs: None cited as new evidence; this task performs no test run of its own beyond confirming `main`'s existing green state.
- New test IDs to introduce: None.

### Task-safe private context

- Approved summary / references: None.

## 4. Verified current state

### Verified facts

- `ODY-S03-009`/`010` (SLICE-03 closure, formal owner acceptance) are on `main` — confirmed via `git fetch origin main && git merge --ff-only` and `git log --oneline -10` before this task's branch was created.
- The roadmap (`17_Roadmap_Odyssey_VTT_v0.11.md`) has **no section describing UI/client architecture as its own vertical slice**. Its own internal `SLICE-04` label (line 216, milestone table; §13.9) names a *different*, unrelated slice: "Rules Engine, персонажи и развитие" (Characters and Progression), gated behind `GATE-C` — confirmed by `Grep` across the full document. This is a genuine naming collision between the product owner's ad hoc label for this UI-prerequisites effort and the roadmap's own pre-existing numbering, recorded explicitly in `SLICE-04_BACKLOG.md` §0 rather than silently assumed away or silently renamed.
- A real Unity project already exists in this repository (`Assets/`, `ProjectSettings/`, two scenes: `Assets/Odyssey/Client/Scenes/AppShell.unity` and `Bootstrap.unity`) — not merely `Packages/com.odyssey.*` without a project shell, confirmed by `Glob`/`find`. `Assets/Odyssey/Client/` already contains a working `SLICE-00`-era bootstrap/diagnostics skeleton: `RuntimeComposition.cs` (`OdysseyRuntimeCompositionRoot`/`AppRuntime`/`PresentationRuntime`), `DeveloperShellPresenter.cs` (a plain C# presenter over a UI Toolkit `UIDocument`), and `Assets/Odyssey/Client/UI/AppShell.uxml`/`AppShell.uss`/`OdysseyPanelSettings.asset` (UI Toolkit assets, not UGUI). No board/dice/game-log game UI exists yet — only infrastructure (diagnostics, build identity, crash markers) and a "Developer Shell" probe screen.
- `Odyssey.Unity.Client.Runtime.asmdef` (confirmed by `Read`) already references `Odyssey.Domain`, `Odyssey.Rules`, `Odyssey.Content`, `Odyssey.Application`, `Odyssey.Persistence`, and `Odyssey.Networking` directly — the Unity client assembly already has a compile-time path to call every Application/Persistence service this precursor discusses, with no additional project-reference change needed.
- `ADR-001` §6.7 (confirmed by `Read`) already explicitly permits, for `Odyssey.Unity.Client`: "bootstrap и composition root," "UI Toolkit views," "presenters/view models," "thin integration code для вызова Application" — and explicitly forbids "authoritative campaign state в MonoBehaviour," "прямое чтение/запись SQLite из UI," and "service locator как основной способ composition." `DeveloperShellPresenter.cs` is a live, working instance of exactly this pattern: a plain C# class, constructor-injected with its dependencies, calling directly into Application-layer `Result<T>`-returning methods from UI Toolkit button-click handlers, with no adapter layer and no DI container.
- `ADR-008` §11/§13 (confirmed by `Read`) defines "host" as the authoritative role/process drawing production RNG, not a claim about the number of physical machines involved — nothing in it, or in `ADR-002`'s command model, requires more than one process for host-authoritative semantics to hold.
- `ODY-S03-004`/`005`'s own task contracts (confirmed by `Read`) already document `MoveTokenRequest.ActorIsMainGm`/`SubmitRollRequest.ActorCanCreateRoll` as caller-supplied booleans, a deliberate simplification "mirroring... this task has no session/role model of its own" — meaning a UI-level role selector supplying these same caller-side values is not a new simplification this precursor invents, it is the same one two already-merged tasks already established and documented.

### Assumptions

- None. Every fact above was directly observed via `Read`/`Grep`/`Glob`/`git log` during this task, not recalled from memory or assumed by analogy to `SLICE-02`/`SLICE-03`'s own precursors.

## 5. Scope

### In scope

- Reading the roadmap for a UI/client-architecture section (none found; recorded as a finding, not invented).
- Reading `ADR-001`/`002`/`008` and the existing `Assets/Odyssey/Client/` skeleton to confirm (or, if necessary, extend) the UI↔Application boundary.
- Recording five explicit architectural/scope decisions (UI technology; UI↔Application call boundary; single-process role-switching; minimal screen/action list derived from `ODY-S03-008`; persistence choice) in `docs/tasks/SLICE-04_BACKLOG.md`.
- This task contract itself.

### Out of scope

- Any production code, UI scene, script, prefab, UXML/USS content, or Unity asset. Confirmed: this task's diff touches only documentation files.
- Any new ADR content — none was found to be required (§4/§18); if this finding is later disputed, a new ADR is a separate future task's content, not retrofitted here.
- The `SLICE-04` UI implementation itself (any actual screen) — a separate, future `SLICE-04_IMPLEMENTATION_BACKLOG.md` revision, created only after this precursor's findings are accepted.
- Real network integration (`ODY-S02-014`/`ADR-016` §14) — a separate, still-deferred product-owner decision, unchanged from `SLICE-03_IMPLEMENTATION_BACKLOG.md` §2.3's identical framing.
- Reopening any already-`Accepted` ADR's own decisions.
- Reconciling the `SLICE-04` naming collision with the roadmap's own `SLICE-04` (Characters and Progression) — reported, not resolved; a product-owner decision.

### Allowed paths

```text
docs/tasks/active/ODY-S04-000_SLICE_04_Minimal_UI_Prerequisites.md
docs/tasks/SLICE-04_BACKLOG.md
```

### Paths requiring explicit approval before editing

```text
docs/adr/** (no ADR content is created by this task)
Assets/** (read-only source for this task; no UI implementation)
Packages/** (read-only source for this task)
docs/plans/** (Brief plan mode; no ExecPlan is created)
Any production code, test code, script, Unity, or package file
```

## 6. Technical constraints

- Module ownership and dependency direction: Not applicable to this scaffold itself — this task decides no new module boundary; it confirms `ADR-001` §6.7's existing one already covers a game-mechanic UI.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Not applicable.
- Time / RNG rule: Not applicable to this task directly; it records (does not decide) that `ADR-008`'s host-authoritative model already covers a single-process trial UI.
- Unity / thread / lifetime rule: Not applicable — no Unity code is written.
- Dependency / licensing rule: No new dependency, GitHub Action, Unity package, executable, or downloadable tool is introduced.
- Security / privacy / redaction rule: Not applicable.
- Performance or platform constraint: Not applicable.
- Other: None.

## 7. Expected behavior

### Scenario 1 — every architectural question has an explicit, justified answer

**Given** the merged state of `ODY-S03-004`–`010` and the already-`Accepted` `ADR-001`/`002`/`008`
**When** each of the five questions in `SLICE-04_BACKLOG.md` §3 is checked
**Then** each cites a specific ADR section, a specific existing production file, or a specific `ODY-S03-008` scenario step as its justification — never an unstated assumption — and the backlog records whether a new ADR was required (none was).

### Required invariants

- No new ADR is authored unless a genuine gap is found; none was found here, and this is stated explicitly rather than left ambiguous.
- No production code, UI, or Unity asset is introduced.
- The roadmap-UI-section search finding (none exists) is recorded, not silently omitted.
- The `SLICE-04`/roadmap-`SLICE-04` naming collision is recorded, not silently renamed or ignored.

## 8. Deliverables

- Production code: None.
- Tests: None.
- Scripts / CI: None.
- Configuration: None.
- Documentation: This task contract; `docs/tasks/SLICE-04_BACKLOG.md`.
- Generated evidence or build artifacts: None.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. `docs/tasks/active/ODY-S04-000_SLICE_04_Minimal_UI_Prerequisites.md` exists, following `docs/tasks/TASK_TEMPLATE.md` with all 18 numbered sections present.
2. `docs/tasks/SLICE-04_BACKLOG.md` exists, mirroring `SLICE-03_BACKLOG.md`'s structure, and explicitly answers all five ТЗ §3 questions with a cited authority for each.
3. The roadmap-UI-section search result (none found) and the `SLICE-04` naming collision with the roadmap's own `SLICE-04` are both recorded explicitly, not smoothed over.
4. No new ADR file exists as a result of this task — the backlog explicitly justifies why none was required, question by question.
5. No production code, UI scene, script, or Unity asset exists as a result of this task.
6. `.\scripts\verify-format.ps1` and `.\scripts\check-repository-policy.ps1` both pass unchanged.
7. `git diff --name-status` against `main` shows only the two files listed in §5's Allowed paths.
8. A Draft pull request exists with all required CI checks green; the PR is not moved to Ready without separate owner confirmation.

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

None. This is a documentation-only architectural-confirmation task; no new test ID is introduced.

### Required commands

```powershell
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

(Also run for extra confirmation, per this task's own instruction that it is optional but welcome: `dotnet build`/`dotnet test`, full solution — confirming this documentation-only diff does not accidentally disturb anything.)

### Manual validation

- Owner review of the five architectural decisions before any `SLICE-04` implementation task is activated.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 (PowerShell validation only).
- Unity editor or Player profile: Not applicable — no Unity code is touched; the existing client skeleton is read-only source for this task.
- Scripting backend: Not applicable.
- Network topology or database fixture: None.
- Other: None.

### Validation not required by this task

- `dotnet build`/`dotnet test` (optional, not required — no code is touched), Unity compile/EditMode/PlayMode, or any other script — none is affected because no production code, test code, script, Unity asset, package, or CI workflow file is touched.

## 11. Compatibility, migration, and rollback

Not applicable. This task introduces no persisted state, public contract, protocol, package, Unity version, or build identity change.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

- Data classes handled: None new.
- Trust boundaries: Not applicable.
- Authorization / audience checks: Not applicable — this task confirms an existing boundary, introduces none.
- Redaction requirements: Not applicable.
- Log-safe fields: Not applicable.
- Abuse / malformed input limits: Not applicable.
- Security tests: None.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: Checked against `PLANS.md` §1's conditions individually, following the same discipline `ODY-S02-000`/`ODY-S03-000` used for their own precursor scaffolds. (1) Contained in one area — two documentation files, no production module touched. (2) Does not change a public contract, persisted format, protocol, permissions model, dependency graph, package version, or build pipeline — every question this task answers is a *confirmation* of an already-`Accepted` decision or a Client-layer-only/product-scope choice, never a new Application/Domain/Persistence contract. (3) One clear path — read the roadmap/ADRs/existing client skeleton, answer each of the five questions, write the two files. (4) Fits one focused pull request. (5) No migration or recovery procedure required. `PLANS.md` §1.2's ExecPlan triggers do not apply: no port/DTO/event/command/schema/protocol/manifest/package/build-profile/migration is introduced or changed by this task — it is even lighter than `ODY-S03-000` (which needed `ExecPlan` reasoning is not claimed here since `ODY-S03-000` itself used Brief plan too; this task matches that precedent exactly), since unlike `SLICE-02`/`SLICE-03`'s own precursors, this one concludes with zero new ADR content to hand off.
- Brief plan:
  1. Files inspected: `17_Roadmap_Odyssey_VTT_v0.11.md` (searched for a UI section; none found); `ADR-001` §6.7, `ADR-002` §4, `ADR-008` §11/§13; `ODY-S03-004`–`008`'s task contracts and public contracts; `ODY-S03-008`'s own ten-step scenario; `Assets/Odyssey/Client/Runtime/RuntimeComposition.cs`/`DeveloperShellPresenter.cs`/`Odyssey.Unity.Client.Runtime.asmdef`; `Assets/Odyssey/Client/UI/*`; `SLICE-03_BACKLOG.md` (structural precedent).
  2. Intended change: `SLICE-04_BACKLOG.md` (five architectural questions answered, zero new ADRs, explicit naming-collision finding), this task contract.
  3. Validation: `verify-format.ps1`/`check-repository-policy.ps1`; optional full `dotnet build`/`dotnet test` re-run.
  4. Non-goals: no UI code, no new ADR, no `ODY-S02-014`/`ADR-016` §14 work, no renaming of the roadmap's own `SLICE-04`.
- ExecPlan path: Not required.
- Expected pull request count: 1.
- Milestone or sequencing constraints: Do not begin any `SLICE-04` implementation task until this precursor's findings are reviewed and accepted by the product owner.

## 15. Documentation and versioning impact

- Documents that must change: This task contract; `docs/tasks/SLICE-04_BACKLOG.md`.
- Documents that must not change: All ADRs, `ODY-S03-004`–`010` task contracts, the roadmap, `Assets/**`, `Packages/**`.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None.
- Changelog or release-note requirement: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied.
- [x] Required automated tests pass (none required; optional full-suite rehearsal recorded in §17).
- [x] Required manual checks are completed (owner review pending — see Pull request note).
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

- `docs/tasks/SLICE-04_BACKLOG.md` — new.
- `docs/tasks/active/ODY-S04-000_SLICE_04_Minimal_UI_Prerequisites.md` — this task contract.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `.\scripts\verify-format.ps1` | Passed | `FORMAT-001 PASS repository text formatting checks passed`. |
| `.\scripts\check-repository-policy.ps1` | Passed | All `REPO-POLICY-*`/`TC-CI-*` checks passed; `Repository policy check passed.` |
| CI — Draft PR [#65](https://github.com/odyssey-services/Odyssey_VTT/pull/65), commit `6676e0b` | Passed | Run [33028987921](https://github.com/odyssey-services/Odyssey_VTT/actions/runs/33028987921): `repository-policy-format-structure`, `dotnet-restore-build-test`, `unity-project-package-static`, `buildidentity-provenance` — all 4 `SUCCESS`. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | This file, all 18 sections present. |
| AC-2 | Passed | `SLICE-04_BACKLOG.md` §3, all five questions answered with cited authorities. |
| AC-3 | Passed | `SLICE-04_BACKLOG.md` §0 (naming collision) and this contract's §4 (roadmap-search finding). |
| AC-4 | Passed | `SLICE-04_BACKLOG.md` §2.1 — zero new ADRs, justified per question. |
| AC-5 | Passed | `git status --porcelain` before commit shows only documentation files. |
| AC-6 | Passed | See Validation results above. |
| AC-7 | Passed | `git diff --name-status` matches §5's Allowed paths exactly. |
| AC-8 | Passed | Draft PR [#65](https://github.com/odyssey-services/Odyssey_VTT/pull/65) open; all 4 required CI checks `SUCCESS` on run 33028987921 (commit `6676e0b`); PR remains Draft pending explicit owner confirmation before any merge. |

### Build and artifact evidence

- Build identity: Not applicable.
- Artifact path / name: None.
- Checksums: None.
- Test or quality report: Not applicable.

### Known limitations

- The `SLICE-04`/roadmap-`SLICE-04` naming collision (§4) is reported, not resolved — a product-owner decision for later.
- This precursor does not itself implement any UI; a future `SLICE-04_IMPLEMENTATION_BACKLOG.md` does that, once these findings are accepted.

### Follow-up tasks

- A future `SLICE-04_IMPLEMENTATION_BACKLOG.md` revision (analogous to `ODY-S03-003`'s role for `SLICE-03`), created only after the product owner accepts this precursor's findings.

### Self-review summary

- Scope review: Zero production/UI code touched; two documentation files only.
- Architecture review: No ADR reopened; `ADR-001`/`002`/`008` cited, not altered; zero new ADRs authored, with per-question justification for why none was needed.
- Test review: No new TestCase IDs introduced.
- Security/privacy review: Not applicable — no new data classes or trust boundaries.
- Documentation/version review: Only the two new/updated files required changes.

## 18. Blockers, decisions, and change control

### Blockers

- None for this task's own closure. A future `SLICE-04` implementation task should not begin until the product owner reviews and accepts this precursor's five decisions.

### Decisions made during execution

- 2026-08-27 — Finding: the roadmap has no dedicated UI/client-architecture section, and its own internal `SLICE-04` label already names a different, unrelated slice (Characters and Progression, `GATE-C`) — Authority: full-document `Grep` of `17_Roadmap_Odyssey_VTT_v0.11.md`; reported explicitly per this task's own ТЗ instruction not to invent a section that doesn't exist.
- 2026-08-27 — Decision: UI technology is UI Toolkit, confirmed by `ADR-001` §6.7 and the already-existing `Assets/Odyssey/Client/UI/AppShell.uxml`/`.uss` — not reopened, no new ADR.
- 2026-08-27 — Decision: UI↔Application boundary is direct calls from plain-C#-class presenters into Application-layer public services (no adapter layer, no DI container, no service locator), confirmed by `ADR-001` §6.7's explicit permission/prohibition list and the already-working `DeveloperShellPresenter.cs` precedent — not reopened, no new ADR.
- 2026-08-27 — Decision: the trial UI runs single-process, host-authoritative by construction (one process is trivially "the host"), with a UI-level "Playing as" role selector supplying the same caller-side `actorUserId`/`actorIsMainGm`/`ActorCanCreateRoll` values `ODY-S03-004`/`005` already document as deliberate simplifications — confirmed by `ADR-008` §11/§13's role-not-topology definition of "host"; this specific selector is a Client-layer-only decision recorded in the backlog, not an ADR (touches no Application/Domain contract).
- 2026-08-27 — Decision: the minimal screen/action list is derived directly from `ODY-S03-008`'s already-proven ten steps, not independent UI-design judgment — see `SLICE-04_BACKLOG.md` §3.4 for the full list and explicit exclusions.
- 2026-08-27 — Decision: the trial UI uses real SQLite persistence (`SqliteCampaignRepository`/`SqliteSceneRepository`/`SqliteGameLogRepository`), not a parallel in-memory store — Authority: no complexity-reduction argument favors a substitute store; persistence/reconnect is itself one of the mechanics worth exercising by hand; `ADR-001` §6.7's "no direct SQLite from UI" rule is satisfied by calling the same repository interfaces every existing test already calls.
- 2026-08-27 — Decision: zero new ADRs are required for this precursor — a genuinely different (cleaner) outcome than `SLICE-02`'s five or `SLICE-03`'s two, stated explicitly as the more unusual result, not assumed as the default — Authority: `ADR-001` §6.7 was written during `SLICE-00` specifically to govern `Odyssey.Unity.Client` and already answers every UI-boundary question this precursor raises; `ADR-008` already defines "host" as a role, not a topology; no new domain concept, Application port, or persisted format is introduced by a trial UI consuming already-built contracts.

### Approved task changes

- None.
