# ODY-S11-230 — Switch the whole design system to a dark theme with light text

**Status:** In Review
**Roadmap stage / slice:** SLICE-11 (found during the product owner's first live visual pass of the UI)
**Owner:** Claude Code
**Requested by:** Product owner
**Branch:** `claude/pensive-gates-n18srp`
**Pull request:** Not opened
**ExecPlan:** Not required
**Created:** 2026-10-01
**Last updated:** 2026-10-01

## 1. Goal

Every screen (catalog, character, inventory, combat, board chrome) uses one unified dark panel/card
palette with light, readable text, replacing phase 0's light "D&D Beyond" palette. This is an explicit,
disclosed **reversal** of that earlier decision (`docs/ui/Odyssey_Design_System.md`'s phase 0 intent),
requested directly by the product owner after their own first live visual look at the UI, referencing
their own earlier `Odyssey_System` prototype (dark blue/near-black panels, light text, subtle borders,
saturated accent fills with white text).

## 2. Why this task exists

The product owner's screenshots showed a light theme throughout and asked for a full repalette to dark,
explicitly including the board screen's own chrome (topbar, drawers, Rolls & Game Log dock) — not just the
catalog/character/inventory/combat screens — and explicitly including a known, separately-tracked tab
readability problem (transparent tab backgrounds blending with whatever sits behind them) as part of the
same pass, verified as its own DoD item rather than assumed fixed by the palette alone.

The task's own stated invariant: this is a token recolor plus hunting down whatever bypasses the token
system, not a rewrite of component/class structure.

## 3. Authorities and requirement references

### Required authorities

- `AGENTS.md`
- `docs/ui/Odyssey_Design_System.md` (the document whose phase 0 light-theme decision this task reverses)

### Requirement and test IDs

- No new automated test IDs: no existing test asserted a specific hex/rgb color value (confirmed by
  repository-wide search, see §4), so there was nothing to update for color expectations, and a palette
  swap is not itself a behavior worth a new regression test — visual correctness here is confirmed by the
  live diagnostic in §17, not by an automated color assertion.

### Task-safe private context

- Approved summary: None

## 4. Verified current state

### Verified facts

- `OdysseyDesignSystem.uss`'s `:root` token block was the single source of all design-system colors;
  confirmed via a full read of the file and a repository-wide search that no test file asserts a specific
  color value (`grep` for hex/rgb literals and `resolvedStyle.backgroundColor`/`resolvedStyle.color` across
  `Assets/Odyssey/Client/Tests/` found nothing) — so there was no "test tied to the old palette" to update.
- `AppShell.uss` (an old dev-shell dark theme: `.app-root`/`.shell-*`, dark bg/light text/raised panel) is
  referenced nowhere in C# code or in any scene file (`grep` across `Runtime/*.cs` and `Scenes/*.unity`
  found zero matches) — confirmed dead/orphaned, not a live, conflicting second dark theme to consolidate.
  Used only as a color-inspiration reference per this task's own instruction to check for one.
- A repository-wide search for hardcoded hex/rgb colors in `OdysseyDesignSystem.uss` itself found exactly
  one real escape outside the `:root` block and the intentionally-dark board section: `.ody-board-layer`'s
  text color (`rgb(230, 235, 241)`, hardcoded instead of a token) — fixed (§5).
- A repository-wide search for hardcoded colors in the presenter `.cs` files (`BoardScreenPresenter.cs`,
  `BoardToolbarPresenter.cs`, `BoardFogOfWarPresenter.cs`, `BoardTokenInspectorPresenter.cs`,
  `AssetPoolPresenter.cs`) found many `new Color(...)` literals, but every one of them belongs to the
  board's own map-canvas rendering (tokens, obstacles, fog, HP bars, selection box, the board's own
  toolbar) — already dark before this task, explicitly out of scope per the Owlbear-exception carve-out
  this design system's own header comment states, and per this task's own scope (board screen **chrome**:
  topbar/drawers/dock, not the map canvas itself).
