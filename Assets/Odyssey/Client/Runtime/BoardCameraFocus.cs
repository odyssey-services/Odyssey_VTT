using System;
using Odyssey.Application.Persistence;

namespace Odyssey.Unity.Client
{
    public enum BoardFocusOutcome
    {
        /// <summary>The camera is easing towards the token.</summary>
        Started = 1,

        /// <summary>The token is already comfortably in frame; the camera does not move.</summary>
        AlreadyInFrame = 2,

        /// <summary>No rendered token for that character on this board.</summary>
        NotOnBoard = 3,

        /// <summary>The token exists but is hidden from the local actor; focusing would reveal where it is.</summary>
        Hidden = 4
    }

    /// <summary>
    /// ODY-S11-214 (polish P0 item 5): eases the board camera so a token ends up in frame (centered), stepped with
    /// elapsed milliseconds like every other client animation (<see cref="OdyTween"/>). Pure state over a
    /// <see cref="BoardCamera"/>; any manual camera input cancels it (<see cref="Cancel"/>). Zoom is never changed.
    /// </summary>
    public sealed class BoardCameraFocus
    {
        /// <summary>
        /// A token closer than this fraction of the view to an edge counts as "not in frame". Executor's choice, not a
        /// value from any reference product.
        /// </summary>
        public const double InFrameInsetFraction = 0.15;

        /// <summary>Camera move duration. Executor's choice (slightly longer than a token move: the whole view moves).</summary>
        public const double DurationMs = 300.0;

        private OdyTween? _x;
        private OdyTween? _y;

        public bool IsActive => _x != null && _y != null && !(_x.IsDone && _y.IsDone);

        /// <summary>Whether <paramref name="position"/> lies inside the view shrunk by <see cref="InFrameInsetFraction"/> on each side.</summary>
        public static bool IsInFrame(BoardCamera camera, TokenPosition position, double viewWidth, double viewHeight)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            double x = camera.ToPixelsX(position.X);
            double y = camera.ToPixelsY(position.Y);
            double insetX = viewWidth * InFrameInsetFraction;
            double insetY = viewHeight * InFrameInsetFraction;
            return x >= insetX && x <= viewWidth - insetX && y >= insetY && y <= viewHeight - insetY;
        }

        /// <summary>Starts easing the camera so <paramref name="position"/> ends at the center of the view.</summary>
        public void Start(BoardCamera camera, TokenPosition position, double viewWidth, double viewHeight, double durationMs = DurationMs)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            double targetOffsetX = position.X - viewWidth / 2.0 / camera.Scale;
            double targetOffsetY = position.Y - viewHeight / 2.0 / camera.Scale;
            _x = new OdyTween(camera.OffsetX, targetOffsetX, durationMs);
            _y = new OdyTween(camera.OffsetY, targetOffsetY, durationMs);
        }

        public void Cancel()
        {
            _x = null;
            _y = null;
        }

        /// <summary>Steps the move and applies it to <paramref name="camera"/> (as a pan). Returns whether it still runs.</summary>
        public bool Advance(BoardCamera camera, double elapsedMs)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (_x == null || _y == null) return false;
            _x.Advance(elapsedMs);
            _y.Advance(elapsedMs);
            // BoardCamera.Pan subtracts delta/scale from the offset, so this delta moves the offset to the tween value.
            camera.Pan((camera.OffsetX - _x.Current) * camera.Scale, (camera.OffsetY - _y.Current) * camera.Scale);
            bool running = !(_x.IsDone && _y.IsDone);
            if (!running) Cancel();
            return running;
        }
    }
}
