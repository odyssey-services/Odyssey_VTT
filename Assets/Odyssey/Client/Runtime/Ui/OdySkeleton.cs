using System;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-220 (polish P1 item 10): a skeleton placeholder -- simple grey bars where content will appear -- for
    /// content that waits for an asynchronous answer, with an optional soft shimmer. Technique from the owner's earlier
    /// prototype (<c>Odyssey_System</c>, <c>.cp-skel</c>); built anew with UI Toolkit: USS cannot run keyframe
    /// animations, so the shimmer is a class toggled every <see cref="ShimmerHalfPeriodMs"/> with a USS transition on
    /// the bars' colour. Decorative: under <see cref="OdyClasses.ReducedMotion"/> it does not toggle (and the USS
    /// transition is off), leaving still grey bars.
    /// <para>
    /// Not wired into a screen yet: every client read is currently synchronous on the main thread, so nothing ever waits
    /// a frame for data and a placeholder could never be painted (see the task contract). Ready for the first
    /// asynchronous / network load.
    /// </para>
    /// </summary>
    public sealed class OdySkeleton
    {
        /// <summary>Half of the 1.3 s shimmer cycle named in the polish P1 request for the prototype's skeleton.</summary>
        public const long ShimmerHalfPeriodMs = 650;

        private IVisualElementScheduledItem? _ticker;
        private double _elapsedMs;

        /// <param name="lines">Number of placeholder bars; the last one is shorter, like the end of a paragraph.</param>
        public OdySkeleton(string name, int lines = 3)
        {
            if (lines < 1) throw new ArgumentOutOfRangeException(nameof(lines));
            Element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Element.AddToClassList(OdyClasses.Skeleton);
            for (int index = 0; index < lines; index++)
            {
                var bar = new VisualElement { name = name + "-bar-" + index, pickingMode = PickingMode.Ignore };
                bar.AddToClassList(OdyClasses.SkeletonBar);
                if (index == lines - 1 && lines > 1) bar.AddToClassList(OdyClasses.SkeletonBarShort);
                Element.Add(bar);
            }
        }

        public VisualElement Element { get; }

        /// <summary>Whether the bars are in the bright half of the shimmer.</summary>
        public bool IsBright => Element.ClassListContains(OdyClasses.SkeletonBright);

        public int LineCount => Element.childCount;

        /// <summary>Starts the shimmer (runs only while the element is on a panel).</summary>
        public void StartShimmer()
        {
            if (_ticker == null) _ticker = Element.schedule.Execute(timer => Advance(timer.deltaTime)).Every(OdyMotion.FrameIntervalMs);
        }

        public void StopShimmer()
        {
            _ticker?.Pause();
            _ticker = null;
            _elapsedMs = 0;
            Element.RemoveFromClassList(OdyClasses.SkeletonBright);
        }

        /// <summary>Steps the shimmer by <paramref name="elapsedMs"/>; public so tests step it. No-op under reduced motion.</summary>
        public void Advance(double elapsedMs)
        {
            if (elapsedMs <= 0.0 || double.IsNaN(elapsedMs)) return;
            if (OdyMotion.IsReducedMotion(Element))
            {
                Element.RemoveFromClassList(OdyClasses.SkeletonBright);
                return;
            }

            _elapsedMs += elapsedMs;
            while (_elapsedMs >= ShimmerHalfPeriodMs)
            {
                _elapsedMs -= ShimmerHalfPeriodMs;
                Element.EnableInClassList(OdyClasses.SkeletonBright, !IsBright);
            }
        }
    }
}
