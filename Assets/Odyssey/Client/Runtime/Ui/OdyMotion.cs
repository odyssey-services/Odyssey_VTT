using System;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-211 (polish P0 item 2): "local action instant, remote action animated". What the local user does
    /// themselves is shown immediately, 1:1; a change that arrives from elsewhere (another participant, another panel,
    /// a reload after a conflict) eases from the last shown value to the new one so the eye can follow it.
    /// Presentation only: the authoritative value is already applied; the tween only changes what is drawn meanwhile.
    /// </summary>
    public static class OdyMotion
    {
        /// <summary>
        /// Duration of a remote-change animation. A starting point inside the 150-250 ms range requested by the product
        /// owner, chosen by the executor; it is not a value published by Owlbear Rodeo.
        /// </summary>
        public const double RemoteUpdateDurationMs = 200.0;

        /// <summary>Tick interval of the UI Toolkit scheduler that drives running tweens (~60 Hz).</summary>
        public const long FrameIntervalMs = 16;

        /// <summary>
        /// ODY-S11-216: decorative motion (marching ants, skeleton shimmer) stops when any ancestor carries
        /// <see cref="OdyClasses.ReducedMotion"/> -- the hook the later reduced-motion setting (polish P2) only has to set
        /// on the screen. Walks the element's own parents; no global state.
        /// </summary>
        public static bool IsReducedMotion(UnityEngine.UIElements.VisualElement element)
        {
            for (UnityEngine.UIElements.VisualElement? current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains(OdyClasses.ReducedMotion)) return true;
            }

            return false;
        }

        /// <summary>Ease-out cubic: fast start, gentle arrival.</summary>
        public static double EaseOut(double progress)
        {
            double t = progress <= 0.0 ? 0.0 : progress >= 1.0 ? 1.0 : progress;
            double inverse = 1.0 - t;
            return 1.0 - inverse * inverse * inverse;
        }
    }

    /// <summary>
    /// One eased value from <see cref="From"/> to <see cref="To"/>. Stepped explicitly with elapsed milliseconds (the
    /// UI Toolkit scheduler's tick delta at runtime, fixed steps in tests); it never reads a clock itself.
    /// </summary>
    public sealed class OdyTween
    {
        public OdyTween(double from, double to, double durationMs)
        {
            if (double.IsNaN(from) || double.IsNaN(to)) throw new ArgumentException("Tween endpoints must be numbers.");
            From = from;
            To = to;
            DurationMs = durationMs > 0.0 ? durationMs : 0.0;
        }

        public double From { get; }
        public double To { get; }
        public double DurationMs { get; }
        public double ElapsedMs { get; private set; }
        public double Progress => DurationMs <= 0.0 ? 1.0 : Math.Min(1.0, ElapsedMs / DurationMs);
        public bool IsDone => Progress >= 1.0;
        public double Current => IsDone ? To : From + (To - From) * OdyMotion.EaseOut(Progress);

        public void Advance(double elapsedMs)
        {
            if (elapsedMs > 0.0 && !double.IsNaN(elapsedMs)) ElapsedMs = Math.Min(DurationMs, ElapsedMs + elapsedMs);
        }
    }
}
