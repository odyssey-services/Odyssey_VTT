# Odyssey VTT — Client Design System

Owner task: `ODY-S11-200` (phase 0 of the full client UI series `ODY-S11-200`…`ODY-S11-205`).

- Stylesheet: `Assets/Odyssey/Client/UI/OdysseyDesignSystem.uss` (single file, sections 1–13).
- C# helpers: `Assets/Odyssey/Client/Runtime/Ui/OdysseyUiKit.cs` (`OdyClasses`, `OdyUi`, `OdyBanner`,
  `OdyResourceBar`, `OdyTabs`, `OdyConfirmDialog`, `OdyMessages`, `UiCommandIds`).
- Visual reference: D&D Beyond (light, clean, readable). **Exception:** the board screen follows Owlbear Rodeo —
  full-bleed map, every panel floats over it (section 12 of the stylesheet, `ODY-S11-201`).

## 1. Wiring (no runtime lookup)

`AppShell.uxml` references the stylesheet explicitly:

```xml
<Style src="OdysseyDesignSystem.uss" />
```

The UXML importer resolves this as a static asset dependency of the document the `UIDocument` already uses.
There is no `Resources.Load`, `ServiceLocator`, `GetService`, `FindObjectOfType` or `GameObject.Find`
(ADR-001 / `TC-ARCH-001`). Presenters only add class names; they never embed RGB values.

`AppShell.uss` (the dark developer-shell theme) is unchanged and still styles `DeveloperShellPresenter`.

