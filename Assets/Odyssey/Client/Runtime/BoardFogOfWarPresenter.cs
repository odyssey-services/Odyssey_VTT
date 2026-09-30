using System;
using System.Collections.Generic;
using Odyssey.Application.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 2: renders the permanent map-memory fog of war -- a single translucent-black
    /// <see cref="VisualElement"/> covering the whole board, with a hole cut out (via
    /// <see cref="Painter2D"/>'s odd-even fill rule: draw the board rectangle, then draw each reveal circle
    /// with the same winding, then fill once with <see cref="FillRule.OddEven"/> -- any point covered by an
    /// odd number of the drawn shapes is filled, so a point inside exactly one reveal circle is left
    /// unfilled) for every <see cref="FogRevealRecord"/> the caller currently knows about. No pixel-perfect
    /// boundary accuracy is attempted (task contract section 4) -- a circle per reveal is a readable
    /// approximation, not a lit/unlit tile grid. Kept as its own small class rather than inlined into
    /// <see cref="BoardScreenPresenter"/>, by precedent of <see cref="BoardToolbarPresenter"/> (task contract
    /// section 6: a preferred factoring, not a required one).
    ///
    /// Owns no repository/authorization logic of its own -- the caller is responsible for only ever passing
    /// it reveals it already obtained through the authorized
    /// <see cref="Odyssey.Application.Board.PlayerVisibilityService.ListExploredReveals"/> service method,
    /// never a raw <see cref="IFogOfWarRepository.ListReveals"/> read. This class only draws whatever circles
    /// it is given.
    /// </summary>
    internal sealed class BoardFogOfWarPresenter
    {
        private const float FogAlpha = 0.82f;

        private readonly VisualElement _element;
        private IReadOnlyList<FogRevealRecord> _reveals = Array.Empty<FogRevealRecord>();
        private BoardCamera? _camera;
        private double _widthPixels;
        private double _heightPixels;
        private bool _visible;

        public BoardFogOfWarPresenter()
        {
            _element = new VisualElement { name = "board-fog-of-war", pickingMode = PickingMode.Ignore };
            _element.style.position = Position.Absolute;
            _element.style.left = 0;
            _element.style.top = 0;
            _element.style.display = DisplayStyle.None;
            _element.generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>The fog overlay element -- callers re-add it to the board area on every Refresh() (a full <c>Clear()</c> discards it like every other overlay).</summary>
        public VisualElement Element => _element;

        /// <summary>Whether the fog is currently meant to be shown -- exposed for tests, not used internally.</summary>
        public bool IsVisible => _visible;

        /// <summary>The reveal circles the fog was last shown with -- exposed for tests, not used internally.</summary>
        public IReadOnlyList<FogRevealRecord> CurrentReveals => _reveals;

        /// <summary>MainGm (or any caller with nothing to hide): no fog at all, board fully visible.</summary>
        public void Hide()
        {
            _visible = false;
            _element.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Recomputes and redraws the fog from <paramref name="reveals"/>, converting each circle's world
        /// coordinates/radius through <paramref name="camera"/> -- called fresh on every
        /// <see cref="BoardScreenPresenter.Refresh"/>, never cached in screen space, so a pan/zoom is picked
        /// up the same way obstacle/token rendering already is.
        /// </summary>
        public void Show(IReadOnlyList<FogRevealRecord> reveals, BoardCamera camera, double widthPixels, double heightPixels)
        {
            _visible = true;
            _reveals = reveals ?? Array.Empty<FogRevealRecord>();
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _widthPixels = widthPixels;
            _heightPixels = heightPixels;
            _element.style.display = DisplayStyle.Flex;
            _element.style.width = (float)widthPixels;
            _element.style.height = (float)heightPixels;
            _element.MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!_visible || _camera == null) return;

            Painter2D painter = context.painter2D;
            painter.fillColor = new Color(0f, 0f, 0f, FogAlpha);

            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2((float)_widthPixels, 0f));
            painter.LineTo(new Vector2((float)_widthPixels, (float)_heightPixels));
            painter.LineTo(new Vector2(0f, (float)_heightPixels));
            painter.ClosePath();

            foreach (FogRevealRecord reveal in _reveals)
            {
                float cx = (float)_camera.ToPixelsX(reveal.CenterX);
                float cy = (float)_camera.ToPixelsY(reveal.CenterY);
                float radius = (float)(reveal.Radius * _camera.Scale);
                if (radius <= 0f) continue;

                painter.MoveTo(new Vector2(cx + radius, cy));
                painter.Arc(new Vector2(cx, cy), radius, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.ClosePath();
            }

            painter.Fill(FillRule.OddEven);
        }
    }
}