- A live PlayMode diagnostic (temporary, not committed) found a real, pre-existing CSS cascade-order bug,
  unrelated to color choice: `.ody-drawer`'s own `background-color` (and `.ody-modal`'s) was always
  overridden to fully transparent by `.ody-popover__paper--hidden`, because that rule happened to be
  declared later in the stylesheet than `.ody-drawer`/`.ody-modal` and USS resolves an equal-specificity
  tie by source order. This was true under the OLD light palette too (not something this repalette
  introduced) — it only surfaced now because actually looking at the live resolved background, rather than
  assuming the token cascade worked, was part of this task's own verification. Fixed (§5); see §18 for why
  this was fixed here rather than spun into a separate task.

### Assumptions

- None.

## 5. Scope

### In scope

- `OdysseyDesignSystem.uss` `:root` token block: full dark-palette repaint (neutrals, semantic status
  colors, overlay/scrim/shadow, resource-bar colors, focus ring).
- `.ody-board-layer`'s hardcoded text color -> `var(--ody-color-text)`.
- Moving `.ody-popover__paper` / `.ody-popover__paper--hidden` earlier in the file (before section 11,
  "Modal dialogs") so consumer classes that opt out of the generic paper (`.ody-drawer`, `.ody-modal`) keep
  winning the background/border cascade regardless of their own position in the file. No selector, no
  property value, and no component structure changed by this move — only where two already-existing rules
  sit relative to their consumers.

### Out of scope

- The board's own map-canvas rendering colors (tokens, obstacles, fog, HP bars, selection box, the board's
  own in-canvas toolbar) — already dark, not part of the design-system token cascade, not touched.
- Component/class structure, UXML, or presenter C# (other than the one hardcoded-color fix above, which is
  a one-line value change, not a structural edit).
- Backend — untouched.

### Allowed paths

```text
Assets/Odyssey/Client/UI/OdysseyDesignSystem.uss
```

## 6. Technical constraints

- Every derived class must keep reading from `--ody-*` tokens, never a literal value, so the palette
  swap is a single-file change.
- `--ody-color-accent` and the other "fill + white text" saturated colors (accent, bar-fill) are unchanged:
  they already pair a mid-brightness saturated fill with white text, which reads the same against either
  theme's panels.

## 7. Expected behavior

### Scenario 1 — unified dark panels

**Given** any screen (catalog, character, inventory, combat, board chrome)
**When** rendered
**Then** its panel/card backgrounds use the new dark `--ody-color-surface`/`--ody-color-bg` family, and
body text uses the new light `--ody-color-text`, with no leftover light-background "island."

### Scenario 2 — tabs readable

**Given** the top bar, character sheet sub-tabs, or catalog type filter
**When** rendered, active or inactive
**Then** the inactive tab's muted text is legible against the (now dark) parent panel, and the active
tab's accent-tinted background and accent text are both legible — confirmed live (§17), not assumed.

### Scenario 3 — board chrome matches

**Given** the board screen's topbar, drawers, dock (Rolls & Game Log), and floating toolbar
**When** rendered
**Then** they use the same dark `--ody-color-overlay-surface` as every other floating panel, not a
leftover light background — confirmed live (§17) after fixing the cascade-order bug in §4/§5.

### Required invariants

- The board's own map canvas (full-bleed, Owlbear-style) is unaffected: still `--ody-color-board` directly,
  not part of this token swap's palette change.
- No component/class structure change: every selector name and every non-color property is unchanged.

## 8. Deliverables

- Production code: `OdysseyDesignSystem.uss` token repaint, one hardcoded-color fix, one cascade-order fix.
- Tests: none added (see §3 for why).
- Documentation: this task file.

## 9. Acceptance criteria

1. Every `--ody-color-*` token in `:root` has a dark-theme value; no light-theme value remains.
2. No hardcoded hex/rgb color remains in `OdysseyDesignSystem.uss` outside the `:root` block and the
   board-canvas exception already documented in the file's own header comment.