`ODY-S11-101` (the earlier catalog theme `CatalogTheme.uss` with `--catalog-*` tokens) is **not present in the
repository** (verified against `main`, every `origin` branch and every PR up to #205). The product owner decided
(2026-09-30) to build the UI anew; the palette the spec quoted from that task (`#C53131` / `#F4F5F7` / `#1F2933`)
is kept as the base of the `--ody-*` tokens below, so nothing from it is lost.

## 2. Palette (`:root` custom properties)

| Token | Hex | Use |
|---|---|---|
| `--ody-color-accent` | `#C53131` | primary buttons, active tab, section rule, links |
| `--ody-color-accent-hover` | `#A82828` | primary hover |
| `--ody-color-accent-pressed` | `#8E2121` | primary active |
| `--ody-color-accent-soft` | `#FBE9E9` | selected list item, toggled button |
| `--ody-color-bg` | `#F4F5F7` | page background |
| `--ody-color-surface` | `#FFFFFF` | cards, inputs, lists |
| `--ody-color-surface-muted` | `#F9FAFB` | hover row, muted card |
| `--ody-color-surface-sunken` | `#EDEFF2` | secondary hover, tab hover |
| `--ody-color-border` | `#D7DBE0` | default borders/dividers |
| `--ody-color-border-strong` | `#B8BFC7` | input borders, card bottom edge |
| `--ody-color-text` | `#1F2933` | body text |
| `--ody-color-text-muted` | `#5B6875` | labels, meta |
| `--ody-color-text-subtle` | `#8A96A3` | placeholders |
| `--ody-color-text-inverse` | `#FFFFFF` | text on accent |
| `--ody-color-header` | `#1F2327` | dark bars (reserved) |
| `--ody-color-success` / `-soft` | `#2E7D4F` / `#E5F4EA` | Published, Pass, success banner |
| `--ody-color-warning` / `-soft` | `#B7791F` / `#FDF3E1` | Draft, conflict, warning banner |
| `--ody-color-danger` / `-soft` | `#9B2C2C` / `#FBE9E9` | errors, destructive buttons |
| `--ody-color-info` / `-soft` | `#2B6CB0` / `#E6F0FA` | Pending, info banner, role notices |
| `--ody-color-neutral-soft` | `#EDEFF2` | neutral/archived badge |
| `--ody-color-overlay-surface` | `rgba(255,255,255,0.96)` | board overlays |
| `--ody-color-overlay-scrim` | `rgba(17,20,24,0.45)` | modal scrim |
| `--ody-color-shadow` | `rgba(17,20,24,0.18)` | overlay bottom edge (USS has no box-shadow) |
| `--ody-color-board` | `#2A2E33` | map background |
| `--ody-color-bar-track` / `-fill` / `-fill-mid` / `-fill-high` | `#E3E6EA` / `#C53131` / `#D69E2E` / `#2E7D4F` | resource bars |

Radius: `--ody-radius-sm 4px`, `-md 6px`, `-lg 10px`, `-pill 999px`.
Spacing: `--ody-space-1…6` = `4, 8, 12, 16, 24, 32 px`. Sizes: control height `30px` (small `24px`), top bar
`44px`, drawer `380px`.

## 3. Typography

Decision: **system/default Unity font** for now (no TTF added — no font licence review in scope). A later art pass
can add a font by importing a TTF, creating a Font Asset (`Window > Text > Font Asset Creator`) and setting
`-unity-font-definition` on `.ody-root` / `.ody-game-root` only; no presenter change is needed.

| Class | Token | Size | Weight | Use |
|---|---|---|---|---|
| `.ody-text-display` | `--ody-font-display` | 28px | bold | screen title |
| `.ody-text-h1` | `--ody-font-h1` | 22px | bold | page/sheet title (character name) |
| `.ody-text-h2` | `--ody-font-h2` | 18px | bold | panel title, modal title |
| `.ody-text-h3` | `--ody-font-h3` | 16px | bold | card title, drawer title |
| `.ody-text-body` | `--ody-font-body` | 13px | regular | default text, controls, tabs |
| `.ody-text-small` | `--ody-font-small` | 12px | regular | field labels, bar labels |
| `.ody-text-caption` / `.ody-text-label` | `--ody-font-caption` | 11px | regular / bold+spaced | meta, badges, section titles |

Modifiers: `.ody-text-muted`, `.ody-text-strong`, `.ody-text-accent`, `.ody-text-danger`, `.ody-text-success`,
`.ody-text-wrap`.

Icons: none. Where a glyph is needed a plain text character is used (`×` close, `+`, `−`, `‹`, `›`), marked
**temporary until the art pass**.

## 4. Class catalogue (with usage)

### Layout
`.ody-root` (light page root), `.ody-screen` (padded page), `.ody-row` (horizontal, wrapping, spaced children),
`.ody-row--top`, `.ody-row--nowrap`, `.ody-column`, `.ody-grow`, `.ody-spacer`, `.ody-hidden` (display none —
use `OdyUi.SetVisible`), `.ody-scroll`, `.ody-split` + `.ody-split__sidebar` + `.ody-split__main` (list/detail),
`.ody-divider`.

```csharp
VisualElement split = new VisualElement(); split.AddToClassList(OdyClasses.Split);
list.AddToClassList(OdyClasses.SplitSidebar); detail.AddToClassList(OdyClasses.SplitMain);
```

### Cards, sections, key/value
`.ody-card` (+`--flat`, `--muted`), `.ody-card__header`, `.ody-card__title`, `.ody-card__body`,
`.ody-card__footer`; `.ody-section` + `.ody-section__title` (small caps title with red rule — DDB style);
`.ody-kv` + `.ody-kv__key` + `.ody-kv__value`.

```csharp
VisualElement card = OdyUi.Card("Attributes", out VisualElement body);
body.Add(OdyUi.KeyValue("Status", "Draft", out Label statusValue));
```

### Buttons
`.ody-button` (secondary), `--primary`, `--danger`, `--ghost`, `--small`, `--icon`; panel toggles are tab-convention buttons (see Tabs);
`.ody-button-row`.

```csharp
OdyUi.Button("Publish", Publish, OdyButtonVariant.Primary, name: "catalog-publish");
OdyUi.Button("Delete", Delete, OdyButtonVariant.Danger);
```

### Forms
`.ody-form`, `.ody-form-row`, `.ody-field` (stacked label + input for TextField/IntegerField/DropdownField),
`--grow`, `--narrow`, `--multiline`, `--invalid`; `.ody-toggle` (checkbox row); `.ody-field-error`,
`.ody-field-hint`.

```csharp
TextField name = OdyUi.TextField("Name", record.Name, "catalog-name");
name.EnableInClassList(OdyClasses.FieldInvalid, hasError);
```

### Lists
`.ody-list`, `.ody-list-item` (+`--selected`), `.ody-list-item__main`, `__title`, `__meta`, `.ody-empty-state`.

### Badges and status
`.ody-badge` + one modifier via `OdyUi.Badge(text, OdyStatusKind)` / `OdyUi.SetBadgeKind`:

| `OdyStatusKind` | Class | Meaning |
|---|---|---|
| `Draft` / `Published` / `Archived` | `--draft` / `--published` / `--archived` | content & character lifecycle |
| `Pending` | `--pending` | waiting for MainGM intervention (attack, recommendation) |
| `Conflict` | `--conflict` | stack conflict / stale data |
| `Error` / `Success` / `Info` / `Neutral` / `Accent` | matching modifier | results, generic state |

Banners (`OdyBanner`): `.ody-banner` + `--info` / `--success` / `--warning` / `--error`. Used for readable
explanations, e.g. “Only the MainGM can …” notices (never a silent disabled button), validation summaries,
command results (`banner.ShowError(action, error)` → `OdyMessages.Describe`).

### Tabs — the tab convention (`OdyTabs`, `OdyTabBar`, `ODY-S11-212`)
`.ody-tabs`, `.ody-tabs__bar`, `.ody-tab` (+`--active`, `--pill`), `.ody-tabs__panel`; `.ody-focus-visible`.

- **One active-state class** for every tab-like control — character sheet tabs, the top bar's panel toggles, the
  catalog type filter: `.ody-tab--active` (`OdyClasses.TabActive`, set with `OdyUi.SetActive`) = accent fill
  (`--ody-color-accent-soft`) + accent border + bold accent text.
- **Pill shape** (`--ody-radius-pill`, 999px) for sub-tabs inside a screen: `new OdyTabs(name, pill: true)`,
  `new OdyTabBar(name, pill: true)`. Top-level toggles keep the small radius.
- **Keyboard focus ring** on every tab button: Tab/arrow navigation shows a 2px `--ody-color-focus` border. USS has
  no `:focus-visible`, so `OdyFocusVisible` (owned by the game shell) puts `.ody-focus-visible` on the screen while the
  keyboard is used and removes it on the next pointer press; the rule is `.ody-focus-visible .ody-tab:focus`.
- Every tab button is created by `OdyUi.TabButton` (focusable).

```csharp
var tabs = new OdyTabs("character-tabs", pill: true);
VisualElement general = tabs.AddTab("general", "General");
tabs.Select("skills");
var filter = new OdyTabBar("catalog-type-tabs", pill: true); filter.AddTab("all", "All types");
```

### Resource bars (`OdyResourceBar`)
`.ody-resource-bar` (+`--small`), `.ody-resource-bar__fill` (+`--mid`, `--high` chosen by fraction),
`.ody-resource-bar__label`.

```csharp
var hp = new OdyResourceBar("resource-hp"); hp.SetValue(current, minimum, maximum, "HP");
```

### Modal confirmation (`OdyConfirmDialog`)
`.ody-modal-scrim`, `.ody-modal`, `.ody-modal__title`, `__body`, `__actions`. Mandatory for irreversible actions
(physical delete, archive, death, compensation, conflict resolution). `RequiredTextLabel` adds a mandatory
reason field.

```csharp
OdyConfirmDialog.Show(screenRoot,
    new OdyConfirmOptions("Delete permanently", "This cannot be undone.", "Delete") { Destructive = true, RequiredTextLabel = "Reason" },
    reason => DeleteCharacter(reason));
```

### Popover (`OdyPopover`, `ODY-S11-210`) — the one floating-surface primitive
Side drawers, confirmation dialogs and dropdown lists (`OdySelect`) are all `OdyPopover`s; new floating UI uses
only this primitive. Classes: `.ody-popover`, `.ody-popover__paper` (+`--hidden`), `.ody-popover-host` (marks the
screen that `OdyPopover.FindHost` mounts panel popovers on); `.ody-select` (+`__label`, `__button`, `__menu`,
`__options`, `__option`, `__option--active`).

| Option | Meaning |
|---|---|
| `OdyPopoverAnchor.ToElement(e, origin)` / `ToPoint(x, y)` | attach to a point of an element's rectangle, or to host-local coordinates; `OffsetX/OffsetY` |
| `Pivot` | which point of the popover sits on the anchor = growth direction (Left/Center/Right × Top/Center/Bottom) |
| `Width` / `Height`, `SetWidth` / `SetHeight` | fixed size (also while open); `null` = content size |
| `FillToBottomEdge` | height runs to the viewport bottom minus the margin (drawers) |
| `HidePaper` | no default surface; the content or `PaperClasses` style it |
| `DisableClickAway` | outside presses do not close it |
| `Modal` | scrim behind it blocks the screen |
| `EdgeMargin` (default 8px) | minimum distance to every viewport edge; the popover is moved and, if needed, shrunk |
| `ClickAwayScope` | where an outside press is detected (drawers: the board layer); presses are never swallowed |
| `RemoveOnClose` | one-shot (removed) vs persistent (hidden, stays mounted) |

```csharp
OdyPopover.Show(screen, menuContent, new OdyPopoverOptions(OdyPopoverAnchor.ToElement(button, OdyPopoverOrigin.BottomLeft))
    { Pivot = OdyPopoverOrigin.TopLeft, Width = 200f });
OdySelect mode = OdyUi.Select("Attack mode", choices, 0, "catalog-weapon-mode");
mode.ValueChanged += value => ...;
```

`OdyUi.Dropdown` (Unity `DropdownField`) remains only in the pre-existing call sites not yet migrated.

### Board / overlay layout (Owlbear exception, `ODY-S11-201`)
`.ody-game-root`, `.ody-board-layer` (full-bleed map), `.ody-overlay-layer` (picking ignored in code),
`.ody-topbar` (+`__title`, `__role`, `__toggles`), `.ody-drawer` (+`--left`, `--right`, `--wide`, `__header`,
`__title`, `__body`, `__content`), `.ody-dock` (+`--left`, `--collapsed`, `__header`, `__title`, `__body`),
`.ody-floating-toolbar`. Section 13 of the stylesheet (added by `ODY-S11-201`) restyles the existing board chrome (`#board-title`,
`#board-area`, `#board-toolbar`, `#board-status`) inside `.ody-board-layer` without touching the board presenter.

## 5. Rules for later phases

1. Use `OdyClasses` constants / `OdyUi` factories; no inline colors in presenters.
2. Role restrictions are explained with an info banner, never only hidden or disabled controls.
3. Every failure is shown through `OdyMessages.Describe` (safe `UserMessageKey` / `SafeReasonCode` only).
4. Irreversible actions go through `OdyConfirmDialog`.
5. Floating surfaces (menus, pickers, drawers, dialogs) are `OdyPopover`s; dropdown selectors in new code are `OdySelect` (`ODY-S11-210`).
6. Fixed-width text that may not fit (list row titles and meta lines, names) is made with `OdyUi.TruncatedText`: one line with an ellipsis, and the full text in a hover tooltip only when it is really cut (`ODY-S11-219`). Runtime UI Toolkit does not show the built-in `tooltip` property.
