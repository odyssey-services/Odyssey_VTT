# ODY-S11-209 — Attach `OdysseyDesignSystem.uss` to the real UIDocument

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (phase 0 follow-up, project-wide design system)
**Owner:** Claude Code
**Requested by:** Product owner (found during `ODY-S11-208` diagnostics on `claude/pensive-gates-n18srp`)
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Attach `OdysseyDesignSystem.uss` to the running app's `UIDocument` so the `.ody-*` classes already used
throughout every SLICE-11 screen actually take visual effect at runtime, by explicit reference (not
`Resources.Load`).

## 2. Why this task exists

- Problem: found while writing up `ODY-S11-208`. A project-wide search confirmed `OdysseyDesignSystem.uss` is
  never attached to any `UIDocument`/`PanelSettings` on this branch — no `styleSheets.Add` call and no UXML
  `<Style>` reference exists anywhere in `Assets/Odyssey/Client/Runtime`. Every `.ody-*` class name added via
  `AddToClassList` throughout `GameShellPresenter`, `BoardToolbarPresenter`, `OdyUi`, etc. has had zero visual
  effect on this branch.
- Value: Phase 0's Definition of Done ("the design system exists and is applied") was not actually met, even
  though the stylesheet itself was fully written. Without this, none of the topbar/drawer/dock/board-overlay
  visual design from phases 0–5 has ever actually rendered as intended.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`, ADR-001 §6.7 (UI Toolkit views), ADR-005 (composition/lifetimes)
- Precedent: `ODY-S11-101` wired `CatalogTheme.uss` onto `AppShellEntryPoint` via an explicit
  `[SerializeField] StyleSheet` field, threaded through composition, `element.styleSheets.Add(...)` — done on
  a different branch (`claude/meta-files-regeneration-kvlmtt`) for a catalog-only screen that does not exist
  in that form on this branch.

### Requirement and test IDs

- Requirement IDs: None
- Existing test IDs: full EditMode/PlayMode suite (regression check only)
- New test IDs to introduce: None

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- No `CatalogTheme.uss` file, and no `--catalog-*` CSS custom property, exists anywhere on this branch
  (`claude/pensive-gates-n18srp`) — confirmed by a repo-wide search. `ODY-S11-101`'s catalog-theme work lives
  on a separate branch and was never merged here. On this branch, `OdysseyDesignSystem.uss` is the single,
  unified stylesheet from Phase 0, and the catalog screen (`ContentCatalogPresenter`, this branch's
  `ODY-S11-202`) already uses the same unified `OdyClasses`/`.ody-*` classes directly — there is no second,
  divergent token set to reconcile.
- `AppShellEntryPoint` (the sole `MonoBehaviour` composition root for every screen, dev-shell and trial alike)
  had no `StyleSheet` field and never touched `UIDocument.rootVisualElement.styleSheets`.
- `OdysseyPanelSettings.asset`'s `themeUss` field points to Unity's built-in default runtime theme, not to
  `OdysseyDesignSystem.uss`; `PanelSettings` has no other stylesheet-list field in this Unity version.
- `VisualElement.Clear()` (called by every presenter when it rebuilds its subtree, e.g.
  `GameShellPresenter.Build()`, `BoardScreenPresenter.BuildView()`) removes child elements only; it does not
  touch the element's own `styleSheets` list. Attaching the stylesheet once, to the outermost
  `_document.rootVisualElement`, at `AppShellEntryPoint.Initialize()` (called once per accepted host) survives
  every subsequent screen swap (dev shell → trial screen).

### Assumptions

- None.

## 5. Scope

### In scope

- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs`: add the serialized `StyleSheet` field and the
  `styleSheets.Add` call.
- `Assets/Odyssey/Client/Scenes/AppShell.unity`: assign the field to `OdysseyDesignSystem.uss` (Unity-generated
  scene edit via a temporary Editor script, per this track's established convention — never hand-written YAML).

### Out of scope

- Rewriting `OdysseyDesignSystem.uss` itself. No syntax errors were found in it during this task, so no
  content changes were needed (see §18 for what was checked).
- A full manual visual QA pass of every screen — see §10/§17 for why this remains blocked in this environment,
  same limitation already recorded in every prior SLICE-11 verification task here.
- Backend (`Packages/com.odyssey.*`, `DotNet/**`) — untouched.

### Allowed paths

```text
Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs
Assets/Odyssey/Client/Scenes/AppShell.unity
```

### Paths requiring explicit approval before editing

```text
None
```

## 6. Technical constraints

- Unity / thread / lifetime rule: the stylesheet is attached exactly once, guarded by
  `styleSheets.Contains(...)`, at the same point `_document` is first resolved in `Initialize`.
- Dependency / licensing rule: Not applicable.
- Other: no string/path-based asset lookup (`Resources.Load`, `AssetDatabase.LoadAssetAtPath` at runtime,
  etc.) — explicit serialized reference only, so `TC-ARCH-001`'s ban on that composition pattern is not
  triggered. Note: the checker matches on literal source text, not on comments-vs-code, so this task's first
  pass tripped `TC-ARCH-001` by merely *mentioning* the forbidden method name inside an explanatory code
  comment — fixed by rewording the comment (see §18).

## 7. Expected behavior

### Scenario 1 — any screen after this fix

**Given** the app has bootstrapped through `AppShellEntryPoint.Initialize`
**When** any screen (dev shell or trial/Owlbear) builds its `VisualElement` tree using `.ody-*` classes
**Then** the design system's rules for those classes are in effect (colors, spacing, absolute-positioned
overlays, etc. render as the stylesheet defines them), because the stylesheet is attached to the document's
root element before any screen is built.