3. Live PlayMode diagnostic confirms the topbar, a drawer (body, not just its header), an active tab, and
   the design tokens resolve to the new dark values at runtime, not just in the stylesheet source.
4. All pre-existing EditMode/PlayMode tests remain green, unmodified in their assertions (none were tied to
   a specific color).
5. `test-unity.ps1`, `test-fast.ps1`, `verify-*.ps1` all green.

## 10. Tests and validation

### Required automated tests

None added — see §3.

### Required commands

```powershell
scripts/test-unity.ps1
scripts/test-fast.ps1
scripts/verify-format.ps1
scripts/verify-repository.ps1
scripts/verify-test-structure.ps1
```

### Manual validation

Performed via a temporary, non-committed PlayMode diagnostic (`Temp_DarkPaletteDiagnostic`, removed before
this commit): opened the trial UI, read `resolvedStyle.backgroundColor`/`color` for the topbar, an opened
drawer's frame and header, and an active tab, and printed them to the log. See §17 for the exact values and
how the drawer-transparency bug was found and confirmed fixed with this same diagnostic.

**Known limitation, disclosed, not worked around:** this environment has no interactive GUI, and
`ScreenCapture.CaptureScreenshot` was already confirmed (earlier in this track) to produce no output file
in headless Unity batchmode — so no "after" screenshot or GIF could be produced for this task, matching the
same limitation already disclosed for `ODY-S11-226`. The live numeric diagnostic in §17 is offered as the
closest available substitute; the product owner should look at the live build themselves to confirm the
visual result matches their own expectation, per this task's own request for an "after" comparison.

## 11. Compatibility, migration, and rollback

Not applicable — pure stylesheet value/ordering change. Rollback = revert the commit.

## 12. Dependencies and licensing

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

## 13. Security, privacy, and hidden information

Not applicable.

## 14. Planning and execution mode

- Planning mode: Brief plan
- Expected pull request count: 1 (bundled with the rest of `claude/pensive-gates-n18srp`)

## 15. Documentation and versioning impact

- Documents that must change: `docs/ui/Odyssey_Design_System.md` should eventually be updated to describe
  the dark palette as current (not done here — out of this task's allowed paths; flagged as follow-up,
  §"Follow-up tasks" below).
- Application version change: No.

## 16. Definition of Done

