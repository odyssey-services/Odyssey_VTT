using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-200 phase 0: class names of the project-wide design system (<c>OdysseyDesignSystem.uss</c>).
    /// Presenters reference these constants instead of repeating string literals or inline colors; the stylesheet
    /// itself is attached once through <c>AppShell.uxml</c> (an explicit asset reference -- no runtime asset lookup,
    /// ADR-001 / TC-ARCH-001). Full catalogue: <c>docs/ui/Odyssey_Design_System.md</c>.
    /// </summary>
    public static class OdyClasses
    {
        public const string Root = "ody-root";
        public const string Screen = "ody-screen";
        public const string Row = "ody-row";
        public const string RowTop = "ody-row--top";
        public const string Column = "ody-column";
        public const string Grow = "ody-grow";
        public const string Spacer = "ody-spacer";
        public const string Hidden = "ody-hidden";
        public const string Scroll = "ody-scroll";
        public const string Split = "ody-split";
        public const string SplitSidebar = "ody-split__sidebar";
        public const string SplitMain = "ody-split__main";
        public const string Divider = "ody-divider";

        public const string TextDisplay = "ody-text-display";
        public const string TextH1 = "ody-text-h1";
        public const string TextH2 = "ody-text-h2";
        public const string TextH3 = "ody-text-h3";
        public const string TextBody = "ody-text-body";
        public const string TextSmall = "ody-text-small";
        public const string TextCaption = "ody-text-caption";
        public const string TextLabel = "ody-text-label";
        public const string TextMuted = "ody-text-muted";
        public const string TextStrong = "ody-text-strong";
        public const string TextAccent = "ody-text-accent";
        public const string TextDanger = "ody-text-danger";
        public const string TextSuccess = "ody-text-success";
        public const string TextWrap = "ody-text-wrap";

        public const string Card = "ody-card";
        public const string CardFlat = "ody-card--flat";
        public const string CardMuted = "ody-card--muted";
        public const string CardHeader = "ody-card__header";
        public const string CardTitle = "ody-card__title";
        public const string CardBody = "ody-card__body";
        public const string CardFooter = "ody-card__footer";
        public const string Section = "ody-section";
        public const string SectionTitle = "ody-section__title";
        public const string Kv = "ody-kv";
        public const string KvKey = "ody-kv__key";
        public const string KvValue = "ody-kv__value";

        public const string Button = "ody-button";
        public const string ButtonPrimary = "ody-button--primary";
        public const string ButtonDanger = "ody-button--danger";
        public const string ButtonGhost = "ody-button--ghost";
        public const string ButtonSmall = "ody-button--small";
        public const string ButtonIcon = "ody-button--icon";
        public const string ButtonRow = "ody-button-row";

        public const string Form = "ody-form";
        public const string FormRow = "ody-form-row";
        public const string Field = "ody-field";
        public const string FieldGrow = "ody-field--grow";
        public const string FieldNarrow = "ody-field--narrow";
        public const string FieldMultiline = "ody-field--multiline";
        public const string FieldInvalid = "ody-field--invalid";
        public const string Toggle = "ody-toggle";
        public const string FieldError = "ody-field-error";
        public const string FieldHint = "ody-field-hint";

        public const string List = "ody-list";
        public const string ListItem = "ody-list-item";
        public const string ListItemSelected = "ody-list-item--selected";
        public const string ListItemMain = "ody-list-item__main";
        public const string ListItemTitle = "ody-list-item__title";
        public const string ListItemMeta = "ody-list-item__meta";
        public const string EmptyState = "ody-empty-state";

        public const string Badge = "ody-badge";
        public const string Banner = "ody-banner";

        public const string Tabs = "ody-tabs";
        public const string TabsBar = "ody-tabs__bar";
        public const string Tab = "ody-tab";
        /// <summary>
        /// ODY-S11-212: the one active-state class of the client -- tabs, sub-tabs, the top bar's panel toggles and the
        /// catalog type filter all use it (set through <see cref="OdyUi.SetActive"/>).
        /// </summary>
        public const string TabActive = "ody-tab--active";
        public const string TabPill = "ody-tab--pill";

        /// <summary>ODY-S11-217: counter badge on a tab-like toggle (background status while its panel is closed).</summary>
        public const string TabBadge = "ody-tab__badge";

        /// <summary>On a root while the user navigates with the keyboard: focus rings show (USS has no :focus-visible).</summary>
        public const string FocusVisible = "ody-focus-visible";

        /// <summary>ODY-S11-216: on a root, stops decorative motion below it (set by the polish P2 reduced-motion setting).</summary>
        public const string ReducedMotion = "ody-reduced-motion";
        public const string MarchingAnts = "ody-marching-ants";

        // ODY-S11-219: single-line text with an ellipsis, and the tooltip that shows the full text.
        public const string TextTruncate = "ody-text-truncate";
        public const string Tooltip = "ody-tooltip";
        public const string TabsPanel = "ody-tabs__panel";

        public const string ResourceBar = "ody-resource-bar";
        public const string ResourceBarSmall = "ody-resource-bar--small";
        public const string ResourceBarFill = "ody-resource-bar__fill";
        public const string ResourceBarFillMid = "ody-resource-bar__fill--mid";
        public const string ResourceBarFillHigh = "ody-resource-bar__fill--high";
        public const string ResourceBarLabel = "ody-resource-bar__label";

        public const string ModalScrim = "ody-modal-scrim";
        public const string Modal = "ody-modal";
        public const string ModalTitle = "ody-modal__title";
        public const string ModalBody = "ody-modal__body";
        public const string ModalActions = "ody-modal__actions";

        // ODY-S11-210: the popover primitive (OdyPopover) and the dropdown selector built on it (OdySelect).
        public const string Popover = "ody-popover";
        public const string PopoverHost = "ody-popover-host";
        public const string PopoverPaper = "ody-popover__paper";
        public const string PopoverPaperHidden = "ody-popover__paper--hidden";
        public const string Select = "ody-select";
        public const string SelectLabel = "ody-select__label";
        public const string SelectButton = "ody-select__button";
        public const string SelectMenu = "ody-select__menu";
        public const string SelectOptions = "ody-select__options";
        public const string SelectOption = "ody-select__option";
        public const string SelectOptionActive = "ody-select__option--active";

        public const string GameRoot = "ody-game-root";
        public const string GameScreen = "ody-game-screen";
        public const string BoardLayer = "ody-board-layer";
        public const string OverlayLayer = "ody-overlay-layer";
        public const string Topbar = "ody-topbar";
        public const string TopbarTitle = "ody-topbar__title";
        public const string TopbarRole = "ody-topbar__role";
        public const string TopbarToggles = "ody-topbar__toggles";
        public const string Drawer = "ody-drawer";
        public const string DrawerLeft = "ody-drawer--left";
        public const string DrawerRight = "ody-drawer--right";
        public const string DrawerWide = "ody-drawer--wide";
        public const string DrawerFrame = "ody-drawer__frame";
        public const string DrawerHeader = "ody-drawer__header";
        public const string DrawerTitle = "ody-drawer__title";
        public const string DrawerBody = "ody-drawer__body";
        public const string DrawerContent = "ody-drawer__content";
        public const string Dock = "ody-dock";
        public const string DockLeft = "ody-dock--left";
        public const string DockCollapsed = "ody-dock--collapsed";
        public const string DockHeader = "ody-dock__header";
        public const string DockTitle = "ody-dock__title";
        public const string DockBody = "ody-dock__body";
        public const string FloatingToolbar = "ody-floating-toolbar";
    }

    public enum OdyButtonVariant
    {
        Secondary = 1,
        Primary = 2,
        Danger = 3,
        Ghost = 4
    }

    /// <summary>Badge / status colors. Lifecycle (Draft/Published/Archived) and runtime states (Pending/Conflict/Error).</summary>
    public enum OdyStatusKind
    {
        Neutral = 1,
        Draft = 2,
        Published = 3,
        Archived = 4,
        Pending = 5,
        Conflict = 6,
        Error = 7,
        Success = 8,
        Info = 9,
        Accent = 10
    }

    public enum OdyBannerKind
    {
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }

    /// <summary>
    /// ODY-S11-200 phase 0: small, stateless factory helpers for the recurring building blocks. Plain static helpers
    /// (no registry, no lookup) -- each returns a new element carrying the design-system classes.
    /// </summary>
    public static class OdyUi
    {
        public static Label Text(string text, params string[] classes)
        {
            var label = new Label(text ?? string.Empty);
            foreach (string cls in classes) label.AddToClassList(cls);
            return label;
        }

        /// <summary>
        /// ODY-S11-219: a one-line label that ends in an ellipsis when it does not fit and shows its full text on hover
        /// (<see cref="OdyTruncationTooltip"/>). Use it for list rows and other fixed-width text.
        /// </summary>
        public static Label TruncatedText(string text, params string[] classes)
        {
            Label label = Text(text, classes);
            OdyTruncationTooltip.Attach(label);
            return label;
        }

        public static Button Button(string text, Action onClick, OdyButtonVariant variant = OdyButtonVariant.Secondary, string? name = null, bool small = false)
        {
            if (onClick == null) throw new ArgumentNullException(nameof(onClick));
            var button = new Button(onClick) { text = text ?? string.Empty };
            if (!string.IsNullOrEmpty(name)) button.name = name;
            button.AddToClassList(OdyClasses.Button);
            switch (variant)
            {
                case OdyButtonVariant.Primary:
                    button.AddToClassList(OdyClasses.ButtonPrimary);
                    break;
                case OdyButtonVariant.Danger:
                    button.AddToClassList(OdyClasses.ButtonDanger);
                    break;
                case OdyButtonVariant.Ghost:
                    button.AddToClassList(OdyClasses.ButtonGhost);
                    break;
            }

            if (small) button.AddToClassList(OdyClasses.ButtonSmall);
            return button;
        }

        public static VisualElement ButtonRow(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.ButtonRow);
            foreach (VisualElement child in children) row.Add(child);
            return row;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.Row);
            foreach (VisualElement child in children) row.Add(child);
            return row;
        }

        public static Label Badge(string text, OdyStatusKind kind, string? name = null)
        {
            var badge = new Label(text ?? string.Empty);
            if (!string.IsNullOrEmpty(name)) badge.name = name;
            badge.AddToClassList(OdyClasses.Badge);
            SetBadgeKind(badge, kind);
            return badge;
        }

        public static void SetBadgeKind(VisualElement badge, OdyStatusKind kind)
        {
            if (badge == null) throw new ArgumentNullException(nameof(badge));
            foreach (OdyStatusKind candidate in (OdyStatusKind[])Enum.GetValues(typeof(OdyStatusKind)))
            {
                badge.RemoveFromClassList(BadgeModifier(candidate));
            }

            badge.AddToClassList(BadgeModifier(kind));
        }

        public static string BadgeModifier(OdyStatusKind kind) => "ody-badge--" + kind.ToString().ToLowerInvariant();

        /// <summary>A card with an optional title; children go into <paramref name="body"/>.</summary>
        public static VisualElement Card(string? title, out VisualElement body, string? name = null)
        {
            var card = new VisualElement();
            if (!string.IsNullOrEmpty(name)) card.name = name;
            card.AddToClassList(OdyClasses.Card);
            if (!string.IsNullOrEmpty(title))
            {
                var header = new VisualElement();
                header.AddToClassList(OdyClasses.CardHeader);
                header.Add(Text(title!, OdyClasses.CardTitle));
                card.Add(header);
            }

            body = new VisualElement();
            body.AddToClassList(OdyClasses.CardBody);
            card.Add(body);
            return card;
        }

        public static VisualElement Section(string title, string? name = null)
        {
            var section = new VisualElement();
            if (!string.IsNullOrEmpty(name)) section.name = name;
            section.AddToClassList(OdyClasses.Section);
            section.Add(Text(title, OdyClasses.SectionTitle));
            return section;
        }

        public static VisualElement KeyValue(string key, string value, out Label valueLabel, string? valueName = null)
        {
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.Kv);
            row.Add(Text(key, OdyClasses.KvKey));
            valueLabel = Text(value, OdyClasses.KvValue);
            if (!string.IsNullOrEmpty(valueName)) valueLabel.name = valueName;
            row.Add(valueLabel);
            return row;
        }

        public static TextField TextField(string label, string value, string name, bool multiline = false)
        {
            var field = new TextField(label) { name = name, value = value ?? string.Empty, multiline = multiline };
            field.AddToClassList(OdyClasses.Field);
            if (multiline) field.AddToClassList(OdyClasses.FieldMultiline);
            return field;
        }

        public static IntegerField IntegerField(string label, int value, string name)
        {
            var field = new IntegerField(label) { name = name, value = value };
            field.AddToClassList(OdyClasses.Field);
            field.AddToClassList(OdyClasses.FieldNarrow);
            return field;
        }

        public static DropdownField Dropdown(string label, List<string> choices, int index, string name)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            var field = new DropdownField(label, choices, choices.Count == 0 ? -1 : Math.Max(0, Math.Min(index, choices.Count - 1))) { name = name };
            field.AddToClassList(OdyClasses.Field);
            return field;
        }

        /// <summary>ODY-S11-210: the dropdown selector for new code (its list is an <see cref="OdyPopover"/>).</summary>
        public static OdySelect Select(string label, IReadOnlyList<string> choices, int index, string name) => new OdySelect(label, choices, index, name);

        public static Toggle Toggle(string label, bool value, string name)
        {
            var toggle = new Toggle(label) { name = name, value = value };
            toggle.AddToClassList(OdyClasses.Toggle);
            return toggle;
        }

        /// <summary>ODY-S11-212: marks an element active/inactive with the single active-state class.</summary>
        public static void SetActive(VisualElement element, bool active) => element.EnableInClassList(OdyClasses.TabActive, active);

        public static bool IsActive(VisualElement element) => element != null && element.ClassListContains(OdyClasses.TabActive);

        /// <summary>
        /// ODY-S11-212: a tab-convention button (tab, sub-tab, panel toggle): keyboard-focusable, active state through
        /// <see cref="SetActive"/>; <paramref name="pill"/> for sub-tabs inside a screen.
        /// </summary>
        public static Button TabButton(string text, Action onClick, string name, bool pill = false)
        {
            var button = new Button(onClick) { name = name, text = text ?? string.Empty, focusable = true };
            button.AddToClassList(OdyClasses.Tab);
            if (pill) button.AddToClassList(OdyClasses.TabPill);
            return button;
        }

        public static Label EmptyState(string text, string? name = null)
        {
            Label label = Text(text, OdyClasses.EmptyState);
            if (!string.IsNullOrEmpty(name)) label.name = name;
            return label;
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            element.EnableInClassList(OdyClasses.Hidden, !visible);
        }

        public static bool IsVisible(VisualElement element) => element != null && !element.ClassListContains(OdyClasses.Hidden);

        /// <summary>Parses a comma/newline separated list of short keys, trimming and dropping empties (tags, ammo keys, body part ids).</summary>
        public static List<string> ParseList(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (string part in text!.Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0 && !result.Contains(trimmed)) result.Add(trimmed);
            }

            return result;
        }

        public static string JoinList(IEnumerable<string> values) => string.Join(", ", values);

        public static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Canonical id minting for UI-issued commands (the exact pattern every existing presenter already uses).</summary>
    public static class UiCommandIds
    {
        public static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        public static CorrelationId NewCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// ADR-004 outer boundary for UI-issued commands: repositories and request constructors guard their preconditions
    /// with argument exceptions (a programming error, not a normal outcome). A form must never crash the screen, so
    /// such an exception is translated here into a typed, safe <c>InvalidRequest</c> failure; any other exception is
    /// not swallowed.
    /// </summary>
    public static class UiGuard
    {
        private static readonly CorrelationId Placeholder = CorrelationId.Parse("corr_00000000000000000000000000000000");

        public static Result<T> Run<T>(Func<Result<T>> call) where T : notnull
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            try
            {
                return call();
            }
            catch (ArgumentException)
            {
                return Result<T>.Failure(InvalidRequest());
            }
            catch (FormatException)
            {
                return Result<T>.Failure(InvalidRequest());
            }
        }

        public static Result Run(Func<Result> call)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            try
            {
                return call();
            }
            catch (ArgumentException)
            {
                return Result.Failure(InvalidRequest());
            }
            catch (FormatException)
            {
                return Result.Failure(InvalidRequest());
            }
        }

        public static Error InvalidRequest() => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.ui.invalid_request"), RetryDirective.DoNotRetry, Placeholder);
    }

    /// <summary>A banner that shows one readable message at a time (role explanation, validation summary, result).</summary>
    public sealed class OdyBanner
    {
        private readonly Label _label;

        public OdyBanner(string name)
        {
            Element = new VisualElement { name = name };
            Element.AddToClassList(OdyClasses.Banner);
            _label = new Label { name = name + "-text" };
            _label.AddToClassList(OdyClasses.TextWrap);
            Element.Add(_label);
            Hide();
        }

        public VisualElement Element { get; }
        public string Text => _label.text;
        public OdyBannerKind Kind { get; private set; } = OdyBannerKind.Info;
        public bool IsVisible => OdyUi.IsVisible(Element);

        public void Show(OdyBannerKind kind, string text)
        {
            foreach (OdyBannerKind candidate in (OdyBannerKind[])Enum.GetValues(typeof(OdyBannerKind)))
            {
                Element.RemoveFromClassList(Modifier(candidate));
            }

            Kind = kind;
            Element.AddToClassList(Modifier(kind));
            _label.text = text ?? string.Empty;
            OdyUi.SetVisible(Element, true);
        }

        public void ShowError(string action, Error error) => Show(OdyBannerKind.Error, action + ": " + OdyMessages.Describe(error));

        public void Hide()
        {
            _label.text = string.Empty;
            OdyUi.SetVisible(Element, false);
        }

        private static string Modifier(OdyBannerKind kind) => "ody-banner--" + kind.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// A filled bar with a text label over it (HP and similar resources). <see cref="SetValue"/> is instant (the local
    /// user's own change); <see cref="AnimateFrom"/> then eases the fill from a previously shown value (ODY-S11-211:
    /// a change made by someone else). The numbers in the label always show the new value at once.
    /// </summary>
    public sealed class OdyResourceBar
    {
        private readonly VisualElement _fill;
        private readonly Label _label;
        private OdyTween? _tween;
        private IVisualElementScheduledItem? _ticker;

        public OdyResourceBar(string name, bool small = false)
        {
            Element = new VisualElement { name = name };
            Element.AddToClassList(OdyClasses.ResourceBar);
            if (small) Element.AddToClassList(OdyClasses.ResourceBarSmall);
            _fill = new VisualElement { name = name + "-fill" };
            _fill.AddToClassList(OdyClasses.ResourceBarFill);
            _fill.pickingMode = PickingMode.Ignore;
            Element.Add(_fill);
            _label = new Label { name = name + "-label" };
            _label.AddToClassList(OdyClasses.ResourceBarLabel);
            _label.pickingMode = PickingMode.Ignore;
            Element.Add(_label);
        }

        public VisualElement Element { get; }

        /// <summary>The value's fraction (the target while animating).</summary>
        public double FillFraction { get; private set; }

        /// <summary>The fraction drawn right now.</summary>
        public double DisplayedFraction { get; private set; }

        public bool IsAnimating => _tween != null && !_tween.IsDone;
        public string LabelText => _label.text;

        /// <summary>Fraction is (current - minimum) / (maximum - minimum), clamped to [0, 1]; a non-positive span shows empty.</summary>
        public void SetValue(long current, long minimum, long maximum, string? caption = null)
        {
            long span = maximum - minimum;
            double fraction = span <= 0 ? 0.0 : (double)(current - minimum) / span;
            if (fraction < 0.0) fraction = 0.0;
            if (fraction > 1.0) fraction = 1.0;
            FillFraction = fraction;
            StopAnimation();
            ShowFraction(fraction);
            _fill.EnableInClassList(OdyClasses.ResourceBarFillMid, fraction >= 0.34 && fraction < 0.67);
            _fill.EnableInClassList(OdyClasses.ResourceBarFillHigh, fraction >= 0.67);
            string numbers = OdyUi.Format(current) + " / " + OdyUi.Format(maximum);
            _label.text = string.IsNullOrEmpty(caption) ? numbers : caption + "  " + numbers;
        }

        /// <summary>
        /// Call after <see cref="SetValue"/>: the fill starts at <paramref name="fromFraction"/> (the previously shown
        /// value) and eases to <see cref="FillFraction"/>. Driven by the element's own scheduler while it is on a panel.
        /// </summary>
        public void AnimateFrom(double fromFraction, double durationMs = OdyMotion.RemoteUpdateDurationMs)
        {
            double from = double.IsNaN(fromFraction) ? FillFraction : Math.Max(0.0, Math.Min(1.0, fromFraction));
            StopAnimation();
            if (from.Equals(FillFraction)) return;
            _tween = new OdyTween(from, FillFraction, durationMs);
            ShowFraction(_tween.Current);
            if (_tween.IsDone)
            {
                _tween = null;
                return;
            }

            _ticker = Element.schedule.Execute(timer => AdvanceAnimation(timer.deltaTime)).Every(OdyMotion.FrameIntervalMs).Until(() => !IsAnimating);
        }

        /// <summary>Steps a running animation by <paramref name="elapsedMs"/>; public so tests step it deterministically.</summary>
        public void AdvanceAnimation(double elapsedMs)
        {
            if (_tween == null) return;
            _tween.Advance(elapsedMs);
            ShowFraction(_tween.Current);
            if (_tween.IsDone) _tween = null;
        }

        private void StopAnimation()
        {
            _tween = null;
            _ticker?.Pause();
            _ticker = null;
        }

        private void ShowFraction(double fraction)
        {
            DisplayedFraction = fraction;
            _fill.style.width = new Length((float)(fraction * 100.0), LengthUnit.Percent);
        }
    }

    /// <summary>
    /// ODY-S11-212: a row of tab buttons with exactly one active (<see cref="OdyClasses.TabActive"/>). Used alone as a
    /// filter (catalog types) and inside <see cref="OdyTabs"/>. Pill shape for sub-tabs inside a screen.
    /// </summary>
    public sealed class OdyTabBar
    {
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private readonly bool _pill;

        public OdyTabBar(string name, bool pill = false)
        {
            _pill = pill;
            Element = new VisualElement { name = name };
            Element.AddToClassList(OdyClasses.TabsBar);
        }

        public event Action<string>? TabChanged;

        public VisualElement Element { get; }
        public string? ActiveTabId { get; private set; }
        public IReadOnlyList<string> TabIds => _order;
        public bool IsPill => _pill;

        public Button AddTab(string id, string title)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tab id is required.", nameof(id));
            if (_buttons.ContainsKey(id)) throw new ArgumentException("Duplicate tab id.", nameof(id));
            Button button = OdyUi.TabButton(title, () => Select(id), "tab-" + id, _pill);
            Element.Add(button);
            _buttons[id] = button;
            _order.Add(id);
            return button;
        }

        public Button? ButtonFor(string id) => _buttons.TryGetValue(id, out Button? button) ? button : null;

        /// <summary>Makes <paramref name="id"/> the active tab; raises <see cref="TabChanged"/> when it changed and <paramref name="notify"/>.</summary>
        public bool Select(string id, bool notify = true)
        {
            if (!_buttons.ContainsKey(id)) return false;
            foreach (KeyValuePair<string, Button> entry in _buttons) OdyUi.SetActive(entry.Value, string.Equals(entry.Key, id, StringComparison.Ordinal));
            bool changed = !string.Equals(ActiveTabId, id, StringComparison.Ordinal);
            ActiveTabId = id;
            if (changed && notify) TabChanged?.Invoke(id);
            return true;
        }
    }

    /// <summary>Tab strip + panels. One panel visible at a time; tests call <see cref="Select"/> directly.</summary>
    public sealed class OdyTabs
    {
        private readonly OdyTabBar _bar;
        private readonly VisualElement _panels;
        private readonly Dictionary<string, VisualElement> _panelsById = new Dictionary<string, VisualElement>(StringComparer.Ordinal);

        /// <param name="pill">ODY-S11-212: pill-shaped sub-tabs (tabs inside a screen, e.g. the character sheet).</param>
        public OdyTabs(string name, bool pill = false)
        {
            Element = new VisualElement { name = name };
            Element.AddToClassList(OdyClasses.Tabs);
            _bar = new OdyTabBar(name + "-bar", pill);
            _bar.TabChanged += id => TabChanged?.Invoke(id);
            Element.Add(_bar.Element);
            _panels = new VisualElement { name = name + "-panels" };
            _panels.AddToClassList(OdyClasses.TabsPanel);
            Element.Add(_panels);
        }

        public event Action<string>? TabChanged;

        public VisualElement Element { get; }
        public OdyTabBar Bar => _bar;
        public string? ActiveTabId => _bar.ActiveTabId;
        public IReadOnlyList<string> TabIds => _bar.TabIds;

        public VisualElement AddTab(string id, string title)
        {
            _bar.AddTab(id, title);
            var panel = new ScrollView(ScrollViewMode.Vertical) { name = "tab-panel-" + id };
            panel.AddToClassList(OdyClasses.Scroll);
            OdyUi.SetVisible(panel, false);
            _panels.Add(panel);
            _panelsById[id] = panel;
            if (ActiveTabId == null) Select(id);
            return panel.contentContainer;
        }

        public bool Select(string id)
        {
            if (!_panelsById.ContainsKey(id)) return false;
            // Panels first, so a TabChanged handler already sees the new panel visible.
            foreach (KeyValuePair<string, VisualElement> entry in _panelsById) OdyUi.SetVisible(entry.Value, string.Equals(entry.Key, id, StringComparison.Ordinal));
            return _bar.Select(id);
        }

        public bool IsPanelVisible(string id) => _panelsById.TryGetValue(id, out VisualElement panel) && OdyUi.IsVisible(panel);
    }

    /// <summary>
    /// ODY-S11-212: keyboard focus rings without showing them on mouse clicks. USS has no <c>:focus-visible</c>, so the
    /// root gets <see cref="OdyClasses.FocusVisible"/> while the user navigates with the keyboard (Tab / arrows) and
    /// loses it on the next pointer press; the stylesheet draws the ring only under that class. Owned by the screen
    /// presenter that creates it; <see cref="Dispose"/> removes its callbacks.
    /// ODY-S11-215: keyboard navigation is detected only through <see cref="NavigationMoveEvent"/> -- arrows arrive as
    /// one from the <c>UI/Navigate</c> action, Tab / Shift+Tab from <c>UiTabNavigation</c>. The former raw
    /// <c>KeyDownEvent</c> Tab check is removed: the input module never delivers an unbound Tab key as a KeyDownEvent.
    /// </summary>
    public sealed class OdyFocusVisible : IDisposable
    {
        private readonly VisualElement _root;
        private bool _disposed;

        public OdyFocusVisible(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        }

        public bool IsKeyboardMode => _root.ClassListContains(OdyClasses.FocusVisible);

        public void NoteKeyboardNavigation() => _root.AddToClassList(OdyClasses.FocusVisible);

        public void NotePointer() => _root.RemoveFromClassList(OdyClasses.FocusVisible);

        public void Dispose()
        {
            if (_disposed) return;
            _root.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            NotePointer();
            _disposed = true;
        }

        private void OnNavigationMove(NavigationMoveEvent evt) => NoteKeyboardNavigation();

        private void OnPointerDown(PointerDownEvent evt) => NotePointer();
    }

    /// <summary>Options of a confirmation dialog.</summary>
    public sealed class OdyConfirmOptions
    {
        public OdyConfirmOptions(string title, string message, string confirmText)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ConfirmText = confirmText ?? throw new ArgumentNullException(nameof(confirmText));
        }

        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public string CancelText { get; set; } = "Cancel";

        /// <summary>Destructive/irreversible actions (delete, archive, override) get the danger button.</summary>
        public bool Destructive { get; set; }

        /// <summary>When set, the dialog shows a text field with this label and refuses to confirm while it is blank (reason codes).</summary>
        public string? RequiredTextLabel { get; set; }
    }

    /// <summary>
    /// Modal confirmation for irreversible actions: a modal <see cref="OdyPopover"/> on a host element (usually the
    /// screen root), removed on either outcome. <see cref="Confirm"/>/<see cref="Cancel"/> are public so tests drive it without synthetic clicks.
    /// </summary>
    public sealed class OdyConfirmDialog
    {
        private readonly OdyConfirmOptions _options;
        private readonly Action<string> _onConfirm;
        private readonly Action? _onCancel;
        private readonly TextField? _textField;
        private readonly Label _error;
        private readonly OdyPopover _popover;

        private OdyConfirmDialog(VisualElement host, OdyConfirmOptions options, Action<string> onConfirm, Action? onCancel)
        {
            _options = options;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // ODY-S11-210: a modal OdyPopover centered on the host; the scrim blocks the screen and a click on it does
            // not dismiss (irreversible actions need an explicit Confirm or Cancel).
            var modal = new VisualElement { name = "ody-confirm-dialog-content" };
            _popover = new OdyPopover(host, modal, new OdyPopoverOptions(OdyPopoverAnchor.ToElement(host, OdyPopoverOrigin.Center))
            {
                Pivot = OdyPopoverOrigin.Center,
                Modal = true,
                DisableClickAway = true,
                HidePaper = true,
                Name = "ody-confirm-dialog",
                PaperName = "ody-confirm-dialog-panel",
                PaperClasses = new[] { OdyClasses.Modal }
            });
            Element = _popover.Element;

            modal.Add(OdyUi.Text(options.Title, OdyClasses.ModalTitle));
            var body = new VisualElement();
            body.AddToClassList(OdyClasses.ModalBody);
            body.Add(OdyUi.Text(options.Message, OdyClasses.TextWrap));
            if (!string.IsNullOrEmpty(options.RequiredTextLabel))
            {
                _textField = OdyUi.TextField(options.RequiredTextLabel!, string.Empty, "ody-confirm-dialog-text");
                body.Add(_textField);
            }

            _error = OdyUi.Text(string.Empty, OdyClasses.FieldError);
            _error.name = "ody-confirm-dialog-error";
            OdyUi.SetVisible(_error, false);
            body.Add(_error);
            modal.Add(body);

            var actions = new VisualElement();
            actions.AddToClassList(OdyClasses.ModalActions);
            actions.Add(OdyUi.Button(options.CancelText, Cancel, OdyButtonVariant.Secondary, "ody-confirm-dialog-cancel"));
            actions.Add(OdyUi.Button(options.ConfirmText, () => Confirm(), options.Destructive ? OdyButtonVariant.Danger : OdyButtonVariant.Primary, "ody-confirm-dialog-confirm"));
            modal.Add(actions);

            _popover.Open();
            IsOpen = true;
        }

        public VisualElement Element { get; }
        public OdyPopover Popover => _popover;
        public bool IsOpen { get; private set; }
        public string Title => _options.Title;

        public static OdyConfirmDialog Show(VisualElement host, OdyConfirmOptions options, Action<string> onConfirm, Action? onCancel = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));
            return new OdyConfirmDialog(host, options, onConfirm, onCancel);
        }

        public void SetText(string text)
        {
            if (_textField != null) _textField.value = text ?? string.Empty;
        }

        /// <summary>Returns false (and stays open) when a required text is blank.</summary>
        public bool Confirm()
        {
            if (!IsOpen) return false;
            string text = _textField?.value ?? string.Empty;
            if (_textField != null && string.IsNullOrWhiteSpace(text))
            {
                _error.text = _options.RequiredTextLabel + " is required.";
                OdyUi.SetVisible(_error, true);
                return false;
            }

            Close();
            _onConfirm(text.Trim());
            return true;
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            Close();
            _onCancel?.Invoke();
        }

        private void Close()
        {
            IsOpen = false;
            _popover.Close();
        }
    }

    /// <summary>
    /// Maps a typed <see cref="Error"/> to a readable, safe sentence (ADR-004: only <see cref="Error.UserMessageKey"/> and
    /// <see cref="Error.SafeReasonCode"/> are used -- never internal codes, exception text or ids). Unknown keys fall back
    /// to the safe reason code's generic sentence.
    /// </summary>
    public static class OdyMessages
    {
        private static readonly Dictionary<string, string> ByMessageKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "errors.content_catalog.authoring_denied", "Only the MainGM can author the content catalog." },
            { "errors.content_catalog.publish_validation_failed", "The definition did not pass publish validation -- see the issues listed below." },
            { "errors.content_catalog.validation.not_draft", "Only a Draft can be validated for publishing." },
            { "errors.content_catalog.validation.ruleset_incompatible", "Ruleset compatibility does not include the campaign's active ruleset." },
            { "errors.content_catalog.validation.weapon_ammo_keys_required", "The weapon needs ammunition but lists no compatible ammo keys." },
            { "errors.content_catalog.validation.weapon_no_compatible_ammo", "The weapon requires ammunition, but no Ammo definition in the catalog shares any of its compatible ammo keys. Create an Ammo definition with a matching compatibility key first." },
            { "errors.content_catalog.validation.dependency_cycle", "The definition's references form a cycle." },
            { "errors.content_catalog.validation.dependency_graph_too_deep", "The reference chain is too deep." },
            { "errors.content_catalog.validation.reference_missing", "A referenced definition does not exist." },
            { "errors.content_catalog.validation.reference_version_mismatch", "A referenced definition version is not published." },
            { "errors.content_catalog.validation.reference_wrong_type", "A reference points to a definition of the wrong type." },
            { "errors.combat.denied", "Only the MainGM can do this in combat." },
            { "errors.inventory.move_denied", "Only the MainGM can move or equip items." },
            { "errors.ui.invalid_request", "The entered values are not valid for this action." },
            { "errors.persistence.character_not_found", "The character was not found." },
            { "errors.persistence.character_revision_conflict", "The character changed since it was loaded. The sheet was refreshed -- try again." },
            { "errors.persistence.character_development_insufficient_balance", "Not enough development points available." },
            { "errors.persistence.character_development_purchase_denied", "Only the character's owner or the MainGM can spend its development points." },
            { "errors.persistence.character_development_grant_denied", "Only the MainGM can grant development points." },
            { "errors.persistence.character_attribute_cap_exceeded", "This value is above the normal development cap and needs an explicit rule, ability or MainGM override." },
            { "errors.persistence.character_skill_level_requires_recommendation", "Skill levels above the ordinary limit need a MainGM-approved recommendation." },
            { "errors.persistence.character_advancement_recommendation_not_pending", "This recommendation was already resolved." },
            { "errors.persistence.character_advancement_resolution_denied", "Only the MainGM can resolve recommendations." },
            { "errors.persistence.character_advancement_reason_required", "A reason is required." },
            { "errors.persistence.character_advancement_purchase_has_dependent", "A later purchase depends on this one; revert that first." },
            { "errors.persistence.character_advancement_purchase_not_applied", "This purchase is no longer applied." },
            { "errors.persistence.character_advancement_operation_kind_not_supported", "Only attribute and skill purchases can be reverted or respecced." },
            { "errors.persistence.character_advancement_operation_denied", "You cannot change this character's advancement." },
            { "errors.persistence.character_ability_grant_denied", "Only the MainGM can grant abilities." },
            { "errors.persistence.character_ability_removal_not_allowed", "Only abilities from an item or an active effect can be removed." },
            { "errors.persistence.character_ability_not_found", "The ability was not found on this character." },
            { "errors.persistence.character_resource_operation_denied", "Only the MainGM can change resources." },
            { "errors.persistence.character_resource_value_out_of_range", "The value is outside the resource's allowed range." },
            { "errors.persistence.character_resource_not_found", "The resource was not found on this character." },
            { "errors.persistence.character_anatomy_already_initialized", "The anatomy is already initialized." },
            { "errors.persistence.character_anatomy_not_initialized", "Initialize the character's anatomy first." },
            { "errors.persistence.character_anatomy_operation_denied", "You cannot change this character's anatomy." },
            { "errors.persistence.character_body_part_already_exists", "A body part with this id already exists." },
            { "errors.persistence.character_body_part_has_dependent", "This body part cannot be removed: another part is attached to it or equipment is worn on it." },
            { "errors.persistence.character_body_part_not_found", "The body part was not found." },
            { "errors.persistence.character_ownership_denied", "Only the MainGM can change ownership and control." },
            { "errors.persistence.character_ownership_reason_required", "A reason is required." },
            { "errors.persistence.character_approval_denied", "Only the MainGM can approve a character." },
            { "errors.persistence.character_archive_denied", "Only the MainGM or the character's owner can archive it." },
            { "errors.persistence.character_deletion_denied", "Only the MainGM can delete a character permanently." },
            { "errors.persistence.character_deletion_has_dependent", "The character still owns items or other data; remove them first." },
            { "errors.persistence.character_deletion_reason_required", "A reason is required." },
            { "errors.persistence.character_dead_transition_denied", "Only the MainGM can mark a character dead." },
            { "errors.persistence.character_restore_denied", "Only the MainGM can restore a dead character." },
            { "errors.persistence.character_restore_not_dead", "Only a dead character can be restored." },
            { "errors.persistence.character_restore_reason_required", "A reason is required." },
            { "errors.persistence.character_lifecycle_transition_invalid", "This lifecycle change is not allowed from the character's current status." },
            { "errors.persistence.character_draft_ruleset_incompatible", "The template's ruleset does not match the campaign." },
            { "errors.persistence.character_export_bundle_malformed", "The selected .odchar bundle is missing or damaged." },
            { "errors.persistence.inventory_item_revision_conflict", "The item changed since it was loaded. The inventory was refreshed -- try again." },
            { "errors.persistence.inventory_revision_conflict", "The inventory changed since it was loaded. It was refreshed -- try again." },
            { "errors.persistence.equipment_entry_revision_conflict", "The equipment changed since it was loaded. It was refreshed -- try again." },
            { "errors.persistence.equipment_entry_already_equipped", "This item is already equipped." },
            { "errors.persistence.equipment_entry_not_found", "The item is not equipped." },
            { "errors.inventory.create_denied", "Only the MainGM can create items." },
            { "errors.inventory.create_definition_not_published", "Only published catalog definitions can become items." },
            { "errors.inventory.create_definition_type_unsupported", "This definition cannot be created with the chosen form (instance vs. stack)." },
            { "errors.inventory.equip_body_part_not_found", "One of the chosen body parts does not exist on the character." },
            { "errors.inventory.equip_body_part_refs_require_character_owner", "Body parts can only be chosen for a character's inventory." },
            { "errors.inventory.move_destination_unchanged", "The item is already there." },
            { "errors.inventory.move_source_invalid", "The item is not in the chosen source inventory." },
            { "errors.inventory.stack_merge_exceeds_max_quantity", "The merged stack would exceed the maximum stack size." },
            { "errors.inventory.stack_merge_mismatch", "Only stacks of the same definition can be merged." },
            { "errors.inventory.stack_split_quantity_invalid", "The split quantity must be between 1 and the stack size minus 1." },
            { "errors.attack.denied", "You cannot attack with this character." },
            { "errors.attack.not_current_turn", "It is not this character's turn." },
            { "errors.persistence.attack_outcome_not_pending", "This attack is not waiting for a decision." },
            { "errors.persistence.attack_outcome_already_compensated", "This log entry was already corrected." },
            { "errors.persistence.attack_outcome_compensation_reason_required", "A reason is required to correct a log entry." },
            { "errors.persistence.attack_outcome_not_accepted", "Only an applied attack can have its log entry corrected." },
            { "errors.persistence.attack_outcome_operation_denied", "Only the MainGM can do this." },
            { "errors.persistence.combat_stack_conflict_already_resolved", "This conflict was already resolved." },
            { "errors.persistence.combat_stack_conflict_not_found", "No such unresolved stacking conflict." },
            { "errors.persistence.combat_stack_conflict_operation_denied", "Only the MainGM can resolve stacking conflicts." },
            { "errors.check.formula_invalid", "The check formula is not valid (for example 1d20+2)." },
            { "errors.check.requires_exactly_one_dice_group", "A check formula needs exactly one dice group, like 1d20." },
            { "errors.check.requires_at_most_one_attribute_reference", "A check formula can reference at most one attribute." },
            { "errors.check.unresolved_reference", "The formula references something the character does not have." },
            { "errors.check.ambiguous_reference", "The formula reference is ambiguous." },
        };

        public static string Describe(Error error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (ByMessageKey.TryGetValue(error.UserMessageKey.ToString(), out string? text)) return text;
            return DescribeReason(error.SafeReasonCode);
        }

        public static string DescribeReason(SafeReasonCode code)
        {
            switch (code.ToString())
            {
                case "PermissionDenied":
                    return "You do not have permission for this action.";
                case "InvalidRequest":
                    return "The request is not valid -- check the highlighted values.";
                case "ActionNotAllowed":
                    return "This action is not allowed in the current state.";
                case "TargetUnavailable":
                    return "The target is not available.";
                case "StateChanged":
                    return "The data changed since it was loaded. The view was refreshed -- try again.";
                case "ResourceUnavailable":
                    return "A required resource is not available.";
                case "CapacityReached":
                    return "A limit was reached.";
                case "ApprovalRequired":
                    return "This needs MainGM approval.";
                case "VersionUnsupported":
                    return "This version is not supported.";
                case "DataCorrupted":
                    return "The stored data could not be read.";
                default:
                    return "Something went wrong (" + code + ").";
            }
        }
    }
}
