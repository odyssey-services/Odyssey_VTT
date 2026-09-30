# ODY-S11-200 — Project-wide client design system (phase 0 of the full client UI)

**Status:** In Review  
**Roadmap stage / slice:** SLICE-11 (full client UI, phases 0–5: `ODY-S11-200`…`ODY-S11-205`)  
**Owner:** Claude Code  
**Requested by:** Product owner  
**Branch:** `claude/pensive-gates-n18srp`  
**Pull request:** Not opened  
**ExecPlan:** this contract, section 14 (series plan)  
**Created:** 2026-09-30  
**Last updated:** 2026-09-30

## 1. Goal

One light, D&D Beyond–style design system (tokens + reusable classes + small C# helpers) that every later client
screen uses, attached through an explicit asset reference, with the palette, type scale and class catalogue
documented so phases 1–5 take it ready-made.

## 2. Why this task exists

- The client had only the dark developer theme (`AppShell.uss`) and inline colors in presenters.
- Phases 1–5 (board overlay layout, catalog, character sheet, inventory, combat) need shared overlay panels,
  resource bars, tabs, modal confirmations and status indicators.

## 3. Authorities and requirement references

- `AGENTS.md` §5, §9, §12, §14; ADR-001 (module boundaries, no runtime lookup), ADR-004 (safe errors),
  ADR-005 (explicit composition).
- Product-owner decision in the task request (2026-09-30): D&D Beyond reference, Owlbear exception for the board.
- New test IDs: `TC-UIKIT-001`…`TC-UIKIT-007`.

## 4. Verified current state

- `ODY-S11-101` (`CatalogTheme.uss`, `ContentCatalogScreenPresenter`, `ItemFormModel`, `TC-CATALOG-126…141`)
  does **not** exist in the repository: not on `main` (`0837cfc`), not on any `origin` branch, not in any PR up
  to #205. Raised with the product owner, who decided (2026-09-30): build the UI anew. The palette quoted from
  that task is the base of the new tokens.
- No custom fonts or icons exist in `Assets/`.
- The execution container has no Unity, PowerShell or .NET SDK (see §10).

## 5. Scope

### In scope
- `Assets/Odyssey/Client/UI/OdysseyDesignSystem.uss` (new), `AppShell.uxml` (one `<Style>` line added).
- `Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs` (new), `Assets/Odyssey/Client/Tests/EditMode/OdysseyUiKitTests.cs` (new).
- `docs/ui/Odyssey_Design_System.md` (new), this contract, test catalog entries.

### Out of scope
- Any backend (`Packages/**`, `DotNet/**`) — untouched in all six phases.
- `AppShell.uss` (unchanged; retires from the board only in `ODY-S11-201`).
- Custom font / icon set (decided: system font, text glyphs, see design doc §3).

## 6. Technical constraints

- Stylesheet attached by `<Style src="OdysseyDesignSystem.uss" />` in `AppShell.uxml`; no `Resources.Load`,
  `ServiceLocator`, `GetService`, `FindObjectOfType`, `GameObject.Find`.
- USS limits respected: no `calc()`, no `gap`, no `box-shadow` (shadow approximated with a translucent bottom border).
- `.meta` files for the new assets are **not** hand-written; Unity generates them on first import.

## 7. Expected behavior

- Any element under the document root can use `--ody-*` tokens and `.ody-*` classes.
- `OdyTabs` shows exactly one panel; `OdyResourceBar` clamps its fraction; `OdyConfirmDialog` refuses to confirm
  with a blank required reason and removes itself on either outcome; `OdyMessages` uses only
  `UserMessageKey`/`SafeReasonCode`.

## 8. Deliverables

Palette, typography scale, class catalogue with examples: `docs/ui/Odyssey_Design_System.md`.

## 9. Acceptance criteria

1. Project-wide design system exists (tokens + classes + helpers). ✔
2. The existing catalog screen is not broken — N/A: it does not exist in the repository (see §4).
3. Palette / scale / classes documented. ✔
4. `git diff --name-status main` touches no backend path. ✔ (verified, see §17)

## 10. Tests and validation

| ID | Test | Check |
|---|---|---|
| `TC-UIKIT-001` | `OdysseyUiKitTests.Tabs_FirstTabIsActive_SelectSwitchesVisiblePanel` | one visible panel, active class, change event |
| `TC-UIKIT-002` | `ResourceBar_ComputesClampedFractionAndLabel` | fraction clamp, zero span, label |
| `TC-UIKIT-003` | `ConfirmDialog_RequiresTextBeforeConfirming_AndRemovesItselfOnEitherOutcome` | required reason, removal |
| `TC-UIKIT-004` | `Banner_ShowsKindAndText_HideClears` | kind modifier swap |
| `TC-UIKIT-005` | `Badge_ReplacesPreviousKindModifier` | one kind modifier |
| `TC-UIKIT-006` | `ParseList_TrimsDeduplicatesAndDropsEmpties` | list input parsing |
| `TC-UIKIT-007` | `Messages_UseSafeUserMessageKeyOrReasonCodeOnly` | safe error text |

Required commands: `./scripts/verify-format.ps1`, `./scripts/test-fast.ps1`, `./scripts/verify-repository.ps1`,
`./scripts/test-unity.ps1`, `./scripts/build-dev.ps1`.

**Not run in this session**: the cloud container has no Unity 6000.4.0f1, no `pwsh`, no .NET SDK. The product
owner explicitly accepted (2026-09-30) that code is prepared here and validated by them locally in Unity. No test
is claimed as passed.

Manual validation: Play mode → Developer shell renders unchanged (dark); the trial screen (`ODY-S11-201`) renders
with the light tokens.

## 11. Compatibility, migration, and rollback

No persisted data, no contract change. Rollback = revert the commit.

## 12. Dependencies and licensing

None added.

## 13. Security, privacy, and hidden information

`OdyMessages` never shows internal error codes, exception text, ids or paths (ADR-004).

## 14. Planning and execution mode — series plan

| Phase | Task | Result |
|---|---|---|
| 0 | `ODY-S11-200` | design system (this task) |
| 1 | `ODY-S11-201` | Owlbear board: full-bleed map + overlay top bar / drawers / dock |
| 2 | `ODY-S11-202` | content catalog UI for Item/Weapon/Armor/Ammo/Ability/Effect/Skill |
| 3 | `ODY-S11-203` | character sheet, creation/review, lifecycle |
| 4 | `ODY-S11-204` | inventory and equipment |
| 5 | `ODY-S11-205` | combat UI |

Each phase is a separate commit set on `claude/pensive-gates-n18srp`.

## 15. Documentation and versioning impact

New `docs/ui/Odyssey_Design_System.md`. No version bump.

## 16. Definition of Done

Code + docs committed; owner runs the Unity/PowerShell validation locally (see §10).

## 17. Completion evidence

- Changed files: see §5. `git diff --name-status main -- Packages DotNet` is empty.
- Validation results: not run (no toolchain in container) — owner validation pending.

## 18. Blockers, decisions, and change control

- Decision (product owner, 2026-09-30): ODY-S11-101 absent → build the UI anew.
- Decision (product owner, 2026-09-30): commit without in-container Unity validation; owner validates locally.
- Decision (executor): system font, text glyphs as temporary icons; no font/icon licence work.
