using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-107: the pure C# state of a left-button drag on the empty board -- a click (movement below
    /// <see cref="BoardPointerGesture.DragThresholdPixels"/>) or a rubber-band selection box. Separate from
    /// <see cref="BoardPointerGesture"/> because a box needs both corners at once (where the press started
    /// and where the pointer is now), while <see cref="BoardPointerGesture"/> only reports movement deltas.
    /// All coordinates are board-local pixels; converting the finished box to world coordinates is the
    /// caller's job (through <see cref="BoardCamera"/>), so this type knows nothing about the camera.
    /// </summary>
    public sealed class BoardBoxSelectGesture
    {
        private bool _isActive;
        private bool _isDragging;
        private double _startX;
        private double _startY;
        private double _currentX;
        private double _currentY;

        public bool IsActive => _isActive;

        /// <summary>True once the pointer has moved at least the drag threshold away from the press point (and stays true until the gesture ends).</summary>
        public bool IsDragging => _isDragging;

        public void Begin(double x, double y)
        {
            _isActive = true;
            _isDragging = false;
            _startX = x;
            _startY = y;
            _currentX = x;
            _currentY = y;
        }

        /// <summary>Feeds a pointer position. Returns <see cref="IsDragging"/> after the update.</summary>
        public bool Move(double x, double y)
        {
            if (!_isActive) return false;
            _currentX = x;
            _currentY = y;
            if (!_isDragging)
            {
                double dx = x - _startX;
                double dy = y - _startY;
                if (Math.Sqrt(dx * dx + dy * dy) >= BoardPointerGesture.DragThresholdPixels) _isDragging = true;
            }

            return _isDragging;
        }

        /// <summary>
        /// The current box, normalised so min &lt;= max on both axes, or false when the gesture is not a
        /// drag (inactive, or still below the threshold).
        /// </summary>
        public bool TryGetBox(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = Math.Min(_startX, _currentX);
            minY = Math.Min(_startY, _currentY);
            maxX = Math.Max(_startX, _currentX);
            maxY = Math.Max(_startY, _currentY);
            return _isActive && _isDragging;
        }

        /// <summary>Ends the gesture. Returns true when it was a click (never became a drag).</summary>
        public bool End()
        {
            bool wasClick = _isActive && !_isDragging;
            _isActive = false;
            _isDragging = false;
            return wasClick;
        }

        public void Cancel()
        {
            _isActive = false;
            _isDragging = false;
        }
    }
}