- [x] Goal achieved.
- [x] All acceptance criteria satisfied.
- [x] Required automated tests pass (250/250 EditMode, 10/10 PlayMode), unmodified.
- [x] Required commands and their real results recorded (see §17).
- [x] Architecture rules remain valid (`TC-ARCH-001` PASS).
- [x] Self-review performed against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work. (Not opened; owner decides.)
- [ ] Product owner or authorized reviewer completes the required review (explicitly needs their own visual
      look at the live build, since no screenshot could be produced here — see §10's disclosed limitation).

## 17. Completion evidence

### Changed files / areas

- `OdysseyDesignSystem.uss`: full `:root` dark-palette repaint; `.ody-board-layer` text color now a token;
  `.ody-popover__paper`/`--hidden` moved earlier in the file to fix a pre-existing cascade-order bug that
  made `.ody-drawer`/`.ody-modal` bodies fully transparent.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `scripts/test-unity.ps1` | Passed | EditMode 250/250, PlayMode 10/10 (run twice: once before, once after the cascade-order fix, both green) |
| `scripts/test-fast.ps1` | Passed | `TC-ARCH-001 PASS`; `dotnet test` 952/952 |
| `scripts/verify-format.ps1` / `verify-repository.ps1` / `verify-test-structure.ps1` | Passed | all PASS |
| Live PlayMode diagnostic (temporary, removed) — before the cascade-order fix | — | `topbar bg=RGBA(0.094,0.110,0.133,0.960)` (matches new `--ody-color-overlay-surface`); `drawer bg=RGBA(0,0,0,0)` (bug: fully transparent); `activeTab bg=RGBA(0.773,0.192,0.192,0.180) color=RGBA(0.773,0.192,0.192,1.000)` (matches new accent-soft/accent) |
| Live PlayMode diagnostic — after the cascade-order fix | Passed | `drawer bg=RGBA(0.094,0.110,0.133,0.960)` — now matches `--ody-color-overlay-surface`, same as the topbar; `drawerHeader bg=RGBA(0.110,0.125,0.157,1.000)` matches the new `--ody-color-surface` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | full `:root` block rewritten, see the diff |
| AC-2 | Passed | repository-wide search in §4; one found escape fixed |
| AC-3 | Passed | live diagnostic log above, both runs |
| AC-4 | Passed | full suite green, unmodified assertions |
| AC-5 | Passed | all scripts green above |

### Known limitations

- No screenshot/GIF could be produced (headless batchmode, no interactive GUI, `ScreenCapture` already
  confirmed non-functional here) — the product owner's own look at the live build is the real DoD gate for
  "does this match the reference" visual decisions; this report's numeric diagnostic is a substitute, not a
  replacement, for that.
- `--ody-color-accent`, the bar-fill colors tied to it, and the whole board-canvas palette were kept exactly
  as before, per the task's own instruction to keep the current brand accent — a human should confirm this
  reads well enough against the new dark surfaces in the live build; it was not algorithmically contrast-
  checked beyond the semantic-color brightening already done for success/warning/danger/info/focus.

### Follow-up tasks

- `docs/ui/Odyssey_Design_System.md`'s own text still describes the phase 0 light theme as current; it
  should be updated to match, as a documentation-only follow-up (out of this task's allowed paths).
- The `ODY-S11-227` 44px topbar-tab min-width remains a disclosed trade-off tuned to the 640x480 test
  window (unrelated to this task, tracked in that task's own follow-up).

### Self-review summary

- Scope review: touched only `OdysseyDesignSystem.uss`; the one C# touch originally considered (a presenter
  hardcoded color) turned out to be out of scope (board canvas, not design-system chrome) and was left
  alone.
- Architecture review: `TC-ARCH-001` passes; no new dependency.
- Test review: no test tied to a specific color existed to update; full suite reconfirmed green after both
  the palette change and the separate cascade-order fix.
- Documentation/version review: this task file plus a flagged follow-up for the design-system reference doc.

## 18. Blockers, decisions, and change control

### Blockers

- None.

### Decisions made during execution

- 2026-10-01 — Rebuilt the entire `--ody-*` neutral/semantic/overlay token palette for a dark theme,
  explicitly reversing phase 0's original light "D&D Beyond" decision, per the product owner's direct
  instruction and their own `Odyssey_System` prototype reference.
- 2026-10-01 — Brightened the semantic status colors (success/warning/danger/info) and the focus ring,
  rather than keeping their phase-0 values: those were tuned for contrast as *text/border* against a white
  surface, and would have been too dark to read as text against the new dark surfaces. The brand accent and
  the three resource-bar *fill* colors (used as a filled area, not as text) were intentionally left as-is
  per the task's own instruction to keep the current brand accent, except `bar-fill-mid`/`bar-fill-high`,
  which were re-aliased to the brightened warning/success tokens so a filled bar stays visibly distinct from
  the new dark track color.
- 2026-10-01 — Found and fixed, as part of this task rather than a separate one, a pre-existing CSS
  cascade-order bug (`.ody-drawer`/`.ody-modal` backgrounds always losing to `.ody-popover__paper--hidden`
  regardless of theme). Decided to fix in-place rather than spin off a new task number because: (a) it
  directly defeats this task's own central deliverable — a repainted `--ody-color-overlay-surface` is
  meaningless if the element it is assigned to can never actually show it; (b) the fix is a two-rule
  reposition, not a structural rewrite, consistent with this task's own "token recolor + hunt down what
  bypasses it" framing; (c) it was discovered only because this task's own live-verification step looked at
  it, so folding it in keeps the discovery and the fix in the same evidence trail instead of forcing a
  reader to cross-reference two task files for one observation.

### Approved task changes

- None.
