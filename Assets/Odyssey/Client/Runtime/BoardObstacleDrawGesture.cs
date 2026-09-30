using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 1: the pure C# state of a left-button drag on the board while a drawing tool
    /// (wall/door/window) is active -- structurally the same shape as <see cref="BoardBoxSelectGesture"/>
    /// (both corners tracked at once, board-local pixels only, converting to world coordinates is the
    /// caller's job through <see cref="BoardCamera"/>), but reports a two-point segment rather than a
    /// normalised box. Deliberately holds no UnityEngine/UI Toolkit type at all, so it is exercised by
    /// calling its methods directly with plain numbers, not by simulating pointer events.
    /// </summary>
    public sealed class BoardObstacleDrawGesture
    {
        private bool _isActive;
        private double _startX;
        private double _startY;
        private double _currentX;
        private double _currentY;

        /// <summary>True from <see cref="Begin"/> until <see cref="End"/> or <see cref="Cancel"/>.</summary>
        public bool IsActive => _isActive;

        public void Begin(double x, double y)
        {
            _isActive = true;
            _startX = x;
            _startY = y;
            _currentX = x;
            _currentY = y;
        }

        /// <summary>Feeds a pointer position. Has no effect when no gesture is active.</summary>
        public void Move(double x, double y)
        {
            if (!_isActive) return;
            _currentX = x;
            _currentY = y;
        }

        /// <summary>The segment's current start/end board-local pixel positions, or <c>false</c> when no gesture is active.</summary>
        public bool TryGetSegment(out double startX, out double startY, out double currentX, out double currentY)
        {
            startX = _startX;
            startY = _startY;
            currentX = _currentX;
            currentY = _currentY;
            return _isActive;
        }

        /// <summary>
        /// Ends the gesture and reports its final segment. Returns <c>true</c> when the pixel distance
        /// between the start and end points reaches or exceeds <see cref="BoardPointerGesture.DragThresholdPixels"/>
        /// (the caller should create an obstacle for it); <c>false</c> when the movement was too short (an
        /// accidental click -- the caller creates nothing), or when no gesture was active.
        /// </summary>
        public bool End(out double startX, out double startY, out double endX, out double endY)
        {
            startX = _startX;
            startY = _startY;
            endX = _currentX;
            endY = _currentY;
            bool longEnough = _isActive && Distance(_startX, _startY, _currentX, _currentY) >= BoardPointerGesture.DragThresholdPixels;
            _isActive = false;
            return longEnough;
        }

        /// <summary>Aborts the current gesture; the caller creates nothing.</summary>
        public void Cancel()
        {
            _isActive = false;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
