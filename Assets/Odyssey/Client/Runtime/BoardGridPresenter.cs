using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-226: a faint reference grid over the board, one line per game cell (<see cref="CellWorldUnits"/> world
    /// units), drawn in the same camera-driven pixel math as tokens/obstacles/the map background -- its lines are
    /// computed from <see cref="BoardCamera"/>'s current offset/scale on every <see cref="Show"/> call, so a pan or
    /// zoom that repositions the rest of the scene repositions the grid identically, without any separate logic of
    /// its own. Deliberately added only after the base pan/zoom defect (the rest of the scene not moving together)
    /// was fixed: a grid that does not track the scene would be actively misleading, not merely cosmetic.
    /// Picking-ignoring, purely visual, below tokens/obstacles in z-order (first child after the background).
    /// Kept as its own small class by exact precedent of <see cref="BoardFogOfWarPresenter"/>/<see cref="BoardToolbarPresenter"/>.
    /// </summary>
    internal sealed class BoardGridPresenter
    {
        /// <summary>One grid line per world unit -- the same unit <see cref="Domain.Geometry.TokenPosition"/> coordinates already use.</summary>
        public const double CellWorldUnits = 1.0;

        // Low-contrast on purpose (task contract: "barely visible on a dark background").
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.06f);
        private const float LineWidthPixels = 1f;

        private readonly VisualElement _element;
        private BoardCamera? _camera;
        private double _widthPixels;
        private double _heightPixels;
        private bool _visible;

        public BoardGridPresenter()
        {
            _element = new VisualElement { name = "board-grid", pickingMode = PickingMode.Ignore };
            _element.style.position = Position.Absolute;
            _element.style.left = 0;
            _element.style.top = 0;
            _element.generateVisualContent += OnGenerateVisualContent;
        }

        public VisualElement Element => _element;
        public bool IsVisible => _visible;

        /// <summary>(Re)draws the grid for the camera's current state. Called on every Refresh() and after every pan/zoom.</summary>
        public void Show(BoardCamera camera, double widthPixels, double heightPixels)
        {
            _camera = camera;
            _widthPixels = widthPixels;
            _heightPixels = heightPixels;
            _visible = true;
            _element.style.width = (float)widthPixels;
            _element.style.height = (float)heightPixels;
            _element.MarkDirtyRepaint();
        }

        public void Hide()
        {
            _visible = false;
            _element.RemoveFromHierarchy();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (!_visible || _camera == null || _widthPixels <= 0.0 || _heightPixels <= 0.0) return;
            double cellPixels = CellWorldUnits * _camera.Scale;
            if (cellPixels < 2.0) return; // zoomed out far enough that lines would just be visual noise

            Painter2D painter = context.painter2D;
            painter.strokeColor = LineColor;
            painter.lineWidth = LineWidthPixels;
            painter.BeginPath();

            // The world X of pixel 0, rounded down to the grid, converted back to the pixel of that first line --
            // same "world is the source of truth, screen is derived" shape as every other camera-driven element.
            double firstWorldX = System.Math.Floor(_camera.FromPixelsX(0.0) / CellWorldUnits) * CellWorldUnits;
            for (double worldX = firstWorldX; ; worldX += CellWorldUnits)
            {
                double x = _camera.ToPixelsX(worldX);
                if (x > _widthPixels) break;
                painter.MoveTo(new Vector2((float)x, 0f));
                painter.LineTo(new Vector2((float)x, (float)_heightPixels));
            }

            double firstWorldY = System.Math.Floor(_camera.FromPixelsY(0.0) / CellWorldUnits) * CellWorldUnits;
            for (double worldY = firstWorldY; ; worldY += CellWorldUnits)
            {
                double y = _camera.ToPixelsY(worldY);
                if (y > _heightPixels) break;
                painter.MoveTo(new Vector2(0f, (float)y));
                painter.LineTo(new Vector2((float)_widthPixels, (float)y));
            }

            painter.Stroke();
        }
    }
}
