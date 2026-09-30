using System;
using System.Collections.Generic;
using Odyssey.Domain.Identity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 3: the token-facing/FOV/view-distance inspector panel, plus the
    /// independent cover-preview control -- a small presenter class, <see cref="BoardToolbarPresenter"/>/
    /// <see cref="BoardFogOfWarPresenter"/>'s own precedent, rather than growing
    /// <see cref="BoardScreenPresenter"/>'s already-large obstacle-inspector-inline shape further.
    ///
    /// Owns no repository/authorization/server-call logic of its own: every button here fires a plain
    /// callback with the raw field values, and the caller (<see cref="BoardScreenPresenter"/>) is the
    /// only place that ever talks to <c>TokenVisionService</c>/<c>CoverSuggestionService</c> or decides
    /// what is authorized -- this class only reads/writes UI Toolkit fields.
    ///
    /// Facing and FOV/view-distance are applied through two separate buttons (task contract section
    /// 2.1's own "if both changed at once, two separate calls, not one combined" instruction) -- a
    /// deliberate design choice over a single combined button, since it maps 1:1 onto the two distinct
    /// server commands (<c>SetTokenFacing</c> is owner-or-MainGM, <c>SetTokenVisionParameters</c> is
    /// MainGM-only) without this class ever needing to detect "which field actually changed."
    ///
    /// The cover-preview target list identifies a token by its own <see cref="TokenId"/> string (task
    /// contract section 2.3: use whatever identifies a token to a human if such a convention exists;
    /// recon found none anywhere in this client -- <see cref="Odyssey.Application.Persistence.TokenRecord"/>
    /// has no display-name field at all -- so the <see cref="TokenId"/> itself is what is shown,
    /// disclosed as this task's own design decision).
    /// </summary>
    internal sealed class BoardTokenInspectorPresenter
    {
        private readonly Action<double> _onApplyFacing;
        private readonly Action<double, double> _onApplyVisionParameters;
        private readonly Action<TokenId> _onCheckCover;

        private readonly VisualElement _element;
        private readonly FloatField _facingField;
        private readonly Button _applyFacingButton;
        private readonly FloatField _fovField;
        private readonly FloatField _viewDistanceField;
        private readonly Button _applyVisionParametersButton;
        private readonly DropdownField _coverTargetDropdown;
        private readonly Button _checkCoverButton;
        private readonly Label _coverResultLabel;

        private List<TokenId> _coverTargetTokenIds = new List<TokenId>();

        public BoardTokenInspectorPresenter(Action<double> onApplyFacing, Action<double, double> onApplyVisionParameters, Action<TokenId> onCheckCover)
        {
            _onApplyFacing = onApplyFacing ?? throw new ArgumentNullException(nameof(onApplyFacing));
            _onApplyVisionParameters = onApplyVisionParameters ?? throw new ArgumentNullException(nameof(onApplyVisionParameters));
            _onCheckCover = onCheckCover ?? throw new ArgumentNullException(nameof(onCheckCover));

            _element = new VisualElement { name = "token-vision-inspector" };
            _element.style.position = Position.Absolute;
            _element.style.backgroundColor = new StyleColor(new Color(0.10f, 0.10f, 0.12f, 0.92f));
            _element.style.paddingLeft = 4;
            _element.style.paddingRight = 4;
            _element.style.paddingTop = 2;
            _element.style.paddingBottom = 2;
            _element.style.flexDirection = FlexDirection.Column;

            VisualElement facingRow = new VisualElement { name = "token-facing-row" };
            facingRow.style.flexDirection = FlexDirection.Row;
            _facingField = new FloatField("Facing") { name = "token-facing-field" };
            _facingField.style.width = 110;
            facingRow.Add(_facingField);
            _applyFacingButton = new Button(() => _onApplyFacing(_facingField.value)) { name = "token-apply-facing-button", text = "Apply Facing" };
            facingRow.Add(_applyFacingButton);
            _element.Add(facingRow);

            VisualElement visionRow = new VisualElement { name = "token-vision-parameters-row" };
            visionRow.style.flexDirection = FlexDirection.Row;
            _fovField = new FloatField("FOV") { name = "token-fov-field" };
            _fovField.style.width = 90;
            visionRow.Add(_fovField);
            _viewDistanceField = new FloatField("Range") { name = "token-view-distance-field" };
            _viewDistanceField.style.width = 90;
            visionRow.Add(_viewDistanceField);
            _applyVisionParametersButton = new Button(() => _onApplyVisionParameters(_fovField.value, _viewDistanceField.value)) { name = "token-apply-vision-parameters-button", text = "Apply Vision" };
            visionRow.Add(_applyVisionParametersButton);
            _element.Add(visionRow);

            VisualElement coverRow = new VisualElement { name = "token-cover-row" };
            coverRow.style.flexDirection = FlexDirection.Row;
            _coverTargetDropdown = new DropdownField("Cover target") { name = "token-cover-target-dropdown" };
            _coverTargetDropdown.style.width = 160;
            coverRow.Add(_coverTargetDropdown);
            _checkCoverButton = new Button(OnCheckCoverButtonClicked) { name = "token-check-cover-button", text = "Check Cover" };
            coverRow.Add(_checkCoverButton);
            _element.Add(coverRow);

            _coverResultLabel = new Label { name = "token-cover-result" };
            _element.Add(_coverResultLabel);
        }

        /// <summary>The panel element -- callers re-add it to the board area on every Refresh() (a full <c>Clear()</c> discards it like every other overlay).</summary>
        public VisualElement Element => _element;

        /// <summary>Sets the three fields from the token's real, currently-stored vision settings. Call only when the inspected token changes -- not on every Refresh() -- so an in-progress, not-yet-applied edit is never silently overwritten by an unrelated Refresh() (e.g. another participant moving elsewhere, or this board's own fog recompute).</summary>
        public void SetValues(double facingDegrees, double fovAngleDegrees, double viewDistance)
        {
            _facingField.SetValueWithoutNotify((float)facingDegrees);
            _fovField.SetValueWithoutNotify((float)fovAngleDegrees);
            _viewDistanceField.SetValueWithoutNotify((float)viewDistance);
        }

        /// <summary>Presentational gates only -- the server re-checks owner-or-MainGM/MainGM-only regardless, exactly as every other command in this class already does.</summary>
        public void SetEditable(bool facingEditable, bool visionParametersEditable)
        {
            _facingField.SetEnabled(facingEditable);
            _applyFacingButton.SetEnabled(facingEditable);
            _fovField.SetEnabled(visionParametersEditable);
            _viewDistanceField.SetEnabled(visionParametersEditable);
            _applyVisionParametersButton.SetEnabled(visionParametersEditable);
        }

        /// <summary>
        /// Repopulates the cover-target dropdown from the current token set (every other token in the
        /// Scene, excluding the inspected one) -- safe to call on every Refresh() (unlike
        /// <see cref="SetValues"/>) since it is a selection list, not a free-form edit that could be
        /// silently clobbered; the currently chosen target is preserved across a repopulation when it
        /// is still present in the new list.
        /// </summary>
        public void SetCoverTargets(IReadOnlyList<TokenId> targetTokenIds)
        {
            string previouslySelected = _coverTargetDropdown.value;
            _coverTargetTokenIds = new List<TokenId>(targetTokenIds);
            var choices = new List<string>(_coverTargetTokenIds.Count);
            foreach (TokenId tokenId in _coverTargetTokenIds)
            {
                choices.Add(tokenId.ToString());
            }

            _coverTargetDropdown.choices = choices;
            if (!string.IsNullOrEmpty(previouslySelected) && choices.Contains(previouslySelected))
            {
                _coverTargetDropdown.SetValueWithoutNotify(previouslySelected);
            }
            else
            {
                _coverTargetDropdown.SetValueWithoutNotify(choices.Count > 0 ? choices[0] : string.Empty);
            }
        }

        public void SetCoverResult(string text)
        {
            _coverResultLabel.text = text;
        }

        public void PositionAt(double x, double y)
        {
            _element.style.left = (float)x;
            _element.style.top = (float)y;
        }

        private void OnCheckCoverButtonClicked()
        {
            string selected = _coverTargetDropdown.value;
            if (string.IsNullOrEmpty(selected)) return;

            for (int index = 0; index < _coverTargetTokenIds.Count; index++)
            {
                if (_coverTargetTokenIds[index].ToString() == selected)
                {
                    _onCheckCover(_coverTargetTokenIds[index]);
                    return;
                }
            }
        }
    }
}