### Required invariants

- The stylesheet is attached at most once per document (no duplicate entries after repeated
  dev-shell ⇄ trial-screen transitions).

## 8. Deliverables

- Production code: `AppShellEntryPoint._designSystemStyleSheet` field + one-time `styleSheets.Add` call.
- Tests: None new (existing suite is the regression check).
- Scripts / CI: None.
- Documentation: this task file.
- Generated evidence or build artifacts: None retained.

## 9. Acceptance criteria

1. `OdysseyDesignSystem.uss` is referenced from `AppShellEntryPoint` via an explicit `[SerializeField]`, and
   `AppShell.unity`'s `AppShellEntryPoint` instance has that field populated.
2. The full EditMode suite (201 tests) and PlayMode suite (7 tests, including `TC-BOARD-121`) remain green
   after attaching it, with the DOM-lookup-based tests unaffected (styling alone does not change element
   names/structure).
3. `TC-ARCH-001` and the rest of `verify-*.ps1`/`test-fast.ps1` remain green.
4. Whether a full human visual pass was performed is stated explicitly, not implied (see §17) — this
   environment has batchmode-only Unity access with no interactive GUI/display, the same limitation recorded
   in every prior SLICE-11 verification task on this track; a `ScreenCapture.CaptureScreenshot` attempt from a
   temporary PlayMode test was tried as a substitute and did not produce a usable image in this batchmode
   environment (see §17), so this remains genuinely unverified visually pending a human with a GUI.

## 10. Tests and validation

### Required automated tests

None new. Existing suite is the regression check (see §17 for the actual runs).

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

- Attempted: a temporary `[UnityTest]` loaded the trial screen and called
  `ScreenCapture.CaptureScreenshot(...)` after allowing a couple of frames to settle. The test itself passed
  (no exception), but no image file was produced anywhere in the project or temp directories — consistent
  with `ScreenCapture.CaptureScreenshot` requiring a real backbuffer/display surface that Unity's `-batchmode`
  (no `-nographics` window, no interactive session) does not provide. The test was removed before committing
  (temporary diagnostic only, per this track's convention of never leaving throwaway scaffolding in committed
  test files).
- Not performed: an actual human, interactive Play-mode visual walkthrough of the Owlbear layout with the
  stylesheet attached. Still blocked by this environment's lack of an interactive Unity GUI/display — recorded
  honestly rather than assumed passing.

### Required environments / profiles

- OS / architecture: Windows, Unity 6000.4.0f1 (batchmode), .NET 10.

### Validation not required by this task

- A pixel-level or perceptual screenshot-diff test suite does not exist in this repository and was not built
  for this task (out of scope — building test infrastructure was not requested).

## 11. Compatibility, migration, and rollback

Not applicable — presentation-only, no persisted state, contract, or protocol affected. Rollback = revert the
commit (removes the field reference and the scene edit).

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Reason for selected mode: Single, well-precedented composition change (explicit `StyleSheet` reference,
  same pattern as `ODY-S11-101`), no new architecture.
- Expected pull request count: 1 (bundled with the rest of `claude/pensive-gates-n18srp`, per owner's own
  branch/PR decision).

## 15. Documentation and versioning impact

- Documents that must change: None beyond this task file.
- Application version change: No.
- Schema / format / contract / protocol / ruleset version change: None.

## 16. Definition of Done

- [x] Goal is achieved without unapproved scope expansion.
- [x] All acceptance criteria are satisfied (AC-4 satisfied by stating the limitation explicitly, not by
      performing an impossible-in-this-environment visual pass).
- [x] Required automated tests pass.
- [x] Required manual checks are completed to the extent possible in this environment; the remainder is
      explicitly recorded as not done, not silently skipped.
- [x] Required commands and their real results are recorded (see §17).
- [x] Architecture and dependency rules remain valid (`TC-ARCH-001` PASS, after fixing a false-positive
      triggered by a code comment — see §18).
- [x] Security, privacy, redaction, and audience rules are verified where applicable (not applicable).
- [x] Compatibility, migration, rollback, and versioning obligations are complete where applicable (not applicable).
- [x] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [x] Documentation is updated only where materially required.
- [x] Self-review performed against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. (Not opened; owner decides.)
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

### Changed files / areas

- `Assets/Odyssey/Client/Runtime/AppShellEntryPoint.cs` — added `_designSystemStyleSheet` field; attaches it
  to `_document.rootVisualElement.styleSheets` once, in `Initialize`, guarded against double-attachment.
