using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-102 (SLICE-08 block 2): disambiguates an ordinary click on the board's empty area from the
    /// start of a camera-pan drag, from raw pointer-down/move/up pixel positions. Deliberately holds no
    /// <c>UnityEngine</c>/UI Toolkit event type at all -- like <see cref="BoardCamera"/>, it is exercised by
    /// calling its methods directly with plain numbers, not by simulating pointer events.
    /// <see cref="BoardScreenPresenter"/> is this type's only caller: a pointer-down on the empty board
    /// area begins a gesture, each pointer-move feeds it, and pointer-up ends it and reports whether it was
    /// a click. A pointer-down that starts on a token never reaches this type at all (the token's own
    /// event handler stops it from bubbling to the board), so a token click can never be mistaken for the
    /// start of a pan, regardless of any hand tremor during the click.
    /// </summary>
    public sealed class BoardPointerGesture
    {
        /// <summary>
        /// Movement threshold, in screen pixels, below which a pointer-down/up pair is still a click, not
        /// the start of a drag. A handful of pixels is the same order of magnitude several common UI
        /// toolkits use to distinguish a click from a drag (e.g. Windows' own default drag threshold);
        /// anything close to a single pixel would misfire on ordinary hand tremor during a real click,
        /// while a much larger threshold would make short, deliberate pan gestures feel like clicks.
        /// </summary>
        public const double DragThresholdPixels = 5.0;

        private bool _isActive;
        private bool _isDragging;
        private double _downX;
        private double _downY;
        private double _lastX;
        private double _lastY;

        /// <summary>True from the first <see cref="Move"/> call whose accumulated movement from the down-point exceeds <see cref="DragThresholdPixels"/>, until <see cref="End"/> or <see cref="Cancel"/>.</summary>
        public bool IsDragging => _isDragging;

        /// <summary>Starts tracking a new gesture at the pointer-down position.</summary>
        public void Begin(double x, double y)
        {
            _isActive = true;
            _isDragging = false;
            _downX = x;
            _downY = y;
            _lastX = x;
            _lastY = y;
        }

        /// <summary>
        /// Feeds a new pointer position into the gesture. Once the movement accumulated from the down-point
        /// crosses <see cref="DragThresholdPixels"/>, this becomes (and stays) a drag; from that call
        /// onwards, <paramref name="deltaX"/>/<paramref name="deltaY"/> is the pixel movement since the
        /// previous <see cref="Move"/> call (for the caller to pan the camera by exactly that much) and the
        /// method returns <c>true</c>. Before the threshold is crossed, or when no gesture is active, it
        /// returns <c>false</c> and the deltas are zero.
        /// </summary>
        public bool Move(double x, double y, out double deltaX, out double deltaY)
        {
            deltaX = 0.0;
            deltaY = 0.0;
            if (!_isActive) return false;

            if (!_isDragging)
            {
                double fromDownX = x - _downX;
                double fromDownY = y - _downY;
                if (Math.Sqrt(fromDownX * fromDownX + fromDownY * fromDownY) >= DragThresholdPixels)
                {
                    _isDragging = true;
                }
            }

            bool producedDelta = _isDragging;
            if (producedDelta)
            {
                deltaX = x - _lastX;
                deltaY = y - _lastY;
            }

            _lastX = x;
            _lastY = y;
            return producedDelta;
        }

        /// <summary>
        /// Ends the gesture. Returns <c>true</c> when the gesture never crossed the drag threshold -- i.e.
        /// the caller should treat the pointer-up position as an ordinary click -- and <c>false</c> when it
        /// was a pan (or when no gesture was active).
        /// </summary>
        public bool End()
        {
            bool wasClick = _isActive && !_isDragging;
            _isActive = false;
            _isDragging = false;
            return wasClick;
        }

        /// <summary>Aborts the current gesture without reporting a click (e.g. pointer capture was lost).</summary>
        public void Cancel()
        {
            _isActive = false;
            _isDragging = false;
        }
    }
}