- `Assets/Odyssey/Client/Scenes/AppShell.unity` — `AppShellEntryPoint`'s `_designSystemStyleSheet` field now
  references `Assets/Odyssey/Client/UI/OdysseyDesignSystem.uss` (assigned via a temporary Editor script using
  `SerializedObject`, deleted before commit — the only legitimate way to edit a Unity-serialized scene field
  without hand-writing YAML).

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `scripts/test-unity.ps1` EditMode | Passed | `root total=201 passed=201 failed=0` |
| `scripts/test-unity.ps1` PlayMode | Passed | `root total=7 passed=7 failed=0` (`TC-BOARD-121` included) |
| `scripts/test-fast.ps1` | Passed (after one fix) | First run: `TC-ARCH-001 FAIL` — "Forbidden composition pattern 'Resources.Load'" flagged against a source comment that merely *named* that method while explaining what the fix does NOT use. Reworded the comment (no code change) and reran: `TC-ARCH-001 PASS`; `dotnet test`: Contracts 1/1, Domain 90/90, Networking 67/67, Unit 220/220, Architecture 10/10, Persistence 948/948 |
| `scripts/verify-format.ps1` | Passed | `FORMAT-001 PASS` |
| `scripts/verify-repository.ps1` | Passed | `REPOSITORY-VERIFY PASS` |
| `scripts/verify-test-structure.ps1` | Passed | exit 0, `TC-ARCH-001`/`TC-ARCH-002` all PASS |
| Screenshot capture attempt (temporary test, not committed) | Inconclusive/no artifact | Test passed without exception; `ScreenCapture.CaptureScreenshot` produced no file anywhere on disk — batchmode has no real display surface. Test removed. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | scene diff shows `_designSystemStyleSheet` set to `OdysseyDesignSystem.uss`'s guid |
| AC-2 | Passed | EditMode/PlayMode results above |
| AC-3 | Passed | test-fast.ps1 + verify-*.ps1 results above |
| AC-4 | Passed (by disclosure) | this section states plainly that no human visual pass was performed |

### Build and artifact evidence

Not applicable (no build/package artifact produced by this task).

### Known limitations

- **The visual appearance of every SLICE-11 screen with the design system actually applied has never been
  seen by a human.** This task makes the stylesheet load; it does not itself verify the result looks right.
  Given the CSS was written across 6 phases without ever being rendered, some visual issues beyond the
  z-order bug (`ODY-S11-208`) may still surface once someone with an interactive Unity Editor opens the Trial
  screen — the design system's className→layout mapping has effectively never been exercised end-to-end
  until now.
- No syntax or rule errors were found in `OdysseyDesignSystem.uss` while reading it for this task, so nothing
  in the stylesheet itself was changed.

### Follow-up tasks

- A full human, interactive visual QA pass of every SLICE-11 screen (Owlbear board, drawers, dock, catalog,
  character sheet, inventory, combat) is needed now that the stylesheet actually applies — not filed as a
  numbered task; flagged here for the product owner to schedule once they (or a reviewer with GUI access) can
  run the Editor interactively.

### Self-review summary

- Scope review: touched exactly the two files needed to attach the stylesheet; did not rewrite the stylesheet
  itself or expand into a visual redesign.
- Architecture review: `TC-ARCH-001` passes cleanly after the comment fix; no new cross-module dependency.
- Test review: full existing suite green; no test rewritten to accommodate this change.
- Documentation/version review: this task file is the only new documentation; no version fields changed.

## 18. Blockers, decisions, and change control

### Blockers

- None remaining. A full human visual QA pass is a follow-up, not a blocker for this task's own scope
  (attaching the stylesheet), per its own Definition of Done wording ("явно зафиксировано... или он
  по-прежнему ждёт человека с GUI").

### Decisions made during execution

- 2026-10-01 — Confirmed there is no `CatalogTheme.uss`/`--catalog-*` token set on this branch to reconcile
  with `OdysseyDesignSystem.uss` — that work exists only on a separate branch not merged here. Attached
  `OdysseyDesignSystem.uss` alone, unconditionally, at the document root. — Authority / approval: verified
  directly by repository search before implementing, per the task's own instruction to check this first.
- 2026-10-01 — `TC-ARCH-001`'s first failure was a false positive from a source comment naming
  "Resources.Load" while explaining the fix does not use it; reworded the comment rather than treating this
  as a real architecture violation, since the actual code only uses an explicit `[SerializeField]` reference.
  — Authority / approval: direct reading of the `TC-ARCH-001` checker's own error output and the actual source
  line it flagged.
- 2026-10-01 — Did not build a screenshot-diff test suite when the ad hoc `ScreenCapture` attempt failed to
  produce an artifact; the task's own text allowed either building such infrastructure or explicitly stating
  the limitation, and building one would be a disproportionately large addition for this ticket's scope. —
  Authority / approval: ODY-S11-209's own instruction ("решить самостоятельно... или явно сообщить").

### Approved task changes

- None.
